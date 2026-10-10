# Open and deferred items

What is built but not yet seen in game, what is undecided, and what was put off. As of 2026-10-03, API 0.20.0,
Saddle Up 0.1.0. Tick items off (or delete them) as they're checked; add new ones at the end of their list.

## To check in game (built and deployed, not yet seen)

With GK2 Mod Framework installed:

1. **Choice labels in the Mods menu** (`MenuLabels.cs`): the framework shows each value's plain name, so the API
   writes the page's labels over its dropdowns.
   - Riding speed: Normal (x2 walking speed), Faster (x2.25), Fastest (x2.5).
   - Cart speed: Normal (x1.5), Faster (x1.75), Fastest (x2).
   - Coat, Harness, Paint, Lantern light: "Game default" (not "Game"), "Dark wood", "Warm (game default)".
   - A picked value still sticks after closing and reopening the menu.
   - Every dropdown list of our pages is at most 8 rows tall and scrolls (`MenuLabels.MaxListRows`; the 24 palette
     colours ran off the top of the screen, 2026-10-03).
2. **"Setting a key" note** sits right under Status, before the first key (`MountSettingsPage.AddKeyHelp`).
3. **Backspace clears a key in the Mods menu:** the framework's capture takes Backspace as a key; the API clears it
   at once (`MountSettingsPage.ClearIfBackspace`). The field should show no key, not "Backspace" (if it does, leave
   the page and come back to see whether it's really cleared).
4. **GK2 Donkey API in the Mods list** (its own entry, no Settings button), and Saddle Up's details show
   `Dependencies: dreas.gk2.donkeyapi`.

In a game:

5. **The two speeds** feel right: riding at Normal (x2) as before; Faster and Fastest noticeably faster.
6. **Baby donkeys 2 to 5:** more than one foal at a time has never been seen in game (the siblings' spots around
   the parent exist in `Foals.cs` but are untested). Also: the count going down removes the extra foals, and the
   first foal keeps its save key `"foal"` (the others `"foal2"` to `"foal5"`).
7. **Defaults** (Saddle Up's cfg was reset 2026-10-03): bell key B, bell when setting off off, both speeds Normal,
   no baby donkeys, keys H and G.
8. **Keys:**
   - J does nothing (Saddle Up sets the API's `HitchKey` to None).
   - E (interact) hitches a cart you ride up to ("Hitch the cart").
   - H while driving leaves the cart; when it can't, the keeper says why in a thought bubble.
   - H and G work while walking (the API polls them: `KeyActions`).
9. **Building doors** always dismount (Saddle Up forces `DismountIndoors`; the setting is gone from Saddle Up).
9a. **The door bug, fixed 2026-10-03 (API 0.18.0):** ride into the house and come back out. You shouldn't be stuck
    in the doorway behind the parked donkey and foals. The likely cause is navmesh cuts and holes the donkey views
    left behind. They're now removed, and the log measures them and the colliders around the keeper at each door
    ([foals-indoors.md](foals-indoors.md), "The door bug").
9c. **The "double donkey"** (a still head over the walking donkey): settled 2026-10-03 by measurement, see
    [pooled-parts.md](pooled-parts.md), "The double donkey, settled". To confirm over normal play: no
    `[heads] ... a still head` warning in the log, with and without the cart, foals included.
9d. **The pooled-parts guard** (`DonkeyPrefab.cs`, [pooled-parts.md](pooled-parts.md)), built 2026-10-03:
    - check the log for `Mount: the donkey prefab's clean state read`;
    - `[pool] ... leftover(s)` warnings should not appear (each one names a change the mod didn't undo);
    - `[pool] ... left as it is` lines name objects the prefab hasn't: confirm they're the game's.
9e. **Wheel and hub colours** (API 0.19.0): Saddle Up's Cart section, "Wheel colour" and "Wheel hub colour", each
    with a Custom colour. The presets are first guesses (judge them in game, and the picture). Paint's Dark wood no
    longer darkens the wheels.
9f. **Foals at doors** (2026-10-03): coming in and going out, no two foals in the same place, and none on top of the
    keeper. If one is, the `[foal] ... caught up with` line says "no slot with a clear line" and why.
9g. **Lively foals** (API 0.20.0, [foals.md](foals.md) "Lively foals"): more movement around the parent: varied
    steps and speeds, strides in a row, a quick trot now and then. Judge the values (the `ShuffleFoal*` keys, live
    on the dev panel), and watch for foals caught up in view (wedged) more often than before.
9h. **Fishing while mounted, and ground the keeper is put on** (2026-10-03, [safe-ground.md](safe-ground.md)):
    - ride to a fishing jetty and press E: the ride ends where you stand (`Dismounted (the game walks the keeper)`),
      the keeper walks to the spot on foot, and the fishing pose shows, not a walk on the spot;
    - the donkey stays on land; if the ride had left it off the navmesh the log says `was left off walkable ground`;
    - the donkey still standing in the lake from the bug: E on it brings it to you (`its seat is no place for the
      keeper ... it comes to him instead`), and you don't fall;
    - an ordinary dismount next to a fence or a wall: the donkey doesn't move and there's no `left off walkable
      ground` line;
    - after fishing, getting on again works (no "I'm busy.");
    - hitching a standing cart on plain ground still works.
    - (confirmed 2026-10-03.) Second round, water and carts ([safe-ground.md](safe-ground.md), "Other places
      checked"): drive the cart onto the jetty and get off: the donkey and cart move to land (`with its cart off
      walkable ground`); H there to leave the cart: "The cart can't stand here."; donkeys and foals idling by the lake
      don't wander into it; ordinary dismounts and leaving the cart on plain ground work as before.
    - Fence regression (fixed 2026-10-03, safe-ground.md): riding along a fence, G brings the cart and hitches it;
      a cart standing by a fence hitches when you ride up to it; no `hitching ... refused` lines.
9i. **Rig flickering left-right after getting on while moving** (2026-10-03): the user's repro, donkey (or cart)
    facing left, keeper holding right, E while still moving: the rig flipped every frame until the key was let go.
    Cause: `SetFacingDirection` at getting on leaves `PlayerData.Direction` stale (the game copies the animator's
    direction into it only when that changes), and `Follow` read it. `Follow` now reads the keeper's animator
    (`KeeperDrawnDirection`). Confirmed in game and in the log 2026-10-03 (one turn, the stored direction stayed
    stale underneath); the temporary `[facing]` probe is removed. Still to see: hitching while moving.
9j. **Rider motion** (API 0.21.0, [rider-motion.md](rider-motion.md)): riding at a walk, the rider bobs a little more
    and a step late, his hands stay steadier and move with the strides, his head settles after the body. Judge the
    amounts on the dev panel (and whether 0.5 px hand sway shows at all). Check both facings, stopping mid-stride
    (everything back in place at once), getting off (no hand or head left out of place, then walking on foot looks
    normal), the cart style unchanged, and no `rider motion found ...` warning in the log.
9k. **Foals cross the bridge by the house** (2026-10-03, [foals.md](foals.md) "The bridge by the house"): both ways,
    with no `no step fits` or `no way around` line at (-21, 6, 19); they walk on the deck, not in it (`StandHeight`, built 2026-10-03), and ordinary ground looks as before.
9l. **The cart on a hill** (2026-10-03, the user's video `2026-10-03 11-48-07 PM`): going down, the cart seemed to
    float; going back up, a wheel went into the ground. None of the day's changes touch the ridden cart (its height is
    `FollowCartParts`, unchanged since WholeCartPitch, 2026-09-29). Measuring first: Debug tab, "Log the cart's wheels
    against the ground" (`Dev/CartGroundProbe.cs`, session only), then drive down and up that hill. Each `[cartground]`
    line gives every wheel's drawn bottom against the navmesh and the raycast surface under it.
    **Measured 2026-10-04**, three runs of the probe on the same hill. Where the root's height comes from, in Cart
    style: SnapToGround is off, so the root keeps the keeper's height. But the root stands about 0.4 away from him
    (Depth 0.24, CenterOffset 0.48, RiderOffsetPx −8): log, keeper at (20.81, 15.51), root at (20.49, 15.75). On the
    slope the root is therefore 0.1 or so above or below the ground under itself. The cart's parts hang from the root
    (on the flat, the wheels' bottom is the root's height).

    Two lift rules failed, each taking a ground difference against a point that isn't the root's ground:
    - against the donkey's body point: wheels +0.10 going down, −0.06 going up;
    - then against the root's own spot: −0.12 going down, +0.17 going up (the user's video `12-04-44 AM`: the reverse).

    **Fixed** (deployed 2026-10-04, 00:11): the lift is the walkable surface under the cart (layers 11 and 12, as the
    game's own `RaycastUtils.TrySnapToTheGround`) minus the root's drawn height, whatever gave the root that height
    (ridden, parked, unhitched, reloaded). Checked against all four measurements before building:

    | Run | Lift then | New lift | Predicted error | Measured |
    |---|---|---|---|---|
    | 1st, down | 0.387 | 0.294 | +0.093 | +0.098 |
    | 1st, up | −0.387 | −0.327 | −0.060 | −0.065 |
    | 2nd, down | 0.170 | 0.296 | −0.126 | −0.122 |
    | 2nd, up | −0.166 | −0.336 | +0.170 | +0.165 |

    The surface is searched from 0.6 above the root (`CartRiseWindow`: the slope climbs about 0.35 to the cart body
    here) down to a unit below it, so another level beside the road isn't taken.

    The probe's wheel gap is now nearly true by construction (same raycast; only the wheels' 0.42 sideways offset
    differs), so the video is the test:
    - the hill both ways;
    - the cart on the bridge;
    - a cart left standing on the hill after getting off.

    **Confirmed 2026-10-04** (the user: "looks great"; 55 probe lines, down and up twice each way): every wheel gap is
    between −0.005 and +0.005 on the slope and the flat, the applied lift always equals its target (no lag), lifts
    are 0.26 to 0.31 down and −0.21 to −0.35 up, and there are no warnings in the log. The pitch touched its 20° cap
    once (lift −0.353, it needed 20.3°). Still to see: the cart on the bridge, and a cart left standing on the hill.

    Known, not touched: in Cart style the donkey sprite itself is off its ground by the same ±0.1 on this hill
    (SnapToGround off); nobody has reported it. The earlier note that the pitch hits its 20° cap came from the wrong
    0.387 lift: the right lift (about 0.33) needs about 19°.
9m. **The ridden donkey on the ground surface** (2026-10-04, [slopes.md](slopes.md) "Built instead: A"): on the
    hill, ridden without the cart, the donkey's middle is on the ground (no float of a few pixels going down); flat
    ground and the bridge look as before. (The slant, B and C, was tried and removed.) Confirmed by the user 2026-10-04.
    Then the same for foals and standing donkeys (`StandHeight` takes the surface slightly below the navmesh too):
    - foals at the foot of the hill stand on the ground;
    - the bridge deck still holds them;
    - a lake shore doesn't drop them onto the bed.
9n. **Never locked in behind a wall** (2026-10-04, [safe-ground.md](safe-ground.md) "Locked in behind a rock"):
    - first, next to that ore in RuinedTemple (and by a fence, a wall, a building): Debug tab "Write what the move
      check says around the keeper". Every `[reach]` spot behind the rock must be refused, the open ones allowed;
    - mine the ore while riding, then get on again: the donkey is moved to the keeper's side (`... where the keeper
      can't reach it`), and getting on logs `the keeper may move from ... to ...` or the donkey comes to him;
    - ordinary getting on and hitching still work; any refusal says why (watch for one test refusing everywhere, e.g.
      `the game's walk grid has no free cell`);
    - riding along a fence and hitching there still works (the 10-03 regression).
    - ride into the house and into the church (with and without the cart): the donkey stays on the doorstep outside
      (`... stays where it stood ...: the keeper is not on it`), never inside next to you. **Passed for the dungeon
      door with the cart, 2026-10-05** (log: `stays where it stood at (23.87, 3.60, 20.56): the keeper is not on it
      (..., a door)`, the foals came in and went back out). House and church still to see.
    - after the second fix (footprint, levels): mining the ore while riding parks the donkey fully clear of the ore,
      on the floor (`moved ... to (..., 6.00, ...)`, never on the rock), or on the keeper's spot.
9o. **One hint at a time** (2026-10-04, `InstanceControls.Find`): the mod's hints ("Ride", "Load on cart", "Take
    from cart", "Hitch the cart") show only while the keeper walks freely (the game's `FreePlayerState`; not working,
    building, planting, on a ladder or attacking) and only when the game shows no hint of its own at his target or at
    what he works on, for either key (`Wgo.GetWidgetData`'s test: E, F, events). Check:
    - holding F at a crafting station next to a donkey: only "[F] Craft";
    - facing an ore or a tree with the donkey beside it: only the game's "[F]"; turn or step to the donkey and "[E]
      Ride" shows (and E rides only while it shows);
    - normal "Ride", carrying a body to the cart ("Load on cart"), "Take from cart" and "Hitch the cart" still show.
9p. **Zombies step 1, safety** (2026-10-05, [zombies-on-cart.md](zombies-on-cart.md) §6 step 1). **Passed in game
    2026-10-05** (the user: "looks good"; log: Ride and Cart, no warnings, no zombie set down). The Debug-tab and
    sheet guards weren't exercised:
    - riding (Ride style, and Cart style), a zombie at its job shows only "[F] Inspect", no "[E] Take"; E does
      nothing, and it stays at its job. On foot, "[E] Take" works as before;
    - inspect (F) on that zombie still opens its window while riding;
    - the Debug tab's test unload never discards a zombie, and the test load refuses `body_zombie`;
    - the cargo sheet: piles and scripts don't see its test items, and a save during a run aborts it (`[sheet]
      aborted (save)`);
    - no warnings from `zombie take check` or `hiding the zombie's take hint`.
9q. **Zombies on the cart, steps 2–6** (2026-10-05, [zombies-on-cart.md](zombies-on-cart.md) §6 and its test
    list). Watch the log for `linked to its copy`, `drawn lying`, `zombie box`, and never `LOST`, `no zombie behind
    it` or `stands in the world … instead` (that last one means a rescue ran: report it).
    **Round 1 passed (2026-10-05, log):** no warning or error of any kind, game included.
    - Both zombies (`zombie_name_14` head 1059 / `hed_lut_02`, `zombie_name_28` head 1055 / `hed_lut_04`) went on
      the cart and were drawn lying. Each relink kept `9 item(s) inside`: after pickups, doors (re-summon), loading
      on foot and the save.
    - Measured box 0.98 × 0.32 × 0.60, as the asset bundles said.
    - Handed off the cart by the game twice (station), set down behind the cart (spot from the zombie's own length,
      came to rest 0.3–1.4 behind), taken into the hands on foot and loaded back with E.
    - Through doors with 1 and 2 aboard. Saved and woke with both on the standing cart: lent to the save, back on
      the cart after it.
    - Saddle Up's donkey parks with its cargo at a dismount (not an API summon), so zombies stay on the standing
      cart; test 7's "set down" applies to API summons only.
    - **Found (the user, round 2):** with two zombies aboard a working zombie showed no "[E] Take" (right), but
      after setting one down it came back only after turning away and back. The game rebuilds a hint only when asked
      (`Wgo.DrawWidgets`); now asked at every change of the ridden cart (`Rig.RefreshTargetHint`). Built, to test:
      set one down while facing the worker, and "[E] Take" appears at once; load one back and it goes.
    - **Round 3 (2026-10-05, log):** no warnings or errors. The test zombie (`zombie_name_29`, made from the body
      definition `zombie_fighter_pike`, 13 items inside) lay on the ground. With the cart full, E on it gave `cart
      full, body_zombie dropped in front of the keeper` each time, the same as any ground item (the zombie's own
      hint "[E] Take / [F] Inspect" shows there; kept, so "The cart is full." still explains it). All three
      zombies swapped on and off the cart (station hand-ins, set-downs behind the cart) with their item counts
      unchanged (9, 9, 13).
    - Perf in that session: the mod at 1–2.4 ms a frame, all of it the open dev panel (Window 0.8 ms, Debug tab
      ~0.5 ms and 60–110 KB/frame of GUI garbage). Dev only; the core's own Tick stayed at 0.2 ms.
    - **Round 3 passed (the user):** the hint redraw (set one down facing a worker: "[E] Take" at once; load one:
      gone) and the setting off (the cart refuses zombies; no worker taken while riding).
    - Fixed on the way (2026-10-05): a quit-time warning, `the donkey prefab could not be read (not found)`. The
      prefab audit reads the prefab on the first view release, which in that session was at the quit, with the asset
      system torn down; `DonkeyPrefab.Ready` now skips while `Plugin.Quitting`.
    - **Found while tuning (the user's video, 2026-10-05 00:53):** moving the top zombie of a tuned
      `body_zombie+body_zombie` pair swapped the two (their heads changed places). The packer orders a pair by
      footprint area, then id, then load order. Each view is measured on its own, through world space, so two equal
      zombies differed by float noise that moved with the views, and the order flipped on every slider move. Areas
      are now compared in whole square centimetres, so equal items fall through to the load order, which doesn't
      change. Also: the `zombie box` line is logged only when a view is (re)drawn, not on every repack.
    - **Found while tuning (video 2026-10-05 01:04):** dragging the lower item's "up" past the other swapped the
      two slider blocks under the mouse (the panel lists the higher item first), so the drag went on with the other
      item and the two leapfrogged up. An old panel bug, seen now because zombie pairs get tuned upwards. First fixed
      by re-sorting only while no slider is held. Then, at the user's request (re-sorting after each release was
      still confusing), the blocks follow the pair's arrangement, never the live heights: stacked, the item the
      packer put on top first (as on the cart; the packer's order is fixed), otherwise packing order. The "(on top)" /
      "(bottom)" tags, read from the current heights, are gone too (they flipped as one item passed the other); two
      of the same item are labelled "(1)" and "(2)" by their fixed block.
    - **The sheet's stand-in zombies (built 2026-10-05, to test):** with an empty cart, Debug tab, "Photograph all
      cargo combinations". Check:
      - `[sheet] stand-in zombie … made` twice;
      - a `body_zombie` row and column, and the 10-head strip;
      - `stand-in zombie … removed (the sheet ended)` twice;
      - no `left the cart`.
      Then dismount mid-run once: `[sheet] aborted (dismount: …)`, both stand-ins removed, nothing left on the
      standing cart.
    - Not yet: tuning. Already done: a third zombie with a full cart (only two in the save; a Debug-tab "Spawn a test zombie" button is
      built for it), the setting off, and tuning.
9b. **Baby donkeys come indoors** (0.18.0): the test list in [foals-indoors.md](foals-indoors.md). Check especially
    the `[indoors]` timing line ("control not back yet"), small rooms, and doors to other scenes and the church.

Without GK2 Mod Framework (move `GK2.Framework.dll` and the `GK2.Framework` folder out of `BepInEx\plugins`, game
closed):

10. **The new paging** (`SettingsRows.PlanPages`): 12 rows a page like the game's own; only shown rows count;
    the fewest pages; a section continues on the next page ("(more)") only when keeping it whole would cost a page;
    at least two rows on each side of a cut. Expected with the defaults: 3 pages (Keys + first 4 of Behaviour |
    Behaviour (more) + Donkey | Cart + Sound); more with Custom colours on, fewer again when they're off. Checked
    only in an offline copy of the planner. With "Baby donkeys come indoors" (0.18.0), page 2 has 8 rows (10 with
    Custom colours), so still 3 pages.
11. **Speed labels fit** the game's narrow value field ("Fastest (x2.5 walking speed)"); if they're cut off,
    shorten them for that window only.

## Undecided

- **Rebinding in the Mods menu may fire the key's action:** e.g. setting Ride to G might also bring the cart. The
  API's key actions pause only during the game window's own capture (`SettingsRows.CapturingKey`), not the
  framework's. If it happens, extend the pause to the Mods menu's capture.
- **Version numbers:** GK2 Donkey API's plugin version is still 0.1.0 while its API is 0.17.0; the Mods list shows
  0.1.0. Align them before release (and decide whether Saddle Up's dependency should then name a minimum version).
- **The game's Settings window has no text row:** the donkey's name can't be edited there (only in the cfg or in
  the Mods menu).
- **Foal sounds:** investigated, nothing built ([foal-sounds.md](foal-sounds.md)). Waiting on the audition
  (`C:\ClaudeCodeTemp\GK2-foal-sound-audition\`): the pitch, whether a filter is worth it, which calls.
- **The game's sounds:** all 6,393 clips extracted 2026-10-04 ([game-sounds.md](game-sounds.md)); a 66-clip donkey
  shortlist waits for the user to listen (talking-donkey lines, footsteps by ground, cart loops, bells).
- **Showcase video** ([showcase-video.md](showcase-video.md), revised 2026-10-04): shot list updated, automation
  planned (a dev "director" plugin for each shot's setup, OBS and ffmpeg scripts), nothing built. To decide:
  landscape or vertical, music or game audio, Saddle Up only or the API too. Shot 7 waits for the door fix (9n).
- **Getting-on, getting-off and hitching sounds (built 2026-10-05, the user's picks):**
  - Each style has its own (keys `Mount*`, `Dismount*`, `Hitch*`): the donkey alone `donkey_lips#1` on getting on
    and `donkey_lips#2` on getting off; with the cart `ladder_climb_start` and `ladder_climb_finish`.
  - Hitching plays `equip_tool`.
  - No getting-off sound at doors, scene changes, failed re-summons or style changes.
  - A sound id `id#n` plays only clip n (`MountSounds.PlayClip`: only that sample is enabled for the play call).
  - Rows and previews are on the Sounds tab. To hear in game.
- **The cargo thump when loading on foot (found 2026-10-05, fixed, to test):** loading a standing cart with E made no
  sound. `MountSounds.CargoLanded` asked only for the ridden cart (style and position). It now plays for the bound
  instance (`Mount.Current`: the ridden cart, or the standing cart being loaded), at that cart, with its settings.
  Test: load on foot, and the thump plays at the cart; pickups while driving still thump.
- **Label over the wrong donkey (found 2026-10-05, fixed, to test):** walking up to Saddle Up's donkey showed
  "[E] Ride RSK" (its name, right) over a panel-spawned donkey (wrong place). `Instance.HintPoint` cached the view's
  `BubblePoint` and never let go. When the view was parked in the view pool (Tiers) and handed to another donkey, the
  cache still pointed into it. Now cleared when a view is parked, and only used while it is inside this donkey's
  current view (`IsChildOf`). Test: with several standing donkeys (some far enough to be parked and woken), every
  "[E] Ride" sits over its own donkey.
- **Showcase director, time and weather** (built 2026-10-05, [showcase-video.md](showcase-video.md)): F9 window,
  untested. Check: the presets change the light and the HUD wheel; the real time stays (the label); back to the real
  time; the weather list (`[director] N weather states: …`), holding one, back to normal.
- **Zombies on the cart** ([zombies-on-cart.md](zombies-on-cart.md), investigated 2026-10-04): possible with
  changes listed in the doc. Decided: two at once, fully tunable like every item, setting on by default. Key catch:
  the inventory add copies a zombie's organs and gear away from it, so every add needs a relink. Second-round audit
  done; the build plan (§6) waits for approval. Found on the way, in the current build: in Ride style (or with a
  full cart), E on a working zombie pulls it off its job and lays it on the ground (§5 finding 4). Zombies are now unlocked in the user's save, so the old zombie tests can run.
- **More than two items on the cart** ([cart-capacity.md](cart-capacity.md), investigated and built 2026-10-10, API
  0.28.0): `CartSlots` (2 / 4 / 6 in Saddle Up's "Cart capacity"), the packer in layers of two, the door re-add and
  the chest restore fixed, self-tested. To check in game: four crates, four logs, two bodies and two logs, four
  zombies (the lantern pole, the rider), the two-item pictures unchanged, a door and a save with four aboard. Then
  decide the shipped default (2 now) and whether the Carrier perk should count (option B, not built).
- **Tuning stacks by hand** ([cargo-layout-layers.md](cargo-layout-layers.md), investigated 2026-10-10): a pair entry
  holds one relation (beside or on top), so six logs need both. Recommended: both relations per pair as the unit
  of tuning, support-aware placement, relation blocks and an isolate button on the panel, the sheet picturing
  each pair twice. Steps 1-4 built 2026-10-10 (file v2 and migration, packer from relations, the sheet's second picture per pair, the panel's relation rows with Isolate). To check in game: the sheet (two-item pictures unchanged), six logs, four crates, three logs, a crate over a log and a slab; then calibrate the on-top relations.

## Deferred

- **The doorstep bug:** found and fixed 2026-10-03 (see 9a above). The donkey is still parked on the doorstep after
  "Dismounted (indoors)". That's harmless now it no longer blocks the way, but it could be parked a little off the
  door if it looks odd.
- **"Take from cart" and the several-items perk:** load-on-foot is built (API 0.12.0) but not tested in game; the
  zone outlines are still on for testing ([load-on-foot.md](load-on-foot.md)).
- **Preset colours are first guesses:** judge them in game (Coat, Harness, Paint, Lantern light presets).
- **No offline test for the game's Settings-window row names:** the native pages rely on the game's row and
  component names, guarded only at run time (a failure logs once and leaves the settings in the cfg).
- **`dist/` at release:** rebuild here, and in GK2QOLMods, where it's stale.
- **The herd test mod** (`examples/GK2DonkeyHerd`) still polls its own keys and clears its tuning in `OnDestroy`;
  it could use the API's key actions instead.
- **Earlier showcase tests:** zombie, scene-door and save-while-mounted tests, and the persistence check J8
  ([persistence-tests.md](persistence-tests.md)).
