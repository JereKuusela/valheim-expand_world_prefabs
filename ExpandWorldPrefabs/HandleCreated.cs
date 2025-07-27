using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class HandleCreated
{
  public static void Patch(Harmony harmony)
  {
    var method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.CreateNewZDO), [typeof(Vector3), typeof(int)]);
    var patch = AccessTools.Method(typeof(HandleCreated), nameof(HandleOwnCreated));
    harmony.Patch(method, postfix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.RPC_ZDOData));
    patch = AccessTools.Method(typeof(HandleCreated), nameof(RPC_ZDOData));
    harmony.Patch(method, transpiler: new HarmonyMethod(patch));
  }
  // Single player requires manual delay so that the initial data is loaded.
  // This is also used for server to keep the ZDO removing logic consistent.
  private static readonly List<ZDOID> CreatedZDOs = [];
  // Ghost init must be handled separately to not assign ownership to clients.
  private static readonly List<ZDOID> GhostZDOs = [];
  public static bool Skip = false;
  public static void Execute()
  {
    for (var i = 0; i < CreatedZDOs.Count; i++)
    {
      var uid = CreatedZDOs[i];
      var zdo = ZDOMan.instance.GetZDO(uid);
      if (zdo == null) continue;
      Manager.Handle(ActionType.Create, [], zdo);
    }
    ZNetView.m_ghostInit = true;
    for (var i = 0; i < GhostZDOs.Count; i++)
    {
      var uid = GhostZDOs[i];
      var zdo = ZDOMan.instance.GetZDO(uid);
      if (zdo == null) continue;
      Manager.Handle(ActionType.Create, [], zdo);
    }
    ZNetView.m_ghostInit = false;
    CreatedZDOs.Clear();
    GhostZDOs.Clear();
  }
  private static void HandleOwnCreated(ZDO __result, int prefabHash)
  {
    if (Skip) return;
    if (prefabHash == 0) return;
    if (ZNetView.m_ghostInit)
      GhostZDOs.Add(__result.m_uid);
    else
      CreatedZDOs.Add(__result.m_uid);
  }
  private static IEnumerable<CodeInstruction> RPC_ZDOData(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchEndForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ZDO), nameof(ZDO.Deserialize))))
      .Advance(1)
      .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 12))
      .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 13))
      .InsertAndAdvance(new CodeInstruction(OpCodes.Call, Transpilers.EmitDelegate(HandleClientCreated).operand))
      .InstructionEnumeration();
  }
  private static void HandleClientCreated(ZDO zdo, bool flag)
  {
    if (flag)
      CreatedZDOs.Add(zdo.m_uid);
  }
}
