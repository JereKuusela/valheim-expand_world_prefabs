using System;
using System.Collections.Generic;
using Data;
using HarmonyLib;
using Service;
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

  // Native initial synchronization needs no EWP owner/scale workaround.
  public static bool NeedsLegacyScaleHack(ZDO zdo) =>
    Config.RestoreScale && !SupportsInitialScaleSync(zdo) && ShouldRestoreScale(zdo);

  // Bound the client-owned scalar repair to one attempt per target per ZDO.
  // Client writes may repeatedly reintroduce a conflicting runtime vector.
  private static readonly Dictionary<ZDOID, float> ClientScalarAttempts = [];
  private static readonly HashSet<ZDOID> ReportedConflicts = [];
  public static void Clear()
  {
    ClientScalarAttempts.Clear();
    ReportedConflicts.Clear();
  }

  // This path is invoked after *intentional* EWP data writes to existing ZDOs.
  // The dictionaries contain only the rule's resolved writes, not all ZDO data.
  public static void UpdateWrittenScale(ZDO zdo,
    Dictionary<int, float>? floats, Dictionary<int, Vector3>? vectors)
  {
    if (!Config.RestoreScale || SupportsInitialScaleSync(zdo)) return;
    if (vectors != null && vectors.TryGetValue(ZDOVars.s_scaleHash, out var vec))
      BackupVector(zdo, vec);
    else if (floats != null && floats.TryGetValue(ZDOVars.s_scaleScalarHash, out var scalar))
      BackupScalar(zdo, scalar);
  }

  private static void BackupVector(ZDO zdo, Vector3 value)
  {
    SetSyncScale(zdo);
    zdo.Set(ScaleBackupVecHash, value);
    zdo.RemoveFloat(ScaleBackupScalarHash);
    ClientScalarAttempts.Remove(zdo.m_uid);
    ReportedConflicts.Remove(zdo.m_uid);
  }
  private static void BackupScalar(ZDO zdo, float value)
  {
    SetSyncScale(zdo);
    zdo.Set(ScaleBackupScalarHash, value);
    zdo.RemoveVec3(ScaleBackupVecHash);
    ClientScalarAttempts.Remove(zdo.m_uid);
    ReportedConflicts.Remove(zdo.m_uid);
  }

  // Quality of life: Automatically applies m_syncScale for scaled objects that need it.
  public static void Check(ZDO zdo, float? explicitScalar = null, Vector3? explicitVector = null)
  {
    if (!Config.RestoreScale || SupportsInitialScaleSync(zdo)) return;
    // The spawning rule explicitly supplied one or both representations.
    if (explicitVector.HasValue)
    {
      BackupVector(zdo, explicitVector.Value);
      return;
    }
    if (explicitScalar.HasValue)
    {
      BackupScalar(zdo, explicitScalar.Value);
      return;
    }

    // Regeneration can merge a client's runtime scale with older authored
    // backup data. The existing backup is more reliable than that runtime write.
    // Do not promote it to a *new* CustomScaling intent, however.
    bool hasScalar = TryCurrentScalar(zdo, out _);
    bool hasVector = TryCurrentVector(zdo, out _);
    if (TryGetScaleBackup(zdo, out Vector3 previousVector))
    {
      BackupVector(zdo, previousVector);
      return;
    }
    if (TryGetScaleBackup(zdo, out float previousScalar))
    {
      BackupScalar(zdo, previousScalar);
      return;
    }
    // Legacy migration: no provenance exists. Preserve upstream vector-first
    // precedence rather than reinterpret old-world saves.
    if (hasVector && TryCurrentVector(zdo, out var vec))
      BackupVector(zdo, vec);
    else if (hasScalar && TryCurrentScalar(zdo, out var scalar))
      BackupScalar(zdo, scalar);
  }

  private static bool TryCurrentScalar(ZDO zdo, out float scalar)
  {
    scalar = 0f;
    return ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) &&
           floats.TryGetValue(ZDOVars.s_scaleScalarHash, out scalar);
  }
  private static bool TryCurrentVector(ZDO zdo, out Vector3 vec)
  {
    vec = Vector3.zero;
    return ZDOExtraData.s_vec3.TryGetValue(zdo.m_uid, out var vectors) &&
           vectors.TryGetValue(ZDOVars.s_scaleHash, out vec);
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
    // Server-only custom scaling's Held phase wins over incoming client
    // ownership/runtime data. Do not fall into the legacy owner-reset path.
    if (CustomScaling.EnforceHeldAfterDeserialize(__instance)) return;
    if (!NeedsLegacyScaleHack(__instance))
      return;
    // An active client owns this Character's runtime ZSyncTransform. For that
    // case retain the authored backup, but do not fight a client's vector or
    // manipulate its owner on every network update.
    if (ScaleOwnershipPolicy.HasClientOwner(__instance))
    {
      // Keep upstream vector-first priority for pre-existing saves where both
      // backup formats happen to be present. New EWP writes keep only one.
      if (TryGetScaleBackup(__instance, out Vector3 targetVector))
      {
        if (NeedsVectorScaleRestore(__instance, targetVector))
          ReportClientConflict(__instance);
      }
      else if (TryGetScaleBackup(__instance, out float targetScalar))
      {
        if (!TryCurrentScalar(__instance, out var currentScalar) ||
            Math.Abs(currentScalar - targetScalar) > 0.0001f)
        {
          if (!ClientScalarAttempts.TryGetValue(__instance.m_uid, out var lastAttempt) ||
              Math.Abs(lastAttempt - targetScalar) > 0.0001f)
          {
            ClientScalarAttempts[__instance.m_uid] = targetScalar;
            __instance.Set(ZDOVars.s_scaleScalarHash, targetScalar);
          }
        }
        // Runtime vector may still be 1.2 even if the target is 1.1/1.3.
        // Never delete it in this path; the client can recreate it immediately.
        if (TryCurrentVector(__instance, out var runtimeVector) &&
            (Math.Abs(runtimeVector.x - targetScalar) > 0.0001f ||
             Math.Abs(runtimeVector.y - targetScalar) > 0.0001f ||
             Math.Abs(runtimeVector.z - targetScalar) > 0.0001f))
          ReportClientConflict(__instance);
      }
      else
        SetScaleBackup(__instance);
      return;
    }
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

  private static void ReportClientConflict(ZDO zdo)
  {
    // Limited diagnostics for a testable upstream candidate, not a tracer.
    if (ReportedConflicts.Count >= 64 || !ReportedConflicts.Add(zdo.m_uid)) return;
    Log.Debug($"EWP_SCALE_POLICY|event=client-runtime-conflict|zdo={zdo.m_uid}|owner={zdo.GetOwner()}|action=preserve-owner|visual-scale-guaranteed=false");
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
    // Character scale repair and releasing its owner are separate decisions.
    // Releasing even an unowned/server-owned Character can destabilize its next
    // client initialization. Physics/non-Character paths keep the original hack.
    if (ScaleOwnershipPolicy.IsOrdinaryCharacter(zdo)) return;
    var previousOwner = zdo.GetOwner();
    zdo.SetOwner(0L);
    zdo.DataRevision += 100;
    DelayedOwner.Add(0.1f, zdo, previousOwner);
  }
}
