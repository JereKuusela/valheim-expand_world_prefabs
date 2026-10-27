using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;

namespace Service;

// Connects filesystem notifications to EWP's YAML cache and rule loaders.
public static class FileLoading
{
  public static readonly string BaseDirectory = Path.Combine(Paths.ConfigPath, "expand_world");
  public static readonly string BackupDirectory = Path.Combine(Paths.ConfigPath, "expand_world_backups");
  private const string PrefabFileName = "expand_prefabs.yaml";
  private static readonly string PrefabFilePath = Path.Combine(BaseDirectory, PrefabFileName);
  public static readonly string DataGamePath = Path.GetFullPath(Path.Combine("BepInEx", "config", "data"));
  public static readonly string DataProfilePath = Path.GetFullPath(Path.Combine(Paths.ConfigPath, "data"));
  public const string DataPattern = "expand_data*.yaml";
  public const string PrefabPattern = "expand_prefabs*.yaml";

  private static readonly FileChangeTracker Changes = new();
  private static readonly YamlFileCache Cache = new();
  private static bool OverwriteBackup = true;

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
    if (!Directory.Exists(BaseDirectory))
      Directory.CreateDirectory(BaseDirectory);
  }

  private static void EnsurePrefabFile()
  {
    if (!Directory.Exists(BaseDirectory))
      Directory.CreateDirectory(BaseDirectory);
    if (!File.Exists(PrefabFilePath))
      File.WriteAllText(PrefabFilePath, "# Write your entries here. Good luck!");
  }

  public static List<string> GetDataSourceFiles()
  {
    return GetDataSourceFiles(DataGamePath, DataProfilePath, BaseDirectory, DataPattern, PrefabPattern);
  }

  // Watchers belong to the file tracker; EWP only supplies paths and reload callbacks.
  public static void EnsureBaseDirectory() => Directory.CreateDirectory(BaseDirectory);

  public static void SetupWatchers(ConfigFile config, bool overwriteBackup = true)
  {
    Changes.StopWatching();
    OverwriteBackup = overwriteBackup;
    EnsureDataDirectories();
    EnsurePrefabFile();
    try
    {
      Changes.Watch(Path.GetDirectoryName(config.ConfigFilePath)!, Path.GetFileName(config.ConfigFilePath),
        (path, _) => ReloadConfig(path, config), ThreadingHelper.SynchronizingObject);
      WatchFiles(DataGamePath, "*");
      if (DataGamePath != DataProfilePath)
        WatchFiles(DataProfilePath, "*");
      WatchFiles(BaseDirectory, DataPattern);
      WatchFiles(BaseDirectory, PrefabPattern);
    }
    catch
    {
      Changes.StopWatching();
      throw;
    }
  }

  public static void StopWatching() => Changes.StopWatching();

  private static void WatchFiles(string folder, string pattern) =>
    Changes.Watch(folder, pattern, QueueChange, ThreadingHelper.SynchronizingObject);

  private static void ReloadConfig(string path, ConfigFile config)
  {
    if (!File.Exists(path)) return;
    if (ExpandWorld.Prefab.ConfigManager.IsSelfWrite && config == ExpandWorld.Prefab.Config.Main) return;
    BackupFile(path, true);
    try { config.Reload(); }
    catch
    {
      Log.Error($"There was an issue loading your {DisplayPath(config.ConfigFilePath)}");
      Log.Error("Please check your config entries for spelling and format!");
    }
  }

  // Startup and ewp_reload deliberately load the current files immediately.
  public static void ReloadAll()
  {
    if (ExpandWorld.Prefab.Helper.IsClient()) return;
    Changes.ClearPending();
    EnsureDataDirectories();
    EnsurePrefabFile();

    Cache.Clear();
    var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var prefabFiles = GetPrefabFiles();
    var dataFiles = GetDataSourceFiles();
    foreach (var file in prefabFiles)
      CacheFile(file, hashes);
    foreach (var file in dataFiles.Where(file => !IsPrefabPath(file)))
      CacheFile(file, hashes);

    Cache.ApplyData(dataFiles);
    Cache.ApplyPrefabs(prefabFiles);
    Changes.ReplaceAppliedHashes(hashes);
  }

  // Watcher callbacks run on Unity's main thread.
  private static void QueueChange(string path, string? oldPath)
  {
    if (ExpandWorld.Prefab.Helper.IsClient()) return;
    var now = FileChangeTracker.Now;
    if (IsTrackedDataPath(path)) Changes.Queue(NormalizePath(path), now);
    if (oldPath != null && IsTrackedDataPath(oldPath)) Changes.Queue(NormalizePath(oldPath), now);
  }

  public static void ProcessPendingChanges()
  {
    if (ExpandWorld.Prefab.Helper.IsClient() || ZoneSystem.instance == null || Changes.PendingCount == 0) return;
    var now = FileChangeTracker.Now;
    if (Changes.IsQuietFor(now, ExpandWorld.Prefab.Config.ReloadQuietSeconds))
      ReloadChangedFiles(Changes.TakePending());
    else if (Changes.ShouldReportDelay(now))
      Log.Info($"YAML reload pending: {Changes.Elapsed(now):0} seconds elapsed ({Changes.PendingCount} file(s)); waiting for {ExpandWorld.Prefab.Config.ReloadQuietSeconds} seconds without changes.");
  }

  // Automatic reload: apply a batch only after filesystem activity has settled.
  private static void ReloadChangedFiles(List<string> paths)
  {
    var updates = ReadChanges(paths, out var unchanged);

    // A null Entries/hash means a deletion; PruneMissing removes its cached entries during rebuild.
    foreach (var update in updates)
    {
      if (update.Entries == null) continue;
      Cache.Store(update.Path, update.Entries);
      try { BackupFile(update.Path, OverwriteBackup); }
      catch (Exception ex) { Log.Warning($"YAML backup failed for '{DisplayPath(update.Path)}': {ex.Message}"); }
    }

    // Unreported new files are also included. Their hashes are acknowledged after loading.
    var addedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var dataFiles = GetDataSourceFiles();
    var prefabFiles = GetPrefabFiles();
    // A prefab discovered between the two scans can also contain data entries.
    dataFiles = dataFiles.Concat(prefabFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    AddUnreportedNewFiles(dataFiles, addedHashes);
    if (updates.Count == 0 && addedHashes.Count == 0)
    {
      Log.Info(unchanged == paths.Count
        ? $"YAML reload skipped: all {paths.Count} file(s) unchanged."
        : $"YAML reload skipped: no applicable changes ({paths.Count} file(s) checked).");
      return;
    }
    // Files found during the directory scan must trigger their loader as well.
    var prefabsChanged = NeedsPrefabRebuild(updates.Select(update => update.Path), addedHashes.Keys);
    Cache.ApplyData(dataFiles);
    if (prefabsChanged)
      Cache.ApplyPrefabs(prefabFiles);

    // Hashes describe applied contents, not parsed files
    foreach (var update in updates)
      Changes.Commit(update.Path, update.Hash);
    foreach (var added in addedHashes)
      Changes.Commit(added.Key, added.Value);
  }

  public static bool IsPrefabPath(string path, string baseDirectory, string prefabPattern)
  {
    return IsYaml(path) && IsInFolder(path, baseDirectory) && MatchesPattern(path, prefabPattern);
  }

  public static bool IsPrefabPath(string path)
  {
    return IsPrefabPath(path, BaseDirectory, PrefabPattern);
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
    return IsTrackedDataPath(path, DataGamePath, DataProfilePath, BaseDirectory, DataPattern, PrefabPattern);
  }
  // Both watcher changes and files discovered during the directory scan can require a prefab rebuild.
  internal static bool NeedsPrefabRebuild(IEnumerable<string> changedPaths, IEnumerable<string> addedPaths) =>
    changedPaths.Concat(addedPaths).Any(IsPrefabPath);

  // Only changed, successfully parsed files enter the batch. Null means deletion.
  private static List<(string Path, string? Hash, Yaml.MixedFileEntries? Entries)> ReadChanges(List<string> paths, out int unchanged)
  {
    var updates = new List<(string Path, string? Hash, Yaml.MixedFileEntries? Entries)>();
    unchanged = 0;
    foreach (var path in paths)
    {
      if (!File.Exists(path))
      {
        if (Changes.Contains(path)) updates.Add((path, null, null));
        continue;
      }
      if (!FileChangeTracker.ReadFile(path, out var raw, out var hash, out var error))
      {
        Log.Warning($"YAML reload skipped '{DisplayPath(path)}': {error}. Previous valid entries remain active (if any).");
        continue;
      }
      if (Changes.ContainsApplied(path, hash))
      {
        unchanged++;
        continue;
      }
      if (!Yaml.ParseMixed(path, raw, out var entries, out error))
      {
        Log.Warning($"YAML reload skipped '{DisplayPath(path)}': {error}. Previous valid entries remain active (if any).");
        continue;
      }
      updates.Add((path, hash, entries));
    }
    return updates;
  }

  private static void CacheFile(string path, Dictionary<string, string> hashes)
  {
    if (AddFileToCache(path, out var hash)) hashes[path] = hash;
  }

  // A directory scan may find files whose watcher notifications were missed.
  private static void AddUnreportedNewFiles(IEnumerable<string> paths, Dictionary<string, string> addedHashes)
  {
    foreach (var path in paths)
      if (!Cache.Contains(path) && AddFileToCache(path, out var hash))
        addedHashes[path] = hash;
  }

  private static bool AddFileToCache(string path, out string hash)
  {
    if (!FileChangeTracker.ReadFile(path, out var raw, out hash, out var error) ||
        !Yaml.ParseMixed(path, raw, out var entries, out error))
    {
      Log.Warning($"YAML load skipped '{DisplayPath(path)}': {error}");
      return false;
    }
    Cache.Store(path, entries);
    return true;
  }

  private static List<string> GetPrefabFiles()
  {
    var files = GetPatternFiles(BaseDirectory, PrefabPattern);
    files.Reverse();
    return files;
  }

  // File discovery and path filtering are used by startup and watcher handling.
  public static string DisplayPath(string path)
  {
    var root = Path.GetFullPath(Paths.ConfigPath)
      .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    var full = Path.GetFullPath(path);
    return (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full)
      .Replace('\\', '/');
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


}
