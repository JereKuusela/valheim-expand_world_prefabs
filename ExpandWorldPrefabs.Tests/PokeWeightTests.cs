using System.Globalization;
using System.Runtime.Serialization;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class PokeWeightTests
{
  [TestCase("0.2", 0.2f)]
  [TestCase("0.8", 0.8f)]
  [TestCase("0.001", 0.001f)]
  [TestCase("1e-3", 0.001f)]
  [TestCase("1.0", 1f)]
  [TestCase("2", 2f)]
  [TestCase("8", 8f)]
  [TestCase("0", 0f)]
  public void NestedWeightPreservesFraction(string text, float expected)
  {
#pragma warning disable SYSLIB0050
    var functions = (Functions)FormatterServices.GetUninitializedObject(typeof(Functions));
#pragma warning restore SYSLIB0050
    var previous = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
      // No target prefab: this test exercises weight parsing, not the game registry.
      var poke = new Poke(new PokeData { weight = text });
      Assert.That(poke.Weight, Is.InstanceOf<IFloatValue>());
      Assert.That(poke.Weight!.Get(functions), Is.EqualTo(expected));
    }
    finally { CultureInfo.CurrentCulture = previous; }
  }
}
