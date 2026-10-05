# Zombies on the cart (investigated 2026-10-04; nothing built)

The user has unlocked zombies (two stand near the house in their save) and wants them on the cart:
- **two at once**;
- **positions fully configurable against every other item**, like all cargo (item entries, alone and top offsets,
  tuned pairs, the panel, the cargo sheet);
- **an on/off setting, on by default.**

Today the cart refuses them (`Cargo.Allowed` refuses item group `zombie`) and they stay on the keeper's head.

Sources: decomp 1.008, our code, and an offline read of the game's asset bundles (UnityPy; scripts and dumps in the
job's tmp folder, `dvam.json`, `pitem.txt`, `el.txt`).

## Pending zombie tests (from before the unlock)

| Where | Test |
|---|---|
| [baseline-test.md](baseline-test.md) 13 | Cart full (2/2), pick up a zombie: it goes onto the head as in the normal game, nothing duplicated or dropped twice (no `LOST`). |
| [baseline-test.md](baseline-test.md) 14 | Cart full, a third item: it's put down in front of the keeper. Listed under zombies, but any item works; can run now. |
| `PROBE-FINDINGS.md` (hands-locked rule) | A zombie picked up while riding goes to the head and is set down the next frame. Unconfirmed: whether the game lets a zombie be dropped that way. |
| `PROBE-FINDINGS.md` (ground refusal) | While mounted, a zombie lying on the ground shows no outline and E does nothing. |
| [saddleup-scope.md](saddleup-scope.md), API README | The refusal message. |

With zombies allowed on the cart, 13, the hands-locked test and the refusal message become "onto the cart" tests (Ride
style keeps the refusal).

## 1. How the game carries a zombie

- **A carried zombie is an item.** `body_zombie`, item group `zombie`, `ItemDef.isLinkedToWgo`. The item's
  `UniqueId` is the zombie's GUID. Its nested inventory holds the organs, the collar, the tool, the weapon and the
  armour.
- **While carried,** the `ZombieWgoData` sits in `ZombieSystemData.zombieDrops` (the store); `zombieItem` is its
  link to the item. Stored zombies don't tick: the systems loop over `zombieOnSceneWgoIds` only.
- **Picking up a standing zombie:** `ZombieInteractionHandler.Interact` → `PutZombieFromGameSceneToStoreForPlayer`
  → `AddOverheadItem(zombie.ZombieItem)` (the sink the cart already reroutes). There is no free-slot check, so with
  a full cart our sink drops it at the keeper's feet, a valid zombie drop.
- **A zombie lying on the ground** is a drop with `DropType.WgoData` and a `ZombieDropInteractionHandler`. It
  relinks on pickup (`LinkItemToZombieData` → `SetZombieItem(dropItem)`). Zombie drops never auto-destroy (that
  timer is corpses only) and are never auto-collected.
- **Putting it to work** at a clay, sand, mine, sawmill, carrier, porter, power, craft, garden, assistant, conveyor
  or fighters station: `TryGetOverheadItem(group zombie)`, then `PutZombieFromStoreToGameScene*`, then
  `RemoveOverheadItem`. The cart reroutes all three, so a zombie can go to its station **straight from the cart**.
  Taking a worker back is gated by `HasFreeOverheadSlot` (rerouted to the cart's room).
- **Choir, autopsy, embalm** and the scripts (`HasPlayerOvrhdItemByGrp`, `PlayerOvrhdItemIsZombie`,
  `InsertOvrhdItem`, …) all go through the rerouted calls.
- **Places that read the real head:**
  - "can't work while carrying a zombie" (`WorkPlayerState`);
  - fight start dropping the head item;
  - `Flow_PlayerDropOverhead`;
  - visuals.

  All harmless while riding.
- **Two zombies are normal game play:** `OverheadStackLimit = 1 + extra_overhead`, and nothing assumes at most one
  zombie.
- **No whole-save sweep** looks for a zombie's item by GUID or deletes a stored zombie without one. A zombie lent
  to the save as a ground drop (`LendCargoToSave`) is safe, even if the mod is removed later.

## 2. The serious catch: an inventory add copies the zombie's body away from it

`Item.AddItemToInventory`, for items that don't stack (`stackCount == 1`), calls
`Split(1, transferInternalData: true)` (Item.cs). That makes a **new** `Item` with the same GUID and moves the
nested inventory into it; the original keeps an empty list.

The original is usually the zombie's own `zombieItem` (both pickup paths hand the game's linked object to
`AddOverheadItem`). On the vanilla head the same object is stored, with no copy made. On the cart, the cart holds
the full copy, while the zombie points at an empty shell. `TakeToCart` even puts the shell's count back to 1.

