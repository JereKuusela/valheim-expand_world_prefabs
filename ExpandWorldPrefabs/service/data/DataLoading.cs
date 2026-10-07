using System;
using System.Collections.Generic;
using Service;

namespace Data;

public class DataLoading
{
  // Each file can have multiple data entries so we need to load them all.
  // Hash is used as key because base64 encoded strings can be loaded too.
  public static Dictionary<int, DataEntry> Data = [];
  private static readonly Dictionary<string, List<DataYaml>> FileEntries = new(StringComparer.OrdinalIgnoreCase);

  public static void Add(int hash, DataEntry entry)
  {
    Data[hash] = entry;
  }
  public static DataEntry? Get(string name)
  {
    var data = Get(name.GetStableHashCode());
    if (data != null)
      return data;
    // Legacy base64 handling.
    if (name.Length >= 12 && name.Length % 4 == 0)
    {
      try
      {
        DataEntry d = new(new ZPackage(name));
        Data[name.GetStableHashCode()] = d;
        return d;
      }
      catch
      {
        Log.Error($"Failed to decode base64 data: {name}");
      }
    }
    Log.Warning($"Data entry not found: {name}");
    return null;
  }
  public static DataEntry? Get(int hash) => Data.ContainsKey(hash) ? Data[hash] : null;
  public static bool TryGet(int hash, out DataEntry? entry)
  {
    entry = Data.ContainsKey(hash) ? Data[hash] : null;
    return entry != null;
  }

  public static void LoadFromFiles(List<string> files, Dictionary<string, List<DataYaml>> fileEntries)
  {
    var prev = Data;
    FileEntries.Clear();
    foreach (var file in files)
      FileEntries[file] = fileEntries.TryGetValue(file, out var entries) ? entries : [];
    RebuildFromCache(prev, files);
  }

  private static void RebuildFromCache(Dictionary<int, DataEntry> prev, List<string> files)
  {
    Data = [];
    ValueGroups.Clear();
    foreach (var file in files)
    {
      if (!FileEntries.TryGetValue(file, out var entries)) continue;
      foreach (var d in entries)
        ValueGroups.Add(d, file);
    }
    if (ValueGroups.Count > 0)
      Log.Info($"Loaded {ValueGroups.Count} value groups.");

    ValueGroups.Resolve();
    foreach (var file in files)
    {
      if (!FileEntries.TryGetValue(file, out var entries)) continue;
      foreach (var d in entries)
        LoadEntry(d, prev);
    }
    PrefabHelper.ClearCache();
    Log.Info($"Loaded {Data.Count} data entries.");
  }

  private static void LoadEntry(DataYaml data, Dictionary<int, DataEntry> oldData)
  {
    if (data.name != null)
    {
      var hash = data.name.GetStableHashCode();
      if (Data.ContainsKey(hash))
        Log.Warning($"Duplicate data entry: {data.name}");
      Data[hash] = oldData.TryGetValue(hash, out var prev) ? prev.Reset(data) : new DataEntry(data);
    }
  }
}
