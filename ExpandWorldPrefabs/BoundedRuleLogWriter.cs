using System;
using System.IO;
using System.Text;

namespace ExpandWorld.Prefab;

// The background logging worker is the sole caller. Count encoded bytes before
// writing, including buffered output; never read or truncate existing log data.
internal sealed class RuleLogFileLimitException : IOException { }

internal sealed class BoundedRuleLogWriter : TextWriter
{
  private readonly TextWriter Inner;
  private readonly long MaximumBytes;
  private long WrittenBytes;
  internal BoundedRuleLogWriter(TextWriter inner, long existingBytes, long maximumBytes)
  {
    if (existingBytes < 0 || maximumBytes < 1) throw new ArgumentOutOfRangeException();
    Inner = inner;
    WrittenBytes = existingBytes;
    MaximumBytes = maximumBytes;
  }
  public override Encoding Encoding => Inner.Encoding;
  public override void WriteLine(string? value)
  {
    var bytes = (long)Encoding.GetByteCount(value ?? "") + Encoding.GetByteCount(Inner.NewLine);
    if (WrittenBytes > MaximumBytes || bytes > MaximumBytes - WrittenBytes)
      throw new RuleLogFileLimitException();
    WrittenBytes += bytes;
    Inner.WriteLine(value);
  }
  public override void Flush() => Inner.Flush();
  protected override void Dispose(bool disposing)
  {
    if (disposing) Inner.Dispose();
    base.Dispose(disposing);
  }
}
