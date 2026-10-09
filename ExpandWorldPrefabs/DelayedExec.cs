using System.Collections.Generic;
using Data;
using UnityEngine;
namespace ExpandWorld.Prefab;

public class DelayedExec(double due, Exec exec, Functions f)
{
  private static readonly List<DelayedExec> Execs = [];
  public static void Clear() => Execs.Clear();

  public static void Add(Rule info, Functions f)
  {
    if (info.Execs != null)
    {
      foreach (var exec in info.Execs)
        Add(exec, f);
    }
    var weighted = info.GetWeightedExec(f);
    if (weighted != null)
      Add(weighted, f);
  }
  private static void Add(Exec exec, Functions f)
  {
    var chance = exec.Chance?.Get(f) ?? 1f;
    if (chance < 1f && Random.value > chance)
      return;

    var delay = exec.Delay?.Get(f) ?? 0f;
    var repeat = exec.Repeat?.Get(f) ?? 0;
    var repeatInterval = exec.RepeatInterval?.Get(f) ?? delay;
    var repeatChance = exec.RepeatChance?.Get(f) ?? 1f;
    var delays = Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
    if (delays != null)
    {
      foreach (var d in delays)
        Add(exec, f, d);
    }
    else
      Add(exec, f, delay);
  }
  private static void Add(Exec exec, Functions f, float delay)
  {
    if (delay <= 0f)
      exec.Run(f);
    else
      Execs.Add(new(ZNet.instance.m_netTime + delay, exec, f));
  }
  public static void Execute()
  {
    for (var i = 0; i < Execs.Count; i++)
    {
      var delayed = Execs[i];
      if (delayed.Due > ZNet.instance.m_netTime) continue;
      Execs.RemoveAt(i);
      i--;
      delayed.Exec.Run(delayed.F);
    }
  }
  private readonly double Due = due;
  private readonly Exec Exec = exec;
  private readonly Functions F = f;
}
