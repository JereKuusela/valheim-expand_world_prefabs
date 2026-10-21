using System.Collections.Generic;
using System.Linq;
using Common;

namespace ExpandWorld.Prefab;

public class LocationNames(HashSet<string> names)
{
  private readonly HashSet<string> Exact = [.. names.Where(name => !Wildcard.IsPattern(name))];
  private readonly string[] Patterns = [.. names.Where(Wildcard.IsPattern)];
  public bool Contains(string name) => Exact.Contains(name) || Patterns.Any(pattern => Wildcard.Match(name, pattern));
}
