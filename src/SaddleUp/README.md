# GK2 Saddle Up!

Your own donkey and cart in Graveyard Keeper 2. Call it, ride it, drive the cart, haul logs, stone and corpses on it,
leave it standing with its load, and find it where you left it after a reload.

Install both DLLs: `GK2DonkeyAPI.dll` (the library it's built on, [GK2 Donkey API](../GK2DonkeyAPI/README.md)) and
`SaddleUp.dll`. Version 0.1.0, for GK2 Donkey API 0.1.0 (API 0.14.0 or newer) and Steam game build 25676698 (game
1.008).

## Playing

| Key | On foot | Riding the donkey | Driving the cart |
|---|---|---|---|
| **H** | your donkey comes and you ride it | get off | leave the cart standing, ride on |
| **G** | your donkey comes with its cart and you drive | the cart comes and is hitched | get off |
| **J** | – | take the cart in reach | leave the cart standing |
| **E** (the game's interact key) | at your donkey: ride or drive it; at the cart holding something: put it on the cart; at the cart's back with empty hands: take the top item | next to a free cart: take it | with nothing in front: set the top item down behind the cart |
| Bell key (none by default) | – | ring the bell | ring the bell |
| Send-away key (none by default) | your donkey, cart and foal go (the load is set down first) | the same | the same |

- **Your donkey stays in the world** where you got off, with its cart and its load. Walk up to it and press E, or
  press H or G anywhere: it comes to you.
- **It's kept with the save:** after loading, it's where you left it, the load on the cart.
- **The cart carries two items.** Anything you pick up while driving goes onto it; piles, tables, graves and quest
  hand-ins take from it as if you held the item. On the donkey alone, your hands stay empty.
- **You're faster while mounted**, and you get off by yourself for work, ladders, building, fishing, fighting and
  sleep, and at building doors. Chests and the inventory keep you mounted.

## Settings

All of these apply at once:
- **With [GK2 Mod Framework](https://www.nexusmods.com/graveyardkeeper2/mods/42) installed** (optional): in the
  game's **Mods** menu, under GK2 Saddle Up!. Its sections are numbered (1. Keys first) to keep them in this order.
- **Without it:** in the game's own **Settings** window. The "Showing" row at the top switches between the game's
  settings and Saddle Up's pages. The keys and the name show there read-only; you set those in the cfg.
- **Always:** in `BepInEx\config\dreas.gk2.saddleup.cfg` (with the game closed).

| Section (in the cfg) | Setting | Default | |
|---|---|---|---|
| 1. Keys | Ride, Drive, Hitch, Bell, Send away | H, G, J, none, none | The hitch key is a single key (modifiers are ignored). |
| 2. Behaviour | Riding speed | Normal | Normal, Faster, Fastest. |
| | Get off at building doors | on | Off: you ride into houses, and the cart goes through the furniture. |
| | Say why mounting is refused | on | The keeper's thought bubble. |
| | Shuffles while standing | on | Left without its cart, your donkey takes a few slow steps now and then. |
| | Keep the donkey with the save | on | Kept in a small file next to each save (`<save>.gk2donkeyapi.dat`), which Steam Cloud syncs along with your saves. |
| 3. Donkey | Name | none | Shown when you walk up to it: "[E] Ride Bella". |
| | Coat | the game's | Brown, Dark, Cream, Black, or Custom. |
| | Harness | the game's | Black, Red, Blue, Green, or Custom. |
| | Brays | on | |
| | A foal | off | A small donkey that stays with yours and follows it around. |
| 4. Cart | Paint | the game's | Dark wood, Red, Green, Blue, or Custom. |
| | Lantern | on | The lantern on its pole. |
| | Lantern lit by day | on | Off: it goes dark by day, like your hand lantern. |
| | Lantern light | warm (the game's) | White, Green, Blue, or Custom. |
| | Bell when setting off | on | When the cart sets off with a load after standing. |
| 5. Sound | Donkey and cart sounds | 100% | 0 to 150%, on top of the game's SFX volume. |

**Choosing colours:**
- **A picture** of your donkey in the chosen colours sits next to the settings: at the top right in the Mods menu,
  beside the Settings window without it. It shows your donkey as last seen in the game, so call it once if it isn't
  there yet.
- **Custom** shows two more rows for that part: a colour by name (24 of them: Chestnut, Sand, Cream, Snow, Ash, Slate,
  Charcoal, Black, Rust, Brick, Wine, Coral, Rose, Plum, Lavender, Navy, Sky, Teal, Mint, Moss, Forest, Olive,
  Mustard, Gold) and its lightness, with a swatch of the result.
- **Choose colours** (in Donkey) closes the menu and opens the colour rows at the bottom of the screen, over the game.
  You see your real donkey, cart and lantern change as you click. **Done** keeps them, **Cancel** puts them back.
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
