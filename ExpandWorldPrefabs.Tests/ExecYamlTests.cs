using System.Linq;
using ExpandWorld.Prefab;
using NUnit.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExpandWorldPrefabs.Tests;

public class ExecYamlTests
{
  private const string NL = "\n";

  private static ExecYaml[] Parse(string yaml) =>
    new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance)
      .WithTypeConverter(new FlexibleYamlConverter()).Build().Deserialize<RuleYaml>(yaml).exec!.Data;

  [Test]
  public void SingleLine_StartingWithAngleBracketIsFunction()
  {
    var entries = Parse("exec: <save_a_1>");
    Assert.That(entries, Has.Length.EqualTo(1));
    Assert.That(entries[0].function, Is.EqualTo("<save_a_1>"));
    Assert.That(entries[0].command, Is.Null);
  }

  [Test]
  public void SingleLine_OtherTextIsCommand()
  {
    var entries = Parse("exec: say hello <name>");
    Assert.That(entries, Has.Length.EqualTo(1));
    Assert.That(entries[0].command, Is.EqualTo("say hello <name>"));
    Assert.That(entries[0].function, Is.Null);
  }

  [Test]
  public void List_AcceptsLinesAndObjects()
  {
    var entries = Parse("exec:" + NL + "- <save_a_1>" + NL + "- say hello" + NL + "- command: say later" + NL + "  function: <save_b_1>" + NL + "  delay: 2" + NL + "  condition: <key_a> != 1" + NL);
    Assert.That(entries, Has.Length.EqualTo(3));
    Assert.That(entries[0].function, Is.EqualTo("<save_a_1>"));
    Assert.That(entries[1].command, Is.EqualTo("say hello"));
    Assert.That(entries[2].command, Is.EqualTo("say later"));
    Assert.That(entries[2].function, Is.EqualTo("<save_b_1>"));
    Assert.That(entries[2].delay, Is.EqualTo("2"));
    Assert.That(entries[2].condition, Is.EqualTo("<key_a> != 1"));
  }

  [Test]
  public void Exec_ReadsTimingAndWeightFields()
  {
    var exec = new Exec(new ExecYaml { function = "<save_a_1>", weight = "2", chance = "0.5", delay = "1", repeat = "3", repeatInterval = "4", repeatChance = "0.9" });
    Assert.That(new object?[] { exec.Weight, exec.Chance, exec.Delay, exec.Repeat, exec.RepeatInterval, exec.RepeatChance }.All(v => v != null), Is.True);
  }

  [Test]
  public void Exec_PlainEntryHasNoTimingOrWeight()
  {
    var exec = new Exec(new ExecYaml { command = "say hello" });
    Assert.That(new object?[] { exec.Weight, exec.Chance, exec.Delay, exec.Repeat }.All(v => v == null), Is.True);
  }
}
