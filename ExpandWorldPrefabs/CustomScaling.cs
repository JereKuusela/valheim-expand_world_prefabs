using System;
using System.Collections.Generic;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

// Persistent, server-authoritative scale intent with temporary ownership. No client-side patches.
// An authored scale is never inferred from the live (client-written) ZDO vector.
internal static class CustomScaling
{
  private static readonly int IntentHash = ZdoHelper.Hash("ewp_custom_scaling");
  private const float NearbyRadius = 90f;
  private const float FarRadius = 160f;
  private const float ReleaseDelaySeconds = 1.5f;
  private const float ScanIntervalSeconds = 0.5f;

  private sealed class State
  {
    public bool Held;
    public bool HostOwned;
    public bool NeedsStage;
    public float NearbySince = -1f;
    public bool ConflictLogged;
    public bool HeldRepairLogged;
  }

  private static readonly Dictionary<ZDOID, State> States = [];
  private static readonly List<ZDOID> Gone = [];
  private static bool initialized;
  private static bool warnedPrerequisites;
  private static float nextScan;

  private static bool Enabled => Config.RestoreScale && Config.ServerSideData && Config.ServerOwned;

  internal static void Initialize()
  {
    if (initialized) return;
    initialized = true;
    ServerSideData.Loaded += OnLoaded;
    ServerSideData.Destroyed += OnDestroyed;
  }

  internal static void Clear()
  {
    States.Clear();
    Gone.Clear();
    nextScan = 0f;
    warnedPrerequisites = false;
  }

  // Only server-side data is authoritative: an incoming ZDO.Deserialize cannot
  // update this entry. Presence of the vector, not its numeric value, is the flag.
  internal static bool TryGetIntent(ZDO zdo, out Vector3 value) =>
    ServerSideData.TryGetVec(zdo.m_uid, IntentHash, out value);

  // A hold is an existing runtime ownership state, not a configuration query.
  // This method is also called from ZDO.SetOwner's Harmony prefix before
  // BepInEx has initialized Config (e.g. isolated NUnit tests).
  // Configuration changes release holds via OnConfigurationChanged().
  internal static bool IsHeld(ZDO zdo) => zdo != null &&
    States.TryGetValue(zdo.m_uid, out var state) && state.Held;

  private static bool Valid(Vector3 vec) =>
    PositiveFinite(vec.x) && PositiveFinite(vec.y) && PositiveFinite(vec.z);

  private static bool PositiveFinite(float val) =>
    val > 0f && !float.IsNaN(val) && !float.IsInfinity(val);

  private static bool Eligible(ZDO zdo)
  {
    if (zdo == null || ZNetScene.instance == null ||
        ServerOwned.IsMarked(zdo) || SupportAttach.IsAttached(zdo) ||
        PersistPlayers.IsPlayer(zdo)) return false;
    var prefab = ZNetScene.instance.GetPrefab(zdo.m_prefab);
    if (!prefab || !prefab.GetComponent<Character>() || prefab.GetComponent<Player>() ||
        prefab.GetComponent<ItemDrop>() || prefab.GetComponent<Ship>()) return false;
    var view = prefab.GetComponent<ZNetView>();
    return view && !view.m_syncInitialScale;
  }

  // Both spawn and later rule updates use the same authored scalar/vector conversion.
  // Vector takes precedence, including when invalid; never silently use a scalar fallback.
  internal static bool TryResolveAuthoredScale(float? scalar, Vector3? vector, out Vector3 target)
  {
    if (vector.HasValue)
      target = vector.Value;
    else if (scalar.HasValue)
      target = new Vector3(scalar.Value, scalar.Value, scalar.Value);
    else
    {
      target = default;
      return false;
    }
    return Valid(target);
  }

  internal static bool TryResolveWrittenScale(Dictionary<int, float>? floats,
    Dictionary<int, Vector3>? vectors, out Vector3 target)
  {
    float? scalar = null;
    Vector3? vector = null;
    if (floats != null && floats.TryGetValue(ZDOVars.s_scaleScalarHash, out var authoredScalar))
      scalar = authoredScalar;
    if (vectors != null && vectors.TryGetValue(ZDOVars.s_scaleHash, out var authoredVector))
      vector = authoredVector;
    return TryResolveAuthoredScale(scalar, vector, out target);
  }

