# A called mount's babies (investigation, 2026-11-05 evening)

The user, 2026-11-05, after the F11 of 18:21: "when i had al the mounts summoned inside, i went out with mount a and its
babies followed, then from ourside i spawned mount b but its babies stayed in the house; sae when i sapawned mount c
outside too, its babies remainiend inside". The same evening: "lets investigate this a bit more".

Status: **built 2026-11-05 evening (section 8): option A, mirroring the door, the safe spot too; whole F11 green 19:19 (1978 passed, 0 failed, 0 warnings).** The log is `BepInEx\LogOutput.log` of 18:21 onwards (line numbers below). The
code was read in `MountInstance.cs` (`ComeTo`), `Foals.cs`, `FoalsIndoors.cs`, `SafeSpots.cs`, `Patches.cs` and
`Api/MountCompanion.cs`.

## 1. What the log shows

The house and the yard are one scene (RuinedTemple). The inside is round (-96, 0, -80), and the yard by the door round
(4, 6, 18), about 140 to 145 apart.

The families: the donkey #1 (babies #61, #62), the dragon #74 (#75, #76), Gerry #77 (#78 to #82), the rat #83 (#84 to
#88), the snail #89 (#90 to #94), the worm #95 (two). All were called inside, so every other one was left inside with its
babies.

| Line | What happened | The babies |
|---|---|---|
| 9287, 9294 | Mount A, the donkey with the cart, ridden out through the door | "2 foal(s) came through the door with the mount he rides" |
| 9313 | Mount B, the dragon, picked outside: "left at (-103.68, 0.00, -80.42) ... is his again" | still inside |
| 9314 | the switch has him on foot for a moment, and the dragon's follow switches are back | #75 and #76 "follow the keeper (the keeper walks on foot)", from 145 away |
| 9328, 9337 | he is on the dragon | #75 "follows its parent #74 (145.5 away)": asleep at that distance, so it doesn't move |
| 9384, 9386 | Mount C, the rat, picked and ridden; the dragon is left outside and stands | |
| 9387 | | #75 "caught up with its parent #74 (lost for a while, out of sight)": the dragon's babies come out once the dragon stands |
| 9390 | | #88 (the rat's) "route to the keeper: none (no way around inside the area)" |
| to the end (10147) | he rides the rat | the rat's five babies shuffle inside, round (-96, -79) |

So B's babies did come out, but only after he left B standing (he may have seen them appear by it later). C's were
still inside at the end.

## 2. The cause

- **The mount comes alone.** `Instance.ComeTo` (`MountInstance.cs` 708), behind every call (`MountHandle.ComeToKeeper`),
  moves the mount to where a ride puts it, in one step. Its cart comes because the cart is part of it, but its babies are
  instances of their own and nothing moves them.
- **A baby waits for its parent to stand.** Far from its parent, a baby follows it, but it stops when the way is blocked
  (inside the house: "no way around inside the area"). It catches up, placed by its parent, only when:
  - the parent has stood 1.5 s and the baby is off screen;
  - or the parent has stood 6 s;
  - or the baby has been lost 10 s, is out of sight, and the lead isn't moving (`FollowParentTick`).
- **A ridden parent never stands.** Ridden, he counts as moving.
- **An asleep baby is stricter still** (beyond 35, no view): `AsleepTick` catches up only with a parent that is not
  ridden and has its view (docs/foals.md "Stopping for good").
- **So a called mount that is ridden at once leaves its babies wherever it stood**, until he gets off and it stands.
- **The default settings are what the player sees.** `ShuffleFollowAlways` ships on (`defaults.cfg`), so a baby 145 away
  "follows" and is stopped by the walls. With it off, the 10 of `ShuffleFollowMaxDistance` stops it as "too far to
  follow". It's the same wait either way.

