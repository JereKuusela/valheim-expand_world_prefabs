using System.Collections.Generic;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class InfoSelector
{
  public static Info? Select(ActionType type, ZDO zdo, string[] args, Parameters parameters)
  {
    var infos = InfoManager.Select(type);
    return SelectDefault(infos, zdo, args, parameters) ?? SelectFallback(infos, zdo, args, parameters);
  }
  public static Info[]? SelectSeparate(ActionType type, ZDO zdo, string[] args, Parameters parameters)
  {
    var infos = InfoManager.Select(type);
    return SelectSeparate(infos, zdo, args, parameters);
  }
  private static Info? SelectDefault(PrefabInfo infos, ZDO zdo, string[] args, Parameters parameters)
  {
    var prefab = zdo.m_prefab;
    if (!infos.TryGetValue(prefab, out var data)) return null;
    return SelectInfo(data, zdo, args, parameters);
  }
  private static Info? SelectFallback(PrefabInfo infos, ZDO zdo, string[] args, Parameters parameters)
  {
    var prefab = zdo.m_prefab;
    if (!infos.TryGetFallbackValue(prefab, out var data)) return null;
    return SelectInfo(data, zdo, args, parameters);
  }
  private static Info[]? SelectSeparate(PrefabInfo infos, ZDO zdo, string[] args, Parameters parameters)
  {
    var prefab = zdo.m_prefab;
    if (!infos.TryGetSeparateValue(prefab, out var data)) return null;
    return SelectInfos(data, zdo, args, parameters);
  }
  private static Info? SelectInfo(List<Info> data, ZDO zdo, string[] args, Parameters parameters)
  {
    var infos = SelectInfos(data, zdo, args, parameters);
    if (infos == null || infos.Length == 0) return null;
    return Randomize(infos, parameters);
  }

  private static Info[]? SelectInfos(List<Info> data, ZDO zdo, string[] args, Parameters parameters)
  {
    if (data.Count == 0) return null;
    var pos = zdo.m_position;
    var biome = WorldGenerator.instance.GetBiome(pos);
    var distance = Utils.LengthXZ(pos);
    var day = EnvMan.IsDay();
    var waterY = pos.y - ZoneSystem.instance.m_waterLevel;
    var linq = data
      .Where(d => CheckArgs(d, args))
      .Where(d => (d.Biomes & biome) == biome)
      .Where(d => (d.BannedBiomes & biome) == 0)
      .Where(d => d.Day?.GetBool(parameters) != false || !day)
      .Where(d => d.Night?.GetBool(parameters) != false || day)
      .Where(d => d.MinDistance == null || !d.MinDistance.TryGet(parameters, out var v) || v < distance)
      .Where(d => d.MaxDistance == null || !d.MaxDistance.TryGet(parameters, out var v) || v >= distance)
      .Where(d => d.MinY == null || !d.MinY.TryGet(parameters, out var v) || v < pos.y)
      .Where(d => d.MaxY == null || !d.MaxY.TryGet(parameters, out var v) || v >= pos.y)
      .Where(d => d.MinX == null || !d.MinX.TryGet(parameters, out var v) || v < pos.x)
      .Where(d => d.MaxX == null || !d.MaxX.TryGet(parameters, out var v) || v >= pos.x)
      .Where(d => d.MinZ == null || !d.MinZ.TryGet(parameters, out var v) || v < pos.z)
      .Where(d => d.MaxZ == null || !d.MaxZ.TryGet(parameters, out var v) || v >= pos.z)
      .Where(d => d.MinAltitude == null || !d.MinAltitude.TryGet(parameters, out var v) || v < waterY)
      .Where(d => d.MaxAltitude == null || !d.MaxAltitude.TryGet(parameters, out var v) || v >= waterY)
      .Where(d => Helper.HasEveryGlobalKey(d.GlobalKeys, parameters))
      .Where(d => !Helper.HasAnyGlobalKey(d.BannedGlobalKeys, parameters))
      .Where(d => DataStorage.HasEveryKey(d.Keys, parameters))
      .Where(d => !DataStorage.HasAnyKey(d.BannedKeys, parameters));
    // Minor optimization to resolve simpler checks first (not measured).
    linq = [.. linq];
    var checkEnvironments = linq.Any(d => d.Environments.Count > 0) || linq.Any(d => d.BannedEnvironments.Count > 0);
    var checkEvents = linq.Any(d => d.Events.Count > 0);
    var checkObjects = linq.Any(d => d.Objects != null);
    var checkBannedObjects = linq.Any(d => d.BannedObjects != null);
    var checkLocations = linq.Any(d => d.Locations != null || d.BannedLocations != null);
    var checkPlayerEvents = linq.Any(d => d.PlayerEvents != null || d.BannedPlayerEvents != null);
    var checkFilters = linq.Any(d => d.Filters != null);
    var checkPaint = linq.Any(d => d.MinPaint != null || d.MaxPaint != null);
    var checkTerrainHeight = linq.Any(d => d.MinTerrainHeight != null || d.MaxTerrainHeight != null);
    var checkAdmin = linq.Any(d => d.Admin != null);
    if (checkTerrainHeight)
    {
      var height = WorldGenerator.instance.GetHeight(pos.x, pos.z);
      linq = [.. linq.Where(d =>
        (d.MinTerrainHeight == null && d.MaxTerrainHeight == null)
        || Helper.ApproxBetween(height, d.MinTerrainHeight?.Get(parameters) ?? -1000000, d.MaxTerrainHeight?.Get(parameters) ?? 1000000)
      )];
    }
    if (checkEnvironments)
    {
      var environment = GetEnvironment(biome);
      linq = [.. linq
        .Where(d => d.Environments.Count == 0 || d.Environments.Contains(environment))
        .Where(d => !d.BannedEnvironments.Contains(environment))];
    }
    if (checkEvents)
    {
      var ev = EWP.GetCurrentEvent(pos);
      // Three cases:
      // 1. Nothing set, always true.
      // 2. Only event distance set, any event is fine.
      // 3. Event name set, only that event is fine.
      // Event distance is zero only if nothing is set.
      linq = [.. linq.Where(d => d.EventDistance == 0f || (ev != null && (d.Events.Contains(ev.m_name) || d.Events.Count == 0) && d.EventDistance >= Utils.DistanceXZ(pos, ev.m_pos)))];
    }
    if (checkObjects)
    {
      linq = [.. linq.Where(d => d.Objects == null || ObjectsFiltering.HasNearby(d.ObjectsLimit, d.Objects, zdo, parameters))];
    }
    if (checkBannedObjects)
    {
      linq = [.. linq.Where(d => d.BannedObjects == null || ObjectsFiltering.HasNotNearby(d.BannedObjectsLimit, d.BannedObjects, zdo, parameters))];
    }
    if (checkAdmin)
    {
      var peer = ZNet.instance.GetPeer(zdo.GetOwner());
      var admin = peer != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
      linq = [.. linq.Where(d => d.Admin == null || d.Admin.GetBool(parameters) == admin)];
    }
    if (checkLocations)
    {
      var zone = ZoneSystem.GetZone(pos);
      linq = [.. linq.Where(d => CheckLocations(d, pos, zone))];
    }
    if (checkPlayerEvents)
    {
      var eventData = ObjectParameters.GetPlayerData(zdo, "possibleEvents");
      var events = eventData.Split(',');
      linq = [.. linq.Where(d =>
      {
        if (d.BannedPlayerEvents != null && events.Any(ev => d.BannedPlayerEvents.Contains(ev))) return false;
        if (d.PlayerEvents == null) return true;
        return events.Any(ev => d.PlayerEvents.Contains(ev));
      })];
    }
    if (checkFilters)
    {
      linq = [.. linq.Where(d => d.Filters == null || d.Filters.Match(parameters, zdo))];
    }
    if (checkPaint)
    {
      var paint = Paint.GetPaint(pos, biome);
      linq = [.. linq.Where(d =>
        (d.MinPaint == null || (d.MinPaint.Value.b <= paint.b && d.MinPaint.Value.g <= paint.g && d.MinPaint.Value.r <= paint.r && d.MinPaint.Value.a <= paint.a)) &&
        (d.MaxPaint == null || (d.MaxPaint.Value.b >= paint.b && d.MaxPaint.Value.g >= paint.g && d.MaxPaint.Value.r >= paint.r && d.MaxPaint.Value.a >= paint.a)))];
    }
    return [.. linq];
  }
  private static bool CheckLocations(Info d, Vector3 pos, Vector2i zone) => CheckBannedLocations(d, pos, zone) && CheckRequiredLocations(d, pos, zone);
  private static bool CheckBannedLocations(Info d, Vector3 pos, Vector2i zone)
  {
    if (d.BannedLocations == null) return true;
    // +1 because the location can be at zone edge, so any distance can be on the next zone.
    int di = (int)(d.BannedLocationDistance / 64f) + 1;
    int dj = (int)(d.BannedLocationDistance / 64f) + 1;
    int minI = zone.x - di;
    int maxI = zone.x + di;
    int minJ = zone.y - dj;
    int maxJ = zone.y + dj;
    for (int i = minI; i <= maxI; i++)
    {
      for (int j = minJ; j <= maxJ; j++)
      {
        var key = new Vector2i(i, j);
        if (!ZoneSystem.instance.m_locationInstances.TryGetValue(key, out var loc)) continue;
        if (!d.BannedLocations.Contains(loc.m_location.m_prefabName)) continue;
        var dist = d.LocationDistance == 0 ? loc.m_location.m_exteriorRadius : d.LocationDistance;
        if (Utils.DistanceXZ(loc.m_position, pos) <= dist) return false;
      }
    }
    return true;
  }
  private static bool CheckRequiredLocations(Info d, Vector3 pos, Vector2i zone)
  {
    if (d.Locations == null) return true;
    // +1 because the location can be at zone edge, so any distance can be on the next zone.
    int di = (int)(d.LocationDistance / 64f) + 1;
    int dj = (int)(d.LocationDistance / 64f) + 1;
    int minI = zone.x - di;
    int maxI = zone.x + di;
    int minJ = zone.y - dj;
    int maxJ = zone.y + dj;
    for (int i = minI; i <= maxI; i++)
    {
      for (int j = minJ; j <= maxJ; j++)
      {
        var key = new Vector2i(i, j);
        if (!ZoneSystem.instance.m_locationInstances.TryGetValue(key, out var loc)) continue;
        if (!d.Locations.Contains(loc.m_location.m_prefabName)) continue;
        var dist = d.LocationDistance == 0 ? loc.m_location.m_exteriorRadius : d.LocationDistance;
        if (Utils.DistanceXZ(loc.m_position, pos) <= dist) return true;
      }
    }
    return false;
  }
  private static Info? Randomize(Info[] valid, Parameters parameters)
  {
    if (valid.Length == 0) return null;
    var weights = valid.Select(d => d.Weight?.Get(parameters) ?? 1f).ToArray();
    if (valid.Length == 1 && weights[0] >= 1f) return valid[0];
    var totalWeight = Mathf.Max(1f, weights.Sum());
    var random = Random.Range(0f, totalWeight);
    for (int i = 0; i < valid.Length; i++)
    {
      random -= weights[i];
      if (random <= 0f) return valid[i];
    }
    return null;
  }
  private static bool CheckArgs(Info info, string[] args)
  {
    if (info.Args.Length == 0) return true;
    if (info.Args.Length > args.Length) return false;
    for (int i = 0; i < info.Args.Length; i++)
      if (!Helper.CheckWild(info.Args[i], args[i])) return false;
    return true;

  }
  private static string GetEnvironment(Heightmap.Biome biome)
  {
    var em = EnvMan.instance;
    var availableEnvironments = em.GetAvailableEnvironments(biome);
    if (availableEnvironments == null || availableEnvironments.Count == 0) return "";
    Random.State state = Random.state;
    var num = (long)ZNet.instance.GetTimeSeconds() / em.m_environmentDuration;
    Random.InitState((int)num);
    var env = em.SelectWeightedEnvironment(availableEnvironments);
    Random.state = state;
    return env.m_name.ToLower();
  }
  public static Info? SelectGlobal(ActionType type, string[] args, Parameters parameters, Vector3 pos, bool remove)
  {
    var infos = InfoManager.SelectGlobal(type);
    return SelectGlobalInfo(infos.Info, args, parameters, pos, remove) ?? SelectGlobalInfo(infos.Fallback, args, parameters, pos, remove);
  }
  public static Info[]? SelectGlobalSeparate(ActionType type, string[] args, Parameters parameters, Vector3 pos, bool remove)
  {
    var infos = InfoManager.SelectGlobal(type);
    return SelectGlobalInfos(infos.Separate, args, parameters, pos, remove);
  }

  private static Info? SelectGlobalInfo(List<Info> data, string[] args, Parameters parameters, Vector3 pos, bool remove)
  {
    var infos = SelectGlobalInfos(data, args, parameters, pos, remove);
    if (infos == null || infos.Length == 0) return null;
    return Randomize(infos, parameters);
  }

  private static Info[]? SelectGlobalInfos(List<Info> data, string[] args, Parameters parameters, Vector3 pos, bool remove)
  {
    if (data.Count == 0) return null;
    var day = EnvMan.IsDay();
    var distance = Utils.LengthXZ(pos);
    var waterY = pos.y - ZoneSystem.instance.m_waterLevel;
    var linq = data
      .Where(d => CheckArgs(d, args))
      .Where(d => remove == (d.Remove?.GetBool(parameters) == true))
      .Where(d => d.Day?.GetBool(parameters) != false || !day)
      .Where(d => d.Night?.GetBool(parameters) != false || day)
      .Where(d => d.MinDistance == null || distance >= d.MinDistance.Get(parameters))
      .Where(d => d.MaxDistance == null || distance < d.MaxDistance.Get(parameters))
      .Where(d => d.MinY == null || !d.MinY.TryGet(parameters, out var v) || v < pos.y)
      .Where(d => d.MaxY == null || !d.MaxY.TryGet(parameters, out var v) || v >= pos.y)
      .Where(d => d.MinAltitude == null || !d.MinAltitude.TryGet(parameters, out var v) || v < waterY)
      .Where(d => d.MaxAltitude == null || !d.MaxAltitude.TryGet(parameters, out var v) || v >= waterY)
      .Where(d => Helper.HasEveryGlobalKey(d.GlobalKeys, parameters))
      .Where(d => !Helper.HasAnyGlobalKey(d.BannedGlobalKeys, parameters))
      .Where(d => DataStorage.HasEveryKey(d.Keys, parameters))
      .Where(d => !DataStorage.HasAnyKey(d.BannedKeys, parameters));

    return [.. linq];
  }
}