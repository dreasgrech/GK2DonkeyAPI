# The window code as a library for the other mods (investigation, 2026-11-13)

The user, 2026-11-13: "extract our base UI code from our Saddle Up / Donkey API DLLs and have it be shipped with them
but in a separate DLL ... to reuse that code to have a settings window in our other mods like Tech Tree Search".
Investigation only; nothing built. The occasion: Tech Tree Search went on the Steam Workshop that day, where no player
has GK2 Mod Framework (a Nexus-only mod), so the QOL mods' settings are a cfg file there (the same reason the API got
its own options window on 2026-10-30, [options-window.md](options-window.md)).

Short answer: **worth doing, but not as a runtime DLL.** The generic window code is about 3,300 lines with a dozen
small hooks into the mounts; cutting it out is a day or two of refactoring plus the tests. Shipping it as one shared
DLL between Workshop items is the part that does not work well (§3). Compiling the same sources into each mod (§4)
gives every mod the window with none of the sharing problems; that is the recommendation (§6).

## 1. What the window code is

Read by an inventory of `src/GK2DonkeyAPI` (an Opus agent, line numbers in its report; spot-checked). The pieces are
methods, not classes: `UiKit` makes windows, title bars, boxes, texts, scroll views, buttons and focus frames;
`RowControls` makes the controls (switch, number buttons, slider, text box, key box, button, info, dropdown).
`MountsPanel` (2,742 lines) is the second user of both; it is all mounts and stays where it is.

