# GK2 Donkey API

A BepInEx library for Graveyard Keeper 2 that gives the game a rideable donkey: ridden on its back (**Ride**), or
pulling its cart (**Cart**), with cargo, a lantern, sounds and colours. Donkeys and carts can also stand in the world,
any number of them, and be kept with the save.

**On its own it does nothing.** A mod built on it decides when the keeper may mount and what the ride can do:
- **For players:** **GK2 Saddle Up!** ([`src/SaddleUp`](../SaddleUp/README.md)) is the
  playable version. Install both DLLs.
- **For mod authors:** the C# API is in [`docs/api.md`](../../docs/api.md). Tie the mount into the
  story, gate it on the tech tree, quests or story flags, spawn herds, keep your own data on each donkey.

Version 0.28.0 (one number: the mod's and the API's, `Mounts.ApiVersion`), built and audited against Steam game build 25752683, game 1.009 hotfix (Assembly-CSharp
MVID `44edf1a9-…`, LazyBearTechnology `02955186-…`; re-audit in
[`docs/game-update-25731225.md`](../../docs/game-update-25731225.md)).

- **Every setting and how it works inside the game:** [`docs/reference.md`](../../docs/reference.md).
- **Design:** [`docs/api-design.md`](../../docs/api-design.md), [`FEASIBILITY.md`](FEASIBILITY.md), and
  the runtime facts it rests on in [`PROBE-FINDINGS.md`](PROBE-FINDINGS.md).

The game has no mount of its own. The donkey you ride borrows only the corpse-delivery donkey's *looks*; the real
donkey NPC and its schedule are untouched.

## Features

