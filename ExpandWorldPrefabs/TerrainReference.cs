using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandWorld.Prefab;

/// <summary>
/// Reconstructs the surface that Valheim creates after world generation and
/// static location modifiers, immediately before TerrainComp applies TCData.
/// </summary>
internal static class TerrainReference
{
  private const float ProxyMatchDistance = 1f;
  private const float ZoneHalfDiagonal = 46f;
  private static readonly int LocationProxyHash = "LocationProxy".GetStableHashCode();
  private static readonly int LocationReferenceHash = "locationreference".GetStableHashCode();

  internal static bool TryCreateSurface(Vector3 center, int width, float scale,
    IReadOnlyList<float> baseHeights, Color[] baseMask, out Surface surface,
    out string reason)
  {
    surface = new Surface(baseHeights, baseMask, center, width, scale);
    reason = "reference-worldgen";
    if (ZoneSystem.instance == null)
    {
      reason = "reference-zone-system-unavailable";
      return false;
    }

    var snapshots = new List<ModifierSnapshot>();
    var locations = 0;
    foreach (var instance in ZoneSystem.instance.m_locationInstances.Values)
    {
      var location = instance.m_location;
      if (!instance.m_placed || location == null)
        continue;
      var maximumDistance = Mathf.Max(1f, location.m_exteriorRadius) + ZoneHalfDiagonal;
      if (Utils.DistanceXZ(instance.m_position, center) > maximumDistance)
        continue;

      var proxy = FindLocationProxy(instance.m_position, location.Hash);
      if (proxy == null)
      {
        reason = "reference-location-proxy-missing:" + location.m_prefabName;
        return false;
      }
      if (!TryAddLocation(location, proxy, snapshots, out reason))
        return false;
      locations++;
    }

    snapshots.Sort(ModifierSnapshot.Compare);
    foreach (var snapshot in snapshots)
      surface.Apply(snapshot);
    reason = "reference-locations=" + locations + ":modifiers=" + snapshots.Count;
    return true;
  }

  private static bool TryAddLocation(ZoneSystem.ZoneLocation location, ZDO proxy,
    List<ModifierSnapshot> snapshots, out string reason)
  {
    reason = "reference-location-unavailable:" + location.m_prefabName;
    var reference = location.m_prefab;
    try
    {
      reference.Load();
      var prefab = reference.Asset;
      if (prefab == null) return false;
      var root = prefab.transform;
      var rotation = proxy.GetRotation();
      var modifiers = prefab.GetComponentsInChildren<TerrainModifier>(true);
      foreach (var modifier in modifiers)
      {
        if (HasRandomAncestor(modifier.transform, root))
        {
          reason = "reference-randomized-modifier:" + location.m_prefabName;
          return false;
        }
        if (!modifier.enabled || !IsActiveInPrefab(modifier.transform, root))
          continue;
        if (modifier.m_playerModifiction)
        {
          reason = "reference-player-modifier:" + location.m_prefabName;
          return false;
        }

        var localOffset = Quaternion.Inverse(root.rotation) *
          (modifier.transform.position - root.position);
        var worldPosition = proxy.m_position + rotation * localOffset;
        snapshots.Add(new ModifierSnapshot(modifier, worldPosition,
          snapshots.Count));
      }
      reason = "reference-location-ready:" + location.m_prefabName;
      return true;
    }
    catch (Exception exception)
    {
      reason = "reference-location-error:" + location.m_prefabName + ":" +
        exception.GetType().Name;
      return false;
    }
    finally
    {
      reference.Release();
    }
  }

  private static bool IsActiveInPrefab(Transform transform, Transform root)
  {
    Transform? current = transform;
    while (current != null)
    {
      if (!current.gameObject.activeSelf) return false;
      if (current == root) return true;
      current = current.parent;
    }
    return false;
  }

  private static bool HasRandomAncestor(Transform transform, Transform root)
  {
    Transform? current = transform;
    while (current != null)
    {
      if (current.GetComponent<RandomObject>() != null ||
          current.GetComponent<RandomSpawn>() != null)
        return true;
      if (current == root) return false;
      current = current.parent;
    }
    return false;
  }

