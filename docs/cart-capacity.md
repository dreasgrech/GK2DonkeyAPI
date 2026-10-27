# Cart capacity: more than two items on the cart

Investigation, 2026-10-10 (game: Steam build 25752683, 1.009 hotfix; decompiles `decomp_1009b` and `lbt_1009b`).
The question came from a player: the keeper can carry several items on his head with a perk, so a cart that holds
only two does not make sense. **Built the same day as option A (API 0.28.0), see section 9**; the shipped default
stays 2 until the layered look has been judged in game.

## 1. The premise, checked in the game's data

**The head.** `PlayerData.OverheadStackLimit = 1 + ExtraOverhead`, where `ExtraOverhead` is the game resource
`extra_overhead` (`PlayerData.cs:145-149`). Every "is there room in his hands" question in the game goes through
`HasFreeOverheadSlot = OverheadCount < OverheadStackLimit` (stations, choir, porter, resurrection, autopsy, embalm,
conveyor cells: 37 call sites in the decompile). The stack is drawn by `AnimationComponentBase.SetOverheadItems`: one extra
`DropViewAtomMesh` per item above the first, each at `LazyConsts.OVERHEAD_STACK_OFFSET * i` = `(0, 0.3, -0.04)` per
step (`LazyConsts.cs:749`): a straight tower, 0.3 up and a hair back per item, with a sorting bump per level.

