using System;
using System.Collections.Generic;
using System.Globalization;
using ExpandWorld.Prefab;
using Service;
using UnityEngine;

namespace Data;

// Helper for retrieving ZDO data.
// For example to use field values as the default.
public static class ZdoHelper
{
  public static string GetString(ZDO zdo, string value, string defaultValue)
  {
    var hash = Hash(value);
    if (ServerSideData.ShouldUse(hash) && ServerSideData.TryGetString(zdo.m_uid, hash, out var serverValue)) return serverValue;
    return ZDOExtraData.s_strings.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(hash, out var str) ? str : GetStringField(zdo.m_prefab, value, defaultValue);
  }
  public static float GetFloat(ZDO zdo, string value, string defaultValue)
  {
    var hash = Hash(value);
    if (ServerSideData.ShouldUse(hash) && ServerSideData.TryGetFloat(zdo.m_uid, hash, out var serverValue)) return serverValue;
    return ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(hash, out var f) ? f : GetFloatField(zdo.m_prefab, value, defaultValue);
  }
  public static int GetInt(ZDO zdo, string value, string defaultValue)
  {
    var hash = Hash(value);
    if (ServerSideData.ShouldUse(hash) && ServerSideData.TryGetInt(zdo.m_uid, hash, out var serverValue)) return serverValue;
    return ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(hash, out var i) ? i : GetIntField(zdo.m_prefab, value, defaultValue);
  }
  public static long GetLong(ZDO zdo, string value, string defaultValue)
  {
    var hash = Hash(value);
    if (ServerSideData.ShouldUse(hash) && ServerSideData.TryGetLong(zdo.m_uid, hash, out var serverValue)) return serverValue;
    return ZDOExtraData.s_longs.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(hash, out var l) ? l : GetLongField(zdo.m_prefab, value, defaultValue);
  }
  public static bool GetBool(ZDO zdo, string value, string defaultValue)
  {
    var hash = Hash(value);
    if (ServerSideData.ShouldUse(hash) && ServerSideData.TryGetInt(zdo.m_uid, hash, out var serverValue)) return serverValue > 0;
    return ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(hash, out var b) ? b > 0 : GetBoolField(zdo.m_prefab, value, defaultValue);
  }
  public static Vector3 GetVec(ZDO zdo, string value, string defaultValue)
  {
    var hash = Hash(value);
    if (ServerSideData.ShouldUse(hash) && ServerSideData.TryGetVec(zdo.m_uid, hash, out var serverValue)) return serverValue;
    return ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(hash, out var v) ? v : GetVecField(zdo.m_prefab, value, defaultValue);
  }
  public static Quaternion GetQuaternion(ZDO zdo, string value, string defaultValue)
  {
    var hash = Hash(value);
    if (ServerSideData.ShouldUse(hash) && ServerSideData.TryGetQuaternion(zdo.m_uid, hash, out var serverValue)) return serverValue;
    return ZDOExtraData.s_quats.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(hash, out var q) ? q : GetQuatField(zdo.m_prefab, value, defaultValue);
  }
  public static string? TryGetString(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetString(zdo.m_uid, value, out var serverValue)) return serverValue;
    return ZDOExtraData.s_strings.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var str) ? str : TryGetStringField(zdo.m_prefab, value);
  }
  public static float? TryGetFloat(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetFloat(zdo.m_uid, value, out var serverValue)) return serverValue;
    return ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var f) ? f : TryGetFloatField(zdo.m_prefab, value);
  }
  public static int? TryGetInt(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetInt(zdo.m_uid, value, out var serverValue)) return serverValue;
    return ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var i) ? i : TryGetIntField(zdo.m_prefab, value);
  }
  public static long? TryGetLong(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetLong(zdo.m_uid, value, out var serverValue)) return serverValue;
    return ZDOExtraData.s_longs.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var l) ? l : TryGetLongField(zdo.m_prefab, value);
  }
  public static bool? TryGetBool(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetInt(zdo.m_uid, value, out var serverValue)) return serverValue > 0;
    return ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var b) ? b > 0 : TryGetBoolField(zdo.m_prefab, value);
  }
  public static Vector3? TryGetVec(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetVec(zdo.m_uid, value, out var serverValue)) return serverValue;
    return ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var v) ? v : TryGetVecField(zdo.m_prefab, value);
  }
  public static Quaternion? TryGetQuaternion(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetQuaternion(zdo.m_uid, value, out var serverValue)) return serverValue;
    return ZDOExtraData.s_quats.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var q) ? q : TryGetQuatField(zdo.m_prefab, value);
  }

  private static string GetStringField(int prefabHash, string value, string defaultValue) => GetField(prefabHash, value) is string s ? s : defaultValue;
  private static float GetFloatField(int prefabHash, string value, string defaultValue) => GetField(prefabHash, value) is float f ? f : Parse.Float(defaultValue);
  private static int GetIntField(int prefabHash, string value, string defaultValue) => GetField(prefabHash, value) is int i ? i : Parse.Int(defaultValue);
  private static bool GetBoolField(int prefabHash, string value, string defaultValue) => GetField(prefabHash, value) is bool b ? b : Parse.Boolean(defaultValue);
  private static long GetLongField(int prefabHash, string value, string defaultValue) => GetField(prefabHash, value) is long l ? l : Parse.Long(defaultValue);
  private static Vector3 GetVecField(int prefabHash, string value, string defaultValue) => GetField(prefabHash, value) is Vector3 v ? v : Parse.VectorXZY(defaultValue);
  private static Quaternion GetQuatField(int prefabHash, string value, string defaultValue) => GetField(prefabHash, value) is Quaternion q ? q : Parse.AngleYXZ(defaultValue);
  private static string? TryGetStringField(int prefabHash, int value) => GetField(prefabHash, ReverseHash(value)) is string s ? s : null;
  private static float? TryGetFloatField(int prefabHash, int value) => GetField(prefabHash, ReverseHash(value)) is float f ? f : null;
  private static int? TryGetIntField(int prefabHash, int value) => GetField(prefabHash, ReverseHash(value)) is int i ? i : null;
  private static bool? TryGetBoolField(int prefabHash, int value) => GetField(prefabHash, ReverseHash(value)) is bool b ? b : null;
  private static long? TryGetLongField(int prefabHash, int value) => GetField(prefabHash, ReverseHash(value)) is long l ? l : null;
  private static Vector3? TryGetVecField(int prefabHash, int value) => GetField(prefabHash, ReverseHash(value)) is Vector3 v ? v : null;
  private static Quaternion? TryGetQuatField(int prefabHash, int value) => GetField(prefabHash, ReverseHash(value)) is Quaternion q ? q : null;

  private static object? GetField(int prefabHash, string value)
  {
    var kvp = Parse.Kvp(value, '.');
    if (kvp.Value == "") return null;
    var prefab = ZNetScene.instance.GetPrefab(prefabHash);
    if (prefab == null) return null;
    var component = FindComponent(prefab, kvp.Key);
    if (component == null) return null;
    var fields = kvp.Value.Split('.');
    object result = component;
    foreach (var field in fields)
    {
      var fieldInfo = result.GetType().GetField(field);
      if (fieldInfo == null) return null;
      result = fieldInfo.GetValue(result);
      if (result == null) return null;
    }
    if (result is GameObject go) return go.name;
    if (result is ItemDrop itemDrop) return itemDrop.gameObject.name;
    return result;
  }
  private static Component? FindComponent(GameObject obj, string name)
  {
    obj.GetComponentsInChildren(ZNetView.m_tempComponents);
    foreach (var monoBehaviour in ZNetView.m_tempComponents)
    {
      if (monoBehaviour.GetType().Name == name)
      {
        return monoBehaviour;
      }
    }
    foreach (Transform child in obj.transform)
    {
      var component = FindComponent(child.gameObject, name);
      if (component != null) return component;
    }
    return null;
  }


  private static readonly Dictionary<string, int> HashCache = [];
  private static readonly Dictionary<int, string> ReverseHashCache = [];
  private static readonly HashSet<int> ServerSideHashes = [];
  public static int Hash(string key)
  {
    if (HashCache.TryGetValue(key, out var hash)) return hash;
    hash = HashKey(key);
    HashCache[key] = hash;
    ReverseHashCache[hash] = key;
    if (key.StartsWith("ewp_", StringComparison.OrdinalIgnoreCase))
      ServerSideHashes.Add(hash);
    return hash;
  }
  public static bool IsServerSideHash(int hash) => ServerSideHashes.Contains(hash);
  private static int HashKey(string key)
  {
    if (Parse.TryInt(key, out var result)) return result;
    if (key.StartsWith("$", StringComparison.InvariantCultureIgnoreCase))
    {
      var hash = ZSyncAnimation.GetHash(key.Substring(1));
      if (key == "$anim_speed") return hash;
      return 438569 + hash;
    }
    return key.GetStableHashCode();
  }
  public static string ReverseHash(int hash) => ReverseHashCache.TryGetValue(hash, out var k) ? k : hash.ToString(CultureInfo.InvariantCulture);

  private static readonly int InventoryWidthHash = Hash("Container.m_width");
  private static readonly int InventoryHeightHash = Hash("Container.m_height");
  public static Vector2i GetInventorySize(DataEntry entry, Parameters parameters, ZDO zdo)
  {
    // Width and height can come from data entry, existing ZDO fields or directly from the prefab.
    int width = 0;
    int height = 0;

    if (entry.Ints?.TryGetValue(InventoryWidthHash, out var w) == true)
      width = w.Get(parameters) ?? 0;
    if (entry.Ints?.TryGetValue(InventoryHeightHash, out var h) == true)
      height = h.Get(parameters) ?? 0;
    if (width > 0 && height > 0)
      return new Vector2i(width, height);

    if (width <= 0)
      width = zdo.GetInt(InventoryWidthHash, 0);
    if (height <= 0)
      height = zdo.GetInt(InventoryHeightHash, 0);
    if (width > 0 && height > 0)
      return new Vector2i(width, height);

    // Field might only change one dimension, which complicates the logic.
    var obj = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    var container = obj.GetComponentInChildren<Container>();
    if (container)
    {
      if (width <= 0)
        width = container.m_width;
      if (height <= 0)
        height = container.m_height;
    }
    if (width <= 0) width = 4;
    if (height <= 0) height = 2;
    return new Vector2i(width, height);
  }
}