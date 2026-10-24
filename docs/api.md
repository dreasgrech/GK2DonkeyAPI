# GK2 Donkey API: guide for mod authors

GK2 Donkey API (`GK2DonkeyAPI.dll`, GUID `dreas.gk2.donkeyapi`) is a library. It gives Graveyard Keeper 2 a rideable
donkey: ridden on its back (**Ride**), or pulling its cart (**Cart**), with cargo, a lantern and sounds. On its own it
does nothing: **your mod decides when the keeper may mount, what the ride can do and how it looks.** Saddle Up
(`src/SaddleUp`) is the playable version for players and uses nothing but this API.

API version **0.36.0** (`Mounts.ApiVersion`, the same number as the mod's own; `Plugin.Version` is the one place). Everything public is in the namespace `GK2DonkeyAPI.Api`. The exact
surface is `tests/ApiSurface/GK2DonkeyAPI.Api.txt`.

## Setup

1. Reference `GK2DonkeyAPI.dll` (from the release zip, or the installed `BepInEx/plugins/GK2DonkeyAPI/GK2DonkeyAPI.dll`) with
   `Private=false`, so you never ship a copy of it.
2. Declare the dependency, then check the API version at startup:

```csharp
[BepInPlugin("you.gk2.mymod", "My Mod", "1.0.0")]
[BepInDependency("dreas.gk2.donkeyapi", "0.35.0")]          // hard: your mod needs the core, at least this version
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
- **You can add restrictions but never remove the core's.** Co-op, "busy" (work, fishing, cutscenes;
  not the game's attack states: the keeper fights from the saddle, [mounted-combat.md](mounted-combat.md)), the cart
  while the keeper wears armour (fights are for the Ride style), build mode, the save hand-back and the mount-object
  check always apply. `TryMount` returns their reason like any other. Indoors is not one of them: it is a Behaviour
  switch (`DismountIndoors`, `CartIndoors`), on in the core.
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
| `MountResult Jump()`, `TakeOff()`, `Land()` (0.36.0) | The ridden mount off the ground, as the jump and fly keys do it (for a mod's own button or a script). `Jump`: a full jump; refused in the air, with the cart, on a mount whose `Jumps` is false. `TakeOff`: from the ground, or from a jump in the air (the jump becomes the take-off); refused on a mount whose `Flies` is false, indoors (`FlyIndoors`), with the cart, already flying. `Land`: while taking off or flying; it comes down where the keeper can stand, else hovers. The reason is in the result; the bubble says nothing. All refused while `FlyingEnabled` is false. |
| `MountResult ReturnToSafeSpot(reason = null)`, `LastSafeSpot`, event `ReturnedToSafeSpot` (0.36.0) | Never lost ([never-lost.md](never-lost.md)): the API keeps the spots the keeper stood on (the walkable ground under his feet, nothing over him, his body fitting; eight per scene, plus where the game put him at a load and after each door) and, with the Behaviour key `FallGuard` on, puts him back on the newest one that still holds when he has fallen out of the world (under the ground, into nothing, far below it). `ReturnToSafeSpot` does the same by hand (a mod's button or key; Saddle Up's "Back to the last safe spot"): refused with the reason while not in a game, while a door, a cutscene or a dialogue has the keeper, and in a scene with no spot yet; the bubble says the result. A flight or a jump is ended first, a ride goes on. `LastSafeSpot`: this scene's newest, or null. The event: where he was put, by whoever asked. Every return is a warning in the log with its facts. |
| `FlightState`, `FlyingEnabled` (0.36.0) | `MountFlight`: `Ground`, `Jumping`, `TakingOff`, `Flying`, `Landing` (Ground while not riding). `FlyingEnabled`: jumping and flying are switched on (a dev-only experiment for now): show your own jump or fly rows only then. |

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
- `FlightChanged(MountFlight now)` (0.36.0): the ridden mount's `FlightState` changed. A jump: `Jumping`, then
  `Ground`; a take-off: `TakingOff`, `Flying`; a landing: `Landing`, `Ground`; a jump turned flight: `Jumping`,
  `TakingOff`. Raised whoever started it: the keys, `Jump` / `TakeOff` / `Land`, or the core (a door, a dismount).
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
cart.Set("you.gk2.mymod", "SpeedMultiplier", 2.5f);   // returns the value in effect: yours, clamped to the allowed range, or the holder's when another owner has the key (TrySet says which)
cart.Set("you.gk2.mymod", "Pose", RiderPose.Walk);
float now = cart.Get<float>("SpeedMultiplier");       // the value in effect
Mounts.Behaviour.Set("you.gk2.mymod", "DismountIndoors", false);
Mounts.ClearTuning("you.gk2.mymod");                  // all your overrides, every section
```

- **Session only:** a `Set` overrides the player's value until `Clear(ownerId, key)`, `Reset()`, `ClearTuning(owner)` or the
  end of the session. It's never written to the player's cfg.
- **Type-checked:** a wrong type throws `ArgumentException`, naming the expected type.
- **Visible in the dev panel:** an overridden row is cyan and locked, and shows the value in effect.
- **When it applies:** while mounted in that style, most values apply at once. Values the donkey is built with (the
  cart and lantern geometry measured at summon) apply from the next mount or style switch.
- **Discovering keys:** `Keys`, `Has(key)`, `TypeOf(key)`, `IsOverridden(key)` and `OverriddenBy(key)`.
- **A value belongs to the owner that set it** (0.35.0): `Set` by another owner is refused (the value in effect is
  returned; `TrySet(owner, key, value, out applied)` says whether it was set; one Info line), `Clear(owner, key)`
  clears only your own. The one value anyone may replace is the mount's own (set by the mount's merge under
  `dreas.gk2.donkeyapi.skin`): a mod's value wins over the creature's and the creature's returns when cleared.

**Behaviour** (`Mounts.Behaviour`, section "General"):
- `HitchKey` (0.14.0, a `KeyCode`; `None` switches the key off)
- `StayMountedThroughDoors`
- `DismountIndoors` (on): dismount at the door into a building, the church or a dungeon (the game's indoor light
  presets) and refuse to mount inside ("Not indoors."). Off: riding goes on through a same-scene door and the
  keeper can get on inside; a door with a loading screen still follows `StayMountedThroughDoors`.
- `FlyIndoors` (0.36.0, off): on, a flying mount may take off and fly indoors, as a lift only (never through walls).
  Off: no take-off indoors ("indoors (flying indoors is off)"), and a flight that goes through a building's door
  lands there. Saddle Up keeps it off and offers no row for it.
- `FallGuard` (0.36.0, on): the fall watchdog ([never-lost.md](never-lost.md)); off, nothing watches (the record and
  `ReturnToSafeSpot` still work). `ReturnKey` (0.36.0, `KeyboardShortcut`, None): a key for `ReturnToSafeSpot`, read by
  the API with the flight keys' gate.
- `StandingSounds` (0.36.0, on): standing mounts and babies near the keeper make their sounds (steps, grazing, the
  lip smack; a baby higher and softer). Off: only the ridden mount, and a standing one's lip smack is the game's own
  ([mount-sounds-design.md](mount-sounds-design.md)).
