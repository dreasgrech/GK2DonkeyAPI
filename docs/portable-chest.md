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
`new WgoData(id, pos, scene)` and `gameScene.AddWgoData` (`WgoBuildPointer.cs:141-175`). The player-built chests,
read from the balance data by string search (costs confirmed, the rest needs the probe):

| Build def → object | Cost | Refund (`_r`) |
|---|---|---|
| `chest_rough_place_p` → `chest_rough_place` | 4 wooden_plank, 4 nails_bronze | not decoded |
| `chest_place_p` → `chest_place` | 4 oiled_plank, 2 wooden_beam, 6 nails_iron | `chest_place_r`: in full |
| `chest_good_place_p` → `chest_good_place` | 2 oiled_beam, 6 carved_wood, 8 screw_steel | not decoded |

Other chest ids exist and are never touched by this feature: `chest`, `chest_home`, `chest_yard_place`,
`chest_garden_place`, `chest_graveyard_place`, `chest_mine`, `chest_kitchen`, `sawmill_chest`, `yard_chest`,
`military_base_chest`, `looters_chest(_full)`, `chest_myst`, `chest_resurrection`, the conveyor chests
(`conveyor_chest*`, `garden_bags_storage_1-3`, `conveyor_wine_beer_pallet`, which have their own windows and
systems) and `test_chest_size_2/4`.

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
   see it).

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
4. Re-register in the chunk system at the new position: `ChunkManager.UnregisterStaticChunkableObject` then
   `RegisterStaticChunkableObject` (both public; `Wgo.RegisteredInChunker` is settable). Fallback if the probe
   shows a problem: despawn the view and recreate it through the scene's own `CreateWgoView` (private, via
   AccessTools), which is how a freshly built chest gets its view.
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
| 6 | **Double zone registration** (no `Contains` check) and the fight-level zone grab | Remove from all zones before any add; prefix `TryAddWgoData` for riding chests |
| 7 | **Caretaker with a stale target** finishes its trip into or out of the chest by id | Before pickup, scan `MainGame.ZombieSystemData` for a caretaker or transporter whose target id is the chest; refuse with "a zombie is using this chest" (or wait). Nothing is lost either way, this only avoids a stuck zombie and surprise transfers |
| 8 | **Fallback drops are lossy.** `DropSystem.DropItem` destroys fuel items outright, splits stacks into recreated items, converts tech points; corpses on the ground auto-expire (`DropSystem.cs:9-80`, `DropData.cs:175-181`) | The design never drops chest contents. Every failure path sets the whole chest down instead |
| 9 | **Saved flags.** `isHidden` and `isInteractable` are saved | Never use `IsInteractable`. Undo `IsHidden` in the save prefix (§3) |
| 10 | **A chest with `hasRefToOtherWgoInventory`** shows another object's inventory | Probe; such ids are never allowed |
| 11 | **Quest or script removal.** Expressions remove objects by custom tag or as their own scope; chest defs with an `attachedScript` or delayed events could | Probe `attachedScript`; only ids without one are allowed |
| 12 | Co-op: `Position` writes and zone changes aren't replicated | Out of scope, as for all of the mod |

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

## 8. Plan

0. **Probe** (§6): **built and deployed 2026-10-06** (`Dev/ChestProbe.cs`, Debug tab, section "Chest on the cart:
   step-0 probe"; log lines are tagged `[chest]`, the definitions go to `BepInEx/GK2-chest-facts.txt`). Nothing
   else is built before its results are in.
1. Pick up and set down in one scene, with all §4 guards, no stand-in yet (the chest is simply invisible while it
   rides), no saving.
2. The stand-in on the bed and the layout entry (box measured from the stand-in).
3. The open-from-cart interaction.
4. Save hook, side file, restore after load, lost-cart paths.
5. Doors as decided, API and settings, docs and tests.
