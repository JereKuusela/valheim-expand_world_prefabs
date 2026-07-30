using Data;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class ConditionsTests
{
  private static bool Evaluate(string condition)
  {
    var parsed = Conditions.TryParse(condition, out var clause, out var error);
    Assert.That(parsed, Is.True, error);
    Assert.That(clause, Is.Not.Null);
    return clause!.Evaluate(static token => token);
  }

  [Test]
  public void Evaluate_UsesLogicalPrecedence()
  {
    // NOT > AND > XOR > OR
    var result = Evaluate("0 OR 1 AND 0 XOR 1");
    Assert.That(result, Is.True);
  }

  [Test]
  public void Evaluate_ParenthesesOverridePrecedence()
  {
    var result = Evaluate("(0 OR 1) AND (0 XOR 1)");
    Assert.That(result, Is.True);
  }

  [Test]
  public void Evaluate_OperatorsAreCaseInsensitive()
  {
    var result = Evaluate("1 aNd noT 0 xOr 0 oR 0");
    Assert.That(result, Is.True);
  }

  [Test]
  public void Evaluate_EqualitySupportsNumericAndText()
  {
    Assert.That(Evaluate("1 = 1.0"), Is.True);
    Assert.That(Evaluate("Player = player"), Is.True);
    Assert.That(Evaluate("Player != enemy"), Is.True);
  }

  [Test]
  public void Evaluate_RelationalOperatorsRequireNumbers()
  {
    Assert.That(Evaluate("5 >= 4"), Is.True);
    Assert.That(Evaluate("5 < 4"), Is.False);
    Assert.That(Evaluate("apple > banana"), Is.False);
  }

  [Test]
  public void Evaluate_TruthyRulesMatchZeroAndEmptyBehavior()
  {
    Assert.That(Evaluate("0"), Is.False);
    Assert.That(Evaluate("1"), Is.True);
    Assert.That(Evaluate("''"), Is.False);
    Assert.That(Evaluate("false"), Is.False);
    Assert.That(Evaluate("FALSE"), Is.False);
    Assert.That(Evaluate("text"), Is.True);
  }

  [Test]
  public void Evaluate_FunctionTokensResolveBeforeComparison()
  {
    var parsed = Conditions.TryParse("<amount> > 0 AND <name> != ''", out var clause, out var error);
    Assert.That(parsed, Is.True, error);
    Assert.That(clause, Is.Not.Null);

    var values = new System.Collections.Generic.Dictionary<string, string>
    {
      ["<amount>"] = "2",
      ["<name>"] = "wolf",
    };

    var result = clause!.Evaluate(token => values.TryGetValue(token, out var value) ? value : token);
    Assert.That(result, Is.True);
  }

  [Test]
  public void Evaluate_InOperator_UsesContainsMatching()
  {
    Assert.That(Evaluate("wolf IN wolf,boar,deer"), Is.True);
    Assert.That(Evaluate("boar IN wolf,boar,deer"), Is.True);
    Assert.That(Evaluate("lox IN wolf,boar,deer"), Is.False);
  }

  [Test]
  public void Evaluate_NotInOperator_UsesContainsMatching()
  {
    Assert.That(Evaluate("wolf NOT IN wolf,boar,deer"), Is.False);
    Assert.That(Evaluate("lox NOT IN wolf,boar,deer"), Is.True);
  }

  [Test]
  public void Evaluate_InAndNotInWorkWithSimpleLists()
  {
    Assert.That(Evaluate("wolf IN wolf,boar,deer"), Is.True);
    Assert.That(Evaluate("wolf IN boar,deer"), Is.False);
    Assert.That(Evaluate("wolf NOT IN boar,deer"), Is.True);
    Assert.That(Evaluate("wolf NOT IN wolf,boar,deer"), Is.False);
  }

  [Test]
  public void Evaluate_InSupportsCaseInsensitiveContainsBehavior()
  {
    Assert.That(Evaluate("wolf IN AlphaWolf"), Is.True);
    Assert.That(Evaluate("Wolf IN alphawolf"), Is.True);
  }

  [Test]
  public void TryParse_DetectsMalformedCondition()
  {
    var parsed = Conditions.TryParse("(1 AND 0", out var clause, out var error);
    Assert.That(parsed, Is.False);
    Assert.That(clause, Is.Null);
    Assert.That(error, Is.Not.Empty);
  }

  [Test]
  public void TryParse_RejectsDoubleEqualsAlias()
  {
    var parsed = Conditions.TryParse("Player == player", out var clause, out var error);
    Assert.That(parsed, Is.False);
    Assert.That(clause, Is.Null);
    Assert.That(error, Is.Not.Empty);
  }

  [Test]
  public void TryParse_RejectsAngleBracketNotEqualAlias()
  {
    var parsed = Conditions.TryParse("Player <> enemy", out var clause, out var error);
    Assert.That(parsed, Is.False);
    Assert.That(clause, Is.Null);
    Assert.That(error, Is.Not.Empty);
  }
}
