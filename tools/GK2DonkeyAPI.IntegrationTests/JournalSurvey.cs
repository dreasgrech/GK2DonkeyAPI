using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // The journal survey (the journal's two costs, 2026-11-09, docs/performance.md): opening it, step by step (the
    // [steps] trace, Dev/StepTrace.cs), three times; its picture while it is open, rendered every frame (the old way) and
    // only when it can have changed (PreviewStage.NeedsRender), each twice, uncapped; and the stage it leaves on after
    // closing, on and off, each twice, uncapped. Checks: the picture still renders while open (a walk frame changes it
    // about ten times a second, the sway 24), and fewer photos the new way than the old.
    internal static class JournalSurvey
    {
        internal static IEnumerator Run()
        {
            Info(StandardZoom.Begin()); // the game's own zoom for a measurement (StandardZoom.cs), put back after
            yield return Seconds(1f);
            try
            {
                yield return RunAtZoom();
            }
            finally
            {
                StandardZoom.End();
            }
        }

        private static IEnumerator RunAtZoom()
        {
            if (!DevTools.Unlocked) { Skip("the steps trace is dev only"); yield break; }
            MountSettingsPage page = null;
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && live.HasMountRow) { page = live; break; }
            if (page == null || LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                Skip("no settings page with a Mount row, or a window open");
                yield break;
            }
            bool forced = StepTrace.Forced, every = PreviewStage.RenderEveryFrame, stageWas = PreviewStage.TestStageActive;
            int vsyncWas = QualitySettings.vSyncCount, rateWas = Application.targetFrameRate;
            try
            {
                StepTrace.Forced = true;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;

                // 1. opening it, three times (the first of a session builds more: fonts, icons; later ones show the steady cost)
                for (int k = 1; k <= 3; k++)
                {
                    StepTrace.Last = "";
                    MountsPanel.Open(page, toggle: false);
                    Info($"[journal] open {k}: {(StepTrace.Last.Length > 0 ? StepTrace.Last : "no trace line")}");
                    yield return Seconds(1.5f);
                    if (k < 3)
                    {
                        MountsPanel.TestClose(keep: true);
                        yield return Seconds(1f);
                    }
                }
                Check(MountsPanel.IsOpen, "the journal opened");

                // 1b. each mount picked once (BuildRight runs on every pick too); the player's choice put back on closing
                var ids = new List<string> { MountCatalog.DonkeyId };
                foreach (string id in new[] { "dragon", "rat", "snail", "worm", "gerry" }) if (Catalog.Find(Tests.M(id)) != null) ids.Add(Tests.M(id));
                ids.Add(MountCatalog.DonkeyId);
                int reusedAll = 0, madeAll = 0, boundWrong = 0;
                foreach (string id in ids)
                {
                    StepTrace.Last = "";
                    MountsPanel.TestSelect(id);
                    Info($"[journal] picked {id}: {(StepTrace.Last.Length > 0 ? StepTrace.Last : "no trace line")}");
                    Info($"[journal]   its rows: {MountsPanel.LastRowsReused} kept from the mount before, {MountsPanel.LastRowsMade} made");
                    reusedAll += MountsPanel.LastRowsReused;
                    madeAll += MountsPanel.LastRowsMade;
                    boundWrong += MountsPanel.TestRowsBoundWrong();
                    yield return Seconds(1.5f); // its look built and shown (held by the journal once whole)
                }
                // the rows' controls pointed at the next mount's rows, not destroyed and made again (2.4-3.8 ms and 300-360 KB
                // a pick before; RowControls.Bound)
                Check(reusedAll > 0 && reusedAll > madeAll, $"picking another mount keeps the journal's rows ({reusedAll} kept, {madeAll} made over {ids.Count} picks)");
                Check(boundWrong == 0, $"every kept row shows the picked mount's own setting ({boundWrong} pointing elsewhere)");

                // 1c. closed and opened again, every mount picked again: the looks the journal showed are kept after it closes
                // (Looks.KeepPanelHoldsAWhile), so nothing is built again (before: every look, the Mecha 33 MB each time)
                int again0 = Looks.BuiltAgain;
                MountsPanel.TestClose(keep: true);
                yield return Seconds(1f);
                MountsPanel.Open(page, toggle: false);
                yield return Seconds(1f);
                foreach (string id in ids)
                {
                    MountsPanel.TestSelect(id);
                    yield return Seconds(1.5f);
                }
                int again = Looks.BuiltAgain - again0;
                Check(again == 0, $"the journal closed, opened and browsed again: no look built again ({again}; the looks kept {Looks.PanelKeepSeconds:0} s after it closes)");
                yield return Seconds(1f); // the picture framed and settled
                var renders = new Dictionary<bool, List<double>>();
                foreach (bool old in new[] { true, false, false, true })
                {
                    PreviewStage.RenderEveryFrame = old;
                    yield return Seconds(0.5f);
                    int r0 = PreviewStage.TestRenders, s0 = PreviewStage.TestRenderSkips;
                    float t0 = Time.unscaledTime;
                    double sum = 0;
                    int n = 0;
                    Perf.PhaseStart();
                    float end = Time.unscaledTime + 4f;
                    while (Time.unscaledTime < end && !abort) { yield return null; sum += Time.unscaledDeltaTime * 1000.0; n++; }
                    string parts = Perf.PhaseEnd(old ? "the picture every frame" : "the picture when changed");
                    double secs = Mathf.Max(0.001f, Time.unscaledTime - t0);
                    double perSecond = (PreviewStage.TestRenders - r0) / secs;
                    if (!renders.ContainsKey(old)) renders[old] = new List<double>();
                    renders[old].Add(perSecond);
                    Info($"[journal] the picture {(old ? "every frame (old)" : "when it can have changed (new)")}: {perSecond:F1} photos a second, {(PreviewStage.TestRenderSkips - s0) / secs:F1} skipped; {n} frames, {(n > 0 ? sum / n : 0):F2} ms a frame uncapped");
                    Info("[journal]   " + parts);
                }
                PreviewStage.RenderEveryFrame = every;
                double oldRate = Avg(renders[true]), newRate = Avg(renders[false]);
                Check(newRate >= 8.0, $"the picture still renders while open, the new way ({newRate:F1} a second: the walk's frames and the sway)");
                Check(newRate < oldRate * 0.6, $"fewer photos the new way ({newRate:F1} a second against {oldRate:F1})");

                // 3. closed (the player's own choice put back: the picks above were the test's): the stage left on (as today)
                // against switched off, each twice, the order reversed
                MountsPanel.TestClose(keep: false);
                yield return Seconds(1.5f);
                if (!PreviewStage.TestStageBuilt) { Info("[journal] no stage built: the closed comparison skipped"); }
                else
                {
                    foreach (bool on in new[] { true, false, false, true })
                    {
                        PreviewStage.TestStageActive = on;
                        yield return Seconds(1f);
                        double sum = 0;
                        int n = 0;
                        Perf.PhaseStart();
                        float end = Time.unscaledTime + 4f;
                        while (Time.unscaledTime < end && !abort) { yield return null; sum += Time.unscaledDeltaTime * 1000.0; n++; }
                        double mod = Perf.PhaseModMsPerFrame;
                        string parts = Perf.PhaseEnd(on ? "closed, the stage on" : "closed, the stage off");
                        Info($"[journal] closed, the stage {(on ? "on (as today)" : "switched off")}: {n} frames, {(n > 0 ? sum / n : 0):F2} ms a frame uncapped, the mod {mod:F3} ms");
                        Info("[journal]   " + parts);
                    }
                }
            }
            finally
            {
                PreviewStage.RenderEveryFrame = every;
                PreviewStage.TestStageActive = stageWas || PreviewStage.TestStageBuilt;
                StepTrace.Forced = forced;
                QualitySettings.vSyncCount = vsyncWas;
                Application.targetFrameRate = rateWas;
                if (MountsPanel.IsOpen) MountsPanel.TestClose(keep: false);
            }
        }

        private static double Avg(List<double> l)
        {
            double s = 0;
            foreach (double d in l) s += d;
            return l.Count > 0 ? s / l.Count : 0;
        }
    }
}
