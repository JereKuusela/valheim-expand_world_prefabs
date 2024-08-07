using System.Linq;
using Service;
using UnityEngine;

namespace Data;

public class Vector3Value(string[] values) : AnyValue(values), IVector3Value
{
  public Vector3? Get(Parameters pars) => Parse.VectorXZYNull(GetValue(pars));
  public bool? Match(Parameters pars, Vector3 value)
  {
    var values = GetAllValues(pars);
    if (values.Length == 0) return null;
    return values.Any(v => Parse.VectorXZYNull(v) == value);
  }
}

public class SimpleVector3Value(Vector3 value) : IVector3Value
{
  private readonly Vector3 Value = value;
  public Vector3? Get(Parameters pars) => Value;
  public bool? Match(Parameters pars, Vector3 value) => Value == value;
}

public interface IVector3Value
{
  Vector3? Get(Parameters pars);
  bool? Match(Parameters pars, Vector3 value);
}