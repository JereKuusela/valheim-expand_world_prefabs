using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Service;

public sealed class ChangedFile(string path, string content, string hash)
{
  public string Path { get; } = path;
  public string Content { get; } = content;
  public string Hash { get; } = hash;
}

public sealed class FailedFile(string path, string error)
{
  public string Path { get; } = path;
  public string Error { get; } = error;
}

// Only files whose content differs from the last applied version are reported.
public sealed class YamlChanges
{
  public List<ChangedFile> Changed { get; } = [];
  public List<string> Deleted { get; } = [];
  public List<FailedFile> Failed { get; } = [];
  // Every file currently matching the watched sources, in source order.
  public List<string> Files { get; set; } = [];
  public int Unchanged { get; set; }
}

// Debounces file events and hashes contents. Hands the changed files to a single callback,
// which returns whether it applied them; only then are their hashes acknowledged.
public sealed class YamlChangeDetector(Func<YamlChanges, bool> onFlush, Func<double> quietSeconds)
{
  private sealed class Source(string folder, string pattern)
  {
    public string Folder { get; } = folder;
    public string Pattern { get; } = pattern;
  }

  private readonly List<Source> sources = [];
  private readonly List<FileSystemWatcher> watchers = [];
  private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, string> appliedHashes = new(StringComparer.OrdinalIgnoreCase);
  private double first, last, nextNotice;

  public static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
  public int PendingCount => pending.Count;

  public static string NormalizePath(string path)
  {
    try { return Path.GetFullPath(path); }
    catch { return path; }
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
    var wildcardIndex = pattern.IndexOf('*');
    if (wildcardIndex < 0)
      return file.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    var prefix = pattern.Substring(0, wildcardIndex);
    var suffix = pattern.Substring(wildcardIndex + 1);
    return file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
      && file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
  }

  // Source order is the load order of the files.
  public void AddSource(string folder, string pattern) => sources.Add(new Source(folder, pattern));

  public void StartWatching(ISynchronizeInvoke? synchronizer = null)
  {
    StopWatching();
    try
    {
      foreach (var source in sources)
      {
        Directory.CreateDirectory(source.Folder);
        var watcher = new FileSystemWatcher(source.Folder, source.Pattern)
        {
          IncludeSubdirectories = true,
          SynchronizingObject = synchronizer
        };
        watcher.Created += (s, e) => OnEvent(e.FullPath, null);
        watcher.Changed += (s, e) => OnEvent(e.FullPath, null);
        watcher.Renamed += (s, e) => OnEvent(e.FullPath, e.OldFullPath);
        watcher.Deleted += (s, e) => OnEvent(e.FullPath, null);
        watchers.Add(watcher);
        watcher.EnableRaisingEvents = true;
      }
    }
    catch
    {
      StopWatching();
      throw;
    }
  }

  public void StopWatching()
  {
    foreach (var watcher in watchers)
    {
      watcher.EnableRaisingEvents = false;
      watcher.Dispose();
    }
    watchers.Clear();
    ClearPending();
  }

  private void OnEvent(string path, string? oldPath)
  {
    var now = Now;
    if (IsWatched(path)) Queue(NormalizePath(path), now);
    if (oldPath != null && IsWatched(oldPath)) Queue(NormalizePath(oldPath), now);
  }

  private bool IsWatched(string path) =>
    IsYaml(path) && sources.Any(source => IsInFolder(path, source.Folder) && MatchesPattern(path, source.Pattern));

  public void Queue(string path, double now)
  {
    if (pending.Count == 0) { first = now; nextNotice = 5; }
    pending.Add(path);
    last = now;
  }

  // Called every frame by the owner; flushes once events have stopped for the quiet period.
  public void Update() => Update(Now);

  internal void Update(double now)
  {
    if (pending.Count == 0) return;
    var quiet = quietSeconds();
    if (now - last >= quiet) Flush();
    else if (ShouldReportDelay(now))
      Log.Info($"YAML reload pending: {now - first:0} seconds elapsed ({pending.Count} file(s)); waiting for {quiet} seconds without changes.");
  }

  internal bool ShouldReportDelay(double now)
  {
    if (pending.Count == 0 || now - first < nextNotice) return false;
    nextNotice = nextNotice == 5 ? 15 : nextNotice + 15;
    return true;
  }

  internal void Flush()
  {
    var changes = Scan(pending);
    ClearPending();
    if (onFlush(changes)) Commit(changes);
  }

  // Manual reload: forget everything and report all files as changed. Hashes are acknowledged immediately.
  public YamlChanges TakeAll()
  {
    ClearPending();
    appliedHashes.Clear();
    var changes = Scan([]);
    Commit(changes);
    return changes;
  }

  private void ClearPending()
  {
    pending.Clear();
    first = last = nextNotice = 0;
  }

  private List<string> ListFiles()
  {
    var result = new List<string>();
    foreach (var source in sources)
    {
      Directory.CreateDirectory(source.Folder);
      result.AddRange(Directory.GetFiles(source.Folder, source.Pattern, SearchOption.AllDirectories).Select(NormalizePath));
    }
    return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  // A directory listing finds files whose events were missed; only unknown or queued files are read.
  private YamlChanges Scan(IEnumerable<string> queued)
  {
    var files = ListFiles();
    var present = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
    var candidates = new HashSet<string>(queued, StringComparer.OrdinalIgnoreCase);
    candidates.UnionWith(files.Where(file => !appliedHashes.ContainsKey(file)));
    candidates.UnionWith(appliedHashes.Keys.Where(file => !present.Contains(file)));

    var changes = new YamlChanges { Files = files };
    foreach (var path in candidates)
    {
      if (!File.Exists(path))
      {
        if (appliedHashes.ContainsKey(path)) changes.Deleted.Add(path);
        continue;
      }
      if (!TryRead(path, out var content, out var hash, out var error))
        changes.Failed.Add(new FailedFile(path, error));
      else if (appliedHashes.TryGetValue(path, out var previous) && previous == hash)
        changes.Unchanged++;
      else
        changes.Changed.Add(new ChangedFile(path, content, hash));
    }
    return changes;
  }

  private void Commit(YamlChanges changes)
  {
    foreach (var file in changes.Changed) appliedHashes[file.Path] = file.Hash;
    foreach (var path in changes.Deleted) appliedHashes.Remove(path);
  }

  // The hash covers exactly the text that is returned.
  private static bool TryRead(string path, out string content, out string hash, out string error)
  {
    content = "";
    hash = "";
    error = "";
    try
    {
      content = File.ReadAllText(path);
      using var sha = SHA256.Create();
      hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
      return true;
    }
    catch (Exception ex)
    {
      error = $"{ex.GetType().Name}: {ex.Message}";
      return false;
    }
  }
}
