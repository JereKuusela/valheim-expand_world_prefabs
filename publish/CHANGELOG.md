- v1.23
  - Adds support for new state types (Feast eat and ItemDrop piece).
  - Adds new parameter `<pos_x,z,y>` to get the offset position from the object position.
  - Fixed for the new patch.
  - Fixes iten drops being spawned when stack size was explicitly set to 0.
  - Updates the RPC.md file.

- v1.22
  - Adds a new field `owner` to set the object owner when using `injectData` field.
  - Changes the zdo field default data value to work recursively.
  - Fixes the default item variant being 1 which caused container data loading to fail.

- v1.21
  - Adds a new field `terrain` to more easily support the ApplyOperation RPC call.
  - Adds a new parameter `self` to poke actions.
  - Fixes arithmetic operations sometimes working incorrectly.
  - Fixes arithmetic operations not working on some parameters.
  - Fixes some rare edge cases with some parameters.
  - Fixes data with an unknown numeric key duplicating on the object.

- v1.20
  - Fixes wrong default value for the field `triggerRules`.
  - Fixes parameters with empty value not working.

- v1.19
  - Adds the file name to the error message when failing to load a file.
  - Adds a new way of filtering with `objects` and `bannedObjects` fields.
  - Changes the default value of zdo field parameters to be acquired from the object.
  - Fixes multiple prefabs not working on the field `prefab`.
  - Fixes the field `bannedObjects` not working.

- v1.18
  - Adds a new way of spawning with `spawn` and `swap` fields.
  - Adds new fields `addItems` and `removeItems` to more easily add or remove items from containers.
  - Adds new fields `maxHeight` and `minHeight` to pokes and object filters.
  - Adds support for filtering with the data type items.
  - Adds new parameters "par5", "par6", "par7", "par8" and "par9".
  - Adds new parameters "item_*" to get the amount of a specific item in containers.
  - Adds a new parameter "snap" to get world height at current position.
  - Adds parameter support to fields `data`, `day`, `maxAltitude`, `maxDistance`, `minAltitude`, `minDistance`, `minY`, `night`, `poke`, `remove`, `removeDelay`, `weight`.
  - Adds a new field `cancel` to cancel some triggering actions.
  - Fixes global key filters not being converted to lower case.
  - Fixes nested parameters not working.
  - Fixes zdo based parameters not working when spawning objects.
  - Fixes the type long missing from filter types.
  - Fixes container items filtering not working with any container size.
  - Fixes the fields `maxAltitude` and `minAltitude` being checked incorrectly (60 meters off).
  - Removes the default value of 10000 meters from `maxAltitude`.
  - Removes door usage from the type `state` (no longer sent to the server).

- v1.17
  - Adds new filters `paint`, `minPaint` and `maxPaint`.
  - Adds a new field `injectData` to change data without replacing the entire object.
  - Changes parameters "pid", "pchar" and "pname" to work as normal parameters.
  - Fixes creature spawn points losing track of the spawned creature when data swapping.
  - Removes the field `playerSearch` as obsolete. Use player pokes instead.
  - Removes the RPC target "search" as obsolete. Use player pokes instead.

- v1.16
  - Adds a new parameter "day" to get the current day of the world.
  - Adds a new parameter "ticks" to get ticks since the world start.
  - Adds a new parameter "pos" to get the object position as x,z,y.
  - Adds a new parameter "rot" to get the object rotation as y,x,z.
  - Adds new data fields `position` and `rotation`.
  - Adds a new field `packaged` to RPC calls to send parameters as a single package (some RPCs require this).
  - Adds support for the data field `connection` to use ZDO id instead of connection hash.
  - Changes the parameter "time" to be seconds instead of ticks.
  - Fixes value groups not working for data filters.
  - Fixes nested value groups not working.
  - Fixes parameters not working for data filters.
  - Updates the RPC.md file.

- v1.15
  - Adds a new type "globalkey" to trigger on global key changes.
  - Adds a new type "event" to trigger on event start or end.
  - Changes missing global keys to be evaluated as value 0.
