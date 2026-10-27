# Making a mount: how a mod registers one of its own

The one-page guide. The long forms: [skins.md](skins.md) for every key of a pack, [api.md](api.md) "Mounts" and
"Skins" for every member of the API, [mounts-as-mods.md](mounts-as-mods.md) for the rules between mods (ids, owners,
versions, load order), [mounts-design.md](mounts-design.md) for why things are as they are.

**2026-10-25 (API 0.37.0):** a mount and a skin no longer name an effect (`effect` / `effect_when` are not read,
`MountDef.Effect` and `MountSkinDef.Effect` are gone); effects are registered only, and how a skin will use one is
still to be designed (docs/open-items.md "Effects on skins").

## Two things, not one

A **mount** is the creature: its id, name, description, whether it pulls the cart, whether it has babies and how big
they are, its own tuning (speed, sounds, seat, cart geometry), free stat lines for the
Mounts panel, and abilities (code hooks). The donkey is the mount with no definition: the game's own.

A **skin** is a drawing for a mount: 44 frames on a canvas, where the rider sits on that drawing, and what the
drawing's colours mean. Every mount but the donkey has exactly one default skin, named `default`; its other skins
are built on it and draw only what they change (docs/skins.md "Variants: built on the default"). Hellfire and Royal
Parade are skins of the donkey (`donkey/saddleup.hellfire`, `donkey/saddleup.royal-parade`); the red dragon is the
dragon's default (`saddleup-dragon.dragon/default`).

The rule that ties them: **a mount is a folder with a `mount.cfg`, and its skins are the folders under its
`skins\`.** The folder `skins\default\` is the mount's own look: the one a new instance of it wears (its Default in
the Skin row); a pack mount without it is refused. A mount folder without a `mount.cfg` adds its skins to a mount
registered elsewhere (another mod's, a code mount, or the donkey's `donkey\skins\`); it may not bring a `default`,
and each of its skins is named `<mod>.<name>`, your prefix and the skin's name (`donkey\skins\me.frost\` is
`donkey/me.frost`): a skin added to a mount another mod owns carries its author, so two mods' `frost` never meet
(without the prefix it is left out with a warning). The same goes for code: a `MountSkinDef` on a mount another mod
registered, and every donkey skin and recipe (`Mounts.Looks.RegisterRecipe(Guid, "me.sepia", ...)` is
`donkey/me.sepia`), throws an `ArgumentException` without it. Your own mount's skins take any name.

**Ids.** A mount's id is `<mod>.<name>`: your mod's prefix, a dot, the mount's name (`me.giant-crab`; Saddle Up's
dragon is `saddleup-dragon.dragon`). One without a dot is refused; only the game's `donkey` is plain. A skin is named within
its mount: its folder's name, and its full name `<mount id>/<skin>` (`me.giant-crab/default`). A skin you add to a
mount another mod owns (the donkey too) must carry your prefix, `<mod>.<name>` (`saddleup-dragon.dragon\skins\me.frost\`,
`donkey/me.sepia`), so two authors' additions don't meet; without it the skin is refused. Why: a saved mount or skin
must never turn into another mod's of the same name (docs/mount-id-collisions.md).

A skin's pictures come one of three ways: a **pack** (a folder of PNGs and a `skin.cfg`, on disk or embedded in a
DLL; no code), a **recipe** (code that turns each donkey frame's pixels into new ones), or **code sprites** (a mod
hands the API its own `Sprite` per frame key). A pack is what a mod normally ships.

## Shape 1: a pack in a DLL (no mount code)

The mount and its skin are a folder of files; the mod only hands the folder to the API.

```
MyMountMod/
  MyMountMod.csproj
  Plugin.cs
  assets/mounts/me.giant-crab/
    mount.cfg                                           the mount (every key: skins.md "mount.cfg")
    sounds/click_1.ogg, click_2.ogg                     optional: the mount's sounds, for every skin of it
    portrait.png                                        optional: the panel's icon; else the default skin's standing picture is cut
    skins/default/
      skin.cfg                                          the mount's own look (every key: skins.md "skin.cfg")
      bdy_static_left.png, bdy_walk_left_01.png, ...    the 44 frames on the canvas (skins.md "The pictures")
      front/ ...                                        optional: parts drawn over the rider
