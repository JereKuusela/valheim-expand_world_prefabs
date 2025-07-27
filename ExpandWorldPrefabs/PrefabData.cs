using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class Data
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
  [DefaultValue(null)]
  public string? weight;
  [DefaultValue(null)]
  public SpawnData[]? swap;
  [DefaultValue(null)]
  public string[]? swaps;
  [DefaultValue(null)]
  public SpawnData[]? spawn;
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
  [DefaultValue("")]
  public string data = "";
  [DefaultValue(null)]
  public string? command;
  [DefaultValue(null)]
  public string[]? commands;
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
  public PokeData[]? poke;
  [DefaultValue(null)]
  public string[]? pokes;
  [DefaultValue(0)]
  public int pokeLimit = 0;
  [DefaultValue("")]
  public string pokeParameter = "";
  [DefaultValue(0f)]
  public float pokeDelay = 0f;
  [DefaultValue(null)]
  public TerrainData[]? terrain;

  [DefaultValue(null)]
  public ObjectData[]? objects;
  [DefaultValue(null)]
  public string? objectsLimit;
  [DefaultValue(null)]
  public ObjectData[]? bannedObjects;
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
  [DefaultValue("")]
  public string addItems = "";
  [DefaultValue("")]
  public string removeItems = "";
  [DefaultValue(null)]
  public string? cancel;
  [DefaultValue(null)]
  public string? exec;
  [DefaultValue(null)]
  public string? admin;
}


public class Info
{
  public string Prefabs = "";
  public string ExcludedPrefabs = "";
  public ActionType Type = ActionType.Create;
  public bool Fallback = false;
  public string[] Args = [];
  public IFloatValue? Weight;
  public Spawn[]? Swaps;
  public Spawn[]? Spawns;
  public IBoolValue? Remove;
  public bool Regenerate = false;
  public IFloatValue? RemoveDelay;
  public IStringValue? Drops;
  public IStringValue? Data;
  public bool? InjectData;
  public string[] Commands = [];
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
  public Terrain[]? Terrains;
  public int PokeLimit = 0;
  public string PokeParameter = "";
  public float PokeDelay = 0f;
  public Range<int>? ObjectsLimit;
  public Object[]? Objects;
  public Range<int>? BannedObjectsLimit;
  public Object[]? BannedObjects;
  public HashSet<string>? Locations;
  public HashSet<string>? BannedLocations;
  public float LocationDistance = 0f;
  public float BannedLocationDistance = 0f;
  public HashSet<string>? PlayerEvents;
  public HashSet<string>? BannedPlayerEvents;
  public Filters? Filters;
  public bool TriggerRules;
  public ObjectRpcInfo[]? ObjectRpcs;
  public ClientRpcInfo[]? ClientRpcs;
  public Color? MinPaint;
  public Color? MaxPaint;
  public IFloatValue? MinTerrainHeight;
  public IFloatValue? MaxTerrainHeight;
  public DataEntry? AddItems;
  public DataEntry? RemoveItems;
  public ILongValue? Owner;
  public IBoolValue? Cancel;
  public IStringValue? Execute;
  public IBoolValue? Admin;
}

public class SpawnData
{
  [DefaultValue(null)]
  public string? prefab;
  [DefaultValue(null)]
  public string? snap;
  [DefaultValue(null)]
  public string? pos;
  [DefaultValue(null)]
  public string? position;
  [DefaultValue(null)]
  public string? rot;
  [DefaultValue(null)]
  public string? rotation;
  [DefaultValue(null)]
  public string? data;
  [DefaultValue(null)]
  public string? delay;
  [DefaultValue(null)]
  public string? triggerRules;
}

public class Spawn
{
  private readonly IPrefabValue Prefab;
  public readonly IVector3Value? Pos;
  public readonly IBoolValue? Snap;
  public readonly IQuaternionValue? Rot;
  public readonly IStringValue? Data;
  public readonly IFloatValue? Delay;
  public readonly IBoolValue? TriggerRules;

