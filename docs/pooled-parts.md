# Pooled parts: what the mod changes on a donkey view, and where it's undone

The game's donkey parts are recycled:
- **The game's own pool:** a destroyed `Wgo` hands its parts back, and the next `Wgo.Spawn` of an `npc_donkey`
  reuses them. That includes the game's own delivery donkey.
- **The mod's view pool:** `ViewPool` in `Tiers.cs` parks views of sleeping donkeys and gives them to the next one
  that wakes.

So anything the mod changes on a view and doesn't undo is carried into the next donkey. Three bugs of 2026-10-03
were exactly that:

| Bug | What was carried over | Fix |
|---|---|---|
| Keeper thrown up and stuck in a doorway | A reused or new view's `Obstacle` capsule was live for a physics step before `PrepareParts` muted it | `MuteColliders` the moment a view comes alive |
| Second, still head on the ridden donkey | A part's head sprite (`hed_static_down`), taken by the animator as its bound default (the walk clips don't key the head; write defaults) | Sprites reset to the prefab's and the animator rebound in `PrepareParts`; sprites reset when a view goes back |
| (Found in the audit, not seen yet) Frozen or invisible donkey from reused parts | The idle tier's frozen state (animators off, renderers forced off) left on a view removed, sent away or unloaded while frozen | `ReleaseView` undoes the freeze first (`UnquietForPool`) |

## The guard (`DonkeyPrefab.cs`)

The game's `npc_donkey` prefab is the measure of a clean view. It's read once from the game's files.

- **When a view is prepared** (`PrepareParts`): every sprite is set to the prefab's, then the animator is bound
  again (`ResetHeadDefault`). Whatever the animator takes as its defaults is therefore the prefab's, whatever the
  parts held before. Logged as `[view] ... sprites set back to the prefab's`, for the first 5 views that differed.
- **When a view goes back** (`ReleaseView`, after the mod's own restores): sprites are reset, then what the mod
  changes is compared with the prefab, put right, and logged as a warning:
  `[pool] ...: its view went back to the pool with N leftover(s), put right: ...`.
  - Renderers forced off.
  - Collider layers. The mod moves colliders to layer 26; whether a collider is on is partly the game's, so only
    the layer is compared.
  - Navmesh cut objects.
  - The head sprite.
  - Animators: switched off, or at a speed other than 1.
  - The mod's own objects still attached: `mount_rider_legs`, `gk2_hanging_lamp` and `cart_cargo`. These are
    destroyed.
  - Objects the prefab hasn't and the mod doesn't make are named once (`... left as it is`) and never touched. They
    may be the game's.
- **What to do with a `[pool]` warning:** a change of the mod wasn't undone. Find the code that makes it and undo
  it in `ReleaseView`. The guard has already put the part right, so nothing else is affected meanwhile.

## Every change the mod makes to a view, and where it's undone

| Change | Made in | Undone in |
|---|---|---|
| Renderers hidden until set up (`forceRenderingOff`) | `HideRenderersNow` (spawn, reuse) | `RevealDonkeyIfDue`; `ReleaseView` |
| A free cart's donkey hidden | `HideDonkeyOnly` | `ReleaseView` |
| Idle freeze: animators off, keep-state on, renderers off | `QuietAnimators(true)` (`Tiers.cs`) | `QuietAnimators(false)`; `ReleaseView` → `UnquietForPool` |
| Whole view switched off (idle) | `QuietAnimators(true)` | `QuietAnimators(false)` (the `Wgo` itself, not the parts) |
| Colliders off, layer 26; navmesh cuts off | `MuteColliders` (alive), `PrepareParts` | `RestoreColliders` (values recorded once per view life) |
| Navmesh holes held by the data (`GlobalNavigationManager`) | the game, for the data | `NavHoles.Clear` (spawn, prepare, every second, destroy) |
| Cart part nodes: active, position, rotation | `PrepareParts`, the cart pose, slopes | `partRest` in `ReleaseView` |
| Colours (materials, LUTs) | `ColorsRig` | `ColorsRig.Detach` |
| Lantern pole and lights; the hanging lamp object | `LanternRig`, `LampHang` | `LanternRig.Detach` (lamp unparented, then destroyed: `Destroy` waits for the frame's end, and the first `[pool]` check, 2026-10-03, found the lamp still on returned views) |
| Cargo holder and cargo meshes (`cart_cargo`) | `CargoRig` | `CargoRig.Detach` (destroyed) |
| Rider's near leg (`mount_rider_legs` under `bdy`) | `RiderLeg.Attach` | `RiderLeg.Detach` (unparented, then destroyed) |
| Sprites (the head's bound default) | the animator, the game's skin | `ResetSprites` in `PrepareParts` and `ReleaseView` |
| Animator facing / pose after a view comes back on | the animator reset on enable | sent again: `QuietAnimators(false)`, `TakeParkedView`, `PrepareParts` |
| Walk animation rate | `SetWalkAnimationSpeedMultiplier` | an animator parameter, reset when the animator is rebound |

**Rule for new code:** an object the mod adds to a view is unparented before `Destroy` (which waits for the frame's
end). Beyond that, anything that changes a donkey view gets a row here and an undo in `ReleaseView`. If the
`[pool]` check doesn't already cover it, extend `DonkeyPrefab.Audit`.
