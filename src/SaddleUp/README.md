# GK2 Saddle Up

Your own donkey and cart in Graveyard Keeper 2. Call it, ride it, drive the cart, haul logs, stone and corpses on it,
leave it standing with its load, and find it where you left it after a reload.

Install both DLLs: `GK2DonkeyAPI.dll` (the library it's built on, [GK2 Donkey API](../GK2DonkeyAPI/README.md)) and
`GK2SaddleUp.dll`, each in its own folder under `BepInEx\plugins` (the release zip has them so; on game 1.011 or newer the
zip can go into `Mods\Code\GK2SaddleUp\` in the save data folder instead, and the game's Mods window lists and installs it). Version 0.13.2, for GK2 Donkey API 0.41.2 or newer, and Steam game build 26297960 (game
1.011).

**Mounts and skins.** Saddle Up itself brings the donkey (the game's own look) and Gerry, the game's talking skull
(a pack in `assets/mounts/saddleup.gerry/`, registered with the API at start the way any mod ships a mount). More
mounts and skins are downloads of their own, each a folder for `BepInEx\plugins\`:
- **GK2 Saddle Up: Dragon** (`GK2SaddleUpDragonsMountPack\GK2SaddleUpDragonsMountPack.dll`): the dragon, which flies;
- **GK2 Saddle Up: Critters** (`GK2SaddleUpCrittersMountPack\GK2SaddleUpCrittersMountPack.dll`): the rat, the snail and the worm;
- **GK2 Saddle Up: Donkey Skins** (`GK2SaddleUpDonkeySkinPack\GK2SaddleUpDonkeySkinPack.dll`): the donkey's skins Hellfire
  and Royal Parade.

| Mount | Download | Riding | Cart |
|---|---|---|---|
| Donkey | GK2 Saddle Up | Normal | 6 items |
| Gerry | GK2 Saddle Up | Fast | 4 items |
| Rat | GK2 Saddle Up: Critters | Very fast | 2 items |
| Worm | GK2 Saddle Up: Critters | Slow | 4 items |
| Snail | GK2 Saddle Up: Critters | Very slow | 6 items |
| Dragon | GK2 Saddle Up: Dragon | Slow | no cart (it flies) |

Each mount's cart carries its own number of items, the table above. Switching to a mount that carries less never
drops what the cart already holds. The donkey's skins (GK2 Saddle Up: Donkey Skins): Hellfire (horns, a devil's tail, glowing eyes, lava cracks; its ring
of fire comes in a later update) and Royal Parade (a saddle cloth and a crown; the cloth follows your harness colour).

## Playing

| Key | On foot | Riding the donkey | Driving the cart |
|---|---|---|---|
| **H** | your donkey comes and you ride it; a parked cart stays where you left it (a setting) | get off | leave the cart standing, ride on |
| **G** | your donkey comes with its cart and you drive (the dragon: it says it can't pull the cart) | the cart comes and is hitched | get off |
| **E** (the game's interact key) | at your donkey: ride or drive it; at the cart holding something: put it on the cart; at the cart's back with empty hands: take the top item | next to a free cart: take it | with nothing in front: set the top item down behind the cart |
| Bell key (B) | – | – | ring the bell (the bell is the cart's) |
| Call out key (V) | – | your mount calls out (the donkey brays, the dragon roars, the rat squeaks, Gerry talks); its babies answer | the same |
| Send-away key (none by default) | your mount, its cart and its babies go (the load is set down first) | the same | the same |
| Back to the last safe spot (none by default) | puts you back on the last ground you stood on in this scene | the same | the same |

- **Your donkey stays in the world** where you got off, with its cart and its load. Walk up to it and press E, or
  press H or G anywhere: it comes to you.
- **It's kept with the save:** after loading, it's where you left it, the load on the cart.
- **The cart carries the mount's own number of items**, the table above. The donkey's carries six, in three layers
  of two. Anything you pick up while driving goes onto it; piles, tables, graves and quest
  hand-ins take from it as if you held the item. On the donkey alone, your hands stay empty.
- **The dragon flies** (riding it plain, outdoors): Space jumps, Space again in the air takes off, Left Shift climbs,
  Left Ctrl descends, Space while flying lands. Over water, fences, cliffs and roofs; it comes down only where you can
  stand ("Nowhere to land here." while it can't), and a save, H or a door lands it first. Every mount jumps with Space.
  Not in a fight, where Space is your attack.
- **You're faster while mounted**, and you get off by yourself for work, ladders, building, fishing and sleep, and
  at building doors. Chests and the inventory keep you mounted. **Fights are ridden:** in your armour you swing the
  sword and shoot the bow from the donkey, aim up and down, and the donkey backs away with you while you aim (half
  speed, the game's own). The cart stays outside a fight: it is left standing when the armour goes on.

## Settings

All of these apply at once:
- **With [GK2 Mod Framework](https://www.nexusmods.com/graveyardkeeper2/mods/42) installed** (optional): in the
  game's **Mods** menu, under GK2 Saddle Up. Its sections are numbered (1. Keys first) to keep them in this order.
- **Without it:** in the game's own **Settings** window. The "Showing" row at the top switches between the game's
  settings and Saddle Up's pages. To set a key, press either arrow of its row, then the key (Backspace: no key; Esc: cancel). A lone Shift or Ctrl can't be set there, only in the Mods menu or the cfg.
- **In the Mounts panel** (L, the Mounts panel key, or the Mount section's Mounts button): everything kept per mount,
  marked "(in the Mounts panel)" below.
- **Always:** in `BepInEx\config\dreas.gk2.saddleup.cfg` (with the game closed).

| Section (in the cfg) | Setting | Default | |
|---|---|---|---|
| 1. Keys | Mounts panel | L | Opens the Mounts panel. Pick your mount there and set its name, skin, babies and colours. |
| | Ride your mount | H | Calls your mount and gets you on. Press it again to get off. In the cart, it leaves the cart standing and you ride on. |
| | Drive the cart | G | Calls your mount with its cart so you can drive. Press it again to get off. While riding, it brings the cart. |
| | Ring the bell | B | Rings the bell while you drive the cart. |
| | Call out | V | Your mount calls out while you ride, and its babies answer. |
| | Send your mount away | none | Sends your mount, its cart and its babies away. The cart's load is put down first. |
| | Jump | Space | Your mount jumps while you ride. Hold the key to jump higher. In a fight, Space attacks instead, so set another key if you want to jump while fighting. |
| | Fly | Space | Takes off on a mount that can fly. Press it again while flying to land. If it shares the Jump key, jump first and press it in the air to take off. In a fight, Space attacks instead. |
| | Climb (hold) | Left Shift | Hold it while flying to go up. |
| | Descend (hold) | Left Ctrl | Hold it while flying to go down. You land when you reach the ground. |
| 2. Mount | Your mount (the picture) | | Your mount, drawn in its colours. |
| | Mounts | | A button. Opens the Mounts panel. Pick your mount there and set its name, skin, babies and colours. Done keeps your choices and Cancel puts them back. |
| | Mount | Donkey | (in the Mounts panel) The mount you ride. Saddle Up has the donkey and Gerry. Packs and other mods add more. |
| | Skin | Default | (in the Mounts panel) How your mount looks. With GK2 Saddle Up: Donkey Skins the donkey also has Hellfire and Royal Parade. |
| | Name | none | (in the Mounts panel) Shown when you walk up to your mount, as in "Ride Bella". |
| | Keep your mount with the save | on | After you load a game, your mount and cart are where you left them, with their load. They are kept in a small file next to each save, which Steam Cloud syncs. |
| | Babies | none | (in the Mounts panel) How many little ones follow your mount around, up to 5. They always look like their parent. Only for a mount that has babies. |
| | Babies follow you on foot | on | (in the Mounts panel) When you get off, the babies follow you instead of staying with your mount. |
| | Babies come indoors | on | (in the Mounts panel) The babies follow you into buildings and back out. |
| | Voices volume | 100% | (in the Mounts panel) How loud your mount's calls are, and its babies' answers. 0 to 150%. |
| | Steps volume | 100% | (in the Mounts panel) How loud its steps are, with the cart, the bell and its wings. |
| | Baby steps volume | 100% | (in the Mounts panel) How loud its babies' steps are. Only for a mount with babies. |
| 3. Colours | | | All of these are in the Mounts panel's colours window and kept per mount. A mount that pulls no cart (the dragon) has no cart colours. |
| | *Mount colours:* Coat colour | Game default | (in the Mounts panel) The colour of your mount's body. Brown, Dark, Cream, Black or Custom. |
| | Harness colour | Game default | (in the Mounts panel) The colour of your mount's harness. Black, Red, Blue, Green or Custom. Mounts without one ignore it. |
| | Second tone | Game default | (in the Mounts panel) Your mount's second colour, such as the donkey's muzzle or the dragon's belly. Pale, Rose, Brown, Green, Blue, Dark or Custom. Mounts without one ignore it. |
| | *Cart colours:* Paint | Game default | (in the Mounts panel) The colour of the cart. Dark wood, Red, Green, Blue or Custom. |
| | Wheel colour | Game default | (in the Mounts panel) The colour of the wheels. Dark wood, Red, Green, Blue, Black or Custom. |
| | Wheel hub colour | Game default | (in the Mounts panel) The colour of the metal in the middle of the wheels. Black, Brass, Silver or Custom. |
| | Lantern light | Warm (game default) | (in the Mounts panel) The colour of the lantern's light. White, Green, Blue or Custom. |
| | Lantern | on | (in the Mounts panel) Shows the lantern on its pole. |
| | Lantern lit by day | on | (in the Mounts panel) Keeps the lantern lit in daylight. Turn it off to light it only at night. |
| 4. Mount Behaviour | Say why you can't get on | on | The keeper says why in a thought bubble. |
| | Shuffles while standing | on | Your mount takes a few slow steps now and then while it waits without its cart. |
| | Allow mounting indoors | off | Ride into houses, the church and the morgue. Doors with a loading screen still end the ride. |
| | Allow mounting with the cart indoors | off | The cart comes inside too. It has no collider indoors, so it rolls through furniture and door frames rather than getting stuck; it only looks odd. When this is off, the cart waits at the door. Only with the row above on. |
| 5. Cart | Zombies ride on the cart | on | Zombies you pick up while driving lie on the cart. You can hand them to their work from there. |
| | A storage chest rides on the cart | on | Drive up to a storage chest and press the action key to load it with everything inside. Press interact with nothing in front of you to set it down. |
| | Carry the corpse into the morgue | on | Drive through the morgue door with a corpse on the cart and you walk in carrying it. The cart waits outside. |
| | Donkey leaves corpses at the crossroads | **off** | The game's delivery donkey leaves new bodies at the crossroads instead of in front of the morgue. You bring them in yourself. |
| 6. Sound | Mount calls | on | Your mount calls out now and then, and when you get on. |
| | Standing mounts and babies sound | on | Parked mounts and babies near you make their sounds too. When this is off, you only hear the mount you ride. |
| 7. Safe spot | Bring me back if I fall out of the world | on | If you fall out of the world, you are put back on the last ground you stood on. |
| | Back to the last safe spot (key) | none | Puts you back on the last ground you stood on. Use it if you get stuck. |
| | Back to the last safe spot (button) | | Puts you back on the last ground you stood on, right now. |
| 8. Freeplay | All mounts carry max cargo | off | Every mount that pulls a cart carries 6 items, whatever its own cargo. |
| | All mounts have the same speed | off | Every mount, the donkey too, goes at the speed you pick below. With the cart it is slower, as always. |
| | Speed of every mount (shown with the row above on) | Normal | Very slow, Slow, Normal, Fast or Very fast, the Mounts panel's words. Normal is the donkey's speed. |

**Choosing colours:**
- **A picture** of your mount and cart in the chosen colours, walking on the spot: at the top of the Mount
  section, in the Mounts panel and its colours window, and above the Mods window too when the screen
  has room for it (1440p and up). Above the Settings window without the Mods menu. It needs no mount in
  the game.
- **Custom** shows two more rows for that part: a colour by name (24 of them: Chestnut, Sand, Cream, Snow, Ash, Slate,
  Charcoal, Black, Rust, Brick, Wine, Coral, Rose, Plum, Lavender, Navy, Sky, Teal, Mint, Moss, Forest, Olive,
  Mustard, Gold) and its lightness. The picture shows the result.
- **The Mounts panel** (the Mount section's Mounts button, or the Mounts panel key) closes the menu and opens over the
  game: every mount you can ride listed on the left, the chosen one drawn on the right with what it is and does, and
  under it its journal page (Name, Skin, the babies, Voices, Steps and Baby steps volume). **Customize** opens the colours window (your
  mount's colours and the cart's under their two headings, the lantern with the cart's). You see
  your real donkey, cart and lantern change as you click. **Done** keeps them, **Cancel** puts them back.
  Only in a game. Everything
else (the seat, the cargo on the cart, the lantern's place, the sounds themselves) is tuned and fixed; mod authors
can change it in GK2 Donkey API's dev panel.

## How it's built

`Plugin.cs` only, and most of it is the list of settings. It defines the settings (`Mounts.SettingsPage`) and maps
the keys to `Mounts.Companion`, which does the rest: calling the donkey, riding, hitching, the looks, the foal and
keeping it with the save. Everything goes through the public API ([`docs/api.md`](../../docs/api.md)), so it's also
the example for mod authors. It declares a hard dependency on `dreas.gk2.donkeyapi` and checks `Mounts.ApiVersion`.

## Tested in game

- **2026-09-30:**
  - H and G rode, switched style both ways and got off;
  - a building door dismounted;
  - without Saddle Up, GK2 Donkey API alone did nothing on any key.
- **2026-10-02:**
  - after the rename, the kept donkey and cart came back with their load, owned by Saddle Up.
- **Not yet tested (0.14.0):**
  - the settings, both in the Mods menu and in the game's Settings window;
  - the looks;
  - the foal;
  - the name;
  - the speed steps.
