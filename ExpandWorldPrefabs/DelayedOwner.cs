using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace ExpandWorld.Prefab;

// Game has annoying feature that pre-existing objects have their body set to sleep.
// This causes server spawned item drops to float in the air.
// The body is awakened when a client gets ownership of the object.
// So for server spawned item drops (and boats), the owner is assigned only after the ZDO has been sent to the target.
// Until then the ZDO is unowned and the server is blocked from assigning it (see DelayedOwnerReleasePatch).
public static class DelayedOwner
{
  internal enum Outcome { Keep, Assign, Release }

  private static readonly Dictionary<ZDOID, long> Owners = [];
  public static void Clear() => Owners.Clear();

  // Nearest client most likely wants to interact with the object.
  // So this should give the smoothest experience.
  public static long FindNearestOwner(ZDO zdo)
  {
    if (ZDOMan.instance == null) return 0;
    var peer = ZDOMan.instance.m_peers
      .Where(p => p.m_peer != null && p.m_peer.IsReady())
      .OrderBy(p => (float)Utils.DistanceXZ(p.m_peer!.m_refPos, zdo.m_position))
      .FirstOrDefault();
    return peer?.m_peer?.m_uid ?? 0;
  }

  public static void Check(ZDO zdo, long owner)
  {
    // Scripts can pass any owner, so fall back to the nearest client if it isn't connected.
    if (owner != 0 && owner != ZDOMan.instance.m_sessionID && PeerManager.GetPeer(owner) == null)
      owner = 0;
    bool shouldNotBeOwned = SupportAttach.IsAttached(zdo) || PersistPlayers.IsPlayer(zdo);
    if (shouldNotBeOwned)
      owner = SupportAttach.HackOwner;

    // For most cases, some client should instantly own the object so that it is being handled.
    // Also some features like creature health from stars requires a client to be the owner.
    if (owner == 0)
      owner = FindNearestOwner(zdo);
    var prefab = ZNetScene.instance.GetPrefab(zdo.m_prefab);

    bool isItem = prefab.GetComponent<ItemDrop>();
    bool isShip = prefab.GetComponent<Ship>();
    bool shouldBackupScale = RestoreScale.ShouldRestoreScale(zdo);
    bool delay = NeedsDelay(shouldNotBeOwned, isItem, isShip, shouldBackupScale);
    // This is normally set on Awake which won't trigger for server spawned.
    // Without this, "remove old loot" is instantly triggered.
    if (isItem)
      zdo.Set(ZDOVars.s_spawnTime, ZNet.instance.GetTime().Ticks);

    // Some objects like item drops or ships have initially their body set to sleep.
    // This is only cleared when a client acquires ownership of already existing game object.
    // Which means the ownership can't be assigned before the ZDO is sent.
    if (delay)
      Add(zdo, owner);
    else
    {
      Cancel(zdo.m_uid);
      zdo.SetOwnerInternal(owner);
    }
  }

  internal static bool NeedsDelay(bool shouldNotBeOwned, bool isItem, bool isShip, bool scaleBackup) =>
    !shouldNotBeOwned && (isItem || isShip || scaleBackup);

  private static void Cancel(ZDOID uid) => Owners.Remove(uid);

  public static void Add(ZDO zdo, long owner)
  {
    Cancel(zdo.m_uid);
    zdo.SetOwner(0);
    if (owner == 0) return;
    if (owner == ZDOMan.instance.m_sessionID)
    {
      Owners[zdo.m_uid] = owner;
      return;
    }
    if (PeerManager.GetPeer(owner) == null) return;
    // SetOwner(0) above bumps the owner revision, so the peer always needs a resend.
    Owners[zdo.m_uid] = owner;
    ZDOMan.instance.ForceSendZDO(owner, zdo.m_uid);
  }

  // ready: the target has the ZDO. giveUp: it never will.
  internal static Outcome Decide(long currentOwner, bool ready, bool giveUp)
  {
    if (currentOwner != 0) return Outcome.Release;
    if (ready) return Outcome.Assign;
    return giveUp ? Outcome.Release : Outcome.Keep;
  }

  private static void Resolve(ZDOID uid, long owner, bool ready, bool giveUp)
  {
    var zdo = ZDOMan.instance.GetZDO(uid);
    if (zdo == null)
    {
      Owners.Remove(uid);
      return;
    }
    var outcome = Decide(zdo.GetOwner(), ready, giveUp);
    if (outcome == Outcome.Keep) return;
    Owners.Remove(uid);
    if (outcome == Outcome.Assign) zdo.SetOwner(owner);
  }

  // Handles targets that are not fed by SendZDOs: the server itself and disconnected peers.
  public static void Execute()
  {
    if (Owners.Count == 0 || ZDOMan.instance == null || ZNetScene.instance == null) return;
    var serverUid = ZDOMan.instance.m_sessionID;
    foreach (var pair in Owners.ToArray())
    {
      if (pair.Value == serverUid)
      {
        var zdo = ZDOMan.instance.GetZDO(pair.Key);
        var ready = zdo != null && ZNetScene.instance.HaveInstance(zdo);
        var outside = zdo != null && !ZNetScene.InActiveArea(zdo.GetPosition(), ZNet.instance.GetReferencePosition());
        Resolve(pair.Key, pair.Value, ready, outside);
      }
      else if (PeerManager.GetPeer(pair.Value) == null)
        Owners.Remove(pair.Key);
    }
  }

  internal static void ReleaseSetOwner(ZDO zdo, long uid)
  {
    if (Owners.ContainsKey(zdo.m_uid)) return;
    zdo.SetOwner(uid);
  }

  internal static void AfterSend(ZDOMan.ZDOPeer peer, bool sent)
  {
    if (!sent || Owners.Count == 0 || peer.m_peer == null) return;
    var uid = peer.m_peer.m_uid;
    foreach (var pair in Owners.Where(p => p.Value == uid).ToArray())
    {
      var zdo = ZDOMan.instance.GetZDO(pair.Key);
      // Something was sent to this peer, so a ZDO that was not included gets released.
      Resolve(pair.Key, pair.Value, zdo != null && !peer.ShouldSend(zdo), true);
    }
  }
}

[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOs))]
public class DelayedOwnerSendPatch
{
  static void Postfix(ZDOMan.ZDOPeer peer, bool __result) => DelayedOwner.AfterSend(peer, __result);
}

// Prevents vanilla from handing pending ZDOs to whoever is nearby.
[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.ReleaseNearbyZDOS))]
public class DelayedOwnerReleasePatch
{
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var setOwner = AccessTools.Method(typeof(ZDO), nameof(ZDO.SetOwner));
    var replacement = AccessTools.Method(typeof(DelayedOwner), nameof(DelayedOwner.ReleaseSetOwner));
    return new CodeMatcher(instructions)
      .MatchStartForward(new CodeMatch(i => i.Calls(setOwner)))
      .ThrowIfInvalid("Failed to patch ZDOMan.ReleaseNearbyZDOS for pending ownership.")
      .Repeat(m => m.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, replacement)))
      .InstructionEnumeration();
  }
}
