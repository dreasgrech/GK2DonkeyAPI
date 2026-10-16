using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2PerfRunner
{
    // The profiling suite (docs/performance-hog.md): one press after loading a save, then hands off for about 12 minutes.
    // Each run: the state set up (on foot, riding, the cart with a six-item test load, standing by the parked donkey),
    // SettleSeconds in that state (Performance Hog's "before" window is the 30 s before the recording key, from 5 s after
    // the last load), the recording, the report exported. The hitch run records Saddle Up's own key actions (H, G) and
    // the test load going on and off, spaced apart. Memory snapshots at the start and the end, then one more export.
    // Every report's name goes into suite-<time>.txt next to the reports; tools/perfhog-report.py --suite reads it.
    // Several suites in a row (a series): each with its own suite file, census and memory report, the time held
    // throughout; before each next one the start checks again (on foot, on the loop, the cart empty), and a census line
    // over the whole series at the end (a leak shows as growth suite after suite, caches filling only in the first).
    //
    // Never touches the save or real cargo:
    //   - it doesn't sleep or save (the game saves only on waking from sleep, EnergySystem.StopSleeping) and never sends
    //     the donkey away (that sets the cart's load down in the world);
    //   - it refuses to start while the cart holds cargo; the test load (TestLoads, Put refuses next to real cargo) is
    //     always taken off while still driving, and on any stop the cleanup takes it off (calling the cart first if need
    //     be) before anything else;
    //   - the time of day is held for the whole suite (EnvironmentEngine.SetTimeOfDayFake: the light stays the same for
    //     every run, the clock and the time-triggered scripts stand still) and put back at the end.
    // Stops (and cleans up) on a pause (Esc, any window), a movement key, the loop blocked, anything that doesn't happen in
    // time, or an exception.
    internal sealed class Suite
    {
        internal enum HoldTime { Current, Noon, Night }

        internal const float SettleSeconds = 36f, RecordSeconds = 75f, IdleSeconds = 60f;
        internal const int HitchCycles = 5;
        private static readonly string[] Load = { "wood", "wood", "stone", "stone", "box_flour", "box_vine" };

        private readonly ManualLogSource log;
        private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private readonly List<string> lines = new List<string>();
        private readonly HoldTime hold;
        private readonly int count;
        private int index;
        private string abortWhy;
        private bool cleaning, expectDriving, expectRecording, testLoadOn, timeHeld;
        private float startedAt;
        private Dictionary<string, int> censusAtStart, censusAfterWalk, censusSeriesStart;
        private readonly List<string> results = new List<string>();

        internal static Suite Current { get; private set; }
        internal static string LastResult = "";
        internal string Status { get; private set; } = "";
        internal string Step { get; private set; } = "";

        private Suite(ManualLogSource log, HoldTime hold, int count)
        {
            this.log = log;
            this.hold = hold;
            this.count = Math.Max(1, count);
        }

        // Why the suite can't start now, or null.
        internal static string CantStart()
        {
            if (Current != null) return "a suite is running";
            return CantStartNext(timeHeldByUs: false);
        }

        // The same checks before each suite of a series (the time is the series' own hold then).
        private static string CantStartNext(bool timeHeldByUs)
        {
            if (LoopPath.Keeper == null || !Mounts.IsGameReady) return "load a game first";
            if (MainGame.IsGamePaused) return "the game is paused";
            if (!Hog.Find()) return Hog.Problem;
            if (!Hog.Enabled) return "Performance Hog is off in the Mods menu";
            if (!Hog.Idle) return "Performance Hog is busy (" + Hog.State + ")";
            if (Hog.Walking) return "Performance Hog is taking a memory snapshot";
            if (!ApiDev.Find()) return ApiDev.Problem;
            if (ApiDev.KeyAction("RideKey") == null || ApiDev.KeyAction("CartKey") == null) return "Saddle Up's ride and cart keys weren't found";
            if (Mounts.IsMounted) return "get off first (the suite starts on foot)";
            if (!LoopPath.HasPath) return "record the loop first";
            if (LoopPath.SceneId() != LoopPath.Scene) return "the loop is in " + LoopPath.Scene;
            if (LoopPath.DistanceToLoop() > 2f) return $"you are {LoopPath.DistanceToLoop():F1} from the loop: walk onto it";
            MountCompanion c = Mounts.Companion(ApiDev.SaddleUp);
            if (c.Cart != null && c.Cart.Exists && c.Cart.Cargo.Count > 0)
                return $"the cart holds {c.Cart.Cargo.Count} item(s): unload it first (the suite only ever loads a test load)";
            EnvironmentEngine env = EnvironmentEngine.Instance;
            if (env == null) return "no time of day (no EnvironmentEngine)";
            if (env.IsPaused && !timeHeldByUs) return "the time of day is held already (the showcase director?): back to the real time first";
            return null;
        }

        internal static void Start(ManualLogSource log, HoldTime hold, int count)
        {
            var s = new Suite(log, hold, count);
            s.stack.Push(s.RunSeries());
            Current = s;
        }

        internal string Progress => count > 1 ? $"suite {index} of {count}, {Step}" : Step;

        internal void Abort(string why)
        {
            if (abortWhy == null && !cleaning)
            {
                abortWhy = why;
                log.LogWarning("[perfrunner] suite stopped: " + why);
            }
        }

        // From Plugin.Update: one step of the suite (nested enumerators run on a stack).
        internal void Tick()
        {
            if (!cleaning)
            {
                if (abortWhy == null && MainGame.IsGamePaused) Abort("the game was paused (Esc or a window)");
                if (abortWhy == null && expectDriving && !LoopPath.Driving) Abort("the loop drive ended: " + (LoopPath.StopReason ?? "?"));
                if (abortWhy == null && expectRecording && !Hog.Recording) Abort("the recording ended outside the suite (" + Hog.State + ")");
                if (abortWhy != null)
                {
                    cleaning = true;
                    stack.Clear();
                    stack.Push(Cleanup());
                }
            }
            try
            {
                while (stack.Count > 0)
                {
                    if (abortWhy != null && !cleaning)
                    {
                        // a step stopped the suite: nothing after it runs, not even in this frame
                        cleaning = true;
                        stack.Clear();
                        stack.Push(Cleanup());
                    }
                    IEnumerator e = stack.Peek();
                    if (!e.MoveNext())
                    {
                        stack.Pop();
                        continue;
                    }
                    if (e.Current is IEnumerator sub)
                    {
                        stack.Push(sub);
                        continue;
                    }
                    return; // a frame's wait
                }
                if (!cleaning)
                {
                    cleaning = true;
                    stack.Push(Cleanup());
                    return;
                }
                Current = null;
            }
            catch (Exception ex)
            {
                log.LogError("[perfrunner] suite failed: " + ex);
                if (!cleaning)
                {
                    abortWhy = abortWhy ?? ("an error: " + ex.Message);
                    cleaning = true;
                    stack.Clear();
                    stack.Push(Cleanup());
                }
                else
                {
                    stack.Clear();
                    Current = null;
                    LastResult = "the cleanup failed (see the log): check the cart for a test load (Cargo tab, Take off) and the time of day";
                }
            }
        }

        private IEnumerator RunSeries()
        {
            HoldTimeOfDay();
            for (index = 1; index <= count; index++)
            {
                if (index > 1)
                {
                    string why = CantStartNext(timeHeldByUs: true);
                    if (why != null)
                    {
                        Abort($"suite {index} of {count} can't start: {why}");
                        yield break;
                    }
                    yield return Wait(3f, $"suite {index} of {count}: starting");
                }
                yield return Run();
                string done = $"suite {index} of {count} done in {(Time.realtimeSinceStartup - startedAt) / 60f:F1} min";
                lines.Add("# " + done);
                results.Add(done + "; " + WriteSuiteFile());
                log.LogInfo("[perfrunner] " + results[results.Count - 1]);
            }
            if (count > 1)
            {
                Census.LogGrown(log, $"over the whole series of {count} suites", censusSeriesStart, Census.Take());
            }
        }

        // This suite's lines into suite-<time>.txt (then cleared for the next suite); its file name, or why not.
        private string WriteSuiteFile()
        {
            try
            {
                string dir = Path.Combine(Paths.BepInExRootPath, "PerformanceHog");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"suite-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                File.WriteAllLines(file, lines, new UTF8Encoding(false));
                return Path.GetFileName(file);
            }
            catch (Exception e)
            {
                log.LogWarning("[perfrunner] the suite file wasn't written: " + e.Message);
                return "no suite file (" + e.Message + ")";
            }
            finally
            {
                lines.Clear();
            }
        }

        private IEnumerator Run()
        {
            startedAt = Time.realtimeSinceStartup;
            lines.Add($"# GK2 Perf Runner suite{(count > 1 ? $" {index} of {count}" : "")}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}; scene {LoopPath.Scene}, loop {LoopPath.Length:F1} units");
            lines.Add($"# settle {SettleSeconds:F0} s, record {RecordSeconds:F0} s (idle {IdleSeconds:F0} s), hitch cycles {HitchCycles}; dev panel {(ApiDev.TuningWindowOn() ? "ON ([perf] logging adds to the frames)" : "off")}");
            lines.Add(timeLine);
            yield return Snapshot("the first memory snapshot");
            censusAtStart = Census.Take();
            if (censusSeriesStart == null) censusSeriesStart = censusAtStart;

            Step = "walk";
            Drive();
            yield return Wait(SettleSeconds, "walk: settling on foot");
            yield return Record("walk", Wait(RecordSeconds, "walk: recording on foot"));
            censusAfterWalk = Census.Take();
            Census.LogGrown(log, "during the walk run", censusAtStart, censusAfterWalk);

            Step = "ride";
            StopDrive();
            yield return Press("RideKey", () => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, "riding");
            yield return Wait(2f, "ride: mounted");
            Drive();
            yield return Wait(SettleSeconds, "ride: settling");
            yield return Record("ride", Wait(RecordSeconds, "ride: recording"));

            Step = "cart";
            StopDrive();
            yield return Press("CartKey", () => Mounts.IsMounted && Mounts.Style == MountStyle.Cart, "driving the cart");
            yield return Wait(2f, "cart: hitched");
            yield return PutLoad();
            Drive();
            yield return Wait(SettleSeconds, "cart: settling with six items");
            yield return Record("cart", Wait(RecordSeconds, "cart: recording"));
            StopDrive();
            yield return TakeOffLoad();

            Step = "idle";
            yield return Press("RideKey", () => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, "riding on (the cart left standing)");
            yield return Wait(2f, "idle: the cart left");
            yield return Press("RideKey", () => !Mounts.IsMounted, "on foot");
            yield return Wait(SettleSeconds, "idle: settling by the donkey");
            yield return Record("idle", Wait(IdleSeconds, "idle: recording"));

            Step = "hitch";
            yield return Wait(SettleSeconds, "hitch: settling (the idle run's stop and export out of the before window)");
            yield return Record("hitch", Hitches());

            Step = "memory";
            yield return Snapshot("the second memory snapshot");
            Dictionary<string, int> censusAtEnd = Census.Take();
            Census.LogGrown(log, "after the walk run to the end", censusAfterWalk, censusAtEnd);
            Census.LogGrown(log, "over the whole suite", censusAtStart, censusAtEnd);
            yield return Export("memory");
        }

        // The recording around body: started, checked, body run, stopped, exported.
        private IEnumerator Record(string label, IEnumerator body)
        {
            if (!Hog.Idle)
            {
                Abort("Performance Hog is busy before " + label + " (" + Hog.State + ")");
                yield break;
            }
            Hog.StartRecording();
            yield return null;
            if (Hog.Idle)
            {
                Abort("Performance Hog didn't start a recording (is it on in the Mods menu?)");
                yield break;
            }
            yield return Until(() => Hog.Recording, 60f, label + ": Performance Hog setting its timers");
            log.LogInfo($"[perfrunner] {label}: recording.");
            expectRecording = true;
            yield return body;
            expectRecording = false;
            Hog.StopRecording();
            yield return Until(() => Hog.Idle, 60f, label + ": Performance Hog taking its timers off");
            yield return Export(label);
        }

        private IEnumerator Export(string label)
        {
            string path = Hog.Export(out string error);
            if (path == null)
            {
                Abort($"the {label} report wasn't written: {error}");
                yield break;
            }
            lines.Add(label + "\t" + Path.GetFileName(path));
            log.LogInfo($"[perfrunner] {label}: report {Path.GetFileName(path)}.");
            yield return Wait(1.5f, label + ": written"); // a new second for the next report's name
        }

        private IEnumerator Snapshot(string what)
        {
            if (!Hog.StartSnapshot())
            {
                Abort("Performance Hog didn't start " + what);
                yield break;
            }
            yield return Until(() => !Hog.Walking, 180f, what);
        }

        private IEnumerator Hitches()
        {
            for (int k = 1; k <= HitchCycles; k++)
            {
                string n = $"hitch {k}/{HitchCycles}";
                yield return Press("CartKey", () => Mounts.IsMounted && Mounts.Style == MountStyle.Cart, n + ": the cart comes");
                yield return Wait(3f, n + ": driving");
                yield return PutLoad();
                yield return Wait(3f, n + ": loaded");
                yield return TakeOffLoad();
                yield return Wait(2f, n + ": unloaded");
                yield return Press("RideKey", () => Mounts.IsMounted && Mounts.Style == MountStyle.Ride, n + ": the cart left standing");
                yield return Wait(3f, n + ": riding");
                yield return Press("RideKey", () => !Mounts.IsMounted, n + ": off");
                yield return Wait(3f, n + ": on foot");
            }
        }

        private IEnumerator PutLoad()
        {
            testLoadOn = true; // from here the cleanup checks the cart
            string r = ApiDev.PutLoad(Load, "the perf runner");
            yield return Until(() => ApiDev.OnCart() == Load.Length, 5f, "the test load on the cart (" + r + ")");
        }

        private IEnumerator TakeOffLoad()
        {
            string r = ApiDev.TakeOff();
            yield return Until(() => ApiDev.OnCart() == 0, 5f, "the test load off the cart (" + r + ")");
            if (abortWhy == null) testLoadOn = false;
        }

        // Saddle Up's key action (as if its key was pressed), then until done.
        private IEnumerator Press(string key, Func<bool> done, string what)
        {
            Action a = ApiDev.KeyAction(key);
            if (a == null)
            {
                Abort("Saddle Up's " + key + " wasn't found");
                yield break;
            }
            a();
            yield return Until(done, 20f, what);
        }

        private IEnumerator Wait(float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                Status = $"{what} ({end - Time.realtimeSinceStartup:F0} s)";
                yield return null;
            }
        }

        private IEnumerator Until(Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    Abort($"{what}: didn't happen within {timeout:F0} s");
                    yield break;
                }
                Status = what + "...";
                yield return null;
            }
        }

        private void Drive()
        {
            if (!LoopPath.StartDriving())
            {
                Abort("the loop drive didn't start: " + LoopPath.Status);
                return;
            }
            expectDriving = true;
        }

        private void StopDrive()
        {
            expectDriving = false;
            LoopPath.Stop("the suite's next step");
        }

        private string timeLine = "";

        private void HoldTimeOfDay()
        {
            EnvironmentEngine env = EnvironmentEngine.Instance;
            float t = hold == HoldTime.Noon ? 0.5f : hold == HoldTime.Night ? 0.9f : env.Data.TimeOfDay;
            env.SetTimeOfDayFake(t);
            timeHeld = true;
            timeLine = $"# time of day held at {t:F3} ({hold}; real time {env.Data.TimeOfDay:F3}, day {env.Data.CurrentDayNumber})";
            log.LogInfo($"[perfrunner] time of day held at {t:F3} ({hold}).");
        }

        // Every way out: driving stopped, a recording stopped (not exported), the test load off, the time back, the
        // suite file written.
        private IEnumerator Cleanup()
        {
            expectDriving = false;
            expectRecording = false;
            LoopPath.Stop("the suite ended");
            string state = Hog.Find() ? Hog.State : "";
            if (state == "Preparing" || state == "Recording")
            {
                Hog.StopRecording();
            }
            while (MainGame.IsGamePaused)
            {
                Status = "stopped: " + abortWhy + "; close the menu to finish cleaning up";
                yield return null;
            }
            if (testLoadOn)
            {
                if (!(Mounts.IsMounted && Mounts.Style == MountStyle.Cart))
                {
                    // the cart (with the test load on it) is standing somewhere: hitch it to take the load off
                    Action g = ApiDev.KeyAction("CartKey");
                    g?.Invoke();
                    float end = Time.realtimeSinceStartup + 20f;
                    while (!(Mounts.IsMounted && Mounts.Style == MountStyle.Cart) && Time.realtimeSinceStartup < end)
                    {
                        Status = "cleaning up: calling the cart to take the test load off...";
                        yield return null;
                    }
                }
                if (Mounts.IsMounted && Mounts.Style == MountStyle.Cart)
                {
                    log.LogInfo("[perfrunner] cleanup: " + ApiDev.TakeOff());
                    yield return null;
                }
                if (Mounts.IsMounted && Mounts.Style == MountStyle.Cart && ApiDev.OnCart() == 0)
                {
                    testLoadOn = false;
                }
                else
                {
                    log.LogError("[perfrunner] cleanup: the test load may still be on the cart: drive it (G) and press Take off in the dev panel's Cargo tab.");
                }
            }
            if (timeHeld)
            {
                EnvironmentEngine.Instance?.ResumeTimeOfDayFromData();
                timeHeld = false;
                log.LogInfo("[perfrunner] time of day back to the real time.");
            }
            float t0 = Time.realtimeSinceStartup;
            while (!Hog.Idle && Time.realtimeSinceStartup - t0 < 60f)
            {
                Status = "cleaning up: Performance Hog taking its timers off...";
                yield return null;
            }
            string result;
            if (abortWhy == null)
            {
                result = count > 1 ? $"all {count} suites done" : results.Count > 0 ? results[0] : "done";
                if (count > 1 && results.Count > 0) result += " (last: " + results[results.Count - 1] + ")";
            }
            else
            {
                result = "stopped: " + abortWhy + (testLoadOn ? " (THE TEST LOAD MAY STILL BE ON THE CART: see the log)" : "")
                    + (results.Count > 0 ? $"; {results.Count} suite(s) finished before it" : "");
                if (lines.Count > 0)
                {
                    // the suite that was running: its reports so far, and why it stopped
                    lines.Add("# " + result);
                    result += "; its reports so far in " + WriteSuiteFile();
                }
            }
            log.LogInfo("[perfrunner] " + result);
            LastResult = result;
            Status = result;
        }
    }
}
