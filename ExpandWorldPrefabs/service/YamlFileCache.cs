using System;
using System.Collections.Generic;
using System.Linq;

namespace Service;

// Stores the successfully parsed YAML for each file and rebuilds EWP data from it.
// Invalid YAML must never replace a previously valid entry during automatic reload.
public sealed class YamlFileCache
{
  private readonly Dictionary<string, List<ExpandWorld.Prefab.RuleYaml>> prefabFiles = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, List<Data.DataYaml>> dataFiles = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, List<ExpandWorld.Prefab.ConfigYaml>> configFiles = new(StringComparer.OrdinalIgnoreCase);

  public void Clear()
  {
    prefabFiles.Clear();
    dataFiles.Clear();
    configFiles.Clear();
  }

  public void Store(string path, Yaml.MixedFileEntries entries)
  {
    dataFiles[path] = entries.DataEntries;
    if (!FileLoading.IsPrefabPath(path)) return;
    prefabFiles[path] = entries.ScriptEntries;
    configFiles[path] = entries.ConfigEntries;
  }

  // Every successfully parsed file has a data entry, even when its YAML contains no data rules.
  public bool Contains(string path) => dataFiles.ContainsKey(path);

  public void ApplyData(List<string> paths)
  {
    PruneMissing(dataFiles, paths);
    Data.DataLoading.LoadFromFiles(paths, dataFiles);
  }

  public void ApplyPrefabs(List<string> paths)
  {
    PruneMissing(prefabFiles, paths);
    PruneMissing(configFiles, paths);
    ExpandWorld.Prefab.ConfigManager.LoadFromFiles(paths, configFiles);
    ExpandWorld.Prefab.Loading.LoadFromFiles(paths, prefabFiles);
  }

  private static void PruneMissing<T>(Dictionary<string, T> cache, List<string> files)
  {
    var existing = files.ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var path in cache.Keys.ToList())
      if (!existing.Contains(path)) cache.Remove(path);
  }
}
