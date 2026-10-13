// PackerScan --real: the repo's own cargo-layout.cfg with the game's seven size classes (tools/cargo-sheet/out/shapes.html,
// 2026-10-12), offline. The coverage report as the game shows it, and a box-level problem scan of every load: items
// cutting into each other, items floating above what is under them, items past the bed. Boxes, not meshes (a log is
// round, a crate's lid may be lower than its box): the scan points at loads to look at, it does not judge them.
using System.Reflection;
using System.Text;
using GK2DonkeyAPI;
using UnityEngine;

static class Real
{
    // class representative -> its box (x along, y up, z across) and members
    static readonly Dictionary<string, Vector3> Box = new()
    {
        ["body_zombie"] = new Vector3(0.98f, 0.32f, 0.60f),
        ["wood"] = new Vector3(0.82f, 0.57f, 0.35f),
        ["box_flour"] = new Vector3(0.52f, 0.58f, 0.49f),
        ["box_beer"] = new Vector3(0.52f, 0.50f, 0.48f),
        ["body_corpse"] = new Vector3(0.84f, 0.33f, 0.43f),
        ["box_appliances_1"] = new Vector3(0.52f, 0.40f, 0.50f),
        ["iron_ore_2h"] = new Vector3(0.74f, 0.33f, 0.35f),
    };
    static readonly Dictionary<string, string> Name = new()
    {
        ["body_zombie"] = "zombie", ["wood"] = "log", ["box_flour"] = "flour sack", ["box_beer"] = "tall crate",
        ["body_corpse"] = "corpse", ["box_appliances_1"] = "low crate", ["iron_ore_2h"] = "slab",
    };
    static readonly Dictionary<string, string> ClassOfId = new();

    static Real()
    {
        void M(string rep, params string[] ids) { ClassOfId[rep] = rep; foreach (var i in ids) ClassOfId[i] = rep; }
        M("body_zombie");
        M("wood");
        M("box_flour");
        M("box_beer", "box_clothes_1", "box_clothes_2", "box_fabric", "box_vine");
        M("body_corpse", "body_wild_zombie");
        M("box_appliances_1", "box_appliances_2", "box_building_kit_1", "box_building_kit_2", "box_cutlery_1", "box_cutlery_2", "box_engineering_mechanism",
          "box_furniture_1", "box_furniture_2", "box_iron_ingot", "box_kitchen_utensils", "box_preserves_1", "box_preserves_2", "box_tools_iron", "box_tools_steel", "box_vegetables");
        M("iron_ore_2h", "marble", "stone");
    }

    static string C(string id) => ClassOfId.TryGetValue(id, out var c) ? c : id;

    public static int Run(string cfgPath, bool verbose)
    {
        string text = File.ReadAllText(cfgPath);
        typeof(CargoLayout).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { text });
        // undo / compare snapshots are the file's text: writing and reading back must lose nothing
        var parse = typeof(CargoLayout).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!;
        string t1 = CargoLayout.ToText();
        int ni = CargoLayout.Items.Count, np = CargoLayout.Pairs.Count;
        CargoLayout.Items.Clear(); CargoLayout.Pairs.Clear();
        typeof(CargoLayout).GetField("itemOrder", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null).GetType().GetMethod("Clear")!.Invoke(typeof(CargoLayout).GetField("itemOrder", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null), null);
        parse.Invoke(null, new object[] { t1 });
        string t2 = CargoLayout.ToText();
        bool roundTrip = t1 == t2 && CargoLayout.Items.Count == ni && CargoLayout.Pairs.Count == np;
        Console.WriteLine((roundTrip ? "PASS" : "FAIL") + $" the layout's text reads back unchanged ({ni} items, {np} pairs)");
        // an accepted alone-on-a-pair at zero survives the round trip
        var probe = CargoLayout.Items.Values.First();
        bool had = probe.HasOnPair; Vector3 hadV = probe.OnPair;
        probe.HasOnPair = true; probe.OnPair = Vector3.zero;
        string t3 = CargoLayout.ToText();
        CargoLayout.Items.Clear(); CargoLayout.Pairs.Clear();
        typeof(CargoLayout).GetField("itemOrder", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null).GetType().GetMethod("Clear")!.Invoke(typeof(CargoLayout).GetField("itemOrder", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null), null);
        parse.Invoke(null, new object[] { t3 });
        var back = CargoLayout.Items[probe.Id];
        Console.WriteLine((back.HasOnPair && back.OnPairTuned && back.OnPair == Vector3.zero ? "PASS" : "FAIL") + " an alone-on-a-pair accepted at zero reads back as tuned");
        back.HasOnPair = had; back.OnPair = hadV;
        var items = CargoLayout.Items.Values.ToList();
        CargoPacker.LiftOff = Environment.GetCommandLineArgs().Contains("--no-lift");
        if (Environment.GetCommandLineArgs().Contains("--no-class-top")) foreach (var e in items) e.Top = Vector3.zero; // what removing the old class on-top offsets would do
        var pairs = CargoLayout.Pairs.ToList();
        bool Same(string a, string b) => C(a) == C(b);
        CargoLayout.ItemEntry ForItem(string id) => CargoLayout.Items.TryGetValue(id, out var e) ? e : items.FirstOrDefault(x => Same(x.Id, id));
        CargoLayout.PairEntry ForPair(string a, string b) =>
            pairs.FirstOrDefault(p => (p.A == a && p.B == b) || (p.A == b && p.B == a))
            ?? pairs.FirstOrDefault(p => (Same(p.A, a) && Same(p.B, b)) || (Same(p.A, b) && Same(p.B, a)));
        bool FirstIsA(CargoLayout.PairEntry p, string first, string second)
        {
            if (p.A == first && p.B == second) return true;
            if (p.A == second && p.B == first) return false;
            bool straight = Same(p.A, first) && Same(p.B, second), crossed = Same(p.A, second) && Same(p.B, first);
            return straight || !crossed;
        }
        var L = new CargoPacker.Lookup { ForItem = ForItem, ForPair = ForPair, FirstIsA = FirstIsA, SameClass = Same, BoxOf = id => Box[C(id)] };
        var S = new CargoPacker.Settings { Floor = -0.26f, Gap = 0.02f, Overhang = 0.12f, Nestle = 0.03f, LayerShift = -0.03f, BedWidth = 0.75f, BedLength = 0.90f };
        var reps = Box.Keys.ToList();
        var rep = CargoCoverage.Analyse(reps, 6, null, L, S, pairs, items, id => Name[C(id)]);
        Console.WriteLine(CargoCoverage.Text(rep));

