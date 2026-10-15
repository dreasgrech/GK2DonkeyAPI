// PackerScan: the cargo packer (CargoPacker.Pack) and the panel's relation edits, run without the game.
//   dotnet run -c Release          (after building src/GK2DonkeyAPI in Release)
// Checks: one and two items reproduce the pre-layer layouts (a reference port of that algorithm below); the panel's
// Tune loop is a fixed point (reading a relation and writing it back moves nothing, for every row of many loads, many
// times); a slider moves exactly what it says and nothing below it; packing is deterministic; forced relations and
// isolation; sanity (no NaN, nothing under the floor, valid partners, contiguous layers).
using GK2DonkeyAPI;
using UnityEngine;

if (args.Length > 0 && args[0] == "--show") { Show.Run(); return 0; }
int pass = 0, fail = 0;
void Check(bool ok, string what)
{
    if (ok) pass++;
    else { fail++; Console.WriteLine("FAIL " + what); }
}
bool Near(Vector3 a, Vector3 b, float eps = 1e-4f) => Mathf.Abs(a.x - b.x) <= eps && Mathf.Abs(a.y - b.y) <= eps && Mathf.Abs(a.z - b.z) <= eps;
string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";

// --cfg <path>: another layout file (a scratch copy for an experiment); the shapes file is looked for next to it
if (args.Contains("--review")) return Review.Run(Array.IndexOf(args, "--cfg") is int cr && cr >= 0 && cr + 1 < args.Length ? args[cr + 1] : Path.Combine("..", "..", "src", "GK2DonkeyAPI", "cargo-layout.cfg"));
if (args.Contains("--real")) return Real.Run(Array.IndexOf(args, "--cfg") is int ca && ca >= 0 && ca + 1 < args.Length ? args[ca + 1] : Path.Combine("..", "..", "src", "GK2DonkeyAPI", "cargo-layout.cfg"), args.Contains("--verbose"));

