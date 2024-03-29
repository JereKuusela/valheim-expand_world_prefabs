using System.Collections.Generic;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class Manager
{

  public static void Handle(ActionType type, string args, ZDO zdo, ZDO? source = null)
  {
    // Already destroyed before.
    if (ZDOMan.instance.m_deadZDOs.ContainsKey(zdo.m_uid)) return;
    if (!ZNet.instance.IsServer()) return;
    var name = ZNetScene.instance.GetPrefab(zdo.m_prefab)?.name ?? "";
    var parameters = Helper.CreateParameters(name, args, zdo);
    var info = InfoSelector.Select(type, zdo, args, parameters, source);
    if (info == null) return;
    Commands.Run(info, zdo, parameters, source);
    HandleSpawns(info, zdo, parameters);
    Poke(info, zdo, parameters);
    if (info.Drops)
      SpawnDrops(zdo);
    // Original object was regenerated to apply data.
    if (info.Remove || info.Data != "")
      DelayedRemove.Add(info.RemoveDelay, zdo, info.Remove && info.TriggerRules);
  }
  private static void HandleSpawns(Info info, ZDO zdo, Dictionary<string, string> parameters)
  {
    // Original object must be regenerated to apply data.
    var regenerateOriginal = !info.Remove && info.Data != "";
    if (info.Spawns.Length == 0 && info.Swaps.Length == 0 && !regenerateOriginal) return;

    var customData = DataHelper.Get(info.Data);
    foreach (var p in info.Spawns)
      CreateObject(p, zdo, customData, parameters, info.TriggerRules);

    if (info.Swaps.Length == 0 && !regenerateOriginal) return;
    var data = DataHelper.Merge(new DataEntry(zdo), customData);

    foreach (var p in info.Swaps)
      CreateObject(p, zdo, data, parameters, info.TriggerRules);
    if (regenerateOriginal)
      CreateObject(zdo, data, parameters, false);
  }
  public static void RemoveZDO(ZDO zdo, bool triggerRules)
  {
    if (!triggerRules)
      ZDOMan.instance.m_deadZDOs[zdo.m_uid] = ZNet.instance.GetTime().Ticks;
    zdo.SetOwner(ZDOMan.instance.m_sessionID);
    ZDOMan.instance.DestroyZDO(zdo);
  }
  public static void CreateObject(Spawn spawn, ZDO originalZdo, DataEntry? data, Dictionary<string, string> parameters, bool triggerRules)
  {
    var pos = originalZdo.m_position;
    var rot = originalZdo.GetRotation();
    pos += rot * spawn.Pos;
    rot *= spawn.Rot;
    data = DataHelper.Merge(data, DataHelper.Get(spawn.Data));
    DelayedSpawn.Add(spawn.Delay, pos, rot, spawn.GetPrefab(parameters), originalZdo.GetOwner(), data, parameters, triggerRules);
  }
  public static void CreateObject(ZDO originalZdo, DataEntry? data, Dictionary<string, string> parameters, bool triggerRules) => CreateObject(originalZdo.m_prefab, originalZdo.m_position, originalZdo.GetRotation(), originalZdo.GetOwner(), data, parameters, triggerRules);
  public static void CreateObject(int prefab, Vector3 pos, Quaternion rot, long owner, DataEntry? data, Dictionary<string, string> parameters, bool triggerRules)
  {
    if (prefab == 0) return;
    var obj = ZNetScene.instance.GetPrefab(prefab);
    if (!obj || !obj.TryGetComponent<ZNetView>(out var view))
    {
      Log.Error($"Can't spawn missing prefab: {prefab}");
      return;
    }
    // Prefab hash is used to check whether to trigger rules.
    var zdo = ZDOMan.instance.CreateNewZDO(pos, triggerRules ? prefab : 0);
    zdo.Persistent = view.m_persistent;
    zdo.Type = view.m_type;
    zdo.Distant = view.m_distant;
    zdo.m_prefab = prefab;
    zdo.m_rotation = rot.eulerAngles;
    // Some client should always be the owner so that creatures are initialized correctly (for example max health from stars).
    // Things work slightly better when the server doesn't have ownership (for example max health from stars).

    // For client spawns, the original owner can be just used.
    if (!ZNetView.m_ghostInit && ZNet.instance.IsDedicated() && owner == ZDOMan.instance.m_sessionID && !ZNetView.m_ghostInit)
    {
      // But if the server spawns, the owner must be handled manually.
      // Unless ghost init, because those are meant to be unloaded.
      var closestClient = ZDOMan.instance.m_peers.OrderBy(p => Utils.DistanceXZ(p.m_peer.m_refPos, pos)).FirstOrDefault(p => p.m_peer.m_uid != owner);
      owner = closestClient?.m_peer.m_uid ?? 0;
    }
    zdo.SetOwnerInternal(owner);
    data?.Write(parameters ?? [], zdo);
  }

  public static void SpawnDrops(ZDO zdo)
  {
    var obj = ZNetScene.instance.CreateObject(zdo);
    obj.GetComponent<ZNetView>().m_ghost = true;
    ZNetScene.instance.m_instances.Remove(zdo);
    HandleCreated.Skip = true;
    if (obj.TryGetComponent<DropOnDestroyed>(out var drop))
      drop.OnDestroyed();
    if (obj.TryGetComponent<CharacterDrop>(out var characterDrop))
    {
      characterDrop.m_character = obj.GetComponent<Character>();
      if (characterDrop.m_character)
        characterDrop.OnDeath();
    }
    if (obj.TryGetComponent<Piece>(out var piece))
      piece.DropResources();
    HandleCreated.Skip = false;
    UnityEngine.Object.Destroy(obj);
  }

  public static void Poke(Info info, ZDO zdo, Dictionary<string, string> parameters)
  {
    var zdos = ObjectsFiltering.GetNearby(info.PokeLimit, info.Pokes, zdo, parameters);
    var pokeParameter = Helper.ReplaceParameters(info.PokeParameter, parameters);
    foreach (var z in zdos)
      Handle(ActionType.Poke, pokeParameter, z);
  }
}
