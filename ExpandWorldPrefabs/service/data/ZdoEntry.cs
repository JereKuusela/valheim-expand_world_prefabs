using System.Collections.Generic;
using System.Linq;
using ExpandWorld.Prefab;
using Service;
using UnityEngine;

namespace Data;

// Replicates resolved ZDO data.
// This is needed for delayed spawns since the original ZDO might be already destroyed.
// This also helps to split code from DataEntry.
// Technically lowers performance because of extra value copying but this is negligible.
public class ZdoEntry(int Prefab, Vector3 Position, Vector3 rotation, ZDO zdo)
{
  // Nulls add more code but should be more performant.
  public Dictionary<int, string>? Strings;
  public Dictionary<int, string>? ServerStrings;
  public Dictionary<int, float>? Floats;
  public Dictionary<int, float>? ServerFloats;
  public Dictionary<int, int>? Ints;
  public Dictionary<int, int>? ServerInts;
  public Dictionary<int, long>? Longs;
  public Dictionary<int, long>? ServerLongs;
  public Dictionary<int, Vector3>? Vecs;
  public Dictionary<int, Vector3>? ServerVecs;
  public Dictionary<int, Quaternion>? Quats;
  public Dictionary<int, Quaternion>? ServerQuats;
  public Dictionary<int, byte[]>? ByteArrays;
  public Dictionary<int, byte[]>? ServerByteArrays;
  public ZDOExtraData.ConnectionType? ConnectionType;
  public int ConnectionHash = 0;
  public ZDOID? OriginalId;
  public ZDOID? TargetConnectionId;
  public Vector3 Rotation = rotation;
  public long Owner = zdo.GetOwner();
  public bool? Persistent;
  public bool? Distant;
  public ZDO.ObjectType? Type;

  public ZdoEntry(ZDO zdo) : this(zdo.m_prefab, zdo.m_position, zdo.m_rotation, zdo) { }

  public ZDO? Create()
  {
    var zdo = SpawnZDO(Prefab, Position, Rotation);
    if (zdo == null) return null;
    Write(zdo);
    RestoreScale.Check(zdo);
    DelayedOwner.Check(zdo, Owner);
    return zdo;
  }

  // Helper function to ensure everything is initialized correctly.
  // Normally this is done by ZNetView which is not available purely server side.
  public static ZDO? Spawn(int prefab, Vector3 position, Vector3 rotation, long owner)
  {
    var zdo = SpawnZDO(prefab, position, rotation);
    if (zdo == null) return null;
    RestoreScale.Check(zdo);
    DelayedOwner.Check(zdo, owner);
    return zdo;
  }

  private static readonly int PlayerHash = ZdoHelper.Hash("Player");
  private static ZDO? SpawnZDO(int prefab, Vector3 position, Vector3 rotation)
  {
    if (prefab == 0) return null;
    var prefabObj = ZNetScene.instance.GetPrefab(prefab);
    if (!prefabObj)
    {
      Log.Error($"Can't spawn missing prefab: {prefab}");
      return null;
    }
    // Prefab hash is used to check whether to trigger rules.
    var zdo = ZDOMan.instance.CreateNewZDO(position, prefab);
    var view = prefabObj.GetComponent<ZNetView>();
    zdo.m_prefab = prefab;
    zdo.m_rotation = rotation;
    // Usually players are non persistent but this way NPCs can be spawned without having to manually set persistent in the data.
    if (prefab == PlayerHash && ExpandWorld.Prefab.Settings.PersistPlayers)
      zdo.Persistent = true;
    else
      zdo.Persistent = view.m_persistent;
    zdo.Distant = view.m_distant;
    zdo.Type = view.m_type;
    return zdo;
  }


