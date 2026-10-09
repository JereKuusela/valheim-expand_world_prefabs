using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ExpandWorld.Prefab;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExpandWorldPrefabs.Tests;

internal static class NamedRuleLogChecks
{
  private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
  private static void Until(Func<bool> condition, string message) => Check(SpinWait.SpinUntil(condition, 3000), message);
  private static readonly Func<string, string, string> Format = (template, value) => template.Replace("<value>", value);

  internal static readonly Action[] Tests = [YamlForms, InvalidYamlValues, SharedRuleBudget, FanoutAndFailureIsolation,
      QuietDestinationFlush, ReloadAndDestinationBound, DestinationGapSummaries, FanoutCapacityAndOpenFailure, RollingRetentionAndRestart,
      MultilineAndSingleSegment, RollingFailureIsolation];

  private static RuleLogSource[] Parse(string yaml, out List<string> warnings, out ExpandWorld.Prefab.RuleYaml data)
  {
    data = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance)
      .WithTypeConverter(new FlexibleYamlConverter()).Build().Deserialize<ExpandWorld.Prefab.RuleYaml>(yaml);
    var messages = new List<string>();
    var result = data.log?.Normalize(data.logFile, messages.Add) ?? [];
    warnings = messages;
    return result;
  }

  private static void YamlForms()
  {
    var sources = Parse("log: hello\n", out var warnings, out _);
    Check(sources.Length == 1 && sources[0].Files.SequenceEqual(new[] { "ewp_log" }) && warnings.Count == 0, "legacy scalar");
    sources = Parse("logFile: HEATMAP\nlog: 'hello, world'", out warnings, out _);
    Check(sources[0].Files.SequenceEqual(new[] { "heatmap" }) && sources[0].Template == "hello, world", "single name and message commas");
    sources = Parse("logFile: ewp_log, HeatMap, heatmap\nlog: '<value>'", out warnings, out _);
    Check(sources.Length == 1 && sources[0].Files.SequenceEqual(new[] { "ewp_log", "heatmap" }), "comma lists and deduplication");
    sources = Parse("logFile: admin\nlog:\n- logFile: ewp_log\n  log: human\n- logFile: heatmap\n  log:\n  - Time= <value>\n  - Player= Jack\n- log: inherited", out warnings, out _);
    Check(sources.Length == 4 && sources[0].Files[0] == "ewp_log" && sources[1].Files[0] == "heatmap" &&
      sources[2].Files[0] == "heatmap" && sources[3].Files[0] == "admin" && warnings.Count == 0, "structured entries/list messages/inheritance");
    Check(sources.All(s => ReferenceEquals(s.Budget, sources[0].Budget)), "one budget per YAML rule");
    sources = Parse("log:\n- one\n- two", out warnings, out _);
    Check(sources.Length == 2 && sources[0].Template == "one" && sources[1].Template == "two", "separate list entries");
    sources = Parse("log: |\n  Time: now\n  Player: Jack\n", out warnings, out _);
    Check(sources.Length == 1 && sources[0].Template == "Time: now\nPlayer: Jack\n", "existing multiline string including trailing newline");
    sources = Parse("log: |-\n  Time: now\n  Player: Jack\n", out warnings, out _);
    Check(sources[0].Template == "Time: now\nPlayer: Jack", "literal block chomping");
    sources = Parse("log: ''", out warnings, out _);
    Check(sources.Length == 1 && sources[0].Template == "", "legacy empty string");
  }

  private static void InvalidYamlValues()
  {
    foreach (var name in new[] { "", "../heatmap", "heatmap.txt", "con", "COM1", "a,bad/name", "a,,b", new string('x', 65), "a b", "<pname>" })
    {
      var sources = Parse("command: preserved\nlog: text\nlogFile: '" + name + "'", out var warnings, out var data);
      Check(sources.Length == 0 && warnings.Count > 0 && data.command?.Items[0] == "preserved", "invalid filename: " + name);
    }
    foreach (var yaml in new[] { "log: text\nlogFile: [heatmap]", "log: text\nlogFile: null", "log: []", "log: {wrong: message}",
      "log:\n- log: [ {Time: now} ]", "log:\n- log:\n  - log: too-deep", "log:\n- logFile: heatmap", "log:\n- log: text\n  typo: ignored" })
    {
      var sources = Parse("command: preserved\n" + yaml, out var warnings, out var data);
      Check(sources.Length == 0 && warnings.Count > 0 && data.command?.Items[0] == "preserved", "invalid logging must preserve other actions: " + yaml);
    }
    var valid = Parse("log:\n- log: {wrong: message}\n- logFile: heatmap\n  log: valid", out var notes, out _);
    Check(valid.Length == 1 && notes.Count > 0 && valid[0].Template == "valid", "one bad item must not suppress valid siblings");
  }

  private sealed class Sink : TextWriter
  {
    internal readonly ConcurrentQueue<string> Lines = new();
    internal int Flushes, Closes, Writes, ThreadId;
    internal bool Fail, WrongThread;
    public override Encoding Encoding => Encoding.UTF8;
    private void Own()
    {
      int id = Thread.CurrentThread.ManagedThreadId;
      Interlocked.CompareExchange(ref ThreadId, id, 0);
      if (ThreadId != id) WrongThread = true;
    }
    public override void WriteLine(string? message)
    {
      Own(); Interlocked.Increment(ref Writes);
      if (Fail) throw new IOException("injected");
      Lines.Enqueue(message ?? "");
    }
    public override void Flush() { Own(); Interlocked.Increment(ref Flushes); }
    protected override void Dispose(bool disposing) { Own(); Interlocked.Increment(ref Closes); }
  }

  private static void SharedRuleBudget()
  {
    var sink = new Sink(); var budget = new RuleLogBudget();
    var worker = new BufferedRuleLog(() => sink, new RuleLogOptions { RuleRate = 1 }, _ => { });
    try
    {
      Check(worker.TryWrite(new("one", budget: budget), "", Format), "first message admitted");
      Check(!worker.TryWrite(new("two", budget: budget), "", Format), "second message shares rule budget");
      Check(worker.TryWrite(new("other"), "", Format), "another rule has its own budget");
    }
    finally { worker.Stop(1000); }
  }

  private static void FanoutAndFailureIsolation()
  {
    var bad = new Sink { Fail = true }; var good = new Sink(); var notes = new ConcurrentQueue<string>();
    var worker = new BufferedRuleLog(name => name == "ewp_log" ? bad : good, new RuleLogOptions(), notes.Enqueue);
    worker.SetDestinations(new[] { "ewp_log", "heatmap" });
    int formats = 0;
    try
    {
      var source = new RuleLogSource("<value>", new[] { "ewp_log", "heatmap", "heatmap" });
      Check(worker.TryWrite(source, "unique", (template, value) => { formats++; return value; }), "fanout admission");
      Until(() => good.Lines.Count == 1 && notes.Count == 1, "failed general log must not prevent heatmap copy");
      Check(formats == 1 && bad.Writes == 1 && good.Writes == 1, "resolve once, deduplicate, and latch failure");
      worker.SetDestinations(new[] { "ewp_log", "heatmap" });
      Check(worker.TryWrite(source, "second", Format), "healthy destination remains writable after reload");
      Until(() => good.Lines.Count == 2, "second heatmap record");
      Check(bad.Writes == 1 && notes.Single().Contains("ewp_log.txt"), "failure remains latched until restart and identifies filename");
      var broken = new RuleLogSource("<bad>", new[] { "heatmap" });
      Check(!worker.TryWrite(broken, "", (_, _) => throw new Exception()), "broken formatting");
      Check(worker.TryWrite(new("other", new[] { "heatmap" }), "", Format), "other message remains available");
    }
    finally { Check(worker.Stop(1000), "fanout shutdown"); }
    Check(!good.WrongThread && !bad.WrongThread && good.ThreadId == bad.ThreadId, "one worker owns every writer");
  }

  private static void QuietDestinationFlush()
  {
    var quiet = new Sink(); var busy = new Sink();
    var worker = new BufferedRuleLog(name => name == "quiet" ? quiet : busy,
      new RuleLogOptions { GlobalRate = 10000, RuleRate = 10000, FlushMilliseconds = 30, FlushBytes = int.MaxValue }, _ => { });
    worker.SetDestinations(new[] { "quiet", "busy" });
    try
    {
      worker.TryWrite(new("quiet", new[] { "quiet" }), "", Format);
      var source = new RuleLogSource("busy", new[] { "busy" });
      var watch = Stopwatch.StartNew();
      while (watch.ElapsedMilliseconds < 150) { worker.TryWrite(source, "", Format); Thread.Sleep(1); }
      Check(quiet.Flushes > 0 && busy.Flushes > 0, "busy output must not postpone quiet-file flushes");
    }
    finally { worker.Stop(1000); }
  }

  private static void ReloadAndDestinationBound()
  {
    var retired = new Sink(); var active = new Sink();
    var worker = new BufferedRuleLog(name => name == "retired" ? retired : active, new RuleLogOptions(), _ => { });
    try
    {
      worker.SetDestinations(new[] { "retired" });
      worker.TryWrite(new("before reload", new[] { "retired" }), "", Format);
      worker.SetDestinations(new[] { "active" });
      Until(() => retired.Lines.Count == 1 && retired.Closes == 1, "retired destination drains and closes");
      Check(!worker.TryWrite(new("obsolete", new[] { "retired" }), "", Format), "obsolete source cannot reopen retired file");
      var rejected = worker.SetDestinations(Enumerable.Range(0, 40).Select(i => "log" + i));
      Check(rejected.Length == 11, "32 total names including general and two previously configured names");
    }
    finally { worker.Stop(1000); }
  }

  private static void DestinationGapSummaries()
  {
    var main = new Sink(); var heatmap = new Sink(); var notes = new ConcurrentQueue<string>();
    var worker = new BufferedRuleLog(name => name == "heatmap" ? heatmap : main,
      new RuleLogOptions { GlobalRate = 1, ReportMilliseconds = 20, FlushMilliseconds = 20 }, notes.Enqueue);
    worker.SetDestinations(new[] { "ewp_log", "heatmap" });
    try
    {
      var source = new RuleLogSource("event", new[] { "ewp_log", "heatmap" });
      Check(worker.TryWrite(source, "", Format), "fanout counts as one global admission");
      Check(!worker.TryWrite(source, "", Format), "shared rate drops second logical entry");
      Until(() => main.Lines.Any(l => l.Contains("ewp_log.txt") && l.Contains("rate=1")) &&
        heatmap.Lines.Any(l => l.Contains("heatmap.txt") && l.Contains("rate=1")), "each destination gets attributed gap summary");
      Check(notes.Count == 2, "both summaries reach diagnostics");
    }
    finally { worker.Stop(1000); }
  }

  private static string Temp() { var path = Path.Combine(Path.GetTempPath(), "ewp-log-check-" + Guid.NewGuid()); Directory.CreateDirectory(path); return path; }
  private static void FanoutCapacityAndOpenFailure()
  {
    var files = new[] { "ewp_log" }.Concat(Enumerable.Range(1, 31).Select(i => "log" + i)).ToArray();
    var sinks = files.ToDictionary(name => name, _ => new Sink());
    using var entered = new ManualResetEventSlim(false);
    using var resume = new ManualResetEventSlim(false);
    var worker = new BufferedRuleLog(name => { entered.Set(); resume.Wait(); return sinks[name]; },
      new RuleLogOptions { MaxRecordChars = 100, MaxMemoryBytes = 528 }, _ => { });
    worker.SetDestinations(files);
    int formats = 0;
    Func<string, string, string> format = (_, value) => { formats++; return value; };
    try
    {
      var source = new RuleLogSource("<value>", files);
      Check(worker.TryWrite(source, "x", format), "32-way fanout admission");
      Check(entered.Wait(3000), "worker stalls at first destination");
      for (int i = 0; i < 1000; i++) Check(!worker.TryWrite(source, "x", format), "fanout reference storage is charged to shared capacity");
      Check(formats == 1 && worker.PendingRecords == 1 && worker.PendingBytes <= 528, "bounded fanout rejects before formatting");
      resume.Set();
      Check(worker.Stop(1000), "32-way fanout drain");
      Check(sinks.Values.All(s => s.Lines.First() == "x"), "all 32 copies receive the same record");
    }
    finally { resume.Set(); worker.Stop(1000); }
    var healthy = new Sink(); var notes = new ConcurrentQueue<string>();
    var openFailure = new BufferedRuleLog(name => name == "bad" ? throw new IOException("open failure") : healthy, new RuleLogOptions(), notes.Enqueue);
    openFailure.SetDestinations(new[] { "bad", "good" });
    try
    {
      openFailure.TryWrite(new("record", new[] { "bad", "good" }), "", Format);
      Check(openFailure.Stop(1000) && healthy.Lines.Single() == "record" && notes.Single().Contains("bad.txt"), "an open failure isolates only its destination");
    }
    finally { openFailure.Stop(1000); }
  }
  private static void RollingRetentionAndRestart()
  {
    var folder = Temp(); var path = Path.Combine(folder, "heatmap.txt");
    try
    {
      // Three UTF-8 bytes plus newline makes exactly one entry per segment.
      var bytes = Encoding.UTF8.GetByteCount("éx" + Environment.NewLine);
      using (var writer = new RollingRuleLogWriter(path, bytes, 4))
        foreach (var value in new[] { "é1", "é2", "é3", "é4", "é5" }) writer.WriteLine(value);
      Check(File.ReadAllText(path) == "é5" + Environment.NewLine, "newest active segment");
      Check(File.ReadAllText(Path.Combine(folder, "heatmap.1.txt")) == "é4" + Environment.NewLine, "newest archive");
      Check(File.ReadAllText(Path.Combine(folder, "heatmap.3.txt")) == "é2" + Environment.NewLine, "oldest retained archive");
      Check(Directory.GetFiles(folder).Length == 4 && Directory.GetFiles(folder).All(p => new FileInfo(p).Length <= bytes), "four bounded segments");
      File.WriteAllText(Path.Combine(folder, "unrelated.txt"), "preserve");
      using (var writer = new RollingRuleLogWriter(path, bytes, 4)) writer.WriteLine("é6");
      Check(File.ReadAllText(path) == "é6" + Environment.NewLine && File.ReadAllText(Path.Combine(folder, "heatmap.1.txt")) == "é5" + Environment.NewLine, "restart rotates full active file");
      Check(File.ReadAllText(Path.Combine(folder, "unrelated.txt")) == "preserve", "rotation does not touch unrelated files");
      // Interrupted rotations may leave missing slots; surviving order remains usable.
      File.Delete(Path.Combine(folder, "heatmap.2.txt"));
      using (var writer = new RollingRuleLogWriter(path, bytes, 4)) writer.WriteLine("é7");
      Check(File.ReadAllText(Path.Combine(folder, "heatmap.1.txt")) == "é6" + Environment.NewLine, "archive gaps recover");
      using (var writer = new RollingRuleLogWriter(path, bytes, 2)) { }
      Check(!File.Exists(Path.Combine(folder, "heatmap.2.txt")) && !File.Exists(Path.Combine(folder, "heatmap.3.txt")), "reduced retention removes excess managed slots");
    }
    finally { Directory.Delete(folder, true); }
  }

  private static void MultilineAndSingleSegment()
  {
    var folder = Temp(); var path = Path.Combine(folder, "main.txt");
    var limit = Encoding.UTF8.GetByteCount("replacement" + Environment.NewLine);
    try
    {
      File.WriteAllText(path, "older\n");
      using (var writer = new RollingRuleLogWriter(path, limit, 4)) writer.WriteLine("a\nb\nc\nd");
      Check(File.ReadAllText(path) == "a\nb\nc\nd" + Environment.NewLine, "multiline record is intact in new segment");
      Check(File.ReadAllText(Path.Combine(folder, "main.1.txt")) == "older\n", "earlier content survives rotation");
      using (var writer = new RollingRuleLogWriter(path, limit, 1)) { writer.WriteLine("replacement"); }
      Check(File.ReadAllText(path) == "replacement" + Environment.NewLine && !File.Exists(Path.Combine(folder, "main.1.txt")), "one segment rolling overwrite");
      using (var writer = new RollingRuleLogWriter(path, limit, 1))
      {
        bool thrown = false; try { writer.WriteLine(new string('x', limit + 1)); } catch (IOException) { thrown = true; }
        Check(thrown, "oversized record rejected without splitting");
      }
      Check(File.ReadAllText(path) == "replacement" + Environment.NewLine, "oversized record preserves existing data");
    }
    finally { Directory.Delete(folder, true); }
  }

  private static void RollingFailureIsolation()
  {
    var folder = Temp(); var main = Path.Combine(folder, "ewp_log.txt"); var heatmap = Path.Combine(folder, "heatmap.txt");
    var notes = new ConcurrentQueue<string>();
    var worker = new BufferedRuleLog(name => new RollingRuleLogWriter(Path.Combine(folder, name + ".txt"), 64, 4), new RuleLogOptions(), notes.Enqueue);
    worker.SetDestinations(new[] { "ewp_log", "heatmap" });
    try
    {
      // A directory at the exact oldest archive path injects a rotation error.
      Directory.CreateDirectory(Path.Combine(folder, "ewp_log.3.txt"));
      var source = new RuleLogSource("<value>", new[] { "ewp_log", "heatmap" });
      worker.TryWrite(source, new string('a', 40), Format);
      worker.TryWrite(source, new string('b', 40), Format);
      Check(worker.Stop(1000), "rolling worker shutdown");
      Check(File.ReadAllText(heatmap) == new string('b', 40) + Environment.NewLine, "healthy file continues and rotates after another file fails");
      Check(File.ReadAllText(Path.Combine(folder, "heatmap.1.txt")) == new string('a', 40) + Environment.NewLine, "heatmap archive preserved");
      Check(notes.Any(n => n.Contains("ewp_log.txt")), "rotation failure names destination");
    }
    finally { worker.Stop(1000); Directory.Delete(folder, true); }
  }
}
