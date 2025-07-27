using HarmonyLib;

namespace ExpandWorld.Prefab;

public class HandleDestroyed
{
  public static void Patch(Harmony harmony)
  {
    var method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO), [typeof(ZDOID)]);
    var patch = AccessTools.Method(typeof(HandleDestroyed), nameof(Handle));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }
  private static void Handle(ZDOID uid)
  {
    var zdo = ZDOMan.instance.GetZDO(uid);
    if (zdo == null) return;
    Manager.Handle(ActionType.Destroy, [], zdo);
  }
}
