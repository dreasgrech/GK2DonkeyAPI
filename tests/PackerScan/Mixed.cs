// The exact file state of 2026-10-10 afternoon: wood's class entry, the log pair's beside relation tuned (to values
// that equal the defaults), its on-top relation untuned. Pressing Tune on "wood on wood" must move nothing.
using GK2DonkeyAPI;
using UnityEngine;

static class Mixed
{
    public static int Run(Func<bool, string, bool> check)
    {
        var items = new Dictionary<string, CargoLayout.ItemEntry>
        {
            ["wood"] = new CargoLayout.ItemEntry { Id = "wood", Base = new Vector3(-0.02f, -0.09f, 0.05f), HasAlone = true, Alone = new Vector3(-0.02f, -0.2f, -0.03f) },
        };
        var pairs = new List<CargoLayout.PairEntry>
        {
            new CargoLayout.PairEntry { A = "wood", B = "wood", Mode = "side", BesideTuned = true, BesideA = new Vector3(0.04f, -0.09f, 0.235f), BesideB = new Vector3(-0.02f, -0.09f, -0.135f) },
        };
        var L = new CargoPacker.Lookup
        {
            ForItem = id => items.TryGetValue(id, out var e) ? e : null,
            ForPair = (a, b) => pairs.Find(p => (p.A == a && p.B == b) || (p.A == b && p.B == a)),
            FirstIsA = (p, f, s) => p.A == f && p.B == s || !(p.A == s && p.B == f),
            SameClass = (a, b) => a == b,
            BoxOf = id => new Vector3(0.82f, 0.57f, 0.35f),
        };
        var S = new CargoPacker.Settings { Floor = -0.26f, Gap = 0.02f, Overhang = 0.12f, Nestle = 0.03f, LayerShift = -0.03f, BedWidth = 0.75f, BedLength = 0.90f };
        var load = Enumerable.Range(0, 6).Select(k => new CargoPacker.Item { Id = "wood", Box = new Vector3(0.82f, 0.57f, 0.35f), LoadOrder = k }).ToList();
        int bad = 0;
        var before = CargoPacker.Pack(load, S, L);
        int i = Enumerable.Range(0, 6).First(k => before.Relation[k] == "on");
        var pair = pairs[0];
        bool qIsA = L.FirstIsA(pair, before.Id[before.Partner[i]], before.Id[i]);
        Vector3 v = CargoPacker.OnTopShown(before, i, L, pair);
        CargoPacker.FreezeOnTop(pair, qIsA, items["wood"].Base, v);
        var after = CargoPacker.Pack(load, S, L);
        for (int k = 0; k < 6; k++)
        {
            Vector3 d = after.Target[k] - before.Target[k];
            if (!check(d.magnitude < 1e-4f, $"mixed six logs: item {k} moved by ({d.x:0.000}, {d.y:0.000}, {d.z:0.000}) when the on-top relation was frozen (before {before.Target[k]}, after {after.Target[k]}; note before: {before.Note}; after: {after.Note})")) bad++;
        }
        return bad;
    }
}
