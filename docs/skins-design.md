# The skinning system: design (2026-10-17)

**Amended 2026-10-24 (the mount folders):** a mount is no longer a `[Mount]` block inside its default skin's
skin.cfg. A pack is `gk2mounts\<mount>\` with `mount.cfg` (`[Mount]` with `default_skin`, `[Stats]`, `[Tuning*]`,
`[Sounds*]`: plain names), the mount's `sounds\`, and `skins\<skin>\` for each look (skin.cfg, pictures, `front\`,
its own `sounds\`); the donkey's folder holds skins only. The "declaring skin", joiner and Default-by-id rules
below are history; docs/skins.md "What a pack is" and docs/making-a-mount.md are current (MountPacks.cs).

**Amended 2026-10-24, later (the ids):** a mount's id is `<mod>.<name>` (`saddleup.dragon`; only the game's `donkey`
is plain; one without a dot is refused). A skin is named within its mount, full name `<mount id>/<skin>`
(`saddleup.dragon/default`, `donkey/saddleup.hellfire`; skin.cfg has no `id =`). Every mount but the donkey has exactly one
default look, its `skins\default\`; `default_skin` is gone. Why: docs/mount-id-collisions.md.

**Amended 2026-10-24, later still (skin layering):** every skin of a mount but the donkey is built on that mount's
default (`<mount>/default`): what it doesn't give (pictures per frame, `[Skin]` settings, `[Colors]`, `[Tuning*]`,
`[Sounds*]`) comes from the default, and `layer = over` on it draws it over the default (holes from the erase colour)
instead of over the game's donkey. The donkey's skins are unchanged. docs/skin-layering-plan.md (built 2026-10-24),
docs/skins.md "Variants: built on the default", LooksInherit.cs.

Status 2026-10-20: this is the design as written on 2026-10-17; the system has moved on and docs/skins.md (the pack
format), docs/mounts-design.md (mounts) and docs/mounts-foundation-audit.md (the definitions as they stand) are
current. Out of date below: the recipes zebra/ghost/ember/golden/shadow and the blue dragon (gone 2026-10-19;
Hellfire is a pack); `ISkinSource` with textures (it is `IMountSkinSource` with sprites); the resource names
(`GK2DonkeyAPI.skins.<id>\<file>`, in the API's DLL, not Saddle Up's); "one section" in skin.cfg (there are
`[Skin]`, `[Tuning]`, `[Game]`, `[Colors]`, `[Base.*]`, `[Mount]` and its tuning sections); a skin `foals` flag (none);
packs scanned under `gk2skins\` of the plugins root and one folder down, not `plugins/**`; a missing skin shows
"<id> (not installed)"; the self-test group is "Skins and effects"; per-skin offsets are built (§12).

Status (2026-10-17 evening): **all six steps built (API 0.30.0 / Saddle Up 0.10.0)**; step 6 is the particle effects
system with Hellfire as its first effect (`Mounts.Effects`, Effects.cs, api.md "Effects"), not a skin attachment. The
loader now packs every skin onto **one sheet texture** (§7), a skin names its **base** (§12, the donkey is the only
record), picture packs may ship **one sheet PNG**, and code sources give **sprites** (`IMountSkinSource.TryGetFrame`
returns a `Sprite`). A red dragon as own art (128 canvas, the wing above the rider) joined the built-in skins at 19:50 (tools/skins/creatures/make_creatures.py). Hand tests of steps 3 to 6 are open. Earlier status: **steps 1 to 5 built (2026-10-17, API 0.30.0): the rig, the built-in picture skins, the companion property, skin packs on disk with the validation report, withdrawing a skin, three self-tests, the List settings row (ids stored, labels shown, the list read once per session) and `Mounts.Looks`; docs/skins.md for pack authors; the five recipes (zebra, ghost, ember, golden, shadow) built from the atlas at runtime with the whole switch. The game's own look has the id "donkey" in the row and the cfg (an empty dropdown choice was a risk with the framework), and "" means the same everywhere. Step 1 passed its in-game test (hitch, unhitch, a door, foals, no pool leftovers, no warnings).** One change from the text below: the built-in skins (rat, snail, worm, later the recipes) ship inside the **API** DLL (`assets/skins`), not Saddle Up's, so the API is self-contained and Saddle Up stays a settings shell. This is the full specification of how skins are defined, loaded, drawn, chosen, saved,
exposed to other mods and tested. It follows the investigation in [external-assets.md](external-assets.md) (what the
game allows) and the rounds in [mount-looks-design.md](mount-looks-design.md) (what the player gets, the decisions).
Everything here was designed against the code as it is; the file and line references are from 2026-10-17.

## 1. Purpose and scope

A **skin** draws the donkey as something else: a zebra-striped donkey, a ghost, a giant rat, a dragon. The system has
to be:
- **scalable:** dozens of skins, many skinned donkeys and foals on screen, no per-frame cost to speak of, memory in
  proportion to the skins in use, not the skins installed;
- **customisable:** players pick per donkey; skins carry their own options where that makes sense;
- **extensible:** new skins without touching the API (folders), new mounts of skin (recipes, code), other mods
  registering skins, future layers (saddles, barding), future bases (another mount), and the same lifecycle for
  attachments like Hellfire.

In scope for the first version (API 0.30.0 / Saddle Up 0.10.0, with Hellfire, mount-looks-design.md §Decided):
picture skins from folders, embedded pictures, recipe skins built in, one skin per donkey, foals optional, the settings
row, the settings picture, persistence, validation, self-tests. Out of scope until the system settles: per-skin rider
and cart offsets, skin sounds, several overlay layers in the UI, a skin picker with pictures.

## 2. What the code dictates

The investigation read Mount.cs, MountInstance.cs, MountRegistry.cs, Tiers.cs, DonkeyPrefab.cs, Colors.cs, the rider
files, Persistence.cs, the Api folder, the tests and the framework DLL. These facts bind the design:

| Fact | Where | Consequence |
|---|---|---|
| A donkey's view is made of the game's pooled parts; the API owns the view lifecycle: `PrepareParts` (Mount.cs:1187), the per-frame tick (Mount.cs:414 ridden, MountInstance.cs:125 standing), wake from switched-off (Tiers.cs:382-418), `ReleaseView` (MountInstance.cs:365) with `DonkeyPrefab.ResetSprites`/`Audit` after the rigs detach | pooled-parts.md | A skin is **instance state attached and removed by the API at those points**, never something that remembers renderers (the probe's foal bug, mount-looks-design.md §a foal wore the skin) |
| Hitch/unhitch is a new `Wgo` (`Restyle`, MountInstance.cs:625); doors destroy the view (`Wgo.OnDestroy` prefix); parked views go to `ViewPool` keyed by scene, prefab and scale (Tiers.cs:46-111) and can be handed to any instance, a foal included | | Attach when a view is prepared, detach when it goes; the skin id lives on the instance, not the view |
| The game's `SkinChangerGK2` re-points `bdy`/`hed` sprites to `(Clone)` copies and **rewrites each renderer's `enabled`** every LateUpdate; `AnimationComponent.OnEnable` puts its layers back | docs/colors.md:55-59, Tiers.cs:402 | Never hide by `enabled`; the API's own hide is `forceRenderingOff` (`HideDonkeyOnly`, `HideDonkeyForAFrame`, the LOD freeze), restored in `ReleaseView` and reset by the audit |
| The rider's seat, bob and leg read the donkey's **sprite names** every frame: `BackHeightPx` (Mount.cs:2727-2772), `WalkFrameIndexByName` (RiderMotion.cs:42-58); HeadWatch reads `hed.sprite` | | The original sprite must stay as the game set it: a skin is an **overlay renderer**, never a replacement of `bdy.sprite` |
| The game blanks `hed` during walks (walk frames carry the head); `hed_*` is only the standing head layer | HeadWatch.cs:8-31 | A skin's body frames must include the head; `hed_*` pictures are for standing only |
| The game draws with two cameras; Unity's `Camera.main` is the deform camera | mount-looks-design.md §cameras | Per-frame apply must cover every camera; screen positions use `CameraSystem.Instance.WorldCamera` |
| `Colors.Rig` is the model of a per-view feature: `Attach`/`AttachRoot`/`Tick`/`Detach`/`Release`/`MarkDirty`/`CheckNow`, a refcounted `SharedTextures` cache, per-instance choices as `Tuned<T>` overrides resolved while the instance is bound, `OwnLook` + `IsLook` | Colors.cs | The skin rig copies that shape exactly, so every place that already handles Colors handles skins the same way |
| Per-instance overrides are saved in the side file as `set <key> <setBy> <value>` and restored through `InstanceTuning.Find` (Persistence.cs:273-287, 714-727); `Tuned<string>` exists | | The skin choice is a `Tuned<string>`: saved, restored, read by the settings picture's paper donkey, with no new persistence code |
| `PreviewStage` renders a stripped prefab copy; rigs attach with `AttachRoot` and tick in `Pose`; the donkey-alone photo hides renderers not in `donkeyRenderers`; the copy's `hed` is disabled | PreviewStage.cs:112-260 | The skin rig needs `AttachRoot`, a tick in `Pose`, and its overlays counted as donkey renderers |
| `MountSettingsPage.Choice<T>` is enum-only; the framework has `AddDropdown(section, key, value, string[] choices, name, description, order)` returning `ConfigEntry<string>`; the Settings window and the look bar already take any `Fields`/`Values` arrays | MountSettingsPage.cs:98, GK2.Framework.dll, SettingsRows.cs:479-486 | A new string-list row mount is possible everywhere; its list is fixed when the row is materialised |
| CecilScan: a class that uses game members must be sorted in game-code.txt; a listed class that uses none fails as stale; frozen needs a `Freezers` entry | GameCode.cs, tests/CecilScan | The skin rig uses Unity types and prefab names, probably no game member: its freeze is tied to `Colors.Frozen` (same borrowed parts) rather than a table line |
| `DefaultsScan` refuses `Colors*` keys in defaults.cfg; `IsLook` keys stay out of "Make these the mod defaults" | tests/DefaultsScan, Tuned.cs:29 | The skin keys are `IsLook`, so players always start with the game's art |
| `Live.TextureMade/Gone` feed the Leaks self-test (80 donkeys spawned and removed twice) | Perf.cs:342-357, SelfTest.cs:1240 | Every skin texture is counted, so leaks show in the existing test |
| api-design.md (2026-09-30) said "C# only, no data files" | api-design.md:18-19 | **Overridden** by the open-folder decision (mount-looks-design.md §Decided 3): skin packs are data files by design, read-only, and never touch the save |

Settled by the probe runs (external-assets.md): a PNG per frame drawn with the donkey's material is lit like the game's
art and recoloured by Colors when it uses the donkey's palette colours; 44 donkey frames exist (41 body + 3 head);
96×96 at 50 pixels per unit with the pivot in the middle; left-facing only.

## 3. Concept model

```
Look (per instance)                 what a donkey wears: a list of layers (v1 UI: one)
 └─ Layer = a Skin + a role          replace (the animal) | over (saddle, barding, hat)
      └─ Skin                        id, name, author, format, base, canvas, mount, flags, source
           └─ Source                 where the pictures come from: folder | embedded | code | recipe
                └─ frames by key     "bdy_walk_left_03" -> Sprite
```

- **Base:** the frame-key namespace. v1 has one base, `donkey` (prefab `npc_donkey`, the `0301_` sprites). A future
  mount would be another base with its own key list; a skin names its base and never applies to another.
- **Frame key:** the game sprite's name without the leading digits-and-underscore preset id and without `(Clone)`:
  `0301_bdy_walk_left_03(Clone)` → `bdy_walk_left_03`. Any numeric prefix is stripped, not literally `0301_` (that
  prefix is the game's skin preset id and may change).
- **Resolution order** for a frame: exact key → clip (`bdy_walk_left`) → part (`bdy`) → none. "None" means the
  donkey's own picture shows for that frame, and it is logged once per skin and key, so a half-finished skin works and
  tells its author what is missing.
- **Mount:** `palette` (drawn in the donkey's exact palette colours, so the Colors feature recolours it) or `own`
  (its own colours; Colors has nothing to match and leaves it).
- **Flags:** `foals` (may be worn by foals), `layer` (replace | over).

## 4. Skin definitions

### 4.1 The folder format (skin packs, no code)

```
<any folder>/gk2skins/<skin id>/
  skin.cfg
  bdy_static_left.png
  bdy_walk_left_01.png ... _08.png
  bdy_ear_left_01..05.png, bdy_graze_left_01..05.png, bdy_chew_left_01..03.png,
  bdy_lips_left_01..13.png, bdy_turn_left_01..06.png
  hed_static_down.png, hed_static_down_02.png, hed_static_down_03.png
  bdy_walk_left.png / bdy.png / hed.png            (optional fallbacks: one picture for a clip, or for a part)
```

`skin.cfg` (BepInEx cfg syntax, one section):

```
[Skin]
format = 1              # the format version; a newer one is refused with a log line
id = rat                # letters, digits, dots, dashes; unique; "donkey" is reserved
name = Giant Rat        # shown in the settings
author = dreas
version = 1.0
base = donkey           # the frame-key namespace (default donkey)
colours = own              # palette | own (default own)
layer = replace         # replace | over (default replace)
canvas = 96 96          # picture size; every picture must be this size (default 96 96)
pivot = 48 48           # the donkey's ground anchor in the picture, from the bottom left (default: the centre)
```

Rules:
- **Pictures:** PNG, RGBA, exactly `canvas` in size, facing left, point-filtered, drawn at 50 pixels per unit (the
  game stretches upright sprites ×1.667 itself). A larger canvas is allowed (the high-winged dragon is 128×128); the
  pivot keeps the donkey's anchor so the rider sits where he sits on the donkey.
- **Names:** the frame keys above. An unknown name is listed in the log as a probable typo. The complete key list is
  exported by the dev panel and by `Mounts.Looks.FrameKeys`.
- **The head:** body frames include the head, as the game's do; `hed_*` is drawn only while standing. A skin that
  draws a separate head in walk frames gets a double head: the validation warns when `hed_walk_*` names appear.
- **Mirroring:** none in the files. The API mirrors as the game does (the donkey's `Base` scale).
- **Where packs live:** the API scans `BepInEx/plugins/**/gk2skins/*/skin.cfg` at its own `Awake` (depth-limited, so a
  pack is a zip dropped into `plugins`, no DLL). Saddle Up's own skins ship inside its DLL (4.2), not as a folder, so a
  broken install can't half-load them.
- **Unknown keys** in `skin.cfg` are ignored with one log line, so a future format can add keys that an older API
  skips.

### 4.2 Embedded pictures (a mod's own skins)

The same files as `EmbeddedResource` with logical names `<prefix>.<skin id>.<file>`, e.g.
`GK2SaddleUp.skins.rat.bdy_walk_left_03.png` and `GK2SaddleUp.skins.rat.skin.cfg`. Registered with
`Mounts.Looks.RegisterEmbedded(ownerId, assembly, "GK2SaddleUp.skins")`, which reads every `skin.cfg` under the
prefix. Pictures are read lazily and decoded once (`Texture2D.LoadImage`, already referenced).

### 4.3 Code skins

`Mounts.Looks.Register(ownerId, SkinDef)` with a `SkinDef` whose `Source` is an `ISkinSource` the mod implements:

```csharp
public interface ISkinSource
{
    IEnumerable<string> Keys { get; }                 // the frames it has (for validation and the to-do list)
    bool TryGetFrame(string key, out Texture2D tex);  // the picture for a frame key, canvas-sized; built on demand
}
```

A source is asked once per key per load; it may build textures lazily. A source that throws is logged once and
treated as "no picture". **Ownership:** a texture returned by a mod's source belongs to the source (the API never
destroys it, and doesn't count it); the API owns the sprites it creates from it, and owns both the textures and the
sprites of its own loaders (folder, embedded, recipe), which it counts and frees (7).

### 4.4 Recipe skins (built in; no art shipped)

A recipe is a rule applied to the donkey's own frames at runtime: the zebra, ghost, ember, zombie, golden, shadow and
dragon prototypes (tools/skin_recipes.py, retired 2026-10-19). In the API a recipe is an `ISkinSource` that:
1. reads the donkey's frame pixels once per key through the existing read-back path (`ColorsPreview.Extract`:
   `Graphics.Blit` to a RenderTexture, `ReadPixels`; cached per sprite);
2. runs a pixel function `(SkinFrame frame) → Color32[]` with helpers the prototypes needed: luminance, the outline
   (rim) and inner-rim tests, the back line per frame (`BOB`, the row of the back at column 58, measured 2026-10-17),
   ordered dithering, polygon fill, hashing for deterministic noise;
3. returns the texture, which the API caches like any other.

Recipes are registered the same way as code skins: `Mounts.Looks.RegisterRecipe(ownerId, id, name, pixelFunction)`.
The donkey's atlas is never written to disk or redistributed: the recipe runs in memory, as Colors' palettes do.
Because recipes build from the game's frames, they cover every clip automatically, including clips a later game
version adds.

**Atomic switch.** Reading back 44 frames stalls the GPU for a moment each; building a recipe is spread over frames
(a budget per frame, e.g. 6 keys), and the instance keeps its previous look until the whole set is ready. No plain
donkey frames pop during the first walk.

## 5. Runtime

### 5.1 The rig and its lifecycle

`Looks.Rig` is one object per `Mount.Instance`, created with the instance like `ColorsRig` (MountInstance.cs:49, 67):

| Step | Where | Skin rig call |
|---|---|---|
| View prepared | Mount.cs:1272, right after `ColorsRig.Attach(donkey)` | `LooksRig.Attach(donkey)`: finds `bdy`/`hed` by name (as Colors does, Colors.cs:574), makes the overlays, resolves the instance's skin |
| Settings picture | PreviewStage.cs:136-141 (`AttachRoot`), :255-259 (`Pose`), :268 (`Fail`) | `LooksRig.AttachRoot(copy)`, `Tick()` in `Pose`, `Detach()` in `Fail`; the overlays are added to `donkeyRenderers` so the donkey-alone photo keeps them |
| Every frame | the per-frame apply (5.2) | pick the picture for the frame, copy renderer state, assert the hide |
| Woken from switched-off | Tiers.cs:404, next to `ColorsRig.CheckNow()` | `LooksRig.CheckNow()`: re-assert the hide after `QuietAnimators(false)` restored `forceRenderingOff` on every renderer |
| Skin changed | `InstanceTuning.Changed()` (MountInstances.cs:521-530), next to `ColorsRig.MarkDirty()` | `LooksRig.MarkDirty()`: resolve the new skin on the next tick (atomic switch, 4.4) |
| View released | MountInstance.cs:381, next to `ColorsRig.Detach()`, **before** `ResetSprites`/`Audit` | `LooksRig.Detach()`: hide cleared, overlays **unparented then destroyed** (Destroy waits for the end of the frame; the audit runs now, pooled-parts.md) |
| Instance gone | `Discard` (MountInstance.cs:438), dismount of the summon (Mount.cs:1009), ephemeral removal (:1055), next to `ColorsRig.Release()` | `LooksRig.Release()`: drops the instance's references on the skin's textures (7) |
| Cached renderers cleared | `ParkOrDestroyView`, `DestroyDonkey`, `OnViewDestroyedByGame` (where `bodySprite`/`donkeyBodyRenderer` are nulled) | the rig clears its own renderer references |

`DonkeyPrefab.ModObjects` (DonkeyPrefab.cs:226) gets the overlay names, so a leftover is flagged and destroyed, not
merely logged; the pooled-parts.md table gets a row. `ResetSprites` walks the hierarchy but only touches nodes in the
prefab baseline (DonkeyPrefab.cs:106-129), so an overlay child is skipped. The LOD freeze collects renderers with
`GetComponentsInChildren` (Tiers.cs:484), so an overlay is frozen and restored with the rest: harmless, and `CheckNow`
runs after the restore.

Free carts (`!HasDonkey`): the rig does nothing; the donkey is hidden by `HideDonkeyOnly` already. Foals: the same rig
on the same prefab; the root scale (0.6) is inherited by the overlay as a child; a foal wears the skin only when the
skin's mount has foals (`[Mount] foals`) and the instance's `FoalsWearSkin` is on (a skin has no foals flag of its
own: the mount is the creature, the skin its drawing).

### 5.2 The per-frame apply

The Animator sets the frame's sprite before any `LateUpdate`, so the **choice** of picture can be made in the plugin's
LateUpdate tick (where `ColorsRig.Tick` runs). But the game's `AnimationComponent.LateUpdate` switches `hed.enabled`
during walks, and its order against the plugin's LateUpdate is undefined (Mount.cs:2774-2776): mirroring `enabled` or
the sprite from LateUpdate would be one frame late at every walk start and stop. So the **apply** happens in one
`Camera.onPreCull` handler owned by the registry: deduplicated by `Time.frameCount` on the first camera of the frame,
whichever camera that is, then for every attached rig:

1. read the part's current sprite; key it (3); look the picture up in the rig's resolved frame table (a dictionary
   from the game sprite instance to our sprite, filled on first sight, so steady state is one dictionary lookup per
   part per frame);
2. set the overlay's sprite; copy `sharedMaterial` (Colors' instance, so the palette recolour applies), the property
   block (the game feeds per-renderer data through one), sorting layer and order, `flipX`/`flipY`, `color`, and
   `enabled` from the part; place the overlay at the part's sub-pixel trim offset (`textureRectOffset`, as the probe
   and Clerk Desk do);
3. for a `replace` layer with a picture for this frame: `part.forceRenderingOff = true`; without one (or for `over`):
   leave the part as it is.

**PreviewStage's cameras are ignored by the handler** (they render to their own `RenderTexture`: matched on
`targetTexture`), and the paper rig is applied explicitly in `Pose`. `PreviewStage.Tick` calls `Camera.Render()`
inside the plugin's LateUpdate; if Unity raised `onPreCull` for it and the handler counted it, that would be the
"first camera of the frame", the world apply would run mid-LateUpdate (before the game's `AnimationComponent.LateUpdate`
may have switched `hed.enabled`) and the real cameras' `onPreCull` would be skipped by the dedupe: the one-frame-late
head, exactly while the look bar is open. Ignoring those cameras removes the dependency on what manual renders raise.

Interplay with the API's own hides: `RevealDonkeyIfDue` clears `forceRenderingOff` on all hidden renderers one frame
after `PrepareParts` (Mount.cs:81); the apply runs later in the same frame, so there is no flash. The hide is
re-asserted every frame anyway, which makes every other clearing (wake, the game's re-skin) harmless.

### 5.3 The overlay renderers

- One `SpriteRenderer` child per part: `gk2_skin_bdy` under `bdy`, `gk2_skin_hed` under `hed`, same layer as the part
  (20, `CustomLightingObject`). As children they inherit the `Base` mirror (scale x −1), the `gfx` stretch (1.667)
  and a foal's root scale, so nothing is computed per facing.
- **Depth:** the shader writes depth, so two sprites on one plane fight. The `replace` overlay sits on the part's own
  plane (local z 0: the part is hidden). Each `over` layer steps a little toward the camera (local z −0.005 × its
  index). `mount_rider_legs` already uses −0.01 (RiderLeg.cs:127); the steps stay inside that.
- **Pivot and canvas:** the overlay sprite is created with the skin's pivot at the donkey's anchor, so a 128×128
  picture is centred on the same point as a 96×96 one.
- The overlays carry no colliders, no scripts, and are never saved: they only exist while the view does.
- **The shadow.** `gfx (90 rotated)/-bdy` is a shadow caster drawn from the donkey's silhouette, so a rat casts a
  donkey-shaped shadow. A decision for step 1 (16): leave it, or a third overlay on `-bdy` with the same picture
  (`-bdy` is in `DonkeyOnlyNames`, so the hide path knows it already).

### 5.4 What stays the donkey's

The rider's seat, bob, leg, the cart's shafts, the lantern, the cargo and the sounds all keep working from the
donkey's own sprites, transforms and meshes, untouched. A skin of another shape may look off there until phase 2 adds
per-skin offsets (14).

## 6. Choosing a skin, and keeping the choice

- **Keys** (a new `[Look]` section of the API's cfg, both `IsLook`):
  - `Skin` (`Tuned<string>`, default `""` = the donkey): the skin id this instance wears;
  - `FoalsWearSkin` (`Tuned<bool>`, default true): whether this instance, as a foal, wears its parent's skin.
    Alternative at build time: a companion-only setting that sets or clears `Skin` on the foals, one key fewer in
    the side file; the rig doesn't care which.
- **Per instance:** `handle.Tuning.Set(ownerId, "Skin", "rat")`, the same call as every colour. The override is
  saved as `set Skin <owner> rat` and restored on load; `Changed()` marks the rig dirty.
- **The companion** (`MountCompanion`): a `Skin` property and `FoalsWearSkin`, applied like `Coat`: to the donkey,
  to the foals when allowed, and to the paper donkey for the picture; re-applied at `OnGameReady`, after spawns and
  on hitch/unhitch, exactly where `ApplyLook` runs today (MountCompanion.cs:640-686). For the companion's donkey the
  side file isn't even needed: the look is re-applied from Saddle Up's cfg on every load.
- **A skin that isn't there** (its pack removed, a typo in the cfg): the rig draws the donkey and logs once; the
  stored id is **kept**, so the skin returns when its pack does. The settings row shows "Donkey" for an unknown id
  without writing anything.
- **Older versions** of the API skip the `set Skin` line with a warning and drop it on their next save, which is the
  right outcome for a version that can't draw it. The side-file format number stays 2 (an added key is not a format
  change, Persistence.cs:491-499).
- **Who may set it:** any mod, like any tuning key (the setter is recorded as the owner). Saddle Up's row is the
  player's way; another mod can dress its own kept donkeys.

## 7. Loading, memory, performance

- **Loaded on use, freed when unused.** A skin's pictures are decoded when the first instance resolves it and
  destroyed when the last instance releases it: a refcounted `SkinTextures` cache keyed by `<skin id>/<frame key>`,
  built as `Colors.SharedTextures` is (Colors.cs:32-73), counted with `Live.TextureMade/Gone`, so the Leaks self-test
  covers it. Installed skins cost nothing until worn.
- **Per skin in memory (built 2026-10-17 evening): one sheet texture per skin.** Every source (PNGs per frame, a
  sheet PNG, a recipe) is decoded into one `Texture2D` with a cell per frame and a one-pixel gutter (44 frames of
  96×96: 7×7 cells, 680×680, 1.8 MB) and the sprites are cut from it: one texture object per skin instead of 44
  (`Live.Textures` counts one). The win is memory and object count; draw calls are what they were (each overlay
  carries the part's property block, which the built-in pipeline doesn't batch). The frames are decoded `BuildPerTick`
  (6) a tick for every source, not only recipes, so no first wear stalls a frame, and the skin is worn once the sheet
  is whole (`Loaded.Ready`). A code source's sprites are the mod's own (any texture, their sheet if they like).
  Until then the previous look stays, which on a new view is the game's donkey: the first call of a session showed
  it for the build's ~29 frames (the dragon's 173 pictures; the user, 2026-10-23). So the companion's chosen skin is
  built ahead and held (`Looks.WarmWanted`, set in `MountCompanion.ApplyLook`; `Looks.TickWarm` each registry tick,
  one skin at a time, the old one released on a new choice): the first spawn wears it at once.
- **Per skinned view per frame:** one dictionary lookup per part, a few property copies, one bool. Measured target:
  under 0.01 ms per view (the perf runner's census and suites, docs/performance-hog.md); the budget for a herd of 60
  skinned donkeys and foals is under 0.5 ms a frame.
- **Timed parts:** `Perf.S.Looks` (the apply) and `Perf.S.LooksBuild` (decoding and recipes), like `Colors` and
  `ColorBuild`.
- **No garbage in steady state:** the frame table is filled on first sight of a sprite and never rebuilt; the apply
  allocates nothing.
- **The log budget:** attach lines share the 4-lines-per-second view budget (Colors.cs:593); validation prints one
  block per skin per session.

## 8. The public API (sketch; final names at build time)

Pattern (b) of the API (a sealed area reached through `Mounts`), plus tuning keys:

```csharp
public static class Mounts { public static MountLooks Looks { get; } }

public sealed class MountLooks
{
    public IReadOnlyList<SkinInfo> Skins { get; }                       // every registered skin, the donkey first
    public SkinInfo Find(string id);
    public IDisposable Register(string ownerId, SkinDef def);          // code, folder or recipe sources; disposing unregisters
    public int RegisterFolder(string ownerId, string folder);          // every gk2skins/*/skin.cfg below it
    public int RegisterEmbedded(string ownerId, Assembly asm, string prefix);
    public IDisposable RegisterRecipe(string ownerId, string id, string name, Func<SkinFrame, Color32[]> pixels, SkinOptions o = null);
    public SkinReport Validate(string id);                             // what the loader found: missing, unknown, wrong size
    public IReadOnlyList<string> FrameKeys { get; }                    // the donkey's frame keys (the paint-over list)
    public event Action SkinsChanged;
}

