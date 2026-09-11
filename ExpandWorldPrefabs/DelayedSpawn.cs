using System.Collections.Generic;
using Data;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class DelayedSpawn(double due, ZdoEntry zdoEntry, bool triggerRules, float? removeDelay)
{
  private static readonly List<DelayedSpawn> Spawns = [];

  public static void Clear() => Spawns.Clear();

  public static ZDO? CreateObject(ZdoEntry entry, bool triggerRules)
  {
    HandleCreated.Skip = !triggerRules;
    var zdo = entry.Create();
    HandleCreated.Skip = false;
    return zdo;
  }
  public static void Add(Spawn spawn, ZDO originalZdo, DataEntry? data, Functions f)
  {
    if (spawn.Condition != null && !spawn.Condition.Evaluate(f))
      return;

    var chance = spawn.Chance?.Get(f) ?? 1f;
    if (chance < 1f && Random.value > chance)
      return;

    var delay = spawn.Delay?.Get(f) ?? 0f;
    var removeDelay = spawn.RemoveDelay?.Get(f);
    var repeat = spawn.Repeat?.Get(f) ?? 0;
    var repeatInterval = spawn.RepeatInterval?.Get(f) ?? delay;
    var repeatChance = spawn.RepeatChance?.Get(f) ?? 1f;
    var delays = Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
    if (delays != null)
    {
      foreach (var d in delays)
        Add(spawn, originalZdo, data, f, d, removeDelay);
    }
    else
      Add(spawn, originalZdo, data, f, delay, removeDelay);
  }
  private static void Add(Spawn spawn, ZDO originalZdo, DataEntry? data, Functions f, float delay, float? removeDelay)
  {
    var pos = originalZdo.m_position;
    var rotQuat = originalZdo.GetRotation();
    var offset = rotQuat * (spawn.Pos?.Get(f) ?? Vector3.zero);
    pos += offset;
    rotQuat *= spawn.Rot?.Get(f) ?? Quaternion.identity;
    var rot = rotQuat.eulerAngles;
    if (spawn.Snap?.GetBool(f) == true)
      pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z) + offset.y;
    data = DataHelper.Merge(data, DataHelper.Get(spawn.Data, f));
    var prefab = spawn.GetPrefab(f);
    if (prefab == 0) return;
    ZdoEntry zdoEntry = new(prefab, pos, rot, originalZdo);
    if (data != null)
      zdoEntry.Load(data, f);
    var owner = spawn.Owner?.Get(f);
    if (owner.HasValue)
      zdoEntry.Owner = owner.Value;
    if (spawn.OwnerServer)
      ServerOwned.Mark(zdoEntry);
    var attach = spawn.Attach?.Get(f);
    if (attach.HasValue && attach.Value != ZDOID.None)
      SupportAttach.Attach(zdoEntry, attach.Value);
    var connect = spawn.Connect?.Get(f);
    if (connect.HasValue && connect.Value != ZDOID.None)
      SupportAttach.Connect(zdoEntry, connect.Value);
    Add(delay, zdoEntry, spawn.TriggerRules?.GetBool(f) ?? false, removeDelay);
  }
  private static void Add(float delay, ZdoEntry zdoEntry, bool triggerRules, float? removeDelay)
  {
    if (delay <= 0f)
    {
      var zdo = CreateObject(zdoEntry, triggerRules);
      if (zdo != null && removeDelay.HasValue)
        DelayedRemove.Add(removeDelay.Value, zdo.m_uid, triggerRules);
      return;
    }
    Spawns.Add(new(ZNet.instance.m_netTime + delay, zdoEntry, triggerRules, removeDelay));
  }
  public static void Execute()
  {
    for (var i = 0; i < Spawns.Count; i++)
    {
      var spawn = Spawns[i];
      if (spawn.Due > ZNet.instance.m_netTime) continue;
      spawn.ExecuteAction();
      Spawns.RemoveAt(i);
      i--;
    }
  }
  private readonly double Due = due;
  private readonly ZdoEntry ZdoEntry = zdoEntry;
  private readonly bool TriggerRules = triggerRules;
  private readonly float? RemoveDelay = removeDelay;

  private void ExecuteAction()
  {
    var zdo = CreateObject(ZdoEntry, TriggerRules);
    if (zdo != null && RemoveDelay.HasValue)
      DelayedRemove.Add(RemoveDelay.Value, zdo.m_uid, TriggerRules);
  }
}