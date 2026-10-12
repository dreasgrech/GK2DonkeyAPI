using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The real shapes of the cargo size classes (docs/cargo-tuning-tools.md): each class's top and bottom surface as
    // height maps on a 1 cm grid, measured from the meshes by the Debug tab's "Measure the item shapes" (ShapeProbe) and
    // kept in cargo-shapes.cfg (embedded; the repo copy wins on the machine with the sources). Scene-free like
    // CargoPacker, so PackerScan reads the same file. On top of a pack it answers what boxes cannot: how far an item
    // really is above what is under it (Gap: the seat suggestions) and where items cut into each other or float (Scan).
    internal static class CargoShapes
    {
        internal sealed class Shape
        {
            public string Class, Name;
            public Vector3 Box;
            public int Nx, Nz;
            public float X0, Z0, Cell;
            public float[] Top, Bottom; // [z * Nx + x], above the box bottom; NaN: nothing there

            // The cell at a point relative to the footprint centre, or -1.
            public int CellAt(float x, float z)
            {
                // Nearest cell, a point exactly between two cells always to the upper one: places are often whole half
                // centimetres (on the cell boundary), and rounding there flipped with float noise, a whole row of cells
                // in or out between two packs of the same load (accepting a contact moved it 2 mm, 2026-10-12).
                int xi = Mathf.FloorToInt((x - X0) / Cell + 0.5f + 1e-3f), zi = Mathf.FloorToInt((z - Z0) / Cell + 0.5f + 1e-3f);
                return xi < 0 || zi < 0 || xi >= Nx || zi >= Nz ? -1 : zi * Nx + xi;
            }
        }

        private static readonly Dictionary<string, Shape> byClass = new Dictionary<string, Shape>(StringComparer.Ordinal);
        public static string Source { get; private set; } = "";
        public static int Count => byClass.Count;
        public static Shape ByClass(string cls) => cls != null && byClass.TryGetValue(cls, out Shape s) ? s : null;

        // In the game: the repo copy, else the embedded one. Read once at start and after a new measurement.
        public static void Load()
        {
            try
            {
                string repo = Defaults.RepoFile("cargo-shapes.cfg");
                if (repo != null)
                {
                    Parse(File.ReadAllText(repo));
                    Source = repo;
                    return;
                }
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("GK2DonkeyAPI.cargo-shapes.cfg"))
                {
                    if (s != null)
                    {
                        using (var r = new StreamReader(s)) Parse(r.ReadToEnd());
                        Source = "embedded";
                        return;
                    }
                }
                byClass.Clear();
                Source = "(none measured)";
            }
            catch (Exception e)
            {
                byClass.Clear();
                Source = "(read failed: " + e.Message + ")";
            }
        }

        internal static void Parse(string text)
        {
            byClass.Clear();
            Shape cur = null;
            float cell = 0.01f;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line.StartsWith("[class ", StringComparison.Ordinal) && line.EndsWith("]"))
                {
                    cur = new Shape { Class = line.Substring(7, line.Length - 8).Trim(), Cell = cell };
                    byClass[cur.Class] = cur;
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim(), val = line.Substring(eq + 1).Trim();
                if (cur == null)
                {
                    if (key == "cell") cell = F(val);
                    continue;
                }
                if (key == "name") cur.Name = val;
                else if (key == "box") { string[] p = val.Split(' '); cur.Box = new Vector3(F(p[0]), F(p[1]), F(p[2])); }
                else if (key == "grid")
                {
                    string[] p = val.Split(' ');
                    cur.Nx = int.Parse(p[0], CultureInfo.InvariantCulture);
                    cur.Nz = int.Parse(p[1], CultureInfo.InvariantCulture);
                    cur.X0 = F(p[2]);
                    cur.Z0 = F(p[3]);
                    cur.Top = new float[cur.Nx * cur.Nz];
                    cur.Bottom = new float[cur.Nx * cur.Nz];
                }
                else if (key.StartsWith("top_", StringComparison.Ordinal) || key.StartsWith("bottom_", StringComparison.Ordinal))
                {
                    bool top = key[0] == 't';
                    int z = int.Parse(key.Substring(top ? 4 : 7), CultureInfo.InvariantCulture);
                    string[] p = val.Split(' ');
                    float[] into = top ? cur.Top : cur.Bottom;
                    for (int x = 0; x < p.Length && x < cur.Nx; x++) into[z * cur.Nx + x] = p[x] == "-" ? float.NaN : F(p[x]);
                }
            }
        }

        private static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ on top of a pack

        // How far item i's underside is above what lies under it in lower layers (negative: it cuts in), over the cells
        // where something is under it; null when nothing is under it at all or a shape is missing. The seat suggestion is
        // to move it down by this (plus the sink).
        internal static float? GapBelow(CargoPacker.Result r, int i, Func<string, Shape> shapeOf)
        {
            Shape si = shapeOf(r.Id[i]);
            if (si == null) return null;
            float best = float.PositiveInfinity;
            bool any = false;
            for (int k = 0; k < r.Count; k++)
            {
                if (k == i || r.Layer[k] >= r.Layer[i]) continue;
                Shape sk = shapeOf(r.Id[k]);
                if (sk == null) return null;
                for (int c = 0; c < si.Bottom.Length; c++)
                {
                    float b = si.Bottom[c];
                    if (float.IsNaN(b)) continue;
                    float wx = r.Target[i].x + si.X0 + (c % si.Nx) * si.Cell, wz = r.Target[i].z + si.Z0 + (c / si.Nx) * si.Cell;
                    int ck = sk.CellAt(wx - r.Target[k].x, wz - r.Target[k].z);
                    if (ck < 0 || float.IsNaN(sk.Top[ck])) continue;
                    float gap = (r.Target[i].y + b) - (r.Target[k].y + sk.Top[ck]);
                    if (gap < best) best = gap;
                    any = true;
                }
            }
            return any ? best : (float?)null;
        }

        // Every cell's gap under item i (its underside minus the highest top below it there), over the cells where
        // something is under it; sorted, smallest (most sunk) first. A small protrusion (the log's stub) is a few cells
        // at the start: a seat taken at a low percentile ignores it.
        internal static List<float> GapsBelow(CargoPacker.Result r, int i, Func<string, Shape> shapeOf) => GapsBelow(r, i, shapeOf, -1);

        // only: against that one item (the support), or every item in the lower layers (-1).
        internal static List<float> GapsBelow(CargoPacker.Result r, int i, Func<string, Shape> shapeOf, int only)
        {
            var gaps = new List<float>();
            Shape si = shapeOf(r.Id[i]);
            if (si == null) return gaps;
            for (int c = 0; c < si.Bottom.Length; c++)
            {
                float b = si.Bottom[c];
                if (float.IsNaN(b)) continue;
                float wx = r.Target[i].x + si.X0 + (c % si.Nx) * si.Cell, wz = r.Target[i].z + si.Z0 + (c / si.Nx) * si.Cell;
                float topBelow = float.NegativeInfinity;
                for (int k = 0; k < r.Count; k++)
                {
                    if (k == i || r.Layer[k] >= r.Layer[i] || (only >= 0 && k != only)) continue;
                    Shape sk = shapeOf(r.Id[k]);
                    if (sk == null) continue;
                    int ck = sk.CellAt(wx - r.Target[k].x, wz - r.Target[k].z);
                    if (ck < 0 || float.IsNaN(sk.Top[ck])) continue;
                    topBelow = Mathf.Max(topBelow, r.Target[k].y + sk.Top[ck]);
                }
                if (!float.IsNegativeInfinity(topBelow)) gaps.Add(r.Target[i].y + b - topBelow);
            }
            gaps.Sort();
            return gaps;
        }

        // Judged the way the user tunes by eye (calibrated 2026-10-12 on every tuned contact: none of them is flagged): an
        // item floats when even its lowest tenth is more than FloatAt above what is under it (a visible gap all round),
        // and sinks when half of it is more than SinkAt into what is under it. A few centimetres in reads as resting at
        // this camera angle, and a small bump (the log's stub) may poke into the item above.
        internal const float FloatAt = 0.04f, SinkAt = 0.12f, SeatSink = 0.03f;

        internal sealed class Verdict
        {
            public string Kind;   // "floats", "sinks", "hangs" (nothing under it)
            public int Item, Under;
            public float Amount;  // how far it floats or sinks (the tenth / the half)
        }

        internal static Verdict Judge(CargoPacker.Result r, int i, Func<string, Shape> shapeOf)
        {
            if (r.Layer[i] == 0 || shapeOf(r.Id[i]) == null) return null;
            // the fast path of the seat pass (sampled every 2 cm, no allocation): the scan judges every item of 1,715 loads
            var shapes = new Shape[r.Count];
            for (int k = 0; k < r.Count; k++) shapes[k] = shapeOf(r.Id[k]);
            SeatQuantiles(r, i, shapes, -1, 0.10f, out float p10, out _);
            if (float.IsNaN(p10)) return new Verdict { Kind = "hangs", Item = i, Under = r.Partner[i] };
            SeatQuantiles(r, i, shapes, -1, 0.5f, out float p50, out _);
            if (p10 > FloatAt) return new Verdict { Kind = "floats", Item = i, Under = MostUnder(r, i, shapes), Amount = p10 };
            if (p50 < -SinkAt) return new Verdict { Kind = "sinks", Item = i, Under = MostUnder(r, i, shapes), Amount = -p50 };
            return null;
        }

        // The item in a lower layer that most of item i's footprint lies over (a beside item's Partner is its neighbour in
        // its own layer, not what is under it).
        private static int MostUnder(CargoPacker.Result r, int i, Shape[] shapes)
        {
            int best = r.Partner[i];
            float bestArea = 0f;
            Vector3 hi = r.Box[i] * 0.5f;
            for (int k = 0; k < r.Count; k++)
            {
                if (k == i || r.Layer[k] >= r.Layer[i]) continue;
                Vector3 hk = r.Box[k] * 0.5f;
                float ox = Mathf.Min(r.Target[i].x + hi.x, r.Target[k].x + hk.x) - Mathf.Max(r.Target[i].x - hi.x, r.Target[k].x - hk.x);
                float oz = Mathf.Min(r.Target[i].z + hi.z, r.Target[k].z + hk.z) - Mathf.Max(r.Target[i].z - hi.z, r.Target[k].z - hk.z);
                // the nearer layer wins a tie of area (what it rests on, not the bed under that)
                float area = ox > 0f && oz > 0f ? ox * oz + r.Layer[k] * 1e-4f : 0f;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = k;
                }
            }
            return best;
        }

        // How far to move item i up (+) or down (-) so it rests on the bulk of what is under it: its lowest quarter at the
        // usual sink (a bump under a few cells is ignored). NaN when nothing is under it.
        internal static float SeatDelta(CargoPacker.Result r, int i, Func<string, Shape> shapeOf)
        {
            // the seat pass's own measurement (sampled every 2 cm): Seat it on an item the seat pass already rested
            // changes nothing at all, instead of writing a few millimetres
            SeatQuantiles(r, i, shapeOf, -1, 0.25f, out float p25, out _);
            return float.IsNaN(p25) ? float.NaN : -(p25 + SeatSink);
        }

        // ------------------------------------------------------------------ the fast path (the packer's seat pass)
        // The seat pass runs on every pack, every frame of a slider drag: no allocations, the underside sampled every
        // other cell (2 cm), both gap sets (every item below, the support only) in one sweep, percentiles by selection.
        // 1.8 ms and 250 KB per six-item pack before (PackerScan --real timing, 2026-10-12).
        private const int Stride = 2;
        [ThreadStatic] private static float[] bufAll, bufSup;

        // The underside cells sampled for the seat pass: offsets from the footprint centre and heights, made once.
        private sealed class Underside
        {
            public float[] X, Z, Y;
            public int Count;
        }

        private static readonly Dictionary<Shape, Underside> undersides = new Dictionary<Shape, Underside>();

        private static Underside UndersideOf(Shape s)
        {
            if (undersides.TryGetValue(s, out Underside u)) return u;
            var xs = new List<float>();
            var zs = new List<float>();
            var ys = new List<float>();
            for (int zi = 0; zi < s.Nz; zi += Stride)
            {
                for (int xi = 0; xi < s.Nx; xi += Stride)
                {
                    float b = s.Bottom[zi * s.Nx + xi];
                    if (float.IsNaN(b)) continue;
                    xs.Add(s.X0 + xi * s.Cell);
                    zs.Add(s.Z0 + zi * s.Cell);
                    ys.Add(b);
                }
            }
            u = new Underside { X = xs.ToArray(), Z = zs.ToArray(), Y = ys.ToArray(), Count = xs.Count };
            undersides[s] = u;
            return u;
        }

        // The q-quantile of item i's gaps over every lower layer (all) and over the support alone (sup); NaN where no
        // cell has anything under it.
        internal static void SeatQuantiles(CargoPacker.Result r, int i, Func<string, Shape> shapeOf, int support, float q, out float all, out float sup)
        {
            var shapes = new Shape[r.Count];
            for (int k = 0; k < r.Count; k++) shapes[k] = shapeOf(r.Id[k]);
            SeatQuantiles(r, i, shapes, support, q, out all, out sup);
        }

        [ThreadStatic] private static int[] near;

        // shapes: each item's shape, resolved once per pack (a lookup per cell and item was most of the cost).
        internal static void SeatQuantiles(CargoPacker.Result r, int i, Shape[] shapes, int support, float q, out float all, out float sup)
        {
            all = sup = float.NaN;
            Shape si = shapes[i];
            if (si == null) return;
            Underside u = UndersideOf(si);
            // the items in lower layers whose footprint meets this one's (the others cannot be under any of its cells)
            if (near == null || near.Length < r.Count) near = new int[Mathf.Max(r.Count, 16)];
            int nn = 0;
            Vector3 tc = r.Target[i];
            float hx = si.Nx * si.Cell * 0.5f + si.Cell, hz = si.Nz * si.Cell * 0.5f + si.Cell;
            for (int k = 0; k < r.Count; k++)
            {
                if (k == i || r.Layer[k] >= r.Layer[i] || shapes[k] == null) continue;
                Shape sk = shapes[k];
                float kx = sk.Nx * sk.Cell * 0.5f + sk.Cell, kz = sk.Nz * sk.Cell * 0.5f + sk.Cell;
                if (Mathf.Abs(r.Target[k].x - tc.x) > hx + kx || Mathf.Abs(r.Target[k].z - tc.z) > hz + kz) continue;
                near[nn++] = k;
            }
            if (nn == 0) return;
            if (bufAll == null || bufAll.Length < u.Count)
            {
                bufAll = new float[Mathf.Max(u.Count, 4096)];
                bufSup = new float[Mathf.Max(u.Count, 4096)];
            }
            int na = 0, ns = 0;
            Vector3 ti = r.Target[i];
            for (int c = 0; c < u.Count; c++)
            {
                float wx = ti.x + u.X[c], wz = ti.z + u.Z[c];
                float topAll = float.NegativeInfinity, topSup = float.NegativeInfinity;
                for (int m = 0; m < nn; m++)
                {
                    int k = near[m];
                    Shape sk = shapes[k];
                    int ck = sk.CellAt(wx - r.Target[k].x, wz - r.Target[k].z);
                    if (ck < 0) continue;
                    float t = sk.Top[ck];
                    if (float.IsNaN(t)) continue;
                    t += r.Target[k].y;
                    if (t > topAll) topAll = t;
                    if (k == support && t > topSup) topSup = t;
                }
                float bottom = ti.y + u.Y[c];
                if (!float.IsNegativeInfinity(topAll)) bufAll[na++] = bottom - topAll;
                if (!float.IsNegativeInfinity(topSup)) bufSup[ns++] = bottom - topSup;
            }
            if (na > 0) all = Select(bufAll, na, Mathf.Clamp(Mathf.FloorToInt(q * (na - 1)), 0, na - 1));
            if (ns > 0) sup = Select(bufSup, ns, Mathf.Clamp(Mathf.FloorToInt(q * (ns - 1)), 0, ns - 1));
        }

        // The k-th smallest of a[0..n) (reorders it), Hoare's selection.
        private static float Select(float[] a, int n, int k)
        {
            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                float pivot = a[(lo + hi) >> 1];
                int i = lo, j = hi;
                while (i <= j)
                {
                    while (a[i] < pivot) i++;
                    while (a[j] > pivot) j--;
                    if (i <= j)
                    {
                        float t = a[i]; a[i] = a[j]; a[j] = t;
                        i++; j--;
                    }
                }
                if (k <= j) hi = j;
                else if (k >= i) lo = i;
                else break;
            }
            return a[k];
        }

        internal static float Percentile(List<float> sorted, float p) => sorted.Count == 0 ? float.NaN : sorted[Mathf.Clamp(Mathf.FloorToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1)];

        // How deep items i and k overlap where both have material (the larger of their column overlaps), 0 if not.
        internal static float Overlap(CargoPacker.Result r, int i, int k, Func<string, Shape> shapeOf)
        {
            Shape si = shapeOf(r.Id[i]), sk = shapeOf(r.Id[k]);
            if (si == null || sk == null) return 0f;
            float worst = 0f;
            for (int c = 0; c < si.Top.Length; c++)
            {
                float ti = si.Top[c], bi = si.Bottom[c];
                if (float.IsNaN(ti)) continue;
                float wx = r.Target[i].x + si.X0 + (c % si.Nx) * si.Cell, wz = r.Target[i].z + si.Z0 + (c / si.Nx) * si.Cell;
                int ck = sk.CellAt(wx - r.Target[k].x, wz - r.Target[k].z);
                if (ck < 0 || float.IsNaN(sk.Top[ck])) continue;
                float lo = Mathf.Max(r.Target[i].y + bi, r.Target[k].y + sk.Bottom[ck]);
                float hi = Mathf.Min(r.Target[i].y + ti, r.Target[k].y + sk.Top[ck]);
                if (hi - lo > worst) worst = hi - lo;
            }
            return worst;
        }

        internal sealed class Issue
        {
            public string Kind; // "cuts", "floats", "hangs"
            public int Item, Other;
            public float Depth;
        }

        // Everything in a pack that looks wrong by the real shapes: two items cutting into each other deeper than cut,
        // an item on an upper layer floating higher than flt above what is under it, or with nothing under it at all.
        internal static List<Issue> Scan(CargoPacker.Result r, Func<string, Shape> shapeOf, float cut = 0.035f, float flt = 0.03f)
        {
            var list = new List<Issue>();
            for (int i = 0; i < r.Count; i++)
            {
                for (int k = i + 1; k < r.Count; k++)
                {
                    float d = Overlap(r, i, k, shapeOf);
                    if (d > cut) list.Add(new Issue { Kind = "cuts", Item = r.Layer[i] >= r.Layer[k] ? i : k, Other = r.Layer[i] >= r.Layer[k] ? k : i, Depth = d });
                }
                if (r.Layer[i] == 0) continue;
                float? gap = GapBelow(r, i, shapeOf);
                if (gap == null) list.Add(new Issue { Kind = "hangs", Item = i, Other = r.Partner[i], Depth = 0f });
                else if (gap.Value > flt) list.Add(new Issue { Kind = "floats", Item = i, Other = r.Partner[i], Depth = gap.Value });
            }
            return list;
        }
    }
}
