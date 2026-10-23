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
- Keys (session, cycled on the tab; right-click goes back): Space take off / land, Left Shift climb, Left Ctrl descend (R and F until 2026-10-23), J jump. Not
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

- Through the ground on a raised place (the user's video 15-24-40, a spot the keeper never reaches; the log:
  "can't land here: no ground under him (none)", held at the clear height). The body crosses a higher plateau at
  its own height, and the clear height was measured from the body: the hold sat under the raised ground, and the
  ground test, a raycast from 1 above the body, started under its surface. Now: the floor is the clear height above
  the ground under the flight (the ground layers' raycast from the mount's feet down); a landing is judged at that
  ground (the walkable mesh within 0.6 of it, else "at another level"), and must be on the same connected part of
  the walkable mesh as the take-off spot (`WhyNotConnected`: no rock tops, no ledges he can't walk off). Flying low
  over a rise only keeps above it; only a landing or the descend key asks to come down. "Nowhere to land here." is
  said once per landing attempt.

- Still shifting up on the slope after the body fix (video 15-31-34; too dark to measure from the picture, the
  per-frame lines were off). The likely cause found in the code: `Follow`'s ride ground snap still ran while flying
  free, placing the mount from the ground under it against the body's height (the step limit's last good
  difference near a slope), meaningless with the body in the air. Now skipped while free (the mount is the body plus
  the lift). Measuring added in case it isn't that: `[flight] the floor lifts the flight N px` and `[flight] the
  body steps up N`.

- Video 15-32-47 and a screenshot: (1) "Nowhere to land here." showed at the keeper's body on the ground, not
  at the rider: the game puts the bubble at `PlayerView.BubblePoint`; it now rises with the flight. (2) After a
  refusal the landing went on, held at the floor, and took the first spot that passed (the log: refused "his body
  doesn't fit ... inside DecomposedConvexMeshCollider by 0.09", then "landing at (31.09, 3.60, 25.24)" two units on):
  a refused landing is now called off, the dragon pulls up to its flying height and waits for the next Land (the
  descend key held stays at the floor). (3) Hovering over a cliff the dragon was inside the rim's rocks: the floor
  counted the ground only. Now a sphere cast down from the mount's feet also finds the see-through shapes and the
  drawn tops of the obstacles under it: never inside them, and the clear height above them when coming down.

### Open: through the ground on the high rock table (studying, nothing built for it yet)

Video 15-38-25: a flat-topped rock pillar the keeper can't reach. Hovering over its top and landing, the dragon sinks
into it and is gone. The log there: "ground under it NaN" (the ground layers' raycast down from the mount's feet,
30 units, finds nothing), the body at 6.00 (the ground around the pillar), and every landing "no ground under him
(none)". So the table's top is drawn but has no ground collider under it, at least none on the layers the flight
reads: the floor falls back to the clear height above the body, deep inside the drawn rock.

What the game's code says (read 2026-10-22):

- The drawn ground is the game's `LazyTerrain`: per terrain a list of `LazyTerrainMeshData` chunks (a baked `Mesh`,
  `localPosition`, `center`, `bounds` with no height), shown through pooled `LazyTerrainMesh` objects while their
  chunk is in view. These meshes are the only copy of the drawn ground's shape; there is no height grid, heightmap or
  "sample height" anywhere in the game.
- The game strips the meshes' CPU data: `LazyTerrainMesh.DrawFromData` calls
  `LazyTerrainMeshCollection.Runtime_StripCpuMeshData` (`UploadMeshData(markNoLongerReadable: true)`) before showing
  one, and `ConsolesManager.LoadLazyTerrainMeshCollectionAsync` strips the whole collection at its deferred config
  load. After that `mesh.vertices` can't be read; `mesh.bounds` still can.
- Ground colliders (`LazyTerrainCollidersGroup` / `LazyTerrainCollider`, layer 11) are baked in the scene by an
  editor tool, never generated at runtime and never streamed: they exist only where the keeper is meant to walk. So
  an unreachable table top has no ground collider by design, not one that is unloaded.
- The cliff walls (`Walls/rock_...(Clone)`) are pooled props (`BakedChunkableObjectComponentData`): spawned, with
  their obstacle colliders, only while their chunk is near the camera's view.
- Everything in the game that needs ground height (dropped items, the player, arrows, decals) raycasts layers 11/12
  (mask 6144), so none of it knows about drawn-only ground either.

So the facts so far point at one real source for "the drawn ground under the flight": the terrain meshes, read
before the game strips them (a Harmony prefix on `Runtime_StripCpuMeshData` that keeps a height grid of each chunk's
near-flat triangles), or `mesh.bounds` per chunk as a coarse upper bound. The survey now lists every `LazyTerrain`
(path, height), the chunks within 5 of the keeper (their meshes: readable or not, vertex count, bounds) and, when
readable, their vertex heights, to check this in game before choosing.

### The map: every height at once (built 2026-10-22, not run yet)

The user: "isn't it possible to survey the entire map and zones? check what we can learn from the assets". Two ways:

- In game, the Flying tab's **"Survey the whole map (loaded scenes)"** (`Dev/MapSurvey.cs`): one press writes
  `BepInEx/GK2-map-survey-HHmmss/` with every `LazyTerrain` chunk (world place, mesh bounds, readable or not), the
  highest near-flat triangle per 0.5 cell of every chunk whose mesh can still be read, every baked prop of the
  scenes (`GameScene.bakedChunkableObjectComponentDatas`: spawned or not, with bounds), every world object with its
  baked bounds, every ground, water, see-through and zone collider, and the scenes and zones. Then
  `python -I tools/flying/map_survey.py <folder>` draws `map-heights.png` (the drawn ground's top, walkable ground
  outlined, water, the 20 tallest props), `map-walkable.png` (green: drawn ground with walkable ground at its height;
  red: drawn ground with none, where a flight can't land) and `report.txt` (the levels, the tallest props and objects).
  Covers the loaded scenes only.
- From the game's files: the scenes ship as Addressables bundles (StreamingAssets/aa, 25 269 bundles, 891 MB, hashed
  names; catalog.bin). UnityPy 1.25.4 (in the user's Python, run without `-I`: `python -E -P`) reads them; the
  meshes there are whole (the stripping happens at runtime), so the terrain of every scene could be read offline,
  visited or not. Indexing the bundles (`bundle-index.tsv` in the job's tmp) to find the scene and terrain ones.

What the game's files say (UnityPy, 2026-10-22):

- The catalog (`aa/catalog.bin`) names 16 scenes: `MainScene`, `RuinedTemple`, `NorthRuinedTemple`, `Prison`,
  `PalaceSewer`, `IntroScoutBuilding`, `IntroScoutSewer` and the `Cinematic_*` ones. The bundles' index (all 25 269
  read, none failed) found no bundle holding a scene's files under the expected names; the scenes' objects are not
  located yet.
- `LazyTerrainMeshCollection` is one bundle (`9b7dfbb11c0a...`, 2.7 MB): 4 209 meshes, all named `LazyTerrainMesh`.
  **Every one is flat** (all 624 726 vertices at y 0): 4 163 chunks of 5 x 5 tiles (4.8 x 6.0, a tile 0.96 x 1.2,
  150 vertices: a quad of 6 per tile) and 46 single tiles. A tile's drawing is packed into its third and fourth uv
  channels (`TEXCOORD2/3`: tile x, y, texture, as `LazyTerrainMath.PackShaderValue`); 31 442 of the 104 121 tiles
  hold all zeros there, i.e. nothing is drawn: a chunk's square is filled, its drawn shape is its non-zero tiles.
- So **the drawn ground is a stack of flat levels**: each `LazyTerrain` (one per level, it seems) is a flat plane at
  its own height, drawn where its chunks' tiles hold data; the sides between levels are the rock wall props. The
  drawn ground's height at x, z = the highest terrain with a drawn tile there. That is exact, cheap to look up (a
  bit per tile), and it covers the table tops and ledges no collider covers.
- At runtime the uv data is gone once the game strips a mesh, so it would be read just before
  (`Runtime_StripCpuMeshData` prefix: 25 bits per mesh, about 13 KB for all of them) or, if the collection is still
  readable on PC when the mod looks (the survey's `readable` column says), read then.

What the surveys say (2026-10-22 17:26, hovering over the table at 17.31, 29.40; and the whole map):

- The whole world is one loaded scene: 66 `LazyTerrain`s, 13 654 chunks (sharing the 4 209 meshes: identical
  chunks reuse one), 4 361 baked props, 1 001 world objects. The interiors are terrains far off (z -160 to -820).
- The levels are terrains named `[n] Level...` at y = 1.2 n: `[3] Level/[3] Level_3` at 3.6, `[5] Level` 6.0,
  `[7] Level` 8.4, `[8] Level` 9.6, `[10] Level VillageHill 02 (...)` 12.0, and so on; the drawn tops found are
  0, 1.2, 2.4, 3.6, 4.8, 6.0, 7.2, 8.4, 9.6, 10.8, 12.0.
- Over the table the chunks of six levels overlap (1.2, 3.6, 6.0, 8.4, 9.6, 10.8): every chunk is a full 4.8 x 6.0
  square, so the bounds can't tell which level is drawn there; only the tiles' data can. The highest is 10.8, the
  table's top most likely (4.8 over the plateau at 6.0), not yet proven.
- No collider at all under the table's top (the column: only the keeper's own colliders and the camera's sound zone),
  and the flight's reads there: ground NaN, floor NaN.
- Every chunk near the keeper was already unreadable (shown, so stripped); 490 of the 13 654 were still readable:
  the ones never shown yet. So on PC the game strips a mesh when it first shows it (`LazyTerrainMesh.DrawFromData`),
  not all at load: a prefix on `Runtime_StripCpuMeshData` sees every mesh while it can still be read.
- A shown chunk is placed under its terrain at `localPosition` (`DrawFromData`: `SetParent(terrain)`,
  `localPosition = data.localPosition`): a tile's world place = terrain position + chunk local position + the
  tile's vertices (-2.4..2.4, -3..3).

The solution this points to (to be agreed before building):

1. **A drawn-ground map**: a Harmony prefix on `LazyTerrainMeshCollection.Runtime_StripCpuMeshData(Mesh)` reads,
   while the mesh is still readable, each of its 25 tiles' data (uv channels 2 and 3) into a 25-bit mask per mesh;
   meshes never shown are read on demand. `DrawnGroundAt(x, z, below)`: the highest terrain with a drawn tile under
   x, z below a height (a per-terrain chunk grid built once per scene).
2. **The flight uses it**: the floor is the clear height above the highest of the walkable ground, the drawn ground
   and the drawn things under the flight; moving at a height below the drawn ground ahead (a table's side, a level
   above) is blocked like a wall and slides; landing still needs walkable ground (the table stays refused, with the
   dragon above its top, not in it).
3. **Checked in game first**: the tile masks against the walkable colliders where both exist (they must agree), and
   the table's top level from its mask.

### Step 1 of the drawn ground: built 2026-10-22 (the map and its checks; the flight doesn't use it yet)

`DrawnGround.cs`: a prefix on `LazyTerrainMeshCollection.Runtime_StripCpuMeshData` reads a terrain mesh's tiles just
before the game frees them (uv channels 2 and 3 non-zero = drawn; a bit per tile, the grid from the mesh's bounds,
0.96 x 1.2), and on demand while a mesh is still readable. A per-scene index (every active `LazyTerrain`'s chunks in
6 x 6 buckets, rebuilt when the terrains change, checked every 3 s) answers `HighestAt(x, z, below)` and
`LevelsAt(x, z)`. Dev tools only: for a player the prefix returns at once.

The checks, on the Flying tab ("The drawn ground"): the levels under the keeper, live ("9.6 drawn, 8.4 -, ..."), and
"Check the drawn ground (radius 30)": rays down on a 0.75 grid; where one meets walkable ground (not water), the
drawn level there must be at its height within 0.3. The log line `[drawn-ground] check:` counts agree / disagree /
unknown (a chunk freed before the mod read it) / nothing drawn, with examples of disagreeing points. The map survey
now draws freed chunks from the read tiles too (`tiles_read` column), so its map is exact where the keeper has been.

Pass before the flight uses it: the check agrees nearly everywhere (ramps and stairs between levels may disagree),
the unknowns are few after a fresh start (the prefix sees every mesh from the first time it is shown), and over the
rock table the readout shows its top level drawn (10.8 expected).

Found on the way: the `LockMovement` patch (keep a flying body kinematic through a movement unlock) was never in
`Plugin.PatchClasses`, so it never applied; it is now. The tuning keys moved off PageUp/PageDown and Home/End
(the Grave Zoom mod's zoom keys) to keypad 8/2 and 4/6; the flight's key choices leave those and the keypad out.

First results (map surveys 17:42-17:43, at the smallest zoom; 4 238 meshes' tiles read as the game freed them,
13 543 of the 13 654 chunks drawn from them, 104 still readable, about 7 unknown):

- The landing map (green: drawn ground with walkable ground at its height; red: drawn only) is coherent: the
  plateaus, the river, the town; red along cliff edges and on decoration.
- Placement: against the 1 505 small flat terrain colliders (their boxes' centres, a coarse test) the drawn level
  agrees at 90.5 % with no offset, and no offset does better than 91.7 %: the tiles sit where they should. Every
  disagreement has the drawn level *above* the collider (by 1.2, 2.4 or 4.8): box centres off the collider's mesh,
  or an upper level's edge tiles (rim art) over the ground below. The in-game check (rays, not boxes) is the real test.
- **The rock table**: its top is level 10.8, a block of tiles about x 14.3..21.2, z 25.3..33.8, ringed by the rim
  rocks' own ground colliders at 10.8 (`rock_l_blue-wall-corn_out-...(Clone)/Ground`, spawned only near the camera).
  Its middle has no collider; the 6.0 level's ground collider and navmesh run on **under** it. That explains an
  earlier landing: "landing at (18.71, 6.00, 28.79)" put the keeper inside the table, on the hidden 6.0 ground.
  So step 2 must also refuse a landing where a higher level is drawn over the spot.

**The in-game checks (2026-10-22, step 1 passed):**

- Two checks of 30 around the keeper (4 308 and 4 336 rays meeting walkable ground): the drawn level under the
  walkable surface is at its height at **96.3 % and 97.1 %**, **0 unknown** (4 219 to 4 225 meshes' tiles read, every
  one as the game freed it, none failed). The disagreements are all walkable things *above* the drawn ground: stairs
  and raised platforms (Ground 7.2 over drawn 6.0), big drops' top surfaces (6.4), a tower's collider (6.5); "nothing
  drawn" (61, 14) the same kind. So where the keeper walks, the drawn ground is right.
- Over the rock table the levels under the keeper: **10.80 drawn** (`[10]  Level VillageHill 02 (5)`), 9.60 not,
  8.40 drawn, 6.00 drawn, lower ones not: its top is 10.8, as the map said.
- Not measured by the check: drawn ground *over* walkable ground (the table over the hidden 6.0 ground); the rays
  ask only the levels at or under the walkable surface.

### Step 2 of the drawn ground: built 2026-10-22 (not seen in game yet; a tab switch, on)

- **The cover check** (tab: "Cover check (radius 30)"): rays down on a 0.5 grid onto walkable ground; where a higher
  level is drawn over the surface (more than 0.6 above), how many tiles in from that level's edge (0 = the edge
  tile). `[drawn-ground] cover check:` with examples, deep ones (2+ tiles in) first. It measures the two things
  the rules below assume: the hidden ground under a table, and how far a rim's edge tiles hang over a cliff's foot.
- **Floor**: the clear height above the highest of the walkable ground, the drawn ground (`HighestAt` at or under
  the mount's feet) and the drawn things under the flight.
- **Walls**: in `FixedStep`, a move whose body edge (0.25 ahead) lies over a drawn level above the flight's feet is
  blocked, sliding along x or z when one is free; not when the keeper is already under such a level (let out).
  `[flight] the drawn ground ahead stops the flight: level ... at ..., the flight's feet at ...`.
