using ExpandWorld.Prefab;
using NUnit.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExpandWorldPrefabs.Tests;

public class InlineDataTests
{
  private const string NL = "\n";

  private static RuleYaml Parse(string yaml) =>
    new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance)
      .WithTypeConverter(new FlexibleYamlConverter()).Build().Deserialize<RuleYaml>(yaml);

  [Test]
  public void Data_ScalarIsName()
  {
    var rule = Parse("data: my_data");
    Assert.That(rule.data!.Name, Is.EqualTo("my_data"));
    Assert.That(rule.data.Inline, Is.Null);
  }

  [Test]
  public void Data_MappingIsInline()
  {
    var rule = Parse("data:" + NL + "  bools:" + NL + "    - WearNTear.m_noRoofWear, true");
    Assert.That(rule.data!.Name, Is.Null);
    Assert.That(rule.data.Inline!.bools, Is.EqualTo(new[] { "WearNTear.m_noRoofWear, true" }));
  }

  [Test]
  public void Hoist_ReplacesInlineWithGeneratedName()
  {
    var rule = Parse(
      "data:" + NL + "  floats:" + NL + "    - a, 1" + NL +
      "spawn:" + NL + "  - prefab: x" + NL + "    data:" + NL + "      ints:" + NL + "        - b, 2" + NL +
      "poke:" + NL + "  - prefab: y" + NL + "    data: named");
    var hoisted = DataField.Hoist([rule], "test.yaml");
    Assert.That(hoisted, Has.Count.EqualTo(2));
    Assert.That(rule.data!.Inline, Is.Null);
    Assert.That(rule.data.Name, Is.EqualTo(hoisted[0].name));
    Assert.That(rule.spawn!.Data[0].data!.Name, Is.EqualTo(hoisted[1].name));
    Assert.That(rule.poke![0].data!.Name, Is.EqualTo("named"));
  }

  [Test]
  public void Hoist_NamesAreDeterministic()
  {
    const string yaml = "data:" + NL + "  floats:" + NL + "    - a, 1";
    var first = DataField.Hoist([Parse(yaml)], "test.yaml");
    var second = DataField.Hoist([Parse(yaml)], "test.yaml");
    Assert.That(first[0].name, Is.EqualTo(second[0].name));
  }

  [Test]
  public void LegacyLines_WrapDataName()
  {
    Assert.That(SpawnYaml.FromLine("x, my_data").data!.Name, Is.EqualTo("my_data"));
    Assert.That(ObjectYaml.FromLine("x, 10, my_data").data!.Name, Is.EqualTo("my_data"));
  }
}
