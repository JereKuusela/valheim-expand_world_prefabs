using Data;
using UnityEngine;

namespace ExpandWorld.Prefab;

public static class Paint
{
  private static readonly int TerrainCompilerHash = ZdoHelper.Hash("_TerrainCompiler");
  public static Color GetPaint(Vector3 pos, Heightmap.Biome biome)
  {
    WorldGenerator.instance.GetBiomeHeight(biome, pos.x, pos.z, out Color paint);
    var zdo = GetCompiler(pos);
    if (zdo == null) return paint;
    var data = zdo.GetByteArray(ZDOVars.s_TCData, null);
    if (data == null) return paint;
    var zonePos = ZoneSystem.GetZonePos(ZoneSystem.GetZone(pos));
    Vector3 vector = pos - zonePos;
    var x = Mathf.FloorToInt(vector.x + 0.5f) + 32;
    var y = Mathf.FloorToInt(vector.z + 0.5f) + 32;
    var index = x + y * 65;

    var package = new ZPackage(Utils.Decompress(data));
    package.ReadInt();
    package.ReadInt();
    package.ReadVector3();
    package.ReadSingle();
    var num = package.ReadInt();
    for (var i = 0; i < num; i++)
    {
      if (package.ReadBool())
      {
        package.ReadSingle();
        package.ReadSingle();
      }
    }
    var num2 = package.ReadInt();

    for (var j = 0; j < num2; j++)
    {
      if (package.ReadBool())
      {
        var r = package.ReadSingle();
        var g = package.ReadSingle();
        var b = package.ReadSingle();
        var a = package.ReadSingle();
        if (j == index) return new(r, g, b, a);
      }
      else if (j == index) return paint;
    }
    return paint;

  }
  private static ZDO? GetCompiler(Vector3 pos)
  {
    var zone = ZoneSystem.GetZone(pos);
    var index = ZDOMan.instance.SectorToIndex(zone);
    if (index > -1)
    {
      var zdos = ZDOMan.instance.m_objectsBySector[index];
      if (zdos == null) return null;
      foreach (var zdo in zdos)
      {
        if (zdo.m_prefab == TerrainCompilerHash)
          return zdo;
      }
    }
    else if (ZDOMan.instance.m_objectsByOutsideSector.ContainsKey(zone))
    {
      var zdos = ZDOMan.instance.m_objectsByOutsideSector[zone];
      if (zdos == null) return null;
      foreach (var zdo in zdos)
      {
        if (zdo.m_prefab == TerrainCompilerHash)
          return zdo;
      }
    }
    return null;
  }
}