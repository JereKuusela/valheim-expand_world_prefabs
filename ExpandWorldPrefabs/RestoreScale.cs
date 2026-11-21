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
    if (!Config.RestoreScale) return;
    if (SupportsInitialScaleSync(zdo)) return;

    if (SetScaleBackup(zdo))
      SetSyncScale(zdo);
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

    var vector = ReadVector(__instance, ZDOVars.s_scaleHash);
    var scalar = ReadScalar(__instance, ZDOVars.s_scaleScalarHash);
    if (TryGetScaleBackup(__instance, out Vector3 backedVecScale))
    {
      if (vector == backedVecScale && !scalar.HasValue) return;
      __instance.Set(ZDOVars.s_scaleHash, backedVecScale);
      __instance.RemoveFloat(ZDOVars.s_scaleScalarHash);
    }
    else if (TryGetScaleBackup(__instance, out float backedScalarScale))
    {
      if (!vector.HasValue && scalar.HasValue && !(Math.Abs(scalar.Value - backedScalarScale) > 0.0001f)) return;
      __instance.Set(ZDOVars.s_scaleScalarHash, backedScalarScale);
      __instance.RemoveVec3(ZDOVars.s_scaleHash);
    }
    else
    {
      SetScaleBackup(__instance);
      return;
    }
    ReassignOwnerAfterScaleRestore(__instance);
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



  private static bool SetScaleBackup(ZDO zdo)
  {
    if (ReadVector(zdo, ZDOVars.s_scaleHash) is Vector3 vecScale)
      zdo.Set(ScaleBackupVecHash, vecScale);
    else if (ReadScalar(zdo, ZDOVars.s_scaleScalarHash) is float scalarScale)
      zdo.Set(ScaleBackupScalarHash, scalarScale);
    else
      return false;
    return true;
  }

  private static Vector3? ReadVector(ZDO zdo, int key) =>
    ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var values) && values.TryGetValue(key, out var scale) ? scale : null;

  private static float? ReadScalar(ZDO zdo, int key) =>
    ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var values) && values.TryGetValue(key, out var scale) ? scale : null;

  public static bool TryGetScaleBackup(ZDO zdo, out Vector3 scale)
  {
    var backup = ReadVector(zdo, ScaleBackupVecHash);
    scale = backup.GetValueOrDefault();
    return backup.HasValue;
  }

  public static bool TryGetScaleBackup(ZDO zdo, out float scale)
  {
    var backup = ReadScalar(zdo, ScaleBackupScalarHash);
    scale = backup.GetValueOrDefault();
    return backup.HasValue;
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
    DelayedOwner.Add(zdo, previousOwner);
  }
}
