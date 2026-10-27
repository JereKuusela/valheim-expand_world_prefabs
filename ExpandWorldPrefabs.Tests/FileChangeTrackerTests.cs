using System.Collections.Generic;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

public class FileChangeTrackerTests
{
  [Test]
  public void WaitsForQuietPeriodSinceLastEvent()
  {
    var batch = new FileChangeTracker();
    batch.Queue("first.yaml", 0);
    batch.Queue("second.yaml", 8);
    Assert.That(batch.IsQuietFor(17.9, 10), Is.False);
    Assert.That(batch.IsQuietFor(18.1, 10), Is.True);
    Assert.That(batch.TakePending(), Is.EquivalentTo(new[] { "first.yaml", "second.yaml" }));
    Assert.That(batch.PendingCount, Is.Zero);
  }

  [Test]
  public void RepeatedEventsDoNotAddNewPaths()
  {
    var batch = new FileChangeTracker();
    batch.Queue("file.yaml", 0);
    batch.Queue("FILE.YAML", 2);
    Assert.That(batch.PendingCount, Is.EqualTo(1));
    Assert.That(batch.IsQuietFor(11, 10), Is.False);
    Assert.That(batch.IsQuietFor(12.1, 10), Is.True);
  }

  [Test]
  public void ReportsAtFiveFifteenThenEveryFifteenSeconds()
  {
    var batch = new FileChangeTracker();
    batch.Queue("file.yaml", 0);
    Assert.That(batch.ShouldReportDelay(4.9), Is.False);
    Assert.That(batch.ShouldReportDelay(5.1), Is.True);
    Assert.That(batch.ShouldReportDelay(6), Is.False);
    Assert.That(batch.ShouldReportDelay(15.1), Is.True);
    Assert.That(batch.ShouldReportDelay(29), Is.False);
    Assert.That(batch.ShouldReportDelay(30.1), Is.True);
    Assert.That(batch.ShouldReportDelay(45.1), Is.True);
  }

  [Test]
  public void ContinuousEventsCanPostponeIndefinitely()
  {
    var batch = new FileChangeTracker();
    for (var i = 0; i < 1000; i++)
    {
      batch.Queue("file.yaml", i * 5);
      Assert.That(batch.IsQuietFor(i * 5 + 1, 10), Is.False);
    }
  }

  [Test]
  public void EmptyYamlListIsValidDuringAutomaticReload()
  {
    Assert.That(Yaml.ParseMixed("empty.yaml", "[]", out var entries, out var error), Is.True, error);
    Assert.That(entries.ScriptEntries, Is.Empty);
    Assert.That(entries.DataEntries, Is.Empty);
    Assert.That(entries.ConfigEntries, Is.Empty);
  }

  [Test]
  public void InvalidYamlIsNotAcceptedAsEmptyRules()
  {
    Assert.That(Yaml.ParseMixed("broken.yaml", "- prefab: Player\n  type: [unclosed", out _, out var error), Is.False);
    Assert.That(error, Is.Not.Empty);
  }

  [Test]
  public void ValidMixedYamlLoadsAllCategories()
  {
    var yaml = "- prefab: Player\n  type: create\n- name: group\n  value: test\n- config: Custom\n  key: example\n";
    Assert.That(Yaml.ParseMixed("mixed.yaml", yaml, out var entries, out var error), Is.True, error);
    Assert.That(entries.ScriptEntries, Has.Count.EqualTo(1));
    Assert.That(entries.DataEntries, Has.Count.EqualTo(1));
    Assert.That(entries.ConfigEntries, Has.Count.EqualTo(1));
  }

