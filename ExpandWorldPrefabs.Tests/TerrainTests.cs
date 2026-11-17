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
