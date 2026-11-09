# GK2 Donkey API: performance

Measured 2026-10-01 on the Steam build 25601286. The save had 63 instances: 10 families of a donkey and 4 foals, plus
other donkeys and carts; 43 of them foals. The game runs at a frame cap of about 117 fps (8.5 ms) on this PC.

## The profiler (`src/GK2DonkeyAPI/Perf.cs`)

**What it measures:** Stopwatch timestamps around each part of the mod's frame.
- Each part's time is summed per frame and averaged over one second.
- For each part it reports the average, the worst frame of that second, and how often it ran per frame.
- Parts nest, so each number includes the parts inside it: Tick contains Stand, which contains Shuffle, Follow, Line,
  Ground and Free.
- **Panel** is the panel's own OnGUI code. **Window** is the window's contents, which IMGUI runs after OnGUI returns,
  so they aren't in Panel.

**Where to read it:**
- The panel's Performance tab (its own tab since 2026-10-10; it was a section of the Debug tab).
- `[perf]` lines in the log every 10 s, once that switch is on. They keep coming with the panel closed.

**Two experiments,** both for the session only:
- **No shuffle:** switches off the mod's movement work.
- **Hide standing donkeys:** switches off their drawing and animation, to show the game's own cost of displaying them.

## Findings

| Situation | Frame | The mod |
|---|---|---|
| Panel closed, 63 roaming | 8.5 ms (117 fps, the cap) | 0.75 ms: Shuffle 0.35 (Follow 0.15), Pose 0.16, Colors 0.16 |
| **Instances tab open (before)** | **32–37 ms (27–31 fps)** | Panel 0.74 ms, plus about 27 ms in the window contents (not measured then) |
| Instances tab open (after pass 1) | 8.5–9.1 ms | Window 0.9 ms folded, 1.7 ms with rows opened |
| No shuffle / hidden views | 8.5 ms | Movement 0.39 ms; hiding changed nothing at the cap |

**What made the game choppy: the Instances tab.**
- **The cause:** it laid out about 63 rows of IMGUI controls with long wrapped text, fresh status strings and a
  nearest-cart search per row, several times a frame.
- **Not the donkeys:** with the panel closed, 63 roaming donkeys keep the game at its cap.

## Changes

- **Pass 1:**
  - **The Instances tab:**
    - groups fold, and a group of more than 6 starts folded;
    - each donkey is one compact line with a Details button;
    - full controls appear only on opened rows and on the donkey being edited.
  - **Colours:** sprites are checked five times a second instead of every frame (0.16 → 0.02 ms).
  - **The log:** foal and shuffle lines are capped at 4 a second, and the next line says how many were skipped. It
    had been 10–19 lines a second.
- **Pass 2:**
  - **World markers:** text and size are cached (0.56 → 0.05 ms).
  - **The panel's performance summary** (the Performance tab) is built once a second.
  - **Writing a standing view's position and facing only when they changed** was tried and reverted. It saved 0.02 ms,
    and every write is kept as it was.
- **Foal behaviour found during the measuring:** foals disappearing and sprinting in crowded families.
  - **The data:** every failed step pick was "outside the area" or "near another donkey", never ground. Yet three in a
    row counted as wedged, and that teleported the foal back to its parent, even in view.
  - **Fixes:**
    - only picks blocked by the world count as wedged;
    - a step back towards home is allowed when the foal is outside its area;
    - a catch-up happens only out of sight, unless truly wedged;
    - within about 1 unit of its spot a foal walks at about 1 u/s, and trots at full speed only from 4 units out.

## Many donkeys scattered around the world (2026-10-01)

**The test:** the herd test mod's scatter key (L) adds 50 donkeys at random up to 60 units around the keeper. They're
not kept with the save. The game was still at its 117 fps cap, because uncapping hadn't taken.

| Donkeys | Frame | The mod |
|---|---|---|
| 63 | 8.5 ms (the cap) | 0.5 ms |
| 163 | 8.5–9.1 ms | 0.8–1.0 ms |
| 363 | 12.3–14.1 ms (71–81 fps) | 1.9–2.0 ms (Shuffle 1.0, Pose 0.45) |

**What it shows:**
- **The game's share is the large one:** at 363, about 11 ms of a 13 ms frame went to the game updating, animating and
  culling 363 donkey views.
- **Why the game can't cull them itself:** our views are spawned outside the game's chunk system
  (`ignoreChunkRegistration: true`, then `UpdateChunkVisibility(true)`), so its distance culling never hides them.
- **The mod's own cost:** about 5 µs per donkey per frame.
- **Pressing L hitched:** 50 views made in one frame, a 20 ms frame.

### Pass A (no behaviour change)

- **Families:** `Children()` and the sibling index are cached until a parent link or the set of instances changes
  (`Mount.structureVersion`; `Parent` is a property that bumps it).
- **Neighbour lookups:** `NearOther` walks only this frame's instances of its own scene (`Mount.InScene`).
- **The API's lists:** `Mounts.Instances` and `InstancesOf` are cached per change. A new list is made per change, so a
  list a caller holds stays a snapshot, as before.
- **Unloaded scenes:** instances there tick twice a second instead of every frame.

### Pass B: levels (`src/GK2DonkeyAPI/Tiers.cs`)

| Level | When | What it does |
|---|---|---|
| Active | ridden, edited on the panel, within 15 of the keeper, or on screen (30% margin beyond each edge) | everything |
| Idle | the keeper's scene, off screen, up to 35 | the view stays (no pop-in when the camera turns), frozen: no shuffle, no posing, animators off, renderers left out of drawing |
| Asleep | beyond 35 (awake again inside 30) | the view is released through the mod's scene-change path; cargo, colours, settings and data stay with the instance. A foal goes to its still parent once a second |

- **When levels are checked:** four times a second per instance, staggered, and at once after a move (MoveTo,
  ComeToKeeper, a catch-up).
- **Hysteresis:** leaving Active needs 3 more units and 10% more margin.
- **New views:** at most 1 per frame, nearest first (`Mount.ProcessSpawnQueue`). That covers spawns from the API, a
  restore at load, and waking up.
- **Panel:** every number is tunable for the session on the Debug tab ("Levels"), with a switch to turn levels off;
  the readout is on the Performance tab.
  The `[perf]` line has the counts per level and the views made and released per second. Compact rows say "idle (off
  screen)" or "asleep (far)".
- **For API users:** an asleep donkey has no view, so `IsReady` is false and `Ride` refuses. `MoveTo` or
  `ComeToKeeper` brings it near, and it wakes on the next frame.

### Measured, uncapped (2026-10-01; the NVIDIA cap removed)

| 363 donkeys, the same spot | Frame | The mod |
|---|---|---|
| Levels off | 64–72 fps (13.8–15.6 ms) | 2.1–2.3 ms |
| Levels on (45 / 38, 3 views a frame) | 96–98 fps (10.2–10.4 ms); 144–158 with fewer in view | 0.83–0.87 ms |
| 63 donkeys, baseline | 200 fps (5.0 ms) | 0.40 ms |

- **Levels on:** about 4 ms off the frame. At that spot 101 were active, 138 idle and 124 asleep.
- **Scattering 50:** still a 22 ms frame. Making views cost up to 12 ms in one frame, and the log took 114 lines a second,
  two per new view.
- **After that:**
  - one new view per frame;
  - the per-view lines (spawned, colours attached, skipped script) share the 4-a-second log budget;
  - idle views also stop rendering;
  - asleep beyond 35, awake again within 30.

  Measured in the next run:

| 363 donkeys, the same spot | Frame | The mod (per-frame work) |
|---|---|---|
| Levels off | 61–70 fps (14.3–16.4 ms) | 1.9–2.2 ms |
| Levels on (35 / 30, 1 view a frame) | 100–104 fps (9.6–10.0 ms) | 0.74–0.79 ms |
| 63 donkeys at that spot, after removing the 300 | 110 fps (9.1 ms) | 0.31 ms |

- **What 300 extra scattered donkeys cost with levels on:** about 0.6 ms over the 63-donkey frame at the same spot.
  Without levels they cost about 6 ms.
- **The levels at 363 donkeys:** 92 active, 60 idle, 211 asleep, so 152 views (239 with the 45 / 38 ring).
- **Scattering 50:** the worst frame of that second fell from 22 to 13–20 ms. Making a view now costs at most about
  4 ms (one a frame), and the log stays at 4 lines a second.
- **Switching levels off:** all 211 asleep views come back at once, one a frame (63 a second).
- **In game:** the user saw no donkeys popping in or freezing at the screen edges while walking and riding through the 363.
- **The panel:** the "mod" figure of 1.7 ms in these lines includes about 1 ms for the open Debug tab. With the panel
  closed, the per-frame work is Tick, 0.75 ms.

## Memory and leaks (2026-10-01)

**What the `[perf]` line shows:**
- **Allocations:** the mod's managed allocations per frame (`GC.GetTotalMemory` around Tick and the panel window), and
  per part, as "+N B", with the dev panel on.