  // Called after RestoreScale.Check() has interpreted explicit rule data.
  // A copy/regeneration may carry an existing server-only intent; never infer
  // a new one from a copied runtime 'scale' field.
  internal static bool TryBegin(ZDO zdo, float? explicitScalar, Vector3? explicitVector, bool explicitOwner)
  {
    if (!Enabled || explicitOwner || !Eligible(zdo)) return false;
    Vector3 target;
    if (explicitVector.HasValue || explicitScalar.HasValue)
    {
      if (!TryResolveAuthoredScale(explicitScalar, explicitVector, out target)) return false;
    }
    else if (!TryGetIntent(zdo, out target) || !Valid(target))
      return false;
    ApplyTarget(zdo, target);
    ServerSideData.SetVec(zdo, IntentHash, target);
    var state = GetState(zdo.m_uid);
    Hold(zdo, state, "create");
    return true; // Avoid DelayedOwner.Check assigning a client before Awake.
  }

  // Rules that explicitly write scale into an existing object are authoritative;
  // routine ZDO.Deserialize packets are NOT. No implicit ownership takeover when
  // the rule itself selected a special owner.
  internal static void UpdateWrittenScale(ZDO zdo, Dictionary<int, float>? floats,
    Dictionary<int, Vector3>? vectors, bool explicitOwner)
  {
    if (!Enabled || explicitOwner || !Eligible(zdo)) return;
    if (!TryResolveWrittenScale(floats, vectors, out var target)) return;
    ServerSideData.SetVec(zdo, IntentHash, target);
    ApplyTarget(zdo, target);
    GetState(zdo.m_uid);
    // Existing active client ownership is not reset here; wait until the
    // area empties, otherwise this would introduce a new equipment/AI race.
  }

  private static State GetState(ZDOID id)
  {
    if (!States.TryGetValue(id, out var state))
    {
      state = new State();
      States.Add(id, state);
    }
    return state;
  }

  private static void ApplyTarget(ZDO zdo, Vector3 target)
  {
    zdo.Set(ZDOVars.s_scaleHash, target);
    zdo.RemoveFloat(ZDOVars.s_scaleScalarHash);
    // Align the existing RC's restore target with authored intent.
    RestoreScale.UpdateWrittenScale(zdo, null, new Dictionary<int, Vector3> { [ZDOVars.s_scaleHash] = target });
  }

  private static void Hold(ZDO zdo, State state, string reason)
  {
    if (ZDOMan.instance == null) return;
    state.Held = true; // ServerOwned.SetOwner prefix intercepts early Vanilla release attempts.
    state.HostOwned = false;
    state.NeedsStage = false;
    state.NearbySince = -1f;
    state.HeldRepairLogged = false;
    ApplyTarget(zdo, GetTarget(zdo));
    zdo.SetOwnerInternal(ZDOMan.instance.m_sessionID);
    Log.Debug($"EWP_CUSTOM_SCALE|event=hold|zdo={zdo.m_uid}|reason={reason}|owner={zdo.GetOwner()}");
  }

  private static Vector3 GetTarget(ZDO zdo)
  {
    TryGetIntent(zdo, out var target);
    return target;
  }

