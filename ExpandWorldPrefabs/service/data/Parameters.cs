using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExpandWorld.Prefab;
using Service;
using UnityEngine;

namespace Data;

// Parameters are technically just a key-value mapping.
// Proper class allows properly adding caching and other features.
// While also ensuring that all code is in one place.
public class Parameters(string prefab, string[] args, Vector3 pos)
{
  protected const char Separator = '_';
  public static Func<string, string?> ExecuteCode = key => null!;
  public static Func<string, string, string?> ExecuteCodeWithValue = (key, value) => null!;

  private readonly double time = ZNet.instance.GetTimeSeconds();

  public string Replace(string str)
  {
    StringBuilder parts = new();
    int nesting = 0;
    var start = 0;
    for (int i = 0; i < str.Length; i++)
    {
      if (str[i] == '<')
      {
        if (nesting == 0)
        {
          parts.Append(str.Substring(start, i - start));
          start = i;
        }
        nesting++;

      }
      if (str[i] == '>')
      {
        if (nesting == 1)
        {
          var key = str.Substring(start, i - start + 1);
          parts.Append(ResolveParameters(key));
          start = i + 1;
        }
        if (nesting > 0)
          nesting--;
      }
    }
    if (start < str.Length)
      parts.Append(str.Substring(start));

    return parts.ToString();
  }
  private string ResolveParameters(string str)
  {
    for (int i = 0; i < str.Length; i++)
    {
      var end = str.IndexOf(">", i);
      if (end == -1) break;
      i = end;
      var start = str.LastIndexOf("<", end);
      if (start == -1) continue;
      var length = end - start + 1;
      if (TryReplaceParameter(str.Substring(start, length), out var resolved))
      {
        str = str.Remove(start, length);
        str = str.Insert(start, resolved);
        // Resolved could contain parameters, so need to recheck the same position.
        i = start - 1;
      }
      else
      {
        i = end;
      }
    }
    return str;
  }
  private bool TryReplaceParameter(string rawKey, out string? resolved)
  {
    var key = rawKey.Substring(1, rawKey.Length - 2);
    var keyDefault = Parse.Kvp(key, '=');
    var defaultValue = keyDefault.Value;
    // Ending with just '=' is probably a base64 encoded value.
    if (defaultValue.All(c => c == '='))
      defaultValue = "";
    else
      key = keyDefault.Key;

    resolved = GetParameter(key, defaultValue);
    if (resolved == null)
      resolved = ResolveValue(rawKey);
    return resolved != rawKey;
  }

  protected virtual string? GetParameter(string key, string defaultValue)
  {
    var value = ExecuteCode(key);
    if (value != null) return value;
    value = GetGeneralParameter(key, defaultValue);
    if (value != null) return value;
    var keyArg = Parse.Kvp(key, Separator);
    if (keyArg.Value == "") return null;
    key = keyArg.Key;
    var arg = keyArg.Value;

    value = ExecuteCodeWithValue(key, arg);
    if (value != null) return value;
    return GetValueParameter(key, arg, defaultValue);
  }

  private string? GetGeneralParameter(string key, string defaultValue) =>
    key switch
    {
      "prefab" => prefab,
      "safeprefab" => prefab.Replace(Separator, '-'),
      "par" => string.Join(" ", args),
      "par0" => GetArg(0, defaultValue),
      "par1" => GetArg(1, defaultValue),
      "par2" => GetArg(2, defaultValue),
      "par3" => GetArg(3, defaultValue),
      "par4" => GetArg(4, defaultValue),
      "par5" => GetArg(5, defaultValue),
      "par6" => GetArg(6, defaultValue),
      "par7" => GetArg(7, defaultValue),
      "par8" => GetArg(8, defaultValue),
      "par9" => GetArg(9, defaultValue),
      "time" => Helper.Format(time),
      "day" => EnvMan.instance.GetDay(time).ToString(),
      "ticks" => ((long)(time * 10000000.0)).ToString(),
      "x" => Helper.Format(pos.x),
      "y" => Helper.Format(pos.y),
      "z" => Helper.Format(pos.z),
      "snap" => Helper.Format(WorldGenerator.instance.GetHeight(pos.x, pos.z)),
      _ => null,
    };

