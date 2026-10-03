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

