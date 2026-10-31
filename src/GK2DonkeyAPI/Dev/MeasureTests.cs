using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Measurements for open decisions (the user, 2026-10-31: "make sure we're collecting as much data as possible when
    // testing so we don't do one thing at a time"). Each writes a file of its own under BepInEx\ and says its lines; it
    // checks nothing it wasn't asked to and puts back every setting it touched.
    internal static class MeasureTests
    {
        private static MountSettingsPage PageWithMounts()
        {
            foreach (MountSettingsPage live in MountSettingsPage.All) if (live.Shown && live.HasMountRow && live.TestCompanion != null) return live;
            return null;
        }

        // The Mount Journal's cart picture with the lamp lit, dark and off, for every skin of the donkey: the picture's size,
        // what is drawn in it with the lamp and without (the mount and the cart), and how far the mount and cart sit from the
        // middle (docs/open-items.md "The journal's picture": whether framing "as if the lamp were there" takes its glow).
        // Lit: the look's lantern on and lit by day (the stage is lit as at noon); dark: on, not lit by day; off: no lantern.
        // BepInEx\GK2-lamp-framing.tsv.
        internal static IEnumerator LampFraming()
        {
            MountSettingsPage page = PageWithMounts();
            if (page == null)
            {
                SelfTest.Skip("no mod's settings page with a Mount row is shown");
                yield break;
            }
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            MountCompanion pc = page.TestCompanion;
            ConfigEntryBase skinRow = null;
            foreach (MountSettingsPage.Row r in page.Rows)
            {
                if (r.Entry != null && r.Entry.Definition.Key == "Skin_" + MountCatalog.DonkeyId) { skinRow = r.Entry; break; }
            }
            if (skinRow == null)
            {
                SelfTest.Skip("the page has no Skin row for the donkey");
                yield break;
            }
            object skinWas = skinRow.BoxedValue;
            string mountWas = page.MountChosen;
            var lines = new List<string> { "skin\tlamp\tvariant\tpicture w\tpicture h\tdrawn with the lamp (x y w h)\tdrawn without its glow (x y w h)\tthe mount and cart (x y w h)\tits centre off the picture's (dx dy)\tthe lamp's extra (w h)\tthe glow's extra (w h)\tthe lamp's parts\tthe stage\tsettled after (frames)\tthe stage before settling\tthe paper's look before" };
            var states = new[] { (Lantern: true, ByDay: true, Name: "lit"), (Lantern: true, ByDay: false, Name: "dark"), (Lantern: false, ByDay: true, Name: "off") };
            MountsPanel.Open(page, toggle: false);
            yield return SelfTest.Frames(3);
            try
            {
                if (!MountsPanel.IsOpen)
                {
                    SelfTest.Warn("the Mount Journal didn't open; nothing measured");
                    yield break;
                }
                MountsPanel.TestSelect(MountCatalog.DonkeyId);
                yield return SelfTest.Frames(3);
                foreach ((string id, string label) in Looks.SkinChoicesOf(MountCatalog.DonkeyId))
                {
                    skinRow.BoxedValue = id;
                    yield return SelfTest.Frames(3);
                    MountLook look = pc.LookOf(id);
                    bool lanternWas = look.Lantern, byDayWas = look.LanternByDay;
                    try
                    {
                        foreach (var s in states)
                        {
                            look.Lantern = s.Lantern;
                            look.LanternByDay = s.ByDay;
                            yield return SelfTest.Frames(4);
                            yield return SelfTest.Until(() => !PreviewStage.TestCartMeasuring, 8f);
                            yield return SelfTest.Frames(2);
                            // the moment the runs before 2026-11-01 measured at; then until the look is worn and framed for
                            // what the stage draws (a pack skin's picture kept its no-lamp framing, 2026-11-01: a sheet still building?)
                            string stageBefore = PreviewStage.TestCartState, looksBefore = PreviewStage.TestShownLooks;
                            int settleFrames = 0;
                            float settleBy = Time.unscaledTime + 10f;
                            while ((PreviewStage.TestShownSettling || PreviewStage.TestCartMeasuring || PreviewStage.TestCartVariant != PreviewStage.TestCartVariantNow) && Time.unscaledTime < settleBy)
                            {
                                settleFrames++;
                                yield return null;
                            }
                            bool settled = !PreviewStage.TestShownSettling && !PreviewStage.TestCartMeasuring && PreviewStage.TestCartVariant == PreviewStage.TestCartVariantNow;
                            if (settleFrames > 0 || !settled) SelfTest.Info($"lamp framing: {label}, lamp {s.Name}: {(settled ? $"settled after {settleFrames} frame(s)" : "NOT settled in 10 s")}; before: {stageBefore}; the paper's look before: {looksBefore}; {(settled ? "" : "now: " + PreviewStage.TestCartState + "; " + PreviewStage.TestShownLooks)}");
                            yield return SelfTest.Frames(2);
                            string variant = PreviewStage.TestCartVariant ?? "-";
                            bool withLamp = PreviewStage.TestDrawnRect(PreviewStage.TestHide.Nothing, out int ax, out int ay, out int aw, out int ah, out int pw, out int ph);
                            bool noGlow = PreviewStage.TestDrawnRect(PreviewStage.TestHide.Glow, out int gx, out int gy, out int gw, out int gh, out _, out _);
                            bool body = PreviewStage.TestDrawnRect(PreviewStage.TestHide.Lamp, out int bx, out int by, out int bw, out int bh, out _, out _);
                            string parts = PreviewStage.TestLampReport();
                            string stageState = PreviewStage.TestCartState; // why a picture kept a framing that no longer fits (a pack skin's, 2026-11-01)
                            if (!withLamp || !body || !noGlow)
                            {
                                SelfTest.Info($"lamp framing: {label} {s.Name}: no picture to measure ({variant}); {parts}; the stage: {stageState}");
                                continue;
                            }
                            float dx = bx + bw / 2f - pw / 2f, dy = by + bh / 2f - ph / 2f;
                            lines.Add(string.Join("\t", label, s.Name, variant, pw.ToString(), ph.ToString(), $"{ax} {ay} {aw} {ah}", $"{gx} {gy} {gw} {gh}", $"{bx} {by} {bw} {bh}", $"{dx:0.#} {dy:0.#}", $"{aw - bw} {ah - bh}", $"{aw - gw} {ah - gh}", parts, stageState, settled ? settleFrames.ToString() : "not in 10 s", stageBefore, looksBefore));
                            SelfTest.Info($"lamp framing: {label}, lamp {s.Name} ({variant}): picture {pw} x {ph}, the mount and cart {bw} x {bh} at ({bx}, {by}), their centre {dx:+0.#;-0.#;0} {dy:+0.#;-0.#;0} px off the middle; with the lamp drawn {aw} x {ah}, without its glow {gw} x {gh}; {parts}; the stage: {stageState}");
                        }
                    }
                    finally
                    {
                        look.Lantern = lanternWas;
                        look.LanternByDay = byDayWas;
                    }
                }
            }
            finally
            {
                skinRow.BoxedValue = skinWas;
                if (MountsPanel.IsOpen && !string.IsNullOrEmpty(mountWas)) MountsPanel.TestSelect(mountWas);
                if (MountsPanel.IsColoursOpen) MountsPanel.TestStepBack();
                if (MountsPanel.IsOpen) MountsPanel.TestStepBack();
                try { File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "GK2-lamp-framing.tsv"), string.Join("\n", lines) + "\n", new UTF8Encoding(false)); }
                catch (Exception e) { SelfTest.Info("GK2-lamp-framing.tsv couldn't be written: " + e.Message); }
            }
            SelfTest.Info($"lamp framing: {lines.Count - 1} measurement(s) in BepInEx\\GK2-lamp-framing.tsv; the skin, the mount and every lantern setting back as they were");
        }
    }
}