  protected virtual string? GetValueParameter(string key, string value, string defaultValue) =>
   key switch
   {
     "sqrt" => Parse.TryFloat(value, out var f) ? Mathf.Sqrt(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "round" => Parse.TryFloat(value, out var f) ? Mathf.Round(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "ceil" => Parse.TryFloat(value, out var f) ? Mathf.Ceil(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "floor" => Parse.TryFloat(value, out var f) ? Mathf.Floor(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "abs" => Parse.TryFloat(value, out var f) ? Mathf.Abs(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "sin" => Parse.TryFloat(value, out var f) ? Mathf.Sin(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "cos" => Parse.TryFloat(value, out var f) ? Mathf.Cos(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "tan" => Parse.TryFloat(value, out var f) ? Mathf.Tan(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "asin" => Parse.TryFloat(value, out var f) ? Mathf.Asin(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "acos" => Parse.TryFloat(value, out var f) ? Mathf.Acos(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "atan" => Atan(value, defaultValue),
     "pow" => Parse.TryKvp(value, out var kvp, Separator) && Parse.TryFloat(kvp.Key, out var f1) && Parse.TryFloat(kvp.Value, out var f2) ? Mathf.Pow(f1, f2).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "log" => Loga(value, defaultValue),
     "exp" => Parse.TryFloat(value, out var f) ? Mathf.Exp(f).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "min" => HandleMin(value, defaultValue),
     "max" => HandleMax(value, defaultValue),
     "add" => HandleAdd(value, defaultValue),
     "sub" => HandleSub(value, defaultValue),
     "mul" => HandleMul(value, defaultValue),
     "div" => HandleDiv(value, defaultValue),
     "mod" => HandleMod(value, defaultValue),
     "addlong" => HandleAddLong(value, defaultValue),
     "sublong" => HandleSubLong(value, defaultValue),
     "mullong" => HandleMulLong(value, defaultValue),
     "divlong" => HandleDivLong(value, defaultValue),
     "modlong" => HandleModLong(value, defaultValue),
     "randf" => Parse.TryKvp(value, out var kvp, Separator) && Parse.TryFloat(kvp.Key, out var f1) && Parse.TryFloat(kvp.Value, out var f2) ? UnityEngine.Random.Range(f1, f2).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "randi" => Parse.TryKvp(value, out var kvp, Separator) && Parse.TryInt(kvp.Key, out var i1) && Parse.TryInt(kvp.Value, out var i2) ? UnityEngine.Random.Range(i1, i2).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "randomfloat" => Parse.TryKvp(value, out var kvp, Separator) && Parse.TryFloat(kvp.Key, out var f1) && Parse.TryFloat(kvp.Value, out var f2) ? UnityEngine.Random.Range(f1, f2).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "randomint" => Parse.TryKvp(value, out var kvp, Separator) && Parse.TryInt(kvp.Key, out var i1) && Parse.TryInt(kvp.Value, out var i2) ? UnityEngine.Random.Range(i1, i2).ToString(CultureInfo.InvariantCulture) : defaultValue,
     "hashof" => ZdoHelper.Hash(value).ToString(),
     "textof" => Parse.TryInt(value, out var hash) ? ZdoHelper.ReverseHash(hash) : defaultValue,
     "len" => value.Length.ToString(CultureInfo.InvariantCulture),
     "lower" => value.ToLowerInvariant(),
     "upper" => value.ToUpperInvariant(),
     "trim" => value.Trim(),
     "left" => HandleLeft(value, defaultValue),
     "right" => HandleRight(value, defaultValue),
     "mid" => HandleMid(value, defaultValue),
     "proper" => HandleProper(value, defaultValue),
     "search" => HandleSearch(value, defaultValue),
     "calcf" => Calculator.EvaluateFloat(value)?.ToString(CultureInfo.InvariantCulture) ?? defaultValue,
     "calci" => Calculator.EvaluateInt(value)?.ToString(CultureInfo.InvariantCulture) ?? defaultValue,
     "calcfloat" => Calculator.EvaluateFloat(value)?.ToString(CultureInfo.InvariantCulture) ?? defaultValue,
     "calcint" => Calculator.EvaluateInt(value)?.ToString(CultureInfo.InvariantCulture) ?? defaultValue,
     "calclong" => Calculator.EvaluateLong(value)?.ToString(CultureInfo.InvariantCulture) ?? defaultValue,
     "par" => Parse.TryInt(value, out var i) ? GetArg(i, defaultValue) : defaultValue,
     "rest" => Parse.TryInt(value, out var i) ? GetRest(i, defaultValue) : defaultValue,
     "load" => DataStorage.GetValue(value, defaultValue),
     "save" => SetValue(value),
     "save++" => DataStorage.IncrementValue(value, 1),
     "save--" => DataStorage.IncrementValue(value, -1),
     "clear" => RemoveValue(value),
     "rank" => HandleRank(value, defaultValue),
     "small" => HandleSmall(value, defaultValue),
     "large" => HandleLarge(value, defaultValue),
     "eq" => HandleEqual(value, defaultValue),
     "ne" => HandleNotEqual(value, defaultValue),
     "gt" => HandleGreater(value, defaultValue),
     "ge" => HandleGreaterOrEqual(value, defaultValue),
     "lt" => HandleLess(value, defaultValue),
     "le" => HandleLessOrEqual(value, defaultValue),
     "even" => HandleEven(value, defaultValue),
     "odd" => HandleOdd(value, defaultValue),
     "findupper" => HandleFindUpper(value, defaultValue),
     "findlower" => HandleFindLower(value, defaultValue),
     _ => null,
   };

  private string HandleMin(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;
    return values.Select(v => Parse.Float(v, float.MaxValue)).Min().ToString(CultureInfo.InvariantCulture);
  }
  private string HandleMax(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;
    return values.Select(v => Parse.Float(v, float.MinValue)).Max().ToString(CultureInfo.InvariantCulture);
  }

  private string SetValue(string value)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (kvp.Value == "") return "";
    DataStorage.SetValue(kvp.Key, kvp.Value);
    return kvp.Value;
  }
  private string RemoveValue(string value)
  {
    DataStorage.SetValue(value, "");
    return "";
  }
  private string GetRest(int index, string defaultValue = "")
  {
    if (index < 0 || index >= args.Length) return defaultValue;
    return string.Join(" ", args, index, args.Length - index);
  }

  private string Atan(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (!Parse.TryFloat(kvp.Key, out var f1)) return defaultValue;
    if (kvp.Value == "") return Mathf.Atan(f1).ToString(CultureInfo.InvariantCulture);
    if (!Parse.TryFloat(kvp.Value, out var f2)) return defaultValue;
    return Mathf.Atan2(f1, f2).ToString(CultureInfo.InvariantCulture);
  }

  private string Loga(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (!Parse.TryFloat(kvp.Key, out var f1)) return defaultValue;
    if (kvp.Value == "") return Mathf.Log(f1).ToString(CultureInfo.InvariantCulture);
    if (!Parse.TryFloat(kvp.Value, out var f2)) return defaultValue;
    return Mathf.Log(f1, f2).ToString(CultureInfo.InvariantCulture);
  }

  private string HandleAdd(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    float result = 0f;
    foreach (var val in values)
    {
      result += Parse.Float(val, 0f);
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleSub(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    float result = Parse.Float(values[0], 0f);
    for (int i = 1; i < values.Length; i++)
    {
      result -= Parse.Float(values[i], 0f);
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleMul(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    float result = 1f;
    foreach (var val in values)
    {
      result *= Parse.Float(val, 1f);
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleDiv(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    float result = Parse.Float(values[0], 0f);
    for (int i = 1; i < values.Length; i++)
    {
      var divisor = Parse.Float(values[i], 1f);
      if (divisor == 0f) return defaultValue;
      result /= divisor;
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleMod(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    float result = Parse.Float(values[0], 0f);
    for (int i = 1; i < values.Length; i++)
    {
      var divisor = Parse.Float(values[i], 1f);
      if (divisor == 0f) return defaultValue;
      result %= divisor;
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleAddLong(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    long result = 0L;
    foreach (var val in values)
    {
      result += Parse.Long(val, 0L);
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleSubLong(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    long result = Parse.Long(values[0], 0L);
    for (int i = 1; i < values.Length; i++)
    {
      result -= Parse.Long(values[i], 0L);
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleMulLong(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    long result = 1L;
    foreach (var val in values)
    {
      result *= Parse.Long(val, 1L);
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleDivLong(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    long result = Parse.Long(values[0], 0L);
    for (int i = 1; i < values.Length; i++)
    {
      var divisor = Parse.Long(values[i], 1L);
      if (divisor == 0L) return defaultValue;
      result /= divisor;
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleModLong(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length == 0) return defaultValue;

    long result = Parse.Long(values[0], 0L);
    for (int i = 1; i < values.Length; i++)
    {
      var divisor = Parse.Long(values[i], 1L);
      if (divisor == 0L) return defaultValue;
      result %= divisor;
    }
    return result.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleLeft(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    var text = kvp.Key;
    var numChars = Parse.Int(kvp.Value, 1);

    if (text.Length == 0) return defaultValue;
    if (numChars <= 0) return "";
    if (numChars >= text.Length) return text;

    return text.Substring(0, numChars);
  }

  private string HandleRight(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    var text = kvp.Key;
    var numChars = Parse.Int(kvp.Value, 1);

    if (text.Length == 0) return defaultValue;
    if (numChars <= 0) return "";
    if (numChars >= text.Length) return text;

    return text.Substring(text.Length - numChars);
  }

  private string HandleMid(string value, string defaultValue)
  {
    var parts = value.Split(Separator);
    if (parts.Length < 3) return defaultValue;

    var text = parts[0];
    if (!Parse.TryInt(parts[1], out var startNum) || !Parse.TryInt(parts[2], out var numChars))
      return defaultValue;

    if (text.Length == 0 || startNum >= text.Length || numChars <= 0)
      return "";

    var endPos = Math.Min(startNum + numChars, text.Length);
    return text.Substring(startNum, endPos - startNum);
  }

  private string HandleProper(string value, string defaultValue)
  {
    if (string.IsNullOrEmpty(value)) return defaultValue;

    var words = value.Split(' ');
    for (int i = 0; i < words.Length; i++)
    {
      if (words[i].Length > 0)
      {
        words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1).ToLower() : "");
      }
    }
    return string.Join(" ", words);
  }

  private string HandleSearch(string value, string defaultValue)
  {
    var parts = value.Split(Separator);
    if (parts.Length < 2) return defaultValue;

    var findText = parts[0];
    var withinText = parts[1];
    var startNum = parts.Length >= 3 ? Parse.Int(parts[2], 0) : 0;

    if (startNum >= withinText.Length) return defaultValue;

    var index = withinText.IndexOf(findText, startNum, StringComparison.OrdinalIgnoreCase);
    return index >= 0 ? index.ToString() : defaultValue;
  }

  private string GetArg(int index, string defaultValue = "")
  {
    return args.Length <= index || args[index] == "" ? defaultValue : args[index];
  }

  private string HandleRank(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length < 2) return defaultValue;

    if (!Parse.TryFloat(values[0], out var numberToRank)) return defaultValue;

    var numbers = values.Skip(1).Select(v => Parse.Float(v, float.MaxValue)).ToList();

    // Count how many numbers are greater than the number to rank
    int rank = 0;
    foreach (var num in numbers)
    {
      if (num > numberToRank)
        rank++;
    }

    return rank.ToString(CultureInfo.InvariantCulture);
  }

  private string HandleSmall(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length < 2) return defaultValue;

    if (!Parse.TryInt(values[0], out var index) || index < 1) return defaultValue;
    index -= 1; // Convert to 0-indexed

    var numbers = values.Skip(1).Select(v => Parse.Float(v, float.MaxValue)).ToList();
    numbers.Sort();
    return numbers[index].ToString(CultureInfo.InvariantCulture);
  }

  private string HandleLarge(string value, string defaultValue)
  {
    var values = value.Split(Separator);
    if (values.Length < 2) return defaultValue;

    if (!Parse.TryInt(values[0], out var index) || index < 1) return defaultValue;
    index = values.Length - index; // Convert to 0-indexed for largest
    var numbers = values.Skip(1).Select(v => Parse.Float(v, float.MinValue)).ToList();
    numbers.Sort();
    return numbers[index].ToString(CultureInfo.InvariantCulture);
  }

  private string HandleEqual(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (kvp.Value == "") return defaultValue;

    // Try numeric comparison first
    if (Parse.TryFloat(kvp.Key, out var f1) && Parse.TryFloat(kvp.Value, out var f2))
      return (Math.Abs(f1 - f2) < float.Epsilon) ? "true" : "false";

    // Fall back to string comparison
    return string.Equals(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase) ? "true" : "false";
  }

  private string HandleNotEqual(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (kvp.Value == "") return defaultValue;

    // Try numeric comparison first
    if (Parse.TryFloat(kvp.Key, out var f1) && Parse.TryFloat(kvp.Value, out var f2))
      return (Math.Abs(f1 - f2) >= float.Epsilon) ? "true" : "false";

    // Fall back to string comparison
    return !string.Equals(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase) ? "true" : "false";
  }

  private string HandleGreater(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (kvp.Value == "" || !Parse.TryFloat(kvp.Key, out var f1) || !Parse.TryFloat(kvp.Value, out var f2))
      return defaultValue;

    return (f1 > f2) ? "true" : "false";
  }

  private string HandleGreaterOrEqual(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (kvp.Value == "" || !Parse.TryFloat(kvp.Key, out var f1) || !Parse.TryFloat(kvp.Value, out var f2))
      return defaultValue;

    return (f1 >= f2) ? "true" : "false";
  }

  private string HandleLess(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (kvp.Value == "" || !Parse.TryFloat(kvp.Key, out var f1) || !Parse.TryFloat(kvp.Value, out var f2))
      return defaultValue;

    return (f1 < f2) ? "true" : "false";
  }

  private string HandleLessOrEqual(string value, string defaultValue)
  {
    var kvp = Parse.Kvp(value, Separator);
    if (kvp.Value == "" || !Parse.TryFloat(kvp.Key, out var f1) || !Parse.TryFloat(kvp.Value, out var f2))
      return defaultValue;

    return (f1 <= f2) ? "true" : "false";
  }

  private string HandleEven(string value, string defaultValue)
  {
    if (!Parse.TryInt(value, out var number))
      return defaultValue;

    return (number % 2 == 0) ? "true" : "false";
  }

  private string HandleOdd(string value, string defaultValue)
  {
    if (!Parse.TryInt(value, out var number))
      return defaultValue;

    return (number % 2 != 0) ? "true" : "false";
  }

  private string HandleFindUpper(string value, string defaultValue)
  {
    if (string.IsNullOrEmpty(value)) return defaultValue;
    return new string([.. value.Where(char.IsUpper)]);
  }

  private string HandleFindLower(string value, string defaultValue)
  {
    if (string.IsNullOrEmpty(value)) return defaultValue;
    return new string([.. value.Where(char.IsLower)]);
  }

  // Parameter value could be a value group, so that has to be resolved.
  private static string ResolveValue(string value)
  {
    if (!value.StartsWith("<", StringComparison.OrdinalIgnoreCase)) return value;
    if (!value.EndsWith(">", StringComparison.OrdinalIgnoreCase)) return value;
    var sub = value.Substring(1, value.Length - 2);
    if (TryGetValueFromGroup(sub, out var valueFromGroup))
      return valueFromGroup;
    return value;
  }

  private static bool TryGetValueFromGroup(string group, out string value)
  {
    var hash = group.ToLowerInvariant().GetStableHashCode();
    if (!DataLoading.ValueGroups.ContainsKey(hash))
    {
      value = group;
      return false;
    }
    var roll = UnityEngine.Random.Range(0, DataLoading.ValueGroups[hash].Count);
    // Value from group could be another group, so yet another resolve is needed.
    value = ResolveValue(DataLoading.ValueGroups[hash][roll]);
    return true;
  }
}
public class ObjectParameters(string prefab, string[] args, ZDO zdo) : Parameters(prefab, args, zdo.m_position)
{
  private Inventory? inventory;


  protected override string? GetParameter(string key, string defaultValue)
  {
    var value = base.GetParameter(key, defaultValue);
    if (value != null) return value;
    value = GetGeneralParameter(key);
    if (value != null) return value;
    var keyArg = Parse.Kvp(key, Separator);
    if (keyArg.Value == "") return null;
    key = keyArg.Key;
    var arg = keyArg.Value;
    value = ExecuteCodeWithValue(key, arg);
    if (value != null) return value;
    value = base.GetValueParameter(key, arg, defaultValue);
    if (value != null) return value;
    return GetValueParameter(key, arg, defaultValue);
  }

  private string? GetGeneralParameter(string key) =>
    key switch
    {
      "zdo" => zdo.m_uid.ToString(),
      "pos" => Helper.FormatPos(zdo.m_position),
      "i" => ZoneSystem.GetZone(zdo.m_position).x.ToString(),
      "j" => ZoneSystem.GetZone(zdo.m_position).y.ToString(),
      "a" => Helper.Format(zdo.m_rotation.y),
      "rot" => Helper.FormatRot(zdo.m_rotation),
      "pid" => GetPid(zdo),
      "pname" => GetPname(zdo),
      "pchar" => GetPchar(zdo),
      "owner" => zdo.GetOwner().ToString(),
      "biome" => WorldGenerator.instance.GetBiome(zdo.m_position).ToString(),
      _ => null,
    };

  private static string GetPid(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_rpc.GetSocket().GetHostName();
    else if (Player.m_localPlayer)
      return "Server";
    return "";
  }
  private static string GetPname(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_playerName;
    else if (Player.m_localPlayer)
      return Player.m_localPlayer.GetPlayerName();
    return "";
  }
  private static string GetPchar(ZDO zdo)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_characterID.ToString();
    else if (Player.m_localPlayer)
      return Player.m_localPlayer.GetPlayerID().ToString();
    return "";
  }
  private static ZNetPeer? GetPeer(ZDO zdo) => zdo.GetOwner() != 0 ? ZNet.instance.GetPeer(zdo.GetOwner()) : null;


  protected override string? GetValueParameter(string key, string value, string defaultValue) =>
   key switch
   {
     "key" => DataHelper.GetGlobalKey(value),
     "string" => GetString(value, defaultValue),
     "float" => GetFloat(value, defaultValue).ToString(CultureInfo.InvariantCulture),
     "int" => GetInt(value, defaultValue).ToString(CultureInfo.InvariantCulture),
     "long" => GetLong(value, defaultValue).ToString(CultureInfo.InvariantCulture),
     "bool" => GetBool(value, defaultValue) ? "true" : "false",
     "hash" => GetHash(value, defaultValue),
     "vec" => DataEntry.PrintVectorXZY(GetVec(value, defaultValue)),
     "quat" => DataEntry.PrintAngleYXZ(GetQuaternion(value, defaultValue)),
     "byte" => GetBytes(value, defaultValue),
     "zdo" => zdo.GetZDOID(value).ToString(),
     "amount" => GetAmount(value, defaultValue),
     "quality" => GetQuality(value, defaultValue),
     "durability" => GetDurability(value, defaultValue),
     "item" => GetItem(value, defaultValue),
     "pos" => DataEntry.PrintVectorXZY(GetPos(value)),
     "pdata" => GetPlayerData(zdo, value),
     _ => null,
   };


  private string GetBytes(string value, string defaultValue)
  {
    var bytes = zdo.GetByteArray(value);
    return bytes == null ? defaultValue : Convert.ToBase64String(bytes);
  }
  private string GetString(string value, string defaultValue) => ZdoHelper.GetString(zdo, value, defaultValue);
  private float GetFloat(string value, string defaultValue) => ZdoHelper.GetFloat(zdo, value, defaultValue);
  private int GetInt(string value, string defaultValue) => ZdoHelper.GetInt(zdo, value, defaultValue);
  private long GetLong(string value, string defaultValue) => ZdoHelper.GetLong(zdo, value, defaultValue);
  private bool GetBool(string value, string defaultValue) => ZdoHelper.GetBool(zdo, value, defaultValue);
  private string GetHash(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var zdoValue = zdo.GetInt(value);
    return ZNetScene.instance.GetPrefab(zdoValue)?.name ?? ZoneSystem.instance.GetLocation(zdoValue)?.m_prefabName ?? defaultValue;
  }
  private Vector3 GetVec(string value, string defaultValue) => ZdoHelper.GetVec(zdo, value, defaultValue);
  private Quaternion GetQuaternion(string value, string defaultValue) => ZdoHelper.GetQuaternion(zdo, value, defaultValue);
  private string GetItem(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var kvp = Parse.Kvp(value, Separator);
    // Coordinates requires two numbers, otherwise it's an item name.
    if (!Parse.TryInt(kvp.Key, out var x) || !Parse.TryInt(kvp.Value, out var y)) return GetAmountOfItems(value).ToString();
    return GetNameAt(x, y) ?? defaultValue;
  }
  private string GetAmount(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var kvp = Parse.Kvp(value, Separator);
    // Coordinates requires two numbers, otherwise it's an item name.
    if (!Parse.TryInt(kvp.Key, out var x) || !Parse.TryInt(kvp.Value, out var y)) return GetAmountOfItems(value).ToString();
    return GetAmountAt(x, y) ?? defaultValue;
  }
  private string GetDurability(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var kvp = Parse.Kvp(value, Separator);
    // Coordinates requires two numbers, otherwise it's an item name.
    if (!Parse.TryInt(kvp.Key, out var x) || !Parse.TryInt(kvp.Value, out var y)) return defaultValue;
    return GetDurabilityAt(x, y) ?? defaultValue;
  }
  private string GetQuality(string value, string defaultValue)
  {
    if (value == "") return defaultValue;
    var kvp = Parse.Kvp(value, Separator);
    // Coordinates requires two numbers, otherwise it's an item name.
    if (!Parse.TryInt(kvp.Key, out var x) || !Parse.TryInt(kvp.Value, out var y)) return defaultValue;
    return GetQualityAt(x, y) ?? defaultValue;
  }
  private int GetAmountOfItems(string prefab)
  {
    LoadInventory();
    if (inventory == null) return 0;
    if (prefab == "") return inventory.m_inventory.Sum(i => i.m_stack);
    if (prefab == "*") return inventory.m_inventory.Sum(i => i.m_stack);
    int count = 0;
    if (prefab[0] == '*' && prefab[prefab.Length - 1] == '*')
    {
      prefab = prefab.Substring(1, prefab.Length - 2).ToLowerInvariant();
      foreach (var item in inventory.m_inventory)
      {
        if (GetName(item).ToLowerInvariant().Contains(prefab)) count += item.m_stack;
      }
    }
    else if (prefab[0] == '*')
    {
      prefab = prefab.Substring(1);
      foreach (var item in inventory.m_inventory)
      {
        if (GetName(item).EndsWith(prefab, StringComparison.OrdinalIgnoreCase)) count += item.m_stack;
      }
    }
    else if (prefab[prefab.Length - 1] == '*')
    {
      prefab = prefab.Substring(0, prefab.Length - 1);
      foreach (var item in inventory.m_inventory)
      {
        if (GetName(item).StartsWith(prefab, StringComparison.OrdinalIgnoreCase)) count += item.m_stack;
      }
    }
    else
    {
      var wildIndex = prefab.IndexOf('*');
      if (wildIndex > 0 && wildIndex < prefab.Length - 1)
      {
        var prefix = prefab.Substring(0, wildIndex);
        var suffix = prefab.Substring(wildIndex + 1);
        foreach (var item in inventory.m_inventory)
        {
          var name = GetName(item);
          if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
              name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            count += item.m_stack;
        }
      }
      else
      {
        foreach (var item in inventory.m_inventory)
        {
          if (GetName(item) == prefab) count += item.m_stack;
        }
      }

    }
    return count;
  }
  private string GetName(ItemDrop.ItemData? item) => item?.m_dropPrefab?.name ?? item?.m_shared.m_name ?? "";
  private string? GetNameAt(int x, int y)
  {
    var item = GetItemAt(x, y);
    return GetName(item);
  }
  private string? GetAmountAt(int x, int y) => GetItemAt(x, y)?.m_stack.ToString();
  private string? GetDurabilityAt(int x, int y) => GetItemAt(x, y)?.m_durability.ToString();
  private string? GetQualityAt(int x, int y) => GetItemAt(x, y)?.m_quality.ToString();
  private ItemDrop.ItemData? GetItemAt(int x, int y)
  {
    LoadInventory();
    if (inventory == null) return null;
    if (x < 0 || x >= inventory.m_width || y < 0 || y >= inventory.m_height) return null;
    return inventory.GetItemAt(x, y);
  }


  private void LoadInventory()
  {
    if (inventory != null) return;
    var currentItems = zdo.GetString(ZDOVars.s_items);
    if (currentItems == "") return;
    inventory = new("", null, 9999, 9999);
    inventory.Load(new ZPackage(currentItems));
  }

  private Vector3 GetPos(string value)
  {
    var offset = Parse.VectorXZY(value);
    return zdo.GetPosition() + zdo.GetRotation() * offset;
  }

  public static string GetPlayerData(ZDO zdo, string key)
  {
    var peer = GetPeer(zdo);
    if (peer != null)
      return peer.m_serverSyncedPlayerData.TryGetValue(key, out var data) ? data : "";
    else if (Player.m_localPlayer)
      return ZNet.instance.m_serverSyncedPlayerData.TryGetValue(key, out var data) ? data : "";
    return "";
  }
}
