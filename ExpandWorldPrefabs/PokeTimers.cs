using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Service;

namespace ExpandWorld.Prefab;

// Timer data lives on the target ZDO.
// Never persist ZDOIDs: Valheim assigns new IDs while loading that save.
public static class PokeTimers
{
  private const int Format = 2;
  private static readonly int StorageHash = ZdoHelper.Hash("ewp_pending_pokes");
  private sealed class Entry(ZDOID id, double due, string[] args)
  {
    internal readonly ZDOID Id = id;
    internal readonly double Due = due;
    internal readonly string[] Args = args;
  }
  private static readonly List<Entry> Pending = [];
  private static readonly Dictionary<ZDOID, List<Entry>> ByTarget = [];
  private static readonly HashSet<ZDOID> Dirty = [];

  public static void Initialize()
  {
    ServerSideData.Loaded += Restore;
    ServerSideData.Destroyed += Remove;
  }

  public static void Clear()
  {
    Pending.Clear();
    ByTarget.Clear();
    Dirty.Clear();
  }

  private static void Insert(Entry entry)
  {
    Pending.Add(entry);
    Dirty.Add(entry.Id);
    if (!ByTarget.TryGetValue(entry.Id, out var entries))
      ByTarget[entry.Id] = entries = [];
    entries.Add(entry);
  }

  public static void Add(float delay, ZDOID[] targets, string[] args)
  {
    if (float.IsNaN(delay) || float.IsInfinity(delay))
    {
      Log.Warning("Skipped delayed poke with a non-finite delay.");
      return;
    }
    for (var i = 0; i < targets.Length; i++)
    {
      try
      {
        var target = ZDOMan.instance.GetZDO(targets[i]);
        if (target == null) continue;
        Insert(new Entry(target.m_uid, ZNet.instance.m_netTime + delay, args));
      }
      catch (ArgumentOutOfRangeException)
      {
        Log.Warning("Skipped delayed poke with an invalid target ID.");
      }
    }
  }

  private static void Remove(Entry entry)
  {
    var entries = ByTarget[entry.Id];
    entries.Remove(entry);
    if (entries.Count == 0) ByTarget.Remove(entry.Id);
    Dirty.Add(entry.Id);
  }

  private static void Remove(ZDOID id)
  {
    if (ByTarget.TryGetValue(id, out var entries))
    {
      foreach (var entry in entries)
        Pending.Remove(entry);
    }
    ByTarget.Remove(id);
    Dirty.Remove(id);
  }

  public static void Execute()
  {
    try
    {
      for (var i = 0; i < Pending.Count; i++)
      {
        var entry = Pending[i];
        if (entry.Due > ZNet.instance.m_netTime) continue;
        Pending.RemoveAt(i);
        i--;
        // Consume before dispatch: a throwing recipient must not replay earlier recipients.
        Remove(entry);
        if (ZDOMan.instance.GetZDO(entry.Id) != null)
          Manager.Handle(ActionType.Poke, entry.Args, entry.Id);
      }
    }
    finally { Flush(); }
  }

  // Batch mutations per target; ServerSideData writes the combined payload during ZDO.Save.
  public static void Flush()
  {
    foreach (var id in Dirty)
    {
      var target = ZDOMan.instance.GetZDO(id);
      if (target != null && target.m_uid == id && target.Persistent)
      {
        ByTarget.TryGetValue(id, out var entries);
        if (Config.ServerSideData)
          ServerSideData.SetBytes(target, StorageHash, Encode(entries));
      }
    }
    Dirty.Clear();
  }

  private static byte[] Encode(List<Entry>? entries)
  {
    if (entries == null || entries.Count == 0) return [];
    ZPackage pkg = new();
    pkg.Write(Format); pkg.Write(entries.Count);
    foreach (var entry in entries)
    {
      pkg.Write(entry.Due); pkg.Write(entry.Args.Length);
      foreach (var arg in entry.Args)
        pkg.Write(arg);
    }
    return pkg.GetArray();
  }

  public static void Restore(ZDO target)
  {
    if (!ServerSideData.TryGetBytes(target.m_uid, StorageHash, out var bytes) || bytes.Length == 0) return;
    try
    {
      ZPackage pkg = new(bytes);
      var format = pkg.ReadInt();
      if (format != Format)
      {
        if (format == 1)
          Log.Warning("Saved poke timers use obsolete format 1 and were discarded.");
        else
          Log.Warning($"Saved poke timers use unsupported format {format} and were discarded.");
        return;
      }
      var count = pkg.ReadInt();
      if (count < 0 || count > bytes.Length / 20) throw new InvalidOperationException("Invalid poke timer count.");
      List<Entry> loaded = [];
      for (var i = 0; i < count; i++)
      {
        var due = pkg.ReadDouble(); var argc = pkg.ReadInt();
        if (double.IsNaN(due) || double.IsInfinity(due) || argc < 0 || argc > bytes.Length)
          throw new InvalidOperationException("Invalid poke timer entry.");
        var args = new string[argc];
        for (var a = 0; a < argc; a++)
          args[a] = pkg.ReadString();
        loaded.Add(new Entry(target.m_uid, due, args));
      }
      if (pkg.GetPos() != pkg.Size()) throw new InvalidOperationException("Trailing poke timer data.");
      foreach (var entry in loaded) Insert(entry);
    }
    catch (Exception e)
    {
      Log.Warning($"Saved poke timers were not restored: {e.Message}");
    }
  }
}