        // ---- the scan by the measured shapes (cargo-shapes.cfg)
        string shapesPath = Path.Combine(Path.GetDirectoryName(cfgPath)!, "cargo-shapes.cfg");
        if (File.Exists(shapesPath))
        {
            CargoShapes.Parse(File.ReadAllText(shapesPath));
            CargoShapes.Shape ShapeOf(string id) => CargoShapes.ByClass(C(id));
            // the cart packs with the measured shapes (the seat pass), unless --boxes
            if (!Environment.GetCommandLineArgs().Contains("--boxes")) L.ShapeOf = ShapeOf;
            // --show wood,wood,wood;box_beer,box_beer,box_beer,box_beer : the pack of each load, item by item (a look at the
            // numbers the game would place: layer, relation, partner, target, layer origin, lift), then back to the scan.
            int showAt = Array.IndexOf(Environment.GetCommandLineArgs(), "--show");
            if (showAt >= 0 && showAt + 1 < Environment.GetCommandLineArgs().Length)
            {
                foreach (string loadText in Environment.GetCommandLineArgs()[showAt + 1].Split(';'))
                {
                    var ids = loadText.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                    var input = ids.Select((id, j) => new CargoPacker.Item { Id = id, Box = Box[C(id)], LoadOrder = j }).ToList();
                    var r = CargoPacker.Pack(input, S, L);
                    Console.WriteLine($"-- {string.Join(" + ", ids.Select(i => Name[C(i)]))}: {r.Note}");
                    for (int i = 0; i < r.Count; i++)
                    {
                        Console.WriteLine($"   {i} {Name[C(r.Id[i])],-10} layer {r.Layer[i] + 1} {r.Relation[i],-5} partner {r.Partner[i],2} {(r.Tuned[i] ? "tuned" : "auto ")}  target ({r.Target[i].x,6:0.000} {r.Target[i].y,6:0.000} {r.Target[i].z,6:0.000})  origin ({r.Origin[i].x,6:0.000} {r.Origin[i].y,6:0.000} {r.Origin[i].z,6:0.000})  lift {r.Lift[i]:0.000}");
                    }
                }
            }
            void RestoreLayout(string text)
            {
                CargoLayout.Items.Clear(); CargoLayout.Pairs.Clear();
                var order = typeof(CargoLayout).GetField("itemOrder", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
                order.GetType().GetMethod("Clear")!.Invoke(order, null);
                typeof(CargoLayout).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { text });
                items = CargoLayout.Items.Values.ToList();
                pairs = CargoLayout.Pairs.ToList();
            }
            // One box touched (Looks right / Seat it / the first slider move on ONE on-top box): its relation and its layer's
            // arrangement frozen as the tab does, nothing else; the load must not move. Every untuned on-top row of every load.
            if (Environment.GetCommandLineArgs().Contains("--thorough"))
            {
                string start1 = CargoLayout.ToText();
                int rows = 0, movedRows = 0;
                foreach (var ld in rep.Loads.Where(l => l.Classes.Length >= 3))
                {
                    var input = ld.Classes.Select((cl, j) => new CargoPacker.Item { Id = reps[cl], Box = Box[reps[cl]], LoadOrder = j }).ToList();
                    RestoreLayout(start1);
                    var r0 = CargoPacker.Pack(input, S, L);
                    for (int i = 1; i < r0.Count; i++)
                    {
                        if (r0.Relation[i] != "on" || r0.Tuned[i]) continue;
                        RestoreLayout(start1);
                        var b = CargoPacker.Pack(input, S, L);
                        int q = b.Partner[i];
                        var pr = ForPair(b.Id[q], b.Id[i]);
                        if (pr == null) { pr = new CargoLayout.PairEntry { A = b.Id[q], B = b.Id[i] }; CargoLayout.Pairs.Add(pr); pairs.Add(pr); }
                        var eq = ForItem(b.Id[q]);
                        CargoPacker.FreezeOnTop(pr, FirstIsA(pr, b.Id[q], b.Id[i]), eq != null ? eq.Base : Vector3.zero, b.Rel(i));
                        CargoPacker.FreezeLayerArrangement(b, i, L, (x, y) =>
                        {
                            var pp = ForPair(x, y);
                            if (pp == null) { pp = new CargoLayout.PairEntry { A = x, B = y }; CargoLayout.Pairs.Add(pp); pairs.Add(pp); }
                            return pp;
                        });
                        var a = CargoPacker.Pack(input, S, L);
                        rows++;
                        int badK = Enumerable.Range(0, a.Count).FirstOrDefault(k => (a.Target[k] - b.Target[k]).magnitude > 0.0005f, -1);
                        if (badK >= 0)
                        {
                            movedRows++;
                            if (movedRows <= 8) Console.WriteLine($"  ONE BOX MOVED {rep.Describe(ld.Classes)}: freezing {Name[C(b.Id[i])]} on {Name[C(b.Id[q])]} moved {Name[C(b.Id[badK])]} ({b.Relation[badK]}) {b.Target[badK].ToString("F3")} -> {a.Target[badK].ToString("F3")}");
                        }
                    }
                }
                RestoreLayout(start1);
                Console.WriteLine((movedRows == 0 ? "PASS" : "FAIL") + $" touching one on-top box moves nothing ({rows} rows, {movedRows} moved)");
                // and one beside box
                int brows = 0, bmoved = 0;
                foreach (var ld in rep.Loads.Where(l => l.Classes.Length >= 2))
                {
                    var input = ld.Classes.Select((cl, j) => new CargoPacker.Item { Id = reps[cl], Box = Box[reps[cl]], LoadOrder = j }).ToList();
                    RestoreLayout(start1);
                    var r0 = CargoPacker.Pack(input, S, L);
                    for (int i = 1; i < r0.Count; i++)
                    {
                        if ((r0.Relation[i] != "side" && r0.Relation[i] != "along") || r0.Tuned[i]) continue;
                        RestoreLayout(start1);
                        var b = CargoPacker.Pack(input, S, L);
                        int q = b.Partner[i];
                        var pr = ForPair(b.Id[q], b.Id[i]);
                        if (pr == null) { pr = new CargoLayout.PairEntry { A = b.Id[q], B = b.Id[i] }; CargoLayout.Pairs.Add(pr); pairs.Add(pr); }
                        if (b.Count == 2)
                        {
                            // the two-item view freezes both places (relative to the floor) in the arrangement shown
                            bool fa = FirstIsA(pr, b.Id[0], b.Id[1]);
                            pr.Mode = b.PairMode;
                            Vector3 p0 = b.Target[0] - new Vector3(0, b.Floor, 0), p1 = b.Target[1] - new Vector3(0, b.Floor, 0);
                            pr.SetFor(b.PairMode, fa ? p0 : p1, fa ? p1 : p0);
                        }
                        else CargoPacker.FreezeBeside(pr, FirstIsA(pr, b.Id[q], b.Id[i]), b.BesideStored(q), b.BesideStored(i), b.Relation[i]);
                        var a = CargoPacker.Pack(input, S, L);
                        brows++;
                        int badK = Enumerable.Range(0, a.Count).FirstOrDefault(k => (a.Target[k] - b.Target[k]).magnitude > 0.0005f, -1);
                        if (badK >= 0)
                        {
                            bmoved++;
                            if (bmoved <= 8) Console.WriteLine($"  ONE BESIDE BOX MOVED {rep.Describe(ld.Classes)}: freezing {Name[C(b.Id[q])]} | {Name[C(b.Id[i])]} moved {Name[C(b.Id[badK])]} ({b.Relation[badK]}) {b.Target[badK].ToString("F3")} -> {a.Target[badK].ToString("F3")}");
                        }
                    }
                }
                RestoreLayout(start1);
                Console.WriteLine((bmoved == 0 ? "PASS" : "FAIL") + $" touching one beside box moves nothing ({brows} rows, {bmoved} moved)");
            }
            // A lone item on a pair sits over the middle of the two below (footprint centres) plus its class's alone-on-a-pair
            // offset, in every load, tuned or not.
            {
                int lone = 0, off = 0;
                foreach (var ld in rep.Loads.Where(l => l.Classes.Length >= 3))
                {
                    var input = ld.Classes.Select((cl, j) => new CargoPacker.Item { Id = reps[cl], Box = Box[reps[cl]], LoadOrder = j }).ToList();
                    var r = CargoPacker.Pack(input, S, L);
                    for (int i = 1; i < r.Count; i++)
                    {
                        if (r.Relation[i] != "on") continue;
                        int inLayer = 0, bs = -1, be = -1;
                        for (int k = 0; k < r.Count; k++)
                        {
                            if (r.Layer[k] == r.Layer[i]) inLayer++;
                            if (r.Layer[k] == r.Layer[i] - 1) { if (bs < 0) bs = k; be = k; }
                        }
                        if (inLayer != 1 || be == bs) continue;
                        lone++;
                        var e = ForItem(r.Id[i]);
                        Vector3 op = e != null ? e.OnPair : Vector3.zero;
                        float mx = (r.Target[bs].x + r.Target[be].x) * 0.5f + op.x, mz = (r.Target[bs].z + r.Target[be].z) * 0.5f + op.z;
                        if (Mathf.Abs(r.Target[i].x - mx) > 1e-4f || Mathf.Abs(r.Target[i].z - mz) > 1e-4f)
                        {
                            off++;
                            if (verbose) Console.WriteLine($"    {rep.Describe(ld.Classes)}: {Name[C(r.Id[i])]} at ({r.Target[i].x:0.000}, {r.Target[i].z:0.000}), the middle ({mx:0.000}, {mz:0.000})");
                        }
                    }
                }
                Console.WriteLine((off == 0 ? "PASS" : "FAIL") + $" a lone item on a pair sits over the pair's middle plus its offset ({lone} cases, {off} off)");
            }
            // Accept from three, then four: for every same-class pair that lies beside and takes a third on top, with its on-top
            // relation taken away (as a Reset does), Accept from a + a + a must give a + a + a + a its upper pair straight over the
            // lower one (plus the layer shift), the lone third centred, and the three-item load unmoved by the Accept.
            {
                string start2 = CargoLayout.ToText();
                int cases = 0, badAccept = 0;
                foreach (string a in reps)
                {
                    RestoreLayout(start2);
                    var pr = ForPair(a, a);
                    if (pr == null) continue;
                    pr.ClearFor("stack");
                    var in3 = Enumerable.Range(0, 3).Select(j => new CargoPacker.Item { Id = a, Box = Box[a], LoadOrder = j }).ToList();
                    var in4 = Enumerable.Range(0, 4).Select(j => new CargoPacker.Item { Id = a, Box = Box[a], LoadOrder = j }).ToList();
                    var r3 = CargoPacker.Pack(in3, S, L);
                    if (r3.Relation[1] == "on" || r3.Relation[2] != "on" || r3.Tuned[2]) continue; // only pairs that lie beside and take a third on top
                    cases++;
                    var eq = ForItem(a);
                    CargoPacker.FreezeOnTop(pr, FirstIsA(pr, a, a), eq != null ? eq.Base : Vector3.zero, r3.Rel(2));
                    CargoPacker.FreezeLayerArrangement(r3, 2, L, (x, y) => ForPair(x, y));
                    var r3b = CargoPacker.Pack(in3, S, L);
                    var r4 = CargoPacker.Pack(in4, S, L);
                    bool unmoved = Enumerable.Range(0, 3).All(k => (r3b.Target[k] - r3.Target[k]).magnitude < 0.0005f) && r3b.Tuned[2];
                    bool straight = r4.Count == 4 && r4.Layer[2] == 1 && r4.Layer[3] == 1
                        && Mathf.Abs(r4.Target[2].x - (r4.Target[0].x + S.LayerShift)) < 1e-4f && Mathf.Abs(r4.Target[2].z - r4.Target[0].z) < 1e-4f
                        && Mathf.Abs(r4.Target[3].x - (r4.Target[1].x + S.LayerShift)) < 1e-4f && Mathf.Abs(r4.Target[3].z - r4.Target[1].z) < 1e-4f;
                    Vector3 op = eq != null ? eq.OnPair : Vector3.zero;
                    bool centred = Mathf.Abs(r3b.Target[2].x - ((r3b.Target[0].x + r3b.Target[1].x) * 0.5f + op.x)) < 1e-4f && Mathf.Abs(r3b.Target[2].z - ((r3b.Target[0].z + r3b.Target[1].z) * 0.5f + op.z)) < 1e-4f;
                    if (!(unmoved && straight && centred))
                    {
                        badAccept++;
                        Console.WriteLine($"    {Name[a]}: unmoved {unmoved}, four straight {straight} (upper {r4.Target[2].x:0.000}/{r4.Target[3].x:0.000} over {r4.Target[0].x:0.000}/{r4.Target[1].x:0.000}), lone centred {centred}");
                    }
                }
                RestoreLayout(start2);
                Console.WriteLine((badAccept == 0 ? "PASS" : "FAIL") + $" Accept from a + a + a stacks a + a + a + a straight and leaves the lone third centred ({cases} pairs, {badAccept} bad)");
            }
            // How long a pack takes with and without the seat pass (the cart packs on every change, every frame of a drag)
            {
                var six = rep.Loads.Where(l => l.Classes.Length == 6).ToList();
                var inputs = six.Select(l => l.Classes.Select((cl, j) => new CargoPacker.Item { Id = reps[cl], Box = Box[reps[cl]], LoadOrder = j }).ToList()).ToList();
                var keep = L.ShapeOf;
                foreach (var on in new[] { false, true, false, true })
                {
                    L.ShapeOf = on ? keep : null;
                    long bytes0 = GC.GetAllocatedBytesForCurrentThread();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    foreach (var inp in inputs) CargoPacker.Pack(inp, S, L);
                    sw.Stop();
                    long bytes = GC.GetAllocatedBytesForCurrentThread() - bytes0;
                    Console.WriteLine($"TIMING six-item packs {(on ? "with" : "without")} the seat pass: {sw.Elapsed.TotalMilliseconds * 1000 / inputs.Count:0} us per pack, {bytes / inputs.Count / 1024.0:0.0} KB allocated per pack ({inputs.Count} loads)");
                }
                L.ShapeOf = keep;
            }
            // Accepting (Looks right / Accept this load) moves nothing, with the seat pass: every untuned contact of the
            // contacts' smallest loads and the covering loads frozen as the tab does it, then packed again.
            {
                string start = CargoLayout.ToText();
                var testLoads = Environment.GetCommandLineArgs().Contains("--thorough") ? rep.Loads.Where(l => l.Classes.Length >= 2).Select(l => l.Classes).ToList() : rep.Contacts.Where(c => c.Smallest.Length >= 2).Select(c => c.Smallest).Concat(rep.Cover.Select(i => rep.Loads[i].Classes)).Distinct().ToList();
                int moved = 0, checkedLoads = 0;
                foreach (var cls in testLoads)
                {
                    RestoreLayout(start);
                    var input = cls.Select((cl, j) => new CargoPacker.Item { Id = reps[cl], Box = Box[reps[cl]], LoadOrder = j }).ToList();
                    var r0 = CargoPacker.Pack(input, S, L);
                    if (r0.Count == 2)
                    {
                        var pr = ForPair(r0.Id[0], r0.Id[1]) ?? new CargoLayout.PairEntry { A = r0.Id[0], B = r0.Id[1] };
                        if (!CargoLayout.Pairs.Contains(pr)) { CargoLayout.Pairs.Add(pr); pairs.Add(pr); }
                        string shown = string.IsNullOrEmpty(r0.PairMode) ? pr.Mode : r0.PairMode;
                        if (!pr.TunedFor(shown))
                        {
                            bool fa = FirstIsA(pr, r0.Id[0], r0.Id[1]);
                            pr.Mode = shown;
                            Vector3 p0 = r0.Target[0] - new Vector3(0, r0.Floor, 0), p1 = r0.Target[1] - new Vector3(0, r0.Floor, 0);
                            pr.SetFor(shown, fa ? p0 : p1, fa ? p1 : p0);
                        }
                    }
                    else
                    {
                        for (int i = 1; i < r0.Count; i++)
                        {
                            int q = r0.Partner[i];
                            var pr = ForPair(r0.Id[q], r0.Id[i]) ?? new CargoLayout.PairEntry { A = r0.Id[q], B = r0.Id[i] };
                            if (!CargoLayout.Pairs.Contains(pr)) { CargoLayout.Pairs.Add(pr); pairs.Add(pr); }
                            bool qa = FirstIsA(pr, r0.Id[q], r0.Id[i]);
                            if (r0.Relation[i] == "on" && !pr.TopTuned) { var eq = ForItem(r0.Id[q]); CargoPacker.FreezeOnTop(pr, qa, eq != null ? eq.Base : Vector3.zero, r0.Rel(i)); }
                            else if (r0.Relation[i] != "on" && !pr.BesideTuned) CargoPacker.FreezeBeside(pr, qa, r0.BesideStored(q), r0.BesideStored(i), r0.Relation[i]);
                        }
                    }
                    var r1 = CargoPacker.Pack(input, S, L);
                    if (Environment.GetCommandLineArgs().Contains("--dbg5") && rep.Describe(cls) == "5 × low crate")
                    {
                        foreach (var (nm, rr) in new[] { ("before", r0), ("after", r1) })
                            for (int k = 0; k < rr.Count; k++)
                                Console.WriteLine($"    {nm} {k} L{rr.Layer[k]} {rr.Relation[k]} p{rr.Partner[k]} tuned {rr.Tuned[k]} target {rr.Target[k].ToString("F3")} origin {rr.Origin[k].ToString("F3")} rel {rr.Rel(k).ToString("F3")} lift {rr.Lift[k]:F3}");
                        var e5 = ForItem("box_appliances_1");
                        Console.WriteLine($"    low crate entry {e5?.Id} onpair {e5?.OnPair} top {e5?.Top} base {e5?.Base}");
                    }
                    checkedLoads++;
                    for (int i = 0; i < r0.Count; i++)
                    {
                        if ((r1.Target[i] - r0.Target[i]).magnitude > 0.0005f)
                        {
                            moved++;
                            foreach (var (nm, rr) in new[] { ("before", r0), ("after", r1) })
                            {
                                int sp = rr.Partner[i];
                                var ga = CargoShapes.GapsBelow(rr, i, ShapeOf, -1); var gs = CargoShapes.GapsBelow(rr, i, ShapeOf, sp);
                                Console.WriteLine($"    {nm}: partner {sp} y {rr.Target[i].y:F4} all n={ga.Count} p25={CargoShapes.Percentile(ga, 0.25f):F4}  support n={gs.Count} p25={CargoShapes.Percentile(gs, 0.25f):F4}  others: " + string.Join(" ", Enumerable.Range(0, rr.Count).Select(k => rr.Id[k] + rr.Target[k].ToString("F4"))));
                            }
                            Console.WriteLine($"  ACCEPT MOVED {rep.Describe(cls)}: {Name[C(r0.Id[i])]} ({r0.Relation[i]}) {r0.Target[i].ToString("F4")} -> {r1.Target[i].ToString("F4")}; support {r0.Target[r0.Partner[i]].ToString("F4")} -> {r1.Target[r1.Partner[i]].ToString("F4")}; lift {r0.Lift[i]:F4} -> {r1.Lift[i]:F4}; tuned {r0.Tuned[i]} -> {r1.Tuned[i]}; notes: {r0.Note} || {r1.Note}");
                            break;
                        }
                    }
                }
                RestoreLayout(start);
                Console.WriteLine((moved == 0 ? "PASS" : "FAIL") + $" accepting moves nothing with the seat pass ({checkedLoads} loads, {moved} moved)");
            }
            var perContact = new Dictionary<string, (int n, float worst, string sample)>();
            int bad = 0, cutsN = 0, floatsN = 0, hangsN = 0;
            foreach (var ld in rep.Loads)
            {
                var input = ld.Classes.Select((c, j) => new CargoPacker.Item { Id = reps[c], Box = Box[reps[c]], LoadOrder = j }).ToList();
                var r = CargoPacker.Pack(input, S, L);
                var issues = CargoShapes.Scan(r, ShapeOf);
                if (issues.Count > 0) bad++;
                foreach (var iss in issues)
                {
                    if (iss.Kind == "cuts") cutsN++; else if (iss.Kind == "floats") floatsN++; else hangsN++;
                    int i = iss.Item;
                    string key = r.Relation[i] == "bed" ? "bed:" + Name[C(r.Id[i])] : CargoCoverage.KeyOf(r.Relation[i], r.Id[i], r.Id[r.Partner[i]], C);
                    string label = (rep.ByKey.TryGetValue(key, out var cc) ? cc.Title : key) + " -> " + iss.Kind + (iss.Kind == "cuts" ? " into " + Name[C(r.Id[iss.Other])] : "");
                    perContact.TryGetValue(label, out var st);
                    st.n++;
                    if (iss.Depth > st.worst) { st.worst = iss.Depth; st.sample = rep.Describe(ld.Classes); }
                    perContact[label] = st;
                }
            }
            // What the tuned contacts measure in their smallest load: the user's own idea of right.
            Console.WriteLine("TUNED CONTACTS BY THE SHAPES (smallest load): on top: gap under the upper item (negative = sinks in); beside: overlap depth / across / along");
            foreach (var c in rep.Contacts.Where(c => c.Kind == "on" || c.Kind == "beside"))
            {
                var input = c.Smallest.Select((cl, j) => new CargoPacker.Item { Id = reps[cl], Box = Box[reps[cl]], LoadOrder = j }).ToList();
                var r = CargoPacker.Pack(input, S, L);
                for (int i = 0; i < r.Count; i++)
                {
                    if (r.Relation[i] == "bed" || CargoCoverage.KeyOf(r.Relation[i], r.Id[i], r.Id[r.Partner[i]], C) != c.Key) continue;
                    int q = r.Partner[i];
                    if (c.Kind == "on")
                    {
                        var gs = CargoShapes.GapsBelow(r, i, ShapeOf);
                        string P(float q) => CargoShapes.Percentile(gs, q).ToString("+0.00;-0.00");
                        Console.WriteLine($"  {(c.Tuned ? "tuned   " : "fallback")} {c.Title,-28} gaps: min {P(0)} p5 {P(0.05f)} p10 {P(0.10f)} p25 {P(0.25f)} p50 {P(0.5f)}  ({gs.Count} cells)  in {rep.Describe(c.Smallest)}");
                    }
                    else
                    {
                        float d = CargoShapes.Overlap(r, i, q, ShapeOf);
                        float dz = Mathf.Abs(r.Target[i].z - r.Target[q].z) - (r.Box[i].z + r.Box[q].z) / 2, dx = Mathf.Abs(r.Target[i].x - r.Target[q].x) - (r.Box[i].x + r.Box[q].x) / 2;
                        Console.WriteLine($"  {(c.Tuned ? "tuned   " : "fallback")} {c.Title,-28} overlap {d:0.00}  box gap across {dz:+0.00;-0.00} along {dx:+0.00;-0.00} ({r.Relation[i]})   in {rep.Describe(c.Smallest)}");
                    }
                    break;
                }
            }
            // the judged scan: what the status page will show
            var judged = new Dictionary<string, (int n, float worst, string sample)>();
            int judgedLoads = 0;
            foreach (var ld in rep.Loads)
            {
                var input = ld.Classes.Select((c, j) => new CargoPacker.Item { Id = reps[c], Box = Box[reps[c]], LoadOrder = j }).ToList();
                var r = CargoPacker.Pack(input, S, L);
                bool any = false;
                for (int i = 0; i < r.Count; i++)
                {
                    var v = CargoShapes.Judge(r, i, ShapeOf);
                    if (v == null) continue;
                    any = true;
                    string key = CargoCoverage.KeyOf(r.Relation[i], r.Id[i], r.Id[r.Partner[i]], C);
                    string label = (rep.ByKey.TryGetValue(key, out var cc) ? (cc.Tuned ? "tuned    " : "fallback ") + cc.Title : key) + " " + v.Kind;
                    judged.TryGetValue(label, out var st);
                    st.n++;
                    if (v.Amount > st.worst) { st.worst = v.Amount; st.sample = rep.Describe(ld.Classes); }
                    judged[label] = st;
                }
                if (any) judgedLoads++;
            }
            Console.WriteLine($"JUDGED SCAN: {judgedLoads} of {rep.Loads.Count} loads have an item floating (lowest tenth > {CargoShapes.FloatAt:0.00}) or sinking (half > {CargoShapes.SinkAt:0.00}).");
            foreach (var kv in judged.OrderByDescending(k => k.Value.n))
                Console.WriteLine($"  {kv.Key,-48} {kv.Value.n,5} loads, worst {kv.Value.worst:0.00} in {kv.Value.sample}");
            // Seat it, as the Cargo tab does it (freeze, then write the relation moved by the seat), on every on-top contact in
            // its smallest load: afterwards the item's lowest quarter must sit at the seat sink. The layout is put back.
            string before = CargoLayout.ToText();
            int seated = 0, seatOk = 0;
            foreach (var c in rep.Contacts.Where(c => c.Kind == "on"))
            {
                var input = c.Smallest.Select((cl, j) => new CargoPacker.Item { Id = reps[cl], Box = Box[reps[cl]], LoadOrder = j }).ToList();
                var r = CargoPacker.Pack(input, S, L);
                int i = Enumerable.Range(0, r.Count).FirstOrDefault(k => r.Relation[k] == "on" && CargoCoverage.KeyOf("on", r.Id[k], r.Id[r.Partner[k]], C) == c.Key);
                if (r.Relation[i] != "on") continue;
                int q = r.Partner[i];
                float d = CargoShapes.SeatDelta(r, i, ShapeOf);
                if (float.IsNaN(d)) continue;
                var pair = ForPair(r.Id[q], r.Id[i]) ?? new CargoLayout.PairEntry { A = r.Id[q], B = r.Id[i] };
                if (!pairs.Contains(pair)) { pairs.Add(pair); }
                bool qIsA = FirstIsA(pair, r.Id[q], r.Id[i]);
                Vector3 rel = r.Rel(i);
                if (!pair.TopTuned) { var eq = ForItem(r.Id[q]); CargoPacker.FreezeOnTop(pair, qIsA, eq != null ? eq.Base : Vector3.zero, rel); }
                CargoPacker.WriteOnTop(pair, qIsA, rel + new Vector3(0f, d, 0f));
                var r2 = CargoPacker.Pack(input, S, L);
                CargoShapes.SeatQuantiles(r2, i, ShapeOf, -1, 0.25f, out float p25, out _); // measured as Seat it and the seat pass measure
                seated++;
                bool ok = Mathf.Abs(p25 + CargoShapes.SeatSink) < 0.011f;
                if (ok) seatOk++;
                else Console.WriteLine($"  SEAT MISSED {c.Title}: lowest quarter at {p25:+0.000;-0.000} after a seat of {d:+0.000;-0.000}");
            }
            Console.WriteLine((seatOk == seated ? "PASS" : "FAIL") + $" Seat it: {seatOk} of {seated} on-top contacts rest at the seat sink afterwards");
            _ = before;
            Console.WriteLine($"SHAPE SCAN (measured meshes): {bad} of {rep.Loads.Count} loads show something; {cutsN} cut-ins, {floatsN} floating, {hangsN} with nothing under them.");
            foreach (var kv in perContact.OrderByDescending(k => k.Value.n))
                Console.WriteLine($"  {kv.Key,-58} {kv.Value.n,5} times, worst {kv.Value.worst:0.00} in {kv.Value.sample}");
        }

