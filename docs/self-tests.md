# GK2 Donkey API: in-game self-tests

These are scripted checks that run inside the game (`src/GK2DonkeyAPI/Dev/SelfTest.cs`). They were written on 2026-10-01
so that the levels and view reuse can be tested without someone watching for each detail.

**What they check:** every bug found by hand that day is a scenario. Each one checks the state that decides what is
seen:
- the animator's own `Direction` parameter: 0 is right, -180 is left;
- which cart parts are switched on;
- whose material each sprite wears;
- the counters.

A fix can't pass because the case happened not to show.

## How to run

1. Load a save and stand outdoors with open ground around the keeper, **inside a built-up area** (the chest scenarios
   build their test chest beside the keeper and need a zone; outside every area they skip with a WARN). The tests need
   ground 3, 22, 26 and 42–50 units away. The start line says which zone the keeper is in. When he isn't inside a **container** zone (the kind
   with chests and crafting, which is what the chest scenarios need; the game's own zone readout counts other kinds
   too, such as `inquisitions_base`), the runner moves him into the nearest one first and warns only when none can
   be reached. **A run that passes everywhere remembers its spot** (`Debug.SelfTestSpot` in the API's cfg, `scene|x,y,z`):
   the next run in that scene moves the keeper there first and puts him back where he stood at the end, so after one
   good run it no longer matters where you start it from.
2. Open the panel (F11), go to the **Tests** tab and press **Run all**. It takes about 4 minutes. The tab lists every
   scenario by group (API and carts; levels and views; chest; morgue; corpses), each with **Run** and each group with
   **Run group**: a group or one scenario alone has the same start and finish as the full run (the test spot, the
   cleanup between scenarios, the world's own donkeys checked), and the others' last results stay on the tab. Only a
   full run remembers its spot (below). The summary of a partial run says so (`done (3 of 30 scenarios): ...`).
3. **Don't touch the keys while it runs.** The tests move the keeper, ride and dismount. **Abort** stops after the
   current step.
4. **Progress and results:** the panel stays up during the run (only the frame-time scenario hides it, its own cost
   would be in the measurement): a bar and "k of N scenarios done", and every scenario's row says `running, 12 s`,
   then `PASS 9 (14 s)`, `WARN 1, 3 passed`, or `FAIL 1, 8 passed`. In `BepInEx\LogOutput.log`: `[test] PASS`,
   `FAIL`, `WARN`, `INFO` and `SKIP` lines and a summary, `[test] done: N passed, M failed, K warning(s)` (and `, S
   scenario(s) skipped` when some can't run in this setup: the tab shows `SKIP (not in this setup)`, not a warning;
   with `Debug.MountsPreview` off, the state players have, the scenarios that need the built-in mounts and skins skip
   and "mounts off" runs instead). The tab ends with
   the failures and warnings of the last run.

**Safety:**
- **Test donkeys** belong to `dreas.gk2.donkeyapi.selftest`. They're never kept with the save and are removed after each
  scenario.
- **The keeper** is put back where he stood, facing the same way, after every scenario.
- **The panel's state** (`Debug.TuningWindowHidden`) is left as it was: hidden for the frame-time scenario only, and
  back afterwards.
- **Nothing is saved.**
- **The level settings** are restored at the end.

**Guards against false results:**
- **Spots:** need ground and room for the keeper (nothing on the obstacle layer).
- **After every move,** the runner reads back where the keeper is. If he was pushed away from a blocked spot, the
  scenario gives a WARN and skips.
- **The leak counts** are taken only once the levels have settled: no view made or released, and no level changed for
  2 s.

**A WARN** means a scenario couldn't set itself up at this spot (no ground there, or a point still on screen). It isn't
a failure of the mod. Try another spot.

## The scenarios

| Scenario | What it does | What it checks |
|---|---|---|
| The per-mod API | two test owners, each with a #1 and the same key | each mod's own numbers; a taken number or key refused; `Find` by number and key; `InstancesOf`; a removed number not handed out again; another mod's `SetPersist` and `SetKey` refused, its own mod's allowed; data per mod, set and cleared independently |
| Kept-with-save text, round trip | a kept parent with a foal, a donkey with its cart and a free cart, with keys, own settings and a value holding a tab, a line break and a backslash; written to the side file's text and read back in memory (no file, no save) | the header and every field of every record come back the same: owner, number, key, kind, scene, exact position, facing, size, rideable, parent, removeWithParent, settings, data |
| Hitch and unhitch | a donkey takes a free cart and leaves it again | one instance with the cart, the cart's data merged in, cart parts on; then a new free cart with the donkey's owner and kept setting, and the donkey's cart parts off |
| Unhitch without a view (a parked cart stays) | a donkey with its cart and an item spawned 42–50 away (asleep, no view) is unhitched, then called to the keeper | a free cart exactly where the donkey stood, with the item, the donkey's owner and kept setting; the donkey without a cart and still without a view; called, it comes without cart parts and the cart stays put |
| Shared colour textures | two donkeys with the same colours, then one with another | the second adds no texture, another colour adds its own, and all removed gives the starting count back |
| Settings: a moved section keeps a player's values | a temp cfg with values under "2. Behaviour", "3. Donkey", "4. Cart" and "5. Cart", bound by a page whose sections are Keys, Mount, Behaviour, Donkey, Cart, Sound | Behaviour and Donkey (moved one down) have the file's values, Cart (the file has it under its own number too) keeps its own, a key the file has nowhere keeps its default; a second page on a cfg that saves on every set: the page holds the saves while it binds, the last row carries its old value, and the one write at the end leaves the old line out |
| Upgrade: a released version's player files are all read | every folder of `tests/fixtures/released/` (0.9.1 reconstructed; later ones captured at release): Saddle Up's cfg through the live page's own carry-over, the API's cfg and that release's `defaults.cfg` (an independent witness to the key names) against this build's binds, the side file through the reader; a format-99 side file set aside twice | no line of an old Saddle Up cfg is left without a row (sections renumbered, keys moved) and each value reads as its row's type (enum names); every old API key is still bound and its value reads as its type; every side-file record comes back whole (0.9.1: the named donkey with its setting and an escaped data line, the 0.6 foal with its parent, the free cart with two items and a chest); the unreadable file is `<slot>.gk2donkeyapi.format99.dat`, and a newer one replaces it |
| One new view per frame | 20 donkeys at once around the keeper | all get views within 10 s, at most `SpawnsPerFrame` made in any frame |
| Waking (view switched off) | a donkey with its own colour goes idle as the keeper moves 26 away, gets a new facing while idle, then the keeper comes back | idle: view switched off, `IsReady` false, `Ride` refused; woken: the animator faces the new way, no cart part on, its own colours on, nothing frozen; riding it works, and it's drawn and faces the ride's way |
| Waking (view frozen) | the same with `IdleSwitchesOff` off, and then a ride started on the frozen donkey directly | idle: frozen but on; woken as above; ridden from frozen: drawn, animated and facing the ride's way (the invisible ridden donkey) |
| Mounted while idle | a donkey frozen off screen facing away is brought to the keeper and ridden in the same frame (the showcase's H) | the same-frame ride waits (refused); ready within 3 s; ridden, it's drawn and faces the keeper's way |
| Reused views | a donkey and a 0.6 foal fall asleep (keeper 42–50 away), are removed, and two new ones are spawned | both views parked; parked views taken again (by anyone: the world's own donkeys waking with the keeper take them too); full size and foal size each where they belong; no cart parts; facing as spawned |
| Foals keep up off screen | a parent with two foals (following however far) is ridden 22 away | the foals stay active, and reach the parent by trotting, with no catch-up jumps (a jump gives WARN) |
| No leaks | 80 donkeys (8 with carts) spawned and removed, twice | instances, colour materials, colour textures (also shared), lamps, meshes and cargo holders all back at the start |
| Corpses: drop and stand points | reads the GD points the feature uses | the drop point, the three stand points, the game's spawn and fallback points exist; the walk out is short; what the morgue looks like in this save (door, pallets, count) as INFO |
| Corpses: one body left | a body generated the way the game's node does it, through the mod's drop step | taken; `CorpseLeft` raised; one notification pending, only one; a plain item drop of the very same instance with its inside intact; within a unit of the point; the game's rot timer started; a view (WARN if the keeper is far from the crossroads) |
| Corpses: the patches follow the switch | the switch set on and off through `Mounts.Corpses` | patched only while on: our prefix on the pallet node appears and disappears; whatever override was on the switch (Saddle Up's) is put back exactly |
| Corpses: the notification, swapped once | the feature on, a body left, the game's "New Body" call in the same frame, then again a frame later | our text-and-icon notification shows and the stock one doesn't; a frame later the stock one shows as before (the nun's script path); the icon resolves |
| Corpses: twelve bodies at once | twelve bodies in one frame, as the graph's loop delivers them | twelve drops of count 1, twelve notifications pending, twelve views after settling, no two on one spot, all within 8 of the point at ground level, none merged or lost |
| Corpses: never rot | one body with the setting on, one with it off | the game's never-auto-destroy property and the drop's flag with it on; neither with it off |
| Corpses: an unknown point steps aside | the drop step with a GD point that doesn't exist | false, nothing dropped, nothing pending, the unused body nowhere |
| Corpses: walk decisions | the walk rule with made-up positions, and the game's donkey looked up | unload at the crossroads; the game's walk while a quest talk waits; leave from here when it already stands on its target; the game's walk back from the stand point or the morgue; every other walk untouched; which quest holds the donkey in this save (INFO) |
| Frame time by mode | 300 donkeys around the keeper; 3 s each of: levels with idle switched off, idle frozen, levels off, levels on again | INFO lines only: the frame time and the mod's share in each mode, and what levels and switching off save at this spot |
| Chest: hide, move, show (and off screen) | a test `chest_rough` built beside the keeper is hidden, moved to a free spot inside the camera's view and shown again; the keeper walks 45 away and back; moved back | data and view at the new spot; drawn, registered in the chunk system, held by a chunk around the new spot; zone membership untouched; culled away, drawn again back near it (the view's private state and the camera's facts are logged when not) |
| Chest: zone out and in | the chest with one item leaves every zone list and comes back through the game's own priority order | out: no list entry, not a crafting source, no navigation hole, the item the same object; in: the same zone, exactly one entry, a crafting source with its hole again |
| Chest: ten stand-ins | ten stand-ins of the chest spawned and destroyed | drawn, no solid collider, not interactable in the scene list, town quality unchanged, no stand-in part put into the pool (a temp object destroys its parts even when they came from the pool; the borrowed count is reported) |
| Chest: the ride (step 1) | a test cart ridden to the chest: hint, refusals (cargo aboard, a sawmill chest), lift, set-down (up to five stands against the reach gate), lift again, dismount, unhitch and hitch, the save hook, the cart removed | the handler's rows; hidden, zone-less, no hole, `TryAddWgoData` refused while riding; the cart at 0 slots; the stand-in under the cargo holder with only its trigger on; the chest follows the cart; `ChestLoaded`/`ChestUnloaded` once each; set down shown and zoned with its hole; it goes with the free cart and comes back; stands at the cart for the save and rides again after; the cart removed leaves it standing; contents the same object throughout |
| Chest: the backstops | the chest broken on purpose (hidden and zone-less, nobody's), then the game's save order (hook, pass, take-back) and the after-load repair; a normal chest for contrast | both show and zone it, drawn, contents untouched; a chest standing normally is left alone (its "Please report this line" warnings are announced as the test's own) |
| Chest: the save as the game writes it | with the chest riding a test cart, the real save steps and the game's own serializer in memory, read back into a copy; then the fail-safe lock | in the copy: present, not hidden, interactable, the same items, by the cart on ground, in a zone's list; the side file names it; it rides again after; locked: set down at the cart, pickups refused ("Chests don't ride on the cart."); unlocked; any other riding chest (the player's) lifted back |
| Chest: a zombie using it | a caretaker staged by data in the zombie system's lookup (never in a scene, never saved) with the chest as its target, then a conveyor transporter | refused with "A zombie is using this chest." by the hint's reason and the press, the chest shown and zoned; target cleared, liftable; the zombie gone again, the list's count as before |
| Morgue: the door rule | `MorgueHandover.Decide` alone | the morgue area decides; the morgue door decides before the area is known; another area and door, nothing known, an empty list: no; case and spaces don't matter; the setting in effect names the morgue |
| Morgue: a corpse off the parked cart into the hands | a test cart beside the keeper with a generated body on it; `HandOver` with the switch off, then on, then a second body | off: the body stays on the cart; on: it is in the keeper's hands and off the cart; the second body stays when the hands are full (one slot; an INFO with more); `cur_bodies_count` unchanged; the bodies removed again, the cart removed. The hand-over is the game's own pickup, so its `AddOverhead body_corpse` trigger fires, as it does whenever the keeper picks up a body |
| Cargo: more than two items | a standing test cart; `CartSlots` overridden to 4; four plain items (wood, stone); unhitch and hitch; lowered to 2; two items off | the override applies to the standing cart at once; four taken, a fifth refused; two layers of two, the upper one standing on the lower; the free cart takes all four on a 4-slot inventory and the re-made inventory after hitching has all four back (nothing dropped); lowered to 2: the size stays at the load and a fifth is still refused; two off: 2 slots; the relations recorded (bed, beside, on its support, beside above) and the relative places consistent; isolating two of the four on the standing cart is let go at the next layout (isolation is the ridden cart's, the panel's Cargo tab; a parked cart shows its whole load) and four stay drawn, clearing changes nothing; the override cleared, the items removed, both carts removed |

**After every scenario that moves the keeper:** every donkey in the world with its view on has a view of its own size,
and its cart parts match whether it has a cart. That covers reused views taken by any donkey, not only the test's.

| Fight: the armour on and off while riding | a test donkey ridden; the game's `SetArmorView(true)` then `false` | the ride goes on both ways; in armour the animator's State is Idle/Walk, the body is drawn with an armour frame, no rod, the weapon container at its rest height over the body; the riding pose is back after |
| Fight: mounting in armour, every cart path refused | armour on; a donkey with its cart, a plain donkey and a free cart | the donkey with its cart, a hitch on foot, the style switch and a hitch from the saddle all refuse with "Not the cart in a fight."; the plain donkey rides; the same hitch works once the armour is off |
| Fight: the armour on while driving leaves the cart | driving a test donkey's cart; armour on | the ride goes on in Ride style, the cart stands as a free cart by the keeper, the rider checks as above |
| Fight: a sword attack from the saddle | armour on, the view's sword equipped (no item), `ForceEnterState<AttackSwordDefaultPlayerState>` | the attack state does not end the ride, ends by itself, the ride is still on |
| Fight: build mode dismounts | `SetControlTakenType(ByBuilding, false)` while riding | the ride ends with "control taken ByBuilding"; control given back; ridden again |

| Mounts off: only the donkey offered, registrations kept out | with `Debug.MountsPreview` off (what players have in 0.10.0) | the Mount list holds the donkey, the donkey's Skin list Default, the Effect list two entries; nothing built in is registered; a skin, a mount and an effect registered through the API each return a handle and register nothing, a folder reports 0, disposing changes nothing; `Mounts.MountsEnabled` false. SKIP with the key on |
| Skin: on, off, removed | a spawned donkey given the rat skin, then cleared, then removed while wearing it | the overlay shows and the donkey's own body is kept from rendering; its pictures load on one sheet (one texture more) and are freed when cleared; removed while wearing it, nothing is left |
| Skin pack: a folder read, validated, worn, withdrawn | a temp `gk2skins\pack-test` folder with a skin.cfg, 44 PNGs, a front picture and a `[Tuning]` key, registered, worn, withdrawn by its owner | the report names the pack and its unknown keys; worn: drawn, the front picture above the rider, the `[Tuning]` key set on the wearer and owned by the skin; another owner's withdrawal is refused; withdrawn: the donkey shows again and the choice is kept (the warning it logs is announced) |
| Colours: the player's regions on a mount in its own colours | the worm on the test companion's paper donkey, the settings picture rendered and read back before and after a custom green coat; then the dragon on a spawned donkey with a Muzzle colour | the picture leans pink, then green; the worm's 720 colours are all mapped through the colour cube; the dragon's 21 go through the exact swap and Muzzle changes its three belly colours only (its skin.cfg [Colors]) |
| Skin leaks | skins put on and taken off donkeys many times | textures and materials back at the starting counts |
| Recipe: built from the donkey's frames | the built-in recipe skin Hellfire worn | drawn from the donkey's own frames a few at a time, worn whole when complete; its pictures hold the lava cracks, the eyes' glow and no eye white (the drawing changed, not the colours); removed: the pictures freed |
| Effect: hellfire lit, follows, out, gone | a spawned donkey given the Hellfire effect, moved, the effect cleared, the donkey removed | its rig's emitters are lit, follow the move, go out when cleared; removed while lit: nothing of its fire is left |
| Mount kind from a pack | a temp pack declaring the mount `test-beast` (no cart, no foals, `foal_scale = 0.4`, `[Mount.Tuning]` and `[Mount.Tuning.Cart]`) worn by a donkey | the kind is declared and listed with the pack's babies' size; the wearer is that kind with the kind's values on it, owned by the mount; the cart is refused with the kind's name; a mod's value wins over the mount's and the mount's returns when cleared; a foal of a no-foals mount stays a donkey; the file round trip (a value and a babies' size set, saved to the pack's skin.cfg with its own lines kept and `foal_scale` once, reloaded onto the same kind object and the wearer); withdrawn: the kind stays listed, the donkey is a donkey again with the mount's values off it |
| Colours: a skin over the donkey keeps the donkey's regions | a temp over pack (one 10x10 picture: 80 red pixels named Harness, 20 gold unnamed) worn by a test donkey, a green coat colorize and a blue harness colorize set | the mount group's pairs: the donkey's colours plus the one named colour, the exact swap; the coat colours all changed; the red follows the harness, the gold is kept. Then the world's own drawing read back (`RenderAlone`, added 2026-10-19): the overlay renderer alone through its real material and shader, photographed by a camera at the game camera's angle into a 160x160 texture; 100 to 260 pixels drawn (the picture at the camera's density), the red ones blue, none red; the same render with the swap off (a copy of the material, both swap keywords off): no blue, the same pixel count, and the gold pixels the same colours as with the swap on (judged against that render, because the scene's light is dark at night: the first version judged against the picture's values and failed by night); then the overlay rendered together with the body part: its square lies on the torso, so blue pixels remain only when it sorts above the part (the bug found 2026-10-19: the same order, coplanar, the depth test's pick). The material, keywords and sorting orders in INFOs. The player's own mount's worn skin is read back the same way for the log. Added after Royal Parade showed in the world without its red while the Mods picture had it: no preview outside the game can see that path |
| Mount kind from code | a kind registered through `Mounts.Kinds.Register` with an ability | the ability's Attached, Mounted, Dismounted, Detached hooks run on spawn, ride, dismount, removal; a hitch flips the style and the riding-only value is off the donkey by the next tick; the cart-style value never moves onto the free cart |
| Settings: the Mount, Skin, babies and effect rows | `MountRows`, `FoalRows`, `EffectRows` and a `PerMount` coat row set on a throwaway page and companion | one Skin row per mount, shown for the chosen mount only; the Mount dropdown picks the mount and the companion's skin follows (the mount's first skin, then the one picked); each mount remembers its skin; the babies rows reach the companion; Effect defaults to "As the mount"; one coat row set per mount (the donkey's keys plain, the dragon's suffixed), a per-mount row knows its mount and keeps its own condition apart, the dragon's coat set while the donkey is chosen stays on the dragon's look and comes up as the current one when the dragon is chosen |
| Skins and kinds: the changed events | a recipe skin and a code kind registered and unregistered | `SkinsChanged` and `KindsChanged` fire on both |
| Game creature: Gerry's frames found, scaled and worn | a spawned donkey given the built-in `gerry` skin (`source = game`) | at least 60 of Gerry's 73 frames are found (in memory or through a sprite atlas); the wearer is the Gerry mount; the rest, walk and somersault pictures are drawn at 3x, the head key is empty on purpose; the mount's seat value is on the wearer; `seat = picture`: the seat is the skull picture's own top, above the ground; the Gerry mount's babies are 0.33x and the donkey's 0.6x |
| Hitch and unhitch (added) | `HitchGapPx` set on the donkey, the donkey turned, the cart left and taken again | the way to the cart is against the facing; the cart body sits 0.40 further behind, on both facings; the free cart stands where the cart was drawn; taken again it is drawn there with the gap; cleared, the prefab's distance again |
| Kept-with-save text (added) | the parent wears the dragon skin | the record has `Skin = dragon`; the dragon's own `HitchGapPx` is on the wearer but not in the record |

**At the end of the run:** every one of the world's own donkeys (not the tests') is still there, with the same owner,
number, scene, kind, keep setting and key. No test donkey is left behind.

## Not automated

- **The fight itself** (docs/mounted-combat.md): the armoured rider's look, the swing and hoof sounds, hits landing, being
  shoved, death, the fight's end and the strafe's feel need a real town battle; the scenarios above cover the rules.

- **Persistence through a real save and load.** That needs sleeping and the menu, and the tests don't touch the real
  save. See `persistence-tests.md`.
- **Whether it looks right overall.** A screenshot page per check could be added, like the cargo sheet.
- **How riding feels:** hitches while the player rides.
- **The morgue door itself** (0.27.0): drive the cart in with a body aboard; the body should be in the keeper's hands
  inside, the cart at the door with the rest. The log says which signal decided (the `morgue` area or the door's
  destination point), or why not. Done by hand 2026-10-10: the door signal decided (`tp_RT_morgue_exit`, preset
  `dungeons`) on the dismount frame, with control not yet back, so the body was in his hands before the screen came
  back; the cart stayed at (24.2, 3.6, 20.5) with 0 items.

## Offline: PackerScan

`tests/PackerScan` (`dotnet run -c Release`, after a Release build of the mod) runs the cargo packer (`CargoPacker.cs`) and the
panel's relation edits without the game: one and two items against the pre-layer algorithm, the Tune loop as a fixed point,
a slider moving exactly its relation, every slider over its whole panel range keeping the load's shape (which boxes the
Cargo tab shows), determinism, forced relations, sanity, and the coverage analysis ([cargo-coverage.md](cargo-coverage.md)). Run it with the other test projects after any
change to the packer, the layout file format or the panel's cargo editors ([cargo-layout-layers.md](cargo-layout-layers.md)).

## Runs

| Date | Result | Notes |
|---|---|---|
| 2026-10-01, run 1 | 74 passed, 0 failed, 1 warning | the frozen ride skipped: the donkey stayed active (see run 2) |
| 2026-10-01, run 2 | 74 passed, 0 failed, 1 warning | the reason: after the ride it stood 22 from the keeper's far spot, 37% of the screen past the edge, inside the 40% an active donkey needs to leave. The test's far spot is now taken 30 from the donkey |
| 2026-10-01, run 3 | 71 passed, 1 failed, 1 warning | the frozen ride passed. The "both took a parked view" failure was the test's assumption: the world's own donkeys woke with the keeper and took parked views too. Reuse is now counted for anyone, and every donkey with a view is checked for size and cart parts |
| 2026-10-01, run 4 | **81 passed, 0 failed, 0 warnings** | levels save 5.7 ms a frame with 300 test donkeys (14.1 → 8.4 ms); switched off versus frozen: no measurable difference here |
| 2026-10-09, several runs | **179 passed, 0 failed, 0 warnings** (last run) | the seven chest scenarios added (docs/portable-chest.md). Test fixes on the way: the lock test lifts the player's own riding chests back; the backstops test uses the game's save order; hide/move/show picks a spot inside the camera's view (Grave Zoom at 2x halves it); stand-ins may take idle pool parts; the ride test tries several stands against the reach gate; the per-mod API's made-up GUID warnings announced |
| 2026-10-10, run 1 (API 0.26.0) | 155 passed, 1 failed, 7 warnings | the eight corpse scenarios added, all passed, with Saddle Up's switch on and put back after the switch test; run from the crossroads, so the seven chest scenarios skipped (no zone there) and the foal-follow scenario failed (foals 8.4 and 8.5 from the parent after 25 s, no jumps): both location-bound, to re-run from the yard |
| 2026-10-10, run 2 (yard) | 156 passed, 0 failed, 7 warnings | the foal-follow scenario passed from the yard (location-bound, as suspected). The chest scenarios still skipped: the keeper stood in the yard zone but the chest spot 3 units east was just outside it; the chest tests now pick a spot inside a zone first. Run 3 pending (then the spot is remembered) |
| 2026-10-10, run 3 | 156 passed, 0 failed, 7 warnings | started from `inquisitions_base`, which the game lists as the keeper's zone but which is not a container zone, so no chest could belong anywhere near; the runner now moves the keeper into the nearest container zone at the start when he isn't in one. Run 4 pending |
| 2026-10-10, run 4 | **254 passed, 0 failed, 0 warnings** | started outside any container zone; the runner moved the keeper into the graveyard zone at (38.4, 3.6, 15.6) and back afterwards; every chest and corpse scenario ran; the spot is remembered (`Debug.SelfTestSpot`) |
| 2026-10-10, run 5 | 267 passed, 1 failed, 0 warnings | the two new morgue scenarios (API 0.27.0). The hand-over itself worked (1 handed, cart 0, held 1); the FAIL was the test's own check, which compared the body by object reference, while the game's inventory stores a split-off instance with its own GUID (`Item.Split`). Its cleanup missed for the same reason and the test body stayed in the keeper's hands (a free body in that save). Fixed: GUIDs read from the cart, and the cleanup takes out of the hands whatever was not held before the test. |
| 2026-10-10, run 6 | **269 passed, 0 failed, 0 warnings** | the fixed morgue scenario, with the new check that the keeper's hands are as they were (1 test body taken out of them). The morgue door itself is the hand test. |
| 2026-10-14, 18:37 | 331 passed, 2 failed, 0 warnings | the full run after the hitch event order fix (`MountInstance.Hitch`: `Hitched` before the old cart's `InstanceRemoved`; its new order check passed). The two FAILs were the cargo test's isolate checks on the standing test cart, written 2026-10-10 against a rule of the same commit (isolation holds on the ridden cart only); rewritten to the rule. The run's `[Warning]`/`[Error]` log lines are the scenarios' own inputs (the ids test's made-up owner GUIDs, the chest broken on purpose and the chest kill switch, a corpse drop point that does not exist). |
| 2026-10-14, 18:46 | **332 passed, 0 failed, 0 warnings** | the rerun with the rewritten isolate checks (one check fewer, one more: 332). The same deliberate `[Warning]`/`[Error]` lines as the run before. |
| 2026-10-19, 14:40 (mounts off, `Debug.MountsPreview` off: the players' state, over the 0.9.1 cfgs restored to the dev ones) | 425 passed, 1 failed, 0 warnings, 9 skipped | "mounts off" and "upgrade" green; the nine skin scenarios SKIP; the one failure was the pack auto-reload's baseline (fixed) |
| 2026-10-19, 15:10 (mounts on) | **490 passed, 0 failed, 0 warnings**, 1 skipped ("mounts off") | after three test fixes (settings rows left the dragon chosen; Hellfire already worn by the player's donkey; the worm's hue at night, 285°: the preview is blue-lit at night, an open item) |
| 2026-10-19, 17:20 (mounts on; the 0.11 page regroup, API 0.31.0) | **498 passed, 0 failed, 0 warnings**, 1 skipped ("mounts off") | "upgrade" over two fixtures now: 0.10.0 (50 values, 35 through a moved section or key; the API cfg 460 keys; the side file 5 records) and 0.9.1 (35 values, 31 moved); the live cfg's 143 moved keys carried over (134 "is ... now" lines, 2 renumbered sections, 143 old lines left out), every value equal to the backup key by key; the log's only warnings are the tests' own (announced). Second start on the deployed heading fix: 499 passed (the new "a row with a picture row's label is refused" check), no carry-over lines, nothing left to move |
