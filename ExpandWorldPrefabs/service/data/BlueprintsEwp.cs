using System.Collections.Generic;
using System.IO;
using BepInEx;
using ExpandWorld.Prefab;
using Service;

namespace Data;

public partial class Blueprints
{
  private static partial IEnumerable<string> Folders() =>
    [Path.Combine("BepInEx", "config", Config.BlueprintFolder), Path.Combine(Paths.ConfigPath, Config.BlueprintFolder)];

  private static partial DataEntry? ResolveData(string data, string fileName) => DataHelper.Get(data);
}

public static partial class BlueprintWatcher
{
  static partial void OnChanged(string name, bool wasLoaded, bool listChanged)
  {
    Log.Info($"Blueprint {name} changed.");
    // Rules cache the resolved prefabs, so added or removed blueprints need a full reload.
    if (listChanged && ZNetScene.instance != null)
      FileLoading.ReloadAll();
  }
}
