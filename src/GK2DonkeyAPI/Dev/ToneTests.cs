using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A skin's own tones (docs/mount-colours.md section 7; the user, 2026-11-03): a temp skin over the donkey with two tones,
    // a red cloth and a blue trim that follows Harness (listed under [Colors] Harness too, the compatible listing of a
    // released skin given tones), and a grey strap under [Colors] Harness. Read: the tones in their order, their colours
    // in one place only (a colour twice, a bad id: the tone left out; a palette skin: ignored), the trim's moved from Harness to it,
    // the skin's info listing them apart from its regions. Drawn: as drawn until set, a tone nobody set unpaired; the
    // cloth set to Gold through the look (MountLook.Tone, the companion's path) changes the cloth's colour only, in the
    // pairs and in what the world draws; the Harness row the strap and the trim that follows it, until the trim has a
    // colour of its own. The rows: a choice, its custom colour and the lightness per tone under the skin's keys,
    // labelled by the tone's name, the trim's first choice "As the harness", the choice setting the look, the custom row
    // shown with Custom only; a skin registered later gets its tone rows at CatchUp, with its skin's other rows and not
    // after the page's next heading.
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
            Action<string> onReady = null; // the LoadedReady listener (taken off at the end)
            try
            {
                WriteSkin(root, "selftest.tones", "Tone Test", "[Colors]\nHarness = 808080 2850B4\n[Tone.cloth]\nname = Cloth\ncolours = C41E38\n[Tone.trim]\nname = Trim\nfollows = Harness\ncolours = 2850B4\n");
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
                // the trim follows Harness and is listed under it too: its colour moved to the tone, the strap's stayed
                bool moved = def.ColorRegions != null && def.ColorRegions.TryGetValue("Harness", out HashSet<string> harness) && harness.Contains("808080") && !harness.Contains("2850B4")
                    && SkinTones.ColoursOf(def, "trim").Contains("2850B4") && def.Tones[1].Follows == "Harness" && info != null && info.Tones[1].Follows == "Harness";
                SelfTest.Check(moved, "a tone that follows Harness, its colour listed under [Colors] Harness too (for an API without tones), has it: the loader moves it to the tone, the strap's stays the region's");

                // drawn: a test donkey wearing it (its own colours only, as the companion's donkey: Instance.OwnLook); when
                // its sheet is whole (LoadedReady) its drawn colours are there for the lightness, though it is worn a frame later
                bool readyHeard = false, coloursAtReady = false;
                onReady = id =>
                {
                    if (!string.Equals(id, SkinId, StringComparison.OrdinalIgnoreCase)) return;
                    readyHeard = true;
                    coloursAtReady = Looks.LoadedOwnColours(id) != null;
                };
                Looks.LoadedReady += onReady;
                Vector3 near = SelfTest.Spot(SelfTest.home, 4f) ?? SelfTest.home;
                a = SelfTest.Spawn(near, true); // the self-test's own owner (a made-up mod's donkey is warned of as a typo); the look is set as the tones owner
                if (a != null) a.I.OwnLook = true;
                a?.Tuning.Set(owner, "Skin", SkinId);
                yield return SelfTest.Until(() => a != null && a.IsReady && a.I.LooksRig.Wearing == SkinId && !a.I.LooksRig.Building, 10f);
                if (!SelfTest.waited)
                {
                    SelfTest.Fail("the tone skin was never worn");
                    yield break;
                }
                SelfTest.Check(!readyHeard || coloursAtReady, readyHeard ? $"at LoadedReady the skin's colours are there for the lightness (the companion's re-apply then anchors to them: {coloursAtReady})" : "the skin's sheet was built before the test listened (LoadedReady not heard): its colours' timing unchecked");
                yield return SelfTest.Frames(3);
                var d = a.I.ColorsRig.TestMountGroup();
                // the path is the player's setting up to the limit of a skin over the donkey (PaletteMaxColoursOver; SpriteMethod:
                // Palette, or Lut on a setup that needs it)
                SelfTest.Check(!Changed(d.ChangedSources, Cloth) && !Changed(d.ChangedSources, Trim) && !Changed(d.ChangedSources, Strap) && d.Colours <= Colors.PaletteMaxColoursOver && d.Method == Colors.SpriteMethod.Value
                    && !a.I.ColorsRig.TestDstOf(Cloth).HasValue && a.I.ColorsRig.TestDstOf(Trim).HasValue,
                    $"as drawn until set: the cloth, the trim and the strap unchanged, {d.Colours} colours on the player's path ({d.Method}); the cloth, set by nobody, has no pair, the trim (it follows Harness) has its region's");
                int pairsUnset = d.Pairs;

                // the cloth Gold through the look, as the companion applies it
                MountCompanion me = Mounts.Companion(owner);
                MountLook look = me.LookOf(SkinId);
                look.Tone("cloth").Color = ToneColor.Gold;
                Dictionary<string, Colors.RegionTweak> values = SkinTones.ValuesOf(look, SkinId);
                SelfTest.Check(values != null && values.Count == 1 && values.ContainsKey("cloth") && values["cloth"].Colorize == "D8A838",
                    $"the look's values: the cloth's Gold, nothing for the trim ({(values == null ? "none" : string.Join(", ", new List<string>(values.Keys)))})");
                SkinTones.Apply(a.I, values, owner, SkinId);
                yield return SelfTest.Frames(3);
                d = a.I.ColorsRig.TestMountGroup();
                SelfTest.Check(Changed(d.ChangedSources, Cloth) && !Changed(d.ChangedSources, Trim) && !Changed(d.ChangedSources, Strap) && d.Pairs == pairsUnset + 1,
                    $"the cloth set to Gold changes the cloth's colour and neither the trim's nor the Harness strap's ({d.Changed} of {d.Pairs} pairs changed; {pairsUnset} pairs before it was set)");

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

                // the Harness row's colour reaches the strap and the trim that follows it (its first choice), not the cloth's own
                a.Tuning.Set(owner, "HarnessColorize", "00FF00");
                a.Tuning.Set(owner, "HarnessColorizeAmount", 1f);
                yield return SelfTest.Frames(3);
                d = a.I.ColorsRig.TestMountGroup();
                Color32? trimFollowing = a.I.ColorsRig.TestDstOf(Trim), clothGold = a.I.ColorsRig.TestDstOf(Cloth);
                SelfTest.Check(Changed(d.ChangedSources, Strap) && Changed(d.ChangedSources, Trim), "a harness colour changes the strap named Harness and the trim that follows it");
                // a colour of its own cuts it loose: another harness colour doesn't reach it any more, nor the cloth
                look.Tone("trim").Color = ToneColor.Gold;
                SkinTones.Apply(a.I, SkinTones.ValuesOf(look, SkinId), owner, SkinId);
                yield return SelfTest.Frames(3);
                Color32? trimOwn = a.I.ColorsRig.TestDstOf(Trim);
                a.Tuning.Set(owner, "HarnessColorize", "FF00FF");
                yield return SelfTest.Frames(3);
                Color32? trimOwnAfter = a.I.ColorsRig.TestDstOf(Trim), clothAfter = a.I.ColorsRig.TestDstOf(Cloth);
                SelfTest.Check(SameRgb(trimOwn, trimOwnAfter) && !SameRgb(trimOwn, trimFollowing) && SameRgb(clothGold, clothAfter),
                    $"given a colour of its own the trim leaves the harness: another harness colour reaches neither it nor the cloth ({Hex(trimFollowing)} following, {Hex(trimOwn)} its own, {Hex(trimOwnAfter)} after the harness changed)");
                // back on its first choice it follows the harness again (now FF00FF)
                look.Tone("trim").Color = ToneColor.Game;
                SkinTones.Apply(a.I, SkinTones.ValuesOf(look, SkinId), owner, SkinId);
                yield return SelfTest.Frames(3);
                Color32? trimBack = a.I.ColorsRig.TestDstOf(Trim);
                SelfTest.Check(trimBack.HasValue && !SameRgb(trimBack, trimOwn) && !SameRgb(trimBack, trimFollowing) && !SameRgb(trimBack, Trim),
                    $"back on its first choice (As the harness) it follows the harness's new colour ({Hex(trimBack)})");
                look.Tone("cloth").Color = ToneColor.Game;
                SkinTones.Apply(a.I, SkinTones.ValuesOf(look, SkinId), owner, SkinId);
                yield return SelfTest.Frames(3);
                d = a.I.ColorsRig.TestMountGroup();
                SelfTest.Check(!Changed(d.ChangedSources, Cloth), "Game default puts the cloth back as drawn");
                // the tones go with their owner's overrides: another owner's clearing leaves them, the owner's own takes them
                look.Tone("cloth").Color = ToneColor.Gold;
                SkinTones.Apply(a.I, SkinTones.ValuesOf(look, SkinId), owner, SkinId);
                // another owner neither replaces nor wipes them (as an override: the owner that set a value keeps it)
                SkinTones.Apply(a.I, null, owner + ".someone-else", SkinId);
                bool notWiped = a.I.Tones != null && a.I.TonesOwner == owner;
                // made for another skin they colour nothing on this one (a skin changing under them, a sheet building)
                SkinTones.Apply(a.I, SkinTones.ValuesOf(look, SkinId), owner, "donkey/selftest.another-skin");
                yield return SelfTest.Frames(3);
                d = a.I.ColorsRig.TestMountGroup();
                bool otherSkinLeft = !Changed(d.ChangedSources, Cloth);
                SkinTones.Apply(a.I, SkinTones.ValuesOf(look, SkinId), owner, SkinId);
                yield return SelfTest.Frames(3);
                d = a.I.ColorsRig.TestMountGroup();
                SelfTest.Check(notWiped && otherSkinLeft && Changed(d.ChangedSources, Cloth), $"another owner's tones (none) leave the owner's, and tones made for another skin colour nothing on this one; for this skin they do (wiped: {!notWiped}, the other skin's coloured the cloth: {!otherSkinLeft})");
                Mounts.ClearTuning(owner + ".someone-else");
                bool kept = a.I.Tones != null;
                Mounts.ClearTuning(owner);
                SelfTest.Check(kept && a.I.Tones == null && a.I.TonesOwner == null, "the tones stay through another mod's ClearTuning and go with their owner's, as its overrides do");
                look.Tone("cloth").Color = ToneColor.Game;

                // the rows
                var cfg = new ConfigFile(cfgPath, false) { SaveOnConfigSet = false };
                var page = new MountSettingsPage(owner, cfg, "Tone test", "test", "", null);
                page.MountRows("Mount", me);
                // the page's picture named as the skin's tone is: a pack's tone name never makes the page refuse its rows
                page.Preview("Colours", me, "Cloth");
                string refused = null;
                try
                {
                    page.PerSkin((skinId, mountId, sfx, shown) =>
                    {
                        if (mountId.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) page.ToneRows("Colours", skinId, sfx, me);
                    });
                }
                catch (ArgumentException e)
                {
                    refused = e.Message;
                }
                SelfTest.Check(refused == null, $"a tone named like the page's picture row (Cloth) gets its rows ({refused ?? "built"})");
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
                SelfTest.Check(label.Contains("Cloth") && !label.Contains("(tone") && customLabel.Contains("Cloth") && customLabel != label && lightLabel.Contains("Cloth") && gold.Contains("Gold") && cloth.Label.Contains("(tone cloth)"),
                    $"labelled by the tone's name, the choices the API's words ({label} | {customLabel} | {lightLabel} | {gold}); the English label for the logs carries its id ({cloth.Label})");
                // a tone that follows a region: its first choice names it (As the harness), the rest are the same presets
                string trimFirst = trim != null ? Texts.Choice(trim, 0) : null, clothFirst = Texts.Choice(cloth, 0);
                SelfTest.Check(trimFirst == UiWords.ToneFollowHarness.Text && clothFirst == UiWords.ToneGame.Text && trim.ChoiceLabels[0] == UiWords.ToneFollowHarness.English,
                    $"the trim's first choice is the region it follows ({trimFirst}), the cloth's Game default ({clothFirst})");
                var entry = (ConfigEntry<ToneColor>)cloth.Entry;
                bool hidden = custom.Enabled != null && !custom.Enabled();
                entry.Value = ToneColor.Custom;
                bool showsWithCustom = custom.Enabled == null || custom.Enabled();
                entry.Value = ToneColor.Purple;
                SelfTest.Check(hidden && showsWithCustom && look.Tone("cloth").Color == ToneColor.Purple, $"the custom colour shows with Custom only, and the choice sets the look ({look.Tone("cloth").Color})");
                // greyed once the loaded skin draws none of a tone's colours (SkinTones.Draws), live while that isn't known
                if (Looks.LoadedOwnColours(SkinId) != null)
                    SelfTest.Check(SkinTones.Draws(SkinId, "cloth") && !SkinTones.Draws(SkinId, "nosuch") && cloth.Enabled != null && cloth.Enabled() && SkinTones.Draws("donkey/selftest.not-loaded", "cloth"),
                        "a tone's rows are live while the loaded skin draws its colours, greyed for a tone it draws none of, live for a skin not loaded");
                else SelfTest.Warn("the temp skin isn't loaded here: the tone rows' greying went unchecked");
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
                if (onReady != null) Looks.LoadedReady -= onReady;
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                if (registered) Looks.Unregister(SkinId, owner);
                if (lateRegistered) Looks.Unregister(LateId, owner + ".late");
                try { if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true); } catch { }
                try { if (System.IO.Directory.Exists(lateRoot)) System.IO.Directory.Delete(lateRoot, true); } catch { }
                try { System.IO.File.Delete(cfgPath); } catch { }
            }
        }

        // skin.cfg's tone sections read as the loader reads them: a tone it can't take (a colour twice, a bad id, a bad
        // follows) left out and said in the report, the skin loaded; a palette skin's tones ignored and a tone without
        // colours of its own colouring nothing, each said in the report; a variant's merge: a tone of its default's id
        // without a name keeps the default's (its translation too), with one renames it
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
            Looks.SkinDef Read(string body) => Looks.FromCfg("[Skin]\nformat = 1\nname = Bad\nlayer = over\n" + body, "selftest.bad", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
            bool LeftOut(Looks.SkinDef s, string tone) => (s.Tones == null || !s.Tones.Exists(t => t.Id == tone)) && s.Report.Contains("[Tone." + tone + "]") && s.Report.Contains("left out");
            Looks.SkinDef twice = Read("[Colors]\nHarness = C41E38\n[Tone.cloth]\nname = Cloth\ncolours = C41E38\n");
            Looks.SkinDef twoTones = Read("[Tone.a]\ncolours = C41E38\n[Tone.b]\ncolours = C41E38\n");
            Looks.SkinDef badId = Read("[Tone.Red_Cloth]\ncolours = C41E38\n");
            bool regionKept = twice.ColorRegions != null && twice.ColorRegions.TryGetValue("Harness", out HashSet<string> h) && h.Contains("C41E38");
            SelfTest.Check(LeftOut(twice, "cloth") && regionKept && LeftOut(twoTones, "b") && twoTones.Tones != null && twoTones.Tones.Exists(t => t.Id == "a") && LeftOut(badId, "red_cloth"),
                $"a tone with a colour [Colors] or another tone names, or an id with '_', is left out and said in the report, the skin loads and its regions stay as they were ({twice.Report} | {twoTones.Report} | {badId.Report})");
            string colorsTwice = Refused("[Colors]\nHarness = C41E38\nMuzzle = C41E38\n");
            SelfTest.Check(colorsTwice != null && colorsTwice.Contains("one region per colour"), $"a colour under two [Colors] regions still refuses the skin, as in 0.43 ({colorsTwice})");
            Looks.SkinDef palette = Looks.FromCfg("[Skin]\nformat = 1\nname = Palette\ncolours = palette\n[Tone.cloth]\ncolours = C41E38\n", "selftest.palette", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
            Looks.SkinDef empty = Looks.FromCfg("[Skin]\nformat = 1\nname = Empty\nlayer = over\n[Tone.cloth]\nname = Cloth\n", "selftest.empty", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
            bool emptyKept = empty.Tones != null && empty.Tones.Count == 1 && empty.Tones[0].NameGiven && SkinTones.ColoursOf(empty, "cloth").Count == 0;
            SelfTest.Check(palette.Tones == null && palette.ColorRegions == null && palette.Report.Contains("[Tone.*]") && emptyKept && empty.Report.Contains("names no colours"),
                "a palette skin's tones are ignored; a tone without colours of its own is kept for its name only (no colours, so no row), each said in the report");
            // a default's cloth follows Harness; a variant adds a shade to it without a name and says follows = Muzzle, renames the
            // trim, adds a plume
            var fromDefault = new List<SkinTones.ToneDef> { new SkinTones.ToneDef { Id = "cloth", Name = "Cloth", NameGiven = true, Follows = "Harness", OwnFollows = "Harness" }, new SkinTones.ToneDef { Id = "trim", Name = "Trim", NameGiven = true } };
            var variantOwn = new List<SkinTones.ToneDef> { new SkinTones.ToneDef { Id = "cloth", Name = "cloth", Follows = "Muzzle", OwnFollows = "Muzzle" }, new SkinTones.ToneDef { Id = "trim", Name = "Braid", NameGiven = true }, new SkinTones.ToneDef { Id = "plume", Name = "Plume", NameGiven = true } };
            var noFollow = new List<SkinTones.ToneDef> { new SkinTones.ToneDef { Id = "cloth", Name = "cloth" } };
            List<SkinTones.ToneDef> merged = SkinTones.Merge(fromDefault, variantOwn), plainShade = SkinTones.Merge(fromDefault, noFollow), alone = SkinTones.Merge(null, noFollow);
            bool names = merged.Count == 3 && merged[0].Name == "Cloth" && merged[0].NameFromParent && merged[1].Name == "Braid" && !merged[1].NameFromParent && merged[2].Id == "plume";
            bool follows = merged[0].Follows == "Muzzle" && plainShade[0].Follows == "Harness" && alone[0].Follows == null;
            bool untouched = variantOwn[0].Follows == "Muzzle" && noFollow[0].Follows == null && fromDefault[0].Follows == "Harness" && merged[0] != variantOwn[0] && merged[0] != fromDefault[0];
            SelfTest.Check(names && follows && untouched,
                $"a variant's tones: the default's in their order, one re-declared without a name keeps the default's name (its translation), one with a name renames it, its own new ones after ({string.Join(", ", merged.ConvertAll(t => t.Name))}); what it follows its own word, else the default's ({merged[0].Follows}, {plainShade[0].Follows}; alone {alone[0].Follows ?? "none"}); new records, the declared ones untouched ({untouched})");
            // a code tone's follows comes with its factory, read only after
            var codeFollow = MountSkinTone.Following("Harness", "Cloth", "Cloth", new[] { "C41E38" });
            var mine = new List<string> { "2850B4" };
            var trimTone = new MountSkinTone("trim", "Trim", mine);
            mine.Add("C41E38");
            string swapped = null;
            try { MountSkinTone.Following("cloth", "Harness", "Cloth", new[] { "C41E38" }); }
            catch (ArgumentException e) { swapped = e.Message; }
            var lowerRegion = MountSkinTone.Following("harness", "cloth", "Cloth", new[] { "C41E38" });
            SelfTest.Check(codeFollow.Id == "cloth" && codeFollow.Follows == "Harness" && trimTone.Follows == null && trimTone.Colours.Count == 1 && swapped != null && lowerRegion.Follows == "Harness",
                $"a code tone's follows comes with MountSkinTone.Following, a region in any case, one that is none throws at the call ({swapped ?? "no throw"}); its colours are a copy (the mod's list changed after leaves the tone as it was)");
            // a palette skin's tones aren't read, so a colour they share with its (ignored) [Colors] doesn't refuse it
            string paletteShared = Refused("colours = palette\n[Colors]\nHarness = C41E38\n[Tone.cloth]\ncolours = C41E38\n");
            // a code skin's tone without a name has none of its own (a code variant keeps its default's), one with a name
            // has it; the skin's listed tones say the same (one fed back into a MountSkinDef keeps its meaning)
            Looks.SkinDef code = new MountSkinDef { Id = "selftest.code-tones", Tones = new List<MountSkinTone> { new MountSkinTone("cloth", null, new[] { "C41E38" }), new MountSkinTone("trim", "Trim", new[] { "2850B4" }) } }.ToDef(SelfTest.Owner);
            IReadOnlyList<MountSkinTone> listed = SkinTones.InfosOf(code);
            SelfTest.Check(paletteShared == null && code.Tones != null && code.Tones.Count == 2 && !code.Tones[0].NameGiven && code.Tones[1].NameGiven
                && listed.Count == 2 && !listed[0].NameGiven && listed[0].Name == "cloth" && listed[1].NameGiven && listed[1].Name == "Trim",
                $"a palette skin isn't refused over its ignored sections ({paletteShared ?? "read"}), and a code skin's tone without a name is unnamed, as a pack's is, in its listed tones too");
            // follows names a mount region (else the tone is left out); a tone's list of separators only names nothing, a
            // [Colors] line naming none still makes its region (as in 0.43)
            Looks.SkinDef badFollow = Read("[Tone.cloth]\nname = Cloth\nfollows = Mane\ncolours = C41E38\n");
            Looks.SkinDef seps = Looks.FromCfg("[Skin]\nformat = 1\nname = Seps\nlayer = over\n[Colors]\nHarness = ,\n[Tone.cap]\nname = Cap\ncolours = ,\n", "selftest.seps", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
            string mixedCase = new MountSkinTone("Cloth", "Cloth", new[] { "C41E38" }).Id;
            bool emptyRegion = seps.ColorRegions != null && seps.ColorRegions.TryGetValue("Harness", out HashSet<string> none) && none.Count == 0 && !SkinTones.HasColours(seps, "cap");
            SelfTest.Check(LeftOut(badFollow, "cloth") && badFollow.Report.Contains("not a mount region") && emptyRegion && seps.Report.Contains("names no colours") && mixedCase == "cloth",
                $"a follows = that is no mount region leaves the tone out ({badFollow.Report}); \"colours = ,\" names nothing, \"Harness = ,\" makes an empty Harness as in 0.43 ({emptyRegion}); a code tone's id is kept in lower case, as a pack's ({mixedCase})");
            // a preset's brightness stops where the tone's lightest shade reaches white (no flat, clipped light shades)
            Looks.SkinDef shades = Looks.FromCfg("[Skin]\nformat = 1\nname = Shades\nlayer = over\n[Tone.x]\nname = X\ncolours = 202020 C0C0C0\n", "selftest.shades", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
            float white = SkinTones.Lightness("ECE8E0", shades, "x"), black = SkinTones.Lightness("2C2A30", shades, "x");
            SelfTest.Check(Mathf.Abs(white - 255f / 0xC0) < 0.01f && black < 1f && black > 0.1f,
                $"White on a tone of 202020 and C0C0C0 lifts it only until C0C0C0 is white ({white:0.###}, the uncapped rule would be {MountCompanion.LightnessAgainst("ECE8E0", (0x20 + 0xC0) / 2f / 255f):0.###}); Black darkens it ({black:0.###})");
            // every ToneColor its word in the enum's order (a choice row's words go by position) and, but Game and Custom, its colour
            var all = (ToneColor[])Enum.GetValues(typeof(ToneColor));
            Word[] words = SkinTones.Choices(null), following = SkinTones.Choices("Harness");
            bool table = words.Length == all.Length && following.Length == all.Length && words[0] == UiWords.ToneGame && following[0] == UiWords.ToneFollowHarness && following[1] == words[1] && words[all.Length - 1] == UiWords.ToneCustomChoice;
            foreach (ToneColor c in all) table &= (SkinTones.PresetHex(c) == null) == (c == ToneColor.Game || c == ToneColor.Custom);
            SelfTest.Check(table, "the presets' table (SkinTones.Preset): every ToneColor its word in the enum's order and, but Game and Custom, its colour; a following tone's first word is its region's");
        }

        private static bool Changed(List<Color32> sources, Color32 c) => sources.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b);

        private static bool SameRgb(Color32? a, Color32? b) => a.HasValue && b.HasValue && a.Value.r == b.Value.r && a.Value.g == b.Value.g && a.Value.b == b.Value.b;

        private static string Hex(Color32? c) => c.HasValue ? $"{c.Value.r:X2}{c.Value.g:X2}{c.Value.b:X2}" : "none";

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
