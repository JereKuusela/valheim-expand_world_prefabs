using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class NamedRuleLogTests
{
  public static IEnumerable<TestCaseData> Checks => NamedRuleLogChecks.Tests.Select(test =>
    new TestCaseData(test).SetName("NamedRuleLog_" + test.Method.Name));

  [TestCaseSource(nameof(Checks))]
  public void NamedAndRollingLoggingChecks(Action check) => check();
}