Every feature the library has, grouped by area. **Reached by** says how a player or a mod gets at it:
- **cfg:** a setting in `BepInEx\config\dreas.gk2.donkeyapi.cfg` (the key names are in the reference);
- **API:** a call in [`docs/api.md`](../../docs/api.md);
- **panel:** the dev panel (for mod authors: the empty marker file `BepInEx\config\dreas.gk2.dev`, then
  `Debug.TuningWindow = true`; without the marker a player's cfg has no `[Debug]` keys);
- **always:** built in, no switch.

**Status** is the state of in-game testing as of 2026-10-02: **tested**; **partly** (some of its test list passed);
**untested** (built, not yet checked in game).

### 1. Riding

| Feature | Reached by | Status |
|---|---|---|
| **Two styles:** Ride (on the donkey's back) and Cart (the donkey pulls its cart, the keeper rides at the front). | API | tested |
| **Mount, dismount, switch style in place.** Switching asks the new style's rules first; a refusal leaves the ride as it was. | API | tested |
| **Faster while mounted:** a speed multiplier per style (1.75 Ride, 1.5 Cart), measured to scale exactly. | cfg, API | tested |
| **Left/right only:** the keeper always faces left or right while mounted, like the donkey (it has no up/down art). The donkey turns in the same frame as the keeper. | always | tested |
| **Rider pose:** reins-holding pose, idle, walk or focused walk; frozen while standing, with an occasional fidget so the rider doesn't look like a still image; pose speed follows the donkey's walk. | cfg, API | tested |
| **Seat and position:** depth, centring under the rider, rider and torso offsets, seat height (fixed or computed from the sprites), a 1 px bob with the donkey's back. | cfg, API | tested |
| **Rider's legs:** drawn in front of the donkey, re-sliced from the keeper's own outfit (or a drawn leg), following the pose. | cfg, API | tested |
| **Ground following:** the donkey stands on the ground under itself, not at the keeper's height; donkey and keeper read the same ground source, so bridges over the navmesh don't flicker. | cfg, API | tested |
| **Automatic dismount** for anything that takes the keeper out of free walking: work, ladders, building, planting, fishing, weapons, sleep, death, cutscenes, co-op. Chests and the inventory keep him mounted. | always | tested |
| **Indoors:** entering a building, church or dungeon ends the ride, and mounting inside is refused (the 3D cart would cut through furniture). | cfg, API | tested |
| **Through scene doors:** optionally keep riding through a door that loads another area; the donkey is summoned again there. | cfg, API | untested |
| **Talking while mounted:** a plain NPC conversation keeps the keeper mounted; a scripted walk, a staged pose or the letterbox ends the ride. | always | untested |
| **Thought bubbles** saying why a mount was refused ("I'm busy.", "Not indoors.", a mod's own reason). | cfg, API | tested |
| **Co-op refused** (not supported). | always | tested |

### 2. The cart and its cargo

| Feature | Reached by | Status |
|---|---|---|
| **The cart is the keeper's hands:** anything he picks up while driving goes onto the cart: logs, stone, ore, crates, sacks, corpses, from the ground, piles, pallets and tables. | always (feature `Cargo`) | tested |
| **Six items** in layers of two, or four, or two like the game's own delivery cart (`CartSlots`, 6 by default since 2026-10-13; [`docs/cart-capacity.md`](../../docs/cart-capacity.md)). A full cart puts a ground pickup back ("The cart is full."). | cfg `CartSlots` | two items tested; the layers self-tested, their look in game pending |
| **The game takes from the cart:** piles, pallets, tables, graves and quest hand-ins take their items from the cart as if he held them. | always | tested |
| **Setting down:** the interact key with nothing in front sets the top item down, behind the cart (the game's own drop-spot search, avoiding walls, water and other drops), so the next press sets the next one down. | cfg, API | tested |
| **Hands stay empty while mounted:** what he holds when mounting goes onto the cart or is set down first; in Ride style the game refuses pickups and ground items show no outline. | always | tested |
| **Zombies ride on the cart:** as many as the cart holds, lying with their own heads and colours, handed to their work straight from the cart; organs and gear stay with them. A working zombie offers no "Take" while the cart can't take it. | Behaviour `CargoZombies` (on), API | tested ([docs/zombies-on-cart.md](../../docs/zombies-on-cart.md)) |
| **A storage chest rides on the cart:** drive up to a plain chest (rough, normal or good) and press the action key: it rides alone, drawn on the bed, contents untouched (it stays the game's own chest object throughout, never copied); the interact key with nothing in front sets it down; on foot at the standing cart it opens and sets down from there; a save and a load keep it on the cart; a door to another scene sets it down at the cart ([`docs/portable-chest.md`](../../docs/portable-chest.md)). | cfg `CargoChest`, API `ChestLoaded` / `ChestUnloaded`, `MountHandle.Chest` | tested |
| **A corpse into the morgue:** drive the cart through the morgue door with a body aboard and the ride ends at the door as always, the cart parked outside with its load, but the body is in the keeper's hands as he walks in. Only there; bodies only, as many as his hands take; nothing dropped. | cfg `CorpseIntoMorgue`, `CorpseIntoMorguePlaces` | tested |
| **Cargo is drawn on the cart:** every item's mesh measured at runtime and packed (side by side, end to end, or stacked), corpses drawn with the game's corpse mesh, the load rocking with the cart. Per-item and per-pair corrections in `cargo-layout.cfg`. | cfg, API, panel | tested |
| **Loading a standing cart on foot:** holding something, "[E] Load on cart" puts the top item onto it, one per press (with the several-items perk, press again); "Cart is full" when it is. | cfg, API | partly (loading tested) |
| **Unloading a standing cart on foot:** empty hands at the cart's back, "[E] Take from cart" takes the top item into the hands. At the donkey the same key drives. | cfg, API | untested |
| **Cargo events** for other mods: an item loaded, an item unloaded. | API | tested |
| **Corpses at the crossroads:** the game's own delivery donkey leaves the bodies it brings by the nun, where it appears, for the keeper to haul (the cart takes them); it walks out from behind its tree, unloads, walks back. Off in the core; a mod's switch; its four patches exist only while it's on ([`docs/corpse-delivery.md`](../../docs/corpse-delivery.md)). | `Mounts.Corpses` (off), `CorpseLeft`, `StartCorpseDelivery` | tested (as a standalone mod on 2026-10-10; the API form's in-game run pending) |
| **Never lost:** cargo lives in a temporary inventory, so it's handed back, or set down at the cart, around every save, dismount and crash cleanup. | always | tested |

### 3. Cart on slopes

| Feature | Reached by | Status |
|---|---|---|
| **The cart follows the ground:** raised or lowered as one so it doesn't sink into a slope behind the donkey. | cfg, API | tested |
| **Slope styles:** the whole cart pitches about its axle (default), only the shafts tilt, or a half lift; a maximum pitch and a maximum shift. | cfg, API | tested |

### 4. Lantern

| Feature | Reached by | Status |
|---|---|---|
| **A lantern pole on the cart,** movable along, up and across, tilted, leaned, scaled, mirrored, or hidden. | cfg, API (feature `Lantern`) | tested |
| **The lamp hangs straight down** from the pole's hook on slopes, while rolling and when tilted (the pole mesh is split at runtime; a game update that changes it turns this off safely). | cfg, API | tested |
| **Swaying** like a pendulum, scaled by the game's wind. | cfg, API | tested |
| **Its light:** the main light and the character light, glow and lit glass, each with intensity, range, shadows and colour; dimmer by day if wanted, following the game's sun. | cfg, API (feature `LanternLight`) | tested |

### 5. Sounds

All through the game's own sound player, in its mixer groups, so the game's volume sliders apply. No audio files ship.

| Feature | Reached by | Status |
|---|---|---|
| **Hoofsteps** on each footfall, matching the ground (grass, dirt, wood, stone); the keeper's own steps are silenced while mounted. | cfg, API | tested |
| **Grazing** chewing (a game sound the game never plays) and the donkey's lip-smacking. | cfg, API | tested |
| **Brays:** a chance at mounting, now and then while standing, optionally while walking. | cfg, API | tested |
| **Cart wheels:** a wooden rumble once per walk cycle. | cfg, API | tested |
| **Cargo thump** when an item lands in the cart. | cfg, API | tested |
| **The bell:** rung by a mod, and optionally on setting off after a stop, only with cargo if wanted. | cfg, API (feature `Bell`) | tested |
| **Every sound id editable,** with volume and pitch, and a master switch and volume. | cfg, API (feature `Sounds`) | tested |

### 6. Colours

| Feature | Reached by | Status |
|---|---|---|
| **Recolour every colour of the art on its own,** in 14 regions: donkey (coat, dark parts, muzzle, eye, harness), cart (body, shafts, straps, hooks, wheels, hubs), lantern (pole, lamp, glass). | cfg, API, panel | tested |
| **Per region:** hue, saturation, brightness, contrast, colorize, tint, overlay, and exact colour swaps. | cfg, API | tested |
| **Lamp colours:** glow, lit glass and character light. | cfg, API | tested |
| **The game's own delivery donkey stays untouched** (recolouring never reaches the parts the game reuses). | always | tested |
| **Per donkey:** each donkey in the world can have its own colours. | API | tested |

### 7. Donkeys and carts in the world

| Feature | Reached by | Status |
|---|---|---|
| **Any number of donkeys, donkeys with carts, and free carts** standing in the world, each in its own scene. | API, panel | tested |
| **Own settings per donkey:** any tuning, colour or behaviour key, winning over everyone else's values. | API, panel | tested |
| **Walk up and ride:** "[E] Ride" / "[E] Drive" over a standing donkey; the keeper is put on its seat, cargo, colours and lantern as they were. | cfg, API | tested |
| **Dismounting leaves it standing** where it is, with cart and cargo. | always | tested |
| **Hitch and unhitch:** ride up to a free cart and take it (interact key or J), or leave the cart standing with its cargo and settings (J). | cfg, API | tested |
| **Calling a donkey to the keeper,** refused (and kept where it is) where a ride would be refused. | API | tested |
| **Moving, removing, setting its cargo down** from a mod. | API | tested |
| **Spawn events:** spawned, removed, hitched, unhitched. | API | tested |

### 8. Idle life

| Feature | Reached by | Status |
|---|---|---|
| **Idle shuffle:** a standing donkey without a cart takes a few slow steps now and then, inside a small area around its spot, never straight up or down, checking the ground and obstacles; it holds still while the keeper is close. Off in the core. | cfg, API | partly (in daily use, test list open) |
| **Foals:** small donkeys (any size 0.2–2) that can't be ridden and stay with a parent: shuffling around it, trotting after it when it leaves, catching up out of sight when they can't. | API, panel | partly |
| **Foals find their way** round fences, log piles and workbenches with a small route finder. | cfg, API | partly |
| **Size and rideable** as general properties of any donkey, not just foals. | API | partly |

### 9. Kept with the save

| Feature | Reached by | Status |
|---|---|---|
| **Persist:** a donkey, cart or foal marked to keep comes back when that save is loaded: same place and scene, cargo on its cart, its own settings and every mod's data. | API, panel | tested (J1–J7) |
| **The game's save is never changed:** a side file next to each slot (`<slot>.gk2donkeyapi.dat`), synced by Steam Cloud, deleted with the slot, and ignored when it doesn't match the loaded save. | always | tested |
| **Several mods side by side:** each mod numbers its own donkeys, gives them its own keys, and keeps its own data on any donkey. | API | tested |
| **A mod that isn't installed:** its kept donkeys sleep in the file and come back with it. | always | partly |
| **Game ready event:** once per loaded game, after the kept donkeys are back, the place for a mod's own spawning. | API | tested |

### 10. Rules for other mods

| Feature | Reached by | Status |
|---|---|---|
| **Mount rules:** refuse a mount or a style, with the text the keeper says. | API | tested |
| **Stay rules:** end a ride while it goes on. | API | tested |
| **Feature rules:** switch cargo, the lantern, its light, sounds, the bell, or riding through doors off. | API | tested |
| **Session tuning:** a mod can set any value for the session without touching the player's cfg; the panel shows it locked. | API | tested |
| **Companion:** one call gives a mod the keeper's own donkey with its cart and foal: the ride and cart keys' behaviour, sending away, finding it again after a load, look and speed presets, a name in its prompt. | API | untested |
| **Settings page:** a mod defines its settings once; they show in GK2 Mod Framework's Mods menu when it's installed, else on pages in the game's own Settings window (the game's own rows). | API | untested |
| **Display names:** "[E] Ride Bella". | API | untested |
| **Gating on progress:** tech tree, quests, story flags, buildings, days (the example mod gates the cart and the lamp on techs). | API (with the game's own state) | tested |

### 11. Performance

| Feature | Reached by | Status |
|---|---|---|
| **Levels:** donkeys off screen stand frozen, far ones have no view at all and wake when the keeper comes near (363 donkeys measured at 10 ms a frame). | always, panel | tested |
| **View reuse** and at most one new view per frame, so spawning many doesn't hitch. | always | tested |
| **Built-in profiler** with `[perf]` log lines. | panel | tested |

### 12. Safety

| Feature | Reached by | Status |
|---|---|---|
| **No save changes:** the ridden donkey is a temporary world object the game never saves; a save made while mounted loads without the mod as if on foot. | always | tested |
| **Game-update guard:** does nothing if the game's code changed since the audit, and checks every game member it uses at start. | cfg | tested |
| **Master switch** (`General.Enabled`): off, no mod can mount; the kill switch if a mod built on this misbehaves. | cfg | tested |
| **Crash cleanup:** a failure mid-ride hands the cargo back and restores the keeper. | always | tested |
| **The real donkey NPC is never touched;** the mod only reads its prefab. | always | tested |

### 13. For mod authors: the dev panel

the marker file `BepInEx\config\dreas.gk2.dev`, `Debug.TuningWindow = true`, then F11. Tabs: General, Riding, Lantern, Sounds, Colors, Instances, Debug, Sources (where every value comes from and where it is saved).

| Feature | Status |
|---|---|
| **Live tuning** of every value, saved as you go; yellow rows differ from the mod default, cyan rows are locked by another mod; right-click resets a row. | tested |
| **Mod defaults** kept in the repo's `defaults.cfg` and shipped in the DLL ("Make these the mod defaults"). Colours are never mod defaults: every player starts with the game's own art (`tests/DefaultsScan`). | tested |
| **Instances tab:** spawn donkeys, carts and families, ride, move, unhitch, keep or forget, per-donkey settings, the standing-cart areas drawn on the ground, the head-limit test buttons. | tested |
| **Cargo photo sheet:** every item alone and every pair on the cart, photographed into one HTML page. | tested |
| **Self-tests:** about 4 minutes of scripted in-game checks (levels, view reuse, persistence text, hitching, leaks, frame time). | tested |
| **Probes and dumps:** the game donkey's script, carry facts, held-item facts, a real delivery on demand, any big item dropped in front. | tested |

## Not built

- Standing donkeys make no sounds (only the ridden one does).
- Only the donkey is tested as the mount (`MountWgoId`).
- No co-op.

## Install

1. BepInEx 5 in the Steam game folder.
2. `GK2DonkeyAPI.dll` and a mod built on it (Saddle Up) in `BepInEx\plugins`.
3. Start the game once; the log says `GK2 Donkey API 0.28.0 loaded; nothing happens until a mod uses it.`

The full settings, the internals and the in-game test list are in
[`docs/reference.md`](../../docs/reference.md).
