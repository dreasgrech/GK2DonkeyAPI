# The Mounts panel, second design (the journal)

The first panel (2026-10-20, docs/mounts-foundation-2.md 3) worked but looked wrong (the user, 2026-10-21: "I don't
like it at all"): a small picture in a sea of empty space, 28 dropdown rows at once, six loose buttons for the list,
and the whole thing shrunk to 0.83 at 1080p because it was taller than the screen. The reference the user gave is a
game's mount journal: a card list on the left, the model dominating on the right, a calm detail area. Decided the
same day: direction B (the journal layout with the game's own rows inside tabs; colour swatches as a later step),
portraits generated from each skin's standing picture, a list that scrolls with the mouse wheel.

## What can be built

The game's UI is Unity uGUI. Nothing stops the mod from building its own panels, buttons, cards, tabs, scroll views
and text from code; what it takes from the game is what keeps the look native: the window frame sprite, the dialog
button (copied, as before), the font asset read off one of the game's labels. Pictures of the mod's own embed in
the DLL like the skins do, drawn at integer scales with point filtering so they stay pixel art.

## The layout, third cut (canvas units; the canvas is 1280x720 at every resolution)

The second cut (tabs under a tall stage) squashed the rows into a slit and left the stats a single line (the user,
2026-10-21: "a lot of empty space ... the customisation squashed"). Decided the same day, direction D: **the journal
and a colours window**. A window of 1060x600, centred, scaled down only on a smaller canvas; laid out by hand.

```
+-- Mounts ------------------------------------------------------ 6 mounts --+
| [list 260]        | [icon] Gerry                                           |
|  Donkey           |        description, two or three lines                |
|  Dragon           |--------------------------------------------------------|
|  Gerry  <----     | [ stage 440x236, up to 3x ]   | Stats                  |
|  Giant Rat        |                               |  Speed    2x walking   |
|  Giant Snail      |                               |  Cart     pulls it     |
|  Giant Worm       |                               |  Babies   0.33x its sz |
|   (scrolls)       |                               |  Skins    1            |
|                   |--------------------------------------------------------|
|                   |   Skin                 |   Effect                      |
|                   |    Skin  < Default >   |    Effect       < As the ... >|
|                   |                        |    Effect shown < Always >    |
|                          [ Colours and effect... ]  [ Done ]  [ Cancel ]   |
+---------------------------------------------------------------------------+

+-- Colours and effect: Gerry ------------------------------------------------+
|   Mount colours                     |   Cart colours                        |
|    Coat colour       < ... >        |    Paint            < ... >           |
|    Custom coat       < ... >        |    Wheel colour     < ... >           |
|    Harness colour    < ... >        |    Wheel hub colour < ... >           |
|    Second tone       < ... >        |    Lantern light    < ... >           |
|   Effect                            |                                       |
|    The effect leaves a trail < On > |                                       |
|    ... (the four toggles)           |                                       |
|                                                        [ Done ]  [ Cancel ] |
+-----------------------------------------------------------------------------+
```

- **The list**: a flat row per mount (a dark face, the icon at the left, the name; the chosen one in the game's red,
  hover brightens), in a scroll view with a thin scrollbar. The icon is the mount's portrait: a PNG the pack names,
  else the Default skin's standing picture (MountIcons: a pack's own PNG decoded once and cropped, a code skin's
  sprite, a game-source skin's game sprite by name, the donkey's own game sprite) through a RawImage's `uvRect`.
- **The header**: the same portrait at 64 px, the name large, the description wrapped by the text component.
- **The stage**: 440x236, a dark box with a faint edge; the live picture (PreviewStage) up to 3x inside it. One
  size on every mount: nothing moves.
- **The stats card** beside it: Speed (the mount's own pin, else the Ride setting in effect), Cart, Babies, Skins
  (the user's pick; a mount's `[Mount.Stats]` lines and its abilities are read into `Entry.Extra` for a later line).
- **The look rows** under them: the Mount section's rows (the Skin) and the Effect section's choices (Effect, Effect
  shown) as copied game rows in two groups, placed where they draw (MeasureRow). A row shown only with a preset is
  left out until the preset is chosen; a row greyed by a rule stays greyed.
- **The colours window** ("Colours and effect..."): 800x560 over the journal, which waits hidden; every other look
  section (the colours under their two headings) in two full columns, the Effect section's toggles under "Effect";
  Done keeps, Cancel puts back what these rows held when it opened. The journal's own Cancel still covers everything.
- **Done** bottom right, the panel's own button (the copied dialog button drew off its rectangle until its hover
  animation; a game row does too, which is why rows are measured). No Cancel (the user, 2026-10-21): Done and Escape
  keep what was chosen; Escape steps the colours window back to the journal and closes the journal, and the pause
  menu the same press opened is closed again in the same frame.
- A dev switch, `Debug.MountsPanelFakeEntries`, pads the list with made-up entries to see it scroll.

## Resolutions (measured 2026-10-21)

The game's UI canvas is not a fixed 1280x720: its scaler keeps a factor of 2, so the canvas is the screen in half
pixels. The windows are laid out at their fixed size in those units and scaled down only when the canvas is smaller:

| Screen | Canvas (units) | Window scale |
|---|---|---|
| 2560x1440 | 1280x720 | 1 |
| 1360x768 | 680x384 | 0.56 |
| 1280x800 | 640x400 | 0.56 |
| 1280x960 | 640x480 | 0.56 |
| 1280x1024 | 640x512 | 0.56 |
| 1280x1440 | 640x720 | 0.56 |

The user judged both windows at these sizes: they scale well. The floor is a scale of 0.5 (below it the window would
be cut at the edges, not shrunk further); a canvas under 530x300 units (a screen under about 1060x600) would reach it.

## Escape and the way back

Measured in the game's code (decompiled 2026-10-21): a modal game window takes the player's controls
(`PlayerController.SetControlTakenType(ByUI, false)` in `MainGame.OnWindowOpened`); the pause menu is opened by
`PlayerInputHandler.UpdateInput` on `GameKey.InGameMenu`, which `FreePlayerState` runs only while the controls are
enabled; a key press is consumed with `LazyInput.ClearKeyDown`; the pause menu opens the Settings window with
`onClosed` = reopen the pause menu. The window stack (`LazyWindowsStackController`) has no back step of its own.

So the panel takes `ByUI` while open (re-asserted every tick, since the game gives it back when its last modal window
closes; `ByUI` because the ride ends on any other taken control) and consumes Escape and Back. The way back is a stack:
the game's own windows below, then the journal, then the colours window. The game window on top when the panel opens
(`LazyWindowsStackController.ActiveWindow`: the Mods menu on a mod's page, the Settings window on a page, the pause
menu) is not closed: it stays in the game's stack as it is, modal (the game stays paused), with its GameObject switched
off so its own Escape handling does not run. Closing the panel switches it back on, same page and state, and its own
Escape goes on from there. Closing and reopening would not do: GK2 Mod Framework's `ModsMenuWindow.Close` reopens the
pause menu and forgets its return window, and its `Open` starts on the mod list. The panel ticks in `LateUpdate`, after
every window's `Update`, so a window switched back on reads keys from the next frame, when that Escape is over.
Escape steps back: out of the search box, the colours window to the journal, the journal to the window under it.
Done does the same; Ride and Drive close the whole chain into the game.

## The gamepad (2026-10-21, the game's way)

Read from the game: every window has a `GamepadNavigationController` on its root, enabled only while
`LazyInput.IsGamepadActive`; its `LazyWindowInputController` maps Up/Down/Left/Right (and the d-pad) to `Navigate`,
Select to `SelectFocusedItem`, Back to close, and consumes a key it used (`LazyInput.ClearKeyDown`). The Settings
window leaves Left, Right and Select to its rows: a focused `UISwitchButton` steps itself in its `Update` with the
keys it was initialised with (the Settings window gives `{DecSlider, Left}` and `{IncSlider, Right}`). A game button's
`GamepadNavigationItem` selects through `SetCallbacks` and shows its `focusFrame` while focused; items are grouped,
with hand-offs between groups (`navigationGroupSources`).

The panel does the same: a controller on each window (vertical loop, remember the last item in a group); groups in
the journal: the list (0), the look rows (1), the buttons (2), with Right from the list to the rows, Down to the
buttons, Left and Up back; in the colours window: the rows and swatches (0), Done (1). Our own buttons carry an item
that presses the button and a focus frame copied from the game row's own; a swatch has a small frame. Up/Down move;
Left/Right move unless a value row is focused (it steps itself); Select presses unless a value row is focused; Back
steps back like Escape. Items are re-collected whenever the list or the rows are rebuilt and when a window swaps; the
first focus is the chosen mount's row; a focused item in a scroll view is scrolled into view. The search box is not
a gamepad item (no on-screen keyboard). Our rows in the game's Settings window got the game's key arrays too.

## Swatches

In the colours window a custom-colour row (a `PaletteColor` choice, `MountSettingsPage.Color`) is drawn by the panel
itself: the label where a game row's label sits, then the 24 named colours as 14 px squares in two rows of twelve,
the chosen one framed in white, hover brightens, a click sets the row's value (the page's `Color` combines it with
the lightness and applies the hex). The lightness row stays the game's steps row under it. The journal's rows and
every other row are still the game's own.

## What stays from the first panel

Open/toggle (the key toggles, the button brings forward with the menus out of the way), `Entries()` from the
registry (the self-tests read it), the `before` snapshot and Cancel, following the registry while open, the
picture tick, the test hooks (`TestMeasure` reports the window's size and scale).

## Later (what is left)

Colour swatches with a lightness slider instead of the custom-colour dropdowns (direction A's second half), a
search box once the list passes eight mounts, a Call button that summons the chosen mount, gamepad navigation.