- **Landing**: `WhyCannotLandAt` refuses a spot with a higher level drawn over it, more than `CoverEdgeTiles` (1)
  tiles in from that level's edge: "a higher level is drawn over it (...): inside a rock or under a ledge".
  A level's outermost tile over a cliff's foot is allowed; the number is on the tab until the cover check says.
- "The flight uses the drawn ground (step 2)" on the tab turns all three off (walkable ground and objects only).

### The frames: built 2026-10-22 (first pass, not seen in game)

The user's reference: `Downloads/2dsprites/flying-dragon` (ZaPaper's Red Dragon rework, CC BY 3.0: side view, three
wing poses per direction). Our own drawing in `tools/skins/creatures/make_creatures.py` (`dragon(..., fly=, legs_mode=)`):

- `spread_wing(near, t)`: a bat wing from the side, the arm (shoulder, elbow, wrist) as the leading edge, three fingers
  from the wrist, the membrane back to a root before the hips, scalloped between the tips; three keyed poses (t 0
  raised over the back, 0.5 level back, 1 swept under the belly), joints interpolated; inside the 128 canvas. The far
  wing up and a little forward, darker, behind the body. The near wing in front, except over the rider's place
  (x 43-66, y 16-52): there behind him, so the upstroke doesn't hide him.
