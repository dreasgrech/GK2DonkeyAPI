// PackerScan --tune: can the measured shapes judge a cargo layout the way the user does? (docs/cargo-auto-tune.md)
// Writes nothing. Part 1: the load the user fixed by eye on 2026-10-15 (low crate alone on log + low crate), measured
// with the class's alone-on-a-pair offset (before) and with the one-pair offset (after). Part 2: for every tuned
// contact, the cost of its loads as one axis of its stored value moves, and whether the stored value sits in the
// lowest band (where the shapes find nothing wrong) or the shapes would put it elsewhere.
using System.Reflection;
using GK2DonkeyAPI;
using UnityEngine;

static class Tune
{
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

    static Tune()
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

    static readonly CargoPacker.Settings S = new() { Floor = -0.26f, Gap = 0.02f, Overhang = 0.12f, Nestle = 0.03f, LayerShift = -0.03f, BedWidth = 0.75f, BedLength = 0.90f };
    static CargoShapes.Shape ShapeOf(string id) => CargoShapes.ByClass(C(id));

    // What the shapes find wrong in a pack, in metres: cut-ins deeper than the scan's 3.5 cm (the sink into the item
    // below is 3 cm and reads as resting), the judged float and sink of every upper item past its threshold, an item
    // with nothing under it, and an item past the bed plus the overhang.
    static float Cost(CargoPacker.Result r, out string why)
    {
        float cost = 0f;
        var w = new List<string>();
        for (int i = 0; i < r.Count; i++)
        {
            for (int k = i + 1; k < r.Count; k++)
            {
                float d = CargoShapes.Overlap(r, i, k, ShapeOf);
                if (d > 0.035f) { cost += d - 0.035f; w.Add($"{Name[C(r.Id[i])]}/{Name[C(r.Id[k])]} cut {d:0.00}"); }
            }
            var v = CargoShapes.Judge(r, i, ShapeOf);
            if (v != null)
            {
                float amt = v.Kind == "hangs" ? 0.5f : v.Kind == "floats" ? v.Amount - CargoShapes.FloatAt : v.Amount - CargoShapes.SinkAt;
                cost += Mathf.Max(amt, 0.005f);
                w.Add($"{Name[C(r.Id[i])]} {v.Kind} {v.Amount:0.00}");
            }
            float offX = Mathf.Abs(r.Target[i].x) + r.Box[i].x / 2 - (S.BedLength / 2 + S.Overhang);
            float offZ = Mathf.Abs(r.Target[i].z) + r.Box[i].z / 2 - (S.BedWidth / 2 + S.Overhang);
            if (offX > 0.005f) { cost += offX; w.Add($"{Name[C(r.Id[i])]} off along {offX:0.00}"); }
            if (offZ > 0.005f) { cost += offZ; w.Add($"{Name[C(r.Id[i])]} off across {offZ:0.00}"); }
        }
        why = w.Count == 0 ? "nothing" : string.Join(", ", w);
        return cost;
    }

    static string P(Vector3 v) => $"({v.x:+0.00;-0.00}, {v.y:+0.00;-0.00}, {v.z:+0.00;-0.00})";

