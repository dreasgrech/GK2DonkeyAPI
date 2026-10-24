# Making a mount: how a mod registers one of its own

The one-page guide. The long forms: [skins.md](skins.md) for every key of a pack, [api.md](api.md) "Mounts" and
"Skins" for every member of the API, [mounts-as-mods.md](mounts-as-mods.md) for the rules between mods (ids, owners,
versions, load order), [mounts-design.md](mounts-design.md) for why things are as they are.

## Two things, not one

A **mount** is the creature: its id, name, description, whether it pulls the cart, whether it has babies and how big
they are, an effect it comes with, its own tuning (speed, sounds, seat, cart geometry), free stat lines for the
Mounts panel, and abilities (code hooks). The donkey is the mount with no definition: the game's own.

A **skin** is a drawing for a mount: 44 frames on a canvas, where the rider sits on that drawing, and what the
drawing's colours mean. Every mount has at least one skin. Hellfire and Royal Parade are skins of the donkey; the red
scales are a skin of the dragon, and the dragon exists because that skin declares it.

The rule that ties them: **a mount is declared by a skin's `[Mount]` block, and the skin whose id is the mount's id
is its Default and owns the declaration.** Other skins say `mount = <id>` and join as another look of the same mount.
A joiner's own `[Mount]` block holds only until the Default is registered.

A skin's pictures come one of three ways: a **pack** (a folder of PNGs and a `skin.cfg`, on disk or embedded in a
DLL; no code), a **recipe** (code that turns each donkey frame's pixels into new ones), or **code sprites** (a mod
hands the API its own `Sprite` per frame key). A pack is what a mod normally ships.

## Shape 1: a pack in a DLL (no mount code)

The mount and its skin are a folder of files; the mod only hands the folder to the API.

```
MyMountMod/
  MyMountMod.csproj
  Plugin.cs
  assets/skins/giant-crab/
    skin.cfg
    bdy_static_left.png, bdy_walk_left_01.png, ...   the 44 frames on the canvas (skins.md "The pictures")
    front/ ...                                        optional: parts drawn over the rider
    portrait.png                                      optional: the panel's icon; else the standing picture is cut
```

`skin.cfg`, the Default skin declaring the mount (every key: skins.md "skin.cfg"):

```ini
[Skin]
format = 1
needs = 0.36
id = giant-crab
name = Giant Crab
description = A crab the size of a cart, sideways at everything.
author = you
version = 1.0
base = donkey          # the skeleton it animates on: the donkey's 44 frame keys
colours = own          # drawn in its own colours (palette: drawn in the donkey's)
layer = replace        # replaces the donkey's picture (over: drawn on top of it)
canvas = 128 128       # the picture size; 96 is the donkey's
seat = 70              # the canvas row the rider's hip sits on, from the top
mount = giant-crab     # the mount this skin is a look of

[Tuning.Cart]          # the skin's own geometry: where the cart meets this drawing
HitchGapPx = 18

[Colors]               # what the drawing's colours mean, by region, so the player's colour rows reach them
Dark = 2A1008 3C1810
Eye = F0E040

[Mount]                # the creature: in the Default skin only
id = giant-crab
name = Giant Crab
description = Pulls the cart, has young, scuttles a little faster than a donkey.
author = you
version = 1.0
cart = true
foals = true
flies = true           # it flies (leave out for a mount that doesn't; jumps = false for one that never jumps)
foal_scale = 0.4
speed = 2.25           # riding, times walking speed (the donkey: 2); the panel says "Fast"
cargo = 6              # items its cart carries, 1 to 6 (with the cart it goes at its speed x 0.75, as the donkey)

[Mount.Tuning]         # the mount's own values; a mount may pin its sounds, seat, cart geometry and shuffle
BrayVolume = 0

[Mount.Sounds]         # the creature's own sounds, shared by its skins (docs/mount-sounds.md): files of this skin's
voice = click_1.ogg, click_2.ogg   # sounds\mount\ folder, a game sound id, none, or ground; <event>.volume etc.
lips = none

[Mount.Stats]          # free lines for the Mounts panel
Legs = 8
```

