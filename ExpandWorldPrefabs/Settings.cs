
using BepInEx.Configuration;
using Data;

namespace ExpandWorld.Prefab;

public class Config
{
#nullable disable
  private static ConfigEntry<bool> ConfigAutomaticReload;
  private static ConfigEntry<bool> ConfigRestoreScale;
  private static ConfigEntry<bool> ConfigPersistPlayers;
  private static ConfigEntry<bool> ConfigSupportAttach;
  private static ConfigEntry<bool> ConfigServerSideData;
  private static ConfigEntry<float> ConfigNpcPlayerListRange;
  private static ConfigEntry<string> ConfigCustomPrefabNames;
#nullable enable
  public static bool AutomaticReload => ConfigAutomaticReload.Value;
  public static bool RestoreScale => ConfigRestoreScale.Value;
  public static bool PersistPlayers => ConfigPersistPlayers.Value;
  public static bool SupportAttach => ConfigSupportAttach.Value;
  public static bool ServerSideData => ConfigServerSideData.Value;
  public static float NpcPlayerListRange => ConfigNpcPlayerListRange.Value;
  public static string CustomPrefabNames => ConfigCustomPrefabNames.Value;

  public static void Init(ConfigFile config)
  {
    ConfigAutomaticReload = config.Bind("General", "Automatic file reload", true, "Settings are automatically reloaded on file changes. Requires restart to take effect.");
    ConfigRestoreScale = config.Bind("General", "Restore scale", true, "When enabled, EWP automatically restores custom scale for objects with ZSyncTransform.m_syncScale.");
    ConfigSupportAttach = config.Bind("General", "Object attaching", true, "When enabled, EWP keeps ownership of attached objects to prevent clients from separating them.");
    ConfigServerSideData = config.Bind("General", "Server side data", true, "When enabled, data keys starting with ewp_ are stored in server-only payload to reduce network traffic.");
    ConfigPersistPlayers = config.Bind("General", "Persist spawned players", true, "When enabled, EWP spawned players will be saved to the save file.");
    ConfigNpcPlayerListRange = config.Bind("General", "NPC player list range", 0f, "Maximum distance for NPC profiles to appear in the player list. Set to 0 to disable this feature.");
    ConfigCustomPrefabNames = config.Bind("General", "Custom prefab names", "", "Comma separated list of prefab names that are processed even when server doesn't recognize them.");

    ConfigRestoreScale.SettingChanged += (_, _) => RefreshPatches();
    ConfigPersistPlayers.SettingChanged += (_, _) => RefreshPatches();
    ConfigSupportAttach.SettingChanged += (_, _) => RefreshPatches();
    ConfigServerSideData.SettingChanged += (_, _) => RefreshPatches();
    ConfigNpcPlayerListRange.SettingChanged += (_, _) => RefreshPatches();
    ConfigCustomPrefabNames.SettingChanged += (_, _) => RefreshPatches();
  }

  private static void RefreshPatches()
  {
    if (EWP.Harmony == null) return;
    PrefabHelper.ClearCache();
    InfoManager.Patch();
  }


}