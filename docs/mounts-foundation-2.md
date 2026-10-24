# Mounts and skins from many hands: the foundation, second look (2026-10-20, night)

**Amended 2026-10-24:** the packs are sorted by mount (`gk2mounts\<mount>\` with `mount.cfg`, its skins under
`skins\`), and three id rules hold: a mount's id is `<mod>.<name>` (`saddleup.dragon`; only the game's `donkey` is
plain; one without a dot is refused); a skin is named within its mount, full name `<mount id>/<skin>`
(`saddleup.dragon/default`); every mount but the donkey has exactly one default look, its `skins\default\`. The
`gk2skins` folders, `[Mount]` blocks in skin.cfg and the author-prefix Info below are history; docs/skins.md and
docs/mount-id-collisions.md are current.

The user, after the five mounts moved into Saddle Up and the API took a mount from any mod's DLL: *"any side effects or
issues this can cause in the future once we start adding mounts and skins, all with different abilities and speed
values and everything customized?"*, then *"lets investigate this properly so our foundation is solid"*, and: the
mount selection should become **a panel of its own, WoW-style** (the mounts listed on the left, the chosen one drawn
on the right with its stats and abilities), offered by the API. This is that investigation: three read-throughs of
the code (the tuning layers and abilities; what is still the donkey's; game-source skins, sheet memory, the pack
format and the registration order), every claim with its file and line, the four sharpest checked a second time by
hand. Nothing was changed while writing it but one line (section 7, a regression of the same evening). Line numbers
are of 2026-10-20 night: `Looks.Rig.ApplyTuning` is at Looks.cs:2087-2166 now, `LayeredValue` 1186, `TickAbilities`
2171, `MountNow` 1879. Tags as in docs/mounts-parity-matrix.md: **fix** (no decision needed), **decide** (two
defensible ways), **by design**.

## 1. In one paragraph

The runtime is sound for what ships today: no code names a mount, the merge of a skin's geometry with its mount's
facts is one path for every mount, and a mount from another DLL is drawn, seated, coloured, hitched and kept with the
save like our own. What is not ready for many hands is **the rules between hands**: who owns a value on an instance
(nobody: the last writer wins, silently), who owns an id and a mount's facts (whoever comes first, and a loose pack
comes before every mod), what a mount may pin of the player's own settings (anything but a look key, and the page
cannot tell), how the settings page scales (22 cfg keys per mount), and how much memory skins may take (no cap,
sleeping instances keep theirs). Everything else is a list of small fixes. The panel the user wants is the right
answer to the page's growth and belongs in the API; what it needs from the format is a description, a portrait and
a name and description per ability, none of which exist yet.

## 2. The five findings that change the design

### 2.1 Ownership of a value is a convention, not a rule (**fix**)

Facts:
- `InstanceTuning.Set` (Api/MountInstances.cs:476-488) writes `Overrides[key]` and `OverrideOwners[key]` with no
  check on the previous owner and no log: owner B replaces owner A's value and takes the key. `Clear(key)` (490-498)
  takes no owner: any caller removes any owner's value, a mount's included. The session layer (`MountTuning.Set`,
  Api/MountTuning.cs:99-109; `Tuned.SetOverride`, Tuned.cs:95-106) is the same. Only `ClearOwner` (512-528) is
  owner-scoped, and it runs by itself only for owners `KeyActions` tracks, when they unload (KeyActions.cs:114-127).
- Callers check `SetBy` before clearing **by convention**: the companion (Api/MountCompanion.cs:717, 738, 875), the
  example ability (examples/GK2DonkeySkins/Plugin.cs:112-113). The API enforces nothing.
- The skin whitelist (`SkinTuningKeys`, Looks.cs:1173-1177) and the look-key refusal (Looks.cs:2139-2142) exist only
  in the mount's merge (`ApplyTuning`). `InstanceTuning.Set` applies neither, so an ability or any mod can set
  `Effect`, `Skin`, the colours, `SpeedMultiplier`, anything.
- A mount's `[Mount.Tuning]` has no whitelist beyond the look keys (FromCfg stores it raw, Looks.cs:518-520, 597; the
  merge filters `IsLook` only, 2139). A mount may pin speed, every sound key, the behaviour keys, the cart keys.
- The instance layer is read before the session layer (`Tuned<T>.Value`, Tuned.cs:51-67: the instance override at
  56, the session override at 64). Saddle Up writes the player's speed, sounds, bell, lantern, cart and behaviour
  rows as **session** overrides (`ApplyRide`, MountCompanion.cs:677-696; the table in the tuning report), and its
  Effect, Skin and colours as **instance** overrides under its GUID (MountCompanion.cs:849-885). So a mount's
  `[Mount.Tuning] SpeedMultiplier = 2` beats the player's Speed row, and the page shows nothing.
- The Mods menu and the game's Settings window have no way to show a pinned row: nothing in MountSettingsPage.cs,
  SettingsRows.cs, LookBar.cs or FrameworkLink.cs reads `SetBy` or `IsSet`. The F11 panel does (orange for the
  mount, cyan for a mod, locked: Dev/TuningWindow.cs:24-39, 2015-2034; TuningWindow.Instances.cs:36-61).
- The live case: the example ability `BurnsWhenRidden` sets `Effect` and `EffectWhen` on Mounted under its own GUID,
  replacing the companion's (the player's pick) silently; on Dismounted it clears them because it now owns them, and
  the player's Effect is gone until the companion's `ApplyLook` runs again (on a property change, spawn, hitch,
  unhitch, load: MountCompanion.cs:159-639). A hitch while ridden re-applies the player's over the ability's.
- Persistence saves every override but the mount's (`Looks.TuningOwner`, Persistence.cs:276-291) and restores them
  under their saved owner GUID with no check that the owner is loaded (761-775). A save taken mid-ride on a burning
  Checker Beast keeps `Effect = hellfire` under the skins mod's GUID; after a load the mount stands and burns, nothing
  clears it until the next ride ends, and if that mod is gone, nothing ever does.
- "Left to it, logged once" (Looks.cs:2144-2147) is once per session per (skin id, key), not per instance or owner
  (`tuningLogged` static, 2275).

What this needs, as one rule in the API, not three conventions: **a value on an instance belongs to the owner that
set it**; `Set` by another owner is refused with a result and a log line (or, for the one case that wants it, an
explicit `Take`); `Clear` takes the owner and clears only its own; when a mount leaves an instance (a skin change,
`Detached`), the API clears the mount's abilities' owner too, not only `TuningOwner`; what a mount may pin is a list
(the mount keys: speed, sounds, the rider-motion scale, the cart's hitch gap; never the player's behaviour switches),
the same way the skin's is; the page can ask the API who pinned a key and grey the row with "the dragon sets this",
the way the F11 panel does; a restored override whose owner is not loaded is set aside with the record's other lines
(dormant), not applied. The self-tests for each are one scenario.

### 2.2 Ids and a mount's facts go to whoever comes first (**fix**, one part **decide**)

Facts:
- A skin id registered by another owner is refused (Looks.cs:271-274); the same owner replaces silently and logs
  "registered", never "replaced" (289-294). **Every loose pack shares one owner** (`<api>.packs`, Looks.cs:125), so two
  loose folders with the same id replace each other silently, in the unsorted order `Directory.GetDirectories` returns
  (362, 370).
- A mount's facts are the **first** `[Mount]` declaration's (Catalog.Declare, Catalog.cs:71-86): a later one with different
  facts "joins" with an Info line and its facts and `[Mount.Tuning]` are dropped unlisted. A code `Register` of an id
  a pack declared throws for any other owner (Catalog.cs:63): no mod can take over a mount a loose pack declared.
- The loose-pack scan runs in the API's Awake (Plugin.cs:649-651), before every mod's Awake. So a loose
  `gk2skins\dragon\` takes the skin id before Saddle Up registers, and Saddle Up's dragon is refused with a warning;
  a loose pack with `[Mount] id = dragon, cart = false` declares the Dragon's facts, and Saddle Up's dragon joins it.
  Before the move the built-in won; this changed tonight.

What this needs: **ids carry an author prefix by convention** (`dreas.dragon`, or the mod's GUID tail), checked by
the scan for our own and said in docs/skins.md for others; **the declaration of a mount belongs to the skin whose id
equals the mount id** (its Default; a pack without such a skin declares only when nobody has), whichever registers
first, so a later Default re-declares over an earlier joiner and logs it; **one owner per loose pack folder**
(`<api>.packs.<folder>`), the scan sorted by name; a refused embedded skin names the folder that took the id.
**Decide**: whether a mod's registration at its Awake should evict a loose pack's skin of the same id (the mod is the
author's, the folder a player's copy; eviction is what a player who installed both would expect) or the folder keeps
it and the log says which to delete.

### 2.3 The settings page grows with every mount: the Mounts panel (**decide**, the user's proposal)

Measured on the live Saddle Up cfg: 182 keys, 144 of them in Colours; 22 keys per mount (the Skin row, seven colour
presets with their custom colour and its lightness) and six mounts today. The Mods menu holds 170 rows, hidden by
visibility per mount (FrameworkLink.cs:193). Thirty mounts would be about 700 rows and 660 cfg keys; the lists are
read once per session and the hidden rows cost only at start, but the page, the cfg and the menu's own list grow
with every mount a player installs, and the game's own Settings window pages through them ten at a time.

The user's answer, and the right one: a **Mounts panel** in the API, opened by a player mod with one call (as
`LookButton` opens the colours bar), the mounts listed on the left from the registry, the chosen one drawn on the
right by the preview stage (which already draws any mount with or without its cart into a texture,
PreviewStage.cs), its skins, stats and abilities beside it, the colour rows for that one mount below. The selection
and the colours write the same cfg entries the page uses today (`Mount`, `Skin_<id>`, `Coat_<id>`...), so a player's
choices and the side file do not change; once the panel exists the Mount and Skin rows and the per-mount colour rows
leave the page, which keeps the plain rows and a button, and stops growing. Section 3 has what exists to build it
from and what the format lacks.

### 2.4 Every mount rides the donkey's skeleton (**by design** for drawn creatures; one leak to close, **fix**)

Fixed to the donkey today (the base report, compressed): the 44 frame keys, left-facing and mirrored, the game's
16-clip animator, no frame or clip a skin could add (Looks.cs:80-101, 1520-1533, one `SkinBase`, 1860); the walk-clip
rate 1.5 × the keeper's speed multiplier (Mount.cs:2492, 1289, 2247; MountInstance.cs:585) and the shuffle rate
(IdleShuffle.cs:819-821); the eight-frame back table that drives the rider's bob, hands, head, the automatic seat and
the bob added to a declared seat row (RiderMotion.cs:42-88, 152-162; Mount.cs:2871, 2957-2959, 3068); when sounds
fire (hoofsteps four per walk cycle, graze on the clip, the lips smack from the game's clip event; sounds only while
ridden: MountSounds.cs:150-210, 575-593; Mount.cs:538); every footprint, gap, reach and probe number (SafeGround.cs:
338-350, 219, 487-491; IdleShuffle.cs:57, 69, 186, 527; InstanceControls.cs:27-28); the shadow caster and grass bend
(MountInstance.cs:284); 50 pixels per unit; no scale per mount (a scaled instance can neither be ridden nor hitched,
MountRegistry.cs:812-821). Already the skin's or the mount's: the pictures, canvas, pivot, offsets, front layer, a
declared seat and the legs with it, the geometry keys, the effect ring's width, and through `[Mount.Tuning]` speed,
every sound's id, volume, pitch and phase, and the six rider-motion keys (scaled or zeroed, never replaced).

Said plainly: a mount with a different gait or a different number of legs looks wrong in ways no cfg key reaches,
and a flying or galloping mount needs a second base (its own keys and clips), which is a design project, not a fix.
The one leak to close now: **a fast mount's legs run at the donkey's stride**, since the clip rate follows the speed
through a constant of 1.5 nobody can set. A mount key `WalkCycleSpeed` (the clip rate per unit of speed, the donkey's
1.5) under `[Mount.Tuning]` is the one-line version; the sounds' `HoofStepsPerCycle` already exists for the feet.

### 2.5 Sheets have no cap and sleeping instances keep theirs (**fix**)

Facts: one RGBA32 sheet per loaded skin, 1.8 MB at 96 px, 3.1 MB at 128, double with a front layer, 49 MB at the
allowed 512 (Looks.cs:1384-1401, 571; the only guard is `maxTextureSize`); refcounted and freed when the last wearer
lets go (1554-1562, 1761-1780); **no cap** on loaded sheets; an instance that goes to sleep detaches its overlays
but keeps its refs (MountInstance.cs:395), so a far-away instance keeps its sheet for as long as it exists; a sheet
whose only wearers are asleep **stops building** (builds advance only in an attached rig's tick, Looks.cs:1998-1999)
and holds its refs until some rig ticks; during a reload two sheets of one skin exist at once (1556-1562).

What this needs: release the sheet on sleep and re-acquire on wake (the wake already rebuilds the view); builds
advance from the API's tick, not a rig's; a canvas cap below 512 (256 is four times the dragon's area) or a warning
in the scan; `LoadedCount` and the bytes in the F11 panel's memory line.

## 3. The Mounts panel: what exists, what is missing

To build from, all in the API already:
- **The picture**: `PreviewStage` draws a copy of any mount, with its cart or alone, in its colours and lantern, into
  a RenderTexture, framed on what is drawn (PreviewStage.cs:1-30); `LookPreview` places it in the menus; the paper
  mount follows the companion's look at once (the gap fix of this afternoon).
- **The window parts**: `LookBar` builds a window over the game out of the game's own Settings rows and dialog
  buttons, sorted above the HUD, scaled to fit, nothing moving (LookBar.cs:1-40); `SettingsRows` copies the game's
  row prefab with its look, sounds and gamepad navigation (SettingsRows.cs:1-40). The game has list-plus-detail
  windows of its own to copy from: `UICraftSelectionWindow` (a list of crafts with a chosen one previewed),
  `UITutorialListWindow` (a list with content beside it), `UIAlchemyFolioWindow` (tabs), with `UICraftItemCell`,
  `UITutorialListItemWidget` and `UIFolioWindowTab` as the cells (61 `UI*Window` classes in the game's assembly;
  which parts copy cleanly is a probe in the game, the way the colours bar's rows were found, not a claim here).
- **The list**: `Mounts.Catalog.All` (donkey first, then by name), `MountInfo` with name, noun, author, version,
  cart, foals and their size, effect, the tuning dictionaries (speed is `Tuning["SpeedMultiplier"]` when set), its
  skins Default first; `Mounts.Looks.Find(id)` for a skin's regions, canvas, front layer, origin.
- **The selection**: `MountCompanion.Skin`, `LookOf(mountId)` with every colour preset; the cfg keys stay.

Missing, and the API change the panel needs (**fix**): a `description` (and a `portrait` or `icon` picture) on a
skin and on a mount, in the pack (`[Skin]`, `[Mount]`) and on `MountSkinDef` / `MountDef`; a **name and a
description per ability** (`MountAbility` has an `Id` and hooks only, Api/MountCatalog.cs:148-179) and, if the panel is
to show "stats", a small list of lines a mount declares (`[Mount.Stats]`, free text per line, or the known keys
rendered: speed, cart, babies, effect); a `Mounts.MountsPanel(owner, companion)` or a page row mount that opens it.
**Decide**: whether the panel is also where a player picks the mount (the WoW model: the panel is the mount list,
the page keeps keys and behaviour) or only shows and colours it while the page keeps the Mount row. The user's
proposal is the first; it is the one that removes the per-mount rows from the page.

## 4. The smaller fixes

| # | Finding | Where | Tag |
|---|---|---|---|
| 4.1 | A game-source skin that finds no frames is drawn **invisible** (44 transparent cells, the donkey's parts hidden), while the warning says "the donkey shows". | LooksGame.cs:51-54, 70; Looks.cs:2427 | **fix**: show the donkey (refuse the sheet) and say so |
| 4.2 | Frames are searched once, at first wear, among loaded objects only; a partial set in memory blocks the atlas fallback; no retry. | LooksGame.cs:105-147 | **fix**: the atlas when the memory set is short of the clip count; a retry on the next wear |
| 4.3 | The atlas fallback copies every sprite of every loaded atlas in one tick and destroys the copies. | LooksGame.cs:126-143 | **fix**: by name through `GetSprite`, no `GetSprites(all)` |
| 4.4 | Sprite prefixes, the atlas name and frame names are outside the dependency check. | src/Shared/DependencyManifest.cs:38; game-code.txt | **decide**: a sprite manifest for game-source skins (names checked at start, the skin set aside when they changed), or the run-time warning as today |
| 4.5 | `format` that is not a number reads as 0 and is accepted; no key says which API a pack needs; a format 2 pack is refused whole. | Looks.cs:527, 564 | **fix**: a non-number refused; `needs = 0.34` honoured with "needs GK2 Donkey API x" when short |
| 4.6 | Unknown `[Game]` keys become frame-map entries and are never reported. | Looks.cs:639 | **fix**: report like every other section |
| 4.7 | A reload whose `[Mount] id` changed leaves the old mount registered; `Declare` on reload joins instead of re-declaring when the owner differs. | Looks.cs:1055-1066 | **fix** with 2.2 |
| 4.8 | The panel's save writes the mount's rows into every file of that mount that holds `[Mount]`; it does not update `DiskStamp`, so auto-reload reads the save back and rebuilds the sheet. | Dev/TuningWindow.Mounts.cs:641-648; Looks.cs:1005-1007, 1098-1118 | **fix**: stamp after save; write the Default's file only (2.2's rule) |
| 4.9 | Anti-aliased edges and front-layer colours count toward the 48-colour limit that switches a skin to the cube; on the cube, two colours in one cell share a target, and an over skin's unnamed colours (kept as drawn) shift by up to a cell (the identity cell holds the rounded value: Colors.cs:1107-1128, checked). | Looks.cs:1425-1428; Colors.cs:1143, 1096-1099 | **fix**: the scan says which path a skin takes and why; a pack author sees "173 colours: the cube" before the game does (the snail is on it today) |
| 4.10 | The per-frame apply writes the property block every frame when the part has one, and runs for Idle views still in `attached`. | Looks.cs:2457-2462, 1835-1846 | **fix**: skip Idle rigs; compare before writing |
| 4.11 | `MountNow` is evaluated twice per tick (a chain of null-coalescing reads, the mount cached per version): cheap, but one read and a pass would do. | Looks.cs:2000, 2175 | **by design** (noted) |
| 4.12 | The panel writes overrides directly under `PanelOwner`; the cart-settings move drops the target's cart keys whatever their owner; `Pose` is dropped whoever set it. | Dev/TuningWindow.Instances.cs:95-108; MountInstance.cs:930-954, 577-581 | **fix** with 2.1 (the same owner rule) |
| 4.13 | An ability that sets keys from `Tick` pays a full re-merge and a looks refresh every frame (`Changed()`). | Api/MountInstances.cs:530-547 | **fix**: a `Set` that changes nothing is a no-op; said in the ability docs |
| 4.14 | `LooksRig.Tick` has no `Perf.Measure` of its own (its neighbours have one). | MountInstance.cs:182 | **fix** |
| 4.15 | A loose pack's `[Mount.Tuning]` is ignored when it joins an existing mount, with no line saying so. | Catalog.cs:76-78 | **fix** with 2.2 |

## 5. What is already right (so it stays)

The merge order of a skin's geometry over a mount's facts (Looks.cs:2104-2123) and that geometry follows the worn
drawing while facts follow the wanted skin (1879); the skin whitelist; the look-key refusal for mounts; the mount's
effect as a declared default under "As the mount or skin"; one mount's abilities per instance with a clean
Detached/Attached cycle on a skin change (2176-2187) and a throwing ability switched off with one warning; the cost
of abilities scaling with instances in view, never with the number of mounts (2199-2213); the refcounted sheets; the
cart rule and the babies' size per mount; the page's catch-up for late mounts (this evening).

## 6. Decisions, taken (the user, 2026-10-20 night)

1. **2.2, a folder and a DLL with one skin id**: **the higher `version` wins, a tie goes to the DLL**, the log names
   the loser and its folder. The cases this covers, found by the investigation: two copies of one thing (an author's
   folder release and the DLL that followed it, or a player who added the DLL without deleting the folder: the
   stale copy loses at the next update); a player's own edited copy of a shipped skin (wins by bumping its version,
   which is also our drawing loop's case); two authors picking the same plain name is the prefix convention's, not
   this rule's. The `[Mount]` hijack is closed apart, by the Default skin owning the declaration.
2. **3, the panel**: **the panel is the mount list**; the settings page loses its Mount, Skin and per-mount colour
   rows and keeps keys, behaviour, cart, sound and a button to the panel; the cfg keys stay as they are.
3. **4.4, game-source skins**: **a sprite manifest checked at start**, one more scope of the dependency check: the
   skin lists the frame names it needs, a missing one sets the skin aside with one line.
4. **Order**: **the ownership rule (2.1) and the id rule (2.2) before the release that flips the gate**.

Everything tagged **fix** needs no decision. The build order that follows from this: 2.1 and 2.2 with their
self-tests (the rules, before anything relies on the loose behaviour), the format additions the panel needs (3:
description, portrait, ability names, stats lines), the panel itself, then 2.4's walk-cycle key, 2.5's sheet
release, the sprite manifest, and the table of section 4.

## 7. Changed while writing

One line, a regression of this evening: `ReloadCfg` copied `BuiltIn`, `Source` and `DiskStamp` onto the fresh def
but not `SourceAssembly`, so after one reload Saddle Up's mounts (embedded skins) had no file to save to or reload
from (Looks.cs, ReloadCfg: `SourceAssembly` kept). It copies it now; built and deployed; to see in the Mounts tab: reload a mount's skin
twice, the save path stays.

## 8. Built (2026-10-20 night; the user: "lets begin and do all of them so that our base will be solid")

| Finding | Outcome |
|---|---|
| 2.1 ownership | **done**: `Set` refused for another owner's key (the mount's stays replaceable), `TrySet`, `Clear(owner, key)` on both layers; what a mount may pin is a rule (`Looks.Rig.MountMayPin`); `MountAbility.Owner` given by the API and cleared when the mount leaves; `TuningKey` / `PinnedBy` on the page, Saddle Up's speed, bray and bell rows use it; restored overrides of an unloaded owner set aside (`AsideSets`); the example ability gallops; self-test "tuning: a value belongs to the owner that set it" |
| 2.2 ids | **done**: the Default re-declares over a joiner (`Entry.DeclaredBy`), a differing joiner says so, the version rule (`Looks.Outranks`, `CompareVersions`), one owner per folder in name order, the prefix note for other mods' ids, a reload withdraws a renamed mount, the Mounts tab saves the declaring skin's file and stamps it; self-test "ids: ..." |
| 2.3 / 3 the panel | **done**: `MountsPanel.cs` (the look bar's successor): the list (paged by ten), the chosen mount drawn, its description, what it does, its stats and abilities, its rows rebuilt per choice, Done and Cancel over every mount's rows; `PanelButton`, `OpenPanel`, `LookButton` kept; the look rows leave the Mods menu, the Settings window and the menu labels; Saddle Up: a Mounts button in the Mount section and a MountsKey (none by default); the format: description, portrait, `[Mount.Stats]`, ability `Name` / `Description`, `needs`, format strictness, `[Game]` unknown keys; descriptions on the seven cfgs; self-tests "format: ..." and the panel checks in "settings: ..." |
| 2.4 skeleton | **the leak closed**: `WalkCycleSpeed` (ride and cart, 1.5) at the five sites and the shuffle; the rest by design |
| 2.5 sheets | **done**: `Rig.Sleep` releases the sheet on the sleep tier (pass 2: not on every view rebuild; an unhitch keeps it), builds advance from the registry's tick, canvas at most 256; the F11 memory line was not built: the log's "unloaded (no donkey wears it)" and the sleep scenario stand in |
| 4.1-4.3 game source | **done** but 4.3 in part: no frames refuses the sheet (the donkey shows), memory and atlases merged for the needed names; the atlases are still listed whole (`GetSprites`, the names need cleaning), their copies destroyed at once |
| 4.4 sprite manifest | **done** (the decision): `Looks.VerifyGameSources` at game-ready checks every game-source skin's named frames; missing ones set the skin aside (`SkinDef.Unavailable`: not offered, the donkey shows for whoever had it) with one warning naming them |
| 4.5, 4.6 | **done**: `needs`, a non-number `format` refused, `[Game]` unknown keys reported |
| 4.7, 4.8, 4.15 | **done** with 2.2 |
| 4.9 | **done**: the scan says which colour path each skin takes (the snail and the worm: the cube) |
| 4.10 | **done**: Idle rigs skipped before the cameras; the property block write stays (its cost was not measured apart) |
| 4.11 | by design |
| 4.12 | with 2.1: the panel's direct writes stay dev-only |
| 4.13 | **done**: a Set of the same value by the same owner is a no-op |
| 4.14 | **done**: `Perf.S.LooksTick` |

Reviewed the same night by four independent readers, three times (CHANGELOG, Development: what each pass
found and what was fixed; the second pass read the first pass's fixes, the end-to-end cycles, every other
scenario and the docs; the third read the panel and the registry whole, not as a diff); left for later: the Mounts tab's save writes the live mount's `[Mount]` facts into a
joiner's own file (dev-only, no shipped joiner), and a code mount against a loose pack's mount of the same id
(docs/open-items.md).

Not seen in game yet: the panel's look (the list, the words, the rows), a mount's stride, a sleeping mount's
sheet freed (the log's "unloaded (no donkey wears it)" line; the sleep scenario), the pinned speed row left out for a mount that pins it. The F11 set
runs the rest. docs/open-items.md has the list.
