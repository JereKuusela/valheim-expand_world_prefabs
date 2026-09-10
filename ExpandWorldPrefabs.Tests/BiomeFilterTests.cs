using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

[NonParallelizable]
public class BiomeFilterTests
{
  private List<AltBiome> previousAlts = null!;
  private ZoneSystem? previousZoneSystem;
  private AltBiome kalhygge = null!;
  private AltBiome blueberry = null!;
  private static readonly MethodInfo LoadRule = typeof(Loading).GetMethod("FromData", BindingFlags.Static | BindingFlags.NonPublic)!;

  private static T Uninitialized<T>()
  {
#pragma warning disable SYSLIB0050
    return (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
  }

  private static AltBiome Alt(string name)
  {
    var alt = Uninitialized<AltBiome>();
    alt.m_name = name;
    alt.m_biome = Heightmap.Biome.BlackForest;
    return alt;
  }

  [SetUp]
  public void SetUp()
  {
    previousAlts = [.. AltBiomeList.m_altBiomes];
    AltBiomeList.m_altBiomes.Clear();
    kalhygge = Alt("Kalhygge Black Forest");
    blueberry = Alt("Blueberry Black Forest");
    AltBiomeList.m_altBiomes.AddRange([kalhygge, blueberry]);
    previousZoneSystem = ZoneSystem.s_instance;
    ZoneSystem.s_instance = Uninitialized<ZoneSystem>();
  }

  [TearDown]
  public void TearDown()
  {
    AltBiomeList.m_altBiomes.Clear();
    AltBiomeList.m_altBiomes.AddRange(previousAlts);
    ZoneSystem.s_instance = previousZoneSystem;
  }

  private static Info Load(string biomes = "", string bannedBiomes = "") =>
    ((Info[])LoadRule.Invoke(null, [new ExpandWorld.Prefab.Data
    {
      prefab = "Player", type = "create", biomes = biomes, bannedBiomes = bannedBiomes
    }])!).Single();

  [Test]
  public void BaseBiome_StillIncludesItsAlternateBiomes()
  {
    var rule = Load("BlackForest");
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, []), Is.True);
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [kalhygge]), Is.True);
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.Meadows, []), Is.False);
    Assert.That(rule.AltBiomes, Is.Null);
  }

  [Test]
  public void AlternateOnly_DoesNotMatchOrdinaryParentBiome()
  {
    var rule = Load("kalhygge black forest");
    Assert.That(rule.Biomes, Is.EqualTo(Heightmap.Biome.None));
    Assert.That(rule.AltBiomes, Is.EquivalentTo(new[] { "Kalhygge Black Forest" }));
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [kalhygge]), Is.True);
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, []), Is.False);
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [blueberry]), Is.False);
  }

  [Test]
  public void MixedList_MatchesEitherBaseOrAlternate()
  {
    var rule = Load("Meadows, Kalhygge Black Forest");
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.Meadows, []), Is.True);
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [kalhygge]), Is.True);
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, []), Is.False);
  }

  [Test]
  public void OverlappingAlternates_AnyBannedMatchBlocksRule()
  {
    var rule = Load("Kalhygge Black Forest", "Blueberry Black Forest");
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [kalhygge]), Is.True);
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [kalhygge, blueberry]), Is.False);
    Assert.That(InfoSelector.CheckBiomes(Load("Kalhygge Black Forest", "BlackForest"), Heightmap.Biome.BlackForest, [kalhygge]), Is.False);
  }

  [Test]
  public void EmptyFilters_KeepExistingDefaults()
  {
    var rule = Load();
    Assert.That(rule.Biomes, Is.EqualTo((Heightmap.Biome)(-1)));
    Assert.That(rule.BannedBiomes, Is.EqualTo(Heightmap.Biome.None));
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [kalhygge, blueberry]), Is.True);
  }

  [TestCase("")]
  [TestCase(" ")]
  [TestCase("Meadows,BlackForest")]
  [TestCase("8")]
  [TestCase("None")]
  public void BaseParsing_PreservesExistingMasks(string value)
  {
    Assert.That(Yaml.ToBiomeFilter(value, true, out var alts), Is.EqualTo(Yaml.ToBiomes(value, true)));
    Assert.That(alts, Is.Null);
  }

  [Test]
  public void UnknownName_StillFailsValidation()
  {
    Assert.Throws<InvalidOperationException>(() => Yaml.ToBiomeFilter("Kalhygge typo", true, out _));
  }

  [Test]
  public void AlternateList_UsesExactNamesWithoutDuplicates()
  {
    var rule = Load("Kalhygge Black Forest, kalhygge black forest, Blueberry Black Forest");
    Assert.That(rule.AltBiomes, Has.Count.EqualTo(2));
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [blueberry]), Is.True);
  }

  [Test]
  public void BiomeFunction_ReturnsBaseWhenNoAlternatesExist()
  {
    Assert.That(ObjectFunctions.GetBiomeName(Heightmap.Biome.Meadows, []), Is.EqualTo("Meadows"));
  }

  [Test]
  public void BiomeFunction_ReturnsExactSingleAlternateName()
  {
    Assert.That(ObjectFunctions.GetBiomeName(Heightmap.Biome.BlackForest, [kalhygge]), Is.EqualTo("Kalhygge Black Forest"));
  }

  [Test]
  public void BiomeFunction_ListsAllOverlappingNamesInStableOrder()
  {
    var value = ObjectFunctions.GetBiomeName(Heightmap.Biome.BlackForest, [kalhygge, blueberry, kalhygge]);
    Assert.That(value, Is.EqualTo("Blueberry Black Forest, Kalhygge Black Forest"));
    var rule = Load(value);
    Assert.That(InfoSelector.CheckBiomes(rule, Heightmap.Biome.BlackForest, [kalhygge, blueberry]), Is.True);
  }
}
