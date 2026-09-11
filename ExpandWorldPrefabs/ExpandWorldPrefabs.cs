using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Service;
using UnityEngine;
namespace ExpandWorld.Prefab;

[BepInPlugin(GUID, NAME, VERSION)]
public class EWP : BaseUnityPlugin
{
  public const string GUID = "expand_world_prefabs";
  public const string NAME = "Expand World Prefabs";
  public const string VERSION = "1.60";
#nullable disable
  public static Harmony Harmony;
#nullable enable
  public static Assembly? ExpandEvents;
  public void Awake()
  {
    Prefab.Config.Init(Config);
    PokeTimers.Initialize();
    Harmony = new(GUID);
    Harmony.PatchAll();
    Log.Init(Logger);
    Yaml.Init();
    RuleLog.Init(Path.Combine(Yaml.BaseDirectory, "ewp_log.txt"));
    try
    {
      if (Prefab.Config.AutomaticReload)
      {
        Yaml.SetupWatcher(Config);
        FileLoading.SetupWatchers(true);
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
      FileLoading.ReloadAll();
      DataStorage.LoadSavedData();
    }, true);
    new Terminal.ConsoleCommand("ewp_biomes", "Lists alternate biome names for biomes and bannedBiomes.", (args) =>
    {
      var names = AltBiomeList.m_altBiomes.Select(alt => alt.m_name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();
      args.Context.AddString(names.Length > 0 ? string.Join("\n", names) : "No alternate biomes loaded. Enter a world and try again.");
    });
  }
  public void LateUpdate()
  {
    if (ZNet.instance == null) return;
    HandleCreated.Execute();
    HandleChanged.Execute();
    DelayedSpawn.Execute();
    DelayedRemove.Execute();
    DelayedPoke.Execute();
    DelayedRpc.Execute();
    DelayedTerrain.Execute();
    DelayedOwner.Execute();
    DataStorage.SaveSavedData();
  }
  public void OnDestroy()
  {
    RuleLog.Close();
  }

  public static RandomEvent GetCurrentEvent(Vector3 pos)
  {
    if (ExpandEvents == null) return RandEventSystem.instance.GetCurrentRandomEvent();
    var method = ExpandEvents.GetType("ExpandWorld.EWE").GetMethod("GetCurrentRandomEvent", BindingFlags.Public | BindingFlags.Static);
    if (method == null) return RandEventSystem.instance.GetCurrentRandomEvent();
    return (RandomEvent)method.Invoke(null, [pos]);
  }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
public class CleanupOnShutdown
{
  static void Postfix()
  {
    HandleCreated.Clear();
    HandleChanged.Clear();
    DelayedSpawn.Clear();
    DelayedRemove.Clear();
    PokeTimers.Clear();
    DelayedRpc.Clear();
    DelayedTerrain.Clear();
    DelayedOwner.Clear();
  }
}