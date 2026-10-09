using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

public class ExcludePrefabTests
{
  private static readonly string[] Names = ["Boar", "Boar_piggy", "Greydwarf", "Wolf"];
  private static readonly FieldInfo CacheField = typeof(PrefabHelper).GetField("PrefabCache", BindingFlags.NonPublic | BindingFlags.Static)!;

  [OneTimeSetUp]
  public void AddGroup() => ValueGroups.Add(new DataYaml { valueGroup = "ExcludeTestGroup", values = ["Wolf", "Greydwarf"] });

  [SetUp]
  public void SetUp()
  {
    PrefabHelper.ClearCache();
    var cache = new Dictionary<string, int>();
    foreach (var name in Names) cache[name] = name.GetStableHashCode();
    CacheField.SetValue(null, cache);
  }

  [TearDown]
  public void TearDown() => PrefabHelper.ClearCache();

  private static Functions CreateFunctions() => (Functions)FormatterServices.GetUninitializedObject(typeof(Functions));

  private static ZDO CreateZdo(string prefab)
  {
    var zdo = (ZDO)FormatterServices.GetUninitializedObject(typeof(ZDO));
    zdo.m_prefab = prefab.GetStableHashCode();
    return zdo;
  }

  private static bool IsValid(ObjectYaml data, string prefab)
  {
    var f = CreateFunctions();
    var filter = new ExpandWorld.Prefab.Object(data);
    filter.Roll(f, Vector3.zero, Quaternion.identity);
    return filter.IsValid(CreateZdo(prefab), f, null);
  }

  [TestCase("Boar", true)]
  [TestCase("Boar_piggy", false)]
  [TestCase("Wolf", false)]
  public void Wildcard_SkipsExcludedPrefabs(string prefab, bool expected)
  {
    var data = new ObjectYaml { prefab = "Boar*,Wolf", excludePrefab = "*piggy,Wolf" };
    Assert.That(IsValid(data, prefab), Is.EqualTo(expected));
  }

  [TestCase("Wolf", false)]
  [TestCase("Greydwarf", false)]
  [TestCase("Boar", true)]
  public void ValueGroup_SkipsExcludedPrefabs(string prefab, bool expected)
  {
    var data = new ObjectYaml { prefab = "*", excludePrefab = "ExcludeTestGroup" };
    Assert.That(IsValid(data, prefab), Is.EqualTo(expected));
  }

  [Test]
  public void WithoutPrefabFilter_ExcludesOnlyListedPrefabs()
  {
    var data = new ObjectYaml { excludePrefab = "Wolf" };
    Assert.That(IsValid(data, "Wolf"), Is.False);
    Assert.That(IsValid(data, "Boar"), Is.True);
  }

  [TestCase("Boar", true)]
  [TestCase("Boar_piggy", false)]
  public void Poke_SkipsExcludedPrefabs(string prefab, bool expected)
  {
    var poke = new Poke(new PokeYaml { prefab = "Boar*", excludePrefab = "Boar_piggy" });
    var f = CreateFunctions();
    poke.Filter.Roll(f, Vector3.zero, Quaternion.identity);
    Assert.That(poke.Filter.IsValid(CreateZdo(prefab), f, null), Is.EqualTo(expected));
  }

  [TestCase("Boar", true)]
  [TestCase("Boar_piggy", true)]
  [TestCase("Wolf", false)]
  public void NoExclude_KeepsPrefabFilterBehavior(string prefab, bool expected)
  {
    var data = new ObjectYaml { prefab = "Boar*" };
    Assert.That(IsValid(data, prefab), Is.EqualTo(expected));
  }
}
