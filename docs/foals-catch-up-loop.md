# A baby with no room beside its lead, and the stuck report on resting babies (investigation, 2026-11-06)

The user, 2026-11-06, after the 0.16.0 release: "investigate these: ... the catch-up loop, and the stuck report for
babies that are asleep". Both were seen on 2026-11-05 and left for later (docs/foals-called.md section 7).

Status: **built 2026-11-06 (section 5): A + B2 and the report fix; deployed 03:51, the Taken fix 14:34; F11 14:32 green but the no-room test skipped (section 5).**
- The user's "ok" (2026-11-06) took the recommendations: the report fix (3.4), and A + B for the loop.
- B turned out to mean no wedge at all (2.5). B2 replaced it, and the user chose "A + B2".

**The logs.** The 18:21 log that section 7 quotes has been overwritten. Two kept logs show the same things:
- `BepInEx\rehearsal-aside-2026-11-05\after-b\LogOutput.log`, from rehearsal B of 2026-11-05 with 0.16.0 / API 0.44.0.
  `Foals.cs`, `IdleShuffle.cs`, `FoalHops.cs` and `Tiers.cs` are unchanged since then (last commit 38f632e, 16:10 that
  day), so this is the released code.
- `BepInEx\packs-aside-2026-10-27\log-dragon.log`, an F11 run of 2026-10-27. `AsleepTick` hasn't changed since
  2026-10-01, nor `WatchFollowing` since 2026-10-26.

## 1. The catch-up loop: what the log shows

The yard by the house door, round (2, 6, 19). The parent #1 stands by the door and its babies are beside it. In the
rehearsal log:

| Lines | Baby | What happened |
|---|---|---|
| 397-401 | #62 | "can't follow ... (no step fits where it stands: root sample 4 ... blocked by Obstacle_01)", then "follows its parent again (1.1 away, to its near side)", then "no step fits" again (three times, two rejoins) |
| 402 | #62 | "caught up with its parent ... (its parent stands again) at (3.40, 6.00, 20.48)" |
| 422-432 | #64 | six "no step fits" and five "follows its parent again (0.9 away, to its near side)" in turn |
| 433 | #64 | "caught up ... (its parent stands again) at (2.51, 6.00, 19.58)" |
| 455 | #64 | "no step fits where it stands: turn sample 4 at (2.08, 6.00, 19.58)": the turn from the very spot the catch-up put it on |
| 456-468 | #64 | rejoins to real slots (1.4, 1.7, 1.9 away), each followed by "no step fits" at the new spot |

