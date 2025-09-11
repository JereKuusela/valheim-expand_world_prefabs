using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class HandleRPC
{
  private delegate bool RPCHandler(ZDO zdo, ZRoutedRpc.RoutedRPCData data);
  private static readonly Dictionary<int, RPCHandler> Handlers = [];

  public static void Patch(Harmony harmony)
  {
    var method = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.HandleRoutedRPC));
    var patch = AccessTools.Method(typeof(HandleRPC), nameof(Handle));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.RouteRPC));
    patch = AccessTools.Method(typeof(HandleRPC), nameof(RouteRPC));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }

  public static void SetRequiredStates(HashSet<string> requiredStates)
  {
    Handlers.Clear();

    foreach (var (hash, handler, states) in AllAvailableHandlers)
    {
      foreach (var state in states)
      {
        if (requiredStates.Contains(state))
          Handlers[hash] = handler;
      }
    }
  }


  static bool RouteRPC(ZRoutedRpc.RoutedRPCData rpcData)
  {
    var cancel = false;
    if (rpcData.m_methodHash == SayHash)
    {
      var zdo = ZDOMan.instance.GetZDO(rpcData.m_targetZDO);
      if (zdo == null) return true;
      cancel = CancelSay(zdo, rpcData);
    }
    return !cancel;
  }

  static bool Handle(ZRoutedRpc.RoutedRPCData data)
  {
    var zdo = ZDOMan.instance.GetZDO(data.m_targetZDO);
    if (zdo == null) return true;

    var cancel = false;

    if (Handlers.TryGetValue(data.m_methodHash, out var handler))
      cancel = handler(zdo, data);

    return !cancel;
  }


  static readonly int RepairHash = "RPC_HealthChanged".GetStableHashCode();
  static readonly ParameterInfo[] RepairPars = AccessTools.Method(typeof(WearNTear), nameof(WearNTear.RPC_HealthChanged)).GetParameters();
  private static bool WNTHealthChanged(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    if (!prefab) return false;
    if (!prefab.TryGetComponent(out WearNTear wearNTear)) return false;
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RepairPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var health = (float)pars[1];
    if (health > 1E20) return false;
    if (health == wearNTear.m_health)
    {
      return Manager.Handle(ActionType.State, ["repair"], zdo);
    }
    else
    {
      return Manager.Handle(ActionType.State, ["damage"], zdo);
    }
  }

  static readonly int SetTriggerHash = "SetTrigger".GetStableHashCode();
  static readonly ParameterInfo[] SetTriggerPars = AccessTools.Method(typeof(ZSyncAnimation), nameof(ZSyncAnimation.RPC_SetTrigger)).GetParameters();
  private static bool SetTrigger(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetTriggerPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var trigger = (string)pars[1];

    return Manager.Handle(ActionType.State, ["action", trigger], zdo);
  }
  static readonly int SetTargetHash = "RPC_SetTarget".GetStableHashCode();
  static readonly ParameterInfo[] SetTargetPars = AccessTools.Method(typeof(Turret), nameof(Turret.RPC_SetTarget)).GetParameters();
  private static bool SetTarget(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetTargetPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var target = (ZDOID)pars[1];
    if (target == ZDOID.None) return false;
    var targetZDO = ZDOMan.instance.GetZDO(target);
    if (targetZDO == null) return false;
    var targetPrefab = ZNetScene.instance.GetPrefab(targetZDO.GetPrefab());
    if (!targetPrefab) return false;
    var cancel1 = Manager.Handle(ActionType.State, ["targeting", targetPrefab.name], zdo);
    var cancel2 = Manager.Handle(ActionType.State, ["target"], targetZDO);
    return cancel1 || cancel2;
  }
  static readonly int ShakeHash = "RPC_Shake".GetStableHashCode();
  static readonly ParameterInfo[] ShakePars = AccessTools.Method(typeof(TreeBase), nameof(TreeBase.RPC_Shake)).GetParameters();
  private static bool Shake(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["damage"], zdo);
  }
  static readonly int OnStateChangedHash = "RPC_OnStateChanged".GetStableHashCode();
  static readonly ParameterInfo[] OnStateChangedPars = AccessTools.Method(typeof(Trap), nameof(Trap.RPC_OnStateChanged)).GetParameters();
  private static bool OnStateChanged(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnStateChangedPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var state = (int)pars[1];
    if (state == 0) return false;
    return Manager.Handle(ActionType.State, ["trap", state.ToString()], zdo);
  }
  static readonly int SetSaddleHash = "SetSaddle".GetStableHashCode();
  static readonly ParameterInfo[] SetSaddlePars = AccessTools.Method(typeof(Tameable), nameof(Tameable.RPC_SetSaddle)).GetParameters();
  private static bool SetSaddle(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetSaddlePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var saddle = (bool)pars[1];
    return Manager.Handle(ActionType.State, [saddle ? "saddle" : "unsaddle"], zdo);
  }
  static readonly int SayHash = "Say".GetStableHashCode();
  static readonly ParameterInfo[] SayPars = AccessTools.Method(typeof(Talker), nameof(Talker.RPC_Say)).GetParameters();
  private static bool Say(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SayPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 4) return false;
    var text = (string)pars[3];
    return Manager.Handle(ActionType.Say, text.Split(' '), zdo);
  }
  private static bool CancelSay(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SayPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 4) return false;
    var text = (string)pars[3];
    return Manager.CheckCancel(ActionType.Say, text.Split(' '), zdo);
  }
  static readonly int ChatMessageHash = "ChatMessage".GetStableHashCode();
  static readonly ParameterInfo[] ChatMessgePars = AccessTools.Method(typeof(Chat), nameof(Chat.RPC_ChatMessage)).GetParameters();
  private static bool HandleChat(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, ChatMessgePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 5) return false;
    var text = (string)pars[4];
    return Manager.Handle(ActionType.Say, text.Split(' '), zdo);
  }
  private static bool CancelChat(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, ChatMessgePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 5) return false;
    var text = (string)pars[4];
    return Manager.CheckCancel(ActionType.Say, text.Split(' '), zdo);
  }
  static readonly int FlashShieldHash = "FlashShield".GetStableHashCode();
  static readonly ParameterInfo[] FlashShieldPars = AccessTools.Method(typeof(PrivateArea), nameof(PrivateArea.RPC_FlashShield)).GetParameters();
  private static bool FlashShield(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["flash"], zdo);
  }
  static readonly int SetPickedHash = "RPC_SetPicked".GetStableHashCode();
  static readonly ParameterInfo[] SetPickedPars = AccessTools.Method(typeof(Pickable), nameof(Pickable.RPC_SetPicked)).GetParameters();
  private static bool SetPicked(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetPickedPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var picked = (bool)pars[1];
    return Manager.Handle(ActionType.State, [picked ? "picked" : "unpicked"], zdo);
  }
  static readonly int PlayMusicHash = "RPC_PlayMusic".GetStableHashCode();
  static readonly ParameterInfo[] PlayMusicPars = AccessTools.Method(typeof(MusicVolume), nameof(MusicVolume.RPC_PlayMusic)).GetParameters();
  private static bool PlayMusic(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["music"], zdo);
  }
  static readonly int WakeupHash = "RPC_Wakeup".GetStableHashCode();
  static readonly ParameterInfo[] WakeupPars = AccessTools.Method(typeof(MonsterAI), nameof(MonsterAI.RPC_Wakeup)).GetParameters();
  private static bool Wakeup(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["wakeup"], zdo);
  }
  static readonly int SetAreaHealthHash = "RPC_SetAreaHealth".GetStableHashCode();
  static readonly ParameterInfo[] SetAreaHealthPars = AccessTools.Method(typeof(MineRock5), nameof(MineRock5.RPC_SetAreaHealth)).GetParameters();
  private static bool SetAreaHealth(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetAreaHealthPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 3) return false;
    var index = (int)pars[1];
    var health = (float)pars[2];
    return Manager.Handle(ActionType.State, ["damage", index.ToString(), Helper.Format2(health)], zdo);
  }
  static readonly int HideHash = "Hide".GetStableHashCode();
  static readonly ParameterInfo[] HidePars = AccessTools.Method(typeof(MineRock), nameof(MineRock.RPC_Hide)).GetParameters();
  private static bool Hide(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, HidePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var index = (int)pars[1];
    return Manager.Handle(ActionType.State, ["damage", index.ToString()], zdo);
  }
  static readonly int SetVisualItemHash = "SetVisualItem".GetStableHashCode();
  static readonly ParameterInfo[] ItemStandPars = AccessTools.Method(typeof(ItemStand), nameof(ItemStand.RPC_SetVisualItem)).GetParameters();

  private static bool SetVisualItem(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    if (!prefab) return false;
    var pars = ZNetView.Deserialize(data.m_senderPeerID, ItemStandPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 4) return false;
    var item = (string)pars[1];
    var variant = (int)pars[2];
    var quality = (int)pars[3];
    var state = item == "" ? "<none>" : item;

    return Manager.Handle(ActionType.State, ["item", state, variant.ToString(), quality.ToString()], zdo);
  }
  static readonly int SetArmorVisualItemHash = "RPC_SetVisualItem".GetStableHashCode();
  static readonly ParameterInfo[] ArmorStandPars = AccessTools.Method(typeof(ArmorStand), nameof(ArmorStand.RPC_SetVisualItem)).GetParameters();
  private static bool SetArmorVisualItem(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    if (!prefab) return false;
    var pars = ZNetView.Deserialize(data.m_senderPeerID, ArmorStandPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 4) return false;
    var slot = (int)pars[1];
    var item = (string)pars[2];
    var variant = (int)pars[3];
    var state = item == "" ? "<none>" : item;

    return Manager.Handle(ActionType.State, ["item", state, variant.ToString(), slot.ToString()], zdo);
  }
  static readonly int AnimateLeverHash = "RPC_AnimateLever".GetStableHashCode();
  static readonly ParameterInfo[] AnimateLeverPars = AccessTools.Method(typeof(Incinerator), nameof(Incinerator.RPC_AnimateLever)).GetParameters();
  private static bool AnimateLever(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["start"], zdo);
  }
  static readonly int AnimateLeverReturnHash = "RPC_AnimateLeverReturn".GetStableHashCode();
  static readonly ParameterInfo[] AnimateLeverReturnPars = AccessTools.Method(typeof(Incinerator), nameof(Incinerator.RPC_AnimateLeverReturn)).GetParameters();
  private static bool AnimateLeverReturn(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["end"], zdo);
  }
  static readonly int SetSlotVisualHash = "RPC_SetSlotVisual".GetStableHashCode();
  static readonly ParameterInfo[] SetSlotVisualPars = AccessTools.Method(typeof(CookingStation), nameof(CookingStation.RPC_SetSlotVisual)).GetParameters();
  private static bool SetSlotVisual(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetSlotVisualPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 3) return false;
    var slot = (int)pars[1];
    var item = (string)pars[2];
    var state = item == "" ? "<none>" : item;

    return Manager.Handle(ActionType.State, ["item", slot.ToString(), state], zdo);
  }


  static readonly int MakePieceHash = "RPC_MakePiece".GetStableHashCode();
  static readonly ParameterInfo[] MakePieceHashPars = AccessTools.Method(typeof(ItemDrop), nameof(ItemDrop.RPC_MakePiece)).GetParameters();
  private static bool MakePiece(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["piece"], zdo);
  }

  static readonly int OnEatHash = "RPC_OnEat".GetStableHashCode();
  static readonly ParameterInfo[] OnEatPars = AccessTools.Method(typeof(Feast), nameof(Feast.RPC_OnEat)).GetParameters();
  private static bool OnEat(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["eat"], zdo);
  }

  static readonly int OnDeathHash = "OnDeath".GetStableHashCode();
  static readonly ParameterInfo[] OnDeathPars = AccessTools.Method(typeof(Player), nameof(Player.RPC_OnDeath)).GetParameters();
  private static bool OnDeath(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["death"], zdo);
  }

  static readonly int OnSetPoseHash = "RPC_SetPose".GetStableHashCode();
  static readonly ParameterInfo[] OnSetPosePars = AccessTools.Method(typeof(ArmorStand), nameof(ArmorStand.RPC_SetPose)).GetParameters();
  private static bool OnSetPose(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnSetPosePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var index = (int)pars[1];
    return Manager.Handle(ActionType.State, ["pose", index.ToString()], zdo);
  }

  static readonly int OnLegUseHash = "RPC_OnLegUse".GetStableHashCode();
  static readonly ParameterInfo[] OnLegUsePars = AccessTools.Method(typeof(Catapult), nameof(Catapult.RPC_OnLegUse)).GetParameters();
  private static bool OnLegUse(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnLegUsePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var legsLocked = (bool)pars[1];
    return Manager.Handle(ActionType.State, [legsLocked ? "lock" : "release"], zdo);
  }

  static readonly int OnSetLoadedHash = "RPC_SetLoadedVisual".GetStableHashCode();
  static readonly ParameterInfo[] OnSetLoadedPars = AccessTools.Method(typeof(Catapult), nameof(Catapult.RPC_SetLoadedVisual)).GetParameters();
  private static bool OnSetLoaded(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnSetLoadedPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var name = (string)pars[1];
    return Manager.Handle(ActionType.State, ["loaded", name], zdo);
  }

  static readonly int OnShootHash = "RPC_Shoot".GetStableHashCode();
  static readonly ParameterInfo[] OnShootPars = AccessTools.Method(typeof(Catapult), nameof(Catapult.RPC_Shoot)).GetParameters();
  private static bool OnShoot(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["shoot"], zdo);
  }

  static readonly int OnFreezeFrameHash = "RPC_FreezeFrame".GetStableHashCode();
  static readonly ParameterInfo[] OnFreezeFramePars = AccessTools.Method(typeof(Character), nameof(Character.RPC_FreezeFrame)).GetParameters();
  private static bool OnFreezeFrame(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnFreezeFramePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var duratiom = (float)pars[1];
    return Manager.Handle(ActionType.State, ["freezeframe", Helper.Format2(duratiom)], zdo);
  }

  static readonly int OnResetClothHash = "RPC_ResetCloth".GetStableHashCode();
  static readonly ParameterInfo[] OnResetClothPars = AccessTools.Method(typeof(Character), nameof(Character.RPC_ResetCloth)).GetParameters();
  private static bool OnResetCloth(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["resetcloth"], zdo);
  }

  static readonly int OnFragmentsHash = "RPC_CreateFragments".GetStableHashCode();
  static readonly ParameterInfo[] OnFragmentsPars = AccessTools.Method(typeof(Destructible), nameof(Destructible.RPC_CreateFragments)).GetParameters();
  private static bool OnFragments(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["fragments"], zdo);
  }

  static readonly int OnStepHash = "Step".GetStableHashCode();
  static readonly ParameterInfo[] OnStepPars = AccessTools.Method(typeof(FootStep), nameof(FootStep.RPC_Step)).GetParameters();
  private static bool OnStep(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnStepPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 3) return false;
    var index = (int)pars[1];
    Vector3 pos = (Vector3)pars[2];
    return Manager.Handle(ActionType.State, ["step", index.ToString(), Helper.Format(pos.x), Helper.Format(pos.z), Helper.Format(pos.y)], zdo);
  }

  static readonly int OnMaterialHash = "RPC_UpdateMaterial".GetStableHashCode();
  static readonly ParameterInfo[] OnMaterialPars = AccessTools.Method(typeof(MaterialVariation), nameof(MaterialVariation.RPC_UpdateMaterial)).GetParameters();
  private static bool OnMaterial(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnMaterialPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var index = (int)pars[1];
    return Manager.Handle(ActionType.State, ["material", index.ToString()], zdo);
  }

  static readonly int OnEffectsHash = "RPC_UpdateEffects".GetStableHashCode();
  static readonly ParameterInfo[] OnEffectsPars = AccessTools.Method(typeof(SapCollector), nameof(SapCollector.RPC_UpdateEffects)).GetParameters();
  private static bool OnEffects(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["effects"], zdo);
  }

  static readonly int OnHitHash = "RPC_HitNow".GetStableHashCode();
  static readonly ParameterInfo[] OnHitPars = AccessTools.Method(typeof(ShieldGenerator), nameof(ShieldGenerator.RPC_HitNow)).GetParameters();
  private static bool OnHit(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["hit"], zdo);
  }

  static readonly int OnUnsummonHash = "RPC_UnSummon".GetStableHashCode();
  static readonly ParameterInfo[] OnUnsummonPars = AccessTools.Method(typeof(Tameable), nameof(Tameable.RPC_UnSummon)).GetParameters();
  private static bool OnUnsummon(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["unsummon"], zdo);
  }

  static readonly int OnGrowHash = "RPC_Grow".GetStableHashCode();
  static readonly ParameterInfo[] OnGrowPars = AccessTools.Method(typeof(TreeBase), nameof(TreeBase.RPC_Grow)).GetParameters();
  private static bool OnGrow(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["grow"], zdo);
  }

  private static ZDO? GetSource(long id)
  {
    ZDO? source = null;
    if (id == ZDOMan.GetSessionID())
      source = Player.m_localPlayer?.m_nview?.GetZDO();
    else
    {
      var peer = ZNet.instance.GetPeer(id);
      if (peer != null)
        source = ZDOMan.instance.GetZDO(peer.m_characterID);
    }
    return source;
  }


  // GetSource(data.m_senderPeerID) could be used to get the player ZDO of the sender.
  // Currently not used, but left here for future reference.
  // For some RPCs this is always the player what did the action.
  private static readonly (int Hash, RPCHandler Handler, string[] States)[] AllAvailableHandlers = [
    (RepairHash, WNTHealthChanged, ["damage", "repair"]),
    (SetTriggerHash, SetTrigger, ["action"]),
    (SetTargetHash, SetTarget, ["target", "targeting"]),
    (ShakeHash, Shake, ["damage"]),
    (OnStateChangedHash, OnStateChanged, ["trap"]),
    (SetSaddleHash, SetSaddle, ["saddle", "unsaddle"]),
    (SayHash, Say, ["say"]), // Uses ActionType.Say
    (FlashShieldHash, FlashShield, ["flash"]),
    (SetPickedHash, SetPicked, ["picked", "unpicked"]),
    (PlayMusicHash, PlayMusic, ["music"]),
    (WakeupHash, Wakeup, ["wakeup"]),
    (SetAreaHealthHash, SetAreaHealth, ["damage"]),
    (HideHash, Hide, ["damage"]),
    (SetVisualItemHash, SetVisualItem, ["item"]),
    (AnimateLeverHash, AnimateLever, ["start"]),
    (AnimateLeverReturnHash, AnimateLeverReturn, ["end"]),
    (SetArmorVisualItemHash, SetArmorVisualItem, ["item"]),
    (SetSlotVisualHash, SetSlotVisual, ["item"]),
    (MakePieceHash, MakePiece, ["piece"]),
    (OnEatHash, OnEat, ["eat"]),
    (OnDeathHash, OnDeath, ["death"]),
    (OnSetPoseHash, OnSetPose, ["pose"]),
    (OnLegUseHash, OnLegUse, ["lock", "release"]),
    (OnSetLoadedHash, OnSetLoaded, ["loaded"]),
    (OnShootHash, OnShoot, ["shoot"]),
    (OnFreezeFrameHash, OnFreezeFrame, ["freezeframe"]),
    (OnResetClothHash, OnResetCloth, ["resetcloth"]),
    (OnFragmentsHash, OnFragments, ["fragments"]),
    (OnStepHash, OnStep, ["step"]),
    (OnMaterialHash, OnMaterial, ["material"]),
    (OnEffectsHash, OnEffects, ["effects"]),
    (OnHitHash, OnHit, ["hit"]),
    (OnUnsummonHash, OnUnsummon, ["unsummon"]),
    (OnGrowHash, OnGrow, ["grow"])
  ];

  private static readonly Dictionary<int, RPCHandler> RPCHandlers = new()
  {
    { RepairHash, WNTHealthChanged },
    { SetTriggerHash, SetTrigger },
    { SetTargetHash, SetTarget },
    { ShakeHash, Shake },
    { OnStateChangedHash, OnStateChanged },
    { SetSaddleHash, SetSaddle },
    { SayHash, Say },
    { FlashShieldHash, FlashShield },
    { SetPickedHash, SetPicked },
    { PlayMusicHash, PlayMusic },
    { WakeupHash, Wakeup },
    { SetAreaHealthHash, SetAreaHealth },
    { HideHash, Hide },
    { SetVisualItemHash, SetVisualItem },
    { AnimateLeverHash, AnimateLever },
    { AnimateLeverReturnHash, AnimateLeverReturn },
    { SetArmorVisualItemHash, SetArmorVisualItem },
    { SetSlotVisualHash, SetSlotVisual },
    { MakePieceHash, MakePiece },
    { OnEatHash, OnEat },
    { OnDeathHash, OnDeath },
    { OnSetPoseHash, OnSetPose },
    { OnLegUseHash, OnLegUse },
    { OnSetLoadedHash, OnSetLoaded },
    { OnShootHash, OnShoot },
    { OnFreezeFrameHash, OnFreezeFrame },
    { OnResetClothHash, OnResetCloth },
    { OnFragmentsHash, OnFragments },
    { OnStepHash, OnStep },
    { OnMaterialHash, OnMaterial },
    { OnEffectsHash, OnEffects },
    { OnHitHash, OnHit },
    { OnUnsummonHash, OnUnsummon },
    { OnGrowHash, OnGrow }
  };

}
