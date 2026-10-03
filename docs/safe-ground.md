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
also refused a ride from the jetty once (the donkey came to him instead). Colliders were never the danger; the check
is ground only now (`GroundGap`).