// The size classes (tools/cargo-sheet/out/shapes.html): x along, y up, z across.
var boxes = new Dictionary<string, Vector3>
{
    ["wood"] = new Vector3(0.82f, 0.57f, 0.35f),
    ["box"] = new Vector3(0.52f, 0.40f, 0.50f),
    ["slab"] = new Vector3(0.74f, 0.33f, 0.35f),
    ["sack"] = new Vector3(0.52f, 0.58f, 0.49f),
    ["body"] = new Vector3(0.84f, 0.33f, 0.43f),
    ["zombie"] = new Vector3(0.98f, 0.32f, 0.60f),
};
var items = new Dictionary<string, CargoLayout.ItemEntry>();
var pairs = new List<CargoLayout.PairEntry>();
CargoLayout.PairEntry FindPair(string a, string b) => pairs.Find(p => (p.A == a && p.B == b) || (p.A == b && p.B == a));
CargoLayout.PairEntry EditPair(string a, string b)
{
    var p = FindPair(a, b);
    if (p == null) { p = new CargoLayout.PairEntry { A = a, B = b }; pairs.Add(p); }
    return p;
}
CargoLayout.ItemEntry EditItem(string id)
{
    if (!items.TryGetValue(id, out var e)) { e = new CargoLayout.ItemEntry { Id = id }; items[id] = e; }
    return e;
}
bool FirstIsA(CargoLayout.PairEntry p, string first, string second) => p.A == first && p.B == second || !(p.A == second && p.B == first);
var L = new CargoPacker.Lookup
{
    ForItem = id => items.TryGetValue(id, out var e) ? e : null,
    ForPair = FindPair,
    FirstIsA = FirstIsA,
    SameClass = (a, b) => a == b,
    BoxOf = id => boxes[id],
};
// --shapes: every check below again with the seat pass on, each test class a solid box as its measured shape (top
// flat at its height, bottom flat at 0): the seat pass must keep every fixed point, slider delta and shape check.
if (args.Contains("--shapes"))
{
    var synth = new Dictionary<string, CargoShapes.Shape>();
    foreach (var kv in boxes)
    {
        int nx = Mathf.CeilToInt(kv.Value.x / 0.01f) + 1, nz = Mathf.CeilToInt(kv.Value.z / 0.01f) + 1;
        var sh = new CargoShapes.Shape { Class = kv.Key, Name = kv.Key, Box = kv.Value, Nx = nx, Nz = nz, Cell = 0.01f, X0 = -(nx - 1) * 0.005f, Z0 = -(nz - 1) * 0.005f, Top = new float[nx * nz], Bottom = new float[nx * nz] };
        for (int c = 0; c < sh.Top.Length; c++) { sh.Top[c] = kv.Value.y; sh.Bottom[c] = 0f; }
        synth[kv.Key] = sh;
    }
    L.ShapeOf = id => synth.TryGetValue(id, out var sh) ? sh : null;
    Console.WriteLine("INFO the seat pass is on (synthetic box shapes)");
}
CargoPacker.Settings S(string forced = null) => new CargoPacker.Settings { Floor = -0.26f, Gap = 0.02f, Overhang = 0.12f, Nestle = 0.03f, LayerShift = -0.03f, BedWidth = 0.75f, BedLength = 0.90f, Forced = forced };
List<CargoPacker.Item> Load(params string[] ids) => ids.Select((id, k) => new CargoPacker.Item { Id = id, Box = boxes[id], LoadOrder = k }).ToList();
void ResetLayout()
{
    items.Clear();
    pairs.Clear();
    items["wood"] = new CargoLayout.ItemEntry { Id = "wood", Base = new Vector3(-0.02f, -0.09f, 0.05f), HasAlone = true, Alone = new Vector3(-0.02f, -0.2f, -0.03f) };
    items["sack"] = new CargoLayout.ItemEntry { Id = "sack", Base = new Vector3(0.01f, 0f, 0f), Top = new Vector3(0f, -0.22f, 0f) };
    items["slab"] = new CargoLayout.ItemEntry { Id = "slab", Top = new Vector3(0f, -0.14f, 0f), HasAlone = true, Alone = new Vector3(0f, -0.13f, -0.02f) };
    items["body"] = new CargoLayout.ItemEntry { Id = "body", Base = new Vector3(0f, 0.03f, 0.03f), HasAlone = true, Alone = new Vector3(0f, -0.02f, 0f) };
    pairs.Add(new CargoLayout.PairEntry { A = "wood", B = "wood", Mode = "side" });
    pairs.Add(new CargoLayout.PairEntry { A = "wood", B = "slab", Mode = "stack" });
    pairs.Add(new CargoLayout.PairEntry { A = "wood", B = "sack", Mode = "stack" });
    pairs.Add(new CargoLayout.PairEntry { A = "box", B = "box", Mode = "along", BesideTuned = true, BesideA = new Vector3(0.26f, 0f, 0f), BesideB = new Vector3(-0.23f, 0f, 0f) });
    pairs.Add(new CargoLayout.PairEntry { A = "body", B = "body", Mode = "stack", TopTuned = true, TopA = new Vector3(0f, 0.02f, 0.03f), TopB = new Vector3(0f, 0.24f, 0.03f) });
    pairs.Add(new CargoLayout.PairEntry { A = "body", B = "wood", Mode = "side", BesideTuned = true, BesideA = new Vector3(0f, 0.11f, 0.13f), BesideB = new Vector3(-0.02f, -0.09f, -0.13f) });
    pairs.Add(new CargoLayout.PairEntry { A = "zombie", B = "zombie", Mode = "stack", TopTuned = true, TopA = new Vector3(0.01f, -0.02f, 0f), TopB = new Vector3(0.07f, 0.25f, 0f) });
}

// ---------------------------------------------------------------- the pre-layer algorithm for one and two items (reference)
Vector3[] OldTwo(List<CargoPacker.Item> input, CargoPacker.Settings s, out string mode, out bool tuned)
{
    var d = input.OrderByDescending(x => Mathf.RoundToInt(x.Box.x * x.Box.z * 10000f)).ThenBy(x => x.Id, StringComparer.Ordinal).ThenBy(x => x.LoadOrder).ToList();
    var t = new Vector3[d.Count];
    float floor = s.Floor;
    var e0 = L.ForItem(d[0].Id);
    bool aloneOwn = d.Count == 1 && e0 != null && e0.HasAlone;
    t[0] = new Vector3(0f, floor, 0f) + (e0 == null ? Vector3.zero : aloneOwn ? e0.Alone : e0.Base);
    mode = ""; tuned = false;
    if (d.Count == 1) return t;
    var e = L.ForItem(d[1].Id);
    var pair = L.ForPair(d[0].Id, d[1].Id);
    string m = pair != null ? pair.Mode : "auto";
    if (s.Forced == "stack") m = "stack"; else if (s.Forced == "beside" && m == "stack") m = "auto";
    Vector3 ca = boxes[d[0].Id], cb = boxes[d[1].Id];
    float w0 = d[0].Box.z, w1 = d[1].Box.z, l0 = d[0].Box.x, l1 = d[1].Box.x, gap = s.Gap, over = s.Overhang;
    bool side = m == "side" || (m == "auto" && CargoPacker.Cm(ca.z + gap + cb.z) < CargoPacker.Cm(s.BedWidth + over));
    bool along = !side && (m == "along" || (m == "auto" && CargoPacker.Cm(ca.x + gap + cb.x) < CargoPacker.Cm(s.BedLength + over)));
    mode = side ? "side" : along ? "along" : "stack";
    if (side)
    {
        float total = w0 + gap + w1;
        t[0].z = total * 0.5f - w0 * 0.5f + (e0 != null ? e0.Base.z : 0f);
        t[1] = new Vector3(0f, floor, -total * 0.5f + w1 * 0.5f) + (e != null ? e.Base : Vector3.zero);
    }
    else if (along)
    {
        float total = l0 + gap + l1;
        t[0].x = total * 0.5f - l0 * 0.5f + (e0 != null ? e0.Base.x : 0f);
        t[1] = new Vector3(-total * 0.5f + l1 * 0.5f, floor, 0f) + (e != null ? e.Base : Vector3.zero);
    }
    else
    {
        float prevTop = t[0].y + d[0].Box.y;
        t[1] = new Vector3(t[0].x, prevTop - s.Nestle, t[0].z) + (e != null ? e.Top : Vector3.zero);
    }
    // the tuned set of the arrangement shown (the old file held one set, the one of the pair's mode)
    if (pair != null && pair.TunedFor(mode))
    {
        bool firstIsA = L.FirstIsA(pair, d[0].Id, d[1].Id);
        Vector3 pa = pair.PosAFor(mode), pb = pair.PosBFor(mode);
        t[0] = (firstIsA ? pa : pb) + new Vector3(0f, floor, 0f);
        t[1] = (firstIsA ? pb : pa) + new Vector3(0f, floor, 0f);
        tuned = true;
    }
    return t;
}

