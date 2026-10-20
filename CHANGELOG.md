# Changelog

Every public release of the Nexus download (GK2 Saddle Up with GK2 Donkey API bundled), newest first. Each entry
lists what changed since the previous public release. While working, every change is added under **Unreleased**
straight away; at release that heading becomes the version and date (docs/release-checklist.md, section 6).

- **For players**: what someone playing notices (fixes, features, settings, performance).
- **For mod authors**: changes to the Donkey API's public surface (docs/api.md has the full history per API version).
- **Development**: tools, tests and docs in this repository, not in the download.

## Unreleased

For players:

- **The cart no longer sinks into a hill behind the mount.** Its ground is sampled under the cart body, up to a
  unit and more behind the mount's root, and at the edge of a raised path that sample lands on the level below:
  0.77 lower by the Inquisition Base rocks under the dragon's cart (the user's video, 2026-10-20), within the
  slope limit, so the cart followed it down into the drawn ground. A target that jumps by more than 0.15 between
  two frames without the cart having swapped sides is a ledge, not a slope (walking moves the sample 0.12 a
  frame at most): it is held off and logged, and the cart keeps the mount's level until the sample agrees again,
  which it does once the mount has walked the step itself. Before, the jump was logged and applied.
- **The settings page regrouped** (Saddle Up 0.11.0): seven sections in a new order, each holding one kind of thing.
  Keys. Mount: its picture (with the cart now), Name, Mount, Skin, the babies rows, and "Keep your mount with the
  save" (was in Behaviour). Colours, new: "Choose colours", the picture, then every colour under two headings, "Mount
  colours" (coat and harness, the old Donkey section) and "Cart colours" (paint, wheels, wheel hubs, lantern light,
  from Cart). Effect. Behaviour: the two speeds, "Say why mounting is refused", "Shuffles while standing". Cart: "Cart
  capacity", "Zombies ride on the cart", "A storage chest rides on the cart", "Carry the corpse into the morgue",
  "Donkey leaves corpses at the crossroads" and "Calling your mount leaves the cart parked" (all were in Behaviour),
  then "Lantern" and "Lantern lit by day". Sound. The Donkey section is gone. Every value carries over: a moved row
  reads its old place on the first start (the log says which) and the old lines are cleaned out of the cfg.
- **The colours window** puts the rows under the same two headings, "Mount colours" in the left column and "Cart
  colours" in the right, with Mount and Skin above the first and Effect below the second.
- **What the next update brings, corrected** (the 0.10.0 notes below previewed it; still switched off in this
  version): one dragon, red (a blue one is the red one with a custom coat); Hellfire is a skin pack like any other,
  built in (nothing drawn at run time) and comes with its ring of fire; **Royal Parade** is a third donkey skin (a
  saddle cloth and a crown; the cloth's reds follow your harness colour); Gerry is "a skull" in the keeper's words
  and his babies are his own size; the giant snail and the giant worm are silent; a colour row for a part your mount
  hasn't got is greyed.

For mod authors:

- **Skin packs: an erase colour for skins drawn over the donkey** (docs/skins.md "Erasing from the donkey"). A
  pixel of `erase = FF00FF` (magenta by default for `layer = over`; `none` switches it off) makes the donkey
  transparent there, so an over pack can take the ears off under horns or the tail off under another. Such a picture
  is built whole when first worn (the donkey's frame from the game with the holes, the picture's pixels on top) and
  drawn in the body's place; pictures without it stay plain overlays. Nothing of the game's art goes into the pack.
  The pack checker and the review page honour it; a self-test wears a pack that erases the ears. The step towards
  every skin being a pack (Hellfire next: the user, "for the concept of a mount skin we should do this drawing-over
  thing").
- **The skin tools, one pipeline** (the plan's M6; docs/sprite-tools.md "Making a skin: the one pipeline"):
  `tools/skins/new_skin.py` scaffolds a skin or a mount (the cfg in the canonical layout, a generator stub, the
  Aseprite sidecar, the scan); every generator writes its strips, preview and sheet through one module
  (`tools/skins/review.py`: the front layer composited, the rider on the seat row), so the four creatures have strips
  too; the colour rules of `skin_colors.py` are data (`tools/skins/creatures/colors.json`), the front layer counts and
  Gerry's `[Colors]` comes from the game's export; the Aseprite watcher splits the exported sheet into the built-in's
  per-frame PNGs; the checker knows the front layer and `source = game`, reads the donkey only under an over skin,
  judges lone pixels over what the game draws beneath (Hellfire's one-pixel lava crack), says a kept donkey colour
  and the colour cube instead of warning, and returns an exit code, so it runs in the release scans on every
  built-in; the scan reads pixels (the seat row must be drawn at the pivot column of the standing frame, the erase
  colour is refused in a replace skin, a sheet is a whole number of cells, the `[Game]` map's targets are frame
  names); the review page draws the front layer in front of the rider, the rider on the seat row with the skin's
  offset and the seat line, and the card's facts (seat, hitch gap, babies, colour regions); the Gerry scripts take
  the prefix and find the bundle by content; the creature generator wrote into `tools/src/...` since its move (fixed).
  Wording: the Riding tab says "mount"; the `Debug.MountsPreview` description, the csproj's skins comment and the
  export button's tooltip say what is there now.
- **After the review of the parity work** (three independent reads of the diff, 2026-10-20): the side file's record
  label read the owner column instead of the key, so every kept mount was labelled "donkey" in the log (the data was
  right); the Gerry self-test still expected "gerry" as the noun; a refused unhitch of a mount that became a no-cart
  kind was tried again every tick with two log lines each (now once a second, said once, and run after the rig's own
  work); the layered value a hitch reads took the wanted skin's geometry while the drawing applied the worn one (both
  worn now); a mount without babies of its own gave its `foal_scale` to plain baby donkeys (0.6 now); spawning a
  mount by name that has no skin spawned a donkey (refused); the noun reaches every refusal and log sentence; a code
  skin's sprite colours copied its texture once per sprite (once per texture); the settings picture read the hitch
  gap from the layers every frame (when they change); a page's grey condition composes with a row's own, and Saddle
  Up greys the custom harness row with the harness row. Tools: the checker counts magenta in a replace skin (it
  never did), survives a cfg the canonical writer refuses, says that a front picture can't erase; the canonical
  writer refuses `[Mount.Tuning]` without `[Mount]` (it dropped it) and leaves an empty `[Tuning]` out; the colours
  writer no longer eats the comment of the section after `[Colors]`; the shared review module clears erase holes at
  the right place on a bigger canvas, lays the sheet out by frame order, and puts the rider where the skin's own
  `RiderOffsetPx` / `SeatAdjustPx` put him (the page too, and it re-places him when the scale changes); the watcher
  splits by the sidecar's columns and removes a front picture whose cell is empty; the scan says which PNG it cannot
  read instead of aborting, wants RGBA, and checks the order among the three skin tuning sections; `make_hellfire.py
  --cfg` keeps the effect keys; a fresh creature cfg is canonical; Royal Parade's generator writes its strips through
  the shared module (its Aseprite sidecar keeps naming its `out\pack`: the generator is its source); `new_skin.py`
  takes its flags in any order and refuses `--over` with `--mount`.
- **After the second run** (2026-10-20): two findings in the game, both fixed. The cart-style seat sat 6 px higher
  than the ride seat on every drawn creature: a declared seat row still added the player's own `SeatAdjustPx`, whose
  mod default is -6 riding and 0 with the cart. A declared seat now ignores the player's own value (only the skin's
  own `[Tuning] SeatAdjustPx` or a mod's override nudges from it, in full), and the built-in rows moved by those 6
  (rat and worm 52, the snail 55, the dragon 68; the donkey's own seat is row 52 on the 96 canvas; the scaffold, the
  review tools and the example say 52), so the riding look is exactly as calibrated and the cart look matches it. The
  dragon's near wing (the front layer) was drawn behind the rider in the world: it was sorted above him but not lifted
  past his sprite's plane, and the game draws by distance; the front picture is now lifted past the nearest of the
  keeper's sprites (measured each frame in the body part's space) by `Looks.Rig.FrontLift`, 0.05 world units (2.5
  art pixels; the first lift was a fifth of a pixel, which tied in the depth buffer and flickered his shirt through
  the wing while he moved, seen 2026-10-20; the front-layer test now moves him 40 frames and counts the pixels lost
  at the shipped lift and at the old one). The cart readout's rule was wrong, not the gaps:
  the rat's and the dragon's declared gaps (judged by eye) leave their carts about 10 px further from the body than
  the donkey's, and the snail, worm and Gerry stand further still without any gap (narrower bodies), so the rule
  asserted is "no cart closer to its body than the donkey's"; the three declare no gap and their cfgs say why
  (docs/seat-investigation.md 9, with the measured table). The Riding tab no longer offers the player's nudge on a
  declared seat. (Third run: Gerry sat at 41.5 px instead of 25.5: a skin's own nudge is an override on the instance,
  which the global override flag did not see; the seat reads the instance's override owners too, and the readout
  now guards Gerry's 25.5. Fourth run: all green but the "pictures loaded and freed" scenario, which assumed nobody
  wears the rat: the player's own mount was the rat, whose one sheet is shared; it now picks a built-in creature
  nobody wears.)
- **The dragon's wings are folded** (the user, 2026-10-20, after riding it: the raised near wing, drawn in front
  of him, hid the rider), drawn as the user's reference sheets draw a resting dragon: the wrist is a tall peak above
  the back with the thumb claw hooking towards the head, the fingers run down from the peak as red lines, and the
  membrane hangs between them like a curtain to the ground behind the hindquarters, scalloped along its hem, wine
  against the body's red and outlined. Both wings root at the shoulder, where wings attach and where the rider
  sits: the peak stands above the shoulder and behind his head, the curtain hangs from it back over the flank, its
  hem on the ground behind the hindquarters only and rising forward along the belly line, both hind legs under
  the curtain (its hem ends on the feet's own row, solid along the ground, the walk bob kept out of it; a hem
  2.5 px lower looked sunk into the ground, and the near leg drawn in front of it read as part of the wing; a
  scan over all 41 frames finds no leg pixel under the hem), nothing of the wing forward of the peak. The whole
  method, the game's placing of the layer, the rider's measures on the canvas and the recipe for the next mount
  are in docs/front-layer.md. The whole near wing is the front layer, over the
  rider wherever the two overlap: he sits inside the fold with the top of his head, part of his face and the
  reins arm out of it. (Tried on the way and rejected: a cut that kept his chest in view by drawing the membrane
  behind him there, and a one-layer wing shaped around him, which grew a lobe under his seat that stood out
  like a thumb when nobody rode.)
  The far wing is the same fold four pixels back and five up (the perspective), drawn behind the
  body, which hides all but its peak and the top of its curtain behind the near one's. (The user's corrections on
  the way: a curtain at the tail, one too high, a far wing of its own shape, and one "back on his ass" when the root
  sat at the mid-back.) (Eight drafts went before it: flat folds and fans along the flank read as a plank, a
  line, a blanket, a flag, a hand fan; the reference read at pixel size was what was missing; the dragon is compared
  with the reference crop at one scale before each hand-over now.) The front-layer readback takes a curtain that
  hangs behind the rider (the order is judged on the body). Two readouts joined the self-tests after that look: the rider's hip as a row of
  the worn skin's canvas (asserted on the dragon's declared 68, which the review strips draw him on) and every cart
  part's shift along the hitch direction (a rigid cart: the far wheel seen beside the near one in the Mods picture
  is to be judged with it). The settings picture no longer moves a cart part's mesh child a second time (the far
  wheel's child is named like its part; the world's cart skipped them already).
