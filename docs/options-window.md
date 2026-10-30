# Our own options window (investigation, 2026-10-30)

The user, 2026-10-30: GK2 Mod Framework is not on the Steam Workshop, so Workshop players won't have it. Drop the
soft dependency on it and show the options in a window of our own, built the way the Mounts panel's journal was; the
game's options get one button that opens it. Everyone then sees the same options, and there is one way to maintain
instead of two. The same day: "Make no compromise on controls. If some control is not available or not exactly like
how we needed it, we write our own ... the options are important."

Status: built and deployed 2026-10-30 evening (API 0.42.0, unreleased): §10. F11 and the hand checks are open
(docs/open-items.md "To check in game").

## 1. What there is today (read 2026-10-30)

A player mod's settings are a `MountSettingsPage` (API, `Api/MountSettingsPage.cs`). Saddle Up only declares rows
(`src/SaddleUp/Plugin.cs`) and never draws anything. The page is shown the frame after `Publish` (`Show`), in one of
three places:

| Where | Which rows | Code |
|---|---|---|
| GK2 Mod Framework's Mods menu, when it is installed | every row with `InMenu` true | `FrameworkLink.cs` (223 lines), `MenuLabels.cs`, `ModsWindow.cs`, part of `LookPreview.cs` |
| else the game's Settings window: a "Showing" row switches between the game's page and pages of the mod's rows, copies of the game's `UISwitchButton` row | the same | `SettingsRows.cs` (873 lines) |
| the Mounts panel (journal and colours window), always | the look rows (`look: true`) | `MountsPanel.cs`, `MountsPanel.Controls.cs` |

- **The dependency is the API's, not Saddle Up's.** The soft `BepInDependency` is in `GK2DonkeyAPI/Plugin.cs`
  (line 366). The `GK2.Framework` reference is in `GK2DonkeyAPI.csproj` and `Directory.Build.props` (`lib/GK2.Framework`).
  Saddle Up references the API, BepInEx and Unity only.
- **On the user's machine** the framework is installed; the log says "GK2 Saddle Up is in GK2 Mod Framework's Mods
  menu (288 rows)". 288 counts every row, the look rows of every mount and skin among them.
- **The rows the options window would show** (every row not `look`, read from Saddle Up's `Plugin.cs`; to be counted
  in game, §7):

| Section | Rows |
|---|---|
| Keys | the "Setting a key" note (`AddKeyHelp`), Mounts panel, Ride your mount, Drive the cart, Ring the bell, Call out, Send your mount away, Jump, Fly, Climb (hold), Descend (hold) (10 keys; Jump to Descend grey while flying is off) |
| Mount | Your mount (the picture, `Preview`), Mounts (opens the panel, `PanelButton`), Keep your mount with the save |
| Mount Behaviour | Say why you can't get on, Shuffles while standing, Allow mounting indoors, Allow mounting with the cart indoors (grey while the one above is off) |
| Cart | Zombies ride on the cart, A storage chest rides on the cart, Carry the corpse into the morgue, Donkey leaves corpses at the crossroads |
| Sound | Mount calls, Standing mounts and babies sound |
| Safe spot | Bring me back if I fall out of the world, Back to the last safe spot (a key), Back to the last safe spot (a button) |
| Freeplay | All mounts carry max cargo, All mounts have the same speed, Speed of every mount (shown only with the one above on) |

About 30 rows in 7 sections; Keys is the longest with 10.

## 2. Where the window's code goes

What draws a row is all in the API today: the controls (`OwnRow`: switch, number buttons, slider, dropdown, text
box), the window frame and title bar (`Window`, `TitleBar`), the buttons (`OwnButton`), scroll views and their
scrollbar, the gamepad navigation, fitting to the screen, the way back (`HideUnder`, `RestoreUnder`, Escape), and the
controls taken from the player while a window is open (`TakeControls`). The framework link and the Settings window
rows, the two things the new window replaces, are in the API as well.

- **In the API (recommended):** the window is the API's way of showing any `MountSettingsPage`, as the Mounts panel
  is. Its title is the page's (`page.Title`), so it never names Saddle Up (the layering rule). Saddle Up keeps
  declaring its rows, and its code barely changes. It is a Saddle Up thing in what it shows, the API's in how.
