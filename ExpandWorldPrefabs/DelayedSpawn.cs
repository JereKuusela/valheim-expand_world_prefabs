using System.Collections.Generic;
using Data;
using Service;

namespace ExpandWorld.Prefab;

public class DelayedSpawn(float delay, ZdoEntry zdoEntry, bool triggerRules)
{
  private static readonly List<DelayedSpawn> Spawns = [];
  public static void Add(float delay, List<float>? delays, ZdoEntry zdoEntry, bool triggerRules)
  {
    if (delays != null && delays.Count > 0)
      Add(delays, zdoEntry, triggerRules);
    else
      Add(delay, zdoEntry, triggerRules);
  }
  private static void Add(List<float> delays, ZdoEntry zdoEntry, bool triggerRules)
  {
    foreach (var delay in delays)
    {
      if (delay <= 0f)
        Manager.CreateObject(zdoEntry, triggerRules);
      else
        Spawns.Add(new(delay, zdoEntry, triggerRules));
    }
  }
  private static void Add(float delay, ZdoEntry zdoEntry, bool triggerRules)
  {
    if (delay <= 0f)
    {
      Manager.CreateObject(zdoEntry, triggerRules);
      return;
    }
    Spawns.Add(new(delay, zdoEntry, triggerRules));
  }
  public static void Execute(float dt)
  {
    // Two loops to preserve order.
    for (var i = 0; i < Spawns.Count; i++)
    {
      var spawn = Spawns[i];
      spawn.Delay -= dt;
      if (spawn.Delay > -0.001) continue;
      spawn.Execute();
    }
    for (var i = Spawns.Count - 1; i >= 0; i--)
    {
      if (Spawns[i].Delay > -0.001) continue;
      Spawns.RemoveAt(i);
    }
  }
  public float Delay = delay;
  private readonly ZdoEntry ZdoEntry = zdoEntry;
  private readonly bool TriggerRules = triggerRules;

  public void Execute()
  {
    Manager.CreateObject(ZdoEntry, TriggerRules);
  }
}