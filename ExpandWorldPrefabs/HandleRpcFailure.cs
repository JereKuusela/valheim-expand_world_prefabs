using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Service;

namespace ExpandWorld.Prefab;

public class HandleRpcFailure
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
    var objectRpc = AccessTools.Method(typeof(ZNetView), nameof(ZNetView.HandleRoutedRPC));
    var objectTranspiler = AccessTools.Method(typeof(HandleRpcFailure), nameof(ObjectRpcTranspiler));
    harmony.Patch(objectRpc, transpiler: new HarmonyMethod(objectTranspiler));

    var clientRpc = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.HandleRoutedRPC));
    var clientTranspiler = AccessTools.Method(typeof(HandleRpcFailure), nameof(ClientRpcTranspiler));
    harmony.Patch(clientRpc, transpiler: new HarmonyMethod(clientTranspiler));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var objectRpc = AccessTools.Method(typeof(ZNetView), nameof(ZNetView.HandleRoutedRPC));
    var objectTranspiler = AccessTools.Method(typeof(HandleRpcFailure), nameof(ObjectRpcTranspiler));
    harmony.Unpatch(objectRpc, objectTranspiler);

    var clientRpc = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.HandleRoutedRPC));
    var clientTranspiler = AccessTools.Method(typeof(HandleRpcFailure), nameof(ClientRpcTranspiler));
    harmony.Unpatch(clientRpc, clientTranspiler);
  }

  // Replaces the vanilla failure warning with our own (which has the method name).
  private static IEnumerable<CodeInstruction> ObjectRpcTranspiler(IEnumerable<CodeInstruction> instructions)
  {
    var warning = AccessTools.Method(typeof(ZLog), nameof(ZLog.LogWarning), [typeof(object)]);
    var hash = AccessTools.Field(typeof(ZRoutedRpc.RoutedRPCData), nameof(ZRoutedRpc.RoutedRPCData.m_methodHash));
    var log = AccessTools.Method(typeof(HandleRpcFailure), nameof(LogObjectFailure));
    return new CodeMatcher(instructions)
      .MatchStartForward(new CodeMatch(OpCodes.Call, warning))
      .ThrowIfInvalid("Failed to patch ZNetView.HandleRoutedRPC for rpc failure logging.")
      .SetOpcodeAndAdvance(OpCodes.Pop)
      .Insert(
        new CodeInstruction(OpCodes.Ldarg_1),
        new CodeInstruction(OpCodes.Ldfld, hash),
        new CodeInstruction(OpCodes.Call, log))
      .InstructionEnumeration();
  }

  // Hooks the failed lookup branch of m_functions.TryGetValue.
  private static IEnumerable<CodeInstruction> ClientRpcTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var functions = AccessTools.Field(typeof(ZRoutedRpc), nameof(ZRoutedRpc.m_functions));
    var hash = AccessTools.Field(typeof(ZRoutedRpc.RoutedRPCData), nameof(ZRoutedRpc.RoutedRPCData.m_methodHash));
    var log = AccessTools.Method(typeof(HandleRpcFailure), nameof(LogClientFailure));
    // Advance to the original branch on the lookup result; the duplicated result skips our log when found.
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(new CodeMatch(i => i.LoadsField(functions)))
      .MatchStartForward(new CodeMatch(i => i.opcode == OpCodes.Callvirt && i.operand is MethodInfo { Name: "TryGetValue" }))
      .ThrowIfInvalid("Failed to patch ZRoutedRpc.HandleRoutedRPC for rpc failure logging.")
      .Advance(1)
      .CreateLabel(out var ok)
      .Insert(
        new CodeInstruction(OpCodes.Dup),
        new CodeInstruction(OpCodes.Brtrue, ok),
        new CodeInstruction(OpCodes.Ldarg_1),
        new CodeInstruction(OpCodes.Ldfld, hash),
        new CodeInstruction(OpCodes.Call, log))
      .InstructionEnumeration();
  }

  private static void LogObjectFailure(int hash)
  {
    if (RpcInfo.TryGetName(hash, out var name))
      Log.Warning($"Failed to find rpc method {name}");
    else
      // Original warning even when quite useless to avoid changing behavior.
      ZLog.LogWarning("Failed to find rpc method " + hash);
  }
  private static void LogClientFailure(int hash)
  {
    if (RpcInfo.TryGetName(hash, out var name))
      Log.Warning($"Failed to find rpc method {name}");
    // Game doesn't log the failure. It also calls missing RPCs constantly.
  }
}
