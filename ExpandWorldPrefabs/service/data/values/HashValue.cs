using System.Linq;

namespace Data;

public class HashValue(string[] values) : AnyValue(values), IHashValue
{
  public int? Get(Parameters pars) => GetValue(pars)?.GetStableHashCode();
  public bool? Match(Parameters pars, int value)
  {
    var values = GetAllValues(pars);
    if (values.Count == 0) return null;
    return values.Any(v => v.GetStableHashCode() == value);
  }
}
public class SimpleHashValue(string value) : IHashValue
{
  private readonly int Value = value.GetStableHashCode();

  public int? Get(Parameters pars) => Value;
  public bool? Match(Parameters pars, int value) => Value == value;
}
public interface IHashValue
{
  int? Get(Parameters pars);
  bool? Match(Parameters pars, int value);
}
