using System;
using System.Collections.Generic;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class Helper
{

  public static bool CheckWild(string wild, string str)
  {
    if (wild == "*")
      return true;
    // Could be optimized when data is parsed to see if it's array, wild or range.
    var split = Parse.Split(wild);
    if (split.Length > 1)
    {
      foreach (var s in split)
      {
        if (CheckWild(s, str))
          return true;
      }
      return false;
    }
    if (wild[0] == '*' && wild[wild.Length - 1] == '*')
      return str.ToLowerInvariant().Contains(wild.Substring(1, wild.Length - 2).ToLowerInvariant());
    if (wild[0] == '*')
      return str.EndsWith(wild.Substring(1), StringComparison.OrdinalIgnoreCase);
    else if (wild[wild.Length - 1] == '*')
      return str.StartsWith(wild.Substring(0, wild.Length - 1), StringComparison.OrdinalIgnoreCase);
    /*else if (Parse.TryLong(str, out var l))
    {
      var range = Parse.LongRange(wild);
      return range.Min <= l && l <= range.Max;
    }*/
    else if (Parse.TryFloat(str, out var f) && (Parse.TryFloat(wild, out var _) || wild.Contains(";")))
    {
      var range = Parse.FloatRange(wild);
      return ApproxBetween(f, range.Min, range.Max);
    }
    else
      return str.Equals(wild, StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsServer() => ZNet.instance && ZNet.instance.IsServer();
  // Note: Intended that is client when no Znet instance (so stuff isn't loaded in the main menu).
  public static bool IsClient() => !IsServer();

  public static bool IsZero(float a) => Mathf.Abs(a) < 0.001f;
  public static bool Approx(float a, float b) => Mathf.Abs(a - b) < 0.001f;
  public static bool ApproxBetween(float a, float min, float max) => min - 0.001f <= a && a <= max + 0.001f;

  public static bool HasAnyGlobalKey(List<string> keys, Parameters pars)
  {
    foreach (var key in keys)
    {
      if (key.Contains("<"))
      {
        if (ZoneSystem.instance.m_globalKeys.Contains(pars.Replace(key))) return true;
      }
      else
      {
        if (ZoneSystem.instance.m_globalKeys.Contains(key)) return true;
      }
    }
    return false;
  }
  public static bool HasEveryGlobalKey(List<string> keys, Parameters pars)
  {
    foreach (var key in keys)
    {
      if (key.Contains("<"))
      {
        if (!ZoneSystem.instance.m_globalKeys.Contains(pars.Replace(key))) return false;
      }
      else
      {
        if (!ZoneSystem.instance.m_globalKeys.Contains(key)) return false;
      }
    }
    return true;
  }
}