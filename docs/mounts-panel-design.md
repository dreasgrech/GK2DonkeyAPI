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
- **Done and Cancel** bottom right, the panel's own buttons (the copied dialog button drew off its rectangle until
  its hover animation; a game row does too, which is why rows are measured).
- A dev switch, `Debug.MountsPanelFakeEntries`, pads the list with made-up entries to see it scroll.

## What stays from the first panel

Open/toggle (the key toggles, the button brings forward with the menus out of the way), `Entries()` from the
registry (the self-tests read it), the `before` snapshot and Cancel, following the registry while open, the
picture tick, the test hooks (`TestMeasure` reports the window's size and scale).

## Later

Colour swatches with a lightness slider instead of the custom-colour dropdowns (direction A's second half), a
search box once the list passes eight mounts, a Call button that summons the chosen mount, gamepad navigation.
