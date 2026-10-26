using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
  private static readonly YamlReloadDebounce Pending = new();
  private static readonly Dictionary<string, string> AppliedHashes = new(StringComparer.OrdinalIgnoreCase);
  private static bool OverwriteBackup = true;
  private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

  private static string Hash(string text)
  {
    using var sha = SHA256.Create();
    return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
  }

  private static readonly Dictionary<string, List<ExpandWorld.Prefab.RuleYaml>> PrefabFileEntries = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, List<Data.DataYaml>> DataFileEntries = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, List<ExpandWorld.Prefab.ConfigYaml>> ConfigFileEntries = new(StringComparer.OrdinalIgnoreCase);

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
    OverwriteBackup = overwriteBackup;
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
    if (ExpandWorld.Prefab.Helper.IsClient()) return;
    ClearPending();
    EnsureDataDirectories();
    EnsurePrefabFile();

    var prefabFiles = GetPatternFiles(Yaml.BaseDirectory, PrefabPattern);
    prefabFiles.Reverse();
    PrefabFileEntries.Clear();
    ConfigFileEntries.Clear();
    DataFileEntries.Clear();
    var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var file in prefabFiles)
      LoadInitialFile(file, true, hashes);

    var dataFiles = GetDataSourceFiles();
    foreach (var file in dataFiles)
      if (!IsPrefabPath(file)) LoadInitialFile(file, false, hashes);

    Data.DataLoading.LoadFromFiles(dataFiles, DataFileEntries);
    ExpandWorld.Prefab.ConfigManager.LoadFromFiles(prefabFiles, ConfigFileEntries);
    ExpandWorld.Prefab.Loading.LoadFromFiles(prefabFiles, PrefabFileEntries);
    AppliedHashes.Clear();
    foreach (var entry in hashes)
      AppliedHashes[entry.Key] = entry.Value;
  }

  // A loaded hash must describe the exact contents that were parsed, not a later file read.
  internal static bool TryReadFileSnapshot(string file, out Yaml.MixedFileEntries entries, out string hash, out string error)
  {
    entries = new Yaml.MixedFileEntries();
    hash = "";
    error = "";
    try
    {
      var raw = File.ReadAllText(file);
      if (!Yaml.TryReadMixedFile(file, raw, out entries, out error)) return false;
      hash = Hash(raw);
      return true;
    }
    catch (Exception ex)
    {
      error = $"{ex.GetType().Name}: {ex.Message}";
      return false;
    }
  }

  private static void LoadInitialFile(string file, bool prefab, Dictionary<string, string> hashes)
  {
    if (!TryReadFileSnapshot(file, out var entries, out var hash, out var error))
    {
      Log.Warning($"YAML load skipped '{Yaml.DisplayPath(file)}': {error}");
      return;
    }
    DataFileEntries[file] = entries.DataEntries;
    if (prefab)
    {
      PrefabFileEntries[file] = entries.ScriptEntries;
      ConfigFileEntries[file] = entries.ConfigEntries;
    }
    hashes[file] = hash;
  }

  private static void HandleDataFileChange(string path, string? oldPath, Yaml.FileChangeType type) => QueueChange(path, oldPath);

  private static void HandlePrefabFileChange(string path, string? oldPath, Yaml.FileChangeType type) => QueueChange(path, oldPath);

  private static void QueueChange(string path, string? oldPath)
  {
    if (ExpandWorld.Prefab.Helper.IsClient()) return;
    var now = Now;
    if (IsTrackedDataPath(path)) Pending.Add(NormalizePath(path), now);
    if (oldPath != null && IsTrackedDataPath(oldPath)) Pending.Add(NormalizePath(oldPath), now);
  }

  public static void ClearPending() => Pending.Clear();

  // FileSystemWatcher callbacks are marshalled to the Unity main thread by Yaml.SetupWatcher.
  public static void ExecutePending()
  {
    if (ExpandWorld.Prefab.Helper.IsClient() || ZoneSystem.instance == null || Pending.Count == 0) return;
    var now = Now;
    if (Pending.IsReady(now, ExpandWorld.Prefab.Config.ReloadQuietSeconds))
    {
      var files = Pending.Take();
      ApplyChanges(files);
    }
    else if (Pending.ShouldReportDelay(now))
      Log.Info($"YAML reload pending: {Pending.Elapsed(now):0} seconds elapsed ({Pending.Count} file(s)); waiting for {ExpandWorld.Prefab.Config.ReloadQuietSeconds} seconds without changes.");
  }

  private static void ApplyChanges(List<string> paths)
  {
    // Read and validate the complete batch before changing the shared caches.
    var changes = new List<(string Path, string? Hash, Yaml.MixedFileEntries? Entries)>();
    var unchanged = 0;
    foreach (var path in paths)
    {
      if (!File.Exists(path))
      {
        if (AppliedHashes.ContainsKey(path)) changes.Add((path, null, null));
        continue;
      }
      try
      {
        var raw = File.ReadAllText(path);
        var hash = Hash(raw);
        if (AppliedHashes.TryGetValue(path, out var previous) && previous == hash)
        {
          unchanged++;
          continue;
        }
        if (!Yaml.TryReadMixedFile(path, raw, out var entries, out var error))
        {
          Log.Warning($"YAML reload skipped '{Yaml.DisplayPath(path)}': {error}. Previous valid entries remain active (if any).");
          continue;
        }
        changes.Add((path, hash, entries));
      }
      catch (Exception ex)
      {
        Log.Warning($"YAML reload skipped '{Yaml.DisplayPath(path)}': {ex.GetType().Name}: {ex.Message}. Previous valid entries remain active (if any).");
      }
    }
    if (changes.Count == 0)
    {
      Log.Info(unchanged == paths.Count
        ? $"YAML reload skipped: all {paths.Count} file(s) unchanged."
        : $"YAML reload skipped: no applicable changes ({paths.Count} file(s) checked).");
      return;
    }
    var prefabsChanged = false;
    foreach (var change in changes)
    {
      var path = change.Path;
      var prefab = IsPrefabPath(path);
      if (prefab) prefabsChanged = true;
      if (change.Entries == null)
      {
        DataFileEntries.Remove(path);
        PrefabFileEntries.Remove(path);
        ConfigFileEntries.Remove(path);
      }
      else
      {
        DataFileEntries[path] = change.Entries.DataEntries;
        if (prefab)
        {
          PrefabFileEntries[path] = change.Entries.ScriptEntries;
          ConfigFileEntries[path] = change.Entries.ConfigEntries;
        }
        try { Yaml.BackupFile(path, OverwriteBackup); }
        catch (Exception ex) { Log.Warning($"YAML backup failed for '{Yaml.DisplayPath(path)}': {ex.Message}"); }
      }
    }

    var dataFiles = GetDataSourceFiles();
    PruneStaleCache(DataFileEntries, dataFiles);
    EnsureCache(DataFileEntries, dataFiles, file => ReadDataEntries(file));
    Data.DataLoading.LoadFromFiles(dataFiles, DataFileEntries);
    if (prefabsChanged)
    {
      var prefabFiles = GetPatternFiles(Yaml.BaseDirectory, PrefabPattern);
      prefabFiles.Reverse();
      PruneStaleCache(PrefabFileEntries, prefabFiles);
      PruneStaleCache(ConfigFileEntries, prefabFiles);
      foreach (var file in prefabFiles)
        if (!PrefabFileEntries.ContainsKey(file) || !ConfigFileEntries.ContainsKey(file)) LoadPrefabFile(file);
      ExpandWorld.Prefab.ConfigManager.LoadFromFiles(prefabFiles, ConfigFileEntries);
      ExpandWorld.Prefab.Loading.LoadFromFiles(prefabFiles, PrefabFileEntries);
    }

    // Only acknowledge the file snapshots after the rebuild completed.
    foreach (var change in changes)
    {
      if (change.Hash == null)
        AppliedHashes.Remove(change.Path);
      else
        AppliedHashes[change.Path] = change.Hash;
    }
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

  private static void LoadPrefabFile(string file)
  {
    var mixed = File.Exists(file) ? Yaml.ReadMixedFile(file) : new Yaml.MixedFileEntries();
    PrefabFileEntries[file] = mixed.ScriptEntries;
    DataFileEntries[file] = mixed.DataEntries;
    ConfigFileEntries[file] = mixed.ConfigEntries;
  }

  public static List<Data.DataYaml> ReadDataEntries(string file)
  {
    if (!File.Exists(file)) return [];
    return Yaml.ReadMixedFile(file).DataEntries;
  }

}