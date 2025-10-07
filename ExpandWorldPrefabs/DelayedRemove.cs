using System.Collections.Generic;
namespace ExpandWorld.Prefab;

public class DelayedRemove(float delay, ZDOID zdo, bool triggerRules)
{
  private static readonly List<DelayedRemove> Removes = [];
  public static void Add(float delay, ZDOID zdo, bool triggerRules)
  {
    if (delay <= 0f)
    {
      Manager.RemoveZDO(zdo, triggerRules);
      return;
    }
    Removes.Add(new(delay, zdo, triggerRules));
  }
  public static void Execute(float dt)
  {
    // Two loops to preserve order.
    for (var i = 0; i < Removes.Count; i++)
    {
      var remove = Removes[i];
      remove.Delay -= dt;
      if (remove.Delay > -0.001) continue;
      remove.Execute();
    }
    for (var i = Removes.Count - 1; i >= 0; i--)
    {
      if (Removes[i].Delay > -0.001) continue;
      Removes.RemoveAt(i);
    }
  }
  private readonly ZDOID Zdo = zdo;
  public float Delay = delay;
  private readonly bool TriggerRules = triggerRules;

  public void Execute()
  {
    Manager.RemoveZDO(Zdo, TriggerRules);
  }
}