using System;
using System.Collections.Generic;

namespace Data;

// EWP only additions.
// - Directly adding and removing can be moved over if needed.
// - Server side data can't be shared as EWP only has the data.
public partial class DataEntry
{
  public void AddItems(Functions f, ZDO zdo)
  {
    if (Items == null || Items.Count == 0) return;
    var size = ContainerSize ?? ZdoHelper.GetInventorySize(this, f, zdo);
    var records = Service.ItemDataHelper.Load(zdo);
    var items = GenerateItems(f, size);
    foreach (var item in items)
      item.AddTo(f, records, size);
    Service.ItemDataHelper.SaveTo(zdo, records);
  }
  public void RemoveItems(Functions f, ZDO zdo)
  {
    if (Items == null || Items.Count == 0) return;
    var records = Service.ItemDataHelper.Load(zdo);
    if (records.Count == 0) return;

    var items = GenerateItems(f, new(10000, 10000));
    foreach (var item in items)
      item.RemoveFrom(f, records);
    Service.ItemDataHelper.SaveTo(zdo, records);
  }
  public List<ItemValue> GenerateItems(Functions f, Vector2i size)
  {
    if (Items == null) throw new ArgumentNullException(nameof(Items));
    return ItemValue.Generate(f, Items, size, ItemAmount?.Get(f) ?? 0);
  }

  // Server side values are stored outside of ZDOExtraData.
  public void LoadServerData(ZDO zdo)
  {
    var id = zdo.m_uid;
    if (ExpandWorld.Prefab.ServerSideData.TryGetFloats(id, out var serverFloats))
    {
      Floats ??= [];
      foreach (var pair in serverFloats)
        Floats[pair.Key] = DataValue.Simple(pair.Value);
    }
    if (ExpandWorld.Prefab.ServerSideData.TryGetInts(id, out var serverInts))
    {
      Ints ??= [];
      foreach (var pair in serverInts)
        Ints[pair.Key] = DataValue.Simple(pair.Value);
    }
    if (ExpandWorld.Prefab.ServerSideData.TryGetLongs(id, out var serverLongs))
    {
      Longs ??= [];
      foreach (var pair in serverLongs)
        Longs[pair.Key] = DataValue.Simple(pair.Value);
    }
    if (ExpandWorld.Prefab.ServerSideData.TryGetStrings(id, out var serverStrings))
    {
      Strings ??= [];
      foreach (var pair in serverStrings)
        Strings[pair.Key] = DataValue.Simple(pair.Value);
    }
    if (ExpandWorld.Prefab.ServerSideData.TryGetVecs(id, out var serverVecs))
    {
      Vecs ??= [];
      foreach (var pair in serverVecs)
        Vecs[pair.Key] = DataValue.Simple(pair.Value);
    }
    if (ExpandWorld.Prefab.ServerSideData.TryGetQuaternions(id, out var serverQuats))
    {
      Quats ??= [];
      foreach (var pair in serverQuats)
        Quats[pair.Key] = DataValue.Simple(pair.Value);
    }
    if (ExpandWorld.Prefab.ServerSideData.TryGetBytes(id, out var serverBytes))
    {
      ByteArrays ??= [];
      foreach (var pair in serverBytes)
        ByteArrays[pair.Key] = DataValue.Simple(pair.Value);
    }
  }
}
