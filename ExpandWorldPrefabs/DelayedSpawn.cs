using System.Collections.Generic;
using Data;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class DelayedSpawn(float delay, ZdoEntry zdoEntry, bool triggerRules)
{
  private static readonly List<DelayedSpawn> Spawns = [];

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
    var repeat = spawn.Repeat?.Get(f) ?? 0;
    var repeatInterval = spawn.RepeatInterval?.Get(f) ?? delay;
    var repeatChance = spawn.RepeatChance?.Get(f) ?? 1f;
    var delays = Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
    if (delays != null)
    {
      foreach (var d in delays)
        Add(spawn, originalZdo, data, f, d);
    }
    else
      Add(spawn, originalZdo, data, f, delay);
  }
  private static void Add(Spawn spawn, ZDO originalZdo, DataEntry? data, Functions f, float delay)
  {
    var pos = originalZdo.m_position;
    var rotQuat = originalZdo.GetRotation();
    pos += rotQuat * (spawn.Pos?.Get(f) ?? Vector3.zero);
    rotQuat *= spawn.Rot?.Get(f) ?? Quaternion.identity;
    var rot = rotQuat.eulerAngles;
    if (spawn.Snap?.GetBool(f) == true)
      pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
    data = DataHelper.Merge(data, DataHelper.Get(spawn.Data, f));
    var prefab = spawn.GetPrefab(f);
    if (prefab == 0) return;
    ZdoEntry zdoEntry = new(prefab, pos, rot, originalZdo);
    if (data != null)
      zdoEntry.Load(data, f);
    var owner = spawn.Owner?.Get(f);
    if (owner.HasValue)
      zdoEntry.Owner = owner.Value;
    var attach = spawn.Attach?.Get(f);
    if (attach.HasValue && attach.Value != ZDOID.None)
      SupportAttach.Attach(zdoEntry, attach.Value);
    var connect = spawn.Connect?.Get(f);
    if (connect.HasValue && connect.Value != ZDOID.None)
      SupportAttach.Connect(zdoEntry, connect.Value);
    Add(delay, zdoEntry, spawn.TriggerRules?.GetBool(f) ?? false);
  }
  private static void Add(float delay, ZdoEntry zdoEntry, bool triggerRules)
  {
    if (delay <= 0f)
    {
      CreateObject(zdoEntry, triggerRules);
      return;
    }
    Spawns.Add(new(delay, zdoEntry, triggerRules));
  }
  public static void Execute(float dt)
  {
    // Two loops to preserve order.
    for (var i = 0; i < Spawns.Count; i++)
    {
      var spawn = Spawns[i];
      spawn.Delay -= dt;
      if (spawn.Delay > -0.001) continue;
      spawn.Execute();
      Spawns.RemoveAt(i);
      i--;
    }
  }
  public float Delay = delay;
  private readonly ZdoEntry ZdoEntry = zdoEntry;
  private readonly bool TriggerRules = triggerRules;

  public void Execute()
  {
    CreateObject(ZdoEntry, TriggerRules);
  }
}