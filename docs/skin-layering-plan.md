# Skin layering: every skin of a mount is built on its default (plan, 2026-10-24)

**2026-10-25 (API 0.37.0):** a skin no longer names an effect, so a variant inherits nothing about effects
(`effect` / `effect_when`, `MountSkinDef.Effect` and `SkinGiven.Effect` are gone; the layering self-tests lost their
effect clauses). Where the text below lists the effect among what a variant takes from its default, it is history.

**Built 2026-10-24** (steps 1-15; not yet seen in game: the F11 scenarios "layering: ..." and the changed sounds
scenario are written, not run). The code: `LooksInherit.cs` (Given, Parent, OverDefault, Resolve, RefreshInherited,
SetAside/Healed, the game-ready check), `Looks.cs` (FromCfg's Given, Register/Unregister, MakeProducer and
VariantProducer out of Acquire, the reuse checks, SortColours, ApplyTuning/LayeredValue, SaveCfg, the watch,
PortraitOf), `Sounds.cs` (the "default" layer, Coverage), `MountIcons.cs`, `LooksExport.cs`, `MountLooks.cs`
(`MountSkinInfo.BuiltOn` / `OverDefault`, the code sentinels), the Mounts tab. Where the build differs from the text
below, or chose where the text left a choice:
- **Portrait:** not resolved in place (the variant's `ReadFile` reads its own folder): `PortraitOf` delegates to the
  default when the variant has none and doesn't say `portrait = none`.
- **SaveCfg gate:** only for a skin that has a parent now (`d.Parent != null`); every other skin (the donkey's, a
  default, a variant standing alone) is written by its values exactly as before.
- **Resolve touches variants only:** a skin that can't have a parent (the donkey's, a mount's default) is never
  rewritten by Resolve; a variant's values as read are kept in `AloneValues` (a given seat, effect or offset the tab
  takes back falls to the neutral value).
