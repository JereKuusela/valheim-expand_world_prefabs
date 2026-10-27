using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Service;

// Tracks raw file changes. The caller decides which files to watch, how to parse them,
// and when a successfully applied snapshot may be acknowledged.
public sealed class FileChangeTracker
{
  private readonly List<FileSystemWatcher> watchers = [];
  private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, string> appliedHashes = new(StringComparer.OrdinalIgnoreCase);
  private double first, last, nextNotice;

  // Owns filesystem watchers for the plugin lifetime. The caller decides which files matter.
  public void Watch(string folder, string pattern, Action<string, string?> changed, ISynchronizeInvoke? synchronizer = null)
  {
    var watcher = new FileSystemWatcher(folder, pattern)
    {
      IncludeSubdirectories = true,
      SynchronizingObject = synchronizer
    };
    watcher.Created += (s, e) => changed(e.FullPath, null);
    watcher.Changed += (s, e) => changed(e.FullPath, null);
    watcher.Renamed += (s, e) => changed(e.FullPath, e.OldFullPath);
    watcher.Deleted += (s, e) => changed(e.FullPath, null);
    watchers.Add(watcher);
    try { watcher.EnableRaisingEvents = true; }
    catch
    {
      watchers.Remove(watcher);
      watcher.Dispose();
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

  public static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
  public int PendingCount => pending.Count;

  public void Queue(string path, double now)
  {
    if (pending.Count == 0) { first = now; nextNotice = 5; }
    pending.Add(path);
    last = now;
  }

  public bool IsQuietFor(double now, double quietSeconds) => PendingCount > 0 && now - last >= quietSeconds;
  public double Elapsed(double now) => now - first;

  public bool ShouldReportDelay(double now)
  {
    if (PendingCount == 0 || Elapsed(now) < nextNotice) return false;
    nextNotice = nextNotice == 5 ? 15 : nextNotice + 15;
    return true;
  }

  public List<string> TakePending()
  {
    var result = new List<string>(pending);
    ClearPending();
    return result;
  }

  public void ClearPending()
  {
    pending.Clear();
    first = last = nextNotice = 0;
  }

  // Read text once and hash exactly the text returned by File.ReadAllText.
  public static bool ReadFile(string path, out string content, out string hash, out string error)
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

  public bool Contains(string path) => appliedHashes.ContainsKey(path);
  public bool ContainsApplied(string path, string hash) => appliedHashes.TryGetValue(path, out var previous) && previous == hash;

  // Call only after the caller has successfully applied its data.
  // A null hash acknowledges deletion of a previously tracked file.
  public void Commit(string path, string? hash)
  {
    if (hash == null) appliedHashes.Remove(path);
    else appliedHashes[path] = hash;
  }

  public void ReplaceAppliedHashes(IEnumerable<KeyValuePair<string, string>> hashes)
  {
    appliedHashes.Clear();
    foreach (var entry in hashes)
      appliedHashes[entry.Key] = entry.Value;
  }
}