- **After the first in-game run of the new tests** (2026-10-20): three probes were wrong, the game was right. The
  cart readout asked a cart part's transform for its renderer (the mesh is a child, as the mount code reads it); the
  front-layer readback counted every drawn pixel, so "the body with the rider" could never be smaller than "the body
  alone" (it now compares the overlap pixel by pixel at one camera spot: whose colour shows); the over-colours test
  classed the scene-lit gold as "red" at dusk (it now checks that the lit red colour is gone, whatever class the
  light gives the gold).
- **The tests nobody had** (the plan's M5, docs/mounts-parity-matrix.md §6): the dragon's front layer read back
  in front of the rider (where they overlap, the pair shows the keeper's colour over the body overlay and the wing's over the keeper); Hellfire and Royal Parade
  on the built-ins themselves (whole frames with the erase marks, the ash coat, the cloth's reds under the harness
  colour, both walking); the snail's own rider offset on its wearer; the cart-style seat and the hitch gap of every
  mount (M2's readout).
- **The code that only knew the donkey** (the plan's M4, docs/mounts-parity-matrix.md §2): the rider's leg overlay
  (NearLeg) hangs from the skin's declared seat as the torso does, not from the donkey's back line; a baby spawned
  without a size is its parent mount's own (`SpawnFoal`'s default), and plain baby donkeys are 0.6 whatever the
  parent is; `Mounts.Spawn` with a cart as a no-cart mount is refused, and a hitched mount that becomes one leaves
  its cart standing where it is (the user's decision; before, the cart stayed hitched as data); `[Base.*]` on a
  replace skin and `[Colors]` on a palette skin are named in the skin's report instead of ignored silently; a mount
  may say `noun = ` ("a skull can't pull the cart"; Gerry says skull) and the save file's and the dormant list's
  labels say the mount instead of "donkey"; a mount with no skin is not offered; `MountSpawn.Mount` spawns a mount
  by name in its Default skin. The API records say more: `MountSkinInfo.Regions` (the colour regions the player's
  colours reach), `MountKindInfo.EffectWhen`, `Noun` and its tuning, `Skins` with the Default first; a code skin
  can declare its colour regions, an erase colour, a base recolour and the picture seat (`MountSkinDef.Regions`,
  `Erase`, `BaseRegions`, `SeatFromPicture`), and a readable sprite's colours join the look (an unreadable one is
  said once). `MountSettingsPage.EnableWhen` and `MountHasRegion` grey a colour row for a region a mount never has.
  Sounds, phase one: every sound key is documented as a mount's to set; the snail and the worm are silent (bray,
  hoofsteps, lip smack off), Gerry's lip smack is off; the scan reads every bound key (the helper-bound ones were
  invisible to it). Self-tests: the two cart-rule doors, the noun, the silent snail and skull, the baby's default
  size, the leg overlay on the snail's seat row, the snail's regions.
- **A skin comes with its effect; the ring fits the creature** (the plan's M3; the user's decision 9.5 (a)): a
  skin may say `effect = ` / `effect_when = ` (`MountSkinDef.Effect` / `EffectWhen` for code), shown when the
  player's Effect row says "As the mount or skin" (its default; the entry was "As the mount"): the skin's first,
  else the mount's. Hellfire says `effect = hellfire`, so a player who picks the skin sees the horns and the fire
  together without opening the Effect section. The effect ring's radius scales by the worn skin's standing-frame
  width over the donkey's 46 px (the rat's 85: almost twice the ring; an over skin or a code-sprite skin keeps
  1.0), measured at the sheet build. The Mounts tab has the skin's effect row; the scan and the layout know the
  two keys; `make_hellfire.py` writes the built-in's cfg once (`--cfg` rewrites it), so a regeneration keeps the
  hand-written lines. Self-test: Hellfire lit under the default row, Royal Parade not, the rat's ring 1.48.
