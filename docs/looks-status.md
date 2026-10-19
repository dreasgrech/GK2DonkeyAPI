# Looks: skins, mounts, effects — where it stands (2026-10-17, 21:45)

Everything below is built, scans green (CecilScan, ApiSurface 481 lines, FrameworkScan, DefaultsScan) and deployed
to the game folder as dev builds **GK2 Donkey API 0.30.0 / GK2 Saddle Up 0.10.0** (21:39). Nothing is committed and
nothing is in `dist/` yet. In-game verification so far: skin steps 1 and 2 by hand (2026-10-17 afternoon), and one
self-test run at 19:36 (31/31: skins, pack, leaks, recipe, Hellfire). Everything since 19:36 is untested in game.

Design docs: [skins-design.md](skins-design.md) (drawing), [mounts-design.md](mounts-design.md) (kinds, abilities),
[mount-looks-design.md](mount-looks-design.md) (the player proposals and decisions, Hellfire's build note),
[external-assets.md](external-assets.md) (what the game allows). Author guides: [skins.md](skins.md) (packs),
[api.md](api.md) sections "Mount kinds", "Skins", "Effects". Settings: [reference.md](reference.md) `[Look]`, `[Effects]`.

## 1. What was built

### 1.1 Skins (how a mount is drawn)
- **Overlay rig** (`Looks.cs`): our own sprite renderers under the donkey's `bdy` and `hed`, the donkey's own kept from
  rendering (`forceRenderingOff`, re-asserted every frame), applied in `Camera.onPreCull` after the game's skin
  changer; attach/detach at the view's lifecycle points; nothing left on the pooled parts (the prefab audit knows the
  overlay names). The rider's seat, bob and legs keep reading the donkey's real sprites, so they work unchanged.
- **Sources:** a pack on disk (`BepInEx\plugins\gk2skins\<id>\skin.cfg` + a PNG per frame, or one sheet PNG via
  `sheet =`), embedded in a DLL (the built-ins), a mod's own sprites (`IMountSkinSource`), or a **recipe** (a pixel
  rule over the donkey's own frames read back from the atlas at runtime; nothing of the game's art shipped).
- **One sheet texture per skin**, filled 6 frames a tick from any source, worn once whole (atomic switch, no
  half-built look); refcounted, freed when no donkey wears it; the per-frame apply allocates nothing.
