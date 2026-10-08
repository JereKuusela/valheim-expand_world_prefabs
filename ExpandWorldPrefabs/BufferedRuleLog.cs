using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ExpandWorld.Prefab;

// Shared by every message/action type originating from one loaded YAML rule.
internal sealed class RuleLogBudget
{
  internal double Tokens;
  internal long LastTick;
  internal bool Started;
}

internal sealed class RuleLogSource(string template, string[]? files = null, RuleLogBudget? budget = null)
{
  internal readonly string Template = template;
  internal readonly bool NeedsFormatting = template.IndexOf('<') >= 0;
  internal readonly string[] Files = files?.Distinct().ToArray() ?? [RuleLogFiles.DefaultName];
  internal readonly RuleLogBudget Budget = budget ?? new();
  internal volatile bool Disabled;
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

// Only the worker invokes file writers and diagnostic callbacks. Producers
// reserve bounded capacity before formatting; they never wait on filesystem I/O.
internal sealed class BufferedRuleLog
{
  internal const int MaximumDestinations = 32;
  private sealed class Destination(string name)
  {
    internal readonly string Name = name;
    internal bool Configured, Failed;
    internal int References, DirtyBytes;
    internal long LastFlush = Stopwatch.GetTimestamp();
    internal TextWriter? Writer;
    internal long RateDrops, CapacityDrops, SizeDrops, FormatDrops;
    internal string? FormatExample;
  }
  private sealed class Record(string message, Destination[] destinations)
  {
    internal readonly string Message = message;
    internal readonly Destination[] Destinations = destinations;
    internal int Charge => Message.Length * 2 + 64 + 8 * (Destinations.Length - 1);
  }

  private readonly object Sync = new();
  private readonly Queue<Record> Queue;
  private readonly Dictionary<string, Destination> Destinations = new(StringComparer.Ordinal);
  private readonly RuleLogOptions Options;
  private readonly Func<string, TextWriter> Open;
  private readonly Action<string> Report;
  private readonly Thread Worker;
  private volatile bool Enabled = true, Stopping;
  private long StopDeadline = long.MaxValue;
  private int RetainedRecords, RetainedBytes;
  private double GlobalTokens;
  private long GlobalTick;

  internal BufferedRuleLog(Func<TextWriter> open, RuleLogOptions options, Action<string> report)
    : this(_ => open(), options, report) { }

  internal BufferedRuleLog(Func<string, TextWriter> open, RuleLogOptions options, Action<string> report)
  {
    options.Validate();
    Open = open; Options = options; Report = report;
    Queue = new Queue<Record>(options.MaxRecords);
    Destinations.Add(RuleLogFiles.DefaultName, new(RuleLogFiles.DefaultName) { Configured = true });
    GlobalTokens = Math.Min(100, options.GlobalRate);
    GlobalTick = Stopwatch.GetTimestamp();
    Worker = new Thread(Run) { IsBackground = true, Name = "EWP rule log" };
    Worker.Start();
  }

  internal bool IsFailed
  {
    get
    {
      lock (Sync)
      {
        bool any = false;
        foreach (var d in Destinations.Values)
        {
          if (!d.Configured) continue;
          any = true;
          if (!d.Failed) return false;
        }
        return any;
      }
    }
  }
  internal int PendingRecords { get { lock (Sync) return RetainedRecords; } }
  internal int PendingBytes { get { lock (Sync) return RetainedBytes; } }

  // Keep failed states until restart, including across YAML reloads. The registry
  // is bounded to 32 distinct names per process, not 32 extra threads/queues.
  internal string[] SetDestinations(IEnumerable<string> names)
  {
    var rejected = new List<string>();
    lock (Sync)
    {
      foreach (var destination in Destinations.Values) destination.Configured = false;
      foreach (var name in names.Distinct())
      {
        if (!Destinations.TryGetValue(name, out var destination))
        {
          if (Destinations.Count >= MaximumDestinations) { rejected.Add(name); continue; }
          Destinations.Add(name, destination = new(name));
        }
        destination.Configured = true;
      }
      Monitor.Pulse(Sync);
    }
    return rejected.ToArray();
  }

  internal void SetEnabled(bool enabled)
  {
    Enabled = enabled;
    lock (Sync) Monitor.Pulse(Sync);
  }

