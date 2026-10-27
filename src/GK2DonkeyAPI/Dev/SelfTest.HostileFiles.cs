using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Files another mod brings can't freeze the game, exhaust it or read outside their pack (docs/multi-mod-loading.md
    // section 2): a PNG's size taken from its header before it is decoded, a WAV chunk with a bad length refused (it was
    // an endless loop), NaN and Infinity refused wherever a pack writes a number, a file name leaving its folder
    // refused, a byte-order mark taken off an embedded cfg, a sheet's frame names checked.
    internal static partial class SelfTest
    {
        private static IEnumerator HostileFiles()
        {
            // pictures: the header first
            byte[] png = TempPicture();
            Check(Numbers.PngSize(png, out int pw, out int ph) && pw == 96 && ph == 96, $"a PNG's size from its header ({pw}x{ph})");
            byte[] huge = (byte[])png.Clone();
            huge[16] = 0; huge[17] = 0; huge[18] = 0x4E; huge[19] = 0x20; // 20000 wide
            huge[20] = 0; huge[21] = 0; huge[22] = 0x4E; huge[23] = 0x20; // 20000 high
            Color32[] px = Looks.DecodePng(huge, out int hw, out int hh, out string why, 96, 96);
            Check(px == null && hw == 20000 && (why ?? "").Contains("larger"), $"a 20000x20000 frame is refused from its header, never decoded ({why})");
            px = Looks.DecodePng(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 }, out _, out _, out why);
            Check(px == null && why == "not a PNG file", $"bytes that are no PNG are refused ({why})");
            px = Looks.DecodePng(png, out pw, out ph, out why, 96, 96);
            Check(px != null && px.Length == 96 * 96, "a frame of its canvas size is decoded");

            // a WAV chunk that says its length is -8 never moved on
            var wav = new byte[64];
            void Put(int at, string s) { for (int k = 0; k < 4; k++) wav[at + k] = (byte)s[k]; }
            Put(0, "RIFF"); Put(8, "WAVE"); Put(12, "junk");
            BitConverter.GetBytes(-8).CopyTo(wav, 16);
            string wavWhy = null;
            float t0 = Time.realtimeSinceStartup;
            try { SoundBank.FromWav(wav, "selftest-bad.wav"); } catch (FormatException e) { wavWhy = e.Message; }
            Check(wavWhy != null && wavWhy.Contains("length") && Time.realtimeSinceStartup - t0 < 1f, $"a WAV chunk of length -8 is refused at once ({wavWhy})");

            // numbers
            Check(!Numbers.TryFloat("NaN", out _) && !Numbers.TryFloat("Infinity", out _) && !Numbers.TryFloat("-Infinity", out _) && Numbers.TryFloat(" 1.5 ", out float f15) && f15 == 1.5f,
                "NaN and Infinity are not numbers; 1.5 is");
            Check(Numbers.Finite(float.NaN, 0.2f, 1f, 0.6f) == 0.6f && Numbers.Finite(5f, 0.2f, 1f, 0.6f) == 1f, "a code mod's NaN gets the default, a number its range");

            // names inside the pack only
            string dir = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-hostile", "pack");
            Check(Numbers.InFolder(dir, @"..\outside.png") == null && Numbers.InFolder(dir, @"\\some-host\share\p.png") == null
                && Numbers.InFolder(dir, Path.Combine(Path.GetPathRoot(Path.GetTempPath()), "x.png")) == null && Numbers.InFolder(dir, "front/bdy_walk_left_01.png") != null,
                "a file name climbing out, rooted or on a network share is refused; one in a subfolder is found");
            Check(Numbers.CfgText(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'[', (byte)'M', (byte)']' }) == "[M]", "an embedded cfg's byte-order mark is taken off");

            const string owner = Owner + ".hostile";
            const string id = "selftest.hostile";
            string root = null, root2 = null;
            try
            {
                root = WriteTempPack("gk2donkeyapi-selftest-hostile", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = Hostile\nmount = " + id + "\noffset = NaN 0\npivot = 100000 5\nportrait = ..\\..\\outside.png\n\n" +
                    "[Mount]\nid = " + id + "\nname = Hostile\nauthor = the self-test\nversion = 0.1\nfoal_scale = NaN\nspeed = Infinity\n");
                // the file the portrait line points at exists: before, it was read from outside the skin's folder
                File.WriteAllBytes(Path.Combine(root, Looks.PackDirName, id, "outside.png"), png);
                int n = Looks.RegisterFolder(root, owner);
                Catalog.Entry k = Catalog.Find(id);
                Looks.SkinDef d = Looks.Find(id + "/" + Looks.DefaultSkinName);
                Check(n == 1 && k != null && d != null, $"the pack loads ({n})");
                if (k != null)
                {
                    Check(k.FoalScale == 0.6f && (k.Pack?.Report ?? "").Contains("foal_scale = NaN"), $"foal_scale = NaN: the babies stay 0.6, said in the report ({k.FoalScale})");
                    Check(!k.Speed.HasValue, $"speed = Infinity: no speed of its own ({k.Speed})");
                }
                if (d != null)
                {
                    Check(Numbers.IsFinite(d.OffsetRide.x) && (d.Report ?? "").Contains("offset = NaN 0"), $"offset = NaN 0: ignored and said ({d.OffsetRide})");
                    Check(d.PivotX <= 2 * Looks.MaxCanvas, $"pivot = 100000 5: kept in range ({d.PivotX})");
                    Info("the next \"is not in the pack\" warning is this test's own: a portrait outside the pack");
                    Texture2D portrait = Looks.PortraitOf(d);
                    Check(portrait == null, "portrait = ..\\..\\outside.png: not read from outside the pack");
                }
                root2 = WriteTempPack("gk2donkeyapi-selftest-hostile2", "selftest.hostile-game", "[Skin]\nformat = 1\nname = Hostile Game\nsource = game\n\n[Game]\nprefix = 0901_\nscale = NaN\n");
                Info("the next \"could not be registered\" warning is this test's own: [Game] scale = NaN is refused");
                n = Looks.RegisterFolder(root2, owner);
                Check(n == 0 && Looks.Find("donkey/selftest.hostile-game") == null, "[Game] scale = NaN is refused");
            }
            finally
            {
                if (Looks.Find(id + "/" + Looks.DefaultSkinName) != null) Looks.Unregister(id + "/" + Looks.DefaultSkinName, owner);
                Looks.UnregisterMount(id, owner);
                foreach (string r in new[] { root, root2 }) { try { if (r != null) Directory.Delete(r, true); } catch { } }
            }
            yield break;
        }
    }
}
