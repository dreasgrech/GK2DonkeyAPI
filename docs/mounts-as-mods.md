# Mounts and skins as separate mods (2026-10-20)

**2026-10-21:** the donkey's two skins (Hellfire, Royal Parade) and the Hellfire effect moved to Saddle Up as well; the API ships no skin or effect of its own (the user: the backend, not bloated with skins). Paths below that say `src/GK2DonkeyAPI/assets/skins` are the state before the move.

**2026-10-24:** the packs are sorted by mount (the mount folders). A mount is a folder `gk2mounts\<mount>\` with its
`mount.cfg` (`[Mount]`, `[Stats]`, `[Tuning*]`, `[Sounds*]`) and its `sounds\`, and its skins are the folders under
its `skins\` (`skin.cfg`, the PNGs, `front\`, the skin's own `sounds\`); in this repo `src/SaddleUp/assets/mounts/`.
A skin's `[Mount]` block, the declaring skin, joiners and `gk2skins\<id>\` are gone. Where sections 1 to 9 describe
those, they are the 2026-10-20 state; the current forms are given next to them, and docs/skins.md has them all.

How a mod registers a mount of its own, in one page: [making-a-mount.md](making-a-mount.md).

The user: *"are mounts also structured all the same or do they have fiddly code involved? ... my idea is we would be
able to create separate mods containing mounts and skins to integrate with Saddle Up / Donkey API. So for example,
we could release a Saddle Up Dragon mod which is downloaded separately, but once loaded as a mod, can be used in
Saddle Up / Donkey API."* This is the investigation: what the code does today (read, not assumed; the line numbers
are of 2026-10-20 night, after the parity work of docs/mounts-parity-matrix.md section 11), what a separate mount mod
would be, where it breaks, and what is left to decide. Nothing was changed while writing it.

## 1. The verdict

**Yes, and the runtime is ready for it today.** Every mount we ship is already a pack in the pack format anyone may
use: one folder with `skin.cfg` and a PNG per frame (`front\` for the layer drawn in front of the rider), the mount
declared in that file's `[Mount]` block (since 2026-10-24: a mount folder with `mount.cfg`, its skins under
`skins\<skin>\`). The API's shipped code (everything outside `Dev/SelfTest.cs`) names no
mount or skin id anywhere: the only built-in id in code is the `hellfire` effect (`Effects.cs:186`), which is an
effect, not a mount. There is no dragon code, no rat code, no Gerry code: the dragon's 128 canvas, its front layer,
its hitch gap, its belly colour, Gerry's game frames and his picture seat are all keys of their files, read by one
loader. So a "Saddle Up Dragon" mod is the dragon's folder moved out of `src/GK2DonkeyAPI/assets/skins/` into a mod
of its own, and nothing in the game would know the difference (today: `src/SaddleUp/assets/mounts/dragon/`, the
mount's `mount.cfg` and `sounds\` with the red dragon's look under `skins\red-dragon\`).

What *is* fiddly sits in three places, none of them the runtime: **the load order of a mod that registers from code**
(section 4: a real gap, with a fix on our side), **the tooling and the tests** (sections 5 and 6: a dozen scripts and
sixteen self-tests hold the path `src/GK2DonkeyAPI/assets/skins` or the ids of the built-ins), and **the gate**
(`Debug.MountsPreview`: until the next release flips it, a player's game keeps every third-party registration out,
`Mounts.KeptOut`).

## 2. What the API offers a separate mod, as built

Three ways in, all public since API 0.30.0 (`Api/MountLooks.cs:14-24`, docs/api.md "Where skins come from"):

| Route | What the mod ships | Code | Found by |
|---|---|---|---|
| **A loose pack** | `BepInEx\plugins\<AnyFolder>\gk2mounts\<mount>\mount.cfg` + `skins\<skin>\skin.cfg` + PNGs (or one sheet); a mount folder without `mount.cfg` adds skins to a mount registered elsewhere (was `gk2skins\<id>\skin.cfg` until 2026-10-24) | none, no DLL | the API's own scan at its Awake (`Looks.RegisterBuiltIns`: `plugins\gk2mounts\<mount>\` and `plugins\*\gk2mounts\<mount>\`, one level down, `RegisterFolder`) |
| **A DLL with the pack embedded** | one DLL; the same folder as `EmbeddedResource` | one line in Awake: `Mounts.Looks.RegisterEmbedded(Guid, Assembly, "MyMod.mounts.")` (MountLooks.cs) | the mod itself, in its Awake; the loader is the one Saddle Up's mounts use (`Looks.RegisterEmbedded`: the csproj line `assets\mounts\**\*` with `LogicalName="GK2SaddleUp.mounts.%(RecursiveDir)%(Filename)%(Extension)"` is exactly what a mod would copy, its own prefix in place of `GK2SaddleUp`; until 2026-10-24 `assets\skins\**\*` and `<prefix>.skins.`) |
| **A DLL with code** | a DLL; pictures from code (`IMountSkinSource`) or a recipe; a mount with abilities (`MountAbility`) | `Mounts.Catalog.Register`, `Mounts.Looks.Register` (examples/GK2DonkeySkins/Plugin.cs) | the mod, in its Awake |

A mount declared by a pack and a mount registered from code are the same `Entry` (`Catalog.Declare`, Catalog.cs:69-80:
first declaration wins, later skins with `mount = <id>` join it; since 2026-10-24 a pack's `mount.cfg` with a higher
`[Mount] version` takes over a pack's mount, and a code mount is never replaced). A second dragon colour from a third
mod is a pack with `mount = dragon` and no `[Mount]` block (today: `gk2mounts\dragon\skins\<its id>\`, a mount folder
with no `mount.cfg`), and it lists under the dragon's Skin row by its name (Looks.cs:1242).

**Proof already in the suite.** The self-test "mount from a pack" writes a loose `gk2skins\mount-test` folder
(today `gk2mounts\test-beast\mount.cfg` with `skins\mount-test\`) with a `[Mount] cart = false` block, registers it
through the folder scan, finds the mount listed, wears it, is refused the cart with the mount's name, withdraws it,
and round-trips the file (SelfTest.cs ~2700-2780; the mount reports
`IsBuiltIn` false and the file's author, line 2717). "skin pack: a folder read, validated, worn, withdrawn" does the
same for a pack with a `front\` picture and a `[Tuning]` key. So the loose-pack route for a mount of its own, with a
front layer, is proven in the game every full run. The embedded route is proven by the five built-ins every start,
on our own assembly; another assembly goes through the same function with a different prefix (the only difference
is `builtIn: false`, which decides the Mounts tab's Save path, Looks.cs:780, and the Mount list's sort order).

**The dragon as a pack, checked.** `assets/skins/dragon/` is 44 PNGs, `front\` with 41, and a `skin.cfg` with
`[Skin]`, `[Tuning.Cart] HitchGapPx = 24`, `[Colors]` (Dark, Eye, Muzzle) and `[Mount]`. `RegisterPack`
(Looks.cs:386-404) reads the folder's PNGs and its `front\` subfolder and hands them to the same `FromCfg` the
embedded loader uses. Nothing of the dragon lives outside that folder except the generator (section 5) and the tests
(section 6). (Since 2026-10-24: `src/SaddleUp/assets/mounts/dragon/` holds `mount.cfg` (`[Mount]` with
`default_skin = red-dragon`, `[Sounds]`) and `sounds\` with the flight's twelve `.ogg`, and `skins\red-dragon\` the
PNGs, `front\` and a `skin.cfg` with `[Skin]`, `[Tuning.Cart] HitchGapPx = 24` and `[Colors]`.)

## 3. What a "Saddle Up Dragon" mod would be

Two shapes, both valid today; the recommendation is the second.

**(a) A zip of a folder.** `BepInEx\plugins\GK2SaddleUpDragon\gk2mounts\dragon\...` (`gk2skins\dragon\` before
2026-10-24): the dragon's folder as it is in the repo (or its skin's sheet form: `sheet = sheet.png`,
`sheet_front = front.png`, two PNGs instead of 85). No DLL, no version, no dependency declaration: a mod manager sees files, not a plugin. The API finds it at its own Awake, before
Saddle Up's (hard dependency), so every list has it. The cheapest route and the one an artist takes
(docs/skins.md "Sharing it").

**(b) A plugin DLL with the folder embedded** (recommended for anything we publish; since the evening it is how Saddle
Up itself ships its five mounts, section 10): `GK2SaddleUpDragon.dll` with
`[BepInPlugin("dreas.gk2.saddleup.dragon", "GK2 Saddle Up: Dragon", "1.0.0")]`, `[BepInDependency("dreas.gk2.donkeyapi",
"0.33.0")]`, the `assets\mounts\dragon\**` embedded with the csproj line above (`assets\skins\dragon\**` before
2026-10-24), and an Awake of five lines: the API
version check (`Mounts.ApiVersion`, as the example does) and `RegisterEmbedded`. It carries a version and the
dependency (mod managers and the Mods menu show it; an old API is refused with one log line instead of a half-read
file), one file instead of a folder of pictures, and a home for an ability later (the dragon's wing beat, a flight)
without a new mod. It is `examples/GK2DonkeySkins` with the pictures swapped in for the code source; the csproj there
is the template (a `HintPath` to the released `GK2DonkeyAPI.dll`, no game references at all, no dependency manifest:
a mount mod touches no game member).

Either way the player's side is the same: the dragon appears in Saddle Up's Mount list, its Skin row says Default,
its colour rows are its own (`Coat_dragon`, `Muzzle_dragon`: the page builds a set per mount, MountSettingsPage.cs
`PerMount`), the settings picture shows it with its cart at its gap, the F11 Mounts tab tunes it, the side file keeps
`Skin = red-dragon` (the skin's id; `dragon` before 2026-10-24) on every instance that is one.

**When the mod is gone again** (uninstalled, or the API loaded without it): the cfg keeps `Mount = dragon` and the
row shows "dragon (not installed)" and is left alone (MountSettingsPage.cs:465, 503); the companion's `MountId`
falls back to the donkey (MountCompanion.cs:227), so the player rides a donkey; the per-mount colour rows vanish from
the page but their cfg lines stay (BepInEx keeps orphaned entries on save); every kept instance with
`Skin = red-dragon` loads as a donkey wearing a missing skin, its cart and babies with it, the record's label says
"donkey" (Persistence.cs:402-420); the choice is kept on all of them and returns with the mod. One thing to change:
with the mounts on, a missing skin id logs a **warning** per start (Looks.cs:2039, "no skin 'dragon' is installed"). Right
for a typo in a cfg, wrong for a mod a player chose to remove: once mounts come from separate mods, that line is an
Info (the no-ignored-warnings rule: a warning a player can do nothing about is a false alarm).

## 4. The fiddly part that matters: load order for a DLL mount

**The finding.** Saddle Up builds its page in its Awake (src/SaddleUp/Plugin.cs:28-162, `Publish` at 162).
`MountRows` (MountSettingsPage.cs:349-397) creates one `Skin_<mount>` row per mount by iterating
`Mounts.Catalog.All` *at that moment*, and `PerMount` (727-750) does the same for the colour rows; `Publish` hands the
rows to the Mods menu at once (`FrameworkLink.TryRegister`, 657; `FrameworkApi.RegisterMod`, FrameworkLink.cs:47),
and the menu's dropdowns are filled then (`FillList`, FrameworkLink.cs:149). The comment on `List` says it: "The list
is read once per session: anything registered after the row is shown appears after a restart." For a loose pack
that is never a problem: the API's scan runs in the API's Awake, which BepInEx runs before Saddle Up's (hard
dependency). For a DLL that calls `RegisterEmbedded` or `Catalog.Register` **in its own Awake**, the order between it
and Saddle Up is not ours to set: BepInEx orders plugins by their dependencies and otherwise by the order it found
their files (in practice alphabetical by folder and file name), and there is no "load before X" attribute. A mount
mod can only say "after the API".

What a mount registered *after* Saddle Up's Awake gets today, from the code: the Mods menu's Mount dropdown lacks it
for the whole session (filled at Publish); the game's own Settings window and the Mounts panel fill their lists
lazily (SettingsRows.cs:481, MenuLabels.cs:52) and may offer it; picked there, `Apply` finds no `Skin_<id>` row and
heals the skin to the mount's first (MountRows's `Apply`), so riding it works; but it has no colour rows of its own
(its colours stay the game's) and no Skin row (a second skin of it can't be chosen). And "after a restart" never
fixes it, because the order is the same every start. The example mod proves nothing here: `GK2DonkeySkins` and its
GUID both sort before `GK2SaddleUp`.

**The fix is ours, small, and belongs in the API** (not built; the user's call):
- `MountRows` and `PerMount` subscribe to `Mounts.Catalog.MountsChanged` and add the rows of a mount that arrives after
  the call, as long as the page is not yet published; and `Publish` moves from the end of Saddle Up's Awake to the
  API's `Start` (every plugin's Awake has run by then; Unity runs all Awakes before any Start), or the page's rows are
  handed to the menu from the first `Update`. Either way a mod's Awake registrations are in before any list is
  built, whatever the file order.
- A self-test: register a mount and a skin from code *after* a page is built and published, and check the Mount row
  offers it, the Skin row exists, the colour rows exist (today's "settings: the Mount, Skin, babies and effect rows"
  builds its rows after the registrations, so it cannot see this).
- Until then the docs say what the MountLooks comment says: register in Awake, and a mod that must be first gives
  its folder a name that sorts before `GK2SaddleUp` (ugly; the fix above is the real answer).

## 5. The tooling: paths that know only the API's folder

Every script and scan assumes one home for every skin (the 2026-10-20 state; section 10 says what was built, and
since 2026-10-24 every tool walks `src/*/assets/mounts/<mount>/` through `tools/skins/skinroots.py`: `roots()`,
`mount_dirs()`, `skin_dirs()` = `<mount>/skins/<skin>`, `find_mount(id)`; `canonical_cfg.py` writes and checks both
`skin.cfg` and `mount.cfg`; `new_skin.py <id> [--mount <mount>]` makes the mount folder and its `mount.cfg` for a new
mount; `release-scans.sh` checks `src/*/assets/mounts/*/skins/*/`):

| Tool | Hard-coded today | For a mount in its own mod |
|---|---|---|
| `tests/SkinsScan/Program.cs:10` | `src/GK2DonkeyAPI/assets/skins` | a folder argument (or a list of roots) |
| `tools/skins/canonical_cfg.py:16`, `skin_colors.py:15`, `new_skin.py:18,106`, `skin_preview_html.py:353` | the same path | the same |
| `tools/release-scans.sh:36` | the `check_skin` loop over that folder | a loop per skin root |
| `tools/skins/creatures/make_creatures.py:829` | the path is already `argv[2]` | fine |
| `tools/skins/check_skin.py` | takes a folder | fine |
| `tools/skins/creatures/colors.json` | one block per built-in creature in one file | the block moves with the creature, or the file stays here as the generator's own |
| `tools/skins/hellfire/make_hellfire.py:28`, `royal-parade/make_royal.py:30` | their own folders under the API | the donkey's two skins stay in the API (section 7) |

A skin root argument on five scripts and the two scans is a mechanical change; `new_skin.py` would take `--mod
<folder>` to scaffold a mount mod (csproj from the example, the embed line, the Awake) instead of a built-in.

## 6. The tests: sixteen scenarios name a built-in

Sixteen of the 61 self-tests name a built-in mount or skin (`rat`, `snail`, `worm`, `dragon`, `gerry`, `hellfire`,
`royal-parade`): the seat and cart readouts over every mount, the Coat and Second tone audits, the preview gap
(worm, dragon), the front layer (dragon), the over skins (hellfire, royal-parade, snail), the game creature (gerry),
the skin effect (hellfire, royal-parade, rat), the kept-with-save round trip (`Skin = dragon`), the settings rows
(dragon), the colours' regions (dragon, rat, worm), on/off and leak tests (rat, snail), the mounts-off check. Each
already guards with `Looks.Find(id) == null` and skips or warns, so a creature that leaves the DLL does not fail the
suite, it hollows it: the dragon's front layer, the cart readout's six lines, the Coat audit would run on nothing. If
the creatures become separate mods, those tests follow them in one of two ways: the dev build deploys the mount
mods next to the API (`-p:Deploy=true` as the example mod does) so the suite finds them, or the scenarios take their
subjects from `Mounts.Catalog.All` instead of a fixed list (the readouts already loop; the targeted ones, the dragon's
wing and Gerry's frames, need the creature present). The first keeps every assert as it is.

## 7. If the creatures leave the API: what moves and what it costs

The API cannot carry a built-in `dragon` *and* a mod ship one: a second skin with the same id is refused at start
(the second registration by another owner, Looks.cs ~270-292; a built-in mount cannot be redeclared, docs/mounts-design.md
2.1). So "Saddle Up Dragon as a separate download" means the dragon leaves `assets/skins/`. The moving parts, counted
(the 2026-10-20 plan, with that day's paths; what was done is section 10, and the folders are
`assets/mounts/<mount>/` since 2026-10-24):

- **The folders**: `assets/skins/{dragon,rat,snail,worm,gerry}` (the five creatures: 86, 45, 45, 45 files and
  Gerry's one cfg) into `mods/<Name>/assets/skins/<id>/` with a csproj each (or one `GK2SaddleUpCreatures` carrying all five, one
  download, which is the simpler first step). The csproj embed line (`GK2DonkeyAPI.csproj:31`) stays for the donkey's
  own skins.
- **The donkey's two skins stay**: Hellfire and Royal Parade are skins of the donkey (`mount = donkey`), and Hellfire
  is the worked example of erase and `[Base.*]`; they are what a player with "only the API and Saddle Up" gets.
  Gerry is a cfg alone (no art) and could stay as the game-creature example; it is listed above because it is a mount.
- **The tooling of section 5** (five scripts, two scans, the loop in `release-scans.sh`): a root per mod.
- **The tests of section 6**: deploy the mods for the suite, or loop over the installed mounts.
- **The release**: `tools/build-dist.sh`, `check-dist.py`, `make-release-zip.py` build two DLLs (`FILES` in
  make-release-zip.py:21); each mount mod needs its own zip (`GK2-Saddle-Up-Dragon-<version>.zip`) and a Nexus page,
  with the API's minimum version in its README. `docs/release-checklist.md` gets the mount mods' steps.
- **Texts**: Saddle Up's descriptions name the dragon and the rat as examples ("A donkey, or any mount a skin pack or
  another mod adds (dragons, a giant rat...)", Plugin.cs:42, 72, 92-102), the `Debug.MountsPreview` description
  (Plugin.cs:638), the CHANGELOG's player block, the READMEs' mount tables: wording, 21 lines in 20 files, none of
  them code.
- **Compatibility**: nothing a player has is touched. No released version has the creatures on (the gate), so no
  player's cfg or side file holds `Skin = dragon` yet; the fixtures in `tests/fixtures/released/` are donkey-only.
  The decision costs nothing now and something after the next release.

What does not change: `Looks.cs`, `Catalog.cs`, the merge of a skin's geometry with the mount's facts, the front
layer, the colours by region, the cart rule, the babies, the paper mount, Persistence. The runtime is the same code
for a built-in and a mod; that was the point of the parity work.

## 8. The preconditions and the open questions

1. **The gate.** `Debug.MountsPreview` off (every player today) keeps out every registration from any mod
   (`Mounts.KeptOut` in `Register`, `RegisterFolder`, `RegisterEmbedded`, `Catalog.Register`, `Effects.Register`; the
   self-test "mounts off" proves it). A separate mount mod is inert for players until the release that flips the
   gate. The flip is already on the list (docs/release-checklist.md section 1, last item).
2. **The load order** (section 4): fix before the first DLL mount ships, or ship the first as a loose pack (a).
3. **The missing-skin warning** (section 3): Info once mounts are removable.
4. **The API's minimum**: a mount mod's `BepInDependency` names the API version its file needs. Unknown keys and
   sections are ignored with a log line (docs/skins.md: "a newer pack still loads in an older mod"), so an old API
   draws a new dragon without its front layer or its `[Tuning.Cart]` rather than refusing it; the dependency line
   is what tells the player. The format has no version of its own beyond `format = 1`; a mount mod pins the API.
5. **One download or one per creature.** One `GK2SaddleUpCreatures` (rat, snail, worm, dragon, Gerry) is one zip, one
   csproj, one test deploy and one Nexus page; five mods are five of each. The runtime is indifferent. A first
   release as one, split later if a creature grows abilities of its own, loses nothing (ids never change).
6. **Where the mods live.** This repo (`mods/<Name>/`, sharing `DeployToGame.targets`, the scans, the generators and
   the self-tests' deploy) keeps the pipeline in one place; separate repos would copy the tools. The memory rule
   "each feature its own plugin" says plugin, not repo.

## 9. Recommendation

Keep the runtime as it is: it needs nothing. Before the first separate mount mod: fix the load order in the API
(section 4, with its self-test), turn the missing-skin warning into an Info, give the tooling a skin root (section
5), and scaffold a mount mod from the example (`new_skin.py --mod`). Then move the five creatures out as one
`GK2SaddleUpCreatures` plugin (embedded packs, no code), deployed by the dev build so the sixteen scenarios keep
their subjects, released as its own zip next to Saddle Up, and split a creature off into its own mod the day it
gets code of its own. Hellfire and Royal Parade stay in the API as the donkey's skins and the pack format's examples.

## 10. Done (2026-10-20 evening)

Built the same evening, in the order of section 9 (the user: "ok lets begin"), with two corrections from the user on
the way: the words are **mounts and skins**, never "creatures"; and the goal is **someone else writing a mount that
works with Saddle Up and the API**, not our five leaving the download. So the five mounts moved out of the API,
but into Saddle Up (`src/SaddleUp/assets/skins/<id>/` then, `src/SaddleUp/assets/mounts/<mount>/` since
2026-10-24): the API is the backend, the player mod ships the content,
and Saddle Up is now the worked example of what a third party does. (A third plugin held them for an hour in
between; gone.)

- **The load order** (section 4): `MountSettingsPage.Publish` queues the hand-over for the next frame
  (`Plugin.NextFrame`, drained at the top of the API's LateUpdate, so a stopped API still shows the page); `Show`
  runs `CatchUp` first, which adds a Skin row and every `PerMount` builder's rows for each mount with a skin that
  has none yet, then the menus read every list. No event: by that frame every plugin's Awake has run. The self-test
  "settings: the Mount, Skin, babies and effect rows" registers a pack declaring a mount after the rows and checks
  the catch-up, and checks every page shown this session for a Skin row per mount of a mod or pack. The log's
  "got their rows before the page was shown" line says when a start exercised the path.
- **The Info line** (section 3): a set skin or effect that isn't installed logs an Info ("its mod or pack isn't
  loaded; the choice is kept and comes back with it").
- **The tooling** (section 5): `tools/skins/skinroots.py`; every tool and scan walks `src/*/assets/skins` (since
  2026-10-24 `src/*/assets/mounts/<mount>/` and its `skins/<skin>/`); `new_skin.py --mod <project>` (a mount of its
  own goes to Saddle Up by default); `make_creatures.py` writes into Saddle Up's folder by default; `release-scans.sh`
  checks every root.
- **The move** (section 7, into Saddle Up): `SaddleUp.csproj` embeds `assets\skins\**` as `GK2SaddleUp.skins.<dir>\<file>`
  and records `GK2SkinsSourceDir` in a dev build (since 2026-10-24: `assets\mounts\**` as
  `GK2SaddleUp.mounts.<mount>\mount.cfg`, `GK2SaddleUp.mounts.<mount>\skins\<skin>\<file>`, and
  `GK2MountsSourceDir` = `assets\mounts`; the Mounts tab saves into the repo as before: `Looks.CfgPathFor` reads it
  off the skin's `SourceAssembly`, and a mount's rows go to its `mount.cfg`); Saddle Up's Awake calls
  `Mounts.Looks.RegisterEmbedded` (prefix `GK2SaddleUp.mounts.`) before it builds its page; Saddle Up needs API
  0.34.0. The API keeps Hellfire and Royal Parade. The Gerry test expects
  its mount from a pack, not built into the API; the readout and Coat scenarios say "every shipped mount"; the
  temp packs' sample picture is Royal Parade's.
- **One rule that changed with the move** (before the mount folders, 2026-10-24): the API's own folder scan runs in
  the API's Awake, before Saddle Up's, so a loose `gk2skins\dragon` pack took the id first and Saddle Up's embedded
  dragon was refused with the loader's "already registered" warning (before, the built-in won); and a loose pack
  with a `[Mount] id = dragon` block declared the Dragon's facts first, Saddle Up's dragon joining it (first
  declaration wins). Since 2026-10-24 the version rules decide instead of the order: the same skin id from two
  places, the higher `version` wins and a tie goes to the DLL; two `mount.cfg` of one mount id, the higher `[Mount]
  version` wins (its facts replace the other's in place, the other's skins keep the mount), a tie goes to the DLL
  over a loose folder, otherwise the first stays and the later one is refused with a warning. A pack author who wants
  a second dragon look puts it in `gk2mounts\dragon\skins\<another id>\` with no `mount.cfg`.
- **What a third party does now**, in full: a folder `gk2mounts\<mount>\` with `mount.cfg` and `skins\<skin>\`
  (`skin.cfg` and the pictures), dropped under `plugins\` (no code), or the same folder embedded in a DLL with the
  two lines Saddle Up uses (the csproj's `EmbeddedResource` and `RegisterEmbedded` in Awake) and a `BepInDependency`
  on the API 0.34.0 or newer. (Until 2026-10-24: a folder `gk2skins\<id>\` with `skin.cfg` and its `[Mount]`
  block.) Either way the mount is in Saddle Up's Mount row with its own Skin and colour rows, its cart, babies and
  save-keeping handled by the API, whatever order the mods loaded in; removing the mod leaves the player a donkey
  and an Info line, and the choice returns with the mod. The pack format: docs/skins.md.
- **Not done**: the gate stays (the next release flips it); `dist/` is not rebuilt (the release step); a bare
  example mod under `examples/` with a placeholder pack, should Saddle Up's own lines not be example enough.
