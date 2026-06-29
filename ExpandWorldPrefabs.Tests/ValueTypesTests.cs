using Data;
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
    Parameters.ExecuteCode = _ => null;
    Parameters.ExecuteCodeWithValue = (_, _) => null;
  }

  [TearDown]
  public void TearDown()
  {
    Parameters.ExecuteCode = _ => null;
    Parameters.ExecuteCodeWithValue = (_, _) => null;
  }

  private static Parameters CreateParameters()
  {
#pragma warning disable SYSLIB0050
    return (Parameters)FormatterServices.GetUninitializedObject(typeof(Parameters));
#pragma warning restore SYSLIB0050
  }

  [Test]
  public void SimpleLongValue_GetAndMatch_ReturnConstantValue()
  {
    var value = new SimpleLongValue(42L);
    var pars = CreateParameters();

    var result = value.Get(pars);

    Assert.That(result, Is.EqualTo(42L));
    Assert.That(value.Match(pars, 42L), Is.True);
    Assert.That(value.Match(pars, 41L), Is.False);
  }

  [Test]
  public void LongValue_Match_SupportsSimpleRangeAndSteppedRange()
  {
    var value = new LongValue(["3", "10;20", "100;110;5"]);
    var pars = CreateParameters();

    Assert.That(value.Match(pars, 3), Is.True);
    Assert.That(value.Match(pars, 15), Is.True);
    Assert.That(value.Match(pars, 105), Is.True);
    Assert.That(value.Match(pars, 106), Is.False);
  }

  [Test]
  public void LongValue_Match_ReturnsNullWhenAllValuesAreUnparsable()
  {
    var value = new LongValue(["bad", "x;y", "1;2;bad-step"]);

    var result = value.Match(CreateParameters(), 1L);

    Assert.That(result, Is.Null);
  }

  [Test]
  public void IntValue_Match_SteppedRangeHonorsStep()
  {
    var value = new IntValue(["2;10;2"]);
    var pars = CreateParameters();

    Assert.That(value.Match(pars, 8), Is.True);
    Assert.That(value.Match(pars, 9), Is.False);
  }

  [Test]
  public void SimpleFloatValue_TryGet_ReturnsTrueAndValue()
  {
    var value = new SimpleFloatValue(2.5f);

    var ok = value.TryGet(CreateParameters(), out var result);

    Assert.That(ok, Is.True);
    Assert.That(result, Is.EqualTo(2.5f).Within(0.0001f));
  }

  [Test]
  public void FloatValue_Match_UsesApproximateComparison()
  {
    var value = new FloatValue(["1.5"]);

    var result = value.Match(CreateParameters(), 1.500001f);

    Assert.That(result, Is.True);
  }

  [Test]
  public void BoolValue_GetIntAndGetBool_MapTrueAndFalse()
  {
    var pars = CreateParameters();
    var truthy = new BoolValue(["true"]);
    var falsy = new BoolValue(["false"]);

    Assert.That(truthy.GetInt(pars), Is.EqualTo(1));
    Assert.That(truthy.GetBool(pars), Is.True);
    Assert.That(falsy.GetInt(pars), Is.EqualTo(0));
    Assert.That(falsy.GetBool(pars), Is.False);
  }

  [Test]
  public void BytesValue_Get_ParsesBase64Payload()
  {
    var base64 = Convert.ToBase64String([1, 2, 3, 4]);
    var value = new BytesValue([base64]);

    var result = value.Get(CreateParameters());

    Assert.That(result, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
  }

  [Test]
  public void BytesValue_Match_HandlesNullAndInvalidCandidates()
  {
    var base64 = Convert.ToBase64String([10, 20]);
    var value = new BytesValue(["", "invalid-base64", base64]);
    var pars = CreateParameters();

    Assert.That(value.Match(pars, null), Is.True);
    Assert.That(value.Match(pars, new byte[] { 10, 20 }), Is.True);
    Assert.That(value.Match(pars, new byte[] { 20, 10 }), Is.False);
  }

  [Test]
  public void HashValue_GetAndMatch_UseStableHashCode()
  {
    var value = new HashValue(["Greydwarf"]);
    var expectedHash = "Greydwarf".GetStableHashCode();
    var pars = CreateParameters();

    Assert.That(value.Get(pars), Is.EqualTo(expectedHash));
    Assert.That(value.Match(pars, expectedHash), Is.True);
    Assert.That(value.Match(pars, expectedHash + 1), Is.False);
  }

  [Test]
  public void StringValue_Match_SupportsWildcardPatterns()
  {
    var value = new StringValue(["wolf*", "*fenring", "*core*"]);
    var pars = CreateParameters();

    Assert.That(value.Match(pars, "wolf_pup"), Is.True);
    Assert.That(value.Match(pars, "night_fenring"), Is.True);
    Assert.That(value.Match(pars, "surtling_core_item"), Is.True);
    Assert.That(value.Match(pars, "boar"), Is.False);
  }

  [Test]
  public void Vector3Value_GetAndMatch_ParseXzyOrder()
  {
    var value = new Vector3Value(["1,2,3"]);
    var expected = new Vector3(1f, 3f, 2f);
    var pars = CreateParameters();

    Assert.That(value.Get(pars), Is.EqualTo(expected));
    Assert.That(value.Match(pars, expected), Is.True);
    Assert.That(value.Match(pars, new Vector3(1f, 2f, 3f)), Is.False);
  }

  [Test]
  public void SimpleQuaternionValue_GetAndMatch_UsesExactValue()
  {
    var expected = Quaternion.identity;
    var value = new SimpleQuaternionValue(expected);
    var pars = CreateParameters();

    Assert.That(value.Get(pars), Is.EqualTo(expected));
    Assert.That(value.Match(pars, expected), Is.True);
    Assert.That(value.Match(pars, new Quaternion(0f, 1f, 0f, 0f)), Is.False);
  }

  [Test]
  public void ZdoIdValue_GetAndMatch_ParseAndCompareIds()
  {
    var value = new ZdoIdValue(["123:456"]);
    var expected = new ZDOID(123L, 456u);
    var pars = CreateParameters();

    Assert.That(value.Get(pars), Is.EqualTo(expected));
    Assert.That(value.Match(pars, expected), Is.True);
    Assert.That(value.Match(pars, new ZDOID(123L, 457u)), Is.False);
  }
}