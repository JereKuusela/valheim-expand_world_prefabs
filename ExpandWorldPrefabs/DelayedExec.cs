using System;
using System.Collections.Generic;
using Common;
using Data;
using Service;
namespace ExpandWorld.Prefab;

public class DelayedExec(double due, Functions f, string rest)
{
  private const string Wait = "<wait_";
  private static readonly List<DelayedExec> Execs = [];
  public static void Clear() => Execs.Clear();

  public static void Run(Functions f, string value)
  {
    var start = value.IndexOf(Wait, StringComparison.Ordinal);
    var end = start < 0 ? -1 : FindEnd(value, start);
    if (end < 0)
    {
      f.Replace(value);
      return;
    }
    f.Replace(value.Substring(0, start));
    var rest = value.Substring(end + 1);
    var arg = f.Replace(value.Substring(start + Wait.Length, end - start - Wait.Length));
    if (Parse.TryFloat(arg, out var delay) && delay > 0f)
      Execs.Add(new(ZNet.instance.m_netTime + delay, f, rest));
    else
      Run(f, rest);
  }
  private static int FindEnd(string value, int start)
  {
    var nesting = 0;
    for (var i = start; i < value.Length; i++)
    {
      if (value[i] == '<') nesting++;
      if (value[i] == '>' && --nesting == 0) return i;
    }
    return -1;
  }
  public static void Execute()
  {
    for (var i = 0; i < Execs.Count; i++)
    {
      var exec = Execs[i];
      if (exec.Due > ZNet.instance.m_netTime) continue;
      Execs.RemoveAt(i);
      i--;
      Run(exec.F, exec.Rest);
    }
  }
  private readonly double Due = due;
  private readonly Functions F = f;
  private readonly string Rest = rest;
}
