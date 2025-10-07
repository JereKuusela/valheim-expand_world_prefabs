using System.Collections.Generic;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;


public class ObjectsFiltering
{
  // Note: Can include the object itself.
  public static ZDOID[] GetNearby(int limit, Object[] objects, Vector3 pos, Quaternion rot, Parameters parameters, ZDOID? exclude)
  {
    if (objects.Length == 0) return [];
    foreach (var o in objects) o.Roll(parameters, pos, rot);
    var maxRadius = objects.Max(o => o.MaxDistance);
    if (maxRadius > 10000)
    {
      var zdos = ZDOMan.instance.m_objectsByID.Values;
      return GetObjects(limit, zdos, objects, pos, parameters, exclude);
    }
    var zdoLists = GetSectorIndices(objects);
    return GetObjects(limit, zdoLists, objects, pos, parameters, exclude);
  }
  public static ZDOID[] GetNearby(int limit, Object objects, Vector3 pos, Quaternion rot, Parameters parameters, ZDOID? exclude)
  {
    objects.Roll(parameters, pos, rot);
    var maxRadius = objects.MaxDistance;
    if (maxRadius > 10000)
    {
      var zdos = ZDOMan.instance.m_objectsByID.Values;
      return GetObjects(limit, zdos, objects, pos, parameters, exclude);
    }
    var zdoLists = GetSectorIndices(objects);
    return GetObjects(limit, zdoLists, objects, pos, parameters, exclude);
  }
  private static ZDOID[] GetObjects(int limit, List<List<ZDO>> zdoLists, Object objects, Vector3 pos, Parameters parameters, ZDOID? exclude)
  {
    var zm = ZDOMan.instance;
    var query = zdoLists.SelectMany(z => z).Where(z => z.m_uid != exclude && objects.IsValid(z, parameters));
    if (limit > 0)
      query = query.OrderBy(z => Utils.DistanceXZ(z.m_position, pos)).Take(limit);
    return [.. query.Select(z => z.m_uid)];
  }
  private static ZDOID[] GetObjects(int limit, Dictionary<ZDOID, ZDO>.ValueCollection zdos, Object objects, Vector3 pos, Parameters parameters, ZDOID? exclude)
  {
    var zm = ZDOMan.instance;
    var query = zdos.Where(z => z.m_uid != exclude && objects.IsValid(z, parameters));
    if (limit > 0)
      query = query.OrderBy(z => Utils.DistanceXZ(z.m_position, pos)).Take(limit);
    return [.. query.Select(z => z.m_uid)];
  }
  private static ZDOID[] GetObjects(int limit, List<List<ZDO>> zdoLists, Object[] objects, Vector3 pos, Parameters parameters, ZDOID? exclude)
  {
    var zm = ZDOMan.instance;
    var query = zdoLists.SelectMany(z => z).Where(z => z.m_uid != exclude && objects.Any(o => o.IsValid(z, parameters)));
    if (limit > 0)
      query = query.OrderBy(z => Utils.DistanceXZ(z.m_position, pos)).Take(limit);
    return [.. query.Select(z => z.m_uid)];
  }
  private static ZDOID[] GetObjects(int limit, Dictionary<ZDOID, ZDO>.ValueCollection zdos, Object[] objects, Vector3 pos, Parameters parameters, ZDOID? exclude)
  {
    var zm = ZDOMan.instance;
    var query = zdos.Where(z => z.m_uid != exclude && objects.Any(o => o.IsValid(z, parameters)));
    if (limit > 0)
      query = query.OrderBy(z => Utils.DistanceXZ(z.m_position, pos)).Take(limit);
    return [.. query.Select(z => z.m_uid)];
  }