- **Erase is not inherited** (it is about the variant's own pixels); `layer` is not inherited either: a variant is
  drawn in the default's place unless it says `layer = over`. `layer = over` on a mount's **default** is as before
  (over the game's donkey); a default drawn over the donkey gives its variants settings, not pictures (said once).
- **Default withdrawn:** the "stands alone" line is a Warning only for another mod's variant; a pack withdrawing its
  own default and variants together says it as Info (no warning for a pack going as a whole).
- **Validate's wording** is decided in FromCfg ("N of the donkey's 44 frames from its default (<m>/default)") for
  every variant slot, also one registered before its default (the Info line says it stands alone until then).
- **The canvas mismatch** is one Warning at resolve (pictures not layered, seat only when height and pivot row
  match, "drawn alone, its erase pixels left out" for an over variant); the build then says once if erase pixels were
  dropped (WrapAlone).
- **Code skins:** `Over` is refused only on a code skin that can have a default (a mount other than the donkey, not
  its `default`); a code default with `Over` is as before. Given from the sentinels: `Colours` and `Layer` always;
  `Offset` when `Offset` is not zero or `CartOffset` is set (then `OffsetCart` too).
- **Icon:** a variant without its own standing picture shows its default's icon; one drawn over its default the
  composed picture (`ResolvedPicture`); the `d.Over` branch is unchanged (only skins over the game's donkey have
  `Over` now).
- **Mounts tab:** "(from the default)" on the label of a row the variant doesn't set (seat, effect, offsets, the
  skin's tuning rows show the default's value); a right-click on a row it sets clears Given.
- **canonical_cfg.py** keeps one layout for every skin; for a variant `--check` notes a `[Skin]` line that only
  repeats its default's (never a failure).
- **Sound clips of a code default** are banked as that skin's (`"skin"`), so wearing the default and a variant
  share one entry and it goes with the default.

The user: "all other skins are built on top of it". Decided meaning, both parts:
- **Inherit**: a skin of a non-donkey mount starts as a copy of `<mount>/default`. Every picture and every skin.cfg
  setting it doesn't give comes from the default.
- **Draw over**: `layer = over` on such a skin draws it over that default, holes from the erase colour (FF00FF by
  default), the way Hellfire is drawn over the game's donkey.

The donkey mount is unchanged: its "default" is the game's art. Line numbers are from the study of 2026-10-24 and drift
as the code changes.

## 1. Facts: how it works today

### Definition and parsing
- SkinDef: Looks.cs:157-264. Sources, one of: Keys+LoadPng, a sheet, Sprites/FrontSprites (code), Recipe, Game.
- FromCfg: Looks.cs:667-857. Defaults are filled in, so "given" is lost for canvas (set to the base's), pivot (a local
  `pivotGiven`), erase (a local `eraseGiven`; FF00FF for layer = over), colours (OwnColours true), effect_when
  (Always), offset (zero = not said; offset_cart copies offset unless the text has offset_cart), seat (-1 or picture).
- Validate (873-911) writes "N of the donkey's 44 frames not drawn (the donkey shows there)".
- Pictures: a PNG list (Keys = KeysOf(FileNames)), a sheet (SheetKeys = `frames` or the base's 44), game frames.
  Folder files: *.png, `front/<f>` (FrontKey), `sounds/<f>` (SoundsKey).

### Registration
- Register: Looks.cs:334-400 (full name, the default/donkey rules, the prefix rule, the source check, the version
  rule against another owner, `defs[d.Id] = d`, MarkWearersDirty, RaiseChanged).
- RegisterEmbedded takes the skins in dir-name order ("black" sorts before "default"); RegisterMountFolder in name
  order. A mount pack without skins\default\ is refused.
- SkinDef.Mount re-resolves lazily against Catalog.Version: the pattern for the parent link.
- DefaultSkinOf: a code mount without "default" falls back to its first skin by name.

### Pictures
- Acquire: Looks.cs:1786-1876. A Loaded is shared per id and reused only when `l.Def == d`. Sprites are ready at
  once; game skins: StartGameBuild (LooksGame.cs); recipes: StartBuild (LooksRecipes.cs); sheet: StartSheetBuild;
  PNG list inline. WrapErase when `d.Over && d.Erase != null`. A failing game skin is set aside, any other finishes
  empty.
- Put: refuses the wrong size, collects the colours, records the seat top, columns, stand width. Finish makes the
  sprites (pivot Def.PivotX/PivotY).
- A sheet past its last cell gives an empty frame (the base shows there).
- AdvanceBuilds: 6 frames a tick. Resolve: key, then clip, then part, else the donkey's own.
- Release: refcounted. TickWarm: reuses `warm.Def == d`, waits for base frames when NeedsBaseFrames.

### Erase compose (donkey only)
- WrapErase (Looks.cs:1950-2001) reads the game frame (FindGameFrames, ReadFrame) and puts it under the picture with
  holes where the erase colour is; WholeKeys and Erased recorded; front keys can't erase; another canvas: no compose.

### Drawing
- Rig.ApplyPart: an over skin's overlay sorts above the game part; the part is hidden unless Over or the frame is
  whole. A non-whole over frame shows the game donkey under it: non-donkey mounts have nothing under an overlay.
- Follow applies the offsets; TrySeatTop reads the seat; EffectScale uses StandWidthPx when not Over.
- Rig.Tick keeps a pending skin while `ReferenceEquals(pending.Def, Find(want))`; a rebuild goes through Acquire.

### Tuning, colours, sounds
- ApplyTuning: mount Tuning, mount style, speed, then skin Tuning, TuningValues, skin style; LayeredValue mirrors it.
- SortColours: a colour [Colors] names goes to that region, others to Coat (none for Over). Colors.cs recolours.
- Sounds (Sounds.cs:799-831): mount layers, then skin layers (the skin's own sounds\), then a mod's override.
  Coverage: Sounds.cs:106-121.

### Portrait, icon, export, save, reload, watch
- PortraitOf, PortraitOfMount, MountIcons.OfSkin, MountsPanel, Effects (skin effect), MountSkinInfo, LooksExport.
- SaveCfg keeps [Skin] lines as written except seat, effect/effect_when, mount, offset, offset_cart (rebuilt from
  the fields); [Tuning*]/[Sounds*] from the dictionaries; [Colors], [Base.*], [Game] as text.
- ReloadCfg: fresh FromCfg, Register, Catalog.Touch. TickPackWatch watches loaded skins with a Source.
- The Mounts tab edits the live def in place, then Catalog.Touch; auto-save 0.75 s later.

### Code skins
- MountSkinDef.ToDef: CanvasWidth 96, Pivot 48, SeatRow -1, Offset zero, PaletteColours false, Over false; Effect,
  Description, Portrait, Regions, Tuning, Sounds may be null.

### Tests and tools that rely on today's behaviour
- SkinsScan requires all 44 PNGs per non-sheet, non-game skin, the canvas, the seat row, [Colors] in its own pictures.
- check_skin.py: canvas from its own cfg, missing frames warned, over composed against the donkey's frames.
- SelfTest.Sounds.cs ~357/371: the second skin of the sounds pack expects the mount's voice.

## 2. The design

### 2.1 Where inheritance resolves
- Parent of d: `<d.MountId>/default`, only when the mount isn't the donkey and d isn't the default. A code mount whose
  look isn't named "default" gives no parent.
- Settings resolve **in place** into the registered SkinDef's fields, so every reader stays as it is. New partial
  file src/GK2DonkeyAPI/LooksInherit.cs: `SkinDef Parent`, `SkinGiven Given` ([Flags]: Canvas, Pivot, Colours, Layer,
  Erase, Seat, Offset, OffsetCart, Effect, Description, Portrait, Base), `OwnColorRegions`, `Resolve(d)`,
  `RefreshInherited(mount)`.
- When: (a) in Register before `defs[d.Id] = d`; (b) after a default registers or unregisters: every variant of that
  mount re-resolves, then Catalog.Touch(mount); (c) in Catalog.Touch(mount): RefreshInherited (tab edits to the default).
- Order: the embedded and folder loaders take skins\default\ first; cross-mod joiners are covered by (b).

### 2.2 How "given" is tracked (FromCfg)
- canvas, pivot (canvas given but not pivot: centre of the own canvas; neither: the parent's), erase, colours, layer,
  base, seat (`seat = none`: given, no seat), effect/effect_when (`effect = none`: given, none: the mount's effect),
  offset/offset_cart (offset without offset_cart: cart = ride as today; neither: the parent's; `offset = 0 0` given
  cancels), description, portrait (`portrait = none`).
- Never inherited: name, author, version, format/needs.
- [Colors]: own map kept; resolved = parent's minus colours the variant names, plus the variant's lines.
- [Tuning*], [Sounds*]: not merged, layered at read time (2.5).
- [Base.*] on a non-donkey over variant: reported and dropped.
- source/[Game]/sheet say where the variant's own pictures come from; the default's producer gives the rest.
- Code skins: not given = SeatRow < 0 && !SeatFromPicture, Effect null, Description null, Portrait null, Regions null,
  Offset zero && CartOffset null, canvas/pivot left at 96/48. Given: PaletteColours, Over. Said in the XML comments.

### 2.3 Pictures
- Refactor the producers out of Acquire into `MakeProducer(SkinDef d, string prefix)` (keys incl. front/, a
  Func<string, Pixels>, needsBaseFrames). Parent-less skins behave as today.
- Variant (both pixel sources, not code sprites): keys = the parent's in order, then own keys not in it (flight and
  front/ keys included). Produce(key): own pixels when there (an all-transparent own picture counts: a frame blanked
  on purpose); an own picture that fails to read: one warning, then the parent's; else the parent's. A short own sheet
  falls through to the parent.
- A variant with `sheet =` takes only its sheet as its own; loose PNGs are ignored (said in the report).
- Front layer: per key, own front/ over the parent's front/.
- Canvas/pivot: inheritance and the over compose need the same effective canvas and pivot; on a mismatch the variant
  uses its own pictures only and one line is logged.
- Loaded gets `Parent`, `Inherited`, `Composed`, `ParentColours`.
- Validate: "M from its default (<id>)" instead of "the donkey shows there".
- A variant with no pictures and no parent: Unavailable ("no pictures of its own and no default look to build on").

### 2.4 Over the default (non-donkey mounts)
- `layer = over` on a non-donkey, non-default skin sets `OverDefault = true`; `Def.Over` (over the game donkey) stays
  false. The erase default FF00FF applies.
- In the variant's Produce, for own pixels O and parent pixels P: out = P with O's erase pixels transparent and O's
  other opaque pixels on top (WrapErase's rule). Composed++, Erased += n. No P: O alone, erase cleared, one warning.
  Front keys compose against the parent's front picture.
- Every frame is then a plain replace frame: no second render layer.
- NeedsBaseFrames(variant) = own || parent's.

### 2.5 Sounds, tuning, colours, effect
- Sounds: a "default" layer between the mount's and the skin's (the parent's blocks with the parent's sounds\ files);
  the same in Coverage. A variant's file line reads only its own sounds\.
- Tuning: ApplyTuning and LayeredValue take the parent's Tuning/TuningValues/style first, then the variant's.
- Colours: an unnamed colour is Coat when it came from the parent's pixels or the skin isn't OverDefault; a colour only
  in an OverDefault variant's own pixels stays unmapped. The "names colours its pictures don't have" warning counts
  only OwnColorRegions.
- Effect: inherited as a field.

### 2.6 Code skins
- Settings inherit by the sentinels (2.2). Picture inheritance from or into code sprites: not in step 1 (one log
  line). Over on a non-donkey code skin: refused with a report line, drawn as replace.

### 2.7 Default changes, reloads, withdrawal
- A new default (version rule or ReloadCfg): every variant re-resolves, icons forgotten, wearers dirty, Catalog.Touch.
  Reuse of a Loaded needs `l.Def == d && l.Parent == d.Parent` (Acquire, TickWarm, Rig.Tick's pending check).
- Default withdrawn: variants stand alone (non-given fields back to standalone values), wearers rebuild, a warning.
- Unavailable: a default set aside or healed carries its picture-inheriting variants (helper `SetAside`); a parent
  producer throwing inside a variant's build sets the variant aside.

### 2.8 SaveCfg writes only the variant's own values
- seat only if Given.Seat ("none" when given with none), effect/effect_when only if Given.Effect ("effect = none"),
  offset only if Given.Offset, offset_cart only if Given.OffsetCart or it differs from the own offset.
- The Mounts tab's seat, effect and offset editors set the Given flag; a right-click on a variant clears it
  (RefreshInherited, Catalog.Touch).

### 2.9 Reload and watch
- TickPackWatch watches loaded defs and their parents.

### 2.10 Portrait, icon, export, panel, API
- PortraitOf: no own portrait and not given → the parent's.
- MountIcons.OfSkin: a variant without its own rest picture, or OverDefault: the resolved producer's
  bdy_static_left; else the parent's. The `d.Over` branch only for the donkey.
- LooksExport manifest: ", built on <default>: X own, Y from it, Z drawn over it".
- MountSkinInfo: new `BuiltOn` and `OverDefault`.
- Mounts tab: "built on <default name>: N own pictures, M from it"; rows show own / from the default / unset.

## 3. Edge rules and log lines
- Variant before its default (Info once): "Looks: skin '<m>/<s>' is built on '<m>/default', which isn't registered
  yet; it stands alone until it is."
- The default arrives (Info): "Looks: skin '<m>/<s>' is now built on '<m>/default' (<n> own pictures, the rest from
  it; <settings> from it)."
- Still parentless at game ready (Warning for pack mounts): "Looks: skin '<id>': its mount '<m>' has no default look;
  it stands alone (the donkey where it has no picture)."
- Default withdrawn (Warning): "... its default look '<m>/default' was withdrawn by <owner>; it stands alone now."
- Default reloaded or replaced (Info): "... takes the new '<m>/default' (<where>, version <v>); its wearers rebuild."
- Different canvas (Warning once): pictures not inherited, seat inherited only when CanvasH and PivotY match.
- layer = over with a different canvas (Warning once): drawn alone, erase pixels left out.
- Own picture bad (Warning per key): "the default's is drawn there."
- Over with no default picture for the key (once): erase pixels left out.
- Variant of the donkey: no parent, no new line, everything as today.

## 4. Tests
New self-tests (GroupLooks, temp packs): variant built on its default (pictures, seat, offset, effect, tuning, sound
layer "default", colours); variant over its default (erase and compose, regions); default reloaded (variants follow,
watch); SaveCfg leaves inherited values out (and Given via the tab path); order and withdrawal; canvas mismatch; donkey
skins unchanged; code skin inherits by sentinels. Flip SelfTest.Sounds.cs ~357/371 (the default layer). SkinsScan,
check_skin.py and canonical_cfg.py read a variant against ../default.

## 5. Steps
1. LooksInherit.cs (Given, Parent, OverDefault, Resolve, RefreshInherited); FromCfg sets Given; Register/Unregister
   resolve; Catalog.Touch refreshes.
2. Registration order: the default first; the game-ready check.
3. Producers: MakeProducer and the Acquire split. Gate: no behaviour change for parent-less skins.
4. Variant build: key union, fallback, compose, Loaded.Parent/ParentColours/Composed, canvas rule; the reuse checks.
5. Colours. 6. Tuning and sound layers. 7. Unavailable propagation. 8. SaveCfg gating and "none" tokens. 9. Watch.
10. Portrait and icon. 11. API and code skins. 12. Mounts tab. 13. Export manifest. 14. Self-tests. 15. SkinsScan,
check_skin.py, canonical_cfg.py, docs.

## Risks
- Stale composed sheets if a reuse check misses the Parent comparison (Acquire, TickWarm, Rig.Tick).
- Inherited values baked into variant files (the SaveCfg gate and the tab's Given marking).
- Edits to the default reach variants only through Catalog.Touch → RefreshInherited.
- Colour provenance on over variants.
- Memory: a variant's sheet holds every default frame (dragon about 11.5 MB); build time 173 frames / 6 a tick.
- Game-source defaults (Gerry): a parent producer throwing must set the variant aside.
- Code skins: SeatRow -1 now means "inherit" (said in XML docs and the release notes).
