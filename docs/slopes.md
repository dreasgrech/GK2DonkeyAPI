# The donkey on slopes

Investigated 2026-10-04, after the cart fix on the Inquisition Base hill (open-items 9l). The user: "riding the donkey
by itself suffers from the same problem". Nothing built yet. Mock-ups: `C:\ClaudeCodeTemp\GK2-slope-mockups\`
(`slope-rider-ingame*.gif` / `.png`, drawn the way the game draws them).

## Measured (`[rideground]` probe lines, Debug tab)

Riding without the cart down and up that hill (the hill drops 0.45 per unit along the road):

| | Middle of the body | Front hooves | Back hooves |
|---|---|---|---|
| Going down | +0.03 to +0.06 | about +0.14 (floating) | about −0.06 (inside) |
| Going up | about 0 | about −0.11 (inside) | about +0.11 (floating) |

The hooves are 23 px apart in the static frame (columns 41–64) and up to 30 px in the walk. The probe sampled ±0.4,
so its front/back numbers are scaled here to ±0.25.

- **The middle** is off by the keeper's height over the ground (+0.03 to +0.05 going down). Ride style takes the
  donkey's height from the keeper plus the ground difference to the body point (`SnapToGround`), so his physics body
  hovering over the slope carries through. The cart avoids this since the 9l fix: its height comes from the ground
  surface directly.
- **The ends** come from the donkey being an upright, flat picture whose hooves stand on one straight line. Moving it
  up or down only trades floating at one end for sinking at the other. The legs are only about 0.2 tall.

## How the game draws (decomp 1.008)

- **Projection:** `VisualConsts`: one art pixel is 0.02 world x, 0.0333 world y (`Y_SCALE` 1.667) or 0.025 world z.
  The camera angle is 0.6435 rad (36.87° from vertical). The hill's 0.45 per unit shows as about 0.27 px per px on
  screen (about 15°).
- **Pixel-perfect:** the world is drawn into a point-filtered render texture of screen size ÷ pixel size
  (`MainCamera.ReCreateRenderTexture`), then scaled up. `WorldToScreenPoint` gives 1.00 per art pixel. Every offset
  therefore lands on whole art pixels, and a slant would be a pixel staircase. (The fractional rider-motion values
  are rounded the same way.)
- **No slope handling for sprites anywhere:** only the keeper's shadow caster tilts with the ground normal
  (`PlayerPhysicalBody.UpdateShadowcaster`). The game's own donkey never takes this hill.
- **The donkey's animator** writes only: the root's scale (the facing mirror), `gfx/bdy` sprite, `gfx/hed` position
  and sprite, `gfx/light_sway`, the cart wheel sprites and the cart sway nodes. It never writes the rotation or scale
  of `gfx` or of the donkey's own sprites.

## Rider placement, measured in game

`[rideground] screen:` lines, 41 of them, both facings, standing and walking:
- the keeper frame's pivot is 9 art px towards the tail and 6.1 px up from the donkey frame's pivot (4.6 to 7.6 with
  the bob);
- both frames are 96×96 with the pivot in the middle;
- the keeper is drawn nearer the camera, in front of the donkey;
- the user's cfg has seat 0.22 and torso shift 0.06 (3 px).

## Options

- **A, small:** the donkey takes its height from the ground surface directly (as the cart now does). That fixes the
  middle; the ends stay.
- **B, the donkey slanted, rider upright** (mock-up B): the body follows the slope, the legs stay vertical, and all
  four hooves are on the ground. The rider is carried by the slant at his place.
- **C, donkey and rider slanted** (mock-up C).
- **Not taken: a rotated donkey.** It leans the legs and jags the outline; it looked worse in the mock-up.

**How a slant would be done:** Unity can't slant one transform. It needs rotation · non-uniform scale · rotation over
two levels. Doing that on `gfx` would bend the cart's lamp rig and wheel sprites, which hang under it. Reparenting
`bdy` would break the animator, which binds by path. The clean way: draw copies of the body and head inside a slanted
group of our own, hide the originals, and copy the frame, colours and facing each frame (the animator keeps driving the
hidden originals). Plan: an in-game test toggle with a fixed slant first, then the full version.

## Tried and removed: the slanted donkey (2026-10-04)

B and C were built (setting `SlopeSlant`, API 0.22.0) and tried in game on the hill. The user's verdict: "the skewing
kinda kills both solutions". The slant was the right amount: the `[rideground]` lines show −0.455, the hill's own
slope, and in the user's screenshot the hooves lie at about 0.31 px per px, right for this hill (0.27). So the look is
what slanting this art gives, not a bug. Removed completely (`Slant.cs`, `RideSlant.cs`, the setting and its enum; the
API is back to 0.21.0).

What was learned, for any later attempt:
- An exact slant needs no reparenting. Use rotation · scale on the group and a counter-rotation on its children (the
  closed-form 2×2 decomposition M = R(φ)·diag(sx, sy)·R(θ)).
- Apply it for drawing only: put on in `Canvas.willRenderCanvases`, taken off at the start of the next frame.
- At the art's resolution the slanted outline is a staircase, and the head and ears lean with it.

## Built instead: A (2026-10-04)

The ridden donkey (Ride style, `SnapToGround`) stands on the walkable surface under its body (`HighestHitUnder`, up to
0.6 above the keeper's level), as the game snaps a walking object. Before, it took the keeper's height plus a ground
difference, which carried his body's hover over the slope (+0.03 to +0.05 going down).
- The two surfaces' difference is still held to the step limit (0.5).
- Failing that, or with no surface, the pair difference applies as before.
- The rider follows through `groundDelta`, now the surface minus the keeper's height.

The ends of the upright picture still float or sink by about ±0.11 on this hill, like any character in the game.

**Foals and standing donkeys** never had the keeper's hover: they stand at `StandHeight` under their own body. But
that took the navmesh height and only rose to a surface above it. Where the navmesh lies a little above the ground (up
to 0.07 at the foot of the hill: navmesh 3.709 against surface 3.635), they floated by that much. Now the highest
walkable surface from 0.6 above the navmesh height down to 0.15 below it wins. Lower than that is another level (a lake
bed, the bank under a bridge), where the navmesh height stands. This also covers parked carts, cart shafts at rest, and
every `SnapToGround` placement.
