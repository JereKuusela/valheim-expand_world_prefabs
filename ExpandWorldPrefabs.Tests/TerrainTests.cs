using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ExpandWorld.Prefab;
using NUnit.Framework;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

public class TerrainTests
{
  [Test]
  public void SerializedTerrainRoundTripsNativePayload()
  {
    const int width = 64;
    var count = (width + 1) * (width + 1);
    var heights = Enumerable.Repeat(10f, count).ToArray();
    var mask = Enumerable.Repeat(new Color(0f, 0f, 0f, 1f), count).ToArray();
    var state = SerializedTerrain.TerrainState.Load(null, heights, mask,
      Vector3.zero, width, 1f);
    var settings = new TerrainOp.Settings
    {
      m_level = true,
      m_levelRadius = 2f,
      m_smooth = true,
      m_smoothRadius = 3f,
      m_smoothPower = 3f
    };

    Assert.That(state.Apply(new Vector3(0f, 12f, 0f), settings), Is.GreaterThan(0));
    state.Operations++;
    state.LastOperationPoint = new Vector3(0f, 12f, 0f);
    state.LastOperationRadius = settings.GetRadius();
    var first = state.Save();
    var restored = SerializedTerrain.TerrainState.Load(Utils.Compress(first),
      heights, mask, Vector3.zero, width, 1f);

    Assert.That(restored.Save(), Is.EqualTo(first));
    Assert.That(restored.Operations, Is.EqualTo(1));
    Assert.That(restored.SampleBaseHeight(Vector3.zero), Is.EqualTo(10f));
    Assert.That(restored.SampleHeight(Vector3.zero), Is.EqualTo(13f).Within(0.001f));
  }

  [Test]
  public void SerializedTerrainAppliesDeltasToLocationReferenceSurface()
  {
    const int width = 64;
    const int pitch = width + 1;
    var count = pitch * pitch;
    var rawHeights = Enumerable.Repeat(10f, count).ToArray();
    var referenceHeights = rawHeights.ToArray();
    referenceHeights[32 * pitch + 32] = 14f;
    var mask = Enumerable.Repeat(new Color(0f, 0f, 0f, 1f), count).ToArray();
    var state = SerializedTerrain.TerrainState.Load(null, rawHeights, mask,
      referenceHeights, mask, Vector3.zero, width, 1f);
    var settings = new TerrainOp.Settings
    {
      m_level = true,
      m_levelRadius = 0f,
      m_square = true
    };

    Assert.That(state.Apply(new Vector3(0f, 12f, 0f), settings),
      Is.GreaterThan(0));
    Assert.That(state.SampleBaseHeight(Vector3.zero), Is.EqualTo(10f));
    Assert.That(state.SampleReferenceHeight(Vector3.zero), Is.EqualTo(14f));
    Assert.That(state.SampleHeight(Vector3.zero), Is.EqualTo(12f).Within(0.001f));

    var payload = Utils.Compress(state.Save());
    var restored = SerializedTerrain.TerrainState.Load(payload, rawHeights,
      mask, referenceHeights, mask, Vector3.zero, width, 1f);
    var rawOnly = SerializedTerrain.TerrainState.Load(payload, rawHeights,
      mask, Vector3.zero, width, 1f);

    Assert.That(restored.SampleHeight(Vector3.zero), Is.EqualTo(12f).Within(0.001f));
    Assert.That(rawOnly.SampleHeight(Vector3.zero), Is.EqualTo(8f).Within(0.001f));
  }

  [Test]
  public void DedicatedRouteRequiresSerializedFallbackWithoutLiveHeightmap()
  {
    Assert.That(TerrainManager.RequiresSerializedPath(false, false, false, false, false),
      Is.True);
    Assert.That(TerrainManager.RequiresSerializedPath(true, true, false, false, true),
      Is.True);
    Assert.That(TerrainManager.RequiresSerializedPath(true, true, true, true, true),
      Is.False);
  }
}

[NonParallelizable]
public class TerrainProxyTests
{
  private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
  private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
  private static readonly FieldInfo ManagerInstance = typeof(ZDOMan).GetField("s_instance", Static)!;
  private static readonly FieldInfo ZdoId = typeof(ZDO).GetField("m_uid", Instance)!;
  private static readonly MethodInfo FindProxy = typeof(TerrainReference).GetMethod("FindLocationProxy", Static)!;
  private static readonly int BaseLocation = "CharredRuins3".GetStableHashCode();
  private static readonly int VariantLocation = "CharredRuins3:Pocket290".GetStableHashCode();
  private readonly List<ZDO> proxies = [];
  private object? previousManager;

  [SetUp]
  public void SetUp()
  {
    previousManager = ManagerInstance.GetValue(null);
    var manager = (ZDOMan)FormatterServices.GetUninitializedObject(typeof(ZDOMan));
    manager.m_objectsBySector = new List<ZDO>[512 * 512];
    manager.m_objectsBySector[ZoneSystem.SectorToIndex(ZoneSystem.GetZone(Vector3.zero)).Sector] = proxies;
    ManagerInstance.SetValue(null, manager);
  }

  [TearDown]
  public void TearDown()
  {
    ManagerInstance.SetValue(null, previousManager);
    foreach (var proxy in proxies)
      ZDOExtraData.s_ints.Remove(proxy.m_uid);
    proxies.Clear();
  }

  [TestCase(false)]
  [TestCase(true)]
  public void LocationReferenceSelectsVariantInsteadOfColocatedHelper(bool helperFirst)
  {
    var ruin = NewProxy(BaseLocation, VariantLocation);
    NewProxy(0);
    if (helperFirst) proxies.Reverse();

    Assert.That(FindLocationProxy(VariantLocation), Is.SameAs(ruin));
  }

  [Test]
  public void LocationHashSelectsNormalLocationWithoutReference()
  {
    NewProxy(0);
    var ruin = NewProxy(BaseLocation);

    Assert.That(FindLocationProxy(BaseLocation), Is.SameAs(ruin));
  }

  [Test]
  public void UnmatchedColocatedProxiesRemainAmbiguous()
  {
    NewProxy(0);
    NewProxy(BaseLocation);

    Assert.That(FindLocationProxy(VariantLocation), Is.Null);
  }

  private ZDO NewProxy(int location, int? reference = null)
  {
    var proxy = (ZDO)FormatterServices.GetUninitializedObject(typeof(ZDO));
    ZdoId.SetValue(proxy, new ZDOID(-7301009L, (uint)proxies.Count + 1));
    proxy.m_prefab = "LocationProxy".GetStableHashCode();
    proxy.m_position = Vector3.zero;
    proxies.Add(proxy);
    // Populate incoming data without invoking native ownership/revision writes.
    ZDOHelper.Init(ZDOExtraData.s_ints, proxy.m_uid);
    ZDOExtraData.s_ints[proxy.m_uid].SetValue(ZDOVars.s_location, location);
    if (reference.HasValue)
      ZDOExtraData.s_ints[proxy.m_uid].SetValue("locationreference".GetStableHashCode(), reference.Value);
    return proxy;
  }

  private static ZDO? FindLocationProxy(int location) =>
    (ZDO?)FindProxy.Invoke(null, [Vector3.zero, location]);
}
