namespace ExpandWorld.Prefab;

// These predicates deliberately separate a Character from a client-owned Character.
// They must not treat the server session, an arbitrary UID or a disconnected peer
// as a client. Physics objects and special owner schemes stay on their native paths.
internal static class ScaleOwnershipPolicy
{
  internal static bool IsConnectedClient(long uid)
  {
    if (uid == 0 || ZDOMan.instance == null || uid == ZDOMan.instance.m_sessionID)
      return false;
    foreach (var peer in ZDOMan.instance.m_peers)
      if (peer.m_peer != null && peer.m_peer.m_uid == uid)
        return true;
    return false;
  }

  internal static bool IsOrdinaryCharacter(ZDO zdo)
  {
    if (zdo == null || ZNetScene.instance == null ||
        ServerOwned.IsMarked(zdo) || SupportAttach.IsAttached(zdo) ||
        PersistPlayers.IsPlayer(zdo))
      return false;
    var prefab = ZNetScene.instance.GetPrefab(zdo.m_prefab);
    return prefab && prefab.GetComponent<Character>() &&
           !prefab.GetComponent<Player>() &&
           !prefab.GetComponent<ItemDrop>() && !prefab.GetComponent<Ship>();
  }

  internal static bool HasClientOwner(ZDO zdo) =>
    IsOrdinaryCharacter(zdo) && IsConnectedClient(zdo.GetOwner());
}
