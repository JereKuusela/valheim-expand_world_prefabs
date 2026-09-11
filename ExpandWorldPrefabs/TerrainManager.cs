using System;
using System.Linq;
using UnityEngine;

namespace ExpandWorld.Prefab;

internal static class TerrainManager
{
  internal static void Modify(Vector3 pos, float radius, TerrainOp.Settings settings, float resetRadius)
  {
    // Terrain may have to be modified in multiple zones.
    var corner1 = pos + new Vector3(radius, 0, radius);
    var corner2 = pos + new Vector3(-radius, 0, -radius);
    var corner3 = pos + new Vector3(-radius, 0, radius);
    var corner4 = pos + new Vector3(radius, 0, -radius);
    var zone1 = ZoneSystem.GetZone(corner1);
    var zone2 = ZoneSystem.GetZone(corner2);
    var zone3 = ZoneSystem.GetZone(corner3);
    var zone4 = ZoneSystem.GetZone(corner4);
    var startI = Mathf.Min(zone1.x, zone2.x, zone3.x, zone4.x);
    var endI = Mathf.Max(zone1.x, zone2.x, zone3.x, zone4.x);
    var startJ = Mathf.Min(zone1.y, zone2.y, zone3.y, zone4.y);
    var endJ = Mathf.Max(zone1.y, zone2.y, zone3.y, zone4.y);

    for (var i = startI; i <= endI; i++)
    {
      for (var j = startJ; j <= endJ; j++)
      {
        var zone = new Vector2s(i, j);
        if (!ZoneSystem.instance.IsZoneGenerated(zone)) continue;
        ModifyZone(pos, zone, settings, resetRadius);
      }
    }
  }

  internal static bool GenerateCompilers(Vector3 pos, float radius)
  {
    var corner1 = pos + new Vector3(radius, 0, radius);
    var corner2 = pos + new Vector3(-radius, 0, -radius);
    var corner3 = pos + new Vector3(-radius, 0, radius);
    var corner4 = pos + new Vector3(radius, 0, -radius);
    var zone1 = ZoneSystem.GetZone(corner1);
    var zone2 = ZoneSystem.GetZone(corner2);
    var zone3 = ZoneSystem.GetZone(corner3);
    var zone4 = ZoneSystem.GetZone(corner4);
    var startI = Mathf.Min(zone1.x, zone2.x, zone3.x, zone4.x);
    var endI = Mathf.Max(zone1.x, zone2.x, zone3.x, zone4.x);
    var startJ = Mathf.Min(zone1.y, zone2.y, zone3.y, zone4.y);
    var endJ = Mathf.Max(zone1.y, zone2.y, zone3.y, zone4.y);

    var created = false;
    for (var i = startI; i <= endI; i++)
    {
      for (var j = startJ; j <= endJ; j++)
      {
        var zone = new Vector2s(i, j);
        if (!ZoneSystem.instance.IsZoneGenerated(zone)) continue;
        created |= GenerateCompiler(zone);
      }
    }
    return created;
  }

  private static void ModifyZone(Vector3 pos, Vector2s zone, TerrainOp.Settings settings, float resetRadius)
  {
    var compiler = FindCompiler(zone);
    if (compiler == null) return;
    if (resetRadius > 0f)
    {
      ResetInZdo(pos, resetRadius, zone, compiler);
      return;
    }

    var terrain = ZNetScene.instance.FindInstance(compiler.m_uid)?.GetComponent<TerrainComp>();
    if (terrain != null)
      TerrainOperations.ApplyLegacyEwpOperation(terrain, pos, settings);
  }

  private static void ResetInZdo(Vector3 pos, float radius, Vector2s zone, ZDO zdo)
  {
    var byteArray = zdo.GetByteArray(ZDOVars.s_TCData);
    if (byteArray == null) return;
    var center = ZoneSystem.GetZonePos(zone);
    var change = false;
    var from = new ZPackage(Utils.Decompress(byteArray));
    var to = new ZPackage();
    to.Write(from.ReadInt());
    to.Write(from.ReadInt() + 1);
    from.ReadVector3();
    to.Write(center);
    from.ReadSingle();
    to.Write(radius);
    var size = from.ReadInt();
    to.Write(size);
    var width = (int)Math.Sqrt(size);
    for (int index = 0; index < size; index++)
    {
      var wasModified = from.ReadBool();
      var modified = wasModified;
      var j = index / width;
      var i = index % width;
      var worldPos = VertexToWorld(center, j, i);
      if (Utils.DistanceXZ(worldPos, pos) < radius)
        modified = false;
      to.Write(modified);
      if (modified)
      {
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
      }
      if (wasModified && !modified)
      {
        change = true;
        from.ReadSingle();
        from.ReadSingle();
      }
    }
    size = from.ReadInt();
    to.Write(size);
    for (int index = 0; index < size; index++)
    {
      var wasModified = from.ReadBool();
      var modified = wasModified;
      var j = index / width;
      var i = index % width;
      var worldPos = VertexToWorld(center, j, i);
      if (Utils.DistanceXZ(worldPos, pos) < radius)
        modified = false;
      to.Write(modified);
      if (modified)
      {
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
      }
      if (wasModified && !modified)
      {
        change = true;
        from.ReadSingle();
        from.ReadSingle();
        from.ReadSingle();
        from.ReadSingle();
      }
    }
    if (!change) return;
    zdo.DataRevision += 100;
    zdo.Set(ZDOVars.s_TCData, Utils.Compress(to.GetArray()));
  }

  private static Vector3 VertexToWorld(Vector3 pos, int j, int i)
  {
    pos.x += i - 32.5f;
    pos.z += j - 32.5f;
    return pos;
  }

  private static readonly int TerrainCompilerHash = "_TerrainCompiler".GetStableHashCode();

  private static bool GenerateCompiler(Vector2s zone)
  {
    if (FindCompiler(zone) != null)
      return false;

    var zdo = ZDOMan.instance.CreateNewZDO(ZoneSystem.GetZonePos(zone), TerrainCompilerHash);
    var view = ZNetScene.instance.GetPrefab(TerrainCompilerHash).GetComponent<ZNetView>();
    zdo.m_prefab = TerrainCompilerHash;
    zdo.Persistent = view.m_persistent;
    zdo.Type = view.m_type;
    zdo.Distant = view.m_distant;
    return true;
  }

  private static ZDO? FindCompiler(Vector2s zone)
  {
    var zdos = Helper.GetZDOsInSector(zone);
    return zdos?.FirstOrDefault(z => z.m_prefab == TerrainCompilerHash);
  }
}

internal static class TerrainOperations
{
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
