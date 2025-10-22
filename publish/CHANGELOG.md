- v1.47
  - Adds automatic backup for EWP data files (once per day).
  - Adds automatic backup for data files (once per day).
  - Adds some new function parameters.
  - Adds new field `overwrite` to RPC calls to allow canceling existing delayed calls.
  - Improves support for byte array data type.

- v1.46
  - Fixes delayed objects possibly targeting wrong objects if the targeted object was already removed.
  - Removes `type: damage` support
  - Removes `type: repair` support.
  - Removes deprecated `type: state` triggers.

- v1.45
  - Breaking change: Scripts are now checked separately by default!
  - This means multiple scripts can run from a single trigger.
  - Weight system is only used when the field `weight` is explicitly set.
  - Adds new field `excludePrefab` to exclude prefabs from the field `prefab`.
  - Adds default value groups for structure material types and item drop types.
  - Adds new field `pars` to pokes for structured parameter handling.
  - Adds support for a wildcard `*` in the middle of the text (for example sfx_*_destroyed).
  - Adds `drops` support to plants.
  - Adds new field `chance` to allow randomly skipping the selected action.
  - Adds new field `chance` to spawns, pokes and RPC calls to allow randomly skipping them.
  - Adds new field `repeat` to spawns, pokes and RPC calls to allow repeating after a delay.
  - Adds new field `weight` to spawns, pokes and RPC calls to allow triggering only one of the entries.
  - Adds new states `death`, `effects`, `fragments`, `freezeframe`, `grow`, `hit`, `leguse`, `loaded`, `material`, `resetcloth`, `shoot`, `step` and `unsummon`.

- v1.44
  - Adds offset support to pokes.

- v1.43
  - Adds a field `bannedLocationDistance` so that different distance can be used for required and banned locations.
  - Adds automatic container size detection to simplify adding items.
  - Adds back legacy base64 encoded data string support.
  - Adds data entry support for the field `drops` to allow dropping custom items.
  - Changes the parameter `<hash_>` to also work for location ids.
  - Changes poking action to not include the object itself by default (can be changed with the poke field `self`).
  - Fixes quoted strings being split up when used as "type, key, value" in the field `data`.
  - Fixes field value queries not checking child objects (for example Boat chests).

- v1.42
  - Adds a new parameter `<biome>` to get the biome of the object.
  - Adds a new field `filterLimit` to set how many filters must match.
  - Fixes comment or extra space at end of the fields `spawn` or `swap` breaking the file parsing.
  - Reworks the filtering system. Now multiple filters are checked separately, instead of being combined into a single check.
