using System.Linq;
using Common;
using Data;

namespace ExpandWorld.Prefab;

public class Filters(string[]? filters, string[]? bannedFilters, string? filterLimit)
{
  // Default limit is that all positive filters must match.
  public readonly IFloatValue? Limit = filterLimit == null ? new ConstantFloatValue(filters?.Length ?? 0f) : DataValue.Float(filterLimit);
  public readonly Filter[] Values = [.. filters?.Select(f => new Filter(f, false)) ?? [], .. bannedFilters?.Select(f => new Filter(f, true)) ?? []];

  public bool Match(Functions f, ZDO zdo)
  {
    var limit = Limit?.Get(f);
    if (limit == null) return false;
    var totalWeight = Values.Sum(v => v.Match(f, zdo));
    return limit <= totalWeight || Helper.Approx(totalWeight, limit.Value);
  }
}

public class Filter
{
  public Filter(string filter, bool banned)
  {
    var split = Parse.ToList(filter);
    // Data is either single name or type, key, value format.
    // Last part can optionally be a weight.
    if (split.Count == 2)
    {
      Weight = DataValue.Float(split[1]);
      filter = split[0];
    }
    else if (split.Count == 4)
    {
      Weight = DataValue.Float(split[3]);
      filter = string.Join(",", split.Take(3));
    }
    else
    {
      Weight = banned ? new ConstantFloatValue(10000) : new ConstantFloatValue(1);
    }
    Data = DataValue.String(filter);
    Banned = banned;
  }
  public readonly IStringValue? Data;
  public readonly bool Banned;
  // Default behavior is that none of the banned filters must match (so default limit of banned filter must be very high).
  public readonly IFloatValue? Weight;

  public float Match(Functions f, ZDO zdo)
  {
    var data = DataHelper.Get(Data, f);
    if (data == null) return 0f;
    if (!data.Match(f, zdo)) return 0f;
    var weight = Weight?.Get(f) ?? 0f;
    // Negative weight for banned means it counts towards failing the filter.
    return Banned ? -weight : weight;
  }
}
