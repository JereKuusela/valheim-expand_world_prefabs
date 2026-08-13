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
  public static void Add(Poke poke, ZDOID zdo, Vector3 pos, Quaternion rot, Functions f)
  {
    var chance = poke.Chance?.Get(f) ?? 1f;
    if (chance < 1f && Random.value > chance)
      return;

    var delay = poke.Delay?.Get(f) ?? 0f;
    var repeat = poke.Repeat?.Get(f) ?? 0;
    var repeatInterval = poke.RepeatInterval?.Get(f) ?? delay;
    var repeatChance = poke.RepeatChance?.Get(f) ?? 1f;
    var delays = Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
    if (delays != null)
    {
      foreach (var d in delays)
        Add(poke, zdo, pos, rot, f, d);
    }
    else
      Add(poke, zdo, pos, rot, f, delay);

  }
  private static void Add(Poke poke, ZDOID zdo, Vector3 pos, Quaternion rot, Functions f, float delay)
  {
    var connected = poke.Connected?.GetBool(f) == true;
    if (poke.HasPrefab)
    {
      var random = poke.Random?.GetBool(f) == true;
      var zdos = ObjectsFiltering.GetNearby(poke.Limit?.Get(f) ?? 0, poke.Filter, pos, rot, f, zdo, random).ToList();
      if (connected)
      {
        var connectedZdos = new HashSet<ZDOID>(SupportAttach.GetConnnected(zdo));
        zdos.RemoveAll(id => !connectedZdos.Contains(id));
      }
      if (zdos.Count == 0) return;
      f.Amount = zdos.Count;
      var args = poke.GetArgs(f);
      Add(delay, [.. zdos], args);
      return;
    }
    var self = poke.Filter.AllowSelf(f);
    var target = poke.Target?.Get(f);
    if (self || target != null || connected)
    {
      HashSet<ZDOID> targets = [];
      if (self)
        targets.Add(zdo);
      if (target != null && (self || target.Value != zdo))
        targets.Add(target.Value);
      if (connected)
        targets.UnionWith(SupportAttach.GetConnnected(zdo));
      if (targets.Count == 0) return;
      var args = poke.GetArgs(f);
      Add(delay, [.. targets], args);
    }
  }
  public static void AddGlobal(Poke poke, Vector3 pos, Quaternion rot, Functions f)
  {
    var chance = poke.Chance?.Get(f) ?? 1f;
    if (chance < 1f && Random.value > chance)
      return;

    var delay = poke.Delay?.Get(f) ?? 0f;
    var repeat = poke.Repeat?.Get(f) ?? 0;
    var repeatInterval = poke.RepeatInterval?.Get(f) ?? delay;
    var repeatChance = poke.RepeatChance?.Get(f) ?? 1f;
    var delays = Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
    if (delays != null)
    {
      foreach (var d in delays)
        AddGlobal(poke, pos, rot, f, d);
    }
    else
      AddGlobal(poke, pos, rot, f, delay);
  }
  private static void AddGlobal(Poke poke, Vector3 pos, Quaternion rot, Functions f, float delay)
  {
    var args = poke.GetArgs(f);
    var random = poke.Random?.GetBool(f) == true;
    var zdos = ObjectsFiltering.GetNearby(poke.Limit?.Get(f) ?? 0, poke.Filter, pos, rot, f, null, random);
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