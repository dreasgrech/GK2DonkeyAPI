# From proof of concept to features: looks, fire, sounds (design draft, 2026-10-17)

**2026-10-26 (built in or external):** the dragon, rat, snail and worm are no longer in Saddle Up's DLL but external
packs of their own in the repo's `packs/` (`SaddleUpDragon`, `SaddleUpCritters`), ids by the pack
(`saddleup-dragon.dragon`, `saddleup-critters.rat` ...); Saddle Up keeps Gerry and the donkey's two skins. Paths and ids
below are of their day; docs/making-a-mount.md "Built in or external" is current.

**The skinning system's full specification is [skins-design.md](skins-design.md)** (2026-10-17, designed against the
code); this file keeps the player-facing proposals, the decisions and what the probe rounds found.

The asset investigation ([external-assets.md](external-assets.md)) proved four things in game:
- **The game's lamp fire** can ring the donkey (chosen look: 1.5x, staying with the donkey).
- **Fiery steps or a burning trail** work.
- **The donkey can be redrawn** frame by frame with outside art, lit like the game.
- **Outside sounds** play and follow the game's Sounds volume.

This draft turns them into features for Saddle Up. As always, **the API does all the work and Saddle Up only calls
it** and offers settings. Nothing here is built; the decisions at the end are the user's.

## What makes it gameplay, not a toy

A proof of concept shows something; a feature needs a reason to use it and a place in the game. Three questions for
each idea:
1. **What does the player get?** A look, an ability, or an effect on the game (light, speed, fights).
2. **How do they get it?** Always available as a setting, or earned in the game (a tech, an item, a ritual).
3. **When does it show?** Always, while riding, at night, in fights, or while a timer runs.

