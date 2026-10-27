using System;
using System.Collections;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Versions and ids between mods (docs/multi-mod-loading.md section 5 and 6): a mount.cfg says which API it needs
    // (a newer one refuses it with its skins); ids are ASCII without a dot at an end; a version that isn't a number ranks
    // below one that is; a sound bank id can't be two packs'; a mod's sound for an event isn't another's to replace.
    internal static partial class SelfTest
    {
        private static IEnumerator VersionsAndIds()
        {
            Check(Catalog.IdRule("saddleup.dragon", "id") == null && Catalog.IdRule("saddleup.drаgon", "id") != null, "ids: ASCII only (a Cyrillic letter that looks Latin is refused)");
            Check(Catalog.IdRule("acme.dragon.", "id") != null && Catalog.IdRule(".acme", "id") != null && Catalog.IdRule("acme..dragon", "id") != null && Catalog.IdRule(new string('a', Catalog.IdMax + 1), "id") != null,
                "ids: no dot at an end or two in a row, at most " + Catalog.IdMax + " characters");
            Check(Looks.CompareVersions("10", "9") > 0 && Looks.CompareVersions("1.0", "1.0-beta") > 0 && Looks.CompareVersions("1.0-beta", "1.0") < 0 && Looks.CompareVersions("1.10", "1.9") > 0,
                "versions: numbers compare as numbers; one that isn't a number ranks below one that is");
            var a = new SoundBank.FileSource("acme", "x.dragon/s", null, null, "a");
            var b = new SoundBank.FileSource("acme.x", "dragon/s", null, null, "b");
            Check(SoundBank.IdFor(a, "voice") != SoundBank.IdFor(b, "voice"), $"two packs' sound ids never meet ({SoundBank.IdFor(a, "voice")} / {SoundBank.IdFor(b, "voice")})");

            const string owner = Owner + ".versions";
            string r1 = null, r2 = null;
            MountHandle h = null;
            IDisposable s1 = null, s2 = null;
            try
            {
                r1 = WriteTempPack("gk2donkeyapi-selftest-needs", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Needs\n\n[Mount]\nneeds = 99.0\nid = selftest.needs-beast\nname = Needs Beast\nversion = 1.0\n");
                Info("the next \"could not be registered\" warning is this test's own: a mount.cfg that needs API 99.0");
                int n = Looks.RegisterFolder(r1, owner);
                Check(n == 0 && Catalog.Find("selftest.needs-beast") == null && Looks.Find("selftest.needs-beast/default") == null, $"a mount.cfg with needs = 99.0 is refused with its skins ({n})");
                r2 = WriteTempPack("gk2donkeyapi-selftest-needs2", Looks.DefaultSkinName, "[Skin]\nformat = 1\nname = Needs Now\n\n[Mount]\nformat = 1\nneeds = " + Mounts.ApiVersion.Major + "." + Mounts.ApiVersion.Minor + "\nid = selftest.needs-now\nname = Needs Now\nversion = 1.0\n");
                n = Looks.RegisterFolder(r2, owner);
                Catalog.Entry k = Catalog.Find("selftest.needs-now");
                Check(n == 1 && k != null && k.Needs != null && k.Format == 1, $"needs = this version and format = 1 are taken, and kept for its Save ({k?.Needs}, format {k?.Format})");

                h = Spawn(home + new Vector3(3f, 0f, 0f), true);
                if (h != null)
                {
                    s1 = Mounts.Sounds.Set(Owner + ".versions-a", h, "voice", MountSound.Game("donkey_bell"));
                    Info("the next \"is refused\" warning is this test's own: another mod's sound for the same event");
                    s2 = Mounts.Sounds.Set(Owner + ".versions-b", h, "voice", MountSound.None);
                    Check(h.I.SoundsRig.OverrideOwner("voice") == Owner + ".versions-a", "a second mod's sound for the event is refused: the first stays");
                    s2.Dispose();
                    Check(h.I.SoundsRig.OverrideOwner("voice") == Owner + ".versions-a", "... and taking the refused one back leaves the first's");
                    s1.Dispose();
                    Check(h.I.SoundsRig.OverrideOwner("voice") == null, "the first takes its own back");
                }
            }
            finally
            {
                s2?.Dispose();
                s1?.Dispose();
                if (h != null && h.Exists) h.Remove();
                if (Looks.Find("selftest.needs-now/default") != null) Looks.Unregister("selftest.needs-now/default", owner);
                Looks.UnregisterMount("selftest.needs-now", owner);
                foreach (string r in new[] { r1, r2 }) { try { if (r != null) System.IO.Directory.Delete(r, true); } catch { } }
            }
            yield break;
        }
    }
}
