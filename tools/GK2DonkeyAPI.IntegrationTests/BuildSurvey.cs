using GK2DonkeyAPI.Api;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // The skin builds compared (measures only; the investigation of 2026-11-09, docs/performance.md "Skin sheets let go
    // and built again"): the red dragon's own pictures (read through its skin's loader, from the pack's DLL) written as
    // two temporary donkey skins, one PNG per picture (as every shipped skin is) and one ready-made sheet with its
    // frame list (the existing sheet = support), each worn, let go and worn again, alternating. Each build's line: its
    // time, its frames, the most in one frame, and its garbage by step. Nothing checked but that both built.
    internal static class BuildSurvey
    {
        private const string FilesId = "selftest.build-files", SheetId = "selftest.build-sheet";

        internal static IEnumerator Run()
        {
            string noPack = Tests.MissingPack("dragon");
            if (noPack != null) { Skip(noPack); yield break; }
            Looks.SkinDef src = Looks.Find(Tests.Sk("dragon"));
            if (src == null || src.LoadPng == null || src.Keys == null) { Skip("the dragon's skin has no picture files to read"); yield break; }
            string owner = Owner + ".builds";
            string root = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-builds");
            var made = new List<MountHandle>();
            try
            {
                // the pictures, once: as files, and packed in a sheet (16 a row) with the frame list in its order
                var keys = new List<string>();
                var pngs = new List<byte[]>();
                foreach (string k in src.Keys())
                {
                    if (k.IndexOf('/') >= 0) continue; // the main layer only (the front layer is a second sheet)
                    byte[] b = src.LoadPng(k);
                    if (b == null) continue;
                    keys.Add(k);
                    pngs.Add(b);
                }
                if (keys.Count == 0) { Skip("none of the dragon's pictures could be read"); yield break; }
                if (Directory.Exists(root)) Directory.Delete(root, true);
                string files = TempSkinDir(root, MountCatalog.DonkeyId, FilesId), sheet = TempSkinDir(root, MountCatalog.DonkeyId, SheetId);
                Directory.CreateDirectory(files);
                Directory.CreateDirectory(sheet);
                string cfg = "[Skin]\nformat = 1\nname = {0}\nbase = donkey\ncolours = own\nlayer = replace\ncanvas = 128 128\nseat = 62\n";
                File.WriteAllText(Path.Combine(files, "skin.cfg"), string.Format(cfg, "Build test, files"));
                for (int i = 0; i < keys.Count; i++) File.WriteAllBytes(Path.Combine(files, keys[i] + ".png"), pngs[i]);
                // the decode into a reused buffer (its bytes put in order by hand for ARGB32) against Unity's own conversion,
                // pixel for pixel, on a picture with colour in it and on a second one into the same buffer
                {
                    Color32[] reuse = null;
                    int mismatches = 0, compared = 0;
                    foreach (int pi in new[] { 0, keys.Count / 2 })
                    {
                        Color32[] mine = Looks.DecodePngInto(pngs[pi], ref reuse, out int mw, out int mh, out string mwhy);
                        var refTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        try
                        {
                            refTex.LoadImage(pngs[pi]);
                            Color32[] theirs = refTex.GetPixels32();
                            if (mine == null || mine.Length != theirs.Length) { mismatches++; continue; }
                            for (int i = 0; i < theirs.Length; i++)
                            {
                                compared++;
                                Color32 a = mine[i], b = theirs[i];
                                if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a) mismatches++;
                            }
                        }
                        finally
                        {
                            UnityEngine.Object.Destroy(refTex);
                        }
                    }
                    Check(mismatches == 0 && compared > 0, $"a picture decoded into the reused buffer is Unity's own, pixel for pixel ({compared} compared, {mismatches} differ)");
                }
                const int cols = 16, cell = 128;
                int rows = (keys.Count + cols - 1) / cols;
                var big = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false);
                var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    var clear = new Color32[big.width * big.height];
                    big.SetPixels32(clear);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (!tmp.LoadImage(pngs[i]) || tmp.width != cell || tmp.height != cell) { Info($"[builds] {keys[i]}: not a {cell}x{cell} picture, its cell left empty"); continue; }
                        int x = (i % cols) * cell, yTop = (i / cols) * cell;
                        big.SetPixels32(x, big.height - yTop - cell, cell, cell, tmp.GetPixels32()); // the sheet's rows run from the top
                    }
                    File.WriteAllBytes(Path.Combine(sheet, "sheet.png"), big.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.Destroy(big);
                    UnityEngine.Object.Destroy(tmp);
                }
                File.WriteAllText(Path.Combine(sheet, "skin.cfg"), string.Format(cfg, "Build test, one sheet") + "sheet = sheet.png\nframes = " + string.Join(", ", keys) + "\n");
                long sheetBytes = new FileInfo(Path.Combine(sheet, "sheet.png")).Length, fileBytes = 0;
                foreach (byte[] b in pngs) fileBytes += b.Length;
                int registered = Looks.RegisterFolder(root, owner);
                Info($"[builds] {keys.Count} pictures of the dragon: as files {fileBytes / 1024} KB in all, as one sheet {sheetBytes / 1024} KB ({cols} a row, {rows} rows); {registered} test skin(s) registered");
                if (registered != 2) { Fail($"{registered} of the 2 test skins registered"); yield break; }

                // each worn, let go, worn again: files, sheet, files, sheet
                Vector3 at = Spot(home, 3f) ?? home;
                foreach (string id in new[] { FilesId, SheetId, FilesId, SheetId })
                {
                    string full = Looks.SkinKey(MountCatalog.DonkeyId, id);
                    MountHandle h = Spawn(at, true);
                    if (h == null) { Fail("could not spawn the wearer"); yield break; }
                    made.Add(h);
                    yield return Until(() => h.IsReady, 6f);
                    long gc0 = GC.CollectionCount(0);
                    h.Tuning.Set(Owner, "Skin", full);
                    yield return Until(() => Looks.TestBuildLine(full) != null, 20f);
                    string line = Looks.TestBuildLine(full);
                    long alloc = Looks.TestBuildAlloc(full);
                    if (id == FilesId && alloc >= 0) Check(alloc < 3L * 1024 * 1024, $"one PNG per picture: the build's garbage under 3 MB ({alloc / 1048576.0:F1} MB; 8.6-9.0 before the pictures were decoded into a reused buffer)");
                    Info($"[builds] {(id == FilesId ? "one PNG per picture" : "one ready-made sheet")}: {(line ?? "not built in 20 s")}; collections meanwhile {GC.CollectionCount(0) - gc0}");
                    if (line == null) Fail($"{full} wasn't built");
                    h.Remove();
                    made.Remove(h);
                    yield return Seconds(1f); // its sheet let go (nobody else wears it)
                }
            }
            finally
            {
                foreach (MountHandle h in made) { try { if (h != null && h.Exists) h.Remove(); } catch { } }
                foreach (string id in new[] { FilesId, SheetId }) { try { Looks.Unregister(Looks.SkinKey(MountCatalog.DonkeyId, id), owner); } catch { } }
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
            Check(true, "the skin builds compared (the [builds] lines above)");
        }
    }
}
