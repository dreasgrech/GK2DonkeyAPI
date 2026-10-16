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

**Fixed** (`ModsWindow.cs`, both use it): the window is looked for only at the main menu or while the game is paused
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
- **What the API logs for players:** the routine `[foal]` and `[shuffle]` lines (follows, works out a way round, route
  found, found no step) are written without the dev tools too, up to 4 a second, for as long as foals are about; their
  text is built before the 4-a-second budget is checked. Proposed 2026-10-16: those dev-only, the ones a bug report
  needs kept (can't follow, stood inside, caught up out of sight); not decided.
- **One-off hitches:** calling the cart (G) took 25.8 ms in one frame (Performance Hog bills it to Saddle Up's key
  handler; the work is the API's view build). Not looked into yet.
- **The `[shuffle] frame +N` turn diagnostics** read the body before the frame's own position write. They can't tell
  whether a turn jumps on screen.