**What that breaks:**
- **Organs and equipment lost for good.** `CreateAssistantFromThis` and `CreateCommonZombieFromThis` build the
  worker from the empty `zombieItem`; our `RemoveOverheadItem` patch then throws away the full cart copy. The
  equipped-item lookups by GUID fail afterwards.
- **A possible NRE or lock-out:** a worker whose item GUID no longer matches its zombie; `GetZombie` returns null
  in the next station handler or in the drawing.

Corpses go through the same copy today, but nothing links to a corpse, so it's harmless for them.

**The fix:** after every add of a linked item to the cart, find the stored object by GUID (`FindOnCart`) and
relink: `GetZombie(guid)?.SetZombieItem(stored)`.

| Add point | Where |
|---|---|
| A pickup goes to the cart | `HeadReroute.TakeToCart` |
| The held item loads at mount | `Cargo.LoadFromHead` (through the game's `InsertOverheadItemTo`) |
| Loading on foot with E | `LoadOneFromHead` |
| Re-adding the carried list (doors, view rebuilds) | `Cargo.Attach` |
| Restoring after a load | `SetCarried` (after `Persistence.TakeDropBack`) |

**Known and accepted:** in `TakeToCart` the add fires the cart's change event, and with it the API's `CargoLoaded`,
before the relink runs. A mod listening there sees the zombie one call before its link is fixed. The event carries
the zombie's id, GUID and name, never its gear, so nothing can read the stale link through it.

Removals (`RemoveItemFromInventoryByUID`) return the same object, so the hand-back, set-down and station
placement stay consistent once the link is right.

**One more data-loss guard:** if an unload's `DropAtKeeper` is refused (it logs `LOST`), a zombie would end up
stored with no item anywhere. For a linked item, force it to the head instead (`GateAnswersForHead`), or, as a
last resort, put it back in the world with `PutZombieFromStoreToGameScene(guid, scene, pos)`.

## 3. Drawing: the meshes are there, and every zombie is one size

From the asset bundles:
- **The keeper's overhead view (the one the cart clones) carries both zombie poses.** It's at
  `PlayerPhysicalBody/PlayerView2D/main_character/gfx/overhead/item`, and `FindSourceAtom` picks it (parent
  `overhead`).
  - `zombieWorkerOverheadPrefabs`: 10 entries, `drops_item_zombie_pose1_t1050..1059` (held overhead).
  - `zombieWorkerDropPrefabs`: 10 entries, `pose2_t1050..1059` (lying).
  - The ground drop view has the same lists.
  - A second, empty copy exists in the same bundle (`CustomizationCharacterScaledContainer/.../overhead/item`, 0
    elements). Today's cargo shows items, so the clone isn't taken from that one.
- **The pose follows `isOverhead`.** `MakeView` already sets it false, so `ActivateZombie` gives pose 2, lying,
  in its own container at local (0, 0, 0.15).
- **One body for all zombies; only the head mesh changes** (10 heads, identical bounds per pose). The colours are
  a head LUT: `ZombieSkinHelper.GetPresetForWgoData(zombie, "zombie_worker")`, giving `head.id` ("D4") and
  `head.palette`, as `AnimationComponentBase.ApplyOverheadItemToView` does.

Sizes (mesh space, scale 1):

| Mesh | Full size | Note |
|---|---|---|
| Lying zombie, body | 0.92 × 0.30 × 0.60 | centre z −0.125, in a container at z 0.15 |
| Lying zombie, head | 0.48 × 0.23 × 0.35 | at the −x end |
| Corpse shroud | 0.84 × 0.33 × 0.43 | `i_body`, at z 0.075 |

**Two zombies:** each is a little longer and about 40% deeper than the shroud. Side by side across, they need about
1.2 of bed width, so it may have to be a stack or end to end. The tuned pair decides, as with every other pair.

The game's own `npc_donkey` prefab has a `drops_item_corpse_shroud` on its cart at (0.36, 0.233, 0.125). That is
a reference for where the game puts a body.

## 4. Our cargo code: what is keyed by item and needs a zombie case

- **`RefreshViews` (Cargo.cs).** Today it would call `view.Activate("i_body_zombie")`, which has no element, so
  the white placeholder shows and the game logs an error.
  - The zombie branch would `Deactivate`, then `ActivateZombie(head, lut)`, and switch the colliders off.
  - `ActivateZombie` sets `IconId` to the head id. The "changed?" check needs its own key, `zombie:<head>:<lut>`,
    so that two zombies swapping views redraw.
- **`CargoLayout.BoxOf("body_zombie")` returns zero** (it measures `FindOverheadElement`, which has nothing for
  zombies). `SameClass` would then lump zombies with any other zero box.
  - A measured box close to the shroud's could also make zombies quietly share the corpse's entries, and editing
    one would retune the other.
  - So zombies get their **own class keyed `body_zombie`**: never merged with other items, measured from the
    pose 2 prefab.
- **Placement** uses each view's measured box (footprint centre and bottom), so the ten heads need no separate
  entries.
- **Layout file:** `[item body_zombie]` (along, up and across; top_*; alone_*), `[pair body_zombie+body_zombie]`,
  and `[pair body_zombie+<class>]` for each of the other classes, written by the panel as they're tuned. Exact ids
  already win over class matches. The class list in `Save`'s header gains `body_zombie`.
- **The tuning panel** only tunes what's really on the cart (one item gives the alone sliders, two items a tuned
  pair).
  - There is no dev way to put a zombie on the cart: `SpawnDrop` refuses them, and `BigItemIds` leaves them out
    (also out of the panel's `<`/`>` list).
  - In game a real zombie works (the user has two). A dev stand-in would make tuning every pair practical.
- **The cargo sheet** builds cells from `new Item(id)`. A bare `body_zombie` has no zombie behind it, so no skin.
  - Proposed: a dev skin override (item GUID → head id and LUT) that `RefreshViews` checks before `GetZombie`. The
    sheet then gets a zombie representative, a strip of all 10 heads alone, and zombie pairs with every class.
  - The same override gives a panel button, "Put a stand-in zombie on the cart", guarded like the sheet so it is
    never unloaded or dropped.
  - Borrowing a real zombie from the store was rejected (too invasive if a run aborts).
- **The corpse special case** (`CorpseId`, `CargoDrawCorpse`, `SetGameLoadedBody`) exists because the cart
  prefab's drawer shows `body_corpse` itself. Nothing does that for zombies, so they're ordinary mod-drawn views.
- **Lifting `Allowed`** changes these at once:
  - `LoadFromHead` at mount;
  - loading on foot (`HeldItemFits`, `InstanceControls`);
  - `RefuseGroundItem`, which should keep refusing in Ride style only (hands locked);
  - the `AddOverheadItem` prefix's zombie-to-head branch, which becomes the setting-off path.

  Unload, set-down (front and behind the cart) and the hand-back are generic drops and work unchanged.

## 5. Second round (2026-10-04): every other path, both sides

Two more audits: every path in our mod that touches cargo, and the game's remaining angles. Key claims were
re-checked by hand (marked ✔).

### Confirmed safe or settled

- **Removal keeps the object** ✔. `RemoveItemFromInventoryByUID` (Item.cs:720) returns the stored object itself
  when the whole count is taken (it copies only for a partial count). So once the link is right, hand-backs,
  set-downs and station hand-offs keep it.
- **The carried list after `Attach`** ✔. The re-add copies each item, but `SyncMirror` then rebuilds `mirror` from
  `inv.Data.Inventory` (Cargo.cs:446-449), so nothing later hands back an emptied copy. Corpses' organs are safe
  today.
- **The cart's inventory takes zombies** ✔. It has no whitelist or blacklist: `GK2-carry-facts.txt` gives
  `npc_donkey` `whitelist empty=True`, `blacklist empty=True`, size 2.
- **The cart model's own shroud is only for corpses** ✔. Both drawer rules are `ItemCountCondition` on the id
  `body_corpse` (PROBE-FINDINGS), so a zombie never switches the game's loaded body on.
- **Item groups** ✔ (`GK2-itemdefs.csv`):
  - `body_zombie` is in `body;zombie;overhead`.
  - `body_wild_zombie` is in `body;wild_zombie;overhead`, not "zombie": it already rides the cart today, as a
    shroud.
  - Whether `body_wild_zombie` is `isLinkedToWgo` isn't in the dump. It doesn't matter: the relink is guarded by
    `GetZombie != null`, and the new rules are keyed on the group "zombie", so wild zombies keep today's behaviour
    either way.
- **Dropping before the zombie leaves the scene is fine.** A zombie laid at the feet inside
  `PutZombieFromGameSceneToStoreForPlayer` gives one drop, the zombie in `zombieDrops` and no world object. The game
  does the same itself (`CraftComponent.AddDestroyCraft`, `Wgo.cs`). Drops keep the item object itself, never merge
  zombie drops, and never auto-destroy them.
- **Save and load.** One Odin graph, so a relinked object lent as a drop is still the zombie's object after a load.
  Nothing at load time relinks; the game does it lazily on pickup.
- **Zombie types** (Free, Crafter, Caretaker, ConveyorCrafter, Worker, Porter, Gardener, ConveyorTransporter,
  Fighter) are all picked up the same way. `UnAttachFromWgoData` cleans them up before the item reaches us. A
  stored zombie doesn't tick or follow anyone. Nothing lists `zombieDrops` or looks for a zombie's item in
  inventories, so a zombie on the cart can't show as missing in any window.
- **Skins:** `GetPresetForWgoData` never returns null. Every real zombie has a head (rolled at creation; 1050 /
  `hed_lut_01` fallback at resurrection).
- **Events:** nothing subscribes to `OnOverheadItemAdded`/`Removed`. Script conditions go through the rerouted
  `TryGetOverheadItem`.
- **Storage (`InsertOverheadItemTo`):** the game copies into the storage and never relinks, so our patch matches
  it. Our relink on the way back out repairs the stale link, which is better than the game does.
- **Views:** the cargo views are the mod's own copies, destroyed in `Detach`, not pooled game parts (no `[pool]`
  rule). The colour walks skip `cart_cargo`, and Lantern, LampHang and the previews never touch cargo.

