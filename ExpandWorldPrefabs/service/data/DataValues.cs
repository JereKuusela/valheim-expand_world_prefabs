
using System.Collections.Generic;
using System.Linq;
using ExpandWorld.Prefab;
using Service;
using UnityEngine;

namespace Data;

public class DataValue
{

  public static IZdoIdValue ZdoId(string values)
  {
    var split = SplitWithValues(values);
    var zdo = Parse.ZdoId(split[0]);
    if (split.Length == 1 && zdo != ZDOID.None)
      return new SimpleZdoIdValue(zdo);
    return new ZdoIdValue(split);
  }
  // Different function name because string would be ambiguous.
  public static IIntValue Simple(int value) => new SimpleIntValue(value);
  public static IStringValue Simple(string value) => new SimpleStringValue(value);
  public static IFloatValue Simple(float value) => new SimpleFloatValue(value);
  public static ILongValue Simple(long value) => new SimpleLongValue(value);
  public static IVector3Value Simple(Vector3 value) => new SimpleVector3Value(value);
  public static IQuaternionValue Simple(Quaternion value) => new SimpleQuaternionValue(value);
  public static IBytesValue Simple(byte[]? value) => new SimpleBytesValue(value);

  public static IIntValue Int(string values)
  {
    var split = SplitWithValues(values);
    if (split.Length == 1 && int.TryParse(split[0], out var result))
      return new SimpleIntValue(result);
    return new IntValue(split);
  }

  public static IFloatValue Float(string values)
  {
    var split = SplitWithValues(values);
    if (split.Length == 1 && Parse.TryFloat(split[0], out var result))
      return new SimpleFloatValue(result);
    return new FloatValue(split);
  }

  public static ILongValue Long(string values)
  {
    var split = SplitWithValues(values);
    if (split.Length == 1 && long.TryParse(split[0], out var result))
      return new SimpleLongValue(result);
    return new LongValue(split);
  }

  public static IStringValue String(string values)
  {
    // Quick hack for quoted strings.
    if (values.Length > 2 && values[0] == '"' && values[values.Length - 1] == '"')
      return new SimpleStringValue(values.Substring(1, values.Length - 2));
    var split = SplitWithValues(values);
    if (split.Length == 1 && !HasParameters(split[0]))
      return new SimpleStringValue(split[0]);
    return new StringValue(split);
  }
  public static IBoolValue Bool(string values)
  {
    var split = SplitWithValues(values);
    if (split.Length == 1 && bool.TryParse(split[0], out var result))
      return new SimpleBoolValue(result);
    return new BoolValue(split);
  }

  public static IBytesValue Bytes(string values)
  {
    var split = SplitWithValues(values);
    if (split.Length == 1 && !HasParameters(split[0]))
    {
      if (string.IsNullOrEmpty(split[0]))
        return new SimpleBytesValue(null);
      try
      {
        var bytes = System.Convert.FromBase64String(split[0]);
        return new SimpleBytesValue(bytes);
      }
      catch (System.FormatException)
      {
        // If not valid base64, treat as a dynamic value
      }
    }
    return new BytesValue(split);
  }

  public static IHashValue Hash(string values)
  {
    var split = SplitWithValues(values);
    if (split.Length == 1 && !HasParameters(split[0]))
      return new SimpleHashValue(split[0]);
    return new HashValue(split);
  }
  public static IPrefabValue Prefab(string values)
  {
    if (HasParameters(values))
      return new PrefabValue(SplitWithValues(values));
    var prefabs = PrefabHelper.GetPrefabs(values, "");
    if (prefabs.Count == 0) return new SimplePrefabValue(null);
    if (prefabs.Count == 1) return new SimplePrefabValue(prefabs[0]);
    return new SimplePrefabsValue(prefabs);
  }

