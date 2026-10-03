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
| Second, still head on the walking donkey | The head layer's sprite when the animator started, the walk's fallback (see "The double donkey, settled" below) | The animator started again with the head layer empty (`RestartWithoutHead`); emptied before an idle view is switched on |
| (Found in the audit, not seen yet) Frozen or invisible donkey from reused parts | The idle tier's frozen state (animators off, renderers forced off) left on a view removed, sent away or unloaded while frozen | `ReleaseView` undoes the freeze first (`UnquietForPool`) |

## The guard (`DonkeyPrefab.cs`)

The game's `npc_donkey` prefab is the measure of a clean view. It's read once from the game's files.

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
| Colliders off, layer 26; navmesh cuts off | `MuteColliders` (alive: colliders), `PrepareParts` (both) | `RestoreColliders` (values recorded once per view life) |
| Cart part nodes: active, position, rotation | `PrepareParts`, the cart pose, slopes | `partRest` in `ReleaseView` |
| Colours (materials, LUTs) | `ColorsRig` | `ColorsRig.Detach` |
| Lantern pole and lights; the hanging lamp object | `LanternRig`, `LampHang` | `LanternRig.Detach` (lamp unparented, then destroyed: `Destroy` waits for the frame's end, and the first `[pool]` check, 2026-10-03, found the lamp still on returned views) |
| Cargo holder and cargo meshes (`cart_cargo`) | `CargoRig` | `CargoRig.Detach` (destroyed) |
| Rider's near leg (`mount_rider_legs` under `bdy`) | `RiderLeg.Attach` | `RiderLeg.Detach` (unparented, then destroyed) |
| Sprites | the animator, the game's skin | `ResetSprites` in `ReleaseView` (the parts go back as the prefab has them) |
| The walk's head fallback | the animator, when it starts | `RestartWithoutHead` in `PrepareParts`; emptied before an idle view is switched on (see below) |
| Animator facing / pose after a view comes back on | the animator reset on enable | sent again: `QuietAnimators(false)`, `TakeParkedView`, `PrepareParts` |
| Walk animation rate | `SetWalkAnimationSpeedMultiplier` | an animator parameter, reset when the animator is rebound |

**Rule for new code:** an object the mod adds to a view is unparented before `Destroy` (which waits for the frame's
end). Beyond that, anything that changes a donkey view gets a row here and an undo in `ReleaseView`. If the
`[pool]` check doesn't already cover it, extend `DonkeyPrefab.Audit`.

## The "double donkey", settled (2026-10-03, measured)

**What it is:** a still head (`0301_hed_static_down`) drawn over the walking body.

**How the animator does it:** the walk clips don't draw the head layer (`hed`); the walking body has its own head.
So during a walk that layer shows its fallback (write defaults): the sprite it held when the animator **started
running**. Measured with `Animator.WriteDefaultValues` (the `[defaults]` probe, removed since):

| Moment | The walk's head fallback |
|---|---|
| A new view, as the game hands it over | `0301_hed_static_down(Clone)`: the game's skin swap put that copy there before the animator started |
| After `Animator.Rebind` | unchanged |
| After switching the Animator component off and on | unchanged |
| After switching the animator's object off and on, the head layer empty | `none`, and the walk is clean |
| A pooled view switched on with the head layer empty | `none` |

**Why it showed only without the cart, and only now and then:** the game's skin swap usually hides that head copy
during a walk (it switches the renderer off), but not reliably. Views reused from the mod's pool (donkeys without a
cart only) were switched on holding the prefab's own head, which nothing hides.

**The fix** (`HeadWatch.cs`):
- `RestartWithoutHead`, first thing in `PrepareParts`, for every view: the head layer emptied, then the animator's
  object switched off and on.
- Before an idle (frozen) view is switched back on (`Tiers.cs`): the head layer emptied. No `PrepareParts` follows
  there, and switching on starts the animator.

**The tripwire:** `HeadWatch` checks every view a few times a second. A head sprite during a walk logs once per view:
`[heads] ... a still head (...) over the walk; its view was ...`. It doesn't hide it: a hidden fix hid the cause
for new views until the probe found it.

**Tried and removed, with the reason:**
- Resetting the sprites to the prefab's before binding: the prefab's own head layer holds the still head, so this
  brought the bug back.
- `Rebind`: it doesn't change the fallback.
- Emptying the head layer before a pooled view is switched on, and when a view goes back: covered by
  `RestartWithoutHead`.
- Emptying the head layer on every walk frame: it hid the problem rather than fixing it.
