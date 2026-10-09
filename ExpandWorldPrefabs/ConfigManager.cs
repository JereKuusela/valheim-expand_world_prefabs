using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Common;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

// Script declared settings stored in the main EWP config file.
public static class ConfigManager
{
  private class Declared(string signature, ConfigEntryBase entry)
  {
    public readonly string Signature = signature;
    public readonly ConfigEntryBase Entry = entry;
  }

  public const char Separator = '_';
  private const float SaveDelay = 2f;
  private const float SelfWriteWindow = 1.5f;

  private static readonly Dictionary<string, Declared> Entries = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<ConfigEntryBase, string> Names = [];
  private static readonly HashSet<string> Warned = [];
  private static bool dirty;
  private static float nextSave;
  private static float selfWriteUntil;
  private static bool triggerEnabled;
  private static bool handling;
  private static bool subscribed;

  // Used by the file watcher so own saves don't cause a reload.
  public static bool IsSelfWrite => Time.unscaledTime < selfWriteUntil;

  public static void LoadFromFiles(List<string> files, Dictionary<string, List<ConfigYaml>> fileEntries)
  {
    var file = Config.Main;
    if (file == null) return;
    var declarations = files.Where(fileEntries.ContainsKey).SelectMany(f => fileEntries[f]).ToList();
    var wasSaving = file.SaveOnConfigSet;
    file.SaveOnConfigSet = false;
    try
    {
      Sync(file, declarations);
    }
    finally
    {
      file.SaveOnConfigSet = wasSaving;
    }
    Save(file);
  }

  private static void Sync(ConfigFile file, List<ConfigYaml> declarations)
  {
    HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
    foreach (var yaml in declarations)
    {
      var name = yaml.config.Trim();
      var section = yaml.section.Trim();
      if (!IsValidName(name) || !IsValidName(section))
      {
        Log.Error($"Invalid config \"{section}/{name}\". Section and name can't be empty or contain _ = < >.");
        continue;
      }
      var id = section + Separator + name;
      if (!seen.Add(id))
      {
        Log.Warning($"Duplicate config \"{id}\" ignored.");
        continue;
      }
      var key = yaml.key.Trim() == "" ? name : yaml.key.Trim();
      var signature = string.Join("|", section, key, yaml.type, yaml.@default, yaml.description, yaml.min, yaml.max, yaml.values);
      if (Entries.TryGetValue(id, out var current) && current.Signature == signature) continue;
      try
      {
        Declare(file, id, section, key, signature, yaml, current);
      }
      catch (Exception e)
      {
        Log.Error($"Failed to create config \"{id}\": {e.Message}");
      }
    }
    foreach (var name in Entries.Keys.Where(n => !seen.Contains(n)).ToList())
      Remove(file, name);
  }

  private static void Declare(ConfigFile file, string name, string section, string key, string signature, ConfigYaml yaml, Declared? previous)
  {
    string? oldValue = previous?.Entry.GetSerializedValue();
    if (previous != null)
      Remove(file, name);
    var definition = new ConfigDefinition(section, key);
    if (file.Keys.Contains(definition))
    {
      Log.Error($"Config \"{name}\" conflicts with the existing setting {section}/{key}.");
      return;
    }
    var entry = Bind(file, definition, yaml);
    if (entry == null) return;
    if (oldValue != null)
    {
      try { entry.SetSerializedValue(oldValue); }
      catch { }
    }
    Entries[name] = new Declared(signature, entry);
    Names[entry] = name;
  }

  private static void Remove(ConfigFile file, string name)
  {
    if (!Entries.TryGetValue(name, out var declared)) return;
    Entries.Remove(name);
    Names.Remove(declared.Entry);
    file.Remove(declared.Entry.Definition);
  }

  private static bool IsValidName(string name) => name != "" && name.IndexOfAny(['_', '=', '<', '>']) < 0;

  private static ConfigEntryBase? Bind(ConfigFile file, ConfigDefinition definition, ConfigYaml yaml)
  {
    switch (yaml.type.Trim().ToLowerInvariant())
    {
      case "bool":
        return file.Bind(definition, !bool.TryParse(yaml.@default, out var b) ? false : b, new ConfigDescription(yaml.description));
      case "int":
        {
          var value = Parse.Int(yaml.@default);
          AcceptableValueBase? range = null;
          if (yaml.min != null || yaml.max != null)
            range = new AcceptableValueRange<int>(Parse.Int(yaml.min ?? "", int.MinValue), Parse.Int(yaml.max ?? "", int.MaxValue));
          return file.Bind(definition, value, new ConfigDescription(yaml.description, range));
        }
      case "float":
        {
          var value = Parse.Float(yaml.@default);
          AcceptableValueBase? range = null;
          if (yaml.min != null || yaml.max != null)
            range = new AcceptableValueRange<float>(Parse.Float(yaml.min ?? "", float.MinValue), Parse.Float(yaml.max ?? "", float.MaxValue));
          return file.Bind(definition, value, new ConfigDescription(yaml.description, range));
        }
      case "string":
        {
          AcceptableValueBase? list = null;
          if (yaml.values != null)
            list = new AcceptableValueList<string>([.. Parse.ToList(yaml.values)]);
          return file.Bind(definition, yaml.@default, new ConfigDescription(yaml.description, list));
        }
      default:
        Log.Error($"Invalid config type \"{yaml.type}\". Use bool, int, float or string.");
        return null;
    }
  }

