// PackerScan --review: the cargo review store (CargoReview.cs) offline, on the repo's own layout: load keys are stable
// and unique over the 1,715 loads; a fingerprint changes when a value a load depends on changes and only then; the
// file's text reads back unchanged; the saved position survives.
using System.Reflection;
using GK2DonkeyAPI;
using UnityEngine;

static class Review
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

    static Review()
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

    // --review-check: the repo's cargo-review.cfg against the layout as it is now. Every ok of a fully tuned load must
    // carry the fingerprint the layout gives today (the game wrote it, the scan recomputes it: the two paths agree, and
    // nothing was edited since); an ok whose fingerprint differs is listed (it shows as 'to re-check' in the panel).
    public static int CheckFile(string cfgPath)
    {
        var (rep, fi, fp) = Build(cfgPath);
        var T = typeof(CargoLayout).Assembly.GetType("GK2DonkeyAPI.CargoReview")!;
        var keyOf = T.GetMethod("KeyOf", BindingFlags.NonPublic | BindingFlags.Static)!;
        var fingerprint = T.GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!;
        var fingerprintText = T.GetMethod("FingerprintText", BindingFlags.NonPublic | BindingFlags.Static)!;
        bool verbose = Environment.GetCommandLineArgs().Contains("--verbose");
        var allTuned = T.GetMethod("AllTuned", BindingFlags.NonPublic | BindingFlags.Static)!;
        var parse = T.GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!;
        var all = T.GetProperty("All", BindingFlags.NonPublic | BindingFlags.Static)!;
        string reviewPath = Path.Combine(Path.GetDirectoryName(cfgPath)!, "cargo-review.cfg");
        if (!File.Exists(reviewPath)) { Console.WriteLine("no cargo-review.cfg next to the layout"); return 1; }
        parse.Invoke(null, new object[] { File.ReadAllText(reviewPath) });
        var byKey = rep.Loads.ToDictionary(l => (string)keyOf.Invoke(null, new object[] { rep, l.Classes })!);
        int ok = 0, same = 0, stale = 0, untunedOk = 0, unknown = 0, wrong = 0, later = 0;
        foreach (object m in (System.Collections.IEnumerable)all.GetValue(null)!)
        {
            var mt = m.GetType();
            string key = (string)mt.GetField("Key")!.GetValue(m)!;
            string verdict = mt.GetField("Verdict")!.GetValue(m)!.ToString()!;
            string stored = (string)mt.GetField("Fingerprint")!.GetValue(m)!;
            if (!byKey.TryGetValue(key, out var load)) { unknown++; Console.WriteLine($"  unknown load key: {key}"); continue; }
            if (verdict == "Wrong") { wrong++; continue; }
            if (verdict == "Later") { later++; continue; }
            ok++;
            bool tuned = (bool)allTuned.Invoke(null, new object[] { rep, load, fi, fp })!;
            if (!tuned) { untunedOk++; Console.WriteLine($"  ok but a contact is untuned (fallbacks differ offline, not compared): {key}"); continue; }
            var fargs = new object[] { rep, load, fi, fp, "fb", CargoLayout.OnPairs, (Func<string, string, bool>)((a, b) => C(a) == C(b)) };
            string now = (string)fingerprint.Invoke(null, fargs)!;
            if (now == stored) same++;
            else
            {
                stale++;
                Console.WriteLine($"  to re-check: {key} (stored {stored}, now {now})");
                // the same line the panel logs ([review] at / ok), to compare with LogOutput.log
                if (verbose) Console.WriteLine($"    [review] scan {key}: stored {stored}, now {now} <- {(string)fingerprintText.Invoke(null, fargs)!}");
            }
        }
        Console.WriteLine($"{ok} ok marks: {same} match the layout now, {stale} to re-check, {untunedOk} not comparable; {wrong} wrong, {later} later, {unknown} unknown keys");
        return unknown == 0 ? 0 : 1;
    }

    // --review-migrate [--write]: brings the review file's ok marks from fingerprint v2 to v3 (2026-10-15: the class
    // offsets in id order). v2 listed them in the analysis's class order, which differs between the game and this scan,
    // so a v2 fingerprint is checked against every order of the class section: an ok whose stored fingerprint one of
    // them reproduces is proven unchanged since it was marked and gets today's v3 fingerprint; any other ok keeps its
    // old one and shows as to re-check. Without --write nothing is written. The file is replaced atomically, the old
    // one kept as cargo-review.cfg.v2.bak.
    public static int Migrate(string cfgPath, bool write)
    {
        var (rep, fi, fp) = Build(cfgPath);
        var T = typeof(CargoLayout).Assembly.GetType("GK2DonkeyAPI.CargoReview")!;
        var keyOf = T.GetMethod("KeyOf", BindingFlags.NonPublic | BindingFlags.Static)!;
        var text = T.GetMethod("FingerprintText", BindingFlags.NonPublic | BindingFlags.Static)!;
        var hash = T.GetMethod("Hash", BindingFlags.NonPublic | BindingFlags.Static)!;
        var allTuned = T.GetMethod("AllTuned", BindingFlags.NonPublic | BindingFlags.Static)!;
        var parse = T.GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!;
        var toText = T.GetMethod("ToText", BindingFlags.NonPublic | BindingFlags.Static)!;
        var all = T.GetProperty("All", BindingFlags.NonPublic | BindingFlags.Static)!;
        var fileVer = T.GetProperty("FileFingerprintVersion", BindingFlags.NonPublic | BindingFlags.Static)!;
        int codeVer = (int)T.GetField("FingerprintVersion", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        string reviewPath = Path.Combine(Path.GetDirectoryName(cfgPath)!, "cargo-review.cfg");
        string before = File.ReadAllText(reviewPath);
        parse.Invoke(null, new object[] { before });
        int fromVer = (int)fileVer.GetValue(null)!;
        if (fromVer == codeVer) { Console.WriteLine($"the file is already v{codeVer}: nothing to do"); return 0; }
        if (fromVer != 2 || codeVer != 3) { Console.WriteLine($"this migration is v2 -> v3 only (file v{fromVer}, code v{codeVer}): nothing written"); return 1; }
        var keys = rep.Loads.Select(l => (string)keyOf.Invoke(null, new object[] { rep, l.Classes })!).ToList();
        var byKey = rep.Loads.Zip(keys).ToDictionary(z => z.Second, z => z.First);
        Func<string, string, bool> same = (a, b) => C(a) == C(b);
        int ok = 0, proven = 0, notComparable = 0;
        var unproven = new List<string>();
        var marks = ((System.Collections.IEnumerable)all.GetValue(null)!).Cast<object>().ToList();
        foreach (object m in marks)
        {
            var mt = m.GetType();
            if (mt.GetField("Verdict")!.GetValue(m)!.ToString() != "Ok") continue;
            ok++;
            string key = (string)mt.GetField("Key")!.GetValue(m)!;
            string stored = (string)mt.GetField("Fingerprint")!.GetValue(m)!;
            var load = byKey[key];
            if (!(bool)allTuned.Invoke(null, new object[] { rep, load, fi, fp })!) { notComparable++; unproven.Add(key + " (a contact is untuned: the fallbacks differ offline)"); continue; }
            string now = (string)text.Invoke(null, new object[] { rep, load, fi, fp, "fb", CargoLayout.OnPairs, same })!;
            // split the text into its parts: the class section is the run of "cls:" parts
            var parts = now.Split(';').Where(x => x.Length > 0).Select(x => x + ";").ToList();
            var cls = parts.Where(x => x.StartsWith("cls:")).ToList();
            int first = parts.FindIndex(x => x.StartsWith("cls:"));
            var head = parts.Take(first).ToList();
            var tail = parts.Skip(first + cls.Count).ToList();
            bool match = false;
            foreach (var perm in Perms(cls))
            {
                string old = string.Concat(head) + string.Concat(perm) + string.Concat(tail);
                if ((string)hash.Invoke(null, new object[] { old })! == stored) { match = true; break; }
            }
            if (match)
            {
                proven++;
                mt.GetField("Fingerprint")!.SetValue(m, (string)hash.Invoke(null, new object[] { now })!);
            }
            else unproven.Add(key);
        }
        Console.WriteLine($"v{fromVer} -> v{codeVer}: {ok} ok marks, {proven} proven unchanged (re-stamped), {unproven.Count} left as to re-check ({notComparable} not comparable offline)");
        foreach (string k in unproven) Console.WriteLine("  to re-check: " + k);
        if (!write) { Console.WriteLine("dry run: nothing written (add --write)"); return 0; }
        fileVer.GetSetMethod(true)!.Invoke(null, new object[] { codeVer });
        string after = (string)toText.Invoke(null, new object[] { keys })!;
        // every mark is still there: same number of mark lines before and after
        int Lines(string t) => t.Split('\n').Count(l => l.Contains(" = ") && !l.TrimStart().StartsWith("#") && !l.StartsWith("position") && !l.StartsWith("fingerprint-version"));
        if (Lines(after) != Lines(before)) { Console.WriteLine($"ABORT: {Lines(before)} mark lines before, {Lines(after)} after; nothing written"); return 1; }
        string tmp = reviewPath + ".tmp";
        File.WriteAllText(tmp, after);
        File.Replace(tmp, reviewPath, reviewPath + ".v2.bak");
        Console.WriteLine($"written: {reviewPath} (the old file is {reviewPath}.v2.bak)");
        return 0;
    }

    static IEnumerable<List<string>> Perms(List<string> xs)
    {
        if (xs.Count <= 1) { yield return new List<string>(xs); yield break; }
        for (int i = 0; i < xs.Count; i++)
        {
            var rest = xs.Where((_, j) => j != i).ToList();
            foreach (var p in Perms(rest)) { p.Insert(0, xs[i]); yield return p; }
        }
    }

    static (CargoCoverage.Report, Func<string, CargoLayout.ItemEntry>, Func<string, string, CargoLayout.PairEntry>) Build(string cfgPath)
    {
        string text = File.ReadAllText(cfgPath);
        typeof(CargoLayout).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { text });
        var items = CargoLayout.Items.Values.ToList();
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
        var rep = CargoCoverage.Analyse(Box.Keys.ToList(), 6, null, L, S, pairs, items, id => Name[C(id)]);
        return (rep, ForItem, ForPair);
    }

    public static int Run(string cfgPath)
    {
        int pass = 0, fail = 0;
        void Check(bool ok, string what)
        {
            if (ok) pass++; else fail++;
            Console.WriteLine((ok ? "PASS " : "FAIL ") + what);
        }
        string text = File.ReadAllText(cfgPath);
        typeof(CargoLayout).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { text });
        var items = CargoLayout.Items.Values.ToList();
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

        // the store, through reflection (internal in the DLL, as the panel uses it)
        var T = typeof(CargoLayout).Assembly.GetType("GK2DonkeyAPI.CargoReview")!;
        var keyOf = T.GetMethod("KeyOf", BindingFlags.NonPublic | BindingFlags.Static)!;
        var fingerprint = T.GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!;
        var parse = T.GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!;
        var toText = T.GetMethod("ToText", BindingFlags.NonPublic | BindingFlags.Static)!;
        var set = T.GetMethod("Set", BindingFlags.NonPublic | BindingFlags.Static)!;
        var get = T.GetMethod("Get", BindingFlags.NonPublic | BindingFlags.Static)!;
        var verdictT = T.GetNestedType("Verdict", BindingFlags.NonPublic)!;
        var posProp = T.GetProperty("LastPositionKey", BindingFlags.NonPublic | BindingFlags.Static)!;
        Func<string, CargoLayout.ItemEntry> fi = ForItem;
        Func<string, string, CargoLayout.PairEntry> fp = ForPair;
        string Key(CargoCoverage.Load ld) => (string)keyOf.Invoke(null, new object[] { rep, ld.Classes })!;
        Func<string, string, bool> same = Same;
        var ops = new List<CargoLayout.OnPairEntry>(CargoLayout.OnPairs);
        string Fp(CargoCoverage.Load ld) => (string)fingerprint.Invoke(null, new object[] { rep, ld, fi, fp, "fb", ops, same })!;
        string FpNone(CargoCoverage.Load ld) => (string)fingerprint.Invoke(null, new object[] { rep, ld, fi, fp, "fb", null!, null! })!;

        // 1. keys: one per load, unique, class order independent, readable
        var keys = rep.Loads.Select(Key).ToList();
        Check(keys.Distinct().Count() == keys.Count && keys.Count == rep.Loads.Count, $"every load has its own key ({keys.Count} loads)");
        Check(keys.All(k => k.Split('+').All(n => Name.Values.Contains(n))), "keys are the readable class names, sorted");
        var threeLogs = rep.Loads.First(l => l.Classes.Length == 3 && l.Classes.All(c => rep.Classes[c] == "wood"));
        Check(Key(threeLogs) == "log+log+log", $"three logs read 'log+log+log' ({Key(threeLogs)})");

        // 2. fingerprints: deterministic; one contact's value changes exactly the loads that use it
        var fps = rep.Loads.Select(Fp).ToList();
        Check(rep.Loads.Select(Fp).SequenceEqual(fps), "fingerprints are deterministic");
        var pair = pairs.First(p => p.TopTuned);
        var usesTop = rep.Loads.Where(l => l.Keys.Any(k =>
        {
            var c = rep.ByKey[k];
            return c.Kind == "on" && ((Same(c.Lower, pair.A) && Same(c.Upper, pair.B)) || (Same(c.Lower, pair.B) && Same(c.Upper, pair.A)));
        })).ToList();
        Vector3 was = pair.TopB;
        pair.TopB += new Vector3(0f, 0.01f, 0f);
        var fps2 = rep.Loads.Select(Fp).ToList();
        pair.TopB = was;
        int changed = 0, unchanged = 0, wrongChange = 0;
        var usesSet = new HashSet<CargoCoverage.Load>(usesTop);
        for (int i = 0; i < rep.Loads.Count; i++)
        {
            bool diff = fps[i] != fps2[i];
            if (usesSet.Contains(rep.Loads[i])) { if (diff) changed++; else wrongChange++; }
            else { if (!diff) unchanged++; else wrongChange++; }
        }
        Check(wrongChange == 0 && changed == usesTop.Count && changed > 0, $"a 1 cm change to {Name[C(pair.B)]} on {Name[C(pair.A)]} changes exactly the {usesTop.Count} loads that use it ({changed} changed, {unchanged} unchanged, {wrongChange} wrong)");
        Check(rep.Loads.Select(Fp).SequenceEqual(fps), "put back, the fingerprints are as before");
        // a class offset changes every load holding the class
        var logEntry = ForItem("wood");
        if (logEntry != null)
        {
            Vector3 b = logEntry.Base;
            logEntry.Base += new Vector3(0.01f, 0f, 0f);
            var fps3 = rep.Loads.Select(Fp).ToList();
            logEntry.Base = b;
            int withLog = 0, bad = 0;
            for (int i = 0; i < rep.Loads.Count; i++)
            {
                bool holds = rep.Loads[i].Classes.Any(c => rep.Classes[c] == "wood");
                if (holds) withLog++;
                if (holds != (fps[i] != fps3[i])) bad++;
            }
            Check(bad == 0, $"a log class offset changes exactly the {withLog} loads with a log ({bad} wrong)");
        }

        // 3. the file: marks written in the loads' order, read back the same; the position kept
        object ok = Enum.Parse(verdictT, "Ok"), wrong = Enum.Parse(verdictT, "Wrong"), later = Enum.Parse(verdictT, "Later");
        parse.Invoke(null, new object[] { "" });
        set.Invoke(null, new object[] { keys[5], ok, fps[5], "", "" });
        set.Invoke(null, new object[] { keys[2], wrong, fps[2], "floats,sinks", "a note | with a bar" });
        set.Invoke(null, new object[] { keys[9], later, "", "", "" });
        posProp.SetValue(null, keys[9]);
        string t1 = (string)toText.Invoke(null, new object[] { keys })!;
        parse.Invoke(null, new object[] { t1 });
        string t2 = (string)toText.Invoke(null, new object[] { keys })!;
        Check(t1 == t2, "the review file's text reads back unchanged");
        var lines = t1.Split('\n').Where(l => l.Contains(" = ") && !l.StartsWith("#") && !l.StartsWith("position") && !l.StartsWith("fingerprint-version")).ToList();
        Check(lines.Count == 3 && lines[0].StartsWith(keys[2]) && lines[1].StartsWith(keys[5]) && lines[2].StartsWith(keys[9]), "marks are written in the loads' order");
        var m2 = get.Invoke(null, new object[] { keys[2] })!;
        var mT = m2.GetType();
        Check((string)mT.GetField("Tags")!.GetValue(m2)! == "floats,sinks" && ((string)mT.GetField("Note")!.GetValue(m2)!).Contains("a note / with a bar"), "tags and the note come back (a bar in the note is replaced)");
        Check((string)posProp.GetValue(null)! == keys[9], "the saved position comes back");
        var m5 = get.Invoke(null, new object[] { keys[5] })!;
        Check((string)mT.GetField("Fingerprint")!.GetValue(m5)! == fps[5], "an ok keeps its fingerprint");
        // the fingerprint format's version: a file without the line is v1; a re-stamp brings it to the code's version
        var fileVer = T.GetProperty("FileFingerprintVersion", BindingFlags.NonPublic | BindingFlags.Static)!;
        int codeVer = (int)T.GetField("FingerprintVersion", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        parse.Invoke(null, new object[] { "position = x\n" + keys[5] + " = ok | d | oldfp |  | \n" });
        Check((int)fileVer.GetValue(null)! == 1, "a file without a version line is v1");
        var restamp = T.GetMethod("Restamp", BindingFlags.NonPublic | BindingFlags.Static)!;
        var fpByKey = rep.Loads.ToDictionary(Key, Fp);
        int n = (int)restamp.Invoke(null, new object[] { (Func<string, string>)(k => fpByKey.TryGetValue(k, out var f) ? f : null) })!;
        var m5b = get.Invoke(null, new object[] { keys[5] })!;
        Check(n == 1 && (string)mT.GetField("Fingerprint")!.GetValue(m5b)! == fps[5] && (int)fileVer.GetValue(null)! == codeVer, $"re-stamp gives the ok today's fingerprint and the file the code's version v{codeVer}");
        string t4 = (string)toText.Invoke(null, new object[] { keys })!;
        Check(t4.Contains("fingerprint-version = " + codeVer), "the version line is written");

        // 4. one-pair offsets ([onpair <item> on <a>+<b>], 2026-10-15): none gives the fingerprint exactly as before they
        // existed; one changes exactly the loads it fits in; the packer moves the lone item on that pair only; the
        // file reads back.
        Check(ops.Count > 0 || rep.Loads.All(l => Fp(l) == FpNone(l)), "without one-pair offsets the fingerprints are as before they existed");
        var lowOnLows = new CargoLayout.OnPairEntry { Item = "box_appliances_1", A = "box_appliances_1", B = "box_appliances_1", Offset = new Vector3(0.05f, 0f, -0.04f) };
        var before = rep.Loads.Select(Fp).ToList();
        ops.Add(lowOnLows);
        var after = rep.Loads.Select(Fp).ToList();
        ops.Remove(lowOnLows);
        int fitBad = 0, fitN = 0;
        for (int k = 0; k < rep.Loads.Count; k++)
        {
            bool fits = rep.Loads[k].Classes.Count(c => rep.Classes[c] == "box_appliances_1") >= 3;
            if (fits) fitN++;
            if (fits != (before[k] != after[k])) fitBad++;
        }
        Check(fitBad == 0 && fitN > 0, $"a one-pair offset for low crate on two low crates changes exactly the {fitN} loads with three low crates or more ({fitBad} wrong)");
        CargoPacker.Item It(string id, int o) => new CargoPacker.Item { Id = id, Box = Box[C(id)], LoadOrder = o };
        int LoneOf(CargoPacker.Result r)
        {
            int top = r.Layer.Max();
            var inTop = Enumerable.Range(0, r.Count).Where(q => r.Layer[q] == top).ToList();
            return inTop.Count == 1 && top > 0 && Enumerable.Range(0, r.Count).Count(q => r.Layer[q] == top - 1) == 2 ? inTop[0] : -1;
        }
        var threeLow = new List<CargoPacker.Item> { It("box_appliances_1", 0), It("box_appliances_2", 1), It("box_tools_iron", 2) };
        var lowOnCorpses = new List<CargoPacker.Item> { It("body_corpse", 0), It("body_corpse", 1), It("box_appliances_1", 2) };
        var a0 = CargoPacker.Pack(threeLow, S, L);
        var b0 = CargoPacker.Pack(lowOnCorpses, S, L);
        var opsL = new List<CargoLayout.OnPairEntry> { lowOnLows };
        L.ForOnPair = (it, a, b) => CargoLayout.FindOnPair(opsL, it, a, b, Same);
        var a1 = CargoPacker.Pack(threeLow, S, L);
        var b1 = CargoPacker.Pack(lowOnCorpses, S, L);
        L.ForOnPair = null;
        int la = LoneOf(a0), lb = LoneOf(b0);
        var cls = ForItem("box_appliances_1");
        Vector3 classOff = cls != null ? cls.OnPair : Vector3.zero;
        Vector3 moved = la >= 0 ? a1.Target[la] - a0.Target[la] : new Vector3(9f, 9f, 9f);
        Vector3 expect = lowOnLows.Offset - classOff;
        Check(la >= 0 && (moved - expect).magnitude < 0.0005f && Enumerable.Range(0, a0.Count).Where(q => q != la).All(q => a1.Target[q] == a0.Target[q]),
            $"on three low crates the top crate moves by the one-pair offset less the class's ({moved.x:0.000}, {moved.y:0.000}, {moved.z:0.000}), nothing else moves");
        Check(lb >= 0 && Enumerable.Range(0, b0.Count).All(q => b1.Target[q] == b0.Target[q]), "a low crate alone on two corpses does not move");
        // Restore (not Parse) clears the file order of the items too, so a second read does not list them twice
        var restoreL = typeof(CargoLayout).GetMethod("Restore", BindingFlags.NonPublic | BindingFlags.Static)!;
        restoreL.Invoke(null, new object[] { text, false });
        CargoLayout.OnPairs.Add(new CargoLayout.OnPairEntry { Item = lowOnLows.Item, A = lowOnLows.A, B = lowOnLows.B, Offset = lowOnLows.Offset });
        string lt1 = CargoLayout.ToText();
        restoreL.Invoke(null, new object[] { lt1, false });
        string lt2 = CargoLayout.ToText();
        var back = CargoLayout.OnPairs.FirstOrDefault(o => o.Item == "box_appliances_1");
        if (lt1 != lt2 || CargoLayout.OnPairs.Count != ops.Count + 1) Console.WriteLine($"  round trip: same text {lt1 == lt2}, {CargoLayout.OnPairs.Count} one-pair offsets read back (expected {ops.Count + 1}), back {(back == null ? "none" : back.Offset.ToString("F3"))}");
        Check(lt1 == lt2 && CargoLayout.OnPairs.Count == ops.Count + 1 && back != null && back.Offset == lowOnLows.Offset && lt1.Contains("[onpair box_appliances_1 on box_appliances_1+box_appliances_1]"),
            "a one-pair offset is written as [onpair <item> on <a>+<b>] and reads back unchanged");

        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
