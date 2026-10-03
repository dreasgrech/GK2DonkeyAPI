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
  water surface on a ground layer would pass the raycast), at the seat's height (within 0.6), and the keeper must be
  able to walk there (`LineWalkable`). The seat is never below the navmesh: `PlayerController.SetPosition` puts his
  body at exactly the given height, and under the ground's surface nothing holds him. Otherwise:
  - riding: the donkey comes to the keeper instead (`ComeTo`), and he doesn't move. Logged:
    `Mount: ...: its seat is no place for the keeper (...); it comes to him instead.`;
  - hitching while riding: refused, "I can't get to that cart from here."

For the parked donkey, "ground" (`GroundAt`) is the navmesh, or the ground raycast at about the keeper's level
(within 0.6). The navmesh alone would be too strict there: along a wall or a fence it is cut back, and every ordinary dismount there would move the
donkey. The raycast alone is no proof either: over water it finds nothing, or the lake bed well below. In a scene
without a navmesh nothing is judged and everything works as before.

A donkey already saved standing in the water stays there until it is ridden: getting on brings it to the keeper,
and the next dismount parks it on the ground.