  private static ZDO? FindLocationProxy(Vector3 locationPosition,
    int locationHash)
  {
    ZDO? hashMatch = null;
    var hashMatchDistance = float.MaxValue;
    ZDO? colocatedFallback = null;
    var fallbackAmbiguous = false;
    var zone = ZoneSystem.GetZone(locationPosition);
    for (var x = zone.x - 1; x <= zone.x + 1; ++x)
    {
      for (var y = zone.y - 1; y <= zone.y + 1; ++y)
      {
        var zdos = Helper.GetZDOsInSector(new Vector2s(x, y));
        if (zdos == null) continue;
        foreach (var candidate in zdos)
        {
          if (candidate.m_prefab != LocationProxyHash) continue;
          var distance = Utils.DistanceXZ(candidate.m_position, locationPosition);
          if (distance > ProxyMatchDistance) continue;

          if (candidate.GetInt(ZDOVars.s_location, 0) == locationHash ||
              candidate.GetInt(LocationReferenceHash, 0) == locationHash)
          {
            if (distance >= hashMatchDistance) continue;
            hashMatch = candidate;
            hashMatchDistance = distance;
            continue;
          }

          // EWD variants can retain their base location hash in the placed
          // proxy. Position is safe only when it identifies one proxy.
          if (colocatedFallback == null)
            colocatedFallback = candidate;
          else
            fallbackAmbiguous = true;
        }
      }
    }
    return hashMatch ?? (fallbackAmbiguous ? null : colocatedFallback);
  }

  private static bool IsFinite(float value) =>
    !float.IsNaN(value) && !float.IsInfinity(value);

  internal sealed class ModifierSnapshot
  {
    internal readonly Vector3 Position;
    internal readonly int SortOrder;
    internal readonly int Sequence;
    internal readonly bool Level;
    internal readonly float LevelOffset;
    internal readonly float LevelRadius;
    internal readonly bool Square;
    internal readonly bool Smooth;
    internal readonly float SmoothRadius;
    internal readonly float SmoothPower;
    internal readonly bool Paint;
    internal readonly bool PaintHeightCheck;
    internal readonly TerrainModifier.PaintType PaintType;
    internal readonly float PaintRadius;
    internal readonly float PaintStrength;

    internal ModifierSnapshot(TerrainModifier modifier, Vector3 position,
      int sequence)
    {
      Position = position;
      SortOrder = modifier.m_sortOrder;
      Sequence = sequence;
      Level = modifier.m_level;
      LevelOffset = modifier.m_levelOffset;
      LevelRadius = modifier.m_levelRadius;
      Square = modifier.m_square;
      Smooth = modifier.m_smooth;
      SmoothRadius = modifier.m_smoothRadius;
      SmoothPower = modifier.m_smoothPower;
      Paint = modifier.m_paintCleared;
      PaintHeightCheck = modifier.m_paintHeightCheck;
      PaintType = modifier.m_paintType;
      PaintRadius = modifier.m_paintRadius;
      PaintStrength = modifier.m_paintStrength;
    }

    internal static int Compare(ModifierSnapshot left, ModifierSnapshot right)
    {
      var result = left.SortOrder.CompareTo(right.SortOrder);
      if (result != 0) return result;
      result = left.Position.sqrMagnitude.CompareTo(right.Position.sqrMagnitude);
      return result != 0 ? result : left.Sequence.CompareTo(right.Sequence);
    }
  }

  internal sealed class Surface
  {
    private readonly Vector3 Center;
    private readonly int Width;
    private readonly int Pitch;
    private readonly float Scale;
    internal readonly float[] Heights;
    internal readonly Color[] Mask;

    internal Surface(IReadOnlyList<float> baseHeights, Color[] baseMask,
      Vector3 center, int width, float scale)
    {
      Center = center;
      Width = width;
      Pitch = width + 1;
      Scale = scale;
      Heights = new float[baseHeights.Count];
      for (var index = 0; index < baseHeights.Count; index++)
        Heights[index] = baseHeights[index];
      Mask = new Color[baseMask.Length];
      Array.Copy(baseMask, Mask, baseMask.Length);
    }

    internal void Apply(ModifierSnapshot modifier)
    {
      if (modifier.Level)
        Level(modifier.Position + Vector3.up * modifier.LevelOffset,
          modifier.LevelRadius, modifier.Square);
      if (modifier.Smooth)
        Smooth(modifier.Position + Vector3.up * modifier.LevelOffset,
          modifier.SmoothRadius, modifier.SmoothPower);
      if (modifier.Paint)
        Paint(modifier.Position, modifier.PaintRadius, modifier.PaintType,
          modifier.PaintHeightCheck, modifier.PaintStrength);
    }

