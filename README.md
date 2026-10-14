# GK2 Donkey API and GK2 Saddle Up!

BepInEx mods for Graveyard Keeper 2 that give the game a rideable donkey and its cart.

| Mod | Version | For | What it is |
|---|---|---|---|
| [GK2 Saddle Up!](src/SaddleUp/README.md) | 0.9.0 | players | Your own donkey and cart: call it with H or G, ride it, drive it, haul logs and corpses on the cart, leave it standing with its load and find it again after a reload. |
| [GK2 Donkey API](src/GK2DonkeyAPI/README.md) | 0.29.0 | mod authors (and every Saddle Up player, as its library) | The library behind it: riding, the cart and its cargo, the lantern, sounds, colours, any number of donkeys and carts in the world, foals, keeping them with the save, rules for other mods. On its own it does nothing. |

Both are built and audited against Steam game build 25910882 (game 1.010).

Until 2026-10-02 the two were called GK2 Mounts and GK2 Showcase, and lived in the
[GK2QOLMods](https://github.com/dreasgrech/GK2QOLMods) repo. Their history came along: `git log --follow` works
through the rename.

## Installing (players)

1. **BepInEx 5** (the 5.4.23.x, x64, Mono build) in the game folder.
2. Both `GK2DonkeyAPI.dll` and `SaddleUp.dll` into `BepInEx\plugins`.
3. Start the game. The log (`BepInEx\LogOutput.log`) says `GK2 Donkey API 0.29.0 loaded` and `GK2 Saddle Up! 0.9.0
   loaded`.
4. Optional: [GK2 Mod Framework](https://www.nexusmods.com/graveyardkeeper2/mods/42) puts Saddle Up's settings in
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
| `src/SaddleUp` | The player mod. What it offers and why: [`docs/saddleup-scope.md`](docs/saddleup-scope.md). |
| `examples/` | `GK2DonkeyTechGate` (the cart and the lamp's light need techs) and `GK2DonkeyHerd` (a second mod with its own kept donkeys). Not shipped. |
| `tools/` | `GK2DonkeyProbe` (the investigation plugin and its archived reports), `GK2DonkeyAPIProbe` (every API call on F-keys), `colors` (the shader and texture tools behind the colours). Not shipped. |
| `tests/` | Checks run without the game: the public API snapshot, the game members the API uses, the optional framework link, and the game-update diff ([`tests/README.md`](tests/README.md)). |
| `docs/` | The design and investigation notes, one per feature, plus the game-update audits. What is still to check in game, undecided or put off: [`docs/open-items.md`](docs/open-items.md). |
| `lib/GK2.Framework` | The GK2 Mod Framework DLL that Saddle Up compiles against (it's never shipped with our mods). |
| `dist/` | The release DLLs. |

**Building:** `dotnet build -c Release` in a project folder. The game folder is set once in `Directory.Build.props`
(`GameDir`). A build copies the DLL into the game's `BepInEx\plugins`, but only while the game is closed
(`DeployToGame.targets`). `-p:Deploy=false` skips the copy.