In the whole log: 14 "no step fits" and 12 rejoins. None of lines 397-433 carries the "(N more line(s) skipped)" note,
so no line was dropped there and the lines came slower than the 4-a-second cap. (Section 7's "at the cap" was about the
18:21 log and can't be checked any more.)

## 2. The causes (code)

A lively baby's shuffle step goes forward in its facing, within 35 degrees, or after a turn. A turn moves the root to
the body's other side (`TryStartStep`, `IdleShuffle.cs` 383-497). Beside a wall or a fence, every forward step and the
turn hit the obstacle, so the pick fails "blocked by the world".

Three ingredients make the loop:

1. **Wedged at home.** `NotePickFailed` (`Foals.cs` 755-774) makes a baby "wedged" after three world-blocked picks over
   8 s, at any distance from its lead. `LoseParent` then counts it lost.
   - That rule was written for a baby spawned against a wall away from its parent ("caught up like a lost one", the
     comment at 226).
   - A baby already at its place beside a parent that stands against a wall counts the same way.
2. **The wedge count is never reset on a rejoin.** The two rejoins (`Foals.cs` 436-447, straight; 453-462, a route)
   clear `followLost` and `wedged`. They leave `pickFailsInRow` and `firstPickFailAt` alone. Only a good pick
   (`NotePickOk`), a pick refused for crowding, or `CatchUp` (817) resets them.
   - So after the first wedge, the "three picks over 8 s" guard is gone.
   - The next world-blocked pick wedges it again at once.
3. **The rejoin goes nowhere.**
   - `NearSideOfParent` (212-222) is 0.9 from the lead, on the line towards the baby. A baby 0.9 away is already there:
     "0.9 away, to its near side".
   - The next frame it counts as arrived (`d < 0.25`, 388-395) without moving.
   - That arrival also sets `rejoinBackoff` to 0 (392), so the wait between rejoins never grows past 0.5 s.

Together, at the baby's own spot beside its lead:
- wedged, then a rejoin to where it stands, then arrived, then a pause (0.3 to 4 s);
- then a failed pick, and it is wedged again at once.

Between rejoins, once the parent has stood 6 s, the lost branch's last way out runs: `wedged && still >= 6f` (468-471),
a catch-up **in view**. That catch-up doesn't end the loop:
- `FreeSlot` (837-875) takes a spot with nobody on it and a clear line from the lead's body. It doesn't test the step
  there (the root's place, a turn, a step).
- The root goes to the slot minus the body offset, with the facing unchanged.
- So the baby lands where the step test refuses again: line 455's failed turn starts at the spot line 433 put it on
  (the same z, 19.58).

**What the player sees.**
- The rejoins move nothing, so most of the loop is invisible: the baby stands still beside its parent.
- Every so often, while the parent stands, the baby **jumps in view** to a slot a unit or so away. This is the jump the
  user objected to on 2026-10-01 ("a foal vanishing in view and appearing next to its parent looked broken").
- The log fills with "[foal]" lines. That budget is shared by every mount, so other mounts' lines get skipped.
- With the keeper as the lead it is the same: section 7's #62 indoors ("can't follow the keeper", "follows the keeper
  again", 36 times).

**Since when.**
- 0.40.0 (2026-10-26, docs/foals.md "Rejoin: ... `wedged` no longer stops a rejoin") opened the rejoin to a wedged baby
  without clearing its count. Ingredients 2 and 3 date from then.
- Before that, a wedged baby at home waited for the 6 s catch-up to the same spot, then was wedged again 8 s later.
  That was slower, but the same in-view jump.

### 2.5 Options (the user's)

- **A. Reset the wedge count on a rejoin**, in both rejoin paths (`pickFailsInRow = 0`). A new wedge then needs three
  fresh failures over 8 s.
  - A plain defect: needed whatever else is chosen.
  - Alone, it slows the loop to the wedge's own pace (8 s or more) but doesn't end it.
- **B as first written: a baby in its own area isn't wedged. Withdrawn 2026-11-06, after the user's "ok".**
  - A baby that isn't lost only tries a step inside its own area. Outside it (`d > homeRange`, `Foals.cs` 483), it
    follows instead.
  - So every wedge happens inside the area, and B would have meant **no wedge at all** for a baby that isn't lost.
  - That drops the wedge's working way out: the rejoin's trot to a real slot (456-468 above, 1.4 to 1.9 away). The trot
    can leave where the shuffle can't, because `FollowStep` turns without a line test (668-678).
  - A baby that walks face-first at a wall can't shuffle out: the shuffle's turn swings the root into the wall. Without
    the wedge it would stand there facing the wall for as long as its lead stands, which for a mount left in the world
    is for good. That was the "they stop following" complaint of 2026-10-26.
- **B2. A way out must go somewhere (recommended instead).**
  - **The rejoin skips a target at the baby's own spot** (within the arrival distance, 0.25), and takes the next
    reachable slot.
    - This filter is the rejoin's only. The trot's own target pick (`FollowTarget`) keeps the plain one.
    - The route rejoin runs only when its own slot is farther than that.
    - Why the near side lands on its own spot: `NearSideOfParent`'s 0.9 equals `ShuffleFollowGap`'s default 0.9. The
      guard goes by distance, so neither number needs to change. A comment there says so.
  - **The wedged catch-up in view first asks `FreeSlot` where it would go.** A spot within `MinApart` (0.5) of where
    the baby stands isn't a way out.
  - **No way out: it stands as at home.** It isn't lost or wedged, and its count is reset. With A, the next check
    comes after three fresh failed picks over 8 s at the earliest.
  - **Logged once until it moves.** Otherwise a left mount's baby in a spot with no room writes a line every 8 s all
    session.
  - The result: every wedge ends in a real trot, at most one jump, or standing still. The loop is gone, and the wall
    case keeps its way out.
- **C. `FreeSlot` tests the step.** A spot counts only where a straight step fits in one facing (its root and body
  lines), and the facing is set to the one that fits. Otherwise the tests stay as now.
  - It improves every placement (doors, calls, spawns, catch-ups).
  - But it changes where each one lands. The door and called-babies tests read those spots.
  - Not needed to end the loop. A follow-on, if the hand look shows a jump landing where no step fits.

**Recommended: A + B2.**

**Test (F11, with the fix):**
- A test parent stands where the step test refuses a baby's steps. Find the spot with the test itself, round the
  yard's door, where the log's wedges were.
- One baby at its slot, about 30 s.
- Count the wedges, rejoins and in-view catch-ups. Any one of them is a WARN.
- The step is written with the fix and run once. Without the fix it fails: the loop starts within seconds.

## 3. The stuck report on resting babies

### 3.1 What the logs show

- **Section 7 (18:21):** "#82 has not got nearer its parent #77 for 20 s (4.8 away) ... Level: asleep": babies 4 from
  their parent, far from the keeper.
- **`log-dragon.log` 5234-5239:** #61, #63 and #70 "Level: asleep ... no view (released)", 10 to 13 from their parent #1,
  and "the lead has its view".
  - It was during the garbage survey: the keeper was round (40, 16) and riding the test dragon.
  - #1 was left at (4.88, 6.00, 15.48) (line 617), and no ride or move of it is logged after that, only a wake (4381).
    That is on the far side of its babies from the keeper, 12 beyond them, so past 35 from the keeper and asleep too.
- **The same log, 5278 and 5285:** #62 and #64 "Level: idle (off screen, within 35); view switched off", 11 to 13 from
  #1.

### 3.2 Why it fires

- `WatchFollowing` (`FoalHops.cs` 216-259) runs once a second at every level, on purpose: its comment says "a frozen or
  sleeping baby doesn't shuffle".
- It leaves out only a baby near its lead, a lead that is moving, or a lead in another scene.
- But a baby that isn't active moves only in these ways:
  - **Idle:** never. `StandTick` returns before the shuffle (`MountInstance.cs` 170-173).
  - **Asleep:** only through `AsleepTick` (`Tiers.cs` 506-523). That needs a parent in its scene, not ridden, **with
    its view**, more than 3 away, that has stood 1.5 s.
  - **And a baby is idle or asleep only when its parent isn't active.** `WantTier` (`Tiers.cs` 224-262) keeps it active
    while its parent is active in its scene, or while it is with the keeper.
- So the idle ones, and the asleep ones whose parent has no view, are still by design. The report counts them as
  stuck.

### 3.3 The report misstates the lead

- It prints `the lead has its view` from `Lead.Here`.
- Since 2026-10-26, `Here` means "its view, **or its scene loaded**" (`Foals.cs` 138, the fix for babies waiting on a
  parent asleep in their own scene).
- So a parent asleep in the loaded yard reads "has its view", as in 5234-5239.
- The line can't tell "asleep by design" from "`AsleepTick` should have moved it and didn't". That is the one case the
  report must catch.

### 3.4 Options (the user's)

- **The fix (recommended):**
  - **Pause the report's clock** (as for a moving lead) while the baby is idle, or asleep with a parent that has no
    view. Those are the states no code moves it in. The test for "asleep with a parent that can move it" is taken out
    of `AsleepTick` into one shared check, so the two can't drift.
  - **Keep reporting** an asleep baby whose parent has its view and isn't ridden. `AsleepTick` should have caught it up
    within a second or two, so 20 s there is a real fault.
    - Its distance rule doesn't leave a gap: the report's "near" (the baby's area + 1.6, 3.0 at the least) is never
      under `AsleepTick`'s 3.
  - **Print the lead as it is:** the keeper, or the parent's level and whether it has its view (and whether its scene
    is loaded), instead of `Here`.
- **Not recommended: leave every baby that isn't active out.** It would hide the one real fault above.

**Test (F11):**
- A baby 5 from its parent, both asleep, 42 to 50 from the keeper ("Reused views" places them so): no report after 21 s.
- The same baby with its parent woken and standing: caught up as now, with no report.
- The report's text is read once (a test hook): the parent's level and view.

## 4. To decide

1. The loop: A + B2 (recommended), A alone, or A + B2 + C. (B withdrawn, section 2.5.)
2. The report: the fix above.

Both are API-only. Saddle Up and the packs don't change. Both go into the next API version, with Unreleased entries in
CHANGELOG.md when built.

The user (2026-11-06): "ok", then "A + B2". Both decided.

## 5. Built (2026-11-06)

**The loop (`Foals.cs`):**
- **A.** Both rejoins reset `pickFailsInRow`. A new wedge takes three fresh world-blocked picks over 8 s.
- **B2, the rejoin.** `PickReachableSlot(body, skipWithin)` leaves out a target within `skipWithin` of the body. The
  rejoin passes `ArriveDist` (0.25, the arrival distance, now one constant); the trot's `FollowTarget` passes 0. The
  route rejoin runs only when its own slot is at least `ArriveDist` away. `NearSideOfParent`'s comment records the two
  0.9s.
- **B2, the catch-up.** `LostCatchUp` serves the lost branch's two catch-ups (lost 10 s out of sight; its parent stands
  again). For a **wedged** baby it first asks `CatchUpSpot` (`FreeSlot`). A spot within `MinApart` of its body is no
  way out, so it calls `StandWithoutRoom`: not lost, not wedged, a fresh count, one line for the spot. A baby that
  isn't wedged is caught up as before.
  - Only for a wedged baby, because a wedge only ever happens inside its own area. A baby lost elsewhere and stood
    there would have followed again at once (`d > homeRange`) and been lost again, about once a second.
- **The lines.** A wedge where it was already said to have no room writes no line. The wedge's lines (the wedge, a
  rejoin after it, "no room") are at most six per baby, the last saying so. In a cramped spot it may trot between two
  slots every 8 s or more for as long as its lead stands.
