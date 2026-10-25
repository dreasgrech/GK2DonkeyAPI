# Mounts and skins: what is still not the same (2026-10-20)

**2026-10-25 (API 0.37.0):** items 2.13 / 9.5 (a skin that comes with an effect, "As the mount or skin") are undone:
the skin's and the mount's `effect =`, the Effect rows and the instance keys are gone, and nothing shows Hellfire's
fire until the skins' use of an effect is designed (docs/open-items.md "Effects on skins"). The ring's size by the
drawn width (2.4 / 9.3) stays in the engine.

The second investigation after docs/mounts-foundation-audit.md (the definitions) and the seat row
(docs/seat-investigation.md). The user: *"investigate to see exactly what is remaining to achieve what we want
regarding parity where all mounts are built the same as much as possible. Once we are 100% on everything, we go
into plan mode so that we finish all of them."* Two full read-throughs of the code, the tools, the tests and the
docs, checked against each other. The result: **no code anywhere branches on a mount's or a skin's id**; every
difference comes from a flag in a skin's file or from code that only knows the donkey. The second mount is what is
left, and it hits Gerry (160 px, the picture seat) and the dragon (128 px, the front layer) most.

Tags: **fix** (no decision needed), **decide** (two defensible ways), **by design** (a difference the design
wants; written down so it stops looking like a gap). Line numbers are of 2026-10-20 00:00.

## 1. Where a value lives

| # | Finding | Tag |
|---|---|---|
| 1.1 | The drawing-dependent keys sit in different places: the seat row on the skin (rat, worm, dragon), `RiderOffsetPx` on the snail's mount, `SeatAdjustPx` on Gerry's mount, `HitchGapPx` on the rat's and dragon's mount. docs/mounts-design.md §1 says "a value lives on the mount unless it exists only because of this skin's picture". For a one-skin mount both readings are true. | **decide**: the rule proposed in §9 |
| 1.2 | `HitchGapPx` is 0 on the snail, the worm and Gerry and 9 / 24 on the rat and the dragon; only the latter two are explained in their files. Nobody measured the three. | **fix**: measure (a cart readout like the seat one), then declare or explain |
| 1.3 | Gerry declares no `[Colors]` (everything Coat; its sockets could be Dark). Every other skin names its regions. | **decide**: every skin names its regions, even if only to say "all Coat" in a comment |
| 1.4 | The snail carried a stray `SeatAdjustPx = -7` from a panel test (committed by accident; it hid a pixel of the seat calibration). Gone. The Mounts tab's auto-save makes this easy to repeat. | **fix** (done) + the tab says what it saved in the log (done) |

## 2. Code that only knows the donkey

