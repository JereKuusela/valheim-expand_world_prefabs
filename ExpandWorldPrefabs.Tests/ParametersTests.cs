using Data;
using NUnit.Framework;
using System.Globalization;
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
  public void HandleSub_SubtractsFromFirstValue()
  {
    var result = InvokeFloat("HandleSub", "10_2_1.5");
    Assert.That(result, Is.EqualTo(6.5f).Within(0.0001f));
  }

  [Test]
  public void HandleMul_MultipliesValues()
  {
    var result = InvokeFloat("HandleMul", "2_3_4");
    Assert.That(result, Is.EqualTo(24f).Within(0.0001f));
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