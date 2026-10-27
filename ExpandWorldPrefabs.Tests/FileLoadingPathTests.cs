using System;
using System.IO;
using BepInEx;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

[NonParallelizable]
public class FileLoadingPathTests
{
  private TestPathScope? paths;

  [OneTimeSetUp]
  public void InitializePaths() => paths = new TestPathScope();

  [OneTimeTearDown]
  public void RestorePaths() => paths?.Dispose();

  [Test]
  public void DisplayPathIsRelativeToConfigPathOnly()
  {
    var inside = Path.Combine(Paths.ConfigPath, "expand_world", "expand_prefabs.yaml");
    Assert.That(FileLoading.DisplayPath(inside), Is.EqualTo("expand_world/expand_prefabs.yaml"));

    var outside = Path.GetFullPath(Path.Combine(Paths.ConfigPath, "..", "other", "test.yaml"));
    Assert.That(FileLoading.DisplayPath(outside), Is.EqualTo(outside.Replace('\\', '/')));

    // A sibling whose name begins with the config directory name is still outside.
    var sibling = Path.GetFullPath(Paths.ConfigPath) + "_other";
    var siblingFile = Path.Combine(sibling, "test.yaml");
    Assert.That(FileLoading.DisplayPath(siblingFile), Is.EqualTo(siblingFile.Replace('\\', '/')));
  }

  [Test]
  public void SimilarFolderNamesAreNotMistakenForTheDataDirectory()
  {
    var root = Path.Combine(Path.GetTempPath(), "ewp-" + Guid.NewGuid().ToString("N"));
    var inside = Path.Combine(root, "data", "nested", "rule.yaml");
    var sibling = Path.Combine(root, "data-other", "rule.yaml");
    Assert.That(YamlChangeDetector.IsInFolder(inside, Path.Combine(root, "data")), Is.True);
    Assert.That(YamlChangeDetector.IsInFolder(sibling, Path.Combine(root, "data")), Is.False);
  }
}
