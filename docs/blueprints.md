# Blueprints

Blueprints can be spawned like any other object. Use the name of a blueprint file as the `prefab` of a `spawn` or `swap`.

Both PlanBuild .blueprint and BuildShare .vbuild files are supported (recommended to use PlanBuild files).

```yaml
- prefab: Vegvisir
  type: create
  spawn:
    prefab: MarketplaceStall
    pos: 5, 0, 0
    rot: 90, 0, 0
```

## Configuration

- Blueprint folder (default: `PlanBuild`, relative to the config folder): Folder for the blueprint files. Both the profile and the base Valheim config folders are searched, including subfolders.
- Terrain level limit (default: `8`): Maximum height change of the terrain `level` and `raise` operations. Use `0` for no limit. This is for mods that change the limit of the game.

Blueprints are loaded when they are first used. If `Automatic file reload` is enabled, changed files are loaded automatically. Adding or removing a file reloads all scripts so that rules can find the new name. Changes to the content of an existing file are used by the next spawn.

## Usage

- The blueprint is placed so that its center is at the spawn position. `pos`, `rot` and `snap` work like for other objects.
- Blueprint names work like object ids. For example wildcards (`Stall*`) and value groups can be used to pick a random blueprint.
- Real objects are preferred if a blueprint has the same name as an object.
- Every object of the blueprint is spawned separately. `delay`, `removeDelay`, `owner` and `triggerRules` apply to every object.
- Objects can be spawned gradually (relative to the blueprint rotation, the earliest object has no extra delay):
  - blueprintVerticalDelay (default: `0`): Total extra delay in seconds from the lowest to the highest object. Negative builds from top to bottom.
  - blueprintHorizontalDelay (default: `0`): Total extra delay in seconds from the center to the farthest object. Negative builds from outside to inside.
  - blueprintSectorDelay (default: `0`): Total extra delay in seconds for a full turn around the center, starting from the forward direction.
  - blueprintDelayStep (default: `0`): Precision of the extra delay. The delay is rounded to the nearest multiple of this value. For example `0.5` spawns the objects at `0`, `0.5`, `1`, etc.
  - Terrain changes are not delayed.
- `chance` and `weight` of the spawn apply to the whole blueprint. `repeat` spawns the whole blueprint again with new chances.
- `data`, `attach` and `connect` of the spawn are not used for the objects. The objects use the data from the blueprint file.
- Creator data is removed from the objects, so creatures don't consider them to be built by a player.

## File format

The file format is shared with Expand World Data and Infinity Hammer. The same blueprint file can be used with all of them.

- `.blueprint` rows are separated by `;`: `name;unused;posX;posY;posZ;rotX;rotY;rotZ;rotW;info;scaleX;scaleY;scaleZ;`**`data;chance`**
- `.vbuild` rows are separated by space: `name rotX rotY rotZ rotW posX posY posZ`**`data chance`**

The bold fields are extensions made for the Expand World mods and Infinity Hammer. They are optional and always the last columns, so files without them still work. The `.vbuild` format has no scale or info.

### Fields

- name: Object id or name of another blueprint (see Nested blueprints).
- pos, rot: Position and rotation relative to the center of the blueprint. Rotation is a quaternion.
- scale (default: `1,1,1`): Scale of the object.
- data: Initial object data.
  - Either a name of a data entry or the raw base64 data.
  - Infinity Hammer saves this when creating .blueprint files.
- info: Text field of PlanBuild that is used if `data` is empty. The meaning depends on the object:
  - Sign: text.
  - Teleporter: tag.
  - Creature: tamed name.
  - Item stand: `item:variant:quality:orientation`, only the item is required.
  - Armor stand: `pose:slotCount:item:variant:item:variant...`.
  - Item drops (except fish) are marked as placed by a player.
  - Info can contain commas.
- chance (default: `1`): Chance (from 0 to 1) that the object appears. Each object is rolled separately.
  - Can have a group after an underscore, for example `0.5_a`. See Alternatives.

### Alternatives

Objects with the same group (like `0.5_a` and `0.5_a`) are alternatives. One of them is picked and the chances work as probabilities of being the picked one.

