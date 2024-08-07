using System.Collections.Generic;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class Manager
{
  public static void HandleGlobal(ActionType type, string args, Vector3 pos, bool remove)
  {
    if (!ZNet.instance.IsServer()) return;
    Parameters parameters = new("", args, pos);
    var info = InfoSelector.SelectGlobal(type, args, parameters, pos, remove);
    if (info == null) return;
    if (info.Commands.Length > 0)
      Commands.Run(info, parameters);
    if (info.ClientRpcs != null)
      GlobalClientRpc(info.ClientRpcs, parameters);
    PokeGlobal(info, parameters, pos);
  }
  public static void Handle(ActionType type, string args, ZDO zdo, ZDO? source = null)
  {
    // Already destroyed before.
    if (ZDOMan.instance.m_deadZDOs.ContainsKey(zdo.m_uid)) return;
    if (!ZNet.instance.IsServer()) return;
    var name = ZNetScene.instance.GetPrefab(zdo.m_prefab)?.name ?? "";
    ObjectParameters parameters = new(name, args, zdo);
    var info = InfoSelector.Select(type, zdo, args, parameters, source);
    if (info == null) return;

    if (info.Commands.Length > 0)
      Commands.Run(info, parameters);

    if (info.ObjectRpcs != null)
      ObjectRpc(info.ObjectRpcs, zdo, parameters);
    if (info.ClientRpcs != null)
      ClientRpc(info.ClientRpcs, zdo, parameters);
    HandleSpawns(info, zdo, parameters);
    Poke(info, zdo, parameters);
    if (info.Drops)
      SpawnDrops(zdo);
    // Original object was regenerated to apply data.
    if (info.Remove || info.Regenerate)
      DelayedRemove.Add(info.RemoveDelay, zdo, info.Remove && info.TriggerRules);
    else if (info.InjectData)
    {
      var data = DataHelper.Get(info.Data);
      var removeItems = info.RemoveItems;
      var addItems = info.AddItems;
      if (data != null)
      {
        ZdoEntry entry = new(zdo);
        entry.Load(data, parameters);
        entry.Write(zdo);
      }
      removeItems?.RemoveItems(parameters, zdo);
      addItems?.AddItems(parameters, zdo);
      if (data != null || removeItems != null || addItems != null)
      {
        zdo.DataRevision += 100;
        ZDOMan.instance.ForceSendZDO(zdo.m_uid);
      }
    }
  }
  private static void HandleSpawns(Info info, ZDO zdo, Parameters pars)
  {
    // Original object must be regenerated to apply data.
    var regenerateOriginal = !info.Remove && info.Regenerate;
    if (info.Spawns.Length == 0 && info.Swaps.Length == 0 && !regenerateOriginal) return;

    var customData = DataHelper.Get(info.Data);
    foreach (var p in info.Spawns)
      CreateObject(p, zdo, customData, pars, p.TriggerRules);

    if (info.Swaps.Length == 0 && !regenerateOriginal) return;
    var data = DataHelper.Merge(new DataEntry(zdo), customData);
    foreach (var p in info.Swaps)
      CreateObject(p, zdo, data, pars, p.TriggerRules);
    if (regenerateOriginal)
    {
      var removeItems = info.RemoveItems;
      var addItems = info.AddItems;
      ZdoEntry entry = new(zdo);
      if (data != null)
        entry.Load(data, pars);
      var newZdo = CreateObject(entry, false);
      if (newZdo != null)
      {
        removeItems?.RemoveItems(pars, newZdo);
        addItems?.AddItems(pars, newZdo);
        PrefabConnector.AddSwap(zdo.m_uid, newZdo.m_uid);
      }
    }
  }
  public static void RemoveZDO(ZDO zdo, bool triggerRules)
  {
    if (!triggerRules)
      ZDOMan.instance.m_deadZDOs[zdo.m_uid] = ZNet.instance.GetTime().Ticks;
    zdo.SetOwner(ZDOMan.instance.m_sessionID);
    ZDOMan.instance.DestroyZDO(zdo);
  }
  public static void CreateObject(Spawn spawn, ZDO originalZdo, DataEntry? data, Parameters parameters, bool triggerRules)
  {
    var pos = originalZdo.m_position;
    var rotQuat = originalZdo.GetRotation();
    pos += rotQuat * spawn.Pos;
    rotQuat *= spawn.Rot;
    var rot = rotQuat.eulerAngles;
    if (spawn.Snap)
      pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
    data = DataHelper.Merge(data, DataHelper.Get(spawn.Data));
    var prefab = spawn.GetPrefab(parameters);
    ZdoEntry zdoEntry = new(prefab, pos, rot, originalZdo);
    if (data != null)
      zdoEntry.Load(data, parameters);
    DelayedSpawn.Add(spawn.Delay, zdoEntry, triggerRules);
  }

  public static ZDO? CreateObject(ZdoEntry entry, bool triggerRules)
  {
    HandleCreated.Skip = !triggerRules;
    var zdo = entry.Create();
    HandleCreated.Skip = false;
    if (zdo == null) return null;
    FixOwner(zdo);
    return zdo;
  }

  private static void FixOwner(ZDO zdo)
  {
    // Some client should always be the owner so that creatures are initialized correctly (for example max health from stars).
    // Things work slightly better when the server doesn't have ownership (for example max health from stars).

    // During ghost init, objects are meant to be unloaded so setting owner could cause issues.
    if (ZNetView.m_ghostInit) return;
    // When single player, the owner is always the client.
    // When self-hosted, things might not work but can be fixed later if needed.
    if (!ZNet.instance.IsDedicated()) return;
    // For client spawns, the original owner can be just used.
    if (zdo.GetOwner() != ZDOMan.instance.m_sessionID) return;

    var closestClient = ZDOMan.instance.m_peers.OrderBy(p => Utils.DistanceXZ(p.m_peer.m_refPos, zdo.m_position)).FirstOrDefault(p => p.m_peer.m_uid != zdo.GetOwner());
    zdo.SetOwnerInternal(closestClient?.m_peer.m_uid ?? 0);
  }

  public static void SpawnDrops(ZDO zdo)
  {
    if (ZNetScene.instance.m_instances.ContainsKey(zdo))
    {
      SpawnDrops(ZNetScene.instance.m_instances[zdo].gameObject);
    }
    else
    {
      var obj = ZNetScene.instance.CreateObject(zdo);
      obj.GetComponent<ZNetView>().m_ghost = true;
      ZNetScene.instance.m_instances.Remove(zdo);
      SpawnDrops(obj);
      UnityEngine.Object.Destroy(obj);
    }
  }
  private static void SpawnDrops(GameObject obj)
  {
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
    if (obj.TryGetComponent<TreeBase>(out var tree))
    {
      var items = tree.m_dropWhenDestroyed.GetDropList();
      foreach (var item in items)
        UnityEngine.Object.Instantiate(item, obj.transform.position, Quaternion.identity);
    }
    if (obj.TryGetComponent<TreeLog>(out var log))
    {
      var items = log.m_dropWhenDestroyed.GetDropList();
      foreach (var item in items)
        UnityEngine.Object.Instantiate(item, obj.transform.position, Quaternion.identity);
    }
    HandleCreated.Skip = false;
  }

  public static void Poke(Info info, ZDO zdo, Parameters pars)
  {
    if (info.LegacyPokes.Length > 0)
    {
      var zdos = ObjectsFiltering.GetNearby(info.PokeLimit, info.LegacyPokes, zdo.m_position, pars);
      var pokeParameter = pars.Replace(info.PokeParameter);
      DelayedPoke.Add(info.PokeDelay, zdos, pokeParameter);
    }
    foreach (var poke in info.Pokes)
    {
      var zdos = ObjectsFiltering.GetNearby(poke.Limit.Get(pars) ?? 0, poke.Filter, zdo.m_position, pars);
      var pokeParameter = pars.Replace(poke.Parameter.Get(pars) ?? "");
      DelayedPoke.Add(poke.Delay.Get(pars) ?? 0f, zdos, pokeParameter);

    }
  }
  public static void PokeGlobal(Info info, Parameters pars, Vector3 pos)
  {
    if (info.LegacyPokes.Length > 0)
    {
      var zdos = ObjectsFiltering.GetNearby(info.PokeLimit, info.LegacyPokes, pos, pars);
      var pokeParameter = pars.Replace(info.PokeParameter);
      DelayedPoke.Add(info.PokeDelay, zdos, pokeParameter);
    }
    foreach (var poke in info.Pokes)
    {
      var zdos = ObjectsFiltering.GetNearby(poke.Limit.Get(pars) ?? 0, poke.Filter, pos, pars);
      var pokeParameter = pars.Replace(poke.Parameter.Get(pars) ?? "");
      DelayedPoke.Add(poke.Delay.Get(pars) ?? 0f, zdos, pokeParameter);

    }
  }
  public static void Poke(ZDO[] zdos, string parameter)
  {
    foreach (var z in zdos)
      Handle(ActionType.Poke, parameter, z);
  }

  public static void ObjectRpc(ObjectRpcInfo[] info, ZDO zdo, Parameters parameters)
  {
    foreach (var i in info)
      i.Invoke(zdo, parameters);
  }
  public static void ClientRpc(ClientRpcInfo[] info, ZDO zdo, Parameters parameters)
  {
    foreach (var i in info)
      i.Invoke(zdo, parameters);
  }
  public static void GlobalClientRpc(ClientRpcInfo[] info, Parameters parameters)
  {
    foreach (var i in info)
      i.InvokeGlobal(parameters);
  }
  public static void Rpc(long source, long target, ZDOID id, int hash, object[] parameters)
  {
    var router = ZRoutedRpc.instance;
    ZRoutedRpc.RoutedRPCData routedRPCData = new()
    {
      m_msgID = router.m_id + router.m_rpcMsgID++,
      m_senderPeerID = source,
      m_targetPeerID = target,
      m_targetZDO = id,
      m_methodHash = hash
    };
    ZRpc.Serialize(parameters, ref routedRPCData.m_parameters);
    routedRPCData.m_parameters.SetPos(0);
    if (target == router.m_id || target == ZRoutedRpc.Everybody)
      router.HandleRoutedRPC(routedRPCData);
    if (target != router.m_id)
      router.RouteRPC(routedRPCData);
  }
}
