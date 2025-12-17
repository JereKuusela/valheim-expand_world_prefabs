using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Data;
using Service;

namespace ExpandWorld.Prefab;

public class Commands
{

  public static void Run(Info info, Parameters pars)
  {
    if (info.Commands.Length == 0) return;
    var commands = info.Commands.Select(c => pars.Replace(c, true)).ToArray();
    Run(commands);
  }

  private static void Run(IEnumerable<string> commands)
  {
    var parsed = commands.Select(Parse).ToArray();
    foreach (var cmd in parsed)
    {
      try
      {
        Console.instance.TryRunCommand(cmd);
      }
      catch (Exception e)
      {
        Log.Error($"Failed to run command: {cmd}\n{e.Message}");
      }
    }
  }
  private static string Parse(string command)
  {
    var expressions = command.Split(' ').Select(s => s.Split('=')).Select(a => a[a.Length - 1].Trim()).SelectMany(s => s.Split(',')).ToArray();
    foreach (var expression in expressions)
    {
      if (expression.Length == 0) continue;
      // Single negative number would get handled as expression.
      var sub = expression.Substring(1);
      if (!sub.Contains('*') && !sub.Contains('/') && !sub.Contains('+') && !sub.Contains('-')) continue;
      var value = Calculator.EvaluateFloat(expression);
      if (value == null) continue;
      int pos = command.IndexOf(expression);
      if (pos < 0) continue;
      command = command.Substring(0, pos) + value.Value.ToString("0.#####", NumberFormatInfo.InvariantInfo) + command.Substring(pos + expression.Length);
    }
    return command;
  }
}