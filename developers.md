# Developers

- Mods can register custom function handlers for Expand World Prefabs.
- Mods can register custom group handlers for Expand World Prefabs.
- Mods can directly use custom triggers.

Add a new file to your project `ExpandWorldPrefabsApi.cs`

This adds a soft dependency. If Expand World Prefabs is not installed, then nothing happens.

```cs
using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace EWP;

public static class Api
{
  public const string GUID = "expand_world_prefabs";
  private static bool isSetup = false;

  private static MethodInfo? registerSimpleFunctionHandlerMethod;
  private static MethodInfo? registerValueFunctionHandlerMethod;
  private static MethodInfo? unregisterFunctionHandlerMethod;
  private static MethodInfo? registerGroupHandlerMethod;
  private static MethodInfo? unregisterGroupHandlerMethod;
  private static MethodInfo? triggerCustomMethod;
  private static MethodInfo? triggerCustomWithPositionMethod;

  private static void SetupIfNeeded()
  {
    if (isSetup) return;
    isSetup = true;
    if (!Chainloader.PluginInfos.TryGetValue(GUID, out var plugin)) return;
    Setup(plugin.Instance.GetType().Assembly);
  }

  private static void Setup(Assembly assembly)
  {
    if (assembly == null) return;
    var type = assembly.GetType("ExpandWorld.Prefab.Api");
    if (type == null) return;

    registerSimpleFunctionHandlerMethod = AccessTools.Method(type, "RegisterFunctionHandler", [typeof(string), typeof(Func<string?>)]);
    registerValueFunctionHandlerMethod = AccessTools.Method(type, "RegisterFunctionHandler", [typeof(string), typeof(Func<string, string?>)]);
    unregisterFunctionHandlerMethod = AccessTools.Method(type, "UnregisterFunctionHandler", [typeof(string)]);
    registerGroupHandlerMethod = AccessTools.Method(type, "RegisterGroupHandler", [typeof(string), typeof(Func<string, long, string, bool>)]);
    unregisterGroupHandlerMethod = AccessTools.Method(type, "UnregisterGroupHandler", [typeof(string)]);
    triggerCustomMethod = AccessTools.Method(type, "TriggerCustom", [typeof(string[])]);
    triggerCustomWithPositionMethod = AccessTools.Method(type, "TriggerCustom", [typeof(Vector3), typeof(string[])]);
  }

  public static void AddFunction(string key, Func<string?> handler)
  {
    SetupIfNeeded();
    registerSimpleFunctionHandlerMethod?.Invoke(null, [key, handler]);
  }

  public static void AddValueFunction(string key, Func<string, string?> handler)
  {
    SetupIfNeeded();
    registerValueFunctionHandlerMethod?.Invoke(null, [key, handler]);
  }

  public static void RemoveFunction(string key)
  {
    SetupIfNeeded();
    unregisterFunctionHandlerMethod?.Invoke(null, [key]);
  }

  public static void RegisterGroupHandler(string key, Func<string, long, string, bool> handler)
  {
    SetupIfNeeded();
    registerGroupHandlerMethod?.Invoke(null, [key, handler]);
  }

  public static void UnregisterGroupHandler(string key)
  {
    SetupIfNeeded();
    unregisterGroupHandlerMethod?.Invoke(null, [key]);
  }

  public static void TriggerCustom(params string[] args)
  {
    SetupIfNeeded();
    triggerCustomMethod?.Invoke(null, [args]);
  }

  public static void TriggerCustom(Vector3 pos, params string[] args)
  {
    SetupIfNeeded();
    triggerCustomWithPositionMethod?.Invoke(null, [pos, args]);
  }
}
```

Then to your plugin add

```cs
public void Start()
{
  EWP.Api.AddFunction("test", GetSomething);
  EWP.Api.AddValueFunction("anothertest", AnotherTest);
  EWP.Api.RegisterGroupHandler("modgroup", IsInModGroup);
}

private string GetSomething()
{
  return "Test stuff!";
}
private string AnotherTest(string value)
{
  return $"You sent {value}";
}

private bool IsInModGroup(string playerId, long characterId, string group)
{
  return group == "modgroup" && playerId == "MySpecialPlayer";
}

private void TriggerExample()
{
  EWP.Api.TriggerCustom("my_event", "arg1", "arg2");
  EWP.Api.TriggerCustom(transform.position, "my_event", "arg1", "arg2");
}
```
