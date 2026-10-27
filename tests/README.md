# Verification tools

Console projects that check the built mod DLLs against the installed game without launching it. The game folder
comes from `GameDir` in the repo's `Directory.Build.props` (the Steam install); `tests/Directory.Build.props` passes
it to every test (`GamePaths.cs`). Each is run after every build and after every game update.

**After a game update, first:** `dotnet run -c Release -- "<previous game folder>"` in `GameDiff` lists every
member of `Assembly-CSharp` and `LazyBearTechnology` that was removed, added or changed (full list in
`%TEMP%\gamediff.txt`), and `dotnet tools\DepManifest\bin\Release\net8.0\DepManifest.dll verify <the API DLL>
<Managed>` says which of the members the API uses changed (`docs/dependency-check.md`). Check those against the
code, then rebuild (the build embeds the manifest), run the scans below and update the version strings.

| Project | Mod | What it checks | Run |
|---|---|---|---|
| `GameDiff` | all | Member-by-member diff of two game installs (`Assembly-CSharp`, `LazyBearTechnology`): removed / added / changed, compiler-generated removals counted apart. | `dotnet run -c Release -- "<old game folder>" ["<new game folder>"]` |
| `FrameworkScan` | Saddle Up (the one mod here with the optional Mods-menu integration) | Mono.Cecil, on each mod's built DLL: every `[HarmonyPatch]` class is in `Plugin.PatchClasses`, and `Plugin` never calls `PatchAll(Assembly)`; `Plugin` has no method Unity calls by name (Start, OnEnable, …); `GK2.Framework` is used only by `FrameworkLink.Register` (NoInlining) and the classes nested in `FrameworkLink`; there's a soft `BepInDependency` on the framework. See `docs/gk2-mod-framework.md`. Mutation-tested: a planted framework field and a missing patch class each fail it. | `dotnet run -c Release [ModName ...]` |
| `NoLinqScan` | every netstandard2.1 project of the repo (the mods and the in-game dev tools) | Mono.Cecil, on each one's built DLL (the newer of bin/Release and bin/Debug): no reference into `System.Linq` anywhere (IL operands, locals, fields, signatures, base types, attributes), each one named. The build already refuses LINQ (`Directory.Build.targets`: the banned-API analyzer, error RS0030, `BannedSymbols.txt`); this catches what the analyzer can't see. No LINQ in anything that runs in the game: every call allocates (the user, 2026-10-25). | `dotnet run -c Release` |
| `CecilScan-DonkeyAPI` | GK2 Donkey API | Mono.Cecil: every IL reference into the game assemblies resolves; every Harmony target named by string (`[HarmonyPatch(typeof(T), "name")]`) is declared on that type, not inherited (an inherited one fails at start and the mod disables itself; found 2026-10-06); the `CreateScript` patch target and the `Wgo`/`WgoData`/animation members the mount calls exist with the expected overloads; the corpse delivery's on-demand patch targets (no `[HarmonyPatch]` attribute: `Flow_TryToAddCorpseToPallet.TryToAddCorpseToPallet`, `Flow_DropItem.Drop`, `Flow_GoTo.GoTo`, `UINotificator.ShowNewBodyNotification(string)`), the private ports their prefixes take and the movement overload they call; the embedded dependency manifest finds every member in the installed game unchanged, and every class of the DLL that uses game members is sorted in `game-code.txt` (`docs/dependency-check.md`). | `dotnet run -c Release` |
| `ApiSurface` | GK2 Donkey API | Mono.Cecil: the public API (namespace `GK2DonkeyAPI.Api`), one line per type and member, compared with the committed snapshot `ApiSurface/GK2DonkeyAPI.Api.txt`. Any change fails as an ADDED/REMOVED diff; review it, bump `Mounts.ApiVersion`, then `--update`. Mutation-tested: a line removed from the snapshot fails it. | `dotnet run -c Release [-- --update]` |
| `DefaultsScan` | GK2 Donkey API | The shipped mod defaults (`src/GK2DonkeyAPI/defaults.cfg`) hold no colours, so every player starts with the game's own art. Fails on any `[Colors*]` value or a lamp light other than the game's `FF9600`. | `dotnet run -c Release` |
| `SkinsScan` | GK2 Donkey API, GK2 Saddle Up | The shipped mounts and skins (built in, every `src/*/assets/mounts/<mount>/`: Saddle Up's Gerry; external, every `packs/<Pack>/gk2mounts/<mount>/`: the dragon, rat, snail and worm, and the Donkey Skins pack's `donkey/skins/` (a mount folder without `mount.cfg`), whose ids carry the pack's prefix, whose `mount.cfg` says `needs`, and whose pack has its README.md and CHANGELOG.md): every `mount.cfg` parses as the loader reads it (known keys only; `id`, `name`, `author`, `version`, `cart`, `foals` given; the folder named `<mod>.<name>`; no `default_skin`, a `skins\default\skin.cfg` instead; speed and cargo in range; its `[Tuning*]` keys exist in `StyleSettings`; its sounds in the mount's `sounds\`; the section order), the donkey's folder has no `mount.cfg`, `sounds\` or `default`, and every skin in a mount folder without `mount.cfg` (the donkey's) is named `<mod>.<name>` (`saddleup-donkey-skins.hellfire`: a skin added to a mount another mod owns, `Looks.AddsToOthers`), and every `skin.cfg` parses too (format, no `id =` line (the folder's name is the skin's), name, known keys only, no `[Mount*]` section, a `mount =` line only if it matches the folder, no skin `foals` flag, its sounds in its own `sounds\`); every one of the donkey base's 44 frame keys has a PNG at the canvas size (front-layer PNGs too; a skin built on its mount's default, the ones it draws, the rest read against `../default` with its canvas, colours and sounds), and a skin's `[Tuning]` holds only the picture keys (`Looks.SkinTuningKeys`). The key lists are read from `Looks.cs` and `Plugin.cs`. The shipped effect ids are read from every `src/*/Plugin.cs` and `packs/*/Plugin.cs`: each carries a mod prefix, a pack's effect the pack's own (Hellfire's, `saddleup-donkey-skins.hellfire`). | `dotnet run -c Release` |

`fixtures/released/<version>/` holds the player-owned files as each public release wrote them (`dreas.gk2.saddleup.cfg`,
`dreas.gk2.donkeyapi.cfg`, a `*.gk2donkeyapi.dat` side file, that release's `defaults.cfg`); the in-game self-test
"upgrade: a released version's player files are all read" (the API's files) and the integration tests' "upgrade:
Saddle Up's cfg as every released version wrote it ..." (Saddle Up's cfg, since 2026-10-27) run over every folder
(`docs/compatibility.md`). 0.9.1 is
reconstructed from the sources at commit 7ac9a6c; later folders are captured at release (`docs/release-checklist.md`, 5).

The tests of the four small mods (Global Storage, Built Count, Move Building, Owned Count) are in the GK2QOLMods
repo, where those mods live.

In-game test lists: `docs/reference.md` (the API), `src/SaddleUp/README.md`, and the self-tests on the dev panel
(`docs/self-tests.md`: the API's own, and those of Saddle Up and its packs in the dev plugin
`tools/GK2DonkeyAPI.IntegrationTests`).
