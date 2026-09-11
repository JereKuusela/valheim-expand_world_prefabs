using System.Collections.Generic;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class DelayedTerrain(float delay, Vector3 pos, float size, ZPackage pkg, float resetRadius)
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
    Terrains.Add(new(delay, pos, size, pkg, resetRadius));
  }
  public static void Execute(float dt)
  {
    // Two loops to preserve order.
    for (var i = 0; i < Terrains.Count; i++)
    {
      var terrain = Terrains[i];
      terrain.Delay -= dt;
      if (terrain.Delay > -0.001) continue;
      terrain.Execute();
      Terrains.RemoveAt(i);
      i--;
    }
  }
  public float Delay = delay;
  public void Execute()
  {
    Manager.ModifyTerrain(pos, size, pkg, resetRadius);
  }
}
