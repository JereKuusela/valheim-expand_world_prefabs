using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class RuleLogTests
{
  public static IEnumerable<TestCaseData> Checks => RuleLogChecks.Tests.Select(test =>
    new TestCaseData(test).SetName("RuleLog_" + test.Method.Name));

  [TestCaseSource(nameof(Checks))]
  public void BufferedTransportChecks(Action check) => check();
}
