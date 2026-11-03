using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // The game's donkey art against the art the skins were drawn on (docs/skins-design.md section 11). The start-up
    // check of the game's code (the dependency manifest) sees members, not pictures: an update that redrew a donkey
    // frame under the same name would leave every skin drawn over the donkey off by those pixels, and nothing would say
    // so. Once a session, when the donkey's frames are first in memory (a donkey out in the world), each 0301_ sprite of
    // the character atlases is read back the way the skins' builds read it (Looks.ReadFrame), one a frame, hashed, and
    // compared with donkey-frames.txt (the asset probe's export the skins were drawn on; tools/skins/donkey_frames.py
    // makes it): one info line when every frame is the same, one warning naming the changed, missing and new frames
    // otherwise. It changes nothing: no skin is set aside, no feature stops. Dev builds also write what they read to
    // BepInEx\GK2-donkey-frames\ (its PNGs and its list), for `donkey_frames.py check <that folder>`.
    internal static class DonkeyFrames
    {
        internal enum State { Waiting, Reading, Same, Differs, NoList, NotFound }

        // A frame's fingerprint: its canvas and the FNV-1a 64 of its RGBA bytes, rows from the top, a pixel with alpha
        // 0 as four zeros (donkey_frames.py hashes the probe's PNGs the same way). W -1: the frame could not be read.
        internal struct Print
        {
            internal int W, H;
            internal ulong Hash;
        }

        internal static State Now = State.Waiting;
        internal static string Note = "";   // what the log line said, for the self-test
        internal static readonly List<string> Changed = new List<string>();
        internal static readonly List<string> Missing = new List<string>();
        internal static readonly List<string> New = new List<string>();

        private const string ResourceName = "GK2DonkeyAPI.donkey-frames.txt";
        private const int NamesShown = 8;
        private static Dictionary<string, Print> listed;
        private static readonly Dictionary<string, Sprite> found = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly List<string> toRead = new List<string>();
        private static readonly Dictionary<string, Print> read = new Dictionary<string, Print>(StringComparer.Ordinal);
        private static readonly Stopwatch work = new Stopwatch();
        private static int frames;
        private static string dumpDir;
        private const float SearchEvery = 3f, SearchFor = 30f;
        private static float nextSearch, searchingSince = -1f;

        // Once per frame from the registry's tick (beside Looks.AdvanceBuilds).
        internal static void Tick()
        {
            if (Now == State.Waiting) Begin();
            else if (Now == State.Reading) ReadOne();
        }

        // The donkey's frames in memory: the list of what to read. Looked for only in a loaded game with a view of a mount
        // out (the donkey's atlases come with its prefab, which every mount's view is), at most every 3 s: each search
        // scans every sprite in memory (6 to 7 ms). Not found for 30 s of such views: given up for the session.
        private static void Begin()
        {
            Looks.SkinBase b = Looks.Donkey;
            float now = Time.realtimeSinceStartup;
            if (now < nextSearch) return;
            nextSearch = now + SearchEvery;
            if (!Mount.IsGameReady || !AnyView()) return;
            if (searchingSince < 0f) searchingSince = now;
            if (!Looks.BaseFramesInMemory(b, b.GamePrefix))
            {
                if (now - searchingSince < SearchFor) return;
                Now = State.NotFound;
                Note = $"the donkey's frames were not in memory in {SearchFor:0} s with a mount out: its art is not checked this session";
                Plugin.Log.LogInfo("Looks: " + Note + ".");
                return;
            }
            if (listed == null)
            {
                listed = ReadList();
                if (listed == null)
                {
                    Now = State.NoList;
                    Plugin.Log.LogWarning("Looks: the list of the donkey's frames is not in the DLL (" + ResourceName + ", a build defect): the game's donkey art is not checked.");
                    return;
                }
            }
            found.Clear();
            Looks.GameFramesFound(found);
            toRead.Clear();
            toRead.AddRange(found.Keys);
            toRead.Sort(StringComparer.Ordinal);
            read.Clear();
            work.Reset();
            frames = 0;
            dumpDir = null;
            if (DevTools.Unlocked)
            {
                try
                {
                    dumpDir = Path.Combine(Paths.BepInExRootPath, "GK2-donkey-frames");
                    Directory.CreateDirectory(dumpDir);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Looks: the donkey's frames are checked but not written (dev): " + e.Message);
                    dumpDir = null;
                }
            }
            Now = State.Reading;
        }

        private static bool AnyView()
        {
            foreach (Mount.Instance i in Mount.All)
            {
                if (i.HasView) return true;
            }
            return false;
        }

        private static void ReadOne()
        {
            using (Perf.Measure(Perf.S.LooksBuild))
            {
                string key = toRead[read.Count];
                Sprite s = found[key];
                if (s == null)
                {
                    Now = State.Waiting; // its scene unloaded while it was read: from the start when the frames are back
                    searchingSince = -1f;
                    return;
                }
                work.Start();
                Looks.SkinFrame f = Looks.ReadFrame(key, s, Looks.Donkey);
                read[key] = f == null ? new Print { W = -1 } : Of(f.W, f.H, f.P);
                if (dumpDir != null && f != null) Dump(key, f);
                work.Stop();
                frames++;
                if (read.Count == toRead.Count) Finish();
            }
        }

        private static void Finish()
        {
            Compare(listed, read, Changed, Missing, New);
            string how = $"{read.Count} frame(s) read in {work.ElapsedMilliseconds} ms over {frames} game frames";
            if (dumpDir != null) WriteList(Path.Combine(dumpDir, "donkey-frames.txt"), read);
            if (Changed.Count == 0 && Missing.Count == 0 && New.Count == 0)
            {
                Now = State.Same;
                Note = $"the game's donkey frames are the ones the skins were drawn on (donkey-frames.txt; {how})";
                Plugin.Log.LogInfo("Looks: " + Note + ".");
                return;
            }
            Now = State.Differs;
            var parts = new List<string>();
            if (Changed.Count > 0) parts.Add("changed " + Names(Changed));
            if (Missing.Count > 0) parts.Add("missing " + Names(Missing));
            if (New.Count > 0) parts.Add("new " + Names(New));
            Note = $"the game's donkey frames differ from the ones the skins were drawn on (donkey-frames.txt): {string.Join("; ", parts.ToArray())} ({how})";
            Plugin.Log.LogWarning("Looks: " + Note + ". A skin drawn over the donkey may not line up in those frames until a re-audited build (docs/skins-design.md section 11).");
        }

        // What differs between the list and the frames read: a frame of both whose canvas or pixels differ, a listed one
        // the game no longer has, one the game has that the list doesn't. Each sorted.
        internal static void Compare(Dictionary<string, Print> want, Dictionary<string, Print> have, List<string> changed, List<string> missing, List<string> extra)
        {
            changed.Clear();
            missing.Clear();
            extra.Clear();
            foreach (KeyValuePair<string, Print> kv in want)
            {
                if (!have.TryGetValue(kv.Key, out Print p)) missing.Add(kv.Key);
                else if (p.W != kv.Value.W || p.H != kv.Value.H || p.Hash != kv.Value.Hash) changed.Add(kv.Key);
            }
            foreach (string k in have.Keys)
            {
                if (!want.ContainsKey(k)) extra.Add(k);
            }
            changed.Sort(StringComparer.Ordinal);
            missing.Sort(StringComparer.Ordinal);
            extra.Sort(StringComparer.Ordinal);
        }

        // Unity's pixels run from the bottom row; the fingerprint's rows from the top (as a PNG's).
        internal static Print Of(int w, int h, Color32[] px)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int yTop = 0; yTop < h; yTop++)
                {
                    int row = (h - 1 - yTop) * w;
                    for (int x = 0; x < w; x++)
                    {
                        Color32 c = px[row + x];
                        bool clear = c.a == 0;
                        hash = (hash ^ (clear ? (byte)0 : c.r)) * 1099511628211UL;
                        hash = (hash ^ (clear ? (byte)0 : c.g)) * 1099511628211UL;
                        hash = (hash ^ (clear ? (byte)0 : c.b)) * 1099511628211UL;
                        hash = (hash ^ c.a) * 1099511628211UL;
                    }
                }
                return new Print { W = w, H = h, Hash = hash };
            }
        }

        private static string Names(List<string> keys)
        {
            if (keys.Count <= NamesShown) return string.Join(", ", keys.ToArray());
            return string.Join(", ", keys.GetRange(0, NamesShown).ToArray()) + $" (+{keys.Count - NamesShown} more)";
        }

        private static Dictionary<string, Print> ReadList()
        {
            using (Stream s = typeof(DonkeyFrames).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (s == null) return null;
                using (var r = new StreamReader(s, Encoding.UTF8))
                {
                    return ParseList(r.ReadToEnd());
                }
            }
        }

        // "<key> <width>x<height> <hash, 16 hex digits>" per line; "#" lines are comments.
        internal static Dictionary<string, Print> ParseList(string text)
        {
            var list = new Dictionary<string, Print>(StringComparer.Ordinal);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split(' ');
                int x = f.Length == 3 ? f[1].IndexOf('x') : -1;
                if (x <= 0 || !int.TryParse(f[1].Substring(0, x), out int w) || !int.TryParse(f[1].Substring(x + 1), out int h)
                    || !ulong.TryParse(f[2], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong hash))
                {
                    continue;
                }
                list[f[0]] = new Print { W = w, H = h, Hash = hash };
            }
            return list;
        }

        // ------------------------------------------------------------------ dev only: what was read, for donkey_frames.py check

        private static void Dump(string key, Looks.SkinFrame f)
        {
            Texture2D t = null;
            try
            {
                t = new Texture2D(f.W, f.H, TextureFormat.RGBA32, false);
                t.SetPixels32(f.P);
                t.Apply();
                File.WriteAllBytes(Path.Combine(dumpDir, key + ".png"), t.EncodeToPNG());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Looks: the donkey's frame {key} could not be written (dev): {e.Message}");
                dumpDir = null;
            }
            finally
            {
                if (t != null) Object.Destroy(t);
            }
        }

        private static void WriteList(string path, Dictionary<string, Print> prints)
        {
            try
            {
                var keys = new List<string>(prints.Keys);
                keys.Sort(StringComparer.Ordinal);
                var sb = new StringBuilder("# The donkey's frames as this game run read them (DonkeyFrames.cs, a dev build): compare with\n" +
                    "# python tools/skins/donkey_frames.py check \"" + Path.GetDirectoryName(path) + "\"\n");
                foreach (string k in keys)
                {
                    Print p = prints[k];
                    sb.Append(k).Append(' ').Append(p.W).Append('x').Append(p.H).Append(' ').Append(p.Hash.ToString("x16")).Append('\n');
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Looks: the donkey's frame list could not be written (dev): " + e.Message);
            }
        }
    }
}
