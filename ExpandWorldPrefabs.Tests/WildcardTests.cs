using Common;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class WildcardTests
{
  [TestCase("Wood", "Wood", true)]
  [TestCase("Wood", "wood", false)]
  [TestCase("Wood", "*", true)]
  [TestCase("", "*", true)]
  [TestCase("Wood", "", false)]
  [TestCase("WoodPile", "Wood*", true)]
  [TestCase("WoodPile", "*Pile", true)]
  [TestCase("WoodPile", "*odP*", true)]
  [TestCase("WoodPile", "W*e", true)]
  [TestCase("WoodPile", "W*x", false)]
  [TestCase("WoodPile", "Stone*", false)]
  public void Match_IsCaseSensitiveByDefault(string value, string pattern, bool expected)
  {
    Assert.That(Wildcard.Match(value, pattern), Is.EqualTo(expected));
  }

  [TestCase("WoodPile", "wood*", true)]
  [TestCase("WoodPile", "*PILE", true)]
  [TestCase("WoodPile", "*ODp*", true)]
  [TestCase("WoodPile", "w*E", true)]
  [TestCase("Wood", "wood", true)]
  public void Match_CanIgnoreCase(string value, string pattern, bool expected)
  {
    Assert.That(Wildcard.Match(value, pattern, true), Is.EqualTo(expected));
  }

  [Test]
  public void Match_InnerWildcardPartsMustNotOverlap()
  {
    Assert.That(Wildcard.Match("aba", "ab*ba"), Is.False);
    Assert.That(Wildcard.Match("abba", "ab*ba"), Is.True);
  }

  [TestCase("a*", true)]
  [TestCase("*", true)]
  [TestCase("a", false)]
  [TestCase("", false)]
  public void IsPattern_DetectsWildcard(string value, bool expected)
  {
    Assert.That(Wildcard.IsPattern(value), Is.EqualTo(expected));
  }
}
