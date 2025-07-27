
using HarmonyLib;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class HandleGlobalKey
{
  public static void Patch(Harmony harmony)
  {
    var method = AccessTools.Method(typeof(ZoneSystem), nameof(ZoneSystem.RPC_SetGlobalKey));
    var patch = AccessTools.Method(typeof(HandleGlobalKey), nameof(RPC_SetGlobalKey));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZoneSystem), nameof(ZoneSystem.RPC_RemoveGlobalKey));
    patch = AccessTools.Method(typeof(HandleGlobalKey), nameof(RPC_RemoveGlobalKey));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZoneSystem), nameof(ZoneSystem.ClearGlobalKeys));
    patch = AccessTools.Method(typeof(HandleGlobalKey), nameof(ClearGlobalKeys));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }

  private static void RPC_SetGlobalKey(string name)
  {
    var keyValue = ZoneSystem.GetKeyValue(name.ToLower(), out _, out _);
    Manager.HandleGlobal(ActionType.GlobalKey, [keyValue], Vector3.zero, false);
  }
  private static void RPC_RemoveGlobalKey(string name)
  {
    var keyValue = ZoneSystem.GetKeyValue(name.ToLower(), out _, out _);
    Manager.HandleGlobal(ActionType.GlobalKey, [keyValue], Vector3.zero, true);
  }
  private static void ClearGlobalKeys(ZoneSystem __instance)
  {
    foreach (var key in __instance.m_globalKeys)
      RPC_RemoveGlobalKey(key);
  }
}
