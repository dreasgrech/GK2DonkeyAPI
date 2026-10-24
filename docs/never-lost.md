# Never lost: safe spots, the fall watchdog, back to the last safe spot

Built 2026-10-24 (API 0.36.0, unreleased). The user: "just in case something happens and we do fall off the map, that
we reset back. and also have some reset back to last place button in our options ... this is critical and very
important ... and we need to have tests for it too." It came out of the jump work (docs/flying.md): a keeper under the
ground is catastrophic, and the no-underground guard on every flight and jump put-down is the first line; this is the
net under everything.

## What the game does (nothing)

Read from the game's code 2026-10-24 (ilspy and Cecil; docs/flying.md 1183-1217 had found the same):

- No kill plane, no respawn, no "last valid position", no unstuck, no check on load. The only respawn-like thing is the
  fight arena's spawn point (`FightingGameController.SetPlayerDefault`), used at the end of a fight.
- Off the ground the keeper falls for ever: `PlayerPhysicalBody.UpdatePhysics` adds gravity (`gravityFallScale` 0.3)
  while not on the ground and not jumping; nothing stops the fall. The edge walls (`PlayerInvisibleWalls`: eight walls,
  each up where a 0.6 ray down from his feet finds no ground on layers 11/12) only hold him while `isOnGround`; off
  the ground they are dropped.
- A door places him raw: `PlayerController.Teleport` -> `SetPosAndUnFadeAsync` -> `PlayerController.SetPosition`
  -> `PlayerPhysicalBody.SetPosition`, which sets `rb.position`, `transform.position` and `playerData.position` and
  nothing else: no ground check at the destination, no snap. A missing wgo gives (0, 0, 0).
- The save stores `playerData.position` as is, and the load puts him there as is (`PrepareForGame`). The game saves
  only on waking (`EnergySystem.StopSleeping`), so a bad spot can't be saved by normal play; the home bed has a
  hard-coded push-out (`SaveCodeFixes_Helper.PushPlayerOutOfBed`), nothing else.
- `IsControlsEnabled` is false through every door (ByTeleport), scene load, cutscene (ByCinematics, ByFlow), dialogue
  (ByFlow), sleep, death, fishing, build mode, ladder and UI window: the one flag that says "the game has him".
- Standing under a bridge is legal: the NPCs' walkable mesh runs under the broken bridge's deck (6.0 under a deck at
  6.5). So "ground over him" alone is never a fall.

## The design (`src/GK2DonkeyAPI/SafeSpots.cs`)