// ---------------------------------------------------------------- 1. one and two items as before
ResetLayout();
string[] classes = boxes.Keys.ToArray();
foreach (string a in classes)
{
    var r1 = CargoPacker.Pack(Load(a), S(), L);
    var o1 = OldTwo(Load(a), S(), out _, out _);
    Check(Near(r1.Target[0], o1[0]), $"one {a}: {V(r1.Target[0])} vs old {V(o1[0])}");
    Check(r1.Relation[0] == "bed" && r1.Layer[0] == 0 && !r1.OnTop[0], $"one {a}: bed");
}
var rnd = new System.Random(7);
int twoCases = 0;
foreach (string a in classes)
{
    foreach (string b in classes)
    {
        foreach (string mode in new[] { "auto", "side", "along", "stack" })
        {
            foreach (int variant in new[] { 0, 1, 2 }) // 0 untuned, 1 beside tuned, 2 on-top tuned
            {
                ResetLayout();
                var p = EditPair(a, b);
                p.Mode = mode;
                p.BesideTuned = p.TopTuned = false;
                if (variant == 1) p.SetFor("side", new Vector3(R(), R(), R()), new Vector3(R(), R(), R()));
                if (variant == 2) p.SetFor("stack", new Vector3(R(), R(), R()), new Vector3(R(), R() + 0.4f, R()));
                foreach (string forced in new string[] { null, "beside", "stack" })
                {
                    var load = Load(a, b);
                    var r = CargoPacker.Pack(load, S(forced), L);
                    var o = OldTwo(load, S(forced), out string om, out bool ot);
                    twoCases++;
                    Check(Near(r.Target[0], o[0]) && Near(r.Target[1], o[1]), $"two {a}+{b} mode {mode} variant {variant} forced {forced ?? "-"}: {V(r.Target[0])} {V(r.Target[1])} vs old {V(o[0])} {V(o[1])} ({r.Note})");
                    Check(r.PairMode == om && r.PairTuned == ot, $"two {a}+{b} mode {mode} variant {variant} forced {forced ?? "-"}: pair mode {r.PairMode}/{r.PairTuned} vs old {om}/{ot}");
                    Check(r.Relation[1] == (om == "stack" ? "on" : om) && r.Partner[1] == 0, $"two {a}+{b}: relation {r.Relation[1]} partner {r.Partner[1]} (old {om})");
                }
            }
        }
    }
}
Console.WriteLine($"INFO {twoCases} two-item cases against the pre-layer algorithm");
float R() => (float)(rnd.NextDouble() * 0.4 - 0.2);
int Area(string id) => Mathf.RoundToInt(boxes[id].x * boxes[id].z * 10000f);
string Lower(string a, string b) => Area(a) != Area(b) ? (Area(a) > Area(b) ? a : b) : (string.CompareOrdinal(a, b) <= 0 ? a : b);
// The load's shape as the Cargo tab draws it (which boxes, in which order): order, relation, partner and layer of every
// item. A slider in 'Tune this load' must never change it, or the boxes above the dragged slider come and go and the
// drag lands on another slider (IMGUI knows a control by its order; 2026-10-10, video 18-19-36).
string Shape(CargoPacker.Result r) => string.Join(";", Enumerable.Range(0, r.Count).Select(j => $"{r.Order[j]}/{r.Relation[j]}/{r.Partner[j]}/{r.Layer[j]}"));
string Key(CargoPacker.Result r, int j) => r.Relation[j] == "bed" ? "bed" : (r.Relation[j] == "on" ? "on:" : "beside:") + Lower(r.Id[r.Partner[j]], r.Id[j]) + "+" + (Lower(r.Id[r.Partner[j]], r.Id[j]) == r.Id[r.Partner[j]] ? r.Id[j] : r.Id[r.Partner[j]]);
Vector3 OnRel(CargoPacker.Result r, int j) => r.Rel(j); // the stored relation (the shelf lift taken off)

