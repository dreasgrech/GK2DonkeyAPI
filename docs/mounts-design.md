# Mounts: design (2026-10-17 evening)

**2026-10-26 (built in or external):** the dragon, rat, snail and worm are no longer in Saddle Up's DLL but external
packs of their own in the repo's `packs/` (`SaddleUpDragon`, `SaddleUpCritters`), ids by the pack
(`saddleup-dragon.dragon`, `saddleup-critters.rat` ...); Saddle Up keeps Gerry and the donkey's two skins. Paths and ids
below are of their day; docs/making-a-mount.md "Built in or external" is current.

**Amended 2026-10-25 (API 0.37.0):** a mount no longer has a default effect (`MountDef.Effect` / `EffectWhen`,
`MountInfo.Effect` / `EffectWhen`, `[Mount] effect` / `effect_when` and `MountEffects.FromMount` are gone); effects
are registered only and nothing shows one (docs/api.md "Effects", docs/open-items.md "Effects on skins").

**Amended 2026-10-24 (the mount folders):** a mount is no longer a `[Mount]` block inside its default skin's
skin.cfg. A pack is `gk2mounts\<mount>\` with `mount.cfg` (`[Mount]` with `default_skin`, `[Stats]`, `[Tuning*]`,
`[Sounds*]`: plain names), the mount's `sounds\`, and `skins\<skin>\` for each look (skin.cfg, pictures, `front\`,
its own `sounds\`); the donkey's folder holds skins only. The "declaring skin", joiner and Default-by-id rules
below are history; docs/skins.md "What a pack is" and docs/making-a-mount.md are current (MountPacks.cs).

**Amended 2026-10-24, later (the ids):** a mount's id is `<mod>.<name>` (`saddleup.dragon`; only the game's `donkey`
is plain; one without a dot is refused). A skin is named within its mount, full name `<mount id>/<skin>`
(`saddleup.dragon/default`, `donkey/saddleup.hellfire`; skin.cfg has no `id =`). Every mount but the donkey has exactly one
default look, its `skins\default\`; `default_skin` is gone. Why: docs/mount-id-collisions.md.

Status 2026-10-20 (the foundation audit, docs/mounts-foundation-audit.md): the five built-in mounts are declared in
one layout (`[Mount]` with id, name, author, version, cart, foals always, the block last in the file, every tuning key
in one section; `tools/skins/canonical_cfg.py`, enforced by `tests/SkinsScan`); a built-in skin's mount reports
`IsBuiltIn`; a mount's `Name` defaults to its id and `Label` is what sentences use; `author` and `version` survive a
reload; the Mounts tab's Save keeps the comments of the mount sections; `effect_when` takes `Off`, `Night`, `Always`
("Never" is only the menu's word). Corrections to the text below: a skin has no `foals =` key (§3: the mount's
`Foals` and the player's FoalsWearSkin decide); a withdrawn skin leaves its mount listed (§8, as the Status says);
Gerry (a game-creature skin) declares its mount like the four drawn creatures; the recipes and the blue dragon of
the §1 table are gone, Royal Parade and Hellfire are the donkey's two skins. A mount's seat is its skin's `seat = <row>` since API 0.32.0 (the same for every player whatever their
AutoSeat; docs/seat-investigation.md §8), the skin's own `SeatAdjustPx` (its `[Tuning]`) nudging from it.

Status: **built 2026-10-17 evening** (API 0.30.0 / Saddle Up 0.10.0, the same release as skins and effects): Catalog.cs,
Api/MountCatalog.cs, the mount on `Looks.SkinDef`, `[Mount]` sections, the merged tuning per style, the cart rule at the three
refusal points, "As the mount" effects, abilities on the skin rig, `MountSettingsPage.MountRows` (Saddle Up uses it),
two self-tests, the example mod's `checker-beast`. Changes from the text below while building: `Unregister` is explicit
(a mount with no skins stays listed, nothing refcounts mounts); a pack's declaration is first-wins (packs share one owner);
abilities run only for instances with a handle (not the paper donkey, not a plain summon). Follows
[skins-design.md](skins-design.md) (how a skin is drawn) and [mount-looks-design.md](mount-looks-design.md) (the
player proposals). The user's brief: *make it as easy as possible to add more mounts with individual functionality,
abilities and configuration (the rider's seat and any other mount-specific values); define mounts against skins,
since a mount can have several skins; think about performance.*

## 1. The three things, and who owns what

| | What it is | Owns | Examples |
|---|---|---|---|
| **Base** (`Looks.SkinBase`, internal) | The game's view a mount is drawn on: its sprite parts, frame keys, canvas, pivot, pixels per unit, atlas. | Nothing a player sees. | the donkey (the only one) |
| **Mount** (`MountDef`, `Mounts.Catalog`) | *What the player rides*: a creature type. | Identity (id, name, noun, author, version); rules (may it pull the cart, may it have foals, their size); defaults (an effect); its sounds and other creature facts as tuning values (never the picture keys: those are the skin's, per style); **abilities** (code hooks). | donkey, giant rat, giant snail, giant worm, dragon, gerry |
| **Skin** (`MountSkinDef`, `Mounts.Looks`) | *How it is drawn*: a complete set of pictures for one mount's frame keys, made any way (a recolour recipe, a folder of PNGs, a sheet, own art, code sprites). | The pictures (behind and in front of the rider), the canvas, where the pictures sit (`offset`, `offset_cart`); and only when its drawing differs from the mount's other skins, the picture-dependent keys of the mount's tuning (its own `[Tuning]`: seat, torso, legs, hitch gap). | Red Dragon (mount dragon); Hellfire, Royal Parade (mount donkey, over its art); rat; snail; worm; gerry (the game's frames) |

**Amended 2026-10-24 (the sound system, [mount-sounds-design.md](mount-sounds-design.md)):** a mount's sounds are
the creature's by default and **a skin may override any event of them** (`[Sounds]` over `[Mount.Sounds]`, the
player's keys under both): two drawings of one creature may have different voices. The rest of the table stands.

**In one sentence: a mount has skins.** The Donkey is a mount; the game's own donkey (Default) and Hellfire are its
skins. The Dragon is a mount with one skin today (Default); a second would be another drawing of it. The Giant Rat, Giant Snail
and Giant Worm are mounts with one skin each. **The mount is the creature, the skin is the drawing**: a mount's values
(seat, hitch gap, cart, foals, effect, abilities, speed) are shared by all its skins; a skin's values (pictures,
offsets) are that look's only, and a value lives on the mount unless it exists only because of this skin's picture.
A skin cannot change what the mount is: its `[Tuning]` takes the picture-dependent keys (seat, torso, legs, hitch
gap) and refuses the creature's (speed, cart, foals, effect; said once in the log). (In code the mount is
`MountDef` / `Mounts.Catalog`, because `Mounts` is the API's root.)

A skin **names its mount** (`mount = dragon` in skin.cfg, `MountSkinDef.Mount`; the default is `donkey`). A mount has
one or more skins; the game's own donkey art is the donkey mount's first skin (`MountLooks.DonkeyId`). A mount without
any skin would be invisible, so a mount is only offered to players through its skins.

**The player picks a skin; the mount follows.** The instance's mount is derived from its skin, so nothing new is
saved (the `Skin` key already is). The settings page shows the relationship as two dropdowns: **Mount**
(Donkey, Dragon, Giant Rat, ...) and, under it, **Skin** with that mount's skins only (Donkey: Default, Hellfire;
every mount's default skin is listed as "Default" first). A dropdown's choices are fixed once built, so there is one Skin row per
mount, shown for the chosen mount (cfg `Mount`, `Skin_<id>`); what is saved on the donkey is still the one skin id; the panel's Mounts tab says "Tuning: Dragon (skin: Red
Dragon)" with *The mount: Dragon (shared by its skins)* and *This skin: Red Dragon* as its two groups. `MountHandle.Mount` and `MountSkinInfo.Mount` say which mount that is. A skin that names a
mount nobody registered falls back to the donkey mount, said once in the log (the pictures still show).

## 2. Where a value comes from (per instance, per key)

The first that has one wins. Everything below the first row is the existing machinery (reference.md "Where a value
comes from"); the two new rows slot into the "instance override" level as one merged set owned by
`Looks.TuningOwner`, recomputed only when the skin, the mount or the style changes.

1. An override another mod set on this instance (`handle.Tuning.Set(owner, ...)`): left to it, logged once.
2. **The skin's own geometry**: `[Tuning.Ride]` / `[Tuning.Cart]` (one style) over `[Tuning]` (both). Since
   2026-10-20 the skin owns its drawing's geometry (the seat row and the picture keys), and the scan refuses a
   picture key under the mount.
3. **The mount's tuning**: `TuningRide` / `TuningCart` (one style) over `Tuning` (both): creature facts only
   (speed, sounds); the picture keys are the skin's.
4. The player's cfg, then the mod defaults.

Per style matters: a creature may sit the rider differently with the cart hitched, and the keys exist in both
sections. The override mechanism is per key, so the rig re-applies the merged set when the instance's style changes
(one compare per tick).

### 2.1 Keeping the layers apart (audited 2026-10-17 23:10)

- **The Riding tab** (your cfg) never looks like it changes what it doesn't: a row the ridden creature sets shows
  the creature's value, orange and locked, with "the Mounts tab is where it is tuned" in its tooltip; a mod's
  override on the ridden donkey is cyan and locked as before. Your value underneath still applies to every donkey
  that isn't this creature, and the scope line says how many rows the creature takes.
- **The Instances tab's edit mode** (one donkey's own values, magenta) shows a creature-set key orange; dragging it
  gives that donkey its own value (the creature leaves the key to it, as to any owner); right-click gives it back and
  the creature's value returns at once (`RetuneNow`). The same for a mod's `Set` / `Clear`.
- **The Mounts tab** changes the creature only: never a donkey's own value, never your cfg; a note says so while a
  donkey is being edited. A change re-merges only the instances of that mount (`Catalog.Touch(mount)`).
- **The side file** never holds the creature's overrides (owner `Looks.TuningOwner`); a donkey's own and a mod's are
  kept as before. Owners read "the creature (Mounts tab)" and "this panel" wherever they are listed.
- Self-test "mount from a pack" checks the precedence (a mod's value wins and stays, cleared it returns to the
  mount's), a no-foals mount's foal, and the file round trip (save, reload, the wearer has the value); "mount
  from code" checks the hitch: the riding-only value is off the donkey by the next tick, the cart-style value never
  moves onto the free cart.
- Packs of a user share one owner (`<api>.packs`), the built-ins are the API's own: a user's pack can't redeclare a
  built-in mount (first declaration wins, it joins). A reload re-declares in place only for the same owner.
- One refresh per re-merge (the clear and the apply are one change); the settings picture's paper donkey gets no
  mount tuning (no rider to seat).

## 3. Rules and defaults on the mount

- `Cart` (true): false refuses the cart style for a creature of this mount: `Mounts.TryMount(Cart)`, `Hitch`,
  `MountCompanion.Use(Cart)` and `Mounts.Spawn(WithCart)` say "A giant snail can't pull the cart." A cart already
  hitched when the skin changes to such a mount is **left standing where it is** (the user's decision of 2026-10-20): the
  rig unhitches it on the next tick when the mount has a view; a mount asleep far away keeps it as data until it
  wakes, then the same; nothing is destroyed.
- `Foals` (true): false means no foal is drawn as this mount (the foals stay donkeys, as `FoalsWearSkin` off). The
  player's FoalsWearSkin is the only other say; a skin has no foals key of its own (removed 2026-10-18).
- `Effect` / `EffectWhen`: the effect a creature of this mount shows when its own `Effect` key says "the mount's"
  (`MountEffects.FromMount = "mount"`, a list entry "As the mount or skin": the skin's `effect =` first, else the mount's); the explicit picks (none, hellfire, ...) win.

## 4. Abilities: code on a mount

An ability is behaviour a mod attaches to a mount; the API calls it at the instance's lifecycle points. An abstract
class with no-op virtuals, so an ability overrides what it needs:

```csharp
public abstract class MountAbility
{
    public abstract string Id { get; }
    public virtual bool TicksWhileStanding => false;          // Tick only while ridden unless true (performance)
    public virtual void Attached(MountHandle mount) { }       // the instance became this mount (spawn, load, a skin change)
    public virtual void Detached(MountHandle mount) { }       // it stopped being this mount, or goes
    public virtual void Mounted(MountHandle mount, MountStyle style) { }
    public virtual void Dismounted(MountHandle mount) { }
    public virtual void Tick(MountHandle mount, float dt) { } // every frame with a view: ridden, or standing when TicksWhileStanding
}
```

One ability object per mount, shared by every instance of it (state per instance goes in a dictionary keyed by the
handle, or `handle.SetData`). An ability that throws is switched off for the session with one warning, as a skin that
fails to draw is. Keys, settings rows and sounds are the mod's own (the settings page, `MountEffects`, the sounds
API) called from the hooks; the API does not bind keys for abilities. The example mod carries one
(`examples/GK2DonkeySkins`: the dragon's "wing beat", a puff of the lamp's sparks on setting off, through
`Mounts.Effects`), so the shape is proven.

Why hooks and not a bigger set of built-in abilities: flight, fire, gallops are each a feature with their own game
touchpoints and tests; the mount layer gives them a home and a lifecycle, not an implementation. What the mount does
carry without code is everything the existing tuning keys express (speed through `SpeedMultiplier`, the seat, the
legs, the idle shuffle, sounds volume), which is most of "mount-specific values" today.

## 5. Registration, three ways

- **Code:** `Mounts.Catalog.Register(owner, new MountDef { Id, Name, Cart, Foals, Tuning, TuningRide, TuningCart,
  Effect, EffectWhen, Abilities })` → `IDisposable`. Same owner replaces (its instances re-apply), another owner is
  refused. `Unregister`, `Find`, `Mounts`, `MountsChanged` as for skins and effects.
- **A pack, no code:** a `[Mount]` section in `skin.cfg` *declares* the mount if nobody has registered it:
  ```
  [Mount]
  id = snail
  name = Giant Snail
  cart = true
  foals = true
  effect = none
  [Mount.Tuning]           # the creature's facts as tuning values, both styles ([Mount.Tuning.Ride] / [Mount.Tuning.Cart]
  BrayVolume = 0           # for one); never a picture key (the seat, the legs, the hitch gap are the skin's [Tuning])
  ```
  Owned by the pack's owner; a later pack naming the same id with its own `[Mount]` is told the mount exists and its
  skin joins it. The five mounts (rat, snail, worm, dragon, Gerry; Saddle Up's packs since 2026-10-20) declare
  their mounts exactly this way, in one layout (docs/skins.md "Worked examples"), so the pack path is exercised by
  the shipped skins every start.
- **The donkey mount** is registered by the API (no tuning, no abilities): the base the game gives us.

## 6. The menus

**The tuning panel (F11), Mounts tab** (built 22:55): one creature at a time, the one you ride by default (a cycler
picks any other; "Follow my ride" returns). Two groups, named for what they are: *The mount: Dragon (shared by its
skins)* with cart / foals / the effect it comes with and the rider's seat rows for the current style
(the same keys and ranges as the Riding tab, drawn plain with your own value when not set, yellow when set for the
creature, right-click = not set, a style switch button, copy-to-the-other-style, clear); and *This skin: Red
Dragon* with the pictures' offsets riding and with the cart, the rider's seat on this skin (its picture row or
picture top) and, per style, the skin's own rider, leg and cart pixel rows (since 2026-10-20: the skin owns its
geometry; the mount's group keeps the speed and its other keys). A line under the header says whether you
ride it ("every change shows on it at once") or what to ride to see them. *Keeping these values* says exactly which
file "Save" writes (the skin's own `skin.cfg`; a built-in's in the repo on the machine with the sources; the mount's
rows into every file that declares the mount), asks once more, and "Reload" reads the file back. Code-registered
mounts say so and keep their changes for the session. The creature generator no longer overwrites an existing
skin.cfg, so saved values survive a redraw.

Saddle Up has a **Mount** section (its picture, Name, the Mount dropdown, the Skin dropdown, the three baby rows,
Keep with the save) right after Keys (what you ride comes first), then **Colours** (every colour of the mount and
the cart under two headings), an **Effect** section, then Behaviour, Cart, Sound (0.11; 0.10 had Mount, Effect,
Behaviour, Donkey, Cart, Sound). Moving the sections renumbered them in the cfg, and the rows that moved (Name and the
baby rows from Donkey to Mount in 0.10; the colours, the cart's rows and Keep with the save in 0.11) name their old
place with `MovedKey`; the page carries a player's values over both ways (docs/api.md, "Sections keep your order"),
so nothing resets on an update. Three calls add the rows every player mod wants: `MountRows(section, companion)`
(the Mount dropdown, a Skin dropdown per mount), `FoalRows` (babies: how many, indoors, the same mount and skin),
`EffectRows` (the effect list with "As the mount or skin" / none / every effect, when it shows, the four effect toggles).
Saddle Up calls them instead of rows of its own, so a new mount, skin or effect from any mod appears with no change
to Saddle Up. **Since 0.35.0 (the user, 2026-10-20: a panel of its own, WoW-style) those rows are not in the Mods
menu or the Settings window at all: they live in the Mounts panel** (`MountsPanel.cs`; `PanelButton`, `OpenPanel`),
the mounts listed on the left, the chosen one drawn on the right with its description, what it does, its stat lines
and abilities, and its skin, effect and colour rows under it. The page keeps keys, behaviour, cart and sound, and
stops growing with the mounts installed; the cfg keys are the same as before.

**The F11 panel's Instances tab** spawns any mount with any of its skins: a "Mount and skin" cycler (the list
Saddle Up offers, by mount) above the buttons; "with its cart" is greyed when the mount refuses the cart, the family
button when it has no babies, each with the reason; the babies wear the parent's skin; every instance row names the
mount and skin ("#3 dragon (Red Dragon) with cart"). `MountSpawn.Skin` is the same for mods: the spawned one wears
the skin, and with it is that mount, from the first frame.

**Words.** Player-facing text says *mount* where the thing may not be a donkey ("Ride your mount", "Your mount is
nowhere to be found", "Mount and cart sounds"), *babies* for the young (a dragon's are not foals), and the mount's
own name where an instance is in hand (`Instance.Noun`: "This dragon already pulls a cart"). *Donkey* stays where
it is the donkey: the game's delivery donkey (corpses at the crossroads), the Donkey section's coat and harness,
the ids and keys (`npc_donkey`, `FoalCount`, the API's names: 0.9.x is public, those are not strings).

## 7. Performance

- The mount is a reference on the skin, resolved when the skin is registered and again when mounts change (a version
  counter, one compare per rig tick); no string lookups per frame.
- The merged tuning set is applied on change only (skin, mount, style), never per frame; the override write path is
  the existing one (`Overrides` dictionary) and the refresh is `ChangedByLooks` (seat, lantern, cargo views).
- Abilities: the rig holds the mount's abilities as an array; the ridden instance ticks its array every frame, a
  standing one only those with `TicksWhileStanding`; no allocations in the loop (`Time.deltaTime`, the cached
  handle). `Perf.S.Abilities` measures them (time and bytes) like every other part.
- The settings labels are built once per session (`FillList`), as before.

## 8. Tests

- Self-test "mount from a pack": a temp pack with `[Mount] cart = false` and `[Mount.Tuning.Cart]`: the mount is
  registered and listed; a donkey wearing the skin reports the mount; `Hitch` is refused with the mount's name; the
  mount's tuning is on the wearer and owned by the skin owner; the skin withdrawn, the mount stays listed until its
  owner withdraws it; the file round trip keeps the comments of the mount sections and the author and version.
- Self-test "ability hooks": a test ability on a code-registered mount counts Attached, Tick, Detached on a spawned
  instance wearing its skin; a throw switches it off with one warning.
- The existing skin, pack, leak, recipe and effect scenarios unchanged.
- Hand test: the Mount list in the three menus; the dragon's seat per style; a snail with the cart key.

## 9. Not in this version

~~Sounds per mount (the sounds API first)~~ (built 2026-10-24: [mount-sounds.md](mount-sounds.md)), flight/fire/gallop abilities (each its own design), mounts on another base
(a second `SkinBase` first), a picker with pictures per mount.
