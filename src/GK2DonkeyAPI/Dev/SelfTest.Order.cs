using System;
using System.Collections;
using System.IO;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Order and identity between mods (docs/multi-mod-loading.md section 4): a newer pack of a mount brings its facts
    // and its pictures together (the old pack's skins go); a mount.cfg whose id isn't its folder's is refused; a code
    // skin registered before its mount meets the name rules when the mount comes; skins of a mount that isn't installed
    // are nobody's (the panel lists the mount as not installed, never pickable); a handle withdraws only what it
    // registered; a pack folder is one MountsChanged.
    internal static partial class SelfTest
    {
        private static IEnumerator OrderAndIdentity()
        {
            const string oA = Owner + ".order-a", oB = Owner + ".order-b", oC = Owner + ".order-c";
            const string beast = "selftest.order-beast", other = "selftest.order-other", codeMount = "selftest.order-code", absent = "selftest.order-absent";
            string r1 = null, r2 = null, r3 = null, r4 = null;
            IDisposable cm = null, early = null, earlyPlain = null, earlyDotted = null, h1 = null, h2 = null;
            int changed = 0;
            Action count = () => changed++;
            try
            {
                // v1 then v2 of one mount, two loose folders: v2's facts and v2's look
                r1 = WriteTempPack("gk2donkeyapi-selftest-order1", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Old Look\n\n[Mount]\nid = " + beast + "\nname = Order Beast v1\nversion = 1.0\ncart = false\nfoals = true\n");
                AddTempSkin(r1, beast, "extra", "[Skin]\nformat = 1\nname = v1's extra\n");
                Looks.RegisterFolder(r1, oA);
                r2 = WriteTempPack("gk2donkeyapi-selftest-order2", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = New Look\n\n[Mount]\nid = " + beast + "\nname = Order Beast v2\nversion = 2.0\ncart = true\nfoals = true\n");
                Info("the next \"replaces the one of\" warning is this test's own: v2 of a mount next to v1");
                Mounts.Catalog.MountsChanged += count;
                Looks.RegisterFolder(r2, oB);
                Mounts.Catalog.MountsChanged -= count;
                Catalog.Entry k = Catalog.Find(beast);
                Looks.SkinDef own = Looks.Find(beast + "/" + Looks.DefaultSkinName);
                Check(k != null && k.Owner == oB && k.Cart && own != null && own.Owner == oB && own.Name == "New Look",
                    $"v2 next to v1: v2's facts and v2's own look ({k?.Name}; the look \"{own?.Name}\" of {own?.Owner})");
                Check(Looks.Find(beast + "/extra") == null, "v1's other skin is withdrawn with its pack");
                Check(changed >= 1, $"MountsChanged was raised for the takeover ({changed})");

                // a mount.cfg whose id isn't its folder's
                r3 = WriteTempPack("gk2donkeyapi-selftest-order3", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Misnamed\n\n[Mount]\nid = " + other + "\nname = Misnamed\nversion = 1.0\n");
                string cfg = Path.Combine(TempMountDir(r3, other), Looks.MountCfgName);
                File.WriteAllText(cfg, File.ReadAllText(cfg).Replace("id = " + other, "id = selftest.order-renamed"));
                Info("the next \"could not be registered\" warning is this test's own: a mount.cfg whose id isn't its folder's name");
                int n = Looks.RegisterFolder(r3, oA);
                Check(n == 0 && Catalog.Find(other) == null && Catalog.Find("selftest.order-renamed") == null, $"a mount.cfg whose id isn't its folder's is refused with its skins ({n})");

                // a code skin registered before its mount, by another mod
                early = Mounts.Looks.Register(oC, new MountSkinDef { Id = Looks.DefaultSkinName, Name = "Squatter", Mount = codeMount, Source = new NoPictures() });
                earlyPlain = Mounts.Looks.Register(oC, new MountSkinDef { Id = "frost", Name = "Plain", Mount = codeMount, Source = new NoPictures() });
                earlyDotted = Mounts.Looks.Register(oC, new MountSkinDef { Id = "selftest.frost", Name = "Dotted", Mount = codeMount, Source = new NoPictures() });
                Info("the next two \"registered before its mount\" warnings are this test's own");
                changed = 0;
                Mounts.Catalog.MountsChanged += count;
                cm = Mounts.Catalog.Register(oA, new MountDef { Id = codeMount, Name = "Order Code" });
                Mounts.Catalog.MountsChanged -= count;
                Check(Looks.Find(codeMount + "/" + Looks.DefaultSkinName) == null, "another mod's 'default' registered before the mount: withdrawn when the mount comes (it is the mount's own look)");
                Check(Looks.Find(codeMount + "/frost") == null && Looks.Find(codeMount + "/selftest.frost") != null, "its skin without its mod's prefix withdrawn, the prefixed one kept");

                // a skin pack for a mount that isn't installed
                r4 = WriteTempPack("gk2donkeyapi-selftest-order4", "selftest.waiting", "[Skin]\nformat = 1\nname = Waiting\nmount = " + absent + "\n");
                n = Looks.RegisterFolder(r4, oA);
                Check(n == 1 && Catalog.WaitingSkins().ContainsKey(absent), $"a skin for a mount that isn't installed is registered and waits ({n})");
                Check(!Catalog.SkinsOf(Catalog.Donkey).Exists(s => s.MountId == absent), "it is no skin of the donkey");
                MountsPanel.Entry card = MountsPanel.Entries().Find(e => e.Id == absent);
                Check(card != null && card.Fake && card.Label.Contains("not installed"), $"the panel lists its mount as not installed, not pickable ({card?.Label})");

                // a handle withdraws only what it registered
                h1 = Mounts.Catalog.Register(oA, new MountDef { Id = "selftest.order-handle", Name = "First" });
                h2 = Mounts.Catalog.Register(oA, new MountDef { Id = "selftest.order-handle", Name = "Second" });
                h1.Dispose();
                Check(Catalog.Find("selftest.order-handle")?.Name == "Second", "the first handle disposed after the mount was registered again: the second stays");
                h2.Dispose();
                Check(Catalog.Find("selftest.order-handle") == null, "the second handle withdraws it");
            }
            finally
            {
                Mounts.Catalog.MountsChanged -= count;
                foreach (IDisposable h in new[] { earlyDotted, earlyPlain, early, cm, h2, h1 }) { try { h?.Dispose(); } catch { } }
                foreach (Looks.SkinDef d in new System.Collections.Generic.List<Looks.SkinDef>(Looks.All))
                {
                    if (d.MountId == beast || d.MountId == absent || d.MountId == other) Looks.Unregister(d.Id, d.Owner);
                }
                foreach (string m in new[] { beast, other })
                {
                    Catalog.Entry kd = Catalog.Find(m);
                    if (kd != null) Looks.UnregisterMount(m, kd.Owner);
                }
                foreach (string r in new[] { r1, r2, r3, r4 }) { try { if (r != null) Directory.Delete(r, true); } catch { } }
            }
            yield break;
        }
    }
}
