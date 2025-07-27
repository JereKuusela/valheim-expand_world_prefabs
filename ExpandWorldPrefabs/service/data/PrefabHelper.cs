
using System;
using System.Collections.Generic;
using System.Linq;
using Service;

namespace Data;

// Separate class because parsing and caching is quite complicated.
// Input string can contain wildcards, multiple values and value groups.
// There should be a single entry point that handles all of these.

// Caching simply saves results for the same input string.
// Since value groups can be changed, cache should be cleared when needed.

public class PrefabHelper
{
  private static readonly Dictionary<string, List<int>> ResultCache = [];

  public static void ClearCache()
  {
    ResultCache.Clear();
    PrefabCache.Clear();
  }
  public static List<int> GetPrefabs(string include, string exclude)
  {
    var key = $"{include}|{exclude}";
    if (ResultCache.ContainsKey(key)) return ResultCache[key];
    var includes = Parse.ToList(include);
    var excludes = exclude == "" ? null : Parse.ToList(exclude);
    var prefabs = GetPrefabs(includes, excludes);
    // No point to cache error results from users.
    if (prefabs == null) return [];
    ResultCache[key] = prefabs;
    return prefabs;
  }
  // Called by PrefabValue that handles the caching.
  // Ideally this would be cached here but difficult with parameters.

  public static List<int>? GetPrefabs(List<string> includes, List<string>? excludes)
  {
    if (includes.Count == 0) return null;
    HashSet<int>? excludedPrefabs = null;
    if (excludes != null && excludes.Count > 0)
      excludedPrefabs = excludes.Select(ParsePrefabs).Where(s => s != null).SelectMany(s => s).ToHashSet();

    if (includes.Count == 1)
    {
      var prefabs = ParsePrefabs(includes[0]);
      if (prefabs == null) return null;
      if (excludedPrefabs != null)
        prefabs = [.. prefabs.Where(i => !excludedPrefabs.Contains(i))];
      return prefabs;
    }
    else
    {
      var prefabs = includes.Select(ParsePrefabs).Where(s => s != null).ToList();
      HashSet<int> value = [];
      foreach (var p in prefabs)
      {
        if (p == null) continue;
        foreach (var i in p)
        {
          if (excludedPrefabs != null && excludedPrefabs.Contains(i))
            continue;
          value.Add(i);
        }
      }
      return value.Count == 0 ? null : [.. value];
    }
  }

  private static Dictionary<string, int> PrefabCache = [];
  private static List<int>? ParsePrefabs(string prefab)
  {
    var p = prefab.ToLowerInvariant();
    if (PrefabCache.Count == 0)
      PrefabCache = ZNetScene.instance.m_namedPrefabs.ToDictionary(pair => pair.Value.name, pair => pair.Key);
    if (p == "*")
      return [.. PrefabCache.Values];
    if (p[0] == '*' && p[p.Length - 1] == '*')
    {
      p = p.Substring(1, p.Length - 2);
      return [.. PrefabCache.Where(pair => pair.Key.ToLowerInvariant().Contains(p)).Select(pair => pair.Value)];
    }
    if (p[0] == '*')
    {
      p = p.Substring(1);
      return [.. PrefabCache.Where(pair => pair.Key.EndsWith(p, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value)];
    }
    if (p[p.Length - 1] == '*')
    {
      p = p.Substring(0, p.Length - 1);
      return [.. PrefabCache.Where(pair => pair.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value)];
    }
    if (PrefabCache.ContainsKey(prefab))
      return [PrefabCache[prefab]];
    var group = DataHelper.GetValuesFromGroup(prefab);
    if (group != null)
      return [.. group.Select(s => s.GetStableHashCode())];
    Log.Warning($"Failed to resolve prefab: {prefab}");
    return null;
  }
}