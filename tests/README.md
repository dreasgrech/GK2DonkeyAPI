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
| `CecilScan-DonkeyAPI` | GK2 Donkey API | Mono.Cecil: every IL reference into the game assemblies resolves; every Harmony target named by string (`[HarmonyPatch(typeof(T), "name")]`) is declared on that type, not inherited (an inherited one fails at start and the mod disables itself; found 2026-10-06); the `CreateScript` patch target and the `Wgo`/`WgoData`/animation members the mount calls exist with the expected overloads; the corpse delivery's on-demand patch targets (no `[HarmonyPatch]` attribute: `Flow_TryToAddCorpseToPallet.TryToAddCorpseToPallet`, `Flow_DropItem.Drop`, `Flow_GoTo.GoTo`, `UINotificator.ShowNewBodyNotification(string)`), the private ports their prefixes take and the movement overload they call; the embedded dependency manifest finds every member in the installed game unchanged, and every class of the DLL that uses game members is sorted in `game-code.txt` (`docs/dependency-check.md`). | `dotnet run -c Release` |
| `ApiSurface` | GK2 Donkey API | Mono.Cecil: the public API (namespace `GK2DonkeyAPI.Api`), one line per type and member, compared with the committed snapshot `ApiSurface/GK2DonkeyAPI.Api.txt`. Any change fails as an ADDED/REMOVED diff; review it, bump `Mounts.ApiVersion`, then `--update`. Mutation-tested: a line removed from the snapshot fails it. | `dotnet run -c Release [-- --update]` |
| `DefaultsScan` | GK2 Donkey API | The shipped mod defaults (`src/GK2DonkeyAPI/defaults.cfg`) hold no colours, so every player starts with the game's own art. Fails on any `[Colors*]` value or a lamp light other than the game's `FF9600`. | `dotnet run -c Release` |
| `SkinsScan` | GK2 Donkey API, GK2 Saddle Up | The shipped skins (every `src/*/assets/skins/<id>/`: the API's two donkey skins, Saddle Up's five mounts): every `skin.cfg` parses as the loader reads it (format, id = folder, name, mount, known keys only, no skin `foals` flag), every one of the donkey base's 44 frame keys has a PNG at the canvas size (front-layer PNGs too), a skin's `[Tuning]` holds only the picture keys (`Looks.SkinTuningKeys`), a mount's `[Mount.Tuning*]` keys exist in `StyleSettings`, and a mount declared by several skins is declared the same way in each. The key lists are read from `Looks.cs` and `Plugin.cs`. | `dotnet run -c Release` |

`fixtures/released/<version>/` holds the player-owned files as each public release wrote them (`dreas.gk2.saddleup.cfg`,
`dreas.gk2.donkeyapi.cfg`, a `*.gk2donkeyapi.dat` side file, that release's `defaults.cfg`); the in-game self-test
"upgrade: a released version's player files are all read" runs over every folder (`docs/compatibility.md`). 0.9.1 is
reconstructed from the sources at commit 7ac9a6c; later folders are captured at release (`docs/release-checklist.md`, 5).

The tests of the four small mods (Global Storage, Built Count, Move Building, Owned Count) are in the GK2QOLMods
repo, where those mods live.

In-game test lists: `docs/reference.md` (the API), `src/SaddleUp/README.md`, and the self-tests on the dev panel
(`docs/self-tests.md`).