- `CatchUp` is split into `CatchUpSpot` and `PlaceBy`; door, call and spawn placements are unchanged.
- Counters for the tests: `CatchUps`, `Rejoins`, `Wedges` and `StoodWithoutRoom` per baby.

**The report (`FoalHops.cs`, `Tiers.cs`):**
- `AsleepGoesTo(p)`: one test for `AsleepTick` and the report.
- `Resting(p)`: idle, or asleep with a parent it doesn't go to. The watch's clock is held then, as for a moving lead.
- The line comes from `StuckText`. The lead is "the keeper", or "is <level>, with its view / without a view (its scene
  loaded)". `StuckReportS` is a field, so the test can shorten it.

**Tests:** two new scenarios in Levels and views (docs/self-tests.md): `FoalRoomTests.NoRoom` and
`FoalRoomTests.ReportResting`.

**Checks (2026-11-06 03:51, after pinning the test baby's gap, lively steps, area and keeper distance to the shipped values, and running the placement bound to it):**
- built and deployed with the game closed, no warnings;
- NoLinqScan (11 assemblies), ApiSurface (unchanged, 708 lines), CecilScan, layering and the DepManifest verify all
  pass;
- not run: `release-scans.sh`, because it writes into `release/logs/`.

**Open:** the F11 (the two new scenarios and the foal ones), and a look by hand at the yard's door with a mount left
there.

**F11 of 2026-11-06 (ended 13:55): 2007 passed, 3 failed (the three below), 0 warnings.** Every [Warning] or [Error] line in the log comes from a self-test provoking it (test GUIDs, temp folders); none is from the foals.
- "the stuck report leaves out a baby still by design": all four checks pass.
  - The asleep baby, 5.0 from its asleep parent, isn't reported.
  - Its line says "the lead is asleep, without a view (its scene loaded)".
  - The penned baby is reported, active, 6.0 away.
- "a baby with no room": three FAIL (1 wedge, 1 rejoin, 0 catch-ups, moved 0.58). **The test's setup, not the fix.**
  - The catch-up put the baby on the slot beside its own: the shuffle's line has it at z 15.03, home 15.73. The world
    kept it off its own slot.
  - So its own slot, 0.7 away outside the walls, was a real place to go. The route rejoin took it there: a way out,
    as designed.
  - The test now tries up to four spots, takes one where a catch-up puts the baby on its own slot
    (`TestWhyNotOwnSlot`), and says in INFO why each other spot won't do. Deployed 13:55; that scenario to run again.

**F11 of 2026-11-06 14:32: 2006 passed, 0 failed, 0 warnings, 1 skipped.**
- The skip was "a baby with no room". At all four spots: "its own slot (...) has another donkey's body on it".
- **A bug, the user's "fix it":**
  - `Taken` read another donkey's body with `BodyAt(o.Position, o.riderFacingX)`. Its offset is measured only while
    the shuffle runs.
  - The test's parent doesn't shuffle, which is the game's default (`IdleShuffle` off). So its body was its root:
    (44.40, 15.60), 0.44 from the baby's own slot (43.98, 15.73). The real body is 0.9 from that slot.
  - So every catch-up (a door, a call, a spawn, the safe spot) put a baby behind a parent that doesn't shuffle one
    slot over. That was also the first run's "the world kept it off its own slot".
- **Fixed:** an instance without a measured offset is read at its view's body point (`BodyPoint`, as the lead's own
  Body). One that has it keeps `BodyAt` from its `Position`, so a sibling placed earlier in the same frame counts where
  it now is (the two babies on one slot at a door, 2026-10-03).
- Deployed 14:34. NoLinq, ApiSurface, Cecil and layering pass. The no-room scenario is to run again.

**F11 of 2026-11-06 23:40: 2009 passed, 0 failed, 0 warnings, none skipped.**
- With the `Taken` fix and the run's hold on the game's story (`Dev/GameQuiet.cs`).
- "a baby with no room" passes: 2 wedges, 0 rejoins, 0 catch-ups, stood without room twice, moved 0.00.
- The report test passes.
- The morgue's trigger was held ("held the game's trigger PlayerEnterGDZone 'GDZone_Intro_Graveyard_Morgue'"; 2 held
  in all). Player.log shows no quest change and no control taken by a story flow.
- Left: the look by hand at the yard's door.
