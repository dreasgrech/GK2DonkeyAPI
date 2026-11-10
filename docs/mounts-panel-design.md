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

**2026-10-25 evening, the alignment** (the user's screenshots: "remove that SKIN header above the skin dropdown ... it's
aligned to the right and all over the place"; the colours window: "the alignment of everything in here is also
weird"). The copied game row (354x25 drawn) right-aligns its label on its "< value >" and has a wide empty label cell:
centred across the area it sat off to the right. Now:

- The journal's rows have no heading, and stand in the stats card's column under the card (`journalW`, the card's
  width). A row wider than its area is put with its value's end on the area's right edge (only the label cell's
  empty left reaches out, clipped). The stage fills the height down to the buttons (`StageH` 392, was 236): no empty
  band under it; the colours window keeps its own (`CStageH` 236).
- `MeasureRow` also measures where the game row's label ends and where its value spans (`rowLabelRight`,
  `rowCtrlLeft`, `rowCtrlRight`, logged). A custom colour's swatch row lays its label on that same end and its 24
  squares across exactly the value's span (the square's size follows from it); they ended 10 px short and ran past
  the switch.
- Headings (Mount colours, Cart colours) are the panel's own text, bold, left on the column's start, with a faint line
  under: a game heading row right-aligned on the labels' edge and floated mid-column.

**2026-10-25 evening, the picture between the two views.** From the dragon to a mount that pulls the cart, the new
mount showed large and cartless for a few frames, then the cart came (the user). The panel fetches its picture every
0.2 s and shows the texture between; the stage re-renders every view asked within 0.5 s. The dragon is shown in the
alone view, so that view went on being drawn with the new mount until the next fetch. Now the stage never re-renders a
view the mount wouldn't be shown in (the cart view for a cartless mount, the alone view asked in the cart view's place
for one that pulls a cart), a pick fetches at once, and while the view it switches to has no finished photo of the new
look (`PreviewStage.ShowsLookNow`) the panel keeps the last photo, asking again each frame: the new mount comes whole,
with its cart, in one frame. Self-test (the panel): every frame after the pick is the last photo or the new look whole.

