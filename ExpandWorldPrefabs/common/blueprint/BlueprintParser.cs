// Shared code: keep identical in EWD, EWP and WEC (common/). Sync changes to all.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Service;
using UnityEngine;
using Common;

namespace Data;

public partial class Blueprints
{
  // Invalid pieces fail the whole blueprint. Invalid terrain only drops that terrain.
  public static Blueprint? GetPlanBuild(Blueprint bp, string[] rows, bool loadData = true)
  {
    var section = PlanBuildSection.Pieces;
    var currentRow = -1;
    try
    {
      foreach (var row in rows)
      {
        currentRow += 1;
        if (row.StartsWith("#", StringComparison.Ordinal))
        {
          section = ReadPlanBuildHeader(bp, row, section);
          continue;
        }
        if (section == PlanBuildSection.Pieces)
        {
          if (!string.IsNullOrWhiteSpace(row))
            bp.Objects.Add(GetPlanBuildObject(row, bp.Name, loadData));
        }
        else if (section == PlanBuildSection.SnapPoints)
        {
          if (!string.IsNullOrWhiteSpace(row))
            bp.SnapPoints.Add(new(GetPlanBuildSnapPoint(row), Quaternion.identity));
        }
        else if (section == PlanBuildSection.TerrainHeight && bp.TerrainHeight != null)
        {
          if (!TryAddTerrainRow(bp, bp.TerrainHeight, GetPlanBuildHeightRow, row, "height"))
          {
            bp.TerrainHeight = null;
            section = PlanBuildSection.Ignore;
          }
        }
        else if (section == PlanBuildSection.TerrainPaint && bp.TerrainPaint != null)
        {
          if (!TryAddTerrainRow(bp, bp.TerrainPaint, GetPlanBuildPaintRow, row, "paint"))
          {
            bp.TerrainPaint = null;
            section = PlanBuildSection.Ignore;
          }
        }
      }
    }
    catch (Exception e)
    {
      Log.Error($"Failed to load blueprint {bp.Name} at row {currentRow}: {rows[currentRow]}. {e.Message}");
      return null;
    }
    return bp;
  }

  private static bool TryAddTerrainRow<T>(Blueprint bp, BlueprintTerrain<T> terrain, Func<string, T?[]> parse, string row, string type) where T : struct
  {
    try
    {
      terrain.AddRow(parse(row));
      return true;
    }
    catch (Exception e) when (e is FormatException or OverflowException)
    {
      Log.Warning($"Blueprint {bp.Name}: Ignoring terrain {type}: {e.Message}");
      return false;
    }
  }

  private enum PlanBuildSection
  {
    Ignore,
    SnapPoints,
    Pieces,
    TerrainHeight,
    TerrainPaint
  }

  // Metadata headers keep the current section so that files without a #Pieces header still work.
  private static PlanBuildSection ReadPlanBuildHeader(Blueprint bp, string row, PlanBuildSection current)
  {
    // Terrain rows always end at the next header.
    if (current is PlanBuildSection.TerrainHeight or PlanBuildSection.TerrainPaint)
      current = PlanBuildSection.Ignore;
    var separator = row.IndexOf(':');
    var name = (separator < 0 ? row : row.Substring(0, separator)).Trim();
    var value = separator < 0 ? "" : row.Substring(separator + 1);
    if (name.Equals("#snappoints", StringComparison.OrdinalIgnoreCase))
      return PlanBuildSection.SnapPoints;
    if (name.Equals("#pieces", StringComparison.OrdinalIgnoreCase))
      return PlanBuildSection.Pieces;
    if (name.Equals("#center", StringComparison.OrdinalIgnoreCase))
    {
      if (separator < 0) throw new FormatException("#Center requires a prefab name.");
      bp.CenterPiece = value;
      return current;
    }
    if (name.Equals("#name", StringComparison.OrdinalIgnoreCase))
    {
      bp.Title = value;
      return current;
    }
    if (name.Equals("#creator", StringComparison.OrdinalIgnoreCase))
    {
      bp.Creator = value;
      return current;
    }
    if (name.Equals("#description", StringComparison.OrdinalIgnoreCase))
    {
      bp.Description = value;
      return current;
    }
    if (name.Equals("#category", StringComparison.OrdinalIgnoreCase))
    {
      bp.Category = value;
      return current;
    }
    if (name.Equals("#coordinates", StringComparison.OrdinalIgnoreCase))
    {
      bp.Coordinates = Parse.VectorXZY(value);
      return current;
    }
    if (name.Equals("#rotation", StringComparison.OrdinalIgnoreCase))
    {
      bp.Rotation = Parse.VectorXZY(value);
      return current;
    }
    if (name.Equals("#height", StringComparison.OrdinalIgnoreCase) || name.Equals("#paint", StringComparison.OrdinalIgnoreCase))
    {
      Log.Warning($"Blueprint {bp.Name}: Legacy {name} terrain format is not supported.");
      return PlanBuildSection.Ignore;
    }
    if (name.Equals("#terrainheight", StringComparison.OrdinalIgnoreCase))
    {
      try
      {
        bp.TerrainHeight = GetPlanBuildTerrain<float>(row, separator);
        return PlanBuildSection.TerrainHeight;
      }
      catch (FormatException)
      {
        return PlanBuildSection.Ignore;
      }
      catch (OverflowException)
      {
        return PlanBuildSection.Ignore;
      }
    }
    if (name.Equals("#terrainpaint", StringComparison.OrdinalIgnoreCase))
    {
      try
      {
        bp.TerrainPaint = GetPlanBuildTerrain<Color>(row, separator);
        return PlanBuildSection.TerrainPaint;
      }
      catch (FormatException)
      {
        return PlanBuildSection.Ignore;
      }
      catch (OverflowException)
      {
        return PlanBuildSection.Ignore;
      }
    }
    // Plain comments don't affect the section. Unknown sections must not inherit the previous
    // parser state, so future PlanBuild extensions are not interpreted as pieces.
    if (row.Length == 1 || char.IsWhiteSpace(row[1]))
      return current;
    return PlanBuildSection.Ignore;
  }

