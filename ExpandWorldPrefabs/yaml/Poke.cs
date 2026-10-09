using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Common;
using Data;

namespace ExpandWorld.Prefab;

public class Poke(PokeYaml data)
{
  public Object Filter = new(data);
  private readonly string? Parameter = data.parameter;
  private readonly string[]? Parameters = data.pars == null ? null : Parse.ToArr(data.pars);
  public IIntValue? Limit = data.limit == null ? null : DataValue.Int(data.limit);
  public IBoolValue? Random = data.random == null ? null : DataValue.Bool(data.random);
  public IFloatValue? Delay = data.delay == null ? null : DataValue.Float(data.delay);
  public IFloatValue? Weight = data.weight == null ? null : DataValue.Float(data.weight);
  public IIntValue? Repeat = data.repeat == null ? null : DataValue.Int(data.repeat);
  public IFloatValue? RepeatInterval = data.repeatInterval == null ? null : DataValue.Float(data.repeatInterval);
  public IFloatValue? RepeatChance = data.repeatChance == null ? null : DataValue.Float(data.repeatChance);
  public IFloatValue? Chance = data.chance == null ? null : DataValue.Float(data.chance);
  public IBoolValue? Connected = data.connected == null ? null : DataValue.Bool(data.connected);
  public IZdoIdValue? Target = data.target == null ? null : DataValue.ZdoId(data.target);
  private readonly IBoolValue? Evaluate = data.evaluate == null ? null : DataValue.Bool(data.evaluate);
  public bool HasPrefab = !string.IsNullOrWhiteSpace(data.prefab);

  public string[] GetArgs(Functions f)
  {
    if (Parameters != null)
      return [.. Parameters.Select(f.Replace)];
    else
    {
      var pokeParameter = f.Replace(Parameter ?? "");
      if (Evaluate?.GetBool(f) != false)
        pokeParameter = PokeEvaluate(pokeParameter);
      return pokeParameter.Split(' ');
    }
  }


  public static string PokeEvaluate(string str)
  {
    var expressions = str.Split(' ').ToArray();
    bool changed = false;
    for (var i = 0; i < expressions.Length; ++i)
    {
      var expression = expressions[i];
      if (expression.Length == 0) continue;
      // Single negative number would get handled as expression.
      var sub = expression.Substring(1);
      if (!sub.Contains('*') && !sub.Contains('/') && !sub.Contains('+') && !sub.Contains('-')) continue;
      changed = true;
      var value = Calculator.EvaluateFloat(expression);
      if (value.HasValue)
        expressions[i] = value.Value.ToString("0.#####", NumberFormatInfo.InvariantInfo);
    }
    return changed ? string.Join(" ", expressions) : str;
  }
}

public class PokeYaml : ObjectYaml
{
  [DefaultValue(null)]
  public string? delay;
  [DefaultValue(null)]
  public string? repeat;
  [DefaultValue(null)]
  public string? repeatInterval;
  [DefaultValue(null)]
  public string? repeatChance;
  [DefaultValue(null)]
  public string? chance;
  [DefaultValue(null)]
  public string? connected;
  [DefaultValue(null)]
  public string? target;
  [DefaultValue(null)]
  public string? parameter;
  [DefaultValue(null)]
  public string? pars;
  [DefaultValue(null)]
  public string? limit;
  [DefaultValue(null)]
  public string? random;
  [DefaultValue(null)]
  public string? evaluate;
}
