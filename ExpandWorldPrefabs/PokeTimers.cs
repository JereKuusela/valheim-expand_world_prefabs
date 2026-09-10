using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Service;

namespace ExpandWorld.Prefab;

// Timer data lives on the target ZDO, in the same save as the affected world.
// Never persist ZDOIDs: Valheim assigns new IDs while loading that save.
public static class PokeTimers
{
  private const int Format = 1;
  // Conservatively keep ID-shaped arguments session-local unless they refer exactly to self.
  private static readonly Regex ObjectId = new(@"(?<![0-9])[+-]?[0-9]+:[0-9]+(?![0-9])");
  private static bool WarnedAboutReferences;
  private static readonly int StorageHash = "_ewp_pending_pokes".GetStableHashCode();
  private sealed class Entry(ZDO target, double due, long order, int ordinal, string[] args)
  {
    internal readonly ZDO Target = target;
    internal readonly ZDOID Id = target.m_uid;
    internal readonly double Due = due;
    internal readonly long Serial = ++NextSerial;
    internal readonly long Order = order;
    internal readonly int Ordinal = ordinal;
    internal readonly string[] Args = args;
    internal readonly string Self = target.m_uid.ToString();
    internal bool CanPersist => Args.All(arg => arg == Self || !ObjectId.IsMatch(arg));
  }
  private static long NextSerial;
  private static double Clock;
  private static readonly SortedSet<Entry> Pending = new(Comparer<Entry>.Create((a, b) =>
    a.Due != b.Due ? a.Due.CompareTo(b.Due) : a.Serial.CompareTo(b.Serial)));
  private static readonly Dictionary<ZDOID, List<Entry>> ByTarget = [];
  private static readonly Dictionary<ZDOID, ZDO> Dirty = [];
  private static readonly HashSet<ZDOID> Restored = [];
  private static readonly HashSet<ZDOID> Invalid = [];
  private static readonly HashSet<ZDOID> WriteFailures = [];
  private static ZDOMan? Session;
  private static long World;
  private static long Order;

  private static bool Active() => ZNet.instance && ZNet.instance.enabled &&
    ZNet.instance.IsServer() && ZNet.World != null && ZDOMan.instance != null;

  private static bool EnsureSession()
  {
    if (!Active()) return false;
    if (!ReferenceEquals(Session, ZDOMan.instance) || World != ZNet.World.m_uid)
    {
      Clear();
      Session = ZDOMan.instance;
      World = ZNet.World.m_uid;
    }
    return true;
  }

  public static void Clear()
  {
    Pending.Clear(); ByTarget.Clear(); Dirty.Clear(); Restored.Clear(); Invalid.Clear(); WriteFailures.Clear();
    Session = null; World = 0; Order = 0; Clock = 0; NextSerial = 0; WarnedAboutReferences = false;
  }

  private static void Insert(Entry entry)
  {
    Pending.Add(entry);
    if (!ByTarget.TryGetValue(entry.Id, out var entries))
      ByTarget[entry.Id] = entries = [];
    entries.Add(entry);
  }

  public static void Add(float delay, ZDOID[] targets, string[] args)
  {
    if (!EnsureSession()) return;
    if (float.IsNaN(delay) || float.IsInfinity(delay))
    {
      Log.Warning("Skipped delayed poke with a non-finite delay.");
      return;
    }
    var order = ++Order;
    // Keep already-evaluated arguments. Do not reroll selection or repeats on reload.
    var parameters = (string[])args.Clone();
    for (var i = 0; i < targets.Length; i++)
    {
      try
      {
        var target = Session!.GetZDO(targets[i]);
        if (target == null || Invalid.Contains(target.m_uid)) continue;
        Insert(new Entry(target, Clock + delay, order, i, parameters));
        Dirty[target.m_uid] = target;
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
    Dirty[entry.Id] = entry.Target;
  }

  public static void Execute(float dt)
  {
    if (!EnsureSession()) return;
    Clock += dt;
    // Only inspect due work. A large multi-target delay is not a full recipient scan per frame.
    List<Entry> ready = [];
    while (Pending.Count > 0 && Pending.Min!.Due - Clock <= -0.001)
    {
      var entry = Pending.Min!;
      Pending.Remove(entry);
      ready.Add(entry);
    }
    ready.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) :
      a.Ordinal != b.Ordinal ? a.Ordinal.CompareTo(b.Ordinal) : a.Serial.CompareTo(b.Serial));
    try
    {
      foreach (var entry in ready)
      {
        // Consume before dispatch: a throwing recipient must not replay earlier recipients.
        Remove(entry);
        try
        {
          if (ReferenceEquals(Session!.GetZDO(entry.Id), entry.Target) && entry.Target.m_uid == entry.Id)
            Manager.Handle(ActionType.Poke, entry.Args, entry.Id);
        }
        catch (Exception e)
        {
          Log.Warning($"Delayed poke failed and was consumed: {e}");
        }
        if (!Active() || !ReferenceEquals(Session, ZDOMan.instance)) break;
      }
    }
    finally { Flush(); }
  }