**The precedent.** The same was reported on 2026-10-24 for doors: a mount chosen inside the house and ridden out left
its babies inside. `FoalsIndoors.ThroughDoor` was fixed then, and its comment says it exactly: "the house and the yard
are one scene, so it waited for its parent to stand still out of reach, and a ridden parent never does." Since then, the
babies of the mount he rides that stood within 10 of the door come through with it
(`FollowParentThroughDoor`: placed by the parent while the screen is black, again each frame until a slot is clear,
woken at once). A call is the same jump without a door.

**Why it shows now.** Before the mounts left in the world it was rare:
- Saddle Up's babies follow the keeper on foot (on by default), so they stand by him when he calls.
- The left mounts change that. A left mount's babies stay with it, their follow switches off (docs/leave-mounts.md
  "What a left mount follows"), so picking a left mount from afar always meets it.
- It was possible before, for the player's one mount, with "Babies follow you on foot" off, or with babies that didn't
  come indoors after him.

## 3. Every way a parent jumps, and its babies today

| The jump | Its babies today | |
|---|---|---|
| A door, riding | the ones within 10 of the door come through with it (2026-10-24) | fine |
| A door, on foot | the ones with him come along; indoors only with "Babies come indoors" | fine |
| A call: the journal's Ride or Drive, H, G; any mod's `ComeToKeeper` | stay where they were until the parent, unridden, stands (section 2) | **the bug** |
| Back to the last safe spot or the fall guard, riding (`pc.SetPosition`) | the ridden mount goes with him. The patch on `SetPosition` only resets the safe-spot clocks (`Patches.cs` 362); it isn't a door, so the babies stay at the old place | the same class, read in the code, never reported (the jump is mostly short) |
| A mod's `MoveTo` of an unridden mount | catch up once it stands and they're off screen | fine (it stands) |
| Flying far | trot after it, then catch up once it has landed and stands | fine (by design) |
| A load | each restored where it was saved | fine |

## 4. Options

**A. `ComeTo` brings its babies (recommended).**
- When a mount comes to the keeper, each of its babies farther than `ShuffleFollowMaxDistance` (10) from where it now
  stands, or in another scene, is placed by it.
- This reuses the door's own step, `FollowParentThroughDoor` ("came with its parent, called to the keeper"), not a bare
  `CatchUp`. The door's step clears `withKeeper` and the follow state first, which matters: the log's dragon babies had
  joined the keeper from 145 away (line 9314). It also retries each frame until a slot is clear while the keeper's
  controls are taken (the journal holds them, `TakenControlType.ByUI`, as a door's black screen does), and it wakes the
  baby so its view is there at once.
- Babies within 10 trot over as now.
- Seen by the player:
  - **from the journal:** the journal covers the screen;
  - **on H or G:** the mount itself appears by him in one step already, so its babies appear with it.
- It is the API's rule for every mod's `ComeToKeeper` ("a cart comes with its donkey", and its babies too).
- One place, the same step the door uses.

**B. A "parent jumped" check in `Foals`.** A baby whose parent moved more than a few units in one frame is placed by it.
- It covers every jump at once: the call, the safe spot, a mod's `MoveTo`.
- But it reaches into the follow states (the door already places its babies, so it must not place them twice), and it
  changes a mod's unridden `MoveTo`, which works now.
- Broader, and riskier for a fix this size.

**C. The companion only** (after its own `ComeToKeeper`). It misses H and G for the player's one mount with "Babies
follow you on foot" off, and every other mod.

**The safe spot** can take the same step after `pc.SetPosition` when he rides (the ridden mount's babies within reach
of where he was), as the door does. That's a line in `SafeSpots.Return`, for the user to take with A or leave.

## 5. To decide (the user)

**A called mount's babies when he is indoors and "Babies come indoors" is off.**
- The row says "The babies follow you into buildings and back out", which reads as on foot.
- Riding through a door, a ridden mount's babies come in with it whatever that row says (`ThroughDoor`'s first case).
- **Mirror the door (recommended):** they come with the mount he will ride, in or out. One rule: a mount's babies go
  where it is taken.
