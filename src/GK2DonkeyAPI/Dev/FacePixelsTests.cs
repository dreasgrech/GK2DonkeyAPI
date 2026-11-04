using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The self-test of the donkey's eye and nostril repaint (FacePixels; docs/face-pixels.md): the list read and its two
    // ways on a made-up frame; then the game's atlas once the repaint is done: every listed pixel holds its new colour, and
    // every frame put back as the game draws it fingerprints as donkey-frames.txt says (no other pixel moved). After the
    // run, the run having crossed scenes and doors: the atlas still repainted. The colours' pairs (the eye's line as
    // drawn under "Legs and mane", the nostril following Muzzle) are the integration tests' (FacePixelsPairs).
    internal static class FacePixelsTests
    {
        internal static IEnumerator Repainted()
        {
            Dictionary<string, List<FacePixels.Spot>> parsed = FacePixels.ParseList("# a comment\nbdy_a 0 0 eye 292316 292319\r\nbroken line\nbdy_a 2 1 nostril 352e1e 362d1e\nbdy_b 1 1 lid 292316 292319\n");
            bool parsedOk = parsed.Count == 1 && parsed.TryGetValue("bdy_a", out List<FacePixels.Spot> spots) && spots.Count == 2 && spots[0].Eye && !spots[1].Eye && spots[1].YTop == 1;
            SelfTest.Check(parsedOk, $"face-pixels.txt read: a comment, a broken line and an unknown part skipped ({parsed.Count} frame(s))");
            if (parsedOk)
            {
                // a 3 x 2 frame, Unity's rows from the bottom: (0, 0 from the top) is index 3, (2, 1 from the top) index 2
                Color32 dark = C(0x292316), eye = C(0x292319), mane = C(0x352e1e), nostril = C(0x362d1e), nose = C(0xabb298);
                var px = new[] { dark, nose, mane, dark, nose, nose };
                FacePixels.Apply(parsed["bdy_a"], px, 3, 2, FacePixels.Read.Repainted);
                bool painted = Same(px[3], eye) && Same(px[2], nostril) && Same(px[0], dark);
                FacePixels.Apply(parsed["bdy_a"], px, 3, 2, FacePixels.Read.Repainted);
                bool twice = Same(px[3], eye) && Same(px[2], nostril);
                FacePixels.Apply(parsed["bdy_a"], px, 3, 2, FacePixels.Read.AsDrawn);
                bool back = Same(px[3], dark) && Same(px[2], mane);
                px[2] = nose;
                FacePixels.Apply(parsed["bdy_a"], px, 3, 2, FacePixels.Read.Repainted);
                bool otherKept = Same(px[2], nose);
                SelfTest.Check(painted && twice && back && otherKept,
                    $"on a made-up frame: the listed pixels repainted, the same dark elsewhere kept ({painted}); a second pass changes nothing ({twice}); as drawn puts them back ({back}); a listed pixel of another colour left alone ({otherKept})");
            }
            SelfTest.Check(FacePixels.Listed > 0, $"the list is in the DLL: {FacePixels.Listed} pixel(s) on {FacePixels.ListedFrames.Count} frame(s)");

            yield return SelfTest.Until(() => FacePixels.Now == FacePixels.State.Done || FacePixels.Now == FacePixels.State.Off, 60f);
            if (FacePixels.Now == FacePixels.State.Off && (DonkeyFrames.Now == DonkeyFrames.State.NotFound || DonkeyFrames.Now == DonkeyFrames.State.NoList))
            {
                SelfTest.Skip(FacePixels.Note);
                yield break;
            }
            if (FacePixels.Now != FacePixels.State.Done)
            {
                SelfTest.Fail($"the repaint is {FacePixels.Now} after 60 s (the donkey-art check {DonkeyFrames.Now}){(FacePixels.Note.Length > 0 ? ": " + FacePixels.Note : "")}");
                yield break;
            }
            SelfTest.Check(FacePixels.Problems.Count == 0 && FacePixels.Frames == FacePixels.ListedFrames.Count, FacePixels.Note + (FacePixels.Problems.Count > 0 ? "; " + string.Join("; ", FacePixels.Problems.ToArray()) : ""));
            yield return AtlasHolds("now");
        }

        internal static IEnumerator AfterRun()
        {
            if (FacePixels.Now != FacePixels.State.Done)
            {
                SelfTest.Skip($"the repaint is {FacePixels.Now} ({FacePixels.Note})");
                yield break;
            }
            SelfTest.Info($"the frames found unloaded and repainted again this session: {FacePixels.Repatched} time(s)");
            yield return AtlasHolds("after the run");
        }

        // Every listed frame read from the atlas: its listed pixels the new colours, and put back as the game draws it, its
        // fingerprint donkey-frames.txt's.
        private static IEnumerator AtlasHolds(string when)
        {
            Looks.SkinBase b = Looks.Donkey;
            yield return SelfTest.Until(() => Looks.BaseFramesInMemory(b, b.GamePrefix), 10f);
            var found = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            Looks.GameFramesFound(found);
            var keys = new List<string>(FacePixels.ListedFrames);
            keys.Sort(StringComparer.Ordinal);
            int frames = 0, pixels = 0, repainted = 0, printsSame = 0, printsListed = 0, read = 0;
            var notRepainted = new List<string>();
            var otherPrint = new List<string>();
            foreach (string key in keys)
            {
                if (!found.TryGetValue(key, out Sprite s) || s == null) continue;
                frames++;
                Looks.SkinFrame f = Looks.ReadFrame(key, s, b, FacePixels.Read.Atlas);
                if (f == null) continue;
                read++;
                int here = 0;
                List<FacePixels.Spot> spots = FacePixels.Spots(key);
                foreach (FacePixels.Spot p in spots)
                {
                    pixels++;
                    if (Same(f.P[(f.H - 1 - p.YTop) * f.W + p.X], p.To)) here++;
                }
                repainted += here;
                if (here < spots.Count) notRepainted.Add($"{key} {here}/{spots.Count}");
                FacePixels.Apply(spots, f.P, f.W, f.H, FacePixels.Read.AsDrawn);
                if (DonkeyFrames.Listed(key, out DonkeyFrames.Print want))
                {
                    printsListed++;
                    DonkeyFrames.Print have = DonkeyFrames.Of(f.W, f.H, f.P);
                    if (have.W == want.W && have.H == want.H && have.Hash == want.Hash) printsSame++;
                    else otherPrint.Add(key);
                }
                if (read % 4 == 0) yield return null; // a few reads a frame: each blits a whole atlas page
            }
            SelfTest.Check(frames == keys.Count && read == frames && repainted == pixels && pixels == FacePixels.Listed,
                $"{when}: the atlas holds the new colours at {repainted} of the {pixels} listed pixel(s), {read} of the {keys.Count} frame(s) read{(notRepainted.Count > 0 ? "; not: " + string.Join(", ", notRepainted.ToArray()) : "")}");
            SelfTest.Check(printsListed == read && printsSame == printsListed,
                $"{when}: put back as the game draws them, {printsSame} of {printsListed} frame(s) are donkey-frames.txt's, pixel for pixel (nothing else moved){(otherPrint.Count > 0 ? "; differing: " + string.Join(", ", otherPrint.ToArray()) : "")}");
        }

        private static Color32 C(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
