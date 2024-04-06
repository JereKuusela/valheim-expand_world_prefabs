- v1.10
  - Adds a new field `pokeDelay` to delay pokes.
  - Adds a new field `rpcDelay` to delay RPC calls.
  - Adds a new field `customRpc` to trigger any RPC call.
  - Changes the field `delay` to `spawnDelay` to make it more explicit.
  - Breaking change: Poke can now also poke the poker.

- v1.9
  - Adds the new data system from World Edit Commands mod.
  - Adds a new field `triggerRules` to cause spawns from rules to trigger other rules.
  - Adds a new field `removeDelay` to delay object removing.
  - Adds value group support to the field `prefab`(from the new data system).
  - Adds parameter support to the fields `globalKeys` and `bannedGlobalKeys`.
  - Changes the fields `minDistance` and `maxDistance` to not scale with the world radius.
  - Fixes data filters not automatically updating when modifying the data entries.
  - Removes dependency of Expand World Data mod.

- v1.8
  - Adds a new field `drops` to spawn drops.
  - Adds the keyword "creature" to the field `objects` and `bannedObjects`.
  - Adds the keyword "all" to the field `prefab`.
  - Adds a new value "poke" to the field `type`.
  - Adds new fields `pokes`, `pokeLimit` and `pokeParameter`.

- v1.7
  - Adds support for checking data of the player who triggered armor stands, cooking stations, item stands or obliterators.
  - Adds a new field `delay` to delay spawns and swaps.
  - Fixes armor stand state not working.

- v1.6
  - Adds support for checking data of the player who caused the `repair` trigger.
  - Adds a new field `types` to set multiples types at once.
  - Changes the keyword format from `{}` to `<>`.
  - Fixes the mod not loading the yaml file automatically (changing the file was required).

- v1.5
  - Adds values `state`, `command`, `say`, `damage` and `repair` to the field `type`. This field is now mandatory.
  - Adds `{prefab}`, `{par}`, `{par0}`, `{par1}`, `{par2}`, `{par3}` and `{par4}` keywords to fields `command`, `spawn`, `swap` and `objects`.
  - Adds wildcard * support to the field `prefab` and `type`.
  - Adds keyword `creature` to the field `prefab` to affect all creatures.
  - Adds support for only setting field `eventDistance` to work with any nearby event.
  - Adds type `hash` to the field `filter` and `bannedFilter`.
  - Adds new field `minY` and `maxY` to filter by exact y-coordinate.
  - Adds new fields `bannedObjects` and `bannedObjectsLimit` to filter spawned objects.
  - Changes the field `data` only apply to the original object.
  - Changes the format of existing keywords from `$$` to `{}`.
  - Improves the performance of fields `objects` and `bannedObjects`.
  - Improves overall performance (only code that is needed is now patched).
  - Fixes the field `events` not working.
  - Fixes the altitude check not using object altitude (used terrain altitude).

- v1.4
  - Adds a new field `objectsLimit` to set how the field `objects` is used.
  - Improves single player support.
  - Reworks the field `objects` to allow putting data filter.
  - Removes the field `objectDistance` as obsolete.
  - Fixes lag if vegetation was changed in a server.
  - Fixes swapping incorrectly taking some properties from the original object, instead of the new object.

- v1.3
  - Adds a new field `type` to select if the entry affects spawned or destroyed objects.
  - Adds a new field `spawn` to spawn a new object without removing the original object.
  - Adds a new field `remove` to remove the original object.
  - Adds new fields `bannedFilter` and `filter` to filter destroyed objects.
  - Adds new fields  `bannedFilters`, `commands`, `filters`, `spawns` and `swaps` to allow multiple values.
  - Adds support of coordinates and data to `spawn` and `swap`.
  - Changes the default max distance to 1000x of the world radius.

- v1.2
  - Improves object swapping.

- v1.1
  - Adds support for multiple ids in the prefab field.
  - Adds fields events and eventDistance.
  - Adds fields objects and objectDistance.
  - Adds fields locations and locationDistance.
  - Adds fields day and night.
  - Changes weight calculation to allow nothing if total weight is less than 1.
  - Fixes command not working without data or swap.

- v1.0
  - Initial release.
