namespace ExpandWorld.Prefab;

public class DelayedRemove(double due, ZDOID zdo, bool triggerRules)
{
  private static readonly DelayedQueue<DelayedRemove> Removes = new();
  public static void Clear() => Removes.Clear();

  public static void Add(float delay, ZDOID zdo, bool triggerRules)
  {
    if (delay <= 0f)
    {
      Manager.RemoveZDO(zdo, triggerRules);
      return;
    }
    var remove = new DelayedRemove(ZNet.instance.m_netTime + delay, zdo, triggerRules);
    Removes.Add(remove.Due, remove);
  }
  public static void Execute() => Removes.Drain(ZNet.instance.m_netTime, static remove =>
  {
    remove.ExecuteAction();
    return null;
  }, consumeOnFailure: false);
  private readonly double Due = due;
  private readonly ZDOID Zdo = zdo;
  private readonly bool TriggerRules = triggerRules;

  private void ExecuteAction()
  {
    Manager.RemoveZDO(Zdo, TriggerRules);
  }
}
