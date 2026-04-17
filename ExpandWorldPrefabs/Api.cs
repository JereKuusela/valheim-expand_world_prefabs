using System;
using System.Collections.Generic;

namespace ExpandWorld.Prefab;

public static class Api
{
  private static readonly Dictionary<string, Func<string?>> ParameterHandlers = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, Func<string, string?>> ValueParameterHandlers = new(StringComparer.OrdinalIgnoreCase);

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
}