**2026-10-25 (API 0.39.0):** a mount that can't pull the cart (the dragon) has no **Drive the cart** button and no
**Cart colours** in the colours window (the user: "for mounts that cant pull the cart, the drive the cart button
still appears in teh journal and there's still cart customization"): `BuildRight` hides the button for the chosen
mount (`MountSettingsPage.MountPullsCart`; a mount not installed keeps it, the donkey comes), and the page's cart
rows are `CartRow`s, left out with their heading.

**2026-10-25 (API 0.37.0):** the effect rows are gone. The journal has no Effect group and no "Comes with: ..." line,
the colours window has no Effect toggles, and its button and title are "Colours..." / "Colours: <mount>" (since 0.41.0
"Colours: <mount> - <skin>" when the chosen skin isn't the mount's first: the window's rows are that skin's,
`PerSkin`, and a change of the Skin row rebuilds the right side, `builtForSkin`; docs/per-skin-looks.md). The
drawing below keeps the effect parts as built on 2026-10-21; nothing picks an effect until the skins' use of one is
designed (docs/open-items.md "Effects on skins").

```
+-- Mounts ------------------------------------------------------ 6 mounts --+
| [list 260]        | [icon] Larry                                           |
|  Donkey           |        description, two or three lines                |
|  Dragon           |--------------------------------------------------------|
|  Larry  <----     | [ stage 440x236, up to 3x ]   | Stats                  |
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

+-- Colours and effect: Larry ------------------------------------------------+
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
  (the user's pick; a mount's `[Stats]` lines (its mount.cfg; `[Mount.Stats]` in a skin.cfg before 2026-10-24)
  and its abilities are read into `Entry.Extra` for a later line).
- **The look rows** under them: the Mount section's rows (the Skin) and, until 2026-10-25, the Effect section's
  choices (Effect, Effect shown) as copied game rows in two groups, placed where they draw (MeasureRow). A row shown only with a preset is
  left out until the preset is chosen; a row greyed by a rule stays greyed.
- **The colours window** ("Colours...", "Colours and effect..." until 2026-10-25): 800x560 over the journal, which
  waits hidden; every other look section (the colours under their two headings) in two full columns (until
  2026-10-25 also the Effect section's toggles under "Effect");
  Done keeps, Cancel puts back what these rows held when it opened. The journal's own Cancel still covers everything.
- **Done** bottom right, the panel's own button (the copied dialog button drew off its rectangle until its hover
  animation; a game row does too, which is why rows are measured). No Cancel (the user, 2026-10-21): Done and Escape
  keep what was chosen; Escape steps the colours window back to the journal and closes the journal, and the pause
  menu the same press opened is closed again in the same frame.
- A dev switch, `Debug.MountsPanelFakeEntries`, pads the list with made-up entries to see it scroll.

## The panel's own controls (2026-10-26)

The user, 2026-10-26, on the journal with the babies, name and voice rows moved in: the labels were cut on the left
("abies come indoors": the copied game row draws 354 wide in the 288 column, right-aligned, its label cell masked), the
Name was read-only ("set in the cfg"), the "< >" arrows "way too limiting ... not scalable", and the rows came in a
different order on each mount. Decided with the user ("mixed by kind"; the colours window "same controls"):

| Row kind | Control |
|---|---|
| Toggle | a switch (40x20), its knob right and red when on |
| Steps, up to 8 values (the babies, 0-5) | a row of number buttons, the chosen one red |
| Steps, more values (a volume 0-150 in 10s) | a slider with the row's own words beside it ("80%"); Left/Right step it on the gamepad |
| Choice, List (the Skin, every colour preset) | a dropdown: a click opens its list under it (over it near the window's bottom), 7 shown, then scrolling; a click outside, Escape or Back closes it |
| Text (the name) | a text box (24 characters), written when the typing ends; the mods' keys wait while typing |

Other kinds (a key, a button, an info line) got the game's row until 2026-10-30; since then every kind has its own
control (the options window needs them; the user: "no compromise on controls"). Code: `RowControls.cs`
(`OwnRow`; `MountsPanel.Controls.cs` was a partial of the panel, gone: the user wants no partial classes).

- **One row**: its label left on the column's label edge, wrapping onto a second line when long (the row grows to
  fit), its control ending on the column's value edge. Journal: labels at x 12 and controls ending at 276, the stats
  card's own edges; the wide controls 140 wide. Colours window: columns of 369 (two side by side in 754), labels
  0 to 150 (as the headings, on the column's start), the controls and the swatches from 160 to the column's end.
  First built on the game row's measured value span (`rowCtrlLeft`): it measured 12 of 354 in game (the row's switch
  covers the whole row now), the labels got 10 px and wrapped a letter a line under the dropdowns (the user's
  screenshot, 2026-10-26); the panel's own numbers since. Nothing is drawn outside a column.
- **The look** (the user, 2026-10-26: the switches and slider "need to match aesthetic more"): a switch is a dark box
  with the faint edge (the stage's, the stats card's), its knob the game's dialog button face (red, right) when on,
  greyed on the left when off; the slider a dark track with the edge, filled in the journal's red, its handle the
  button face. The dropdown's arrow is two bars, a "v" (the first build drew a "^").
- **The text box** is built switched off and switched on only once its text parts are set (as GK2 Tech Search's):
  `TMP_InputField` sets itself up for typing when it is enabled, and switched on before it had a text part it never
  took a key (the user: "the name field is currently read only"). The search box is the same `InputBox`.
- **The journal's order** is the same on every mount: a text row (the name) first, the Skin second ("to show that the
  name is per mount not per skin"), then the others in the order the page made the donkey's (`JournalOrder`; a mount
  registered later had its rows, its Skin among them, made later).
- **The gamepad**: every control is a stop of the rows' group; a dropdown's list is group 3 on its own while open (no
  hand-over out of it), focus goes to the chosen choice and back to the dropdown when it closes.
- The buttons at the bottom stand on the list's and the stage's edges (the user's screenshots, 2026-10-30, three cuts
  that evening): Options centred under the mount list, the buttons' own width; Customize centred under the stage (it colours what the
  stage shows); Drive the cart and Ride on the right, equal, Drive from the stage's right edge, Ride ending on the
  window's margin. No Done: the title bar's X and Escape close the journal (`PanelSmoke` checks every edge and that
  there is no Done).
- Self-test (`PanelSmoke`, `JournalChecks`): the name first and the Skin second, no game row left, every label 100 or
  wider (journal and colours window), the name's box focused when activated, every part inside the column, the babies rows only for a mount with babies, the same order on each mount picked. The screenshots
  (`PanelShot`) go to `BepInEx\GK2-panel-shots\`.

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

The options window (2026-10-30, docs/options-window.md) is a level of the same way back: opened from the journal's
**Options** button it hides the journal, which its Done, Escape or Mounts button shows again; its own Mounts button
opens the journal over it, and the journal's Done, Escape or Options button goes back to it; Ride and Drive from a
journal over the options close the options and the game windows under them. The game window under the first of
ours, the controls and one Escape a frame are `GameWindowAside`'s (moved out of the panel), the rows' controls
`RowControls`', the window pieces `UiKit`'s: the panel and the options window use the same.

Any game window (2026-10-30): the window under the panel is held as the stack's own `LazyWidgetBase`. It was held as
`LazyWindow<LazyWidgetDataBase>`, which C# doesn't match for a window with its own data type (classes have no generic
variance): 44 of the game's 62 windows, among them the Character window (Tech Tree, map, quests), the inventory and
chests, stayed live under the panel, and Ride left them open. The game has no `Close` on the base type, so Ride and
Drive call each window's `LazyWindow<T>.Close` found once per window type (`MountsPanel.CloseWindow`), and stop with
a warning if a window stays on top. F11's way-back test covers the Character window on the Tech Tree.

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

**No dropdown of named colours (2026-11-10).** (Its first cut, below, was replaced the same day by direction E, further down.) The user: "get rid of the predefined dropdown list of colours and have
'custom' just be the one provided". A colour choice whose next row is its custom colour and which has a value named
`Custom` (`IsColourPart`) is drawn as one part (`ColourPart`): its label, then thirteen squares in two rows:

- **Game default** first, a dark square crossed: the part as the look draws it (the choice's first value);
- **the 24 named colours**: a click sets the custom colour, then the choice to `Custom` (the lightness row shows under
  the part, the page's own row, as before);
- **the player's named colour from before**, if the choice was one when the window opened (Brown, Dark wood...): a
  slot of its own after them, filled with its colour (`MountCompanion.PresetSwatchHex`), or crossed in grey for the
  ones that darken or fade the drawn art (Dark, Black, Dark wood: no one colour), and its name after the label while
  it is chosen ("Scales · Dark"). It stays until the window closes, so a player can go back to it.

**The look: direction E** (the user, the same day: the inline swatch boxes looked "like a quick poc instead of
production", the crossed Game default square and the slider that came and went were disliked; the board "Colours
window directions" offered five mock-ups, the user picked E and kept "Original"). References: the game's own
Customization window (the dresser's: square ◀ ▶ buttons around a value field tinted with the colour, an arrow at the
end greyed) and its zombie screen (item slots, the chosen one in yellow corner brackets). A tab with colour parts
(`HasColourParts`) is built by `BuildPartsTab`:

- **Left column, the parts:** one row each (`PartRow`): the row a button that chooses the part (the chosen one in the
  game's red), its label, and on the controls' span the arrows around a field in its colour. The field names
  **Original** (`UiWords.ColourOriginal`, 11 languages) or a kept named colour (the page's own translated choice word);
  a palette colour shows as its colour (the 24 have no translated names). The arrows step Original, the kept named
  colour, then the 24; each greys at its end. The tab's other rows (the lantern switches) under the parts.
- **Right column, the chosen part's palette** (`BuildPartPanel`): Original and the kept named colour as wide slots
  with their names, the 24 colours as 34 px slots eight a row, the chosen slot's frame yellow and the game's corner
  brackets around it, then the part's lightness row: always there, greyed while the part is Original or a kept named
  colour (the user: "i prefer disabling the control instead of hiding it because it moves stuff up and down").
- **Original's colour** is the part's art as drawn (`Colors.ArtSampleHex`: of the region's colours as the lightness
  rule reads them, the one nearest their average; a skin's own part's from `SkinTones.ColoursOf`). Rows say what they
  colour (`Row.ArtRegion`, `Row.ArtTone`, set by `RegionRows`, the region parts and `ToneRows`); the page's colour
  kinds without a mark by their kind (paint: CartBody, wheels, hubs; the lantern's light: its warm glow).
- **Nothing moves on a pick:** a part's own rows (its custom colour, its lightness) don't count in the tab's signature,
  so a pick refreshes in place; choosing another part builds only the right column again.
- **As the board drew it** (the user, 2026-11-10: "the html version looks a lot more polished in every way so lets make
  it like that"): the parts' list 330 wide and the palette the rest; a part row 32 tall (more for a label on two
  lines), the stepper compact on its right (the arrows with an edge, a 108 wide field framed dark with a faint sheen);
  every field named: Original, a kept named colour, or the palette colour (`UiWords.PaletteNames`, 24 words, 11
  languages); the palette starting with Original and the kept colour (the part's name over it until the user, the same day: the chosen row says it); the 24 in three even rows of
  eight, 38 px slots 8 apart; the lightness row says just "Lightness" (`UiWords.ColourLightness`).
- **A named colour from before shows only while it is the part's colour** (`KeptNow`; the user, 2026-11-10: an old
  "Rose" beside Original read as one colour too many): picked away from, its slot and its step are gone; Cancel or
  Escape still bring it back. The fields' text is light on every colour: the game's font draws its own dark outline
  (dark letters ran into it).
- **Room:** the window 660 tall (640 until 2026-11-10), part rows 30, slots 6 apart: six parts, or the cart's four and
  the two lantern switches, fit without a scrollbar (a self-test check). A named colour that darkens the art (Dark,
  Black, Dark wood) shows as the part's own colour darkened and faded by its preset (`MountCompanion.PresetShade`).

**Nothing saved changes** (the user: "can we keep 100% backwards compatibility regarding colours?"). The rows, their
keys and values are the page's as before; drawing or refreshing a part writes nothing; the colouring code is the same.
A named colour from before is applied as it was and stays until the player picks another square. Picking one new is no
longer offered (decided with the user: option A, no new setting); the cfg still takes every name. Another mod's choice
made otherwise stays a dropdown. Self-tests (the Mounts panel scenario): every saved value of the page the same after
walking every tab and closing; a square sets Custom with its colour, Game default the first value; a named colour kept,
named after the label, and taken again from its slot.

## What stays from the first panel

Open/toggle (the key toggles, the button brings forward with the menus out of the way), `Entries()` from the
registry (the self-tests read it), the `before` snapshot and Cancel, following the registry while open, the
picture tick, the test hooks (`TestMeasure` reports the window's size and scale).

## Later (what is left)

~~Colour swatches with a lightness slider instead of the custom-colour dropdowns~~ (done 2026-11-10: Swatches), a
search box once the list passes eight mounts, a Call button that summons the chosen mount, gamepad navigation.

## The colours window's tabs (2026-11-08)

The user: skins can have quite a few colour rows, so the rows of a skin that isn't the default should be apart from
the mount's. Under the picture, where the "Mount colours" and "Cart colours" headings were (now "Mount" and "Cart"), a strip of tabs across
the window, each tab's rows filling both columns below it (`MountsPanel.MakeColourTabs`, `TabColumns`).

- **The tabs come from the page, not from names.** Each heading's rows are a tab named by the heading (Saddle Up's
  "Mount" and "Cart", translated by the page: the options window's Mount and Cart tabs' words, renamed from "Mount
  colours" and "Cart colours" when they became tabs). The rows of the worn skin's own parts (`ToneRows`,
  marked `Row.SkinPart`) are a tab of their own right after the rows they came with, named by the skin
  (`Looks.NameTerm`). So every mod's page gets the same, and the API names nothing of a mod or a pack. A skin
  without parts of its own: two tabs. A page with one group: no strip.
- **The two columns.** The tab's rows keep their order, down the left column then the right, split where the two
  come out nearest in height. A row and the rows shown under it with a preset (its custom colour, its lightness)
  stay in one column: a unit starts at every row but a swatch row and the slider after it (the first cut went by
  `Visible`, which every per-skin row has, and drew the whole tab in one column). Opening a custom colour builds the
  tab again (its signature) and never moves the split (2026-11-10, the user: a row went from the left column to the
  right when another was set to Custom): the heights are each unit's first row as the mount and skin show it, the
  custom colour and lightness rows a choice opens weigh nothing, so the column that opens them grows instead.
  (A page whose first row of a unit has a `visible:` of its own that follows a value could still move it; no page
  does: only the custom colour rows follow a value.)
- **The picture.** The mount alone, larger, on a tab without the cart's rows; with its cart on the cart's tab (the
  user, 2026-11-10: "only show cart in preview when the cart tab is selected"). The cart's tab is the one with a row
  the page marked with `CartRow` (`Row.CartPart`), so no tab is known by its name; a page that marks no row keeps the
  cart on every tab. The photo shown stays until the other view has the look, so a tab switch doesn't flash. The
  mount alone is drawn up to 4 per art pixel where the stage has room (the cart picture keeps 3): at 3 the donkey alone
  was no bigger than with its cart (224x147 in the 440x236 stage). The size is a whole number of
  screen pixels per art pixel, not of the panel's units (one unit is about 2 screen pixels at 2560x1440, 1.5 at
  1920x1080): rounded to whole units, the dragon (119x78 art, room for 2.82) was drawn at 2, half the donkey's 4. The
  stage is 256 tall (236 until 2026-11-10), so the dragon fits at 3, as big as in the journal.
- **Looks.** The tabs are the options window's section cards laid side by side: flat, the chosen one in the game's
  red with its name bold, each as wide as its name (120 to 260), a rule under the strip.
- **The gamepad.** The tabs are a navigation group over the rows: up from the rows reaches them, down goes back.
- **Each opening starts on the first tab.** The parts' tab stays chosen when the skin changes under it.
- **Checks.** Self-test "settings: the Mounts panel ..." chooses every tab in turn: its rows built whole and only
  those, every label with room. The colours-window measurement takes a picture of each tab
  (`colours-skin-<mount>-<skin>-tab<n>.png`).
