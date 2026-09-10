using ExpandWorld.Prefab;
using NUnit.Framework;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

public class TerrainProtocolTests
{
  [Test]
  public void EwpPacketRoundTripsDynamicTerrainSettings()
  {
    var expectedPosition = new Vector3(12.5f, -3f, 88f);
    var expected = new TerrainOp.Settings
    {
      m_levelOffset = 1.25f,
      m_level = true,
      m_levelRadius = 9f,
      m_square = true,
      m_raise = true,
      m_raiseRadius = 8f,
      m_raisePower = 0.7f,
      m_raiseDelta = -2f,
      m_smooth = true,
      m_smoothRadius = 7f,
      m_smoothPower = 0.4f,
      m_paintCleared = true,
      m_paintHeightCheck = true,
      m_paintType = TerrainModifier.PaintType.Dirt,
      m_paintRadius = 6f
    };

    var pkg = TerrainProtocol.Write(expectedPosition, expected);
    pkg.SetPos(0);

    var result = TerrainProtocol.Read(pkg, out var position, out var actual);

    Assert.That(result, Is.EqualTo(TerrainPacketResult.Ewp));
    Assert.That(actual, Is.Not.Null);
    Assert.That(position, Is.EqualTo(expectedPosition));
    Assert.That(actual!.m_levelOffset, Is.EqualTo(expected.m_levelOffset));
    Assert.That(actual.m_level, Is.EqualTo(expected.m_level));
    Assert.That(actual.m_levelRadius, Is.EqualTo(expected.m_levelRadius));
    Assert.That(actual.m_square, Is.EqualTo(expected.m_square));
    Assert.That(actual.m_raise, Is.EqualTo(expected.m_raise));
    Assert.That(actual.m_raiseRadius, Is.EqualTo(expected.m_raiseRadius));
    Assert.That(actual.m_raisePower, Is.EqualTo(expected.m_raisePower));
    Assert.That(actual.m_raiseDelta, Is.EqualTo(expected.m_raiseDelta));
    Assert.That(actual.m_smooth, Is.EqualTo(expected.m_smooth));
    Assert.That(actual.m_smoothRadius, Is.EqualTo(expected.m_smoothRadius));
    Assert.That(actual.m_smoothPower, Is.EqualTo(expected.m_smoothPower));
    Assert.That(actual.m_paintCleared, Is.EqualTo(expected.m_paintCleared));
    Assert.That(actual.m_paintHeightCheck, Is.EqualTo(expected.m_paintHeightCheck));
    Assert.That(actual.m_paintType, Is.EqualTo(expected.m_paintType));
    Assert.That(actual.m_paintRadius, Is.EqualTo(expected.m_paintRadius));
  }

  [Test]
  public void NativeDeepNorthPacketIsNotConsumed()
  {
    var pkg = new ZPackage();
    pkg.Write(new Vector3(2f, 3f, 4f));
    pkg.Write(false);
    pkg.Write("native_terrain_op".GetStableHashCode());
    pkg.SetPos(0);

    var result = TerrainProtocol.Read(pkg, out _, out _);

    Assert.That(result, Is.EqualTo(TerrainPacketResult.Native));
    Assert.That(pkg.GetPos(), Is.EqualTo(0));
  }

  [Test]
  public void UnsupportedEwpPacketVersionFailsClosed()
  {
    var pkg = new ZPackage();
    pkg.Write(TerrainProtocol.Marker);
    pkg.Write(TerrainProtocol.Marker2);
    pkg.Write(TerrainProtocol.Version + 1);
    pkg.SetPos(0);

    var result = TerrainProtocol.Read(pkg, out _, out _);

    Assert.That(result, Is.EqualTo(TerrainPacketResult.InvalidEwp));
  }
}
