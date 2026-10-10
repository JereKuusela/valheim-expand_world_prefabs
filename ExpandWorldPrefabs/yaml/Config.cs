using System.ComponentModel;

namespace ExpandWorld.Prefab;

// Declares BepInEx config entries that scripts can read and write.
// All text fields support functions, resolved separately for each matching prefab.
public class ConfigYaml
{
  // Section in the config file. Must be the first field.
  [DefaultValue("Custom")]
  public string config = "Custom";
  // Name in the config file. Defaults to key.
  [DefaultValue("")]
  public string name = "";
  // Id used by scripts. Defaults to name.
  [DefaultValue("")]
  public string key = "";
  // Creates an entry for each matching prefab. Same format as the rule prefab.
  [DefaultValue("")]
  public string prefab = "";
  // Prefabs to skip from the prefab list.
  [DefaultValue("")]
  public string excludePrefab = "";
  // Evaluated for each prefab, entry is skipped if false.
  [DefaultValue("")]
  public string condition = "";
  [DefaultValue("string")]
  public string type = "string";
  [DefaultValue("")]
  public string @default = "";
  [DefaultValue("")]
  public string description = "";
  [DefaultValue(null)]
  public string? min;
  [DefaultValue(null)]
  public string? max;
  // Comma separated allowed values for string type.
  [DefaultValue(null)]
  public string? values;
}
