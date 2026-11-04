using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // The donkey's eye and nostril pixels (docs/face-pixels.md; the user, 2026-11-04: "there are some pixels around the eye
    // which are affected by the legs and mange group ... it looks sloppy otherwise"). The game draws the eye's line and the
    // nostril in the legs' and the mane's dark (292316, 352e1e), and the colour swap goes by colour, so "Legs and mane"
    // coloured them with the legs. Their places frame by frame (face-pixels.txt, made by tools/skins/donkey/face_pixels.py
    // from rig.json's eye and muzzle points) get colours of their own: the eye's line 292319, in no region (as drawn, the
    // eye's own drawing), the nostril 362d1e, the Muzzle's (Colors.cs). A pixel is changed only where it still holds the game's
    // colour, so a redrawn frame is left alone pixel by pixel and a second pass changes nothing.
    // - The atlas: once the donkey-art check says the frames are the ones the skins were drawn on (DonkeyFrames), one frame
    //   a tick, its pixels copied into the game's character atlas on the GPU (Graphics.CopyTexture: the pages are RGBA32,
    //   uncompressed, point-filtered, one mip, not readable) and read back to verify. Every donkey drawn from the game's
    //   sprites shows them then: the plain donkey, a skin's overlays over it, the foals, the paper donkey, and the game's
    //   own delivery donkey (a step of blue or red off the game's colours: the same to the eye). Again when the frames are
    //   found unloaded and back on other pages.
    // - The frames the skins' builds read (Looks.ReadFrame): repainted from the list as they are read, so a whole frame
    //   built before the atlas was done has them too. The donkey-art check and the exports read the art as drawn (the
    //   list's colours back to the game's).
    internal static class FacePixels
    {
        internal enum State { Waiting, Patching, Done, Off }

        // How a frame of the donkey is read: as the game shows it since the repaint (the skins' builds), as the game draws
        // it (the donkey-art check, the exports: the list's colours back to the game's), or as the atlas holds it now.
        internal enum Read { Repainted, AsDrawn, Atlas }

        internal struct Spot
        {
            internal int X, YTop;      // on the frame's canvas, from the top (as the tool's PNGs)
            internal Color32 From, To; // the game's colour, the API's
            internal bool Eye;         // the eye's line; else the nostril
        }

        internal static State Now = State.Waiting;
        internal static string Note = "";               // what the log line said, for the self-test
        internal static int Patched, Frames, Repatched; // the last pass: pixels copied in, frames done; the session: times the frames were found unloaded
        internal static readonly List<string> Problems = new List<string>(); // frames left alone: a pixel not the game's colour, a copy not read back

        private const string ResourceName = "GK2DonkeyAPI.face-pixels.txt";
        private const float WatchEvery = 3f;
        private static Dictionary<string, List<Spot>> list;
        private static bool listRead;
        private static readonly Dictionary<string, Sprite> found = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly List<string> toPatch = new List<string>();
        private static readonly Dictionary<string, (Sprite S, int Page)> done = new Dictionary<string, (Sprite, int)>(StringComparer.Ordinal);
        private static readonly HashSet<int> pages = new HashSet<int>();
        private static readonly Dictionary<int, Texture2D> one = new Dictionary<int, Texture2D>(); // a 1x1 texture per colour copied in
        private static readonly List<(Spot P, int X, int Y)> copied = new List<(Spot, int, int)>();
        private static readonly Stopwatch work = new Stopwatch();
        private static int next, ticks;
        private static bool verifying;
        private static float nextWatch, nextSearch;

        // Once per frame from the registry's tick, after DonkeyFrames.Tick.
        internal static void Tick()
        {
            if (Now == State.Waiting) Begin();
            else if (Now == State.Patching) PatchStep();
            else if (Now == State.Done) Watch();
        }

        // The pixels of one frame, or null (a frame with none, or the list not in the DLL).
        internal static List<Spot> Spots(string key)
        {
            if (!listRead)
            {
                listRead = true;
                list = ReadList();
            }
            return list != null && list.TryGetValue(key, out List<Spot> s) ? s : null;
        }

        internal static int Listed
        {
            get
            {
                Spots("");
                int n = 0;
                if (list != null) foreach (List<Spot> s in list.Values) n += s.Count;
                return n;
            }
        }

        internal static ICollection<string> ListedFrames
        {
            get
            {
                Spots("");
                return list != null ? (ICollection<string>)list.Keys : new string[0];
            }
        }

        // A frame of the donkey's (its canvas, Unity's rows from the bottom) read one way or the other: a pixel that holds the
        // colour it is changed from gets the other, alpha kept; any other pixel is left as it is.
        internal static void Apply(string key, Color32[] p, int w, int h, Read how) => Apply(Spots(key), p, w, h, how);

        internal static void Apply(List<Spot> spots, Color32[] p, int w, int h, Read how)
        {
            if (how == Read.Atlas || p == null || spots == null) return;
            foreach (Spot s in spots)
            {
                if (s.X < 0 || s.YTop < 0 || s.X >= w || s.YTop >= h) continue;
                int i = (h - 1 - s.YTop) * w + s.X;
                Color32 c = p[i], from = how == Read.Repainted ? s.From : s.To, to = how == Read.Repainted ? s.To : s.From;
                if (c.a != 0 && c.r == from.r && c.g == from.g && c.b == from.b) p[i] = new Color32(to.r, to.g, to.b, c.a);
            }
        }

        // ------------------------------------------------------------------ the atlas

        private static void Begin()
        {
            DonkeyFrames.State check = DonkeyFrames.Now;
            if (check == DonkeyFrames.State.Waiting || check == DonkeyFrames.State.Reading) return;
            if (check == DonkeyFrames.State.NoList || check == DonkeyFrames.State.NotFound)
            {
                Off(false, "the donkey's eye and nostril pixels are not repainted in the game's atlas this session: the donkey-art check did not read the frames (" + check + ")");
                return;
            }
            Spots(""); // the list read
            if (list == null)
            {
                Off(true, "the list of the donkey's eye and nostril pixels is not in the DLL (" + ResourceName + ", a build defect): \"Legs and mane\" colours them with the legs");
                return;
            }
            if (SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None)
            {
                Off(true, $"the GPU ({SystemInfo.graphicsDeviceType}) can't copy into a texture: the donkey's eye and nostril pixels are left as the game draws them, and \"Legs and mane\" colours them with the legs (a skin's whole frames have them)");
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (now < nextSearch) return;
            nextSearch = now + WatchEvery;
            if (!Mount.IsGameReady || !DonkeyFrames.AnyView()) return; // as the check: the frames come with a mount's view, looked for every few seconds
            Looks.SkinBase b = Looks.Donkey;
            if (!Looks.BaseFramesInMemory(b, b.GamePrefix)) return; // searched again at most every 3 s
            found.Clear();
            Looks.GameFramesFound(found);
            toPatch.Clear();
            foreach (string key in list.Keys)
            {
                if (DonkeyFrames.Changed.Contains(key)) continue; // redrawn by a game update: left as the game draws it
                if (!found.TryGetValue(key, out Sprite s)) continue;
                if (s == null)
                {
                    Looks.ForgetGameFrames(); // found before they were unloaded: looked for again (at most every 3 s)
                    return;
                }
                toPatch.Add(key);
            }
            toPatch.Sort(StringComparer.Ordinal);
            next = 0;
            ticks = 0;
            verifying = false;
            work.Reset();
            Now = State.Patching;
        }

        // One tick: a frame's pixels copied in, the next tick read back. A frame whose sprite went (its scene unloaded) starts
        // the search again.
        private static void PatchStep()
        {
            using (Perf.Measure(Perf.S.LooksBuild))
            {
                ticks++;
                if (next >= toPatch.Count)
                {
                    Finish();
                    return;
                }
                string key = toPatch[next];
                Sprite s = found[key];
                if (s == null || s.texture == null)
                {
                    Restart();
                    return;
                }
                work.Start();
                try
                {
                    if (!verifying) CopyFrame(key, s);
                    else VerifyFrame(key, s);
                }
                finally
                {
                    work.Stop();
                }
            }
        }

        private static void CopyFrame(string key, Sprite s)
        {
            Texture2D page = s.texture;
            if (page.format != TextureFormat.RGBA32 || page.mipmapCount != 1)
            {
                Problems.Add($"{key}: its atlas page is {page.format} with {page.mipmapCount} mip(s), not RGBA32 with one");
                next++;
                return;
            }
            Looks.SkinFrame f = Looks.ReadFrame(key, s, Looks.Donkey, Read.Atlas);
            List<Spot> spots = Spots(key);
            copied.Clear();
            if (f == null)
            {
                Problems.Add(key + ": not read");
                next++;
                return;
            }
            // canvas (x, Unity row) to the page's texel: the crop ReadFrame reads starts at the texture rect's corner,
            // rounded (the way its ReadPixels lands: its reads hash the same as the asset probe's, which rounds), and sits
            // on the canvas at the rect offset, floored
            Rect tr = s.textureRect;
            int ox = Mathf.FloorToInt(s.textureRectOffset.x), oy = Mathf.FloorToInt(s.textureRectOffset.y);
            int cx0 = Mathf.RoundToInt(tr.x) - ox, cy0 = Mathf.RoundToInt(tr.y) - oy;
            int w = Mathf.RoundToInt(tr.width), h = Mathf.RoundToInt(tr.height);
            int already = 0, other = 0;
            foreach (Spot p in spots)
            {
                int yU = f.H - 1 - p.YTop;
                if (p.X >= f.W || yU < 0 || p.X - ox < 0 || p.X - ox >= w || yU - oy < 0 || yU - oy >= h)
                {
                    other++;
                    continue;
                }
                Color32 c = f.P[yU * f.W + p.X];
                if (Same(c, p.To)) { already++; continue; }
                if (!Same(c, p.From)) { other++; continue; }
                int tx = cx0 + p.X, ty = cy0 + yU;
                Graphics.CopyTexture(OneOf(p.To, page), 0, 0, 0, 0, 1, 1, page, 0, 0, tx, ty);
                copied.Add((p, tx, ty));
            }
            if (other > 0) Problems.Add($"{key}: {other} of its {spots.Count} pixel(s) not the game's colour there, left alone");
            if (copied.Count == 0)
            {
                if (already > 0) Done(key, s);
                next++;
                return;
            }
            verifying = true;
        }

        // The frame read back: every pixel copied holds its new colour, or each copied pixel gets the game's back and the
        // frame is left as the game draws it.
        private static void VerifyFrame(string key, Sprite s)
        {
            verifying = false;
            Looks.SkinFrame f = Looks.ReadFrame(key, s, Looks.Donkey, Read.Atlas);
            int good = 0;
            if (f != null)
            {
                foreach (var (p, _, _) in copied)
                {
                    if (Same(f.P[(f.H - 1 - p.YTop) * f.W + p.X], p.To)) good++;
                }
            }
            if (good == copied.Count)
            {
                Done(key, s);
                Patched += good;
            }
            else
            {
                foreach ((Spot p, int x, int y) in copied) Graphics.CopyTexture(OneOf(p.From, s.texture), 0, 0, 0, 0, 1, 1, s.texture, 0, 0, x, y);
                Problems.Add($"{key}: {copied.Count - good} of {copied.Count} pixel(s) copied in did not read back: the game's colours put back");
            }
            copied.Clear();
            next++;
        }

        private static void Done(string key, Sprite s)
        {
            done[key] = (s, s.texture.GetInstanceID());
            pages.Add(s.texture.GetInstanceID());
            Frames++;
        }

        private static void Finish()
        {
            foreach (Texture2D t in one.Values) if (t != null) Object.Destroy(t);
            one.Clear();
            Now = State.Done;
            nextWatch = Time.realtimeSinceStartup + WatchEvery;
            string how = $"{work.ElapsedMilliseconds} ms over {ticks} game frames";
            Note = $"the donkey's eye and nostril pixels repainted in the game's atlas: {Patched} pixel(s) on {Frames} of {Count(list)} frame(s), {pages.Count} page(s){(Repatched > 0 ? $", the frames back on new pages {Repatched} time(s)" : "")} ({how})";
            if (Problems.Count == 0) Plugin.Log.LogInfo("Looks: " + Note + ".");
            else Plugin.Log.LogWarning("Looks: " + Note + "; left as the game draws them: " + string.Join("; ", Problems.ToArray()) + " (docs/face-pixels.md).");
        }

        // Every few seconds: the frames repainted still in memory on the pages that were repainted. Gone (the atlas unloaded):
        // looked for again, and repainted on the new pages when they are back.
        private static void Watch()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextWatch) return;
            nextWatch = now + WatchEvery;
            foreach (KeyValuePair<string, (Sprite S, int Page)> kv in done)
            {
                Sprite s = kv.Value.S;
                if (s != null && s.texture != null && s.texture.GetInstanceID() == kv.Value.Page) continue;
                Plugin.Log.LogInfo("Looks: the donkey's frames were unloaded (" + kv.Key + "): their eye and nostril pixels are repainted again when the frames are back.");
                Repatched++;
                Restart();
                return;
            }
        }

        private static void Restart()
        {
            Looks.ForgetGameFrames();
            foreach (Texture2D t in one.Values) if (t != null) Object.Destroy(t);
            one.Clear();
            copied.Clear();
            done.Clear();
            pages.Clear();
            Problems.Clear();
            Patched = 0;
            Frames = 0;
            Now = State.Waiting;
        }

        private static void Off(bool warn, string why)
        {
            Now = State.Off;
            Note = why;
            if (warn) Plugin.Log.LogWarning("Looks: " + why + ".");
            else Plugin.Log.LogInfo("Looks: " + why + ".");
        }

        private static Texture2D OneOf(Color32 c, Texture2D page)
        {
            int k = c.r << 16 | c.g << 8 | c.b;
            if (one.TryGetValue(k, out Texture2D t) && t != null) return t;
            t = new Texture2D(1, 1, page.format, false) { filterMode = FilterMode.Point, name = "gk2donkeyapi face pixel" };
            t.SetPixels32(new[] { c });
            t.Apply(false, false);
            one[k] = t;
            return t;
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        private static int Count(Dictionary<string, List<Spot>> l) => l == null ? 0 : l.Count;

        // ------------------------------------------------------------------ the list

        private static Dictionary<string, List<Spot>> ReadList()
        {
            using (Stream s = typeof(FacePixels).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (s == null) return null;
                using (var r = new StreamReader(s, Encoding.UTF8))
                {
                    return ParseList(r.ReadToEnd());
                }
            }
        }

        // "<key> <x> <y from the top> <eye|nostril> <the game's colour> <the API's>" per line; "#" lines are comments, a
        // line that doesn't read is skipped.
        internal static Dictionary<string, List<Spot>> ParseList(string text)
        {
            var l = new Dictionary<string, List<Spot>>(StringComparer.Ordinal);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split(' ');
                if (f.Length != 6 || !int.TryParse(f[1], out int x) || !int.TryParse(f[2], out int y) || (f[3] != "eye" && f[3] != "nostril")
                    || !Hex(f[4], out Color32 from) || !Hex(f[5], out Color32 to))
                {
                    continue;
                }
                if (!l.TryGetValue(f[0], out List<Spot> spots)) l[f[0]] = spots = new List<Spot>();
                spots.Add(new Spot { X = x, YTop = y, From = from, To = to, Eye = f[3] == "eye" });
            }
            return l;
        }

        private static bool Hex(string h, out Color32 c)
        {
            c = default;
            if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int v)) return false;
            c = new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255);
            return true;
        }
    }
}
