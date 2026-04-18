using System;
using System.Globalization;
using System.Linq;
using ExpandWorld.Prefab;
using Service;
using UnityEngine;

namespace Data;

public class ObjectParameters(string prefab, string[] args, ZDO zdo) : Parameters(prefab, args, zdo.m_position)
{
  private Inventory? inventory;


  protected override string? GetParameter(string key, string defaultValue)
  {
    var value = base.GetParameter(key, defaultValue);
    if (value != null) return value;
    value = GetGeneralParameter(key);
    if (value != null) return value;
    var keyArg = Parse.Kvp(key, Separator);
    if (keyArg.Value == "") return null;
    key = keyArg.Key;
    var arg = keyArg.Value;
    value = ExecuteCodeWithValue(key, arg);
    if (value != null) return value;
    value = base.GetValueParameter(key, arg, defaultValue);
    if (value != null) return value;
    return GetValueParameter(key, arg, defaultValue);
  }

  private string? GetGeneralParameter(string key) =>
    key switch
    {
      "zdo" => zdo.m_uid.ToString(),
      "pos" => Helper.FormatPos(zdo.m_position),
      "i" => ZoneSystem.GetZone(zdo.m_position).x.ToString(),
      "j" => ZoneSystem.GetZone(zdo.m_position).y.ToString(),
      "a" => Helper.Format(zdo.m_rotation.y),
      "rot" => Helper.FormatRot(zdo.m_rotation),
      "pid" => GetPid(zdo),
      "cid" => GetCid(zdo),
      "platform" => GetPlatform(zdo),
      "pname" => GetPName(zdo),
      "pchar" => GetPChar(zdo),
      "pvisible" => GetPVisible(zdo),
      "owner" => zdo.GetOwner().ToString(),
      "biome" => WorldGenerator.instance.GetBiome(zdo.m_position).ToString(),
      _ => null,
    };

