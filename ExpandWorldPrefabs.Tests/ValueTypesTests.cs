using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using Service;
using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

public class ValueTypesTests
{
  [SetUp]
  public void SetUp()
  {
    Functions.ExecuteCode = _ => null;
    Functions.ExecuteCodeWithValue = (_, _) => null;
  }

  [TearDown]
  public void TearDown()
  {
    Functions.ExecuteCode = _ => null;
    Functions.ExecuteCodeWithValue = (_, _) => null;
  }

  [Test]
  public void TryAngleRadians_ParsesDegAndRadSuffixes()
  {
    Assert.That(Parse.TryAngleRadians("180deg", out var fromDeg), Is.True);
    Assert.That(fromDeg, Is.EqualTo(Mathf.PI).Within(0.001f));

    Assert.That(Parse.TryAngleRadians("3.1415927rad", out var fromRad), Is.True);
    Assert.That(fromRad, Is.EqualTo(3.1415927f).Within(0.001f));
  }

  [Test]
  public void TryAngleDegrees_ParsesDegAndRadSuffixes()
  {
    Assert.That(Parse.TryAngleDegrees("180deg", out var fromDeg), Is.True);
    Assert.That(fromDeg, Is.EqualTo(180f).Within(0.001f));

    Assert.That(Parse.TryAngleDegrees("3.1415927rad", out var fromRad), Is.True);
    Assert.That(fromRad, Is.EqualTo(180f).Within(0.001f));
  }

  [Test]
  public void CalculatorEvaluateVector3_ParsesDistanceAnglePolarFormat()
  {
    var vector = Calculator.EvaluateVector3("5,90deg");

    Assert.That(vector.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vector.z, Is.EqualTo(5f).Within(0.0001f));
    Assert.That(vector.y, Is.EqualTo(0f).Within(0.0001f));
  }

  [Test]
  public void TryDistanceAngle_ParsesPolarOffset()
  {
    Assert.That(Parse.TryDistanceAngle("5,90deg", out var vector), Is.True);
    Assert.That(vector.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vector.z, Is.EqualTo(5f).Within(0.0001f));
    Assert.That(vector.y, Is.EqualTo(0f).Within(0.0001f));

    Assert.That(Parse.TryDistanceAngle("3,1.5707964rad", out var vectorRad), Is.True);
    Assert.That(vectorRad.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vectorRad.z, Is.EqualTo(3f).Within(0.0001f));
  }

  [Test]
  public void TryDistanceAngle_StringArrayOverload_ParsesPolarOffset()
  {
    Assert.That(Parse.TryDistanceAngle(["5", "90deg"], out var vector), Is.True);
    Assert.That(vector.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vector.z, Is.EqualTo(5f).Within(0.0001f));
  }

  [Test]
  public void TryDistanceAngle_StringArrayOverload_ParsesPolarOffsetWithY()
  {
    Assert.That(Parse.TryDistanceAngle(["5", "90deg", "2"], out var vector), Is.True);
    Assert.That(vector.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vector.z, Is.EqualTo(5f).Within(0.0001f));
    Assert.That(vector.y, Is.EqualTo(2f).Within(0.0001f));
  }

  [Test]
  public void VectorXZY_ParsesPolarOffset()
  {
    var vector = Parse.VectorXZY("5,90deg");

    Assert.That(vector.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vector.z, Is.EqualTo(5f).Within(0.0001f));
    Assert.That(vector.y, Is.EqualTo(0f).Within(0.0001f));
  }

  [Test]
  public void VectorXZY_ParsesPolarOffsetWithY()
  {
    var vector = Parse.VectorXZY("5,90deg,2");

    Assert.That(vector.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vector.z, Is.EqualTo(5f).Within(0.0001f));
    Assert.That(vector.y, Is.EqualTo(2f).Within(0.0001f));
  }

  [Test]
  public void VectorXZYNull_ParsesPolarOffset()
  {
    var vector = Parse.VectorXZYNull(["5", "90deg"]);

    Assert.That(vector, Is.Not.Null);
    Assert.That(vector!.Value.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vector.Value.z, Is.EqualTo(5f).Within(0.0001f));
  }

  [Test]
  public void VectorXZYNull_ParsesPolarOffsetWithY()
  {
    var vector = Parse.VectorXZYNull(["5", "90deg", "2"]);

    Assert.That(vector, Is.Not.Null);
    Assert.That(vector!.Value.x, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vector.Value.z, Is.EqualTo(5f).Within(0.0001f));
    Assert.That(vector.Value.y, Is.EqualTo(2f).Within(0.0001f));
  }

  private static Functions CreateFunctions()
  {
#pragma warning disable SYSLIB0050
    return (Functions)FormatterServices.GetUninitializedObject(typeof(Functions));
#pragma warning restore SYSLIB0050
  }

  [Test]
  public void SimpleLongValue_GetAndMatch_ReturnConstantValue()
  {
    var value = new SimpleLongValue(42L);
    var f = CreateFunctions();

    var result = value.Get(f);

    Assert.That(result, Is.EqualTo(42L));
    Assert.That(value.Match(f, 42L), Is.True);
    Assert.That(value.Match(f, 41L), Is.False);
  }