  [Test]
  public void InitialSnapshotHashMatchesTheParsedVersion()
  {
    var file = Path.Combine(Path.GetTempPath(), $"ewp_snapshot_{Guid.NewGuid():N}.yaml");
    const string initial = "- name: initial\n  value: first\n";
    try
    {
      File.WriteAllText(file, initial);
      Assert.That(FileChangeTracker.ReadFile(file, out var raw, out var hash, out var error), Is.True, error);
      Assert.That(Yaml.ParseMixed(file, raw, out var entries, out error), Is.True, error);
      File.WriteAllText(file, "- name: changed\n  value: second\n");
      var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(initial)));
      Assert.That(hash, Is.EqualTo(expectedHash));
      Assert.That(entries.DataEntries, Has.Count.EqualTo(1));
      Assert.That(entries.DataEntries[0].name, Is.EqualTo("initial"));
    }
    finally
    {
      if (File.Exists(file)) File.Delete(file);
    }
  }

  [Test]
  public void ParsingFailureDoesNotMeanSnapshotReadFailure()
  {
    var file = Path.Combine(Path.GetTempPath(), $"ewp_snapshot_{Guid.NewGuid():N}.yaml");
    try
    {
      File.WriteAllText(file, "- prefab: Player\n  type: [unclosed");
      Assert.That(FileChangeTracker.ReadFile(file, out var raw, out var hash, out var error), Is.True, error);
      Assert.That(Yaml.ParseMixed(file, raw, out _, out error), Is.False);
      Assert.That(hash, Is.Not.Empty);
      Assert.That(error, Is.Not.Empty);
    }
    finally
    {
      if (File.Exists(file)) File.Delete(file);
    }
  }

  [Test]
  public void ManualReloadCancelsPendingChanges()
  {
    var batch = new FileChangeTracker();
    batch.Queue("file.yaml", 0);
    batch.ClearPending();
    Assert.That(batch.IsQuietFor(100, 10), Is.False);
    Assert.That(batch.TakePending(), Is.Empty);
  }

  [Test]
  public void HashesMustBeExplicitlyCommitted()
  {
    var tracker = new FileChangeTracker();
    var file = Path.Combine(Path.GetTempPath(), $"ewp_raw_{Guid.NewGuid():N}.yaml");
    try
    {
      File.WriteAllText(file, "first");
      Assert.That(FileChangeTracker.ReadFile(file, out var content, out var hash, out var error), Is.True, error);
      Assert.That(content, Is.EqualTo("first"));
      Assert.That(tracker.Contains(file), Is.False);
      Assert.That(tracker.ContainsApplied(file, hash), Is.False);
      // Parsing failures or exceptions leave the old hash in place: no acknowledgement here.
      tracker.Commit(file, hash);
      Assert.That(tracker.Contains(file), Is.True);
      Assert.That(tracker.ContainsApplied(file, hash), Is.True);
      File.WriteAllText(file, "second");
      Assert.That(FileChangeTracker.ReadFile(file, out _, out var newHash, out error), Is.True, error);
      Assert.That(tracker.ContainsApplied(file, newHash), Is.False);
      Assert.That(tracker.ContainsApplied(file, hash), Is.True);
      tracker.Commit(file, newHash);
      Assert.That(tracker.ContainsApplied(file, newHash), Is.True);
      tracker.Commit(file, null);
      Assert.That(tracker.Contains(file), Is.False);
    }
    finally
    {
      if (File.Exists(file)) File.Delete(file);
    }
  }

  [Test]
  public void ReplacingBaselineAndClearingPendingAreIndependent()
  {
    var tracker = new FileChangeTracker();
    tracker.Queue("file.yaml", 0);
    tracker.ReplaceAppliedHashes(new Dictionary<string, string> { ["file.yaml"] = "old" });
    Assert.That(tracker.PendingCount, Is.EqualTo(1));
    tracker.Commit("file.yaml", "updated");
    Assert.That(tracker.PendingCount, Is.EqualTo(1), "Committing a hash must not discard pending events.");
    tracker.Commit("file.yaml", "old");
    Assert.That(tracker.ContainsApplied("FILE.YAML", "old"), Is.True);
    tracker.ClearPending();
    Assert.That(tracker.ContainsApplied("file.yaml", "old"), Is.True);
    tracker.ReplaceAppliedHashes(new Dictionary<string, string> { ["new.yaml"] = "new" });
    Assert.That(tracker.Contains("file.yaml"), Is.False);
    Assert.That(tracker.ContainsApplied("new.yaml", "new"), Is.True);
  }

  [Test]
  public void WatcherLifecycleIsOwnedByTracker()
  {
    var tracker = new FileChangeTracker();
    var directory = Path.Combine(Path.GetTempPath(), $"ewp_watcher_{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
      tracker.Watch(directory, "*.yaml", (path, oldPath) => { });
      tracker.Queue("pending.yaml", 0);
      tracker.StopWatching();
      Assert.That(tracker.PendingCount, Is.Zero);
      // Repeated shutdown is safe.
      tracker.StopWatching();
    }
    finally
    {
      tracker.StopWatching();
      Directory.Delete(directory, true);
    }
  }

  [Test]
  public void ParserDoesNotReadFilesFromDisk()
  {
    var missingFile = Path.Combine(Path.GetTempPath(), $"ewp_missing_{Guid.NewGuid():N}.yaml");
    Assert.That(File.Exists(missingFile), Is.False);
    const string raw = "- name: group\n  value: value\n";
    Assert.That(Yaml.ParseMixed(missingFile, raw, out var entries, out var error), Is.True, error);
    Assert.That(entries.DataEntries, Has.Count.EqualTo(1));
    Assert.That(File.Exists(missingFile), Is.False);
  }

}