  public static string? Get(string name)
  {
    if (Entries.TryGetValue(name, out var declared))
      return declared.Entry.GetSerializedValue();
    WarnOnce($"Unknown config \"{name}\".");
    return null;
  }

  public static string? Set(string name, string value)
  {
    if (!Entries.TryGetValue(name, out var declared))
    {
      WarnOnce($"Unknown config \"{name}\". Declare it with a config entry first.");
      return null;
    }
    return SetEntry(declared.Entry, value);
  }

  // Input is guid_section_key.
  public static string? GetMod(string value)
  {
    var entry = FindModEntry(value, false, out _);
    return entry?.GetSerializedValue();
  }

  // Input is guid_section_key_value.
  public static string? SetMod(string value)
  {
    if (!Config.AllowModConfigWrite)
    {
      WarnOnce("Writing mod configs is disabled in the EWP settings.");
      return null;
    }
    var entry = FindModEntry(value, true, out var newValue);
    if (entry == null) return null;
    return SetEntry(entry, newValue);
  }

  // Guid, section and key can contain separators, so the split is resolved by what exists.
  private static ConfigEntryBase? FindModEntry(string value, bool withValue, out string rest)
  {
    rest = "";
    string? guid = null;
    foreach (var id in Chainloader.PluginInfos.Keys)
    {
      if (value.Length > id.Length && value.StartsWith(id + Separator, StringComparison.Ordinal) && (guid == null || id.Length > guid.Length))
        guid = id;
    }
    if (guid == null || Chainloader.PluginInfos[guid].Instance == null)
    {
      WarnOnce($"Unknown mod in \"{value}\".");
      return null;
    }
    var file = Chainloader.PluginInfos[guid].Instance.Config;
    var path = value.Substring(guid.Length + 1);
    for (var i = path.IndexOf(Separator); i > 0; i = path.IndexOf(Separator, i + 1))
    {
      var section = path.Substring(0, i);
      if (!withValue)
      {
        var definition = new ConfigDefinition(section, path.Substring(i + 1));
        if (file.Keys.Contains(definition)) return file[definition];
        continue;
      }
      for (var j = path.IndexOf(Separator, i + 1); j > i + 1; j = path.IndexOf(Separator, j + 1))
      {
        var definition = new ConfigDefinition(section, path.Substring(i + 1, j - i - 1));
        if (!file.Keys.Contains(definition)) continue;
        rest = path.Substring(j + 1);
        return file[definition];
      }
    }
    WarnOnce($"Unknown config in \"{value}\".");
    return null;
  }

  private static string? SetEntry(ConfigEntryBase entry, string value)
  {
    var file = entry.ConfigFile;
    var wasSaving = file.SaveOnConfigSet;
    file.SaveOnConfigSet = false;
    try
    {
      entry.SetSerializedValue(value);
    }
    catch (Exception e)
    {
      Log.Error($"Failed to set {entry.Definition.Section}/{entry.Definition.Key} to \"{value}\": {e.Message}");
      return null;
    }
    finally
    {
      file.SaveOnConfigSet = wasSaving;
    }
    if (wasSaving)
      MarkDirty(file);
    return entry.GetSerializedValue();
  }

  private static readonly HashSet<ConfigFile> DirtyFiles = [];

  private static void MarkDirty(ConfigFile file)
  {
    if (!dirty)
      nextSave = Time.unscaledTime + SaveDelay;
    dirty = true;
    DirtyFiles.Add(file);
  }

  // Called every frame, batches writes to the config files.
  public static void Flush(bool force = false)
  {
    if (!dirty || (!force && Time.unscaledTime < nextSave)) return;
    dirty = false;
    foreach (var file in DirtyFiles)
      Save(file);
    DirtyFiles.Clear();
  }

  private static void Save(ConfigFile file)
  {
    if (file == Config.Main)
      selfWriteUntil = Time.unscaledTime + SelfWriteWindow;
    file.Save();
  }

  public static void SetTriggerEnabled(bool enabled)
  {
    triggerEnabled = enabled;
    if (subscribed || Config.Main == null) return;
    subscribed = true;
    Config.Main.SettingChanged += OnSettingChanged;
  }

  private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
  {
    if (!triggerEnabled || handling) return;
    if (!Names.TryGetValue(e.ChangedSetting, out var id)) return;
    var split = id.IndexOf(Separator);
    handling = true;
    try
    {
      Manager.HandleGlobal(ActionType.Config, [id.Substring(0, split), id.Substring(split + 1), e.ChangedSetting.GetSerializedValue()], Vector3.zero, false);
    }
    finally
    {
      handling = false;
    }
  }

  private static void WarnOnce(string message)
  {
    if (Warned.Add(message))
      Log.Warning(message);
  }
}