// ---------------------------------------------------------------- 2. the panel's edit loop is a fixed point, and a slider moves what it says
var loads = new List<string[]>
{
    new[] { "wood", "wood", "wood", "wood", "wood", "wood" },
    new[] { "wood", "wood", "wood" },
    new[] { "box", "box", "box", "box" },
    new[] { "wood", "wood", "box", "box" },
    new[] { "wood", "slab", "box" },
    new[] { "zombie", "zombie", "wood" },
    new[] { "body", "body", "wood", "wood" },
    new[] { "sack", "box", "wood", "slab", "body", "box" },
};
for (int k = 0; k < 30; k++)
{
    int n = 3 + rnd.Next(4);
    loads.Add(Enumerable.Range(0, n).Select(_ => classes[rnd.Next(classes.Length)]).ToArray());
}
int loops = 0;
foreach (string[] ids in loads)
{
    string name = string.Join("+", ids);
    foreach (int tunedStart in new[] { 0, 1 })
    {
        ResetLayout();
        if (tunedStart == 1)
        {
            // every pair present pre-tuned at random, both relations; A is the class the packer puts first (the
            // lower / first item), as the panel creates them
            foreach (string a in ids) foreach (string b in ids)
            {
                string lo = Lower(a, b), hi = lo == a ? b : a;
                var p = EditPair(lo, hi);
                if (!p.BesideTuned) p.SetFor("side", new Vector3(R(), 0f, R()), new Vector3(R(), 0f, R()));
                if (!p.TopTuned) p.SetFor("stack", new Vector3(R(), 0f, R()), new Vector3(R(), 0.3f + R(), R()));
            }
        }
        var load = Load(ids);
        var r0 = CargoPacker.Pack(load, S(), L);
        var again = CargoPacker.Pack(load, S(), L);
        Check(Enumerable.Range(0, r0.Count).All(i => Near(r0.Target[i], again.Target[i], 0f)), $"{name}: deterministic");
        // sanity
        for (int i = 0; i < r0.Count; i++)
        {
            Check(!float.IsNaN(r0.Target[i].x + r0.Target[i].y + r0.Target[i].z), $"{name}: NaN at {i}");
            Check(r0.Target[i].y >= r0.Floor - 0.35f, $"{name}: item {i} ({r0.Id[i]}) under the floor: {r0.Target[i].y:0.00}");
            Check(r0.Relation[i] == "bed" ? r0.Partner[i] == -1 : r0.Partner[i] >= 0 && r0.Partner[i] < i, $"{name}: partner of {i} is {r0.Partner[i]} ({r0.Relation[i]})");
            if (i > 0) Check(r0.Layer[i] == r0.Layer[i - 1] || r0.Layer[i] == r0.Layer[i - 1] + 1, $"{name}: layers not contiguous at {i}");
            if (r0.Relation[i] == "on") Check(r0.Layer[r0.Partner[i]] == r0.Layer[i] - 1, $"{name}: support of {i} not in the layer below");
            if (r0.Relation[i] == "side" || r0.Relation[i] == "along") Check(r0.Layer[r0.Partner[i]] == r0.Layer[i] && r0.Partner[i] == i - 1, $"{name}: beside partner of {i} is {r0.Partner[i]}");
        }
        // every relation row: read, freeze, write back unchanged -> nothing moves; then a slider delta moves exactly that
        for (int i = 1; i < r0.Count; i++)
        {
            if (r0.Relation[i] == "bed") continue;
            var before = CargoPacker.Pack(load, S(), L);
            int q = before.Partner[i];
            var pair = EditPair(before.Id[q], before.Id[i]);
            bool qIsA = FirstIsA(pair, before.Id[q], before.Id[i]);
            bool onTop = before.Relation[i] == "on";
            for (int rep = 0; rep < 5; rep++)
            {
                var cur = CargoPacker.Pack(load, S(), L);
                if (onTop)
                {
                    Vector3 v = CargoPacker.OnTopShown(cur, i, L, pair);
                    if (!pair.TopTuned)
                    {
                        var eq = L.ForItem(cur.Id[q]);
                        CargoPacker.FreezeOnTop(pair, qIsA, eq != null ? eq.Base : Vector3.zero, v);
                        CargoPacker.FreezeLayerArrangement(cur, i, L, EditPair); // as the tab does
                    }
                    CargoPacker.WriteOnTop(pair, qIsA, v);
                }
                else
                {
                    if (!pair.BesideTuned) CargoPacker.FreezeBeside(pair, qIsA, cur.BesideStored(q), cur.BesideStored(i), cur.Relation[i]);
                    CargoPacker.WriteBeside(pair, qIsA, cur.BesideStored(q));
                    CargoPacker.WriteBeside(pair, !qIsA, cur.BesideStored(i));
                }
                var after = CargoPacker.Pack(load, S(), L);
                loops++;
                bool same = after.Count == before.Count && Enumerable.Range(0, after.Count).All(j => Near(after.Target[j], before.Target[j]) && after.Relation[j] == before.Relation[j] && after.Partner[j] == before.Partner[j]);
                Check(same, $"{name}: row {i} ({before.Relation[i]} {before.Id[i]}/{before.Id[q]}, rep {rep}) moved on a write-back: " + string.Join(" ", Enumerable.Range(0, after.Count).Where(j => !Near(after.Target[j], before.Target[j])).Select(j => $"{j}:{V(before.Target[j])}->{V(after.Target[j])}")));
                if (!same) break;
            }
            // any slider of this row, anywhere in its range (the tab's ranges), keeps the load's shape
            {
                var shapeBase = CargoPacker.Pack(load, S(), L);
                string shape0 = Shape(shapeBase);
                foreach (var d in new[] { new Vector3(-0.6f, -0.3f, -0.5f), new Vector3(0.6f, 1.0f, 0.5f), new Vector3(-0.6f, 1.0f, 0.5f), new Vector3(0.6f, -0.3f, -0.5f), new Vector3(R(), R(), R()) })
                {
                    if (onTop)
                    {
                        Vector3 keep = CargoPacker.OnTopShown(shapeBase, i, L, pair);
                        CargoPacker.WriteOnTop(pair, qIsA, d);
                        string sh = Shape(CargoPacker.Pack(load, S(), L));
                        Check(sh == shape0, $"{name}: on-top row {i} set to {V(d)} changed the load's shape: {shape0} -> {sh}");
                        CargoPacker.WriteOnTop(pair, qIsA, keep);
                    }
                    else
                    {
                        foreach (bool aSide in new[] { qIsA, !qIsA })
                        {
                            Vector3 keep = aSide == qIsA ? shapeBase.BesideStored(q) : shapeBase.BesideStored(i);
                            CargoPacker.WriteBeside(pair, aSide, d);
                            string sh = Shape(CargoPacker.Pack(load, S(), L));
                            Check(sh == shape0, $"{name}: beside row {i} item {(aSide ? "A" : "B")} set to {V(d)} changed the load's shape: {shape0} -> {sh}");
                            CargoPacker.WriteBeside(pair, aSide, keep);
                        }
                    }
                    // the lone-on-a-pair offset of every class in the load
                    foreach (string c in ids.Distinct())
                    {
                        var e = EditItem(c);
                        Vector3 keepE = e.OnPair;
                        e.OnPair = new Vector3(Mathf.Clamp(d.x, -0.4f, 0.4f), Mathf.Clamp(d.y, -0.4f, 0.4f), Mathf.Clamp(d.z, -0.4f, 0.4f));
                        string sh = Shape(CargoPacker.Pack(load, S(), L));
                        Check(sh == shape0, $"{name}: {c} alone-on-a-pair set to {V(e.OnPair)} changed the load's shape");
                        e.OnPair = keepE;
                    }
                }
                Check(Shape(CargoPacker.Pack(load, S(), L)) == shape0, $"{name}: row {i}: shape not back after the range sweep");
            }
            // a slider delta: the relation moves by exactly that wherever it occurs (every layer of the same pair), and
            // no other relation changes (relative quantities: an on-top relation is its layer origin relative to the
            // support, a beside relation is the item's place in its layer)
            var basePack = CargoPacker.Pack(load, S(), L);
            var delta = new Vector3(0.05f, 0.07f, -0.04f);
            string key = Key(basePack, i);
            if (onTop)
            {
                CargoPacker.WriteOnTop(pair, qIsA, CargoPacker.OnTopShown(basePack, i, L, pair) + delta);
                var moved = CargoPacker.Pack(load, S(), L);
                for (int j = 1; j < moved.Count; j++)
                {
                    if (moved.Relation[j] == "on")
                    {
                        // an untuned on-top relation is derived from the layer below (its shelf and middle) and may follow; a tuned one is fixed
                        if (!basePack.Tuned[j]) continue;
                        Vector3 want = Key(basePack, j) == key ? delta : Vector3.zero;
                        Check(Near(OnRel(moved, j) - OnRel(basePack, j), want), $"{name}: on-top row {i} moved: tuned on-top relation of {j} ({basePack.Id[j]} on {basePack.Id[basePack.Partner[j]]}) changed by {V(OnRel(moved, j) - OnRel(basePack, j))}, wanted {V(want)}");
                    }
                    else
                    {
                        // with the seat pass an untuned item rests on what is under it, so it may follow (as untuned on-top rows do)
                        if (L.ShapeOf != null && !basePack.Tuned[j]) continue;
                        Check(Near(moved.LayerPos(j), basePack.LayerPos(j)), $"{name}: on-top row {i} moved: beside item {j} changed its place in its layer");
                    }
                }
                CargoPacker.WriteOnTop(pair, qIsA, CargoPacker.OnTopShown(basePack, i, L, pair)); // back
            }
            else
            {
                CargoPacker.WriteBeside(pair, !qIsA, basePack.BesideStored(i) + delta);
                var moved = CargoPacker.Pack(load, S(), L);
                for (int j = 1; j < moved.Count; j++)
                {
                    if (moved.Relation[j] == "on")
                    {
                        if (basePack.Tuned[j]) Check(Near(OnRel(moved, j), OnRel(basePack, j)), $"{name}: beside row {i} moved: tuned on-top relation of {j} changed");
                    }
                    else
                    {
                        if (L.ShapeOf != null && !basePack.Tuned[j]) continue; // an untuned item seated on what is under it may follow
                        Vector3 want = Key(basePack, j) == key ? delta : Vector3.zero;
                        Check(Near(moved.LayerPos(j) - basePack.LayerPos(j), want), $"{name}: beside row {i} moved: beside item {j} ({basePack.Id[j]}) changed by {V(moved.LayerPos(j) - basePack.LayerPos(j))}, wanted {V(want)}");
                        Check(Near(moved.LayerPos(moved.Partner[j]), basePack.LayerPos(basePack.Partner[j])), $"{name}: beside row {i} moved: the first item of layer of {j} moved");
                    }
                }
                CargoPacker.WriteBeside(pair, !qIsA, basePack.BesideStored(i)); // back
            }
            var restored = CargoPacker.Pack(load, S(), L);
            Check(Enumerable.Range(0, restored.Count).All(j => Near(restored.Target[j], basePack.Target[j])), $"{name}: row {i}: not back after undoing the delta");
        }
    }
}
Console.WriteLine($"INFO {loads.Count} loads, {loops} write-back loops");

