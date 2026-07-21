# Hacks

Expand World Prefabs modifies the server logic to allow some unusual behavior. These rely the game client working in a certain way which may change in future updates.

## Extended scale support

This can be disabled by setting "Restore scale" to false in the settings.

Normally scaling only works for objects that have ZNetView.m_syncScale set to true. Unfortunately this field can't be modified because client sets this value **after** field edits have been loaded.

Objects with ZSyncTransform component can be scaled **only when** the object is owned by another game client. EWP supports this by detecting if a client sends wrong scale and then briefly takes ownership which corrects the scale.

This requires the object to have ZSyncTransform.m_syncScale set to true. EWP automatically sets this value for **new or respawned** scaled objects that need it. Scale data can be a single value (scaleScalar) or a vector (scale).

EWP saves backup of the original scale to the object (scaleBackup or scaleScalarBackup). If this data is removed, then scaling might stop working.

```yaml
# Spawns a scaled object.
# !scaled Wolf 5
- prefab: Player
  type: say, !scaled
  spawn:
  - prefab: <par1>
    data: float, scaleScalar, <par2>

# Spawns a scaled object with vector scale.
# !vecscaled Karve 0.1,1,1
- prefab: Player
  type: say, !vecscaled
  spawn:
  - prefab: <par1>
    data: vec, scale, <par2>

# Scales closest nearby object.
# !grow 2
- prefab: Player
  type: say, !grow
  poke:
# Note: ZSyncTransform includes hundreds of prefabs, in real use at least narrow it down to a more specific component.
  - prefab: ZSyncTransform
    limit: 1
    maxDistance: 10
    pars: grow, <par1>
    
- prefab: ZSyncTransform
  type: poke, grow
  data: float, scaleScalar, <par1>
# Respawn is required for the scale hack to apply.
  injectData: false
```

## Attaching

This can be disabled by setting "Object attaching" to false in the settings.

ZSyncTransform component has unused code for "character parent sync". This allows an object to move with another object. However this works **only when** the object is owned by another game client. EWP supports this by forcing the attached object to be owned by a non-existing client. This means no client is running code for the attached object so it just moving around passively.

When the parent object is destroyed, EWP automatically unattaches the child object.

Attach position and rotation can be customized with vec relPos and quat relRot data.

Additionally object can be attached to a specific transformation with string attachJoint data. Use function `<joints>` to get a list of all transformation names for some object.

```yaml
# Spawns prefab and attaches it to the Player.
# !attach Wolf 0,0,180
- prefab: Player
  type: say, !attach
  spawn:
  - prefab: <par1>
    data: quat, relRot, <par2>
    attach: <zdo>

# Unattaches object from the Player.
- prefab: Player
  type: say, !unattach
  poke:
  - connected: true
    pars: unattach

# Note: ZSyncTransform includes hundreds of prefabs, in real use at least narrow it down to a more specific component.
- prefab: ZSyncTransform
  type: poke, unattach
  attach: 0

# Attaches second arg to first arg.
# !glue Wolf Wolf_cub
- prefab: Player
  type: say, !glue
# First poke the parent object that then pokes the child object to attach it.
  poke:
  - prefab: <par1>
    limit: 1
    maxDistance: 50
    pars: glue, <par2>

# Note: ZSyncTransform includes hundreds of prefabs, in real use at least narrow it down to a more specific component.
- prefab: ZSyncTransform
  type: poke, glue
  poke:
  - prefab: <par1>
    limit: 1
    maxDistance: 50
    pars: come, <zdo>

- prefab: ZSyncTransform
  type: poke, come
  attach: <par1>

# Lists joints of nearby objects.
- prefab: Player
  type: say, !joints
  poke:
  - prefab: "*"
# Can be removed to not include the player itself.
    self: true
    maxDistance: 5
    pars: joints

- prefab: "*"
  type: poke, joints
  command: "say <prefab>: <joints>"
```

## Objects that can be attached or scaled

ZSyncTransform component exists in objects that can move around. This includes characters, item drops, projectiles, effects and vehicles.

Infinity Hammer mod has a menu that shows objects by component, which allows easily to see which objects can be attached or scaled.

Infinity Hammer mod also supports scaling these objects when enabled from its settings.

## Server side data

This can be disabled by setting "Server side data" to false in the settings.

Data keys that start with `ewp_` are stored as server-only instead of normal ZDO fields. This reduces network traffic and can even completely skip server force sending data to clients (which can briefly freeze creatures).

On world save, the server side data is packed into a `_ewp_serverdata` byte array. The byte array is read back on load and removed from normal ZDO byte array fields, so clients do not receive these values.

This is intended for internal rule state. Client-visible behavior should still use normal fields.

```yaml
# Stores internal server-only value.
- prefab: Player
  type: say, !create
  spawn:
  - prefab: Wolf
    data: int, ewp_stuff, 1

- prefab: Player
  type: say, !update
  poke:
  - prefab: Wolf
    maxDistance: 50
    pars: update

# Reads and updates value.
- prefab: Wolf
  type: poke, update
  data: int, ewp_stuff, <add_<int_ewp_stuff>_1>
  filter: int, ewp_stuff, 1;4
  command: say Update from <int_ewp_stuff> to <add_<int_ewp_stuff>_1>

# Removed on fifth update.
- prefab: Wolf
  type: poke, update
  remove: true
  fallback: true
  command: say Final update!
```

## Persisting spawned players

This can be enabled by setting "Persist spawned players (experimental)" to true in the settings. It is disabled by default because some issues have been resolved (possibly conflict with another mod).

When enabled, EWP automatically makes EWP spawned players persistent. EWP also forces the player object to be owned by a non-existing client. This is needed because game clients automatically destroy non-controlled owned player objects.

Examples will come later.
