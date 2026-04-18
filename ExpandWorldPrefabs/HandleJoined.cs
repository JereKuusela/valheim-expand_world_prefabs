using System.Collections.Generic;
using HarmonyLib;
using Service;

namespace ExpandWorld.Prefab;

public class HandleJoined
{
  public static void Patch(Harmony harmony)
  {
    var method = AccessTools.Method(typeof(ZNet), nameof(ZNet.Disconnect));
    var patch = AccessTools.Method(typeof(HandleJoined), nameof(Disconnect));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }

  private static readonly HashSet<ZNetPeer> HandledPeers = [];

  private static void Disconnect(ZNetPeer peer)
  {
    var zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
    if (zdo == null) return;
    HandledPeers.Remove(peer);
    Manager.Handle(ActionType.State, ["leave"], zdo);
  }

  private static readonly int PlayerHash = "Player".GetStableHashCode();
  public static void HandlePlayerCreatedState(ZDO zdo)
  {
    if (zdo.m_prefab != PlayerHash) return;
    var peer = ZNet.instance.GetPeer(zdo.GetOwner());
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
}