    internal float SampleHeight(Vector3 position)
    {
      WorldToVertex(position, false, out var x, out var y);
      x = Mathf.Clamp(x, 0, Pitch - 1);
      y = Mathf.Clamp(y, 0, Pitch - 1);
      return Heights[y * Pitch + x];
    }

    private void Level(Vector3 position, float radius, bool square)
    {
      WorldToVertex(position, true, out var centerX, out var centerY);
      var targetHeight = position.y - Center.y;
      var scaledRadius = radius / Scale;
      var extent = Mathf.CeilToInt(scaledRadius);
      var center = new Vector2(centerX, centerY);
      for (var y = centerY - extent; y <= centerY + extent; y++)
      {
        for (var x = centerX - extent; x <= centerX + extent; x++)
        {
          if (!square && Vector2.Distance(center, new Vector2(x, y)) > scaledRadius)
            continue;
          if (InBounds(x, y)) Heights[y * Pitch + x] = targetHeight;
        }
      }
    }

    private void Smooth(Vector3 position, float radius, float power)
    {
      WorldToVertex(position, false, out var centerX, out var centerY);
      var targetHeight = position.y - Center.y;
      var scaledRadius = radius / Scale;
      var extent = Mathf.CeilToInt(scaledRadius);
      var center = new Vector2(centerX, centerY);
      for (var y = centerY - extent; y <= centerY + extent; y++)
      {
        for (var x = centerX - extent; x <= centerX + extent; x++)
        {
          var distance = Vector2.Distance(center, new Vector2(x, y));
          if (distance > scaledRadius || !InBounds(x, y)) continue;
          var ratio = distance / scaledRadius;
          ratio = power == 3f ? ratio * ratio * ratio : Mathf.Pow(ratio, power);
          var index = y * Pitch + x;
          Heights[index] = Mathf.Lerp(Heights[index], targetHeight, 1f - ratio);
        }
      }
    }

    private void Paint(Vector3 position, float radius,
      TerrainModifier.PaintType paintType, bool heightCheck, float strength)
    {
      position.x -= 0.5f;
      position.z -= 0.5f;
      var targetHeight = position.y - Center.y;
      WorldToVertex(position, true, out var centerX, out var centerY);
      var scaledRadius = radius / Scale;
      var extent = Mathf.CeilToInt(scaledRadius);
      var center = new Vector2(centerX, centerY);
      var target = PaintColor(paintType);
      for (var y = centerY - extent; y <= centerY + extent; y++)
      {
        for (var x = centerX - extent; x <= centerX + extent; x++)
        {
          if (!InBounds(x, y)) continue;
          var index = y * Pitch + x;
          if (heightCheck && Heights[index] > targetHeight) continue;
          var distance = Vector2.Distance(center, new Vector2(x, y));
          var amount = Mathf.Pow(1f - Mathf.Clamp01(distance / scaledRadius),
            0.1f) * strength;
          var alpha = Mask[index].a;
          Mask[index] = Color.Lerp(Mask[index], target, amount);
          if (paintType != TerrainModifier.PaintType.ClearVegetation)
            Mask[index].a = alpha;
        }
      }
    }

    private void WorldToVertex(Vector3 position, bool mask, out int x, out int y)
    {
      var relative = position - Center;
      var half = mask ? (Width + 1) / 2 : Width / 2;
      x = Mathf.FloorToInt(relative.x / Scale + 0.5f + half);
      y = Mathf.FloorToInt(relative.z / Scale + 0.5f + half);
    }

    private bool InBounds(int x, int y) =>
      x >= 0 && y >= 0 && x < Pitch && y < Pitch;

    private static Color PaintColor(TerrainModifier.PaintType paintType) =>
      paintType switch
      {
        TerrainModifier.PaintType.Cultivate => Heightmap.m_paintMaskCultivated,
        TerrainModifier.PaintType.Paved => Heightmap.m_paintMaskPaved,
        TerrainModifier.PaintType.Reset => Heightmap.m_paintMaskNothing,
        TerrainModifier.PaintType.ClearVegetation => Heightmap.m_paintMaskClearVegetation,
        TerrainModifier.PaintType.DeepSnow => Heightmap.m_paintMaskDeepSnow,
        _ => Heightmap.m_paintMaskDirt
      };
  }
}
