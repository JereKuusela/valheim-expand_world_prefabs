using System;
using System.Collections.Generic;
using System.IO;
using Data;
using UnityEngine;

namespace ExpandWorld.Prefab;

/// <summary>
/// Dedicated-server fallback for generated zones whose terrain compiler ZDO
/// exists but whose Heightmap/TerrainComp scene objects are intentionally not
/// loaded. It edits Valheim's native TCData payload; clients still consume the
/// result through their ordinary TerrainComp implementation.
/// </summary>
internal static class SerializedTerrain
{
  private const int Width = 64;
  private const float Scale = 1f;

  internal static bool Apply(ZDO compiler, Vector2s zone, Vector3 pos,
    TerrainOp.Settings settings, out string reason)
  {
    if (!TryLoadState(compiler, zone, out var state, out var storedBefore, out var referenceReason, out reason))
      return false;
    var mutationWrites = state.Apply(pos, settings);
    var paintOnly = settings.m_paintCleared && !settings.m_level && !settings.m_raise && !settings.m_smooth;
    if (paintOnly && mutationWrites == 0)
    {
      // The authoritative payload may already be correct while an owning peer
      // still renders an older revision. Republish it instead of reporting a
      // dispatch-only success.
      if (storedBefore != null)
      {
        TerrainSync.PublishNativeSave(compiler);
      }
      reason = storedBefore == null ? "paint-noop-no-payload" : "paint-noop-republished";
      return true;
    }

    state.Operations++;
    state.LastOperationPoint = pos;
    state.LastOperationRadius = settings.GetRadius();
    var bytes = state.Save();
    TerrainSync.Commit(compiler, Utils.Compress(bytes));
    var storedAfter = compiler.GetByteArray(ZDOVars.s_TCData);
    if (!HasSavedOperation(storedAfter, state.Operations))
      throw new InvalidOperationException("Serialized terrain write returned without a matching stored operation receipt.");
    reason = "serialized-saved:" + referenceReason;
    return true;
  }

  // Sets exact heights and paint from a blueprint terrain. The samplers return the value for a world position or null.
  internal static bool ApplySnapshot(ZDO compiler, Vector2s zone, Func<Vector3, float?>? sampleHeight,
    Func<Vector3, Color?>? samplePaint, Vector3 pos, float radius, out string reason)
  {
    if (!TryLoadState(compiler, zone, out var state, out _, out var referenceReason, out reason))
      return false;
    if (state.ApplySnapshot(sampleHeight, samplePaint, pos.y) == 0)
    {
      reason = "snapshot-noop";
      return true;
    }
    state.Operations++;
    state.LastOperationPoint = pos;
    state.LastOperationRadius = radius;
    TerrainSync.Commit(compiler, Utils.Compress(state.Save()));
    if (!HasSavedOperation(compiler.GetByteArray(ZDOVars.s_TCData), state.Operations))
      throw new InvalidOperationException("Serialized terrain write returned without a matching stored operation receipt.");
    reason = "snapshot-saved:" + referenceReason;
    return true;
  }

  private static bool TryLoadState(ZDO compiler, Vector2s zone, out TerrainState state, out byte[]? storedBefore,
    out string referenceReason, out string reason)
  {
    state = null!;
    storedBefore = null;
    referenceReason = "";
    reason = "unknown";
    if (HeightmapBuilder.instance == null || WorldGenerator.instance == null)
    {
      reason = "terrain-builder-unavailable";
      return false;
    }

    var center = ZoneSystem.GetZonePos(zone);
    var build = HeightmapBuilder.instance.RequestTerrainSync(center, Width, Scale, false, WorldGenerator.instance);
    var pitch = Width + 1;
    var count = pitch * pitch;
    if (build == null || build.m_baseHeights == null || build.m_baseHeights.Count != count ||
        build.m_baseMask == null || build.m_baseMask.Length != count)
    {
      var heightCount = build?.m_baseHeights?.Count ?? -1;
      var maskCount = build?.m_baseMask?.Length ?? -1;
      reason = $"terrain-build-data-not-ready:heights={heightCount}:mask={maskCount}:expected={count}";
      return false;
    }

    if (!TerrainReference.TryCreateSurface(center, Width, Scale,
        build.m_baseHeights, build.m_baseMask, out var reference,
        out referenceReason))
    {
      reason = referenceReason;
      return false;
    }

    storedBefore = compiler.GetByteArray(ZDOVars.s_TCData);
    state = TerrainState.Load(storedBefore, build.m_baseHeights,
      build.m_baseMask, reference.Heights, reference.Mask, center, Width, Scale);
    return true;
  }

  private static bool HasSavedOperation(byte[]? bytes, int operations)
  {
    if (bytes == null) return false;
    var package = new ZPackage(Utils.Decompress(bytes));
    return package.ReadInt() == 1 && package.ReadInt() == operations;
  }
}
