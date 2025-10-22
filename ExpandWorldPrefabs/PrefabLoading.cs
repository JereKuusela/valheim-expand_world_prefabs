using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Data;
using HarmonyLib;
using Service;
namespace ExpandWorld.Prefab;

public class Loading
{
  private static readonly string FileName = "expand_prefabs.yaml";
  private static readonly string FilePath = Path.Combine(Yaml.BaseDirectory, FileName);
  private static readonly string Pattern = "expand_prefabs*.yaml";

  public static void FromFile()
  {
    if (Helper.IsClient()) return;
    if (!File.Exists(FilePath))
    {
      var yaml = "# Write your entries here. Good luck!";
      File.WriteAllText(FilePath, yaml);
      // Watcher triggers reload.
      return;
    }
    else
    {
      Load();
    }
  }

  private static void Load()
  {
    InfoManager.Clear();
    if (Helper.IsClient()) return;

    var data = Yaml.Read<Data>(Pattern, true);
    if (data.Count == 0)
    {
      Log.Warning($"Failed to load any prefab data.");
      return;
    }
    Log.Info($"Reloading prefab rules ({data.Count} entries).");
    var items = data.SelectMany(FromData).ToList();
    foreach (var item in items)
    {
      InfoManager.Add(item);
    }
    InfoManager.Patch();
  }