  public Spawn(SpawnData data, float? delay, bool? triggerRules)
  {
    Prefab = data.prefab == null ? new SimplePrefabValue(0) : DataValue.Prefab(data.prefab);
    Pos = data.pos != null ? DataValue.Vector3(data.pos) : data.position != null ? DataValue.Vector3(data.position) : null;
    Snap = data.snap == null ? null : DataValue.Bool(data.snap);
    Rot = data.rot != null ? DataValue.Quaternion(data.rot) : data.rotation != null ? DataValue.Quaternion(data.rotation) : null;
    Data = data.data == null ? null : DataValue.String(data.data);
    Delay = data.delay == null ? delay == null ? null : new SimpleFloatValue(delay.Value) : DataValue.Float(data.delay);
    TriggerRules = data.triggerRules == null ? triggerRules == null ? null : new SimpleBoolValue(triggerRules.Value) : DataValue.Bool(data.triggerRules);
  }

  public Spawn(string line, float? delay, bool? triggerRules)
  {
    Delay = delay == null ? null : new SimpleFloatValue(delay.Value);
    TriggerRules = triggerRules == null ? null : new SimpleBoolValue(triggerRules.Value);
    var split = Parse.ToList(line);
    Prefab = DataValue.Prefab(split[0]);
    var posParsed = false;
    for (var i = 1; i < split.Count; i++)
    {
      var value = split[i];
      if (Parse.TryBoolean(value, out var boolean))
        TriggerRules = new SimpleBoolValue(boolean);
      else if (Parse.TryFloat(value, out var number1))
      {
        if (split.Count <= i + 2)
          Delay = new SimpleFloatValue(number1);
        else if (Parse.TryFloat(split[i + 1], out var number2))
        {
          var number3 = Parse.Float(split[i + 2]);
          if (posParsed)
          {
            Rot = new SimpleQuaternionValue(Quaternion.Euler(number2, number1, number3));
          }
          else
          {
            Pos = new SimpleVector3Value(new Vector3(number1, number3, number2));
            if (split[i + 2] == "snap")
              Snap = new SimpleBoolValue(true);
            posParsed = true;
          }
          i += 2;
        }
        else
          Delay = new SimpleFloatValue(number1);
      }
      else
        Data = new SimpleStringValue(value);
    }

  }
  public int GetPrefab(Parameters pars) => Prefab.Get(pars) ?? 0;
}

public class Poke(PokeData data)
{
  public Object Filter = new(data);
  private readonly string? Parameter = data.parameter;
  private readonly string[]? Parameters = data.pars == null ? null : Parse.ToArr(data.pars);
  public IIntValue? Limit = data.limit == null ? null : DataValue.Int(data.limit);
  public IFloatValue? Delay = data.delay == null ? null : DataValue.Float(data.delay);
  public IBoolValue? Self = data.self == null ? null : DataValue.Bool(data.self);
  public IZdoIdValue? Target = data.target == null ? null : DataValue.ZdoId(data.target);
  private readonly IBoolValue? Evaluate = data.evaluate == null ? null : DataValue.Bool(data.evaluate);
  public bool HasPrefab = data.prefab != null && data.prefab != "";

  public string[] GetArgs(Parameters pars)
  {

    if (Parameters != null)
      return [.. Parameters.Select(pars.Replace)];
    else
    {
      var pokeParameter = pars.Replace(Parameter ?? "");
      if (Evaluate?.GetBool(pars) != false)
        pokeParameter = PokeEvaluate(pokeParameter);
      return pokeParameter.Split(' ');
    }
  }


