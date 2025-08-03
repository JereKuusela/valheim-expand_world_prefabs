using System;
using System.Collections.Generic;
using System.Globalization;
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
      // No return to also compare the full value.
    }
    if (wild[0] == '*' && wild[wild.Length - 1] == '*')
      return str.ToLowerInvariant().Contains(wild.Substring(1, wild.Length - 2).ToLowerInvariant());
    if (wild[0] == '*')
      return str.EndsWith(wild.Substring(1), StringComparison.OrdinalIgnoreCase);
    if (wild[wild.Length - 1] == '*')
      return str.StartsWith(wild.Substring(0, wild.Length - 1), StringComparison.OrdinalIgnoreCase);
    var wildIndex = wild.IndexOf('*');
    if (wildIndex > 0 && wildIndex < wild.Length - 1)
    {
      var prefix = wild.Substring(0, wildIndex);
      var suffix = wild.Substring(wildIndex + 1);
      return str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
             str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }
    /*else if (Parse.TryLong(str, out var l))
    {
      var range = Parse.LongRange(wild);
      return range.Min <= l && l <= range.Max;
    }*/
    if (Parse.TryFloat(str, out var f) && (Parse.TryFloat(wild, out var _) || wild.Contains(";")))
    {
      var range = Parse.FloatRange(wild);
      return ApproxBetween(f, range.Min, range.Max);
    }
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
        if (!ZoneSystem.instance.m_globalKeys.Contains(pars.Replace(key).ToLowerInvariant())) return false;
      }
      else
      {
        if (!ZoneSystem.instance.m_globalKeys.Contains(key)) return false;
      }
    }
    return true;
  }


  public static string Format(float value) => value.ToString("0.#####", NumberFormatInfo.InvariantInfo);
  public static string Format(double value) => value.ToString("0.#####", NumberFormatInfo.InvariantInfo);
  public static string FormatPos(Vector3 value) => $"{Format(value.x)},{Format(value.z)},{Format(value.y)}";
  public static string FormatRot(Vector3 value) => $"{Format(value.y)},{Format(value.x)},{Format(value.z)}";
  public static string FormatPos2(Vector3 value) => $"{Format2(value.x)},{Format2(value.z)},{Format2(value.y)}";
  public static string FormatRot2(Vector3 value) => $"{Format2(value.y)},{Format2(value.x)},{Format2(value.z)}";
  public static string Format2(float value) => value.ToString("0.##", NumberFormatInfo.InvariantInfo);
}