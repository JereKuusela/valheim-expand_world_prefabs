using BepInEx.Logging;
using Data;
using NUnit.Framework;
using Service;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

public class BlueprintParsingTests
{
  [OneTimeSetUp]
  public void Setup() => Log.Init(new ManualLogSource("BlueprintParsingTests"));

  private static Blueprint Parse(params string[] rows) => Blueprints.GetPlanBuild(new("test"), rows)!;

  [Test]
  public void DefaultObjectDataFilter_StripsBuilderAndTemporaryDataFromCopy()
  {
    DataEntry source = new()
    {
      Longs = new() { [ZDOVars.s_creator] = DataValue.Constant(123L) },
      Strings = new()
      {
        [ZDOVars.s_author] = DataValue.Constant("author"),
        [ZDOVars.s_authorDisplayName] = DataValue.Constant("display name"),
        [ZDOVars.s_text] = DataValue.Constant("sign text")
      },
      Floats = new()
      {
        [ZDOVars.s_support] = DataValue.Constant(1e19f),
        [ZDOVars.s_scaleScalarHash] = DataValue.Constant(2f)
      },
      Vecs = new() { [ZDOVars.s_scaleHash] = DataValue.Constant(new Vector3(2, 3, 4)) }
    };

    var data = Blueprints.FilterObjectData(source, Blueprints.RemoveCreatorData())!;

    Assert.That(data.Longs, Is.Empty);
    Assert.That(data.Floats, Is.Empty);
    Assert.That(data.Vecs, Is.Empty);
    Assert.That(data.Strings!.Keys, Is.EquivalentTo(new[] { ZDOVars.s_text }));
    Assert.That(source.Longs, Has.Count.EqualTo(1));
    Assert.That(source.Strings, Has.Count.EqualTo(3));
    Assert.That(source.Floats, Has.Count.EqualTo(2));
    Assert.That(source.Vecs, Has.Count.EqualTo(1));
  }

  [TestCase(0f, false)]
  [TestCase(0.0000001f, true)]
  [TestCase(2f, true)]
  [TestCase(null, true)]
  public void ObjectDataFilter_StripsOnlyLiteralZeroVelocity(float? speed, bool expected)
  {
    var velocity = speed.HasValue ? DataValue.Constant(new Vector3(speed.Value, 0, 0)) : new Vector3Value(["<speed>,0,0"]);
    DataEntry source = new() { Vecs = new() { [ZDOVars.s_velHash] = velocity } };

    var data = Blueprints.FilterObjectData(source, false)!;

    Assert.That(data.Vecs!.ContainsKey(ZDOVars.s_velHash), Is.EqualTo(expected));
    Assert.That(source.Vecs[ZDOVars.s_velHash], Is.SameAs(velocity));
  }

  [Test]
  public void PlanBuild_ReadsPiecesWithoutPiecesHeader_AfterMetadata()
  {
    var bp = Parse("#Name:My blueprint", "#Creator:me", "#Category:Walls", "#Center:piece_x", "wood_wall;;1;2;3;0;0;0;1;;1;1;1;;0.5");

    Assert.That(bp.Title, Is.EqualTo("My blueprint"));
    Assert.That(bp.Creator, Is.EqualTo("me"));
    Assert.That(bp.Category, Is.EqualTo("Walls"));
    Assert.That(bp.CenterPiece, Is.EqualTo("piece_x"));
    Assert.That(bp.Objects, Has.Count.EqualTo(1));
    Assert.That(bp.Objects[0].Pos, Is.EqualTo(new Vector3(1, 2, 3)));
    Assert.That(bp.Objects[0].Chance, Is.EqualTo(0.5f));
  }

  [Test]
  public void PlanBuild_ReadsGroupFromChance()
  {
    var bp = Parse("a;;0;0;0;0;0;0;1;;1;1;1;;0.25_x", "b;;0;0;0;0;0;0;1;;1;1;1;;_x", "c;;0;0;0;0;0;0;1;;1;1;1;;0.5");

    Assert.That(bp.Objects[0].Chance, Is.EqualTo(0.25f));
    Assert.That(bp.Objects[0].Group, Is.EqualTo("x"));
    Assert.That(bp.Objects[1].Chance, Is.EqualTo(1f));
    Assert.That(bp.Objects[1].Group, Is.EqualTo("x"));
    Assert.That(bp.Objects[2].Chance, Is.EqualTo(0.5f));
    Assert.That(bp.Objects[2].Group, Is.EqualTo(""));
  }

  [TestCase(0.0f, "a")]
  [TestCase(0.49f, "a")]
  [TestCase(0.51f, "b")]
  [TestCase(0.99f, "b")]
  public void Group_PicksOneWithChancesAsProbabilities(float roll, string expected)
  {
    var objects = new[] { Object("a", 0.5f, "g"), Object("b", 0.5f, "g") };

    var picked = BlueprintPlan.PickGroups(objects, () => roll);

    Assert.That(picked["g"]!.Prefab, Is.EqualTo(expected));
  }

