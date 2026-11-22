using System;
using System.Collections.Generic;
using ExpandWorld.Prefab;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class DelayedQueueTests
{
  [Test]
  public void SkippedUpdatesDrainAllDueTiersInEnqueueOrder()
  {
    DelayedQueue<string> queue = new();
    List<string> executed = [];
    queue.Add(8, "far");
    queue.Add(3, "medium");
    queue.Add(0.4, "near");
    queue.Add(9, "future");
    double? Execute(string value) { executed.Add(value); return null; }

    queue.Drain(0, Execute);
    Assert.That(executed, Is.Empty);
    queue.Drain(8.1, Execute);
    Assert.That(executed, Is.EqualTo(new[] { "far", "medium", "near" }));
    queue.Drain(9, Execute);
    Assert.That(executed, Is.EqualTo(new[] { "far", "medium", "near", "future" }));
  }

  [Test]
  public void CallbackCanCancelReadyAndFutureWorkAndEnqueueDueWork()
  {
    DelayedQueue<string> queue = new();
    List<string> executed = [];
    queue.Add(1, "first");
    queue.Add(1, "cancel");
    queue.Add(10, "cancel");
    queue.Add(1, "next");

    queue.Drain(1, value =>
    {
      executed.Add(value);
      if (value == "first")
      {
        queue.RemoveWhere(pending => pending == "cancel");
        queue.Add(1, "added");
      }
      return null;
    });
    queue.Drain(10, value => { executed.Add(value); return null; });

    Assert.That(executed, Is.EqualTo(new[] { "first", "next", "added" }));
  }

  [Test]
  public void RetryKeepsItsOriginalOrderAndRunsOnlyWhenDue()
  {
    DelayedQueue<string> queue = new();
    List<string> executed = [];
    queue.Add(1, "retry");
    queue.Add(2, "later");
    queue.Drain(1, _ => 2);
    double? Execute(string value) { executed.Add(value); return null; }

    queue.Drain(1.99, Execute);
    Assert.That(executed, Is.Empty);
    queue.Drain(2, Execute);
    Assert.That(executed, Is.EqualTo(new[] { "retry", "later" }));
  }

  [TestCase(true)]
  [TestCase(false)]
  public void CallbackFailureRespectsConsumptionPolicy(bool consumeOnFailure)
  {
    DelayedQueue<string> queue = new();
    List<string> executed = [];
    queue.Add(1, "throws");
    queue.Add(1, "next");

    Assert.Throws<InvalidOperationException>(() => queue.Drain(1,
      _ => throw new InvalidOperationException(), consumeOnFailure));
    queue.Drain(1, value => { executed.Add(value); return null; });

    Assert.That(executed, Is.EqualTo(consumeOnFailure ? new[] { "next" } : new[] { "throws", "next" }));
  }
}