using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace Service;

public static class FileLoading
{
  private const string PrefabFileName = "expand_prefabs.yaml";
  private static readonly string PrefabFilePath = Path.Combine(Yaml.BaseDirectory, PrefabFileName);
  public static readonly string DataGamePath = Path.GetFullPath(Path.Combine("BepInEx", "config", "data"));
  public static readonly string DataProfilePath = Path.GetFullPath(Path.Combine(Paths.ConfigPath, "data"));
  public const string DataPattern = "expand_data*.yaml";
  public const string PrefabPattern = "expand_prefabs*.yaml";
  private static readonly Dictionary<string, List<global::ExpandWorld.Prefab.Data>> PrefabFileEntries = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, List<global::Data.DataData>> DataFileEntries = new(StringComparer.OrdinalIgnoreCase);

  public static string NormalizePath(string path)
  {
    try
    {
      return Path.GetFullPath(path);
    }
    catch
    {
      return path;
    }
  }

  public static bool IsYaml(string path) => path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);

  public static bool IsInFolder(string path, string folder)
  {
    var fullPath = NormalizePath(path);
    var fullFolder = NormalizePath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
      + Path.DirectorySeparatorChar;
    return fullPath.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
  }

  public static bool MatchesPattern(string path, string pattern)
  {
    var file = Path.GetFileName(path);
    if (pattern == "*.yaml")
      return file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);

    var wildcardIndex = pattern.IndexOf('*');
    if (wildcardIndex < 0)
      return file.Equals(pattern, StringComparison.OrdinalIgnoreCase);

    var prefix = pattern.Substring(0, wildcardIndex);
    var suffix = pattern.Substring(wildcardIndex + 1);
    if (prefix != "" && !file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
    if (suffix != "" && !file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
    return true;
  }

  public static List<string> GetPatternFiles(string folder, string pattern)
  {
    if (!Directory.Exists(folder))
      Directory.CreateDirectory(folder);
    return Directory.GetFiles(folder, pattern, SearchOption.AllDirectories)
      .Select(NormalizePath)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();
  }

  public static List<string> GetDataSourceFiles(string gamePath, string profilePath, string baseDirectory, string dataPattern, string prefabPattern)
  {
    if (!Directory.Exists(gamePath))
      Directory.CreateDirectory(gamePath);
    if (!Directory.Exists(profilePath))
      Directory.CreateDirectory(profilePath);
    if (!Directory.Exists(baseDirectory))
      Directory.CreateDirectory(baseDirectory);

    return Directory.GetFiles(gamePath, "*.yaml", SearchOption.AllDirectories)
      .Concat(Directory.GetFiles(profilePath, "*.yaml", SearchOption.AllDirectories))
      .Concat(Directory.GetFiles(baseDirectory, dataPattern, SearchOption.AllDirectories))
      .Concat(Directory.GetFiles(baseDirectory, prefabPattern, SearchOption.AllDirectories))
      .Select(NormalizePath)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();
  }

  public static void EnsureDataDirectories()
  {
    if (!Directory.Exists(DataGamePath))
      Directory.CreateDirectory(DataGamePath);
    if (!Directory.Exists(DataProfilePath))
      Directory.CreateDirectory(DataProfilePath);
    if (!Directory.Exists(Yaml.BaseDirectory))
      Directory.CreateDirectory(Yaml.BaseDirectory);
  }

  private static void EnsurePrefabFile()
  {
    if (!Directory.Exists(Yaml.BaseDirectory))
      Directory.CreateDirectory(Yaml.BaseDirectory);
    if (!File.Exists(PrefabFilePath))
      File.WriteAllText(PrefabFilePath, "# Write your entries here. Good luck!");
  }

  public static List<string> GetDataSourceFiles()
  {
    return GetDataSourceFiles(DataGamePath, DataProfilePath, Yaml.BaseDirectory, DataPattern, PrefabPattern);
  }

  public static void SetupWatchers(bool overwriteBackup = true)
  {
    EnsureDataDirectories();
    EnsurePrefabFile();
    Yaml.SetupWatcher(DataGamePath, "*", HandleDataFileChange, overwriteBackup);
    if (DataGamePath != DataProfilePath)
      Yaml.SetupWatcher(DataProfilePath, "*", HandleDataFileChange, overwriteBackup);
    Yaml.SetupWatcher(DataPattern, HandleDataFileChange, overwriteBackup);
    Yaml.SetupWatcher(PrefabPattern, HandlePrefabFileChange, overwriteBackup);
  }

  public static void ReloadAll()
  {
    if (global::ExpandWorld.Prefab.Helper.IsClient()) return;
    EnsureDataDirectories();
    EnsurePrefabFile();

    var prefabFiles = GetPatternFiles(Yaml.BaseDirectory, PrefabPattern);
    prefabFiles.Reverse();
    PrefabFileEntries.Clear();
    foreach (var file in prefabFiles)
      PrefabFileEntries[file] = ReadScriptEntries(file);

    var dataFiles = GetDataSourceFiles();
    DataFileEntries.Clear();
    foreach (var file in dataFiles)
      DataFileEntries[file] = ReadDataEntries(file);

    global::Data.DataLoading.LoadFromFiles(dataFiles, DataFileEntries);
    global::ExpandWorld.Prefab.Loading.LoadFromFiles(prefabFiles, PrefabFileEntries);
  }

  private static void HandleDataFileChange(string path, string? oldPath, Yaml.FileChangeType type)
  {
    if (global::ExpandWorld.Prefab.Helper.IsClient()) return;
    path = NormalizePath(path);
    oldPath = oldPath == null ? null : NormalizePath(oldPath);
    if (!IsTrackedDataPath(path) && (oldPath == null || !IsTrackedDataPath(oldPath))) return;

    if (oldPath != null && oldPath != path)
      DataFileEntries.Remove(oldPath);

    if (type == Yaml.FileChangeType.Deleted)
      DataFileEntries.Remove(path);
    else if (IsYaml(path) && File.Exists(path))
      DataFileEntries[path] = ReadDataEntries(path);

    var dataFiles = GetDataSourceFiles();
    PruneStaleCache(DataFileEntries, dataFiles);
    EnsureCache(DataFileEntries, dataFiles, file => ReadDataEntries(file));
    global::Data.DataLoading.LoadFromFiles(dataFiles, DataFileEntries);
  }

  private static void HandlePrefabFileChange(string path, string? oldPath, Yaml.FileChangeType type)
  {
    if (global::ExpandWorld.Prefab.Helper.IsClient()) return;
    path = NormalizePath(path);
    oldPath = oldPath == null ? null : NormalizePath(oldPath);
    if (!IsPrefabPath(path) && (oldPath == null || !IsPrefabPath(oldPath))) return;

    if (oldPath != null && oldPath != path)
    {
      PrefabFileEntries.Remove(oldPath);
      DataFileEntries.Remove(oldPath);
    }

    if (type == Yaml.FileChangeType.Deleted)
    {
      PrefabFileEntries.Remove(path);
      DataFileEntries.Remove(path);
    }
    else
    {
      var mixed = Yaml.ReadMixedFile(path, true);
      PrefabFileEntries[path] = mixed.ScriptEntries;
      DataFileEntries[path] = mixed.DataEntries;
    }

    var prefabFiles = GetPatternFiles(Yaml.BaseDirectory, PrefabPattern);
    prefabFiles.Reverse();
    PruneStaleCache(PrefabFileEntries, prefabFiles);
    EnsureCache(PrefabFileEntries, prefabFiles, file => ReadScriptEntries(file));

    var dataFiles = GetDataSourceFiles();
    PruneStaleCache(DataFileEntries, dataFiles);
    EnsureCache(DataFileEntries, dataFiles, file => ReadDataEntries(file));

    global::Data.DataLoading.LoadFromFiles(dataFiles, DataFileEntries);
    global::ExpandWorld.Prefab.Loading.LoadFromFiles(prefabFiles, PrefabFileEntries);
  }

  public static bool IsPrefabPath(string path, string baseDirectory, string prefabPattern)
  {
    return IsYaml(path) && IsInFolder(path, baseDirectory) && MatchesPattern(path, prefabPattern);
  }

  public static bool IsPrefabPath(string path)
  {
    return IsPrefabPath(path, Yaml.BaseDirectory, PrefabPattern);
  }

  public static bool IsTrackedDataPath(string path, string gamePath, string profilePath, string baseDirectory, string dataPattern, string prefabPattern)
  {
    if (!IsYaml(path)) return false;
    if (IsInFolder(path, gamePath)) return true;
    if (IsInFolder(path, profilePath)) return true;
    return IsInFolder(path, baseDirectory) && (MatchesPattern(path, dataPattern) || MatchesPattern(path, prefabPattern));
  }

  public static bool IsTrackedDataPath(string path)
  {
    return IsTrackedDataPath(path, DataGamePath, DataProfilePath, Yaml.BaseDirectory, DataPattern, PrefabPattern);
  }

  public static void PruneStaleCache<T>(Dictionary<string, T> cache, List<string> files)
  {
    var valid = files.ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var key in cache.Keys.ToList())
    {
      if (!valid.Contains(key))
        cache.Remove(key);
    }
  }

  public static void EnsureCache<T>(Dictionary<string, T> cache, List<string> files, Func<string, T> loader)
  {
    foreach (var file in files)
    {
      if (!cache.ContainsKey(file))
        cache[file] = loader(file);
    }
  }

  public static List<global::ExpandWorld.Prefab.Data> ReadScriptEntries(string file, bool migrateScripts = true)
  {
    if (!File.Exists(file)) return [];
    return Yaml.ReadMixedFile(file, migrateScripts).ScriptEntries;
  }

  public static List<global::Data.DataData> ReadDataEntries(string file, bool migrateScripts = true)
  {
    if (!File.Exists(file)) return [];
    return Yaml.ReadMixedFile(file, migrateScripts).DataEntries;
  }

}