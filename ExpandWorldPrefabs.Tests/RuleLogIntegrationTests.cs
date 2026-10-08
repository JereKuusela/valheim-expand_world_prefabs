using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Data;
using ExpandWorld.Prefab;
using HarmonyLib;
using NUnit.Framework;
using Service;
using YamlDotNet.Serialization;

namespace ExpandWorldPrefabs.Tests;

[NonParallelizable]
public class RuleLogIntegrationTests
{
  private string ManagedDirectory = "";
  private Dictionary<FieldInfo, object?> PreviousPaths = [];

  [OneTimeSetUp]
  public void InitializeManagedPaths()
  {
    PreviousPaths = typeof(Paths).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
      .Where(field => !field.IsInitOnly).ToDictionary(field => field, field => (object?)field.GetValue(null));
    ManagedDirectory = TemporaryDirectory();
    // BepInEx may create its core config. Keep it outside the source checkout.
    AccessTools.Method(typeof(Paths), "SetExecutablePath").Invoke(null,
      [Path.Combine(ManagedDirectory, "valheim.exe"), Path.Combine(ManagedDirectory, "BepInEx"), null, null]);
  }

  [OneTimeTearDown]
  public void RestoreManagedPaths()
  {
    foreach (var pair in PreviousPaths) pair.Key.SetValue(null, pair.Value);
    if (Directory.Exists(ManagedDirectory)) Directory.Delete(ManagedDirectory, true);
  }