  [Test]
  public void Group_RemainderMeansNone()
  {
    var objects = new[] { Object("a", 0.25f, "g"), Object("b", 0.25f, "g") };

    Assert.That(BlueprintPlan.PickGroups(objects, () => 0.2f)["g"]!.Prefab, Is.EqualTo("a"));
    Assert.That(BlueprintPlan.PickGroups(objects, () => 0.4f)["g"]!.Prefab, Is.EqualTo("b"));
    Assert.That(BlueprintPlan.PickGroups(objects, () => 0.6f)["g"], Is.Null);
  }

  [Test]
  public void Group_ChancesOverOneAreScaledDown()
  {
    var objects = new[] { Object("a", 1f, "g"), Object("b", 1f, "g") };

    Assert.That(BlueprintPlan.PickGroups(objects, () => 0.4f)["g"]!.Prefab, Is.EqualTo("a"));
    Assert.That(BlueprintPlan.PickGroups(objects, () => 0.6f)["g"]!.Prefab, Is.EqualTo("b"));
  }

  private static BlueprintObject Object(string name, float chance, string group) =>
    new(name, Vector3.zero, Quaternion.identity, Vector3.one, null, chance) { Group = group };

  [Test]
  public void PlanBuild_KeepsCommasInInfoColumn()
  {
    var bp = Parse("sign;;0;0;0;0;0;0;1;Hello, world;1;1;1;");

    Assert.That(bp.Objects[0].Info, Is.EqualTo("Hello, world"));
  }

  [Test]
  public void PlanBuild_AcceptsCommaAsDecimalSeparator()
  {
    var bp = Parse("wood_wall;;1,5;0;0;0;0;0;1");

    Assert.That(bp.Objects[0].Pos.x, Is.EqualTo(1.5f));
  }

  [Test]
  public void PlanBuild_CommentsAndUnknownSections()
  {
    var bp = Parse("#Pieces", "# a comment", "a;;1;0;0;0;0;0;1", "#Unknown", "b;;2;0;0;0;0;0;1");

    Assert.That(bp.Objects, Has.Count.EqualTo(1));
    Assert.That(bp.Objects[0].Prefab, Is.EqualTo("a"));
  }

  [Test]
  public void PlanBuild_InvalidTerrainRow_DropsOnlyTheTerrain()
  {
    var bp = Parse("a;;1;0;0;0;0;0;1", "#TerrainHeight:0,0,0;0;1", "1;2", "1;x", "#TerrainPaint:0,0,0;0;1", "1:0:0");

    Assert.That(bp.Objects, Has.Count.EqualTo(1));
    Assert.That(bp.TerrainHeight, Is.Null);
    Assert.That(bp.TerrainPaint, Is.Not.Null);
    Assert.That(bp.TerrainPaint!.HasValues, Is.True);
  }

  [Test]
  public void PlanBuild_InvalidPiece_FailsTheBlueprint()
  {
    Assert.That(Blueprints.GetPlanBuild(new("test"), ["a;;x;0;0;0;0;0;1"]), Is.Null);
  }

  // Quaternion.Inverse is a Unity native call, so centering on a center piece can't be tested here.
  [Test]
  public void Center_ExactMovesSnapPointsAndKeepsHeight()
  {
    var bp = Parse("a;;2;1;0;0;0;0;1", "b;;4;1;0;0;0;0;1", "#SnapPoints", "3;1;0");

    var center = bp.Center("", true);

    // Bounds start from the origin, so the horizontal center is 2 and not 3.
    Assert.That(center, Is.EqualTo(new Vector3(2, 1, 0)));
    Assert.That(bp.Objects[0].Pos, Is.EqualTo(Vector3.zero));
    Assert.That(bp.SnapPoints[0].Pos, Is.EqualTo(new Vector3(1, 0, 0)));
  }

  [Test]
  public void Center_LiftsHeightSlightlyByDefault()
  {
    var bp = Parse("a;;2;1;0;0;0;0;1", "b;;4;1;0;0;0;0;1");

    var center = bp.Center();

    Assert.That(center.y, Is.EqualTo(1.05f).Within(0.0001f));
  }

  [Test]
  public void Terrain_SetCloneAndSmooth()
  {
    BlueprintTerrain<float> terrain = new();
    terrain.Initialize(3, 3, Vector3.zero);
    terrain.Set(1, 1, 5f);
    terrain.Smooth = 0.5f;

    var clone = terrain.Clone();
    terrain.Set(1, 1, 7f);

    Assert.That(clone.Get(1, 1), Is.EqualTo(5f));
    Assert.That(clone.Smooth, Is.EqualTo(0.5f));
    Assert.That(clone.HasValues, Is.True);
    Assert.That(clone.Get(0, 0), Is.Null);
  }
}
