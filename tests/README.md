# Verification tools

Console projects that check the built mod DLLs against the installed game without launching it. The game folder
comes from `GameDir` in the repo's `Directory.Build.props` (the Steam install); `tests/Directory.Build.props` passes
it to every test (`GamePaths.cs`). Each is run after every build and after every game update.

**After a game update, first:** `dotnet run -c Release -- "<previous game folder>"` in `GameDiff` lists every
member of `Assembly-CSharp` and `LazyBearTechnology` that was removed, added or changed (full list in
`%TEMP%\gamediff.txt`). Check it against every patch target and every game member the mods use, then run the
scans below, then update the audited MVIDs and version strings.

| Project | Mod | What it checks | Run |
|---|---|---|---|
| `GameDiff` | all | Member-by-member diff of two game installs (`Assembly-CSharp`, `LazyBearTechnology`): removed / added / changed, compiler-generated removals counted apart. | `dotnet run -c Release -- "<old game folder>" ["<new game folder>"]` |
| `FrameworkScan` | Saddle Up (the one mod here with the optional Mods-menu integration) | Mono.Cecil, on each mod's built DLL: every `[HarmonyPatch]` class is in `Plugin.PatchClasses`, and `Plugin` never calls `PatchAll(Assembly)`; `Plugin` has no method Unity calls by name (Start, OnEnable, …); `GK2.Framework` is used only by `FrameworkLink.Register` (NoInlining) and the classes nested in `FrameworkLink`; there's a soft `BepInDependency` on the framework. See `docs/gk2-mod-framework.md`. Mutation-tested: a planted framework field and a missing patch class each fail it. | `dotnet run -c Release [ModName ...]` |
| `CecilScan-DonkeyAPI` | GK2 Donkey API | Mono.Cecil: every IL reference into the game assemblies resolves; the `CreateScript` patch target and the `Wgo`/`WgoData`/animation members the mount calls exist with the expected overloads. | `dotnet run -c Release` |
| `ApiSurface` | GK2 Donkey API | Mono.Cecil: the public API (namespace `GK2DonkeyAPI.Api`), one line per type and member, compared with the committed snapshot `ApiSurface/GK2DonkeyAPI.Api.txt`. Any change fails as an ADDED/REMOVED diff; review it, bump `Mounts.ApiVersion`, then `--update`. Mutation-tested: a line removed from the snapshot fails it. | `dotnet run -c Release [-- --update]` |

The tests of the four small mods (Global Storage, Built Count, Move Building, Owned Count) are in the GK2QOLMods
repo, where those mods live.

In-game test lists: `docs/reference.md` (the API), `src/SaddleUp/README.md`, and the self-tests on the dev panel
(`docs/self-tests.md`).
