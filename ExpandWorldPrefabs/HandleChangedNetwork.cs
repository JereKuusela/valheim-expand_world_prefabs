using System.Collections.Generic;
using UnityEngine;
namespace ExpandWorld.Prefab;

public partial class HandleChanged
{
  private static readonly Dictionary<int, HashSet<int>> TrackedPrefabs = [];

  // Network Deserialize reserves/replaces the typed data tables and calls Add,
  // bypassing Set. Capture existing watched values before that replacement.
  // A first snapshot is creation, not a change from an invented empty object.
  private static void BeforeDeserialize(ZDO __instance, out NetworkSnapshot? __state)
  {
    __state = null;
    if (__instance.m_prefab == 0 || IgnoreZdo == __instance.m_uid ||
        !TrackedPrefabs.TryGetValue(__instance.m_prefab, out var hashes)) return;
    __state = new(__instance, hashes);
  }
  private static void AfterDeserialize(ZDO __instance, NetworkSnapshot? __state)
  {
    if (__state == null || IgnoreZdo == __instance.m_uid || __state.Prefab != __instance.m_prefab) return;
    foreach (var previous in __state.Values) previous.QueueChanges(__instance);
  }
  private sealed class NetworkSnapshot(ZDO zdo, HashSet<int> hashes)
  {
    public readonly int Prefab = zdo.m_prefab;
    public readonly List<WatchedValues> Values = Capture(zdo, hashes);
    private static List<WatchedValues> Capture(ZDO zdo, HashSet<int> hashes)
    {
      List<WatchedValues> result = new(hashes.Count);
      foreach (var hash in hashes) result.Add(new(zdo, hash));
      return result;
    }
  }
  private sealed class WatchedValues(ZDO zdo, int hash)
  {
    private readonly int Hash = hash;
    private readonly int Int = zdo.GetInt(hash);
    private readonly float Float = zdo.GetFloat(hash);
    private readonly string String = zdo.GetString(hash);
    private readonly long Long = zdo.GetLong(hash);
    private readonly Vector3 Vec = zdo.GetVec3(hash, Vector3.zero);
    private readonly Quaternion Rotation = zdo.GetQuaternion(hash, Quaternion.identity);
    private readonly byte[] Bytes = zdo.GetByteArray(hash);
    public void QueueChanges(ZDO current)
    {
      QueueInt(current, Hash, Int, current.GetInt(Hash));
      QueueFloat(current, Hash, Float, current.GetFloat(Hash));
      QueueString(current, Hash, String, current.GetString(Hash));
      QueueLong(current, Hash, Long, current.GetLong(Hash));
      QueueVec(current, Hash, Vec, current.GetVec3(Hash, Vector3.zero));
      QueueQuaternion(current, Hash, Rotation, current.GetQuaternion(Hash, Quaternion.identity));
      QueueByteArray(current, Hash, Bytes, current.GetByteArray(Hash));
    }
  }
}
