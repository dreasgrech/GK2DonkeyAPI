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
