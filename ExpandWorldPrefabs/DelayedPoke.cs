using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;
namespace ExpandWorld.Prefab;


public interface IPokeable
{
  public float Delay { get; set; }
  void Execute();
}
public class DelayedSinglePoke(float delay, ZDOID zdo, string[] args) : DelayedPoke, IPokeable
{
  private readonly ZDOID Zdo = zdo;
  private readonly string[] Args = args;

  float IPokeable.Delay { get => delay; set => delay = value; }
  public void Execute() => Poke(Zdo, Args);

}
public class DelayedMultiPoke(float delay, ZDOID[] zdos, string[] args) : DelayedPoke, IPokeable
{
  private readonly ZDOID[] Zdos = zdos;
  private readonly string[] Args = args;

  float IPokeable.Delay { get => delay; set => delay = value; }
  public void Execute() => Poke(Zdos, Args);

}

public class DelayedPoke
{
  private static readonly List<IPokeable> Pokes = [];
  public static void Add(Poke poke, ZDOID zdo, Vector3 pos, Quaternion rot, Parameters pars)
  {
    var chance = poke.Chance?.Get(pars) ?? 1f;
    if (chance < 1f && Random.value > chance)
      return;

    var delay = poke.Delay?.Get(pars) ?? 0f;
    var repeat = poke.Repeat?.Get(pars) ?? 0;
    var repeatInterval = poke.RepeatInterval?.Get(pars) ?? delay;
    var repeatChance = poke.RepeatChance?.Get(pars) ?? 1f;
    var delays = Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
    if (delays != null)
    {
      foreach (var d in delays)
        Add(poke, zdo, pos, rot, pars, d);
    }
    else
      Add(poke, zdo, pos, rot, pars, delay);

  }
  private static void Add(Poke poke, ZDOID zdo, Vector3 pos, Quaternion rot, Parameters pars, float delay)
  {
    var self = poke.Self?.GetBool(pars);
    var connected = poke.Connected?.GetBool(pars) == true;
    var target = poke.Target?.Get(pars);
    if (poke.HasPrefab)
    {
      var zdos = ObjectsFiltering.GetNearby(poke.Limit?.Get(pars) ?? 0, poke.Filter, pos, rot, pars, self == true ? null : zdo).ToList();
      if (connected)
      {
        var connectedZdos = new HashSet<ZDOID>(Hack.GetConnnected(zdo));
        zdos.RemoveAll(id => !connectedZdos.Contains(id));
      }
      if (zdos.Count == 0) return;
      pars.Amount = zdos.Count;
      var args = poke.GetArgs(pars);
      Add(delay, [.. zdos], args);
    }
    else if (self == true || target != null || connected)
    {
      HashSet<ZDOID> targets = [];
      if (self == true)
        targets.Add(zdo);
      if (target != null && (self == true || target.Value != zdo))
        targets.Add(target.Value);
      if (connected)
        targets.UnionWith(Hack.GetConnnected(zdo));
      if (targets.Count == 0) return;
      var args = poke.GetArgs(pars);
      Add(delay, [.. targets], args);
    }
  }
  public static void AddGlobal(Poke poke, Vector3 pos, Quaternion rot, Parameters pars)
  {
    var chance = poke.Chance?.Get(pars) ?? 1f;
    if (chance < 1f && Random.value > chance)
      return;

    var delay = poke.Delay?.Get(pars) ?? 0f;
    var repeat = poke.Repeat?.Get(pars) ?? 0;
    var repeatInterval = poke.RepeatInterval?.Get(pars) ?? delay;
    var repeatChance = poke.RepeatChance?.Get(pars) ?? 1f;
    var delays = Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
    if (delays != null)
    {
      foreach (var d in delays)
        AddGlobal(poke, pos, rot, pars, d);
    }
    else
      AddGlobal(poke, pos, rot, pars, delay);
  }
  private static void AddGlobal(Poke poke, Vector3 pos, Quaternion rot, Parameters pars, float delay)
  {
    var args = poke.GetArgs(pars);
    var zdos = ObjectsFiltering.GetNearby(poke.Limit?.Get(pars) ?? 0, poke.Filter, pos, rot, pars, null);
    Add(delay, zdos, args);
  }
  public static void Add(float delay, ZDOID[] zdos, string[] args)
  {
    if (delay <= 0f)
      Poke(zdos, args);
    else
      Pokes.Add(new DelayedMultiPoke(delay, zdos, args));
  }
  private static void Add(float delay, ZDOID zdo, string[] args)
  {
    if (delay <= 0f)
      Poke(zdo, args);
    else
      Pokes.Add(new DelayedSinglePoke(delay, zdo, args));
  }
  public static void Execute(float dt)
  {
    // Two loops to preserve order.
    for (var i = 0; i < Pokes.Count; i++)
    {
      var poke = Pokes[i];
      poke.Delay -= dt;
      if (poke.Delay > -0.001) continue;
      poke.Execute();
      Pokes.RemoveAt(i);
      i--;
    }
  }

  protected static void Poke(ZDOID[] zdos, string[] args)
  {
    foreach (var z in zdos)
      Manager.Handle(ActionType.Poke, args, z);
  }
  protected static void Poke(ZDOID zdo, string[] args)
  {
    Manager.Handle(ActionType.Poke, args, zdo);
  }
}