using System.Collections.Generic;
namespace ExpandWorld.Prefab;

public class DelayedRpc(double due, long source, long target, ZDOID zdo, int hash, object[] parameters)
{
  private static readonly List<DelayedRpc> Rpcs = [];
  public static void Clear() => Rpcs.Clear();

  public static void Add(float delay, long source, long target, ZDOID zdo, int hash, object[] parameters, bool overwrite)
    => Add(delay, source, target, zdo, hash, parameters, overwrite, zdo);

  internal static void Add(float delay, long source, long target, ZDOID zdo, int hash, object[] parameters, bool overwrite, ZDOID actor)
  {
    if (overwrite)
      RemoveScoped(zdo, hash, target, source);
    // Teleports run on the next update so the rule that requested one can
    // finish its source-state work before the Player enters transit.
    if (ShouldExecuteImmediately(delay, hash, IsQuarantined(zdo, actor)))
      Manager.Rpc(source, target, zdo, hash, parameters, actor);
    else
      Rpcs.Add(new(ZNet.instance.m_netTime + System.Math.Max(0f, delay), source, target, zdo, hash, parameters) { Actor = actor });
  }
  public static void Remove(ZDOID zdo, int hash)
  {
    RemoveScoped(zdo, hash);
  }
  private static void RemoveScoped(ZDOID zdo, int hash, long? target = null, long? source = null)
  {
    for (var i = Rpcs.Count - 1; i >= 0; i--)
    {
      var rpc = Rpcs[i];
      if (rpc.Zdo == zdo && rpc.Hash == hash && (!target.HasValue || rpc.Target == target.Value) && (!source.HasValue || rpc.Source == source.Value))
        Rpcs.RemoveAt(i);
    }
  }
  public static void Execute()
  {
    for (var i = 0; i < Rpcs.Count; i++)
    {
      var rpc = Rpcs[i];
      if (rpc.Due > ZNet.instance.m_netTime) continue;
      if (IsQuarantined(rpc.Zdo, rpc.Actor)) continue;
      Rpcs.RemoveAt(i);
      i--;
      // Consume first: an exception must not replay an RPC on every later frame.
      rpc.ExecuteAction();
    }
  }
  private readonly double Due = due;
  private readonly long Source = source;
  private readonly long Target = target;
  private readonly ZDOID Zdo = zdo;
  private readonly int Hash = hash;
  private readonly object[] Parameters = parameters;
  private ZDOID Actor = zdo;

  internal static bool ShouldExecuteImmediately(float delay, int hash, bool quarantined) =>
    delay <= 0f && !TeleportManager.IsTeleport(hash) && !quarantined;

  private static bool IsQuarantined(ZDOID target, ZDOID actor) =>
    TeleportManager.IsPlayerWriteQuarantined(actor) ||
    TeleportManager.IsPlayerWriteQuarantined(target);

  private void ExecuteAction()
  {
    Manager.Rpc(Source, Target, Zdo, Hash, Parameters, Actor);
  }
}
