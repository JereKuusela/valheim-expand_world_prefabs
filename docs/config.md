# Config

Scripts can declare their own settings in the main config file (`expand_world_prefabs.cfg`) with a `config` entry in `expand_prefabs*.yaml`:

```yaml
- config: bossHealth
  section: Bosses
  type: float
  default: 1
  min: 0.1
  max: 10
  description: Boss health multiplier.
```

- config: Name used in the functions. Can't be empty or contain `_`, `=`, `<` or `>`.
- section (default: `Custom`) and key (default: name): Location in the config file.
  - Section is also part of the function id, so it can't contain `_`, `=`, `<` or `>`. Names only need to be unique within a section.
- type (default: `string`): `bool`, `int`, `float` or `string`.
- default, description: Default value and description.
- min, max: Allowed range for `int` and `float`.
- values: Comma separated allowed values for `string`.

Settings are added, changed and removed when the yaml files reload. Removed settings are also removed from the config file. Existing settings of EWP can't be overridden.

## Functions

- `<config_SECTION_NAME>`: Value of the declared setting. For the example above `<config_Bosses_bossHealth>`.
- `<saveconfig_SECTION_NAME_Y>`: Sets the declared setting to Y. Returns the new value.
  - The file is saved in batches, so frequent changes don't cause lots of disk writes.
- `<modconfig_GUID_SECTION_KEY>`: Value of a setting from another mod.
- `<savemodconfig_GUID_SECTION_KEY_Y>`: Sets a setting of another mod to Y.
  - Requires the EWP setting `Allow modifying mod configs`.
  - Underscores in the names are resolved by checking which setting exists.
  - Many mods only read their settings on startup, so changes might not have an effect.

## Trigger

See `type: config` in [scripting](scripting.md) to run rules when a setting changes.