**The perk.** Exactly one definition changes `extra_overhead`: `perk_extra_overhead_1`, icon `i_p-porter_3`, linked
from the talent `talent_extra_overhead_1` (costs orange talent points, `talentId talent_orange`; parent node
`building_lvl_up_1_1`: a talent offered in the Building level-up tree). Its `setGameResOnAdd` is `extra_overhead = 3.0` and its
`setGameResOnRemove` is `extra_overhead = 0` (`resources.assets` bytes at offset 39440556: `GameRes.resValues[0]`
= `GameResAtom { type "extra_overhead", value 0x40400000 = 3.0f }`, `lbt_1009b/LazyBearTechnology/GameRes.cs`,
`GameResAtom.cs`; the game's dump in `BepInEx/GK2-head-facts.txt` lists the same fields). So the one perk takes the
head from **1 to 4 items** in a single step. The perk system applies it with `PlayerData.SetRes` on add
(`PerkSystemData.cs:93-99`). `IncreaseOverheadStackLimit` / `SetOverheadStackLimit` also exist as script functions
(`LazyExpression.cs:764-785`) but no definition in the data calls them.

**Carrier II and III do not exist.** The localization tables hold `perk_extra_overhead_2`, `_2_d`, `_3`, `_3_d`
("Carrier II: The pile is getting dangerously ambitious. Carry even more items on your head." / "Carrier III: At this
point, you're basically a walking warehouse. Carry a huge number of items on your head."), but the balance region of
`resources.assets` has no `perk_extra_overhead_2`/`_3` and no `talent_extra_overhead_2`/`_3`: strings with no
definition behind them: `talent_extra_overhead_2`/`_3` appear nowhere in `resources.assets`, and
`perk_extra_overhead_2`/`_3` only in the localization blocks (file offsets above 42,000,000; the perk definitions
sit near 39,440,000). The runtime confirms it: the head-facts dump walked 13,843 `GameBalance` definitions and lists
only `_1` under `perkDefs` and `talentLevelUpDefs`. There is no higher head tier to match. At the time of that dump
the user's keeper had the default 1 slot (`[head] count 0, limit 1 (extra_overhead 0)`).

**What the perk does to the cart today.** Mounting with a loaded head moves onto the cart only what the cart takes
(`LoadFromHead`, `Cargo.cs:306-314`: `Allowed && CanAddItemToInventory`, one by one); the rest **stays on his head**,
nothing is dropped. While riding, the game's own head lookup runs first and the cart is searched only when the head
had no match (the `TryGetOverheadItem` postfix, `HeadReroute.cs`), so those hand items stay usable at piles and
stations; "is there room" answers for the cart (`HasFreeOverheadSlot` prefix). So a perked keeper who gets on with
four items rides with a split load, two on the cart and two on his head, which is the player's complaint seen in
code: the head out-carries the cart and the cart cannot even take what he arrives with.

**The cart.** The game's delivery cart carries its body in the donkey `WgoData`'s own inventory, created once from
`npc_donkey.inventorySize = 2` (`WgoData.cs:2172-2174`: `Inventory.Create(Definition.inventorySize, ...)`). The
game's own script never puts more than one body on it (`PROBE-FINDINGS.md`, "How the game's cart carries the body").
Our cart's cargo is that same inventory (`Cargo.Rig.Attach`: `inv = d.Data.Inventory`), so the two-item limit is
simply the definition's slot count. **Nothing in the game or the mod chose "two" for the cart; it is the number the
delivery donkey happened to be given.** The player's point stands: with the perk, the keeper's head out-carries the
cart two to one.

## 2. Raising the number is cheap at the inventory level

- The slot count is a plain settable integer: `Item.InventorySize { get; set; }` on the inventory's root item
  (`Item.cs:119`), read live by `CanAddItemCountToInventory` (free slots × stackCount; all 29 big items have
  stackCount 1, so one item per slot). `CartChest` already sets it to 0 while a chest rides and restores it after
  (`CartChest.cs:559-560`, `RestoreCartSize`), so the technique is proven in this code base.
- **Set it per instance in `Rig.Attach`, never on the `WGODef`.** Changing `npc_donkey.inventorySize` would also
  change the game's own delivery donkey (built from the same definition). Our donkeys are temp objects: their
  inventory is never written to a save, so a bigger size can never leak into the save either.
- Every new view goes through `Attach`: mounting, hitching and unhitching (the standing cart gets a new inventory),
  the re-summon after a door, a restore from the side file, the summon style's cart. One line covers all of them.
- Everything downstream reads the inventory, not the definition, so it follows a bigger size with no change:
  - the head gate while riding (`PlayerData_HasFreeOverheadSlot_Patch`: `Count < InventorySize`,
    `HeadReroute.cs:464-465`), which is what every station and pile asks before taking from or giving to the cart;
  - the ground pickup onto the cart and the "The cart is full." hint (`HeadReroute.cs:261`, `Cargo.cs:349-355`);
  - load on foot: "Load on cart" / "Cart is full" (`InstanceControls.cs:329` via `HeldItemFits` →
    `CanAddItemToInventory`);
  - the morgue hand-over (takes bodies while his hands have room, whatever the cart holds);
  - the API's cargo events (diffed by GUID, no count anywhere; `CargoItem` carries no slot);
  - persistence: the side file is a list of `cargo <guid>` lines per cart, no fixed count (`Persistence.cs:310`,
    `494`); the save gets one ground drop per item at the cart point (`MountRegistry.LendCargoToSave`, already the
    same spot for both items today) and takes them back by GUID.
- No game system weighs the cart: there is no speed, sway or stamina effect keyed to the cargo count (`CartPart.Weight`
  is the sway animation's per-part weight, `Mount.cs:1173`), so more cargo changes nothing physical unless we add it.
- The order at a new view is fixed in `Mount.cs:1092-1096`: `CargoRig.Attach(donkey)` first (the inventory, the
  carried-over items put back, the views), then `ChestRig.OnCartInventory(CargoRig.Inv)` (a chest aboard shrinks the
  new inventory to 0 and remembers what it saw), then the cart parts and the lantern. A size set inside `Attach` is
  therefore what the chest remembers and restores.
- The game's own loaded-cart look (the prefab's `ConditionalDrawer`, which switches the `donkey_cart_body_sway`
  shroud mesh on while a `body_corpse` is in the inventory) is kept off on our carts (`Mount.cs:1083-1086`), and the
  mod draws every item itself (`RefreshViews`: one copy of the keeper's overhead mesh element per item, measured,
  then `Pack`). A bigger inventory cannot confuse the game's drawer because it never draws for us.
- The game's inventory rules do not care about the number: `Inventory.Create(size, isFuel, whiteList, blackList, id)`
  just stores it on the root item (`Inventory.cs:66-72`), and `Item.CanAddItemCountToInventory` compares the fill
  against `InventorySize` at the time of the call (`Item.cs:396-485`). The donkey definition's white and black lists
  (dumped in `BepInEx/GK2-carry-facts.txt`) keep applying to every slot.

## 3. Where "two" is baked into the mod

These are the real work, in order of how much they matter.

1. **The packer** (`Cargo.Rig.Pack`, `Cargo.cs:1265-1360`). Items are sorted by footprint; the first goes on the
   floor, the second lies beside it (across the bed, or end to end, or stacks), and **every further item stacks on
   the previous one**: a third and fourth item build a tower on item two, which is the wrong picture for four items
   (the game's own timber pile and the keeper's head are towers, but a cart bed wants layers). Pair tuning
   (`cargo-layout.cfg` `[pair a+b]`, `drawn.Count == 2`) and the panel's pair sliders (`TuningWindow.cs:1561`,
   `placed.Count >= 2` edits the first two) are untouched by a bigger cart: they keep describing the bottom pair.
2. **The re-add after a door** (`Attach`, `Cargo.cs:235-265`). With `StayMountedThroughDoors` the cargo is kept in
   memory and put back onto the re-summoned cart's fresh inventory, and whatever does not fit is **dropped at the
   keeper** (or at the cart). The fresh inventory has the definition's 2 slots, so with four items aboard two would
   hit the ground at every door. The size has to be applied right after `inv = d.Data.Inventory` and before that
   loop. The same applies to the restore from the side file (`SetCarried` → `Attach`).
3. **The chest's remembered size** (`CartChest.RestoreCartSize`, `OnCartInventory`): it restores the value it saw
   when the chest came aboard, not the setting. Harmless today (always 2); with a setting it should re-apply the
   setting instead, so a capacity changed while a chest rode is not undone when it is set down.
4. **The summon style's dismount** hands the cargo back: hands first, the rest dropped at his feet (`Unload`,
   `Cargo.cs:746-812`). With six items and one hand slot that is a heap of five on the ground at every dismount.
   Instances park with their load, so this only concerns the old summon style; worth a line in the docs, not code.
