using System;
using System.ComponentModel;
using Data;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class TerrainYaml
{
  [DefaultValue(null)]
  public string? delay;
  [DefaultValue(null)]
  public string? pos;
  [DefaultValue(null)]
  public string? position;
  [DefaultValue(null)]
  public string? square;
  [DefaultValue(null)]
  public string? resetRadius;
  [DefaultValue(null)]
  public string? levelRadius;
  [DefaultValue(null)]
  public string? levelOffset;
  [DefaultValue(null)]
  public string? raiseRadius;
  [DefaultValue(null)]
  public string? raisePower;
  [DefaultValue(null)]
  public string? raiseDelta;
  [DefaultValue(null)]
  public string? smoothRadius;
  [DefaultValue(null)]
  public string? smoothPower;
  [DefaultValue(null)]
  public string? paintRadius;
  [DefaultValue(null)]
  public string? paintHeightCheck;
  [DefaultValue(null)]
  public string? paint;
}

public class Terrain(TerrainYaml data)
{
  public readonly IFloatValue? Delay = data.delay == null ? null : DataValue.Float(data.delay);
  public readonly IFloatValue? ResetRadius = data.resetRadius == null ? null : DataValue.Float(data.resetRadius);
  public readonly IVector3Value? Position = data.pos != null ? DataValue.Vector3(data.pos) : data.position != null ? DataValue.Vector3(data.position) : null;
  public readonly IBoolValue? Square = data.square == null ? null : DataValue.Bool(data.square);
  public readonly IFloatValue? LevelRadius = data.levelRadius == null ? null : DataValue.Float(data.levelRadius);
  public readonly IFloatValue? LevelOffset = data.levelOffset == null ? null : DataValue.Float(data.levelOffset);
  public readonly IFloatValue? RaiseRadius = data.raiseRadius == null ? null : DataValue.Float(data.raiseRadius);
  public readonly IFloatValue? RaisePower = data.raisePower == null ? null : DataValue.Float(data.raisePower);
  public readonly IFloatValue? RaiseDelta = data.raiseDelta == null ? null : DataValue.Float(data.raiseDelta);
  public readonly IFloatValue? SmoothRadius = data.smoothRadius == null ? null : DataValue.Float(data.smoothRadius);
  public readonly IFloatValue? SmoothPower = data.smoothPower == null ? null : DataValue.Float(data.smoothPower);
  public readonly IFloatValue? PaintRadius = data.paintRadius == null ? null : DataValue.Float(data.paintRadius);
  public readonly IBoolValue? PaintHeightCheck = data.paintHeightCheck == null ? null : DataValue.Bool(data.paintHeightCheck);
  public readonly IStringValue? Paint = data.paint == null ? null : DataValue.String(data.paint);

  public void Get(Functions f, Vector3 basePosition, Quaternion baseRotation, out Vector3 pos, out float size, out float resetRadius, out TerrainOp.Settings settings)
  {
    pos = basePosition;
    pos += baseRotation * (Position?.Get(f) ?? Vector3.zero);
    var levelRadius = LevelRadius?.Get(f) ?? 0f;
    var raiseRadius = RaiseRadius?.Get(f) ?? 0f;
    var smoothRadius = SmoothRadius?.Get(f) ?? 0f;
    var paintRadius = PaintRadius?.Get(f) ?? 0f;
    var paint = Paint?.Get(f) ?? "Reset";
    var paintEnum =
      Enum.TryParse(paint, true, out TerrainModifier.PaintType paintType) ? paintType :
      int.TryParse(paint, out var paintInt) ? (TerrainModifier.PaintType)paintInt :
      TerrainModifier.PaintType.Reset;
    settings = new TerrainOp.Settings
    {
      m_levelOffset = LevelOffset?.Get(f) ?? 0f,
      m_level = levelRadius > 0f,
      m_levelRadius = levelRadius,
      m_square = Square?.GetBool(f) == true,
      m_raise = raiseRadius > 0f,
      m_raiseRadius = raiseRadius,
      m_raisePower = RaisePower?.Get(f) ?? 0f,
      m_raiseDelta = RaiseDelta?.Get(f) ?? 0f,
      m_smooth = smoothRadius > 0f,
      m_smoothRadius = smoothRadius,
      m_smoothPower = SmoothPower?.Get(f) ?? 0f,
      m_paintCleared = paintRadius > 0f,
      m_paintHeightCheck = PaintHeightCheck?.GetBool(f) == true,
      m_paintType = paintEnum,
      m_paintRadius = paintRadius
    };
    resetRadius = ResetRadius?.Get(f) ?? 0f;
    size = Mathf.Max(levelRadius, raiseRadius, smoothRadius, paintRadius, resetRadius);
  }
}
