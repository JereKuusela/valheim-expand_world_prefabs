using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class TeleportTests
{
  [Test]
  public void DestinationSynchronization() =>
    TeleportChecks.RunDestinationSynchronizationChecks();
}
