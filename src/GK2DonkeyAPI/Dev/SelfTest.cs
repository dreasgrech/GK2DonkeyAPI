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

        private static int pass, fail, warn;
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
            new Scenario { Group = GroupLooks, Name = "skin: on, off, removed (pictures loaded and freed)", Body = SkinOnOff },
            new Scenario { Group = GroupLooks, Name = "skin pack: a folder read, validated, worn, withdrawn", Body = SkinPack },
            new Scenario { Group = GroupLooks, Name = "skins: no leaks over spawn and remove", Body = SkinLeaks },
            new Scenario { Group = GroupLooks, Name = "recipe: built from the donkey's frames, worn whole", Body = SkinRecipe },
            new Scenario { Group = GroupLooks, Name = "effect: hellfire lit, follows, out, gone with the donkey", Body = EffectOnOff },
            new Scenario { Group = GroupLooks, Name = "mount kind from a pack: declared, worn, no cart, its tuning, withdrawn", Body = KindPack },
            new Scenario { Group = GroupLooks, Name = "mount kind from code: the ability's hooks on spawn, ride, dismount, removal", Body = KindAbility },
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
            pass = fail = warn = 0;
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
            string summary = $"{(abort ? "aborted" : "done")}{(full ? "" : $" ({scenarios.Count} of {Catalog.Length} scenarios)")}: {pass} passed, {fail} failed, {warn} warning(s).";
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
            yield return null;
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
                    if (!r.Sets.Exists(x => x[0] == kv.Key)) bad.Add("setting " + kv.Key);
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
            MountHandle left = Mounts.Unhitch(d);
            Check(left != null && left.Exists && !left.HasDonkey && left.HasCart, "unhitched: a free cart again");
            if (left == null) yield break;
            Check(left.Owner == d.Owner && left.Persist == d.Persist, "the new free cart has the donkey's owner and kept setting");
            Check(!d.HasCart, "the donkey has no cart now");
            yield return Until(() => d.IsReady && left.IsReady, 4f);
            yield return Frames(3);
            Check(d.I.TestCartPartsOn() == 0, $"the donkey's cart parts are off ({d.I.TestCartPartsOn()} on)");
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
        // section's own value wins when it has one, and a key the file has nowhere keeps its default.
        private static IEnumerator MovedSections()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-moved.cfg");
            try
            {
                System.IO.File.WriteAllText(path, "## Settings file was created by plugin Moved test\n\n[1. Keys]\n\n[2. Behaviour]\n\n# Setting type: Int32\nCartSlots = 4\n\n[3. Donkey]\n\nName = Bella\n\nFoals = 2\n\n[4. Cart]\n\nLantern = false\n\n[5. Cart]\n\nLantern = true\n");
                var cfg = new ConfigFile(path, false) { SaveOnConfigSet = false };
                var page = new MountSettingsPage("dreas.gk2.selftest.moved", cfg, "Moved test", "test", "", null);
                page.Status("Keys");
                page.Toggle("Mount", "Foo", true, "Foo", "");
                ConfigEntry<int> slots = page.Steps("Behaviour", "CartSlots", 6, 2, 6, 2, "Cart capacity", "");
                ConfigEntry<string> name = page.Text("Donkey", "Name", "", "Name", "");
                ConfigEntry<int> foals = page.Steps("Donkey", "Foals", 0, 0, 5, 1, "Foals", "");
                ConfigEntry<bool> lantern = page.Toggle("Cart", "Lantern", false, "Lantern", "");
                ConfigEntry<bool> bell = page.Toggle("Sound", "Bell", true, "Bell", "");
                Check(slots.Value == 4, $"Behaviour moved from 2 to 3: CartSlots is the file's 4 ({slots.Value})");
                Check(name.Value == "Bella" && foals.Value == 2, $"Donkey moved from 3 to 4: Name and Foals are the file's (\"{name.Value}\", {foals.Value})");
                Check(lantern.Value, "Cart is 5 and the file has it under 5 too: its own value wins over the older 4");
                Check(bell.Value, "a key the file has nowhere keeps its default");
                Check(MountSettingsPage.PlainName("12. Sound") == "Sound" && MountSettingsPage.PlainName("General") == "General" && MountSettingsPage.PlainName("A. B") == "A. B", "plain section names");
            }
            finally
            {
                try { System.IO.File.Delete(path); } catch { }
            }
            yield break;
        }

        // A built-in skin on a spawned donkey: drawn (its overlay shows, the donkey's own body kept from rendering),
        // its pictures loaded; cleared: the donkey's own body again; removed: the pictures freed.
        private static IEnumerator SkinOnOff()
        {
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
                    "# a test pack\n[Skin]\nformat = 1\nid = kind-test\nname = Kind Test\nmount = test-beast\n\n[Mount]\nid = test-beast\nname = Test Beast\ncart = false\nfoals = false\n\n[Mount.Tuning]\nRiderOffsetPx = 3\n\n[Mount.Tuning.Cart]\nSeatAdjustPx = 7\n");
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
                string saveErr = Looks.SaveCfg(def);
                Check(saveErr == null, "saved to the pack's skin.cfg" + (saveErr != null ? " (" + saveErr + ")" : ""));
                string text = System.IO.File.ReadAllText(System.IO.Path.Combine(tempRoot, Looks.PackDirName, "kind-test", "skin.cfg"));
                Check(text.Contains("# a test pack") && text.Contains("name = Kind Test") && text.Contains("[Mount.Tuning.Ride]") && text.Contains("LegAdjustYPx = 2") && text.Contains("RiderOffsetPx = 3"), "the file keeps its own lines and has the new value");
                kd.TuningRide.Remove("LegAdjustYPx");
                string loadErr = Looks.ReloadCfg(def);
                Kinds.KindDef again = Kinds.Find("test-beast");
                Check(loadErr == null && again == kd && kd.TuningRide != null && kd.TuningRide.TryGetValue("LegAdjustYPx", out object back) && Convert.ToInt32(back) == 2, "reloaded: the same kind object has the file's value again" + (loadErr != null ? " (" + loadErr + ")" : ""));
                yield return Frames(3);
                Check(a != null && a.Tuning.IsSet("LegAdjustYPx") && a.Tuning.Get<int>("LegAdjustYPx") == 2, "the wearer has the reloaded value");
            }
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
            if (Looks.Find("zebra") == null)
            {
                Warn("no built-in recipe 'zebra': skipped");
                yield break;
            }
            Vector3 near = Spot(home, 4f) ?? home;
            yield return Seconds(0.5f);
            int tex0 = Live.Textures;
            MountHandle a = Spawn(near, true);
            yield return Until(() => a != null && a.IsReady, 6f);
            a?.Tuning.Set(Owner, "Skin", "zebra");
            yield return Frames(2);
            bool buildingSeen = a != null && a.I.TestSkinBuilding() && !a.I.TestSkinShowing();
            yield return Until(() => a != null && a.I.TestSkinShowing(), 10f);
            Check(waited, "the zebra shows once built");
            Check(buildingSeen, "while it was being built, the donkey's own look stayed (no half-built skin)");
            Check(Live.Textures == tex0 + 1, $"the 44 frames built from the donkey's own onto one sheet ({tex0} -> {Live.Textures})");
            a?.Remove();
            yield return Frames(2);
            Check(Live.Textures == tex0, $"removed: the pictures freed ({tex0} -> {Live.Textures})");
        }

        // The built-in effect: lit on a spawned donkey (the game's lamp fire copied, its emitters on the ring), follows it,
        // out when the mode is off, gone when the donkey is removed (nothing of it left in the world).
        private static IEnumerator EffectOnOff()
        {
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
            if (Looks.Find("rat") == null)
            {
                Warn("no built-in skin 'rat': skipped");
                yield break;
            }
            yield return Settle(10f);
            int tex0 = Live.Textures, loaded0 = Looks.LoadedCount, inst0 = Mount.All.Count;
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
                Check(Looks.LoadedCount == loaded0 + 2, $"cycle {cycle}: two skins loaded for {made.Count} donkeys ({Looks.LoadedCount - loaded0})");
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
