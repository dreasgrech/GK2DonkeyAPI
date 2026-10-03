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
| Colliders off, layer 26; navmesh cuts off | `MuteColliders` (alive: colliders), `PrepareParts` (both) | `RestoreColliders` (values recorded once per view life) |
| Cart part nodes: active, position, rotation | `PrepareParts`, the cart pose, slopes | `partRest` in `ReleaseView` |
| Colours (materials, LUTs) | `ColorsRig` | `ColorsRig.Detach` |
| Lantern pole and lights; the hanging lamp object | `LanternRig`, `LampHang` | `LanternRig.Detach` (lamp unparented, then destroyed: `Destroy` waits for the frame's end, and the first `[pool]` check, 2026-10-03, found the lamp still on returned views) |
| Cargo holder and cargo meshes (`cart_cargo`) | `CargoRig` | `CargoRig.Detach` (destroyed) |
| Rider's near leg (`mount_rider_legs` under `bdy`) | `RiderLeg.Attach` | `RiderLeg.Detach` (unparented, then destroyed) |
| Sprites (the head's bound default) | the animator, the game's skin | `ResetSprites` in `PrepareParts` (then the head layer cleared before binding: the prefab's own is the still head) and `ReleaseView` |
| Animator facing / pose after a view comes back on | the animator reset on enable | sent again: `QuietAnimators(false)`, `TakeParkedView`, `PrepareParts` |
| Walk animation rate | `SetWalkAnimationSpeedMultiplier` | an animator parameter, reset when the animator is rebound |

**Rule for new code:** an object the mod adds to a view is unparented before `Destroy` (which waits for the frame's
end). Beyond that, anything that changes a donkey view gets a row here and an undo in `ReleaseView`. If the
`[pool]` check doesn't already cover it, extend `DonkeyPrefab.Audit`.

## The head came back (2026-10-04): the head watch

The still head over the walk came back while riding after a few house exits, with no `[pool]` or `[view]` line: the
reset and the audit above saw nothing wrong, so it came by another way. Not guessed this time: `HeadWatch.cs` checks
every view a few times a second. The walk clips never draw the head layer (`hed`; the walking body has its own head),
so a `hed` sprite while every playing clip is a walk is this bug, whatever caused it.
- On a hit: `[heads] ... a still head (...) over the walk (...); its view: <where it came from> N frame(s) ago,
  animator last bound by the mod ..., since then: <prepared, mounted, switched off/on, ...>`, then the view is put
  right (sprites, the animator bound again, facing sent again). At most 3 fixes per view, so never a loop.
- Once a session: `[heads] first ridden walk: playing ...`, which shows the check matches the real clip names.
- **Found (second test, same day):** the game's prefab itself holds `0301_hed_static_down` on the head layer. The
  guard's reset to the prefab put exactly that sprite back just before the animator was bound again, so it became the
  default the walk shows: the guard brought the bug back (the first fix had cleared the head). The `[heads]` lines
  showed it on a view from the mod's pool, and the head back 12 frames after each of the watch's resets.
- **First fix, not enough:** `ResetHeadDefault` resets to the prefab's sprites, then clears the head layer before
  `Rebind`. The third test still showed the head on pooled views: binding again later didn't change the fallback.
- **The clue (the user):** it never happens with the cart. A donkey with a cart never takes a parked view, and the idle
  switch-off skips carts too; both switch a view off and on again. Switching on builds the animator anew from the
  sprites the view holds at that moment, and `ReleaseView` had left the prefab's still head there.
- **Fix:** the head layer is emptied right before a view is switched on (`EmptyHeadLayer`: `TakeParkedView` and the
  end of idle). Backup: `HeadGuardFrame` empties the head layer on every frame of a walk state, after the animator.
  The `[heads] ... the walk's head layer falls back to ...` line (once per view) shows whether the root fix holds:
  after it, the line should never appear.
- **Status after the fourth test (2026-10-03, 13:50 log):** the pooled views walk with no head layer: root-caused and
  fixed. But three views made by `Wgo.Spawn` (with and without the cart) logged the fallback line: on those the
  backup did the work. They reuse the game's pooled parts, which come back from `ReleaseView` holding the prefab's
  head (the reset and the audit put it there). So `ReleaseView` now empties the head layer last too.
  - **To read next:** if the fallback line stops for `Wgo.Spawn` views, the backup is only a safety net. If not, the
    fresh views' fallback is set some other way, still unknown.
  - **Then (step two, one change at a time):** `ResetHeadDefault`'s `Rebind` (and the sprite reset in `PrepareParts`)
    didn't change the fallback in test 3, so it may do nothing. Remove it and see whether the fallback lines come
    back.
