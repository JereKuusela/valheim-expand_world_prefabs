using System.Collections.Generic;
using System.Linq;
using Service;
using UnityEngine;

namespace Data;

// EWP only part: modifying existing inventories and spawning items as objects.
public partial class ItemValue
{
  public void Spawn(ZDO source, Functions f)
  {
    var prefab = ZNetScene.instance.GetPrefab(RolledPrefab);
    if (prefab == null)
    {
      Log.Error($"Can't spawn missing drop: {RolledPrefab}");
      return;
    }
    var pos = source.m_position;
    if (prefab.GetComponent<ItemDrop>())
    {
      var zdo = ZdoEntry.Spawn(RolledPrefab, pos, Vector3.zero, source.GetOwner());
      if (zdo == null) return;
      ItemDrop.SaveToZDO(CreateItemData(f, prefab), zdo);
    }
    else
    {
      for (var i = 0; i < RolledStack; ++i)
      {
        var zdo = ZdoEntry.Spawn(RolledPrefab, pos, Vector3.zero, source.GetOwner());
        if (zdo == null) return;
        if (prefab.GetComponent<Character>())
          zdo.Set(ZDOVars.s_level, Quality?.Get(f) ?? 1);
        if (CustomData != null)
        {
          foreach (var kvp in CustomData)
            LoadCustomData(zdo, f, kvp);
        }
      }
    }
  }

  private void LoadCustomData(ZDO zdo, Functions f, KeyValuePair<string, IStringValue> kvp)
  {
    if (kvp.Key == "data")
    {
      var data = DataHelper.Get(kvp.Value.Get(f) ?? "");
      if (data == null) return;
      ZdoEntry entry = new(zdo);
      entry.Load(data, f, zdo);
      entry.Write(zdo);
    }
  }
  public void AddTo(Functions f, List<ItemRecord> records, Vector2i size)
  {
    var stack = Stack?.Get(f) ?? 1;
    stack = StackTo(f, stack, records);
    InsertTo(f, stack, records, size);
  }
  private int StackTo(Functions f, int stack, List<ItemRecord> records)
  {
    foreach (var item in records)
    {
      if (!MatchItem(f, item)) continue;
      var amount = Mathf.Min(ItemDataHelper.GetMaxStackSize(item.PrefabHash) - item.Stack, stack);
      item.Stack += amount;
      stack -= amount;
      if (stack <= 0) break;
    }
    return stack;
  }
  private int InsertTo(Functions f, int stack, List<ItemRecord> records, Vector2i size)
  {
    while (stack > 0)
    {
      var prefab = Prefab.Get(f) ?? 0;
      var item = ObjectDB.instance.GetItemPrefab(prefab);
      if (item == null || !item.TryGetComponent(out ItemDrop drop)) return stack;
      var quality = Quality?.Get(f) ?? 1;
      var amount = Mathf.Min(drop.m_itemData.m_shared.m_maxStackSize, stack);
      stack -= amount;

      var record = new ItemRecord
      {
        PrefabHash = prefab,
        PrefabName = item.name,
        Stack = amount,
        Durability = Durability?.Get(f) ?? drop.m_itemData.GetMaxDurability(quality),
        Quality = quality,
        Variant = Variant?.Get(f) ?? 0,
        CrafterID = CrafterID?.Get(f) ?? 0L,
        CrafterName = CrafterName?.Get(f) ?? "",
        WorldLevel = WorldLevel?.Get(f) ?? 0,
        Equipped = Equipped?.GetBool(f) ?? false,
        PickedUp = PickedUp?.GetBool(f) ?? false,
        Cheated = Cheated?.GetBool(f) ?? false,
        CustomData = CustomData?.ToDictionary(x => x.Key, x => x.Value.Get(f) ?? "") ?? [],
      };

      if (Position == "")
      {
        var slot = ItemDataHelper.FindFreeSlot(records, size);
        if (slot == null) return stack;
        record.GridPos = slot.Value;
      }
      else
      {
        record.GridPos = RolledPosition;
        records.RemoveAll(x => x.GridPos == RolledPosition);
      }
      records.Add(record);
    }
    return stack;
  }
  public void RemoveFrom(Functions f, List<ItemRecord> records)
  {
    var stack = Stack?.Get(f) ?? 1;
    for (var i = records.Count - 1; i >= 0; --i)
    {
      var item = records[i];
      if (!MatchItem(f, item)) continue;
      var amount = Mathf.Min(item.Stack, stack);
      item.Stack -= amount;
      stack -= amount;
      if (stack <= 0) break;
    }
    records.RemoveAll(x => x.Stack <= 0);
  }
}