  public static IVector3Value Vector3(string values)
  {
    var split = SplitWithValues(values);
    if (HasParameters(values) || split.Length > 3)
    {
      // Vectors are trickly to handle because the coordinate separator is same as value separator.
      List<string> combined = [];
      for (var i = 0; i < split.Length; i += 3)
      {
        // Last vector can be partial.
        var v = i + 3 < split.Length ? split.Skip(i).Take(3).ToArray() : [.. split.Skip(i)];
        combined.Add(string.Join(",", v));
      }
      return new Vector3Value([.. combined]);
    }
    var parsed = Parse.VectorXZYNull(split);
    return new SimpleVector3Value(parsed.HasValue ? parsed.Value : UnityEngine.Vector3.zero);
  }
  public static IQuaternionValue Quaternion(string values)
  {
    var split = SplitWithValues(values);
    if (HasParameters(values) || split.Length > 3)
    {
      List<string> combined = [];
      for (var i = 0; i < split.Length; i += 3)
      {
        var v = i + 3 < split.Length ? split.Skip(i).Take(3).ToArray() : [.. split.Skip(i)];
        combined.Add(string.Join(",", v));
      }
      return new QuaternionValue([.. combined]);
    }
    var parsed = Parse.AngleYXZNull(split);
    return new SimpleQuaternionValue(parsed.HasValue ? parsed.Value : UnityEngine.Quaternion.identity);
  }

  private static bool HasParameters(string value) => value.Contains("<") && value.Contains(">");

  private static string[] SplitWithValues(string str)
  {
    List<string> result = [];
    var split = Parse.SplitWithEmpty(str);
    foreach (var value in split)
    {
      if (!value.Contains("<") || !value.Contains(">"))
      {
        result.Add(value);
        continue;
      }
      var parSplit = value.Split('<', '>');
      List<string> parameters = [];
      List<int> hashes = [];
      for (var i = 1; i < parSplit.Length; i += 2)
      {
        var hash = parSplit[i].ToLowerInvariant().GetStableHashCode();
        // Value groups should work same as using the value directly.
        // So it makes sense to resolve them on load.
        // This way other code doesn't have to worry about resolving them and performance is better.
        // Originally they were resolved later because World Edit Commands allows overriding them.
        // But this is not needed for EW Prefabs.
        if (DataLoading.ValueGroups.ContainsKey(hash))
        {
          parameters.Add($"<{parSplit[i]}>");
          hashes.Add(hash);
        }
      }
      if (parameters.Count == 0)
      {
        result.Add(value);
        // Early exit because preloading too many values wastes memory.
        if (result.Count > 1000)
          break;
      }
      else
        SubstitueValues(result, value, parameters, hashes, 0);

    }
    if (result.Count > 1000)
      Log.Warning("Too many values loaded for " + str);
    return result.Count > 1000 ? [str] : [.. result];
  }

  // Recursion needed because there can be multiple value groups.
  private static void SubstitueValues(List<string> result, string format, List<string> parameters, List<int> hashes, int index)
  {
    var groups = DataLoading.ValueGroups[hashes[index]];
    foreach (var group in groups)
    {
      var newFormat = format.Replace(parameters[index], group);
      if (index == parameters.Count - 1)
      {
        result.Add(newFormat);
        // Early exit because preloading too many values wastes memory.
        if (result.Count > 1000)
          break;
      }
      else
        SubstitueValues(result, newFormat, parameters, hashes, index + 1);
    }
  }
}


public class AnyValue(string[] values)
{
  protected readonly string[] Values = values;

  private string? RollValue()
  {
    if (Values.Length == 1)
      return Values[0];
    return Values[Random.Range(0, Values.Length)];
  }
  protected string? GetValue(Parameters pars)
  {
    var value = RollValue();
    if (value == null || value == "<none>")
      return null;
    return pars.Replace(value);
  }
  protected string? GetValue()
  {
    var value = RollValue();
    return value == null || value == "<none>" ? null : value;
  }
  protected List<string> GetAllValues(Parameters pars)
  {
    return [.. Values.Select(pars.Replace).Where(v => v != null && v != "" && v != "<none>")];
  }
  protected string GetWholeValue(Parameters pars)
  {
    return string.Join(",", Values.Select(pars.Replace));
  }
}
public class ItemValue(ItemData data)
{

  public static bool Match(Parameters pars, List<ItemValue> data, ZDO zdo, IIntValue? amount)
  {
    var inv = CreateInventory(zdo);
    var matches = data.Where(item => item.Match(pars, inv)).Count();
    // If no amount is set, then must match exactly.
    if (amount == null)
      return matches == data.Count && inv.m_inventory.Count == 0;
    return amount.Match(pars, matches) == true;
  }
  public static bool Match(Parameters pars, ZDO zdo, IIntValue amount)
  {
    var inv = CreateInventory(zdo);
    return amount.Match(pars, inv.m_inventory.Count) == true;
  }

