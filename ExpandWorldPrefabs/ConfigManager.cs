using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Common;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

// Script declared settings stored in the main EWP config file.
public static class ConfigManager
{
  private class Declared(string signature, ConfigEntryBase entry, string prefab)
  {
    public readonly string Signature = signature;
    public readonly ConfigEntryBase Entry = entry;
    public readonly string Prefab = prefab;
  }

  // Declaration with functions resolved for one prefab.
  private class Resolved
  {
    public string Section = "";
    public string Key = "";
    public string Name = "";
    public string Prefab = "";
    public string Type = "";
    public string Default = "";
    public string Description = "";
    public string? Min;
    public string? Max;
    public string? Values;
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
    var declarations = files.Where(fileEntries.ContainsKey).SelectMany(f => fileEntries[f]).SelectMany(Resolve).ToList();
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

  // One declaration becomes one entry per matching prefab.
  private static IEnumerable<Resolved> Resolve(ConfigYaml yaml)
  {
    var prefabs = yaml.prefab.Trim();
    ConditionClause? condition = null;
    if (yaml.condition.Trim() != "")
    {
      if (!Conditions.TryParse(yaml.condition, out condition, out var error))
      {
        Log.Error($"Invalid condition \"{yaml.condition}\" for config \"{yaml.key}\": {error}");
        yield break;
      }
    }
    if (prefabs == "")
    {
      var resolved = Resolve(yaml, condition, new Functions("", [], Vector3.zero), "");
      if (resolved != null) yield return resolved;
      yield break;
    }
    if (!ZNetScene.instance)
    {
      Log.Warning($"Config \"{yaml.key}\" with a prefab can't be created before the scene is loaded.");
      yield break;
    }
    foreach (var hash in PrefabHelper.GetPrefabs(prefabs, yaml.excludePrefab.Trim()))
    {
      var prefab = ZNetScene.instance.GetPrefab(hash);
      if (!prefab) continue;
      // The ZDO has only the prefab, so object functions return prefab defaults.
      var zdo = new ZDO { m_prefab = hash };
      var resolved = Resolve(yaml, condition, new ObjectFunctions(prefab.name, [], zdo), prefab.name);
      if (resolved != null) yield return resolved;
    }
  }

  private static Resolved? Resolve(ConfigYaml yaml, ConditionClause? condition, Functions f, string prefab)
  {
    if (condition != null && !condition.Evaluate(f)) return null;
    var resolved = new Resolved
    {
      Section = f.Replace(yaml.config).Trim(),
      Key = f.Replace(yaml.key).Trim(),
      Name = f.Replace(yaml.name).Trim(),
      Prefab = prefab,
      Type = f.Replace(yaml.type),
      Default = f.Replace(yaml.@default),
      Description = Translate(f.Replace(yaml.description)),
      Min = yaml.min == null ? null : f.Replace(yaml.min),
      Max = yaml.max == null ? null : f.Replace(yaml.max),
      Values = yaml.values == null ? null : f.Replace(yaml.values),
    };
    if (resolved.Key == "") resolved.Key = resolved.Name;
    if (resolved.Name == "") resolved.Name = resolved.Key;
    return resolved;
  }

  // Resolves $tokens like $enemy_troll with the language of the game.
  private static string Translate(string text) => Localization.instance == null ? text : Localization.instance.Localize(text);

  private static void Sync(ConfigFile file, List<Resolved> declarations)
  {
    HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
    foreach (var declaration in declarations)
    {
      var section = declaration.Section;
      var key = declaration.Key;
      if (!IsValidName(key) || !IsValidName(section))
      {
        Log.Error($"Invalid config \"{section}/{key}\". Section and key can't be empty or contain _ = < >. Use <safeprefab> instead of <prefab>.");
        continue;
      }
      var id = section + Separator + key;
      if (!seen.Add(id))
      {
        Log.Warning($"Duplicate config \"{id}\" ignored.");
        continue;
      }
      var signature = string.Join("|", declaration.Name, declaration.Prefab, declaration.Type, declaration.Default, declaration.Description, declaration.Min, declaration.Max, declaration.Values);
      if (Entries.TryGetValue(id, out var current) && current.Signature == signature) continue;
      try
      {
        Declare(file, id, signature, declaration, current);
      }
      catch (Exception e)
      {
        Log.Error($"Failed to create config \"{id}\": {e.Message}");
      }
    }
    foreach (var name in Entries.Keys.Where(n => !seen.Contains(n)).ToList())
      Remove(file, name);
  }

  private static void Declare(ConfigFile file, string id, string signature, Resolved declaration, Declared? previous)
  {
    string? oldValue = previous?.Entry.GetSerializedValue();
    if (previous != null)
      Remove(file, id);
    var definition = new ConfigDefinition(declaration.Section, declaration.Name);
    if (file.Keys.Contains(definition))
    {
      Log.Error($"Config \"{id}\" conflicts with the existing setting {declaration.Section}/{declaration.Name}.");
      return;
    }
    var entry = Bind(file, definition, declaration);
    if (entry == null) return;
    if (oldValue != null)
    {
      try { entry.SetSerializedValue(oldValue); }
      catch { }
    }
    Entries[id] = new Declared(signature, entry, declaration.Prefab);
    Names[entry] = id;
  }

  private static void Remove(ConfigFile file, string name)
  {
    if (!Entries.TryGetValue(name, out var declared)) return;
    Entries.Remove(name);
    Names.Remove(declared.Entry);
    file.Remove(declared.Entry.Definition);
  }

  private static bool IsValidName(string name) => name != "" && name.IndexOfAny(['_', '=', '<', '>']) < 0;

  private static ConfigEntryBase? Bind(ConfigFile file, ConfigDefinition definition, Resolved yaml)
  {
    switch (yaml.Type.Trim().ToLowerInvariant())
    {
      case "bool":
        return file.Bind(definition, !bool.TryParse(yaml.Default, out var b) ? false : b, new ConfigDescription(yaml.Description));
      case "int":
        {
          var value = Parse.Int(yaml.Default);
          AcceptableValueBase? range = null;
          if (yaml.Min != null || yaml.Max != null)
            range = new AcceptableValueRange<int>(Parse.Int(yaml.Min ?? "", int.MinValue), Parse.Int(yaml.Max ?? "", int.MaxValue));
          return file.Bind(definition, value, new ConfigDescription(yaml.Description, range));
        }
      case "float":
        {
          var value = Parse.Float(yaml.Default);
          AcceptableValueBase? range = null;
          if (yaml.Min != null || yaml.Max != null)
            range = new AcceptableValueRange<float>(Parse.Float(yaml.Min ?? "", float.MinValue), Parse.Float(yaml.Max ?? "", float.MaxValue));
          return file.Bind(definition, value, new ConfigDescription(yaml.Description, range));
        }
      case "string":
        {
          AcceptableValueBase? list = null;
          if (yaml.Values != null)
            list = new AcceptableValueList<string>([.. Parse.ToList(yaml.Values)]);
          return file.Bind(definition, yaml.Default, new ConfigDescription(yaml.Description, list));
        }
      default:
        Log.Error($"Invalid config type \"{yaml.Type}\". Use bool, int, float or string.");
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
    var prefab = Entries.TryGetValue(id, out var declared) ? declared.Prefab : "";
    handling = true;
    try
    {
      Manager.HandleGlobal(ActionType.Config, [id.Substring(0, split), id.Substring(split + 1), e.ChangedSetting.GetSerializedValue(), prefab], Vector3.zero, false);
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
