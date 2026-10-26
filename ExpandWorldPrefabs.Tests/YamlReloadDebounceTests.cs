using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

public class YamlReloadDebounceTests
{
  [Test]
  public void WaitsForTenSecondsSinceLastEvent()
  {
    var batch = new YamlReloadDebounce();
    batch.Add("first.yaml", 0);
    batch.Add("second.yaml", 8);
    Assert.That(batch.IsReady(17.9, 10), Is.False);
    Assert.That(batch.IsReady(18.1, 10), Is.True);
    Assert.That(batch.Take(), Is.EquivalentTo(new[] { "first.yaml", "second.yaml" }));
    Assert.That(batch.Count, Is.Zero);
  }

  [Test]
  public void RepeatedEventsDoNotAddNewPaths()
  {
    var batch = new YamlReloadDebounce();
    batch.Add("file.yaml", 0);
    batch.Add("FILE.YAML", 2);
    Assert.That(batch.Count, Is.EqualTo(1));
    Assert.That(batch.IsReady(11, 10), Is.False);
    Assert.That(batch.IsReady(12.1, 10), Is.True);
  }

  [Test]
  public void ReportsAtFiveFifteenThenEveryFifteenSeconds()
  {
    var batch = new YamlReloadDebounce();
    batch.Add("file.yaml", 0);
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
    var batch = new YamlReloadDebounce();
    for (var i = 0; i < 1000; i++)
    {
      batch.Add("file.yaml", i * 5);
      Assert.That(batch.IsReady(i * 5 + 1, 10), Is.False);
    }
  }

  [Test]
  public void EmptyYamlListIsValidDuringAutomaticReload()
  {
    Assert.That(Yaml.TryReadMixedFile("empty.yaml", "[]", out var entries, out var error), Is.True, error);
    Assert.That(entries.ScriptEntries, Is.Empty);
    Assert.That(entries.DataEntries, Is.Empty);
    Assert.That(entries.ConfigEntries, Is.Empty);
  }

  [Test]
  public void InvalidYamlIsNotAcceptedAsEmptyRules()
  {
    Assert.That(Yaml.TryReadMixedFile("broken.yaml", "- prefab: Player\n  type: [unclosed", out _, out var error), Is.False);
    Assert.That(error, Is.Not.Empty);
  }

  [Test]
  public void ValidMixedYamlLoadsAllCategories()
  {
    var yaml = "- prefab: Player\n  type: create\n- name: group\n  value: test\n- config: Custom\n  key: example\n";
    Assert.That(Yaml.TryReadMixedFile("mixed.yaml", yaml, out var entries, out var error), Is.True, error);
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
      Assert.That(FileLoading.TryReadFileSnapshot(file, out var entries, out var hash, out var error), Is.True, error);
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
  public void InvalidInitialSnapshotDoesNotProduceAnAppliedHash()
  {
    var file = Path.Combine(Path.GetTempPath(), $"ewp_snapshot_{Guid.NewGuid():N}.yaml");
    try
    {
      File.WriteAllText(file, "- prefab: Player\n  type: [unclosed");
      Assert.That(FileLoading.TryReadFileSnapshot(file, out _, out var hash, out var error), Is.False);
      Assert.That(hash, Is.Empty);
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
    var batch = new YamlReloadDebounce();
    batch.Add("file.yaml", 0);
    batch.Clear();
    Assert.That(batch.IsReady(100, 10), Is.False);
    Assert.That(batch.Take(), Is.Empty);
  }
}
