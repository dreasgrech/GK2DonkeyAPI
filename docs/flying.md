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
- The scans accept `bdy_(fly|takeoff|land)_left_NN` (tests/SkinsScan `FlightKey`, tools/skins/check_skin.py).
- Second pass (the user: "the body is completely stiff, look at the reference again"; compared side by side,
  `compare3.png` in the job's tmp): the body moves with the beat, the saddle staying put: the legs hang well under
  the body, bent, the paws with their claws down (the flat walking foot read as standing in the air), the forelegs
  reaching with the wings raised and drawn up on the downstroke, the hind legs trailing back then pulled forward under
  the belly; the tail droops from the hips and swings down and in on the downstroke; the back humps behind the saddle
  on the downstroke; the head a little higher with the wings raised, lower with them down; the chest leads up with the
  wings raised. And in code (`Flight.BeatBobPx`, 3 px, the tab): the whole dragon and the rider rise through each
  downstroke and sink on the upstroke, in place of the hover, since a frame can't move the seat.
- Known for the next pass: the raised wing goes behind the rider (a compromise); the rider's bob and hands still follow the game's walk frames (they read the game's sprite, RiderMotion).

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
