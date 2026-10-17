# Art from outside the game (investigation, 2026-10-16)

Everything the mods draw so far is the game's own art: the donkey, cart and lamp are game parts, and the rider's leg
and back are cut from the keeper's frames. The question here is whether a mod can bring its own art. Three examples:

1. Ride something else, say a giant rat.
2. Flames drawn around the donkey.
3. Fiery footsteps left behind it.

## Short answer

**Yes, technically, for all three, and without a Unity editor.** The engine parts are in the game, and we already do
the core of it. What isn't known yet is how good it looks (lighting), and what a new animal costs. The probe below
answers both.

## What is known without running the game

- **Engine.** Unity 6000.3.9f1, built-in render pipeline (no URP/HDRP in the build), Direct3D 11 and gamma colour space
  (probe step 2; the `D3D12` folder is not what it runs on). The modules a mod needs are all shipped in `GraveyardKeeper2_Data\Managed`:
  - `ImageConversionModule`: PNG to texture at runtime.
  - `ParticleSystemModule`: particle effects built in code.
  - `AssetBundleModule`: loading Unity asset bundles.
  - `AnimationModule`.
  - The game itself loads its art through Addressables (25,269 asset bundles in `StreamingAssets\aa`).
- **We already draw art the game doesn't have.** The rider's leg fallback (`RiderLeg.DrawnLeg`) is a texture
  built pixel by pixel in code, made into a sprite with `Sprite.Create` and drawn in the world. A PNG is the same thing
  with the pixels read from a file (`Texture2D.LoadImage`). So pictures from outside work; that part isn't a question.
- **How the donkey is drawn** (PROBE-FINDINGS.md):
  - **Parts:** two sprites, `bdy` (body) and `hed` (head), under an Animator (`CharacterNPC_Donkey`).
  - **Facings:** only left and right; right is the mirror.
  - **Clips:** `static`, `walk` (0.97 s), `graze`, `ear`, `lips` and the head turns.
  - **Shader:** `Custom/3D Sprite` on layer 20 (`CustomLightingObject`).
  - **The shader reads more than the picture:** the sprites carry secondary textures (we copy them in
    `RiderLeg.CreateSlice`), and the colours go through a palette and a LUT (Colors.cs).
  - **The open part:** what those extra textures hold (light masks, palette indices) is what decides whether
    outside art lights like the game's at night.
- **The game's own effects** are prefabs loaded by name (`WorldFX.Spawn(position, "puff_npc")` loads
  `puff_npc.prefab` through Addressables, pooled by `WorldFXPool`). Its start list
  (`WorldFXPoolInitialSizeConfig`) names every effect it uses. If the game already has a fire or ember effect,
  flames around the donkey need no outside art at all.
- **Searching the catalogue file offline doesn't work.** `catalog.bin` packs its keys, so a text search found only
  106 paths out of tens of thousands. The real list has to come from the running game (probe step 1).

## What another mod already does: Clerk Desk 1.0.9

