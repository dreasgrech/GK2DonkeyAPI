# GK2 Saddle Up 0.12.0: what's new since 0.10.0

For players. GK2 Donkey API 0.40.0 is in the download. Your donkey, cart, colours, keys and saves carry over from
0.10.0.

## The mounts are here

- **The Mount Journal (L):** every mount you have, with its picture, a line about it, its speed and cargo, its
  abilities and its skins. Ride and Drive the cart call it from there; Customize opens its colours. Picking a mount
  in the list only shows it: Ride or Drive the cart makes it yours.
- **Saddle Up comes with two mounts:**
  - **The Donkey**: normal speed, a cart of 6 items.
  - **Gerry**: the game's talking skull, three times his size. He's fast and pulls a cart of 4, and he talks.
- **More mounts and skins are separate downloads**, each on its own: **GK2 Saddle Up: Dragon** (a dragon that
  flies), **GK2 Saddle Up: Critters** (the rat, the snail and the worm) and **GK2 Saddle Up: Donkey Skins** (two new
  skins for the donkey: **Hellfire**, with horns, a devil's tail, glowing eyes and lava cracks, and **Royal Parade**,
  a saddle cloth that follows your harness colour, and a crown). Other mods can add mounts and skins too.
- **Each mount has its own speed and its own load:** the faster it is, the less its cart holds. If you switch to a
  mount that pulls fewer items than the cart has on it, the cart is left standing with its load. Take the extra off,
  and it hitches again.
- **Everything is kept per mount:** its name, its colours, its babies, its sound volumes and its lantern. You set
  them in the Mount Journal, and you type its name there too.
- **Jump (Space):** every mount jumps; hold the key for a higher jump. Space is still your attack in a fight.
- **Call out (V):** your mount gives its cry when you ask: the donkey brays, Gerry talks. Its babies answer.

## Babies

- **They look like their parent**, and each one is a little different: its size, its shade of the colours, its
  temperament (lively ones roam, step faster and hop more; calm ones stay close) and its voice. Each baby is the same
  after every load.
- **They hop** now and then. When you jump, the babies following you jump too, each a moment after you.
- **They can follow you on foot** (new setting, on): off your mount, outdoors, they trot after you.
- **They answer their parent's call**, one after another, and loud enough to hear.
- **They get stuck less, and they never stop following.** A baby that falls far behind is brought to you.
- **They come through a door with the mount you ride.**
- **Standing mounts and babies make their sounds near you** (new setting, on): grazing, steps and calls, not only
  the mount you ride.

## New settings

- **Freeplay** (the last section, for players who don't want the mounts' differences):
  - **All mounts carry max cargo**: every cart holds 6 items, whatever mount pulls it.
  - **All mounts have the same speed**: every mount, the donkey too, goes at the speed you pick, from Very slow to
    Very fast. Normal is the donkey's.
  - Both are off by default and take effect at once, even while you ride.
- **Allow mounting indoors** and **Allow mounting with the cart indoors** (both off): ride into houses, the church
  and the morgue. With the cart not allowed, it waits at the door. A door with a loading screen still ends the ride.
- **Safe spot** (its own section): if you ever fall out of the world, you're put back on the last ground you stood
  on (on by default). There's also a button and a key (none by default) to do it by hand when you're stuck.
- **Three sound sliders per mount:** voices, steps and baby steps. Your old volume becomes the voices one.
- **New keys:** Mounts panel (L), Call out (V), Jump (Space).

## Changed and removed

- **The mounts set the speeds now:** the Riding speed and Cart speed settings are gone, so a Faster or Fastest you
  had picked no longer applies. The donkey is as fast as it always was.
- **Cart capacity is gone:** a cart holds what its mount carries (the donkey 6, Gerry 4).
- **The Effect section is gone.** Particle effects will come back on specific skins later.
- **Gone:** "Babies are the same mount and skin" (babies always look like their parent now), "Calling your mount
  leaves the cart parked" (it always does now) and "Bell sound when setting off".
- **The colours, the name, the babies, the volume and the lantern moved into the Mount Journal.** The settings
  page stays the same size however many mounts you install.
- **The bell only rings while you drive the cart.**
- **"Behaviour" is now "Mount Behaviour"**, and every setting's text is shorter and plainer.
- **Everything you had set carries over.** Retired settings leave your settings file by themselves.

## Fixes

- Binding a key in the Mods menu no longer presses it (binding G used to hitch the cart).
- The mod's keys no longer act while the Mount Journal is open.
- The cart no longer sinks into a hill behind the mount at the edge of a raised path.
- The mount's picture in the Mods menu sits in its row again (it slid up with GK2 Mod Framework 0.1.24), and it is
  always lit as at midday, never tinted by the hour, the room or the fog.
- The mount and cart kept with your save are never lost when something goes wrong: their file is kept or backed up
  instead of being overwritten.
- The keeper names your mount when it refuses something ("Bella pulls 4 items at most").
