using System.Collections.Generic;
using System.Runtime.Serialization;
using Data;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

public class DataStorageTests
{
  [SetUp]
  public void SetUp()
  {
    Functions.ExecuteCode = _ => null;
    Functions.ExecuteCodeWithValue = (_, _) => null;
    DataStorage.SetValue("*", "");
  }

  [TearDown]
  public void TearDown()
  {
    Functions.ExecuteCode = _ => null;
    Functions.ExecuteCodeWithValue = (_, _) => null;
    DataStorage.SetValue("*", "");
  }

  private static Functions CreateParameters()
  {
#pragma warning disable SYSLIB0050
    return (Functions)FormatterServices.GetUninitializedObject(typeof(Functions));
#pragma warning restore SYSLIB0050
  }

  [Test]
  public void HasEveryKey_ExactAndPresence_UseExistingBehavior()
  {
    DataStorage.SetValue("alpha", "x");
    DataStorage.SetValue("beta", "2");

    var result = DataStorage.HasEveryKey(new List<string> { "alpha x", "beta" }, CreateParameters());

    Assert.That(result, Is.True);
  }

  [Test]
  public void HasAnyKey_RangeWithoutStep_MatchesInclusiveRange()
  {
    DataStorage.SetValue("counter", "15");

    var result = DataStorage.HasAnyKey(new List<string> { "counter 10;20" }, CreateParameters());

    Assert.That(result, Is.True);
  }

  [Test]
  public void HasEveryKey_RangeWithStep_MatchesOnlySteppedValues()
  {
    DataStorage.SetValue("counter", "15");

    var result = DataStorage.HasEveryKey(new List<string> { "counter 10;20;5" }, CreateParameters());

    Assert.That(result, Is.True);
  }

  [Test]
  public void HasEveryKey_RangeWithStep_ReturnsFalseForNonStepValue()
  {
    DataStorage.SetValue("counter", "16");

    var result = DataStorage.HasEveryKey(new List<string> { "counter 10;20;5" }, CreateParameters());

    Assert.That(result, Is.False);
  }

  [Test]
  public void HasAnyKey_WildcardRange_MatchesWhenAnyWildcardValuePasses()
  {
    DataStorage.SetValue("enemy_a", "11");
    DataStorage.SetValue("enemy_b", "30");

    var result = DataStorage.HasAnyKey(new List<string> { "enemy_* 10;12" }, CreateParameters());

    Assert.That(result, Is.True);
  }

  [Test]
  public void HasEveryKey_WildcardRange_MatchesWhenAllWildcardValuesPass()
  {
    DataStorage.SetValue("enemy_a", "11");
    DataStorage.SetValue("enemy_b", "12");

    var result = DataStorage.HasEveryKey(new List<string> { "enemy_* 10;12" }, CreateParameters());

    Assert.That(result, Is.True);
  }

  [Test]
  public void HasAnyKey_InvalidRangeOrNonNumericStoredValue_ReturnFalse()
  {
    DataStorage.SetValue("score", "15");
    DataStorage.SetValue("name", "boar");

    var invalidStep = DataStorage.HasAnyKey(new List<string> { "score 1;20;bad" }, CreateParameters());
    var nonNumeric = DataStorage.HasAnyKey(new List<string> { "name 1;20" }, CreateParameters());

    Assert.That(invalidStep, Is.False);
    Assert.That(nonNumeric, Is.False);
  }
}
