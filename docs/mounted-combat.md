# Mounted combat: investigation (2026-10-14)

Asked for: attacking while mounted, with the sword and with the bow, in the game's fights (the town battles that came
with 1.010). Nothing is built; this is what the game does, where it collides with the mount, and a plan. Sources: the
1.010 decompile (`tmp/decomp_1010`), the probe's animator dump (`tools/GK2DonkeyProbe/report-round1-RuinedTemple.txt`),
and the mod's own code. The baseline until this is built: the ride ends when the keeper's armour goes on and cannot
start again while he wears it (`Mount.InArmour`, 2026-10-14), because the armour layers have no frames for the rider
poses (the keeper showed as a blue shape).

## 1. How a fight runs

1. The quest's talk fires `Flow_EnablePreFight` → `FightingGameController.StartPreFight(levelId)`: the fighting level
   loads and its border objects switch on (invisible walls around the arena), the environment pauses, allies spawn,
   the fight state becomes `InPreFight`, the keeper's held item is dropped, **his armour view goes on**
   (`PlayerController.SetArmorView(true)`: the armour skin preset and an armour animator layer), his sword or bow is
   equipped (`AttackComponent.EquipWeapon`), stamina is filled, and the pre-fight window opens (the chained fights,
   the tutorial once). `StartPreFight` itself does not teleport: the quest's script has its own "Teleport Player" node
   (`Flow_TeleportPlayer`) that puts the keeper at the fight's start point (`gd_fight_A1_1` in the user's case), a
   same-scene `PlayerController.Teleport` under `ByFlow` control, which the mod logs as "a door while riding" and
   rides through (corrected 2026-10-14 morning, §9.3).
2. "Fight" in the window → `Play()`: state `ActiveFight`, the fight HUD and timeline, the fight playlist, the level's
   stage 4, the player's death callback set, `PlayerPhysicalBody.IsActiveCombatant` and the player registered as a
   target for the enemies (`TargetsDatabase`).
3. Won or lost → `Stop()` → `SetPlayerDefault()`: weapon unequipped, **armour view off**, the keeper teleported to the
   level's fight-back point (`RT_fightback_player_spawn` by default), state `Disabled`.
4. Death: the death animation's callback opens `FightDeadWindow`, then a fade, `Stop()`, the `FightLost` trigger, the
   death-exit animator trigger, and control is taken `ByDeath` while it plays.

`FightingGameController` is a `LazySingleton`, whose `Instance` creates the controller when none exists: the mod does
not read it. The armour flag is the one true signal (`isArmorViewActive`, private); the three ways it goes on are the
pre-fight, a script's "Equip Armor" node (`Flow_SetPlayerArmorState`) and equipping body armour during a fight.

## 2. How attacks work

**Input.** `PlayerInputHandler.UpdateInput` (called only from `FreePlayerState.Update`) enters an attack state through
`Ssm.ForceEnterState<T>()` when controls are enabled and a weapon is equipped; the mouse handler
(`PlayerMouseCombatHandler`) does the same from the mouse: right click = focus (aim at the cursor,
`MouseAimHelper.TryApplyAim`), left click = attack. `SSM.CustomUpdate` returns to `FreePlayerState` when the attack
state's `IsActive` turns false.

**Melee** (`meleeMode`): `AttackSwordPlayerState` (WithStop: control taken `ByAttack`, a dash impulse on the rigidbody
for `attackDashDuration` along the input or facing, the "attack" trigger, chained swings on the key while stamina
allows), `AttackSwordContinuousPlayerState`, `AttackSwordDefaultPlayerState` (WithMove/WithFocus: moving while
swinging), and **`AttackSwordFocusedPlayerState`** (right click held).

**Ranged** (`rangedMode`): `AttackBowDefaultPlayerState` (single shot: `bow_shoot`, moving allowed),
`AttackBowAutoPlayerState`, and **`AttackBowFocusedPlayerState`** (right click held: `bow_preparing` / aim loop,
`bow_shoot` on the key).

**The focus stance** (both focused states, the user's screenshot with the arrows widget):
- the facing is **locked to the aim direction**: `PlayerData.SetDirectionLock(false)` and
  `PlayerPhysicalBody.SetDirectionLock(false)` make `MoveByDirection` keep the stored facing (`GetFacingDirection`
  returns the aim direction when one is set, else the locked `PlayerData.Direction`), so the keeper **walks in any
  direction while facing where he aims**; with the mouse, `SetAimDirection(cursor)` every frame;
- the `StanceWalk` animator layer (13) plays one of eight `armor_stance_walk_<facing>_<movement>` clips, chosen by the
  `MovementDirection` float (the raw input angle) and the facing; `walk_speed` is ±0.5 with the sign of the locked
  facing's x (not of the movement against the facing: corrected in §9.2), which plays the side clips backwards;
- **speed is halved**: `PhysicalBody.SpeedMultiplier = 0.5` on enter, `1` on exit (the user noticed the slowdown);
- the attack key swings or shoots in the focused direction; letting go of the focus key leaves the state.

**Hits.** The sword's damage is a trigger collider, `Sword Hit Box`, under `Weapon Container` (a child of the player
view at the physical body's level, not under the animator root; local y 0.032), switched by the `Sword Attack Hitbox`
layer (15) during the swing (`SwordHitBoxNotifierSMB` clears the hit accumulator on exit); `SwordHitBox.OnTriggerEnter`
hits `ICombatEntity` colliders of the other team through their `HitZone`. Enemies target `PlayerPhysicalBody`
(rigidbody and mesh collider), which the mount never moves: the rider's sprite root is what the seat shifts. Arrows:
`BowWeapon` spawns a projectile from the animation's events (`attack_bow_*`: 3 events, `armor_attack_bow_shoot_*`: 2);
where the spawn point sits in the hierarchy decides whether the seat shift raises the arrows (to measure, §5).

## 3. The frames

The player's animator has 19 layers. Clips by layer (the probe's dump, 391 clips): base `static_*` / `walk_*` per
facing; armour layers 7 (`armor_static_*`, `armor_walk_*`), 8 (`armor_static_sword_*`, `armor_walk_sword_*`), 9
(spear), 10 (bow), 18 (no helmet); sword attack 11 (`armor_attack_sword_<facing>` and `_x2`, each with a `_wait`;
0.38–0.41 s); bow attack 12 (`armor_attack_bow_prepare_*`, `_shoot_*`, `_single_shot_*`); stance walk 13 (eight
`armor_stance_walk_*`); death (`armor_death_*`). Every armour and attack clip exists in **all four facings**. There
is **no seated or fishing frame in armour**: the rider pose `FishingIdle` (left/right only, the base layer) has
nothing to show once an armour layer is on top, which is the blue shape.