Saddle Up's rule still holds: players choose *what happens* (on/off, which look, which key), never *how it's drawn*
(sizes, offsets, particle settings: the API's tuned values).

## Proposal 1: Hellfire (the fire ring)

The donkey wreathed in the game's own lamp fire.

| Variant | What the player gets | Cost and risk |
|---|---|---|
| **1a. A look** | A setting "Hellfire": off / at night / always. Purely visual. | Small. The probe's code, made proper. |
| **1b. A light** | The same flames light the way at night like the cart's lantern, so you can ride without the cart and still see. | Small to medium: the lantern's light already exists (Lantern.cs); one more light source to place and tune. |
| **1c. A weapon in fights** | In a mounted fight, enemies that touch the ring burn: small damage now and then. The game has the parts (`ICombatEntity.CombatEntityHpComponent.ApplyDamage`, used by its own `AOEDamageSource` with a team filter). | Medium to large: fights are the game's balance; it needs its own investigation like mounted-combat.md, and limits (damage, how often, only enemies). |
| **1d. Earned** | Any of the above unlocked by something in the game: a tech, or burning a rare item on the cart. | Small for a tech (the TechGate example does it); an item needs its id found in the game's data. |

**Recommended first step: 1a + 1b, with 1d as an option** ("Earn it", off by default, as section 6 extra A of
saddleup-scope.md). 1c only after its own investigation.

## Proposal 2: Fiery steps (the trail)

Fire left behind where the donkey walks. Seen in the probe as step bursts and as a dense burning trail.

| Variant | What the player gets | Cost and risk |
|---|---|---|
| **2a. Part of Hellfire** | While Hellfire is on and the donkey moves, small flames are left behind, burning out in about 2 s. | Small: one emitter, the lamp flame in world space. |
| **2b. A gallop** | A sprint key: a burst of speed for a few seconds, with a cooldown, leaving the fire trail while it lasts. The trail shows the ability is running. | Medium: speed is already the API's (riding multipliers); a timed boost plus a cooldown is new, and must respect the game's energy (does sprinting cost energy?). |

**Recommended: 2a as part of Hellfire.** 2b is a separate ability: decide if Saddle Up should have one at all.

## Proposal 3: Mount skins (another animal)

The donkey redrawn: a rat, a zebra, a skeleton donkey.

**How it works:**
- **The art:** a skin is a set of pictures named after the donkey's frames, 96×96, facing left: about 15 for stand,
  walk and ear, more for graze and the head turns. The probe's templates (`donkey-frames\`) are the paint-over set.
- **Drawing it:** each frame, the API draws the skin's picture for the donkey's current frame over or instead of
  the donkey (Clerk Desk's method, proven in the probe), with the donkey's material: lit like the game, and
  recoloured by the Colors feature if drawn in the donkey's colours.
- **The undo:** the API owns it, as for every change to the reused donkey parts (pooled-parts.md).

| Variant | What the player gets | Cost and risk |
|---|---|---|
| **3a. Skins we ship** | A "Look" setting: Donkey, Zebra, Skeleton, ... Each skin is our own art, inside the DLL. | Small in code; **the art is the cost** (about 15-30 pictures each). A different outline also needs its seat, leg and cart offsets: a skin carries its own. |
| **3b. Skins from anyone** | The API reads skin folders (pictures plus a small text file with the offsets) from a skins folder, so artists can make skins without code, and other mods can register skins. | Medium: a folder format, checks (size, names, missing frames fall back to the donkey), docs for artists. Opens the door to community content. |

**Recommended: 3b's format from the start, with one or two skins of our own as the examples (3a).** Without art,
skins can't ship; the first question is who draws.

## Proposal 4: Sounds of our own

Sounds that go with a look: a rat's squeak instead of the bray, fire crackling with Hellfire.
- **The path, proven:** a `.wav` read at runtime, played through the game's mixer (`AudioConfig.audioMixer`, group
  `sfx_inworld` for sounds in the world), following the Sounds volume.
- **Where they fit:** a skin brings its sounds (bray, steps) in its folder, and Hellfire can crackle. The game's own
  fire sounds (furnaces: `ConveyorFurnace_Fire_*`, `furace_fire`) may do for Hellfire without shipping any.

## What the API would offer (sketch, not final)

- **`Mounts.Looks`:**
  - register a skin (from embedded pictures or a folder), list the skins, pick one per donkey;
  - turn Hellfire on per donkey, with its mode (night / always) and a light.
- **The settings page** gets Saddle Up's new rows: Look, Hellfire, Fire trail.
- **Earning things:** a gate ("unlocked when"), so Saddle Up can offer "Earn it" without knowing how.
- **Everything else stays inside the API:** the frame matching and the undo on the pooled parts, the game's flame
  copies, sound routing, the save of who has what.

## Decisions for the user

1. **What kind of mod should Saddle Up become here:** looks (cosmetic), abilities (light, gallop), or fight power
   (a burning ring that hurts)?
2. **Earned or free:** are Hellfire and skins settings anyone can switch on, or unlocked in the game (and then by
   what: a tech, an item, a quest)?
3. **Skins and art:** who draws them? And should skins be open to anyone (a folder format for artists and other
   mods), or only ours?
4. **Which first:** Hellfire (no art needed: everything is the game's) is the quickest to make real; skins need art.

## Decided (2026-10-17)

1. **Looks first, abilities later.** Hellfire and skins ship as looks. The light (1b), the gallop (2b) and fight
   effects (1c) come later, each designed on its own.
2. **Free, with an optional "Earn it".** Everything can be chosen in the settings at once. An option, off by default,
   makes the looks unlock in the game instead (what unlocks them: open, below).
3. **Skins in an open format.** A skin is a folder of pictures plus a small text file. Artists and other mods can
   add skins; ours are the examples.
4. **Both together.** Hellfire and skins are designed and built as one API change, "Looks", one new minor version
   (API 0.30.0, Saddle Up 0.10.0).

## The specification (draft 1)

### Hellfire

Since 2026-10-20 the Hellfire **skin** comes with this effect (`effect = hellfire` in its cfg): a player who picks the
skin and leaves the Effect row at its default ("As the mount or skin") sees the horns and the fire together, as the
look was designed; the Effect row still overrides it. The ring scales by the worn skin's drawn width.

**What the player sees.** The game's own lamp fire, tall flames and a few sparks, in a ring round the donkey,
moving with it. While the donkey walks, small flames are left behind and burn out in about 2 s (proposal 2a).

**Settings** (Saddle Up, under Donkey):

| Row | Choices | Default |
|---|---|---|
| Hellfire | Off / At night / Always | Off |
| Fire trail | On / Off (only while Hellfire shows) | On |

**When it shows:**
- **"At night":** from dusk to dawn by the game's clock, like the lantern's own night rule (Lantern.cs).
- **Riding or standing:** both: it's the donkey's look, not the rider's.
- **Indoors** (inside a building, where the donkey can follow as foals do): off. Fire in a house reads as a fire,
  and the flames would go through the furniture.
- **Mounted fights:** on (a look only; no damage). Open: hide it in fights, so it can't be taken for an attack?
- **Cutscenes and dialogue:** left as they are, like the donkey itself.
- **Sleeping:** the donkey isn't with the keeper then; nothing changes.

**Who has it:**
- **The donkey:** yes.
- **The cart:** no fire of its own. When hitched, the ring is round the donkey only, and the cart follows inside or
  beside it. Open: a wider ring round donkey and cart, as in the probe video, if that looked better.
- **Foals:** no. Open: a small ring at foal size.

**How it's built** (API):
- **The fire:** copies of the game's `light_fx/flame` and `light_fx/sparks` from the lamp prefab, found the way the
  probe does. They are made under an inactive holder and kept to the particle systems.
  - Local space for the ring. World space for the trail, so its flames stay behind.
  - Size 1.5x, six flames on a ring of radius 0.8 (the probe's values): tuned values, not settings.
- **Separate objects** that follow the donkey's root each frame, never children of the reused donkey parts. So
  nothing needs undoing on those parts; the copies are destroyed when the donkey's view goes.
- **If the game's lamp fire can't be found** (a game update renamed it): no Hellfire, one line in the log, and the
  setting stays but does nothing. It's a "warned" feature in game-code.txt.
- **Cost:** about 10 particle systems per donkey with Hellfire on. To be measured with the perf runner before
  release.

### Skins

**What the player sees.** The donkey drawn as another animal. Anything the skin doesn't draw stays the donkey's own,
so a half-finished skin still works.

**Settings** (Saddle Up, under Donkey):

| Row | Choices | Default |
|---|---|---|
| Look | Donkey, then every skin found | Donkey |

- The coat and harness colours keep working for skins drawn in the donkey's palette colours.
- A skin with its own colours ignores them; its folder says which mount it is.

**Who wears it:** the player's donkey. The skin's text file can say whether foals wear it too: a foal-sized skin is
the same pictures, since foals are the donkey scaled down. The cart is never skinned.

**The skin folder** (`BepInEx\plugins\<any mod>\skins\<skin id>\`, or embedded in a mod's DLL under the same names):

| File | What |
|---|---|
| `skin.cfg` | Name shown in the settings, author, version; `colours = palette` or `own colours`; foals yes/no; offsets (below). |
| `bdy_<clip>_<frame>.png` | One picture per body frame, 96×96, facing left, named like the probe's templates (`bdy_walk_left_03.png`). |
| `bdy_<clip>.png`, `bdy.png` | Fallbacks: one picture for a whole clip, or for everything. |
| `hed_<...>.png` | The same for the head layer. |
| `sounds\bray_1.wav` ... | Optional: the skin's sounds instead of the donkey's (below). |

**Offsets in `skin.cfg`**, for an animal of another shape:
- the rider's seat: up and along, in pixels;
- the rider's leg and back cut lines;
- where the cart's shafts attach.

All are optional: missing means the donkey's. They're the only values a skin may tune, in whole pixels, clamped to a
safe range.

**Checks when a skin loads** (every problem is logged once, and the skin still loads unless it has no usable
picture):
- every picture 96×96 PNG: a wrong size is skipped;
- names that match no donkey frame: listed, so an artist sees typos;
- frames the skin doesn't draw: listed (the to-do), and drawn as the donkey;
- offsets outside their range: clamped.

**How it's built** (API):
- **Drawing:** Clerk Desk's overlay method, proven in the probe. For each body and head sprite, the skin's picture for
  the current game frame is drawn just before the camera draws, with the donkey's material and per-renderer data.
  The donkey's own sprite is hidden for the frames the skin draws.
- **The undo:** the overlays and the hiding are view changes on reused parts. They get their undo in
  `ReleaseView`, and `DonkeyPrefab`'s audit learns about them (pooled-parts.md).
- **Pictures:** loaded once per skin, when it's chosen, and kept while it's in use. A skin of 30 pictures is about
  1 MB in memory.

### Sounds of a skin

Optional, in the skin's `sounds\` folder. Names match the donkey's sound events (bray, step, ...), several numbered
files for variety. Played through the game's mixer (`sfx_inworld`), so the Sounds volume applies. Missing sounds:
the donkey's. Hellfire can crackle with the game's own furnace fire sound (to audition, like the foal sounds were).

### Earn it

An option "Earn your looks" (off by default): with it on, Hellfire and the skins show as locked in the settings until
earned. The API offers a gate, "unlocked when ...", and Saddle Up says what unlocks what. **Open: what unlocks
them.** It must be something every player reaches, checked in the game's data, not guessed.

### Not in this version

- The light (1b), the gallop (2b) and fire in fights (1c).
- Skins for the cart.
- A skin picker with a picture: the settings picture (LookPreview) could show the skin later.

## Open questions (draft 1)

1. **The ring with the cart hitched:** round the donkey only, or wider round donkey and cart?
2. **Hellfire in fights:** shown (a look) or hidden?
3. **Foals:** Hellfire on foals (small), and skins on foals when the skin says so: yes?
4. **Earn it:** what should unlock Hellfire and the skins? Ideas, to check in the game: a tech late in the tree for
   Hellfire (fire, alchemy, or the dark side of the church), and for skins a cost (coins, or an item) per skin.
5. **Our first skins:** which animals? This needs art: who draws them?

## Decided, round 2 (2026-10-17)

1. **The cart, fights and foals (open questions 1-3): settings, as many as it takes.** What works best is found by
   playing, so each choice is a setting.
   - Hellfire round the donkey only, or round donkey and cart.
   - Shown in fights or not.
   - On foals or not.
   - Foals wear the skin or not.

   Defaults are picked after trying them. The ring's size, radius and flame count stay tuned values (the dev panel),
   per Saddle Up's rule.
2. **Earn it: not now.** Everything is available in the settings. The "Earn it" option and the API's gate are left
   out of this version.
3. **Skins: get creative.** Our own first skins are made without drawing (below).

## Recipe skins (round 2)

A **recipe** is a rule applied to the donkey's own frames while the game runs, instead of a set of drawn pictures.
Examples: stripes, a ghostly see-through glow, charcoal with glowing lava cracks.

**Why recipes:**
- **They animate perfectly.** Every frame of every clip is covered (walk, graze, ear, the head turns), because the
  recipe works on whatever frame the game shows, with the donkey's exact outline: no seat or cart offsets to tune.
- **No art to draw.** Nobody has to paint anything.
- **No game art redistributed.** We ship the rule, not edited copies of the game's sprites, the same way the Colors
  feature recolours at runtime today.

**How they'd be built:** each recipe makes a texture from the donkey's atlas once (like Colors' palette work, cached
per frame), drawn with the overlay method. The two mounts of skin then sit side by side:
- **picture skins:** an artist's folder;
- **recipe skins:** built in, and maybe a few recipe settings in a skin's `skin.cfg`, so others can make their own:
  base colours, a pattern, a glow on the outline.

**The first prototypes** (Python on the 15 frames exported so far, a preview only, never shipped:
`tools/skin_recipes.py`, its output in `BepInEx\GK2-asset-probe\test\skins\` on the dev machine; both retired 2026-10-19):

| Recipe | The idea |
|---|---|
| zebra | white and black stripes over the donkey's own shading, black outline |
| ghost | pale cyan, see-through (62%), a bright outline |
| ember | charcoal body, glowing orange outline and lava cracks: Hellfire's partner |
| zombie | rotten green, purple rot patches, dark stitches |
| golden | gold ramp with glints, a dark gold outline |
| shadow | near-black with a violet outline |

**To try in game:** the probe's **Next test skin** button cycles through `test\skins\`, and **10** dresses the
donkey with the chosen one (best "instead of the donkey"). **Export every donkey frame** saves all the donkey's
frames, not only those seen in 30 s, so the recipes can be run on the complete set.

## Seen in game (2026-10-17 14:43)

- **Every donkey frame:** 48 in the character atlases, 44 of them the donkey's (4 are the cart wheels):
  - body: `chew` 3, `ear` 5, `graze` 5, `lips` 13, `static` 1, `turn` 6, `walk` 8;
  - head: `hed_static_down` 3.

  That's the full set a recipe or a picture skin covers. The recipes are now made on all 44.
- **The recipe skins on the ridden donkey** (screenshots: ghost, golden, shadow): each sat exactly on the donkey,
  lit like the scene, with the rider seated as on the plain donkey. Foals stay plain (not skinned in the probe).
  There were no warnings or errors.
- **The fire ring wasn't centred on the donkey.** It was centred on the donkey's root, its ground anchor. The body
  sprite sits beside the root, on a side that flips with the facing; the API's own centering already cancels that
  offset (Mount.cs). The probe now centres the ring (and the steps) under the middle of the drawn body, and logs the
  offset. The real feature centres the same way.

## Seen in game: a foal wore the skin (2026-10-17)

While hitching, unhitching and switching skins, the probe's skin moved from the ridden donkey onto a foal (screenshot).
At the same time the API logged two `[pool]` warnings: the donkey's view went back to the pool with `bdy` and `hed`
still forced off, and the audit put them right.

**Why:**
- **It remembered parts, not a donkey.** The probe kept the donkey's body and head renderers from when it first
  found them.
- **Hitching and unhitching swap the donkey's view.** The old view's parts go back to the API's pool, and a foal
  took them.
- **So it dressed the wrong animal.** The probe went on dressing those renderers wherever they were.
- **And the hiding travelled with them.** In "instead" mode the probe hid the donkey's own picture with
  `forceRenderingOff` and left it set between frames, so the pool got the parts still hidden.

**The probe's fix (0.2.0, built):**
- **Hidden only while drawing.** The donkey's picture is hidden only while the main camera draws (set before it
  culls, put back after it renders), so no game code ever sees the part hidden.
- **Re-checked twice a second.** The probe looks again for which donkey is the player's (never a foal: those are
  drawn at 0.6 size), and moves the dressing when the view changes.

**What it means for the real feature:** skins must be tied to the **view's lifecycle in the API**, not to remembered
renderers:
- the overlay is made when a donkey's view is set up for an instance that wears a skin;
- it's removed in `ReleaseView`, together with any hiding;
- `DonkeyPrefab`'s audit knows the overlay child.

The API already knows which view belongs to which instance (MountRegistry), which the probe can only guess. This test
is the proof that guessing isn't enough.

## Seen in game: the cameras, and a flash of donkey on a swap (2026-10-17)

**The game draws with two cameras** (logged by the probe):
- **"Deform Texture Camera":** depth -50, one layer (mask 8) into a 1280×720 buffer, probably for grass that bends.
- **"Main Camera":** depth -1, the screen.

**Unity's `Camera.main` is the deform camera**, as the API's CargoSheet.cs already noted. So:
- **Hiding the donkey** for a skin has to last across every camera of the frame. The probe hides it from the first
  camera's cull to the end of the frame. Hiding only during "the main camera" hid it during the deform camera's draw,
  and the screen camera still drew the donkey.
- **Screen positions** (labels, captures) must use `CameraSystem.Instance.WorldCamera`, as the API does.
- **The API's own two `Camera.main` uses** (Mount.cs: the seat measure's up direction, the away-from-camera
  direction) only take the camera's orientation, which both cameras share: harmless, left as they are before
  release.

**A flash of the plain donkey when switching between donkey and cart** (about a second). Hitching swaps the donkey's
view, and the probe only looked for the new view on a timer. The probe now looks at once, every frame until found,
right before the cameras draw. The real feature can't flash at all: the API attaches the skin when it makes or wakes
the view, before that view is ever drawn.

## Creatures of our own, and what comes next (2026-10-17)

**Captures with the rider** (the probe's Capture tab, 15:54-15:55):
- **Snail:** the keeper sits on the shell, legs down its side; he sat a little *into* the shell, so the snail now
  has a saddle on the shell's top.
- **Worm:** he sits on the rear hump with the worm's front rising before his hands; reads well.
- **The plain donkey never showed through** in any capture.

**The rat** (tools/skins/creatures/make_creatures.py): four short legs in a walk cycle (diagonal pairs together, the feet lifting
on the forward swing, far legs dark behind the body), a pointed snout, ear, eye, whiskers and a swaying tail; the
back on the seat line, so the rider sits without tuning.

**Decided:** once the skinning system is settled, **rider settings per skin and per mount** come next: seat height
and position, the leg and back cuts, the cart hookup, possibly a pose. Not before.

## Built: Hellfire as the first particle effect (2026-10-17 evening)

The user's rule for it: *the fire is one effect of a particle effects system exposed in the API; everything we do
must be possible through the API.* So Hellfire is not a feature of its own: `Mounts.Effects` (Effects.cs) is the
system, and Hellfire is registered through the public `MountEffectDef` exactly as a mod would register one.

**2026-10-25 (API 0.37.0): the rule is reversed for picking and showing an effect.** The user: "Remove all the stuff
we're exposing regarding the effects. Then we'll [add] some specific individual particle effects on specific skins
after."; asked how far: "The ability would be available from the api and then we will consume it from the skin
consumer"; and of the skin hook: "Remove it and we'll think about how to implement properly". Registration stays in
the API (`Mounts.Effects`, `MountEffectDef` and its parts, the particle engine, Saddle Up's Hellfire); everything that
picked or showed an effect is gone: the instance keys `Effect` / `EffectWhen`, `MountEffects.FromMount` / `NoneId`,
`MountEffectWhen`, `Mounts.EffectSettings` and the `[Effects]` section (retired), `EffectRows` and Saddle Up's Effect
section, `MountDef` / `MountSkinDef` / `MountCompanion.Effect`, the pack keys `effect` / `effect_when`, the panel's
"Comes with" line. Nothing shows Hellfire's fire until the skins' use of an effect is designed (docs/open-items.md
"Effects on skins"). The text below is the 2026-10-17 build.

- **An effect** = parts; a part = a source of particles + emitters. Sources: the game's own particle systems found by
  material and copied (`GameParticles`; the lamp's `flame_fx` and `sparks_fx`, held through Addressables so the magenta
  squares of the probe days can't return), or a mod's prefab. Emitters: a ring (local space, follows), the body, a
  trail (world space, bursts that burn out). Per emitter: count, radius, size, height, offset, angle, every, burst.
- **Hellfire** = six lamp flames and three sparks on a ring of 0.8 at 1.5× (the "almost satanic" size), a flame left
  behind every half unit. Ring centred on the drawn body (`BodyPoint`), 1.6× wider and centred between donkey and cart
  when `EffectWithCart` is on.
- **Per donkey (look keys, saved):** `Effect` (id) and `EffectWhen` (off / at night / always). **Everyone's:**
  `EffectTrail`, `EffectWithCart`, `EffectInFights`, `EffectOnFoals`, `EffectNightBelow`. Never indoors. A mod's
  effect may add its own `Show` rule on top.
- **Saddle Up rows (Donkey):** Effect (a list: None, Hellfire, every mod's), Effect shown, and the four toggles, shown
  only while an effect is picked.
- **Night** = the shader global `_SunLight` (what the lantern reads) below the threshold: no game member, so Effects
  stays out of the dependency manifest, like Looks. The fight check goes through `Mount.Instance.RiderInArmour()`.
- **Nothing on the pooled parts:** every emitter is an object of ours under a holder that follows the body; gone with
  the view (Detach) or the instance (Release). Self-test "effect: hellfire lit, follows, out, gone with the donkey".
- **Left for the hand test:** the ring's look on the snail and the rat, the trail at a gallop, the sparks' count,
  a fight with it on, and whether the lamp's bundle ever goes while a donkey burns (a lost material rebuilds the ring
  and logs it).

Skins got the same treatment the same evening (skins-design.md status): one sheet texture per skin, a base record so
nothing of the donkey is assumed by the loader or the rig, sheet packs, and sprite sources for code.

## The dragon, Gerry and Royal Parade (built 2026-10-18 to 21)

**Red Dragon** (`tools/skins/creatures/make_creatures.py`, `assets/skins/dragon/`; since 2026-10-24 the mount
`assets/mounts/saddleup.dragon/` with its look in `skins/default/`): the one mount on a bigger canvas
(128: the donkey's frame sits centred, so its pivot and ground row are the donkey's), drawn with a **front layer**
(`front\`: the near wing, drawn in front of the rider; the self-test "front layer" reads it back over him). The
near wing was raised at first, its outer fan in front of him: ridden, it hid him (the user, 2026-10-20), so it is
**folded** now, as the user's reference sheets draw a resting dragon (a tall wrist peak above the back with the claw
hooking forward, the fingers running down from it, the membrane hanging like a curtain to the ground behind the
hindquarters, scalloped, in a colour of its own with an outline): both wings rooted at the shoulder, the peak
above it and behind the rider's head, the curtain hanging from the peak back over the flank, its hem on the
belly line (the big idle reference hangs the curtain to the ground; the red walk frames and every blue frame
tuck the wing against the flank, and a mount that walks does too: the belly band and both hind legs in view
under it, its tip pointing back along the tail over the rump (the blue references' long wedge from the wrist to
past the hips; the user's mark), nothing on the ground, the whole wing one piece with the body, no sway and no pinned hem, after a
ground-reaching version jiggled in the world, the user's video 2026-10-20); nothing of the wing lies forward of the
peak. The whole near wing is the front layer, over the rider wherever the two overlap (the user, 2026-10-20: the
wing is nearer the camera than he is, so no part of him may show in front of it): he sits inside the fold with
the top of his head, part of his face and the reins arm out of it, chest and everything below under the
membrane. (Tried and rejected the same day: a cut that drew the membrane behind him above his waist to keep his
chest in view, which showed him in front of the wing; and a one-layer wing shaped around him, which grew a lobe
under his seat that stood out like a thumb when nobody rode and moved the peak to the mid-back; no reference
draws either; the method and the recipe for the next mount: docs/front-layer.md.) The far wing is
the same fold a few pixels back and up behind the body (its peak and the
top of its curtain show behind the near one's), `seat = 68` (the back on the 128 canvas), `[Tuning.Cart] HitchGapPx = 24` (the whole cart
that far further back, or the body is drawn inside it), colours Coat = scales and wing, Muzzle = the golden belly (the page's Second tone row: its own colour, apart from the scales),
Dark = bone, Eye = yellow. One dragon: a blue one is the red one with a custom coat (the colour rows do it). Cart and
babies allowed; the effect ring scales by its drawn width (108 px over the donkey's 46).

**Gerry** (`assets/skins/gerry/skin.cfg`, since 2026-10-24 `assets/mounts/saddleup.gerry/mount.cfg` and `skins/default/skin.cfg`;
docs/gerry-mount.md): the game's talking skull as a mount with no pictures
of our own (`source = game`: his `0901_` frames read out of the character atlas at run time, 3x on a 160 canvas, the
hop kept at a quarter under a rider), `seat = picture` (the rider on the skull's own top, per frame), `noun = skull`,
babies 0.33 (his own size), lips silenced. The first mount that is another character of the game: any preset prefix
goes the same way.

**Royal Parade** (`tools/skins/royal-parade/make_royal.py`, `assets/skins/royal-parade/`, since 2026-10-24
`assets/mounts/donkey/skins/royal-parade/`, later that day `saddleup.royal-parade/`): the plain **over** pack
and the worked example of drawing on the donkey without touching it: a saddle cloth cut from the standing frame and
hung from the hip in every frame (so it rides the walk's bob and the graze's dip), a crown at the Hellfire horn
bases, the cloth's reds named `Harness` so the player's harness colour recolours them, the gold kept as drawn. A
built-in since 2026-10-19 (before: a sheet pack copied into the game's `gk2skins\`); the first skin that went
through Aseprite and the watcher by hand (docs/sprite-tools.md).

**Hellfire as a pack** (2026-10-19): the recipe code is gone; the pack holds only the prototype's own pixels (horns,
tail, cracks, the glow) with the erase colour where the ears and tail come off and `[Base.Coat]` for the ash coat
(a recolour rule, so the player's coat colour still works), and since 2026-10-20 it comes with its fire
(`effect = hellfire`). Every built-in skin is now a pack of the one format, made by a generator in the repo
(docs/skins.md "Worked examples", docs/sprite-tools.md "Making a skin: the one pipeline").