5. **Text that says two:** `src/SaddleUp/README.md:24` ("The cart carries two items"), `src/GK2DonkeyAPI/README.md:59`,
   `docs/reference.md:49`, `docs/api.md:60` ("pickups land on the cart (2 slots)"), `docs/load-on-foot.md:48`,
   `docs/baseline-test.md` steps 11, 12 and 16, `docs/showcase-video.md` scene 4, the Saddle Up toggle text for
   zombies ("two at once", `src/SaddleUp/Plugin.cs:54`), `docs/zombies-on-cart.md` ("two at once" was the decision
   there), and the Debug-tab description in `docs/reference.md:240` ("2-slot inventory").
6. **The cargo sheet** (`Dev/CargoSheet.cs`) photographs items alone and in pairs only. Fine as it is: it documents
   the bottom layer. A "four items" sheet would be a new tool, not a change.
7. **Self-tests** check exact counts of one item here and there (`SelfTest.cs:863-872`, `SelfTest.Morgue.cs`), none
   assumes a limit of two; a new scenario is needed for the bigger cart (see section 7).

## 4. What it would look like

The bed measures 0.90 along × 0.75 across (`Cargo.cs:190-191`, measured from the empty cart body). The carryable
shapes, as the shapes page measured them (`tools/cargo-sheet/out/shapes.html`; long × high × across):

| Shape | Items | Box |
|---|---|---|
| Log | wood | 0.82 × 0.57 × 0.35 |
| Flour sack | box_flour | 0.52 × 0.58 × 0.49 |
| Tall crate | box_beer, box_clothes_1/2, box_fabric, box_vine | 0.52 × 0.50 × 0.47 |
| Low crate | 15 box_* incl. preserves | 0.52 × 0.40 × 0.50 |
| Body | body_corpse, body_wild_zombie | 0.84 × 0.33 × 0.43 |
| Slab | stone, iron_ore_2h (marble shown as a slab) | 0.74 × 0.33 × 0.35 |
| Zombie | body_zombie | 0.98 × 0.32 × 0.60 |

