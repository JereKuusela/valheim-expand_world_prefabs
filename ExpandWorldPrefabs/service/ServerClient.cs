using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Splatform;
using Steamworks;
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
    patch = AccessTools.Method(typeof(ServerClient), nameof(AddServerClient));
    harmony.Patch(method, transpiler: new HarmonyMethod(patch));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var method = AccessTools.Method(typeof(ZNet), nameof(ZNet.TryGetPlayerByPlatformUserID));
    var patch = AccessTools.Method(typeof(ServerClient), nameof(RecognizeServerClient));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZNet), nameof(ZNet.SendPlayerList));
    patch = AccessTools.Method(typeof(ServerClient), nameof(AddServerClient));
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

  static IEnumerable<CodeInstruction> AddServerClient(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions).End().MatchStartBackwards(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ZNet), nameof(ZNet.m_players))))
      .Advance(-1)
      .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
      .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_0))
      .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ServerClient), nameof(AddServer))))
      .InstructionEnumeration();
  }

  static void AddServer(ZNet net, ZPackage pkg)
  {
    // This is needed in case multiple mods are adding extra players.
    var prev = pkg.GetPos();
    pkg.SetPos(0);
    if (IsExtraPlayerAdded(net, pkg.ReadInt()))
    {
      pkg.SetPos(prev);
    }
    else
    {
      pkg.SetPos(0);
      pkg.Write(net.m_players.Count + 1);
      Write(pkg);
    }
  }
  static bool IsExtraPlayerAdded(ZNet net, int count) => count >= net.m_players.Count + 1;


  public static ZNet.PlayerInfo Client => client ??= CreatePlayerInfo();
  private static ZNet.PlayerInfo? client;

  private static ZNet.PlayerInfo CreatePlayerInfo() => new()
  {
    m_name = "Server",
    // Receiving chat messages requires a valid character ID.
    m_characterID = new ZDOID(ZDOMan.GetSessionID(), uint.MaxValue),
    m_userInfo = new() { m_id = new(ZNet.instance.m_steamPlatform, GetId()), m_displayName = "Server" },
    m_serverAssignedDisplayName = "Server",
    m_publicPosition = false,
    m_position = Vector3.zero,
  };
  private static string GetId()
  {
    try
    {
      return SteamGameServer.GetSteamID().ToString();
    }
    catch (InvalidOperationException)
    {
      return "0";
    }
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