- **In Saddle Up:** Saddle Up would need the drawing machinery too: either a copy of about 3,000 lines of the panel's
  code, or the API making its UI building blocks public API, which must then stay compatible for every mod. Both cost
  more and buy nothing for the player.

## 3. The controls: our own for every kind

The user: no compromise. No row falls back to a copied game row (the panel still uses the game's row for a key, a
button and an info line; the options window will not).

| Row kind | Control | Today |
|---|---|---|
| Toggle | the switch (40x20, knob right and red when on) | exists (`Switch`) |
| Steps, up to 8 values | number buttons | exists (`Numbers`) |
| Steps, more values | slider with its words ("80%"), Left/Right step it | exists (`SliderRow`) |
| Choice, List | dropdown, 7 shown then scrolling | exists (`Dropdown`) |
| Text | text box, written when typing ends | exists (`TextRow`, `InputBox`) |
| **Key** | **new**: a key box showing the shortcut ("H", "Ctrl+G", "none"). Click or Select: "Press a key...", the next key with its Shift/Ctrl/Alt is the key; Backspace: none; Escape or a click elsewhere: cancel. A lone Shift or Ctrl (Climb's and Descend's defaults) is taken when it is released with no other key: the Settings window can't set one today (README: "only in the cfg"). A key that another of the page's rows already has is named on the row. A gamepad has no keys to capture: the row says so | the capture logic is in `SettingsRows` (`StartCapture`, `EndCapture`, `Tick`, `WindowInput`), to port |
| **Button** | **new as a row**: the panel's `OwnButton` on the value edge, with its word ("Open", "Go"), greyed with its reason when it can't run (outside a game: "In a game") | `OwnButton` exists |
| **Info** | **new**: label and its value as text, read again while shown | none |
| **Note** | **new**: a line of wrapped text over the column (the "Setting a key" note, if it stays: the key box explains itself) | none |
| **Heading** | the panel's bold heading with the faint line, inside a section | exists (the colours window's) |
| **Preview** | the mount's picture (`PreviewStage`), only in a game: decision 7 | `PreviewStage` exists |

For every row:
- **Greyed** while its `EnableWhen` is false: shown, not usable, with its description saying why.
- **Hidden** while `Visible` is false. The rows below move up (Speed of every mount appears under All mounts have the
  same speed).
- **Changes apply at once**, as they do in both menus now: the entry is set, its apply runs, BepInEx saves the cfg.
- **Description:** every row has a description (the Settings window never showed them; the Mods menu did). Decision 3.
- **Default:** the Mods menu had a reset; nothing of ours has. Decision 4.

## 4. The layout

- **A (recommended): sections on the left, rows on the right**, the journal's pattern (the user liked its card list).
  The 7 sections stand as cards on the left. The chosen section's rows are on the right, scrolling only if a section
  ever outgrows the window. With Keys at 10 rows plus a note, every section fits a window about 600 high.
- **B: one list** of every section under its heading, scrolling, as the Mods menu page was.

Both use the same controls and the same row (label left, control on the value edge, a long label wrapping).

## 5. Opening it, and the way back

- **The game's Settings window:** one row per page ("GK2 Saddle Up", button "Open") in place of the "Showing" row and
  the mod pages. That button row is our own too (the Settings window is the one place it sits among game rows: it is
  made to match them and works with the gamepad there).
- **The Mounts panel:** a "Settings" button beside Done, Customize, Ride and Drive the cart (to place).
- **In the options:** the Mount section's "Mounts" button opens the panel over the options.
- **A key:** possible (`Key` with `pressed`), unbound by default.
- **The main menu:** the game's Settings window is there too. Keys, behaviour, cart, sound and Freeplay rows work
  without a game. The Mounts button, the safe-spot button and the picture need one: greyed there, "In a game".
- **The way back** goes Settings window → options → Mounts panel → colours window, Escape stepping back one level
  each. The options window is ours, not a game window (`LazyWidgetBase`): the panel's `HideUnder` reads the game's
  window stack, which would hold the Settings window (already set aside by the options), not the options. So the
  options window becomes a level of the panel's own way back, as the colours window is. To be measured in game, not
  designed on paper (§7).

## 6. What goes, what stays

