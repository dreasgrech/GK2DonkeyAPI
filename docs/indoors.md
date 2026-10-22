# Mounting indoors (API 0.36.0 / Saddle Up 0.12.0, built 2026-10-21, hand-tested 2026-10-22)

A player asked to ride and drive indoors. Saddle Up has two rows under Behaviour, both off by default, so a player
who never touches them gets what they had before: the ride ends at a building's door.

| Saddle Up row | API key (`Mounts.Behaviour`) | Off | On |
|---|---|---|---|
| Allow mounting indoors | `DismountIndoors` (the row turned round) | the ride ends at the door, "Not indoors." inside | riding goes on through the door; the keeper can get on inside |
| Allow mounting with the cart indoors (greyed unless the first is on) | `CartIndoors` | the cart stays at the door with its load and the ride goes on inside; at the morgue you get off inside carrying the corpse, the mount with you; "Not the cart indoors." inside | the cart comes in too, its load on it |

## How it works

- **Indoors** is the game's light preset: `EnvironmentEngine.TimesOfDay.indoorPreset`, switched by the door's
  teleport (`PlayerController.Teleport` -> `SetPosition` -> `SetTimeOfDayPreset`). Houses use `indoor`, the morgue
  `dungeons`. A house's inside is elsewhere in the same scene (docs/foals-indoors.md): the mount's view stays
  alive, and `Follow` puts it under the keeper on the next frame, on the floor found by the ground probe (the
  keeper's navmesh, then a raycast).
- **A door with a loading screen** (another scene) is `StayMountedThroughDoors`, not these switches. Saddle Up keeps
  that off, so such a door still ends the ride.
- **The cart rule** is in `Instance.WhyNotCart` (code: `Mount.cs`): getting on with a cart, `SwitchStyle`, a
  hitch from the saddle or on foot and the companion's cart key all ask it.
- **The cart left at the door** (`LeaveCartAtTheDoor`, Mount.cs): a cart ride under the indoor light with the
  cart not allowed unhitches it (as H does mid-drive) where it is still drawn, on the doorstep: the door moved only
  the keeper, and the ride's frame runs before `Follow`. Its ground is judged at the cart's own level, the keeper
  being on another floor. The mount gets a Ride-style view under him inside. A summoned mount (its cart goes with
  the ride), or a cart that can't stand there, gets off at the door as before.
- **The morgue, mount only:** the left cart is armed for the hand-over with the rider still on (`MorgueHandover.Arm`,
  `rider`). When the door or the area is the morgue, the keeper gets off inside (`"morgue"`: a ride needs empty
  hands) and the corpse comes off the cart at the door into his hands; the mount stays inside by him. Not the morgue:
  he rides on, the corpse stays on the cart at the door.
- **Switching the riding row off while inside** ends the ride on the next frame; the cart row off while driving
  inside leaves the cart where it is and the ride goes on.
- **The morgue with the cart allowed in:** the drive goes on into the morgue and the corpse stays on the cart
  inside, taken off it as from any cart (the user, 2026-10-21).

## Self-tests (group Indoors)

- **The two switches under the indoor light:** the game's `indoor` preset switched on where the keeper stands.
  Every refusal and every end of a ride, with all three settings, and Saddle Up's two rows (bound; the second greys
  with the first).
- **Through a building's door:** the game's own teleport (fade, control taken, the preset, control back) to the
  nearest floor inside one of the scene's buildings (its `IndoorArea` boxes), riding and driving, and back out.
  Checks that the mount and the cart are on the building's floor by the keeper, and that with the cart not allowed
  they stay at the door outside.

**Safe wherever they start** (the user, 2026-10-21):

- **Skipped (not failed)** when the keeper is indoors, in armour, busy (a script, a conversation, work), in a
  co-op game (the light is the other player's too), or the light preset can't be read or put back. The light
  is only changed after a ride really started. A building's floor counts only well inside its box (0.75 in from
  the walls), not its doorstep.
- **The light** is put back to the start's own preset by name (`TimesOfDay.name`), never a guessed "outdoor".
  The game writes the name into the save, and an area may have its own preset. Both names must be in the
  game's preset table before anything changes.
- **Nothing tells the game anything.** A building is used only where landing would enter world zones the player
  has already visited (so the save's visited list doesn't change) and that no quest waits to be walked into
  (the game's `WalkIntoWorldZone` triggers, read from its event table). The spot the keeper comes back to must
  pass the same check, or the test skips. The test's door has an id no trigger listens for.
- **The player's own mounts are left alone:** while a test door runs, the foals' door handling
  (`Mount.TestDoorQuiet`) and the morgue's door note ignore it, so the player's babies don't follow the keeper
  in and out.
- **Always brought back:** the way back out runs after the test's cleanup, whatever happened inside (an abort
  included). It waits for control first, because a second teleport during the first would leave him between
  the two. If he can't be brought back, that is a FAIL with where he is.

The tests can't judge furniture, door frames or how it looks. That is the hand test below.

## In game (by hand)

Seen 2026-10-22 with the dragon: riding through Home, its basement and the lab; mount only through the church door (the
cart and its corpse stayed at the door, "not the morgue"); the cart allowed into the morgue (the corpse stayed on the
cart and was taken off by hand); the cart in Home; sleeping with the mount parked inside (moved 0.5 off the furniture
it stood in), saved and loaded indoors (all back, the chest on the cart). Not yet by hand: the morgue with the cart
row off (the self-test covers it with the morgue's door note).

Turn on "Allow mounting indoors" (Mods -> GK2 Saddle Up -> Behaviour).

1. Ride (H) through a house door. You stay on; the mount is under you inside, on the floor, not floating or sunk.
   Walk around: furniture and walls, and the mount drawn in front of or behind things correctly.
2. Ride back out. You're still on, by the door outside.
3. Get off inside (H), then H again: the mount comes and you get on.
4. Drive the cart (G) through the door with the cart row off: the cart stays at the door outside and you ride on
   inside. Inside, G says "Not the cart indoors.". Back outside, G hitches the cart again.
5. Turn on "Allow mounting with the cart indoors". Drive through the door: the cart comes in. Turn round in a small
   room, drive past furniture, through the door back out. Note anywhere the cart sticks out through a wall.
6. The church and the morgue the same way. The morgue with a corpse on the cart: with the cart row on, you drive
   in and it stays on the cart; off, you get off inside carrying it, your mount in there with you, the cart at the
   door.
7. Save and load while riding indoors, and with the mount parked inside: it is where you left it.
8. Turn the first row off while riding inside: you get off at once. The cart row greys.
