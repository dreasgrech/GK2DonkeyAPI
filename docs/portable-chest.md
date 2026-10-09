# A chest on the cart: feasibility

Status 2026-10-06: **investigation only, nothing built.** The user's ask: a portable chest that rides on the cart,
goes places with it, and can be deconstructed when it's no longer needed. Two investigation rounds: the first
mapped the game's chests (§1); the second re-checked every claim against the code and traced every path that can
touch a chest's contents (§2 to §5). The second round changed the design (§3).

Decompile references are relative to `scratchpad\decomp_1008\`. "Probe" means a fact only the running game can
settle; those are collected in §6.

## 1. How the game's chests work

**A chest is a world object (`WgoData`) with an inventory.**
- The inventory is `WgoData.inventory` (`WgoData.cs:34`), created once from `WGODef.inventorySize`
  (`inventory_size`, `WGODef.cs:105`) in `SetDataFromDefinition` (`WgoData.cs:2157-2174`). Loading a save uses
  the parameterless constructor, so this never runs again on a saved chest.
- An `Inventory` wraps one root `Item`. Its slot count is `Data.InventorySize`. Items don't have fixed slots; the
  list is kept sorted (`Item.cs:1396-1413`).
- Bags inside a chest widen it and are shown as their own panel in the window; bag contents are shared by
  reference, never copied (`Inventory.cs:85-97`, `Item.cs:164-180`).

**Opening it.** `ChestInteractionHandler.Interact` opens `UIChestWindow` with `UIBaseChestWindowData(playerInv,
MultiInventory(player's zone, chest), chestData)` (`ChestInteractionHandler.cs:20-37`).
- The window only reads `chestData.Inventory`, once, in the constructor. It never looks at position or zone
  (`UIBaseChestWindowData.cs:20-37`). The zone argument may be `null` (the game's own test does this,
  `UIBaseChestWindow.cs:198-206`).
- Changes go straight into the live inventory. Nothing is committed on close.
- The window is modal: the game takes control and pauses while it's open (`MainGame.cs:815-824`), so nobody can
  ride off with it open. Opening a chest keeps the keeper mounted (`reference.md`).
- **The game's own chest transfers recreate items** (`InventoryUIItemMoveOpHandler.cs:150-188`): a non-bag item
  is moved as `new Item(id, count)`, so properties such as durability are the game's to lose, in every chest. Not
  our concern, but good to know when comparing before and after.

**Zones: chests feed crafting.** `WorldZoneData.TryAddWgoData` (`WorldZoneData.cs:290-333`, public) registers a
chest in the zone whose rectangle contains its position. `MultiInventory(zone)` then offers its items to
workbench crafting (`WgoData.cs:1406`), build costs (`BuildManager.cs:57`) and zombie workers (`ZombieWgoData.cs`,
many sites). Zone membership is a saved list of ids (`wgoDataList`); **on load it is rebuilt from that list only,
never from positions** (`WorldZoneData.cs:239-282`, `GameSceneData.cs:77-105`). Setting `Position` never changes
the zone.

**Zones also own the pathfinding hole.** `TryAddWgoData` cuts the navmesh at the chest's position at that moment
(`AddCutUnitByBakedData`, `WorldZoneData.cs:354-373`) and `RemoveWgoData` removes the cut (`:375-394`). The
default for every world object is "hole while inside a zone" (`WGODef.ForceSetNavigationHoleType.InsideWorldZone`,
`WGODef.cs:63-68, 243`). So a chest in no zone cuts nothing, and a chest re-added elsewhere cuts a fresh hole
there. No holes are left behind as long as every removal goes through `RemoveWgoData`.

**Building.** From the build menu in a builder zone: `WgoBuildPointer.TryDoBuildAction` takes the cost, then
`new WgoData(id, pos, scene)` and `gameScene.AddWgoData` (`WgoBuildPointer.cs:141-175`). What gets placed is the
**construction site** `<chest>_place`; when its build craft finishes, the game turns the same object into the
finished chest with `WorldData.ChangeWgoData` (the same `WgoData`, re-id'd; it leaves `isRemovingFromData` true
forever, a quirk Move Building also notes). The finished player chests, from the probe's definitions dump
(2026-10-06, `BepInEx/GK2-chest-facts.txt`):

| Finished chest | Slots | Built as | Cost | Remove (`_r`, a destroy craft) refunds |
|---|---|---|---|---|
| `chest_rough` | 20 | `chest_rough_place` | 4 wooden_plank, 4 nails_bronze | 2 wooden_plank, 2 nails_bronze |
| `chest` | 30 | `chest_place` | 4 oiled_plank, 2 wooden_beam, 6 nails_iron | 2 oiled_plank, 1 wooden_beam, 3 nails_iron |
| `chest_good` | 40 | `chest_good_place` | 2 oiled_beam, 6 carved_wood, 8 screw_steel | 1 oiled_beam, 3 carved_wood, 4 screw_steel |

Removing an unfinished site (`<chest>_place_r`) is instant with the full cost back. All three finished chests:
`openInMultiInventory` true, `isMovable` false, `townQuality` 0, no attached script, no NPC group, no reference to
another object's inventory, `conveyorType` None, nav hole "inside world zone", `hp` 0, `dropInventoryOnDeath`
false, no `replaceToWgoOnDie`, no start items, no filters. So every flag the design worried about is clean, and
**the allow-list is `chest_rough`, `chest`, `chest_good`.**

Other chest ids exist and are never touched by this feature: `chest_home`, `chest_kitchen`, `chest_mine`,
`chest_resurrection`, `sawmill_chest`, `yard_chest`, `military_base_chest`, `town_supplies`,
`alchemy_scroll_closet`, `bookshelf_1`, the conveyor chests and pallets (`conveyor_chest*`, `*_container_*`,
`garden_bags_storage_1-3`, `conveyor_wine_beer_pallet`: their own windows and systems, some with a reference to
another object's inventory) and `test_chest*`.

**Scenes:** the probe also logged `disableUnloadOnTeleport` per scene: **`RuinedTemple` (the main outdoor scene) is
kept loaded when the keeper goes through a door**; the other five unload. So indoors the outdoor donkey view
survives, and a carry-through would hit the duplicate view-cache key (§4b) exactly for the main scene.

**Deconstructing.** In remove mode, `Wgo.DoBuildRemove` (`Wgo.cs:2203-2318`) never refuses because of contents.
Normally it queues a `destroy_basic` craft whose output is the refund **plus live references to the items still in
the chest** (`:2276-2309`); the player works it off with a tool, and while it's pending the chest can't be opened
(`CraftInteractionHandler`). Pressing again cancels it. Detect it with `CraftComponent.IsDestroyingCraftActive`
(`CraftComponent.cs:223-233`).

**Saving.** Whole `WgoData` objects, inventories included, are saved per scene in `GameSceneData.wgoDataList`
(`GameSceneData.cs:15`) by Odin binary, synchronously inside `SaveSystem.Save` (`SaveSystem.cs:891-960`: prepare,
serialize, write, callback, all in one call). Nothing at save or load time checks positions or zones, and the
versioned save fixes (`SaveCodeFixes`, `SaveFixer`) run only for saves older than the client and target authored
ids, never player-built chests. Saved per chest and relevant here: `position`, `worldZoneDataId`, `isHidden`,
`isInteractable`. `isTempObject` is not saved.

**There's no item form of a chest.** Any `Item` can hold items (`Item.cs:11-27`), bags and zombie bodies use it,
but no item is a packed building, and mods can't add item or object definitions (the balance is inside
`resources.assets`).

## 2. What the second round corrected

1. **The battle flag is not a precedent for carrying a visible object.** While attached, the flag's own view is
   switched to the part state `"empty"` and a banner is drawn on the keeper instead (`PlayerController.cs:763-771`);
   it's a temporary fight object deleted at fight end. Nothing in the game carries a visible, persistent world
   object by moving it. The game *does* do: hide the real object, show a stand-in. That became the design.
2. **A moved world object stays in its old map chunk.** Static objects are bucketed into chunks once, at
   registration (`ChunkManager.cs:128-165, 435-465`), and nothing re-buckets them. A chest moved across the map
   would be shown or hidden by the chunk it was *built* in. The "ignore culling" flag the flag uses is cleared by
   fights, build mode and animations (`FightingGameController.cs:1394-1407`, `BuildController.cs:252-265`,
   `IdleSMB.cs:16-19`), so it can't be relied on.
3. **Pooled parts leak overrides.** Collider layers and enabled flags set on a view's parts are not reset when
   the parts go back to the pool (`WgoPart.cs:234-306`), so the flag's layer trick would bleed into other chests.
   The design below never touches the real chest's parts.
4. **Remove mode can hit a chest on the cart** if its colliders stay on, because `RemovePointer` treats a
   zone-less object as "in the current zone" (`RemovePointer.cs:419-441`). Hiding the real chest (no active
   colliders) closes this.
5. **A chest saved while riding does not come back as a normal chest without the mod.** It would be zone-less
   forever (point about load above). The save hook in §3 fixes this.
6. **Caretaker zombies keep the chest as a target by id**, saved, and finish a trip they started even after the
   chest left the zone (`ZombieWgoData.cs:371-379, 2744-2790`). Items teleport into or out of the chest at its old
   spot; nothing is lost, but a zombie can get stuck retrying a path. Guard in §4.
7. **Confirmed as assumed:** the window needs only the `Inventory`; zone remove and re-add handle the navmesh;
   `Position` has no other listeners that matter (`WgoData.cs:316-326`: the view, a ground-snap only while a
   movement component moves, and the custom cut only while tracked); no day tick, rot or decay touches chest
   inventories; chunk hiding and scene unload never touch data; the save is synchronous.

## 3. The design: hidden real chest, stand-in on the cart

The keeper builds an ordinary chest with the game's build menu. Throughout, it remains the **same `WgoData`** in
its scene, never cloned, never removed from the scene, its `Inventory` object never replaced.

**Pick up (mounted, facing the chest, cart has room):**
1. Refuse if any guard in §4 says no.
2. `RemoveWgoData` on **every** zone of the scene (the game's own removal does all zones,
   `GameSceneData.cs:321-331`); set `WorldZoneData = null`. The navmesh cut goes with it.
3. `IsHidden = true`. The game's own hide switch (used for fights and workshop conveyors): the view releases its
   parts to the pool and goes inactive (`Wgo.cs:1055-1117`), so there are **no active colliders anywhere**: the
   interaction scan, remove mode and physics can't see it. We touch none of its parts, so nothing leaks.
4. Keep `Position` following the cart each frame (one assignment; the inactive view just moves along).
5. Spawn the stand-in: a temporary `WgoData(chestId)` with the real chest's variation, not added to the scene,
   `Wgo.Spawn(..., ignoreChunkRegistration: true)`, collider objects deactivated, `UpdateChunkVisibility(true)`.
   This is exactly the game's build preview (`BuildController.cs:700-735`) and exactly how our donkey exists
   (`Mount.cs:975-983`). On destroy, a temp object's parts are destroyed, not pooled (`Wgo.cs DestroyParts`), so
   it can't leak either. The stand-in is placed by our cargo layout and follows the bed each frame (kept under the
   scene transform with `UpdatePosByData = false`, not parented under the cart, so the cart's child scans never
   see it). One stand-in per ride, made at pick-up and destroyed at set-down: each one leaves the part pool's
   borrowed count one higher (the game's own build preview does the same, §6), so it is not re-made per frame or
   per redraw.

**On the cart:**
- **Open:** our own interaction opens `UIChestWindow` with `new UIBaseChestWindowData(playerInv, null, chestData)`.
  The window takes exactly what the game gives it. Modal, so the ride can't continue while it's open.
- **Not a crafting source:** zone-less, so workbenches and zombies in the area the cart is parked in don't see it.
- **Fight-level content load** calls `TryAddWgoData` on every object in the scene (`GameSceneData.cs:227-234`,
  from `FightingLevelSystem.cs:249`) and would grab a riding chest into the new zone. A Harmony prefix on
  `TryAddWgoData` returns false for a riding chest.

**Set down (mounted, facing a spot):**
1. Pick the spot as cargo set-down does, then require: the safe-ground reach gate (`safe-ground.md`) passes, and
   **some zone accepts the position** (`TryAddWgoData` would succeed). Outside every zone there's no pathfinding
   hole and no crafting, a state the build menu never allows, so refuse with a message.
2. `Position = spot`; rotation as before.
3. `IsHidden = false`: the view loads its parts at the new position (the root's transform followed `Position` all
   along, `Wgo.cs:1596-1601`).
4. Re-register in the chunk system at the new position, the way **Move Building** does it (GK2QOLMods
   `src/GK2MoveBuilding/Mover.cs`, `Relocate`): the private `Wgo.TryUnregisterInChunkManager` then
   `TryRegisterInChunkManager` through `AccessTools.MethodDelegate`, after `RefreshCustomNavMeshCutUnit()` and
   `Physics.SyncTransforms()`. Move Building moves standing buildings exactly this way (zone remove → `Position` →
   zone re-add by the game's priority loop → chunk re-register) and its in-game test 12, "leave the area and come
   back: the building appears at the new spot", passed on 2026-09-30. Its other steps apply too: cut workbench
   links before and relink after (`DetachLinks` / `RelinkExtensions`; a chest has none unless built against a
   table), re-register baked navigation points if the object has any (`gdPointsData`; chests are expected to have
   none, the probe's step B logs it), refuse while a dock point is occupied or a worker is set.
5. Zone: `RemoveWgoData` on every zone first (duplicate guard: `TryAddWgoData` has no `Contains` check and a
   double add would list the same inventory twice), then the game's loop: zones by descending
   `processingPriority`, first `TryAddWgoData` that returns true (`GameSceneData.cs:306-319`, replicated;
   `WorldZonesByProcessingPriority` is public). This cuts the new navmesh hole.
6. Destroy the stand-in.

**Deconstruct:** set it down and use the game's remove mode. Refund and dropped contents are the game's.

**Save while riding** (`SaveSystem.Save` prefix and postfix, which we already use for cargo):
- Prefix: `IsHidden = false`, `Position` at the cart, add to the zone at that position if one accepts. The save
  then holds an ordinary, visible, zoned chest standing where the cart is, contents included. Without the mod,
  that's simply a chest to open or deconstruct. If no zone accepts, it's a visible zone-less chest, still openable
  and removable (remove mode accepts zone-less objects).
- The side file records "chest `<SGuid>` rides on cart `<owner#id>`" and the chest's set-down facing.
- Postfix: undo, same order as pick-up.
- On load, the mod's restore (after `GameReady`, like cargo reclaim) finds the chest by id near the cart and picks
  it up again. If the side file is missing or stale, the chest just stands there as a normal chest.

**The cart is removed or lost** (`MountRegistry.Remove`, `ForceCleanup`, a failed re-summon, a mod `Dismount`):
set the chest down at the cart's last position with the set-down steps, skipping the reach gate; if no zone
accepts there, set it down zone-less and log it. Never drop its contents.

**Rejected routes, for the record.**
- *Carry the real view by moving it (first-round plan):* chunk membership, the clearable ignore flag, pooled-part
  leaks, remove mode and moving static colliders every frame (§2).
- *A mod-owned virtual chest:* contents would live only in our side file; a lost or stale file loses items.
- *A chest as an item, like a bag:* two conversions, and mods can't add item definitions.

## 4. Item safety: every path that can touch a chest's contents

Traced in full. Nothing in the game drops, clears or replaces a chest's inventory because the chest is in no zone,
at an unusual position, hidden, or has no colliders (`SyncStoredInventoryFilters` only changes the filter property
and never touches items inside, `Inventory.cs:132-199`). The risks are these, each with its guard:

| # | Path | Guard |
|---|---|---|
| 1 | **Cloning the data.** `new WgoData(other)` copies the inventory reference, then `SetDataFromDefinition` replaces it with an empty one (`WgoData.cs:562, 581`). `ChangeId` silently truncates overflow (`:800-821`). `ReplaceWgoData` discards the old inventory (`WorldData.cs:185-192`) | Never construct, copy or change the id of the real chest. Only ever move the one instance |
| 2 | **Removing from the scene.** `RemoveWgoDataFromGameScene` drops nothing; items vanish with the object (`WorldData.cs:160-181`, `WgoData.cs:872-931`) | Never call it on the chest. The design never removes the chest from its scene |
| 3 | **Pending demolish or craft.** The destroy craft holds live item references (`Wgo.cs:2286-2312`); a scene move would silently clear it | Refuse pickup while `CraftComponent.IsDestroyingCraftActive`, `IsStarted` or a queue exists. While hidden the chest can't be marked (no colliders) |
| 4 | **Workbench extension cascade.** A chest attached as a full-cover extension is dropped and removed when its parent is demolished, wherever it is (`WgoData.cs:1955-2000`) | Refuse pickup while `WorkbenchParents` or `AttachedWorkbenchExtensions` is non-empty |
| 5 | **Death logic.** HP death with `replaceToWgoOnDie` or without `dropInventoryOnDeath` removes without a drop (`WgoData.cs:2072-2150`) | Probe `hp`, `dropInventoryOnDeath`, `replaceToWgoOnDie` for the three ids. Hidden objects take no damage anyway |
| 6 | **Double zone registration** (no `Contains` check) and the fight-level zone grab. **Found by the self-test:** `RemoveWgoData` takes one list entry per call (`List.Remove`), so a duplicate survives a plain remove-then-add, and the game's own scene removal leaves one orphan id in the zone list | Remove from a zone *until it holds no entry* (`ChestProbe.ZoneRemoveAll`), then add; prefix `TryAddWgoData` for riding chests |
| 7 | **Caretaker with a stale target** finishes its trip into or out of the chest by id | Before pickup, scan `MainGame.ZombieSystemData` for a caretaker or transporter whose target id is the chest; refuse with "a zombie is using this chest" (or wait). Nothing is lost either way, this only avoids a stuck zombie and surprise transfers |
| 8 | **Fallback drops are lossy.** `DropSystem.DropItem` destroys fuel items outright, splits stacks into recreated items, converts tech points; corpses on the ground auto-expire (`DropSystem.cs:9-80`, `DropData.cs:175-181`) | The design never drops chest contents. Every failure path sets the whole chest down instead |
| 9 | **Saved flags.** `isHidden` and `isInteractable` are saved | Never use `IsInteractable`. Undo `IsHidden` in the save prefix (§3) |
| 10 | **A chest with `hasRefToOtherWgoInventory`** shows another object's inventory | Probe; such ids are never allowed |
| 11 | **Quest or script removal.** Expressions remove objects by custom tag or as their own scope; chest defs with an `attachedScript` or delayed events could | Probe `attachedScript`; only ids without one are allowed |
| 12 | Co-op: `Position` writes and zone changes aren't replicated | Out of scope, as for all of the mod |

**Two independent reviews of the built code (2026-10-06)** added these, all built:
- **A hidden chest in a save is the one unrecoverable state** (nothing in the game un-hides a chest; the view spawns inactive and collider-less). So there are now three lines, each on its own: (1) the save hook stands every riding chest at its cart, each instance in its own try, chests before cargo; (2) a prefix on `GameSave.PrepareToSave`, the last call before the game serializes, un-hides any allowed chest still hidden, loudly ("please report this line"); (3) after a load, `RepairHiddenChests` shows any allowed chest found hidden that no cart owns, and the restore lifts a hidden chest it recognises as a repair instead of refusing it.
- **The rig owns the chest before anything changes** in a lift; on a throw the chest is shown and zoned again where it stands. A set-down that throws after the chest is shown completes the bookkeeping, so a visible chest is never dragged along.
- **Hitching hands the chest over first**, before the view is re-made or anything can fail (a failure used to leave a hidden chest on an unregistered instance that no save hook visits).
- **The zone guard the design asked for exists** (`WorldZoneData.TryAddWgoData` prefix: a riding chest is refused, except for the mod's own adds), and every set-down removes before it adds.
- **The open-from-cart patch never falls through** to the game's handler on a stand-in (that would open the stand-in's own empty inventory; items put there would be nobody's), and a dropped stand-in is non-interactable at once.
- **Only the player's own door press is refused.** A scripted teleport (a quest flow, a fight, a cutscene) has already taken control and needs the teleport to finish; refusing it froze the keeper. Those set the chest down at the cart and go ahead.
- The save stands the chest at the cart **data-only** (shown, zoned), the rig keeps it, the stand-in and the 0 slots stay; the postfix hides it again, and the next tick does so if the postfix never ran. Ground height is corrected only in the keeper's own scene. `Restore` requires a cart. The reclaim ring allows for the cart body behind the donkey root and logs every refusal.

**A third review (2026-10-09, of the code as fixed) found no loss or duplication path and eight hardening points, all
built:** one cart per chest (the restore refuses a chest another cart already carries, and each chest GUID is
reclaimed once per load, so a dormant record from an uninstalled mod plus a live one can't both claim it); the
backstops only treat a chest that is hidden **and in no zone list** as "our ride that never ended", since the game's
own staging (fight stages, scripts) hides objects but leaves them in their zones; the lift's rollback returns the
chest to where it stood, not to the cart; the save's stand-at-cart marks the take-back before anything can throw and
removes before it adds; the tick re-hides a riding chest found shown outside a save; the after-load repair has its
own try; the teleport prefix asks the game's `CanTeleport` first. Verified safe by that review, with method: the
synchronous save order (prefix → `PrepareToSave` with our un-hide → serialize → write → success callback with the
side file → postfix/finalizer take-back), the 0-slot inventory (`Item` inserts only while fill ≠ size, and the
shrink only ever happens empty), the mod-less save fields, the save-failure branches, hitch and unhitch, and every
instance exit path reaching `LetGo`.

Vanilla behaviour that looks like loss but isn't ours: the game's chest UI recreating items (§1), sawmill zombies
discarding output when the zone has no space (`Flow_FinishZombieSawmillCraft.cs:36-50`), which happens more often
when a chest leaves the zone.

### 4a. Zombies and the chest (third round)

Only two zombie roles hold a reference to a chest; all others take and deliver within one frame from the zone
list and keep nothing (`ZombieWgoData.cs`, porter 3031-3043, gardener 3603-3614, crafter 1937-1959):
- **Caretaker:** `caretakerCurrentTargetUniqueId` (saved), resolved through the global cache (371-381), path end
  snapshotted at trip start (2734-2763), and on arrival it adds to or removes from `CaretakerCurrentTarget.Inventory`
  with no zone, distance, hidden or interactable check (2352-2418, 2772-2875). A failed path retries the **same**
  target every second forever (2152-2155, 2721-2728), re-computed from the chest's current position, so the zombie
  would follow the cart. No zombie code reads `IsHidden` on a chest.
- **Conveyor transporter:** the same, for chests in the `conveyor_storage` zone only, add-only (4321-4334).
- **Nothing lost in any case:** the carried item is saved on the zombie and only ever dropped by
  `UnAttachFromWgoData` (1012-1016). But the player would see items appear in or vanish from the riding chest, and
  removing the chest from the global cache or changing its id while targeted throws in `MoveAction` (2736, 2746).
- No zombie subscribes to zone events, so zone removal alone never makes them re-plan.

**Decision: refuse pickup while a zombie targets the chest.** Detection is public API only, on the same frame as
the zone removal, so nothing can start targeting in between:
```
foreach (SGuid id in MainGame.ZombieSystemData.zombieOnSceneWgoIds)
    z = MainGame.ZombieSystemData.GetZombie(id);
    busy |= z?.ZombieType == Caretaker && z.CaretakerCurrentTargetUniqueId == chest.UniqueId
         || z?.ZombieType == ConveyorTransporter && z.ConveyorTransporterCurrentTargetUniqueId == chest.UniqueId;
```
Message: "A zombie is using this chest." Cancelling the trip instead is possible but needs private methods in the
right order per state (`CaretakerAbortPickUpFromMissingInventory`, `CaretakerTryMoveToNearestInventoryToPutPortableItem`)
and a bare `ForceStop` or an empty target strands the zombie; kept as a later fallback for a zombie stuck on a
walled-in chest. **Also refuse while build mode, the building window or any chest window is open**: those hold
the zone's `MultiInventory` for the whole session (`BuildManager.cs:53-66`, `UIBuildingWindow.cs:75, 93`,
`ChestInteractionHandler.cs:20`) and would keep spending from the riding chest. Set-down needs no refresh:
caretakers poll the zone list every frame (2128-2133) and the order helpers read live.

**Game 1.009 (2026-10-06, `game-update-25731225.md`):** the findings above were made against 1.008 and were
re-checked against the 1.009 diff. `UnAttachFromWgoData` now unsubscribes the caretaker from the zone's
`OnOrderRemoved` (the double-subscription quirk is gone); `CaretakerState`'s setter only adds a widget redraw; the
caretaker's `MoveAction` change concerns carrier stations only. The new save fixes (`< 1009`) touch no world objects
or inventories. The caretaker station's new "[E] Take" of an away-working caretaker is a head path (covered by the
zombies-on-cart station patch), not a chest matter.

### 4b. Doors to another scene (third round)

**Left alone, the chest is left behind:** its data stays in the old scene's list, hidden and zone-less, and no
game code ever moves it. The game itself never moves a `WgoData` between scenes directly; a carried zombie is
removed from the scene into a store and re-added elsewhere (`ZombieSystemData.cs:107-136`), which runs the full
`OnRemove`, and the game then re-creates the zombie's script by hand.

The teleport (`PlayerController.Teleport`, `PlayerController.cs:587-703`): `CanTeleport` → controls off → fade →
unload the old scene (unless `GameSceneConfig.disableUnloadOnTeleport`) → load the new one → `GameScene.Start`
(sets the keeper's scene id at its start, 174-175; views spawned from a copy of the list at 446; `OnWgoDataAdd`
subscribed at 188) → `OnPlayerTeleported` → controls back. All scenes' data are always in memory with one shared
`WgoDataCache`; zones of an unloaded scene accept `TryAddWgoData` and their navmesh cut units live in one global
pool, so adding a cut in an unloaded scene is the normal state of every placed object there.

| Option | How | Risks |
|---|---|---|
| **Refuse the door** (chosen for v1) | Harmony prefix on `Teleport`, `__result = false`, before `CanTeleport`, when riding with a chest aboard and the destination scene differs. Message: "Set the chest down before going through." Same-scene teleports pass | Fewest unknowns. Map fast travel (`MapPageWidget.cs:300`) closes the map and runs its expression regardless of the result; a door script may have played a sound first |
| Set down at the door | In the same prefix, while the old scene is still loaded: the normal set-down at a clear spot near the keeper | The chest could block the doorway; fast travel would leave it far away |
| Carry through (later) | After the new scene started (`OnPlayerTeleported`, scene id changed, old scene unloaded): remove from zones → `oldScene.wgoDataList.Remove` → `Cache.RemoveWgoDataFromCache` → reset GD points (`RewriteGdPointsData(empty)`, `gdPointsRegistered = false`) → `newScene.AddWgoData(chest)` (public; auto-zones) → remove from that zone again. Never `MoveWgoDataToAnotherGameScene`: its `OnRemove` loses town quality, scripts and GD points for good | If the old scene stays loaded (`disableUnloadOnTeleport`, asset data, check at runtime) the view cache throws on the duplicate key; the `Start` race only on the 30 s timeout |

Carry-through is viable and documented for a later version. v1 refuses the door.

## 5. Edge cases

| Case | Behaviour |
|---|---|
| Sleep (the save) while the chest rides | §3 save hook: the save holds a normal chest at the cart; restored onto the cart after load |
| Quit without saving | The last save is untouched and had the chest wherever it was then |
| Doors to another scene | **v1: the door is refused while a chest is aboard** ("Set the chest down before going through"), by a prefix on `PlayerController.Teleport`. Carrying it through is viable later with the manual route in §4b |
| Map fast travel with a chest aboard | The same prefix refuses it; the map closes anyway (the game's behaviour) |
| Build mode, building window or a chest window open | Pickup refused (§4a): they hold the zone's inventories until closed |
| A zombie on its way to or from the chest | Pickup refused: "A zombie is using this chest." (§4a) |
| Dismount for any reason, API summon, `Remove` | The chest stays on the standing cart (it's cart cargo, like the rest). When the cart itself goes, it's set down at the cart |
| Death of the keeper | A dismount; as above |
| Two chests | One per cart in v1 |
| Chest plus other cargo | Never: load refused while cargo is on the cart ("Set the cargo down first"), and cargo pickup refused while a chest rides (the cart counts as full, as with two items). Items from the cart's own inventory are untouched: the chest is not an item in it |
| Set-down spot has no zone, fails the reach gate, or is inside something | Refused with a message; the chest stays on the cart |
| Chest marked for demolish, mid-craft, a workbench extension, a zombie's target | Pickup refused with the reason |
| Chest window open when the ride would move | Impossible: the window is modal and pauses the game |
| Mod removed with a chest riding in the save | The save already holds it as a normal chest at the cart (§3) |
| Side file stale or missing | The chest stands at the cart as a normal chest; nothing to restore |
| Game update changes chest definitions | The allow-list is by id; the probe values are logged at start and a mismatch disables the feature with a warning |
| Bags inside the chest | Ride along by reference; the window shows them as the game does |
| Fight level loads a zone over the cart | The `TryAddWgoData` prefix keeps the riding chest out |
| Wild or working zombies near the chest | Unaffected; they only see zone chests |

## 6. What only the running game can settle (step 0 probe)

**Status 2026-10-06:** the definitions dump (1) is done, see §1. Step 2 passed by hand (the chest moved 4 m east,
south and north, drawn and openable at each new spot, and back; the first "disappearance" was the chest moved
behind the yard's north wall). Steps 2 to 5 are now **self-tests** ("Run self-tests" on the Debug tab,
`Dev/SelfTest.Chest.cs`): each scenario builds its own empty `chest_rough` beside the keeper, runs the move through
`ChestProbe`'s pieces, checks, and removes the chest again, so no building, walking or reload is needed:
- *chest: hide, move, show (and off screen)*: data and view at the new spot, parts drawn, registered, held by a
  chunk around the new spot, zone untouched; the keeper 45 m away and back, the view drawn again; moved back.
- *chest: zone out and in*: with an item inside: out = no list entry, not a crafting source
  (`MultiInventory(zone)`), no navigation cut (`GlobalNavigationManager.usedUnits`); in = the same zone by the
  game's order, one entry, source and cut back, the item the same object; a bare second add shown to duplicate and
  remove-all-then-add shown to put it right.
- *chest: ten stand-ins*: each drawn, no active collider, not interactable, never in the scene list; town quality
  and the pool's idle parts unchanged; the pool's borrowed count reported (a temp object's parts are destroyed,
  not returned: the game's build preview does the same), the chest's colliders logged.
The manual buttons stay for looking at things by eye.

**Second run (2026-10-06, after the fixes below): 106 passed, 0 failed, 0 warnings.** Step 0 is complete. (A later run
with the test chest standing on a chunk boundary showed it held by two chunks, which is how the game registers a
straddling object; the chunk check now accepts any holding chunk around the spot.)

**First run (2026-10-06, game 1.009):** 102 passed, 4 failed, all 4 in "zone out and in", two of them the test's own
mistake and two a real finding:
- *hide, move, show:* all passed. The moved view is held by one chunk around its new spot (chunk centre (0, 8.7, 10)
  size 18×10.5×10 for a chest at z 12); with the keeper 45 m away the view is culled, back near it is drawn again;
  moved back, drawn and registered.
- *zone out and in:* lists, crafting source and navigation hole all follow, in both directions, back into the same
  zone with one entry. **Finding:** `RemoveWgoData` takes one list entry per call, so after the deliberate double
  add the remove-all-then-add left two entries, and the game's own scene removal then left an orphan id in the
  zone list (§4 row 6; `ZoneRemoveAll` now loops until no entry is left). The two "same object" checks compared
  with the `Item` handed to `AddItemToInventory`, which the game recreates for a stackable item; the stored object
  is what must stay the same, and the test now captures that one.
- *ten stand-ins:* all passed. Town quality unchanged (10), the pool's idle parts untouched (0). **The pool's
  borrowed count rose by one per stand-in** (4 → 14): a temp object's parts are destroyed, not returned, so the
  game's `NoteReturned` never runs (its own build preview leaks the same way). It only lowers the save-load pre-warm
  of that prefab for the rest of the session, but the design keeps **one stand-in per ride**, made at pick-up and
  destroyed at set-down, so the drift is one per ride.
- *colliders of `chest_rough`:* two orientations, `Horizontal` (active) and `Vertical` (inactive), each with
  `Base/Obstacle` (layer 8, solid), `Interactable` (layer 6, trigger, the one the interaction scan hits), four
  `DockPoints` (layer 7, triggers) and a `Build Collider` (layer 19, trigger, what remove mode hits). Hidden, none
  is active: the design's "no colliders anywhere" holds.

A dev button logs, for every `WGODef` with `interactionType == Chest` or `inventorySize > 0`: `inventorySize`,
`OpenInMultiInventory`, `isMovable`, `townQuality`, `attachedScript`, `npcLifeSimGroup`, `hasRefToOtherWgoInventory`,
`refToOtherWgoInventory`, `conveyorType`, `forceSetNavigationHoleType`, `hp`, `dropInventoryOnDeath`,
`replaceToWgoOnDie`, `transferDataToNewWgo`, `startItems`, and the matching build defs (`_p`, `_r`: `builds_in`,
costs, refunds, `instant_destroy`). The allow-list ships from that log. (UnityPy can't read the balance asset: the
type tree fails on it, tried 2026-10-06.)

Then, on a test chest the user builds and empties:
1. **Hide and show:** `IsHidden` true, move `Position` 20 m, `IsHidden` false, chunk re-register; the chest shows
   at the new spot, from near and far, after walking away and back. If not, the `CreateWgoView` fallback.
2. **Zone remove and re-add** at the new spot: the chest is a crafting source there (open a nearby workbench) and
   no longer at the old spot; villagers path around it at the new spot and through the old one.
3. **The stand-in:** spawned and destroyed ten times; no `[pool]` leftovers in the DonkeyPrefab audit, no
   stray town quality (log `townSystem.Quality` before and after).
4. **Save hook:** sleep with the chest riding; the save, loaded with the mod disabled, shows a normal chest at the
   cart's spot, openable, with its items.
5. **Collider layers per child** of the chest prefab (for the record; the design doesn't depend on them).

## 7. Decisions (the user, 2026-10-06)

1. **Keys: agreed, to be tested.** E on a chest is the game's "Open" and stays. The Action key (F) is free on
   chests (the handler never overrides `HasInteraction2`): **F facing a chest while mounted = load it; F facing
   the chest on the cart = set it down; E facing the chest on the cart = open it.**
2. **Space on the bed: the chest takes the whole cart.** No other cargo while a chest rides, and no chest while
   cargo is on the cart; one position to tune. Pair entries (chest + another item) could come later, the layout
   code already handles pairs.
3. **Doors: refuse the door in v1** (§4b). Carry-through later.
4. **Zombie guard: refuse pickup while a zombie targets the chest** (§4a).
5. **Where it lives: the Donkey API does all the work** (pick-up, hide, stand-in, set-down, save hook, guards,
   door prefix); Saddle Up gets the toggle and labels, as with zombies.

## 6a. What a game update, a broken mod or a removed mod does to a chest

The question the design is built around: **nothing the mod can do, break or stop doing may touch the player's chest.**

- **The chest is never the mod's.** It is the game's own object with the game's own inventory from first to last;
  the mod only hides it, moves its position and takes it in and out of zones. There is no mod-owned copy of any
  item, so there is no state to migrate and nothing a missing mod could fail to give back.
- **A save always holds a normal chest.** Before the game writes: the save hook stands each riding chest at its
  cart (shown, zoned); a prefix on the game's `PrepareToSave` (its last call before serializing) un-hides any
  allowed chest still hidden; the side file only *names* the chest. A save made with the mod is therefore a save a
  mod-less game reads as "a chest standing by a cart". Remove mode takes it down normally, zoned or not.
- **A game update disables the mod before it patches anything** (`Safety.DisableOnGameUpdate`, the audited MVIDs):
  the game runs unmodified, the chest stands where the last save put it. If the player forces the mod on
  (`DisableOnGameUpdate=false`), **the chest feature alone stays locked** until the mod is re-audited, because it is
  the one feature that writes saved state; everything else of the mod runs as the player asked.
- **The members the feature reaches by name** (`Wgo.TryRegisterInChunkManager`, `TryUnregisterInChunkManager`,
  `WGOInteractionHandlerBase.assignedWgo`) are checked at start with the rest (`Access.Verify`): gone, the whole
  mod stays off with a clear log line instead of a chest operation throwing later. The build-time scan checks
  every Harmony target by name as well.
- **At run time the feature is its own fail-safe:** any chest operation that throws (lifting, setting down, the
  save's stand-at-the-cart or take-back) puts the chest back where it was, then **locks the feature for the
  session** and sets every riding chest down at its cart, with a bubble and an error line. A broken mod can hurt
  at most once, visibly, and never silently.
- **After a load**, before anything else, every allowed chest found hidden that no cart owns is shown and zoned
  again (`RepairHiddenChests`), using only the game's most basic calls; it runs in the locked state too.
- **Where a chest can ever be left standing:** every fallback (the save, a removal, a scene change, the lock) uses the
  cart's own spot, which is ground the keeper drove onto (the donkey follows his ground rules; `SafeGround` keeps it
  out of water and off ledges). Checked in the code, not assumed (2026-10-09): the park check (`SafeGround.cs`
  `KeepOnGround`) moves a parked donkey until its body *and* its cart point are on ground, reachable and on the
  walk grid, but skips that when the keeper has just gone through a door ("stays where it stood"); while riding, the
  root is placed from the keeper with an offset and ground snapping is a setting (`SnapToGround`), so along a bank
  the root, and the cart point behind it, can be over water. Hence `FallbackSpot` tries three spots in turn: the
  cart point on ground, else the donkey's root on ground, else the game's own drop-spot search from the root
  (`SpecialPhysicsCastUtils.GetPlayerDropPosition`, which returns ground only: no water, no other drops), and only
  then the cart point with a warning. `GroundHeight` (`Mount.cs:272`) answers NaN for water and for no ground.
  Zone or no zone, the chest can be opened from beside it (the interaction scan filters only by `IsInteractable`,
  `PlayerInteractionComponent.GetWgoTargetsFromColliders`) and taken down with the game's remove mode
  (`RemovePointer.IsFromCurrentWorldZone` returns true for an object in no zone; `Wgo.IsBuildRemovable` asks only
  for a remove definition, which all three chests have).
- **What a mod-less game sees that it wouldn't otherwise:** a chest standing where a cart stood (inside a zone when
  one accepted the spot, on walkable ground, not on the build grid), the same kind of thing Move Building leaves.
  The game's own save fixes target authored ids and never player chests (checked in 1.008 and 1.009).

## 7a. Release checks (the worst case, empirically)

Besides the self-tests ("chest: the backstops" exercises the pre-save pass, the after-load repair and the zone
guard directly, since in normal play none of them ever runs), two checks only the real game can give:
1. **The mod removed with a chest riding in the save.** Automated in substance by the self-test *"chest: the save as
   the game writes it"*: with a chest riding a test cart, the real save steps run, the game's save object is
   serialized with the game's own serializer in memory and read back into a separate copy, and the chest **in that
   copy** (what a mod-less game loads) is checked: not hidden, interactable, the same items, standing by the cart
   on ground, in a zone's list; then the take-back, and the chest rides again. The same scenario trips the
   fail-safe lock with a chest aboard (set down at the cart, pickups refused) and undoes it. By hand, for the eyes:
   sleep with a chest aboard, close the game, rename `BepInEx/plugins/GK2DonkeyAPI.dll` and `SaddleUp.dll` to
   `.off`, start the game, load: the chest stands beside where the cart stood, openable, with its items; remove mode
   takes it down normally. Rename back, load again: the chest is lifted back onto the cart (`restored with the
   save`). Status: automated check **built, not yet run**; by hand **pending**.
2. **The caretaker refusal** ("A zombie is using this chest."): needs a working caretaker. Status: **pending**.

## 8. Plan

0. **Probe** (§6): **done 2026-10-06.** Definitions dumped (`BepInEx/GK2-chest-facts.txt`), the moves passed by hand
   and as self-tests (106/106 on the second run). Findings folded in: the allow-list (§1), the one-entry-per-call
   zone removal (§4 row 6), one stand-in per ride (§3). Dev code: `Dev/ChestProbe.cs`, `Dev/SelfTest.Chest.cs`.
1. **Pick up and set down in one scene: built 2026-10-06** (`CartChest.cs`, API 0.23.0, behaviour key `CargoChest`,
   Saddle Up toggle "A storage chest rides on the cart"). No stand-in yet, so the chest is invisible while it rides.
   - **Keys:** while driving, F on a chest = "[F] Load onto the cart" (the chest's own handler gains the Action key
     through three patches; the refusal reason is a thought bubble); E with nothing in front = set it down where
     cargo would go (behind the cart with `CargoSetDownBehindCart`, else in front), when the spot is in the keeper's
     sight and reach (`SafeGround.PlaceBlocked`) and some zone accepts it, else "That spot is outside any building
     area." and it stays aboard.
   - **Whole cart:** the cart's inventory is shrunk to 0 slots while the chest rides, so every "room on the cart"
     question (the head gate, the stations, the hints) says full; the size comes back at set-down. Loading a chest
     is refused while cargo is on the cart.
   - **Guards** (§4): the allow-list; hidden or temp objects; another scene; co-op; a pending destroy craft or any
     craft; workbench links; a worker or an occupied dock point; build mode or the building and chest windows open;
     a caretaker or transporter targeting it.
   - **Fallbacks, all "set down at the cart"** (shown, zoned if a zone takes the spot, else zone-less and logged):
     the save prefix (the save always holds a normal chest; taking it back after the save is step 4), the summon's
     dismount and cleanup, a standing cart removed or cleaned up, a scene change (the chest stays in the scene it
     came from, where the cart stood).
   - **Hitch and unhitch** hand the chest over to the other instance with the cart.
   - **Self-test "chest: the ride (step 1)"** (`Dev/SelfTest.Chest.cs`): a test donkey with its cart is ridden; the
     chest's handler offers the action key with the "Load onto the cart" row; the press lifts the chest (hidden,
     zone-less, no navigation hole, the cart at 0 slots and the head gate saying no, contents the same object,
     position following the cart); refusals for cargo aboard and for a sawmill chest; set down where cargo goes
     (shown, zoned, drawn, the cart's slots back); the chest stays on the parked cart, goes with the cart through
     unhitch and hitch (the re-made inventory at 0 slots); the save hook and the cart's removal set it down at the
     cart. Not automatable: a door, a zombie walking to the chest, the real key presses and the thought bubbles.
     **Passed 2026-10-06** (132/132 for the whole suite). A first start had failed: the two Action-key patches named
     `ChestInteractionHandler`, which inherits `HasInteraction2`/`Interact2`; Harmony found no target and the API
     disabled itself. They sit on `WGOInteractionHandlerBase` now, and `CecilScan-DonkeyAPI` checks every string-named
     patch target is declared on its type.
   - **By hand, 2026-10-06, passed:** pick up and set down with 4 then 5 items (the same items after every move);
     "(not now)" and "Set the cargo down first." with cargo aboard; no "[F]" on the home chest; a full-cart ground
     pickup drops the item in front of the keeper as it always did; sleeping with the chest aboard (Saddle Up
     dismounts at the door first, the chest stays on the parked cart, the save hook sets it down at the cart: `set
     down at the cart (save)`); unhitch and hitch with the chest aboard, then set down; the Saddle Up toggle. Not
     reachable with Saddle Up: the scene-change fallback (it keeps "stay mounted through doors" off). Not tested: a
     caretaker heading to the chest (the user had none working).
   - **To test in game by hand:** (a) build a chest, put a few items in, drive up in Cart style, F: the chest vanishes, the
     log says `Chest: … lifted onto …`; a ground item then shows no "[E] Take" (cart full); E with nothing in
     front: the chest stands where cargo would, openable, same items, `… set down …` in the log; (b) the refusals:
     F with cargo on the cart ("Set the cargo down first"), F on the home chest or a conveyor chest (nothing offered),
     F while a caretaker heads to it; (c) with the chest aboard: sleep (the chest stands at the cart after the save),
     unhitch and hitch (the chest follows the cart), a door (the chest stands where the cart was, in the old scene),
     dismount the summon; (d) the Saddle Up toggle "A storage chest rides on the cart" off: no "[F]" on chests.
2. **The stand-in on the bed: built 2026-10-06.** A copy of the chest's look (`ChestMoves.SpawnStandIn`, the build-preview
   technique) made once per ride at pick-up, parented under the cargo holder so it sways and pitches with the cart
   and goes with the holder when the view is released (`Cargo.Rig.Detach` → `ChestRig.OnHolderGone`); destroyed
   at set-down and at hand-over; re-made on a re-made view (`OnCartInventory` → `RefreshViews`). Placed by the cargo
   packer's pass (`Cargo.Rig.RefreshViews` → `ChestRig.Layout`): bottom on the cargo floor, at the chest id's
   `[item]` entry (alone place, else base), its box measured from its meshes (else its renderers), listed in
   `Cargo.Placed` so the Riding tab's "One item on the cart" sliders tune it like any lone cargo item. The chest
   ids are a layout class each, matched by id only. Self-test additions: the stand-in drawn under the holder, no
   active collider, temp and not interactable, listed alone with a measured box; gone at set-down; back on the
   re-made view after hitching.
3. **Open from the cart: built 2026-10-06.** While mounted the keeper can never face the cart (it is always behind the
   donkey), so this is an on-foot action at the standing cart, like loading on foot: once its parts are loaded the
   stand-in gets its interactable trigger back (layer 6, the one the keeper's interaction scan hits; the solid,
   build-area and dock-point colliders stay off) and its temp data is made interactable, so the game's own chest
   handler offers "[E] Open" on it. `ChestInteractionHandler.Interact` is patched to open `UIChestWindow` over the
   **real** chest's inventory (no zone list) when the handler's object is a stand-in (`CartChest.RigOfStandIn`), and
   the Action-key patches offer "[F] Set down" on a stand-in, which runs the ordinary set-down from the keeper's
   position.
4. **Save and restore: built 2026-10-06.** The save hook sets the chest down at the cart and remembers it
   (`lentChest`); the side file gets a `chest <guid>` line on that instance; `TakeCargoBackFromSave` lifts it again
   right after the save (so sleeping no longer ends the ride); after a load, `RestoreFromSideFile` finds the chest by
   GUID within 3 units of where the cart stood and lifts it (`Rig.Restore`: the player's guards skipped, only "not
   one of the three", hidden, or a pending destroy craft still refuse). The side-file round trip and the take-back
   are in the ride self-test. **Hand-tested 2026-10-09 with 19 items inside:** open from the cart and set down on
   foot; sleeping (`stands at … for the save`, `rides on … again after the save`); quit and load (`restored with
   the save onto …`, `1 chest(s) back on their carts`); the same 19 items at every step. Self-tests 139/140, the one
   failure a test building the side text from the kept list, which the test cart isn't in (fixed).
5. **Doors: built 2026-10-06, changed 2026-10-09.** Two facts from the game decided it: the house (and every indoor
   room of the main map) is a **same-scene** teleport (`from RuinedTemple to RuinedTemple, indoors` in the log), so
   nothing needs doing there: Saddle Up dismounts at the door and the chest rides on with the parked cart, hidden,
   like cargo; and the game's cross-scene doors are **scripted** teleports that take control before calling
   `PlayerController.Teleport`, so a refusal there couldn't be told from a cutscene's teleport and would have left
   the keeper frozen. So the prefix on `Teleport` no longer refuses: a teleport to another scene **sets the chest down
   at the cart** (shown, zoned) and goes ahead, with the bubble "The chest stays by the cart." The scene-change
   fallback stays as a safety net. Open: API events and docs/reference entries; the tuning of the chest's place.