**The record.** Every 0.5 s, while the game is ready, the controls are his, not paused, his body not kinematic, the
flight not holding it, no door for 2 s and no placement for 1 s, and the game's own `isOnGround` says so
(`PlayerPhysicalBody.isOnGround`, a private field read by name; without it the walkable surface within 0.15 under his
feet stands in): the spot is verified (`Verify`: the walkable surface within 0.15 under the feet, nothing of the
ground's layers over him within his height (`WhyUnderground`), his body fitting (`KeeperFits`)) and kept in a ring of
eight per scene (`currentGameSceneId`), newest first; a spot within 0.5 of the newest only refreshes it. Seeds: the
first spot of a scene, and the destination of each door 2 s after `OnPlayerTeleported`, both verified first; a seed is
the last to be dropped from a full ring. A new game clears the rings (`BecomeReady`). Session-only: the game saves
only in bed, a bad spot is never saved by play, and the load position is a seed at once.

**A placement by anyone** (`PlayerController.SetPosition`, patched with a postfix: a door, a script, the mod,
`PutKeeper`) resets the clocks: the watchdog judges again after 0.3 s, the record waits 1 s (a bad place is never a
safe spot).

**The watchdog** (`FallGuard`, on), every 0.1 s under the same gate (without `isOnGround`), each rule held for a moment:

| Rule | Condition | Held |
|---|---|---|
| Underground | ground over him within 5 (he falls while judged) **and** no walkable surface within 0.3 under his feet | 0.5 s |
| Falling into nothing | not on the ground, going down faster than 2 a second, no walkable surface within 30 under him | 1.2 s |
| Below the world | 5 under this scene's lowest safe spot and no surface within 2 under him | 0.3 s |

A drop onto the ground from above never fires (the ground is within 30); under a bridge never fires (ground at his
feet); a slope, a jump, a ride never fire (he is on the ground, or the flight has the body and its own guards). Fired:
`Return`, then 3 s quiet.

**Return** (`SafeSpots.Return`; `Mounts.ReturnToSafeSpot`, the key, Saddle Up's button): refused, with the reason,
while not in a game, while the controls are taken ("Not now: a door, a scene or a cutscene has the keeper."), and in a
scene with no spot yet. Else the newest spot of this scene that still passes `Verify` (newest to oldest, the seed last):
a flight or a jump is ended first (`Flight.Release`), a ride goes on (the mount follows the keeper each tick),
`PlayerController.SetPosition` (the body, its data and the camera at once), the velocity cleared, "Back on solid
ground." in the bubble, `ReturnedToSafeSpot` raised, and a **warning** in the log with every fact:
`[safe-spot] back to (x, y, z) from (x, y, z) (D away, H in height): <reason>; the spot stood on / the game's own N s
ago, number k of n`. Two physics steps later the spot is checked again (`ReturnCheck`); failed, the next older one.
A return is always a warning: it must not happen, and when it does the line says why.

**Keys and switches:** the Behaviour keys `FallGuard` (bool, on; the watchdog only: the record and the return always
work) and `ReturnKey` (`KeyboardShortcut`, None), read by the API with the flight's gate (panel closed, no text box,
not capturing a key, controls free, not paused, a rebound key waits for its release). Saddle Up: Behaviour "Bring me
back if I fall out of the world" (the toggle), "Back to the last safe spot" (a button: `MountSettingsPage.Button`, new
in 0.36.0, the word "Go" on it; it runs in the game's Settings window and in the Mods menu in a game) and the Info row
"Last safe spot"; Keys "Back to the last safe spot" (None).

**Cost:** two or three raycasts every 0.1 s, an overlap box every 0.5 s (`Perf.S.SafeSpots`); no grid rescan.

## Tests (F11, group "Never lost", `Dev/SelfTest.Safety.cs`; docs/self-tests.md)

- safe spots: a spot after the load; standing still records this one; a unit under the ground (the watchdog held off):
  nothing recorded, nothing put back, the last spot still the one on the ground.
- the fall watchdog: a unit under the ground: back once within 3 s, on the ground, nothing over him, at the spot;
  fifty under: back once; dropped from 25 up: lands by himself, left alone; standing under something (a bridge) for
  2 s: left alone (INFO when there is none within 14).
- the return: on foot from 10 away by the API; riding: back, still riding, the mount at the keeper; refused with the
  controls taken (ByUI) and in a scene with no spot (the ring taken out); Saddle Up's toggle drives `FallGuard`, the
  button brings him back, the key row sets `ReturnKey`; the watchdog off: under the ground stays, the return by hand
  works.
- the jump test: the watchdog never fires through its jumps, the deck, the slope.

## The first run (F11 2026-10-24, 915 passed, 0 failed)

- A body placed a unit under the terrain is shot **downward** by the physics' depenetration: 16.7 units in 0.8 s
  (3.6 to -13.1). So the rule that fires for "under the ground" is mostly "below the world" or "falling into nothing",
  not "underground"; the underground rule stands for a body that is held inside something. Caught in 0.8 s.
- Fifty under: caught, but after 2.8 s: the 3 s quiet after the previous return held the watchdog off. The quiet is
  1 s now (the arrival check is the guard against a bad spot).
- A drop from 25 up: landed by himself in 1.2 s, left alone. The return on foot, riding (the mount 0.4 behind), the
  refusals, Saddle Up's toggle, button and key row, the watchdog off: all as designed. The bridge case needs the run
  by the broken bridge (INFO otherwise).
- The scene's lowest safe spot read 0.0 in the yard's scene (its spots at 3.6): a spot was stood on at y 0 earlier in
  the run (the indoors tests' buildings), so "below the world" fires 5 under the lowest level ever stood on in the
  scene; the falling rule covers the rest.

## Reading a `[safe-spot]` warning

It means he really was out of the world, or something placed him badly. The line has: where he was and where he is
put (and the height difference: under the ground is a few units, below the world tens), the rule, the spot's age and
whether it was stood on or the game's own, which of the ring it was and why newer ones were skipped. With the flight
on, the jump probe's trace (`BepInEx/GK2-jumps/`) has the frames before it. A return that itself failed on arrival is
a second line ("failed on arrival"); nowhere to go is "there is nowhere to put him back" (a scene with no spot yet:
the fall goes on and is the game's).
