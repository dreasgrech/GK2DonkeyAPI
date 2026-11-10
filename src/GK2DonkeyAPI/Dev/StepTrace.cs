using System.Diagnostics;
using System.Text;

namespace GK2DonkeyAPI
{
    // Dev only (the [perf] logging's switch): a one-off piece of work split into its steps, one "[steps]" line each time
    // it runs, each step's time and managed allocation (the journal opening, 2026-11-09, docs/performance.md). Runs don't
    // nest: a Begin while one runs is ignored. Nothing allocates unless the line is written.
    internal static class StepTrace
    {
        internal static bool Forced; // a self-test reads Last without the [perf] logging
        internal static bool On => DevTools.Unlocked && (Perf.LogEvery10s || Forced);
        internal static string Last = ""; // the last line written (the self-tests)
        internal static double LastMs;

        private const int Max = 32;
        private static readonly string[] names = new string[Max];
        private static readonly long[] ticks = new long[Max], bytes = new long[Max];
        private static int n;
        private static string what;
        private static long t0, b0;

        internal static bool Begin(string name) // true: this run started (false: off, or one already running)
        {
            if (!On || what != null) return false;
            what = name;
            n = 0;
            b0 = Perf.AllocatedBytes();
            t0 = Stopwatch.GetTimestamp();
            return true;
        }

        internal static void Mark(string step)
        {
            if (what == null || n >= Max) return;
            names[n] = step;
            ticks[n] = Stopwatch.GetTimestamp();
            bytes[n] = Perf.AllocatedBytes();
            n++;
        }

        // The same, the line written only when the run took at least minMs (a frame's step traced every frame of a pick
        // trace: only the heavy ones said).
        internal static void End(double minMs)
        {
            if (what == null) return;
            if ((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency < minMs)
            {
                what = null;
                return;
            }
            End();
        }

        internal static void End()
        {
            if (what == null) return;
            long t = Stopwatch.GetTimestamp(), b = Perf.AllocatedBytes();
            double ms = 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder();
            LastMs = (t - t0) * ms;
            sb.Append($"[steps] {what}: {LastMs:F2} ms, {(b - b0) / 1024.0:F0} KB;");
            long pt = t0, pb = b0;
            for (int i = 0; i < n; i++)
            {
                sb.Append($" {names[i]} {(ticks[i] - pt) * ms:F2} ({(bytes[i] - pb) / 1024.0:F0} KB);");
                pt = ticks[i];
                pb = bytes[i];
            }
            if (t - pt > 0) sb.Append($" the rest {(t - pt) * ms:F2} ({(b - pb) / 1024.0:F0} KB)");
            what = null;
            Last = sb.ToString();
            Plugin.Log.LogInfo(Last);
        }
    }
}
