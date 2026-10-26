# GK2 Donkey API: foals

This was investigated on 2026-09-30 against the Steam build 25601286.

**Built 2026-09-30 (API 0.8.0), not yet tested in game.**
- **Code:** `src/GK2DonkeyAPI/Foals.cs`, with the shuffle in `IdleShuffle.cs`.
- **API:** `Mounts.Spawn` with a `MountSpawn`, and `Mounts.SpawnFoal`.
- **Panel:** "A donkey family" and "Add a foal" on the Instances tab.

**User decisions:**
1. When the parent is ridden away, the foals trot after it, and fall back to waiting and catching up out of sight.
2. The general features (scale, rideable, parent) with `SpawnFoal` on top.
3. Foals ignore the keeper.
4. The showcase's own donkey gets no foals yet, but a family can be spawned from the panel.

**The idea:** cute foals roaming around a parent donkey. They are small donkeys that can't be ridden and only
wander around their parent. The request is to find the most scalable way to do this, one that fits the API.

## Short answer

- **Scale is built into the game.**
  - `WgoData.Scale` (`WgoData.cs:329`) is applied at every spawn: `obj.localScale = wgo.Data.Scale` (`Wgo.cs:309`).
  - The game sets it only for town decorations (`TownSystem.cs:109-157`).
  - The scaled object is the view's **own new root** (`new GameObject()` in `Wgo.Spawn`), and the pooled parts hang
    under it. So scaling never reaches the parts the game reuses for its delivery donkey.
  - Our donkeys are temp `WgoData` that are never saved, so the scale isn't saved either.
- **A foal is three general building blocks together, not a special kind of object:**
  1. **scale**, for any instance;
  2. **not rideable** (and never a cart), for any instance;
  3. **shuffle around another instance**: its idle-shuffle home follows a parent instead of staying where it was
     left.

  Each one is useful on its own: a large show donkey, a donkey only a quest may ride, two donkeys grazing
  together. `Mounts.SpawnFoal` would then be a two-line convenience over them.
- **Most of the work already exists:** the idle shuffle, instance settings and the registry. The new parts are the
  scale, the rideable flag, the anchor, and what foals do when their parent leaves.

## What the game and the mod already give