- **The Mounts tab tunes the skin** (the plan's M2): a "The rider's seat on this skin" row (the picture row, the
  picture top, or none; right-click = none) and the rider, leg and cart pixel rows per style under "Rider on this
  skin" (a value already in `[Tuning]` is changed there, a new one goes into the style's section; right-click = not
  set), saved by the same auto-save into the skin's file; the mount's group keeps the speed, NearLeg and its
  other keys. The Riding tab shows the pixel nudge alone while riding a skin that declares its seat, and says why.
  The settings picture places the cart at the mount's hitch gap (the dragon's and rat's cart stood at the
  donkey's distance) and shows no cart for a mount that cannot pull one. Creature facts (the mount's cart rule,
  babies, effect, abilities) follow the wanted skin at once while the drawing's geometry follows the worn one, so
  a rider on the old picture never takes the new picture's numbers. A "cart readout" self-test measures, for every
  built-in mount with a cart, the distance from the body's rear edge to the cart body and the cart-style seat,
  and asserts the rat's and dragon's declared gaps; the snail's, worm's and Gerry's gaps are declared from its
  numbers next.
- **The skin owns its drawing's geometry** (API 0.33.0; the user's decision of 2026-10-20, docs/mounts-parity-matrix.md
  9.1): a skin's `[Tuning]` has `[Tuning.Ride]` and `[Tuning.Cart]` beside it for one style, merged after it and
  before nothing (the merge order is in docs/mounts-design.md §2; `Looks.LayeredValue` reads the same order for the
  hitch); `MountSkinDef.TuningRide` / `TuningCart` for code skins. The three pixel values that sat on mounts moved
  to their skins: the snail's `RiderOffsetPx` into `[Tuning.Ride]`, Gerry's `SeatAdjustPx` into `[Tuning]`, the rat's
  and the dragon's `HitchGapPx` into `[Tuning.Cart]`; the creature generator writes a creature's gap; the scan
  refuses a picture key under `[Mount.Tuning*]`, a key in `[Tuning]` and a style section, `SeatOffset` on a skin
  with a seat row, an empty section, and places the sections after `[Game]`. The Mounts tab's Save writes the three
  sections with their comments and regenerates the `seat` line with its remark. `MountSkinInfo` says the seat row,
  the picture seat, a front layer and a game source. Self-tests: Gerry's value comes from its skin and the mount
  holds no pixel key after a reload; a temp pack's `[Tuning.Ride]` value is the riding value on the wearer.
- **A mount's seat is its skin's row now** (API 0.32.0; the user: "ok let's go for it since it's part of the
  foundation for more mounts"; docs/seat-investigation.md §8). A skin may say `seat = <row>`, the canvas row (from
  the top) the rider's hip sits on; `MountSkinDef.SeatRow` for code skins. The rider sits there in every frame for
  every player, whatever their AutoSeat setting (the picture path Gerry uses, generalised, with the walk bob), and
  the mount's `SeatAdjustPx` nudges from it, so one declaration gives every player the same seat; before, a mount's
  `SeatOffset` reached only AutoSeat-off players and its `SeatAdjustPx` only AutoSeat-on ones. The rat and the worm
  say 46 (the donkey's own seat on the 96 canvas, measured in game: the first guess of 49 sat 3 px low, with
  both modes already equal), the snail 49 (3 px lower, its `SeatOffset` gone; a stray `SeatAdjustPx = -7` from a panel test, committed by accident, hid one pixel in the second run and is gone), the dragon 62 (the 128 canvas); the creature generator writes the row; the scan refuses a row outside the
  canvas and a mount that sets `SeatOffset`; the example mod's checker says 46. The game's own donkey art declares
  no row: a default player's seat is untouched (the readout self-test asserts 6.6 px). The Mounts tab says when a
  skin declares its row.
- **A built-in's reload and undo on the Mounts tab never took** (the user, 2026-10-20: "I changed torso down to
  -4 and pressed undo and it stayed yellow"; the note said "Not reloaded: Value cannot be null. Parameter name:
  path1"): the reload stamps the pack's folder for the auto-reload watch, and an embedded built-in has no folder,
  so it threw before the file was read and the kind kept the panel's value. The stamp is 0 for a built-in or a
  code skin now; the Gerry self-test reloads a built-in from the repo's cfg; the Undo and Reload buttons log
  their outcome, so a failure is in the log and not only in a five-second note. Undo is one step per gesture now:
  saves within four seconds of each other on the same file share the earlier restore point (a slider dragged in
  two moves with a pause took two presses).
