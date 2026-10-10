using System;
using System.Collections.Generic;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

/// <summary>
/// Prevents EWP from writing a stale source-position snapshot back to a
/// client-owned Player ZDO while a teleport is still synchronizing.
/// Native teleport and resetcloth RPCs are never delayed or canceled.
/// </summary>
internal static class TeleportManager
{
  // Time only produces a warning. A Player is released by synchronization or
  // peer lifecycle, never by an assumed connection speed.
  internal const double StallWarningSeconds = 30d;
  private const float DestinationHorizontalTolerance = 8f;
  internal const float PlayerResyncTolerance = 32f;
  private const int MaximumPendingEvents = 256;
  private static readonly int TeleportHash = "RPC_TeleportTo".GetStableHashCode();
  private static readonly int ClientTeleportHash = "RPC_TeleportPlayer".GetStableHashCode();
  private static readonly Dictionary<ZDOID, TeleportWatch> Watches = [];

  internal static bool IsTeleport(int hash) => hash == TeleportHash || hash == ClientTeleportHash;

  internal static void Track(ZDOID actor, int hash, object[] parameters)
  {
    if (!IsTeleport(hash) || actor == ZDOID.None || ZNet.instance == null ||
        !TryGetDestination(parameters, out var destination) ||
        !HasConnectedRemotePlayer(actor))
      return;

    Watches.TryGetValue(actor, out var previous);
    if (previous != null && !previous.Inferred &&
        HorizontalDistance(previous.Destination, destination) < 0.01f)
      return;

    var watch = new TeleportWatch(destination,
      ZNet.instance.m_netTime + StallWarningSeconds, false);
    if (previous != null)
      AdoptPending(previous, watch);
    Watches[actor] = watch;
  }

  /// <summary>
  /// Observes native teleports routed by the server. This lets server command
  /// mods use Valheim's existing teleport RPC without depending on EWP.
  /// </summary>
  internal static void TrackRouted(ZRoutedRpc.RoutedRPCData data)
  {
    if (ZNet.instance == null || !ZNet.instance.IsServer() || !IsTeleport(data.m_methodHash))
      return;

    var actor = data.m_targetZDO;
    if (data.m_methodHash == ClientTeleportHash)
      actor = ZNet.instance.GetPeer(data.m_targetPeerID)?.m_characterID ?? ZDOID.None;
    if (actor == ZDOID.None) return;

    var position = data.m_parameters.GetPos();
    try
    {
      Track(actor, data.m_methodHash, [data.m_parameters.ReadVector3()]);
    }
    catch (Exception exception)
    {
      Log.Warning($"Unable to inspect routed Player teleport: {exception.Message}");
    }
    finally
    {
      data.m_parameters.SetPos(position);
    }
  }

  /// <summary>
  /// Queues EWP's resetcloth callback. Valheim's original resetcloth RPC must
  /// still continue normally.
  /// </summary>
  internal static bool TryDeferResetCloth(ZDO zdo)
  {
    if (ZNet.instance == null || !PersistPlayers.IsRealPlayer(zdo))
      return false;

    // Resetcloth also occurs during ordinary Player startup and respawn. It is
    // not evidence of a teleport, so it must never create a synchronization
    // watch. Route observation and desynchronized-peer discovery provide the
    // teleport evidence instead.
    if (!ShouldDeferResetCloth(TryGetUnsafeWatch(zdo, out var watch)))
      return false;

    if (watch.PendingResetCloth)
      watch.CoalescedResetClothCallbacks = CoalesceResetClothCallbacks(watch.PendingResetCloth,
        watch.CoalescedResetClothCallbacks);
    else
      watch.PendingResetCloth = true;
    return true;
  }

  /// <summary>
  /// Queues complete EWP Player events while a known or inferred teleport is
  /// unsafe. This protects every rule action, not only resetcloth rules.
  /// </summary>
  internal static bool TryDeferPlayerEvent(ActionType type, string[] args, ZDO zdo)
  {
    if (IsResetCloth(type, args) || !PersistPlayers.IsRealPlayer(zdo) ||
        ZNet.instance == null || !TryGetUnsafeWatch(zdo, out var watch))
      return false;

    if (watch.PendingEvents.Count >= MaximumPendingEvents)
    {
      if (!watch.CapacityWarningWritten)
      {
        watch.CapacityWarningWritten = true;
        Log.Warning($"Skipped additional deferred Player events for {zdo.m_uid}: the {MaximumPendingEvents}-event teleport queue is full.");
      }
      return true;
    }

    watch.PendingEvents.Add(new PendingPlayerEvent(type, (string[])args.Clone()));
    return true;
  }