| # | Finding | Where | Tag |
|---|---|---|---|
| 2.1 | **The leg overlay ignores the skin's seat.** With `NearLeg` on, the rider's legs are drawn on the donkey's back line plus `LegAdjustYPx`; `TrySeatTop` is never asked, so on Gerry or the snail the legs land off the hip. Off by default, but the Mounts tab offers it per mount. | RiderLeg.cs:119-127, Mount.cs:2825-2870 | **fix**: the legs follow the seat the rider got |
| 2.2 | **The settings picture ignores `HitchGapPx`.** The paper donkey never gets the mount's tuning, so the dragon's and rat's cart picture shows the cart at the donkey's distance. The cart view is also shown for a no-cart mount. | Looks.cs:1955, Api/MountCompanion.cs:149 | **fix** |
| 2.3 | **Babies' size.** `Mounts.SpawnFoal` defaults to 0.6 whatever the mount; and with "babies are the same mount and skin" off the babies are plain donkeys but at the parent mount's `foal_scale` (Gerry's are 0.33 donkeys), while the row promises "plain baby donkeys". | Api/Mounts.cs:469, Api/MountCompanion.cs:533 vs 782 | **fix**: the mount's scale for its own babies, 0.6 for plain donkeys |
| 2.4 | **The effect ring is 0.8 for every canvas** (×1.6 with the cart, scaled for foals, never by the mount's size). On a 160 px Gerry the flames sit inside the skull. | Effects.cs:188-191 | **decide**: scale by canvas/96, or a per-mount `[Mount] effect_radius` |
| 2.5 | **The shadow caster and the grass bend stay donkey-shaped** on every mount (the rig overlays bdy/hed only). Inferred from the code, not seen in game. | Looks.cs:95, MountInstance.cs:284 | **fix after a look in game**: scale with the canvas, or declare |
| 2.6 | **Sounds are the donkey's on every mount**: bray `voice_donkey`, graze, mount/dismount `donkey_lips`, the lips smack fired by the donkey animator's clip event, hoofsteps timed on the donkey's walk clip. The sound ids are ordinary per-instance settings, so `[Mount.Tuning]` could already set another id or silence one; no mount does, and nothing documents it. | MountSounds.cs, Plugin.cs:226-251 | **decide**: phase 1 = mounts may set or silence the sound keys (data only, documented, the snail silenced); phase 2 = a sounds API with the game's other creature voices |
| 2.7 | **The cart rule has two holes**: `Mounts.Spawn(WithCart)` with a no-cart mount's skin is not refused, nor is a skin change on a hitched instance. | Api/Mounts.cs:415-460, MountRegistry.cs:777+ | **fix** |
| 2.8 | **`[Base.*]` and `[Colors]` are ignored silently** on a replace or palette skin (erase is reported). | Colors.cs:921-947, Looks.cs:1435 | **fix**: a report line |
| 2.9 | **Colour rows for regions a skin lacks**: the colours page builds the same rows for every mount, so the worm gets Harness and Eye rows that map nothing. Nothing tells a mod which regions a skin has. | Api/MountSettingsPage.cs:697-718 | **fix**: `MountSkinInfo.Regions`, rows greyed for a region the skin has none of |
| 2.10 | **Mount tuning switches late while a skin builds**: the cart rule, effect, babies and abilities follow the wanted skin at once, the seat and hitch values follow when the sheet is whole. | Looks.cs:1951 vs 1763 | **fix**: one source (the wanted skin's mount) |
| 2.11 | The rider's bob, hand sway and head lag are the donkey's walk table on every mount; a pack cannot supply its own. | RiderMotion.cs:61-162 | **by design** for now (the creatures are drawn to the donkey's walk); noted |
| 2.12 | The side file's record label and the dormant list always say "donkey". `Noun` says "a gerry". | Persistence.cs:402, TuningWindow.Instances.cs:469, MountInstance.cs:79 | **fix**: the mount's name; a `noun` key or "Gerry the skull" |
| 2.13 | "As the mount" with the Hellfire skin shows no Hellfire effect: a skin cannot come with an effect, only a mount can, and Hellfire's mount is the donkey. | Effects.cs, Looks.cs | **decide**: a skin-level `effect =` (a Hellfire skin that burns by default) or leave it to the player's pick |

## 3. The dev panel

| # | Finding | Tag |
|---|---|---|
| 3.1 | The Mounts tab shows the torso height (`SeatOffset`) and `SeatAdjustPx` rows for every mount, including skins with a seat row or picture where `SeatOffset` is dead; its tips and the note still say "seat = picture" only and "else nothing". | **fix** |
| 3.2 | The Riding tab shows AutoSeat and `SeatOffset` while riding a seat-row or picture skin (dead there) and hides `SeatAdjustPx` when AutoSeat is off, where it is live on those skins. | **fix** |
| 3.3 | The NearLeg row is offered per mount while 2.1 stands. | **fix with 2.1** |
| 3.4 | No row edits a skin's `seat =` or its `[Tuning]`; the tab says "edit in the file". Authors are pushed towards the mount for picture keys (1.1). | **decide with 1.1**: a seat-row slider on the skin's rows, saved by the same Save |
| 3.5 | Panel texts say "donkey", "Saddle Up: Donkey, Skin", "babies stay plain donkeys" in a few places. | **fix** |

## 4. The public API: a pack can, code cannot (and the reverse)

| # | Finding | Tag |
|---|---|---|
| 4.1 | `MountSkinDef` (code) has no erase, `[Base.*]`, `[Colors]` regions, `seat = picture`, `source = game` or sheet; a code skin with its own sprites collects no colours, so the player's colours do nothing on it. A pack has all of these. | **decide**: packs are the full format and code skins are for generated or dynamic pictures (write it down), or add `Regions`, `Erase`, `BaseRegions`, `SeatFromPicture` to the def |
| 4.2 | `RegisterRecipe` makes a donkey replace skin only (no mount, over, canvas, seat, front, tuning). No built-in uses a recipe. | **by design** (recipes are the legacy path), noted in api.md |
| 4.3 | `MountSkinInfo` does not say the seat mode, the front layer, the game source or the colour regions; `MountSkinInfo.Donkey.Mount` is null; `MountInfo` lacks `EffectWhen` and the tuning; `MountInfo.Skins` is in registry order, not Default first; `MountSpawn` picks a mount only through a skin id. | **fix**: the info records say what the cfg says |
| 4.4 | A code mount with no skins appears in the Mount list and its "Default" resolves to the donkey's own art. | **fix**: a mount with no skin is not offered |
| 4.5 | `MountSkinInfo.OwnColours`' comment says coat and harness colours don't apply, which contradicts the region system. | **fix** (comment) |

## 5. The pipeline and the tools

| # | Finding | Tag |
|---|---|---|
| 5.1 | The four creatures share one script with **no review outputs** (no `out\`, no strips, no preview, no sheet) and no by-hand loop; Hellfire and Royal Parade have strips and previews, Royal Parade alone has the Aseprite round trip. The pipeline doc says every generator has `out\` strips. | **fix**: the creature generator writes strips and a preview per creature like the other two |
| 5.2 | `make_hellfire.py` **rewrites skin.cfg on every run** (the pipeline rule says the cfg is by hand and kept). | **fix**: write the cfg once, keep it after |
| 5.3 | **The by-hand route is broken end to end**: the Aseprite export writes a sheet; the game reads a sheet only with `sheet =` in the cfg, which `out\pack\skin.cfg` lacks; no tool turns a sheet back into the per-frame PNGs the built-ins use; the watcher's previews ignore `front.png` and the rider. | **fix**: a sheet-to-frames step in the watcher (or `sheet =` written into the out cfg) and the front layer in its previews |
| 5.4 | `check_skin.py` does not handle `front/` (the dragon's 41 front pictures are never checked), nor `source = game` (Gerry reports "0 of 44 drawn"), needs the donkey's frames even for a replace skin, checks neither `[Mount]`, `seat` nor `[Base.*]`, and **is not in the release scans**. Its live findings that nothing gates: Hellfire 9 donkey colours and a lone pixel at walk_02 (45,56), the snail 1 donkey colour (3A3428). | **fix** |
| 5.5 | `skin_colors.py` holds the Dark/Muzzle/Eye rules for the four creatures as Python lambdas (a new creature means editing the script) and reads root PNGs only, not `front/`, while SkinsScan's colour reader includes `front/`. | **fix**: rules as data next to the generator, `front/` included |
| 5.6 | SkinsScan skips the picture and `[Colors]`-against-pixels checks for `sheet =` and `source = game` skins (no sheet-size or `sheet_front` check at all), does not check the `[Game]` map's target frame names, nor the seat row against the picture (opaque at that row at the pivot column), nor erase pixels in a replace skin. canonical_cfg's docstring says SkinsScan refuses a non-canonical file; only the release scan's `--check` does. | **fix** |
| 5.7 | The review page: the front layer is in the animated view only (the strips and the 44-frame view drop it); the rider overlay is a fixed picture at the stage centre that ignores `seat =`, `RiderOffsetPx` and the picture seat; nothing shows the seat row, the hitch gap, the babies or the regions; Gerry's card says "44 frames missing" even with the game export present; `MOUNT_NAMES` hard-codes gerry; the docstring still calls Hellfire a recipe at a ClaudeCodeTemp path. | **fix** |
| 5.8 | The in-game export covers only the skins loaded at that moment (a skin taken off is unloaded); the tooltip says "every skin worn since the start". | **fix** (wording, or keep the frames) |
| 5.9 | `gerry_probe.py` / `gerry_sheet.py` have Gerry's prefix and atlas as constants; the scale, lift, key map and seat were found by hand. Another game character means editing constants and writing the cfg by hand. | **decide**: a `make_game_creature.py <prefix>` that exports, sheets and proposes a cfg, or accept by-hand for the rare game creature |
| 5.10 | A new mount starts by copying an example; nothing scaffolds the folder, the canonical cfg and the generator stub. | **fix**: `tools/skins/new_skin.py <id> [--mount <name>]` |

## 6. What the self-tests never exercise

| # | Never tested in game | Tag |
|---|---|---|
| 6.1 | The dragon's front layer drawn in front of the rider (only a temp pack's `front/bdy.png` is checked for presence), the 128 canvas on a ride | **fix**: a readback with the rider |
| 6.2 | The hitch gap's geometry on the dragon (24) and the rat (9); the cart hitched to any creature or Gerry; the cart-style seat on any mount | **fix**: a cart readout per mount (also 1.2) |
| 6.3 | Babies of any creature (Gerry's 0.33 is only read as a value) | **fix** |
| 6.4 | The built-in Hellfire's erase marks and `[Base.Coat]`, either over built-in while walking or with a readback; Royal Parade's Harness colours; the snail's `RiderOffsetPx` (the readout reads height only) | **fix**: the over-skin scenarios run on the built-ins, not only temp packs |
| 6.5 | The GameCreature comment says "128 canvas"; the cfg is 160. | **fix** (comment) |

## 7. Docs that describe an older state

| # | Finding | Tag |
|---|---|---|
| 7.1 | docs/gerry-mount.md: "Nothing is built yet", 128 canvas, "seat lifted 10 px", babies 0.6, "not yet seen in game". | **fix** |
| 7.2 | docs/mount-colours.md §2.7 and §4: make_creatures generates the colours (skin_colors.py does), the dragon's Muzzle "the wings" (the belly), snail and worm "everything Coat", Gerry "Dark = sockets", "the six skin.cfg". | **fix** |
| 7.3 | docs/mount-looks-design.md has sections for the snail, worm and rat only; nothing on the dragon, Gerry or Royal Parade. | **fix** |
| 7.4 | README.md and src/GK2DonkeyAPI/README.md list no mounts or skins; src/SaddleUp/README.md lists the mounts but the skins as "Hellfire, skin packs" (no Royal Parade) and its Coat row says a mount with its own colours ignores it. | **fix** |
| 7.5 | CHANGELOG's player-facing next-update block: "a red and a blue dragon", Hellfire "drawn from the donkey's own frames the first time it is worn", no Royal Parade; `Debug.MountsPreview`'s description the same. | **fix** |
| 7.6 | docs/sprite-tools.md's pipeline claims `tools/skins/<id>/make_<id>.py` with `out\` for every skin (true for two), "kept by the generator" (Hellfire overwrites), check_skin for every skin (5.4), "the self-tests cover the over-skin drawing" (temp packs only), the composites to `test\skins\royal-parade` (gone). The csproj comment says every built-in is drawn by make_creatures. docs/mounts-foundation-audit.md §2 is a pre-change snapshot (marked). | **fix** |

## 8. Shipping

Every built-in is embedded, and `Debug.MountsPreview` (off) keeps them from players; the release scans run SkinsScan and
the canonical check, nothing else about skins. The next release flips the key and moves the CHANGELOG block up. Not
a parity item; listed so the plan ends with "what a player gets". **by design**

## 9. The decisions, narrowed by a second look (2026-10-20 00:10)

The user: *"can you do more investigations regarding the questions, maybe we can narrow some down a bit?"* Each
decision with the fact that narrows it and what is left to choose.

**9.1 Where a drawing's geometry lives (1.1, 3.4).** Facts: a skin's `[Tuning]` applies to both styles; there is no
`[Tuning.Ride]` / `[Tuning.Cart]` for a skin (Looks.cs:502), while the donkey's own `RiderOffsetPx` differs per
style (-6 riding, -8 with the cart). The hitch gap cannot be read off the picture's bounds (the rat's rear edge is
24 px behind the donkey's, its declared gap 9; the dragon's 41 against 24: the tails hang past the cart), so it
is a judgement per drawing, which the generator can hold since it draws the body. Narrowed: **the skin owns its
geometry** (`seat`, and `[Tuning]`, `[Tuning.Ride]`, `[Tuning.Cart]` for the pixel keys; the two style sections
are new and small); the mount owns creature facts only; the scan refuses pixel keys under `[Mount.Tuning*]`; the
Mounts tab edits the skin's rows and saves them. The three mount-level values move: snail `RiderOffsetPx = -6`
into `[Tuning.Ride]`, Gerry `SeatAdjustPx = -16` into `[Tuning]`, the rat's and dragon's `HitchGapPx` into
`[Tuning.Cart]`; the snail's, worm's and Gerry's gaps measured with a cart readout before they are declared.
Nothing left to decide unless you prefer the mount as the default home.

**9.2 Every skin names its regions (1.3).** Fact: Gerry's colours are the game's bone and socket shades; the probe
script that would list them (`gerry_probe.py`) no longer finds the Characters atlas in the current build (its
bundle name and folder are constants from 2026-10-18), so the list comes from the in-game export. Narrowed:
every skin carries `[Colors]`; Gerry's names its socket shades as Dark once the export is in; the probe script
takes the prefix and finds the bundle by content. Nothing to decide.

**9.3 The effect ring (2.4).** Fact: the canvas says nothing about the creature's size: the rat is 85 px wide on
the 96 canvas, the snail 62, the worm 64, the donkey 46, the dragon 108 on 128, Gerry about 45 on 160. Scaling by
canvas would leave the rat's ring at the donkey's and make Gerry's 1.7 times too wide. Narrowed: **the ring scales
by the drawn width of the standing frame** (46 px = 1.0), which the sheet build already measures for the seat; no
key. Nothing to decide.

**9.4 Mount sounds (2.6).** Fact: every sound is already a per-instance setting a mount may set in
`[Mount.Tuning]`: the ids `BraySoundId`, `GrazeSoundId`, `MountSoundId`, `DismountSoundId`, `HitchSoundId`,
`UnhitchSoundId`, the volumes, pitch and timing; none is a look key (only `Effect`, `EffectWhen`, `Skin` and the
colours are, Tuned.cs:130, Plugin.cs:582-585). The one donkey-only sound is the lip smack, fired by the donkey
animator's clip event (`LipsVolume = 0` silences it). Narrowed: **phase 1 is data and docs only**: the snail and
the worm silence the bray and the hoofsteps (`BrayVolume = 0`, `HoofVolume = 0`, `LipsVolume = 0`), Gerry keeps
the lips off, the keys are documented as a mount's; phase 2 (the game's other voices) is its own design later.
Nothing to decide for now.

**9.5 A skin that comes with an effect (2.13).** Fact: Saddle Up's Effect row defaults to "As the mount"
(`MountEffects.FromMount`, Api/MountSettingsPage.cs:423), so a player who picks the Hellfire skin and never opens the
Effect section sees horns without fire, which contradicts the look the skin was designed as (horns, tail and
flames together). Narrowed to a real choice: **(a)** a skin-level `effect =` / `effect_when =` ("what this skin
comes with"), resolved under "As the mount" as the skin's, else the mount's (Effects.cs:399 is the one place); or
**(b)** keep skins silent and change nothing. Recommendation: (a). It is small, it gives Hellfire its fire by
default, and a mount's own effect still rules its other skins.

**9.6 Packs versus code skins (4.1).** Fact: the user's standing rule (2026-10-17): *everything must be possible
through the API*. The fields exist on the internal `SkinDef` (`ColorRegions`, `Erase`, `BaseRegions`,
`SeatFromPicture`, `Game`); exposing them on `MountSkinDef` is mechanical, and a code skin with its own sprites
can collect its colours the way a pack does at `Put`. Narrowed: **close the gaps** (regions, erase, base
recolour, the picture seat, a game source) and the info fields (4.3); write that packs and code are the same
format in two forms. Nothing to decide.

**9.7 Game creatures (5.9).** Fact: the Gerry scripts hard-code the prefix and the atlas bundle, and the bundle
name has changed with the game since (no frames found today). Narrowed: the two scripts take the prefix as an
argument and find the atlas by content; the cfg stays by hand with the steps in gerry-mount.md. Nothing to
decide.

So one decision remains yours: **9.5 (a) or (b)**; and a yes on 9.1's home for the pixel keys (the skin).

## 10. The first reading (before the second look above)

- **1.1 / 3.4 Where picture keys live.** Recommendation: *the skin owns its drawing's geometry* (`seat`, and
  `[Tuning]` for `RiderOffsetPx`, `HitchGapPx`, `SeatAdjustPx`, the leg keys); *the mount owns creature facts*
  (cart, foals, foal_scale, effect, sounds, abilities). A second skin of a mount declares its own geometry, which is
  what "a skin is the drawing" means, and the Default skin's values are the mount's by construction. The three
  mount-level px keys move to the skins' `[Tuning]`; the Mounts tab gets a skin-rows editor for them and the seat
  row, saved by the same Save; the scan refuses picture keys under `[Mount.Tuning*]`.
- **1.3** Every skin declares `[Colors]`, Gerry with `Dark` for the sockets.
- **2.4** The effect ring scales with the canvas (96 = 1.0); a `[Mount] effect_radius` later if a mount wants more.
- **2.6** Phase 1 now: a mount may set or silence the sound keys in `[Mount.Tuning]`, documented, the snail
  silenced, the worm's hoofsteps off; phase 2 is a design of its own (built 2026-10-24: [mount-sounds.md](mount-sounds.md);
  a skin may now override its mount's sounds, amending 9.1's "creature facts are the mount's" for sounds).
- **2.13** No skin-level effect; the player's pick stays the rule (an over skin is a drawing).
- **4.1** Packs are the full format; code skins are for generated or dynamic pictures. Write it down; add only the
  cheap info fields (4.3).
- **5.9** By hand for the rare game creature, written down in gerry-mount.md.

Everything tagged **fix** needs no decision and goes into the plan as is.

## 11. Done (2026-10-20, the plan's M1 to M7; CHANGELOG Unreleased has each in full)

| # | Outcome |
|---|---|
| 1.1 | **done** (M1): the skin owns its drawing's geometry (`seat`, `[Tuning]`, `[Tuning.Ride]`, `[Tuning.Cart]`); the mount owns the creature's facts; the scan refuses a pixel key under `[Mount.Tuning*]` |
| 1.2 | **done** (M2, M2b): the cart readout measures every mount (docs/seat-investigation.md 9); no mount's cart closer to its body than the donkey's; the snail, worm and Gerry need no gap (narrower bodies), said in their cfgs |
| 1.3 | **done** by rule: Gerry's `[Colors]` is written from the game's export when one exists (`colors.json`); until then everything is Coat, and his cfg's header says so |
| 1.4 | **done** |
| 2.1 | **done** (M4): the legs hang from the skin's seat; self-test on the snail's row |
| 2.2 | **done** (M2): the paper mount applies the hitch gap; no cart picture for a no-cart mount |
| 2.3 | **done** (M4): the mount's size for its own babies, 0.6 for plain donkeys; `SpawnFoal` defaults to the parent's |
| 2.4 | **done** (M3): the ring scales by the drawn standing width (self-test: the rat 1.48); Gerry's by eye (open) |
| 2.5 | **open**: the user's look first (shadow and grass bend on the dragon and Gerry) |
| 2.6 | **phase 1 done** (M4): the sound keys are a mount's to set; the snail and the worm silent, Gerry's lips off; **phase 2 built 2026-10-24**: the sound system per instance with a pack's own files, `[Sounds]` / `[Mount.Sounds]` and the API ([mount-sounds.md](mount-sounds.md)) |
| 2.7 | **done** (M4): `Spawn(WithCart)` refused; a hitched mount that becomes a no-cart mount leaves the cart standing |
| 2.8 | **done** (M4): report lines, and the scan's rules |
| 2.9 | **done** (M4): `MountSkinInfo.Regions`, `MountSettingsPage.EnableWhen` / `MountHasRegion`; Saddle Up greys the row |
| 2.10 | **done** (M2): facts follow the wanted skin, geometry the worn drawing |
| 2.11 | by design (noted) |
| 2.12 | **done** (M4): `noun =`, the side file's and the dormant list's labels |
| 2.13 | **done** (M3): `effect =` / `effect_when =` on a skin; "As the mount or skin"; Hellfire burns by default |
| 3.1–3.5 | **done** (M2): the tab edits the skin's seat row and pixel keys per style; the Riding tab shows the nudge alone on a seat skin; "mount" wording |
| 4.1 | **done** (M4): `MountSkinDef.Regions`, `Erase`, `BaseRegions`, `SeatFromPicture` (+ `TuningRide`, `TuningCart`, `Effect`, `EffectWhen`); a readable sprite's colours join the look |
| 4.2 | by design (api.md) |
| 4.3 | **done** (M4): the info records say what the cfg says; `Skins` Default first; `MountSpawn.Mount` |
| 4.4 | **done** (M4): a mount with no skin is not offered |
| 4.5 | **done** |
| 5.1 | **done** (M6): `tools/skins/review.py`, strips per creature |
| 5.2 | **done** (M3): the cfg written once, `--cfg` rewrites |
| 5.3 | **done** (M6): the watcher splits the sheet into per-frame PNGs (`front\` too), previews with the front layer and the rider |
| 5.4 | **done** (M6): `front\`, `source = game`, the donkey only under an over skin, the layout, an exit code; in the release scans; the three live findings settled by the game's drawing path (kept colours and lone pixels over the body are said, not warned) |
| 5.5 | **done** (M6): `colors.json`, `front\` included, Gerry from the export |
| 5.6 | **done** (M6): sheet cells, `[Game]` targets, the seat row on the body, erase pixels in a replace skin; the docstring |
| 5.7 | **done** (M6): the front layer everywhere, the rider on the seat row with the skin's offset and nudge and the seat line, the card's facts, Gerry from the export, names from the cfgs. To settle once with the game's export: whether the rider's hip row in the strips and the page is the game's to the pixel (a 1 px difference is possible) |
| 5.8 | **done** (M6): the tooltip says "loaded now" |
| 5.9 | **done** by the small road (M6): the prefix an argument, the bundle found by content; a cfg proposal is by hand (docs/gerry-mount.md) |
| 5.10 | **done** (M6): `tools/skins/new_skin.py` |
| 6.1–6.5 | **built** (M5): FrontLayer, CartReadout, the babies and legs tests, OverBuiltIns; the in-game run is the user's (open) |
| 7.1–7.6 | **done** (M7) |
| 8 | by design |
