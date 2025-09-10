using System.Collections.Generic;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public enum ActionType
{
  Create,
  Destroy,
  Repair,
  Damage,
  State,
  Command,
  Say,
  Poke,
  GlobalKey,
  Event,
  Change,
  Key,
  Time
}
public class InfoManager
{
  public static readonly PrefabInfo CreateDatas = new();
  public static readonly PrefabInfo RemoveDatas = new();
  public static readonly PrefabInfo RepairDatas = new();
  public static readonly PrefabInfo DamageDatas = new();
  public static readonly PrefabInfo StateDatas = new();
  public static readonly PrefabInfo SayDatas = new();
  public static readonly PrefabInfo PokeDatas = new();
  public static readonly PrefabInfo ChangeDatas = new();
  public static readonly GlobalInfo GlobalKeyDatas = new();
  public static readonly GlobalInfo KeyDatas = new();
  public static readonly GlobalInfo EventDatas = new();
  public static readonly GlobalInfo TimeDatas = new();

  public static void Clear()
  {
    CreateDatas.Clear();
    RemoveDatas.Clear();
    RepairDatas.Clear();
    DamageDatas.Clear();
    StateDatas.Clear();
    SayDatas.Clear();
    PokeDatas.Clear();
    GlobalKeyDatas.Clear();
    KeyDatas.Clear();
    EventDatas.Clear();
    ChangeDatas.Clear();
    TimeDatas.Clear();
  }
  public static void Add(Info info)
  {
    if (info.Type == ActionType.GlobalKey)
    {
      GlobalKeyDatas.Add(info);
      return;
    }
    if (info.Type == ActionType.Key)
    {
      KeyDatas.Add(info);
      return;
    }
    if (info.Type == ActionType.Event)
    {
      EventDatas.Add(info);
      return;
    }
    if (info.Type == ActionType.Time)
    {
      TimeDatas.Add(info);
      return;
    }
    if (info.Type == ActionType.Command)
    {
      info.Admin = new SimpleBoolValue(true);
      info.Type = ActionType.Say;
    }
    Select(info.Type).Add(info);
  }
  public static void Patch()
  {
    EWP.Harmony.UnpatchSelf();
    if (Helper.IsClient())
      return;
    EWP.Harmony.PatchAll();
    if (CreateDatas.Exists)
      HandleCreated.Patch(EWP.Harmony);
    if (RemoveDatas.Exists)
      HandleDestroyed.Patch(EWP.Harmony);
    if (RepairDatas.Exists || DamageDatas.Exists || StateDatas.Exists || SayDatas.Exists)
    {
      HandleRPC.Patch(EWP.Harmony);
      HandleRPC.SetRequiredStates(GetRequiredStates());
    }
    if (SayDatas.Exists)
      ServerClient.Patch(EWP.Harmony);
    if (GlobalKeyDatas.Exists)
      HandleGlobalKey.Patch(EWP.Harmony);
    if (EventDatas.Exists)
      HandleEvent.Patch(EWP.Harmony);
    if (ChangeDatas.Exists)
      HandleChanged.Patch(EWP.Harmony, ChangeDatas);
    if (TimeDatas.Exists)
    {
      var checkTicks = TimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "tick");
      var checkMinutes = TimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "minute");
      var checkHours = TimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "hour");
      var checkDays = TimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "day");
      HandleTime.Patch(EWP.Harmony, checkTicks, checkMinutes, checkHours, checkDays);
    }
    DataStorage.OnSet = KeyDatas.Exists ? OnKeySet : null;
  }

  private static HashSet<string> GetRequiredStates()
  {
    var states = new HashSet<string>();
    if (SayDatas.Exists) states.Add("say");
    if (DamageDatas.Exists) states.Add("damage");
    if (RepairDatas.Exists) states.Add("repair");

    // Collect from all three categories: Weighted, Fallback, and Separate
    foreach (var infos in StateDatas.Weighted.Values)
    {
      foreach (var info in infos)
      {
        if (info.Args.Length > 0)
          states.Add(info.Args[0]);
      }
    }

    foreach (var infos in StateDatas.Fallback.Values)
    {
      foreach (var info in infos)
      {
        if (info.Args.Length > 0)
          states.Add(info.Args[0]);
      }
    }

    foreach (var infos in StateDatas.Separate.Values)
    {
      foreach (var info in infos)
      {
        if (info.Args.Length > 0)
          states.Add(info.Args[0]);
      }
    }

    return states;
  }

  private static void OnKeySet(string key, string value)
  {
    Manager.HandleGlobal(ActionType.Key, [key, value], Vector3.zero, value == "");
  }
  public static PrefabInfo Select(ActionType type) => type switch
  {
    ActionType.Destroy => RemoveDatas,
    ActionType.Repair => RepairDatas,
    ActionType.Damage => DamageDatas,
    ActionType.State => StateDatas,
    ActionType.Say => SayDatas,
    ActionType.Poke => PokeDatas,
    ActionType.Create => CreateDatas,
    ActionType.Change => ChangeDatas,
    _ => Error(type),
  };
  private static PrefabInfo Error(ActionType type)
  {
    Log.Error($"Unknown entry type {type}");
    return new();
  }
  public static GlobalInfo SelectGlobal(ActionType type) => type switch
  {
    ActionType.GlobalKey => GlobalKeyDatas,
    ActionType.Key => KeyDatas,
    ActionType.Event => EventDatas,
    ActionType.Time => TimeDatas,
    _ => ErrorGlobal(type),
  };
  private static GlobalInfo ErrorGlobal(ActionType type)
  {
    Log.Error($"Unknown entry type {type}");
    return new();
  }
}

