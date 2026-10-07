using Data;
using NUnit.Framework;
using System.Runtime.Serialization;

namespace ExpandWorldPrefabs.Tests;

public class ValueMatchingTests
{
  private static Functions CreateFunctions() => (Functions)FormatterServices.GetUninitializedObject(typeof(Functions));

  [TestCase("true", true)]
  [TestCase("TRUE", true)]
  [TestCase("True", true)]
  [TestCase("false", false)]
  [TestCase("1", false)]
  [TestCase("", false)]
  public void BoolValue_ComparesTrueIgnoringCase(string raw, bool expected)
  {
    var value = new BoolValue([raw]);
    Assert.That(value.GetBool(CreateFunctions()), Is.EqualTo(expected));
    Assert.That(value.GetInt(CreateFunctions()), Is.EqualTo(expected ? 1 : 0));
  }

  [Test]
  public void IntValue_RangeWithZeroStep_MatchesPlainRange()
  {
    var value = new IntValue(["1;5;0"]);
    var f = CreateFunctions();
    Assert.That(value.Match(f, 3), Is.True);
    Assert.That(value.Match(f, 6), Is.False);
  }

  [Test]
  public void IntValue_RangeWithStep_MatchesOnlyStepValues()
  {
    var value = new IntValue(["0;10;5"]);
    var f = CreateFunctions();
    Assert.That(value.Match(f, 5), Is.True);
    Assert.That(value.Match(f, 6), Is.False);
  }

  [Test]
  public void IntValue_RangeWithExtraSegment_IsStillUsed()
  {
    var value = new IntValue(["1;9;2;extra"]);
    var f = CreateFunctions();
    Assert.That(value.Match(f, 5), Is.True);
    Assert.That(value.Match(f, 4), Is.False);
  }

  [Test]
  public void LongValue_RangeWithZeroStep_MatchesPlainRange()
  {
    var value = new LongValue(["10;50;0"]);
    var f = CreateFunctions();
    Assert.That(value.Match(f, 30L), Is.True);
    Assert.That(value.Match(f, 51L), Is.False);
  }

  [Test]
  public void FloatValue_RangeWithZeroStep_MatchesPlainRange()
  {
    var value = new FloatValue(["1;2;0"]);
    var f = CreateFunctions();
    Assert.That(value.Match(f, 1.5f), Is.True);
    Assert.That(value.Match(f, 2.5f), Is.False);
  }

  [Test]
  public void FloatValue_RangeWithExtraSegment_IsStillUsed()
  {
    var value = new FloatValue(["0;1;0.5;extra"]);
    var f = CreateFunctions();
    Assert.That(value.Match(f, 0.5f), Is.True);
    Assert.That(value.Match(f, 0.25f), Is.False);
  }

  [Test]
  public void StringValue_MatchSupportsPatternsAndIgnoresEmptyValues()
  {
    var f = CreateFunctions();
    Assert.That(new StringValue(["Wood*", ""]).Match(f, "WoodPile"), Is.True);
    Assert.That(new StringValue(["Wood*"]).Match(f, "Stone"), Is.False);
    Assert.That(new StringValue(["", "<none>"]).Match(f, "Stone"), Is.Null);
  }
}