  private static Info[] FromData(Data data)
  {
    var waterLevel = ZoneSystem.instance.m_waterLevel;
    float? spawnDelay = data.delay == null && data.spawnDelay == null ? null : Math.Max(data.delay ?? 0f, data.spawnDelay ?? 0f);
    bool? triggerRules = data.triggerRules;

    var allSwaps = data.swap == null ? data.swaps == null ? null : ParseSpawns(data.swaps, spawnDelay, triggerRules) : ParseSpawns(data.swap, spawnDelay, triggerRules);
    var allSpawns = data.spawn == null ? data.spawns == null ? null : ParseSpawns(data.spawns, spawnDelay, triggerRules) : ParseSpawns(data.spawn, spawnDelay, triggerRules);

    var types = (data.types ?? [data.type]).Select(s => new InfoType(data.prefab, s)).ToArray();
    if (data.prefab == "" && types.Any(t => t.Type != ActionType.GlobalKey && t.Type != ActionType.Key && t.Type != ActionType.Event && t.Type != ActionType.Time))
      Log.Warning($"Prefab missing for type {data.type}");
    HashSet<string> events = [.. Parse.ToList(data.events)];
    var commands = data.commands ?? (data.command == null ? [] : [data.command]);
    HashSet<string> environments = [.. Parse.ToList(data.environments).Select(s => s.ToLower())];
    HashSet<string> bannedEnvironments = [.. Parse.ToList(data.bannedEnvironments).Select(s => s.ToLower())];
    HashSet<string>? locations = data.locations == null ? null : [.. Parse.ToList(data.locations)];
    HashSet<string>? bannedLocations = data.bannedLocations == null ? null : [.. Parse.ToList(data.bannedLocations)];
    HashSet<string>? playerEvents = data.playerEvents == null ? null : [.. Parse.ToList(data.playerEvents)];
    HashSet<string>? bannedPlayerEvents = data.bannedPlayerEvents == null ? null : [.. Parse.ToList(data.bannedPlayerEvents)];
    var objectsLimit = ParseObjectsLimit(data.objectsLimit);
    var objects = data.objects == null ? null : ParseObjects(data.objects);
    var bannedObjects = data.bannedObjects == null ? null : ParseObjects(data.bannedObjects);
    var bannedObjectsLimit = ParseObjectsLimit(data.bannedObjectsLimit);

    var filters = data.filters == null && data.bannedFilters == null ? null : new Filters(data.filters, data.bannedFilters, data.filterLimit);

    var legacyPokes = data.pokes == null ? null : ParseObjects(data.pokes);

    var allPokes = data.poke == null ? null : ParsePokes(data.poke);

    var terrains = data.terrain == null ? null : data.terrain.Select(s => new Terrain(s)).ToArray();
    var allObjectRpcs = ParseObjectRpcs(data);
    var allClientRpcs = ParseClientRpcs(data);
    var minPaint = data.minPaint != "" ? Parse.Color(data.minPaint, 0f) : data.paint != "" ? Parse.Color(data.paint, 0f) : null;
    var maxPaint = data.maxPaint != "" ? Parse.Color(data.maxPaint, 1f) : data.paint != "" ? Parse.Color(data.paint, 0f) : null;
    var addItems = HandleItems(data.addItems);
    var removeItems = HandleItems(data.removeItems);
    var minTerrainHeight = data.minTerrainHeight == null ? null : DataValue.Float(data.minTerrainHeight);
    var maxTerrainHeight = data.maxTerrainHeight == null ? null : DataValue.Float(data.maxTerrainHeight);
    if (data.terrainHeight != null)
    {
      // Expected format is either single value or min;max.
      var split = Parse.Kvp(data.terrainHeight, ';');
      if (split.Value == "")
      {
        minTerrainHeight = DataValue.Float(split.Key);
        maxTerrainHeight = DataValue.Float(split.Key);
      }
      else
      {
        minTerrainHeight = DataValue.Float(split.Key);
        maxTerrainHeight = DataValue.Float(split.Value);
      }
    }
    return [.. types.Select(t =>
    {
      var d = t.Type != ActionType.Destroy ? data.data : "";
      bool? remove = t.Type == ActionType.Destroy ? false : allSwaps != null ? true : data.remove == "" ? false : null;
      return new Info()
      {
        Prefabs = data.prefab,
        ExcludedPrefabs = data.excludePrefab,
        Type = t.Type,
        Fallback = data.fallback,
        Args = t.Parameters,
        Remove = remove == null ? data.remove == null ? null : DataValue.Bool(data.remove) : new SimpleBoolValue(remove.Value),
        Regenerate = d != "" || data.addItems != "" || data.removeItems != "",
        RemoveDelay = data.removeDelay == null ? null : DataValue.Float(data.removeDelay),
        Drops =  data.drops == null ? null : DataValue.String(data.drops),
        Spawns = allSpawns?.Item1,
        WeightedSpawns = allSpawns?.Item2,
        Swaps = allSwaps?.Item1,
        WeightedSwaps = allSwaps?.Item2,
        Data = DataValue.String(d),
        InjectData = data.injectData,
        Commands = commands,
        Weight = data.weight == null ? null : DataValue.Float(data.weight),
        Chance = data.chance == null ? null : DataValue.Float(data.chance),
        Day = data.day == null ? null : DataValue.Bool(data.day),
        Night = data.night == null ? null : DataValue.Bool(data.night),
        MinDistance = data.minDistance == null ? null : Parse.TryFloat(data.minDistance, out var minDistance) ? minDistance < 1f ? new SimpleFloatValue(minDistance * 10000f) : new SimpleFloatValue(minDistance) : DataValue.Float(data.minDistance),
        MaxDistance = data.maxDistance == null ? null : Parse.TryFloat(data.maxDistance, out var maxDistance) ? maxDistance < 1f ? new SimpleFloatValue(maxDistance * 10000f) : new SimpleFloatValue(maxDistance) : DataValue.Float(data.maxDistance),
        MinY = data.minY == null ? null : DataValue.Float(data.minY),
        MaxY = data.maxY == null ? null : DataValue.Float(data.maxY),
        MinX = data.minX == null ? null : DataValue.Float(data.minX),
        MaxX = data.maxX == null ? null : DataValue.Float(data.maxX),
        MinZ = data.minZ == null ? null : DataValue.Float(data.minZ),
        MaxZ = data.maxZ == null ? null : DataValue.Float(data.maxZ),
        MinAltitude = data.minAltitude == null ? null : DataValue.Float(data.minAltitude),
        MaxAltitude = data.maxAltitude == null ? null : DataValue.Float(data.maxAltitude),
        Biomes = Yaml.ToBiomes(data.biomes, true),
        BannedBiomes = Yaml.ToBiomes(data.bannedBiomes, false),
        Environments = environments,
        BannedEnvironments = bannedEnvironments,
        GlobalKeys = [.. Parse.ToList(data.globalKeys).Select(FormatKey)],
        BannedGlobalKeys = [.. Parse.ToList(data.bannedGlobalKeys).Select(FormatKey)],
        Keys = [.. Parse.ToList(data.keys).Select(FormatKey)],
        BannedKeys = [.. Parse.ToList(data.bannedKeys).Select(FormatKey)],
        Events = events,
        // Distance can be set without events for any event.
        // However if event is set, there must be a distance (the default value).
        // Zero distance means no check at all.
        EventDistance = data.eventDistance ?? (events.Count > 0 ? 100f : 0f),
        LocationDistance = data.locationDistance ?? 0f,
        BannedLocationDistance = data.bannedLocationDistance ?? data.locationDistance ?? 0f,
        Locations = locations,
        BannedLocations = bannedLocations,
        PlayerEvents = playerEvents,
        BannedPlayerEvents = bannedPlayerEvents,
        PokeLimit = data.pokeLimit,
        PokeParameter = data.pokeParameter,
        Pokes = allPokes?.Item1,
        WeightedPokes = allPokes?.Item2,
        Terrains = terrains,
        LegacyPokes = legacyPokes,
        PokeDelay = data.pokeDelay,
        ObjectsLimit = objectsLimit,
        Objects = objects,
        BannedObjects = bannedObjects,
        BannedObjectsLimit = bannedObjectsLimit,
        Filters = filters,
        TriggerRules = triggerRules ?? false,
        ObjectRpcs = allObjectRpcs?.Item1,
        WeightedObjectRpcs = allObjectRpcs?.Item2,
        ClientRpcs = allClientRpcs?.Item1,
        WeightedClientRpcs = allClientRpcs?.Item2,
        MinPaint = minPaint,
        MaxPaint = maxPaint,
        AddItems = addItems,
        RemoveItems = removeItems,
        Cancel = data.cancel == null ? null : DataValue.Bool(data.cancel),
        Owner = data.owner == null ? null : DataValue.Long(data.owner),
        MinTerrainHeight = minTerrainHeight,
        MaxTerrainHeight = maxTerrainHeight,
        Execute = data.exec == null ? null : DataValue.String(data.exec),
        Admin = data.admin == null ? null : DataValue.Bool(data.admin),
      };
    })];
  }
  private static string FormatKey(string key)
  {
    // Parameters are case sensitive so can't be lower cased.
    if (key.Contains("<")) return key;
    // Only key should be lower cased.
    var kvp = Parse.Kvp(key, ' ');
    if (kvp.Value == "") return kvp.Key.ToLowerInvariant();
    return kvp.Key.ToLowerInvariant() + " " + kvp.Value;
  }
  private static Tuple<Spawn[]?, Spawn[]?> ParseSpawns(string[] spawns, float? delay, bool? triggerRules)
  {
    var allSpawns = spawns.Select(s => new Spawn(s, delay, triggerRules)).ToArray();
    var spawn = allSpawns.Where(s => s.Weight == null).ToArray();
    if (spawn.Length == 0)
      spawn = null;
    var weightedSpawns = allSpawns.Where(s => s.Weight != null).ToArray();
    if (weightedSpawns.Length == 0)
      weightedSpawns = null;
    return Tuple.Create(spawn, weightedSpawns);
  }
  private static Tuple<Spawn[]?, Spawn[]?> ParseSpawns(SpawnData[] spawns, float? delay, bool? triggerRules)
  {
    var allSpawns = spawns.Select(s => new Spawn(s, delay, triggerRules)).ToArray();
    var spawn = allSpawns.Where(s => s.Weight == null).ToArray();
    if (spawn.Length == 0)
      spawn = null;
    var weightedSpawns = allSpawns.Where(s => s.Weight != null).ToArray();
    if (weightedSpawns.Length == 0)
      weightedSpawns = null;
    return Tuple.Create(spawn, weightedSpawns);
  }

