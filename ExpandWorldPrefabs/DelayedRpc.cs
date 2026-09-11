using System.Collections.Generic;
namespace ExpandWorld.Prefab;

public class DelayedRpc(double due, long source, long target, ZDOID zdo, int hash, object[] parameters)
{
  private static readonly List<DelayedRpc> Rpcs = [];
  public static void Clear() => Rpcs.Clear();

  public static void Add(float delay, long source, long target, ZDOID zdo, int hash, object[] parameters, bool overwrite)
  {
    if (overwrite)
      Remove(zdo, hash);
    if (delay <= 0f)
      Manager.Rpc(source, target, zdo, hash, parameters);
    else
      Rpcs.Add(new(ZNet.instance.m_netTime + delay, source, target, zdo, hash, parameters));
  }
  public static void Remove(ZDOID zdo, int hash)
  {
    for (var i = Rpcs.Count - 1; i >= 0; i--)
    {
      var rpc = Rpcs[i];
      if (rpc.Zdo == zdo && rpc.Hash == hash)
        Rpcs.RemoveAt(i);
    }
  }
  public static void Execute()
  {
    for (var i = 0; i < Rpcs.Count; i++)
    {
      var rpc = Rpcs[i];
      if (rpc.Due > ZNet.instance.m_netTime) continue;
      rpc.ExecuteAction();
      Rpcs.RemoveAt(i);
      i--;
    }
  }
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