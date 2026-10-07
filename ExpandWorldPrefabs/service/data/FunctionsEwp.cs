namespace Data;

// EWP only: API handlers and stored values.
public partial class Functions
{
  partial void ResolveApiFunction(string key, ref string? result) => result = ExpandWorld.Prefab.Api.ResolveFunction(key);
  partial void ResolveApiValueFunction(string key, string value, ref string? result) => result = ExpandWorld.Prefab.Api.ResolveValueFunction(key, value);
  partial void GetStoredValue(string key, string defaultValue, ref string? result) => result = Service.DataStorage.GetValue(key, defaultValue);
  partial void IncrementStoredValue(string key, long amount, ref string? result) => result = Service.DataStorage.IncrementValue(key, amount);
  partial void SetStoredValue(string key, string value) => Service.DataStorage.SetValue(key, value);
}
