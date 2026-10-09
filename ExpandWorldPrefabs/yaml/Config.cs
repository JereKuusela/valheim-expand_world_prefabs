using System.ComponentModel;

namespace ExpandWorld.Prefab;

// Declares a BepInEx config entry that scripts can read and write.
public class ConfigYaml
{
  [DefaultValue("")]
  public string config = "";
  [DefaultValue("Custom")]
  public string section = "Custom";
  [DefaultValue("")]
  public string key = "";
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
