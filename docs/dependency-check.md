# The dependency check for the Donkey API

Why: `GK2QOLMods/docs/update-gate.md`. The QOL mods replaced their MVID pin with an embedded manifest of the game
members they use, compared at start (`GK2QOLMods/docs/dependency-check.md`). The question for this repo was whether
the API could do the same without ever risking an item, and what "degraded" could mean for it. Designed and built
2026-10-14; the investigation first, then what was built.

## What the investigation found

**A stopped API never endangers an item, by the existing save design.** The mod leaves nothing of its own in the
game's save (`docs/persistence.md`): cargo is lent to the save as ordinary ground drops at the cart and taken back
by GUID after a load; the chest on the cart is the game's own object, shown and zoned at the cart for the moment of
every save; zombies on the cart are store items handed back the same way; corpses are the game's instances dropped
by the game's drop system. A load with the mod stopped is exactly the tested "mod removed" case
(`docs/portable-chest.md` §"Mod removed with a chest riding in the save"): the drops lie at the cart, the chest
stands there as a normal chest, the side file is left unread. So "stop" is always available and always safe, and
"freeze a feature" means "do not take those items back at load, do not touch them in the session": they rest where
the save holds them. One consequence to tell players: small items lying on the ground for two game days go into the
crow nest (game 1.010); nothing is lost, it is just elsewhere.

**Where changes land, by the API's own classes.** The manifest tool knows which of the mod's types reference each
game member. Against the three updates so far:

| Update | Changed members the API uses | API types that use them |
|---|---|---|
| 1.007 → 1.008 | `DropSystem.DropItem`, `Wgo.UpdateChunkVisibility`, `GameSave.PrepareToSave`, `ZombieWgoData` ctor, `EasySpritesCollection.GetSprite`, `ZombieSystemData.CreateZombieDrop` | Mount, Cargo, Corpses, ChestMoves, the save patch; dev tools (SelfTest, CarryProbe, CargoSheet, TestLoads) |
| 1.008 → 1.009 hf | `Wgo.GetChunkableData` | ChestProbe (a dev tool) only |
| 1.009 hf → 1.010 | the three `PlayerData` overhead methods, `WorldZoneData.TryAddWgoData`, `WgoData` ctor, `WgoData.RefreshCustomNavMeshCutUnit`, `UIBaseChestWindowData` ctor, `GameSave.PrepareToSave`, `SaveSystem.Save` | Cargo, Mount, ChestMoves, the chest window patch, both save patches; dev tools (SelfTest, HeadProbe) |

Two of three updates touched the core (Mount, Cargo, the save hooks); one touched only a dev probe. Finer
granularity would not have changed that: the core changes were in `Mount` and `Cargo` themselves. Under the policy
below the three updates would have been: stop, run in full, stop. Today's pin: stop, stop, stop.

What stays true: a changed verdict is not a guarantee of anything, and an identical one is not either for this mod.
The 1.009 caretaker-station finding was in a class the API never referenced, and the 1.008 overload ambiguity
(`PlayerData.InsertOverheadItemTo` gained a second overload) was caught by `Access.Verify`'s lookup, not by a hash.
The audit after every update remains the real safety; the manifest gives it a reading list (`DepManifest verify`).

## The policy (built)

Four verdicts, as in the QOL mods, with the changed verdict decided by the mod's own classes that use each changed
member (`GameCode.cs`, the table `src/GK2DonkeyAPI/game-code.txt` embedded in the DLL):

- **Identical** (the game recompiled, every member the same): run, one info line, the build confirmed to the Mods
  menu. This is the verdict that keeps an abandoned mod alive, and it gets likelier as the game's updates slow down.
- **Missing** (a member gone or ambiguous) or **the check could not run**: stop, as before. Nothing overrides this:
  several classes hold `static readonly` field accessors that would throw at the first touch.
- **Changed, in the core** (`core` in the table: `Mount` with all its partial files, `Cargo*`, `HeadReroute` and
  its patches, the patches of `Patches.cs`, `InstanceControls`, `Plugin`, the Api): stop, with the members and the
  classes named. Cargo cannot run without the save hooks (a temp donkey's inventory never reaches a save: the
  hand-back before a save is what keeps the items), so none of these can be frozen alone.
  `Safety.ForceRunOnChangedGameCode=true` (default false) runs the mod anyway, at the player's own risk, with a loud
  error at every start and the chest on the cart off for the session: the escape hatch for an abandoned mod, the
  same as the old `DisableOnGameUpdate=false`. It never overrides a missing member.
