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
  private static bool IsPatched = false;
  public static void Patch(Harmony harmony, PrefabInfo changeDatas, bool shouldPatch)
  {
    if (shouldPatch && !IsPatched)
      DoPatch(harmony);
    if (!shouldPatch && IsPatched)
      DoUnpatch(harmony);

    ChangedZDOs.Clear();
    Index = 0;
    TrackedHashes.Clear();
    AddTracks(changeDatas.Weighted);
    AddTracks(changeDatas.Fallback);
    AddTracks(changeDatas.Separate);
  }
  private static void DoPatch(Harmony harmony)
  {
    IsPatched = true;
    var method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(int)]);
    var patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleInt));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(int)]);
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(float)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleFloat));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(float)]);
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(string)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleString));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(string)]);
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(long)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleLong));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(long)]);
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(Vector3)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleVec));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(Vector3)]);
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(Quaternion)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleQuaternion));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(Quaternion)]);
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(byte[])]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleByteArray));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(byte[])]);
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }
  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(int)]);
    var patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleInt));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(int)]);
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(float)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleFloat));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(float)]);
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(string)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleString));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(string)]);
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(long)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleLong));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(long)]);
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(Vector3)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleVec));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(Vector3)]);
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(Quaternion)]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleQuaternion));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(Quaternion)]);
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Set), [typeof(ZDOID), typeof(int), typeof(byte[])]);
    patch = AccessTools.Method(typeof(HandleChanged), nameof(HandleByteArray));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.Add), [typeof(ZDOID), typeof(int), typeof(byte[])]);
    harmony.Unpatch(method, patch);
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
  public static void Clear()
  {
    ChangedZDOs.Clear();
    Index = 0;
    IgnoreZdo = ZDOID.None;
  }

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
    while (Index < count)
    {
      // Consume before dispatch: a failing rule must not replay every frame.
      var changed = ChangedZDOs[Index++];
      var manager = ZDOMan.instance;
      if (manager == null || !ReferenceEquals(manager, changed.Manager)) continue;
      if (!manager.m_objectsByID.TryGetValue(changed.Zdo, out var zdo) ||
          !ReferenceEquals(zdo, changed.Target) || !zdo.Valid) continue;
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
    QueueInt(zdo, hash, zdo.GetInt(hash), value);
  }
  private static void QueueInt(ZDO zdo, int hash, int prev, int value)
  {
    if (prev == value) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value.ToString(), prev.ToString()));
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value != 0 ? "true" : "false", prev != 0 ? "true" : "false"));
    var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(value) : null;
    var prevPrefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(prev) : null;
    if (prefab || prevPrefab)
      ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), prefab?.name ?? "<none>", prevPrefab?.name ?? "<none>"));
  }
  private static void HandleFloat(ZDOID zid, int hash, float value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    QueueFloat(zdo, hash, zdo.GetFloat(hash), value);
  }
  private static void QueueFloat(ZDO zdo, int hash, float prev, float value)
  {
    if (prev == value) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value.ToString(NumberFormatInfo.InvariantInfo), prev.ToString(NumberFormatInfo.InvariantInfo)));
  }
  private static void HandleString(ZDOID zid, int hash, string value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    QueueString(zdo, hash, zdo.GetString(hash), value);
  }
  private static void QueueString(ZDO zdo, int hash, string prev, string value)
  {
    if (prev == value) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value == "" ? "<none>" : value, prev == "" ? "<none>" : prev));
  }
  private static void HandleLong(ZDOID zid, int hash, long value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    QueueLong(zdo, hash, zdo.GetLong(hash), value);
  }
  private static void QueueLong(ZDO zdo, int hash, long prev, long value)
  {
    if (prev == value) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value.ToString(), prev.ToString()));
  }
  private static void HandleVec(ZDOID zid, int hash, Vector3 value)
  {
    if (!TrackedHashes.TryGetValue(hash, out var tracked)) return;
    if (!ZDOMan.instance.m_objectsByID.TryGetValue(zid, out var zdo)) return;
    if (!tracked.Contains(zdo.m_prefab)) return;
    if (IgnoreZdo == zid) return;
    QueueVec(zdo, hash, zdo.GetVec3(hash, Vector3.zero), value);
  }
  private static void QueueVec(ZDO zdo, int hash, Vector3 previous, Vector3 value)
  {
    var prev = Helper.FormatPos2(previous);
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
    QueueQuaternion(zdo, hash, zdo.GetQuaternion(hash, Quaternion.identity), value);
  }
  private static void QueueQuaternion(ZDO zdo, int hash, Quaternion previous, Quaternion value)
  {
    var prev = Helper.FormatRot2(previous.eulerAngles);
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
    QueueByteArray(zdo, hash, zdo.GetByteArray(hash), value);
  }
  private static void QueueByteArray(ZDO zdo, int hash, byte[] prev, byte[] value)
  {
    if (ReferenceEquals(prev, value) || (prev != null && value != null && prev.SequenceEqual(value))) return;
    ChangedZDOs.Add(new(zdo, ZdoHelper.ReverseHash(hash), value == null ? "" : Convert.ToBase64String(value), prev == null ? "" : Convert.ToBase64String(prev)));
  }
}

public class ChangedZdo(ZDO zdo, string key, string value, string previous)
{
  public readonly ZDOMan Manager = ZDOMan.instance;
  public readonly ZDO Target = zdo;
  public readonly ZDOID Zdo = zdo.m_uid;
  public string Key = key;
  public string Value = value;
  public string PreviousValue = previous;
}