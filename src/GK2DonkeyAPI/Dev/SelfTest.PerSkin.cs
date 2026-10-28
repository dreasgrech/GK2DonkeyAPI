using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Looks per skin (docs/per-skin-looks.md, 0.41.0; the user, 2026-10-28): a page's PerSkin rows are one set per skin of
    // every mount, a mount's first skin on the mount's keys (the 0.12 keys), every other skin on "<key>_<skin id>"; the
    // chosen mount's chosen skin's set is the shown one; each skin's look is the companion's when worn; a 0.12 file whose
    // mount wore a skin that isn't the first moves the mount's values to that skin, once; a skin registered after the
    // page gets its rows at CatchUp; SkinHasRegion is the skin's own regions, the donkey's under an over skin.
    internal static partial class SelfTest
    {
        internal static IEnumerator LooksPerSkin()
        {
            const string owner = Owner + ".perskin";
            const string skinA = "donkey/selftest.perskin-a", skinB = "donkey/selftest.perskin-b";
            string rootA = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-perskin-a");
            string rootB = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-perskin-b");
            string cfgPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk2donkeyapi-selftest-perskin.cfg");
            bool registeredA = false, registeredB = false;
            try
            {
                WriteOverSkin(rootA, "selftest.perskin-a", "Per Skin A");
                // a 0.12 file: the donkey's colours per mount (its plain keys), written while the donkey wore skin A
                System.IO.File.WriteAllText(cfgPath, "[1. Mount]\r\nMount = donkey\r\nSkin_donkey = " + skinA + "\r\n\r\n[2. Colours]\r\nCoat = Black\r\nLantern = false\r\n");
            }
            catch (Exception e)
            {
                Warn("the temp skin or cfg could not be written: " + e.Message);
                yield break;
            }
            try
            {
                registeredA = Looks.RegisterFolder(rootA, owner + ".a") == 1;
                Check(registeredA && Mounts.Looks.Find(skinA) != null, "a temp over skin of the donkey is registered before the page");
                if (!registeredA) yield break;
                MountCompanion me = Mounts.Companion(owner);
                var cfg = new ConfigFile(cfgPath, false) { SaveOnConfigSet = false };
                var page = new MountSettingsPage(owner, cfg, "Per skin test", "test", "", null);
                int sets = PerSkinRows(page, me);
                ConfigEntry<T> Row<T>(string key) => page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == key)?.Entry as ConfigEntry<T>;
                MountSettingsPage.Row RowOf(string key) => page.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == key);
                int choices = Looks.SkinChoicesOf(MountCatalog.DonkeyId).Count;
                Check(sets >= 2 && sets == choices, $"PerSkin ran once per skin of the donkey ({sets} of {choices}): the own art and the temp skin at least");
                ConfigEntry<CoatColor> coatPlain = Row<CoatColor>("Coat"), coatA = Row<CoatColor>("Coat_" + skinA);
                ConfigEntry<bool> lanternPlain = Row<bool>("Lantern"), lanternA = Row<bool>("Lantern_" + skinA);
                Check(coatPlain != null && coatA != null && lanternPlain != null && lanternA != null, "the donkey's own art keeps the plain keys, the temp skin's carry its id (Coat_<skin>, Lantern_<skin>)");
                if (coatPlain == null || coatA == null || lanternPlain == null || lanternA == null) yield break;
                // the 0.12 carry-over: the mount's values moved to the skin it wore, the own art back at its defaults
                Check(page.SeededSkins == 1 && coatA.Value == CoatColor.Black && !lanternA.Value, $"the 0.12 file's Coat = Black and Lantern = false went to the skin the donkey wore ({coatA.Value}, lantern {lanternA.Value}; {page.SeededSkins} skin seeded)");
                Check(coatPlain.Value == CoatColor.Game && lanternPlain.Value, $"and the donkey's own art is at its defaults again ({coatPlain.Value}, lantern {lanternPlain.Value}): a copy would have left it in the skin's colours");
                Check(me.LookOf(skinA).Coat == CoatColor.Black && !me.LookOf(skinA).Lantern && me.LookOf(MountLooks.DonkeyId).Coat == CoatColor.Game, "the companion's looks follow: the skin's Black without a lantern, the own art's Game default");
                // the shown set: the chosen mount's chosen skin's
                MountSettingsPage.Row rowA = RowOf("Coat_" + skinA), rowPlain = RowOf("Coat");
                Check(string.Equals(page.SkinChosen(MountCatalog.DonkeyId), skinA, StringComparison.OrdinalIgnoreCase) && rowA.Shown() && !rowPlain.Shown(), $"with the Skin row on the temp skin its rows show and the own art's don't (chosen {page.SkinChosen(MountCatalog.DonkeyId)})");
                ConfigEntry<string> skinRow = Row<string>("Skin_" + MountCatalog.DonkeyId);
                if (skinRow != null) skinRow.Value = MountLooks.DonkeyId;
                Check(skinRow != null && !rowA.Shown() && rowPlain.Shown(), "the Skin row on the own art: the sets swap");
                // worn: each skin's look is the companion's
                me.Skin = skinA;
                Check(me.Coat == CoatColor.Black && !me.Lantern, $"wearing the temp skin the companion's coat is its Black and its lantern off ({me.Coat}, {me.Lantern})");
                me.Skin = MountLooks.DonkeyId;
                Check(me.Coat == CoatColor.Game && me.Lantern, $"wearing the own art: Game default and the lantern on ({me.Coat}, {me.Lantern})");
                // regions per skin
                Check(MountSettingsPage.SkinHasRegion(skinA, "Harness") && MountSettingsPage.SkinHasRegion(skinA, "Muzzle") && MountSettingsPage.SkinHasRegion(MountLooks.DonkeyId, "Coat") && !MountSettingsPage.SkinHasRegion("donkey/selftest.perskin-none", "Coat"),
                    "SkinHasRegion: the over skin's own Harness, the donkey's Muzzle under it, the own art's Coat; a skin nothing provides has none");
                // no second seed: the file now holds the skin's values
                cfg.Save();
                var cfg2 = new ConfigFile(cfgPath, false) { SaveOnConfigSet = false };
                var page2 = new MountSettingsPage(owner + ".again", cfg2, "Per skin test 2", "test", "", null);
                PerSkinRows(page2, me);
                ConfigEntry<CoatColor> coatA2 = page2.Rows.Find(r => r.Entry != null && r.Entry.Definition.Key == "Coat_" + skinA)?.Entry as ConfigEntry<CoatColor>;
                Check(page2.SeededSkins == 0 && coatA2 != null && coatA2.Value == CoatColor.Black, $"a page over the saved file seeds nothing ({page2.SeededSkins}) and reads the skin's Black ({coatA2?.Value})");
                // a skin registered after the page (a skin pack loaded later): its rows at CatchUp, nothing seeded for it
                bool wroteB = true;
                try
                {
                    WriteOverSkin(rootB, "selftest.perskin-b", "Per Skin B");
                }
                catch (Exception e)
                {
                    Warn("the second temp skin could not be written: " + e.Message);
                    wroteB = false;
                }
                if (!wroteB) yield break;
                registeredB = Looks.RegisterFolder(rootB, owner + ".b") == 1;
                Check(registeredB && RowOf("Coat_" + skinB) == null, "a second temp skin registered after the page has no rows yet");
                int caught = page.CatchUp();
                Check(caught >= 1 && RowOf("Coat_" + skinB) != null && RowOf("Lantern_" + skinB) != null && page.SeededSkins == 1, $"CatchUp adds the late skin's rows ({caught} mount(s) caught up) and seeds nothing for it");
            }
            finally
            {
                if (registeredA) Looks.Unregister(skinA, owner + ".a");
                if (registeredB) Looks.Unregister(skinB, owner + ".b");
                try { if (System.IO.Directory.Exists(rootA)) System.IO.Directory.Delete(rootA, true); } catch { }
                try { if (System.IO.Directory.Exists(rootB)) System.IO.Directory.Delete(rootB, true); } catch { }
                try { System.IO.File.Delete(cfgPath); } catch { }
            }
        }

        // The rows a mod's page keeps per skin, as a player mod's colour rows are: a Coat choice and a Lantern toggle.
        private static int PerSkinRows(MountSettingsPage page, MountCompanion me)
        {
            page.MountRows("Mount", me);
            int sets = 0;
            page.PerSkin((skinId, mountId, sfx, shown) =>
            {
                if (!mountId.Equals(MountCatalog.DonkeyId, StringComparison.OrdinalIgnoreCase)) return; // the donkey's skins are enough here
                sets++;
                MountLook look = me.LookOf(skinId);
                page.Choice("Colours", "Coat" + sfx, look.Coat, "Coat colour", "", v => look.Coat = v, new[] { "Game default", "Brown", "Dark", "Cream", "Black", "Custom" }, look: true);
                page.Toggle("Colours", "Lantern" + sfx, look.Lantern, "Lantern", "", v => look.Lantern = v, look: true);
            });
            return sets;
        }

        // A temp over skin of the donkey (a red square named Harness) under root\gk2mounts\donkey\skins\<id>\.
        private static void WriteOverSkin(string root, string id, string name)
        {
            string dir = TempSkinDir(root, Catalog.DonkeyId, id);
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skin.cfg"), "[Skin]\nformat = 1\nname = " + name + "\nlayer = over\n[Colors]\nHarness = C41E38\n");
            var px = new Color32[96 * 96];
            var red = new Color32(196, 30, 56, 255);
            for (int y = 40; y < 50; y++)
            {
                for (int x = 50; x < 60; x++) px[y * 96 + x] = red;
            }
            var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "bdy.png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }
}
