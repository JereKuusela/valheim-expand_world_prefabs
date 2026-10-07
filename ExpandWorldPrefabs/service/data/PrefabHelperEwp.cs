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
  }

  static partial void OnClearCache() => PrefabCharacterParentSync.Clear();

  public static bool HasCharacterParentSync(int prefab)
  {
    if (PrefabCharacterParentSync.TryGetValue(prefab, out var value))
      return value;
    PrefabCharacterParentSync[prefab] = ZNetScene.instance.GetPrefab(prefab)?.GetComponent<ZSyncTransform>()?.m_characterParentSync ?? false;
    return PrefabCharacterParentSync[prefab];
  }
}