| File | Lines | Generic | Mount-specific (approx.) | The couplings |
|---|---|---|---|---|
| `UiKit.cs` | 735 | all | 0 | `Plugin.Log`; the object-name prefix "GK2DonkeyAPI" (:93, :106, :301) |
| `UiSounds.cs` | 115 | all | 0 | `SoundBank.Config` (one line: the game's `AudioConfig` singleton) |
| `RowControls.cs` | 692 | ~680 | ~10 | `MountSettingsPage.Row`; `FillList` (:117) and `Texts.Dimmed` carry skin logic; `DeferredSave`, `KeyCapture` |
| `OptionsWindow.cs` | 702 | ~680 | ~20 | `MountsPanel.IsColoursOpen/IsShownOnTop/HideUnderOptions/ShowAfterOptions` (:86-161), `Mount.IsGameReady` (:91, :166, :377, :492, :601), `r.OpensPanel` (:411); ~95 lines of self-test accessors |
| `SettingsButtons.cs` | 211 | all | 0 | the game's `LazyWindowsStackController.OnWindowOpened` event, no patch |
| `GameWindowAside.cs` | 296 | ~280 | ~15 | `Mount.IsGameReady` (:162); the `Restore(back:false)` branch for Ride/Drive (:103-114) |
| `KeyCapture.cs` | 479 | all | 0 | `KeyActions.SameKey` (:217, :252, :264), `MountSettingsPage.All`, `Plugin.Guid` |
| `DeferredSave.cs` | 85 | all | 0 | ticked from `MountRegistry.Tick` (:451) |
| `UiWords.cs` | 379 | ~110 | ~270 | 192 words, about 20 of them the window's (`options.*`, `key.*`, four `row.*`) |
| `Texts.cs` | 1,052 | ~950 | ~100 | the owner id is `Plugin.Guid` (:34); `Catalog`/`Looks`/`SkinTones`/`SkinRegions` English (:837-879) |
| `TranslationFiles.cs` | 1,701 | all | 0 | the CSV parser |
| `Api/MountSettingsPage.cs` | 1,585 | ~830 | ~755 | `Row` carries mount fields; `MountRows`, `FoalRows`, `Color`, `Preview`, `PanelButton`, `Cart/Region/Tone/PerMount/PerSkin` rows; `Publish` → `Plugin.NextFrame` → `FrameworkLink.TryRegister` |

Totals: the window core (`UiKit`, `UiSounds`, `RowControls`, `OptionsWindow`, `SettingsButtons`, `GameWindowAside`,
`KeyCapture`, `DeferredSave`) is 3,315 lines; the translation system (`Texts` + `TranslationFiles`) 2,753; the generic
part of the page declaration about 830. About 1,170 lines in those files are mounts.

**Hooks into the game** (what any copy of the code needs): one Harmony postfix,
`InWorldSfxFilterController.ShouldEnable` (the world muffled under our window, cosmetic); the private
`MainGame.PauseGame`/`UnpauseGame` through `AccessTools.MethodDelegate`; `PlayerController.SetControlTakenType(ByUI)`;
Escape and gamepad Back consumed through `LazyInput.ClearKeyDown`; the game window underneath set inactive through
`LazyWindowsStackController.ActiveWindow`; the Settings window's button added on `OnWindowOpened` plus a half-second
watchdog. The look is read off `UIGameSettingsWindow` at open time (its row's font, its OK button's face, its frame
and title bar sprites, the dim behind it); two objects are cloned (the OK button, a row's focus frame), the rest
referenced. No `Resources.Load`, no asset bundle, no prefab of our own.

**The driver:** `Plugin.LateUpdate` (:827-839) ticks `GameWindowAside`, `SettingsButtons`, `KeyCapture` and
`OptionsWindow` every frame; `DeferredSave.Tick` from the registry; `DeferredSave.FlushAll` on quit.

**Words:** the window's ~20 words and every page's labels go through `Texts` (owner = the API's GUID) with
`Languages\<lang>.csv` next to the DLL (not embedded; 11 files of 193 lines), the English in the code as the fallback.

## 2. What a consumer mod needs

A QOL mod today declares 1 to 8 toggles and a key in ~100 lines of `FrameworkLink.cs` (Tech Tree Search: two toggles
and a key; Owned Count: seven toggles; Event Decorations: ~18 toggles over three sections). The same in the API's page
language is `p.Toggle(section, key, value, label, description, apply)` and `p.Key(...)`, plus `Publish()`. With the
window it gets: the button in the game's Settings window (main menu and in game), the window with the game's look,
pause, muffle, dim, sounds, the gamepad, key capture with clash names, descriptions, deferred cfg writes. Nothing a
QOL mod needs is mount-specific. The QOL mods are English-only; the words system can ship English alone (the CSV
folder is optional: the code's English is the fallback).

## 3. Way A: one shared runtime DLL (the question as asked)

**Loading, measured** on the installed BepInEx 5.4.23.5 (decompiled `PreloaderRunner.LocalResolve`,
`Utility.TryResolveDllAssembly`): a reference to `GK2Ui.dll` from a plugin is resolved first against the assemblies
already loaded with that simple name (the exact version if loaded, else the highest), then by file `GK2Ui.dll` in
`core`, `patchers`, then `plugins` and every subfolder in directory order, first hit wins. So a library DLL in any
plugin folder is found. It is also scanned by the chainloader like every DLL under `plugins`; without a plugin class
it is skipped, which is fine.

What follows from that for our two channels:

| Concern | What happens |
|---|---|
| Two copies of different versions on disk (Saddle Up's folder and Tech Tree Search's) | the first folder found serves everyone; the mod built against the newer one hits `MissingMethodException` at the first new member. The order is the file system's, not ours. |
| The same path in two Workshop items (`plugins\GK2Ui\GK2Ui.dll` in both) | the game's sync shares the file: unsubscribing one item deletes it for the other ([mods-window-and-workshop.md](mods-window-and-workshop.md) §6, the lab run of §9). |
| A Workshop item of its own ("GK2 UI", required by every mod item) | works like the API for the packs: one more subscription for every player (a second for Tech Tree Search, a second or third for Saddle Up with a pack); the Mods window lists it with a Disable button that deletes its DLL and breaks every window (§4 of the pointer doc: Disable removes the copied file). |
| Nexus | the same DLL in every zip at the same path: extracts over itself; fine as long as the newest always wins, which the loader does not guarantee (above). |
| Versions | the library's public surface becomes a contract kept forever, like the API's: every change a new version, every mod built against the oldest it can accept. |
| The driver | a library has no `LateUpdate`: either it becomes a plugin itself (then it is a mod in the Mods window, see Disable) or every consumer ticks it, and two consumers tick it twice. |
| Words | `Languages\*.csv` next to the library, one folder per copy. |

The upshot: a runtime DLL is the shape of the API/pack split, which exists because the packs' content must not be
duplicated. The window code has no such constraint: a copy per mod costs nothing a player can see.

## 4. Way B: the same sources compiled into each mod (recommended)

The pattern already in GK2QOLMods: `src/Shared/DependencyManifest.cs` and `GameDependencies.cs` are compiled into
every mod through `DependencyManifest.targets` (`<Compile Include=... Link=...>`). The window sources would live in
one place and be compiled into the API, Tech Tree Search, Owned Count, Built Count, Global Storage and Event
Decorations, each with its own copy, `internal`, under its own assembly.

Where the one place is: a third repo (GK2Ui) as a git submodule of both repos, or a folder in GK2DonkeyAPI referenced
by path from GK2QOLMods' `Directory.Build.props` (fragile: a path outside the repo). The submodule is the clean one;
the user decides.

What two copies of the same code in one game do:

- The one Harmony postfix (`InWorldSfxFilterController.ShouldEnable`, GameWindowAside.cs:290-294): it only ever sets
  the result to true, while its owner's window is open in game, and leaves it alone otherwise; so two copies stack
  without undoing each other (read, not run). Harmony ids must differ (each mod patches with its own GUID; they do).
- **The pause is per copy and must not be.** `LetPauseGo` (GameWindowAside.cs:240-256) unpauses when its own copy
  paused (`PausedByUs`) and no game window wants the pause; it does not know another copy's window. Two of our windows
  open at once (a key-opened window over another mod's) and the second one closing would unpause under the first. Two
  copies in separate assemblies share no statics, so the fix is a marker in the scene both find by name (one holder
  object under the game's canvas carrying a count of open windows, created by the first copy), or the rule that a copy
  refuses to open while the holder says another window is open. To design; the holder is the simpler.
- **The template filters must know every copy.** `UiKit.SwitchOf` (:98-109) falls back to the first `UISwitchButton`
  not named "GK2DonkeyAPI…" when the private field read fails; with per-owner names, copy B's fallback could take copy
  A's row as its template. Every copy names its objects with one common prefix ("GK2Ui " + the owner's GUID) and the
  filters test the common part. `StripForeign` is safe as it is (it keeps only components from the game's Managed
  folder, by assembly location).
- The Settings window gets one button per mod: each copy adds its own on `OnWindowOpened`. Today the API lays its
  buttons out for its own pages; with N independent copies the buttons' places must not collide. One rule in the
  shared code (a fixed slot order by GUID, or a shared named holder object under the Settings window that the first
  copy creates and the others find by name) settles it; to design, and to measure in game with three mods.
- `DeferredSave`, `KeyCapture`, `GameWindowAside`: per copy, per window; a window of one mod over a window of another
  is the same case as a game window over ours (`Covered`), which the code handles.
- Object names: the prefix "GK2DonkeyAPI" becomes the owner's GUID.
- Size: the API DLL is 3.4 MB (pictures); the window code compiles to an estimated 150 to 250 KB per mod (to measure
  with the first build). Tech Tree Search is 54 KB today; it would become about 250 KB. Nothing a player notices.

## 5. What the extraction entails (either way; B shown)

1. **Cut the mount couplings** (the dozen spots in §1): `OptionsWindow`'s four `MountsPanel` calls become two hooks
   the owner registers (an "under the options" window to hide and show; a "may open" veto); `Mount.IsGameReady`
   becomes an `inGame` function the owner gives (the API passes its registry's; a QOL mod passes
   `MainGame.Instance != null`); `KeyCapture`'s clash check takes a key-equality function; `DeferredSave` and the
   four ticks move to a `UiHost.LateUpdate` the owner calls; `Plugin.Log` becomes the owner's `ManualLogSource`;
   the name prefix the owner's GUID; `Texts.Api` the owner's GUID.
2. **Split the page.** `MountSettingsPage` (1,585 lines) becomes `SettingsPage` (the generic ~830 lines: Row, Bind,
   MovedKey/RetiredKey, upgrade claims, Toggle/Choice/List/Steps/Key/Text/Info/Heading/Button, EnableWhen, Publish)
   in the library, and `MountSettingsPage` in the API keeps its name and every public method as a wrapper or
   extension (the packs and Saddle Up keep compiling: the layering and compatibility rules). `Row`'s mount fields move
   to a side table the API keeps.
3. **Split the words.** `UiWords` keeps the ~170 mount words in the API; the ~20 window words go with the library.
   `Texts` + `TranslationFiles` go whole (the page labels need them); the API's `EnglishOf` mount branch stays behind
   as an owner hook.
4. **Several copies in one game** (§4): the Settings window's buttons, the shared pause holder, the common name
   prefix: design, then measure with three mods.
5. **Build plumbing:** the shared folder or submodule, a `.targets` in each repo, the QOL mods' `FrameworkLink.cs`
   replaced by a page declaration (the framework link can stay as a soft extra, as the API kept it: its Settings
   button opens our window).
6. **Tests:** the API's F11 options scenarios (the ~95 accessor lines) keep passing unchanged; a QOL mod gets a smoke
   check (window opens from the Settings window, a toggle writes the cfg, Escape closes, gamepad focus). The
   `FrameworkScan` of each repo checks the patch class list is complete; the layering scan of the API repo adds the
   rule that the library names no mount.
7. **Releases:** a new API minor (internal move, public API unchanged), each QOL mod a minor (a new window), the
   Workshop/Nexus texts' Settings sections rewritten (Tech Tree Search's Workshop text says "cfg file" today).

Order of work: 1 to 3 in the API repo first, with F11 green and the Saddle Up window unchanged on screen (the user's
look); then 4 to 6 with Tech Tree Search as the first consumer; the other QOL mods after.

## 6. Is it worth it

For: every QOL mod gets the same in-game settings as Saddle Up, on Steam too, where the framework does not exist;
one window code to maintain (the Mods-menu link stays optional); the design the user asked for on 2026-10-30 ("no
compromise on controls") reaches the other mods for free. Against: a refactor of ~8,000 lines' worth of files in the
API (most of it moves, little changes), the Settings-window button layout for several mods (new design), and each
QOL release carrying ~200 KB more. The QOL mods' settings are few, so the window shows one short section each, which
is fine; Event Decorations with ~18 toggles in three sections is the one that benefits most.

The trade-off between the two ways, plainly: with copies (B), a fix to the window is N rebuilds and N releases, one per
mod carrying the code, where a library (A) is one release. The other side of that coin: the copies are independent,
an old copy keeps working until its mod's next release, and nothing forces every mod to update on the same day, which
a shared library does the moment one mod needs a newer member. The QOL mods release rarely (Tech Tree Search twice
since October), so a window fix reaching them with their next version is the normal rhythm, not a cost.

Not worth it as a runtime DLL (§3): the loader's version rule and the Workshop's shared-path rule turn one library
into a fourth kind of item with its own compatibility contract, for no gain a player sees.

## 7. Open questions for the user

1. The home of the shared sources: a third repo as a submodule (recommended), or a folder in the API repo.
2. The Settings window with several of our buttons: one button per mod, or one "GK2 mods" button opening a list
   (a shared holder the first copy creates). The list is more work and more room; per mod is the simplest.
3. Whether the QOL mods keep their framework link (as the API did) or drop it.
4. Which mod goes first (Tech Tree Search is the obvious one; Event Decorations gains the most).

## 8. Second pass: the concrete numbers and shapes (2026-11-13, later the same day)

### 8.1 Size, measured

IL bytes per type in `dist/GK2DonkeyAPI.dll` (Mono.Cecil, method bodies; the DLL has 1,177,368 bytes of IL in 991
types and 427,379 bytes of embedded resources; the rest of its 3.4 MB is metadata and strings):

| Part | IL bytes | Goes to the library |
|---|---|---|
| Window core (`UiKit`, `UiSounds`, `RowControls`, `OptionsWindow`, `SettingsButtons`, `GameWindowAside`, the muffle patch, `KeyCapture`, `DeferredSave`) | 36,213 | all |
| `Texts` + `TranslationFiles` | 27,528 | all |
| `MountSettingsPage` | 17,949 | about half (the generic rows) |
| `UiWords` + `Term` + `Word` | 5,641 | about a tenth (the window's ~20 words) |
| `MountsPanel` (for scale; stays) | 37,289 | no |

About 75 KB of IL. A DLL is roughly twice its IL (metadata, strings), so the library adds about 150 KB to a mod.
Tech Tree Search is 54,784 bytes today with 12,742 bytes of IL (its framework link 509); with the window it lands
near 200 KB. The API DLL loses nothing (its copy stays inside).

### 8.2 The Settings window with several of our buttons: no coordination needed

`SettingsButtons.AddButtons` (:140-192) copies the window's own OK button, cleans it, and inserts the copy at the OK
button's sibling index, so each copy of the code puts its button just above OK and the earlier ones move up. Its
watchdog (`AllThere`, :116-124) checks only its own buttons. Two or six copies therefore stack their buttons above OK
with no shared state; the order is the plugins' load order (alphabetical by GUID within BepInEx's dependency order:
`dreas.gk2.builtcount` before `dreas.gk2.donkeyapi` before `dreas.gk2.techtreesearch`). Each button reads
"<Title> options" in the window's language. The one thing to measure, not design: the Settings window's height with
six buttons above OK on a 1080p screen (the window refreshes its content fitter; whether it still fits the screen is a
look, in game, with the five QOL mods and Saddle Up). Decision 2 of §7 is thereby answered: one button per mod, and
the "GK2 mods" list only if the measurement says the window overflows.

### 8.3 The one shared scene object

Three places in the code assume that the only windows of ours are this assembly's:

| Place | Today | With copies |
|---|---|---|
| `GameWindowAside.LetPauseGo` (:240-256) | unpauses when this copy paused and no game window wants the pause | would unpause under another copy's window |
| `GameWindowAside.ReleaseControls` (:176-190) | gives the player's controls back unless a game modal window is open | would give them back under another copy's window |
| `KeyActions.Poll` (:104), `windowOpen = MountsPanel.IsOpen || OptionsWindow.IsOpen` | the mods' polled keys wait while a window of ours is open | the API's L (the journal) would open over Tech Tree Search's options |

Separate assemblies share no statics, so the shared fact lives in the scene: one `GameObject` named `GK2Ui.open`
under the game's UI canvas (the Settings window's parent, which every copy reads anyway), made by whichever copy opens
first, found by name by the others (`Transform.Find` on the canvas; the object stays active, so `GameObject.Find`
works too). While a copy's window is open it keeps a child named by its owner's GUID; it removes it on close. The
rule each copy follows: pause and take the controls when the holder goes from empty to one child; unpause and give
the controls back when it goes to empty; a copy refuses to open while the holder has a child that is not its own; the
polled keys wait while the holder has any child. No component type is needed on the object (a component of copy A's
type is unknown to copy B), only names and child counts. The self-test for it: two pages in one build cannot show it
(the API has one copy); it is a hand check with Tech Tree Search built as the first consumer, plus one F11 scenario
in the API that fakes the holder (a child added by hand) and asserts the refusal and the keys waiting.

### 8.4 The hooks an owner gives the library

What the mount couplings of §1 become, as one small record the owner fills at start:

```csharp
internal sealed class UiOwner
{
    internal string Guid;                       // object-name prefix "GK2Ui <guid> ...", Texts owner, the holder's child name
    internal ManualLogSource Log;               // was Plugin.Log
    internal Func<bool> InGame;                 // was Mount.IsGameReady; a QOL mod gives () => MainGame.Instance != null && MainGame.Instance.gameState == InGame
    internal Func<bool> MayOpen;                // was !MountsPanel.IsColoursOpen; null = always
    internal Func<bool> IsUnderShown;           // was MountsPanel.IsShownOnTop (a window of the owner's under the options)
    internal Action HideUnder, ShowUnder;       // was MountsPanel.HideUnderOptions / ShowAfterOptions
    internal Func<ConfigEntry<KeyboardShortcut>, string> OtherKeysOf; // the API's KeyActions: the mod's own key rows that clash; null = none
}
```

`KeyActions.SameKey` (KeyActions.cs:309-318) is pure (it compares two `KeyboardShortcut`s by reflection on their
modifier array) and moves into the library as it is; the API's `KeyActions` keeps calling it. `DeferredSave.Tick`,
`GameWindowAside.Tick`, `SettingsButtons.Tick`, `KeyCapture.Tick` and `OptionsWindow.Tick` become one
`UiHost.LateUpdate()` the owner's plugin calls from its own `LateUpdate` (the API's `Plugin.cs:827-839` already does
exactly this sequence; a QOL mod adds the one line). `Row.OpensPanel` becomes a row flag the owner sets on its own
button rows ("this button opens my under-window"); the API sets it on `PanelButton`.

### 8.5 The page split, exactly

`Api/MountSettingsPage.cs` (1,585 lines) splits into:

- **`SettingsPage` (library, ~830 lines):** `Row` with its generic fields, `RowKind`, `Bind`, `MovedKey`, `RetiredKey`,
  the upgrade claims (`UpgradeClaim`, 263-394), `Toggle`, `Choice<T>`, `List`, `Steps`, `Key`, `Text`, `Info`,
  `Status`, `Heading`, `Button`, `EnableWhen`, `Publish` (minus `FrameworkLink`), `Guard`/`Add`, `InOptions`, `All`.
- **`MountSettingsPage` (API, public, unchanged surface):** wraps a `SettingsPage` and keeps every public method the
  frozen surface lists (`tests/ApiSurface/GK2DonkeyAPI.Api.txt` holds it; the test fails on any removal), delegating
  the generic ones and keeping `MountRows`, `FoalRows`, `Color`, `Preview`, `PanelButton`, `LookButton`, `OpenPanel`,
  `PinnedBy`, `TuningKey`, `CartRow`, `RegionRows`, `ToneRows`, `PerMount`, `PerSkin`, `SelectMount`, `SavePicked`.
  `Row`'s mount fields (`MountId`, `SkinId`, `SkinPart`, `CartPart`, `ArtRegion`, `ArtTone`, `ChoiceNotes`) move to a
  dictionary the API keeps, keyed by the row. `FillList` (:726-727, `MountsPanel.NameFromId`, `Looks.Find`) becomes a
  `Func<string, string>` on the `List` row the owner supplies.
- `Texts`: the owner id is a field, not `Plugin.Guid`; the mount branch of `EnglishOf` (:837-879) becomes an owner hook
  `Func<string, string> EnglishOf` (null for a QOL mod); `TickWarm`'s `Mount.IsGameReady` becomes `InGame`.

### 8.6 Tech Tree Search as the first consumer, sketched

Its settings today: `General.Enabled`, `General.SearchUnlocks`, `General.MaxResults` (1-20), `General.FocusKey`,
`Layout.BoxOffset`/`BoxSize`/`ListWidth` (Vector2 and float: no row kind, they stay cfg-only, as they are off the
framework's menu today), `Safety.StopOnChangedGameCode`. The page, in place of the 108-line `FrameworkLink.cs`:

```csharp
var ui = new UiOwner { Guid = Guid, Log = Log, InGame = () => MainGame.Instance != null && MainGame.Instance.gameState == MainGame.GameState.InGame };
SettingsPage p = SettingsPage.Make(ui, Config, "GK2 Tech Tree Search", "dreas", "A search box on the Tech Tree page.", status: GameDependencies.StatusText);
p.Toggle("General", "Enabled", Enabled.Value, "Search box on the Tech Tree", "Show the box at the top right of the Tech Tree page.", v => Enabled.Value = v);
p.Toggle("General", "SearchUnlocks", SearchUnlocks.Value, "Also search what techs unlock", "A tech is listed when an item, building or perk on its card matches.", v => SearchUnlocks.Value = v);
p.Steps("General", "MaxResults", MaxResults.Value, 1, 20, 1, "Rows in the list", "Further matches are counted in a last row.", v => MaxResults.Value = v); // 20 values: a slider
p.Key("General", "FocusKey", FocusKey.Value, "Key to start a search", "Puts the cursor in the box while the Tech Tree page is open.", v => FocusKey.Value = v);
p.Toggle("Safety", "StopOnChangedGameCode", StopOnChanged.Value, "Stop when the game's code changed", "After a game update, stop instead of running with a warning.", v => StopOnChanged.Value = v);
p.Publish();
```

Then one line in the plugin's `LateUpdate`: `UiHost.LateUpdate();`. What the player gets: "GK2 Tech Tree Search
options" above OK in the game's Settings window (main menu and pause menu), a window with two sections (General,
Safety) in the game's look, the key box with clash names, pause and muffle in game, the gamepad. The mod's own
`Ctrl+F` polling is the mod's (it reads `FocusKey` as before); the library only edits the entry.

### 8.7 Build plumbing, exactly

A `GK2Ui.targets` next to the shared sources, imported by each consumer's csproj like `DependencyManifest.targets`:

```xml
<ItemGroup>
  <Compile Include="$(MSBuildThisFileDirectory)src\**\*.cs" Link="GK2Ui\%(RecursiveDir)%(Filename)%(Extension)" />
  <Reference Include="Unity.TextMeshPro"><HintPath>$(Managed)\Unity.TextMeshPro.dll</HintPath><Private>false</Private></Reference>
  <Reference Include="UnityEngine.UI"><HintPath>$(Managed)\UnityEngine.UI.dll</HintPath><Private>false</Private></Reference>
  <!-- the consumer already references Assembly-CSharp, LazyBearTechnology, UnityEngine, CoreModule, UIModule, BepInEx, 0Harmony -->
</ItemGroup>
```

The UI files use `System`, `System.Collections.Generic`, `System.Text`, `System.IO`, `System.Globalization`,
`LazyBearTechnology`, `UnityEngine`, `UnityEngine.UI`, `TMPro`, `BepInEx`, `BepInEx.Configuration`,
`BepInEx.Bootstrap`, `HarmonyLib`: everything the QOL csprojs reference already, plus `Unity.TextMeshPro` (Tech Tree
Search has it; Owned Count and the others add it). The code compiles as `internal` in namespace `GK2Ui` into each
assembly; two assemblies holding the same internal type names is ordinary .NET. The home: a repo `GK2Ui` added as a
git submodule at `lib/GK2Ui` of both repos (the user's submodule commands; a path reference across repos would break
on any other clone). The `Languages\*.csv` of a consumer are optional: the library's ~20 window words and the page's
labels fall back to the English in the code; the API keeps its 11 files.

Two side effects to know: each consumer's dependency manifest (the DepManifest tool lists every game member a DLL
uses) grows by the UI's game members (`UIGameSettingsWindow`, `UIDialogWindowButton`, `LazyWindowsStackController`,
`MainGame`, `PlayerController`, `LazyInput`, `LazyAudio`...), so a game update that changes one of them marks that mod
"changed" too (the QOL policy: it runs with a warning; the window code guards every game call and falls back to the
cfg). And `tests/FrameworkScan` of each repo, which checks the patch class list is complete, sees the muffle patch
class in every consumer: each plugin's `PatchClasses` lists it.

### 8.8 What stays green

The API's three options scenarios (`OptionsTests.Smoke`, `WayBack`, `AmongOtherMods`: SelfTest.cs:206-208) and the
five panel ones (:199-205) are the acceptance test of steps 1 to 3: F11 green and the Saddle Up window pixel-identical
on screen (the user's look) before any consumer is touched. `tests/ApiSurface` holds the public surface. The QOL mods
have no in-game test runner; the first consumer gets a log line per window open (sections, rows, scaled or not: the
API's `Open` already logs it) and the hand check of §8.3.

### 8.9 The steps, sized

| Step | Where | Size |
|---|---|---|
| 1. `UiOwner` + `UiHost`, the dozen couplings cut, `SameKey` moved, names prefixed by GUID | API | the largest: ~40 edits across 8 files, no new behaviour; F11 options + panel scenarios green |
| 2. `SettingsPage` split out of `MountSettingsPage`; the row side table; `FillList` hook; `Texts` owner field + `EnglishOf` hook | API | ~830 lines move, ~100 change; ApiSurface unchanged |
| 3. `UiWords` split (20 words out), `Texts`/`TranslationFiles` moved whole | API | mechanical |
| 4. The holder object (§8.3) + its F11 scenario | API | ~80 lines |
| 5. Sources into the `GK2Ui` repo + targets; the API builds from the submodule | both repos | plumbing; `release.sh` and the scans unchanged |
| 6. Tech Tree Search's page (§8.6), `UiHost.LateUpdate`, the muffle patch class listed; hand check with Saddle Up installed (two copies: the holder, the two buttons, the window's look) | QOL | ~60 lines; the look is the user's |
| 7. The Settings window with six buttons: the measurement of §8.2 | QOL (all five pages on) | a screenshot |
| 8. Releases: API minor (the move; the surface unchanged), Tech Tree Search minor, the texts' Settings sections | both | the usual |

Steps 1 to 4 are one block to do and test together in the API repo; 5 to 7 the consumer block.

## 9. Block 1 built (2026-11-15)

Steps 1 to 4 of §8.9, in the API repo, as one block:

- **The library folder** `src/GK2DonkeyAPI/GK2DreasUI/`, namespace `GK2DreasUI`, compiled into the API by the csproj's
  default glob (a `global using GK2DreasUI;` in `GlobalUsings.cs` puts it in scope everywhere in the API). Moved whole,
  with their owner hooks in place of the API's statics: `UiKit`, `UiSounds`, `RowControls`, `OptionsWindow`,
  `SettingsButtons`, `GameWindowAside` (with the muffle patch), `KeyCapture`, `DeferredSave`, `Texts`,
  `TranslationFiles`, `LegacyText`. New: `UiOwner` (the record of §8.4, as static fields: the GUID, the logger, in-game,
  the owner's window under the options, the status text, the key poller, the key alias, the List row hooks, the dev
  and translation switches, the owner's words and more English, the English stamp), `UiHost` (the next-frame queue,
  the one `LateUpdate` that ticks the windows, the cfg writes and the read-ahead, `OnQuit`, and the shared scene
  object of §8.3), `Words` (`Term`, `Word` and the window's 21 words; `Term.Owner` is the owner's GUID when none was
  given, so the words need no owner at static-init time), `Keys` (the shortcut array accessor and `SameKey`, moved
  from `KeyActions`), `SettingsPage` (the generic page: ~830 lines of the old `MountSettingsPage`, plus `Tag`,
  `KeyTails`, `Untranslated` and `OpensOwnerWindow` on `Row`, `PendingShown`/`PendingTag`/`PendingKeyTails` round an
  owner's builder, `BeforeShow`/`AfterShow` round the showing, `FileSections`/`InFile`/`RowOfKey`/`MovedFrom` for the
  owner's carry-overs).
- **The API side**: `Api/MountSettingsPage.cs` is the wrapper (every public method kept, the surface test unchanged;
  `MountRowInfo` on `Row.Tag`, read through `MountSettingsPage.Info(row)`; `All` kept as the wrappers' list beside
  `SettingsPage.All`); `UiOwnerSetup.Apply()` fills `UiOwner` at the top of Awake (the mounts' and skins' English and
  owners moved out of `Texts` into it); `UiWords` keeps the mount words and aliases the window's (the same `Word`
  objects, so `UiWords.All` and the English file list each key once); `Plugin.LateUpdate` calls `UiHost.LateUpdate()`,
  `Plugin.NextFrame` forwards, the quit flush is `UiHost.OnQuit()`; `MountRegistry.Tick` no longer ticks the cfg writes
  and the read-ahead; `KeyActions` calls `Keys.SameKey` and lets the mods' keys wait while another mod's copy has a
  window open; `MountsPanel`, `FrameworkLink`, the Dev tests and the integration-test plugin name `SettingsPage.Row`
  and pass `page.Page` where the library is called.
- **Names**: every object of ours is `GK2DreasUI <owner guid> <window> <part>`; the template filters test the common
  prefix; the tests find parts by `UiOwner.NamePrefix`.
- **The shared marker** (§8.3) is built: `GameWindowAside.TakeControls` marks the copy open, `ReleaseControls` marks
  it closed and leaves the controls taken while another copy is open, `HoldPause` marks the pause, `LetPauseGo` leaves
  the pause to the last copy and unpauses whichever paused; `OptionsWindow.Open` refuses over another copy's window
  (logged); `KeyActions.Poll` waits. One F11 scenario, "settings: another mod's window open (the shared marker)",
  pretends the other copy with a child on the marker and checks the refusal, the pause and the controls across two
  opens.
- **Checks**: the API, Saddle Up, the three packs, the dev tools and the three examples build with 0 warnings; the
  release scans 28 of 29 before the translation scan got the library's namespace (its rerun below); the layering scan
  has a third rule, the library's code (comments stripped, identifiers by case) names nothing of the API or of mounts.
  F11 in game: the user's run.
- **Not yet** (block 2, §8.9 steps 5 to 7): the sources into the GK2DreasUI repository with a targets file, the
  submodule in both repos, Tech Tree Search as the first consumer, the six-button measurement.

### 9.1 The in-game run (2026-11-15)

F11 full: 2129 then 2143 passed, 0 failed, 0 warnings beyond the self-tests' own. Two defects the suite did not catch,
both seen by the user's eye in the Mount Journal, and both now covered by checks:

- **The colours window lost its mount tab** (only "Armored" and "Cart"): the wrapper gave every row of one per-skin
  build the same mount-data object, so the skin's tone rows marked the mount's own colour rows as skin parts too.
  Fixed: the pending tag is a factory, one object per row (`SettingsPage.PendingTag`). New scenario "settings: a row's
  mount data is its own" checks every row's data against its key.
- **The journal's picture lost the cart and the lantern after a full run**, from the Measurements group's skin-offset
  scenario on. Not the library's doing: the stage's mount-alone photo switches the cart parts off with a list to switch
  them back; Unity's willRenderCanvases fires inside that photo, the journal's `Refit` runs there and, the screen density
  it wanted having changed, asked `WantDetail`, which photographed the other view inside the first; the inner photo
  cleared the shared list, the outer restore put nothing back. Found with a stack trace from a probe in `RenderLit`
  after two probe runs. Fixed in `PreviewStage`: a detail asked during a photo waits for the next tick (`detailPending`),
  a nested photo keeps a list of its own and is said in the log (`renderDepth`). Kept, dev-gated: the journal's picture
  log line at every open (asked with the cart or not, the mount wanted, the view drawn, the stage's cart parts, the cart
  in the picture by pixels, the framing and camera facts, each part's state, material and place), a once-a-second
  watchdog for a cart renderer off between photos, and in the self-test runner the stage's cart parts and the pixel
  verdict compared before and after every scenario (warnings name the scenario; a "last drawn after" range covers the
  stage dressed as a cartless mount meanwhile). PanelSmoke checks a cart-pulling mount's journal picture is the cart
  view. CHANGELOG has the fix under For players: the same chain can run in play.

Block 1 is done. Left for the user: one more full F11 (the new checks in the whole run), then block 2.

## 10. Block 2 built (2026-11-15)

- The sources are the repository GK2DreasUI (commit 47c7b8d: `src/*.cs`, `GlobalUsings.cs`, `GK2DreasUI.targets`,
  `README.md`), a git submodule at `lib/GK2DreasUI` of this repository and of GK2QOLMods (the user's git). The API's
  csproj imports the targets; the in-repo folder is gone. The targets add TextMeshPro, UnityEngine.UI and
  UnityEngine.TextRenderingModule when a project lacks them (a throwaway project with a plain mod's references
  compiled the sources alone: 0 errors). The layering scan's rule 3 reads `lib/GK2DreasUI/src`.
- GK2 Tech Tree Search is the first QOL consumer (GK2QOLMods, `src/GK2TechTreeSearch`): the csproj imports the
  targets (plus `Sirenix.Serialization`, compile-time, for `PlayerController` in its in-game test, the same test as
  `Mount.IsInGame`); `Plugin.Awake` fills `UiOwner` (Guid, DisplayName, Log, InGame, StatusText) right after the
  logger; `BindConfig` makes the page first (`numberSections: false`), binds its entries as before, then declares the
  rows over them (Status, Enabled, SearchUnlocks, MaxResults as Steps 1-20, FocusKey without `pressed:`,
  Safety.StopOnChangedGameCode): BepInEx returns the entries already bound for the same section, key and type, so the
  window edits what the mod reads and the cfg keeps `[General]`, `[Layout]`, `[Safety]`. `Publish()` after the
  framework registration; `LateUpdate` and `OnApplicationQuit` call `UiHost`; the muffle patch is in `PatchClasses`.
  The DLL: about 60 KB to 258 KB. The Mods menu rows (GK2 Mod Framework) are unchanged; pointing the framework's
  Settings button at the window (the API's `FrameworkLink.PatchSettingsButton`) is about 60 lines per consumer and a
  decision for later.
- Two library changes, made in the QOL submodule's working copy (the user commits there and moves this repository's
  pointer): the `numberSections` constructor parameter (`Sec` returns the plain name; the old-copy and moved-key logic
  is unchanged, a plain name is its own `PlainName`), and 39 hook members of `UiOwner`, `SettingsPage`, `Texts`,
  `TranslationFiles` and `Words` as auto-properties instead of fields: a consumer that sets few hooks got CS0649
  "never assigned" for every other one (78 warnings in Tech Tree Search's build), and a property is assigned, invoked
  and used in an object initialiser the same way (no `ref` use anywhere, checked). The API and its IntegrationTests
  compile against the changed files (copied in, built without deploying, restored).
- Checks: Tech Tree Search builds with 0 warnings; the QOL release scans are green after the FrameworkScan's rule 2
  learned the exception (a `LateUpdate`/`OnApplicationQuit` whose calls all go to `GK2DreasUI.UiHost`); CecilScan
  resolves the window code's 93 game references; the embedded manifest has 139 entries (49 before): every game member
  the window code uses is now under the mod's one dependency policy (a changed member is a warning at start, or a
  stop with `Safety.StopOnChangedGameCode`), where the API decides per feature. Not yet: the window seen in the game
  for Tech Tree Search, the two-copies check with Saddle Up installed (one holder object, two buttons in the Settings
  window), a full F11 here.

## 11. Translations for the other mods (studied 2026-11-15; nothing built)

The user's question: can the localization system of Saddle Up and the API go into the library, so the other mods use
it. This revisits §2 and §8.7, which left the QOL mods English-only with the `Languages\` folder optional.

### 11.1 What a consumer has already

The translation code moved with the windows in block 1 and is compiled into every consumer: `Texts` (the lookup at
draw time, the folders, the report, the test language), `TranslationFiles` and `LegacyText` (the reader of what
translators do to a file), `Term`/`Word`/`Words` (a mod's own words, templates). `UiHost.LateUpdate` ticks
`Texts.TickWarm`. A consumer's page rows, sections and title are looked up under its own GUID
(`Texts.Of(owner, key, english)`), the Settings button retitles on a language change. Nothing of the reader is the
API's any more; the API adds only its mounts' and skins' English through `UiOwner.MoreEnglish`/`MoreOwners`.

### 11.2 Why Tech Tree Search's window is still English in every language (read from the code and the folders)

| # | Gap | Fact |
|---|---|---|
| G1 | The window's own 21 words have no translation in a consumer | their owner is the mod the windows are compiled into (`Term.Owner` = `UiOwner.Guid`); their 10 translations exist only as 21 of the 193 lines of the API's `Languages\<id>.csv`. `FilesFor` reads only `<lang>.csv` in a folder (a second file, `de.window.csv`, is refused and said). |
| G2 | The QOL dev deploy is loose | `plugins\GK2TechTreeSearch.dll`: `Texts.BundledFolder` is `Languages\` beside the DLL, so all five loose mods would share `plugins\Languages` and each warn about the others' keys. The release zips are already a folder per mod. |
| G3 | No English template | `Texts.Published` writes `BepInEx\GK2-l10n\<guid>\en.csv` only when `UiOwner.DevUnlocked` says so; no QOL mod sets it (nor `TestLanguage`, `TranslationReport`). |
| G4 | The mods' texts outside the page are literals | Tech Tree Search: "Search...", the statuses ("Active", "Off (...)", made in `Awake`, where the language is always English); Owned Count: "Owned: " glued to a number; Event Decorations' notices. |
| G5 | The pipeline is the API repo's | `tools/l10n-sync.py` names `src/GK2DonkeyAPI`, `src/SaddleUp`, `packs/*` and carries the API's own check (a bubble said with a literal); `DeployToGame.targets`, `make-release-zip.py` and `release-versions.py` carry `Languages\` there; the QOL zip maker and its Workshop staging ship the DLL alone. |
| G6 | The tests are the API's | the mistake catalogue (`TranslationTests.RunCatalogue`, 6 references to the API in it), the in-game Translations group, `tests/TranslationScan` (built against the API's DLL). |

### 11.3 Second pass: measured (2026-11-15; the user: "investigate these findings more before we make decisions")

Two throwaway programs against the reader as built (not repo tools), the tools read line by line, an inventory of the
five QOL mods' texts by a subagent (spot-checked), the folders of the game.

**The reader with a consumer's files** (a harness on the API's Release DLL, through `Texts`' own test handles):

| | What | Result |
|---|---|---|
| E1 | a file holding only the window's 21 words, cut from the API's file, for an owner whose English is those words; all 10 languages | 21 of 21 shown in each, 0 warnings |
| E2 | a small mod (12 own lines + the 21), its file the 21 only | 21 of 21, its own 12 English, 0 warnings, in all 10 |
| E3 | the same with its own lines still the English in the file (12, then 60 of them) | 21 of 21, 0 warnings (the "copy of the English" rule touches only the lines still English) |
| E4 | mod B reading a `Languages` folder whose `de.csv` is mod A's (the loose deploy) | never a wrong text (A's `page.title` has another English: English shown), but a warning: "has 11 key(s) the mod doesn't have"; and the two mods' files have the same name, so one overwrites the other |
| E5 | a translator's `Translations\<guid>\de.csv` with 3 of the mod's lines over the bundled 21 | 3 + 21 shown: files merge key by key |
| E6 | times | parsing the API's 193-line file 0.45 ms, a 21-line file 0.11 ms; a small mod's whole table 0.35 ms, 1.04 ms with the search of the game's 12 plugin folders for `Translations\`. One read per mod and language change, one mod a frame (`TickWarm`). |

**The baseline before anything moves**: `tests/TranslationScan` on today's build: ALL PASS, the catalogue 231 of 231,
143,000 reads of the 55 shipped files saved wrong ways with 0 failing, 7 min 27 s (the headers phase alone 153 s). Its
corpus phase was skipped: `GK2_L10N_CORPUS` names no folder (the game's tables lie in the 2026-10-31 session's job
folder, nowhere durable). Its build warned of an unused variable (`refused`, CS0219): removed.

**The library tests itself, tried**: the mistake catalogue (415 lines cut from the API's `Dev/TranslationTests.cs`: the
cases and `RunCatalogue`, without the in-game wrapper) compiled with the library's sources alone through
`GK2DreasUI.targets`, no API in the process: 0 errors, 0 warnings, **231 of 231**. The layering scan's library rule
run over the cut file finds one line (a test value, "donkey", in the noun case): one word to change. Moving it is
mechanical; the API must still compile it (the F11 Mistakes scenario and the offline scan run it), so it sits outside
`src\` and a consumer asks for it. The offline scan's own ties to this repo are three lines (the stale-build check,
`src` and `packs`).

**The sync tool, read whole** (363 lines): about 45 are this repo's (the three source folders; the check for a bubble
said with a literal). About 150 are a second implementation of the reader's parsing in Python (`clean`, `sniff`,
`columns`, `is_key`, `damaged`, `foreign`, `csv_rows`: "as the mod reads them"), so the tool is tied to the reader's
version and belongs where the reader is: a copy in the QOL repo would drift from it. `l10n-codepages.py` writes
`LegacyText.cs` (library source); `l10n-glyphs.py` lists characters the game's own text never draws.

**The check was failing, found on the way**: `l10n-sync.py --check` said the API's `en.csv` differed from the last dev
run's (2026-11-15 04:22, the first after block 1): `row.open`'s note reworded for the library and `row.no_choice`
moved up among the window's words. Notes and order only, no translation touched; synced (the standing step), all five
"same" again, against that dump: the next dev start confirms it. It shows the check does catch a change of the
library's words.

**The window's words over time**: made on 2026-11-01, their English not changed since (git, the 21 keys in `en.csv`);
block 1 changed one note. They are 21 of 26 keys under `options.`, `row.`, `key.` in the API's file: the API has five
of its own under the same prefixes (`row.skin`, `row.babies`...), so the library's words are told by its list, not by
a prefix. A 21-line file is 1.4 to 1.8 KB a language, 15 KB for the ten (the layering rule finds nothing in them).

**The library's English is not only the 21 words** (its sources searched for literals that reach a window): the
default Status row, `SettingsPage.Status()`, has a label and a description of its own ("Status", "What the mod is
doing right now."), looked up under the page's keys (`info.status.label`, `.desc`). No shipped file has them (Saddle
Up's page has no Status row), so they were never translated: two lines to write in the 10 languages, and part of the
common set, 23 lines in all. The key names (Shift, Ctrl, Enter, Num 1) are literals by design, English in the API's
windows too.

**The dev switch exists already**: the API's dev tools unlock by a marker file, `BepInEx\config\dreas.gk2.dev` (on the
dev machine since 2026-10-13). A QOL mod can read the same file for `UiOwner.DevUnlocked`: no cfg entry, nothing a
player can switch on. The translators' report is another matter: in the API it is a cfg entry (`[Translations] Report`).

**The five mods' texts** (the subagent's inventory, file and line for each; counts are texts a player sees):

| Mod | Uses the library | Page rows + descriptions | Texts it draws itself | Through what | Glued |
|---|---|---|---|---|---|
| Tech Tree Search | yes | 8 + 5 | "Search...", "No visible tech matches", "+N more: type more letters" | its own TMP labels, the font read once from a page label | "+" + n + " more..." |
| Event Decorations | yes | 39 + 31 | none (the "On today" line is an Info value) | - | the "On today" line, from about 8 pieces |
| Owned Count | no | (7 + 7 in the Framework menu) | "Owned: N (all grades: N)", "on you N · in storage N (N chests)", the per-area line | the game's tooltip widget and style | all three, with an English plural (chest/chests) |
| Global Storage | no | (5 + 5) | none | - | - |
| Built Count | no | (1 + 1) | " (N)" after the game's name, no words | the game's own label | - |

About 120 lines for the five (Saddle Up and the API: 291), 70 of them Event Decorations'.

Three findings from it:

- **G7: a status is drawn raw.** `RowControls.InfoValue` shows what the owner's function returns
  (`RowControls.cs:461`), so a Status or Info row's value never goes through `Texts`: the mods' statuses, Event
  Decorations' "On today", and the API's own status line (`GameCode.Status`, literals) are English in every language
  today. And `Plugin.Status` is
  program logic in all five mods (compared with "Active"): a status can only be translated where it is drawn.
- **G8: the five mods share texts of their own.** `src\Shared\GameDependencies.cs` makes the same five status
  sentences in each (with a count and "member(s)"), and four of them say `Off ("<a row's label>" is unchecked)`,
  quoting a label that would be translated. The same question as the window's words, one level up: one set of words,
  several owners.
- **Fonts.** Owned Count and Built Count write into the game's own widgets (the game's font for the language). Tech
  Tree Search's result rows are new labels with a font read once from a label of the page (`SearchBox.cs:69`; a
  comment there says that font has no check mark). The rows already show the game's own tech names, so with the game
  in Japanese, Chinese or Korean they either work today or are already broken: not known, to see in the game (one look
  with the game in Japanese, before any translation of "Search...").

**The Framework's menu**: its translations are JSON in the Framework's own folder,
`plugins\GK2.Framework\Localization\<mod id>\<lang>.json`, keys `mod.description`, `sections.<Section>`,
`settings.<Section>.<Key>.name` and `.description` (seen: another author's mod ships 11 languages there; the
Framework's language ids differ from the game's: `pt`, `zh`, `it`). The labels and descriptions our mods pass are the
same sentences as their page rows (Tech Tree Search: 3 labels and 2 descriptions word for word), so such files could
be written from our CSVs by a tool, with no second translation. Not looked into: how a mod's zip puts files into the
Framework's folder.

**What it costs a mod**: the reader is already in every consumer (27.5 KB of IL; Tech Tree Search's DLL is 258 KB with
the library, 55 KB before); translations add files only.

**The translation-writing tools of 2026-11-01** (the validating writer, the review brief, the terms, the character
checks) lie in that session's temporary scratchpad, nowhere in a repo.

Not tried: writing a mod's `en.csv` without the game (a plugin can't be made outside Unity; the page would have to be
built by reflection). The dev start with the marker is the way that exists and works for every owner.

### 11.4 Third pass: what the second only reasoned about, tried (2026-11-15; the user: still more investigating)

Four more throwaway programs (job folder, not repo tools), the Framework and the game's font code decompiled, the
game's asset files read (UnityPy). Three statements of the second pass were wrong or untested; each is corrected here.

**A mod's English file without the game: works** (the second pass said "not tried", expecting it could not). A
console program loads the mod's built DLL, makes its plugin object without its constructor, gives it a scratch config,
calls its own `BindConfig` and asks the library copy inside that DLL (`Texts.EnglishOf`, `EnglishCsv`) by reflection:

| Mod | Lines written | Its own | Game assemblies it pulled in |
|---|---|---|---|
| Tech Tree Search | 36 | 15 (title, 2 sections, the Status row's 2, 5 labels, 5 descriptions) | Unity's only |
| Event Decorations | 97 | 76 | Unity's and Assembly-CSharp |

About 70 lines of code, no dev marker, no game start; the template comes from the build, so the check no longer
depends on the last game run. It needs a page built in one method from the config alone (both mods' are). A text the
mod draws itself appears once it is a `Word` listed through `UiOwner.OwnWords`. Not tried on the API (its mounts and
skins register in the game, and its packs' texts come with them); its dev start stays. Seen in the output: the translator's note of `row.none` says "no
babies", an API word in a library note.

**Embedding the window's words, built** (the second pass called it a change to the reader "for no gain"). In a scratch
copy of the library: the ten 21-line files as resources of the DLL through the targets file, read after every file by
the same reader and the same `Take`, only for the owner the copy is compiled into. 29 changed lines in `Texts.cs` (two
in `Load`, a new method), 14 in `TranslationFiles.cs` (`Read` split so bytes can be read), nothing in the parsing.
Results: the catalogue 231 of 231; an owner with no `Languages` folder at all shows 21 of 21 in all 10 languages with
no warning; a translator's line of a key wins over the built-in one; another owner gets nothing. The DLL grows by
18 KB. Not run on it: the offline scan's sweep, the in-game group.

| | Merged into each mod's files by the tool | Built into the DLL |
|---|---|---|
| Library sources | untouched | 43 lines around the reader, not in the parsing |
| A mod with no files | English | the window's words translated |
| Where the words live | 21 lines x 10 languages in every consumer's folder (and in git), kept alike by the check | once, in the library |
| A player changing a word | edits the mod's `Languages\de.csv`, as the README and the 0.15.0 note say | adds the line to that file (it is not in it) |
| Out of step with the code | possible (an old folder under a new DLL: English for the changed line) | not possible |
| Tools | the sync tool learns "a set of words written into every owner" | the sync tool's report learns the built-in lines are not missing |

The window's words alone give a window that is half translated (its buttons and hints, not its rows); the rows need
the mod's files and the pipeline either way. So building in saves no pipeline work for a mod that is translated at
all. Both work; the choice is where the 21 lines live.

**A status needs no library change** (the second pass said it did). The owner's status function runs each time the
row is drawn (`RowControls.InfoValue`), after the language is known: a mod that keeps its state apart from its words
returns `word.Text` or `word.Format(...)` there, and its words are listed through `UiOwner.OwnWords`. The API's way
for sentences that also travel as English (`Word.Make`, then `UiWords.Shown` finds the translation by the English:
35 lines, the ring of the last 8 for bubbles said within a frame) is not needed for a status kept as a word and its
values.

**The Framework's menu, decompiled** (`FrameworkLocalization`): it reads only
`BepInEx\plugins\GK2.Framework\Localization\<mod id, lower case>\<language>.json`, the language as the game's id in
lower case with `-` as `_` (`pt_br`, then `pt`, then `en`), keys `mod.name`, `mod.description`, `sections.<Section>`,
`settings.<Section>.<Key>.name` and `.description`. A status value is the mod's function, not looked up. So a mod's
zip would carry files into the Framework's folder; on the Workshop, where the Framework is not, that would leave a
`GK2.Framework` folder with no DLL, so such files belong in the Nexus zip only.

**Tech Tree Search's font: its result rows can't draw Japanese, Chinese or Korean, by the files** (not yet seen in
the game). The label it copies its font from (`redSpheresLabel`, the object "Red") has a text style component
(`staticFont` 0, no `StaticFontLabel`) whose style asset is `tiny_font-perk_techpoint_cost` with `isAlwaysStaticFont`
1. The game's `LazyFontData.GetFontAssetFor(lang, staticFont)` returns a language's own font only when not static; so
this label wears `tiny_font` in every language. `tiny_font`: 311 characters, a fixed atlas, no fallback fonts; it has
Latin, Turkish and Cyrillic letters and none of the Japanese, Chinese or Korean test characters (nor the check mark,
as the code's comment says). `small_font` (332) is the same. The rows show the game's own tech names, so this is not
about our translations: with the game in one of the three languages the released mod's result list is expected to
show boxes (TextMeshPro's global fallback list is in the player data and could not be read: the one thing that could
save it). The window library already makes labels in the language's font (`UiKit`, from the Settings row, restyled
for the language): the search box can take its font the same way, or a style from the page that is not static (by
the component's flag 291 of the page's 301 labels are not; their styles' own flags were not read). The game's Japanese,
Chinese and Korean fonts are not among the bundles' font assets (three found: `small_font`, `small_font_bold`,
`tiny_font`); the game loads them by path.

**Who plays in other languages: no numbers.** The store lists the 11 languages and 4,436 English reviews; the two
requests the rate rule allows gave no count per language.

### 11.5 Fourth pass: the decisions, one by one (2026-11-15; the user: "can we still investigate these decisions")

**D1, where the window's words live.** The whole offline scan, not only the catalogue, run on a DLL holding the
library alone, as it is and with the built-in words: both ALL PASS with the API baseline's counts (231 of 231; 3,015,
36,036, 4,314, 43,475 and 56,000 reads, 0 failing) and the corpus phase the baseline skipped (85 reads of the game's
own tables, 0 failing). So the built-in variant breaks nothing in the reading of files. What "the window's words
only" looks like, Tech Tree Search in German with either way and its own lines untranslated:

| Where | Shown |
|---|---|
| the button in the game's Settings window | "Optionen für GK2 Tech Tree Search" (German template, English name) |
| title, the tabs General and Safety, the six rows' labels and descriptions | English |
| the hint under the rows, the buttons | "Zeige auf eine Option...", "Standardwerte", "Fertig" |
| setting the key, the Defaults question | "Drücke eine Taste ...", "Zurücksetzen", "Abbrechen" |

**D2, the tests.** The scan's `Program.cs` runs on the library alone with two path lines changed and a four-line
stand-in for the API's class name; it took 2 min 19 s (the API's run, with other jobs going, 7 min 27 s). In the
game: of the ten Translations scenarios two are free of the API (Files, Mistakes); the other eight drive the API's
windows, bubbles and packs and stay where they are, and they are what covers the library in the game. The QOL repo
has no in-game self-test at all (its tests are offline scans and Harmony harnesses).

**D3, the mods' own texts.** Lines, from the build's own dump or the inventory: Tech Tree Search 15, Event
Decorations 76, Owned Count about 19, Global Storage 11, Built Count 3. Only 6 of their 77 short texts are words the
game has (Village, Tavern, Barracks, chest, Storage twice): nothing to reuse at run time, though the game's tables
give translators the terms. Room, against the last test run's measurements: their longest label is 32 characters
(Saddle Up's, measured with room for 1.8 times, 40); the description box (572 x 40) holds about 380 characters at
full size and at least 765 shrunk; their longest descriptions are 278 (Tech Tree Search) and 329 (Event
Decorations), Saddle Up's 255. None is cut in any language; at 1.6 times the length one of Tech Tree Search's and
four of Event Decorations' are drawn smaller.

**D4, statuses.** Saddle Up's page has no Status row, so no released window of ours shows a status: the only places
are the QOL mods' new pages and the Framework's menu. A player normally reads one word, "Active"; the sentences
appear after a game update, and the longest holds a list of game members no translation helps with. Each mod has
about 9 status texts, 5 of them the shared ones.

**D5, the Framework's menu.** A setting's key is `settings.<Section>.<Key>`, the cfg's own names, so a tool that
has the page (the English dump has it) can write the JSON. But where the Framework's Settings button opens our
window (Saddle Up and Event Decorations do that with a prefix on its `OpenSettingsPage`), its menu shows of the mod
only its name and description, and one button as a fallback: nothing of the rows. Tech Tree Search still registers
its rows there (7 of its 10 texts are in its CSV word for word); pointing its button at the window is the open
decision of its release. If the other mods follow, D5 is one sentence a mod, the description in the list.

**D6, the search box's font.** The page's 301 labels wear 30 styles: 19 follow the language, 11 always keep one
font, among them `tiny_font-perk_techpoint_cost` on the three point counters (Red, Blue, Green), the one the mod
copies from. All three font families of the page (`tiny_font`, `small_font`, `small_font_bold`) swap to
`Fonts & Materials/japanese`, `chinese`, `korean` for ja, zh_cn, ko when a label is not static. So the fix is small:
ask the same style's font data for the language's font (`GetFontAssetFor(lang, staticFont: false)`), or wear one of
the page's `tiny_font` styles that follow the language (`tiny_font-item_cost`, on its 14 price labels).

### 11.6 What the measurements change

- The window's words: merged or built in, both proven. Merged keeps the library's sources untouched and the promise
  "every text is in the mod's CSV"; built in keeps one copy and can't fall out of step. Recommended: merged, because
  every translated mod ships files anyway and players were told to edit them.
- If merged, the merge is general: "a set of words with a master file, written into every owner that carries them".
- The tool cannot be copied: it holds the reader's rules. It moves to the library with the master files.
- The tests can move at no risk (231 of 231 on the library alone, one test value to rename).
- The QOL deploy must become a folder per mod before the first `Languages` file is deployed (E4).
- The English template of a QOL mod can come from the build, with no game start and no dev marker.
- A status can be translated by the mod alone; whether it should is still a decision (it is English in the API).
- Owned Count's tooltip lines have to become templates without a noun after a number (the rule of 2026-11-01) before
  they can be translated.
- Tech Tree Search's rows need their font fixed first, translations or not; one look in the game in Japanese
  confirms it.

### 11.7 The steps, in the order the data gives

1. Library: `Languages/<id>.csv` (the 21 words x 10, cut from the API's files by a script, and the Status row's two
   lines, new), `tools/` (the sync tool made general, with the word sets; the English dump from a built DLL; the
   code-page and glyph tools; the 2026-11-01 writing tools saved from the scratchpad), `tests/` (the catalogue
   outside `src\` and the offline scan). No change under `src\` if the words are merged.
2. API: its tools become the library's; its literal check stays as its own step; proof as in the rule above (the scan,
   the check "same" five times, F11 Translations 362/0/0).
3. QOL pipeline: deploy by folder, `Languages\` in the zip and the Workshop folder, the dump, the check and the scan
   in `release-scans.sh`.
4. Tech Tree Search: its `en.csv` from the build, the window's words merged in: its window's own words in 10
   languages, its rows English until translated. A look in the game in German, and in Japanese for the font.
5. By decision: the mods' own texts (rows first; then the texts they draw themselves, after the templates and the
   font), statuses, the Framework's JSON.

### 11.8 The user's decisions

- D1: the window's words merged into each mod's files by the tool (recommended), or built into the DLL (both built,
  both pass the whole scan).
- D2: steps 1 to 4 now with the offline tests (the catalogue and the scan) moved in step 1 (recommended: run on the
  library alone, ALL PASS), or left in the API for now. The in-game scenarios stay in the API either way.
- D3: after step 4, which of the mods' own texts get the 10 languages: none yet; the page rows (Tech Tree Search 15
  lines, Event Decorations 76, about 100 over the five mods as they become consumers); or also what they draw
  themselves (Owned Count's tooltip, the search box).
- D4: statuses stay English (recommended: they are for support, and the API's is English too), or are translated
  (the mod's own work, no library change).
- D5: the Framework's menu stays English (recommended for now), or gets JSON written from our CSVs, in the Nexus zip
  only. It hangs on the release's own question, whether every mod's Settings button there opens our window: if so,
  only the description in its list is left to translate.
- D6 (new): the search box's font in Tech Tree Search: fix it with the next version whatever the rest (recommended,
  after the look in Japanese), or leave it.

### 11.9 Built, phase 1 (2026-11-16; the user: "ok lets do it")

Taken as said: D1 merged, D2 the offline tests with the tools, D4 statuses English, D6 the font fixed. D3 (which of
the mods' own texts) and D5 (the Framework's menu) are not decided: no new translation was written, the default
Status row's two lines included.

**The library** (edited in the QOL repository's copy, the one on `main`; this repository's copy is on the old commit
until its pointer moves, and nothing of this repository's own changed for it):

- `Languages/<id>.csv`, ten files: the window's 21 words, the lines cut from this repository's files byte for byte.
- `tools/l10n-sync.py`: the general tool. A repository says in `tools/l10n.json` where its plugins are and where
  their English comes from (`"game"`: the dev start's files, as here; `"build"`: `tools/EnglishDump`, below). It
  writes each plugin's `en.csv` and the window's words into its `<id>.csv` wherever the plugin's English has that key
  with that English (a line replaced, added at the end, or the file made; every other line and each line's own ending
  kept); `--check` fails where anything would change. The reader's rules it holds are the same functions as before.
- `tools/EnglishDump`: a mod's `en.csv` from its built DLL (exit codes for a stale DLL, a missing method, a page that
  can't be built without the game, a DLL without the window code).
- `tools/l10n-codepages.py` (it still named this repository's old path and namespace for `LegacyText.cs`; it now
  writes the library's, keeps the file's line endings and has `--check`: "same") and `tools/l10n-glyphs.py` (`--repo`).
- `dev/TranslationCatalogue.cs` (the catalogue; "donkey" in the noun case is "ox") and the opt-in in
  `GK2DreasUI.targets` (`<GK2DreasUIDev>true</GK2DreasUIDev>`); `tests/TranslationScan` (the scan's program with the
  repository as its argument, the library's own files read too).

**Proved on this repository's files, by path, before anything else** (`tools/l10n.json` is the one file added here):

| | Result |
|---|---|
| the library's scan on the 55 shipped files | ALL PASS: 231 of 231; 3,015, 36,036, 4,314, 43,475 and 56,000 reads, 0 failing (the baseline's counts); the corpus phase 85 reads, 0 failing; the library's own 10 files read clean |
| the general tool's `--check` | "same" five times, the window's words in step |
| the general tool writing | nothing changed: `git status` shows no language file of this repository touched |
| its 50 language reports against the old tool's | identical, line for line |
| the code-page tool's `--check`, the glyph tool's Japanese listing | the library's file reproduced exactly; the old tool's output |

**The QOL repository**: `tools/l10n.json` (Tech Tree Search and Event Decorations, English from the build); the
deploy by folder with `Languages\`; the zip and the Workshop staging with `Languages/`, the refusal of uncommitted
files and `--dry-run` (the writing tried in a scratch folder: 16 entries, 11 language files, each way); the sync
check and the library's scan at the end of `release-scans.sh`. Both mods have `Languages/` now: `en.csv` (36 and 97
lines) and ten files of the 21 words; deployed beside their DLLs.

**The search box** (`SearchBox.ReadFont`): the counter label's style is asked for the language's font
(`CurrentTextStyle.Font.GetFontAssetFor(language, false)`), each game call in a no-inline method inside a `try`; the
counter's own font stays the fallback; read again when the language changed; the dot between a tab and an unlock is
a hyphen where the language's font has none (the game's Japanese and Korean text never uses that dot); with the dev
marker one log line a language names both fonts and the letters of the first result's name each can't draw
(`HasCharacters` with fallbacks and adding, the test of this repository's Fonts scenario). Found beside it, not
fixed: 26 of the game's 51 Japanese tech lines and 32 of the 51 Chinese hold an invisible break mark (U+200B) inside
the name, and the search compares letter for letter, so the whole name typed finds nothing.

**Phase 2, after the pointer here has moved**: this repository takes the library's tools and tests (its
`tools/l10n-sync.py` becomes the literal check alone, `tools/l10n-codepages.py`, `tools/l10n-glyphs.py` and
`tests/TranslationScan` go, the catalogue leaves `Dev/TranslationTests.cs` for the library's `dev\` with the opt-in,
the layering scan reads `dev\`, `release-scans.sh` calls the library's), then F11 Translations. Until then both
copies exist and this repository runs on its own, unchanged.

### 11.10 Built, phase 2 (2026-11-17, after the pointer moved to the library's `d7d8eb6`)

This repository uses the library's tools and tests; its own copies are gone:

- `tests/TranslationScan`, `tools/l10n-codepages.py`, `tools/l10n-glyphs.py` and `tools/l10n-sync.py` deleted. What
  was the API's own in the sync tool, the check for a bubble or a prompt said with a literal, is
  `tools/l10n-literals.py` (the same patterns and walk, compared line for line with the old tool's before it went).
- The catalogue (413 lines) left `Dev/TranslationTests.cs`; `Mistakes()` calls the library's
  `TranslationCatalogue.RunCatalogue`. The project sets `<GK2DreasUIDev>true</GK2DreasUIDev>`; the
  `InternalsVisibleTo("TranslationScan")` is gone (the library's scan compiles the sources itself).
- `tools/release-scans.sh` runs the library's scan with this repository as its argument, the sync tool's merge test,
  `lib/GK2DreasUI/tools/l10n-sync.py --check` and `tools/l10n-literals.py`. `tools/layering-scan.py` reads the
  library's `dev`, `tests` and `tools` folders as well as `src` (all four: no higher layer named).
- `docs/release-checklist.md` section 2 and a table at the head of `docs/localization.md` say where each thing is now.
- The API and its IntegrationTests rebuilt (0 warnings) and deployed.
- `tools/release-scans.sh`: 31 of 31 green.

**The in-game run (the user, 2026-11-17 15:52, full F11): 2137 passed, 0 failed, 0 warnings.** All 187 scenarios
left their lines; no warning or error before the run's start, and the log's 55 are the tests' own. The mistake
catalogue, now the library's, 231 of 231 on the game's Mono. While the Translations group switched the game through
its languages, the two QOL mods' copies of the reader read their new files: Tech Tree Search "21 line(s) read ...
of its 36 lines", Event Decorations "of its 97 lines", in each of the 10 languages, no warning: the English written
from the builds without the game (36 and 97 lines) is what the game counts.

The total is 13 under the run before (2150, 2026-11-15), and which checks those were can't be said: the game's log
is overwritten at every start. What can be said: against the last full log that survives (Unity's
`Player-prev.log`, the run of 2026-11-11, 2108) every scenario makes as many checks or more, but one ("Saddle Up has
1 baby out: its own babies not compared"), and every check line added to the test code since shows in the counts
except three in the Settings scenario; the surplus of 2026-11-15 was checks that depend on the save. So that it can
be said next time, `tools/log-scenarios.py` counts each scenario's checks, compares two logs (`--diff`) and keeps a
run's counts for the next (`--keep`; today's are kept).

**For the user to look at**: Tech Tree Search's options window with the game in German (the window's words German,
its rows English; the log's "Translations: dreas.gk2.techtreesearch in de: 21 line(s) read ..." line), and its search
list with the game in Japanese (the names drawn; the `[dev]` line with both fonts).