- **Honour the row:** indoors, with the row off, they stay outside. They join it when he rides out through the door, if
  they stand within 10 of that door; else they catch up once it stands outside.

## 6. Tests (F11, with the fix)

In "mounts left in the world", a new step:
- a mount left with its baby, both moved far away (more than 10, in the test's own area);
- the mount picked again and ridden: its baby is placed by it within a few frames, and stays with it while it is ridden;
- a baby within 10 isn't placed: it trots over;
- with H (the companion's `Use`) and the mount out but standing far away: the same.

The step is written with the fix and run once. Without the fix it can't pass: the baby stays where it stood.

## 7. Seen on the way, not part of this

Both investigated 2026-11-06: docs/foals-catch-up-loop.md (the causes, and the options for the user).

- **A catch-up loop.** #75 outside (lines from 9440 on) is placed by its parent with a clear line ("caught up ... its
  parent stands again", 9 times), then "can't follow ... no step fits where it stands" at the same spot, then caught up
  again, over and over. #62 inside, with the keeper, does the same between "can't follow the keeper ... no step fits"
  and "follows the keeper again" (36 times). `FreeSlot`'s test (a clear line from the parent) passes a spot that the step's own
  test (root, turn and body samples) refuses. In the log it is lines at the 4-a-second cap. In game it may be a baby
  standing still or twitching. To study on its own.
- **The stuck report fires on asleep babies.** "#82 has not got nearer its parent #77 for 20 s (4.8 away) ... Level:
  asleep" (9396, 9403): babies 4 from their parent, asleep by design (far from the keeper). It is an info line, but a
  report that should leave asleep ones out.

## 8. Built (2026-11-05 evening)

The user: "yes lets do it". That means option A, and in section 5, babies go with the mount in or out, mirroring the
door. The safe spot is in too.

- **`Instance.BringFarBabies(why)`** (`FoalsIndoors.cs`, by `FollowParentThroughDoor`):
  - every baby of the mount, not ridden, that is in another scene or farther than `ShuffleFollowMaxDistance` from it
    takes the door's step;
  - each baby is bound in turn (`ForEachInstance`);
  - returns how many came.
- **`Instance.ComeTo`** calls it last, once the mount stands where the ride puts it. Logged as "Mount: <id> came to the
  keeper with N baby(ies) from out of their reach, placed by it.", and each baby's "caught up with ... (came with its
  parent, called to the keeper)".
- **`SafeSpots.ReturnCheck`** calls it for the mount ridden, once the ride has followed him (two physics steps and a
  frame after `SetPosition`). Logged as "[safe-spot] N baby(ies) of <id> came back with it, placed by it."

**Tests (F11):**
- the new scenario "a called mount brings its babies" (`LeaveMountsTests.CalledBabies`): the journal and the ride key
  with a baby out of reach, read in the frame of the call; a baby within reach not moved, then trotting over;
- in "Back to the last safe spot", the ridden test donkey has a baby, put out of reach of the ride's spot before the
  return and read as soon as the arrival check has run.

Build and deploy at 18:44. NoLinq, Cecil, ApiSurface (no public change), layering and DepManifest: all pass.

**F11 (19:19): 1978 passed, 0 failed, 0 warnings.**
- The journal: the baby went from 14.6 to 0.7 from its mount in the call, and stayed by it, ridden.
- Within reach: a baby 6.4 off wasn't moved, and trotted over.
- The ride key: 16.4 to 0.6.
- The safe spot: 0.6.
- The log lines: "Mount: ... came to the keeper with 1 baby(ies) from out of their reach, placed by it." and "[safe-spot]
  1 baby(ies) of ... came back with it, placed by it."
- Still to see by hand: the user's own case (mounts left in the house, one picked in the yard: its babies by it at
  once).
