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

## The layout (canvas units; the canvas is 1280x720 at every resolution)

A window of 1200x660, centred, scaled down only when the canvas is smaller than that (never at 1080p and up). Laid
out by hand with anchored rectangles, no nested auto-sizing: the first panel's five fitter levels were the reason its
size could only be known after a frame.

```
+-- Mounts --------------------------------------------------- 6 mounts --+
| [list 300 wide]        | [portrait] Name                                 |
|  [icon] Donkey         |            description, two or three lines     |
|  [icon] Dragon  <----- |-------------------------------------------------|
|  [icon] Gerry          |                                                 |
|  [icon] Giant Rat      |          the mount and its cart, up to 3x       |
|  [icon] Giant Snail    |          on a dark stage (180 to 300 tall)      |
|  [icon] Giant Worm     |                                                 |
|   (scrolls)            |-------------------------------------------------|
|                        | Pulls the cart . Has babies . Speed 1.75x . ... |
|                        | [ Skin ] [ Colours ] [ Effect ]                 |
|                        |  the chosen tab's rows, one or two columns,     |
|                        |  scrolling when they need more room             |
|                                                       [ Done ] [ Cancel ]|
+-------------------------------------------------------------------------+
```

- **The list**: one card per mount (a copied dialog button with a 40 px icon on its left and the name), the chosen
  one bright, the others dimmed, in a scroll view. The icon is the mount's portrait: a PNG the pack names, else
  the Default skin's standing picture (`bdy_static_left`: a pack's own PNG decoded once, a code skin's sprite, a
  game-source skin's game sprite by name, the donkey's own game sprite), drawn through a RawImage's `uvRect` so no
  texture is copied or read back.
- **The header**: the same portrait at 72 px, the name large, the description wrapped by the text component (the
  first panel wrapped by hand at 58 characters because it reused label rows).
- **The stage**: a dark backdrop with a thin outline; the live picture (PreviewStage) up to 3x inside it. The stage
  takes the room the tab's rows don't need: 300 tall over the Skin tab, down to 180 over a full Colours tab.
- **The stats line**: what the mount does, in one wrapped line with separators; a mount's own `[Mount.Stats]` and
  its abilities on a second line.
- **The tabs**: Skin, Colours, Effect; a third-party page's other look sections get a tab each, named by the
  section. A tab holds the page's rows for the chosen mount as copied game rows (SettingsRows), the Colours tab in
  two columns under its two headings. A row shown only with a preset (a custom colour) is left out until the preset
  is chosen, not greyed; a row greyed by a rule (a region the mount's skin lacks, a key the mount pins) stays greyed.
  The tab is remembered while the game runs.
- **Done and Cancel** bottom right; Cancel puts back every look value as it was when the panel opened, as before.

## What stays from the first panel

Open/toggle (the key toggles, the button brings forward with the menus out of the way), `Entries()` from the
registry (the self-tests read it), the `before` snapshot and Cancel, following the registry while open, the
picture tick, the test hooks (`TestMeasure` reports the window's size and scale).

## Later

Colour swatches with a lightness slider instead of the custom-colour dropdowns (direction A's second half), a
search box once the list passes eight mounts, a Call button that summons the chosen mount, gamepad navigation.
