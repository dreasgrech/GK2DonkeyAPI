using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Dev only (the [perf] logging's switch): what one cargo change costs, step by step, one line per change
    // ("[cargo-cost]"). Performance Hog put a player's item at 2-6 ms in Cargo.Rig.OnChanged (2026-11-09,
    // docs/performance.md); this splits it before anything is changed. Nothing allocates unless the line is written.
    internal static class CargoCost
    {
        internal static bool On => DevTools.Unlocked && Perf.LogEvery10s;

        private const int Max = 48;
        private static readonly string[] names = new string[Max];
        private static readonly long[] at = new long[Max];
        private static int n;
        private static long start;
        private static bool running;

        internal static void Begin()
        {
            if (!On) return;
            running = true;
            n = 0;
            start = Stopwatch.GetTimestamp();
        }

        // The step that just ended (from the last mark, or the start).
        internal static void Mark(string step)
        {
            if (!running || n >= Max) return;
            names[n] = step;
            at[n] = Stopwatch.GetTimestamp();
            n++;
        }

        internal static void End(string what, Component donkey, int views)
        {
            if (!running) return;
            running = false;
            long end = Stopwatch.GetTimestamp();
            double ms = 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder();
            sb.Append($"[cargo-cost] {what}: {(end - start) * ms:F2} ms in all;");
            long prev = start;
            for (int i = 0; i < n; i++)
            {
                sb.Append($" {names[i]} {(at[i] - prev) * ms:F2}");
                prev = at[i];
            }
            if (end - prev > 0) sb.Append($", the rest {(end - prev) * ms:F2}");
            if (donkey != null && donkey)
            {
                int all = donkey.GetComponentsInChildren<Transform>(includeInactive: true).Length;
                int active = donkey.GetComponentsInChildren<Transform>(includeInactive: false).Length;
                Transform first = null;
                foreach (Transform t in donkey.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t.name == "cargo_0") { first = t; break; }
                }
                int perView = first != null ? first.GetComponentsInChildren<Transform>(includeInactive: true).Length : 0;
                sb.Append($"; {views} cargo view(s), {perView} transforms in one; under the cart {all} transforms ({active} active)");
            }
            Plugin.Log.LogInfo(sb.ToString());
        }
    }
}
