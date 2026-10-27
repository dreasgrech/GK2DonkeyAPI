using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Skin layering (docs/skin-layering-plan.md, LooksInherit.cs): every skin of a mount but the donkey is built on the
    // mount's own look. The scenarios use temp mount folders with pictures of their own (TempPng), so a pixel says where
    // it came from: the default's red, the variant's navy, its erase colour's hole.
    internal static partial class SelfTest
    {
        private static bool SameRgb(Color32 a, Color32 b) => a.a != 0 && a.r == b.r && a.g == b.g && a.b == b.b;

        private static readonly Color32 LayerRed = new Color32(170, 0, 0, 255), LayerDark = new Color32(16, 16, 16, 255), LayerNavy = new Color32(0, 0, 170, 255);

        // The default's picture of the layering scenarios: a red body (x 30-65, y 30-53 from the bottom) and a dark patch
        // above it (x 30-37, y 54-57; [Colors] Dark = 101010).
        private static byte[] LayerDefaultPicture() => TempPng((30, 30, 36, 24, LayerRed), (30, 54, 8, 4, LayerDark));

        // A variant built on its default: what it doesn't say is the default's (description, offset, colours,
        // a picture, tuning, a sound), what it says is its own (seat = none, a picture, a tuning key), it wears so, an
        // edit to the default reaches it, and its file keeps its own lines only (the tab's set and taken back too).
        private static IEnumerator SkinVariant()
        {
            const string owner = Owner + ".layer";
            const string mount = "selftest.layered", def = mount + "/default", blue = mount + "/blue";
            string root = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-layer");
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                WriteTempMountCfg(root, mount, "[Mount]\nid = " + mount + "\nname = Layered Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n");
                string dd = TempSkinDir(root, mount, Looks.DefaultSkinName);
                Directory.CreateDirectory(Path.Combine(dd, "sounds"));
                byte[] body = LayerDefaultPicture();
                File.WriteAllBytes(Path.Combine(dd, "bdy_static_left.png"), body);
                File.WriteAllBytes(Path.Combine(dd, "bdy_walk_left_01.png"), body);
                File.WriteAllBytes(Path.Combine(dd, "sounds", "voice_1.wav"), SoundBank.TestTone(0.3f, 440f));
                File.WriteAllText(Path.Combine(dd, "skin.cfg"), "[Skin]\nformat = 1\nname = Layered Default\ndescription = The default's own words.\nseat = 50\noffset = 2 1\n\n[Tuning]\nRiderOffsetPx = 3\n\n[Sounds]\nvoice = voice_1.wav\n\n[Colors]\nDark = 101010\n");
                string bd = TempSkinDir(root, mount, "blue");
                Directory.CreateDirectory(bd);
                File.WriteAllBytes(Path.Combine(bd, "bdy_walk_left_01.png"), TempPng((30, 30, 36, 24, LayerNavy)));
                File.WriteAllText(Path.Combine(bd, "skin.cfg"), "# keep: the variant's own file\n[Skin]\nformat = 1\nname = Layered Blue\nseat = none\n\n[Tuning]\nSeatAdjustPx = 4\n");
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            MountHandle a = null;
            try
            {
                int n = Looks.RegisterFolder(root, owner);
                Looks.SkinDef dDef = Looks.Find(def), bDef = Looks.Find(blue);
                Check(n == 2 && dDef != null && bDef != null && bDef.Parent == dDef && dDef.Parent == null, $"two skins: the variant is built on the default, the default on nothing ({n}; {bDef?.Parent?.Id ?? "none"})");
                MountSkinInfo bi = Mounts.Looks.Find(blue);
                Check(bi != null && bi.BuiltOn == def && !bi.OverDefault && !bi.Over && Mounts.Looks.Find(def)?.BuiltOn == null, $"MountSkinInfo: BuiltOn {bi?.BuiltOn ?? "null"}, OverDefault {bi?.OverDefault}; the default's BuiltOn null");
                if (dDef == null || bDef == null) yield break;
                Check(bDef.Description == "The default's own words." && bDef.OffsetRide == new Vector2(2f, 1f) && bDef.OffsetCart == new Vector2(2f, 1f) && bDef.CanvasW == 96 && bDef.CanvasH == 96,
                    $"what it doesn't say is the default's: the description, offset 2 1 riding and with the cart, the canvas ({bDef.Description}; {bDef.OffsetRide} {bDef.OffsetCart})");
                Check(bDef.SeatRow < 0 && !bDef.SeatFromPicture && dDef.SeatRow == 50, $"seat = none: no seat, not the default's 50 ({bDef.SeatRow})");
                Check(bDef.ColorRegions != null && bDef.ColorRegions.TryGetValue("Dark", out HashSet<string> darks) && darks.Contains("101010"), "the default's [Colors] reach it (Dark 101010)");
                (int own, int from) = Looks.PictureCounts(bDef);
                Check(own == 1 && from == 1, $"its pictures: 1 of its own, 1 from the default ({own}, {from})");
                Color32[] walk = Looks.TestPicture(blue, "bdy_walk_left_01", out int w1, out _), rest = Looks.TestPicture(blue, "bdy_static_left", out int w2, out _);
                Check(walk != null && SameRgb(walk[40 * w1 + 40], LayerNavy) && rest != null && SameRgb(rest[40 * w2 + 40], LayerRed), "a frame it draws is its own (navy), a frame it doesn't is the default's (red)");

                // worn
                a = Spawn(Spot(home, 4f) ?? home, true);
                a?.Tuning.Set(Owner, "Skin", blue);
                yield return Until(() => a != null && a.IsReady && a.Mount == mount && a.I.TestSkinWearing() == blue && !a.I.LooksRig.Building, 10f);
                Check(waited, "a donkey wears the variant");
                yield return Frames(3);
                Looks.Loaded l = Looks.TestLoaded(blue);
                Check(l != null && l.Ready && l.Layered && l.Parent == dDef && l.OwnPictures == 1 && l.Inherited == 1 && l.Composed == 0, $"its sheet: built on the default, 1 own frame, 1 from it ({l?.OwnPictures}, {l?.Inherited})");
                Check(a != null && a.Tuning.IsSet("RiderOffsetPx") && a.Tuning.Get<int>("RiderOffsetPx") == 3 && a.Tuning.IsSet("SeatAdjustPx") && a.Tuning.Get<int>("SeatAdjustPx") == 4,
                    "its tuning on the wearer: the default's RiderOffsetPx 3 and its own SeatAdjustPx 4");
                Check(a != null && Equals(Looks.LayeredValue(a.I, MountStyle.Ride, "RiderOffsetPx"), "3"), "LayeredValue finds the default's value under the variant's");
                if (a != null)
                {
                    a.I.SoundsRig.Invalidate();
                    SoundSpec v = a.I.SoundsRig.Resolve("voice");
                    Check(v != null && v.Layer == "default" && v.BankId != null, $"its voice is the default's [Sounds] line, layer \"default\" ({v?.Describe()})");
                }
                string voiceFrom = "-";
                foreach (var c in Sounds.Coverage(bDef, Catalog.Find(mount))) if (c.Event == "voice") voiceFrom = c.From;
                Check(voiceFrom == "default", $"the coverage report: voice from the default ({voiceFrom})");

                // an edit to the default reaches it (the Mounts tab's path: the live def changed, then Catalog.Touch)
                dDef.Description = "Changed words.";
                Catalog.Touch(Catalog.Find(mount));
                Check(bDef.Description == "Changed words.", $"the default's description changed in place: the variant's follows ({bDef.Description})");

                // its file: its own lines only
                string cfgPath = Path.Combine(TempSkinDir(root, mount, "blue"), "skin.cfg");
                string err = Looks.SaveCfg(bDef);
                string text = File.ReadAllText(cfgPath).Replace("\r\n", "\n");
                Check(err == null && text.Contains("seat = none") && text.Contains("# keep: the variant's own file") && text.Contains("SeatAdjustPx = 4") && !text.Contains("offset") && !text.Contains("RiderOffsetPx") && !text.Contains("description"),
                    "saved: its own lines (seat = none, its [Tuning]) and none of the default's (offset, description, tuning)" + (err != null ? " (" + err + ")" : ""));
                // the Mounts tab's path: a value set there is its own (Given), taken back (a right-click) the default's again
                bDef.OffsetRide = bDef.OffsetCart = new Vector2(5f, 0f);
                bDef.Given |= Looks.SkinGiven.Offset;
                Catalog.Touch(Catalog.Find(mount));
                err = Looks.SaveCfg(bDef);
                text = File.ReadAllText(cfgPath).Replace("\r\n", "\n");
                Check(err == null && bDef.OffsetRide == new Vector2(5f, 0f) && text.Contains("offset = 5 0") && !text.Contains("offset_cart"), "an offset set on the tab is its own and written (offset = 5 0, no offset_cart)" + (err != null ? " (" + err + ")" : ""));
                bDef.Given &= ~(Looks.SkinGiven.Offset | Looks.SkinGiven.OffsetCart);
                Catalog.Touch(Catalog.Find(mount));
                err = Looks.SaveCfg(bDef);
                text = File.ReadAllText(cfgPath).Replace("\r\n", "\n");
                Check(err == null && bDef.OffsetRide == new Vector2(2f, 1f) && !text.Contains("offset"), $"taken back: the default's offset again ({bDef.OffsetRide}), and out of the file");
                a?.Remove();
                a = null;
                yield return Frames(2);
            }
            finally
            {
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                try { Looks.Unregister(blue, owner); } catch { }
                try { Looks.Unregister(def, owner); } catch { }
                Catalog.Entry k = Catalog.Find(mount);
                if (k != null) { try { Looks.UnregisterMount(mount, k.Owner); } catch { } }
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        // A variant drawn over its default (layer = over on a skin of a mount other than the donkey): the default's
        // picture with the variant's erase colour cleared and its other pixels on top, built as one plain frame; the
        // default's colours are Coat (or what the default names), the variant's named ones their region, a colour only
        // in its own pixels and unnamed kept as drawn.
        private static IEnumerator SkinOverDefault()
        {
            const string owner = Owner + ".layerover";
            const string mount = "selftest.overbeast", def = mount + "/default", horns = mount + "/horns";
            Color32 erase = new Color32(255, 0, 255, 255), gold = new Color32(224, 192, 0, 255), green = new Color32(0, 192, 0, 255);
            string root = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-layerover");
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                WriteTempMountCfg(root, mount, "[Mount]\nid = " + mount + "\nname = Over Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n");
                string dd = TempSkinDir(root, mount, Looks.DefaultSkinName);
                Directory.CreateDirectory(dd);
                File.WriteAllBytes(Path.Combine(dd, "bdy_static_left.png"), LayerDefaultPicture());
                File.WriteAllBytes(Path.Combine(dd, "bdy_walk_left_01.png"), LayerDefaultPicture());
                File.WriteAllText(Path.Combine(dd, "skin.cfg"), "[Skin]\nformat = 1\nname = Over Beast\n\n[Colors]\nDark = 101010\n");
                string hd = TempSkinDir(root, mount, "horns");
                Directory.CreateDirectory(hd);
                // a hole in the red body (x 30-33, y 30-33), gold horns above it (x 40-43, y 56-59), a green fleck (x 60-61, y 56-57)
                File.WriteAllBytes(Path.Combine(hd, "bdy_static_left.png"), TempPng((30, 30, 4, 4, erase), (40, 56, 4, 4, gold), (60, 56, 2, 2, green)));
                File.WriteAllText(Path.Combine(hd, "skin.cfg"), "[Skin]\nformat = 1\nname = Horns\nlayer = over\n\n[Colors]\nHarness = E0C000\n");
            }
            catch (Exception e)
            {
                Warn("the temp pack could not be written: " + e.Message);
                yield break;
            }
            MountHandle a = null;
            try
            {
                int n = Looks.RegisterFolder(root, owner);
                Looks.SkinDef dDef = Looks.Find(def), hDef = Looks.Find(horns);
                Check(n == 2 && hDef != null && hDef.Parent == dDef && hDef.OverDefault && !hDef.Over && hDef.Erase.HasValue && SameRgb(hDef.Erase.Value, erase),
                    $"layer = over on a variant: drawn over its default (OverDefault), not over the game's donkey (Over false), the erase colour FF00FF by default ({n}; over default {hDef?.OverDefault}, over {hDef?.Over})");
                Check(Mounts.Looks.Find(horns)?.OverDefault == true && Mounts.Looks.Find(horns)?.Over == false, "MountSkinInfo: OverDefault true, Over false");
                Color32[] px = Looks.TestPicture(horns, "bdy_static_left", out int w, out _);
                Check(px != null && px[31 * w + 31].a == 0 && SameRgb(px[57 * w + 41], gold) && SameRgb(px[40 * w + 50], LayerRed) && SameRgb(px[57 * w + 61], green),
                    "composed: a hole where its erase colour is, its gold on top, the default's red where it draws nothing, its green fleck");
                Color32[] walk = Looks.TestPicture(horns, "bdy_walk_left_01", out int ww, out _);
                Check(walk != null && SameRgb(walk[40 * ww + 40], LayerRed), "a frame it doesn't draw: the default's alone");

                a = Spawn(Spot(home, 4f) ?? home, true);
                a?.Tuning.Set(Owner, "Skin", horns);
                yield return Until(() => a != null && a.IsReady && a.Mount == mount && a.I.TestSkinWearing() == horns && !a.I.LooksRig.Building, 10f);
                Check(waited, "a donkey wears it");
                yield return Frames(3);
                Looks.Loaded l = Looks.TestLoaded(horns);
                Check(l != null && l.Ready && l.Layered && l.Composed == 1 && l.Erased == 16 && l.Inherited == 1 && l.WholeKeys.Count == 0,
                    $"its sheet: 1 frame drawn over the default (16 pixels erased), 1 the default's, no second layer ({l?.Composed}, {l?.Erased}, {l?.Inherited})");
                bool InRegion(string region, Color32 c)
                {
                    if (l == null || !l.ColoursByRegion.TryGetValue(region, out List<Color32> cols)) return false;
                    foreach (Color32 x in cols) if (SameRgb(x, c)) return true;
                    return false;
                }
                bool inAny = false;
                if (l != null) foreach (var kv in l.ColoursByRegion) foreach (Color32 x in kv.Value) if (SameRgb(x, green)) inAny = true;
                Check(InRegion("Coat", LayerRed) && InRegion("Dark", LayerDark) && InRegion("Harness", gold) && !inAny,
                    "its colours: the default's red is Coat, its Dark the default's line, its gold Harness (its own line), its unnamed green kept as drawn");
                a?.Remove();
                a = null;
                yield return Frames(2);
            }
            finally
            {
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                try { Looks.Unregister(horns, owner); } catch { }
                try { Looks.Unregister(def, owner); } catch { }
                Catalog.Entry k = Catalog.Find(mount);
                if (k != null) { try { Looks.UnregisterMount(mount, k.Owner); } catch { } }
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        // Order and change: a variant registered before its default stands alone, then is built on it; the default read
        // again from its file (its wearer rebuilds on the new one), then withdrawn (another mod's variant stands alone,
        // said); a variant of another canvas takes the settings, not the pictures nor the seat; a code skin takes the
        // default's values by its sentinels and gives its own; the donkey's skins have no default and are as before.
        private static IEnumerator SkinLayerLifecycle()
        {
            const string owner = Owner + ".layerlife", joinerOwner = Owner + ".layerjoin";
            const string mount = "selftest.later", def = mount + "/default", early = mount + "/selftest.early", big = mount + "/big";
            string root1 = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-layerlife1");
            string root2 = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-layerlife2");
            string defCfg = null;
            try
            {
                foreach (string r in new[] { root1, root2 }) if (Directory.Exists(r)) Directory.Delete(r, true);
                WriteTempMountCfg(root1, mount, "[Mount]\nid = " + mount + "\nname = Later Beast\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\n");
                string dd = TempSkinDir(root1, mount, Looks.DefaultSkinName);
                Directory.CreateDirectory(dd);
                File.WriteAllBytes(Path.Combine(dd, "bdy_static_left.png"), LayerDefaultPicture());
                defCfg = Path.Combine(dd, "skin.cfg");
                File.WriteAllText(defCfg, "[Skin]\nformat = 1\nname = Later Default\ndescription = First words.\nseat = 50\n");
                // another canvas: its settings are the default's, its pictures and its seat row not
                AddTempSkin(root1, mount, "big", "[Skin]\nformat = 1\nname = Big\ncanvas = 128 128\n");
                // another mod's look for the mount, in a folder without mount.cfg (named <mod>.<name>)
                string ed = TempSkinDir(root2, mount, "selftest.early");
                Directory.CreateDirectory(ed);
                File.WriteAllBytes(Path.Combine(ed, "bdy_walk_left_01.png"), TempPng((30, 30, 36, 24, LayerNavy)));
                File.WriteAllText(Path.Combine(ed, "skin.cfg"), "[Skin]\nformat = 1\nname = Early\n");
            }
            catch (Exception e)
            {
                Warn("the temp packs could not be written: " + e.Message);
                yield break;
            }
            MountHandle a = null;
            IDisposable code1 = null, code2 = null;
            try
            {
                // (a) before its default
                int n2 = Looks.RegisterFolder(root2, joinerOwner);
                Looks.SkinDef eDef = Looks.Find(early);
                Check(n2 == 1 && eDef != null && eDef.Parent == null && eDef.Description == null && eDef.Unavailable == null, $"a variant before its default stands alone (its own picture: offered) ({n2}; parent {eDef?.Parent?.Id ?? "none"})");
                Info("the next \"has a 128x128 canvas\" warning is this test's own: a variant of another canvas is not built on its default's pictures");
                int n1 = Looks.RegisterFolder(root1, owner);
                Looks.SkinDef dDef = Looks.Find(def), bDef = Looks.Find(big);
                Check(n1 == 2 && eDef != null && eDef.Parent == dDef && dDef != null && eDef.Description == "First words." && eDef.SeatRow == 50,
                    $"its default came: built on it, its description and seat ({eDef?.Parent?.Id ?? "none"}; {eDef?.Description}; {eDef?.SeatRow})");
                Check(bDef != null && bDef.Parent == dDef && !Looks.InheritsPictures(bDef) && bDef.CanvasW == 128 && bDef.Description == "First words." && bDef.SeatRow < 0,
                    $"another canvas: the description is the default's, the pictures and the seat row not ({bDef?.CanvasW}; {bDef?.Description}; seat {bDef?.SeatRow})");
                if (eDef == null || dDef == null) yield break;

                // (b) worn, then the default read again from its file: the wearer rebuilds on the new one
                a = Spawn(Spot(home, 4f) ?? home, true);
                a?.Tuning.Set(Owner, "Skin", early);
                yield return Until(() => a != null && a.IsReady && a.I.TestSkinWearing() == early && !a.I.LooksRig.Building, 10f);
                Check(waited && Looks.TestLoaded(early)?.Parent == dDef && Looks.TestLoaded(early).Layered, "worn: its sheet built on the default");
                File.WriteAllText(defCfg, "[Skin]\nformat = 1\nname = Later Default\ndescription = Second words.\nseat = 50\n");
                string err = Looks.ReloadCfg(dDef);
                Looks.SkinDef dNew = Looks.Find(def);
                Check(err == null && dNew != null && dNew != dDef && eDef.Parent == dNew && eDef.Description == "Second words.", $"the default read again: the variant is built on the new one and takes its description ({eDef.Description})" + (err != null ? " (" + err + ")" : ""));
                yield return Until(() => Looks.TestLoaded(early)?.Parent == dNew && Looks.TestLoaded(early).Ready && a != null && !a.I.LooksRig.Building && a.I.TestSkinWearing() == early, 10f);
                Check(waited, "its wearer rebuilt its sheet on the new default");

                // (c) a code skin by its sentinels: nothing said, the default's; said, its own ("none" too)
                try
                {
                    code1 = Mounts.Looks.Register(owner, new MountSkinDef { Id = "codey", Name = "Codey", Mount = mount, Source = new NoPictures() });
                    code2 = Mounts.Looks.Register(owner, new MountSkinDef { Id = "codey2", Name = "Codey Two", Mount = mount, Source = new NoPictures(), SeatRow = 10, Over = true });
                }
                catch (Exception e) { Fail("a code skin was refused: " + e.Message); }
                Looks.SkinDef c1 = Looks.Find(mount + "/codey"), c2 = Looks.Find(mount + "/codey2");
                Check(c1 != null && c1.Parent == dNew && c1.SeatRow == 50 && c1.Description == "Second words.", $"a code skin with nothing said takes the default's seat and description ({c1?.SeatRow}, {c1?.Description})");
                Check(c2 != null && c2.SeatRow == 10 && !c2.Over && c2.Report.Contains("not drawn over its default yet"), $"one that says its seat keeps its own (seat 10: not the default's); Over on it is refused and reported ({c2?.SeatRow}, over {c2?.Over})");
                code1?.Dispose();
                code2?.Dispose();
                code1 = code2 = null;

                // (d) the donkey's skins: no default, nothing rewritten
                foreach (string id in new[] { Sk("hellfire"), Sk("royal-parade") })
                {
                    Looks.SkinDef s = Looks.Find(id);
                    if (s == null) { Warn($"{id} is not registered (Saddle Up not loaded?)"); continue; }
                    Check(s.Parent == null && s.AloneValues == null && !s.OverDefault && !Looks.IsVariantSlot(s) && Mounts.Looks.Find(id)?.BuiltOn == null && Looks.TestLoaded(id)?.Layered != true, $"{id}: no default, nothing resolved, as before (over {s.Over})");
                }

                // (e) the default withdrawn: another mod's variant stands alone again, its wearer rebuilds
                Info("the next \"was withdrawn by\" warning is this test's own: another mod's variant is left standing alone");
                Looks.Unregister(big, owner);
                Check(Looks.Unregister(def, owner), "the default withdrawn by its owner");
                Check(eDef.Parent == null && eDef.Description == null && eDef.SeatRow < 0 && eDef.Unavailable == null, $"the variant stands alone: its own values only, still offered ({eDef.Description ?? "no description"}, seat {eDef.SeatRow})");
                yield return Until(() => Looks.TestLoaded(early)?.Parent == null && Looks.TestLoaded(early).Ready && a != null && !a.I.LooksRig.Building, 10f);
                Check(waited && Looks.TestLoaded(early)?.Layered == false, "its wearer rebuilt its sheet alone");
                a?.Remove();
                a = null;
                yield return Frames(2);
            }
            finally
            {
                try { if (a != null && a.Exists) a.Remove(); } catch { }
                code1?.Dispose();
                code2?.Dispose();
                try { Looks.Unregister(early, joinerOwner); } catch { } // the variants before the default (no "withdrawn" warning)
                try { Looks.Unregister(big, owner); } catch { }
                try { Looks.Unregister(def, owner); } catch { }
                Catalog.Entry k = Catalog.Find(mount);
                if (k != null) { try { Looks.UnregisterMount(mount, k.Owner); } catch { } }
                foreach (string r in new[] { root1, root2 }) { try { if (Directory.Exists(r)) Directory.Delete(r, true); } catch { } }
            }
        }
    }
}
