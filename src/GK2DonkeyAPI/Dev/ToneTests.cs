using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A skin's own tones (docs/mount-colours.md section 7; the user, 2026-11-03): a temp skin over the donkey with two tones,
    // a red cloth and a blue trim, and a grey strap under [Colors] Harness. Read: the tones in their order, their colours
    // in one place only (a colour twice, a bad id: refused; a palette skin: ignored), the skin's info listing them apart
    // from its regions. Drawn: as drawn until set; the cloth set to Gold through the look (MountLook.Tone, the
    // companion's path) changes the cloth's colour only, in the pairs and in what the world draws; the Harness row the
    // strap only. The rows: a choice, its custom colour and the lightness per tone under the skin's keys, labelled by the
    // tone's name, the choice setting the look, the custom row shown with Custom only; a skin registered later gets
    // its tone rows at CatchUp, with its skin's other rows and not after the page's next heading.
    internal static class ToneTests
    {
        private const string SkinId = "donkey/selftest.tones", LateId = "donkey/selftest.tones-late";
        private static readonly Color32 Cloth = new Color32(196, 30, 56, 255), Trim = new Color32(40, 80, 180, 255), Strap = new Color32(128, 128, 128, 255);

        internal static IEnumerator Tones()
        {
            const string owner = SelfTest.Owner + ".tones";
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-tones");
            string lateRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-tones-late");
            string cfgPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-tones.cfg");
            bool registered = false, lateRegistered = false;
            MountHandle a = null;
            try
            {
                WriteSkin(root, "selftest.tones", "Tone Test", "[Colors]\nHarness = 808080\n[Tone.cloth]\nname = Cloth\ncolours = C41E38\n[Tone.trim]\nname = Trim\ncolours = 2850B4\n");
                if (System.IO.File.Exists(cfgPath)) System.IO.File.Delete(cfgPath);
            }
            catch (Exception e)
            {
                SelfTest.Warn("the temp skin could not be written: " + e.Message);
                yield break;
            }
            try
            {
                ReadChecks();
                registered = Looks.RegisterFolder(root, owner) == 1;
                Looks.SkinDef def = Looks.Find(SkinId);
                SelfTest.Check(registered && def != null, "the temp skin with two tones is registered");
                if (def == null) yield break;
                SelfTest.Check(def.Tones != null && def.Tones.Count == 2 && def.Tones[0].Id == "cloth" && def.Tones[0].Name == "Cloth" && def.Tones[1].Id == "trim",
                    $"its tones in the file's order with their names ({(def.Tones == null ? "none" : string.Join(", ", def.Tones.ConvertAll(t => t.Id + " " + t.Name)))})");
                MountSkinInfo info = Mounts.Looks.Find(SkinId);
                bool regionsPlain = info != null;
                if (info != null) foreach (string r in info.Regions) regionsPlain &= !SkinTones.IsTone(r);
                SelfTest.Check(info != null && info.Tones.Count == 2 && info.Tones[0].Colours.Count == 1 && info.Tones[0].Colours[0] == "C41E38" && regionsPlain && MountSettingsPage.SkinHasRegion(SkinId, "Harness"),
                    $"the skin's info lists the tones with their colours, its regions without them ({(info == null ? "no info" : string.Join(", ", info.Regions))})");

                // drawn: a test donkey wearing it (its own colours only, as the companion's donkey: Instance.OwnLook)
                Vector3 near = SelfTest.Spot(SelfTest.home, 4f) ?? SelfTest.home;
                a = SelfTest.Spawn(near, true, owner: owner);
                if (a != null) a.I.OwnLook = true;
                a?.Tuning.Set(owner, "Skin", SkinId);
                yield return SelfTest.Until(() => a != null && a.IsReady && a.I.LooksRig.Wearing == SkinId && !a.I.LooksRig.Building, 10f);
                if (!SelfTest.waited)
                {
                    SelfTest.Fail("the tone skin was never worn");
                    yield break;
                }
                yield return SelfTest.Frames(3);
                var d = a.I.ColorsRig.TestMountGroup();
                SelfTest.Check(!Changed(d.ChangedSources, Cloth) && !Changed(d.ChangedSources, Trim) && !Changed(d.ChangedSources, Strap) && d.Method == SpriteColorMethod.Palette,
                    $"as drawn until set: the cloth, the trim and the strap unchanged, {d.Colours} colours on the exact swap ({d.Method})");

                // the cloth Gold through the look, as the companion applies it
                MountCompanion me = Mounts.Companion(owner);
                MountLook look = me.LookOf(SkinId);
                look.Tone("cloth").Color = ToneColor.Gold;
                Dictionary<string, Colors.RegionTweak> values = SkinTones.ValuesOf(look, SkinId);
                SelfTest.Check(values != null && values.Count == 1 && values.ContainsKey("cloth") && values["cloth"].Colorize == "D8A838",
                    $"the look's values: the cloth's Gold, nothing for the trim ({(values == null ? "none" : string.Join(", ", new List<string>(values.Keys)))})");
                SkinTones.Apply(a.I, values, owner);
                yield return SelfTest.Frames(3);
                d = a.I.ColorsRig.TestMountGroup();
                SelfTest.Check(Changed(d.ChangedSources, Cloth) && !Changed(d.ChangedSources, Trim) && !Changed(d.ChangedSources, Strap),
                    $"the cloth set to Gold changes the cloth's colour and neither the trim's nor the Harness strap's ({d.Changed} of {d.Pairs} pairs changed)");

                // what the world draws (the overlay alone, lit at noon; against the same render with the swap off)
                SpriteRenderer ov = a.I.LooksRig.TestOverlay();
                SelfTest.DrawnAlone drawn = ov != null ? SelfTest.RenderAlone(ov) : null;
                SelfTest.DrawnAlone plain = ov != null ? SelfTest.RenderAlone(ov, swapOff: true) : null;
                if (drawn == null || plain == null) SelfTest.Warn("no game camera: the readback of the drawn tones skipped");
                else
                {
                    SelfTest.Info($"drawn: red {drawn.Red}, gold {drawn.Gold}, blue {drawn.Blue}, other {drawn.Other}; swap off: red {plain.Red}, gold {plain.Gold}, blue {plain.Blue}, other {plain.Other}");
                    SelfTest.Check(plain.Red >= 40 && drawn.Red <= plain.Red / 10 && drawn.Gold >= plain.Red * 6 / 10,
                        $"the world draws the cloth gold: its red pixels gone ({plain.Red} red with the swap off, {drawn.Red} with it), {drawn.Gold} gold");
                    SelfTest.Check(plain.Blue >= 40 && Math.Abs(drawn.Blue - plain.Blue) <= plain.Blue / 10,
                        $"and the trim as drawn ({plain.Blue} blue pixels with the swap off, {drawn.Blue} with it)");
                }

                // the Harness row's colour reaches the strap, not the tones
                a.Tuning.Set(owner, "HarnessColorize", "00FF00");
                a.Tuning.Set(owner, "HarnessColorizeAmount", 1f);
                yield return SelfTest.Frames(3);
                d = a.I.ColorsRig.TestMountGroup();
                SelfTest.Check(Changed(d.ChangedSources, Strap) && !Changed(d.ChangedSources, Trim), "a harness colour changes the strap named Harness and leaves the trim's tone alone");
                look.Tone("cloth").Color = ToneColor.Game;
                SkinTones.Apply(a.I, SkinTones.ValuesOf(look, SkinId), owner);
                yield return SelfTest.Frames(3);
                d = a.I.ColorsRig.TestMountGroup();
                SelfTest.Check(!Changed(d.ChangedSources, Cloth), "Game default puts the cloth back as drawn");
                // the tones go with their owner's overrides: another owner's clearing leaves them, the owner's own takes them
                look.Tone("cloth").Color = ToneColor.Gold;
                SkinTones.Apply(a.I, SkinTones.ValuesOf(look, SkinId), owner);
                Mounts.ClearTuning(owner + ".someone-else");
                bool kept = a.I.Tones != null;
                Mounts.ClearTuning(owner);
                SelfTest.Check(kept && a.I.Tones == null && a.I.TonesOwner == null, "the tones stay through another mod's ClearTuning and go with their owner's, as its overrides do");
                look.Tone("cloth").Color = ToneColor.Game;

                // the rows
                var cfg = new ConfigFile(cfgPath, false) { SaveOnConfigSet = false };
                var page = new MountSettingsPage(owner, cfg, "Tone test", "test", "", null);
                page.MountRows("Mount", me);
                page.PerSkin((skinId, mountId, sfx, shown) =>
                {
                    if (mountId.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) page.ToneRows("Colours", skinId, sfx, me);
                });
                page.Heading("Colours", "A later heading");
                MountSettingsPage.Row RowOf(string key) => page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == key);
                string sx = "_" + SkinId;
                MountSettingsPage.Row cloth = RowOf("Tone.cloth" + sx), custom = RowOf("CustomTone.cloth" + sx), light = RowOf("CustomTone.clothLightness" + sx), trim = RowOf("Tone.trim" + sx);
                int toneRows = 0;
                foreach (MountSettingsPage.Row r in page.Rows) if (r.Entry != null && r.Entry.Definition.Key.StartsWith("Tone.", StringComparison.Ordinal) && r.SkinId != null && r.SkinId.Equals(SkinId, StringComparison.OrdinalIgnoreCase)) toneRows++;
                SelfTest.Check(cloth != null && custom != null && light != null && trim != null && toneRows == 2 && cloth.SkinId == SkinId && custom.SkinId == SkinId,
                    $"a choice, its custom colour and the lightness per tone, under the skin's keys (Tone.cloth{sx}, CustomTone.cloth{sx}, CustomTone.clothLightness{sx}; {toneRows} tone choices)");
                if (cloth == null || custom == null || light == null) yield break;
                string label = Texts.Label(cloth), customLabel = Texts.Label(custom), lightLabel = Texts.Label(light), gold = Texts.Choice(cloth, (int)ToneColor.Gold);
                SelfTest.Check(label.Contains("Cloth") && customLabel.Contains("Cloth") && customLabel != label && lightLabel.Contains("Cloth") && gold.Contains("Gold"),
                    $"labelled by the tone's name, the choices the API's words ({label} | {customLabel} | {lightLabel} | {gold})");
                var entry = (ConfigEntry<ToneColor>)cloth.Entry;
                bool hidden = custom.Enabled != null && !custom.Enabled();
                entry.Value = ToneColor.Custom;
                bool showsWithCustom = custom.Enabled == null || custom.Enabled();
                entry.Value = ToneColor.Purple;
                SelfTest.Check(hidden && showsWithCustom && look.Tone("cloth").Color == ToneColor.Purple, $"the custom colour shows with Custom only, and the choice sets the look ({look.Tone("cloth").Color})");
                entry.Value = ToneColor.Game;

                // a skin registered later: its tone rows at CatchUp, before the page's later heading
                bool wrote = true;
                try { WriteSkin(lateRoot, "selftest.tones-late", "Tone Test Late", "[Tone.sash]\nname = Sash\ncolours = C41E38\n"); }
                catch (Exception e) { SelfTest.Warn("the late temp skin could not be written: " + e.Message); wrote = false; }
                if (!wrote) yield break;
                lateRegistered = Looks.RegisterFolder(lateRoot, owner + ".late") == 1;
                int caught = page.CatchUp();
                MountSettingsPage.Row sash = RowOf("Tone.sash_" + LateId);
                int heading = page.Rows.FindIndex(r => r.RowKind == MountSettingsPage.RowKind.Note && r.Label == "A later heading");
                int at = sash != null ? page.Rows.IndexOf(sash) : -1;
                SelfTest.Check(lateRegistered && caught >= 1 && sash != null && at >= 0 && at < heading, $"a skin registered after the page gets its tone rows at CatchUp, with the skins' rows (row {at}, the later heading at {heading})");
            }
            finally
            {
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                if (registered) Looks.Unregister(SkinId, owner);
                if (lateRegistered) Looks.Unregister(LateId, owner + ".late");
                try { if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true); } catch { }
                try { if (System.IO.Directory.Exists(lateRoot)) System.IO.Directory.Delete(lateRoot, true); } catch { }
                try { System.IO.File.Delete(cfgPath); } catch { }
            }
        }

        // skin.cfg's tone sections read as the loader reads them: a colour twice and a bad id refused, a palette skin's
        // tones ignored and a tone without colours of its own colouring nothing, each said in the report; a variant's
        // merge: a tone of its default's id without a name keeps the default's (its translation too), with one renames it
        private static void ReadChecks()
        {
            string Refused(string body)
            {
                try
                {
                    Looks.FromCfg("[Skin]\nformat = 1\nname = Bad\nlayer = over\n" + body, "selftest.bad", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
                    return null;
                }
                catch (FormatException e)
                {
                    return e.Message;
                }
            }
            string twice = Refused("[Colors]\nHarness = C41E38\n[Tone.cloth]\nname = Cloth\ncolours = C41E38\n");
            string twoTones = Refused("[Tone.a]\ncolours = C41E38\n[Tone.b]\ncolours = C41E38\n");
            string badId = Refused("[Tone.Red_Cloth]\ncolours = C41E38\n");
            SelfTest.Check(twice != null && twice.Contains("one place per colour") && twoTones != null && badId != null && badId.Contains("lower case"),
                $"a colour under [Colors] and a tone, or in two tones, and a tone id with '_' are refused ({twice} | {twoTones} | {badId})");
            Looks.SkinDef palette = Looks.FromCfg("[Skin]\nformat = 1\nname = Palette\ncolours = palette\n[Tone.cloth]\ncolours = C41E38\n", "selftest.palette", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
            Looks.SkinDef empty = Looks.FromCfg("[Skin]\nformat = 1\nname = Empty\nlayer = over\n[Tone.cloth]\nname = Cloth\n", "selftest.empty", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
            bool emptyKept = empty.Tones != null && empty.Tones.Count == 1 && empty.Tones[0].NameGiven && SkinTones.ColoursOf(empty, "cloth").Count == 0;
            SelfTest.Check(palette.Tones == null && palette.ColorRegions == null && palette.Report.Contains("[Tone.*]") && emptyKept && empty.Report.Contains("names no colours"),
                "a palette skin's tones are ignored; a tone without colours of its own is kept for its name only (no colours, so no row), each said in the report");
            var fromDefault = new List<SkinTones.ToneDef> { new SkinTones.ToneDef { Id = "cloth", Name = "Cloth", NameGiven = true }, new SkinTones.ToneDef { Id = "trim", Name = "Trim", NameGiven = true } };
            var addsShade = new List<SkinTones.ToneDef> { new SkinTones.ToneDef { Id = "cloth", Name = "cloth" }, new SkinTones.ToneDef { Id = "plume", Name = "Plume", NameGiven = true } };
            var renames = new List<SkinTones.ToneDef> { new SkinTones.ToneDef { Id = "trim", Name = "Braid", NameGiven = true } };
            List<SkinTones.ToneDef> shade = SkinTones.Merge(fromDefault, addsShade), renamed = SkinTones.Merge(fromDefault, renames);
            SelfTest.Check(shade.Count == 3 && shade[0] == fromDefault[0] && shade[2].Id == "plume" && renamed.Count == 2 && renamed[1] == renames[0] && renamed[0] == fromDefault[0],
                $"a variant's tones: the default's in their order, one re-declared without a name stays the default's (its name and translation), one with a name renames it, its own new ones after ({string.Join(", ", shade.ConvertAll(t => t.Name))} | {string.Join(", ", renamed.ConvertAll(t => t.Name))})");
        }

        private static bool Changed(List<Color32> sources, Color32 c) => sources.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b);

        // A temp skin over the donkey under root\gk2mounts\donkey\skins\<id>\: its standing picture a 12x10 block of the
        // cloth's red, then the trim's blue, then the strap's grey, four columns each.
        private static void WriteSkin(string root, string id, string name, string sections)
        {
            string dir = SelfTest.TempSkinDir(root, Catalog.DonkeyId, id);
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skin.cfg"), "[Skin]\nformat = 1\nname = " + name + "\nlayer = over\n" + sections);
            var px = new Color32[96 * 96];
            for (int y = 40; y < 50; y++)
            {
                for (int x = 48; x < 60; x++) px[y * 96 + x] = x < 52 ? Cloth : x < 56 ? Trim : Strap;
            }
            var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "bdy.png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }
}
