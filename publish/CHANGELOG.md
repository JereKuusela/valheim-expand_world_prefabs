- v1.59
  - Adds server side position update for attached objects when a script triggers on them.
  - Adds experimental NPC chat support.

- v1.58
  - Adds new function `random` to get a random number between two values.
  - Adds new field `random` to pokes to allow randomizing affected objects.
  - Adds support for specifying unit (deg or rad) for angle parameters.
  - Adds support for "distance, angle, y" format for vectors (requires using deg or rad for angle).
  - Adds dynamic value support to `objectsLimit` and `bannedObjectsLimit`.
  - Fixes `pos` y coordinate offset not being applied when `snap` is true.

- v1.57
  - Adds field `self` to object filters.
  - Adds field `removeDelay` to spawns to allow automatic removal of spawned objects.
  - Adds new setting to allow processing custom prefab names even when server doesn't recognize them.
  - Adds new function `globalkey` to get global key values.
  - Adds support for putting data entries to script yaml files.
  - Fixes server player broken by last update.
  - Fixes `bannedGlobalKeys` not lower casing function replacements automatically.
  - Fixes say commands happening twice when players are being created.
  - Fixes function `key` returning global key instead of custom data value.
  - Fixes `Object attaching` not working for temporary objects like status effects.
  - Optimizes file reloading to only reload the changed file.

- v1.56
  - Fixes `Object attaching` affecting some normal effects like magic shield bubble.
  - Removes dependency from Steamworks so should now work on non-Steam hosts.

- v1.55
  - Adds support for server side only data. Keys starting with `ewp_` are stored as server-only instead of normal ZDO fields.
  - Adds range support for `keys` and `bannedKeys`.
  - Adds file watcher to the config file to reload when manually edited.
  - Changes `Persist spawned players` setting to be enabled by default (issue was probably with the attach system, not this).
  - Fixes subfolders not working for data files.
  - Fixes persisted NPC players not being removable by EWP scripts.
  - Fixes scale hack not working on single player.
  - Fíxes automatic `injectData` not being false when position or rotation is set.
  - Fixes `Object attaching` affecting normal players when they are on beds, ships, etc (left a ghost player when disconnected).
  - Optimizes script file reloading to only patch/unpatch game code when needed, instead of always patching/unpatching on every reload.