- **Changed, only in a feature**: `frozen` features are off from the start (their `[HarmonyPatch]` classes are not
  applied at all; the feature's own switch does nothing; the warning names it): the corpses at the crossroads
  (`Corpses.Freeze`), the chest on the cart (`CartChest.Lock`; `ChestRig.Restore` then puts no chest back onto a cart
  after a load, it stands where the save holds it), the colours (`Colors.Frozen`: no part attached), the lantern
  (`Lantern.Frozen`: the pole stays as the prefab poses it, the lamp is not split). These three cosmetics borrow the
  game's pooled parts, which a throw on changed code could leave dirty for the game's own donkey. `warned` features
  run on the changed code and the warning says so: the sounds (`MountSounds` and its two animation patches), the look
  bar, the settings page in the game's own Settings window (`SettingsRows`); they touch nothing shared. The idle
  shuffle uses no game member of its own and cannot change. The rest runs.
- **Changed, only in dev tools** (`dev`: `SelfTest`, the probes, `CargoSheet`, `TestLoads`, `TuningWindow`,
  `ScriptDump`): the dev tools are locked for the session (`DevTools.DebugConfig` binds the `[Debug]` keys into the
  scratch config, so the panel, F5 and the keys are off whatever unlocked them), one info line, the mod runs and
  its build is confirmed to the Mods menu: everything a player uses is identical.
- `verify`: `Access` (the start-up verifier) is not a user of a member. A member only it names (an overload sibling
  of what the mod calls) changes nothing; the line lists it.
- `Safety.StopOnChangedGameCode=true` turns every changed verdict into a stop.
- The four `frozen` feature names are the contract between the table and `GameCode.Freezers`; a name the build has
  no switch for stops the mod (a build defect, logged as such).
- A class the table does not list counts as core (fail closed); `tests/CecilScan-DonkeyAPI` fails when a class of
  the built DLL that uses game members is listed nowhere, or the table names a class that uses none, so a new class
  gets sorted on purpose. A member used by classes of several kinds takes the most serious one.

The Mods menu (`FrameworkLink`): the build is confirmed only when `Mounts.StatusText` is "Active" (identical, or
dev tools only). Frozen, warned or forced, the Status row says what runs how ("Running with the colours and the chest
on the cart off: the game changed 3 member(s) this mod uses since this build. A re-audited build is pending.") and
the menu shows "Unknown build" for the API and every mod built on it. Saddle Up has no manifest of its own: it uses
no game member (`tools/check-dist.py` checks that), follows the API through `Mounts.IsAvailable`, and its Status row
is `Mounts.StatusText`.

## The chest's after-load repair

`CartChest.RepairHiddenChests` shows a chest left hidden in a save by a ride that never ended (a crash between a
pickup and a save). It goes through `ChestMoves.Show`, so with the chest feature frozen because `ChestMoves`'s
members changed, the repair would run on changed code. It keeps running: a hidden chest with items inside that
nobody can reach is the worse outcome, and the repair uses the most basic zone calls and logs loudly. The pre-save
pass (`GameSave_PrepareToSave_Patch`) is in the chest's class list and is not applied while frozen: nothing can be
lifted in a frozen session, so there is nothing for it to catch, and the repair covers a chest hidden earlier.

## Mechanics

The QOL repo's `src/Shared/DependencyManifest.cs`, `GameDependencies.cs`, `tools/DepManifest` and
`DependencyManifest.targets`, copied into this repo (two independent public repos; the copies are byte-identical and
say so in their headers; change both). What this repo added to them, used by both:

- **The used-by column.** Each `M`/`F`/`T` line ends with a tab and the mod's own top-level types that use the member
  (`DepEntry.UsedBy`, from the type the IL was found in, nested types counted for their outermost type). The feature
  mapping is data in the DLL, not a second scan at run time; `GameDependencies.Result.UsedBy` carries it for the
  changed and missing items, on the cached path too. The QOL mods ignore the column.
- **A strict reading of by-name lookups.** `AccessTools.X(typeof(T), "name", …)` is read only when the name is a
  literal reached from the call through nothing but the IL that builds the later arguments, and the type is a
  `typeof` right before it; a computed name (a parameter, a loop variable) or a computed type (`x.GetType()`) fails
  the build. Before this, a loop over types and names (`ZombieStations_TakeBack_Patch.TargetMethods`) passed silently
  with one target recorded instead of 24, and `Corpses.Verify`'s loops confused the walk-back. Those became one
  literal per lookup (all 24 station targets exist, so the list is exact). Lookups the code means to survive when the
  member is absent (a probe reading a run-time type's private fields, a diagnostic printing whichever field it is
  asked for) use `System.Type`'s own reflection, which the tool does not read, and say so in a comment.
- **Overloads.** A lookup that gives parameter types (`AccessTools.Method(typeof(T), "name", new[] { typeof(A) })`)
  lists only those overloads, not every method of the name. `AccessTools.Inner` lists the nested type.
- **`embed-text <mod.dll> <manifest.txt>`** in the tool: embeds a manifest text as given, for the in-game tests.

Scopes: the manifest reads `Assembly-CSharp` and `LazyBearTechnology`; `tests/CecilScan-DonkeyAPI` also resolves
`FlowCanvas` references (the corpse nodes' base types), which never changed and belong to a third-party package.

At start, `Plugin.BindAndPatch` binds the Safety keys and runs the comparison before the `[Debug]` keys bind (the dev
lock needs the verdict), applies the verdict after every key is bound (`GameCode.Apply`: the log line, the freezes,
the Status), and patches by class leaving out a frozen feature's patch classes (`GameCode.SkipsPatch`). The
`Access.Verify` lookups stay: the manifest cannot express `PlayerAnimation : AnimationComponentBase`, and a missing
member stops the mod there before any patch goes in.

cfg: `Safety.StopOnChangedGameCode` (false), `Safety.ForceRunOnChangedGameCode` (false), `Safety.LastDependencyCheck`
(written by the mod). The old `Safety.DisableOnGameUpdate` is gone (nothing had shipped; the cfg in the game was
cleaned by hand). The dev panel's General tab shows the two switches and the Status.

`tools/GK2DonkeyProbe` keeps a plain MVID pin on purpose: a dev tool never deployed by a plain build, whose job after
an update is to dump the new build ("the game changed, running anyway" is the wanted answer).

Offline: `tools/check-dist.py` runs `DepManifest verify` on the dist API DLL and checks the dist Saddle Up DLL
references neither game assembly; `tests/CecilScan-DonkeyAPI` verifies the bin DLL's manifest against the installed
game and the class table's coverage; after a game update `verify` lists exactly which members changed (the audit's
reading list), and `show` prints the manifest with its classes.

## Testing it

The fast path hides everything (the game is the build the mod was made against: nothing is compared while the
module ids match, which the first attempt on 2026-10-14 showed), so a deployed DLL gets a manifest with its
`game-mvid` line and the chosen hashes altered by hand (`embed-text`; the cfg cache is keyed by the manifest's SHA-1,
so the start compares with Cecil). Three starts, game closed between them, the save slot and its `.gk2donkeyapi.dat` side file copied aside
first:

1. **Core stop and the mod-removed load.** Normal build. A chest rides alone, so two carts (the dev panel's Instances
   tab spawns a second one, or two saves): cargo on one cart, a chest on the other, both parked, save, quit. A
   `Mount` member's hash altered: the start logs the core error naming the member and `Mount`, `Mounts.IsAvailable`
   is false, Saddle Up's keys do nothing, the Mods menu says "Unknown build" with the Status "Disabled: …". Load
   the save: the cargo lies at its cart as drops, the chest stands at its cart as a normal chest (it opens), nothing
   takes them back (the side file is left unread). Then `Safety.ForceRunOnChangedGameCode=true`: the start logs
   "RUNNING ANYWAY", the chest on the cart is locked (the chest stays standing, the info line "the chest on the
   cart is off"), the cargo is taken back onto its cart once (no duplicate). Real manifest back (`restore`), load
   again: the chest back onto its cart, everything as before.
2. **Features frozen.** A `Colors`-only, a `Corpses`-only and a `CartChest`-only member altered: one warning naming
   the three features and their members; the colours do nothing (the donkey, the cart and the preview picture in the
   game's own art: the preview goes through the same attach); the chest cannot be lifted ("Chests don't ride on the cart") and a chest standing at the
   cart from the save stays there with its info line; the corpses toggle does nothing; the Status row and "Unknown
   build" in the Mods menu; the rest rides.
3. **Dev tools only.** A `SelfTest`-only member altered, with the dev tools unlocked by the sources: one info line,
   no `Dev tools unlocked` line, the panel does not open, the mod runs, "Compatible" in the Mods menu.

Script: `tmp/api_test_embed.sh core|optional|dev|restore` (this machine).

Done 2026-10-14, all six starts, no warning or error beyond the gate's own line in any of them:

0. Real manifest: nothing logged by the gate, both mods Compatible.
1. Core (a `Mount`-only field altered): the error line naming the member and `Mount`, both mods UnknownBuild, the
   verdict cached, the side file untouched, H and G dead, the six items on the ground at the cart, the chest standing
   and openable. With `ForceRunOnChangedGameCode=true`: "RUNNING ANYWAY", the six items back on the cart once, the
   chest left standing with its info line, riding works. (The first attempt altered only hashes and ran on the fast
   path: the module ids matched, so nothing was compared; the test manifest alters `game-mvid` too since.)
2. Features (a `Colors`-, a `Corpses`- and a `CartChest`-only member altered): one warning naming the three, the chest
   left standing, the cargo back, UnknownBuild. Finding: the chest stands on the cart body's spot, so that cart cannot
   be hitched while the feature is off (`docs/open-items.md`, Undecided: place the restored cart behind the chest).
3. Dev tools (a `SelfTest`-only member altered): one info line, no "Dev tools unlocked" line, Compatible, chest and
   cargo restored.
4. `restore`: a plain start again, chest and cargo back, Compatible.
