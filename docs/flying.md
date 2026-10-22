# Flying mounts (experiment)

Started 2026-10-22: the dragon lifts off, flies at a height, lands. The same height drives a jump. Dev-only: the
tuning window's **Flying** tab, session values, nothing saved; on by default where the dev tools are unlocked
(the keys did nothing with it off, the user 2026-10-22), never for players. A refused take-off or jump is logged.

## How the ride is drawn (what the lift builds on)

- The keeper's physics body is never moved by the mod. `Instance.Follow` (Mount.cs) places the mount's root at the
  keeper's body each frame (`donkey.Data.Position`, pixel-snapped); in Ride style its height is the walkable surface
  under the mount's body (docs/slopes.md). The cart's 3D parts follow the root's height (`FollowCartParts`).
- The rider is his sprite root (`main_character`) lifted by the seat (`ApplySeat`). AutoSeat and a skin's seat
  (`seat = 68` on the dragon) measure the mount's picture in world space, so they follow the mount wherever it is;
  the manual seat is a number and adds the lift itself.
- Visual up is world +Y; one art pixel up the screen is 0.0333 world y. The world is drawn pixel-snapped, so the
  lift is whole pixels (a fraction shimmers).
- Shadows are real Unity shadows: the mount's `Cylinder Shadowcaster` and `gfx (90 rotated)` are under its root and
  rise with it (the shadow lands on the ground off to the sun's side); the keeper's casters stay with his body.

## Step 1: the lift only (built 2026-10-22, not seen in game)

`Flight.cs`: `Flight.LiftFor(instance)` is added to the mount's height in `Follow` and to the manual seat; with
the experiment off it is 0 and nothing changes (a default player's donkey is untouched).

- Phases: Ground, Rising (to the take-off height), Airborne (climb/descend keys, the hover bob), Landing, Jumping
  (up at 4H/T, down under 8H/T^2: the top at H after half the time).
- Who flies: riding plain (never the cart) and the dragon, or any mount with "Any mount flies". The jump: every
  ridden mount, or only the flying ones.
- Down at once on a dismount, a style change, the experiment off, a cleanup. Safe in step 1: the keeper never left
  the walkable ground.
- Keys (session, cycled on the tab; right-click goes back): Space take off / land, R climb, F descend, J jump. Not
  read while the Mounts panel or a text box has the keys. **Unchecked: whether the game binds any of them** (the
  game reads its keys through its own input map; if one of them also does something, pick another on the tab).
- The camera rises with the height (seen 2026-10-22: without it the mount flew off the screen's middle): the
  game's camera-space offset on its follow camera (`CameraController.SetCameraSpaceOffset`, otherwise only the
  credits use it), world up times the camera's tilt (0.6), the hover left out; a tab slider sets how much (0-1);
  back to zero once landed. The first use logs whether the offset component was found.
- Jitter while climbing (the user's video 13-50-54, measured frame by frame): the mount and the ground moved
  together one screen pixel back and forth every 3 frames, i.e. the camera, not the mount. The offset is added
  after the game's pixel rounding of the camera (CinemachineCorrectPixelExtension); along the screen's up it put
  the camera between grid steps. Now the offset is world straight up by the lift, whole steps of the y grid
  (VisualConsts.Y_STEP 0.01666667; one art pixel = two steps). Seen in game: no change, the theory was wrong.
- Measured properly (video 14-05-15, the world's shift by phase correlation, the dragon's on-screen place per
  frame): the camera moved smoothly; the dragon flipped one step back and forth on the screen, from about 8 px up,
  while climbing or descending only. The mount was rounded as height + hover, the camera as the height alone:
  once the hover eased in, the two crossed their rounding at different frames. Now the height and the hover are
  rounded each on their own (the mount's lift = round(height) + round(hover)), so the mount and the camera step
  in the same frame. The per-frame `[flight-probe]` log lines stay until this is confirmed.
- Log: `[flight] ...` on every phase change.

What to look at in game: the take-off and landing speed; the hover; the dragon's shadow on the ground; the
rider's seat at height (dragon: the skin's seat; a donkey with "any mount": AutoSeat or the manual seat); the
depth against trees and buildings in front (the lift moves the picture up the screen, not towards the camera);
the front layer (the near wing over the rider) at height; a jump on the donkey.

Known look gaps: the dragon has no flying frames (folded wings, the walk cycle in the air);
`tools/skins/creatures/make_creatures.py` has an unused open-wing drawing (`wing(near, flap)`) that is the start
of a `bdy_fly_left_NN` set, which needs a frame swap of the mod's own while airborne.

## Step 2: free flight (built 2026-10-22, not seen in game)

The user, 2026-10-22: "when I'm flying, I'm getting stuck with things on the ground". Built as planned below
(Flight.cs, FlightGround.cs), through everything (fences, roofs, water): a "bump into obstacles" variant can come later.

- From the clear height (tab, 16 px) on the way up: `SetNonKinematicFlag(ByLadder, false)` (the one type the game
  never sets), the edge walls put down once. The body is then moved in `Plugin.FixedUpdate` (`Flight.FixedStep`):
  `rb.MovePosition` (not `MoveByPosition`, which teleports past the interpolation and sets Walk against
  `MoveByDirection`'s Idle), the input as `MoveByDirection` reads it (0.8 along z), at the walking top speed against
  the damping (speed x multiplier x (1 + c dt) / c), `playerData.position` set. Up the hills; over water, cliffs and
  roofs the body keeps its height. The walk animation and facing still come from the game's `MoveByDirection`.
- Down: below the clear height only where `Mount.Instance.WhyCannotLandAt` passes (the navmesh under him, his body
  fits there, the game's walk grid); otherwise the mount holds the clear height, "Nowhere to land here." and the
  reason on the tab and in the log. Landing: the body put down (`SetPosition`), the height grown by the drop so the
  picture keeps its place, the flag back, then the usual descent.
- Ways out: a save (`SaveSystem.Save` prefix) lands him now, under him or back at the last spot he could have landed
  on (checked once a second, the take-off spot first); a dismount or a style change through the API (H, G, the
  panel, a mod) becomes "Land first." and a landing; forced exits (`Dismount`, `ForceCleanup`) give the body back,
  moved to safe ground, except through a door or a scene change (the game placed him) and indoors (riding indoors
  allowed: given back where he is). A flow's `LockMovement(false)` is undone while free (postfix).
- `[flight]` lines on every change of who moves the body; `[flight-probe]` lines carry `free`, the body's y and the
  kinematic flag.

- Through everything was too much (the user, 2026-10-22: "now I'm going through everything ... is there no
  smarter way, regarding buildings and trees"). Now (tab: "Bump into what's taller than the flight", on): each
  physics step the body's capsule, from the ground under him up to the flight's height, is swept along the move
  against what the keeper collides with (`Mount.Instance.FlyMove`, FlightGround.cs). A hit stops the flight only when
  the thing reaches above the flight's feet (body + lift - a clearance): its collider's top or its picture's
  (the renderers beside the collider, for a tree's short trunk collider under a tall crown), whichever is higher;
  ground layers only by where the hit itself is (a cliff face). Blocked: the flight slides along it. So a low
  flight bumps into houses and trees, a high one passes over; fences, rocks and water always pass under.
  `[flight] bumps into <name> (layer, top: collider, picture) above the flight at <y>` says what stopped it.
  Unmeasured: whether the game's colliders and pictures give tops that match what the eye sees (the log is the check).

### How tall is a thing (read of the game code, 2026-10-22)

- The world's objects are real 3D meshes (`Wgo` -> `WgoPart` -> `Object3D` -> `Object3DMesh` with a `Renderer`),
  so a renderer's bounds are its drawn height. No definition (`WGODef`, `BuildingDef`) stores a height.
- Colliders come from the prefabs; layer 8 (obstacles) is only ever tested by layer in the game, never by height:
  their Y size says nothing reliable. A navmesh cut's height copies its box collider's.
- **The see-through system is the game's own "is something tall here"**: `PlayerView` casts the keeper as a 1.4 tall,
  0.24 wide capsule towards the camera against layer 15 (`TRANSPARENCY_OCCLUDER`, mask 32768);
  `TransparencyOccluderMeshCollider` puts on that layer a MeshCollider that *is* the drawn mesh, only on things tall
  enough to hide him (trees, houses, big props). No height numbers anywhere: a physics cast against the drawn shapes.
- Baked bounds: `WgoData.SerializedBounds` (all parts' renderers, inactive ones and lights' ranges included: a lamp
  reports too high); `Object3DMesh.treeHeight` (a shader value, 4 by default). Shadow-only meshes are `*_sh`/`-sh`,
  water surfaces `*-ws`.
- Nothing in the game flies around obstacles (wisps go straight, arrows pass through obstacles).

So the bumping (FlightGround.cs) now: sweep 1, the keeper's see-through capsule at the flight's height against
layer 15 (he meets a crown or a wall where it is drawn, passes over a roof he is above); sweep 2, what his body
collides with, from the ground up, counting only things whose top (their collider's or their Wgo's meshes', shadow
and water meshes left out) is above the flight's feet. The tab's survey button writes every collider around him
with these tops and a screenshot (BepInEx/GK2-flight-survey.csv/.png): the check that they match the eye.

### The first survey (2026-10-22, the Inquisition base plateau, 343 colliders within 12)

- Obstacles (layer 8): 198, nearly all the scene's own pieces (rock walls along the cliff, the ruins, the house; no
  Wgo), `DecomposedConvexMeshCollider`s. Their heights are fixed extrusions, not the drawing: 135 exactly 2.0 tall,
  17 at 3.0, 14 at 1.5. So "the collider's top" stopped the flight at every rock below 2 units (the bumps logged
  "top 8.00 / 9.00 above the flight at 7.62"). The drawn meshes sit beside them in the scene (`Walls/rock_.../
  rock_...(Clone)` renderers: tops 6.0 to 8.4 on a plateau at 6, i.e. 0 to 2.4 tall).
- See-through shapes (layer 15): 16, the trees' crowns (min 8.8 to max 10.6 over ground 6: the crown only, the
  trunk below has none) and a teleport milestone.
- Now: an obstacle counts by what is drawn over it (its Wgo's meshes, else the nearest parent's renderers that
  overlap it from above), never by its collider's top; nothing drawn: it doesn't stop. Survey files are now
  numbered by the time (`GK2-flight-survey-HHmmss.csv/.png`), the column `drawn_top` is what the bumping reads.

- Pushed up at cliffs (the user's video 15-15-26): `[flight-probe]` body y 6.12 -> 8.41 in one physics step,
  flying free at 109 px, the mount 9.67 -> 12.08. The body followed `GroundHeight`, whose navmesh lies on rock tops
  and the plateau above a cliff. Now it follows only the ground layers' raycast from half a unit above it (a
  slope, not a cliff); a higher plateau is crossed at the body's height and a landing puts the body on it (the
  height adjusted both ways so the picture keeps its place). Seen in the same log, not fixed yet: the first two
  frames after a landing onto a lower level the mount's height flickers (6.73, 7.98, 6.70).
- The per-frame probe is now a tab switch, off by default (one session wrote 70 000 lines, an 18 MB log).

- Take-off and landing as a dragon's (the user, 2026-10-22: "like a slow elevator"; they were constant-speed
  moves). Take-off: a crouch (3 px down, 0.09 s), then a spring launch towards the height (stiffness from the rise
  time 0.45 s, damping from the overshoot asked) with 3 wing-beat kicks (30 px/s, every 0.16 s): simulated for 48 px,
  it passes the height at 0.36 s, peaks about 20 % over and settles in about a second. Landing: a dive speeding up
  (700 px/s^2 to 200 px/s), a flare from 18 px braking to 40 px/s, a 2 px thud: 0.44 s from 48 px. Every value on
  the tab. The climb and descend keys are unchanged (steady speed while held).

Hand tests: cross water and come back; fly over a fence and a building; off a cliff and land below; try to land over
water (holds, says why); save in the air; H in the air (lands first); a door while flying; then F11 (nothing else
changed: all of it only while flying free).

## Step 2: the investigation

The keeper's `PlayerPhysicalBody` (decomp) keeps him on the ground:

- `MoveByDirection` adds a velocity change projected on the ground plane; `UpdatePhysics` finds the ground from
  contacts or a down raycast on layers 11/12 (mask 6144) and falls with `gravityFallScale` without it.
- `PlayerInvisibleWalls.UpdateEdgeWallsState` raises eight edge walls wherever a sphere cast finds no walkable
  ground: what keeps him out of water and off ledges. It is skipped while not on the ground.
- So switching the walls off is not enough: over water there is no ground, he falls (into the water). The path that
  fits flying is the kinematic one the game already has: `SetNonKinematicFlag(..., false)` + `MoveByPosition`
  (the unused `LadderPlayerState` does this), the mod moving him from the input itself and ignoring colliders.
- Open questions before building it: does `FreePlayerState` keep calling `MoveByDirection` on a kinematic body;
  how the walk animation (`charState`) gets set; what the camera, the interaction trigger, enemies and scene
  zones do with a keeper over water.
- Read of the game code (2026-10-22), the answers to the questions above:
  - Kinematic: `SetNonKinematicFlag(type, false)` (four types: ByMovementComponent, ByBuilding,
    ByFlowPlayerControl, ByLadder; all must be true for a dynamic body). `FreePlayerState` keeps calling
    `MoveByDirection` on a kinematic body: no force (ignored), but it still writes Walk/Idle and the facing every
    fixed step, from the input. `FixedUpdate` skips the edge walls, physics and `playerData.position` while
    kinematic; walls already up stay up (`ChangeWallsVisibility(false)` once). `UpdateWorldZone` still runs.
  - `MoveByPosition` (kinematic only) is the game's own mover: Walk state, facing, `rb.position`,
    `UpdatePlayerData`. Used by `PlayerLocalAreaMovement`. Landing: `PhysicalBody.SetPosition` or
    `PlayerController.SetPosition` (also snaps the camera and the wisp), then the flag back.
  - Traps: `LockMovement` (Flow_LockPlayerMovement) writes `isKinematic` directly, past the flags: a flow unlock
    mid-flight makes the body dynamic (it would fall). `ResetSimulationState` (menu, session reset) forces dynamic.
  - Nothing drowns, respawns or kills a player off the ground: only the edge walls keep him out of water (layer 4,
    fishing and looks only). Obstacles are layer 8, walkable ground 11/12, the edge walls 14.
  - Over water: zones may be left (harmless), enemies path towards an unreachable point (no crash found), a
    flow-driven walk started there fails ("Can not calculate path"). No stuck check, no fall death, no autosave
    position rule found.
  - So the plan: airborne = our own flag false, the edge walls hidden, each frame `MoveByPosition(rb.position +
    input * speed * dt)` (the walk animation and facing come with it), obstacles: none (flying over fences and
    roofs) or layer 8 only (decide in game). Landing only where `SafeGround` says the keeper can stand; every way
    out lands first; a patch on `LockMovement` keeps the body kinematic while airborne.
- Lock-outs are the top risk (docs/safe-ground.md): every way out (dismount, a door, a scene change, a save, the
  mod's cleanup) must land on ground checked with `SafeGround` (`WhyKeeperCannotReach`) and give the body back
  its physics; a save mid-flight over water must never happen.
