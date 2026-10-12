using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // How far the cargo layout is tuned, over EVERY load the cart can carry (docs/cargo-coverage.md). Scene-free like
    // CargoPacker, so tests/PackerScan runs it too. Every load of 1 to MaxItems items (one item per size class, the
    // unit the layout tunes; the packer sorts the items, so the order they were loaded in never matters and a load is a
    // multiset) goes through the real packer, and the contacts it is built from are collected with the Cargo tab's own
    // keys: "x on y" (a layer on its support), "x beside y" (two items in one layer), "x alone on a pair" (an item lying
    // alone on top of a pair: its class's onpair offset) and "x alone" (a one-item cart). Each contact is one tuning,
    // used by every load it occurs in, so the contacts ARE the work, not the 1,700 loads. Reported per contact: tuned or
    // still on the fallbacks, how many loads use it, the smallest load showing it; then the fewest loads that together
    // show every contact (a greedy set cover), and tunings in the layout file that no load can ever use.
    // Shape only: the packer is fed the size classes' boxes, so which contacts occur is exact; where items end up also
    // depends on the boxes measured on the cart, which only the pictures show.
    internal static class CargoCoverage
    {
        internal sealed class Contact
        {
            public string Key;     // the Cargo tab's key: "on:a+b", "beside:a+b" (classes sorted), "onpair:c", "alone:c"
            public string Kind;    // "on", "beside", "onpair", "alone"
            public string Lower;   // on: the support; beside: the layer's first item; onpair / alone: the item
            public string Upper;   // on: the item on top; beside: the second item; onpair: the pair's first item below it
            public bool Tuned;
            public int Loads;      // loads that contain it
            public int[] Smallest; // the smallest load showing it (class indices, sorted)
            public int[] SmallestOnPair; // on: the smallest load where its support is the first item of a pair (the shelf is the pair's top)
            public bool OnSingle, OnPair; // on: seen on a lone support / on a pair below
            public int Completes;  // untuned: loads where it is the last untuned contact (tuning it completes them)
            public string LowerName, UpperName; // readable class names (Report.Label)
            public readonly HashSet<string> Arrangements = new HashSet<string>(StringComparer.Ordinal); // beside: side / along

            public string Title
            {
                get
                {
                    string lo = LowerName ?? Lower, up = UpperName ?? Upper;
                    return Kind == "on" ? $"{up} on {lo}" : Kind == "beside" ? $"{lo} beside {up}" : Kind == "onpair" ? $"{lo} alone on a pair" : $"{lo} alone";
                }
            }
        }

        internal sealed class Load
        {
            public int[] Classes;            // class indices, sorted
            public readonly List<string> Keys = new List<string>(); // its contacts (distinct), bottom up
            public int Untuned;
            public int Layers;
        }

        internal sealed class Report
        {
            public List<string> Classes = new List<string>();
            public int MaxItems;
            public readonly List<Load> Loads = new List<Load>();
            public readonly List<Contact> Contacts = new List<Contact>();          // in a stable order: kind, then classes
            public readonly Dictionary<string, Contact> ByKey = new Dictionary<string, Contact>(StringComparer.Ordinal);
            public readonly List<int> Cover = new List<int>();                      // indices into Loads
            public readonly List<string> Dead = new List<string>();                 // tunings no load uses
            public readonly int[] LoadsBySize = new int[16];
            public int Tuned, Total;
            public string Signature = "";
            public Func<string, string> Label = id => id; // a class's readable name

            // "2 × log + corpse": the classes with their counts, in class order.
            public string Describe(int[] classes)
            {
                var parts = new List<string>();
                for (int i = 0; i < classes.Length; i++)
                {
                    int n = 1;
                    while (i + n < classes.Length && classes[i + n] == classes[i]) n++;
                    string name = Label(Classes[classes[i]]);
                    parts.Add(n == 1 ? name : $"{n} × {name}");
                    i += n - 1;
                }
                return string.Join(" + ", parts);
            }
        }

        // The Cargo tab's key of item i's contact in a pack (Cargo.PlacedItem or a packer result): kind and the two
        // classes, the classes sorted. ClassOf maps an item id to its class's name.
        internal static string KeyOf(string relation, string id, string partnerId, Func<string, string> classOf)
        {
            string a = classOf(partnerId), b = classOf(id);
            string pair = string.CompareOrdinal(a, b) <= 0 ? a + "+" + b : b + "+" + a;
            return (relation == "on" ? "on:" : "beside:") + pair;
        }

        // classes: one item id per size class (the class's name). maxCount: how many of one class a load may hold (the
        // sheet has as many stand-in zombies as it could make). pairs / items: the layout file's entries, for the dead
        // tunings. sameClass: CargoLayout.SameClass (the entries are keyed by any member's id).
        internal static Report Analyse(IList<string> classes, int maxItems, Func<string, int> maxCount, CargoPacker.Lookup L, CargoPacker.Settings s,
            IEnumerable<CargoLayout.PairEntry> pairs, IEnumerable<CargoLayout.ItemEntry> items, Func<string, string> label = null)
        {
            var rep = new Report { MaxItems = maxItems };
            if (label != null) rep.Label = label;
            rep.Classes.AddRange(classes);
            int k = classes.Count;
            var counts = new int[k];
            for (int c = 0; c < k; c++) counts[c] = Mathf.Max(0, Mathf.Min(maxItems, maxCount != null ? maxCount(classes[c]) : maxItems));
            var cur = new List<int>();
            // multisets in a fixed order: by size, then lexicographic by class index
            for (int size = 1; size <= maxItems; size++)
            {
                Enumerate(k, size, counts, 0, cur, load => AddLoad(rep, load, L, s));
            }
            // tuned state and the stable order
            rep.Contacts.Sort((x, y) =>
            {
                int c = KindRank(x.Kind).CompareTo(KindRank(y.Kind));
                if (c == 0) c = string.CompareOrdinal(x.Lower, y.Lower);
                if (c == 0) c = string.CompareOrdinal(x.Upper ?? "", y.Upper ?? "");
                return c;
            });
            foreach (Contact c in rep.Contacts)
            {
                c.Tuned = IsTuned(c, L);
                rep.Total++;
                if (c.Tuned) rep.Tuned++;
            }
            foreach (Load ld in rep.Loads)
            {
                foreach (string key in ld.Keys) if (!rep.ByKey[key].Tuned) ld.Untuned++;
            }
            foreach (Load ld in rep.Loads)
            {
                if (ld.Untuned != 1) continue;
                foreach (string key in ld.Keys) if (!rep.ByKey[key].Tuned) rep.ByKey[key].Completes++;
            }
            foreach (Contact c in rep.Contacts)
            {
                c.LowerName = rep.Label(c.Lower);
                if (c.Upper != null) c.UpperName = rep.Label(c.Upper);
            }
            Cover(rep);
            FindDead(rep, L, pairs, items);
            return rep;
        }

        private static int KindRank(string kind) => kind == "alone" ? 0 : kind == "beside" ? 1 : kind == "on" ? 2 : 3;

        private static void Enumerate(int k, int left, int[] counts, int from, List<int> cur, Action<int[]> visit)
        {
            if (left == 0)
            {
                visit(cur.ToArray());
                return;
            }
            for (int c = from; c < k; c++)
            {
                int have = 0;
                foreach (int x in cur) if (x == c) have++;
                if (have >= counts[c]) continue;
                cur.Add(c);
                Enumerate(k, left - 1, counts, c, cur, visit);
                cur.RemoveAt(cur.Count - 1);
            }
        }

        private static void AddLoad(Report rep, int[] load, CargoPacker.Lookup L, CargoPacker.Settings s)
        {
            var input = new List<CargoPacker.Item>(load.Length);
            for (int j = 0; j < load.Length; j++)
            {
                string id = rep.Classes[load[j]];
                input.Add(new CargoPacker.Item { Id = id, Box = L.BoxOf(id), LoadOrder = j });
            }
            CargoPacker.Result r = CargoPacker.Pack(input, s, L);
            var ld = new Load { Classes = load };
            rep.Loads.Add(ld);
            rep.LoadsBySize[Mathf.Min(load.Length, rep.LoadsBySize.Length - 1)]++;
            for (int i = 0; i < r.Count; i++) ld.Layers = Mathf.Max(ld.Layers, r.Layer[i] + 1);
            foreach (KeyValuePair<string, Contact> kv in ContactsOf(r, id => id))
            {
                Contact c;
                if (!rep.ByKey.TryGetValue(kv.Key, out c))
                {
                    c = kv.Value;
                    rep.ByKey[c.Key] = c;
                    rep.Contacts.Add(c);
                }
                else
                {
                    c.OnSingle |= kv.Value.OnSingle;
                    c.OnPair |= kv.Value.OnPair;
                    c.Arrangements.UnionWith(kv.Value.Arrangements);
                }
                if (ld.Keys.Contains(c.Key)) continue;
                ld.Keys.Add(c.Key);
                c.Loads++;
                if (c.Smallest == null || Smaller(load, c.Smallest)) c.Smallest = load;
                if (kv.Value.OnPair && (c.SmallestOnPair == null || Smaller(load, c.SmallestOnPair))) c.SmallestOnPair = load;
            }
        }

        // Fewer items first; the enumeration order breaks ties (it visits smaller loads and lower classes first, so the
        // first one seen stays).
        private static bool Smaller(int[] a, int[] b) => a.Length < b.Length;

        // The contacts of one pack, keyed like the Cargo tab (several items may share one contact; the first wins).
        internal static List<KeyValuePair<string, Contact>> ContactsOf(CargoPacker.Result r, Func<string, string> classOf)
        {
            var list = new List<KeyValuePair<string, Contact>>();
            int n = r.Count;
            for (int i = 0; i < n; i++)
            {
                string rel = r.Relation[i];
                if (rel == "bed")
                {
                    if (n == 1)
                    {
                        string c0 = classOf(r.Id[0]);
                        list.Add(new KeyValuePair<string, Contact>("alone:" + c0, new Contact { Key = "alone:" + c0, Kind = "alone", Lower = c0 }));
                    }
                    continue;
                }
                int p = r.Partner[i];
                string key = KeyOf(rel, r.Id[i], r.Id[p], classOf);
                bool on = rel == "on";
                int inBelow = 0, inLayer = 0;
                for (int j = 0; j < n; j++)
                {
                    if (r.Layer[j] == r.Layer[i]) inLayer++;
                    if (r.Layer[j] == r.Layer[i] - 1) inBelow++;
                }
                var c = new Contact { Key = key, Kind = on ? "on" : "beside", Lower = classOf(r.Id[p]), Upper = classOf(r.Id[i]) };
                if (on)
                {
                    c.OnSingle = inBelow < 2;
                    c.OnPair = inBelow >= 2;
                }
                else
                {
                    c.Arrangements.Add(rel);
                }
                list.Add(new KeyValuePair<string, Contact>(key, c));
                if (on && inLayer == 1 && inBelow >= 2)
                {
                    string ci = classOf(r.Id[i]);
                    list.Add(new KeyValuePair<string, Contact>("onpair:" + ci, new Contact { Key = "onpair:" + ci, Kind = "onpair", Lower = ci, Upper = classOf(r.Id[p]) }));
                }
            }
            return list;
        }

        private static bool IsTuned(Contact c, CargoPacker.Lookup L)
        {
            switch (c.Kind)
            {
                case "alone":
                {
                    CargoLayout.ItemEntry e = L.ForItem(c.Lower);
                    return e != null && e.HasAlone;
                }
                case "onpair":
                {
                    CargoLayout.ItemEntry e = L.ForItem(c.Lower);
                    return e != null && e.OnPairTuned;
                }
                default:
                {
                    CargoLayout.PairEntry p = L.ForPair(c.Lower, c.Upper);
                    return p != null && (c.Kind == "on" ? p.TopTuned : p.BesideTuned);
                }
            }
        }

        // The fewest loads that together show every contact but the one-item ones (those are the one-item carts):
        // greedy, the load adding the most contacts not shown yet; ties to fewer items, then the enumeration order.
        private static void Cover(Report rep)
        {
            var left = new HashSet<string>(StringComparer.Ordinal);
            foreach (Contact c in rep.Contacts) if (c.Kind != "alone") left.Add(c.Key);
            while (left.Count > 0)
            {
                int best = -1, bestGain = 0;
                for (int i = 0; i < rep.Loads.Count; i++)
                {
                    Load ld = rep.Loads[i];
                    if (ld.Classes.Length < 2) continue;
                    int gain = 0;
                    foreach (string key in ld.Keys) if (left.Contains(key)) gain++;
                    if (gain > bestGain || (gain == bestGain && gain > 0 && ld.Classes.Length < rep.Loads[best].Classes.Length))
                    {
                        best = i;
                        bestGain = gain;
                    }
                }
                if (best < 0) break; // cannot happen: every contact came from a load
                rep.Cover.Add(best);
                foreach (string key in rep.Loads[best].Keys) left.Remove(key);
            }
        }

        // Tunings that no load uses: a tuned on-top or beside relation of a pair whose classes never meet that way, an
        // onpair offset on a class that never lies alone on a pair, an entry that matches no carryable class.
        private static void FindDead(Report rep, CargoPacker.Lookup L, IEnumerable<CargoLayout.PairEntry> pairs, IEnumerable<CargoLayout.ItemEntry> items)
        {
            string ClassFor(string id)
            {
                foreach (string c in rep.Classes) if (L.SameClass(c, id)) return c;
                return null;
            }
            if (pairs != null)
            {
                foreach (CargoLayout.PairEntry p in pairs)
                {
                    if (p == null || (!p.TopTuned && !p.BesideTuned)) continue;
                    string a = ClassFor(p.A), b = ClassFor(p.B);
                    if (a == null || b == null)
                    {
                        rep.Dead.Add($"[pair {p.A}+{p.B}]: {(a == null ? p.A : p.B)} is in no carryable size class; the entry is never used.");
                        continue;
                    }
                    string sorted = string.CompareOrdinal(a, b) <= 0 ? a + "+" + b : b + "+" + a;
                    if (p.TopTuned && !rep.ByKey.ContainsKey("on:" + sorted))
                        rep.Dead.Add($"[pair {p.A}+{p.B}]: its on-top tuning (top_*) is never used: no load of up to {rep.MaxItems} puts one of these on the other.");
                    if (p.BesideTuned && !rep.ByKey.ContainsKey("beside:" + sorted))
                        rep.Dead.Add($"[pair {p.A}+{p.B}]: its beside tuning (beside_*) is never used: these two never lie in one layer.");
                }
            }
            if (items != null)
            {
                foreach (CargoLayout.ItemEntry e in items)
                {
                    if (e == null || !e.OnPairTuned) continue;
                    string c = ClassFor(e.Id);
                    if (c == null || !rep.ByKey.ContainsKey("onpair:" + c))
                        rep.Dead.Add($"[item {e.Id}]: its onpair offset is never used: {(c == null ? "no carryable size class" : "it never lies alone on a pair")}.");
                }
            }
        }

        // One line per contact and per cover load, for the log and for reading the file without a browser.
        internal static string Text(Report rep)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{rep.Tuned} of {rep.Total} contacts tuned; {rep.Loads.Count} loads of 1 to {rep.MaxItems} items over {rep.Classes.Count} size classes; cover {rep.Cover.Count} loads.");
            foreach (Contact c in rep.Contacts)
                sb.AppendLine($"  {(c.Tuned ? "tuned  " : "UNTUNED")} {c.Title,-40} {c.Loads,5} loads   smallest: {rep.Describe(c.Smallest)}");
            foreach (string d in rep.Dead) sb.AppendLine("  dead: " + d);
            return sb.ToString();
        }
    }
}
