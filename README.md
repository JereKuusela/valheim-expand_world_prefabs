# Expand World Prefabs

Allows creating rules to react to objects being spawned, destroyed and more.

Install on the server (modding [guide](https://youtu.be/L9ljm2eKLrk)).

## Features

- Modify or swap spawned creatures.
- Modify or swap built structures.
- Modify or swap other objects.
- Swap destroyed creatures, structures and objects.
- And a lot more...

When swapping creature spawns, the spawn limit still checks the amount of original creature. This can lead to very high amount of creatures.

## Configuration

The file `expand_world/expand_prefabs.yaml` is created when loading a world.

This mod uses the [data system](https://github.com/JereKuusela/valheim-world_edit_commands/blob/main/README_data.md) of World Edit Commands.

Most of the values can be parametrized. These are indicated by letter P in this document.

See section [Parameters](### Parameters) for more information.

### expand_prefabs.yaml

Most fields are put on a single line. List values are separated by `,`.

- prefab: List of affected object ids.
  - Wildcard `*` can be used for partial matches. For example `Trophy*` to match all trophies.
  - Value groups can be used ([data system](https://github.com/JereKuusela/valheim-world_edit_commands/blob/main/README_data.md#multiple-parameter-values)).
    - By default, each object component has its own value group. For example `Tameable` or `Piece`.
    - By default, keywords `creature` (Humanoid) and `structure` (WearNTear) have their own value group.
    - Values from groups are cached, so the prefab yaml must be manually saved when changing an already used value group.
- type: Type of the trigger and parameter (`type, parameter`).
  - Parameter is optional and can be used to specify the trigger.
  - Supported types are:
    - `create`: When objects are created. No parameter.
    - `destroy`: When objects are destroyed. No parameter.
    - `repair`: When structures are repaired. No parameter.
    - `damage`: When structures or trees are damaged. No parameter.
    - `state`: When objects change state. Parameter is the state name.
    - `say`: When objects or players say something. Parameter is the text.
      - Using this type automatically adds Server client to the player list.
      - Server client is needed to intercept chat messages.
      - Server client counts as an extra player for boss kills, increasing the amount of loot.
    - `command`: When admins say something. Parameter is the text.
      - Using this type automatically adds Server client to the player list.
    - `poke`: When `pokes` field is used.
    - `globalkey`: When a global key is set or removed. Parameter is the key name.
      - Use field `remove` to trigger on key removal.
      - There is no prefab or position for this type, so most fields won't work.
    - `key`: When a custom saved data is set or removed. Parameter is the data name.
      - Use field `remove` to trigger on data removal.
      - This only triggers when the saved data actually changes.
      - There is no prefab or position for this type, so most fields won't work.
    - `event`: When an event starts or ends. Parameter is the event name.
      - Use field `end` to trigger on event end.
      - There is no prefab for this type, so most fields won't work.
  - Objects spawned or removed by this mod won't trigger `create` or `destroy`.
- types: List of types.
- weight (default: `1`, P): Chance to be selected if multiple entries match.
  - All weights are summed and the probability is `weight / sum`.
  - If the sum is less than 1, the probability is `weight`, so there is a chance to not select any entry.
- fallback (default: `false`): If true, this entry can only be selected if no other entries match.

## Filters

If a filter is not specified, it's not checked and is always considered valid.

- biomes: List of valid biomes.
- day (P): Valid during the day.
- night (P): Valid during the night.
- minDistance (P): Minimum distance from the world center.
- maxDistance (P): Maximum distance from the world center.
- minX (P): Minimum x coordinate.
- maxX (P): Maximum x coordinate.
- minZ (P): Minimum z coordinate.
- maxZ (P): Maximum z coordinate.
- minY (P): Minimum y coordinate.
- maxY (P): Maximum y coordinate.
- minAltitude (P): Minimum altitude (y coordinate + 30).
- maxAltitude (P): Maximum altitude (y coordinate + 30).
- minTerrainHeight (P): Minimum terrain height (y coordinate).
- maxTerrainHeight (P): Maximum terrain height (y coordinate).
- paint: Valid terrain paint color.
  - Supports values cultivated, dirt, grass, grass_dark, patches, paved, paved_dark, paved_dirt and paved_moss.
  - Supports numeric value r,g,b,a.
  - The terrain must be exactly the same color.
- minPaint: Minimum terrain paint color.
  - Each terrain color component must be same or higher.
- maxPaint: Maximum terrain paint color.
  - Each terrain color component must be same or lower.
- environments: List of valid environments.
- bannedEnvironments: List of  invalid environments.
- globalKeys (P): List of global keys that must be set.
  - The values are converted to lower case because the game always uses lower case.
- bannedGlobalKeys (P): List of global keys that must not be set.
  - The values are converted to lower case because the game always uses lower case.
- keys (P): List of saved custom data that must be set.
  - The values are converted to lower case to match global keys behavior.
- bannedKeys (P): List of saved custom data that must not be set.
  - The values are converted to lower case to match global keys behavior.
- locations: List of location ids. At least one must be nearby.
- bannedLocations: List of location ids. None must be nearby.
- locationDistance (default: `0` meters): Search distance for nearby locations.
  - If 0, uses the location exterior radius.
- events: List of event ids. At least one must be active nearby.
  - If set without `eventDistance`, the search distance is 100 meters.
- eventDistance: Search distance for nearby events.
  - If set without `events`, any nearby event is valid.
- playerEvents: List of event ids. At least one must be possible for the player.
  - The list depends on the player progression (killed enemies, known items, taken Forsaken powers).
  - The list is created even when player based events are not enabled.
  - You can use the parameter `<pdata_possibleEvents>` to print them.
- bannedPlayerEvents: List of event ids. None must be possible for the player.

### Data filters

- filter: Data filter for the object. This can be a data entry or a single data value.
  - Format for a single data value is `type, key, value`. Supported types are bool, int, hash, float and string.
    - `filter: bool, boss, true` would apply only to boss creatures.
    - `filter: string, Humanoid.m_name, Piggy` would apply only to creatures with name "Piggy".
  - Ranges are supported for int and float.
    - `filter: int, level, 2;3` would apply to creatures with 1 or 2 stars
    - `filter: int, level, 0;1` is required for 1 star because 0 is the default value.
  - Wildcards are suppored for strings.
    - `filter: string, TamedName, *(S)*` would apply to pets with name containing "(S)".
  - For type `repair`, the filter is also checked for the player who did the repair.
    - Filter is valid if either the player or the object matches.
  - Data type "items" can be used to filter containers.
    - If item amount is not set, then the items must match exactly.
    - If item amount is set, then at least that many items must match.
    - Items are checked in the same order as they are defined in the "items" list.
    - If item amount is set but items are not, then only the item count is checked.
- filters: List of data value filters. All must match.
- bannedFilter: Data filter that must not be true.
- bannedFilters: List of data value filters. None must match.

### Object filters

- objectsLimit: How many of the `objects` must match (`min` or `min-max`).
  - If not set, then each entry must be found at least once. One object can match multiple filters.
  - If set, that many entries must be found. Each filter can be matched by multiple entries.
  - When using max, all objects must be searched.
- objects (P): List of required nearby objects.
  - prefab: Target object id or value group.
  - minDistance: Minimum distance to the object.
  - maxDistance: Maximum distance to the object. Default is 100 meters.
    - All objects are searched if the max distance is more than 10000 meters.
  - minHeight: Minimum height difference to the object.
  - maxHeight: Maximum height difference to the object.
  - weight: How much this object counts towards the `objectsLimit`. Default is 1.  
  - data: Entry in the `data.yaml` that can be used as filter. All values must match.
- bannedObjectsLimit: How many of the `bannedObjects` must not match (`min` or `min-max`).
  - If not set, then all of the entries must not be found.
  - If set, that many `bannedObjects` must not be found. Each filter can be matched by multiple entries.
  - When using max, all objects must be searched.
- bannedObjects (P): List of banned nearby objects.

See object filtering [examples](examples_object_filtering.md).

### Actions

- addItems: Data entry that is used to add items to the container object.
  - Data type "items" is used for this.
  - If the item exists, its stack amount is increased up to the max.
  - Remaining stack amount is added as new items.
  - For adding a single item, shorthand `itemid, amount` can be used.
- cancel (default: `false`, P): If true, the RPC call of the triggering action is cancelled.
  - This affects types `command`, `damage`, `say`, `state` and `repair`.
  - This only works properly for some actions, since the RPC calls are usually for cosmetic changes.
  - For example chat messages can be cancelled so that they are never shown to other players (for example for non-admin custom commands).
- command: Console command to run.
  - Parameters are supported.
  - Basic arithmetic is supported. For example `<x>+10` would add 10 meters to the x coordinate.
- commands: List of console commands to run.
- data (P): Changes data to the original object.
  - Name of the data entry (from `data.yaml`) or data code that is added to the object.
  - This is done by respawning the original object with the new data.
- drops (P): If true, the object drops are spawned.
  - These include creature drops, destructible drops and structure materials.
  - Not supported for type `destroy`.
- injectData (default: `false`): If true, the object is not respawned when adding data.
  - This doesn't work in most cases because clients don't load the new data.
  - Some possible cases are:
    - When adding data that is only used by this mod. In this case, clients wouldn't use the data anyway.
    - When changing data that changes during the normal game play. For example creature health.
- owner (P): Changes the object owner (number).
  - Only works when using `injectData`.
  - Number 0 removes the owner, but the server will reassign it after a few seconds.
- remove (default: `false`, P): If true, the original object is removed.
- removeDelay (P): Delay in seconds for remove.
- removeItems: Data entry that is used to removes items from the container object.
  - Data type "items" is used for this.
  - If the item doesn't exist then nothing happens.
  - For removing a single item, shorthand `itemid, amount` can be used.
- triggerRules (default: `false`): If true, spawns or remove from this entry can trigger other entries.

### Spawns

- spawn (P): Spawns another object.
  - prefab: Object id or value group.
  - data: Entry in the `data.yaml` to be used as initial data.
  - delay: Delay in seconds for spawning.
  - pos: Position offset in x,z,y from the original object.
  - rot: Rotation offset in y,x,z from the original object.
  - triggerRules: If true, this spawn can trigger other entries.
- swap (P): Swaps the original object with another object.
  - Format and keywords are same as for `spawn`.
  - The initial data is copied from the original object.
  - Swap is done by removing the original object and spawning the swapped object.
  - If the swapped object is not valid, the original object is still removed.
  - Swapping can break ZDO connection, so spawn points may respawn even when the creature is alive.

### Pokes

- poke (P): List of poke objects:
  - prefab: Target object id or value group.
  - self: If true, the object itself is poked. Prefab is not checked.
  - parameter: Custom value used as the parameter for the `poke` type.
  - evaluate: If false, math expressions are not calculated in the parameter. Default is true.
    - For example if some text has math symbols, it might cause weird results.
    - Math expression are considered legacy, use [functions](Functions) instead.
  - delay: Delay in seconds for poking.
  - limit: Maximum amount of poked objects. If not set, all matching objects are poked.
  - minDistance: Minimum distance from the poker.
  - maxDistance: Maximum distance from the poker. Default is 100 meters.
  - minHeight: Minimum height difference from the poker.
  - maxHeight: Maximum height difference from the poker.
  - data: Optional. Entry in the `data.yaml` to be used as filter. All data entries must match.

### RPCs

RPC calls are way to send data to clients. Usually these are used by clients but server can call them too.

Checks possible RPCs [here](RPCs.md).

- objectRpc: List of RPC calls. The RPC must be related to the triggering object.
- clientRpc: List of RPC calls. These calls are not related to any object.

RPC format:

- name: Name of the RPC call. Must be exact match.
  - See list of supported calls: (RPCs.md)
- target (P): Target of the RPC call. Default is `owner`.
  - `owner`: The RPC is sent to the owner of the object.
  - `all`: The is sent to all clients.
  - ZDO id: The RPC is sent to the owner of this ZDO.
    - Parameters are supported. For example `<zdo>` can be useful.
- delay (P): Delay in seconds for the RPC call.
- source (P): ZDO id. The RPC call is faked to be from owner of this ZDO.
  - Parameters are supported. For example `<zdo>` can be useful.
- packaged: If true, the parameters are sent as a package. Default is false.
  - This must be set to true for some RPC calls.
- 1: First parameter.
- 2: Second parameter.
- 3: Third parameter.
- ...: More parameters.

### Terrain

Terrain can be changed with RPC call ApplyOperation.

However this is very difficult to use because of the underlyting terrain compiler system.

For this reason, terrrain changes have their own field.

- terrain (P): List of terrain operations.
  - Automatically creates missing _TerrainCompiler objects.
  - When compiler object is created, the terrain change is delayed by 1 second.
  - Automatically affects all compilers within the radius.
  - Only works for zones that are loaded by some client.
    - For this reason, radius shouldn't exceed ~100 meters.

Terrain operation:

- delay: Delay in seconds for the terrain change.
- pos: Position offset in x,z,y from the original object.
- resetRadius: Radius for the terrain and paint reset.
  - This is purely done server side, so you can't use other operations with this.
- square: If true, square shape is used.
- levelRadius: Radius for the level change.
- levelOffset: Offset for the level change.
- raiseRadius: Radius for the raise change.
- raisePower: Power for the raise change.
- raiseDelta: Delta for the raise change.
- smoothRadius: Radius for the smooth change.
- smoothPower: Power for the smooth change.
- paintRadius: Radius for the paint change.
- paintHeightCheck: If true, checks something.
- paint: Terrain paint color. Supports values ClearVegetation, Cultivate, Dirt, Paved and Reset.
  - Numeric values are not supported.

### States

State works for following objects:

- Armor stand: Setting item triggers state with `itemid variant slot` or `none 0 slot`.
  - For specific item on any slot, use `itemid` or `itemid variant`.
  - For any item on specific slot, use `* * slot`.
- Ballista: Targeting triggers state with the target id.
- Cooking stations: Setting item triggers state with `itemid slot` or `none slot`.
  - For specific item on any slot, use `itemid`.
  - For any item on specific slot, use `* slot`.
- Creatures: Each animation such as attacks triggers state.
- Creatures: Being targeted by ballista triggers state `target`.
- Creatures: Setting saddle triggers state `saddle` or `unsaddle`.
- Creatures: Waking up from sleep triggers state `wakeup`.
- Item stands: Setting item triggers state with `itemid variant quality` or `none 0 0`.
  - For specific item of any variant or quality, use `itemid`.
  - For any item of specific quality, use `* * quality`.
- Feast: Eating triggers state `eat`.
- ItemDrop: Turning a drop into a piece triggers `piece`.
- MusicVolume: Entering the volume triggers state without parameter.
- Obliterator: Using the lever triggers state `start` and `end`.
- Pickables: Picking triggers state `picked` or `unpicked`.
- Traps: Triggering the trap triggers state with the target id.
- Ward: Triggering the ward triggers state `flash`.

### Parameters

Following parameters are available to be used in the yaml file:

- `<prefab>`: Original prefab id.
- `<safeprefab>`: Original prefab id with underscores replaced by dashes.
  - This can be used as workaround because underscores split the prefab id as separate parameters.
- `<zdo>`: Object id.
- `<x>`, `<y>` and `<z>`: Object center point.
- `<pos>`: Object center point as x,z,y.
- `<i>` and `<j>`: Object zone indices.
- `<a>`: Object rotation.
- `<rot>`: Object rotation as y,x,z.
- `<day>`: Days since the world start (int type).
- `<time>`: Seconds since the world start (float type).
  - Each day is 1800 seconds.
- `<ticks>`: Ticks since the world start (long type).
  - Each second is 10000000 ticks.
- `<key_*>`: Global key value.
- `<int_*>`: Integer value from the object data.
- `<float_*>`: Decimal value from the object data.
- `<long_*>`: Big integer value from the object data.
- `<string_*>`: Text value from the object data.
- `<bool_*>`: Integer value from the object converted to true or false.
- `<hash_*>`: Integer value from the object converted to prefab name.
- `<vec_*>`: Vector3 value from the object converted to x,z,y.
- `<quat_*>`: Quaternion value from the object converted to y,x,z.
- `<byte_*>`: Byte value from the object converted to base64 text.
- `<zdo_*>`: Object id value from the object.
- `<item_*>`: Amount of specific item in the container.
- `<item_X_Y>`: Item name at slot X,Y.
- `<pdata_*>`: Player data.
  - `<pdata_baseValue>`: Amount of nearby player base structures.
  - `<pdata_possibleEvents>`: List of possible events.
- `<pid>`: Steam/Playfab of the client that controls the object.
  - The client always controls its player object.
- `<pname>`: Player name of the client that controls the object.
- `<pchar>`: Character id of the client that controls the object.
- `<owner>`: Id of the owner client (long number).

Object attributes can be queried with the field system. For example `<float_WearNTear.m_health>` to get piece maximum health or `<float_ItemDrop.m_itemData.m_shared.m_maxDurability>` to get item maximum durability.

For missing object data, the default value can be set by adding `=value`. For example `<int_level=1>`.

### Functions

Custom functions: See [Expand World Code](https://github.com/JereKuusela/valheim-expand_world_code).

Text related functions:

- `<len_X>`: Returns length of the text X.
- `<lower_X>`: Returns lower case of the text X.
- `<par>`: Returns the whole parameter.
- `<par_X`>: Returns parameter X.
- `<upper_X>`: Returns upper case of the text X.
- `<rest_X>`: Returns the rest of the text starting from par X.
- `<trim_X>`: Returns text X without leading and trailing spaces.
- `<hash_X>`: Returns hash of the text X.

Number related functions:

- `<abs_X>`: Returns absolute value of the number X.
- `<add_X_Y>`: Returns sum of X and Y.
- `<asin_X>`: Returns arcsine of X.
- `<acos_X>`: Returns arccosine of X.
- `<atan_X>`: Returns arctangent of X.
- `<atan_X_Y>`: Returns arctangent of X/Y.
- `<calcf_X>`: Evaluates the math expression X and returns a decimal number.
- `<calci_X>`: Evaluates the math expression X and returns an integer number.
- `<ceil_X>`: Returns smallest integer greater than or equal to X.
- `<cos_X>`: Returns cosine of X.
- `<div_X_Y>`: Returns quotient of X and Y.
- `<exp_X>`: Returns e raised to the power of X.
- `<floor_X>`: Returns largest integer less than or equal to X.
- `<log_X>`: Returns natural logarithm of X.
- `<log_X_Y>`: Returns logarithm of X with base Y.
- `<max_X_Y>`: Returns maximum of X and Y.
- `<min_X_Y>`: Returns minimum of X and Y.
- `<mod_X_Y>`: Returns remainder of X divided by Y.
- `<mul_X_Y>`: Returns product of X and Y.
- `<pow_X_Y>`: Returns X raised to the power of Y.
- `<randf_X_Y>`: Returns random decimal number between X and Y.
- `<randi_X_Y>`: Returns random integer number between X and Y.
- `<round_X>`: Returns nearest integer of X.
- `<sin_X>`: Returns sine of X.
- `<sqrt_X>`: Returns square root of X.
- `<sub_X_Y>`: Returns difference of X and Y.
- `<tan_X>`: Returns tangent of X.

Custom data related functions:

- `<save_X_Y>`: Saves custom data with key X and value Y.
  - Wildcard * in the key name can be used to set multiple keys at once (these keys must already exist).
- `<save++_X>`: Shorthand for increasing the value of key X by 1. Missing keys are created with value 1.
- `<save--_X>`: Shorthand for decreasing the value of key X by 1. Missing keys are created with value -1.
- `<clear_X>`: Removes custom data with key X.
  - Wildcard * in the key name can be used to remove multiple keys at once.
- `<load_X=default>`: Gets custom data with key X. If not found, returns the given default value.

Custom data can be used to replace global keys. The biggest benefit is that custom data is not sent to clients, which reduces network traffic and keeps them hidden from players.

### Legacy

Legacy ways will be supported but they may miss some features.

Old way of object filtering. When using both old and new format, entries with the old format must be before the new format.

- objects (P): List of required nearby objects. Format is `- id, distance, data, weight, height`:
  - id: Object id.
  - distance: Distance to the object (`max` or `min-max`). Default is up to 100 meters.
    - All objects are searched if the max distance is more than 10000 meters.
  - data: Optional. Entry in the `data.yaml` to be used as filter. All data entries must match.
  - weight: Optional. How much tis match counts towards the `objectsLimit`. Default is 1.
  - height: Optional. Height difference to the object  (`max` or `min-max`).'
- bannedObjects (P): List of banned nearby objects.

Old way of poking.

- pokeDelay: Delay in seconds for poking.
- pokeParameter: Custom value used as the parameter for the `poke` type.
- pokeLimit: Maximum amount of poked objects.
  - If not set, all matching objects are poked.
- pokes: List of object information. Format is `- id, distance, data`:
  - id: Object id or value group.
  - distance: Distance to the object (`max` or `min-max`). Default is up to 100 meters.
  - data: Optional. Entry in the `data.yaml` to be used as filter. All data entries must match.

Old way of spawning.

- spawns: Short-format for spawns without parameter support.
  - Format is `id, posX,posZ,posY, rotY,rotX,rotZ, data, delay, triggerRules`.
  - Most parts are optional. For example following formats are valid:
    - `id, posX,posZ,posY, rotY,rotX,rotZ, delay`
    - `id, posX,posZ,posY, data, triggerRules`
    - `id, data, delay`
    - `id, triggerRules`
  - Id is required and supports parameters.
  - Position must be set before rotation.
  - PosY can be `snap` to snap to the ground.
- spawn: Single line short-format for spawns without parameter support.
- spawnDelay: Delay in seconds for spawns and swaps.
- swaps: Short-format for swaps without parameter support.
- swap: Single line short-format for swaps without parameter support.

## Credits

Thanks for Azumatt for creating the mod icon!

Sources: [GitHub](https://github.com/JereKuusela/valheim-expand_world_prefabs)

Donations: [Buy me a computer](https://www.buymeacoffee.com/jerekuusela)
