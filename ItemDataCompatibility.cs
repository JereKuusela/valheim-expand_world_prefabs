using System.Collections.Generic;
using Data;
using UnityEngine;

namespace ExpandWorld.Prefab;

internal static class ItemDataCompatibility
{
  // A nonzero ID is required for the crafter name to be visible.
  internal const long SyntheticCrafterId = 1L;
  internal static readonly int DurabilityHash = ZDOVars.s_durability;
  internal static readonly int StackHash = ZDOVars.s_stack;
  internal static readonly int QualityHash = ZDOVars.s_quality;
  internal static readonly int VariantHash = ZDOVars.s_variant;
  internal static readonly int CrafterIdHash = ZDOVars.s_crafterID;
  internal static readonly int CrafterNameHash = ZDOVars.s_crafterName;
  internal static readonly int WorldLevelHash = ZDOVars.s_worldLevel;
  internal static readonly int PickedUpHash = ZDOVars.s_pickedUp;

  internal static bool TryLoad(ZDO zdo, out ItemDrop.ItemData itemData)
  {
    itemData = new ItemDrop.ItemData();
    var packed = zdo.GetByteArray(ZDOVars.s_itemData);
    if (packed == null || packed.Length <= 2) return false;
    ItemDrop.LoadFromZDO(itemData, zdo);
    var prefab = ZNetScene.instance == null ? null : ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    if (prefab != null)
    {
      itemData.m_dropPrefab = prefab;
      var drop = prefab.GetComponent<ItemDrop>();
      if (drop != null)
        itemData.m_shared = drop.m_itemData.m_shared;
    }
    return true;
  }

  internal static ItemDrop.ItemData Create(
    GameObject prefab,
    int stack,
    float? durability,
    int quality,
    int variant,
    long crafterId,
    string crafterName,
    int worldLevel,
    bool pickedUp,
    bool equipped,
    Dictionary<string, string>? customData)
  {
    var drop = prefab.GetComponent<ItemDrop>();
    var itemData = drop.m_itemData.Clone();
    itemData.m_dropPrefab = prefab;
    itemData.m_stack = stack;
    itemData.m_quality = quality;
    itemData.m_variant = variant;
    itemData.m_crafterID = EnsureVisibleCrafterId(crafterId, crafterName);
    itemData.m_crafterName = crafterName;
    itemData.m_worldLevel = worldLevel;
    itemData.m_durability = durability ?? itemData.GetMaxDurability(quality);
    itemData.m_equipped = equipped;
    itemData.m_pickedUp = pickedUp;
    if (customData != null)
      itemData.m_customData = new Dictionary<string, string>(customData);
    return itemData;
  }

  internal static void ApplyLegacyOverrides(ZDO zdo)
  {
    if (ZNetScene.instance == null) return;
    var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    var drop = prefab == null ? null : prefab.GetComponent<ItemDrop>();
    if (drop == null) return;

    var hasOverride = HasFloat(zdo, DurabilityHash)
      || HasInt(zdo, StackHash)
      || HasInt(zdo, QualityHash)
      || HasInt(zdo, VariantHash)
      || HasLong(zdo, CrafterIdHash)
      || HasString(zdo, CrafterNameHash)
      || HasInt(zdo, WorldLevelHash)
      || HasInt(zdo, PickedUpHash);
    if (!hasOverride) return;

    var itemData = TryLoad(zdo, out var packed) ? packed : drop.m_itemData.Clone();
    itemData.m_dropPrefab = prefab;
    itemData.m_shared = drop.m_itemData.m_shared;
    if (TryFloat(zdo, DurabilityHash, out var durability)) itemData.m_durability = durability;
    if (TryInt(zdo, StackHash, out var stack)) itemData.m_stack = stack;
    if (TryInt(zdo, QualityHash, out var quality)) itemData.m_quality = quality;
    if (TryInt(zdo, VariantHash, out var variant)) itemData.m_variant = variant;
    if (TryLong(zdo, CrafterIdHash, out var crafterId)) itemData.m_crafterID = crafterId;
    if (TryString(zdo, CrafterNameHash, out var crafterName)) itemData.m_crafterName = crafterName;
    itemData.m_crafterID = EnsureVisibleCrafterId(itemData.m_crafterID, itemData.m_crafterName);
    if (TryInt(zdo, WorldLevelHash, out var worldLevel)) itemData.m_worldLevel = worldLevel;
    if (TryInt(zdo, PickedUpHash, out var pickedUp)) itemData.m_pickedUp = pickedUp != 0;
    ItemDrop.SaveToZDO(itemData, zdo);
    RemoveLegacyFields(zdo);
  }

