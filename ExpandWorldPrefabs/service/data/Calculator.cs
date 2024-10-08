using System;
using System.Globalization;
using System.Linq;
using Service;
using UnityEngine;

namespace Data;

public class Calculator
{

  public static Vector3 EvaluateVector3(string expression)
  {
    var vector = Vector3.zero;
    var s = Parse.Split(expression);
    vector.x = EvaluateFloat(s[0]) ?? 0f;
    if (s.Length > 1) vector.z = EvaluateFloat(s[1]) ?? 0f;
    if (s.Length > 2) vector.y = EvaluateFloat(s[2]) ?? 0f;
    return vector;
  }
  public static Vector3 EvaluateVector3(string[] s, int index)
  {
    var vector = Vector3.zero;
    if (s.Length > index) vector.x = EvaluateFloat(s[index]) ?? 0f;
    if (s.Length > index + 1) vector.z = EvaluateFloat(s[index + 1]) ?? 0f;
    if (s.Length > index + 2) vector.y = EvaluateFloat(s[index + 2]) ?? 0f;
    return vector;
  }
  public static Quaternion EvaluateQuaternion(string expression)
  {
    var vector = Vector3.zero;
    var s = Parse.Split(expression);
    vector.y = EvaluateFloat(s[0]) ?? 0f;
    if (s.Length > 1) vector.x = EvaluateFloat(s[1]) ?? 0f;
    if (s.Length > 2) vector.z = EvaluateFloat(s[2]) ?? 0f;
    return Quaternion.Euler(vector);
  }
  public static Quaternion EvaluateQuaternion(string[] s, int index)
  {
    var vector = Vector3.zero;
    if (s.Length > index) vector.y = EvaluateFloat(s[index]) ?? 0f;
    if (s.Length > index + 1) vector.x = EvaluateFloat(s[index + 1]) ?? 0f;
    if (s.Length > index + 2) vector.z = EvaluateFloat(s[index + 2]) ?? 0f;
    return Quaternion.Euler(vector);
  }
  public static int? EvaluateInt(string expression)
  {
    try
    {
      return (int?)EvaluateLong(expression);
    }
    catch
    {
      return null;
    }
  }
  public static float? EvaluateFloat(string expression)
  {
    try
    {
      return (float?)EvaluateDouble(expression);
    }
    catch
    {
      return null;
    }
  }
  private static double EvaluateDouble(string expression)
  {
    var plus = expression.Split('+');
    if (plus.Length > 1)
    {
      var sum = 0d;
      foreach (var p in plus) sum += EvaluateDouble(p);
      return sum;
    }
    var minus = expression.Split('-');
    // Negative numbers get split as well, so check for actual parts.
    if (minus.Where(s => s != "").Count() > 1)
    {
      double? sum = null;
      for (var i = 0; i < minus.Length; ++i)
      {
        if (minus[i] == "" && i + 1 < minus.Length)
        {
          minus[i + 1] = "-" + minus[i + 1];
          continue;
        }
        if (sum == null) sum = EvaluateDouble(minus[i]);
        else sum -= EvaluateDouble(minus[i]);
      }
      return sum ?? 0;
    }

    var mult = expression.Split('*');
    if (mult.Length > 1)
    {
      var sum = 1d;
      foreach (var m in mult) sum *= EvaluateDouble(m);
      return sum;
    }
    var div = expression.Split('/');
    if (div.Length > 1)
    {
      var sum = EvaluateDouble(div[0]);
      for (var i = 1; i < div.Length; ++i) sum /= EvaluateDouble(div[i]);
      return sum;
    }
    try
    {
      return double.Parse(expression.Trim(), NumberFormatInfo.InvariantInfo);
    }
    catch
    {
      throw new InvalidOperationException($"Failed to parse expression: {expression}");
    }
  }

  public static long? EvaluateLong(string expression)
  {
    try
    {
      return EvalLong(expression);
    }
    catch
    {
      return null;
    }
  }
  private static long EvalLong(string expression)
  {
    var plus = expression.Split('+');
    if (plus.Length > 1)
    {
      var sum = 0L;
      foreach (var p in plus) sum += EvalLong(p);
      return sum;
    }
    var minus = expression.Split('-');
    // Negative numbers get split as well, so check for actual parts.
    if (minus.Where(s => s != "").Count() > 1)
    {
      long? sum = null;
      for (var i = 0; i < minus.Length; ++i)
      {
        if (minus[i] == "" && i + 1 < minus.Length)
        {
          minus[i + 1] = "-" + minus[i + 1];
          continue;
        }
        if (sum == null) sum = EvalLong(minus[i]);
        else sum -= EvalLong(minus[i]);
      }
      return sum ?? 0;
    }
    var mult = expression.Split('*');
    if (mult.Length > 1)
    {
      var sum = 1L;
      foreach (var m in mult) sum *= EvalLong(m);
      return sum;
    }
    var div = expression.Split('/');
    if (div.Length > 1)
    {
      var sum = EvalLong(div[0]);
      for (var i = 1; i < div.Length; ++i) sum /= EvalLong(div[i]);
      return sum;
    }
    try
    {
      return long.Parse(expression.Trim());
    }
    catch
    {
      throw new InvalidOperationException($"Failed to parse expression: {expression}");
    }
  }

}