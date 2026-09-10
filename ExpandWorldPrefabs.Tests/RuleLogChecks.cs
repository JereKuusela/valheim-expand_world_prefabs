using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExpandWorld.Prefab;

namespace ExpandWorldPrefabs.Tests;

// Dependency-free transport checks used by NUnit.
// Real transport source; fake sinks inject stalls/failures without requiring Unity.
internal static class RuleLogChecks
{
  private static readonly Func<string, string, string> Echo = (_, value) => value;
  private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
  private static void Until(Func<bool> condition, string label)
  { Check(SpinWait.SpinUntil(condition, 3000), "Timeout: " + label); }

  internal static void RunAll()
  {
    Action[] tests = { AppendAndOwnership, BoundedStall, MemoryAndSizeLimits,
      RateGatesAndBrokenTemplate, ToggleAndOpenFailure, WriteFlushCloseFailures,
      PeriodicFlushUnderTraffic, ShutdownDuringStall, ConcurrentProducers, Cadences };
    foreach (var test in tests)
    {
      var watch = Stopwatch.StartNew();
      test();
      System.Console.WriteLine("PASS " + test.Method.Name + " (" + watch.ElapsedMilliseconds + " ms)");
    }
  }

  private sealed class Sink : TextWriter
  {
    internal readonly ConcurrentQueue<string> Lines = new();
    internal readonly ManualResetEventSlim Entered = new(false), Resume = new(true);
    internal int Flushes, Closes, Owner;
    internal bool ThrowWrite, ThrowFlush, ThrowClose, WrongThread;
    public override Encoding Encoding => Encoding.UTF8;
    private void ThreadCheck()
    {
      int id = Thread.CurrentThread.ManagedThreadId;
      Interlocked.CompareExchange(ref Owner, id, 0);
      if (Owner != id) WrongThread = true;
    }
    public override void WriteLine(string? text)
    {
      ThreadCheck(); Entered.Set(); Resume.Wait();
      if (ThrowWrite) throw new IOException("injected write error");
      Lines.Enqueue(text ?? "");
    }
    public override void Flush()
    {
      ThreadCheck();
      if (ThrowFlush) throw new IOException("injected flush error");
      Interlocked.Increment(ref Flushes);
    }
    protected override void Dispose(bool disposing)
    {
      ThreadCheck(); Interlocked.Increment(ref Closes);
      if (ThrowClose) throw new IOException("injected close error");
    }
  }

  private static void AppendAndOwnership()
  {
    string dir = Path.Combine(Path.GetTempPath(), "ewp-async-log-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    string path = Path.Combine(dir, "ewp_log.txt");
    var sink = new Sink();
    var worker = new BufferedRuleLog(() => sink, new RuleLogOptions(), _ => { });
    try
    {
      Check(worker.TryWrite(new RuleLogSource("literal"), "", Echo), "literal admission");
      Check(worker.Stop(1000), "sink shutdown");
      Check(!sink.WrongThread && sink.Owner != Thread.CurrentThread.ManagedThreadId && sink.Closes == 1,
        "sink I/O must have exactly one background owner");
      File.WriteAllText(path, "existing\n", new UTF8Encoding(false));
      for (int session = 0; session < 2; session++)
      {
        var fileWorker = new BufferedRuleLog(() => new StreamWriter(
          new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)),
          new RuleLogOptions { FlushMilliseconds = 40 }, _ => { });
        try
        {
          var source = new RuleLogSource("<text>");
          for (int i = 0; i < 10; i++)
            Check(fileWorker.TryWrite(source, "record■" + session + "-" + i, Echo), "ordered admission");
          Until(() => File.ReadAllText(path).Contains("record■" + session + "-9"), "live read after timed flush");
        }
        finally { Check(fileWorker.Stop(1000), "file worker stop"); }
      }
      var lines = File.ReadAllLines(path);
      Check(lines.Length == 21 && lines[0] == "existing", "append must preserve old bytes");
      for (int i = 0; i < 20; i++) Check(lines[i + 1] == "record■" + i / 10 + "-" + i % 10, "FIFO text");
      Check(File.ReadAllBytes(path)[0] != 0xEF, "no new BOM");
    }
    finally { worker.Stop(1000); Directory.Delete(dir, true); }
  }