  public static string PokeEvaluate(string str)
  {
    var expressions = str.Split(' ').ToArray();
    bool changed = false;
    for (var i = 0; i < expressions.Length; ++i)
    {
      var expression = expressions[i];
      if (expression.Length == 0) continue;
      // Single negative number would get handled as expression.
      var sub = expression.Substring(1);
      if (!sub.Contains('*') && !sub.Contains('/') && !sub.Contains('+') && !sub.Contains('-')) continue;
      changed = true;
      var value = Calculator.EvaluateFloat(expression);
      if (value.HasValue)
        expressions[i] = value.Value.ToString("0.#####", NumberFormatInfo.InvariantInfo);
    }
    return changed ? string.Join(" ", expressions) : str;
  }
}
public class Object
{
  private readonly IPrefabValue PrefabsValue;
  private readonly IFloatValue? MinDistanceValue;
  private readonly IFloatValue MaxDistanceValue;
  private readonly IFloatValue? MinHeightValue;
  private readonly IFloatValue? MaxHeightValue;
  private readonly IVector3Value? OffsetValue;
  private readonly Filters? filters;
  private readonly IIntValue WeightValue = new SimpleIntValue(1);

  public Object(ObjectData data)
  {

    PrefabsValue = DataValue.Prefab(data.prefab);
    if (data.minDistance != null)
      MinDistanceValue = DataValue.Float(data.minDistance);
    if (data.maxDistance != null)
      MaxDistanceValue = DataValue.Float(data.maxDistance);
    else
      MaxDistanceValue = new SimpleFloatValue(100);
    if (data.minHeight != null)
      MinHeightValue = DataValue.Float(data.minHeight);
    if (data.maxHeight != null)
      MaxHeightValue = DataValue.Float(data.maxHeight);
    if (data.offset != null)
      OffsetValue = DataValue.Vector3(data.offset);
    if (data.weight != null)
      WeightValue = DataValue.Int(data.weight);
    if (data.filters != null || data.bannedFilters != null)
      filters = new Filters(data.filters, data.bannedFilters, data.filterLimit);
    else if (data.data != null)
      filters = new Filters([data.data], null, data.filterLimit);
  }
  public Object(string line)
  {
    var split = Parse.ToList(line);
    PrefabsValue = DataValue.Prefab(split[0]);
    MaxDistanceValue = new SimpleFloatValue(100f);

    if (split.Count > 1)
    {
      var range = Parse.StringRange(split[1]);
      if (range.Min != range.Max)
        MinDistanceValue = DataValue.Float(range.Min);
      MaxDistanceValue = DataValue.Float(range.Max);
    }
    if (split.Count > 2)
      filters = new Filters([split[2]], null, null);
    if (split.Count > 3)
    {
      WeightValue = DataValue.Int(split[3]);
    }
    if (split.Count > 4)
    {
      var range = Parse.StringRange(split[4]);
      if (range.Min != range.Max)
        MinHeightValue = DataValue.Float(range.Min);
      MaxHeightValue = DataValue.Float(range.Max);
    }
  }
  private float? MinDistance;
  public float MaxDistance;
  private float? MinHeight;
  private float? MaxHeight;
  private Vector3? Offset;
  public Vector3 CachedPosition;
  public int Weight;

  public void Roll(Parameters pars, Vector3 pos, Quaternion rot)
  {
    MinDistance = MinDistanceValue?.Get(pars);
    MaxDistance = MaxDistanceValue.Get(pars) ?? 100f;
    MinHeight = MinHeightValue?.Get(pars);
    MaxHeight = MaxHeightValue?.Get(pars);
    Weight = WeightValue.Get(pars) ?? 1;
    Offset = OffsetValue?.Get(pars);
    if (Offset.HasValue)
      CachedPosition = pos + rot * Offset.Value;
    else
      CachedPosition = pos;
  }

  public bool IsValid(ZDO zdo, Parameters pars)
  {
    if (PrefabsValue.Match(pars, zdo.GetPrefab()) == false) return false;
    var d = Utils.DistanceXZ(CachedPosition, zdo.GetPosition());
    if (MinDistance != null && d < MinDistance) return false;
    if (d > MaxDistance) return false;
    var dy = Mathf.Abs(CachedPosition.y - zdo.GetPosition().y);
    if (MinHeight != null && dy < MinHeight) return false;
    if (MaxHeight != null && dy > MaxHeight) return false;

    if (filters == null) return true;
    return filters.Match(pars, zdo);
  }
}

