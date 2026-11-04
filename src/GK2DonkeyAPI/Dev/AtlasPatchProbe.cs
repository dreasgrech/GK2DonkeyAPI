using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // A probe for giving the donkey's eye and nostril pixels a colour of their own (the user, 2026-11-04: "there are some
    // pixels around the eye which are affected by the legs and mane group ... it looks sloppy otherwise"; docs/face-pixels.md).
    // Those pixels are 292316 and 352e1e, the same colours as the legs' and the mane's fill, and the colour swap goes by
    // colour; the idea under test is to repaint them, once, in the game's own character atlas on the GPU (Graphics.CopyTexture:
    // the atlas is RGBA32, uncompressed, point-filtered, one mip, not readable), so every look over the donkey sees them.
    // What it needs to know first, measured here and nothing kept: the GPU's copy support; per donkey sprite its atlas page,
    // packing and rects; whether a one-pixel copy into the real page reads back (ReadFrame, as the skins' builds read the
    // art), and the original copied back reads back too; and (the scenario after the run) whether the pages are the same
    // objects after the run's scenes and doors.
    internal static class AtlasPatchProbe
    {
        private static readonly Dictionary<string, int> pagesAtProbe = new Dictionary<string, int>(StringComparer.Ordinal);

        internal static IEnumerator Probe()
        {
            Looks.SkinBase b = Looks.Donkey;
            yield return SelfTest.Until(() => Looks.BaseFramesInMemory(b, b.GamePrefix), 10f);
            var found = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            Looks.GameFramesFound(found);
            if (found.Count == 0)
            {
                SelfTest.Skip("the donkey's frames are not in memory (a mount out in the world brings them)");
                yield break;
            }
            SelfTest.Info($"the GPU: {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceVersion}; copy support {SystemInfo.copyTextureSupport}");
            var pages = new Dictionary<int, (Texture2D T, int N)>();
            int rotated = 0, tight = 0, fractional = 0;
            var odd = new List<string>();
            pagesAtProbe.Clear();
            foreach (KeyValuePair<string, Sprite> kv in found)
            {
                Sprite s = kv.Value;
                if (s == null || s.texture == null) continue;
                int id = s.texture.GetInstanceID();
                pages[id] = (s.texture, pages.TryGetValue(id, out var p) ? p.N + 1 : 1);
                pagesAtProbe[kv.Key] = id;
                if (s.packed && s.packingRotation != SpritePackingRotation.None) { rotated++; odd.Add(kv.Key + " rotated " + s.packingRotation); }
                if (s.packed && s.packingMode == SpritePackingMode.Tight) tight++;
                Rect r = s.textureRect;
                if (Mathf.Abs(r.x - Mathf.Round(r.x)) > 0.001f || Mathf.Abs(r.y - Mathf.Round(r.y)) > 0.001f) fractional++;
            }
            foreach (var kv in pages)
            {
                Texture2D t = kv.Value.T;
                SelfTest.Info($"page {kv.Key} '{t.name}': {t.width}x{t.height} {t.format}, readable {t.isReadable}, mips {t.mipmapCount}, filter {t.filterMode}, {kv.Value.N} donkey frame(s)");
            }
            SelfTest.Info($"{found.Count} donkey frame(s) on {pages.Count} page(s); packed and rotated {rotated}{(odd.Count > 0 ? " (" + string.Join(", ", odd) + ")" : "")}, tight-packed {tight}, a fractional texture rect {fractional}");
            // one sprite's rects, for the mapping a patch uses
            if (found.TryGetValue("bdy_static_left", out Sprite st) && st != null)
                SelfTest.Info($"bdy_static_left: textureRect {st.textureRect}, rect {st.rect}, textureRectOffset {st.textureRectOffset}, packed {st.packed}, mode {(st.packed ? st.packingMode.ToString() : "-")}");
            yield return null;

            // the one-pixel copy: the eye pixel left of the white on the standing frame (292316, the eye's dark), its texel
            // in the page found by reading it, 292319 copied in and read back, 292316 copied back and read back
            if (st == null) { SelfTest.Warn("bdy_static_left not among the frames: the copy not tried"); yield break; }
            Looks.SkinFrame before = Looks.ReadFrame("bdy_static_left", st, b);
            if (before == null) { SelfTest.Warn("bdy_static_left could not be read: the copy not tried"); yield break; }
            int white = -1;
            for (int i = 0; i < before.P.Length; i++) if (before.P[i].a >= 128 && before.P[i].r == 255 && before.P[i].g == 255 && before.P[i].b == 255) { white = i; break; }
            if (white < 0) { SelfTest.Warn("no eye white on bdy_static_left: the copy not tried"); yield break; }
            int wx = white % before.W, wy = white / before.W; // canvas, Unity rows from the bottom
            int ex = wx - 1, ey = wy;
            Color32 eye = before.P[ey * before.W + ex];
            SelfTest.Info($"the eye white at canvas ({wx}, {before.H - 1 - wy} from the top); its left neighbour {Hex(eye)} (the eye's dark: 292316 expected)");
            if (Hex(eye) != "292316") { SelfTest.Warn("the eye's left neighbour isn't 292316: the copy not tried"); yield break; }
            Texture2D page = st.texture;
            Rect tr = st.textureRect;
            int ox = Mathf.FloorToInt(st.textureRectOffset.x), oy = Mathf.FloorToInt(st.textureRectOffset.y);
            // ReadFrame reads the crop at (tr.x, tr.y); the texel of canvas (ex, ey) is (tr.x + ex - ox, tr.y + ey - oy):
            // the rect's corner as a whole texel, round and floor both tried, the one that holds 292316 taken
            int ax = -1, ay = -1;
            foreach (int cx in new[] { Mathf.RoundToInt(tr.x), Mathf.FloorToInt(tr.x) })
                foreach (int cy in new[] { Mathf.RoundToInt(tr.y), Mathf.FloorToInt(tr.y) })
                {
                    if (ax >= 0) break;
                    Color32? t = Texel(page, cx + ex - ox, cy + ey - oy);
                    if (t != null && Hex(t.Value) == "292316") { ax = cx + ex - ox; ay = cy + ey - oy; }
                }
            if (ax < 0) { SelfTest.Warn($"the eye pixel's texel not found in the page (textureRect {tr}, offset {ox},{oy}): the copy not tried"); yield break; }
            SelfTest.Info($"the eye pixel is texel ({ax}, {ay}) of page {page.GetInstanceID()}");
            bool copied = Copy(page, ax, ay, new Color32(0x29, 0x23, 0x19, 255), out string error);
            yield return null;
            Looks.SkinFrame patched = copied ? Looks.ReadFrame("bdy_static_left", st, b) : null;
            string afterPatch = patched != null ? Hex(patched.P[ey * before.W + ex]) : "unread";
            bool restored = Copy(page, ax, ay, new Color32(0x29, 0x23, 0x16, 255), out string error2);
            yield return null;
            Looks.SkinFrame back = restored ? Looks.ReadFrame("bdy_static_left", st, b) : null;
            string afterRestore = back != null ? Hex(back.P[ey * before.W + ex]) : "unread";
            int otherChanged = 0;
            if (back != null) for (int i = 0; i < back.P.Length; i++) if (!Same(back.P[i], before.P[i])) otherChanged++;
            SelfTest.Info($"the copy: 292319 in {(copied ? "done" : "FAILED: " + error)}, read back {afterPatch}; 292316 back {(restored ? "done" : "FAILED: " + error2)}, read back {afterRestore}; pixels of the frame differing from before after both: {otherChanged}");
            if (!restored || afterRestore != "292316") SelfTest.Warn("the eye pixel may stay 292319 this session (one step of blue, the same to the eye): restart the game to be sure");
        }

        // After the run: the donkey's pages the same objects as at the probe (the run crosses scenes and doors).
        internal static IEnumerator PagesAfterRun()
        {
            if (pagesAtProbe.Count == 0) { SelfTest.Skip("the probe did not run in this session (the Measurements group)"); yield break; }
            var found = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            Looks.GameFramesFound(found);
            int same = 0, other = 0, gone = 0;
            foreach (KeyValuePair<string, int> kv in pagesAtProbe)
            {
                if (!found.TryGetValue(kv.Key, out Sprite s) || s == null || s.texture == null) gone++;
                else if (s.texture.GetInstanceID() == kv.Value) same++;
                else other++;
            }
            SelfTest.Info($"after the run: of the {pagesAtProbe.Count} donkey frames the probe saw, {same} on the same page object, {other} on another (the atlas loaded again), {gone} not in memory now");
            yield return null;
        }

        private static bool Copy(Texture2D page, int x, int y, Color32 c, out string error)
        {
            error = null;
            Texture2D one = null;
            try
            {
                one = new Texture2D(1, 1, page.format, false) { filterMode = FilterMode.Point };
                one.SetPixels32(new[] { c });
                one.Apply(false, false);
                Graphics.CopyTexture(one, 0, 0, 0, 0, 1, 1, page, 0, 0, x, y);
                return true;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }
            finally
            {
                if (one != null) Object.Destroy(one);
            }
        }

        // One texel of a page, read the way ReadFrame reads (a blit to an sRGB render texture, ReadPixels).
        private static Color32? Texel(Texture2D tex, int x, int y)
        {
            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return null;
            RenderTexture prev = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D tmp = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                tmp = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
                return tmp.GetPixels32()[0];
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (tmp != null) Object.Destroy(tmp);
            }
        }

        private static string Hex(Color32 c) => $"{c.r:x2}{c.g:x2}{c.b:x2}";
        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