  // Batch mutations per target; no payload rewrite merely because a timer ticks.
  // Also called before the native save clones are taken, including saves mid-frame.
  public static void Flush(bool snapshot = false)
  {
    if (!Active() || !ReferenceEquals(Session, ZDOMan.instance)) return;
    if (snapshot)
      foreach (var pair in ByTarget)
        Dirty[pair.Key] = pair.Value[0].Target;
    foreach (var pair in Dirty.ToArray())
    {
      try
      {
        if (ReferenceEquals(Session!.GetZDO(pair.Key), pair.Value) && pair.Value.m_uid == pair.Key && pair.Value.Persistent)
        {
          ByTarget.TryGetValue(pair.Key, out var entries);
          pair.Value.Set(StorageHash, Encode(entries));
        }
        Dirty.Remove(pair.Key);
        WriteFailures.Remove(pair.Key);
      }
      catch (Exception e)
      {
        // Do not silently turn a failed save update into a successful one.
        if (WriteFailures.Add(pair.Key))
          Log.Warning($"Could not update saved poke timers (will retry): {e.Message}");
      }
    }
  }

  private static byte[] Encode(List<Entry>? entries)
  {
    if (entries == null || entries.Count == 0) return [];
    var safe = entries.Where(entry => entry.CanPersist).ToList();
    if (safe.Count != entries.Count && !WarnedAboutReferences)
    {
      WarnedAboutReferences = true;
      Log.Warning("Pokes carrying other or embedded object IDs remain session-local; their references cannot safely survive a world reload.");
    }
    if (safe.Count == 0) return [];
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write(Format); writer.Write(World); writer.Write(safe.Count);
    foreach (var entry in safe)
    {
      writer.Write(entry.Due - Clock); writer.Write(entry.Order); writer.Write(entry.Ordinal);
      writer.Write(entry.Args.Length);
      foreach (var arg in entry.Args)
      {
        var self = arg == entry.Self;
        writer.Write(self);
        if (!self) writer.Write(arg);
      }
    }
    return stream.ToArray();
  }

  public static void Restore(ZDO target)
  {
    if (!EnsureSession()) return;
    var bytes = target.GetByteArray(StorageHash, Array.Empty<byte>());
    if (bytes.Length == 0 || !Restored.Add(target.m_uid)) return;
    try
    {
      using var stream = new MemoryStream(bytes, false);
      using var reader = new BinaryReader(stream);
      if (reader.ReadInt32() != Format) throw new InvalidDataException("Unknown poke timer format.");
      if (reader.ReadInt64() != World) throw new InvalidDataException("Poke timers belong to a different world.");
      var count = reader.ReadInt32();
      if (count < 0 || count > bytes.Length / 24) throw new InvalidDataException("Invalid poke timer count.");
      List<Entry> loaded = [];
      for (var i = 0; i < count; i++)
      {
        var due = reader.ReadDouble(); var order = reader.ReadInt64(); var ordinal = reader.ReadInt32();
        var argc = reader.ReadInt32();
        if (double.IsNaN(due) || double.IsInfinity(due) || order < 0 || ordinal < 0 || argc < 0 || argc > stream.Length - stream.Position)
          throw new InvalidDataException("Invalid poke timer entry.");
        var args = new string[argc];
        for (var a = 0; a < argc; a++)
        {
          var self = reader.ReadByte();
          if (self > 1) throw new InvalidDataException("Invalid poke argument kind.");
          args[a] = self == 1 ? target.m_uid.ToString() : reader.ReadString();
        }
        var entry = new Entry(target, Clock + due, order, ordinal, args);
        if (!entry.CanPersist) throw new InvalidDataException("Saved poke contains session-local references.");
        loaded.Add(entry);
      }
      if (stream.Position != stream.Length) throw new InvalidDataException("Trailing poke timer data.");
      foreach (var entry in loaded) { Insert(entry); Order = Math.Max(Order, entry.Order); }
    }
    catch (Exception e)
    {
      // Preserve unreadable data for diagnosis; do not partially restore or overwrite it.
      Invalid.Add(target.m_uid);
      Log.Warning($"Saved poke timers were not restored: {e.Message}");
    }
  }
}

[HarmonyPatch(typeof(ZDO), nameof(ZDO.Load))]
public static class LoadPokeTimers
{
  static void Postfix(ZDO __instance) => PokeTimers.Restore(__instance);
}
[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.PrepareSave))]
public static class SavePokeTimers
{
  static void Prefix() => PokeTimers.Flush(true);
}
[HarmonyPatch(typeof(ZDOID), nameof(ZDOID.Reset))]
public static class ResetPokeTimers
{
  static void Prefix() => PokeTimers.Clear();
}
[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.ShutDown))]
public static class StopPokeTimers
{
  static void Postfix() => PokeTimers.Clear();
}

[HarmonyPatch(typeof(ZDOMan), "ResetBeforeLoad")]
public static class ReloadPokeTimers
{
  static void Prefix() => PokeTimers.Clear();
}
