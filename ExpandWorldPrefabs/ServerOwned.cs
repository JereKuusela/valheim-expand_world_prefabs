using Data;
using HarmonyLib;

namespace ExpandWorld.Prefab;

// Keeps objects marked with "owner: server" owned by the dedicated server/host itself.
// This lets the server receive RPCs that vanilla only routes to the owning client.
public class ServerOwned
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
    var original = AccessTools.Method(typeof(ZDO), nameof(ZDO.SetOwner));
    var prefix = AccessTools.Method(typeof(ServerOwned), nameof(SetOwner));
    harmony.Patch(original, prefix: new HarmonyMethod(prefix));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var original = AccessTools.Method(typeof(ZDO), nameof(ZDO.SetOwner));
    var prefix = AccessTools.Method(typeof(ServerOwned), nameof(SetOwner));
    harmony.Unpatch(original, prefix);
  }

  private static readonly int EWPServerOwnedHash = ZdoHelper.Hash("ewp_server_owned");

  public static bool IsMarked(ZDO zdo) => ServerSideData.TryGetInt(zdo, EWPServerOwnedHash, out var value) && value == 1;

  private static void SetOwner(ZDO __instance, ref long uid)
  {
    if (!IsMarked(__instance) && !CustomScaling.IsHeld(__instance)) return;
    // Real players must stay in control of themselves.
    //if (PersistPlayers.IsRealPlayer(__instance)) return;
    uid = ZDOMan.instance.m_sessionID;
  }

  public static void Mark(ZDO zdo)
  {
    //if (PersistPlayers.IsRealPlayer(zdo)) return;
    ServerSideData.SetInt(zdo, EWPServerOwnedHash, 1);
    zdo.SetOwner(ZDOMan.instance.m_sessionID);
  }

  public static void Mark(ZdoEntry zdoEntry)
  {
    // During delayed spawn, write the marker through the server-only path.
    // Writing directly to Ints would synchronize it to clients, where a stale
    // value can later re-enter the server in ZDO.Deserialize.
    if (ServerSideData.ShouldUse(EWPServerOwnedHash))
    {
      zdoEntry.Ints?.Remove(EWPServerOwnedHash);
      zdoEntry.ServerInts ??= [];
      zdoEntry.ServerInts[EWPServerOwnedHash] = 1;
    }
    else
    {
      zdoEntry.Ints ??= [];
      zdoEntry.Ints[EWPServerOwnedHash] = 1;
    }
    zdoEntry.Owner = ZDOMan.instance.m_sessionID;
  }

  // Used when a later rule assigns a normal numeric owner, to stop forcing the server owner back.
  // A missing entry is NOT equivalent to an explicit release: TryGetInt falls back to
  // the synced ZDO data. A client may send an older ewp_server_owned=1 during
  // ZDO.Deserialize and accidentally reactivate the server-ownership lock.
  // Persist an authoritative zero in the server-only store instead of deleting
  // the key, so even a stale synced value cannot override the release.
  public static void Unmark(ZDO zdo)
  {
    if (ServerSideData.ShouldUse(EWPServerOwnedHash))
    {
      ServerSideData.SetInt(zdo, EWPServerOwnedHash, 0);
      // Also discard any legacy synchronized copy on this server ZDO.
      zdo.RemoveInt(EWPServerOwnedHash);
    }
    else
    {
      // Without server-only storage this is a synchronized field. Keep an
      // explicit zero rather than removing it altogether.
      zdo.Set(EWPServerOwnedHash, 0);
    }
  }
}
