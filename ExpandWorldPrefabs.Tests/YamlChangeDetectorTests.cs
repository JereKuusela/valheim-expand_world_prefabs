using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

public class YamlChangeDetectorTests
{
  private string directory = "";
  private readonly List<YamlChanges> served = [];
  private bool applied;
  private object? oldLogger;
  private static readonly System.Reflection.FieldInfo LoggerField =
    typeof(Log).GetField("Logger", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

  [SetUp]
  public void SetUp()
  {
    directory = Path.Combine(Path.GetTempPath(), $"ewp_detector_{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    served.Clear();
    applied = true;
    oldLogger = LoggerField.GetValue(null);
    Log.Init(new ManualLogSource("DetectorTests"));
  }

  [TearDown]
  public void TearDown()
  {
    LoggerField.SetValue(null, oldLogger);
    Directory.Delete(directory, true);
  }

  private YamlChangeDetector Create(double quiet = 10)
  {
    var detector = new YamlChangeDetector(changes => { served.Add(changes); return applied; }, () => quiet);
    detector.AddSource(directory, "*.yaml");
    return detector;
  }

  private string Write(string name, string content)
  {
    var path = Path.GetFullPath(Path.Combine(directory, name));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content);
    return path;
  }

  [Test]
  public void WaitsForQuietPeriodSinceLastEvent()
  {
    var detector = Create();
    var path = Write("a.yaml", "a");
    detector.Queue(path, 0);
    detector.Queue(path, 8);
    detector.Update(17.9);
    Assert.That(served, Is.Empty);
    detector.Update(18.1);
    Assert.That(served, Has.Count.EqualTo(1));
    Assert.That(detector.PendingCount, Is.Zero);
  }

  [Test]
  public void RepeatedEventsDoNotAddNewPaths()
  {
    var detector = Create();
    detector.Queue("file.yaml", 0);
    detector.Queue("FILE.YAML", 2);
    Assert.That(detector.PendingCount, Is.EqualTo(1));
  }

  [Test]
  public void ReportsAtFiveFifteenThenEveryFifteenSeconds()
  {
    var detector = Create();
    detector.Queue("file.yaml", 0);
    Assert.That(detector.ShouldReportDelay(4.9), Is.False);
    Assert.That(detector.ShouldReportDelay(5.1), Is.True);
    Assert.That(detector.ShouldReportDelay(6), Is.False);
    Assert.That(detector.ShouldReportDelay(15.1), Is.True);
    Assert.That(detector.ShouldReportDelay(29), Is.False);
    Assert.That(detector.ShouldReportDelay(30.1), Is.True);
    Assert.That(detector.ShouldReportDelay(45.1), Is.True);
  }

  [Test]
  public void ContinuousEventsCanPostponeIndefinitely()
  {
    var detector = Create();
    for (var i = 0; i < 1000; i++)
    {
      detector.Queue("file.yaml", i * 5);
      detector.Update(i * 5 + 1);
    }
    Assert.That(served, Is.Empty);
  }

  [Test]
  public void EditedFileIsServedOnceThenUnchanged()
  {
    var detector = Create();
    var path = Write("a.yaml", "first");
    detector.Queue(path, 0);
    detector.Update(11);
    Assert.That(served[0].Changed, Has.Count.EqualTo(1));
    Assert.That(served[0].Changed[0].Content, Is.EqualTo("first"));

    detector.Queue(path, 20);
    detector.Update(31);
    Assert.That(served[1].Changed, Is.Empty);
    Assert.That(served[1].Unchanged, Is.EqualTo(1));

    File.WriteAllText(path, "second");
    detector.Queue(path, 40);
    detector.Update(51);
    Assert.That(served[2].Changed[0].Content, Is.EqualTo("second"));
  }

  [Test]
  public void HashIsOnlyAcknowledgedWhenCallbackSucceeds()
  {
    var detector = Create();
    var path = Write("a.yaml", "first");
    applied = false;
    detector.Queue(path, 0);
    detector.Update(11);
    applied = true;
    detector.Queue(path, 20);
    detector.Update(31);
    Assert.That(served[1].Changed, Has.Count.EqualTo(1), "Unacknowledged content is served again.");
  }

  [Test]
  public void DeletedFileIsReportedOnce()
  {
    var detector = Create();
    var path = Write("a.yaml", "first");
    detector.TakeAll();
    File.Delete(path);
    detector.Queue(path, 0);
    detector.Update(11);
    Assert.That(served[0].Deleted, Is.EqualTo(new[] { path }));

    detector.Queue(path, 20);
    detector.Update(31);
    Assert.That(served[1].Deleted, Is.Empty);
  }

  [Test]
  public void UnreportedAddsAndFolderDeletesAreFoundByScan()
  {
    var detector = Create();
    var known = Write(Path.Combine("sub", "known.yaml"), "known");
    detector.TakeAll();
    var added = Write("added.yaml", "added");
    Directory.Delete(Path.GetDirectoryName(known)!, true);

    detector.Queue(Path.Combine(directory, "other.yaml"), 0);
    detector.Update(11);
    Assert.That(served[0].Changed.ConvertAll(file => file.Path), Is.EqualTo(new[] { added }));
    Assert.That(served[0].Deleted, Is.EqualTo(new[] { known }));
    Assert.That(served[0].Files, Is.EqualTo(new[] { added }));
  }

  [Test]
  public void TakeAllReturnsEverythingAndCancelsPending()
  {
    var detector = Create();
    var a = Write("a.yaml", "a");
    var b = Write("b.yaml", "b");
    detector.TakeAll();
    detector.Queue(a, 0);
    var all = detector.TakeAll();
    Assert.That(detector.PendingCount, Is.Zero);
    Assert.That(all.Changed, Has.Count.EqualTo(2));
    Assert.That(all.Files, Is.EquivalentTo(new[] { a, b }));
    detector.Update(100);
    Assert.That(served, Is.Empty);
  }

  [Test]
  public void ReadFailureIsReportedAndNotAcknowledged()
  {
    var detector = Create();
    var path = Write("a.yaml", "a");
    using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
    {
      var locked = detector.TakeAll();
      Assert.That(locked.Failed, Has.Count.EqualTo(1));
      Assert.That(locked.Changed, Is.Empty);
    }
    Assert.That(detector.TakeAll().Changed, Has.Count.EqualTo(1));
  }

  [Test]
  public void WatcherLifecycleIsOwnedByDetector()
  {
    var detector = Create();
    detector.StartWatching();
    detector.Queue("pending.yaml", 0);
    detector.StopWatching();
    Assert.That(detector.PendingCount, Is.Zero);
    detector.StopWatching();
  }

  [Test]
  public void ParserDoesNotReadFilesFromDisk()
  {
    var missingFile = Path.Combine(directory, "missing.yaml");
    Assert.That(Yaml.ParseMixed(missingFile, "- name: group\n  value: value\n", out var entries, out var error), Is.True, error);
    Assert.That(entries.DataEntries, Has.Count.EqualTo(1));
    Assert.That(File.Exists(missingFile), Is.False);
  }

  [Test]
  public void EmptyYamlListIsValid()
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
}
