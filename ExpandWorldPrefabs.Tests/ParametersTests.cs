using Data;
using NUnit.Framework;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace ExpandWorldPrefabs.Tests;

public class ParametersTests
{
  private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

  [SetUp]
  public void SetUp()
  {
    Parameters.ExecuteCode = _ => null;
    Parameters.ExecuteCodeWithValue = (_, _) => null;
  }

  [TearDown]
  public void TearDown()
  {
    Parameters.ExecuteCode = _ => null;
    Parameters.ExecuteCodeWithValue = (_, _) => null;
  }

  private static Parameters CreateUninitializedParameters()
  {
#pragma warning disable SYSLIB0050
    return (Parameters)FormatterServices.GetUninitializedObject(typeof(Parameters));
#pragma warning restore SYSLIB0050
  }

  private static string Invoke(string methodName, string value, string defaultValue = "fallback")
  {
    var instance = CreateUninitializedParameters();
    var method = typeof(Parameters).GetMethod(methodName, PrivateInstance);
    Assert.That(method, Is.Not.Null, $"Expected private method '{methodName}' on Parameters.");

    return (string)method!.Invoke(instance, new object[] { value, defaultValue })!;
  }

  private static float InvokeFloat(string methodName, string value, string defaultValue = "fallback")
  {
    var result = Invoke(methodName, value, defaultValue);
    return float.Parse(result, CultureInfo.InvariantCulture);
  }

  private static long InvokeLong(string methodName, string value, string defaultValue = "fallback")
  {
    var result = Invoke(methodName, value, defaultValue);
    return long.Parse(result, CultureInfo.InvariantCulture);
  }

