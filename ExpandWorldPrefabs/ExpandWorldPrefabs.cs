using System;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Service;
namespace ExpandWorld.Prefab;

[BepInPlugin(GUID, NAME, VERSION)]
public class EWP : BaseUnityPlugin
{
  public const string GUID = "expand_world_prefabs";
  public const string NAME = "Expand World Prefabs";
  public const string VERSION = "1.66";
#nullable disable
  public static Harmony Harmony;
#nullable enable
  public void Awake()
  {
    Prefab.Config.Init(Config);
    PokeTimers.Initialize();
    Harmony = new(GUID);
    Harmony.PatchAll();
    Log.Init(Logger);
    Yaml.Init();
    RuleLog.Init(Path.Combine(Yaml.BaseDirectory, "logs"));
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
    ServerDevcommands.Init();
    ExpandWorldData.Init();
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
    new Terminal.ConsoleCommand("ewp_itemtype", "Lists the itemtype_* value groups that can be used in prefab.", (args) =>
    {
      if (!ObjectDB.instance)
      {
        args.Context.AddString("No items loaded. Enter a world and try again.");
        return;
      }
      var names = ObjectDB.instance.m_items.Select(item => item.GetComponent<ItemDrop>()).Where(item => item).Select(item => $"itemtype_{item.m_itemData.m_shared.m_itemType}").Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();
      args.Context.AddString(string.Join("\n", names));
    });
  }
  public void LateUpdate()
  {
    ConfigManager.Flush();
    if (ZNet.instance == null) return;
    FileLoading.ExecutePending();
    HandleCreated.Execute();
    HandleChanged.Execute();
    DelayedSpawn.Execute();
    DelayedRemove.Execute();
    DelayedPoke.Execute();
    DelayedExec.Execute();
    DelayedRpc.Execute();
    DelayedTerrain.Execute();
    DelayedOwner.Execute();
    DataStorage.SaveSavedData();
  }
  public void OnDestroy()
  {
    FileLoading.ClearPending();
    ConfigManager.Flush(true);
    RuleLog.Close();
  }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
public class CleanupOnShutdown
{
  static void Postfix()
  {
    FileLoading.ClearPending();
    HandleCreated.Clear();
    HandleChanged.Clear();
    DelayedSpawn.Clear();
    DelayedRemove.Clear();
    PokeTimers.Clear();
    DelayedExec.Clear();
    DelayedRpc.Clear();
    DelayedTerrain.Clear();
    DelayedOwner.Clear();
  }
}