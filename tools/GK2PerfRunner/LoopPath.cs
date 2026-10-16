using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2PerfRunner
{
    // The loop every run drives, so every run covers the same ground and the scene costs the same (docs/performance-hog.md).
    //   Record: walk (or ride) the loop once by hand; the keeper's position is sampled as he moves. Stop: the samples are
    //     smoothed, closed (the end joined to the start), spaced evenly and kept in BepInEx/config (FilePath).
    //   Drive: each physics step the keeper's free state (FreePlayerState.FixedUpdate, which does
    //     PhysicalBody.MoveByDirection(LazyInput.GetDirection())) gets the direction to a point a little ahead on the
    //     path instead of the player's input (a Harmony prefix; it passes straight through while not driving). The game's
    //     own movement does the rest: speed (the donkey's while mounted), the walk animation, facing, slopes,
    //     collisions. The API reads none of the input itself (it follows the keeper's movement), so a driven keeper is
    //     ridden like one under the keys.
    // The input direction: x is world +x, y is world +z (MoveByDirection builds (x, 0, y) and tilts it onto the slope).
    // Stops on any movement key, a pause (Esc, any window), a scene change, controls taken (a dialogue: no progress) or
    // being stuck.
    internal static class LoopPath
    {
        private const float Spacing = 0.08f;     // path points, world units apart
        private const float Lookahead = 0.45f;   // steer towards the point this far ahead on the path
        private const float SampleStep = 0.04f;  // a recorded sample every this far moved
        private const int SmoothWindow = 5;      // samples each side averaged before spacing
        internal const float StuckSeconds = 2f;

        private static ManualLogSource log;
        private static readonly List<Vector2> recorded = new List<Vector2>();
        private static List<Vector2> path = new List<Vector2>();
        private static float pathLength;
        private static string pathScene;
        private static bool recording;
        private static bool driving;
        private static int nearest;
        private static float travelled, lapStartTime, lastLapTime;
        private static int laps;
        private static Vector2 lastProgressPos;
        private static float lastProgressTime;

        internal static string Status = "";
        internal static string StopReason; // why the last drive ended (null while driving or never driven)

        internal static bool Driving => driving;
        internal static bool Recording => recording;
        internal static bool HasPath => path.Count > 0;
        internal static float Length => pathLength;
        internal static string Scene => pathScene;
        internal static int Laps => laps;
        internal static float LastLapTime => lastLapTime;

        // The keeper, or null: MainGame.PlayerController reads MainGame.Instance unchecked and throws at the main menu
        // (1202 NullReferenceExceptions in the first suite's log, 2026-10-16).
        internal static PlayerController Keeper => MainGame.Instance != null ? MainGame.PlayerController : null;

        private static string FilePath => Path.Combine(Paths.ConfigPath, Plugin.Guid + ".loop.txt");

        internal static void Init(ManualLogSource logger, Harmony harmony)
        {
            log = logger;
            harmony.Patch(AccessTools.Method(typeof(FreePlayerState), nameof(FreePlayerState.FixedUpdate)),
                prefix: new HarmonyMethod(typeof(LoopPath), nameof(FixedUpdatePrefix)));
            Load();
        }

        private static bool FixedUpdatePrefix()
        {
            if (!driving)
            {
                return true; // the player's own input
            }
            PlayerController pc = Keeper;
            if (pc == null || pc.PhysicalBody == null)
            {
                Stop("the keeper is gone");
                return true;
            }
            if (LazyInput.GetDirection().sqrMagnitude > 0.04f)
            {
                Stop("a movement key took over");
                return true;
            }
            pc.PhysicalBody.MoveByDirection(Steer(Pos(pc)));
            return false;
        }

        private static Vector2 Pos(PlayerController pc)
        {
            Vector3 p = pc.transform.position;
            return new Vector2(p.x, p.z);
        }

        // The direction to the path point Lookahead ahead of the nearest one; the nearest is searched forward only (a
        // little back too), so the keeper never jumps to another part of the path where it passes close by.
        private static Vector2 Steer(Vector2 pos)
        {
            int n = path.Count;
            int best = nearest;
            float bestD = float.MaxValue;
            for (int k = -3; k < 40; k++)
            {
                int i = ((nearest + k) % n + n) % n;
                float d = (path[i] - pos).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            int step = ((best - nearest) % n + n) % n;
            if (step > n / 2) step -= n; // a step back
            travelled += step * Spacing;
            nearest = best;
            if (travelled >= pathLength)
            {
                travelled -= pathLength;
                laps++;
                lastLapTime = Time.time - lapStartTime;
                lapStartTime = Time.time;
                log.LogInfo($"[perfrunner] loop: lap {laps} in {lastLapTime:F2} s.");
            }
            int ahead = (nearest + Mathf.Max(1, Mathf.RoundToInt(Lookahead / Spacing))) % n;
            Vector2 d2 = path[ahead] - pos;
            return d2.sqrMagnitude > 1e-6f ? d2.normalized : Vector2.zero;
        }

        internal static void Tick()
        {
            PlayerController pc = Keeper;
            if (pc == null)
            {
                if (recording || driving) Stop("no keeper (left the game?)");
                return;
            }
            Vector2 pos = Pos(pc);
            if (recording && (recorded.Count == 0 || (recorded[recorded.Count - 1] - pos).sqrMagnitude >= SampleStep * SampleStep))
            {
                recorded.Add(pos);
            }
            if (!driving)
            {
                return;
            }
            if (MainGame.IsGamePaused)
            {
                Stop("the game was paused (Esc or a window)");
                return;
            }
            if (SceneId() != pathScene)
            {
                Stop("another scene");
                return;
            }
            if ((pos - lastProgressPos).sqrMagnitude > 0.05f * 0.05f)
            {
                lastProgressPos = pos;
                lastProgressTime = Time.time;
            }
            else if (Time.time - lastProgressTime > StuckSeconds)
            {
                Stop($"no progress for {StuckSeconds:F0} s (blocked, or the controls were taken)");
            }
        }

        internal static void StartRecording()
        {
            if (driving) return;
            recorded.Clear();
            recording = true;
            Status = "recording: walk the loop once and end where you started";
            log.LogInfo("[perfrunner] loop: recording.");
        }

        internal static void StopRecording()
        {
            if (!recording) return;
            recording = false;
            Build();
        }

        // Starts driving from the nearest point of the path; false (and Status says why) if it can't.
        internal static bool StartDriving()
        {
            PlayerController pc = Keeper;
            if (pc == null || path.Count == 0) return false;
            if (SceneId() != pathScene)
            {
                Status = $"the loop is in {pathScene}; go there first";
                return false;
            }
            Vector2 pos = Pos(pc);
            float bestD = float.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                float d = (path[i] - pos).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    nearest = i;
                }
            }
            if (bestD > 4f)
            {
                Status = $"{Mathf.Sqrt(bestD):F1} units from the loop: walk to it first";
                return false;
            }
            travelled = 0f;
            laps = 0;
            lapStartTime = Time.time;
            lastProgressPos = pos;
            lastProgressTime = Time.time;
            StopReason = null;
            driving = true;
            Status = "driving: any movement key, Esc or Stop takes over";
            log.LogInfo($"[perfrunner] loop: driving from point {nearest} of {path.Count}.");
            return true;
        }

        // How far the keeper is from the loop's nearest point (for the suite's start check).
        internal static float DistanceToLoop()
        {
            PlayerController pc = Keeper;
            if (pc == null || path.Count == 0) return float.MaxValue;
            Vector2 pos = Pos(pc);
            float bestD = float.MaxValue;
            foreach (Vector2 p in path) bestD = Mathf.Min(bestD, (p - pos).sqrMagnitude);
            return Mathf.Sqrt(bestD);
        }

        internal static void Stop(string why)
        {
            if (recording)
            {
                recording = false;
                Build();
            }
            if (driving)
            {
                driving = false;
                StopReason = why;
                Status = "stopped: " + why + (laps > 0 ? $" after {laps} lap(s), last {lastLapTime:F2} s" : "");
                log.LogInfo("[perfrunner] loop: " + Status);
            }
        }

        // The recorded samples smoothed (a moving average, wrapping round), the loop closed, and points every Spacing.
        private static void Build()
        {
            int m = recorded.Count;
            if (m < 20)
            {
                Status = "too short a recording: walk a whole loop";
                return;
            }
            float gap = (recorded[m - 1] - recorded[0]).magnitude;
            var smooth = new List<Vector2>(m);
            for (int i = 0; i < m; i++)
            {
                Vector2 s = Vector2.zero;
                for (int k = -SmoothWindow; k <= SmoothWindow; k++) s += recorded[((i + k) % m + m) % m];
                smooth.Add(s / (2 * SmoothWindow + 1));
            }
            smooth.Add(smooth[0]); // closed
            var even = new List<Vector2> { smooth[0] };
            float carry = 0f;
            for (int i = 1; i < smooth.Count; i++)
            {
                Vector2 a = smooth[i - 1], b = smooth[i];
                float seg = (b - a).magnitude;
                float t = Spacing - carry;
                while (t <= seg)
                {
                    even.Add(Vector2.Lerp(a, b, t / seg));
                    t += Spacing;
                }
                carry = seg - (t - Spacing);
            }
            if ((even[even.Count - 1] - even[0]).magnitude < Spacing * 0.5f) even.RemoveAt(even.Count - 1);
            path = even;
            pathLength = path.Count * Spacing;
            pathScene = SceneId();
            Save();
            Status = $"loop built: {pathLength:F1} units round, {path.Count} points" + (gap > 1f ? $"; the end was {gap:F1} units from the start (joined)" : "");
            log.LogInfo("[perfrunner] loop: " + Status);
        }

        internal static string SceneId()
        {
            try
            {
                return MainGame.PlayerData?.currentGameSceneId ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# GK2 Perf Runner loop (LoopPath.cs): scene, then x z per point, " + Spacing.ToString(CultureInfo.InvariantCulture) + " units apart");
            sb.AppendLine(pathScene);
            foreach (Vector2 p in path) sb.AppendLine(p.x.ToString("F3", CultureInfo.InvariantCulture) + " " + p.y.ToString("F3", CultureInfo.InvariantCulture));
            File.WriteAllText(FilePath, sb.ToString());
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var pts = new List<Vector2>();
                string scene = null;
                foreach (string l in File.ReadAllLines(FilePath))
                {
                    if (l.StartsWith("#") || l.Trim().Length == 0) continue;
                    if (scene == null)
                    {
                        scene = l.Trim();
                        continue;
                    }
                    string[] t = l.Split(' ');
                    pts.Add(new Vector2(float.Parse(t[0], CultureInfo.InvariantCulture), float.Parse(t[1], CultureInfo.InvariantCulture)));
                }
                if (scene != null && pts.Count >= 10)
                {
                    path = pts;
                    pathScene = scene;
                    pathLength = path.Count * Spacing;
                    log.LogInfo($"[perfrunner] loop: {pathLength:F1} units round in {pathScene} (from {FilePath}).");
                }
            }
            catch (Exception e)
            {
                log.LogWarning("[perfrunner] loop: the saved loop could not be read: " + e.Message);
            }
        }
    }
}
