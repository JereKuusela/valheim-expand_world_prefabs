namespace Data;

// EWP only: API handlers and stored values.
public partial class Functions
{
  partial void ResolveApiFunction(string key, ref string? result) => result = ExpandWorld.Prefab.Api.ResolveFunction(key);
  partial void ResolveApiValueFunction(string key, string value, ref string? result) => result = key switch
  {
    "config" => ExpandWorld.Prefab.ConfigManager.Get(value) ?? "",
    "saveconfig" => SaveConfig(value),
    "modconfig" => ExpandWorld.Prefab.ConfigManager.GetMod(value) ?? "",
    "savemodconfig" => ExpandWorld.Prefab.ConfigManager.SetMod(value) ?? "",
    _ => ExpandWorld.Prefab.Api.ResolveValueFunction(key, value),
  };
  // Input is section_name_value.
  private static string SaveConfig(string value)
  {
    var first = value.IndexOf(Separator);
    var second = first < 0 ? -1 : value.IndexOf(Separator, first + 1);
    if (second < 0) return "";
    return ExpandWorld.Prefab.ConfigManager.Set(value.Substring(0, second), value.Substring(second + 1)) ?? "";
  }
  partial void GetStoredValue(string key, string defaultValue, ref string? result) => result = Service.DataStorage.GetValue(key, defaultValue);
  partial void IncrementStoredValue(string key, long amount, ref string? result) => result = Service.DataStorage.IncrementValue(key, amount);
  partial void SetStoredValue(string key, string value) => Service.DataStorage.SetValue(key, value);
}
