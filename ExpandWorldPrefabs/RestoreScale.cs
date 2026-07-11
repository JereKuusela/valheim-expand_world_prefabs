
using System.Collections.Generic;
using System;
using HarmonyLib;
using UnityEngine;
using Data;
using System.Linq;

namespace ExpandWorld.Prefab;


public class RestoreScale
{
  private static readonly int ScaleBackupVecHash = "scaleBackup".GetStableHashCode();
  private static readonly int ScaleBackupScalarHash = "scaleScalarBackup".GetStableHashCode();
  private static readonly int SyncScaleHash = "ZSyncTransform.m_syncScale".GetStableHashCode();
  public static bool ShouldRestoreScale(ZDO zdo) => zdo.GetBool(SyncScaleHash);

  public static void Patch(Harmony harmony)
  {
    var original = AccessTools.Method(typeof(ZDO), nameof(ZDO.Deserialize));
    var postfix = AccessTools.Method(typeof(SupportAttach), nameof(Deserialize));
    harmony.Patch(original, postfix: new HarmonyMethod(postfix));
  }

  static void Deserialize(ZDO __instance)
  {
    if (!ShouldRestoreScale(__instance))
      return;
    if (TryGetScaleBackup(__instance, out Vector3 backedVecScale))
    {
      if (NeedsVectorScaleRestore(__instance, backedVecScale, out var hasScalar))
      {
        __instance.Set(ZDOVars.s_scaleHash, backedVecScale);
        __instance.RemoveFloat(ZDOVars.s_scaleScalarHash);
        ReassignOwnerAfterScaleRestore(__instance);
      }
      return;
    }

    if (TryGetScaleBackup(__instance, out float backedScalarScale))
    {
      if (NeedsScalarScaleRestore(__instance, backedScalarScale, out var hasVec))
      {
        __instance.Set(ZDOVars.s_scaleScalarHash, backedScalarScale);
        __instance.RemoveVec3(ZDOVars.s_scaleHash);
        ReassignOwnerAfterScaleRestore(__instance);
      }
      return;
    }
    SetScaleBackup(__instance);
  }

  private static bool NeedsVectorScaleRestore(ZDO zdo, Vector3 backedVecScale, out bool hasScalar)
  {
    var currentVecScale = Vector3.zero;
    var hasVec = ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var vecs) && vecs.TryGetValue(ZDOVars.s_scaleHash, out currentVecScale);
    hasScalar = ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) && floats.ContainsKey(ZDOVars.s_scaleScalarHash);
    return !hasVec || currentVecScale != backedVecScale || hasScalar;
  }

  private static bool NeedsScalarScaleRestore(ZDO zdo, float backedScalarScale, out bool hasVec)
  {
    var currentScalarScale = 0f;
    var hasScalar = ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) && floats.TryGetValue(ZDOVars.s_scaleScalarHash, out currentScalarScale);
    hasVec = ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var vecs) && vecs.ContainsKey(ZDOVars.s_scaleHash);
    return !hasScalar || Math.Abs(currentScalarScale - backedScalarScale) > 0.0001f || hasVec;
  }



  public static void SetScaleBackup(ZDO zdo)
  {
    if (ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var vecs) && vecs.TryGetValue(ZDOVars.s_scaleHash, out var vecScale))
      zdo.Set(ScaleBackupVecHash, vecScale);
    else if (ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) && floats.TryGetValue(ZDOVars.s_scaleScalarHash, out var scalarScale))
      zdo.Set(ScaleBackupScalarHash, scalarScale);
  }

  public static bool TryGetScaleBackup(ZDO zdo, out Vector3 scale)
  {
    if (ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var vecs) && vecs.TryGetValue(ScaleBackupVecHash, out scale))
      return true;
    scale = Vector3.zero;
    return false;
  }

  public static bool TryGetScaleBackup(ZDO zdo, out float scale)
  {
    if (ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) && floats.TryGetValue(ScaleBackupScalarHash, out scale))
      return true;
    scale = 0f;
    return false;
  }

  private static void ReassignOwnerAfterScaleRestore(ZDO zdo)
  {
    var previousOwner = zdo.GetOwner();
    zdo.SetOwner(0L);
    zdo.DataRevision += 100;
    DelayedOwner.Add(0.1f, zdo, previousOwner);
  }
}
