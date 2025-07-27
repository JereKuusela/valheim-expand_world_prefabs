
using System.Collections.Generic;
using UnityEngine;

namespace Data;

public class PrefabValue(string[] values) : AnyValue(values), IPrefabValue
{
  // Caching makes sense because parameters and wildcards makes it slow.
  // Also prefab is often checked many times.
  private List<int>? Cache;
  private Parameters? LastParameters;
  public int? Get(Parameters pars)
  {
    if (pars != LastParameters)
    {
      var values = GetAllValues(pars);
      Cache = PrefabHelper.GetPrefabs(values, null);
      LastParameters = pars;
    }
    if (Cache == null || Cache.Count == 0) return null;
    if (Cache.Count == 1) return Cache[0];
    return Cache[Random.Range(0, Cache.Count)];
  }

  public bool? Match(Parameters pars, int value)
  {
    if (pars != LastParameters)
    {
      var values = GetAllValues(pars);
      Cache = PrefabHelper.GetPrefabs(values, null);
      LastParameters = pars;
    }
    if (Cache == null || Cache.Count == 0) return null;
    return Cache.Contains(value);
  }
}
public class SimplePrefabsValue(List<int> value) : IPrefabValue
{
  private readonly List<int> Values = value;

  public int? Get(Parameters pars) => RollValue();
  public bool? Match(Parameters pars, int value) => Values.Contains(value);
  private int RollValue() => Values[Random.Range(0, Values.Count)];
}
public class SimplePrefabValue(int? value) : IPrefabValue
{
  private readonly int? Value = value;

  public int? Get(Parameters pars) => Value;
  public bool? Match(Parameters pars, int value) => Value == value;
}
public interface IPrefabValue
{
  int? Get(Parameters pars);
  bool? Match(Parameters pars, int value);
}