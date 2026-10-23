using System.Collections.Generic;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

// These tests exercise the resolver shared by TryBegin (spawn) and
// UpdateWrittenScale (later authored rule writes), without requiring a scene.
[TestFixture]
public class CustomScalingInputTests
{
  [TestCase(0.5f)]
  [TestCase(1f)]
  [TestCase(4f)]
  [TestCase(20f)]
  public void ScalarBecomesUniformScaleVector(float scalar)
  {
    Assert.That(CustomScaling.TryResolveAuthoredScale(scalar, null, out var target), Is.True);
    Assert.That(target, Is.EqualTo(new Vector3(scalar, scalar, scalar)));
  }

  [Test]
  public void AnisotropicVectorPreservesEveryComponent()
  {
    var authored = new Vector3(2f, 1f, 0.5f);
    Assert.That(CustomScaling.TryResolveAuthoredScale(null, authored, out var target), Is.True);
    Assert.That(target, Is.EqualTo(authored));
  }

  [Test]
  public void ExplicitVectorOverridesScalar()
  {
    var authored = new Vector3(2f, 1f, 0.5f);
    Assert.That(CustomScaling.TryResolveAuthoredScale(9f, authored, out var target), Is.True);
    Assert.That(target, Is.EqualTo(authored));
  }

  [Test]
  public void NeitherScaleInputCreatesANewIntent()
  {
    Assert.That(CustomScaling.TryResolveAuthoredScale(null, null, out _), Is.False);
  }

  [TestCase(0f)]
  [TestCase(-0.5f)]
  [TestCase(float.NaN)]
  [TestCase(float.PositiveInfinity)]
  [TestCase(float.NegativeInfinity)]
  public void InvalidScalarIsRejected(float scalar)
  {
    Assert.That(CustomScaling.TryResolveAuthoredScale(scalar, null, out _), Is.False);
  }

  [Test]
  public void InvalidVectorDoesNotFallBackToValidScalar()
  {
    Assert.That(CustomScaling.TryResolveAuthoredScale(2f, new Vector3(0f, 2f, 2f), out _), Is.False);
  }

  [Test]
  public void NonFiniteVectorIsRejected()
  {
    Assert.That(CustomScaling.TryResolveAuthoredScale(null, new Vector3(1f, float.NaN, 1f), out _), Is.False);
    Assert.That(CustomScaling.TryResolveAuthoredScale(null, new Vector3(1f, 1f, float.PositiveInfinity), out _), Is.False);
  }

  [Test]
  public void WrittenScalarUsesTheSameUniformVectorAsSpawn()
  {
    var floats = new Dictionary<int, float> { [ZDOVars.s_scaleScalarHash] = 0.5f };
    Assert.That(CustomScaling.TryResolveWrittenScale(floats, null, out var written), Is.True);
    Assert.That(CustomScaling.TryResolveAuthoredScale(0.5f, null, out var spawned), Is.True);
    Assert.That(written, Is.EqualTo(spawned));
  }

  [Test]
  public void WrittenVectorTakesPrecedenceOverWrittenScalar()
  {
    var authored = new Vector3(2f, 1f, 0.5f);
    var floats = new Dictionary<int, float> { [ZDOVars.s_scaleScalarHash] = 4f };
    var vectors = new Dictionary<int, Vector3> { [ZDOVars.s_scaleHash] = authored };
    Assert.That(CustomScaling.TryResolveWrittenScale(floats, vectors, out var written), Is.True);
    Assert.That(written, Is.EqualTo(authored));
  }

  [Test]
  public void UnrelatedRuleValuesDoNotCreateScalingIntent()
  {
    var floats = new Dictionary<int, float> { [123456789] = 4f };
    var vectors = new Dictionary<int, Vector3> { [987654321] = new Vector3(4f, 4f, 4f) };
    Assert.That(CustomScaling.TryResolveWrittenScale(floats, vectors, out _), Is.False);
  }
}