  internal static bool TryGetString(ZDO zdo, int hash, out string value)
  {
    value = "";
    if (hash != CrafterNameHash || !TryLoad(zdo, out var itemData)) return false;
    value = itemData.m_crafterName;
    return true;
  }

  internal static bool TryGetFloat(ZDO zdo, int hash, out float value)
  {
    value = 0f;
    if (hash != DurabilityHash || !TryLoad(zdo, out var itemData)) return false;
    value = itemData.m_durability;
    return true;
  }

  internal static bool TryGetInt(ZDO zdo, int hash, out int value)
  {
    value = 0;
    if (!TryLoad(zdo, out var itemData)) return false;
    if (hash == StackHash) value = itemData.m_stack;
    else if (hash == QualityHash) value = itemData.m_quality;
    else if (hash == VariantHash) value = itemData.m_variant;
    else if (hash == WorldLevelHash) value = itemData.m_worldLevel;
    else if (hash == PickedUpHash) value = itemData.m_pickedUp ? 1 : 0;
    else return false;
    return true;
  }

  internal static bool TryGetLong(ZDO zdo, int hash, out long value)
  {
    value = 0L;
    if (hash != CrafterIdHash || !TryLoad(zdo, out var itemData)) return false;
    value = itemData.m_crafterID;
    return true;
  }

  internal static long EnsureVisibleCrafterId(long crafterId, string crafterName)
    => crafterId == 0L && !string.IsNullOrWhiteSpace(crafterName)
      ? SyntheticCrafterId
      : crafterId;

  private static bool HasString(ZDO zdo, int hash) => ZDOExtraData.s_strings.TryGetValue(zdo.m_uid, out var values) && values.ContainsKey(hash);
  private static bool HasFloat(ZDO zdo, int hash) => ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var values) && values.ContainsKey(hash);
  private static bool HasInt(ZDO zdo, int hash) => ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var values) && values.ContainsKey(hash);
  private static bool HasLong(ZDO zdo, int hash) => ZDOExtraData.s_longs.TryGetValue(zdo.m_uid, out var values) && values.ContainsKey(hash);
  private static bool TryString(ZDO zdo, int hash, out string value)
  {
    value = "";
    return ZDOExtraData.s_strings.TryGetValue(zdo.m_uid, out var values) && values.TryGetValue(hash, out value);
  }
  private static bool TryFloat(ZDO zdo, int hash, out float value)
  {
    value = 0f;
    return ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var values) && values.TryGetValue(hash, out value);
  }
  private static bool TryInt(ZDO zdo, int hash, out int value)
  {
    value = 0;
    return ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var values) && values.TryGetValue(hash, out value);
  }
  private static bool TryLong(ZDO zdo, int hash, out long value)
  {
    value = 0L;
    return ZDOExtraData.s_longs.TryGetValue(zdo.m_uid, out var values) && values.TryGetValue(hash, out value);
  }

  private static void RemoveLegacyFields(ZDO zdo)
  {
    zdo.RemoveFloat(DurabilityHash);
    zdo.RemoveInt(StackHash);
    zdo.RemoveInt(QualityHash);
    zdo.RemoveInt(VariantHash);
    zdo.RemoveLong(CrafterIdHash);
    ZDOExtraData.s_strings.Remove(zdo.m_uid, CrafterNameHash);
    zdo.RemoveInt(WorldLevelHash);
    zdo.RemoveInt(PickedUpHash);
  }
}
