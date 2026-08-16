using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Service;
using Splatform;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class NPCManager
{
  private static bool IsPatched = false;
  private static readonly Dictionary<string, NPCGroup> Profiles = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<ZDOID, string> NameById = [];

  public static void Patch(Harmony harmony, bool shouldPatch)
  {
    if (shouldPatch && !IsPatched)
      DoPatch(harmony);
    if (!shouldPatch && IsPatched)
      DoUnpatch(harmony);

    if (shouldPatch && ZDOMan.instance != null)
      ScanAll();
    if (!shouldPatch)
      Clear();
  }

  public static bool IsEnabled => Config.NpcPlayerListRange > 0f;


  private static void DoPatch(Harmony harmony)
  {
    IsPatched = true;
    var method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.Load));
    var patch = AccessTools.Method(typeof(NPCManager), nameof(AfterLoad));
    harmony.Patch(method, postfix: new HarmonyMethod(patch));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.Load));
    var patch = AccessTools.Method(typeof(NPCManager), nameof(AfterLoad));
    harmony.Unpatch(method, patch);
  }

  private static void AfterLoad()
  {
    if (!IsEnabled)
      return;
    ScanAll();
  }


  private static void ScanAll()
  {
    Clear();
    var zdoMan = ZDOMan.instance;
    if (!IsEnabled || zdoMan == null)
      return;

    foreach (var zdo in zdoMan.m_objectsByID.Values)
      Track(zdo);
  }

  public static void Track(ZDO zdo)
  {
    if (!IsEnabled || !PersistPlayers.IsNpc(zdo))
      return;
    var name = GetName(zdo);

    // If the same ZDO is re-tracked with a new name, remove stale mapping first.
    if (NameById.TryGetValue(zdo.m_uid, out var previousName) && !string.Equals(previousName, name, StringComparison.OrdinalIgnoreCase))
      RemoveFromGroup(previousName, zdo.m_uid);

    if (!Profiles.TryGetValue(name, out var group))
      Profiles[name] = group = new NPCGroup(name);

    group.Positions[zdo.m_uid] = zdo.m_position;
    NameById[zdo.m_uid] = name;
    ZNet.instance.SendPlayerList();
  }

  public static void Untrack(ZDOID uid)
  {
    if (!IsEnabled)
      return;

    if (!NameById.TryGetValue(uid, out var name))
      return;

    NameById.Remove(uid);
    RemoveFromGroup(name, uid);
    ZNet.instance.SendPlayerList();
  }


  public static List<NPCSelection> Search(Vector3 center)
  {
    List<NPCSelection> selections = [];

    foreach (var group in Profiles.Values)
    {
      var hasNearby = false;
      var closestDistance = float.MaxValue;
      var closestId = ZDOID.None;
      var closestPosition = Vector3.zero;
      foreach (var position in group.Positions)
      {
        var distance = Utils.DistanceXZ(position.Value, center);
        if (distance > Config.NpcPlayerListRange)
          continue;
        if (distance < closestDistance)
        {
          hasNearby = true;
          closestDistance = distance;
          closestId = position.Key;
          closestPosition = position.Value;
        }
      }

      if (!hasNearby)
        continue;

      selections.Add(new NPCSelection(group, closestId, closestPosition));
    }
    return selections;
  }

  public static bool TryGetUserInfo(string name, out UserInfo userInfo)
  {
    userInfo = new UserInfo();
    if (string.IsNullOrEmpty(name))
      return false;

    if (!Profiles.TryGetValue(name, out var match))
      return false;

    userInfo = new UserInfo
    {
      Name = match.Name,
      UserId = match.UserId,
    };
    return true;
  }

  public static void WriteProfiles(ZPackage pkg, IEnumerable<NPCSelection> profiles)
  {
    foreach (var profile in profiles)
      WriteProfile(pkg, profile);
  }

  private static ZDOID FakeZDOID = new(long.MaxValue, 1);
  private static void WriteProfile(ZPackage pkg, NPCSelection profile)
  {
    pkg.Write(profile.Group.Name);
    // Some ZDOID is required to appear on minimap.
    // But using real one causes duplicate received messages.
    pkg.Write(FakeZDOID);
    pkg.Write(profile.Group.UserId.ToString());
    pkg.Write(profile.Group.DisplayName);
    pkg.Write(profile.Group.DisplayName);
    pkg.Write(true);
    pkg.Write(profile.Position);
  }

  private static string GetName(ZDO zdo)
  {
    var name = zdo.GetString(ZDOVars.s_playerName, "");
    if (string.IsNullOrEmpty(name))
      name = ServerClient.Client.m_userInfo.m_displayName;
    return name;
  }

  private static void RemoveFromGroup(string name, ZDOID uid)
  {
    if (!Profiles.TryGetValue(name, out var group))
      return;

    group.Positions.Remove(uid);
    if (group.Positions.Count == 0)
      Profiles.Remove(name);
  }

  private static void Clear()
  {
    Profiles.Clear();
    NameById.Clear();
  }

  public sealed class NPCGroup(string name)
  {
    public readonly string Name = name;
    public readonly string DisplayName = name;
    public readonly PlatformUserID UserId = new("npc", name);
    public readonly Dictionary<ZDOID, Vector3> Positions = [];
  }

  public readonly struct NPCSelection(NPCGroup group, ZDOID characterId, Vector3 position)
  {
    public NPCGroup Group { get; } = group;
    public ZDOID CharacterId { get; } = characterId;
    public Vector3 Position { get; } = position;
  }
}
