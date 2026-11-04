using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // A look's names for its parts (docs/second-tone.md 8; the user, 2026-11-04: every row of the colours window a part of
    // the look by its name, never "Second tone"). Read: [Region.<region>] name =, a bad region or key said in the report.
    // Named: the donkey's own words on its look; a skin over the donkey takes them but for a region it names itself; a
    // skin in its own colours names its own or none. The rows: RegionRows labels a page's region rows by the look's name
    // (its custom colour and lightness rows in the API's templates) and leaves them out where the look has no colour of
    // the region; ToneRows makes a Dark or Eye row where the look names one, coloured as a part (RegionValues). A part that
    // followed a region (follows =): the player's saved value of the region becomes its own once, while the file has
    // none of its rows (three pages on one file: the released player's, the update's first load, the next load).
    internal static class RegionTests
    {
        private const string OverId = "donkey/selftest.regions-over", OwnId = "donkey/selftest.regions-own", RowlessId = "donkey/selftest.regions-rowless";
        private static readonly Color32 Cloth = new Color32(0xC4, 0x1E, 0x38, 255), Plume = new Color32(0x28, 0x50, 0xB4, 255);
        private static readonly Color32 Shell = new Color32(0x50, 0x8C, 0x3C, 255), Spots = new Color32(0x20, 0x18, 0x40, 255), Body = new Color32(0xB4, 0x96, 0x64, 255);

        internal static IEnumerator Regions()
        {
            const string owner = SelfTest.Owner + ".regions";
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-regions");
            string cfgPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-regions.cfg");
            int registered = 0;
            try
            {
                // read: the names, a bad region, a bad key
                Looks.SkinDef parsed = Looks.FromCfg("[Skin]\nformat = 1\nname = Named\nlayer = over\n[Region.Muzzle]\nname = Belly\n[Region.Mane]\nname = X\n[Region.dark]\nhue = 3\nname = Spots\n", "selftest.regions-parse", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
                string muzzleName = null, darkName = null;
                bool namesRead = parsed.RegionNames != null && parsed.RegionNames.TryGetValue("Muzzle", out muzzleName) && parsed.RegionNames.TryGetValue("Dark", out darkName);
                SelfTest.Check(namesRead && muzzleName == "Belly" && darkName == "Spots" && parsed.Report.Contains("no such mount region") && parsed.Report.Contains("not a region's key"),
                    $"[Region.<region>] name = read, any case of the region ({muzzleName}, {darkName}); a region that is none and a key other than name said in the report ({parsed.Report})");

                // the donkey's own words on its look
                SelfTest.Check(SkinRegions.NameTerm(MountLooks.DonkeyId, "Coat") == UiWords.RegionCoat && SkinRegions.NameTerm(MountLooks.DonkeyId, "Muzzle") == UiWords.RegionDonkeyMuzzle
                    && SkinRegions.NameTerm(MountLooks.DonkeyId, "Dark") == UiWords.RegionDonkeyDark && SkinRegions.NameTerm(MountLooks.DonkeyId, "Eye") == UiWords.RegionEyes && SkinRegions.NameTerm(MountLooks.DonkeyId, "Harness") == UiWords.RegionHarness
                    && SkinRegions.NameTerm(MountLooks.DonkeyId, "Outline") == UiWords.RegionOutline,
                    "the donkey's own look names its six parts in the API's words (Coat, Legs mane and ears, Muzzle, Eyes, Harness, Outline)");

                // [Region.<region>] colours = (docs/mount-colours.md 7.15): the skin's own colours in the region, read as a
                // [Colors] line (any case, commas); Coat refused, a colour under two regions refused
                string Refused(string body)
                {
                    try
                    {
                        Looks.FromCfg("[Skin]\nformat = 1\nname = Lined\nlayer = over\n" + body, "selftest.regions-colours", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
                        return null;
                    }
                    catch (FormatException e)
                    {
                        return e.Message;
                    }
                }
                Looks.SkinDef lined = Looks.FromCfg("[Skin]\nformat = 1\nname = Lined\nlayer = over\n[Region.Outline]\ncolours = 281B26, 1e0e0c\n", "selftest.regions-colours", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
                bool linesRead = lined.ColorRegions != null && lined.ColorRegions.TryGetValue("Outline", out HashSet<string> lineColours) && lineColours.Contains("281B26") && lineColours.Contains("1E0E0C");
                string coatRefused = Refused("[Region.Coat]\ncolours = 281B26\n"), twiceRefused = Refused("[Colors]\nDark = 281B26\n[Region.Outline]\ncolours = 281B26\n");
                SelfTest.Check(linesRead && coatRefused != null && twiceRefused != null,
                    $"[Region.<region>] colours = read as a [Colors] line (281B26 and 1e0e0c under Outline: {linesRead}); Coat refused ({coatRefused ?? "NOT refused"}); a colour under two regions refused ({twiceRefused ?? "NOT refused"})");

                // two temp skins: one over the donkey naming its Muzzle, with two parts that were the regions' (follows =);
                // one in its own colours naming its Muzzle and Dark, with no harness
                if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
                if (System.IO.File.Exists(cfgPath)) System.IO.File.Delete(cfgPath);
                WriteSkin(root, "selftest.regions-over", "[Skin]\nformat = 1\nname = Regions Over\nlayer = over\n[Region.Muzzle]\nname = Belly\n[Tone.cloth]\nname = Cloth\nfollows = Harness\ncolours = C41E38\n[Tone.plume]\nname = Plume\nfollows = Muzzle\ncolours = 2850B4\n", Cloth, Plume);
                WriteSkin(root, "selftest.regions-own", "[Skin]\nformat = 1\nname = Regions Own\ncolours = own\n[Colors]\nMuzzle = 508C3C\nDark = 201840\n[Region.Muzzle]\nname = Shell\n[Region.Dark]\nname = Spots\n", Shell, Spots, Body);
                // and one over the donkey that takes its Harness and Eye rows away (row = none; docs/mount-colours.md 7.13)
                WriteSkin(root, "selftest.regions-rowless", "[Skin]\nformat = 1\nname = Regions Rowless\nlayer = over\n[Region.Harness]\nrow = none\n[Region.Eye]\nrow = none\n", Plume);
                registered = Looks.RegisterFolder(root, owner);
                if (registered != 3)
                {
                    SelfTest.Fail($"{registered} of the 3 temp skins registered");
                    yield break;
                }
                // row = none read: a value other than none said, a name with it never declared (no translation for it), a
                // variant without a name of its own takes its default's away, one naming the region brings its row back
                Looks.SkinDef rowParse = Looks.FromCfg("[Skin]\nformat = 1\nname = Rowless\nlayer = over\n[Region.Eye]\nrow = maybe\n[Region.Dark]\nrow = none\nname = Spots\n", "selftest.regions-rowparse", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
                bool badSaid = rowParse.Report != null && rowParse.Report.Contains("none is its one value");
                bool declaredDark = false;
                foreach (KeyValuePair<string, string> kv in SkinRegions.Declared(rowParse)) if (kv.Key == "Dark") declaredDark = true;
                Looks.SkinDef plainVariant = Looks.FromCfg("[Skin]\nformat = 1\nname = V\n", "selftest.regions-rowvariant", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
                Looks.SkinDef namingVariant = Looks.FromCfg("[Skin]\nformat = 1\nname = W\n[Region.Dark]\nname = Mane\n", "selftest.regions-rowback", SelfTest.Owner, "test", f => null, new[] { "bdy.png" }, Catalog.DonkeyId);
                plainVariant.Parent = rowParse;
                namingVariant.Parent = rowParse;
                bool inherited = !SkinRegions.HasRow(plainVariant, "Dark") && SkinRegions.HasRow(namingVariant, "Dark");
                SelfTest.Check(badSaid && !SkinRegions.HasRow(rowParse, "Dark") && SkinRegions.HasRow(rowParse, "Eye") && !declaredDark && inherited,
                    $"[Region.<region>] row = none read: row = maybe said ({badSaid}), the Dark row gone and its name not declared ({!SkinRegions.HasRow(rowParse, "Dark")}, declared {declaredDark}), a variant takes its default's away and one naming it brings it back ({inherited})");
                Term rowlessEye = SkinRegions.NameTerm(RowlessId, "Eye"), rowlessCoat = SkinRegions.NameTerm(RowlessId, "Coat");
                SelfTest.Check(!SkinRegions.HasRow(RowlessId, "Harness") && !SkinRegions.HasRow(RowlessId, "Eye") && SkinRegions.HasRow(RowlessId, "Coat") && rowlessEye == null && rowlessCoat == UiWords.RegionCoat,
                    $"the skin with row = none for Harness and Eye: no row for them, the others the donkey's ({(rowlessEye == null ? "no Eye name" : rowlessEye.English)}, Coat {rowlessCoat?.English})");
                Term overMuzzle = SkinRegions.NameTerm(OverId, "Muzzle"), overCoat = SkinRegions.NameTerm(OverId, "Coat"), ownMuzzle = SkinRegions.NameTerm(OwnId, "Muzzle"), ownCoat = SkinRegions.NameTerm(OwnId, "Coat");
                SelfTest.Check(overMuzzle != null && overMuzzle.English == "Belly" && overMuzzle.Key == SkinRegions.KeyOf(OverId, "Muzzle") && overCoat == UiWords.RegionCoat
                    && ownMuzzle != null && ownMuzzle.English == "Shell" && ownCoat == null,
                    $"a skin over the donkey takes the donkey's words but where it names its own ({overMuzzle?.English}, its Coat {overCoat?.English}); one in its own colours names its own or none ({ownMuzzle?.English}, its Coat {(ownCoat == null ? "none: the page's words" : ownCoat.English)})");

                // page A: a player's file from before the parts: the harness Custom (Wine at 120 %), the second tone Rose
                MountCompanion me = Mounts.Companion(owner);
                MountSettingsPage pageA = Page(cfgPath, owner, me, false, out ConfigFile cfgA);
                Entry<HarnessColor>(pageA, "Harness_" + OverId).Value = HarnessColor.Custom;
                Entry<PaletteColor>(pageA, "CustomHarness_" + OverId).Value = PaletteColor.Wine;
                Entry<int>(pageA, "CustomHarnessLightness_" + OverId).Value = 120;
                Entry<MuzzleColor>(pageA, "Muzzle_" + OverId).Value = MuzzleColor.Rose;
                cfgA.Save();

                // page B: the update's first load: the rows named, the Harness rows of the skin without a harness left out, its
                // Dark a row of its own; the two parts take the regions' values
                MountSettingsPage pageB = Page(cfgPath, owner, me, true, out ConfigFile cfgB);
                MountSettingsPage.Row Row(MountSettingsPage page, string key) => page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == key);
                MountSettingsPage.Row overMuzzleRow = Row(pageB, "Muzzle_" + OverId), overCustomRow = Row(pageB, "CustomMuzzle_" + OverId), ownMuzzleRow = Row(pageB, "Muzzle_" + OwnId);
                MountSettingsPage.Row ownHarnessRow = Row(pageB, "Harness_" + OwnId), ownCoatRow = Row(pageB, "Coat_" + OwnId), ownDarkRow = Row(pageB, "Dark_" + OwnId), ownEyeRow = Row(pageB, "Eye_" + OwnId);
                string overMuzzleLabel = Texts.Label(overMuzzleRow), overCustomLabel = Texts.Label(overCustomRow), ownMuzzleLabel = Texts.Label(ownMuzzleRow), ownCoatLabel = Texts.Label(ownCoatRow);
                SelfTest.Check(overMuzzleLabel == overMuzzle.Text && overCustomLabel == UiWords.ToneCustom.Format(overMuzzle) && ownMuzzleLabel == ownMuzzle.Text && ownCoatLabel == ownCoatRow.Label,
                    $"RegionRows labels a page's rows by the look's names ({overMuzzleLabel} | {overCustomLabel} | {ownMuzzleLabel}); a region the look names none of keeps the row's own words ({ownCoatLabel})");
                // PerSkin shows a skin's rows only while the Skin row has that skin: chosen, the region's own condition decides
                // (unchosen, every row of it is hidden whatever its regions: the first run of this check, 2026-11-04)
                ConfigEntry<string> skinRow = Entry<string>(pageB, "Skin_" + Catalog.DonkeyId);
                string wasSkin = skinRow?.Value;
                if (skinRow != null) skinRow.Value = OwnId;
                bool chosen = string.Equals(pageB.SkinChosen(Catalog.DonkeyId), OwnId, StringComparison.OrdinalIgnoreCase);
                bool harnessOut = ownHarnessRow != null && ownHarnessRow.Visible != null && !ownHarnessRow.Visible();
                bool muzzleIn = ownMuzzleRow != null && (ownMuzzleRow.Visible == null || ownMuzzleRow.Visible());
                // the skin without a Harness and an Eye row: chosen, its Harness rows left out and no Eye row made, its
                // Coat and Dark rows there
                if (skinRow != null) skinRow.Value = RowlessId;
                MountSettingsPage.Row rlHarness = Row(pageB, "Harness_" + RowlessId), rlCoat = Row(pageB, "Coat_" + RowlessId);
                bool rlHarnessOut = rlHarness != null && rlHarness.Visible != null && !rlHarness.Visible();
                bool rlCoatIn = rlCoat != null && (rlCoat.Visible == null || rlCoat.Visible());
                bool rlNoEye = Row(pageB, "Eye_" + RowlessId) == null, rlDark = Row(pageB, "Dark_" + RowlessId) != null;
                if (skinRow != null) skinRow.Value = wasSkin;
                SelfTest.Check(rlHarnessOut && rlCoatIn && rlNoEye && rlDark,
                    $"row = none on the page: with that skin chosen its Harness left out ({rlHarnessOut}), its Coat shown ({rlCoatIn}), no Eye row made ({rlNoEye}), its Dark row there ({rlDark})");
                SelfTest.Check(chosen && harnessOut && muzzleIn, $"a region the look has no colour of has no row: with the own-colour skin chosen ({chosen}) its Harness left out ({harnessOut}), its Muzzle shown ({muzzleIn}; SkinHasRegion Harness {MountSettingsPage.SkinHasRegion(OwnId, "Harness")}, Muzzle {MountSettingsPage.SkinHasRegion(OwnId, "Muzzle")})");
                SelfTest.Check(ownDarkRow != null && Texts.Label(ownDarkRow) == "Spots" && ownEyeRow == null && Row(pageB, "Dark_" + OverId) != null,
                    $"a Dark or Eye row where the look names its part ({(ownDarkRow != null ? Texts.Label(ownDarkRow) : "none")}; none for an Eye it doesn't name; the skin over the donkey its own from the donkey's words)");
                MountLook overLook = me.LookOf(OverId), ownLook = me.LookOf(OwnId);
                MountTone cloth = overLook.Tone("cloth"), plume = overLook.Tone("plume");
                var clothCustom = Entry<PaletteColor>(pageB, "CustomTone.cloth_" + OverId);
                var clothLight = Entry<int>(pageB, "CustomTone.clothLightness_" + OverId);
                SelfTest.Check(cloth.Color == ToneColor.Custom && clothCustom != null && clothCustom.Value == PaletteColor.Wine && clothLight != null && clothLight.Value == 120 && plume.Color == ToneColor.Pink,
                    $"the update's first load: a part that followed a region takes the player's saved value as its own (the cloth {cloth.Color} {clothCustom?.Value} at {clothLight?.Value} %, from the harness's Custom Wine at 120 %; the plume {plume.Color}, from the second tone's Rose)");
                // the dark part coloured as a part is: its region's colorize and lightness
                ownLook.Dark.Color = ToneColor.Red;
                Dictionary<string, object> dark = SkinTones.RegionValues(ownLook.Dark, "Dark", OwnId);
                SelfTest.Check(dark != null && (string)dark["DarkColorize"] == SkinTones.PresetHex(ToneColor.Red) && (float)dark["DarkColorizeAmount"] == 1f && (float)dark["DarkBrightness"] > 0f && SkinTones.RegionValues(overLook.Eye, "Eye", OverId) == null,
                    $"a Dark row's Red is the region's colorize as a part's ({(dark != null ? dark["DarkColorize"] + " at " + ((float)dark["DarkBrightness"]).ToString("0.00") : "none")}); Game is nothing (the eyes)");
                ownLook.Dark.Color = ToneColor.Game;
                // after the first load the parts are the player's own: a later harness colour doesn't reach them
                Entry<HarnessColor>(pageB, "Harness_" + OverId).Value = HarnessColor.Black;
                cfgB.Save();

                // the engine: a test donkey wearing it, the Harness keys set: the donkey's harness under the skin drawn as
                // it is (no value applied for a region without a row), the Coat keys set: its coat coloured
                MountHandle rl = SelfTest.Spawn(SelfTest.Spot(SelfTest.home, 4f) ?? SelfTest.home, true);
                if (rl != null)
                {
                    rl.Tuning.Set(SelfTest.Owner, "Skin", RowlessId);
                    yield return SelfTest.Until(() => rl.IsReady && rl.I.LooksRig.Wearing == RowlessId && !rl.I.LooksRig.Building, 20f);
                    rl.Tuning.Set(SelfTest.Owner, "HarnessColorize", "00FF00");
                    rl.Tuning.Set(SelfTest.Owner, "HarnessColorizeAmount", 1f);
                    rl.Tuning.Set(SelfTest.Owner, "CoatColorize", "00FF00");
                    rl.Tuning.Set(SelfTest.Owner, "CoatColorizeAmount", 1f);
                    yield return SelfTest.Frames(3);
                    Color32 harness = new Color32(0x62, 0x41, 0x1b, 255), coat = new Color32(0x73, 0x6c, 0x57, 255);
                    Color32? hd = rl.I.ColorsRig.TestDstOf(harness), cd = rl.I.ColorsRig.TestDstOf(coat);
                    bool harnessKept = hd == null || (hd.Value.r == harness.r && hd.Value.g == harness.g && hd.Value.b == harness.b);
                    bool coatChanged = cd != null && (cd.Value.r != coat.r || cd.Value.g != coat.g || cd.Value.b != coat.b);
                    SelfTest.Check(SelfTest.waited && harnessKept && coatChanged,
                        $"row = none in the engine: the Harness keys green, the donkey's harness 62411B drawn as it is ({(hd == null ? "no pair" : $"{hd.Value.r:X2}{hd.Value.g:X2}{hd.Value.b:X2}")}); the Coat keys green, its coat 736C57 coloured ({(cd == null ? "no pair" : $"{cd.Value.r:X2}{cd.Value.g:X2}{cd.Value.b:X2}")})");
                    rl.Remove();
                }
                else SelfTest.Fail("could not spawn a test donkey for row = none in the engine");

                // page C: the next load: the parts' rows are in the file, nothing is carried over again
                MountSettingsPage pageC = Page(cfgPath, owner, me, true, out _);
                var clothC = Entry<ToneColor>(pageC, "Tone.cloth_" + OverId);
                SelfTest.Check(clothC != null && clothC.Value == ToneColor.Custom && cloth.Color == ToneColor.Custom,
                    $"the next load carries nothing over again: the cloth stays the player's ({clothC?.Value}) though the harness is Black now");
            }
            finally
            {
                if (registered > 0)
                {
                    Looks.Unregister(OverId, owner);
                    Looks.Unregister(OwnId, owner);
                    Looks.Unregister(RowlessId, owner);
                }
                try { if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true); } catch { }
                try { if (System.IO.File.Exists(cfgPath)) System.IO.File.Delete(cfgPath); } catch { }
            }
        }

        // A page as a mod builds its colours (the regions' rows by its own keys; named with RegionRows, then ToneRows when
        // withParts), on the file at cfgPath, for the two temp skins.
        private static MountSettingsPage Page(string cfgPath, string owner, MountCompanion me, bool withParts, out ConfigFile cfg)
        {
            cfg = new ConfigFile(cfgPath, false) { SaveOnConfigSet = false };
            var page = new MountSettingsPage(owner, cfg, "Region test", "test", "", null);
            page.MountRows("Mount", me);
            page.PerSkin((skinId, mountId, sfx, shown) =>
            {
                if (!skinId.Equals(OverId, StringComparison.OrdinalIgnoreCase) && !skinId.Equals(OwnId, StringComparison.OrdinalIgnoreCase) && !skinId.Equals(RowlessId, StringComparison.OrdinalIgnoreCase)) return;
                MountLook look = me.LookOf(skinId);
                page.Choice("Colours", "Coat" + sfx, look.Coat, "Coat colour", "test", v => look.Coat = v, look: true);
                page.Color("Colours", "CustomCoat" + sfx, "Custom coat colour", "test", PaletteColor.Chestnut, v => look.CustomCoat = v);
                page.Choice("Colours", "Harness" + sfx, look.Harness, "Harness colour", "test", v => look.Harness = v, look: true);
                page.Color("Colours", "CustomHarness" + sfx, "Custom harness colour", "test", PaletteColor.Wine, v => look.CustomHarness = v);
                page.Choice("Colours", "Muzzle" + sfx, look.Muzzle, "Second tone", "test", v => look.Muzzle = v, look: true);
                page.Color("Colours", "CustomMuzzle" + sfx, "Custom second tone", "test", PaletteColor.Chestnut, v => look.CustomMuzzle = v);
                if (!withParts) return;
                page.RegionRows("Colours", skinId, "Coat", "Coat" + sfx, "CustomCoat" + sfx);
                page.RegionRows("Colours", skinId, "Harness", "Harness" + sfx, "CustomHarness" + sfx);
                page.RegionRows("Colours", skinId, "Muzzle", "Muzzle" + sfx, "CustomMuzzle" + sfx);
                page.ToneRows("Colours", skinId, sfx, me);
            });
            return page;
        }

        private static ConfigEntry<T> Entry<T>(MountSettingsPage page, string key) =>
            page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == key)?.Entry as ConfigEntry<T>;

        // A temp skin over or in its own colours, its colours drawn side by side in a band of the body.
        private static void WriteSkin(string root, string id, string cfg, params Color32[] colours)
        {
            string dir = SelfTest.TempSkinDir(root, Catalog.DonkeyId, id);
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skin.cfg"), cfg);
            var px = new Color32[96 * 96];
            for (int y = 40; y < 50; y++)
            {
                for (int x = 44; x < 44 + 6 * colours.Length; x++) px[y * 96 + x] = colours[(x - 44) / 6];
            }
            var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "bdy.png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }
}
