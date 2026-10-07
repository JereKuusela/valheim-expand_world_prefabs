using UnityEngine;

namespace Data;

// EWP specific hooks for the shared Functions.
public class PrefabFunctions(string prefab, string[] args, Vector3 pos) : Functions(prefab, args, pos)
{
  protected override string? ResolveApiFunction(string key) => ExpandWorld.Prefab.Api.ResolveFunction(key);
  protected override string? ResolveApiValueFunction(string key, string value) => ExpandWorld.Prefab.Api.ResolveValueFunction(key, value);
  protected override string GetStoredValue(string key, string defaultValue) => Service.DataStorage.GetValue(key, defaultValue);
  protected override string IncrementStoredValue(string key, long amount) => Service.DataStorage.IncrementValue(key, amount);
  protected override void SetStoredValue(string key, string value) => Service.DataStorage.SetValue(key, value);
}
