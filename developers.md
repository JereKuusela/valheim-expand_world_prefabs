# Developers

Mods can register custom parameter handlers for Expand World Prefabs.

Mods can also directly use custom triggers.

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

  private static MethodInfo? registerSimpleParameterHandlerMethod;
  private static MethodInfo? registerValueParameterHandlerMethod;
  private static MethodInfo? unregisterParameterHandlerMethod;
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

    registerSimpleParameterHandlerMethod = AccessTools.Method(type, "RegisterParameterHandler", [typeof(string), typeof(Func<string?>)]);
    registerValueParameterHandlerMethod = AccessTools.Method(type, "RegisterParameterHandler", [typeof(string), typeof(Func<string, string?>)]);
    unregisterParameterHandlerMethod = AccessTools.Method(type, "UnregisterParameterHandler", [typeof(string)]);
    triggerCustomMethod = AccessTools.Method(type, "TriggerCustom", [typeof(string[])]);
    triggerCustomWithPositionMethod = AccessTools.Method(type, "TriggerCustom", [typeof(Vector3), typeof(string[])]);
  }

  public static void AddParameter(string key, Func<string?> handler)
  {
    SetupIfNeeded();
    registerSimpleParameterHandlerMethod?.Invoke(null, [key, handler]);
  }

  public static void AddValueParameter(string key, Func<string, string?> handler)
  {
    SetupIfNeeded();
    registerValueParameterHandlerMethod?.Invoke(null, [key, handler]);
  }

  public static void RemoveParameter(string key)
  {
    SetupIfNeeded();
    unregisterParameterHandlerMethod?.Invoke(null, [key]);
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
  EWP.Api.AddParameter("test", GetSomething);
  EWP.Api.AddValueParameter("anothertest", AnotherTest);
}

private string GetSomething()
{
  return "Test stuff!";
}
private string AnotherTest(string value)
{
  return $"You sent {value}";
}

private void TriggerExample()
{
  EWP.Api.TriggerCustom("my_event", "arg1", "arg2");
  EWP.Api.TriggerCustom(transform.position, "my_event", "arg1", "arg2");
}
```
