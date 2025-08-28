- v1.45
  - Adds new field `excludePrefab` to exclude prefabs from the field `prefab`.
  - Adds default value groups for structure material types and item drop types.
  - Adds new field `pars` to pokes for structured parameter handling.
  - Adds support for a wildcard `*` in the middle of the text (for example sfx_*_destroyed).
  - Adds `drops` support to plants.
  - Adds new field `chance` to allow randomly executing the selected action.
  - Adds new field `separate` to allow actions to ignore the weight system.
  - Adds new field `repeat` to RPC calls to allow repeating after a delay.

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

- v1.41
  - Changes the field `poke` and `objects` to use `filter` instead of `data` (old way still works).
  - Fixes multiple values not working for the field `data`.
  - Fixes the field `exec` not working with global triggers (like time).
  - Fixes GameObject and ItemDrop types not working for field parameters.

- v1.40
  - Adds support of vector and quaternion values to the type `change`.
  - Adds a new field `admin` to only trigger for admins or non-admins.
  - Adds wildcard support to the parameter `item` to match multiple items.
  - Adds shorthand format for data entries (automatically enables the field `injectData`).
  - Adds new parameter `<addlong>`, `<calclong>`, `<divlong>`, `<modlong>`, `<mullong>` and `<sublong>` for large integer values.
  - Adds new parameter `<amount>`, `<durability>` and `<quality>` to get item properties from a chest.
  - Changes the type `change` to return `<none>` instead of `none` for empty values (to match how data filtering works).
  - Changes the type `state` to return `<none>` instead of `none` for empty values (to match how data filtering works).
  - Fixes error when trying to spawn invalid prefab.
  - Fixes parameters with upper case letters not working for filters `keys`, `bannedKeys`, `globalKeys` and `bannedGlobalKeys`.
