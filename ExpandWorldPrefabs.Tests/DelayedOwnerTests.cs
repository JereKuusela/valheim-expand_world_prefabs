using ExpandWorld.Prefab;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class DelayedOwnerTests
{
  private const long Client = 101;

  [TearDown]
  public void TearDown() => DelayedOwner.Clear();

  [Test]
  public void NewOwnerReleasesPending()
  {
    Assert.That(DelayedOwner.Decide(Client, true, false), Is.EqualTo(DelayedOwner.Outcome.Release));
    Assert.That(DelayedOwner.Decide(0, true, false), Is.EqualTo(DelayedOwner.Outcome.Assign));
  }

  [Test]
  public void KeepsWaitingUntilSentOrGivenUp()
  {
    Assert.That(DelayedOwner.Decide(0, false, false), Is.EqualTo(DelayedOwner.Outcome.Keep));
    Assert.That(DelayedOwner.Decide(0, false, true), Is.EqualTo(DelayedOwner.Outcome.Release));
  }

  [Test]
  public void SentWinsOverGiveUp()
  {
    Assert.That(DelayedOwner.Decide(0, true, true), Is.EqualTo(DelayedOwner.Outcome.Assign));
  }

  [TestCase(false, true, false, false, true)]
  [TestCase(false, false, true, false, true)]
  [TestCase(false, false, false, true, true)]
  [TestCase(false, false, false, false, false)]
  [TestCase(true, true, true, true, false)]
  public void ExistingPhysicsAndScaleDelaysRemainUnchanged(
    bool specialOwner, bool item, bool ship, bool scaleBackup, bool expected)
  {
    Assert.That(DelayedOwner.NeedsDelay(specialOwner, item, ship, scaleBackup), Is.EqualTo(expected));
  }
}
