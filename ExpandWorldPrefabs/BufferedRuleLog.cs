using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ExpandWorld.Prefab;

// One instance per loaded rule, not per player, message or event. Recreated on YAML reload.
internal sealed class RuleLogSource(string template)
{
  internal readonly string Template = template;
  internal readonly bool NeedsFormatting = template.IndexOf('<') >= 0;
  internal volatile bool Disabled;
  internal double Tokens;
  internal long LastTick;
  internal bool Started;
}

internal sealed class RuleLogOptions
{
  internal int GlobalRate = 1000;
  internal int RuleRate = 250;
  internal int MaxRecords = 4096;
  internal int MaxMemoryBytes = 4 * 1024 * 1024;
  internal int MaxRecordChars = 8192;
  internal int FlushMilliseconds = 1000;
  internal int FlushBytes = 65536;
  internal int ReportMilliseconds = 30000;

  internal void Validate()
  {
    if (GlobalRate < 1 || RuleRate < 1 || MaxRecords < 1 || MaxRecordChars < 1 ||
        MaxRecordChars > 65536 || MaxMemoryBytes < MaxRecordChars * 2 + 64 ||
        FlushMilliseconds < 1 || FlushBytes < 1 || ReportMilliseconds < 1)
      throw new ArgumentOutOfRangeException(nameof(RuleLogOptions));
  }
}

// Pure BCL transport. Only the worker invokes the sink and diagnostics callbacks.
// Producer locks protect small queue/accounting operations, never file I/O or formatting.
internal sealed class BufferedRuleLog
{
  private readonly object Sync = new();
  private readonly Queue<string> Queue;
  private readonly RuleLogOptions Options;
  private readonly Func<TextWriter> Open;
  private readonly Action<string> Report;
  private readonly Thread Worker;
  private volatile bool Enabled = true;
  private volatile bool Stopping;
  private volatile bool Failed;
  private long StopDeadline = long.MaxValue;
  private int RetainedRecords;
  private int RetainedBytes;
  private double GlobalTokens;
  private long GlobalTick;
  private long RateDrops, CapacityDrops, SizeDrops, FormatDrops;
  private string? FormatExample;

  internal BufferedRuleLog(Func<TextWriter> open, RuleLogOptions options, Action<string> report)
  {
    options.Validate();
    Open = open;
    Options = options;
    Report = report;
    Queue = new Queue<string>(options.MaxRecords);
    GlobalTokens = Math.Min(100, options.GlobalRate);
    GlobalTick = Stopwatch.GetTimestamp();
    Worker = new Thread(Run) { IsBackground = true, Name = "EWP rule log" };
    Worker.Start();
  }

  internal bool IsFailed => Failed;
  internal int PendingRecords { get { lock (Sync) return RetainedRecords; } }
  internal int PendingBytes { get { lock (Sync) return RetainedBytes; } }

  internal void SetEnabled(bool enabled)
  {
    Enabled = enabled;
    lock (Sync) Monitor.Pulse(Sync);
  }

  internal bool TryWrite<T>(RuleLogSource source, T context, Func<string, T, string> format)
  {
    if (!Enabled || Stopping || Failed) return false;
    if (source.Disabled) { Interlocked.Increment(ref FormatDrops); return false; }
    // Short bookkeeping lock only. Never wait for queue capacity or filesystem I/O.
    int reservation = Options.MaxRecordChars * 2 + 64;
    lock (Sync)
    {
      if (!Enabled || Stopping || Failed) return false;
      if (RetainedRecords >= Options.MaxRecords || RetainedBytes > Options.MaxMemoryBytes - reservation)
      { Interlocked.Increment(ref CapacityDrops); return false; }
      long now = Stopwatch.GetTimestamp();
      if (!source.Started)
      { source.Started = true; source.Tokens = Math.Min(25, Options.RuleRate); source.LastTick = now; }
      Refill(ref GlobalTokens, ref GlobalTick, now, Options.GlobalRate, Math.Min(100, Options.GlobalRate));
      Refill(ref source.Tokens, ref source.LastTick, now, Options.RuleRate, Math.Min(25, Options.RuleRate));
      if (GlobalTokens < 1 || source.Tokens < 1)
      { Interlocked.Increment(ref RateDrops); return false; }
      GlobalTokens--; source.Tokens--;
      RetainedRecords++; RetainedBytes += reservation;
    }

    string? message = null;
    try
    {
      if (source.Template.Length > Options.MaxRecordChars)
      { Interlocked.Increment(ref SizeDrops); return false; }
      message = source.NeedsFormatting ? format(source.Template, context) : source.Template;
      if (message == null || message.Length > Options.MaxRecordChars)
      { message = null; Interlocked.Increment(ref SizeDrops); return false; }
    }
    catch (Exception e)
    {
      source.Disabled = true;
      Interlocked.Increment(ref FormatDrops);
      // One bounded sample, not a growing set of failed templates or resolved player text.
      Interlocked.CompareExchange(ref FormatExample,
        source.Template.Substring(0, Math.Min(128, source.Template.Length)) + ": " +
        e.GetType().Name + ". Rule disabled until YAML reload.", null);
      return false;
    }
    finally
    {
      if (message == null) Release(reservation);
    }

    lock (Sync)
    {
      if (!Enabled || Stopping || Failed)
      { RetainedRecords--; RetainedBytes -= reservation; Interlocked.Increment(ref CapacityDrops); return false; }
      RetainedBytes += Charge(message) - reservation;
      Queue.Enqueue(message);
      if (Queue.Count == 1) Monitor.Pulse(Sync);
    }
    return true;
  }

