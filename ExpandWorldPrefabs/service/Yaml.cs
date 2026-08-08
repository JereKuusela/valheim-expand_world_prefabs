using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Service;

public class Yaml
{
  public enum FileChangeType
  {
    Created,
    Changed,
    Renamed,
    Deleted
  }

  public class MixedFileEntries
  {
    public List<global::ExpandWorld.Prefab.Data> ScriptEntries = [];
    public List<global::Data.DataData> DataEntries = [];
  }

  public static string BaseDirectory = Path.Combine(Paths.ConfigPath, "expand_world");
  public static string BackupDirectory = Path.Combine(Paths.ConfigPath, "expand_world_backups");


  public static List<T> Read<T>(string pattern, bool migrate)
  {
    if (!Directory.Exists(BaseDirectory))
      Directory.CreateDirectory(BaseDirectory);
    var files = Directory.GetFiles(BaseDirectory, pattern, SearchOption.AllDirectories).Reverse().ToList();
    return Read<T>(files, migrate);
  }

  public static List<T> Read<T>(List<string> files, bool migrate)
  {
    List<T> result = [];
    foreach (var file in files)
    {
      try
      {
        var lines = migrate ? PreParse(File.ReadAllLines(file)) : File.ReadAllText(file);
        result.AddRange(Deserialize<T>(lines, file));
      }
      catch (Exception ex)
      {
        Log.Error($"Error reading {Path.GetFileName(file)}: {ex.Message}");
      }
    }
    return result;
  }

  public static List<T> ReadFile<T>(string file, bool migrate)
  {
    try
    {
      var raw = migrate ? PreParse(File.ReadAllLines(file)) : File.ReadAllText(file);
      return Deserialize<T>(raw, file);
    }
    catch (Exception ex)
    {
      Log.Error($"Error reading {Path.GetFileName(file)}: {ex.Message}");
      return [];
    }
  }

  public static MixedFileEntries ReadMixedFile(string file, bool migrateScripts)
  {
    try
    {
      var split = SplitMixed(File.ReadAllLines(file));
      var result = new MixedFileEntries();
      if (split.ScriptLines.Count > 0)
      {
        var raw = migrateScripts ? PreParse([.. split.ScriptLines]) : string.Join("\n", split.ScriptLines);
        result.ScriptEntries = Deserialize<global::ExpandWorld.Prefab.Data>(raw, file);
      }
      if (split.DataLines.Count > 0)
      {
        var raw = string.Join("\n", split.DataLines);
        result.DataEntries = Deserialize<global::Data.DataData>(raw, file);
      }
      return result;
    }
    catch (Exception ex)
    {
      Log.Error($"Error reading {Path.GetFileName(file)}: {ex.Message}");
      return new MixedFileEntries();
    }
  }

  private class MixedSplit
  {
    public List<string> ScriptLines = [];
    public List<string> DataLines = [];
  }

  private static MixedSplit SplitMixed(string[] lines)
  {
    var split = new MixedSplit();
    List<string> pending = [];
    List<string> block = [];
    foreach (var line in lines)
    {
      if (IsTopLevelListItem(line))
      {
        if (block.Count > 0)
          AddMixedBlock(split, block);
        block = [];
        if (pending.Count > 0)
        {
          block.AddRange(pending);
          pending.Clear();
        }
        block.Add(line);
      }
      else
      {
        if (block.Count == 0)
          pending.Add(line);
        else
          block.Add(line);
      }
    }
    if (block.Count > 0)
      AddMixedBlock(split, block);
    return split;
  }

  private static void AddMixedBlock(MixedSplit split, List<string> block)
  {
    if (IsDataBlock(block))
      split.DataLines.AddRange(block);
    else
      split.ScriptLines.AddRange(block);
  }

  private static bool IsTopLevelListItem(string line)
  {
    var raw = line.Length > 0 && line[0] == '\uFEFF' ? line.Substring(1) : line;
    return raw.StartsWith("- ");
  }

  private static bool IsDataBlock(List<string> block)
  {
    foreach (var line in block)
    {
      var key = ParseBlockKey(line);
      if (key == null) continue;
      return key.Equals("name", StringComparison.OrdinalIgnoreCase)
        || key.Equals("valueGroup", StringComparison.OrdinalIgnoreCase)
        || key.Equals("value", StringComparison.OrdinalIgnoreCase);
    }
    return false;
  }

  private static string? ParseBlockKey(string line)
  {
    var noComment = line.Split('#')[0].Trim();
    if (noComment.Length == 0) return null;
    if (noComment.StartsWith("- "))
      noComment = noComment.Substring(2).TrimStart();
    if (noComment.Length == 0 || noComment.StartsWith("-")) return null;
    var index = noComment.IndexOf(':');
    if (index <= 0) return null;
    var key = noComment.Substring(0, index).Trim();
    if (key.Length == 0) return null;
    if (key.StartsWith("\"") && key.EndsWith("\"") && key.Length > 1)
      key = key.Substring(1, key.Length - 2);
    return key;
  }

