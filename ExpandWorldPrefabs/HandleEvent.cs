using HarmonyLib;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class HandleEvent
{
  public static void Patch(Harmony harmony)
  {
    var method = AccessTools.Method(typeof(RandEventSystem), nameof(RandEventSystem.SetRandomEvent));
    var patch = AccessTools.Method(typeof(HandleEvent), nameof(SetRandomEvent));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }

  private static void SetRandomEvent(RandEventSystem __instance, RandomEvent ev, Vector3 pos)
  {
    if (__instance.m_randomEvent != null)
      Manager.HandleGlobal(ActionType.Event, [__instance.m_randomEvent.m_name], __instance.m_randomEvent.m_pos, true);
    if (ev != null)
      Manager.HandleGlobal(ActionType.Event, [ev.m_name], pos, false);
  }
}
