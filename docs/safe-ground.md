# Ground the keeper can stand on

Found 2026-10-03 (two of the user's videos, at the Inquisition Base fishing jetty):

1. Riding up to the jetty and pressing E to fish: the fishing window opened, the keeper stood at the fishing spot
   running on the spot, and the donkey was standing in the lake.
2. Pressing E on that donkey to ride it: the keeper fell through the map.

## What happened

From the game code (decomp 1.008):

- `ReservoirInteractionHandler.StartFishing` walks the keeper to the jetty's dock point with
  `PlayerController.MovementComponent.StartPath` (`MovementType.Direct`). It takes **no control**: the keeper stays in
  `FreePlayerState`, so the ride went on. The mod ended a ride on a scripted walk only under a script's control
  (`ByFlow`), and this walk has none.
- While ridden, the donkey is drawn next to the keeper (`Follow`: keeper + away·Depth − right·(lateral + fore)),
  wherever he goes, and that place is never checked. At the jetty's tip it is over the water.
- At the end of the walk the fishing window opens: `PlayerFishingComponent.StartActivity` takes control
  (`ByFishing`) and sets the keeper's animator to `FishingStart`. The mod saw control taken and dismounted:
  - `Park` left the donkey where the ride had drawn it, in the lake;
  - `RestorePose` set the keeper's animator to the walk (he was still walking when the path ended), over the game's
    `FishingStart`: the run on the spot.
- Getting on (`RideFromWorld`) moves the keeper to the donkey's seat (`KeeperSpotFor`), at the donkey's height, with
  no look at the ground. Over the water there is no navmesh and nothing to stand on: he fell.

The log of that session was overwritten by a later run, so this is from the code, not measured. The new log lines
below say when each guard acts.

## The fixes (`SafeGround.cs`, `Mount.cs`, `MountInstance.cs`)

- **The game walking the keeper ends the ride** (`WhyMustDismount`): `MovementComponent.IsMoving`, with or without a
  script's control. Only scripts (`Flow_GoTo`) and the walk to a fishing spot use the keeper's MovementComponent;
  the player's own walking never does. The ride ends where he stands, before he is walked off. Getting on while it
  walks him is refused ("I'm busy.").
- **The pose is restored only over the riding pose** (`RestorePose`): when the keeper's animator shows a state the
  game set (`GetState()` is not Walk, Idle or the riding pose), it is left alone.
- **Park keeps the donkey on the ground** (`KeepOnGround`): when there is no ground under the donkey's body, it moves
  to the nearest spot next to the keeper where its body is on the navmesh, its root on ground, nothing blocks them and
  the keeper can walk to it (rings 0.25 to 2 units around its body, 12 directions); failing that, onto the keeper's
  own spot. Logged: `Mount: ... was left off walkable ground at ...; moved ...`.
- **Getting on only onto ground** (`WhyKeeperCannotStandAt`): the seat must be on the navmesh (only the navmesh: a
  water surface on a ground layer would pass the raycast), at the seat's height (within 0.6), and there must be ground
  all the way from the keeper to it (`GroundGap`: no water, no ledge; no collider test, see below). The seat is never below the navmesh: `PlayerController.SetPosition` puts his
  body at exactly the given height, and under the ground's surface nothing holds him. Otherwise:
  - riding: the donkey comes to the keeper instead (`ComeTo`), and he doesn't move. Logged:
    `Mount: ...: its seat is no place for the keeper (...); it comes to him instead.`;
  - hitching while riding: refused, "I can't get to that cart from here."

For the parked donkey, "ground" (`GroundAt`) is the navmesh, or the ground raycast at about the keeper's level
(within 0.6). The navmesh alone would be too strict there: along a wall or a fence it is cut back, and every ordinary dismount there would move the
donkey. The raycast alone is no proof either: over water it finds nothing, or the lake bed well below. In a scene
without a navmesh nothing is judged and everything works as before.

## Other places checked (2026-10-03, after the fix was confirmed in game)

Every place the mod puts the keeper, a donkey, a cart or an item, checked against the code:

| Where | Finding | Done |
|---|---|---|
| The keeper: riding a standing donkey, hitching while riding | The only two moves of the keeper (besides the dev self-test) | Guarded (above) |
| **Water in the ground test** | The game marks water with `WaterComponentTag` on a solid collider in layers 8, 10, 11, 12 (its drop-spot search, `SpecialPhysicsCastUtils.GetPlayerDropPosition`, mask 7424, refuses it). `GroundHeight`'s raycast (layers 11, 12) didn't look: on those layers a water surface counted as ground; off them the ray went through to the bed, and the shuffle's line check (height change per sample only) could walk a donkey down a sloping bank into it | `GroundHeight` returns no ground (source `water:<collider>`) where water lies over the ground or there is water and no ground. Riding's own ground sampling (`GroundHeightPair`) is unchanged |
| The cart of a parked donkey | Only the donkey's body was checked; the cart, about a unit behind, could stand over the water | `KeepOnGround` checks the cart point too and searches out to 3 units with a cart. Logged `... was left with its cart off walkable ground` |
| Leaving the cart (H while riding, `Unhitch`) | The free cart stays where the ridden cart was drawn, which can be over the water | Refused with "The cart can't stand here." (the log says why) |
| Calling the donkey (`ComeToKeeper`) | The place next to the keeper isn't checked. Saddle Up rides it at once (and bringing the cart hitches it at once), so it only stays there if that ride doesn't start or another mod calls it without riding | Left as is: the next dismount parks it on ground, and correcting it would move the keeper at the hitch |
| Cargo set down at the cart (`CartPoint`) | Direct `DropItem` with no water check: another mod's `SetDownCargo`/`SendAway`, the API switched off in its config, items that don't fit a re-summoned cart, and the save's copy (only seen if the mod is uninstalled). Saddle Up calls none of these | Covered by carts staying on ground |
| Cargo set down behind the cart / in front of the keeper | The game's own spot search, which refuses water | Nothing to do |
| A foal's last-resort spot (`FreeSlot` fallback) | Not checked; only when no slot has a clear line from its parent. A foal could stand in shallow water | Left: visual only |

A donkey already saved standing in the water stays there until it is ridden: getting on brings it to the keeper,
and the next dismount parks it on the ground.

## Fence regression (2026-10-03, fixed the same day)

The first version checked the way from the keeper to the seat with the idle shuffle's `LineWalkable`, which also
refuses any keeper-blocking collider along the line. Riding along a fence, the keeper's own spot touched it ("sample 0
... blocked by Collider (layer Obstacle)"), so every hitch there was refused: the cart G brought to him stayed by the
fence, and riding up to it again was refused each time (the user's video, log lines "hitching ... refused"). It had
also refused a ride from the jetty once (the donkey came to him instead). The check became ground only (`GroundGap`).
That was wrong: walls are a danger too (next section).

## Locked in behind a rock (2026-10-04)

**What happened** (the user, RuinedTemple, riding without the cart):
1. F at an ore ended the ride (`WorkPlayerState`), and the donkey, drawn next to the keeper, stayed inside the rock at
   (-14.46, 6.00, 23.34). `KeepOnGround` only asked for ground, and there is ground under a rock.
2. The idle shuffle found it inside `Obstacle (1)` and stepped it 0.6 out (`StepOutIfInside`), to (-15.24, 6.00, 23.86):
   the nearest free spot, on the far side of the rock face, in a closed-off pocket.
3. Getting on moved the keeper to its seat (`RideFromWorld`, `pc.SetPosition`). Ground, navmesh and height all passed,
   so he was put in the pocket, with no way out. Nothing was saved there (the last save was before the ore).

**What stops the keeper** (decomp 1.008, checked in the review): physics only. He is a rigidbody (layer Player 10,
convex `MeshCollider` 0.50 × 1.07 × 0.30, continuous-dynamic collision checks already) moved by velocity
(`PlayerPhysicalBody.MoveByDirection`), stopped by every collider his layer collides with and by the invisible edge
walls the game puts up where a ground ray finds nothing (`PlayerInvisibleWalls`). The recast navmesh only sets his
height (`MovementAdjustComponentBase.UpdatePos`). The game's own "can he get there" is `PlayerLocalAreaMovement.IsReachable`
on its walk grid (graph 3, rescanned around him against the colliders before every walk it gives him).

**The rule now:** the mod moves the keeper only to a spot he could walk to himself. The only moves are getting on a
standing donkey and hitching while riding (plus the dev self-test); both go through `WhyKeeperCannotStandAt`, which
ends with `WhyKeeperCannotReach`. Everything must pass:
1. **Not far:** at most 2 units (`MaxKeeperMove`). Seats are within reach anyway (1.3 to the donkey's body); further
   (another mod's `Ride` from afar) the donkey comes to him.
2. **His body's way:** a capsule a little thinner than him (radius 0.12, 0.2 to 0.95 up) swept from him to the spot,
   and rays at 0.15, 0.5 and 0.9 both ways (a one-sided mesh blocks rays from one side only), with back faces on.
   Against every layer he collides with; only a ground layer (11, 12) facing up is floor. A collider he starts inside
   doesn't count, so the fence he rides along (the 10-03 regression) doesn't. A full hit buffer counts as blocked.
3. **His body fits there:** his own collider placed at the spot (`ComputePenetration`, the game's 0.05 tolerance).
4. **The game's walk grid connects him** to the spot (rescanned around him, as the game does; not while the game is
   walking him).
5. **The walkable mesh connects them**, where both lie on it (within 0.25): a nearest node off the mesh means nothing.

If any of them fails, the donkey comes to him instead (getting on), or the hitch is refused. In a scene without a
navmesh, all but the ground tests still run. Every allowed move logs `Mount: the keeper may move from ... to ...`;
every refusal logs which test failed and what was in the way.

**Donkeys are kept on the keeper's side too**, so a later ride doesn't have to refuse:
- `KeepOnGround` (every dismount with the view there): the donkey's body must be free (`FreeAt`), his body's way to it
  clear (test 2, any distance) and on his part of the walkable mesh; otherwise it moves to the nearest spot that is
  (ground all the way from him, `GroundGap`; the old `LineWalkable` refused his own spot beside a fence), or onto the
  keeper's spot. Logged as `... was left where the keeper can't reach it (...)`.
  Only while the keeper is on it: when he is more than 2 units away (flat) or 1.2 up or down, it stays where it stood
  (`... stays where it stood ...: the keeper is not on it`). **Found 2026-10-04:** riding through a door, the game
  moves him inside and switches to the indoor light in one call (`SetPosAndUnFadeAsync`); the ride ends on the next
  frame with the donkey still on the doorstep. The house's inside lies about 100 away and 6 lower (RuinedTemple), so
  nothing outside counted as his ground, and the last resort put the donkey, cart and all, onto his spot inside: in
  the house and in the church, where riding is refused ("Not indoors."). A donkey already left inside comes out with
  Saddle Up's H (or G with its cart) outdoors.
- `StepOutIfInside`: a spot out of a wall must be on the keeper's part of the walkable mesh, and his body's way to it
  clear when he is within 8 units (in his scene).

**First test in game (2026-10-04), and what it showed:** getting on never moved the keeper into the rock (the donkey
came to him), but parking still went wrong twice:
- the donkey was moved only 0.25–0.5, still half inside the ore: `FreeAt` tests one thin capsule at its middle, and
  the ore's collider is smaller than its picture. Now its whole body has to be free (`FootprintBlocked`: a box 0.9
  long, 0.3 deep, 0.1 to 0.9 up), at parking and when a rideable donkey steps out of something;
- once it was put on top of the rock, at 8.41 against the floor's 6.00: under the rock the floor is cut out of the
  navmesh, and the nearest navmesh seen from above is the rock's top. Ground now counts only near the level it is
  judged at (`NearLevel`: the raycast within 0.6, the navmesh within 1.2, the parking search within 0.6 plus 0.6 per
  unit from the keeper), and a parking spot must also pass the game's walk grid around the keeper (rescanned once per
  search).
- An ore's drop ("SmallItemTriggerCollider", layer Drop) counted as a wall on the way to a seat. Items on the ground
  and anything the keeper pushes away (a moving rigidbody) are no walls now.

**Checking it in game:** Debug tab, "Keeper moves (safety check)": 32 `[reach]` lines around the keeper, each spot
allowed or refused with the reason. Next to a rock or a wall, every spot on its far side must be refused.

**Tried and dropped the same day:** switching the keeper to continuous collision checks at high ride speeds. His body
already uses continuous-dynamic checks (PROBE-FINDINGS, Player), so it never applied. At 4x he moves 5 × 4 = 20
units/s (damping 10), 0.4 per physics step; continuous checks stop him at walls, and the edge walls keep him on the
ground.

**Audit of keeper lock-out paths** (2026-10-04, by me and an independent review):

| Path | Finding |
|---|---|
| The mod moving the keeper | Only `RideFromWorld` and `Hitch` (and the dev self-test, off by default, which sends him home after each scenario); both go through the gate. The hitch moves him to exactly the seat it checked (same inputs) |
| The keeper's body | The mod never changes his collider, layer, kinematic state or collision mode; the seat and rider offsets move `main_character` (the sprites) only |
| Our donkeys, carts and foals blocking a passage | Their colliders are off from the moment a view exists (`MuteColliders`, `PrepareParts`); no navmesh cut |
| Ride speed | Clamped to 4x (API overrides too); continuous checks are the game's own |
| Dismount, doors, saving while riding | He stays where he is; doors are the game's teleport; the game saves his real position |
| Work, ladders, the game walking him | The ride ends first; the game walks him on its own grid |
| Saddle Up, the examples | Never touch the keeper's body |
| Never lost (`SafeSpots.Return`, 2026-10-24, [never-lost.md](never-lost.md)) | The one move that goes **to** a spot he could not walk to from where he is: he is out of the world (under the ground, falling into nothing, far below it) or asked for it (the button, the key, a mod). The spot is one he stood on in this scene, verified again at the move (the walkable surface at his feet, nothing over him, his body fitting), checked again two physics steps later; every such move is a warning in the log |

**Known, not a lock-out:**
- `SceneRecastGraph` is kept from the last scene that had one; in a scene without its own, the ground tests judge
  against the wrong mesh: every seat refused (the donkey comes to him) and parked donkeys moved onto his spot. No such
  scene is known.
- Donkeys placed without the check: a park with the view gone (through a door), `ForceCleanup` / ending the ride
  without parking, `ComeTo`'s spot, `MoveTo`, a restore from the save, a foal's last-resort spot, and a step-out with
  the keeper in another scene. None of these moves him; the gate is what keeps him out of a pocket.
- A hitch may now be refused where a post, a lamp or a person stands on the straight way to the seat, though he
  could walk around ("I can't get to that cart from here."). Riding up from another side works.
