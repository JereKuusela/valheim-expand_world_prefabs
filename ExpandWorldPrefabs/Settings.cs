
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
  private static ConfigEntry<bool> ConfigServerOwned;
  private static ConfigEntry<bool> ConfigRuleLogging;
  private static ConfigEntry<int> ConfigLogGlobalRate, ConfigLogRuleRate, ConfigLogFlushMs, ConfigLogFileMiB;
  private static ConfigEntry<float> ConfigNpcPlayerListRange;
  private static ConfigEntry<string> ConfigCustomPrefabNames;
#nullable enable
  public static bool AutomaticReload => ConfigAutomaticReload.Value;
  public static bool RestoreScale => ConfigRestoreScale.Value;
  public static bool PersistPlayers => ConfigPersistPlayers.Value;
  public static bool SupportAttach => ConfigSupportAttach.Value;
  public static bool ServerSideData => ConfigServerSideData.Value;
  public static bool ServerOwned => ConfigServerOwned.Value;
  public static bool RuleLogging => ConfigRuleLogging.Value;
  internal static long RuleLogMaximumFileBytes => (long)ConfigLogFileMiB.Value * 1024 * 1024;
  internal static RuleLogOptions GetRuleLogOptions() => new()
  {
    GlobalRate = ConfigLogGlobalRate.Value,
    RuleRate = ConfigLogRuleRate.Value,
    FlushMilliseconds = ConfigLogFlushMs.Value
  };
  public static float NpcPlayerListRange => ConfigNpcPlayerListRange.Value;
  public static string CustomPrefabNames => ConfigCustomPrefabNames.Value;

  public static void Init(ConfigFile config)
  {
    ConfigAutomaticReload = config.Bind("General", "Automatic file reload", true, "Settings are automatically reloaded on file changes. Requires restart to take effect.");
    ConfigRestoreScale = config.Bind("General", "Restore scale", true, "When enabled, EWP automatically restores custom scale for objects with ZSyncTransform.m_syncScale.");
    ConfigSupportAttach = config.Bind("General", "Object attaching", true, "When enabled, EWP keeps ownership of attached objects to prevent clients from separating them.");
    ConfigServerSideData = config.Bind("General", "Server side data", true, "When enabled, data keys starting with ewp_ are stored in server-only payload to reduce network traffic.");
    ConfigServerOwned = config.Bind("General", "Server owned objects", true, "When enabled, EWP keeps ownership of objects marked with owner: server, so the server receives owner-only RPCs.");
    ConfigRuleLogging = config.Bind("General", "Rule logging", true, "When enabled, prefab rule log actions append to ewp_log.txt.");
    ConfigPersistPlayers = config.Bind("General", "Persist spawned players", true, "When enabled, EWP spawned players will be saved to the save file.");
    ConfigNpcPlayerListRange = config.Bind("General", "NPC player list range", 0f, "Maximum distance for NPC profiles to appear in the player list. Set to 0 to disable this feature.");
    ConfigCustomPrefabNames = config.Bind("General", "Custom prefab names", "", "Comma separated list of prefab names that are processed even when server doesn't recognize them.");

    ConfigLogFileMiB = config.Bind("Rule logging", "Maximum file MiB", 256,
       new ConfigDescription("Append-only file limit. At the limit, RuleLogFileLimitException disables logging until restart; gameplay continues. Stop the server and archive/rename ewp_log.txt before restarting. Existing oversized logs are preserved. Requires restart.",
         new AcceptableValueRange<int>(1, 4096)));
    ConfigLogGlobalRate = config.Bind("Rule logging", "Records per second", 1000,
      new ConfigDescription("Global admission limit before formatting. Burst allowance is min(100, rate). Requires restart.",
        new AcceptableValueRange<int>(1, 10000)));
    ConfigLogRuleRate = config.Bind("Rule logging", "Records per rule per second", 250,
      new ConfigDescription("Per loaded rule, shared across all players/objects. Burst allowance is min(25, rate). Requires restart.",
        new AcceptableValueRange<int>(1, 10000)));
    ConfigLogFlushMs = config.Bind("Rule logging", "Flush interval milliseconds", 1000,
      new ConfigDescription("Background flush deadline while output is pending. Also flushes at 64 KiB. Requires restart.",
        new AcceptableValueRange<int>(100, 10000)));

    ConfigRestoreScale.SettingChanged += (_, _) => RefreshPatches();
    ConfigPersistPlayers.SettingChanged += (_, _) => RefreshPatches();
    ConfigSupportAttach.SettingChanged += (_, _) => RefreshPatches();
    ConfigServerSideData.SettingChanged += (_, _) => RefreshPatches();
    ConfigServerOwned.SettingChanged += (_, _) => RefreshPatches();
    ConfigNpcPlayerListRange.SettingChanged += (_, _) => RefreshPatches();
    ConfigCustomPrefabNames.SettingChanged += (_, _) => RefreshPatches();

    ConfigRuleLogging.SettingChanged += (_, _) => RuleLog.SetEnabled(RuleLogging);
  }

  private static void RefreshPatches()
  {
    if (EWP.Harmony == null) return;
    PrefabHelper.ClearCache();
    InfoManager.Patch();
  }


}
