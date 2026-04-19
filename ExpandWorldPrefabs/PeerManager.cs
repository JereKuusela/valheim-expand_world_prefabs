using System.Collections.Generic;
using Data;
using HarmonyLib;
using Splatform;

namespace ExpandWorld.Prefab;

public class PeerManager
{
  private static readonly Dictionary<long, ZNetPeer> PeersByOwner = [];
  private static readonly Dictionary<ZNetPeer, PlatformUserID> PeerIds = [];

  private static readonly HashSet<ZNetPeer> HandledPeers = [];

  public static ZNetPeer? GetPeer(ZDO zdo) => GetPeer(zdo.GetOwner());
  public static ZNetPeer? GetPeer(long owner)
  {
    if (owner == 0 || !ZNet.instance) return null;
    if (PeersByOwner.TryGetValue(owner, out var peer)) return peer;
    peer = ZNet.instance.GetPeer(owner);
    if (peer != null)
      PeersByOwner[owner] = peer;
    return peer;
  }

  private static void RemoveCachedPeer(long owner, ZNetPeer peer)
  {
    if (owner != 0)
      PeersByOwner.Remove(owner);

    var ownersToRemove = new List<long>();
    foreach (var entry in PeersByOwner)
    {
      if (entry.Value == peer)
        ownersToRemove.Add(entry.Key);
    }
    foreach (var cachedOwner in ownersToRemove)
      PeersByOwner.Remove(cachedOwner);
  }

  [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
  [HarmonyPrefix]
  private static void DisconnectPrefix(ZNetPeer peer)
  {
    RemoveCachedPeer(peer.m_uid, peer);
    PeerIds.Remove(peer);
    HandledPeers.Remove(peer);
    var zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
    if (zdo == null) return;
    Manager.Handle(ActionType.State, ["leave"], zdo);
  }

  private static readonly int PlayerHash = "Player".GetStableHashCode();
  public static void HandlePlayerCreatedState(ZDO zdo)
  {
    if (zdo.m_prefab != PlayerHash) return;
    var owner = zdo.GetOwner();
    var peer = GetPeer(owner);
    if (peer == null) return;
    if (HandledPeers.Contains(peer))
    {
      Manager.Handle(ActionType.State, ["respawn"], zdo);
    }
    else
    {
      HandledPeers.Add(peer);
      Manager.Handle(ActionType.State, ["join"], zdo);
    }
  }

  public static string GetPid(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null && peer.IsReady())
      return GetPeerPid(peer);
    else if (Player.m_localPlayer)
      return "Server";
    return "";
  }
  public static long? GetCid(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
    {
      var characterZdo = ZDOMan.instance.GetZDO(peer.m_characterID);
      if (characterZdo != null)
      {
        var cid = ZdoHelper.TryGetLong(characterZdo, ZDOVars.s_playerID);
        if (cid != null)
          return cid.Value;
      }
    }
    else if (Player.m_localPlayer)
      return Player.m_localPlayer.GetPlayerID();
    return null;
  }
  public static string GetPlatform(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null && peer.IsReady())
      return GetPeerPlatform(peer);
    else if (Player.m_localPlayer)
      return "Server";
    return "";
  }
  public static string GetPName(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_playerName;
    else if (Player.m_localPlayer)
      return Player.m_localPlayer.GetPlayerName();
    return "";
  }
  public static string GetPChar(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_characterID.ToString();
    else if (Player.m_localPlayer)
      return Player.m_localPlayer.GetPlayerID().ToString();
    return "";
  }
  public static string GetPVisible(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_publicRefPos.ToString();
    else if (ZNet.instance)
      return ZNet.instance.IsReferencePositionPublic().ToString();
    return "";
  }
  public static string GetPlayerData(ZDO zdo, string key)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_serverSyncedPlayerData.TryGetValue(key, out var data) ? data : "";
    else if (Player.m_localPlayer)
      return ZNet.instance.m_serverSyncedPlayerData.TryGetValue(key, out var data) ? data : "";
    return "";
  }
  public static bool IsAdmin(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    return peer != null ? ZNet.instance.IsAdmin(peer.m_socket.GetHostName()) : false;
  }

  private static string GetPeerPid(ZNetPeer peer)
  {
    if (PeerIds.TryGetValue(peer, out var id))
      return id.m_userID.ToString();
    PeerIds[peer] = GetUserId(peer);
    return PeerIds[peer].m_userID.ToString();
  }
  private static string GetPeerPlatform(ZNetPeer peer)
  {
    if (PeerIds.TryGetValue(peer, out var id))
      return id.m_platform.ToString();
    PeerIds[peer] = GetUserId(peer);
    return PeerIds[peer].m_platform.ToString();
  }
  private static PlatformUserID GetUserId(ZNetPeer peer)
  {
    if (ZNet.m_onlineBackend == OnlineBackendType.Steamworks)
      return new PlatformUserID(ZNet.instance.m_steamPlatform, peer.m_socket.GetHostName());
    else
      return new PlatformUserID(peer.m_socket.GetHostName());
  }
  [HarmonyPatch(typeof(ZNet), nameof(ZNet.ClearPlayerData))]
  [HarmonyPostfix]
  private static void ClearPlayerDataPostfix(ZNetPeer peer)
  {
    RemoveCachedPeer(peer.m_uid, peer);
    PeerIds.Remove(peer);
    HandledPeers.Remove(peer);
  }
}