### New findings to handle (most severe first)

1. **More add points need the relink.** Beyond the table in §2:
   - `Cargo.TakeOneToHead` (Cargo.cs:302), when the hands refuse and it goes back on the cart;
   - `HeadReroute`'s `InsertOverheadItemTo` (:386), when the target refuses and it goes back on the cart.

   `Attach` covers doors, H/G recall, the scene respawn of standing carts, hitch, unhitch and restyle. Its add runs
   before the events are subscribed, so the relink must sit right after **our own add calls**, never in the
   change events (those also fire on removals, when a station has just made its own copy).
2. **A zombie must never be thrown away.** Paths that discard or "lose" cargo would leave a zombie stored with
   no item anywhere, unreachable forever:
   - `Mount.Remove` / `MountHandle.Remove()` ("gone with it"; the Herd example calls it without
     `SetDownCargo`);
   - the `LOST` branches in `Attach`, `Unload`, `DropAtKeeper` and `ForceCleanup`;
   - a refused lend in `LendCargoToSave`.

   For a linked item: set it down; if the drop is refused, put it back in the world with
   `PutZombieFromStoreToGameScene(guid, scene, pos)` (the game's own call, which stands it up as a free zombie).
3. **Dev tools could delete or invent zombies:**
   - `CarryProbe.TestUnload` throws the first item away.
   - `TestLoad("body_zombie")` makes a zombie item with nothing behind it, which then lands in the world as an
     orphan.
   - The cargo sheet's `Abort` clears its inventory, but `HeadReroute.Active` ignores `CargoSheet.Running`, so a
     zombie picked up during a run would be wiped. A save during a run lends the sheet's test items into the save.
   - **Required:** the dev tools refuse linked items; `Active` is false while the sheet runs; `LendCargoToSave`
     aborts the sheet first. The stand-in zombie needs the same guard on every drop, unload, lend and set-down path
     (it must never leave the cart).
4. **Ride style, existing bug (in the current build):** riding past a working zombie and pressing E pulls it off
   its job ✔.
   - World-object interactions aren't filtered while mounted, and `ZombieInteractionHandler.HasInteraction` is
     always true except at the sawmill and coal crafters. `Interact` has no slot check: it finishes the auto-craft,
     drops the crafter's stock, unassigns the zombie and frees its dock. Then the hands-locked sink lays the zombie
     on the ground.
   - The same happens in Cart style with a full cart.
   - **Required:** a postfix on `ZombieInteractionHandler.HasInteraction` returning false while the hands are
     locked, or the cart is full or zombies are off. Also filter its "[E] Take" hint line (`FormInteractionInfo`
     adds it regardless). Inspect (F) stays.
5. **The ride ending at a fight, sleep or work** (`Mount.Tick` → "state X"): the hand-back puts items on the head
   after the game's own "drop the head item at fight start" and the work/sleep checks. **Required:** linked items go
   to the ground at such ends; only the save hand-back uses the head.
6. **Where the setting lives.** `Cargo.Allowed` is static and used without an instance (`FitsCart`,
   `InstanceControls`, `RefuseGroundItem`, the `AddOverheadItem` prefix). Make it a **Behaviour key** that Saddle Up
   sets through `Mounts.Behaviour.Set` (the IdleShuffle pattern), not a per-style key. That also means:
   - defaults.cfg and the reference and api.md key tables gain it;
   - turning it off mid-ride leaves zombies already aboard where they are (all removals are generic).
7. **Fighters container:** it teleports the keeper when its dock is within 1 m. Check how the mounted donkey and
   SafeGround react when a zombie is handed in from the cart there.
8. **Smaller:**
   - Set the layer again after `ActivateZombie` (the zombie mesh is built after `MakeView` set it).
   - `DropBehindCart` uses `BoxOf` (0 for zombies); it needs the zombie box.
   - `TryCartRear` counts cargo renderers, so a long zombie moves "behind the cart" further out.
   - Hide walks (`Mount.cs:59`, `Tiers`, `Perf`) can miss a newly built zombie mesh for a frame, as for all cargo.
   - Fire `AddOverhead` when a zombie is laid at the feet (game parity).
   - Logs: "The cart is full." is printed for any refusal; "gone with it"; "not in this save". The take, set-down,
     park and summary lines should name the zombie.
   - Stale comments that say "zombies are refused": HeadReroute.cs:22, 40, 51, 169, 206, 465; Mount.cs:421;
     InstanceControls.cs:325; CarryProbe; TuningWindow.cs:1188.
9. **API:** `CargoLoaded`/`CargoUnloaded` and `CargoIds` give only the id, so two zombies can't be told apart.
   Add a GUID (and name) overload. api.md lines on zombies (:102, :243, :245 `Remove`) change.

Unchanged and fine: dormant records, a restore more than 3 away or without the mod (zombies stay ordinary ground
drops), the game closing, "another game loaded", Cargo switched off mid-ride, `SetDownLast` / `DropBehindCart` /
`SetDownCargo`, `TakeAllForTransfer`, ViewPool. A save during a summon ride with two zombies and one head slot
leaves the second on the ground, as for every item.

### Still only knowable in game (checked by the first build, before anything else is trusted)

- The measured box of the lying zombie on the cart, and that `ActivateZombie` draws the right head and colours
  on a cargo view.
- The relink log on a real pickup: organs and gear counts on the cart copy equal the zombie's.

## 6. Build plan (revised; for approval)

Each step is built and checked before the next.

0. **Done 2026-10-05** (results in [baseline-test.md](baseline-test.md) 13/14). **Before any build:** run the pending zombie tests (top of this doc) on today's DLL. They test today's
   behaviour, which step 1 changes. This gives a baseline for the Ride-style bug, and may turn up more.
1. **Safety, before zombies are allowed** (its own build). **Built and passed in game 2026-10-05** (open-items 9p):
   `HeadReroute.RefuseWorldZombie` with postfixes on `ZombieInteractionHandler.HasInteraction` and
   `FormInteractionInfo` (the "[E] Take" row); `Cargo.IsLinked`; `CarryProbe` test load/unload refuse zombies;
   `HeadReroute.Active` off while the sheet runs; `LendCargoToSave` aborts the sheet.
   - Fix the existing Ride-style bug (finding 4).
   - Guard the dev tools and the sheet (finding 3).
2. **The setting:** a Behaviour key `CargoZombies` (on by default), with a Saddle Up row. **Built 2026-10-05**
   (API 0.22.0, Saddle Up needs 0.22; the dev panel's General tab has the toggle). Not read by anything yet: step 5
   wires it into `Cargo.Allowed`.
3. **Built 2026-10-05:** `HeadReroute.RelinkZombie`, called after `TakeToCart`, `LoadFromHead`, `LoadOneFromHead`,
   `Attach`'s re-add, `TakeOneToHead`'s refused re-add, and the insert's refused re-add. Also after a successful
   insert into another object's inventory: relinked to that copy, where the game would leave the zombie stale.
   **The relink** at every add point (§2 table plus finding 1), guarded by `GetZombie != null`, with the log line
   `Cargo: zombie <name> linked to the cart copy (n items inside)`.
4. **Built 2026-10-05:** `Cargo.RescueZombie` (the game's `PutZombieFromStoreToGameScene` at the spot) in every
   `LOST` branch (`Attach`, `Unload`, `DropAtKeeper`, `ForceCleanup`) and for a refused lend (the zombie leaves the
   cart). `Mount.Remove` sets zombies down (or rescues them) instead of discarding them. `Unload` gives a zombie to
   the hands only for the save hand-back; at every other ride end it goes to the ground.
   **Never lose a zombie:** the `Remove`, `LOST`, lend and fight/sleep/work end paths (findings 2 and 5).
5. **Built 2026-10-05:** `Rig.Allowed` lets group "zombie" through while `CargoZombies` is on.
   `RefuseWorldZombie` (step 1) refuses a working zombie while the cart is full or off.
   - **Taking a worker back from its station:** porter, power source, carrier, clay, mine, sand and sawmill (every
     other caller of `PutZombieFromGameSceneToStoreForPlayer`) check only `HasFreeOverheadSlot`. That answers the
     cart's room. With the setting off, the station would unassign its worker, the head would get it, and the
     empty-hands rule would lay it on the ground (found in the review before the test).
   - `ZombieStations_TakeBack_Patch` marks those handlers' `Interact`, `HasInteraction` and `FormInteractionInfo`
     (all 21 exist, decomp 1.008), and the gate answers "no room" there while zombies are off. With zombies on, the
     cart's room is the right answer.
   **Allow zombies:** `Allowed` follows the setting. `RefuseGroundItem` and the new zombie-interaction postfix
   refuse only while the hands are locked or the cart is full.
6. **Built 2026-10-05:** `Rig.ActivateFor` with a per-view key; the zombie's own head (1050 if outside
   1050–1059) and head LUT; checks that the lying element really shows (else the placeholder and a warning); layer
   and colliders after. Logs `zombie … drawn lying …` and `zombie box … size …`.
   `Cargo.IsZombie` (group "zombie") for drawing and layout, `Cargo.IsLinked` for safety, so wild zombies are as
   before.
   **Drawing:** the zombie branch in `RefreshViews` (preset skin, pose 2, layer, colliders, its own key, head 1050
   fallback).
7. **Partly built 2026-10-05:** `SameClass` keeps zombies a class of their own; `BoxOf` measures the game's
   lying-zombie prefab (`Cargo.ZombieDropPrefab`), which also gives `DropBehindCart` the right length. Still to do:
   seed entries once the box is measured in game, and the `Save` header's class list.
   **Layout:** the `body_zombie` class (own box, never merged); `BoxOf` and `DropBehindCart` use it; seed entries
   and the `Save` header.
8. **Built 2026-10-05:** the sheet makes two real temporary zombies (`CarryProbe.NewZombieBody` +
   `CreateZombieDrop`, held in the store of carried zombies, no world object). It adds a `body_zombie` row and column
   (alone, with itself, with every class) and a strip of the 10 heads (`ApplySkinToZombieWgoData` on the first
   stand-in). It removes them with `RemoveZombie` when it ends or aborts, unless one's item is found off the sheet's
   cart (then it stays, a real zombie, with a warning).
   - Chosen over a fake look (a skin override with no zombie behind it): leaked, a fake would be an orphan zombie
     item that could break a station; a real stand-in is harmless.
   - The sheet now also aborts at a dismount or park, a cleanup, `Mount.Remove` and a hitch or unhitch, besides
     unload and save, so its test items never reach a standing cart or the ground.
   - `CarryProbe.SpawnZombie` shares `NewZombieBody`.
   - Also 2026-10-05, the user's request: the sheet writes to the repo's `tools/cargo-sheet/out/` (git-ignored) on
     the machine with the sources, else to `BepInEx/GK2-cargo-sheet`. Each run first removes its own old pictures
     (the grid's or the shapes page's).
   - The shapes page ("Photograph every item alone") has a zombie bucket too: the stand-in, one picture per head,
     removed when the page ends.
   - Not tested in game yet.

   **Dev tools** (the user chose (a), 2026-10-05):
   - **(a) A stand-in only inside the cargo sheet:** a fake zombie (a head picker, drawn without a real zombie)
     that exists only while `CargoSheet.Running`. The sheet then shows a zombie representative, the 10 heads and
     every pair. The panel is tuned with real zombies.
   - **(b) No stand-in:** tune everything with the user's two real zombies; the sheet skips zombies.

   A stand-in usable outside the sheet was dropped. It would need a guard on 8+ paths (drop, unload, lend,
   set-down, `Remove`, quit, the `LOST` branches), and each one missed would leave an orphan zombie in the world
   (`GetZombie == null`, a station NRE or lock-out).
9. **Done 2026-10-05:**
   - `CargoLoaded` / `CargoUnloaded` pass a `CargoItem` (id, GUID, `IsZombie`, `ZombieName`), and
     `MountHandle.Cargo` replaces `CargoIds`. Replaced in place, not added alongside: nothing is shipped (the user).
   - The key log lines name the zombie (`Cargo.Name`: "body_zombie (zombie_name_14)").
   - api.md (and its 0.22.0 history entry), reference, the API README, saddleup-scope, the Saddle Up README,
     instances, persistence and api-design are updated, baseline 13 is rewritten, and the stale comments are fixed.
   - **Dropped: firing `AddOverhead` when a zombie is laid at the feet** (§5 finding 8). That was meant for a
     working zombie taken with a full cart, which `RefuseWorldZombie` now refuses before anything happens. A zombie
     put back on the ground ("The cart is full.") was never picked up, so the game's "picked up" event would be
     false (quests listen to it).
   - "The cart is full." is accurate now: the sink reaches it only when the cart has no room (refused items are
     sorted out before).

   **API and docs:** GUID/name event overloads, api.md, reference, README, saddleup-scope, baseline 13 rewritten,
   the stale comments.

**In-game tests**, in order. Each lists what to check (the zombie's worker window shows its gear; the log has the
relink line and no `LOST`):
1. Ride style: E near a working zombie does nothing; zombies on the ground show no outline.
2. Cart style: pick up a standing zombie (relink line), then a second; both lie on the cart.
3. Cart full: a third zombie can't be taken from its job (no prompt).
4. Hand one to a workstation from the cart, then one to the fighters container (watch the teleport).
5. Set one down at the front and one behind the cart; pick them up again.
6. Load one on foot with E, and take one off with E.
7. Through a door; H/G recall; unhitch and hitch with zombies aboard.
8. Dismount with two zombies and one head slot (one to the head, one to the feet).
9. A fight or sleep ending the ride: zombies go to the ground, not the head.
10. Save with zombies on a standing cart, then load: both back, gear intact.
11. Setting off: today's behaviour; turned off mid-ride, zombies aboard stay.
12. Tuning: `body_zombie` alone, `+body_zombie`, `+log`, `+corpse` in the panel; then the sheet.