  private static int Charge(string message) => message.Length * 2 + 64;
  private void Release(int bytes)
  {
    lock (Sync) { RetainedRecords--; RetainedBytes -= bytes; }
  }
  private static void Refill(ref double tokens, ref long previous, long now, int rate, int burst)
  {
    tokens = Math.Min(burst, tokens + Math.Max(0, now - previous) / (double)Stopwatch.Frequency * rate);
    previous = now;
  }
  private static double Milliseconds(long since) =>
    (Stopwatch.GetTimestamp() - since) * 1000.0 / Stopwatch.Frequency;

  // Only shutdown waits, and never longer than the caller's budget. Do not close or
  // replace a sink from another thread if the OS has stalled the worker inside a write.
  internal bool Stop(int milliseconds)
  {
    lock (Sync)
    {
      if (!Stopping)
      {
        StopDeadline = Stopwatch.GetTimestamp() + (long)(Math.Max(0, milliseconds) / 1000.0 * Stopwatch.Frequency);
        Stopping = true;
      }
      Monitor.Pulse(Sync);
    }
    return Worker.Join(Math.Max(0, milliseconds));
  }

  private void Run()
  {
    TextWriter? writer = null;
    int dirtyBytes = 0;
    long lastFlush = Stopwatch.GetTimestamp(), lastReport = lastFlush;
    try
    {
      while (true)
      {
        string? message = null;
        bool done = false;
        lock (Sync)
        {
          if (Stopping && Stopwatch.GetTimestamp() >= StopDeadline)
          {
            while (Queue.Count > 0)
            {
              var abandoned = Queue.Dequeue();
              RetainedRecords--; RetainedBytes -= Charge(abandoned);
              Interlocked.Increment(ref CapacityDrops);
            }
          }
          if (Queue.Count > 0) message = Queue.Dequeue();
          else if (Stopping) done = true;
        }
        if (message != null)
        {
          try
          {
            writer ??= Open();
            writer.WriteLine(message);
            dirtyBytes += Encoding.UTF8.GetByteCount(message) + Encoding.UTF8.GetByteCount(writer.NewLine);
          }
          finally { Release(Charge(message)); }
        }
        // Deadline checked during busy traffic as well as idle periods.
        if (dirtyBytes > 0 && (done || !Enabled || dirtyBytes >= Options.FlushBytes ||
            Milliseconds(lastFlush) >= Options.FlushMilliseconds))
        { writer!.Flush(); dirtyBytes = 0; lastFlush = Stopwatch.GetTimestamp(); }

        if (done || Milliseconds(lastReport) >= Options.ReportMilliseconds)
        {
          var notice = TakeNotice();
          if (notice != null)
          {
            SafeReport(notice);
            writer ??= Open();
            writer.WriteLine(notice);
            writer.Flush(); dirtyBytes = 0; lastFlush = Stopwatch.GetTimestamp();
          }
          lastReport = Stopwatch.GetTimestamp();
        }
        if (done) break;
        if (!Enabled && message == null && writer != null)
        { writer.Dispose(); writer = null; }

        lock (Sync)
        {
          if (Queue.Count == 0 && !Stopping)
          {
            var wait = dirtyBytes > 0 ? Math.Max(1, Options.FlushMilliseconds - Milliseconds(lastFlush)) : Options.FlushMilliseconds;
            Monitor.Wait(Sync, (int)Math.Min(wait, Options.ReportMilliseconds));
          }
        }
      }
    }
    catch (Exception e)
    {
      Failed = true;
      int pending;
      lock (Sync)
      {
        pending = Queue.Count;
        while (Queue.Count > 0)
        { var dropped = Queue.Dequeue(); RetainedRecords--; RetainedBytes -= Charge(dropped); }
      }
      SafeReport("[EWP LOG ERROR] Logging disabled until restart; file output failed: " + e.GetType().Name +
        ". Pending records discarded=" + pending + ". The current record/unflushed tail may be incomplete. " +
        (TakeNotice() ?? ""));
    }
    finally
    {
      try { writer?.Dispose(); }
      catch (Exception e)
      {
        if (!Failed) SafeReport("[EWP LOG ERROR] Close failed: " + e.GetType().Name + ". Tail may be incomplete.");
        Failed = true;
      }
    }
  }

  private string? TakeNotice()
  {
    long rate = Interlocked.Exchange(ref RateDrops, 0), capacity = Interlocked.Exchange(ref CapacityDrops, 0),
      size = Interlocked.Exchange(ref SizeDrops, 0), format = Interlocked.Exchange(ref FormatDrops, 0);
    var sample = Interlocked.Exchange(ref FormatExample, null);
    if (rate + capacity + size + format == 0 && sample == null) return null;
    return "[EWP LOG GAP] Omitted since previous summary: rate=" + rate + ", capacity/shutdown=" + capacity +
      ", oversized=" + size + ", formatting/disabled-rule=" + format +
      ". Summary placement is not the exact gap position." + (sample == null ? "" : " Example: " + sample);
  }
  private void SafeReport(string message)
  {
    try { Report(message); } catch { /* Diagnostics must not restart a failing writer. */ }
  }
}
