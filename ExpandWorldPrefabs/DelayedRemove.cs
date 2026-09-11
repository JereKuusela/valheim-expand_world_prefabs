using System.Collections.Generic;
namespace ExpandWorld.Prefab;

public class DelayedRemove(double due, ZDOID zdo, bool triggerRules)
{
  private static readonly List<DelayedRemove> Removes = [];
  public static void Clear() => Removes.Clear();

  public static void Add(float delay, ZDOID zdo, bool triggerRules)
  {
    if (delay <= 0f)
    {
      Manager.RemoveZDO(zdo, triggerRules);
      return;
    }
    Removes.Add(new(ZNet.instance.m_netTime + delay, zdo, triggerRules));
  }
  public static void Execute()
  {
    for (var i = 0; i < Removes.Count; i++)
    {
      var remove = Removes[i];
      if (remove.Due > ZNet.instance.m_netTime) continue;
      remove.ExecuteAction();
      Removes.RemoveAt(i);
      i--;
    }
  }
  private readonly ZDOID Zdo = zdo;
  private readonly double Due = due;
  private readonly bool TriggerRules = triggerRules;

  private void ExecuteAction()
  {
    Manager.RemoveZDO(Zdo, TriggerRules);
  }
}
