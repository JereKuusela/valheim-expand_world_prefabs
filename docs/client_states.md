# Client states

Very niche feature, see [hacks](hacks.md#server-owned-objects) for the general idea.

Type `clientstate` reacts to RPC calls that vanilla only sends to the owning client. These are normally never seen by the server, so the triggering object must have `owner: server` set (or otherwise be forced server owned) for these to ever fire.

Format and filters are identical to type `state` (first parameter is the state name, further parameters depend on the state).

- AddSaddle (Tameable): `addsaddle`.
- Alert (BaseAI): `alert`.
- Command (Tameable): `command`.
- MineRock: Hit triggers `hit "part index"`.
- MineRock5: RPC_Damage triggers `hit "part index"`.
- OnTargeted (Player): `targeted "is sensed" "is targeted"`.
- Pick (PickableItem): `pick`.
- RemoveSaddle (Tameable): `removesaddle`.
- RPC_AddAdrenaline (Character): `adrenaline "amount"`.
- RPC_AddAmmo (Turret): `ammo "item name"`.
- RPC_AddFuel (CookingStation, Fireplace, ShieldGenerator, Smelter): `fuel`.
- RPC_AddFuelAmount (Fireplace): `fuel "amount"`.
- RPC_AddItem (CookingStation, Fermenter): `item "item name"`.
- RPC_AddNoise (Character): `noise "amount"`.
- RPC_AddOre (Smelter): `ore "item name"`.
- RPC_AddStatusEffect (SEMan): `statuseffect "status effect hash"`.
- RPC_Attack (ShieldGenerator): `attack`.
- RPC_ClearCachedSupport (WearNTear): `clearsupport`.
- RPC_Damage (Character, Destructible, TreeBase, TreeLog, WearNTear): `damage`.
  - Note: shares the same RPC name/hash across all of these components, so it can't distinguish which one triggered it.
- RPC_Drain (ResourceRoot): `drain "amount"`.
- RPC_EmptyProcessed (Smelter): `empty`.
- RPC_Extract (Beehive, SapCollector): `extract`.
- RPC_Heal (Character): `heal "amount"`.
- RPC_HitWhileDodging (Player): `dodge`.
- RPC_OnHit (Projectile): `hit`.
- RPC_Pick (Pickable): `pick "extra amount"`.
- RPC_Remove (WearNTear): `remove`.
- RPC_RemoveDoneItem (CookingStation): `donecooking`.
- RPC_Repair (WearNTear): `repair`.
- RPC_Sleep (MonsterAI): `sleep`.
- RPC_SetFuel (ShieldGenerator): `fuel "amount"`.
- RPC_SetFuelAmount (Fireplace): `fuel "amount"`.
- RPC_SetTag (TeleportWorld): `tag "tag"`.
- RPC_SetTamed (Character): `tamed` or `untamed`.
- RPC_Stagger (Character): `stagger`.
- RPC_Tap (Fermenter): `tap`.
- RPC_ToggleOn (Fireplace): `toggle`.
- SetAggravated (BaseAI): `aggravated "is aggravated" "reason (0/1/2)"`.
- SetName (Tameable): `name "creature name"`.
- SetPlayed (MusicLocation): `played`.
- TogglePermitted (PrivateArea): `ward_permitted`.
- ToggleEnabled (PrivateArea): `ward_toggle`.
- Trigger (TriggerSpawner): `trigger`.
- UseDoor (Door): `door "forward"` or `door "backward"`.
- UseStamina (Player): `stamina "amount"`.
