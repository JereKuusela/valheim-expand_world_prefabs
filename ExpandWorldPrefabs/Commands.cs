using System;
using System.Collections.Generic;
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
    foreach (var cmd in commands)
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
}