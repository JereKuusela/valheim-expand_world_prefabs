using System;
using System.Collections.Generic;
using System.IO;
using Data;
using Service;

namespace ExpandWorld.Prefab;

internal static class RuleLog
{
  private static BufferedRuleLog? Writer;
  private static readonly Func<string, Functions, string> Format =
    (template, functions) => functions.Replace(template, false, false);

  public static void Init(string directory)
  {
    // Never replace a worker that might still own the file.
    if (Writer != null) return;
    try
    {
      var maximumBytes = Config.RuleLogMaximumFileBytes;
      var segments = Config.RuleLogSegments;
      Writer = new BufferedRuleLog(name => new RollingRuleLogWriter(
        Path.Combine(directory, name + ".txt"), maximumBytes, segments), Config.GetRuleLogOptions(), Log.Warning);
      Writer.SetEnabled(Config.RuleLogging);
      Log.Info("Rule log files: " + directory + "; segment size=" + (maximumBytes / (1024 * 1024)) +
        " MiB; retained segments=" + segments + " per log.");
    }
    catch (Exception e)
    {
      Log.Error("Unable to start rule logging. Gameplay will continue without it. " + e.Message);
    }
  }

  public static void Configure(IEnumerable<string> names)
  {
    var rejected = Writer?.SetDestinations(names);
    if (rejected != null && rejected.Length > 0)
      Log.Warning("Rule logging tracks at most " + BufferedRuleLog.MaximumDestinations +
        " destination names until restart. Skipping: " + string.Join(", ", rejected));
  }

  public static void Write(RuleLogSource[] sources, Functions functions)
  {
    foreach (var source in sources) Writer?.TryWrite(source, functions, Format);
  }
  public static void SetEnabled(bool enabled) => Writer?.SetEnabled(enabled);
  public static void Close() => Writer?.Stop(1000);
}
