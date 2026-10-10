// `dotnet run -c Release -- --show`: prints what the Cargo tab would show as items are loaded one by one (the same
// relations, states and slider values the tab reads from the packer), with the repo's current log values.
using GK2DonkeyAPI;
using UnityEngine;

static class Show
{
    public static void Run()
    {
        var boxes = new Dictionary<string, Vector3>
        {
            ["wood"] = new Vector3(0.82f, 0.57f, 0.35f),
            ["box_appliances_1"] = new Vector3(0.52f, 0.40f, 0.50f),
            ["stone"] = new Vector3(0.74f, 0.33f, 0.35f),
        };
        var items = new Dictionary<string, CargoLayout.ItemEntry>
        {
            ["wood"] = new CargoLayout.ItemEntry { Id = "wood", Base = new Vector3(-0.02f, -0.09f, 0.05f), HasAlone = true, Alone = new Vector3(-0.02f, -0.2f, -0.03f) },
            ["stone"] = new CargoLayout.ItemEntry { Id = "stone", Top = new Vector3(0f, -0.14f, 0f), HasAlone = true, Alone = new Vector3(0f, -0.13f, -0.02f) },
        };
        var pairs = new List<CargoLayout.PairEntry>
        {
            new CargoLayout.PairEntry { A = "wood", B = "wood", Mode = "side", BesideTuned = true, BesideA = new Vector3(0.04f, -0.09f, 0.235f), BesideB = new Vector3(-0.02f, -0.09f, -0.135f) },
            new CargoLayout.PairEntry { A = "wood", B = "stone", Mode = "stack" },
        };
        var L = new CargoPacker.Lookup
        {
            ForItem = id => items.TryGetValue(id, out var e) ? e : null,
            ForPair = (a, b) => pairs.Find(p => (p.A == a && p.B == b) || (p.A == b && p.B == a)),
            FirstIsA = (p, f, s) => p.A == f && p.B == s || !(p.A == s && p.B == f),
            SameClass = (a, b) => a == b,
            BoxOf = id => boxes[id],
        };
        var S = new CargoPacker.Settings { Floor = -0.26f, Gap = 0.02f, Overhang = 0.12f, Nestle = 0.03f, LayerShift = -0.03f, BedWidth = 0.75f, BedLength = 0.90f };
        string V(Vector3 v) => $"H {v.y:0.00}  along {v.x:0.00}  across {v.z:0.00}";

        foreach (string[] ids in new[] { new[] { "wood" }, new[] { "wood", "wood" }, new[] { "wood", "wood", "wood" }, new[] { "wood", "wood", "wood", "wood" }, new[] { "wood", "wood", "wood", "wood", "wood", "wood" }, new[] { "wood", "wood", "stone", "box_appliances_1" } })
        {
            var load = ids.Select((id, k) => new CargoPacker.Item { Id = id, Box = boxes[id], LoadOrder = k }).ToList();
            var r = CargoPacker.Pack(load, S, L);
            int n = r.Count, layers = r.Layer.Max() + 1;
            Console.WriteLine();
            Console.WriteLine($"==== cart: {string.Join(", ", ids)}  ->  {n} item{(n == 1 ? "" : "s")}, {layers} layer{(layers == 1 ? "" : "s")}");
            for (int l = layers - 1; l >= 0; l--)
            {
                var inLayer = Enumerable.Range(0, n).Where(i => r.Layer[i] == l).Select(i => r.Id[i]);
                string where = layers == 1 ? "" : l == 0 ? " (bed)" : l == layers - 1 ? " (top)" : "";
                Console.WriteLine($"  Layer {l + 1}{where}:  {string.Join("  |  ", inLayer)}");
            }
            Console.WriteLine($"  layout note: {r.Note}");
            if (n == 1)
            {
                var e = L.ForItem(r.Id[0]);
                Console.WriteLine($"  STEP 1 open: {r.Id[0]} alone: {V(e != null && e.HasAlone ? e.Alone : e?.Base ?? Vector3.zero)}   (item at {V(r.Target[0] - new Vector3(0, r.Floor, 0))})");
            }
            else if (n == 2)
            {
                var pair = L.ForPair(r.Id[0], r.Id[1]);
                string shown = r.PairMode;
                bool stack = shown == "stack";
                bool tuned = pair != null && pair.TunedFor(shown);
                Console.WriteLine($"  STEP 2 open: {r.Id[0]} + {r.Id[1]}: {(stack ? "one on the other" : shown == "along" ? "end to end" : "side by side across")} ({(tuned ? "tuned" : "by the rules")}); arrangement buttons, mode = {pair?.Mode ?? "auto"}");
                for (int i = 0; i < 2; i++) Console.WriteLine($"     {r.Id[i]} ({(i == 0 ? (stack ? "below" : "first") : (stack ? "on top" : "second"))}): {V(r.Target[i] - new Vector3(0, r.Floor, 0))}");
            }
            else
            {
                Console.WriteLine("  STEP 3 open:");
                var seen = new HashSet<string>();
                for (int i = 1; i < n; i++)
                {
                    if (r.Relation[i] == "bed") continue;
                    int q = r.Partner[i];
                    bool on = r.Relation[i] == "on";
                    string key = (on ? "on:" : "beside:") + r.Id[q] + "+" + r.Id[i];
                    if (!seen.Add(key)) continue;
                    var ls = Enumerable.Range(1, n - 1).Where(j => r.Relation[j] != "bed" && ((r.Relation[j] == "on") == on) && r.Id[r.Partner[j]] == r.Id[q] && r.Id[j] == r.Id[i]).Select(j => r.Layer[j] + 1).Distinct();
                    var pair = L.ForPair(r.Id[q], r.Id[i]);
                    bool tuned = pair != null && (on ? pair.TopTuned : pair.BesideTuned);
                    if (on)
                    {
                        Console.WriteLine($"   [box] {r.Id[i]} on {r.Id[q]}   ({(tuned ? "tuned" : "by the rules")}; layers {string.Join(", ", ls)})  [Alone] {(tuned ? "[Reset]" : "")}");
                        Console.WriteLine($"         Height / Along / Across: {V(r.Rel(i))}");
                    }
                    else
                    {
                        Console.WriteLine($"   [box] {r.Id[q]}  |  {r.Id[i]} in one layer   ({(tuned ? "tuned" : "by the rules")}; layers {string.Join(", ", ls)})  [Alone] {(tuned ? "[Reset]" : "")}");
                        Console.WriteLine($"         {r.Id[q]} (1): {V(r.LayerPos(q))}");
                        Console.WriteLine($"         {r.Id[i]} (2): {V(r.LayerPos(i))}");
                    }
                }
            }
            Console.WriteLine("  (steps 4 Rules, 5 Behaviour folded; 6 Check; help folded)");
        }
    }
}
