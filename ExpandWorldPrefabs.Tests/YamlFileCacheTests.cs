using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

[NonParallelizable]
public class YamlFileCacheTests
{
  private TestPathScope? paths;

  [OneTimeSetUp]
  public void InitializePaths() => paths = new TestPathScope();

  [OneTimeTearDown]
  public void RestorePaths() => paths?.Dispose();

  [Test]
  public void StoreReplacesAndRemoveForgetsFiles()
  {
    var cache = new YamlFileCache();
    var path = Path.Combine(FileLoading.BaseDirectory, "expand_prefabs_categories.yaml");
    Assert.That(Yaml.ParseMixed(path,
      "- prefab: Player\n  type: create\n- name: group\n  value: example\n- config: Custom\n  key: example\n",
      out var parsed, out var error), Is.True, error);
    cache.Store(path, parsed);
    Assert.That(cache.Contains(path), Is.True);

    cache.Store(path, new Yaml.MixedFileEntries());
    Assert.That(cache.Contains(path), Is.True, "A valid empty file remains cached.");

    Assert.That(cache.Remove(path), Is.True);
    Assert.That(cache.Contains(path), Is.False);
    Assert.That(cache.Remove(path), Is.False);
  }

  [Test]
  public void PathsAreCaseInsensitive()
  {
    var cache = new YamlFileCache();
    cache.Store("A.yaml", new Yaml.MixedFileEntries());
    Assert.That(cache.Contains("a.YAML"), Is.True);
  }
}