Two items fill the floor in every combination the packer knows: two logs or slabs across (0.70 on 0.75), a body
beside a log (0.78, within the overhang), two crates end to end (1.04 on 0.90 plus overhang), two bodies or two
zombies stacked. So **more than two means layers, not a wider spread**: four items is two layers (the look of the
game's timber stockpile, which is what the packer already imitates), six is three. Heights: crates 0.4-0.58 per
layer, logs 0.57, bodies and slabs 0.33, so three layers of crates stand about 1.5 above the bed and three layers of
bodies about 1.0. Two layers of anything looks like a loaded cart; three layers of crates is a tower on wheels.

Open on paper, to be judged in game:

- **The lantern pole** stands at the front of the cart (pivot `(0.36, 0.267, 0)`, `Lantern.cs:11`) and the lamp hangs
  from its head; a second layer at the front end may touch the pole or hide the lamp. The packer puts the larger
  footprint first and the pair centred, so the front end is used by the second item's end; a layered packer can
  keep the upper layers slightly to the back, as a real load would be.
- **The rider.** Cargo is drawn as mesh views in the cart holder with the game's sorting; a tall stack behind the
  donkey, seen from the camera's angle, sits in front of nothing (the keeper rides the donkey, ahead of the bed),
  so no occlusion problem is expected, but a stack higher than the rider's head will look top-heavy.
- **Zombies**: 0.60 across, so two never lie side by side; four zombies would be a single stack of four
  (4 × 0.32 = 1.3 high) unless the layered packer lays the upper pair end to end where it can.

## 5. Options

**A. A fixed setting, `CartSlots`** (Behaviour key, `[General]`, `Tuned<int>`, default to be chosen, range 1-6).
Saddle Up shows it as a Steps row ("Cart capacity": 2, 4, 6; `MountSettingsPage.Steps`, as "Baby donkeys" and
"Donkey and cart sounds" already use). No new API surface: Behaviour keys are strings, and `Tuned<int>` exists
(`RiderOffsetPx`). `defaults.cfg` gets the key (`DefaultsScan`). Honest and simple; the player who asked gets a
slider.

**B. Follow the hands**: slots = base + `PlayerData.ExtraOverhead` (2 → 5 with the perk) or
2 × `OverheadStackLimit` (2 → 8). Resized live on `PlayerData.OnGameResChanged` (the API's own gating pattern,
`docs/api.md` "Gating on the game's progress"; null-safe before a save loads: the base until then). The story is
good ("a stronger neck, a fuller cart") but the perk is a single step to 4 on the head, so the cart would jump from
2 to 5 or 8 in one go, with no tier in between to match, and 8 is four layers of crates. It also ties the cart to a
perk whose text says "on your head", which a player may not expect to touch his cart.

**C. A tech- or perk-gated step**: capacity 2 until the player has `perk_extra_overhead_1`, then a configured number
(say 4). That is option A with a condition; it suits the progression but adds a rule the player cannot see in any
menu, and the API's rule mechanism is for mods, not the core.

**Recommendation:** build A, with the packer laying items out in layers, and make the number the player's choice
(Steps 2 / 4 / 6). Keep **default 2** for the first build so nothing changes under existing players until the look
of a loaded cart is judged in game; decide the shipped default after that (4 is the natural candidate: two layers,
twice the game's cart, and level with the perked head's 4). B can be added later as one more choice
in the same row ("as many as your hands") if the user wants the perk to matter; it is a two-line rule on top of A.
The default, and whether B is offered at all, is the user's call.

## 6. Where a setting plugs in (checked, nothing new needed)

- **The core's keys** are BepInEx config entries wrapped in `Tuned<T>` (`Tuned.cs`; `Plugin.cs:371-372` for
  `CargoZombies`/`CargoChest` as `Tuned<bool>`; `Tuned<int>` and `Tuned<float>` are in use for the rider offsets and
  the speed, `Plugin.cs:17-21`). A mod sets them by key name through `Mounts.Behaviour.Set(guid, key, value)`
  (`Api/Mounts.cs:232`, `MountTuning.Get<T>`), so a new `CartSlots` key is a config entry plus a `defaults.cfg` line
  (`[General]`, `tests/DefaultsScan` fails without it) and a row in `docs/reference.md`; the public API surface does
  not change (`tests/ApiSurface` stays as is).
