using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Common;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class RuleYaml
{
  [DefaultValue("")]
  public string prefab = "";
  [DefaultValue("")]
  public string excludePrefab = "";
  public string type = "";
  [DefaultValue(null)]
  public string[]? types;
  [DefaultValue(false)]
  public bool fallback = false;
  [DefaultValue(false)]
  public bool separate = false;
  [DefaultValue(null)]
  public string? weight;
  [DefaultValue(null)]
  public SpawnEntries? swap;
  [DefaultValue(null)]
  public string[]? swaps;
  [DefaultValue(null)]
  public SpawnEntries? spawn;
  [DefaultValue(null)]
  public string[]? spawns;
  [DefaultValue(null)]
  public float? spawnDelay;
  [DefaultValue(null)]
  public string? remove;
  [DefaultValue(null)]
  public string? removeDelay;
  [DefaultValue("")]
  public string drops = "";
  [DefaultValue(null)]
  public DataField? data;
  [DefaultValue(null)]
  public StringList? command;
  [DefaultValue(null)]
  public string[]? commands;
  [DefaultValue(null)]
  public RuleLogData? log;
  [DefaultValue(null)]
  public RuleLogFiles? logFile;
  [DefaultValue(null)]
  public string? day;
  [DefaultValue(null)]
  public string? night;
  [DefaultValue("")]
  public string biomes = "";
  [DefaultValue("")]
  public string bannedBiomes = "";
  [DefaultValue(null)]
  public string? minDistance;
  [DefaultValue(null)]
  public string? maxDistance;
  [DefaultValue(null)]
  public string? minAltitude;
  [DefaultValue(null)]
  public string? maxAltitude;
  [DefaultValue(null)]
  public string? minY;
  [DefaultValue(null)]
  public string? maxY;
  [DefaultValue(null)]
  public string? minX;
  [DefaultValue(null)]
  public string? maxX;
  [DefaultValue(null)]
  public string? minZ;
  [DefaultValue(null)]
  public string? maxZ;
  [DefaultValue("")]
  public string environments = "";
  [DefaultValue("")]
  public string bannedEnvironments = "";
  [DefaultValue("")]
  public string globalKeys = "";
  [DefaultValue("")]
  public string bannedGlobalKeys = "";
  [DefaultValue("")]
  public string keys = "";
  [DefaultValue("")]
  public string bannedKeys = "";
  [DefaultValue("")]
  public string events = "";
  [DefaultValue(null)]
  public float? eventDistance;
  [DefaultValue(null)]
  public PokeYaml[]? poke;
  [DefaultValue(null)]
  public string[]? pokes;
  [DefaultValue(0)]
  public int pokeLimit = 0;
  [DefaultValue("")]
  public string pokeParameter = "";
  [DefaultValue(0f)]
  public float pokeDelay = 0f;
  [DefaultValue(null)]
  public TerrainYaml[]? terrain;

  [DefaultValue(null)]
  public ObjectEntries? objects;
  [DefaultValue(null)]
  public string? objectsLimit;
  [DefaultValue(null)]
  public ObjectEntries? bannedObjects;
  [DefaultValue(null)]
  public string? bannedObjectsLimit;
  [DefaultValue(null)]
  public string? locations;
  [DefaultValue(null)]
  public float? locationDistance = null;
  [DefaultValue("")]
  public string? bannedLocations;
  [DefaultValue(null)]
  public float? bannedLocationDistance = null;
  [DefaultValue(null)]
  public string? playerEvents;
  [DefaultValue(null)]
  public string? bannedPlayerEvents;
  [DefaultValue(null)]
  public string? groups;
  [DefaultValue(null)]
  public string? bannedGroups;
  [DefaultValue(null)]
  public string[]? filters = null;
  [DefaultValue(null)]
  public string[]? bannedFilters = null;
  [DefaultValue(null)]
  public string? filterLimit = null;
  [DefaultValue(null)]
  public float? delay;

  [DefaultValue(null)]
  public bool? triggerRules = null;
  [DefaultValue(null)]
  public Dictionary<string, string>[]? objectRpc = null;
  [DefaultValue(null)]
  public Dictionary<string, string>[]? clientRpc = null;

  [DefaultValue("")]
  public string minPaint = "";
  [DefaultValue("")]
  public string maxPaint = "";
  [DefaultValue("")]
  public string paint = "";
  [DefaultValue(null)]
  public string? terrainHeight;
  [DefaultValue(null)]
  public string? minTerrainHeight;
  [DefaultValue(null)]
  public string? maxTerrainHeight;

  public bool? injectData;

  [DefaultValue(null)]
  public string? owner;
  [DefaultValue(null)]
  public string? attach;
  [DefaultValue(null)]
  public string? connect;
  [DefaultValue("")]
  public string addItems = "";
  [DefaultValue("")]
  public string removeItems = "";
  [DefaultValue(null)]
  public string? cancel;
  [DefaultValue(null)]
  public ExecEntries? exec;
  [DefaultValue(null)]
  public string? admin;
  [DefaultValue(null)]
  public string? chance;
  [DefaultValue(null)]
  public string? condition;
}


