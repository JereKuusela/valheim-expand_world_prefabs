using NUnit.Framework;
namespace ExpandWorldPrefabs.Tests;
public class RuleLogTests
{
  [Test]
  public void BufferedTransportChecks() => RuleLogChecks.RunAll();
}