- Flying: the neck stretched forward, the head level; the forelegs bent under the chest, the hind legs trailing; the
  tail streaming back nearly level. No lift drawn into a frame: the seat is a fixed row (68), the rider would come off.
- 14 frames (+ front layers): `bdy_fly_left_01..06` (the beat: 0, 0.3, 0.6, 1.0, 0.65, 0.3), `bdy_takeoff_left_01..04`
  (folded crouch, hind legs pushing with the wings high, the first downstroke), `bdy_land_left_01..04` (wings high,
  legs reaching, the touch with the wings level, folded). The walking and idle frames are unchanged (byte for byte).
  Review sheet: `tools/skins/creatures/out/dragon/flying.png` (3x, the rider on the seat).
- Played by `Flight.FrameFor(instance)` through `Looks.Rig.ApplyPart` (the body part only; its front layer from
  `front/<key>`): the crouch and the launch's first 0.24 s on the take-off frames, then the beat (`WingBeatS` 0.6 s
  for the six); landing, the beat until the flare, `land_01`/`_02` through it, `land_03` on the thud, `land_04` for
  0.18 s after. A skin without the frames shows its own as before. Tab: "Flying frames", "One wing beat".
- The skin preview page (`tools/skins/skin_preview_html.py`) has the clips `fly` (100 ms a frame), `takeoff`, `land`
  and `flight` (take-off, three beats, landing, as the game plays them) when a skin has the frames.
