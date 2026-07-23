using System;
using Data;
using HarmonyLib;
using UnityEngine;

namespace ExpandWorld.Prefab;


public class RestoreScale
{
  private static bool IsPatched = false;
  private static readonly int ScaleBackupVecHash = ZdoHelper.Hash("scaleBackup");
  private static readonly int ScaleBackupScalarHash = ZdoHelper.Hash("scaleScalarBackup");
  private static readonly int HasFieldsHash = ZdoHelper.Hash("HasFields");
  private static readonly int HasFieldsZSyncTransformHash = ZdoHelper.Hash("HasFieldsZSyncTransform");
  private static readonly int SyncScaleHash = ZdoHelper.Hash("ZSyncTransform.m_syncScale");
  public static bool ShouldRestoreScale(ZDO zdo) => zdo.GetBool(SyncScaleHash);

  // Quality of life: Automatically applies m_syncScale for scaled objects that need it.
  public static void Check(ZDO zdo)
  {
    if (!Settings.RestoreScale) return;
    if (SupportsInitialScaleSync(zdo)) return;

    if (ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var vecs) && vecs.TryGetValue(ZDOVars.s_scaleHash, out var vecScale))
    {
      SetSyncScale(zdo);
      zdo.Set(ScaleBackupVecHash, vecScale);
    }
    else if (ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) && floats.TryGetValue(ZDOVars.s_scaleScalarHash, out var scalarScale))
    {
      SetSyncScale(zdo);
      zdo.Set(ScaleBackupScalarHash, scalarScale);
    }
  }
  private static void SetSyncScale(ZDO zdo)
  {
    zdo.Set(HasFieldsHash, true);
    zdo.Set(HasFieldsZSyncTransformHash, true);
    zdo.Set(SyncScaleHash, true);

  }
  public static void Patch(Harmony harmony, bool shouldPatch)
  {
    if (shouldPatch && !IsPatched)
      DoPatch(harmony);
    if (!shouldPatch && IsPatched)
      DoUnpatch(harmony);
  }

  private static void DoPatch(Harmony harmony)
  {
    IsPatched = true;
    var original = AccessTools.Method(typeof(ZDO), nameof(ZDO.Deserialize));
    var postfix = AccessTools.Method(typeof(RestoreScale), nameof(Deserialize));
    harmony.Patch(original, postfix: new HarmonyMethod(postfix));
    original = AccessTools.Method(typeof(ZSyncTransform), nameof(ZSyncTransform.Awake));
    postfix = AccessTools.Method(typeof(RestoreScale), nameof(SinglePlayerFix));
    harmony.Patch(original, postfix: new HarmonyMethod(postfix));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var original = AccessTools.Method(typeof(ZDO), nameof(ZDO.Deserialize));
    var postfix = AccessTools.Method(typeof(RestoreScale), nameof(Deserialize));
    harmony.Unpatch(original, postfix);
    original = AccessTools.Method(typeof(ZSyncTransform), nameof(ZSyncTransform.Awake));
    postfix = AccessTools.Method(typeof(RestoreScale), nameof(SinglePlayerFix));
    harmony.Unpatch(original, postfix);
  }

  static void Deserialize(ZDO __instance)
  {
    if (!ShouldRestoreScale(__instance))
      return;
    if (TryGetScaleBackup(__instance, out Vector3 backedVecScale))
    {
      if (NeedsVectorScaleRestore(__instance, backedVecScale))
      {
        __instance.Set(ZDOVars.s_scaleHash, backedVecScale);
        __instance.RemoveFloat(ZDOVars.s_scaleScalarHash);
        ReassignOwnerAfterScaleRestore(__instance);
      }
      return;
    }

    if (TryGetScaleBackup(__instance, out float backedScalarScale))
    {
      if (NeedsScalarScaleRestore(__instance, backedScalarScale))
      {
        __instance.Set(ZDOVars.s_scaleScalarHash, backedScalarScale);
        __instance.RemoveVec3(ZDOVars.s_scaleHash);
        ReassignOwnerAfterScaleRestore(__instance);
      }
      return;
    }
    // Fail safe if backup info missing. This should never happen, but just in case.
    SetScaleBackup(__instance);
  }

  private static bool NeedsVectorScaleRestore(ZDO zdo, Vector3 backedVecScale)
  {
    var currentVecScale = Vector3.zero;
    var hasVec = ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var vecs) && vecs.TryGetValue(ZDOVars.s_scaleHash, out currentVecScale);
    var hasScalar = ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) && floats.ContainsKey(ZDOVars.s_scaleScalarHash);
    return !hasVec || currentVecScale != backedVecScale || hasScalar;
  }

  private static bool NeedsScalarScaleRestore(ZDO zdo, float backedScalarScale)
  {
    var currentScalarScale = 0f;
    var hasScalar = ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) && floats.TryGetValue(ZDOVars.s_scaleScalarHash, out currentScalarScale);
    var hasVec = ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var vecs) && vecs.ContainsKey(ZDOVars.s_scaleHash);
    return !hasScalar || Math.Abs(currentScalarScale - backedScalarScale) > 0.0001f || hasVec;
  }

  // Single player won't trigger Deserialize so correction doesn't happen.
  // But easy to just handle it manually.
  static void SinglePlayerFix(ZSyncTransform __instance)
  {
    if (!__instance.m_syncScale)
      return;
    if (!__instance.m_nview.IsValid())
      return;
    var zdo = __instance.m_nview.GetZDO();
    var vec3 = zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero);
    if (vec3 != Vector3.zero)
    {
      __instance.transform.localScale = vec3;
    }
    else
    {
      var scalar = zdo.GetFloat(ZDOVars.s_scaleScalarHash, __instance.transform.localScale.x);
      if (!__instance.transform.localScale.x.Equals(scalar))
        __instance.transform.localScale = new Vector3(scalar, scalar, scalar);
    }
  }



  private static void SetScaleBackup(ZDO zdo)
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

  private static bool SupportsInitialScaleSync(ZDO zdo)
  {
    var prefab = ZNetScene.instance.GetPrefab(zdo.m_prefab);
    return prefab && prefab.GetComponent<ZNetView>().m_syncInitialScale;
  }

  private static void ReassignOwnerAfterScaleRestore(ZDO zdo)
  {
    var previousOwner = zdo.GetOwner();
    zdo.SetOwner(0L);
    zdo.DataRevision += 100;
    DelayedOwner.Add(0.1f, zdo, previousOwner);
  }
}