  private static void BoundedStall()
  {
    var sink = new Sink(); sink.Resume.Reset();
    var notes = new ConcurrentQueue<string>();
    var worker = new BufferedRuleLog(() => sink, new RuleLogOptions { MaxRecords = 4 }, notes.Enqueue);
    var source = new RuleLogSource("<value>");
    int formats = 0;
    Func<string, int, string> format = (_, n) => { formats++; return "item-" + n; };
    try
    {
      Check(worker.TryWrite(source, 0, format), "first admission");
      Check(sink.Entered.Wait(2000), "worker should be stalled inside write");
      for (int i = 1; i < 4; i++) Check(worker.TryWrite(source, i, format), "queue fill");
      var allocationMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");
      var allocated = allocationMethod == null ? null :
        (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
      var watch = Stopwatch.StartNew();
      long before = allocated?.Invoke() ?? 0;
      for (int i = 0; i < 100000; i++) Check(!worker.TryWrite(source, i, format), "full queue rejection");
      long bytes = (allocated?.Invoke() ?? 0) - before;
      watch.Stop();
      Check(formats == 4 && worker.PendingRecords == 4, "rejection before formatter, including in-flight record");
      Check(worker.PendingBytes <= 4 * 1024 * 1024, "bounded memory");
      System.Console.WriteLine("  stalled sink: 100000 rejected calls in " + watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms; formatter calls=" + formats);
      if (allocated != null)
      {
        System.Console.WriteLine("  rejected-path producer allocations=" + bytes + " bytes");
        Check(bytes == 0, "overload rejection should not allocate per record");
      }
      Check(watch.ElapsedMilliseconds < 1500, "producer waited for disk");
    }
    finally { sink.Resume.Set(); Check(worker.Stop(2000), "stall cleanup"); }
    Check(sink.Lines.Any(x => x.Contains("[EWP LOG GAP]") && x.Contains("100000")), "explicit gap count");
    Check(notes.Count == 1, "one aggregate warning, not per rejected call");
  }

  private static void MemoryAndSizeLimits()
  {
    var sink = new Sink(); sink.Resume.Reset();
    var options = new RuleLogOptions { MaxRecordChars = 100, MaxMemoryBytes = 528, MaxRecords = 10 };
    var worker = new BufferedRuleLog(() => sink, options, _ => { });
    var source = new RuleLogSource("<value>");
    try
    {
      Check(!worker.TryWrite(source, new string('x', 101), Echo), "reject oversized result, never truncate");
      Check(worker.PendingRecords == 0 && worker.PendingBytes == 0, "release oversize reservation");
      Check(worker.TryWrite(source, new string('x', 100), Echo), "first maximum record");
      Check(sink.Entered.Wait(2000), "memory stall");
      Check(worker.TryWrite(source, new string('y', 100), Echo), "second maximum record");
      Check(!worker.TryWrite(source, "z", Echo), "conservative memory reservation");
      Check(worker.PendingBytes == 528 && worker.PendingRecords == 2, "queue and in-flight share byte budget");
    }
    finally { sink.Resume.Set(); worker.Stop(2000); }
  }

  private static void RateGatesAndBrokenTemplate()
  {
    var sink = new Sink();
    var worker = new BufferedRuleLog(() => sink, new RuleLogOptions { RuleRate = 1 }, _ => { });
    var source = new RuleLogSource("<value>");
    int calls = 0;
    Func<string, int, string> format = (_, n) => { calls++; return n.ToString(); };
    try
    {
      Check(worker.TryWrite(source, 1, format), "initial rule token");
      for (int i = 0; i < 1000; i++) Check(!worker.TryWrite(source, i, format), "rate gate");
      Check(calls == 1, "rate gate before formatting");
      Check(worker.TryWrite(new RuleLogSource("<other>"), 2, format), "different rule has separate budget");
      var broken = new RuleLogSource("<broken>");
      int throws = 0;
      Func<string, int, string> bad = (_, _) => { throws++; throw new Exception("bad"); };
      for (int i = 0; i < 1000; i++) Check(!worker.TryWrite(broken, i, bad), "broken source");
      Check(throws == 1 && broken.Disabled, "broken formatter only runs once");
      Check(worker.TryWrite(new RuleLogSource("<broken>"), 3, format), "YAML-reload-equivalent source recovers");
    }
    finally { worker.Stop(1000); }
    var globalWorker = new BufferedRuleLog(() => new Sink(), new RuleLogOptions { GlobalRate = 1 }, _ => { });
    try
    {
      Check(globalWorker.TryWrite(new RuleLogSource("one"), "", Echo), "global first");
      Check(!globalWorker.TryWrite(new RuleLogSource("two"), "", Echo), "global gate across sources");
    }
    finally { globalWorker.Stop(1000); }
  }

  private static void ToggleAndOpenFailure()
  {
    int opens = 0;
    var sink = new Sink();
    var worker = new BufferedRuleLog(() => { Interlocked.Increment(ref opens); return sink; },
      new RuleLogOptions { FlushMilliseconds = 30 }, _ => { });
    try
    {
      Thread.Sleep(50); Check(opens == 0, "lazy file open");
      Check(worker.TryWrite(new RuleLogSource("first"), "", Echo), "enabled write");
      worker.SetEnabled(false);
      Until(() => sink.Closes == 1, "disable drains and closes in background");
      Check(!worker.TryWrite(new RuleLogSource("<no>"), 0, (_, _) => throw new Exception()), "disabled skips formatting");
      worker.SetEnabled(true);
      Check(worker.TryWrite(new RuleLogSource("second"), "", Echo), "reenable");
      Until(() => sink.Lines.Count == 2, "append after reenable");
      Check(opens == 2, "one lazy reopen");
    }
    finally { worker.Stop(1000); }
    int attempts = 0;
    var failed = new BufferedRuleLog(() => { attempts++; throw new IOException("open error"); },
      new RuleLogOptions(), _ => { });
    try
    {
      failed.TryWrite(new RuleLogSource("trigger"), "", Echo);
      Until(() => failed.IsFailed, "open failure latch");
      failed.SetEnabled(false); failed.SetEnabled(true);
      for (int i = 0; i < 1000; i++)
        Check(!failed.TryWrite(new RuleLogSource("<no>"), 0, (_, _) => throw new Exception()), "failed skips formatting");
      Check(attempts == 1, "no reopen storm");
    }
    finally { failed.Stop(1000); }
  }

  private static void WriteFlushCloseFailures()
  {
    for (int mode = 0; mode < 3; mode++)
    {
      var sink = new Sink { ThrowWrite = mode == 0, ThrowFlush = mode == 1, ThrowClose = mode == 2 };
      var notes = new ConcurrentQueue<string>();
      var worker = new BufferedRuleLog(() => sink, new RuleLogOptions { FlushMilliseconds = 20 }, notes.Enqueue);
      worker.TryWrite(new RuleLogSource("trigger"), "", Echo);
      if (mode < 2) Until(() => worker.IsFailed, "write/flush failure latch");
      Check(worker.Stop(1000), "failure shutdown");
      Check(worker.IsFailed && notes.Count == 1, "one failure diagnostic including dispose error");
      Check(worker.PendingRecords == 0, "failure releases queue");
    }
  }

  private static void PeriodicFlushUnderTraffic()
  {
    var sink = new Sink();
    var worker = new BufferedRuleLog(() => sink,
      new RuleLogOptions { GlobalRate = 10000, RuleRate = 10000, FlushMilliseconds = 50, FlushBytes = int.MaxValue }, _ => { });
    var source = new RuleLogSource("short");
    try
    {
      var clock = Stopwatch.StartNew();
      while (clock.ElapsedMilliseconds < 350)
      { worker.TryWrite(source, "", Echo); Thread.Sleep(1); }
      Check(sink.Flushes >= 3, "continuous traffic must not postpone deadline forever");
    }
    finally { worker.Stop(1000); }
    var thresholdSink = new Sink();
    var thresholdWorker = new BufferedRuleLog(() => thresholdSink,
      new RuleLogOptions { FlushMilliseconds = 10000, FlushBytes = 10 }, _ => { });
    try
    {
      thresholdWorker.TryWrite(new RuleLogSource("1234567890"), "", Echo);
      Until(() => thresholdSink.Flushes >= 1, "byte threshold");
    }
    finally { thresholdWorker.Stop(1000); }
  }

  private static void ShutdownDuringStall()
  {
    var sink = new Sink(); sink.Resume.Reset();
    var worker = new BufferedRuleLog(() => sink, new RuleLogOptions(), _ => { });
    try
    {
      worker.TryWrite(new RuleLogSource("blocked"), "", Echo);
      Check(sink.Entered.Wait(2000), "shutdown stall");
      worker.TryWrite(new RuleLogSource("abandon-after-deadline"), "", Echo);
      var clock = Stopwatch.StartNew();
      Check(!worker.Stop(30), "stalled shutdown should time out");
      Check(clock.ElapsedMilliseconds < 500 && sink.Closes == 0, "no forced close on producer thread");
      Check(!worker.TryWrite(new RuleLogSource("late"), "", Echo), "no writes after stop");
    }
    finally { sink.Resume.Set(); Check(worker.Stop(2000), "worker exits after storage resumes"); }
    Check(!sink.Lines.Contains("abandon-after-deadline") && sink.Lines.Any(x => x.StartsWith("[EWP LOG GAP]")),
      "deadline abandons queue visibly");
    Check(!sink.WrongThread, "one owner during timeout");
  }

  private static void ConcurrentProducers()
  {
    var sink = new Sink();
    var accepted = new ConcurrentBag<string>();
    var worker = new BufferedRuleLog(() => sink, new RuleLogOptions(), _ => { });
    try
    {
      Parallel.For(0, 8, p =>
      {
        var source = new RuleLogSource("<value>");
        for (int i = 0; i < 100; i++)
        {
          var text = p + ":" + i;
          if (worker.TryWrite(source, text, Echo)) accepted.Add(text);
        }
      });
    }
    finally { Check(worker.Stop(2000), "concurrent shutdown"); }
    var records = sink.Lines.Where(x => !x.StartsWith("[EWP LOG GAP]")).ToArray();
    Check(records.Length == accepted.Count && records.OrderBy(x => x).SequenceEqual(accepted.OrderBy(x => x)),
      "every accepted record exactly once");
    Check(!sink.WrongThread && worker.PendingRecords == 0 && worker.PendingBytes == 0, "concurrent accounting");
  }

  private static void Cadences()
  {
    // Real wall-clock pacing; not a Valheim frame-time benchmark.
    foreach (int interval in new[] { 2000, 16, 2 })
    {
      var sink = new Sink();
      var sources = Enumerable.Range(0, 4).Select(_ => new RuleLogSource("<value>")).ToArray();
      var worker = new BufferedRuleLog(() => sink, new RuleLogOptions(), _ => { });
      int count = interval == 2000 ? 3 : interval == 16 ? 60 : 500;
      int accepted = 0;
      long maxTicks = 0, totalTicks = 0;
      try
      {
        for (int i = 0; i < count; i++)
        {
          long start = Stopwatch.GetTimestamp();
          if (worker.TryWrite(sources[i % 4], "sample■" + i, Echo)) accepted++;
          long ticks = Stopwatch.GetTimestamp() - start;
          totalTicks += ticks; maxTicks = Math.Max(ticks, maxTicks);
          if (i + 1 < count) Thread.Sleep(interval);
        }
      }
      finally { worker.Stop(2000); }
      Check(accepted == count, "no loss at supported cadence");
      System.Console.WriteLine("  interval=" + interval + "ms accepted=" + accepted +
        " mean-producer-us=" + (totalTicks * 1000000.0 / Stopwatch.Frequency / count).ToString("F2") +
        " max-producer-us=" + (maxTicks * 1000000.0 / Stopwatch.Frequency).ToString("F2"));
    }
  }
}