  internal bool TryWrite<T>(RuleLogSource source, T context, Func<string, T, string> format)
  {
    if (!Enabled || Stopping) return false;
    Destination[] destinations;
    int reservation;
    lock (Sync)
    {
      if (!Enabled || Stopping) return false;
      int count = 0;
      foreach (var name in source.Files)
        if (Destinations.TryGetValue(name, out var d) && d.Configured && !d.Failed) count++;
      if (count == 0) return false;
      if (source.Disabled) { DropLocked(source.Files, 3); return false; }
      reservation = Options.MaxRecordChars * 2 + 64 + 8 * (count - 1);
      if (RetainedRecords >= Options.MaxRecords || RetainedBytes > Options.MaxMemoryBytes - reservation)
      { DropLocked(source.Files, 1); return false; }
      long now = Stopwatch.GetTimestamp();
      var budget = source.Budget;
      if (!budget.Started)
      { budget.Started = true; budget.Tokens = Math.Min(25, Options.RuleRate); budget.LastTick = now; }
      Refill(ref GlobalTokens, ref GlobalTick, now, Options.GlobalRate, Math.Min(100, Options.GlobalRate));
      Refill(ref budget.Tokens, ref budget.LastTick, now, Options.RuleRate, Math.Min(25, Options.RuleRate));
      if (GlobalTokens < 1 || budget.Tokens < 1)
      { DropLocked(source.Files, 0); return false; }
      GlobalTokens--; budget.Tokens--;
      RetainedRecords++; RetainedBytes += reservation;
      destinations = new Destination[count];
      int index = 0;
      foreach (var name in source.Files)
        if (Destinations.TryGetValue(name, out var d) && d.Configured && !d.Failed)
        { destinations[index++] = d; d.References++; }
    }

    string? message = null;
    try
    {
      if (source.Template.Length > Options.MaxRecordChars)
      { Drop(destinations, 2); return false; }
      message = source.NeedsFormatting ? format(source.Template, context) : source.Template;
      if (message == null || message.Length > Options.MaxRecordChars)
      { message = null; Drop(destinations, 2); return false; }
    }
    catch (Exception e)
    {
      source.Disabled = true;
      lock (Sync)
      {
        foreach (var d in destinations)
        {
          d.FormatDrops++;
          d.FormatExample ??= source.Template.Substring(0, Math.Min(128, source.Template.Length)) + ": " +
            e.GetType().Name + ". Logging item disabled until YAML reload.";
        }
      }
      return false;
    }
    finally { if (message == null) Release(reservation, destinations); }

    var record = new Record(message, destinations);
    lock (Sync)
    {
      if (!Enabled || Stopping)
      {
        foreach (var d in destinations) d.CapacityDrops++;
        ReleaseLocked(reservation, destinations);
        return false;
      }
      RetainedBytes += record.Charge - reservation;
      Queue.Enqueue(record);
      if (Queue.Count == 1) Monitor.Pulse(Sync);
    }
    return true;
  }

