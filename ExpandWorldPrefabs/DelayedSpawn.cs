using System.Collections.Generic;
using Data;
using Service;
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
  public static void Add(Spawn spawn, ZDO originalZdo, DataEntry? data, Parameters pars)
  {
    var chance = spawn.Chance?.Get(pars) ?? 1f;
    if (chance < 1f && Random.value > chance)
      return;

    var delay = spawn.Delay?.Get(pars) ?? 0f;
    var repeat = spawn.Repeat?.Get(pars) ?? 0;
    var repeatInterval = spawn.RepeatInterval?.Get(pars) ?? delay;
    var repeatChance = spawn.RepeatChance?.Get(pars) ?? 1f;
    var delays = Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
    if (delays != null)
    {
      foreach (var d in delays)
        Add(spawn, originalZdo, data, pars, d);
    }
    else
      Add(spawn, originalZdo, data, pars, delay);
  }
  private static void Add(Spawn spawn, ZDO originalZdo, DataEntry? data, Parameters pars, float delay)
  {
    var pos = originalZdo.m_position;
    var rotQuat = originalZdo.GetRotation();
    pos += rotQuat * (spawn.Pos?.Get(pars) ?? Vector3.zero);
    rotQuat *= spawn.Rot?.Get(pars) ?? Quaternion.identity;
    var rot = rotQuat.eulerAngles;
    if (spawn.Snap?.GetBool(pars) == true)
      pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
    data = DataHelper.Merge(data, DataHelper.Get(spawn.Data, pars));
    var prefab = spawn.GetPrefab(pars);
    if (prefab == 0) return;
    ZdoEntry zdoEntry = new(prefab, pos, rot, originalZdo);
    if (data != null)
      zdoEntry.Load(data, pars);
    Add(delay, zdoEntry, spawn.TriggerRules?.GetBool(pars) ?? false);
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
    }
    for (var i = Spawns.Count - 1; i >= 0; i--)
    {
      if (Spawns[i].Delay > -0.001) continue;
      Spawns.RemoveAt(i);
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