using System.Collections.Generic;
using System.Linq;
using ExpandWorld.Prefab;
using Service;
using UnityEngine;

namespace Data;

// Needed for delayed spawns since the original ZDO might be already destroyed.
public class ZdoEntry : ResolvedDataEntry
{
  public ZdoEntry(int prefab, Vector3 position, Vector3 rotation, long owner)
  {
    Prefab = prefab;
    Position = position;
    Rotation = rotation;
    Owner = owner;
  }
  public ZdoEntry(ZDO zdo) : this(zdo.m_prefab, zdo.m_position, zdo.m_rotation, zdo.GetOwner()) { }

  public int Prefab;
  public long Owner;
  public Dictionary<int, string>? ServerStrings;
  public Dictionary<int, float>? ServerFloats;
  public Dictionary<int, int>? ServerInts;
  public Dictionary<int, long>? ServerLongs;
  public Dictionary<int, Vector3>? ServerVecs;
  public Dictionary<int, Quaternion>? ServerQuats;
  public Dictionary<int, byte[]>? ServerByteArrays;

  public void LoadItem(DataEntry data, Functions f)
  {
    if (data.Item == null) return;
    var itemData = data.Item.Create(f, Prefab);
    if (itemData != null)
      AddByteArray(ZDOVars.s_itemData, itemData);
    else if (WarnedItems.Add(Prefab))
      Log.Warning($"Prefab {ZNetScene.instance.GetPrefab(Prefab).name} has no ItemDrop, so item data is not created.");
  }
  private static readonly HashSet<int> WarnedItems = [];

  private static readonly int WidthHash = ZdoHelper.Hash("Container.m_width");
  private static readonly int HeightHash = ZdoHelper.Hash("Container.m_height");
  public void LoadInventory(DataEntry data, Functions f)
  {
    if (data.Items == null || data.Items.Count == 0) return;
    var prefab = ZNetScene.instance.GetPrefab(Prefab);
    if (!prefab) return;
    var container = prefab.GetComponentInChildren<Container>();
    var width = Ints != null && Ints.TryGetValue(WidthHash, out var w) ? w : container ? container.m_width : 4;
    var height = Ints != null && Ints.TryGetValue(HeightHash, out var h) ? h : container ? container.m_height : 2;
    var size = data.ContainerSize ?? new Vector2i(width, height);
    var inventory = ItemValue.LoadItemBytes(f, data.Items, size, data.ItemAmount?.Get(f) ?? 0);
    AddByteArray(ZDOVars.s_items, inventory);
  }

  public ZDO? Create()
  {
    var zdo = SpawnZDO(Prefab, Position ?? Vector3.zero, Rotation ?? Vector3.zero);
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
    if (prefab == PlayerHash && Config.PersistPlayers)
      zdo.Persistent = true;
    else
      zdo.Persistent = view.m_persistent;
    zdo.Distant = view.m_distant;
    zdo.Type = view.m_type;
    return zdo;
  }

  public override void Write(ZDO zdo)
  {
    base.Write(zdo);
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

  protected override void AddString(int key, string value)
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
  protected override void AddFloat(int key, float value)
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
  protected override void AddInt(int key, int value)
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
  protected override void AddLong(int key, long value)
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
  protected override void AddVec(int key, Vector3 value)
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
  protected override void AddQuat(int key, Quaternion value)
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
  protected override void AddByteArray(int key, byte[] value)
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
}