The csproj embeds the folder under a predictable name:

```xml
<ItemGroup>
  <EmbeddedResource Include="assets\skins\**\*"
                    LogicalName="MyMountMod.skins.%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

`Plugin.cs`, the whole mod:

```csharp
using System.Reflection;
using BepInEx;
using GK2DonkeyAPI.Api;

[BepInPlugin("you.gk2.mymountmod", "My Mount Mod", "1.0.0")]
[BepInDependency("dreas.gk2.donkeyapi", "0.36.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        int n = Mounts.Looks.RegisterEmbedded(Info.Metadata.GUID, Assembly.GetExecutingAssembly(), "MyMountMod.skins");
        Logger.LogInfo($"My Mount Mod: {n} skin pack(s) registered.");
    }
}
```

That is all. On the next start the Giant Crab is in Saddle Up's Mounts panel with its Skin, Colours and Effect
rows, its icon cut from the standing picture, its speed pinned so the player's Speed row is left out for it. A
second skin of the same mount is another folder with `mount = giant-crab` and no `[Mount]` block. Several packs in
one DLL: several folders under the same prefix.

A mod removed from the plugins folder needs nothing: the next start has no registration, and a player who had its
mount chosen rides a donkey until it is back. A plugin destroyed while the game runs (a script reloader) loses its
tuning and keys by itself, but its skins and mounts stay registered until withdrawn: `Mounts.Looks.Unregister(ownerId,
skinId)` per skin in `OnDestroy` (a mount goes with its Default skin when no other skin of yours declares it).

The same folder also works loose, as `BepInEx\plugins\gk2skins\giant-crab\` with no DLL at all: the API scans the
folder at start. A loose copy and a DLL's copy of the same id: the higher version wins, a tie goes to the DLL.

## Shape 2: a mount in code, with an ability

Code is needed only for abilities, or when the pictures are generated rather than drawn. `examples/GK2DonkeySkins`
is a buildable mod doing both; cut to the mount:

```csharp
using System;
using System.Collections.Generic;
using BepInEx;
using GK2DonkeyAPI.Api;