public class Rule
{
  public string Prefabs = "";
  public string ExcludedPrefabs = "";
  public ActionType Type = ActionType.Create;
  public bool Fallback = false;

  public string[] Args = [];
  public IFloatValue? Weight;
  public Spawn[]? Swaps;
  public Spawn[]? WeightedSwaps;
  public Spawn[]? Spawns;
  public Spawn[]? WeightedSpawns;

  public Spawn? GetWeightedSpawn(Functions f) => GetWeighted(f, WeightedSpawns);
  public Spawn? GetWeightedSwap(Functions r) => GetWeighted(r, WeightedSwaps);
  private Spawn? GetWeighted(Functions r, Spawn[]? candidates)
  {
    if (candidates == null || candidates.Length == 0) return null;
    var weights = candidates.Select(s => s.Weight?.Get(r) ?? 0f).ToArray();
    var total = Mathf.Max(1f, weights.Sum(s => s));
    var random = UnityEngine.Random.Range(0f, total);
    for (var i = 0; i < weights.Length; i++)
    {
      random -= weights[i];
      if (random <= 0f) return candidates[i];
    }
    return null;
  }
  public IBoolValue? Remove;
  public bool Regenerate = false;
  public IFloatValue? RemoveDelay;
  public IStringValue? Drops;
  public IStringValue? Data;
  public bool? InjectData;
  public string[] Commands = [];
  internal RuleLogSource[]? LogSources;
  public IBoolValue? Day;
  public IBoolValue? Night;
  public IFloatValue? MinDistance;
  public IFloatValue? MaxDistance;
  public IFloatValue? MinY;
  public IFloatValue? MaxY;
  public IFloatValue? MinX;
  public IFloatValue? MaxX;
  public IFloatValue? MinZ;
  public IFloatValue? MaxZ;
  public IFloatValue? MinAltitude;
  public IFloatValue? MaxAltitude;
  public Heightmap.Biome Biomes = Heightmap.Biome.None;
  public Heightmap.Biome BannedBiomes = Heightmap.Biome.None;
  internal HashSet<string>? AltBiomes;
  internal HashSet<string>? BannedAltBiomes;
  public float EventDistance = 0f;
  public HashSet<string> Events = [];
  public HashSet<string> Environments = [];
  public HashSet<string> BannedEnvironments = [];
  public List<string> GlobalKeys = [];
  public List<string> BannedGlobalKeys = [];
  public List<string> Keys = [];
  public List<string> BannedKeys = [];
  public Object[]? LegacyPokes;
  public Poke[]? Pokes;
  public Poke[]? WeightedPokes;
  public Poke? GetWeightedPoke(Functions f)
  {
    if (WeightedPokes == null || WeightedPokes.Length == 0) return null;
    var weights = WeightedPokes.Select(p => p.Weight?.Get(f) ?? 0f).ToArray();
    var total = Mathf.Max(1f, weights.Sum(s => s));
    var random = UnityEngine.Random.Range(0f, total);
    for (var i = 0; i < weights.Length; i++)
    {
      random -= weights[i];
      if (random <= 0f) return WeightedPokes[i];
    }
    return null;
  }
  public Terrain[]? Terrains;
  public int PokeLimit = 0;
  public string PokeParameter = "";
  public float PokeDelay = 0f;
  public IRangeIntValue? ObjectsLimit;
  public Object[]? Objects;
  public IRangeIntValue? BannedObjectsLimit;
  public Object[]? BannedObjects;
  public HashSet<string>? Locations;
  public HashSet<string>? BannedLocations;
  public float LocationDistance = 0f;
  public float BannedLocationDistance = 0f;
  public HashSet<string>? PlayerEvents;
  public HashSet<string>? BannedPlayerEvents;
  public HashSet<string>? Groups;
  public HashSet<string>? BannedGroups;
  public Filters? Filters;
  public bool TriggerRules;
  public ObjectRpcInfo[]? ObjectRpcs;
  public ObjectRpcInfo[]? WeightedObjectRpcs;
  public ObjectRpcInfo? GetWeightedObjectRpc(Functions f)
  {
    if (WeightedObjectRpcs == null || WeightedObjectRpcs.Length == 0) return null;
    var weights = WeightedObjectRpcs.Select(r => r.Weight?.Get(f) ?? 0f).ToArray();
    var total = Mathf.Max(1f, weights.Sum(s => s));
    var random = UnityEngine.Random.Range(0f, total);
    for (var i = 0; i < weights.Length; i++)
    {
      random -= weights[i];
      if (random <= 0f) return WeightedObjectRpcs[i];
    }
    return null;
  }
  public ClientRpcInfo[]? ClientRpcs;
  public ClientRpcInfo[]? WeightedClientRpcs;

