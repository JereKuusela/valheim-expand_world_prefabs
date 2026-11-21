using System.Collections.Generic;
using Data;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class DelayedSpawn(double due, ZdoEntry zdoEntry, bool triggerRules, float? removeDelay)
{
  private static readonly List<DelayedSpawn> Spawns = [];

  public static void Clear() => Spawns.Clear();

  public static ZDO? CreateObject(ZdoEntry entry, bool triggerRules)
  {
    var previousSkip = HandleCreated.Skip;
    HandleCreated.Skip = !triggerRules;
    try
    {
      return entry.Create();
    }
    finally
    {
      HandleCreated.Skip = previousSkip;
    }
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
    var prefab = spawn.GetPrefab(f);
    if (prefab == 0) return;
    if (TryGetBlueprint(prefab, out var blueprint))
    {
      AddBlueprint(blueprint, spawn, originalZdo, f, pos, rotQuat, delay, removeDelay);
      return;
    }
    data = DataHelper.Merge(data, DataHelper.Get(spawn.Data, f));
    ZdoEntry zdoEntry = new(prefab, pos, rot, originalZdo.GetOwner());
    if (data != null)
    {
      zdoEntry.Load(data, f, originalZdo);
      zdoEntry.LoadItem(data, f);
      zdoEntry.LoadInventory(data, f);
    }
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
  // Real prefabs take priority over blueprints with the same name.
  private static bool TryGetBlueprint(int prefab, out Blueprint blueprint)
  {
    blueprint = null!;
    if (ZNetScene.instance.m_namedPrefabs.ContainsKey(prefab)) return false;
    return BlueprintCache.TryGet(prefab, out blueprint);
  }

  private static Blueprint? GetBlueprint(string name) => BlueprintCache.TryGet(name, out var blueprint) ? blueprint : null;

  private static void AddBlueprint(Blueprint blueprint, Spawn spawn, ZDO originalZdo, Functions f, Vector3 pos, Quaternion rot, float delay, float? removeDelay)
  {
    var triggerRules = spawn.TriggerRules?.GetBool(f) ?? false;
    var owner = spawn.Owner?.Get(f);
    var plan = BlueprintPlan.Create(blueprint, pos, rot, Vector3.one, (data, _) => data, name => name, GetBlueprint);
    var (height, paint) = plan.GetTerrain(pos, rot);
    TerrainSnapshot.Add(delay, blueprint.Name, height, paint, pos, rot);
    var extraDelays = GetBuildDelays(plan, spawn, f, pos, rot);
    for (var i = 0; i < plan.Objects.Count; i++)
    {
      var obj = plan.Objects[i];
      var objDelay = delay + extraDelays[i];
      ZdoEntry zdoEntry = new(obj.Prefab.GetStableHashCode(), obj.Pos, obj.Rot.eulerAngles, originalZdo.GetOwner());
      if (obj.Data != null)
      {
        zdoEntry.Load(obj.Data, f, originalZdo);
        zdoEntry.LoadItem(obj.Data, f);
        zdoEntry.LoadInventory(obj.Data, f);
      }
      // Copied blueprint data often has a creator, which makes enemies treat the pieces as player built.
      zdoEntry.Longs?.Remove(ZDOVars.s_creator);
      if (obj.Scale != Vector3.one)
      {
        zdoEntry.Vecs ??= [];
        zdoEntry.Vecs[ZDOVars.s_scaleHash] = obj.Scale;
      }
      if (owner.HasValue)
        zdoEntry.Owner = owner.Value;
      if (spawn.OwnerServer)
        ServerOwned.Mark(zdoEntry);
      Add(objDelay, zdoEntry, triggerRules, removeDelay);
    }
  }

  // Extra per-object delay, normalized so the earliest object has no extra delay.
  private static float[] GetBuildDelays(BlueprintPlan plan, Spawn spawn, Functions f, Vector3 pos, Quaternion rot)
  {
    var count = plan.Objects.Count;
    var result = new float[count];
    var vertical = spawn.BlueprintVerticalDelay?.Get(f) ?? 0f;
    var horizontal = spawn.BlueprintHorizontalDelay?.Get(f) ?? 0f;
    var sector = spawn.BlueprintSectorDelay?.Get(f) ?? 0f;
    if (count == 0 || (vertical == 0f && horizontal == 0f && sector == 0f)) return result;
    var step = spawn.BlueprintDelayStep?.Get(f) ?? 0f;
    var inverse = Quaternion.Inverse(rot);
    var locals = new Vector3[count];
    float minY = float.MaxValue, maxY = float.MinValue, maxDistance = 0f;
    for (var i = 0; i < count; i++)
    {
      var local = inverse * (plan.Objects[i].Pos - pos);
      locals[i] = local;
      minY = Mathf.Min(minY, local.y);
      maxY = Mathf.Max(maxY, local.y);
      maxDistance = Mathf.Max(maxDistance, new Vector2(local.x, local.z).magnitude);
    }
    var heightRange = maxY - minY;
    var min = float.MaxValue;
    for (var i = 0; i < count; i++)
    {
      var local = locals[i];
      var angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
      if (angle < 0f) angle += 360f;
      var heightT = heightRange > 0f ? (local.y - minY) / heightRange : 0f;
      var distanceT = maxDistance > 0f ? new Vector2(local.x, local.z).magnitude / maxDistance : 0f;
      var value = vertical * heightT + horizontal * distanceT + sector * angle / 360f;
      result[i] = value;
      if (value < min) min = value;
    }
    for (var i = 0; i < count; i++)
    {
      var value = result[i] - min;
      result[i] = step > 0f ? Mathf.Round(value / step) * step : value;
    }
    return result;
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
      Spawns.RemoveAt(i);
      i--;
      spawn.ExecuteAction();
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
