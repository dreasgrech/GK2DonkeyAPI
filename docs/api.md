# GK2 Donkey API: guide for mod authors

GK2 Donkey API (`GK2DonkeyAPI.dll`, GUID `dreas.gk2.donkeyapi`) is a library. It gives Graveyard Keeper 2 a rideable
donkey: ridden on its back (**Ride**), or pulling its cart (**Cart**), with cargo, a lantern and sounds. On its own it
does nothing: **your mod decides when the keeper may mount, what the ride can do and how it looks.** Saddle Up
(`src/SaddleUp`) is the playable version for players and uses nothing but this API.

API version **0.29.0** (`Mounts.ApiVersion`, the same number as the mod's own; `Plugin.Version` is the one place). Everything public is in the namespace `GK2DonkeyAPI.Api`. The exact
surface is `tests/ApiSurface/GK2DonkeyAPI.Api.txt`.

## Setup

1. Reference `GK2DonkeyAPI.dll` (from the release zip, or the installed `BepInEx/plugins/GK2DonkeyAPI.dll`) with
   `Private=false`, so you never ship a copy of it.
2. Declare the dependency, then check the API version at startup:

```csharp
[BepInPlugin("you.gk2.mymod", "My Mod", "1.0.0")]
[BepInDependency("dreas.gk2.donkeyapi", "0.29.0")]          // hard: your mod needs the core, at least this version
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        if (Mounts.ApiVersion < new Version(0, 4, 0)) { Logger.LogError("GK2 Donkey API too old."); return; }
        Mounts.AddMountRule("you.gk2.mymod", style => /* ... */ null);
    }
}
```

If the mount is only an extra for your mod, use a soft dependency instead, and only touch `GK2DonkeyAPI.Api` from
code that runs when the plugin is present (`Chainloader.PluginInfos.ContainsKey("dreas.gk2.donkeyapi")`). The pattern
is in `docs/gk2-mod-framework.md`.

**Keys:** if you read a BepInEx `KeyboardShortcut` for a gameplay key, don't use `KeyboardShortcut.IsDown()`. It
also requires that no other key at all is held, so it never fires while the player walks (W/A/S/D). Check
`UnityInput.Current.GetKeyDown(shortcut.MainKey)` plus `GetKey` for each of `shortcut.Modifiers`, as Saddle Up
does.

**Rules of the road:**
- **Main thread only:** call from Update, from event handlers, and from Harmony patches of game code.
- **Owner id:** pass your plugin GUID wherever it's asked for. It's how the core tells mods apart in rules, rides,
  overrides and the log.
- **You can add restrictions but never remove the core's.** Co-op, indoors, "busy" (work, fishing, cutscenes;
  not the game's attack states: the keeper fights from the saddle, [mounted-combat.md](mounted-combat.md)), the cart
  while the keeper wears armour (fights are for the Ride style), build mode, the save hand-back and the mount-object
  check always apply. `TryMount` returns their reason like any other.
- `Mounts.IsAvailable` is false when the player switched the core off (`General.Enabled`) or its start-up check of
  the game's code stopped it (a member gone or the core's code changed since the build). Every call then refuses or
  does nothing; nothing throws. `Mounts.StatusText` says which, and names a feature the check switched off.

## Riding

| Call | What it does |
|---|---|
| `MountResult TryMount(owner, style, showMessage = true)` | Summons the donkey under the keeper. `result.Ok`, or `result.Reason`, which is the text of the keeper's thought bubble; `showMessage: false` keeps it quiet. |
| `void Dismount(reason = null)` | Ends the ride. Cargo goes back to the keeper's hands, or to the ground if his hands are full. |
| `MountResult SwitchStyle(style, showMessage = true)` | Remounts in place in the other style. The new style's rules are asked first, and if one refuses, the ride goes on unchanged. |
| `IsMounted`, `Style`, `Owner` | State. `Owner` is the id of the mod that started the ride, kept across style switches. |
| `RingBell()`, `Bray()` | Sounds, subject to the `Sounds` and `Bell` features. |

**While mounted, the keeper's hands stay empty.** Only the cart carries:
- in Cart style, pickups land on the cart (`CartSlots` slots: 6 by default, 1 to 6, three and more in layers);
- in Ride style, or with `Cargo` switched off, the game refuses pickups, and ground items show no outline;
- whatever he holds when mounting goes onto the cart, or is set down first.

Reason: the riding pose can't show a held item (see `src/GK2DonkeyAPI/PROBE-FINDINGS.md`, Round 14).

## Rules

Every rule is a function the core asks when the answer matters, so read the game's state inside it. You don't need
to track changes.

| Call | Asked | Return |
|---|---|---|
| `AddMountRule(owner, Func<MountStyle, string>)` | before every mount and style switch | `null` to allow, or the refusal text |
| `AddStayRule(owner, Func<string>)` | about twice a second while riding | `null` to keep riding, or the text shown as the ride ends |
| `AddFeatureRule(owner, MountFeature, Func<bool>)` | at mount, about twice a second while riding, on `RefreshRules()` | `false` switches that feature off |

- Every mod's rules must allow; one refusal wins.
- A rule that throws is logged once and treated as allowing.
- Each call returns an `IDisposable`: dispose it to remove the rule. `RemoveRules(owner)` removes all of yours.
- `RefreshRules()` asks the feature and stay rules now, e.g. right after your mod unlocked something.
- `IsFeatureAllowed(feature)` gives the combined answer of all rules.

**Features:**

| Feature | When off |
|---|---|
| `Cargo` | The cart carries nothing. Switching it off mid-ride puts the cargo on the ground. |
| `Lantern` | No lantern pole or lamp on the cart. |
| `LanternLight` | The lamp is dark: no lights, glow or lit glass. |
| `Sounds` | No mount sounds (hoofs, wheels, brays, graze, bell, cargo thump). |
| `Bell` | No bell (on setting off, or `RingBell`). |
| `StayThroughDoors` | The ride ends at a door that loads another scene. A house door isn't a scene change. |

To keep the player out of the cart style altogether, there's no feature; use a mount rule, which can say why:
`AddMountRule(id, s => s == MountStyle.Cart && !unlocked ? "No cart yet." : null)`.

## Events

- `Mounted(MountStyle)`: after the donkey is summoned, and after a style switch.
- `Dismounted(string reason)`: after the ride ended, with the cargo already handed back. Reasons include
  `"dismounted"`, `"style change"`, `"indoors"` and a stay rule's text.
- `CargoLoaded(CargoItem item)` and `CargoUnloaded(CargoItem item)`: an item went onto the cart or left it.
  `CargoItem` has `Id` (e.g. `"wood"`, `"body_corpse"`, `"body_zombie"`), `UniqueId` (the game's item GUID, which
  tells two zombies apart), `IsZombie` and `ZombieName`. Nothing is reported for the core's own bookkeeping, such as
  a door re-summon or the hand-back around a save.
- `ChestLoaded(ChestInfo chest)` and `ChestUnloaded(ChestInfo chest)`: a storage chest went onto a cart or was set
  down from it ([portable-chest.md](portable-chest.md)). `ChestInfo` has `Cart` (the handle), `Id` (`"chest_rough"`,
  `"chest"`, `"chest_good"`), `UniqueId` (the game's GUID of the chest object, the same on the ground and on the
  cart) and `Items` (stacks inside right now). Raised for the player's loading and for every set-down, the mod's
  fallbacks included; not for the bookkeeping around a save or a load, nor when the chest changes carts at hitching.

A handler that throws is logged and skipped.

## Tuning and behaviour

`Mounts.Tuning(MountStyle.Ride / Cart)` holds a style's 96 look and sound values. `Mounts.Behaviour` holds the
behaviour switches, which apply to both styles, and `Mounts.Shuffle` how the idle shuffle steps. All are
`MountTuning`, addressed by the value's cfg key.

