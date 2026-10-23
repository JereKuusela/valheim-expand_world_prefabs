using System;
using System.Reflection;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

[TestFixture]
public class CustomScalingPolicyTests
{
  [TestCase(false, true, false, true)]
  [TestCase(false, false, true, true)]
  [TestCase(false, false, false, false)]
  [TestCase(true, true, true, false)]
  public void StageOnlyWhenReleasedAndReloadedOrFar(bool held, bool needsStage, bool far, bool expected) =>
    Assert.That(CustomScaling.ShouldBeginStage(held, needsStage, far), Is.EqualTo(expected));

  [TestCase(3f, 4.4f, false)]
  [TestCase(3f, 4.5f, true)]
  [TestCase(-1f, 100f, false)]
  public void ReleaseNeedsAnUninterruptedNearbyWindow(float since, float now, bool release) =>
    Assert.That(CustomScaling.ReleaseDelayElapsed(since, now), Is.EqualTo(release));

  [TestCase(42L, 42L, false, false, true)]
  [TestCase(42L, 42L, true, false, false)]
  [TestCase(42L, 42L, false, true, false)]
  [TestCase(99L, 42L, false, false, false)]
  [TestCase(0L, 42L, false, false, false)]
  public void ReleaseOnlyOurTemporaryServerOwner(long actual, long server, bool hostNearby,
    bool explicitServerOwner, bool release) =>
    Assert.That(CustomScaling.ShouldGiveUpServerOwner(actual, server, hostNearby, explicitServerOwner), Is.EqualTo(release));

  // The compiler/test project includes this source; no native ZNetScene is required.
  [Test]
  public void AuthoredScaleComesOnlyFromSuppliedRuleEntries()
  {
    var entry = new ZdoEntry(123, Vector3.zero, Vector3.zero, 0);
    var runtime = new DataEntry { Vecs = new() { [ZDOVars.s_scaleHash] = new ConstantVector3Value(new Vector3(1.2f, 1.2f, 1.2f)) } };
    var authored = new DataEntry { Floats = new() { [ZDOVars.s_scaleScalarHash] = new ConstantFloatValue(1.3f) } };
    entry.RecordExplicitScale(authored, null!);
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var scalar = (float?)typeof(ZdoEntry).GetField("ExplicitScalar", flags)!.GetValue(entry);
    var vector = (Vector3?)typeof(ZdoEntry).GetField("ExplicitVector", flags)!.GetValue(entry);
    Assert.That(scalar, Is.EqualTo(1.3f));
    Assert.That(vector.HasValue, Is.False, "Copied runtime vector is not an authored rule input");
    Assert.That(CustomScaling.TryResolveAuthoredScale(scalar, vector, out var target), Is.True);
    Assert.That(target, Is.EqualTo(new Vector3(1.3f, 1.3f, 1.3f)));
    Assert.That(runtime.Vecs, Is.Not.Empty); // Runtime data exists, but was not promoted.
  }

  [Test]
  public void SpawnRuleVectorOverridesParentRuleScalar()
  {
    var entry = new ZdoEntry(123, Vector3.zero, Vector3.zero, 0);
    var parent = new DataEntry { Floats = new() { [ZDOVars.s_scaleScalarHash] = new ConstantFloatValue(1.3f) } };
    var spawn = new DataEntry { Vecs = new() { [ZDOVars.s_scaleHash] = new ConstantVector3Value(new Vector3(2, 1, 0.5f)) } };
    entry.RecordExplicitScale(parent, null!);
    entry.RecordExplicitScale(spawn, null!);
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var scalar = (float?)typeof(ZdoEntry).GetField("ExplicitScalar", flags)!.GetValue(entry);
    var vector = (Vector3?)typeof(ZdoEntry).GetField("ExplicitVector", flags)!.GetValue(entry);
    Assert.That(CustomScaling.TryResolveAuthoredScale(scalar, vector, out var target), Is.True);
    Assert.That(target, Is.EqualTo(new Vector3(2, 1, 0.5f)));
  }
}
