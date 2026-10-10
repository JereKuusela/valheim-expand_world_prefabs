using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ExpandWorld.Prefab;

// Accepts either a single string or a list of strings.
public sealed class StringList(string[] items)
{
  public readonly string[] Items = items;
}

// Accepts either a legacy single-line string or a list of spawn objects.
public sealed class SpawnEntries(SpawnYaml[] data)
{
  public readonly SpawnYaml[] Data = data;
}

// Accepts a list where each item is either a legacy line or an object.
public sealed class ObjectEntries(ObjectYaml[] data)
{
  public readonly ObjectYaml[] Data = data;
}

public sealed class ExecEntries(ExecYaml[] data)
{
  public readonly ExecYaml[] Data = data;
}

// Either a data entry name or an inline data definition (moved to a named file entry on load).
public sealed class DataField(string? name, Data.DataYaml? inline = null)
{
  public string? Name = name;
  public Data.DataYaml? Inline = inline;

  internal static List<Data.DataYaml> Hoist(List<RuleYaml> rules, string file)
  {
    var result = new List<Data.DataYaml>();
    var prefix = "_inline_" + System.IO.Path.GetFullPath(file).ToLowerInvariant().GetStableHashCode() + "_";
    var counter = 0;
    void Visit(DataField? field)
    {
      if (field?.Inline == null) return;
      var entry = field.Inline;
      entry.name ??= prefix + counter++;
      field.Name = entry.name;
      field.Inline = null;
      result.Add(entry);
    }
    void VisitObjects(ObjectYaml[]? objects)
    {
      if (objects == null) return;
      foreach (var o in objects) Visit(o.data);
    }
    void VisitSpawns(SpawnYaml[]? spawns)
    {
      if (spawns == null) return;
      foreach (var s in spawns) Visit(s.data);
    }
    foreach (var rule in rules)
    {
      Visit(rule.data);
      VisitSpawns(rule.spawn?.Data);
      VisitSpawns(rule.swap?.Data);
      VisitObjects(rule.objects?.Data);
      VisitObjects(rule.bannedObjects?.Data);
      VisitObjects(rule.poke);
    }
    return result;
  }
}

internal sealed class FlexibleYamlConverter : IYamlTypeConverter
{
  public bool Accepts(Type type) =>
    type == typeof(StringList) || type == typeof(SpawnEntries) || type == typeof(ObjectEntries) || type == typeof(ExecEntries) || type == typeof(DataField) || type == typeof(RuleLogData) || type == typeof(RuleLogFiles);

  public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
  {
    if (type == typeof(RuleLogData)) return new RuleLogData(rootDeserializer(typeof(object)));
    if (type == typeof(RuleLogFiles)) return new RuleLogFiles(rootDeserializer(typeof(object)));
    if (type == typeof(DataField))
    {
      if (parser.Current is MappingStart)
        return new DataField(null, (Data.DataYaml?)rootDeserializer(typeof(Data.DataYaml)));
      if (parser.Current is Scalar name)
      {
        parser.MoveNext();
        return string.IsNullOrWhiteSpace(name.Value) ? null : new DataField(name.Value);
      }
      rootDeserializer(typeof(object));
      return null;
    }
    if (type == typeof(ObjectEntries))
    {
      if (parser.Current is not SequenceStart)
      {
        rootDeserializer(typeof(object));
        return null;
      }
      parser.Consume<SequenceStart>();
      var objects = new List<ObjectYaml>();
      while (!parser.TryConsume<SequenceEnd>(out _))
      {
        if (parser.Current is Scalar item)
        {
          parser.MoveNext();
          objects.Add(ObjectYaml.FromLine(item.Value));
        }
        else objects.Add((ObjectYaml)rootDeserializer(typeof(ObjectYaml))!);
      }
      return new ObjectEntries([.. objects]);
    }
    if (type == typeof(ExecEntries))
    {
      if (parser.Current is Scalar single)
      {
        parser.MoveNext();
        return string.IsNullOrWhiteSpace(single.Value) ? null : new ExecEntries([ExecYaml.FromLine(single.Value)]);
      }
      if (parser.Current is not SequenceStart)
      {
        rootDeserializer(typeof(object));
        return null;
      }
      parser.Consume<SequenceStart>();
      var execs = new List<ExecYaml>();
      while (!parser.TryConsume<SequenceEnd>(out _))
      {
        if (parser.Current is Scalar item)
        {
          parser.MoveNext();
          execs.Add(ExecYaml.FromLine(item.Value));
        }
        else execs.Add((ExecYaml)rootDeserializer(typeof(ExecYaml))!);
      }
      return new ExecEntries([.. execs]);
    }
    var scalar = parser.Current is Scalar;
    if (type == typeof(StringList))
    {
      if (!scalar) return new StringList((string[]?)rootDeserializer(typeof(string[])) ?? []);
      var text = (string?)rootDeserializer(typeof(string));
      return text == null ? null : new StringList([text]);
    }
    if (!scalar) return new SpawnEntries((SpawnYaml[]?)rootDeserializer(typeof(SpawnYaml[])) ?? []);
    var line = (string?)rootDeserializer(typeof(string));
    return string.IsNullOrWhiteSpace(line) ? null : new SpawnEntries([SpawnYaml.FromLine(line!)]);
  }

  public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
    serializer(value switch
    {
      StringList list => list.Items,
      SpawnEntries spawn => spawn.Data,
      ObjectEntries objects => objects.Data,
      ExecEntries execs => execs.Data,
      DataField field => (object?)field.Inline ?? field.Name,
      RuleLogData log => log.Value,
      RuleLogFiles files => files.Value,
      _ => null
    }, typeof(object));
}
