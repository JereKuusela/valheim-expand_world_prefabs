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
    if (!IsMarked(__instance)) return;
    // Real players must stay in control of themselves.
    if (PersistPlayers.IsRealPlayer(__instance)) return;
    uid = ZDOMan.instance.m_sessionID;
  }

  public static void Mark(ZDO zdo)
  {
    if (PersistPlayers.IsRealPlayer(zdo)) return;
    ServerSideData.SetInt(zdo, EWPServerOwnedHash, 1);
    zdo.SetOwner(ZDOMan.instance.m_sessionID);
  }

  public static void Mark(ZdoEntry zdoEntry)
  {
    zdoEntry.Ints ??= [];
    zdoEntry.Ints[EWPServerOwnedHash] = 1;
    zdoEntry.Owner = ZDOMan.instance.m_sessionID;
  }

  // Used when a later rule assigns a normal numeric owner, to stop forcing the server owner back.
  public static void Unmark(ZDO zdo) => ServerSideData.RemoveInt(zdo, EWPServerOwnedHash);
}
