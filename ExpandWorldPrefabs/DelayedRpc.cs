using System.Collections.Generic;
namespace ExpandWorld.Prefab;

public class DelayedRpc(float delay, long source, long target, ZDOID id, int hash, object[] parameters)
{
  private static readonly List<DelayedRpc> Rpcs = [];
  public static void Add(float delay, List<float>? delays, long source, long target, ZDOID id, int hash, object[] parameters)
  {
    if (delays != null && delays.Count > 0)
      Add(delays, source, target, id, hash, parameters);
    else
      Add(delay, source, target, id, hash, parameters);
  }
  private static void Add(List<float> delays, long source, long target, ZDOID id, int hash, object[] parameters)
  {
    foreach (var delay in delays)
    {
      if (delay <= 0f)
        Manager.Rpc(source, target, id, hash, parameters);
      else
        Rpcs.Add(new(delay, source, target, id, hash, parameters));
    }
  }
  private static void Add(float delay, long source, long target, ZDOID id, int hash, object[] parameters)
  {
    if (delay <= 0f)
      Manager.Rpc(source, target, id, hash, parameters);
    else
      Rpcs.Add(new(delay, source, target, id, hash, parameters));
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