  internal static void Execute()
  {
    DiscoverInferredWatches();
    var zdoManager = ZDOMan.instance;
    if (Watches.Count == 0 || ZNet.instance == null || zdoManager == null) return;

    // Released rules may dispatch another teleport, so iterate a snapshot.
    foreach (var pair in new List<KeyValuePair<ZDOID, TeleportWatch>>(Watches))
    {
      var watch = pair.Value;
      if (!Watches.TryGetValue(pair.Key, out var current) || !ReferenceEquals(current, watch))
        continue;

      var zdo = zdoManager.GetZDO(pair.Key);
      if (zdo == null || zdoManager.m_deadZDOs.ContainsKey(pair.Key))
      {
        Watches.Remove(pair.Key);
        DropPending(watch, pair.Key, "Player ZDO is missing or dead");
        continue;
      }

      var peer = PeerManager.GetPeer(zdo);
      if (peer == null || peer.m_characterID != pair.Key)
      {
        Watches.Remove(pair.Key);
        DropPending(watch, pair.Key, "the owning peer disconnected");
        continue;
      }

      if (IsSafe(watch, zdo, peer))
      {
        Watches.Remove(pair.Key);
        ReleaseResetCloth(watch, zdo);
        ReleasePending(watch, zdo);
        continue;
      }

      if (!watch.StallWarningWritten && watch.StallWarningAt < ZNet.instance.m_netTime)
      {
        watch.StallWarningWritten = true;
        Log.Warning($"Player event synchronization for {pair.Key} has been waiting for its owner for more than {StallWarningSeconds:0} seconds. Queued work remains quarantined.");
      }
    }
  }

  internal static void Clear() => Watches.Clear();

  internal static bool IsPlayerWriteQuarantined(ZDOID id)
  {
    if (!Watches.ContainsKey(id)) return false;
    var zdo = ZDOMan.instance?.GetZDO(id);
    return zdo != null && PersistPlayers.IsRealPlayer(zdo);
  }

  private static bool TryGetDestination(object[] parameters, out Vector3 destination)
  {
    foreach (var parameter in parameters)
    {
      if (parameter is Vector3 vector)
      {
        destination = vector;
        return true;
      }
    }
    destination = Vector3.zero;
    return false;
  }

  private static bool TryGetUnsafeWatch(ZDO zdo, out TeleportWatch watch)
  {
    // Execute is the single release point so queued events retain their order.
    if (Watches.TryGetValue(zdo.m_uid, out watch!)) return true;
    if (!TryGetDesynchronizedPeer(zdo, out var peer))
    {
      watch = null!;
      return false;
    }
    watch = CreateInferredWatch(zdo, peer);
    return true;
  }

  private static void DiscoverInferredWatches()
  {
    if (ZNet.instance == null || ZDOMan.instance == null || !ZNet.instance.IsServer()) return;
    foreach (var peer in ZNet.instance.GetPeers())
    {
      if (peer.m_characterID == ZDOID.None || Watches.ContainsKey(peer.m_characterID)) continue;
      var zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
      if (zdo == null || !PersistPlayers.IsRealPlayer(zdo) ||
          !IsOutsidePeerActiveArea(zdo.m_position, peer))
        continue;
      CreateInferredWatch(zdo, peer);
    }
  }

  private static bool TryGetDesynchronizedPeer(ZDO zdo, out ZNetPeer peer)
  {
    peer = PeerManager.GetPeer(zdo)!;
    return peer != null && peer.m_characterID == zdo.m_uid &&
      IsOutsidePeerActiveArea(zdo.m_position, peer);
  }

  private static bool HasConnectedRemotePlayer(ZDOID actor)
  {
    var zdo = ZDOMan.instance?.GetZDO(actor);
    var peer = zdo == null ? null : PeerManager.GetPeer(zdo);
    return ShouldTrackRemotePlayerTeleport(zdo != null && PersistPlayers.IsRealPlayer(zdo),
      peer != null && peer.m_characterID == actor);
  }

  private static TeleportWatch CreateInferredWatch(ZDO zdo, ZNetPeer peer)
  {
    var watch = new TeleportWatch(peer.m_refPos,
      ZNet.instance!.m_netTime + StallWarningSeconds, true);
    Watches[zdo.m_uid] = watch;
    return watch;
  }

  private static bool IsSafe(TeleportWatch watch, ZDO zdo, ZNetPeer peer)
  {
    if (watch.Inferred)
    {
      watch.Destination = peer.m_refPos;
      return IsPlayerResynchronized(zdo.m_position, peer.m_refPos);
    }

    return IsKnownTeleportSafe(zdo.m_position, watch.Destination, peer.m_refPos);
  }

  private static void ReleaseResetCloth(TeleportWatch watch, ZDO zdo)
  {
    if (!watch.PendingResetCloth) return;
    watch.PendingResetCloth = false;
    if (watch.CoalescedResetClothCallbacks > 0)
      Log.Warning($"Coalesced {watch.CoalescedResetClothCallbacks} duplicate resetcloth callbacks for {zdo.m_uid} during Player synchronization.");
    watch.CoalescedResetClothCallbacks = 0;
    Manager.Handle(ActionType.State, ["resetcloth"], zdo);
  }

