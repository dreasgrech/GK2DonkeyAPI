# Profiling with Performance Hog

Performance Hog (Orion, Nexus; 1.2.0 installed 2026-10-16 on the dev machine only, never a dependency) is a per-mod
profiler for GK2. It sees our mods from outside, the way a player's machine runs them, next to the game and the other
mods. Our own `[perf]` line (docs/performance.md) sees inside. The two answer different questions, so a profiling
session uses both. Found with it so far: the 2-3 ms Mods-window lookups (docs/performance.md, 2026-10-16).

## What it measures, and what it can't

- **CPU per mod**, while a recording runs: it times every entry point of each mod with a Harmony prefix and finalizer
  (Unity messages, our Harmony patch methods, coroutines' `MoveNext`, delegate targets). The Donkey API's whole frame
  is one entry point, `Plugin.LateUpdate`, so the report says how much and when, not which part: that is the
  `[perf]` line's job (its parts: Ride, Stand, Shuffle, Pose, Lantern, ...) or a probe's.
- **Worst frame per mod** and the frames where mods took 1 ms or more, each with the method that took it. A key
  handler's lambda (`SaddleUp.Plugin+<>c__DisplayClass3_0.<Awake>b__0` is H, `b__1` is G) is billed for the API work
  it starts.
- **Allocations per mod**: from the heap's growth around each call (an estimate), and the garbage collections that
  started inside a mod's code. Long frames with a collection in them are marked "(garbage collection)".
- **Log lines, disk, exceptions, web requests** per mod, counted since launch (or the recording: the Counts button).
  Exceptions include caught ones. The 8 `NotSupportedException`s in `Corpses.Patch`/`Apply` are Harmony's own, caught
  inside it while it patches (once, when the feature switches on).
- **Memory** (snapshot): the data each mod holds and the Unity assets it made at runtime. Two snapshots a while apart
  give the change: a mod that keeps growing leaks.
- **Frame pacing** (1.2): the 30 s before the recording key against the recording (FPS, 1% low, median, spread,
  frame-to-frame change, hitches), from 5 s after the last load. Export also writes `<report>-frames.csv`: every frame's
  length, phase and the sensors (CPU and GPU use, GPU clock, temperature, power).
- **Its own cost** is measured and taken off (about 0.2 µs a timed call); the numbers compare mods, they are not a
  bare game's.

## Before a session (game closed)

1. Player numbers: `BepInEx\config\dreas.gk2.donkeyapi.cfg`, `[Debug]` `TuningWindow = false`. With it on, the
   `[perf]` line and the per-part allocation counting run and add their own cost. The drill-down pass (below) turns
   it on again.
2. Performance Hog's settings in the Mods menu: all counters on.
3. Keys: F9 its window, Ctrl+F9 record start/stop (our showcase director is on Shift+F9).

## Pass 1, scripted: the suite (tools/GK2PerfRunner)

The dev-only runner (built 2026-10-16, `Deploy=false`: `dotnet build -c Release -p:Deploy=true` with the game closed)
does pass 1 by itself. Shift+F8 opens its window.

1. **The loop, once:** Record loop, walk a loop of 15-25 s by hand (wide enough for the cart, ending where it
   started), Stop recording. It is kept in `BepInEx\config\dreas.gk2.perfrunner.loop.txt`; Drive loop tries it.
   The runner steers the keeper's own movement (`FreePlayerState.FixedUpdate` gets a direction instead of the keys),
   so speed, animation, slopes and collisions are the game's, on foot, riding and driving (tested 2026-10-16: laps of
   5.4 s on foot, 2.8 s riding, 3.3 s with the cart).
2. **The suite:** stand on the loop, on foot, the cart empty; pick the time of day to hold (the current one, noon or
   night: night lights the lantern); Start the suite. About 12 minutes, hands off. It runs walk, ride, cart (six test
   items: wood, wood, stone, stone, box_flour, box_vine), idle (by the parked donkey and cart), hitch (five rounds of G, test
   load on and off, H, H, spaced apart) and memory (snapshots at the start and the end), each with 36 s in the same
   state before Performance Hog's recording key. Every report's name goes into `BepInEx\PerformanceHog\suite-<time>.txt`.