| Need | Already there |
|---|---|
| Smaller donkey | `WgoData.Scale` before `Wgo.Spawn` (the game's own mechanism); the whole view scales: body, head, shadow (`Cylinder Shadowcaster`), grass bending (`DeformTextureSprite`), bubble point |
| Its own look | `MountHandle.Tuning` with colour keys (a lighter coat, say): the instance's own `Colors.Rig` |
| Moving about | The idle shuffle (docs/roaming.md): small slow steps, left/right facing, ground checks, pauses |
| Many of them | The registry: any number of instances, each with its own settings; views only while their scene is loaded |
| Not in the save | Temp `WgoData`, as every instance |

## What scale changes

- **The body offset** (the body drawn about 0.48 to the facing side of the root) shrinks with the scale. The shuffle
  already **measures** it from the view (`MeasureLateral`), so this is automatic. `BodyPoint` and `HintPoint` are
  measured too.
- **The walk cycle has to be faster.** Shorter legs cover less ground per cycle, so the animator's rate should be
  `speed / (1.5 × scale)`, not `speed / 1.5`. At 0.6 scale and the shuffle's 0.5 u/s, that is 0.56 of the clip's rate,
  against 0.33 for a full-size donkey at the same speed.
- **Distances in the shuffle** (area, step length, speed, spacing to others) are world units. A foal gets its own
  values through the instance tuning (smaller steps, a wider area around the parent). Spacing needs one change,
  below.
- **Riding and the cart are not scaled.**
  - The seat, legs, depth, rider offsets, cart parts, cargo bed and lantern are tuned in art pixels and world units
    for a full-size donkey.
  - Scale is therefore only allowed on instances that **can't be ridden and have no cart**, at first.
  - A scaled rideable donkey would need every ride offset multiplied, which is a separate job.
- **Pixel art:**
  - The world is drawn into a smaller pixel-art texture and then enlarged (`CameraSystem.WorldToScreenPoint`), with
    `GRAPHICAL_PIXEL_SIZE = 2`.
  - A scaled sprite is resampled onto that grid, so some art pixels are dropped and the foal looks a little crunchier
    than the parent.
  - 0.5 is the cleanest scale: every other pixel. It may shimmer while walking.
  - **Measure first:** screenshots at 0.5, 0.6 and 0.7, standing and walking.
- **Positions** stay pixel-rounded (`GetRoundedPosXYZ`), as now.

## Proposed API (additions only, API 0.8.0)

A spawn spec keeps `SpawnDonkey`'s signature stable and can grow without breaking callers:

```csharp
var spec = new MountSpawn
{
    Position = at,
    FacingRight = true,
    WithCart = false,
    Scale = 0.6f,                 // 1 = full size; below 1 needs Rideable = false and no cart (at first)
    Rideable = false,             // no E prompt, Ride() refused, never hitched
    Parent = mother,              // a MountHandle: its home follows the parent (below)
};
MountHandle foal = Mounts.Spawn("you.gk2.mymod", spec);

// Convenience for the common case (scale 0.6, not rideable, parent set, shuffle on for it):
MountHandle foal2 = Mounts.SpawnFoal("you.gk2.mymod", mother);

foal.Tuning.Set("you.gk2.mymod", "CoatBrightness", 0.15f);   // its own look, as any instance
```

**On `MountHandle`:**
- `Scale` (get).
- `IsRideable` (get).
- `Parent` (get), and `Children` (the instances whose parent it is).
- Maybe `SetParent(MountHandle)` for re-parenting, e.g. adopting a foal.

**Rules:**
- **A foal's shuffle is on by itself.** `SpawnFoal` sets the instance's own `IdleShuffle = true`. A foal that stood
  still would be pointless, and it doesn't depend on the mod's global switch.
- **A parent's removal takes its children with it** by default (`MountSpawn.RemoveWithParent = true`). With it off,
  the children become orphans and shuffle where they are.
- **Events:** the existing `InstanceSpawned` and `InstanceRemoved` cover foals. `Parent` says which is which.
- **Refused combinations** (`Mounts.Spawn` returns null and logs why, as now):
  - a scale other than 1 with `Rideable` or `WithCart`;
  - a parent in another scene;
  - a parent that is itself a child (no chains, at first).

## Shuffling around a parent

- **Home:** the parent's body point, taken again whenever the parent's place changes. The foal's box is centred
  there.
  - The foal gets its own larger area, e.g. `ShuffleAreaWidth = 1.8` and `ShuffleAreaDepth = 0.8`, so it plays
    around the parent rather than under it.
- **Spacing:** the shuffle now keeps 1.1 away from every other instance. Two changes:
  - the gap scales with the two instances' scale;
  - the parent counts only for its body, about 0.5, so a foal can stand beside its parent and next to its siblings
    at about 0.4.
- **While the parent shuffles,** the foals follow the new home at once. The parent's steps are small, so their box
  just moves with it.
- **Keeper close:** foals don't hold still for the keeper, since nobody rides them. Maybe they should keep a little
  distance instead. That's a decision.

## Hops, getting stuck, and stopping for good (studied and built 2026-10-26, API 0.40.0, not seen in game)

**As built** (the user's answers: hops moving and standing, "now and then", a cfg key only; and "No, it stayed stuck":
walking back to a stopped baby did **not** wake it, so the level cause below is real but not the whole story):
- `FoalHops.cs`: `ShuffleFoalHopChance` (0.2) per lively step; every 4-10 s trotting, 8-20 s standing (scaled by
  0.2 / the chance). `HopTick` before `Stand()`, `ResetHop` in `StopShuffle`.
- Hop sizes (the user: "sometimes they do little hops and sometimes they do big jumps"): each hop's let-go time is
  picked from a flick to past the rise (`StartHop`; `u²`, mostly small), so the mount's own jump goes from its tap
  height to its full height (donkey foal 10 to 17 px).
- Jumping along: `Flight.Jump` calls `KeeperJumped`. The ridden mount's lively babies following it (not lost, within
  10) each jump with `ShuffleFoalJumpAlongChance` (0.75), 0.05-0.4 s later, leaning big (`√u`). On foot the keeper
  never jumps, so there is nothing to answer there.
- The babies' answers to a call (Sounds.cs `PlanAnswers`): a shuffled order, gaps 0.04-0.18 (a quarter), 0.25-0.6 or
  0.7-1.2 (15 %), pitch 0.94-1.07 each; they were in spawn order, 0.35 s apart.
