# GK2 Saddle Up

Your own donkey and cart in Graveyard Keeper 2. Call it, ride it, drive the cart, haul logs, stone and corpses on it,
leave it standing with its load, and find it where you left it after a reload.

Install both DLLs: `GK2DonkeyAPI.dll` (the library it's built on, [GK2 Donkey API](../GK2DonkeyAPI/README.md)) and
`GK2SaddleUp.dll`, each in its own folder under `BepInEx\plugins` (the release zip has them so). Version 0.11.0, for GK2 Donkey API 0.31.0 or newer, and Steam game build 25910882 (game
1.010).

## Playing

| Key | On foot | Riding the donkey | Driving the cart |
|---|---|---|---|
| **H** | your donkey comes and you ride it; a parked cart stays where you left it (a setting) | get off | leave the cart standing, ride on |
| **G** | your donkey comes with its cart and you drive | the cart comes and is hitched | get off |
| **E** (the game's interact key) | at your donkey: ride or drive it; at the cart holding something: put it on the cart; at the cart's back with empty hands: take the top item | next to a free cart: take it | with nothing in front: set the top item down behind the cart |
| Bell key (B) | – | ring the bell | ring the bell |
| Send-away key (none by default) | your donkey, cart and foal go (the load is set down first) | the same | the same |

- **Your donkey stays in the world** where you got off, with its cart and its load. Walk up to it and press E, or
  press H or G anywhere: it comes to you.
- **It's kept with the save:** after loading, it's where you left it, the load on the cart.
- **The cart carries six items** in three layers of two (two or four with "Cart capacity"). Anything you pick up while driving goes onto it; piles, tables, graves and quest
  hand-ins take from it as if you held the item. On the donkey alone, your hands stay empty.
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
| 1. Keys | Ride, Drive, Bell, Send away | H, G, B, none | No hitch key: H and G switch between riding and driving (H while driving leaves the cart standing; G while riding brings your cart), and the interact key (E) hitches a cart you ride up to. |
| 2. Mount | Name | none | Shown when you walk up to it: "[E] Ride Bella". |
| | Mount | Donkey | What you ride. In this version: the donkey only; the other mounts (Dragon, Giant Rat, Giant Snail, Giant Worm, Gerry) are built and switched off until the next update, and a row under the Skin row says so. Its picture is at the top of the section. |
| | Skin | Default | How that mount is drawn. In this version: the donkey as the game draws it; the skins (Hellfire with its ring of fire, Royal Parade, skin packs from anyone) come in the next update. |
| | Babies | none | Up to 5 little ones of your mount that stay with it and follow it around. |
| | Babies come indoors | on | They come through doors with you into buildings, the church and dungeons, follow you inside and come back out with you. |
| | Babies are the same mount and skin | on | Off: they stay plain baby donkeys whatever yours is. |
| | Keep your mount with the save | on | Kept in a small file next to each save (`<save>.gk2donkeyapi.dat`), which Steam Cloud syncs along with your saves. |
| 3. Colours | Choose colours | | A button: closes the menu and opens the colour rows over the game (below). The picture of your mount and cart is under it, then the rows under two headings, "Mount colours" and "Cart colours". Kept per mount. |
| | Coat colour | Game default | The mount's body (the donkey's coat, and skins drawn in its colours): Brown, Dark, Cream, Black, or Custom. On another mount it reaches every colour its skin doesn't name as something else (the dragon's scales, the rat's fur); a colour row for a part the mount hasn't got is greyed. |
| | Harness colour | Game default | The mount's tack (the donkey's harness): Black, Red, Blue, Green, or Custom. |
| | Second tone | Game default | The mount's second body tone, where its look has one: the donkey's muzzle, the rat's tail, ears and nose, the snail's shell, the dragon's belly, Hellfire's lava. Pale, Rose, Brown, Green, Blue, Dark, or Custom. Not shown for a mount without one (the worm); greyed in the colours window over the game. |
| | Paint | Game default | Your cart's body and shafts: Dark wood, Red, Green, Blue, or Custom. Another cart you hitch in the world keeps its own look. |
| | Wheel colour | Game default | The wood of the wheels: Dark wood, Red, Green, Blue, Black, or Custom. |
| | Wheel hub colour | Game default | The iron in the middle of the wheels: Black, Brass, Silver, or Custom. |
| | Lantern light | Warm (game default) | White, Green, Blue, or Custom. |
| 4. Effect | Effect | As the mount | A particle effect on your mount. In this version there is none to pick (the donkey comes with none); Hellfire (the game's lamp fire in a ring, with sparks) and effects from mods come in the next update, and a row at the top of the section says so. |
| | Effect shown | Always | Never, At night, Always. |
| | Leaves a trail, Round the cart too, Shows in fights, Babies have it too | on, off, on, off | How the effect behaves. |
| 5. Behaviour | Riding speed | Normal | Riding your mount: Normal (x2 walking speed), Faster (x2.25), Fastest (x2.5). |
| | Cart speed | Normal | Driving the cart: Normal (x1.5), Faster (x1.75), Fastest (x2). |
| | Say why mounting is refused | on | The keeper's thought bubble. |
| | Shuffles while standing | on | Left without its cart, your mount takes a few slow steps now and then. |
| 6. Cart | Cart capacity | 6 items | How many items the cart carries: 6 (three layers of two), 4 (two layers) or 2 like the game's own delivery cart. Piles, tables and stations take from all of them; a chest aboard still takes the whole cart. Lowering it never drops anything: a fuller cart stays full until unloaded. |
| | Zombies ride on the cart | on | Pick up a zombie while driving and it lies on the cart, as many as the cart holds; hand it to its work straight from the cart. Off: the cart refuses zombies. |
| | A storage chest rides on the cart | on | Drive up to a plain storage chest and press the action key: it rides on the cart with everything inside, alone. The interact key with nothing in front sets it down. Off: chests stay where they are. |
| | Carry the corpse into the morgue | on | Driving the cart through the morgue door with a corpse on it: you get off at the door as always and the cart stays outside with its load, but you walk in carrying the corpse. Off: it stays on the cart. |
| | Donkey leaves corpses at the crossroads | **off** | The game's own delivery donkey leaves the bodies it brings at the crossroads by the nun, where it appears: it walks out from behind its tree, the bodies drop beside it with a notification, and it walks back and vanishes. You carry them to the morgue, or cart them. Bodies on the ground count toward the morgue's capacity, and one left out of sight for about 75 minutes of play turns into a grave mound (the game's rules; the API's cfg can switch the rot off, change the spots and the text). Off: the game's own delivery, nothing of it hooked. |
| | Calling your mount leaves the cart parked | on | H while your mount stands somewhere with the cart: it comes alone and the cart stays where you left it, with its load. Off: both come and the cart is left standing next to you. |
| | Lantern | on | The lantern on its pole. |
| | Lantern lit by day | on | Off: it goes dark by day, like your hand lantern. |
| 7. Sound | Bell sound when setting off | off | When the cart sets off with a load after standing. |
| | Bray sounds | on | Now and then, and when you get on. |
| | Mount and cart sounds | 100% | 0 to 150%, on top of the game's SFX volume. |

**Choosing colours:**
- **A picture** of your mount and cart in the chosen colours, walking on the spot: at the top of the Mount
  section and under the Choose colours button in the Colours section, and above the Mods window too when the screen
  has room for it (1440p and up). Above the Settings window without the Mods menu. It needs no mount in
  the game.
- **Custom** shows two more rows for that part: a colour by name (24 of them: Chestnut, Sand, Cream, Snow, Ash, Slate,
  Charcoal, Black, Rust, Brick, Wine, Coral, Rose, Plum, Lavender, Navy, Sky, Teal, Mint, Moss, Forest, Olive,
  Mustard, Gold) and its lightness. The picture shows the result.
- **Choose colours** (in Colours) closes the menu and opens the colour rows at the bottom of the screen, over the game:
  your mount's colours on the left, the cart's on the right, the Mount, Skin and Effect rows with them. You see your
  real donkey, cart and lantern change as you click. **Done** keeps them, **Cancel** puts them back.
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
