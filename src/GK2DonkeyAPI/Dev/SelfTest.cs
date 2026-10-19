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

        private static int pass, fail, warn, skip;
        private static bool abort;
        private static int spawnSeq;

        // The run as a whole, for the panel's progress: the scenarios in it, how many are done, when it started, and
        // the tallies so far.
        internal static int PassCount => pass;
        internal static int FailCount => fail;
        internal static int WarnCount => warn;
        internal static int RunCount { get; private set; }
        internal static int RunDone { get; private set; }
        internal static float RunStarted { get; private set; }
        internal static Scenario Current => running;

        private static bool startRequested;
        private static Func<Scenario, bool> pick; // which scenarios the requested run takes; null = all

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

        private const string GroupCore = "API, side file, carts, colours";
        private const string GroupLevels = "Levels and views";
        private const string GroupChest = "Chest on the cart";
        private const string GroupCargo = "Cargo in layers";
        private const string GroupMorgue = "Morgue";
        private const string GroupCorpses = "Corpses at the crossroads";
        private const string GroupLooks = "Skins and effects";

        // Every scenario, in run order (docs/self-tests.md). The panel's Tests tab lists them from here.
        internal static readonly Scenario[] Catalog =
        {
            new Scenario { Group = GroupCore, Name = "the per-mod API (ids, keys, data, owner rules)", Body = ApiIdentity },
            new Scenario { Group = GroupCore, Name = "kept-with-save text, round trip", Body = RoundTrip },
            new Scenario { Group = GroupCore, Name = "hitch and unhitch", Body = HitchUnhitch },
            new Scenario { Group = GroupCore, Name = "unhitch without a view (a parked cart stays)", Body = UnhitchWithoutView },
            new Scenario { Group = GroupCore, Name = "shared colour textures", Body = SharedColours },
            new Scenario { Group = GroupCore, Name = "settings: a moved section keeps a player's values", Body = MovedSections },
            new Scenario { Group = GroupCore, Name = "upgrade: a released version's player files are all read", Body = UpgradeFiles },
            new Scenario { Group = GroupLevels, Name = "one new view per frame", Body = SpawnCap },
            new Scenario { Group = GroupLevels, Name = "waking (view switched off)", Body = () => Wake(switchOff: true) },
            new Scenario { Group = GroupLevels, Name = "waking (view frozen)", Body = () => Wake(switchOff: false) },
            new Scenario { Group = GroupLevels, Name = "mounted while idle (Saddle Up's H)", Body = MountWhileIdle },
            new Scenario { Group = GroupLevels, Name = "reused views", Body = Reuse },
            new Scenario { Group = GroupLevels, Name = "foals keep up off screen", Body = FoalsFollow },
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
            new Scenario { Group = GroupLooks, Name = "mounts off: only the donkey offered, registrations kept out", Body = MountsOff },
            new Scenario { Group = GroupLooks, Name = "skin: on, off, removed (pictures loaded and freed)", Body = SkinOnOff },
            new Scenario { Group = GroupLooks, Name = "skin pack: a folder read, validated, worn, withdrawn", Body = SkinPack },
            new Scenario { Group = GroupLooks, Name = "skins: no leaks over spawn and remove", Body = SkinLeaks },
            new Scenario { Group = GroupLooks, Name = "recipe: built from the donkey's frames, worn whole", Body = SkinRecipe },
            new Scenario { Group = GroupLooks, Name = "effect: hellfire lit, follows, out, gone with the donkey", Body = EffectOnOff },
            new Scenario { Group = GroupLooks, Name = "mount kind from a pack: declared, worn, no cart, its tuning, withdrawn", Body = KindPack },
            new Scenario { Group = GroupLooks, Name = "mount kind from code: the ability's hooks on spawn, ride, dismount, removal", Body = KindAbility },
            new Scenario { Group = GroupLooks, Name = "settings: the Mount, Skin, babies and effect rows", Body = SettingsRows },
            new Scenario { Group = GroupLooks, Name = "skins and kinds: the changed events", Body = ChangedEvents },
            new Scenario { Group = GroupLooks, Name = "game creature: Gerry's frames found, scaled and worn", Body = GameCreature },
            new Scenario { Group = GroupLooks, Name = "colours: the player's regions on a mount in its own colours", Body = SkinColours },
            new Scenario { Group = GroupLooks, Name = "colours: a skin over the donkey keeps the donkey's regions", Body = SkinOverColours },
            new Scenario { Group = GroupLooks, Name = "skin pack: read again by itself when its files change (dev switch)", Body = SkinPackAutoReload },
        };

        // The groups in the order they first appear in the catalog (after Catalog: static fields initialise in order).
        internal static readonly string[] Groups = DistinctGroups();

        private static string[] DistinctGroups()
        {
            var g = new List<string>();
            foreach (Scenario s in Catalog)
            {
                if (!g.Contains(s.Group)) g.Add(s.Group);
            }
            return g.ToArray();
        }

        private static Scenario running;

        private static IEnumerator Run()
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
            bool lodOn = Lod.On, idleOff = Lod.IdleSwitchesOff;
            int perFrame = Lod.SpawnsPerFrame;
            // A test load left on the cart from the panel (TestLoads.Put, e.g. the review's Load it) holds every cart at
            // its size until Take off; the scenarios check the setting alone, so the floor is off for the run and back
            // after it. Found 2026-10-16: "two off: the size follows the setting (2 on it, 3 slots)" after a review session.
            int slotFloorWas = Cargo.SlotFloor;
            Cargo.SlotFloor = 0;
            // Where the keeper stood: he is put back there at the end. The run itself starts from the remembered spot
            // (Debug.SelfTestSpot, written by a run that passed everywhere) when it is in this scene.
            Vector3 origin = Keeper();
            float originFacing = pc.PlayerData.Direction.x >= 0f ? 1f : -1f;
            current = "start";
            if (TryParseSpot(Plugin.SelfTestSpot.Value, out string spotScene, out Vector3 spot) && spotScene == scene.Id && Vector3.Distance(spot, origin) > 1f)
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
            // Not inside a container zone (a built-up area with chests and crafting, the kind a chest can belong to;
            // the player's own zone readout counts other kinds too): into the nearest one, at a free spot with ground.
            if (ZoneAt(Keeper(), scene) == null)
            {
                Vector3? into = SpotInNearestZone(Keeper(), scene, out WorldZoneData intoZone);
                if (into.HasValue)
                {
                    PutKeeper(into.Value, originFacing);
                    yield return Frames(2);
                    if (Vector3.Distance(Keeper(), into.Value) < 1f)
                    {
                        Info($"the keeper stood in no container zone; moved into {intoZone.id} at {into.Value:F1} (back to {origin:F1} at the end)");
                    }
                    else
                    {
                        Warn($"the spot in {intoZone.id} at {into.Value:F1} didn't take (he is at {Keeper():F1}); running from here");
                    }
                }
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

            // The world's own instances (not the tests'): all must come through the run as they were.
            var before = new Dictionary<string, string>();
            foreach (Mount.Instance i in Mount.All)
            {
                if (!IsTestOwner(i.OwnerId) && !i.Ephemeral) before[i.FullId] = Sig(i);
            }

            // The scenarios of this run (all, one group, or one): their last results are cleared, the others' stand.
            var scenarios = new List<Scenario>();
            foreach (Scenario c in Catalog)
            {
                if (pick == null || pick(c)) scenarios.Add(c);
            }
            pick = null;
            bool full = scenarios.Count == Catalog.Length;
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
                Line($"[test] this run: {scenarios.Count} of {Catalog.Length} scenario(s): {string.Join("; ", scenarios.ConvertAll(c => c.Name))}");
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
            current = "the world's own donkeys";
            var after = new Dictionary<string, string>();
            foreach (Mount.Instance i in Mount.All)
            {
                if (!IsTestOwner(i.OwnerId) && !i.Ephemeral) after[i.FullId] = Sig(i);
            }
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
                ? $"all {before.Count} came through unchanged (owner, number, scene, kind, kept)"
                : $"{changed.Count} changed: {string.Join("; ", changed.GetRange(0, Math.Min(8, changed.Count)))}");
            int leftover = 0;
            foreach (Mount.Instance i in Mount.All)
            {
                if (IsTestOwner(i.OwnerId)) leftover++;
            }
            Check(leftover == 0, $"no test donkey left behind ({leftover})");
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
            string summary = $"{(abort ? "aborted" : "done")}{(full ? "" : $" ({scenarios.Count} of {Catalog.Length} scenarios)")}: {pass} passed, {fail} failed, {warn} warning(s){(skip > 0 ? $", {skip} scenario(s) skipped" : "")}.";
            Line("[test] " + summary);
            Status = summary;
        }

        // Runs a coroutine with its nested coroutines itself, so an exception anywhere ends only that scenario (FAIL)
        // and the run goes on (Unity would stop the whole coroutine).
        private static IEnumerator Drive(IEnumerator root)
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

        private static string current = "";

        private static void Line(string s)
        {
            Lines.Add(s);
            Plugin.Log.LogInfo(s);
        }

        private static void Pass(string what) { pass++; if (running != null) running.Pass++; Line($"[test] PASS {current}: {what}"); }

        private static void Fail(string what) { fail++; if (running != null) running.Fail++; Line($"[test] FAIL {current}: {what}"); }

        private static void Warn(string what) { warn++; if (running != null) running.Warn++; Line($"[test] WARN {current}: {what}"); }

        private static void Info(string what) => Line($"[test] INFO {current}: {what}");
        // A scenario that can't run in this setup (the mounts switched off, say): said, counted apart, not a warning.
        private static void Skip(string why) { skip++; if (running != null) running.Skipped = true; Line($"[test] SKIP {current}: {why}"); }

        private static void Check(bool ok, string what)
        {
            if (ok) Pass(what); else Fail(what);
        }

        // ------------------------------------------------------------------ helpers

        private static Vector3 home;
        private static float homeFacing;
        private static bool waited;

        private static PlayerController PC => MainGame.PlayerController;

        private static Vector3 Keeper() => PC.PlayerData.position.Value;

        // "scene|x,y,z" (Debug.SelfTestSpot), invariant culture.
        private static bool TryParseSpot(string s, out string scene, out Vector3 p)
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

        private static void PutKeeper(Vector3 p, float facing)
        {
            PC.SetPosition(p);
            PC.PhysicalBody.SetFacingDirection(new Vector2(facing, 0f));
        }

        // The keeper moved and checked two frames later: a spot he can't stand on pushes him elsewhere, and the checks
        // after it would run against a keeper who isn't where they think (moved false: the scenario skips).
        private static bool moved;

        private static IEnumerator MoveKeeper(Vector3 p, float facing)
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

        private static readonly Collider[] overlap = new Collider[8];

        // Nothing of the keeper-blocking layer (8, Obstacle) where he would stand.
        private static bool FreeSpot(Vector3 p)
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
        private static Vector3? Spot(Vector3 from, float dist, Func<Vector3, bool> also = null)
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
        private static Vector3? SpotInNearestZone(Vector3 p, GameScene scene, out WorldZoneData zone)
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
                Func<Vector3, bool> inside = q => ZoneAt(q, scene) == want;
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

        private static float RectDistance(Rect r, Vector2 q)
        {
            float dx = Mathf.Max(r.xMin - q.x, 0f, q.x - r.xMax);
            float dy = Mathf.Max(r.yMin - q.y, 0f, q.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // The building zone a world object placed at p would belong to (the game's own test: an active container
        // zone whose rectangle holds the point, highest processing priority first), or null.
        private static WorldZoneData ZoneAt(Vector3 p, GameScene scene)
        {
            GameSceneData sd = MainGame.WorldData.GetGameSceneDataById(scene.Id);
            if (sd?.WorldZonesByProcessingPriority == null) return null;
            var xz = new Vector2(p.x, p.z);
            foreach (var kv in System.Linq.Enumerable.Reverse(sd.WorldZonesByProcessingPriority))
            {
                foreach (WorldZoneData z in kv.Value)
                {
                    if (z.IsContainer && z.IsActive && z.wholeZoneRect.Contains(xz)) return z;
                }
            }
            return null;
        }

        // The levels have settled: no view made or released and no level changed for 2 s (at most timeout seconds).
        private static IEnumerator Settle(float timeout)
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

        private static IEnumerator Until(Func<bool> cond, float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (!cond() && Time.unscaledTime < end && !abort)
            {
                yield return null;
            }
            waited = cond();
        }

        private static IEnumerator Frames(int n)
        {
            for (int k = 0; k < n; k++)
            {
                yield return null;
            }
        }

        private static IEnumerator Seconds(float s)
        {
            float end = Time.unscaledTime + s;
            while (Time.unscaledTime < end && !abort)
            {
                yield return null;
            }
        }

        // Two more test owners for the API's per-mod rules (not the core's: they can't change each other's instances).
        private const string OwnerA = Owner + ".a", OwnerB = Owner + ".b";

        private static bool IsTestOwner(string o) => o == Owner || o == OwnerA || o == OwnerB;

        // What must not change for the world's own instances during the run.
        private static string Sig(Mount.Instance i) => $"{i.OwnerId}|{i.SceneId}|{i.HasDonkey}|{i.HasCart}|{i.Persist}|{i.Key}";

        private static MountHandle Spawn(Vector3 at, bool right, float scale = 1f, bool withCart = false, string owner = Owner,
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

        private static IEnumerator CleanUp()
        {
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

        private static bool IsActive(MountHandle h) => h.Exists && h.I.tier == Mount.Tier.Active;

        private static string Dir(float? d) => d.HasValue ? d.Value.ToString("F0") : "none";

        // Every instance in the world with its view on: the view's size is its own, and its cart parts match whether it
        // has a cart. A reused view, taken by whichever donkey woke first, must fit that donkey (any owner).
        private static IEnumerator WorldViewsRight()
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
        private static string State(MountHandle h)
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
        private static IEnumerator ApiIdentity()
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
        private static IEnumerator RoundTrip()
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
            cartDonkey.Tuning.Set(Owner, "Skin", "dragon"); // a skin whose mount sets a cart-style value (HitchGapPx): the skin id is kept, the mount's value is not
            if (Plugin.MountsOn) yield return Until(() => cartDonkey.Tuning.IsSet("HitchGapPx"), 6f); // the skin's rig merges the dragon's cart-style values once the view is up
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
                if (r.Donkey != i.HasDonkey || r.Cart != i.HasCart) bad.Add("kind");
                if (r.Scene != i.SceneId) bad.Add("scene");
                if ((r.Position - i.Position).sqrMagnitude > 1e-6f) bad.Add($"position {r.Position}");
                if (r.FacingRight != i.FacingRight) bad.Add("facing");
                if (Mathf.Abs(r.Scale - i.ViewScale) > 1e-5f || r.Rideable != i.Rideable) bad.Add("scale or rideable");
                if (r.RemoveWithParent != i.RemoveWithParent) bad.Add("removeWithParent");
                string wantParent = i.Parent != null ? i.Parent.FullId : null;
                if (r.ParentFull != wantParent) bad.Add($"parent {r.ParentFull}");
                foreach (var kv in i.Overrides)
                {
                    bool mountsOwn = i.OverrideOwners.TryGetValue(kv.Key, out string by) && by == Looks.TuningOwner;
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
                Check(bad.Count == 0, $"{i.FullId} ({r.Kind}) comes back the same{(bad.Count > 0 ? ": " + string.Join(", ", bad) : "")}");
            }
            Mount.Record pr = records.Find(x => x.Key == "rt parent");
            Check(pr != null && pr.DataLines.Exists(x => x[1] == "tricky" && x[2] == v2), "a value with a tab, a line break and a backslash comes back exactly");
            Mount.Record cr = records.Find(x => x.Key == "rt cart");
            Check(cr != null && cr.Sets.Exists(x => x[0] == "Skin" && x.Length > 1 && x[x.Length - 1] == "dragon"), "the skin id is in the record (Skin = dragon)");
            if (Plugin.MountsOn) Check(cartDonkey.Tuning.IsSet("HitchGapPx") && cartDonkey.Tuning.SetBy("HitchGapPx") == Looks.TuningOwner && cr != null && !cr.Sets.Exists(x => x[0] == "HitchGapPx"), "the dragon's own hitch gap is on the wearer (cart style) but not in the record");
            else Info("the mounts are off (Debug.MountsPreview): the dragon isn't registered, so its hitch gap on the wearer isn't checked (the id is kept all the same)");
        }

        // A donkey takes a free cart and leaves it again: one instance with the cart (the cart's data merged, its parts
        // on), then a new free cart of the same owner and kept setting, the donkey's cart parts off.
        private static IEnumerator HitchUnhitch()
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
            d.Tuning.Clear("HitchGapPx");
            yield return Frames(3);
            Check(Mathf.Abs(Along(d) - plain) < 0.03f, $"gap cleared: the prefab's distance again ({Along(d):F2})");
        }

        // A standing donkey with its cart far away has no view (asleep): its cart is left where it stands all the same,
        // on the data alone (Saddle Up's H leaves a parked cart this way), and the donkey called to the keeper comes
        // without cart parts while the cart stays put with the cargo.
        private static IEnumerator UnhitchWithoutView()
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
        private static IEnumerator SharedColours()
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

        // A mod moving a section on its settings page (Saddle Up 0.11: Mount right after Keys) renumbers the cfg
        // sections ("3. Donkey" -> "4. Donkey" here); a player's cfg from before keeps its values: a key the file lacks
        // under its section takes the value under the same name with another number (MountSettingsPage.Bind), the
        // section's own value wins when it has one, a key the file has nowhere keeps its default, of several old
        // copies the fullest (then the highest-numbered) wins, a key moved to another section (MovedKey) is found
        // there, and the old copies are gone from the file after a save (BepInEx writes orphans back otherwise).
        private static IEnumerator MovedSections()
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
                Check(refused && !page.Rows.Exists(r => r.Kind == MountSettingsPage.Kind.Note && r.Label == "Picture"), "a row with a picture row's label is refused");
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
        // docs/compatibility.md): an update must read every value a player has on disk. Saddle Up's cfg goes through
        // the live page's own carry-over (renumbered sections, moved keys): every line of the old file lands on a row
        // of today's page and reads as the row's type (an enum name that changed shows here). The API's cfg: every
        // key is still bound by this build and reads as its type. The side file: the reader takes every record whole.
        // And a side file of a format this version doesn't read is set aside, never left for the next save to delete.
        private static IEnumerator UpgradeFiles()
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

                // Saddle Up's cfg through the live page (its real sections, keys and MovedKey calls)
                string su = System.IO.Path.Combine(dir, "dreas.gk2.saddleup.cfg");
                if (System.IO.File.Exists(su))
                {
                    MountSettingsPage page = MountSettingsPage.All.Find(p => p.Config != null && string.Equals(System.IO.Path.GetFileName(p.Config.ConfigFilePath), "dreas.gk2.saddleup.cfg", StringComparison.OrdinalIgnoreCase));
                    if (page == null)
                    {
                        Warn($"{version}: GK2 Saddle Up isn't loaded, its cfg isn't checked");
                    }
                    else
                    {
                        Dictionary<string, Dictionary<string, string>> file = MountSettingsPage.ReadSections(su);
                        int n = 0;
                        foreach (var s in file) n += s.Value.Count;
                        List<MountSettingsPage.UpgradeClaim> claims = page.Upgrade(file, out List<(string Section, string Key, string Raw)> unclaimed);
                        var lost = new StringBuilder();
                        foreach (var u in unclaimed) lost.Append(lost.Length > 0 ? ", " : "").Append('[').Append(u.Section).Append("] ").Append(u.Key);
                        Check(unclaimed.Count == 0, $"{version} Saddle Up cfg: every one of its {n} values lands on a row of today's page" + (unclaimed.Count > 0 ? $"; lost: {lost}" : ""));
                        var bad = new StringBuilder();
                        int found = 0, moved = 0;
                        foreach (MountSettingsPage.UpgradeClaim c in claims)
                        {
                            if (c.Raw == null) continue;
                            found++;
                            if (c.Moved) moved++;
                            try
                            {
                                TomlTypeConverter.ConvertToValue(c.Raw, c.Row.Entry.SettingType);
                            }
                            catch (Exception e)
                            {
                                bad.Append(bad.Length > 0 ? "; " : "").Append('[').Append(c.Section).Append("] ").Append(c.Key).Append(" = ").Append(c.Raw).Append(" as ").Append(c.Row.Entry.SettingType.Name).Append(": ").Append(e.Message);
                            }
                        }
                        Check(bad.Length == 0, $"{version} Saddle Up cfg: every carried value reads as its row's type (enum names, keys)" + (bad.Length > 0 ? $": {bad}" : ""));
                        Info($"{version} Saddle Up cfg: {found} values found, {moved} of them from a moved section or key; {claims.Count - found} rows are new since");
                    }
                }

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
                    int n = 0, lostCount = 0, badCount = 0;
                    foreach (var s in file)
                    {
                        foreach (var kv in s.Value)
                        {
                            n++;
                            var def = new ConfigDefinition(s.Key, kv.Key);
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
                    Check(lostCount == 0, $"{version} {name}: every one of its {n} keys is still bound (a renamed or dropped key resets a player's value)" + (lostCount > 0 ? $"; not bound: {lost}" : ""));
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
                        Check(parent != null && parent.Name == "Bella" && parent.Kind == "donkey" && !parent.FacingRight && parent.Position == new Vector3(12.5f, -3.25f, 0f) && parent.Sets.Count == 1 && parent.Sets[0][0] == "SeatAdjustPx" && parent.Sets[0][2] == "3",
                            "0.9.1: the named donkey with its position, facing and its own setting");
                        Check(parent != null && parent.DataLines.Count == 1 && parent.DataLines[0][2] == "a\ttab and a \\ backslash", "0.9.1: a mod's data line with its escaped tab and backslash");
                        Check(foal != null && foal.Kind == "foal" && Math.Abs(foal.Scale - 0.6f) < 0.001f && foal.ParentFull == parent?.FullId && foal.RemoveWithParent, "0.9.1: the foal of size 0.6 with its parent");
                        Check(cart != null && cart.Kind == "free cart" && cart.FacingRight && cart.Cargo.Count == 2 && cart.Chest == "6f1c2a9e-0000-4000-8000-000000000003", "0.9.1: the free cart with two items and its chest");
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

        // The settings rows every player mod gets (MountRows, FoalRows, EffectRows), on a throwaway page and a test
        // companion: the Mount dropdown picks a mount and its Skin dropdown that mount's skin; the companion's Skin
        // follows; one Skin row shows at a time; each mount remembers its own skin; the babies rows bind the companion's
        // foal values; the effect row defaults to "As the mount".
        private static IEnumerator SettingsRows()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): the rows need the built-in mounts (the off state is in 'mounts off')");
                yield break;
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
                page.EffectRows("Effect", me);
                ConfigEntry<T> Row<T>(string key) => page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == key)?.Entry as ConfigEntry<T>;
                ConfigEntry<string> mount = Row<string>("Mount"), skinDonkey = Row<string>("Skin_" + MountKinds.DonkeyId), skinDragon = Row<string>("Skin_dragon");
                Check(mount != null && skinDonkey != null && skinDragon != null, "the Mount row and a Skin row per mount are bound");
                if (mount == null || skinDragon == null) yield break;
                Check(mount.Value == MountKinds.DonkeyId && me.Skin == MountLooks.DonkeyId, $"defaults: the donkey, the game's own art (\"{me.Skin}\")");
                int visible = page.Rows.FindAll(r => r.Entry != null && r.Entry.Definition.Key.StartsWith("Skin_", StringComparison.Ordinal) && (r.Visible == null || r.Visible())).Count;
                Check(visible == 1, $"one Skin row shows at a time ({visible})");
                mount.Value = "dragon";
                Check(me.Skin == Looks.FirstSkinOf("dragon") && Mounts.Looks.Find(me.Skin)?.Mount == "dragon", $"Mount = dragon: the companion's skin is the dragon's first (\"{me.Skin}\")");
                skinDragon.Value = "dragon";
                Check(me.Skin == "dragon", $"Skin = Red Dragon: the companion follows (\"{me.Skin}\")");
                mount.Value = MountKinds.DonkeyId;
                Check(me.Skin == MountLooks.DonkeyId, $"back to the donkey: its own art again (\"{me.Skin}\")");
                mount.Value = "dragon";
                Check(me.Skin == "dragon", $"the dragon again: it remembered Red Dragon (\"{me.Skin}\")");
                ConfigEntry<int> foals = Row<int>("Foals");
                ConfigEntry<bool> wear = Row<bool>("FoalsWearSkin");
                Check(foals != null && wear != null && Row<bool>("FoalsIndoors") != null, "the three babies rows are bound");
                if (foals != null)
                {
                    foals.Value = 2;
                    Check(me.FoalCount == 2, $"Babies = 2 reaches the companion ({me.FoalCount})");
                    foals.Value = 0;
                }
                if (wear != null)
                {
                    wear.Value = false;
                    Check(!me.FoalsWearSkin, "Babies are the same mount and skin = off reaches the companion");
                    wear.Value = true;
                }
                ConfigEntry<string> effect = Row<string>("Effect");
                Check(effect != null && effect.Value == MountEffects.KindId, $"Effect defaults to As the mount (\"{effect?.Value}\")");

                // the look per mount (PerMount, MountLook): a row set per mount, the donkey's keys plain, the others
                // suffixed; the chosen mount's set is the one shown; a mount's look is kept while another is chosen
                mount.Value = MountKinds.DonkeyId; // the checks below assume the donkey chosen (the test left the dragon chosen above and failed on it, 2026-10-19)
                int sets = 0;
                page.PerMount((id, sfx, shown) =>
                {
                    sets++;
                    MountLook look = me.LookOf(id);
                    ConfigEntry<CoatColor> coatRow = page.Choice("Donkey", "Coat" + sfx, look.Coat, "Coat colour", "", v => look.Coat = v, new[] { "Game default", "Brown", "Dark", "Cream", "Black", "Custom" }, look: true);
                    page.Color("Donkey", "CustomCoat" + sfx, "Custom coat colour", "", PaletteColor.Chestnut, v => look.CustomCoat = v, () => coatRow.Value == CoatColor.Custom);
                });
                ConfigEntry<CoatColor> coatDonkey = Row<CoatColor>("Coat"), coatDragon = Row<CoatColor>("Coat_dragon");
                ConfigEntry<int> lightDragon = Row<int>("CustomCoatLightness_dragon");
                Check(sets == Mounts.Kinds.Kinds.Count && coatDonkey != null && coatDragon != null && lightDragon != null, $"one row set per mount ({sets}): the donkey's keys plain, the dragon's suffixed (CustomCoatLightness_dragon too)");
                MountSettingsPage.Row rowDragon = page.Rows.Find(r => r.Entry == coatDragon), rowCustomDragon = page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == "CustomCoat_dragon");
                Check(rowDragon != null && rowDragon.Shown != null && rowDragon.Enabled == null && rowCustomDragon != null && rowCustomDragon.Shown != null && rowCustomDragon.Enabled != null, "a per-mount row knows which mount shows it; the custom colour row keeps its own condition apart (Enabled)");
                Check(rowDragon != null && !rowDragon.Shown() && !rowDragon.Visible(), "with the donkey chosen the dragon's Coat row isn't shown");
                coatDragon.Value = CoatColor.Custom;
                Check(me.LookOf("dragon").Coat == CoatColor.Custom && me.Coat == CoatColor.Game, $"the dragon's coat set to Custom while the donkey is chosen: kept on the dragon's look, the current (donkey's) coat still {me.Coat}");
                Check(rowCustomDragon != null && rowCustomDragon.Enabled() && !rowCustomDragon.Visible(), "its custom colour row is enabled by its preset but not shown (the dragon isn't chosen)");
                mount.Value = "dragon";
                Check(me.MountId == "dragon" && me.Coat == CoatColor.Custom, $"Mount = dragon: the current look is the dragon's ({me.MountId}: {me.Coat})");
                Check(rowDragon.Shown() && rowDragon.Visible() && rowCustomDragon.Visible(), "the dragon's rows are the shown ones now");
                coatDragon.Value = CoatColor.Game;
                mount.Value = MountKinds.DonkeyId;
            }
            finally
            {
                try { System.IO.File.Delete(path); } catch { }
            }
            yield break;
        }

        // Mounts.Looks.SkinsChanged and Mounts.Kinds.KindsChanged fire on a register and on an unregister.
        private static IEnumerator ChangedEvents()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): the API keeps registrations out");
                yield break;
            }
            int skins = 0, kinds = 0;
            Action onSkins = () => skins++;
            Action onKinds = () => kinds++;
            Mounts.Looks.SkinsChanged += onSkins;
            Mounts.Kinds.KindsChanged += onKinds;
            try
            {
                IDisposable skin = Mounts.Looks.RegisterRecipe(Owner, "evt-skin", "Event Skin", f => f.Pixels);
                Check(skins >= 1, $"SkinsChanged fired on the register ({skins})");
                IDisposable kind = Mounts.Kinds.Register(Owner, new MountKindDef { Id = "evt-kind", Name = "Event Kind" });
                Check(kinds >= 1, $"KindsChanged fired on the register ({kinds})");
                int skinsBefore = skins, kindsBefore = kinds;
                skin?.Dispose();
                kind?.Dispose();
                Check(skins > skinsBefore && kinds > kindsBefore, $"both fired again on the unregister ({skinsBefore} -> {skins}, {kindsBefore} -> {kinds})");
                Check(Mounts.Looks.Find("evt-skin") == null && Mounts.Kinds.Find("evt-kind") == null, "both are gone");
            }
            finally
            {
                Mounts.Looks.SkinsChanged -= onSkins;
                Mounts.Kinds.KindsChanged -= onKinds;
            }
            yield break;
        }

        // A game-creature skin (LooksGame.cs): Gerry's 0901_ frames are found in memory or through a sprite atlas,
        // every base key gets a picture (the mapped ones drawn, the head keys empty on purpose), scaled onto the 128
        // canvas, and a donkey wears it as the Gerry mount.
        private static IEnumerator GameCreature()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): needs the built-in Gerry");
                yield break;
            }
            if (Looks.Find("gerry") == null)
            {
                Fail("the built-in skin 'gerry' isn't registered");
                yield break;
            }
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null)
            {
                Fail("could not spawn");
                yield break;
            }
            a.Tuning.Set(Owner, "Skin", "gerry");
            // Wearing is set when the skin is acquired; the sheet fills a few frames a tick (44 reads with a blit each),
            // and the pictures' counts are known once it is whole (Ready): wait for that, not for the acquire.
            yield return Until(() => a.IsReady && a.I.TestSkinWearing() == "gerry" && Looks.TestCellOpaque("gerry", "bdy_static_left") >= 0, 30f);
            if (!waited)
            {
                Fail($"the skin never became whole ({Looks.LastGameSourceNote})");
                a.Remove();
                yield break;
            }
            yield return Frames(3);
            Check(Looks.LastGameSourceFound >= 60, $"Gerry's frames found: {Looks.LastGameSourceNote} (73 expected)");
            Check(a.Kind == "gerry", $"the wearer is the Gerry mount ({a.Kind})");
            int rest = Looks.TestCellOpaque("gerry", "bdy_static_left");
            int walk = Looks.TestCellOpaque("gerry", "bdy_walk_left_05");
            int turn = Looks.TestCellOpaque("gerry", "bdy_turn_left_03");
            int head = Looks.TestCellOpaque("gerry", "hed_static_down");
            Check(rest > 9 * 100, $"the rest picture is drawn and scaled 3x ({rest} opaque pixels; Gerry's skull is about 200 pixels, so 1800 or more)");
            Check(walk > 0 && turn > 0, $"the walk ({walk}) and the somersault ({turn}) pictures are drawn");
            Check(head == 0, $"the head key is empty on purpose, so nothing of the donkey's head shows ({head})");
            Check(a.Tuning.IsSet("SeatAdjustPx") && a.Tuning.SetBy("SeatAdjustPx") == Looks.TuningOwner, "the Gerry mount's seat value is on the wearer");
            // seat = picture: the rider's seat is the skull picture's top at the pivot column, above the ground and
            // within the picture (160 px canvas: under 3.2 units) whatever the player's AutoSeat / SeatOffset
            bool seatOnPicture = a.I.LooksRig.TrySeatTop(out Vector3 seatTop);
            float seatUp = seatTop.y - a.Position.y;
            Check(seatOnPicture && seatUp > 0.2f && seatUp < 6f, $"the seat is the picture's own top, {seatUp:0.00} above the ground");
            // the babies' size lives on the mount: Gerry's 0.33 (its skin.cfg), the donkey's 0.6
            MountKindInfo gk = Mounts.Kinds.Find("gerry");
            Check(gk != null && Math.Abs(gk.FoalScale - 0.33f) < 0.001f, $"the Gerry mount's babies are 0.33x ({gk?.FoalScale})");
            MountKindInfo dk = Mounts.Kinds.Find(MountKinds.DonkeyId);
            Check(dk != null && Math.Abs(dk.FoalScale - 0.6f) < 0.001f, $"the donkey's babies are 0.6x ({dk?.FoalScale})");
            a.Remove();
            yield return Frames(3);

            // A Gerry asleep far away, with no view to draw the skin on, is still a Gerry: its kind comes from its
            // "Skin" key, not from the sheet it wears (2026-10-18: a sleeping parent counted as a donkey and its
            // babies stayed at 0.6 until it woke).
            Vector3? far = Spot(home, 50f) ?? Spot(home, 42f);
            if (far == null)
            {
                Warn("no spot 42-50 away for the sleeping check: skipped");
                yield break;
            }
            MountHandle s = Spawn(far.Value, true);
            if (s == null)
            {
                Fail("could not spawn far away");
                yield break;
            }
            s.Tuning.Set(Owner, "Skin", "gerry");
            yield return Until(() => s.I.tier == Mount.Tier.Asleep && !s.I.HasViewObject, 6f);
            if (!waited)
            {
                Warn($"not asleep without a view {Vector3.Distance(home, far.Value):F0} away (level {s.I.tier}): the sleeping check skipped");
            }
            else
            {
                Check(s.Kind == "gerry" && s.I.Noun == "gerry", $"asleep without a view it is still the Gerry mount ({s.Kind}, \"{s.I.Noun}\")");
                s.Tuning.Set(Owner, "Skin", "");
                Check(s.Kind == MountKinds.DonkeyId, $"the key cleared: a donkey again ({s.Kind})");
            }
            s.Remove();
            yield return Frames(2);
        }

        // The colour settings mean the same on every mount (docs/mount-colours.md): the worm (720 colours of its own,
        // none of the donkey's) on the test companion's paper donkey, its coat set to a green through the rows a
        // player uses, the settings picture rendered and read back: the pink is gone and the worm leans green; the
        // engine took the worm's own colours through the colour cube. Then the dragon on a spawned donkey: its
        // Muzzle setting changes the belly's colours only (its [Colors]).
        private static IEnumerator SkinColours()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): needs the built-in worm and dragon");
                yield break;
            }
            if (Looks.Find("worm") == null || Looks.Find("dragon") == null)
            {
                Warn("the built-in worm or dragon isn't registered: skipped");
                yield break;
            }
            const string owner = Owner + ".colours";
            MountCompanion me = Mounts.Companion(owner);
            me.Coat = CoatColor.Game;
            me.Skin = "worm";
            Mount.Instance paper = me.Paper;
            // the stage builds on the first ask (the donkey prefab loads over a few frames); ask every frame until a
            // picture of the worn worm comes
            yield return Until(() => PreviewStage.Picture(paper, withCart: false) != null && paper.LooksRig.Wearing == "worm" && !paper.LooksRig.Building, 20f);
            if (!waited)
            {
                Warn($"no settings picture of the worm within 20 s (stage failed: {PreviewStage.Failed}): skipped");
                me.Skin = "";
                yield break;
            }
            for (int i = 0; i < 4; i++) { PreviewStage.Picture(paper, withCart: false); yield return null; }
            float before = PreviewStage.TestDominantHue(false, out int pinkPixels, out string topBefore);
            // at night the preview stage takes the scene's blue-tinted light (seen 2026-10-19: the worm's outline 36141C read
            // back as 1E0A1C, red and green halved, blue kept; pink 350° came out violet 291°), so the as-drawn hue is
            // only meaningful by day; the mapping and the custom-coat checks below hold either way
            if (GK2DonkeyAPI.Effects.Night()) Info($"at night the preview is blue-lit ({before:0}°): the as-drawn hue check needs daylight; rerun by day (the picture at night is an open item)");
            else Check(pinkPixels > 100 && (before >= 320f || before <= 20f), $"the worm as drawn leans pink ({before:0}°, {pinkPixels} coloured pixels; most drawn: {topBefore})");
            var before2 = paper.ColorsRig.TestMountGroup();
            bool regionsAtDefault = true;
            foreach (string r in Colors.MountRegionKeys) regionsAtDefault &= !Colors.RegionChanged(r);
            if (regionsAtDefault) Check(before2.Colours > Colors.PaletteMaxColours && before2.Changed == 0, $"the game's colours: the worm's {before2.Colours} colours mapped, none changed");
            else Info($"the mount regions aren't at their defaults in the cfg (the Colors tab): the 'none changed' check skipped ({before2.Changed} of {before2.Colours} changed)");
            me.CustomCoat = "20C040";
            me.Coat = CoatColor.Custom;
            for (int i = 0; i < 4; i++) { PreviewStage.Picture(paper, withCart: false); yield return null; }
            float after = PreviewStage.TestDominantHue(false, out int greenPixels, out string topAfter);
            Check(greenPixels > 100 && after >= 80f && after <= 170f, $"a custom green coat: the picture leans green ({after:0}°, {greenPixels} coloured pixels; most drawn: {topAfter})");
            var g = paper.ColorsRig.TestMountGroup();
            Check(g.Colours > Colors.PaletteMaxColours && g.Method == SpriteColorMethod.Lut, $"{g.Colours} colours of its own: the colour cube ({g.Method})");
            Check(g.Pairs == g.Colours && g.Changed >= g.Colours - 8, $"every one of its colours is mapped ({g.Pairs} pairs), the coat's all changed ({g.Changed}; its 3 Dark colours may not)");
            me.Coat = CoatColor.Game;
            me.Skin = "";
            yield return Frames(2);

            // the dragon: Muzzle is its belly (skin.cfg [Colors]), and only that
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            if (a == null)
            {
                Fail("could not spawn");
                yield break;
            }
            a.Tuning.Set(Owner, "Skin", "dragon");
            yield return Until(() => a.IsReady && a.I.LooksRig.Wearing == "dragon" && !a.I.LooksRig.Building, 20f);
            if (!waited)
            {
                Fail("the dragon was never worn");
                a.Remove();
                yield break;
            }
            a.Tuning.Set(Owner, "MuzzleColorize", "00FF00");
            a.Tuning.Set(Owner, "MuzzleColorizeAmount", 1f);
            yield return Frames(3);
            var d = a.I.ColorsRig.TestMountGroup();
            Looks.Loaded look = a.I.LooksRig.WornOwnColours;
            int belly = look != null && look.ColoursByRegion.TryGetValue("Muzzle", out List<Color32> m) ? m.Count : -1;
            bool onlyBelly = d.Changed == belly && belly > 0;
            foreach (Color32 c in d.ChangedSources) onlyBelly &= look.ColoursByRegion["Muzzle"].Exists(x => x.r == c.r && x.g == c.g && x.b == c.b);
            Check(d.Method == SpriteColorMethod.Palette && d.Colours <= Colors.PaletteMaxColours, $"the dragon's {d.Colours} colours: the exact swap ({d.Method})");
            Check(onlyBelly, $"Muzzle changes the dragon's belly colours only ({d.Changed} changed of {d.Pairs}; the belly has {belly})");
            a.Remove();
            yield return Frames(2);
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
        }

        // swapOff: the same render through a copy of the material with both colour-swap keywords off (the picture as
        // drawn, lit): what differs from the normal render is the swap's doing.
        // with: a second renderer drawn in the same picture (the body part under an overlay: the real sorting decides
        // which shows). The lighting is the scene's (dark at night), so judge colours against another render of the
        // same thing, never against the colour in the picture.
        private static DrawnAlone RenderAlone(SpriteRenderer r, bool swapOff = false, SpriteRenderer with = null)
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
                cam.transform.position = r.bounds.center - main.transform.forward * 8f;
                rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
                Color32[] px = tex.GetPixels32();
                var d = new DrawnAlone();
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

        private static IEnumerator SkinOverColours()
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-over");
            string packDir = System.IO.Path.Combine(tempRoot, Looks.PackDirName, "over-test");
            const string owner = Owner + ".over";
            var red = new Color32(196, 30, 56, 255);
            var gold = new Color32(226, 168, 48, 255);
            try
            {
                if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
                System.IO.Directory.CreateDirectory(packDir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(packDir, "skin.cfg"), "[Skin]\nformat = 1\nid = over-test\nname = Over Test\nkind = own\nlayer = over\n[Colors]\nHarness = C41E38\n");
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
            a?.Tuning.Set(Owner, "Skin", "over-test");
            yield return Until(() => a != null && a.IsReady && a.I.LooksRig.Wearing == "over-test" && !a.I.LooksRig.Building, 10f);
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
                // keyword or a sprite that isn't drawn shows here and nowhere else (the Royal Parade plate of
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
                    Check(drawn.Blue >= 60 && drawn.Red == 0, $"the red pixels come out blue, the harness colour ({drawn.Blue} blue, {drawn.Red} red)");
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
                    a.MoveTo(near + new Vector3(4f, 0f, 0f));
                    int walked = 0, stale = 0, renders = 0, lost = 0, lostNoLift = 0, moving = 0;
                    Vector3 lastPos = a.Position;
                    for (int i = 0; i < 48 && part != null && part; i++)
                    {
                        yield return null;
                        walked++;
                        if (a.Position != lastPos) moving++;
                        lastPos = a.Position;
                        if (ov.sortingOrder != part.sortingOrder + 1) stale++;
                        if (i % 3 != 0) continue;
                        renders++;
                        DrawnAlone lifted = RenderAlone(ov, with: part);
                        if (lifted != null && lifted.Blue < 40) lost++;
                        Vector3 lp = ov.transform.localPosition;
                        ov.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
                        DrawnAlone flat = RenderAlone(ov, with: part);
                        ov.transform.localPosition = lp;
                        if (flat != null && flat.Blue < 40) lostNoLift++;
                    }
                    Info($"walking {walked} frames ({moving} with the position changing): the overlay's order stale on {stale}; of {renders} renders with the body it lost the depth test {lost} times with the lift and {lostNoLift} times without");
                    Check(stale == 0, $"the overlay's sorting order follows the part's every frame while walking ({stale} stale of {walked})");
                    Check(renders > 0 && lost == 0, $"the overlay never flickers into the body while walking ({lost} of {renders} renders lost with the lift; {lostNoLift} without it)");
                }
                // the player's own mount, when it wears a skin now: what the world draws of it, for the log
                foreach (MountHandle h in Mounts.InstancesOf("dreas.gk2.saddleup"))
                {
                    Looks.Rig own = h?.I?.LooksRig;
                    SpriteRenderer ownOv = own?.TestOverlay();
                    if (ownOv == null || !ownOv.enabled || ownOv.sprite == null) continue;
                    DrawnAlone od = RenderAlone(ownOv);
                    if (od != null) Info($"the player's mount wears '{own.Wearing}' ({ownOv.sprite.name}): drawn alone {od.Pixels} pixels: red {od.Red}, gold {od.Gold}, blue {od.Blue}, green {od.Green}, other {od.Other}; most: {od.Top}");
                    break;
                }
            }
            a?.Remove();
            yield return Frames(2);
            Looks.Unregister("over-test", owner);
            yield return Frames(2);
            try { System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        // Debug.SkinPackAutoReload: a worn pack (a temp folder, one red picture for every body frame) whose picture is
        // saved again in blue is read again by itself within a few seconds, and the donkey wears the blue one; with the
        // switch off, the same save changes nothing. The switch is put back as it was.
        private static IEnumerator SkinPackAutoReload()
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-watch");
            string packDir = System.IO.Path.Combine(tempRoot, Looks.PackDirName, "watch-test");
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
                Looks.Loaded l = Looks.LoadedOwnColours("watch-test");
                return l != null && l.Colours.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b);
            }
            try
            {
                if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
                System.IO.Directory.CreateDirectory(packDir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(packDir, "skin.cfg"), "[Skin]\nformat = 1\nid = watch-test\nname = Watch Test\nkind = own\n");
                Save(red, 0);
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            bool was = Plugin.SkinPackAutoReload.Value;
            Plugin.SkinPackAutoReload.Value = false;
            Check(Looks.RegisterFolder(tempRoot, owner) == 1, "the pack is found");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "watch-test");
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
            Looks.Unregister("watch-test", owner);
            yield return Frames(2);
            try { System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        // Debug.MountsPreview off (what players have in this version): the donkey is the only mount offered, its only
        // skin the game's own, no effect to pick, nothing built in registered, and a mod's registrations are kept out:
        // a handle to nothing, nothing thrown, nothing in the lists.
        private static IEnumerator MountsOff()
        {
            if (Plugin.MountsOn)
            {
                Skip("the mounts are on (Debug.MountsPreview): the switch isn't in effect");
                yield break;
            }
            IReadOnlyList<(string Id, string Label)> kinds = Looks.KindChoices();
            Check(kinds.Count == 1 && kinds[0].Id == MountKinds.DonkeyId, $"the Mount list holds the donkey only ({kinds.Count})");
            IReadOnlyList<(string Id, string Label)> skins = Looks.SkinChoicesOf(MountKinds.DonkeyId);
            Check(skins.Count == 1 && skins[0].Id == MountLooks.DonkeyId && skins[0].Label == Looks.DefaultLabel, $"the donkey's Skin list holds Default only ({skins.Count})");
            IReadOnlyList<(string Id, string Label)> effects = GK2DonkeyAPI.Effects.EffectChoices();
            Check(effects.Count == 2, $"the Effect list holds As the mount and None only ({effects.Count})");
            Check(Looks.Find("hellfire") == null && Looks.Find("rat") == null && Looks.Find("gerry") == null && Kinds.Find("dragon") == null && GK2DonkeyAPI.Effects.Find("hellfire") == null, "no built-in skin, mount or effect is registered");
            Check(!Mounts.MountsEnabled, "Mounts.MountsEnabled says so");
            int skinsBefore = Mounts.Looks.Skins.Count, kindsBefore = Mounts.Kinds.Kinds.Count, effectsBefore = Mounts.Effects.Effects.Count;
            IDisposable a = Mounts.Looks.RegisterRecipe(Owner, "off-skin", "Off Skin", f => f.Pixels);
            IDisposable b = Mounts.Kinds.Register(Owner, new MountKindDef { Id = "off-kind", Name = "Off Kind" });
            var fx = new MountEffectDef { Id = "off-fx", Name = "Off Fx" };
            var part = new MountEffectPart { Source = Mounts.Effects.GameParticles("flame_fx", "Assets/AddressableAssets/Materials/flame_fx.mat") };
            part.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 1 });
            fx.Parts.Add(part);
            IDisposable c = Mounts.Effects.Register(Owner, fx);
            int folder = Mounts.Looks.RegisterFolder(Owner, System.IO.Path.GetTempPath());
            Check(a != null && b != null && c != null, "a skin, a mount and an effect registration each return a handle (nothing thrown)");
            Check(Mounts.Looks.Skins.Count == skinsBefore && Mounts.Kinds.Kinds.Count == kindsBefore && Mounts.Effects.Effects.Count == effectsBefore
                && Looks.Find("off-skin") == null && Kinds.Find("off-kind") == null && GK2DonkeyAPI.Effects.Find("off-fx") == null, "none of them was registered");
            Check(folder == 0, $"a folder registration reports 0 packs ({folder})");
            a.Dispose();
            b.Dispose();
            c.Dispose();
            Check(Mounts.Looks.Skins.Count == skinsBefore && Mounts.Kinds.Kinds.Count == kindsBefore, "disposing the handles changes nothing");
            yield break;
        }

        // A built-in skin on a spawned donkey: drawn (its overlay shows, the donkey's own body kept from rendering),
        // its pictures loaded; cleared: the donkey's own body again; removed: the pictures freed.
        private static IEnumerator SkinOnOff()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): needs the built-in skins");
                yield break;
            }
            if (Looks.Find("rat") == null)
            {
                Warn("no built-in skin 'rat': skipped");
                yield break;
            }
            Vector3 near = Spot(home, 4f) ?? home;
            yield return Seconds(0.5f);
            int tex0 = Live.Textures, loaded0 = Looks.LoadedCount;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "rat");
            yield return Until(() => a != null && a.IsReady && a.I.TestSkinShowing(), 8f);
            Check(waited, "the rat skin shows (an overlay drawn, the donkey's own body kept from rendering)");
            Check(Looks.LoadedCount == loaded0 + 1 && Live.Textures == tex0 + 1, $"its pictures are loaded on one sheet ({tex0} -> {Live.Textures} textures)");
            a?.Tuning.Set(Owner, "Skin", "");
            yield return Frames(3);
            Check(a != null && !a.I.TestSkinShowing(), "cleared: the donkey's own body renders again");
            Check(Looks.LoadedCount == loaded0 && Live.Textures == tex0, $"cleared: its pictures freed ({Live.Textures} textures)");
            a?.Tuning.Set(Owner, "Skin", "rat");
            yield return Until(() => a != null && a.I.TestSkinShowing(), 4f);
            a?.Remove();
            yield return Frames(2);
            Check(Looks.LoadedCount == loaded0 && Live.Textures == tex0, $"removed while wearing it: pictures freed ({tex0} -> {Live.Textures})");
        }

        // A temp skin pack: skin.cfg as given, the rat's standing picture as its one frame. Returns the pack root.
        private static string WriteTempPack(string folder, string id, string cfg)
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), folder);
            string packDir = System.IO.Path.Combine(tempRoot, Looks.PackDirName, id);
            if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
            System.IO.Directory.CreateDirectory(packDir);
            using (var s = typeof(Looks).Assembly.GetManifestResourceStream("GK2DonkeyAPI.skins.rat\\bdy_static_left.png"))
            using (var f = System.IO.File.Create(System.IO.Path.Combine(packDir, "bdy_static_left.png")))
            {
                s.CopyTo(f);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(packDir, "skin.cfg"), cfg);
            return tempRoot;
        }

        // A pack's [Mount] section declares a kind (docs/mounts-design.md): listed with its skin, no cart, its tuning
        // (both styles' value on, the cart-only one off while standing) on the wearer owned by the skin system, the
        // hitch refused with the kind's name; the skin withdrawn: the kind stays listed, its tuning is off the donkey.
        private static IEnumerator KindPack()
        {
            const string owner = Owner + ".kindpack";
            string tempRoot;
            try
            {
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-kind", "kind-test",
                    "# a test pack\n[Skin]\nformat = 1\nid = kind-test\nname = Kind Test\nmount = test-beast\n\n[Mount]\nid = test-beast\nname = Test Beast\ncart = false\nfoals = false\nfoal_scale = 0.4\n\n[Mount.Tuning]\nRiderOffsetPx = 3\n\n[Mount.Tuning.Cart]\nSeatAdjustPx = 7\n\n[Colors]\nDark = 221A20\n");
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            int n = Looks.RegisterFolder(tempRoot, owner);
            Check(n == 1, $"one pack found ({n})");
            MountKindInfo k = Mounts.Kinds.Find("test-beast");
            Check(k != null && k.FromPack && !k.Cart && System.Linq.Enumerable.Contains(k.Skins, "kind-test"), "the pack declared the kind: listed, no cart, with its skin");
            Check(k != null && Math.Abs(k.FoalScale - 0.4f) < 0.001f, $"the pack's foal_scale is the kind's ({k?.FoalScale})");
            MountSkinInfo s = Mounts.Looks.Find("kind-test");
            Check(s != null && s.Mount == "test-beast", "the skin names the kind");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "kind-test");
            yield return Until(() => a != null && a.IsReady && a.Kind == "test-beast", 8f);
            Check(waited, "a donkey wearing the skin is of the kind");
            yield return Frames(2);
            Check(a != null && a.Tuning.IsSet("RiderOffsetPx") && a.Tuning.Get<int>("RiderOffsetPx") == 3 && a.Tuning.SetBy("RiderOffsetPx") == Looks.TuningOwner, "the kind's tuning (both styles) is on the wearer, owned by the skin system");
            Check(a != null && !a.Tuning.IsSet("SeatAdjustPx"), "the cart-only value is not on it while it stands for riding");
            // precedence: a mod's own value on the donkey wins over the creature's; cleared, the creature's returns
            a?.Tuning.Set(Owner, "RiderOffsetPx", 11);
            yield return Frames(3);
            Check(a != null && a.Tuning.Get<int>("RiderOffsetPx") == 11 && a.Tuning.SetBy("RiderOffsetPx") == Owner, "a mod's value set on the donkey wins over the creature's and stays");
            a?.Tuning.Clear("RiderOffsetPx");
            yield return Frames(3);
            Check(a != null && a.Tuning.Get<int>("RiderOffsetPx") == 3 && a.Tuning.SetBy("RiderOffsetPx") == Looks.TuningOwner, "cleared: the creature's value is back on it");
            MountHandle cart = Mounts.SpawnCart(Owner, near + new Vector3(2.5f, 0f, 0f));
            yield return Until(() => cart != null && cart.IsReady, 6f);
            MountResult hitch = a != null && cart != null ? Mounts.Hitch(a, cart) : MountResult.Refused("not spawned");
            Check(!hitch.Ok && hitch.Reason.IndexOf("test beast", StringComparison.OrdinalIgnoreCase) >= 0, "the hitch is refused by the kind: " + hitch.Reason);
            // a mount without foals: a foal given the skin stays a donkey
            MountHandle foal = Mounts.SpawnFoal(Owner, a, 0.6f);
            foal?.Tuning.Set(Owner, "Skin", "kind-test");
            yield return Until(() => foal != null && foal.IsReady, 6f);
            yield return Frames(3);
            Check(foal != null && foal.Kind == MountKinds.DonkeyId && foal.I.TestSkinWearing() == "", "a foal of a no-foals mount stays a donkey though given the skin");
            foal?.Remove();
            // the file round trip: a value set in code, saved, read back, with the file's own lines kept
            Looks.SkinDef def = Looks.Find("kind-test");
            Kinds.KindDef kd = Kinds.Find("test-beast");
            if (def != null && kd != null)
            {
                (kd.TuningRide ?? (kd.TuningRide = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)))["LegAdjustYPx"] = 2;
                kd.FoalScale = 0.45f; // the Mounts tab's "Babies' size" slider does this
                string saveErr = Looks.SaveCfg(def);
                Check(saveErr == null, "saved to the pack's skin.cfg" + (saveErr != null ? " (" + saveErr + ")" : ""));
                string text = System.IO.File.ReadAllText(System.IO.Path.Combine(tempRoot, Looks.PackDirName, "kind-test", "skin.cfg"));
                Check(text.Contains("# a test pack") && text.Contains("name = Kind Test") && text.Contains("[Mount.Tuning.Ride]") && text.Contains("LegAdjustYPx = 2") && text.Contains("RiderOffsetPx = 3"), "the file keeps its own lines and has the new value");
                Check(text.Contains("foal_scale = 0.45") && !text.Contains("foal_scale = 0.4\n"), "the file has the new foal_scale once");
                Check(text.Contains("[Colors]") && text.Contains("Dark = 221A20"), "the file keeps its [Colors] section (the Mounts tab's auto-save must never drop a skin's colour regions)");
                kd.TuningRide.Remove("LegAdjustYPx");
                kd.FoalScale = 0.6f;
                string loadErr = Looks.ReloadCfg(def);
                Kinds.KindDef again = Kinds.Find("test-beast");
                Check(loadErr == null && again == kd && kd.TuningRide != null && kd.TuningRide.TryGetValue("LegAdjustYPx", out object back) && Convert.ToInt32(back) == 2, "reloaded: the same kind object has the file's value again" + (loadErr != null ? " (" + loadErr + ")" : ""));
                Check(Math.Abs(kd.FoalScale - 0.45f) < 0.001f && Math.Abs((Mounts.Kinds.Find("test-beast")?.FoalScale ?? 0f) - 0.45f) < 0.001f, $"reloaded: the file's foal_scale is on the kind and listed ({kd.FoalScale})");
                yield return Frames(3);
                Check(a != null && a.Tuning.IsSet("LegAdjustYPx") && a.Tuning.Get<int>("LegAdjustYPx") == 2, "the wearer has the reloaded value");
            }
            Info("the next \"no skin 'kind-test' is installed\" warning is this test's own: it withdraws the skin from under the wearer");
            Check(Looks.Unregister("kind-test", owner), "the owner withdraws the skin");
            yield return Frames(3);
            Check(Mounts.Kinds.Find("test-beast") != null, "the kind stays listed after its skin is withdrawn");
            Check(a != null && a.Kind == MountKinds.DonkeyId && !a.Tuning.IsSet("RiderOffsetPx"), "withdrawn: the donkey is a donkey again, the kind's tuning off it");
            Check(Mounts.Kinds.Unregister(owner, "test-beast"), "the pack's owner withdraws the kind");
            a?.Remove();
            cart?.Remove();
            yield return Frames(2);
            try { System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        private sealed class CountingAbility : MountAbility
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

        // A kind registered in code with an ability: Attached once when a donkey wearing one of its skins is ready,
        // ticking while it stands, Mounted on the ride and Dismounted after, Detached when it is removed; the kind
        // withdrawn with the handle.
        private static IEnumerator KindAbility()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): the API keeps a mount kind out");
                yield break;
            }
            const string owner = Owner + ".kind";
            var ability = new CountingAbility();
            IDisposable reg;
            string tempRoot;
            try
            {
                reg = Mounts.Kinds.Register(owner, new MountKindDef { Id = "test-able", Name = "Test Able", Abilities = { ability }, TuningRide = new Dictionary<string, object> { ["SeatAdjustPx"] = 4f }, TuningCart = new Dictionary<string, object> { ["CargoBedDepth"] = -0.31f } }); // a float for an int key: converted
                tempRoot = WriteTempPack("gk2donkeyapi-selftest-able", "able-test", "[Skin]\nformat = 1\nid = able-test\nname = Able Test\nmount = test-able\n");
            }
            catch (Exception e)
            {
                Warn("the kind or the temp pack could not be made: " + e.Message);
                yield break;
            }
            Looks.RegisterFolder(tempRoot, owner);
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "able-test");
            yield return Until(() => a != null && a.IsReady && a.Kind == "test-able", 8f);
            Check(waited, "a donkey wearing the skin is of the kind");
            yield return Frames(5);
            Check(ability.AttachedCount == 1 && ability.Ticks > 0, $"attached once, ticking while standing ({ability.AttachedCount}, {ability.Ticks} ticks)");
            Check(a != null && a.Tuning.IsSet("SeatAdjustPx") && a.Tuning.Get<int>("SeatAdjustPx") == 4, "the kind's riding tuning is on it (a float given, the key's int taken)");
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
            // the kind's cart-style value: on the donkey while hitched, on neither after the unhitch (a cart key, which
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
                Check(a != null && a.Tuning.IsSet("CargoBedDepth") && a.Tuning.SetBy("CargoBedDepth") == Looks.TuningOwner, "hitched: the kind's cart-style value is on the donkey");
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
            Looks.Unregister("able-test", owner);
            reg.Dispose();
            Check(Mounts.Kinds.Find("test-able") == null, "the kind is withdrawn with its handle");
            try { System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        // A skin pack on disk (a temp folder, two pictures taken from the built-in rat, one of them misnamed): registered
        // with the right report, worn (the standing frame shows; a walk frame falls back to the donkey's own), then
        // withdrawn: the donkey shows again and the skin is gone from the list.
        private static IEnumerator SkinPack()
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest");
            string packDir = System.IO.Path.Combine(tempRoot, Looks.PackDirName, "pack-test");
            const string owner = Owner + ".pack";
            try
            {
                if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
                System.IO.Directory.CreateDirectory(packDir);
                var asm = typeof(Looks).Assembly;
                byte[] Res(string name)
                {
                    using (var s = asm.GetManifestResourceStream(name))
                    using (var ms = new System.IO.MemoryStream())
                    {
                        s.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(packDir, "skin.cfg"), "[Skin]\nformat = 1\nid = pack-test\nname = Pack Test\nkind = own\nflavour = spicy\n[Tuning]\nSeatAdjustPx = 9\n");
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(packDir, "bdy_static_left.png"), Res("GK2DonkeyAPI.skins.rat\\bdy_static_left.png"));
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(packDir, "bdy_wlak_left_01.png"), Res("GK2DonkeyAPI.skins.rat\\bdy_walk_left_01.png"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(packDir, "front"));
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(packDir, "front", "bdy.png"), Res("GK2DonkeyAPI.skins.rat\\hed_static_down.png"));
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            int n = Looks.RegisterFolder(tempRoot, owner);
            Check(n == 1, $"one pack found under the folder ({n})");
            string report = Looks.Validate("pack-test");
            Info("report: " + report);
            Check(report.Contains("unknown keys ignored: flavour"), "the report names the unknown cfg key");
            Check(report.Contains("bdy_wlak_left_01"), "the report names the misnamed picture");
            Check(report.Contains("43 of the donkey's 44 frames not drawn"), "the report counts the frames still to draw");
            Check(report.Contains("1 in front of the rider"), "the report counts the front layer's picture");
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            a?.Tuning.Set(Owner, "Skin", "pack-test");
            yield return Until(() => a != null && a.IsReady && a.I.TestSkinWearing() == "pack-test", 8f);
            Check(waited, "a donkey wears the pack's skin");
            yield return Frames(3);
            Check(a != null && a.I.TestSkinShowing(), "its standing frame is drawn from the pack (the only one it has)");
            Check(a != null && a.I.TestSkinFrontShowing(), "its front picture (front/bdy.png, every body frame) is drawn above");
            Check(a != null && a.Tuning.Get<int>("SeatAdjustPx") == 9 && a.Tuning.SetBy("SeatAdjustPx") == Looks.TuningOwner, "its [Tuning] key is set on the wearer, owned by the skin");
            Check(!Looks.Unregister("pack-test", Owner), "a withdrawal by another owner is refused");
            Info("the next \"no skin 'pack-test' is installed\" warning is this test's own: it withdraws the skin from under the wearer");
            Check(Looks.Unregister("pack-test", owner), "the owner withdraws it");
            yield return Frames(3);
            Check(a != null && a.I.TestSkinWearing() == "" && !a.I.TestSkinShowing(), "withdrawn: the donkey shows again (the choice is kept)");
            Check(a != null && !a.Tuning.IsSet("SeatAdjustPx"), "withdrawn: its tuning key is off the donkey again");
            Check(Looks.Find("pack-test") == null, "withdrawn: gone from the list");
            a?.Remove();
            yield return Frames(2);
            try { System.IO.Directory.Delete(tempRoot, true); } catch { }
        }

        // A recipe skin: nothing shows until every frame is built (the donkey's own look stays), then all 44 pictures are
        // there; cleared, they are freed.
        private static IEnumerator SkinRecipe()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): needs the built-in recipe");
                yield break;
            }
            if (Looks.Find("hellfire") == null)
            {
                Warn("no built-in recipe 'hellfire': skipped");
                yield break;
            }
            Vector3 near = Spot(home, 4f) ?? home;
            yield return Seconds(0.5f);
            int tex0 = Live.Textures;
            // worn already by a donkey of the world (the player's own with Skin = hellfire): its sheet is built and
            // shared, so there is no build to watch and no texture to count; those two checks can't run then
            bool preloaded = Looks.IsLoaded("hellfire");
            MountHandle a = Spawn(near, true);
            yield return Until(() => a != null && a.IsReady, 6f);
            a?.Tuning.Set(Owner, "Skin", "hellfire");
            yield return Frames(2);
            bool buildingSeen = a != null && a.I.TestSkinBuilding() && !a.I.TestSkinShowing();
            yield return Until(() => a != null && a.I.TestSkinShowing(), 10f);
            Check(waited, "Hellfire shows once built");
            if (preloaded)
            {
                Info("Hellfire was loaded already (a donkey of the world wears it): the build-phase and one-sheet checks can't run; clear the player's skin to see them");
            }
            else
            {
                Check(buildingSeen, "while it was being built, the donkey's own look stayed (no half-built skin)");
                Check(Live.Textures == tex0 + 1, $"the 44 frames built from the donkey's own onto one sheet ({tex0} -> {Live.Textures})");
            }
            // the drawing changed, not just the colours: the lava cracks (Muzzle), the horns and the outline (Dark) and
            // the eyes' glow (Eye) are in the pictures as the recipe's own colours; the donkey's eye white is gone
            Looks.Loaded look = a?.I.LooksRig.WornOwnColours;
            int lava = look != null && look.ColoursByRegion.TryGetValue("Muzzle", out List<Color32> m) ? m.Count : -1;
            int glow = look != null && look.ColoursByRegion.TryGetValue("Eye", out List<Color32> e) ? e.Count : -1;
            bool white = look != null && look.Colours.Exists(c => c.r == 255 && c.g == 255 && c.b == 255);
            Check(lava >= 3 && glow >= 2 && !white, $"the pictures hold the cracks ({lava} lava colours), the eyes' glow ({glow}) and no eye white: the drawing changed");
            a?.Remove();
            yield return Frames(2);
            Check(Live.Textures == tex0, $"removed: the pictures freed ({tex0} -> {Live.Textures})");
        }

        // The built-in effect: lit on a spawned donkey (the game's lamp fire copied, its emitters on the ring), follows it,
        // out when the mode is off, gone when the donkey is removed (nothing of it left in the world).
        private static IEnumerator EffectOnOff()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): needs the built-in effect");
                yield break;
            }
            Vector3 near = Spot(home, 4f) ?? home;
            MountHandle a = Spawn(near, true);
            yield return Until(() => a != null && a.IsReady, 6f);
            a?.Tuning.Set(Owner, "Effect", "hellfire");
            a?.Tuning.Set(Owner, "EffectWhen", MountEffectWhen.Always);
            yield return Until(() => a != null && a.I.TestEffectLit(), 6f);
            if (!waited)
            {
                Warn("the fire never lit (no lamp flame in memory, or indoors): skipped");
                a?.Remove();
                yield break;
            }
            int n = a.I.TestEffectEmitters();
            Check(n == 9, $"the ring has its six flames and three sparks ({n} emitters)");
            Vector3? ringAt = a.I.TestEffectRingPosition();
            Check(ringAt.HasValue, "the ring is in the world");
            Vector3 before = ringAt ?? Vector3.zero;
            MountResult moved = a != null ? a.MoveTo(near + new Vector3(3f, 0f, 0f)) : MountResult.Refused("gone");
            Check(moved.Ok, "moved it: " + (moved.Ok ? "ok" : moved.Reason));
            yield return Seconds(1f);
            ringAt = a?.I.TestEffectRingPosition();
            Check(ringAt.HasValue && (ringAt.Value - before).sqrMagnitude > 0.25f, "the ring followed the donkey");
            a?.Tuning.Set(Owner, "EffectWhen", MountEffectWhen.Off);
            yield return Frames(3);
            Check(a != null && !a.I.TestEffectLit(), "off: the fire is out");
            a?.Tuning.Set(Owner, "EffectWhen", MountEffectWhen.Always);
            yield return Until(() => a != null && a.I.TestEffectLit(), 4f);
            int lit = a != null ? a.I.TestEffectInWorld() : 0;
            a?.Remove();
            yield return Frames(3);
            int left = a != null ? a.I.TestEffectInWorld() : 0;
            Check(lit > 0 && left == 0, $"removed while lit: nothing of its fire left in the world ({lit} -> {left} pieces)");
        }

        // Many skinned donkeys spawned and removed, twice: the skin loads once, and everything is freed at the end.
        private static IEnumerator SkinLeaks()
        {
            if (!Plugin.MountsOn)
            {
                Skip("the mounts are off (Debug.MountsPreview): needs the built-in skins");
                yield break;
            }
            if (Looks.Find("rat") == null)
            {
                Warn("no built-in skin 'rat': skipped");
                yield break;
            }
            yield return Settle(10f);
            int tex0 = Live.Textures, loaded0 = Looks.LoadedCount, inst0 = Mount.All.Count;
            // a skin one of them already wears (the settings' paper donkey in the snail after the colours window was
            // open, 2026-10-18) loads nothing new: the test's two skins count only when they aren't loaded yet
            int newSkins = (Looks.IsLoaded("rat") ? 0 : 1) + (Looks.IsLoaded("snail") ? 0 : 1);
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                var made = new List<MountHandle>();
                for (int k = 0; k < 40; k++)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 30f;
                    MountHandle h = Spawn(home + new Vector3(r.x, 0f, r.y), k % 2 == 0, withCart: k % 10 == 0);
                    if (h != null)
                    {
                        h.Tuning.Set(Owner, "Skin", k % 3 == 0 ? "snail" : "rat");
                        made.Add(h);
                    }
                }
                yield return Seconds(4f);
                Check(Looks.LoadedCount == loaded0 + newSkins, $"cycle {cycle}: {newSkins} skin(s) loaded for {made.Count} donkeys ({Looks.LoadedCount - loaded0} new; {2 - newSkins} of the two were loaded already)");
                foreach (MountHandle h in made)
                {
                    if (h.Exists) h.Remove();
                }
                yield return Seconds(2f);
            }
            yield return Settle(10f);
            Check(Mount.All.Count == inst0, $"instances back ({inst0} -> {Mount.All.Count})");
            Check(Looks.LoadedCount == loaded0 && Live.Textures == tex0, $"skins unloaded, textures back ({tex0} -> {Live.Textures}, loaded {loaded0} -> {Looks.LoadedCount})");
        }

        // New views are made at most Lod.SpawnsPerFrame a frame (50 in one frame made a 20 ms frame).
        private static IEnumerator SpawnCap()
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
        private static IEnumerator Wake(bool switchOff)
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

        // Saddle Up's H on a donkey frozen off screen: brought to the keeper and ridden in the same frame. Riding a
        // switched-off view must wait for it to wake; once ridden it is drawn and faces the keeper's way.
        private static IEnumerator MountWhileIdle()
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

        // Views released far away are parked and taken again by the next donkey of the same scene, kind and size: a
        // foal's view never under a full donkey, no cart parts, its own colours.
        private static IEnumerator Reuse()
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
            int reused0 = Lod.ReusedTotal; // from before the keeper comes back: the world's donkeys wake with him
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
        private static IEnumerator FoalsFollow()
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
        private static IEnumerator Leaks()
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
        private static IEnumerator FrameTimes()
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
