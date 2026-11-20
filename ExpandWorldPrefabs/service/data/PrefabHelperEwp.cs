using System.Collections.Generic;
using Common;
using ExpandWorld.Prefab;

namespace Data;

public static partial class PrefabHelper
{
  private static readonly Dictionary<int, bool> PrefabCharacterParentSync = [];

  static partial void AddCustomPrefabs(Dictionary<string, int> prefabs)
  {
    foreach (var configuredPrefab in Parse.ToList(Config.CustomPrefabNames))
    {
      if (string.IsNullOrWhiteSpace(configuredPrefab)) continue;
      var trimmedPrefab = configuredPrefab.Trim();
      prefabs[trimmedPrefab] = trimmedPrefab.GetStableHashCode();
    }
    // Real prefabs take priority over blueprints with the same name.
    foreach (var name in BlueprintCache.GetNames())
      if (!prefabs.ContainsKey(name))
        prefabs[name] = name.GetStableHashCode();
  }

  static partial void OnClearCache()
  {
    PrefabCharacterParentSync.Clear();
    BlueprintCache.Clear();
  }

  public static bool HasCharacterParentSync(int prefab)
  {
    if (PrefabCharacterParentSync.TryGetValue(prefab, out var value))
      return value;
    PrefabCharacterParentSync[prefab] = ZNetScene.instance.GetPrefab(prefab)?.GetComponent<ZSyncTransform>()?.m_characterParentSync ?? false;
    return PrefabCharacterParentSync[prefab];
  }
}
