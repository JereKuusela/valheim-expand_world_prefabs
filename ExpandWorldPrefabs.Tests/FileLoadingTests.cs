using System;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

[NonParallelizable]
public class FileLoadingTests
{
  private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
  private TestPathScope? paths;
  private YamlFileCache cache = null!;
  private FieldInfo loggerField = null!;
  private object? oldLogger;

  [OneTimeSetUp]
  public void InitializePaths() => paths = new TestPathScope();

  [OneTimeTearDown]
  public void RestorePaths() => paths?.Dispose();

  [SetUp]
  public void SetUp()
  {
    cache = (YamlFileCache)typeof(FileLoading).GetField("Cache", StaticPrivate)!.GetValue(null)!;
    cache.Clear();
    loggerField = typeof(Log).GetField("Logger", StaticPrivate)!;
    oldLogger = loggerField.GetValue(null);
    Log.Init(new ManualLogSource("FileLoadingTests"));
  }

  [TearDown]
  public void TearDown()
  {
    cache.Clear();
    loggerField.SetValue(null, oldLogger);
  }

  private static ChangedFile Changed(string path, string content) => new(path, content, "hash");

  [Test]
  public void ChangedPrefabFileReportsPrefabsTouched()
  {
    var data = Path.Combine(FileLoading.BaseDirectory, "expand_data_weather.yaml");
    var prefab = Path.Combine(FileLoading.BaseDirectory, "expand_prefabs_new.yaml");

    var dataOnly = new YamlChanges();
    dataOnly.Changed.Add(Changed(data, "- name: a\n  value: b\n"));
    Assert.That(FileLoading.StoreChanges(dataOnly, false), Is.EqualTo((1, false)));

    var both = new YamlChanges();
    both.Changed.Add(Changed(prefab, "- prefab: Player\n  type: create\n"));
    Assert.That(FileLoading.StoreChanges(both, false), Is.EqualTo((1, true)));
    Assert.That(cache.Contains(prefab), Is.True);
  }

  [Test]
  public void InvalidYamlPreservesPreviousVersion()
  {
    var path = Path.Combine(FileLoading.BaseDirectory, "expand_data_invalid.yaml");
    var valid = new YamlChanges();
    valid.Changed.Add(Changed(path, "- name: valid\n  value: first\n"));
    FileLoading.StoreChanges(valid, false);

    var broken = new YamlChanges();
    broken.Changed.Add(Changed(path, "- prefab: Player\n  type: [broken"));
    Assert.That(FileLoading.StoreChanges(broken, false).Modified, Is.Zero);
    Assert.That(cache.Contains(path), Is.True);
  }

  [Test]
  public void DeletedFileIsRemovedFromCache()
  {
    var path = Path.Combine(FileLoading.BaseDirectory, "expand_prefabs_old.yaml");
    var added = new YamlChanges();
    added.Changed.Add(Changed(path, "- prefab: Player\n  type: create\n"));
    FileLoading.StoreChanges(added, false);

    var deleted = new YamlChanges();
    deleted.Deleted.Add(path);
    Assert.That(FileLoading.StoreChanges(deleted, false), Is.EqualTo((1, true)));
    Assert.That(cache.Contains(path), Is.False);
  }

  [Test]
  public void BackupMatchesParsedContent()
  {
    var path = Path.Combine(FileLoading.BaseDirectory, "expand_data_backup.yaml");
    var changes = new YamlChanges();
    changes.Changed.Add(Changed(path, "- name: a\n  value: b\n"));
    FileLoading.StoreChanges(changes, true);
    var backup = Directory.GetFiles(FileLoading.BackupDirectory, "expand_data_backup_*.bak");
    Assert.That(backup, Has.Length.EqualTo(1));
    Assert.That(File.ReadAllText(backup[0]), Is.EqualTo("- name: a\n  value: b\n"));
  }
}
