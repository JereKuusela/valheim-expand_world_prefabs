using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Data;
using HarmonyLib;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class HandleChanged
{
  public static void Patch(Harmony harmony, PrefabInfo changeDatas)
  {
    var method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(int)]);
    var patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleInt));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(float)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleFloat));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(string)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleString));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(long)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleLong));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(Vector3)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleVec));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(Quaternion)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleQuaternion));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(byte[])]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleByteArray));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));

    TrackedHashes.Clear();
    AddTracks(changeDatas.Weighted);
    AddTracks(changeDatas.Fallback);
    AddTracks(changeDatas.Separate);
  }

  private static void AddTracks(Dictionary<int, List<Info>> datas)
  {
    foreach (var kvp in datas)
    {
      var prefab = kvp.Key;
      foreach (var info in kvp.Value)
      {
        if (info.Args.Length == 0) continue;
        var hash = ZdoHelper.Hash(info.Args[0]);
        if (!TrackedHashes.ContainsKey(hash)) TrackedHashes[hash] = [];
        TrackedHashes[hash].Add(prefab);
      }
    }
  }

  private static readonly List<ChangedZdo> ChangedZDOs = [];
  private static int Index = 0;
  public static ZDOID IgnoreZdo = ZDOID.None;

  public static void Execute()
  {
    if (ChangedZDOs.Count > 10000)
    {
      Log.Warning("Too many changes, possible infinite loop.");
      Index = 0;
      ChangedZDOs.Clear();
      return;
    }
    // Execution can trigger changes, so foreach can't be used.
    // Handling new changes next frame ensures that the data is fully changed.
    var count = ChangedZDOs.Count;
    for (; Index < count; Index++)
    {
      var changed = ChangedZDOs[Index];
      var zdo = ZDOMan.instance.GetZDO(changed.Zdo);
      if (zdo == null || !zdo.Valid) continue;
      Manager.Handle(ActionType.Change, [changed.Key, changed.Value, changed.PreviousValue], zdo);
    }
    if (Index < ChangedZDOs.Count) return;
    Index = 0;
    ChangedZDOs.Clear();
  }
  private static readonly Dictionary<int, HashSet<int>> TrackedHashes = [];
  private static void HandleInt(ZDOID zid, int hash, int value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    var prev = zdo.GetInt(hash);
    if (prev == value) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value.ToString(), prev.ToString()));
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value != 0 ? "true" : "false", prev != 0 ? "true" : "false"));
    var prefab = ZNetScene.instance.GetPrefab(value);
    var prevPrefab = ZNetScene.instance.GetPrefab(prev);
    if (prefab || prevPrefab)
      ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), prefab?.name ?? "<none>", prevPrefab?.name ?? "<none>"));
  }
  private static void HandleFloat(ZDOID zid, int hash, float value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    var prev = zdo.GetFloat(hash);
    if (prev == value) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value.ToString(NumberFormatInfo.InvariantInfo), prev.ToString(NumberFormatInfo.InvariantInfo)));
  }
  private static void HandleString(ZDOID zid, int hash, string value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    var prev = zdo.GetString(hash);
    if (prev == value) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value == "" ? "<none>" : value, prev == "" ? "<none>" : prev));
  }
  private static void HandleLong(ZDOID zid, int hash, long value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    var prev = zdo.GetLong(hash);
    if (prev == value) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value.ToString(), prev.ToString()));
  }
  private static void HandleVec(ZDOID zid, int hash, Vector3 value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    var prev = Helper.FormatPos2(zdo.GetVec3(hash, Vector3.zero));
    var curr = Helper.FormatPos2(value);
    if (prev == curr) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), curr, prev));
  }
  private static void HandleQuaternion(ZDOID zid, int hash, Quaternion value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    var prev = Helper.FormatRot2(zdo.GetQuaternion(hash, Quaternion.identity).eulerAngles);
    var curr = Helper.FormatRot2(value.eulerAngles);
    if (prev == curr) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), curr, prev));
  }
  private static void HandleByteArray(ZDOID zid, int hash, byte[] value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    var prev = zdo.GetByteArray(hash);
    if (prev == null ? value == null : prev.SequenceEqual(value)) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value == null ? "" : Convert.ToBase64String(value), prev == null ? "" : Convert.ToBase64String(prev)));
  }
}

public class ChangedZdo(ZDO zdo, string key, string value, string previous)
{
  public ZDOID Zdo = zdo.m_uid;
  public string Key = key;
  public string Value = value;
  public string PreviousValue = previous;
}