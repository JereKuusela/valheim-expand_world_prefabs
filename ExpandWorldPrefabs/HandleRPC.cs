using System.Reflection;
using HarmonyLib;

namespace ExpandWorld.Prefab;

public class HandleRPC
{
  public static void Patch(Harmony harmony)
  {
    var method = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.HandleRoutedRPC));
    var patch = AccessTools.Method(typeof(HandleRPC), nameof(Handle));
    harmony.Patch(method, postfix: new HarmonyMethod(patch));
  }
  // Not implemented:
  // SapCollector extract: Can be handled by created.
  // Tameable unsummon: Can be handled by destroyed.
  // TreeBase grow: Can be handled by created/destroyed.
  // WearNTear destroy: Can be handled by destroyed.
  // Player death: Can be handled by destroyed.
  // Foot step: Not handled, might be spammy.
  // Character resetcloth / freezeframe: Not handled, not sure what it does.
  static void Handle(ZRoutedRpc.RoutedRPCData data)
  {
    var zdo = ZDOMan.instance.GetZDO(data.m_targetZDO);
    if (zdo == null) return;
    if (data.m_methodHash == RepairHash)
      WNTHealthChanged(zdo, data);
    else if (data.m_methodHash == SetTriggerHash)
      SetTrigger(zdo, data);
    else if (data.m_methodHash == SetTargetHash)
      SetTarget(zdo, data);
    else if (data.m_methodHash == ShakeHash)
      Shake(zdo);
    else if (data.m_methodHash == OnStateChangedHash)
      OnStateChanged(zdo, data);
    else if (data.m_methodHash == SetSaddleHash)
      SetSaddle(zdo, data);
    else if (data.m_methodHash == SayHash)
      Say(zdo, data);
    else if (data.m_methodHash == FlashShieldHash)
      FlashShield(zdo);
    else if (data.m_methodHash == SetPickedHash)
      SetPicked(zdo, data);
    else if (data.m_methodHash == PlayMusicHash)
      PlayMusic(zdo);
    else if (data.m_methodHash == WakeupHash)
      Wakeup(zdo);
    else if (data.m_methodHash == SetAreaHealthHash)
      SetAreaHealth(zdo);
    else if (data.m_methodHash == HideHash)
      Hide(zdo, data);
    else if (data.m_methodHash == SetVisualItemHash)
      SetVisualItem(zdo, data);
    else if (data.m_methodHash == AnimateLeverHash)
      AnimateLever(zdo, data);
    else if (data.m_methodHash == AnimateLeverReturnHash)
      AnimateLeverReturn(zdo, data);
    else if (data.m_methodHash == SetArmorVisualItemHash)
      SetArmorVisualItem(zdo, data);
    else if (data.m_methodHash == SetSlotVisualHash)
      SetSlotVisual(zdo, data);
  }


  static readonly int RepairHash = "RPC_HealthChanged".GetStableHashCode();
  static readonly ParameterInfo[] RepairPars = AccessTools.Method(typeof(WearNTear), nameof(WearNTear.RPC_HealthChanged)).GetParameters();
  private static void WNTHealthChanged(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    if (!prefab) return;
    if (!prefab.TryGetComponent(out WearNTear wearNTear)) return;
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RepairPars, data.m_parameters);
    if (pars.Length < 2) return;
    var health = (float)pars[1];
    if (health > 1E20) return;
    if (health == wearNTear.m_health)
      Manager.Handle(ActionType.Repair, "", zdo, GetSource(data.m_senderPeerID));
    else
      Manager.Handle(ActionType.Damage, "", zdo);
  }

  static readonly int SetTriggerHash = "SetTrigger".GetStableHashCode();
  static readonly ParameterInfo[] SetTriggerPars = AccessTools.Method(typeof(ZSyncAnimation), nameof(ZSyncAnimation.RPC_SetTrigger)).GetParameters();
  private static void SetTrigger(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetTriggerPars, data.m_parameters);
    if (pars.Length < 2) return;
    var trigger = (string)pars[1];
    Manager.Handle(ActionType.State, trigger, zdo);
  }
  static readonly int SetTargetHash = "RPC_SetTarget".GetStableHashCode();
  static readonly ParameterInfo[] SetTargetPars = AccessTools.Method(typeof(Turret), nameof(Turret.RPC_SetTarget)).GetParameters();
  private static void SetTarget(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetTargetPars, data.m_parameters);
    if (pars.Length < 2) return;
    var target = (ZDOID)pars[1];
    if (target == ZDOID.None) return;
    var targetZDO = ZDOMan.instance.GetZDO(target);
    if (targetZDO == null) return;
    var targetPrefab = ZNetScene.instance.GetPrefab(targetZDO.GetPrefab());
    if (!targetPrefab) return;
    Manager.Handle(ActionType.State, targetPrefab.name, zdo);
    Manager.Handle(ActionType.State, "target", targetZDO);
  }
  static readonly int ShakeHash = "RPC_Shake".GetStableHashCode();
  static readonly ParameterInfo[] ShakePars = AccessTools.Method(typeof(TreeBase), nameof(TreeBase.RPC_Shake)).GetParameters();
  private static void Shake(ZDO zdo)
  {
    Manager.Handle(ActionType.Damage, "", zdo);
  }
  static readonly int OnStateChangedHash = "RPC_OnStateChanged".GetStableHashCode();
  static readonly ParameterInfo[] OnStateChangedPars = AccessTools.Method(typeof(Trap), nameof(Trap.RPC_OnStateChanged)).GetParameters();
  private static void OnStateChanged(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnStateChangedPars, data.m_parameters);
    if (pars.Length < 2) return;
    var state = (int)pars[1];
    if (state == 0) return;
    Manager.Handle(ActionType.State, state.ToString(), zdo);
  }
  static readonly int SetSaddleHash = "SetSaddle".GetStableHashCode();
  static readonly ParameterInfo[] SetSaddlePars = AccessTools.Method(typeof(Tameable), nameof(Tameable.RPC_SetSaddle)).GetParameters();
  private static void SetSaddle(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetSaddlePars, data.m_parameters);
    if (pars.Length < 2) return;
    var saddle = (bool)pars[1];
    Manager.Handle(ActionType.State, saddle ? "saddle" : "unsaddle", zdo);
  }
  static readonly int SayHash = "Say".GetStableHashCode();
  static readonly ParameterInfo[] SayPars = AccessTools.Method(typeof(Talker), nameof(Talker.RPC_Say)).GetParameters();
  private static void Say(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SayPars, data.m_parameters);
    if (pars.Length < 2) return;
    var text = (string)pars[3];
    var userId = (string)pars[4];
    if (ZNet.instance.IsAdmin(userId))
      Manager.Handle(ActionType.Command, text, zdo);
    else
      Manager.Handle(ActionType.Say, text, zdo);
  }
  static readonly int FlashShieldHash = "FlashShield".GetStableHashCode();
  static readonly ParameterInfo[] FlashShieldPars = AccessTools.Method(typeof(PrivateArea), nameof(PrivateArea.RPC_FlashShield)).GetParameters();
  private static void FlashShield(ZDO zdo)
  {
    Manager.Handle(ActionType.State, "flash", zdo);
  }
  static readonly int SetPickedHash = "RPC_SetPicked".GetStableHashCode();
  static readonly ParameterInfo[] SetPickedPars = AccessTools.Method(typeof(Pickable), nameof(Pickable.RPC_SetPicked)).GetParameters();
  private static void SetPicked(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetPickedPars, data.m_parameters);
    if (pars.Length < 2) return;
    var picked = (bool)pars[1];
    Manager.Handle(ActionType.State, picked ? "picked" : "unpicked", zdo);
  }
  static readonly int PlayMusicHash = "RPC_PlayMusic".GetStableHashCode();
  static readonly ParameterInfo[] PlayMusicPars = AccessTools.Method(typeof(MusicVolume), nameof(MusicVolume.RPC_PlayMusic)).GetParameters();
  private static void PlayMusic(ZDO zdo)
  {
    Manager.Handle(ActionType.State, "", zdo);
  }
  static readonly int WakeupHash = "RPC_Wakeup".GetStableHashCode();
  static readonly ParameterInfo[] WakeupPars = AccessTools.Method(typeof(MonsterAI), nameof(MonsterAI.RPC_Wakeup)).GetParameters();
  private static void Wakeup(ZDO zdo)
  {
    Manager.Handle(ActionType.State, "wakeup", zdo);
  }
  static readonly int SetAreaHealthHash = "RPC_SetAreaHealth".GetStableHashCode();
  static readonly ParameterInfo[] SetAreaHealthPars = AccessTools.Method(typeof(MineRock5), nameof(MineRock5.RPC_SetAreaHealth)).GetParameters();
  private static void SetAreaHealth(ZDO zdo)
  {
    Manager.Handle(ActionType.Damage, "", zdo);
  }
  static readonly int HideHash = "Hide".GetStableHashCode();
  static readonly ParameterInfo[] HidePars = AccessTools.Method(typeof(MineRock), nameof(MineRock.RPC_Hide)).GetParameters();
  private static void Hide(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, HidePars, data.m_parameters);
    if (pars.Length < 2) return;
    var index = (int)pars[1];
    Manager.Handle(ActionType.Damage, index.ToString(), zdo);
  }
  static readonly int SetVisualItemHash = "SetVisualItem".GetStableHashCode();
  static readonly ParameterInfo[] ItemStandPars = AccessTools.Method(typeof(ItemStand), nameof(ItemStand.RPC_SetVisualItem)).GetParameters();

  private static void SetVisualItem(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    if (!prefab) return;
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, ItemStandPars, data.m_parameters);
    if (pars.Length < 4) return;
    var item = (string)pars[1];
    var variant = (int)pars[2];
    var quality = (int)pars[3];
    var state = $"{(item == "" ? "none" : item)} {variant} {quality}";
    Manager.Handle(ActionType.State, state, zdo, GetSource(data.m_senderPeerID));
  }
  static readonly int SetArmorVisualItemHash = "RPC_SetVisualItem".GetStableHashCode();
  static readonly ParameterInfo[] ArmorStandPars = AccessTools.Method(typeof(ArmorStand), nameof(ArmorStand.RPC_SetVisualItem)).GetParameters();
  private static void SetArmorVisualItem(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
    if (!prefab) return;
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, ArmorStandPars, data.m_parameters);
    if (pars.Length < 4) return;
    var slot = (int)pars[1];
    var item = (string)pars[2];
    var variant = (int)pars[3];
    var state = $"{(item == "" ? "none" : item)} {variant} {slot} ";
    Manager.Handle(ActionType.State, state, zdo, GetSource(data.m_senderPeerID));
  }
  static readonly int AnimateLeverHash = "RPC_AnimateLever".GetStableHashCode();
  static readonly ParameterInfo[] AnimateLeverPars = AccessTools.Method(typeof(Incinerator), nameof(Incinerator.RPC_AnimateLever)).GetParameters();
  private static void AnimateLever(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    Manager.Handle(ActionType.State, "start", zdo, GetSource(data.m_senderPeerID));
  }
  static readonly int AnimateLeverReturnHash = "RPC_AnimateLeverReturn".GetStableHashCode();
  static readonly ParameterInfo[] AnimateLeverReturnPars = AccessTools.Method(typeof(Incinerator), nameof(Incinerator.RPC_AnimateLeverReturn)).GetParameters();
  private static void AnimateLeverReturn(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    Manager.Handle(ActionType.State, "end", zdo, GetSource(data.m_senderPeerID));
  }
  static readonly int SetSlotVisualHash = "RPC_SetSlotVisual".GetStableHashCode();
  static readonly ParameterInfo[] SetSlotVisualPars = AccessTools.Method(typeof(CookingStation), nameof(CookingStation.RPC_SetSlotVisual)).GetParameters();
  private static void SetSlotVisual(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    data.m_parameters.SetPos(0);
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetSlotVisualPars, data.m_parameters);
    if (pars.Length < 3) return;
    var slot = (int)pars[1];
    var item = (string)pars[2];
    var state = $"{slot} {(item == "" ? "none" : item)}";
    Manager.Handle(ActionType.State, state, zdo, GetSource(data.m_senderPeerID));
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