```

`mount.cfg`, the creature:

```ini
[Mount]
needs = 0.40           # the GK2 Donkey API it was written for: an older one refuses it instead of loading it without [Jump]
id = me.giant-crab     # the same as the mount folder's name: "<mod>.<name>" (a different id is refused)
name = Giant Crab
description = Pulls the cart, has young, scuttles a little faster than a donkey.
author = you
version = 1.0
cart = true
foals = true
flies = true           # it flies (leave out for a mount that doesn't; jumps = false for one that never jumps)
foal_scale = 0.4
speed = 2.25           # riding, times walking speed (the donkey: 2); the panel says "Fast"
cargo = 6              # items its cart carries, 1 to 6, under the player's Cart capacity (with the cart it goes at its speed x 0.75, as the donkey)

[Stats]                # free lines for the Mounts panel
Legs = 8

[Tuning]               # the mount's own values; a mount may pin its sounds, seat, cart geometry and shuffle
BrayVolume = 0

[Jump]                 # its own jump, ridden plain (optional; a key left out is the default jump's, the donkey's)
height = 12            # a low scuttle-hop, px (the donkey: 28)
time = 0.3             # seconds in the air (the donkey: 0.45)

[Sounds]               # the creature's own sounds, shared by its skins (docs/mount-sounds.md): files of the mount
voice = click_1.ogg, click_2.ogg   # folder's sounds\, a game sound id, none, or ground; <event>.volume etc.
lips = none
```

`skins/default/skin.cfg`, its look (the one a new crab wears):

```ini
[Skin]
format = 1
needs = 0.40
name = Red Crab
description = A crab the size of a cart, sideways at everything.
author = you
version = 1.0
base = donkey          # the skeleton it animates on: the donkey's 44 frame keys
colours = own          # drawn in its own colours (palette: drawn in the donkey's)
layer = replace        # replaces the donkey's picture (over: drawn on top of it)
canvas = 128 128       # the picture size; 96 is the donkey's
seat = 70              # the canvas row the rider's hip sits on, from the top

[Tuning.Cart]          # the skin's own geometry: where the cart meets this drawing
HitchGapPx = 18

[Colors]               # what the drawing's colours mean, by region, so the player's colour rows reach them
Dark = 2A1008 3C1810
Eye = F0E040
```

The skin's mount is the folder it sits in, and its name the folder's own (no `id =` line); a `[Mount]` section in a
`skin.cfg` is ignored (the report says it goes in `mount.cfg`).

The csproj embeds the folder under a predictable name:

```xml
<ItemGroup>
  <EmbeddedResource Include="assets\mounts\**\*"
                    LogicalName="MyMountMod.mounts.%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

The files come out as `MyMountMod.mounts.me.giant-crab\mount.cfg`, `MyMountMod.mounts.me.giant-crab\sounds\click_1.ogg`,
`MyMountMod.mounts.me.giant-crab\skins\default\bdy_walk_left_03.png` and so on.

`Plugin.cs`, the whole mod:

```csharp
using System.Reflection;
using BepInEx;
using GK2DonkeyAPI.Api;

[BepInPlugin("you.gk2.mymountmod", "My Mount Mod", "1.0.0")]
[BepInDependency("dreas.gk2.donkeyapi", "0.40.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        int n = Mounts.Looks.RegisterEmbedded(Info.Metadata.GUID, Assembly.GetExecutingAssembly(), "MyMountMod.mounts.");
        Logger.LogInfo($"My Mount Mod: {n} skin pack(s) registered.");
    }
}
```

