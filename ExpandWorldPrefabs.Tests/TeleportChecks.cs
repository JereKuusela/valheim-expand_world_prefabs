using System;
using ExpandWorld.Prefab;

namespace ExpandWorldPrefabs.Tests;

internal static class TeleportChecks
{
  internal static void RunDestinationSynchronizationChecks()
  {
    void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    var destination = new UnityEngine.Vector3(100f, 50f, 200f);
    Check(TeleportManager.IsAtDestination(new UnityEngine.Vector3(100f, -500f, 200f), destination), "Vertical floor adjustment should not block release");
    Check(TeleportManager.IsAtDestination(new UnityEngine.Vector3(107.99f, 50f, 200f), destination), "Position inside horizontal tolerance was rejected");
    Check(!TeleportManager.IsAtDestination(new UnityEngine.Vector3(108.01f, 50f, 200f), destination), "Position outside horizontal tolerance was accepted");
    Check(Math.Abs(TeleportManager.HorizontalDistance(new UnityEngine.Vector3(103f, 0f, 204f), destination) - 5f) < 0.001f, "Horizontal distance calculation changed");
    Check(TeleportManager.IsPlayerResynchronized(new UnityEngine.Vector3(0f, 0f, 0f),
      new UnityEngine.Vector3(32f, -500f, 0f)), "resynchronization tolerance rejected a safe Player");
    Check(!TeleportManager.IsPlayerResynchronized(new UnityEngine.Vector3(0f, 0f, 0f),
      new UnityEngine.Vector3(32.01f, 0f, 0f)), "unsafe Player/peer divergence was released");
    Check(!TeleportManager.IsKnownTeleportSafe(destination, destination,
      new UnityEngine.Vector3(133f, 0f, 200f)),
      "known teleport released before the peer reference synchronized");
    Check(!TeleportManager.IsKnownTeleportSafe(new UnityEngine.Vector3(109f, 0f, 200f),
      destination, new UnityEngine.Vector3(109f, 0f, 200f)),
      "known teleport released outside its announced destination");
    Check(TeleportManager.IsKnownTeleportSafe(destination, destination,
      new UnityEngine.Vector3(132f, -500f, 200f)),
      "known synchronized teleport did not release at the tolerance boundary");
    Check(TeleportManager.IsInsideActiveArea(new UnityEngine.Vector3(64f, 500f, 0f),
      UnityEngine.Vector3.zero, 64f, 1, true), "minimum simulation boundary was excluded");
    Check(!TeleportManager.IsInsideActiveArea(new UnityEngine.Vector3(64.01f, 0f, 0f),
      UnityEngine.Vector3.zero, 64f, 1, true), "position outside minimum simulation area was accepted");
    Check(!TeleportManager.HasTeleportMovement(UnityEngine.Vector3.zero,
      new UnityEngine.Vector3(32f, 0f, 0f), UnityEngine.Vector3.zero),
      "safe local movement was classified as a teleport");
    Check(TeleportManager.HasTeleportMovement(UnityEngine.Vector3.zero,
      new UnityEngine.Vector3(32.01f, 0f, 0f), UnityEngine.Vector3.zero),
      "teleport-sized Player movement was not detected");
    var teleportHash = "RPC_TeleportTo".GetStableHashCode();
    Check(!DelayedRpc.ShouldExecuteImmediately(0f, teleportHash, false),
      "an immediate teleport was allowed to interrupt its source rule");
    Check(DelayedRpc.ShouldExecuteImmediately(0f, "ordinary".GetStableHashCode(), false),
      "an ordinary immediate RPC was unnecessarily delayed");
    Check(!DelayedRpc.ShouldExecuteImmediately(0f, "ordinary".GetStableHashCode(), true),
      "a quarantined immediate RPC was allowed to write during teleport");
    Check(!TeleportManager.ShouldTrackRemotePlayerTeleport(true, false),
      "singleplayer Player teleport created a remote synchronization watch");
    Check(TeleportManager.ShouldTrackRemotePlayerTeleport(true, true),
      "connected remote Player teleport did not create a synchronization watch");
    Check(!TeleportManager.ShouldTrackRemotePlayerTeleport(false, true),
      "non-Player teleport created a Player synchronization watch");
    Check(!TeleportManager.ShouldDeferResetCloth(false),
      "ordinary resetcloth created a teleport synchronization watch");
    Check(TeleportManager.ShouldDeferResetCloth(true),
      "resetcloth during a confirmed unsafe teleport was not deferred");
    Check(TeleportManager.CoalesceResetClothCallbacks(false, 0) == 0,
      "first resetcloth callback was counted as a duplicate");
    Check(TeleportManager.CoalesceResetClothCallbacks(true, 0) == 1,
      "second resetcloth callback was not coalesced");
  }
}
