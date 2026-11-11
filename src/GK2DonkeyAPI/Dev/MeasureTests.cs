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

        // A skin drawn over the donkey (layer = over) against the donkey under it, on screen, in art pixels (the user,
        // 2026-11-11: a sliver of the coat in the corners of Royal Parade's cloth and a brown line under Armored's collar
        // strap in the colours window, each about a quarter of an art pixel; the art itself has no such pixels). For every
        // frame shown: where the skin's canvas pixel (0, 0) lands against the donkey's own, across and up the screen
        // and along the view, with the game frame's trim offset (Looks.Rig.Follow adds its fractions) and the overlay's
        // local position. Two places: a donkey in the world under the game's camera, walking, and the Mount Journal's
        // picture (the paper, under the stage's camera). BepInEx\GK2-skin-offset.tsv.
        internal static IEnumerator SkinOffset()
        {
            var lines = new List<string> { "where\tskin\tframe\tflip\tpixel grid off across (art px)\tpixel grid off up (art px)\tcanvas origin across\tcanvas origin up\talong the view (art px)\ttrim offset (x y)\toverlay local (x y z)\tpart sprite rect (w h)\tppu\tthe donkey's atlas sampling\tthe skin sheet's sampling\tthe material / shader" };
            var seen = new HashSet<string>();
            var worst = new Dictionary<string, (float A, float U)>();
            string[] skins = { "saddleup-donkey-skins.royal-parade", "saddleup-donkey-skins.armored" };
            var saved = new HashSet<string>();
            string shots = Path.Combine(Paths.BepInExRootPath, "GK2-skin-offset");
            try { Directory.CreateDirectory(shots); } catch { }

            void Measure(string where, string skin, SpriteRenderer part, SpriteRenderer ov, Quaternion camRot)
            {
                if (part == null || !part || ov == null || !ov || !ov.enabled || part.sprite == null || ov.sprite == null) return;
                string key = where + "|" + skin + "|" + part.sprite.name + "|" + part.flipX;
                if (seen.Contains(key)) return;
                if (!PreviewStage.CanvasWorld(part, new Vector2(0f, 0f), out Vector3 p0) || !PreviewStage.CanvasWorld(part, new Vector2(1f, 0f), out Vector3 p1)
                    || !PreviewStage.CanvasWorld(part, new Vector2(0f, 1f), out Vector3 p2) || !PreviewStage.CanvasWorld(ov, new Vector2(0f, 0f), out Vector3 o0))
                {
                    if (seen.Add(key + "|unread")) SelfTest.Info($"skin offset: {where} {skin} {part.sprite.name}: a sprite's mesh couldn't be read");
                    return;
                }
                seen.Add(key);
                Vector3 right = camRot * Vector3.right, up = camRot * Vector3.up, fwd = camRot * Vector3.forward;
                Vector3 d = o0 - p0;
                float ax = Vector3.Dot(p1 - p0, right), uy = Vector3.Dot(p2 - p0, up);
                float across = Mathf.Abs(ax) > 1e-6f ? Vector3.Dot(d, right) / ax : float.NaN;
                float upward = Mathf.Abs(uy) > 1e-6f ? Vector3.Dot(d, up) / uy : float.NaN;
                float pxWorld = (p1 - p0).magnitude;
                float along = pxWorld > 1e-6f ? Vector3.Dot(d, fwd) / pxWorld : float.NaN;
                Vector2 trim = Vector2.zero;
                try { trim = part.sprite.textureRectOffset; } catch { }
                // the donkey's own pixels sit at its trim offset's fraction on its canvas (the first run, 2026-11-11: the
                // canvas origins 0.051 across apart, exactly the fraction): the grids' offset is the origins' less it
                float gridAcross = across - (trim.x - Mathf.Floor(trim.x)), gridUp = upward - (trim.y - Mathf.Floor(trim.y));
                Vector3 lp = ov.transform.localPosition;
                lines.Add(string.Join("\t", where, skin, part.sprite.name, part.flipX ? "flipped" : "-", gridAcross.ToString("0.###"), gridUp.ToString("0.###"),
                    across.ToString("0.###"), upward.ToString("0.###"), along.ToString("0.###"), $"{trim.x:0.###} {trim.y:0.###}", $"{lp.x:0.#####} {lp.y:0.#####} {lp.z:0.#####}",
                    $"{part.sprite.rect.width:0} {part.sprite.rect.height:0}", part.sprite.pixelsPerUnit.ToString("0.##"),
                    // how each picture is sampled: a smoothed one blends a shown pixel with the hidden one beside it
                    $"{part.sprite.texture.filterMode} {part.sprite.texture.width}x{part.sprite.texture.height} mips {part.sprite.texture.mipmapCount}",
                    $"{ov.sprite.texture.filterMode} mips {ov.sprite.texture.mipmapCount}",
                    part.sharedMaterial != null ? part.sharedMaterial.name + " / " + (part.sharedMaterial.shader != null ? part.sharedMaterial.shader.name : "-") : "-"));
                string wk = where + " " + skin;
                worst.TryGetValue(wk, out var w);
                worst[wk] = (Mathf.Max(w.A, Mathf.Abs(gridAcross)), Mathf.Max(w.U, Mathf.Abs(gridUp)));
            }

            // the world: a donkey of the test's wearing each skin, standing, then walking
            Camera main = Camera.main;
            if (main != null) SelfTest.Info($"skin offset: the game camera turned {main.transform.eulerAngles} (the stage's: {PreviewStage.TestCameraRotation.eulerAngles}), orthographic {main.orthographic}, size {main.orthographicSize:0.###}, screen {Screen.width}x{Screen.height}");
            foreach (string sk in skins)
            {
                string id = Looks.SkinKey(MountCatalog.DonkeyId, sk);
                if (Looks.Find(id) == null) { SelfTest.Info($"skin offset: {sk} isn't installed; left out"); continue; }
                Vector3 near = SelfTest.Spot(SelfTest.home, 4f) ?? SelfTest.home;
                MountHandle a = SelfTest.Spawn(near, true);
                try
                {
                    if (a == null) { SelfTest.Info("skin offset: no test donkey could be made"); continue; }
                    a.I.OwnLook = true;
                    a.Tuning.Set(SelfTest.Owner, "Skin", id);
                    yield return SelfTest.Until(() => a.IsReady && a.I.LooksRig.Wearing == id && !a.I.LooksRig.Building, 10f);
                    if (!SelfTest.waited) { SelfTest.Info($"skin offset: the test donkey never wore {sk}"); continue; }
                    yield return SelfTest.Frames(3);
                    SpriteRenderer part = a.I.LooksRig.TestPart(), ov = a.I.LooksRig.TestOverlay();
                    Measure("world", sk, part, ov, main != null ? main.transform.rotation : Quaternion.identity);
                    yield return SelfTest.WalkFor(a, part, 90, () => Measure("world", sk, a.I.LooksRig.TestPart(), a.I.LooksRig.TestOverlay(), main != null ? main.transform.rotation : Quaternion.identity), null);
                }
                finally
                {
                    a?.Remove();
                }
                yield return SelfTest.Frames(2);
            }

            // the Mount Journal's picture: the paper wearing each skin, under the stage's camera (the game camera's pitch)
            MountSettingsPage page = PageWithMounts();
            ConfigEntryBase skinRow = null;
            if (page != null)
            {
                foreach (MountSettingsPage.Row r in page.Rows)
                {
                    if (r.Entry != null && r.Entry.Definition.Key == "Skin_" + MountCatalog.DonkeyId) { skinRow = r.Entry; break; }
                }
            }
            if (page == null || skinRow == null || LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Info("skin offset: the journal's picture left out (no page with the donkey's Skin row, or a window open)");
            }
            else
            {
                object skinWas = skinRow.BoxedValue;
                string mountWas = page.MountChosen;
                Quaternion stageRot = PreviewStage.TestCameraRotation;
                MountsPanel.Open(page, toggle: false);
                yield return SelfTest.Frames(3);
                try
                {
                    if (MountsPanel.IsOpen)
                    {
                        MountsPanel.TestSelect(MountCatalog.DonkeyId);
                        yield return SelfTest.Frames(3);
                        foreach (string sk in skins)
                        {
                            string id = Looks.SkinKey(MountCatalog.DonkeyId, sk);
                            if (Looks.Find(id) == null) continue;
                            skinRow.BoxedValue = id;
                            Mount.Instance paper = null;
                            yield return SelfTest.Until(() => (paper = MountsPanel.PaperOf(page)) != null && paper.LooksRig.Wearing == id && !paper.LooksRig.Building, 10f);
                            if (!SelfTest.waited) { SelfTest.Info($"skin offset: the journal's picture never wore {sk}"); continue; }
                            // the picture animates: three seconds of its frames
                            float until = Time.unscaledTime + 3f;
                            while (Time.unscaledTime < until)
                            {
                                paper = MountsPanel.PaperOf(page);
                                if (paper != null) Measure("journal", sk, paper.LooksRig.TestPart(), paper.LooksRig.TestOverlay(), stageRot);
                                yield return null;
                            }
                            // the colours window's picture (the mount alone, larger: the user's screenshots are of it)
                            MountsPanel.TestOpenColours();
                            yield return SelfTest.Frames(3);
                            if (MountsPanel.IsColoursOpen)
                            {
                                until = Time.unscaledTime + 3f;
                                while (Time.unscaledTime < until)
                                {
                                    paper = MountsPanel.PaperOf(page);
                                    if (paper != null) Measure("colours", sk, paper.LooksRig.TestPart(), paper.LooksRig.TestOverlay(), stageRot);
                                    // the picture as drawn, once per frame of the walk: the rendered pixels themselves
                                    SpriteRenderer pp = paper?.LooksRig.TestPart();
                                    string fk = pp != null && pp.sprite != null ? pp.sprite.name : null;
                                    if (fk != null && saved.Add(sk + "|" + fk)) SavePicture(MountsPanel.TestShownPicture, Path.Combine(shots, sk.Replace("saddleup-donkey-skins.", "") + "-" + fk.Replace("0301_", "") + ".png"));
                                    yield return null;
                                }
                                MountsPanel.TestCloseColours(keep: true);
                                yield return SelfTest.Frames(2);
                            }
                            else SelfTest.Info($"skin offset: the colours window didn't open for {sk}");
                        }
                    }
                    else SelfTest.Info("skin offset: the Mount Journal didn't open; its picture left out");
                }
                finally
                {
                    skinRow.BoxedValue = skinWas;
                    if (MountsPanel.IsOpen && !string.IsNullOrEmpty(mountWas)) MountsPanel.TestSelect(mountWas);
                    if (MountsPanel.IsOpen) MountsPanel.TestStepBack();
                }
            }

            try { File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "GK2-skin-offset.tsv"), string.Join("\n", lines) + "\n", new UTF8Encoding(false)); }
            catch (Exception e) { SelfTest.Info("GK2-skin-offset.tsv couldn't be written: " + e.Message); }
            foreach (var kv in worst) SelfTest.Info($"skin offset: {kv.Key}: the skin's pixel grid off the donkey's by up to {kv.Value.A:0.###} art px across and {kv.Value.U:0.###} up (0: on it)");
            SelfTest.Info($"skin offset: {lines.Count - 1} frame(s) measured in BepInEx\\GK2-skin-offset.tsv");
        }

        // A picture (a render texture or a texture) written as a PNG, as drawn.
        private static void SavePicture(Texture t, string path)
        {
            if (t == null) return;
            RenderTexture prev = RenderTexture.active;
            RenderTexture tmp = null;
            Texture2D read = null;
            try
            {
                tmp = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32);
                tmp.filterMode = FilterMode.Point;
                Graphics.Blit(t, tmp);
                RenderTexture.active = tmp;
                read = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0, false);
                read.Apply(false);
                File.WriteAllBytes(path, read.EncodeToPNG());
            }
            catch (Exception e)
            {
                SelfTest.Info("skin offset: the picture couldn't be saved: " + e.Message);
            }
            finally
            {
                RenderTexture.active = prev;
                if (tmp != null) RenderTexture.ReleaseTemporary(tmp);
                if (read != null) UnityEngine.Object.Destroy(read);
            }
        }

        // Every skin of every mount written as the game builds it, for the review page (tools/skins/skin_preview_html.py
        // reads BepInEx\GK2-asset-probe\game-skins\ first; the user, 2026-11-05: the Mounts tab's button wrote only the
        // skins worn at that moment, "we need something that will export all of them"): Looks.ExportEverySkin in every run,
        // so the folder is the last run's build.
        internal static IEnumerator ExportSkins()
        {
            string result = null;
            yield return Looks.ExportEverySkin(Path.Combine(Paths.BepInExRootPath, "GK2-asset-probe", Looks.ExportDirName), r => result = r);
            if (result == null || result.StartsWith("failed", StringComparison.Ordinal)) SelfTest.Fail("every skin exported as built: " + (result ?? "no result"));
            else if (result.Contains("not built in two minutes")) SelfTest.Warn("every skin exported as built: " + result);
            else SelfTest.Info("every skin exported as built: " + result);
        }

        // The colours window of every skin of every mount as the player sees it (the user's look at the skins whose parts
        // became tones and the looks whose regions got names, 2026-11-04): a picture each,
        // BepInEx\GK2-panel-shots\colours-skin-<mount>-<skin>.png (and -end where its rows scroll), taken once the window's
        // picture is drawn with the dev panel hidden; and each skin's colour rows in words, as the window draws them (the
        // label, the first choice, the value, shown or left out, live or greyed), BepInEx\GK2-colours-rows.tsv.
        internal static IEnumerator ColoursWindows()
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
            // each mount's Skin row, and its value to put back
            var skinRows = new List<(string Mount, ConfigEntryBase Row, object Was)>();
            foreach (MountInfo m in Mounts.Catalog.All)
            {
                foreach (MountSettingsPage.Row r in page.Rows)
                {
                    if (r.Entry != null && r.Entry.Definition.Key == "Skin_" + m.Id) { skinRows.Add((m.Id, r.Entry, r.Entry.BoxedValue)); break; }
                }
            }
            if (skinRows.Count == 0)
            {
                SelfTest.Skip("the page has no Skin rows");
                yield break;
            }
            string mountWas = page.MountChosen;
            bool panelWasHidden = Plugin.TuningWindowHidden;
            var lines = new List<string> { "mount\tskin\trow key\tlabel as drawn\tfirst choice\tvalue\tshown\tlive" };
            int shots = 0, looks = 0;
            MountsPanel.Open(page, toggle: false);
            yield return SelfTest.Frames(3);
            try
            {
                if (!MountsPanel.IsOpen)
                {
                    SelfTest.Warn("the Mount Journal didn't open; nothing taken");
                    yield break;
                }
                foreach ((string mount, ConfigEntryBase skinRow, object _) in skinRows)
                {
                    if (MountsPanel.IsColoursOpen) MountsPanel.TestCloseColours(true);
                    MountsPanel.TestSelect(mount);
                    yield return SelfTest.Frames(3);
                    foreach ((string id, string label) in Looks.SkinChoicesOf(mount))
                    {
                        if (MountsPanel.IsColoursOpen) MountsPanel.TestCloseColours(true);
                        skinRow.BoxedValue = id;
                        yield return SelfTest.Frames(3);
                        MountsPanel.TestOpenColours();
                        yield return SelfTest.Frames(3);
                        yield return SelfTest.Until(() => !PreviewStage.TestShownSettling, 10f);
                        // and until the window shows its picture and that picture is drawn (settled came first: in the first
                        // run one skin's picture was still empty, a few frames before it showed; the cart view alone was
                        // waited for until 2026-11-04, a mount drawn without a cart has the alone view)
                        yield return SelfTest.Until(() => MountsPanel.TestColoursTexture != null && PreviewStage.TestDrawnRect(PreviewStage.TestHide.Nothing, out _, out _, out _, out _, out _, out _, alone: PreviewStage.TestIsAloneView(MountsPanel.TestColoursTexture)), 10f);
                        yield return SelfTest.Frames(6);
                        int rows = 0, shown = 0;
                        foreach (MountSettingsPage.Row r in page.Rows)
                        {
                            if (r.Entry == null || !r.IsLook || r.RowKind != MountSettingsPage.RowKind.Choice || !string.Equals(r.SkinId, id, StringComparison.OrdinalIgnoreCase)) continue;
                            bool visible = (r.Visible == null || r.Visible()) && (r.Shown == null || r.Shown());
                            bool live = r.Enabled == null || r.Enabled();
                            lines.Add(string.Join("\t", mount, label, r.Entry.Definition.Key, Texts.Label(r), Texts.Choice(r, 0), r.Entry.BoxedValue?.ToString() ?? "-", visible ? "shown" : "left out", live ? "live" : "greyed"));
                            rows++;
                            if (visible) shown++;
                        }
                        Plugin.TuningWindowHidden = true;
                        yield return SelfTest.Frames(2);
                        // the picture as drawn at the shot (probe: the box was empty for every mount but the donkey, 2026-11-04)
                        bool cartDrawn = PreviewStage.TestDrawnRect(PreviewStage.TestHide.Nothing, out int pcx, out int pcy, out int pcw, out int pch, out int ppw, out int pph);
                        bool aloneDrawn = PreviewStage.TestDrawnRect(PreviewStage.TestHide.Nothing, out int pax, out int pay, out int paw, out int pah, out int papw, out int paph, alone: true);
                        SelfTest.Info($"colours picture: {mount}, {label}: {MountsPanel.TestColoursPicture()}; pixels drawn: the cart view {(cartDrawn ? $"{pcw}x{pch} at {pcx},{pcy} of {ppw}x{pph}" : "none")}, the alone view {(aloneDrawn ? $"{paw}x{pah} at {pax},{pay} of {papw}x{paph}" : "none")}");
                        string name = mount + "-" + id.Substring(id.LastIndexOf('/') + 1);
                        yield return SelfTest.PanelShot("colours-skin-" + name);
                        shots++;
                        bool scrolls = MountsPanel.TestColoursScroll;
                        if (scrolls)
                        {
                            // rows past the window's view: a second picture at the end of them
                            MountsPanel.TestScrollColours(0f);
                            yield return SelfTest.Frames(3);
                            yield return SelfTest.PanelShot("colours-skin-" + name + "-end");
                            MountsPanel.TestScrollColours(1f);
                            shots++;
                        }
                        // its other tabs (2026-11-08: a skin's own parts on a tab of their own, the cart's on its own), a
                        // picture of each
                        for (int ti = 1; ti < MountsPanel.TestColourTabCount; ti++)
                        {
                            if (!MountsPanel.TestShowColourTab(ti)) continue;
                            yield return SelfTest.Frames(3);
                            yield return SelfTest.PanelShot("colours-skin-" + name + "-tab" + (ti + 1));
                            shots++;
                        }
                        if (MountsPanel.TestColourTabCount > 1) MountsPanel.TestShowColourTab(0);
                        SelfTest.Info($"colours window: {mount}, {label}: tabs {MountsPanel.TestColourTabs()}");
                        Plugin.TuningWindowHidden = panelWasHidden;
                        looks++;
                        SelfTest.Info($"colours window: {mount}, {label}: {MountsPanel.TestColourRows} rows; of its {rows} colour choices {shown} shown{(scrolls ? "; they scroll (a second picture at their end)" : "")}{(PreviewStage.TestShownSettling ? "; its picture still settling" : "")}");
                    }
                }
            }
            finally
            {
                Plugin.TuningWindowHidden = panelWasHidden;
                foreach ((string _, ConfigEntryBase row, object was) in skinRows) row.BoxedValue = was;
                if (MountsPanel.IsColoursOpen) MountsPanel.TestStepBack();
                if (MountsPanel.IsOpen && !string.IsNullOrEmpty(mountWas)) MountsPanel.TestSelect(mountWas);
                if (MountsPanel.IsOpen) MountsPanel.TestStepBack();
                try { File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "GK2-colours-rows.tsv"), string.Join("\n", lines) + "\n", new UTF8Encoding(false)); }
                catch (Exception e) { SelfTest.Info("GK2-colours-rows.tsv couldn't be written: " + e.Message); }
            }
            SelfTest.Info($"colours windows: {looks} look(s), {shots} picture(s) in BepInEx\\GK2-panel-shots (colours-skin-*), {lines.Count - 1} colour row(s) in BepInEx\\GK2-colours-rows.tsv; every skin and the mount back as they were");
        }

        // The colour swap's cost (the user, 2026-11-04: a baby's own shade of a part "unless it's a performance cost we
        // can't afford"; a skin over the donkey with many parts wants more than the 64 pairs it keeps the exact swap to,
        // Colors.PaletteMaxColoursOver). The exact swap loops over every pair for every pixel of the mount's sprites
        // (docs/colors.md); the cube is one lookup. Frame times, uncapped (no vsync, no frame-rate target; both put back),
        // with 21 test donkeys in view drawn: as the game draws them; on the exact swap at the donkey's own pairs (a coat
        // colour set), at 64 (a skin over it with 22 colours of its own, all set: the limit until 2026-11-04), 72 (30)
        // and 96 (54: the limit since the same day); and on the cube with the 30 (the limit lowered for
        // the test; each configuration's limit its own, whatever PaletteMaxColoursOver is). Two loads: the
        // donkeys at their size, and twice as big (four times the pixels each: the shader's part of the frame apart from
        // the rest). Each configuration twice, the second round in the reverse order (drift), 2 s of frames each.
        // BepInEx\GK2-palette-cost.tsv.
        private sealed class CostConfig
        {
            internal string Name, Skin, Coat;
            internal int Limit;
        }

        private static string CostSkinId(int n) => "selftest.cost-" + n;

        internal static IEnumerator PaletteCost()
        {
            if (LazyWindowsStackController.ActiveWindow != null || MountsPanel.IsOpen || OptionsWindow.IsOpen)
            {
                SelfTest.Warn("a window is open: close it and run again; skipped");
                yield break;
            }
            if (Colors.SpriteMethod.Value != SpriteColorMethod.Palette)
            {
                SelfTest.Skip($"the donkeys' sprites are set to the {Colors.SpriteMethod.Value} path (Colors SpriteMethod), not the exact swap: nothing to compare");
                yield break;
            }
            const string owner = SelfTest.Owner + ".cost";
            string root = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-cost");
            int[] sizes = { 22, 30, 54 };
            var configs = new List<CostConfig>
            {
                new CostConfig { Name = "as the game draws them" },
                new CostConfig { Name = "the exact swap, the donkey's own colours", Coat = "6A4A8A" },
                new CostConfig { Name = "the exact swap, 22 colours of a skin's own", Skin = "donkey/" + CostSkinId(22), Limit = 64 },
                new CostConfig { Name = "the exact swap, 30 of a skin's own", Skin = "donkey/" + CostSkinId(30), Limit = 72 },
                new CostConfig { Name = "the exact swap, 54 of a skin's own", Skin = "donkey/" + CostSkinId(54), Limit = 96 },
                new CostConfig { Name = "the colour cube, the 30", Skin = "donkey/" + CostSkinId(30), Limit = 48 },
            };
            var lines = new List<string> { "load\tconfiguration\tpath\tpairs\tround\tframes\tmean ms\tmedian ms\tp90 ms\tgpu mean ms" };
            var medians = new Dictionary<string, List<float>>(StringComparer.Ordinal);
            var herd = new List<MountHandle>();
            int vsyncWas = QualitySettings.vSyncCount, rateWas = Application.targetFrameRate;
            bool gpu = FrameTimingManager.IsFeatureEnabled();
            var timings = new FrameTiming[1];
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                foreach (int n in sizes) WriteCostSkin(root, n);
                int registered = Looks.RegisterFolder(root, owner);
                if (registered != sizes.Length)
                {
                    SelfTest.Warn($"{registered} of the {sizes.Length} test skins registered; nothing measured");
                    yield break;
                }
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                foreach (float scale in new[] { 1f, 2f })
                {
                    foreach (MountHandle h in herd) if (h.Exists) h.Remove();
                    herd.Clear();
                    for (int row = 0; row < 3; row++)
                    {
                        for (int col = 0; col < 7; col++)
                        {
                            Vector3 want = SelfTest.home + new Vector3(-6f + 2f * col, 0f, -1.5f + 1.5f * row);
                            MountHandle h = SelfTest.Spawn(SelfTest.Spot(want, 1.5f) ?? want, col % 2 == 0, scale: scale);
                            if (h != null) herd.Add(h);
                        }
                    }
                    yield return SelfTest.Until(() => herd.TrueForAll(h => h.IsReady), 20f);
                    string load = herd.Count + " donkeys" + (scale > 1f ? " twice as big" : ""); // of the 21 asked for, the ones spawned
                    var order = new List<CostConfig>(configs);
                    for (int round = 1; round <= 2; round++)
                    {
                        if (round == 2) order.Reverse();
                        foreach (CostConfig c in order)
                        {
                            Mounts.ClearTuning(owner);
                            Colors.TestPaletteMaxOver = c.Limit;
                            Colors.MarkDirty();
                            foreach (MountHandle h in herd)
                            {
                                if (!h.Exists) continue;
                                h.I.OwnLook = true;
                                h.Tuning.Set(owner, "IdleShuffle", false);
                                if (c.Coat != null)
                                {
                                    h.Tuning.Set(owner, "CoatColorize", c.Coat);
                                    h.Tuning.Set(owner, "CoatColorizeAmount", 1f);
                                }
                                if (c.Skin != null)
                                {
                                    h.Tuning.Set(owner, "Skin", c.Skin);
                                    var gold = new Dictionary<string, Colors.RegionTweak>(StringComparer.Ordinal) { ["parts"] = new Colors.RegionTweak { Colorize = "D8A838", ColorizeAmount = 1f } };
                                    SkinTones.Apply(h.I, gold, owner, c.Skin);
                                }
                            }
                            yield return SelfTest.Until(() => herd.TrueForAll(h => !h.Exists || ((c.Skin == null || string.Equals(h.I.LooksRig.Wearing, c.Skin, StringComparison.OrdinalIgnoreCase)) && !h.I.LooksRig.Building)), 15f);
                            yield return SelfTest.Frames(10);
                            var group = herd.Count > 0 ? herd[0].I.ColorsRig.TestMountGroup() : (0, SpriteColorMethod.Palette, 0, 0, null);
                            string path = group.Pairs == 0 ? "none" : group.Method == SpriteColorMethod.Lut ? "cube" : "exact swap";
                            float settle = Time.unscaledTime + 0.5f;
                            while (Time.unscaledTime < settle) yield return null;
                            var ms = new List<float>();
                            float gpuSum = 0f;
                            int gpuN = 0;
                            float until = Time.unscaledTime + 2f;
                            while (Time.unscaledTime < until)
                            {
                                yield return null;
                                ms.Add(Time.unscaledDeltaTime * 1000f);
                                if (gpu)
                                {
                                    FrameTimingManager.CaptureFrameTimings();
                                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) { gpuSum += (float)timings[0].gpuFrameTime; gpuN++; }
                                }
                            }
                            ms.Sort();
                            float mean = 0f;
                            foreach (float v in ms) mean += v;
                            mean /= Mathf.Max(1, ms.Count);
                            float median = ms.Count > 0 ? ms[ms.Count / 2] : 0f, p90 = ms.Count > 0 ? ms[Mathf.Min(ms.Count - 1, ms.Count * 9 / 10)] : 0f;
                            string gpuText = gpuN > 0 ? (gpuSum / gpuN).ToString("0.00") : "-";
                            lines.Add(string.Join("\t", load, c.Name, path, group.Pairs.ToString(), round.ToString(), ms.Count.ToString(), mean.ToString("0.00"), median.ToString("0.00"), p90.ToString("0.00"), gpuText));
                            string key = load + "|" + c.Name;
                            if (!medians.TryGetValue(key, out List<float> m)) medians[key] = m = new List<float>();
                            m.Add(median);
                            SelfTest.Info($"colour swap's cost: {load}, {c.Name}: {path}, {group.Pairs} pairs, round {round}: {ms.Count} frames, median {median:0.00} ms, mean {mean:0.00}, p90 {p90:0.00}, gpu {gpuText}");
                        }
                    }
                    float baseMs = medians.TryGetValue(load + "|" + configs[0].Name, out List<float> b0) ? (b0[0] + b0[b0.Count - 1]) / 2f : 0f;
                    foreach (CostConfig c in configs)
                    {
                        if (!medians.TryGetValue(load + "|" + c.Name, out List<float> m)) continue;
                        float both = (m[0] + m[m.Count - 1]) / 2f;
                        SelfTest.Info($"colour swap's cost: {load}, {c.Name}: median {both:0.00} ms over both rounds, {both - baseMs:+0.00;-0.00;0.00} ms against the game's own drawing");
                    }
                }
            }
            finally
            {
                QualitySettings.vSyncCount = vsyncWas;
                Application.targetFrameRate = rateWas;
                Colors.TestPaletteMaxOver = 0;
                Mounts.ClearTuning(owner);
                foreach (MountHandle h in herd) if (h.Exists) h.Remove();
                foreach (int n in sizes) Looks.Unregister("donkey/" + CostSkinId(n), owner);
                Colors.MarkDirty();
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (Exception e) { SelfTest.Info("the test skins' folder couldn't be removed: " + e.Message); }
                try { File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "GK2-palette-cost.tsv"), string.Join("\n", lines) + "\n", new UTF8Encoding(false)); }
                catch (Exception e) { SelfTest.Info("GK2-palette-cost.tsv couldn't be written: " + e.Message); }
            }
            SelfTest.Info($"colour swap's cost: {lines.Count - 1} measurement(s) in BepInEx\\GK2-palette-cost.tsv (gpu times {(gpu ? "read" : "not available: the game's build keeps no frame timing")}); the frame rate, the limit and every test donkey and skin back as they were");
        }

        // A skin over the donkey with n colours of its own in one tone ("parts"), each drawn: bright, far from the donkey's.
        private static void WriteCostSkin(string root, int n)
        {
            string dir = SelfTest.TempSkinDir(root, MountCatalog.DonkeyId, CostSkinId(n));
            Directory.CreateDirectory(dir);
            var cols = new List<Color32>();
            var hexes = new List<string>();
            for (int i = 0; i < n; i++)
            {
                Color c = Color.HSVToRGB(i / (float)n, 0.85f, 0.95f);
                var c32 = new Color32((byte)Mathf.RoundToInt(c.r * 255f), (byte)Mathf.RoundToInt(c.g * 255f), (byte)Mathf.RoundToInt(c.b * 255f), 255);
                string hex = c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
                if (hexes.Contains(hex)) continue;
                cols.Add(c32);
                hexes.Add(hex);
            }
            File.WriteAllText(Path.Combine(dir, "skin.cfg"), "[Skin]\nformat = 1\nname = Cost " + n + "\nlayer = over\n[Tone.parts]\nname = Parts\ncolours = " + string.Join(" ", hexes) + "\n");
            var px = new Color32[96 * 96];
            int k = 0;
            for (int y = 36; y < 56; y++)
            {
                for (int x = 20; x < 76; x++) px[y * 96 + x] = cols[k++ % cols.Count];
            }
            var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "bdy.png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }
}