  public static Inventory CreateInventory(ZDO zdo, int width = 100000, int height = 10000)
  {
    // Load only loads up to the inventory size, so the size must be large enough.
    var inv = new Inventory("", null, width, height);
    var str = zdo.GetString(ZDOVars.s_items);
    if (str != "")
    {
      ZPackage pkg = new(str);
      inv.Load(pkg);
    }
    return inv;
  }

  public static string LoadItems(Parameters pars, List<ItemValue> items, Vector2i size, int amount)
  {
    ZPackage pkg = new();
    pkg.Write(106);
    items = Generate(pars, items, size, amount);
    pkg.Write(items.Count);
    foreach (var item in items)
      item.Write(pars, pkg);
    return pkg.GetBase64();
  }
  public static List<ItemValue> Generate(Parameters pars, List<ItemValue> data, Vector2i size, int amount)
  {
    var fixedPos = data.Where(item => item.Position != "").ToList();
    var randomPos = data.Where(item => item.Position == "").ToList();
    Dictionary<Vector2i, ItemValue> inventory = [];
    foreach (var item in fixedPos)
    {
      if (!item.Roll(pars)) continue;
      inventory[item.RolledPosition] = item;
    }
    if (amount == 0)
      GenerateEach(pars, inventory, size, randomPos);
    else
      GenerateAmount(pars, inventory, size, randomPos, amount);
    return [.. inventory.Values];
  }
  private static void GenerateEach(Parameters pars, Dictionary<Vector2i, ItemValue> inventory, Vector2i size, List<ItemValue> items)
  {
    foreach (var item in items)
    {
      if (!item.Roll(pars)) continue;
      var slot = FindNextFreeSlot(inventory, size);
      if (!slot.HasValue) break;
      item.RolledPosition = slot.Value;
      inventory[slot.Value] = item;
    }
  }
  private static void GenerateAmount(Parameters pars, Dictionary<Vector2i, ItemValue> inventory, Vector2i size, List<ItemValue> items, int amount)
  {
    var maxWeight = items.Sum(item => item.Chance);
    for (var i = 0; i < amount && items.Count > 0; ++i)
    {
      var slot = FindNextFreeSlot(inventory, size);
      if (!slot.HasValue) break;
      var item = RollItem(items, maxWeight);
      item.RolledPosition = slot.Value;
      if (item.RollPrefab(pars))
        inventory[slot.Value] = item;
      maxWeight -= item.Chance;
      items.Remove(item);
    }
  }
  private static ItemValue RollItem(List<ItemValue> items, float maxWeight)
  {
    var roll = Random.Range(0f, maxWeight);
    foreach (var item in items)
    {
      if (roll < item.Chance)
        return item;
      roll -= item.Chance;
    }
    return items.Last();
  }
  private static Vector2i? FindNextFreeSlot(Dictionary<Vector2i, ItemValue> inventory, Vector2i size)
  {
    var maxW = size.x;
    var maxH = size.y;
    for (var y = 0; y < maxH; ++y)
      for (var x = 0; x < maxW; ++x)
      {
        var pos = new Vector2i(x, y);
        if (!inventory.ContainsKey(pos))
          return pos;
      }
    return null;
  }
  // Prefab is saved as string, so hash can't be used.
  public IPrefabValue Prefab = DataValue.Prefab(data.prefab);
  public float Chance = data.chance;
  public IIntValue? Stack = data.stack == null ? null : DataValue.Int(data.stack);
  public IFloatValue? Durability = data.durability == null ? null : DataValue.Float(data.durability);
  public string Position = data.pos;
  private Vector2i RolledPosition = Parse.Vector2Int(data.pos);
  public IBoolValue? Equipped = data.equipped == null ? null : DataValue.Bool(data.equipped);
  public IIntValue? Quality = data.quality == null ? null : DataValue.Int(data.quality);
  public IIntValue? Variant = data.variant == null ? null : DataValue.Int(data.variant);
  public ILongValue? CrafterID = data.crafterID == null ? null : DataValue.Long(data.crafterID);
  public IStringValue? CrafterName = data.crafterName == null ? null : DataValue.String(data.crafterName);
  public Dictionary<string, IStringValue>? CustomData = data.customData?.ToDictionary(kvp => kvp.Key, kvp => DataValue.String(kvp.Value));
  public IIntValue? WorldLevel = data.worldLevel == null ? null : DataValue.Int(data.worldLevel);
  public IBoolValue? PickedUp = data.pickedUp == null ? null : DataValue.Bool(data.pickedUp);
  // Must know before writing is the prefab good, so it has to be rolled first.
  private int RolledPrefab = 0;
  private int RolledStack = 0;
  public bool RollPrefab(Parameters pars)
  {
    RolledPrefab = Prefab.Get(pars) ?? 0;
    RolledStack = Stack?.Get(pars) ?? 1;
    return RolledPrefab != 0 && RolledStack != 0;
  }
  public bool RollChance() => Chance >= 1f || Random.value <= Chance;
  public bool Roll(Parameters pars) => RollChance() && RollPrefab(pars);
  public void Write(Parameters pars, ZPackage pkg)
  {
    var prefab = ObjectDB.instance.GetItemPrefab(RolledPrefab);
    pkg.Write(prefab?.name ?? "");
    var quality = Quality?.Get(pars) ?? 1;
    var durability = Durability?.Get(pars);
    if (!durability.HasValue)
    {
      if (prefab != null && prefab.TryGetComponent(out ItemDrop drop))
        durability = drop.m_itemData.GetMaxDurability(quality);
      else
        durability = 100f;
    }
    ;
    pkg.Write(RolledStack);
    pkg.Write(durability.Value);
    pkg.Write(RolledPosition);
    pkg.Write(Equipped?.GetBool(pars) ?? false);
    pkg.Write(quality);
    pkg.Write(Variant?.Get(pars) ?? 0);
    pkg.Write(CrafterID?.Get(pars) ?? 0L);
    pkg.Write(CrafterName?.Get(pars) ?? "");
    pkg.Write(CustomData?.Count ?? 0);
    if (CustomData != null)
    {
      foreach (var kvp in CustomData)
      {
        pkg.Write(kvp.Key);
        pkg.Write(kvp.Value.Get(pars));
      }
    }
    pkg.Write(WorldLevel?.Get(pars) ?? 0);
    pkg.Write(PickedUp?.GetBool(pars) ?? false);
  }
  public void Spawn(ZDO source, Parameters pars)
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
      zdo.Set(ZDOVars.s_durability, Durability?.Get(pars) ?? 100f);
      zdo.Set(ZDOVars.s_stack, RolledStack);
      zdo.Set(ZDOVars.s_quality, Quality?.Get(pars) ?? 1);
      zdo.Set(ZDOVars.s_variant, Variant?.Get(pars) ?? 0);
      zdo.Set(ZDOVars.s_crafterID, CrafterID?.Get(pars) ?? 0L);
      zdo.Set(ZDOVars.s_crafterName, CrafterName?.Get(pars) ?? "");
      zdo.Set(ZDOVars.s_dataCount, CustomData?.Count ?? 0);
      int num = 0;
      if (CustomData != null)
      {
        foreach (var kvp in CustomData)
        {
          zdo.Set(string.Format("data_{0}", num), kvp.Key);
          zdo.Set(string.Format("data__{0}", num), kvp.Value.Get(pars) ?? "");
          num += 1;
        }
      }
      zdo.Set(ZDOVars.s_worldLevel, WorldLevel?.Get(pars) ?? 0);
      zdo.Set(ZDOVars.s_pickedUp, PickedUp?.GetBool(pars) ?? false);
    }
    else
    {
      for (var i = 0; i < RolledStack; ++i)
      {
        var zdo = ZdoEntry.Spawn(RolledPrefab, pos, Vector3.zero, source.GetOwner());
        if (zdo == null) return;
        if (prefab.GetComponent<Character>())
          zdo.Set(ZDOVars.s_level, Quality?.Get(pars) ?? 1);
        if (CustomData != null)
        {
          foreach (var kvp in CustomData)
            LoadCustomData(zdo, pars, kvp);
        }
      }
    }
  }
  private void LoadCustomData(ZDO zdo, Parameters pars, KeyValuePair<string, IStringValue> kvp)
  {
    if (kvp.Key == "data")
    {
      var data = DataHelper.Get(kvp.Value.Get(pars) ?? "");
      if (data == null) return;
      ZdoEntry entry = new(zdo);
      entry.Load(data, pars);
      entry.Write(zdo);
    }
  }
  public void AddTo(Parameters pars, Inventory inv)
  {
    var stack = Stack?.Get(pars) ?? 1;
    stack = StackTo(pars, stack, inv);
    InsertTo(pars, stack, inv);
  }
  private int StackTo(Parameters pars, int stack, Inventory inv)
  {
    foreach (var item in inv.m_inventory)
    {
      if (!MatchItem(pars, item)) continue;
      var amount = Mathf.Min(item.m_shared.m_maxStackSize - item.m_stack, stack);
      item.m_stack += amount;
      stack -= amount;
      if (stack <= 0) break;
    }
    return stack;
  }
  private int InsertTo(Parameters pars, int stack, Inventory inv)
  {
    while (stack > 0)
    {
      var prefab = Prefab.Get(pars) ?? 0;
      var item = ObjectDB.instance.GetItemPrefab(prefab);
      if (item == null || !item.TryGetComponent(out ItemDrop drop)) return stack;
      var itemData = drop.m_itemData.Clone();
      itemData.m_dropPrefab = item;
      itemData.m_quality = Quality?.Get(pars) ?? 1;
      itemData.m_variant = Variant?.Get(pars) ?? 0;
      itemData.m_crafterID = CrafterID?.Get(pars) ?? 0L;
      itemData.m_crafterName = CrafterName?.Get(pars) ?? "";
      itemData.m_worldLevel = WorldLevel?.Get(pars) ?? 0;
      itemData.m_durability = Durability?.Get(pars) ?? itemData.GetMaxDurability(itemData.m_quality);
      itemData.m_equipped = Equipped?.GetBool(pars) ?? false;
      itemData.m_pickedUp = PickedUp?.GetBool(pars) ?? false;
      if (CustomData != null)
        itemData.m_customData = CustomData.ToDictionary(x => x.Key, x => x.Value.Get(pars) ?? "");

      var amount = Mathf.Min(itemData.m_shared.m_maxStackSize, stack);
      stack -= amount;
      itemData.m_stack = amount;

      if (Position == "")
      {
        var slot = inv.FindEmptySlot(true);
        if (slot.x < 0) return stack;
        itemData.m_gridPos = slot;
        inv.m_inventory.Add(itemData);
      }
      else
      {
        itemData.m_gridPos = RolledPosition;
        inv.m_inventory.RemoveAll(x => x.m_gridPos == RolledPosition);
        inv.m_inventory.Add(itemData);
      }
    }
    return stack;
  }
  public void RemoveFrom(Parameters pars, Inventory inv)
  {
    var stack = Stack?.Get(pars) ?? 1;
    for (var i = inv.m_inventory.Count - 1; i >= 0; --i)
    {
      var item = inv.m_inventory[i];
      if (!MatchItem(pars, item)) continue;
      var amount = Mathf.Min(item.m_stack, stack);
      item.m_stack -= amount;
      stack -= amount;
      if (stack <= 0) break;
    }
    inv.m_inventory.RemoveAll(x => x.m_stack <= 0);
  }

  public bool Match(Parameters pars, Inventory inv)
  {
    var item = FindMatch(pars, inv);
    if (item == null) return false;
    inv.RemoveItem(item);
    return true;
  }
  private ItemDrop.ItemData? FindMatch(Parameters pars, Inventory inv)
  {
    if (Position != "")
    {
      var item = inv.GetItemAt(RolledPosition.x, RolledPosition.y);
      if (item == null) return null;
      if (Stack?.Match(pars, item.m_stack) == false) return null;
      if (MatchItem(pars, item)) return item;
    }
    foreach (var item in inv.m_inventory)
    {
      if (Stack?.Match(pars, item.m_stack) == false) continue;
      if (MatchItem(pars, item)) return item;

    }
    return null;
  }
  private bool MatchItem(Parameters pars, ItemDrop.ItemData item)
  {
    var name = item.m_dropPrefab?.name ?? item.m_shared.m_name;
    if (Prefab.Match(pars, StringExtensionMethods.GetStableHashCode(name)) == false) return false;
    if (Durability?.Match(pars, item.m_durability) == false) return false;
    if (Equipped?.Match(pars, item.m_equipped) == false) return false;
    if (Quality?.Match(pars, item.m_quality) == false) return false;
    if (Variant?.Match(pars, item.m_variant) == false) return false;
    if (CrafterID?.Match(pars, item.m_crafterID) == false) return false;
    if (CrafterName?.Match(pars, item.m_crafterName) == false) return false;
    if (WorldLevel?.Match(pars, item.m_worldLevel) == false) return false;
    if (PickedUp?.Match(pars, item.m_pickedUp) == false) return false;
    if (CustomData == null) return true;
    foreach (var kvp in CustomData)
    {
      if (!item.m_customData.TryGetValue(kvp.Key, out var value)) return false;
      if (kvp.Value.Match(pars, value) == false) return false;
    }
    return true;
  }
}
