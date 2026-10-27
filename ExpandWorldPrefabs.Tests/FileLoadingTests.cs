using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

[NonParallelizable]
public class FileLoadingTests
{
  private TestPathScope? paths;

  [OneTimeSetUp]
  public void InitializePaths() => paths = new TestPathScope();

  [OneTimeTearDown]
  public void RestorePaths() => paths?.Dispose();

  [Test]
  public void DiscoveredPrefabTriggersPrefabRebuild()
  {
    var directory = FileLoading.BaseDirectory;
    var data = Path.Combine(directory, "expand_data_weather.yaml");
    var prefab = Path.Combine(directory, "expand_prefabs_new.yaml");

    // Only the data file notified the watcher; a directory scan found the prefab file.
    Assert.That(FileLoading.NeedsPrefabRebuild([data], [prefab]), Is.True);
    Assert.That(FileLoading.NeedsPrefabRebuild([data], []), Is.False);
    Assert.That(FileLoading.NeedsPrefabRebuild([prefab], []), Is.True);
  }

  [Test]
  public void AddUnreportedNewFilesCachesUnreportedFilesWithoutPrematureHashCommit()
  {
    var method = typeof(FileLoading).GetMethod("AddUnreportedNewFiles", BindingFlags.NonPublic | BindingFlags.Static)!;
    var cache = (YamlFileCache)typeof(FileLoading).GetField("Cache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    var tracker = (FileChangeTracker)typeof(FileLoading).GetField("Changes", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    var path = Path.Combine(Path.GetTempPath(), "ewp-new-file-" + Guid.NewGuid().ToString("N") + ".yaml");
    try
    {
      File.WriteAllText(path, "- name: newly_discovered\n  value: example\n");
      var added = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      Assert.That(cache.Contains(path), Is.False);
      method.Invoke(null, [new[] { path }, added]);
      Assert.That(cache.Contains(path), Is.True);
      Assert.That(added.ContainsKey(path), Is.True);
      Assert.That(tracker.ContainsApplied(path, added[path]), Is.False,
        "Directory discovery must not commit the hash before loader execution.");
    }
    finally
    {
      cache.Clear();
      File.Delete(path);
    }
  }

  [Test]
  public void UnchangedNotificationStillDiscoversNewPrefabFiles()
  {
    const BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    var readChanges = typeof(FileLoading).GetMethod("ReadChanges", staticPrivate)!;
    var addNewFiles = typeof(FileLoading).GetMethod("AddUnreportedNewFiles", staticPrivate)!;
    var cache = (YamlFileCache)typeof(FileLoading).GetField("Cache", staticPrivate)!.GetValue(null)!;
    var tracker = (FileChangeTracker)typeof(FileLoading).GetField("Changes", staticPrivate)!.GetValue(null)!;
    var id = Guid.NewGuid().ToString("N");
    var known = Path.Combine(FileLoading.BaseDirectory, "expand_data_known_" + id + ".yaml");
    var discovered = Path.Combine(FileLoading.BaseDirectory, "expand_prefabs_new_" + id + ".yaml");
    try
    {
      Directory.CreateDirectory(FileLoading.BaseDirectory);
      File.WriteAllText(known, "- name: known\n  value: unchanged\n");
      File.WriteAllText(discovered, "- prefab: Player\n  type: create\n");
      Assert.That(FileChangeTracker.ReadFile(known, out var raw, out var hash, out var error), Is.True, error);
      Assert.That(Yaml.ParseMixed(known, raw, out var entries, out error), Is.True, error);
      cache.Store(known, entries);
      tracker.Commit(known, hash);

      object?[] arguments = [new List<string> { known }, 0];
      var updates = (ICollection)readChanges.Invoke(null, arguments)!;
      Assert.That(updates.Count, Is.Zero, "The watcher reported an unchanged file.");
      Assert.That(arguments[1], Is.EqualTo(1));

      // Discovery must still run when the event itself supplies no update.
      var found = FileLoading.GetPatternFiles(FileLoading.BaseDirectory, FileLoading.PrefabPattern)
        .FindAll(path => path.Equals(discovered, StringComparison.OrdinalIgnoreCase));
      var addedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      addNewFiles.Invoke(null, [found, addedHashes]);
      Assert.That(addedHashes.ContainsKey(discovered), Is.True);
      Assert.That(cache.Contains(discovered), Is.True);
      Assert.That(FileLoading.NeedsPrefabRebuild([], addedHashes.Keys), Is.True);
      Assert.That(tracker.ContainsApplied(discovered, addedHashes[discovered]), Is.False,
        "The newly discovered hash must be committed only after rebuilding.");
    }
    finally
    {
      tracker.Commit(known, null);
      tracker.Commit(discovered, null);
      cache.Clear();
      if (File.Exists(known)) File.Delete(known);
      if (File.Exists(discovered)) File.Delete(discovered);
    }
  }

  [Test]
  public void InvalidYamlPreservesPreviousVersion()
  {
    const BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    var method = typeof(FileLoading).GetMethod("ReadChanges", staticPrivate)!;
    var cache = (YamlFileCache)typeof(FileLoading).GetField("Cache", staticPrivate)!.GetValue(null)!;
    var tracker = (FileChangeTracker)typeof(FileLoading).GetField("Changes", staticPrivate)!.GetValue(null)!;
    var loggerField = typeof(Log).GetField("Logger", staticPrivate)!;
    var oldLogger = loggerField.GetValue(null);
    var path = Path.Combine(Path.GetTempPath(), "ewp-invalid-" + Guid.NewGuid().ToString("N") + ".yaml");
    try
    {
      Log.Init(new ManualLogSource("FileLoadingTests"));
      File.WriteAllText(path, "- name: valid\n  value: first\n");
      Assert.That(FileChangeTracker.ReadFile(path, out var raw, out var originalHash, out var error), Is.True, error);
      Assert.That(Yaml.ParseMixed(path, raw, out var entries, out error), Is.True, error);
      cache.Store(path, entries);
      tracker.Commit(path, originalHash);

      File.WriteAllText(path, "- prefab: Player\n  type: [broken");
      object?[] arguments = [new List<string> { path }, 0];
      var updates = (ICollection)method.Invoke(null, arguments)!;
      Assert.That(updates.Count, Is.Zero);
      Assert.That(tracker.ContainsApplied(path, originalHash), Is.True);
      Assert.That(cache.Contains(path), Is.True);

      File.WriteAllText(path, "- name: repaired\n  value: second\n");
      arguments = [new List<string> { path }, 0];
      updates = (ICollection)method.Invoke(null, arguments)!;
      Assert.That(updates.Count, Is.EqualTo(1), "Fixed YAML must be offered for the next rebuild.");
      Assert.That(tracker.ContainsApplied(path, originalHash), Is.True,
        "Reading an update must not commit its hash before the loaders execute.");
    }
    finally
    {
      tracker.Commit(path, null);
      cache.Clear();
      loggerField.SetValue(null, oldLogger);
      File.Delete(path);
    }
  }

}
