using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using TMPro;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Text room (2026-10-31, the translation investigation: docs/localization.md section 4, "Room"): how much longer each
    // text of our windows can get before it no longer fits its box, to set against how much longer translations run (the
    // game's own labels: French 1.25x, German 1.12x at the median). Measures only, from the self-tests (F11) where they
    // already show the options window's sections, the journal and the colours window. Writes BepInEx\GK2-text-room.tsv (one
    // line per text; a window measured again replaces its earlier lines) and an INFO line per window.
    // A text grows by repeating its own words and is laid out by TMP in its own box: one that doesn't wrap fits while it is
    // no wider than its box, one that wraps while its lines are no taller than it. A text that shrinks (auto size) is
    // measured at its full size and at its smallest.
    internal static class TextRoom
    {
        private const float Step = 0.1f, MaxFactor = 3f;
        private static readonly Dictionary<string, List<string>> measured = new Dictionary<string, List<string>>();
        private static readonly List<string> windows = new List<string>();

        // Every shown text under root. Returns each text that shrinks (auto size) with its room at its smallest size, for
        // a check that the long-text rule holds (TranslationTests: under the test language every one fits).
        // shortOnes: when given, every text without room at its smallest size (a shrinking one at its smallest, the rest at
        // their own) is added with its room (under 1: the share that fits) and whether it wraps (TranslationTests.Layout).
        internal static List<(string Text, float Shrunk, bool Wraps)> Measure(Transform root, string where, List<(string Text, float Room, bool Wraps)> shortOnes = null)
        {
            var shrinking = new List<(string Text, float Shrunk, bool Wraps)>();
            if (root == null)
            {
                SelfTest.Info($"text room: {where}: not open, nothing measured");
                return shrinking;
            }
            var lines = new List<string>();
            var room = new List<(float Factor, string Text)>();
            foreach (TextMeshProUGUI t in root.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (string.IsNullOrWhiteSpace(t.text)) continue;
                bool shrinks = t.enableAutoSizing;
                float f = Factor(t, t.text, out float shrunk);
                lines.Add(Line(where, PathOf(t.transform, root), t, t.text, f, shrunk));
                room.Add((f, t.text));
                if (shrinks) shrinking.Add((t.text, shrunk, t.textWrappingMode != TextWrappingModes.NoWrap));
                float best = shrinks ? shrunk : f;
                if (shortOnes != null && best < 0.999f) shortOnes.Add((t.text, best, t.textWrappingMode != TextWrappingModes.NoWrap));
            }
            Store(where, lines, room);
            return shrinking;
        }

        // Texts that take turns in one box (the options window's description line, one row's description at a time).
        internal static void MeasureIn(TextMeshProUGUI box, string where, List<(string Name, string Text)> texts, List<(string Text, float Room, bool Wraps)> shortOnes = null)
        {
            if (box == null)
            {
                SelfTest.Info($"text room: {where}: no box, nothing measured");
                return;
            }
            var lines = new List<string>();
            var room = new List<(float Factor, string Text)>();
            foreach (var x in texts)
            {
                if (string.IsNullOrWhiteSpace(x.Text)) continue;
                float f = Factor(box, x.Text, out float shrunk);
                lines.Add(Line(where, x.Name, box, x.Text, f, shrunk));
                room.Add((f, x.Text));
                float best = box.enableAutoSizing ? shrunk : f;
                if (shortOnes != null && best < 0.999f) shortOnes.Add((x.Name + ": " + x.Text, best, box.textWrappingMode != TextWrappingModes.NoWrap));
            }
            Store(where, lines, room);
        }

        // How many times longer the text can get and still fit; under 1 when it doesn't fit now (the share that fits).
        // shrunk: the same at the smallest size, for a text that shrinks; else the same as the result.
        private static float Factor(TextMeshProUGUI t, string text, out float shrunk)
        {
            bool auto = t.enableAutoSizing;
            float size = t.fontSize;
            try
            {
                t.enableAutoSizing = false; // measured at a size we set, not the one auto size chose for the text shown now
                if (auto) t.fontSize = t.fontSizeMax;
                float full = Fit(t, text);
                shrunk = full;
                if (auto)
                {
                    t.fontSize = t.fontSizeMin;
                    shrunk = Fit(t, text);
                }
                return full;
            }
            finally
            {
                t.fontSize = size;
                t.enableAutoSizing = auto;
            }
        }

        private static float Fit(TextMeshProUGUI t, string text)
        {
            Rect r = t.rectTransform.rect;
            bool wrap = t.textWrappingMode != TextWrappingModes.NoWrap;
            if (!Fits(t, text, r, wrap))
            {
                Vector2 v = wrap ? t.GetPreferredValues(text, r.width, float.PositiveInfinity) : t.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity);
                float need = wrap ? v.y : v.x, have = wrap ? r.height : r.width;
                return need > 0f ? have / need : 0f;
            }
            float ok = 1f;
            for (int i = 1; 1f + i * Step <= MaxFactor + 0.001f; i++)
            {
                float f = 1f + i * Step;
                if (!Fits(t, Grow(text, f), r, wrap)) break;
                ok = f;
            }
            return ok;
        }

        private static bool Fits(TextMeshProUGUI t, string text, Rect r, bool wrap)
        {
            if (!wrap) return t.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x <= r.width + 0.5f;
            float high = t.GetPreferredValues(text, r.width, float.PositiveInfinity).y;
            if (high <= r.height + 0.5f) return true;
            // a text that doesn't wrap at the box's width loses nothing in a label that draws over its edges (the game's
            // prompt: a Japanese, Chinese or Korean font's line is taller than the label a Latin one fits); a label that
            // clips keeps the height
            return !Clips(t) && high <= t.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).y + 0.5f;
        }

        // Whether a label cuts what is past its box: an overflow mode other than Overflow, or a mask above it.
        internal static bool Clips(TextMeshProUGUI t) =>
            t.overflowMode != TextOverflowModes.Overflow || t.GetComponentInParent<UnityEngine.UI.RectMask2D>() != null || t.GetComponentInParent<UnityEngine.UI.Mask>() != null;

        // The text made f times as long by repeating its own words.
        private static string Grow(string text, float f)
        {
            int target = Mathf.RoundToInt(text.Length * f);
            var sb = new StringBuilder(text);
            while (sb.Length < target) sb.Append(' ').Append(text);
            sb.Length = target;
            return sb.ToString();
        }

        private static string Line(string where, string path, TextMeshProUGUI t, string text, float f, float shrunk)
        {
            Rect r = t.rectTransform.rect;
            bool wrap = t.textWrappingMode != TextWrappingModes.NoWrap;
            return string.Join("\t", new[]
            {
                where, path, wrap ? "wrap" : "line", t.enableAutoSizing ? "shrinks" : "fixed",
                (t.enableAutoSizing ? t.fontSizeMax : t.fontSize).ToString("0.#"), r.width.ToString("0"), r.height.ToString("0"),
                f.ToString("0.00"), shrunk.ToString("0.00"), Clean(text),
            });
        }

        private static void Store(string where, List<string> lines, List<(float Factor, string Text)> room)
        {
            if (!measured.ContainsKey(where)) windows.Add(where);
            measured[where] = lines;
            int tight = 0, over = 0;
            float least = float.MaxValue;
            string leastText = "";
            foreach (var x in room)
            {
                if (x.Factor < 1f) over++;
                if (x.Factor < 1.25f) tight++;
                if (x.Factor < least) { least = x.Factor; leastText = x.Text; }
            }
            SelfTest.Info($"text room: {where}: {room.Count} texts, {tight} with less than 1.25x room, {over} already over;" +
                (room.Count > 0 ? $" the least room: \"{Short(leastText)}\" {least:0.00}x" : " none"));
            var sb = new StringBuilder("where\tpath\twrap\tsize mode\tfont size\tbox w\tbox h\tfits up to\tfits up to shrunk\ttext\n");
            foreach (string w in windows)
            {
                foreach (string l in measured[w]) sb.Append(l).Append('\n');
            }
            try
            {
                File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "GK2-text-room.tsv"), sb.ToString());
            }
            catch (System.Exception e)
            {
                SelfTest.Info("text room: could not write GK2-text-room.tsv: " + e.Message);
            }
        }

        // The text's place under the window: its last three objects' names, without the windows' own prefix.
        private static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (Transform p = t; p != null && p != root && parts.Count < 3; p = p.parent) parts.Insert(0, p.name.Replace("GK2DonkeyAPI options ", "").Replace("GK2DonkeyAPI mounts panel ", ""));
            return string.Join(" / ", parts);
        }

        private static string Clean(string s) => s.Replace("\t", " ").Replace("\r", "").Replace("\n", "\\n");

        private static string Short(string s)
        {
            s = Clean(s);
            return s.Length <= 40 ? s : s.Substring(0, 40) + "...";
        }
    }
}