- Levels: `WantTier` keeps a baby with the keeper Active (Tiers.cs); the Details row says "with the keeper".
- Routes: `FoalRouteSearch` takes the trot's `LineWalkable` as `legClear` for its pulled legs, the first from the
  foal's body; up to two quick re-plans within a second (`QuickReplan`, reset at a corner, its slot, a catch-up).
- Rejoin: routes within 3 x `ShuffleRouteRange` (24); `wedged` no longer stops a rejoin; lost 10 s and out of sight:
  caught up even while the lead walks (`LongLostS`).
- **The measurement for the cause still unknown:** `WatchFollowing` (every level, once a second): a baby that hasn't
  got 0.5 nearer its lead for 20 s, the lead in its scene and not riding away, writes one `[foal] ... has not got
  nearer` line with its level, view, shuffle status, phase, lost / wedged / with the keeper, picks failed, route and
  rejoin state, once until it is near again. The next stuck baby's line says which path held it.
- Self-test: "foals: follow the keeper on foot outdoors" checks the level rule and a hop (docs/self-tests.md).

**The user (2026-10-26):** "make the babies jump occasionally as part of their movement", and the babies "are getting
stuck a lot and sometimes they just stop following you completely after a while of playing".

### Stopping for good: the levels freeze a baby that follows the keeper (code read, not measured in game)

- `WantTier` (Tiers.cs) keeps a foal Active only while its **parent** is Active. A baby following the keeper on foot
  has no such rule: once the keeper has walked away from the parent (the parent goes idle or asleep), the baby's
  level is its own distance to the keeper.
- Fallen behind (over 15 away, 18 to leave Active) and off screen, it goes **Idle**: frozen, and `StandTick` returns
  before `ShuffleTick`, so it never follows or catches up again.
- Beyond 35 it goes **Asleep** (no view). `AsleepTick` only catches up with the **parent**, and only one that is not
  ridden and has its view. The keeper is never looked at, so the baby stays wherever it fell asleep.
- It wakes only when the keeper comes back within 15 of it, or it comes on screen. **The check that tells this cause
  apart:** a baby stopped this way moves again as soon as you walk back near it.

### Getting stuck (the log of 2026-10-26, lines 637-666, and the code)

1. **A route the trot refuses.** The route's corners are pulled straight on the grid: the line through cell
   **centres** (`FoalRouteSearch.ClearLine`), starting from the start cell's centre, not from the foal's body. The
   trot checks each leg with `LineWalkable`, a body capsule every 0.2 along the real line, which can clip a corner
   the cell centres miss. In the log: "route ... found, 2 corner(s)" and in the same second "can't follow ... follow
   sample 3 ... blocked by Obstacle".
2. **No second try.** A leg refused within a second of the plan (`nextRouteAt`, one plan a second) loses the parent
   at once (`FollowWithRoute`), instead of planning again from where the foal stands.
3. **Rejoining only within 8.** A lost foal plans a way round only when the lead is within `ShuffleRouteRange` (8),
   though the search already makes a partial route towards a target further away. Beyond that it waits for the
   out-of-sight catch-up.
4. **Wedged stays wedged in view.** `wedged` (three world-blocked picks in 8 s) turns off both ways back
   (`!wedged`). The only way out is the catch-up, which needs the lead to stand 1.5 s with the foal off screen, or
   6 s. A keeper walking about in view leaves it standing there.

