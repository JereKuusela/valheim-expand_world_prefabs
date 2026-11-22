namespace ExpandWorld.Prefab;

public class DelayedRpc(double due, long source, long target, ZDOID zdo, int hash, object[] parameters)
{
  private static readonly DelayedQueue<DelayedRpc> Rpcs = new();
  public static void Clear() => Rpcs.Clear();

  public static void Add(float delay, long source, long target, ZDOID zdo, int hash, object[] parameters, bool overwrite)
  {
    if (overwrite)
      Remove(zdo, hash);
    if (delay <= 0f)
      Manager.Rpc(source, target, zdo, hash, parameters);
    else
    {
      var rpc = new DelayedRpc(ZNet.instance.m_netTime + delay, source, target, zdo, hash, parameters);
      Rpcs.Add(rpc.Due, rpc);
    }
  }
  public static void Remove(ZDOID zdo, int hash) => Rpcs.RemoveWhere(rpc => rpc.Zdo == zdo && rpc.Hash == hash);
  public static void Execute() => Rpcs.Drain(ZNet.instance.m_netTime, static rpc =>
  {
    rpc.ExecuteAction();
    return null;
  }, consumeOnFailure: false);
  private readonly double Due = due;
  private readonly long Source = source;
  private readonly long Target = target;
  private readonly ZDOID Zdo = zdo;
  private readonly int Hash = hash;
  private readonly object[] Parameters = parameters;


  private void ExecuteAction()
  {
    Manager.Rpc(Source, Target, Zdo, Hash, Parameters);
  }
}