  private static string TemporaryDirectory()
  {
    var path = Path.Combine(Path.GetTempPath(), "ewp-rule-log-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
  }

  private static string RepositoryFile(string name)
  {
    var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
    while (directory != null)
    {
      var path = Path.Combine(directory.FullName, name);
      if (File.Exists(path)) return path;
      directory = directory.Parent;
    }
    throw new FileNotFoundException("Repository file not found: " + name);
  }

  private static T Uninitialized<T>()
  {
#pragma warning disable SYSLIB0050
    return (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
  }

  [Test]
  public void LoggingSmokeFileLoadsThroughProductionPreprocessing()
  {
    var loaded = Yaml.ReadMixedFile(RepositoryFile("docs/logging-smoke.yaml"), true);
    Assert.That(loaded.DataEntries, Is.Empty);
    Assert.That(loaded.ScriptEntries.Count, Is.EqualTo(2));
    var warnings = new List<string>();
    var scalar = loaded.ScriptEntries[0];
    Assert.That(scalar.log!.Normalize(scalar.logFile, warnings.Add).Length, Is.EqualTo(1));
    var rule = loaded.ScriptEntries[1];
    var sources = rule.log!.Normalize(rule.logFile, warnings.Add);
    Assert.That(warnings, Is.Empty);
    Assert.That(sources.Length, Is.EqualTo(3));
    Assert.That(sources[0].Files, Is.EqualTo(new[] { "ewp_log", "logging_smoke" }));
    Assert.That(sources[1].Files, Is.EqualTo(new[] { "logging_details" }));
    Assert.That(sources[2].Files, Is.EqualTo(new[] { "logging_details" }));
    Assert.That(sources[2].Template, Is.EqualTo("SMOKE multiline λ\nsecond line, intact"));
  }

  [TestCase("Deserializer")]
  [TestCase("DeserializerUnSafe")]
  public void ProductionDeserializersAcceptLoggingForms(string method)
  {
    // Initialize only managed paths; no Unity scene or game process is started.
    var deserializer = (IDeserializer)AccessTools.Method(typeof(Yaml), method).Invoke(null, null);
    var rule = deserializer.Deserialize<ExpandWorld.Prefab.Data>(
      "command: preserved\nlogFile: shared\nlog:\n- 'é, λ, 🜂'\n- logFile: other\n  log: |\n    first\n    second\n");
    var warnings = new List<string>();
    var sources = rule.log!.Normalize(rule.logFile, warnings.Add);
    Assert.That(warnings, Is.Empty);
    Assert.That(rule.command, Is.EqualTo("preserved"));
    Assert.That(sources.Select(source => source.Template), Is.EqualTo(new[] { "é, λ, 🜂", "first\nsecond\n" }));
    Assert.That(sources.Select(source => source.Files.Single()), Is.EqualTo(new[] { "shared", "other" }));
    Assert.Throws<YamlDotNet.Core.YamlException>(() => deserializer.Deserialize<ExpandWorld.Prefab.Data>("command: [invalid]\nlog: valid"));
  }

  [Test]
  public void ExpandedActionTypesShareOneAuthoredRuleBudget()
  {
    var instance = AccessTools.Field(typeof(ZoneSystem), "s_instance");
    var previous = instance.GetValue(null);
    instance.SetValue(null, Uninitialized<ZoneSystem>());
    try
    {
      var rule = new ExpandWorld.Prefab.Data
      {
        prefab = "Player", types = ["create", "destroy", "state, join"],
        log = new RuleLogData(new[] { "one", "two" })
      };
      var infos = Loading.FromData(rule);
      Assert.That(infos.Length, Is.EqualTo(3));
      Assert.That(infos.All(info => ReferenceEquals(info.LogSources, infos[0].LogSources)), Is.True);
      var sources = infos[0].LogSources!;
      Assert.That(ReferenceEquals(sources[0].Budget, sources[1].Budget), Is.True);
      Assert.That(ReferenceEquals(Loading.FromData(rule)[0].LogSources![0].Budget, sources[0].Budget), Is.False,
        "Reload creates a new budget.");
      var sink = new StringWriter();
      var worker = new BufferedRuleLog(() => sink, new RuleLogOptions { RuleRate = 1 }, _ => { });
      try
      {
        Assert.That(worker.TryWrite(sources[0], "", (text, _) => text), Is.True);
        Assert.That(worker.TryWrite(infos[1].LogSources![1], "", (text, _) => text), Is.False);
      }
      finally { Assert.That(worker.Stop(1000), Is.True); }
    }
    finally { instance.SetValue(null, previous); }
  }

  [Test]
  public void InvalidInheritanceDoesNotSuppressExplicitOverride()
  {
    var data = new RuleLogData(new object[]
    {
      "inherits invalid destination",
      new Dictionary<string, object> { ["log"] = "valid", ["logFile"] = "good" },
      new Dictionary<string, object> { ["log"] = "invalid override", ["logFile"] = new[] { "bad" } }
    });
    var warnings = new List<string>();
    var sources = data.Normalize(new RuleLogFiles("../bad"), warnings.Add);
    Assert.That(sources.Select(source => source.Template), Is.EqualTo(new[] { "valid" }));
    Assert.That(sources[0].Files, Is.EqualTo(new[] { "good" }));
    Assert.That(warnings.Count, Is.EqualTo(2));
  }

  [Test]
  public void InvalidMessageTypesWarnLocally()
  {
    var warnings = new List<string>();
    var sources = new RuleLogData(new object?[] { 123, true, null, new object[] { false }, "valid" })
      .Normalize(null, warnings.Add);
    Assert.That(sources.Select(source => source.Template), Is.EqualTo(new[] { "valid" }));
    Assert.That(warnings.Count, Is.EqualTo(4));
    Assert.That(new RuleLogData(123).Normalize(null, warnings.Add), Is.Empty);
    Assert.That(new RuleLogFiles(123).Names(warnings.Add), Is.Empty);
  }

  [TestCase("PRN")]
  [TestCase("aux")]
  [TestCase("nul")]
  [TestCase("clock$")]
  [TestCase("LPT1")]
  [TestCase("com9")]
  [TestCase("lpt9")]
  [TestCase("a\\b")]
  [TestCase("/absolute")]
  [TestCase(".hidden")]
  [TestCase("a:b")]
  [TestCase("é")]
  [TestCase("a\0b")]
  public void InvalidDestinationNamesAreRejected(string name)
  {
    var warnings = new List<string>();
    Assert.That(new RuleLogFiles(name).Names(warnings.Add), Is.Empty);
    Assert.That(warnings, Is.Not.Empty);
  }

  [Test]
  public void ValidNameBoundariesAreNormalized()
  {
    var warnings = new List<string>();
    var longest = new string('a', 64);
    Assert.That(new RuleLogFiles("A, _-09, " + longest + ", a").Names(warnings.Add),
      Is.EqualTo(new[] { "a", "_-09", longest }));
    Assert.That(warnings, Is.Empty);
  }

  [Test]
  public void ReaddedDestinationReopensAndAppends()
  {
    var directory = TemporaryDirectory();
    var closes = 0;
    var worker = new BufferedRuleLog(name => new CloseNotice(
      new RollingRuleLogWriter(Path.Combine(directory, name + ".txt"), 1024, 4),
      () => Interlocked.Increment(ref closes)), new RuleLogOptions(), _ => { });
    var source = new RuleLogSource("record", ["named"]);
    try
    {
      worker.SetDestinations(["named"]);
      Assert.That(worker.TryWrite(source, "", (text, _) => text), Is.True);
      worker.SetDestinations([]);
      Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref closes) == 1, 3000), Is.True);
      Assert.That(File.ReadAllText(Path.Combine(directory, "named.txt")), Is.EqualTo("record" + Environment.NewLine));
      worker.SetDestinations(["named"]);
      Assert.That(worker.TryWrite(source, "", (text, _) => text), Is.True);
      Assert.That(worker.Stop(1000), Is.True);
      Assert.That(closes, Is.EqualTo(2));
      Assert.That(File.ReadAllLines(Path.Combine(directory, "named.txt")), Is.EqualTo(new[] { "record", "record" }));
    }
    finally { worker.Stop(1000); Directory.Delete(directory, true); }
  }

  private sealed class CloseNotice(TextWriter inner, Action close) : TextWriter
  {
    public override Encoding Encoding => inner.Encoding;
    public override void WriteLine(string? value) => inner.WriteLine(value);
    public override void Flush() => inner.Flush();
    protected override void Dispose(bool disposing)
    {
      if (disposing) { inner.Dispose(); close(); }
      base.Dispose(disposing);
    }
  }

  [Test]
  public void LoweredByteLimitPreservesExistingOversizedSegments()
  {
    var directory = TemporaryDirectory();
    var path = Path.Combine(directory, "named.txt");
    var old = new string('x', 128) + "\n";
    try
    {
      File.WriteAllText(path, old, new UTF8Encoding(false));
      using (var writer = new RollingRuleLogWriter(path, 16, 4)) writer.WriteLine("next");
      Assert.That(File.ReadAllText(Path.Combine(directory, "named.1.txt")), Is.EqualTo(old));
      Assert.That(File.ReadAllText(path), Is.EqualTo("next" + Environment.NewLine));
    }
    finally { Directory.Delete(directory, true); }
  }

  [TestCase("registerSimpleFunctionHandlerMethod", "RegisterFunctionHandler", false)]
  [TestCase("registerValueFunctionHandlerMethod", "RegisterValueFunctionHandler", true)]
  public void DevelopersLookupMatchesActualApi(string variable, string expected, bool withValue)
  {
    var text = File.ReadAllText(RepositoryFile("developers.md"));
    var match = Regex.Match(text, "\\b" + variable + " = AccessTools\\.Method\\(type, \"([^\"]+)\"");
    Assert.That(match.Success, Is.True);
    Assert.That(match.Groups[1].Value, Is.EqualTo(expected));
    var handlerType = withValue ? typeof(Func<string, string>) : typeof(Func<string>);
    var method = AccessTools.Method(typeof(Api), match.Groups[1].Value, [typeof(string), handlerType]);
    Assert.That(method, Is.Not.Null);
    const string key = "rule_log_lookup_test";
    object handler = withValue ? (object)new Func<string, string>(value => "value=" + value) : new Func<string>(() => "simple");
    try
    {
      method.Invoke(null, [key, handler]);
      Assert.That(withValue ? Api.ResolveValueFunction(key, "argument") : Api.ResolveFunction(key),
        Is.EqualTo(withValue ? "value=argument" : "simple"));
    }
    finally { Api.UnregisterFunctionHandler(key); }
  }

  [Test]
  public void RuleLogGlueResolvesFanoutOnceAndLeavesOldRootLogUntouched()
  {
    var directory = TemporaryDirectory();
    var writerField = AccessTools.Field(typeof(RuleLog), "Writer");
    var previousWriter = writerField.GetValue(null);
    var loggerField = AccessTools.Field(typeof(Log), "Logger");
    var previousLogger = loggerField.GetValue(null);
    var previousConfig = typeof(Config).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
      .Where(field => field.Name.StartsWith("Config")).ToDictionary(field => field, field => field.GetValue(null));
    const string key = "rule_log_fanout_test";
    var calls = 0;
    try
    {
      File.WriteAllText(Path.Combine(directory, "ewp_log.txt"), "old root log", new UTF8Encoding(false));
      Log.Init(new ManualLogSource("EWP rule log tests"));
      Config.Init(new ConfigFile(Path.Combine(directory, "test.cfg"), false));
      writerField.SetValue(null, null);
      RuleLog.Init(Path.Combine(directory, "logs"));
      RuleLog.Configure(["ewp_log", "named"]);
      Assert.That(Config.RuleLogMaximumFileBytes, Is.EqualTo(256L * 1024 * 1024));
      Assert.That(Config.RuleLogSegments, Is.EqualTo(4));
      Api.RegisterFunctionHandler(key, () => { calls++; return "é; intact, λ\nsecond line"; });
      RuleLog.Write([new RuleLogSource("<" + key + ">", ["ewp_log", "named", "named"])], Uninitialized<Functions>());
      RuleLog.Close();
      Assert.That(calls, Is.EqualTo(1));
      Assert.That(File.ReadAllText(Path.Combine(directory, "logs/ewp_log.txt")),
        Is.EqualTo("é; intact, λ\nsecond line" + Environment.NewLine));
      Assert.That(File.ReadAllBytes(Path.Combine(directory, "logs/named.txt")),
        Is.EqualTo(File.ReadAllBytes(Path.Combine(directory, "logs/ewp_log.txt"))));
      Assert.That(File.ReadAllText(Path.Combine(directory, "ewp_log.txt")), Is.EqualTo("old root log"));
    }
    finally
    {
      RuleLog.Close();
      writerField.SetValue(null, previousWriter);
      foreach (var pair in previousConfig) pair.Key.SetValue(null, pair.Value);
      loggerField.SetValue(null, previousLogger);
      Api.UnregisterFunctionHandler(key);
      Directory.Delete(directory, true);
    }
  }
}
