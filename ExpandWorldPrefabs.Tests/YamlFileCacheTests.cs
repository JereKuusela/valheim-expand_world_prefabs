using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

[NonParallelizable]
public class YamlFileCacheTests
{
  private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
  private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

  private static Dictionary<string, List<T>> Files<T>(YamlFileCache cache, string name) =>
    (Dictionary<string, List<T>>)typeof(YamlFileCache).GetField(name, PrivateInstance)!.GetValue(cache)!;

  // Exercise the cache's actual pruning algorithm without starting Unity/EWP loaders.
  private static void Prune<T>(Dictionary<string, List<T>> files, List<string> currentFiles)
  {
    typeof(YamlFileCache).GetMethod("PruneMissing", PrivateStatic)!.MakeGenericMethod(typeof(List<T>))
      .Invoke(null, [files, currentFiles]);
  }

  private TestPathScope? paths;

  [OneTimeSetUp]
  public void InitializePaths() => paths = new TestPathScope();

  [OneTimeTearDown]
  public void RestorePaths() => paths?.Dispose();

  [Test]
  public void FileCacheKeepsCategoriesConsistent()
  {
    var cache = new YamlFileCache();
    var path = Path.Combine(FileLoading.BaseDirectory, "expand_prefabs_categories.yaml");
    Assert.That(Yaml.ParseMixed(path,
      "- prefab: Player\n  type: create\n- name: group\n  value: example\n- config: Custom\n  key: example\n",
      out var parsed, out var error), Is.True, error);
    cache.Store(path, parsed);

    Assert.That(cache.Contains(path), Is.True);
    Assert.That(Files<RuleYaml>(cache, "prefabFiles")[path], Has.Count.EqualTo(1));
    Assert.That(Files<DataYaml>(cache, "dataFiles")[path], Has.Count.EqualTo(1));
    Assert.That(Files<ConfigYaml>(cache, "configFiles")[path], Has.Count.EqualTo(1));

    cache.Store(path, new Yaml.MixedFileEntries());
    Assert.That(cache.Contains(path), Is.True, "A valid empty file remains cached.");
    Assert.That(Files<RuleYaml>(cache, "prefabFiles")[path], Is.Empty);
    Assert.That(Files<DataYaml>(cache, "dataFiles")[path], Is.Empty);
    Assert.That(Files<ConfigYaml>(cache, "configFiles")[path], Is.Empty);
  }

  [Test]
  public void DeletedAndRenamedFilesArePruned()
  {
    var cache = new YamlFileCache();
    var tracker = new FileChangeTracker();
    var oldPath = Path.Combine(FileLoading.BaseDirectory, "expand_prefabs_old.yaml");
    var renamed = Path.Combine(FileLoading.BaseDirectory, "expand_prefabs_renamed.yaml");
    var entries = new Yaml.MixedFileEntries();
    cache.Store(oldPath, entries);
    cache.Store(renamed, entries);
    tracker.Commit(oldPath, "oldhash");
    tracker.Commit(renamed, "newhash");

    Prune(Files<RuleYaml>(cache, "prefabFiles"), [renamed]);
    Prune(Files<DataYaml>(cache, "dataFiles"), [renamed]);
    Prune(Files<ConfigYaml>(cache, "configFiles"), [renamed]);
    tracker.Commit(oldPath, null);

    Assert.That(cache.Contains(oldPath), Is.False);
    Assert.That(cache.Contains(renamed), Is.True);
    Assert.That(Files<RuleYaml>(cache, "prefabFiles").ContainsKey(oldPath), Is.False);
    Assert.That(Files<ConfigYaml>(cache, "configFiles").ContainsKey(oldPath), Is.False);
    Assert.That(tracker.Contains(oldPath), Is.False);
    Assert.That(tracker.ContainsApplied(renamed, "newhash"), Is.True);
  }

}