- `0.5_a` and `0.5_a`: One of the objects always appears.
- `0.25_a` and `0.25_a`: Half of the time none of them appears.
- `0.9_a` and `0.9_a`: If the total is over 1, the chances are scaled down so that they add up to 1.
- `_a` without a number has chance 1.
- Groups are separate for each blueprint, so nested blueprints can use the same group names.

### Nested blueprints

Blueprints can contain other blueprints as objects.

- These must be added manually to the file.
- This can be useful for larger blueprints that have distinct parts.
- Another use is to make `chance` field to affect multiple objects.
- Real objects are preferred if a blueprint has the same name as an object.
- Maximum depth is 8.
- Terrain of the nested blueprints is also applied. See Terrain snapshots.

### Sections and headers

Lines starting with `#` are headers. Rows after a header belong to that section. Rows before any header are objects.

- `#Pieces`: Objects. This is the default section.
- `#Center:object`: Center piece (bottom center of the blueprint). Default is `piece_bpcenterpoint`.
  - This object is never spawned to the world. If you want to spawn it, duplicate the line manually.
  - Infinity Hammer saves this information automatically to .blueprint files.
  - If the center piece is not found, the blueprint is centered automatically and placed 0.05 meters towards the ground.
- `#SnapPoints`: Position of snap points as rows `x;y;z`. Used by dungeon rooms in Expand World Data.
- `#TerrainHeight`, `#TerrainPaint`: Terrain snapshots. See Terrain snapshots.
- `#Name`, `#Creator`, `#Description`, `#Category`, `#Coordinates`, `#Rotation`: Information that Infinity Hammer uses. These are not used by the Expand World mods.
- `#` alone or followed by a space is a comment and is ignored.
- Unknown headers skip the rows until the next header.

If an object row is invalid, the blueprint is not loaded and the error is logged. Invalid terrain only skips the terrain.

## Terrain snapshots

Blueprints with the `#TerrainHeight` and `#TerrainPaint` sections change the terrain when they are spawned. Terrain is stored as a final height and paint grid rather than as ordered terrain modifier objects. Empty cells leave the destination terrain unchanged.

The section header contains the grid center in XZY order, its capture yaw and the distance between nodes:

```txt
#TerrainHeight:centerX,centerZ,centerY;yaw;nodeSpacing
#TerrainPaint:centerX,centerZ,centerY;yaw;nodeSpacing
```

Height values are absolute heights relative to the blueprint root. Paint values use `r:g:b:a`. Infinity Hammer is not required on the server or clients.

- Terrain is edited on the server through the terrain compilers of the zones, so the zones must be generated. If a zone or its compiler is not ready, it is tried again every second for 12 attempts.
- Terrain uses the same `delay` as the objects, but the objects are not delayed if the terrain is not ready.
- The terrain is not restored when the objects are removed.
- Blueprint scale does not scale terrain grids.
- The stored height values are not limited by this mod. The game limits how much terrain can be changed (vanilla limit is 8 meters from the generated base terrain, some mods change this).

Terrain of nested blueprints is included:

- Chance, alternatives and nested blueprints are resolved first, so only the terrain of objects that actually spawn is used.
- The terrains are merged into one grid for the blueprint. Terrain of later objects overrides earlier ones.
- The merged grid uses the smallest node spacing. Nested blueprints rotated other than by multiples of 90 degrees can be slightly uneven.
- The merged area can't be larger than 2048 x 2048 nodes (or 64 zones). Otherwise the nested terrain is skipped with a warning.

Terrain-enabled files are an Infinity Hammer/Expand World extension and current PlanBuild versions do not load these snapshot rows directly.

## Examples

Two alternative decorations, one of them always appears:

```txt
Pickable_Mushroom;;2;0;1;0;0;0;1;;1;1;1;;0.5_a
Pickable_Thistle;;2;0;1;0;0;0;1;;1;1;1;;0.5_a
```

Sign with text in the info column:

```txt
sign;;0;0;2;0;0;0;1;Welcome, traveler;1;1;1;
```

Nested blueprint object with chance and ZDO data (PlanBuild format):

```txt
MarketplaceStall;0;6;0;4;0;0;0;1;;1;1;1;infinite_health;0.35
```
