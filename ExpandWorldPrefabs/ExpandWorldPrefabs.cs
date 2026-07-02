using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
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
  public const string VERSION = "1.53.8";
#nullable disable
  public static Harmony Harmony;
  public static ConfigEntry<bool> EnableScaleRestoreHack;
#nullable enable
  public static Assembly? ExpandEvents;
  public void Awake()
  {
    var configReload = Config.Bind("General", "Automatic file reload", true, "Settings are automatically reloaded on file changes. Requires restart to take effect.");
    EnableScaleRestoreHack = Config.Bind("General", "Enable scale restore hack", true, "Applies workaround patch that restores scale values after ZDO deserialization.");
    Harmony = new(GUID);
    Harmony.PatchAll();
    Log.Init(Logger);
    Yaml.Init();
    try
    {
      if (configReload.Value)
      {
        DataLoading.SetupWatcher();
        Loading.SetupWatcher();
      }
      DataStorage.LoadSavedData();
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
    new Terminal.ConsoleCommand("ewp_reload_data", "Manually reloads the ewp_data.yaml file.", (args) =>
    {
      DataStorage.LoadSavedData();
    }, true);
    new Terminal.ConsoleCommand("ewp_reload", "Manually reloads all config and data files.", (args) =>
    {
      DataLoading.LoadEntries();
      Loading.FromFile();
      DataStorage.LoadSavedData();
    }, true);
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
