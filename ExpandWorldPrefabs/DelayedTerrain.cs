using System.Collections.Generic;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class DelayedTerrain(double due, Vector3 pos, float size, ZPackage pkg, float resetRadius)
{
  private static readonly List<DelayedTerrain> Terrains = [];
  public static void Clear() => Terrains.Clear();

  public static void Add(float delay, Vector3 pos, float size, ZPackage pkg, float resetRadius)
  {
    var created = Manager.GenerateTerrainCompilers(pos, size);
    // Allow a newly created compiler, or corrected ownership, to initialize.
    if (created) delay = Mathf.Max(delay, 1f);
    if (delay <= 0f)
    {
      Manager.ModifyTerrain(pos, size, pkg, resetRadius);
      return;
    }
    Terrains.Add(new(ZNet.instance.m_netTime + delay, pos, size, pkg, resetRadius));
  }
  public static void Execute()
  {
    for (var i = 0; i < Terrains.Count; i++)
    {
      var terrain = Terrains[i];
      if (terrain.Due > ZNet.instance.m_netTime) continue;
      terrain.ExecuteAction();
      Terrains.RemoveAt(i);
      i--;
    }
  }
  private readonly double Due = due;
  private void ExecuteAction()
  {
    Manager.ModifyTerrain(pos, size, pkg, resetRadius);
  }
}
