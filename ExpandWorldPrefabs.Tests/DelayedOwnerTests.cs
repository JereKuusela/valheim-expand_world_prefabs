using System;
using System.Collections;
using System.Reflection;
using ExpandWorld.Prefab;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class DelayedOwnerTests
{
  private const long Server = 900;
  private const long FirstClient = 101;
  private const long SecondClient = 202;

  [Test]
  public void ExistingPublicConstructorRemainsAvailable()
  {
    Assert.That(typeof(DelayedOwner).GetConstructor([typeof(double), typeof(ZDOID), typeof(long)]), Is.Not.Null);
  }

  [TearDown]
  public void TearDown() => DelayedOwner.Clear();

  [Test]
  public void OnlyConnectedCurrentClientIsReused()
  {
    var candidates = new[] { (Uid: FirstClient, Distance: 8f) };
    Assert.That(DelayedOwner.SelectOwner(FirstClient, Server, candidates), Is.EqualTo(FirstClient));
  }

  [Test]
  public void DifferentClientIsPreferredEvenWhenCurrentIsCloser()
  {
    var candidates = new[]
    {
      (Uid: FirstClient, Distance: 1f),
      (Uid: SecondClient, Distance: 9f),
      (Uid: 303L, Distance: 15f)
    };
    Assert.That(DelayedOwner.SelectOwner(FirstClient, Server, candidates), Is.EqualTo(SecondClient));
  }

  [Test]
  public void NearestClientIsChosenWhenNoCurrentOwner()
  {
    var candidates = new[]
    {
      (Uid: FirstClient, Distance: 15f),
      (Uid: SecondClient, Distance: 2f)
    };
    Assert.That(DelayedOwner.SelectOwner(0, Server, candidates), Is.EqualTo(SecondClient));
  }

  [Test]
  public void DisconnectedCurrentOwnerIsNotReused()
  {
    Assert.That(DelayedOwner.SelectOwner(FirstClient, Server,
      Array.Empty<(long Uid, float Distance)>()), Is.Zero);
    Assert.That(DelayedOwner.SelectOwner(FirstClient, Server,
      new[] { (Uid: SecondClient, Distance: 4f) }), Is.EqualTo(SecondClient));
  }

  [Test]
  public void ServerUidAndZeroAreNeverRemoteFallbacks()
  {
    var candidates = new[]
    {
      (Uid: Server, Distance: 0f),
      (Uid: 0L, Distance: 0f)
    };
    Assert.That(DelayedOwner.SelectOwner(Server, Server, candidates), Is.Zero);
    Assert.That(DelayedOwner.SelectOwner(0, Server, candidates), Is.Zero);
    Assert.That(DelayedOwner.SelectOwner(Server, Server,
      new[] { (Uid: FirstClient, Distance: 5f) }), Is.EqualTo(FirstClient));
  }

  [Test]
  public void ChangedOwnerRevisionInvalidatesDelayedAssignment()
  {
    Assert.That(DelayedOwner.CanAssign(0, 12, 11, FirstClient, Server, true), Is.False);
    Assert.That(DelayedOwner.CanAssign(0, 11, 11, FirstClient, Server, true), Is.True);
  }

  [Test]
  public void NewOwnerInvalidatesDelayedAssignmentEvenAtSameRevision()
  {
    Assert.That(DelayedOwner.CanAssign(SecondClient, 11, 11, FirstClient, Server, true), Is.False);
  }

  [Test]
  public void DisconnectedPeerCannotBeReinstalled()
  {
    Assert.That(DelayedOwner.CanAssign(0, 11, 11, FirstClient, Server, false), Is.False);
    Assert.That(DelayedOwner.CanAssign(0, 11, 11, FirstClient, Server, true), Is.True);
  }

  [Test]
  public void ExplicitServerOwnerAndUnownedRequestAreNotRemotePeers()
  {
    Assert.That(DelayedOwner.CanAssign(0, 11, 11, Server, Server, false), Is.True);
    Assert.That(DelayedOwner.CanAssign(0, 11, 11, 0, Server, false), Is.True);
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

  [Test]
  public void ReplacementKeepsOnlyLatestActionForZdoWithoutTouchingOthers()
  {
    var id = new ZDOID(101L, 1U);
    var other = new ZDOID(101L, 2U);
    DelayedOwner.ReplaceAction(1.0, id, FirstClient, 7);
    DelayedOwner.ReplaceAction(1.5, other, SecondClient, 8);
    DelayedOwner.ReplaceAction(2.0, id, SecondClient, 9);

    var field = typeof(DelayedOwner).GetField("Owners", BindingFlags.NonPublic | BindingFlags.Static)!;
    var queue = (IList)field.GetValue(null)!;
    Assert.That(queue.Count, Is.EqualTo(2));
    int idCount = 0;
    foreach (var action in queue)
    {
      var actionType = action!.GetType();
      var queuedId = (ZDOID)actionType.GetField("Zdo", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(action)!;
      if (queuedId != id) continue;
      idCount++;
      Assert.That(actionType.GetField("Owner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(action),
        Is.EqualTo(SecondClient));
      Assert.That(actionType.GetField("ExpectedOwnerRevision", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(action),
        Is.EqualTo(9UL));
    }
    Assert.That(idCount, Is.EqualTo(1));
  }
}