        // ---- the box-level problem scan
        const float Tol = 0.04f; // within this nothing is reported (pixel art, rounded meshes)
        var byContact = new Dictionary<string, (int sink, int flo, int loads, float worst)>();
        int sinkLoads = 0, floatLoads = 0, pastLoads = 0;
        var worstSinks = new List<(float d, string load, string what)>();
        foreach (var ld in rep.Loads)
        {
            var input = ld.Classes.Select((c, j) => new CargoPacker.Item { Id = reps[c], Box = Box[reps[c]], LoadOrder = j }).ToList();
            var r = CargoPacker.Pack(input, S, L);
            bool sink = false, flo = false, past = false;
            for (int i = 0; i < r.Count; i++)
            {
                Vector3 a0 = r.Target[i] - new Vector3(r.Box[i].x / 2, 0, r.Box[i].z / 2), a1 = r.Target[i] + new Vector3(r.Box[i].x / 2, r.Box[i].y, r.Box[i].z / 2);
                if (Mathf.Abs(r.Target[i].z) + r.Box[i].z / 2 > S.BedWidth / 2 + S.Overhang + Tol || Mathf.Abs(r.Target[i].x) + r.Box[i].x / 2 > S.BedLength / 2 + 0.25f) past = true;
                if (r.Relation[i] != "on") continue;
                // what is under this item's footprint in the layer below
                float under = float.MinValue, sinkBy = 0f; string into = null;
                for (int k = 0; k < r.Count; k++)
                {
                    if (r.Layer[k] != r.Layer[i] - 1) continue;
                    Vector3 b0 = r.Target[k] - new Vector3(r.Box[k].x / 2, 0, r.Box[k].z / 2), b1 = r.Target[k] + new Vector3(r.Box[k].x / 2, r.Box[k].y, r.Box[k].z / 2);
                    float ox = Mathf.Min(a1.x, b1.x) - Mathf.Max(a0.x, b0.x), oz = Mathf.Min(a1.z, b1.z) - Mathf.Max(a0.z, b0.z);
                    if (ox <= 0.02f || oz <= 0.02f) continue;
                    under = Mathf.Max(under, b1.y);
                    float d = b1.y - a0.y; // > 0: this item's bottom is below the other's top
                    if (d > sinkBy) { sinkBy = d; into = r.Id[k]; }
                }
                string key = CargoCoverage.KeyOf("on", r.Id[i], r.Id[r.Partner[i]], C);
                byContact.TryGetValue(key, out var st);
                st.loads++;
                if (sinkBy > S.Nestle + Tol)
                {
                    sink = true; st.sink++; st.worst = Mathf.Max(st.worst, sinkBy);
                    worstSinks.Add((sinkBy, rep.Describe(ld.Classes), $"{Name[C(r.Id[i])]} sinks {sinkBy:0.00} into {Name[C(into)]} ({(r.Tuned[i] ? "tuned" : "fallback")}, support {Name[C(r.Id[r.Partner[i]])]})"));
                }
                else if (under > float.MinValue && a0.y - under > Tol) { flo = true; st.flo++; }
                byContact[key] = st;
            }
            if (sink) sinkLoads++;
            if (flo) floatLoads++;
            if (past) pastLoads++;
        }
        Console.WriteLine($"PROBLEM SCAN (boxes, tolerance {Tol:0.00} past the nestle): {sinkLoads} of {rep.Loads.Count} loads have an item cutting into one below, {floatLoads} an item floating, {pastLoads} an item well past the bed.");
        foreach (var kv in byContact.Where(k => k.Value.sink > 0 || k.Value.flo > 0).OrderByDescending(k => k.Value.sink + k.Value.flo))
        {
            var c = rep.ByKey[kv.Key];
            Console.WriteLine($"  {(c.Tuned ? "tuned   " : "fallback")} {c.Title,-30} in {kv.Value.loads,4} loads: cuts in {kv.Value.sink,4} (worst {kv.Value.worst:0.00}), floats {kv.Value.flo,4}   [{(c.OnSingle ? "single " : "")}{(c.OnPair ? "pair" : "")}]");
        }
        if (verbose)
        {
            foreach (var w in worstSinks.OrderByDescending(w => w.d).Take(15)) Console.WriteLine($"    {w.d:0.00}  {w.load}: {w.what}");
        }
        return 0;
    }
}
