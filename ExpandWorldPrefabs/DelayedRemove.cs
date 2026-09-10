using System.Collections.Generic;
using Service;
namespace ExpandWorld.Prefab;

public class DelayedRemove(float delay, string zdo, bool triggerRules)
{
  private static readonly List<DelayedRemove> Removes = [];
  public static void Add(float delay, ZDOID zdo, bool triggerRules)
  {
    if (delay <= 0f)
    {
      Manager.RemoveZDO(zdo, triggerRules);
      return;
    }
    // The compact owner index may change before the delay expires.
    Removes.Add(new(delay, zdo.ToString(), triggerRules));
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
      Removes.RemoveAt(i);
      i--;
    }
  }
  private readonly string Zdo = zdo;
  public float Delay = delay;
  private readonly bool TriggerRules = triggerRules;

  public void Execute()
  {
    var zdo = Parse.ZdoId(Zdo);
    if (zdo == ZDOID.None) return;
    Manager.RemoveZDO(zdo, TriggerRules);
  }
}
