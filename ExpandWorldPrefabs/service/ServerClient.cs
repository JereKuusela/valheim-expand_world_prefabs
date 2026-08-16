using System;
using System.Collections.Generic;
using ExpandWorld.Prefab;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace Service;

public class ServerClient
{
  private static bool IsPatched = false;

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
    var method = AccessTools.Method(typeof(ZNet), nameof(ZNet.TryGetPlayerByPlatformUserID));
    var patch = AccessTools.Method(typeof(ServerClient), nameof(RecognizeServerClient));
    harmony.Patch(method, postfix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZNet), nameof(ZNet.SendPlayerList));
    patch = AccessTools.Method(typeof(ServerClient), nameof(SendPlayerListPrefix));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var method = AccessTools.Method(typeof(ZNet), nameof(ZNet.TryGetPlayerByPlatformUserID));
    var patch = AccessTools.Method(typeof(ServerClient), nameof(RecognizeServerClient));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZNet), nameof(ZNet.SendPlayerList));
    patch = AccessTools.Method(typeof(ServerClient), nameof(SendPlayerListPrefix));
    harmony.Unpatch(method, patch);
  }
  // Server client is only sent to clients, so this is needed for the server to recognize it.
  static bool RecognizeServerClient(bool result, PlatformUserID platformUserID, ref ZNet.PlayerInfo playerInfo)
  {
    if (result) return result;
    if (platformUserID != Client.m_userInfo.m_id) return result;

    playerInfo = Client;
    return true;
  }

  static bool SendPlayerListPrefix(ZNet __instance)
  {
    __instance.UpdatePlayerList();
    if (__instance.m_peers.Count <= 0)
      return false;

    foreach (var peer in __instance.m_peers)
    {
      if (!peer.IsReady())
        continue;

      var center = peer.m_refPos;
      var zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
      if (zdo != null)
        center = zdo.m_position;

      var pkg = new ZPackage();
      if (NPCManager.IsEnabled)
      {
        var npcProfiles = NPCManager.Search(center);
        pkg.Write(__instance.m_players.Count + 1 + npcProfiles.Count);
        foreach (var player in __instance.m_players)
          WritePlayer(pkg, player);
        Write(pkg);
        if (npcProfiles.Count > 0)
          NPCManager.WriteProfiles(pkg, npcProfiles);
      }
      else
      {
        pkg.Write(__instance.m_players.Count + 1);
        foreach (var player in __instance.m_players)
          WritePlayer(pkg, player);
        Write(pkg);
      }
      peer.m_rpc.Invoke("PlayerList", pkg);
    }

    __instance.UpdatePlayerHistory();
    return false;
  }

  static void WritePlayer(ZPackage pkg, ZNet.PlayerInfo player)
  {
    pkg.Write(player.m_name);
    pkg.Write(player.m_characterID);
    pkg.Write(player.m_userInfo.m_id.ToString());
    pkg.Write(player.m_userInfo.m_displayName);
    pkg.Write(player.m_serverAssignedDisplayName);
    pkg.Write(player.m_publicPosition);
    if (player.m_publicPosition)
      pkg.Write(player.m_position);
  }


  public static ZNet.PlayerInfo Client => client ??= CreatePlayerInfo();
  private static ZNet.PlayerInfo? client;

  private static ZNet.PlayerInfo CreatePlayerInfo() => new()
  {
    m_name = "Server",
    // Receiving chat messages requires a valid character ID.
    m_characterID = new ZDOID(ZDOMan.GetSessionID(), uint.MaxValue),
    m_userInfo = new() { m_id = GetServerUserId(), m_displayName = "Server" },
    m_serverAssignedDisplayName = "Server",
    m_publicPosition = false,
    m_position = Vector3.zero,
  };

  private static PlatformUserID GetServerUserId()
  {
    try
    {
      // Steamworks is not available for Microsoft clients.
      var steamGameServer = Type.GetType("Steamworks.SteamGameServer, com.rlabrecque.steamworks.net", false);
      var getSteamId = steamGameServer?.GetMethod("GetSteamID");
      var steamId = getSteamId?.Invoke(null, null)?.ToString();
      if (!string.IsNullOrEmpty(steamId))
        return new PlatformUserID(ZNet.instance.m_steamPlatform, steamId);
    }
    catch
    {
    }
    if (ZNet.m_onlineBackend == OnlineBackendType.PlayFab)
      return new PlatformUserID("playfab", ZPlayFabMatchmaking.m_instance.m_serverData.remotePlayerId);
    else if (ZNet.instance.m_hostSocket == null)
      return new PlatformUserID(ZNet.instance.m_steamPlatform, "Server");
    return new PlatformUserID(ZNet.instance.m_steamPlatform, ZNet.instance.m_hostSocket.GetHostName());
  }

  public static void Write(ZPackage pkg)
  {
    pkg.Write(Client.m_name);
    pkg.Write(Client.m_characterID);
    pkg.Write(Client.m_userInfo.m_id.ToString());
    pkg.Write(Client.m_userInfo.m_displayName);
    pkg.Write(Client.m_serverAssignedDisplayName);
    // Server position is never public.
    pkg.Write(false);
  }
}