  public static bool HasNearby(Range<int>? limit, Object[] objects, ZDO zdo, Parameters parameters)
  {
    if (objects.Length == 0) return true;
    foreach (var o in objects) o.Roll(parameters, zdo.m_position, zdo.GetRotation());
    var maxRadius = objects.Max(o => o.MaxDistance);
    if (maxRadius > 10000)
    {
      var zdos = ZDOMan.instance.m_objectsByID.Values;
      if (limit == null)
        return HasAllObjects(zdos, objects, zdo, parameters);
      else
        return HasLimitObjects(zdos, limit, objects, zdo, parameters);
    }
    var zdoLists = GetSectorIndices(objects);
    if (limit == null)
      return HasAllObjects(zdoLists, objects, zdo, parameters);
    else
      return HasLimitObjects(zdoLists, limit, objects, zdo, parameters);
  }
  public static bool HasNotNearby(Range<int>? limit, Object[] objects, ZDO zdo, Parameters parameters)
  {
    if (objects.Length == 0) return true;
    foreach (var o in objects) o.Roll(parameters, zdo.m_position, zdo.GetRotation());
    var zdoLists = GetSectorIndices(objects);
    if (limit == null)
      return !HasAllObjects(zdoLists, objects, zdo, parameters);
    else
      return !HasLimitObjects(zdoLists, limit, objects, zdo, parameters);
  }

  private static bool HasAllObjects(List<List<ZDO>> zdoLists, Object[] objects, ZDO zdo, Parameters parameters)
  {
    return objects.All(o => zdoLists.Any(zdos => zdos.Any(z => o.IsValid(z, parameters) && z != zdo)));
  }
  private static bool HasAllObjects(Dictionary<ZDOID, ZDO>.ValueCollection zdos, Object[] objects, ZDO zdo, Parameters parameters)
  {
    return objects.All(o => zdos.Any(z => o.IsValid(z, parameters) && z != zdo));
  }
  private static bool HasLimitObjects(List<List<ZDO>> zdoLists, Range<int> limit, Object[] objects, ZDO zdo, Parameters parameters)
  {
    var counter = 0;
    var useMax = limit.Max > 0;
    foreach (var list in zdoLists)
    {
      foreach (var z in list)
      {
        var valid = objects.FirstOrDefault(o => o.IsValid(z, parameters) && z != zdo);
        if (valid == null) continue;
        counter += valid.Weight;
        if (useMax && limit.Max < counter) return false;
        if (limit.Min <= counter && !useMax) return true;
      }
    }
    return limit.Min <= counter && counter <= limit.Max;
  }

  private static bool HasLimitObjects(Dictionary<ZDOID, ZDO>.ValueCollection zdos, Range<int> limit, Object[] objects, ZDO zdo, Parameters parameters)
  {
    var counter = 0;
    var useMax = limit.Max > 0;
    foreach (var z in zdos)
    {
      var valid = objects.FirstOrDefault(o => o.IsValid(z, parameters) && z != zdo);
      if (valid == null) continue;
      counter += valid.Weight;
      if (useMax && limit.Max < counter) return false;
      if (limit.Min <= counter && !useMax) return true;
    }
    return limit.Min <= counter && counter <= limit.Max;
  }
  private static List<List<ZDO>> GetSectorIndices(Object[] objects)
  {
    List<List<ZDO>> zdoLists = [];
    HashSet<Vector2i> handled = [];
    foreach (var o in objects)
      GetSectorIndices(o, zdoLists, handled);

    return zdoLists;
  }

  private static List<List<ZDO>> GetSectorIndices(Object objects)
  {
    List<List<ZDO>> zdoLists = [];
    HashSet<Vector2i> handled = [];
    GetSectorIndices(objects, zdoLists, handled);
    return zdoLists;
  }

  private static void GetSectorIndices(Object o, List<List<ZDO>> zdoLists, HashSet<Vector2i> handled)
  {
    float radius = o.MaxDistance;
    var corner1 = ZoneSystem.GetZone(o.CachedPosition + new Vector3(-radius, 0, -radius));
    var corner2 = ZoneSystem.GetZone(o.CachedPosition + new Vector3(radius, 0, radius));
    var zm = ZDOMan.instance;
    for (var x = corner1.x; x <= corner2.x; x++)
    {
      for (var y = corner1.y; y <= corner2.y; y++)
      {
        var zone = new Vector2i(x, y);
        if (handled.Contains(zone)) continue;
        handled.Add(zone);
        var index = zm.SectorToIndex(zone);
        if (index < 0 || index >= zm.m_objectsBySector.Length)
        {
          if (zm.m_objectsByOutsideSector.TryGetValue(zone, out var list) && list != null)
            zdoLists.Add(list);
          continue;
        }
        if (zm.m_objectsBySector[index] != null)
          zdoLists.Add(zm.m_objectsBySector[index]);
      }
    }
  }
}