  private static void ReleasePending(TeleportWatch watch, ZDO zdo)
  {
    if (watch.PendingEvents.Count == 0) return;
    var pending = watch.PendingEvents.ToArray();
    watch.PendingEvents.Clear();
    foreach (var entry in pending)
      Manager.Handle(entry.Type, entry.Args, zdo);
  }

  private static void AdoptPending(TeleportWatch previous, TeleportWatch next)
  {
    next.PendingResetCloth = previous.PendingResetCloth;
    next.CoalescedResetClothCallbacks = previous.CoalescedResetClothCallbacks;
    next.CapacityWarningWritten = previous.CapacityWarningWritten;
    next.PendingEvents.AddRange(previous.PendingEvents);
  }

  private static void DropPending(TeleportWatch watch, ZDOID actor, string reason)
  {
    var count = (watch.PendingResetCloth ? 1 : 0) + watch.PendingEvents.Count;
    if (count > 0)
      Log.Warning($"Skipped {count} deferred Player events for {actor}: {reason}.");
  }

  internal static float HorizontalDistance(Vector3 value, Vector3 destination)
  {
    var x = value.x - destination.x;
    var z = value.z - destination.z;
    return Mathf.Sqrt(x * x + z * z);
  }

  internal static bool IsAtDestination(Vector3 value, Vector3 destination) =>
    HorizontalDistance(value, destination) <= DestinationHorizontalTolerance;

  internal static bool IsPlayerResynchronized(Vector3 zdoPosition, Vector3 peerPosition) =>
    HorizontalDistance(zdoPosition, peerPosition) <= PlayerResyncTolerance;

  internal static bool IsKnownTeleportSafe(Vector3 zdoPosition, Vector3 destination,
    Vector3 peerPosition) =>
    IsAtDestination(zdoPosition, destination) &&
    IsPlayerResynchronized(zdoPosition, peerPosition);

  internal static bool HasTeleportMovement(Vector3 sourcePosition,
    Vector3 zdoPosition, Vector3 peerPosition) =>
    HorizontalDistance(sourcePosition, zdoPosition) > PlayerResyncTolerance ||
    HorizontalDistance(sourcePosition, peerPosition) > PlayerResyncTolerance;

  internal static bool ShouldTrackRemotePlayerTeleport(bool realPlayer,
    bool hasConnectedOwnerPeer) => realPlayer && hasConnectedOwnerPeer;

  internal static bool ShouldDeferResetCloth(bool hasUnsafeWatch) => hasUnsafeWatch;

  internal static int CoalesceResetClothCallbacks(bool pending, int callbacks) =>
    pending ? callbacks + 1 : callbacks;

  // Mirrors ZNetScene.PointInsideActiveArea with the simulation distance
  // negotiated for this peer instead of the dedicated server's local setting.
  private static bool IsOutsidePeerActiveArea(Vector3 zdoPosition, ZNetPeer peer)
  {
    if (ZoneSystem.instance == null) return false;
    zdoPosition.y = 0f;
    var simulationDistance = peer.m_simulationDistance;
    var zoneSize = ZoneSystem.instance.m_zoneSize;
    var zonePosition = ZoneSystem.GetZonePos(ZoneSystem.GetZone(peer.m_refPos));
    return !IsInsideActiveArea(zdoPosition, zonePosition, zoneSize,
      simulationDistance.NearSimulationDistance, simulationDistance.IsClassic);
  }

  internal static bool IsInsideActiveArea(Vector3 point, Vector3 zonePosition,
    float zoneSize, int nearSimulationDistance, bool classic)
  {
    point.y = 0f;
    zonePosition.y = 0f;
    var multiplier = nearSimulationDistance == 1 ? 1f : 1.5f;
    var inside = Utils.ChebyshevDistance(zonePosition, point) <= multiplier * zoneSize;
    if (nearSimulationDistance == 2 && !classic)
    {
      var radius = zoneSize * 1.75f;
      return inside && (zonePosition - point).sqrMagnitude < radius * radius;
    }
    return inside;
  }

  private static bool IsResetCloth(ActionType type, string[] args) =>
    type == ActionType.State && args.Length > 0 && args[0] == "resetcloth";

  private sealed class TeleportWatch(Vector3 destination, double stallWarningAt, bool inferred)
  {
    internal Vector3 Destination { get; set; } = destination;
    internal double StallWarningAt { get; } = stallWarningAt;
    internal bool StallWarningWritten { get; set; }
    internal bool Inferred { get; set; } = inferred;
    internal bool PendingResetCloth { get; set; }
    internal int CoalescedResetClothCallbacks { get; set; }
    internal bool CapacityWarningWritten { get; set; }
    internal List<PendingPlayerEvent> PendingEvents { get; } = [];
  }

  private sealed class PendingPlayerEvent(ActionType type, string[] args)
  {
    internal ActionType Type { get; } = type;
    internal string[] Args { get; } = args;
  }

}