The log is lossy here: `DiagLog` drops lines past 4 a second (line 532: "27 more line(s) skipped"), so a missing line
proves nothing. `ShuffleFollowAlways` is on (the user's cfg), so the distance limits don't apply.

### Hops (the design)

- **The arc** is the mount's own jump (`JumpShapes.Of`, mount.cfg `[Jump]`) through `Flight.JumpHeightAt`, tapped
  (the tap height, 16 px by default), times the foal's size: about 10 px for a 0.6 donkey foal. A mount whose jumps
  are off: no hops.
- **Drawn only:** the lift is added in `Stand()` where the view's place is written (pixel-snapped). `Position` stays
  on the ground: the shuffle re-homes whenever `Position` changes behind its back (`shuffleWrote`), and
  `BodyPoint()`, the slots and the spacing use `Position.y`.
- The dragon's take-off frames are not used for a hop: the walk or idle pose goes on in the air.

## Following the keeper on foot (API 0.36.0, built 2026-10-22)

The user asked for babies that follow the keeper on foot outdoors too, not only indoors or when he rides their
parent. Saddle Up's "Babies follow you on foot" (Babies section, on by default: the user, 2026-10-22) switches it on
for its own foals. The row's default is the companion's `FoalsFollowOnFoot` when `FoalRows` is called (off in the
core; Saddle Up sets it on first), so another mod's page starts off unless it says otherwise.

- **Only the mod's own foals.** The Shuffle key `ShuffleFollowKeeperOnFoot` is off in the core. A companion sets it on
  its own foals (`MountCompanion.FoalsFollowOnFoot`, as `FoalsComeIndoors` sets `ShuffleFollowKeeperIndoors`), so a
  foal any other mod spawns keeps to its parent.
- **The lead** is the keeper (`withKeeper`, `LeadNow`) while he is on foot outdoors in the foal's scene: the steady
  check (once a second while he has control, `FoalsIndoors.cs`) joins it without placing it, so it trots over, or
  catches up out of sight when it fell behind (the catch-up stays out of view, as for a parent).
- **He gets on a mount:** the foal goes back to its parent. When the mount is the parent, that is following him
  anyway; another mount: it returns to its parent. Off again: with him within a second.
- **Doors:** outdoors on the far side (a scene change), the foals with him stay with him, placed by him while the
  screen is black. Into a building: with "Babies come indoors" as before; without it they go back to the parent
  outside and join him again when he comes out on foot.
- **The other babies settings:** the count, "the same mount and skin" and the lively steps work as before; the
  lively steps go round the keeper while they stand with him.
- **Self-test:** "foals: follow the keeper on foot outdoors, with the other babies settings" (docs/self-tests.md).

## Lively foals (API 0.20.0, built 2026-10-03, not yet seen in game)

**Why:** the user, 2026-10-03: the foals "idle in the same spot a lot", and move "from point a to b, then stop, and
repeat". They shuffled like grown donkeys: a 5–15 s pause after every 0.3–0.9 step at 0.5 units/s (about a tenth of
the time moving), in a 1 x 0.5 box, and kept 0.7–0.9 from each other, which refused most steps in a family.

