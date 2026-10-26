using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // What a file another mod or a player wrote may hand in (docs/multi-mod-loading.md section 2): numbers that are
    // finite ("NaN" and "Infinity" parse as floats, and Mathf.Clamp passes NaN on), a file name that stays inside its
    // pack's folder, a PNG's size read from its header before anything is decoded.
    // A warning that would repeat every frame (a mod's code failing in a loop the API runs): said once, then at most
    // once a minute with how many times it came meanwhile (docs/multi-mod-loading.md 6).
    internal static class Said
    {
        private static readonly System.Collections.Generic.Dictionary<string, float> next = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly System.Collections.Generic.Dictionary<string, int> more = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);

        internal static void Warn(string key, string text)
        {
            float now = Time.unscaledTime;
            if (next.TryGetValue(key, out float at) && now < at)
            {
                more.TryGetValue(key, out int m);
                more[key] = m + 1;
                return;
            }
            if (more.TryGetValue(key, out int skipped) && skipped > 0) text += $" (and {skipped} more time(s) since it was last said)";
            more[key] = 0;
            next[key] = now + 60f;
            Plugin.Log.LogWarning(text);
        }

        // The mod a delegate belongs to, for a line about it.
        internal static string ModOf(Delegate d) => d?.Method?.DeclaringType?.Assembly.GetName().Name ?? "a mod";
    }

    internal static class Numbers
    {
        // Invariant culture, finite only; f is 0 when false.
        internal static bool TryFloat(string s, out float f)
        {
            if (s != null && float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f) && !float.IsNaN(f) && !float.IsInfinity(f)) return true;
            f = 0f;
            return false;
        }

        internal static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        // v clamped to min..max; fallback when it isn't a number at all.
        internal static float Finite(float v, float min, float max, float fallback) => IsFinite(v) ? Mathf.Clamp(v, min, max) : fallback;

        // A file a pack names (sheet =, portrait =, a frame, a sound), under its folder: null when the name is rooted
        // (C:\..., \\host\share\...: Windows would open a network share for it and hand it the player's login), climbs
        // out with "..", or otherwise ends up outside the folder.
        internal static string InFolder(string folder, string file)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(file)) return null;
            string rel = file.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(rel) || rel.IndexOf(':') >= 0 || rel.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            foreach (string part in rel.Split(Path.DirectorySeparatorChar)) if (part == "..") return null;
            try
            {
                string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(Path.Combine(root, rel));
                return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
            }
            catch
            {
                return null;
            }
        }

        // A PNG's width and height from its IHDR chunk (the 8-byte signature, then the chunk: length, "IHDR", width and
        // height big-endian at 16 and 20); false when it isn't a PNG.
        internal static bool PngSize(byte[] png, out int w, out int h)
        {
            w = h = 0;
            if (png == null || png.Length < 24) return false;
            if (png[0] != 0x89 || png[1] != (byte)'P' || png[2] != (byte)'N' || png[3] != (byte)'G') return false;
            if (png[12] != (byte)'I' || png[13] != (byte)'H' || png[14] != (byte)'D' || png[15] != (byte)'R') return false;
            long lw = ((long)png[16] << 24) | ((long)png[17] << 16) | ((long)png[18] << 8) | png[19];
            long lh = ((long)png[20] << 24) | ((long)png[21] << 16) | ((long)png[22] << 8) | png[23];
            if (lw <= 0 || lh <= 0 || lw > int.MaxValue || lh > int.MaxValue) return false;
            w = (int)lw;
            h = (int)lh;
            return true;
        }

        // A cfg's text from a DLL's resource: UTF-8 without the byte-order mark Notepad may write (it would hide the
        // first section's header).
        internal static string CfgText(byte[] bytes)
        {
            string s = System.Text.Encoding.UTF8.GetString(bytes);
            return s.Length > 0 && s[0] == '\uFEFF' ? s.Substring(1) : s;
        }
    }
}
