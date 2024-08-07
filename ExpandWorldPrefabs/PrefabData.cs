using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class Data
{
  [DefaultValue("")]
  public string prefab = "";
  public string type = "";
  [DefaultValue(null)]
  public string[]? types = null;
  [DefaultValue(false)]
  public bool fallback = false;
  [DefaultValue(1f)]
  public float weight = 1f;
  [DefaultValue(null)]
  public string? swap = null;
  [DefaultValue(null)]
  public string[]? swaps = null;
  [DefaultValue(null)]
  public string? spawn = null;
  [DefaultValue(null)]
  public string[]? spawns = null;
  [DefaultValue(0f)]
  public float spawnDelay = 0f;
  [DefaultValue(false)]
  public bool remove = false;
  [DefaultValue(0f)]
  public float removeDelay = 0f;
  [DefaultValue(false)]
  public bool drops = false;
  [DefaultValue("")]
  public string data = "";
  [DefaultValue(null)]
  public string? command = null;
  [DefaultValue(null)]
  public string[]? commands = null;
  [DefaultValue(true)]
  public bool day = true;
  [DefaultValue(true)]
  public bool night = true;
  [DefaultValue("")]
  public string biomes = "";
  [DefaultValue(0f)]
  public float minDistance = 0f;
  [DefaultValue(100000f)]
  public float maxDistance = 100000f;
  [DefaultValue(-10000f)]
  public float minAltitude = -10000f;
  [DefaultValue(10000f)]
  public float maxAltitude = 10000f;
  [DefaultValue(null)]
  public float? minY = null;
  [DefaultValue(null)]
  public float? maxY = null;
  [DefaultValue("")]
  public string environments = "";
  [DefaultValue("")]
  public string bannedEnvironments = "";
  [DefaultValue("")]
  public string globalKeys = "";
  [DefaultValue("")]
  public string bannedGlobalKeys = "";
  [DefaultValue("")]
  public string events = "";
  [DefaultValue(null)]
  public float? eventDistance = null;
  [DefaultValue(null)]
  public PokeData[]? poke = null;
  [DefaultValue(null)]
  public string[]? pokes = null;
  [DefaultValue(0)]
  public int pokeLimit = 0;
  [DefaultValue("")]
  public string pokeParameter = "";
  [DefaultValue(0f)]
  public float pokeDelay = 0f;

  [DefaultValue(null)]
  public string[]? objects = null;
  [DefaultValue("")]
  public string objectsLimit = "";
  [DefaultValue(null)]
  public string[]? bannedObjects = null;
  [DefaultValue("")]
  public string bannedObjectsLimit = "";
  [DefaultValue("")]
  public string locations = "";
  [DefaultValue(0f)]
  public float locationDistance = 0f;
  [DefaultValue(null)]
  public string? filter = null;
  [DefaultValue(null)]
  public string[]? filters = null;
  [DefaultValue(null)]
  public string? bannedFilter = null;
  [DefaultValue(null)]
  public string[]? bannedFilters = null;
  [DefaultValue(0f)]
  public float delay = 0f;

  [DefaultValue(false)]
  public bool triggerRules = false;
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

  [DefaultValue(false)]
  public bool injectData = false;
  [DefaultValue("")]
  public string addItems = "";
  [DefaultValue("")]
  public string removeItems = "";
}


public class Info
{
  public string Prefabs = "";
  public ActionType Type = ActionType.Create;
  public bool Fallback = false;
  public string[] Args = [];
  public float Weight = 1f;
  public Spawn[] Swaps = [];
  public Spawn[] Spawns = [];
  public bool Remove = false;
  public bool Regenerate = false;
  public float RemoveDelay = 0f;
  public bool Drops = false;
  public string Data = "";
  public bool InjectData = false;
  public string[] Commands = [];
  public bool Day = true;
  public bool Night = true;
  public float MinDistance = 0f;
  public float MaxDistance = 0f;
  public float MinY = 0f;
  public float MaxY = 0f;
  public Heightmap.Biome Biomes = Heightmap.Biome.None;
  public float EventDistance = 0f;
  public HashSet<string> Events = [];
  public HashSet<string> Environments = [];
  public HashSet<string> BannedEnvironments = [];
  public List<string> GlobalKeys = [];
  public List<string> BannedGlobalKeys = [];
  public Object[] LegacyPokes = [];
  public Poke[] Pokes = [];
  public int PokeLimit = 0;
  public string PokeParameter = "";
  public float PokeDelay = 0f;
  public Range<int>? ObjectsLimit = null;
  public Object[] Objects = [];
  public Range<int>? BannedObjectsLimit = null;
  public Object[] BannedObjects = [];
  public HashSet<string> Locations = [];
  public float LocationDistance = 0f;
  public DataEntry? Filter;
  public DataEntry? BannedFilter;
  public bool TriggerRules = false;
  public ObjectRpcInfo[]? ObjectRpcs;
  public ClientRpcInfo[]? ClientRpcs;
  public Color? MinPaint;
  public Color? MaxPaint;
  public DataEntry? AddItems;
  public DataEntry? RemoveItems;
}
public class Spawn
{
  private readonly IPrefabValue Prefab;
  public readonly Vector3 Pos = Vector3.zero;
  public readonly bool Snap = false;
  public readonly Quaternion Rot = Quaternion.identity;
  public readonly string Data = "";
  public readonly float Delay = 0;
  public readonly bool TriggerRules = false;
  public Spawn(string line, float delay, bool triggerRules)
  {
    Delay = delay;
    TriggerRules = triggerRules;
    var split = Parse.ToList(line);
    Prefab = DataValue.Prefab(split[0]);
    var posParsed = false;
    for (var i = 1; i < split.Count; i++)
    {
      var value = split[i];
      if (Parse.TryBoolean(value, out var boolean))
        TriggerRules = boolean;
      else if (Parse.TryFloat(value, out var number1))
      {
        if (split.Count <= i + 2)
          Delay = number1;
        else if (Parse.TryFloat(split[i + 1], out var number2))
        {
          var number3 = Parse.Float(split[i + 2]);
          if (posParsed)
          {
            Rot = Quaternion.Euler(number2, number1, number3);
          }
          else
          {
            Pos = new Vector3(number1, number3, number2);
            if (split[i + 2] == "snap")
              Snap = true;
            posParsed = true;
          }
          i += 2;
        }
        else
          Delay = number1;
      }
      else
        Data = value;
    }

  }
  public int GetPrefab(Parameters pars) => Prefab.Get(pars) ?? 0;
}