- **The game:** its garbage collections per second and its managed heap.
- **Live counters for every Unity object the mod makes and must destroy:** colour textures (count and KB), colour
  materials, hanging lamps, meshes, cargo holders, and shared colour textures (`Live` in Perf.cs).
- **Slow frames:** a separate line for any frame over 20 ms, with each part of the mod's time in it.

**The measurements:** the scatter test twice, and riding through 363 donkeys.

| | 63 before | 363 | 63 after the 1st removal | 63 after the 2nd cycle |
|---|---|---|---|---|
| Textures | 427 (39 MB) | 1,197–1,400 (110–128 MB) | 441 (40 MB) | 441 (40 MB) |
| Materials | 244 | 616–684 | 252 | 252 |
| Lamps / meshes / cargo holders | 4 / 8 / 4 | 4 / 8 / 4 | 4 / 8 / 4 | 4 / 8 / 4 |

- **No leak:** every counter returns to the same level after each cycle. The game's heap moves between 440 and 520 MB
  without drifting.
- **Colour textures were the big item:** each donkey kept its own copies, about 350 KB, also while asleep. That's
  128 MB at 363, nearly all identical default colours. Building them allocated 700–800 KB of garbage per new view.
  - **The fix:** `Colors.SharedTextures`, keyed by group and content, counted by users; the last user destroys the
    texture. A shared texture is never filled in place.
- **Per-frame garbage:** 6–17 KB a frame (1–2 MB/s at 120 fps). The game collected about every 15 s.
  - A status string rebuilt every frame for every donkey near the keeper is now cached.
  - The per-part allocation figures are there to find the rest.
- **Riding through the herd:** views were made and released at up to 11–14 a second. Making a view took 2–5 ms in its
  worst frame (the view 1.7–2.4, parts up to 1.4, colours up to 1.2). Most slow frames while riding (21–34 ms) had the
  mod at about 1 ms, so they were the game's own.
- **Found by the ride:** a donkey mounted while idle stayed invisible, and then faced the wrong way.
  - **Invisible:** riding never runs the level check, so the ridden one is now woken every frame.
  - **Facing:** an Animator switched off forgets its state and parameters by default. It now keeps them
    (`keepAnimatorStateOnDisable`), and the stand pose is sent again on waking.

## View reuse (2026-10-01, `ViewPool` in Tiers.cs)

**How it works:**
- **Parking:** a view released because its donkey fell asleep is put back to the prefab's state (`ReleaseView`, as for
  the game's own part pool), switched off with `SetActive(false)` (the game's own way of hiding a Wgo), and parked by
  scene, kind and size, at most 24 each.
- **Reuse:** the next donkey of that scene, kind and size takes it. It gets its place and facing, stays hidden for the
  frame its parts are prepared in, and is switched on.
- **Excluded:** carts (their data holds the cargo inventory), ridden donkeys and the summon.

**The game's code behind this:** `Wgo.Spawn` leaves a new view inactive, and `RefreshVisuals` (chunk culling) switches
views on and off with `SetActive`. `WgoPart.OnEnable` and `OnDisable` initialise and deinitialise its drawer, and
`AnimationComponentBase.OnDisable` resets to Idle.

**Riding through 363 donkeys:**

| | Before (new views only) | With reuse |
|---|---|---|
| Views made while riding | 11–15/s, all new | 4–18/s, 4–16 of them reused |
| Worst Spawn in a second | 4.6–4.9 ms | 1.5–3.1 ms |
| Worst making of the view itself | 1.7–2.4 ms | 0.8–1.6 ms (switching the parked view on) |

- **What a reused view still costs:** preparing its parts and colours, up to about 0.6 and 0.3 ms.
- **When many fall asleep at once:** only as many as fit are parked. The rest are destroyed as before; one second
  released 53 and parked 15.

## Per-frame garbage, pass 2 (2026-10-01)

**How:** the allocation counter is page-quantized (4 KB), so this pass was a code review of everything that runs every
frame, checked against the game's code where it was called.

**Found and fixed:**

| Where | What allocated | Now |
|---|---|---|
| The stand loop, `ForEachInstance` | `instances.ToArray()`: a new array of every instance, every frame (8 bytes each) | reused lists filled by `Add` (List.AddRange copies through a temporary array on this runtime) |
| Loops over `Mount.All` (controls, panel, API) | foreach over `IReadOnlyList` boxes its enumerator | `Mount.All` is the `List` itself |
| Cart parts (4 carts) | `Transform.name` read for each part, twice a frame: a new string per read | `CartPart.IsContainer`, set once |
| The cart's ground check | `collider.name` per raycast hit, and a joined source text per check | cached per collider and per pair |
| The lantern (each lamp) | `sharedMaterials` (a new array per read); `ParseColor` trimmed and joined strings; `TryHex`'s `TrimStart('#')` made a `char[]` | `GetSharedMaterials` into a kept list; hex read in place |
| Riding | `Enum.GetValues` every frame in the dismount check (array + boxing); the donkey's sprite name and `Substring` two or three times a frame (`BackHeightPx`); the clip name (sounds); the leg sprite's name | cached array; back height per sprite; clip name per clip; leg sprite compared by reference |
| The interaction hint | the game's `GetIconId` builds a string on each call, and the hint was joined every frame | icon asked twice a second, the hint joined when it changes |
| Unity's IMGUI | the layout pass before each OnGUI, even with the panel hidden | `useGUILayout` off while the panel isn't showing |
| Spawn queue | `List.Sort(Comparison)` wraps it in a comparer per call | one comparer, made once |
| Picking a step | a formatted failure text for every rejected line, only the first kept | built only while none is kept |

**Check for the back-height cache:** if the donkey's body sprites were new objects every frame, the cache would fill.
It logs `[perf] the back-height cache filled` once in that case.

