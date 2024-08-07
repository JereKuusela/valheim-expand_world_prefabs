using System.Globalization;
using UnityEngine;

namespace Data;

public class IntValue(string[] values) : AnyValue(values), IIntValue
{
  public int? Get(Parameters pars)
  {
    var value = GetValue(pars);
    if (value == null)
      return null;
    if (!value.Contains(";"))
      return Calculator.EvaluateInt(value);
    // Format for range is "start;end;step;statement".
    var split = value.Split(';');
    if (split.Length < 2)
      throw new System.InvalidOperationException($"Invalid range format: {value}");
    var min = Calculator.EvaluateInt(split[0]);
    var max = Calculator.EvaluateInt(split[1]);
    if (min == null || max == null)
      return null;
    int? roll;
    if (split.Length < 3 || split[2] == "")
      roll = Random.Range(min.Value, max.Value + 1);
    else
    {
      var step = Calculator.EvaluateInt(split[2]);
      if (step == null)
        roll = Random.Range(min.Value, max.Value + 1);
      else
      {
        var steps = (max - min) / step;
        var rollStep = Random.Range(0, steps.Value + 1);
        roll = min + rollStep * step;
      }
    }
    if (split.Length < 4)
      return roll;
    return Calculator.EvaluateInt(split[3].Replace("<value>", roll.ToString()));
  }
  public bool? Match(Parameters pars, int value)
  {
    // If all values are null, default to a match.
    var allNull = true;
    foreach (var rawValue in Values)
    {
      var v = pars.Replace(rawValue);
      // Case 1: Simple value.
      if (!v.Contains(";"))
      {
        var parsed = Calculator.EvaluateInt(v);
        if (parsed == null) continue;
        allNull = false;
        if (parsed.Value == value)
          return true;
        continue;
      }
      var split = v.Split(';');
      if (split.Length < 2)
        throw new System.InvalidOperationException($"Invalid range format: {v}");
      var min = Calculator.EvaluateInt(split[0]);
      var max = Calculator.EvaluateInt(split[1]);
      if (min == null || max == null)
        continue;
      // Case 2: Range.
      if (split.Length < 3)
      {
        allNull = false;
        if (value >= min.Value && value <= max.Value)
          return true;
      }
      // Case 3: Range with step.
      else if (split.Length < 4)
      {
        var step = Calculator.EvaluateInt(split[2]);
        if (step == null)
          continue;
        allNull = false;
        var steps = (max.Value - min.Value) / step.Value;
        for (var i = 0; i <= steps; ++i)
        {
          var roll = min.Value + i * step.Value;
          if (roll == value)
            return true;
        }
      }
      else
      {
        // Case 4: Range with statement.
        if (split[2] == "")
        {
          var minValue = Calculator.EvaluateInt(split[3].Replace("<value>", min?.ToString(CultureInfo.InvariantCulture)));
          var maxValue = Calculator.EvaluateInt(split[3].Replace("<value>", max?.ToString(CultureInfo.InvariantCulture)));
          if (minValue == null || maxValue == null)
            continue;
          allNull = false;
          if (value >= minValue.Value && value <= maxValue.Value)
            return true;
        }
        else
        {
          // Case 5: Range with step and statement.
          var step = Calculator.EvaluateInt(split[2]);
          if (step == null)
            continue;
          allNull = false;
          var steps = (max.Value - min.Value) / step.Value;
          for (var i = 0; i <= steps; ++i)
          {
            var roll = min + i * step;
            var parsed = Calculator.EvaluateInt(split[3].Replace("<value>", roll?.ToString(CultureInfo.InvariantCulture)));
            if (parsed == null) continue;
            if (parsed.Value == value)
              return true;
          }
        }
      }
    }
    return allNull ? null : false;
  }
}

public class SimpleIntValue(int value) : IIntValue
{
  private readonly int Value = value;
  public int? Get(Parameters pars) => Value;
  public bool? Match(Parameters pars, int value) => Value == value;
}
public interface IIntValue
{
  int? Get(Parameters pars);
  bool? Match(Parameters pars, int value);
}