  private static string PreParse(string[] lines)
  {
    bool objectsMode = false;
    List<string> result = [];
    foreach (var line in lines)
    {
      if (objectsMode)
      {
        if (line.StartsWith("  - ") && !line.Contains(":"))
        {
          HandleObjects(result, line);
          continue;
        }
        objectsMode = false;
      }
      // Some extra checks needed to not break if the spawn line has extra spaces or comments.
      if (line.StartsWith("  spawn: ") && !line.Contains("#") && line.Trim().Length > 6)
      {
        // Convert to spawns list.
        result.Add("  spawns:");
        result.Add("  - " + line.Substring(9));
      }
      else if (line.StartsWith("  swap: ") && !line.Contains("#") && line.Trim().Length > 5)
      {
        // Convert to swaps list.
        result.Add("  swaps:");
        result.Add("  - " + line.Substring(8));
      }
      else if (line.StartsWith("  filter:"))
      {
        // Convert to filters list.
        result.Add("  filters:");
        if (!line.Contains("#") && line.Trim().Length > 7)
          result.Add("  - " + line.Substring(10));
      }
      else if (line.StartsWith("  bannedFilter:"))
      {
        // Convert to banned filters list.
        result.Add("  bannedFilters:");
        if (!line.Contains("#") && line.Trim().Length > 13)
          result.Add("  - " + line.Substring(16));
      }
      else if (line.StartsWith("    filter:"))
      {
        // Convert to filters list.
        result.Add("    filters:");
        if (!line.Contains("#") && line.Trim().Length > 7)
          result.Add("    - " + line.Substring(12));
      }
      else if (line.StartsWith("    bannedFilter: "))
      {
        // Convert to banned filters list.
        result.Add("    bannedFilters:");
        if (!line.Contains("#") && line.Trim().Length > 7)
          result.Add("    - " + line.Substring(18));
      }
      else if (line.StartsWith("  objects:") || line.StartsWith("  bannedObjects:"))
      {
        objectsMode = true;
        result.Add(line);
      }
      else result.Add(line);
    }
    return string.Join("\n", result);
  }
  private static void HandleObjects(List<string> result, string line)
  {
    var parts = line.Substring(4).Split(',');
    result.Add("  - prefab: " + parts[0]);
    if (parts.Length > 1)
    {
      var distance = Parse.StringRange(parts[1]);
      if (distance.Min != distance.Max)
        result.Add("    minDistance: " + distance.Min);
      result.Add("    maxDistance: " + distance.Max);
    }
    if (parts.Length > 2)
      result.Add("    data: " + parts[2]);

    if (parts.Length > 3)
      result.Add("    weight: " + parts[3]);
    if (parts.Length > 4)
    {
      var height = Parse.StringRange(parts[4]);
      if (height.Min != height.Max)
        result.Add("    minHeight: " + height.Min);
      result.Add("    maxHeight: " + height.Max);
    }

  }
  public static Heightmap.Biome ToBiomes(string biomeStr, bool defaultAll)
  {
    Heightmap.Biome result = 0;
    if (biomeStr == "")
    {
      return defaultAll ? (Heightmap.Biome)(-1) : 0;
    }
    else
    {
      var biomes = Parse.Split(biomeStr);
      foreach (var biome in biomes)
      {
        if (Enum.TryParse<Heightmap.Biome>(biome, true, out var number))
          result |= number;
        else
        {
          if (int.TryParse(biome, out var value)) result += value;
          else throw new InvalidOperationException($"Invalid biome {biome}.");
        }
      }
    }
    return result;
  }
  public static void SetupWatcher(ConfigFile config)
  {
    FileSystemWatcher watcher = new(Path.GetDirectoryName(config.ConfigFilePath), Path.GetFileName(config.ConfigFilePath));
    watcher.Changed += (s, e) => ReadConfigValues(e.FullPath, config);
    watcher.Created += (s, e) => ReadConfigValues(e.FullPath, config);
    watcher.Renamed += (s, e) => ReadConfigValues(e.FullPath, config);
    watcher.IncludeSubdirectories = true;
    watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
    watcher.EnableRaisingEvents = true;
  }
  private static void ReadConfigValues(string path, ConfigFile config)
  {
    if (!File.Exists(path)) return;
    BackupFile(path, true);
    try
    {
      config.Reload();
    }
    catch
    {
      Log.Error($"There was an issue loading your {config.ConfigFilePath}");
      Log.Error("Please check your config entries for spelling and format!");
    }
  }
  public static void SetupWatcher(string pattern, Action<string> action) => SetupWatcherSub(Paths.ConfigPath, pattern, action);
  public static void SetupWatcher(string folder, string pattern, Action action, bool overwriteBackup) => SetupWatcherSub(folder, pattern, file =>
  {
    BackupFile(file, overwriteBackup);
    action();
  });
  public static void SetupWatcher(string folder, string pattern, Action<string, string?, FileChangeType> action, bool overwriteBackup) => SetupWatcherSub(folder, pattern, (path, oldPath, type) =>
  {
    if (type != FileChangeType.Deleted && path != "")
      BackupFile(path, overwriteBackup);
    action(path, oldPath, type);
  });
  public static void SetupWatcher(string pattern, Action action, bool overwriteBackup) => SetupWatcherSub(BaseDirectory, pattern, file =>
  {
    BackupFile(file, overwriteBackup);
    action();
  });
  public static void SetupWatcher(string pattern, Action<string, string?, FileChangeType> action, bool overwriteBackup) => SetupWatcherSub(BaseDirectory, pattern, (path, oldPath, type) =>
  {
    if (type != FileChangeType.Deleted && path != "")
      BackupFile(path, overwriteBackup);
    action(path, oldPath, type);
  });