**Measured after (the user's run, 2026-10-01, panel hidden):**

| | Before | After |
|---|---|---|
| Standing among 63: `Tick` B/frame | ~1.3–7 KB | 66–433 B |
| Standing among 63: mod allocates | 2–7 KB | 0.1–0.4 KB |
| Riding through them: mod allocates | not measured | 0.1–0.2 KB (1.4 KB in the 10 s with mounting and views waking) |
| Riding: `Ride` B/frame | not measured | 43–168 B |
| GC per second | 0 (one about every 15 s) | 0 (once 1, while views were made) |

- **The self-tests:** 81 passed, 0 failed, 0 warnings, as before. In the frame-time test with 363 donkeys and levels
  on, the mod allocated 0.2 KB a frame.
- **The back-height cache** never filled: the sprites are reused objects.
- **The panel open** (Debug tab) still makes about 12 KB a frame in its own window. That's the dev panel only.

## The panel's own cost (2026-10-01)

Measured before: with the panel open, its window cost about 1 ms and 100–120 KB of garbage a frame (IMGUI runs it at
least twice a frame, a layout pass and a repaint).

**Changed:**
- **The settings-differ snapshot** (yellow rows and the footer count) compared every setting with its default on every
  layout pass, boxing each value. Now at most four times a second, into a kept list, without boxing float, int, bool
  and string settings; the footer's texts are made with it.
- **Layout options:** `GUILayout.Width(...)` made an option object and an array on every call (72 call sites, every
  row). Now one cached array per width (`Wd`).
- **Enum rows:** `Enum.GetValues` and the enum's name per row and event; now read once per type.
- **The Instances tab:** the groups by mod (new lists, closures, header texts) and every compact row's text were made
  on every event. Now made in a layout event, at most four times a second or at once when an instance or a dormant
  record comes or goes.
- **Measured from now on:** the window's time is split into `WinTop` (tab bar, scope line, snapshot), `WinTab` (the
  open tab's rows, named in brackets) and `WinFooter`.

**Measured after (the user's run, 2026-10-01, 63 donkeys, 10 s lines):**

| Tab | Window before | Window after | WinTab (the tab's rows) | WinTop / WinFooter |
|---|---|---|---|---|
| General | | 0.36–0.45 ms, 31–95 KB | 0.10 ms | 0.08 / 0.17–0.23 ms |
| Riding | | 0.66–0.82 ms, 28–61 KB | 0.30–0.45 ms | 0.10 / 0.25 ms |
| Instances (a group open) | | 0.76–1.04 ms, 31–108 KB | 0.39–0.65 ms | 0.09 / 0.25 ms |
| Debug | ~1 ms, 100–120 KB | 0.66–0.86 ms, 28–108 KB | 0.49 ms | 0.10 / 0.25 ms |

- **The time is down** (Debug about 1 → 0.7–0.9 ms). **The garbage is not**: what is left is Unity's IMGUI itself.
  The footer (three labels and two buttons, none of the mod's code allocating) took 0.25 ms and 2–11 KB a frame:
  about 0.04 ms and 0.5–1 KB per control per event, the word-wrapped help labels the most. The amounts per part
  swing a lot from line to line (4 KB pages, two events a frame).
- **The world markers** (Instances tab) were drawn on every event: 0.33 ms and 12–22 KB a frame for 63 labels. They
  are plain labels, so they are drawn on the repaint only now (built after this run).
- **What would still help:** fewer controls per event. The help texts are most of them: hiding them behind a switch
  would cut the panel's work by about half. The panel is a dev tool, closed in normal play, so this is optional.

## The Mods-window lookups (2026-10-16, Performance Hog)

**Seen:** a 5 s recording of Performance Hog 1.0.1 (a per-mod profiler for GK2, dev machine only;
[performance-hog.md](performance-hog.md) has how to profile with it) put the API at
0.29 ms a frame of 8.3 (6 donkeys, 4 foals), but `Plugin.LateUpdate` had 2-3 ms frames all through the recording.
Our `[perf]` line agreed (Tick worst 2.4-3.2 ms per 10 s) while every part it times stayed at or under 0.2 ms: the
time was outside the parts.

**Measured** with a temporary probe that timed each step of `MountRegistry.Tick` (worst per 10 s, frames over 1 ms):
`LookPreview.Tick` 1.7-2.1 ms and `MenuLabels.Tick` 0.7-1.3 ms, 40 frames in each 10 s (their 4-a-second timer),
while playing with the Mods window closed. Each looked the window up with `GameObject.Find("GK2ModsMenuWindow")`, a
walk of the whole scene that found nothing. Every other step stayed under 0.5 ms apart from one-off peaks (views
built after a load, a door, a foal's route).

**Fixed** (`ModsWindow.cs`, both use it; all three files gone since the options rework, f6cb77b): the window is looked for only at the main menu or while the game is paused
(it opens only from the main menu or over the pause menu, a window on the game's modal stack, and `MainGame` pauses
while one is open), and once found it is kept (the framework makes it once: `ModsMenuWindow.CreateInstance`) and only
checked for `activeInHierarchy`. Probe after: both 0.00-0.01 ms while playing; the Mods menu page, its picture and
its dropdown labels as before. The probe is gone again.

## The first scripted suite (2026-10-16, tools/GK2PerfRunner, docs/performance-hog.md)

RuinedTemple, a 23.4-unit loop, night held (lantern lit), dev panel off, 4 foals; 120 FPS cap, so the frame rate
can't show our cost: the main-thread milliseconds do. `suite-2026-10-16_21-02-31.txt`.

| Run | API ms/frame | API worst ms | API alloc/frame | Mods of the frame's work |
|---|---|---|---|---|
| walk | 0.159 | 0.62 | 2.4 KB | 0.21 of 4.81 ms |
| ride | 0.209 | 0.46 | 2.1 KB | 0.26 of 4.92 ms |
| cart (6 items) | 0.222 | 0.50 | 1.4 KB | 0.27 of 5.21 ms |
| idle | 0.173 | 0.43 | 1.2 KB | 0.22 of 4.74 ms |
| hitch | 0.220 | 17.09 | 3.2 KB | |

- **Steady cost**: 0.16-0.22 ms a frame in every state, worst frames under 0.7 ms; no garbage collection started in
  our code in any run; the game's own collections (about 13 ms) show in each run's longest frames.
- **Cargo changes are the hitch**: `Cargo.Rig.OnChanged` took up to 4 ms a call, and 10-17 ms in the frames where
  the test load put six items on or took six off at once (one call per item, each refreshing the views of everything
  on the cart). A player loads one item at a time: about 2-4 ms for that frame, more the fuller the cart. The take-off
  frames also hold six writes of `BepInEx/GK2-test-loads.txt` (TestLoads.CartChanged, dev only), so they overstate it.
  Not changed (close to release); a refresh of only the item that changed would cut it.
- **H and G**: Saddle Up's handlers 2.2-2.6 ms at worst (the summon and hitch).
- **Memory**: the API held 1.2 MB, +105 KB and +500 objects over the 9 minutes. Caches filling for the first time,
  or a leak: a second suite in the same session tells (Performance Hog compares with the last snapshot).
- **Two more suites in one session** (`suite-2026-10-16_21-18-26.txt`, `suite-2026-10-16_21-29-15.txt`): the steady
  numbers repeat within about 0.02 ms (walk 0.138 / 0.164, ride 0.211 / 0.219, cart 0.219 / 0.233, idle 0.171 /
  0.177 ms a frame), hitch worst 18.9 / 17.5 ms (the cargo changes again). One frame of 3.3 ms in `Plugin.LateUpdate`
  in two runs (cart, walk), once each: not in the parts Performance Hog can see; the `[perf]` line of a pass 2 names it.
  Memory: +99 KB in the first (from a fresh start, as the first session's +105 KB), **+39 KB and +314 objects in the
  second**: slowing, not flat. Caches still filling or a slow leak (about 4 KB a minute); the runner's census (a count
  of every static collection of the API at the start, after the walk run and at the end of a suite, built
  2026-10-16) names what grew. The dev test load's file was opened 109 times and written 108 times in the session
  (TestLoads.CartChanged on every cargo change): dev-only, but in the cargo-change frames.
- **Log**: about 16 lines every two minutes with the foals about (the routine `[foal]`/`[shuffle]` lines below).

## The zone cache held removed instances (2026-10-16, found by the suites)

**Seen:** three suites in one session: the API at 3,770, 4,080 and 4,394 objects (Performance Hog's memory
snapshots), +38-40 KB and about 310 objects every suite after the first. The runner's census of the API's static
collections: everything flat after the first suite except `InstanceControls.zoneCache`, +6 entries every suite
(7, 13, 19).

**Cause:** a cart left standing (H while driving) is an instance of its own; hitched again (G), it leaves the
registry. `zoneCache` (the load-on-foot zones of the standing instances, InstanceControls.cs) is keyed by the
instance and kept the entry, so the removed instance and everything it holds (about 52 objects) stayed alive until
the cache passed 32 entries and was emptied. Six such instances a suite (the hitch run's five, the idle run's one):
bounded at about 33 instances (about 215 KB), but held for nothing.

**Fixed:** `Mount.Unregister` drops the instance's entry (`InstanceControls.Forget`), and `InstanceControls.Clear`
(the game left, every instance gone) empties the cache. **Confirmed 2026-10-16** with three suites in a row
(`suite-2026-10-16_22-34-57.txt`, `_22-44-30`, `_22-54-10`): the API at 3,509 objects after each (+66 KB warming
up, then +1.3 KB and +8 B; before the fix 3,770, 4,080, 4,394), `zoneCache` at 1-2 entries, and the third suite's
census without any growth. The steady numbers as before: walk 0.15-0.16, ride 0.21-0.22, cart 0.22, idle 0.17-0.18
ms a frame; hitch worst 16.5-19.0 ms (the cargo changes, unchanged).

## Still open

- **The per-frame work, about 0.5 ms with 63:** Shuffle, Pose and Ground checks. Not visible at the cap. Worth a pass
  if slower PCs or hundreds of donkeys matter.
- **What the API logs for players** (done since: the routine lines are built only behind `DevDiag`, found
  2026-11-09; a few rarer ones are still built before the budget, row 22 of the 2026-11-09 pass): the routine `[foal]` and `[shuffle]` lines (follows, works out a way round, route
  found, found no step) are written without the dev tools too, up to 4 a second, for as long as foals are about; their
  text is built before the 4-a-second budget is checked. Proposed 2026-10-16: those dev-only, the ones a bug report
  needs kept (can't follow, stood inside, caught up out of sight); not decided.
- **One-off hitches:** calling the cart (G) took 25.8 ms in one frame (Performance Hog bills it to Saddle Up's key
  handler; the work is the API's view build). Not looked into yet.
- **The `[shuffle] frame +N` turn diagnostics** read the body before the frame's own position write. They can't tell
  whether a turn jumps on screen.

## Performance Hog's "Exceptions: 8" on the API (2026-10-17)

Every report shows the API with 8 exceptions, **all created and caught, 0 unhandled**: 6x `NotSupportedException` in
`Corpses.Patch` and 2x in `Corpses.Apply`. Nothing in our code throws them, and `Apply`'s own catch (which logs a
warning) never fired. They come from inside Harmony's `Patch` call: the corpse feature patches its four methods when
it switches on, after the game has loaded (so after Performance Hog started counting), and each late patch creates and
catches two of them. Other authors' mods show the same in other players' reports: 3x `NotSupportedException` in
`GK2Kit.HubHost.Activate` (Clerk Desk's library, which only calls Harmony there) and in `GK2Codex.SaHost.Activate`.
The API's start-up patches don't show, because they're applied before Performance Hog loads. Harmless: once per
session, when the feature is switched on.

## Against another player's reports (2026-10-17)

Six Performance Hog 1.0.1 reports from another player (12-16 October, a different PC, 35 other mods, none of
ours), next to ours (Performance Hog 1.2.0, our PC, scripted suites). This is a ballpark only: the machines, the
saves and the profiler versions differ.

| | Theirs (per mod) | Ours |
|---|---|---|
| CPU a frame | top mods 0.07-0.16 ms (Timbn Core, Stock Limits, Zombie++); all mods together 0.41-0.75 ms of 6.4-10.4 ms | the API 0.16-0.22 ms of 4.8-5.2 ms; Saddle Up 0.003 ms |
| Worst frame | many at 17-164 ms (Corpse Preview 164, GK2 Codex 87, Thoughtful Week 55, Graphics Override 44) | under 0.7 ms in steady play; 17-19 ms only in the test hitch (6 items at once); a player's single item 2-4 ms; H/G 2.2-2.7 ms |
| Allocations a frame | mostly under 0.4 KB; Timbn Core up to 3.6 KB | 1.2-3.2 KB: at the top of the field |
| Log lines | Zombie++ 2,318 in 43 min, the framework 550 | 686 in a dev session with the perf runner (the routine `[foal]`/`[shuffle]` lines) |
| Memory | GK2 Codex 1.6 MB, Zombie++ 510 KB | 1.2 MB |

**The API would be the heaviest single mod in their list by average CPU.** That's expected for a mod that animates
donkeys, foals and a cart every frame, and it's still about 4% of a frame. Its worst frames are better than half of
theirs. The two open items (docs above) are the ones that show here too: allocations a frame, and the routine log
lines.

## The colour swap's cost (2026-11-04, the user's F11)

The question: a skin over the donkey keeps the game's exact colour swap up to `Colors.PaletteMaxColoursOver` pairs,
and the swap's shader loops over every pair for every pixel of the mount's sprites (docs/colors.md); the Jester's six
parts need 71 (docs/mount-colours.md 7.6), and a baby's own shade of a part was wanted "unless it's a performance cost
we can't afford". The self-test measurement "the colour swap's cost" (`MeasureTests.PaletteCost`, the Measurements
group; BepInEx\GK2-palette-cost.tsv): 21 test donkeys in view, at their size and then twice as big (four times the
pixels each), frame times uncapped (no vsync, no frame-rate target), every configuration twice with the second round
reversed, 2 s each. Medians over both rounds, in ms:

| Configuration | Pairs | 21 donkeys | Twice as big |
|---|---|---|---|
| As the game draws them (the swap was on: see below) | 42 | 5.93 | 6.07 |
| The exact swap, a coat colour | 42 | 5.72 | 5.88 |
| The exact swap, 22 colours of a skin's own | 64 | 5.90 | 5.94 |
| The exact swap, 30 | 72 | 6.02 | 6.05 |
| The exact swap, 54 | 96 | 5.95 | 6.25 |
| The colour cube, the 30 | 72 | 5.87 | 5.88 |

- **No cost this measures, up to 96 pairs.** A frame is about 6 ms (about 170 a second) in every configuration; the
  spread between configurations (0.37 ms at the most) is smaller than the spread between one configuration's two
  rounds (up to 0.63 ms, the 96 twice as big). The cube costs the same as the swap.
- **The frame isn't the GPU's here.** Four times the pixels changed nothing either, so on this machine the shader's
  work hides behind the rest of the frame; the measurement gives no GPU margin. The game's build keeps no frame
  timing (`FrameTimingManager` off), so the GPU's own time isn't read. A weaker graphics card is not measured.
- **The limit is a ceiling, not a cost for everyone:** a look pays for the pairs it has (the palette is as wide as
  them), so raising it costs nothing to a skin under it.
- "As the game draws them" (nothing set by the test) still ran on the exact swap at 42 pairs: there is no swap-off
  baseline in this table.

## The pass of 2026-11-09: before the release, for lower-end PCs

237 commits since the scripted suites of 2026-10-16 (flying, rider motion, sounds, skins and colour regions, the
journal and options windows, leave-mounts, babies on foot). Measure, agree, fix, measure again.

### Measuring added

- **Parts for the frame's untimed steps** (Perf.cs, MountRegistry.Tick): `Companion`, `HeadWatch`, `Doors` (foals
  indoors, the morgue hand-over), `Scenes` (BuildByScene, the snapshot), `Warm` (Texts/Looks.TickWarm,
  AdvanceBuilds, DonkeyFrames, FacePixels, SoundBank.Advance, the pack watch, RecheckAsideSkins; its known heavy
  insides keep their own parts, LooksBuild and SndStart).
- **The mount crowd** (integration scenario, Garbage group; `tools/GK2DonkeyAPI.IntegrationTests/Crowd.cs`): every
  shipped look (the donkey, Hellfire, Royal Parade, Armored, the dragon, Mecha, Larry, rat, snail, worm), every second
  one with a baby, every third donkey look with a cart, night held so the lanterns are lit, no vsync and no frame
  cap. Phases: none, 12 in view, 36 in view, + 120 scattered up to 60 away, riding through them; each the frame's
  mean, median, 99th percentile and worst, the mod's ms and KB a frame, the collections, the levels; a row each in
  `BepInEx\GK2-crowd.tsv` (appended, so runs before and after compare). Then all removed and the colour textures,
  materials, lamps, meshes and cargo holders checked back.

### Seen in the dev session's log before any change (17 instances, 12 babies, 120 fps cap)

The mod 1.0-1.1 ms and 6-7 KB a frame **with the Mount Journal open**: `Preview` 0.6 ms and 5 KB of it (the picture
drawn with a camera render every frame, `PrevRender` 0.5 ms; `PrevPose` the garbage), `Stand` 0.26 ms for 16, `Looks`
0.14, `Ride` 0.14. Opening the journal: one 21 ms frame (`Keys` 11.4 ms, the window built in the key's frame, 1 MB
allocated). The `[preview]` midday line every 10 s while it is open.

### Findings of the code review (four reviews: riding and flying; standing mounts and babies; looks, colours and
sounds; the players' windows). Not measured yet unless said.

| # | Where | What | Who pays | Confidence |
|---|---|---|---|---|
| 1 | `Looks.Rig.TickAbilities` (Looks.cs:3217) | a closure object made at the method's entry (the lambdas capture `h`), every active view every frame, abilities or not: about 32 B each, 2-3 KB a frame at 60-100 active. The last of the 2026-10-25 suspects (the key polling, `Flight.SameKey` and full-id `Looks.Find` are fixed) | everyone | high (IL read) |
| 2 | `RowControls.SetValue` (RowControls.cs:173) | every slider step and click in the journal and options writes the whole cfg (Saddle Up's `SaveOnConfigSet` on): 3.5-4.7 ms a write measured for the mount pick, which alone defers its save (`SavePickedSoon`); a slider drag is 13-16 writes | anyone changing a setting; worse on a slow disk | high |
| 3 | `Sounds.TestVoiceTimes` (Sounds.cs:35, :1191) | a test list filled on every call and answer in normal play, emptied only by the self-tests: grows all session and keeps removed instances alive (the zoneCache kind of leak) | everyone with standing mounts | high |
| 4 | `PreviewStage` (PreviewStage.cs:236, :586) | while the journal or colours window is open, a full camera render every frame (culling the whole scene, the midday hold's ~20 global writes) though the walk changes only at its keyframes; `animator.parameters` a new array a frame (:623) | the journal open (0.6 ms, 5 KB a frame, seen) | high |
| 5 | `PreviewStage` (:892, :1277) | the stage is switched on at the first journal open and never off: its copy animates (`AlwaysAnimate`) and its renderers and light stay in the scene; the paper's look rig stays in `Looks.attached` | everyone after the first open | high on the mechanism, cost unmeasured |
| 6 | `Flight.cs:899`, `:1576`, `:1099`; `FlightGround.cs:429`, `:513` | jump and flight trace lines (11-14 formatted values) built every frame and physics step and thrown away for players (`JumpTrace.On` is dev-only); blocker descriptions (names, layer names) built for every candidate | jumping and flying | high |
| 7 | `LooksRecipes.BaseFramesInMemory` (LooksRecipes.cs:120, from `Looks.TickWarm`) | while the companion's skin waits for the donkey's frames (an over skin that erases, a recipe), `FindObjectsOfTypeAll<Sprite>` every 3 s (6-7 ms measured for the same scan in DonkeyFrames), no game-ready gate, never gives up | the main menu, or before a mount has a view, with such a skin chosen | medium-high |
| 8 | `FoalRouteSearch` (FoalRoute.cs:58) | 30-72 KB per route search (six grid arrays up to 64x64, closures, a Stopwatch); at most one a second per following baby | babies working their way round | high on the allocation |
| 9 | `MountRegistry.IsRegistered` (:150) | `instances.Contains`, a linear search, about twice per active baby per frame and in `WantTier`, `SteadyIndoors`, `MountHandle.Exists`: O(babies x instances) | hundreds of mounts | high, small at 63 |
| 10 | `Foals.FreeSlot`/`Taken` (Foals.cs:952, :1007), `FoalsIndoors.PlaceAgain` (:281) | a catch-up tests up to ~40 spots, each against every instance (not `InScene`) with a camera test each; a door does every baby in one frame; `PlaceAgain` a full search every frame per baby while the screen is black | doors, babies far behind | medium |
| 11 | `SafeGround.IsModView` (:505) from `FlightGround.FloorUnder`/`Blocks` | every instance walked for every cast hit, every frame and physics step in the air; `ObjectTop` (`:525`) `GetComponentInParent` before its cache | flying among many mounts | high on the code |
| 12 | `Sounds.Resolve` (:920) | every event re-resolved every 0.5 s per active mount: a `SoundSpec` and 2-3 joined strings each | many mounts in view | medium |
| 13 | `Mount.cs:2489` | a ground probe (navmesh lookup and raycasts) every ridden frame whose answer only a debug log reads; `GroundHeightPair` computed before the surface hits that are used first | riding | high |
| 14 | `Mount.IsAttackState` (:959) | `GetType().Name`, a new string each call, 1-3 a ridden frame, more in fights | riding | medium-high |
| 15 | `Looks.WantedDef`/`StandIn` (:245, :1748) | a skin that isn't installed, or a short id, takes the miss path every read (Substrings, a joined key, a walk of every skin), 2-3 reads per rig a frame | a player who removed a pack | medium |
| 16 | `Tiers.cs:231` | an instance in another loaded scene is always Active (views queued, shuffle, sounds), never idle or asleep | scenes the game keeps loaded; leave-mounts makes it likelier | high on the code, frequency unknown |
| 17 | `OptionsWindow` refresh (:503), `KeyCapture.Clash` | twice a second while open: a signature string and a clash check of every key row against every plugin's keys (BepInEx's LINQ `Modifiers`) | the options window open; grows with mods installed | medium |
| 18 | `MountCompanion.UpdateFoal` (once a second) | two `System.Random`, a closure and a `ToLowerInvariant` per baby per family, a new list per `LeftNow()` | leave-mounts with families | medium |
| 19 | the lantern (`Lantern.ApplyLight`, `LampHang.CopyLook`) | ~12 light and renderer properties and property blocks written every frame per active cart, changed or not | carts in view | low-medium |
| 20 | `Cargo.Rig.OnChanged` → `RefreshViews` (Cargo.cs:899, :1478) | the known cargo hitch: each item change walks the cart's whole hierarchy with a `.name` per transform, views matched by position so a removal from the middle activates every later one; six changes, six refreshes in one frame | loading the cart | high on the mechanism |
| 21 | `Perf.Measure` (Perf.cs:107) | the profiler is always on: two timestamps per part and call, about 10 parts per active instance a frame plus `Stand` and `HeadWatch` for every instance, asleep ones too (~25 ns each; more on a PC with a slow timer). At 300 instances ~0.15 ms of measuring alone. It also means a run with the dev panel on reads the mod slightly high | everyone | medium |
| 22 | Smaller | `MountsPanel.Tick` closure every frame (closed too); `KeeperMask` 32 Unity calls per call; `Sounds.EmitInner` `LastEvent` join on every play; `DropCartIfRefused` text built before its retry check; `Colors.AllSlotsLut` `sharedMaterials`; a Behaviour override marking colours dirty per instance (O(n²)); `MountRules.Poll` `ToArray` per feature; logs built before the 4-a-second budget (Foals.cs:469, 522, 766, 829, 935; FoalsIndoors.cs:209, 263, 354; MountRegistry.cs:950; Colors.cs:749; Patches.cs:218) | | |

Checked and clean: `OnPreCull` (once a frame, idle rigs skipped), `HeadWatch` (every 6 frames, early outs),
`BuildByScene`/snapshots, idle and asleep `StandTick`, `SafeSpots.Tick`, `KeyActions.Poll`, `Plugin.OnGUI` for
players (`useGUILayout` off), `AdvanceBuilds` idle, `DonkeyFrames`/`FacePixels` once done, colour textures shared,
skin sheets shared and non-readable, `Stand()` writing on change. Struck from "Still open" above: the routine
`[shuffle]` and `[foal]` step lines are built only behind `DevDiag` now.

Not explained by the code: the 2.5 ms `Sounds` frames of 2026-10-25 (the `SndPlay` part answers it in the next run).

### Fixed (the agreed cut, 2026-11-09; built, deployed after the baseline run)

- **1:** the ability hooks are static lambdas fed `h` and the style through `CallAll`: nothing captured, no closure.
- **2:** `DeferredSave.cs`: a row control's value set with the file's save-on-set off, the file written 1.5 s after
  the last change (`MountRegistry.Tick`), half a second after the journal or options window closes (`Soon`: not in
  the closing frame, the 21-24 ms frame of 2026-10-25), and at quit (`FlushAll`). A mod whose file doesn't save on
  set keeps its way. The self-tests don't drive the row controls, so no test reads the file in between.
- **3:** `TestVoiceTimes` filled only while `SelfTest.Running`.
- **6:** `JumpTrace.Frame`/`Step`/`Begin` and the per-jump notes behind `JumpTrace.On`, `traceThing` (a `PathOf`
  every airborne frame) too; `Nearest` and `Shapes` describe their winner once after the loop.
- **13:** `lastCartGround` only with `DevTools.Unlocked` (its only reader is `LogGeometry`).
- **14:** `IsAttackState` compares the state's `Type` with the last one seen and keeps its answer.
- **16:** another scene than the keeper's: Idle instead of Active. Idle, not Asleep, so the view stays and nothing
  pops in when the keeper comes back through the door; the next level check (four a second) makes them active then.
- **20, not done:** the once-a-frame flag would only merge the dev test load's six changes; a player loads one item
  at a time and pays the full refresh each (2-4 ms). What helps there is making the refresh cheaper (the parts
  cached, the container's subtree skipped, views keyed by item), held for the measurements.

Proof: the same run again after deploying (the crowd TSV rows side by side, the suites, the garbage survey).

### The baseline, before the fixes (the user's run, 2026-11-09 01:30-02:40)

**The mount crowd** (`GK2-crowd.tsv`, uncapped, dev panel on):

| Phase | Instances / views | Frame mean (fps) | p99 / worst | The mod | Mod KB a frame |
|---|---|---|---|---|---|
| none of the test's | 14 / 13 | 2.62 ms (381) | 3.43 / 4.2 | 0.05 ms | 0.12 |
| 12 in view | 32 / 31 | 3.46 (289) | 4.27 / 4.7 | 0.21 | 0.53 |
| 36 in view | 68 / 67 | 5.92 (169) | 7.16 / 8.8 | 0.61 | 2.58 (Abilities 1.73 of it) |
| + 120 scattered | 248 / 206 | 6.82 (147) | 8.18 / 10.8 | 1.22 | 3.06 |
| riding through | 248 / 140 | 6.68 (150) | 9.20 / 14.7 | 1.16 | 8.89 |

No collection in any phase. Its leak checks failed the first time on the test itself (the ride left the keeper
elsewhere, 6 of the world's 13 views fell asleep: materials 28 -> 16); the test now puts him back and compares only
when the world's views are back (see Crowd.cs).

**Levels** (frame time by mode, 300 test donkeys, 120 cap): levels on 0.31 ms the mod, 120 fps; levels off 2.02 ms,
102 fps. 104 passed, 0 failed.

**By hand (Performance Hog 1.3.0, dev panel on):**

| Recording | The API ms a frame | Worst | Alloc a frame | Notes |
|---|---|---|---|---|
| a volume slider dragged (17 s) | 0.615 | 6.72 | 9.1 KB | 22 steps, each 5.0-5.5 ms in `RowControls.SliderRow` (file write); `dreas.gk2.saddleup.cfg` written 25 times, 2.8 MB; 1% low 104.7 fps |
| the dragon jumped and flown (36 s) | 0.359 | 4.62 | 3.8 KB | the worst at the start, a cluster of 2.3-3 ms frames 14 s in; no hitch |
| a door with 5 babies (17 s) | 0.214 | 5.46 | 2.2 KB | one 5.5 ms frame going in (the babies placed), then 1.6-2.8 ms frames |
| 6 items onto the cart one by one (11 s) | 0.279 | 7.83 | 0.4 KB | `Cargo.Rig.OnChanged` 2.5-6.2 ms an item |

**The suites** (3 in a row, the donkey, night, dev panel off; `suite-2026-11-09_02-08-30` / `_02-18-42` /
`_02-29-35`; the first attempt stopped on Performance Hog 1.3.0's new `ReportWriter.Write(RecordingResult, out
string)`: the runner now takes the report Performance Hog writes itself, `RecordingResult.ReportPath`). The dragon
can't run the suite (its cart and hitch runs need a mount that pulls the cart).

| Run | API ms a frame (3 suites) | Alloc a frame | 2026-10-16 |
|---|---|---|---|
| walk | 0.283 / 0.279 / 0.265 | 3.5 / 1.6 / 1.4 KB | 0.15-0.16 |
| ride | 0.285 / 0.329 / 0.319 | 1.5 / 1.7 / 1.6 KB | 0.21-0.22 |
| cart | 0.299 / 0.297 / 0.294 | 2.1 / 1.6 / 1.9 KB | 0.22 |
| idle | 0.186 / 0.293 / 0.209 | 6.5 / 0.7 / 0.5 KB | 0.17-0.18 |
| hitch | 0.219 / 0.319 / 0.221, worst 18.3 ms | 18-38 KB | worst 16.5-19.0 |

- **Steady:** 0.27-0.32 ms a frame, about 0.1 ms over 2026-10-16 (the save now has 14 mounts, 11 of them babies,
  against 6 and 4 then); worst frames in steady runs 1.4-1.6 ms; no collection started in our code.
- **Hitches:** only the test load's (`Rig.OnChanged` 10-17.5 ms when six items go on or off at once, the dev test
  load's frames), as on 2026-10-16.
- **Memory:** flat over the series (+0 B, +424 B; 40,400-40,600 objects): no leak.

### After the fixes (the user's run, 2026-11-09 13:40-14:52)

**Regression:** F11 Run all 2050 passed, 0 failed, 0 warnings (the crowd's leak checks pass: materials 28 -> 28,
colour textures 41 -> 41, shared 37 -> 37, lamps, meshes and cargo holders back).

**The suites** (the same three, donkey, night, dev panel off): walk 0.285 / 0.242 / 0.218 ms a frame (before 0.283 /
0.279 / 0.265), ride 0.330 / 0.278 / 0.258 (0.285 / 0.329 / 0.319), cart 0.306 / 0.267 / 0.245 (0.299 / 0.297 /
0.294), idle 0.175 / 0.256 / 0.249 (0.186 / 0.293 / 0.209): suites 2 and 3 about 0.04-0.06 ms (~15%) lower walking,
riding and driving; the first is the warm-up in both. Allocations 1-3 KB a frame in both (Performance Hog's heap
estimate swings; a crowd of 14 has few active views). Memory flat (-56 B, +56 B).

**The mount crowd**, the mod's garbage a frame: 12 in view 0.53 -> 0.41 KB, **36 in view 2.58 -> 0.11 KB**
(Abilities gone from it), + 120 scattered 3.06 -> 1.74 KB. Riding through 8.89 -> 20.07 KB: not comparable (the
scatter is random; 172 views at the end against 140, more woke on the way), and its parts are the waking and
sleeping: Spawn 6.8, LooksTick 3.5, Prepare 2.9, Release 2.4, Abilities 1.8 (the Attached hooks and the `abilityOff`
array of each mount waking, not a frame's closure). The mod's milliseconds as before; the frame times don't compare
between the sessions (the "none" phase itself 2.62 -> 2.83 ms). **Open:** the garbage of views waking and sleeping
(~20 KB a frame riding through a big herd, a collection in 12 s).

**By hand:**

| Recording | Before | After |
|---|---|---|
| a slider dragged end to end | 22 frames of 5.0-5.5 ms (a cfg write each), 0.615 ms a frame | **one** 5.4 ms frame, the write after letting go; 0.580 ms a frame (the journal open) |
| the dragon jumped and flown | worst 4.62 ms, 0.359 ms a frame, 3.8 KB | worst 2.76 ms, 0.373 ms, 3.0 KB |
| a door with 5 babies | one 5.5 ms frame going in | one 11.3 ms frame going in (same scene both sides: the indoors is ~100 units off, so the yard's mounts fall asleep and the babies are placed in that frame; how heavy depends on the mounts near the door) |
| 6 items onto the cart | 2.5-6.2 ms an item | 2.0-2.7 ms an item (unchanged code; varies); opening the journal in the same recording: one 13.9 ms frame (`OpenPanel` 7.5 ms, `BuildList` 3.7) |

The 306 MB "Disk written" in these reports is the self-test run's temporary test packs (`SelfTest.WriteTempPack`,
285 MB) and its settings changes: dev only.

### The cart's item cost, step by step (2026-11-09, after the pass)

A dev-only probe (`Dev/CargoCost.cs`, with the `[perf]` logging): one `[cargo-cost]` line per item on or off, each
step's time and the views' size. Six wood loaded and taken off by hand:

| Step | Before | After |
|---|---|---|
| the loaded body (`SetGameLoadedBody`: every transform under the cart walked by name, 443 -> 2,343 with six views) | 0.20 -> 0.89 ms, growing a view at a time | 0.00 (the two bodies found once per view, `FindCartBodies`) |
| a new item's view: the game's `DropViewAtomMesh.Activate` | 1.40-1.50 (with our walk) | 1.40-1.50 (our walk now of the active parts only: 0.00-0.01) |
| the packer | 3.4 and 1.6 ms on the 2nd and 3rd item, then 0.1-0.3 | the same: a first use a session (later loads in the run: 0.5 ms or under) |
| the rest (mirror, events, slots, the cart parts collected again, the hint) | about 0.2 | about 0.2 |
| **an item on, 4th to 6th** | **2.3-2.5 ms** | **1.75-2.0 ms** |
| **an item off** | **1.0-1.2 ms** | **0.2-0.4 ms** |

Each cargo view is a copy of the keeper's overhead model: 380 transforms, ~370 item meshes. The game's `Activate`
(decompiled) reads the name of every one of them and switches each off to show one: what it does for the keeper's
own hands too. What's left of an item's cost is that call, once per item that gets a fresh view. F11 Run all
after the change: 2046 passed, 0 failed, 0 warnings.

### The door with babies, traced (2026-11-09, after the pass)

A door now starts the `[pick]` trace ("the keeper went through a door", dev only: the next 120 frames, each over
12 ms or with a part of 2 ms or more named by part), and the babies' spot search has its own part (`Slot`, around
`Foals.FreeSlot` in a catch-up and in `PlaceAgain`). The user's run: 16 doors in and out, 8 with one baby and 8 with
five.

- **The babies' spot search is not it:** `Slot` never reached 0.5 ms in a frame; `Doors` (the whole door step, the
  five babies placed) 1.6 ms once, under 0.5 the other 15 times.
- **What the frames after a door hold is the mounts' views made again**, one a frame (the spawn queue's cap):
  `Spawn` 2.4-3.7 ms in each (`SpawnView`, the game's own spawn or a parked view switched on, 0.9-1.4; `Prepare`,
  the view's parts, 0.7-3.0), for a few frames. The keeper lands 100 units from the yard, so the yard's mounts fall
  asleep going in and wake coming out, and the babies' views are made where they land.
- **The worst:** a 13 ms frame with the mod at 3.9 ms (the first door of the session: `Spawn` 3.2 and `Doors` 1.6),
  otherwise frames of 7-10 ms with the mod at 2-3.7 ms; one 21 ms frame with the mod under 0.5 ms (the game's). The
  frames are inside the door's fade. Performance Hog's earlier 5.5 and 11.3 ms frames are the same view work.
- **Not changed.** What would help is cheaper view making (the same work as riding through a herd); taken together
  with that item.

### The views investigation (2026-11-09, after the pass)

What a mount's view costs as it comes and goes, every path, measured before any change: the views probe
(`Dev/ViewCost.cs`, dev only: each path's steps, time and exact allocation, a `[view-cost]` summary every 10 s with
the `[perf]` logging) and the views survey (integration scenario, Garbage group, `ViewsSurvey.cs`: a mixed crowd of 43
made in view, the keeper 60 away and back twice, 24 away (idle) and back). The user's run, plus doors and riding by
hand:

| Path | Time a run | Garbage a run | Its biggest step |
|---|---|---|---|
| a view taken from the pool | 0.4-0.6 ms | 6-11 KB | switched on, the game's `OnEnable`: 0.35-0.47 ms, 6-11 KB |
| a new view (`Wgo.Spawn`) | 0.01 ms | under 1 KB | (its parts arrive frames later: hidden till prepared, 0.02 ms) |
| its parts prepared (`PrepareParts`) | 0.77-0.88 ms | 9-60 KB | **the animator restarted without a head (`RestartWithoutHead`: its object switched off and on): 0.50-0.63 ms, 8 KB, every view**; colours attached 0.09-0.19 ms (1.6-40 KB, the shared colour textures' first build) |
| a cart's parts prepared | 0.97-1.05 ms | 9-75 KB | the cart: cargo, chest, parts, lantern 0.32 ms |
| a view released (parked or destroyed) | 0.25-0.36 ms | 4-33 KB | parked: switched off, the game's `OnDisable`, 0.13-0.15 ms; the sprites reset and the prefab check 0.09 ms (up to 28 KB the first round, 2-5 KB after) |
| an idle view switched on again | 0.41-0.57 ms | 8-12 KB | the game's `OnEnable` |
| an idle view switched off | 0.09-0.16 ms | 0-2 KB | the game's `OnDisable` |
| an idle view frozen or unfrozen | 0.00 ms | 0 | |

- **The pool works:** 26-28 of 31 wakes taken from it in the survey (the misses: carts, never pooled). In the
  user's own play the only misses were carts; the babies' own sizes don't split it (the suspicion was wrong).
- **The walks of the view by name** (colliders, navmesh cuts, cart parts, body sprite) are 0.01-0.03 ms each: not it.
- **A view from the pool restarts its animator twice:** switched on to come out of the pool (0.4 ms), then off and on
  again in `PrepareParts` to start without the head sprite (0.5 ms, 8 KB). The idle switch-on empties the head layer
  before switching on and needs no restart.
- **Idle views switched off** (`Lod.IdleSwitchesOff`, on): every one coming back pays 0.4-0.6 ms and 8-12 KB, against
  0 for a frozen one. Whether switching off saves frame time is unmeasured uncapped (the levels self-test runs at the
  frame cap: frozen 8.34, switched off 8.38 ms); the survey's phase 8 now compares them uncapped with 60 more donkeys.
- **Measured uncapped (phase 8, 74 idle views, each mode twice, the order reversed):** switched off 3.41 and 3.62 ms
  a frame, frozen 3.43 and 3.48 ms; the mod 0.31 ms in all four. Switching off saves no frame time here. What it
  costs: 0.14 ms and 4 KB per view going idle, 0.53-0.57 ms and 15-17 KB per view coming back; frozen: 0.02 and
  0.03-0.04 ms, under 1 KB.

**Changed (agreed with the user):**
- **Idle views frozen, not switched off** (`Lod.IdleSwitchesOff` false by default; the switch stays, on the panel's
  Debug tab and in the self-tests).
- **A view from the pool starts without the head** (`TakeParkedView` empties the head layer before switching it on,
  as the idle switch-on does) and `PrepareParts` skips `RestartWithoutHead` for it (`startedHeadless`); a view from
  `Wgo.Spawn` restarts as before. `HeadWatch` logs `[heads]` if a still head ever shows over a walk.
- **Not changed:** the walks of the view by name (0.01-0.03 ms each), the game's spawn (0.01 ms), a cart's parts
  (about 1 ms, carts don't wake often), the game's own switch on and off into and out of the pool (0.4 and 0.14 ms).

**After (the user's F11 Run all, 2026-11-09):** 2051 passed, 1 failed (below), 0 warnings; no `[heads]` line.

| Path | Before | After |
|---|---|---|
| a view from the pool: its parts prepared | 0.77-0.83 ms, 9-14 KB (the restart 0.50-0.63 ms, 8 KB) | **0.24-0.30 ms**, 0-2 KB once warm (the restart 0.00) |
| a wake from the pool, made and prepared | about 1.4 ms | **about 0.9 ms** |
| an idle view coming back | 0.41-0.57 ms, 8-12 KB | **0.00-0.04 ms, 0 KB** |
| an idle view going idle | 0.09-0.16 ms | 0.00-0.02 ms |

A view from `Wgo.Spawn` (new, or a cart: carts aren't pooled) still restarts its animator (0.46-0.60 ms), as it must.
Phase 8 again: frozen 0.327 ms the mod, switched off 0.308-0.322.

The one failure was the crowd's leak check: "colour textures back (50 -> 49)". `Live.Textures` counts the skin
sheets too, and Larry's sheet, loaded by the garbage survey's picking just before, was let go during the crowd
(5 sheets loaded, 6 unloaded in its span). Not a leak (a leak grows) and not the views change. The check now compares
the colour textures alone (`Live.Textures - Looks.SheetCount`) and the sheets as "no more than at the start".
Run again (the Garbage group): 7 passed, 0 failed, 0 warnings; no `[heads]` line, and the user saw no still head over
a walk by hand. That run's world ended with 11 views against 17 (the levels' border), so the counters were reported
rather than compared: colour textures 43 -> 43, one skin sheet more (Larry's: loaded twice and let go once in the
crowd's span, let go again in the next scenario: held a little longer, not kept).

### The journal: its picture, the stage after closing, the opening (2026-11-09, after the pass)

Measured by the journal survey (integration scenario, Garbage group, `JournalSurvey.cs`; uncapped, the user's zoom):

- **The picture, fixed:** `PreviewStage.NeedsRender` takes a photo only when the picture can have changed (a new walk
  frame: the copy's body sprite; a new look or framing; the tuning re-merged; at most 1/24 s since the last, for the
  cart's sway and the swinging lamp), and the pose reads the animator's parameters once per animator (by hash after).
  Every frame (the old way, `RenderEveryFrame` for comparing): 175-177 photos a second, the mod 0.62-0.65 ms a frame
  (`Preview` 0.50-0.52); the new way: 25 a second, the mod 0.22 ms (`Preview` 0.10); the frame 5.64-5.70 -> 5.46-5.47
  ms. The picture's garbage (5 KB a frame before, `animator.parameters`) under 0.05 KB. Checked: it still renders
  while open (25 a second), and fewer photos than the old way.
- **The stage left on after closing (review row 5): not worth a change.** On against off, each twice: 5.39 / 5.61
  against 5.42 / 5.49 ms a frame, the mod 0.129 / 0.123 against 0.119 / 0.130 ms.
- **Opening it: 7.8-8.9 ms and 430-520 KB a time** (`[steps]` trace, three opens): the right side, the chosen mount's
  rows (`BuildRight`), 2.6-3.2 ms and 250-276 KB; the list's cards 1.6-1.9 ms; the stats and abilities cards 0.6-1.0;
  the buttons 0.35-0.55; the game's window underneath set aside 0.65-0.95. `BuildRight` runs on every pick in the list
  too (the pick traces' `PickRows` 3.0-3.5 ms): split further next (it walks every row of the page, every mount's and
  skin's, for the journal's few).

### Measurements at the game's own zoom (2026-11-09)

Every measurement above was taken at the user's Grave Zoom setting (another author's mod on the dev machine; 250% that
day, and it varies). Grave Zoom shrinks the camera's view (`CameraSystem.CalculateOrthographicSize` x 100 / zoom): at
250% the screen shows 40% of the width and height, a sixth of the area. Our levels make a mount fully active on screen
(or within 15), the babies catch up out of sight, and the game draws what is in view, so zoomed in the costs read
lower than at 100%, which most players have.

From now on the measurement scenarios (the crowd, the views survey, the journal survey, the garbage survey) and the
perf runner's suites run at 100%: `StandardZoom` (tools/GK2DonkeyAPI.IntegrationTests and a copy in GK2PerfRunner)
finds Grave Zoom by its GUID, reads its zoom at that moment, sets 100% without writing its cfg, and puts the read value
back after (with the write, so the file ends as it was); without Grave Zoom nothing is touched. Each says the zoom in
its first line; the crowd's TSV rows carry it in the phase; the suite file has a `# the zoom` line. The views survey's
idle phases find the keeper's spot from the camera's scale now (`IdleSpot`: the most of the crowd off screen with the
levels' margin, inside the sleep radius), not a fixed 24 units that was off screen at 250% only. The functional
scenarios stay at the player's zoom (several place things at distances meant to be off screen there).

So numbers taken from here on read higher than the tables above (more on screen, more active), not as a regression.

**The first run at 100% (Garbage group, 15 passed, 0 failed; the zoom put back after each scenario):**

| The crowd at 100% (vs 250% the same day) | Frame mean | p99 / worst | The mod | Mod KB a frame |
|---|---|---|---|---|
| none of the test's | 5.17 ms | 6.39 / 8.6 | 0.21 ms | 0.29 |
| 12 in view | 6.52 | 7.98 / 9.3 | 0.45 | 0.17 |
| 36 in view | 11.54 (5.92-6.46) | 14.16 / 31.5 | 1.01 (0.61-0.67) | 1.65 |
| + 120 scattered | 12.82 (6.82-7.30) | 15.03 / 17.8 | 2.03 (1.21-1.22) | 6.92 |
| riding through | 11.30 | 20.34 / 34.7 | 1.68 | **53.6** (8.9-20) |

- **Riding through the herd at 100%: 53.6 KB a frame, 32 of it `LooksBuild`.** In the 12 s the Mecha dragon's skin
  was let go (its one wearer asleep as the keeper rode past) and loaded again when it woke: 237 pictures, a 15.6 MB
  sheet, 64 ms of work over 31 frames. `LooksRig.Sleep` lets a sheet go as soon as no awake mount wears it; a mount
  waking again a moment later builds it from scratch. Open: a grace period before a sleeping wearer's sheet is let go.
- **The journal's picture at 100%:** every frame 6.72-6.79 ms a frame, the new way 5.53-5.66 (147-149 photos a second
  against 25).
- **The journal's opening and each pick, split:** the 469 rows of the page sorted into groups cost 0.11-0.18 ms (the
  suspicion was wrong); **building the journal's 8 rows' controls costs 2.4-3.8 ms and 300-360 KB, on every open and
  every pick** (about 0.35 ms and 43 KB a row: the rows are destroyed and made again, their controls bound to the
  rows of the mount picked). Applying the choice 0.5 ms; the rest under 0.3. Not changed: keeping the controls and
  pointing them at the new rows means reworking RowControls (each control's handlers are bound to its row), a larger
  change than a menu click's one frame is worth before the release.

### Skin sheets let go and built again (investigated 2026-11-09)

**When a sheet is held:** by every awake mount wearing it (`Looks.Rig`), by the companion's chosen skin (`TickWarm`,
always), and by the journal while it is open (`HoldForPanel`, up to 64 MB, let go when it closes). **When it is let
go:** at once, when the last hold goes (`Looks.Release`): its last wearer falls asleep (`Rig.Sleep`), is removed, or
the journal closes. Wearing it again builds it from its files again.

**What a build costs** (every build of the run of 2026-11-09, at 100%; `[Looks] ... loaded` lines):

| Skin | Pictures, sheet | Work, frames | Garbage a build | The most in one frame |
|---|---|---|---|---|
| Mecha (dragon/armored) | 237, 2065x1936, 15.6 MB | 60 ms over 28 | **33 MB** | 2.3-3.9 ms |
| dragon/default | 237, 15.6 MB | 40 ms over 19 | (one build) | |
| Larry (game source, read back through the graphics card) | 44, 1128x1128, 5 MB | 85-240 ms over 30-44 | 6.2 MB | 4.7 ms |
| Hellfire (over the donkey, erasing) | 44, 1.8 MB | 225-260 ms over 45 | 5.7 MB | 5.4-7.9 ms |
| Armored | 44, 1.8 MB | 20 ms over 7 | 2.3 MB | 4.1 |
| rat, snail, worm, Royal Parade | 44, 1.8 MB | 4-5 ms over 3-4 | 1.7-2.2 MB | 2.0-2.6 |

The garbage is per picture: the PNG's bytes (read through a growing `MemoryStream`, then `ToArray`), a `Color32[]`
from `GetPixels32` (64 KB for a 128x128 canvas), and a copy for an over skin or one built on its default
(`(Color32[])f.P.Clone()`, `WrapAlone`); each is put in the sheet and dropped. 237 pictures x about 140 KB is the
dragon's 33 MB: one build brings the game's next collection (about 14 ms) much nearer.

**Where it is built again in play:**
- **Browsing the journal again** (the most common): the looks it showed are let go when it closes; opening it and
  picking the dragon again is another 33 MB build, every time.
- **Riding back and forth past a mount**, **a door** (the yard falls asleep going in), **walking away and back**:
  for every skin worn only by mounts that fall asleep (mounts left in the world, other mods' mounts, the test crowds).
  The ridden mount's own skin is the companion's, always held: never this.
- Measured: the crowd's ride at 100%, the Mecha let go and built again within 12 s (53.6 KB a frame for the phase).

**Two ways to cut it, not built yet:**
- **A. Keep let-go sheets a while** (a cache of recently used sheets, capped at about 64 MB of sheets, the least
  recently used let go first): wearing it again takes it at once, no build. Covers the journal browsed again, doors,
  riding past, walking away and back. One place in the code (`Release` keeps instead of destroying; `Acquire` takes
  from it). Costs up to the cap in texture memory (the sheets are non-readable: graphics memory). Two self-tests
  check today's "freed at once" (`SkinSleep`, `SkinOnOff`) and would check "kept, then freed when the cache lets
  go" instead.
- **B. Make a build make less garbage:** each picture decoded into reused buffers (one texture, a reused pixel array,
  the PNG read into a sized buffer, the over and inherit copies into reused arrays). Helps every first build too
  (the first pick of each look, the first spawn). More code in the build pipeline (the pixel hash, the colours, the
  regions read the same arrays).

**The three measurements (the user's run, 2026-11-09):**

1. **A build's garbage by step** (`BuildCost.ARead`... in every build line): nearly all of it is the decode, a new
   `Color32[]` per picture (`GetPixels32`): the dragon 15.7 of 16.0-16.5 MB, the Mecha 31.5 of 32.5-32.8, the critters
   and Royal Parade 1.7 of 1.7-1.9. Reading the files 0.2-1.2 MB. Hellfire 1.7 decoded + 4.2 the erase's copies
   (`WrapErase`); Larry 6.4, all the game's frames read back (`LooksGame`).
2. **A ready-made sheet against a PNG per picture** (the builds survey, `BuildSurvey.cs`: the dragon's 120 main
   pictures written both ways as test skins, each built twice): per picture 64-65 ms of work over 31 frames, the most
   in one frame 2.4-2.5 ms, 8.6-9.0 MB of garbage; one sheet 20 ms over 7 frames, **the most in one frame 10.5-10.6
   ms, 16.2-16.3 MB** (the whole sheet decoded in one frame into one array, then a new array per cell). Not a way.
3. **How often in play** (`built N time(s) this session` in every build line): about 5 minutes of the user's play
   (the journal browsed, closed, browsed again; the yard; a door): **14 builds of skins built before, about 106 MB of
   garbage**, all but one from browsing the journal again (the Mecha three times at 33 MB); the yard and the door
   rebuilt Larry once. The Garbage group's own run: 30 rebuilds, 204 MB (the tests drive it hard).

**What the numbers point to:**
- **B, narrowed:** decode into one reused texture and one reused pixel array per canvas size (`DecodePng`), so a
  build's garbage falls by 90-97% for the dragons, critters and Royal Parade (Hellfire about 30%; Larry needs its own
  read-back change). Every build gains, the first ones too, with no memory kept.
- **A, narrowed to the journal:** keep the journal's held looks after it closes (its existing 64 MB cap, let go after
  some minutes unused) instead of letting them go at once: browsing again builds nothing. The general cache for
  sleeping wearers isn't what play shows (one rebuild in the yard).

**Changed (agreed with the user, 2026-11-09):**
- **B:** `DecodePngInto` decodes into one kept texture (`decodeScratch`, shrunk back after a picture over 512x512)
  and copies its memory (`GetPixelData`, read in place) into the caller's reused array; the per-file producer keeps
  one array per producer (a variant's own and its default's are two producers, so composing keeps both). `DecodePng`
  still hands back a new array (the icons, the sheet pack, the hostile-files test keep theirs). A variant that is not
  drawn over its default no longer decodes the default's picture for a frame it has its own (the Mecha decoded both,
  237 + 237).
- **A:** `Looks.KeepPanelHoldsAWhile` on the journal's close: its looks kept `PanelKeepSeconds` (300 s; showing one
  again keeps them while open), within the 64 MB cap, let go by `TickWarm` when the time is up. The self-test runner
  lets them go between scenarios (`CleanUp`), so each scenario starts from "a sheet nobody wears is freed".
- **Tests:** the builds survey checks a 120-picture build from files leaves under 3 MB (8.6-9.0 before); the journal
  survey closes, opens and browses again and checks no look is built again.

**First try of B didn't take:** the next F11 (2051 passed, 2 failed: the two "under 3 MB" checks, 8.4 and 8.6 MB) showed
the decode still at 8.0 MB. The fast path read RGBA32 only, and **PNGs decode to ARGB32 here** (now said once in the
log: "Looks: pictures decode to ARGB32"), so every picture took the old `GetPixels32`. `DecodePngInto` now copies an
ARGB32 or BGRA32 texture's memory into one kept `int[]` and puts each pixel's bytes in RGBA order itself; a check
compares that with Unity's own conversion pixel for pixel.

**After (the user's F11 Run all, 2026-11-09): 2054 passed, 0 failed, 0 warnings; no `[heads]` line.**

| Skin | Garbage a build before | After |
|---|---|---|
| Mecha (dragon/armored) | 32.5-32.8 MB | **1.2-1.3 MB** (most of it the files read) |
| Armored (donkey) | 2.2-2.3 MB | 0.5-0.7 MB |
| Royal Parade, rat | 1.7-1.8 MB | 0.1-0.2 MB |
| Hellfire | 5.6-6.0 MB | 4.0-4.3 MB (left: the erase's copies, `WrapErase`) |
| Larry | 6.1-6.5 MB | 6.2 MB (left: the game's frames read back, `LooksGame`) |
| the builds survey, 120 dragon pictures from files | 8.4-9.0 MB | 0.7-1.4 MB |

- Decoded into the reused buffer = Unity's own conversion: 32,768 pixels compared, 0 differ.
- The journal closed, opened and browsed again: no look built again.
- The run's every rebuild together: 132 builds of skins built before, 132.9 MB (about 1 MB each; the Garbage group
  alone before the fix: 30 rebuilds, 204 MB).
- **Left:** Hellfire's erase copies (about 4 MB a build) and Larry's read-backs (about 6 MB), their own code paths.

**Left for after this pass, by what a player feels:** the cargo refresh per item (2-6 ms; row 20's other half), the
door frame with babies (row 10), opening the journal (one 14 ms frame), the garbage of views waking and sleeping
riding through a herd, the picture drawn every frame while the journal is open (rows 4-5).

### What can't be measured here

The GPU: the game's build keeps no frame timing (`FrameTimingManager` off), and the colour-swap table above showed
this PC's card hiding the shader's work. A weaker card isn't measured; CPU milliseconds and garbage are what we can
show, and those scale onto a slower CPU roughly as they are.
