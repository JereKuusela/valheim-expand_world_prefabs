using Data;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class CalculatorTests
{
  [Test]
  public void EvaluateFloat_RespectsOperatorPrecedence()
  {
    var result = Calculator.EvaluateFloat("2+3*4");
    Assert.That(result, Is.EqualTo(14f).Within(0.0001f));
  }

  [Test]
  public void EvaluateFloat_SupportsParentheses()
  {
    var result = Calculator.EvaluateFloat("(2+3)*4");
    Assert.That(result, Is.EqualTo(20f).Within(0.0001f));
  }

  [Test]
  public void EvaluateFloat_SupportsExponentiation()
  {
    var result = Calculator.EvaluateFloat("2^3^2");
    Assert.That(result, Is.EqualTo(512f).Within(0.0001f));
  }

  [Test]
  public void EvaluateFloat_HandlesNestedFormula()
  {
    var result = Calculator.EvaluateFloat("3 + 4 * 2 / (1 - 5)^2");
    Assert.That(result, Is.EqualTo(3.5f).Within(0.0001f));
  }

  [Test]
  public void EvaluateFloat_DoubleStarIsSameAsCaret()
  {
    Assert.That(Calculator.EvaluateFloat("2**10"), Is.EqualTo(Calculator.EvaluateFloat("2^10")));
  }

  [Test]
  public void EvaluateLong_DoubleStarIsSameAsCaret()
  {
    Assert.That(Calculator.EvaluateLong("3**4"), Is.EqualTo(Calculator.EvaluateLong("3^4")));
  }

  [Test]
  public void EvaluateFloat_UnaryMinusBindsLessThanPower()
  {
    // -2^2 = -(2^2) = -4, not (-2)^2 = 4
    var result = Calculator.EvaluateFloat("-2^2");
    Assert.That(result, Is.EqualTo(-4f).Within(0.0001f));
  }

  [Test]
  public void EvaluateFloat_NegativeExponentOnRightHandSide()
  {
    // 2^-3 = 0.125 — unary minus on the RHS of ^ must still work
    var result = Calculator.EvaluateFloat("2^-3");
    Assert.That(result, Is.EqualTo(0.125f).Within(0.0001f));
  }

  [Test]
  public void EvaluateFloat_InvalidExpression_ReturnsNull()
  {
    var result = Calculator.EvaluateFloat("2*(3+1");
    Assert.That(result, Is.Null);
  }

  [Test]
  public void EvaluateLong_SupportsParenthesesAndExponentiation()
  {
    var result = Calculator.EvaluateLong("2*(3+4)^2");
    Assert.That(result, Is.EqualTo(98L));
  }

  [Test]
  public void EvaluateLong_UsesIntegerDivision()
  {
    var result = Calculator.EvaluateLong("7/2");
    Assert.That(result, Is.EqualTo(3L));
  }

  [Test]
  public void EvaluateLong_NegativeExponent_ReturnsNull()
  {
    var result = Calculator.EvaluateLong("2^-1");
    Assert.That(result, Is.Null);
  }
}