- **Front layer:** pictures drawn in front of the rider (`front\` folder, `sheet_front`, `FrontSource`), sorted on the
  keeper's layer above his body like the leg overlay. The dragons' wing arm and upper membrane use it.
- **Per-skin values:** `[Tuning]` (existing rider keys, as overrides owned by the skin system while worn, never
  saved) and `offset` / `offset_cart` (the pictures moved in art px, riding vs hitched).
- **Base record** (`SkinBase`): the donkey's keys, parts, canvas, pivot, ppu, sprite prefix, atlas, back line, in one
  place; a second creature view would be a second record.
- **Built-in skins:** own art by `tools/creature_sprites.py`: Giant Rat, Giant Snail (with saddle), Giant Worm,
  **Red Dragon and Blue Dragon** (128 canvas, second-pass anatomy, wing split across the two layers); recipes Zebra,
  Ghost, Ember, Golden, Shadow. Dev previews: `BepInEx\GK2-asset-probe\skins-preview.html` (every clip, rider overlay,
  front layer, one pixel scale).
- **Saved:** the instance key `Skin` (also `MountCompanion.Skin`, `FoalsWearSkin`).

### 1.2 Mount kinds (what you ride)
- `Mounts.Kinds`: a kind = identity, the rider's seat for it (`Tuning`, `TuningRide`, `TuningCart`: values for the
  existing keys), `Cart` (may pull the cart), `Foals`, a default `Effect`, **abilities**. A skin names its kind
  (`mount =`); an instance's kind is its skin's (`MountHandle.Kind`); nothing new is saved.
- **Declared by packs** with `[Mount]` / `[Mount.Tuning*]` in skin.cfg (first declaration wins, later skins join);
  the built-in creatures declare theirs this way. **Registered in code** with `MountKindDef` (+ abilities).
- **Abilities** (`MountAbility`): `Attached` / `Detached` / `Mounted(style)` / `Dismounted` / `Tick(dt)`; one object per
  kind; a throw switches it off on that instance with one warning; only for instances with a handle.
- **Rules:** `cart = false` refuses mounting the cart style, hitching, and the style switch, with the kind's name.
- **Values:** per instance and key: another mod's override › the skin's `[Tuning]` › the kind's for the style › the
  kind's for both › the player's cfg; merged once, re-applied only on a skin, kind or style change.
- `MountSettingsPage.MountRows(section, companion)`: every look row in one call (Saddle Up uses it; cfg keys unchanged).
- Example: `examples/GK2DonkeySkins` `checker-beast` (no cart, seat +2, burns with Hellfire while ridden).

### 1.3 Particle effects (Hellfire first)
- `Mounts.Effects`: an effect = parts of **sources** (`IMountEffectSource`: the game's own particle systems copied by
  material with the material held through Addressables, or a mod's prefab) with **emitters** placed on a ring, at the
  body, or as a trail (count, radius, angle, size, height, offset, every, burst), `Foals`, an own `Show` rule.
- **Hellfire** is defined through the public API: six lamp flames and three sparks on a ring at 1.5x, a flame trail
  (pooled pieces, nothing made or destroyed while walking). Ring round the donkey, or wider round donkey and cart.
- Per instance (saved): `Effect` (an id, `none`, or `mount` = the kind's own) and `EffectWhen` (Never / Night /
  Always). Everyone's: trail, with cart, in fights, on foals, the night threshold (the `_SunLight` shader global; no
  game member). Never indoors. Logs "Effects: <donkey>: 'hellfire' lit (...)" once per donkey.

### 1.4 Dev tooling and tests
- Self-tests, group "Skins and effects" (8): skin on/off/removed; pack read/validated/worn/withdrawn (with a
  `[Tuning]` key and a front picture); no leaks over spawn/remove; recipe built whole; Hellfire lit/follows/out/gone;
  mount kind from a pack; mount kind from code (ability hooks); plus the old ones untouched.
- `[perf]` parts `Looks`, `LooksBuild`, `Effects`, `Abilities` (time and bytes).
- `tools/GK2AssetProbe` (Shift+F7) stays dev-only; `tools/creature_sprites.py`, `tools/skin_preview_html.py`.

## 2. In-game test, step by step (2026-10-17 23:20 build: API 0.30.0 / Saddle Up 0.10.0, example mod deployed)

Expect about 40 minutes. Keep notes as "step, what you saw"; anything odd in the log comes out on "check log".

**A. Start and menus (5 min)**
1. Start the game. Main menu → Mods → GK2 Saddle Up. Sections (0.11): Keys ("Ride your mount", "Drive the cart",
   "Ring the bell", "Send your mount away"), **Mount** (its picture with the cart, Name, Mount dropdown, Skin dropdown,
   Babies, Babies come indoors, Babies are the same mount and skin, Keep your mount with the save), **Colours**
   (Choose colours, the picture, "Mount colours": coat, harness; "Cart colours": paint, wheels, wheel hubs, lantern light,
   each with its custom colour and lightness), **Effect** (Effect, Effect shown while it isn't None, the four
   toggles), Behaviour (two speeds, Say why mounting is refused, Shuffles while standing), Cart (Cart capacity, the
   zombies, chest and two corpse toggles, Calling your mount leaves the cart parked, Lantern, Lantern lit by day),
   Sound, in that order. Rows that moved keep their values (the log says "[4. Behaviour] CartSlots is [6. Cart]
   CartSlots now" and the like, once per moved key; one "Settings: ... carried over" line per section whose number
   changed), the sections are numbered 1 to 7 in that order in the cfg, which holds each section once after this
   start and no Donkey section.
2. Mount lists: Donkey, Checker Beast (example), Dragon, Giant Rat, Giant Snail, Giant Worm. The Skin row shows the
   chosen mount's skins only: Donkey → The game's own, Ember, Ghost, Golden, Sepia (example), Shadow, Zebra; Dragon →
   Blue Dragon, Red Dragon; Giant Worm → Giant Worm, Worm (from a pack); the rat, snail and beast one each.
3. Pick Mount = Dragon, Skin = Red Dragon. Pick Mount = Donkey: the Skin row changes to the donkey's skins, at the one
   it had before (each mount remembers its last skin). Back to Dragon: Red Dragon again.
4. Effect list: "As the mount, None, Hellfire". Leave Red Dragon + As the mount for now.
5. Load a game. Esc → Settings → the Saddle Up section: the same rows and values. The colours bar ("Choose colours"):
   Skin and Effect are there as well (the Mount line may not be: it is an info row).

**B. Skins, each mount (10 min)**
6. Red Dragon, call it (H): the wing's arm crosses in front of your chest, the membrane hangs behind and above you,
   the far wing shows higher behind; no donkey pixels anywhere (look at the legs and under the belly). Walk: the wing
   rocks, the tail sways, the legs step. Stand still 10 s: grazing (head to the ground), ear/lips/turn idles. Turn
   round: the right-facing side is the mirror.
7. Blue Dragon: the same drawing in blue.
8. Giant Rat: walk/graze; hitch the cart (G): note the gap between the rat and the shafts in pixels (for offset_cart).
   Unhitch (H).
9. Giant Snail, Giant Worm: walk and graze once each.
10. Donkey: Zebra: on the first pick, no flash of the plain donkey while it builds (a few frames later the stripes
    are there, moving with the body). Ghost once (translucent look).
11. Giant Worm: Worm (from a pack): the worm again (it joined the worm mount; the log says so).
11b. Mount = Gerry: a skull three times Gerry's size hops along under you, jaw chattering; standing still it does a
    somersault now and then (the turn idle) and chatters (lips). The log says "skin 'gerry': 73 '0901_*' frames
    found (...)": "sprites in memory" if Gerry has been on screen this session, else "N sprite atlas(es) asked by
    name" (the thing to confirm: that the atlas path finds them at all). Judge the 3x pixels against the world; the
    seat (Mounts tab, SeatAdjustPx 10) and the hitch gap are first guesses. F11 → Instances: spawn "Gerry with its
    cart" and "a family: a gerry and 2 babies".
12. Foals: Babies = 2, "Babies are the same mount and skin" on → two small dragons following; off → two plain foals.

**C. Hellfire (5 min)**
13. Effect = Hellfire, Effect shown = Always, on the dragon: a ring of six flames with sparks; walk: a trail of flames
    that burn out behind. Hitch: ring round the dragon only; "round the cart too" on → one wider ring round both.
14. Effect shown = At night: at noon out, at night lit. Go indoors (church): out; outside: lit again.
15. "Shows in fights" off: in armour, fight: it goes out; after: back.
16. Effect = As the mount on the dragon: no fire (the dragon comes with none).

**D. The example mount with an ability (3 min)**
17. Skin = Checker Beast (example) (the checker oval). G: refused with "A checker beast can't pull the cart." H: it
    catches fire the moment you are on, out when you dismount.

**E. The tuning panel, Mounts tab (10 min)** — ride the Red Dragon, F11 → Mounts.
18. Header: "Tuning: Dragon (skin: Red Dragon)"; the line says you ride it; the "Mount and skin" cycler changes the
    creature shown; "Follow my ride" comes back.
19. "The mount: Dragon (shared by Blue Dragon, Red Dragon)": Pulls the cart On, Babies can be it On, Comes with
    the effect none.
20. "Rider on this mount: riding": drag "Torso down/up" to +6: the rider rises at once, the row goes yellow.
    Right-click the slider: back to your value, plain. Drag "Whole rider along" to -4: the rider shifts.
21. Riding tab: the rows you set show the dragon's value in ORANGE and locked; hover: "Locked: the mount you ride sets
    this (its skin.cfg, the Mounts tab)". Other rows are editable as always.
22. Mounts tab → "Go to cart": the header says "with the cart"; the rows are the cart style's (not set: your values);
    "Copy these to the cart style" from riding copies them; "Clear this style" empties them. Go to riding.
    With the cart, the dragon's rows include "Cart further behind (+) / closer (-)" = 24 (its skin.cfg): hitch the
    cart (G): the whole cart, shafts included, sits behind the dragon's body instead of around it. Drag the row: the
    cart moves live. H leaves the cart exactly where it was drawn; G takes it again without the cart jumping.
23. "This skin: Red Dragon": drag "Pictures down/up (riding)" to -3: the dragon's pictures move, the rider doesn't;
    right-click: 0. "With the cart: the same offsets" Off → two more rows for the cart.
24. Keeping these values: the text names the repo file `src/GK2DonkeyAPI/assets/skins/dragon/skin.cfg` "and the
    mount's rows to the file(s) that declare the mount: dragon, dragon-blue" (the sources are on this machine), and
    says the values are saved by themselves a moment after each change, with the last save's time. Drag a row, wait a
    second: "Last saved hh:mm:ss to dragon, dragon-blue"; open the file in an editor: [Mount.Tuning.Ride] with your
    value, the [Skin] lines untouched, no blank lines piling up. "Undo the last change (1)": the file is as before and
    the rider shows the old value. Change a number in the file, "Reload from the file": the pictures reload for a
    moment, then the file's value is on the rider.
25. Pick Blue Dragon in Saddle Up and ride it: the mount's values (seat) are the same; the skin's offsets are not
    (they were Red's).

**F. The layers (5 min)**
26. Riding the dragon with a mount value set: Instances tab → Edit its settings on your donkey → Riding tab: that
    row is ORANGE (the mount's value); drag it: MAGENTA (this donkey's own, it wins); right-click it: orange again
    (the mount's value is back at once). Stop editing: the row is orange and locked as in step 21.
27. Save the game, quit to the menu, load: your donkey is a Red Dragon with the same fire, the mount's seat values
    apply (not from the save: from the file), the one magenta value you gave the donkey is back too.

**G. The dev checks (5 min)**
28. F11 → Tests → run the group "Skins and effects" (8 scenarios): all PASS, 0 warnings. Note any FAIL line.
29. Performance tab while riding the burning dragon with foals: Looks / Effects / Abilities with no "+N B" (a one-off
    +N B right after a change is fine).
30. Quit the game and say "check log": I read warnings (the target is zero), "lit"/"declared"/"joins" lines, sheet
    sizes and the perf numbers.

## 3. Next steps, in order

1. **Fix what the test turns up**, then tune the creatures' values with the mechanism that's now there: the rat's
   `offset_cart`, each creature's `[Mount.Tuning]` seat if the rider sits off (two lines in its skin.cfg, no code).
2. **Dragon look:** anything still off in game (wing vs rider, walk, graze); a third pass is cheap in
   `tools/creature_sprites.py`.
3. **Release 0.10.0:** CHANGELOG Unreleased → version and date; `tools/build-dist.sh`; `tools/release-scans.sh`;
   `tools/make-release-zip.py`; the first-run rehearsal with the probe and the sample pack moved aside; then your
   commit, tags and upload (the 0.9.1 steps are still yours too).
4. **Phase 2 (after the release):** sounds per kind (a skin's/kind's sounds mapped onto the donkey's sound events);
   real abilities (flight/glide, fire breath, a gallop) as separate features on the hooks; a picker with pictures per
   kind; more over-layers UI (a saddle on a rat); a second base if a creature ever needs its own animation set.
