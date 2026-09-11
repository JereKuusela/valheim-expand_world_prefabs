using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Service;

// Normalize YAML mapping keys, not indentation or text inside scalar values.
// Existing list forms and object anchors/aliases are passed through unchanged.
internal static class FilterShorthand
{
  public static string Normalize(string yaml)
  {
    var parser = new Parser(new StringReader(yaml));
    using var writer = new StringWriter();
    var emitter = new Emitter(writer);
    parser.MoveNext();
    while (parser.Current != null) WriteNode(parser, emitter);
    return writer.ToString();
  }

  private static void WriteNode(IParser parser, IEmitter emitter)
  {
    if (parser.Current is MappingStart)
    {
      Copy(parser, emitter);
      while (parser.Current is not MappingEnd)
      {
        bool shorthand = parser.Current is Scalar key && (key.Value == "filter" || key.Value == "bannedFilter");
        if (shorthand)
        {
          var field = (Scalar)parser.Current!;
          emitter.Emit(new Scalar(field.Anchor, field.Tag, field.Value + "s", field.Style, field.IsPlainImplicit, field.IsQuotedImplicit));
          parser.MoveNext();
        }
        else WriteNode(parser, emitter);

        // A singular list remains a list. A scalar (including a scalar alias)
        // becomes one entry. Preserve a bare empty key's historical null value.
        bool scalar = parser.Current is Scalar value && !(value.Value.Length == 0 && value.Style == ScalarStyle.Plain && value.Tag.IsEmpty);
        if (shorthand && (scalar || parser.Current is AnchorAlias))
        {
          emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, true, SequenceStyle.Block));
          WriteNode(parser, emitter);
          emitter.Emit(new SequenceEnd());
        }
        else WriteNode(parser, emitter);
      }
      Copy(parser, emitter);
    }
    else if (parser.Current is SequenceStart)
    {
      Copy(parser, emitter);
      while (parser.Current is not SequenceEnd) WriteNode(parser, emitter);
      Copy(parser, emitter);
    }
    else Copy(parser, emitter);
  }

  private static void Copy(IParser parser, IEmitter emitter)
  {
    if (parser.Current == null) throw new YamlException("Unexpected end of YAML while normalizing filter shorthand.");
    // The emitter quotes an empty scalar as an empty string. Spell implicit
    // nulls explicitly so empty fields keep their original YAML meaning.
    if (parser.Current is Scalar empty && empty.Value.Length == 0 && empty.Style == ScalarStyle.Plain && empty.Tag.IsEmpty)
      emitter.Emit(new Scalar(empty.Anchor, empty.Tag, "null", ScalarStyle.Plain, true, false));
    else emitter.Emit(parser.Current);
    parser.MoveNext();
  }
}
