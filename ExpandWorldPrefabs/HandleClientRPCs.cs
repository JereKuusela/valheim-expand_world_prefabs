using System.Collections.Generic;
using System.Reflection;
using Data;
using HarmonyLib;

namespace ExpandWorld.Prefab;

// Same idea as HandleRPC, but for RPCs that vanilla only routes to the owning client.
// These only ever reach the server when the object is owned by the server (see ServerOwned).
public class HandleClientRPCs
{
  private delegate bool RPCHandler(ZDO zdo, ZRoutedRpc.RoutedRPCData data);
  private static readonly Dictionary<int, RPCHandler> Handlers = [];
  private static bool IsPatched = false;

  public static void Patch(Harmony harmony, bool shouldPatch)
  {
    if (shouldPatch && !IsPatched)
      DoPatch(harmony);
    if (!shouldPatch && IsPatched)
      DoUnpatch(harmony);
    if (!shouldPatch)
      Handlers.Clear();
  }

  private static void DoPatch(Harmony harmony)
  {
    IsPatched = true;
    var method = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.HandleRoutedRPC));
    var patch = AccessTools.Method(typeof(HandleClientRPCs), nameof(HandleClient));
    harmony.Patch(method, prefix: new HarmonyMethod(patch));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    IsPatched = false;
    var method = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.HandleRoutedRPC));
    var patch = AccessTools.Method(typeof(HandleClientRPCs), nameof(HandleClient));
    harmony.Unpatch(method, patch);
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

  static bool HandleClient(ZRoutedRpc.RoutedRPCData data)
  {
    var zdo = ZDOMan.instance.GetZDO(data.m_targetZDO);
    if (zdo == null) return true;

    var cancel = false;
    if (Handlers.TryGetValue(data.m_methodHash, out var handler))
      cancel = handler(zdo, data);

    return !cancel;
  }

  static readonly int AlertHash = ZdoHelper.Hash("Alert");
  private static bool Alert(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["alert"], zdo);
  }

  static readonly int SetAggravatedHash = ZdoHelper.Hash("SetAggravated");
  static readonly ParameterInfo[] SetAggravatedPars = AccessTools.Method(typeof(BaseAI), nameof(BaseAI.SetAggravated)).GetParameters();
  private static bool SetAggravated(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetAggravatedPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 3) return false;
    var aggravated = (bool)pars[1];
    var reason = (int)pars[2];
    return Manager.Handle(ActionType.ClientState, ["aggravated", aggravated ? "true" : "false", reason.ToString()], zdo);
  }

  static readonly int RPCDamageHash = ZdoHelper.Hash("RPC_Damage");
  private static bool RPCDamage(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["damage"], zdo);
  }

  static readonly int RPCHealHash = ZdoHelper.Hash("RPC_Heal");
  static readonly ParameterInfo[] RPCHealPars = AccessTools.Method(typeof(Character), nameof(Character.RPC_Heal)).GetParameters();
  private static bool RPCHeal(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCHealPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var amount = (float)pars[1];
    return Manager.Handle(ActionType.ClientState, ["heal", Helper.Format2(amount)], zdo);
  }

  static readonly int RPCStaggerHash = ZdoHelper.Hash("RPC_Stagger");
  private static bool RPCStagger(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["stagger"], zdo);
  }

  static readonly int RPCSetTamedHash = ZdoHelper.Hash("RPC_SetTamed");
  static readonly ParameterInfo[] RPCSetTamedPars = AccessTools.Method(typeof(Character), nameof(Character.RPC_SetTamed)).GetParameters();
  private static bool RPCSetTamed(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCSetTamedPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var tamed = (bool)pars[1];
    return Manager.Handle(ActionType.ClientState, [tamed ? "tamed" : "untamed"], zdo);
  }

  static readonly int RPCAddFuelHash = ZdoHelper.Hash("RPC_AddFuel");
  private static bool RPCAddFuel(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["fuel"], zdo);
  }

  static readonly int UseDoorHash = ZdoHelper.Hash("UseDoor");
  static readonly ParameterInfo[] UseDoorPars = AccessTools.Method(typeof(Door), nameof(Door.RPC_UseDoor)).GetParameters();
  private static bool UseDoor(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, UseDoorPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var forward = (bool)pars[1];
    return Manager.Handle(ActionType.ClientState, ["door", forward ? "forward" : "backward"], zdo);
  }

  static readonly int RPCToggleOnHash = ZdoHelper.Hash("RPC_ToggleOn");
  private static bool RPCToggleOn(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["toggle"], zdo);
  }

  static readonly int MineRockHitHash = ZdoHelper.Hash("Hit");
  static readonly ParameterInfo[] MineRockHitPars = AccessTools.Method(typeof(MineRock), nameof(MineRock.RPC_Hit)).GetParameters();
  private static bool MineRockHit(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, MineRockHitPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 3) return false;
    var index = (int)pars[2];
    return Manager.Handle(ActionType.ClientState, ["hit", index.ToString()], zdo);
  }

  static readonly int MineRock5DamageHash = ZdoHelper.Hash("RPC_Damage");
  static readonly ParameterInfo[] MineRock5DamagePars = AccessTools.Method(typeof(MineRock5), nameof(MineRock5.RPC_Damage)).GetParameters();
  private static bool MineRock5Damage(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, MineRock5DamagePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 3) return false;
    var index = (int)pars[2];
    return Manager.Handle(ActionType.ClientState, ["hit", index.ToString()], zdo);
  }

  static readonly int RPCSleepHash = ZdoHelper.Hash("RPC_Sleep");
  private static bool RPCSleep(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["sleep"], zdo);
  }

  static readonly int OnTargetedHash = ZdoHelper.Hash("OnTargeted");
  static readonly ParameterInfo[] OnTargetedPars = AccessTools.Method(typeof(Player), nameof(Player.OnTargeted)).GetParameters();
  private static bool OnTargeted(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, OnTargetedPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 3) return false;
    var sensed = (bool)pars[1];
    var targeted = (bool)pars[2];
    return Manager.Handle(ActionType.ClientState, ["targeted", sensed ? "true" : "false", targeted ? "true" : "false"], zdo);
  }

  static readonly int UseStaminaHash = ZdoHelper.Hash("UseStamina");
  static readonly ParameterInfo[] UseStaminaPars = AccessTools.Method(typeof(Player), nameof(Player.UseStamina)).GetParameters();
  private static bool UseStamina(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, UseStaminaPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var amount = (float)pars[1];
    return Manager.Handle(ActionType.ClientState, ["stamina", Helper.Format2(amount)], zdo);
  }

  static readonly int RPCHitWhileDodgingHash = ZdoHelper.Hash("RPC_HitWhileDodging");
  private static bool RPCHitWhileDodging(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["dodge"], zdo);
  }

  static readonly int ToggleEnabledHash = ZdoHelper.Hash("ToggleEnabled");
  private static bool ToggleEnabled(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["ward_toggle"], zdo);
  }

  static readonly int RPCOnHitHash = ZdoHelper.Hash("RPC_OnHit");
  private static bool RPCOnHit(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["hit"], zdo);
  }

  static readonly int RPCDrainHash = ZdoHelper.Hash("RPC_Drain");
  static readonly ParameterInfo[] RPCDrainPars = AccessTools.Method(typeof(ResourceRoot), nameof(ResourceRoot.RPC_Drain)).GetParameters();
  private static bool RPCDrain(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCDrainPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var amount = (float)pars[1];
    return Manager.Handle(ActionType.ClientState, ["drain", Helper.Format2(amount)], zdo);
  }

  static readonly int RPCAttackHash = ZdoHelper.Hash("RPC_Attack");
  private static bool RPCAttack(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["attack"], zdo);
  }

  static readonly int RPCAddAmmoHash = ZdoHelper.Hash("RPC_AddAmmo");
  static readonly ParameterInfo[] RPCAddAmmoPars = AccessTools.Method(typeof(Turret), nameof(Turret.RPC_AddAmmo)).GetParameters();
  private static bool RPCAddAmmo(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCAddAmmoPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var name = (string)pars[1];
    return Manager.Handle(ActionType.ClientState, ["ammo", name], zdo);
  }

  // Shared by Beehive and SapCollector (same RPC name/hash).
  static readonly int RPCExtractHash = ZdoHelper.Hash("RPC_Extract");
  private static bool RPCExtract(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["extract"], zdo);
  }

  static readonly int RPCAddNoiseHash = ZdoHelper.Hash("RPC_AddNoise");
  static readonly ParameterInfo[] RPCAddNoisePars = AccessTools.Method(typeof(Character), nameof(Character.RPC_AddNoise)).GetParameters();
  private static bool RPCAddNoise(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCAddNoisePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var amount = (float)pars[1];
    return Manager.Handle(ActionType.ClientState, ["noise", Helper.Format2(amount)], zdo);
  }

  static readonly int RPCAddAdrenalineHash = ZdoHelper.Hash("RPC_AddAdrenaline");
  static readonly ParameterInfo[] RPCAddAdrenalinePars = AccessTools.Method(typeof(Character), nameof(Character.RPC_AddAdrenaline)).GetParameters();
  private static bool RPCAddAdrenaline(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCAddAdrenalinePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var amount = (float)pars[1];
    return Manager.Handle(ActionType.ClientState, ["adrenaline", Helper.Format2(amount)], zdo);
  }

  // Shared by CookingStation and Fermenter (same RPC name/hash).
  static readonly int RPCAddItemHash = ZdoHelper.Hash("RPC_AddItem");
  static readonly ParameterInfo[] RPCAddItemPars = AccessTools.Method(typeof(CookingStation), nameof(CookingStation.RPC_AddItem)).GetParameters();
  private static bool RPCAddItem(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCAddItemPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var item = (string)pars[1];
    return Manager.Handle(ActionType.ClientState, ["item", item], zdo);
  }

  static readonly int RPCRemoveDoneItemHash = ZdoHelper.Hash("RPC_RemoveDoneItem");
  private static bool RPCRemoveDoneItem(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["donecooking"], zdo);
  }

  static readonly int RPCTapHash = ZdoHelper.Hash("RPC_Tap");
  private static bool RPCTap(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["tap"], zdo);
  }

  // Shared by RPC_AddFuelAmount and RPC_SetFuelAmount (Fireplace).
  static readonly int RPCAddFuelAmountHash = ZdoHelper.Hash("RPC_AddFuelAmount");
  static readonly ParameterInfo[] RPCAddFuelAmountPars = AccessTools.Method(typeof(Fireplace), nameof(Fireplace.RPC_AddFuelAmount)).GetParameters();
  private static bool RPCAddFuelAmount(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCAddFuelAmountPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var amount = (float)pars[1];
    return Manager.Handle(ActionType.ClientState, ["fuel", Helper.Format2(amount)], zdo);
  }

  static readonly int RPCSetFuelAmountHash = ZdoHelper.Hash("RPC_SetFuelAmount");
  static readonly ParameterInfo[] RPCSetFuelAmountPars = AccessTools.Method(typeof(Fireplace), nameof(Fireplace.RPC_SetFuelAmount)).GetParameters();
  private static bool RPCSetFuelAmount(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCSetFuelAmountPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var amount = (float)pars[1];
    return Manager.Handle(ActionType.ClientState, ["fuel", Helper.Format2(amount)], zdo);
  }

  static readonly int SetPlayedHash = ZdoHelper.Hash("SetPlayed");
  private static bool SetPlayed(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["played"], zdo);
  }

  static readonly int RPCPickHash = ZdoHelper.Hash("RPC_Pick");
  static readonly ParameterInfo[] RPCPickPars = AccessTools.Method(typeof(Pickable), nameof(Pickable.RPC_Pick)).GetParameters();
  private static bool RPCPick(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCPickPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var extra = (int)pars[1];
    return Manager.Handle(ActionType.ClientState, ["pick", extra.ToString()], zdo);
  }

  static readonly int PickHash = ZdoHelper.Hash("Pick");
  private static bool Pick(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["pick"], zdo);
  }

  static readonly int TogglePermittedHash = ZdoHelper.Hash("TogglePermitted");
  private static bool TogglePermitted(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["ward_permitted"], zdo);
  }

  static readonly int RPCAddStatusEffectHash = ZdoHelper.Hash("RPC_AddStatusEffect");
  static readonly ParameterInfo[] RPCAddStatusEffectPars = AccessTools.Method(typeof(SEMan), nameof(SEMan.RPC_AddStatusEffect)).GetParameters();
  private static bool RPCAddStatusEffect(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCAddStatusEffectPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var hash = (int)pars[1];
    return Manager.Handle(ActionType.ClientState, ["statuseffect", hash.ToString()], zdo);
  }

  static readonly int RPCSetFuelHash = ZdoHelper.Hash("RPC_SetFuel");
  static readonly ParameterInfo[] RPCSetFuelPars = AccessTools.Method(typeof(ShieldGenerator), nameof(ShieldGenerator.RPC_SetFuel)).GetParameters();
  private static bool RPCSetFuel(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCSetFuelPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var amount = (float)pars[1];
    return Manager.Handle(ActionType.ClientState, ["fuel", Helper.Format2(amount)], zdo);
  }

  static readonly int RPCAddOreHash = ZdoHelper.Hash("RPC_AddOre");
  static readonly ParameterInfo[] RPCAddOrePars = AccessTools.Method(typeof(Smelter), nameof(Smelter.RPC_AddOre)).GetParameters();
  private static bool RPCAddOre(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCAddOrePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var item = (string)pars[1];
    return Manager.Handle(ActionType.ClientState, ["ore", item], zdo);
  }

  static readonly int RPCEmptyProcessedHash = ZdoHelper.Hash("RPC_EmptyProcessed");
  private static bool RPCEmptyProcessed(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["empty"], zdo);
  }

  static readonly int AddSaddleHash = ZdoHelper.Hash("AddSaddle");
  private static bool AddSaddle(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["addsaddle"], zdo);
  }

  static readonly int CommandHash = ZdoHelper.Hash("Command");
  private static bool Command(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["command"], zdo);
  }

  static readonly int SetNameHash = ZdoHelper.Hash("SetName");
  static readonly ParameterInfo[] SetNamePars = AccessTools.Method(typeof(Tameable), nameof(Tameable.SetName)).GetParameters();
  private static bool SetName(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, SetNamePars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var name = (string)pars[1];
    return Manager.Handle(ActionType.ClientState, ["name", name], zdo);
  }

  static readonly int RemoveSaddleHash = ZdoHelper.Hash("RemoveSaddle");
  private static bool RemoveSaddle(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["removesaddle"], zdo);
  }

  static readonly int RPCSetTagHash = ZdoHelper.Hash("RPC_SetTag");
  static readonly ParameterInfo[] RPCSetTagPars = AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.RPC_SetTag)).GetParameters();
  private static bool RPCSetTag(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    var pars = ZNetView.Deserialize(data.m_senderPeerID, RPCSetTagPars, data.m_parameters);
    data.m_parameters.SetPos(0);
    if (pars.Length < 2) return false;
    var tag = (string)pars[1];
    return Manager.Handle(ActionType.ClientState, ["tag", tag], zdo);
  }

  static readonly int TriggerHash = ZdoHelper.Hash("Trigger");
  private static bool Trigger(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["trigger"], zdo);
  }

  static readonly int RPCClearCachedSupportHash = ZdoHelper.Hash("RPC_ClearCachedSupport");
  private static bool RPCClearCachedSupport(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["clearsupport"], zdo);
  }

  static readonly int RPCRemoveHash = ZdoHelper.Hash("RPC_Remove");
  private static bool RPCRemove(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["remove"], zdo);
  }

  static readonly int RPCRepairHash = ZdoHelper.Hash("RPC_Repair");
  private static bool RPCRepair(ZDO zdo, ZRoutedRpc.RoutedRPCData data)
  {
    return Manager.Handle(ActionType.ClientState, ["repair"], zdo);
  }

  // Method names/signatures below are transcribed from docs/RPCs.md.
  private static readonly (int Hash, RPCHandler Handler, string[] States)[] AllAvailableHandlers = [
    (AlertHash, Alert, ["alert"]),
    (SetAggravatedHash, SetAggravated, ["aggravated"]),
    (RPCDamageHash, RPCDamage, ["damage"]),
    (RPCHealHash, RPCHeal, ["heal"]),
    (RPCStaggerHash, RPCStagger, ["stagger"]),
    (RPCSetTamedHash, RPCSetTamed, ["tamed", "untamed"]),
    (RPCAddFuelHash, RPCAddFuel, ["fuel"]),
    (UseDoorHash, UseDoor, ["door"]),
    (RPCToggleOnHash, RPCToggleOn, ["toggle"]),
    (MineRockHitHash, MineRockHit, ["hit"]),
    (MineRock5DamageHash, MineRock5Damage, ["hit"]),
    (RPCSleepHash, RPCSleep, ["sleep"]),
    (OnTargetedHash, OnTargeted, ["targeted"]),
    (UseStaminaHash, UseStamina, ["stamina"]),
    (RPCHitWhileDodgingHash, RPCHitWhileDodging, ["dodge"]),
    (ToggleEnabledHash, ToggleEnabled, ["ward_toggle"]),
    (RPCOnHitHash, RPCOnHit, ["hit"]),
    (RPCDrainHash, RPCDrain, ["drain"]),
    (RPCAttackHash, RPCAttack, ["attack"]),
    (RPCAddAmmoHash, RPCAddAmmo, ["ammo"]),
    (RPCExtractHash, RPCExtract, ["extract"]),
    (RPCAddNoiseHash, RPCAddNoise, ["noise"]),
    (RPCAddAdrenalineHash, RPCAddAdrenaline, ["adrenaline"]),
    (RPCAddItemHash, RPCAddItem, ["item"]),
    (RPCRemoveDoneItemHash, RPCRemoveDoneItem, ["donecooking"]),
    (RPCTapHash, RPCTap, ["tap"]),
    (RPCAddFuelAmountHash, RPCAddFuelAmount, ["fuel"]),
    (RPCSetFuelAmountHash, RPCSetFuelAmount, ["fuel"]),
    (SetPlayedHash, SetPlayed, ["played"]),
    (RPCPickHash, RPCPick, ["pick"]),
    (PickHash, Pick, ["pick"]),
    (TogglePermittedHash, TogglePermitted, ["ward_permitted"]),
    (RPCAddStatusEffectHash, RPCAddStatusEffect, ["statuseffect"]),
    (RPCSetFuelHash, RPCSetFuel, ["fuel"]),
    (RPCAddOreHash, RPCAddOre, ["ore"]),
    (RPCEmptyProcessedHash, RPCEmptyProcessed, ["empty"]),
    (AddSaddleHash, AddSaddle, ["addsaddle"]),
    (CommandHash, Command, ["command"]),
    (SetNameHash, SetName, ["name"]),
    (RemoveSaddleHash, RemoveSaddle, ["removesaddle"]),
    (RPCSetTagHash, RPCSetTag, ["tag"]),
    (TriggerHash, Trigger, ["trigger"]),
    (RPCClearCachedSupportHash, RPCClearCachedSupport, ["clearsupport"]),
    (RPCRemoveHash, RPCRemove, ["remove"]),
    (RPCRepairHash, RPCRepair, ["repair"])
  ];
}