  // ZDO.Deserialize may replace both Owner and the runtime vector directly
  // from a client's packet, bypassing ServerOwned.SetOwner (which only patches
  // ZDO.SetOwner). During the transient server-held stage, restore our actual
  // server ownership and the *authored* target, never the incoming runtime scale.
  // Returning true tells RestoreScale.Deserialize to skip its legacy release
  // hack: during Held we must not trigger another DelayedOwner cycle.
  internal static bool EnforceHeldAfterDeserialize(ZDO zdo)
  {
    if (!Enabled || ZNet.instance == null || !ZNet.instance.IsServer() ||
        ZDOMan.instance == null || zdo == null ||
        !States.TryGetValue(zdo.m_uid, out var state) || !state.Held ||
        !TryGetIntent(zdo, out var target) || !Valid(target)) return false;

    var serverOwner = ZDOMan.instance.m_sessionID;
    var wrongOwner = zdo.GetOwner() != serverOwner;
    var wrongVector = zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero) != target;
    var wrongScalar = ZDOExtraData.s_floats.TryGetValue(zdo.m_uid, out var floats) &&
                      floats.ContainsKey(ZDOVars.s_scaleScalarHash);
    if (wrongVector || wrongScalar)
      ApplyTarget(zdo, target);
    if (wrongOwner)
      zdo.SetOwnerInternal(serverOwner);
    if ((wrongOwner || wrongVector || wrongScalar) && !state.HeldRepairLogged)
    {
      state.HeldRepairLogged = true;
      Log.Debug($"EWP_CUSTOM_SCALE|event=held-deserialize-guard|zdo={zdo.m_uid}|ownerRestored={wrongOwner}|vectorRestored={wrongVector || wrongScalar}|target={target}");
    }
    return true;
  }

  private static void OnLoaded(ZDO zdo)
  {
    if (!Enabled || !TryGetIntent(zdo, out var target) || !Valid(target)) return;
    // Next server update will stage restored ZDOs, once ZDOMan is initialized.
    GetState(zdo.m_uid).NeedsStage = true;
  }

  private static void OnDestroyed(ZDOID id) => States.Remove(id);

  // An explicit rule owner supersedes a temporary scale hold in this rule.
  // Preserve the server-only target for future reloads; do not overwrite the rule's owner.
  internal static void SuspendForExplicitOwner(ZDO zdo)
  {
    if (zdo == null) return;
    States.Remove(zdo.m_uid);
  }

  // EWP 1.65 may change attachments while writing ZDO connections.
  // A newly attached Character must not retain a temporary server hold.
  internal static void RecheckEligibility(ZDO zdo)
  {
    if (!Enabled || zdo == null) return;
    if (!Eligible(zdo))
    {
      if (States.TryGetValue(zdo.m_uid, out var state))
      {
        if (state.Held && ZDOMan.instance != null)
          ReleaseHold(zdo, state, LocalHostNear(zdo, NearbyRadius));
        States.Remove(zdo.m_uid);
      }
      if (ZDOMan.instance != null && SupportAttach.IsAttached(zdo) &&
          !ServerOwned.IsMarked(zdo) && zdo.GetOwner() != SupportAttach.HackOwner)
        zdo.SetOwner(SupportAttach.HackOwner);
      return;
    }
    if (TryGetIntent(zdo, out var target) && Valid(target) &&
        !States.ContainsKey(zdo.m_uid))
      GetState(zdo.m_uid).NeedsStage = true;
  }

  private static bool LocalHostNear(ZDO zdo, float radius) =>
    Player.m_localPlayer &&
    Utils.DistanceXZ(Player.m_localPlayer.transform.position, zdo.m_position) <= radius;

  private static bool HasPeerNear(ZDO zdo, float radius)
  {
    if (LocalHostNear(zdo, radius)) return true;
    if (ZDOMan.instance == null) return false;
    foreach (var peer in ZDOMan.instance.m_peers)
      if (peer.m_peer != null && Utils.DistanceXZ(peer.m_peer.m_refPos, zdo.m_position) <= radius)
        return true;
    return false;
  }

  // A host player owns ZDOs through the server session itself. Keeping that
  // UID after releasing our temporary hold enables local AI without a peer.
  internal static bool ShouldGiveUpServerOwner(long actualOwner, long serverUid,
    bool localHostNear, bool explicitlyServerOwned) =>
    !localHostNear && !explicitlyServerOwned && actualOwner == serverUid;

  internal static bool ShouldBeginStage(bool held, bool needsStage, bool far) =>
    !held && (needsStage || far);

  internal static bool ReleaseDelayElapsed(float nearbySince, float now) =>
    nearbySince >= 0f && now - nearbySince >= ReleaseDelaySeconds;

  private static void ReleaseHold(ZDO zdo, State state, bool localHostNear)
  {
    state.Held = false;
    state.HostOwned = localHostNear && zdo.GetOwner() == ZDOMan.instance.m_sessionID;
    state.NearbySince = -1f;
    if (ShouldGiveUpServerOwner(zdo.GetOwner(), ZDOMan.instance.m_sessionID,
        localHostNear, ServerOwned.IsMarked(zdo)))
      zdo.SetOwner(0);
  }

  // Configuration changes may unpatch SetOwner or server-side data immediately.
  // Release active holds before that happens, even with Restore scale disabled.
  internal static void OnConfigurationChanged()
  {
    if (!Enabled) ReleaseHeldOnDisable();
    else warnedPrerequisites = false;
  }

  private static void ReleaseHeldOnDisable()
  {
    if (ZDOMan.instance == null || ZNet.instance == null || !ZNet.instance.IsServer()) return;
    foreach (var item in States)
    {
      var state = item.Value;
      if (!state.Held && !state.HostOwned) continue;
      var zdo = ZDOMan.instance.GetZDO(item.Key);
      state.Held = false;
      state.HostOwned = false;
      state.NearbySince = -1f;
      state.NeedsStage = true; // Can be staged again if the settings are reenabled.
      if (zdo == null) continue;
      ReleaseHold(zdo, state, LocalHostNear(zdo, NearbyRadius));
      state.HostOwned = false; // Vanilla controls any retained local host ownership.
      Log.Debug($"EWP_CUSTOM_SCALE|event=release|zdo={item.Key}|reason=config-disabled|owner={zdo.GetOwner()}");
    }
  }

  internal static void Execute()
  {
    if (ZDOMan.instance == null || ZNet.instance == null || !ZNet.instance.IsServer()) return;
    if (!Enabled)
    {
      ReleaseHeldOnDisable();
      if (Config.RestoreScale && !warnedPrerequisites)
      {
        warnedPrerequisites = true;
        Log.Warning("EWP_CUSTOM_SCALE|disabled|requires Server side data and Server owned objects");
      }
      return;
    }
    warnedPrerequisites = false;
    if (ZNetScene.instance == null) return;
    var now = Time.realtimeSinceStartup;
    if (now < nextScan) return;
    nextScan = now + ScanIntervalSeconds;
    Gone.Clear();
    foreach (var item in States)
    {
      var zdo = ZDOMan.instance.GetZDO(item.Key);
      // ZNetScene can be present before every prefab has been registered.
      // Do not lose an otherwise persistent intent during world startup.
      if (zdo != null && !ZNetScene.instance.GetPrefab(zdo.m_prefab)) continue;
      if (zdo == null || !TryGetIntent(zdo, out var target) || !Valid(target) || !Eligible(zdo))
      {
        // If this prefab becomes ineligible, never strand it server-owned.
        // An explicit owner: server marker remains authoritative.
        if (zdo != null && item.Value.Held)
        {
          // Disarm our SetOwner prefix *before* releasing. Otherwise the
          // prefix immediately forces the server UID back onto the ZDO.
          ReleaseHold(zdo, item.Value, LocalHostNear(zdo, NearbyRadius));
        }
        Gone.Add(item.Key);
        continue;
      }
      var state = item.Value;
      var nearby = HasPeerNear(zdo, NearbyRadius);
      var far = !HasPeerNear(zdo, FarRadius);
      // When the local host leaves, nearby remote clients need a normal
      // ownership handoff instead of retaining the host's server UID.
      if (state.HostOwned && !LocalHostNear(zdo, FarRadius))
      {
        state.HostOwned = false;
        if (!far && zdo.GetOwner() == ZDOMan.instance.m_sessionID)
          zdo.SetOwner(0);
      }
      if (ShouldBeginStage(state.Held, state.NeedsStage, far))
      {
        Hold(zdo, state, state.NeedsStage ? "loaded" : "no-nearby-clients");
        continue;
      }
      if (!state.Held)
      {
        if (zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero) != target)
        {
          if (!state.ConflictLogged)
          {
            state.ConflictLogged = true;
            Log.Debug($"EWP_CUSTOM_SCALE|event=runtime-diverged|zdo={zdo.m_uid}|target={target}|owner={zdo.GetOwner()}");
          }
        }
        else state.ConflictLogged = false;
        continue;
      }
      if (!nearby)
      {
        state.NearbySince = -1f;
        continue;
      }
      if (state.NearbySince < 0f)
        state.NearbySince = now;
      if (!ReleaseDelayElapsed(state.NearbySince, now)) continue;
      // Do not attempt to reassign repeatedly if Vanilla has already taken over.
      ReleaseHold(zdo, state, LocalHostNear(zdo, NearbyRadius));
      Log.Debug($"EWP_CUSTOM_SCALE|event=release|zdo={zdo.m_uid}|owner={zdo.GetOwner()}|scale={target}");
    }
    foreach (var id in Gone) States.Remove(id);
  }
}