**Goes:**
- `FrameworkLink.cs`, with the API's own Mods-menu entry (`TryRegisterApi`);
- `MenuLabels.cs`, `ModsWindow.cs`, and the Mods-menu part of `LookPreview.cs`;
- the soft dependency (`Plugin.cs` 366, 521);
- the `GK2.Framework` reference (csproj, `Directory.Build.props`, `lib/GK2.Framework`);
- `RefreshMenu`;
- `SettingsRows`' pages: "Showing", `Paginate`, `PlanPages` and their tests (the key capture moves to the window);
- `tests/FrameworkScan`, which becomes a check that nothing references the framework (it runs in
  `tools/release-scans.sh` and the checklist);
- the build-confirmation wording in `Shared/GameDependencies.cs` 186.

**Stays:**
- **The cfg:** the same entries, sections and keys. "1. Keys" keeps its number: numbering was only for the framework's
  sorting, but renaming the sections would move every key of every player. No `MovedKey`; no player file changes.
- **`MountSettingsPage`'s public methods**, all of them, so Saddle Up's `Plugin.cs` and any other mod's page keep
  compiling. `Preview` stays callable whatever decision 7 says.

**Players see:**
- With the framework installed, Saddle Up and GK2 Donkey API are no longer in its Mods menu. The CHANGELOG and the
  Nexus and Workshop texts say where the options are now. Today those texts say "With GK2 Mod Framework installed: in
  the game's Mods menu ... Without: in the game's Settings window, under Showing"; `media/settings.png` shows the Mods
  menu.
- In the Settings window, one button instead of the "Showing" pages.

**Versions:** a new API minor version (the menus change and a reference goes; the public API doesn't) and a new Saddle
Up minor version. The packs have no page and don't change.

**Elsewhere:** the four QOL mods (GK2QOLMods) have their own FrameworkLink: the same question, later, not part of this.

## 7. To measure before building

1. The rows in the options, counted in game (a dev log line over `page.Rows` with `InMenu`), against §1's 30.
2. Screenshots of the Mods-menu page and the Settings window pages as they are, the "before".
3. The main menu: does the panel's window build there (its font, its button sprite, `TakeControls` with no player)?
   Known from the game's code: the main menu opens the same Settings window (`UIMainMenuWindow`,
   `LazyUI.GetWindow<UIGameSettingsWindow>()`), and our rows already show in it there today.
4. The way back with a window of ours between the Settings window and the Mounts panel.
5. Capturing a lone Shift or Ctrl (`UnityInput` sees the key go down and up with nothing else).

## 8. Decisions

Decided by the user, 2026-10-30:

1. **Where the code lives: the API**, showing any page in its own window, titled with the page's title.
2. **Layout A:** sections as cards on the left, the chosen section's rows on the right.
3. **Descriptions:** one line at the bottom of the window, for the row under the mouse or the gamepad's focus.
4. **Defaults:** per section. A "Defaults" button resets the shown section's options after a confirmation.

Taken as proposed (the user did not say otherwise):

5. **The main menu:** the options open there too, with the rows that need a game greyed.
6. **Ways in:** the Settings window button and a Settings button in the Mounts panel. No key.
7. **The picture ("Your mount"):** left out, since the Mounts panel shows the mount. `Preview` stays callable.

## 9. Build steps (proposed)

1. **Measure** (§7): a dev-only log line of the options' rows (gated like the UI probe), and screenshots of the Mods
   menu page and the Settings window pages as they are.
2. **The window**, in the API as a part of the Mounts panel (`MountsPanel.Options.cs`). It shares the panel's
   controls, frame, gamepad navigation and its way back, so the options are one more level of that way back. It
   has the section cards, the rows, the description line and Done.
3. **The new controls:** the key box (its capture, a lone Shift or Ctrl, the same key named twice), the button row,
   info, note, and Defaults per section with its confirmation.
4. **The ways in:** the Settings window's button row in place of "Showing" and its pages, and a Settings button in
   the Mounts panel.
5. **Removal** (§6): the framework link and everything that only served it; FrameworkScan becomes the check that
   nothing references the framework.
