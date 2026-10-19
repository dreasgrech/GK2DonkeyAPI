# Changelog

Every public release of the Nexus download (GK2 Saddle Up with GK2 Donkey API bundled), newest first. Each entry
lists what changed since the previous public release. While working, every change is added under **Unreleased**
straight away; at release that heading becomes the version and date (docs/release-checklist.md, section 6).

- **For players**: what someone playing notices (fixes, features, settings, performance).
- **For mod authors**: changes to the Donkey API's public surface (docs/api.md has the full history per API version).
- **Development**: tools, tests and docs in this repository, not in the download.

## Unreleased

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
