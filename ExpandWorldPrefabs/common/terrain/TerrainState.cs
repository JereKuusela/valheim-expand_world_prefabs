// Shared code: keep identical in EWD and EWP (common/). Sync changes to both.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Data;

/// <summary>
/// The native terrain compiler payload (TCData) of one zone. It can be edited without a loaded Heightmap,
/// so terrain can be changed on a server and for zones that aren't loaded.
/// </summary>
internal sealed partial class TerrainState
{
  // Maximum level change from the reference surface. Vanilla is 8, some mods change it. Zero or less means no limit.
  internal static float LevelLimit
  {
    get => levelLimit;
    set => levelLimit = value > 0f ? value : float.PositiveInfinity;
  }
  private static float levelLimit = 8f;
  private const float SmoothLimit = 1f;

  private readonly IReadOnlyList<float> BaseHeights;
  private readonly IReadOnlyList<float> ReferenceHeights;
  private readonly Vector3 Center;
  private readonly int Width;
  private readonly int Pitch;
  private readonly float Scale;
  private readonly bool[] ModifiedHeight;
  private readonly float[] LevelDelta;
  private readonly float[] SmoothDelta;
  private readonly bool[] ModifiedPaint;
  private readonly Color[] PaintMask;
  // Anything after the known data is kept as is, so newer game versions don't lose data.
  private byte[] TrailingData = [];

  internal int Operations;
  internal Vector3 LastOperationPoint;
  internal float LastOperationRadius;

  private TerrainState(IReadOnlyList<float> baseHeights,
    IReadOnlyList<float> referenceHeights, Color[] referenceMask,
    Vector3 center, int width, float scale)
  {
    BaseHeights = baseHeights;
    ReferenceHeights = referenceHeights;
    Center = center;
    Width = width;
    Pitch = width + 1;
    Scale = scale;
    var count = Pitch * Pitch;
    ModifiedHeight = new bool[count];
    LevelDelta = new float[count];
    SmoothDelta = new float[count];
    ModifiedPaint = new bool[count];
    PaintMask = new Color[count];
    Array.Copy(referenceMask, PaintMask, count);
  }

  internal static TerrainState Load(byte[]? compressed, IReadOnlyList<float> baseHeights,
    Color[] baseMask, Vector3 center, int width, float scale)
    => Load(compressed, baseHeights, baseMask, baseHeights, baseMask,
      center, width, scale);

  // Reference is the surface that deltas are relative to. It can differ from the base when locations modify the terrain.
  internal static TerrainState Load(byte[]? compressed,
    IReadOnlyList<float> baseHeights, Color[] baseMask,
    IReadOnlyList<float> referenceHeights, Color[] referenceMask,
    Vector3 center, int width, float scale)
  {
    if (width <= 0 || width > 2048)
      throw new InvalidDataException($"Terrain width {width} is not supported.");
    var count = (width + 1) * (width + 1);
    if (baseHeights.Count != count || baseMask.Length != count ||
        referenceHeights.Count != count || referenceMask.Length != count)
      throw new InvalidDataException("Canonical terrain grid size does not match the zone pitch.");
    var state = new TerrainState(baseHeights, referenceHeights,
      referenceMask, center, width, scale);
    if (compressed == null) return state;

    var package = new ZPackage(Utils.Decompress(compressed));
    var version = package.ReadInt();
    if (version != 1) throw new InvalidDataException("Unsupported terrain compiler payload version " + version + ".");
    state.Operations = package.ReadInt();
    state.LastOperationPoint = package.ReadVector3();
    state.LastOperationRadius = package.ReadSingle();
    if (!IsFinite(state.LastOperationPoint) || !IsFinite(state.LastOperationRadius))
      throw new InvalidDataException("Terrain compiler operation metadata is not finite.");
    var heightCount = package.ReadInt();
    if (heightCount != count) throw new InvalidDataException("Terrain height array size does not match the canonical zone pitch.");
    for (var index = 0; index < heightCount; index++)
    {
      state.ModifiedHeight[index] = package.ReadBool();
      if (!state.ModifiedHeight[index]) continue;
      state.LevelDelta[index] = package.ReadSingle();
      state.SmoothDelta[index] = package.ReadSingle();
      if (!IsFinite(state.LevelDelta[index]) || !IsFinite(state.SmoothDelta[index]))
        throw new InvalidDataException("Terrain compiler height data is not finite.");
    }

    var paintCount = package.ReadInt();
    if (paintCount != count && paintCount != width * width)
      throw new InvalidDataException("Terrain paint array size does not match a supported native layout.");
    var modified = new bool[paintCount];
    var colors = new Color[paintCount];
    for (var index = 0; index < paintCount; index++)
    {
      modified[index] = package.ReadBool();
      if (!modified[index]) continue;
      colors[index] = new(package.ReadSingle(), package.ReadSingle(), package.ReadSingle(), package.ReadSingle());
      if (!IsFinite(colors[index]))
        throw new InvalidDataException("Terrain compiler paint data is not finite.");
    }
    if (paintCount == count)
    {
      Array.Copy(modified, state.ModifiedPaint, count);
      for (var index = 0; index < count; index++)
        if (modified[index]) state.PaintMask[index] = colors[index];
    }
    else
    {
      for (var y = 0; y < state.Pitch; y++)
      {
        for (var x = 0; x < state.Pitch; x++)
        {
          var source = Math.Min(y, width - 1) * width + Math.Min(x, width - 1);
          var target = y * state.Pitch + x;
          state.ModifiedPaint[target] = modified[source];
          if (modified[source]) state.PaintMask[target] = colors[source];
        }
      }
    }
    var remaining = package.Size() - package.GetPos();
    if (remaining < 0) throw new InvalidDataException("Terrain compiler data ended at an invalid position.");
    if (remaining > 0) state.TrailingData = package.ReadByteArray(remaining);
    return state;
  }