  public void Load(DataEntry data, Parameters pars)
  {
    data.RollItems(pars, zdo);
    if (data.Floats?.Count > 0)
    {
      foreach (var pair in data.Floats)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          AddFloat(pair.Key, value.Value);
      }
    }
    if (data.Ints?.Count > 0)
    {
      foreach (var pair in data.Ints)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          AddInt(pair.Key, value.Value);
      }
    }
    if (data.Longs?.Count > 0)
    {
      foreach (var pair in data.Longs)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          AddLong(pair.Key, value.Value);
      }
    }
    if (data.Strings?.Count > 0)
    {
      foreach (var pair in data.Strings)
      {
        var value = pair.Value.Get(pars);
        if (value != null)
          AddString(pair.Key, value);
      }
    }
    if (data.Vecs?.Count > 0)
    {
      foreach (var pair in data.Vecs)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          AddVec(pair.Key, value.Value);
      }
    }
    if (data.Quats?.Count > 0)
    {
      foreach (var pair in data.Quats)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          AddQuat(pair.Key, value.Value);
      }
    }
    if (data.ByteArrays?.Count > 0)
    {
      foreach (var pair in data.ByteArrays)
      {
        var value = pair.Value.Get(pars);
        if (value != null)
          AddByteArray(pair.Key, value);
      }
    }
    if (data.Bools?.Count > 0)
    {
      foreach (var pair in data.Bools)
      {
        var value = pair.Value.GetInt(pars);
        if (value.HasValue)
          AddInt(pair.Key, value.Value);
      }
    }
    if (data.Hashes?.Count > 0)
    {
      foreach (var pair in data.Hashes)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          AddInt(pair.Key, value.Value);
      }
    }
    if (data.Components != null)
    {
      foreach (var pair in data.Components)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          AddInt(pair.Key, value.Value);
      }
    }
    ConnectionHash = data.ConnectionHash;
    ConnectionType = data.ConnectionType;
    if (data.OriginalId != null)
      OriginalId = data.OriginalId.Get(pars);
    if (data.TargetConnectionId != null)
      TargetConnectionId = data.TargetConnectionId.Get(pars);
    Distant = data.Distant?.GetBool(pars);
    Persistent = data.Persistent?.GetBool(pars);
    Type = data.Priority;
    Position = data.Position?.Get(pars) ?? Position;
    Rotation = data.Rotation?.Get(pars)?.eulerAngles ?? Rotation;
  }

  public void Write(ZDO zdo)
  {
    var id = zdo.m_uid;
    if (Floats != null)
    {
      ZDOHelper.Init(ZDOExtraData.s_floats, id);
      foreach (var pair in Floats)
        zdo.Set(pair.Key, pair.Value);
    }
    if (Ints != null)
    {
      ZDOHelper.Init(ZDOExtraData.s_ints, id);
      foreach (var pair in Ints)
        zdo.Set(pair.Key, pair.Value);
    }
    if (Longs != null)
    {
      ZDOHelper.Init(ZDOExtraData.s_longs, id);
      foreach (var pair in Longs)
        zdo.Set(pair.Key, pair.Value);
    }
    if (Strings != null)
    {
      ZDOHelper.Init(ZDOExtraData.s_strings, id);
      foreach (var pair in Strings)
        zdo.Set(pair.Key, pair.Value);
    }
    if (Vecs != null)
    {
      ZDOHelper.Init(ZDOExtraData.s_vec3, id);
      foreach (var pair in Vecs)
        zdo.Set(pair.Key, pair.Value);
    }
    if (Quats != null)
    {
      ZDOHelper.Init(ZDOExtraData.s_quats, id);
      foreach (var pair in Quats)
        zdo.Set(pair.Key, pair.Value);
    }
    if (ByteArrays != null)
    {
      ZDOHelper.Init(ZDOExtraData.s_byteArrays, id);
      foreach (var pair in ByteArrays)
        zdo.Set(pair.Key, pair.Value);
    }
    zdo.m_position = Position;
    zdo.SetSector(ZoneSystem.GetZone(Position));
    zdo.m_rotation = Rotation;
    if (Persistent.HasValue)
      zdo.Persistent = Persistent.Value;
    if (Distant.HasValue)
      zdo.Distant = Distant.Value;
    if (Type.HasValue)
      zdo.Type = Type.Value;
    HandleConnection(zdo);
    HandleHashConnection(zdo);
    WriteServer(zdo);
  }

  public void WriteServer(ZDO zdo)
  {
    if (ServerFloats != null)
      foreach (var pair in ServerFloats)
        ServerSideData.SetFloat(zdo, pair.Key, pair.Value);
    if (ServerInts != null)
      foreach (var pair in ServerInts)
        ServerSideData.SetInt(zdo, pair.Key, pair.Value);
    if (ServerLongs != null)
      foreach (var pair in ServerLongs)
        ServerSideData.SetLong(zdo, pair.Key, pair.Value);
    if (ServerStrings != null)
      foreach (var pair in ServerStrings)
        ServerSideData.SetString(zdo, pair.Key, pair.Value);
    if (ServerVecs != null)
      foreach (var pair in ServerVecs)
        ServerSideData.SetVec(zdo, pair.Key, pair.Value);
    if (ServerQuats != null)
      foreach (var pair in ServerQuats)
        ServerSideData.SetQuaternion(zdo, pair.Key, pair.Value);
    if (ServerByteArrays != null)
      foreach (var pair in ServerByteArrays)
        ServerSideData.SetBytes(zdo, pair.Key, pair.Value);
  }

  public bool HasSyncedChanges()
  {
    if (Floats?.Count > 0) return true;
    if (Ints?.Count > 0) return true;
    if (Longs?.Count > 0) return true;
    if (Strings?.Count > 0) return true;
    if (Vecs?.Count > 0) return true;
    if (Quats?.Count > 0) return true;
    if (ByteArrays?.Count > 0) return true;
    if (ConnectionType.HasValue) return true;
    if (ConnectionHash != 0) return true;
    if (OriginalId.HasValue) return true;
    if (TargetConnectionId.HasValue) return true;
    if (Persistent.HasValue) return true;
    if (Distant.HasValue) return true;
    if (Type.HasValue) return true;
    return false;
  }

  private void AddString(int key, string value)
  {
    if (ServerSideData.ShouldUse(key))
    {
      ServerStrings ??= [];
      ServerStrings[key] = value;
      return;
    }
    Strings ??= [];
    Strings[key] = value;
  }
  private void AddFloat(int key, float value)
  {
    if (ServerSideData.ShouldUse(key))
    {
      ServerFloats ??= [];
      ServerFloats[key] = value;
      return;
    }
    Floats ??= [];
    Floats[key] = value;
  }
  private void AddInt(int key, int value)
  {
    if (ServerSideData.ShouldUse(key))
    {
      ServerInts ??= [];
      ServerInts[key] = value;
      return;
    }
    Ints ??= [];
    Ints[key] = value;
  }
  private void AddLong(int key, long value)
  {
    if (ServerSideData.ShouldUse(key))
    {
      ServerLongs ??= [];
      ServerLongs[key] = value;
      return;
    }
    Longs ??= [];
    Longs[key] = value;
  }
  private void AddVec(int key, Vector3 value)
  {
    if (ServerSideData.ShouldUse(key))
    {
      ServerVecs ??= [];
      ServerVecs[key] = value;
      return;
    }
    Vecs ??= [];
    Vecs[key] = value;
  }
  private void AddQuat(int key, Quaternion value)
  {
    if (ServerSideData.ShouldUse(key))
    {
      ServerQuats ??= [];
      ServerQuats[key] = value;
      return;
    }
    Quats ??= [];
    Quats[key] = value;
  }
  private void AddByteArray(int key, byte[] value)
  {
    if (ServerSideData.ShouldUse(key))
    {
      ServerByteArrays ??= [];
      ServerByteArrays[key] = value;
      return;
    }
    ByteArrays ??= [];
    ByteArrays[key] = value;
  }

  private void HandleConnection(ZDO ownZdo)
  {
    if (ConnectionType == null) return;
    var ownId = ownZdo.m_uid;
    if (TargetConnectionId != null)
    {
      // If target is known, the setup is easy.
      var otherZdo = ZDOMan.instance.GetZDO(TargetConnectionId.Value);
      if (otherZdo == null) return;

      ownZdo.SetConnection(ConnectionType.Value, TargetConnectionId.Value);
      // Portal is two way.
      if (ConnectionType == ZDOExtraData.ConnectionType.Portal)
        otherZdo.SetConnection(ZDOExtraData.ConnectionType.Portal, ownId);

    }
    else
    {
      // Otherwise all zdos must be scanned.
      if (OriginalId == null) return;
      var other = ZDOExtraData.s_connections.FirstOrDefault(kvp => kvp.Value.m_target == OriginalId);
      if (other.Value == null) return;
      var otherZdo = ZDOMan.instance.GetZDO(other.Key);
      if (otherZdo == null) return;
      // Connection is always one way here, otherwise TargetConnectionId would be set.
      otherZdo.SetConnection(other.Value.m_type, ownId);
    }
  }
  private void HandleHashConnection(ZDO ownZdo)
  {
    if (ConnectionHash == 0) return;
    if (ConnectionType == null) return;
    var ownId = ownZdo.m_uid;

    // Hash data is regenerated on world save.
    // But in this case, it's manually set, so might be needed later.
    ZDOExtraData.SetConnectionData(ownId, ConnectionType.Value, ConnectionHash);

    // While actual connection can be one way, hash is always two way.
    // One of the hashes always has the target type.
    var otherType = ConnectionType.Value ^ ZDOExtraData.ConnectionType.Target;
    var isOtherTarget = (ConnectionType.Value & ZDOExtraData.ConnectionType.Target) == 0;
    var zdos = ZDOExtraData.GetAllConnectionZDOIDs(otherType);
    var otherId = zdos.FirstOrDefault(z => ZDOExtraData.GetConnectionHashData(z, ConnectionType.Value)?.m_hash == ConnectionHash);
    if (otherId == ZDOID.None) return;
    var otherZdo = ZDOMan.instance.GetZDO(otherId);
    if (otherZdo == null) return;
    if ((ConnectionType.Value & ZDOExtraData.ConnectionType.Spawned) > 0)
    {
      // Spawn is one way.
      var connZDO = isOtherTarget ? ownZdo : otherZdo;
      var targetId = isOtherTarget ? otherId : ownId;
      connZDO.SetConnection(ZDOExtraData.ConnectionType.Spawned, targetId);
    }
    if ((ConnectionType.Value & ZDOExtraData.ConnectionType.SyncTransform) > 0)
    {
      // Sync is one way.
      var connZDO = isOtherTarget ? ownZdo : otherZdo;
      var targetId = isOtherTarget ? otherId : ownId;
      connZDO.SetConnection(ZDOExtraData.ConnectionType.SyncTransform, targetId);
    }
    if ((ConnectionType.Value & ZDOExtraData.ConnectionType.Portal) > 0)
    {
      // Portal is two way.
      otherZdo.SetConnection(ZDOExtraData.ConnectionType.Portal, ownId);
      ownZdo.SetConnection(ZDOExtraData.ConnectionType.Portal, otherId);
    }
  }
}