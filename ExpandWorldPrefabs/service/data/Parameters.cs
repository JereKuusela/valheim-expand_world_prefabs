using System;
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

  public int Amount = 0;
  public string Replace(string str) => Replace(str, false);
  public string Replace(string str, bool preventInjections)
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
          var resolved = ResolveParameters(key);
          // Server Devcommands mod supports running commands separated by ';'.
          // This allows injection attacks when players can control parameter values.
          // For example with player name, chat messages or sign texts.
          if (preventInjections && resolved.Contains(";"))
            resolved = resolved.Replace(";", ",");
          parts.Append(resolved);
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
    var value = Api.ResolveParameter(key);
    if (value != null) return value;
    value = ExecuteCode(key);
    if (value != null) return value;
    value = GetGeneralParameter(key, defaultValue);
    if (value != null) return value;
    var keyArg = Parse.Kvp(key, Separator);
    if (keyArg.Value == "") return null;
    key = keyArg.Key;
    var arg = keyArg.Value;

    value = Api.ResolveValueParameter(key, arg);
    if (value != null) return value;
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
      "day" => EnvMan.instance.GetDay(time).ToString(),
      "ticks" => ((long)(time * 10000000.0)).ToString(),
      "x" => Helper.Format(pos.x),
      "y" => Helper.Format(pos.y),
      "z" => Helper.Format(pos.z),
      "snap" => Helper.Format(WorldGenerator.instance.GetHeight(pos.x, pos.z)),
      // Need arg check to avoid conflict with value operations.
      "amount" => args.Length < 2 ? Amount.ToString() : null,
      "time" => Helper.Format(time),
      "realtime" => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
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
     "time" => HandleTime(value),
     "realtime" => HandleRealtime(value),
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

  private string HandleTime(string value)
  {
    var format = value;
    var dayLength = EnvMan.instance.m_dayLengthSec;
    var hourLength = dayLength / 24.0;
    var minuteLength = hourLength / 60.0;
    var day = (int)(time / dayLength);
    var hours = time - (day * dayLength);
    var hour = (int)(hours / hourLength);
    var minute = (int)((hours - (hour * hourLength)) / minuteLength);
    var second = (int)((hours - (hour * hourLength) - (minute * minuteLength)) / (minuteLength / 60.0));

    // Create a DateTimeOffset representing the game time
    var dt = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)
      .AddDays(day)
      .AddHours(hour)
      .AddMinutes(minute)
      .AddSeconds(second);

    return dt.ToString(format, CultureInfo.InvariantCulture);
  }
  private string HandleRealtime(string value)
  {
    var parts = value.Split(Separator);
    var format = parts[0];
    var timezoneOffset = parts.Length > 1 ? Parse.Float(parts[1], 0f) : (float)TimeZoneInfo.Local.BaseUtcOffset.TotalHours;
    var utcNow = DateTimeOffset.UtcNow;
    var offsetTime = utcNow.AddHours(timezoneOffset);
    return offsetTime.ToString(format, CultureInfo.InvariantCulture);
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