- **The mount definitions brought to one shape** (the user: "take a look at the mounts and how we're defining
  them so that we have the same parity"; the investigation is `docs/mounts-foundation-audit.md`). Every built-in
  `skin.cfg` is in one layout now, written by `tools/skins/canonical_cfg.py` and refused otherwise by
  `tests/SkinsScan` and the release scans: `[Skin]`, `[Game]` for a game creature, the colour sections, and the
  `[Mount]` block last with id, name, author, version, cart and foals always (foal_scale, effect, effect_when when
  not the default), every tuning key in one section. Gerry's dead `SeatAdjustPx = 10` is gone (its one value, -16,
  in `[Mount.Tuning]`); the dragon and the worm say `cart` and `foals`; every mount has an author and a version.
  The loader: a `[Mount]` without a name takes its id (sentences and the dev spawn row use the mount's label, so a
  nameless mount never says "donkey" or throws); a built-in skin's mount is built in (`MountKindInfo.IsBuiltIn`);
  author and version survive a reload and count for "the same declaration"; unknown `[Mount]` keys, a bad
  `foal_scale` and a bad `effect_when` are named in the skin's report like every other section's. The Mounts tab's
  Save keeps the comments inside the mount sections (above a key, on its line, before and after the keys) and
  writes the block in the file's order. SkinsScan checks every `[Skin]` value the loader reads by name (layer, kind,
  seat, source, erase, the canvas range), the section order, `[Base.*]` regions and knobs, the `[Mount]` key list and
  values (foal_scale 0.2-1 and not the default, effect a built-in id with its effect_when, `Off`/`Night`/`Always`),
  a key set in both `[Mount.Tuning]` and a style section, an empty tuning section, a `[Mount.Tuning]` without
  `[Mount]`; probed with a wrong cfg (11 findings). Self-tests: Gerry's one seat value and its built-in mount with
  author and version; the pack round trip keeps four comments in the mount sections and the author; an ability that
  throws is off after its first tick while the kind's other goes on. Docs: `effect_when` takes `Off`, not "Never";
  `pivot` and the `[Mount]` author/version documented; the donkey's own block written out next to the five; the
  Mount list and "Default" rule; the stale "Saddle Up's Donkey rows", recipe and blue-dragon lines in api.md,
  reference.md, mount-colours.md, mounts-design.md and skins-design.md marked or fixed. The skin tooling is all
  under `tools/skins/` now (`creatures/make_creatures.py`, `gerry/`, `skin_colors.py`, `skin_preview_html.py`,
  `pngcolors.py`). Open: a mount's seat with the player's AutoSeat off (audit 3.1): `docs/seat-investigation.md`
  has the three seat paths, what every built-in gets under the shipped defaults (the snail's `SeatOffset` works for
  a default player and is lost with AutoSeat on; a mount's `SeatAdjustPx` does nothing for a default player unless
  the skin says `seat = picture`), the options and the compat constraint; a readout self-test ("seat: the rider's
  height on every built-in mount, AutoSeat off and on") logs the seat in pixels for each of the eight looks in both
  modes; docs/reference.md's seat defaults corrected to the shipped values (`SeatOffset` 0.22 both styles,
  `RiderOffsetPx` -6 / -8). Measured in game 2026-10-20: every look sits at 6.6 px with AutoSeat off and 11.5 px
  with it on (the player's two modes disagree by 5 px on the plain donkey), the snail at 3.0 px off and 11.5 on
  (its seat lost in auto mode), Gerry at 25.5 px in both (the picture seat). The seat code is unchanged; the user
  decides. The whole "Skins and effects" group passed in game the same day (16 scenarios, the three warnings the
  tests announce themselves).
- **The creature generator on the same pipeline** (the user: "so now all the skins are on the same level?"):
  `tools/creature_sprites.py` writes the rat, snail, worm and dragon straight into `assets/skins/<name>/` (one name
  as the argument redraws one; the probe's `test\skins\` output and the Blue Dragon entry are gone); all 85 + 3 x 44
  pictures came out byte-identical. Every built-in skin now comes from a generator in the repo writing into its
  `assets/skins/<id>/` folder; Gerry alone has no pictures of its own.
- **Skin leftovers cleared** (the user: "check dirs to see if there's anything obsolete ... so we keep parity"):
  `tools/skin_recipes.py` (the retired recolour recipes' prototype) and `horn_bases_raw.json` are gone from the
  repo; `contact.py` lost `load_raw` / `raw_to_game_shift` and `hellfire_proto.py` reads the probe's frames, so no
  tool reads the old top-left-aligned canvas any more (`horn_bases.py` measures the Hellfire horn bases from the
  Hellfire pack's own bone pixels: 44 of 44 frames the same as the moved v14 positions). Off the dev machine: the
  probe's `test\skins\` experiments, the example mod and the `worm-pack` test pack in the game's plugins, the
  Hellfire review rounds, the raw frame exports, the starter `.aseprite`. `pngcolors.py`'s note names its real user.
- **One pipeline for every skin** (the user: "we need to make them the same since they will both be included",
  "once we set up this pipeline we can keep following it to create more skins for other mounts too"): Royal Parade
  is a built-in pack like Hellfire now, `src/GK2DonkeyAPI/assets/skins/royal-parade/` with `skin.cfg` by hand and
  a PNG per frame written by `tools/skins/royal-parade/make_royal.py` (`--no-assets` for a dry run); the sheet
  form, the `packs/` folder and the copy into the game's `gk2skins\` are gone (the loose copy there was removed:
  two skins with one id are refused). The pipeline is written down in `docs/sprite-tools.md` "Making a skin: the
  one pipeline": generator folder, pack folder, checker + scan + review page, build and deploy, the in-game export.
  `tools/skins/check_skin.py` reads a PNG per frame as well as a sheet. The headless scan passes 17 of 17 with
  Royal Parade among the built-ins.
- **Hellfire is a pack now, not code** (the user: "for the concept of a mount skin we should do this drawing-over
  thing"): `src/GK2DonkeyAPI/assets/skins/hellfire/`, generated by `tools/skins/hellfire/make_hellfire.py` from the
  reviewed prototype: the prototype runs over each of the donkey's frames on the game's canvas and the pack keeps
  only what it changed (horns, the devil's tail, the rump fill, the cracks, the glowing eyes) plus erase marks where
  it took the ears and the tail off; the ash coat is the pack's `[Base.Coat]` recolour. `LooksHellfire.cs` (614
  lines) is gone; the recipe machinery stays for mods (the self-test registers a recipe of its own through the API).
  Same colour regions as before (the cracks follow the muzzle colour, horns and tail the dark parts, the glow the eye).
  Proven the same: `tools/skins/hellfire/compare_hellfire.py` puts every code-drawn frame next to the pack over the
  donkey with a difference picture and count; 44 of 44 identical with the prototype's coat (the devil's tail's sway
  per walk frame matched to the reviewed frames), and with the game's coat rule only coat shades differ: the rule's
  knobs were fitted to the code's darkening formula over the donkey's 14 coat colours (Brightness 0.41, Colorize
  FF4747 at 0.6), the largest difference left 5 of 255 on one channel. The ear remnant the user saw between the
  horns on the page was the horns' own outline, drawn the same by the code; the head pixels are identical.
  What did differ frame to frame (the user: "in some frames there's a gap between the horns, in others it's
  filled") was the mane's tuft that stood between the ears: one column in the standing frames, a block three wide
  where the head bobs (walk 3/4/7/8, the turn). Erasing it left a hole in his head (the user: "it should be
  filled"), so the generator now draws the same mane tuft between the horns in every frame: the four rows above the
  horns' bases, in the donkey's own outline and mane shades (region Dark, so the player's colour reaches them as it
  reaches the mane), 326 pixels added and nothing erased; the compare page shows exactly those 326 pixels as the
  only difference from the code's output, checked on a zoom of all 44 heads through the review page's path.
- **Skin packs: a recolour of the donkey under an over skin** (docs/skins.md "Recolouring the donkey under the
  skin"): `[Base.Coat]`, `[Base.Eye]` ... sections with the player's own knobs (Brightness, Saturation, Colorize,
  Tint, Overlay ...), applied before the player's settings and holding even with them switched off. An ash coat
  under horns without a pixel of the game's art. The erase self-test's pack declares a blue coat and the readback
  checks it.
- GK2 Donkey API 0.31.0: `MountSettingsPage.Heading(section, label, description = "")`, a heading row inside a
  section (a line with no value); in the look bar the look rows after it in that section stand under it in a group
  of their own. A `Color` row whose colour key has a `MovedKey` carries the alias to its `<key>Lightness` key by
  itself, so one `MovedKey` per colour is enough. The look bar deals its groups to the column that leaves the left
  closest to half the rows (before: to the left until the left held half, which with one big section put nearly
  everything there). A row may not share a picture row's (`Preview`) label: the picture is found on screen by it,
  and a heading with the same label got a picture of its own; the page refuses the row with a clear message.

Development:

- `tests/fixtures/released/0.10.0/`: the player files as 0.10.0 wrote them, captured from the release zip's
  rehearsal run (checklist step 3b had been skipped at the release): the two cfgs with the keys new in 0.10.0 set off
  their defaults, 0.30.0's `defaults.cfg`, a real format-2 side file. The upgrade self-test runs over 0.9.1 and
  0.10.0 now.
- `tools/page-carryover.py`: an offline dry run of the page's carry-over, reading the rows and `MovedKey` aliases
  out of Saddle Up's Plugin.cs and checking an older cfg key by key (the fixtures by default, any cfg as an
  argument). Run it before the in-game upgrade self-test whenever the page's sections or keys move.
- The skin tooling is in the repo now (it lived in `C:\ClaudeCodeTemp\GK2-aseprite`, which a commit never saw):
  `tools/skins/` (the pack checker, the watcher, the two Aseprite menu scripts, the .aseprite writer, `gamedir.py`
  for the game folder from Directory.Build.props), `tools/skins/royal-parade/` (the generator and its measurements,
  outputs in a git-ignored `out\`) and `packs/royal-parade/` (the pack's source: `skin.cfg` and the generated
  `sheet.png`, copied into the game by the generator; moved again the same night, see "One pipeline for every
  skin" below). Only the game's frames stay outside, read from the asset
  probe's folder. Verified: the generator run from the repo writes a sheet identical to the game's, the measurement
  scripts reproduce their data files. Hellfire's reviewed Python prototype (the 14 rounds of 2026-10-18/19, the
  rules that `LooksHellfire.cs` carries in C#) is kept as `tools/skins/hellfire/hellfire_proto.py`.
  `docs/skins-inventory.md`: every skin, what it is made of, where its source, its tooling and its game-side files
  live, and what stays outside the repo.
- Royal Parade (the test over-skin): its generator makes the cloth from the donkey's frames and
  writes the pack into the game itself, no drawing step (docs/sprite-tools.md "Generated, no drawing"). The cloth is
  one shape measured on the standing frame, hung from the hip in every frame with each column on that frame's back
  line, so it rides the walk's bob and the graze's dip without changing size (the first cut, measured per frame,
  jumped in the graze clip). The generator writes a frame strip per animation (`strips\`) to look at before the game.
  Then the root of every placement fault: the generator's donkey frames (`harness_frames\*.raw`) had every frame's
  top-left corner at one spot, while the game stands each frame on one ground row, 2 to 17 px apart per frame; the
  generator, the checker and the crown anchors now use the asset probe's frames, the game's own placement
  (docs/sprite-tools.md "The canvas, found wrong").
- **The review page shows what the game built, not its own guess** (the user: "make sure what we see on those pages
  is the same we see in game"): the dev panel's Mounts tab gained "Export the loaded skins as built"
  (`Looks.ExportBuilt`): every skin worn since the start, its pictures read back from the texture the game draws
  from (after the erase compose, whole frames and all), and the donkey's frames as the game reads them, to
  `BepInEx\GK2-asset-probe\game-skins\`. The page prefers that export, says so on the card, and counts the pixels
  where its own composite would have differed (0 is the expectation; a number is a divergence to look at). It also
  gained an "all 44 frames" view: every picture of every skin, named, for a frame-by-frame look.
- `tools/skin_preview_html.py` rebuilt as the skin review page (the user: "we will use it a lot"): every skin the
  API knows from its real sources (the game's donkey frames, the built-in skins in the repo, the packs in the game,
  recipes drawn to pictures such as Hellfire), grouped by mount with the mount's own look first, an over skin shown
  composited over the donkey, the old test folders collapsed at the end; a sticky bar with the clip, a frame
  scrubber and step buttons (arrows, space), a frame-strip view (every frame of the clip side by side), scale, facing,
  night, rider. Output: `BepInEx\GK2-asset-probe\skins-preview.html` (local only: the game's art).
- Self-test "colours: a skin over the donkey keeps the donkey's regions" now reads back what the world draws: the
  overlay renderer through its real material and shader, photographed into a texture, with the swap on, with it
  off (the same picture lit by the scene, dark at night: colours are judged against that render, never against the
  picture's own values) and together with the body part. Royal Parade was seen in the world without its red while
  the Mods picture had it; this check tells the two paths apart. The player's own mount's worn skin is read back
  the same way, for the log.
- **Fixed: a skin drawn over the body (`layer = over`) sorted at the body part's own order**, so the two coplanar
  sprites left the depth test to decide and the overlay showed only outside the body's silhouette in some frames
  (the gold hem below the belly and the border above the back, the red cloth gone: the user's video of 2026-10-19
  17:08). Found by the readback above (the overlay drawn alone was whole; with the body it was not). The over
  overlay now sorts one above the part, the front layer two above when no keeper rides. Not in any public release:
  the skins are switched off for players until the next update.

## 0.10.0 (GK2 Donkey API 0.30.0), 2026-10-19

For players:

- **Only the donkey, for now.** This version's settings have a Mount row and a Skin row, and both hold one
  choice: the donkey, drawn as the game draws it. The other mounts (a dragon, a giant rat, a giant snail, a giant
  worm, Gerry the skull), the skins (Hellfire, skin packs) and the particle effects are built and in this download,
  but switched off until the next update, which switches them on; the settings say so in two "Coming in the next
  update" rows. Nothing you have changes: your donkey, cart, colours and saves carry over as before.
- **Fixed: with GK2 Mod Framework 0.1.19 or newer, Saddle Up's page in the Mods menu was "Faulted"** ("OnRegister
  failed: AmbiguousMatchException", reported 2026-10-19). The newer framework added a second `AddEnum`, and the page
  looked it up by name. It now calls the one it means directly; checked against framework 0.1.16, 0.1.19 and 0.1.23.
  The mod itself kept working; only its settings page in the Mods menu was missing.
- **A Mount section and an Effect section.** Right after Keys: your mount's picture, its Name, the Mount and Skin
  rows (the donkey alone in this version, see above), then the babies rows ("Babies", "Babies come indoors",
  "Babies are the same mount and skin"); after it an Effect section ("Effect": None or As the mount, with the rows
  for when and how it shows; nothing to show in this version). Your Behaviour, Donkey, Cart and Sound settings stay
  as they were, though their sections moved down in the cfg (the old copies are cleaned out of the file on the first
  save); the Name and the babies rows live in the Mount section now (your values move with them).
- **Mount, not donkey.** Saddle Up's settings say "mount" wherever yours may not be a donkey ("Ride your mount",
  "Send your mount away", "Keep your mount with the save", "Mount and cart sounds") and "babies" for the young
  ("Babies", "Babies come indoors", "Babies are the same mount and skin", "Babies have it too"); the Name and the
  babies rows live in the Mount section now (your values move with them); the Donkey section keeps the donkey's own
  coat and harness colours. The keeper's refusals name the mount ("This dragon already pulls a cart", "That is a
  cart without a mount", "Your mount is nowhere to be found"). The F11 panel's Mounts tab says "mount" instead of
  "creature". The game's delivery donkey is still a donkey.
- **The colours window stays put.** "Choose colours" now shows the picture in a slot of fixed size, the rows in two
  columns by section (your mount on the left, the cart on the right) with a heading each, and the custom colour and
  lightness rows always in place, greyed until the preset is Custom. Nothing moves when a value changes (before,
  those rows came and went and the whole stack shifted under the cursor), and a window taller than the screen is
  scaled down whole instead of running off the top. The picture no longer keeps the previous mount's lantern where
  it hung (the preview copy is undressed before it is dressed again), so the mount sits centred.
- **A side file this version can't read is kept, not lost.** If a save's `<save>.gk2donkeyapi.dat` is of a format
  this version doesn't read (a newer one, carried over by Steam Cloud from another machine), it is set aside as
  `<save>.gk2donkeyapi.format<N>.dat` and the log says so; before, the next save deleted or overwrote it, and a
  parked cart's load with it. Updating from 0.9.1: every setting, the custom colours and the kept donkeys and carts
  carry over (docs/compatibility.md).

Built in this version, switched on in the next update (players don't see any of it yet; the Development notes
say how it is switched off):

- **Mounts and skins.** A mount (Donkey, Giant Rat, Giant Snail, Giant Worm, Dragon) has skins (the donkey: the game's
  own and Hellfire; the dragon: its own look): the Mount row lists every mount, the Skin row that mount's skins (cfg
  keys Mount, Skin_<mount>, FoalsWearSkin, Effect...). A mount carries its own seat for the rider and its rules (a mount
  can refuse the cart), and mods can give one abilities. The "Effect" row gains "As the mount".
- **Skins.** Your donkey can be drawn as something else: the giant rat, the giant snail, the giant worm and a red and a blue dragon (their wings raised above the rider) are built in
  (our own art). Donkey settings: "Skin", a list to pick from (the donkey, the built-in skins, every pack installed;
  in the Mods menu, the game's Settings window and the colours bar), and "Babies are the same mount and skin". The skin is
  kept with the save. Skins from other mods come later.
- **Hellfire, a skin of the donkey that changes the drawing, not the colours:** horns where the ears were, a long
  devil's tail with a spade where the tail was, eyes that glow, lava cracks across an ash coat. Drawn from the
  donkey's own frames the first time it is worn, a few frames at a time, so every clip has it and nothing of
  the game's art ships. The earlier five recolour-and-pattern skins (Zebra, Ghost, Ember, Golden, Shadow)
  are gone: the colour settings do that now. Blue Dragon is gone the same way: a blue dragon is the red one
  with a custom coat.
- **Skin packs.** A skin from anyone, no code needed: a folder `gk2skins\<id>\` with a `skin.cfg` and a PNG per donkey
  frame, dropped into `BepInEx\plugins` (or into any mod's folder there). The log says what it found: pictures that
  match no frame, frames still to draw. A pack that goes missing leaves the donkey as the game draws it, and the choice
  is kept for when it comes back (the list shows it as "not installed"). How to make one: docs/skins.md. A pack can be
  one sheet PNG instead of a file per frame.
- **Gerry.** The game's talking skull as a mount (Mount: Gerry): his own frames out of the game, three times his
  size, hopping along with the jaw chattering, a somersault for an idle; cart and babies allowed. A skin pack can do
  the same with any character of the game (`source = game`, docs/skins.md): no pictures to draw, just the
  character's preset prefix and which frame goes where. A mount's babies have their own size (`foal_scale` in the
  mount file, the Mounts tab's "Babies' size"; Gerry's are his own size, the donkey's foals stay 0.6), and Saddle
  Up's babies take it within a second. The rider sits on Gerry's skull and rides its (much reduced) hop: a skin may
  say `seat = picture` (the rider's seat is the picture's own top, per frame) and a game-creature skin `lift` (how
  much of the creature's jump is kept). The Mounts tab says when the player's AutoSeat (off by default) makes the
  torso rows moot, and lets a mount set the torso height players actually use; the Instances tab's "Log drawing"
  writes every renderer around a mount into the log. The Mounts tab saves by itself, like the Cargo tab: a moment
  after the last change the values are in the skin's file (and the mount's declaring files), with the time shown,
  "Undo the last change" to put a file back, and no Save button to forget.
- **The cart sits behind a longer mount.** A mount file can set `HitchGapPx` (the whole cart, shafts included, that
  many art pixels further behind than behind the donkey); the dragons set 24, so their body isn't drawn inside the
  cart. A cart left standing stays where it was drawn, and a mount taking a cart stands its gap ahead of it. The F11
  panel's Mounts tab has the row for the cart style; the "hitch and unhitch" self-test checks it on both facings and
  across leaving and taking the cart.
- **Hellfire.** Donkey settings: "Effect" (None or Hellfire: the game's own lamp fire in a ring round your donkey, with
  its sparks and a trail of flames that burn out behind it; mods can add other effects), "Effect shown" (never, at
  night, always), and whether it leaves the trail, goes round the cart too, shows in fights and on the baby donkeys.
  Kept with the save. Never indoors. (First build 2026-10-17 19:36: the Effect list had no "None" entry because an empty id can't be a dropdown choice, so a pick never stuck and the cfg stayed empty; the id is `none` now and a cfg holding "" takes the row's default.)
- **Your colours are kept per mount.** The coat, harness, paint, wheel, hub and lantern-light choices belong to the
  mount you set them on: a green dragon and a brown donkey, each as you left it, and the cart's paint with each.
  Switching the mount brings its own colours back. The donkey's values from before are its own unchanged; every
  other mount starts with the game's colours. Every mount's default skin is listed as "Default".
- **Your colours on every mount.** Coat colour, harness colour and the custom colours with their lightness now
  recolour any mount the same way they recolour the donkey: a dragon's scales, the rat's fur, the worm's skin.
  Each built-in creature says which of its colours are its outlines, its second tone (the dragon's belly, the
  rat's tail and ears, the snail's shell) and its eye, so those follow the matching settings and the rest is the
  coat. A creature without a harness ignores the harness rows. Before, the colour rows did nothing on any mount
  but the donkey. (docs/mount-colours.md)
- **The settings picture changes mount in one step.** Picking another mount or skin re-frames the picture at once,
  on what the new mount draws over two seconds of its walk (a hopping skull included); before, the new mount was
  drawn inside the old one's frame until the once-a-second edge check caught it, then framed on one frame the hop
  overshot, so the picture changed size two or three times in the first second (the video of 2026-10-18 21-07). The
  picture's texture is resized in place, so the displays never go blank between refreshes.
- **A mount asleep far away is still that mount.** Its kind (what its babies' size, its name in messages and its
  effect follow) came from the skin drawn on it, and a mount asleep out of sight has nothing drawn, so it counted
  as a donkey: Gerry's babies stayed donkey-sized (0.6) until he was called and woke (seen 2026-10-18 indoors, four
  big skulls). The kind now follows the mount's skin setting whether or not it is drawn; the self-test "game
  creature" checks a sleeping Gerry.

For mod authors:

- **The mounts, skins and effects API is in this build but switched off** (`Mounts.MountsEnabled`, false in Saddle
  Up 0.10.0's download): `Mounts.Kinds.Kinds` lists the donkey alone, `Mounts.Looks.Skins` and `Mounts.Effects.Effects`
  are empty, and `Register`, `RegisterRecipe`, `RegisterFolder` and `RegisterEmbedded` on them return a handle that
  does nothing (0 for the counts) with one log line per owner, nothing thrown. Register in Awake as usual and check
  `MountsEnabled` to tell your player; the next update switches it all on with no change on your side. The rest of
  0.30.0 below describes the API as it works then.
- **A skin worn over the donkey keeps the donkey's colours** (`layer = over` in its own colours): before, the
  worn skin's colours replaced the donkey's whole colour map, so the donkey under a saddle cloth lost the player's
  coat and harness colours. Now the donkey's regions stay, the colours the skin's `[Colors]` names join them, and
  the colours it doesn't name are kept as drawn (not Coat). A named colour the donkey also has is the donkey's, said
  once in the log. docs/skins.md, docs/mount-colours.md.
- **A skin is the drawing, a mount the creature** (docs/mounts-design.md §1): a skin's `[Tuning]` / `MountSkinDef.Tuning`
  takes only the picture-dependent keys (seat, torso, legs, hitch gap, depth, centring) and refuses the rest with one
  log line; the per-skin foals flag is gone (`skin.cfg foals`, `MountSkinDef.Foals`, `MountSkinInfo.Foals`, the
  `foals` argument of `RegisterRecipe`): whether foals wear a skin follows its mount's `Foals` and the player's
  `FoalsWearSkin`. `HitchGapPx` is a tuning key of the cart style (docs/reference.md); `Looks.LayeredValue` gives a
  creature's value for a style it isn't in yet.
- `MountSettingsPage`: a section moved to another place on the page (its cfg number changes) keeps a player's values:
  a key the cfg lacks under its section takes the value under the same name with another number (of several old
  copies the fullest, then the highest-numbered), and the old copy is dropped from BepInEx's orphans so the file holds
  each value once; `MovedKey(section, key, oldSection, oldKey)` does the same for a key moved to another section or
  name (docs/api.md, "Sections keep your order"); self-test "settings: a moved section keeps a player's values".
  `MountRows` lost its `legacySection` argument (it served one development cfg). `MountRows` is the Mount and Skin
  dropdowns; the babies rows are `FoalRows(section, companion)` (Foals, FoalsIndoors, FoalsWearSkin); `Preview`'s
  default label is "Your mount". Refusal texts name the instance's mount (`Instance.Noun`, internal). Skin packs:
  `source = game` with a `[Game]` section (prefix, scale, key lines) makes a skin out of another game character's
  frames (docs/skins.md); a skin.cfg's other sections survive the Mounts tab's Save. `MountSpawn.Skin`:
  the spawned one wears that skin (and is that mount) from the first frame, as `handle.Tuning.Set(owner, "Skin", id)`
  would; a handle's description says "dragon with cart" rather than "donkey with cart".
- `MountCompanion.Skin` and `FoalsWearSkin`; the instance tuning key `Skin` (`handle.Tuning.Set(owner, "Skin", id)`,
  saved with the instance; "donkey" or "" is the game's own look); `Mounts.Looks` (`Skins`, `Find`, `Report`,
  `MountSkinInfo`); the settings row kind `MountSettingsPage.List` (a pick list of ids with labels, read when first
  shown); skins from mods: `Mounts.Looks.Register` (a `MountSkinDef` with an `IMountSkinSource`), `RegisterRecipe` (a rule
  over the donkey's own frames, `MountSkinFrame`), `RegisterFolder`, `RegisterEmbedded`, `Unregister`, `FrameKeys`,
  `SkinsChanged`; docs/api.md "Skins"; examples/GK2DonkeySkins. API 0.30.0, Saddle Up 0.10.0 (needs 0.30.0).
  `IMountSkinSource.TryGetFrame` gives a `Sprite` (from any texture of yours), not a `Texture2D`.
- Mount kinds (docs/mounts-design.md): what is ridden as a creature type against a skin (how it is drawn). `Mounts.Kinds`
  (`Kinds`, `Find`, `Register` with a `MountKindDef`: the rider's seat for it as values of existing tuning keys for
  both styles or one, `Cart`, `Foals`, a default `Effect`, `Abilities`; `Unregister`, `KindsChanged`), `MountAbility`
  (Attached / Detached / Mounted / Dismounted / Tick hooks, one object per kind), `MountHandle.Kind`,
  `MountSkinDef.Mount` (a skin names its kind; `Foals` nullable), a pack's `[Mount]` and `[Mount.Tuning*]` sections (no
  code), `MountEffects.KindId` ("As the mount"), `MountSettingsPage.MountRows` and `EffectRows` (the rows in one call
  each; Saddle Up uses them). The built-in creatures are kinds declared by their own skin.cfg.
- A skin's front layer: pictures drawn in front of the rider (`front/` in a pack, `sheet_front`, `MountSkinDef.FrontSource`),
  sorted above the keeper's body while he rides, as the leg overlay is; the dragons' near wing uses it.
- A skin's own rider settings and placement: `MountSkinDef.Tuning` (a pack's `[Tuning]` section: existing tuning keys
  set on the wearer as overrides owned by the skin while worn, cleared when it comes off, never saved) and
  `Offset` / `CartOffset` (`offset` / `offset_cart`: the pictures moved on the donkey, riding and hitched).
- Effects: `Mounts.Effects` (`Effects`, `Find`, `Register` with a `MountEffectDef` of parts, sources and emitters,
  `Unregister`, `GameParticles` for the game's own particle systems, `EffectsChanged`), `IMountEffectSource`,
  `MountEffectEmitter` (ring, body, trail), `MountEffectWhen`; the instance keys `Effect` and `EffectWhen`,
  `MountCompanion.Effect` / `EffectWhen`, `Mounts.EffectSettings`; `MountSettingsPage.Toggle` takes `visible`;
  docs/api.md "Effects". Hellfire is defined through this API.

Development:

- Self-test fixes from the first full run with the mounts on after the gate (2026-10-19): "settings: the Mount, Skin,
  babies and effect rows" left the dragon chosen before its per-mount checks that assume the donkey (a test bug since
  it was written); "recipe: built from the donkey's frames" can't watch the build when a donkey of the world already
  wears Hellfire (said as INFO now); "colours: the player's regions" reads the as-drawn hue only by day (at night the
  preview is blue-lit: an open item). The pack auto-reload compared against the files at its first check rather than
  as read, so a change made before the switch went on was missed: fixed, the scenario is green.
- `tests/fixtures/released/0.9.1/dreas.gk2.saddleup.cfg` gained the six `Custom*` palette keys (CustomCoat = Plum ...)
  that 0.9.1 wrote and the reconstruction had left out, so the upgrade self-test covers the custom colours too. The
  0.9.1 files were run live through the 0.10.0 build on 2026-10-19 (release-checklist.md section 5): 0 lost of 485 values.
- **How the mounts are switched off for players**: the dev key `Debug.MountsPreview` (off by default; a player's
  cfg has no [Debug] keys, so it answers false there). Off, `Looks.RegisterBuiltIns` and `Effects.RegisterBuiltIn`
  don't run (nothing but the donkey kind exists, so every list and the colours per mount hold the donkey alone) and
  the public API keeps registrations out (`Mounts.KeptOut`: a handle to nothing, one info line per owner); a skin
  that is set but missing is an info line, not a warning, while off. On, everything as before. The F11 panel's Debug
  tab has the switch (a change takes the next start) and the log's first lines say which state the game is in.
  Self-tests: a `SKIP` state (counted apart from warnings; the panel shows it), the scenario "mounts off: only the
  donkey offered, registrations kept out", and the eleven scenarios that need the built-ins skip while off (run the
  suite with the key on for them). The flip for the next release is one change: the key's default to true, or the
  key gone and the two `RegisterBuiltIns` calls unconditional.
- `tests/FrameworkScan` check 5: no GK2.Framework member looked up by name (a newer framework's overload makes such
  a lookup throw); it fails on the 0.9.1 DLL and passes on the fix.
- **Skin packs reload by themselves while drawing** (dev only, `Debug.SkinPackAutoReload`, off by default; a switch
  under the Mounts tab's "Reload from the file"): a pack in use is read again a second or two after its files change
  on disk. With the watcher (`watch_skin.py`, every save in Aseprite exported, checked and previewed as GIFs), a
  round is draw, save, look. Self-test "skin pack: read again by itself when its files change (dev switch)".
- **Sprite tools** (docs/sprite-tools.md): after Hellfire took 14 rounds of frames, skins move to a draw-and-look
  loop in Aseprite, built from source for our own use. Two Aseprite scripts, "GK2 Export skin" (every visible layer
  but the donkey reference into the pack's sheet.png, an optional check run after) and "GK2 Part to all frames" (a
  part redrawn once, sent to the other frames at each frame's own place), and a draft generator for the Royal Parade
  skin (a caparison and a crown; the crown placed from the reviewed Hellfire horn positions). Self-test "colours: a
  skin over the donkey keeps the donkey's regions".
- The F11 panel's Instances tab spawns any mount with any of its skins ("Mount and skin" cycler above the buttons;
  "with its cart" and the family button greyed with the reason when the mount refuses the cart or has no babies; the
  babies wear the parent's skin; rows name the mount and skin). Its wording says mount, not donkey.
- Tests of the mounts and skins system (docs/self-tests.md): a headless `tests/SkinsScan` (every built-in skin.cfg
  parses as the loader reads it, all 44 pictures per skin at the canvas size, the front layer, a skin's `[Tuning]`
  holds picture keys only, a mount's tuning keys exist, a mount declared by several skins is declared the same way;
  in `release-scans.sh`); self-tests "settings: the Mount, Skin, babies and effect rows" and "skins and kinds: the
  changed events"; "hitch and unhitch" checks the hitch gap on both facings and across leaving and taking the cart;
  "kept-with-save text" keeps the skin id and leaves the mount's own values out; "settings: a moved section keeps a
  player's values" covers several old copies, `MovedKey` and the orphan cleanup; the pack tests announce the warning
  they cause; "mount kind from a pack" carries `foal_scale` through the pack, the save and the reload, and "game
  creature" checks the seat on the skull's own picture and the babies' size per mount (Gerry 0.33, donkey 0.6).
  The eight looks scenarios are in docs/self-tests.md now. docs/gerry-mount.md: Gerry's sprites (73
  frames, 96 canvas, a hopping skull) and three ways to make him a mount.
- Hellfire is `LooksHellfire.cs` (a recipe over the donkey's frames in top-down canvas coordinates; its pieces placed
  from the eye, the rump, the ear and the torso's box; prototyped in Python over exported frames first). The five
  recolour recipes, Blue Dragon and their tests are gone; `Colors.RegionSourceColours` gives a recipe the donkey's
  coat and outline colours; a code recipe can declare colour regions (`SkinDef.ColorRegions`).
  The game's Hellfire was checked against the prototype over all 44 frames, at the game's own canvas offset: the same
  pixels. To get there, everything that rounds now rounds the same way in both and relative to its own box (half up;
  the game's half-to-even and absolute positions moved a crack by a pixel with the frame's place on the canvas), in
  double precision, and the torso's edge is measured again after the tail is gone in the head-down frames. The hand
  tables (ears, horns, eyes) are relative to the drawn area, the horns sit on the eye as in the ear-twitch frames.
- `MountSettingsPage.PerMount(rows)`: a row set per registered mount (the donkey's keys plain, the others suffixed
  `_<mount>`), the chosen mount's shown (`Row.Shown`), a row's own condition apart (`Row.Enabled`: the look bar
  greys instead of hiding); `MountCompanion.LookOf(mount)`, `MountId` and the `MountLook` type; the companion's
  `Coat`, `Harness`... are the current mount's. The self-test "settings: the Mount, Skin, babies and effect rows"
  covers it. `Looks.DefaultSkinOf`: a mount's default skin, labelled "Default".
- Colours per look (docs/mount-colours.md): a skin's colours are collected as its sheet is built and sorted by
  region from its `[Colors]` section (loader, SkinsScan with a PNG reader of its own); the colour engine maps the
  worn look's colours per sprite group and uses the colour cube over 48 colours; the lightness anchor is per look
  and a companion re-applies its look when the skin's sheet is whole; self-test "colours: the player's regions on
  a mount in its own colours" (the settings picture read back). The `[Colors]` lists of the five picture skins.
- **Compatibility with released versions** (docs/compatibility.md): the four stores a player owns and the rule per
  store (page keys and enum names permanent, sections free to move, `MovedKey` for moved keys; API cfg names
  permanent; every released side-file format stays readable). `tests/fixtures/released/0.9.1/`: the player files as
  the public release wrote them (reconstructed from commit 7ac9a6c), with every value off its default; later versions
  are captured at release (checklist 5, steps 3b and 3c). The self-test "upgrade: a released version's player files
  are all read" runs Saddle Up's cfg through the live page's own carry-over (`MountSettingsPage.Upgrade`: every old
  line lands on a row and reads as its type), checks every old API key is still bound, reads the side file through
  the real reader, and the set-aside of an unreadable one. The release checklist's "nothing shipped" bullet is
  replaced by this check. Open: changed defaults never reach existing players (BepInEx writes every value); the
  stamp-and-table mechanism is described, decision pending. The live run with the 0.9.1 file (2026-10-18) found the
  last row's old line still in the cfg: BepInEx saves inside every Bind, before the page drops the old copy. A page
  now holds the mod's save-on-set while it binds and writes the file once at Publish (`FinishBinding`; one write on
  a first start instead of one per row, as the API's own cfg already did), and logs how many old lines it left out.
- Looks.cs: the skin rig (docs/skins-design.md step 1): overlays under bdy/hed, the hide re-asserted per frame, the apply
  in Camera.onPreCull, attach/detach at the view's lifecycle points, refcounted pictures; the self-test "skin: on, off,
  removed"; the built-in skins under src/GK2DonkeyAPI/assets/skins (tools/creature_sprites.py writes them).
- Step 2: the pack loader (Looks.RegisterFolder/RegisterPack/Unregister/Validate), the self-tests "skin pack: a folder
  read, validated, worn, withdrawn" and "skins: no leaks over spawn and remove".
- Step 5: the public registration API (a mod's textures stay its own; the same owner replaces, another is refused).
- Step 4: recipe skins (LooksRecipes.cs: the frames read back from the character atlas onto the 96x96 canvas, a pixel
  rule per recipe, 6 frames a tick, the switch whole); the self-test "recipe: built from the donkey's frames, worn whole".
- Step 3: the List row across FrameworkLink (AddDropdown with the ids; MenuLabels writes the labels), SettingsRows and
  the look bar; docs/skins.md for pack authors.
- Step 6: Effects.cs, the particle effects system (templates copied from the game's particle systems by material, the
  materials held through Addressables; a holder per instance that follows the body; ring, body and trail emitters;
  the night test on the `_SunLight` shader global, no game member; the trail's pieces pooled and replayed in place,
  nothing made or destroyed while walking), Hellfire registered through the public API; the
  self-test "effect: hellfire lit, follows, out, gone with the donkey"; `Perf.S.Effects`.
- The layers kept apart on the panel: rows the ridden creature sets are orange and locked on the Riding tab (tooltip
  says where to tune them), orange in a donkey's edit mode (drag = that donkey's own, right-click = the creature's
  back), owners named plainly; a cleared instance key returns to the creature's value (mods' Clear too).
- The tuning panel's Mounts tab (TuningWindow.Mounts.cs): a creature's own values edited live and saved into its
  skin.cfg (Looks.SaveCfg / ReloadCfg, Kinds.Touch / Redeclare); the creature generator leaves an existing skin.cfg alone.
- Kinds.cs: the mount kind registry (code and pack declarations, the donkey kind), the kind resolved on the skin with a
  version counter, the tuning merge per style on the skin rig, the cart rule in TryMount / Hitch / SwitchStyle, the
  abilities ticked on the rig (`Perf.S.Abilities`); self-tests "mount kind from a pack" and "mount kind from code".
- Looks.cs reworked for scale: a `SkinBase` record (the donkey's keys, parts, canvas, pivot, ppu, sprite prefix,
  atlas, back line; everything reads it), one sheet texture per skin filled a few frames a tick from any source (PNGs,
  a sheet PNG, a recipe; `Loaded.Produce`), sprite sources for code; the rig's parts as slots per base part; the
  self-tests count one texture per skin. The per-frame apply allocates nothing (Perf.S.Looks shows bytes).

- Investigation: can the mods bring art from outside the game (another animal, flames, fiery steps)?
  docs/external-assets.md, and tools/GK2AssetProbe (dev only, never shipped; Shift+F7) to answer it in game.
- Design draft for "Looks" (Hellfire, skins in an open folder format, sounds of a skin, an optional "Earn it"):
  docs/mount-looks-design.md. Not built.
- The skinning system designed in full against the code (lifecycle, drawing, formats, persistence, the public API, the
  settings row, validation, game updates, tests, build order): docs/skins-design.md. Not built.
- tools/skin_recipes.py (with tools/pngcolors.py): prototype "recipe" skins made from the donkey's own frames, for
  judging in game with the asset probe's test skins (local previews, never shipped).
- tools/skin_preview_html.py: a local HTML page animating every test skin next to the plain donkey (every clip, both
  facings, day or night); it embeds the game's frames, so it stays on the dev machine.
- The asset probe 0.2.0: the window in tabs (Skins, Fire, My files, Capture, Game, Old tests), and a Capture tab that
  films the donkey and its rider for 4 s, played on the preview page; and "Export the rider": the keeper alone
  (rider.png), which the preview page can lay over every skin.
- tools/creature_sprites.py: a giant worm, a giant snail (with a saddle), a giant rat and a dragon in two palettes, red and blue (128 canvas: body on the seat line, a bat wing raised over the back so it shows above the rider, a scalloped membrane, spines, belly plates; from the user's reference sheets, our own drawing) drawn by code (our own art, no game pixels), a picture for
  every donkey frame, fitted to the donkey's seat and ground; and a high-wing dragon recipe (dragon_high) whose wings
  show above the rider.

## 0.9.1 (GK2 Donkey API 0.29.1), 2026-10-16

For players:

- **Smoother frames while playing.** The Mods menu's donkey picture and its lists looked for the Mods window four times
  a second while you played, even with the menu closed: each look took 1-2 ms, so a frame of 2-3 ms extra four times a
  second. It now looks only at the main menu or while the game is paused (the only times the window can be open).
- **No memory held by parked carts.** A cart left standing (H while driving) and hitched again was kept in memory
  afterwards (about 50 objects each, up to about 30 carts). It is now let go at once.
- **Two logs stacked on the cart** line up better: the top log is placed slightly differently along the cart.

For mod authors:

- No API change. `Mounts.ApiVersion` is 0.29.1; mods asking for 0.28.0 or 0.29.0 load as before.

Development:

- Profiling with Performance Hog (a per-mod profiler for GK2, used on the dev machine only): docs/performance-hog.md.
- tools/GK2PerfRunner (dev only, never shipped): drives the keeper round a recorded loop and runs scripted profiling
  suites (on foot, riding, the cart with six items, standing, calling and leaving the cart), several in a row, with a
  count of the API's caches to find leaks. tools/perfhog-report.py reads the reports (`--suite`, `--series N`).
- The showcase director (dev only) opens with Shift+F9 now (F9 and Ctrl+F9 are Performance Hog's), and no longer
  floods the log with the game's "Cannot find instance of EnvironmentEngine" at the main menu.
- Results and baseline numbers: docs/performance.md.

## 0.9.0 (GK2 Donkey API 0.29.0), 2026-10-16

The first public release (sources as committed in `6be69a2`).
