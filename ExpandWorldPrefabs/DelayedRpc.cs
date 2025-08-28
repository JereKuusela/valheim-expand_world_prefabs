using System.Collections.Generic;
namespace ExpandWorld.Prefab;

public class DelayedRpc(float delay, long source, long target, ZDOID id, int hash, object[] parameters)
{
  private static readonly List<DelayedRpc> Rpcs = [];
  public static void Add(float delay, int repeat, long source, long target, ZDOID id, int hash, object[] parameters)
  {
    if (delay <= 0f)
    {
      Manager.Rpc(source, target, id, hash, parameters);
      return;
    }
    if (repeat == 0)
    {
      Rpcs.Add(new(delay, source, target, id, hash, parameters));
      return;
    }
    // First is instant.
    Manager.Rpc(source, target, id, hash, parameters);
    for (var i = 1; i < repeat; i++)
      Rpcs.Add(new(delay * i, source, target, id, hash, parameters));
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
  private readonly ZDOID Id = id;
  private readonly int Hash = hash;
  private readonly object[] Parameters = parameters;


  public void Execute()
  {
    Manager.Rpc(Source, Target, Id, Hash, Parameters);
  }
}