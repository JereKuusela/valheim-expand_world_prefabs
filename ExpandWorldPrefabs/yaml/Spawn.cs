using System.ComponentModel;
using Common;
using Data;
using Service;

namespace ExpandWorld.Prefab;

public class SpawnYaml
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
  public string? removeDelay;
  [DefaultValue(null)]
  public string? repeat;
  [DefaultValue(null)]
  public string? repeatInterval;
  [DefaultValue(null)]
  public string? repeatChance;
  [DefaultValue(null)]
  public string? chance;
  [DefaultValue(null)]
  public string? weight;
  [DefaultValue(null)]
  public string? owner;
  [DefaultValue(null)]
  public string? attach;
  [DefaultValue(null)]
  public string? connect;
  [DefaultValue(null)]
  public string? triggerRules;
  [DefaultValue(null)]
  public string? condition;

  // Legacy format: prefab, then optional x,z,y / rotation angles / delay / triggerRules / data in any order.
  public static SpawnYaml FromLine(string line)
  {
    var split = Parse.ToList(line);
    var result = new SpawnYaml { prefab = split[0] };
    var posParsed = false;
    for (var i = 1; i < split.Count; i++)
    {
      var value = split[i];
      if (Parse.TryBoolean(value, out _))
        result.triggerRules = value;
      else if (Parse.TryFloat(value, out _))
      {
        if (split.Count <= i + 2)
          result.delay = value;
        else if (Parse.TryFloat(split[i + 1], out _))
        {
          var coordinates = value + "," + split[i + 1] + "," + split[i + 2];
          if (posParsed)
            result.rot = coordinates;
          else
          {
            if (split[i + 2] == "snap")
            {
              result.snap = "true";
              coordinates = value + "," + split[i + 1] + ",0";
            }
            result.pos = coordinates;
            posParsed = true;
          }
          i += 2;
        }
        else
          result.delay = value;
      }
      else
        result.data = value;
    }
    return result;
  }
}

public class Spawn
{
  private readonly IPrefabValue Prefab;
  public readonly IVector3Value? Pos;
  public readonly IBoolValue? Snap;
  public readonly IQuaternionValue? Rot;
  public readonly IStringValue? Data;
  public readonly IFloatValue? Delay;
  public readonly IFloatValue? RemoveDelay;
  public readonly IIntValue? Repeat;
  public readonly IFloatValue? RepeatInterval;
  public readonly IFloatValue? RepeatChance;
  public readonly IFloatValue? Chance;
  public readonly IFloatValue? Weight;
  public readonly ILongValue? Owner;
  public readonly bool OwnerServer;
  public readonly IZdoIdValue? Attach;
  public readonly IZdoIdValue? Connect;
  public readonly IBoolValue? TriggerRules;
  public readonly ConditionClause? Condition;

  public Spawn(SpawnYaml data, float? delay, bool? triggerRules)
  {
    Prefab = data.prefab == null ? new ConstantPrefabValue(0) : DataValue.Prefab(data.prefab);
    Pos = data.pos != null ? DataValue.Vector3(data.pos) : data.position != null ? DataValue.Vector3(data.position) : null;
    Snap = data.snap == null ? null : DataValue.Bool(data.snap);
    Rot = data.rot != null ? DataValue.Quaternion(data.rot) : data.rotation != null ? DataValue.Quaternion(data.rotation) : null;
    Data = data.data == null ? null : DataValue.String(data.data);
    Delay = data.delay == null ? delay == null ? null : new ConstantFloatValue(delay.Value) : DataValue.Float(data.delay);
    RemoveDelay = data.removeDelay == null ? null : DataValue.Float(data.removeDelay);
    Repeat = data.repeat == null ? null : DataValue.Int(data.repeat);
    RepeatInterval = data.repeatInterval == null ? null : DataValue.Float(data.repeatInterval);
    RepeatChance = data.repeatChance == null ? null : DataValue.Float(data.repeatChance);
    Chance = data.chance == null ? null : DataValue.Float(data.chance);
    Weight = data.weight == null ? null : DataValue.Float(data.weight);
    OwnerServer = Helper.IsServerOwner(data.owner);
    Owner = data.owner == null || OwnerServer ? null : DataValue.Long(data.owner);
    Attach = data.attach == null ? null : DataValue.ZdoId(data.attach);
    Connect = data.connect == null ? null : DataValue.ZdoId(data.connect);
    TriggerRules = data.triggerRules == null ? triggerRules == null ? null : new ConstantBoolValue(triggerRules.Value) : DataValue.Bool(data.triggerRules);
    if (data.condition != null)
    {
      if (Conditions.TryParse(data.condition, out var condition, out var error))
        Condition = condition;
      else
      {
        Log.Warning($"Invalid spawn condition '{data.condition}' for spawn prefab '{data.prefab}': {error}");
        Condition = Conditions.False(data.condition);
      }
    }
  }

  public int GetPrefab(Functions f) => Prefab.Get(f) ?? 0;
}
