using System;
using HarmonyLib;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

internal enum TerrainPacketResult
{
  Native,
  Ewp,
  InvalidEwp
}

/// <summary>
/// Separates EWP's expression-driven terrain settings from Valheim's native
/// RPC_ApplyOperation packet. Deep North serializes a TerrainOp prefab hash;
/// EWP must retain its evaluated settings without changing native packets.
/// </summary>
internal static class TerrainProtocol
{
  internal const int Version = 1;
  internal static readonly int Marker = "EWP_TerrainOperation".GetStableHashCode();
  internal static readonly int Marker2 = "DynamicSettings".GetStableHashCode();

  internal static ZPackage Write(Vector3 pos, TerrainOp.Settings settings)
  {
    var pkg = new ZPackage();
    pkg.Write(Marker);
    pkg.Write(Marker2);
    pkg.Write(Version);
    pkg.Write(pos);
    pkg.Write(settings.m_levelOffset);
    pkg.Write(settings.m_level);
    pkg.Write(settings.m_levelRadius);
    pkg.Write(settings.m_square);
    pkg.Write(settings.m_raise);
    pkg.Write(settings.m_raiseRadius);
    pkg.Write(settings.m_raisePower);
    pkg.Write(settings.m_raiseDelta);
    pkg.Write(settings.m_smooth);
    pkg.Write(settings.m_smoothRadius);
    pkg.Write(settings.m_smoothPower);
    pkg.Write(settings.m_paintCleared);
    pkg.Write(settings.m_paintHeightCheck);
    pkg.Write((int)settings.m_paintType);
    pkg.Write(settings.m_paintRadius);
    return pkg;
  }

  internal static TerrainPacketResult Read(ZPackage pkg, out Vector3 pos, out TerrainOp.Settings? settings)
  {
    pos = Vector3.zero;
    settings = null;
    var start = pkg.GetPos();
    try
    {
      if (pkg.ReadInt() != Marker)
        return RestoreNative(pkg, start);
      if (pkg.ReadInt() != Marker2)
        return RestoreNative(pkg, start);
    }
    catch
    {
      return RestoreNative(pkg, start);
    }

    try
    {
      if (pkg.ReadInt() != Version)
        return TerrainPacketResult.InvalidEwp;
      pos = pkg.ReadVector3();
      settings = new TerrainOp.Settings
      {
        m_levelOffset = pkg.ReadSingle(),
        m_level = pkg.ReadBool(),
        m_levelRadius = pkg.ReadSingle(),
        m_square = pkg.ReadBool(),
        m_raise = pkg.ReadBool(),
        m_raiseRadius = pkg.ReadSingle(),
        m_raisePower = pkg.ReadSingle(),
        m_raiseDelta = pkg.ReadSingle(),
        m_smooth = pkg.ReadBool(),
        m_smoothRadius = pkg.ReadSingle(),
        m_smoothPower = pkg.ReadSingle(),
        m_paintCleared = pkg.ReadBool(),
        m_paintHeightCheck = pkg.ReadBool(),
        m_paintType = (TerrainModifier.PaintType)pkg.ReadInt(),
        m_paintRadius = pkg.ReadSingle()
      };
      return TerrainPacketResult.Ewp;
    }
    catch
    {
      pkg.SetPos(start);
      return TerrainPacketResult.InvalidEwp;
    }
  }

  private static TerrainPacketResult RestoreNative(ZPackage pkg, int position)
  {
    pkg.SetPos(position);
    return TerrainPacketResult.Native;
  }
}

[HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.RPC_ApplyOperation))]
internal static class TerrainRpcPatch
{
  private static bool Prefix(TerrainComp __instance, ZPackage pkg)
  {
    var result = TerrainProtocol.Read(pkg, out var pos, out var settings);
    if (result == TerrainPacketResult.Native)
      return true;
    if (result == TerrainPacketResult.InvalidEwp || settings == null)
    {
      Log.Error("Rejected an invalid or unsupported EWP terrain-operation packet.");
      return false;
    }
    if (!Helper.IsServer() || !__instance.m_nview.IsOwner())
    {
      Log.Error("Rejected an EWP terrain operation because the terrain compiler is not server-owned.");
      return false;
    }
    try
    {
      ApplyLegacyEwpOperation(__instance, pos, settings);
    }
    catch (Exception e)
    {
      Log.Error($"Failed to apply an EWP terrain operation: {e}");
    }
    return false;
  }

  /// <summary>
  /// EWP terrain rules predate Deep North's neighbor-aware paint routine. EWP
  /// already dispatches one operation to every affected terrain compiler, so
  /// using that routine would spread the same border paint more than once.
  /// Keep established EWP behavior while native packets stay fully native.
  /// </summary>
  internal static void ApplyLegacyEwpOperation(TerrainComp terrain, Vector3 pos, TerrainOp.Settings settings)
  {
    if (!terrain.m_initialized)
      return;

    if (settings.m_level)
      terrain.LevelTerrain(pos + Vector3.up * settings.m_levelOffset, settings.m_levelRadius, settings.m_square);
    if (settings.m_raise)
      terrain.RaiseTerrain(pos, settings.m_raiseRadius, settings.m_raiseDelta, settings.m_square, settings.m_raisePower);
    if (settings.m_smooth)
      terrain.SmoothTerrain(pos + Vector3.up * settings.m_levelOffset, settings.m_smoothRadius, settings.m_square, settings.m_smoothPower);
    if (settings.m_paintCleared)
      PaintLegacy(terrain, pos, settings);

    terrain.m_operations++;
    terrain.m_lastOpPoint = pos;
    terrain.m_lastOpRadius = settings.GetRadius();
    var paintOnly = settings.m_paintCleared && !settings.m_level && !settings.m_raise && !settings.m_smooth;
    terrain.Save(paintOnly);
    terrain.m_hmap.Poke(1, paintOnly);
    if (ClutterSystem.instance)
      ClutterSystem.instance.ResetGrass(pos, settings.GetRadius());
  }

  private static void PaintLegacy(TerrainComp terrain, Vector3 worldPos, TerrainOp.Settings settings)
  {
    // This half-vertex offset was unconditional in the pre-Deep-North routine.
    worldPos.x -= 0.5f;
    worldPos.z -= 0.5f;
    var height = worldPos.y - terrain.transform.position.y;
    terrain.m_hmap.WorldToVertexMask(worldPos, out var x, out var y);
    var radius = settings.m_paintRadius / terrain.m_hmap.m_scale;
    var extent = Mathf.CeilToInt(radius);
    var center = new Vector2(x, y);
    var pitch = terrain.m_width + 1;

    for (var row = y - extent; row <= y + extent; row++)
    {
      for (var column = x - extent; column <= x + extent; column++)
      {
        if (column < 0 || row < 0 || column >= pitch || row >= pitch)
          continue;
        if (settings.m_paintHeightCheck && terrain.m_hmap.GetHeight(column, row) > height)
          continue;

        var distance = Vector2.Distance(center, new Vector2(column, row));
        var strength = Mathf.Pow(1f - Mathf.Clamp01(distance / radius), 0.1f);
        var color = terrain.m_hmap.GetPaintMask(column, row);
        var alpha = color.a;
        color = Color.Lerp(color, PaintColor(settings.m_paintType), strength);
        color.a = alpha;
        var index = row * pitch + column;
        terrain.m_modifiedPaint[index] = true;
        terrain.m_paintMask[index] = color;
      }
    }
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