  [Test]
  public void LongValue_Match_SupportsSimpleRangeAndSteppedRange()
  {
    var value = new LongValue(["3", "10;20", "100;110;5"]);
    var f = CreateFunctions();

    Assert.That(value.Match(f, 3), Is.True);
    Assert.That(value.Match(f, 15), Is.True);
    Assert.That(value.Match(f, 105), Is.True);
    Assert.That(value.Match(f, 106), Is.False);
  }

  [Test]
  public void LongValue_Match_ReturnsNullWhenAllValuesAreUnfable()
  {
    var value = new LongValue(["bad", "x;y", "1;2;bad-step"]);

    var result = value.Match(CreateFunctions(), 1L);

    Assert.That(result, Is.Null);
  }

  [Test]
  public void IntValue_Match_SteppedRangeHonorsStep()
  {
    var value = new IntValue(["2;10;2"]);
    var f = CreateFunctions();

    Assert.That(value.Match(f, 8), Is.True);
    Assert.That(value.Match(f, 9), Is.False);
  }

  [Test]
  public void SimpleFloatValue_TryGet_ReturnsTrueAndValue()
  {
    var value = new SimpleFloatValue(2.5f);

    var ok = value.TryGet(CreateFunctions(), out var result);

    Assert.That(ok, Is.True);
    Assert.That(result, Is.EqualTo(2.5f).Within(0.0001f));
  }

  [Test]
  public void FloatValue_Match_UsesApproximateComparison()
  {
    var value = new FloatValue(["1.5"]);

    var result = value.Match(CreateFunctions(), 1.500001f);

    Assert.That(result, Is.True);
  }

  [Test]
  public void BoolValue_GetIntAndGetBool_MapTrueAndFalse()
  {
    var f = CreateFunctions();
    var truthy = new BoolValue(["true"]);
    var falsy = new BoolValue(["false"]);

    Assert.That(truthy.GetInt(f), Is.EqualTo(1));
    Assert.That(truthy.GetBool(f), Is.True);
    Assert.That(falsy.GetInt(f), Is.EqualTo(0));
    Assert.That(falsy.GetBool(f), Is.False);
  }

  [Test]
  public void BytesValue_Get_fesBase64Payload()
  {
    var base64 = Convert.ToBase64String([1, 2, 3, 4]);
    var value = new BytesValue([base64]);

    var result = value.Get(CreateFunctions());

    Assert.That(result, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
  }

  [Test]
  public void BytesValue_Match_HandlesNullAndInvalidCandidates()
  {
    var base64 = Convert.ToBase64String([10, 20]);
    var value = new BytesValue(["", "invalid-base64", base64]);
    var f = CreateFunctions();

    Assert.That(value.Match(f, null), Is.True);
    Assert.That(value.Match(f, new byte[] { 10, 20 }), Is.True);
    Assert.That(value.Match(f, new byte[] { 20, 10 }), Is.False);
  }

  [Test]
  public void HashValue_GetAndMatch_UseStableHashCode()
  {
    var value = new HashValue(["Greydwarf"]);
    var expectedHash = "Greydwarf".GetStableHashCode();
    var f = CreateFunctions();

    Assert.That(value.Get(f), Is.EqualTo(expectedHash));
    Assert.That(value.Match(f, expectedHash), Is.True);
    Assert.That(value.Match(f, expectedHash + 1), Is.False);
  }

  [Test]
  public void StringValue_Match_SupportsWildcardPatterns()
  {
    var value = new StringValue(["wolf*", "*fenring", "*core*"]);
    var f = CreateFunctions();

    Assert.That(value.Match(f, "wolf_pup"), Is.True);
    Assert.That(value.Match(f, "night_fenring"), Is.True);
    Assert.That(value.Match(f, "surtling_core_item"), Is.True);
    Assert.That(value.Match(f, "boar"), Is.False);
  }

  [Test]
  public void Vector3Value_GetAndMatch_feXzyOrder()
  {
    var value = new Vector3Value(["1,2,3"]);
    var expected = new Vector3(1f, 3f, 2f);
    var f = CreateFunctions();

    Assert.That(value.Get(f), Is.EqualTo(expected));
    Assert.That(value.Match(f, expected), Is.True);
    Assert.That(value.Match(f, new Vector3(1f, 2f, 3f)), Is.False);
  }

  [Test]
  public void SimpleQuaternionValue_GetAndMatch_UsesExactValue()
  {
    var expected = Quaternion.identity;
    var value = new SimpleQuaternionValue(expected);
    var f = CreateFunctions();

    Assert.That(value.Get(f), Is.EqualTo(expected));
    Assert.That(value.Match(f, expected), Is.True);
    Assert.That(value.Match(f, new Quaternion(0f, 1f, 0f, 0f)), Is.False);
  }

  [Test]
  public void ZdoIdValue_GetAndMatch_feAndCompareIds()
  {
    var value = new ZdoIdValue(["123:456"]);
    var expected = new ZDOID(123L, 456u);
    var f = CreateFunctions();

    Assert.That(value.Get(f), Is.EqualTo(expected));
    Assert.That(value.Match(f, expected), Is.True);
    Assert.That(value.Match(f, new ZDOID(123L, 457u)), Is.False);
  }
}