# The self-tests' move to GK2 Mod Test Bench: the audit (2026-11-17)

The user, after the move was proven by one run: "make sure we have migrated ALL tests from the f11 and that we have
migrated all of them correctly, without skipping or assuming about a single one. check them all." This is that
check. Nothing in it is taken from what the move was meant to do: every scenario is read from the code as it was and
as it is, and from the logs of the runs.

**The answer:** all 187 scenarios are in the bench, each one the same scenario with the same code, run in the same
order and making the same checks; nothing was found that differs. That much is proven by the code and by a run in
the game. What a run where nothing fails can't show (a scenario that throws, an abort, a run of one group, a
refusal, the hidden panels) is listed in section 7: it is covered by reading the code and by the bench's offline
test, not yet by the game. Four short looks in the game close it.

**There is no second copy to remove.** The F11 runner and its Tests tab already left the API in the commit of the
move (`94a2092`). The scenarios were never copied: the bench runs the same methods the F11 runner ran. What is left
under F11's Tests tab is four lines saying where the tests are (section 8). The old runner can be had back whole
from `a6d45c4` should it ever be wanted.

## What was compared

| | |
|---|---|
| The code before | commit `a6d45c4`, the last with the API's own runner |
| The code now | `94a2092` and the working tree |
| The old runner's last run | 2026-11-17 15:59, 2137 passed: each scenario's number of checks, kept by `tools/log-scenarios.py --keep` (the log itself is gone: the game writes over it at every start) |
| The bench's run | 2026-11-17 22:13, the suite "GK2 Donkey API" alone: the run's own file, every line |
| An old runner's whole log | Unity's `Player-prev.log`, a full run of 2026-11-11 (2108 passed, an older build): every line |

The tool is `tools/selftest-migration-audit.py` (its header says what each step does); the three inputs are kept
outside the repository (`GK2-selftest-migration-2026-11-17`).

## 1. The lists: 187 then, 187 now, entry by entry the same

The scenarios are listed in two files, `src/GK2DonkeyAPI/Dev/SelfTest.cs` (140) and
`tools/GK2DonkeyAPI.IntegrationTests/Tests.cs` (47). Every `new Scenario { ... }` entry was parsed whole at both
commits: its group, its name, its body and its hides-the-panel flag. Both files: every entry the same, in the same
place. 187 names, 187 different.