    public static int Run(string cfgPath)
    {
        var restore = typeof(CargoLayout).GetMethod("Restore", BindingFlags.NonPublic | BindingFlags.Static)!;
        restore.Invoke(null, new object[] { File.ReadAllText(cfgPath), false });
        CargoShapes.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(cfgPath)!, "cargo-shapes.cfg")));
        bool Same(string a, string b) => C(a) == C(b);
        var items = CargoLayout.Items.Values.ToList();
        var pairs = CargoLayout.Pairs.ToList();
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
        var L = new CargoPacker.Lookup
        {
            ForItem = ForItem, ForPair = ForPair, FirstIsA = FirstIsA, SameClass = Same, BoxOf = id => Box[C(id)],
            ForOnPair = (it, a, b) => CargoLayout.FindOnPair(CargoLayout.OnPairs, it, a, b, Same), ShapeOf = ShapeOf,
        };
        var reps = Box.Keys.ToList();
        var rep = CargoCoverage.Analyse(reps, 6, null, L, S, pairs, items, id => Name[C(id)]);
        CargoPacker.Result PackLoad(int[] classes) => CargoPacker.Pack(classes.Select((c, j) => new CargoPacker.Item { Id = reps[c], Box = Box[reps[c]], LoadOrder = j }).ToList(), S, L);

        // ---- part 1: the load fixed by eye
        Console.WriteLine("PART 1: low crate alone on log + low crate, fixed by eye on 2026-10-15");
        var load = new List<CargoPacker.Item>
        {
            new() { Id = "wood", Box = Box["wood"], LoadOrder = 0 },
            new() { Id = "box_appliances_1", Box = Box["box_appliances_1"], LoadOrder = 1 },
            new() { Id = "box_appliances_2", Box = Box["box_appliances_1"], LoadOrder = 2 },
        };
        var one = CargoLayout.FindOnPair(CargoLayout.OnPairs, "box_appliances_1", "wood", "box_appliances_1", Same);
        var saved = new List<CargoLayout.OnPairEntry>(CargoLayout.OnPairs);
        if (one != null) CargoLayout.OnPairs.Remove(one);
        var before = CargoPacker.Pack(load, S, L);
        float cb = Cost(before, out string wb);
        CargoLayout.OnPairs.Clear();
        CargoLayout.OnPairs.AddRange(saved);
        var after = CargoPacker.Pack(load, S, L);
        float ca = Cost(after, out string wa);
        for (int i = 0; i < before.Count; i++)
        {
            Console.WriteLine($"  {Name[C(before.Id[i])],-10} layer {before.Layer[i]} {before.Relation[i],-5} before {P(before.Target[i])}  after {P(after.Target[i])}");
        }
        Console.WriteLine($"  the shapes before: cost {cb:0.000} ({wb})");
        Console.WriteLine($"  the shapes after:  cost {ca:0.000} ({wa})");
        Console.WriteLine($"  one-pair offset {(one == null ? "none in the file" : P(one.Offset))}");

        // ---- part 2: the landscape of every tuned contact
        Console.WriteLine();
        Console.WriteLine("PART 2: each tuned contact, one axis at a time from -8 to +8 cm in 2 cm steps, over its loads of up to four items (at most 40).");
        Console.WriteLine("  band: the steps whose cost is within 0.005 of the lowest; 'in' = the stored value (0) is in it.");
        int inBand = 0, outBand = 0, flat = 0;
        var steps = new float[] { -0.08f, -0.06f, -0.04f, -0.02f, 0f, 0.02f, 0.04f, 0.06f, 0.08f };
        string[] axisName = { "along", "up", "across" };
        foreach (var c in rep.Contacts.Where(c => c.Tuned && c.Kind != "alone"))
        {
            var loads = rep.Loads.Where(l => l.Keys.Contains(c.Key) && l.Classes.Length <= 4).Take(40).ToList();
            if (loads.Count == 0) continue;
            // the stored vector this contact's sliders move
            Func<Vector3> get;
            Action<Vector3> set;
            if (c.Kind == "onpair")
            {
                var e = ForItem(c.Lower);
                if (e == null) continue;
                get = () => e.OnPair;
                set = v => e.OnPair = v;
            }
            else
            {
                var pr = ForPair(c.Lower, c.Upper);
                if (pr == null) continue;
                if (c.Kind == "on") { get = () => pr.TopB; set = v => pr.TopB = v; }
                else { get = () => pr.BesideB; set = v => pr.BesideB = v; }
            }
            Vector3 stored = get();
            var parts = new List<string>();
            for (int ax = 0; ax < 3; ax++)
            {
                var costs = new float[steps.Length];
                for (int s = 0; s < steps.Length; s++)
                {
                    Vector3 v = stored;
                    v[ax] += steps[s];
                    set(v);
                    float sum = 0f;
                    foreach (var ld in loads) sum += Cost(PackLoad(ld.Classes), out _);
                    costs[s] = sum;
                }
                set(stored);
                float min = costs.Min();
                var band = Enumerable.Range(0, steps.Length).Where(s => costs[s] <= min + 0.005f).ToList();
                bool all = band.Count == steps.Length;
                bool ok = band.Contains(4);
                if (all) flat++; else if (ok) inBand++; else outBand++;
                parts.Add(all ? $"{axisName[ax]} flat" : $"{axisName[ax]} {(ok ? "in" : "OUT")} [{steps[band.First()] * 100:+0;-0;0}..{steps[band.Last()] * 100:+0;-0;0}]{(ok ? "" : $" cost {costs[4]:0.00} vs {min:0.00}")}");
            }
            Console.WriteLine($"  {c.Kind,-6} {c.Title,-30} {loads.Count,2} loads: {string.Join("; ", parts)}");
        }
        Console.WriteLine($"axes: {inBand} stored value in the band, {outBand} outside it, {flat} flat (the shapes do not care on that axis)");
        return 0;
    }
}