```csharp
MountTuning cart = Mounts.Tuning(MountStyle.Cart);
cart.Set("you.gk2.mymod", "SpeedMultiplier", 2.5f);   // returns the value applied, clamped to the allowed range
cart.Set("you.gk2.mymod", "Pose", RiderPose.Walk);
float now = cart.Get<float>("SpeedMultiplier");       // the value in effect
Mounts.Behaviour.Set("you.gk2.mymod", "DismountIndoors", false);
Mounts.ClearTuning("you.gk2.mymod");                  // all your overrides, every section
```

- **Session only:** a `Set` overrides the player's value until `Clear(key)`, `Reset()`, `ClearTuning(owner)` or the
  end of the session. It's never written to the player's cfg.
- **Type-checked:** a wrong type throws `ArgumentException`, naming the expected type.
- **Visible in the dev panel:** an overridden row is cyan and locked, and shows the value in effect.
- **When it applies:** while mounted in that style, most values apply at once. Values the donkey is built with (the
  cart and lantern geometry measured at summon) apply from the next mount or style switch.
- **Discovering keys:** `Keys`, `Has(key)`, `TypeOf(key)`, `IsOverridden(key)` and `OverriddenBy(key)`.

**Behaviour** (`Mounts.Behaviour`, section "General"):
- `HitchKey` (0.14.0, a `KeyCode`; `None` switches the key off)
- `StayMountedThroughDoors`
- `DismountIndoors`
- `ShowMessages`
- `IdleShuffle` (0.7.0, off in the core): donkeys standing in the world **without a cart** take a few slow steps now
  and then, so they don't stand perfectly still. See "Idle shuffle" below.
- `CargoZombies` (0.22.0, on): zombies ride on the cart like any other load, as many as it holds, and are handed to their
  work straight from it ([zombies-on-cart.md](zombies-on-cart.md)). Off: the cart refuses them, and a zombie can't be
  taken while riding (neither from the ground, nor from its job, nor back from a station).
- `CorpseIntoMorgue` (0.27.0, on): a corpse on the cart goes into the keeper's hands when the ride ends at the morgue
  door, so he walks in carrying it instead of leaving it on the parked cart; `CorpseIntoMorguePlaces` (a string, the
  area ids and door destinations that count). See "A corpse into the morgue" below.
- `CartSlots` (0.28.0, 2): how many items the cart carries, 1 to 6; three and more ride in layers of up to two. Applied
  to every cart at once (standing ones too) and never below what a cart holds, so lowering it drops nothing. A chest
  aboard still takes the whole cart. See [cart-capacity.md](cart-capacity.md).

While mounted the keeper always faces only left or right, like the donkey. That isn't a setting.

**Tuning keys per style.** Find the right values with the core's dev panel: create the empty marker file
`BepInEx\config\dreas.gk2.dev` (it unlocks the `[Debug]` keys), set `Debug.TuningWindow = true` in
`dreas.gk2.donkeyapi.cfg`. Every row there is one of these keys.