The first pass of the tool read 185 entries of 187 and said so (two bodies are written as a call, `() =>
Wake(switchOff: true)`, not as a method's name); it now reads both shapes and fails on an entry of any other.

## 2. The order: the bench ran them in exactly the old runner's order

The old runner's order was worked out from the old code (its `AddScenarios`, then its `Run`, which put the checks of
the whole run last and the world's check the very last) and compared with the order the scenarios' lines stand in
in the bench's run file: the same, all 187. Each scenario's lines stand together (none ran inside another).

Against the old runner's own log of 2026-11-11: the 176 scenarios both runs have ran in the same order there too.

This is where the move found something: the list has one group, "Skins and effects", in two parts with Translations
and Measurements between them. The bench ran a group's tests together; it was changed to run tests in the order they
were added, before the proof run.

## 3. The bodies: the same code

92 source files of tests at `a6d45c4`; 85 are the same byte for byte now. The other seven, every differing line read:

| File | What differs |
|---|---|
| `Dev/SelfTest.cs` | the runner's own three blocks (section 5), and one comment line above the list. No scenario and no helper |
| `Dev/SelfTest.Sounds.cs` | one line, in "a pack's own clip and the base's start offset": `Line("[test] INFO " + current + ": ...")` became `Info("...")`. It prints the same line (`[test] INFO <the scenario>: the next "not longer than its start offset" warning is this test's own`), seen in both logs |
| `Dev/TranslationTests.cs` | one comment line |
| `Dev/TuningWindow.cs` | the Tests tab's own drawing (120 lines) became four lines that say where the tests are |
| `Dev/GameQuiet.cs`, `Dev/TestZoom.cs` | deleted: the bench holds the game's story and the camera's zoom (its `StoryHold.cs` and `ZoomHold.cs`, the same patches) |
| `IntegrationTests/Plugin.cs` | the hook-up: `Bench.Register()` after `AddScenarios`, the dependency on the bench |

Added: `IntegrationTests/Bench.cs`, the hook-up itself.

**Outside the two test folders** (the whole repository from `a6d45c4` to now, `git diff --name-status`: 17 files,
one commit, `94a2092`), every one of the others:

| File | What differs |
|---|---|
| `src/GK2DonkeyAPI/MountRegistry.cs` | one line gone: `SelfTest.PollStart();` at the top of `Mounts.Tick`. It started a requested run as a Unity coroutine on the API's plugin. The bench starts a requested run the same way, as a Unity coroutine on its own plugin from its `Update`, and hands Unity the same things the scenarios yield (`Guard`'s `yield return cur`, the old `Drive`'s line). So a scenario's steps are resumed by Unity at the same point of a frame as before; only the first step of a run (to its first wait) runs inside another plugin's `Update` |
| `src/GK2DonkeyAPI/Api/MountCompanion.cs` | one comment line (`SelfTest.Drive` is `SelfTest.HoldRun` in it) |
| `src/GK2DonkeyAPI/game-code.txt` | the list of dev classes: the two deleted files' names out |
| `Directory.Build.props`, the plugin's `.csproj` | where the bench's DLL is, and the reference to it |
| `tools/log-scenarios.py` | reads the bench's summary line and a run's own file too |
| `CHANGELOG.md`, `docs/self-tests.md`, `docs/release-checklist.md` | words |

**What the unchanged files lean on.** The 85 files that are the same byte for byte use names that were defined in the
runner's blocks, so the same text could mean something else now. Each name the rest of the code uses, then and now:

| Name | Then | Now |
|---|---|---|
| `SelfTest.Running` (read by `Perf.Timing` and by `Sounds`' voice times: the same two lines then and now) | true from the first line of `Drive` to its last: the start's wait, every scenario, the finish | true from `HoldRun` to `LetRunGo`: the suite's hold, which the bench begins before the start step and ends after the finish step (`Runner.cs` 306 to 360). The same stretch |
| `MountCompanion.HeldForTests` | set and cleared beside `Running` in `Drive` | set and cleared beside `Running` in `HoldRun` and `LetRunGo` |
| `abort` (read by the waits and by scenarios' own loops) | a field: false at a run's start, true from Abort to the run's end, the finish included | the bench's `Test.Aborted`: false at a run's start, true from Abort to the run's end, the finish included (`Runner.AbortRequested`, cleared only in `Start`) |
| `Pass`, `Fail`, `Warn`, `Info`, `Skip`, `Check` | count and write the line | hand the text to the bench, which counts and writes the same line |
| `Owner`, `spawnSeq`, `Scenarios`, `Groups`, the group names | | the same lines |
| `pass`, `fail`, `warn`, `skip`, `Status`, `Lines`, `Current`, `RunCount`, `RunDone`, `RunStarted`, `RequestStart...`, `Abort()`, a scenario's `State`, `Pass`, `Fail`, `Warn`, `Skipped`, `Elapsed` | read only by the Tests tab's drawing and the runner itself | gone; nothing reads them (the code builds with 0 warnings) |

## 4. The checks: 2137 and 2137, no scenario differs

Each scenario's PASS, FAIL and WARN lines in the bench's run against the old runner's kept count: **0 of 187
differ**. 179 make checks; 8 make none under either runner (they measure and write files), and each of those left
its own lines in the bench's run, so it is seen to have run:

| Scenario | Its lines in the bench's run |
|---|---|
| frame time by mode | 5 INFO |
| babies: Saddle Up's own babies, each its own temperament | 1 INFO (one of its babies is out in this save: not compared) |
| measure: every skin of every mount exported as built | 1 INFO |
| measure: the journal picture with the lamp lit, dark and off, every donkey skin | 17 INFO |
| measure: the colours window of every skin of every mount | 60 INFO |
| measure: the colour swap's cost | 37 INFO |
| measure: a skin over the donkey against the donkey under it | 14 INFO |
| measure: the game's Halloween head swap on the zombie pictures | 23 INFO |

The run: 0 failed, 0 warned, 0 skipped, 0 threw. The game's log of that session has no warning or error line before
the run and none after it; the 55 during it (54 warnings and one error, the chest switched off "for this session:
self-test") are the tests' own: made-up packs and clashing ids in temporary folders, the safe-ground and chest
checks, a drop point that isn't there. All 55 were listed by kind and read, and every one names the self-test, its
temporary folder or its made-up thing. The old runner's log of
2026-11-11 has 55 too, and so had its run at 15:59 (noted then in `docs/self-tests.md`). The table of all 187 is at the end.

## 5. The run's own steps: the old `Run` and `Drive`, line by line

The old runner's code was 191 code lines (`SelfTest.cs` 293 to 607 at `a6d45c4`). Each was looked up in the new
steps; 86 are there word for word, and every one of the others is one of these:

| The old line | Where it went |
|---|---|
| Which scenarios a run takes, their run state and tallies, the summary, `Status`, `Lines`, the "this run: N of M" line | the bench (its `Runner`); the same choices: all, a group, one; the checks of the whole run with any of them |
| `Drive`: the stack of iterators, a throw turned into a FAIL, the unwinding, a cleanup that throws | the bench's `Guard`, the same loop; covered by the bench's offline test |
| `GameQuiet.Begin/End`, `TestZoom.Begin/End` | the bench's two holds, begun before the suite and ended after it |
| The values a run puts back (`lodOn`, `slotFloorWas`, `freeplayWas`, `armourWas`, `origin`, ...: 18 local variables) | the same statements, the variables now fields, so `FinishRun` and `LetRunGo` can put them back |
| `Line("[test] start: ...")` | `Info(...)` with the same text: `[test] INFO GK2 Donkey API: start: RuinedTemple at ...` |
| `Status = "Not in a game ..."`, `Status = "Not started: the game holds the keeper ..."` | `refuse(...)` with the same reasons: the bench skips the suite's tests and says why |
| `current = ...` (3 lines) | gone: the bench names the test in every line |
| `Pass`, `Fail`, `Warn`, `Info`, `Skip`: count, then write `[test] PASS <scenario>: <what>` | the same five methods hand the text to the bench, which counts and writes the same line |

New in the steps, with no old line behind it: the flags that make putting back safe to do twice (`RestoreSettings`
runs in `FinishRun` at the old place and again in `LetRunGo`, which also runs when a run is ended from outside), and
`runScene`, the scene's id kept for the remembered spot. `Line` is kept as a pass-through to `Info`; no scenario
calls it (searched: the only `Line(` calls in the tests' code are `TextRoom`'s own method of that name).

**The order.** The 86 shared statements were also compared as a sequence (the tool's step 7). They stand in the
same order but for 13 statements in three places, each read:

- `Running = true; MountCompanion.HeldForTests = true;` and their two clearing lines stood in `Drive`, below `Run`
  in the file; they stand in `HoldRun` and `LetRunGo`, above `StartRun`. When they run is the same (the table above).
- The eight lines that put the settings back (the language, the levels' three, the slot floor, Freeplay, the
  standing sounds) stood in the finish of `Run`; they stand in `RestoreSettings`, in the same order among
  themselves, and `FinishRun` calls it where they stood: before a failed scenario's corpse bodies are taken away,
  then the spot remembered, the keeper back, his light, his armour.
- `Cargo.SlotFloor = slotFloorWas;` stood a second time where the run is refused because the game holds the keeper;
  `RestoreSettings()` stands there now (the levels' settings it also puts back were only read by then, not changed).
- `if (s.HidesPanel)` stood twice (hide, show again); the showing again asks `if (panelHidden)` now, and stands a
  second time in `LetRunGo` for a run ended from outside.

**What the bench does that the old runner didn't**, and can matter to a run: it ends the run (as an abort, with the
finish) when the game is left for the main menu during it; a hold or a start step that throws skips the suite's
tests with the reason; its own stall watchdog ends a run that stops moving. None of them comes up in a run that goes
through.

**Seen in the run** (the game's log of that session, the lines around the run):

- "the keeper moved to the remembered test spot (38.4, 3.6, 15.6) (Debug.SelfTestSpot); back to (-96.3, 0.0, -79.2)
  at the end" and "the light set to outdoor for the run (it was indoor ...)": the same two lines, word for word, as
  in the old runner's log of 2026-11-11.
- "start: RuinedTemple at (38.4, 3.6, 15.6) (container zone graveyard), 12 instance(s) in the world; levels on, idle
  frozen": the old runner's start line (18 instances then).
- "the game's own story is held for the run ..." before the first scenario, and after the last "the game's own story
  held during the run: 0 quest trigger(s), 0 zone script(s); no world zone newly visited".
- "the camera at the game's own zoom (100%) for the run: it was at 133% (a zoom mod's)" and "the camera's zoom given
  back to the player".

The lines of the run that belong to no scenario are those start lines and nothing else (4 in the bench's run, the
old runner's log: the same, its start line not an INFO line).

## 6. The old runner's own log, word for word

`Player-prev.log` holds every line of a full run of the old runner (2026-11-11, 2108 passed, the build of that day).
Of the 176 scenarios of today's list that made checks in it: 171 make the same number of checks as in the bench's
run; **124 say every check in the very same words, 41 more in the same words but for the numbers they measure**.
The other 11, each read:

| Scenario | Then, now | Why |
|---|---|---|
| the start's check of the game's code | 3, 3 | the module ids in the text are the game's: the game was updated on 2026-11-13 |
| upgrade: a released version's player files are all read | 39, 43 | a release since (0.17.1): more files to read |
| upgrade: Saddle Up's cfg as every released version wrote it | 12, 14 | the same release |
| foals: the stuck report leaves out a baby still by design | 4, 4 | the check says more since 2026-11-12 ("the pen on screen 0.0 s after the keeper came back") |
| cargo: a zombie with the game's Halloween head on the cart | 3, 3 | the game names the drawn object with "(Clone)" since its update |
| settings: the Mounts panel opens, fits, follows the list | 80, 80 | the windows' object names changed with the window library (2026-11-15), and the player's chosen mount is another |
| settings: the options window | 33, 33 | the same object names |
| settings: the options window among other mods | 11, 13 | two checks added 2026-11-15 (a key read exactly) |
| jump: the home yard | 2, 3 | one more built thing in the yard to jump at: the Halloween decorations are up |
| the probe: own clips as game sounds | 13, 13 | the clip's name in the text ("probe-tone") |
| babies: Saddle Up's own babies, each its own temperament | 1, 0 | one of its babies is out in the save: not compared (its INFO line says so) |

None is the runner's doing, and all 11 make the same number of checks in the old runner's run of 2026-11-17 as in
the bench's (section 4).

**The INFO lines** (what a scenario measured or found; they are no checks) were counted per scenario too, against
the same log. 183 of today's scenarios left a line in it (the 176, and 7 that only measure): 176 left the same
number in the bench's run, and no scenario skipped in either. The 7 that differ, each read:

| Scenario | Then, now | What differs |
|---|---|---|
| settings: the Mounts panel opens, fits, follows the list | 14, 15 | the text room of the colours window is measured for the mount the player has chosen: another one now; one line more about the lantern's size |
| measure: the journal picture with the lamp ... | 18, 17 | the pictures are twice the size (sharp at any screen size since 2026-11-13) and one "settled after" line didn't come up |
| jump: the home yard | 23, 37 | 64 things in the yard now, 60 then: a line each |
| jump: a real hillside | 8, 2 | the frame-by-frame lines are written only when a step goes past 2.5 px (`SelfTest.Yard.cs` 303); none did |
| upgrade: Saddle Up's cfg as every released version wrote it | 6, 7 | a line for 0.17.1, released since |
| babies: Saddle Up's own babies | 0, 1 | "Saddle Up has 1 baby out: its own babies not compared" |
| the views survey (measures only) | 53, 43 | a line for each kind of view step that ran: fewer kinds this time (and the zoom line) |

Four scenarios of today's list left no line in that log: they were added to the list after it (2026-11-12 to
2026-11-15). The old runner's run of 2026-11-17 covers them, with the same numbers of checks as the bench's: the
cart key while riding where the hitch would seat the keeper on blocked ground (8), cargo: the pictures from the
game's shared pool (7), settings: another mod's window open (5), settings: a row's mount data is its own (1).

The old runner's run of 2026-11-17 can't be compared line by line: only its numbers of checks were kept.

## 7. What a green run can't show

A run where nothing fails proves the scenarios and the steps around them. It does not exercise these, and they are
covered as said:

| What | How it is covered | Seen in the game |
|---|---|---|
| A scenario that throws: a FAIL, its cleanup still runs, the next scenario runs | the same loop as the old `Drive`; the bench's offline test (`tests/RunnerTest`: a body, a nested step and each hook throwing) | no |
| A run of one group or of one scenario: the same start and finish, the checks of the whole run with it, the spot not remembered | the bench's offline test (partial runs, `run.Full`); the hook-up passes `run.Full` to `FinishRun` | **yes**, 2026-11-17 23:57 (below) |
| Abort | the bench's offline test (the waits end, the cleanup and the finish run) | **yes**, 2026-11-17 23:57 (below) |
| The run refused while the game holds the keeper | the bench's offline test (a refusal from `BeforeRun`); the wait and the reason are the old lines | no |
| The six scenarios that hide the panels (frame time by mode and five surveys) | the flag is in the list (section 1) and handed to the bench by the hook-up; the F11 panel is hidden by the same lines as before. The run's log did not say it; since this audit the bench writes a line when its panel goes and comes back | **yes**, 2026-11-18 12:04, with one frame's difference found and fixed (below) |
| A run ended from outside (stalled, the game closing) | new with the bench: the settings and flags come back through `LetRunGo`; the bench's offline test | no |

To see the rest in the game, four short looks (F4, the suite "GK2 Donkey API" opened):

1. **Run** on one scenario's row (say "hitch and unhitch"): it runs with the two closing checks (so the summary should
   count three tests), and the keeper is back where he stood.
2. **Run** on one group's row (say "Morgue").
3. **Run** on the suite, then **Abort** after a few scenarios: the summary says aborted, the keeper is back, the test
   donkeys are gone.
4. A run of the suite to its end: the log has "the bench's panel is hidden while this test runs" six times.

**Looks 1 to 3, done by the user 2026-11-17 23:57** (one session; the three runs' files and the game's log are kept
with the audit's inputs). Every scenario that ran made as many checks as under the old runner's last run:

| Look | The summary | What ran | Around it |
|---|---|---|---|
| 1. One scenario ("hitch and unhitch") | `done (3 of 204 tests): 20 passed, 0 failed, 0 warning(s)` | hitch and unhitch (16 checks, 16 before), then the two closing checks (2 and 2): "all 5 came through unchanged", "no test donkey left behind (0)" | the same start lines as a full run (the keeper to the test spot, the light, the zone, 12 instances); the story and the zoom held and given back; no warning in the log |
| 2. One group (Morgue) | `done (4 of 204 tests): 19 passed, 0 failed, 0 warning(s)` | the group's two scenarios (7 and 8 checks, as before) in the list's order, then the two closing checks | the same |
| 3. The suite, aborted | `aborted (187 of 204 tests): 106 passed, 0 failed, 0 warning(s). (aborted from the bench)` | the first eight scenarios in the list's order, each with its old number of checks (10, 10, 6, 3, 16, 6, 10, 45); the abort came during the eighth ("the run is stopping (aborted from the bench); the cleanup still runs") | its cleanup ran (the test donkeys, cart, skin and mount of that scenario removed), no closing check ran (the old loop left at an abort before them too), the story and the zoom given back; the log's 4 warnings are the first scenarios' own, as in the full run |

After each of the three: the player's cart was called back beside the spot the keeper started from (the API's finish
put him back), the world held 12 instances as before the run, and `Debug.SelfTestSpot` in the cfg is the spot it
was (a run that isn't whole and clean doesn't write it).

**Look 4, done by the user 2026-11-18 12:04** (a run of the suite to its end, with the F11 window's Tests tab gone):
`done (187 of 204 tests): 2137 passed, 0 failed, 0 warning(s)`. The audit tool on this run's file: all 187 in the
old runner's order, each scenario's lines together, 0 of 187 make another number of checks than under the old
runner. No warning or error in the log before the run or after it; the 55 during it are the tests' own, each naming
the self-test or its made-up thing. The story and the zoom were held and given back.

The panel was hidden for all six measuring scenarios: every line of each lies inside a stretch of the log between
the bench's "hidden" and "shown again" lines (frame time by mode in one; the garbage survey, the mount crowd, the
views survey, the journal survey and the skin builds compared, which run one after another, in a second that lasts
through all five). The run showed one difference from the old runner, and two faults of the new log lines:

- **One frame.** The old runner hid its panel in the same step that started a scenario. The F11 window is still
  hidden that way (`BeforeScenario`). The bench hid its own panel in the panel's `Tick`, the frame after the test
  began: it was drawn in the first frame of a measuring scenario (the garbage survey's first line stands one line
  before the "hidden" line). No check depends on it (the counts are the same), and a survey's numbers are taken
  over hundreds of frames, but it is not what the old runner did. Fixed in the bench the same day: the runner tells
  the host the moment a test begins, before the suite's step and the test's first, and the panel goes there.
- The "shown again" line was also written when the panel was opened again by hand (line 615 of that log, before the
  run), and five scenarios in a row got one "hidden" line. Fixed with it: a line a measuring scenario, "shown again"
  only after one.

The fix is built, tested offline and deployed. **Seen in the next full run** (2026-11-18 13:14, on game 1.012.9;
2141 passed, 0 failed, 0 warnings, `docs/game-update-26734308.md`): six "hidden" lines, each the line before its
scenario's first line, and "shown again" after frame time by mode and after the last of the five in a row.

Still not seen in the game: a scenario that throws, a refused start, a run ended from outside. They can't be made
to happen by pressing Run on a healthy game; they stay covered by the code's comparison (section 5) and the bench's
offline test.

## 8. What is still under F11

- **The Tests tab**: at the time of the audit, four lines (where the tests are, how many scenarios the API lists for
  the bench, the remembered spot), no button, no runner. **Taken out 2026-11-18** (the user: "the tests tab in the
  f11 window is still there"): the window has twelve tabs, and its three texts that named the tab name the bench's
  panel (F4). The remembered spot is still the Debug tab's `SelfTestSpot` row.
- **The other tabs** (Cargo, Review, Flying, Debug, ...): the developer's tools, each started by hand: the cargo
  loads and the review, the probes (shapes, reach, held items, the lantern), the surveys, the script dumps. They
  were never scenarios of the runner and make no PASS or FAIL; their files are among the 85 that are the same byte
  for byte.

## The table: every scenario

Checks are PASS, FAIL and WARN lines. "Old runner": the run of 2026-11-17 15:59. "Bench": the run of 22:13.

| # | Group | Scenario | Body | Checks, old runner | Checks, bench | PASS | FAIL | WARN | INFO | SKIP | Same |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Core | the per-mod API (ids, keys, data, owner rules) | `ApiIdentity` | 10 | 10 | 10 | 0 | 0 | 1 | 0 | yes |
| 2 | Core | kept-with-save text, round trip | `RoundTrip` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 3 | Core | kept with save: a gone mod's lines set aside and written back, an ability's dropped | `AsideLines` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 4 | Core | the start's check of the game's code: the A* pathfinding's module id read, found and counted | `GateTests.Scopes` | 3 | 3 | 3 | 0 | 0 | 0 | 0 | yes |
| 5 | Core | hitch and unhitch | `HitchUnhitch` | 16 | 16 | 16 | 0 | 0 | 0 | 0 | yes |
| 6 | Core | from the saddle onto another mount (offered by it, the first left standing with its cart; a refusal keeps the ride); loading only facing the cart | `RideFromSaddleAndFacing` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 7 | Core | unhitch without a view (a parked cart stays) | `UnhitchWithoutView` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 8 | Core | mounts left in the world: another mount chosen while riding (the one ridden stays with its babies, steps aside, the new one ridden; chosen again, the same one back; the journal's babies, name and colours reach it; one cart, left standing and fetched, taken off the mount that has it) | `LeaveMountsTests.Switch` | 45 | 45 | 45 | 0 | 0 | 1 | 0 | yes |
| 9 | Core | mounts left in the world: off (switched off, the ones left go home, one ridden by hand once he is off it; chosen while ridden, one left is his again and the one out goes home; none left, changed in place as before), a load (found by their keys; a save's other mount left; with the option off, sent home), the side file's keys and parents | `LeaveMountsTests.OffAndLoad` | 23 | 23 | 23 | 0 | 0 | 0 | 0 | yes |
| 10 | Core | mounts left in the world: a save and a load the whole way (the side file written, every mount taken away, the file restored, the load step: each back where it was under its key, its baby, skin and name; found as left; one cart; picked again, the same one) | `LeaveMountsTests.SaveAndLoad` | 12 | 12 | 12 | 0 | 0 | 0 | 0 | yes |
| 11 | Core | a called mount brings its babies (out of reach: placed by it in the call, ridden at once; within reach: they trot over), through the journal and the ride key | `LeaveMountsTests.CalledBabies` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 12 | Core | a mount whose saved skin isn't installed wears its own look and is itself, its baby too, the choice kept; the donkey's missing skin and a missing mount still the donkey | `LeaveMountsTests.MissingSkin` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 13 | Core | the cart key while riding where the hitch would seat the keeper on blocked ground: refused before the cart moves (it stays where it stands, not hitched; with no cart yet, none left behind); where the seat is free, hitched as always | `CartSeatTests.SeatBlocked` | 8 | 8 | 8 | 0 | 0 | 0 | 0 | yes |
| 14 | Core | shared colour textures | `SharedColours` | 3 | 3 | 3 | 0 | 0 | 0 | 0 | yes |
| 15 | Core | settings: a moved section keeps a player's values | `MovedSections` | 18 | 18 | 18 | 0 | 0 | 0 | 0 | yes |
| 16 | Core | upgrade: a released version's player files are all read | `UpgradeFiles` | 43 | 43 | 43 | 0 | 0 | 4 | 0 | yes |
| 17 | Core | upgrade: Saddle Up's cfg as every released version wrote it lands on today's page | `UpgradeSaddleUpCfg` | 14 | 14 | 14 | 0 | 0 | 7 | 0 | yes |
| 18 | Levels | one new view per frame | `SpawnCap` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
| 19 | Levels | waking (view switched off) | `() => Wake(switchOff: true)` | 11 | 11 | 11 | 0 | 0 | 0 | 0 | yes |
| 20 | Levels | waking (view frozen) | `() => Wake(switchOff: false)` | 14 | 14 | 14 | 0 | 0 | 0 | 0 | yes |
| 21 | Levels | mounted while idle (brought to the keeper and ridden in one frame) | `MountWhileIdle` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 22 | Levels | reused views | `Reuse` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 23 | Levels | foals keep up off screen | `FoalsFollow` | 3 | 3 | 3 | 0 | 0 | 0 | 0 | yes |
| 24 | Levels | foals: follow the keeper on foot outdoors, with the other babies settings | `FoalsOnFoot` | 21 | 21 | 21 | 0 | 0 | 0 | 0 | yes |
| 25 | Levels | foals: each baby its own size, shade and temperament, the same every load | `FoalVarietyChecks` | 8 | 8 | 8 | 0 | 0 | 0 | 0 | yes |
| 26 | Levels | foals: a baby with no room beside its parent stands there (wedged, no rejoin to where it stands, no catch-up onto its own spot) | `FoalRoomTests.NoRoom` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 27 | Levels | foals: the stuck report leaves out a baby still by design (asleep beside its asleep parent), names the parent's real view, and still reports an active one that can't get nearer | `FoalRoomTests.ReportResting` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 28 | Levels | no leaks over spawn and remove | `Leaks` | 5 | 5 | 5 | 0 | 0 | 2 | 0 | yes |
| 29 | Levels | frame time by mode | `FrameTimes` | 0 | 0 | 0 | 0 | 0 | 5 | 0 | yes |
| 30 | Levels | babies: Saddle Up's babies rows, in the journal, reaching its companion | `SaddleUpBabiesRows` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 31 | Levels | babies: Saddle Up's own babies, each its own temperament | `SaddleUpFoalTempers` | 0 | 0 | 0 | 0 | 0 | 1 | 0 | yes |
| 32 | Levels | mounts left in the world: Saddle Up's row (the options window's Mount section, off by default, reaching its companion) | `SaddleUpLeaveMountsRow` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 33 | Chest | chest: hide, move, show (and off screen) | `ChestHideMoveShow` | 8 | 8 | 8 | 0 | 0 | 3 | 0 | yes |
| 34 | Chest | chest: zone out and in | `ChestZoneRoundTrip` | 13 | 13 | 13 | 0 | 0 | 1 | 0 | yes |
| 35 | Chest | chest: ten stand-ins | `ChestStandIns` | 4 | 4 | 4 | 0 | 0 | 3 | 0 | yes |
| 36 | Chest | chest: the ride (step 1) | `ChestRide` | 37 | 37 | 37 | 0 | 0 | 1 | 0 | yes |
| 37 | Chest | chest: the backstops | `ChestBackstops` | 9 | 9 | 9 | 0 | 0 | 2 | 0 | yes |
| 38 | Chest | chest: the save as the game writes it | `ChestSaveImage` | 16 | 16 | 16 | 0 | 0 | 2 | 0 | yes |
| 39 | Chest | chest: a zombie using it | `ChestZombieGuard` | 11 | 11 | 11 | 0 | 0 | 1 | 0 | yes |
| 40 | Cargo | cargo: more than two items (layers, a re-made cart, the setting) | `CargoLayers` | 17 | 17 | 17 | 0 | 0 | 0 | 0 | yes |
| 41 | Cargo | cargo: a cart with a hitch gap keeps its place as items go on | `CargoKeepsTheGap` | 3 | 3 | 3 | 0 | 0 | 0 | 0 | yes |
| 42 | Cargo | cargo: a zombie with the game's Halloween head on the cart (head 1051 drawn as the table's 1060, the swap forced off and on) | `ZombieHeadTests.EventHeadOnTheCart` | 3 | 3 | 3 | 0 | 0 | 0 | 0 | yes |
| 43 | Cargo | cargo: the pictures from the game's shared pool (a view made while the keeper carries something, the stand-ins) | `CargoPooledPictures` | 7 | 7 | 7 | 0 | 0 | 1 | 0 | yes |
| 44 | Cargo | cargo: each mount carries its own (a lighter mount leaves an over-full cart standing) | `CargoPerMount` | 11 | 11 | 11 | 0 | 0 | 0 | 0 | yes |
| 45 | Indoors | indoors: the two switches (refused, ended, allowed) under the indoor light | `IndoorsRules` | 19 | 19 | 19 | 0 | 0 | 0 | 0 | yes |
| 46 | Indoors | indoors: through a building's door riding and driving, and back out | `IndoorsThroughADoor` | 18 | 18 | 18 | 0 | 0 | 1 | 0 | yes |
| 47 | Indoors | indoors: the cart left at the morgue door, the corpse into the hands, the mount inside | `IndoorsMorgue` | 11 | 11 | 11 | 0 | 0 | 0 | 0 | yes |
| 48 | Indoors | indoors: Saddle Up's two rows (the second greys while the first is off; the first reaches the API) | `SaddleUpIndoorsRows` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
| 49 | Morgue | morgue: the door rule | `MorgueDecisions` | 7 | 7 | 7 | 0 | 0 | 0 | 0 | yes |
| 50 | Morgue | morgue: a corpse off the parked cart into the hands | `MorgueHandOver` | 8 | 8 | 8 | 0 | 0 | 0 | 0 | yes |
| 51 | Corpses | corpses: drop and stand points | `CorpsePoints` | 11 | 11 | 11 | 0 | 0 | 1 | 0 | yes |
| 52 | Corpses | corpses: one body left | `CorpseOneBody` | 14 | 14 | 14 | 0 | 0 | 0 | 0 | yes |
| 53 | Corpses | corpses: the patches follow the switch | `CorpseSwitch` | 5 | 5 | 5 | 0 | 0 | 1 | 0 | yes |
| 54 | Corpses | corpses: the notification, swapped once | `CorpseNotification` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 55 | Corpses | corpses: twelve bodies at once | `CorpseManyBodies` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 56 | Corpses | corpses: never rot | `CorpseNeverRot` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 57 | Corpses | corpses: an unknown point steps aside | `CorpseUnknownPoint` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 58 | Corpses | corpses: walk decisions | `CorpseWalkDecisions` | 9 | 9 | 9 | 0 | 0 | 1 | 0 | yes |
| 59 | Fight | fight: the armour on and off while riding | `FightArmourWhileRiding` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 60 | Fight | fight: mounting in armour, every cart path refused | `FightMountInArmour` | 11 | 11 | 11 | 0 | 0 | 0 | 0 | yes |
| 61 | Fight | fight: the armour on while driving leaves the cart | `FightArmourWhileDriving` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 62 | Fight | fight: a sword attack from the saddle | `FightAttackRides` | 11 | 11 | 11 | 0 | 0 | 0 | 0 | yes |
| 63 | Fight | fight: build mode dismounts | `FightBuildModeDismounts` | 5 | 5 | 5 | 0 | 0 | 0 | 0 | yes |
| 64 | Looks | skin pack: a folder read, validated, worn, withdrawn | `SkinPack` | 15 | 15 | 15 | 0 | 0 | 1 | 0 | yes |
| 65 | Looks | recipe: built from the donkey's frames, worn whole | `SkinRecipe` | 7 | 7 | 7 | 0 | 0 | 0 | 0 | yes |
| 66 | Looks | the donkey's art: the game's frames against the ones the skins were drawn on (donkey-frames.txt; the comparison on made-up lists, a known picture's fingerprint as donkey_frames.py makes it) | `DonkeyFramesTests.Check` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 67 | Looks | the donkey's eye and nostril pixels repainted in the game's atlas, nothing else changed (face-pixels.txt; its reading on a made-up frame; every frame put back as drawn is donkey-frames.txt's) | `FacePixelsTests.Repainted` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 68 | Looks | mount from a pack: declared, worn, no cart, its tuning, withdrawn | `MountFromPack` | 31 | 31 | 31 | 0 | 0 | 0 | 0 | yes |
| 69 | Looks | mount from code: the ability's hooks on spawn, ride, dismount, removal | `MountFromCode` | 17 | 17 | 17 | 0 | 0 | 1 | 0 | yes |
| 70 | Looks | tuning: a value belongs to the owner that set it; what a mount and an ability may pin | `TuningOwnership` | 23 | 23 | 23 | 0 | 0 | 1 | 0 | yes |
| 71 | Looks | ids: one mount.cfg per mount (a newer one of the same id takes over, an older one is refused); a mount folder without mount.cfg joins; the same skin twice, the newer wins | `IdRules` | 10 | 10 | 10 | 0 | 0 | 4 | 0 | yes |
| 72 | Looks | ids: a code mount meets the version rule (another mod's older or equal one left out, its handle doing nothing; a newer one, code or pack, replaces it; an older pack refused with its skins); its clips banked under its mod | `IdRulesCode` | 14 | 14 | 14 | 0 | 0 | 4 | 0 | yes |
| 73 | Looks | default looks: one per mount (its skins\default\; a mount pack without one refused, a joiner's left out); one skin name on two mounts; a mount id without its mod's prefix refused; default_skin ignored; a skin added to another's mount without its mod's prefix refused (folder, code skin, donkey recipe) | `DefaultSkinRules` | 16 | 16 | 16 | 0 | 0 | 2 | 0 | yes |
| 74 | Looks | layering: a variant built on its default (pictures, settings, tuning, the default's sounds and colours; its file keeps its own lines) | `SkinVariant` | 17 | 17 | 17 | 0 | 0 | 0 | 0 | yes |
| 75 | Looks | layering: a variant drawn over its default (the erase colour's holes, its pixels on top, the colours by where they came from) | `SkinOverDefault` | 7 | 7 | 7 | 0 | 0 | 0 | 0 | yes |
| 76 | Looks | layering: a variant before its default, the default read again and withdrawn, another canvas, a code skin's sentinels, the donkey's skins unchanged | `SkinLayerLifecycle` | 11 | 11 | 11 | 0 | 0 | 2 | 0 | yes |
| 77 | Looks | format: description, portrait, stats, ability names, needs, a bad format, unknown [Game] keys | `PackFormat` | 13 | 13 | 13 | 0 | 0 | 2 | 0 | yes |
| 78 | Looks | hostile files: a picture refused from its header, a bad WAV chunk, NaN and Infinity, names outside the pack, a BOM | `HostileFiles` | 16 | 16 | 16 | 0 | 0 | 2 | 0 | yes |
| 79 | Looks | between mods: no throw (an effect and a code skin meet the version rule, a lost mount's own look left out, an effect id needs its prefix, a row's throwing function) | `NoThrowBetweenMods` | 10 | 10 | 10 | 0 | 0 | 4 | 0 | yes |
| 80 | Looks | between mods: order and identity (v2 beside v1 whole, an id not its folder's refused, a skin before its mount, a skin of a mount not installed, handles, one change per folder) | `OrderAndIdentity` | 11 | 11 | 11 | 0 | 0 | 3 | 0 | yes |
| 81 | Looks | between mods: versions and ids (needs in mount.cfg, ASCII ids, versions that aren't numbers, sound ids, one mod's sound for an event) | `VersionsAndIds` | 9 | 9 | 9 | 0 | 0 | 2 | 0 | yes |
| 82 | Looks | speed: each mount's own, ridden and with the cart; the donkey's from the cfg; the panel's words | `MountSpeeds` | 18 | 18 | 18 | 0 | 0 | 1 | 0 | yes |
| 83 | Looks | freeplay: max cargo and one speed for every mount (the words' one table, the cart's slots, ridden, driven, changed while riding, the panel, MountInfo) | `Freeplay` | 26 | 26 | 26 | 0 | 0 | 0 | 0 | yes |
| 84 | Looks | settings: the Mount, Skin and babies rows | `SettingsRows` | 36 | 36 | 36 | 0 | 0 | 0 | 0 | yes |
| 85 | Looks | looks per skin: a row set per skin, the 0.12 values moved to the skin worn, each skin's look when worn, a late skin caught up, regions per skin | `LooksPerSkin` | 14 | 14 | 14 | 0 | 0 | 0 | 0 | yes |
| 86 | Looks | keys: the mods' keys wait while a text box of any window has the cursor (a TextMeshPro box, a Unity UI box, not a selected button) | `KeysWaitWhileTyping` | 5 | 5 | 5 | 0 | 0 | 0 | 0 | yes |
| 87 | Looks | settings: the Mounts panel opens, fits, follows the list, the colours window, the key | `PanelSmoke` | 80 | 80 | 80 | 0 | 0 | 15 | 0 | yes |
| 88 | Looks | settings: the Mounts panel's way back (from the game; over the pause menu through the colours window; over the Character window on the Tech Tree) | `PanelWayBack` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 89 | Looks | settings: the Mounts panel's Ride and Drive buttons while riding keep the ride | `PanelRideButtons` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 90 | Looks | settings: the Mount Journal's ride and cart keys work as its Ride and Drive buttons | `PanelRideKeys` | 11 | 11 | 11 | 0 | 0 | 0 | 0 | yes |
| 91 | Looks | settings: the options window (every option our own control in its section; a switch; a key set, in words, a clash; the line at the bottom; Defaults asked) | `OptionsTests.Smoke` | 33 | 33 | 33 | 0 | 0 | 16 | 0 | yes |
| 92 | Looks | settings: the options window's way back (the Settings window's button; the Mounts button and back; the journal's Options button and back; Ride from the journal over them) | `OptionsTests.WayBack` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 93 | Looks | settings: the options window among other mods (the window's own OK copied, another mod's part left off it, our button added again when taken away; another mod's key and the game's named; a game window over the options) | `OptionsTests.AmongOtherMods` | 13 | 13 | 13 | 0 | 0 | 1 | 0 | yes |
| 94 | Looks | settings: another mod's window open (the shared marker): the options refuse to open; the pause and the controls go back with the last window | `OptionsTests.OthersOpen` | 5 | 5 | 5 | 0 | 0 | 0 | 0 | yes |
| 95 | Looks | settings: a row's mount data is its own (a skin's parts are its tone rows alone, the cart's rows no skin part) | `OptionsTests.RowData` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | yes |
| 96 | Translations | translations: the files (a translator's own beats the bundled; comma, tab and ; CSVs; the English changed; {0}; the language's base id and a close one; the test language; the API's English and its template read back) | `TranslationTests.Files` | 17 | 17 | 17 | 0 | 0 | 1 | 0 | yes |
| 97 | Translations | translations: the test language over the options window, the Mount Journal and the colours window (every word looked up; the room at 1.6 times) | `TranslationTests.Windows` | 25 | 25 | 25 | 0 | 0 | 32 | 0 | yes |
| 98 | Translations | translations: a German translation in the windows (the page's de.txt with a row whose English changed, the API's de.csv saved with ; and quotes, read back from the options window, the journal and the colours window) | `TranslationTests.German` | 6 | 6 | 6 | 0 | 0 | 1 | 0 | yes |
| 99 | Translations | translations: the Settings window's options button in every language (its words, its font the OK button's and its width at the switch, before any hover) | `TranslationTests.SettingsButtonLanguages` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | yes |
| 100 | Translations | translations: every game language, the font our windows draw with (switched for a moment as the Settings window does, the player's own back; GK2-l10n-fonts.tsv) | `TranslationTests.Fonts` | 33 | 33 | 33 | 0 | 0 | 1 | 0 | yes |
| 101 | Translations | translations: the layout in every language (our real translations in the game's own font for each: the options window's sections and descriptions, the journal on every mount, the colours window; a one-line text that doesn't fit fails, a wrapped text cut warns) | `TranslationTests.Layout` | 10 | 10 | 10 | 0 | 0 | 331 | 0 | yes |
| 102 | Translations | translations: the game's bubble and prompt in every language (the Keeper's longest sentence said for real, the longest prompt over the nearest mount, measured in each language's font; a one-line text that doesn't fit fails, a wrapped text cut warns) | `TranslationTests.GameBubbles` | 11 | 11 | 11 | 0 | 0 | 34 | 0 | yes |
| 103 | Translations | translations: the translator mistake catalogue (every way a person's spreadsheet or editor gets a file wrong: what shows, never a wrong text, and whether the translator is told) | `TranslationTests.Mistakes` | 231 | 231 | 231 | 0 | 0 | 1 | 0 | yes |
| 104 | Translations | translations: the keeper's bubbles, the prompts on foot and the crossroads notice (the test language and a German de.txt; the reasons stay English; a bubble said for real) | `TranslationTests.Bubbles` | 15 | 15 | 15 | 0 | 0 | 0 | 0 | yes |
| 105 | Translations | translations: the mounts' and skins' names in their owners' files (the test language and German: the journal's list and header, the Skin list; every owner's English) | `TranslationTests.PackNames` | 8 | 8 | 8 | 0 | 0 | 1 | 0 | yes |
| 106 | Measurements | measure: every skin of every mount exported as built (GK2-asset-probe\game-skins, the review page's) | `MeasureTests.ExportSkins` | 0 | 0 | 0 | 0 | 0 | 1 | 0 | yes |
| 107 | Measurements | measure: the journal picture with the lamp lit, dark and off, every donkey skin (GK2-lamp-framing.tsv) | `MeasureTests.LampFraming` | 0 | 0 | 0 | 0 | 0 | 17 | 0 | yes |
| 108 | Measurements | measure: the colours window of every skin of every mount (GK2-panel-shots: colours-skin-*.png; GK2-colours-rows.tsv) | `MeasureTests.ColoursWindows` | 0 | 0 | 0 | 0 | 0 | 60 | 0 | yes |
| 109 | Measurements | measure: the colour swap's cost, 21 donkeys at the donkey's own pairs, 64, 72, 96 and on the cube (GK2-palette-cost.tsv) | `MeasureTests.PaletteCost` | 0 | 0 | 0 | 0 | 0 | 37 | 0 | yes |
| 110 | Measurements | measure: a skin over the donkey against the donkey under it, in art pixels on screen, in the world and the journal's picture (GK2-skin-offset.tsv) | `MeasureTests.SkinOffset` | 0 | 0 | 0 | 0 | 0 | 14 | 0 | yes |
| 111 | Measurements | measure: the game's Halloween head swap on the zombie pictures (carried, on the cart, on the ground; the swap forced off and on) | `HalloweenProbe.Heads` | 0 | 0 | 0 | 0 | 0 | 23 | 0 | yes |
| 112 | Looks | skins and mounts: the changed events | `ChangedEvents` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 113 | Looks | skin: a game-source skin with no frames is set aside, the donkey comes, offered again when they are there | `AsideSkin` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 114 | Looks | colours: a skin over the donkey keeps the donkey's regions | `SkinOverColours` | 16 | 16 | 16 | 0 | 0 | 5 | 0 | yes |
| 115 | Looks | colours: a skin's own tones, each its own colour and its own rows | `ToneTests.Tones` | 35 | 35 | 35 | 0 | 0 | 1 | 0 | yes |
| 116 | Looks | colours: a look's names for its parts (regions), the Dark and Eye rows, a part that followed a region taking its saved value once | `RegionTests.Regions` | 14 | 14 | 14 | 0 | 0 | 0 | 0 | yes |
| 117 | Looks | over skin: the erase colour clears the donkey under it | `SkinOverErase` | 8 | 8 | 8 | 0 | 0 | 2 | 0 | yes |
| 118 | Looks | skin pack: read again by itself when its files change (dev switch) | `SkinPackAutoReload` | 3 | 3 | 3 | 0 | 0 | 0 | 0 | yes |
| 119 | Looks | speed: our mounts' words and cargo as their packs say them; Saddle Up's speed rows retired | `ShippedSpeeds` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 120 | Looks | skin: on, off, removed (pictures loaded and freed) | `SkinOnOff` | 5 | 5 | 5 | 0 | 0 | 0 | 0 | yes |
| 121 | Looks | skins: no leaks over spawn and remove | `SkinLeaks` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 122 | Looks | skin: kept through a hitch, freed asleep far away, worn again awake | `SkinSleep` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 123 | Looks | effect: hellfire lit (the test hook), follows, out, gone with the donkey | `EffectOnOff` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 124 | Looks | built in or external: our mounts and the donkey's skins from their packs' own DLLs (owner, plugin, needs, Hellfire's effect), Larry from Saddle Up's, the skins offered though late, no old ids | `ExternalPacks` | 29 | 29 | 29 | 0 | 0 | 0 | 0 | yes |
| 125 | Looks | game creature: Larry's frames found, scaled and worn | `GameCreature` | 16 | 16 | 16 | 0 | 0 | 0 | 0 | yes |
| 126 | Looks | seat: the rider's height on every shipped mount, AutoSeat off and on (a readout) | `SeatReadout` | 11 | 11 | 11 | 0 | 0 | 16 | 0 | yes |
| 127 | Looks | cart: the hitch gap and the cart-style seat on every shipped mount (a readout) | `CartReadout` | 15 | 15 | 15 | 0 | 0 | 10 | 0 | yes |
| 128 | Looks | effect: the ring's size by the worn skin's drawn width (the test hook) | `SkinEffect` | 3 | 3 | 3 | 0 | 0 | 0 | 0 | yes |
| 129 | Looks | front layer: the dragon's near wing stays in front of the rider (a readback) | `FrontLayer` | 8 | 8 | 8 | 0 | 0 | 5 | 0 | yes |
| 130 | Looks | preview: the cart sits at the new mount's hitch gap as soon as the mount is switched | `PreviewGap` | 5 | 5 | 5 | 0 | 0 | 3 | 0 | yes |
| 131 | Looks | skins: Saddle Up's dragon with a skin not installed: the row keeps the choice, its colour rows are the dragon's own look's | `SaddleUpMissingSkinRows` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 132 | Looks | skins: a slow stand-in (Larry with a skin not installed) is worn though its rig is refreshed every frame while it builds | `MissingSkinSlowBuild` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
| 133 | Looks | colours: the Coat row reaches every body colour of every shipped mount, and no other | `CoatEveryBuiltIn` | 16 | 16 | 16 | 0 | 0 | 4 | 0 | yes |
| 134 | Looks | colours: the Second tone row reaches the rat's pink parts, the snail's shell and the dragon's belly, and is left out elsewhere | `SecondTone` | 4 | 4 | 4 | 0 | 0 | 1 | 0 | yes |
| 135 | Looks | colours: the outline is a part of its own: its Outline row colours it, Legs and mane and the Coat don't (the donkey, Hellfire; the dragon's outline no row) | `OutlineAsDrawn` | 8 | 8 | 8 | 0 | 0 | 0 | 0 | yes |
| 136 | Looks | colours: the donkey's Muzzle row reaches its nose only; the face's dots are its Coat, the belly's line its outline | `MuzzleNoseOnly` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
| 137 | Looks | colours: the donkey's eye and nostril repainted: the eye's line as drawn under Legs and mane, the nostril follows the Muzzle (the plain donkey and Hellfire) | `FacePixelsPairs` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 138 | Looks | colours: a colour picked for a part a skin pre-colours replaces the pre-colour (Hellfire's ash coat), a shade stays on it | `PickedColourOverBase` | 3 | 3 | 3 | 0 | 0 | 0 | 0 | yes |
| 139 | Looks | colours: Hellfire gives the donkey's eye no row (its glowing eyes cover it), its other rows kept | `HellfireNoEyesRow` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | yes |
| 140 | Looks | over skins: Hellfire and Royal Parade worn, recoloured and walking; the snail's own rider offset (a readback) | `OverBuiltIns` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 141 | Looks | colours: the player's regions on a mount in its own colours | `SkinColours` | 7 | 7 | 7 | 0 | 0 | 0 | 0 | yes |
| 142 | Looks | layering: the Donkey Skins pack's two skins have no default, nothing resolved, as before | `DonkeySkinsUnlayered` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
| 143 | Looks | freeplay: Saddle Up's Freeplay rows (the last section, the API's words, shown with the switch on, each driving its key) | `SaddleUpFreeplayRows` | 8 | 8 | 8 | 0 | 0 | 0 | 0 | yes |
| 144 | Looks | options: Saddle Up's five tabs (Mount Behaviour and Sound under their headings in Mount, which fits without a scrollbar; the journal button named as the window; Send your mount away after the keys that call it) | `SaddleUpTabs` | 5 | 5 | 5 | 0 | 0 | 0 | 0 | yes |
| 145 | Looks | settings: with the Mounts panel open Saddle Up's ride and cart keys work only as its Ride and Drive buttons, its panel key works | `SaddleUpKeysOverPanel` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
| 146 | Flying | landing: the walk-away test (the keeper's own ground, a walled pen, the cap's time) | `FlightWalkAway` | 5 | 5 | 5 | 0 | 0 | 2 | 0 | yes |
| 147 | Flying | jump: a take-off past the game's walk grid (its nearest free cell walled off: the way not asked; a take-off inside the pen: no way) | `FlightGridTakeOff` | 4 | 4 | 4 | 0 | 0 | 1 | 0 | yes |
| 148 | Flying | mounts: the flies and jumps flags ([Mount] flies, jumps; a pack's mount flies by its flag) | `FlightFlags` | 7 | 7 | 7 | 0 | 0 | 0 | 0 | yes |
| 149 | Flying | keys: jump and fly on one key (a second press in the air takes off a flier), and on two | `FlightOneKey` | 25 | 25 | 25 | 0 | 0 | 0 | 0 | yes |
| 150 | Flying | jump: each mount its own ([Jump] in mount.cfg, MountDef.Jump): read, saved, refused in a skin.cfg, jumped live | `FlightJumpPerMount` | 18 | 18 | 18 | 0 | 0 | 0 | 0 | yes |
| 151 | Flying | jump: beside a real thing (the yard's workbenches) and on ground the NPCs' mesh doesn't cover (a bridge): a jump in place lands in place | `FlightJumpBeside` | 2 | 2 | 2 | 0 | 0 | 3 | 0 | yes |
| 152 | Flying | jump: the mount's arc (held and tapped, the same at any frame rate), and jumped live riding plain: in place, over a low wall, stopped by a tall one | `FlightJump` | 36 | 36 | 36 | 0 | 0 | 0 | 0 | yes |
| 153 | Flying | jump: the home yard (every built thing and fence jumped at, a slide along the fence): down, never sent back, never underground | `FlightJumpYard` | 3 | 3 | 3 | 0 | 0 | 37 | 0 | yes |
| 154 | Flying | jump: a real hillside (terrain levels) run up and down: the body moves in the air, down on the ground, never held, never sent back | `FlightJumpSlope` | 2 | 2 | 2 | 0 | 0 | 2 | 0 | yes |
| 155 | Flying | in the air: the game's E and F prompts off (a jump leaves them), the work state refused, a ride ended up there parks the mount on the ground | `FlightInTheAir` | 8 | 8 | 8 | 0 | 0 | 1 | 0 | yes |
| 156 | Flying | mounts left in the world: another mount chosen in the air waits for the landing (the flier still his and in its skin), then the flier is left where it landed and the one chosen comes and is ridden | `LeaveMountsTests.InTheAir` | 8 | 8 | 8 | 0 | 0 | 0 | 0 | yes |
| 157 | Flying | keys: Saddle Up's Jump and Fly rows (Space, shown only while flying is on, the Behaviour keys follow) | `FlightKeyRows` | 8 | 8 | 8 | 0 | 0 | 0 | 0 | yes |
| 158 | Flying | mounts: our mounts' flies and jumps flags (the dragon flies, the rest jump and don't) | `ShippedFlightFlags` | 4 | 4 | 4 | 0 | 0 | 0 | 0 | yes |
| 159 | Flying | jump: the dragon's own (its [Jump] with wings; live, a take-off and landing on its flying frames) | `DragonJump` | 5 | 5 | 5 | 0 | 0 | 0 | 0 | yes |
| 160 | Flying | rider: still on the dragon's flying pictures while the game walks under them (the stick held in the air), its own BobInFlight bringing the motion back, the bob on the ground after | `DragonRiderSteady` | 5 | 5 | 5 | 0 | 0 | 0 | 0 | yes |
| 161 | Safety | safe spots: standing records one, under the ground never; a seed after the load | `SafeRecord` | 6 | 6 | 6 | 0 | 0 | 0 | 0 | yes |
| 162 | Safety | the fall watchdog: under the ground, far below the world, caught; a drop onto the ground and standing under a bridge, left alone | `SafeWatchdog` | 4 | 4 | 4 | 0 | 0 | 2 | 0 | yes |
| 163 | Safety | back to the last safe spot: the API call (on foot, riding), refused with the reason (controls taken, no spot), the watchdog off | `SafeReturn` | 7 | 7 | 7 | 0 | 0 | 0 | 0 | yes |
| 164 | Safety | side file: held after an error (a save leaves it, never deletes it), the last one kept as .bak, a stale one set aside | `SideFileHeldAndAside` | 6 | 6 | 6 | 0 | 0 | 2 | 0 | yes |
| 165 | Safety | side file: a record that fails to come back is carried forward; a newer version's keys and lines written back | `SideFileCarriedForward` | 6 | 6 | 6 | 0 | 0 | 1 | 0 | yes |
| 166 | Safety | handles: another mod's Remove refused; a mod's data capped | `HandleOwnership` | 5 | 5 | 5 | 0 | 0 | 0 | 0 | yes |
| 167 | Safety | safe spots: Saddle Up's rows (the toggle drives FallGuard, the button brings him back, the key reaches ReturnKey) | `SaddleUpSafeSpotRows` | 5 | 5 | 5 | 0 | 0 | 1 | 0 | yes |
| 168 | Sounds | the probe: own clips as game sounds (a tone from WAV bytes and from a file, registered in AudioConfig, played by the game's player with its 3D preset and mixer group, taken out again) | `SoundsProbe` | 13 | 13 | 13 | 0 | 0 | 0 | 0 | yes |
| 169 | Sounds | the donkey unchanged: every event of the rig resolves to the [Ride] and [Cart] keys, the API's voice and bell play the game's sounds, a standing one and a foal sound under the standing switch, the lips event taken only when played, a standing wearer silent by its mount's pins | `SoundsDonkeyUnchanged` | 15 | 15 | 15 | 0 | 0 | 0 | 0 | yes |
| 170 | Sounds | a pack's own sounds: [Sounds] lines and the sounds folder loaded as game sounds, the convention name, the layers (skin over mount over the keys, a style block), the player's switch on top, a second skin of the mount, the save round trip, the clean-up | `SoundsPack` | 29 | 29 | 29 | 0 | 0 | 1 | 0 | yes |
| 171 | Sounds | a pack's own clip and the base's start offset: the donkey's 0.42 s is its snort's, a layer's own file starts at 0 unless its line says start; a start past a clip's end is dropped, never seeked (the Unity log read back: no audio error) | `SoundsStartOffset` | 6 | 6 | 6 | 0 | 0 | 1 | 0 | yes |
| 172 | Sounds | the API: Mounts.Sounds (the catalogue, Resolved with its layer, a mod's Set over everything and back, its own clips kept, Play and Stop, the Played event), a code mount's MountSound shared by a pack skin of it | `SoundsApi` | 16 | 16 | 16 | 0 | 0 | 0 | 0 | yes |
| 173 | Sounds | an embedded pack's sounds (the built-in skins' route): the API's own test pack, its .ogg decoded through the cache file, its .wav parsed in place, a looping event stopped | `SoundsEmbedded` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 174 | Sounds | the flying events: takeoff, the launch's flaps and the beats airborne, the fly loop, land, touchdown, landed, a jump's jump and jumpland; nothing of the flight on a jump | `SoundsFlying` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 175 | Sounds | the game paused mid-walk: the pause menu over a ride with the stick held idles the keeper through the riding pose (the game's own stop), the ridden donkey and a trotting foal stand, no hoofstep while paused, the foal's step goes on after | `SoundsPaused` | 10 | 10 | 10 | 0 | 0 | 0 | 0 | yes |
| 176 | Sounds | herds and babies: a standing grown one calls on its own (rarer, one voice at a time in the scene), its babies answer one after another with [Sounds.Baby]'s sound, no call with the standing switch off | `SoundsHerd` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 177 | Sounds | the dragon's sound files: the flight's in its pack's mount folder (the mount's, for every dragon skin), none the red dragon's own, decoded once through the cache from the pack's DLL | `SoundsDragonFiles` | 9 | 9 | 9 | 0 | 0 | 0 | 0 | yes |
| 178 | Sounds | a standing snail: no voice and no lips by its [Sounds] and the rule, its step its own squelch | `SnailStandingSounds` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
| 179 | Sounds | Saddle Up's "Standing mounts and babies sound" row drives the Behaviour key | `SaddleUpStandingSoundsRow` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
| 180 | Garbage | the garbage survey (measures only): on foot standing and walking, riding walking and still, the Mounts panel picking, the dev stride probe; the garbage by part in each | `GarbageSurvey` | 1 | 1 | 1 | 0 | 0 | 7 | 0 | yes |
| 181 | Garbage | the mount crowd (measures only): every shipped look with babies and carts, lanterns lit at night, uncapped; 12 then 36 in view, 120 more scattered (the levels), ridden through; rows in BepInEx\GK2-crowd.tsv; then everything gone and the counters back | `CrowdTests.MountCrowd` | 1 | 1 | 1 | 0 | 0 | 13 | 0 | yes |
| 182 | Garbage | the views survey (measures only): a mixed crowd made in view, the keeper far away and back twice (views released, parked, taken from the pool, made again), to an idle distance and back; each phase by path and step (the views probe) | `ViewsSurvey.Run` | 1 | 1 | 1 | 0 | 0 | 43 | 0 | yes |
| 183 | Garbage | the journal survey (measures only, two checks): opening it step by step, three times; its picture rendered every frame against when it can have changed, uncapped (it still renders, fewer photos); closed, the stage it leaves on against off | `JournalSurvey.Run` | 6 | 6 | 6 | 0 | 0 | 34 | 0 | yes |
| 184 | Garbage | the skin builds compared (measures only): the dragon's pictures as one PNG each and as one ready-made sheet, each built twice; time, frames, the most in one frame and the garbage by step | `BuildSurvey.Run` | 4 | 4 | 4 | 0 | 0 | 5 | 0 | yes |
| 185 | Walk | the walk survey (measures only): every mount ridden and with the cart, steered right and left by the test, a standing one and a baby near; the stride probe's rows in BepInEx/GK2-strides | `WalkSurvey` | 1 | 1 | 1 | 0 | 0 | 14 | 0 | yes |
| 186 | After | the donkey's eye and nostril pixels still repainted in the atlas after the run (scenes and doors crossed) | `FacePixelsTests.AfterRun` | 2 | 2 | 2 | 0 | 0 | 1 | 0 | yes |
| 187 | After | the world's own donkeys came through unchanged, no test donkey left behind | `AfterTheRun` | 2 | 2 | 2 | 0 | 0 | 0 | 0 | yes |
