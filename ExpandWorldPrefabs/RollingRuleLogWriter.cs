using System;
using System.IO;
using System.Text;

namespace ExpandWorld.Prefab;

// All calls, including rotation and disposal, belong to the logging worker.
internal sealed class RollingRuleLogWriter : TextWriter
{
  private readonly string Path;
  private readonly long MaximumBytes;
  private readonly int Segments;
  private StreamWriter? Writer;
  private long WrittenBytes;
  public override Encoding Encoding => new UTF8Encoding(false);

  internal RollingRuleLogWriter(string path, long maximumBytes, int segments)
  {
    if (maximumBytes < 1 || segments < 1 || segments > 16) throw new ArgumentOutOfRangeException();
    Path = path;
    MaximumBytes = maximumBytes;
    Segments = segments;
    var directory = System.IO.Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    // A reduced retention setting removes only archive slots managed by EWP.
    for (var segment = Segments; segment < 16; segment++) File.Delete(Archive(segment));
    Open();
  }

  private void Open()
  {
    var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read, 65536);
    try
    {
      Writer = new StreamWriter(stream, new UTF8Encoding(false), 16384) { AutoFlush = false };
      WrittenBytes = stream.Length;
    }
    catch { stream.Dispose(); throw; }
  }

  private string Archive(int segment) => System.IO.Path.Combine(
    System.IO.Path.GetDirectoryName(Path) ?? "",
    System.IO.Path.GetFileNameWithoutExtension(Path) + "." + segment + ".txt");

  public override void WriteLine(string? value)
  {
    if (Writer == null) throw new ObjectDisposedException(nameof(RollingRuleLogWriter));
    var bytes = (long)Encoding.GetByteCount(value ?? "") + Encoding.GetByteCount(Writer.NewLine);
    if (bytes > MaximumBytes) throw new IOException("Record exceeds the segment size.");
    if (WrittenBytes > MaximumBytes - bytes)
    {
      Writer.Flush();
      Writer.Dispose();
      Writer = null;
      // Touch only this destination's exact archive names. Gaps left by an
      // interrupted rotation are valid; missing segments are simply skipped.
      if (Segments > 1) File.Delete(Archive(Segments - 1));
      for (var segment = Segments - 2; segment >= 1; segment--)
        if (File.Exists(Archive(segment))) File.Move(Archive(segment), Archive(segment + 1));
      if (Segments == 1) File.Delete(Path);
      else File.Move(Path, Archive(1));
      Open();
    }
    Writer!.WriteLine(value);
    WrittenBytes += bytes;
  }

  public override void Flush() => Writer?.Flush();
  protected override void Dispose(bool disposing)
  {
    if (disposing) { Writer?.Dispose(); Writer = null; }
    base.Dispose(disposing);
  }
}
