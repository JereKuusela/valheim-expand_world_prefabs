using HarmonyLib;

namespace ExpandWorld.Prefab;

public class HandleDestroyed
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
    var method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO), [typeof(ZDOID)]);
    var patch = AccessTools.Method(typeof(HandleDestroyed), nameof(Handle));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }
  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO), [typeof(ZDOID)]);
    var patch = AccessTools.Method(typeof(HandleDestroyed), nameof(Handle));
    harmony.Unpatch(method, patch);
  }

  private static void Handle(ZDOID uid)
  {
    var zdo = ZDOMan.instance.GetZDO(uid);
    if (zdo == null) return;
    Manager.Handle(ActionType.Destroy, [], zdo);
  }
}
