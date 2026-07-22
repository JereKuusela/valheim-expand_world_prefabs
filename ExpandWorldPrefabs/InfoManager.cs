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
  State,
  Command,
  Say,
  Poke,
  GlobalKey,
  Event,
  Change,
  Key,
  Custom,
  Time,
  RealTime
}
public class InfoManager
{
  public static readonly PrefabInfo CreateDatas = new();
  public static readonly PrefabInfo RemoveDatas = new();
  public static readonly PrefabInfo StateDatas = new();
  public static readonly PrefabInfo SayDatas = new();
  public static readonly PrefabInfo PokeDatas = new();
  public static readonly PrefabInfo ChangeDatas = new();
  public static readonly GlobalInfo GlobalKeyDatas = new();
  public static readonly GlobalInfo KeyDatas = new();
  public static readonly GlobalInfo CustomDatas = new();
  public static readonly GlobalInfo EventDatas = new();
  public static readonly GlobalInfo TimeDatas = new();
  public static readonly GlobalInfo RealTimeDatas = new();

  public static void Clear()
  {
    CreateDatas.Clear();
    RemoveDatas.Clear();
    StateDatas.Clear();
    SayDatas.Clear();
    PokeDatas.Clear();
    GlobalKeyDatas.Clear();
    KeyDatas.Clear();
    CustomDatas.Clear();
    EventDatas.Clear();
    ChangeDatas.Clear();
    TimeDatas.Clear();
    RealTimeDatas.Clear();
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
    if (info.Type == ActionType.Custom)
    {
      CustomDatas.Add(info);
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
    if (info.Type == ActionType.RealTime)
    {
      RealTimeDatas.Add(info);
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
    var canPatch = !Helper.IsClient();
    var shouldPersistPlayers = canPatch && Settings.PersistPlayers;
    var shouldRestoreScale = canPatch && Settings.RestoreScale;
    var shouldSupportAttach = canPatch && Settings.SupportAttach;
    var shouldServerSideData = canPatch && Settings.ServerSideData;
    PersistPlayers.Patch(EWP.Harmony, shouldPersistPlayers);
    RestoreScale.Patch(EWP.Harmony, shouldRestoreScale);
    SupportAttach.Patch(EWP.Harmony, shouldSupportAttach);
    ServerSideData.Patch(EWP.Harmony, shouldServerSideData);

    var requiredStates = GetRequiredStates();
    var shouldHandleCreated = canPatch && (CreateDatas.Exists || requiredStates.Contains("join") || requiredStates.Contains("respawn"));
    var shouldHandleDestroyed = canPatch && RemoveDatas.Exists;
    var shouldHandleRpc = canPatch && (StateDatas.Exists || SayDatas.Exists);
    var shouldHandleSay = canPatch && SayDatas.Exists;
    var shouldHandleGlobalKey = canPatch && GlobalKeyDatas.Exists;
    var shouldHandleEvent = canPatch && EventDatas.Exists;
    var shouldTrackChanges = canPatch && ChangeDatas.Exists;
    var shouldTrackTime = canPatch && TimeDatas.Exists;
    var shouldTrackRealTime = canPatch && RealTimeDatas.Exists;
    var shouldHandlePeerState = canPatch && (requiredStates.Contains("join") || requiredStates.Contains("respawn") || requiredStates.Contains("leave"));
    var shouldHandleSwapConnections = canPatch && RequiresConnectionSwapTracking();

    HandleCreated.Patch(EWP.Harmony, shouldHandleCreated);
    HandleDestroyed.Patch(EWP.Harmony, shouldHandleDestroyed);
    HandleRPC.Patch(EWP.Harmony, shouldHandleRpc);
    if (shouldHandleRpc)
      HandleRPC.SetRequiredStates(requiredStates);

    ServerClient.Patch(EWP.Harmony, shouldHandleSay);
    HandleGlobalKey.Patch(EWP.Harmony, shouldHandleGlobalKey);
    HandleEvent.Patch(EWP.Harmony, shouldHandleEvent);
    HandleChanged.Patch(EWP.Harmony, ChangeDatas, shouldTrackChanges);

    if (shouldTrackTime)
    {
      var checkTicks = TimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "tick");
      var checkMinutes = TimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "minute");
      var checkHours = TimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "hour");
      var checkDays = TimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "day");
      HandleTime.Patch(EWP.Harmony, shouldTrackTime, checkTicks, checkMinutes, checkHours, checkDays);
    }
    else
      HandleTime.Patch(EWP.Harmony, false, false, false, false, false);

    if (shouldTrackRealTime)
    {
      var checkSeconds = RealTimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "second");
      var checkMinutes = RealTimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "minute");
      var checkHours = RealTimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "hour");
      var checkDays = RealTimeDatas.Separate.Any(v => v.Args.Length > 0 && v.Args[0] == "day");
      HandleTime.PatchRealTime(EWP.Harmony, shouldTrackRealTime, checkSeconds, checkMinutes, checkHours, checkDays);
    }
    else
      HandleTime.PatchRealTime(EWP.Harmony, false, false, false, false, false);

    PeerManager.Patch(EWP.Harmony, shouldHandlePeerState);
    PrefabConnector.Patch(EWP.Harmony, shouldHandleSwapConnections);

    DataStorage.OnSet = KeyDatas.Exists ? OnKeySet : null;
  }

  private static bool RequiresConnectionSwapTracking()
  {
    return HasSwapRules(CreateDatas) || HasSwapRules(StateDatas) || HasSwapRules(SayDatas) || HasSwapRules(PokeDatas) || HasSwapRules(ChangeDatas);
  }

  private static bool HasSwapRules(PrefabInfo prefabInfo)
  {
    foreach (var infos in prefabInfo.Weighted.Values)
    {
      foreach (var info in infos)
      {
        if (info.Swaps != null || info.WeightedSwaps != null)
          return true;
      }
    }
    foreach (var infos in prefabInfo.Fallback.Values)
    {
      foreach (var info in infos)
      {
        if (info.Swaps != null || info.WeightedSwaps != null)
          return true;
      }
    }
    foreach (var infos in prefabInfo.Separate.Values)
    {
      foreach (var info in infos)
      {
        if (info.Swaps != null || info.WeightedSwaps != null)
          return true;
      }
    }
    return false;
  }

  private static HashSet<string> GetRequiredStates()
  {
    var states = new HashSet<string>();
    if (SayDatas.Exists) states.Add("say");

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
    ActionType.Custom => CustomDatas,
    ActionType.Event => EventDatas,
    ActionType.Time => TimeDatas,
    ActionType.RealTime => RealTimeDatas,
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
