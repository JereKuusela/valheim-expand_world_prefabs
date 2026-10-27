using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Service;

public class Yaml
{
  public class MixedFileEntries
  {
    public List<ExpandWorld.Prefab.RuleYaml> ScriptEntries = [];
    public List<Data.DataYaml> DataEntries = [];
    public List<ExpandWorld.Prefab.ConfigYaml> ConfigEntries = [];
  }

  // Parse an already read text snapshot; never reads or modifies files.
  public static bool ParseMixed(string file, string content, out MixedFileEntries result, out string error)
  {
    result = new MixedFileEntries();
    error = "";
    var parsed = new MixedFileEntries();
    try
    {
      List<string> lines = [];
      using (var reader = new StringReader(content))
      {
        string? line;
        while ((line = reader.ReadLine()) != null) lines.Add(line);
      }
      var split = SplitMixed([.. lines]);
      if (split.ScriptLines.Count == 0 && split.DataLines.Count == 0 && split.ConfigLines.Count == 0)
        return Deserialize<ExpandWorld.Prefab.RuleYaml>(file, content, out _, out error);
      if (split.ScriptLines.Count > 0)
      {
        if (!Deserialize(file, PreParse([.. split.ScriptLines]), out List<ExpandWorld.Prefab.RuleYaml> rules, out error)) return false;
        parsed.ScriptEntries = rules;
        parsed.DataEntries.AddRange(ExpandWorld.Prefab.DataField.Hoist(rules, file));
      }
      if (split.DataLines.Count > 0)
      {
        if (!Deserialize(file, string.Join("\n", split.DataLines), out List<Data.DataYaml> data, out error)) return false;
        parsed.DataEntries.InsertRange(0, data);
      }
      if (split.ConfigLines.Count > 0)
      {
        if (!Deserialize(file, string.Join("\n", split.ConfigLines), out List<ExpandWorld.Prefab.ConfigYaml> config, out error)) return false;
        parsed.ConfigEntries = config;
      }
      result = parsed;
      return true;
    }
    catch (Exception ex)
    {
      error = ex.GetType().Name + ": " + ex.Message;
      return false;
    }
  }

  private class MixedSplit
  {
    public List<string> ScriptLines = [];
    public List<string> DataLines = [];
    public List<string> ConfigLines = [];
  }

  private static MixedSplit SplitMixed(string[] lines)
  {
    var split = new MixedSplit();
    List<string> pending = [];
    List<string> block = [];
    foreach (var line in lines)
    {
      if (IsTopLevelListItem(line))
      {
        if (block.Count > 0)
          AddMixedBlock(split, block);
        block = [];
        if (pending.Count > 0)
        {
          block.AddRange(pending);
          pending.Clear();
        }
        block.Add(line);
      }
      else
      {
        if (block.Count == 0)
          pending.Add(line);
        else
          block.Add(line);
      }
    }
    if (block.Count > 0)
      AddMixedBlock(split, block);
    return split;
  }

  private static void AddMixedBlock(MixedSplit split, List<string> block)
  {
    if (IsConfigBlock(block))
      split.ConfigLines.AddRange(block);
    else if (IsDataBlock(block))
      split.DataLines.AddRange(block);
    else
      split.ScriptLines.AddRange(block);
  }

  private static bool IsTopLevelListItem(string line)
  {
    var raw = line.Length > 0 && line[0] == '\uFEFF' ? line.Substring(1) : line;
    return raw.StartsWith("- ");
  }

  private static bool IsConfigBlock(List<string> block)
  {
    foreach (var line in block)
    {
      var key = ParseBlockKey(line);
      if (key == null) continue;
      return key.Equals("config", StringComparison.OrdinalIgnoreCase);
    }
    return false;
  }

  private static bool IsDataBlock(List<string> block)
  {
    foreach (var line in block)
    {
      var key = ParseBlockKey(line);
      if (key == null) continue;
      return key.Equals("name", StringComparison.OrdinalIgnoreCase)
        || key.Equals("valueGroup", StringComparison.OrdinalIgnoreCase)
        || key.Equals("value", StringComparison.OrdinalIgnoreCase);
    }
    return false;

  }
  private static string? ParseBlockKey(string line)
  {
    var noComment = line.Split('#')[0].Trim();
    if (noComment.Length == 0) return null;
    if (noComment.StartsWith("- "))
      noComment = noComment.Substring(2).TrimStart();
    if (noComment.Length == 0 || noComment.StartsWith("-")) return null;
    var index = noComment.IndexOf(':');
    if (index <= 0) return null;
    var key = noComment.Substring(0, index).Trim();
    if (key.Length == 0) return null;
    if (key.StartsWith("\"") && key.EndsWith("\"") && key.Length > 1)
      key = key.Substring(1, key.Length - 2);
    return key;
  }

  private static string PreParse(string[] lines)
  {
    return FilterShorthand.Normalize(string.Join("\n", lines));
  }
  private static IDeserializer Deserializer() => new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).WithTypeConverter(new ExpandWorld.Prefab.FlexibleYamlConverter()).Build();
  private static IDeserializer DeserializerUnSafe() => new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).WithTypeConverter(new ExpandWorld.Prefab.FlexibleYamlConverter()).IgnoreUnmatchedProperties().Build();

  private static bool Deserialize<T>(string file, string text, out List<T> result, out string error)
  {
    error = "";
    try
    {
      result = Deserializer().Deserialize<List<T>>(text) ?? [];
      return true;
    }
    catch (Exception strictError)
    {
      try
      {
        result = DeserializerUnSafe().Deserialize<List<T>>(text) ?? [];
      }
      catch (Exception ex)
      {
        result = [];
        error = ex.GetType().Name + ": " + ex.Message;
        return false;
      }
      // Retain the original diagnostic when unknown YAML fields are ignored.
      Log.Error($"{Path.GetFileName(file)}: {strictError.Message}");
      return true;
    }
  }

  public static Dictionary<string, string> DeserializeData(string raw)
  {
    try
    {
      return Deserializer().Deserialize<Dictionary<string, string>>(raw) ?? [];
    }
    catch
    {
      try
      {
        return DeserializerUnSafe().Deserialize<Dictionary<string, string>>(raw) ?? [];
      }
      catch (Exception)
      {
        return [];
      }
    }
  }
  public static string SerializeData(Dictionary<string, string> data)
  {
    var serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
    return serializer.Serialize(data);
  }
}