public class PokeData : ObjectData
{
  [DefaultValue(null)]
  public string? delay;
  [DefaultValue(null)]
  public string? self;
  [DefaultValue(null)]
  public string? target;
  [DefaultValue(null)]
  public string? parameter;
  [DefaultValue(null)]
  public string? pars;
  [DefaultValue(null)]
  public string? limit;
  [DefaultValue(null)]
  public string? evaluate;
}
public class ObjectData
{
  [DefaultValue("")]
  public string prefab = "";
  [DefaultValue(null)]
  public string? maxDistance;
  [DefaultValue(null)]
  public string? minDistance;
  [DefaultValue(null)]
  public string? maxHeight;
  [DefaultValue(null)]
  public string? minHeight;
  [DefaultValue(null)]
  public string? offset;
  [DefaultValue(null)]
  public string? data;
  [DefaultValue(null)]
  public string[]? filters;
  [DefaultValue(null)]
  public string[]? bannedFilters;
  [DefaultValue(null)]
  public string? filterLimit;
  [DefaultValue(null)]
  public string? weight;
}


public class InfoType
{
  public readonly ActionType Type;
  public readonly string[] Parameters;
  public InfoType(string prefab, string line)
  {
    var types = Parse.Kvp(line);
    if (!Enum.TryParse(types.Key, true, out Type))
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


public class TerrainData
{
  [DefaultValue(null)]
  public string? delay;
  [DefaultValue(null)]
  public string? pos;
  [DefaultValue(null)]
  public string? position;
  [DefaultValue(null)]
  public string? square;
  [DefaultValue(null)]
  public string? resetRadius;
  [DefaultValue(null)]
  public string? levelRadius;
  [DefaultValue(null)]
  public string? levelOffset;
  [DefaultValue(null)]
  public string? raiseRadius;
  [DefaultValue(null)]
  public string? raisePower;
  [DefaultValue(null)]
  public string? raiseDelta;
  [DefaultValue(null)]
  public string? smoothRadius;
  [DefaultValue(null)]
  public string? smoothPower;
  [DefaultValue(null)]
  public string? paintRadius;
  [DefaultValue(null)]
  public string? paintHeightCheck;
  [DefaultValue(null)]
  public string? paint;
}


public class Filters(string[]? filters, string[]? bannedFilters, string? filterLimit)
{
  // Default limit is that all positive filters must match.
  public readonly IFloatValue? Limit = filterLimit == null ? new SimpleFloatValue(filters?.Length ?? 0f) : DataValue.Float(filterLimit);
  public readonly Filter[] Values = [.. filters?.Select(f => new Filter(f, false)) ?? [], .. bannedFilters?.Select(f => new Filter(f, true)) ?? []];

  public bool Match(Parameters parameters, ZDO zdo)
  {
    var limit = Limit?.Get(parameters);
    if (limit == null) return false;
    var totalWeight = Values.Sum(f => f.Match(parameters, zdo));
    return limit <= totalWeight || Helper.Approx(totalWeight, limit.Value);
  }
}

public class Filter
{
  public Filter(string filter, bool banned)
  {
    var split = Parse.ToList(filter);
    // Data is either single name or type, key, value format.
    // Last part can optionally be a weight.
    if (split.Count == 2)
    {
      Weight = DataValue.Float(split[1]);
      filter = split[0];
    }
    else if (split.Count == 4)
    {
      Weight = DataValue.Float(split[3]);
      filter = string.Join(",", split.Take(3));
    }
    else
    {
      Weight = banned ? new SimpleFloatValue(10000) : new SimpleFloatValue(1);
    }
    Data = DataValue.String(filter);
    Banned = banned;
  }
  public readonly IStringValue? Data;
  public readonly bool Banned;
  // Default behavior is that none of the banned filters must match (so default limit of banned filter must be very high).
  public readonly IFloatValue? Weight;

  public float Match(Parameters parameters, ZDO zdo)
  {
    var data = DataHelper.Get(Data, parameters);
    if (data == null) return 0f;
    if (!data.Match(parameters, zdo)) return 0f;
    var weight = Weight?.Get(parameters) ?? 0f;
    // Negative weight for banned means it counts towards failing the filter.
    return Banned ? -weight : weight;
  }
}

public class Terrain(TerrainData data)
{
  public readonly IFloatValue? Delay = data.delay == null ? null : DataValue.Float(data.delay);
  public readonly IFloatValue? ResetRadius = data.resetRadius == null ? null : DataValue.Float(data.resetRadius);
  public readonly IVector3Value? Position = data.pos != null ? DataValue.Vector3(data.pos) : data.position != null ? DataValue.Vector3(data.position) : null;
  public readonly IBoolValue? Square = data.square == null ? null : DataValue.Bool(data.square);
  public readonly IFloatValue? LevelRadius = data.levelRadius == null ? null : DataValue.Float(data.levelRadius);
  public readonly IFloatValue? LevelOffset = data.levelOffset == null ? null : DataValue.Float(data.levelOffset);
  public readonly IFloatValue? RaiseRadius = data.raiseRadius == null ? null : DataValue.Float(data.raiseRadius);
  public readonly IFloatValue? RaisePower = data.raisePower == null ? null : DataValue.Float(data.raisePower);
  public readonly IFloatValue? RaiseDelta = data.raiseDelta == null ? null : DataValue.Float(data.raiseDelta);
  public readonly IFloatValue? SmoothRadius = data.smoothRadius == null ? null : DataValue.Float(data.smoothRadius);
  public readonly IFloatValue? SmoothPower = data.smoothPower == null ? null : DataValue.Float(data.smoothPower);
  public readonly IFloatValue? PaintRadius = data.paintRadius == null ? null : DataValue.Float(data.paintRadius);
  public readonly IBoolValue? PaintHeightCheck = data.paintHeightCheck == null ? null : DataValue.Bool(data.paintHeightCheck);
  public readonly IStringValue? Paint = data.paint == null ? null : DataValue.String(data.paint);

  public void Get(Parameters pars, Vector3 basePosition, Quaternion baseRotation, out Vector3 pos, out float size, out float resetRadius, out ZPackage pkg)
  {
    pos = basePosition;
    pos += baseRotation * (Position?.Get(pars) ?? Vector3.zero);
    pkg = new ZPackage();
    pkg.Write(pos);
    pkg.Write(LevelOffset?.Get(pars) ?? 0f);
    var levelRadius = LevelRadius?.Get(pars) ?? 0f;
    pkg.Write(levelRadius > 0f);
    pkg.Write(levelRadius);
    pkg.Write(Square?.GetBool(pars) == true);
    var raiseRadius = RaiseRadius?.Get(pars) ?? 0f;
    pkg.Write(raiseRadius > 0f);
    pkg.Write(raiseRadius);
    pkg.Write(RaisePower?.Get(pars) ?? 0f);
    pkg.Write(RaiseDelta?.Get(pars) ?? 0f);
    var smoothRadius = SmoothRadius?.Get(pars) ?? 0f;
    pkg.Write(smoothRadius > 0f);
    pkg.Write(smoothRadius);
    pkg.Write(SmoothPower?.Get(pars) ?? 0f);
    var paintRadius = PaintRadius?.Get(pars) ?? 0f;
    pkg.Write(paintRadius > 0f);
    pkg.Write(PaintHeightCheck?.GetBool(pars) == true);
    var paint = Paint?.Get(pars) ?? "Reset";
    var paintEnum =
      Enum.TryParse(paint, true, out TerrainModifier.PaintType paintType) ? paintType :
      int.TryParse(paint, out var paintInt) ? (TerrainModifier.PaintType)paintInt :
      TerrainModifier.PaintType.Reset;
    pkg.Write((int)paintEnum);
    pkg.Write(paintRadius);
    resetRadius = ResetRadius?.Get(pars) ?? 0f;
    size = Mathf.Max(levelRadius, raiseRadius, smoothRadius, paintRadius, resetRadius);
  }
}