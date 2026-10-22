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

## Step 2: really flying over things (investigated, not built)

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
