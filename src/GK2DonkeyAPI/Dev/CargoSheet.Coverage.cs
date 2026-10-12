using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // The coverage runs (docs/cargo-coverage.md): how far the cargo layout is tuned over every load the cart can carry.
    //   status.html   instant, no pictures, no ride needed: every contact, tuned or not, how many loads use it, what
    //                 to tune next, the fewest loads showing everything, tunings no load uses (CargoCoverage).
    //   coverage.html the same with pictures: every contact in the smallest load that shows it (its two items tinted
    //                 blue below / first, orange on top / second, the rest untinted), an on-top contact also resting on a
    //                 pair, and the covering loads in the Cargo tab's colours. Each picture is checked against the
    //                 analysis: the contacts the cart really built must be the ones predicted.
    //   all.html      every load, in the Cargo tab's colours, with filters (size, class, contact, untuned only).
    internal static partial class CargoSheet
    {
        internal const int CoverageMaxItems = 6; // the cart's most (CartSlots' range), whatever it is set to now

        private static List<string> classRepsCache;

        // One item id per size class, the zombie class included (its box is the game's lying zombie).
        internal static List<string> ClassReps()
        {
            if (classRepsCache == null || classRepsCache.Count == 0)
            {
                List<string> reps = Representatives();
                reps.Add(ZombieRep);
                classRepsCache = reps;
            }
            return classRepsCache;
        }

        // Readable names for the size classes, by a member the class holds (the representative the sheet picks is just the
        // first item of the class in the game's list, e.g. box_appliances_1 for the fifteen low crates). A class with
        // none of these members keeps its representative's id.
        private static readonly string[][] ClassNames =
        {
            new[] { "wood", "log" },
            new[] { "box_flour", "flour sack" },
            new[] { "box_beer", "tall crate" },
            new[] { "box_appliances_1", "low crate" },
            new[] { "stone", "slab" },
            new[] { "body_corpse", "corpse" },
        };

        private static readonly Dictionary<string, string> labelCache = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static string ClassLabel(string itemId)
        {
            string rep = ClassOf(itemId);
            if (labelCache.TryGetValue(rep, out string name)) return name;
            name = rep == ZombieRep ? "zombie" : rep;
            if (rep != ZombieRep)
            {
                foreach (string[] kv in ClassNames)
                {
                    if (CargoLayout.SameClass(kv[0], rep))
                    {
                        name = kv[1];
                        break;
                    }
                }
            }
            labelCache[rep] = name;
            return name;
        }

        // The items of a class (for the pages' legend).
        internal static List<string> ClassMembers(string rep) => rep == ZombieRep ? new List<string> { "every zombie" } : Members(rep);

        private static readonly Dictionary<string, string> classOfCache = new Dictionary<string, string>(StringComparer.Ordinal);

        // The class (its representative's id) an item belongs to.
        internal static string ClassOf(string itemId)
        {
            if (itemId == null) return "";
            if (classOfCache.TryGetValue(itemId, out string c)) return c;
            c = itemId;
            if (CargoLayout.IsZombieId(itemId) || itemId == ZombieRep)
            {
                c = ZombieRep;
            }
            else
            {
                foreach (string r in ClassReps())
                {
                    if (r != ZombieRep && CargoLayout.SameClass(r, itemId))
                    {
                        c = r;
                        break;
                    }
                }
            }
            classOfCache[itemId] = c;
            return c;
        }

        private static CargoPacker.Settings CoverageSettings()
        {
            StyleSettings s = Plugin.For(MountStyle.Cart);
            Cargo.Rig rig = Cargo.Current;
            return new CargoPacker.Settings
            {
                Floor = s.CargoBedDepth.Value,
                Gap = s.CargoSideGap.Value,
                Overhang = s.CargoOverhang.Value,
                Nestle = s.CargoNestle.Value,
                LayerShift = s.CargoLayerShift.Value,
                BedWidth = rig != null ? rig.BedWidth : 0.75f,
                BedLength = rig != null ? rig.BedLength : 0.9f,
                Forced = null,
            };
        }

        // The analysis packs on the boxes: which contacts occur does not depend on the seat pass (it moves layers up and
        // down only), and it runs 1,715 packs whenever the layout changes. The scan packs as the cart does.
        private static readonly CargoPacker.Lookup CoverageLookup = new CargoPacker.Lookup
        {
            ForItem = CargoLayout.ForItem,
            ForPair = CargoLayout.ForPair,
            FirstIsA = CargoLayout.FirstIsA,
            SameClass = CargoLayout.SameClass,
            BoxOf = CargoLayout.BoxOf,
        };

        private static readonly CargoPacker.Lookup ScanLookup = new CargoPacker.Lookup
        {
            ForItem = CargoLayout.ForItem,
            ForPair = CargoLayout.ForPair,
            FirstIsA = CargoLayout.FirstIsA,
            SameClass = CargoLayout.SameClass,
            BoxOf = CargoLayout.BoxOf,
            ShapeOf = id => Plugin.CargoSeatOnShapes.Value ? ShapeOf(id) : null,
        };

        // zombieCap: how many zombies one load may hold (the stand-ins a photo run could make; no limit for the status).
        internal static CargoCoverage.Report AnalyseNow(int zombieCap = CoverageMaxItems)
        {
            CargoPacker.Settings st = CoverageSettings();
            CargoCoverage.Report rep = CargoCoverage.Analyse(ClassReps(), CoverageMaxItems, id => id == ZombieRep ? zombieCap : CoverageMaxItems,
                CoverageLookup, st, CargoLayout.Pairs, CargoLayout.Items.Values, ClassLabel);
            rep.Signature = Signature(st);
            return rep;
        }

        // ------------------------------------------------------------------ the shape scan (CargoShapes.Judge)

        internal static CargoShapes.Shape ShapeOf(string itemId) => CargoShapes.ByClass(ClassOf(itemId));

        internal sealed class Wrong
        {
            public string Key, Kind, Title;
            public bool Tuned;
            public int Loads;
            public float Worst;
            public int[] WorstLoad;
        }

        // Every load packed again and every item judged by the measured shapes; per contact and kind (floats / sinks /
        // hangs), how many loads and the worst one. Empty without measured shapes.
        internal static List<Wrong> ScanAll(CargoCoverage.Report rep, out int badLoads)
        {
            badLoads = 0;
            var byKey = new Dictionary<string, Wrong>(StringComparer.Ordinal);
            if (CargoShapes.Count == 0) return new List<Wrong>();
            CargoPacker.Settings st = CoverageSettings();
            foreach (CargoCoverage.Load ld in rep.Loads)
            {
                if (ld.Classes.Length < 2) continue;
                var input = new List<CargoPacker.Item>();
                for (int j = 0; j < ld.Classes.Length; j++)
                {
                    string id = rep.Classes[ld.Classes[j]];
                    input.Add(new CargoPacker.Item { Id = id, Box = CargoLayout.BoxOf(id), LoadOrder = j });
                }
                CargoPacker.Result r = CargoPacker.Pack(input, st, ScanLookup);
                bool bad = false;
                for (int i = 0; i < r.Count; i++)
                {
                    CargoShapes.Verdict v = CargoShapes.Judge(r, i, ShapeOf);
                    if (v == null) continue;
                    bad = true;
                    string key = CargoCoverage.KeyOf(r.Relation[i], r.Id[i], r.Id[r.Partner[i]], ClassOf);
                    string wk = key + "|" + v.Kind;
                    if (!byKey.TryGetValue(wk, out Wrong w))
                    {
                        rep.ByKey.TryGetValue(key, out CargoCoverage.Contact c);
                        byKey[wk] = w = new Wrong { Key = key, Kind = v.Kind, Title = c != null ? c.Title : key, Tuned = c != null && c.Tuned };
                    }
                    w.Loads++;
                    if (v.Amount >= w.Worst)
                    {
                        w.Worst = v.Amount;
                        w.WorstLoad = ld.Classes;
                    }
                }
                if (bad) badLoads++;
            }
            var list = new List<Wrong>(byKey.Values);
            list.Sort((a, b) => b.Loads != a.Loads ? b.Loads.CompareTo(a.Loads) : string.CompareOrdinal(a.Key, b.Key));
            return list;
        }

        // What a report depends on: the layout's entries and the fallback rules (the bed is measured once per cart).
        internal static string Signature(CargoPacker.Settings st) => $"{CargoLayout.Version}|{st.Floor:0.000}|{st.Gap:0.000}|{st.Overhang:0.000}|{st.Nestle:0.000}|{st.LayerShift:0.000}|{st.BedWidth:0.000}|{st.BedLength:0.000}";

        internal static string CurrentSignature() => Signature(CoverageSettings());

        // ------------------------------------------------------------------ the instant status page

        public static string StartStatus()
        {
            if (Running)
            {
                return Status = "a run is in progress";
            }
            try
            {
                CargoCoverage.Report rep = AnalyseNow(CoverageMaxItems);
                string dir = OutDir();
                Directory.CreateDirectory(dir);
                ClearOld(dir, "status");
                string path = Path.Combine(dir, "status.html");
                List<Wrong> wrong = ScanAll(rep, out int bad);
                WriteCoverageHtml(path, rep, null, "", photos: false, wrong, bad);
                File.WriteAllText(Path.Combine(dir, "status.txt"), CargoCoverage.Text(rep));
                Plugin.Log.LogInfo($"[coverage] {rep.Tuned} of {rep.Total} contacts tuned over {rep.Loads.Count} loads; written to {path}");
                return Status = "done: " + path;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[coverage] the status page failed: " + e);
                return Status = "status failed: " + e.Message;
            }
        }

        // ------------------------------------------------------------------ the photo runs

        public static string StartCoverage() => Status = Begin(RunCoverage(all: false));

        public static string StartAllLoads() => Status = Begin(RunCoverage(all: true));

        private sealed class Shot
        {
            public CargoCoverage.Load Load;
            public string Key;      // the contact highlighted; null: the Cargo tab's colours
            public string Role;     // "alone", "contact", "onpair-context", "cover", "all"
            public int LoadIndex;
            public string File;
        }

        private static List<Shot> CoverageShots(CargoCoverage.Report rep)
        {
            var shots = new List<Shot>();
            var index = new Dictionary<CargoCoverage.Load, int>();
            for (int i = 0; i < rep.Loads.Count; i++) index[rep.Loads[i]] = i;
            CargoCoverage.Load Find(int[] classes)
            {
                foreach (CargoCoverage.Load l in rep.Loads)
                {
                    if (l.Classes.Length == classes.Length && ((IStructuralEquatable)l.Classes).Equals(classes, StructuralComparisons.StructuralEqualityComparer)) return l;
                }
                return null;
            }
            foreach (CargoCoverage.Contact c in rep.Contacts)
            {
                CargoCoverage.Load l = Find(c.Smallest);
                if (l == null) continue;
                shots.Add(new Shot { Load = l, Key = c.Kind == "alone" ? null : c.Key, Role = c.Kind == "alone" ? "alone" : "contact", LoadIndex = index[l] });
                if (c.Kind == "on" && c.SmallestOnPair != null && !ReferenceEquals(c.SmallestOnPair, c.Smallest))
                {
                    CargoCoverage.Load lp = Find(c.SmallestOnPair);
                    if (lp != null) shots.Add(new Shot { Load = lp, Key = c.Key, Role = "onpair-context", LoadIndex = index[lp] });
                }
            }
            foreach (int i in rep.Cover)
            {
                shots.Add(new Shot { Load = rep.Loads[i], Key = null, Role = "cover", LoadIndex = i });
            }
            return shots;
        }

        private static List<Shot> AllShots(CargoCoverage.Report rep)
        {
            var shots = new List<Shot>();
            for (int i = 0; i < rep.Loads.Count; i++) shots.Add(new Shot { Load = rep.Loads[i], Key = null, Role = "all", LoadIndex = i });
            return shots;
        }

        // The contacts of item i in a pack on the cart, with the Cargo tab's keys (CargoCoverage.ContactsOf, live).
        private static List<string> LiveKeys(List<Cargo.PlacedItem> pl, int i)
        {
            var keys = new List<string>();
            Cargo.PlacedItem p = pl[i];
            if (p.Relation == "bed" || p.Relation == null || p.Partner < 0 || p.Partner >= pl.Count)
            {
                if (pl.Count == 1) keys.Add("alone:" + ClassOf(p.Id));
                return keys;
            }
            keys.Add(CargoCoverage.KeyOf(p.Relation, p.Id, pl[p.Partner].Id, ClassOf));
            if (p.Relation == "on")
            {
                int inLayer = 0, inBelow = 0;
                foreach (Cargo.PlacedItem o in pl)
                {
                    if (o.Layer == p.Layer) inLayer++;
                    if (o.Layer == p.Layer - 1) inBelow++;
                }
                if (inLayer == 1 && inBelow >= 2) keys.Add("onpair:" + ClassOf(p.Id));
            }
            return keys;
        }

        private static int FindContact(List<Cargo.PlacedItem> pl, string key)
        {
            for (int i = 0; i < pl.Count; i++)
            {
                if (LiveKeys(pl, i).Contains(key)) return i;
            }
            return -1;
        }

        // A contact's two items in the tab's colours (the lower / first item blue, the upper / second orange), the rest
        // untinted. An onpair contact: the lone item orange, its support blue.
        private static Func<List<Cargo.PlacedItem>, int, Color?> Highlight(string key)
        {
            return (pl, i) =>
            {
                int f = FindContact(pl, key);
                if (f < 0) return null;
                if (i == f) return Cargo.Rig.TintSecond;
                if (i == pl[f].Partner) return Cargo.Rig.TintFirst;
                return null;
            };
        }

        private static Color? TabColours(List<Cargo.PlacedItem> pl, int i) => Cargo.TintFor(pl[i], pl);

        private static IEnumerator RunCoverage(bool all)
        {
            Running = true;
            string dir = OutDir();
            var log = new StringBuilder();
            Inventory inv = null;
            try
            {
                Directory.CreateDirectory(dir);
                ClearOld(dir, all ? "all" : "coverage");
                inv = Mount.Donkey.Data.Inventory;
                // Nothing of the panel's view may stay on: the isolation shows two items only, the hide filters hide some.
                Cargo.ClearIsolation();
                Cargo.Rig.HideFirst = Cargo.Rig.HideSecond = false;
                Cargo.Rig.HiddenLayers.Clear();
                CargoLayout.ForcedRelation = null;
                Cargo.SlotFloor = CoverageMaxItems;
                Cargo.Current?.ApplySlots();
                MakeStandIns(CoverageMaxItems);
                int zcap = standIns.Count;
                if (zcap < CoverageMaxItems)
                {
                    log.AppendLine($"only {zcap} stand-in zombie(s) could be made: loads with more zombies are left out of this run.");
                }
                CargoCoverage.Report rep = AnalyseNow(zcap);
                List<Shot> shots = all ? AllShots(rep) : CoverageShots(rep);
                Plugin.Log.LogInfo($"[coverage] {(all ? "every load" : "the coverage set")}: {shots.Count} pictures of {rep.Loads.Count} loads, {rep.Tuned} of {rep.Total} contacts tuned.");
                int done = 0, mismatches = 0;
                foreach (Shot shot in shots)
                {
                    inv.Clear();
                    int zombie = 0;
                    bool complete = true;
                    foreach (int c in shot.Load.Classes)
                    {
                        string id = rep.Classes[c];
                        complete &= AddCell(inv, id, id == ZombieRep ? zombie++ : 0);
                    }
                    Cargo.Rig.SheetTint = shot.Key == null ? (Func<List<Cargo.PlacedItem>, int, Color?>)TabColours : Highlight(shot.Key);
                    Cargo.RefreshViews();
                    yield return null;
                    yield return null;
                    string name = rep.Describe(shot.Load.Classes);
                    var placed = new List<Cargo.PlacedItem>(Cargo.Placed);
                    if (!complete || placed.Count != shot.Load.Classes.Length)
                    {
                        log.AppendLine($"{name}: the cart shows {placed.Count} of {shot.Load.Classes.Length} items; picture skipped.");
                        done++;
                        continue;
                    }
                    // the cart's own contacts against the analysis
                    var live = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < placed.Count; i++) live.UnionWith(LiveKeys(placed, i));
                    var want = new HashSet<string>(shot.Load.Keys, StringComparer.Ordinal);
                    if (!live.SetEquals(want))
                    {
                        mismatches++;
                        if (mismatches <= 40)
                        {
                            log.AppendLine($"{name}: the cart built {string.Join(", ", Sorted(live))}; the analysis expected {string.Join(", ", Sorted(want))} (the measured boxes sort or fit differently from the class boxes).");
                        }
                    }
                    if (shot.Key != null && FindContact(placed, shot.Key) < 0)
                    {
                        log.AppendLine($"{name}: the contact {shot.Key} is not on the cart in this load; picture without highlight.");
                    }
                    yield return new WaitForEndOfFrame();
                    Bounds? extra = null;
                    if (Cargo.Current != null && Cargo.Current.CargoWorldBounds(out Bounds b)) extra = b;
                    string file = (all ? "all__" : "cov__") + done.ToString("D4") + "_" + shot.Role + (all ? ".jpg" : ".png");
                    string err = Capture(Path.Combine(dir, file), extra, all);
                    if (err != null)
                    {
                        log.AppendLine(file + ": " + err);
                    }
                    else
                    {
                        shot.File = file;
                    }
                    done++;
                    Status = $"{done} / {shots.Count}";
                }
                if (mismatches > 40)
                {
                    log.AppendLine($"... {mismatches - 40} more loads where the cart built other contacts than the analysis.");
                }
                string path = Path.Combine(dir, all ? "all.html" : "coverage.html");
                if (all)
                {
                    WriteAllHtml(path, rep, shots, log.ToString());
                }
                else
                {
                    List<Wrong> wrong = ScanAll(rep, out int bad);
                    WriteCoverageHtml(path, rep, shots, log.ToString(), photos: true, wrong, bad);
                    File.WriteAllText(Path.Combine(dir, "coverage.txt"), CargoCoverage.Text(rep));
                }
                Plugin.Log.LogInfo($"[coverage] {done} pictures written to {path}" + (log.Length > 0 ? " with notes:\n" + log : ""));
                Status = "done: " + path;
            }
            finally
            {
                if (inv != null)
                {
                    inv.Clear();
                }
                RemoveStandIns(all ? "every load photographed" : "the coverage run ended");
                Cargo.Rig.SheetTint = null;
                Running = false;
                Cargo.SlotFloor = 0;
                CargoLayout.ForcedRelation = null;
                Mount.ApplyCartSlots("the coverage run ended");
                Cargo.RefreshViews();
            }
        }

        private static List<string> Sorted(IEnumerable<string> keys)
        {
            var l = new List<string>(keys);
            l.Sort(string.CompareOrdinal);
            return l;
        }

        // ------------------------------------------------------------------ pages

        private static string H(string s) => WebUtility.HtmlEncode(s ?? "");

        private static string Pct(int part, int whole) => whole == 0 ? "0%" : $"{100.0 * part / whole:0}%";

        private const string PageStyle = "<style>body{font-family:sans-serif;background:#1e1e1e;color:#ddd;margin:12px 16px}h1{font-size:20px;margin:6px 0}details.legend summary{cursor:pointer;color:#9a9a9a;font-size:12px;margin:4px 0}h2{font-size:16px;margin:26px 0 8px;color:#ffd257}"
            + "table{border-collapse:collapse;margin:6px 0}td,th{border:1px solid #444;padding:3px 6px;font-size:12px;vertical-align:top;text-align:left}th{background:#333}"
            + ".ok{color:#8ee68e}.no{color:#ff8c69}.dim{color:#8a8a8a}.big{font-size:26px;font-weight:bold}.box{display:inline-block;border:1px solid #444;padding:8px 14px;margin:0 8px 8px 0;background:#262626}"
            + "img{display:block;max-width:var(--w,260px);height:auto;cursor:zoom-in;margin:2px 0}.pics{display:flex;flex-wrap:wrap;gap:6px}.fig{border:1px solid #444;padding:4px;background:#262626;max-width:var(--w,260px)}"
            + ".fig .cap{font-size:11px;color:#bbb}.chip{display:inline-block;border-radius:3px;padding:0 4px;margin:1px;font-size:11px}.chip.ok{background:#21402a}.chip.no{background:#4a2a20}"
            + "#big{position:fixed;inset:0;background:rgba(0,0,0,.88);display:none;align-items:center;justify-content:center;flex-direction:column;cursor:zoom-out;z-index:9}#big img{max-width:95vw;max-height:88vh;image-rendering:pixelated}#big div{margin-top:8px}"
            + "#bar{position:sticky;top:0;background:#1e1e1e;padding:6px 0;z-index:2;display:flex;flex-wrap:wrap;gap:10px;align-items:center;border-bottom:1px solid #333}#bar select,#bar input{background:#111;color:#ddd;border:1px solid #555}"
            + "pre{background:#111;padding:8px;white-space:pre-wrap}</style>";

        private const string PageScript = "<script>function show(src,cap){var b=document.getElementById('big');b.querySelector('img').src=src;b.querySelector('div').textContent=cap;b.style.display='flex';}"
            + "function setW(w){document.documentElement.style.setProperty('--w',w+'px');}</script><div id=\"big\" onclick=\"this.style.display='none'\"><img alt=\"\"><div></div></div>";

        private static string Img(string file, string caption) => file == null ? "<span class=\"dim\">(no picture)</span>" : $"<img loading=\"lazy\" src=\"{file}\" alt=\"{H(caption)}\" title=\"{H(caption)}\" onclick=\"show('{file}',this.title)\">";

        private static string Chip(CargoCoverage.Report rep, string key)
        {
            CargoCoverage.Contact c = rep.ByKey[key];
            return $"<span class=\"chip {(c.Tuned ? "ok" : "no")}\">{H(c.Title)}</span>";
        }

        private static string ClassLegend(CargoCoverage.Report rep)
        {
            var sb = new StringBuilder("<table><tr><th>Size class</th><th>Box (long × high × across)</th><th>Items in it</th></tr>");
            foreach (string c in rep.Classes)
            {
                Vector3 b = CargoLayout.BoxOf(c);
                sb.Append($"<tr><td><b>{H(rep.Label(c))}</b></td><td>{b.x:0.00} × {b.y:0.00} × {b.z:0.00}</td><td>{H(string.Join(", ", ClassMembers(c)))}</td></tr>");
            }
            return sb.Append("</table>").ToString();
        }

        private static string SettingsLine(CargoCoverage.Report rep)
        {
            CargoPacker.Settings st = CoverageSettings();
            return $"Fallbacks now: floor {st.Floor:0.00}, gap {st.Gap:0.00}, overhang {st.Overhang:0.00}, nestle {st.Nestle:0.00}, layer shift {st.LayerShift:0.00}; bed {st.BedLength:0.00} long × {st.BedWidth:0.00} wide. "
                + $"{rep.Classes.Count} size classes (below); the chests are not in it (a chest on the cart is the portable chest, a class of its own).";
        }

        private static void WriteCoverageHtml(string path, CargoCoverage.Report rep, List<Shot> shots, string problems, bool photos, List<Wrong> wrong = null, int badLoads = 0)
        {
            var picsByKey = new Dictionary<string, List<Shot>>(StringComparer.Ordinal);
            var coverShots = new Dictionary<int, Shot>();
            var aloneShots = new Dictionary<int, Shot>();
            if (shots != null)
            {
                foreach (Shot s in shots)
                {
                    if (s.Role == "cover") coverShots[s.LoadIndex] = s;
                    else if (s.Role == "alone") aloneShots[s.LoadIndex] = s;
                    else
                    {
                        if (!picsByKey.TryGetValue(s.Key, out List<Shot> l)) picsByKey[s.Key] = l = new List<Shot>();
                        l.Add(s);
                    }
                }
            }
            int fully = 0;
            int[] fullyBySize = new int[rep.LoadsBySize.Length];
            foreach (CargoCoverage.Load l in rep.Loads)
            {
                if (l.Untuned == 0)
                {
                    fully++;
                    fullyBySize[l.Classes.Length]++;
                }
            }
            var sb = new StringBuilder();
            string title = photos ? "Cargo coverage" : "Cargo tuning status";
            sb.AppendLine($"<!doctype html><html><head><meta charset=\"utf-8\"><title>{title}</title>{PageStyle}</head><body>{PageScript}");
            sb.AppendLine($"<h1>{title}</h1><p class=\"dim\">{DateTime.Now:yyyy-MM-dd HH:mm}. Every load of 1 to {rep.MaxItems} items the cart can carry ({rep.Loads.Count} loads: the order items are loaded in never changes how they lie, so a load is which items, not in what order) went through the cart's packer with the layout file as it is now. "
                + "A load is built from <b>contacts</b>: an item on the layer below (<i>x on y</i>), two items in one layer (<i>x beside y</i>), an item lying alone on top of a pair, and an item alone on the cart. Each contact is one tuning in the Cargo tab, used by every load it occurs in, so the contacts are the work. "
                + "Which contacts occur is exact; where the items end up also depends on the boxes measured on the cart, which only pictures show.</p>");
            sb.AppendLine($"<p class=\"dim\">{SettingsLine(rep)}</p>");
            sb.AppendLine(ClassLegend(rep));

            // summary
            sb.AppendLine("<div>");
            sb.AppendLine($"<div class=\"box\"><div class=\"big\">{rep.Tuned} / {rep.Total}</div>contacts tuned ({Pct(rep.Tuned, rep.Total)})</div>");
            sb.AppendLine($"<div class=\"box\"><div class=\"big\">{fully} / {rep.Loads.Count}</div>loads fully tuned ({Pct(fully, rep.Loads.Count)})</div>");
            foreach (string kind in new[] { "alone", "beside", "on", "onpair" })
            {
                int t = 0, n = 0;
                foreach (CargoCoverage.Contact c in rep.Contacts) if (c.Kind == kind) { n++; if (c.Tuned) t++; }
                string label = kind == "alone" ? "alone on the cart" : kind == "beside" ? "beside each other" : kind == "on" ? "on top of another" : "alone on a pair";
                sb.AppendLine($"<div class=\"box\"><div class=\"big\">{t} / {n}</div>{label}</div>");
            }
            sb.AppendLine("</div>");
            sb.Append("<table><tr><th>Items</th>");
            for (int n = 1; n <= rep.MaxItems; n++) sb.Append($"<th>{n}</th>");
            sb.Append("</tr><tr><td>loads</td>");
            for (int n = 1; n <= rep.MaxItems; n++) sb.Append($"<td>{rep.LoadsBySize[n]}</td>");
            sb.Append("</tr><tr><td>fully tuned</td>");
            for (int n = 1; n <= rep.MaxItems; n++) sb.Append($"<td>{fullyBySize[n]} ({Pct(fullyBySize[n], rep.LoadsBySize[n])})</td>");
            sb.AppendLine("</tr></table>");

            // what to tune next
            var next = new List<CargoCoverage.Contact>();
            foreach (CargoCoverage.Contact c in rep.Contacts) if (!c.Tuned) next.Add(c);
            next.Sort((x, y) => y.Loads != x.Loads ? y.Loads.CompareTo(x.Loads) : string.CompareOrdinal(x.Key, y.Key));
            sb.AppendLine("<h2>Tune next</h2>");
            if (next.Count == 0)
            {
                sb.AppendLine("<p class=\"ok\">Every contact is tuned: no load uses a fallback any more.</p>");
            }
            else
            {
                sb.AppendLine("<p class=\"dim\">The contacts still on the fallbacks, most used first. <i>Completes</i>: loads that become fully tuned when this one is tuned (it is their last untuned contact). To tune one, load the smallest load shown and use its box in the Cargo tab.</p>");
                sb.AppendLine("<table><tr><th>#</th><th>Contact</th><th>Used in</th><th>Completes</th><th>Load it with</th></tr>");
                int rank = 0;
                foreach (CargoCoverage.Contact c in next)
                {
                    rank++;
                    sb.AppendLine($"<tr><td>{rank}</td><td class=\"no\">{H(c.Title)}</td><td>{c.Loads} ({Pct(c.Loads, rep.Loads.Count)})</td><td>{c.Completes}</td><td>{H(rep.Describe(c.Smallest))}</td></tr>");
                }
                sb.AppendLine("</table>");
            }

            // what looks wrong by the measured shapes
            sb.AppendLine("<h2>Looks wrong (by the measured shapes)</h2>");
            if (CargoShapes.Count == 0)
            {
                sb.AppendLine("<p class=\"dim\">No measured shapes yet: Debug tab, Cargo tools, 'Measure the item shapes'.</p>");
            }
            else if (wrong == null || wrong.Count == 0)
            {
                sb.AppendLine("<p class=\"ok\">Nothing floats or sinks in any load.</p>");
            }
            else
            {
                sb.AppendLine($"<p class=\"dim\">{badLoads} of {rep.Loads.Count} loads have an item that <b>floats</b> (even its lowest tenth more than {CargoShapes.FloatAt * 100:0} cm above what is under it) or <b>sinks</b> (half of it more than {CargoShapes.SinkAt * 100:0} cm in). "
                    + "Calibrated on the contacts tuned by eye: none of them is flagged in its own load; a few centimetres in reads as resting, and a small bump (the log's stub) may poke into the item above. 'Seat it' in the Cargo tab rests a contact on the bulk of what is under it.</p>");
                sb.AppendLine("<table><tr><th>Contact</th><th>Looks</th><th>In loads</th><th>Worst</th><th>Worst load</th></tr>");
                foreach (Wrong w in wrong)
                {
                    sb.AppendLine($"<tr><td>{H(w.Title)} <span class=\"dim\">({(w.Tuned ? "tuned" : "fallbacks")})</span></td><td class=\"no\">{w.Kind}</td><td>{w.Loads}</td><td>{w.Worst * 100:0} cm</td><td>{H(rep.Describe(w.WorstLoad))}</td></tr>");
                }
                sb.AppendLine("</table>");
            }

            // every contact
            sb.AppendLine("<h2>Every contact</h2><p class=\"dim\"><label><input type=\"checkbox\" onchange=\"document.querySelectorAll('tr.t').forEach(r=>r.style.display=this.checked?'none':'')\"> untuned only</label>"
                + (photos ? " &nbsp; picture width <input type=\"range\" min=\"120\" max=\"600\" value=\"260\" oninput=\"setW(this.value)\">. In a contact's picture its two items are tinted: <span style=\"color:#59a6ff\">blue</span> the lower or first item, <span style=\"color:#ff9933\">orange</span> the one on top or second; the rest is untinted." : "")
                + "</p>");
            sb.AppendLine("<table><tr><th>State</th><th>Contact</th><th>Where</th><th>Used in</th><th>Smallest load</th>" + (photos ? "<th>Pictures</th>" : "") + "</tr>");
            string lastKind = null;
            foreach (CargoCoverage.Contact c in rep.Contacts)
            {
                if (c.Kind != lastKind)
                {
                    lastKind = c.Kind;
                    string head = c.Kind == "alone" ? "One item alone (the Cargo tab's 1 item view)" : c.Kind == "beside" ? "Two items in one layer" : c.Kind == "on" ? "An item (and its layer) on the layer below" : "An item lying alone on top of a pair";
                    sb.AppendLine($"<tr><th colspan=\"{(photos ? 6 : 5)}\">{head}</th></tr>");
                }
                string where = c.Kind == "beside" ? string.Join(", ", c.Arrangements).Replace("side", "across").Replace("along", "end to end")
                    : c.Kind == "on" ? (c.OnSingle && c.OnPair ? "on a single item and on a pair" : c.OnPair ? "on a pair only" : "on a single item only")
                    : c.Kind == "onpair" ? "on a pair led by " + c.Upper : "";
                sb.Append($"<tr class=\"{(c.Tuned ? "t" : "u")}\"><td class=\"{(c.Tuned ? "ok" : "no")}\">{(c.Tuned ? "tuned" : "fallbacks")}</td><td>{H(c.Title)}</td><td>{H(where)}</td><td>{c.Loads} ({Pct(c.Loads, rep.Loads.Count)})</td><td>{H(rep.Describe(c.Smallest))}</td>");
                if (photos)
                {
                    sb.Append("<td><div class=\"pics\">");
                    if (c.Kind == "alone")
                    {
                        foreach (KeyValuePair<int, Shot> kv in aloneShots)
                            if (rep.Loads[kv.Key].Classes.Length == 1 && rep.Classes[rep.Loads[kv.Key].Classes[0]] == c.Lower) sb.Append(Img(kv.Value.File, c.Title));
                    }
                    else if (picsByKey.TryGetValue(c.Key, out List<Shot> ps))
                    {
                        foreach (Shot s in ps)
                        {
                            string cap = c.Title + " in " + rep.Describe(s.Load.Classes) + (s.Role == "onpair-context" ? " (resting on a pair)" : "");
                            sb.Append($"<div class=\"fig\">{Img(s.File, cap)}<div class=\"cap\">{H(rep.Describe(s.Load.Classes))}{(s.Role == "onpair-context" ? " · on a pair" : "")}</div></div>");
                        }
                    }
                    sb.Append("</div></td>");
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");

            // covering set
            sb.AppendLine($"<h2>The fewest loads showing every contact ({rep.Cover.Count})</h2><p class=\"dim\">Load these one after another (or look at their pictures) and you have seen every contact of every load the cart can carry. "
                + (photos ? "The pictures are in the Cargo tab's colours: blue the first item of a layer, orange the second or one alone on top, lighter higher up." : "Run 'Photograph the coverage set' for the pictures.") + "</p>");
            sb.AppendLine("<div class=\"pics\">");
            foreach (int i in rep.Cover)
            {
                CargoCoverage.Load l = rep.Loads[i];
                var chips = new StringBuilder();
                foreach (string k in l.Keys) chips.Append(Chip(rep, k));
                string img = photos && coverShots.TryGetValue(i, out Shot s) ? Img(s.File, rep.Describe(l.Classes)) : "";
                sb.AppendLine($"<div class=\"fig\">{img}<div class=\"cap\"><b>{H(rep.Describe(l.Classes))}</b> · {l.Layers} layer(s), {l.Untuned} untuned<br>{chips}</div></div>");
            }
            sb.AppendLine("</div>");

            // dead tunings
            sb.AppendLine("<h2>Tunings no load uses</h2>");
            if (rep.Dead.Count == 0)
            {
                sb.AppendLine("<p class=\"ok\">None: every tuning in the layout file is used by some load.</p>");
            }
            else
            {
                sb.AppendLine("<p class=\"dim\">Values in cargo-layout.cfg that no load of up to " + rep.MaxItems + " items reaches (harmless, but nothing you change there shows). They may come back into use if the fallbacks change which pairs lie side by side.</p><ul>");
                foreach (string d in rep.Dead) sb.AppendLine("<li>" + H(d) + "</li>");
                sb.AppendLine("</ul>");
            }
            if (!string.IsNullOrEmpty(problems))
            {
                sb.AppendLine("<h2>Notes from the run</h2><pre>" + H(problems) + "</pre>");
            }
            sb.AppendLine("</body></html>");
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteAllHtml(string path, CargoCoverage.Report rep, List<Shot> shots, string problems)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<!doctype html><html><head><meta charset=\"utf-8\"><title>Every cargo load</title>{PageStyle}</head><body>{PageScript}");
            sb.AppendLine($"<h1>Every cargo load</h1><p class=\"dim\">{DateTime.Now:yyyy-MM-dd HH:mm}. {shots.Count} loads of 1 to {rep.MaxItems} items, in the Cargo tab's colours (blue the first item of a layer, orange the second or one alone on top, lighter higher up). Under each: its contacts, green tuned, red still on the fallbacks. {SettingsLine(rep)}</p>");
            sb.AppendLine("<details class=\"legend\"><summary>The size classes and their items</summary>" + ClassLegend(rep) + "</details>");
            sb.Append("<div id=\"bar\"><span>Items:</span>");
            for (int n = 1; n <= rep.MaxItems; n++) sb.Append($"<label><input type=\"checkbox\" class=\"sz\" value=\"{n}\" checked onchange=\"f()\">{n}</label>");
            sb.Append("<select id=\"cl\" onchange=\"f()\"><option value=\"\">any class</option>");
            foreach (string c in rep.Classes) sb.Append($"<option value=\"|{H(c)}|\">contains {H(rep.Label(c))}</option>");
            sb.Append("</select><select id=\"ct\" onchange=\"f()\"><option value=\"\">any contact</option>");
            foreach (CargoCoverage.Contact c in rep.Contacts) sb.Append($"<option value=\"|{H(c.Key)}|\">{(c.Tuned ? "" : "untuned: ")}{H(c.Title)}</option>");
            sb.Append("</select><label><input type=\"checkbox\" id=\"un\" onchange=\"f()\"> with an untuned contact only</label>");
            sb.Append("<span>columns <input type=\"range\" id=\"cols\" min=\"3\" max=\"16\" value=\"8\" oninput=\"fit()\"> <span id=\"colsN\">8</span></span><span id=\"cnt\"></span></div>");
            // the pictures fill the window: their width follows the window width and the columns slider, on resize too
            sb.AppendLine("<script>function fit(){var n=+document.getElementById('cols').value;document.getElementById('colsN').textContent=n;var w=Math.floor((document.documentElement.clientWidth-40-n*8)/n);document.documentElement.style.setProperty('--w',Math.max(90,w)+'px');}window.addEventListener('resize',fit);fit();function f(){var sz=[...document.querySelectorAll('.sz')].filter(x=>x.checked).map(x=>x.value);var cl=document.getElementById('cl').value,ct=document.getElementById('ct').value,un=document.getElementById('un').checked,n=0;"
                + "document.querySelectorAll('.fig').forEach(e=>{var ok=sz.includes(e.dataset.n)&&(!cl||e.dataset.c.includes(cl))&&(!ct||e.dataset.k.includes(ct))&&(!un||e.dataset.u!=='0');e.style.display=ok?'':'none';if(ok)n++;});document.getElementById('cnt').textContent=n+' shown';}</script>");
            sb.AppendLine("<div class=\"pics\">");
            foreach (Shot s in shots)
            {
                CargoCoverage.Load l = s.Load;
                var cls = new StringBuilder("|");
                foreach (int c in l.Classes) cls.Append(rep.Classes[c]).Append('|');
                var keys = new StringBuilder("|");
                var chips = new StringBuilder();
                foreach (string k in l.Keys)
                {
                    keys.Append(k).Append('|');
                    chips.Append(Chip(rep, k));
                }
                string name = rep.Describe(l.Classes);
                sb.AppendLine($"<div class=\"fig\" data-n=\"{l.Classes.Length}\" data-c=\"{H(cls.ToString())}\" data-k=\"{H(keys.ToString())}\" data-u=\"{l.Untuned}\">{Img(s.File, name)}<div class=\"cap\"><b>{H(name)}</b><br>{chips}</div></div>");
            }
            sb.AppendLine("</div><script>f();</script>");
            if (!string.IsNullOrEmpty(problems))
            {
                sb.AppendLine("<h2>Notes from the run</h2><pre>" + H(problems) + "</pre>");
            }
            sb.AppendLine("</body></html>");
            File.WriteAllText(path, sb.ToString());
        }
    }
}
