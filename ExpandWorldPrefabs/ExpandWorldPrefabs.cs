using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using Data;
using HarmonyLib;
using Service;
using UnityEngine;
namespace ExpandWorld.Prefab;

[BepInPlugin(GUID, NAME, VERSION)]
public class EWP : BaseUnityPlugin
{
  public const string GUID = "expand_world_prefabs";
  public const string NAME = "Expand World Prefabs";
  public const string VERSION = "1.46.2";
#nullable disable
  public static Harmony Harmony;
#nullable enable
  public static Assembly? ExpandEvents;
  public void Awake()
  {
    Harmony = new(GUID);
    Harmony.PatchAll();
    Log.Init(Logger);
    Yaml.Init();
    try
    {
      DataLoading.SetupWatcher();
      Loading.SetupWatcher();
      DataStorage.SetupWatcher();
    }
    catch (Exception e)
    {
      Log.Error(e.StackTrace);
    }
  }
  public void Start()
  {
    if (Chainloader.PluginInfos.TryGetValue("expand_world_events", out var plugin))
    {
      ExpandEvents = plugin.Instance.GetType().Assembly;
    }
  }
  public void LateUpdate()
  {
    if (ZNet.instance == null) return;
    HandleCreated.Execute();
    HandleChanged.Execute();
    DelayedSpawn.Execute(Time.deltaTime);
    DelayedRemove.Execute(Time.deltaTime);
    DelayedPoke.Execute(Time.deltaTime);
    DelayedRpc.Execute(Time.deltaTime);
    DelayedTerrain.Execute(Time.deltaTime);
    DelayedOwner.Execute(Time.deltaTime);
    DataStorage.SaveSavedData();
  }

  public static RandomEvent GetCurrentEvent(Vector3 pos)
  {
    if (ExpandEvents == null) return RandEventSystem.instance.GetCurrentRandomEvent();
    var method = ExpandEvents.GetType("ExpandWorld.EWE").GetMethod("GetCurrentRandomEvent", BindingFlags.Public | BindingFlags.Static);
    if (method == null) return RandEventSystem.instance.GetCurrentRandomEvent();
    return (RandomEvent)method.Invoke(null, [pos]);
  }
}
