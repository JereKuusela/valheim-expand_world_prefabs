

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Data;

namespace Service;

public class DataStorage
{

  private static Dictionary<string, string> Database = [];

  public static void LoadSavedData()
  {
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
  private static readonly Stopwatch LastSaveStopwatch = Stopwatch.StartNew();
  private static string SavedDataFile => Path.Combine(Yaml.BaseDirectory, "ewp_data.yaml");
  private static bool UnsavedChanges = false;
  public static void SaveSavedData()
  {
    if (!UnsavedChanges) return;
    // Save every 10 seconds at most.
    if (LastSaveStopwatch.Elapsed.TotalSeconds < 10) return;
    UnsavedChanges = false;
    LastSaveStopwatch.Restart();
    if (!Directory.Exists(Yaml.BaseDirectory))
      Directory.CreateDirectory(Yaml.BaseDirectory);
    var yaml = Yaml.SerializeData(Database);
    File.WriteAllText(SavedDataFile, yaml);
  }
  public static Action<string, string>? OnSet;
  public static string GetValue(string key, string defaultValue = "") => Database.TryGetValue(key.ToLowerInvariant(), out var value) ? value : defaultValue;
  public static bool TryGetValue(string key, out string value) => Database.TryGetValue(key.ToLowerInvariant(), out value);
  public static void SetValue(string key, string value)
  {
    if (key == "") return;
    key = key.ToLowerInvariant();
    var wildIndex = key.IndexOf('*');
    if (wildIndex < 0)
    {
      SetValueSub(key, value);
    }
    else
    {
      var keys = MatchKeys(key, wildIndex);
      SetValues(keys, value);
    }
  }
  public static string IncrementValue(string key, long amount)
  {
    if (key == "") return "0";
    key = key.ToLowerInvariant();
    var wildIndex = key.IndexOf('*');
    if (wildIndex < 0)
    {
      var newValue = Parse.Long(GetValue(key, "0"), 0) + amount;
      SetValueSub(key, newValue.ToString());
      return newValue.ToString();
    }
    else
    {
      var keys = MatchKeys(key, wildIndex);
      foreach (var k in keys)
      {
        if (Database.TryGetValue(k, out var value))
        {
          SetValueSub(k, (Parse.Long(value, 0) + amount).ToString());
        }
      }
      return "0";
    }
  }

  private static List<string> MatchKeys(string key, int wildIndex)
  {
    if (key == "*")
      return [.. Database.Keys];
    if (key[0] == '*' && key[key.Length - 1] == '*')
      return [.. Database.Keys.Where(k => k.Contains(key.Substring(1, key.Length - 2)))];
    if (key[0] == '*')
      return [.. Database.Keys.Where(k => k.EndsWith(key.Substring(1), StringComparison.OrdinalIgnoreCase))];
    if (key[key.Length - 1] == '*')
      return [.. Database.Keys.Where(k => k.StartsWith(key.Substring(0, key.Length - 1), StringComparison.OrdinalIgnoreCase))];
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

  private static bool ValueMatches(string dbValue, string conditionValue)
  {
    if (conditionValue == "")
      return true;
    if (!conditionValue.Contains(';'))
      return dbValue == conditionValue;

    var split = conditionValue.Split(';');
    if (split.Length < 2)
      return false;

    var min = Parse.LongNull(split[0]);
    var max = Parse.LongNull(split[1]);
    if (min == null || max == null)
      return false;
    if (min.Value > max.Value)
      return false;

    var step = 1L;
    if (split.Length >= 3 && split[2] != "")
    {
      var parsedStep = Parse.LongNull(split[2]);
      if (parsedStep == null || parsedStep.Value <= 0)
        return false;
      step = parsedStep.Value;
    }

    var value = Parse.LongNull(dbValue);
    if (value == null)
      return false;
    if (value.Value < min.Value || value.Value > max.Value)
      return false;
    return (value.Value - min.Value) % step == 0;
  }

  public static bool HasAnyKey(List<string> keys, Parameters pars)
  {
    foreach (var dataKey in keys)
    {
      var kvp = Parse.Kvp(dataKey.Contains("<") ? pars.Replace(dataKey) : dataKey, ' ');
      var key = kvp.Key.ToLowerInvariant();
      if (key == "") continue;
      var wildIndex = key.IndexOf('*');
      if (wildIndex >= 0)
      {
        var keysMatched = MatchKeys(key, wildIndex);
        if (keysMatched.Count == 0) return false;
        foreach (var k in keysMatched)
        {
          if (Database.TryGetValue(k, out var v) && ValueMatches(v, kvp.Value)) return true;
        }
      }
      else
      {
        if (Database.TryGetValue(key, out var v) && ValueMatches(v, kvp.Value)) return true;
      }
    }
    return false;
  }
  public static bool HasEveryKey(List<string> keys, Parameters pars)
  {
    foreach (var key in keys)
    {
      var kvp = Parse.Kvp(key.Contains("<") ? pars.Replace(key) : key, ' ');
      var wildIndex = kvp.Key.IndexOf('*');
      if (wildIndex >= 0)
      {
        var keysMatched = MatchKeys(kvp.Key, wildIndex);
        if (keysMatched.Count == 0) return false;
        foreach (var k in keysMatched)
        {
          if (!Database.TryGetValue(k, out var value) || !ValueMatches(value, kvp.Value)) return false;
        }
      }
      else
      {
        if (!Database.TryGetValue(kvp.Key.ToLowerInvariant(), out var value) || !ValueMatches(value, kvp.Value)) return false;
      }
    }
    return true;
  }
}
