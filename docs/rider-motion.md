# Rider motion: why the rider looks stiff

Investigated 2026-10-03, after the user's report that the rider "still feels a bit stiff" on the donkey. Mock-ups are
in `C:\ClaudeCodeTemp\GK2-rider-mockups\` (made from the game's own sprites). The user liked them but found them "a
bit exaggerated"; built the same day (API 0.21.0) with smaller defaults, see "Built" at the end.

## Measured

From the atlas dump (`BepInEx\GK2-sprites`) and the game's animation clips, read from the Addressables bundles with
UnityPy (Unity 6000.3.9f1; the keeper's clips via `MainCharacterController`, bundle `157300bc…`).

**The riding pose (`fishing_idle_left/right`, 0.97 s):**
- It swaps sprites only: body and arm, 4 frames each, at 0.12 / 0.24 / 0.60 / 0.72 s.
- Head, hair and beard keep one static sprite each, and **no body part's position is keyed**. The other keyed
  positions are the rod and the fishing line's parts.
- Between frames the body outline doesn't change: 11 to 23 pixels of shading change (breathing).
- The arms move at most 1 px.
- While moving it plays at PoseSpeed 1 × SpeedMultiplier 2 = 2x.

**The keeper walking, for comparison (`walk_left`, 0.73 s, 8 frames):**
- Body: 215 to 290 px change per frame. Arms: 92 to 207 px (a real swing).
- Head, hair and beard are moved by the clip: up 1 px on frames 1–2 and 5–6 (`gfx/hed`, `gfx/hrs`, `gfx/brd`
  localPosition y 0.0027 ↔ 0.036).

**The donkey's back:**
- 23 / 24 / 25 / 24 per walk frame (atlas median over the mid columns). This matches `BackHeightPx` (24 / 25 / 26):
  1 px either way, twice per stride.
- Riding plays the donkey's walk at 1.5 × 2 = 3x. One stride is about 0.32 s, so the bob runs at about 6 Hz.

**So today:**
- The rider moves 1 px either way, exactly with the back, at about 6 Hz. That reads as jitter more than as a bob.
- Nothing on him moves on its own: body, head and arms are one rigid piece.

## Keeper layers

`PlayerView2D/main_character/gfx/`: `bdy`, `arm`, `hed`, `eys`, `brd`, `hrs`, `leg`, `bdy_over`, `bdy_ovr_hor`, `tol/…`.
They are siblings, so each can be offset on its own.

The riding clip keys none of their positions. But the walk clip keys `hed`/`hrs`/`brd`, and so may other states that
show while riding. An offset must therefore be added on top of whatever the animator wrote that frame, in the keeper's
`OnLateUpdate` hook (after the skin swap), and never accumulate.

## Mock-up variants (`rider-variants.gif`, `rider-variants-slow-x8.gif`)

- **A, today:** rigid, ±1 px with the back.
- **B:** the body bobs ±2 px, one donkey frame late, so the weight trails the step.
- **C:** B, plus the arm layer cancels half the bob (steadier hands) and moves 1 px forward and back with each stride.
- **D:** C, plus head, hair and beard follow the body one more frame late.

The mock-ups leave out the near leg (`RiderLeg`), and their placement is by eye, not the game's exact seat.

## Built (2026-10-03, API 0.21.0, `RiderMotion.cs`)

Five style keys, live on the dev panel ("Rider torso" with Bob on, and "Rider hands"). The mock-ups' values are the
upper end of the defaults, since the user found them a bit exaggerated:

| Key | Ride | Cart | Mock-up D |
|---|---|---|---|
| `BobScale` | 1.5 | 1 | 2 |
| `BobLagFrames` | 1 | 0 | 1 |
| `HandsSteady` | 0.5 | 0 | 0.5 |
| `HandsSwayPx` | 0.5 | 0 | 1 |
| `HeadLagFrames` | 1 | 0 | 1 |
| `HeadSettle` | 0.3 | 0 | (1) |

How it works:
- **The trailing bob is a table lookup, with no history.** The donkey's walk is a fixed 8-frame cycle. The frame
  number comes from the body sprite's name, cached per sprite like `BackHeightPx`, and "a frame late" is the back's
  table at that number minus the lag. A stop (a frame outside the walk) ends every offset at once.
- **The body:** the seat (`TickSeat`, and `ComputeAutoSeat` too) uses `RiderBobPx` in place of the raw back height.
- **The arm and head layers** (`gfx/arm`; `gfx/hed`, `hrs`, `brd`, `eys`) are offset in the keeper's `OnLateUpdate`,
  after the seat. The offset goes on top of what the animator wrote that frame: when the position is still the one
  written last, the old offset comes off first. `RestoreSeat` takes the offsets off again, unless the animator wrote
  since.
- **Units:** 1 art px is 0.0333 local y and 0.02 local x. The layers sit under the facing mirror, so local -x is
  forward for both facings.
- **Fractional pixels** (1.5, 0.5) are allowed. Whether the game draws a half-pixel shift isn't verified: if 0.5 px
  hand sway shows nothing, use whole pixels.

## The bobble head (2026-10-03, the user's video `Graveyard Keeper 2 - 2026-10-03 10-19-47 PM.mp4`)

Measured by tracking the head, torso and donkey templates frame by frame (60 fps, about 6 screen px per art px),
before the turn:
- **The torso** moved 10 screen px at a step (about 1.6 art px, `BobScale` 1.5).
- **The head** moved 11 px the other way at the same frame: about 3.5 art px against the torso.

The cause is the head being late at all. The back's table cycles in 4 walk frames, so the head (two frames late)
and the body (one frame late) step in opposite directions at every change. The mock-up had the same, at twice the
amount.

Added `HeadSettle` (0.3): the share of that late movement the head shows, so at most about 0.5 art px against the
body. Each donkey frame lasted 4 video frames (about 0.067 s), slower than the 0.04 s assumed for the mock-ups.

The game draws fractional pixels: 1.5 art px showed as 9 to 10 screen px, so it doesn't snap to whole pixels.

## In flight (2026-11-07, built; to see in game)

The user: flying the dragon, the keeper "is doing the normal up/down rocking motion which is used for when the mounts
are on the ground but it doesnt make sense when flying since the dragon will feel more stable". The gap was noted in
docs/flying.md ("the rider's bob and hands still follow the game's walk frames") and never fixed.

**The cause, from the code (ours and the game's, build 26404424):**
- All of the motion above reads the game's own donkey body sprite (`CurrentWalkFrame`, by its name). A skin is an
  overlay: the game's sprite stays under it, hidden, as the game animates it (Looks.cs). The dragon's flying
  pictures are put on the overlay by the flight (`Flight.FrameFor`); the game's sprite under them plays on.
- The game's `FreePlayerState` calls `PlayerPhysicalBody.MoveByDirection(LazyInput.GetDirection())` every update.
  With a direction held it sets `charState` to Walk; with none it sets Idle. In free flight the body is kinematic and
  moved by the flight (`Flight.FixedStep`), so the game's push does nothing, but the Walk state stands.
- The ridden mount mirrors `charState` (Mount.cs, `SetStateToAnimator`), so the hidden donkey walks under the
  flying pictures whenever a direction is held. The rider then bobs ±1.5 px (`BobScale`), sways his hands
  (`HandsSwayPx` 0.5) and lags his head (`HeadSettle` 0.3) as on the ground. With the dragon's seat row the bob is
  added to the seat (`TickSeat`, fixed row) and to the legs (`OnKeeperLateUpdate`).
- Hovering with no direction, or climbing on the climb key alone, the state is Idle and the bob stops; every
  press and release of a direction starts or stops it at once.
- The self-tests never saw it: `Flight.TestMoveDir` feeds only the flight's own step, never the game's
  `MoveByDirection`, so no test flight had the keeper in Walk.

**Ruled out:**
- The skin's sub-pixel trim: `Follow` moves the overlay by the fraction of the game frame's trim offset, and the
  donkey's walk frames' offsets are whole pixels (the sprite dump: 17-19, 29), so the dragon's picture doesn't move
  with the walk under it.
- The keeper's own pose: the SetState patch keeps him on the riding pose (`fishing_idle`, which keys no position) in
  Walk and Idle, so the walk clip's head keys never play.
- The flying pictures: the seat is a fixed row (62) and no lift is drawn into a frame (docs/flying.md).

**The order of the two LateUpdates.** The seat is worked out in the keeper's `AnimationComponent.LateUpdate`
(`OnLateUpdate`), from the skin's seat row on the mount's picture. The mount is placed, with the flight's lift and
beat, in the plugin's LateUpdate (`donkey.Data.Position`, which moves the view at once: `Wgo.HandleChangedPos`).
Unity doesn't fix which of the two runs first; seated first, the rider would follow the mount a frame late.

**Measured** (the `[rider]` readout, RiderReadout.cs, the Flying tab, off by default; the user's run 2026-11-07 22:08:
walk, take-off, flying along, hover, climb, landing; 2871 frames, 1917 in the air):
- The order: the mount was placed before the seat in all 2871 frames. Not a cause.
- In the air the rider sits exactly where the seat puts him: his hip against the seat row less the walk bob stayed
  within 0.06 px every frame, through the take-off's climb of 4-6 px a frame. So the walk bob is the whole movement.
- The game's sprite under the flying pictures walks: `bdy_takeoff`, `bdy_fly`, `bdy_flyup1/2` shown over
  `0301_bdy_walk_left_01..08`, the keeper in Walk. 418 air frames had a walk bob of ±1.5 px, the rider's hip that
  far above or below the seat row (gap +1.50 on walk frame 8, 0 on 1, -1.50 on 2: 3 px top to bottom, every half
  stride). With no direction held (the hover) the keeper was Idle and the bob 0.
- On the ground, walking, his hip strayed up to 0.6 px from the seat row less the bob (the 2 s summaries; the
  per-frame lines had run out on the standing start). Standing it held within 0.01. Not the flight's; not measured
  further.

**Built** (the user's yes, 2026-11-07: "make sure that from the api and from our f11 debug panel, these settings can be
set PER mount"):
- `CurrentWalkFrame()` (RiderMotion.cs) is 0 while the mount shows a picture of its skin's own for the flight
  (`Flight.ShowsOwnFrame`: `Flight.FrameFor` names one and the skin has it, the same test `Looks.Rig.ApplyPart` draws
  by): take-off, beat, climb and dive, glide, landing, the 0.18 s fold after touching down, and a jump on its wings.
  Every motion reads it (the bob in the seat, the legs and the head; the hands' sway), so the rider sits still on
  the mount and rises and sinks with it (the beat's rise, `Flight.BeatBobPx`, lifts both). A skin without its own
  seat row hangs the legs from the game's back line: on those pictures they hang from its resting height (25 px).
- `BobInFlight` (a ride/cart tuning key, false): true keeps the walk motion there, as before. A mount without
  flying pictures walks in the air and its rider moves with its walk, whatever the key.
- Per mount: every rider motion key (`Bob`, `BobScale`, `BobLagFrames`, `HeadLagFrames`, `HeadSettle`,
  `HandsSteady`, `HandsSwayPx`, `BobInFlight`) is the mount's to set, as `ApplyTuning` takes "the rider's motion and
  pose" from a mount: `mount.cfg` `[Tuning.Ride]`, `MountDef.TuningRide` in code. Never a skin's (`SkinTuningKeys`
  are the drawing's geometry). The F11 panel: the Riding tab has the player's value; the Mounts tab a "Rider motion on
  this mount" section with all eight (yellow when the mount sets one, right-click or x to unset; saved into its
  `mount.cfg`).
- Test: "rider: still on the dragon's flying pictures ..." (integration tests, group Flying; docs/self-tests.md).
- To see in game: the `[rider]` readout again (the Flying tab), the same flight. In the air the lines should read
  `walk N bob +0.00` with the keeper in Walk, and the summaries "the walk bob on in 0"; on the ground the bob as before.

