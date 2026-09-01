using System;
using System.Linq;
using Data;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class Manager
{
  public static void HandleGlobal(ActionType type, string[] args, Vector3 pos, bool remove)
  {
    if (!ZNet.instance.IsServer()) return;
    Functions f = new("", args, pos);
    var info = InfoSelector.SelectGlobalWeighted(type, args, f, pos, remove);
    var infos = InfoSelector.SelectGlobalSeparate(type, args, f, pos, remove);
    if (info == null && infos == null)
      info = InfoSelector.SelectGlobalFallback(type, args, f, pos, remove);

    if (info != null)
      HandleGlobal(info, f, pos, remove);
    if (infos != null)
      foreach (var i in infos)
        HandleGlobal(i, f, pos, remove);
  }
  private static void HandleGlobal(Info info, Functions f, Vector3 pos, bool remove)
  {
    if (info.Chance != null)
    {
      var chance = info.Chance.Get(f);
      if (chance != null && chance < 1f)
      {
        if (UnityEngine.Random.value > chance)
          return;
      }
    }

    info.Execute?.Get(f);
    if (info.Commands.Length > 0)
      Commands.Run(info, f);
    var weightedClientRpc = info.GetWeightedClientRpc(f);
    if (weightedClientRpc != null)
      weightedClientRpc.InvokeGlobal(f);
    if (info.ClientRpcs != null)
      GlobalClientRpc(info.ClientRpcs, f);
    PokeGlobal(info, f, pos);
  }
  public static bool Handle(ActionType type, string[] args, ZDOID id)
  {
    var zdo = ZDOMan.instance.GetZDO(id);
    if (zdo == null) return false;
    return Handle(type, args, zdo);
  }

  public static bool Handle(ActionType type, string[] args, ZDO zdo)
  {
    // Already destroyed before.
    if (ZDOMan.instance.m_deadZDOs.ContainsKey(zdo.m_uid)) return false;
    if (!ZNet.instance.IsServer()) return false;
    SupportAttach.SyncAttachedWorldTransform(zdo);
    var name = ZNetScene.instance.GetPrefab(zdo.m_prefab)?.name ?? "";
    ObjectFunctions f = new(name, args, zdo);
    var info = InfoSelector.SelectWeighted(type, zdo, args, f);
    var infos = InfoSelector.SelectSeparate(type, zdo, args, f);
    if (info == null && infos == null)
      info = InfoSelector.SelectFallback(type, zdo, args, f);
    if (info == null && infos == null) return false;

    bool ret = false;
    if (info != null)
      ret |= Handle(info, f, zdo);
    if (infos != null)
      foreach (var i in infos)
        ret |= Handle(i, f, zdo);
    return ret;
  }

  private static bool Handle(Info info, Functions f, ZDO zdo)
  {
    if (info.Chance != null)
    {
      var chance = info.Chance.Get(f);
      if (chance != null && chance < 1f)
      {
        if (UnityEngine.Random.value > chance)
          return false;
      }
    }

    info.Execute?.Get(f);
    if (info.Commands.Length > 0)
      Commands.Run(info, f);

    var weightedObjectRpc = info.GetWeightedObjectRpc(f);
    if (weightedObjectRpc != null)
      weightedObjectRpc.Invoke(zdo, f);
    var weightedClientRpc = info.GetWeightedClientRpc(f);
    if (weightedClientRpc != null)
      weightedClientRpc.Invoke(zdo, f);

    if (info.ObjectRpcs != null)
      ObjectRpc(info.ObjectRpcs, zdo, f);
    if (info.ClientRpcs != null)
      ClientRpc(info.ClientRpcs, zdo, f);

    var remove = info.Remove?.GetBool(f) == true;
    var data = DataHelper.Get(info.Data, f);
    var inject = info.InjectData ?? data?.CanBeInjected ?? false;
    var regenerate = info.Regenerate && !inject;
    var attach = info.Attach?.Get(f);
    if (attach.HasValue && !SupportAttach.CanSync(zdo))
      regenerate = true;
    if (PersistPlayers.IsRealPlayer(zdo))
      regenerate = false;
    HandleSpawns(info, zdo, f, remove, regenerate, data);
    Poke(info, zdo, f);
    Terrain(info, zdo, f);
    var drops = info.Drops?.Get(f);
    if (drops != null && drops == "true")
      SpawnDrops(zdo);
    else if (drops != null && drops != "false")
      SpawnItems(drops, zdo, f);
    // Original object was regenerated to apply data.
    if (remove || regenerate)
      DelayedRemove.Add(info.RemoveDelay?.Get(f) ?? 0f, zdo.m_uid, remove && info.TriggerRules);
    else
    {
      if (!info.TriggerRules)
        HandleChanged.IgnoreZdo = zdo.m_uid;
      var removeItems = info.RemoveItems;
      var addItems = info.AddItems;
      var hasSyncedDataChanges = false;
      if (data != null)
      {
        ZdoEntry entry = new(zdo);
        entry.Load(data, f);
        hasSyncedDataChanges = entry.HasSyncedChanges();
        if (hasSyncedDataChanges)
          entry.Write(zdo);
        else
          entry.WriteServer(zdo);
      }
      removeItems?.RemoveItems(f, zdo);
      addItems?.AddItems(f, zdo);
      var owner = info.Owner?.Get(f);
      if (info.OwnerServer)
        ServerOwned.Mark(zdo);
      else if (owner.HasValue)
      {
        ServerOwned.Unmark(zdo);
        zdo.SetOwner(owner.Value);
      }
      if (attach.HasValue)
        SupportAttach.Attach(zdo, attach.Value);
      var connect = info.Connect?.Get(f);
      if (connect.HasValue)
        SupportAttach.Connect(zdo, connect.Value);

      if (hasSyncedDataChanges || removeItems != null || addItems != null || attach.HasValue || connect.HasValue || owner.HasValue || info.OwnerServer)
        zdo.DataRevision += 100;
      HandleChanged.IgnoreZdo = ZDOID.None;
    }
    var cancel = info.Cancel?.GetBool(f) == true;

    return cancel;
  }
  public static bool CheckCancel(ActionType type, string[] args, ZDO zdo)
  {
    if (!ZNet.instance.IsServer()) return false;
    SupportAttach.SyncAttachedWorldTransform(zdo);
    var name = ZNetScene.instance.GetPrefab(zdo.m_prefab)?.name ?? "";
    ObjectFunctions f = new(name, args, zdo);
    var info = InfoSelector.SelectWeighted(type, zdo, args, f);
    var infos = InfoSelector.SelectSeparate(type, zdo, args, f);
    if (info == null && infos == null)
      info = InfoSelector.SelectFallback(type, zdo, args, f);
    if (info == null && infos == null) return false;
    if (info?.Cancel?.GetBool(f) == true)
      return true;
    if (infos != null)
      return infos.Any(i => i.Cancel?.GetBool(f) == true);
    return false;
  }
  private static void HandleSpawns(Info info, ZDO zdo, Functions f, bool remove, bool regenerate, DataEntry? customData)
  {
    // Original object must be regenerated to apply data.
    var regenerateOriginal = !remove && regenerate;

    var weightedSpawn = info.GetWeightedSpawn(f);
    if (weightedSpawn != null)
      DelayedSpawn.Add(weightedSpawn, zdo, customData, f);
    if (info.Spawns != null)
      foreach (var p in info.Spawns)
        DelayedSpawn.Add(p, zdo, customData, f);

    var weightedSwap = info.GetWeightedSwap(f);
    if (info.Swaps == null && info.WeightedSwaps == null && !regenerateOriginal) return;
    var data = DataHelper.Merge(new DataEntry(zdo), customData);
    if (weightedSwap != null)
      DelayedSpawn.Add(weightedSwap, zdo, data, f);
    if (info.Swaps != null)
      foreach (var p in info.Swaps)
        DelayedSpawn.Add(p, zdo, data, f);
    if (regenerateOriginal)
    {
      var removeItems = info.RemoveItems;
      var addItems = info.AddItems;
      ZdoEntry entry = new(zdo);
      if (data != null)
        entry.Load(data, f);
      var attach = info.Attach?.Get(f);
      if (attach.HasValue)
        SupportAttach.Attach(entry, attach.Value);
      var connect = info.Connect?.Get(f);
      if (connect.HasValue)
        SupportAttach.Connect(entry, connect.Value);
      var newZdo = DelayedSpawn.CreateObject(entry, false);
      if (newZdo != null)
      {
        removeItems?.RemoveItems(f, newZdo);
        addItems?.AddItems(f, newZdo);
        PrefabConnector.AddSwap(zdo.m_uid, newZdo.m_uid);
      }
    }
  }
  public static void RemoveZDO(ZDOID id, bool triggerRules)
  {
    if (!triggerRules)
      ZDOMan.instance.m_deadZDOs[id] = ZNet.instance.GetTime().Ticks;
    var zdo = ZDOMan.instance.GetZDO(id);
    if (zdo == null) return;
    zdo.SetOwnerInternal(ZDOMan.instance.m_sessionID);
    ZDOMan.instance.DestroyZDO(zdo);
  }


  public static void SpawnDrops(ZDO zdo)
  {
    if (ZNetScene.instance.m_instances.ContainsKey(zdo))
    {
      SpawnDrops(zdo, ZNetScene.instance.m_instances[zdo].gameObject);
    }
    else
    {
      var obj = ZNetScene.instance.CreateObject(zdo);
      obj.GetComponent<ZNetView>().m_ghost = true;
      ZNetScene.instance.m_instances.Remove(zdo);
      SpawnDrops(zdo, obj);
      UnityEngine.Object.Destroy(obj);
    }
  }
  private static void SpawnDrops(ZDO source, GameObject obj)
  {
    HandleCreated.Skip = true;
    if (obj.TryGetComponent<DropOnDestroyed>(out var drop))
    {
      drop.OnDestroyed();
    }
    if (obj.TryGetComponent<CharacterDrop>(out var characterDrop))
    {
      characterDrop.m_character = obj.GetComponent<Character>();
      if (characterDrop.m_character)
        characterDrop.OnDeath();
    }
    if (obj.TryGetComponent<Ragdoll>(out var ragdoll))
      ragdoll.SpawnLoot(ragdoll.GetAverageBodyPosition());
    if (obj.TryGetComponent<Piece>(out var piece))
    {
      if (obj.TryGetComponent<Plant>(out var _))
      {
        foreach (Piece.Requirement requirement in piece.m_resources)
          requirement.m_recover = true;
      }
      piece.DropResources();
    }
    if (obj.TryGetComponent<TreeBase>(out var tree))
    {
      var items = tree.m_dropWhenDestroyed.GetDropList();
      foreach (var item in items)
        CreateDrop(source, item);
    }
    if (obj.TryGetComponent<TreeLog>(out var log))
    {
      var items = log.m_dropWhenDestroyed.GetDropList();
      foreach (var item in items)
        CreateDrop(source, item);
    }
    HandleCreated.Skip = false;
  }

  public static void CreateDrop(ZDO source, GameObject item)
  {
    var hash = item.name.GetStableHashCode();
    var zdo = ZdoEntry.Spawn(hash, item.transform.position, Vector3.zero, source.GetOwner());
    if (zdo == null) return;
  }
  public static void SpawnItems(string dataName, ZDO zdo, Functions f)
  {
    var data = DataHelper.Get(dataName);
    if (data == null) return;
    var items = data.GenerateItems(f, new(10000, 10000));
    HandleCreated.Skip = true;
    foreach (var item in items)
      item.Spawn(zdo, f);
    HandleCreated.Skip = false;
  }
  public static void Poke(Info info, ZDO zdo, Functions f)
  {
    var pos = zdo.m_position;
    var rot = zdo.GetRotation();
    if (info.LegacyPokes != null)
    {
      var zdos = ObjectsFiltering.GetNearby(info.PokeLimit, info.LegacyPokes, pos, rot, f, zdo.m_uid, false);
      var pokeParameter = Prefab.Poke.PokeEvaluate(f.Replace(info.PokeParameter)).Split(' ');
      var delay = info.PokeDelay;
      DelayedPoke.Add(delay, zdos, pokeParameter);
    }
    var weightedPoke = info.GetWeightedPoke(f);
    if (weightedPoke != null)
      DelayedPoke.Add(weightedPoke, zdo.m_uid, pos, rot, f);
    if (info.Pokes == null) return;
    foreach (var poke in info.Pokes)
      DelayedPoke.Add(poke, zdo.m_uid, pos, rot, f);
  }

  public static void PokeGlobal(Info info, Functions f, Vector3 pos)
  {
    if (info.LegacyPokes != null)
    {
      var zdos = ObjectsFiltering.GetNearby(info.PokeLimit, info.LegacyPokes, pos, Quaternion.identity, f, null, false);
      var pokeParameter = Prefab.Poke.PokeEvaluate(f.Replace(info.PokeParameter));
      var delay = info.PokeDelay;
      DelayedPoke.Add(delay, zdos, pokeParameter.Split(' '));
    }
    var weightedPoke = info.GetWeightedPoke(f);
    if (weightedPoke != null)
      DelayedPoke.AddGlobal(weightedPoke, pos, Quaternion.identity, f);
    if (info.Pokes == null) return;
    foreach (var poke in info.Pokes)
      DelayedPoke.AddGlobal(poke, pos, Quaternion.identity, f);
  }
  public static void Terrain(Info info, ZDO zdo, Functions f)
  {
    if (info.Terrains == null) return;
    var pos = zdo.m_position;
    var rot = Quaternion.Euler(zdo.m_rotation);
    var source = zdo.GetOwner();
    foreach (var terrain in info.Terrains)
    {
      var delay = terrain.Delay?.Get(f) ?? 0f;
      terrain.Get(f, pos, rot, out var p, out var s, out var resetRadius, out var pkg);
      DelayedTerrain.Add(delay, source, p, s, pkg, resetRadius);
    }
  }

  public static void ObjectRpc(ObjectRpcInfo[] info, ZDO zdo, Functions f)
  {
    foreach (var i in info)
      i.Invoke(zdo, f);
  }
  public static void ClientRpc(ClientRpcInfo[] info, ZDO zdo, Functions f)
  {
    foreach (var i in info)
      i.Invoke(zdo, f);
  }
  public static void GlobalClientRpc(ClientRpcInfo[] info, Functions f)
  {
    foreach (var i in info)
      i.InvokeGlobal(f);
  }
  public static void ModifyTerrain(long source, Vector3 pos, float radius, ZPackage pkg, float resetRadius)
  {
    // Terrain may have to be modified in multiple zones.
    var corner1 = pos + new Vector3(radius, 0, radius);
    var corner2 = pos + new Vector3(-radius, 0, -radius);
    var corner3 = pos + new Vector3(-radius, 0, radius);
    var corner4 = pos + new Vector3(radius, 0, -radius);
    var zone1 = ZoneSystem.GetZone(corner1);
    var zone2 = ZoneSystem.GetZone(corner2);
    var zone3 = ZoneSystem.GetZone(corner3);
    var zone4 = ZoneSystem.GetZone(corner4);
    var startI = Mathf.Min(zone1.x, zone2.x, zone3.x, zone4.x);
    var endI = Mathf.Max(zone1.x, zone2.x, zone3.x, zone4.x);
    var startJ = Mathf.Min(zone1.y, zone2.y, zone3.y, zone4.y);
    var endJ = Mathf.Max(zone1.y, zone2.y, zone3.y, zone4.y);

    for (var i = startI; i <= endI; i++)
    {
      for (var j = startJ; j <= endJ; j++)
      {
        var zone = new Vector2i(i, j);
        if (!ZoneSystem.instance.IsZoneGenerated(zone)) continue;
        ModifyZoneTerrain(source, pos, zone, pkg, resetRadius);
      }
    }
  }
  private static readonly int TerrainActionHash = "ApplyOperation".GetStableHashCode();
  private static void ModifyZoneTerrain(long source, Vector3 pos, Vector2i zone, ZPackage pkg, float resetRadius)
  {
    var compiler = FindTerrainCompiler(zone);
    if (compiler != null && compiler.HasOwner())
    {
      if (resetRadius > 0f)
        ResetTerrainInZdo(pos, resetRadius, zone, compiler);
      else
        Rpc(source, compiler.GetOwner(), compiler.m_uid, TerrainActionHash, [pkg]);
    }
    // Compiler should be already there.
  }

  public static bool GenerateTerrainCompilers(long source, Vector3 pos, float radius)
  {
    // Terrain may have to be modified in multiple zones.
    var corner1 = pos + new Vector3(radius, 0, radius);
    var corner2 = pos + new Vector3(-radius, 0, -radius);
    var corner3 = pos + new Vector3(-radius, 0, radius);
    var corner4 = pos + new Vector3(radius, 0, -radius);
    var zone1 = ZoneSystem.GetZone(corner1);
    var zone2 = ZoneSystem.GetZone(corner2);
    var zone3 = ZoneSystem.GetZone(corner3);
    var zone4 = ZoneSystem.GetZone(corner4);
    var startI = Mathf.Min(zone1.x, zone2.x, zone3.x, zone4.x);
    var endI = Mathf.Max(zone1.x, zone2.x, zone3.x, zone4.x);
    var startJ = Mathf.Min(zone1.y, zone2.y, zone3.y, zone4.y);
    var endJ = Mathf.Max(zone1.y, zone2.y, zone3.y, zone4.y);

    var created = false;
    for (var i = startI; i <= endI; i++)
    {
      for (var j = startJ; j <= endJ; j++)
      {
        var zone = new Vector2i(i, j);
        if (!ZoneSystem.instance.IsZoneGenerated(zone)) continue;
        created |= GenerateZoneTerrainCompiler(source, zone);
      }
    }
    return created;
  }
  private static void ResetTerrainInZdo(Vector3 pos, float radius, Vector2i zone, ZDO zdo)
  {
    var byteArray = zdo.GetByteArray(ZDOVars.s_TCData);
    if (byteArray == null) return;
    var center = ZoneSystem.GetZonePos(zone);
    var change = false;
    var from = new ZPackage(Utils.Decompress(byteArray));
    var to = new ZPackage();
    to.Write(from.ReadInt());
    to.Write(from.ReadInt() + 1);
    from.ReadVector3();
    to.Write(center);
    from.ReadSingle();
    to.Write(radius);
    var size = from.ReadInt();
    to.Write(size);
    var width = (int)Math.Sqrt(size);
    for (int index = 0; index < size; index++)
    {
      var wasModified = from.ReadBool();
      var modified = wasModified;
      var j = index / width;
      var i = index % width;
      if (j >= 0 && j <= width - 1 && i >= 0 && i <= width - 1)
      {
        var worldPos = VertexToWorld(center, j, i);
        if (Utils.DistanceXZ(worldPos, pos) < radius)
          modified = false;
      }
      to.Write(modified);
      if (modified)
      {
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
      }
      if (wasModified && !modified)
      {
        change = true;
        from.ReadSingle();
        from.ReadSingle();
      }
    }
    size = from.ReadInt();
    to.Write(size);
    for (int index = 0; index < size; index++)
    {
      var wasModified = from.ReadBool();
      var modified = wasModified;
      var j = index / width;
      var i = index % width;
      var worldPos = VertexToWorld(center, j, i);
      if (Utils.DistanceXZ(worldPos, pos) < radius)
        modified = false;
      to.Write(modified);
      if (modified)
      {
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
      }
      if (wasModified && !modified)
      {
        change = true;
        from.ReadSingle();
        from.ReadSingle();
        from.ReadSingle();
        from.ReadSingle();
      }
    }
    if (!change) return;
    var bytes = Utils.Compress(to.GetArray());
    zdo.DataRevision += 100;
    zdo.Set(ZDOVars.s_TCData, bytes);
  }
  private static Vector3 VertexToWorld(Vector3 pos, int j, int i)
  {
    pos.x += i - 32.5f;
    pos.z += j - 32.5f;
    return pos;
  }
  private static readonly int TerrainCompilerHash = "_TerrainCompiler".GetStableHashCode();
  private static bool GenerateZoneTerrainCompiler(long source, Vector2i zone)
  {
    var compiler = FindTerrainCompiler(zone);
    if (compiler != null && compiler.HasOwner())
      return false;
    if (compiler == null)
    {
      var zdo = ZDOMan.instance.CreateNewZDO(ZoneSystem.GetZonePos(zone), TerrainCompilerHash);
      var view = ZNetScene.instance.GetPrefab(TerrainCompilerHash).GetComponent<ZNetView>();
      zdo.m_prefab = TerrainCompilerHash;
      zdo.Persistent = view.m_persistent;
      zdo.Type = view.m_type;
      zdo.Distant = view.m_distant;
      zdo.SetOwnerInternal(source);
    }
    return true;
  }
  // Terrain operations requires a terrain compiler in the zone.
  // These are only created when needed, so it might have to be added.
  private static ZDO? FindTerrainCompiler(Vector2i zone)
  {
    var index = ZDOMan.instance.SectorToIndex(zone);
    var zdos = index < 0 || index >= ZDOMan.instance.m_objectsBySector.Length
      ? ZDOMan.instance.m_objectsByOutsideSector.TryGetValue(zone, out var list) ? list : null
      : ZDOMan.instance.m_objectsBySector[index];
    return zdos?.FirstOrDefault(z => z.m_prefab == TerrainCompilerHash);
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
