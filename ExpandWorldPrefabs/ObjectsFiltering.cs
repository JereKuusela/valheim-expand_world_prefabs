using System.Collections.Generic;
using System.Linq;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;


public class ObjectsFiltering
{
  // Note: Can include the object itself.
  public static ZDO[] GetNearby(int limit, Object[] objects, ZDO zdo, Dictionary<string, string> parameters)
  {
    if (objects.Length == 0) return [];
    var maxRadius = objects.Max(o => o.MaxDistance);
    var indices = GetSectorIndices(zdo.m_position, maxRadius);
    return GetObjects(limit, indices, objects, zdo, parameters);
  }
  private static ZDO[] GetObjects(int limit, HashSet<int> indices, Object[] objects, ZDO zdo, Dictionary<string, string> parameters)
  {
    var pos = zdo.m_position;
    var zm = ZDOMan.instance;
    var query = indices.SelectMany(z => zm.m_objectsBySector[z]).Where(z => objects.Any(o => o.IsValid(z, pos, parameters)));
    if (limit > 0)
      query = query.OrderBy(z => Utils.DistanceXZ(z.m_position, pos)).Take(limit);
    return query.ToArray();
  }

  public static bool HasNearby(Range<int>? limit, Object[] objects, ZDO zdo, Dictionary<string, string> parameters)
  {
    if (objects.Length == 0) return true;
    var maxRadius = objects.Max(o => o.MaxDistance);
    var indices = GetSectorIndices(zdo.m_position, maxRadius);
    if (limit == null)
      return HasAllObjects(indices, objects, zdo, parameters);
    else
      return HasLimitObjects(indices, limit, objects, zdo, parameters);
  }
  public static bool HasNotNearby(Range<int>? limit, Object[] objects, ZDO zdo, Dictionary<string, string> parameters)
  {
    if (objects.Length == 0) return true;
    var maxRadius = objects.Max(o => o.MaxDistance);
    var indices = GetSectorIndices(zdo.m_position, maxRadius);
    if (limit == null)
      return !HasAllObjects(indices, objects, zdo, parameters);
    else
      return !HasLimitObjects(indices, limit, objects, zdo, parameters);
  }

  private static bool HasAllObjects(HashSet<int> indices, Object[] objects, ZDO zdo, Dictionary<string, string> parameters)
  {
    var pos = zdo.m_position;
    var zm = ZDOMan.instance;
    return objects.All(o => indices.Any(z => zm.m_objectsBySector[z].Any(z => o.IsValid(z, pos, parameters) && z != zdo)));
  }
  private static bool HasLimitObjects(HashSet<int> indices, Range<int> limit, Object[] objects, ZDO zdo, Dictionary<string, string> parameters)
  {
    var pos = zdo.m_position;
    var counter = 0;
    var useMax = limit.Max > 0;
    foreach (var i in indices)
    {
      foreach (var z in ZDOMan.instance.m_objectsBySector[i])
      {
        var valid = objects.FirstOrDefault(o => o.IsValid(z, pos, parameters) && z != zdo);
        if (valid == null) continue;
        counter += valid.Weight;
        if (useMax && limit.Max < counter) return false;
        if (limit.Min <= counter && !useMax) return true;
      }
    }
    return limit.Min <= counter && counter <= limit.Max;
  }
  private static HashSet<int> GetSectorIndices(Vector3 pos, float radius)
  {
    HashSet<int> indices = [];
    var corner1 = ZoneSystem.instance.GetZone(pos + new Vector3(-radius, 0, -radius));
    var corner2 = ZoneSystem.instance.GetZone(pos + new Vector3(radius, 0, -radius));
    var corner3 = ZoneSystem.instance.GetZone(pos + new Vector3(-radius, 0, radius));
    var zm = ZDOMan.instance;
    for (var x = corner1.x; x <= corner2.x; x++)
    {
      for (var y = corner1.y; y <= corner3.y; y++)
      {
        var index = zm.SectorToIndex(new Vector2i(x, y));
        if (index < 0 || index >= zm.m_objectsBySector.Length) continue;
        indices.Add(index);
      }
    }
    return indices;
  }
}
