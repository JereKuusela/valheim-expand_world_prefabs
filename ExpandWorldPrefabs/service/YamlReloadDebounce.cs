using System;
using System.Collections.Generic;

namespace Service;

// Main-thread-only debounce for YAML filesystem watcher notifications.
public sealed class YamlReloadDebounce
{
  private readonly HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
  private double first, last, nextNotice;
  public int Count => paths.Count;

  public void Add(string path, double now)
  {
    if (paths.Count == 0) { first = now; nextNotice = 5; }
    paths.Add(path);
    last = now;
  }

  public bool IsReady(double now, double quietSeconds) => Count > 0 && now - last >= quietSeconds;
  public double Elapsed(double now) => now - first;

  public bool ShouldReportDelay(double now)
  {
    if (Count == 0 || Elapsed(now) < nextNotice) return false;
    nextNotice = nextNotice == 5 ? 15 : nextNotice + 15;
    return true;
  }

  public List<string> Take()
  {
    var result = new List<string>(paths);
    Clear();
    return result;
  }

  public void Clear()
  {
    paths.Clear();
    first = last = nextNotice = 0;
  }
}
