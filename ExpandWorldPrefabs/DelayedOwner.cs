using System.Collections.Generic;
using System.Linq;
namespace ExpandWorld.Prefab;

// Game has annoying feature that pre-existing objects have their body set to sleep.
// This causes server spawned item drops to float in the air.
// The body is awakened when a client gets ownership of the object.
// So for server spawned item drops (and boats), have to delay setting the owner.
// Server itself would auto-assign ownership after 2 seconds, but this is bit too slow.
public class DelayedOwner(float delay, ZDOID zdo, long owner)
{
  private static readonly List<DelayedOwner> Owners = [];

  public static void Check(ZDO zdo, long owner)
  {
    if (owner == 0)
    {
      // Some client should always be the owner so that creatures are initialized correctly (for example max health from stars).
      // Things work slightly better when the server doesn't have ownership (for example max health from stars).
      var closestClient = ZDOMan.instance.m_peers.OrderBy(p => Utils.DistanceXZ(p.m_peer.m_refPos, zdo.m_position)).FirstOrDefault(p => p.m_peer.m_uid != zdo.GetOwner());
      owner = closestClient?.m_peer.m_uid ?? 0;
    }
    var prefab = ZNetScene.instance.GetPrefab(zdo.m_prefab);
    if (prefab.GetComponent<ItemDrop>() || prefab.GetComponent<Ship>())
      Add(0.1f, zdo, owner);
    else
      zdo.SetOwnerInternal(owner);
  }


  public static void Add(float delay, ZDO zdo, long owner)
  {
    zdo.SetOwner(0);
    if (delay <= 0f)
    {
      zdo.SetOwner(owner);
      return;
    }
    Owners.Add(new(delay, zdo.m_uid, owner));
  }
  public static void Execute(float dt)
  {
    // Two loops to preserve order.
    for (var i = 0; i < Owners.Count; i++)
    {
      var remove = Owners[i];
      remove.Delay -= dt;
      if (remove.Delay > -0.001) continue;
      remove.Execute();
    }
    for (var i = Owners.Count - 1; i >= 0; i--)
    {
      if (Owners[i].Delay > -0.001) continue;
      Owners.RemoveAt(i);
    }
  }
  private readonly ZDOID Zdo = zdo;
  public float Delay = delay;
  private readonly long Owner = owner;

  public void Execute()
  {
    var zdo = ZDOMan.instance.GetZDO(Zdo);
    if (zdo == null) return;
    zdo.SetOwner(Owner);
  }
}