  public ClientRpcInfo? GetWeightedClientRpc(Functions f)
  {
    if (WeightedClientRpcs == null || WeightedClientRpcs.Length == 0) return null;
    var weights = WeightedClientRpcs.Select(r => r.Weight?.Get(f) ?? 0f).ToArray();
    var total = Mathf.Max(1f, weights.Sum(s => s));
    var random = UnityEngine.Random.Range(0f, total);
    for (var i = 0; i < weights.Length; i++)
    {
      random -= weights[i];
      if (random <= 0f) return WeightedClientRpcs[i];
    }
    return null;
  }
  public Color? MinPaint;
  public Color? MaxPaint;
  public IFloatValue? MinTerrainHeight;
  public IFloatValue? MaxTerrainHeight;
  public DataEntry? AddItems;
  public DataEntry? RemoveItems;
  public ILongValue? Owner;
  public bool OwnerServer = false;
  public IZdoIdValue? Attach;
  public IZdoIdValue? Connect;
  public IBoolValue? Cancel;
  public Exec[]? Execs;
  public Exec[]? WeightedExecs;
  public Exec? GetWeightedExec(Functions f)
  {
    if (WeightedExecs == null || WeightedExecs.Length == 0) return null;
    var weights = WeightedExecs.Select(e => e.Weight?.Get(f) ?? 0f).ToArray();
    var total = Mathf.Max(1f, weights.Sum(s => s));
    var random = UnityEngine.Random.Range(0f, total);
    for (var i = 0; i < weights.Length; i++)
    {
      random -= weights[i];
      if (random <= 0f) return WeightedExecs[i];
    }
    return null;
  }
  public IBoolValue? Admin;
  public IFloatValue? Chance;
  public ConditionClause? Condition;
}

public class InfoType
{
  public readonly ActionType Type;
  public readonly string[] Parameters;
  public InfoType(string prefab, string line)
  {
    var types = Parse.Kvp(line);
    if (!System.Enum.TryParse(types.Key, true, out Type))
    {
      if (line == "")
        Log.Warning($"Missing type for prefab {prefab}.");
      else
        Log.Error($"Invalid type {types} for prefab {prefab}.");
      Type = ActionType.Create;
    }
    Parameters = types.Value != "" ? types.Value.Split(' ') : [];
  }
}
