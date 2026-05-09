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

      /**
      * Bug fix: Skip evaluation if this looks like a formatted date/time string
      * (e.g., "2026-05-04" or "12-30-45" from realtime parameter replacement)
      * These contain only digits and separators, not actual math operations.
      * I can check if removing '-', '/', '.' leaves only digits and if so, it's most likely a date, not arithmetic.
      */
      if (IsLikelyDateTimeString(expression)) continue;

      var value = Calculator.EvaluateFloat(expression);
      if (value == null) continue;
      int pos = command.IndexOf(expression);
      if (pos < 0) continue;
      command = command.Substring(0, pos) + value.Value.ToString("0.#####", NumberFormatInfo.InvariantInfo) + command.Substring(pos + expression.Length);
    }
    return command;
  }

  private static bool IsLikelyDateTimeString(string expressionP)
  {
    /** 
    * A date/time string like "2026-05-04" or "12-30-45" consists only of digits
    * and separator characters (-, /, .). If we remove those separators and find
    * only digits remain, it's not a real math expression it's a formatted date.
    */

    string withoutSeparators = expressionP.Replace("-", "").Replace("/", "").Replace(".", "");

    //removing the stuff... do I get just digits?
    for (int i = 0; i < withoutSeparators.Length; i++)
    {
      char c = withoutSeparators[i];
      if (c < '0' || c > '9')
      {
        // Found a non-digit character, abandon ship
        return false;
      }
    }

    // Only digits remain after removing separators so this is a date/time string most likely
    return true;
  }
}