- **Saddle Up's page** has the row kinds Toggle, Steps (a whole number from min to max in steps, with a formatter;
  `Api/MountSettingsPage.cs:114`, used for "Baby donkeys" 0-4 and "Donkey and cart sounds" 0-150 %), Choice, Color,
  Status, Info, Preview and LookButton. In the GK2 Mod Framework's Mods menu a Steps row becomes an int slider
  (`FrameworkLink.cs:147`, `AddIntSlider`); in the game's own settings it is a `UISlider` row (`SettingsRows.cs:262`).
  So "Cart capacity 2 / 4 / 6" is `p.Steps("Behaviour", "CartSlots", 2, 2, 6, 2, ...)` with a formatter, one line.
- **A live change** reaches the core through the entry's `SettingChanged`, as every other Behaviour key; the core
  would re-apply the size to every attached cart (`Mount.ForEachInstance`), never below what the cart holds.
- **The API's progress gating** (`docs/api.md` "Gating on the game's progress") already documents reading
  `PlayerData.GetRes` and reacting on `PlayerData.OnGameResChanged` per save, which is exactly what option B needs;
  `PlayerData.ExtraOverhead` and `OverheadStackLimit` are public getters.

## 7. Build plan (not started)

1. **The size.** `Plugin.CartSlots = Config.Bind("General", "CartSlots", Defaults.Get(..., 2), ...)` with
   `AcceptableValueRange<int>(1, 6)`; `defaults.cfg` line; Behaviour key documented in `docs/api.md` and
   `docs/reference.md`. In `Rig.Attach`, right after `inv = d.Data.Inventory`: `inv.Data.InventorySize =
   Mathf.Max(Plugin.CartSlots.Value, mirror.Count)` (never below what is being carried over, so a lowered setting
   cannot drop items at a door; it just stays full until unloaded). `CartChest.RestoreCartSize` re-applies the
   setting instead of `savedSize`. A live change (panel or Mods menu) re-applies to every attached cart
   (`Mount.ForEachInstance`), again never below its count.
2. **The packer in layers.** `Pack` becomes: fill layer 0 with the pair logic as today (side / along / stack, tuned
   pairs kept); the next item that would stack starts layer 1 at the layer's top (max of the layer's tops minus
   `CargoNestle`), where the pair logic runs again against the bed; and so on. The per-layer logic is the existing
   code with a floor parameter, so the one- and two-item pictures (and every tuned pair) do not move. Upper layers
   shift towards the back by a small configurable amount to clear the pole. `Placed`/`OnTop` get a `Layer` for the
   panel and the layout note says "layer 2: ...".
