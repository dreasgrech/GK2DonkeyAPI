# Changelog

Every public release of the Nexus download (GK2 Saddle Up with GK2 Donkey API bundled), newest first. Each entry
lists what changed since the previous public release. While working, every change is added under **Unreleased**
straight away; at release that heading becomes the version and date (docs/release-checklist.md, section 6).

- **For players**: what someone playing notices (fixes, features, settings, performance).
- **For mod authors**: changes to the Donkey API's public surface (docs/api.md has the full history per API version).
- **Development**: tools, tests and docs in this repository, not in the download.

## Unreleased

For players:

- **Skins.** Your donkey can be drawn as something else: the giant rat, the giant snail, the giant worm and a red and a blue dragon (their wings raised above the rider) are built in
  (our own art). Donkey settings: "Skin", a list to pick from (the donkey, the built-in skins, every pack installed;
  in the Mods menu, the game's Settings window and the colours bar), and "Babies are the same mount and skin". The skin is
  kept with the save. Skins from other mods come later.
- **Five more built-in skins, made from the donkey's own frames:** Zebra, Ghost, Ember, Golden and Shadow. They are
  drawn the first time a donkey wears them, a few frames at a time, and the donkey keeps its look until the whole set
  is ready.
- **Skin packs.** A skin from anyone, no code needed: a folder `gk2skins\<id>\` with a `skin.cfg` and a PNG per donkey
  frame, dropped into `BepInEx\plugins` (or into any mod's folder there). The log says what it found: pictures that
  match no frame, frames still to draw. A pack that goes missing leaves the donkey as the game draws it, and the choice
  is kept for when it comes back (the list shows it as "not installed"). How to make one: docs/skins.md. A pack can be
  one sheet PNG instead of a file per frame.
- **Mounts and skins.** A mount (Donkey, Giant Rat, Giant Snail, Giant Worm, Dragon) has skins (the donkey: the game's
  own, Zebra, Ghost, Ember, Golden, Shadow; the dragon: Red Dragon, Blue Dragon). A "Mount" section of its own right
  after Keys (its picture, a Mount dropdown, then a Skin dropdown with that mount's skins, the foals toggle) and an
  "Effect" section after it, before Behaviour (cfg keys Mount, Skin_<mount>, FoalsWearSkin, Effect...). Your
  Behaviour, Donkey, Cart and Sound settings stay as they were, though their sections moved down in the cfg (the old
  copies are cleaned out of the file on the first save). A mount carries its own seat for the rider
  and its rules (a mount can refuse the cart), and mods can give one abilities. The "Effect" row gains "As the mount".
- **Mount, not donkey.** Saddle Up's settings say "mount" wherever yours may not be a donkey ("Ride your mount",
  "Send your mount away", "Keep your mount with the save", "Mount and cart sounds") and "babies" for the young
  ("Babies", "Babies come indoors", "Babies are the same mount and skin", "Babies have it too"); the Name and the
  babies rows live in the Mount section now (your values move with them); the Donkey section keeps the donkey's own
  coat and harness colours. The keeper's refusals name the mount ("This dragon already pulls a cart", "That is a
  cart without a mount", "Your mount is nowhere to be found"). The F11 panel's Mounts tab says "mount" instead of
  "creature". The game's delivery donkey is still a donkey.
- **The cart sits behind a longer mount.** A mount file can set `HitchGapPx` (the whole cart, shafts included, that
  many art pixels further behind than behind the donkey); the dragons set 24, so their body isn't drawn inside the
  cart. A cart left standing stays where it was drawn, and a mount taking a cart stands its gap ahead of it. The F11
  panel's Mounts tab has the row for the cart style; the "hitch and unhitch" self-test checks it on both facings and
  across leaving and taking the cart.
- **Hellfire.** Donkey settings: "Effect" (None or Hellfire: the game's own lamp fire in a ring round your donkey, with
  its sparks and a trail of flames that burn out behind it; mods can add other effects), "Effect shown" (never, at
  night, always), and whether it leaves the trail, goes round the cart too, shows in fights and on the baby donkeys.
  Kept with the save. Never indoors. (First build 2026-10-17 19:36: the Effect list had no "None" entry because an empty id can't be a dropdown choice, so a pick never stuck and the cfg stayed empty; the id is `none` now and a cfg holding "" takes the row's default.)

For mod authors:

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
  default label is "Your mount". Refusal texts name the instance's mount (`Instance.Noun`, internal).
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

- Tests of the mounts and skins system (docs/self-tests.md): a headless `tests/SkinsScan` (every built-in skin.cfg
  parses as the loader reads it, all 44 pictures per skin at the canvas size, the front layer, a skin's `[Tuning]`
  holds picture keys only, a mount's tuning keys exist, a mount declared by several skins is declared the same way;
  in `release-scans.sh`); self-tests "settings: the Mount, Skin, babies and effect rows" and "skins and kinds: the
  changed events"; "hitch and unhitch" checks the hitch gap on both facings and across leaving and taking the cart;
  "kept-with-save text" keeps the skin id and leaves the mount's own values out; "settings: a moved section keeps a
  player's values" covers several old copies, `MovedKey` and the orphan cleanup; the pack tests announce the warning
  they cause. The eight looks scenarios are in docs/self-tests.md now. docs/gerry-mount.md: Gerry's sprites (73
  frames, 96 canvas, a hopping skull) and three ways to make him a mount.
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
