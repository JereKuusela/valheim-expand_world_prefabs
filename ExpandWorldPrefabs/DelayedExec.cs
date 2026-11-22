using Data;
using UnityEngine;
namespace ExpandWorld.Prefab;

public class DelayedExec(double due, Exec exec, Functions f)
{
  private static readonly DelayedQueue<DelayedExec> Execs = new();
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
    {
      var delayed = new DelayedExec(ZNet.instance.m_netTime + delay, exec, f);
      Execs.Add(delayed.Due, delayed);
    }
  }
  public static void Execute() => Execs.Drain(ZNet.instance.m_netTime, static delayed =>
  {
    delayed.Exec.Run(delayed.F);
    return null;
  });
  private readonly double Due = due;
  private readonly Exec Exec = exec;
  private readonly Functions F = f;
}
