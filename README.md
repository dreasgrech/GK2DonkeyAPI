# GK2 Donkey API and GK2 Saddle Up

BepInEx mods for Graveyard Keeper 2 that give the game a rideable donkey and its cart.

| Mod | Version | For | What it is |
|---|---|---|---|
| [GK2 Saddle Up](src/SaddleUp/README.md) | 0.13.1 | players | Your own donkey and cart: call it with H or G, ride it, drive it, haul logs and corpses on the cart, leave it standing with its load and find it again after a reload. Gerry the skull comes with it; the Dragon (GK2 Saddle Up: Dragon), the Rat, Snail and Worm (GK2 Saddle Up: Critters) and the donkey's skins Hellfire and Royal Parade (GK2 Saddle Up: Donkey Skins) are downloads of their own. |
| [GK2 Donkey API](src/GK2DonkeyAPI/README.md) | 0.41.1 | mod authors (and every Saddle Up player, as its library) | The library behind it: riding, the cart and its cargo, the lantern, sounds, colours, any number of donkeys and carts in the world, foals, keeping them with the save, rules for other mods. On its own it does nothing. |

Both are built and audited against Steam game build 26297960 (game 1.011; `docs/game-update-26297960.md`).

What you ride and how it is drawn (the donkey in the API; Gerry built into Saddle Up; the dragon, rat, snail and
worm and the donkey's two skins as Saddle Up's external packs, each its own download, in `packs/`; the mounts and skins are on for players since Saddle Up 0.12.0, [`docs/skins.md`](docs/skins.md); a mod shipping a mount of its own: [`docs/making-a-mount.md`](docs/making-a-mount.md)):