## 4. Where this collides with the mount

| The mount today | The fight |
|---|---|
| The ride ends on any state but `FreePlayerState` and on control taken by anything but UI, teleport, self, flow (`WhyMustDismount`). | Every attack is its own SSM state; `AttackSwordPlayerState` takes control `ByAttack`. |
| Rider and donkey face left or right only; any facing with a horizontal part becomes that side, a vertical one keeps the last side (`RiderFacing`, the `SetDirection` prefix); the donkey turns with him. | Attacks swing and shoot in four facings; the focus stance locks the facing to the aim while moving any way, so the keeper can walk left while facing up, or back away while facing the enemy. |
| The donkey is drawn at the keeper each frame (`Follow`) and plays its walk when his `charState` is Walk; it has side views only. | Strafing: the keeper moves away from his facing; the donkey would walk forwards while sliding backwards unless its walk is reversed (`SetWalkAnimationSpeedMultiplier` with a negative value: to try). The dash moves the rigidbody by force; the donkey follows it. |
| The base-layer pose is `FishingIdle` (Ride) or Walk (Cart); `SetState` is rewritten to the pose; a state that is neither Walk nor Idle counts as a scripted pose and ends the ride. | Attacks use layer weights, triggers and bools, not `SetState`; the armour layers draw over the base pose, which must then be Walk or Idle (they have those frames) or nothing shows. |
| The mount sets `SpeedMultiplier` (1.75 Ride, 1.5 Cart) and re-applies it in `FreePlayerState`; in `Attack*` states it leaves the multiplier alone (`RestoreSpeed`). | The focused states set 0.5 on enter and 1 on exit: the mounted keeper would move at half walking speed in focus, and the exit's `1` is what the mount already corrects on the next free frame. |
| The seat lifts the animator root (`seatRoot`, about 25 px) and shifts the torso; `RiderLeg` draws a leg; `HeadReroute` handles the overhead item. | The hit box and the physical body are not under that root: unaffected. Arrow spawn points and the `Dust`/`Hit` particle objects may be (to measure). |
| Same-scene teleports are "a door while riding": the ride goes on. | The pre-fight teleports the keeper into the arena (borders on): riding in is fine; the donkey has no physics and ignores the walls. |
| Death takes control `ByDeath`: the ride ends. | Same; the death animation plays in armour (frames exist). Must end before the death clip shows, as today. |
| The donkey's colliders are on layer 26 with interaction off. | Enemy attacks and projectiles look for `ICombatEntity` / `HitZone`: the donkey has none, so it cannot be hit or targeted (to confirm with a spit projectile). |

## 5. A plan, in steps that can each be tried in game

**Step 0 (built, then replaced by step 1 the same day):** armour ends and refuses the ride.

**Step 1: ride in armour, plain attacks only.** Allow the armour view while mounted with the base pose forced to
Walk/Idle (what the Cart style does already; `FreezePoseWhenIdle` keeps him still when standing), so the armour
layers have frames. Allow the SSM states `AttackSword*` and `AttackBow*` and control taken `ByAttack` in
`WhyMustDismount`, but **not the two focused states** (refuse focus while mounted: nothing happens on right click, a
one-time bubble "Not while riding."; or end the ride when focus begins). Attacks then swing and shoot **sideways only**,
in the shared facing (the `SetDirection` prefix clamps the aim to left/right, so an enemy above or below is hit by
riding past it). Mounted speed in attack states: keep the mount's multiplier (`RestoreSpeed` no longer skips `Attack*`
except during the focused states, which do not occur). The dash: let it move both. Keep the Ride style only in a
fight at first (the cart in an arena is a later question: it is wide, and cargo and zombies ride on it).

To measure before building step 1: where arrows spawn relative to the seat shift (fire a bow mounted in a test
build and watch whether arrows pass over enemies); whether the `Hit`/`Dust` particles sit under the shifted root;
that the death dismount still happens before the death clip; that enemies do not target or hit the donkey; that the
HUD's stance widget and the fight overlay draw correctly over the mount's rows.

**Step 2: the focus stance mounted.** The facing is the aim's side (left/right; a vertical aim keeps the last side,
as `RiderFacing` does), the keeper strafes with the stance-walk clips (they exist for all facing × movement pairs),
and the donkey must move with him while facing the same side: forwards as now, **backwards with its walk reversed**
(`SetWalkAnimationSpeedMultiplier(-x)`, to try; else play the walk and accept the slide), and up or down the screen
with its walk playing (it has no up/down art; this is what it does today when the keeper walks vertically while
mounted). Speed: the mount's multiplier times the focus's 0.5. The arrows widget on the HUD already shows the
stance.

**Step 3 (optional): aim up and down.** Needs the keeper to face up or down on a side-view donkey: either accepted
as a look (the keeper turned on the saddle; the frames exist) with the donkey keeping its side, or not done.

## 6. Open questions for the user

- Should the cart be allowed in a fight at all? (2026-10-14: parked; for now it stays outside at the talk, no message.)
- In focus, mounted speed = mount × 0.5, or the mount's full speed (a cavalry charge)?
- Step 3: is a keeper facing up or down on a side-view donkey acceptable, or do attacks stay sideways?

## 7. What was checked and found harmless

Enemy targeting and damage go to the physical body, which the mount leaves alone; the sword hit box is not under
the seat-shifted root; the arena's walls block only the rigidbody; the other skin swaps on the player (customisation)
keep the base body, which has the fishing frames; the pre-fight teleport is a same-scene teleport the ride already
survives; `RestoreSpeed` already knows the attack states by name.

## 8. Built: step 1 (2026-10-14, untested)

