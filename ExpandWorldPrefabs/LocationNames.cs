using System.Collections.Generic;
using System.Linq;
using Common;

namespace ExpandWorld.Prefab;

public class LocationNames(HashSet<string> names)
{
  private readonly string[] Patterns = [.. names.Where(Wildcard.IsPattern)];
  public bool Contains(string name) => names.Contains(name) || Patterns.Any(pattern => Wildcard.Match(name, pattern));
}
