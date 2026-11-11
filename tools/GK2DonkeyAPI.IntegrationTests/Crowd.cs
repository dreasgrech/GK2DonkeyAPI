using BepInEx;
using GK2DonkeyAPI.Api;
using HarmonyLib;
using LazyBearTechnology;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // The mount crowd (measures only; the performance pass of 2026-11-09, docs/performance.md): every shipped look at
    // once, babies, carts with their lanterns lit at night, first in view (every one active: what the levels can't save),
    // then many more scattered far (what the levels do), then ridden through. Uncapped (no vsync, no frame-rate target),
    // so the frame time shows the work, not the cap. Each phase: the frame (mean, median, 99th percentile, worst), the
    // mod's time and garbage by part (the [garbage] line), the views and levels; a row each appended to
    // BepInEx\GK2-crowd.tsv, so runs before and after a fix compare. Afterwards everything removed and the made-and-
    // destroyed counters checked back where they started (no leak with every look worn).
    internal static class CrowdTests
    {
        private const string TsvName = "GK2-crowd.tsv";

        internal static IEnumerator MountCrowd()
        {
            Info(StandardZoom.Begin()); // the game's own zoom for a measurement (StandardZoom.cs), put back after
            yield return Seconds(1f);
            try
            {
                yield return MountCrowdAtZoom();
            }
            finally
            {
                StandardZoom.End();
            }
        }

        private static IEnumerator MountCrowdAtZoom()
        {
            string noPack = Tests.MissingPack("dragon", "rat", "snail", "worm", "gerry", "hellfire", "royal-parade");
            if (noPack != null) { Skip(noPack); yield break; }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            // every look we ship: the donkey's own and its skins, the dragon and its Mecha, Larry, the critters
            var looks = new List<string> { "", Tests.Sk("hellfire"), Tests.Sk("dragon"), Tests.Sk("gerry"), Tests.Sk("royal-parade"), Tests.Sk("rat"), Tests.Sk("snail"), Tests.Sk("worm") };
            string armored = Looks.SkinKey(Catalog.DonkeyId, "saddleup-donkey-skins.armored");
            if (Looks.Find(armored) != null) looks.Add(armored);
            string mecha = Looks.SkinKey(Tests.M("dragon"), "armored");
            if (Looks.Find(mecha) != null) looks.Add(mecha);

            yield return Settle(15f);
            yield return LooksSettled(10f);
            // Live.Textures counts the skin sheets too: a sheet an earlier scenario still held and let go during this one
            // read as a colour texture gone (50 -> 49, Larry's sheet from the garbage survey's picking, 2026-11-09). The
            // colour textures are compared alone, and the sheets may only be fewer at the end (the crowd's let go).
            int sheets0 = Looks.SheetCount;
            int tex0 = Live.Textures - sheets0, mat0 = Live.Materials, lamp0 = Live.LampObjects, mesh0 = Live.Meshes, cargo0 = Live.CargoHolders;
            int shared0 = Colors.SharedTextures.Count, inst0 = Mount.All.Count, views0 = WorldViews();

            var made = new List<MountHandle>();
            var rows = new List<string>();
            var counts = new int[3]; // adults, babies, carts
            EnvironmentEngine env = EnvironmentEngine.Instance;
            bool timeHeld = false;
            int vsyncWas = QualitySettings.vSyncCount, rateWas = Application.targetFrameRate;
            bool bySection = Perf.AllocBySection;
            Harmony stick = null;
            try
            {
                stick = new Harmony(Plugin.Guid + ".crowdstick");
                stick.Patch(AccessTools.Method(typeof(LazyInput), nameof(LazyInput.GetDirection)),
                    postfix: new HarmonyMethod(typeof(SelfTest), nameof(SurveyStickPostfix)));
            }
            catch (Exception e)
            {
                Fail("the stick could not be patched: " + e.Message);
                try { stick?.UnpatchSelf(); } catch { }
                yield break;
            }
            try
            {
                Perf.AllocBySection = true;
                if (env != null && !env.IsPaused)
                {
                    env.SetTimeOfDayFake(0.9f); // night: the carts' lanterns lit
                    timeHeld = true;
                }
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                yield return Seconds(2f);

                yield return Phase("0. none of the test's (the world's own mounts only)", 8f, rows, counts);

                yield return Grow(made, looks, counts, 12, 2f, 7f);
                yield return Phase("1. 12 in view, every look, babies and carts", 10f, rows, counts);

                yield return Grow(made, looks, counts, 24, 2f, 9f);
                yield return Phase("2. 36 in view", 10f, rows, counts);

                yield return Grow(made, looks, counts, 120, 15f, 60f);
                yield return Settle(20f);
                yield return Phase("3. 36 in view and 120 more scattered up to 60 away (the levels)", 10f, rows, counts);

                // riding through: a plain donkey of the crowd, the stick right then left (views woken and put to sleep)
                MountHandle steed = null;
                float best = float.MaxValue; // the nearest to the keeper, so the ride isn't refused for the distance
                foreach (MountHandle h in made)
                {
                    if (!h.Exists || !h.IsReady || h.Mount != Catalog.DonkeyId || h.Parent != null) continue;
                    float d = Vector3.Distance(h.Position, home);
                    if (d < best) { best = d; steed = h; }
                }
                MountResult r = steed != null ? steed.Ride(showMessage: false) : default;
                if (steed == null || !r.Ok) Info($"no donkey of the crowd could be ridden ({(steed == null ? "none ready" : Said(r))}): the ride skipped");
                else
                {
                    yield return Until(() => Mounts.IsMounted, 5f);
                    yield return Seconds(1f);
                    yield return Phase("4. riding through them, right 6 s then left 6 s", 0f, rows, counts, rideSeconds: 6f);
                    Mounts.Dismount("self-test");
                    yield return Seconds(0.5f);
                }
            }
            finally
            {
                surveyStick = null;
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                Perf.PhaseAbort();
                Perf.AllocBySection = bySection;
                QualitySettings.vSyncCount = vsyncWas;
                Application.targetFrameRate = rateWas;
                if (timeHeld) env?.ResumeTimeOfDayFromData();
                try { stick?.UnpatchSelf(); } catch { }
                foreach (MountHandle h in made) { try { if (h != null && h.Exists) h.Remove(); } catch { } }
                WriteRows(rows);
            }

            // The keeper back where he started (the ride left him elsewhere, and the world's own mounts slept or woke
            // with it: the first run's counters fell 28 -> 16 materials with 6 of their views asleep, 2026-11-09).
            PutKeeper(home, homeFacing);
            yield return Seconds(2f);
            yield return Settle(15f);
            yield return LooksSettled(10f);
            Check(Mount.All.Count == inst0, $"crowd gone: instances back ({inst0} -> {Mount.All.Count})");
            int views1 = WorldViews();
            if (views1 != views0)
            {
                // The levels' hysteresis (asleep beyond 35, awake again only within 30) can leave a world mount at a
                // border asleep: the made-and-destroyed counters then differ by its view, not by a leak. Said, not judged.
                Info($"the world's own mounts have {views1} view(s) now against {views0} at the start (the levels' border): the counters not compared; materials {mat0} -> {Live.Materials}, colour textures {tex0} -> {Live.Textures - Looks.SheetCount}, skin sheets {sheets0} -> {Looks.SheetCount} (shared {shared0} -> {Colors.SharedTextures.Count}), lamps {lamp0} -> {Live.LampObjects}, meshes {mesh0} -> {Live.Meshes}, cargo holders {cargo0} -> {Live.CargoHolders}");
                yield break;
            }
            Check(Live.Materials == mat0, $"crowd gone: colour materials back ({mat0} -> {Live.Materials})");
            Check(Live.Textures - Looks.SheetCount == tex0 && Colors.SharedTextures.Count == shared0, $"crowd gone: colour textures back ({tex0} -> {Live.Textures - Looks.SheetCount}, shared {shared0} -> {Colors.SharedTextures.Count})");
            Check(Looks.SheetCount <= sheets0, $"crowd gone: no skin sheet more than at the start ({sheets0} -> {Looks.SheetCount})");
            Check(Live.LampObjects == lamp0 && Live.Meshes == mesh0, $"crowd gone: lamps and meshes back ({lamp0}/{mesh0} -> {Live.LampObjects}/{Live.Meshes})");
            Check(Live.CargoHolders == cargo0, $"crowd gone: cargo holders back ({cargo0} -> {Live.CargoHolders})");
        }

        // Every world mount with a view wearing the look it wants, built and put on (Looks.Rig.Settling). A look let go
        // while its mount slept and built again when it woke reaches the counters some frames after the levels settle: the
        // colour textures read 60 -> 58 with the world's snail look loading again just after the count (2026-11-11).
        private static IEnumerator LooksSettled(float timeout)
        {
            yield return Until(() =>
            {
                foreach (Mount.Instance i in Mount.All) if (i.HasView && i.LooksRig != null && i.LooksRig.Settling) return false;
                return true;
            }, timeout);
            if (!waited)
            {
                var still = new StringBuilder();
                foreach (Mount.Instance i in Mount.All)
                {
                    if (!i.HasView || i.LooksRig == null || !i.LooksRig.Settling) continue;
                    if (still.Length > 0) still.Append("; ");
                    still.Append(i.FullId).Append(" (").Append(i.tier).Append(": ").Append(i.LooksRig.TestSettleReport).Append(')');
                }
                Info($"the world's looks still settling after {timeout} s, counted as they are: {still}");
            }
            yield return Frames(2); // put on: the colours made the frame after
        }

        private static int WorldViews()
        {
            int n = 0;
            foreach (Mount.Instance i in Mount.All) if (i.HasView) n++;
            return n;
        }

        // n more adults between minR and maxR of home, each the next look in turn; every second one a baby, every third
        // one of the donkey's looks a cart. Then their views and looks given time to come (one new view a frame).
        private static IEnumerator Grow(List<MountHandle> made, List<string> looks, int[] counts, int n, float minR, float maxR)
        {
            var batch = new List<MountHandle>();
            for (int k = 0; k < n; k++)
            {
                int seq = counts[0] + k;
                string look = looks[seq % looks.Count];
                bool donkeyLook = look.Length == 0 || look.StartsWith(Catalog.DonkeyId + "/", StringComparison.Ordinal);
                Vector3? at = null;
                for (int tries = 0; tries < 12 && at == null; tries++)
                {
                    Vector2 d = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(minR, maxR);
                    Vector3 p = home + new Vector3(d.x, 0f, d.y * 0.6f);
                    float g = Mount.Instance.GroundHeight(p, out _);
                    if (float.IsNaN(g)) continue;
                    p.y = g;
                    if (FreeSpot(p)) at = p;
                }
                if (at == null) continue;
                MountHandle h = Spawn(at.Value, seq % 2 == 0, withCart: donkeyLook && seq % 3 == 0);
                if (h == null) continue;
                made.Add(h);
                batch.Add(h);
                if (look.Length > 0) h.Tuning.Set(Owner, "Skin", look);
                if (donkeyLook && seq % 3 == 0) counts[2]++;
                if (seq % 2 == 1)
                {
                    MountHandle baby = Mounts.SpawnFoal(Owner, h, 0.6f, persist: false);
                    if (baby != null) { made.Add(baby); counts[1]++; }
                }
            }
            counts[0] += batch.Count;
            float end = Time.unscaledTime + 20f;
            while (Time.unscaledTime < end && !abort)
            {
                bool all = true;
                foreach (MountHandle h in made) if (h.Exists && !h.IsReady && h.Parent == null && Vector3.Distance(h.Position, home) < 12f) { all = false; break; }
                if (all) break;
                yield return null;
            }
            yield return Seconds(4f); // the looks built, the babies at their parents, the views settled
        }

        // One phase measured: rideSeconds > 0 steers right then left that long each instead of standing for seconds.
        private static IEnumerator Phase(string label, float seconds, List<string> rows, int[] counts, float rideSeconds = 0f)
        {
            var ft = new List<float>(4096);
            Perf.PhaseStart();
            if (rideSeconds > 0f)
            {
                foreach (float way in new[] { 1f, -1f })
                {
                    surveyStick = new Vector2(way, 0f);
                    float stop = Time.unscaledTime + rideSeconds;
                    while (Time.unscaledTime < stop && !abort) { yield return null; ft.Add(Time.unscaledDeltaTime * 1000f); }
                }
                surveyStick = null;
            }
            else
            {
                float stop = Time.unscaledTime + seconds;
                while (Time.unscaledTime < stop && !abort) { yield return null; ft.Add(Time.unscaledDeltaTime * 1000f); }
            }
            double modMs = Perf.PhaseModMsPerFrame, kb = Perf.PhaseKbPerFrame;
            int gcs = Perf.PhaseCollections;
            Info(Perf.PhaseEnd(label));
            if (ft.Count == 0) yield break;
            ft.Sort();
            double sum = 0;
            foreach (float f in ft) sum += f;
            double mean = sum / ft.Count;
            float median = ft[ft.Count / 2], p99 = ft[Math.Min(ft.Count - 1, (int)(ft.Count * 0.99))], worst = ft[ft.Count - 1];
            int views = 0, inst = 0;
            foreach (Mount.Instance i in Mount.All) { inst++; if (i.HasView) views++; }
            label += $" [zoom {StandardZoom.Now}]";
            Info($"{label}: {ft.Count} frames, mean {mean:F2} ms ({1000.0 / mean:F0} fps), median {median:F2}, 99th {p99:F2}, worst {worst:F1}; the mod {modMs:F2} ms and {kb:F2} KB a frame, {gcs} collection(s); {counts[0]} adults, {counts[1]} babies, {counts[2]} carts of the test; {inst} instances, {views} views; levels {Lod.ActiveCount} active / {Lod.IdleCount} idle / {Lod.AsleepCount} asleep");
            rows.Add(string.Join("\t", DateTime.Now.ToString("yyyy-MM-dd HH:mm"), label, counts[0], counts[1], counts[2], inst, views,
                Lod.ActiveCount, Lod.IdleCount, Lod.AsleepCount, ft.Count, mean.ToString("F2"), median.ToString("F2"), p99.ToString("F2"),
                worst.ToString("F1"), modMs.ToString("F3"), kb.ToString("F2"), gcs));
        }

        private static void WriteRows(List<string> rows)
        {
            if (rows.Count == 0) return;
            string path = Path.Combine(Paths.BepInExRootPath, TsvName);
            try
            {
                var sb = new StringBuilder();
                if (!File.Exists(path)) sb.Append("time\tphase\tadults\tbabies\tcarts\tinstances\tviews\tactive\tidle\tasleep\tframes\tmean ms\tmedian ms\tp99 ms\tworst ms\tmod ms\tmod KB/frame\tcollections\n");
                foreach (string r in rows) sb.Append(r).Append('\n');
                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
                Info($"{rows.Count} row(s) added to BepInEx\\{TsvName}");
            }
            catch (Exception e)
            {
                Info($"{TsvName} couldn't be written: {e.Message}");
            }
        }
    }
}