public class Poke(PokeData data)
{
  public Object Filter = new(data.prefab, data.minDistance, data.maxDistance, data.minHeight, data.maxHeight, data.data);
  public IStringValue Parameter = DataValue.String(data.parameter);
  public IIntValue Limit = DataValue.Int(data.limit);
  public IFloatValue Delay = DataValue.Float(data.delay);
}
public class Object
{
  public IPrefabValue Prefabs;
  public string WildPrefab = "";
  public float MinDistance = 0f;
  public float MaxDistance = 100f;
  public float MinHeight = 0f;
  public float MaxHeight = 0f;
  public int Data = 0;
  public int Weight = 1;
  public Object(string prefab, float minDistance, float maxDistance, float minHeight, float maxHeight, string data)
  {
    Prefabs = DataValue.Prefab(prefab);
    MinDistance = minDistance;
    if (maxDistance > 0)
      MaxDistance = maxDistance;
    MinHeight = minHeight;
    MaxHeight = maxHeight;
    if (data != "")
    {
      Data = data.GetStableHashCode();
      if (!DataHelper.Exists(Data))
      {
        Log.Error($"Invalid object filter data: {data}");
        Data = 0;
      }
    }
  }
  public Object(string line)
  {
    var split = Parse.ToList(line);
    Prefabs = DataValue.Prefab(split[0]);

    if (split.Count > 1)
    {
      var range = Parse.FloatRange(split[1]);
      MinDistance = range.Min == range.Max ? 0f : range.Min;
      MaxDistance = range.Max;
    }
    if (split.Count > 2)
    {
      Data = split[2].GetStableHashCode();
      if (!DataHelper.Exists(Data))
      {
        Log.Error($"Invalid object filter data: {split[2]}");
        Data = 0;
      }
    }
    if (split.Count > 3)
    {
      Weight = Parse.Int(split[3]);
    }
    if (split.Count > 4)
    {
      var range = Parse.FloatRange(split[4]);
      MinHeight = range.Min == range.Max ? 0f : range.Min;
      MaxHeight = range.Max;
    }
  }

  public bool IsValid(ZDO zdo, Vector3 pos, Parameters pars)
  {
    if (Prefabs.Match(pars, zdo.GetPrefab()) == false) return false;
    if (WildPrefab != "")
    {
      var prefabName = pars.Replace(WildPrefab);
      var hash = prefabName.GetStableHashCode();
      if (zdo.GetPrefab() != hash) return false;
    }
    var d = Utils.DistanceXZ(pos, zdo.GetPosition());
    if (MinDistance > 0f && d < MinDistance) return false;
    if (d > MaxDistance) return false;
    var dy = Mathf.Abs(pos.y - zdo.GetPosition().y);
    if (MinHeight > 0f && dy < MinHeight) return false;
    if (MaxHeight > 0f && dy > MaxHeight) return false;
    if (Data == 0) return true;
    return DataHelper.Match(Data, zdo, pars);
  }
}

public class PokeData
{
  [DefaultValue("")]
  public string prefab = "";
  [DefaultValue("0f")]
  public string delay = "0f";
  [DefaultValue("")]
  public string parameter = "";
  [DefaultValue(0f)]
  public float maxDistance = 0f;
  [DefaultValue(0f)]
  public float minDistance = 0f;
  [DefaultValue(0f)]
  public float maxHeight = 0f;
  [DefaultValue(0f)]
  public float minHeight = 0f;
  [DefaultValue("0")]
  public string limit = "0";
  [DefaultValue("")]
  public string data = "";
}

public class InfoType
{
  public readonly ActionType Type;
  public readonly string[] Parameters;
  public InfoType(string prefab, string line)
  {
    var types = Parse.ToList(line);
    if (types.Count == 0 || !Enum.TryParse(types[0], true, out Type))
    {
      if (line == "")
        Log.Warning($"Missing type for prefab {prefab}.");
      else
        Log.Error($"Failed to parse type {prefab}.");
      Type = ActionType.Create;
    }
    Parameters = types.Count > 1 ? types[1].Split(' ') : [];
  }
}