  private static Object[] ParseObjects(string[] objects) => [.. objects.Select(s => new Object(s))];
  private static Object[] ParseObjects(ObjectData[] objects) => [.. objects.Select(s => new Object(s))];
  private static Tuple<Poke[]?, Poke[]?> ParsePokes(PokeData[] objects)
  {
    var allPokes = objects.Select(s => new Poke(s)).ToArray();
    var pokes = allPokes.Where(s => s.Weight == null).ToArray();
    if (pokes.Length == 0)
      pokes = null;
    var weightedPokes = allPokes.Where(s => s.Weight != null).ToArray();
    if (weightedPokes.Length == 0)
      weightedPokes = null;
    return Tuple.Create(pokes, weightedPokes);
  }
  private static Tuple<ObjectRpcInfo[]?, ObjectRpcInfo[]?>? ParseObjectRpcs(Data data)
  {
    if (data.objectRpc == null || data.objectRpc.Length == 0) return null;
    var allRpcs = data.objectRpc.Select(s => new ObjectRpcInfo(s)).ToArray();
    var rpcs = allRpcs.Where(s => s.Weight == null).ToArray();
    if (rpcs.Length == 0)
      rpcs = null;
    var weightedRpcs = allRpcs.Where(s => s.Weight != null).ToArray();
    if (weightedRpcs.Length == 0)
      weightedRpcs = null;
    return Tuple.Create(rpcs, weightedRpcs);
  }
  private static Tuple<ClientRpcInfo[]?, ClientRpcInfo[]?>? ParseClientRpcs(Data data)
  {
    if (data.clientRpc == null || data.clientRpc.Length == 0) return null;
    var allRpcs = data.clientRpc.Select(s => new ClientRpcInfo(s)).ToArray();
    var rpcs = allRpcs.Where(s => s.Weight == null).ToArray();
    if (rpcs.Length == 0)
      rpcs = null;
    var weightedRpcs = allRpcs.Where(s => s.Weight != null).ToArray();
    if (weightedRpcs.Length == 0)
      weightedRpcs = null;
    return Tuple.Create(rpcs, weightedRpcs);
  }

  private static Range<int>? ParseObjectsLimit(string? str) => str == null ?
    null : int.TryParse(str, out var limit) ?
      new Range<int>(limit, 0) : Parse.IntRange(str);

  public static void SetupWatcher()
  {
    if (!Directory.Exists(Yaml.BaseDirectory))
      Directory.CreateDirectory(Yaml.BaseDirectory);
    Yaml.SetupWatcher(Pattern, FromFile, true);
  }
  private static DataEntry? HandleItems(string items)
  {
    var split = Parse.Kvp(items);
    if (split.Value == "") return DataHelper.Get(items);
    DataEntry data = new()
    {
      Items = []
    };
    ItemData itemData = new()
    {
      prefab = split.Key,
      stack = split.Value
    };
    ItemValue item = new(itemData);
    data.Items.Add(item);
    return data;
  }
}

[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start)), HarmonyPriority(Priority.VeryLow)]
public class InitializeContent
{
  static void Postfix()
  {
    EWP.Harmony.UnpatchSelf();
    if (Helper.IsServer())
    {
      DataLoading.LoadEntries();
      Loading.FromFile();
    }

  }
}