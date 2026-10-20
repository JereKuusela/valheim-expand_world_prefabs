using Data;
using NUnit.Framework;
using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;

namespace ExpandWorldPrefabs.Tests;

public class TimeFormatTests
{
  private static string Realtime(string value)
  {
#pragma warning disable SYSLIB0050
    var instance = (Functions)FormatterServices.GetUninitializedObject(typeof(Functions));
#pragma warning restore SYSLIB0050
    var method = typeof(Functions).GetMethod("HandleRealtime", BindingFlags.Instance | BindingFlags.NonPublic);
    return (string)method!.Invoke(instance, new object[] { value })!;
  }

  private static void AssertMatches(string value, string customFormat)
  {
    var before = DateTimeOffset.UtcNow.ToString(customFormat, CultureInfo.InvariantCulture);
    var result = Realtime(value + "_0");
    var after = DateTimeOffset.UtcNow.ToString(customFormat, CultureInfo.InvariantCulture);
    Assert.That(result, Is.EqualTo(before).Or.EqualTo(after));
  }

  [TestCase("h", "%h")]
  [TestCase("H", "%H")]
  [TestCase("m", "%m")]
  [TestCase("s", "%s")]
  [TestCase("d", "%d")]
  public void Realtime_SingleLetter_IsCustomSpecifier(string value, string customFormat)
  {
    AssertMatches(value, customFormat);
  }

  [Test]
  public void Realtime_LongerFormat_IsUnchanged()
  {
    AssertMatches("HH:mm", "HH:mm");
    AssertMatches("%H", "%H");
  }
}
