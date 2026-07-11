
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Data;
using System.Linq;

namespace ExpandWorld.Prefab;


public class SupportAttach
{
  public static void Patch(Harmony harmony)
  {
    var original = AccessTools.Method(typeof(ZDO), nameof(ZDO.SetOwner));
    var prefix = AccessTools.Method(typeof(SupportAttach), nameof(SetOwner));
    harmony.Patch(original, prefix: new HarmonyMethod(prefix));
    original = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO), [typeof(ZDOID)]);
    var postfix = AccessTools.Method(typeof(SupportAttach), nameof(HandleDestroyed));
    harmony.Patch(original, postfix: new HarmonyMethod(postfix));
  }

  public static bool IsSynced(ZDO zdo) => zdo.GetConnectionType() == ZDOExtraData.ConnectionType.SyncTransform;
  // Clients can only have one player, so NPCs should stay unowned.
  public static bool IsPlayer(ZDO zdo) => zdo.GetPrefab() == PlayerHash;
  public static bool IsNpc(ZDO zdo) => zdo.GetPrefab() == PlayerHash && zdo.Persistent;
  public static bool IsRealPlayer(ZDO zdo) => zdo.GetPrefab() == PlayerHash && !zdo.Persistent;

  public static readonly long HackOwner = 1;

  // Cache to more quickly release attached objects if parent is destroyed.
  // This also happens over time thtough SetOwner.
  private static readonly HashSet<ZDOID> Parents = [];
  private static readonly long PlayerHash = "Player".GetStableHashCode();
  static void SetOwner(ZDO __instance, ref long uid)
  {
    if (!IsSynced(__instance))
    {
      if (IsNpc(__instance))
        uid = HackOwner;
      return;
    }
    var parent = __instance.GetConnectionZDOID(ZDOExtraData.ConnectionType.SyncTransform);
    var exists = ZDOMan.instance.GetZDO(parent) != null;
    if (exists)
    {
      uid = HackOwner;
      Parents.Add(parent);
    }
    else
    {
      Unattach(__instance);
      Parents.Remove(parent);
      // Clients can only have one player, so NPCs should stay unowned.
      if (IsNpc(__instance))
        uid = HackOwner;
    }
  }


  static void HandleDestroyed(ZDOID uid)
  {
    if (!Parents.Contains(uid)) return;
    var connected = GetConnnected(uid);
    foreach (var id in connected)
    {
      var zdo = ZDOMan.instance.GetZDO(id);
      if (zdo == null) continue;
      Unattach(zdo);
    }
  }

  public static List<ZDOID> GetConnnected(ZDOID uid) =>
    [.. ZDOExtraData.s_connections.Where(pair => pair.Value != null && pair.Value.m_target == uid).Select(pair => pair.Key)];



  private static readonly int HasFields = "HasFields".GetStableHashCode();
  private static readonly int HasFieldsZSyncTransform = "HasFieldsZSyncTransform".GetStableHashCode();
  private static readonly int ZSyncTransformCharacterParentSync = "ZSyncTransform.m_characterParentSync".GetStableHashCode();
  public static void Attach(ZdoEntry zdoEntry, ZDOID target)
  {
    zdoEntry.ConnectionType = ZDOExtraData.ConnectionType.SyncTransform;
    zdoEntry.TargetConnectionId = target;
    zdoEntry.Ints ??= [];
    zdoEntry.Ints[HasFields] = 1;
    zdoEntry.Ints[HasFieldsZSyncTransform] = 1;
    zdoEntry.Ints[ZSyncTransformCharacterParentSync] = 1;
  }
  public static void Connect(ZdoEntry zdoEntry, ZDOID target)
  {
    zdoEntry.ConnectionType = InvalidType;
    zdoEntry.TargetConnectionId = target;
  }
  public static void Connect(ZDO zdo, ZDOID target)
  {
    zdo.SetConnection(InvalidType, target);
  }
  public static void Attach(ZDO zdo, ZDOID target)
  {
    // Actual players can't be attached or they lose control.
    if (IsRealPlayer(zdo))
      return;
    if (target == ZDOID.None)
    {
      Unattach(zdo);
      return;
    }
    zdo.SetConnection(ZDOExtraData.ConnectionType.SyncTransform, target);
    zdo.Set(HasFields, 1);
    zdo.Set(HasFieldsZSyncTransform, 1);
    zdo.Set(ZSyncTransformCharacterParentSync, 1);
    zdo.SetOwnerInternal(HackOwner);
    zdo.OwnerRevision += 1;
    Parents.Add(target);
  }

  // None type is not serialized, so have to use something else for clients to receive it.
  private static readonly ZDOExtraData.ConnectionType InvalidType = unchecked((ZDOExtraData.ConnectionType)0x20);
  private static void Unattach(ZDO zdo)
  {
    var connectionZdoId = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.SyncTransform);
    if (connectionZdoId.IsNone())
      return;
    SyncAttachedWorldTransform(zdo, connectionZdoId);
    zdo.SetConnection(InvalidType, ZDOID.None);
    zdo.DataRevision += 100;
    if (!IsNpc(zdo) && zdo.GetOwner() == HackOwner)
    {
      zdo.SetOwnerInternal(DelayedOwner.FindNearestOwner(zdo));
      zdo.OwnerRevision += 1;
    }
  }

  private static void SyncAttachedWorldTransform(ZDO zdo, ZDOID connectionZdoId)
  {
    var parentZdo = ZDOMan.instance.GetZDO(connectionZdoId);
    if (parentZdo == null)
      return;

    var parentPos = parentZdo.GetPosition();
    var parentRot = parentZdo.GetRotation();

    var attachJoint = zdo.GetString(ZDOVars.s_attachJointHash, "");
    var relPos = zdo.GetVec3(ZDOVars.s_relPosHash, Vector3.zero);
    var relRot = zdo.GetQuaternion(ZDOVars.s_relRotHash, Quaternion.identity);

    if (attachJoint.Length > 0 && TryGetJointWorldPosition(parentZdo, parentPos, parentRot, attachJoint, out var worldPos))
    {
      zdo.m_position = worldPos;
    }
    else
    {
      // One-shot world position restore before detaching.
      var relVel = zdo.GetVec3(ZDOVars.s_velHash, Vector3.zero);
      relPos += relVel * Time.deltaTime;
      worldPos = parentPos + parentRot * relPos;
    }

    var worldRot = parentRot * relRot;
    zdo.m_position = worldPos;
    zdo.SetSector(ZoneSystem.GetZone(worldPos));
    zdo.m_rotation = worldRot.eulerAngles;
  }

  private static bool TryGetJointWorldPosition(ZDO parentZdo, Vector3 parentPos, Quaternion parentRot, string attachJoint, out Vector3 worldPos)
  {
    var parentPrefab = ZNetScene.instance.GetPrefab(parentZdo.GetPrefab());
    if (!parentPrefab)
    {
      worldPos = Vector3.zero;
      return false;
    }

    var joint = Utils.FindChild(parentPrefab.transform, attachJoint, Utils.IterativeSearchType.DepthFirst);
    if (!joint)
    {
      worldPos = Vector3.zero;
      return false;
    }

    var jointLocalPos = parentPrefab.transform.InverseTransformPoint(joint.position);
    worldPos = parentPos + parentRot * jointLocalPos;
    return true;
  }

  public static bool CanSync(ZDO zdo) => zdo.GetInt(ZSyncTransformCharacterParentSync) == 1;
}