That is all. On the next start the Giant Crab is in Saddle Up's Mounts panel with its Skin and Colours
rows, its icon cut from the default skin's standing picture, its speed pinned so the player's Speed row is left out
for it. A second skin of the same mount is another folder under `assets/mounts/me.giant-crab/skins/`. Several mounts in
one DLL: several mount folders under the same prefix. A new look for a mount that isn't yours (the donkey, or another
mod's) is a mount folder with no `mount.cfg`: `assets/mounts/donkey/skins/me.<your skin>/` (your prefix on its name).

A mod removed from the plugins folder needs nothing: the next start has no registration, and a player who had its
mount chosen rides a donkey until it is back. A plugin destroyed while the game runs (a script reloader) loses its
tuning and keys by itself, but its skins and mounts stay registered until withdrawn: `Mounts.Looks.Unregister(ownerId,
skinId)` per skin in `OnDestroy`, with the skin's full name (a pack's mount goes with the last of its skins registered
by the same owner).

The same folder also works loose, as `BepInEx\plugins\gk2mounts\me.giant-crab\` (or the same under any mod's
folder, `BepInEx\plugins\<AnyFolder>\gk2mounts\me.giant-crab\`) with no DLL at all: the API scans for it at
start. A loose copy and a DLL's copy of the same
skin: the higher version wins, a tie goes to the DLL. Two `mount.cfg` of the same mount id: the higher `[Mount]
version` wins (its facts replace the other's, whose skins keep the mount), a tie goes to the DLL over a loose folder,
otherwise the first stays and the later one is refused with a warning.

## Shape 2: a mount in code, with an ability

Code is needed only for abilities, or when the pictures are generated rather than drawn. `examples/GK2DonkeySkins`
is a buildable mod doing both; cut to the mount:

```csharp
using System;
using System.Collections.Generic;
using BepInEx;
using GK2DonkeyAPI.Api;

