using System;
using System.Collections.Generic;

namespace ExpandWorld.Prefab;

// Waiting operations are checked every frame. This can mean lots of checking for longer delays.
// Multiple tiers are used to reduce this, which also keeps the list sizes smaller.
internal sealed class DelayedQueue<T>
{
  // Tests indicated that instant + 0.5 + 4 tier spacing provided the lowest overall time.
  private const double FarInterval = 4;
  private const double MediumInterval = 0.5;

  private readonly struct Entry(T value, double due, long sequence)
  {
    internal readonly T Value = value;
    internal readonly double Due = due;
    internal readonly long Sequence = sequence;
  }

  private sealed class Tier
  {
    internal readonly List<Entry> Entries = [];
    // A cached minimum is used for early skipping.
    internal double Earliest = double.PositiveInfinity;

    internal void Add(Entry entry)
    {
      Entries.Add(entry);
      if (entry.Due < Earliest) Earliest = entry.Due;
    }

    internal void MoveDue(double cutoff, Tier destination)
    {
      if (Earliest > cutoff) return;
      // Each RemoveAt would move every element after it. This can be expensive for long lists.
      // This can be done just once per scan by compacting the array manually.
      var pending = 0;
      Earliest = double.PositiveInfinity;
      for (var index = 0; index < Entries.Count; index++)
      {
        var entry = Entries[index];
        if (entry.Due <= cutoff)
          destination.Add(entry);
        else
        {
          if (pending != index) Entries[pending] = entry;
          pending++;
          if (entry.Due < Earliest) Earliest = entry.Due;
        }
      }
      Entries.RemoveRange(pending, Entries.Count - pending);
    }

    internal void Clear()
    {
      Entries.Clear();
      Earliest = double.PositiveInfinity;
    }

    internal void RemoveWhere(Predicate<T> match, int start = 0)
    {
      var pending = start;
      Earliest = double.PositiveInfinity;
      for (var index = start; index < Entries.Count; index++)
      {
        var entry = Entries[index];
        if (match(entry.Value)) continue;
        if (pending != index) Entries[pending] = entry;
        pending++;
        if (entry.Due < Earliest) Earliest = entry.Due;
      }
      Entries.RemoveRange(pending, Entries.Count - pending);
    }
  }

  private readonly Tier Near = new();
  private readonly Tier Medium = new();
  private readonly Tier Far = new();
  private readonly Tier Ready = new();
  private double NextFar = double.NegativeInfinity;
  private double NextMedium = double.NegativeInfinity;
  private long Sequence;
  private int ReadyIndex;
  private bool Draining;

  internal void Add(double due, T value) => Add(new Entry(value, due, Sequence++));

  internal void RemoveWhere(Predicate<T> match)
  {
    Near.RemoveWhere(match);
    Medium.RemoveWhere(match);
    Far.RemoveWhere(match);
    // The processed prefix includes the active callback; only the unstarted work can be canceled here.
    Ready.RemoveWhere(match, ReadyIndex);
  }

  private void Add(Entry entry)
  {
    // Bypass any tier whose next promotion would occur at or after this deadline.
    if (entry.Due <= NextMedium)
      Near.Add(entry);
    else if (entry.Due <= NextFar)
      Medium.Add(entry);
    else
      Far.Add(entry);
  }

  internal void Clear()
  {
    Near.Clear();
    Medium.Clear();
    Far.Clear();
    Ready.Clear();
    ReadyIndex = 0;
    NextFar = NextMedium = double.NegativeInfinity;
  }

  internal void Drain(double now, Func<T, double?> execute, bool consumeOnFailure = true)
  {
    if (Draining) return;
    Draining = true;
    try
    {
      // Promote from far to near so overdue work can cross multiple tiers in one update, even after a skipped update.
      if (now >= NextFar || Far.Earliest <= now)
      {
        NextFar = now + FarInterval;
        Far.MoveDue(NextFar, Medium);
      }
      if (now >= NextMedium || Medium.Earliest <= now)
      {
        NextMedium = now + MediumInterval;
        Medium.MoveDue(NextMedium, Near);
      }
      do
      {
        // Stage ready work before invoking callbacks, which can add or cancel entries in the same queue.
        Near.MoveDue(now, Ready);
        // Promotion can mix enqueue order. Sort only when the ready batch is actually out of order.
        for (var index = 1; index < Ready.Entries.Count; index++)
        {
          if (Ready.Entries[index - 1].Sequence < Ready.Entries[index].Sequence) continue;
          Ready.Entries.Sort(static (first, second) => first.Sequence.CompareTo(second.Sequence));
          break;
        }
        while (ReadyIndex < Ready.Entries.Count)
        {
          var entry = Ready.Entries[ReadyIndex++];
          double? retry;
          try { retry = execute(entry.Value); }
          catch
          {
            if (!consumeOnFailure && ReadyIndex > 0) ReadyIndex--;
            throw;
          }
          if (retry.HasValue && ReadyIndex > 0)
            Add(new Entry(entry.Value, retry.Value, entry.Sequence));
        }
        Ready.Clear();
        ReadyIndex = 0;
        // Pick up newly due work after the current ready batch finishes.
      } while (Near.Earliest <= now);
    }
    finally
    {
      // If a callback throws, discard only the consumed prefix and leave the rest for the next drain.
      Ready.Entries.RemoveRange(0, ReadyIndex);
      ReadyIndex = 0;
      Draining = false;
    }
  }
}