using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

/// <summary>
/// Applies the terrain of a blueprint. Zones that aren't ready yet are retried like other terrain operations.
/// </summary>
internal class TerrainSnapshot
{
  private const int MaxZones = 64;
  private static readonly List<TerrainSnapshot> Snapshots = [];

  private readonly string Name;
  private readonly BlueprintTerrain<float>? Height;
  private readonly BlueprintTerrain<Color>? Paint;
  private readonly Vector3 Pos;
  private readonly Quaternion Rot;
  private double Due;
  private HashSet<Vector2s>? PendingZones;
  private int Attempt;

  private TerrainSnapshot(double due, string name, BlueprintTerrain<float>? height, BlueprintTerrain<Color>? paint, Vector3 pos, Quaternion rot)
  {
    Due = due;
    Name = name;
    Height = height;
    Paint = paint;
    Pos = pos;
    Rot = rot;
  }

  public static void Clear() => Snapshots.Clear();

  public static void Add(float delay, string name, BlueprintTerrain<float>? height, BlueprintTerrain<Color>? paint, Vector3 pos, Quaternion rot)
  {
    if (height?.HasValues != true && paint?.HasValues != true) return;
    var snapshot = new TerrainSnapshot(ZNet.instance.m_netTime + delay, name, height, paint, pos, rot);
    if (delay > 0f || !snapshot.ExecuteAction())
      Snapshots.Add(snapshot);
  }

  public static void Execute()
  {
    for (var i = 0; i < Snapshots.Count; i++)
    {
      var snapshot = Snapshots[i];
      if (snapshot.Due > ZNet.instance.m_netTime) continue;
      if (!snapshot.ExecuteAction()) continue;
      Snapshots.RemoveAt(i);
      i--;
    }
  }

  private bool ExecuteAction()
  {
    try
    {
      Attempt++;
      PendingZones = Apply(Name, Height, Paint, Pos, Rot, PendingZones);
      if (PendingZones.Count == 0) return true;
      if (Attempt >= DelayedTerrain.MaximumAttempts)
      {
        Log.Warning($"Blueprint {Name}: Terrain failed after {Attempt} attempts. Pending zones: " +
          string.Join(";", PendingZones.OrderBy(zone => zone.x).ThenBy(zone => zone.y).Select(zone => $"{zone.x},{zone.y}")) + ".");
        return true;
      }
      Due = ZNet.instance.m_netTime + DelayedTerrain.RetryIntervalSeconds;
      return false;
    }
    catch (Exception exception)
    {
      Log.Warning($"Blueprint {Name}: Terrain failed and won't be retried: {exception.Message}");
      return true;
    }
  }

  // Returns the zones that must be retried.
  internal static HashSet<Vector2s> Apply(string name, BlueprintTerrain<float>? height, BlueprintTerrain<Color>? paint,
    Vector3 pos, Quaternion rot, HashSet<Vector2s>? onlyZones = null)
  {
    HashSet<Vector2s> retry = [];
    var radius = Mathf.Max(height?.GetRadius() ?? 0f, paint?.GetRadius() ?? 0f);
    // Nodes are rounded to the nearest snapshot node, so the area extends a bit past the outermost nodes.
    var margin = Mathf.Max(1f, height?.DistanceBetweenNodes ?? 0f, paint?.DistanceBetweenNodes ?? 0f);
    var applicationRadius = radius + margin;
    if (float.IsNaN(applicationRadius) || float.IsInfinity(applicationRadius))
      throw new InvalidOperationException("Terrain radius is invalid.");
    var min = ZoneSystem.GetZone(pos - new Vector3(applicationRadius, 0f, applicationRadius));
    var max = ZoneSystem.GetZone(pos + new Vector3(applicationRadius, 0f, applicationRadius));
    var count = ((long)max.x - min.x + 1) * ((long)max.y - min.y + 1);
    if (count > MaxZones)
    {
      Log.Warning($"Blueprint {name}: Terrain covers an unsupported number of zones ({count}).");
      return retry;
    }

    Func<Vector3, float?>? sampleHeight = height == null ? null : node => height.FindNearest(node, pos, rot);
    Func<Vector3, Color?>? samplePaint = paint == null ? null : node => paint.FindNearest(node, pos, rot);
    var changed = false;
    for (var x = min.x; x <= max.x; x++)
    {
      for (var y = min.y; y <= max.y; y++)
      {
        Vector2s zone = new(x, y);
        if (onlyZones != null && !onlyZones.Contains(zone)) continue;
        if (!HasValues(zone, sampleHeight, samplePaint)) continue;
        if (!ZoneSystem.instance.IsZoneGenerated(zone))
        {
          retry.Add(zone);
          continue;
        }
        var compiler = TerrainManager.FindCompiler(zone);
        if (compiler == null)
        {
          TerrainManager.GenerateCompiler(zone);
          retry.Add(zone);
          continue;
        }
        if (!SerializedTerrain.ApplySnapshot(compiler, zone, sampleHeight, samplePaint, pos, applicationRadius, out var reason))
        {
          retry.Add(zone);
          continue;
        }
        if (reason.StartsWith("snapshot-saved", StringComparison.Ordinal))
        {
          changed = true;
          Refresh(compiler);
        }
      }
    }
    if (changed)
      ClutterSystem.instance?.ResetGrass(pos, applicationRadius);
    return retry;
  }

  private static bool HasValues(Vector2s zone, Func<Vector3, float?>? sampleHeight, Func<Vector3, Color?>? samplePaint)
  {
    var center = ZoneSystem.GetZonePos(zone);
    // Same node layout as the terrain compiler: 65 x 65 vertices, one meter apart.
    for (var z = 0; z <= 64; z++)
    {
      for (var x = 0; x <= 64; x++)
      {
        var node = center + new Vector3(x - 32, 0f, z - 32);
        if (sampleHeight?.Invoke(node).HasValue == true || samplePaint?.Invoke(node).HasValue == true)
          return true;
      }
    }
    return false;
  }

  // A loaded compiler would otherwise show the new data only when it notices the revision change.
  private static void Refresh(ZDO compiler)
  {
    var instance = ZNetScene.instance.FindInstance(compiler.m_uid);
    var terrain = instance ? instance.GetComponent<TerrainComp>() : null;
    if (terrain == null || !terrain.m_initialized || terrain.m_hmap == null) return;
    if (terrain.Load())
      terrain.m_hmap.Poke();
  }
}
