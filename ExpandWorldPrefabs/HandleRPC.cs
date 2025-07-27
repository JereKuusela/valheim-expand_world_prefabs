using System.Reflection;
using HarmonyLib;

namespace ExpandWorld.Prefab;

public class HandleRPC
{
  public static void Patch(Harmony harmony)
  {
    var method = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.HandleRoutedRPC));
    var patch = AccessTools.Method(typeof(HandleRPC), nameof(Handle));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
    method = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.RouteRPC));
    patch = AccessTools.Method(typeof(HandleRPC), nameof(RouteRPC));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
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
  // Not implemented:
  // SapCollector extract: Can be handled by created.
  // Tameable unsummon: Can be handled by destroyed.
  // TreeBase grow: Can be handled by created/destroyed.
  // WearNTear destroy: Can be handled by destroyed.
  // Player death: Can be handled by destroyed.
  // Foot step: Not handled, might be spammy.
  // Character resetcloth / freezeframe: Not handled, not sure what it does.
  static bool Handle(ZRoutedRpc.RoutedRPCData data)
  {
    var zdo = ZDOMan.instance.GetZDO(data.m_targetZDO);
    if (zdo == null) return true;
    var cancel = false;
    if (data.m_methodHash == RepairHash)
      cancel = WNTHealthChanged(zdo, data);
    else if (data.m_methodHash == SetTriggerHash)
      cancel = SetTrigger(zdo, data);
    else if (data.m_methodHash == SetTargetHash)
      cancel = SetTarget(zdo, data);
    else if (data.m_methodHash == ShakeHash)
      cancel = Shake(zdo);
    else if (data.m_methodHash == OnStateChangedHash)
      cancel = OnStateChanged(zdo, data);
    else if (data.m_methodHash == SetSaddleHash)
      cancel = SetSaddle(zdo, data);
    else if (data.m_methodHash == SayHash)
      cancel = Say(zdo, data);
    else if (data.m_methodHash == FlashShieldHash)
      cancel = FlashShield(zdo);
    else if (data.m_methodHash == SetPickedHash)
      cancel = SetPicked(zdo, data);
    else if (data.m_methodHash == PlayMusicHash)
      cancel = PlayMusic(zdo);
    else if (data.m_methodHash == WakeupHash)
      cancel = Wakeup(zdo);
    else if (data.m_methodHash == SetAreaHealthHash)
      cancel = SetAreaHealth(zdo);
    else if (data.m_methodHash == HideHash)
      cancel = Hide(zdo, data);
    else if (data.m_methodHash == SetVisualItemHash)
      cancel = SetVisualItem(zdo, data);
    else if (data.m_methodHash == AnimateLeverHash)
      cancel = AnimateLever(zdo, data);
    else if (data.m_methodHash == AnimateLeverReturnHash)
      cancel = AnimateLeverReturn(zdo, data);
    else if (data.m_methodHash == SetArmorVisualItemHash)
      cancel = SetArmorVisualItem(zdo, data);
    else if (data.m_methodHash == SetSlotVisualHash)
      cancel = SetSlotVisual(zdo, data);
    else if (data.m_methodHash == MakePieceHash)
      cancel = MakePiece(zdo, data);
    else if (data.m_methodHash == OnEatHash)
      cancel = OnEat(zdo, data);
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
      return Manager.Handle(ActionType.Repair, [], zdo, GetSource(data.m_senderPeerID));
    else
      return Manager.Handle(ActionType.Damage, [], zdo);
  }

  static readonly int SetTriggerHash = "SetTrigger".GetStableHashCode();
  static readonly ParameterInfo[] SetTriggerPars = AccessTools.Method(typeof(ZSyncAnimation), nameof(ZSyncAnimation.RPC_SetTrigger)).GetParameters();
  private static bool SetTrigger(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetTriggerPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var trigger = (string)pars[1];
    return Manager.Handle(ActionType.State, [trigger], zdo);
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
    var cancel1 = Manager.Handle(ActionType.State, [targetPrefab.name], zdo);
    var cancel2 = Manager.Handle(ActionType.State, ["target"], targetZDO);
    return cancel1 || cancel2;
  }
  static readonly int ShakeHash = "RPC_Shake".GetStableHashCode();
  static readonly ParameterInfo[] ShakePars = AccessTools.Method(typeof(TreeBase), nameof(TreeBase.RPC_Shake)).GetParameters();
  private static bool Shake(ZDO zdo)
  {
    return Manager.Handle(ActionType.Damage, [], zdo);
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
    return Manager.Handle(ActionType.State, [state.ToString()], zdo);
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
  private static bool FlashShield(ZDO zdo)
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
  private static bool PlayMusic(ZDO zdo)
  {
    return Manager.Handle(ActionType.State, [], zdo);
  }
  static readonly int WakeupHash = "RPC_Wakeup".GetStableHashCode();
  static readonly ParameterInfo[] WakeupPars = AccessTools.Method(typeof(MonsterAI), nameof(MonsterAI.RPC_Wakeup)).GetParameters();
  private static bool Wakeup(ZDO zdo)
  {
    return Manager.Handle(ActionType.State, ["wakeup"], zdo);
  }
  static readonly int SetAreaHealthHash = "RPC_SetAreaHealth".GetStableHashCode();
  static readonly ParameterInfo[] SetAreaHealthPars = AccessTools.Method(typeof(MineRock5), nameof(MineRock5.RPC_SetAreaHealth)).GetParameters();
  private static bool SetAreaHealth(ZDO zdo)
  {
    return Manager.Handle(ActionType.Damage, [], zdo);
  }
  static readonly int HideHash = "Hide".GetStableHashCode();
  static readonly ParameterInfo[] HidePars = AccessTools.Method(typeof(MineRock), nameof(MineRock.RPC_Hide)).GetParameters();
  private static bool Hide(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, HidePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var index = (int)pars[1];
    return Manager.Handle(ActionType.Damage, [index.ToString()], zdo);
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
    return Manager.Handle(ActionType.State, [state, variant.ToString(), quality.ToString()], zdo, GetSource(data.m_senderPeerID));
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
    return Manager.Handle(ActionType.State, [state, variant.ToString(), slot.ToString()], zdo, GetSource(data.m_senderPeerID));
  }
  static readonly int AnimateLeverHash = "RPC_AnimateLever".GetStableHashCode();
  static readonly ParameterInfo[] AnimateLeverPars = AccessTools.Method(typeof(Incinerator), nameof(Incinerator.RPC_AnimateLever)).GetParameters();
  private static bool AnimateLever(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["start"], zdo, GetSource(data.m_senderPeerID));
  }
  static readonly int AnimateLeverReturnHash = "RPC_AnimateLeverReturn".GetStableHashCode();
  static readonly ParameterInfo[] AnimateLeverReturnPars = AccessTools.Method(typeof(Incinerator), nameof(Incinerator.RPC_AnimateLeverReturn)).GetParameters();
  private static bool AnimateLeverReturn(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["end"], zdo, GetSource(data.m_senderPeerID));
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
    return Manager.Handle(ActionType.State, [slot.ToString(), state], zdo, GetSource(data.m_senderPeerID));
  }


  static readonly int MakePieceHash = "RPC_MakePiece".GetStableHashCode();
  static readonly ParameterInfo[] MakePieceHashPars = AccessTools.Method(typeof(ItemDrop), nameof(ItemDrop.RPC_MakePiece)).GetParameters();
  private static bool MakePiece(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["piece"], zdo, GetSource(data.m_senderPeerID));
  }

  static readonly int OnEatHash = "RPC_OnEat".GetStableHashCode();
  static readonly ParameterInfo[] OnEatPars = AccessTools.Method(typeof(Feast), nameof(Feast.RPC_OnEat)).GetParameters();
  private static bool OnEat(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.State, ["eat"], zdo, GetSource(data.m_senderPeerID));
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
}
