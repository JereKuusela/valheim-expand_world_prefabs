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
- type: Type of the trigger and parameter (`type, parameter1 parameter2`).
  - Parameters are optional and can be used to specify the trigger.
  - Parameters supports numeric ranges (`min;max`) and multiple values (`value1,value2`).
  - Supported types are:
    - `create`: When objects are created. No parameter.
    - `destroy`: When objects are destroyed. No parameter.
    - `change`: When objects data changes.
      - First parameter is the data name.
      - Second parameter is the data value.
      - Third parameter is the previous data value.
      - If the data value has spaces, it will be split into multiple parameters.
    - `state`: When objects broadcast a state change with RPC. First parameter is the state name. Second parameter is the state value.
      - This is more performant than `change` but supports only specific situations.
      - Recommended to check ([states](### State)) and use this if possible.
    - `say`: When objects or players say something. Parameter is the text.
      - Using this type automatically adds a server client to the player list.
      - Server client is needed to intercept chat messages.
      - Server client counts as an extra player for boss kills, increasing the amount of loot.
    - `command`: Deprecated. Use `say` with `admin: true` instead.
    - `poke`: When `pokes` field is used.
      - [Basic Discord Guide](https://discord.com/channels/1167153871546744842/1366020389905629264), [Advanced Discord Guide](https://discord.com/channels/1167153871546744842/1366022688275173550)
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
    - `time`: When the time changes.
      - First parameter is the granularity (day, hour, minute, tick).
      - Second parameter is the condition (single value, multiple values, range).
      - Note: Valheim day is 30 minutes, Valheim hour is 1.25 minutes, Valheim minute is 1.25 seconds.
      - [Discord Guide](https://discord.com/channels/1167153871546744842/1360994829663867040)
  - Objects spawned or removed by this mod won't trigger `create` or `destroy`.
- types: List of types.
- chance (default: `1`, P): Chance to execute this entry when all filters match.
  - This can be combined with weight and is checked after weight selection.
- weight (optional, P): When set, only one of the weighted entries is selected.
  - All weights are summed and the probability is `weight / sum`.
  - Sum is at least 1, so with low weights there is a chance to not select anything.
- fallback (default: `false`): If true, this entry can only get selected if no other entries match.

## Filters

If a filter is not specified, it's not checked and is always considered valid.

All valid entries will be executed in unspecified order. Weight system can be used trigger only one entry.

- admin (P): Checks if the object owner is an admin.
  - If true, the owner must be an admin.
  - If false, the owner must not be an admin.
  - If not set, nothing is checked.
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
- locationDistance (default: `0` meters): Search distance for nearby locations.
  - If 0, uses the location exterior radius.
  - This also affects banned locations, unless `bannedLocationDistance` is set.
- bannedLocations: List of location ids. None must be nearby.
- bannedLocationDistance (default: `0` meters): Search distance for nearby banned locations.
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

### Data filters (P)

Filtering can be done based on object's data.

- filter: Data filter that must match.
- bannedFilter: Data filter that must not match.

Filters can be either data entries or single data values.

Format for a single data value is `type, key, value`. Supported types are bool, float, hash, int, quat, string and vec.

```yaml
# This data entry must match.
filter: coinStack
# Creature must be a boss.
filter: bool, boss, true
# Creature must have 1-2 stars.
filter: int, level, 2;3
# Pet name must contain "(S)".
filter: string, TamedName, *(S)*
# This data entry must NOT match.
bannedFilter: hasTakenDamage
# Creature must NOT be named "Piggy".
bannedFilter: string, Humanoid.m_name, Piggy
```

For type `repair`, the filter is also checked for the player who did the repair. Filter is valid if either the player or the object matches.

Containers can be filtered by items. This is done by using "items" from a data entry.

- If item amount is not set, then the items must match exactly.
- If item amount is set, then at least that many items must match.
- Items are checked in the same order as they are defined in the "items" list.
- If item amount is set but items are not, then only the item count is checked.

### Multiple filters (P)

There can be multiple required filters and banned filters. By default, each required filter and no banned filter must match.

Format for a data entry is `name, weight`, with weight being optional.

Format for a single data value is `type, key, value, weight`, with weight being optional.

- filterLimit: Can be used to change how many filters must match.
  - Default limit is the amount of required filters.
- filters: List of required data filters.
  - Matching filters count positively towards the limit.
  - Default weight is 1, which means all filters must match to reach the default limit.
- bannedFilters: List of banned data filters.
  - Banned filters count negatively towards the limit.
  - Default weight is 10000, which causes any matching filter to fail the default limit.

```yaml
# Matches when a player is wearing at least 2 pieces of the bronze armor.
filterLimit: 2
filters: 
# Bronze helmet counts as 2 pieces.
- hash, HelmetItem, HelmetBronze, 2
- hash, ChestItem, ArmorBronzeChest
- hash, LegsItem, ArmorBronzeLegs
# The player must not be wearing any iron armor.
bannedFilters:
- hash, HelmetItem, HelmetIron
- hash, ChestItem, ArmorIronChest
- hash, LegsItem, ArmorIronLegs
```

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
  - offset: Position offset in x,z,y from the original object position and rotation.  
  - Data filters like `filter`, `filters`, `bannedFilter` and `bannedFilters` can be used to filter the object.
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
  - Using `say` command requires either Discord Control mod or Server Devcommands mod (with Server chat enabled).
- commands: List of console commands to run.
- data (P): Sets multiple values when using a data entry (name from from `data.yaml`).
  - This is done by respawning the object (can be changed b setting `injectData` to true).
- data (P): Sets a single value with format `type, key, value`.
  - This is done without respawning the object (can be changed by setting `injectData` to false).
- injectData: If true, the object is not respawned when adding data.
  - This doesn't work in most cases because clients don't load the new data.
  - Some possible cases are:
    - When adding data that is only used by this mod. In this case, clients wouldn't use the data anyway.
    - When changing data that changes during the normal game play. For example creature health.
- drops (P): If true, the object drops are spawned.
  - These include creature drops, destructible drops and structure materials.
  - This can also be a data entry with `items` information.
  - Not supported for type `destroy`.
- exec (P): Runs parameters with side effects.
  - Mostly useful for saving custom data with the `save` function.
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
    - Supports `type, key, value` format to set a single data value.
  - pos: Position offset in x,z,y from the original object.
  - rot: Rotation offset in y,x,z from the original object.
  - triggerRules: If true, this spawn can trigger other entries.
  - chance (default: `1`): Chance to spawn the object.
  - weight (optional): When set, only one of the weighted spawns is selected.
    - All weights are summed and the probability is `weight / sum`.
    - Sum is at least 1, so with low weights there is a chance to not spawn anything.
  - delay: Delay in seconds for spawning.
  - repeat (default: `0`): How many times the spawn is repeated.
  - repeatInterval (default: `0`): Interval in seconds between repeats.
  - repeatChance (default: `1`): Chance to spawn for each attempt (including the original).
- swap (P): Swaps the original object with another object.
  - Format and keywords are same as for `spawn`.
  - The initial data is copied from the original object.
  - Swap is done by removing the original object and spawning the swapped object.
  - If the swapped object is not valid, the original object is still removed.
  - Swapping can break ZDO connection, so spawn points may respawn even when the creature is alive.
  - Chance works for swap too. If it fails, the original object is still removed.

### Pokes

Poking allows to trigger actions on other objects (or even on the original object).

- poke (P): List of poke objects:
  - prefab: Target object id or value group.
    - By default, the object itself can't be poked. You can set `self` to true allow self poking.
  - self: When set to true, the object itself can be poked.
    - If prefab is set, then other filters must apply as usual.
    - If prefab is not set, then the object itself is always poked.
  - target: Specific ZDO object. This can't be used with prefab.
  - pars: Parameters for the `poke` type.
    - Format is `par0, par1, par2, ...`.
    - Structure is strictly defined. Parameter values with commas won't cause additional parameters.
  - parameter: Old way to specify the poke parameter.
    - Format is `par0 par1 par2 ...`.
    - Structure is dynamic, Parameter values with spaces will cause additional parameters.
  - evaluate: If false, math expressions are not calculated in the parameter. Default is true.
    - For example if some text has math symbols, it might cause weird results.
    - Math expression are considered legacy, use [functions](Functions) instead.
  - chance (default: `1`): Chance to poke.
  - weight (optional): When set, only one of the weighted pokes is selected.
    - All weights are summed and the probability is `weight / sum`.
    - Sum is at least 1, so with low weights there is a chance to not poke anything.
  - delay: Delay in seconds for poking.
  - repeat (default: `0`): How many times the poke is repeated.
  - repeatInterval (default: `0`): Interval in seconds between repeats.
  - repeatChance (default: `1`): Chance to poke for each attempt (including the original).
  - limit: Maximum amount of poked objects. If not set, all matching objects are poked.
  - minDistance: Minimum distance from the poker.
  - maxDistance: Maximum distance from the poker. Default is 100 meters.
  - minHeight: Minimum height difference from the poker.
  - maxHeight: Maximum height difference from the poker.
  - offset: Position offset in x,z,y from the original object position and rotation.
  - Data filters like `filter`, `filters`, `bannedFilter` and `bannedFilters` can be used to filter the affected objects.

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
- chance (default: `1`): Chance to trigger.
- weight (optional): When set, only one of the weighted RPCs is selected.
  - All weights are summed and the probability is `weight / sum`.
  - Sum is at least 1, so with low weights there is a chance to not trigger anything.
- delay (P): Delay in seconds for the RPC call.
- repeat (default: `0`): How many times the RPC is repeated.
- repeatInterval (default: `0`): Interval in seconds between repeats.
- repeatChance (default: `1`): Chance to trigger for each attempt (including the original).
- overwrite (default: `false`): If true, the RPC call overwrites any existing delayed calls with same name and target.
  - This is useful for messages so that only the last one is shown.
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

- ArmorStand: Setting item triggers state with `item "itemid" "variant" "slot"` or `<none> 0 "slot"`.
  - For specific item on any slot, use `item "itemid"` or `item "itemid variant"`.
  - For any item on specific slot, use `item * * "slot"`.
- ArmorStand: Setting pose triggers state `pose "index"`.
- Catapult: Using leg triggers state `lock` or `release`.
- Catapult: Setting loaded visual triggers state `loaded "itemid"`.
- Catapult: Shooting triggers state `shoot`.
- Character: Freeze frame triggers state `freezeframe "duration"`.
- Character: Reset cloth triggers state `resetcloth`.
- Character: Being targeted by a turret triggers state `target`.
- CookingStation: Setting item triggers state with `item "itemid" "slot"` or `item <none> "slot"`.
  - For specific item on any slot, use `item "itemid"`.
  - For any item on specific slot, use `item * "slot"`.
- Destructible: When destroyed triggers state `fragments`.
- Feast: Eating triggers state `eat`.
- FootStep: Each footstep triggers state `step "index" "x" "z" "y"`.
- Incinerator (obliterator): Using the lever triggers state `start` and `end`.
- ItemDrop: Turning a drop into a piece triggers `piece`.
- ItemStand: Setting item triggers state with `item "itemid" "variant" "quality"` or `item <none> 0 0`.
  - For specific item of any variant or quality, use `item "itemid"`.
  - For any item of specific quality, use `item * * "quality"`.
- MaterialVariation: Change of material triggers state `material "index"`.
- MineRock: Part being broken triggers state `damage "index"`.
- MineRock5: Being hit triggers state `damage "index" "health"`.
- MonsterAI: Waking up from sleep triggers state `wakeup`.
- MusicVolume: Entering the volume triggers state `music`.
- Pickable: Picking triggers state `picked` or `unpicked`.
- Player: Death triggers state `death`.
- PrivateArea (ward): Triggering the ward triggers state `flash`.
- SapCollector: Using the tap triggers state `effects`.
- ShieldGenerator: Project hit triggers state `hit`.
- Tameable: Setting saddle triggers state `saddle` or `unsaddle`.
- Tameable: Unsummoning triggers state `unsummon`.
- Trap: Triggering the trap triggers state `trap "state"`.
- TreeBase: Growing triggers state `grow`.
- TreeBase: Being hit triggers state `damage`.
- Turret (ballista): Targeting triggers state `targeting "targetid"`.
- WearNTear (structure): Health change triggers either `damage` or `repair`.
- ZSyncAnimation: Each animation such as attacks triggers `action "animation"`.

### Parameters

Following parameters are available to be used in the yaml file:

- `<prefab>`: Original prefab id.
- `<safeprefab>`: Original prefab id with underscores replaced by dashes.
  - This can be used as workaround because underscores split the prefab id as separate parameters.
- `<zdo>`: Object id.
- `<biome>`: Biome where the object is located.
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
- `<amount_X_Y>`: Amount of item at slot X,Y.
- `<durability_X_Y>`: Durability of item at slot X,Y.
- `<quality_X_Y>`: Quality of item at slot X,Y.
- `<item_*>`: Amount of specific item in the container.
  - Wildcard `*` can be used for partial matches. For example `Trophy*` to match all trophies or `*` to count everything.
- `<item_X_Y>`: Item name at slot X,Y.
- `<pdata_*>`: Player data.
  - `<pdata_baseValue>`: Amount of nearby player base structures.
  - `<pdata_possibleEvents>`: List of possible events.
- `<pid>`: Steam/Playfab of the client that controls the object.
  - The client always controls its player object.
- `<pname>`: Player name of the client that controls the object.
- `<pchar>`: Character id of the client that controls the object.
- `<owner>`: Id of the owner client (long number).
- `<none>` Empty or lack of value when using filters.

Object attributes can be queried with the field system. For example `<float_WearNTear.m_health>` to get piece maximum health or `<float_ItemDrop.m_itemData.m_shared.m_maxDurability>` to get item maximum durability.

For missing object data, the default value can be set by adding `=value`. For example `<int_level=1>`.

### Functions

Custom functions: See [Expand World Code](https://github.com/JereKuusela/valheim-expand_world_code).

Text related functions:

- `<findlower_X>`: Returns the lowercase letters in text X.
- `<findupper_X>`: Returns the uppercase letters in text X.
- `<hashof_X>`: Returns hash of the text X.
- `<left_X_Y>`: Returns the leftmost Y characters of text X.
- `<len_X>`: Returns length of the text X.
- `<lower_X>`: Returns lower case of the text X.
- `<mid_X_Y_Z>`: Returns Z characters from text X starting at position Y.
- `<par>`: Returns the whole parameter.
- `<par_X`>: Returns parameter X.
- `<proper_X>`: Returns text X with proper case (first letter of each word capitalized).
- `<rest_X>`: Returns the rest of the text starting from par X.
- `<right_X_Y>`: Returns the rightmost Y characters of text X.
- `<search_X_Y_Z>`: Searches for text X in text Y starting at position Z, returns position or default.
- `<textof_X>`: Returns text of the hash X.
- `<trim_X>`: Returns text X without leading and trailing spaces.
- `<upper_X>`: Returns upper case of the text X.

Number related functions:

- `<abs_X>`: Returns absolute value of the number X.
- `<add_X_Y>`: Returns sum of X and Y.
- `<asin_X>`: Returns arcsine of X.
- `<acos_X>`: Returns arccosine of X.
- `<atan_X>`: Returns arctangent of X.
- `<atan_X_Y>`: Returns arctangent of X/Y.
- `<calcfloat_X>`: Evaluates the math expression X and returns a decimal number.
- `<calcint_X>`: Evaluates the math expression X and returns an integer number.
- `<ceil_X>`: Returns smallest integer greater than or equal to X.
- `<cos_X>`: Returns cosine of X.
- `<div_X_Y>`: Returns quotient of X and Y.
- `<eq_X_Y>`: Returns "true" if X equals Y, "false" otherwise.
- `<even_X>`: Returns "true" if X is even, "false" if odd.
- `<exp_X>`: Returns e raised to the power of X.
- `<floor_X>`: Returns largest integer less than or equal to X.
- `<ge_X_Y>`: Returns "true" if X is greater than or equal to Y, "false" otherwise.
- `<gt_X_Y>`: Returns "true" if X is greater than Y, "false" otherwise.
- `<large_X_Y...>`: Returns the Xth largest value from the list of numbers Y....
- `<le_X_Y>`: Returns "true" if X is less than or equal to Y, "false" otherwise.
- `<log_X>`: Returns natural logarithm of X.
- `<log_X_Y>`: Returns logarithm of X with base Y.
- `<lt_X_Y>`: Returns "true" if X is less than Y, "false" otherwise.
- `<max_X_Y>`: Returns maximum of X and Y.
- `<min_X_Y>`: Returns minimum of X and Y.
- `<mod_X_Y>`: Returns remainder of X divided by Y.
- `<ne_X_Y>`: Returns "true" if X is not equal to Y, "false" otherwise.
- `<odd_X>`: Returns "true" if X is odd, "false" if even.
- `<mul_X_Y>`: Returns product of X and Y.
- `<pow_X_Y>`: Returns X raised to the power of Y.
- `<randf_X_Y>`: Returns random decimal number between X and Y.
- `<randi_X_Y>`: Returns random integer number between X and Y.
- `<randomfloat_X_Y>`: Returns random decimal number between X and Y.
- `<randomint_X_Y>`: Returns random integer number between X and Y.
- `<rank_X_Y...>`: Returns how many numbers in the list Y... are greater than X.
- `<round_X>`: Returns nearest integer of X.
- `<sin_X>`: Returns sine of X.
- `<small_X_Y...>`: Returns the Xth smallest value from the list of numbers Y....
- `<sqrt_X>`: Returns square root of X.
- `<sub_X_Y>`: Returns difference of X and Y.
- `<tan_X>`: Returns tangent of X.

Long number related functions (usually only needed when calculating with game ticks):

- `<addlong_X_Y>`: Returns sum of X and Y as long number.
- `<calclong_X>`: Evaluates the math expression X and returns a long number.
- `<divlong_X_Y>`: Returns quotient of X and Y as long number.
- `<modlong_X_Y>`: Returns remainder of X divided by Y as long number.
- `<mullong_X_Y>`: Returns product of X and Y as long number.
- `<sublong_X_Y>`: Returns difference of X and Y as long number.

Custom data related functions:

- `<clear_X>`: Removes custom data with key X.
  - Wildcard * in the key name can be used to remove multiple keys at once.
- `<load_X=default>`: Gets custom data with key X. If not found, returns the given default value.
- `<save_X_Y>`: Saves custom data with key X and value Y.
  - Wildcard * in the key name can be used to set multiple keys at once (these keys must already exist).
- `<save++_X>`: Shorthand for increasing the value of key X by 1. Missing keys are created with value 1.
- `<save--_X>`: Shorthand for decreasing the value of key X by 1. Missing keys are created with value -1.

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
