using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;

namespace ExpandWorldPrefabs.Tests;

// BepInEx paths are global. Each affected fixture restores them and runs non-parallel.
internal sealed class TestPathScope : IDisposable
{
  private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
  private readonly Dictionary<FieldInfo, object?> previousPaths;
  private readonly string directory;

  public TestPathScope()
  {
    previousPaths = typeof(Paths).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
      .Where(field => !field.IsInitOnly).ToDictionary(field => field, field => (object?)field.GetValue(null));
    directory = Path.Combine(Path.GetTempPath(), "ewp-file-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    typeof(Paths).GetMethod("SetExecutablePath", Static)!.Invoke(null,
      [Path.Combine(directory, "valheim.exe"), Path.Combine(directory, "BepInEx"), null, null]);
  }

  public void Dispose()
  {
    foreach (var pair in previousPaths) pair.Key.SetValue(null, pair.Value);
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
  }
}