- `JumpKey`, `FlyKey` (0.36.0, `KeyboardShortcut`, Space both: a key with its modifiers, Ctrl+Space is Ctrl+Space;
  more keys held, such as the climb's Left Shift, don't stop a press): riding plain, the mount jumps; a mount that flies takes off
  and lands. One key for both: a press jumps, a second press in the air during the jump takes off a mount that
  flies, a press while flying lands. Two keys: each its own. `None`: off. Read only while `Mounts.FlyingEnabled`
  (the dev-only experiment), never during a dialogue, a menu, a pause or while a key is being set in the Settings
  window, and Space not in a fight in armour (the game's attack); a key just changed counts once it has been let go.
  Saddle Up sets both from its Keys rows.
- `CartIndoors` (0.36.0, off; counts only with `DismountIndoors` off): the cart comes indoors too, its load on it.
  Off, a cart ride through the door leaves the cart standing at the door with its load (an unhitch: `Unhitched`)
  and goes on inside as a ride; at the morgue with a corpse aboard the keeper gets off inside (`Dismounted`,
  "morgue") and takes the corpse into his hands, the mount there with him. A summoned mount (`TryMount`, its cart
  goes with the ride) or a cart that can't be left there gets off at the door as before (`"indoors"`). The
  cart is refused inside wherever it would come from (getting on with it, `SwitchStyle`, a hitch from the saddle
  or on foot, the companion's cart key) with "Not the cart indoors." Switching either off while inside ends
  the ride at once. See [indoors.md](indoors.md).
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
| Stride | `WalkCycleSpeed` (float, 1.5: the walk clip's rate per unit of walking speed; a mount key) |
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
donkey.Tuning.Set("you.gk2.mymod", "SpeedMultiplier", 2.5f);     // this donkey only (refused if another mod holds it)
donkey.Tuning.Set("you.gk2.mymod", "CoatColorize", "C040FF");    // any key of Tuning, Behaviour or Colors
donkey.Tuning.Clear("you.gk2.mymod", "SpeedMultiplier");         // your own value off again
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
| `Tuning` | Its own settings (`InstanceTuning`: `Get`, `Set`, `TrySet`, `Clear(owner, key)`, `Reset`, `IsSet`, `SetBy`, `Keys`; a value belongs to the owner that set it). Keys are those of `Mounts.Tuning(style)`, `Mounts.Behaviour`, `Mounts.Shuffle` and `Mounts.Colors`; a style key applies in whichever style it is in. This donkey's value wins over everyone's (cfg and session overrides). |
| `Ride(showMessage)` | The keeper gets on and is put on the seat. The same checks and rules as `TryMount`. |
| `MoveTo(position, facingRight)` | Moves a standing one. |
| `IsReady` | Its view is loaded in the keeper's scene: `Ride` works. It takes a few frames after a spawn, `ComeToKeeper`, a hitch or an unhitch. |
| `ComeToKeeper(facingRight, forRide = true, showMessage = true)` | It comes to the keeper, into his scene, to where a ride would put it, so `Ride` right after moves no one. Saddle Up calls its donkey this way. A donkey called while the keeper is on foot is called for a ride: when a ride would be refused there (indoors, busy, building, co-op, a mod's rule, too small to ride), it stays where it is and the refusal is returned and shown like a ride's. `forRide: false` brings it anyway. A cart, or anything called while the keeper rides, always comes. |
| `LoadFromHead(showMessage = true)` | On foot at this standing cart: the topmost item the keeper holds that the cart takes goes onto it, one per call (with the perk for several held items, call again for the next). Refused while the keeper rides, for a ridden or cart-less one, in another scene, when the cart is full or takes none of what is held (zombies with `CargoZombies` off), or when a rule switched `Cargo` off. The keeper's controls call it ("[E] Load on cart"). |
| `TakeToHead(showMessage = true)` | On foot at this standing cart: its top item into the keeper's hands, with the game's own pickup. Refused as `LoadFromHead`, and when the hands are full or the cart is empty. The keeper's controls call it ("[E] Take from cart"). |
| `SetDownCargo()` | Everything on its cart is set down on the ground at the cart. Call it before `Remove` to keep the cargo (zombies are always set down by `Remove`: a zombie is never thrown away). |
| `Remove()` | Gone, with anything on its cart; a ridden one is dismounted first. |

**Events:** `InstanceSpawned`, `InstanceRemoved`, `Hitched(donkey, oldCart)`, `Unhitched(donkey, newCart)`. The
ride's own `Mounted` and `Dismounted` come for instances too. At a hitch, `Hitched` comes first and the old cart's
`InstanceRemoved` right after it, so a handler can still recognise `oldCart` as the cart it held.

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
MountHandle foal = Mounts.SpawnFoal("you.gk2.mymod", mother);        // the mother's mount's size (a donkey: 0.6), not rideable, shuffles and follows
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
| `FoalCount` | 0 to `MaxFoals` (5) foals (the mount's own size: a donkey's 0.6, not rideable) that stay with the donkey. |
| `FoalsComeIndoors` | The foals come through doors with the keeper, follow him inside and come back out with him (`ShuffleFollowKeeperIndoors` on each foal). Off. |
| `FoalsFollowOnFoot` | (0.36.0) The foals follow the keeper while he walks outdoors on foot, instead of staying by the mount (`ShuffleFollowKeeperOnFoot` on each of this companion's foals, never on another mod's); when he gets on a mount they go back to it, following it when it's theirs. Into buildings only with `FoalsComeIndoors`. Off. |
| `Name` | Shown in the prompt: "[E] Ride Bella" (`MountHandle.SetDisplayName`). |
| `Brays`, `BellOnSetOff` (off to start with), `SoundVolume` (0–1.5), `Lantern`, `LanternByDay` | The ride's sounds and lamp. |
| *(every setting)* | Starts at the default a mod's settings should use: pass the property as the row's default (`p.Toggle(..., me.BellOnSetOff, ...)`), so it's written once, in the API. |
| `Coat`, `Harness`, `Muzzle`, `Paint`, `Wheels`, `Hubs`, `LanternLight` | Look presets (`CoatColor`, `HarnessColor`, `MuzzleColor`: the second body tone, the donkey's muzzle, the rat's pink parts, the snail's shell, ignored by a mount whose look has no Muzzle region; `CartPaint`, `WheelColor`, `HubColor`, `LanternColor`); `Game` is the game's art. The paint is the cart body and shafts; the wheels' wood and their hubs have their own (0.19.0). |
| `CustomCoat`, `CustomHarness`, `CustomMuzzle`, `CustomPaint`, `CustomWheels`, `CustomHubs`, `CustomLantern` | Hex `RRGGBB`, used with `Custom`. Empty or invalid means the game's look; an invalid one is logged once. |

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
| `Color` | as above | a look row: in the Mounts panel, not in the menus (0.35.0); its lightness row goes with it |
| `Choice` / `Steps` with `look: true` | as above | in the Mounts panel, not in the menus (0.35.0) |
| `Preview(section, companion, label = "Your mount", cart = true)` | a row with the picture; call once per section that should show it. `cart: false`: that row shows the donkey alone. The picture also goes above the Mods window when there's room | a picture of the companion's donkey and cart in its current colours next to the settings (the Mounts panel draws its own). No donkey needed: the art comes from the game's files, the colours from the companion |
| `PanelButton(section, label, description)` (`LookButton` is the same) | nothing | a button: the menu closes and the Mounts panel opens over the game (every mount listed, the chosen one drawn with its words, its look rows under it). `OpenPanel()` opens it from a key, and closes it when the key comes again while it is open; the button only brings it forward |
| `Button(section, label, description, onClick, buttonText = "Go", enabled = null)` (0.36.0) | nothing | a button of the mod's own: `onClick` runs when it is pressed (the game's Settings window; the Mods menu in a game), `buttonText` is the word on it, `enabled` greys it in the Settings window and leaves it out of the Mods menu while false. A handler that throws is logged. Saddle Up's "Back to the last safe spot" ([never-lost.md](never-lost.md)) |
| `TuningKey(section, key, tuningKey, companion)` | nothing | says which tuning key the row sets: the row is left out of the Mods menu and the game's Settings window while the companion's chosen mount pins that key (`PinnedBy(companion, key)` names the mount; only for a key a mount may pin; a set-aside skin pins nothing) |
| `Heading(section, label, description = "")` | nothing | a heading inside a section: a line with the label and no value, before the rows it names (Saddle Up: "Mount colours" and "Cart colours" in Colours). In the Mounts panel the look rows after it, in its section, stand under this heading in a group of their own instead of under the section's name. Not a picture row's label: a `Preview` row is found on screen by its label, so no other row may share it (the page throws) |

- **In the Mods menu:** your mod lists `dreas.gk2.donkeyapi` as its dependency, and GK2 Donkey API has its own entry
  in the list (no settings). While the API is off or unconfirmed for the game build, the menu marks your mod
  "Dependency unavailable".
- **Binding:** each row binds an entry in your cfg and runs `apply` now and on every change, from the Mods menu, the
  Settings window or the cfg.
- **Sections keep your order:** the cfg section is numbered in the order you first use them (`1. Keys`,
  `2. Behaviour`, ...), because the framework's Mods menu sorts sections by name and offers no other way to order
  them. The game's Settings window shows the plain names. Moving a section in a later version renumbers it, and the
  page carries a player's values over: a key the cfg lacks under its section takes the value the cfg has under the
  same name with another number (of several old copies, the fullest section wins, then the highest number), and the
  old copy is dropped from the file at the next save; the log says which section moved. A key you move to another
  section or name: `page.MovedKey("Mount", "Name", "Donkey")` before the row (old key name as a fourth argument when
  it changed too), and the first bind with nothing in the cfg takes the old value the same way. A key that moved
  more than once names each old place in a call of its own (0.36.0), e.g. Saddle Up's babies:
  `MovedKey("Babies", "Foals", "Mount")` and `MovedKey("Babies", "Foals", "Donkey")`.
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
    and the Mounts panel draws its own picture of the chosen mount;
  - **the Mounts panel:** the mounts listed, the chosen one drawn with its description, what it does and its
    abilities, and the page's look rows for it, with Done and Cancel. Cancel puts back what was set when it opened
    (every mount's). Only in a game. The look rows are not in the Mods menu or the Settings window at all.
- **The picture and the Mounts panel are extras.** They're found and built defensively. If something about them fails
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
- Saddle Up: the Cart toggle "Donkey leaves corpses at the crossroads" (off) sets `CorpsesAtCrossroads`; the
  finer keys are the API's cfg and the panel's "Corpses at the crossroads" rows.

## A corpse into the morgue (0.27.0)

The way back from the crossroads: a body carted to the morgue. Driving the cart through the morgue door, the ride ends
at the door as it always does (`DismountIndoors`, or `CartIndoors` off; a door that loads another scene ends it too)
and the cart is parked outside with its load. With `CorpseIntoMorgue` on (the core's default) the corpse comes off
that parked cart into the keeper's hands in the same breath, so he arrives inside carrying it. Code:
`src/GK2DonkeyAPI/MorgueHandover.cs`. With riding allowed indoors and the cart not (`DismountIndoors` off,
`CartIndoors` off) the cart is left at the door and the keeper rides in: once the door is known to be the morgue he
gets off inside ("morgue"; a ride needs empty hands) and the corpse comes off the cart at the door into his
hands, the mount inside with him. With the cart allowed indoors (both switches) the drive goes on into the morgue
and nothing is handed over: the corpse is on the cart inside, taken off it as from any cart.

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
- Saddle Up: the Cart toggle "Carry the corpse into the morgue" (on).
- Tested in game 2026-10-10: the door signal decided on the dismount frame, while the screen was still black; the
  morgue's inside is at about (-72.5, 0, -388.7) in `RuinedTemple`, lit with the `dungeons` preset.
- Every door while riding writes `Mount: a door while riding: to <point> in <scene> (preset <p>); N item(s) on the
  cart.`, and a door with a corpse aboard then `... goes into the keeper's hands if this is the morgue.` followed by
  either the hand-over line or `the corpse stays on ...: not the morgue (area <a>, door to <d>)`.

## Mounts (0.30.0)

The one-page guide with both shapes of a mod's own mount (a pack in a DLL, a mount in code with an ability):
[making-a-mount.md](making-a-mount.md).

The one-page guide with both shapes of a mod's own mount (a pack in a DLL, a mount in code with an ability):
[making-a-mount.md](making-a-mount.md).

> **Switched off in Saddle Up 0.10.0's download** (`Mounts.MountsEnabled` is false there; the next update switches it
> on): the lists hold the donkey alone or nothing, and a registration returns a handle that does nothing, with one log
> line per owner. Everything below describes the API as it works when switched on, and as it works now on a machine
> with the dev key `Debug.MountsPreview` on.

What the player rides as a creature type, against a skin, which is how it is drawn ([mounts-design.md](mounts-design.md)).
A mount (`Mounts.Catalog`, `MountDef`) owns the rider's seat for it (values for existing tuning keys: `Tuning` for both
styles, `TuningRide` / `TuningCart` for one), whether it pulls the cart (`Cart`) and has foals (`Foals`, their size
against the grown mount in `FoalScale`, 0.2 to 1, 0.6 by default; a running foal is remade when its mount's value
changes), whether it flies and jumps ridden plain (`Flies`, false by default, and `Jumps`, true; a pack's `flies = true`
and `jumps = false`; 0.36.0: flying asks the mount's flag, never its id), a default
effect (`Effect` / `EffectWhen`, shown when an instance's own `Effect` key is `MountEffects.FromMount`, "mount"), and its
**abilities**. A skin names its mount (`MountSkinDef.Mount`; a pack's skin is a look of the mount folder it sits in;
the donkey by default), so the red and
a hypothetical blue dragon would be two skins of the dragon mount, and Hellfire is a skin of the donkey mount.

**Ids.** A mount's id is `<mod>.<name>` (`saddleup.dragon`, `acme.dragon`): a code or pack mount without a dot in its
id is refused; only the game's `donkey` is plain. A skin is named within its mount: `MountSkinDef.Id` is its name
(`default`, `frost`) or its full name, `MountSkinDef.Mount` the mount's id, and the registered id is
`<Mount>/<Id>` (`saddleup.dragon/default`, `donkey/saddleup.hellfire`). Every mount but the donkey has one default look, the
skin named `default` (a pack's `skins\default\`): what a new instance wears, the Skin row's Default. A code mount
should register one; if it doesn't, its first skin by name is its default. The donkey's own look is the game's art;
its skins have names of their own. A skin added to a mount another mod owns is named `<mod>.<name>` (`Id =
"me.frost"`): every donkey skin and recipe, a code skin on a mount another owner registered, a pack skin in a folder
without `mount.cfg`; without the prefix `Register` / `RegisterRecipe` throw an `ArgumentException` (a pack's is left
out with a warning). On your own mount any name. Why: docs/mount-id-collisions.md.

**The same mount id from two mods** (0.36.0): `Mounts.Catalog.Register` no longer throws when another mod has the id.
One version rule decides, for code and packs alike: the higher `Version` wins; at a tie code or a DLL beats a loose
folder; otherwise the first stays. The one left out gets one warning in the log and a handle that does nothing (its
`Dispose` leaves the winner alone); a newer code mount replaces the registered one (its instances become the new one,
the old one's clips go with it). A code mount meets a pack's `mount.cfg` by the same rule: a newer `mount.cfg`
replaces it, an older one is refused with its skins. A code mount's and a code skin's clips (`MountSound.Own`) are
banked under their mod (`gk2.mount.<owner>/<mount id>.<key>`), so two mods' mounts of one id never share an entry.
A mistake in the call itself (no id, no prefix, the donkey's id) still throws.

An instance's mount is its
skin's (`MountHandle.Mount`): the player picks a skin, the mount follows, and nothing new is saved. The mount follows the
instance's `Skin` key whether or not the instance has a view (asleep far from the keeper, it is still that mount).

```csharp
foreach (MountInfo k in Mounts.Catalog.All) Log(k.Name + ": " + string.Join(", ", k.Skins));
IDisposable reg = Mounts.Catalog.Register(Guid, new MountDef
{
    Id = "example.checker-beast", Name = "Checker Beast", Cart = false,       // "<mod>.<name>": a dot is required
    TuningRide = new Dictionary<string, object> { ["SeatAdjustPx"] = 2f },   // the API takes a picture key on a mount; a pack puts it on the skin (the scan refuses it in a mount.cfg's [Tuning*])
    Abilities = { new GallopsWhenRidden(Guid) },
});
Mounts.Looks.Register(Guid, new MountSkinDef { Id = "default", Name = "Checker", Mount = "example.checker-beast", Source = src }); // example.checker-beast/default
```

Where a value comes from, per instance and key: an override another mod set on it; the skin's own `TuningRide` /
`TuningCart` for the style, then its `Tuning` (both styles); for a skin built on its mount's default (0.36.0, below:
"Built on the default"), the default's the same way; the mount's for the style, then for both; the player's cfg. A value of another numeric type than the key's is converted (`4f`
for the int `SeatAdjustPx`); `MountTuning.TypeOf(key)` says which. The merged set goes on the wearer as overrides owned by the skin
system and is re-applied only when the skin, the mount or the style changes.

A **pack declares a mount without code** as a mount folder `gk2mounts\<mount>\` with a `mount.cfg`: `[Mount]` (`id`,
`name`, `noun`, `description`, `portrait`, `author`, `version`, `cart`, `foals`, `flies`, `jumps`,
`foal_scale`, `speed`, `cargo`, `effect`, `effect_when`), `[Stats]`, `[Tuning]`, `[Tuning.Ride]`, `[Tuning.Cart]` for
its values (each key in one of the three) and `[Sounds*]`; its skins are the folders under its `skins\`, and
`skins\default\` is the one a new instance wears (the Skin row's Default); a `mount.cfg` without
`skins\default\skin.cfg` is refused with its skins (one warning), and a `default_skin` line is reported as ignored.
The folder's name is the mount's id (`gk2mounts\saddleup.dragon\`). A mount folder without `mount.cfg` adds its
skins to a mount registered elsewhere (`gk2mounts\donkey\skins\` for the donkey's) and may not bring a `default`
(left out, with a warning); its skins carry their author in the name, `<mod>.<name>`
(`gk2mounts\saddleup.dragon\skins\acme.frost\` is `saddleup.dragon/acme.frost`), and one without the prefix is left
out with a warning. The same mount from two packs:
the higher `[Mount] version` wins and replaces the facts in place (the other's skins keep the mount), a tie goes to
the DLL over a loose folder, otherwise the first stays and the later one is refused with a warning; a mount
registered in code meets the same rule (0.36.0: a newer `mount.cfg` replaces it, an older one is refused with its
skins; before, a pack never replaced a code mount). A pack's mount is withdrawn with the last of its skins from the same
owner, and a mount pack whose `default` doesn't register is withdrawn at once with its skins. Saddle Up's five mounts
(`saddleup.rat`, `saddleup.snail`, `saddleup.worm`, `saddleup.dragon`, `saddleup.gerry`) are packs of this kind, all in one layout (docs/skins.md "Worked examples"); they are Saddle Up's
packs, so `MountInfo.IsBuiltIn` is false for them (true only for the API's own). (Before the mount folders,
2026-10-24, a skin's `[Mount]` block declared the mount and the skin with the mount's id owned the declaration.)

An **ability** (`MountAbility`, abstract: override what you need) is code the API calls for every instance of the
mount that has a handle: `Attached` / `Detached` when the instance becomes / stops being the mount, `Mounted(style)` /
`Dismounted` on the keeper getting on and off (a style switch is a Dismounted then a Mounted), `Tick(dt)` every
frame while ridden (and while standing when `TicksWhileStanding`). One object per mount, shared by its instances; an
ability that throws is switched off on that instance with one warning. Keys, settings rows, sounds and effects are
your own calls from the hooks; `examples/GK2DonkeySkins` has one (the checker beast gallops while ridden: its
`SpeedMultiplier` under the ability's own owner). An ability never sets a look key (the skin, the effect, the
colours are the player's): such a Set is refused, held or free (0.35.0).

A mod's **look rows are kept per mount** with `page.PerMount((mountId, suffix, shown) => { ... })`: the body runs
once per registered mount and builds the rows with the suffix on their cfg keys (`""` for the donkey,
`"_saddleup.dragon"` for the dragon) against `companion.LookOf(mountId)` (a `MountLook`: `Coat`, `CustomCoat`, `Harness`, `Paint`,
`Wheels`, `Hubs`, `LanternLight` and their custom colours); the chosen mount's rows are the ones shown. The
companion's own `Coat`, `Harness`... read and write the current mount's look (`MountId`).

A settings page gets the rows in three calls: `page.MountRows("Mount", companion)` adds a **Mount** dropdown (every
mount) and a **Skin** dropdown with the chosen mount's skins (one row per mount, shown for the chosen one only: a
dropdown's choices are fixed once built), on the cfg keys `Mount`, `Skin_<mount id>` (`Mount = saddleup.dragon`,
`Skin_saddleup.dragon = saddleup.dragon/default`: full names); `page.FoalRows("Babies",
companion)` adds the babies rows (how many, follow you on foot, come indoors, the same mount and skin) on `Foals`,
`FoalsFollow`, `FoalsIndoors`, `FoalsWearSkin`, for a section of their own (Saddle Up's since 0.12; the count,
`FoalsFollow` and `FoalsWearSkin` default to the companion's values when it is called: Saddle Up sets
`FoalsFollowOnFoot = true` first); `page.EffectRows("Effect", companion)` adds the effect list ("As the mount or skin", none, every effect),
when it shows and how, on `Effect`, `EffectWhen`, `EffectTrail`, `EffectWithCart`, `EffectInFights`, `EffectOnFoals`.
The labels say "mount" and "babies", never "donkey": the mount may be a dragon. Mounts, skins and
effects registered before the page is built appear with no change to yours; later ones at the catch-up (0.34.0: the page is shown a frame after Publish and adds a late mount's rows then). `MountsChanged` fires on a
registration, a pack's declaration or a withdrawal.

## Sounds (0.36.0)

Every mount instance plays the events of one catalogue through the game's own player ([mount-sounds-design.md](mount-sounds-design.md)):
`step` (with the surface variants `grass`, `dirt`, `wood`, `stone`), `wheel`, `setoff`, `stop`, `graze`, `lips`,
`ear`, `turn`, `voice`, `answer` (a baby's reply to its parent's call), `refused`, `mount`, `dismount`, `hitch`,
`unhitch`, `cargo`, `bell`, `sent`, `called`, and
the flying and jumping ones `takeoff`, `flap`, `glide`, `fly`, `land`, `touchdown`, `landed`, `refused_takeoff`,
`jump`, `jumpland`, `perch`. What
plays for an event on an instance comes from layers, the first that says a source wins: a mod's `Set` here, the
skin's own (`MountSkinDef.Sounds`, a pack's `[Sounds]`; `SoundsRide` / `SoundsCart` for one style), the mount's
(`MountDef.Sounds`, a pack's `mount.cfg` `[Sounds]`), the player's keys (`Mounts.Tuning`: the donkey's sounds as shipped
and as tuned; the donkey's own sounds among them, its voice, lip smack, chewing and ride-style snorts, never reach
another mount: silent there until a layer gives one). The player's switches and volumes scale whatever won.

`Mounts.Sounds`:

| Member | What |
|---|---|
| `Catalogue`, `Events` | The events (`MountSoundEvent`: id, description, stoppable, variants, its own numbers), and their ids. |
| `MountSound Resolved(handle, eventId, out layer, variant = null)` | What plays now and from which layer (`mod`, `skin`, `mount`, `base`); null and `none` when no layer gives the event a sound. For a layer's files or clips `GameId` holds the bank's id (a game sound id of the API's own). |
| `IDisposable Set(ownerId, handle, eventId, MountSound)` | Your sound for an event (or `event.variant`) on this instance, over every other layer, for the session; Dispose takes it back. `MountSound.Game("id")`, `MountSound.Own(clips)` (your clips, registered as a game sound under your owner id and left to you), `MountSound.None`, `MountSound.Ground`, with optional `Volume`, `Pitch` (x the player's), `PitchVariation`, `Start`, `Loop`, `Speech`, `Numbers`. |
| `MountResult Play(handle, eventId, variant = null)`, `Stop(handle, eventId)` | Plays an event now as the game would (the layers, the player's volumes, the ground under it for a step); refused with the reason when nothing played. Stop ends a stoppable event's sound (grazing, a loop). |
| event `Played(handle, eventId)` | After an event played on an instance, by the ride, a standing one or a mod. |

A code skin or mount gives its sounds in its def (`Sounds`, `SoundsRide`, `SoundsCart`, `SoundsBaby` for the babies:
`Dictionary<string, MountSound>` by event key); `MountInfo.Sounds` / `SoundsRide` / `SoundsCart` / `SoundsBaby` show a mount's (a pack's lines as text, a code mount's
`MountSound`). The Behaviour key `StandingSounds` (on) lets standing mounts and babies sound near the keeper.

## Skins (0.30.0)

> **Switched off in Saddle Up 0.10.0's download** (`Mounts.MountsEnabled` is false there; the next update switches it
> on): the lists hold the donkey alone or nothing, and a registration returns a handle that does nothing, with one log
> line per owner. Everything below describes the API as it works when switched on, and as it works now on a machine
> with the dev key `Debug.MountsPreview` on.

A skin draws the donkey as something else. `Mounts.Looks` lists them and takes new ones; a donkey wears one through its
tuning key `Skin` (also `MountCompanion.Skin` for a player mod's donkey, with `FoalsWearSkin`). The pictures are a PNG
per donkey frame, 96x96 facing left (`Mounts.Looks.FrameKeys` names the 44 frames; [skins.md](skins.md) says how to
draw them); the API draws them over the donkey's own sprites and keeps those from rendering, lit like the game and
recoloured by the colour settings when drawn in the donkey's palette colours.

```csharp
foreach (MountSkinInfo s in Mounts.Looks.Skins) Log(s.Id + ": " + s.Name);   // "donkey" first, then the rest
handle.Tuning.Set(Guid, "Skin", "saddleup.rat/default");                       // this donkey wears the giant rat's look
handle.Tuning.Set(Guid, "Skin", MountLooks.DonkeyId);                          // the game's own look ("" does the same)
companion.Skin = "donkey/saddleup.hellfire"; companion.FoalsWearSkin = false;     // the player's donkey, foals left plain
```

Where skins come from, in order of how little they need:
- **Skin packs** (no code), sorted by mount: `gk2mounts/<mount>/skins/<skin>/` with `skin.cfg` and the PNGs, the
  mount's `gk2mounts/<mount>/mount.cfg` and `sounds/` beside them (no `mount.cfg` for a skin of a mount registered
  elsewhere, its skins named `<mod>.<name>`: `gk2mounts/donkey/skins/<mod>.<skin>/` for the donkey), the `gk2mounts` folder in `BepInEx/plugins` or in any
  folder there (`BepInEx/plugins/<AnyFolder>/gk2mounts/`). The API finds them at start.
  `Mounts.Looks.RegisterFolder(owner, folder)` reads `<folder>/gk2mounts/<mount>/` and
  `<folder>/*/gk2mounts/<mount>/` under a folder of your own and returns the number of skins registered.
- **Embedded in your DLL:** the same tree as `EmbeddedResource` with logical names `<prefix><mount>\mount.cfg`,
  `<prefix><mount>\sounds\<file>`, `<prefix><mount>\skins\<skin>\<file>` (e.g.
  `GK2SaddleUp.mounts.saddleup.dragon\skins\default\bdy_walk_left_03.png`; the csproj line `<EmbeddedResource
  Include="assets\mounts\**\*" LogicalName="<Prefix>.mounts.%(RecursiveDir)%(Filename)%(Extension)" />`), then
  `Mounts.Looks.RegisterEmbedded(owner, assembly, "<Prefix>.mounts.")`. The worked example is Saddle Up itself
  (prefix `GK2SaddleUp.mounts.`; its five mounts and the donkey's two skins: the csproj's embed line, one call in its
  Awake, nothing else; docs/mounts-as-mods.md). A dev build may record the packs' source folder as the assembly
  metadata `GK2MountsSourceDir` (`$(MSBuildProjectDirectory)\assets\mounts`); the F11 Mounts tab then writes a
  skin's tuned values back into its `skin.cfg` and the mount's into its `mount.cfg`.
- **A sheet instead of 44 files:** the same pack with one PNG (`sheet = sheet.png` in `skin.cfg`, cells of the
  canvas size in reading order, the frames in `Mounts.Looks.FrameKeys` order or as `frames = ...` lists them).
- **From code:** `Mounts.Looks.Register(owner, new MountSkinDef { Id, Name, Mount, Source = yourSource })` with an
  `IMountSkinSource` giving a `Sprite` per frame key, from any texture of yours (your own sheet, say: 50 pixels per
  unit, the pivot where the donkey's anchor is). The sprites stay yours; the API only points its overlays at them.
  On a mount another owner registered (the donkey's `Mount` too, the default) the `Id` is `<mod>.<name>` (`me.frost`)
  or `Register` throws an `ArgumentException`; on a mount your own GUID registered, any name (`default`, `fire`).
  A code skin says what a pack says (0.33.0): `Over` with `Erase` (the colour that clears the donkey, `null` = none),
  `BaseRegions` (the `[Base.<Region>]` recolour of the donkey under it), `Regions` (which of its colours are Dark,
  Muzzle, Eye or Harness; the rest Coat), `SeatRow` / `SeatFromPicture`, `Tuning` / `TuningRide` / `TuningCart`,
  `Effect` / `EffectWhen`, `FrontSource`; a readable sprite's colours join the look (an unreadable texture is said
  once in the log, and the player's colours leave it alone).
- **A recipe:** `Mounts.Looks.RegisterRecipe(owner, id, name, frame => pixels)`: a skin of the donkey, so its id is
  `<mod>.<name>` (`example.sepia` is `donkey/example.sepia`; a plain `sepia` throws), a rule over the donkey's own frames
  (`MountSkinFrame`: the pixels on the canvas, the outline and bob helpers), built when first worn, a few frames a
  tick, and worn once complete. Nothing of the game's art is shipped.

Whatever the source, the API decodes a skin's pictures into **one texture per skin** (a sheet with a cell per frame)
a few frames a tick, and the skin is worn once the sheet is whole; every donkey wearing it draws from that one
texture, and nothing is allocated per frame while it is worn. A skin names its **base** (`base = donkey`, the only
one today): the frame keys, canvas, pivot and pixels per unit are the base's, so a second creature with its own frames
is a second base, not a change to the format.

A skin may have a **front layer**: pictures drawn in front of the rider (the near wing, a saddle's near side), the
same frame keys, as a pack skin's `front/` subfolder, a sheet pack's `sheet_front`, or `MountSkinDef.FrontSource`; the API
sorts them on the keeper's own layer above his body while he rides (as the leg overlay), just above the creature
otherwise.

**Built on the default** (0.36.0; docs/skins.md "Variants: built on the default"). Every skin of a mount but the
donkey that isn't that mount's `default` is built on it: the pictures it has none of, its `[Skin]` settings it
doesn't give (canvas and pivot, colours, seat, offsets, effect, description, portrait), the default's `[Colors]` under
its own, the default's tuning and sounds under its own, all come from `<mount>/default`, and follow it when it is
read again or replaced (it stands alone while there is none). `layer = over` on such a skin draws it over the default
(the default's picture, holes where its erase colour is, its pixels on top), not over the game's donkey.
`MountSkinInfo.BuiltOn` is the default's id (`null` for a default, a donkey skin, or one standing alone) and
`MountSkinInfo.OverDefault` says it is drawn over it (`Over` stays "over the game's donkey"). A code skin says what it
gives by leaving the rest at its "not said" value, the sentinels: `SeatRow` -1 with `SeatFromPicture` false, `Offset`
zero and `CartOffset` null, `Effect`, `Description`, `Portrait` and `Regions` null, `CanvasWidth` / `CanvasHeight` 96
and `PivotX` / `PivotY` 48 mean "the default's"; `Effect = "none"` means none (not the default's). Its sprites are its
own (pictures of or into code sprites are not layered yet), and `Over` on it is refused (drawn in the default's
place, said in its report). The donkey's skins have no default: they are as before.

A skin is the drawing, the mount the creature (docs/mounts-design.md §1). So a skin can move the rider and the cart
to fit its pictures, when they differ from the mount's other skins: `MountSkinDef.Tuning` (a pack's `[Tuning]`
section; `TuningRide` / `TuningCart`, a pack's `[Tuning.Ride]` / `[Tuning.Cart]`, for one style, winning over it)
holds the picture-dependent tuning keys only (`SeatAdjustPx`, `SeatOffset`, `RiderOffsetPx`,
`TorsoOffsetPx`, `LegAdjustXPx`, `LegAdjustYPx`, `LegHeightPx`, `BackSeatExtraPx`, `HitchGapPx`, `Depth`,
`CenterOffset`; any other key is refused, said once in the log: speed, the cart, foals, the effect and abilities are
the mount's), which the API sets on the wearer as overrides owned by the skin (`SetBy` = `dreas.gk2.donkeyapi.skin`)
while it is worn, both styles, cleared when it comes off, never saved; a key another mod has set on that donkey is
left to it. `Offset` / `CartOffset` (a pack's `offset` / `offset_cart`) move the pictures on the donkey in art pixels,
riding and hitched. Whether foals wear a skin follows its mount (`MountDef.Foals`) and the player's
`FoalsWearSkin`; a skin has no foals flag of its own.

Every registration returns an `IDisposable`: dispose it to withdraw the skin; `Unregister(owner, id)` with the
skin's full name does the same. `Find(id)` takes the full name, and a clear short one too: a mount's id or its unique
name after the prefix gives its default (`saddleup.dragon`, `dragon`), a skin name only one mount has gives that skin
(`hellfire` finds `donkey/saddleup.hellfire`: the name after a skin's prefix too, as the dev names from before it).
Wearers show the game's own look from their next frame and keep their choice, so the skin returns with its provider.
`Report(id)` says what the loader found (pictures, names that match no frame, frames still to draw);
`SkinsChanged` fires after a registration or withdrawal. Register in `Awake`: a skin registered after Saddle Up's
settings page is made works at once through the cfg and is in its list from the page's catch-up (0.34.0).

A settings page can offer a pick list of ids: `page.List(section, key, MountLooks.DonkeyId, choices, label,
description, apply, look: true)`, with `choices` returning `(id, label)` pairs; the id is what the cfg holds, from
every menu alike, and a stored id that isn't in the list is shown as "not installed" and left alone.

## Effects (0.30.0)

> **Switched off in Saddle Up 0.10.0's download** (`Mounts.MountsEnabled` is false there; the next update switches it
> on): the lists hold the donkey alone or nothing, and a registration returns a handle that does nothing, with one log
> line per owner. Everything below describes the API as it works when switched on, and as it works now on a machine
> with the dev key `Debug.MountsPreview` on.

A particle effect on a donkey: the built-in **Hellfire** (the game's own lamp fire on a ring round the donkey, its
sparks, a trail of flames left behind) and the effects mods register with `Mounts.Effects`. A donkey shows one
through its tuning keys `Effect` (the id; `MountEffects.NoneId` = "none" or `""`: none) and `EffectWhen` (`MountEffectWhen`: `Off`, `Night`, `Always`;
kept with the save), or `MountCompanion.Effect` / `EffectWhen`. How effects show is everyone's, in
`Mounts.EffectSettings`: `EffectTrail` (on), `EffectWithCart` (off: a wider ring round the donkey and the cart),
`EffectInFights` (on), `EffectOnFoals` (off), `EffectNightBelow` (0.35: the sun value "at night" means). Never
indoors.

```csharp
handle.Tuning.Set(Guid, "Effect", "hellfire");                   // this donkey burns
handle.Tuning.Set(Guid, "EffectWhen", MountEffectWhen.Night);     // only at night
companion.Effect = "hellfire";                                     // the player's donkey (Saddle Up's Effect section)
Mounts.EffectSettings.Set(Guid, "EffectWithCart", true);           // the ring round the cart too, for every donkey
```

An effect of your own is a `MountEffectDef`: parts, each a **source** of particles with **emitters** placed round the
donkey. A source (`IMountEffectSource`) makes a template `GameObject` of particle systems once (the API keeps it,
never shown, and copies it per emitter; `Release` when it is dropped): the game's own through
`Mounts.Effects.GameParticles(materialName, addressableMaterialKey)` (a particle system drawn with that material,
copied and stripped to its particle components, the material held through Addressables so the game can't unload it
under the copies: the lamp flame is `("flame_fx", "Assets/AddressableAssets/Materials/flame_fx.mat")`, its sparks
`sparks_fx`), or your own prefab from your bundle. An emitter (`MountEffectEmitter`) is placed on a `Ring` (`Count`
emitters at `Radius`, local-space particles that move with the donkey), at the `Body`, or as a `Trail` (left behind
every `Every` units walked, world space, emitting for `Burst` seconds and burning out), with `Size`, `Height`, an
`Offset` and (ring) a starting `Angle`. `Foals` says whether foals may show it; `Show` is your own condition on top
of the built-in ones (mode, indoors, fights, foals). Hellfire is defined through exactly this: six flames and three
sparks on a ring of 0.8 at 1.5 times the lamp's size, one flame trail.

```csharp
var fx = new MountEffectDef { Id = "frost", Name = "Frost" };
var part = new MountEffectPart { Source = Mounts.Effects.GameParticles("sparks_fx", "Assets/AddressableAssets/Materials/sparks_fx.mat") };
part.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 4, Radius = 0.6f, Size = 1.2f });
fx.Parts.Add(part);
IDisposable reg = Mounts.Effects.Register(Guid, fx);               // dispose to withdraw; Unregister(owner, id) too
foreach (MountEffectInfo e in Mounts.Effects.Effects) Log(e.Id);   // "hellfire" and yours; EffectsChanged fires on changes
```

The API spawns, follows and scales the emitters (smaller on foals), rebuilds them when the cart is hitched or
unhitched, relights a source whose game particles weren't in memory yet, and removes everything with the view or the
donkey; nothing is attached to the game's pooled parts. A settings page offers the list with `page.List(...)` as for
skins (Saddle Up: the Effect section, `EffectRows`).

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

- **`examples/GK2DonkeySkins`:** two skins from code, a recipe (`donkey/example.sepia`) and a picture source (the default
  look `example.checker-beast/default`), and a mount with an ability (`example.checker-beast`: no cart, its own seat,
  burns while ridden).
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
- 0.29.0: fights from the saddle ([mounted-combat.md](mounted-combat.md)): the armour view and the attack states no longer end the ride (`MountRules`: attacks are not work), the keeper fights in the Ride style with the sword and the bow, aims up and down, strafes with the donkey walking backwards; the cart is refused while he wears armour (`Mounts.TryMount(Cart)`, `Hitch`, `MountCompanion.Use(Cart)` and `BringCart` all say why) and a hitched cart is left standing when the armour goes on. New `[Ride]`/`[Cart]` tuning keys `FocusSpeed` (0.5: the speed while aiming, times the riding speed) and `BackSeatExtraPx` (0 / 5: how much higher he sits when seen from behind). No change to the public surface. Fix (2026-10-14, found with the showcase director): at a hitch the old cart's `InstanceRemoved` was raised before `Hitched`, so the companion, which drops its cart handle on the removal, took its own cart for a foreign one at every hitch, left it without its key at the next unhitch, and would have spawned a second cart at the next cart key; now `Hitched` comes first (self-test checks the order).
- 0.29.1: no API change (the public surface is as in 0.29.0). Performance: the Mods-menu donkey picture and its
  labelled lists no longer look the window up while playing (2-3 ms frames four times a second), and a cart left
  standing and hitched again is no longer kept in memory ([performance.md](performance.md)).
- 0.33.0: `MountDef.Noun`, `MountInfo.Noun` / `EffectWhen` / `Tuning*` (its `Skins` Default first); `MountSpawn.Mount`
  (a mount by name, its Default skin); `SpawnFoal`'s size defaults to the parent mount's; `MountSkinDef.Regions` / `Erase` /
  `BaseRegions` / `SeatFromPicture` (a code skin says what a pack says); `MountSkinInfo.Regions`;
  `MountSettingsPage.EnableWhen` and `MountHasRegion` (a colour row for a region a mount never has: greyed in the
  look bar, left out of the Mods menu for that mount); the
  cart rule at `Mounts.Spawn(WithCart)` and a hitched mount that becomes a no-cart mount leaves its cart standing.
- 0.33.0: `MountLook.Muzzle` / `CustomMuzzle` and `MountCompanion.Muzzle` / `CustomMuzzle` (`MuzzleColor`: Game, Pale, Rose,
  Brown, Green, Blue, Dark, Custom; each preset moves every second-tone part by a clear step, measured on the
  donkey's muzzle, the rat's pink, the snail's shell and Hellfire's lava), the second body tone as a look preset like Coat and Harness, on the paper mount, the donkey
  and its foals; Saddle Up's "Second tone" row.
- 0.36.0: **skin layering** ([skin-layering-plan.md](skin-layering-plan.md)): every skin of a mount but the donkey is
  built on that mount's `default` (pictures, settings, colours, tuning, sounds); `layer = over` on it draws it over the
  default. `MountSkinInfo.BuiltOn` (the default's id or `null`) and `MountSkinInfo.OverDefault`; a code skin's
  sentinels (`SeatRow` -1, `Offset` zero and `CartOffset` null, `Effect` / `Description` / `Portrait` / `Regions`
  null, the canvas 96x96 and pivot 48,48) mean "the default's" on such a skin, `Effect = "none"` none; a pack's
  `seat = none`, `effect = none` and `portrait = none` say "not the default's"; `Over` on a code skin of another mount
  than the donkey is refused (drawn in its place). The sound layer `"default"` (`SoundSpec.Layer`, the coverage).
- 0.36.0: `Mounts.Catalog.Register` no longer throws when another mod has the mount's id: the one version rule
  decides (the higher version; at a tie code or a DLL over a loose folder; else the first stays), the loser gets a
  warning and a handle that does nothing; a code mount meets a pack's `mount.cfg` by the same rule (a newer one
  replaces it, an older one is refused with its skins); code mount and code skin clips are banked under their mod.
- 0.36.0 (breaking, pre-release): **each mount's own speed and cargo.** `MountDef.Speed` (`float?`, times the
  keeper's walking speed, 0.5 to 4; null: the donkey's, the API cfg's `[Ride]` / `[Cart]` `SpeedMultiplier`, 2 and
  1.5 as shipped), a pack's `[Mount] speed`; it wins over a `SpeedMultiplier` in the mount's tuning, and with the cart
  it is times the donkey's cart speed over its riding one (0.75 as shipped: the cart slows every mount alike; carts of
  their own speed maybe later). `MountDef.Cargo` / `[Mount] cargo` (`int?`, 1 to 6; null: 6): the items its cart
  carries, a stat for now (what a cart holds is still `CartSlots`). `MountInfo.Speed`, `CartSpeed` (derived),
  `SpeedWord` (the Mounts panel's word against the donkey's 2: under 0.7 of it Very slow, under 0.9 Slow, to 1.1
  Normal, to 1.35 Fast, above Very fast), `Cargo` (0 for a mount that can't pull the cart). A stat line named Speed or
  Cargo (`[Mount.Stats]`, `MountStat`) is left out with a report: the API writes those. Removed: `MountCompanion.RideSpeed`,
  `CartSpeed`, `SpeedLabels`, `SpeedLabelsFor`, `MountSpeed` (the user, 2026-10-22: the mounts define the speeds, the
  players' Faster / Fastest is dropped). `MountSettingsPage.RetiredKey(section, key)`: a setting dropped on purpose;
  its line leaves the player's file at Publish (any number of the section) and the upgrade self-test counts it as
  retired, not lost.
- 0.36.0: babies on foot: the `Mounts.Shuffle` key `ShuffleFollowKeeperOnFoot` (off in the core: only a mod's own
  foals get it), `MountCompanion.FoalsFollowOnFoot`, the `FoalsFollow` row in `FoalRows`; `MovedKey` takes several
  old places for one key (a call each; before, the last call won).
- 0.36.0: the mount sounds ([mount-sounds-design.md](mount-sounds-design.md)): `Mounts.Sounds` (`Catalogue`,
  `Events`, `Resolved`, `Set`, `Play`, `Stop`, the event `Played`), `MountSound`, `MountSoundEvent`,
  `MountSkinDef.Sounds` / `SoundsRide` / `SoundsCart` and `MountDef.Sounds` / `SoundsRide` / `SoundsCart`,
  `MountInfo.Sounds` / `SoundsRide` / `SoundsCart`, then `SoundsBaby` on all three and the `answer` event (the herd:
  a standing grown one's call, the babies' answers); the Behaviour key `StandingSounds` (on): standing mounts and
  babies make their sounds near the keeper.
- 0.36.0: never lost ([never-lost.md](never-lost.md)): `Mounts.ReturnToSafeSpot`, `Mounts.LastSafeSpot`, the event
  `Mounts.ReturnedToSafeSpot`, the Behaviour keys `FallGuard` (on) and `ReturnKey` (None); `MountSettingsPage.Button`
  (a button row of the mod's own: its action, the word on it, an enabled condition; `PanelButton` stays).
- 0.36.0: `MountDef.Flies` (false) and `Jumps` (true), `MountInfo.Flies` / `Jumps` (`[Mount] flies`, `jumps`):
  whether a mount flies and jumps, ridden plain; the Behaviour keys `JumpKey` and `FlyKey` (Space both: one key, a
  second press in the air takes off; `KeyboardShortcut`s, modifiers kept); `Mounts.FlyingEnabled` (jumping and flying
  switched on: dev only for now); `Mounts.Jump()`, `TakeOff()`, `Land()`, `Mounts.FlightState` and the event
  `FlightChanged` (`MountFlight`: Ground, Jumping, TakingOff, Flying, Landing).
- 0.36.0: the Behaviour key `FlyIndoors` (off): on, flying indoors is allowed, as a lift; off, no take-off indoors
  and a flight through a door lands. Saddle Up keeps it off.
- 0.36.0: the Behaviour key `CartIndoors` (off): with `DismountIndoors` off the cart comes indoors only when it is
  on; off, a cart ride through a building's door leaves the cart at the door and goes on as a ride (at the morgue:
  off inside, the corpse into the hands), and the cart is refused inside ("Not the cart indoors."). Before,
  `DismountIndoors` off let the cart in too: a mod that wants that sets both.
- 0.36.0: **the word is mount, not kind** (the user, 2026-10-21: one word for one thing). Renamed, a pre-1.0 breaking
  change: `MountKindDef` -> `MountDef`, `MountKindInfo` -> `MountInfo`, `MountKinds` -> `MountCatalog` at `Mounts.Catalog`
  (its list `Kinds` -> `All`, `KindsChanged` -> `MountsChanged`), `MountHandle.Kind` -> `MountHandle.Mount`,
  `MountEffects.KindId` -> `MountEffects.FromMount`; in a skin.cfg the colour mode key `kind = own|palette` is
  `colours = own|palette`. Nothing else changed.
- 0.35.0: **a value belongs to the owner that set it**: `InstanceTuning.Set` and `MountTuning.Set` refuse another
  owner's key (the mount's own stays replaceable), `TrySet` says so, `Clear(ownerId, key)` replaces `Clear(key)` on
  both; `MountAbility.Owner` (given by the API, cleared when the mount leaves), `Name`, `Description`; what a mount
  may pin is a rule (the ride and cart families but the lantern's, and the shuffle's); `MountSettingsPage.TuningKey`,
  `PinnedBy`; the Mounts panel (`PanelButton`, `OpenPanel`; the look rows leave the menus); `MountSkinInfo.IsAvailable` and `SetAsideWhy` (a game-source skin whose frames the game lacks is set aside: not offered, the donkey comes for whoever has it chosen); an ability never sets a look key (skin, effect, colours), held or free; the format:
  `MountSkinDef.Description` / `Portrait`, `MountDef.Description` / `Portrait` / `Stats` (`MountStat`),
  `MountSkinInfo.Description` / `HasPortrait`, `MountInfo.Description` / `HasPortrait` / `Stats` /
  `AbilityInfos` (`MountAbilityInfo`), a pack's `description`, `portrait`, `[Mount.Stats]`, `needs`; the tuning
  key `WalkCycleSpeed` (the stride, the donkey's 1.5); a canvas is at most 256; a game-source skin's frames are
  checked at game-ready and the skin set aside when they are gone (docs/mounts-foundation-2.md).
- 0.34.0: no change to the public surface. `MountSettingsPage.Publish` shows the page the frame after it is called
  (every plugin's Awake has run by then): `MountRows` and `PerMount` make their rows for the mounts registered
  meanwhile too, so a mount from a mod loaded after yours is in your Mount list, has its Skin row and its colour
  rows (docs/mounts-as-mods.md 4; before, it waited for a restart that never helped, BepInEx's order being fixed).
  A set skin or effect that isn't installed logs an Info, not a warning. The five mounts left the API for
  Saddle Up, which ships them as embedded packs through `RegisterEmbedded` (and since 0.36.0 the donkey's two skins and
  the Hellfire effect too: the API ships no skin or effect of its own).
- 0.33.0: `MountSkinDef.Effect` / `EffectWhen` (the effect a skin comes with, shown under "As the mount or skin";
  `null` = the mount's; the effect ring scales by the worn skin's drawn width); `MountSkinDef.TuningRide` / `TuningCart`
  (a skin's per-style geometry, `null` = none: the skin owns its drawing's geometry, docs/mounts-parity-matrix.md 9.1);
  `MountSkinInfo.SeatRow` (-1 = none), `SeatFromPicture`, `HasFrontLayer`, `FromGame`, `Effect` / `EffectWhen`,
  `Regions`; `MountDef.Noun` (`null` = the name, lower case). A declared seat (`SeatRow`, `SeatFromPicture`) no
  longer adds the player's own `SeatAdjustPx` (its defaults differ by style; the cart seat sat 6 px high): only an
  override of the key, the skin's `Tuning` or a mod's, nudges from it; the built-in rows moved by 6 (52 / 55 / 68)
  so the riding look is unchanged. Defaults of the new
  def fields are "as before": a def that sets none of them behaves exactly as under 0.32.0.
- 0.32.0: `MountSkinDef.SeatRow` (the canvas row the rider's hip sits on, for every player whatever their AutoSeat;
  a pack's `seat = <row>`; docs/seat-investigation.md). Mounts tab: a built-in's Reload and Undo work, and log.
- 0.31.0: `MountSettingsPage.Heading` (a heading row inside a section; a group of its own in the look bar, which now
  deals its groups to the column that leaves the left closest to half the rows). `Color` carries a `MovedKey` alias of
  its colour key over to its `<key>Lightness` key. Saddle Up 0.11.0 regrouped its page with it (Keys, Mount, Colours,
  Effect, Behaviour, Cart, Sound).
- 0.30.0: skins ([skins-design.md](skins-design.md)): `MountCompanion.Skin` (a skin id, "" the donkey) and
  `FoalsWearSkin`; the instance tuning key `Skin` (`handle.Tuning.Set(owner, "Skin", id)`, kept with the save; "donkey"
  or "" is the game's own look); `Mounts.Looks` (`Skins`, `Find`, `Report`, `FrameKeys`, `MountSkinInfo`, `Register` with
  an `IMountSkinSource` of sprites, `RegisterRecipe`, `RegisterFolder`, `RegisterEmbedded`, `Unregister`, `SkinsChanged`);
  `MountSettingsPage.List`, and `Toggle` takes `visible`; the built-in skins rat, snail and worm and the recipe hellfire (the
  recolour recipes zebra, ghost, ember, golden and shadow were retired on 2026-10-19 and Hellfire became a pack). Effects ([above](#effects-0300)): `Mounts.Effects` (`Effects`, `Find`, `Register`,
  `Unregister`, `GameParticles`, `EffectsChanged`), `MountEffectDef`, `MountEffectPart`, `MountEffectEmitter`,
  `MountEffectPlacement`, `IMountEffectSource`, `MountEffectInfo`, `MountEffectWhen`; the instance keys `Effect` and
  `EffectWhen`, `MountCompanion.Effect` / `EffectWhen`, `Mounts.EffectSettings`; Hellfire built in. Mounts
  ([above](#mounts-0300)): `Mounts.Catalog` (`Mounts`, `Find`, `Register`, `Unregister`, `MountsChanged`), `MountDef`,
  `MountInfo` (with `FoalScale`, the babies' size per mount), `MountAbility`, `MountHandle.Mount`, `MountSkinDef.Mount`, `MountSkinInfo.Mount`,
  `MountEffects.FromMount`, `MountSettingsPage.MountRows`; a pack's `[Mount]` section; the built-in creatures as mounts.
  Per-mount looks: `MountSettingsPage.PerMount`, `MountCompanion.LookOf`, `MountCompanion.MountId`, `MountLook`.
  `Mounts.MountsEnabled` (false in Saddle Up 0.10.0's download: the mounts other than the donkey, the skins and the
  effects are switched off, their registrations kept out with a no-op handle; the next update switches them on).
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
  The showcase is now GK2 Saddle Up (`dreas.gk2.saddleup`). A save's old side file and old owner ids are read and
  mapped (`docs/persistence.md`).
- 0.12.0: `MountHandle.LoadFromHead` and `TakeToHead`, loading and unloading a standing cart on foot (docs/load-on-foot.md).
- 0.11.0 (breaking, pre-release): `ComeToKeeper` gains `forRide` and `showMessage`, and refuses to bring a donkey for a ride that would be refused (found in play: Saddle Up's H indoors said "Not indoors." and the donkey came in anyway).
- 0.10.0 (breaking, pre-release): per-owner ids (`MountHandle.Id` is the owner's own number, `FullId`, `MountSpawn.Id`); `Mounts.Find(owner, id)`, `Find(owner, key)`, `InstancesOf(owner)`; `Mounts.GameReady`, `IsGameReady` (spawning before it is refused); `MountHandle.SetPersist(ownerId, keep)` and `SetKey(ownerId, key)` replace the setters (owner only); `GetData`, `SetData`, `DataKeys`; `SpawnFoal` gains `id`, `key`, `persist`; dormant records for owners not installed; side file format 2.
