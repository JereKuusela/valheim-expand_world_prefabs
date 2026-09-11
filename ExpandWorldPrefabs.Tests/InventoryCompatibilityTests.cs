using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using BepInEx.Logging;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using Service;

namespace ExpandWorldPrefabs.Tests;

[NonParallelizable]
public class InventoryCompatibilityTests
{
  private ZDO zdo = null!;
  private Functions functions = null!;
  private readonly List<(FieldInfo Field, object? Value)> previousStatics = [];
  private ManualLogSource logger = null!;

  private static T Uninitialized<T>()
  {
#pragma warning disable SYSLIB0050
    return (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
  }

  [SetUp]
  public void SetUp()
  {
    SetStatic(typeof(Config), "ConfigServerSideData", Uninitialized<ConfigEntry<bool>>());
    logger = new ManualLogSource("InventoryCompatibilityTests");
    SetStatic(typeof(Log), "Logger", logger);
    Functions.ExecuteCode = _ => null;
    Functions.ExecuteCodeWithValue = (_, _) => null;
    functions = Uninitialized<Functions>();
    SetStatic(typeof(ZNet), "m_instance", Uninitialized<ZNet>());
    var manager = Uninitialized<ZDOMan>();
    typeof(ZDOMan).GetField("m_clientChangeQueue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(manager, new HashSet<ZDOID>());
    SetStatic(typeof(ZDOMan), "s_instance", manager);
    var game = Uninitialized<Game>();
    typeof(Game).GetField("<PortalPrefabHash>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(game, new List<int>());
    SetStatic(typeof(Game), "<instance>k__BackingField", game);
    zdo = Uninitialized<ZDO>();
    zdo.m_uid = new ZDOID(987654321L, 1U);
    ZDOHelper.Init(ZDOExtraData.s_strings, zdo.m_uid);
    ZDOHelper.Init(ZDOExtraData.s_byteArrays, zdo.m_uid);
  }

  [TearDown]
  public void TearDown()
  {
    if (zdo != null)
    {
      ZDOExtraData.s_strings.Remove(zdo.m_uid);
      ZDOExtraData.s_byteArrays.Remove(zdo.m_uid);
    }
    Functions.ExecuteCode = _ => null;
    Functions.ExecuteCodeWithValue = (_, _) => null;
    logger?.Dispose();
    foreach (var previous in previousStatics) previous.Field.SetValue(null, previous.Value);
    previousStatics.Clear();
  }

  private void SetStatic(Type type, string name, object value)
  {
    var field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    previousStatics.Add((field, field.GetValue(null)));
    field.SetValue(null, value);
  }

  private void Populate()
  {
    ZDOExtraData.s_byteArrays[zdo.m_uid][ZDOVars.s_items] = [1, 2, 3];
  }

  private void Apply(DataData data)
  {
    var entry = new DataEntry(data);
    var write = new ZdoEntry(zdo);
    write.Load(entry, functions);
    write.Write(zdo);
  }

  private string Snapshot(string key = "items", string fallback = "fallback")
  {
    var f = Uninitialized<ObjectFunctions>();
    typeof(ObjectFunctions).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
      .Single(field => field.FieldType == typeof(ZDO)).SetValue(f, zdo);
    return f.Replace("<string_" + key + "=" + fallback + ">");
  }

  [TestCase(false)]
  [TestCase(true)]
  public void NamedInventory_ReplacesRawBytes(bool explicitBytes)
  {
    Populate();
    var payload = ItemValue.LoadItemBytes(functions, [], new Vector2i(2, 2), 0);
    var value = "items, " + Convert.ToBase64String(payload);
    Apply(explicitBytes ? new DataData { bytes = [value] } : new DataData { strings = [value] });
    Assert.That(zdo.GetByteArray(ZDOVars.s_items), Is.EqualTo(payload));
  }

  [Test]
  public void Snapshot_ReadsRawInventoryAndCanRestoreIt()
  {
    ZDOExtraData.s_byteArrays[zdo.m_uid][ZDOVars.s_items] = [4, 5, 6];
    var snapshot = Snapshot();
    Assert.That(snapshot, Is.EqualTo("BAUG"));
    Populate();
    Apply(new DataData { strings = ["items, " + snapshot] });
    Assert.That(zdo.GetByteArray(ZDOVars.s_items), Is.EqualTo(new byte[] { 4, 5, 6 }));
  }

  [Test]
  public void Snapshot_AbsentInventoryUsesDefault()
  {
    Assert.That(Snapshot(fallback: "missing"), Is.EqualTo("missing"));
  }

  [Test]
  public void UnrelatedStrings_KeepTheirStorageAndReadBehavior()
  {
    Apply(new DataData { strings = ["text, items unchanged", "ewp_items, custom inventory"] });
    Assert.That(Snapshot("text"), Is.EqualTo("items unchanged"));
    Assert.That(Snapshot("ewp_items"), Is.EqualTo("custom inventory"));
    Assert.That(zdo.GetByteArray(ZDOVars.s_items, null), Is.Null);
    Assert.That(DataValue.Bytes("").Get(functions), Is.Null);
  }

  [Test]
  public void HighLevelItems_StillRollsToRawInventory()
  {
    Populate();
    try
    {
      Apply(new DataData { items = [new ItemData { prefab = "<none>", stack = "0", pos = "0,0" }], containerSize = "2,2" });
    }
    catch (System.Security.SecurityException error) when (error.Message.Contains("ECall"))
    {
      Assert.Ignore("Item rolling requires Unity's native random API. Run this case inside Unity.");
    }
    var package = new ZPackage(zdo.GetByteArray(ZDOVars.s_items));
    Assert.That(package.ReadInt(), Is.EqualTo(InventoryStorage.FormatVersion));
    Assert.That(package.ReadUShort(), Is.Zero);
    Assert.That(ZDOExtraData.s_strings.TryGetValue(zdo.m_uid, out var values) && values.ContainsKey(ZDOVars.s_items), Is.False);
  }
}