// ---------------------------------------------------------------- 3. the same relation everywhere it occurs
{
    ResetLayout();
    var load = Load("wood", "wood", "wood", "wood", "wood", "wood");
    var r = CargoPacker.Pack(load, S(), L);
    var pair = EditPair("wood", "wood");
    int firstOn = Enumerable.Range(0, r.Count).First(i => r.Relation[i] == "on");
    Vector3 v = CargoPacker.OnTopShown(r, firstOn, L, pair);
    CargoPacker.FreezeOnTop(pair, true, items["wood"].Base, v);
    CargoPacker.WriteOnTop(pair, true, v + new Vector3(0f, -0.1f, 0f));
    var m = CargoPacker.Pack(load, S(), L);
    var ons = Enumerable.Range(0, m.Count).Where(i => m.Relation[i] == "on").ToList();
    Check(ons.Count == 2 && ons.All(i => Mathf.Abs((m.Origin[i].y - m.Target[m.Partner[i]].y) - (v.y - 0.1f)) < 1e-4f), "six logs: both on-top rows follow one tuning");
    // the top LAYER (its origin) comes down twice the delta; with the seat pass on, its second log, still untuned beside,
    // rests on its own and the panel freezes it with the layer on the first slider move (FreezeLayerArrangement)
    Check(Mathf.Abs((m.Origin[5].y - r.Origin[5].y) - (-0.2f)) < 1e-4f, $"six logs: the top layer came down twice the delta ({m.Origin[5].y - r.Origin[5].y:0.000})");
}

