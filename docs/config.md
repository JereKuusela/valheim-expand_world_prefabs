# Config

Scripts can declare their own settings in the main config file (`expand_world_prefabs.cfg`) with a `config` entry in `expand_prefabs*.yaml`:

```yaml
- config: Bosses
  key: bossHealth
  name: Boss health
  type: float
  default: 1
  min: 0.1
  max: 10
  description: Boss health multiplier.
```

- config (default: `Custom`): Section in the config file. Must be the first field. Can't contain `_`, `=`, `<` or `>`.
- key: Id used in the functions. Defaults to name. Can't be empty or contain `_`, `=`, `<` or `>`. Must be unique within a section.
- name: Name in the config file. Defaults to key.
- type (default: `string`): `bool`, `int`, `float` or `string`.
- default, description: Default value and description. Description supports translation tokens like `$enemy_troll`, using the language of the game (server language on servers).
- min, max: Allowed range for `int` and `float`.
- values: Comma separated allowed values for `string`.
- prefab: Creates a setting for each matching prefab. Supports wildcards, multiple values and value groups like in [scripting](scripting.md).
- condition: Prefabs are skipped if this is false. Evaluated against the prefab and its default values, see [functions](functions.md).

All fields support functions. With `prefab`, functions are resolved separately for each prefab, so `<prefab>` and the default values of the prefab can be used:

```yaml
- config: Health
  key: <safeprefab>
  name: <prefab> health
  prefab: creature
  condition: <float_Humanoid.m_health> > 500
  type: float
  default: <float_Humanoid.m_health>
```

This creates `<config_Health_Troll>` and similar for each matching creature. Use `<safeprefab>` for keys because prefab names often contain `_`.

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