  private void Drop(Destination[] destinations, int kind)
  { lock (Sync) foreach (var d in destinations) { if (kind == 2) d.SizeDrops++; else d.CapacityDrops++; } }
  private void DropLocked(string[] files, int kind)
  {
    foreach (var name in files)
    {
      if (!Destinations.TryGetValue(name, out var d) || !d.Configured || d.Failed) continue;
      if (kind == 0) d.RateDrops++;
      else if (kind == 1) d.CapacityDrops++;
      else d.FormatDrops++;
    }
  }
  private void ReleaseLocked(int bytes, Destination[] destinations)
  {
    RetainedRecords--; RetainedBytes -= bytes;
    foreach (var d in destinations) d.References--;
  }
  private void Release(int bytes, Destination[] destinations)
  { lock (Sync) { ReleaseLocked(bytes, destinations); Monitor.Pulse(Sync); } }
  private static void Refill(ref double tokens, ref long previous, long now, int rate, int burst)
  {
    tokens = Math.Min(burst, tokens + Math.Max(0, now - previous) / (double)Stopwatch.Frequency * rate);
    previous = now;
  }
  private static double Milliseconds(long since) =>
    (Stopwatch.GetTimestamp() - since) * 1000.0 / Stopwatch.Frequency;

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
    long lastReport = Stopwatch.GetTimestamp();
    try
    {
      while (true)
      {
        Record? record = null;
        bool done;
        Destination[] destinations;
        lock (Sync)
        {
          if (Stopping && Stopwatch.GetTimestamp() >= StopDeadline)
            while (Queue.Count > 0)
            {
              var abandoned = Queue.Dequeue();
              foreach (var d in abandoned.Destinations) d.CapacityDrops++;
              ReleaseLocked(abandoned.Charge, abandoned.Destinations);
            }
          if (Queue.Count > 0) record = Queue.Dequeue();
          done = record == null && Stopping;
          destinations = Destinations.Values.ToArray();
        }
        if (record != null)
        {
          try
          {
            foreach (var d in record.Destinations)
            {
              if (DestinationFailed(d)) continue;
              try
              {
                d.Writer ??= Open(d.Name);
                if (d.DirtyBytes == 0) d.LastFlush = Stopwatch.GetTimestamp();
                d.Writer.WriteLine(record.Message);
                d.DirtyBytes += Encoding.UTF8.GetByteCount(record.Message) + Encoding.UTF8.GetByteCount(d.Writer.NewLine);
              }
              catch (Exception e) { Fail(d, e); }
            }
          }
          finally { Release(record.Charge, record.Destinations); }
        }
        foreach (var d in destinations)
        {
          if (DestinationFailed(d)) continue;
          try
          {
            if (d.DirtyBytes > 0 && (done || !Enabled || d.DirtyBytes >= Options.FlushBytes ||
                Milliseconds(d.LastFlush) >= Options.FlushMilliseconds)) Flush(d);
          }
          catch (Exception e) { Fail(d, e); }
        }
        if (done || Milliseconds(lastReport) >= Options.ReportMilliseconds)
        {
          foreach (var d in destinations) Notice(d);
          lastReport = Stopwatch.GetTimestamp();
        }
        foreach (var d in destinations)
        {
          bool close;
          lock (Sync) close = !d.Configured && d.References == 0;
          if ((close || (!Enabled && record == null) || done) && d.Writer != null)
          {
            if (close) Notice(d);
            try { d.Writer.Dispose(); d.Writer = null; d.DirtyBytes = 0; }
            catch (Exception e) { Fail(d, e); }
          }
        }
        if (done) break;
        lock (Sync)
        {
          if (Queue.Count == 0 && !Stopping)
          {
            double wait = Options.FlushMilliseconds;
            foreach (var d in destinations)
              if (d.DirtyBytes > 0) wait = Math.Min(wait, Math.Max(1, Options.FlushMilliseconds - Milliseconds(d.LastFlush)));
            Monitor.Wait(Sync, (int)Math.Min(wait, Options.ReportMilliseconds));
          }
        }
      }
    }
    finally
    {
      Destination[] destinations;
      lock (Sync) destinations = Destinations.Values.ToArray();
      foreach (var d in destinations)
        try { d.Writer?.Dispose(); d.Writer = null; }
        catch (Exception e) { Fail(d, e); }
    }
  }

  private bool DestinationFailed(Destination destination) { lock (Sync) return destination.Failed; }
  private void Flush(Destination d)
  { d.Writer!.Flush(); d.DirtyBytes = 0; d.LastFlush = Stopwatch.GetTimestamp(); }

  private void Fail(Destination d, Exception error)
  {
    lock (Sync) { if (d.Failed) return; d.Failed = true; }
    SafeReport("[EWP LOG ERROR] " + d.Name + ".txt disabled until restart: " + error.GetType().Name +
      ". Accepted records/unflushed output for this destination may be incomplete. Other destinations continue.");
    // Never dispose a sink from a producer thread; the worker owns recovery/close.
    try { d.Writer?.Dispose(); } catch { }
    d.Writer = null; d.DirtyBytes = 0;
  }

  private void Notice(Destination d)
  {
    string? notice;
    lock (Sync)
    {
      if (d.RateDrops + d.CapacityDrops + d.SizeDrops + d.FormatDrops == 0 && d.FormatExample == null) return;
      notice = "[EWP LOG GAP] " + d.Name + ".txt omitted since previous summary: rate=" + d.RateDrops +
        ", capacity/shutdown=" + d.CapacityDrops + ", oversized=" + d.SizeDrops +
        ", formatting/disabled-item=" + d.FormatDrops + ". Summary placement is not the exact gap position." +
        (d.FormatExample == null ? "" : " Example: " + d.FormatExample);
      d.RateDrops = d.CapacityDrops = d.SizeDrops = d.FormatDrops = 0;
      d.FormatExample = null;
    }
    SafeReport(notice);
    if (DestinationFailed(d)) return;
    try { d.Writer ??= Open(d.Name); d.Writer.WriteLine(notice); Flush(d); }
    catch (Exception e) { Fail(d, e); }
  }
  private void SafeReport(string message)
  { try { Report(message); } catch { /* Diagnostics cannot restart a failed destination. */ } }
}
