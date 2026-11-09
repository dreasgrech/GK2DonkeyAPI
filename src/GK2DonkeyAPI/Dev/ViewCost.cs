using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GK2DonkeyAPI
{
    // Dev only: what a mount's view costs as it comes and goes, by path and step (the views investigation of
    // 2026-11-09, docs/performance.md): a new view (Wgo.Spawn) or one from the pool, its parts prepared, an idle view
    // frozen, switched off and on again, a view released (parked or destroyed). Each path is a run of steps; each step's
    // time and managed allocation (Perf.AllocatedBytes, exact on this runtime) are summed per path and step. A summary
    // line ([view-cost]) every 10 s while views come and go, with the [perf] logging; the survey self-test reads it too.
    internal static class ViewCost
    {
        internal static bool Forced; // the survey self-test: on without the [perf] logging
        internal static bool On => DevTools.Unlocked && (Perf.LogEvery10s || Forced);

        private sealed class Stat
        {
            public int N;
            public double SumMs, MaxMs;
            public long Bytes;
        }

        private sealed class Path
        {
            public int Runs;
            public double SumMs, MaxMs;
            public long Bytes;
            public readonly List<string> Order = new List<string>();
            public readonly Dictionary<string, Stat> Steps = new Dictionary<string, Stat>();
        }

        private static readonly Dictionary<string, Path> paths = new Dictionary<string, Path>();
        private static readonly List<string> pathOrder = new List<string>();
        private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

        // The run in progress (runs don't nest: a Begin inside one is counted as its own run after it).
        private static string current;
        private static long t0, tLast, b0, bLast;
        private static readonly Stack<(string Path, long T0, long TLast, long B0, long BLast)> outer = new Stack<(string, long, long, long, long)>();

        internal static void Begin(string path)
        {
            if (!On) return;
            if (current != null) outer.Push((current, t0, tLast, b0, bLast));
            current = path;
            t0 = tLast = Stopwatch.GetTimestamp();
            b0 = bLast = Perf.AllocatedBytes();
        }

        // The step that just ended (since the last mark or the run's start).
        internal static void Mark(string step)
        {
            if (current == null) return;
            long t = Stopwatch.GetTimestamp(), b = Perf.AllocatedBytes();
            Path p = PathOf(current);
            if (!p.Steps.TryGetValue(step, out Stat s))
            {
                p.Steps[step] = s = new Stat();
                p.Order.Add(step);
            }
            double ms = (t - tLast) * MsPerTick;
            s.N++;
            s.SumMs += ms;
            if (ms > s.MaxMs) s.MaxMs = ms;
            s.Bytes += b - bLast;
            tLast = t;
            bLast = b;
        }

        internal static void End()
        {
            if (current == null) return;
            long t = Stopwatch.GetTimestamp(), b = Perf.AllocatedBytes();
            if (t - tLast > 0) Mark("the rest");
            Path p = PathOf(current);
            double ms = (t - t0) * MsPerTick;
            p.Runs++;
            p.SumMs += ms;
            if (ms > p.MaxMs) p.MaxMs = ms;
            p.Bytes += b - b0;
            current = null;
            if (outer.Count > 0)
            {
                var o = outer.Pop();
                current = o.Path;
                t0 = o.T0;
                tLast = Stopwatch.GetTimestamp(); // the inner run's time is its own, not the outer step's
                b0 = o.B0;
                bLast = Perf.AllocatedBytes();
            }
        }

        private static Path PathOf(string name)
        {
            if (!paths.TryGetValue(name, out Path p))
            {
                paths[name] = p = new Path();
                pathOrder.Add(name);
            }
            return p;
        }

        internal static bool Any => pathOrder.Count > 0;

        // One line per path: runs, mean and worst ms, KB a run; then each step's mean ms (worst) and KB a run.
        internal static List<string> Report()
        {
            var lines = new List<string>();
            foreach (string name in pathOrder)
            {
                Path p = paths[name];
                if (p.Runs == 0) continue;
                var sb = new StringBuilder();
                sb.Append($"{name}: {p.Runs} run(s), {p.SumMs / p.Runs:F2} ms a run (worst {p.MaxMs:F2}), {p.Bytes / 1024.0 / p.Runs:F1} KB a run;");
                foreach (string step in p.Order)
                {
                    Stat s = p.Steps[step];
                    sb.Append($" {step} {s.SumMs / p.Runs:F2} (worst {s.MaxMs:F2}) {s.Bytes / 1024.0 / p.Runs:F1} KB;");
                }
                lines.Add(sb.ToString());
            }
            return lines;
        }

        internal static void Reset()
        {
            paths.Clear();
            pathOrder.Clear();
            current = null;
            outer.Clear();
        }

        // Every 10 s with the [perf] lines (Perf.BeginFrame), when any view came or went: the summary, then cleared.
        internal static void LogAndReset()
        {
            if (!Any) return;
            foreach (string l in Report()) Plugin.Log.LogInfo("[view-cost] " + l);
            Reset();
        }
    }
}
