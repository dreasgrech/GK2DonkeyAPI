# GK2 Saddle Up

Your own donkey and cart in Graveyard Keeper 2. Call it, ride it, drive the cart, haul logs, stone and corpses on it,
leave it standing with its load, and find it where you left it after a reload.

Install both DLLs: `GK2DonkeyAPI.dll` (the library it's built on, [GK2 Donkey API](../GK2DonkeyAPI/README.md)) and
`GK2SaddleUp.dll`, each in its own folder under `BepInEx\plugins` (the release zip has them so). Version 0.12.0, for GK2 Donkey API 0.38.0 or newer, and Steam game build 25910882 (game
1.010). Five more mounts come inside it (a dragon, a giant rat, a giant snail, a giant worm, Gerry: mount packs in
`assets/mounts/saddleup.<mount>/`, each a `mount.cfg` with its own look in `skins/default/`, registered with the API at start the way
any mod ships a mount), switched off until the next update.

## Playing

| Key | On foot | Riding the donkey | Driving the cart |
|---|---|---|---|
| **H** | your donkey comes and you ride it; a parked cart stays where you left it (a setting) | get off | leave the cart standing, ride on |
| **G** | your donkey comes with its cart and you drive | the cart comes and is hitched | get off |
| **E** (the game's interact key) | at your donkey: ride or drive it; at the cart holding something: put it on the cart; at the cart's back with empty hands: take the top item | next to a free cart: take it | with nothing in front: set the top item down behind the cart |
| Bell key (B) | – | ring the bell | ring the bell |
| Call out key (V) | – | your mount calls out (the donkey brays, the dragon roars, the rat squeaks, Gerry talks); its babies answer | the same |
| Send-away key (none by default) | your donkey, cart and foal go (the load is set down first) | the same | the same |

- **Your donkey stays in the world** where you got off, with its cart and its load. Walk up to it and press E, or
  press H or G anywhere: it comes to you.
- **It's kept with the save:** after loading, it's where you left it, the load on the cart.
- **The cart carries six items** in three layers of two (two or four with "Cart capacity"). Anything you pick up while driving goes onto it; piles, tables, graves and quest
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
  settings and Saddle Up's pages. To set a key, press either arrow of its row, then the key (Backspace: no key; Esc: cancel). The
  name shows there read-only; you set it in the cfg.
- **Always:** in `BepInEx\config\dreas.gk2.saddleup.cfg` (with the game closed).

| Section (in the cfg) | Setting | Default | |
|---|---|---|---|
| 1. Keys | Ride, Drive, Bell, Call out, Send away, Mounts panel, Jump, Fly, Climb, Descend | H, G, B, V, none, none, Space, Space, Left Shift, Left Ctrl | Call out: your mount's own cry on demand, and its babies answer (the cries now and then still come by themselves; Sound's Bray row switches those off, not the key). | Jump and Fly on one key: a press jumps (hold for a higher jump), a second press in the air takes off a mount that flies (the dragon), a press while flying lands; Climb and Descend are held in the air (a lone modifier can't be set in the game's Settings window, only in the Mods menu or the cfg). No hitch key: H and G switch between riding and driving (H while driving leaves the cart standing; G while riding brings your cart), and the interact key (E) hitches a cart you ride up to. |
| 2. Mount | Name | none | Shown when you walk up to it: "[E] Ride Bella". |
| | Mounts | | A button: closes the menu and opens the **Mounts panel** over the game: every mount you can ride on the left, the chosen one drawn on the right with what it is and does, and its skin and colour rows under it. Done keeps your choices, Cancel puts them back. The Mount, Skin and colour rows below are chosen there, not in this menu (0.12). |
| | Mount | Donkey | (in the Mounts panel) What you ride. In this version: the donkey only; the other mounts (Saddle Up's own Dragon, Giant Rat, Giant Snail, Giant Worm and Gerry; any mod's or skin pack's) are switched off until the next update, and a row in the section says so. Its picture is at the top of the section. |
| | Skin | Default | (in the Mounts panel) How that mount is drawn. In this version: the donkey as the game draws it; the skins (Saddle Up's own Hellfire and Royal Parade, skin packs from anyone) come in the next update. |
| | Keep your mount with the save | on | Kept in a small file next to each save (`<save>.gk2donkeyapi.dat`), which Steam Cloud syncs along with your saves. |
| 3. Babies | Babies | none | Up to 5 little ones of your mount that stay with it and follow it around. |
| | Babies follow you on foot | on | Off your mount, outdoors, they come with you instead of staying by it: they trot after you, and catch up out of sight when you're far ahead. When you get on a mount they go back to yours. Into buildings only with Babies come indoors; without it they wait with your mount and come again when you're out. |
| | Babies come indoors | on | They come through doors with you into buildings, the church and dungeons, follow you inside and come back out with you. |
| | Babies are the same mount and skin | on | Off: they stay plain baby donkeys whatever yours is. |
| 4. Colours | (the picture) | | The picture of your mount and cart, and a line saying where the colour rows went: the rows below are in the Mounts panel (since 0.12; before, a "Choose colours" bar over the game), under two headings, "Mount colours" and "Cart colours". Kept per mount. |
| | Coat colour | Game default | (in the Mounts panel) The mount's body (the donkey's coat, and skins drawn in its colours): Brown, Dark, Cream, Black, or Custom. On another mount it reaches every colour its skin doesn't name as something else (the dragon's scales, the rat's fur); a colour row for a part the mount hasn't got is greyed. |
| | Harness colour | Game default | (in the Mounts panel) The mount's tack (the donkey's harness): Black, Red, Blue, Green, or Custom. |
| | Second tone | Game default | (in the Mounts panel) The mount's second body tone, where its look has one: the donkey's muzzle, the rat's tail, ears and nose, the snail's shell, the dragon's belly, Hellfire's lava. Pale, Rose, Brown, Green, Blue, Dark, or Custom. Not shown for a mount without one (the worm); greyed in the Mounts panel. |
| | Paint | Game default | (in the Mounts panel) Your cart's body and shafts: Dark wood, Red, Green, Blue, or Custom. Another cart you hitch in the world keeps its own look. |
| | Wheel colour | Game default | (in the Mounts panel) The wood of the wheels: Dark wood, Red, Green, Blue, Black, or Custom. |
| | Wheel hub colour | Game default | (in the Mounts panel) The iron in the middle of the wheels: Black, Brass, Silver, or Custom. |
| | Lantern light | Warm (game default) | (in the Mounts panel) White, Green, Blue, or Custom. |
| 5. Behaviour | *(speeds)* | | Each mount has its own speed, and the cart slows every mount alike (the Mounts panel shows it: the donkey Normal, the giant rat Very fast, Gerry Fast, the dragon and the worm Slow, the snail Very slow; and its cargo, 6 items for every mount for now). The Riding speed and Cart speed rows were here until 0.12. |
| | Say why mounting is refused | on | The keeper's thought bubble. |
| | Shuffles while standing | on | Left without its cart, your mount takes a few slow steps now and then. |
| | Allow mounting indoors | off | Ride into houses, the church and the morgue, and get on inside. A door with a loading screen still ends the ride. Off: you get off at the door and can't mount inside. |
| | Allow mounting with the cart indoors | off | (only with the row above on) The cart comes inside too; it can get caught on furniture and door frames. Off: driving the cart through a door, the cart stays at the door with its load and you ride on inside (at the morgue you get off inside carrying the corpse, your mount with you); inside you can ride, not drive. |
| 6. Cart | Cart capacity | 6 items | How many items the cart carries: 6 (three layers of two), 4 (two layers) or 2 like the game's own delivery cart. Piles, tables and stations take from all of them; a chest aboard still takes the whole cart. Lowering it never drops anything: a fuller cart stays full until unloaded. |
| | Zombies ride on the cart | on | Pick up a zombie while driving and it lies on the cart, as many as the cart holds; hand it to its work straight from the cart. Off: the cart refuses zombies. |
| | A storage chest rides on the cart | on | Drive up to a plain storage chest and press the action key: it rides on the cart with everything inside, alone. The interact key with nothing in front sets it down. Off: chests stay where they are. |
| | Carry the corpse into the morgue | on | Driving the cart through the morgue door with a corpse on it: you get off at the door as always and the cart stays outside with its load, but you walk in carrying the corpse (riding indoors allowed and the cart not: your mount comes in with you). Off: it stays on the cart. |
| | Donkey leaves corpses at the crossroads | **off** | The game's own delivery donkey leaves the bodies it brings at the crossroads by the nun, where it appears: it walks out from behind its tree, the bodies drop beside it with a notification, and it walks back and vanishes. You carry them to the morgue, or cart them. Bodies on the ground count toward the morgue's capacity, and one left out of sight for about 75 minutes of play turns into a grave mound (the game's rules; the API's cfg can switch the rot off, change the spots and the text). Off: the game's own delivery, nothing of it hooked. |
| | Calling your mount leaves the cart parked | on | H while your mount stands somewhere with the cart: it comes alone and the cart stays where you left it, with its load. Off: both come and the cart is left standing next to you. |
| | Lantern | on | The lantern on its pole. |
| | Lantern lit by day | on | Off: it goes dark by day, like your hand lantern. |
| 7. Sound | Bell sound when setting off | off | When the cart sets off with a load after standing. |
| | Bray sounds | on | Now and then, and when you get on. |
| | Mount and cart sounds | 100% | 0 to 150%, on top of the game's SFX volume. |

**Choosing colours:**
- **A picture** of your mount and cart in the chosen colours, walking on the spot: at the top of the Mount
  section and in the Colours section, and above the Mods window too when the screen
  has room for it (1440p and up). Above the Settings window without the Mods menu. It needs no mount in
  the game.
- **Custom** shows two more rows for that part: a colour by name (24 of them: Chestnut, Sand, Cream, Snow, Ash, Slate,
  Charcoal, Black, Rust, Brick, Wine, Coral, Rose, Plum, Lavender, Navy, Sky, Teal, Mint, Moss, Forest, Olive,
  Mustard, Gold) and its lightness. The picture shows the result.
- **The Mounts panel** (the Mount section's Mounts button, or the Mounts panel key) closes the menu and opens over the
  game: every mount you can ride listed on the left, the chosen one drawn on the right with what it is and does, and
  under it its Skin and colour rows (your mount's colours and the cart's under their two headings). You see
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