Clerk Desk (another author's mod, Nexus 276) ships its own art. We read its zip (2026-10-17) to learn the technique
only; its art is the author's, and none of it is reused.

- **How the art travels.** 388 PNGs embedded in the DLL (`GetManifestResourceStream` → `ImageConversion.LoadImage` →
  `Sprite.Create`), with no loose files and no asset bundle. That is the same path as our probe; the DLL is 643 KB
  with all of it.
- **A costume over a game character** (`Costume.cs`):
  - **What is drawn.** The clerk zombie wears a suit and hat drawn by the author. For each of the zombie's game
    sprites (`bdy`, `bdy_over`, `bdy_ovr_hor`, `hed`), the mod adds a child `SpriteRenderer` with the **game
    renderer's own material** (`sharedMaterial`, no extra textures), the same layer, sorting and flip.
  - **Picking the frame.** Each frame, the overlay's picture is chosen from the game sprite's name: a game frame
    `0301_bdy_walk_left_01` maps to the PNG `bdy_walk_left_01.png`.
  - **Timing.** It runs in `Camera.onPreCull` for the main camera, so after the Animator and every LateUpdate: it
    can't be a frame behind.
  - **Alignment.** Each PNG is the same 96×96 canvas as the game frame (pivot centre, 50 pixels per unit), and the
    mod copies the game sprite's sub-pixel trim offset.
  - **The size of the job.** 388 frames for one costume on a character with four facings and many actions.
  - **Undoing it.** The overlays are switched off when the zombie changes; there is no undo beyond that.
- **The colours are ordinary colours.** The costume and the desk are plain RGBA with 4 to 17 colours each, in the
  game's palette style, not the palette-index colours our rider's leg uses. Drawn with the game's sprite material
  and no extra textures, they still pass for game art (the mod is released). That is our probe's picture B, so B is
  likely to look right; the probe confirms it for the donkey's material and at night.
- **A 3D object in the game's own style** (`DeskLook.cs`): the desk replaces a building's look.
  - **The model.** Boxes and flat quads from a small text file, with UVs in the game's projection (u = 50·x, v =
    30·y + 40·z, in pixels on a 96×96 texture), so one drawn picture wraps the boxes the way the game's buildings do.
  - **Drawing.** A `MeshRenderer` with the hidden building's own material and an `Object3DMesh` component
    (`GetSubMesh(0).SetTexture`, `CustomInit`, added to the `Object3D`'s mesh list), so the game lights and sorts it
    as its own.
  - **Shadow.** The building's `_sh` shadow sprite swapped for a drawn one.
  - **Undoing it.** Everything is put back by `Revert()`.

  That is the route for a solid object of our own (a different cart, saddlebags, a coffin on the cart), and it is the
  same `Object3DMesh` system our Colors feature already works with.

**What it changes for us:**
- A new animal or a costume works best the Clerk Desk way: an overlay child per donkey sprite, keyed by the game frame's
  name, set in `Camera.onPreCull`. The real `bdy`/`hed` are either hidden or left under the overlay.
- On our pooled parts, the overlay child and any hidden renderer still need their undo in `ReleaseView` and their place
  in `DonkeyPrefab`'s audit.
- The art bill is one PNG per distinct donkey frame on the same canvas (probe step 3 counts them).

## What each example would take

| Example | Possible? | How | Open questions (probe step) |
|---|---|---|---|
| Fiery steps | yes | Small sprites or a particle burst spawned behind the donkey every step, fading out; the art is one small texture, or a game effect. | Which shader (2), how it looks at night (6). |
| Flames around the donkey | yes | A `ParticleSystem` built in code that follows the donkey, with a flame texture; or a game effect, if one fits. | The same, plus whether the game has a fire effect (1, 7). |
| Ride a giant rat | yes, but costly | Each frame, after the donkey's Animator has run, swap the `bdy`/`hed` sprites for the rat's frame that matches the clip and frame (the rider's leg already follows the keeper this way). | Frames to draw (3), whether outside art lights right (4), whether the game already has a rat (1). |

**The rat in more detail.** Swapping the pictures is the easy part. Everything else is tuned to the donkey's shape,
in pixels:
- the rider's seat, the leg and back cuts
- the cart hitch and the lamp hang
- the foals
- the shadow caster
- the Colors feature (it recolours the donkey's palette)

The swap itself also changes pooled game parts: the `bdy`/`hed` renderers are reused by the game's delivery donkey.
So, like every view change (docs/pooled-parts.md), it needs its undo in `ReleaseView`, and `DonkeyPrefab`'s audit
has to know it.

A re-skin with the donkey's outline (a "donkey in armour", a zebra) keeps all of that. A differently shaped animal is
a re-tuning project on top of the art. The art bill per animal is the number of distinct frames the donkey uses
(probe step 3), drawn twice if left and right are to differ.

**Asset bundles aren't needed for any of the three.** Textures, sprites, materials and particle systems can all be
made in code. A bundle would only be needed for:
- a shader of our own
- Animator controllers or clips made in the editor (sprite keyframe clips can't be made at runtime: `AnimationUtility`
  is editor-only)

Building bundles would mean installing Unity 6000.3.9f1. Whether it must be that exact version is not verified.

**How art would ship.** Inside the DLL as embedded resources, as `defaults.cfg` already is (the probe does this
with its two test pictures), not as loose files: a missing file then can't break anything. Any art we ship must be our
own or properly licensed.

## The probe: tools/GK2AssetProbe (dev only, never shipped)

**Opening it.** Shift+F7 opens its window. The answers go to the log under `[assetprobe]` and to files in
`BepInEx\GK2-asset-probe\`.

**What it never does.** It only reads the donkey. Everything it draws is its own object beside the donkey, removed by
"Clear all" or a scene change. It doesn't save.

**Testing your own art.** Put your own `card.png` or `flame.png` in that folder and they replace the test pictures
built into the DLL.

**The steps.** Stand next to the donkey, or ride it:
1. **Dump the game's asset names.**
   - Writes every Addressables name to `addressable-keys.txt`, and the effect names to `effects.txt`.
   - Logs the names that match fire, smoke, step, rat, horse and similar words.
2. **Shaders a mod can use.**
   - Logs which shaders `Shader.Find` finds, and which shader the game's own smoke puff uses.
   - Writes every loaded shader to `shaders-loaded.txt`.
3. **Donkey art spec.**
   - Logs the sprites' sizes, pixels per unit, extra textures and the shader's properties.
   - Saves the frame and its extra textures as PNGs, so we can see what a mask holds.
   - Then samples for 30 s which pictures each clip uses, on every animator layer (the head has its own): stand,
     walk, ride and stop during those 30 s.
   - Logs whether the game feeds the renderer data of its own (a property block); if so, step 4 copies it.
4. **Test pictures beside the donkey.** Five, left to right:
   - 0: a control, the donkey's own frame drawn through the probe's own sprite and material. It must look like the
     donkey; if it doesn't, the test setup is wrong.
   - A: the test card with a plain sprite shader.
   - B: the card with the donkey's shader. Read B against step 3's property list: a sprite without its own extra
     textures falls back to the material's own texture slots (the donkey's), sampled at the card's coordinates, so B
     can look wrong because of that, not because of the shader.
   - C: B plus white extra textures.
   - D: B plus black extra textures.

   Look at them by day and at night (the showcase director's time hold, Shift+F9), and near the lamp.
5. **Flames around the donkey** (on/off): a particle ring that follows the donkey.
6. **Fiery steps** (on/off): small flames left behind every step. "Next shader" switches the shader of 5 and 6.
7. **Game effect:** type an effect name from `effects.txt` and play it at the donkey (checked against the names
   first, so a wrong name logs nothing but our line).
8. **The game's own flame materials** (added after the first run):
   - Loads `flame_fx`, `Fire_1_fx`, `Fire_2_fx`, the two `techpoint drop fire fx`, the two sparks materials and
     `Glow`, and adds them to "Next shader" for 5 and 6, each with its own texture.
   - If a game particle system drawn with one of them is in memory, its frame grid (texture sheet animation) is
     copied too; the log says which systems use each material, or none.
   - The steps are now one particle per step (a sprite would force our own texture), and the flames and steps are
     sorted with the world instead of drawn on top.

### The game's own lamp fire (buttons 12-13, added after the second run)

The second run showed the lamps' `flame_fx` and `sparks_fx` as the best match, but drawn by our particles at about a
third of their size and life. So 12 and 13 copy the game's own lamp fire whole:
- **What is copied:** the particle systems of a lamp's `light_fx/flame` and `light_fx/sparks`, with all their
  settings.
- **How it is copied:** made under an inactive holder, so nothing of theirs wakes up, and stripped of every other
  component: a lamp may carry the game's own scripts or a light. The log lists the settings and what was removed.

The buttons:
- **12. Lamp fire around the donkey:** six flames (and three sparks) on a ring round the donkey, following it.
- **13. Lamp fire steps:** a short burst of the lamp flame at every step, left to burn out.
- **Lamp fire size:** 1x (the lamps' own), 0.75x, 0.5x, 1.5x.

They need a lamp flame in memory: a lit lamp nearby, or the cart's lantern lit.

### Testing your own art (buttons 9-11)

Everything is read from `BepInEx\GK2-asset-probe\test\`, again on each press, so a file can be changed while the
game runs and tried again.

- **9. Your pictures.** Every PNG in `test\` is shown in a row beside the donkey, drawn with the donkey's material (lit
  like the game, recoloured by Colors if it uses the donkey's colours). 50 pixels = 1 unit, as the donkey; the game
  stretches upright pictures 1.67 times in height, the same as its own art.
- **10. Dressing the donkey.** PNGs in `test\donkey\` are drawn on the ridden donkey frame by frame (Clerk Desk's
  method).
  - **Templates.** Step 3 saves every donkey frame it sees, whole on its 96×96 canvas, to `donkey-frames\` (for
    example `bdy_walk_left_03.png`). Paint over a copy, keep the name, put it in `test\donkey\`. Only left-facing
    frames exist: the right is the mirror.
  - **Which picture is used.** For each frame the tester looks for `<frame>.png`, then `<clip>.png` (for example
    `bdy_walk_left.png`, one picture for the whole walk), then `bdy.png` / `hed.png` (one picture for everything: the
    quickest test).
  - **The modes** (each press moves on): off, **over** the donkey, **instead of** the donkey.
    - "Instead" hides the donkey's own picture, but only for frames you have drawn: a missing frame shows the donkey,
      and the log names it once.
    - The hiding is put back when dressing stops, the scene changes, the donkey is more than 20 units away, or on
      "Clear all". The API's pool audit would also catch and log a leftover.
  - **Reloading.** "Reload my donkey pictures" reads the folder again.
  - **When dressing stops,** `donkey-frames-used.txt` lists every frame shown and the ones without a picture: the
    to-do list for a complete animal.
- **11. Your sounds.** `.ogg` and `.wav` files in `test\` play one after the other, as a plain 2D sound: the game's
  volume settings don't apply to them. The log gives the length, channels and rate.

## Results (2026-10-17, first run)

Run in Inquisition Base by day: the log at 00:05, a screenshot of step 4 and a 61 s video of steps 5 and 6. There
were no warnings or errors in the whole log. Night and the lamp were not looked at separately.

**1. What the game has.**
- **Names.** 43,194 Addressables names, in two locators.
- **Effects.**
  - The effect pool's start list has five: `char_hit_ally`, `char_hit_zombie`, `puff`, `tree_puff_test`,
    `zombie_death`.
  - Of the 548 bare prefab names, most are broken-building stages; the effects among them are the `puff`
    variants, `buff_char_*` and `buff_hud_*` in ten colours, `char_hit*`, `harvest_*`, `zombie_blood`,
    `zombie_projectile_explosion`, `red_eye_fx`, `sermon`, `Wisp`, `QuestVFX`, `check_point_vfx_*` and a few more.
  - **There is no fire effect to spawn.**
  - `puff_npc`, `zombie_death` and `char_hit_ally` played at the donkey without a problem.
- **The game's own flame materials** are loadable by name, textures included:
  - `Assets/AddressableAssets/Materials/flame_fx.mat`
  - `Materials/FX/Fire_1_fx (MultiplyDouble).mat` and `Fire_2_fx (AlphaBlendet).mat`
  - `sparks_fx.mat`, `Sparks_fx (MultiplyDouble).mat`
  - `techpoint drop fire fx.mat`
  - `Glow.mat`, `mat_part_wisp_trail.mat`, `mat_part_smoke.mat`

  Flames drawn with these would be the game's own fire art on our particle system: not tried yet.
- **Animals.** No rat or horse. There are `cow_test` prefabs (test leftovers) and two cats
  (`_WorldAssets/Common/Animals/Cats`). A rat would be our own art.

**2. Shaders.**
- **Found by name:** `Sprites/Default`, `Legacy Shaders/Particles/Alpha Blended`, `Particles/Standard Unlit`,
  `Unlit/Texture`, `UI/Default` and `Standard`. 127 shaders are loaded (`shaders-loaded.txt`).
- **Not found by name:** `Custom/3D Sprite`, the donkey's shader. A mod gets it by taking the material from a game
  renderer, as Clerk Desk does and as our Colors already does.
- **The smoke puff** is drawn with `mat_part_lit`, shader `Sprites/Diffuse + Fog (Custom Light)`: the game's lit
  particle material.

**3. The donkey's art.**
- **Frames.** `bdy` and `hed` are 96×96 frames at 50 pixels per unit, the same canvas as Clerk Desk's zombies,
  point-filtered, packed into 2048×2048 atlases.
- **No extra textures.** The sprites have none, so a picture from outside needs nothing beyond its own PNG.
- **Per-renderer data.** Each renderer carries a property block.
- **The material.** `Custom/3D Sprite` with the keyword `USE_PALETTE_COLOR_REPLACE`. The `_Palette` texture is
  ours (Colors); `_UseLUTColorReplace` is off.
- **Facings.** Only left-facing pictures exist: right is the same pictures mirrored (the `Base` scale x is -1).
- **The frames counted in the 30 s** (stand, walk, ride, stop):

  | Clip | Pictures |
  |---|---|
  | `walk_left`/`right` | 8 body (the head is in the body) |
  | `ear_left`/`right` | 5 body |
  | `static_left`/`right` | 1 body + 1 head |

  That is 15 pictures for an animal that walks, stands and twitches an ear. `graze`, `lips` and the head turns
  weren't seen in the 30 s (each is a few more), and the clips list also shows the keeper-style controller's shared
  clips, which the donkey never plays. **An animal is roughly 15-30 pictures, drawn once (left only)**, against
  Clerk Desk's 388 for a four-facing character.

**4. Test pictures (screenshot).**
- **Only three were drawn** (0, A, B): with no extra textures, C and D have nothing to test.
- **0, the control,** looks like the donkey, colours included, so the setup is right.
- **A** (`Sprites/Default`) shows the card's colours flat and bright: unlit, it stands out against the scene.
- **B** (the donkey's material) is warmer and slightly darker, lit like the scene around it.
  - The white band turned pale pink. That is either the scene's light or our palette swap catching a colour: to
    tell, look at B with Colors off.
  - So outside art drawn with the game's material looks like game art. It also means the palette swap applies to
    it, which is useful: a rat drawn in the donkey's harness colours would take the Colors settings for free.
- **The labels were drawn in the wrong place** (bottom right, not above the pictures): the probe's label maths
  uses the screen size, and the game's camera draws at a different size. That's a probe bug, not a game finding.

**5 and 6. Flames and steps (video).**
- **Both work** while riding, and the steps fade as meant.
- **By shader:**
  - The game's lit particle material (the default) and `Sprites/Default` look most at home.
  - `Particles/Standard Unlit` and `Alpha Blended` look brighter.
  - `Unlit/Texture` draws the flames as black squares (no transparency), so it's no use for effects.
- **Sorting.** The flames were set to draw above the donkey (sorting order +10), so some drew over the rider; a
  real effect should be depth-sorted in the world, not forced on top.

**What this settles:**
- **Fiery steps and flames: ready to build.**
  - Our own particles, drawn with the game's lit particle material, or better, with the game's own `flame_fx` /
    `Fire_*` materials. One more probe button would show those.
  - They need no shipped art at all if the game's flame materials look right.
- **Another animal: feasible and smaller than feared.**
  - About 15-30 pictures on the donkey's 96×96 canvas, left-facing only.
  - Drawn with the donkey's own material (lit like the game, recoloured by Colors).
  - Put in place with Clerk Desk's overlay method, keyed by the donkey's frame names.
  - The cost is the art itself, plus re-tuning the rider's seat and the cart for a different outline, plus the
    pooled-part undo.

## Results, second run (2026-10-17 01:40: steps 8-10)

A 64 s video of 5 and 6 through every shader, by day and at night, and a screenshot of 10 at night. There were no
warnings or errors in the log. Which shader is on screen is read from the order of the log's "effects now drawn with"
lines.

**The game's flame materials** (8).
- **What they are.**
  - `flame_fx` (shader `Mobile/Particles/Additive`, a 30×30 texture) is the flame of the game's lamps and lanterns:
    `light_fx/flame`, single image, size 1.2, life 2 s.
  - `sparks_fx` is those lamps' sparks.
  - `Fire_1_fx`/`Fire_2_fx` are an 8×8 dot from the braziers' fire.
  - The two `techpoint drop` materials are soft blobs.
  - `Glow` has no texture.
  - None of them uses a frame sheet.
- **How they looked on our flames and steps:**
  - `flame_fx` gave small yellow flame tongues that look like the game's own lamp flames and glow at night: the best
    match. Our particles are much smaller and shorter-lived than the lamps' (0.25-0.45 against 1.2, under 1 s against
    2 s), so the next try should use the game's own size.
  - `sparks_fx` gave thin rising sparks, a good companion for `flame_fx`.
  - The `techpoint` blobs were soft white or orange puffs, more magic than fire. `Glow`, like `Unlit/Texture`, drew
    plain squares: no use.
- **Lit against glowing.** Our `flame.png` with the game's lit puff material looked best by day, but at night it went
  as dark as the scene: wrong for fire. With the additive or plain shaders (`flame_fx`, `Sprites/Default`) the flames
  glow at night. **Fire wants an additive, unlit material**, and the game's own `flame_fx` is one.
- **Sorting.** Flames now pass in front of and behind the donkey with the world, not over the rider.

**The frame templates** (3). 15 frames saved whole in `donkey-frames\`: `bdy_static_left`, `bdy_walk_left_01..08`,
`bdy_ear_left_01..05`, `hed_static_down`. That is the paint-over set for a new animal's walk, stand and ear twitch.

**Dressing the donkey** (9, 10). The zebra test (`test\donkey\bdy.png`, the standing frame striped):
- **Over and instead** both drew it on the ridden donkey, mirrored with the donkey's facing.
- **At night** its white stripes took the scene's blue light like the game's own art (screenshot).
- **The beside-the-donkey copy** (9) showed with its label in the right place.
- **One picture for every frame** means no leg movement while walking, as expected from a single test picture.
- **Clean-up.** `hed_static_down` had no picture, so the donkey's own head showed (logged once). Turning dressing off
  restored the donkey (`donkey-frames-used.txt`: 9 frames shown, 1 without a picture).

**What this settles:**
- **Flames and fiery steps** can be built from the game's own `flame_fx` and `sparks_fx`, with no art of ours: glowing
  at night, sorted with the world, sized like the lamps.
- **A new animal** has a working path end to end: templates out, paint, drop in, see it in game, lit like the game.

## Results, third run (2026-10-17 01:50: the lamp fire, 12-13)

An 11 s video at dusk with the cart, the lamp fire at 1x. There were no warnings or errors.

- **The game's lamp fire.**
  - **The flame:** `light_fx/flame`, from the lamp prefab: size 1.2, life 2 s, 10 a second, a box shape, its world
    scale (1, 1.6, 1).
  - **The sparks:** size 0.1, life 0.8 s, 1 a second.
  - **Nothing had to be stripped:** both are pure particle systems, with no light or scripts on them. A real feature
    can copy them as they are.
- **How it looks:** real lamp flames, bright and in the game's style, around the donkey and the cart: the right
  look for fire.
- **The ring trailed.** The flame simulates in a **Custom** space (relative to a part of its lamp), and our copies
  don't move that part. So their flames stayed where they were born, and riding left a dense burning trail behind
  the donkey and cart. As a "burning path" that is a strong look of its own (maybe too much as it is).
  - The probe now logs where the custom space points.
  - A new button sets the ring to **stay with the donkey** (local space) or **trail behind** (world space), so both
    can be judged on purpose.
- **Not seen yet:** the size button (0.5x-1.5x) and full night.

**Sound from outside the game (11, same session).**
- **The test:** `5_intro_graveyard_donkey_9.wav` (5.18 s, mono, 48 kHz), dropped into `test\`, loaded from the file
  at runtime and heard in game.
- **So:** an outside `.wav` plays without any asset bundle. `.ogg` goes the same way, not tried yet.
- **Volume:** that test played as a plain sound, outside the game's volume settings. The game keeps its whole mixer
  in `AudioConfig.audioMixer` (LazyBearTechnology, next to `voiceOverGroup` and `mumbleGroup`).
  - The probe now logs the mixer's groups and plays test sounds through the sound-effects one, so they should follow
    the game's Sounds volume. To confirm next run: the log line, and the sound getting quieter with the Sounds
    slider.
  - Our mod's own sounds already go through the game (`LazyAudio`, MountSounds.cs); outside sounds would need this
    mixer route instead.

## Results, fourth run (2026-10-17 01:57: sound volume, the lamp fire ring and sizes)

A 69 s video with the cart, by day and into the evening. There were no warnings or errors.

- **Sound through the game's mixer.** The mixer `AudioMixer` has 14 groups:
  - `Master`;
  - `sfx_root`, `sfx_ui`, `sfx_cinematic_1..3`, `sfx_inworld`, `sfx`, `sfx_weather`;
  - `music_root`, `music`;
  - `speech_root`, `speech`, `speech_mumble`.

  The probe played through `sfx_root`, and **the sound followed the game's Sounds volume** (confirmed by ear). For a
  sound in the world, `sfx_inworld` is likely the closer match.
- **Why the ring trailed: confirmed.** The flame's custom space is the lamp prefab's own `light_fx`, which never
  moves. In local space the ring stays on the donkey.
- **The look.** The ring staying with the donkey, at **1.5x**, sits round the donkey and cart as tall glowing lamp
  flames. In the user's words: "looked really cool, almost satanic". That is the chosen look for flames around the
  donkey. 1x and the smaller sizes read as small lamp flames, and the trail as a burning path.

**Design input for a real feature** (not built; any feature goes into a new minor version):
- **The fire:** the game's lamp flame (`flame_fx`) and sparks (`sparks_fx`) particle systems, copied from the lamp
  prefab as they are, in local space, at 1.5x.
- **Placement:** on a ring round the donkey, following it like the cart does. Separate objects, never children of
  the pooled donkey parts, so nothing needs undoing on them.
- **Ships no art:** everything comes from the game.