6. **Self-tests:**
   - every row kind built with our own control;
   - a value set from the window reaches the cfg;
   - Defaults sets a section back;
   - the way back from the Settings window through the options and the Mounts panel;
   - the main menu with the game-only rows greyed;
   - the key capture driven through its own entry point (a test can't press a key).
7. **Docs, texts, versions:** api.md, the READMEs, the Nexus and Workshop texts, `media/settings.png`, CHANGELOG
   Unreleased, a new API minor version and a new Saddle Up minor version.

## 10. Built (2026-10-30 evening)

The user during the build: no partial classes ("prefer composition over inheritance ... the partial i dont like because
it hides code"; "controls that both could use"), and no refactoring of unrelated classes. So the window is a class of
its own, and what it shares with the Mounts panel moved out of the panel into classes both use:

| Class | Holds |
|---|---|
| `OptionsWindow` | the window: the section cards, the rows, the line at the bottom, Defaults and its question, Done, its way back |
| `SettingsButtons` | one button per page in the game's Settings window, "`<title>` options", above OK: a copy of the window's own OK button, drawn with our words (as the game's Mods window makes its buttons, `UICodeModsWindow.CreateButton`) |
| `RowControls` | every row control, one instance per window (its open dropdown, its text boxes, its sliders' gamepad steps, its gamepad focus): switch, number buttons, slider, dropdown, text box, and new the key box, the button row and the info line |
| `KeyCapture` | setting a key: the next key with Shift, Ctrl or Alt, a modifier let go alone, Backspace none, Esc or a click cancel, 10 s; the key in words ("Ctrl + G", "Left Shift"); the clash rule |
| `UiKit` | the game's look read off the Settings window and the window pieces (frame, title bar, buttons, texts, boxes, scroll views, fit to screen) |
| `GameWindowAside` | the game window under the first of ours (set aside, shown again, the chain closed on Ride), the player's controls, one Escape a frame |

`MountsPanel` uses them: its journal, colours window and way back behave as before; it gained the **Options** button
(left of Customize) and the two hooks for the options over or under it. `MountsPanel.Controls.cs` (a partial) is gone.
Gone with the framework: `FrameworkLink`, `MenuLabels`, `ModsWindow`, `LookPreview`, `SettingsRows`, `lib/GK2.Framework`.

Decided while building:
- **The clash:** two key rows with the same key are marked when either has a `pressed` action (both would act); two
  rows the API reads apart (Jump and Fly share Space by design) are not.
- **The "Setting a key" note row is gone:** the key box says "Press a key..." and the line at the bottom how to set one.
- **A `Preview` row is in no window** (decision 7); the call still names the companion the panel draws.
- **Buttons need a game** (greyed in the main menu, "Only in a game."), as the Mods menu had it.
- **The cfg is untouched:** the same entries and sections; `InMenu` is `InOptions` (the picture row left out).

Not done as §9 planned:
- **The "before" screenshots (§7.2) were not taken:** the deployed build no longer registers with the framework.
  `media/` keeps the old pictures (GK2ModFramework-Mods.png, settings.gif, NoGK2ModFrameworkSettings.png); the page
  texts no longer show them, and new screenshots of the window are an open item.
- **The rows were not counted in game first (§7.1):** the new self-test logs the count ("N options shown in M
  sections") on its first run.

Tests: "settings: the options window" and "settings: the options window's way back" (docs/self-tests.md).
Scans: FrameworkScan (now: no framework reference anywhere, Saddle Up included), NoLinqScan, ApiSurface (unchanged),
CecilScan (`game-code.txt` sorts the new classes), layering: all green on the Release build.

## 11. GK2 Mod Framework kept (2026-10-30, later the same evening)

The user, on the first build: "We should still support the g2k framework but this time when you open the settings
from the g2k, there's only a button which opens our own custom options, unless its possible to directly modify the
settings button [...] to open our own custom options window directly." Their screenshot showed why it matters: an
unregistered plugin is listed by the framework as "Imported settings", every line of its cfg raw (Saddle Up and the
API both were, after the first build).

Read in the framework (0.1.24 installed, 0.1.16 pinned; decompiled 2026-10-30): the details panel's Settings button
calls `ModsMenuWindow.OpenSettingsPage(RegisteredMod mod)`, private, which opens the framework's page and only when
the mod has a setting (`Settings.Items.Count > 0`); `RegisteredMod` and its `Metadata.Id` are public. No public hook.

Built:
- the soft dependency, the API's own entry (`TryRegisterApi`, no settings) and `lib/GK2.Framework` are back;
- `FrameworkLink.PageMod` registers the page with **one setting**: a button "`<title>` options" that opens the
  options window (over the Mods menu, which waits underneath and is shown again on Escape);
- a Harmony prefix on `OpenSettingsPage(RegisteredMod)` opens the window for our pages and skips the framework's
  page; another mod's goes on as before. Found with System.Type's reflection by the exact signature (the build's
  dependency manifest refuses AccessTools lookups it can't follow, and `FrameworkScan` forbids by-name lookups of
  framework members that could pick the wrong overload). Not found, or failing once: the framework's page with the
  one button, which opens the window too;
- `tests/FrameworkScan` is back as it was (the link's isolation); the self-test checks, with the framework installed,
  that the page is registered and the prefix applied (`FrameworkLink.AnyRegistered`, `SettingsButtonPointed`).

Still gone: the framework's page of every row, `MenuLabels` (the choices' labels over its dropdowns), `ModsWindow`
and `LookPreview` (the picture over its window).

## 12. Side effects and other mods (2026-10-30, the user: "Could other mods screw this up? ... hundreds of other mods")

Seen in game: the button stands above OK in the game's red, centred like it (the user's screenshot, 21:15).

### Where another mod can get in the way

| # | Where | What can happen | How likely |
|---|---|---|---|
| 1 | The button is added from `LazyWindowsStackController.OnWindowOpened` | the game raises it as a plain multicast (`OnWindowOpened?.Invoke(window)`, read 2026-10-30): a mod whose handler throws before ours stops ours, and the button is missing for that open | low, silent when it happens |
| 2 | The button is a copy of "the first `UIDialogWindowButton` in the window" | a mod that adds its own dialog button to the Settings window first gets copied instead: its components and look come along on ours | low |
| 3 | The copy brings every component on the OK button | a mod that put a component of its own on OK (a tooltip, a sound, a logger) has it duplicated on our button, acting on our clicks | low |
| 4 | The look (fonts, row measures, focus frame) is read off "the first `UISwitchButton` in the window" | a mod that injects its own rows at the top (as our old "Showing" row was) is read instead: a wrong font or size in our windows | low to medium (it's what settings mods do) |
| 5 | The window under ours is set aside with `SetActive(false)` | Unity stops every coroutine on it; a mod (or the game) running one on the Settings window or the Mods menu loses it; its Update stops meanwhile | low; the panel has done this for weeks without a report |
| 6 | A game window opening while ours is open (a popup, another mod's window on a hotkey) | it opens under our canvas (sorting 30000+), unseen, and takes the game's modal focus; closing ours shows the old window again under it | low, confusing when it happens |
| 7 | Other mods' hotkeys | they read the keyboard themselves: pressing a key to bind it in our key box (or typing a name) also fires another mod's key on it. Only our own mods' keys wait (`KeyActions`) | medium; every BepInEx mod with keys does the same |
| 8 | The key clash line knows only our pages | a key another mod (or the game: E, I, Tab, Space in a fight) already uses isn't said | medium: the commonest real conflict |
| 9 | Escape | a mod that reads Escape itself (closes its window, opens a menu) acts on the same press as ours | low |
| 10 | GK2 Mod Framework's Settings button (our prefix on its private `OpenSettingsPage`) | a framework version that renames it: the prefix isn't applied, the page's one button still opens the window. Another mod patching the same method for its own entries doesn't touch ours (ours acts only on our ids) | low, falls back |
| 11 | The controls taken (`ByUI`) | closing ours gives them back unless a game modal window is open; a mod that took `ByUI` itself for a non-modal window of its own could lose its hold when ours closes | very low |
| 12 | UI scale and resolution mods | a different canvas size: `FitToScreen` follows the canvas we sit on | none seen |

### What would harden it (A to E built 2026-10-30 21:24, the user's pick; F left for later)

- **A. The game's own parts, not "the first found"** (2 and 4): the OK button and a template row from the Settings
  window's own fields (`UIGameSettingsWindow.lazyButton`, `languageButton`; game members, so the dependency manifest
  watches them for game updates), "first found" only as the fallback.
- **B. A clean copy** (3): after copying OK, remove every component that isn't the game's, Unity's or TextMeshPro's.
- **C. A watchdog for the button** (1, and a mod rebuilding the window's content): while the Settings window is the
  window on top, check twice a second that our button is there, and add it if not. The event stays the fast path.
- **D. Clashes with every mod and the game** (8): the key clash reads every loaded plugin's cfg (`Chainloader.PluginInfos`:
  each one's `KeyboardShortcut` and `KeyCode` entries) and the game's own bindings, and says "Same key as <mod>:
  <setting>" or "Same key as the game's <action>". The commonest conflict is a key, and this names it.
- **E. Step aside for a window opened over ours** (6): when another game window becomes the top window while ours is
  open, ours hides and comes back when that one closes (or closes, the game's window then on top).
- **F. A gentler set-aside** (5): hide the window under ours with a CanvasGroup (invisible, no clicks) and switch off its
  own keys (`LazyWindowInputController.Disable`, the game's pair for a covered window) instead of `SetActive(false)`:
  its coroutines and other mods' parts on it keep running. It changes the panel's tested way back, so it would need
  the way-back self-tests and a measure first.
- Nothing to do for 7 and 9: a mod reading the keyboard itself can't be stopped by ours; the key box saying which other
  mod has the key (D) is the help we can give.

Built:
- **A** `UiKit.OkButtonOf` / `SwitchOf`: the window's `lazyButton` and `languageButton` fields (`AccessTools.FieldRefAccess`,
  so the dependency manifest watches them), else the first one found that isn't ours.
- **B** `UiKit.StripForeign`: the copy is made under an inactive holder, and every component whose assembly isn't in the
  game's Managed folder is removed before it wakes; what was removed is logged.
- **C** `SettingsButtons.Tick` (Plugin.LateUpdate, twice a second): while the Settings window is on top, every page's
  button is there (made per page and kept by reference; one switched off is switched on again); a re-add is said once.
- **D** `KeyCapture.Snapshot` (when the options window opens): every other plugin's `KeyboardShortcut` (an exact
  match) and `KeyCode` settings (the main key alone: most mods read it whatever is held), the pages' and the API's own
  cfgs left out; the game's `LazyInput.GameBindings.keyBindings` but the menus' own (values under 100), named in the
  game's language, for a row acting on its press. The line reads "Same key as GK2 Tech Tree Search: ToggleKey" or
  "Same key as the game's Inventory"; the log lists them at each open when they changed.
- **E** `GameWindowAside.Tick` / `Covered`: a game window on top that isn't the one ours set aside makes ours step aside
  (their canvases off, no keys or clicks) until it closes; the window set aside, shown again by something else
  meanwhile, is set aside again. The options window and the Mounts panel both follow it.
- Self-test "settings: the options window among other mods" (OptionsTests.AmongOtherMods).

## 13. Checked (2026-10-30 evening)

- F11, the full set on the final build (the framework kept, the hardening): 1412 passed, 0 failed, 0 warnings (21:50).
- A start without GK2 Mod Framework (it was moved aside and back, the game closed both times; 21:54): GK2 Donkey API and
  Saddle Up load without a warning or an error; no framework line from either; the options open from the Settings
  window's button and from the journal's Options button and come back where they were; no clash at the defaults. The
  four QOL mods load too. The one error in that log is Performance Hog's (a third-party tool with a hard dependency
  on the framework), not ours.
- By hand (the user, 23:13 and 23:44): Ctrl + G on Send your mount away sent the mount away with no cart called, G
  alone drove it; a lone Left Shift set, Backspace to None, Esc; Defaults on Keys and Freeplay; the window from the main
  menu through the Mods menu; the gamepad. The journal's final buttons seen, 634 of 634 at 23:44.
- Not yet: the window at a Steam Deck size, screenshots for the pages.

## 14. Keys that overlap (investigated 2026-10-30, the user: "whats ctrl g? ... investigate the issue more")

The clash line compares whole combinations: G and Ctrl + G are two keys. But not every reader of a key works that way.

### How a press is read (read from the code, 2026-10-30)

| Reader | Who | A press is | Held with it |
|---|---|---|---|
| BepInEx `KeyboardShortcut.IsDown()` | most other BepInEx mods (decompiled BepInEx.dll) | its main key goes down and all its modifiers are held | **any other key held stops it** (`ModifierKeyTest`: every supported key but the mouse buttons, so W held while walking stops it too) |
| The game (`LazyInput`'s `KeyboardController`) | the game's own keys (E, I, Space...) | its key goes down and its extra keys (`additionalKeyCodes`) are held | anything else held is ignored: Ctrl + E is still E |
| Ours (`KeyActions.Pressed`, `ModifiersHeld`; flying's jump and fly keys; the safe spot's key) | Saddle Up's keys | its main key goes down and its modifiers are held | **anything else held is ignored, on purpose**: walking keys (KeyActions' note), the climb's Left Shift held while the fly key is pressed (Flight's note) |
| Ours, held (flying's Climb and Descend) | `UpKey`, `DownKey` | the main key held, nothing else asked | ignored (a modifier can be the key) |

### Where that makes two keys act on one press

1. **Two of ours, one with more modifiers:** Send your mount away on Ctrl + G and Drive the cart on G: Ctrl + G does
   both (our reader ignores the extra Ctrl for G). Not said today: the clash line compares exactly.
2. **One of ours on a lone modifier:** a key acting on its press set to Left Shift fires on every Shift + something of
   any mod or of ours; Climb (held, while flying) climbs whenever any Shift + something is pressed in the air.
3. **Another mod's shortcut with more modifiers than ours:** their Ctrl + G (IsDown) also fires our G; our G alone
   doesn't fire theirs. Not said today.
4. **The game's key as our main key:** Ctrl + E of ours also interacts (the game ignores the Ctrl): said today (the
   game rule compares the main key).
5. **Another mod's plain KeyCode setting:** most read the key alone, whatever is held: said today (the main key).

### What can be done (A and B built 2026-10-30 22:04, the user's pick)

- **The clash line reads overlaps, not only exact matches** (no change to how any key acts): ours against ours, the
  same main key and one's modifiers inside the other's, or one's main key a modifier of the other; ours against a
  mod's shortcut (IsDown), the same main key and ours' modifiers inside theirs, or our main key one of their
  modifiers; ours against a KeyCode setting or a game key, as today. The line: "Same key as X" for the exact match,
  "Clashes with X (Ctrl + G)" for an overlap.
- **The most specific of our keys wins** (a behaviour change, ours only): when two of our keys go down on one press, the
  one whose modifiers are all held and are more (Ctrl + G over G) acts, the other doesn't. Fixes 1 in play without
  changing a key used alone (Shift held while pressing H still rides when no Shift + H is bound). The key poll knows
  only the keys with an action (`pressed:`); flying's and the safe spot's keys would ask it too.
- **Not:** refusing every extra key, as BepInEx does: walking keys held would stop our keys, and the climb's Shift
  would stop the fly key in the air; the game ignores extras too.
- **Can't:** another mod's own reader. The line naming the overlap is the help we can give.

Built:
- **A** `KeyCapture.Clash` returns the whole line. Ours against ours: the same combination, or one's main key a
  modifier of the other's when that one acts on its press (the same main key with more modifiers is no clash: B
  makes the more specific act alone; Climb and Descend, held on Left Shift and Left Ctrl while flying, marked every
  Shift + and Ctrl + key of ours in the first build, F11 22:16, and no longer count). The same against another mod's
  combination: ours on a lone modifier counts only when it acts on its press (the log at the defaults, 23:13:
  "Descend (hold) (Left Ctrl): Clashes with GK2 Tech Tree Search: FocusKey (Ctrl + F)"; fixed 23:15).
  Against a mod's shortcut: the same combination, its exact press doing ours (the same main key, our modifiers among
  its), or a main key that is the other's modifier. Against a mod's KeyCode setting: our main key or one of our
  modifiers. Against the game, for a row acting on its press: our main key, one's held keys among the other's.
- **B** `KeyActions.Shadowed(k)` / `MoreSpecific(o, k)`: the key poll, flying's jump and fly keys and the safe spot's
  key skip a press when another key row has the same main key, k's modifiers and more, all held now.
- Self-tests: the smoke test (the more specific rule; Ctrl + a free key over the key: no line; Right Alt alone and
  Right Alt + the key: both lines) and "among other mods" (another mod's Ctrl + Alt + the key over ours).
