using HarmonyLib;

namespace ExpandWorld.Prefab;

/// <summary>
/// Starts the Player-write quarantine for native teleports routed by the
/// server, including teleports sent by another server mod.
/// </summary>
[HarmonyPatch(typeof(ZRoutedRpc), "RouteRPC")]
internal static class ServerTeleportRouteObserver
{
  private static void Prefix(ZRoutedRpc.RoutedRPCData rpcData)
  {
    TeleportManager.TrackRouted(rpcData);
  }
}