  private static BlueprintObject GetPlanBuildObject(string row, string fileName, bool loadData)
  {
    // Commas are not replaced here because the info column is free text.
    var split = row.Split(';');
    var name = split[0];
    var posX = InvariantFloat(split, 2);
    var posY = InvariantFloat(split, 3);
    var posZ = InvariantFloat(split, 4);
    var rotX = InvariantFloat(split, 5);
    var rotY = InvariantFloat(split, 6);
    var rotZ = InvariantFloat(split, 7);
    var rotW = InvariantFloat(split, 8);
    var info = split.Length > 9 ? split[9] : "";
    var scaleX = InvariantFloat(split, 10, 1f);
    var scaleY = InvariantFloat(split, 11, 1f);
    var scaleZ = InvariantFloat(split, 12, 1f);
    var data = split.Length > 13 ? split[13] : "";
    var chance = InvariantFloat(split, 14, 1f);
    return CreateObject(name, new(posX, posY, posZ), new(rotX, rotY, rotZ, rotW), new(scaleX, scaleY, scaleZ), info, loadData ? data : "", chance, fileName);
  }
  // Old blueprints store simple data (sign text, item stand items...) in the info column.
  private static BlueprintObject CreateObject(string name, Vector3 pos, Quaternion rot, Vector3 scale, string info, string data, float chance, string fileName)
  {
    BlueprintObject obj = new(name, pos, rot, scale, ResolveData(data, fileName), chance) { Info = info, RawData = data };
    if (obj.Data == null && info != "")
      obj.Data = BlueprintInfo.ToData(name, info);
    return obj;
  }
  private static Vector3 GetPlanBuildSnapPoint(string row)
  {
    if (row.IndexOf(',') > -1) row = row.Replace(',', '.');
    var split = row.Split(';');
    var x = InvariantFloat(split, 0);
    var y = InvariantFloat(split, 1);
    var z = InvariantFloat(split, 2);
    return new(x, y, z);
  }
  private static Blueprint GetBuildShare(Blueprint bp, string[] rows, bool loadData)
  {
    bp.Objects = rows.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => GetBuildShareObject(r, bp.Name, loadData)).ToList();
    return bp;
  }
  private static BlueprintObject GetBuildShareObject(string row, string fileName, bool loadData)
  {
    var split = row.Split(' ');
    var name = split[0];
    var rotX = InvariantFloat(split, 1);
    var rotY = InvariantFloat(split, 2);
    var rotZ = InvariantFloat(split, 3);
    var rotW = InvariantFloat(split, 4);
    var posX = InvariantFloat(split, 5);
    var posY = InvariantFloat(split, 6);
    var posZ = InvariantFloat(split, 7);
    var data = loadData && split.Length > 8 ? split[8] : "";
    var chance = split.Length > 9 ? InvariantFloat(split, 9, 1f) : 1f;
    return CreateObject(name, new(posX, posY, posZ), new(rotX, rotY, rotZ, rotW), Vector3.one, "", data, chance, fileName);
  }
  private static float InvariantFloat(string[] row, int index, float defaultValue = 0f)
  {
    if (index >= row.Length) return defaultValue;
    var s = row[index];
    if (string.IsNullOrEmpty(s)) return defaultValue;
    return float.Parse(s.Replace(',', '.'), NumberStyles.Any, NumberFormatInfo.InvariantInfo);
  }
  private static float RequiredInvariantFloat(string[] row, int index)
  {
    if (index >= row.Length || string.IsNullOrWhiteSpace(row[index]))
      throw new FormatException("A required terrain number is missing.");
    return float.Parse(row[index].Replace(',', '.'), NumberStyles.Any, NumberFormatInfo.InvariantInfo);
  }
  private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
