using System;
using System.Collections.Generic;
using System.Linq;

namespace Service;

// Stores the successfully parsed YAML for each file and rebuilds EWP data from it.
// Invalid YAML must never replace a previously valid entry during automatic reload.
public sealed class YamlFileCache
{
  private readonly Dictionary<string, Yaml.MixedFileEntries> files = new(StringComparer.OrdinalIgnoreCase);

  public void Clear() => files.Clear();
  public void Store(string path, Yaml.MixedFileEntries entries) => files[path] = entries;
  public bool Remove(string path) => files.Remove(path);
  public bool Contains(string path) => files.ContainsKey(path);

  // Prefab rules and config declarations are only read from prefabFiles; null leaves them untouched.
  public void Apply(List<string> dataFiles, List<string>? prefabFiles)
  {
    Data.DataLoading.LoadFromFiles(dataFiles, Project(dataFiles, entries => entries.DataEntries));
    if (prefabFiles == null) return;
    ExpandWorld.Prefab.ConfigManager.LoadFromFiles(prefabFiles, Project(prefabFiles, entries => entries.ConfigEntries));
    ExpandWorld.Prefab.Loading.LoadFromFiles(prefabFiles, Project(prefabFiles, entries => entries.ScriptEntries));
  }

  private Dictionary<string, List<T>> Project<T>(List<string> paths, Func<Yaml.MixedFileEntries, List<T>> select) =>
    paths.Where(files.ContainsKey).ToDictionary(path => path, path => select(files[path]), StringComparer.OrdinalIgnoreCase);
}
