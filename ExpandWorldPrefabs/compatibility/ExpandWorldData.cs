using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;
namespace ExpandWorld.Prefab;

public static class ExpandWorldData
{
  private static Func<Vector3, RandomEvent?>? getCurrentEvent;

  public static void Init()
  {
    if (!Chainloader.PluginInfos.TryGetValue("expand_world_data", out var plugin)) return;
    var method = plugin.Instance.GetType().Assembly.GetType("ExpandWorldData.Api")?.GetMethod("GetCurrentRandomEvent", BindingFlags.Public | BindingFlags.Static);
    if (method == null) return;
    getCurrentEvent = (Func<Vector3, RandomEvent?>)Delegate.CreateDelegate(typeof(Func<Vector3, RandomEvent?>), method);
  }

  public static RandomEvent GetCurrentEvent(Vector3 pos) => getCurrentEvent?.Invoke(pos) ?? RandEventSystem.instance.GetCurrentRandomEvent();
}