  // Not compressed.
  internal byte[] Save()
  {
    var package = new ZPackage();
    package.Write(1);
    package.Write(Operations);
    package.Write(LastOperationPoint);
    package.Write(LastOperationRadius);
    package.Write(ModifiedHeight.Length);
    for (var index = 0; index < ModifiedHeight.Length; index++)
    {
      package.Write(ModifiedHeight[index]);
      if (!ModifiedHeight[index]) continue;
      package.Write(LevelDelta[index]);
      package.Write(SmoothDelta[index]);
    }
    package.Write(ModifiedPaint.Length);
    for (var index = 0; index < ModifiedPaint.Length; index++)
    {
      package.Write(ModifiedPaint[index]);
      if (!ModifiedPaint[index]) continue;
      package.Write(PaintMask[index].r);
      package.Write(PaintMask[index].g);
      package.Write(PaintMask[index].b);
      package.Write(PaintMask[index].a);
    }
    foreach (var value in TrailingData)
      package.Write(value);
    return package.GetArray();
  }

  // Heights are set to exact values: the level delta absorbs the difference to the current surface.
  // The samplers return the value for a world position or null. Returns the number of changed nodes.
  internal int ApplySnapshot(Func<Vector3, float?>? sampleHeight, Func<Vector3, Color?>? samplePaint, float placementY)
  {
    var changed = 0;
    for (var z = 0; z < Pitch; z++)
    {
      for (var x = 0; x < Pitch; x++)
      {
        var index = z * Pitch + x;
        var node = Center + new Vector3((x - Width / 2) * Scale, 0f, (z - Width / 2) * Scale);
        var height = sampleHeight?.Invoke(node);
        if (height.HasValue)
        {
          // Not clamped: the game applies its own limit when loading, which some mods change or remove.
          var level = height.Value + placementY - Center.y - ReferenceHeights[index];
          if (!IsFinite(level))
            throw new InvalidDataException("Terrain snapshot height is not finite.");
          if (!ModifiedHeight[index] || level != LevelDelta[index] || SmoothDelta[index] != 0f)
            changed++;
          LevelDelta[index] = level;
          SmoothDelta[index] = 0f;
          ModifiedHeight[index] = true;
        }
        var paint = samplePaint?.Invoke(node);
        if (paint.HasValue)
        {
          if (!IsFinite(paint.Value))
            throw new InvalidDataException("Terrain snapshot paint is not finite.");
          // Even "nothing" is explicit, so the biome paint of the destination doesn't leak through.
          if (!ModifiedPaint[index] || !Same(PaintMask[index], paint.Value))
            changed++;
          ModifiedPaint[index] = true;
          PaintMask[index] = paint.Value;
        }
      }
    }
    return changed;
  }

  internal float SampleHeight(Vector3 pos)
  {
    WorldToVertex(pos, false, out var x, out var y);
    x = Mathf.Clamp(x, 0, Pitch - 1);
    y = Mathf.Clamp(y, 0, Pitch - 1);
    return CurrentHeight(y * Pitch + x);
  }

  internal float SampleBaseHeight(Vector3 pos)
  {
    WorldToVertex(pos, false, out var x, out var y);
    x = Mathf.Clamp(x, 0, Pitch - 1);
    y = Mathf.Clamp(y, 0, Pitch - 1);
    return BaseHeights[y * Pitch + x];
  }

  internal float SampleReferenceHeight(Vector3 pos)
  {
    WorldToVertex(pos, false, out var x, out var y);
    x = Mathf.Clamp(x, 0, Pitch - 1);
    y = Mathf.Clamp(y, 0, Pitch - 1);
    return ReferenceHeights[y * Pitch + x];
  }

  private float CurrentHeight(int index) => Mathf.Clamp(
    ReferenceHeights[index] + LevelDelta[index] + SmoothDelta[index],
    ReferenceHeights[index] - LevelLimit,
    ReferenceHeights[index] + LevelLimit);

  private void WorldToVertex(Vector3 pos, bool mask, out int x, out int y)
  {
    var relative = pos - Center;
    var half = mask ? (Width + 1) / 2 : Width / 2;
    x = Mathf.FloorToInt(relative.x / Scale + 0.5f + half);
    y = Mathf.FloorToInt(relative.z / Scale + 0.5f + half);
  }

  private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Pitch && y < Pitch;

  private static bool Same(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

  private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
  private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
  private static bool IsFinite(Color value) => IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);
}
