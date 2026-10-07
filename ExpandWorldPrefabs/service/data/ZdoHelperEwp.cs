using System;
using System.Collections.Generic;
using ExpandWorld.Prefab;
using UnityEngine;

namespace Data;

// EWP only: server side data is stored outside of the ZDO, and some keys exist only on the server.
public static partial class ZdoHelper
{
  private static readonly HashSet<int> ServerSideHashes = [];

  static partial void OnHashed(string key, int hash)
  {
    if (key.StartsWith("ewp_", StringComparison.OrdinalIgnoreCase))
      ServerSideHashes.Add(hash);
  }
  public static bool IsServerSideHash(int hash) => ServerSideHashes.Contains(hash);

  public static byte[]? TryGetBytes(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetBytes(zdo.m_uid, value, out var serverValue)) return serverValue;
    return zdo.GetByteArray(value);
  }
  public static string? TryGetString(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetString(zdo.m_uid, value, out var serverValue)) return serverValue;
    if (ItemDataHelper.TryGetString(zdo, value, out var packedValue)) return packedValue;
    return ZDOExtraData.s_strings.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var str) ? str : TryGetStringField(zdo.m_prefab, value);
  }
  public static float? TryGetFloat(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetFloat(zdo.m_uid, value, out var serverValue)) return serverValue;
    if (ItemDataHelper.TryGetFloat(zdo, value, out var packedValue)) return packedValue;
    return ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var f) ? f : TryGetFloatField(zdo.m_prefab, value);
  }
  public static int? TryGetInt(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetInt(zdo.m_uid, value, out var serverValue)) return serverValue;
    if (ItemDataHelper.TryGetInt(zdo, value, out var packedValue)) return packedValue;
    return ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var i) ? i : TryGetIntField(zdo.m_prefab, value);
  }
  public static long? TryGetLong(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetLong(zdo.m_uid, value, out var serverValue)) return serverValue;
    if (ItemDataHelper.TryGetLong(zdo, value, out var packedValue)) return packedValue;
    return ZDOExtraData.s_longs.TryGetValue(zdo.m_uid, out var data) && data.TryGetValue(value, out var l) ? l : TryGetLongField(zdo.m_prefab, value);
  }
  public static bool? TryGetBool(ZDO zdo, int value)
  {
    if (ServerSideData.ShouldUse(value) && ServerSideData.TryGetInt(zdo.m_uid, value, out var serverValue)) return serverValue > 0;
    if (ItemDataHelper.TryGetInt(zdo, value, out var packedValue)) return packedValue > 0;
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
}
