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
  public Dictionary<int, float>? Floats;
  public Dictionary<int, int>? Ints;
  public Dictionary<int, long>? Longs;
  public Dictionary<int, Vector3>? Vecs;
  public Dictionary<int, Quaternion>? Quats;
  public Dictionary<int, byte[]>? ByteArrays;
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
    var zdo = Spawn(Prefab, Position, Rotation, Owner);
    if (zdo == null) return null;
    Write(zdo);
    return zdo;
  }

  // Helper function to ensure everything is initialized correctly.
  // Normally this is done by ZNetView which is not available purely server side.
  public static ZDO? Spawn(int prefab, Vector3 position, Vector3 rotation, long owner)
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
    zdo.Persistent = view.m_persistent;
    zdo.Distant = view.m_distant;
    zdo.Type = view.m_type;
    DelayedOwner.Check(zdo, owner);
    return zdo;
  }


  public void Load(DataEntry data, Parameters pars)
  {
    data.RollItems(pars, zdo);
    if (data.Floats?.Count > 0)
    {
      Floats ??= [];
      foreach (var pair in data.Floats)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          Floats[pair.Key] = value.Value;
      }
    }
    if (data.Ints?.Count > 0)
    {
      Ints ??= [];
      foreach (var pair in data.Ints)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          Ints[pair.Key] = value.Value;
      }
    }
    if (data.Longs?.Count > 0)
    {
      Longs ??= [];
      foreach (var pair in data.Longs)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          Longs[pair.Key] = value.Value;
      }
    }
    if (data.Strings?.Count > 0)
    {
      Strings ??= [];
      foreach (var pair in data.Strings)
      {
        var value = pair.Value.Get(pars);
        if (value != null)
          Strings[pair.Key] = value;
      }
    }
    if (data.Vecs?.Count > 0)
    {
      Vecs ??= [];
      foreach (var pair in data.Vecs)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          Vecs[pair.Key] = value.Value;
      }
    }
    if (data.Quats?.Count > 0)
    {
      Quats ??= [];
      foreach (var pair in data.Quats)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          Quats[pair.Key] = value.Value;
      }
    }
    if (data.ByteArrays?.Count > 0)
    {
      ByteArrays ??= [];
      foreach (var pair in data.ByteArrays)
      {
        var value = pair.Value.Get(pars);
        if (value != null)
          ByteArrays[pair.Key] = value;
      }
    }
    if (data.Bools?.Count > 0)
    {
      Ints ??= [];
      foreach (var pair in data.Bools)
      {
        var value = pair.Value.GetInt(pars);
        if (value.HasValue)
          Ints[pair.Key] = value.Value;
      }
    }
    if (data.Hashes?.Count > 0)
    {
      Ints ??= [];
      foreach (var pair in data.Hashes)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          Ints[pair.Key] = value.Value;
      }
    }
    if (data.Components != null)
    {
      Ints ??= [];
      foreach (var pair in data.Components)
      {
        var value = pair.Value.Get(pars);
        if (value.HasValue)
          Ints[pair.Key] = value.Value;
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
  }

  private void HandleConnection(ZDO ownZdo)
  {
    if (OriginalId == null) return;
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