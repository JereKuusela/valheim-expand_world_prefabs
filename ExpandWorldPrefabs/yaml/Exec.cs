using System.ComponentModel;
using Data;
using Service;

namespace ExpandWorld.Prefab;

public class Exec
{
  private readonly IStringValue? Function;
  private readonly string? Command;
  private readonly ConditionClause? Condition;
  public IFloatValue? Weight;
  public IFloatValue? Chance;
  public IFloatValue? Delay;
  public IIntValue? Repeat;
  public IFloatValue? RepeatInterval;
  public IFloatValue? RepeatChance;

  public Exec(ExecYaml data)
  {
    Function = data.function == null ? null : DataValue.String(data.function);
    Command = data.command;
    Weight = data.weight == null ? null : DataValue.Float(data.weight);
    Chance = data.chance == null ? null : DataValue.Float(data.chance);
    Delay = data.delay == null ? null : DataValue.Float(data.delay);
    Repeat = data.repeat == null ? null : DataValue.Int(data.repeat);
    RepeatInterval = data.repeatInterval == null ? null : DataValue.Float(data.repeatInterval);
    RepeatChance = data.repeatChance == null ? null : DataValue.Float(data.repeatChance);
    if (data.condition != null)
    {
      if (Conditions.TryParse(data.condition, out var condition, out var error))
        Condition = condition;
      else
      {
        Log.Warning($"Invalid exec condition '{data.condition}': {error}");
        Condition = Conditions.False(data.condition);
      }
    }
  }

  public void Run(Functions f)
  {
    if (Condition != null && !Condition.Evaluate(f)) return;
    Function?.Get(f);
    if (Command != null)
      Commands.Run([f.Replace(Command, true, false)]);
  }
}

public class ExecYaml
{
  [DefaultValue(null)]
  public string? function;
  [DefaultValue(null)]
  public string? command;
  [DefaultValue(null)]
  public string? weight;
  [DefaultValue(null)]
  public string? chance;
  [DefaultValue(null)]
  public string? condition;
  [DefaultValue(null)]
  public string? delay;
  [DefaultValue(null)]
  public string? repeat;
  [DefaultValue(null)]
  public string? repeatInterval;
  [DefaultValue(null)]
  public string? repeatChance;

  public static ExecYaml FromLine(string line) =>
    line.TrimStart().StartsWith("<") ? new ExecYaml { function = line } : new ExecYaml { command = line };
}
