using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandWorld.Prefab;

public static class Api
{
  private static readonly Dictionary<string, Func<string?>> ParameterHandlers = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, Func<string, string?>> ValueParameterHandlers = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, Func<string, long, string, bool>> GroupHandlers = new(StringComparer.OrdinalIgnoreCase);

  public static void RegisterParameterHandler(string key, Func<string?> handler)
  {
    if (key == "" || handler == null) return;
    ParameterHandlers[key] = handler;
  }

  public static void RegisterParameterHandler(string key, Func<string, string?> handler)
  {
    if (key == "" || handler == null) return;
    ValueParameterHandlers[key] = handler;
  }

  public static bool UnregisterParameterHandler(string key)
  {
    if (key == "") return false;
    bool result = false;
    result |= ParameterHandlers.Remove(key);
    result |= ValueParameterHandlers.Remove(key);
    return result;
  }

  public static void RegisterGroupHandler(string key, Func<string, long, string, bool> handler)
  {
    if (key == "" || handler == null) return;
    GroupHandlers[key] = handler;
  }

  public static bool UnregisterGroupHandler(string key)
  {
    if (key == "") return false;
    return GroupHandlers.Remove(key);
  }

  internal static string? ResolveParameter(string key)
  {
    if (ParameterHandlers.TryGetValue(key, out var handler))
      return handler();
    return null;
  }

  internal static string? ResolveValueParameter(string key, string value)
  {
    if (ValueParameterHandlers.TryGetValue(key, out var handler))
      return handler(value);
    return null;
  }

  internal static bool IsInGroup(string playerId, long characterId, string group)
  {
    if (group == "") return false;
    foreach (var handler in GroupHandlers.Values)
    {
      if (handler(playerId, characterId, group)) return true;
    }
    return false;
  }

  public static void TriggerCustom(params string[] args)
  {
    TriggerCustom(Vector3.zero, args);
  }

  public static void TriggerCustom(Vector3 pos, params string[] args)
  {
    if (args == null || args.Length == 0) return;
    if (args[0] == "") return;
    Manager.HandleGlobal(ActionType.Custom, args, pos, false);
  }
}