- **The armour view rides.** `Mount.EffectivePose` is Walk while the keeper wears armour (every read of the pose goes
  through it: the `SetState` prefix, the idle freeze, the gear hiding, the no-sprite warning), so the armour layers
  have frames; the keeper sits with the standing frames, hips at the back line, legs behind the donkey's body as the
  seat places them. The Cart style is refused while armoured ("Not the cart in a fight."), at mounting, at hitching a
  cart from the saddle and in `Mounts.SwitchStyle`.
- **The attack states ride.** `Mount.IsAttackState` (every SSM state whose class name starts with `Attack`: the four
  sword states, the three bow states, the spear) no longer ends the ride, and control taken `ByAttack` is skipped
  like UI and teleports. The `SetTrigger` and `SetState` notes stay as they were: they only end the ride while a flow
  script holds control, and no script does during an attack.
- **The focus stance rides, as a side-facing strafe.** The facing clamp stays, so right click makes the keeper face
  the cursor's side and strafe with the stance-walk frames; the donkey walks forwards while he backs away (the slide
  is expected for now: the reversed donkey walk is step 2, after the strafe has been seen).
- **Speed.** The mount's multiplier in free walking, half of it in the two focus stances (the game halves walking
  speed there; the game's own 0.5 and the exit's 1 are overwritten the next frame), the other attack states' own.
- **The weapons stay at ground level.** `Weapon Container` (the sword's hit box, the bow's arrow origin) hangs under
  the animator root that the seat lifts; `ApplySeat` holds it at its rest place (minus the seat and the torso shift),
  `RestoreSeat` puts it back. One info line per armoured mount gives the hit box's world height next to the body's.
- **The idle freeze never runs during an attack** (the attack layers run on the same animator).
- Unchanged: death ends the ride (`ByDeath`), the pre-fight's same-scene teleport is ridden through, the attack
  direction is the drawn facing (sideways), the sword step-event skip (`MountSounds`).