[BepInPlugin("you.gk2.crabmod", "Crab Mod", "1.0.0")]
[BepInDependency("dreas.gk2.donkeyapi", "0.40.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private readonly List<IDisposable> registered = new List<IDisposable>();

    private void Awake()
    {
        string guid = Info.Metadata.GUID;

        // the mount: what it is and does
        registered.Add(Mounts.Catalog.Register(guid, new MountDef
        {
            Id = "me.giant-crab",       // "<mod>.<name>": an id without a dot is refused
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
            Id = "default",             // its name within the mount: "default" is the mount's own look
            Name = "Giant Crab",
            Mount = "me.giant-crab",    // registered as me.giant-crab/default
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
    // "<your guid>.ability.me.giant-crab" and clears when the instance stops being a crab.
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
value replaces it (a sound of its own for an event: `Mounts.Sounds.Set` on the instance, docs/mount-sounds.md); it may never set the player's skin or colours (such a Set is refused and said once in the
log), and its values are never saved (set again in play).

## What the API does for either shape, unasked

- Lists the mount in the Mounts panel: its icon (a `portrait` if named, else its Default skin's standing picture),
  its description, and a line built from `cart`, `foals`, the speed, its skins, its
  `[Stats]` (`MountDef.Stats` in code) and its abilities' names and descriptions.
- Makes Saddle Up's rows for it: Skin and the colours, kept per mount in the cfg (`Skin_me.giant-crab =
  me.giant-crab/default`, `Coat_me.giant-crab`, ...). A mount registered after the page was built is caught up the next frame.
- Pins the mount's tuning on every instance of it under the API's own owner. A player's row for a pinned key is left
  out of the menus for that mount (Saddle Up's `TuningKey`); a mod's value still wins over the mount's. A mount may
  not pin a behaviour switch, a colour, a look key or the lantern's keys: refused with one warning.
- Refuses a second mount with the same id from another owner, except a pack's `mount.cfg` with a higher `[Mount]
  version` (or the same version from a DLL over a loose folder), which takes over a pack's mount; a mount registered
  in code is never replaced. The same pack skin (one full name) from another place: the higher version wins, a tie
  goes to the DLL; the log names the loser.
- Saves nothing of the mount itself. A player's kept donkey records the skin's full name (`me.giant-crab/default`);
  if the mod is gone it comes back
  as a donkey and keeps the choice for when the mod returns.
- A game-source skin (`source = game`, another character's frames) is checked at game-ready; frames the game lacks
  set it aside, the donkey comes instead, and it is offered again when they are there.

Ids: a mount's id is `<mod>.<name>` (refused without a dot); a skin is named within its mount, and one you add to
another mod's mount carries your name (`me.frost`). A code mount should register a skin named `default`; if it
doesn't, its first skin by name is its default.

## Built in or external (this repo, 2026-10-26)

The repo keeps two kinds of mount pack apart by their folders (the user: "make a distinction in the directories
between inbuilt mounts/skins in saddle up vs ones which are 'external'"):

```
src/SaddleUp/assets/mounts/            built in: embedded in GK2SaddleUp.dll, ships with Saddle Up
  saddleup.gerry/   donkey/skins/saddleup.hellfire/   donkey/skins/saddleup.royal-parade/
packs/                                 external: each folder is one download, a small plugin with its mounts embedded
  SaddleUpDragon/      gk2mounts/saddleup-dragon.dragon/                 SaddleUpDragon.csproj, Plugin.cs, README.md, CHANGELOG.md
  SaddleUpCritters/    gk2mounts/saddleup-critters.rat/ .snail/ .worm/   SaddleUpCritters.csproj, Plugin.cs, README.md, CHANGELOG.md
```

- **A pack's ids carry the pack's prefix:** its name in lower case, dashes where wanted (`SaddleUpDragon`:
  `saddleup-dragon.dragon`; `SaddleUpCritters`: `saddleup-critters.rat`). A mount lives in exactly one pack for good:
  saves keep the full id. Built-in ones keep `saddleup.`. SkinsScan checks both, and that a pack's `mount.cfg` says
  `needs =` and `version =`.
- **A pack is one DLL** (the user, 2026-10-26: a folder unzipped over an older one keeps the files the new version
  dropped, and the loader would use them; one file is replaced whole). `packs/<Pack>/<Pack>.csproj` embeds
  `gk2mounts\**` as `<Pack>.mounts.<mount>\...` and `Plugin.cs` hands it to `Mounts.Looks.RegisterEmbedded` in Awake:
  a BepInEx plugin of its own (`dreas.gk2.saddleup.dragon`, `dreas.gk2.saddleup.critters`; its version in the log
  and in mod managers), `[BepInDependency]` on the API 0.40.0 (an older one is refused by BepInEx) and a soft one on
  Saddle Up (after it, which offers the mounts). Its version is `Plugin.Version`, the same as its CHANGELOG.md's first
  heading (SkinsScan and check-dist check it).
- **The dev build deploys it like any plugin** (`DeployToGame.targets`: `plugins\<Pack>\<Pack>.dll`, game closed),
  and a dev build records `GK2MountsSourceDir` (the pack's `gk2mounts\`), so the F11 Mounts tab saves into the repo's
  `packs/`. Not watched on disk while the game runs (embedded packs never are): after editing a file, build again.
- **A release** (`tools/build-dist.sh` builds every pack's DLL into `dist/`; `tools/make-release-zip.py`): Saddle Up's
  zip (the two DLLs), one zip per pack (`GK2-Saddle-Up-Dragon-1.0.0.zip`: `BepInEx\plugins\GK2SaddleUpDragon\` with
  `GK2SaddleUpDragon.dll`, `README.md`, `CHANGELOG.md`) and `GK2-Saddle-Up-All-Mounts-<v>.zip` (every pack, each in its
  own folder). `check-dist.py` checks each pack's DLL (no manifest, no game assembly, no path) and its folder (its
  docs, the version, `needs`, `version`, its `skins\default\`, nothing stray in `gk2mounts\`).
- **A new one:** `new_skin.py --mount <name> --pack <Pack> [--prefix <pack-prefix>.]` makes `packs/<Pack>/` (its
  csproj and Plugin.cs, README, CHANGELOG) and the mount in it; `--mod <project>` (or nothing) makes a built-in one.
- **The old ids** (`saddleup.dragon`, `saddleup.rat`, `.snail`, `.worm`, never released) still find the packs' mounts
  in a dev's own world and cfg (`Catalog.renamedMounts`): the Mount row writes the new id back and per-mount keys
  (`Coat_saddleup.dragon`) carry over.

## Building one from this repo (the shipped mounts' way)

1. `python -I tools/skins/new_skin.py default --mount me.giant-crab --name "Giant Crab" --canvas 128 --seat 70`
   (built in; `--pack <Pack>` for an external pack, above)
   writes `src/SaddleUp/assets/mounts/me.giant-crab/skins/default/skin.cfg` in the canonical layout, and, the mount
   being new, `src/SaddleUp/assets/mounts/me.giant-crab/mount.cfg`; a generator stub drawing placeholder frames, and
   the Aseprite sidecar. A second look of the same mount is the same command with another skin name (the mount's
   folder and `mount.cfg` are left as they are); without `--mount` the skin is the donkey's
   (`assets/mounts/donkey/skins/<prefix><skin>/`: `--prefix me.`, Saddle Up's `saddleup.` by default, goes in front of
   a skin added to a mount your mod doesn't own).
2. Draw the 44 frames (Aseprite: `scripts/GK2 Export skin.lua` splits the sheet into the PNGs) or extend the
   generator. docs/skins.md "The pictures" names each key and what the rider does on it.
3. Set `seat`, `[Tuning.Cart] HitchGapPx` if the body is longer than the donkey's, and the `[Colors]` regions in the
   `skin.cfg`; the speed, the babies and the sounds in the `mount.cfg` (the mount's sound files in its `sounds\`).
4. Check: `python -I tools/skins/check_skin.py src/SaddleUp/assets/mounts/me.giant-crab/skins/default` (the
   pictures), `dotnet run --project tests/SkinsScan -c Release` (both cfgs), or `bash tools/release-scans.sh`
   (everything).
5. Build Saddle Up: its csproj embeds everything under `assets/mounts`; its Awake registers them (an external pack:
   the build links it into the game, the API finds it).
6. In the game: the F11 Mounts tab reloads the files as you edit them, nudges the seat and the geometry live and
   saves the skin's rows back into its `skin.cfg` and the mount's into its `mount.cfg`; the seat and cart readout
   scenarios check every shipped mount.

## Living next to other mods

The API loads many mods' mounts and skins side by side (docs/multi-mod-loading.md). What that asks of yours:

- **Register in your plugin's Awake or Start.** The settings page reads every list the frame after Saddle Up's Awake.
  A mount or skin registered later (from `GameReady`, a coroutine) works in the world but has no Skin or colour rows
  until the next start; the log says so once.
- **Your prefix on every id.** Mounts, skins on another mod's mount, and effects are `<mod>.<name>`. Ids are ASCII
  letters and digits, dots, dashes and underscores, at most 64 characters, no dot at either end.
- **Your own GUID as the owner.** Two mods with one owner id replace and withdraw each other's things; the log warns
  when it sees one id from two DLLs.
- **Versions as numbers** (`1.2`, `1.2.0`): between two mods' mounts, skins or effects of one id the higher version
  wins, without a throw (the other is left out with a warning, its handle doing nothing). One that isn't a number
  ranks below any that is. A newer pack of a mount brings its facts and its pictures together: the older pack's skins
  go with it.
- **`needs =` in `[Mount]`** names the API your pack needs: an older API refuses it with the reason, instead of
  loading it without the keys it doesn't know.
- **What the loader refuses** (before anything is decoded): a frame larger than its canvas, a sheet larger than its
  cells, a portrait over 512x512, a sound file over 16 MB, NaN or Infinity anywhere a number goes, a file name outside
  the pack's own folder.
- **Other mods' instances are theirs:** `Remove(ownerId)` refuses another mod's; your data on an instance is at most
  64 keys of 64 characters, values of 4096.
- **Without Saddle Up** nothing offers your mount to ride; the log says so at game ready.
