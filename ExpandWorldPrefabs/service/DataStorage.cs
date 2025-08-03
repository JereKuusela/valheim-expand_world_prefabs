

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Data;

namespace Service;

public class DataStorage
{

  private static Dictionary<string, string> Database = [];

  public static void LoadSavedData()
  {
    if (UnsavedChanges) return;
    if (!Directory.Exists(Yaml.BaseDirectory))
      Directory.CreateDirectory(Yaml.BaseDirectory);
    if (!File.Exists(SavedDataFile)) return;
    var data = File.ReadAllText(SavedDataFile);
    var db = Yaml.DeserializeData(data);
    foreach (var kvp in db)
    {
      Database[kvp.Key.ToLowerInvariant()] = kvp.Value;
    }
    Log.Info($"Reloaded saved data ({Database.Count} entries).");
  }
  private static bool UnsavedChanges = false;
  private static long LastSave = 0;
  private static readonly string SavedDataFile = Path.Combine(Yaml.BaseDirectory, "ewp_data.yaml");
  public static void SaveSavedData()
  {
    if (!UnsavedChanges) return;
    // Save every 10 seconds at most.
    if (DateTime.Now.Ticks - LastSave < 10000000) return;
    LastSave = DateTime.Now.Ticks;
    if (!Directory.Exists(Yaml.BaseDirectory))
      Directory.CreateDirectory(Yaml.BaseDirectory);
    var yaml = Yaml.SerializeData(Database);
    File.WriteAllText(SavedDataFile, yaml);
    UnsavedChanges = false;
  }
  public static Action<string, string>? OnSet;
  public static string GetValue(string key, string defaultValue = "") => Database.TryGetValue(key.ToLowerInvariant(), out var value) ? value : defaultValue;
  public static bool TryGetValue(string key, out string value) => Database.TryGetValue(key.ToLowerInvariant(), out value);
  public static void SetValue(string key, string value)
  {
    if (key == "") return;
    key = key.ToLowerInvariant();
    if (key[0] == '*' || key[key.Length - 1] == '*')
    {
      var keys = MatchKeys(key);
      SetValues(keys, value);
    }
    else SetValueSub(key, value);
  }
  public static string IncrementValue(string key, long amount)
  {
    if (key == "") return "0";
    key = key.ToLowerInvariant();
    if (key[0] == '*' || key[key.Length - 1] == '*')
    {
      var keys = MatchKeys(key);
      foreach (var k in keys)
      {
        if (Database.TryGetValue(k, out var value))
        {
          SetValueSub(k, (Parse.Long(value, 0) + amount).ToString());
        }
      }
      return "0";
    }
    else
    {
      var newValue = Parse.Long(GetValue(key, "0"), 0) + amount;
      SetValueSub(key, newValue.ToString());
      return newValue.ToString();
    }
  }

  private static List<string> MatchKeys(string key)
  {
    if (key == "*")
      return [.. Database.Keys];
    if (key[0] == '*' && key[key.Length - 1] == '*')
      return [.. Database.Keys.Where(k => k.Contains(key.Substring(1, key.Length - 2)))];
    if (key[0] == '*')
      return [.. Database.Keys.Where(k => k.EndsWith(key.Substring(1), StringComparison.OrdinalIgnoreCase))];
    if (key[key.Length - 1] == '*')
      return [.. Database.Keys.Where(k => k.StartsWith(key.Substring(0, key.Length - 1), StringComparison.OrdinalIgnoreCase))];
    var wildIndex = key.IndexOf('*');
    if (wildIndex > 0 && wildIndex < key.Length - 1)
    {
      var prefix = key.Substring(0, wildIndex);
      var suffix = key.Substring(wildIndex + 1);
      return [.. Database.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                                          k.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))];
    }
    return [];
  }
  public static void SetValues(List<string> keys, string value)
  {
    foreach (var key in keys)
      SetValueSub(key, value);
  }
  private static void SetValueSub(string key, string value)
  {
    if (value == "" && !Database.ContainsKey(key)) return;
    else if (Database.TryGetValue(key, out var oldValue) && oldValue == value) return;

    if (value == "") Database.Remove(key);
    else Database[key] = value;
    UnsavedChanges = true;
    OnSet?.Invoke(key, value);
  }

  public static bool HasAnyKey(List<string> keys, Parameters pars)
  {
    foreach (var key in keys)
    {
      if (key.Contains("<"))
      {
        var kvp = Parse.Kvp(pars.Replace(key), ' ');
        if (Database.TryGetValue(kvp.Key.ToLowerInvariant(), out var value) && (kvp.Value == "" || value == kvp.Value)) return true;
      }
      else
      {
        var kvp = Parse.Kvp(key, ' ');
        if (Database.TryGetValue(kvp.Key.ToLowerInvariant(), out var value) && (kvp.Value == "" || value == kvp.Value)) return true;
      }
    }
    return false;
  }
  public static bool HasEveryKey(List<string> keys, Parameters pars)
  {
    foreach (var key in keys)
    {
      if (key.Contains("<"))
      {
        var kvp = Parse.Kvp(pars.Replace(key), ' ');
        if (!Database.TryGetValue(kvp.Key.ToLowerInvariant(), out var value) || (kvp.Value != "" && value != kvp.Value)) return false;
      }
      else
      {
        var kvp = Parse.Kvp(key, ' ');
        if (!Database.TryGetValue(kvp.Key, out var value) || (kvp.Value != "" && value != kvp.Value)) return false;
      }
    }
    return true;
  }

  public static void SetupWatcher()
  {
    if (!Directory.Exists(Yaml.BaseDirectory))
      Directory.CreateDirectory(Yaml.BaseDirectory);
    Yaml.SetupWatcher(Yaml.BaseDirectory, "ewp_data.yaml", LoadSavedData);
    LoadSavedData();
  }
}
