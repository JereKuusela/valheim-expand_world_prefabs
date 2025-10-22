using System.Collections.Generic;
namespace ExpandWorld.Prefab;

public class DelayedRpc(float delay, long source, long target, ZDOID zdo, int hash, object[] parameters)
{
  private static readonly List<DelayedRpc> Rpcs = [];
  public static void Add(float delay, long source, long target, ZDOID zdo, int hash, object[] parameters, bool overwrite)
  {
    if (overwrite)
      Remove(zdo, hash);
    if (delay <= 0f)
      Manager.Rpc(source, target, zdo, hash, parameters);
    else
      Rpcs.Add(new(delay, source, target, zdo, hash, parameters));
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
  public static void Execute(float dt)
  {
    // Two loops to preserve order.
    for (var i = 0; i < Rpcs.Count; i++)
    {
      var rpc = Rpcs[i];
      rpc.Delay -= dt;
      if (rpc.Delay > -0.001) continue;
      rpc.Execute();
    }
    for (var i = Rpcs.Count - 1; i >= 0; i--)
    {
      if (Rpcs[i].Delay > -0.001) continue;
      Rpcs.RemoveAt(i);
    }
  }
  public float Delay = delay;
  private readonly long Source = source;
  private readonly long Target = target;
  private readonly ZDOID Zdo = zdo;
  private readonly int Hash = hash;
  private readonly object[] Parameters = parameters;


  public void Execute()
  {
    Manager.Rpc(Source, Target, Zdo, Hash, Parameters);
  }
}