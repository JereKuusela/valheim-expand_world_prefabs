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
- excludePrefab: List of excluded object ids.
  - This can be used to skip specific objects when a wildcard or component is used in the `prefab` field.
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
    - `custom`: Manual custom event. Can be only triggered by custom code.
      - See [API documentation](developers.md) for more info.
      - There is no prefab for this type, so most fields won't work.
    - `event`: When an event starts or ends. Parameter is the event name.
      - Use field `end` to trigger on event end.
      - There is no prefab for this type, so most fields won't work.
    - `time`: When the time changes.
      - First parameter is the granularity (day, hour, minute, tick).
      - Second parameter is the condition (single value, multiple values, range).
      - Note: Valheim day is 30 minutes, Valheim hour is 1.25 minutes, Valheim minute is 1.25 seconds.
      - [Discord Guide](https://discord.com/channels/1167153871546744842/1360994829663867040)
    - `realtime`: When the real-world time changes.
      - First parameter is the granularity (day, hour, minute, second).
      - Second parameter is the condition (single value, multiple values, range).
      - Uses server timezone.
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
- bannedBiomes: List of invalid biomes.
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
  - Format is `key1 value1, key2 value2, ...`.
  - Value can be a range `min;max;step` for integers (step defaults 1 if not given).
- bannedKeys (P): List of saved custom data that must not be set.
  - The values are converted to lower case to match global keys behavior.
  - Format is `key1 value1, key2 value2, ...`.
  - Value can be a range `min;max;step` for integers (step defaults 1 if not given).
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
- groups: List og groups. Player must be in at least one of these groups.
  - Requires using Server Devcommands mod or some other mod that provides group implementation.
- bannedGroups: List of groups. Player must not be in any of these groups.

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

- objectsLimit: How many of the `objects` must match (`min` or `min;max`).
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
- bannedObjectsLimit: How many of the `bannedObjects` must not match (`min` or `min;max`).
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
  - Using `say` command requires either Discord Control mod or Server Devcommands mod (with Server chat enabled).
- commands: List of console commands to run.
- data (P): Sets object data either with format `name` or `type, key, value`.
  - Format `name` can be used to set multiple values (entry name from `data.yaml`).
  - Format `type, key, value` is a shorthand to set a single data value.
  - If a component field is set, the object is respawned to apply the changes.
  - Otherwise the data is force pushed to clients, which can override local changes (such as creature movement).
- injectData: If set, overrides the default logic for data changes.
  - When true, the object is always respawned (even when component fields are not changed).
  - When false, the object is never respawned (even when component fields are changed).
  - Only use this if really needed, the default logic works for most cases.
- drops (P): If true, the object drops are spawned.
  - These include creature drops, destructible drops and structure materials.
  - This can also be a data entry with `items` information.
  - Not supported for type `destroy`.
- exec (P): Runs parameters with side effects.
  - Mostly useful for saving custom data with the `save` function.
- owner (P): Changes the object owner (number).
  - Only works when using `injectData: true`.
  - Number 0 removes the owner, but the server will reassign it after a few seconds.
- remove (default: `false`, P): If true, the original object is removed.
- removeDelay (P): Delay in seconds for remove.
- removeItems: Data entry that is used to removes items from the container object.
  - Data type "items" is used for this.
  - If the item doesn't exist then nothing happens.
  - For removing a single item, shorthand `itemid, amount` can be used.
- triggerRules (default: `false`): If true, spawns or remove from this entry can trigger other entries.
- connect (P): Connects this object to another object.
  - This can be done by passing `<zdo>` as poke parameter.
  - Connections allow direct poking.
- attach: (P): Attaches this object to another object.
  - This can be done by passing `<zdo>` as poke parameter.
  - Attaching is SyncTransform connection, so it can also be used for direct poking.
  - Attaching makes the object follow the other object (requires ZSyncTransform component).
  - Attaching disablesthe object by forcing it non-owned. This is required to keep the attachment working.

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
  - owner: Overrides the default initial owner assignment. For example with `<owner>` parameter.
  - connect (P): Connects the spawned object to another object. See Actions for more info.
  - attach (P): Attaches the spawned object to another object. See Actions for more info.
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
  - connected: When set to true, includes objects that are connected to the original object.
    - For other way, use `target: <connected>`.
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
    - If you need to poke something far away, try to use `target`, `position` or `offset` instead.
    - Global triggers don't have any object, so the distance is from the world center (0,0,0).
  - minHeight: Minimum height difference from the poker.
  - maxHeight: Maximum height difference from the poker.
  - position: Absolute position in x,z,y to override the original object position.
  - offset: Position offset in x,z,y from the original object position and rotation.
  - Data filters like `filter`, `filters`, `bannedFilter` and `bannedFilters` can be used to filter the affected objects.

### RPCs

RPC calls are way to send data to clients. Usually these are used by clients but server can call them too.

Check possible RPCs in [RPC documentation](RPCs.md).

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
- Player:
  - Death triggers state `death`.
  - Joining server triggers state `join`.
  - Leaving server triggers state `leave`.
  - Respawning triggers state `respawn`.
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

### Functions

Functions can be used to retrieve values from the object or the game.

Additionally functions can be used to combine values (for example addition or multiplication).

See [functions](functions.md) for more information.

### Hacks

Expand World Prefabs modifies the server logic to allow some unusual behavior. These rely the game client working in a certain way which may change in future updates.

See [hacks](hacks.md) for more information.

## Credits

Thanks for Azumatt for creating the mod icon!

Sources: [GitHub](https://github.com/JereKuusela/valheim-expand_world_prefabs)

Donations: [Buy me a computer](https://www.buymeacoffee.com/jerekuusela)
