using System.Collections.Generic;
using HarmonyLib;

namespace ExpandWorld.Prefab;

public class PrefabConnector
{
  private static bool IsPatched = false;
  private static readonly Dictionary<ZDOID, ZDOID> SwappedZDOs = [];
  private static readonly Dictionary<ZDOID, ZDOID> ReverseConnectionTable = [];

  public static void Patch(Harmony harmony, bool shouldPatch)
  {
    if (shouldPatch && !IsPatched)
      DoPatch(harmony);
    if (!shouldPatch && IsPatched)
      DoUnpatch(harmony);
    if (!shouldPatch)
    {
      SwappedZDOs.Clear();
      ReverseConnectionTable.Clear();
    }
  }

  private static void DoPatch(Harmony harmony)
  {
    IsPatched = true;
    var method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.SetConnection));
    var patch = AccessTools.Method(typeof(PrefabConnector), nameof(Prefix));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var method = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.SetConnection));
    var patch = AccessTools.Method(typeof(PrefabConnector), nameof(Prefix));
    harmony.Unpatch(method, patch);
  }

  public static void AddSwap(ZDOID from, ZDOID to)
  {
    SwappedZDOs[from] = to;
    // If the swapped ZDO is connected, update the connection.
    if (ZDOExtraData.s_connections.TryGetValue(from, out var conn))
    {
      ZDOExtraData.s_connections.Remove(from);
      ZDOExtraData.s_connections[to] = conn;
      // No need to use RPC here because the new ZDO was just created.
    }
    // If some other ZDO is connected to the swapped ZDO, update the connection.
    if (ReverseConnectionTable.TryGetValue(from, out var target))
    {
      if (ZDOExtraData.s_connections.TryGetValue(target, out var otherConn))
      {
        ZDOExtraData.s_connections[target] = new(otherConn.m_type, to);
        var zdo = ZDOMan.instance.GetZDO(target);
        if (zdo == null) return;
        // This should guarantee that the change gets through even when not the owner.
        zdo.DataRevision += 100;
        ZDOMan.instance.ForceSendZDO(target);
      }
    }
  }
  // The idea is that if something tries to connect to a swapped ZDO, it will instead connect to the new ZDO.
  private static void Prefix(ZDOID zid, ref ZDOID target)
  {
    if (SwappedZDOs.TryGetValue(target, out var newZid))
    {
      target = newZid;
      var zdo = ZDOMan.instance.GetZDO(zid);
      // This should guarantee that the change gets through even when not the owner.
      zdo.DataRevision += 100;
      ZDOMan.instance.ForceSendZDO(zid);
    }
    ReverseConnectionTable[target] = zid;
  }
}