using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using Service;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

[TestFixture, NonParallelizable]
public class ComponentFieldFilterTests
{
  private const string Field = "TestComponent.m_value";
  private const string FireplaceField = "Fireplace.m_infiniteFuel";
  private static readonly int HasFields = ZdoHelper.Hash("HasFields");
  private static readonly int HasTestComponent = ZdoHelper.Hash("HasFieldsTestComponent");
  private static readonly int HasFireplace = ZdoHelper.Hash("HasFieldsFireplace");
  private static readonly FieldInfo IdField = typeof(ZDO).GetField("m_uid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

  private readonly List<ZDOID> ids = [];
  private uint nextId;

  private static Functions CreateFunctions() =>
    (Functions)FormatterServices.GetUninitializedObject(typeof(Functions));

  private ZDO NewZdo()
  {
    var id = new ZDOID(-8401123L, ++nextId);
    var zdo = (ZDO)FormatterServices.GetUninitializedObject(typeof(ZDO));
    IdField.SetValue(zdo, id);
    ids.Add(id);
    return zdo;
  }

  [TearDown]
  public void TearDown()
  {
    foreach (var id in ids)
    {
      ZDOExtraData.s_floats.Remove(id);
      ZDOExtraData.s_ints.Remove(id);
      ZDOExtraData.s_longs.Remove(id);
      ZDOExtraData.s_strings.Remove(id);
      ZDOExtraData.s_vec3.Remove(id);
      ZDOExtraData.s_quats.Remove(id);
      ZDOExtraData.s_byteArrays.Remove(id);
    }
    ids.Clear();
  }

  private static DataYaml Named(string type, string field, string value)
  {
    var entry = new DataYaml();
    var pair = $"{field}, {value}";
    switch (type)
    {
      case "float": entry.floats = [pair]; break;
      case "int": entry.ints = [pair]; break;
      case "bool": entry.bools = [pair]; break;
      case "hash": entry.hashes = [pair]; break;
      case "long": entry.longs = [pair]; break;
      case "string": entry.strings = [pair]; break;
      case "vec":
      case "vec3": entry.vecs = [pair]; break;
      case "quat": entry.quats = [pair]; break;
      case "bytes": entry.bytes = [pair]; break;
      default: throw new ArgumentOutOfRangeException(nameof(type));
    }
    return entry;
  }

  private static void StoreValue(ZDO zdo, DataEntry entry, string type, int key, Functions f)
  {
    var id = zdo.m_uid;
    switch (type)
    {
      case "float":
        ZDOHelper.Init(ZDOExtraData.s_floats, id);
        ZDOExtraData.s_floats[id].SetValue(key, entry.Floats![key].Get(f)!.Value);
        break;
      case "int":
        StoreInt(zdo, key, entry.Ints![key].Get(f)!.Value);
        break;
      case "bool":
        StoreInt(zdo, key, entry.Bools![key].GetInt(f)!.Value);
        break;
      case "hash":
        StoreInt(zdo, key, entry.Hashes![key].Get(f)!.Value);
        break;
      case "long":
        ZDOHelper.Init(ZDOExtraData.s_longs, id);
        ZDOExtraData.s_longs[id].SetValue(key, entry.Longs![key].Get(f)!.Value);
        break;
      case "string":
        ZDOHelper.Init(ZDOExtraData.s_strings, id);
        ZDOExtraData.s_strings[id].SetValue(key, entry.Strings![key].Get(f)!);
        break;
      case "vec":
      case "vec3":
        ZDOHelper.Init(ZDOExtraData.s_vec3, id);
        ZDOExtraData.s_vec3[id].SetValue(key, entry.Vecs![key].Get(f)!.Value);
        break;
      case "quat":
        ZDOHelper.Init(ZDOExtraData.s_quats, id);
        ZDOExtraData.s_quats[id].SetValue(key, entry.Quats![key].Get(f)!.Value);
        break;
      case "bytes":
        ZDOHelper.Init(ZDOExtraData.s_byteArrays, id);
        ZDOExtraData.s_byteArrays[id].SetValue(key, entry.ByteArrays![key].Get(f)!);
        break;
      default: throw new ArgumentOutOfRangeException(nameof(type));
    }
  }

  private static void StoreInt(ZDO zdo, int key, int value)
  {
    ZDOHelper.Init(ZDOExtraData.s_ints, zdo.m_uid);
    ZDOExtraData.s_ints[zdo.m_uid].SetValue(key, value);
  }

  private static Dictionary<int, DataEntry?> IsolateCache(params string[] keys)
  {
    Dictionary<int, DataEntry?> previous = [];
    foreach (var key in keys)
    {
      var hash = key.GetStableHashCode();
      DataLoading.Data.TryGetValue(hash, out var entry);
      previous[hash] = entry;
      DataLoading.Data.Remove(hash);
    }
    return previous;
  }

  private static void RestoreCache(Dictionary<int, DataEntry?> previous)
  {
    foreach (var pair in previous)
    {
      if (pair.Value == null)
        DataLoading.Data.Remove(pair.Key);
      else
        DataLoading.Data[pair.Key] = pair.Value;
    }
  }

  [TestCase("float", "2.5", "3.25")]
  [TestCase("int", "7", "8")]
  [TestCase("bool", "false", "true")]
  [TestCase("hash", "Greydwarf", "Boar")]
  [TestCase("long", "1234567890123", "1234567890124")]
  [TestCase("string", "alpha", "beta")]
  [TestCase("vec", "1,2,3", "4,5,6")]
  [TestCase("vec3", "1,2,3", "4,5,6")]
  // Identity avoids native Quaternion.Euler in the managed test host.
  [TestCase("quat", "", "")]
  [TestCase("bytes", "AQID", "BAUG")]
  public void ComponentField_AllShorthandTypes_MatchLikeNamedData(string type, string value, string otherValue)
  {
    var f = CreateFunctions();
    var shorthand = new DataEntry([type, Field, value]);
    var named = new DataEntry(Named(type, Field, value));

    Assert.That(shorthand.Components, Is.Not.Null);
    Assert.That(named.Components, Is.Not.Null);
    Assert.That(shorthand.Components!.Keys, Is.EquivalentTo(named.Components!.Keys));
    Assert.That(shorthand.Components[HasFields].Get(f), Is.EqualTo(1));
    Assert.That(shorthand.Components[HasTestComponent].Get(f), Is.EqualTo(1));
    Assert.That(shorthand.Ints?.ContainsKey(HasFields) ?? false, Is.False);
    Assert.That(shorthand.Ints?.ContainsKey(HasTestComponent) ?? false, Is.False);
    Assert.That(shorthand.CanBeInjected, Is.False);
    Assert.That(named.CanBeInjected, Is.False);

    // Stored component field, but neither automatic HasFields marker is present in the ZDO.
    var zdo = NewZdo();
    var key = ZdoHelper.Hash(Field);
    StoreValue(zdo, shorthand, type, key, f);
    Assert.That(shorthand.Match(f, zdo), Is.True);
    Assert.That(named.Match(f, zdo), Is.True);

    if (type == "quat")
      ZDOExtraData.s_quats[zdo.m_uid].SetValue(key, new Quaternion(1f, 0f, 0f, 0f));
    else
      StoreValue(zdo, new DataEntry([type, Field, otherValue]), type, key, f);
    Assert.That(shorthand.Match(f, zdo), Is.False);
    Assert.That(named.Match(f, zdo), Is.False);
  }

  [Test]
  public void FilterPipeline_ExplicitHasFieldsZero_StillRestrictsBothForms()
  {
    var zdo = NewZdo();
    var f = CreateFunctions();
    StoreInt(zdo, HasFields, 0);
    StoreInt(zdo, ZdoHelper.Hash(FireplaceField), 0);

    const string namedFilter = "component_field_filter_regression_named";
    const string markerFilter = "int, HasFields, 0";
    const string shorthandFilter = "bool, Fireplace.m_infiniteFuel, false";
    var previous = IsolateCache(namedFilter, markerFilter, shorthandFilter);
    try
    {
      DataLoading.Add(namedFilter.GetStableHashCode(), new DataEntry(Named("bool", FireplaceField, "false")));
      var shortFilters = new Filters([markerFilter, shorthandFilter], null, null);
      var namedFilters = new Filters([markerFilter, namedFilter], null, null);

      Assert.That(shortFilters.Match(f, zdo), Is.True);
      Assert.That(namedFilters.Match(f, zdo), Is.True);

      StoreInt(zdo, HasFields, 1);
      Assert.That(shortFilters.Match(f, zdo), Is.False);
      Assert.That(namedFilters.Match(f, zdo), Is.False);
    }
    finally
    {
      RestoreCache(previous);
    }
  }

  [Test]
  public void ExplicitComponentPresenceChecks_RestorePreviousShorthandRestriction()
  {
    var zdo = NewZdo();
    var f = CreateFunctions();
    StoreInt(zdo, ZdoHelper.Hash(FireplaceField), 0);
    StoreInt(zdo, HasFields, 1);
    StoreInt(zdo, HasFireplace, 1);

    const string hasFieldsFilter = "int, HasFields, 1";
    const string hasFireplaceFilter = "int, HasFieldsFireplace, 1";
    const string valueFilter = "bool, Fireplace.m_infiniteFuel, false";
    var previous = IsolateCache(hasFieldsFilter, hasFireplaceFilter, valueFilter);
    try
    {
      var filters = new Filters([hasFieldsFilter, hasFireplaceFilter, valueFilter], null, null);
      Assert.That(filters.Match(f, zdo), Is.True);

      StoreInt(zdo, HasFireplace, 0);
      Assert.That(filters.Match(f, zdo), Is.False);
    }
    finally
    {
      RestoreCache(previous);
    }
  }

  [Test]
  public void ComponentField_ApplicationStillWritesAutomaticMarkers()
  {
    var f = CreateFunctions();
    var shorthand = new DataEntry(["bool", FireplaceField, "false"]);
    var named = new DataEntry(Named("bool", FireplaceField, "false"));
    var resolvedShort = new ResolvedDataEntry();
    var resolvedNamed = new ResolvedDataEntry();

    resolvedShort.Load(shorthand, f, null);
    resolvedNamed.Load(named, f, null);

    foreach (var resolved in new[] { resolvedShort, resolvedNamed })
    {
      Assert.That(resolved.Ints, Is.Not.Null);
      Assert.That(resolved.Ints![HasFields], Is.EqualTo(1));
      Assert.That(resolved.Ints[HasFireplace], Is.EqualTo(1));
      Assert.That(resolved.Ints[ZdoHelper.Hash(FireplaceField)], Is.EqualTo(0));
    }
  }
}