**To check in game, in this order (each gives a log line):**
1. Mount in armour: start a fight's talk on foot, then H during the pre-fight: the ride starts (log "Mounted (Ride…)"),
   the keeper sits in armour with the standing frames (judge the look; the panel's Riding tab can move the seat).
2. Talk to the quest giver while mounted: no dismount as the armour goes on; the teleport to the fight point logs
   "a door while riding" and the ride continues; the info line "weapons held at their rest height … sword hit box
   y …" appears once.
3. One sword swing standing still, one while walking: the swing animates (no stall), the log shows no "Dismounted";
   the swing sound plays (if not: the step-event skip in `MountSounds` catches it, to fix).
4. Right click held: the keeper faces the cursor's side and strafes at half the mount's speed; the donkey slides when
   he backs away (expected). Let go: full mount speed again.
5. An arrow at an enemy: it connects (the arrow origin is at rest height). A sword hit on a zombie connects.
6. Being hit: damage and knockback move both. Death: "Dismounted (control taken ByDeath)" before the death clip.
7. The fight's end: won or cancelled, the keeper is teleported to the fight-back point **and the donkey comes along**
   (the ride goes on through the teleport, "a door while riding"); only a death leaves it standing in the arena, and
   then H calls it back (corrected 2026-10-14 morning, §9.3; the night's text expected it to stay).
8. G in armour: "Not the cart in a fight."; E on a free cart while riding in armour: the same.

## 9. Deeper investigation (2026-10-14, morning)

The night's cut (§8) was built after twenty minutes; this section is the investigation the user asked for, done the
morning after, question by question, from the 1.010 decompile and the probe's animator dump
(`tools/GK2DonkeyProbe/report-round1-RuinedTemple.txt`: layers at lines 436-454, clips 131-389, the weapon hierarchy
3398-3460). Blend trees and curves are not in the dump, so what layers 6, 13 and 15 do internally is inferred and
marked so.

### 9.1 Aiming: where every attack gets its direction

**Every player attack takes its direction from the drawn facing, and the mod's clamp already flattens it.**
`AttackComponent.PerformAttack` calls `ActivateWeapon(animationComponent.GetDirection().XZ())`; `GetDirection` reads
the animator's `Direction` float back, which `SetDirection` wrote after snapping to four directions
(`BasicNpcSteppedRotationPreset`). The mod's `SetDirection` prefix (`Patches.cs`, `Mount.RiderFacing`) replaces that
input with (±1, 0), so while mounted every attack vector is pure ±X. `PlayerPhysicalBody.SetAimDirection` still keeps
the unclamped cursor vector in `aimDirection` and `PlayerData.Direction`; only the call to the view is clamped. The
sword's dash impulse uses `PlayerData.Direction`, so the dash can go up or down while the swing stays sideways
(harmless, a short slide). `MouseAimHelper` gives (dx, dz·1.25) normalised.

- **Sword.** Every sword state calls `PerformAttackByTrigger(useCustomDirection: false, …, "attack")`; none reads
  the cursor directly. Left click in the default state and every frame of the focus state only change the facing.
  The hit geometry is a fan of three trigger wedges (`PrismRoot`, `(1)`, `(2)` at yaw 133°/90°/48°, each about
  0.72×0.94×1, 0.35 out) under `Weapon Container/Sword`; in the dumped rest pose it points to world −X and sweeps
  from down-left through left to up-left. The `Sword Hit Box` stick (0.05×0.05×0.75) has a null accumulator and is
  probably never switched on (inferred). Layer 15's only clips are `attck_sword_left` and `_reversed`. Layer 6
  "Weapon HitBox Container" has zero-length pose clips `weapon_hitbox_container_{left,down,right,up}`, weight 1
  during `PerformAttack` and 0 after: it rotates the whole container to the snapped facing (inferred), so the fan is
  the same size in every facing. **Clamped to left/right the fan covers about 17° to 164° from straight down on the
  facing side, reach about 0.8 u** (estimate from bounds). An enemy exactly above or below is missed unless its
  collider sticks out to the facing side; diagonals on the facing side are hit.
- **Bow.** The arrow is fired by an animation event (`PlayerAnimation.OnBowAimShot` → `PerformAttack(useCustomDirection:
  false)`), from `weapon.transform.position` (the `Bow` object, local y 0.15 under `Weapon Container`; no sphere
  emitter), in `GetDirection().XZ()`: four-directional, ±X while mounted. `Projectile.Launch` rotates the arrow from
  +X and moves it straight; speed and lifetime come from the `TestProjectileStat` asset (class defaults 40 u/s and 8 s,
  asset values not dumped). Hits are a box sweep from the last to the current position each `LateUpdate`
  (`ArrowProjectile`), layer 11 sticks the arrow, a `HitZone` goes through `ProcessHit`, damage needs a `WgoPart` of
  another team.
- **Spear.** Nothing enters `AttackSpearPlayerState` (no `ForceEnterState<AttackSpearPlayerState>`;
  `PlayerInputHandler` covers sword and bow only). The player spear is unused in 1.010; if it were, the direction is
  again `GetDirection()` and the damage the `test_spear_hit_box` cylinder under `Weapon Container/Pike`.
  `PikeHitBox.SweepForTargets` (capsule radius 0.4) is only called by zombies.
- **Focus stance, exactly.** Right click calls `TryApplyAim` then enters the Focused state (`PlayerMouseCombatHandler`);
  the aim clears when neither focus nor right click is active. On enter: `SetDirectionLock(false)` on `PlayerData`
  and the body, speed ×0.5, `attack_focus = true`. `GetFacingDirection` returns `aimDirection` while one is set, so
  `MoveByDirection` re-applies the aim as the facing every frame. `MovementDirection` (layer 13) is atan2(input.y,
  input.x) in degrees, the raw input angle, 0 without input; `walk_speed` takes the sign of the input's x, ±0.5.
  The eight clips are `left_left, left_down, down_left, down_down, right_right, right_down, up_left, up_up`. **Clamped
  to left/right only the `left_*` and `right_*` clips can play:** horizontal input gives `left_left`/`right_right`
  (reversed by the negative `walk_speed`), vertical input gives `left_down`/`right_down`. `up_*` and `down_*` are
  never chosen whatever the cursor does (inferred from the clip names).
- **Frames.** Every armour and attack layer has up and down clips (layers 7-10 `armor_static/walk/*_sword/spear/bow_*`,
  11 `armor_attack_sword_{up,down}` + `_x2`/`_wait`, 12 `armor_attack_bow_{prepare,shoot,single_shot}_{up,down}`,
  13 `up_*`/`down_*`, base `attack_bow_*`/`attack_spear_*`). They show the keeper from the front or back, which reads
  badly on a side-view donkey; the left/right clips are fine.
- **Precedent for aim ≠ drawn facing:** `ZombieBowAttackCommand.EmitArrow` fires `ActivateWeapon((target + 0.83 up −
  bow).normalized)`, a free angle, while the zombie's sprite stays on its four-way facing; `BowAttackEmulationComponent`
  does the same. No melee precedent: the sword fan always follows the container pose.

**The three options for step 3, with their mechanics:**

- **(a) Sideways only; up/down aim goes to a side.** Already the behaviour: `RiderFacing` picks the side of any
  |x| > 0.05 and keeps the last side for a vertical aim. Cost: enemies straight above or below cannot be hit without
  moving (the fan reaches about ±74° off the side; the arrow flies exactly sideways). Wanted anyway: **hysteresis in
  `RiderFacing`**, because a near-vertical cursor flips sides on tiny x changes and turns keeper and donkey together.
- **(b) Up/down facing frames while seated.** Let vertical facings through the clamp while attacking or focused and
  keep `riderFacingX` for the donkey; then everything is vanilla (container rotation, arrow direction, stance clips).
  Needs per-facing seat, torso-shift and `RiderLeg` work (they assume side frames), and front/back armour sprites over
  a side donkey are a look question. The user's call.
- **(c) Drawn facing stays sideways; only the damage turns toward the cursor.** Bow: low risk, direct game precedent,
  one patch point: a prefix on `AttackComponent.PerformAttack` (or `ActivateWeapon(Vector3)`) for the player's
  component while mounted with a `BowWeapon`, `customDirection` = the cursor aim as (x, 0, y/1.25) from
  `MouseAimHelper.TryGetAimDirection` or the private `PlayerPhysicalBody.aimDirection`, falling back to
  `PlayerData.Direction` for keyboard/gamepad; snap to four directions (vanilla-equal) or free angle. The arrow then
  leaves a sideways bow sprite going up, from the bow's position. Sword: in a `LateUpdate` after the animator (next to
  `ApplySeat`'s `weaponRoot` hold) set `Weapon Container.localRotation` to the rotation layer 6 gives for the up/down
  facing while layer 6's weight is above 0 and an aim is set, and pass the aim as `customDirection` so knockback
  agrees. The per-facing rotations must be measured first (log them unmounted); layer 6 rewrites the rotation every
  frame, and it also turns the `Bow` child.

**Recommendation:** keep (a) and add the hysteresis now; build the bow half of (c) as the next step after the strafe
has been seen (precedent, one patch point); the sword half of (c) only after the rotations are logged; (b) only if
the user wants the look. Not confirmed: layer 6/13/15 internals, the wedge reach and arc, `TestProjectileStat` values,
the arrow's sweep mask, that `Sword Hit Box` stays off.

### 9.2 The donkey's reversed walk (step 2): feasible, with a sound fix

**Verdict: a negative multiplier reverses the donkey's walk.** `AnimationComponentBase.SetWalkAnimationSpeedMultiplier`
is `animator.SetFloat(walk_speed, speed / 1.5f)`: no clamp, no `Abs`, `Animator.speed` untouched. The game writes a
negative `walk_speed` itself in the two focus states (`Mathf.Abs(0.5f) * Mathf.Sign(MovableDirection.x)`), into the
same `BaseCharacterController` the donkey's `CharacterNPC_Donkey` override controller is built on (an override
controller only swaps clips, so the states and their speed settings are the player's). One link is not in any dump
and needs one in-game look: that the base Walk state's speed multiplier is `walk_speed` (the game paces mob walks
through it in `Wgo`/`MobCommandGoTo`, and the mod's own speed tuning visibly changes the donkey's pace, so it is
near-certain). The build logs the clip time's direction once so the look confirms it.

**Correction to §2:** the game's `walk_speed` sign in focus follows the *locked facing's* x (`MovableDirection` =
`playerData.Direction`, frozen in focus), not movement against the facing. The mod cannot copy that rule; it needs
`sign(velocity.x · riderFacingX)`.

How the donkey's walk is driven today: at the end of `Mount.Follow` the keeper's `charState` is mapped to Walk/Idle
and, **only on a change** (`lastMirrored` is a pose edge latch, nothing to do with mirroring), `SetStateToAnimator` and
`SetWalkAnimationSpeedMultiplier(1.5 × keeper SpeedMultiplier)` are called. Side effect found: the focus halving of the
keeper's speed does not reach the donkey's pace in the middle of a walk (the latch does not fire). Movement is not
read anywhere in `Follow`; `PlayerData.Direction` is useless for it in focus (its setter does nothing while locked).
Usable sources: `rb.velocity` (the keeper's `Rigidbody`, already in hand in `Follow`) or `LazyInput.GetDirection()`
(what the game feeds `MoveByDirection`). The donkey has side art only; the game mirrors it with a −1 scale on Base.

**What a reversed clip breaks:**
- `MountSounds` hoof steps go **silent**: steps are timed from `normalizedTime` (`hk = floor(t·n + phase)`) and play only
  when `hk > lastHoofK`; in reverse `t` falls, the "clip restarted" branch (`hk < lastHoofK`) catches every frame and no
  hoof plays. The wheel counter has the same flaw, and the wheel pitch's `Clamp(walk_speed, 0.5, 2.5)` turns a negative
  into 0.5. The donkey's walk clips carry no events (0 events, 0.97 s, loop), so the game's step event cannot help.
  Fix: count steps on `hk != lastHoofK` (both directions) and take `Abs` for the pitch.
- `RiderMotion` does not break: it reads the walk frame from the donkey body **sprite name**, not animator time. In
  reverse the frames run 8→1, so the bob/head lag becomes a lead and the hand sway reverses phase: cosmetic, and
  arguably right for backing up.
- No game mob ever walks backwards (negative `walk_speed` appears only in the player's focus states; the only
  "Backward" hits are a RichAI navigation flag and a tween), so there is no other mechanism to reuse.

**Alternative, kept as fallback only:** flip the donkey's facing to the movement side each frame while the keeper keeps
`riderFacingX`. `Follow` and `RiderFacing` would have to stop forcing the shared facing in focus, `RiderLeg` hard-codes
`donkeyMirrored = facingRight` (the keeper's), and the look is a donkey facing away from the enemy with the keeper
turned against it.

**Where step 2 goes:** (1) end of `Mount.Follow`: keep the Walk/Idle latch for `SetStateToAnimator`; add a per-frame
check with its own latch (`lastWalkSign`, `lastWalkMag`) that re-applies the multiplier when the sign or the magnitude
changes: `back = (focus state) && rb.velocity.x · riderFacingX < −deadband`, multiplier `(back ? −1 : 1) × 1.5 × keeper
SpeedMultiplier`; the deadband stops flapping near zero and during vertical-only movement, and the magnitude check
fixes the stale-halving side effect. (2) `MountSounds`: direction-aware step and wheel counting, `Abs` for the pitch.
(3) Reset the new latch wherever `lastMirrored` is reset (Mount, MountInstance, Tiers, HeadWatch).

### 9.3 Enemies, physics, death and the fight's end

- **How enemies find and hit the keeper: the seat lift cannot put him out of reach.** Targets come from
  `AgentAI.GetClosestTarget` over the `TargetsDatabase`; the player's entry is `PlayerPhysicalBody`, whose
  `CombatEntityPosition` is `rb.position`, and distances are plain magnitudes from it. Chasing is A* `RichAI` on a
  recast graph re-scanned at every `StartPreFight`; ranged enemies line-cast on the recast graph. **Melee hits use no
  collider and no `HitZone`:** `ZombieMeleeAttackCommand` compares the XZ distance to `attackRange` and calls
  `ApplyDamage` directly. Spit projectiles box-sweep for any collider whose parent is an `ICombatEntity`; for the player
  that is `player_mesh_collider` (convex, 0.50×1.07×0.30, on the root `PlayerPhysicalBody`, layer 10), **not under the
  lifted `main_character`**. Enemy arrows pass `componentInParent.Wgo` and so never hit the player. Whether the player
  prefab has a `HitZone` at all is unconfirmed (nothing that hurts him needs one).
- **The donkey has no live physics, so zombies cannot get stuck on it and the graph scan cannot bake it in:**
  `PrepareParts` puts every collider on layer 26 and disables them, and switches off all navmesh cuts (already muted
  at spawn). The borders are plain `borderObjects` toggled with `SetActive`; they stop the keeper's rigidbody, and the
  donkey is placed at the rigidbody's transform each frame, so it stays inside with him (its sprite may overhang a
  wall by the depth/lateral offset, visual only). The donkey is not an enemy target (registration goes through the
  fighter id cache; that "npc_donkey" is absent from it is unconfirmed but it is no fighter).
- **Knockback: the keeper gets none, so nothing can pull rider and donkey apart.** `PlayerPhysicalBody` is
  `ICombatEntity` and `IPhysicallyMutable`, not `IKnockbackable`; knockback is applied only to `IKnockbackable`
  (`AttackComponent`, turrets), melee zombies apply none, and `OnOtherCombatTargetHitMe` on the player is empty. What
  moves him is zombies shoving his rigidbody and his own attack dash (`AddForce`); both act on the rigidbody the
  donkey follows. §8 check 6's "knockback" is therefore "being shoved".
- **Death, in order:** HP reaches 0 in `ApplyDamage`; `HandleHpChanged` takes control `ByDeath`, fires `PlayerDead`
  and calls `SetState(Death)` (the mod's `SetState` prefix leaves Death alone); the same frame's mod tick sees the
  taken control and dismounts, Park leaves the donkey where it stands, `RestorePose` leaves the Death state alone;
  the death clip ends → `OnPlayerDeathAnimationFinished` → `FightDeadWindow`, fade, `Stop()` (HP restored inside),
  `SetPlayerDefault` (weapon off, `SetArmorView(false)`, teleport to `FightbackGdPointId`, default
  `RT_fightback_player_spawn`, preset "outdoor", no fade), `ByDeath` given back there with `SetState(Idle)`, then the
  `FightLost` trigger. The donkey is a child of the scene transform with chunk registration off; the arena teardown
  removes only registered targets and temporary Wgos, so it survives, and H (`ComeToKeeper` → `ComeTo`) moves it from
  any distance or scene. **No path was found where the donkey is lost.** A minor unconfirmed point: one frame of the
  death clip may be drawn at the lifted seat height before `RestoreSeat`.
- **Fight end (win or cancel): the ride goes on and the donkey comes with the keeper.** Win: `FinishAsWon`, the win
  window, a fade, `Stop`, `SetPlayerDefault` (teleport to `FightbackAfterWinGdPointId`); cancel: `CancelPreFight` →
  `SetPlayerDefault`. `ByTeleport` and `ByUI` are skipped by `WhyMustDismount`, the preset is "outdoor" so the
  indoors dismount does not fire, `MorgueHandover.NoteTeleport` logs "a door while riding" and `Follow` draws the
  donkey at the new position next frame. A chained fight is two teleports in a row (`Stop`, teleport to the next
  level's point, `StartPreFight`); the ride survives both. The §8 check 7 text was wrong and is corrected above. If a
  fight-back point were in another scene, the mod's "scene changed" path re-summons the donkey there.
- **The pre-fight teleport is the quest script's own node** (`Flow_TeleportPlayer`), not `StartPreFight` (which loads
  the level, borders, allies, armour, weapon, stamina, window). Under `ByFlow` control a script's `SetTrigger` or
  `SetState` would end the ride; the user's fight script issued neither (he stayed mounted).
- **Input taken during the pre-fight:** the pre-fight window is a `UIBuildingWindow` (`ByUI`, skipped). **Placing a
  fight building (barricade) in the pre-fight goes through `BuildManager`, which takes `ByBuilding` and makes the
  rigidbody kinematic: the mod dismounts ("control taken ByBuilding").** Expected by the rules, but a mounted player
  will meet it at the command post. `PauseAgentsMovement` pauses enemies only; no cinematics in the fight code.
- **The cart in a fight:** `PrepareParts` disables the cart's colliders too, so a hitched cart cannot block, push or
  get stuck; arena size comes from the level prefab's zone colliders (unconfirmed). The risks are visual and logical
  only: a wide cart drawn through the invisible walls, cargo or a chest riding through a fight, parking or
  `KeepOnGround` with a cart in a cramped arena. **Keeping the refusal is reasonable**; it is the user's call (§6).
- Unconfirmed: the player `HitZone`, the border prefabs' layers, RVO on the donkey prefab, an indoor fight preset
  (sewer/dungeon) triggering the indoors dismount or a cross-scene fight-back.

### 9.4 Sounds, the stance walk's steps, the armour's parts, the idle freeze

- **No combat sound is swallowed by the mounted step skip.** `MountSounds` prefixes only
  `AnimationComponentBase.OnAnimationStepCompleted` (for the keeper's animation component while mounted) and redirects
  `PlaySound` only on the donkey's component. The combat handlers are clip events of their own: `OnSwordAttack`
  (stamina + `sword_attack`), `OnBowAimStart` (`bow_aim_start` then the loop), `OnBowAimShot` (`bow_aim_shot` **and**
  `AttackComponent.PerformAttack`, which spawns the arrow: a wrong skip there would have stopped arrows too),
  `OnSpearAttack` (`spear_attack`); none has a caller in code, so only the clips fire them (`armor_attack_sword_*` 1
  event, `armor_attack_bow_shoot_*`/`_single_shot_*` 2, `attack_spear_*` 1). Hit sounds are code
  (`AttackComponent.HandleHit`: `sword_hit`, `spear_hit_zombie`, `bow_hit_zombie`; `HitZone.PlayHitEffect` →
  `HitZoneReaction.PlayEffect`). The event function names themselves are not in any dump (the probe records counts
  only), so this is inferred from the callers. **The armour's clank (inferred):** `armor_walk*` clips carry 4 events
  against 2 on `walk_*`, most likely the additional-step-sound toggles; that sound plays only inside
  `OnAnimationStepCompleted`, so it is skipped while mounted, which is wanted (the donkey's hooves sound instead).
- **The stance walk has no steps of its own** (all eight `armor_stance_walk_*` clips have 0 events); a strafe's steps
  would come from the armour-walk layers on the same animator, which the skip already covers.
- **The armour view adds no renderers:** `SetArmorView(true)` swaps the skin preset (`9019_main_character_armor`, or
  armour body and arms with the keeper's own head, hair and beard without the helmet), recolours through the shared
  material, and switches one armour layer on (7/18, or 8/10 with a weapon out). The skin changer re-points sprites on
  the same renderers every `LateUpdate` by the id in the sprite name; `leg` and `tol/*` are never re-skinned. So the
  mod's cached `bdy` renderer, the arm and head transforms and `seatRoot`/`seatRest` are the same objects in both
  views: **nothing is stale whether the armour goes on before or after mounting.** `RiderLeg`'s slice cache is keyed by
  sprite, so armour frames get fresh slices.
- **Found and fixed (2026-10-14 morning):**
  1. **The weapon hold could be lost during attacks.** `ApplySeat` wrote `Weapon Container`'s position only when the
     seat or torso shift changed. Animator layer 6 ("Weapon HitBox Container") holds a zero-length pose clip per
     facing (`weapon_hitbox_container_{left,down,right,up}`) and runs at weight 1 during every attack; if those clips
     key the container's position, the animator put it back up with the seat in the middle of each swing or shot. Now
     `HoldWeapons` writes it every frame after the animator (one write when it differs), from `OnKeeperLateUpdate`.
  2. **The armoured arm drifted off its weapon.** `RiderMotion` swayed and bobbed `gfx/arm` with no armour or attack
     check, while the sword, bow, arrow and quiver art sits on `tol/*`, which it does not move. The arm's offset is
     now zero in armour and during attacks (the head keeps its lag).
  3. **The idle freeze's speed branch** set the animator to the pose's `PoseSpeed` even during an attack (reachable
     only un-armoured with a weapon out, e.g. a script turning the armour off mid-fight); during an attack it is 1.
- **Still open, settled by one log line in game:** which renderer carries the legs in the armour's Walk/Idle frames. The
  dumped standing frame shows a separate, enabled `gfx/leg` renderer (`9019_leg_static_down`), and `RiderLeg` slices the
  bottom rows of `bdy` and never touches `leg`. If the armour's legs live on `leg`, the near-leg overlay shows torso
  rows and the real legs show unsliced (the "legs behind the donkey's body" of §8). The armoured mount logs "armour
  frames this moment: body sprite …, leg sprite … (on/off)" once; the Cart style (Walk pose) is in the same position.
- **The idle freeze** uses `Animator.speed`, never `enabled = false`. In armour it never runs (the pose is Walk). The
  resume paths are the per-tick `FreezeRiderWhenIdle` (speed back when `IsAttackState`), `UnfreezeRider` at dismount
  and `RestorePose`. A trigger set while the crawl speed is above 0 fires at most one frame late, and attacks end only
  through animator callbacks (`AttackWaitSMB`/`AttackIdleSMB` → `OnAttackAnimFinished`), so the animator must never be
  fully stopped during an attack: it is not.

### 9.5 Built this morning (2026-10-14)

- **Step 2, the reversed walk:** `Mount.SetDonkeyWalkMultiplier` runs every frame from `Follow`, with its own latch
  (`lastWalkMultiplier`, reset wherever the pose latch is), and sets the donkey's walk multiplier to
  1.5 × the keeper's speed multiplier, **negative while a focus stance is held and the keeper's velocity opposes the
  shared facing** (deadband 0.3 u/s to reverse, 0.1 u/s to come back). This also fixes the stale pace: the focus halving
  now reaches the donkey mid-walk. The first reversal logs once whether the clip's time really runs backwards
  ("reversed walk confirmed" or "NOT confirmed … the Walk state's speed parameter is not walk_speed").
- **The hoof and wheel sounds under a reversed clip:** `MountSounds` counts a step on any change of the step index
  (`hk != lastHoofK`, both directions) instead of only forwards, with the clip-restart guard kept for jumps larger
  than one step, and the wheel pitch takes `Abs(walk_speed)`.
- The three fixes of §9.4, and hysteresis is **not** added to `RiderFacing` yet (§9.1 option (a) asks for it; it
  changes the feel of every ride, so it waits for the user's word after the strafe has been seen).
- **The weapon hold yields to the animator** (advisor's point): `HoldWeapons` remembers what it wrote; a different
  value next frame was the animator's (layer 6's pose clip for the facing) and becomes the new rest place to hold
  from, instead of pinning the container to the position captured at mounting in whatever facing that was. One info
  line the first time it happens during an attack gives the position and rotation the animator wrote, which settles
  whether layer 6 keys position or only rotation (replaces the weak check 10).
- **The reversed-walk confirmation** arms and reads inside one animator state (`shortNameHash` compared); a state
  change under the read re-arms instead of logging a wrong "NOT confirmed".

### 9.6 Seen in the first test (2026-10-14, 10:01) and fixed

The user's video and screenshots, mounted at the quest giver's talk with the cart hitched:

1. **Blue shape while idle in armour, the fishing rod in hand; the knight only during the swing.** Cause: the pose is
   only rewritten when the game next calls `SetState`, and nothing does when the armour goes on mid-ride, so the base
   layer stayed on the fishing pose's frame (no armour frame for it); the attack layers have their own frames, hence
   the knight during the swing. The rod: the armour's skin swap re-activates the gear layer the mount had hidden, and
   `HidePoseGear(EffectivePose = Walk)` hides nothing. **Fix:** `Mount.ArmourChanged`, every tick: when the armour
   view goes on or off during the ride, `ApplyPoseNow()` (the `SetState` prefix maps the real state to the effective
   pose) and the pose's gear is hidden again; one info line "the keeper's armour went on/off while riding: pose …
   re-applied".
2. **The cart came into the arena.** The refusal guarded mounting, hitching from the saddle and the style switch; a
   cart already hitched at the talk rode through the teleport, and on foot afterwards the companion's G brought the
   cart in and hitched it (the instance's `Hitch` asked `WhyNotCart` only while mounted; `WhyCannotRideHere` never
   did) while E on the standing cart refused. **Fix:** the armour going on while driving leaves the cart standing
   ("The cart stays here: a fight.", the ride goes on without it; if it cannot be left the ride ends with "the cart
   in a fight"). **Where it lands:** the log shows the quest script teleports the keeper to the fight's point and puts
   the armour on in the same breath (the door line, then the armour's first frame, two frames before the position
   changed), so the cart might already stand at the fight's point when it is left; when a door was passed within the
   last ten frames the left cart is moved back to the door's far side, the talk's spot (`Mount.CameThroughDoorJustNow` in FoalsIndoors.cs,
   `MoveTo`). The next test's "left standing (…, back at the door's far side …)" line confirms it. `Hitch` refuses in armour riding or on foot;
   `WhyCannotRideHere` refuses a donkey with its cart in armour; the companion's `BringCart` asks before the cart is
   even moved. **The policy itself (no cart in fights) is still the user's choice (§6)**; everything above is one
   function (`WhyNotCart`) and one branch (`ArmourChanged`) to flip.
3. Unhitch then no re-hitch inside the arena: the same inconsistency, the same fix.

**The checks of §8, after this:** 1 and 2 again (the armoured keeper should sit with the standing frames at once when the
armour goes on, and the rod must be gone); 8 becomes: talk to the quest giver while driving the cart: "The cart stays
here: a fight." and the ride goes on without it; G or E afterwards: "Not the cart in a fight." everywhere.

**Added to the checks of §8:**
9. Back away from the cursor in a focus stance: the donkey walks backwards (clip reversed), the hooves still sound,
   and the log says "reversed walk confirmed". Walk towards the cursor again: forwards at once.
10. During a sword swing the hit box height in the log stays at ground level (the one-time line is at the mount; a
    second look needs the dev panel or a hit on a zombie connecting during the swing).
11. Place a barricade from the command post while mounted in the pre-fight: the mod dismounts ("control taken
    ByBuilding"), by the rules (build mode always dismounts). Mount again with H afterwards.

### 9.7 Second test (10:20): the pose fix ran and the blue shape stayed; now measured, not guessed

The log: "the keeper's armour went on while riding: pose Walk re-applied", then the same "no sprite" warning (pose
Walk, facing right), then "armour frames this moment: body sprite none, leg sprite 9019_leg_static_down (on)". So the
state is not the cause: in armour the body renderer resolves to **no sprite** (the game's skin changer
`ApplyResolvedSprite` disables a renderer whose animation sprite it cannot map to the skin, which fits the blue
silhouette), and the leg layer sits on a *standing, facing down* frame while the keeper faces right, as if the armour
layer were still in its default state (`armor_static_*_down` in the unmounted dump, never transitioned). What differs
when mounted is not readable from the code with certainty (the clamped facing, State held at Walk, the lifted root,
the hidden gear layers, the moved part transforms), so **`ArmourDump.cs` (temporary)** writes the keeper's animator to
the log once on foot in armour and once (twice at most) mounted in armour, a second after the armour is on: every
parameter with its value, every layer's weight, state, time and clip, every sprite layer's active/enabled state and
sprite name. The two dumps side by side name the cause. To collect: start the fight's talk **on foot**, stand a second
(the on-foot dump), then H and stand a second (the mounted dump), then send the log.

**Result (10:27, both dumps in the log):** on foot in armour `State = 0`, base layer `static_down`, body sprite
`9019_bdy_static_down` (the armour skin). Mounted in armour `State = 203` (FishingIdle), base layer
`fishing_idle_right`, body and arm renderers **active but switched off** with `1001_bdy_fishing_idle_left_03`: the
skin changer finds no armour frame for a fishing sprite and disables the renderer (the blue silhouette is what is left
under it); the armour layer 8 is in the same FishingIdle state with no clip, so the leg layer keeps its default
`static_down` sprite and is inactive. **Cause:** the `SetState` prefix reads the static `Mount.RiderPoseState`
(MountRegistry.cs), which still returned the *configured* pose; only the instance's `RiderPoseState` had been routed
through `EffectivePose`. The re-apply of §9.6 therefore wrote 203 again. **Fix:** the static accessor returns the
ridden instance's `RiderPoseState`. **Third round (10:41):** mounted in armour `State = 0`, base layer `static_right`,
layer 8 `armor_static_sword_right`, body `9019_bdy_static_sword_left` drawn, rod inactive, no warning; the user: "that
worked". `ArmourDump.cs` removed again; its assertions live on in the fight self-tests (§9.8).

### 9.8 Self-tests and the first passes (2026-10-14, 10:45)

Five scenarios in the group "Fights from the saddle" (`Dev/SelfTest.Fight.cs`, the panel's Tests tab): the armour on
and off while riding (pose Idle/Walk, body drawn with an armour frame, no rod, weapons at rest height over the body,
the pose back when the armour goes off); mounting in armour with every cart path refused by the same words (a donkey
with its cart, a hitch on foot, the style switch, a hitch from the saddle) and the hitch working again once the armour
is off; the armour on while driving leaving the cart as a free cart by the keeper; a sword attack entered as the
input handler enters it (`Ssm.ForceEnterState`, the view's sword equipped, no item touched) that does not end the
ride and ends by itself; build mode (`SetControlTakenType(ByBuilding)`) ending the ride with its reason. They need
body armour on the tool belt (otherwise `SetArmorView` would move one there or log the game's warning) and skip with
a warning without it. Not automatable: the look, the sounds, hits, being hit, death, the fight's end, the strafe.

**The user's round, same morning:** checks 1-6 and 9 of the test list pass in game (the cart left at the talk, the
armoured rider's look, the cart refusals inside, the swings, the strafe with the reversed donkey walk, the hits, the
barricade dismount); death (7) and the fight's end (8) not yet reached.

**First run of the group (the user, 10:55):** 4 of 5 scenarios pass; "mounting in armour, every cart path refused"
found a real gap: a donkey standing with its cart was ridden in armour through the handle's `Ride` path
(`Instance.RideFromWorld` asked `WhyCannotMount` and the other mods' rules but not `WhyNotCart`), the fifth way to a
cart. Fixed the same minute; the second failure ("Already mounted.") was its consequence. The cart at the talk was
seen in game the same round: the cart stayed behind at the talk (the message bubble did not show; the log decides).

**Second in-game round (the user, 11:01), from the log:** the cart at the talk works as designed: "a door while riding
… 2 item(s) on the cart", then "left its cart as … free cart at (83.21, 3.60, 14.85)" and "the armour went on while
driving: the cart was left standing (…, where the keeper stood)": the armour's tick came before the teleport moved
him, so the cart stood at the talk's spot (the user: it did not come into the arena; the bubble did not show, a
cosmetic point). **The reversed walk is confirmed:** "[focus] … velocity x −3.92, facing +1, product −3.92, multiplier
−1.50" and "reversed walk confirmed, the donkey's walk clip runs backwards (clip time 2.500 → 2.475)"; the first
round simply had no three frames of backing away. **The fight's end** rode through: "a door while riding: to
RT_fightback_player_spawn … no cart", "the armour went off while riding: pose FishingIdle re-applied". The focus probe
is removed again. One warning, "the keeper body has no sprite this frame", fired in the self-test the frame the armour
went on: the skin swap leaves the body's old frame without an armour counterpart for the one LateUpdate before the
pose follows in the same frame's tick; the warning now needs two frames in a row without a sprite. Death (check 7)
still unseen.

**Death (the user, 11:07):** "Dismounted (control taken ByDeath); … donkey stays at (105.06, 3.60, 15.79) in
RuinedTemple" at the moment of death, as §9.3 describes; the same session re-ran the fight group with 48 checks
passed, 0 failed, and no warning or error line from the mod or Unity. Every check of §8 is now seen in game; what is
left is the missing "The cart stays here: a fight." bubble (cosmetic) and the §6 choices.

**The missing bubble (11:15):** "The cart stays here: a fight." was said in the frame of the quest script's teleport and
the armour's skin swap (the log order: the door line, the cart left, the armour line, all in one frame). No game code
hides bubbles at a fight's start (`Bubble.SetVisibility` and the hide-all calls have no callers outside the bubble
classes), and Unity's own log lines are not captured by BepInEx here, so whether `Bubble.Talk` ran cannot be read
from the log. The message is now held (`pendingSay`) until the keeper has control back, four seconds at the latest,
and dropped with the ride. If it still does not show, the next step is a probe of the bubble's state, not another
change.

**Decided (the user, 11:20):** no bubble for the cart left at the talk; the cart simply stays outside when the battle
starts, and whether it should come along is decided later. The held message is removed again; the log line stays.
