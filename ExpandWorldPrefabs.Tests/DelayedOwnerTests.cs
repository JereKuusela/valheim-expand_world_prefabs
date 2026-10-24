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
  public void OnlyReadyClientIsSelected()
  {
    var candidates = new[] { (Uid: FirstClient, Distance: 8f, Ready: true) };
    Assert.That(DelayedOwner.SelectOwner(Server, candidates), Is.EqualTo(FirstClient));
  }

  [Test]
  public void NearestReadyClientIsChosenAmongMultiplePeers()
  {
    var candidates = new[]
    {
      (Uid: FirstClient, Distance: 1f, Ready: true),
      (Uid: SecondClient, Distance: 9f, Ready: true),
      (Uid: 303L, Distance: 15f, Ready: true)
    };
    Assert.That(DelayedOwner.SelectOwner(Server, candidates), Is.EqualTo(FirstClient));
  }

  [Test]
  public void NearestReadyClientCanAppearLaterInPeerList()
  {
    var candidates = new[]
    {
      (Uid: FirstClient, Distance: 15f, Ready: true),
      (Uid: SecondClient, Distance: 2f, Ready: true)
    };
    Assert.That(DelayedOwner.SelectOwner(Server, candidates), Is.EqualTo(SecondClient));
  }

  [Test]
  public void UnreadyCloserClientIsSkipped()
  {
    var candidates = new[]
    {
      (Uid: FirstClient, Distance: 1f, Ready: false),
      (Uid: SecondClient, Distance: 9f, Ready: true)
    };
    Assert.That(DelayedOwner.SelectOwner(Server, candidates), Is.EqualTo(SecondClient));
  }

  [Test]
  public void NoReadyClientReturnsZero()
  {
    Assert.That(DelayedOwner.SelectOwner(Server,
      Array.Empty<(long Uid, float Distance, bool Ready)>()), Is.Zero);
    Assert.That(DelayedOwner.SelectOwner(Server,
      new[] { (Uid: FirstClient, Distance: 4f, Ready: false) }), Is.Zero);
  }

  [Test]
  public void ServerUidAndZeroAreNeverClientOwners()
  {
    var candidates = new[]
    {
      (Uid: Server, Distance: 0f, Ready: true),
      (Uid: 0L, Distance: 0f, Ready: true)
    };
    Assert.That(DelayedOwner.SelectOwner(Server, candidates), Is.Zero);
    Assert.That(DelayedOwner.SelectOwner(Server,
      new[] { (Uid: Server, Distance: 0f, Ready: true), (Uid: FirstClient, Distance: 5f, Ready: true) }),
      Is.EqualTo(FirstClient));
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