public sealed class SkinDef { Id, Name, Author, Version, Base, Mount, Layer, Foals, Canvas, Pivot, ISkinSource Source }
public sealed class SkinInfo { Id, Name, Author, Version, Mount, Layer, Foals, Canvas, Owner, IsBuiltIn }
public interface ISkinSource { IEnumerable<string> Keys { get; } bool TryGetFrame(string key, out Texture2D tex); }
public sealed class SkinFrame { string Key; int Width, Height; Color32[] Pixels; int BackRow; ... }   // for recipes
// Per instance: handle.Tuning.Set(owner, "Skin", id) and "FoalsWearSkin" (existing InstanceTuning)
// Companion: MountCompanion.Skin (string), MountCompanion.FoalsWearSkin (bool)
// Settings: MountSettingsPage.List(section, key, value, Func<IReadOnlyList<(string id, string label)>> choices, label, description, apply, look: true)
```

Rules that apply: every mutating call takes the owner id (the plugin GUID) and throws without one; registering an id
that exists throws for a different owner and replaces for the same owner; `Register` returns a handle so a mod can
withdraw; `Mounts.ClearTuning(owner)` clears skins that owner set on instances, as for colours. API version 0.30.0
(additions: minor); `tests/ApiSurface` snapshot updated with the diff reviewed.

## 9. The settings row

A new row mount, **List**, binding a `ConfigEntry<string>`: **the id is the stored value from every UI**, the label is
only shown. The framework binds the same section and key (FrameworkLink.cs:137-138), so the Mods menu and the Settings
window must write the same representation, or one key ends up holding labels from one menu and ids from the other.
- **Mods menu:** `Gk2Settings.AddDropdown(section, key, value, string[] choices, name, description, order)` with the
  **ids** as the choices; `MenuLabels` rewrites the shown option texts by position, as it does for enum rows today
  (MenuLabels.cs:33-46, 78-103; it works for any list).
- **Never bind the entry with an `AcceptableValueList<string>`:** BepInEx clamps an unlisted value to the default on
  load, which would overwrite a stored id the moment its pack is absent. The row's `apply` validates instead.
- **Settings window and look bar:** `SettingsRows.MakeRow` already takes any `Fields`/`Values` arrays (SettingsRows.cs:479-486).
- **Timing.** The list is read when the row is materialised, through the `Func` (not at `Publish`). Folder packs are
  scanned in the API's `Awake`, which runs before every dependent mod's, so **packs are always listed**. A mod that
  registers skins in its own `Awake` may load after Saddle Up's `Awake`; such a skin works at once through the cfg
  (the id is valid the moment the rig resolves it) and appears in the dropdown after a restart. Deferring the pages'
  materialisation to the API's first frame would list them too, but it changes the timing of every existing page
  close to release: a separate, verified change if it ever matters, not part of this feature.
- **A missing skin:** the row shows "Donkey" and leaves the entry alone (6).
- **Fallback if the dropdown can't be made late:** a fixed enum of Saddle Up's shipped skins plus `Custom` with the
  id in a text key, the pattern `CoatColor.Custom` + `CustomCoat` uses today. The API design doesn't depend on which.

## 10. Validation, errors, logging

On registration (folder and embedded) and on first use (code and recipes), the loader builds a `SkinReport`:
- refused: a `format` newer than known, a missing or invalid `id`, an unknown `base`, pictures of the wrong size;
- warned: unknown `skin.cfg` keys, names that match no frame key (typos), `hed_walk_*` names (double head), frames the
  skin doesn't cover (the to-do list, as the probe's `donkey-frames-used.txt`);
- the report is logged once as one block, and `Validate(id)` returns it to tools and the dev panel.

At run time: any exception in a rig's attach, apply or detach detaches that rig and logs once (Colors' rule,
Colors.cs:721-725); the donkey shows; nothing else is affected. A source that throws is treated as "no picture". No
skin code runs on any path that touches items, the cart, cargo or the save: a skin can only fail by not showing.

## 11. Game updates

- **Code:** the rig touches Unity types and prefab part names, not game members, so it has no game-code.txt line of
  its own (a listed class that uses no game member fails CecilScan as stale). Its freeze follows `Colors.Frozen`,
  which guards the same borrowed parts; a comment in the rig says so. If the rig ever needs a game member (for example
  `CameraSystem`), it gets its own `frozen` line and `Freezers` entry.
- **Art:** `DepManifest` covers members, not sprite names. On the first donkey view of a session the registry
  enumerates the base's sprites (the probe's `ExportAllFrames` method: the character atlases' sprites with the
  donkey's preset prefix) and compares the key set with the shipped list; a difference is logged as a warning naming
  the new and missing keys, and every skin still works for the keys that match. The shipped list is updated in the
  game-update audit (docs/game-update-*.md).
- **The game's preset prefix** (`0301_`) is stripped generically, so a renumbered preset changes nothing.

## 12. Extensibility points, by name

| Want | How, without touching the rest |
|---|---|
| A new skin from an artist | a folder under `gk2skins`, no code, no DLL |
| A skin from another mod | `Register`/`RegisterEmbedded` with its own owner id; withdrawn by disposing |
| A new kind of source (a generator, a web pack, an animated texture) | implement `IMountSkinSource` (a `Sprite` per key, from any texture); inside the API, a `SkinDef` source is one of: PNG per key, one sheet PNG, sprites, a recipe (`Loaded.Produce` feeds the sheet) |
| A new recipe | `RegisterRecipe` with a pixel function; the helpers in `SkinFrame` |
| Several layers (a saddle on a rat) | `Layer.Over` skins; the rig keeps a list; the UI gains rows later. **Built 2026-10-17 evening: the front layer** (`front/` pictures, `sheet_front`, `FrontSource`): a third overlay per part sorted on the keeper's layer two above his body while he rides (the leg overlay is +1), else one above the part; the dragon's near wing lives there, so the rider sits between the wings |
| Another mount | `Looks.SkinBase` (built 2026-10-17 evening): the frame keys, the view's part names, canvas, pivot, pixels per unit, the game's sprite prefix and atlas, the back line; the donkey is the one record, `skin.cfg` `base = donkey`, and a skin whose base isn't the view's isn't worn. A second creature is a second record and nothing else changes (validation, the sheet, the recipes' read-back and the rig all read the base) |
| Attachments (Hellfire, glowing eyes, a hat as an object) | built as the **effects system** (Effects.cs, `Mounts.Effects`, api.md "Effects"): an effect is parts of particle sources with emitters (ring, body, trail), its own `Show` rule, spawned on a holder that follows the body point, on the rig lifecycle (tick / detach / release); Hellfire is the first and is defined through the public API itself. Objects that aren't particles (a hat) would be a second source mount on the same def |
| Per-skin rider and cart offsets (built 2026-10-17 evening) | `skin.cfg` `[Tuning]` / `MountSkinDef.Tuning`: existing `MountTuning` keys applied as instance overrides owned by `Looks.TuningOwner` while worn (a key another mod set on that donkey is left to it), cleared on removal, skipped by the side file; `offset` / `offset_cart` move the overlay in art px (the rat closer to the shafts is `offset_cart`). No new seat code |
| Skin sounds (phase 2) | `sounds/` in the folder, played through the game's mixer (`sfx_inworld`), mapped onto the donkey's sound events |
| A skin picker with pictures | `PreviewStage` already renders the paper donkey; a per-skin thumbnail is the same render with the skin set |
| Dev workflow | the panel's Looks tab: reload folders, show the report, export the frame keys and templates |

## 13. Safety

- Only the API ever changes a pooled part, and only in the ways the audit knows: `forceRenderingOff` and named
  children. Both are undone in `ReleaseView` before the audit, and re-asserted per frame, so a leftover is impossible
  by construction and caught by the audit if it happened.
- Skins never touch `WgoData`, the game's save, cargo, chests or the side file's item lines.
- A stopped API (game-update verdict) or a frozen Colors leaves every donkey as the game draws it.

## 14. Tests

- **Self-tests** (a `Skins` group in `SelfTest.Catalog`): attach and detach leave the view as the prefab (audit
  silent); spawn 80 skinned donkeys and remove them twice, `Live.Textures` back to the start (the Leaks pattern); a
  parked view handed to a foal carries no skin; hitch and unhitch keep the skin on the new view with no `[pool]`
  line; an unknown id draws the donkey and keeps the id; a recipe builds over frames and switches atomically; the
  paper donkey renders the skin (PreviewStage picture pixels differ from the plain donkey's).
- **Scans:** ApiSurface updated, CecilScan clean (no stale class), DefaultsScan (the `[Look]` keys are `IsLook`),
  check-dist (embedded PNG bytes against the drive-path regex: run and see).
- **Perf:** the runner's suites with a skinned donkey and four skinned foals, census flat across three suites.
- **By hand:** the first-run check with a skin pack dropped into `plugins`, day and night, the Mods menu and the
  Settings window rows, a pack removed between sessions.

## 15. Build order (each step runs in game before the next)

1. The rig skeleton: attach/detach/per-frame apply with one embedded test skin (the probe's zebra), the audit entry,
   the `Skin` key, the companion property. Hand test: hitch, unhitch, doors, park, foals, no `[pool]` lines.
2. The loader: folder packs, `skin.cfg`, validation, the refcounted cache, the Leaks self-test extended.
3. The settings row (List) in the Mods menu, the Settings window and the look bar; the picture via the paper donkey;
   the missing-skin behaviour.
4. Recipes: the read-back source, the frame helpers, the atomic switch, the first built-in set (zebra, ghost, ember,
   golden, shadow, dragon). Our own creatures (worm, snail, rat) as embedded picture skins.
5. Other mods: `Register`, the handle, `SkinsChanged`, the example mod under `examples/`.
6. Hellfire as the first effect of the particle effects system (`Mounts.Effects`; api.md "Effects"). Built.
7. Docs (api.md, reference.md, a `docs/skins.md` for artists with the templates), the changelog, the version bump.

## 16. To verify before or during step 1

- The overlays are made in `PrepareParts`, after `HideRenderersNow` ran at spawn: whether `HideDonkeyForAFrame`
  (Mount.cs:1273) enumerates the renderers at call time (then the overlays are hidden with the rest) or reuses the
  list from the spawn (then an overlay would draw for one frame on its own). If the latter, `Attach` calls
  `HideWithDonkeyWhilePending(r)` (Mount.cs:72, what LampHang uses for late-made renderers).
- The shadow caster decision (5.3).
- The framework accepts `RegisterMod` after the chainloader (for the late page), and `AddDropdown` returns the label
  or the index; which `ConfigEntry<string>` value it stores.
- `check-dist.py`'s drive-path regex against embedded PNG bytes.
- The per-frame cost with 60 skinned views (the perf runner).

## 17. Decisions this design makes

- Overlay children, never sprite replacement (the rider math and HeadWatch read the originals).
- `forceRenderingOff` re-asserted per frame as the hide; never `enabled`.
- The apply in `Camera.onPreCull`, deduplicated per frame; the choice could be made in LateUpdate but the apply can't.
- The skin choice is a `Tuned<string>` instance override, so persistence, the paper donkey and `ClearTuning` come free.
- Skins load on use and free when unused; recipes build over frames and switch atomically.
- Folder packs are data files by design, overriding the 2026-09-30 "no data files" rule for this one read-only case.
- The freeze follows `Colors.Frozen`; the art check is a key-set comparison at the first view.
- The settings row stores skin ids, never labels; no `AcceptableValueList`.
- The per-frame handler ignores PreviewStage's cameras; the paper donkey is applied in `Pose`.
- No change to when the framework pages are made: packs are always listed, late DLL skins after a restart.
