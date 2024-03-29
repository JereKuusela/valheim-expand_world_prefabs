using System.Collections.Generic;
using System.Linq;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class Commands
{

  public static void Run(Info info, ZDO zdo, Dictionary<string, string> parameters, ZDO? source)
  {
    if (info.Commands.Length == 0) return;
    var players = FindPlayers(zdo, source, info);
    var commands = info.Commands.Select(s => Helper.ReplaceParameters(s, parameters)).ToArray();
    if (info.PlayerSearch == PlayerSearch.None)
      CommandManager.Run(commands, players.Length == 0 ? null : players[0]);
    else
      CommandManager.Run(commands, players);
  }
  private static PlayerInfo[] FindPlayers(ZDO zdo, ZDO? source, Info info)
  {
    var players = ZNet.instance.GetPeers().Select(p => new PlayerInfo(p)).ToList();
    if (ZNet.instance.IsServer() && !ZNet.instance.IsDedicated())
      players.Add(new(Player.m_localPlayer));
    if (info.PlayerSearch == PlayerSearch.None)
      return players.Where(p => p.ZDOID == zdo.m_uid || p.ZDOID == source?.m_uid).ToArray();

    var pos = zdo.m_position;
    var rot = zdo.m_rotation;
    players = players.Where(p =>
      Utils.DistanceXZ(p.Pos, pos) <= info.PlayerSearchDistance
      && (info.PlayerSearchHeight == 0f || Mathf.Abs(p.Pos.y - pos.y) <= info.PlayerSearchHeight
    )).ToList();
    if (info.PlayerSearch == PlayerSearch.All)
      return players.ToArray();
    if (info.PlayerSearch == PlayerSearch.Closest)
    {
      var player = players.OrderBy(p => Utils.DistanceXZ(p.Pos, pos)).FirstOrDefault();
      return player == null ? [] : [player];
    }
    return [];
  }
}