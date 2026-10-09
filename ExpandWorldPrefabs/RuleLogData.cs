using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Common;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace ExpandWorld.Prefab;

// Only these two field types accept flexible YAML. Other string fields retain
// their existing schema and error handling.
public sealed class RuleLogData(object? value)
{
  internal readonly object? Value = value;

  internal RuleLogSource[] Normalize(RuleLogFiles? files, Action<string> warning)
  {
    var result = new List<RuleLogSource>();
    var budget = new RuleLogBudget();
    void Add(object? message, RuleLogFiles? destinations)
    {
      if (message == null)
      { warning("Log messages must be strings; invalid item skipped."); return; }
      if (message is IList lines)
      {
        if (lines.Count == 0) warning("Empty log message list; logging item skipped.");
        foreach (var line in lines)
        {
          if (line is string text) Add(text, destinations);
          else warning("Nested log messages must be strings; invalid item skipped.");
        }
        return;
      }
      if (message is not string template)
      { warning("Log messages must be strings; invalid item skipped."); return; }
      var names = (destinations ?? files ?? RuleLogFiles.Default).Names(warning);
      if (names.Length > 0) result.Add(new RuleLogSource(template, names, budget));
    }
    if (Value is IList entries)
    {
      if (entries.Count == 0) warning("Empty log list; logging skipped.");
      foreach (var entry in entries)
      {
        if (entry is not IDictionary mapping) { Add(entry, files); continue; }
        var invalid = mapping.Keys.Cast<object>().Any(key => key is not string name || (name != "log" && name != "logFile"));
        if (invalid || !mapping.Contains("log"))
        { warning("Log entries require log and may contain logFile only; invalid item skipped."); continue; }
        Add(mapping["log"], mapping.Contains("logFile") ? new RuleLogFiles(mapping["logFile"]) : files);
      }
    }
    else Add(Value, files);
    return result.ToArray();
  }
}

public sealed class RuleLogFiles(object? value)
{
  internal const string DefaultName = "ewp_log";
  internal static readonly RuleLogFiles Default = new(DefaultName);
  internal readonly object? Value = value;

  internal string[] Names(Action<string> warning)
  {
    if (Value is not string text)
    { warning("logFile must be a comma-separated string; logging item skipped."); return []; }
    var names = Parse.ToList(text, false).Select(name => name.ToLowerInvariant()).Distinct().ToArray();
    foreach (var name in names)
    {
      if (IsValid(name)) continue;
      warning("Invalid logFile name '" + name + "'. Use 1-64 letters, numbers, underscores or hyphens, without a path or extension; logging item skipped.");
      return [];
    }
    return names;
  }

  internal static bool IsValid(string name)
  {
    if (name.Length == 0 || name.Length > 64 || name.Any(c =>
      !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')) return false;
    if (name == "con" || name == "prn" || name == "aux" || name == "nul" || name == "clock$") return false;
    return !(name.Length == 4 && (name.StartsWith("com") || name.StartsWith("lpt")) && name[3] >= '1' && name[3] <= '9');
  }
}

internal sealed class RuleLogYamlConverter : IYamlTypeConverter
{
  public bool Accepts(Type type) => type == typeof(RuleLogData) || type == typeof(RuleLogFiles);
  public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
  {
    var value = rootDeserializer(typeof(object));
    return type == typeof(RuleLogData) ? new RuleLogData(value) : new RuleLogFiles(value);
  }
  public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
    serializer(value is RuleLogData data ? data.Value : (value as RuleLogFiles)?.Value, typeof(object));
}