3. `python tools/perfhog-report.py --suite` reads the newest suite file.
4. **Several in a row** (Suites in a row: 1, 2, 3 or 5): one after another without a hand on the keyboard, the time
   held throughout, the start checks again before each. Each suite has its own suite file, census lines and memory
   report, and the log gets a census over the whole series. The first suite warms the caches up (about +100 KB from a
   fresh start); a leak shows as growth in the later ones. `python tools/perfhog-report.py --series N` puts the newest
   N suites side by side: each run's API time, worst frame and allocations suite by suite, and the memory change.

What it guards: it doesn't start while the cart holds cargo, a time of day is held already (the director), or
Performance Hog is off or busy. It stops on Esc (any pause), a movement key, the loop blocked for 2 s, anything that
doesn't happen in time, or an error; the cleanup takes the test load off (calling the cart first if need be), stops
a recording, and puts the real time back. It never sleeps, saves or sends the donkey away (that would set the cart's
load down in the world). H and G are Saddle Up's own key actions invoked as they are, so Performance Hog bills their
work to Saddle Up and the API as for a real key press; the runner's own row is the loop steering and the suite's
steps. `TestLoads` writes `BepInEx\GK2-test-loads.txt` on every load: that shows as Donkey API disk activity.

## Pass 1 by hand: player numbers

Each run: load the save, play 40 s or more (the "before" window needs 30 s from 5 s after the load), press Ctrl+F9
at the change of activity, play 60-90 s, Ctrl+F9 again, F9, **Export report**. No saving, no other windows, no
Alt-Tab during a recording (the report lists focus changes).

| Run | Before (30 s) | Recording (60-90 s) | Answers |
|---|---|---|---|
| walk | a fixed loop on foot, away from the donkey | the same loop on foot | the game's own variation (the noise floor) |
| ride | the loop on foot | the loop riding (H), foals following | riding |
| cart | the loop on foot | the loop driving (G), 6 items on the cart, lantern lit | cart, cargo, lantern |
| idle | standing by the parked donkey, cart and foals | standing there | shuffle, foals, standing views |
| mix | normal play | G, H, load and take off, a door and back | one-off hitches (Show spikes) |

The same loop every time (the road in front of the graveyard), so the scene costs the same.

**Memory**: Memory snapshot after the walk run; about 10 minutes of normal play (hitch and unhitch, doors, loading
and unloading, foals indoors); Memory snapshot again; Export. The Memory column shows the change.

Optional, the worst case: many donkeys (the herd test mod, docs/performance.md) for the idle run.

## Reading the reports

    python tools/perfhog-report.py --last 6 --label walk,ride,cart,idle,mix,memory

For each report: the recording, before against during, our rows and slowest methods, the frames where ours took
1 ms or more, the long frames and how many held a garbage collection, the memory change; from the frames file:
percentiles per phase, hitches, and **hitches on a timer** (the same gap between hitches again and again: a periodic
job; the Mods-window lookups showed as gaps of 0.25 s). Then one table of all the runs.

What to look for:

- **Steady cost**: our ms a frame against the frame's work (8.3 ms at 120 FPS); the walk run is the floor, every
  other run against it.
- **Hitches**: a worst frame of 2 ms or more, or a hitch on a timer. A periodic one is a job of ours on a timer
  (every 0.25 s, 1 s, 10 s ...): find it, then make it cheaper or stop it running when it has nothing to do.
- **Garbage**: allocations a frame and the collections started in our code. On 2026-10-16 the game collected about
  every half minute (collections 152 to 155 in about 105 s of play, heap about 520 MB), a pause of about 14 ms (a
  22 ms frame against a median of 8.4); each KB a frame anyone allocates brings the next one sooner. Our steady state
  was 1.4 KB a frame; per part, see the drill-down.
- **Log lines** that keep growing with nothing happening (docs/performance.md, "Still open").
- **Memory** that grows between two snapshots while the donkeys are the same.

## Pass 2: the drill-down

For whatever pass 1 flagged: `TuningWindow = true` (game closed), the same run again. The `[perf]` line comes every
10 s into `LogOutput.log` with each part's time, worst and allocations (`Tick 0.24 ms (worst 2.9, ..., +3789 B);
Ride ...; Stand ...`), plus a `[perf] slow frame` line naming the parts of each slow frame. A worst that no part
explains is outside the parts: a temporary probe that times each step of `MountRegistry.Tick` (the 2026-10-16 one is
in docs/performance.md) finds it.

Then fix, deploy, and run the same pass-1 run again: the report against the earlier one is the proof.
