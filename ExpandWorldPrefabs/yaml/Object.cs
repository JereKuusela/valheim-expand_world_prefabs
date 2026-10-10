using System.ComponentModel;
using Common;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class Object
{
  private readonly IPrefabValue PrefabsValue;
  private readonly bool HasPrefabFilter;
  private readonly IPrefabValue? ExcludedPrefabsValue;
  private readonly IFloatValue? MinDistanceValue;
  private readonly IFloatValue MaxDistanceValue;
  private readonly IFloatValue? MinHeightValue;
  private readonly IFloatValue? MaxHeightValue;
  private readonly IVector3Value? PositionValue;
  private readonly IVector3Value? OffsetValue;
  private readonly ConditionClause? Condition;
  private readonly Filters? filters;
  private readonly IIntValue WeightValue = new ConstantIntValue(1);
  private readonly IBoolValue? IncludeSelfValue;

  public Object(ObjectYaml data)
  {

    HasPrefabFilter = !string.IsNullOrWhiteSpace(data.prefab);
    PrefabsValue = DataValue.Prefab(data.prefab);
    if (!string.IsNullOrWhiteSpace(data.excludePrefab))
      ExcludedPrefabsValue = DataValue.Prefab(data.excludePrefab);
    if (data.minDistance != null)
      MinDistanceValue = DataValue.Float(data.minDistance);
    if (data.maxDistance != null)
      MaxDistanceValue = DataValue.Float(data.maxDistance);
    else
      MaxDistanceValue = new ConstantFloatValue(100);
    if (data.minHeight != null)
      MinHeightValue = DataValue.Float(data.minHeight);
    if (data.maxHeight != null)
      MaxHeightValue = DataValue.Float(data.maxHeight);
    if (data.position != null)
      PositionValue = DataValue.Vector3(data.position);
    if (data.offset != null)
      OffsetValue = DataValue.Vector3(data.offset);
    if (data.weight != null)
      WeightValue = DataValue.Int(data.weight);
    if (data.self != null)
      IncludeSelfValue = DataValue.Bool(data.self);
    if (data.condition != null)
    {
      if (Conditions.TryParse(data.condition, out var condition, out var error))
        Condition = condition;
      else
      {
        Log.Warning($"Invalid object condition '{data.condition}' for prefab '{data.prefab}': {error}");
        Condition = Conditions.False(data.condition);
      }
    }
    if (data.filters != null || data.bannedFilters != null)
      filters = new Filters(data.filters, data.bannedFilters, data.filterLimit);
    else if (data.data?.Name != null)
      filters = new Filters([data.data.Name], null, data.filterLimit);
  }
  private float? MinDistance;
  public float MaxDistance;
  private float? MinHeight;
  private float? MaxHeight;
  private Vector3? Position;
  private Vector3? Offset;
  public Vector3 CachedPosition;
  public int Weight;
  public bool IncludeSelf;

  public void Roll(Functions f, Vector3 pos, Quaternion rot)
  {
    MinDistance = MinDistanceValue?.Get(f);
    MaxDistance = MaxDistanceValue.Get(f) ?? 100f;
    MinHeight = MinHeightValue?.Get(f);
    MaxHeight = MaxHeightValue?.Get(f);
    Weight = WeightValue.Get(f) ?? 1;
    IncludeSelf = IncludeSelfValue?.GetBool(f) == true;
    Position = PositionValue?.Get(f);
    Offset = OffsetValue?.Get(f);
    if (Position.HasValue)
      CachedPosition = Position.Value;
    else
      CachedPosition = pos;

    if (Offset.HasValue)
      CachedPosition += rot * Offset.Value;
  }

  public bool IsValid(ZDO zdo, Functions f, ZDOID? self)
  {
    if (!IncludeSelf && zdo.m_uid == self) return false;
    if (HasPrefabFilter && PrefabsValue.Match(f, zdo.GetPrefab()) != true) return false;
    if (ExcludedPrefabsValue?.Match(f, zdo.GetPrefab()) == true) return false;
    var d = Utils.DistanceXZ(CachedPosition, zdo.GetPosition());
    if (MinDistance != null && d < MinDistance) return false;
    if (d > MaxDistance) return false;
    var dy = Mathf.Abs(CachedPosition.y - zdo.GetPosition().y);
    if (MinHeight != null && dy < MinHeight) return false;
    if (MaxHeight != null && dy > MaxHeight) return false;
    if (Condition != null && !Condition.Evaluate(f)) return false;

    if (filters == null) return true;
    return filters.Match(f, zdo);
  }

  public bool AllowSelf(Functions f) => IncludeSelfValue?.GetBool(f) == true;
}

public class ObjectYaml
{
  [DefaultValue("")]
  public string prefab = "";
  [DefaultValue("")]
  public string excludePrefab = "";
  [DefaultValue(null)]
  public string? maxDistance;
  [DefaultValue(null)]
  public string? minDistance;
  [DefaultValue(null)]
  public string? maxHeight;
  [DefaultValue(null)]
  public string? minHeight;
  [DefaultValue(null)]
  public string? position;
  [DefaultValue(null)]
  public string? offset;
  [DefaultValue(null)]
  public DataField? data;
  [DefaultValue(null)]
  public string[]? filters;
  [DefaultValue(null)]
  public string[]? bannedFilters;
  [DefaultValue(null)]
  public string? filterLimit;
  [DefaultValue(null)]
  public string? weight;
  [DefaultValue(null)]
  public string? self;
  [DefaultValue(null)]
  public string? condition;

  // Legacy format: prefab, distance range, data, weight, height range, condition.
  public static ObjectYaml FromLine(string line)
  {
    var split = Parse.ToList(line);
    var result = new ObjectYaml { prefab = split[0] };
    if (split.Count > 1)
    {
      var distance = Parse.StringRange(split[1]);
      if (distance.Min != distance.Max)
        result.minDistance = distance.Min.ToString();
      result.maxDistance = distance.Max.ToString();
    }
    if (split.Count > 2) result.data = new DataField(split[2]);
    if (split.Count > 3) result.weight = split[3];
    if (split.Count > 4)
    {
      var height = Parse.StringRange(split[4]);
      if (height.Min != height.Max)
        result.minHeight = height.Min.ToString();
      result.maxHeight = height.Max.ToString();
    }
    if (split.Count > 5) result.condition = split[5];
    return result;
  }
}
