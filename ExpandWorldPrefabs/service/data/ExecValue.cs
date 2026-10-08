using ExpandWorld.Prefab;

namespace Data;

public class ExecValue(string[] values) : DynamicValue(values)
{
  public void Run(Functions f)
  {
    var value = GetValue();
    if (value != null)
      DelayedExec.Run(f, value);
  }
}
