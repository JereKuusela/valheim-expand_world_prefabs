using System;
using System.Linq;

namespace Data;

public class BytesValue(string[] values) : AnyValue(values), IBytesValue
{
  public byte[]? Get(Parameters pars)
  {
    var value = GetValue(pars);
    if (value == null || value == "") return null;
    try
    {
      return Convert.FromBase64String(value);
    }
    catch (FormatException)
    {
      return null;
    }
  }

  public bool? Match(Parameters pars, byte[]? value)
  {
    // If all values are null, default to a match.
    var allNull = true;
    foreach (var rawValue in Values)
    {
      var v = pars.Replace(rawValue);
      if (v == null) continue;
      allNull = false;

      if (v == "" && value == null) return true;
      if (v == "" || value == null) continue;

      try
      {
        var bytes = Convert.FromBase64String(v);
        if (bytes.SequenceEqual(value))
          return true;
      }
      catch (FormatException)
      {
        continue;
      }
    }
    return allNull ? null : false;
  }
}

public class SimpleBytesValue(byte[]? value) : IBytesValue
{
  private readonly byte[]? Value = value;

  public byte[]? Get(Parameters pars) => Value;
  public bool? Match(Parameters pars, byte[]? value)
  {
    if (Value == null && value == null) return true;
    if (Value == null || value == null) return false;
    return Value.SequenceEqual(value);
  }
}

public interface IBytesValue
{
  byte[]? Get(Parameters pars);
  bool? Match(Parameters pars, byte[]? value);
}