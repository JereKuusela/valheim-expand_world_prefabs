using System.Collections.Generic;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;


public class ObjectsFiltering
{
  // Note: Can include the object itself.
  public static ZDOID[] GetNearby(int limit, Object[] objects, Vector3 pos, Quaternion rot, Functions f, ZDOID? self, bool random)
  {
    if (objects.Length == 0) return [];
    foreach (var o in objects) o.Roll(f, pos, rot);
    var maxRadius = objects.Max(o => o.MaxDistance);
    if (maxRadius > 10000)
    {
      var zdos = ZDOMan.instance.m_objectsByID.Values;
      return GetObjects(limit, zdos, objects, f, self, random);
    }
    var zdoLists = GetSectorIndices(objects);
    return GetObjects(limit, zdoLists, objects, f, self, random);
  }
  public static ZDOID[] GetNearby(int limit, Object objects, Vector3 pos, Quaternion rot, Functions f, ZDOID? self, bool random)
  {
    objects.Roll(f, pos, rot);
    var maxRadius = objects.MaxDistance;
    if (maxRadius > 10000)
    {
      var zdos = ZDOMan.instance.m_objectsByID.Values;
      return GetObjects(limit, zdos, objects, f, self, random);
    }
    var zdoLists = GetSectorIndices(objects);
    return GetObjects(limit, zdoLists, objects, f, self, random);
  }
  private static ZDOID[] GetObjects(int limit, List<List<ZDO>> zdoLists, Object objects, Functions f, ZDOID? self, bool random)
  {
    var query = zdoLists.SelectMany(z => z).Where(z => objects.IsValid(z, f, self));
    if (limit > 0)
      query = random ? query.OrderBy(_ => Random.value).Take(limit) : query.OrderBy(z => Utils.DistanceXZ(z.m_position, objects.CachedPosition)).Take(limit);
    return [.. query.Select(z => z.m_uid)];
  }
  private static ZDOID[] GetObjects(int limit, Dictionary<ZDOID, ZDO>.ValueCollection zdos, Object objects, Functions f, ZDOID? self, bool random)
  {
    var query = zdos.Where(z => objects.IsValid(z, f, self));
    if (limit > 0)
      query = random ? query.OrderBy(_ => Random.value).Take(limit) : query.OrderBy(z => Utils.DistanceXZ(z.m_position, objects.CachedPosition)).Take(limit);
    return [.. query.Select(z => z.m_uid)];
  }
  private static ZDOID[] GetObjects(int limit, List<List<ZDO>> zdoLists, Object[] objects, Functions f, ZDOID? self, bool random)
  {
    var query = zdoLists.SelectMany(z => z).Where(z => objects.Any(o => o.IsValid(z, f, self)));
    if (limit > 0)
      query = random ? query.OrderBy(_ => Random.value).Take(limit) : query.OrderBy(z => Utils.DistanceXZ(z.m_position, objects[0].CachedPosition)).Take(limit);
    return [.. query.Select(z => z.m_uid)];
  }
  private static ZDOID[] GetObjects(int limit, Dictionary<ZDOID, ZDO>.ValueCollection zdos, Object[] objects, Functions f, ZDOID? self, bool random)
  {
    var query = zdos.Where(z => objects.Any(o => o.IsValid(z, f, self)));
    if (limit > 0)
      query = random ? query.OrderBy(_ => Random.value).Take(limit) : query.OrderBy(z => Utils.DistanceXZ(z.m_position, objects[0].CachedPosition)).Take(limit);
    return [.. query.Select(z => z.m_uid)];
  }



  public static bool HasNearby(Range<int>? limit, Object[] objects, ZDO zdo, Functions f)
  {
    if (objects.Length == 0) return true;
    foreach (var o in objects) o.Roll(f, zdo.m_position, zdo.GetRotation());
    var maxRadius = objects.Max(o => o.MaxDistance);
    if (maxRadius > 10000)
    {
      var zdos = ZDOMan.instance.m_objectsByID.Values;
      if (limit == null)
        return HasAllObjects(zdos, objects, zdo.m_uid, f);
      else
        return HasLimitObjects(zdos, limit, objects, zdo.m_uid, f);
    }
    var zdoLists = GetSectorIndices(objects);
    if (limit == null)
      return HasAllObjects(zdoLists, objects, zdo.m_uid, f);
    else
      return HasLimitObjects(zdoLists, limit, objects, zdo.m_uid, f);
  }
  public static bool HasNotNearby(Range<int>? limit, Object[] objects, ZDO zdo, Functions f)
  {
    if (objects.Length == 0) return true;
    foreach (var o in objects) o.Roll(f, zdo.m_position, zdo.GetRotation());
    var zdoLists = GetSectorIndices(objects);
    if (limit == null)
      return !HasAllObjects(zdoLists, objects, zdo.m_uid, f);
    else
      return !HasLimitObjects(zdoLists, limit, objects, zdo.m_uid, f);
  }

  private static bool HasAllObjects(List<List<ZDO>> zdoLists, Object[] objects, ZDOID? self, Functions f)
  {
    return objects.All(o => zdoLists.Any(zdos => zdos.Any(z => o.IsValid(z, f, self))));
  }
  private static bool HasAllObjects(Dictionary<ZDOID, ZDO>.ValueCollection zdos, Object[] objects, ZDOID? self, Functions f)
  {
    return objects.All(o => zdos.Any(z => o.IsValid(z, f, self)));
  }
  private static bool HasLimitObjects(List<List<ZDO>> zdoLists, Range<int> limit, Object[] objects, ZDOID? self, Functions f)
  {
    var counter = 0;
    var useMax = limit.Max > 0;
    foreach (var list in zdoLists)
    {
      foreach (var z in list)
      {
        var valid = objects.FirstOrDefault(o => o.IsValid(z, f, self));
        if (valid == null) continue;
        counter += valid.Weight;
        if (useMax && limit.Max < counter) return false;
        if (limit.Min <= counter && !useMax) return true;
      }
    }
    return limit.Min <= counter && counter <= limit.Max;
  }

  private static bool HasLimitObjects(Dictionary<ZDOID, ZDO>.ValueCollection zdos, Range<int> limit, Object[] objects, ZDOID? self, Functions f)
  {
    var counter = 0;
    var useMax = limit.Max > 0;
    foreach (var z in zdos)
    {
      var valid = objects.FirstOrDefault(o => o.IsValid(z, f, self));
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