| Mount | Skins | Notes |
|---|---|---|
| Donkey (the game's own) | the game's own look; in GK2 Saddle Up: Donkey Skins, **Hellfire** (horns, a devil's tail, glowing eyes, lava cracks; its ring of fire is registered but not shown since API 0.37.0), **Royal Parade** (a saddle cloth and a crown; the cloth follows the harness colour), **Armored** (plate barding: a plumed chanfron, a chest plate, flank lames and a crupper; the steel follows the second tone, the plume and padding the harness colour; unreleased) | Normal speed, cart of 6, babies |
| Rat, Snail, Worm (GK2 Saddle Up: Critters) | their own look (our own art) | the rat Very fast, cart of 2; the worm Slow, cart of 4; the snail Very slow, cart of 6; babies; the snail and the worm have no voice |
| Dragon (GK2 Saddle Up: Dragon) | its own look (our own art, called Red Dragon) | Slow, babies, no cart, **flies** (Space jumps and takes off, Left Shift and Left Ctrl climb and descend, docs/flying.md); its near wing is drawn in front of the rider |
| Gerry (the game's talking skull) | the game's own frames, read at run time | Fast, cart of 4, babies his own size; the keeper calls him a skull |

Packs from anyone join the list: a skin is a folder with a `skin.cfg` and a PNG per frame, and a pack can bring a
mount of its own (a mount folder with a `mount.cfg`, its skins under `skins\`; [`docs/skins.md`](docs/skins.md)); a mod can
ship packs in its DLL the way Saddle Up ships its own (the API ships none) ([`docs/mounts-as-mods.md`](docs/mounts-as-mods.md)).

## Installing (players)

1. **BepInEx 5** (the 5.4.23.x, x64, Mono build) in the game folder: the Steam game ships it since 1.011; on another
   store or an older build, install it yourself.
2. Extract the release zip into the game folder: it adds `BepInEx\plugins\GK2DonkeyAPI\GK2DonkeyAPI.dll` and
   `BepInEx\plugins\GK2SaddleUp\GK2SaddleUp.dll`. Or, on 1.011 or newer, into the game's own mod folder
   `%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\Code\GK2SaddleUp\`: the main menu's Mods window then
   lists GK2 Saddle Up with its version and an Enable/Disable switch, and the game copies the plugins in itself
   (`docs/mods-window-and-workshop.md`). One place or the other, never both.
3. Optional: the packs, each a download and a folder of its own for `BepInEx\plugins\` (or for `Mods\Code\`, like the mod)
   (`GK2SaddleUpDragonsMountPack\GK2SaddleUpDragonsMountPack.dll`, `GK2SaddleUpCrittersMountPack\GK2SaddleUpCrittersMountPack.dll`,
   `GK2SaddleUpDonkeySkinPack\GK2SaddleUpDonkeySkinPack.dll`).
4. Start the game. The log (`BepInEx\LogOutput.log`) says `GK2 Donkey API 0.41.1 loaded` and `GK2 Saddle Up 0.13.1
   loaded`.
5. Optional: [GK2 Mod Framework](https://www.nexusmods.com/graveyardkeeper2/mods/42) puts Saddle Up's settings in
   the game's **Mods** menu. Without it, everything works the same and the settings are in
   `BepInEx\config\dreas.gk2.saddleup.cfg`.

**After a game update** the API compares every game member it uses with the new game at start (a list embedded
in the DLL, [`docs/dependency-check.md`](docs/dependency-check.md)). All identical: it runs as before. A member gone, or the code of
the core changed: it stops and the game runs unmodified; your donkeys, carts and their loads stay in the save as the
game holds them (the cargo lies at the cart, a chest stands there), nothing is lost. Only a feature's code changed:
that feature is off or runs with a warning, the rest works. The log line starts with `GK2 Donkey API: the game
changed the code of`; Saddle Up follows the API.

## Building on the API (mod authors)

Start with [`docs/api.md`](docs/api.md). Reference `GK2DonkeyAPI.dll` with `Private=false`, declare
`[BepInDependency("dreas.gk2.donkeyapi")]`, and check `Mounts.ApiVersion`. Saddle Up (`src/SaddleUp/Plugin.cs`) uses
nothing but the public API, so it's the worked example. The two in `examples/` show rules gated on the tech tree and
a second mod keeping its own donkeys with the save.

## The repo

| Where | What |
|---|---|
| `src/GK2DonkeyAPI` | The API mod. Its README lists every feature; the settings and internals are in [`docs/reference.md`](docs/reference.md). |
| `src/SaddleUp` | The player mod. What it offers and why: [`docs/saddleup-scope.md`](docs/saddleup-scope.md). Its built-in Gerry is a pack in `assets/mounts/<mount>/` (`mount.cfg`, `sounds/`, `skins/default/` and any other `skins/<skin>/`), embedded and registered with the API: how any mod ships a mount, [`docs/mounts-as-mods.md`](docs/mounts-as-mods.md). |
| `packs` | Saddle Up's external packs, each its own download laid out as `BepInEx\plugins\<Pack>\`: `GK2SaddleUpDragonsMountPack` (`saddleup-dragon.dragon`), `GK2SaddleUpCrittersMountPack` (`saddleup-critters.rat`, `.snail`, `.worm`), `GK2SaddleUpDonkeySkinPack` (skins only: `donkey/saddleup-donkey-skins.hellfire`, `.royal-parade`, and Hellfire's effect); each a small plugin (`<Pack>.csproj`, `Plugin.cs`) with its `gk2mounts\` embedded, deployed by its build like any plugin. [`docs/making-a-mount.md`](docs/making-a-mount.md) "Built in or external". |
| `examples/` | `GK2DonkeyTechGate` (the cart and the lamp's light need techs) and `GK2DonkeyHerd` (a second mod with its own kept donkeys). Not shipped. |
| `tools/` | `GK2DonkeyProbe` (the investigation plugin and its archived reports), `GK2DonkeyAPIProbe` (every API call on F-keys), `colors` (the shader and texture tools behind the colours), `GK2PerfRunner` (scripted profiling, [`docs/performance-hog.md`](docs/performance-hog.md)), `GK2AssetProbe` (art from outside the game, [`docs/external-assets.md`](docs/external-assets.md)), `GK2DonkeyAPI.IntegrationTests` (the in-game tests of Saddle Up and its packs, joining the API's self-tests, [`docs/self-tests.md`](docs/self-tests.md)), `layering-scan.py` (the API names no mod, Saddle Up no pack). Not shipped. |
| `tests/` | Checks run without the game: the public API snapshot, the game members the API uses, the optional framework link, and the game-update diff ([`tests/README.md`](tests/README.md)). |
| `docs/` | The design and investigation notes, one per feature, plus the game-update audits. What an update must never break for a player (settings, colours, the parked cart's load): [`docs/compatibility.md`](docs/compatibility.md). What is still to check in game, undecided or put off: [`docs/open-items.md`](docs/open-items.md). |
| `lib/GK2.Framework` | The GK2 Mod Framework DLL that Saddle Up compiles against (it's never shipped with our mods). |
| `dist/` | The release DLLs. |

**Building:** `dotnet build -c Release` in a project folder. The game folder is set once in `Directory.Build.props`
(`GameDir`). A build copies the DLL into the game's `BepInEx\plugins`, but only while the game is closed
(`DeployToGame.targets`). `-p:Deploy=false` skips the copy.