public class PrefabInfo
{
  public readonly Dictionary<int, List<Info>> Weighted = [];
  public readonly Dictionary<int, List<Info>> Fallback = [];
  public readonly Dictionary<int, List<Info>> Separate = [];
  public bool Exists => Weighted.Count > 0 || Fallback.Count > 0 || Separate.Count > 0;


  public void Clear()
  {
    Weighted.Clear();
    Fallback.Clear();
    Separate.Clear();
  }
  public void Add(Info info)
  {
    var prefabs = PrefabHelper.GetPrefabs(info.Prefabs, info.ExcludedPrefabs).ToList();
    foreach (var hash in prefabs)
    {
      if (info.Fallback)
      {
        if (!Fallback.TryGetValue(hash, out var list))
          Fallback[hash] = list = [];
        list.Add(info);
      }
      else if (info.Weight != null)
      {
        if (!Weighted.TryGetValue(hash, out var list))
          Weighted[hash] = list = [];
        list.Add(info);
      }
      else
      {
        if (!Separate.TryGetValue(hash, out var list))
          Separate[hash] = list = [];
        list.Add(info);
      }
    }
  }
  public bool TryGetWeightedValue(int prefab, out List<Info> list) => Weighted.TryGetValue(prefab, out list);
  public bool TryGetFallbackValue(int prefab, out List<Info> list) => Fallback.TryGetValue(prefab, out list);
  public bool TryGetSeparateValue(int prefab, out List<Info> list) => Separate.TryGetValue(prefab, out list);

}


public class GlobalInfo
{
  public readonly List<Info> Weighted = [];
  public readonly List<Info> Fallback = [];
  public readonly List<Info> Separate = [];
  public bool Exists => Weighted.Count > 0 || Fallback.Count > 0 || Separate.Count > 0;


  public void Clear()
  {
    Weighted.Clear();
    Fallback.Clear();
    Separate.Clear();
  }
  public void Add(Info info)
  {
    if (info.Fallback)
      Fallback.Add(info);
    else if (info.Weight != null)
      Weighted.Add(info);
    else
      Separate.Add(info);
  }
}
