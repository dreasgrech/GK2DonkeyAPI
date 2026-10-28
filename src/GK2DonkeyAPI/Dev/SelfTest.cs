using LazyBearTechnology;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // In-game self-tests (docs/self-tests.md): scripted scenarios that drive the game over frames (spawn test
    // donkeys, move the keeper, ride, wait for the levels) and check the state that decides what is seen: the
    // animator's facing, which cart parts are on, whose materials the sprites wear, the counters. Every bug of the
    // levels and view reuse found by hand on 2026-10-01 is one of them, so a fix can't pass by luck.
    //
    // Run from the panel's Tests tab: all of them, one group, or one scenario; every run has the same start and
    // finish. Test donkeys belong to SelfTest.Owner, are never kept with the save and are removed after each
    // scenario; the keeper is put back where he stood; nothing is saved. Results go to the log as [test] PASS / FAIL /
    // WARN / INFO lines and a summary, and to the panel (each scenario's own tally while the run goes on).
    internal static partial class SelfTest
    {
        internal const string Owner = "dreas.gk2.donkeyapi.selftest";

        internal static bool Running { get; private set; }
        internal static string Status { get; private set; } = "Not run this session.";
        internal static readonly List<string> Lines = new List<string>();

        internal static int pass, fail, warn, skip;
        internal static bool abort;
        internal static int spawnSeq;

        // The run as a whole, for the panel's progress: the scenarios in it, how many are done, when it started, and
        // the tallies so far.
        internal static int PassCount => pass;
        internal static int FailCount => fail;
        internal static int WarnCount => warn;
        internal static int RunCount { get; private set; }
        internal static int RunDone { get; private set; }
        internal static float RunStarted { get; private set; }
        internal static Scenario Current => running;

        internal static bool startRequested;
        internal static Func<Scenario, bool> pick; // which scenarios the requested run takes; null = all

        // The panel's buttons: the run starts on the next frame (PollStart), not inside the GUI event. All of them,
        // one group, or one scenario; every run has the same start and finish (the test spot, cleanup, the world's
        // own donkeys checked).
        internal static void RequestStart() => RequestStart((Func<Scenario, bool>)null);

        internal static void RequestStart(Scenario one) => RequestStart(s => s == one);

        internal static void RequestStartGroup(string group) => RequestStart(s => s.Group == group);

        internal static void RequestStart(Func<Scenario, bool> which)
        {
            if (Running)
            {
                return;
            }
            pick = which;
            startRequested = true;
        }

        internal static void PollStart()
        {
            if (!startRequested)
            {
                return;
            }
            startRequested = false;
            if (Running || Plugin.Instance == null)
            {
                return;
            }
            Running = true; // at once, so the panel shows Abort from the next event on
            Plugin.Instance.StartCoroutine(Drive(Run()));
        }

        internal static void Abort() => abort = true;

        // ------------------------------------------------------------------ the runner

        internal enum RunState { NotRun, Running, Done }

        internal sealed class Scenario
        {
            internal string Name;
            internal string Group;
            internal Func<IEnumerator> Body;
            internal bool HidesPanel;   // the panel hidden while it runs (its own cost would be in the measurement)
            // Its last run: the state and the tally; kept until it runs again, so a run of one group or one scenario
            // leaves the others' results standing.
            internal RunState State;
            internal int Pass, Fail, Warn;
            internal bool Skipped;      // not run in this setup (Skip): neither a pass nor a warning
            internal float Started, Elapsed;
        }

        internal const string GroupCore = "API, side file, carts, colours";
        internal const string GroupLevels = "Levels and views";
        internal const string GroupChest = "Chest on the cart";
        internal const string GroupCargo = "Cargo in layers";
        internal const string GroupMorgue = "Morgue";
        internal const string GroupCorpses = "Corpses at the crossroads";
        internal const string GroupLooks = "Skins and effects";
        internal const string GroupIndoors = "Indoors";
        internal const string GroupFlying = "Flying";
        internal const string GroupSafety = "Never lost";
        // The checks of the whole run, last in every run (a group's or one scenario's too): the world's own donkeys came
        // through as they were, no test donkey left behind. A scenario of its own so its tally has a header like every
        // other check (the user, 2026-10-26: a failure there showed under all-green headers).
        internal const string GroupAfter = "After the run";
        internal const string AfterRunName = "the world's own donkeys came through unchanged, no test donkey left behind";

        // Every scenario, in run order (docs/self-tests.md). The panel's Tests tab lists them from here. The API's own;
        // a dev test plugin adds its scenarios at its start (AddScenarios).
        internal static readonly List<Scenario> Scenarios = new List<Scenario>
        {
            new Scenario { Group = GroupCore, Name = "the per-mod API (ids, keys, data, owner rules)", Body = ApiIdentity },
            new Scenario { Group = GroupCore, Name = "kept-with-save text, round trip", Body = RoundTrip },
            new Scenario { Group = GroupCore, Name = "kept with save: a gone mod's lines set aside and written back, an ability's dropped", Body = AsideLines },
            new Scenario { Group = GroupCore, Name = "hitch and unhitch", Body = HitchUnhitch },
            new Scenario { Group = GroupCore, Name = "unhitch without a view (a parked cart stays)", Body = UnhitchWithoutView },
            new Scenario { Group = GroupCore, Name = "shared colour textures", Body = SharedColours },
            new Scenario { Group = GroupCore, Name = "settings: a moved section keeps a player's values", Body = MovedSections },
            new Scenario { Group = GroupCore, Name = "upgrade: a released version's player files are all read", Body = UpgradeFiles },
            new Scenario { Group = GroupLevels, Name = "one new view per frame", Body = SpawnCap },
            new Scenario { Group = GroupLevels, Name = "waking (view switched off)", Body = () => Wake(switchOff: true) },
            new Scenario { Group = GroupLevels, Name = "waking (view frozen)", Body = () => Wake(switchOff: false) },
            new Scenario { Group = GroupLevels, Name = "mounted while idle (brought to the keeper and ridden in one frame)", Body = MountWhileIdle },
            new Scenario { Group = GroupLevels, Name = "reused views", Body = Reuse },
            new Scenario { Group = GroupLevels, Name = "foals keep up off screen", Body = FoalsFollow },
            new Scenario { Group = GroupLevels, Name = "foals: follow the keeper on foot outdoors, with the other babies settings", Body = FoalsOnFoot },
            new Scenario { Group = GroupLevels, Name = "foals: each baby its own size, shade and temperament, the same every load", Body = FoalVarietyChecks },
            new Scenario { Group = GroupLevels, Name = "no leaks over spawn and remove", Body = Leaks },
            new Scenario { Group = GroupLevels, Name = "frame time by mode", Body = FrameTimes, HidesPanel = true },
            new Scenario { Group = GroupChest, Name = "chest: hide, move, show (and off screen)", Body = ChestHideMoveShow },
            new Scenario { Group = GroupChest, Name = "chest: zone out and in", Body = ChestZoneRoundTrip },
            new Scenario { Group = GroupChest, Name = "chest: ten stand-ins", Body = ChestStandIns },
            new Scenario { Group = GroupChest, Name = "chest: the ride (step 1)", Body = ChestRide },
            new Scenario { Group = GroupChest, Name = "chest: the backstops", Body = ChestBackstops },
            new Scenario { Group = GroupChest, Name = "chest: the save as the game writes it", Body = ChestSaveImage },
            new Scenario { Group = GroupChest, Name = "chest: a zombie using it", Body = ChestZombieGuard },
            new Scenario { Group = GroupCargo, Name = "cargo: more than two items (layers, a re-made cart, the setting)", Body = CargoLayers },
            new Scenario { Group = GroupCargo, Name = "cargo: a cart with a hitch gap keeps its place as items go on", Body = CargoKeepsTheGap },
            new Scenario { Group = GroupIndoors, Name = "indoors: the two switches (refused, ended, allowed) under the indoor light", Body = IndoorsRules },
            new Scenario { Group = GroupIndoors, Name = "indoors: through a building's door riding and driving, and back out", Body = IndoorsThroughADoor },
            new Scenario { Group = GroupIndoors, Name = "indoors: the cart left at the morgue door, the corpse into the hands, the mount inside", Body = IndoorsMorgue },
            new Scenario { Group = GroupMorgue, Name = "morgue: the door rule", Body = MorgueDecisions },
            new Scenario { Group = GroupMorgue, Name = "morgue: a corpse off the parked cart into the hands", Body = MorgueHandOver },
            new Scenario { Group = GroupCorpses, Name = "corpses: drop and stand points", Body = CorpsePoints },
            new Scenario { Group = GroupCorpses, Name = "corpses: one body left", Body = CorpseOneBody },
            new Scenario { Group = GroupCorpses, Name = "corpses: the patches follow the switch", Body = CorpseSwitch },
            new Scenario { Group = GroupCorpses, Name = "corpses: the notification, swapped once", Body = CorpseNotification },
            new Scenario { Group = GroupCorpses, Name = "corpses: twelve bodies at once", Body = CorpseManyBodies },
            new Scenario { Group = GroupCorpses, Name = "corpses: never rot", Body = CorpseNeverRot },
            new Scenario { Group = GroupCorpses, Name = "corpses: an unknown point steps aside", Body = CorpseUnknownPoint },
            new Scenario { Group = GroupCorpses, Name = "corpses: walk decisions", Body = CorpseWalkDecisions },
            new Scenario { Group = GroupFight, Name = "fight: the armour on and off while riding", Body = FightArmourWhileRiding },
            new Scenario { Group = GroupFight, Name = "fight: mounting in armour, every cart path refused", Body = FightMountInArmour },
            new Scenario { Group = GroupFight, Name = "fight: the armour on while driving leaves the cart", Body = FightArmourWhileDriving },
            new Scenario { Group = GroupFight, Name = "fight: a sword attack from the saddle", Body = FightAttackRides },
            new Scenario { Group = GroupFight, Name = "fight: build mode dismounts", Body = FightBuildModeDismounts },
            new Scenario { Group = GroupLooks, Name = "skin pack: a folder read, validated, worn, withdrawn", Body = SkinPack },
            new Scenario { Group = GroupLooks, Name = "recipe: built from the donkey's frames, worn whole", Body = SkinRecipe },
            new Scenario { Group = GroupLooks, Name = "mount from a pack: declared, worn, no cart, its tuning, withdrawn", Body = MountFromPack },
            new Scenario { Group = GroupLooks, Name = "mount from code: the ability's hooks on spawn, ride, dismount, removal", Body = MountFromCode },
            new Scenario { Group = GroupLooks, Name = "tuning: a value belongs to the owner that set it; what a mount and an ability may pin", Body = TuningOwnership },
            new Scenario { Group = GroupLooks, Name = "ids: one mount.cfg per mount (a newer one of the same id takes over, an older one is refused); a mount folder without mount.cfg joins; the same skin twice, the newer wins", Body = IdRules },
            new Scenario { Group = GroupLooks, Name = "ids: a code mount meets the version rule (another mod's older or equal one left out, its handle doing nothing; a newer one, code or pack, replaces it; an older pack refused with its skins); its clips banked under its mod", Body = IdRulesCode },
            new Scenario { Group = GroupLooks, Name = "default looks: one per mount (its skins\\default\\; a mount pack without one refused, a joiner's left out); one skin name on two mounts; a mount id without its mod's prefix refused; default_skin ignored; a skin added to another's mount without its mod's prefix refused (folder, code skin, donkey recipe)", Body = DefaultSkinRules },
            new Scenario { Group = GroupLooks, Name = "layering: a variant built on its default (pictures, settings, tuning, the default's sounds and colours; its file keeps its own lines)", Body = SkinVariant },
            new Scenario { Group = GroupLooks, Name = "layering: a variant drawn over its default (the erase colour's holes, its pixels on top, the colours by where they came from)", Body = SkinOverDefault },
            new Scenario { Group = GroupLooks, Name = "layering: a variant before its default, the default read again and withdrawn, another canvas, a code skin's sentinels, the donkey's skins unchanged", Body = SkinLayerLifecycle },
            new Scenario { Group = GroupLooks, Name = "format: description, portrait, stats, ability names, needs, a bad format, unknown [Game] keys", Body = PackFormat },
            new Scenario { Group = GroupLooks, Name = "hostile files: a picture refused from its header, a bad WAV chunk, NaN and Infinity, names outside the pack, a BOM", Body = HostileFiles },
            new Scenario { Group = GroupLooks, Name = "between mods: no throw (an effect and a code skin meet the version rule, a lost mount's own look left out, an effect id needs its prefix, a row's throwing function)", Body = NoThrowBetweenMods },
            new Scenario { Group = GroupLooks, Name = "between mods: order and identity (v2 beside v1 whole, an id not its folder's refused, a skin before its mount, a skin of a mount not installed, handles, one change per folder)", Body = OrderAndIdentity },
            new Scenario { Group = GroupLooks, Name = "between mods: versions and ids (needs in mount.cfg, ASCII ids, versions that aren't numbers, sound ids, one mod's sound for an event)", Body = VersionsAndIds },
            new Scenario { Group = GroupLooks, Name = "speed: each mount's own, ridden and with the cart; the donkey's from the cfg; the panel's words", Body = MountSpeeds },
            new Scenario { Group = GroupLooks, Name = "freeplay: max cargo and one speed for every mount (the words' one table, the cart's slots, ridden, driven, changed while riding, the panel, MountInfo)", Body = Freeplay },
            new Scenario { Group = GroupLooks, Name = "settings: the Mount, Skin and babies rows", Body = SettingsRows },
            new Scenario { Group = GroupLooks, Name = "looks per skin: a row set per skin, the 0.12 values moved to the skin worn, each skin's look when worn, a late skin caught up, regions per skin", Body = LooksPerSkin },
            new Scenario { Group = GroupLooks, Name = "settings: the Mounts panel opens, fits, follows the list, the colours window, the key", Body = PanelSmoke },
            new Scenario { Group = GroupLooks, Name = "settings: the Mounts panel's way back (from the game; over the pause menu through the colours window)", Body = PanelWayBack },
            new Scenario { Group = GroupLooks, Name = "settings: the Mounts panel's Ride and Drive buttons while riding keep the ride", Body = PanelRideButtons },
            new Scenario { Group = GroupLooks, Name = "skins and mounts: the changed events", Body = ChangedEvents },
            new Scenario { Group = GroupLooks, Name = "skin: a game-source skin with no frames is set aside, the donkey comes, offered again when they are there", Body = AsideSkin },
            new Scenario { Group = GroupLooks, Name = "colours: a skin over the donkey keeps the donkey's regions", Body = SkinOverColours },
            new Scenario { Group = GroupLooks, Name = "over skin: the erase colour clears the donkey under it", Body = SkinOverErase },
            new Scenario { Group = GroupLooks, Name = "skin pack: read again by itself when its files change (dev switch)", Body = SkinPackAutoReload },
            new Scenario { Group = GroupFlying, Name = "landing: the walk-away test (the keeper's own ground, a walled pen, the cap's time)", Body = FlightWalkAway },
            new Scenario { Group = GroupFlying, Name = "mounts: the flies and jumps flags ([Mount] flies, jumps; a pack's mount flies by its flag)", Body = FlightFlags },
            new Scenario { Group = GroupFlying, Name = "keys: jump and fly on one key (a second press in the air takes off a flier), and on two", Body = FlightOneKey },
            new Scenario { Group = GroupFlying, Name = "jump: each mount its own ([Jump] in mount.cfg, MountDef.Jump): read, saved, refused in a skin.cfg, jumped live", Body = FlightJumpPerMount },
            new Scenario { Group = GroupFlying, Name = "jump: beside a real thing (the yard's workbenches) and on ground the NPCs' mesh doesn't cover (a bridge): a jump in place lands in place", Body = FlightJumpBeside },
            new Scenario { Group = GroupFlying, Name = "jump: the mount's arc (held and tapped, the same at any frame rate), and jumped live riding plain: in place, over a low wall, stopped by a tall one", Body = FlightJump },
            new Scenario { Group = GroupFlying, Name = "jump: the home yard (every built thing and fence jumped at, a slide along the fence): down, never sent back, never underground", Body = FlightJumpYard },
            new Scenario { Group = GroupFlying, Name = "jump: a real hillside (terrain levels) run up and down: the body moves in the air, down on the ground, never held, never sent back", Body = FlightJumpSlope },
            new Scenario { Group = GroupFlying, Name = "in the air: the game's E and F prompts off (a jump leaves them), the work state refused, a ride ended up there parks the mount on the ground", Body = FlightInTheAir },
            new Scenario { Group = GroupSafety, Name = "safe spots: standing records one, under the ground never; a seed after the load", Body = SafeRecord },
            new Scenario { Group = GroupSafety, Name = "the fall watchdog: under the ground, far below the world, caught; a drop onto the ground and standing under a bridge, left alone", Body = SafeWatchdog },
            new Scenario { Group = GroupSafety, Name = "back to the last safe spot: the API call (on foot, riding), refused with the reason (controls taken, no spot), the watchdog off", Body = SafeReturn },
            new Scenario { Group = GroupSafety, Name = "side file: held after an error (a save leaves it, never deletes it), the last one kept as .bak, a stale one set aside", Body = SideFileHeldAndAside },
            new Scenario { Group = GroupSafety, Name = "side file: a record that fails to come back is carried forward; a newer version's keys and lines written back", Body = SideFileCarriedForward },
            new Scenario { Group = GroupSafety, Name = "handles: another mod's Remove refused; a mod's data capped", Body = HandleOwnership },
            new Scenario { Group = GroupSounds, Name = "the probe: own clips as game sounds (a tone from WAV bytes and from a file, registered in AudioConfig, played by the game's player with its 3D preset and mixer group, taken out again)", Body = SoundsProbe },
            new Scenario { Group = GroupSounds, Name = "the donkey unchanged: every event of the rig resolves to the [Ride] and [Cart] keys, the API's voice and bell play the game's sounds, a standing one and a foal sound under the standing switch, the lips event taken only when played, a standing wearer silent by its mount's pins", Body = SoundsDonkeyUnchanged },
            new Scenario { Group = GroupSounds, Name = "a pack's own sounds: [Sounds] lines and the sounds folder loaded as game sounds, the convention name, the layers (skin over mount over the keys, a style block), the player's switch on top, a second skin of the mount, the save round trip, the clean-up", Body = SoundsPack },
            new Scenario { Group = GroupSounds, Name = "the API: Mounts.Sounds (the catalogue, Resolved with its layer, a mod's Set over everything and back, its own clips kept, Play and Stop, the Played event), a code mount's MountSound shared by a pack skin of it", Body = SoundsApi },
            new Scenario { Group = GroupSounds, Name = "an embedded pack's sounds (the built-in skins' route): the API's own test pack, its .ogg decoded through the cache file, its .wav parsed in place, a looping event stopped", Body = SoundsEmbedded },
            new Scenario { Group = GroupSounds, Name = "the flying events: takeoff, the launch's flaps and the beats airborne, the fly loop, land, touchdown, landed, a jump's jump and jumpland; nothing of the flight on a jump", Body = SoundsFlying },
            new Scenario { Group = GroupSounds, Name = "herds and babies: a standing grown one calls on its own (rarer, one voice at a time in the scene), its babies answer one after another with [Sounds.Baby]'s sound, no call with the standing switch off", Body = SoundsHerd },
            new Scenario { Group = GroupAfter, Name = AfterRunName, Body = AfterTheRun },
        };

        // The groups in the order they first appear in the catalog; made again when scenarios are added.
        internal static string[] groups;
        internal static string[] Groups => groups ?? (groups = DistinctGroups());

        // Scenarios from a dev test plugin (the integration tests of the mods built on the API, which the API knows
        // nothing of): each after the last of its group, a new group before the run's own checks (After the run, last).
        internal static void AddScenarios(IEnumerable<Scenario> more)
        {
            foreach (Scenario s in more)
            {
                if (s == null || Scenarios.Contains(s)) continue;
                int at = Scenarios.FindLastIndex(x => x.Group == s.Group);
                if (at < 0) at = Scenarios.FindIndex(x => x.Group == GroupAfter) - 1;
                if (at < -1) at = Scenarios.Count - 1;
                Scenarios.Insert(at + 1, s);
            }
            groups = null;
        }

        internal static string[] DistinctGroups()
        {
            var g = new List<string>();
            foreach (Scenario s in Scenarios)
            {
                if (!g.Contains(s.Group)) g.Add(s.Group);
            }
            return g.ToArray();
        }

        internal static Scenario running;

        internal static IEnumerator Run()
        {
            abort = false;
            Lines.Clear();
            pass = fail = warn = skip = 0;
            if (!Mount.Instance.IsInGame(out PlayerController pc, out GameScene scene) || !Mounts.IsGameReady)
            {
                Status = "Not in a game (load a save first).";
                yield break;
            }
            if (Mounts.IsMounted)
            {
                Mounts.Dismount("self-test");
                yield return null;
            }
            // The panel stays up during the run (its Tests tab shows each scenario's progress); only the frame-time
            // scenario hides it, its own cost would be in the measurement.
            autoReloadAtStart = Plugin.SkinPackAutoReload != null && Plugin.SkinPackAutoReload.Value;
            bool lodOn = Lod.On, idleOff = Lod.IdleSwitchesOff;
            int perFrame = Lod.SpawnsPerFrame;
            // A test load left on the cart from the panel (TestLoads.Put, e.g. the review's Load it) holds every cart at
            // its size until Take off; the scenarios check the setting alone, so the floor is off for the run and back
            // after it. Found 2026-10-16: "two off: the size follows the setting (2 on it, 3 slots)" after a review session.
            int slotFloorWas = Cargo.SlotFloor;
            Cargo.SlotFloor = 0;
            // The run's own starting state, wherever it was started; each part put back at the end.
            // Free: a conversation, a script or work holds him (it can't be broken off from here): waited for.
            yield return Until(() => pc.IsControlsEnabled && pc.Ssm?.CurState is FreePlayerState && (pc.MovementComponent == null || !pc.MovementComponent.IsMoving), 15f);
            if (!pc.IsControlsEnabled || !(pc.Ssm?.CurState is FreePlayerState))
            {
                Cargo.SlotFloor = slotFloorWas;
                Status = "Not started: the game holds the keeper (a conversation, a script, work) and didn't let go within 15 s. Start the run when he walks freely.";
                Line("[test] " + Status);
                yield break;
            }
            // A player's Freeplay (every cart the most cargo, every mount one speed) would change what the scenarios check:
            // held off for the run, back after it (the Freeplay scenario sets its own).
            BehaviourHeld[] freeplayWas = HoldFreeplayOff();
            var standingWas = new BehaviourHeld("StandingSounds"); // the sound scenarios set it as the test's own (SetStandingSounds); the holder's back after the run
            // Out of the armour view (a fight's look: the cart is refused in it); the fight scenarios put it on themselves.
            bool armourWas = Mount.Instance.InArmour(pc);
            if (armourWas)
            {
                pc.SetArmorView(false);
                yield return Frames(2);
            }
            // His light (the game writes its name into the save): the run's is outdoor, his comes back with him.
            string originPreset = EnvironmentEngine.Instance?.TimesOfDay?.name;
            // Where the keeper stood: he is put back there at the end. The run itself starts from the remembered spot
            // (Debug.SelfTestSpot, written by a run that passed everywhere) when it is in this scene.
            Vector3 origin = Keeper();
            float originFacing = pc.PlayerData.Direction.x >= 0f ? 1f : -1f;
            current = "start";
            if (TryParseSpot(Plugin.SelfTestSpot.Value, out string spotScene, out Vector3 spot) && spotScene == scene.Id && Vector3.Distance(spot, origin) > 1f && !InAnyBuilding(spot, scene))
            {
                PutKeeper(spot, originFacing);
                yield return Frames(2);
                if (Vector3.Distance(Keeper(), spot) < 1f)
                {
                    Info($"the keeper moved to the remembered test spot {spot:F1} (Debug.SelfTestSpot); back to {origin:F1} at the end");
                }
                else
                {
                    Warn($"the remembered test spot {spot:F1} didn't take (he is at {Keeper():F1}); running from here");
                }
            }
            // Not inside a container zone (a built-up area with chests and crafting, the mount a chest can belong to;
            // the player's own zone readout counts other mounts too): into the nearest one, at a free spot with ground.
            bool inBuilding = InAnyBuilding(Keeper(), scene);
            if (ZoneAt(Keeper(), scene) == null || inBuilding)
            {
                // never inside a building (a save loaded in a house: the run of 2026-10-21 rode "Not indoors." 29 times)
                Vector3? into = SpotInNearestZone(Keeper(), scene, out WorldZoneData intoZone, q => !InAnyBuilding(q, scene));
                if (into.HasValue)
                {
                    PutKeeper(into.Value, originFacing);
                    yield return Frames(2);
                    if (Vector3.Distance(Keeper(), into.Value) < 1f)
                    {
                        Info($"the keeper stood {(inBuilding ? "in a building" : "in no container zone")}; moved into {intoZone.id} at {into.Value:F1} (back to {origin:F1} at the end)");
                    }
                    else
                    {
                        Warn($"the spot in {intoZone.id} at {into.Value:F1} didn't take (he is at {Keeper():F1}); running from here");
                    }
                }
            }
            if (InAnyBuilding(Keeper(), scene))
            {
                Warn("no spot outside a building could be reached in this scene: the run goes on here under the outdoor light (the door scenario skips)");
            }
            // The outdoor light wherever he stands now (moved out by SetPosition, the house's light stays on; a dungeon is
            // indoor light everywhere): the mount's rules read the light.
            if (Mount.Instance.IsIndoors())
            {
                EnvironmentEngine.Instance.SetTimeOfDayPreset("outdoor");
                yield return Until(() => !Mount.Instance.IsIndoors(), 3f);
                if (Mount.Instance.IsIndoors()) Warn($"the light stays indoor ({EnvironmentEngine.Instance?.TimesOfDay?.name}): the rides will be refused as indoors");
                else Info($"the light set to outdoor for the run (it was {originPreset}; it comes back with the keeper at the end)");
            }
            home = Keeper();
            homeFacing = originFacing;
            int bodiesAtStart = MainGame.PlayerData.GetResInt("cur_bodies_count");
            corpseDrops.Clear();
            WorldZoneData startZone = ZoneAt(home, scene);
            string zone = startZone?.id ?? MainGame.PlayerData.CurrentWorldZoneData?.Definition?.id;
            Line($"[test] start: {scene.Id} at {home:F1} ({(startZone != null ? "container zone " + zone : zone != null ? "zone " + zone + ", not a container zone" : "no zone")}), {Mount.All.Count} instance(s) in the world; levels {(lodOn ? "on" : "off")}, idle {(idleOff ? "switched off" : "frozen")}.");
            if (startZone == null)
            {
                Warn("this spot is in no container zone and none could be reached: the chest scenarios will skip. Start the run inside a built-up area with chests (the yard).");
            }

            // The world's own instances (not the tests'): all must come through the run as they were (AfterTheRun).
            worldBefore = WorldSnapshot();

            // The scenarios of this run (all, one group, or one): their last results are cleared, the others' stand.
            var scenarios = new List<Scenario>();
            foreach (Scenario c in Scenarios)
            {
                if (pick == null || pick(c)) scenarios.Add(c);
            }
            pick = null;
            // the run's own checks, always last (a group or one scenario too), never twice
            scenarios.RemoveAll(c => c.Group == GroupAfter);
            Scenario afterRun = Scenarios.Find(c => c.Group == GroupAfter);
            if (afterRun != null) scenarios.Add(afterRun);
            bool full = scenarios.Count == Scenarios.Count;
            foreach (Scenario c in scenarios)
            {
                c.State = RunState.NotRun;
                c.Pass = c.Fail = c.Warn = 0;
                c.Elapsed = 0f;
            }
            RunCount = scenarios.Count;
            RunDone = 0;
            RunStarted = Time.unscaledTime;
            if (!full)
            {
                Line($"[test] this run: {scenarios.Count} of {Scenarios.Count} scenario(s): {string.Join("; ", scenarios.ConvertAll(c => c.Name))}");
            }
            foreach (Scenario s in scenarios)
            {
                if (abort)
                {
                    break;
                }
                Status = $"Running: {s.Name}";
                current = s.Name;
                running = s;
                s.State = RunState.Running;
                s.Started = Time.unscaledTime;
                Lod.On = true;
                Lod.IdleSwitchesOff = idleOff;
                Lod.SpawnsPerFrame = perFrame;
                bool panelWasHidden = Plugin.TuningWindowHidden;
                if (s.HidesPanel)
                {
                    Plugin.TuningWindowHidden = true;
                }
                yield return s.Body();
                // Between scenarios: everything back.
                yield return CleanUp();
                if (s.HidesPanel)
                {
                    Plugin.TuningWindowHidden = panelWasHidden;
                }
                s.Elapsed = Time.unscaledTime - s.Started;
                s.State = RunState.Done;
                running = null;
                RunDone++;
            }
            running = null;
            Lod.On = lodOn;
            Lod.IdleSwitchesOff = idleOff;
            Lod.SpawnsPerFrame = perFrame;
            Cargo.SlotFloor = slotFloorWas;
            foreach (BehaviourHeld h in freeplayWas) h.Restore();
            standingWas.Restore();
            if (corpseDrops.Count > 0)
            {
                // A corpse scenario that threw after its drop: its bodies are real, so they go now, not at the next load.
                current = "corpse bodies left by a failed scenario";
                yield return RemoveCorpseDrops(bodiesAtStart);
            }
            // Only a full run that passed everywhere remembers its spot: a passing group or scenario says nothing about
            // the others' ground.
            if (full && !abort && fail == 0 && warn == 0)
            {
                string remembered = scene.Id + "|" + home.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + home.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + home.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                if (Plugin.SelfTestSpot.Value != remembered)
                {
                    Plugin.SelfTestSpot.Value = remembered;
                    Info($"this spot passed everywhere and is remembered for the next run (Debug.SelfTestSpot = {remembered})");
                }
            }
            if (Vector3.Distance(origin, Keeper()) > 0.5f)
            {
                PutKeeper(origin, originFacing);
            }
            if (originPreset != null && EnvironmentEngine.Instance?.TimesOfDay?.name != originPreset)
            {
                EnvironmentEngine.Instance.SetTimeOfDayPreset(originPreset); // his own light with him (the house's, if he started in one)
            }
            if (armourWas && !Mount.Instance.InArmour(PC))
            {
                PC.SetArmorView(true);
            }
            string summary = $"{(abort ? "aborted" : "done")}{(full ? "" : $" ({scenarios.Count} of {Scenarios.Count} scenarios)")}: {pass} passed, {fail} failed, {warn} warning(s){(skip > 0 ? $", {skip} scenario(s) skipped" : "")}.";
            Line("[test] " + summary);
            Status = summary;
        }

        // Runs a coroutine with its nested coroutines itself, so an exception anywhere ends only that scenario (FAIL)
        // and the run goes on (Unity would stop the whole coroutine).
        internal static IEnumerator Drive(IEnumerator root)
        {
            Running = true; // set and cleared here only
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                object cur = null;
                bool more;
                try
                {
                    more = top.MoveNext();
                    if (more) cur = top.Current;
                }
                catch (Exception e)
                {
                    more = false;
                    Fail("threw " + e.GetType().Name + ": " + e.Message);
                    Plugin.Log.LogWarning("[test] " + e);
                    // The scenario that threw ends; the runner (the bottom of the stack) goes on with its cleanup.
                    while (stack.Count > 1)
                    {
                        stack.Pop();
                    }
                    continue;
                }
                if (!more)
                {
                    stack.Pop();
                    continue;
                }
                if (cur is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return cur;
            }
            Running = false;
        }

        internal static string current = "";

        internal static void Line(string s)
        {
            Lines.Add(s);
            Plugin.Log.LogInfo(s);
        }

        internal static void Pass(string what) { pass++; if (running != null) running.Pass++; Line($"[test] PASS {current}: {what}"); }

        internal static void Fail(string what) { fail++; if (running != null) running.Fail++; Line($"[test] FAIL {current}: {what}"); }

        internal static void Warn(string what) { warn++; if (running != null) running.Warn++; Line($"[test] WARN {current}: {what}"); }

        internal static void Info(string what) => Line($"[test] INFO {current}: {what}");
        // A scenario that can't run in this setup (the mounts switched off, say): said, counted apart, not a warning.
        internal static void Skip(string why) { skip++; if (running != null) running.Skipped = true; Line($"[test] SKIP {current}: {why}"); }

        internal static void Check(bool ok, string what)
        {
            if (ok) Pass(what); else Fail(what);
        }

        // ------------------------------------------------------------------ helpers

        internal static Vector3 home;
        internal static float homeFacing;
        internal static bool waited;

        internal static PlayerController PC => MainGame.PlayerController;

        internal static Vector3 Keeper() => PC.PlayerData.position.Value;

        // "scene|x,y,z" (Debug.SelfTestSpot), invariant culture.
        internal static bool TryParseSpot(string s, out string scene, out Vector3 p)
        {
            scene = null;
            p = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            int bar = s.IndexOf('|');
            if (bar <= 0) return false;
            string[] xyz = s.Substring(bar + 1).Split(',');
            if (xyz.Length != 3) return false;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (!float.TryParse(xyz[0], System.Globalization.NumberStyles.Float, ci, out float x)
                || !float.TryParse(xyz[1], System.Globalization.NumberStyles.Float, ci, out float y)
                || !float.TryParse(xyz[2], System.Globalization.NumberStyles.Float, ci, out float z)) return false;
            scene = s.Substring(0, bar);
            p = new Vector3(x, y, z);
            return true;
        }

        internal static void PutKeeper(Vector3 p, float facing)
        {
            PC.SetPosition(p);
            PC.PhysicalBody.SetFacingDirection(new Vector2(facing, 0f));
        }

        // The keeper moved and checked two frames later: a spot he can't stand on pushes him elsewhere, and the checks
        // after it would run against a keeper who isn't where they think (moved false: the scenario skips).
        internal static bool moved;

        // A list holds the string (in place of LINQ's Contains: no LINQ in the mod, it allocates).
        internal static bool Has(IReadOnlyList<string> list, string s)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (string.Equals(list[i], s, StringComparison.Ordinal)) return true;
            return false;
        }

        // A method's patches hold a prefix of this Harmony id.
        internal static bool HasPrefixOf(HarmonyLib.Patches info, string owner)
        {
            foreach (HarmonyLib.Patch p in info.Prefixes) if (p.owner == owner) return true;
            return false;
        }

        internal static IEnumerator MoveKeeper(Vector3 p, float facing)
        {
            PutKeeper(p, facing);
            yield return Frames(2);
            Vector3 k = Keeper();
            moved = new Vector2(k.x - p.x, k.z - p.z).magnitude < 1f;
            if (!moved)
            {
                Warn($"the keeper didn't stay at {p:F1} (he is at {k:F1}): that spot is blocked; skipped");
            }
        }

        internal static readonly Collider[] overlap = new Collider[8];

        // Nothing of the keeper-blocking layer (8, Obstacle) where he would stand.
        internal static bool FreeSpot(Vector3 p)
        {
            Rigidbody keeper = PC?.PhysicalBody?.Rb;
            int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.35f, p + Vector3.up * 1.2f, 0.35f, overlap, 1 << 8, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < n; k++)
            {
                if (overlap[k] != null && overlap[k].attachedRigidbody != keeper)
                {
                    return false;
                }
            }
            return true;
        }

        // A point with ground and room for the keeper about 'dist' from 'from' (eight directions, sideways first).
        // also: a further condition on the spot (the chest scenarios want one inside a building zone).
        internal static Vector3? Spot(Vector3 from, float dist, Func<Vector3, bool> also = null)
        {
            float[] angles = { 0f, 180f, 30f, 150f, 210f, 330f, 90f, 270f };
            foreach (float a in angles)
            {
                float r = a * Mathf.Deg2Rad;
                Vector3 p = from + new Vector3(Mathf.Cos(r) * dist, 0f, Mathf.Sin(r) * dist * 0.6f);
                float g = Mount.Instance.GroundHeight(p, out _);
                if (!float.IsNaN(g))
                {
                    p.y = g;
                    if (FreeSpot(p) && (also == null || also(p)))
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        // A free spot with ground inside the nearest active container zone of the scene (its centre first, then rings
        // around it), or null. Nearest by the distance from p to the zone's rectangle.
        internal static Vector3? SpotInNearestZone(Vector3 p, GameScene scene, out WorldZoneData zone, Func<Vector3, bool> also = null)
        {
            zone = null;
            GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(scene.Id);
            if (sd?.WorldZonesByProcessingPriority == null) return null;
            var xz = new Vector2(p.x, p.z);
            var zones = new List<WorldZoneData>();
            foreach (var kv in sd.WorldZonesByProcessingPriority)
            {
                foreach (WorldZoneData z in kv.Value)
                {
                    if (z.IsContainer && z.IsActive && z.wholeZoneRect.width > 0f) zones.Add(z);
                }
            }
            zones.Sort((a, b) => RectDistance(a.wholeZoneRect, xz).CompareTo(RectDistance(b.wholeZoneRect, xz)));
            foreach (WorldZoneData z in zones)
            {
                Vector3 c = new Vector3(z.wholeZoneRect.center.x, p.y, z.wholeZoneRect.center.y);
                WorldZoneData want = z;
                Func<Vector3, bool> inside = q => ZoneAt(q, scene) == want && (also == null || also(q));
                float g = Mount.Instance.GroundHeight(c, out _);
                if (!float.IsNaN(g))
                {
                    c.y = g;
                    if (FreeSpot(c) && inside(c)) { zone = z; return c; }
                }
                foreach (float r in new[] { 2f, 4f, 6f, 9f })
                {
                    Vector3? s = Spot(c, r, inside);
                    if (s.HasValue) { zone = z; return s; }
                }
            }
            return null;
        }

        internal static float RectDistance(Rect r, Vector2 q)
        {
            float dx = Mathf.Max(r.xMin - q.x, 0f, q.x - r.xMax);
            float dy = Mathf.Max(r.yMin - q.y, 0f, q.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // The building zone a world object placed at p would belong to (the game's own test: an active container
        // zone whose rectangle holds the point, highest processing priority first), or null.
        internal static WorldZoneData ZoneAt(Vector3 p, GameScene scene)
        {
            GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(scene.Id);
            if (sd?.WorldZonesByProcessingPriority == null) return null;
            var xz = new Vector2(p.x, p.z);
            var priorities = new List<int>(sd.WorldZonesByProcessingPriority.Keys);
            for (int pi = priorities.Count - 1; pi >= 0; pi--)
            {
                var kv = new KeyValuePair<int, List<WorldZoneData>>(priorities[pi], sd.WorldZonesByProcessingPriority[priorities[pi]]);
                foreach (WorldZoneData z in kv.Value)
                {
                    if (z.IsContainer && z.IsActive && z.wholeZoneRect.Contains(xz)) return z;
                }
            }
            return null;
        }

        // The levels have settled: no view made or released and no level changed for 2 s (at most timeout seconds).
        internal static IEnumerator Settle(float timeout)
        {
            float end = Time.unscaledTime + timeout;
            string last = null;
            float since = Time.unscaledTime;
            while (Time.unscaledTime < end && !abort)
            {
                int a = 0, i = 0, s = 0;
                foreach (Mount.Instance x in Mount.All)
                {
                    if (x.tier == Mount.Tier.Active) a++; else if (x.tier == Mount.Tier.Idle) i++; else s++;
                }
                string now = $"{Lod.SpawnedTotal}/{Lod.ReleasedTotal}/{a}/{i}/{s}";
                if (now != last)
                {
                    last = now;
                    since = Time.unscaledTime;
                }
                else if (Time.unscaledTime - since >= 2f)
                {
                    yield break;
                }
                yield return null;
            }
            Info("the levels didn't settle within " + timeout + " s; going on");
        }

        internal static IEnumerator Until(Func<bool> cond, float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (!cond() && Time.unscaledTime < end && !abort)
            {
                yield return null;
            }
            waited = cond();
        }

        internal static IEnumerator Frames(int n)
        {
            for (int k = 0; k < n; k++)
            {
                yield return null;
            }
        }

        internal static IEnumerator Seconds(float s)
        {
            float end = Time.unscaledTime + s;
            while (Time.unscaledTime < end && !abort)
            {
                yield return null;
            }
        }

        // Two more test owners for the API's per-mod rules (not the core's: they can't change each other's instances).
        internal const string OwnerA = Owner + ".a", OwnerB = Owner + ".b";

        internal static bool IsTestOwner(string o) => o == Owner || o == OwnerA || o == OwnerB;

        // What must not change for the world's own instances during the run.
        internal static string Sig(Mount.Instance i) => $"{i.OwnerId}|{i.SceneId}|{i.HasDonkey}|{i.HasCart}|{i.Persist}|{i.Key}";

        // The world's own instances (not the tests', not ephemeral) by full id with their signature; a mod's foals as
        // one count per owner ("<owner> foals"), not by id: a mount switch remakes the ones over the new mount's
        // count with new ids (the panel scenario's commit of another mount did, 2026-10-27), which loses nothing.
        internal static Dictionary<string, string> WorldSnapshot()
        {
            var snap = new Dictionary<string, string>();
            var foals = new Dictionary<string, int>();
            foreach (Mount.Instance i in Mount.All)
            {
                if (IsTestOwner(i.OwnerId) || i.Ephemeral) continue;
                if (i.MountName == "foal") foals[i.OwnerId] = foals.TryGetValue(i.OwnerId, out int n) ? n + 1 : 1;
                else snap[i.FullId] = Sig(i);
            }
            foreach (var kv in foals) snap[kv.Key + " foals"] = kv.Value.ToString();
            return snap;
        }

        internal static MountHandle Spawn(Vector3 at, bool right, float scale = 1f, bool withCart = false, string owner = Owner,
            int id = 0, string key = null, bool persist = false, bool donkey = true)
        {
            return Mounts.Spawn(owner, new MountSpawn
            {
                Position = at,
                FacingRight = right,
                Scale = scale,
                Rideable = donkey && Mathf.Approximately(scale, 1f),
                Donkey = donkey,
                WithCart = withCart || !donkey,
                Persist = persist,
                Id = id,
                Key = key ?? "test-" + (++spawnSeq),
            });
        }

        internal static bool autoReloadAtStart;

        internal static IEnumerator CleanUp()
        {
            LeftBehind();
            if (Mounts.IsMounted)
            {
                Mounts.Dismount("self-test");
                yield return null;
            }
            foreach (string o in new[] { Owner, OwnerA, OwnerB })
            {
                foreach (MountHandle h in Mounts.InstancesOf(o))
                {
                    if (h.Exists) h.Remove();
                }
            }
            PutKeeper(home, homeFacing);
            yield return Seconds(1f);
        }

        // The safety net: a scenario that threw ends without its own cleanup; whatever it left under a test owner goes here.
        internal static void LeftBehind()
        {
            var left = new List<string>();
            try
            {
                foreach (Looks.SkinDef d in new List<Looks.SkinDef>(Looks.All))
                {
                    if (d.Owner != null && d.Owner.StartsWith(Owner, StringComparison.Ordinal) && Looks.Unregister(d.Id, d.Owner)) left.Add("skin " + d.Id);
                }
                foreach (Catalog.Entry k in new List<Catalog.Entry>(Catalog.All))
                {
                    if (k.Owner != null && k.Owner.StartsWith(Owner, StringComparison.Ordinal) && Catalog.Unregister(k.Id, k.Owner)) left.Add("mount " + k.Id);
                }
                if (Plugin.SkinPackAutoReload != null && Plugin.SkinPackAutoReload.Value != autoReloadAtStart)
                {
                    Plugin.SkinPackAutoReload.Value = autoReloadAtStart;
                    left.Add("the pack-watch switch");
                }
                foreach (string dir in System.IO.Directory.GetDirectories(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest*"))
                {
                    try { System.IO.Directory.Delete(dir, true); left.Add("folder " + System.IO.Path.GetFileName(dir)); } catch { }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[test] the cleanup between scenarios failed: " + e.Message);
            }
            if (left.Count > 0) Info("left behind by the last scenario, put back: " + string.Join(", ", left));
        }

        internal static bool IsActive(MountHandle h) => h.Exists && h.I.tier == Mount.Tier.Active;

        internal static string Dir(float? d) => d.HasValue ? d.Value.ToString("F0") : "none";

        // Every instance in the world with its view on: the view's size is its own, and its cart parts match whether it
        // has a cart. A reused view, taken by whichever donkey woke first, must fit that donkey (any owner).
        internal static IEnumerator WorldViewsRight()
        {
            yield return Frames(3);
            var bad = new List<string>();
            int seen = 0;
            foreach (Mount.Instance i in Mount.All)
            {
                if (i.Ephemeral || !i.Ready || !i.TestViewActive || !i.HasDonkey)
                {
                    continue;
                }
                seen++;
                if (Mathf.Abs(i.TestViewScale - i.ViewScale) > 0.01f) bad.Add($"{i.FullId} view size {i.TestViewScale:F2}, is {i.ViewScale:F2}");
                int cart = i.TestCartPartsOn();
                if (!i.HasCart && cart > 0) bad.Add($"{i.FullId} has {cart} cart part(s) on without a cart");
                if (i.HasCart && cart == 0) bad.Add($"{i.FullId} has a cart with no cart part on");
            }
            Check(bad.Count == 0, bad.Count == 0
                ? $"all {seen} donkeys with a view on: right size, cart parts as they should be"
                : $"{bad.Count} wrong: {string.Join("; ", bad.GetRange(0, Math.Min(6, bad.Count)))}");
        }

        // Everything that decides an instance's level and look, for a WARN or FAIL that has to say why.
        internal static string State(MountHandle h)
        {
            if (h == null || !h.Exists) return "gone";
            Vector3 k = Keeper();
            float dist = new Vector2(h.Position.x - k.x, h.Position.z - k.z).magnitude;
            Vector3 s = CameraSystem.Instance != null ? CameraSystem.WorldToScreenPoint(h.Position) : Vector3.zero;
            return $"level {h.I.tier}, frozen parts {h.I.TestQuietRenderers}, switched off {h.I.TestSwitchedOff}, view on {h.I.TestViewActive}, ridden {h.IsRidden}, {dist:F1} from the keeper, on screen at ({s.x:F0}, {s.y:F0}) of {Screen.width}x{Screen.height}";
        }

        // ------------------------------------------------------------------ the scenarios

        // The API's per-mod contracts (docs/api.md, "Several mods"): each mod numbers its own, a taken number or
        // key is refused, Find and InstancesOf, numbers aren't reused, only the owner changes kept and key, data per mod.
        internal static IEnumerator ApiIdentity()
        {
            Vector3 near = Spot(home, 4f) ?? home;
            Info("the next \"is not a loaded plugin's GUID\" warnings are this test's own: it spawns as two made-up mods on purpose");
            MountHandle a1 = Spawn(near, true, owner: OwnerA, id: 1, key: "same");
            MountHandle b1 = Spawn(near + new Vector3(1.5f, 0f, 0f), false, owner: OwnerB, id: 1, key: "same");
            if (a1 == null || b1 == null)
            {
                Fail("could not spawn the two #1s");
                yield break;
            }
            Check(a1.Id == 1 && b1.Id == 1 && a1.FullId != b1.FullId, $"two mods each have their own #1 ({a1.FullId}, {b1.FullId})");
            Check(Spawn(near, true, owner: OwnerA, id: 1) == null, "asking again for a number the mod has is refused");
            Check(Spawn(near, true, owner: OwnerA, key: "same") == null, "a key the mod already uses is refused");
            Check(Mounts.Find(OwnerA, 1) == a1 && Mounts.Find(OwnerB, "same") == b1, "Find by number and by key, per mod");
            Check(Mounts.InstancesOf(OwnerA).Count == 1 && Mounts.InstancesOf(OwnerB).Count == 1, "InstancesOf: one each");
            MountHandle a2 = Spawn(near, true, owner: OwnerA);
            int id2 = a2 != null ? a2.Id : -1;
            a2?.Remove();
            MountHandle a3 = Spawn(near, true, owner: OwnerA);
            // Relative, not #2 then #3: the owner's counter lives for the session, so a second run starts higher.
            Check(a3 != null && id2 > 1 && a3.Id > id2, $"a removed number isn't handed out again (got #{id2}, removed it, then #{(a3 != null ? a3.Id : -1)})");
            MountResult keepByOther = b1.SetPersist(OwnerA, true);
            MountResult keyByOther = b1.SetKey(OwnerA, "taken");
            Check(!keepByOther.Ok && !keyByOther.Ok, $"another mod can't change kept or key ({keepByOther.Reason})");
            Check(b1.SetPersist(OwnerB, true).Ok && b1.Persist, "its own mod can");
            b1.SetPersist(OwnerB, false);
            b1.SetData(OwnerA, "name", "from a");
            b1.SetData(OwnerB, "name", "from b");
            Check(b1.GetData(OwnerA, "name") == "from a" && b1.GetData(OwnerB, "name") == "from b", "each mod has its own data on the same donkey");
            b1.SetData(OwnerA, "name", null);
            Check(b1.GetData(OwnerA, "name") == null && b1.GetData(OwnerB, "name") == "from b" && b1.DataKeys(OwnerB).Count == 1, "clearing one mod's value leaves the other's");
            yield return null;
        }

        // The side file's text for kept instances (a parent with a foal, a donkey with its cart, a free cart; a key,
        // own settings, data with tabs and line breaks) written and read back in memory: every field the same. No file
        // is written and the game's save isn't touched.
        internal static IEnumerator RoundTrip()
        {
            Vector3 near = Spot(home, 5f) ?? home;
            MountHandle parent = Spawn(near, false, persist: true, key: "rt parent");
            MountHandle cartDonkey = Spawn(near + new Vector3(3f, 0f, 0f), true, withCart: true, persist: true, key: "rt cart");
            MountHandle freeCart = Spawn(near + new Vector3(-3f, 0f, 0f), true, donkey: false, persist: true, key: "rt free");
            if (parent == null || cartDonkey == null || freeCart == null)
            {
                Fail("could not spawn");
                yield break;
            }
            MountHandle foal = Mounts.SpawnFoal(Owner, parent, 0.6f, key: "rt foal", persist: true);
            string v2 = "tab\there, new\nline, back\\slash"; // a tab, a line break and a backslash in one value
            parent.SetData(Owner, "tricky", v2);
            parent.Tuning.Set(Owner, "CoatColorize", "00FF80");
            // a skin of the test's own whose [Tuning.Cart] sets a cart-style value (HitchGapPx 9): the skin id is kept, the
            // merged value is not
            string gapRoot = WriteTempPack("gk2donkeyapi-selftest-rtgap", "selftest.rt-gap", "[Skin]\nformat = 1\nname = Round Trip Gap\n\n[Tuning.Cart]\nHitchGapPx = 9\n");
            const string gapOwner = Owner + ".rtgap";
            Looks.RegisterFolder(gapRoot, gapOwner);
            string gapSkin = Looks.SkinKey(Catalog.DonkeyId, "selftest.rt-gap");
            cartDonkey.Tuning.Set(Owner, "Skin", gapSkin);
            yield return Until(() => cartDonkey.Tuning.IsSet("HitchGapPx"), 6f); // the skin's rig merges its cart-style values once the view is up
            yield return Frames(2);
            List<Mount.Instance> kept = Mount.KeptInstances(i => i.OwnerId == Owner, log: false);
            Check(kept.Count == (foal != null ? 4 : 3), $"the kept list has the test's {kept.Count} (parent, foal, donkey with cart, free cart)");
            string text = Mount.BuildSideText("selftest", "saved at test time", 42, kept, withDormant: false, out _);
            var header = new Dictionary<string, string>();
            var nexts = new List<string[]>();
            var records = new List<Mount.Record>();
            Mount.ParseSideText(text.Split('\n'), header, nexts, records);
            Check(header.TryGetValue("slot", out string slot) && slot == "selftest" && header["saved"] == "saved at test time" && header["day"] == "42",
                "the header comes back (slot, save time, day)");
            Check(records.Count == kept.Count, $"{records.Count} record(s) for {kept.Count} instance(s)");
            foreach (Mount.Instance i in kept)
            {
                Mount.Record r = records.Find(x => x.Owner == i.OwnerId && x.Id == i.Id);
                if (r == null)
                {
                    Fail($"{i.FullId}: no record");
                    continue;
                }
                var bad = new List<string>();
                if (r.Key != i.Key) bad.Add($"key '{r.Key}'");
                if (r.Donkey != i.HasDonkey || r.Cart != i.HasCart) bad.Add("mount");
                if (r.Scene != i.SceneId) bad.Add("scene");
                if ((r.Position - i.Position).sqrMagnitude > 1e-6f) bad.Add($"position {r.Position}");
                if (r.FacingRight != i.FacingRight) bad.Add("facing");
                if (Mathf.Abs(r.Scale - i.ViewScale) > 1e-5f || r.Rideable != i.Rideable) bad.Add("scale or rideable");
                if (r.RemoveWithParent != i.RemoveWithParent) bad.Add("removeWithParent");
                string wantParent = i.Parent != null ? i.Parent.FullId : null;
                if (r.ParentFull != wantParent) bad.Add($"parent {r.ParentFull}");
                foreach (var kv in i.Overrides)
                {
                    bool mountsOwn = i.OverrideOwners.TryGetValue(kv.Key, out string by) && (by == Looks.TuningOwner || Mount.IsAbilityOwner(by)); // the mount's merge and an ability's values: set again in play, never written
                    if (mountsOwn && r.Sets.Exists(x => x[0] == kv.Key)) bad.Add("the mount's own " + kv.Key + " written (it comes back with the skin)");
                    else if (!mountsOwn && !r.Sets.Exists(x => x[0] == kv.Key)) bad.Add("setting " + kv.Key);
                }
                foreach (var mod in i.Data)
                {
                    foreach (var kv in mod.Value)
                    {
                        if (!r.DataLines.Exists(x => x[0] == mod.Key && x[1] == kv.Key && x[2] == kv.Value)) bad.Add("data " + kv.Key);
                    }
                }
                Check(bad.Count == 0, $"{i.FullId} ({r.Label}) comes back the same{(bad.Count > 0 ? ": " + string.Join(", ", bad) : "")}");
            }
            Mount.Record pr = records.Find(x => x.Key == "rt parent");
            Check(pr != null && pr.DataLines.Exists(x => x[1] == "tricky" && x[2] == v2), "a value with a tab, a line break and a backslash comes back exactly");
            Mount.Record cr = records.Find(x => x.Key == "rt cart");
            Check(cr != null && cr.Sets.Exists(x => x[0] == "Skin" && x.Length > 1 && x[x.Length - 1] == gapSkin), $"the skin id is in the record (Skin = {gapSkin})");
            Check(cartDonkey.Tuning.IsSet("HitchGapPx") && cartDonkey.Tuning.SetBy("HitchGapPx") == Looks.TuningOwner && cr != null && !cr.Sets.Exists(x => x[0] == "HitchGapPx"), "the skin's own hitch gap is on the wearer (cart style) but not in the record");
            LeftOver(gapOwner, gapSkin, gapRoot);
        }

        // A donkey takes a free cart and leaves it again: one instance with the cart (the cart's data merged, its parts
        // on), then a new free cart of the same owner and kept setting, the donkey's cart parts off.
        internal static IEnumerator HitchUnhitch()
        {
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle d = Spawn(near, true);
            MountHandle c = Spawn(near + new Vector3(2.5f, 0f, 0f), true, donkey: false);
            if (d == null || c == null)
            {
                Fail("could not spawn");
                yield break;
            }
            c.SetData(Owner, "cart", "its own");
            yield return Until(() => d.IsReady && c.IsReady, 6f);
            if (!waited)
            {
                Fail("never got their views");
                yield break;
            }
            MountResult h = Mounts.Hitch(d, c);
            Check(h.Ok, $"hitched{(h.Ok ? "" : " (" + h.Reason + ")")}");
            if (!h.Ok) yield break;
            Check(!c.Exists && d.HasCart, "the free cart is gone into the donkey");
            Check(d.GetData(Owner, "cart") == "its own", "the cart's data came with it");
            yield return Until(() => d.IsReady, 4f);
            yield return Frames(3);
            Check(d.I.TestCartPartsOn() > 0, $"the cart's parts are on ({d.I.TestCartPartsOn()})");
            // The hitch gap (HitchGapPx): the whole cart sits further behind the mount, on both facings; a cart left
            // standing stays where it was drawn, and taken again it is drawn there still.
            float Along(MountHandle m) // the cart body's distance behind the body, along the ground
            {
                Vector3? cart = m.I.TestCartBodyWorld();
                if (cart == null) return float.NaN;
                Vector3 v = cart.Value - m.I.BodyPoint();
                v.y = 0f;
                return v.magnitude;
            }
            float plain = Along(d);
            float facing = d.I.FacingRight ? 1f : -1f;
            Vector3 dir = d.I.TestHitchDir();
            Check(dir != Vector3.zero && dir.x * facing < 0f, $"the cart trails the mount: the measured way to it ({dir:F2}) is against the facing ({facing})");
            d.Tuning.Set(Owner, "HitchGapPx", 20);
            yield return Frames(3);
            float wide = Along(d);
            Check(Mathf.Abs(d.I.TestHitchGap() - 0.4f) < 0.001f && Mathf.Abs(wide - plain - 0.4f) < 0.03f, $"HitchGapPx 20: the cart body is 0.40 further behind ({plain:F2} -> {wide:F2})");
            d.I.MoveTo(d.Position, -facing);
            yield return Frames(3);
            float turned = Along(d);
            Check(Mathf.Abs(turned - wide) < 0.03f, $"turned round: the same gap on the other side ({turned:F2})");
            Vector3 drawn = d.I.TestCartBodyWorld() ?? Vector3.zero;
            MountHandle left = Mounts.Unhitch(d);
            Check(left != null && left.Exists && !left.HasDonkey && left.HasCart, "unhitched: a free cart again");
            if (left == null) yield break;
            Check(left.Owner == d.Owner && left.Persist == d.Persist, "the new free cart has the donkey's owner and kept setting");
            Check(!d.HasCart, "the donkey has no cart now");
            yield return Until(() => d.IsReady && left.IsReady, 4f);
            yield return Frames(3);
            Check(d.I.TestCartPartsOn() == 0, $"the donkey's cart parts are off ({d.I.TestCartPartsOn()} on)");
            Vector3 standing = left.I.TestCartBodyWorld() ?? Vector3.one * 99f;
            Check(Vector3.Distance(new Vector3(drawn.x, 0f, drawn.z), new Vector3(standing.x, 0f, standing.z)) < 0.05f, $"the free cart stands where the cart was drawn, gap and all ({drawn:F2} -> {standing:F2})");
            MountResult again = Mounts.Hitch(d, left);
            Check(again.Ok, $"hitched again{(again.Ok ? "" : " (" + again.Reason + ")")}");
            if (again.Ok)
            {
                yield return Until(() => d.IsReady, 4f);
                yield return Frames(3);
                Vector3 back = d.I.TestCartBodyWorld() ?? Vector3.one * 99f;
                Check(Vector3.Distance(new Vector3(drawn.x, 0f, drawn.z), new Vector3(back.x, 0f, back.z)) < 0.05f, $"taken again: the cart is drawn where it stood ({back:F2})");
                Check(Mathf.Abs(Along(d) - wide) < 0.03f, "and the gap is on it again");
            }
            d.Tuning.Clear(Owner, "HitchGapPx");
            yield return Frames(3);
            Check(Mathf.Abs(Along(d) - plain) < 0.03f, $"gap cleared: the prefab's distance again ({Along(d):F2})");
        }

        // A standing donkey with its cart far away has no view (asleep): its cart is left where it stands all the same,
        // on the data alone (a mod calling a donkey to ride leaves a parked cart this way), and the donkey called to the keeper comes
        // without cart parts while the cart stays put with the cargo.
        internal static IEnumerator UnhitchWithoutView()
        {
            Vector3? far = Spot(home, 50f) ?? Spot(home, 42f);
            if (!far.HasValue)
            {
                Warn("no ground 42 to 50 units around the keeper: skipped");
                yield break;
            }
            MountHandle d = Spawn(far.Value, true, withCart: true);
            if (d == null)
            {
                Fail("could not spawn");
                yield break;
            }
            yield return Until(() => d.I.tier == Mount.Tier.Asleep, 6f);
            if (!waited || d.I.HasViewObject)
            {
                Warn($"not asleep without a view {Vector3.Distance(home, far.Value):F0} away (level {d.I.tier}): skipped");
                yield break;
            }
            // The test item rides in the cart's mirror only (no view is ever built for the cart); taken off again at
            // the end, whichever instance holds it, so the runner's Remove never drops it into the world.
            var item = new Item(TestItemId);
            MountHandle left = null;
            try
            {
                d.I.CargoRig.SetCarried(new List<Item> { item });
                Vector3 stood = d.Position;
                left = Mounts.Unhitch(d);
                Check(left != null && left.Exists && left.HasCart && !left.HasDonkey, "unhitched without a view: a free cart");
                if (left == null) yield break;
                Check(Vector3.Distance(left.Position, stood) < 0.01f, $"the cart stands where the donkey stood ({left.Position:F2})");
                Check(!d.HasCart && !d.I.HasViewObject && !left.I.HasViewObject, "the donkey has no cart now; neither has a view (the data-only path)");
                Check(left.I.CargoRig.Count == 1 && d.I.CargoRig.Count == 0, $"the cargo went with the cart ({left.I.CargoRig.Count} on it, {d.I.CargoRig.Count} on the donkey)");
                Check(left.Owner == d.Owner && left.Persist == d.Persist, "the cart has the donkey's owner and kept setting");
                MountResult come = d.ComeToKeeper(true);
                Check(come.Ok, $"called to the keeper{(come.Ok ? "" : " (" + come.Reason + ")")}");
                yield return Until(() => d.IsReady, 6f);
                Check(waited, "ready within 6 s");
                yield return Frames(3);
                Check(d.I.TestCartPartsOn() == 0, $"it came without cart parts ({d.I.TestCartPartsOn()} on)");
                Check(left.Exists && Vector3.Distance(left.Position, stood) < 0.01f && !left.HasDonkey, "the cart stayed where it was");
                Check(left.I.CargoRig.Count == 1, "the item is still on the cart");
            }
            finally
            {
                if (d.Exists) d.I.CargoRig.Forget(item);
                if (left != null && left.Exists) left.I.CargoRig.Forget(item);
            }
        }

        // Donkeys with the same colours share their colour textures; another colour adds its own; all removed, the
        // count comes back.
        internal static IEnumerator SharedColours()
        {
            Vector3 near = Spot(home, 4f) ?? home;
            yield return Seconds(0.5f);
            int s0 = Colors.SharedTextures.Count;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "CoatColorize", "8040C0");
            a?.Tuning.Set(Owner, "CoatColorizeAmount", 1f);
            yield return Until(() => a != null && a.IsReady && a.I.TestColoursOurs() != null, 6f);
            if (!waited)
            {
                Warn("colours never attached (colours off?): skipped");
                yield break;
            }
            yield return Frames(3);
            int s1 = Colors.SharedTextures.Count;
            MountHandle b = Spawn(near + new Vector3(1.5f, 0f, 0f), false);
            b?.Tuning.Set(Owner, "CoatColorize", "8040C0");
            b?.Tuning.Set(Owner, "CoatColorizeAmount", 1f);
            yield return Until(() => b != null && b.IsReady && b.I.TestColoursOurs() != null, 6f);
            yield return Frames(3);
            int s2 = Colors.SharedTextures.Count;
            Check(s2 == s1, $"a second donkey with the same colours adds no texture ({s1} -> {s2})");
            MountHandle c = Spawn(near + new Vector3(-1.5f, 0f, 0f), true);
            c?.Tuning.Set(Owner, "CoatColorize", "20C060");
            c?.Tuning.Set(Owner, "CoatColorizeAmount", 1f);
            yield return Until(() => c != null && c.IsReady && c.I.TestColoursOurs() != null, 6f);
            yield return Frames(3);
            int s3 = Colors.SharedTextures.Count;
            if (s3 > s2) Pass($"another colour adds its own ({s2} -> {s3})");
            else Warn($"another colour added none ({s2} -> {s3}): someone in the world has it already?");
            a?.Remove();
            b?.Remove();
            c?.Remove();
            yield return Frames(2);
            Check(Colors.SharedTextures.Count == s0, $"all removed: back to {s0} ({Colors.SharedTextures.Count})");
        }

        // A mod moving a section on its settings page (e.g. its Mount section moved up, right after Keys) renumbers the cfg
        // sections ("3. Donkey" -> "4. Donkey" here); a player's cfg from before keeps its values: a key the file lacks
        // under its section takes the value under the same name with another number (MountSettingsPage.Bind), the
        // section's own value wins when it has one, a key the file has nowhere keeps its default, of several old
        // copies the fullest (then the highest-numbered) wins, a key moved to another section (MovedKey) is found
        // there, and the old copies are gone from the file after a save (BepInEx writes orphans back otherwise).
        internal static IEnumerator MovedSections()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-moved.cfg");
            try
            {
                System.IO.File.WriteAllText(path, "## Settings file was created by plugin Moved test\n\n[1. Keys]\n\n[2. Behaviour]\n\n# Setting type: Int32\nCartSlots = 4\n\n[3. Donkey]\n\nName = Bella\n\nFoals = 2\n\n[4. Cart]\n\nLantern = false\n\n[5. Cart]\n\nLantern = true\n\n[2. Sound]\n\nBrays = false\n\n[7. Sound]\n\nBrays = true\n\nVolume = 80\n");
                var cfg = new ConfigFile(path, false) { SaveOnConfigSet = false };
                var page = new MountSettingsPage("dreas.gk2.selftest.moved", cfg, "Moved test", "test", "", null);
                page.Status("Keys");
                page.MovedKey("Mount", "MountName", "Donkey", "Name");
                page.Toggle("Mount", "Foo", true, "Foo", "");
                ConfigEntry<string> mountName = page.Text("Mount", "MountName", "", "Name", "");
                ConfigEntry<int> slots = page.Steps("Behaviour", "CartSlots", 6, 2, 6, 2, "Cart capacity", "");
                ConfigEntry<int> foals = page.Steps("Donkey", "Foals", 0, 0, 5, 1, "Foals", "");
                ConfigEntry<bool> lantern = page.Toggle("Cart", "Lantern", false, "Lantern", "");
                ConfigEntry<bool> bell = page.Toggle("Sound", "Bell", true, "Bell", "");
                ConfigEntry<bool> brays = page.Toggle("Sound", "Brays", false, "Brays", "");
                Check(slots.Value == 4, $"Behaviour moved from 2 to 3: CartSlots is the file's 4 ({slots.Value})");
                Check(foals.Value == 2, $"Donkey moved from 3 to 4: Foals is the file's ({foals.Value})");
                Check(mountName.Value == "Bella", $"a key moved to another section and name (MovedKey): Mount.MountName is the old Donkey.Name (\"{mountName.Value}\")");
                Check(lantern.Value, "Cart is 5 and the file has it under 5 too: its own value wins over the older 4");
                Check(bell.Value, "a key the file has nowhere keeps its default");
                Check(brays.Value, "Sound is 6 with two old copies (2 and 7): the fullest, 7, wins");
                Check(MountSettingsPage.PlainName("12. Sound") == "Sound" && MountSettingsPage.PlainName("General") == "General" && MountSettingsPage.PlainName("A. B") == "A. B", "plain section names");
                Check(MountSettingsPage.SectionNumber("12. Sound") == 12 && MountSettingsPage.SectionNumber("General") == -1, "section numbers");
                Check(MountSettingsPage.OrphansOf(cfg) != null, "BepInEx's orphan dictionary is reachable");
                // a picture row is found on screen by its label, so no other row may share it (a heading "Your mount"
                // next to the picture "Your mount" got a picture of its own, 2026-10-19)
                page.Preview("Mount", Mounts.Companion("dreas.gk2.selftest.moved"), "Picture");
                bool refused = false;
                try { page.Heading("Mount", "Picture"); } catch (ArgumentException) { refused = true; }
                Check(refused && !page.Rows.Exists(r => r.RowKind == MountSettingsPage.RowKind.Note && r.Label == "Picture"), "a row with a picture row's label is refused");
                cfg.Save();
                Dictionary<string, Dictionary<string, string>> after = MountSettingsPage.ReadSections(path);
                Check(after.TryGetValue("3. Behaviour", out Dictionary<string, string> b3) && b3.ContainsKey("CartSlots") && !after.ContainsKey("2. Behaviour"), "saved: CartSlots is under 3. Behaviour only, the 2. Behaviour copy is gone");
                Check(!after.ContainsKey("3. Donkey") || !after["3. Donkey"].ContainsKey("Name"), "saved: the old Donkey.Name line is gone");
                Check(!after.ContainsKey("4. Cart") && after.ContainsKey("5. Cart"), "saved: the old 4. Cart copy is gone, 5. Cart stays");
                Check(!after.ContainsKey("2. Sound") && after.TryGetValue("7. Sound", out Dictionary<string, string> s7) && !s7.ContainsKey("Brays") && s7.ContainsKey("Volume"), "saved: both old Brays copies are gone; 7. Sound keeps its Volume (a key nothing bound: an orphan, as before)");

                // A mod's cfg saves on every set (BepInEx's default): the page holds the saves while it binds and writes
                // once at the end, so the last row's old copy is dropped too (the 2026-10-18 live run left
                // "[5. Sound] Volume" in the file: BepInEx had saved inside the last Bind, before the drop).
                System.IO.File.WriteAllText(path, "[1. Keys]\n\n[2. Sound]\n\nVolume = 120\n");
                var cfg2 = new ConfigFile(path, false); // SaveOnConfigSet true, as a mod's own ConfigFile has it
                var page2 = new MountSettingsPage("dreas.gk2.selftest.moved2", cfg2, "Moved test 2", "test", "", null);
                Check(!cfg2.SaveOnConfigSet, "the page holds the mod's save-on-set while it binds");
                page2.Status("Keys");
                page2.Toggle("Mount", "Foo", true, "Foo", "");
                ConfigEntry<int> volume = page2.Steps("Sound", "Volume", 100, 0, 150, 10, "Volume", "");
                Check(volume.Value == 120, $"the last row bound carries its value ({volume.Value})");
                page2.FinishBinding();
                Check(cfg2.SaveOnConfigSet, "finished: the mod's save-on-set is back");
                Dictionary<string, Dictionary<string, string>> after2 = MountSettingsPage.ReadSections(path);
                Check(!after2.ContainsKey("2. Sound") && after2.TryGetValue("3. Sound", out Dictionary<string, string> s3) && s3.TryGetValue("Volume", out string v3) && v3 == "120", "saved once at the end: the old copy of the last row is gone and 3. Sound has the value");
            }
            finally
            {
                try { System.IO.File.Delete(path); } catch { }
            }
            yield break;
        }

        // The player-owned files as every public release wrote them (tests/fixtures/released/<version>/,
        // docs/compatibility.md): an update must read every value a player has on disk. The API's cfg: every
        // key is still bound by this build and reads as its type (a mod's own cfg is that mod's test). The side file: the reader takes every record whole.
        // And a side file of a format this version doesn't read is set aside, never left for the next save to delete.
        internal static IEnumerator UpgradeFiles()
        {
            string root = Defaults.RepoPath(System.IO.Path.Combine("..", "..", "tests", "fixtures", "released"));
            if (root == null || !System.IO.Directory.Exists(root))
            {
                Warn("tests/fixtures/released isn't on this machine (no sources): skipped");
                yield break;
            }
            string[] dirs = System.IO.Directory.GetDirectories(root);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            Check(dirs.Length > 0, $"released versions on file: {dirs.Length}");
            foreach (string dir in dirs)
            {
                string version = System.IO.Path.GetFileName(dir);

                // the API's cfg: every key still bound, every value still of the key's type. That release's
                // defaults.cfg (the same section and key names, written by that build) is a second witness: a cfg
                // fixture reconstructed from a later file can't show a key renamed in between, the defaults can.
                foreach (string name in new[] { "dreas.gk2.donkeyapi.cfg", "defaults.cfg" })
                {
                    string api = System.IO.Path.Combine(dir, name);
                    if (!System.IO.File.Exists(api) || Plugin.Instance == null) continue;
                    Dictionary<string, Dictionary<string, string>> file = MountSettingsPage.ReadSections(api);
                    var lost = new StringBuilder();
                    var bad = new StringBuilder();
                    int n = 0, lostCount = 0, badCount = 0, retiredCount = 0;
                    foreach (var s in file)
                    {
                        foreach (var kv in s.Value)
                        {
                            n++;
                            var def = new ConfigDefinition(s.Key, kv.Key);
                            if (Plugin.Instance.IsRetired(s.Key, kv.Key))
                            {
                                retiredCount++; // dropped on purpose (Plugin.DropRetired: the [Effects] keys, 2026-10-25)
                                continue;
                            }
                            if (!Plugin.Instance.Config.ContainsKey(def))
                            {
                                lostCount++;
                                lost.Append(lost.Length > 0 ? ", " : "").Append('[').Append(s.Key).Append("] ").Append(kv.Key);
                                continue;
                            }
                            try
                            {
                                TomlTypeConverter.ConvertToValue(kv.Value, Plugin.Instance.Config[def].SettingType);
                            }
                            catch (Exception e)
                            {
                                badCount++;
                                bad.Append(bad.Length > 0 ? "; " : "").Append('[').Append(s.Key).Append("] ").Append(kv.Key).Append(" = ").Append(kv.Value).Append(": ").Append(e.Message);
                            }
                        }
                    }
                    Check(lostCount == 0, $"{version} {name}: every one of its {n} keys is still bound or retired on purpose ({retiredCount} retired; a renamed or dropped key resets a player's value)" + (lostCount > 0 ? $"; not bound: {lost}" : ""));
                    Check(badCount == 0, $"{version} {name}: every value reads as its key's type" + (badCount > 0 ? $": {bad}" : ""));
                }

                // the side file(s) as that version wrote them
                foreach (string dat in System.IO.Directory.GetFiles(dir, "*.gk2donkeyapi.dat"))
                {
                    var header = new Dictionary<string, string>();
                    var nexts = new List<string[]>();
                    var records = new List<Mount.Record>();
                    Mount.ParseSideText(System.IO.File.ReadAllLines(dat), header, nexts, records);
                    header.TryGetValue("format", out string format);
                    Check(format == Mount.SideFileFormat.ToString(), $"{version} side file: format {format} is the one this version reads ({Mount.SideFileFormat}); a newer format needs a reader for this one, never \"nothing restored\"");
                    int whole = 0, cargo = 0, chests = 0, sets = 0, data = 0;
                    foreach (Mount.Record r in records)
                    {
                        if (r.Complete && !string.IsNullOrEmpty(r.Owner) && r.Id > 0 && !string.IsNullOrEmpty(r.Scene)) whole++;
                        cargo += r.Cargo.Count;
                        if (r.Chest != null) chests++;
                        sets += r.Sets.Count;
                        data += r.DataLines.Count;
                    }
                    Check(records.Count > 0 && whole == records.Count, $"{version} side file: all {records.Count} records read whole ({whole}), {nexts.Count} counter(s)");
                    Info($"{version} side file: {cargo} cargo item(s), {chests} chest(s), {sets} setting(s), {data} data line(s)");
                    if (version == "0.9.1")
                    {
                        Mount.Record parent = records.Find(r => r.Id == 1), foal = records.Find(r => r.Id == 2), cart = records.Find(r => r.Id == 3);
                        Check(parent != null && parent.Name == "Bella" && parent.Label == "donkey" && !parent.FacingRight && parent.Position == new Vector3(12.5f, -3.25f, 0f) && parent.Sets.Count == 1 && parent.Sets[0][0] == "SeatAdjustPx" && parent.Sets[0][2] == "3",
                            "0.9.1: the named donkey with its position, facing and its own setting");
                        Check(parent != null && parent.DataLines.Count == 1 && parent.DataLines[0][2] == "a\ttab and a \\ backslash", "0.9.1: a mod's data line with its escaped tab and backslash");
                        Check(foal != null && foal.Label == "foal" && Math.Abs(foal.Scale - 0.6f) < 0.001f && foal.ParentFull == parent?.FullId && foal.RemoveWithParent, "0.9.1: the foal of size 0.6 with its parent");
                        Check(cart != null && cart.Label == "free cart" && cart.FacingRight && cart.Cargo.Count == 2 && cart.Chest == "6f1c2a9e-0000-4000-8000-000000000003", "0.9.1: the free cart with two items and its chest");
                    }
                }
            }

            // a side file this version can't read is set aside under its format's name, not left for the save to delete
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-upgrade");
            try
            {
                System.IO.Directory.CreateDirectory(tmp);
                string p = System.IO.Path.Combine(tmp, "Slot.gk2donkeyapi.dat");
                System.IO.File.WriteAllText(p, "format\t99\n");
                string aside = Mount.SetAside(p, "99");
                Check(aside != null && !System.IO.File.Exists(p) && System.IO.File.Exists(aside) && System.IO.Path.GetFileName(aside) == "Slot.gk2donkeyapi.format99.dat", $"a side file of format 99 is set aside as {System.IO.Path.GetFileName(aside ?? "(none)")}");
                System.IO.File.WriteAllText(p, "format\t99\nslot\tnewer\n");
                string again = Mount.SetAside(p, "99");
                Check(again == aside && System.IO.File.ReadAllText(again).Contains("newer"), "set aside again: the newer file replaces the one already aside");
            }
            finally
            {
                try { System.IO.Directory.Delete(tmp, true); } catch { }
            }
            yield break;
        }

        // The world's own instances at the run's start (owner, number, scene, mount, kept), for AfterTheRun.
        internal static Dictionary<string, string> worldBefore;

        // After every other scenario of the run (and its clean-up): the world's own donkeys as they were at the start, no
        // test donkey left. A world's donkey that changed on its own (the player's companion taking a mount chosen since
        // the save, out of view until a test came near) shows here too.
        internal static IEnumerator AfterTheRun()
        {
            var before = worldBefore ?? new Dictionary<string, string>();
            Dictionary<string, string> after = WorldSnapshot();
            var changed = new List<string>();
            foreach (var kv in before)
            {
                if (!after.TryGetValue(kv.Key, out string now)) changed.Add(kv.Key + " gone");
                else if (now != kv.Value) changed.Add($"{kv.Key} {kv.Value} -> {now}");
            }
            foreach (string k in after.Keys)
            {
                if (!before.ContainsKey(k)) changed.Add(k + " new");
            }
            Check(changed.Count == 0, changed.Count == 0
                ? $"all {before.Count} came through unchanged (owner, number, scene, mount, kept; foals by count)"
                : $"{changed.Count} changed: {string.Join("; ", changed.GetRange(0, Math.Min(8, changed.Count)))}");
            int leftover = 0;
            foreach (Mount.Instance i in Mount.All)
            {
                if (IsTestOwner(i.OwnerId)) leftover++;
            }
            Check(leftover == 0, $"no test donkey left behind ({leftover})");
            yield break;
        }

        // The settings rows every player mod gets (MountRows, FoalRows, EffectRows), on a throwaway page and a test
        // companion: the Mount dropdown picks a mount and its Skin dropdown that mount's skin; the companion's Skin
        // follows; one Skin row shows at a time; each mount remembers its own skin; the babies rows bind the companion's
        // foal values; there is no effect row. Then the mounts that come late (docs/mounts-as-mods.md
        // 4): every page shown this session has a Skin row for every mount with a skin, whatever order the plugins
        // loaded in; and a mount declared by a pack after the page's rows were built gets its Skin row and its
        // per-mount rows at CatchUp (what Show runs the frame after Publish).
        internal static IEnumerator SettingsRows()
        {
            // a second mount of the test's own, with babies and no cart (a pack's mount declared before the page, as a
            // mod's mounts are at the API's start): the rows the API builds for any mount
            const string beast = "selftest.rows-beast", beastOwner = Owner + ".rowsbeast";
            string beastSkin = Looks.SkinKey(beast, Looks.DefaultSkinName);
            string beastRoot = WriteTempPack("gk2donkeyapi-selftest-rowsbeast", Looks.DefaultSkinName,
                "[Skin]\nformat = 1\nname = Rows Look\nmount = " + beast + "\n\n[Mount]\nid = " + beast + "\nname = Rows Beast\ndescription = A test creature for the rows.\nauthor = the self-test\nversion = 0.1\ncart = false\nfoals = true\n");
            Looks.RegisterFolder(beastRoot, beastOwner);
            foreach (MountSettingsPage live in MountSettingsPage.All)
            {
                if (!live.Shown || !live.HasMountRow) continue;
                var missing = new List<string>();
                foreach (MountInfo k in Mounts.Catalog.All) if (k.Skins.Count > 0 && !k.Owner.StartsWith(Owner, StringComparison.Ordinal) && !live.HasSkinRow(k.Id)) missing.Add(k.Id); // a scenario's own mount (left behind, or a second run) is no fault of the page
                Check(missing.Count == 0, $"the live page \"{live.Title}\" has a Skin row for every mount with a skin ({Mounts.Catalog.All.Count} mounts{(missing.Count > 0 ? "; missing: " + string.Join(", ", missing) : "")})");
            }
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-rows.cfg");
            try
            {
                try { System.IO.File.Delete(path); } catch { }
                var cfg = new ConfigFile(path, false) { SaveOnConfigSet = false };
                const string owner = Owner + ".rows";
                MountCompanion me = Mounts.Companion(owner);
                var page = new MountSettingsPage(owner, cfg, "Rows test", "test", "", null);
                page.MountRows("Mount", me);
                page.FoalRows("Mount", me);
                ConfigEntry<T> Row<T>(string key) => page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == key)?.Entry as ConfigEntry<T>;
                ConfigEntry<string> mount = Row<string>("Mount"), skinDonkey = Row<string>("Skin_" + MountCatalog.DonkeyId), skinBeast = Row<string>("Skin_" + beast);
                Check(mount != null && skinDonkey != null && skinBeast != null, "the Mount row and a Skin row per mount are bound");
                if (mount == null || skinBeast == null) yield break;
                Check(mount.Value == MountCatalog.DonkeyId && me.Skin == MountLooks.DonkeyId, $"defaults: the donkey, the game's own art (\"{me.Skin}\")");
                int visible = page.Rows.FindAll(r => r.Entry != null && r.Entry.Definition.Key.StartsWith("Skin_", StringComparison.Ordinal) && (r.Visible == null || r.Visible())).Count;
                Check(visible == 1, $"one Skin row shows at a time ({visible})");
                mount.Value = beast;
                Check(me.Skin == Looks.FirstSkinOf(beast) && Mounts.Looks.Find(me.Skin)?.Mount == beast, $"Mount = beast: the companion's skin is the beast's first (\"{me.Skin}\")");
                skinBeast.Value = beastSkin;
                Check(me.Skin == beastSkin, $"Skin = Default (the beast's own look): the companion follows (\"{me.Skin}\")");
                mount.Value = MountCatalog.DonkeyId;
                Check(me.Skin == MountLooks.DonkeyId, $"back to the donkey: its own art again (\"{me.Skin}\")");
                mount.Value = beast;
                Check(me.Skin == beastSkin, $"the beast again: it remembered its skin (\"{me.Skin}\")");
                // the Mounts panel's data side (MountsPanel: the list and the words come from the registry, the choice
                // goes through the page's Mount row)
                page.SelectMount(MountCatalog.DonkeyId);
                Check(page.MountChosen == MountCatalog.DonkeyId && me.Skin == MountLooks.DonkeyId, $"the panel's choice goes through the Mount row: the donkey (\"{me.Skin}\")");
                page.SelectMount(beast);
                Check(page.MountChosen == beast && me.Skin == beastSkin, $"and the beast, with the skin it remembered (\"{me.Skin}\")");
                List<MountsPanel.Entry> entries = MountsPanel.Entries();
                MountsPanel.Entry beastEntry = entries.Find(e => e.Id == beast);
                int withSkins = 0;
                foreach (MountInfo k in Mounts.Catalog.All)
                {
                    // as MountChoices counts: a mount with a skin a player can pick (not set aside)
                    bool pickable = k.Id == MountCatalog.DonkeyId && k.Skins.Count > 0;
                    foreach (string sid in k.Skins) if (Looks.Find(sid)?.Unavailable == null) pickable = true;
                    if (pickable) withSkins++;
                }
                Check(entries.Count == withSkins && entries[0].Id == MountCatalog.DonkeyId, $"the panel lists every mount with a skin, the donkey first ({entries.Count} of {withSkins} mounts)");
                Check(beastEntry != null && beastEntry.Description.Length > 0 && beastEntry.Lines.Exists(l => l.StartsWith("Can't pull the cart")) && beastEntry.Lines.Exists(l => l.StartsWith("Has babies")), $"the beast's entry carries its description and what it does: no cart, babies ({beastEntry?.Lines.Count} lines)");
                ConfigEntry<int> foals = Row<int>("Foals");
                Check(foals != null && Row<bool>("FoalsWearSkin") == null && Row<bool>("FoalsIndoors") != null && Row<bool>("FoalsFollow") != null, "the three babies rows are bound (no \"same mount and skin\" row since 2026-10-27)");
                Check(Row<bool>("FoalsFollow") != null && Equals(Row<bool>("FoalsFollow").DefaultValue, false), "a page whose companion is left as the core has it: \"follow you on foot\" starts off");
                // per mount since 2026-10-26: the plain keys are the donkey's (its MountLook), the beast's carry its id
                if (foals != null)
                {
                    foals.Value = 2;
                    Check(me.OwnOf(MountCatalog.DonkeyId).FoalCount == 2, $"Babies = 2 reaches the donkey's own ({me.OwnOf(MountCatalog.DonkeyId).FoalCount})");
                    ConfigEntry<int> beastFoals = Row<int>("Foals_" + beast);
                    if (beastFoals != null)
                    {
                        beastFoals.Value = 0;
                        Check(me.OwnOf(beast).FoalCount == 0 && me.OwnOf(MountCatalog.DonkeyId).FoalCount == 2, "the beast's babies are its own: 0 for it leaves the donkey's 2");
                        Check(me.FoalCount == me.OwnOf(me.MountId).FoalCount, $"the companion's count is the current mount's ({me.MountId}: {me.FoalCount})");
                    }
                    else Warn("no Foals row for the beast (its pack not installed?): the per-mount babies not checked");
                    foals.Value = 0;
                }
                Check(me.OwnOf(MountCatalog.DonkeyId).FoalsWearSkin, "the babies wear their parent's mount and skin (no row turns it off)");
                Check(Row<string>("Effect") == null, "no Effect row (the effect rows went, 2026-10-25)");

                // the look per skin (PerSkin, MountLook; per mount until 0.40.0): a row set per skin of every mount, a mount's
                // first skin's keys the mount's (the donkey's plain), the others suffixed with the skin's id; the chosen
                // mount's chosen skin's set is the one shown; a skin's look is kept while another is chosen
                mount.Value = MountCatalog.DonkeyId; // the checks below assume the donkey chosen (the test left the beast chosen above and failed on it, 2026-10-19)
                int sets = 0, skinSets = 0;
                foreach (MountInfo k in Mounts.Catalog.All) skinSets += Looks.SkinChoicesOf(k.Id).Count;
                page.PerSkin((skinId, id, sfx, shown) =>
                {
                    sets++;
                    MountLook look = me.LookOf(skinId);
                    ConfigEntry<CoatColor> coatRow = page.Choice("Donkey", "Coat" + sfx, look.Coat, "Coat colour", "", v => look.Coat = v, new[] { "Game default", "Brown", "Dark", "Cream", "Black", "Custom" }, look: true);
                    page.Color("Donkey", "CustomCoat" + sfx, "Custom coat colour", "", PaletteColor.Chestnut, v => look.CustomCoat = v, () => coatRow.Value == CoatColor.Custom);
                });
                ConfigEntry<CoatColor> coatDonkey = Row<CoatColor>("Coat"), coatBeast = Row<CoatColor>("Coat_" + beast);
                ConfigEntry<int> lightBeast = Row<int>("CustomCoatLightness_" + beast);
                Check(sets == skinSets && coatDonkey != null && coatBeast != null && lightBeast != null, $"one row set per skin ({sets} of {skinSets}): the donkey's own art's keys plain, the beast's first skin's suffixed with the mount (CustomCoatLightness_beast too)");
                MountSettingsPage.Row rowBeast = page.Rows.Find(r => r.Entry == coatBeast), rowCustomBeast = page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == "CustomCoat_" + beast);
                Check(rowBeast != null && rowBeast.Shown != null && rowBeast.Enabled == null && rowCustomBeast != null && rowCustomBeast.Shown != null && rowCustomBeast.Enabled != null, "a per-mount row knows which mount shows it; the custom colour row keeps its own condition apart (Enabled)");
                Check(rowBeast != null && !rowBeast.Shown() && !rowBeast.Visible(), "with the donkey chosen the beast's Coat row isn't shown");
                coatBeast.Value = CoatColor.Custom;
                Check(me.LookOf(beastSkin).Coat == CoatColor.Custom && me.Coat == CoatColor.Game, $"the beast's coat set to Custom while the donkey is chosen: kept on the beast's look, the current (donkey's) coat still {me.Coat}");
                Check(rowCustomBeast != null && rowCustomBeast.Enabled() && !rowCustomBeast.Visible(), "its custom colour row is enabled by its preset but not shown (the beast isn't chosen)");
                mount.Value = beast;
                Check(me.MountId == beast && me.Coat == CoatColor.Custom, $"Mount = beast: the current look is the beast's ({me.MountId}: {me.Coat})");
                Check(rowBeast.Shown() && rowBeast.Visible() && rowCustomBeast.Visible(), "the beast's rows are the shown ones now");
                coatBeast.Value = CoatColor.Game;
                mount.Value = MountCatalog.DonkeyId;

                // a stored id nothing installed provides (its pack gone; seen 2026-10-27 as "donkey/<pack>-do..." cut short in the
                // journal): the dropdown lists it by a name made from the id, "(not installed)", the id kept as the choice
                const string goneId = "donkey/selftest.gone-skin";
                ConfigEntry<string> gone = page.List("Mount", "GoneTest", goneId, () => Looks.SkinChoicesOf(MountCatalog.DonkeyId), "Gone skin", "");
                MountSettingsPage.Row rowGone = page.Rows.Find(r => r.Entry == gone);
                if (rowGone != null) MountSettingsPage.FillList(rowGone);
                int atGone = rowGone?.ChoiceValues == null ? -1 : Array.IndexOf(rowGone.ChoiceValues, goneId);
                string goneLabel = atGone >= 0 && rowGone.ChoiceLabels != null && atGone < rowGone.ChoiceLabels.Length ? rowGone.ChoiceLabels[atGone] : "(not listed)";
                Check(goneLabel == "Gone skin (not installed)" && gone.Value == goneId, $"a skin nothing installed provides is listed by its name from the id, \"(not installed)\", the id kept (\"{goneLabel}\")");
                Check(MountsPanel.NameFromId("donkey/acme-pack.blaze") == "Blaze" && MountsPanel.NameFromId("donkey/frost") == "Frost" && MountsPanel.NameFromId("acme-pack.big-crab") == "Big crab",
                    $"a name from a skin id (after its mount and pack) as from a mount id: {MountsPanel.NameFromId("donkey/acme-pack.blaze")}, {MountsPanel.NameFromId("donkey/frost")}");

                // a mount that comes after the rows were built (a mod loaded after the page's mod): a pack declaring one
                const string lateOwner = Owner + ".late";
                string lateRoot = null;
                try
                {
                    lateRoot = WriteTempPack("gk2donkeyapi-selftest-late", Looks.DefaultSkinName,
                        "[Skin]\nformat = 1\nname = Late Test\nmount = selftest.late-beast\n\n[Mount]\nid = selftest.late-beast\nname = Late Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n");
                    int n = Looks.RegisterFolder(lateRoot, lateOwner);
                    Check(n == 1 && Mounts.Catalog.Find("selftest.late-beast") != null, $"a pack registered after the rows declares a mount ({n} pack)");
                    Check(!page.HasSkinRow("selftest.late-beast") && Row<CoatColor>("Coat_selftest.late-beast") == null, "before the catch-up the page has no rows for it");
                    int late = page.CatchUp();
                    ConfigEntry<string> skinLate = Row<string>("Skin_selftest.late-beast");
                    ConfigEntry<CoatColor> coatLate = Row<CoatColor>("Coat_selftest.late-beast");
                    Check(late == 1 && skinLate != null && skinLate.Value == "selftest.late-beast/default", $"the catch-up adds its Skin row with the pack's skin as the default ({late} mount(s); \"{skinLate?.Value}\")");
                    MountSettingsPage.Row rowLate = page.Rows.Find(r => r.Entry == coatLate);
                    Check(coatLate != null && rowLate != null && rowLate.Shown != null && !rowLate.Shown(), "and its per-mount coat row, wired to the Mount row (not shown while the donkey is chosen)");
                    bool offered = false;
                    foreach ((string id, string label) in Looks.MountChoices()) if (id == "selftest.late-beast") offered = true;
                    Check(offered, "the Mount list offers it");
                    mount.Value = "selftest.late-beast";
                    Check(me.Skin == "selftest.late-beast/default" && rowLate != null && rowLate.Shown(), $"chosen: the companion wears its skin (\"{me.Skin}\") and its rows show");
                    mount.Value = MountCatalog.DonkeyId;
                    Check(page.CatchUp() == 0, "a second catch-up adds nothing");
                }
                finally
                {
                    Looks.Unregister("selftest.late-beast/default", lateOwner);
                    Mounts.Catalog.Unregister(lateOwner, "selftest.late-beast");
                    try { if (lateRoot != null) System.IO.Directory.Delete(lateRoot, true); } catch { }
                }
            }
            finally
            {
                try { System.IO.File.Delete(path); } catch { }
                Looks.Unregister(beastSkin, beastOwner);
                Mounts.Catalog.Unregister(beastOwner, beast);
                try { System.IO.Directory.Delete(beastRoot, true); } catch { }
            }
            yield break;
        }

        // Mounts.Looks.SkinsChanged and Mounts.Catalog.MountsChanged fire on a register and on an unregister.
        internal static IEnumerator ChangedEvents()
        {
            int skins = 0, mounts = 0;
            Action onSkins = () => skins++;
            Action onMounts = () => mounts++;
            Mounts.Looks.SkinsChanged += onSkins;
            Mounts.Catalog.MountsChanged += onMounts;
            try
            {
                IDisposable skin = Mounts.Looks.RegisterRecipe(Owner, "selftest.evt-skin", "Event Skin", f => f.Pixels);
                Check(skins >= 1, $"SkinsChanged fired on the register ({skins})");
                IDisposable mount = Mounts.Catalog.Register(Owner, new MountDef { Id = "selftest.evt-mount", Name = "Event Kind" });
                Check(mounts >= 1, $"MountsChanged fired on the register ({mounts})");
                int skinsBefore = skins, mountsBefore = mounts;
                skin?.Dispose();
                mount?.Dispose();
                Check(skins > skinsBefore && mounts > mountsBefore, $"both fired again on the unregister ({skinsBefore} -> {skins}, {mountsBefore} -> {mounts})");
                Check(Mounts.Looks.Find("donkey/selftest.evt-skin") == null && Mounts.Catalog.Find("selftest.evt-mount") == null, "both are gone");
            }
            finally
            {
                Mounts.Looks.SkinsChanged -= onSkins;
                Mounts.Catalog.MountsChanged -= onMounts;
            }
            yield break;
        }

        // The near and far points of a renderer's bounds along a ground direction (the box's corners projected).
        internal static Vector3 Near(Bounds b, Vector3 dir)
        {
            Vector3 best = b.center; float bestDot = float.MaxValue;
            foreach (Vector3 c in Corners(b)) { float d = Vector3.Dot(c, dir); if (d < bestDot) { bestDot = d; best = c; } }
            return best;
        }
        internal static Vector3 Far(Bounds b, Vector3 dir)
        {
            Vector3 best = b.center; float bestDot = float.MinValue;
            foreach (Vector3 c in Corners(b)) { float d = Vector3.Dot(c, dir); if (d > bestDot) { bestDot = d; best = c; } }
            return best;
        }
        internal static IEnumerable<Vector3> Corners(Bounds b)
        {
            Vector3 mn = b.min, mx = b.max;
            yield return new Vector3(mn.x, mn.y, mn.z); yield return new Vector3(mx.x, mn.y, mn.z);
            yield return new Vector3(mn.x, mn.y, mx.z); yield return new Vector3(mx.x, mn.y, mx.z);
            yield return new Vector3(mn.x, mx.y, mn.z); yield return new Vector3(mx.x, mx.y, mn.z);
            yield return new Vector3(mn.x, mx.y, mx.z); yield return new Vector3(mx.x, mx.y, mx.z);
        }

        // A skin worn over the donkey (layer = over) in its own colours, from a temp pack (a red block named Harness and
        // a gold one named nothing, on every body frame): the donkey under it keeps its regions (a custom coat still
        // changes all the donkey's coat colours), the red follows Harness, the gold is kept as drawn, and it stays on
        // the exact swap (the donkey's colours plus the named one).
        // What the world draws of one renderer: it alone, through its own material and property block (the colour
        // swap as the game applies it), photographed by a camera of its own at the game camera's angle into a small
        // texture; the pixels back, with a count per colour class. The renderer is moved to a spare layer for the
        // one render and put back. Null when there is no game camera.
        internal sealed class DrawnAlone
        {
            internal int Pixels, Red, Blue, Green, Gold, Other;
            internal string Top; // the most drawn colours
            internal Dictionary<int, int> Count; // every drawn colour (RGB packed) and its pixel count
            internal Color32[] Px; // the render itself (Size x Size), for a pixel-by-pixel comparison of renders made at one spot
            internal int TopKey => Count == null || Count.Count == 0 ? -1 : new List<KeyValuePair<int, int>>(Count).Find(kv => kv.Value == MaxCount()).Key;
            private int MaxCount() { int m = 0; foreach (var kv in Count) if (kv.Value > m) m = kv.Value; return m; }

            // Where this render and `other` both draw (the overlap), how many of those pixels the pair `together` shows
            // in other's colour: which of the two is in front, whatever the light (all three renders are lit alike).
            internal (int Overlap, int OtherInFront) Overlap(DrawnAlone other, DrawnAlone together)
            {
                int overlap = 0, front = 0;
                if (Px == null || other?.Px == null || together?.Px == null || Px.Length != other.Px.Length || Px.Length != together.Px.Length) return (0, 0);
                for (int i = 0; i < Px.Length; i++)
                {
                    if (Px[i].a < 32 || other.Px[i].a < 32) continue;
                    overlap++;
                    Color32 o = other.Px[i], t = together.Px[i];
                    if (Math.Abs(o.r - t.r) <= 8 && Math.Abs(o.g - t.g) <= 8 && Math.Abs(o.b - t.b) <= 8) front++;
                }
                return (overlap, front);
            }

            // Pixels of this render whose colour the other render has nowhere: what was added, whatever the light
            // (the scene's lighting falls on both renders alike, so a class like "red" would fail at night).
            internal int AddedAgainst(DrawnAlone other)
            {
                int n = 0;
                foreach (var kv in Count) if (other == null || !other.Count.ContainsKey(kv.Key)) n += kv.Value;
                return n;
            }
        }

        // swapOff: the same render through a copy of the material with both colour-swap keywords off (the picture as
        // drawn, lit): what differs from the normal render is the swap's doing.
        // with: a second renderer drawn in the same picture (the body part under an overlay: the real sorting decides
        // which shows). The render is lit at noon (PreviewStage.Midday, as the Mods picture is; since 2026-10-27: by
        // night the lit red of the over skin read as blue and the keeper's colours collapsed into the wing's, two
        // scenarios failed), so a read-back is the same at any hour; still judge colours against another render of
        // the same thing, never against the colour in the picture.
        internal static DrawnAlone RenderAlone(SpriteRenderer r, bool swapOff = false, SpriteRenderer with = null, Vector3? at = null)
        {
            Camera main = Camera.main;
            if (r == null || !r || main == null) return null;
            const int Layer = 31, Size = 160;
            int oldLayer = r.gameObject.layer, oldWithLayer = with != null && with ? with.gameObject.layer : 0;
            Material oldMaterial = r.sharedMaterial, plain = null;
            GameObject go = null;
            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prev = RenderTexture.active;
            try
            {
                r.gameObject.layer = Layer;
                if (with != null && with) with.gameObject.layer = Layer;
                if (swapOff && oldMaterial != null)
                {
                    plain = new Material(oldMaterial) { name = "GK2DonkeyAPI selftest plain" };
                    plain.DisableKeyword("USE_PALETTE_COLOR_REPLACE");
                    plain.DisableKeyword("USE_LUT_COLOR_REPLACE");
                    r.sharedMaterial = plain;
                }
                go = new GameObject("GK2DonkeyAPI selftest readback camera");
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << Layer;
                cam.orthographic = true;
                cam.orthographicSize = 1.2f;
                cam.transform.rotation = main.transform.rotation;
                cam.transform.position = (at ?? r.bounds.center) - main.transform.forward * 8f; // `at`: one spot for renders to be compared pixel by pixel
                rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                rt.Create();
                cam.targetTexture = rt;
                using (PreviewStage.Midday.Hold()) cam.Render(); // noon for this render; the scene's light back at once
                RenderTexture.active = rt;
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
                Color32[] px = tex.GetPixels32();
                var d = new DrawnAlone { Px = px };
                var count = new Dictionary<int, int>();
                foreach (Color32 c in px)
                {
                    if (c.a < 32) continue;
                    d.Pixels++;
                    int key = c.r << 16 | c.g << 8 | c.b; // the exact colour
                    count[key] = count.TryGetValue(key, out int n) ? n + 1 : 1;
                    if (c.b > c.r + 40 && c.b > c.g + 40) d.Blue++;
                    else if (c.g > c.r + 40 && c.g > c.b + 40) d.Green++;
                    else if (c.r > 120 && c.g < 90 && c.b < 100) d.Red++;
                    else if (c.r > 130 && c.g > 90 && c.b < 110 && c.r > c.b + 60) d.Gold++;
                    else d.Other++;
                }
                d.Count = count;
                var top = new List<KeyValuePair<int, int>>(count);
                top.Sort((x, y) => y.Value.CompareTo(x.Value));
                var sb = new StringBuilder();
                for (int i = 0; i < Math.Min(6, top.Count); i++)
                {
                    sb.Append(i > 0 ? ", " : "").Append($"{top[i].Key:X6} x{top[i].Value}");
                }
                d.Top = sb.ToString();
                return d;
            }
            finally
            {
                RenderTexture.active = prev;
                r.gameObject.layer = oldLayer;
                if (with != null && with) with.gameObject.layer = oldWithLayer;
                if (plain != null)
                {
                    r.sharedMaterial = oldMaterial;
                    UnityEngine.Object.Destroy(plain);
                }
                if (tex != null) UnityEngine.Object.Destroy(tex);
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                if (go != null) UnityEngine.Object.Destroy(go);
            }
        }

        // A test donkey walking for a probe: MoveTo and ComeToKeeper place it (no walk), so this moves it a little every
        // frame and asks its animator for the walk (the stage's recipe: State 1, walk_speed 1) for as many frames as
        // given, running perFrame after each; then idle again. walkFrames counts the frames a walk picture showed.
        internal static IEnumerator WalkFor(MountHandle a, SpriteRenderer part, int frames, Action perFrame, Action<int> walkFrames)
        {
            Animator anim = part != null && part ? part.GetComponentInParent<Animator>() : null;
            int seen = 0;
            for (int i = 0; i < frames && a != null && a.Exists; i++)
            {
                if (anim != null)
                {
                    foreach (AnimatorControllerParameter p in anim.parameters)
                    {
                        if (p.name == "State" && p.type == AnimatorControllerParameterType.Int) anim.SetInteger("State", 1);
                        else if (p.name == "walk_speed" && p.type == AnimatorControllerParameterType.Float) anim.SetFloat("walk_speed", 1f);
                    }
                }
                a.MoveTo(a.Position + new Vector3(-0.03f, 0f, 0f), facingRight: false); // a few pixels a frame, the way it faces
                yield return null;
                if (part != null && part && part.sprite != null && part.sprite.name.Contains("walk")) seen++;
                perFrame?.Invoke();
            }
            if (anim != null)
            {
                foreach (AnimatorControllerParameter p in anim.parameters)
                {
                    if (p.name == "State" && p.type == AnimatorControllerParameterType.Int) anim.SetInteger("State", 0);
                }
                anim.SetTrigger(AnimationComponentBase.RESET_TO_IDLE_TRIGGER);
            }
            walkFrames?.Invoke(seen);
        }

        internal static IEnumerator SkinOverColours()
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-over");
            string packDir = TempSkinDir(tempRoot, Catalog.DonkeyId, "selftest.over-test");
            const string owner = Owner + ".over";
            var red = new Color32(196, 30, 56, 255);
            var gold = new Color32(226, 168, 48, 255);
            try
            {
                if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
                System.IO.Directory.CreateDirectory(packDir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(packDir, "skin.cfg"), "[Skin]\nformat = 1\nname = Over Test\nlayer = over\n[Colors]\nHarness = C41E38\n");
                var px = new Color32[96 * 96];
                for (int y = 40; y < 50; y++)
                {
                    for (int x = 50; x < 60; x++) px[y * 96 + x] = x < 58 ? red : gold;
                }
                var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(packDir, "bdy.png"), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            Check(Looks.RegisterFolder(tempRoot, owner) == 1, "the over pack is found");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            // its own colours only (Instance.OwnLook, as the companion's): the player's chosen mount's colours reached it
            // too (a Red harness and a Black coat under the test's blue, 2026-10-26: the readbacks failed)
            if (a != null) a.I.OwnLook = true;
            a?.Tuning.Set(Owner, "Skin", "donkey/selftest.over-test");
            yield return Until(() => a != null && a.IsReady && a.I.LooksRig.Wearing == "donkey/selftest.over-test" && !a.I.LooksRig.Building, 10f);
            if (!waited)
            {
                Fail("the over skin was never worn");
            }
            else
            {
                a.Tuning.Set(Owner, "CoatColorize", "00FF00");
                a.Tuning.Set(Owner, "CoatColorizeAmount", 1f);
                a.Tuning.Set(Owner, "HarnessColorize", "0000FF");
                a.Tuning.Set(Owner, "HarnessColorizeAmount", 1f);
                yield return Frames(3);
                var d = a.I.ColorsRig.TestMountGroup();
                bool Has(Color32 c) => d.ChangedSources.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b);
                MountColorRegion coat = null;
                foreach (MountColorRegion r in Colors.RegionList) if (r.Key == "Coat") coat = r;
                int coatChanged = 0, coatAll = coat?.SourceColors.Count ?? 0;
                if (coat != null)
                {
                    foreach (string hex in coat.SourceColors)
                    {
                        Colors.TryHex(hex, out Color32 c);
                        if (Has(c)) coatChanged++;
                    }
                }
                Check(d.Method == SpriteColorMethod.Palette && d.Colours <= Colors.PaletteMaxColours, $"the donkey's colours and the skin's named one: {d.Colours}, the exact swap ({d.Method})");
                Check(coatAll > 0 && coatChanged == coatAll, $"a custom coat still changes the donkey under it ({coatChanged} of its {coatAll} coat colours)");
                Check(Has(red), "the skin's red, named Harness, follows the harness");
                Check(!Has(gold), "the skin's gold, named nothing, is kept as drawn");

                // what the world draws of the overlay (its real material, the swap applied by the shader): the 80 red
                // pixels blue (the harness colorize), the 20 gold ones gold, no red left. A wrong palette, a missing
                // keyword or a sprite that isn't drawn shows here and nowhere else (a skin's plate of
                // 2026-10-19: the Mods picture right, the world without its red).
                SpriteRenderer ov = a.I.LooksRig.TestOverlay();
                Check(ov != null && ov.enabled && ov.sprite != null, $"the overlay renderer is on with a picture ({ov?.sprite?.name ?? "none"})");
                DrawnAlone drawn = ov != null ? RenderAlone(ov) : null;
                if (drawn == null) Warn("no game camera: the readback of the drawn overlay skipped");
                else
                {
                    Info($"drawn alone: {drawn.Pixels} pixels: blue {drawn.Blue}, gold {drawn.Gold}, red {drawn.Red}, green {drawn.Green}, other {drawn.Other}; most: {drawn.Top}; material {ov.sharedMaterial?.name} [{string.Join(" ", ov.sharedMaterial?.shaderKeywords ?? new string[0])}]");
                    // the same without the swap (the picture as drawn, lit by the scene: dark at night), so the swap's
                    // own doing is what differs between the two: the red pixels swapped, the gold ones left alone
                    DrawnAlone plain = RenderAlone(ov, swapOff: true);
                    if (plain != null) Info($"drawn alone with the swap off: {plain.Pixels} pixels: blue {plain.Blue}, gold {plain.Gold}, red {plain.Red}, green {plain.Green}, other {plain.Other}; most: {plain.Top}");
                    // the 10x10 picture drawn at the camera's density: about 1.8 x 1.8 screen pixels each
                    Check(drawn.Pixels >= 100 && drawn.Pixels <= 260, $"the world draws the whole overlay (100 picture pixels, {drawn.Pixels} drawn)");
                    // the red, lit by the scene, is the swap-off render's most drawn colour; with the swap on none of it
                    // is left (the gold's class varies with the light: at dusk it reads as "red", so the class is no judge)
                    int litRed = plain?.TopKey ?? -1;
                    bool redLeft = litRed >= 0 && drawn.Count.ContainsKey(litRed);
                    Check(drawn.Blue >= 60 && (plain == null ? drawn.Red == 0 : !redLeft), $"the red pixels come out blue, the harness colour ({drawn.Blue} blue; the lit red {(litRed >= 0 ? litRed.ToString("X6") : "?")} {(redLeft ? "still drawn" : "gone")})");
                    Check(plain != null && plain.Blue == 0 && plain.Pixels == drawn.Pixels, $"with the swap off nothing is blue and the same pixels are drawn ({plain?.Blue} blue, {plain?.Pixels} pixels)");
                    // the gold pixels: the same colours with the swap on and off (the swap leaves an unnamed colour
                    // alone), judged on the drawn colour list, whatever the light
                    string goldOn = drawn.Top, goldOff = plain?.Top ?? "";
                    string[] offColours = goldOff.Split(',');
                    int kept = 0;
                    foreach (string c in offColours)
                    {
                        string hex = c.Trim().Split(' ')[0];
                        if (hex.Length == 6 && goldOn.Contains(hex)) kept++;
                    }
                    int nonBlue = drawn.Pixels - drawn.Blue; // gold by day, dark brown by night: the class varies, the count doesn't
                    Check(kept >= 1 && nonBlue >= 14 && nonBlue <= 60, $"the gold pixels come out as without the swap: an unnamed colour of an over skin is drawn as it is ({kept} of the swap-off colours drawn the same, {nonBlue} non-blue pixels)");
                    // over the body: the overlay sits inside the body's silhouette (its square is on the torso), so
                    // it shows only when it sorts above the part (the same order = coplanar = the depth test's pick)
                    DrawnAlone both = RenderAlone(ov, with: a.I.LooksRig.TestPart());
                    if (both != null) Info($"drawn with the body: {both.Pixels} pixels: blue {both.Blue}; most: {both.Top}; overlay order {ov.sortingOrder}, part order {a.I.LooksRig.TestPart()?.sortingOrder}");
                    Check(both != null && both.Blue >= 60, $"the overlay shows over the body ({both?.Blue} blue pixels with the body drawn too)");

                    // while the donkey walks: the user saw the cloth flicker in and out of the body when moving, never
                    // idle (2026-10-19). Each frame: is the overlay's order still one above the part's (a stale copy
                    // would say so), and does it win the depth test with the body drawn, with its lift (OverLift)
                    // and with the lift taken away for the same render (the fault's own picture)
                    SpriteRenderer part = a.I.LooksRig.TestPart();
                    Material pm = part?.sharedMaterial;
                    Info($"material queue {pm?.renderQueue} (shader {pm?.shader?.name} queue {pm?.shader?.renderQueue}), part layer {part?.sortingLayerName} order {part?.sortingOrder}, overlay order {ov.sortingOrder}, overlay local z {ov.transform.localPosition.z:0.####} (lift {Looks.Rig.OverLift})");
                    // the donkey moved a little every frame with its walk playing (WalkFor: MoveTo and ComeToKeeper only
                    // place it; a plain destination left it standing and the probe sampled nothing, 2026-10-19)
                    int walked = 0, stale = 0, renders = 0, lost = 0, lostNoLift = 0, moving = 0, walkPics = 0;
                    Vector3 lastPos = a.Position;
                    yield return WalkFor(a, part, 48, () =>
                    {
                        walked++;
                        if (a.Position != lastPos) moving++;
                        lastPos = a.Position;
                        if (ov.sortingOrder != part.sortingOrder + 1) stale++;
                        if (walked % 3 != 1) return;
                        renders++;
                        DrawnAlone lifted = RenderAlone(ov, with: part);
                        if (lifted != null && lifted.Blue < 40) lost++;
                        Vector3 lp = ov.transform.localPosition;
                        ov.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
                        DrawnAlone flat = RenderAlone(ov, with: part);
                        ov.transform.localPosition = lp;
                        if (flat != null && flat.Blue < 40) lostNoLift++;
                    }, n => walkPics = n);
                    Info($"walking {walked} frames ({moving} with the position changing, {walkPics} showing a walk picture): the overlay's order stale on {stale}; of {renders} renders with the body it lost the depth test {lost} times with the lift and {lostNoLift} times without");
                    Check(moving >= walked - 2, $"the test donkey moves for the probe ({moving} of {walked} frames)");
                    Check(stale == 0, $"the overlay's sorting order follows the part's every frame while moving ({stale} stale of {walked})");
                    Check(renders > 0 && lost == 0, $"the overlay never flickers into the body while moving ({lost} of {renders} renders lost with the lift; {lostNoLift} without it)");
                }
            }
            a?.Remove();
            yield return Frames(2);
            Looks.Unregister("donkey/selftest.over-test", owner);
            yield return Frames(2);
            try { System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        // Debug.SkinPackAutoReload: a worn pack (a temp folder, one red picture for every body frame) whose picture is
        // saved again in blue is read again by itself within a few seconds, and the donkey wears the blue one; with the
        // switch off, the same save changes nothing. The switch is put back as it was.
        // An over pack's erase colour (FF00FF by default): a picture with it is built whole, the donkey's own frame with
        // those pixels cleared and the picture's other pixels on top, and drawn in the body's place; a picture without
        // it stays a plain overlay. The pack: the standing frame with a magenta block over the ears and a red block on
        // the back; the walk frames with the red block only.
        internal static IEnumerator SkinOverErase()
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-erase");
            string packDir = TempSkinDir(tempRoot, Catalog.DonkeyId, "selftest.erase-test");
            const string owner = Owner + ".erase";
            var red = new Color32(196, 30, 56, 255);
            var magenta = new Color32(255, 0, 255, 255);
            try
            {
                if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
                System.IO.Directory.CreateDirectory(packDir);
                // and the skin's own recolour of the donkey's coat under it ([Base.Coat]): blue, so it shows in the readback
                System.IO.File.WriteAllText(System.IO.Path.Combine(packDir, "skin.cfg"), "[Skin]\nformat = 1\nname = Erase Test\nlayer = over\n[Base.Coat]\nColorize = 0000FF\nColorizeAmount = 1\n");
                Color32[] Picture(bool ears)
                {
                    var px = new Color32[96 * 96];
                    for (int y = 40; y < 46; y++) for (int x = 55; x < 61; x++) px[y * 96 + x] = red;        // on the back
                    if (ears) for (int y = 60; y < 76; y++) for (int x = 35; x < 47; x++) px[y * 96 + x] = magenta;  // the ears (rows from the bottom)
                    return px;
                }
                foreach ((string key, bool ears) in new[] { ("bdy_static_left", true), ("bdy_walk_left_01", false), ("bdy_walk_left_02", false) })
                {
                    var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
                    tex.SetPixels32(Picture(ears));
                    tex.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(packDir, key + ".png"), tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(tex);
                }
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            Check(Looks.RegisterFolder(tempRoot, owner) == 1, "the erase pack is found");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a != null) a.I.OwnLook = true; // its own colours only: never the player's chosen mount's (as above)
            a?.Tuning.Set(Owner, "Skin", "donkey/selftest.erase-test");
            yield return Until(() => a != null && a.IsReady && a.I.LooksRig.Wearing == "donkey/selftest.erase-test" && !a.I.LooksRig.Building, 10f);
            if (!waited)
            {
                Fail("the erase skin was never worn");
            }
            else
            {
                yield return Frames(3);
                Looks.Loaded l = Looks.LoadedOwnColours("donkey/selftest.erase-test");
                Check(l != null && l.WholeKeys.Count == 1 && l.WholeKeys.Contains("bdy_static_left") && l.Erased == 16 * 12, $"one picture built whole, the standing one, {l?.Erased} pixels erased (16x12)");
                SpriteRenderer ov = a.I.LooksRig.TestOverlay(), part = a.I.LooksRig.TestPart();
                // the standing frame comes round between the idle clips: wait for it, the whole-frame checks need it
                yield return Until(() => ov != null && ov.sprite != null && ov.sprite.name.EndsWith("bdy_static_left", StringComparison.Ordinal), 8f);
                Info($"the donkey shows {ov?.sprite?.name ?? "nothing"}; the body part hidden: {a.I.LooksRig.TestShowing()}");
                if (!waited) Fail("the standing frame never showed within 8 s: the whole-frame checks couldn't run");
                else
                {
                    Check(a.I.LooksRig.TestShowing(), "on the standing frame the body part is hidden: the whole picture takes its place");
                    // what is drawn: the whole picture alone, against the donkey's own part alone (made visible for the render)
                    DrawnAlone whole = RenderAlone(ov);
                    bool was = part.forceRenderingOff;
                    part.forceRenderingOff = false;
                    DrawnAlone plain = RenderAlone(part);
                    part.forceRenderingOff = was;
                    Info($"drawn alone: the whole picture {whole?.Pixels} pixels ({whole?.Red} red), the donkey's own part {plain?.Pixels}");
                    Check(whole != null && plain != null && whole.Pixels < plain.Pixels - 100 && whole.Pixels > plain.Pixels / 2, $"the whole picture is the donkey with the ears gone ({whole?.Pixels} pixels against the part's {plain?.Pixels}: fewer, not another thing)");
                    // the red block: pixels in colours the donkey's own render has nowhere (judged against that render,
                    // under the same light: at night red comes out purple and a colour class would miss it)
                    int added = whole != null ? whole.AddedAgainst(plain) : 0;
                    Check(added >= 20, $"the red block is drawn on it ({added} pixels in colours the donkey's own part has nowhere; {whole?.Red} read as red)");
                    // the skin's [Base.Coat] recolour: the donkey's coat under the skin comes out blue, with the player's
                    // coat setting at its default
                    var grp = a.I.ColorsRig.TestMountGroup();
                    MountColorRegion coat = null;
                    foreach (MountColorRegion r in Colors.RegionList) if (r.Key == "Coat") coat = r;
                    int coatChanged = 0;
                    if (coat != null) foreach (string hex in coat.SourceColors) { Colors.TryHex(hex, out Color32 c); if (grp.ChangedSources.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b)) coatChanged++; }
                    Check(coat != null && coatChanged == coat.SourceColors.Count, $"the skin's [Base.Coat] recolours every coat colour under it ({coatChanged} of {coat?.SourceColors.Count})");
                    Check(whole != null && whole.Blue >= 150, $"the whole picture's coat is drawn blue ({whole?.Blue} blue pixels)");
                }
                // a walk frame stays a plain overlay: the part shows under it (the donkey walked by WalkFor)
                int plainSeen = 0, wholeSeen = 0, walkPics = 0;
                yield return WalkFor(a, part, 40, () =>
                {
                    if (ov == null || ov.sprite == null || !ov.sprite.name.Contains("walk")) return;
                    if (!a.I.LooksRig.TestShowing() && !l.Whole.Contains(ov.sprite)) plainSeen++; else wholeSeen++;
                }, n => walkPics = n);
                if (walkPics > 0) Check(plainSeen > 0 && wholeSeen == 0, $"a picture without the erase colour is a plain overlay, the body part drawn under it ({plainSeen} walk frames plain, {wholeSeen} whole)");
                else Fail("no walk picture showed in 40 frames of walking: the overlay check couldn't run");
            }
            a?.Remove();
            yield return Frames(2);
            Looks.Unregister("donkey/selftest.erase-test", owner);
            yield return Frames(2);
            try { System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        internal static IEnumerator SkinPackAutoReload()
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-watch");
            string packDir = TempSkinDir(tempRoot, Catalog.DonkeyId, "selftest.watch-test");
            string picture = System.IO.Path.Combine(packDir, "bdy.png");
            const string owner = Owner + ".watch";
            var red = new Color32(200, 40, 40, 255);
            var blue = new Color32(40, 60, 200, 255);
            void Save(Color32 c, int secondsLater)
            {
                var px = new Color32[96 * 96];
                for (int y = 30; y < 60; y++)
                {
                    for (int x = 30; x < 70; x++) px[y * 96 + x] = c;
                }
                var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                System.IO.File.WriteAllBytes(picture, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
                // a later stamp whatever the file system's clock resolution
                System.IO.File.SetLastWriteTimeUtc(picture, DateTime.UtcNow.AddSeconds(secondsLater));
            }
            bool Wears(Color32 c)
            {
                Looks.Loaded l = Looks.LoadedOwnColours("donkey/selftest.watch-test");
                return l != null && l.Colours.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b);
            }
            try
            {
                if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
                System.IO.Directory.CreateDirectory(packDir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(packDir, "skin.cfg"), "[Skin]\nformat = 1\nname = Watch Test\n");
                Save(red, 0);
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            bool was = Plugin.SkinPackAutoReload.Value;
            Plugin.SkinPackAutoReload.Value = false;
            try
            {
            Check(Looks.RegisterFolder(tempRoot, owner) == 1, "the pack is found");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "donkey/selftest.watch-test");
            yield return Until(() => a != null && a.IsReady && Wears(red) && !a.I.LooksRig.Building, 10f);
            if (!waited)
            {
                Fail("the red pack was never worn");
            }
            else
            {
                Save(blue, 5);
                yield return Until(() => Wears(blue), 4f);
                Check(!waited && Wears(red), "the switch off: a save changes nothing (still red after 4 s)");
                Plugin.SkinPackAutoReload.Value = true;
                Save(blue, 10); // a stamp after the one the watch takes as seen when switched on
                yield return Until(() => Wears(blue) && !a.I.LooksRig.Building, 8f);
                Check(waited, "the switch on: the blue picture is read again by itself and worn");
            }
            Plugin.SkinPackAutoReload.Value = was;
            a?.Remove();
            yield return Frames(2);
            Looks.Unregister("donkey/selftest.watch-test", owner);
            yield return Frames(2);
            }
            finally
            {
                // whatever stopped the scenario (a check that threw, the run stopped): nothing of it stays
                LeftOver(owner, "donkey/selftest.watch-test", tempRoot);
                Plugin.SkinPackAutoReload.Value = was;
            }
        }

        // A pack scenario's leftovers, from its finally: the run's donkeys and carts, the temp skin (and the mount its
        // [Mount] block declared, which goes with it), the temp folder. Each step on its own: one failing never keeps the
        // next from running. Done already on the way through: each is a no-op.
        internal static void LeftOver(string owner, string skinId, string tempRoot)
        {
            try { foreach (MountHandle h in Mounts.InstancesOf(Owner)) if (h.Exists) h.Remove(); } catch { }
            try { Looks.Unregister(skinId, owner); } catch { }
            try { if (tempRoot != null && System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        // A temp pack in the mounts tree (docs/skins.md "What a pack is"): <temp>\<folder>\gk2mounts\<mount>\ with the
        // mount's mount.cfg when the text declares one, and skins\<id>\ with skin.cfg and one picture (a skin's
        // standing frame). The text is written as one cfg, as the scenarios were before the mount folders: its [Mount]
        // and [Mount.*] sections go into mount.cfg under their plain names ([Mount.Tuning] -> [Tuning], [Mount.Stats] ->
        // [Stats]), each with the comments above it; the rest into skin.cfg. The mount folder is the [Mount] id, else the
        // skin's mount = line, else donkey. A mount portrait named in it gets the same picture in the mount folder.
        // A text that declares a mount writes its mount's own look, so its id must be "default" (Looks.DefaultSkinName:
        // a mount.cfg without skins\default\ is refused, 2026-10-24). Returns the temp root.
        internal static string WriteTempPack(string folder, string id, string cfg)
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), folder);
            if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
            SplitPackCfg(cfg, out string skinCfg, out string mountCfg, out string mountId, out string mountPortrait);
            if (mountCfg != null && !id.Equals(Looks.DefaultSkinName, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"a temp pack that declares the mount '{mountId}' writes its own look: its skin is '{Looks.DefaultSkinName}', not '{id}'");
            string skinDir = TempSkinDir(tempRoot, mountId, id);
            System.IO.Directory.CreateDirectory(skinDir);
            byte[] png = TempPicture();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(skinDir, "bdy_static_left.png"), png);
            System.IO.File.WriteAllText(System.IO.Path.Combine(skinDir, "skin.cfg"), skinCfg);
            if (mountCfg != null)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(TempMountDir(tempRoot, mountId), Looks.MountCfgName), mountCfg);
                if (mountPortrait != null) System.IO.File.WriteAllBytes(System.IO.Path.Combine(TempMountDir(tempRoot, mountId), mountPortrait), png);
            }
            return tempRoot;
        }

        // The API's own test pack's standing picture (assets\testmounts, embedded in this DLL): a 96x96 frame for the
        // temp packs, so they need no mod's art.
        internal const string TestPictureResource = "GK2DonkeyAPI.testmounts.donkey\\skins\\selftest.embedded-sound-test\\bdy_static_left.png";

        internal static byte[] TempPicture()
        {
            using (var s = typeof(Plugin).Assembly.GetManifestResourceStream(TestPictureResource))
            using (var ms = new System.IO.MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        // One more skin in a temp pack: gk2mounts\<mount>\skins\<id>\ with its skin.cfg and one picture (the API's test
        // picture). The skin's folder.
        internal static string AddTempSkin(string root, string mount, string id, string skinCfg)
        {
            string dir = TempSkinDir(root, mount, id);
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "bdy_static_left.png"), TempPicture());
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skin.cfg"), skinCfg);
            return dir;
        }

        // A 96x96 picture as PNG bytes for the temp packs: transparent, the rectangles filled (x, y from the bottom left,
        // width, height, colour), so a pixel read back says where it came from (the layering scenarios).
        internal static byte[] TempPng(params (int X, int Y, int W, int H, Color32 C)[] rects)
        {
            var px = new Color32[96 * 96];
            foreach (var r in rects)
            {
                for (int y = r.Y; y < r.Y + r.H; y++) for (int x = r.X; x < r.X + r.W; x++) px[y * 96 + x] = r.C;
            }
            var t = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            try
            {
                t.SetPixels32(px);
                t.Apply(false);
                return t.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.Destroy(t);
            }
        }

        // A temp mount folder's mount.cfg: gk2mounts\<mount>\mount.cfg as given.
        internal static void WriteTempMountCfg(string root, string mount, string mountCfg)
        {
            System.IO.Directory.CreateDirectory(TempMountDir(root, mount));
            System.IO.File.WriteAllText(TempMountCfg(root, mount), mountCfg);
        }

        internal static string TempMountDir(string root, string mount) => System.IO.Path.Combine(root, Looks.PackDirName, mount);

        internal static string TempSkinDir(string root, string mount, string id) => System.IO.Path.Combine(root, Looks.PackDirName, mount, Looks.SkinsDirName, id);

        // One cfg text split into a skin.cfg and a mount.cfg (WriteTempPack).
        internal static void SplitPackCfg(string cfg, out string skinCfg, out string mountCfg, out string mountId, out string mountPortrait)
        {
            var skin = new StringBuilder();
            var mount = new StringBuilder();
            var pending = new List<string>();
            StringBuilder to = skin;
            string section = "", skinMount = null;
            mountId = null;
            mountPortrait = null;
            foreach (string raw in cfg.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#") || line.Length == 0)
                {
                    pending.Add(raw);
                    continue;
                }
                if (line.StartsWith("["))
                {
                    section = line.Split('#')[0].Trim().Trim('[', ']').Trim();
                    bool ofMount = section.Equals("Mount", StringComparison.OrdinalIgnoreCase) || section.StartsWith("Mount.", StringComparison.OrdinalIgnoreCase);
                    to = ofMount ? mount : skin;
                    foreach (string p in pending) to.Append(p).Append('\n');
                    pending.Clear();
                    to.Append(ofMount && section.Length > 6 ? "[" + section.Substring(6) + "]" : line).Append('\n');
                    continue;
                }
                foreach (string p in pending) to.Append(p).Append('\n');
                pending.Clear();
                to.Append(raw).Append('\n');
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Split('#')[0].Trim();
                if (section.Equals("Mount", StringComparison.OrdinalIgnoreCase) && key.Equals("id", StringComparison.OrdinalIgnoreCase)) mountId = value;
                if (section.Equals("Mount", StringComparison.OrdinalIgnoreCase) && key.Equals("portrait", StringComparison.OrdinalIgnoreCase)) mountPortrait = value;
                if (section.Equals("Skin", StringComparison.OrdinalIgnoreCase) && key.Equals("mount", StringComparison.OrdinalIgnoreCase)) skinMount = value;
            }
            foreach (string p in pending) to.Append(p).Append('\n');
            skinCfg = skin.ToString();
            mountCfg = mount.Length > 0 ? mount.ToString() : null;
            mountId = mountId ?? skinMount ?? Catalog.DonkeyId;
        }

        // The skin.cfg / mount.cfg a temp pack wrote (WriteTempPack), found by the skin's id or the mount's.
        internal static string TempSkinCfg(string root, string id)
        {
            foreach (string m in System.IO.Directory.GetDirectories(System.IO.Path.Combine(root, Looks.PackDirName)))
            {
                string p = System.IO.Path.Combine(m, Looks.SkinsDirName, id, "skin.cfg");
                if (System.IO.File.Exists(p)) return p;
            }
            return null;
        }

        internal static string TempMountCfg(string root, string mount) => System.IO.Path.Combine(TempMountDir(root, mount), Looks.MountCfgName);

        // A pack's [Mount] section declares a mount (docs/mounts-design.md): listed with its skin, no cart, its tuning
        // (both styles' value on, the cart-only one off while standing) on the wearer owned by the skin system, the
        // hitch refused with the mount's name; the skin withdrawn: the mount goes with it (no other skin declares it), its tuning is off the donkey.
        internal static IEnumerator MountFromPack()
        {
            const string owner = Owner + ".kindpack";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-mount", Looks.DefaultSkinName,
                    "# a test pack\n[Skin]\nformat = 1\nname = Mount Test\nmount = selftest.test-beast\n\n[Colors]\nDark = 3A0A12\n\n# keep: the mount block's comment\n[Mount]\nid = selftest.test-beast\nname = Test Beast\nauthor = the self-test\nversion = 0.1\ncart = false\nfoals = false  # keep: no babies\nfoal_scale = 0.4\n\n# keep: both styles\n[Mount.Tuning]\nRiderOffsetPx = 3\n\n[Mount.Tuning.Cart]\n# keep: above the cart key\nSeatAdjustPx = 7\n");
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            try
            {
            int n = Looks.RegisterFolder(tempRoot, owner);
            Check(n == 1, $"one pack found ({n})");
            MountInfo k = Mounts.Catalog.Find("selftest.test-beast");
            Check(k != null && k.FromPack && !k.Cart && Has(k.Skins, "selftest.test-beast/default"), "the pack declared the mount: listed, no cart, with its skin");
            Check(k != null && Math.Abs(k.FoalScale - 0.4f) < 0.001f, $"the pack's foal_scale is the mount's ({k?.FoalScale})");
            Check(k != null && !k.IsBuiltIn && k.Author == "the self-test" && k.Version == "0.1", $"a loose pack's mount is not built in and carries the file's author and version ({k?.Author} {k?.Version})");
            MountSkinInfo s = Mounts.Looks.Find("selftest.test-beast/default");
            Check(s != null && s.Mount == "selftest.test-beast", "the skin names the mount");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "selftest.test-beast/default");
            yield return Until(() => a != null && a.IsReady && a.Mount == "selftest.test-beast", 8f);
            Check(waited, "a donkey wearing the skin is of the mount");
            yield return Frames(2);
            Check(a != null && a.Tuning.IsSet("RiderOffsetPx") && a.Tuning.Get<int>("RiderOffsetPx") == 3 && a.Tuning.SetBy("RiderOffsetPx") == Looks.TuningOwner, "the mount's tuning (both styles) is on the wearer, owned by the skin system");
            Check(a != null && !a.Tuning.IsSet("SeatAdjustPx"), "the cart-only value is not on it while it stands for riding");
            // precedence: a mod's own value on the donkey wins over the creature's; cleared, the creature's returns
            a?.Tuning.Set(Owner, "RiderOffsetPx", 11);
            yield return Frames(3);
            Check(a != null && a.Tuning.Get<int>("RiderOffsetPx") == 11 && a.Tuning.SetBy("RiderOffsetPx") == Owner, "a mod's value set on the donkey wins over the creature's and stays");
            a?.Tuning.Clear(Owner, "RiderOffsetPx");
            yield return Frames(3);
            Check(a != null && a.Tuning.Get<int>("RiderOffsetPx") == 3 && a.Tuning.SetBy("RiderOffsetPx") == Looks.TuningOwner, "cleared: the creature's value is back on it");
            MountHandle cart = Mounts.SpawnCart(Owner, near + new Vector3(2.5f, 0f, 0f));
            yield return Until(() => cart != null && cart.IsReady, 6f);
            MountResult hitch = a != null && cart != null ? Mounts.Hitch(a, cart) : MountResult.Refused("not spawned");
            Check(!hitch.Ok && hitch.Reason.IndexOf("test beast", StringComparison.OrdinalIgnoreCase) >= 0, "the hitch is refused by the mount: " + hitch.Reason);
            // the cart rule's two other doors (docs/mounts-parity-matrix.md 2.7): a spawn with a cart as a no-cart mount is
            // refused; a hitched donkey that becomes one leaves its cart standing
            MountHandle withCart = Mounts.Spawn(Owner, new MountSpawn { Position = near + new Vector3(0f, 0f, 2f), WithCart = true, Skin = "selftest.test-beast/default" });
            Check(withCart == null, "a spawn with a cart wearing a no-cart mount's skin is refused");
            withCart?.Remove();
            MountHandle b = Spawn(near + new Vector3(-2.5f, 0f, 0f), true);
            MountHandle cart2 = Mounts.SpawnCart(Owner, near + new Vector3(-2.5f, 0f, 2.5f));
            yield return Until(() => b != null && cart2 != null && b.IsReady && cart2.IsReady, 6f);
            MountResult hitch2 = b != null && cart2 != null ? Mounts.Hitch(b, cart2) : MountResult.Refused("not spawned");
            if (hitch2.Ok)
            {
                yield return Until(() => b.HasCart && b.IsReady, 6f);
                b.Tuning.Set(Owner, "Skin", "selftest.test-beast/default");
                yield return Until(() => !b.HasCart, 8f);
                Check(waited, "a hitched donkey that becomes a no-cart mount leaves its cart standing");
                int freeCarts = 0;
                foreach (MountHandle h in Mounts.Instances) if (h.Exists && !h.HasDonkey && h.Owner == Owner) freeCarts++;
                Check(freeCarts >= 1, $"the cart stands free where it was ({freeCarts} free cart(s) of the test's)");
            }
            else Warn("could not hitch a second donkey for the live cart rule: " + hitch2.Reason);
            b?.Remove();
            cart2?.Remove();
            // a mount without foals: a foal given the skin stays a donkey
            MountHandle foal = Mounts.SpawnFoal(Owner, a, 0.6f);
            foal?.Tuning.Set(Owner, "Skin", "selftest.test-beast/default");
            yield return Until(() => foal != null && foal.IsReady, 6f);
            yield return Frames(3);
            Check(foal != null && foal.Mount == MountCatalog.DonkeyId && foal.I.TestSkinWearing() == "", "a foal of a no-foals mount stays a donkey though given the skin");
            foal?.Remove();
            // the file round trip: a value set in code, saved, read back, with the file's own lines kept
            Looks.SkinDef def = Looks.Find("selftest.test-beast/default");
            Catalog.Entry kd = Catalog.Find("selftest.test-beast");
            if (def != null && kd != null)
            {
                (kd.TuningRide ?? (kd.TuningRide = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)))["LegAdjustYPx"] = 2;
                kd.FoalScale = 0.45f; // the Mounts tab's "Babies' size" slider does this
                string saveErr = Looks.SaveCfg(def) ?? Looks.SaveMountCfg(kd);
                Check(saveErr == null, "saved to the pack's skin.cfg and its mount's mount.cfg" + (saveErr != null ? " (" + saveErr + ")" : ""));
                string skinText = System.IO.File.ReadAllText(TempSkinCfg(tempRoot, Looks.DefaultSkinName));
                string text = System.IO.File.ReadAllText(TempMountCfg(tempRoot, "selftest.test-beast"));
                Check(skinText.Contains("# a test pack") && skinText.Contains("name = Mount Test") && skinText.IndexOf("[Mount", StringComparison.Ordinal) < 0, "the skin's file keeps its own lines, and no mount section (those are the mount.cfg's)");
                Check(text.Contains("[Tuning.Ride]") && text.Contains("LegAdjustYPx = 2") && text.Contains("RiderOffsetPx = 3"), "mount.cfg has the new value and keeps its own");
                Check(text.Contains("foal_scale = 0.45") && !text.Contains("foal_scale = 0.4\n"), "mount.cfg has the new foal_scale once");
                Check(skinText.Contains("[Colors]") && skinText.Contains("Dark = 3A0A12"), "the skin's file keeps its [Colors] section (the Mounts tab's auto-save must never drop a skin's colour regions)");
                // the comments of the rewritten mount sections stay where they were (docs/mounts-foundation-audit.md 3.4)
                int kept = 0;
                string lf = text.Replace("\r\n", "\n");
                foreach (string s2 in new[] { "# keep: the mount block's comment\n[Mount]", "foals = false  # keep: no babies", "# keep: both styles\n[Tuning]", "# keep: above the cart key\nSeatAdjustPx = 7" }) if (lf.Contains(s2)) kept++;
                Check(kept == 4, $"the four comments in mount.cfg are kept in place ({kept} of 4)");
                Check(text.Contains("author = the self-test") && text.Contains("version = 0.1"), "the saved [Mount] carries author and version");
                kd.TuningRide.Remove("LegAdjustYPx");
                kd.FoalScale = 0.6f;
                kd.Author = "nobody";
                string loadErr = Looks.ReloadMountCfg(kd);
                Catalog.Entry again = Catalog.Find("selftest.test-beast");
                Check(loadErr == null && again == kd && kd.TuningRide != null && kd.TuningRide.TryGetValue("LegAdjustYPx", out object back) && Convert.ToInt32(back) == 2, "reloaded: the same mount object has the file's value again" + (loadErr != null ? " (" + loadErr + ")" : ""));
                Check(Math.Abs(kd.FoalScale - 0.45f) < 0.001f && Math.Abs((Mounts.Catalog.Find("selftest.test-beast")?.FoalScale ?? 0f) - 0.45f) < 0.001f, $"reloaded: the file's foal_scale is on the mount and listed ({kd.FoalScale})");
                Check(kd.Author == "the self-test", $"reloaded: the file's author is back on the mount ({kd.Author})");
                // idempotence: a second save of an unchanged mount writes nothing (no comment or blank-line drift)
                string againErr = Looks.SaveMountCfg(kd, out bool changedAgain);
                Check(againErr == null && !changedAgain, "saved again with nothing changed: the file is left as it is" + (againErr != null ? " (" + againErr + ")" : changedAgain ? " (it was rewritten: the save drifts)" : ""));
                yield return Frames(3);
                Check(a != null && a.Tuning.IsSet("LegAdjustYPx") && a.Tuning.Get<int>("LegAdjustYPx") == 2, "the wearer has the reloaded value");
            }
            Check(Looks.Unregister("selftest.test-beast/default", owner), "the owner withdraws the skin");
            yield return Frames(3);
            Check(Mounts.Catalog.Find("selftest.test-beast") == null, "the mount goes with its last skin of the pack's owner (a mount with nothing to wear is not offered)");
            Check(a != null && a.Mount == MountCatalog.DonkeyId && !a.Tuning.IsSet("RiderOffsetPx"), "withdrawn: the donkey is a donkey again, the mount's tuning off it");
            Check(!Mounts.Catalog.Unregister(owner, "selftest.test-beast"), "nothing left for the owner to withdraw");
            a?.Remove();
            cart?.Remove();
            yield return Frames(2);
            }
            finally
            {
                // whatever stopped the scenario (a check that threw, the run stopped): nothing of it stays
                LeftOver(owner, "selftest.test-beast/default", tempRoot);
            }
        }

        internal sealed class CountingAbility : MountAbility
        {
            internal int AttachedCount, DetachedCount, MountedCount, DismountedCount, Ticks;
            internal MountStyle LastStyle;
            public override string Id => "count";
            public override bool TicksWhileStanding => true;
            public override void Attached(MountHandle mount) => AttachedCount++;
            public override void Detached(MountHandle mount) => DetachedCount++;
            public override void Mounted(MountHandle mount, MountStyle style) { MountedCount++; LastStyle = style; }
            public override void Dismounted(MountHandle mount) => DismountedCount++;
            public override void Tick(MountHandle mount, float dt) => Ticks++;
        }

        // An ability that throws on its first tick: the API switches it off for that instance with one warning and
        // the mount's other abilities go on (docs/mounts-design.md §4).
        internal sealed class ThrowingAbility : MountAbility
        {
            internal int Ticks, DetachedCount;
            public override string Id => "throws";
            public override bool TicksWhileStanding => true;
            public override void Tick(MountHandle mount, float dt) { Ticks++; throw new InvalidOperationException("the self-test's ability throws on purpose"); }
            public override void Detached(MountHandle mount) => DetachedCount++;
        }

        // A mount registered in code with an ability: Attached once when a donkey wearing one of its skins is ready,
        // ticking while it stands, Mounted on the ride and Dismounted after, Detached when it is removed; the mount
        // withdrawn with the handle.
        internal static IEnumerator MountFromCode()
        {
            const string owner = Owner + ".mount";
            var ability = new CountingAbility();
            var thrower = new ThrowingAbility();
            IDisposable reg;
            string tempRoot;
            try
            {
                reg = Mounts.Catalog.Register(owner, new MountDef { Id = "selftest.test-able", Name = "Test Able", Abilities = { ability, thrower }, TuningRide = new Dictionary<string, object> { ["SeatAdjustPx"] = 4f }, TuningCart = new Dictionary<string, object> { ["CargoBedDepth"] = -0.31f } }); // a float for an int key: converted
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-able", "selftest.able-test", "[Skin]\nformat = 1\nname = Able Test\nmount = selftest.test-able\n");
            }
            catch (Exception e)
            {
                Warn("the mount or the temp pack could not be made: " + e.Message);
                yield break;
            }
            Looks.RegisterFolder(tempRoot, owner);
            Info("the next \"ability 'throws' of 'selftest.test-able' threw\" warning is this test's own: it checks that a throwing ability is switched off");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "selftest.test-able/selftest.able-test");
            yield return Until(() => a != null && a.IsReady && a.Mount == "selftest.test-able", 8f);
            Check(waited, "a donkey wearing the skin is of the mount");
            yield return Frames(5);
            Check(ability.AttachedCount == 1 && ability.Ticks > 0, $"attached once, ticking while standing ({ability.AttachedCount}, {ability.Ticks} ticks)");
            Check(thrower.Ticks == 1 && ability.Ticks > 1, $"an ability that throws is off for this one after its first tick, the other goes on ({thrower.Ticks} tick of the thrower, {ability.Ticks} of the other)");
            Check(a != null && a.Tuning.IsSet("SeatAdjustPx") && a.Tuning.Get<int>("SeatAdjustPx") == 4, "the mount's riding tuning is on it (a float given, the key's int taken)");
            MountResult ride = a != null ? a.Ride(showMessage: false) : MountResult.Refused("not spawned");
            Check(ride.Ok, $"ridden{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
            if (ride.Ok)
            {
                yield return Until(() => Mounts.IsMounted && ability.MountedCount == 1, 5f);
                Check(waited && ability.LastStyle == MountStyle.Ride, "Mounted once, riding");
                int t = ability.Ticks;
                yield return Frames(3);
                Check(ability.Ticks > t, "ticking while ridden");
                Mounts.Dismount("self-test");
                yield return Until(() => !Mounts.IsMounted && ability.DismountedCount == 1, 5f);
                Check(waited, "Dismounted once");
            }
            // the mount's cart-style value: on the donkey while hitched, on neither after the unhitch (a cart key, which
            // the unhitch moves between instances; a creature's never)
            MountHandle cart = Mounts.SpawnCart(Owner, near + new Vector3(2.5f, 0f, 0f));
            yield return Until(() => cart != null && cart.IsReady, 6f);
            MountResult hitch = a != null && cart != null ? Mounts.Hitch(a, cart) : MountResult.Refused("not spawned");
            Check(hitch.Ok, "hitched a cart: " + (hitch.Ok ? "ok" : hitch.Reason));
            if (hitch.Ok)
            {
                yield return Frames(2);
                Check(a != null && !a.Tuning.IsSet("SeatAdjustPx"), "hitched: the riding-only value is off it by the next tick (the re-merge sees the cart style)");
                yield return Until(() => a != null && a.HasCart && a.IsReady, 6f);
                yield return Frames(3);
                Check(a != null && a.Tuning.IsSet("CargoBedDepth") && a.Tuning.SetBy("CargoBedDepth") == Looks.TuningOwner, "hitched: the mount's cart-style value is on the donkey");
                Check(a != null && !a.Tuning.IsSet("SeatAdjustPx"), "hitched: the riding-only value is off it");
                MountHandle left = Mounts.Unhitch(a);
                yield return Frames(3);
                Check(left != null && !left.Tuning.IsSet("CargoBedDepth"), "unhitched: the creature's value did not move onto the free cart");
                Check(a != null && !a.Tuning.IsSet("CargoBedDepth") && a.Tuning.IsSet("SeatAdjustPx"), "unhitched: the donkey is back on its riding values");
                left?.Remove();
            }
            else
            {
                cart?.Remove();
            }
            a?.Remove();
            yield return Frames(3);
            Check(ability.DetachedCount == 1, $"Detached once on removal ({ability.DetachedCount})");
            Check(thrower.DetachedCount == 0, $"the switched-off ability gets no more calls, not even Detached ({thrower.DetachedCount})");
            Looks.Unregister("selftest.test-able/selftest.able-test", owner);
            reg.Dispose();
            Check(Mounts.Catalog.Find("selftest.test-able") == null, "the mount is withdrawn with its handle");
            try { System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        // A value on an instance belongs to the owner that set it (docs/mounts-foundation-2.md 2.1): another mod's Set
        // and Clear are refused (TrySet says so) and the mount's own value is anyone's to replace; the session layer
        // the same; a mount may pin its speed and sounds, never a behaviour switch; an ability's values are set under
        // the owner the API gave it and are cleared when the mount leaves, whether the ability cleared them or not; a
        // page row that names its tuning key is disabled while the companion's chosen mount pins that key.
        internal static IEnumerator TuningOwnership()
        {
            const string one = Owner + ".one", two = Owner + ".two", mountOwner = Owner + ".own";
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle m = Spawn(near, true);
            if (m == null) { Fail("could not spawn"); yield break; }
            m.Tuning.Set(one, "SpeedMultiplier", 1.7f);
            Check(m.Tuning.SetBy("SpeedMultiplier") == one && Math.Abs(m.Tuning.Get<float>("SpeedMultiplier") - 1.7f) < 0.001f, "mod one sets the speed on its donkey");
            float got = m.Tuning.Set(two, "SpeedMultiplier", 3f);
            Check(m.Tuning.SetBy("SpeedMultiplier") == one && Math.Abs(got - 1.7f) < 0.001f, $"mod two's Set is refused: the value stays mod one's and Set returns it ({got})");
            Check(!m.Tuning.TrySet(two, "SpeedMultiplier", 3f, out float applied) && Math.Abs(applied - 1.7f) < 0.001f, "TrySet says it was refused");
            Check(!m.Tuning.Clear(two, "SpeedMultiplier") && m.Tuning.IsSet("SpeedMultiplier"), "mod two's Clear is refused");
            Check(m.Tuning.Clear(one, "SpeedMultiplier") && !m.Tuning.IsSet("SpeedMultiplier"), "mod one's Clear clears it");
            MountTuning ride = Mounts.Tuning(MountStyle.Ride);
            string heldBy = ride.OverriddenBy("HeadSettle");
            if (heldBy != null)
            {
                Warn($"HeadSettle is held by {heldBy} this session (the dev panel or a mod): the session-layer checks skipped");
            }
            else
            {
                ride.Set(one, "HeadSettle", 0.3f);
                Check(!ride.TrySet(two, "HeadSettle", 0.9f, out float s2) && Math.Abs(s2 - 0.3f) < 0.001f && ride.OverriddenBy("HeadSettle") == one, "the session layer: another owner's Set is refused the same way");
                Check(!ride.Clear(two, "HeadSettle") && ride.IsOverridden("HeadSettle"), "and its Clear");
            }
            Check(ride.Clear(one, "HeadSettle") && !ride.IsOverridden("HeadSettle"), "the owner's Clear clears it");

            // a mount of its own: it pins a mount key and a sound key, tries a behaviour switch; its ability pins under its owner
            var ability = new PinningAbility();
            IDisposable reg = null;
            string tempRoot = null;
            try
            {
                // its speed is its word (Fast: the API's 2.25); the SpeedMultiplier in its tuning is refused (2026-10-27)
                reg = Mounts.Catalog.Register(mountOwner, new MountDef { Id = "selftest.own-beast", Name = "Own Beast", Abilities = { ability }, Speed = SpeedBand.Fast,
                    Tuning = new Dictionary<string, object> { ["SpeedMultiplier"] = 1.1f, ["BrayVolume"] = 0.2f, ["IdleShuffle"] = false } });
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-own", "selftest.own-test", "[Skin]\nformat = 1\nname = Own Test\nmount = selftest.own-beast\n");
            }
            catch (Exception e)
            {
                Warn("the mount or the temp pack could not be made: " + e.Message);
                m.Remove();
                yield break;
            }
            try
            {
            Looks.RegisterFolder(tempRoot, mountOwner);
            Info("the next \"mount 'selftest.own-beast' sets SpeedMultiplier in its tuning\" and \"wants to set IdleShuffle\" warnings are this test's own: a mount's speed is its word, and it may not pin a behaviour switch");
            Check(ability.Owner == mountOwner + ".ability.selftest.own-beast", $"the ability's owner is given by the API ({ability.Owner})");
            m.Tuning.Set(Owner, "Skin", "selftest.own-beast/selftest.own-test");
            yield return Until(() => m.IsReady && m.Mount == "selftest.own-beast" && m.Tuning.IsSet("SpeedMultiplier"), 8f);
            float fast = Catalog.SpeedOfBand(SpeedBand.Fast, MountStyle.Ride);
            Check(waited && m.Tuning.SetBy("SpeedMultiplier") == Looks.TuningOwner && Math.Abs(m.Tuning.Get<float>("SpeedMultiplier") - fast) < 0.001f, $"the mount's word sets its speed, Fast's {fast}; the 1.1 in its tuning refused ({(m.Tuning.IsSet("SpeedMultiplier") ? m.Tuning.Get<float>("SpeedMultiplier").ToString("0.##") : "not set")})");
            Check(m.Tuning.IsSet("BrayVolume") && m.Tuning.SetBy("BrayVolume") == Looks.TuningOwner, "and its bray volume (a sound key)");
            Check(!m.Tuning.IsSet("IdleShuffle"), "its IdleShuffle is refused: a behaviour switch is the player's");
            m.Tuning.Set(one, "SpeedMultiplier", 2.5f);
            Check(m.Tuning.SetBy("SpeedMultiplier") == one && Math.Abs(m.Tuning.Get<float>("SpeedMultiplier") - 2.5f) < 0.001f, "a mod's value replaces the mount's");
            m.Tuning.Clear(one, "SpeedMultiplier");
            yield return Frames(3);
            Check(m.Tuning.SetBy("SpeedMultiplier") == Looks.TuningOwner, "cleared: the mount's value is back");
            m.Tuning.Set(one, "HarnessColorize", "C040FF"); // a mod's (the player's mod's) look choice, which the ability will try to take
            MountResult rideRes = m.Ride(showMessage: false);
            Check(rideRes.Ok, $"ridden{(rideRes.Ok ? "" : " (" + rideRes.Reason + ")")}");
            if (rideRes.Ok)
            {
                yield return Until(() => Mounts.IsMounted && ability.MountedCount == 1, 5f);
                Check(waited, $"Mounted once ({ability.MountedCount})");
                yield return Frames(2);
                Check(m.Tuning.SetBy("HandsSwayPx") == ability.Owner, "ridden: the ability pins a mount key under its own owner");
                Check(m.Tuning.SetBy("HarnessColorize") == one && m.Tuning.Get<string>("HarnessColorize") == "C040FF", "the ability's Set of the harness colour, which a mod holds, is refused");
                Mounts.Dismount("self-test");
                yield return Until(() => !Mounts.IsMounted, 5f);
                Check(m.Tuning.IsSet("HandsSwayPx"), "dismounted: the ability left its value behind on purpose");
            }
            m.Tuning.Set(Owner, "Skin", "");
            yield return Until(() => m.Mount == MountCatalog.DonkeyId, 8f);
            yield return Frames(3);
            Check(waited && !m.Tuning.IsSet("HandsSwayPx"), "the mount gone: the ability's value is cleared by the API");
            Check(m.Tuning.SetBy("HarnessColorize") == one, "the mod's own value is untouched by that");
            m.Tuning.Clear(one, "HarnessColorize");

            // a page row that names its tuning key: disabled while the chosen mount pins the key
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-pin.cfg");
            try
            {
                try { System.IO.File.Delete(path); } catch { }
                var cfg = new ConfigFile(path, false) { SaveOnConfigSet = false };
                MountCompanion me = Mounts.Companion(Owner + ".pin");
                var page = new MountSettingsPage(Owner + ".pin", cfg, "Pin test", "test", "", null);
                page.Toggle("Behaviour", "Fast", true, "Fast", "");
                page.TuningKey("Behaviour", "Fast", "SpeedMultiplier", me);
                MountSettingsPage.Row row = page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == "Fast");
                me.Skin = "";
                Check(row != null && row.Enabled != null && row.Enabled() && MountSettingsPage.PinnedBy(me, "SpeedMultiplier") == null, "on the donkey the speed row is live");
                me.Skin = "selftest.own-beast/selftest.own-test";
                Check(row != null && row.Enabled != null && !row.Enabled() && MountSettingsPage.PinnedBy(me, "SpeedMultiplier") == "Own Beast", $"on a mount that pins the speed the row is disabled (pinned by \"{MountSettingsPage.PinnedBy(me, "SpeedMultiplier")}\")");
                me.Skin = "";
            }
            finally
            {
                try { System.IO.File.Delete(path); } catch { }
            }
            m.Remove();
            yield return Frames(3);
            }
            finally
            {
                // whatever happened above: nothing of this scenario stays (the mount, its pack, the donkey)
                try { if (m != null && m.Exists) m.Remove(); } catch { }
                Looks.Unregister("selftest.own-beast/selftest.own-test", mountOwner);
                reg?.Dispose();
                try { if (tempRoot != null) System.IO.Directory.Delete(tempRoot, true); } catch { }
            }
        }

        // Ids and a mount's facts (docs/mounts-foundation-2.md 6.1; the mount folders and one own look per mount,
        // 2026-10-24): one mount.cfg defines a mount; another pack's mount.cfg of the same id meets the version rule (a
        // newer one replaces the facts in place and its skins\default\ the first pack's own look, the first pack's other
        // skins keep the mount; an older one is refused with its skins); a mount folder without mount.cfg adds its skins
        // to the mount; the same skin from two places: the higher version wins, versions compared as numbers.
        internal static IEnumerator IdRules()
        {
            const string o1 = Owner + ".ids1", o2 = Owner + ".ids2", o4 = Owner + ".ids4";
            const string beast = "selftest.idbeast", own = beast + "/default", joiner = beast + "/selftest.joiner-test", oldLook = beast + "/old-beast-look";
            string r1 = null, r2 = null, r3 = null, r4 = null;
            try
            {
                r1 = WriteTempPack("gk2donkeyapi-selftest-ids1", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Joiner's Beast\n\n[Mount]\nid = selftest.idbeast\nname = Joiner's Beast\nauthor = the self-test\nversion = 0.1\ncart = false\nfoals = true\n");
                AddTempSkin(r1, beast, "selftest.joiner-test", "[Skin]\nformat = 1\nname = Joiner\nversion = 1.0\n");
                Looks.RegisterFolder(r1, o1);
                MountInfo k = Mounts.Catalog.Find(beast);
                Check(k != null && !k.Cart && k.Name == "Joiner's Beast" && Mounts.Looks.Find(own) != null && Mounts.Looks.Find(joiner) != null, "a mount pack declares the mount (version 0.1, cart off) with its own look and a second skin");
                r2 = WriteTempPack("gk2donkeyapi-selftest-ids2", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Id Beast\nversion = 1.0\n\n[Mount]\nid = selftest.idbeast\nname = Id Beast\nauthor = the self-test\nversion = 1.0\ncart = true\nfoals = true\n");
                Info("the next \"replaces the one of\" and \"replaces the one from\" warnings are this test's own: a newer mount.cfg replaces the registered mount's facts, its skins\\default\\ the mount's own look");
                Looks.RegisterFolder(r2, o2);
                k = Mounts.Catalog.Find(beast);
                Check(k != null && k.Cart && k.Name == "Id Beast" && k.Owner == o2, $"a newer mount.cfg (1.0) of the same id takes over: cart on, its name, its owner ({k?.Name}, {k?.Owner})");
                Check(Mounts.Looks.Find(joiner) == null, "the first pack's other skin is withdrawn with its pack: never the old pack's pictures on the new facts (docs/multi-mod-loading.md 4.1)");
                Looks.SkinDef ownDef = Looks.Find(own);
                Check(Catalog.Find(beast)?.Pack?.Source != null && Catalog.Find(beast).Pack.Source.StartsWith(r2, StringComparison.OrdinalIgnoreCase) && Looks.DefaultSkinOf(Catalog.Find(beast)) == own && ownDef != null && ownDef.Owner == o2 && ownDef.Name == "Id Beast",
                    $"the mount's files and its own look are the newer pack's ({Looks.DefaultSkinOf(Catalog.Find(beast))}, \"{ownDef?.Name}\" of {ownDef?.Owner})");
                r4 = WriteTempPack("gk2donkeyapi-selftest-ids4", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Old Beast\nversion = 0.5\n\n[Mount]\nid = selftest.idbeast\nname = Old Beast\nauthor = the self-test\nversion = 0.5\ncart = false\nfoals = true\n");
                AddTempSkin(r4, beast, "old-beast-look", "[Skin]\nformat = 1\nname = Old Look\n");
                Info("the next \"could not be registered\" warning is this test's own: an older mount.cfg of a registered mount is refused");
                Looks.RegisterFolder(r4, o4);
                k = Mounts.Catalog.Find(beast);
                Check(k != null && k.Name == "Id Beast" && k.Cart, $"an older mount.cfg (0.5) changes nothing ({k?.Name})");
                Check(Mounts.Looks.Find(oldLook) == null && Looks.Find(own)?.Owner == o2, "and the skins under it are not registered, its own look neither (their mount pack was refused)");
                r3 = WriteTempPack("gk2donkeyapi-selftest-ids3", "selftest.joiner-test", "[Skin]\nformat = 1\nname = Joiner\nversion = 1.0\nmount = selftest.idbeast\n");
                int n = Looks.RegisterFolder(r3, o1 + ".old");
                Check(n == 1 && Mounts.Looks.Find(joiner)?.Mount == beast, $"a mount folder without mount.cfg: its skin joins the mount ({n} registered)");
                System.IO.File.WriteAllText(TempSkinCfg(r3, "selftest.joiner-test"), "[Skin]\nformat = 1\nname = Joiner Older\nversion = 0.9\nmount = selftest.idbeast\n");
                Info("the next \"could not be registered\" warning is this test's own: an older copy of a registered skin is refused");
                n = Looks.RegisterFolder(r3, o1 + ".older");
                Check(n == 0 && Mounts.Looks.Find(joiner)?.Name == "Joiner", $"an older copy (0.9) of a registered skin (1.0) is refused ({n} registered)");
                System.IO.File.WriteAllText(TempSkinCfg(r3, "selftest.joiner-test"), "[Skin]\nformat = 1\nname = Joiner Newer\nversion = 2.0\nmount = selftest.idbeast\n");
                Info("the next \"replaces the one from\" warning is this test's own: a newer copy replaces the registered skin");
                n = Looks.RegisterFolder(r3, o1 + ".newer");
                MountSkinInfo sk = Mounts.Looks.Find(joiner);
                Check(n == 1 && sk != null && sk.Id == joiner && sk.Name == "Joiner Newer" && sk.Owner == o1 + ".newer" && sk.Mount == beast, $"a newer copy (2.0) replaces it, from a mount folder without mount.cfg: it joins the mount (\"{sk?.Name}\", {sk?.Owner}, {sk?.Mount})");
                Check(Looks.CompareVersions("1.10", "1.9") > 0 && Looks.CompareVersions("1.0", "1") == 0 && Looks.CompareVersions(null, "0.1") < 0, "versions compare as numbers (1.10 > 1.9; 1.0 = 1; none < 0.1)");
            }
            finally
            {
                foreach (string id in new[] { joiner, own, oldLook })
                {
                    Looks.SkinDef d = Looks.Find(id);
                    if (d != null && d.Id == id) Looks.Unregister(id, d.Owner);
                }
                Catalog.Entry kd = Catalog.Find(beast);
                if (kd != null) Looks.UnregisterMount(beast, kd.Owner);
                foreach (string r in new[] { r1, r2, r3, r4 }) { try { if (r != null) System.IO.Directory.Delete(r, true); } catch { } }
            }
            yield break;
        }

        // A code mount against another mod's mount of the same id (the user, 2026-10-24: "one version rule, no throw";
        // Catalog.Register, Catalog.Declare): another owner's older or equal one is left out (Catalog.Register false, a
        // warning, the first stays, the loser's handle does nothing), a newer one replaces it; a newer pack mount.cfg
        // replaces a code mount, an older one is refused with its skins. A code mount's clips are banked under its mod
        // ("gk2.mount.<owner>/<id>.<key>"), so two mods' mounts of one id never share an entry, and a mount that lost
        // its id takes its entries with it.
        internal static IEnumerator IdRulesCode()
        {
            const string oA = Owner + ".clashA", oB = Owner + ".clashB", oC = Owner + ".clashC", oD = Owner + ".clashD", oP = Owner + ".clashP";
            const string id = "selftest.clash", id2 = "selftest.clash2", id3 = "selftest.clash3";
            IDisposable hA = null, hC = null, hD = null, hA2 = null, hA3 = null, skinA = null;
            MountHandle a = null;
            string r2 = null, r3 = null;
            AudioClip clipA = null, clipD = null;
            try
            {
                clipA = SoundBank.FromWav(SoundBank.TestTone(0.2f, 300f), "selftest-clash-a");
                clipD = SoundBank.FromWav(SoundBank.TestTone(0.2f, 500f), "selftest-clash-d");
                hA = Mounts.Catalog.Register(oA, new MountDef { Id = id, Name = "Clash A", Version = "1.0", Sounds = new Dictionary<string, MountSound> { ["voice"] = MountSound.Own(clipA) } });
                skinA = Mounts.Looks.Register(oA, new MountSkinDef { Id = Looks.DefaultSkinName, Name = "Clash Look", Mount = id, Source = new NoPictures() });
                Check(Catalog.Find(id)?.Owner == oA, "a code mount registered (version 1.0)");
                // its clip banked under its mod
                a = Spawn(Spot(home, 4f) ?? home, true);
                a?.Tuning.Set(Owner, "Skin", id + "/" + Looks.DefaultSkinName);
                yield return Until(() => a != null && a.IsReady && a.Mount == id, 8f);
                Check(waited, "a donkey wears its look and is the mount");
                string bankA = null;
                if (a != null)
                {
                    a.I.SoundsRig.Invalidate();
                    bankA = a.I.SoundsRig.Resolve("voice")?.BankId;
                }
                Check(bankA != null && bankA.StartsWith("gk2.mount." + oA + "/" + id + ".", StringComparison.Ordinal), $"its voice clip is banked under its mod: gk2.mount.<owner>/<id>.<key> ({bankA ?? "none"})");

                // code against code: equal and older are left out, no throw, the first stays
                Info("the next two \"is left out\" warnings are this test's own: another mod's code mount of the same id, not newer (equal, then older)");
                bool equal = Catalog.Register(new Catalog.Entry { Id = id, Name = "Clash B", Version = "1.0", Owner = oB });
                Check(!equal && Catalog.Find(id)?.Owner == oA && Catalog.Find(id)?.Name == "Clash A", $"an equal version (1.0) from another mod: Register false, the first stays ({Catalog.Find(id)?.Owner})");
                hC = Mounts.Catalog.Register(oC, new MountDef { Id = id, Name = "Clash C", Version = "0.5" });
                Check(hC != null && Catalog.Find(id)?.Owner == oA, "an older one (0.5) through the API: no throw, a handle, the first stays");
                hC.Dispose();
                hC = null;
                Check(Catalog.Find(id)?.Owner == oA && SoundBank.Find(bankA) != null, "the loser's handle disposed: nothing happens to the mount or its clips");

                // a newer one replaces it; the old one's clips go with it
                Info("the next \"replaces the one from\" warning is this test's own: a newer code mount of the same id replaces the first");
                hD = Mounts.Catalog.Register(oD, new MountDef { Id = id, Name = "Clash D", Version = "2.0", Sounds = new Dictionary<string, MountSound> { ["voice"] = MountSound.Own(clipD) } });
                Check(Catalog.Find(id)?.Owner == oD && Catalog.Find(id)?.Name == "Clash D", $"a newer one (2.0) replaces it ({Catalog.Find(id)?.Owner})");
                Check(bankA == null || SoundBank.Find(bankA) == null, "the replaced mount's clip entry is gone with it");
                yield return Frames(2);
                string bankD = null;
                if (a != null)
                {
                    a.I.SoundsRig.Invalidate();
                    bankD = a.I.SoundsRig.Resolve("voice")?.BankId;
                }
                Check(a != null && a.Mount == id && bankD != null && bankD.StartsWith("gk2.mount." + oD + "/" + id + ".", StringComparison.Ordinal) && bankD != bankA, $"the wearer is the new mount, its voice the new mod's own entry ({bankD ?? "none"})");
                hA.Dispose();
                hA = null;
                Check(Catalog.Find(id)?.Owner == oD, "the first mod's handle disposed: the newer mount stays (not its own any more)");
                a?.Remove();
                a = null;
                skinA.Dispose();
                skinA = null;
                hD.Dispose();
                hD = null;
                Check(Catalog.Find(id) == null && (bankD == null || SoundBank.Find(bankD) == null), "the newer one's handle disposed: the mount and its clip entry are gone");

                // code against a pack: a newer mount.cfg replaces the code mount, an older one is refused with its skins
                hA2 = Mounts.Catalog.Register(oA, new MountDef { Id = id2, Name = "Clash Two", Version = "1.0" });
                r2 = WriteTempPack("gk2donkeyapi-selftest-clash2", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Clash Two Pack\n\n[Mount]\nid = " + id2 + "\nname = Clash Two Pack\nauthor = the self-test\nversion = 2.0\ncart = true\nfoals = true\n");
                Info("the next \"replaces the one from\" warning is this test's own: a newer pack mount.cfg replaces a code mount");
                int n2 = Looks.RegisterFolder(r2, oP);
                Catalog.Entry k2 = Catalog.Find(id2);
                Check(n2 == 1 && k2 != null && k2.FromPack && k2.Owner == oP && k2.Name == "Clash Two Pack" && k2.Abilities.Length == 0 && Looks.Find(id2 + "/default") != null, $"a newer pack (2.0) replaces the code mount (1.0) with its own look ({n2}; {k2?.Owner}, from a pack {k2?.FromPack})");
                hA2.Dispose();
                hA2 = null;
                Check(Catalog.Find(id2)?.Owner == oP, "the code mount's handle disposed: the pack's mount stays");
                hA3 = Mounts.Catalog.Register(oA, new MountDef { Id = id3, Name = "Clash Three", Version = "1.0" });
                r3 = WriteTempPack("gk2donkeyapi-selftest-clash3", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Clash Three Pack\n\n[Mount]\nid = " + id3 + "\nname = Clash Three Pack\nauthor = the self-test\nversion = 0.5\ncart = true\nfoals = true\n");
                Info("the next \"could not be registered\" warning is this test's own: an older pack mount.cfg against a code mount is refused");
                int n3 = Looks.RegisterFolder(r3, oP);
                Check(n3 == 0 && Catalog.Find(id3)?.Owner == oA && !Catalog.Find(id3).FromPack && Looks.Find(id3 + "/default") == null, $"an older pack (0.5) is refused, its skins left out; the code mount stays ({n3}; {Catalog.Find(id3)?.Owner})");
            }
            finally
            {
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                foreach (IDisposable h in new[] { skinA, hC, hD, hA, hA2, hA3 }) { try { h?.Dispose(); } catch { } }
                try { Looks.Unregister(id2 + "/default", oP); } catch { }
                Catalog.Entry k = Catalog.Find(id2);
                if (k != null) { try { Looks.UnregisterMount(id2, k.Owner); } catch { } }
                foreach (string r in new[] { r2, r3 }) { try { if (r != null) System.IO.Directory.Delete(r, true); } catch { } }
                if (clipA != null) UnityEngine.Object.Destroy(clipA);
                if (clipD != null) UnityEngine.Object.Destroy(clipD);
            }
        }

        // One own look per mount (2026-10-24): a mount's default is its pack's skins\default\, the skin a new one of it
        // wears. A mount.cfg without one is refused with its skins; a folder without mount.cfg (skins added to another
        // pack's mount) may not bring a default, its other skins join; a skin is named within its mount, so two mounts'
        // "fire" are two skins and the bare name finds neither; a mount id needs its mod's prefix; a mount.cfg's
        // default_skin line is reported and changes nothing. A skin added to a mount another mod owns is named
        // "<mod>.<name>" (2026-10-24): a joiner folder's skin without the prefix is left out, a code skin on another
        // owner's mount and a donkey recipe without it are refused (thrown); on its own mount a code skin takes any name.
        internal static IEnumerator DefaultSkinRules()
        {
            const string o1 = Owner + ".defskin", o2 = Owner + ".defskin2", o3 = Owner + ".defskin3";
            string r1 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-defskin1");
            string r2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-defskin2");
            string[] mounts = { "selftest.hasdef", "selftest.a", "selftest.b", "selftest.nodef", "nodot" };
            string MountCfg(string id, string extra = "") => "[Mount]\nid = " + id + "\nname = " + id + "\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n" + extra;
            string SkinCfg(string name) => "[Skin]\nformat = 1\nname = " + name + "\n";
            try
            {
                foreach (string r in new[] { r1, r2 }) if (System.IO.Directory.Exists(r)) System.IO.Directory.Delete(r, true);
                // (a) a mount.cfg whose skins\ has no default
                WriteTempMountCfg(r1, "selftest.nodef", MountCfg("selftest.nodef"));
                AddTempSkin(r1, "selftest.nodef", "other", SkinCfg("No Default Other"));
                // (b, e, f) a whole pack: its own look and a second skin; a default_skin line that is no longer read
                WriteTempMountCfg(r1, "selftest.hasdef", MountCfg("selftest.hasdef", "default_skin = fire\n"));
                AddTempSkin(r1, "selftest.hasdef", Looks.DefaultSkinName, SkinCfg("Has Default"));
                AddTempSkin(r1, "selftest.hasdef", "fire", SkinCfg("Has Default Fire"));
                // (c) two mounts with a skin of one name
                foreach (string m in new[] { "selftest.a", "selftest.b" })
                {
                    WriteTempMountCfg(r1, m, MountCfg(m));
                    AddTempSkin(r1, m, Looks.DefaultSkinName, SkinCfg(m + " Default"));
                    AddTempSkin(r1, m, "fire", SkinCfg(m + " Fire"));
                }
                // (d) a mount id without its mod's prefix (its own look there, so the id is the only fault)
                WriteTempMountCfg(r1, "nodot", MountCfg("nodot"));
                AddTempSkin(r1, "nodot", Looks.DefaultSkinName, SkinCfg("No Dot"));
                // (b) another mod's folder of selftest.hasdef, no mount.cfg: a default of its own (left out) and a skin (joins)
                AddTempSkin(r2, "selftest.hasdef", Looks.DefaultSkinName, SkinCfg("Joiner's Default"));
                AddTempSkin(r2, "selftest.hasdef", "selftest.frost", SkinCfg("Joiner's Frost"));
                // (g) and one without its mod's prefix: a skin added to a mount another mod owns is named "<mod>.<name>"
                AddTempSkin(r2, "selftest.hasdef", "frost-plain", SkinCfg("Joiner's Plain Frost"));
            }
            catch (Exception e)
            {
                Warn("the temp packs could not be written: " + e.Message);
                yield break;
            }
            try
            {
                Info("the next two warnings are this test's own: the mount 'nodot' has no mod prefix (refused), the mount pack 'selftest.nodef' has no skins\\default\\ (left out with its skins)");
                int n1 = Looks.RegisterFolder(r1, o1);
                Check(n1 == 6, $"six skins registered from the first folder: two of selftest.hasdef, two each of selftest.a and selftest.b ({n1})");
                // (a)
                Check(Catalog.Find("selftest.nodef") == null && Looks.Find("selftest.nodef/other") == null, "a mount.cfg without skins\\default\\ is refused: no mount, and its other skin is left out with it");
                // (d)
                Check(Catalog.Find("nodot") == null && Looks.Find("nodot/default") == null, "a mount id without its mod's prefix ('nodot') is refused, with its skins");
                // (e, f)
                Catalog.Entry has = Catalog.Find("selftest.hasdef");
                MountInfo hasInfo = Mounts.Catalog.Find("selftest.hasdef");
                Check(has != null && Looks.DefaultSkinOf(has) == "selftest.hasdef/default" && hasInfo != null && hasInfo.Skins.Count == 2 && hasInfo.Skins[0] == "selftest.hasdef/default",
                    $"the mount's own look is its skins\\default\\: {(has != null ? Looks.DefaultSkinOf(has) : "no mount")}, first in its list ({(hasInfo != null ? string.Join(", ", hasInfo.Skins) : "-")})");
                Check(has?.Pack != null && has.Pack.Report.Contains("default_skin ignored"), $"a default_skin line is reported as ignored and changes nothing ({has?.Pack?.Report})");
                Check(Looks.Find("selftest.hasdef")?.Id == "selftest.hasdef/default", "a bare mount id finds that mount's own look");
                // (c)
                Looks.SkinDef fa = Looks.Find("selftest.a/fire"), fb = Looks.Find("selftest.b/fire");
                Check(fa != null && fb != null && fa != fb && fa.LocalId == "fire" && fb.LocalId == "fire" && fa.Mount == Catalog.Find("selftest.a") && fb.Mount == Catalog.Find("selftest.b"), "two mounts' skins of one name are two skins, each named within its mount (selftest.a/fire, selftest.b/fire)");
                Check(Looks.Find("fire") == null, "the bare name two mounts share finds neither (say which)");
                // (b)
                Info("the next two warnings are this test's own: a folder without mount.cfg may not bring a mount's own look, and its skin 'frost-plain' has no mod prefix (a skin added to a mount another mod owns is named '<mod>.<name>')");
                int n2 = Looks.RegisterFolder(r2, o2);
                Looks.SkinDef hd = Looks.Find("selftest.hasdef/default"), frost = Looks.Find("selftest.hasdef/selftest.frost");
                Check(n2 == 1 && hd != null && hd.Owner == o1 && hd.Name == "Has Default", $"a joiner's skins\\default\\ is left out: the mount's own look is still its pack's ({n2} registered; \"{hd?.Name}\" of {hd?.Owner})");
                Check(frost != null && frost.Owner == o2 && frost.Mount == Catalog.Find("selftest.hasdef"), $"the joiner's other skin joins the mount as selftest.hasdef/selftest.frost ({frost?.Id}, {frost?.Owner})");
                // (g)
                Check(Looks.Find("selftest.hasdef/frost-plain") == null && frost != null, "a joiner's skin without its mod's prefix (frost-plain) is left out, its prefixed one (selftest.frost) registers");
                // (h) a code skin on a mount another owner registered: "<mod>.<name>"; on its own mount any name
                string thrown = null;
                try { Mounts.Looks.Register(o3, new MountSkinDef { Id = "plain", Name = "Plain", Mount = "selftest.hasdef", Source = new NoPictures() })?.Dispose(); }
                catch (ArgumentException e) { thrown = e.Message; }
                Check(thrown != null && thrown.Contains("<mod>.<name>") && Looks.Find("selftest.hasdef/plain") == null, $"a code skin 'plain' on another owner's mount is refused ({thrown ?? "nothing thrown"})");
                IDisposable dotted = null, ownPlain = null;
                try
                {
                    dotted = Mounts.Looks.Register(o3, new MountSkinDef { Id = "selftest.dotted", Name = "Dotted", Mount = "selftest.hasdef", Source = new NoPictures() });
                    Looks.SkinDef dd = Looks.Find("selftest.hasdef/selftest.dotted");
                    Check(dotted != null && dd != null && dd.Owner == o3, $"... 'selftest.dotted' registers ({dd?.Id})");
                    ownPlain = Mounts.Looks.Register(o1, new MountSkinDef { Id = "plain", Name = "Own Plain", Mount = "selftest.hasdef", Source = new NoPictures() });
                    Check(Looks.Find("selftest.hasdef/plain")?.Owner == o1, "and the mount's own owner registers a code skin of any name on it (plain)");
                }
                catch (ArgumentException e) { Fail("a code skin that should register was refused: " + e.Message); }
                finally
                {
                    dotted?.Dispose();
                    ownPlain?.Dispose();
                }
                Check(Looks.Find("selftest.hasdef/selftest.dotted") == null && Looks.Find("selftest.hasdef/plain") == null, "both withdrawn with their handles");
                // (i) every donkey skin is an addition (the donkey is the game's): a recipe without its prefix is refused
                thrown = null;
                try { Mounts.Looks.RegisterRecipe(o3, "plainrecipe", "Plain Recipe", f => f.Pixels)?.Dispose(); }
                catch (ArgumentException e) { thrown = e.Message; }
                Check(thrown != null && thrown.Contains("<mod>.<name>") && Looks.Find("donkey/plainrecipe") == null, $"a donkey recipe 'plainrecipe' is refused ({thrown ?? "nothing thrown"})");
            }
            finally
            {
                var mine = new List<Looks.SkinDef>(Looks.All).FindAll(d => d.Owner == o1 || d.Owner == o2 || d.Owner == o3);
                mine.Sort((x, y) => Looks.IsDefaultSlot(x).CompareTo(Looks.IsDefaultSlot(y))); // the variants first: a default withdrawn under another mod's variant warns (LooksInherit.cs)
                foreach (Looks.SkinDef d in mine) { try { Looks.Unregister(d.Id, d.Owner); } catch { } }
                foreach (string m in mounts)
                {
                    Catalog.Entry k = Catalog.Find(m);
                    if (k != null && (k.Owner == o1 || k.Owner == o2)) { try { Looks.UnregisterMount(m, k.Owner); } catch { } }
                }
                foreach (string r in new[] { r1, r2 }) { try { if (System.IO.Directory.Exists(r)) System.IO.Directory.Delete(r, true); } catch { } }
            }
            yield break;
        }

        // The pack format the Mounts panel reads (docs/mounts-foundation-2.md 3, 4.5, 4.6): a skin's and a mount's
        // description and portrait, [Mount.Stats] lines in order, an ability's name and description from code; a pack
        // that needs a newer API is refused with the reason, a format that is not a number too; a [Game] line naming no
        // frame key is reported, not dropped.
        internal static IEnumerator PackFormat()
        {
            const string owner = Owner + ".format";
            string r1 = null, r2 = null, r3 = null, r4 = null;
            IDisposable reg = null;
            try
            {
                r1 = WriteTempPack("gk2donkeyapi-selftest-fmt1", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nneeds = 0.35\nname = Format Test\ndescription = A test look.\nportrait = bdy_static_left.png\nmount = selftest.fmt-beast\n\n[Mount]\nid = selftest.fmt-beast\nname = Format Beast\ndescription = A test creature.\nportrait = bdy_static_left.png\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\nspeed = very fast\ncargo = 3\n\n[Mount.Stats]\nSpeed = steady\nDiet = keepers\n");
                int n = Looks.RegisterFolder(r1, owner);
                MountSkinInfo sk = Mounts.Looks.Find("selftest.fmt-beast/default");
                MountInfo k = Mounts.Catalog.Find("selftest.fmt-beast");
                Check(n == 1 && sk != null && sk.Description == "A test look." && sk.HasPortrait, $"the skin's description and portrait are read (\"{sk?.Description}\", portrait {sk?.HasPortrait})");
                Check(k != null && k.Description == "A test creature." && k.HasPortrait, $"the mount's description and portrait are read (\"{k?.Description}\")");
                Check(k != null && k.Stats.Count == 1 && k.Stats[0].Label == "Diet", $"[Mount.Stats] lines in file order, its own Speed line left out: the API writes that one ({k?.Stats.Count})");
                Check(k != null && Mathf.Approximately(k.Speed, Catalog.SpeedOfBand(SpeedBand.VeryFast, MountStyle.Ride)) && k.SpeedWord == "Very fast" && k.Cargo == 3, $"[Mount] speed (a word, any case: the API's number for it) and cargo are read ({k?.Speed} {k?.SpeedWord}, cargo {k?.Cargo})");
                Check((Catalog.Find("selftest.fmt-beast")?.Pack?.Report ?? "").Contains("[Stats] Speed left out"), "and the mount's report says the Speed line was left out");
                Texture2D portrait = Looks.PortraitOf(Looks.Find("selftest.fmt-beast/default"));
                Check(portrait != null && portrait.width == 96 && portrait.height == 96, $"the portrait PNG is read from the pack ({portrait?.width}x{portrait?.height})");
                Check(Looks.PortraitOfMount(Catalog.Find("selftest.fmt-beast")) != null, "the mount's portrait too (from its own folder)");
                Check(sk != null && sk.Description != null && (Looks.Find("selftest.fmt-beast/default")?.Report ?? "").IndexOf("unknown", StringComparison.OrdinalIgnoreCase) < 0, $"no key of it is unknown to the loader ({Looks.Find("selftest.fmt-beast/default")?.Report})");

                r2 = WriteTempPack("gk2donkeyapi-selftest-fmt2", "selftest.fmt-needs", "[Skin]\nformat = 1\nneeds = 99.0\nname = Needs\n");
                Info("the next \"could not be registered\" warning is this test's own: a pack that needs a newer API is refused");
                n = Looks.RegisterFolder(r2, owner);
                Check(n == 0 && Mounts.Looks.Find("donkey/selftest.fmt-needs") == null, $"a pack that needs API 99.0 is refused ({n} registered)");
                r3 = WriteTempPack("gk2donkeyapi-selftest-fmt3", "selftest.fmt-format", "[Skin]\nformat = x\nname = Format X\n");
                Info("the next \"could not be registered\" warning is this test's own: a format that is not a number is refused");
                n = Looks.RegisterFolder(r3, owner);
                Check(n == 0 && Mounts.Looks.Find("donkey/selftest.fmt-format") == null, $"format = x is refused ({n} registered)");
                r4 = WriteTempPack("gk2donkeyapi-selftest-fmt4", "selftest.fmt-game", "[Skin]\nformat = 1\nname = Game Typo\nsource = game\n\n[Game]\nprefix = 0901_\nscale = 3\nlfit = 0.25\nbdy_static_left = bdy_walk_left_01\n");
                n = Looks.RegisterFolder(r4, owner);
                string report = Looks.Find("donkey/selftest.fmt-game")?.Report ?? "";
                Check(n == 1 && report.Contains("[Game] unknown keys ignored: lfit"), $"a [Game] line naming no frame key is reported ({report})");

                var named = new NamedAbility();
                reg = Mounts.Catalog.Register(owner + ".code", new MountDef { Id = "selftest.fmt-code", Name = "Code Beast", Description = "From code.", Abilities = { named }, Stats = { new MountStat { Label = "Mood", Value = "grim" } } });
                MountInfo kc = Mounts.Catalog.Find("selftest.fmt-code");
                Check(kc != null && kc.Description == "From code." && kc.Stats.Count == 1 && kc.Stats[0].Label == "Mood", "a code mount's description and stats");
                Check(kc != null && kc.AbilityInfos.Count == 1 && kc.AbilityInfos[0].Name == "Glowing" && kc.AbilityInfos[0].Description == "It glows.", $"an ability's name and description reach the info ({(kc != null && kc.AbilityInfos.Count > 0 ? kc.AbilityInfos[0].Name : "none")})");
            }
            finally
            {
                foreach (string id in new[] { "selftest.fmt-beast/default", "donkey/selftest.fmt-game" }) if (Looks.Find(id) != null) Looks.Unregister(id, owner);
                Looks.UnregisterMount("selftest.fmt-beast", owner);
                reg?.Dispose();
                foreach (string r in new[] { r1, r2, r3, r4 }) { try { if (r != null) System.IO.Directory.Delete(r, true); } catch { } }
            }
            yield break;
        }

        // A code skin's pictures for the checks that only register one (DefaultSkinRules): a key and no sprite (nothing
        // wears it).
        internal sealed class NoPictures : IMountSkinSource
        {
            public IEnumerable<string> Keys { get { yield return "bdy_static_left"; } }

            public bool TryGetFrame(string key, out Sprite sprite)
            {
                sprite = null;
                return false;
            }
        }

        internal sealed class NamedAbility : MountAbility
        {
            public override string Id => "glow";
            public override string Name => "Glowing";
            public override string Description => "It glows.";
        }

        // Sets a mount key and tries a look key another mod holds while ridden; clears nothing on purpose (the API does).
        internal sealed class PinningAbility : MountAbility
        {
            internal int MountedCount;
            public override string Id => "pins";
            public override void Mounted(MountHandle mount, MountStyle style)
            {
                MountedCount++;
                mount.Tuning.Set(Owner, "HandsSwayPx", 5f);
                mount.Tuning.Set(Owner, "HarnessColorize", "");
            }
        }

        // A skin pack on disk (a temp folder, the API's test picture three times, one of them misnamed): registered
        // with the right report, worn (the standing frame shows; a walk frame falls back to the donkey's own), then
        // withdrawn: the donkey shows again and the skin is gone from the list.
        internal static IEnumerator SkinPack()
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest");
            string packDir = TempSkinDir(tempRoot, Catalog.DonkeyId, "selftest.pack-test");
            const string owner = Owner + ".pack";
            try
            {
                if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
                System.IO.Directory.CreateDirectory(packDir);
                // the API's own test picture for all three: what is checked is which files the loader takes, not their art
                byte[] png = TempPicture();
                System.IO.File.WriteAllText(System.IO.Path.Combine(packDir, "skin.cfg"), "[Skin]\nformat = 1\nname = Pack Test\nflavour = spicy\n[Tuning]\nSeatAdjustPx = 9\n[Tuning.Ride]\nRiderOffsetPx = 3\n[Tuning.Cart]\nRiderOffsetPx = -9\n");
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(packDir, "bdy_static_left.png"), png);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(packDir, "bdy_wlak_left_01.png"), png);
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(packDir, "front"));
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(packDir, "front", "bdy.png"), png);
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            try
            {
            int n = Looks.RegisterFolder(tempRoot, owner);
            Check(n == 1, $"one pack found under the folder ({n})");
            string report = Looks.Validate("donkey/selftest.pack-test");
            Info("report: " + report);
            Check(report.Contains("unknown keys ignored: flavour"), "the report names the unknown cfg key");
            Check(report.Contains("bdy_wlak_left_01"), "the report names the misnamed picture");
            Check(report.Contains("43 of the donkey's 44 frames not drawn"), "the report counts the frames still to draw");
            Check(report.Contains("1 in front of the rider"), "the report counts the front layer's picture");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "donkey/selftest.pack-test");
            yield return Until(() => a != null && a.IsReady && a.I.TestSkinWearing() == "donkey/selftest.pack-test", 8f);
            Check(waited, "a donkey wears the pack's skin");
            yield return Frames(3);
            Check(a != null && a.I.TestSkinShowing(), "its standing frame is drawn from the pack (the only one it has)");
            Check(a != null && a.I.TestSkinFrontShowing(), "its front picture (front/bdy.png, every body frame) is drawn above");
            Check(a != null && a.Tuning.Get<int>("SeatAdjustPx") == 9 && a.Tuning.SetBy("SeatAdjustPx") == Looks.TuningOwner, "its [Tuning] key is set on the wearer, owned by the skin");
            Check(a != null && a.Tuning.IsSet("RiderOffsetPx") && a.Tuning.Get<int>("RiderOffsetPx") == 3 && a.Tuning.SetBy("RiderOffsetPx") == Looks.TuningOwner, $"its [Tuning.Ride] key is the riding value on the standing wearer ({(a != null && a.Tuning.IsSet("RiderOffsetPx") ? a.Tuning.Get<int>("RiderOffsetPx").ToString() : "not set")}; the cart's -9 waits for a hitch)");
            Check(!Looks.Unregister("donkey/selftest.pack-test", Owner), "a withdrawal by another owner is refused");
            Check(Looks.Unregister("donkey/selftest.pack-test", owner), "the owner withdraws it");
            yield return Frames(3);
            Check(a != null && a.I.TestSkinWearing() == "" && !a.I.TestSkinShowing(), "withdrawn: the donkey shows again (the choice is kept)");
            Check(a != null && !a.Tuning.IsSet("SeatAdjustPx"), "withdrawn: its tuning key is off the donkey again");
            Check(Looks.Find("donkey/selftest.pack-test") == null, "withdrawn: gone from the list");
            a?.Remove();
            yield return Frames(2);
            }
            finally
            {
                // whatever stopped the scenario (a check that threw, the run stopped): nothing of it stays
                LeftOver(owner, "donkey/selftest.pack-test", tempRoot);
            }
        }

        // A recipe skin (a mod's, through the public API: no built-in recipe is left since the last one became a pack,
        // 2026-10-19): nothing shows until every frame is built (the donkey's own look stays), then all 44 pictures are
        // there in the recipe's own colours; cleared, they are freed.
        internal static IEnumerator SkinRecipe()
        {
            const string owner = Owner + ".recipe";
            var rim = new Color32(40, 24, 12, 255);
            var fill = new Color32(200, 40, 40, 255);
            IDisposable handle = Mounts.Looks.RegisterRecipe(owner, "selftest.recipe-test", "Recipe Test", frame =>
            {
                Color32[] src = frame.Pixels;
                var dst = new Color32[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    if (src[i].a == 0) continue;
                    dst[i] = frame.Rim(i % frame.Width, i / frame.Width) ? rim : fill;
                }
                return dst;
            }, author: "test", version: "1");
            Check(handle != null && Looks.Find("donkey/selftest.recipe-test") != null, "a recipe registered through the API is listed");
            Vector3 near = Spot(home, 4f) ?? home;
            yield return Seconds(0.5f);
            int tex0 = Live.Textures;
            MountHandle a = Spawn(near, true);
            yield return Until(() => a != null && a.IsReady, 6f);
            a?.Tuning.Set(Owner, "Skin", "donkey/selftest.recipe-test");
            yield return Frames(2);
            bool buildingSeen = a != null && a.I.TestSkinBuilding() && !a.I.TestSkinShowing();
            yield return Until(() => a != null && a.I.TestSkinShowing(), 10f);
            Check(waited, "the recipe shows once built");
            Check(buildingSeen, "while it was being built, the donkey's own look stayed (no half-built skin)");
            Check(Live.Textures == tex0 + 1, $"the 44 frames built from the donkey's own onto one sheet ({tex0} -> {Live.Textures})");
            // the drawing is the recipe's: two colours of its own, the donkey's eye white gone
            Looks.Loaded look = a?.I.LooksRig.WornOwnColours;
            bool white = look != null && look.Colours.Exists(c => c.r == 255 && c.g == 255 && c.b == 255);
            Check(look != null && look.Colours.Count == 2 && !white, $"the pictures hold the recipe's two colours and no eye white ({look?.Colours.Count} colours)");
            a?.Remove();
            yield return Frames(2);
            Check(Live.Textures == tex0, $"removed: the pictures freed ({tex0} -> {Live.Textures})");
            handle?.Dispose();
            Check(Looks.Find("donkey/selftest.recipe-test") == null, "withdrawn: the recipe is gone from the list");
        }

        // The Mounts panel itself, over the game: it opens from a live page with a Mount row, fits the screen, lists the
        // mounts and builds the chosen one's rows; a pick in the list moves the page's mount and the right side; Cancel
        // puts the mount back; the key toggles it, the button only brings it forward (docs/mounts-foundation-2.md 3).
        // The way back (docs/mounts-panel-design.md "Escape and the way back"), walked by the step Escape takes: opened
        // from the game it takes the controls and gives them back, nothing else opens; opened over the pause menu it
        // hides the pause menu (still open, modal), the colours window steps back to the journal, the journal to the
        // pause menu shown again as it was.
        internal static IEnumerator PanelWayBack()
        {
            MountSettingsPage page = null;
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && live.HasMountRow) { page = live; break; }
            if (page == null)
            {
                Skip("no mod's settings page with a Mount row is shown (the Mounts panel is a mod's)");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null)
            {
                Warn($"a game window is open ({LazyWindowsStackController.ActiveWindow.name}): close it and run again; skipped");
                yield break;
            }
            UIGamePauseWindow pause = LazyUI.GetWindow<UIGamePauseWindow>();
            try
            {
                // 1. from the game
                MountsPanel.Open(page, toggle: false);
                yield return Frames(2);
                Check(MountsPanel.IsOpen && MountsPanel.TestControlsTaken && !MainGame.PlayerController.IsControlEnabledByType(TakenControlType.ByUI), "opened from the game: the panel holds the controls (the game's keys don't act under it)");
                MountsPanel.TestStepBack();
                yield return Frames(2);
                Check(!MountsPanel.IsOpen && LazyWindowsStackController.ActiveWindow == null && MainGame.PlayerController.IsControlEnabledByType(TakenControlType.ByUI), "one step back: closed into the game, no window opened, the controls given back");
                // 2. over the pause menu, through the colours window
                if (pause == null)
                {
                    Warn("no pause menu to open: the second half skipped");
                    yield break;
                }
                pause.Open(null);
                yield return Frames(2);
                MountsPanel.Open(page, toggle: false);
                yield return Frames(2);
                Check(MountsPanel.IsOpen && LazyWindowsStackController.IsWindowOpened(pause) && !pause.gameObject.activeSelf, "opened over the pause menu: it waits underneath, still open, switched off");
                MountsPanel.TestOpenColours();
                yield return Frames(2);
                MountsPanel.TestStepBack();
                yield return Frames(2);
                Check(MountsPanel.IsOpen && !MountsPanel.IsColoursOpen && !pause.gameObject.activeSelf, "a step back from the colours window: the journal, the pause menu still underneath");
                MountsPanel.TestStepBack();
                yield return Frames(2);
                Check(!MountsPanel.IsOpen && LazyWindowsStackController.IsWindowOpened(pause) && pause.gameObject.activeSelf && LazyWindowsStackController.ActiveWindow == pause, "a step back from the journal: the pause menu shown again, on top");
            }
            finally
            {
                if (MountsPanel.IsOpen) MountsPanel.TestClose(keep: true);
                try { if (pause != null && LazyWindowsStackController.IsWindowOpened(pause)) { pause.gameObject.SetActive(true); pause.Close(); } } catch { }
            }
            yield return Frames(2);
            Check(LazyWindowsStackController.ActiveWindow == null && MainGame.PlayerController.IsControlEnabledByType(TakenControlType.ByUI), "cleaned up: no window, the controls free");
        }

        // The panel as drawn, into BepInEx\GK2-panel-shots\<name>.png (the self-tests are dev-only): what its layout looks
        // like, read back after the frame (the overlay canvas included), for judging alignment from the picture rather
        // than from the code (the user, 2026-10-26: "make sure everything is aligned in that page").
        internal static IEnumerator PanelShot(string name)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                var t = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                t.Apply();
                string dir = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2-panel-shots");
                System.IO.Directory.CreateDirectory(dir);
                var safe = new System.Text.StringBuilder();
                foreach (char c in name) safe.Append(char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_');
                string file = System.IO.Path.Combine(dir, safe + ".png");
                System.IO.File.WriteAllBytes(file, t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
                Info("the panel's picture: " + file);
            }
            catch (Exception e)
            {
                Info("the panel's picture could not be taken: " + e.Message);
            }
        }

        internal static IEnumerator PanelSmoke()
        {
            MountSettingsPage page = null;
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && live.HasMountRow) { page = live; break; }
            if (page == null)
            {
                Skip("no mod's settings page with a Mount row is shown (the Mounts panel is a mod's)");
                yield break;
            }
            string was = page.MountChosen ?? MountCatalog.DonkeyId;
            Info("if a \"the Mounts panel can't open\" warning follows, it is this test's finding: the Settings window could not be copied before it was ever opened this session");
            try
            {
                MountsPanel.Open(page, toggle: false);
                yield return Frames(2);
                Check(MountsPanel.IsOpen, "the panel opens over the game from a page with a Mount row");
                if (!MountsPanel.IsOpen) yield break;
                var m = MountsPanel.TestMeasure();
                Check(m.Scale >= 0.5f && m.W * m.Scale <= m.RoomW + 1f && m.H * m.Scale <= m.RoomH + 1f, $"it fits the screen ({m.W:0}x{m.H:0} at {m.Scale:0.##} in {m.RoomW:0}x{m.RoomH:0})");
                // the chosen mount's pack may be left out (a mod installed without that pack; 2026-10-27): it is listed greyed, with no
                // rows, no picture and no Ride, Drive or Colours; the rest of the test runs from the mounts that are there
                bool wasMissing = Catalog.Find(was) == null;
                if (wasMissing)
                {
                    Check(MountsPanel.TestEntries >= 2 && MountsPanel.TestRows == 0 && string.Equals(MountsPanel.TestBuiltFor, was, StringComparison.OrdinalIgnoreCase) && !MountsPanel.TestDriveShown && MountsPanel.TestPicture == null
                        && MountsPanel.Entries().Count < MountsPanel.TestEntries,
                        $"the chosen {was} isn't installed: listed greyed (the panel {MountsPanel.TestEntries} entries), no rows ({MountsPanel.TestRows}), no picture, no Drive the cart");
                }
                else Check(MountsPanel.TestEntries >= 1 && MountsPanel.TestRows > 0 && string.Equals(MountsPanel.TestBuiltFor, was, StringComparison.OrdinalIgnoreCase), $"it lists {MountsPanel.TestEntries} mount(s) and built {MountsPanel.TestRows} rows for the chosen one ({was})");
                string firstOrder = wasMissing ? null : JournalChecks(was);
                TMPro.TMP_InputField nameField = MountsPanel.TestNameField();
                if (nameField == null) Info("no name row in this page's journal: the text box check skipped");
                else
                {
                    nameField.ActivateInputField();
                    yield return Frames(2);
                    Check(nameField.isFocused, "the name's text box takes the typing (activated, it is focused)");
                    nameField.DeactivateInputField();
                    yield return Frames(1);
                }
                string other = null;
                foreach (MountsPanel.Entry e in MountsPanel.Entries()) if (!e.Id.Equals(was, StringComparison.OrdinalIgnoreCase) && !e.Fake) { other = e.Id; break; }
                if (other == null)
                {
                    Warn("one mount only: the pick check skipped");
                }
                else
                {
                    MountCompanion me = page.TestCompanion;
                    string hadSkin = me?.Skin;
                    MountsPanel.TestSelect(other);
                    yield return Frames(2);
                    Check(string.Equals(page.MountChosen, other, StringComparison.OrdinalIgnoreCase) && string.Equals(MountsPanel.TestBuiltFor, other, StringComparison.OrdinalIgnoreCase) && MountsPanel.TestRows > 0, $"picking {other} in the list: the page's mount follows and the right side is built for it ({MountsPanel.TestRows} rows)");
                    // the user, 2026-10-22: a pick shows a mount, only Ride or Drive the cart makes it his
                    Check(me != null && me.Skin == hadSkin && string.Equals(Looks.MountOf(Looks.Find(me.Paper.LooksRig.DefNow?.Id ?? ""))?.Id ?? MountCatalog.DonkeyId, other, StringComparison.OrdinalIgnoreCase),
                        $"and his mount is not changed: the companion still wears \"{me?.Skin}\", the picture's paper wears {other}'s (\"{me?.Paper.LooksRig.DefNow?.Id}\")");
                    string otherOrder = JournalChecks(other);
                    if (firstOrder == null) firstOrder = otherOrder; // the chosen one not installed: the first installed one sets the order
                    Check(SameOrder(firstOrder, otherOrder), $"the journal's rows come in one order on {was} and {other} ({firstOrder} / {otherOrder})");
                    var m2 = MountsPanel.TestMeasure();
                    Check(m2.W * m2.Scale <= m2.RoomW + 1f && m2.H * m2.Scale <= m2.RoomH + 1f, $"still on the screen ({m2.W:0}x{m2.H:0} at {m2.Scale:0.##})");
                }
                // a mount that can't pull the cart (the user, 2026-10-25): no Drive the cart, no cart colours; one that can: both
                string noCart = null, withCart = null;
                foreach (MountsPanel.Entry e in MountsPanel.Entries())
                {
                    if (noCart == null && !MountSettingsPage.MountPullsCart(e.Id)) noCart = e.Id;
                    if (withCart == null && MountSettingsPage.MountPullsCart(e.Id)) withCart = e.Id;
                }
                if (noCart == null || withCart == null) Info("no mount that can't pull the cart (or none that can) is installed: the cart check skipped");
                else
                {
                    MountsPanel.TestSelect(noCart);
                    yield return Frames(2);
                    Info($"{noCart} picked: Drive the cart shown {MountsPanel.TestDriveShown}; the colour groups: {MountsPanel.TestColourGroups()}");
                    Check(SameOrder(firstOrder, JournalChecks(noCart)), $"the journal's rows come in one order on {was} and {noCart}");
                    Check(!MountsPanel.TestDriveShown && !MountsPanel.TestColourGroup("Cart colours") && MountsPanel.TestColourGroup("Mount colours"),
                        $"{noCart}, which can't pull the cart: no Drive the cart button, no Cart colours (its mount colours stay)");
                    // its picture finished (the alone view): waited for, not a fixed count (a flying mount, a pack read from disk
                    // since 2026-10-26, was still building after 30 frames: the "last photo" was another mount's)
                    Mount.Instance paperBefore = LookPreview.PaperOf(page);
                    yield return Until(() => MountsPanel.TestPicture != null && PreviewStage.ShowsLookNow(MountsPanel.TestPicture, paperBefore), 15f);
                    Check(waited, $"{noCart}'s picture is shown whole before the switch (within 15 s)");
                    yield return PanelShot("journal-" + noCart);
                    // from it to one that pulls the cart: every frame the picture is the cartless mount's last photo, or the new
                    // one whole in the cart view, never the new mount in the alone view (the user, 2026-10-25: "large and
                    // without a cart" for a few frames)
                    Texture before = MountsPanel.TestPicture;
                    Mount.Instance paperNow = LookPreview.PaperOf(page);
                    MountsPanel.TestSelect(withCart);
                    int stale = 0, bad = 0, frames = 0;
                    Texture now = before;
                    for (; frames < 240; frames++)
                    {
                        yield return null;
                        now = MountsPanel.TestPicture;
                        if (now == before) { stale++; continue; }
                        if (now != null && !PreviewStage.ShowsLookNow(now, paperNow)) bad++;
                        break;
                    }
                    Check(before != null && now != null && now != before && bad == 0,
                        $"from {noCart} to {withCart}: the last photo held {stale} frame(s), then {withCart} whole in its own view{(bad > 0 ? " (NOT: the new view not yet the new look)" : "")}");
                    yield return Frames(2);
                    Check(MountsPanel.TestDriveShown && MountsPanel.TestColourGroup("Cart colours"), $"{withCart}, which pulls the cart: Drive the cart and the Cart colours are back");
                    yield return Seconds(1f);
                    yield return PanelShot("journal-" + withCart);
                    MountsPanel.TestOpenColours();
                    yield return Frames(3);
                    yield return PanelShot("colours-" + withCart);
                    MountsPanel.TestCloseColours(keep: true);
                    yield return Frames(2);
                    MountsPanel.TestSelect(was);
                    yield return Frames(2);
                }
                MountsPanel.TestOpenColours();
                yield return Frames(2);
                yield return PanelShot("colours-" + (page.MountChosen ?? "donkey"));
                Check(MountsPanel.IsColoursOpen && MountsPanel.TestColourRows > 0, $"the colours window opens over the journal with {MountsPanel.TestColourRows} rows (the colours)");
                string narrowColours = MountsPanel.TestNarrowLabel(coloursWindow: true);
                Check(narrowColours == null, $"the colours window: every label has room (100 or more){(narrowColours != null ? " (NOT: " + narrowColours + ")" : "")}");
                MountsPanel.TestCloseColours(keep: true);
                yield return Frames(2);
                Check(!MountsPanel.IsColoursOpen && MountsPanel.IsOpen, "closed: the journal is back");
                // Done with another mount picked: his mount stays (the Mount row goes back), the skin he wore with it
                MountCompanion meNow = page.TestCompanion;
                string wore = meNow?.Skin;
                if (other != null)
                {
                    MountsPanel.TestSelect(other);
                    yield return Frames(2);
                }
                MountsPanel.TestClose(keep: true);
                yield return Frames(2);
                Check(!MountsPanel.IsOpen && string.Equals(page.MountChosen ?? MountCatalog.DonkeyId, was, StringComparison.OrdinalIgnoreCase) && meNow?.Skin == wore, $"Done with {other ?? "the same mount"} picked: the mount is {was} again, the companion still wears \"{meNow?.Skin}\"");
                // the commit below makes the pick his for two frames: with his mount pulling a cart, the pick must pull it
                // too, or the cart is left standing where he is parked for good (the cart rule, with or without a view
                // since 2026-10-27; the player's cart was left at the parked spot by this test that evening)
                string commit = other;
                bool hisCart = meNow?.Donkey != null && meNow.Donkey.HasCart;
                if (hisCart && commit != null && !MountSettingsPage.MountPullsCart(commit))
                {
                    commit = null;
                    foreach (MountsPanel.Entry e in MountsPanel.Entries()) if (!e.Id.Equals(was, StringComparison.OrdinalIgnoreCase) && !e.Fake && MountSettingsPage.MountPullsCart(e.Id)) { commit = e.Id; break; }
                    if (commit == null) Info($"his mount pulls a cart and no other installed mount can: the commit check skipped (it would leave the cart standing)");
                }
                if (commit != null && meNow != null)
                {
                    // the way Ride and Drive the cart close it: the pick becomes his
                    MountsPanel.Open(page, toggle: false);
                    yield return Frames(2);
                    MountsPanel.TestSelect(commit);
                    yield return Frames(2);
                    MountsPanel.TestClose(keep: true, commit: true);
                    yield return Frames(2);
                    Check(string.Equals(page.MountChosen, commit, StringComparison.OrdinalIgnoreCase) && string.Equals(meNow.MountId, commit, StringComparison.OrdinalIgnoreCase), $"closed as Ride and Drive close it: {commit} is his (the companion's mount {meNow.MountId}, \"{meNow.Skin}\"{(hisCart ? "; one that pulls his cart" : "")})");
                    page.SelectMount(was); // put back, outside the panel: at once
                    yield return Frames(2);
                    // a chosen mount that isn't installed: the page keeps the choice, the companion rides the donkey meanwhile
                    Check(wasMissing ? string.Equals(page.MountChosen, was, StringComparison.OrdinalIgnoreCase) && string.Equals(meNow.MountId, MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(meNow.MountId, was, StringComparison.OrdinalIgnoreCase), $"and back to {was} outside the panel, at once (\"{meNow.Skin}\"{(wasMissing ? ", the donkey until it is installed" : "")})");
                }
                MountsPanel.Open(page, toggle: true);
                yield return Frames(2);
                Check(MountsPanel.IsOpen, "the key opens it");
                MountsPanel.Open(page, toggle: false);
                yield return Frames(1);
                Check(MountsPanel.IsOpen, "the button while it is open leaves it open");
                MountsPanel.Open(page, toggle: true);
                yield return Frames(2);
                Check(!MountsPanel.IsOpen && string.Equals(page.MountChosen ?? MountCatalog.DonkeyId, was, StringComparison.OrdinalIgnoreCase), "the key while it is open closes it, what was chosen stays");
            }
            finally
            {
                if (MountsPanel.IsOpen) MountsPanel.TestClose(keep: false);
            }
        }

        // The journal built for a mount (the user, 2026-10-26): the panel's own controls (no game "< >" row), the name
        // first and the Skin second, the babies rows only for a mount with babies, nothing outside its column. Returns
        // its rows' keys in order.
        internal static string JournalChecks(string mount)
        {
            string order = MountsPanel.TestJournalOrder();
            Info($"the journal of {mount}: {order}");
            var keys = new List<string>(order.Split(','));
            bool named = keys.Contains("Name");
            Check(keys.Count > 0 && (named ? keys.Count > 1 && keys[0] == "Name" && keys[1] == "Skin" : keys[0] == "Skin"), $"{mount}'s journal: {(named ? "the name first, the Skin second" : "the Skin first")} ({order})");
            Check(MountsPanel.TestJournalGameRows() == 0, $"{mount}'s journal: every row is the panel's own control ({MountsPanel.TestJournalGameRows()} game rows)");
            string narrow = MountsPanel.TestNarrowLabel(coloursWindow: false);
            Check(narrow == null, $"{mount}'s journal: every label has room (100 or more){(narrow != null ? " (NOT: " + narrow + ")" : "")}");
            string outside = MountsPanel.TestJournalOutside();
            Check(outside == null, $"{mount}'s journal: every row inside its column{(outside != null ? " (NOT: " + outside + ")" : "")}");
            bool babies = GK2DonkeyAPI.Catalog.Find(mount)?.Foals ?? true;
            if (named) Check(babies == keys.Contains("Foals"), $"{mount}'s journal: the babies rows {(babies ? "shown (it has babies)" : "left out (it has none)")}");
            return order;
        }

        // Two journals' orders agree on the rows both have.
        internal static bool SameOrder(string a, string b)
        {
            var x = new List<string>(a.Split(','));
            var y = new List<string>(b.Split(','));
            x.RemoveAll(k => !y.Contains(k));
            y.RemoveAll(k => !x.Contains(k));
            return string.Join(",", x) == string.Join(",", y);
        }

        // A game-source skin whose frames the game lacks: set aside the first time it is worn (not drawn as a blank sheet),
        // the donkey comes instead in mount too (it pulls the cart), it is out of every list; checked again it stays aside;
        // healed (its prefix pointing at frames the game has: a game character's) it is offered again and the wearer draws it with no
        // door and no setting change (docs/mounts-foundation-2.md 4.4).
        internal static IEnumerator AsideSkin()
        {
            const string owner = Owner + ".aside";
            string tempRoot = null;
            MountHandle a = null;
            MountCompanion pageOf = null;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-aside", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = Aside Test\ndescription = A test look from frames the game lacks.\nsource = game\nmount = selftest.aside-beast\n\n[Game]\nprefix = zzznothing_\nscale = 1\nbdy_static_left = bdy_walk_left_01\n\n[Mount]\nid = selftest.aside-beast\nname = Aside Beast\ndescription = A test mount.\nauthor = the self-test\nversion = 0.1\ncart = false\nfoals = true\n");
                Looks.RegisterFolder(tempRoot, owner);
                Looks.SkinDef d = Looks.Find("selftest.aside-beast/default");
                Check(d != null && d.Unavailable == null, "registered: nothing is known against it yet (the frames are checked when worn)");
                if (d == null) yield break;
                a = Spawn(home + new Vector3(4f, 0f, 0f), true);
                if (a == null)
                {
                    Fail("could not spawn");
                    yield break;
                }
                a.Tuning.Set(Owner, "Skin", "selftest.aside-beast/default");
                yield return Until(() => d.Unavailable != null, 8f);
                Check(waited, $"worn with no frames to read: set aside ({d.Unavailable})");
                yield return Frames(3);
                Check(!a.I.TestSkinShowing() && a.Mount == MountCatalog.DonkeyId, $"the donkey comes instead, in mount too ({a.Mount})");
                Check(Looks.WhyNotCartByMount(a.I) == null, "and it pulls the cart (the set-aside mount's no-cart rule is not applied)");
                bool listed = false;
                foreach ((string id, string label) in Looks.MountChoices()) if (id == "selftest.aside-beast") listed = true;
                Check(!listed && !(Mounts.Looks.Find("selftest.aside-beast/default")?.IsAvailable ?? true) && !MountsPanel.Entries().Exists(e => e.Id == "selftest.aside-beast"), "not offered: out of the mount list and the panel's, IsAvailable false");
                Looks.TestResetRecheck(d.Id);
                Check(!Looks.RecheckGameSource(d) && d.Unavailable != null, "checked again: still nothing, stays aside");
                // healed: the skin now names a prefix the game has (a game character's, in memory once the game-creature scenario ran)
                d.Game.Prefix = "0901_";
                Looks.TestResetRecheck(d.Id);
                // a settings page's picture wearing it (a companion's paper donkey, never registered): told too
                pageOf = Mounts.Companion(owner + ".page");
                pageOf.Skin = "selftest.aside-beast/default";
                Mount.Instance paper = pageOf.Paper;
                paper.LooksRig.TestClearDirty(); // as after its tick gave up on the set-aside skin
                bool healed = Looks.RecheckGameSource(d);
                if (!healed)
                {
                    Warn("the game character's 0901_ frames are not loaded (run the game-creature scenario first): the heal check skipped");
                }
                else
                {
                    Check(d.Unavailable == null && (Mounts.Looks.Find("selftest.aside-beast/default")?.IsAvailable ?? false), "the frames are there: offered again");
                    Check(paper.LooksRig.TestDirty, "the settings page's picture is told too (its paper donkey looks at the skin again)");
                    yield return Until(() => a.I.TestSkinShowing(), 20f);
                    Check(waited, "and the donkey wearing it draws the skin, with no door and no setting change");
                }
            }
            finally
            {
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                if (pageOf != null) pageOf.Skin = ""; // the test page's picture back to the donkey
                Looks.Unregister("selftest.aside-beast/default", owner);
                try { if (tempRoot != null) System.IO.Directory.Delete(tempRoot, true); } catch { }
            }
        }

        // The side file's set lines by owner (docs/mounts-foundation-2.md 2.1): a mod that isn't loaded has its lines
        // set aside (kept, not applied, written back as they were); an ability's lines are dropped (session-only); a
        // loaded mod's line is applied under its owner; while a loaded mod holds the key the set-aside line is not
        // written (it would overwrite the live value on the next load), and it is written again once the key is free.
        internal static IEnumerator AsideLines()
        {
            MountHandle a = Spawn(home + new Vector3(3f, 0f, 0f), true, persist: true, key: "aside lines");
            if (a == null)
            {
                Fail("could not spawn");
                yield break;
            }
            try
            {
                yield return Frames(2);
                const string gone = "some.gone.mod";
                Mount.RestoreSetting(a.I, "SpeedMultiplier", gone, "2.5");
                Check(!a.Tuning.IsSet("SpeedMultiplier") && a.I.AsideSets.Count == 1, "a line of a mod that isn't loaded is set aside, not applied");
                Mount.RestoreSetting(a.I, "HandsSwayPx", Owner + ".ability.selftest.some-mount", "5");
                Check(!a.Tuning.IsSet("HandsSwayPx") && a.I.AsideSets.Count == 1, "an ability's line is dropped (its values are set again in play)");
                Mount.RestoreSetting(a.I, "CoatColorize", Owner, "FF00FF");
                Check(a.Tuning.IsSet("CoatColorize") && a.Tuning.SetBy("CoatColorize") == Owner, "a loaded mod's line is applied under its owner");
                string Text() => Mount.BuildSideText("selftest", "saved at test time", 42, Mount.KeptInstances(i => i == a.I, log: false), withDormant: false, out _);
                string t1 = Text();
                Check(t1.Contains(gone) && t1.Contains("SpeedMultiplier"), "saved: the gone mod's line is written back as it was");
                a.Tuning.Set(Owner, "SpeedMultiplier", 1.2f);
                string t2 = Text();
                Check(!t2.Contains(gone) && t2.Contains("SpeedMultiplier"), "a loaded mod holds the key now: its line is written, the gone mod's is not (it would overwrite the live value on the next load)");
                a.Tuning.Clear(Owner, "SpeedMultiplier");
                string t3 = Text();
                Check(t3.Contains(gone), "cleared: the gone mod's line is written again");
            }
            finally
            {
                if (a.Exists) a.Remove();
            }
        }

        // New views are made at most Lod.SpawnsPerFrame a frame (50 in one frame made a 20 ms frame).
        internal static IEnumerator SpawnCap()
        {
            Lod.MaxSpawnsInAFrame = 0;
            var made = new List<MountHandle>();
            for (int k = 0; k < 20; k++)
            {
                Vector3 at = home + new Vector3(UnityEngine.Random.Range(-8f, 8f), 0f, UnityEngine.Random.Range(-4f, 4f));
                MountHandle h = Spawn(at, k % 2 == 0);
                if (h != null) made.Add(h);
            }
            yield return Until(() => made.TrueForAll(h => !h.Exists || h.I.HasView), 10f);
            Check(waited, $"{made.Count} views made within 10 s");
            Check(Lod.MaxSpawnsInAFrame <= Lod.SpawnsPerFrame, $"at most {Lod.SpawnsPerFrame} made in a frame (most: {Lod.MaxSpawnsInAFrame})");
        }

        // A donkey that goes idle off screen, gets a facing while idle, and wakes again: its animator faces that way,
        // no cart part comes on, its own colours are on, nothing stays frozen; and riding it then works.
        internal static IEnumerator Wake(bool switchOff)
        {
            Lod.IdleSwitchesOff = switchOff;
            Vector3 near = Spot(home, 3f) ?? home;
            Vector3? far = Spot(home, 26f);
            if (!far.HasValue)
            {
                Warn("no ground 26 units around the keeper: skipped");
                yield break;
            }
            MountHandle h = Spawn(near, right: false);
            if (h == null)
            {
                Fail("could not spawn");
                yield break;
            }
            h.Tuning.Set(Owner, "CoatColorize", "FF00FF");
            h.Tuning.Set(Owner, "CoatColorizeAmount", 1f);
            yield return Until(() => h.IsReady && IsActive(h), 6f);
            if (!waited)
            {
                Fail("never got ready and active next to the keeper");
                yield break;
            }
            yield return MoveKeeper(far.Value, homeFacing);
            if (!moved) yield break;
            yield return Until(() => h.Exists && h.I.tier == Mount.Tier.Idle, 6f);
            if (!waited)
            {
                Warn($"not idle with the keeper 26 away (level {h.I.tier}; on screen there?): skipped");
                yield break;
            }
            if (switchOff)
            {
                Check(h.I.TestSwitchedOff && !h.I.TestViewActive, "idle: its view is switched off");
                Check(!h.IsReady, "idle and switched off: IsReady is false");
                MountResult early = h.Ride(showMessage: false);
                Check(!early.Ok, $"idle and switched off: Ride refuses ({early.Reason})");
            }
            else
            {
                Check(h.I.TestQuietRenderers > 0 && h.I.TestViewActive, "idle: its view is frozen, still on");
            }
            h.MoveTo(h.Position, facingRight: true); // a new facing while idle: it must reach the animator on waking
            yield return Seconds(0.6f);
            Check(h.I.tier == Mount.Tier.Idle, "still idle after a move while far");
            PutKeeper(home, homeFacing);
            yield return Until(() => h.IsReady && IsActive(h) && h.I.TestViewActive, 5f);
            if (!waited)
            {
                Fail("did not wake with the keeper next to it");
                yield break;
            }
            yield return Frames(4);
            float? d = h.I.TestAnimatorDirection();
            Check(d.HasValue && Mathf.Abs(d.Value) < 1f, $"woken: the animator faces right as set while idle (Direction {Dir(d)})");
            Check(h.I.TestCartPartsOn() == 0, $"woken: no cart part on ({h.I.TestCartPartsOn()} on)");
            bool? c = h.I.TestColoursOurs();
            if (c == null) Warn("woken: colours not attached (colours off?)");
            else Check(c.Value, "woken: its own colours are on");
            Check(h.I.TestQuietRenderers == 0, "woken: nothing left frozen");
            MountResult ride = h.Ride(showMessage: false);
            Check(ride.Ok, $"woken: riding it works{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
            yield return Frames(3);
            Check(Mounts.IsMounted && h.IsRidden && h.I.TestViewActive && h.I.TestQuietRenderers == 0, "ridden: awake and drawn");
            d = h.I.TestAnimatorDirection();
            bool wantRight = h.FacingRight;
            Check(d.HasValue && (wantRight ? Mathf.Abs(d.Value) < 1f : Mathf.Abs(Mathf.Abs(d.Value) - 180f) < 1f),
                $"ridden: the animator faces the ride's way ({(wantRight ? "right" : "left")}, Direction {Dir(d)})");
            Mounts.Dismount("self-test");
            yield return Frames(2);
            if (switchOff)
            {
                yield break;
            }
            // Frozen views are ready, so a ride can start on one directly (the keeper is put on its seat): the case of
            // the invisible ridden donkey. Riding must wake it fully (WakeForRide). The far spot is taken from where the
            // donkey stands now: the ride moved it, and 22 from the home spot it was still within the 40% screen margin
            // an active donkey needs to leave (first run, 2026-10-01).
            Vector3? farAgain = Spot(h.Position, 30f);
            if (!farAgain.HasValue)
            {
                Warn("no ground 30 units from the donkey: the frozen ride skipped");
                yield break;
            }
            yield return MoveKeeper(farAgain.Value, homeFacing);
            if (!moved) yield break;
            yield return Until(() => h.Exists && h.I.tier == Mount.Tier.Idle, 8f);
            if (!waited)
            {
                Warn($"not idle again with the keeper 26 away ({State(h)}): the frozen ride skipped");
                yield break;
            }
            yield return Frames(2);
            Check(h.I.TestQuietRenderers > 0 && h.I.TestViewActive, $"idle again after a ride: frozen ({State(h)})");
            if (h.I.TestQuietRenderers == 0)
            {
                yield break;
            }
            MountResult frozenRide = h.Ride(showMessage: false);
            Check(frozenRide.Ok, $"a frozen donkey can be ridden{(frozenRide.Ok ? "" : " (" + frozenRide.Reason + ")")}");
            yield return Frames(3);
            Check(h.IsRidden && h.I.TestViewActive && h.I.TestQuietRenderers == 0, "ridden from frozen: drawn and animated (not invisible)");
            yield return WorldViewsRight();
            float? fd = h.I.TestAnimatorDirection();
            bool fr = h.FacingRight;
            Check(fd.HasValue && (fr ? Mathf.Abs(fd.Value) < 1f : Mathf.Abs(Mathf.Abs(fd.Value) - 180f) < 1f),
                $"ridden from frozen: faces the ride's way ({(fr ? "right" : "left")}, Direction {Dir(fd)})");
            Mounts.Dismount("self-test");
            yield return Frames(2);
        }

        // A mod's call-and-ride on a donkey frozen off screen: brought to the keeper and ridden in the same frame. Riding a
        // switched-off view must wait for it to wake; once ridden it is drawn and faces the keeper's way.
        internal static IEnumerator MountWhileIdle()
        {
            Lod.IdleSwitchesOff = true;
            Vector3 near = Spot(home, 3f) ?? home;
            Vector3? far = Spot(home, 26f);
            if (!far.HasValue)
            {
                Warn("no ground 26 units around the keeper: skipped");
                yield break;
            }
            float keeperFacing = -homeFacing; // the donkey faces the other way, so it has to turn
            MountHandle h = Spawn(near, right: keeperFacing < 0f);
            if (h == null)
            {
                Fail("could not spawn");
                yield break;
            }
            yield return Until(() => h.IsReady && IsActive(h), 6f);
            far = Spot(h.Position, 30f) ?? far; // from where it stands: 26 from home was within the screen margin once
            yield return MoveKeeper(far.Value, keeperFacing);
            if (!moved) yield break;
            yield return Until(() => h.Exists && h.I.tier == Mount.Tier.Idle, 6f);
            if (!waited)
            {
                Warn($"not idle with the keeper 26 away (level {h.I.tier}): skipped");
                yield break;
            }
            PutKeeper(home, keeperFacing);
            MountResult come = h.ComeToKeeper(keeperFacing > 0f);
            MountResult sameFrame = h.Ride(showMessage: false);
            Check(come.Ok, "ComeToKeeper works on an idle donkey");
            Check(!sameFrame.Ok, $"riding it in the same frame waits (refused: {sameFrame.Reason})");
            yield return Until(() => h.IsReady, 3f);
            Check(waited, "ready within 3 s after coming");
            MountResult ride = h.Ride(showMessage: false);
            Check(ride.Ok, $"ridden once ready{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
            yield return Frames(4);
            float? d = h.I.TestAnimatorDirection();
            bool right = keeperFacing > 0f;
            Check(h.I.TestViewActive && h.I.TestQuietRenderers == 0, "ridden: drawn (not frozen)");
            Check(d.HasValue && (right ? Mathf.Abs(d.Value) < 1f : Mathf.Abs(Mathf.Abs(d.Value) - 180f) < 1f),
                $"ridden: faces the keeper's way ({(right ? "right" : "left")}, Direction {Dir(d)})");
            Mounts.Dismount("self-test");
            yield return Frames(2);
        }

        // Views released far away are parked and taken again by the next donkey of the same scene, mount and size: a
        // foal's view never under a full donkey, no cart parts, its own colours.
        internal static IEnumerator Reuse()
        {
            Vector3 near = Spot(home, 3f) ?? home;
            Vector3? far = Spot(home, 50f) ?? Spot(home, 42f);
            if (!far.HasValue)
            {
                Warn("no ground 42 to 50 units around the keeper: skipped");
                yield break;
            }
            MountHandle d1 = Spawn(near, right: true);
            MountHandle f1 = Spawn(near + new Vector3(1.5f, 0f, 0f), right: false, scale: 0.6f);
            yield return Until(() => d1.IsReady && f1.IsReady, 6f);
            if (!waited)
            {
                Fail("the first two never got their views");
                yield break;
            }
            int parked0 = Lod.ParkedTotal, poolBefore = ViewPool.Count;
            // reuses counted from before the keeper goes: a world's mount waking near the far spot takes a parked view as
            // soon as it is in the pool (2026-10-27: the player's cart left standing out there took one, pool 0 -> 1)
            int reused0 = Lod.ReusedTotal;
            yield return MoveKeeper(far.Value, homeFacing);
            if (!moved) yield break;
            yield return Until(() => d1.I.tier == Mount.Tier.Asleep && f1.I.tier == Mount.Tier.Asleep, 6f);
            if (!waited)
            {
                Warn($"not asleep with the keeper far away (levels {d1.I.tier} / {f1.I.tier}): skipped");
                yield break;
            }
            yield return Frames(2);
            Check(Lod.ParkedTotal - parked0 >= 2, $"both views parked ({Lod.ParkedTotal - parked0} parked; pool {poolBefore} -> {ViewPool.Count})");
            d1.Remove();
            f1.Remove();
            PutKeeper(home, homeFacing);
            yield return Seconds(0.5f);
            MountHandle d2 = Spawn(near, right: false);
            MountHandle f2 = Spawn(near + new Vector3(1.5f, 0f, 0f), right: true, scale: 0.6f);
            yield return Until(() => d2.IsReady && f2.IsReady, 6f);
            Check(waited, "the second two got their views");
            yield return Frames(3);
            // Anyone may take a parked view: the world's own donkeys waking as the keeper comes back take them too
            // (third run, 2026-10-01: the world's carts and donkeys woke with the test's two and took one).
            int reusedNow = Lod.ReusedTotal - reused0;
            Check(reusedNow >= 2, $"parked views were taken again ({reusedNow} reused, by the test's donkeys or the world's waking)");
            yield return WorldViewsRight();
            Check(Mathf.Abs(d2.I.TestViewScale - 1f) < 0.01f, $"the donkey's view is full size ({d2.I.TestViewScale:F2})");
            Check(Mathf.Abs(f2.I.TestViewScale - 0.6f) < 0.01f, $"the foal's view is foal size ({f2.I.TestViewScale:F2})");
            Check(d2.I.TestCartPartsOn() == 0 && f2.I.TestCartPartsOn() == 0, "no cart part on either");
            float? dd = d2.I.TestAnimatorDirection(), fd = f2.I.TestAnimatorDirection();
            Check(dd.HasValue && Mathf.Abs(Mathf.Abs(dd.Value) - 180f) < 1f, $"the donkey faces left as spawned (Direction {Dir(dd)})");
            Check(fd.HasValue && Mathf.Abs(fd.Value) < 1f, $"the foal faces right as spawned (Direction {Dir(fd)})");
        }

        // A parent ridden away: its foals stay active and trot after it, also off screen, without jumps.
        internal static IEnumerator FoalsFollow()
        {
            Vector3 near = Spot(home, 3f) ?? home;
            Vector3? far = Spot(home, 22f);
            if (!far.HasValue)
            {
                Warn("no ground 22 units around the keeper: skipped");
                yield break;
            }
            MountHandle p = Spawn(near, right: true);
            if (p == null)
            {
                Fail("could not spawn the parent");
                yield break;
            }
            yield return Until(() => p.IsReady, 6f);
            var foals = new List<MountHandle>();
            for (int k = 0; k < 2; k++)
            {
                MountHandle f = Mounts.SpawnFoal(Owner, p, 0.6f, persist: false);
                if (f != null)
                {
                    f.Tuning.Set(Owner, "ShuffleFollowAlways", true); // trot however far, so a jump would be a failure
                    foals.Add(f);
                }
            }
            yield return Until(() => foals.TrueForAll(f => f.IsReady), 6f);
            if (foals.Count == 0 || !waited)
            {
                Fail("the foals never got their views");
                yield break;
            }
            MountResult ride = p.Ride(showMessage: false);
            if (!ride.Ok)
            {
                Fail("could not ride the parent: " + ride.Reason);
                yield break;
            }
            yield return Frames(3);
            int jumps0 = Mount.CatchUpCount;
            yield return MoveKeeper(far.Value, homeFacing); // the parent goes with the keeper
            if (!moved) yield break;
            yield return Seconds(0.6f);
            bool allActive = true;
            float end = Time.unscaledTime + 25f;
            while (Time.unscaledTime < end && !abort)
            {
                foreach (MountHandle f in foals)
                {
                    if (f.Exists && f.I.tier != Mount.Tier.Active) allActive = false;
                }
                if (foals.TrueForAll(f => f.Exists && Vector3.Distance(f.Position, p.Position) < 4f))
                {
                    break;
                }
                yield return null;
            }
            var dist = new StringBuilder();
            bool close = true;
            foreach (MountHandle f in foals)
            {
                float dd = Vector3.Distance(f.Position, p.Position);
                close &= dd < 4f;
                dist.Append($" {f.FullId} {dd:F1}");
            }
            int jumps = Mount.CatchUpCount - jumps0;
            Check(allActive, "the foals stayed active while their parent was");
            yield return WorldViewsRight();
            if (close && jumps == 0) Pass($"both reached their parent 22 away by trotting, no jumps (distances{dist})");
            else if (close) Warn($"both reached their parent, with {jumps} jump(s) (blocked on the way?){dist}");
            else Fail($"not with their parent after 25 s (distances{dist}; {jumps} jump(s))");
            Mounts.Dismount("self-test");
            yield return Frames(2);
        }

        // Spawning and removing many, twice: every count of what the mod makes itself comes back to where it was.
        internal static IEnumerator Leaks()
        {
            yield return Settle(15f); // the world's own donkeys still waking and sleeping after the last scenario's moves
            int tex0 = Live.Textures, mat0 = Live.Materials, lamp0 = Live.LampObjects, mesh0 = Live.Meshes, cargo0 = Live.CargoHolders;
            int shared0 = Colors.SharedTextures.Count, inst0 = Mount.All.Count;
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                var made = new List<MountHandle>();
                for (int k = 0; k < 80; k++)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 40f;
                    MountHandle h = Spawn(home + new Vector3(r.x, 0f, r.y), k % 2 == 0, withCart: k % 10 == 0);
                    if (h != null) made.Add(h);
                }
                yield return Seconds(4f);
                foreach (MountHandle h in made)
                {
                    if (h.Exists) h.Remove();
                }
                yield return Seconds(2f);
                Info($"cycle {cycle}: {made.Count} spawned and removed; pool {ViewPool.Count}");
            }
            yield return Settle(15f);
            Check(Mount.All.Count == inst0, $"instances back ({inst0} -> {Mount.All.Count})");
            Check(Live.Materials == mat0, $"colour materials back ({mat0} -> {Live.Materials})");
            Check(Live.Textures == tex0 && Colors.SharedTextures.Count == shared0, $"colour textures back ({tex0} -> {Live.Textures}, shared {shared0} -> {Colors.SharedTextures.Count})");
            Check(Live.LampObjects == lamp0 && Live.Meshes == mesh0, $"lamps and meshes back ({lamp0}/{mesh0} -> {Live.LampObjects}/{Live.Meshes})");
            Check(Live.CargoHolders == cargo0, $"cargo holders back ({cargo0} -> {Live.CargoHolders})");
        }

        // Informational: the frame time at the keeper's spot with 300 test donkeys around, in each mode. The camera
        // doesn't move, so the modes compare.
        internal static IEnumerator FrameTimes()
        {
            var made = new List<MountHandle>();
            for (int k = 0; k < 300; k++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * 60f;
                MountHandle h = Spawn(home + new Vector3(r.x, 0f, r.y), k % 2 == 0);
                if (h != null) made.Add(h);
            }
            yield return Seconds(6f);
            var ms = new double[4];
            string[] names = { "levels on, idle switched off", "levels on, idle frozen", "levels off", "levels on again" };
            for (int mode = 0; mode < 4; mode++)
            {
                Lod.On = mode != 2;
                Lod.IdleSwitchesOff = mode != 1;
                yield return Seconds(mode == 2 ? 6f : 3f); // levels off: every view is made again, one a frame
                double sum = 0;
                int n = 0;
                float end = Time.unscaledTime + 3f;
                while (Time.unscaledTime < end && !abort)
                {
                    sum += Time.unscaledDeltaTime * 1000.0;
                    n++;
                    yield return null;
                }
                ms[mode] = n > 0 ? sum / n : 0;
                Info($"{names[mode]}: {ms[mode]:F2} ms a frame ({(ms[mode] > 0 ? 1000.0 / ms[mode] : 0):F0} fps) with {made.Count} test donkeys, the mod {Perf.AvgMs[(int)Perf.S.Tick]:F2} ms");
            }
            if (ms[2] > 0)
            {
                Info($"levels save {ms[2] - ms[0]:F2} ms a frame here; switching off instead of freezing saves {ms[1] - ms[0]:F2} ms");
            }
            Lod.On = true;
        }
    }
}
