using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class HandleCreated
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
    var method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.CreateNewZDO), [typeof(Vector3), typeof(int)]);
    var patch = AccessTools.Method(typeof(HandleCreated), nameof(HandleOwnCreated));
    harmony.Patch(method, postfix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.RPC_ZDOData));
    patch = AccessTools.Method(typeof(HandleCreated), nameof(RPC_ZDOData));
    harmony.Patch(method, transpiler: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZNetView), nameof(ZNetView.StartGhostInit));
    patch = AccessTools.Method(typeof(HandleCreated), nameof(OnGhostInitStarted));
    harmony.Patch(method, postfix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZNetView), nameof(ZNetView.FinishGhostInit));
    patch = AccessTools.Method(typeof(HandleCreated), nameof(OnGhostInitFinished));
    harmony.Patch(method, postfix: new HarmonyMethod(patch));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.CreateNewZDO), [typeof(Vector3), typeof(int)]);
    var patch = AccessTools.Method(typeof(HandleCreated), nameof(HandleOwnCreated));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.RPC_ZDOData));
    patch = AccessTools.Method(typeof(HandleCreated), nameof(RPC_ZDOData));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZNetView), nameof(ZNetView.StartGhostInit));
    patch = AccessTools.Method(typeof(HandleCreated), nameof(OnGhostInitStarted));
    harmony.Unpatch(method, patch);
    method = AccessTools.Method(typeof(ZNetView), nameof(ZNetView.FinishGhostInit));
    patch = AccessTools.Method(typeof(HandleCreated), nameof(OnGhostInitFinished));
    harmony.Unpatch(method, patch);
    GhostInitActive = false;
  }

  // Single player requires manual delay so that the initial data is loaded.
  // This is also used for server to keep the ZDO removing logic consistent.
  private static readonly List<ZDOID> CreatedZDOs = [];
  // Ghost init must be handled separately to not assign ownership to clients.
  private static readonly List<ZDOID> GhostZDOs = [];
  internal static bool GhostInitActive { get; private set; }
  public static bool Skip = false;
  public static void Execute()
  {
    try
    {
      for (var i = 0; i < CreatedZDOs.Count; i++)
      {
        var uid = CreatedZDOs[i];
        var zdo = ZDOMan.instance.GetZDO(uid);
        if (zdo == null) continue;
        PeerManager.HandlePlayerCreatedState(zdo);
        Manager.Handle(ActionType.Create, [], zdo);
        NPCManager.Track(zdo);
      }
      ZNetView.StartGhostInit();
      try
      {
        for (var i = 0; i < GhostZDOs.Count; i++)
        {
          var uid = GhostZDOs[i];
          var zdo = ZDOMan.instance.GetZDO(uid);
          if (zdo == null) continue;
          Manager.Handle(ActionType.Create, [], zdo);
          NPCManager.Track(zdo);
        }
      }
      finally
      {
        ZNetView.FinishGhostInit();
      }
    }
    finally
    {
      CreatedZDOs.Clear();
      GhostZDOs.Clear();
    }
  }
  private static void HandleOwnCreated(ZDO __result, int prefabHash)
  {
    if (Skip) return;
    if (prefabHash == 0) return;
    if (GhostInitActive)
      GhostZDOs.Add(__result.m_uid);
    else
      CreatedZDOs.Add(__result.m_uid);
  }
  internal static void OnGhostInitStarted() => GhostInitActive = true;
  internal static void OnGhostInitFinished() => GhostInitActive = false;
  private static IEnumerable<CodeInstruction> RPC_ZDOData(IEnumerable<CodeInstruction> instructions)
  {
    var result = new List<CodeInstruction>(instructions);
    var deserialize = AccessTools.Method(typeof(ZDO), nameof(ZDO.Deserialize));
    var index = result.FindIndex(instruction => instruction.opcode == OpCodes.Callvirt && instruction.operand as System.Reflection.MethodInfo == deserialize);
    if (index < 0) return result;
    result.InsertRange(index + 1,
    [
      new(OpCodes.Ldloc_S, 12),
      new(OpCodes.Ldloc_S, 13),
      new(OpCodes.Call, Transpilers.EmitDelegate(HandleClientCreated).operand)
    ]);
    return result;
  }
  private static void HandleClientCreated(ZDO zdo, bool flag)
  {
    if (flag)
      CreatedZDOs.Add(zdo.m_uid);
  }
}
