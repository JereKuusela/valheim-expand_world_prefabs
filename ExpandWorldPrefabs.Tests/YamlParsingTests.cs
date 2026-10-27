using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

public class YamlParsingTests
{
  [Test]
  public void MixedYamlParsingIsAtomic()
  {
    const string source = "- prefab: Player\n  type: create\n- name: valid_data\n  value: example\n- config: Custom\n  key: [unclosed\n";
    Assert.That(Yaml.ParseMixed("mixed.yaml", source, out var entries, out var error), Is.False);
    Assert.That(error, Is.Not.Empty);
    Assert.That(entries.ScriptEntries, Is.Empty);
    Assert.That(entries.DataEntries, Is.Empty);
    Assert.That(entries.ConfigEntries, Is.Empty);
  }

  [Test]
  public void FixedYamlCanBeParsedAfterMixedSyntaxError()
  {
    const string broken = "- prefab: Player\n  type: create\n- name: valid_data\n  value: example\n- config: Custom\n  key: [unclosed\n";
    const string fixedYaml = "- prefab: Player\n  type: create\n- name: valid_data\n  value: example\n";
    Assert.That(Yaml.ParseMixed("mixed.yaml", broken, out _, out _), Is.False);
    Assert.That(Yaml.ParseMixed("mixed.yaml", fixedYaml, out var repaired, out var error), Is.True, error);
    Assert.That(repaired.ScriptEntries, Has.Count.EqualTo(1));
    Assert.That(repaired.DataEntries, Has.Count.EqualTo(1));
  }

  [Test]
  [NonParallelizable]
  public void UnknownYamlFieldStillReportsStrictParserError()
  {
    var loggerField = typeof(Log).GetField("Logger", BindingFlags.Static | BindingFlags.NonPublic)!;
    var previousLogger = loggerField.GetValue(null);
    using var listener = new ErrorListener();
    using var source = new ManualLogSource("YAML fallback test");
    BepInEx.Logging.Logger.Listeners.Add(listener);
    BepInEx.Logging.Logger.Sources.Add(source);
    try
    {
      Log.Init(source);
      const string yaml = "- name: check\n  value: okay\n  unknownProperty: ignored\n";
      Assert.That(Yaml.ParseMixed("fallback-check.yaml", yaml, out var entries, out var error), Is.True, error);
      Assert.That(entries.DataEntries, Has.Count.EqualTo(1));
      Assert.That(listener.Errors.Any(message => message.Contains("fallback-check.yaml")), Is.True,
        "A successful tolerant parse must still report the strict parser error.");
    }
    finally
    {
      BepInEx.Logging.Logger.Sources.Remove(source);
      BepInEx.Logging.Logger.Listeners.Remove(listener);
      loggerField.SetValue(null, previousLogger);
    }
  }

  private sealed class ErrorListener : ILogListener
  {
    public readonly List<string> Errors = [];
    public LogLevel LogLevelFilter => LogLevel.Error;
    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
      if (eventArgs.Level == LogLevel.Error)
        Errors.Add(eventArgs.Data?.ToString() ?? "");
    }
    public void Dispose() { }
  }
}