| Group | Keys (type) |
|---|---|
| Speed and seat | `SpeedMultiplier` (float 0.5–4), `Depth`, `CenterOffset`, `SeatOffset` (float); `RiderOffsetPx`, `TorsoOffsetPx`, `SeatAdjustPx` (int); `AutoSeat`, `Bob`, `SnapToGround` (bool) |
| Rider motion | `BobScale`, `HandsSteady`, `HandsSwayPx` (float); `BobLagFrames`, `HeadLagFrames` (int); `HeadSettle` (float) |
| Pose | `Pose` (`RiderPose`: Walk, Idle, FishingIdle, FocusedWalk); `FreezePoseWhenIdle`, `PoseSpeedFollowsDonkey` (bool); `PoseSpeed`, `IdleFidgetEverySeconds`, `IdleFidgetSpeed`, `IdleCrawlSpeed` (float) |
| Legs | `NearLeg` (bool), `LegSource` (`LegSource`: Body, Drawn), `LegHeightPx`, `LegAdjustXPx`, `LegAdjustYPx` (int) |
| Cart on slopes | `CartFollowsGround` (bool), `CartGroundMaxShift`, `CartPitchMaxDegrees` (float), `CartSlope` (`CartSlopeStyle`: HalfLift, ShaftsTilt, WholeCartPitch) |
| Cargo | `CargoDrawCorpse`, `CargoSwaysWithCart`, `CargoSetDownBehindCart` (bool); `CargoBedDepth`, `CargoSideGap`, `CargoOverhang`, `CargoNestle`, `CargoLayerShift`, `CargoSetDownGap` (float) |
| Lantern pole | `LanternAlong`, `LanternUp`, `LanternAcross`, `LanternTilt`, `LanternLean`, `LanternSwayDegrees`, `LanternSwaySeconds`, `LanternScale` (float); `LanternShow`, `LanternFlip`, `LanternHangs` (bool) |
| Lantern light | `LanternLightOn`, `LanternLightShadows`, `LanternSelfLightOn`, `LanternGlowOn`, `LanternGlassOn` (bool); `LanternLightIntensity`, `LanternLightRange`, `LanternLightDayDim`, `LanternSelfLightIntensity`, `LanternSelfLightRange`, `LanternGlowAlpha`, `LanternGlowScale` (float); `LanternLightColor` (string, hex `RRGGBB`) |
| Sounds | `SoundsOn`, `HoofOn`, `HoofSurfaceAware`, `GrazeOn`, `BrayWhileWalking`, `BrayInSfxChannel`, `CargoThumpOn`, `MountOn`, `DismountOn`, `HitchOn`, `UnhitchOn`, `WheelOn`, `WheelPitchFollowsSpeed`, `BellOnSetOff`, `BellOnlyWithCargo` (bool); `SoundVolume` and every `…Volume`, `…Pitch`, `HoofPitchVariation`, `HoofPhase`, `WheelPhase`, `BrayOnMountChance`, `BrayIdleEverySeconds`, `BellSetOffIdleSeconds` (float); `HoofStepsPerCycle`, `WheelEveryCycles` (int); `HoofSoundId`, `GrazeSoundId`, `BraySoundId`, `CargoThumpSoundId`, `MountSoundId`, `DismountSoundId`, `HitchSoundId`, `UnhitchSoundId`, `WheelSoundId`, `BellSoundId` (string, the game's LazyAudio ids) |

## Colours

`Mounts.Colors` recolours the donkey, the cart and the lantern: one set for both styles, overridable per session like
tuning (same `MountTuning`, same rules: session only, cyan in the panel, `ClearTuning` clears it). How it works in
the game is in `docs/colors.md`.

```csharp
MountTuning colors = Mounts.Colors;
colors.Set("you.gk2.mymod", "CoatColorize", "3050C0");            // a blue donkey, its shading kept
colors.Set("you.gk2.mymod", "HarnessSwaps", "885923=C02020;D68E38=FF6060"); // two exact colours of the harness
colors.Set("you.gk2.mymod", "CartBodyHue", 40f);
foreach (MountColorRegion r in Mounts.ColorRegions)
{
    Logger.LogInfo($"{r.Key} ({r.Area}): {r.Name}, {r.SourceColors.Count} colours, e.g. key {r.KeyOf("Hue")}");
}
```

**Regions** (`Mounts.ColorRegions`, `Key` / `Name` / `Area` / `SourceColors`, the art's exact colours as hex):
- Donkey: `Coat`, `Dark` (outline, mane, hooves), `Muzzle` (muzzle and belly), `Eye`, `Harness`.
- Cart: `CartBody`, `Shafts`, `Straps`, `Hooks`, `Wheels` (3D rims and the wheel sprites), `Hubs`.
- Lantern: `Pole`, `Lamp`, `Glass` (the lamp's painted glass).

**Per region**, key = region key + setting (`MountColorRegion.SettingNames`, `KeyOf`):

| Setting | Type | Meaning |
|---|---|---|
| `Hue` | float −180..180 | hue shift, degrees |
| `Saturation`, `Brightness`, `Contrast` | float 0..3 | multipliers (1 = unchanged) |
| `Colorize` | string hex `RRGGBB`, `""` = off | this colour's hue and saturation, each pixel's own brightness |
| `ColorizeAmount` | float 0..1 | how much of `Colorize` |
| `Tint` | string hex, `""` = off | multiplied in |
| `Overlay` | string hex, `""` = off | a flat colour mixed over the result |
| `OverlayAmount` | float 0..1 | how much of `Overlay` |
| `Swaps` | string `SRC=DST;SRC=DST` | exact replacements of source colours; a swapped colour ignores the rest |

Order: a swap wins; else hue, saturation, brightness, contrast, colorize, tint, overlay.

**Globals:** `Enabled` (bool), `SpriteMethod` (`SpriteColorMethod.Palette` exact, or `Lut`, a fallback),
`GlowColor`, `LitGlassTint`, `CharacterLightColor` (hex, `""` = the game's). The lamp's main light colour is the
Cart tuning key `LanternLightColor`.

Changes show within a frame while mounted. A colour that isn't one of a region's `SourceColors` as a swap source
does nothing.

## Donkeys and carts in the world (0.6.0)

Besides the one summoned under the keeper (`TryMount`), the core keeps any number of donkeys and carts standing in
the world. Each one has its own settings. The design and every choice behind it are in
[`instances.md`](instances.md).

```csharp
MountHandle donkey = Mounts.SpawnDonkey("you.gk2.mymod", pos, withCart: false, facingRight: true);
MountHandle cart = Mounts.SpawnCart("you.gk2.mymod", pos2);
donkey.Tuning.Set("you.gk2.mymod", "SpeedMultiplier", 2.5f);     // this donkey only
donkey.Tuning.Set("you.gk2.mymod", "CoatColorize", "C040FF");    // any key of Tuning, Behaviour or Colors
Mounts.Hitch(donkey, cart);                                       // cart, cargo and cart settings join the donkey
MountHandle left = Mounts.Unhitch(donkey);                        // the cart stays standing, a free cart again
```

**Spawning and reading**

| Member | Does |
|---|---|
| `Mounts.SpawnDonkey(owner, position, withCart = false, facingRight = true)` | A standing donkey (with its cart) in the keeper's scene; the ground height is found under `position`. Null outside a game or while the core is off. |
| `Mounts.SpawnCart(owner, position, facingRight = true)` | A cart on its own: the donkey prefab with the donkey's renderers off, the shafts resting on the ground. |
| `Mounts.Instances` | Every one standing, in spawn order (not the `TryMount` summon). |
| `Mounts.Riding` | The ridden one, or null (on foot, or riding the `TryMount` summon). |

**Hitching**

| Member | Does |
|---|---|
| `Mounts.Hitch(donkey, cart)` | The donkey takes the free cart, which is gone afterwards. Its cargo and its cart keys come too. A standing donkey moves to the cart; a ridden one is put there with the keeper. |
| `Mounts.Unhitch(donkey)`, `Mounts.UnhitchRidden()` | The cart stays standing where it is, as a new free cart with the cargo, the chest and the cart keys; that cart is returned. The ride goes on in Ride style. A standing donkey's cart is left the same way, also without a view (asleep far away, or in a scene that isn't loaded): the split is data only and the views follow (0.25.0). |

**`MountHandle`**

| Member | Does |
|---|---|
| `Id`, `Owner`, `Exists`, `HasDonkey`, `HasCart`, `IsRidden` | Identity and state. |
| `SceneId`, `Position`, `FacingRight` | Where it stands. |
| `Cargo` | What is on its cart, in load order (`CargoItem`: `Id`, `UniqueId`, `IsZombie`, `ZombieName`). |
| `Chest` | The storage chest riding on its cart, or null (`ChestInfo`: `Cart`, `Id`, `UniqueId`, `Items`). While a chest rides, `Cargo` is empty. |
| `Tuning` | Its own settings (`InstanceTuning`: `Get`, `Set`, `Clear`, `Reset`, `IsSet`, `SetBy`, `Keys`). Keys are those of `Mounts.Tuning(style)`, `Mounts.Behaviour`, `Mounts.Shuffle` and `Mounts.Colors`; a style key applies in whichever style it is in. This donkey's value wins over everyone's (cfg and session overrides). |
| `Ride(showMessage)` | The keeper gets on and is put on the seat. The same checks and rules as `TryMount`. |
| `MoveTo(position, facingRight)` | Moves a standing one. |
| `IsReady` | Its view is loaded in the keeper's scene: `Ride` works. It takes a few frames after a spawn, `ComeToKeeper`, a hitch or an unhitch. |
| `ComeToKeeper(facingRight, forRide = true, showMessage = true)` | It comes to the keeper, into his scene, to where a ride would put it, so `Ride` right after moves no one. Saddle Up calls its donkey this way. A donkey called while the keeper is on foot is called for a ride: when a ride would be refused there (indoors, busy, building, co-op, a mod's rule, too small to ride), it stays where it is and the refusal is returned and shown like a ride's. `forRide: false` brings it anyway. A cart, or anything called while the keeper rides, always comes. |
| `LoadFromHead(showMessage = true)` | On foot at this standing cart: the topmost item the keeper holds that the cart takes goes onto it, one per call (with the perk for several held items, call again for the next). Refused while the keeper rides, for a ridden or cart-less one, in another scene, when the cart is full or takes none of what is held (zombies with `CargoZombies` off), or when a rule switched `Cargo` off. The keeper's controls call it ("[E] Load on cart"). |
| `TakeToHead(showMessage = true)` | On foot at this standing cart: its top item into the keeper's hands, with the game's own pickup. Refused as `LoadFromHead`, and when the hands are full or the cart is empty. The keeper's controls call it ("[E] Take from cart"). |
| `SetDownCargo()` | Everything on its cart is set down on the ground at the cart. Call it before `Remove` to keep the cargo (zombies are always set down by `Remove`: a zombie is never thrown away). |
| `Remove()` | Gone, with anything on its cart; a ridden one is dismounted first. |

**Events:** `InstanceSpawned`, `InstanceRemoved`, `Hitched(donkey, oldCart)`, `Unhitched(donkey, newCart)`. The
ride's own `Mounted` and `Dismounted` come for instances too.

**Behaviour and limits**

- **Dismounting** an instance (any dismount, Saddle Up's key included) leaves it standing, cart and cargo
  included. `SwitchStyle` on a ridden instance: to Ride unhitches, and to Cart is refused (hitch a cart).
- **Carts:** the cargo goes onto the ridden cart only; a standing or free cart takes no pickups.
- **Keys that travel with the cart:** `Cart*`, `Cargo*`, `Lantern*`, the Cart and Lantern colour regions, and
  `GlowColor`, `LitGlassTint`, `CharacterLightColor`.
- **Saves:** at each save their cargo is set down at their cart (in the save) and taken back right after, so a
  reload finds the items where the cart stood. Instances spawned with `Persist` (0.9.0, below) come back when that
  save is loaded, with the cargo back on the cart; the others don't.
- **In-game controls:** `General.InstanceControls` (on) makes the interact key ride a standing donkey and hitch a
  free cart. `General.HitchKey` (J) unhitches, and hitches too.

## Idle shuffle (0.7.0)

A standing donkey without a cart takes a few slow steps now and then, inside a small box around its home spot.

- **Switching it on:**

  ```csharp
  Mounts.Behaviour.Set("you.gk2.mymod", "IdleShuffle", true);       // every standing donkey without a cart
  donkey.Tuning.Set("you.gk2.mymod", "IdleShuffle", true);           // or just this one (a MountHandle)
  donkey.Tuning.Set("you.gk2.mymod", "ShuffleAreaWidth", 0.6f);      // its own box, speed, pauses, ...
  ```

- **Who moves:** only a donkey with no cart, not ridden and not the `TryMount` summon. A donkey with a cart and a free
  cart never move. It starts when its cart is unhitched and stops when one is hitched.
- **Its home spot** is where it was spawned, left by a dismount, or last moved (`MoveTo`, `ComeToKeeper`). It never
  drifts away from it.
- **Never straight up or down:** every step is within `ShuffleMaxAngle` of the screen's horizontal, and the donkey
  always faces the way it walks. It turns around first when there is no room ahead, or by `ShuffleTurnChance`.
- **Ground:** each step checks for level ground and nothing in the way along its line. Nothing in the way means no
  keeper-blocking collider, the game's own test for where the keeper can stand.
- **It holds still** while the keeper is within `ShuffleKeeperDistance`, so riding it doesn't chase a moving target,
  and while the game is paused.
- **No game events:** the core moves the donkey itself. The game's `MovementComponent` isn't used, because it fires a
  global "go-to finished" event under the definition id `npc_donkey`, which quests listen for.
- **`MountHandle.IsStepping`** is true during a step.

**`Mounts.Shuffle` keys** (section "Shuffle", all float): `ShuffleAreaWidth` (1, sideways either way),
`ShuffleAreaDepth` (0.5, up/down either way), `ShuffleStepMin` / `ShuffleStepMax` (0.3 / 0.9), `ShuffleSpeed` (0.5
units/s), `ShuffleAnimSpeed` (1 = the walk cycle matches the ground), `ShufflePauseMin` / `ShufflePauseMax` (5 / 15
s), `ShuffleMaxAngle` (35°), `ShuffleTurnChance` (0.25), `ShuffleKeeperDistance` (2).

## Foals and spawning with every property (0.8.0)

```csharp
MountHandle mother = Mounts.SpawnDonkey("you.gk2.mymod", at);
MountHandle foal = Mounts.SpawnFoal("you.gk2.mymod", mother);        // size 0.6, not rideable, shuffles and follows
MountHandle big = Mounts.Spawn("you.gk2.mymod", new MountSpawn { Position = at2, Scale = 1.4f, Rideable = false });
```

- **`MountSpawn` fields:**
  - `Position`, `FacingRight`, `Donkey` (false: a free cart), `WithCart`.
  - `Scale` (0.2–2).
  - `Rideable`.
  - `Parent`, and `RemoveWithParent` (default true).
- **Scale other than 1:** only for a donkey without a cart that can't be ridden. The ride and the cart are made for a
  full-size donkey.
- **A not-rideable instance** has no interact prompt, and `Ride` and `Hitch` refuse it.
- **A child's idle-shuffle home is its parent.**
  - While the way is clear it trots after the parent (`ShuffleFollowSpeed`), within `ShuffleFollowMaxDistance`.
  - Otherwise it waits, shuffling where it is, and catches up out of sight once the parent stands again (into the
    parent's scene too).
  - No chains: a child can't be a parent. Any number of children: their spots form rows further out.
  - The child's shuffle must be on for it: `SpawnFoal` switches it on for the foal only.
- **`SpawnFoal`** also gives the foal its own shuffle settings (smaller, quicker steps, a wider area around the
  parent), owned by you and changeable through `MountHandle.Tuning`.
- **`Mounts.ClearTuning(yourId)`** also clears the foal's own `IdleShuffle` set by your `SpawnFoal`. The foal then
  stands still; set it again if you clear your tuning mid-session.
- **On `MountHandle`:** `Scale`, `IsRideable`, `Parent`, `Children`. `IsStepping` is also true while trotting after
  the parent.
- **Indoors with the keeper (0.18.0):** with the `Mounts.Shuffle` key `ShuffleFollowKeeperIndoors` on for a foal
  (`foal.Tuning.Set(yourId, "ShuffleFollowKeeperIndoors", true)`; off in the core), it comes through a door with the
  keeper when it's within `ShuffleFollowMaxDistance` of him. That covers buildings, the church and dungeons, which
  are the game's indoor places, the same test as `DismountIndoors`. Inside it follows him instead of its parent,
  comes back out with him, then goes back to its parent. `MountHandle.IsWithKeeper` says whether it's with him now.
  It's moved while the door's fade is black. Being with the keeper isn't saved: after a load indoors, the flagged
  foals near him are with him again. See [foals-indoors.md](foals-indoors.md).

## Several mods, ids and the save (0.9.0, 0.10.0)

```csharp
const string Me = "you.gk2.mymod";               // your plugin GUID: the owner of everything you spawn

Mounts.GameReady += () =>                        // once per loaded game, after the save's kept donkeys are back
{
    MountHandle d = Mounts.Find(Me, "stable-donkey");          // yours from the save, if any
    if (d == null)
    {
        d = Mounts.Spawn(Me, new MountSpawn { Position = at, Id = 1, Key = "stable-donkey", Persist = true });
        d?.SetData(Me, "name", "Bella");
    }
};
```

**Identity: each mod numbers its own donkeys.**
- **An instance is `Owner` plus `Id`:** your GUID and a number counted for you alone. Two mods can both have a #2.
  `FullId` gives `owner#id`, which is what the logs and the panel show.
- **Choosing the number:**
  - `MountSpawn.Id = n` asks for #n. It is refused when you already have #n, also when a dormant record (below)
    holds it.
  - With 0, the core gives your next number. Your counter is kept with the save and never hands out a number twice,
    so a number you stored in your own data never points at another donkey later.
- **Finding:** `Mounts.Find(owner, id)`, `Mounts.Find(owner, key)` and `Mounts.InstancesOf(owner)`.
- **`Key`:** your own name for it, unique within your instances. A spawn or `SetKey` with a key you already use is
  refused.

**`Mounts.GameReady` and `IsGameReady`.**
- **`IsGameReady`** is false in the menu, while a game loads, and for a frame at a door that loads another scene. In that
  frame there is no current scene.
- **When it comes:** once per loaded game (a save, or a new game), after the game's own `MainGame.OnGameStarted` and
  after the kept donkeys are back. `InstancesRestored`, with the list that came back, comes just before it.
- **Spawning before it is refused:** `Spawn` returns null, and the log says "Not until the game is ready". Without
  that, a mod could take a number a kept donkey of the save has.
- **The place for your per-game spawning:** `Find` first, spawn what's missing.

**`Persist`: kept with the save.**
- **Setting it:** `MountSpawn.Persist`, or `MountHandle.SetPersist(yourId, bool)`. Only the owner can change it,
  and the core's panel.
- **What a kept instance keeps:** after each save it is written to the slot's side file, and loading that save
  spawns it again with:
  - the same owner, number and key;
  - its place and scene;
  - its cargo, back on its cart;
  - its own settings (`Tuning`) and every mod's data on it.

  The game's save itself is never changed.
- **Off by default.** A mod that spawns its donkeys again each session must not also set it, or there are two of
  each. Use `GameReady` and `Find` instead.
- **Children:** `SetPersist` on a parent also sets its children of the same owner. `SpawnFoal(..., persist: null)`
  takes the parent's.
- **Carts:** a cart left by unhitching takes its donkey's owner and `Persist`. A cart left by the summon of
  `TryMount` is the core's, and kept.
- **Hitching a kept free cart to a donkey that isn't kept:** the pair isn't kept, and its cargo lies on the ground
  after a reload.

**A mod's own data on an instance.**
- **The calls:** `SetData(yourId, key, value)` (null clears), `GetData(yourId, key)` and `DataKeys(yourId)`. Strings,
  kept with the save.
- **Namespaces:** each mod has its own, so two mods can't overwrite each other. Any mod may keep data on any
  instance, for example a naming mod naming other mods' donkeys.
- **Hitching** merges the free cart's data into the donkey's, without replacing the donkey's values.

**Far donkeys do less** (docs/performance.md, "Levels"): off screen they stand frozen, and far away (beyond about 45
units) they have no view at all. An asleep donkey's `IsReady` is false and `Ride` refuses; `MoveTo` or `ComeToKeeper`
brings it near, and it wakes on the next frame. New views are made a few per frame, so spawning many at once spreads
over some frames.

**An owner that isn't installed.** Its kept instances are **dormant** after a load:
- they aren't spawned, but are written back into the side file unchanged, with their number and key reserved;
- they come back when the mod does;
- a foal whose parent is dormant is dormant too;
- the panel lists them, with "Forget".

Their cargo lies on the ground where the save has it. It is taken back only if it still lies within 3 units of the
cart. A corpse there is removed by the game's own corpse timer.

**Also:**
- `MountSpawn.SceneId`: spawn in another scene than the keeper's. The height is used as given there.
- `MountHandle.RemoveWithParent`: what was set at spawn.
- **The file:** `<the game's save folder>/<slot>.gk2donkeyapi.dat`, a text file, synced by Steam Cloud with the saves.
  It is written only after the game reports the save written, holds every mod's instances, and is deleted with the
  slot.
- **A stale file is ignored.** It records the slot's save time and day; a loaded slot with others (saved without the
  mod, a backup, a cloud copy) restores nothing, and the cargo lies on the ground where the carts stood.
- Details: [`persistence.md`](persistence.md). The test mod
  [`examples/GK2DonkeyHerd`](../examples/GK2DonkeyHerd/Plugin.cs) uses all of it.

## A player mod in a few lines: the companion and the settings page (0.14.0; key actions and Status 0.16.0)

Most player mods want the same thing: the keeper's own donkey, called with a key, with its cart, kept with the save,
and settings a player can find. The API does all of that; your mod lists its settings and what its keys do, all in
`Awake`, with no `Update` or `OnDestroy` of its own. `src/SaddleUp/Plugin.cs` is the whole of such a mod.

```csharp
MountCompanion me = Mounts.Companion("you.gk2.mymod");               // one per mod
MountSettingsPage p = Mounts.SettingsPage("you.gk2.mymod", Config, "My Donkey Mod", "you", "What it does.");
p.Status("Keys");                                                    // "Active", or why the API is off
p.Key("Keys", "RideKey", new KeyboardShortcut(KeyCode.H), "Ride", "...", pressed: () => me.Use(MountStyle.Ride));
p.Choice("Donkey", "Coat", CoatColor.Game, "Coat", "", v => me.Coat = v, new[] { "Game default", "Brown", "Dark", "Cream", "Black", "Custom" });
p.Steps("Donkey", "Foals", me.FoalCount, 0, MountCompanion.MaxFoals, 1, "Foals", "...", v => me.FoalCount = v);   // defaults: the companion's own values
p.Publish();
```

- **Keys:** the API polls a `Key` row's key while it's on and calls `pressed`. A key counts with its own modifiers
  held, whatever else is held (walking): `KeyboardShortcut.IsDown()` never fires while a movement key is down.
- **Unloaded:** when your plugin is destroyed (a script reloader), the API clears your tuning
  (`Mounts.ClearTuning`) and drops your keys.

**`MountCompanion`** (`Mounts.Companion(owner)`):

| Member | Does |
|---|---|
| `Use(MountStyle)` | The ride key (Ride) or the cart key (Cart). On foot: the donkey (spawned the first time) comes and the keeper gets on; for Cart its cart is brought and hitched, for Ride a cart the donkey stands hitched to is left where it was parked and the donkey comes alone (`CartStaysParked`, 0.25.0; off, or before: both come and the cart is left standing by the keeper). Mounted in that style: off (the donkey stays). Mounted in the other style: the cart is left standing, or brought and hitched. Refusals come back as `MountResult` (indoors, busy, ...) and are written to the log with their reason. |
| `RingBell()` | The bell, while mounted; else nothing. |
| `SendAway()` | The donkey, cart and foal go; the cart's load is set down first. |
| `Donkey`, `Cart`, `Foals` | The handles (`Cart` while it stands on its own; hitched, it's part of `Donkey`; `Foals` a slot per foal, null when that one isn't there). Found again after a load by the keys `"donkey"`, `"cart"`, `"foal"`, `"foal2"` ... `"foal5"`. A cart the donkey hitches in the world that this companion did not summon is not `Cart`: it keeps its own look while pulled and stays a cart of its own when left again (the companion's own cart waits where it was parked). |
| `KeepWithSaves` | Kept with the save (on). |
| `CartStaysParked` | `Use(Ride)` while the donkey stands hitched to its cart somewhere: the cart is left where it was parked, with its load and chest, and the donkey alone comes. Off: both come and the cart is left standing by the keeper. On (0.25.0). |
| `FoalCount` | 0 to `MaxFoals` (5) foals (size 0.6, not rideable) that stay with the donkey. |
| `FoalsComeIndoors` | The foals come through doors with the keeper, follow him inside and come back out with him (`ShuffleFollowKeeperIndoors` on each foal). Off. |
| `Name` | Shown in the prompt: "[E] Ride Bella" (`MountHandle.SetDisplayName`). |
| `RideSpeed`, `CartSpeed` | `MountSpeed.Normal / Faster / Fastest` (players' words: `SpeedLabels`, or with each step's speed: `SpeedLabelsFor(style)`, "Normal (x2 walking speed)", Normal being the player's own cfg value, rounded to 0.05), riding the donkey alone / driving the cart: Normal is the player's own speed (shipped: 2 Ride, 1.5 Cart); Faster 2.25 (Ride) / 1.75 (Cart); Fastest 2.5 / 2. |
| `Brays`, `BellOnSetOff` (off to start with), `SoundVolume` (0–1.5), `Lantern`, `LanternByDay` | The ride's sounds and lamp. |
| *(every setting)* | Starts at the default a mod's settings should use: pass the property as the row's default (`p.Toggle(..., me.BellOnSetOff, ...)`), so it's written once, in the API. |
| `Coat`, `Harness`, `Paint`, `Wheels`, `Hubs`, `LanternLight` | Look presets (`CoatColor`, `HarnessColor`, `CartPaint`, `WheelColor`, `HubColor`, `LanternColor`); `Game` is the game's art. The paint is the cart body and shafts; the wheels' wood and their hubs have their own (0.19.0). |
| `CustomCoat`, `CustomHarness`, `CustomPaint`, `CustomWheels`, `CustomHubs`, `CustomLantern` | Hex `RRGGBB`, used with `Custom`. Empty or invalid means the game's look; an invalid one is logged once. |

Where they apply:
- **The looks, the name and the foal** are this companion's own donkey, cart and foal only (instance overrides): other
  mods' donkeys keep theirs.
- **Speed, sounds and the lamp** are the ride's (session overrides of `Mounts.Tuning(Ride / Cart)`), since a style
  value can't differ per instance.
- **Set by you**, as your owner id, so the dev panel shows who set them. A value another mod set is never cleared.

**`MountSettingsPage`** (`Mounts.SettingsPage(owner, Config, title, author, description, status)`; without `status`,
`Mounts.StatusText`: "Active", or what the API is doing instead and why: a feature off after a game update, stopped, or switched off):

| Row | Binds | In the game's Settings window |
|---|---|---|
| `Toggle(section, key, value, label, description, apply)` | `ConfigEntry<bool>` | Off / On |
| `Choice<T>(..., apply, labels)` | `ConfigEntry<T>` (an enum); `labels` are what players see | the labels |
| `Steps(section, key, value, min, max, step, ..., apply, format)` | `ConfigEntry<int>` | each step, `format` turns it into text ("100%") |
| `Key(..., apply, pressed)` | `ConfigEntry<KeyboardShortcut>`; `pressed` runs when the player presses it | either arrow, then the key ("Press a key..."; Backspace: none, Esc: cancel). Plain Backspace always means no key (the Mods menu takes it as a key; it's cleared at once), and a note saying so ("While setting a key, Backspace clears it and Esc cancels.") is added before your first key |
| `Status(section, label = "Status", description)` | nothing: the page's status | read-only |
| `Text(..., apply, visible)` | `ConfigEntry<string>`; `visible` shows it only while it says so | read-only |
| `Info(section, label, value)` | nothing | read-only |
| `Color(section, key, label, description, PaletteColor, apply, visible)` | `ConfigEntry<PaletteColor>` `<key>` (24 named colours: Chestnut, Sand, Moss, Sky, ...) and `ConfigEntry<int>` `<key>Lightness` (40–160%); `apply` gets the colour as hex `RRGGBB` | the colour by name, its lightness |
| `Choice` / `Steps` with `look: true` | as above | also in the look bar |
| `Preview(section, companion, label = "Your donkey", cart = true)` | a row with the picture; call once per section that should show it. `cart: false`: that row shows the donkey alone. The picture also goes above the Mods window when there's room | a picture of the companion's donkey and cart in its current colours next to the settings, in the look bar too. No donkey needed: the art comes from the game's files, the colours from the companion |
| `LookButton(section, label, description)` | nothing | a button: the menu closes and the look bar opens over the game |

- **In the Mods menu:** your mod lists `dreas.gk2.donkeyapi` as its dependency, and GK2 Donkey API has its own entry
  in the list (no settings). While the API is off or unconfirmed for the game build, the menu marks your mod
  "Dependency unavailable".
- **Binding:** each row binds an entry in your cfg and runs `apply` now and on every change, from the Mods menu, the
  Settings window or the cfg.
- **Sections keep your order:** the cfg section is numbered in the order you first use them (`1. Keys`,
  `2. Behaviour`, ...), because the framework's Mods menu sorts sections by name and offers no other way to order
  them. The game's Settings window shows the plain names.
- **`Publish()`, once, after the rows, from your `Awake`:**
  - with GK2 Mod Framework installed, your mod gets an entry in its **Mods** menu, grouped by section (the framework
    sorts the sections by name);
  - without it, your rows go into the game's own **Settings** window, on pages of their own: a "Showing" row at the
    top switches between the game's settings and yours. They're copies of the game's own rows (look, sounds,
    gamepad). Each page holds as many rows as the game's own page; only the rows showing count, and the pages are
    planned again when rows show or hide: the fewest pages, a section going on over the next page ("(more)") only
    when keeping it whole would take another page, at least two rows on each side of a cut.
- **The framework is never required:** the API's link to it is soft, and if it's missing or fails, the Settings
  window is used.
- **The look, three ways:**
  - **named colours:** a colour by name and its lightness; the picture shows the result;
  - **the picture:** the donkey and its cart walking on the spot, in the companion's colours (its "paper donkey", an
    instance never spawned, carrying only its look). A stripped copy of the game's own donkey
    (`Assets/AddressableAssets/WGOs/npc_donkey.prefab`, through Addressables) is drawn by a hidden camera at the
    game's angle, at 3 picture pixels per art pixel, so it shows in the main menu and indoors too. In the Mods menu it
    goes into its rows in the list, one per `Preview` call, and above the window too when it fits there (0.75
    canvas units per art pixel or more: 1440p and up, not 1080p). Above the Settings window without the framework,
    and at the top of the look bar;
  - **the look bar:** the look rows at the bottom of the screen over the game, with Done and Cancel. Cancel puts
    back what was set when it opened. Only in a game.
- **The picture and the bar are extras.** They're found and built defensively. If something about them fails
  (after a game or framework update), they don't show and the log says so once; the settings work as before.
- **`status`:** shown as a "Status" row. "Active" lets the Mods menu confirm the game build; anything else shows
  "Unknown build" there.

**Also new in 0.14.0:**
- `MountHandle.DisplayName` and `SetDisplayName(owner, name)` (owner only, at most 24 characters, kept with the save).
- `Mounts.Behaviour` has `HitchKey` (a `KeyCode`).

## Corpses at the crossroads (0.26.0)

The game's own delivery donkey, made to leave the bodies it brings **at the crossroads by the nun**, where it appears,
instead of putting them on the morgue pallets, so the keeper hauls them himself (the cart takes them). Off in the
core; a mod switches it on for its player. How the game delivers and what the feature changes, in full:
[corpse-delivery.md](corpse-delivery.md).

| Member | What it does |
|---|---|
| `Mounts.Corpses` | A `MountTuning` over the `[Corpses]` cfg section, overridable per session like `Behaviour`. Keys: `CorpsesAtCrossroads` (bool, off: the switch), `CorpsesUnloadAtCrossroads` (bool, on: the donkey walks out from behind its tree to the stand point, unloads there and walks back; off: it walks to the morgue as in the game and the bodies still appear at the drop point when it arrives), `CorpsesStandPoint` (string GD point id, `donkey_01_01`), `CorpsesDropPoint` (string, `nun_body_drop_point`), `CorpsesNeverRot` (bool, off), `CorpsesNotification` (string; "" = the game's "New Body"), `CorpsesUnloadDelay` / `CorpsesLeaveDelay` (float seconds, 1.5 / 1; only when it unloads on the spot). |
| `Mounts.CorpsesActive` | Whether the delivery is hooked right now: the switch on, the API active, the four patches in. |
| `Mounts.CorpseLeft` | Event, a `CorpseInfo` (`Id`, `UniqueId`, `Point`, `Position`) per body left on the ground. |
| `Mounts.StartCorpseDelivery()` | Debug aid: the game's own `full_cycle` event on the donkey with its body roll forced; not sent while the donkey is already out. Returns what happened, for the log. |

- **Off means untouched.** The four prefixes (the graph's pallet node, its own fallback drop node, its walk node, the
  "New Body" notification) are applied when `CorpsesAtCrossroads` turns on and removed when it turns off (`Corpses.Tick`,
  every frame). A missing patch target after a game update switches this feature off for good and leaves the rest of
  the API running; so does a changed one, found by the start-up check (`docs/dependency-check.md`).
- **The game's rules stay:** deliveries every second day in the early afternoon, the body roll and its chance, the
  morgue's capacity (bodies on the ground count, so deliveries thin out and then stop until they're hauled), and the
  five quests that keep the donkey at the morgue for a scripted talk (`21_base_donkey_nun` awaiting;
  `137_base_resurrection_donkey`, `165/166/167_base_donkey*_speak` in progress): then it walks there as in the game.
  Both quest triggers (`DonkeyStart`, `DonkeyMorgue`) still fire in their order.
- **Nothing of the mod's own in the save.** The body is the game's generated instance, dropped by the game's drop
  system (what the game does itself when the pallets are full); a body left out of sight for about 75 minutes of
  play turns into a grave mound as the game has it (`CorpsesNeverRot` switches that off). The on-the-spot unload is
  timed with the game's own saved delayed WGO events; loaded without the mod they resolve as the game's own.
- Saddle Up: the Behaviour toggle "Donkey leaves corpses at the crossroads" (off) sets `CorpsesAtCrossroads`; the
  finer keys are the API's cfg and the panel's "Corpses at the crossroads" rows.

## A corpse into the morgue (0.27.0)

The way back from the crossroads: a body carted to the morgue. Driving the cart through the morgue door, the ride ends
at the door as it always does (`DismountIndoors`; a door that loads another scene ends it too) and the cart is parked
outside with its load. With `CorpseIntoMorgue` on (the core's default) the corpse comes off that parked cart into the
keeper's hands in the same breath, so he arrives inside carrying it. Code: `src/GK2DonkeyAPI/MorgueHandover.cs`.

- **Only the morgue.** It is the one building a body is worked in (the pallets, the autopsy table, the crematorium); in
  a house or the church a body in hand is of no use, and no other cargo comes inside either. `CorpseIntoMorguePlaces`
  (default `morgue, tp_RT_morgue_exit, tp_morgue_to_graveyard`) says which: building area ids, as the game tracks the
  keeper's area after the door (`PlayerData.CurrentWorldZoneData`, read from a raycast every physics step, so a moment
  after the door), and door destinations (the teleport point ids the game's door expressions pass:
  `TeleportTo("tp_RT_morgue_exit", "dungeons", "door")` is the morgue's entrance seen from inside, known the moment the
  ride ends, while the screen is still black). Either one matching is enough; the decision waits at most a quarter
  second after control is back. Only after a door: a ride that ends "indoors" without a teleport in the last minute
  (a script's indoor preset) hands nothing over. Not the morgue: nothing happens.
- **Only bodies** (`body_corpse`), as many as his hands take: one, more with the game's `extra_overhead`. Zombies and
  everything else stay on the cart. Nothing is dropped and nothing is lost: a body the hands refuse goes back onto the
  cart. The game's own pickup runs (its sound, `AddOverhead` trigger), so the body shows on his shoulder as a body he
  picked up.
- **Instances only.** The old summon hands its whole load back at every door anyway. A parked cart raises no cargo
  event for this (as `Park` raises none).
- **On the way out** nothing new: getting back on with the body in hand loads it onto the cart, as always.
- Saddle Up: the Behaviour toggle "Carry the corpse into the morgue" (on).
- Tested in game 2026-10-10: the door signal decided on the dismount frame, while the screen was still black; the
  morgue's inside is at about (-72.5, 0, -388.7) in `RuinedTemple`, lit with the `dungeons` preset.
- Every door while riding writes `Mount: a door while riding: to <point> in <scene> (preset <p>); N item(s) on the
  cart.`, and a door with a corpse aboard then `... goes into the keeper's hands if this is the morgue.` followed by
  either the hand-over line or `the corpse stays on ...: not the morgue (area <a>, door to <d>)`.

## Gating on the game's progress

All of this is the game's own state, reached through `MainGame.Instance.GameSave` (reference `Assembly-CSharp.dll`).
Content ids are data, listed at runtime with `GameBalance.Me.techDefs`, `questDefs`, `buildingDefs`, `craftDefs` and
so on. The full survey is in `docs/api-design.md`.

| Progress | Read it in a rule | React at once (optional, then `Mounts.RefreshRules()`) |
|---|---|---|
| Tech tree | `GameSave.knowledgeSystem.IsTechUnlocked("fishing_3")` | static `KnowledgeSystem.OnTechUnlocked += (id, silent) => ...` |
| Quests | `GameSave.questSystemData.IsQuestInStatus(id, QuestStatus.Completed)` | per save: `questSystemData.OnQuestCompleted` |
| Story flags | `MainGame.PlayerData.GetRes("donkey_ready") > 0f` | per save: `PlayerData.OnGameResChanged` |
| Buildings, crafts | `knowledgeSystem.unlockedBuildings.Contains(id)` / `unlockedCrafts` | none |
| Days played | `GameSave.environmentData.Day` | static `EnvironmentEngine.OnNewDayStarted` |

- **Before a save is loaded,** `GameSave` is null or empty, so a rule that reads it should treat that as "not yet"
  (as the example does). Rules are asked again at mount time.
- **Per-save events:** the save object is replaced on every load, so subscribe to its events again in
  `MainGame.OnGameStarted`. The static events are subscribed once.
- **Never evaluate the game's expression strings (`LazyExpression`) as conditions.** The same engine can unlock
  techs, start quests and set flags, so evaluating one can change the save.
- **The story's own donkey** probably arrives with the `21_base_donkey_*` quests, and the flag `donkey_ready` is set
  by its script. Both come from scans of the game data; confirm them by listing `GameBalance.Me.questDefs` in your
  game before relying on them.

## Examples in this repo

- **`examples/GK2DonkeyTechGate`:** the cart needs a tech (a mount rule with a reason) and the lamp's light needs a
  tech (a feature rule). It refreshes on `OnTechUnlocked` and logs your unlocked techs at every game start.
- **`src/SaddleUp`:** the playable mount. It owns the keys (ride H, cart G, bell) and hands its behaviour
  settings to the core with `Mounts.Behaviour`.
- **`tools/GK2DonkeyAPIProbe`:** a dev test of every part of the API on F-keys: F6 mount, F7 switch style, F8 tuning,
  F9 a blocking rule, F10 the features one by one.

## Versioning

- `ApiVersion` is the mod's version: one number (`Plugin.Version`), shown by BepInEx and read by your mod. Until
  2026-10-13 the plugin was 0.1.0 with the API counted apart.
- New members bump the minor version, and a change to existing members bumps the major.
- **Before 1.0**, a breaking change bumps the minor, as semver allows for 0.x, and it's listed here:
  - **0.4.0:** `Mounts.BuiltInKeyOwner` was removed (the core has no keys), and `MountTuning.Style` became
    `MountStyle?` (null for `Behaviour`).
- `tests/ApiSurface` fails on any change to the public surface, so every change is a reviewed diff.

History:
- 0.1.0: mount, dismount, switch style, and the mount and stay rules.
- 0.2.0: features and cargo events.
- 0.3.0: tuning.
- 0.4.0: behaviour, and the core without keys.
- 0.5.0: colours (`Mounts.Colors`, `Mounts.ColorRegions`, `MountColorRegion`, `SpriteColorMethod`); the style keys `CargoSetDownBehindCart`, `CargoSetDownGap`.
- 0.6.0: donkeys and carts in the world (`SpawnDonkey`, `SpawnCart`, `Instances`, `Riding`, `Hitch`, `Unhitch`, `UnhitchRidden`, `MountHandle`, `InstanceTuning`, and the events `InstanceSpawned`, `InstanceRemoved`, `Hitched`, `Unhitched`); `SwitchStyle` on a ridden instance unhitches or refuses.
- 0.7.0: the idle shuffle (`Mounts.Behaviour` key `IdleShuffle`, `Mounts.Shuffle`, `MountHandle.IsStepping`).
- 0.8.0: `Mounts.Spawn(ownerId, MountSpawn)`, `Mounts.SpawnFoal`, `MountSpawn`, `MountHandle.Scale`, `IsRideable`,
  `Parent`, `Children`; the `Mounts.Shuffle` keys `ShuffleFollowSpeed`, `ShuffleFollowMaxDistance`, `ShuffleFollowGap` (how close to the parent it settles, 0.9), `ShuffleFollowAlways` (bool: follow however far, stopping only when blocked), `ShuffleFollowRoutes` (bool, on: a way round obstacles), `ShuffleRouteRange` (8).
- 0.9.0: kept with the save: `MountSpawn.Persist`, `Key`, `SceneId`; `MountHandle.Persist`, `Key`, `RemoveWithParent`; the event `Mounts.InstancesRestored`.
- 0.22.0: zombies on the cart ([zombies-on-cart.md](zombies-on-cart.md)): the `Mounts.Behaviour` key `CargoZombies` (on); two zombies at once, drawn as the game's lying zombie with their own heads and colours, a layout class of their own; their organs and gear stay with them through every move (the zombie is relinked to the copy the cart stores). `CargoLoaded` / `CargoUnloaded` pass a `CargoItem` (id, GUID, zombie name) and `MountHandle.Cargo` replaces `CargoIds`. Fix: riding past a working zombie no longer offers "[E] Take" while the cart can't take it (taking it pulled it off its job). Fix: the hint over what the keeper faces is redrawn when the cart's load changes.
- 0.23.0: a chest on the cart ([portable-chest.md](portable-chest.md)): the `Mounts.Behaviour` key `CargoChest` (on). While driving the cart, the action key on a plain storage chest (`chest_rough`, `chest`, `chest_good`) lifts it onto the cart, contents and all; it rides alone (the cart takes no other cargo meanwhile), drawn on the bed by a stand-in placed like a lone cargo item (its own `[item]` layout entry); the interact key with nothing in front sets it down where cargo would go, when the spot is in reach and inside a building area. On foot at the standing cart the stand-in offers the game's "[E] Open" (the real chest's inventory) and "[F] Set down". A save sets it down at the cart and takes it back right after; the side file names it (`chest <guid>`) and the restore after a load lifts it again. A door to another scene is refused while it rides ("Set the chest down before going through."); a dismount of the summon, a removal or a scene change set it down at the cart. The chest is the same world object throughout: it is never copied or removed from the scene.
- 0.26.0: corpses at the crossroads ([corpse-delivery.md](corpse-delivery.md)): `Mounts.Corpses` (the `[Corpses]` keys, `CorpsesAtCrossroads` off), `Mounts.CorpsesActive`, `Mounts.CorpseLeft` with a `CorpseInfo`, `Mounts.StartCorpseDelivery()`. With the switch on, the game's own delivery donkey walks out beside the nun, leaves its bodies there for the keeper to haul and walks back; its four patches exist only while the switch is on.
- 0.27.0: a corpse into the morgue ([above](#a-corpse-into-the-morgue-0270)): the `Mounts.Behaviour` keys `CorpseIntoMorgue` (on) and `CorpseIntoMorguePlaces`. The ride ends at the morgue door as always; the corpse comes off the parked cart into the keeper's hands, so he walks in carrying it. The game's doors are noted while riding (`PlayerController.Teleport` prefix, never refusing).
- 0.28.0: the cart's capacity ([cart-capacity.md](cart-capacity.md)): the `Mounts.Behaviour` key `CartSlots` (2; 1 to 6). Each cart's inventory is sized when its view is prepared, before carried-over items go back on (a door never sheds any), and again when the setting or an override changes; the game's `npc_donkey.inventorySize` is untouched. The packer lays three and more items out in layers of up to two; one and two items are placed exactly as before.
- 0.29.0: fights from the saddle ([mounted-combat.md](mounted-combat.md)): the armour view and the attack states no longer end the ride (`MountRules`: attacks are not work), the keeper fights in the Ride style with the sword and the bow, aims up and down, strafes with the donkey walking backwards; the cart is refused while he wears armour (`Mounts.TryMount(Cart)`, `Hitch`, `MountCompanion.Use(Cart)` and `BringCart` all say why) and a hitched cart is left standing when the armour goes on. New `[Ride]`/`[Cart]` tuning keys `FocusSpeed` (0.5: the speed while aiming, times the riding speed) and `BackSeatExtraPx` (0 / 5: how much higher he sits when seen from behind). No change to the public surface.
- 0.25.0: `Mounts.Unhitch` works on a standing donkey without a view (another scene, asleep); `MountCompanion.Use(Ride)` leaves a parked cart where it is and brings the donkey alone (`MountCompanion.CartStaysParked`, on; off is the old way: both come and the cart is left by the keeper).
- 0.24.0: the chest in the API: `Mounts.ChestLoaded` / `ChestUnloaded` with a `ChestInfo` (the cart, id, GUID, item count), and `MountHandle.Chest`.
- 0.21.0: rider motion while walking: the tuning keys `BobScale`, `BobLagFrames`, `HandsSteady`, `HandsSwayPx`, `HeadLagFrames`, `HeadSettle` ([rider-motion.md](rider-motion.md)). With the defaults the rider bobs a little more and a step late, his hands stay steadier and follow the strides, and his head settles after his body; 1 / 0 / 0 / 0 / 0 gives the old rigid rider. Fix: foals cross bridges. Where the navmesh lies under a deck, a step check compared it with the deck beside it as a 0.49 wall; big steps are now judged with both points from one source ([foals.md](foals.md), "The bridge by the house"). Fix: the ridden donkey (Ride style) stands on the walkable surface under its body instead of the keeper's height plus a ground difference, which carried his body's hover over a slope ([slopes.md](slopes.md)).
- 0.20.0: lively foals: the `Mounts.Shuffle` keys `ShuffleFoalLively` (on) and `ShuffleFoal*` (area, steps, speeds, pauses, chained steps, quick trots) for donkeys with a parent ([foals.md](foals.md), "Lively foals"). Fix: foals placed together (at a door, a catch-up) each take a free slot; they stood inside each other. Fix: the still head over the walk (a view switched on with the prefab's head on its head layer). Removed: the navmesh-hole code (never found a hole). Fix: the game walking the keeper (the walk to a fishing spot, a script's walk) ends the ride where he stands, with the reason `the game walks the keeper`; a dismount leaves a state the game set on the keeper alone; a donkey is never parked off the ground, and the keeper is never put onto a seat over no ground: the donkey comes to him instead, and hitching such a cart while riding is refused ([safe-ground.md](safe-ground.md)). Fix: getting on (or hitching) while walking the other way no longer flips the rider and donkey every frame (the facing is read from the keeper's animator, not his stored direction, which the game leaves stale).
- 0.19.0: `MountCompanion.Wheels` / `Hubs` (`WheelColor`: Game, DarkWood, Red, Green, Blue, Black, Custom; `HubColor`: Game, Black, Brass, Silver, Custom) with `CustomWheels` / `CustomHubs`; `Paint`'s Dark wood no longer darkens the wheels. Fix: donkey views are reset to the prefab and checked when they go back (docs/pooled-parts.md).
- 0.18.0: foals indoors: the `Mounts.Shuffle` key `ShuffleFollowKeeperIndoors`, `MountHandle.IsWithKeeper`, `MountCompanion.FoalsComeIndoors`. Fix: a view's colliders are muted the moment it comes alive (a parked donkey's capsule shut the keeper in a doorway). (The navmesh-hole removal built for the same bug found nothing and was removed in 0.20.0.)
- 0.17.0 (breaking, pre-release): `MountCompanion.RideSpeed` and `CartSpeed` replace `Speed`; `FoalCount` (0 to `MaxFoals`) and `Foals` replace `HasFoal` and `Foal`; `SpeedLabels`; `BellOnSetOff` starts off.
- 0.16.0: `MountSettingsPage.Key(..., pressed)` (the API polls the keys), `Status(section)`, `Mounts.StatusText`, `MountCompanion.RingBell`; refusals of `Use` logged; an unloaded mod's tuning cleared; plain Backspace means no key, with a note after the last key.
- 0.15.0: `Preview(section, companion, label, cart)` rows; the picture above the Mods window or in the rows.
- 0.14.0: `MountCompanion` (`Mounts.Companion`), the look and speed presets (`MountSpeed`, `CoatColor`,
  `HarnessColor`, `CartPaint`, `LanternColor`), `MountSettingsPage` (`Mounts.SettingsPage`: the Mods menu or the
  game's Settings window), `MountHandle.DisplayName` / `SetDisplayName`, and `HitchKey` in `Mounts.Behaviour`.
- 0.13.0 (breaking, pre-release): the rename. GK2 Mounts is now GK2 Donkey API: `GK2DonkeyAPI.dll`, GUID
  `dreas.gk2.donkeyapi`, namespace `GK2DonkeyAPI.Api` (was `GK2Mounts.Api`). Type and member names are unchanged.
  The showcase is now GK2 Saddle Up! (`dreas.gk2.saddleup`). A save's old side file and old owner ids are read and
  mapped (`docs/persistence.md`).
- 0.12.0: `MountHandle.LoadFromHead` and `TakeToHead`, loading and unloading a standing cart on foot (docs/load-on-foot.md).
- 0.11.0 (breaking, pre-release): `ComeToKeeper` gains `forRide` and `showMessage`, and refuses to bring a donkey for a ride that would be refused (found in play: Saddle Up's H indoors said "Not indoors." and the donkey came in anyway).
- 0.10.0 (breaking, pre-release): per-owner ids (`MountHandle.Id` is the owner's own number, `FullId`, `MountSpawn.Id`); `Mounts.Find(owner, id)`, `Find(owner, key)`, `InstancesOf(owner)`; `Mounts.GameReady`, `IsGameReady` (spawning before it is refused); `MountHandle.SetPersist(ownerId, keep)` and `SetKey(ownerId, key)` replace the setters (owner only); `GetData`, `SetData`, `DataKeys`; `SpawnFoal` gains `id`, `key`, `persist`; dormant records for owners not installed; side file format 2.