- The scans accept `bdy_(fly|glide|glidein|glideout[1-4]|takeoff|land)_left_NN` (tests/SkinsScan `FlightKey`,
  tools/skins/check_skin.py; the glide's frames since 2026-10-23, "The glide").
- Second pass (the user: "the body is completely stiff, look at the reference again"; compared side by side,
  `compare3.png` in the job's tmp): the body moves with the beat, the saddle staying put: the legs hang well under
  the body, bent, the paws with their claws down (the flat walking foot read as standing in the air), the forelegs
  reaching with the wings raised and drawn up on the downstroke, the hind legs trailing back then pulled forward under
  the belly; the tail droops from the hips and swings down and in on the downstroke; the back humps behind the saddle
  on the downstroke; the head a little higher with the wings raised, lower with them down; the chest leads up with the
  wings raised. And in code (`Flight.BeatBobPx`, 3 px, the tab): the whole dragon and the rider rise through each
  downstroke and sink on the upstroke, in place of the hover, since a frame can't move the seat.
- Third pass, the legs (the user, 2026-10-23: walking "a slide or a crawl", flying feet "like a hoof"; "don't rush,
  learn from the reference"): the reference's legs are as long as its body is deep, thick and muscled, a big thigh,
  a shin and a long foot up to the hock, a strong forearm, every paw a red pad with separate yellow claws. Ours were
  6 px sticks under the folded wing's hem with flat bar feet. Now: tapered, shaded limbs (`bone`), two-bone IK
  (`ik`; the elbow bends back, the knee forward, the hind foot rising to the hock), a walk that plants each foot,
  slides it back under the body and swings it forward on a 4 px arc (stance 62 %, the four feet a quarter apart),
  and hand-placed paws (`PAWS`: "ground" toes forward with the gold claw tips on the ground, "hang" claws down,
  "trail" the hind paw flying, claws back; procedural claws read as a broom). The body stands 6 px higher on the
  longer legs (`RAISE`; the user chose it on the preview page from a "body as now" / "6 px higher" pair, the
  preview's new `--study <folder>`), so the seat row moved 68 -> 62 (skin.cfg; the self-test reads the skin's row).
  To check in game: the rider on the seat, the cart's harness and hitch gap at the new height.
  The walk first went backwards (the user): the planted foot slid towards the head; now it slides back towards the
  tail while it carries and swings forward to the head (`step_at`; the dragon faces left).
- Known for the next pass: the rider's bob and hands still follow the game's walk frames (they read the game's sprite, RiderMotion).
- **The rider behind the spread wing (2026-10-23).** The user: the occlusion on the ground "looks great (and took us
  a long time to get right) ... can we get that working during the flying animation too".
  - The cause: the folded wing is all front layer and simply doesn't reach his head. The spread wings sent every near
    wing pixel inside a box round the rider (x 43-66, y 16-52) to the back layer, so the rider was always in front.
    That was the 2026-10-17 compromise: with the whole wing in front, the raised wing hid him completely.
  - Measured (rider composited at seat 62): the folded wing hides 75% of him, everything but his head and face (down
    to canvas row 36) and his front, reins arm (rows 37-52, left of a line from x 67 to 62). The whole spread wing in
    front would hide 55% at the top of the beat, his head included.
  - First pass: `rider_in_view(xc, yc)` (`make_creatures.py`) was that visible part, up to a pixel more, in canvas
    pixels, and the spread near wing was front layer everywhere except there.
  - The user's video (2026-10-23 06-09-32, 120 fps, one beat studied frame by frame): "there are frames which the
    occlusion is missing". The window was a band down to row 37 across his whole width, so as the wing swept across
    at chin height (video frames #188-#196 and #224-#228; our fly_03 and fly_07) his shoulders and upper back stayed
    in front of it. That made a straight cut across his body at row 37. On the ground the fold never reaches that
    high, so "what the fold leaves in view" was the wrong rule.
  - Now the window is his head only. Read off the rider at 12x: the hair spans rows 27-37 across x 66-81; the face
    and beard x 65-75 down to about row 42; the jacket's back x 74-82 from row 38; the reins hands out front at x
    54-62. So the window is x 64-82 up to row 37 (to the picture's top: no rider there, and no stray edge over his
    hair, which the skin check had flagged as a lone pixel), plus x 64-75 for rows 38-42. The wing's thumb claw obeys
    it too. The near wing is nearer the camera than he is, so it covers him everywhere else it crosses him, as the
    fold does on the ground.
  - Checked per frame against the whole wing in front (`DRAGON_WING_OVER_RIDER=all`, which shows where the wing
    crosses him): in all 14 spread-wing frames, every pixel of him the wing crosses outside his head is covered, and
    his head is never covered. 10-28% of him is hidden per flying frame. The folded take-off and landing frames match
    the ground.
  - `DRAGON_WING_OVER_RIDER` keeps the old box (`box`) and the whole wing in front (`all`) as studies. (Since
    2026-10-23 `all` is the default, chosen in game as B; `head`, this window, is the study: docs/front-layer.md section 9.)
  - **Still broken in game** (the user's video 2026-10-23 08-07-28, facing right: "how did u confirm it from the
    frames if its still broken?"). The per-frame check only proved the frames follow the rule. It ran on a composite:
    a still capture of the rider (`rider.png`) centred on the picture by assumption, with the front picture exactly
    on the body picture. Neither assumption had been checked against the game.
  - Measured on the video: the frames were registered on the screen (8 screen px an art px; the body picture by its
    red pixels, the front picture by its wing pixels, separately). In every frame, the front picture sits 3.1 art px
    lower than the body picture (registration score 0.99). Cause: `Follow` lifts the front picture towards the
    camera along the part's own -z, and the camera looks down at an angle, so that lift also moves it down on the
    screen. The comment said "along the view axis, so nothing moves on screen"; the code didn't do that. With the
    two halves of the near wing 3 px apart, steps showed where they meet, and the hole round his head sat 3 px low.
  - Fixed in flight: the lift goes along the camera's line of sight (`Follow(..., alongView)`, the same distance, at
    least as near the camera). On the ground it is unchanged until measured. The fold there is all front layer and
    was judged good, and the ground screenshot was too small to register (score 0.43).
  - Not known yet: where his head goes in game. The rider's head and bob motion move him, and a video can't separate
    his hair from the dragon's dark pixels. `Looks.FrontReadout` logs it (`[occlusion]` lines, dev only): while
    flying, a few times a second, the frame, the facing, the front picture's offset from the body picture in art px,
    and the box each of the rider's sprites covers in the picture's own pixels (mirrored back when facing right). On
    the ground, one line with the front picture's offset there. The head window is to be set from those boxes.
  - The readout's run (2026-10-23, 82 lines; the video 08-27-56 at 4 screen px an art px) settled three things:
    - In flight the front picture is now on the body picture: +0.0 x, +0.0 y, in every line.
    - On the ground it sits 2.7 art px low, in both facings. The ground look that was judged good includes that.
      Aligning the ground later means drawing the fold 3 px lower so the look stays.
    - The rider's head sprite is `hed_static_left` through the whole flight (his body and arm cycle through the
      game's `fishing_idle_left_01..04`). His picture's x is constant and its y moves 2.7-5.0, so his head only bobs
      about 2 px and never moves sideways.
  - The readout can't size the window: every rider sprite is a full 96 px square, so its box is the square, not his
    pixels. The square's x also differs between facings (55 facing left, 25 facing right, mirrored back), so the
    square's position says nothing about where his head pixels are inside it.
  - The video, cropped round the rider at plain zoom, both facings: the steps and notches where the two halves of
    the wing meet are gone. His head, face and beard stay in front in every raised-wing frame. The wing covers his
    back and shoulders, and crosses his chest below the beard. The straight edge at the back of his hair in a raised
    frame (#124) is the hair's own: from the front of his face to the back of his hair is about 15 art px, the same as
    in a wing-down frame (#164). So the head window (x 64-82) holds in both facings.
  - The readout's budget now restarts on each state and facing, so one flight each way logs both.
- **The spread wing drawn as the folded one (2026-10-23).** The user: "the veins aren't as plentiful or the same as
  when on the ground". Measured on the front pictures:
  - **The fold:** veins (801A24) 26% of its pixels, one wine tone (4A1242), a dark border (260828) inside its edge,
    1 px finger lines fanning from the peak.
  - **The spread wing:** veins 9-18%, a lighter purple (702260) the fold never uses, no inner border, and three
    thicker fingers with a light centre (AC282E).
  - **Now `spread_wing` draws the fold's way:**
    - the membrane first, in the fold's tones (its light rule, kept below the lighter wine), darker by the veins;
    - the dark border on its edge;
    - 1 px red veins over the membrane only, fanning from the wrist: every finger, and as many between as the
      trailing edge's length takes, about 9.5 px apart, as the fold's finger tips meet its hem;
    - the arm, the leading edge, a thicker bone in the same red.
  - **Measured after:** veins 17-31% per frame (the raised frames 17-20%: a big wing, with its veins spread over it),
    no lighter purple, no light bone centre. The folded frames are byte for byte as before. On the preview page next
    to "Red Dragon (before: the spread wing's old veins)".
  - **Second pass.** The user: "not exactly. When idle, they look very detailed and structured, but less so when
    flying... it will look like slop otherwise". Compared whole (both layers) at 8x, the fold against the raised and
    down poses, three structural differences remained:
    - **Half the raised wing had no veins.** The fan only reached the trailing edge; the triangle between the arm and
      the back was one flat colour. The fold's fan covers its whole membrane down to the shoulder. Now veins also end
      along the wing's line on the back (root to shoulder), as far apart.
    - **Only three big scallops.** The fold's hem is scalloped between every pair of vein ends. Now every vein end on
      the trailing edge is a scallop point.
    - **No pleats.** The fold's dark bands beside its veins fall in its lower third (its light runs from the peak to
      the hem). The spread wing measured its light against the longest vein, so only the farthest tip had them. Now
      each pixel is measured along its own nearest vein, so every vein gets the dark band in its outer third.
    - Also: the veins start 1.5 px out from the wrist (all meeting in one pixel made a red knot there).
    - The folded frames are byte for byte unchanged; the head-cover counts for A, B and C are unchanged.
- **The far wing joined to the back (2026-10-23).** The user: the far wing sits higher on the body's side than the
  near one, so a gap shows between it and the body, as if detached.
  - **The cause:** the far wing was the near one moved 3 px forward and 7 px up (4 when raised), its shoulder and its
    root on the back too. In the level and down frames (3, 4, 6) its arm floated above the back with ground under it.
  - **Now** the offset grows from the body out:
    | point | share of the offset |
    |---|---|
    | shoulder, root on the back | none |
    | elbow | 0.35 |
    | wrist | 0.7 |
    | finger tips | all |
    The far wing joins the far side of the spine, the same line of the back from this side, and only its outer parts
    rise as the camera looks down on them.
  - Checked at 5x over the eight beat frames, before and after. The folded frames are unchanged and nothing reaches
    the picture's top row.
  - Open: the tail's tip runs 2 px past the picture's right edge in every flying, take-off and landing frame. The
    flying tail streams back further than the walking one. Shorten it by 2 px.
- **The raised wing was cut off at the top** (the same video). With the body 6 px higher on its longer legs, the
  raised wings ran 8-11 px past the 128 picture's top. Measured by drawing the frames on a picture 32 px taller: the
  back layer reached rows -8 to -11 in flying 1 and 2, take-off 2 and landing 2, mostly the far wing, which sits 7 px
  above the near one. Fixed: the raised pose is 18% lower about the shoulder, and the far wing is 4 px above the near
  one while raised (7 level and down). Every frame now ends at least 2 px inside the picture, outline included, and
  no picture uses its top row.

### The beat in time (2026-10-23)

The user: "every part of the body is moving literally synced at the same time ... almost like the dragon is dancing".
They gave a second reference, `Downloads/2dsprites/Dragon - Fully Animated`. It is a 60 fps rig with Idle, Walking,
two attacks, Hurt and Death, 140-300 frames each. It has **no flying clip**. The first reference was studied again.

**The cause.** Every moving part of a flying frame was a function of one number, the wings' pose `gb`:
- the neck and head, nodding 10 px;
- the chest, the hump and the haunch;
- the legs;
- every point of the tail.

Inside the wing, `pose_at(key, t)` gave the elbow, the wrist, the three finger tips and the root the same `t`, so the
wing swung as a plank.

Measured on frames drawn without the wings (`DRAGON_STUDY=nowings`; world height = the frame plus the game's bob),
every part was highest at either 0.00 or 0.50 of the beat:
- at 0.00: head, neck, tail tip;
- at 0.50: hump, tail middle, fore paws.

The head swung 7 px and the fore paws 19.

**The reference, measured.** The top of each part per frame, with its phase against the back (`tmp/ref2/track.py`):

| Idle (161 frames) | swing px | lag behind the back |
|---|---|---|
| back | 3 | 0 |
| neck, head top | 12, 7 | 0.58 (rises as the back sinks) |
| head forward/back | 5 | 0.29 |
| tail middle | 7 | 0.76 |
| tail tip | 4 | 0.58 |

In Walking, the feet lag the back by about a third of a loop, and the tail's middle and tip again have lags of their
own. Treat these as an order, not numbers to copy, since they come from idle and walk, not a flap:
- the trunk leads;
- the loose parts follow later, each by its own amount;
- the tail is a wave, its middle and tip moving at different moments.

**Ours now** (`make_creatures.py`, "the wing beat in time"):
- **Frames.** A flying frame is a moment of the beat, `th` 0..1. There are eight of them: `bdy_fly_left_01..08`
  (`BEAT_FRAMES`).
- **The stroke.** `wing_w(th)` goes down in 40 % of the beat (lowest at 0.4) and up in the rest, eased at each end.
- **The body.** It is highest at 0.5, from the game's bob (`Flight.BeatBobPx`, unchanged), so it rises after the
  power stroke, not with it.

Each part reads the beat at its own lag:
- **The wing.** Elbow 0, wrist 0.03, root 0.05, finger tips 0.07 / 0.10 / 0.13. The tips trail up early in the
  downstroke and down early in the upstroke, so the wing bends like a sail. On the upstroke the hand folds in by up to
  16 %.
- **The hump and chest.** These are the stroke's muscles: just after it (0.06), at under half the old size.
- **The neck and head.** No nod with the wings; that nod was most of the "dance". The head rises by 0.8 of the body's
  rise, a quarter beat later: steadier than the body, and behind it.
- **The legs** are held, tucked: forelegs curled under the chest, hind legs folded under the belly. A first pass
  drew them up and reaching again on every beat (fore at 0.25, hind at 0.35). The user: "he looks like he's swimming
  more than flying ... like how dogs swim". The flying reference holds its legs in one pose across all three wing
  poses, with the body carrying them.
  - A second pass let them hang loose from the hips, each trailing the body's rise at its own lag (near fore 0.14,
    near hind 0.26, far fore 0.22, far hind 0.34). Worked out per frame, all four moved 3-4 px on the same frames:
    the shared body term (B(th) - 0.8 B(th - lag)) swamped the lags. The user: "all legs are still bobbing exactly
    the same".
  - A third pass held them still in the picture (the paw rows pixel-identical across the frames). The user: "the
    legs are all now completely static????". The flying reference has only three frames, so its legs could hardly
    move; that was over-read.
  - Now each leg has its own small loop in the airflow, not a share of the body's rise. The forelegs move mostly
    up and down (a curl), the hind legs mostly back and forth (trailing). `LEG_SWAY` sets the size, 1.5 px.
  - Each leg runs at its own moment, spread unevenly round the beat: near fore 0, far fore 0.3, near hind 0.55,
    far hind 0.8. No two legs are in step, and no pair is opposite (alternating strokes are a paddle again).
  - In flight the far pair sits 5 px back and 2 up from the near pair (3 and 1 on the ground). At 3 px apart the
    swaying claws met (a four-claw bar on one frame, blobs at the hind paws on others), and the near legs hid the
    far legs' motion.
  - Checked before handing over: each paw's offset per frame (no pair shares an offset on more than 4 of 8 frames
    at 1 px, 2 of 8 at 2 px); zoomed crops of all eight frames at 8x without the wings (`DRAGON_STUDY=nowings`)
    and with them. The preview page carried 1 and 2 px as studies beside the shipped 1.5, and the walk frames were
    identical across the three.
- **The tail** is a whip. Its points follow the body's rise at 0.06 / 0.14 / 0.22 / 0.30, with 1.0 / 1.4 / 1.9 / 2.5 of
  its size, so the tip overshoots. The curl goes down at each point's own low.

Measured the same way (wings left out), each part is now highest at:

| Part | phase |
|---|---|
| hump | 0.53 |
| tail middle | 0.61 |
| tail tip | 0.73 |
| head and neck | 0.76 |

The head swings 2 px and the tail tip 8 px. Each leg moves on its own loop (above).

The game plays the eight frames over `WingBeatS` (`FlyNames.Length`): 75 ms a frame at 0.6 s. The preview page's `fly`
clip also runs at 75 ms.

### The glide (2026-10-23)

The user: "a glide animation where the wings are just open while they are gliding and we can have this trigger
sometimes when flying in a direction while not idle". Neither reference has a glide; the frames are our own.

**First pass** (the same day): the wings held still at the pose `GLIDE_T = 0.4`, only the tail and the legs moving,
and a cut from the beat's frame 7 into the glide and back. The user: "the idea is there but it's just making the
wings look very static ... the transition from flying to gliding and vice versa [should be] more natural for the
wings mostly". Two things tried in that pass and dropped, both because they changed pixels without a visible move:
- the finger tips flexing 1 px: every vein re-drawn at a new angle, 200 px of speckle over the membrane each frame;
- the neck swaying 0.3 px: it only re-rounded its shading, 40-100 px of speckle a frame.

**Second pass.**
- **The wings flex** (`GLIDE_FLEX` 0.06, `GLIDE_WING_LAGS`): each joint's pose sways about `GLIDE_T`, slowly, at its
  own lag: the arm first, the wrist, the root, then each finger later (0 .. 0.22 of the loop), so the wing bends
  like a sail working the air. The tips move 4-5 px, a move the eye follows: the wing's top edge runs rows 12-16 and
  its tips columns 118-122 over the loop (the job's `glide_flex2.png`). 640-990 px change a frame.
- **The body swells with them** in the game (`Flight.GlideBob`, `GlideBobPx` 1): half the beat's rise, plus 1 px
  rising a little after the wings press lowest. The neck, the head, the chest and the hump hold the glide's pose
  (their own flex would only re-round their shading). The tail waves (tip 1.5 px), each leg keeps its loop at 0.6.
- **The way in and the way out** are in-between frames whose every part is its beat pose blended into its glide
  pose (`dragon(..., beat=, glide=, mix=(m, +1|-1))`), the wing's joints each with their own share of the way
  (`GLIDE_MIX_LAGS`, eased): going in the arm reaches the glide first and the finger tips last; coming out the arm
  leads back into the beat. The upstroke's half-closed hand opens as the glide takes it.
  - **In** (`bdy_glidein_left_01..04`): the beat's moments th 0.75, 0.875, 0, 0.125 at 0.2, 0.4, 0.6, 0.8 of the
    glide. It starts where the beat's frame 7 would be: the wings come up through the open pose, rise a little past it
    and settle open. Played slowing, 85-150 ms a frame.
  - **Out** (`bdy_glideout<q>_left_01..04`, q 1-4 from each quarter of the loop): the beat's moments th 0.25 .. 0.625
    at 0.8 .. 0.2 of the glide: a downstroke out of the open wings, handing over to the beat's frame 7. Played
    speeding up, 130-80 ms a frame (both scaled with `WingBeatS`).
  - Checked over the whole sequence (`glideseq.py`, every exit): the change per step is 650-1800 px, inside the
    beat's own 1000-2400; the hand-over to the beat (out 4 -> frame 7) is 1900 px, as the beat's frame 6 -> 7.
- **Every other picture of the skin is byte-identical** to before (the beat, take-off, landing, ground frames).
- The tail's spike reaches the picture's last column (127), as in every flying frame and the standing frame (open,
  from before).

**Third pass: the flex in pixels** (the user, looking at lower held poses on the preview page: "the glide in preview is
still flapping"). The flex was a share of the pose (0.06), and the poses change at very different speeds along the beat:
a tip moves 84 px per unit of pose at 0.4 and 163 at 0.85, so the same share swung the tips 5 px at 0.4 and nearly
10 at 0.85, a flap. Now `GLIDE_FLEX_PX` (3) is the swing of the fastest joint, turned into pose at the held pose:
over the loop the front layer's outline moves 4-6 px end to end at every held pose (0.4, 0.55, 0.8, 0.85, 0.9).
The held pose: the user's pick from the page's studies (0.4, 0.55, 0.8, 0.85, 0.9), **0.9** ("angle wise of the wing,
the last one i think looks better"): the wings held low and spread back, the near one hanging in front of the flank
over his hips and legs (44 % of his lower body, 4 % of his torso, never his head) instead of across his chest (0.4:
60 % of his torso), so it no longer cuts through him at the waist. The way in and out blend to it as before.

**When it plays** (`Flight.GlideTick`; Flying tab rows "Glide now and then, flying along" and the "Glide:" rows,
session-only):
- Only airborne (not the launch or a jump), not while climbing, and only for a skin that has the loop, the way in
  and the way out (`Looks.Rig.HasFrame`).
- He must fly along in one direction (the movement input over half way, within 45 degrees of where he set off) for
  `GlideAfterS` 2 s. Then, once each beat as the beat reaches its frame 7, a glide starts by chance (`GlideChance`
  0.35). Its loop lasts `GlideMinS`..`GlideMaxS` (1.5-3 s) at random, over `GlideLoopS` 1.6 s a loop.
- The beat's clock stops on frame 7 for the whole glide and goes on from there after the way out.
- Ending: its time, stopping, turning more than 45 degrees, climbing or a landing. It waits for the next quarter of
  the loop (at most 0.4 s), then plays the way out from that quarter. Begun while the way in still plays, the way out
  follows it from the first quarter. A landing lets the way out play (its flare frames take over as usual); any
  other phase change stops the glide at once. After a glide he beats for `GlideAfterS` again before the next.
- "Glide now" on the tab starts one at the next frame 7 and lets it run its time, moving or not.
- Log: `[flight] glide for 2.3 s (flying along 2.4 s)`, `[flight] glide ends: turned (after 1.9 s)`,
  `[flight] glide stopped: now Ground`.
- The preview page has the clips `glide` (the loop, 200 ms a frame) and `glide-in-out` (two beats, the way in, the
  loop once, the way out from the first quarter, back into the beat). The scans accept
  `bdy_(glide|glidein|glideout[1-4])_left_NN`.

Not seen in game yet.

### Refused landings: cut off from the take-off spot (2026-10-23, investigating)

The user: "im flying over a lot of valid landing spots but its telling me that i can't land. i then land on a
different part of the map, ride towards that area which didnt let me land, and then when i fly there it lands me".
Their screenshot: open ground by their home, "Nowhere to land here.".

**The log.** Every refusal is the connection rule (`FlightGround.WhyCannotLandAt` -> `WhyNotConnected`: a landing
must be on the same connected part of the game's walkable mesh, the Recast graph, as the take-off spot, so never on
a rock top he can't walk off): `(-4.92, 6.00, 15.99) is cut off from (-27.46, 6.00, 18.70) (separate parts of the
walkable mesh, areas 434 and 1)`, and an earlier flight the same with areas 469 and 1. So:
- The mesh holds the take-off spot (area 1) and the ground by the home (434) as two parts with no path between them,
  though the keeper rides from one to the other.
- Taking off from inside the home's part, the same spots pass (same area as the take-off): the "it lands me" half.
- The home's part was 469 in the first flight and 434 in the second: the graph's parts are renumbered between
  flights, so the mesh is updated while playing (a build, a cut).

A readout was added first: `[flight-area]` lines (`Flight.AreaWatch`, every 0.2 s while mounted on the ground), one
each time the mesh's part under the keeper changes or he leaves the mesh. The user's next session (13:18) logged none
(no ride across the line), but its refusals drew the line: take-off at (-20.90, 20.27), area 1; refused spots all west of
it in area 59 (one 127), the nearest (-26.06, 17.74) 5.6 away. The same line as the two sessions before, x -23 to -26.

**The cause, from the game's code** (decompiled 2026-10-23):
- The mesh is the NPCs' navigation (`PlayerController.SceneRecastGraph`, one Recast graph per world from
  `SceneGraphsData`; `AgentAI`, workers, mobs walk it). A Recast mesh is shrunk from every obstacle by the agent's
  radius, and the game cuts it under every building (`NavmeshCut`, `GraphCutUnit`, `NavMeshCutPlannerMesh`). Round a
  home full of buildings it falls apart where the gaps are narrower than an NPC; the keeper's body (moved by physics,
  not by the mesh) passes. The part numbers change whenever the cuts change (469, 434, 59, 127, 503, 643).
- The keeper's own walk grid (`PlayerLocalAreaMovement`, graph 3) is a GridGraph 44 x 44 cells of 0.1, rescanned
  round him (4.4 x 4.4): for walking him to an interaction spot, too small to join places 20 apart.
- **The game's own rules for where the keeper walks:** `PlayerInvisibleWalls` raises an edge wall wherever a ray 0.6
  down from 0.05 over his feet finds no ground layer (11, 12), at eight points 0.1-0.4 round him; and
  `PlayerColliderTester` says he fits where his collider goes no deeper than 0.05 into anything.

**The fix: the walk-away test** (`Mount.Instance.WhyCannotWalkAway` / `WalkAway`, FlightGround.cs; built 2026-10-23,
not seen in game). When the NPCs' mesh says the spot is on another part than the take-off spot, a landing asks the
keeper's own ground instead:
- a spread from the spot over a grid of 0.25 (breadth first: the nearest way out first); a step counts when the
  game's edge rule finds ground there (the highest ground layer under it, no more than 0.55 below the step before,
  no more than 0.45 above it: the shuffle's measured `MaxRisePerSample`) and his body fits there (`BodyDoesNotFit`,
  the game's penetration test against every layer he collides with);
- it succeeds at the first step lying on the take-off's part of the mesh **at its own height** (within 0.6: seen from
  above, a rock top with no mesh finds the ground's mesh under it, and would have passed at once). That part is the
  NPCs' and so narrower than his: a step on it is joined to the take-off spot;
- it is refused when the spread ends ("walled in: ... a rock top, a ledge or a closed pocket") or reaches 6000 steps
  (a hover costs nothing; a wrong landing can strand him on foot);
- one line per test, `[flight-walk] <spot>: the NPCs' mesh says ...; the keeper's own ground: ...` (how far, how
  many steps, how many ms); results kept 5 s per spot (the landing asks every 0.25 s while hovering);
- used by a landing (`TryLand`) and a forced one (`ForceDown`: a save, the experiment off); the 1 s "last safe spot"
  check keeps the mesh's answer (cheap, and never wrong when it says yes).
- Self-test "landing: the walk-away test" (group Flying): his own spot reaches his part at once; a part no cell is on
  runs to the cap from his spot (the worst case's time, under 400 ms); a pen of four temporary walls 2.5 to his side
  walls its spot in.
- The `[flight-area]` readout stays until the user has seen landings by the home work.

**Second pass (the same day): freezes, and the far take-off.** The user: "sometimes is freezing when i press space
while trying to land". The log (13:32):
- Every test that didn't find the take-off's part ran all 6000 cells in one frame: 282-331 ms (`slow frame 299 ms ...
  Tick 293`), one freeze per Land pressed. Physics queries only run on the main thread, so the work has to be spread.
- Those were the user's home ground east of the yard (x -5 to 10, z 16), refused from a take-off on the plateau at
  (-34.61, 7.19, 18.99): the plateau's part of the mesh is 35 away and a level up, past any short spread (15-18 out).
  "Walk to the take-off's part" can't be the only yes when the take-off can be anywhere on the map.
- What worked: by the yard, the take-off's part found 6.0 out in 536 cells, 18.7 ms; a ledge on the 8.4 level refused,
  a strip of 69 cells.

The test now:
- **Yes** at a cell on the take-off's part (at its own height), or once his ground from the spot reaches 1000 cells
  (`WalkOpenCells`, 60 square units of ground he can walk): open ground, no pocket. **No** when the spread ends first:
  a rock top, a ledge, a closed pocket. A big enough area he can't walk out of is accepted: the ground is his, and the
  dragon lands with him to fly off again.
- **Sliced**: at most 4 ms a frame (`WalkSliceMs`), the landing hovering meanwhile (`WalkPending`, asked again the
  next frame, no bubble); 1000 cells at the measured 0.05 ms a cell is about 50 ms, a dozen frames. A forced landing
  (`ForceDown`, before a save) judges at once. The log line gives the cells, the ms and the frames.
- The self-test's open-ground case now expects "open ground" and a whole spread under 200 ms (INFO: how many frames).

**A review before the user's test (2026-10-23, three readers: the flight's states, the walk-away test, the generator).**
Fixed:
- the walk-away test walked on lake beds (the ground ray goes through water): `GroundStep` now asks `WaterOver`;
- the edge walls (layer 14) are left out of its body test, as the flight's own sweeps do (the game may raise them
  round the body under the flight, beside the spot: a false "walled in");
- steering while it judged began a new job every 0.25 and never finished (the landing hovered on): a job goes on
  while the spot stays within a unit of where it began, on the same level; the level is in the kept answers' key;
- "on the take-off's part" at the game's limits (0.55 down, 0.45 up), not 0.6 either way;
- its cells are kept between jobs (no new collections per spot), a clock without an allocation per slice;
- `BodyDoesNotFit` (every ground test) skips the floor under the feet first and asks `IsModView` (a walk over every
  instance) only for what really goes into him;
- a landing in the same frame as a step-up replaced the step's camera move (`GiveBack` now adds to it); a flight
  ended at once (`Release`) left its body move for the next flight's first frame (`Land` now applies it,
  `ApplyBodyMove`); the camera is warped by `CameraFollow`'s share of the height's change;
- the step-up makes up only the height above the floor (low over a rise the flight climbs with it; made up below the
  floor it popped back the same frame);
- a skin with the beat but no landing frames keeps the beat's rise through the flare;
- the movement lock read through a cached field reference (a `Traverse` allocated every physics step); ordinal
  `StartsWith` on the per-frame paths; a glide's way out can't be skipped by a loop length changed mid-glide; "Glide
  now" greyed while gliding;
- the self-test: skipped indoors, the timing after a warm-up run, the same answer in 0.2 ms slices as at once, the
  pen only on a layer that stops him, on the ground, and gone before the next test.
After the user's next session (14:06): one landing judged by the walk-away test (area 59, refused all day, walked to
the take-off's part 4.5 out, 353 steps, 14.5 ms over 4 frames); every refusal a real one (inside the ruins, no
ground, under a ledge); the only slow frame of the mod a touchdown (36 ms, Tick 30.9) with no walk-away test in it.
Suspected: the game's walk grid rescanned round the keeper on every landing (`WhyNotOnGameGrid`). Timed now (`[perf]`
parts `Landing` and, inside it, `LandGrid`) before anything is changed. The `[occlusion]` readout is off by default
(the Flying tab's "Front layer readout"), and a skin's load line counts the flight's own frames instead of listing
them as names that match no frame.
Left, by decision: a closed area over 1000 cells (60 square units) counts as open ground (his ground, the dragon with
him); a cell first reached on one of two stacked surfaces keeps it, a refused cell stays refused (both only refuse).

### Two cuts from the jump detector (2026-10-23, fixed, not seen in game)

The user's session log (12:58), `[flight] jump of N px in one frame` lines of 3 px or more, sorted by kind:

- **Over a raised ground piece, flying free (the biggest).** `the body steps up 0.39 onto BigDropTopSurface`, then
  +4, +5, +3 px on three frames (88 screen px), the flight at 200 px. The cause: free flight's height is counted
  above the keeper's body, and `FixedStep` steps the body up onto the walkable surface under it (it follows the hills).
  The height above the body stayed the same, so the whole dragon rose 0.39 (12 art px), eased over three frames by
  the body's smoothing, and stayed that much higher until it landed. **Fix:** a step of more than 0.1 shrinks the
  height above the body by the step, so the flight keeps its place in the world. As for a landing's move, the camera
  is told the body moved (`bodyMove`, `OnTargetObjectWarped`) and the body's smoothing is off for two physics steps.
  The floor still holds the flight the clear height above the new ground, so flying low over the rise it still
  climbs. Log: `the body steps up 0.39 onto ...: the flight keeps its place (200 -> 188 px over the body)`.
- **The landing's first frame.** -3 to -7 px, every landing, as the frame switched from the beat to `land_01`. The
  cause, from the numbers (line 535, lift 0.733 -> 0.500): `LiftFor` took the beat's rise only for `bdy_fly_` frames;
  for the landing's frames it took the old hover, on its own clock. Beat +3 px to hover -2, with the height's own
  -2: -7. The same switch back put +3 px into a refused landing's return to the beat (`land_01 -> fly_01`). **Fix:**
  every flying frame the skin has (the beat, the take-off, the landing) takes the beat's rise; through the landing's
  frames the beat's clock stands still, so the rise holds and fades with the height (`Clamp01(height / 12)`). The
  hover stays only for a mount without the frames.
- **The end of the take-off** (+5 to +7 px, `takeoff_04 -> fly_05`): the launch really rises that fast there (height
  +5 px a frame); the camera rises with it (6-9 screen px). Not a cut, left as it is.

### The floor risen to, not jumped to (2026-10-23, built, not seen in game)

The jump detector in the user's 14:16 session: +12 to +20 px in one frame, two kinds, both the floor taken at once
(`heightPx = floorPx`) when it rose:
- climbing out from under a ledge's edge (take-off at 21.10, -7.43 on ground at 3.60 under the 6.00 level's edge
  tiles): once the flight was above 6.0 that level counted under it, the floor went 30 -> 88 px, +20 px in a frame;
- hovering low over a wood after a refused landing (13.17, 27.92): every bush and tree top raised the floor 12-17 px.

Now (`FreeTick`): the flight goes at once only as far as what is really there (the ground, a rock, a tree top: the
floor without its clear height), and the clear height over it at `FloorRisePxPerS` (240 px a second, a Flying tab
row). Never below what is there, only inside the 16 px over it for a few frames. A launch keeps its upward speed
while it rises to the floor (its spring was stopped every frame under it). The floor's log line comes once per rise,
with how much of it was at once.

### Height jumps across the land (2026-10-23, investigating)

The user: flying across the land the height sometimes jumps from one height to another at once. The one line the
log had: `the floor lifts the flight 134 px: walkable ground under it 3.60, drawn ground 3.60, drawn things up to
9.77, the body at 3.60, now 185 px up`: the floor's downward cast met a tree's trunk collider and took the tree's
drawn top (9.77, its crown) as a floor under a flight at about 5.3. Fixed: the floor counts only what lies under
the mount's feet (a thing drawn higher is beside or around the flight; the bumping keeps it out sideways).

For the rest, a detector, always on while flying (`Flight.Jumps`): a step of the drawn mount of more than 3 art px
in one frame is logged as `[flight] jump of N px in one frame at x,z:` with the mount's height, the body's (drawn
and physics), the flight's height and lift, the phase and free flight, and the floor's four reads, before and after.
Every remaining kind of jump will name its cause there.

- The cut while landing (2026-10-23, caught by the detector at 83.22, 12.88): the dragon took off from a plateau
  (6.0) and came down over ground 2.4 lower. The landing put the body down 2.42 and grew the lift by as much (the
  mount's height unchanged, 6.583 -> 6.600), but "on the screen 972 -> 684 (camera y 14.583 -> 16.841)": the
  camera's offset grew at once while its follow of the body eased down over the next frames, so the view jumped
  up 2.3 and slid back. And the frames went `land_01 -> fly_04`: the flare had been judged on the height above the
  body (16 px) while the dragon was still 87 px over the ground, then the beat came back. Fixed: after the body is
  set down the camera catches up at once (`CameraController.UpdateTargetPosInstant`, logged), and the flare and
  the landing frames go by the height above the real ground under the flight (`Flight.AboveGroundPx`).
  A second cut the same night, the other way round (12.48, 13.98: landing on a plateau 2.4 above the body): the
  dragon dove at full speed to the floor over the plateau, was set down and touched down from its dive in the same
  moment (frame fly_04 -> land_01, "landed" at once). `AboveGroundPx` counted only ground below the body; it now
  counts ground above it too, so the flare starts over the plateau as over any ground. The camera catch-up worked
  there (camera y 14.567 -> 14.534).

- A refused landing now hovers where it was refused (at the floor), airborne, waiting for the next Land (the
  user, 2026-10-23: it shot back up to the flying height); still no landing at the next spot that would take it.

- More cuts from the same build (2026-10-23, the log): at 40.62, 19.68 the floor found a thing drawn up to 6.72 under
  the flight (over ground at 3.60), the landing still went on, the body was set down on the ground under the thing
  and the dragon came down through it; and the camera's catch-up there moved the dragon 322 px on the screen with the
  camera's height unchanged (1094 -> 772): the catch-up also ends the camera's sideways lag behind a moving body, a
  cut of its own. Fixed: no landing where something drawn stands higher than the ground (the dragon stays on top).
  Open, measured next: the detector now logs the camera's and the body's x and z too, to see the sideways lag the
  catch-up removes before changing how the camera catches up.

- The landing cuts, measured (the user, 2026-10-23: "cuts when landing in some places while I'm still flying"). The
  detector's lines show two causes.
  - **The drawn body blended from the old spot.** At 23.24, 11.15 the body was set down 2.54 off a plateau. Its
    physics position was 3.60 from then on, but the drawn body showed 4.80, then 3.75, then 3.60: a 34 px flash up
    and back (on the screen 776 -> 934 -> 772). At 5.31, 15.77 (set down 2.40 up onto a plateau) the drawn body dipped
    5.996 -> 5.787 -> 5.996, a 6 px dip. The game smooths the drawn body between physics steps from the pose before
    (Rigidbody interpolation); `PlayerPhysicalBody.SetPosition` sets the body and its transform, but the next frames
    still blend from the old pose. Fixed: when a landing moves the body, its smoothing is off until two physics steps
    have run from the new spot, then back as it was (`Flight.GiveBack`, `FixedStep`).
  - **The camera's catch-up threw away its sideways trail.** At 5.31, 15.77 the camera's x went 3.50 -> 5.31 in one
    frame, and the dragon moved 367 px across the screen (1566 -> 1199). `UpdateTargetPosInstant` is Cinemachine's
    `CancelDamping`: it ends every lag, the camera's normal trail behind a moving flight too. This is the 322 px snap
    above. Fixed: the camera is told the body moved by exactly the landing's move. Cinemachine's
    `OnTargetObjectWarped(follow, delta)` shifts the framing transposer's smoothed position with its follow target,
    so the drop and the lift's equal growth cancel and the trail stays. It is logged as "the camera moved with it
    (following <target>)". The API now references the game's Cinemachine.dll; the call sits in a method of its own
    so that a missing Cinemachine falls back to the old snap with a warning.
- The flight's bubbles stay `Flight.BubbleS`, 1.5 s (the Flying tab's "Bubble time"; the user: "way too long"). The
  game's own time for a bubble is 2.5 s plus 0.085 s a letter, counted after the letters type in
  (`SpeechBubbleSettings.CalculateBubbleShowingTime`): about 5 s for "Nowhere to land here.". `Mount.Say(text,
  seconds)` passes `PhraseData.fixedShowTimeValue`; the other callers keep the game's time.

- No cart in the air (the user, 2026-10-23): the cart key, the panel's Drive the cart and `Mounts.SwitchStyle(Cart)`
  do nothing while the mount is off the ground (`Flight.InAir`), quietly: no bubble, no landing.

Not yet known: what draws the table (one mesh, its bounds, readable or not), whether any collider at all lies under
its top (another layer, a trigger, back faces only), and whether such places are common (decorative cliffs, map
edges). The survey now also writes `GK2-flight-survey-HHmmss-column.txt`: every collider a ray from 60 above meets
straight down (any layer, triggers, back faces) and every renderer whose bounds hold the keeper's x and z. Next:
the user surveys hovering over the table; then the solution is chosen from the facts. The candidates: make the drawn
ground solid for the flight (colliders made from the nearby drawn meshes); keep the flight out of places with
neither physical ground nor water under them (an edge, as the game's own walls do on foot); or the drawn meshes'
tops as the floor there.

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