3. **Docs and text** from section 3.5; Saddle Up README and the zombies toggle text ("two at once" → "as many as the
   cart holds").
4. **Self-test** "cargo: more than two" (group "Chest on the cart" or a new "Cargo" group): with `CartSlots` 4, spawn a
   test cart, add four generated items (`CarryProbe`'s test item path), check the inventory takes four and refuses a
   fifth, that `Placed` has two layers with the upper two above the lower two's tops, that detaching and attaching
   (the door path: `Detach` then `Attach` with the mirror) puts all four back with nothing dropped, that a chest
   coming aboard and leaving restores the setting's size, and that lowering the setting to 2 with four aboard keeps
   four and refuses a fifth. Restore the setting; remove the items and the cart.
5. **Hand test**: four crates, four logs, two bodies and two logs, four zombies; the lantern, the door, a save with a
   full cart, the morgue door with bodies in the upper layer, the showcase video's cargo scene.

Estimated size: the inventory part is a handful of lines; the layered packer is the only real code (one afternoon
with the panel to judge it), plus tests and the text.

## 8. Sources checked

Game (decompile `decomp_1009b`, build 25752683): `PlayerData.cs` 140-150, 295-345, 405-430 (the limit, add,
try-add, the two limit setters); `LazyExpression.cs` 610, 764-785, 848-862 (`NoOvhdItem`,
`IncreaseOverheadStackLimit`, `SetOverheadStackLimit`); `LazyConsts.cs` 749-751; `AnimationComponentBase.cs`
104-357 (the head stack's views and offsets); `PerkDef.cs`, `PerkSystemData.cs` 93-99, `TalentLevelUpDef.cs`,
`TechDef.cs`; `WgoData.cs` 2172-2186; `Inventory.cs` 66-114; `Item.cs` 119, 396-485; the 37 `HasFreeOverheadSlot`
call sites. Shared library (`lbt_1009b`): `LazyBearTechnology/GameRes.cs` (`resValues` then `resType`),
`GameResAtom.cs` (`type`, `value`). Data: `resources.assets` byte dump at 39,440,540-39,440,840 (the perk) and
40,259,256 (the talent), id counts for `perk_extra_overhead_*` and `talent_extra_overhead_*`, the English strings
via `tmp/graphtools/lang.py`; `BepInEx/GK2-head-facts.txt` (the runtime walk of the balance definitions and the
keeper's limit at the time).

Mod: `Cargo.cs` (the rig: `Attach` 230-280, `LoadFromHead` 285-325, `HeldItemFits`/`FitsCart` 485-496, `SyncMirror`
and `Announce` 645-700, `Unload` 742-812, `Pack` 1265-1360, the bed size 190-191 and 1595-1596, the views
1078-1115), `CargoLayout.cs`, `cargo-layout.cfg` (the size classes), `CartChest.cs` 25, 487, 550-566, 995-1045
(the 0-slot rule and its restore), `HeadReroute.cs` 122, 261, 440-470 (the room gate), 472-520 (the lookup),
546-597, `InstanceControls.cs` 318-332 and 541-542 (load on foot), `MorgueHandover.cs` 65, 206, `Mount.cs`
1080-1100 and 1173, `MountInstance.cs` 613, 877, `MountRegistry.cs` 657, 928, 1016-1075 (`LendCargoToSave`),
`Persistence.cs` 14-21, 269-311, 494, 650-758, `Patches.cs` 274-281, `Plugin.cs` 17-21, 127-129, 371-372,
471-476, `Tuned.cs`, `Api/Mounts.cs` 232, `Api/MountTuning.cs` 88, `Api/MountSettingsPage.cs` 87-222,
`FrameworkLink.cs` 143-188, `SettingsRows.cs` 262, 479-484, `defaults.cfg`, `Dev/CargoSheet.cs`,
`Dev/CarryProbe.cs` 41-192, `Dev/HeadProbe.cs` 495, `Dev/TuningWindow.cs` 595, 1528-1600, `Dev/SelfTest*.cs`
(every count check), `PROBE-FINDINGS.md` 380-410 and 773, 891, `tools/cargo-sheet/out/shapes.html` (the measured
boxes), `docs/zombies-on-cart.md` 53, 120-124, and the "two items" mentions across the docs and READMEs.

## 9. Built (2026-10-10, API 0.28.0): option A

- **The setting.** `[General] CartSlots` (`Tuned<int>`, 1 to 6, default 2; `defaults.cfg`), a `Mounts.Behaviour` key
  like the others. Saddle Up shows it as the Steps row "Cart capacity" (2 / 4 / 6 items) after the chest toggle; the
  panel's General tab has an int slider beside "Zombies ride on the cart".
- **The size per cart.** `Cargo.Rig.WantedSlots()` is the setting (an instance's own `Overrides["CartSlots"]` first,
  else the global value, never `Tuned.Value`, which reads whichever instance is bound), never below what the cart
  holds or carries over. `ApplySlots()` writes it to the inventory unless a chest is aboard (its 0 stays). Called in
  `Attach` right after the inventory is taken and before the carried-over items go back on (the door fix), from the
  inventory's change handler (a lowered setting takes effect once the extra items are off), and for every instance
  from `Mount.ApplyCartSlots` at a cfg `SettingChanged` and at every Behaviour override (`OnTuningChanged(null)`).
  `CartChest.RestoreCartSize` restores `WantedSlots()`, not the size it found.
- **The packer in layers** (`Pack`): a layer holds up to two items. Its first item sits over the middle of the layer
  below (over the single item when the layer below has one, which is the old stack rule verbatim), at that layer's
  top less `CargoNestle`, with its on-top offsets and moved along the cart by the Cart-style setting
  `CargoLayerShift` per layer (-0.03: towards the donkey, away from the lantern pole at the back); the second lies
  beside it by the same side / along test (the pair centred on that middle) or starts the next layer. One and two
  items come out exactly as before, and the tuned-pair override still applies only to exactly two items.
  `PlacedItem.Layer` says which layer; `OnTop` is "above the bed". The layout note gains "; layer 2: ..." parts. The
  panel's pair sliders show only for exactly two items; with more it says so and shows the layout note.
- **Self-test** "cargo: more than two items (layers, a re-made cart, the setting)" (group "Cargo in layers",
  `Dev/SelfTest.Cargo.cs`): the override applied to a standing cart at once; four plain items taken, a fifth refused;
  two layers of two, the upper standing on the lower; unhitch and hitch (the door path) bring all four back on a
  4-slot inventory with nothing dropped; lowered to 2 with four aboard the size stays 4 and still refuses; two off and
  it is 2. Cleanup: override cleared, items removed by GUID, both carts removed.
- **The panel, checked for 1, 2 and more slots (same day):** the General tab's "Cart capacity" row re-applies on
  every path (the cfg write, Saddle Up's override, and editing one donkey on the Instances tab, which gives that cart
  its own number: `Instance.OnTuningChanged` applies the size, so the API's `MountHandle.Tuning` does too). The
  corrections section shows the alone sliders for one item, the pair sliders for exactly two, and a note with the
  layout for more. The cargo sheet needs pairs, so it holds a floor of two slots while it runs (`Cargo.SlotFloor`,
  cleared when it ends or aborts) and works at a capacity of one. The probe buttons report "REFUSED (filter or
  full)" at a full cart as before. Nothing in the panel assumes two any more.
- **First try in game (2026-10-10):** Saddle Up's row changed nothing and the log said `No tuning value 'CartSlots'`:
  the `Mounts.Behaviour` tuning is built from an explicit list of entries in `Api/Mounts.cs`, and the new key was
  not in it (a Behaviour key needs the cfg entry, the `defaults.cfg` line and that list). Added; the self-test
  uses the same call and would have failed the same way.
- **Six logs in game (2026-10-10):** three layers of two across, 6/6 taken, the two-item picture unchanged. The
  first build leaned the upper layers towards the back, "away from the lantern pole at the front", but the Cart
  style's defaults put the pole at the back (`LanternAlong` 0.47), so the lean went towards the lamp: it is now the
  setting `CargoLayerShift` (per layer, positive = back, default -0.03 towards the donkey), on the panel's packer
  section with the other four values.
- **Tuning stacks by hand** is its own investigation: [cargo-layout-layers.md](cargo-layout-layers.md) (pairwise
  relations, beside and on top, as the unit of tuning; the panel showing the relations a load is made of).
- **Still open:** the look in game (four crates, four logs, two bodies and two logs, four zombies; the lantern pole;
  the sheet's two-item pictures unchanged), a door with four aboard, a save with a full cart, and the shipped default
  (2 for now). Option B (follow the hands) is not built; it would be one more choice in the same row.

## 10. Each mount its own cargo (2026-10-27, API 0.40.0)

The cart holds the smaller of `CartSlots` and the cargo of the mount pulling it (`[Mount] cargo`, 1 to 6, null: 6;
`Cargo.Rig.WantedSlots`), never fewer slots than it holds or carries over. A mount change under a hitched cart
re-applies it when the looks re-merge their tuning (`Looks.Rig.ApplyTuning`, once per mount, skin or style change;
logged `Cargo: the cart's slot count follows <mount>'s cargo`); a hitch re-applies it through `Attach`. The shipped
cargo: the giant rat 2, Gerry and the giant worm 4, the donkey and the giant snail 6 (docs/next-update.md section 3:
the faster, the lighter). Self-test "cargo: each mount carries its own".
