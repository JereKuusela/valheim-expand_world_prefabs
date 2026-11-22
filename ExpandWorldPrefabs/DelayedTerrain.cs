using System;
using System.Collections.Generic;
using System.Linq;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class DelayedTerrain
{
  internal const float RetryIntervalSeconds = 1f;
  internal const int MaximumAttempts = 12;

  private static readonly DelayedQueue<DelayedTerrain> Terrains = new();
  private double Due;
  private readonly Vector3 Pos;
  private readonly float Size;
  private readonly TerrainOp.Settings Settings;
  private readonly float ResetRadius;
  private HashSet<Vector2s>? PendingZones;
  private int Attempt;

  private DelayedTerrain(double due, Vector3 pos, float size, TerrainOp.Settings settings, float resetRadius)
  {
    Due = due;
    Pos = pos;
    Size = size;
    Settings = settings;
    ResetRadius = resetRadius;
  }

  public static void Clear() => Terrains.Clear();

  public static void Add(float delay, Vector3 pos, float size, TerrainOp.Settings settings, float resetRadius)
  {
    if (TerrainManager.GenerateCompilers(pos, size)) delay = Mathf.Max(delay, 1f);
    var terrain = new DelayedTerrain(ZNet.instance.m_netTime + delay, pos, size, settings, resetRadius);
    if (delay > 0f || !terrain.ExecuteAction())
      Terrains.Add(terrain.Due, terrain);
  }

  public static void Execute() => Terrains.Drain(ZNet.instance.m_netTime,
    static terrain => terrain.ExecuteAction() ? null : terrain.Due);

  private bool ExecuteAction()
  {
    try
    {
      Attempt++;
      PendingZones = TerrainManager.Modify(Pos, Size, Settings, ResetRadius, PendingZones);
      if (PendingZones.Count == 0) return true;
      if (Attempt >= MaximumAttempts)
      {
        Log.Warning("Terrain operation failed after " + Attempt + " attempts. Pending zones: " +
          string.Join(";", PendingZones.OrderBy(zone => zone.x).ThenBy(zone => zone.y).Select(zone => $"{zone.x},{zone.y}")) + ".");
        return true;
      }
      Due = ZNet.instance.m_netTime + RetryIntervalSeconds;
      return false;
    }
    catch (Exception exception)
    {
      // A partly applied non-idempotent operation must not be replayed.
      Log.Warning("Terrain operation failed and won't be retried: " + exception.Message);
      return true;
    }
  }
}