// ---------------------------------------------------------------- 4. forced relations
{
    ResetLayout();
    var r = CargoPacker.Pack(Load("wood", "wood"), S("stack"), L);
    Check(r.PairMode == "stack" && r.Relation[1] == "on", "forced stack: two logs stacked");
    r = CargoPacker.Pack(Load("wood", "slab"), S("beside"), L);
    Check(r.PairMode != "stack", $"forced beside on a stack-mode pair that fits: {r.PairMode}");
    r = CargoPacker.Pack(Load("zombie", "zombie"), S("beside"), L);
    Check(r.PairMode == "stack", "forced beside on a pair that fits nowhere: still stacked");
    r = CargoPacker.Pack(Load("box", "slab"), S(), L);
    Check(r.PairMode == "stack", $"crate + slab (0.50+0.02+0.35 = 0.87 against 0.87): no room, stacked ({r.PairMode})");
}

Mixed.Run((ok, what) => { Check(ok, what); return ok; });
{
    // the shelf lift: box on body tuned on a lone body (a two-item stack), then met where the body has a taller log
    // beside it: the box must sit as high above the log's top as it sat above the lone body's, not cut into the log
    ResetLayout();
    var pair = EditPair("body", "box");
    var two = CargoPacker.Pack(Load("body", "box"), S("stack"), L);
    bool bodyIsA = FirstIsA(pair, two.Id[0], two.Id[1]);
    var eb = L.ForItem("body");
    CargoPacker.FreezeOnTop(pair, bodyIsA, eb != null ? eb.Base : Vector3.zero, CargoPacker.OnTopShown(two, 1, L, pair));
    var twoT = CargoPacker.Pack(Load("body", "box"), S("stack"), L);
    Check(Near(twoT.Target[1], two.Target[1]) && twoT.Lift[1] == 0f, "shelf lift: a lone support is not lifted, and freezing moved nothing");
    float aboveBody = twoT.Target[1].y - (twoT.Target[0].y + twoT.Box[0].y);
    var three = CargoPacker.Pack(Load("body", "wood", "box"), S(), L);
    int bi = Array.IndexOf(three.Id, "box"), wi = Array.IndexOf(three.Id, "wood");
    if (three.Relation[bi] == "on" && three.Layer[wi] == three.Layer[three.Partner[bi]])
    {
        float shelf = Enumerable.Range(0, three.Count).Where(k => three.Layer[k] == three.Layer[bi] - 1).Max(k => three.Target[k].y + three.Box[k].y);
        Check(three.Tuned[bi] && three.Lift[bi] > 0.01f && Mathf.Abs((three.Target[bi].y - shelf) - aboveBody) < 1e-4f, $"shelf lift: the box sits {three.Target[bi].y - shelf:0.000} above the taller log, as {aboveBody:0.000} above the lone body (lift {three.Lift[bi]:0.000})");
        Check(Near(CargoPacker.OnTopShown(three, bi, L, pair), CargoPacker.OnTopShown(twoT, 1, L, pair)), "shelf lift: the panel shows the stored relation in both loads");
    }
    else Check(false, $"shelf lift: body + log + box did not put the box on a body-and-log layer: {three.Note}");
}
{
    // a beside contact tuned from a stack keeps its arrangement when the fallback rules change
    ResetLayout();
    var p = EditPair("box", "wood");
    p.Mode = "auto";
    var load = Load("wood", "wood", "box", "wood");
    var wide = S(); wide.Overhang = 0.30f; // box + wood fit side by side
    var r = CargoPacker.Pack(load, wide, L);
    int bi = Enumerable.Range(0, r.Count).FirstOrDefault(i => (r.Relation[i] == "side" || r.Relation[i] == "along") && (r.Id[i] == "box" || r.Id[r.Partner[i]] == "box"));
    if (bi > 0)
    {
        int q = r.Partner[bi];
        CargoPacker.FreezeBeside(p, FirstIsA(p, r.Id[q], r.Id[bi]), r.BesideStored(q), r.BesideStored(bi), r.Relation[bi]);
        var narrow = S(); narrow.Overhang = 0.0f; // would not fit any more
        var r2 = CargoPacker.Pack(load, narrow, L);
        Check(p.Mode == r.Relation[bi] && r2.Relation.Count(x => x == "side" || x == "along") == r.Relation.Count(x => x == "side" || x == "along"), $"a tuned beside pair is pinned ({p.Mode}) and survives a smaller overhang");
    }
    else Check(false, "box + wood did not lie beside each other with a wide overhang: " + r.Note);
}
{
    // alone on top of a pair: five logs; the onpair offset moves the lone top log only, and never a two-item stack
    ResetLayout();
    var five = Load("wood", "wood", "wood", "wood", "wood");
    var r0 = CargoPacker.Pack(five, S(), L);
    items["wood"].OnPair = new Vector3(0.03f, -0.05f, 0.02f);
    var r1 = CargoPacker.Pack(five, S(), L);
    Check(Near(r1.Target[4] - r0.Target[4], items["wood"].OnPair) && Enumerable.Range(0, 4).All(j => Near(r1.Target[j], r0.Target[j])), "five logs: onpair moves the lone top log only");
    var two = CargoPacker.Pack(Load("wood", "wood"), S("stack"), L);
    items["wood"].OnPair = Vector3.zero;
    var twoBase = CargoPacker.Pack(Load("wood", "wood"), S("stack"), L);
    Check(Near(two.Target[1], twoBase.Target[1]), "two logs stacked: onpair never applies");
    var six = CargoPacker.Pack(Load("wood", "wood", "wood", "wood", "wood", "wood"), S(), L);
    items["wood"].OnPair = new Vector3(0.03f, -0.05f, 0.02f);
    var six1 = CargoPacker.Pack(Load("wood", "wood", "wood", "wood", "wood", "wood"), S(), L);
    Check(Enumerable.Range(0, 6).All(j => Near(six1.Target[j], six.Target[j])), "six logs (no lone item): onpair changes nothing");
    items["wood"].OnPair = Vector3.zero;
}
{
    // the coverage analysis (CargoCoverage): every load of up to six items over the six test classes
    ResetLayout();
    items["zombie"] = new CargoLayout.ItemEntry { Id = "zombie", OnPair = new Vector3(0f, 0.1f, 0f) }; // a zombie never lies alone on a pair: a dead tuning
    var cls = classes.ToList();
    var rep = CargoCoverage.Analyse(cls, 6, null, L, S(), pairs, items.Values);
    int expect = 0;
    for (int size = 1; size <= 6; size++) { long c = 1; for (int j = 0; j < size; j++) c = c * (cls.Count + j) / (j + 1); expect += (int)c; }
    Check(rep.Loads.Count == expect, $"coverage: {rep.Loads.Count} loads, expected every multiset of 1..6 of {cls.Count} classes ({expect})");
    Check(rep.Total == rep.Contacts.Count && rep.Total == rep.ByKey.Count, "coverage: contacts counted once");
    bool keysOk = true, smallestOk = true;
    foreach (var ld in rep.Loads)
    {
        var r = CargoPacker.Pack(ld.Classes.Select((c, j) => new CargoPacker.Item { Id = cls[c], Box = boxes[cls[c]], LoadOrder = j }).ToList(), S(), L);
        var keys = CargoCoverage.ContactsOf(r, x => x).Select(kv => kv.Key).Distinct().OrderBy(x => x, StringComparer.Ordinal);
        keysOk &= keys.SequenceEqual(ld.Keys.OrderBy(x => x, StringComparer.Ordinal));
    }
    foreach (var c in rep.Contacts)
    {
        var sm = rep.Loads.First(l => ReferenceEquals(l.Classes, c.Smallest));
        smallestOk &= sm.Keys.Contains(c.Key) && rep.Loads.Where(l => l.Keys.Contains(c.Key)).All(l => l.Classes.Length >= c.Smallest.Length);
        smallestOk &= c.Loads == rep.Loads.Count(l => l.Keys.Contains(c.Key));
    }
    Check(keysOk, "coverage: each load's contacts are the packer's (the Cargo tab's keys)");
    Check(smallestOk, "coverage: every contact's smallest load shows it, no smaller load does, and its load count is right");
    var covered = new HashSet<string>(rep.Cover.SelectMany(i => rep.Loads[i].Keys));
    Check(rep.Contacts.Where(c => c.Kind != "alone").All(c => covered.Contains(c.Key)), "coverage: the covering loads show every contact");
    Check(rep.Contacts.All(c => c.Tuned == (c.Kind == "alone" ? (items.TryGetValue(c.Lower, out var e1) && e1.HasAlone) : c.Kind == "onpair" ? (items.TryGetValue(c.Lower, out var e2) && e2.OnPairTuned) : FindPair(c.Lower, c.Upper) is { } pp && (c.Kind == "on" ? pp.TopTuned : pp.BesideTuned))), "coverage: tuned states match the layout");
    Check(rep.Dead.Any(d => d.Contains("[item zombie]")), "coverage: the zombie's onpair offset is reported as never used");
    var again = CargoCoverage.Analyse(cls, 6, null, L, S(), pairs, items.Values);
    Check(CargoCoverage.Text(again) == CargoCoverage.Text(rep) && again.Cover.SequenceEqual(rep.Cover), "coverage: deterministic");
    var capped = CargoCoverage.Analyse(cls, 6, id => id == "zombie" ? 2 : 6, L, S(), pairs, items.Values);
    Check(capped.Loads.All(l => l.Classes.Count(c => cls[c] == "zombie") <= 2) && capped.Loads.Count < rep.Loads.Count, "coverage: a class cap (the sheet's stand-in zombies) is kept");
    Console.WriteLine($"INFO coverage: {rep.Loads.Count} loads, {rep.Total} contacts ({rep.Tuned} tuned), cover {rep.Cover.Count} loads, {rep.Dead.Count} dead tuning(s)");
    if (args.Contains("--coverage")) Console.WriteLine(CargoCoverage.Text(rep));
    items.Remove("zombie");
}
Console.WriteLine($"{pass} passed, {fail} failed");
Console.WriteLine(fail == 0 ? "ALL PASS" : "FAILED");
return fail == 0 ? 0 : 1;
