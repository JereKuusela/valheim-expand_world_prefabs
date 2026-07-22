using Data;
using HarmonyLib;

namespace ExpandWorld.Prefab;

public class PersistPlayers
{
  private static bool IsPatched = false;
  private static readonly int PlayerHash = ZdoHelper.Hash("Player");

  public static bool IsPlayer(ZDO zdo) => zdo.GetPrefab() == PlayerHash;
  public static bool IsNpc(ZDO zdo) => zdo.GetPrefab() == PlayerHash && zdo.Persistent;
  public static bool IsRealPlayer(ZDO zdo) => zdo.GetPrefab() == PlayerHash && !zdo.Persistent;

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
    var prefix = AccessTools.Method(typeof(PersistPlayers), nameof(SetOwner));
    harmony.Patch(original, prefix: new HarmonyMethod(prefix));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var original = AccessTools.Method(typeof(ZDO), nameof(ZDO.SetOwner));
    var prefix = AccessTools.Method(typeof(PersistPlayers), nameof(SetOwner));
    harmony.Unpatch(original, prefix);
  }

  // Clients can only have one player, so spawned NPC players should stay unowned.
  private static void SetOwner(ZDO __instance, ref long uid)
  {
    if (IsNpc(__instance))
      uid = SupportAttach.HackOwner;
  }
}
