- v1.63
  - Adds support for inline data definitions in field `data`.
  - Adds support for loading decoded `item` field from data.
  - Adds field `excludePrefab` to pokes. Thanks Zeall!
  - Adds trigger type `config` to react to EWP config changes.
  - Adds support adding custom config entries.
  - Adds functions `<config_SECTION_NAME>`, `<saveconfig_SECTION_NAME_Y>`, `<modconfig_GUID_SECTION_KEY>` and `<savemodconfig_GUID_SECTION_KEY_Y>` for interacting with EWP config entries.
  - Adds support for multiple log files and rolling logs to field `log`. Thanks JPValheim!
  - Adds support for multiple execs to field `exec`. Thanks Zeall!
  - Changes value group `material_*` to be based on resource cost rather than support system type. Thanks Zeall!
  - Changes field `exec` to also support running console commands. Thanks Zeall!
  - Fixes value group `itemtype_*` containing other objects like enemy attacks. Thanks Zeall!
  - Fixes unnecessary warnings about missing RPC methods (vanilla issue). Thanks Zeall!

- v1.62
  - Adds automatic backing up for ew_data.yaml file (was accidentally removed).
  - Adds better warning for invalid RPC method lookups Thanks Zeall!

- v1.61
  - Adds new function `objectcount` to return the number of objects matching a given filter. Thanks Zeall!
  - Fixes various issues related to inventory handling.
  - Fixes portals not being detected by object filters or pokes. Thanks Zeall!
  - Fixes terrain operations not working. Thanks JPValheim!
  - Renames undocumented poke `amount` to `pokecount` for consistency with other count fields. Thanks Zeall!

- v1.60
  - Adds experimental server owned object support.
  - Fixes for the new game update (item data, terrain and many other things). Thanks JPValheim!
  - Adds support for saving delayed pokes to object data (requires server side data to be enabled). Thanks JPValheim!
  - Adds new field `log` to add custom log output. Thanks JPValheim!
  - Adds new function `altbiome` to get alternative biome info. Thanks JPValheim!
  - Changes field `biomes` and `bannedBiomes` to support alternative biomes. Thanks JPValheim!
  - Fixes shorthand parsing of `filter` and `bannedFilter` failing in some cases. Thanks JPValheim!
  - Fixes missing clean up on logout (possibly leaving stale data behind). Thanks JPValheim!
  - Fixes poke `weight` being parsed as integer instead of decimal value. Thanks JPValheim!

- v1.59
  - Adds server side position update for attached objects when a script triggers on them.
  - Adds experimental NPC chat support.