  private static float[] ParseUnityVector(string value)
  {
    var trimmed = value.Trim('(', ')');
    var parts = trimmed.Split(',');
    Assert.That(parts.Length, Is.EqualTo(3), "Expected a Vector3-formatted string.");
    return parts.Select(p => float.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
  }

  private static string Replace(string input, bool preventInjections = false)
  {
    var instance = CreateUninitializedParameters();
    return instance.Replace(input, preventInjections);
  }

  [Test]
  public void HandleAdd_SumsValues()
  {
    var result = InvokeFloat("HandleAdd", "1.5_2_3");
    Assert.That(result, Is.EqualTo(6.5f).Within(0.0001f));
  }

  [Test]
  public void HandleAdd_WithVectorValues_AddsComponentWise()
  {
    var result = Invoke("HandleAdd", "1,2,3_4,5,6");

    Assert.That(result, Is.EqualTo("5 7 9"));
  }

  [Test]
  public void HandleSub_SubtractsFromFirstValue()
  {
    var result = InvokeFloat("HandleSub", "10_2_1.5");
    Assert.That(result, Is.EqualTo(6.5f).Within(0.0001f));
  }

  [Test]
  public void HandleSub_WithVectorAndScalar_SubtractsScalarFromEachComponent()
  {
    var result = Invoke("HandleSub", "5,7,9_1");

    Assert.That(result, Is.EqualTo("4 6 8"));
  }

  [Test]
  public void HandleMul_MultipliesValues()
  {
    var result = InvokeFloat("HandleMul", "2_3_4");
    Assert.That(result, Is.EqualTo(24f).Within(0.0001f));
  }

  [Test]
  public void HandleMul_WithVectorAndScalar_MultipliesEachComponent()
  {
    var result = Invoke("HandleMul", "1,2,3_2");

    Assert.That(result, Is.EqualTo("2 4 6"));
  }

  [Test]
  public void HandleDiv_ByZero_ReturnsDefault()
  {
    var result = Invoke("HandleDiv", "10_0", "default");
    Assert.That(result, Is.EqualTo("default"));
  }

  [Test]
  public void HandleDiv_DividesValuesInOrder()
  {
    var result = InvokeFloat("HandleDiv", "20_2_2");
    Assert.That(result, Is.EqualTo(5f).Within(0.0001f));
  }

  [Test]
  public void HandleDiv_WithVectorDivisor_DividesComponentWise()
  {
    var result = Invoke("HandleDiv", "8,12,16_2,3,4");

    Assert.That(result, Is.EqualTo("4 4 4"));
  }

  [Test]
  public void HandleDiv_WithVectorZeroDivisor_ReturnsDefault()
  {
    var result = Invoke("HandleDiv", "8,12,16_2,0,4", "default");

    Assert.That(result, Is.EqualTo("default"));
  }

  [Test]
  public void HandleMod_ModuloValuesInOrder()
  {
    var result = InvokeFloat("HandleMod", "20_6_2");
    Assert.That(result, Is.EqualTo(0f).Within(0.0001f));
  }

  [Test]
  public void HandleAddLong_SumsLongValues()
  {
    var result = InvokeLong("HandleAddLong", "5_10_15");
    Assert.That(result, Is.EqualTo(30L));
  }

  [Test]
  public void HandleMulLong_MultipliesLongValues()
  {
    var result = InvokeLong("HandleMulLong", "2_3_4");
    Assert.That(result, Is.EqualTo(24L));
  }

  [Test]
  public void HandleModLong_ByZero_ReturnsDefault()
  {
    var result = Invoke("HandleModLong", "10_0", "default");
    Assert.That(result, Is.EqualTo("default"));
  }

  [Test]
  public void HandleLeft_ReturnsRequestedCharacterCount()
  {
    var result = Invoke("HandleLeft", "abcdef_3");
    Assert.That(result, Is.EqualTo("abc"));
  }

  [Test]
  public void HandleRight_ReturnsRequestedCharacterCount()
  {
    var result = Invoke("HandleRight", "abcdef_2");
    Assert.That(result, Is.EqualTo("ef"));
  }

  [Test]
  public void HandleMid_ReturnsSubstringFromStartAndLength()
  {
    var result = Invoke("HandleMid", "abcdef_2_3");
    Assert.That(result, Is.EqualTo("cde"));
  }

  [Test]
  public void HandleProper_TitleCasesWords()
  {
    var result = Invoke("HandleProper", "hELLo woRLD");
    Assert.That(result, Is.EqualTo("Hello World"));
  }

  [Test]
  public void HandleSmall_UsesOneBasedIndex()
  {
    var result = Invoke("HandleSmall", "2_8_3_5_10");
    Assert.That(result, Is.EqualTo("5"));
  }

  [Test]
  public void HandleLarge_UsesOneBasedIndex()
  {
    var result = Invoke("HandleLarge", "2_8_3_5_10");
    Assert.That(result, Is.EqualTo("8"));
  }

  [Test]
  public void HandleSearch_IsCaseInsensitive_AndSupportsOffset()
  {
    var result = Invoke("HandleSearch", "ab_XXaBxxAB_3", "not-found");
    Assert.That(result, Is.EqualTo("6"));
  }

  [Test]
  public void HandleRank_CountsGreaterValues()
  {
    var result = Invoke("HandleRank", "5_3_8_5_10");
    Assert.That(result, Is.EqualTo("2"));
  }

  [Test]
  public void HandleEqual_StringComparison_IsCaseInsensitive()
  {
    var result = Invoke("HandleEqual", "Player_player");
    Assert.That(result, Is.EqualTo("true"));
  }

  [Test]
  public void HandleNotEqual_NumericComparison_ReturnsFalseForEqualNumbers()
  {
    var result = Invoke("HandleNotEqual", "42_42");
    Assert.That(result, Is.EqualTo("false"));
  }

  [Test]
  public void HandleGreater_ReturnsTrueWhenLeftIsGreater()
  {
    var result = Invoke("HandleGreater", "9_3", "default");
    Assert.That(result, Is.EqualTo("true"));
  }

  [Test]
  public void HandleLessOrEqual_ReturnsTrueWhenEqual()
  {
    var result = Invoke("HandleLessOrEqual", "7_7", "default");
    Assert.That(result, Is.EqualTo("true"));
  }

  [Test]
  public void HandleEven_ReturnsTrueForEvenNumber()
  {
    var result = Invoke("HandleEven", "42", "default");
    Assert.That(result, Is.EqualTo("true"));
  }

  [Test]
  public void HandleOdd_ReturnsTrueForOddNumber()
  {
    var result = Invoke("HandleOdd", "41", "default");
    Assert.That(result, Is.EqualTo("true"));
  }

  [Test]
  public void HandleFindUpper_ReturnsOnlyUppercaseCharacters()
  {
    var result = Invoke("HandleFindUpper", "aBcD123");
    Assert.That(result, Is.EqualTo("BD"));
  }

  [Test]
  public void HandleFindLower_ReturnsOnlyLowercaseCharacters()
  {
    var result = Invoke("HandleFindLower", "aBcD123");
    Assert.That(result, Is.EqualTo("ac"));
  }

  [Test]
  public void Rad2Deg_ConvertsRadiansToDegrees()
  {
    var converted = Parameters.Rad2Deg("3.1415927");
    Assert.That(converted, Is.Not.Null);
    var result = float.Parse(converted!, CultureInfo.InvariantCulture);

    Assert.That(result, Is.EqualTo(180f).Within(0.001f));
  }

  [Test]
  public void Deg2Rad_ConvertsDegreesToRadians()
  {
    var converted = Parameters.Deg2Rad("180");
    Assert.That(converted, Is.Not.Null);
    var result = float.Parse(converted!, CultureInfo.InvariantCulture);

    Assert.That(result, Is.EqualTo(3.1415927f).Within(0.001f));
  }

  [Test]
  public void Vec2Deg_ReturnsAtan2Result()
  {
    var converted = Parameters.Vec2Deg("0_1");
    Assert.That(converted, Is.Not.Null);
    var result = float.Parse(converted!, CultureInfo.InvariantCulture);

    Assert.That(result, Is.EqualTo(1.5707964f).Within(0.001f));
  }

  [Test]
  public void Vec2Rad_AppliesAdditionalDeg2RadFactor()
  {
    var converted = Parameters.Vec2Rad("0_1");
    Assert.That(converted, Is.Not.Null);
    var result = float.Parse(converted!, CultureInfo.InvariantCulture);

    Assert.That(result, Is.EqualTo(0.0274156f).Within(0.001f));
  }

  [Test]
  public void Rad2Vec_ReturnsExpectedDirectionVector()
  {
    var result = Parameters.Rad2Vec("0");
    Assert.That(result, Is.Not.Null);
    var vec = ParseUnityVector(result!);

    Assert.That(vec[0], Is.EqualTo(1f).Within(0.0001f));
    Assert.That(vec[1], Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vec[2], Is.EqualTo(0f).Within(0.0001f));
  }

  [Test]
  public void Deg2Vec_ReturnsExpectedDirectionVector()
  {
    var result = Parameters.Deg2Vec("90");
    Assert.That(result, Is.Not.Null);
    var vec = ParseUnityVector(result!);

    Assert.That(vec[0], Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vec[1], Is.EqualTo(0f).Within(0.0001f));
    Assert.That(vec[2], Is.EqualTo(1f).Within(0.0001f));
  }

  [Test]
  public void HandleAngle_ReturnsAngleBetweenTwoVector3Values()
  {
    var result = InvokeFloat("HandleAngle", "1,0,0_0,0,1");

    Assert.That(result, Is.EqualTo(90f).Within(0.0001f));
  }

  [Test]
  public void HandleAngle_WithoutSecondVector_ReturnsDefault()
  {
    var result = Invoke("HandleAngle", "1,0,0", "default");

    Assert.That(result, Is.EqualTo("default"));
  }

  [Test]
  public void HandleDistance_ReturnsDistanceBetweenTwoVector3Values()
  {
    var result = InvokeFloat("HandleDistance", "0,0,0_3,4,0");

    Assert.That(result, Is.EqualTo(5f).Within(0.0001f));
  }

  [Test]
  public void HandleDistance_WithoutSecondVector_ReturnsDefault()
  {
    var result = Invoke("HandleDistance", "0,0,0", "default");

    Assert.That(result, Is.EqualTo("default"));
  }

  [Test]
  public void HandleDot_ReturnsDotProductBetweenTwoVector3Values()
  {
    var result = InvokeFloat("HandleDot", "1,2,3_4,5,6");

    Assert.That(result, Is.EqualTo(32f).Within(0.0001f));
  }

  [Test]
  public void HandleCross_ReturnsCrossProductBetweenTwoVector3Values()
  {
    var result = Invoke("HandleCross", "1,0,0_0,0,1");

    Assert.That(result, Is.EqualTo("0 1 0"));
  }

  [Test]
  public void HandleNormalize_ReturnsNormalizedVector3Value()
  {
    var result = Invoke("HandleNormalize", "3,4,0");

    Assert.That(result, Is.EqualTo("0.6 0.8 0"));
  }

  [Test]
  public void HandleMagnitude_ReturnsMagnitudeOfVector3Value()
  {
    var result = InvokeFloat("HandleMagnitude", "3,4,0");

    Assert.That(result, Is.EqualTo(5f).Within(0.0001f));
  }

  [Test]
  public void HandleSqrMagnitude_ReturnsSquaredMagnitudeOfVector3Value()
  {
    var result = InvokeFloat("HandleSqrMagnitude", "3,4,0");

    Assert.That(result, Is.EqualTo(25f).Within(0.0001f));
  }

  [Test]
  public void HandleProject_ReturnsProjectedVector3Value()
  {
    var result = Invoke("HandleProject", "3,4,0_1,0,0");

    Assert.That(result, Is.EqualTo("3 0 0"));
  }

  [Test]
  public void HandleReflect_ReturnsReflectedVector3Value()
  {
    var result = Invoke("HandleReflect", "1,-1,0_0,1,0");

    Assert.That(result, Is.EqualTo("1 1 0"));
  }

  [Test]
  public void HandleLerp_ReturnsInterpolatedVector3Value()
  {
    var result = Invoke("HandleLerp", "0,0,0_10,0,0_0.25");

    Assert.That(result, Is.EqualTo("2.5 0 0"));
  }

  [Test]
  public void HandleLerp_WithInvalidT_ReturnsDefault()
  {
    var result = Invoke("HandleLerp", "0,0,0_10,0,0_not-a-number", "default");

    Assert.That(result, Is.EqualTo("default"));
  }

  [Test]
  public void HandleVecX_ReturnsXComponentFromVector3Value()
  {
    var result = InvokeFloat("HandleVecX", "1,2,3");

    Assert.That(result, Is.EqualTo(1f).Within(0.0001f));
  }

  [Test]
  public void HandleVecY_ReturnsYComponentFromVector3Value()
  {
    var result = InvokeFloat("HandleVecY", "1,2,3");

    Assert.That(result, Is.EqualTo(3f).Within(0.0001f));
  }

  [Test]
  public void HandleVecZ_ReturnsZComponentFromVector3Value()
  {
    var result = InvokeFloat("HandleVecZ", "1,2,3");

    Assert.That(result, Is.EqualTo(2f).Within(0.0001f));
  }

  [Test]
  public void HandleVecY_WithInvalidVector_ReturnsDefault()
  {
    var result = Invoke("HandleVecY", "not-a-vector", "default");

    Assert.That(result, Is.EqualTo("default"));
  }

  [Test]
  public void HandleIter_BuildsReduceExpressionForSingleIterator()
  {
    var result = Invoke("HandleIter", "add_0_3_amount_i=-2", "");

    Assert.That(result, Is.EqualTo("<add_<amount_0=-2>_<amount_1=-2>_<amount_2=-2>_<amount_3=-2>>"));
  }

  [Test]
  public void HandleIter_BuildsReduceExpressionForNumericTemplate()
  {
    var result = Invoke("HandleIter", "add_0_10_i", "");

    Assert.That(result, Is.EqualTo("<add_0_1_2_3_4_5_6_7_8_9_10>"));
  }

  [Test]
  public void HandleIter_AppendsOuterDefaultValueToTemplate()
  {
    var result = Invoke("HandleIter", "add_0_3_amount_i", "-2");

    Assert.That(result, Is.EqualTo("<add_<amount_0=-2>_<amount_1=-2>_<amount_2=-2>_<amount_3=-2>>"));
  }

  [Test]
  public void HandleIter2_BuildsReduceExpressionForTwoIterators()
  {
    var result = Invoke("HandleIter2", "add_0_1_0_2_amount_i_j=-1", "");

    Assert.That(result, Is.EqualTo("<add_<amount_0_0=-1>_<amount_1_0=-1>_<amount_0_1=-1>_<amount_1_1=-1>_<amount_0_2=-1>_<amount_1_2=-1>>"));
  }

  [Test]
  public void HandleIter2_AppendsOuterDefaultValueToTemplate()
  {
    var result = Invoke("HandleIter2", "add_0_1_0_1_amount_i_j", "-1");

    Assert.That(result, Is.EqualTo("<add_<amount_0_0=-1>_<amount_1_0=-1>_<amount_0_1=-1>_<amount_1_1=-1>>"));
  }

  [Test]
  public void HandleIter_WithInvalidRange_ReturnsDefault()
  {
    var result = Invoke("HandleIter", "add_3_1_amount_i=missing", "missing");

    Assert.That(result, Is.EqualTo("missing"));
  }

  [Test]
  public void Replace_UsesExecuteCodeForSimpleParameter()
  {
    Parameters.ExecuteCode = key => key == "name" ? "Alice" : null;

    var result = Replace("Hello <name>!");

    Assert.That(result, Is.EqualTo("Hello Alice!"));
  }

  [Test]
  public void Replace_UsesExecuteCodeWithValueForValueParameter()
  {
    Parameters.ExecuteCodeWithValue = (key, value) => key == "tag" ? $"[{value}]" : null;

    var result = Replace("Value: <tag_demo>");

    Assert.That(result, Is.EqualTo("Value: [demo]"));
  }

  [Test]
  public void Replace_ResolvesNestedParametersInsideOut()
  {
    Parameters.ExecuteCode = key => key == "inner" ? "world" : null;
    Parameters.ExecuteCodeWithValue = (key, value) => key == "wrap" ? $"[{value}]" : null;

    var result = Replace("Hello <wrap_<inner>>");

    Assert.That(result, Is.EqualTo("Hello [world]"));
  }

  [Test]
  public void Replace_WithInjectionPrevention_ReplacesSemicolon()
  {
    Parameters.ExecuteCode = key => key == "cmd" ? "say hi;killall" : null;

    var result = Replace("<cmd>", preventInjections: true);

    Assert.That(result, Is.EqualTo("say hi,killall"));
  }

  [Test]
  public void Replace_WithoutInjectionPrevention_KeepsSemicolon()
  {
    Parameters.ExecuteCode = key => key == "cmd" ? "say hi;killall" : null;

    var result = Replace("<cmd>", preventInjections: false);

    Assert.That(result, Is.EqualTo("say hi;killall"));
  }

}