**What changed** (`IdleShuffle.cs`, all per instance through `Mounts.Shuffle`; grown donkeys unchanged):
- `ShuffleFoalLively` (on) gives a foal its own values instead of the grown donkey's:
  - **area** 2 x 1 around its spot next to the parent (the follow's home range grows with it);
  - **steps** 0.4–1.5, each at its own speed, 0.5–1.2 units/s (the walk animation matches the speed);
  - **pauses** 1–4 s;
  - **chained steps:** half the steps go straight on into another within 0.4 s, in a fresh direction or after a
    turn, so a wander is a few strides instead of a stop after each;
  - **quick trots:** 12 % of steps trot at 2.5 units/s and go further (up to 1.5 x the longest step), easing in and
    out over a longer stretch.
- **Spacing:** foals keep 0.8 of the usual gap from each other, never under 0.55 (they never stand inside each other).
- **Wedged:** a foal counts as wedged (and is caught up) only after three missed picks in a row *and* 8 s; with short
  pauses three misses came in about 3 s.
- Every step still stays within `ShuffleMaxAngle` of the screen's horizontal (side views only).

**Ideas held back** until this has been seen: chasing a sibling (one trots to another, which trots off), visiting the
keeper when he's near, staggered follow speeds.

## When the parent leaves (the open question)

Nothing in the mod or the game makes a donkey travel a route; the shuffle only takes short straight steps. When the
keeper rides the parent away, the foals can:

| Option | What it looks like | Cost |
|---|---|---|
| **A. Wait** | They stay and shuffle where the parent stood. When the parent is parked again, they appear next to it, either as soon as nobody sees the jump (off screen) or after the parent has stood a few seconds | Small: re-home + an off-screen check |
| **B. Trot after it** | They follow in straight lines at a trot, faster than the shuffle and with a quicker walk cycle, and stop when they would walk into something. When the parent is far away or out of sight, they catch up the way A does | Medium: a follow mode in the shuffle, reusing its ground checks |
| **C. Hide while it's ridden** | They vanish when the ride starts and appear around the parent when it's parked | Smallest, but they visibly pop out |

**Recommended: B, falling back to A.** They trot after the parent while the straight line is clear. If it isn't, or
the parent is more than about 6 units away or through a door, they catch up out of sight. It looks alive, and it
never needs route finding.

## Scaling to many foals

- **Each foal is a full donkey view:** an animator, and its own colours. With no colour overrides that is no texture
  work; a coat override costs about 4 ms per change (the log's "an update took 4.4 ms") and nothing per frame.
- **The shuffle's ground checks run only when a step is picked:** every 5–15 s, a few dozen raycasts and capsule
  checks.
- **No cap on children** (removed 2026-09-30: the user asked, and the only reason was the six fixed slots). The
  slots are made per index, two per row with each row 0.6 further out. The cost grows per foal: one view each, and
  ground checks when it picks a step or a target.

## Measure first (before building)

1. **Scale:** spawn at 0.5, 0.6 and 0.7 (a panel button, "Spawn a foal next to the edited donkey") and take
   screenshots standing and walking. Check whether the shadow and the grass bending look right.
2. **Walk cycle:** at `walk_speed = speed / (1.5 × scale)`, do the hooves match the ground?
3. **Sorting:** does a small foal standing right behind the parent's legs sort correctly? The game sorts by
   position, and a foal is shorter.
4. **Idle one-shots:** do graze, ear and lips play normally at scale? They should, since they are the same animator.

## As built

- **Spawn checks** (`Mount.Spawn`), each refused with a reason in the log:
  - a scale outside 0.2–2;
  - a scale other than 1 on a donkey that can be ridden, has a cart, or is a free cart;
  - a parent that is gone, is a cart or the summon, or has a parent itself;
  - a cart with a parent.
- **A child** is spawned into its parent's scene.
- **Foals can't be ridden or hitched:**
  - `RideFromWorld` says "too small to ride";
  - `Hitch` refuses a scaled or unrideable donkey;
  - the interact prompt and the panel's Ride and Hitch buttons skip them.
- **Scale:** `data.Scale = Vector3.one * ViewScale` before `Wgo.Spawn`.
- **Walk cycle rate:** `speed × ShuffleAnimSpeed / scale`. It is updated while trotting, when the speed changes by
  more than 0.15.
- **Following** (`FollowParentTick`):
  - **Around obstacles:** since 2026-09-30, a blocked straight line starts a route search (docs/foals-pathing.md).
    The straight-line rules below still hold in the open.
  - **Home** is the parent's body, taken again every frame.
  - **Trotting starts** when the foal is further from the parent than its area plus 0.6.
  - **Target:** its slot on the side of the parent the foal keeps, `ShuffleFollowGap` (0.9) from the parent's body.
    The siblings' slots are up to 1.1 further out. With a cart, the cart side's slots are alongside the cart, level
    with the donkey. Behind the cart they stood about 2.6 from the donkey (the user, 2026-09-30). Each sibling has its own slot, and the shuffle's area is centred on it.
    - A foal changes side only when it is clearly (over 0.3) on the other one. Riding away leaves it behind anyway.
    - The first build put the slots behind the parent's facing. Every turn of a donkey with a cart then moved them
      five units, and the foals trotted through the cart (found 2026-09-30).
    It trots to a target it can reach in a straight line: its own slot, else the nearest other free slot, else the
    side of the parent it comes from. The target is picked again every 0.5 s.
  - **Speed:** `ShuffleFollowSpeed` (4; riding is about 6.6), slowing over the last 0.6. Far behind it goes faster,
    up to 1.75× from 8 units out.
  - **`ShuffleFollowAlways`** (off): no distance limit. It stops only when blocked, and rejoins from any distance when a
    straight way is clear. Long lines are sampled every 0.2, up to 80 samples.
  - **Facing:** it turns only when the direction's sideways part is over 0.35, keeping the body in place. The same
    one-frame turn as the shuffle.
  - **The way ahead** is checked 0.6 ahead every 0.25 s, with the shuffle's ground and collider test. The keeper's
    own collider is ignored.
- **Losing the parent:**
  - When: the way is blocked, or the parent is further than `ShuffleFollowMaxDistance` (10). At the defaults a foal keeps up for about 4 s of riding.
  - Then: the foal shuffles around where it is.
  - **Rejoining:** it trots after the parent again as soon as the parent is back within 0.8 ×
    `ShuffleFollowMaxDistance` (8) and the straight line to it is clear, ridden or not. This is checked every 0.5 s,
    using the same reachable-target test. A rejoin that is lost again within 1.5 s doubles the wait, up to 8 s. It
    used to test only the line to the parent: a foal then looped, following and losing it several times a second on
    an obstacle before its slot (found 2026-09-30).
    Added 2026-09-30: before this, only a parent coming within about 2 units brought it back.
  - **Catch-up:** it goes to its slot once the parent has stood for 1.5 s and the foal is off screen, or once the
    parent has stood for 6 s. The slot needs a clear line from the parent, else any free slot, else next to the
    parent.
  - **Other scenes:** the foal is moved into the parent's scene. With its own scene unloaded, this runs from
    `StandTick`'s no-view branch.
- **Checking the way ahead while trotting:** every 0.2 s, as far as it moves in that time plus 0.4. At first it
  checked 0.6 every 0.25 s, and at a 4 u/s trot a foal ran into an obstacle between two checks (found 2026-09-30).
- **Stuck inside something:** once a second a standing donkey or foal checks whether its spot is blocked. If it is,
  it moves to the nearest free spot within 1.2, and the log line `[shuffle] ... stood inside ...` says so.
- **Spacing:** the gap is `1.1 × (scale A + scale B) / 2`. That is 1.1 between two donkeys, 0.88 between a foal and
  its parent, and 0.66 between two foals.
- **`SpawnFoal`'s own settings** for the foal, owned by the caller:
  - `IdleShuffle` on;
  - area 0.9 × 0.45 (1.6 × 0.7 at first: they wandered too far from their spot);
  - steps 0.2–0.6 at 0.45/s;
  - pauses 2.5–8 s;
  - turn chance 0.35.
- **The panel's family:** the parent's own `IdleShuffle` is switched on too.
- **Removal:** removing a parent removes its children (`RemoveWithParent`). Otherwise they become orphans.
- **Log lines:**
  - `[foal] ... follows its parent`, the first 5 per foal;
  - `[foal] ... can't follow ...`;
  - `[foal] ... caught up ...`.
- **Spawn spot:** `SpawnFoal` puts the foal in a free slot next to the parent, checked like a catch-up.
- **Wedged:** a foal with a parent that finds no step three times in a row counts as lost. The catch-up then moves
  it to a free slot.
- **Clearing a mod's tuning** (`Mounts.ClearTuning(owner)`) also clears the foal's own `IdleShuffle` that
  `SpawnFoal` set for that owner. The foal then stops shuffling and following.
- **Panel:** each row says "foal of #N, size" or "parent of #…". The Idle shuffle line shows "trotting after its
  parent" and "lost its parent, catches up out of sight".

## In-game test list

1. **Family:** Instances tab, "A donkey family" (2 foals, size 0.6). A donkey and two small donkeys appear.
   - Each has a number marker over it.
   - The list says "foal of #N" and "parent of #…".
2. **Looks:** screenshot the foals standing and walking at sizes 0.5, 0.6 and 0.7.
   - Check the pixel crunch, the shadow and the grass bending.
   - Check that a foal sorts behind or in front of the parent's legs correctly.
3. **Shuffling:** the foals take small quick steps around the parent, never straight up or down. The parent
   shuffles too.
4. **Walk cycle:** the foals' hooves match the ground at the slow step and at the trot. If not, change "Walk
   animation rate" with a foal picked (Edit its settings).
5. **Not rideable:** walking up to a foal shows no E prompt, and the panel's Ride and Hitch are greyed out.
6. **Following:**
   - Ride the parent a few units away slowly. The foals trot after it and settle behind it.
   - Ride fast or far. They fall behind, shuffle where they are, then catch up once you stop: when they are off
     screen, or after 6 s.
7. **Cart:** hitch a cart to the parent. The foals follow behind the cart.
8. **Door:** ride the parent through a door to another area and stop there. After 1.5 s the foals appear next to it.
9. **Remove:** removing the parent removes the foals.
10. **Add a foal** to any donkey, any number. The spots form rows further out, two per row.
11. **Another mod's global switch:** with the showcase's IdleShuffle off, the foals still shuffle (they have their own
    switch), and the showcase's donkey stands still.

## Open decisions for the user (answered, kept for the record)

1. **When the parent is ridden away:** A (wait), B (trot after, falling back to A), or C (hide)? Recommended: B.
2. **The API shape:** the general blocks (scale, rideable, parent), `Mounts.SpawnFoal` on top, and a `MountSpawn`
   spec. Or just a `SpawnFoal`? Recommended: the blocks with `SpawnFoal` on top.
3. **Foals and the keeper:** ignore him, or keep a small distance?
4. **The showcase:** should your donkey get foals (a key, or one foal by default)?

## The bridge by the house (2026-10-03)

The foals wouldn't cross the bridge just west of the house (the user's screenshot), and the log said why: `no step
fits where it stands: root sample 4 at (-21.30, 6.00, 19.52): ground 6.49 (raycast:broken_bridgeto_vineyard-collision)
against 6.00 before`. Routes round found "none".

**The cause:** `GroundHeight` takes the navmesh where there is one, and the ground raycast elsewhere. At the bridge's
ends the navmesh lies under the deck (6.0), and just past where it ends the raycast finds the deck itself (6.3 to
6.5). Neighbouring samples from the two sources looked like a 0.49 wall, over the 0.45 limit (`MaxRisePerSample`).
Riding had the same problem, fixed on 2026-10-01 by comparing two points from one source (`GroundHeightPair`).

**The fix:** `StepFits`. A step above the limit is measured again with both points from one source (the navmesh for
both, or the raycast for both, within a unit of the higher point); a real ledge stays one. It is used everywhere
neighbouring samples are compared:
- the step and follow lines (`LineWalkable`);
- the route search's cells and its straight pulls (`FoalRouteSearch`, a new delegate);
- the keeper's way to a seat (`GroundGap`).

**Then: inside the bridge** (the user's video `Graveyard Keeper 2 - 2026-10-03 11-29-55 PM.mp4`). They crossed, but
drawn inside the deck, only their heads showing. Their roots were logged at 5.996 on the bridge, i.e. the navmesh
lies under most of the deck, not only at its ends. The ridden donkey and the cart never showed this: their height
follows the keeper's, and he walks on the deck's collider.

`StandHeight` fixes it: where the navmesh gives the height, a ground-layer surface up to 0.6 higher wins, as the
keeper's own ground ray finds it (the walkable top of a dropped big item and water excepted). It is used wherever a
donkey or foal is put down:
- a shuffle step;
- a foal following;
- stepping out of an obstacle;
- `SnapToGround` (catch-up, spawn, placing).

The step checks still compare `GroundHeight`s through `StepFits`.

**Still to see:** foals cross the bridge both ways, on the deck and not in it, with no pop at its ends. Ordinary ground
should look as before (the deck rule only adds a surface above the navmesh).

**Audit of the same bug elsewhere** (2026-10-03, after the user confirmed the bridge). Every place that takes a ground
height:

| Where | Finding | Done |
|---|---|---|
| A standing cart's shafts rest on the ground (`MountInstance`, shaft drop) | Read `GroundHeight`: on a bridge the shafts would dip to the navmesh under the deck | `StandHeight` |
| A donkey moved off bad ground at a dismount (`KeepOnGround`) | The new spot took the navmesh height: put onto a deck, it would stand inside it | `StandHeight` |
| Calling the donkey, `MoveTo`, spawning, a foal catching up | `SnapToGround` | Already `StandHeight` |
| Hoofsteps of a standing donkey or foal | The game's surface probe from the root down: sunk under the deck it heard the ground below (stone, dirt) instead of wood | Fixed with the height |
| The ridden donkey, the ridden cart | Height relative to the keeper's, from one source (`GroundHeightPair`) | Nothing to do |
| Getting on (`WhyKeeperCannotStandAt`) | The seat is never put below the navmesh, and a deck is above it | Nothing to do |
| Step checks (lines, routes, the keeper's way to a seat) | `StepFits` | Done |
| Cargo set down | The game's own drop | Nothing to do |

Left as a limit: a surface more than 0.6 above the navmesh (`DeckRise`) is not taken. None is known; the bridge is 0.5.