  private static string GetPid(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null && peer.IsReady())
      return PeerManager.GetPid(peer);
    else if (Player.m_localPlayer)
      return "Server";
    return "";
  }
  private static string GetCid(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
    {
      var characterZdo = ZDOMan.instance.GetZDO(peer.m_characterID);
      if (characterZdo != null)
      {
        var cid = ZdoHelper.TryGetLong(characterZdo, ZDOVars.s_playerID);
        if (cid != null)
          return cid.Value.ToString();
      }
    }
    else if (Player.m_localPlayer)
      return Player.m_localPlayer.GetPlayerID().ToString();
    return "";
  }
  private static string GetPlatform(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null && peer.IsReady())
      return PeerManager.GetPlatform(peer);
    else if (Player.m_localPlayer)
      return "Server";
    return "";
  }
  private static string GetPName(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_playerName;
    else if (Player.m_localPlayer)
      return Player.m_localPlayer.GetPlayerName();
    return "";
  }
  private static string GetPChar(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_characterID.ToString();
    else if (Player.m_localPlayer)
      return Player.m_localPlayer.GetPlayerID().ToString();
    return "";
  }
  private static string GetPVisible(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_publicRefPos.ToString();
    else if (ZNet.instance)
      return ZNet.instance.IsReferencePositionPublic().ToString();
    return "";
  }
  private static ZNetPeer? GetPeer(ZDO zdo) => zdo.GetOwner() != 0 ? ZNet.instance.GetPeer(zdo.GetOwner()) : null;


  protected override string? GetValueParameter(string key, string value, string defaultValue) =>
   key switch
   {
     "key" => DataHelper.GetGlobalKey(value),
     "string" => GetString(value, defaultValue),
     "float" => GetFloat(value, defaultValue).ToString(CultureInfo.InvariantCulture),
     "int" => GetInt(value, defaultValue).ToString(CultureInfo.InvariantCulture),
     "long" => GetLong(value, defaultValue).ToString(CultureInfo.InvariantCulture),
     "bool" => GetBool(value, defaultValue) ? "true" : "false",
     "hash" => GetHash(value, defaultValue),
     "vec" => DataEntry.PrintVectorXZY(GetVec(value, defaultValue)),
     "quat" => DataEntry.PrintAngleYXZ(GetQuaternion(value, defaultValue)),
     "byte" => GetBytes(value, defaultValue),
     "zdo" => zdo.GetZDOID(value).ToString(),
     "amount" => GetAmount(value, defaultValue),
     "quality" => GetQuality(value, defaultValue),
     "durability" => GetDurability(value, defaultValue),
     "item" => GetItem(value, defaultValue),
     "pos" => DataEntry.PrintVectorXZY(GetPos(value)),
     "pdata" => GetPlayerData(GetPeer(zdo), value),
     _ => null,
   };


  private string GetBytes(string value, string defaultValue)
  {
    var bytes = zdo.GetByteArray(value);
    return bytes == null ? defaultValue : Convert.ToBase64String(bytes);
  }
  private string GetString(string value, string defaultValue) => ZdoHelper.GetString(zdo, value, defaultValue);
  private float GetFloat(string value, string defaultValue) => ZdoHelper.GetFloat(zdo, value, defaultValue);
  private int GetInt(string value, string defaultValue) => ZdoHelper.GetInt(zdo, value, defaultValue);
  private long GetLong(string value, string defaultValue) => ZdoHelper.GetLong(zdo, value, defaultValue);
  private bool GetBool(string value, string defaultValue) => ZdoHelper.GetBool(zdo, value, defaultValue);
  private string GetHash(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var zdoValue = zdo.GetInt(value);
    return ZNetScene.instance.GetPrefab(zdoValue)?.name ?? ZoneSystem.instance.GetLocation(zdoValue)?.m_prefabName ?? defaultValue;
  }
  private Vector3 GetVec(string value, string defaultValue) => ZdoHelper.GetVec(zdo, value, defaultValue);
  private Quaternion GetQuaternion(string value, string defaultValue) => ZdoHelper.GetQuaternion(zdo, value, defaultValue);
  private string GetItem(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var kvp = Parse.Kvp(value, Separator);
    // Coordinates requires two numbers, otherwise it's an item name.
    if (!Parse.TryInt(kvp.Key, out var x) || !Parse.TryInt(kvp.Value, out var y)) return GetAmountOfItems(value).ToString();
    return GetNameAt(x, y) ?? defaultValue;
  }
  private string GetAmount(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var kvp = Parse.Kvp(value, Separator);
    // Coordinates requires two numbers, otherwise it's an item name.
    if (!Parse.TryInt(kvp.Key, out var x) || !Parse.TryInt(kvp.Value, out var y)) return GetAmountOfItems(value).ToString();
    return GetAmountAt(x, y) ?? defaultValue;
  }
  private string GetDurability(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var kvp = Parse.Kvp(value, Separator);
    // Coordinates requires two numbers, otherwise it's an item name.
    if (!Parse.TryInt(kvp.Key, out var x) || !Parse.TryInt(kvp.Value, out var y)) return defaultValue;
    return GetDurabilityAt(x, y) ?? defaultValue;
  }
  private string GetQuality(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var kvp = Parse.Kvp(value, Separator);
    // Coordinates requires two numbers, otherwise it's an item name.
    if (!Parse.TryInt(kvp.Key, out var x) || !Parse.TryInt(kvp.Value, out var y)) return defaultValue;
    return GetQualityAt(x, y) ?? defaultValue;
  }
  private int GetAmountOfItems(string prefab)
  {
    LoadInventory();
    if (inventory == null) return 0;
    if (prefab == "") return inventory.m_inventory.Sum(i => i.m_stack);
    if (prefab == "*") return inventory.m_inventory.Sum(i => i.m_stack);
    int count = 0;
    if (prefab[0] == '*' && prefab[prefab.Length - 1] == '*')
    {
      prefab = prefab.Substring(1, prefab.Length - 2).ToLowerInvariant();
      foreach (var item in inventory.m_inventory)
      {
        if (GetName(item).ToLowerInvariant().Contains(prefab)) count += item.m_stack;
      }
    }
    else if (prefab[0] == '*')
    {
      prefab = prefab.Substring(1);
      foreach (var item in inventory.m_inventory)
      {
        if (GetName(item).EndsWith(prefab, StringComparison.OrdinalIgnoreCase)) count += item.m_stack;
      }
    }
    else if (prefab[prefab.Length - 1] == '*')
    {
      prefab = prefab.Substring(0, prefab.Length - 1);
      foreach (var item in inventory.m_inventory)
      {
        if (GetName(item).StartsWith(prefab, StringComparison.OrdinalIgnoreCase)) count += item.m_stack;
      }
    }
    else
    {
      var wildIndex = prefab.IndexOf('*');
      if (wildIndex > 0 && wildIndex < prefab.Length - 1)
      {
        var prefix = prefab.Substring(0, wildIndex);
        var suffix = prefab.Substring(wildIndex + 1);
        foreach (var item in inventory.m_inventory)
        {
          var name = GetName(item);
          if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
              name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            count += item.m_stack;
        }
      }
      else
      {
        foreach (var item in inventory.m_inventory)
        {
          if (GetName(item) == prefab) count += item.m_stack;
        }
      }

    }
    return count;
  }
  private string GetName(ItemDrop.ItemData? item) => item?.m_dropPrefab?.name ?? item?.m_shared.m_name ?? "";
  private string? GetNameAt(int x, int y)
  {
    var item = GetItemAt(x, y);
    return GetName(item);
  }
  private string? GetAmountAt(int x, int y) => GetItemAt(x, y)?.m_stack.ToString();
  private string? GetDurabilityAt(int x, int y) => GetItemAt(x, y)?.m_durability.ToString();
  private string? GetQualityAt(int x, int y) => GetItemAt(x, y)?.m_quality.ToString();
  private ItemDrop.ItemData? GetItemAt(int x, int y)
  {
    LoadInventory();
    if (inventory == null) return null;
    if (x < 0 || x >= inventory.m_width || y < 0 || y >= inventory.m_height) return null;
    return inventory.GetItemAt(x, y);
  }


  private void LoadInventory()
  {
    if (inventory != null) return;
    var currentItems = zdo.GetString(ZDOVars.s_items);
    if (currentItems == "") return;
    inventory = new("", null, 9999, 9999);
    inventory.Load(new ZPackage(currentItems));
  }

  private Vector3 GetPos(string value)
  {
    var offset = Parse.VectorXZY(value);
    return zdo.GetPosition() + zdo.GetRotation() * offset;
  }

  public static string GetPlayerData(ZNetPeer? peer, string key)
  {
    if (peer != null)
      return peer.m_serverSyncedPlayerData.TryGetValue(key, out var data) ? data : "";
    else if (Player.m_localPlayer)
      return ZNet.instance.m_serverSyncedPlayerData.TryGetValue(key, out var data) ? data : "";
    return "";
  }
}