  private static void SetupWatcherSub(string folder, string pattern, Action<string> action)
  {
    FileSystemWatcher watcher = new(folder, pattern);
    watcher.Created += (s, e) => action(e.FullPath);
    watcher.Changed += (s, e) => action(e.FullPath);
    watcher.Renamed += (s, e) => action(e.FullPath);
    watcher.Deleted += (s, e) => action(e.FullPath);
    watcher.IncludeSubdirectories = true;
    watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
    watcher.EnableRaisingEvents = true;
  }
  private static void SetupWatcherSub(string folder, string pattern, Action<string, string?, FileChangeType> action)
  {
    FileSystemWatcher watcher = new(folder, pattern);
    watcher.Created += (s, e) => action(e.FullPath, null, FileChangeType.Created);
    watcher.Changed += (s, e) => action(e.FullPath, null, FileChangeType.Changed);
    watcher.Renamed += (s, e) => action(e.FullPath, e.OldFullPath, FileChangeType.Renamed);
    watcher.Deleted += (s, e) => action(e.FullPath, null, FileChangeType.Deleted);
    watcher.IncludeSubdirectories = true;
    watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
    watcher.EnableRaisingEvents = true;
  }
  private static void SetupWatcherSub(string folder, string pattern, Action action)
  {
    FileSystemWatcher watcher = new(folder, pattern);
    watcher.Created += (s, e) => action();
    watcher.Changed += (s, e) => action();
    watcher.Renamed += (s, e) => action();
    watcher.Deleted += (s, e) => action();
    watcher.IncludeSubdirectories = true;
    watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
    watcher.EnableRaisingEvents = true;
  }
  public static void BackupFile(string path, bool overwrite)
  {
    if (!File.Exists(path)) return;
    if (!Directory.Exists(BackupDirectory))
      Directory.CreateDirectory(BackupDirectory);
    var stamp = DateTime.Now.ToString("yyyy-MM-dd");
    var name = $"{Path.GetFileNameWithoutExtension(path)}_{stamp}{Path.GetExtension(path)}.bak";
    var backupPath = Path.Combine(BackupDirectory, name);
    if (overwrite)
      File.Copy(path, backupPath, true);
    else if (!File.Exists(backupPath))
      File.Copy(path, backupPath);
  }

  public static void Init()
  {
    if (!Directory.Exists(BaseDirectory))
      Directory.CreateDirectory(BaseDirectory);
  }

  private static IDeserializer Deserializer() => new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
  private static IDeserializer DeserializerUnSafe() => new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

  private static List<T> Deserialize<T>(string raw, string file)
  {
    try
    {
      return Deserializer().Deserialize<List<T>>(raw) ?? [];
    }
    catch (Exception ex1)
    {
      Log.Error($"{Path.GetFileName(file)}: {ex1.Message}");
      try
      {
        return DeserializerUnSafe().Deserialize<List<T>>(raw) ?? [];
      }
      catch (Exception)
      {
        return [];
      }
    }
  }

  public static Dictionary<string, string> DeserializeData(string raw)
  {
    try
    {
      return Deserializer().Deserialize<Dictionary<string, string>>(raw) ?? [];
    }
    catch
    {
      try
      {
        return DeserializerUnSafe().Deserialize<Dictionary<string, string>>(raw) ?? [];
      }
      catch (Exception)
      {
        return [];
      }
    }
  }
  public static string SerializeData(Dictionary<string, string> data)
  {
    var serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
    return serializer.Serialize(data);
  }
}
