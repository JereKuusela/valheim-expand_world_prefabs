using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Common;
using ExpandWorld.Prefab;
using Service;
using PrefabObject = ExpandWorld.Prefab.Object;

namespace Data;

// EWP only: player information and counting of nearby objects.
public partial class ObjectFunctions
{
  private Dictionary<int, Dictionary<string, int>>? objectCounts;
  private Dictionary<string, int> objectTotals = [];
  private PrefabObject[]? objects;
  public void SetObjectCounts(PrefabObject[] value)
  {
    objectCounts = null;
    objects = value;
  }

  partial void GetHostFunction(string key, ref string? result) =>
    result = key switch
    {
      "pid" => PeerManager.GetPid(zdo),
      "cid" => PeerManager.GetCid(zdo)?.ToString() ?? "",
      "platform" => PeerManager.GetPlatform(zdo),
      "pname" => PeerManager.GetPName(zdo),
      "pchar" => PeerManager.GetPChar(zdo),
      "pvisible" => PeerManager.GetPVisible(zdo),
      "objectcount" => GetObjectTotals().Values.Sum().ToString(CultureInfo.InvariantCulture),
      _ => null,
    };

  partial void GetHostValueFunction(string key, string value, string defaultValue, ref string? result) =>
    result = key switch
    {
      "pdata" => PeerManager.GetPlayerData(zdo, value),
      "objectcount" => GetObjectCount(value, defaultValue),
      _ => null,
    };

  private Dictionary<int, Dictionary<string, int>> GetObjectCounts() => objectCounts ??= objects == null ? [] : ObjectsFiltering.GetCounts(objects, zdo, this, out objectTotals);

  private Dictionary<string, int> GetObjectTotals()
  {
    if (value == "") return defaultValue;
    if (objects == null) return defaultValue;
    var counts = GetObjectCounts();
    if (counts.TryGetValue(value, out var exact)) return exact.ToString(CultureInfo.InvariantCulture);
    if (ValueGroups.TryGet(value, out var members)) return SumCounts(counts, members);
    if (!Wildcard.IsPattern(value)) return defaultValue;
    var sum = counts.Where(kv => Wildcard.Match(kv.Key, value)).Sum(kv => kv.Value);
    return sum.ToString(CultureInfo.InvariantCulture);
  }

  private string GetObjectCount(string value, string defaultValue) =>
    objects == null ? defaultValue : GetCount(GetObjectCounts(), objectTotals, value, defaultValue);
}
