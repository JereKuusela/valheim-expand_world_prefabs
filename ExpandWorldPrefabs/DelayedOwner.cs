using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpandWorld.Prefab;

// Game has annoying feature that pre-existing objects have their body set to sleep.
// This causes server spawned item drops to float in the air.
// The body is awakened when a client gets ownership of the object.
// So for server spawned item drops (and boats), have to delay setting the owner.
// Server itself would auto-assign ownership after 2 seconds, but this is bit too slow.
public class DelayedOwner(double due, ZDOID zdo, long owner)
{
  private DelayedOwner(double due, ZDOID zdo, long owner, ulong revision) : this(due, zdo, owner) =>
    ExpectedOwnerRevision = revision;

  private static readonly List<DelayedOwner> Owners = [];
  public static void Clear() => Owners.Clear();

  private static bool IsConnectedClient(long uid)
  {
    if (ZDOMan.instance == null) return false;
    return ZDOMan.instance.m_peers.Any(p => p.m_peer != null && p.m_peer.m_uid == uid);
  }

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
    bool shouldNotBeOwned = SupportAttach.IsAttached(zdo) || PersistPlayers.IsPlayer(zdo);
    if (shouldNotBeOwned)
      owner = SupportAttach.HackOwner;

    // For most cases, some client should own the object.
    // This means some client is instantly handling the object.
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
    // Which means the ownership can't be instantly assigned for these objects.
    // Note: Current implementation is bit faulty, because regular ownership assignment could run near instantly after this.
    if (delay)
      Add(0.1f, zdo, owner);
    else
    {
      Cancel(zdo.m_uid);
      zdo.SetOwnerInternal(owner);
    }
  }

  internal static bool NeedsDelay(bool shouldNotBeOwned, bool isItem, bool isShip, bool scaleBackup) =>
    !shouldNotBeOwned && (isItem || isShip || scaleBackup);

  private static void Cancel(ZDOID uid) => Owners.RemoveAll(action => action.Zdo == uid);

  // A later request for the same ZDO supersedes its pending ownership decision.
  internal static void ReplaceAction(double due, ZDOID uid, long owner, ulong revision)
  {
    Cancel(uid);
    Owners.Add(new(due, uid, owner, revision));
  }

  public static void Add(float delay, ZDO zdo, long owner)
  {
    zdo.SetOwner(0);
    if (delay <= 0f)
    {
      Cancel(zdo.m_uid);
      zdo.SetOwner(owner);
      return;
    }
    ReplaceAction(ZNet.instance.m_netTime + delay, zdo.m_uid, owner,
      Convert.ToUInt64(zdo.OwnerRevision));
  }

  public static void Execute()
  {
    for (var i = 0; i < Owners.Count; i++)
    {
      var action = Owners[i];
      if (action.Due > ZNet.instance.m_netTime) continue;
      // Remove before applying so callbacks cannot overwrite a replacement action.
      Owners.RemoveAt(i);
      i--;
      action.ExecuteAction();
    }
  }

  private readonly ZDOID Zdo = zdo;
  private readonly double Due = due;
  private readonly long Owner = owner;
  private readonly ulong ExpectedOwnerRevision;

  internal static bool CanAssign(long currentOwner, ulong currentRevision, ulong expectedRevision,
    long targetOwner, long serverUid, bool targetPeerConnected) =>
    currentOwner == 0 && currentRevision == expectedRevision &&
    (targetOwner == 0 || targetOwner == serverUid || targetPeerConnected);

  private void ExecuteAction()
  {
    if (ZDOMan.instance == null) return;
    var zdo = ZDOMan.instance.GetZDO(Zdo);
    if (zdo == null) return;
    if (!CanAssign(zdo.GetOwner(), Convert.ToUInt64(zdo.OwnerRevision), ExpectedOwnerRevision,
      Owner, ZDOMan.instance.m_sessionID, IsConnectedClient(Owner))) return;
    if (Owner != 0) zdo.SetOwner(Owner);
  }
}
