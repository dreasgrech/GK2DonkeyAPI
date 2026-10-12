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
   `FAIL`, `WARN` and `INFO` lines and a summary, `[test] done: N passed, M failed, K warning(s)`. The tab ends with
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
| Cargo: more than two items | a standing test cart; `CartSlots` overridden to 4; four plain items (wood, stone); unhitch and hitch; lowered to 2; two items off | the override applies to the standing cart at once; four taken, a fifth refused; two layers of two, the upper one standing on the lower; the free cart takes all four on a 4-slot inventory and the re-made inventory after hitching has all four back (nothing dropped); lowered to 2: the size stays at the load and a fifth is still refused; two off: 2 slots; the relations recorded (bed, beside, on its support, beside above) and the relative places consistent; isolating two of the four draws them as a two-item stack, then beside, with all four still on the cart, and clearing draws four again; the override cleared, the items removed, both carts removed |

**After every scenario that moves the keeper:** every donkey in the world with its view on has a view of its own size,
and its cart parts match whether it has a cart. That covers reused views taken by any donkey, not only the test's.

**At the end of the run:** every one of the world's own donkeys (not the tests') is still there, with the same owner,
number, scene, kind, keep setting and key. No test donkey is left behind.

## Not automated

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
