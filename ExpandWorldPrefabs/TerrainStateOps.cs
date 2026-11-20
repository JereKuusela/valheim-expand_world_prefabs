using UnityEngine;

namespace Data;

// Terrain operations of EWP terrain rules. They work on the shared terrain payload.
internal sealed partial class TerrainState
{
  internal int Apply(Vector3 pos, TerrainOp.Settings settings)
  {
    // TerrainComp mutates its delta arrays but does not rebuild Heightmap
    // until every action in this operation has finished. Preserve that
    // ordering by sampling the rendered heights once for this transaction.
    var sampledHeights = new float[ReferenceHeights.Count];
    for (var index = 0; index < sampledHeights.Length; index++)
      sampledHeights[index] = CurrentHeight(index);
    var changed = 0;
    if (settings.m_level)
      changed += Level(pos + Vector3.up * settings.m_levelOffset, settings.m_levelRadius, settings.m_square, sampledHeights);
    if (settings.m_raise)
      changed += Raise(pos, settings.m_raiseRadius, settings.m_raiseDelta, settings.m_square, settings.m_raisePower, sampledHeights);
    if (settings.m_smooth)
      changed += Smooth(pos + Vector3.up * settings.m_levelOffset, settings.m_smoothRadius, settings.m_square, settings.m_smoothPower, sampledHeights);
    if (settings.m_paintCleared)
      changed += Paint(pos, settings, sampledHeights);
    return changed;
  }

  private int Level(Vector3 pos, float radius, bool square, float[] sampledHeights)
  {
    WorldToVertex(pos, false, out var centerX, out var centerY);
    var localY = pos.y - Center.y;
    var scaledRadius = radius / Scale;
    var extent = Mathf.CeilToInt(scaledRadius);
    var changed = 0;
    for (var y = centerY - extent; y <= centerY + extent; y++)
    {
      for (var x = centerX - extent; x <= centerX + extent; x++)
      {
        if (!square && Vector2.Distance(new(centerX, centerY), new(x, y)) > scaledRadius) continue;
        if (!InBounds(x, y)) continue;
        var index = y * Pitch + x;
        var delta = localY - sampledHeights[index] + SmoothDelta[index];
        var level = Mathf.Clamp(LevelDelta[index] + delta, -LevelLimit, LevelLimit);
        if (!ModifiedHeight[index] || level != LevelDelta[index] || SmoothDelta[index] != 0f)
          changed++;
        LevelDelta[index] = level;
        SmoothDelta[index] = 0f;
        ModifiedHeight[index] = true;
      }
    }
    return changed;
  }

  private int Raise(Vector3 pos, float radius, float delta, bool square, float power, float[] sampledHeights)
  {
    WorldToVertex(pos, false, out var centerX, out var centerY);
    var localY = pos.y - Center.y;
    var scaledRadius = radius / Scale;
    var extent = Mathf.CeilToInt(scaledRadius);
    var changed = 0;
    for (var y = centerY - extent; y <= centerY + extent; y++)
    {
      for (var x = centerX - extent; x <= centerX + extent; x++)
      {
        if (!InBounds(x, y)) continue;
        var falloff = 1f;
        if (!square)
        {
          var distance = Vector2.Distance(new(centerX, centerY), new(x, y));
          if (distance > scaledRadius) continue;
          if (power > 0f)
          {
            falloff = 1f - distance / scaledRadius;
            if (power != 1f) falloff = Mathf.Pow(falloff, power);
          }
        }
        var current = sampledHeights[y * Pitch + x];
        var appliedDelta = delta * falloff;
        var target = localY + appliedDelta;
        if (delta < 0f && target > current) continue;
        if (delta > 0f)
        {
          if (target < current) continue;
          if (target > current + appliedDelta) target = current + appliedDelta;
        }
        var index = y * Pitch + x;
        var adjustment = target - current + SmoothDelta[index];
        var level = Mathf.Clamp(LevelDelta[index] + adjustment, -LevelLimit, LevelLimit);
        if (!ModifiedHeight[index] || level != LevelDelta[index] || SmoothDelta[index] != 0f)
          changed++;
        LevelDelta[index] = level;
        SmoothDelta[index] = 0f;
        ModifiedHeight[index] = true;
      }
    }
    return changed;
  }

  private int Smooth(Vector3 pos, float radius, bool square, float power, float[] sampledHeights)
  {
    WorldToVertex(pos, false, out var centerX, out var centerY);
    var targetHeight = pos.y - Center.y;
    var scaledRadius = radius / Scale;
    var extent = Mathf.CeilToInt(scaledRadius);
    var changed = 0;
    for (var y = centerY - extent; y <= centerY + extent; y++)
    {
      for (var x = centerX - extent; x <= centerX + extent; x++)
      {
        var distance = Vector2.Distance(new(centerX, centerY), new(x, y));
        if (distance > scaledRadius) continue;
        if (!InBounds(x, y)) continue;
        var ratio = distance / scaledRadius;
        ratio = power == 3f ? ratio * ratio * ratio : Mathf.Pow(ratio, power);
        var index = y * Pitch + x;
        var current = sampledHeights[index];
        var adjustment = Mathf.Lerp(current, targetHeight, 1f - ratio) - current;
        var smooth = Mathf.Clamp(SmoothDelta[index] + adjustment, -SmoothLimit, SmoothLimit);
        if (!ModifiedHeight[index] || smooth != SmoothDelta[index])
          changed++;
        SmoothDelta[index] = smooth;
        ModifiedHeight[index] = true;
      }
    }
    return changed;
  }

  private int Paint(Vector3 pos, TerrainOp.Settings settings, float[] sampledHeights)
  {
    var worldPos = pos;
    worldPos.x -= 0.5f;
    worldPos.z -= 0.5f;
    var height = worldPos.y - Center.y;
    WorldToVertex(worldPos, true, out var centerX, out var centerY);
    var radius = settings.m_paintRadius / Scale;
    var extent = Mathf.CeilToInt(radius);
    var target = PaintColor(settings.m_paintType);
    var changed = 0;
    for (var y = centerY - extent; y <= centerY + extent; y++)
    {
      for (var x = centerX - extent; x <= centerX + extent; x++)
      {
        if (!InBounds(x, y)) continue;
        var index = y * Pitch + x;
        if (settings.m_paintHeightCheck && sampledHeights[index] > height) continue;
        var distance = Vector2.Distance(new(centerX, centerY), new(x, y));
        var strength = Mathf.Pow(1f - Mathf.Clamp01(distance / radius), 0.1f);
        var color = Color.Lerp(PaintMask[index], target, strength);
        color.a = PaintMask[index].a;
        if (!ModifiedPaint[index] || !Same(PaintMask[index], color))
          changed++;
        ModifiedPaint[index] = true;
        PaintMask[index] = color;
      }
    }
    return changed;
  }

  private static Color PaintColor(TerrainModifier.PaintType paintType) => paintType switch
  {
    TerrainModifier.PaintType.Cultivate => Heightmap.m_paintMaskCultivated,
    TerrainModifier.PaintType.Paved => Heightmap.m_paintMaskPaved,
    TerrainModifier.PaintType.Reset => Heightmap.m_paintMaskNothing,
    TerrainModifier.PaintType.ClearVegetation => Heightmap.m_paintMaskClearVegetation,
    TerrainModifier.PaintType.DeepSnow => Heightmap.m_paintMaskDeepSnow,
    _ => Heightmap.m_paintMaskDirt
  };
}