[BepInPlugin("you.gk2.crabmod", "Crab Mod", "1.0.0")]
[BepInDependency("dreas.gk2.donkeyapi", "0.36.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private readonly List<IDisposable> registered = new List<IDisposable>();

    private void Awake()
    {
        string guid = Info.Metadata.GUID;

        // the mount: what it is and does
        registered.Add(Mounts.Catalog.Register(guid, new MountDef
        {
            Id = "giant-crab",
            Name = "Giant Crab",
            Description = "Scuttles. Sidles faster when the keeper rides it.",
            Author = "you",
            Version = "1.0",
            Cart = true,
            Foals = true,
            FoalScale = 0.4f,
            TuningRide = new Dictionary<string, object> { ["SpeedMultiplier"] = 2f },
            Stats = { new MountStat { Label = "Legs", Value = "8" } },
            Abilities = { new SidlesWhenRidden() },
        }));

        // its Default skin: the pictures from your own sprite source (or RegisterRecipe over the donkey's frames)
        registered.Add(Mounts.Looks.Register(guid, new MountSkinDef
        {
            Id = "giant-crab",          // the mount's id: this skin is its Default
            Name = "Giant Crab",
            Mount = "giant-crab",
            CanvasWidth = 128, CanvasHeight = 128,
            SeatRow = 70,
            Source = new CrabSprites(), // IMountSkinSource: Keys, TryGetFrame(key, out Sprite)
            Description = "A crab the size of a cart.",
        }));
    }

    private void OnDestroy()
    {
        foreach (IDisposable h in registered) h.Dispose(); // wearers show the donkey again and keep their choice
        registered.Clear();
    }

    // An ability: hooks on the instance. Its values go under Owner, which the API gives it as
    // "<your guid>.ability.giant-crab" and clears when the instance stops being a crab.
    private sealed class SidlesWhenRidden : MountAbility
    {
        public override string Id => "sidles-when-ridden";
        public override string Name => "Sidles";
        public override string Description => "Half again the speed while ridden.";

        public override void Mounted(MountHandle mount, MountStyle style)
        {
            mount.Tuning.Set(Owner, "SpeedMultiplier", 1.5f * Mounts.Tuning(style).Get<float>("SpeedMultiplier"));
        }

        public override void Dismounted(MountHandle mount)
        {
            mount.Tuning.Clear(Owner, "SpeedMultiplier");
        }
    }
}
```

The other hooks: `Attached` and `Detached` (the instance becomes or stops being this mount), `Tick(mount, dt)`
every frame while ridden, or while standing too with `TicksWhileStanding`. One ability object per mount. An
ability may set the mount's speed, seat, sounds and cart geometry under its owner, and anything on the mount's own
value replaces it (a sound of its own for an event: `Mounts.Sounds.Set` on the instance, docs/mount-sounds.md); it may never set the player's skin, effect or colours (such a Set is refused and said once in the
log), and its values are never saved (set again in play).

## What the API does for either shape, unasked

- Lists the mount in the Mounts panel: its icon (a `portrait` if named, else its Default skin's standing picture),
  its description, and a line built from `cart`, `foals`, the speed, the effect it comes with, its skins, its
  `[Mount.Stats]` and its abilities' names and descriptions.
- Makes Saddle Up's rows for it: Skin, the colours, the effect, kept per mount in the cfg (`Skin_giant-crab`,
  `Coat_giant-crab`, ...). A mount registered after the page was built is caught up the next frame.
- Pins the mount's tuning on every instance of it under the API's own owner. A player's row for a pinned key is left
  out of the menus for that mount (Saddle Up's `TuningKey`); a mod's value still wins over the mount's. A mount may
  not pin a behaviour switch, a colour, a look key or the lantern's keys: refused with one warning.
- Refuses a second mount with the same id from another owner. A pack skin of the same id from another place: the
  higher version wins, a tie goes to the DLL; the log names the loser.
- Saves nothing of the mount itself. A player's kept donkey records the skin id; if the mod is gone it comes back
  as a donkey and keeps the choice for when the mod returns.
- A game-source skin (`source = game`, another character's frames) is checked at game-ready; frames the game lacks
  set it aside, the donkey comes instead, and it is offered again when they are there.

Ids: use `<author>.<name>` for a mount or skin from a mod (the API says so once in the log for a bare id), so two
authors' names never meet.

## Building one from this repo (the shipped mounts' way)

1. `python -I tools/skins/new_skin.py giant-crab --mount giant-crab --name "Giant Crab" --canvas 128 --seat 70`
   writes `src/SaddleUp/assets/skins/giant-crab/skin.cfg` in the canonical layout with the `[Mount]` block, a
   generator stub under `tools/skins/giant-crab/` drawing placeholder frames, and the Aseprite sidecar.
2. Draw the 44 frames (Aseprite: `scripts/GK2 Export skin.lua` splits the sheet into the PNGs) or extend the
   generator. docs/skins.md "The pictures" names each key and what the rider does on it.
3. Set `seat`, `[Tuning.Cart] HitchGapPx` if the body is longer than the donkey's, and the `[Colors]` regions.
4. Check: `python -I tools/skins/check_skin.py giant-crab` (the pictures), `dotnet run --project tests/SkinsScan
   -c Release` (the cfg), or `bash tools/release-scans.sh` (everything).
5. Build Saddle Up: its csproj embeds everything under `assets/skins`; its Awake registers them.
6. In the game: the F11 Mounts tab reloads the file as you edit it, nudges the seat and the geometry live and saves
   back into the same cfg; the seat and cart readout scenarios check every shipped mount.
