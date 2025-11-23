using System.Collections.Generic;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class DelayedTerrain(float delay, long source, Vector3 pos, float size, ZPackage pkg, float resetRadius)
{
  private static readonly List<DelayedTerrain> Terrains = [];
  public static void Add(float delay, long source, Vector3 pos, float size, ZPackage pkg, float resetRadius)
  {
    var created = Manager.GenerateTerrainCompilers(source, pos, size);
    // One second should be enough to deliver the compiler to the client.
    if (created) delay = Mathf.Max(delay, 1f);
    if (delay <= 0f)
    {
      Manager.ModifyTerrain(source, pos, size, pkg, resetRadius);
      return;
    }
    Terrains.Add(new(delay, source, pos, size, pkg, resetRadius));
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
    Manager.ModifyTerrain(source, pos, size, pkg, resetRadius);
  }
}