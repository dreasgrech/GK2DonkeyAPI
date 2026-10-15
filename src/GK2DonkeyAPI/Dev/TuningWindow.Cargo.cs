using System;
using System.Collections.Generic;
using UnityEngine;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // The Cargo tab: the cargo's places as a workflow (docs/cargo-layout-layers.md). Every knob of the layout file is
    // here, every rule of the packer and every cargo behaviour switch. Three parts: the cart (the floor, set once), this
    // load (one of three views picked by how many items are on the cart: one, two, three or more) and folded options. A contact between two items (lying beside each
    // other, or one on the other) is one tuning wherever it occurs, in this load and in every other.
    internal static partial class TuningWindow
    {
        private static bool cargoRulesOpen, cargoBehaviourOpen, cargoHelpOpen;

        // The tab's tooltips (hover a label or a button). Short; the long form is in the folded help and the docs.
        private const string TipHeightOn = "How high this item (and everything above it) sits on the one below. Applies wherever these two meet this way.";
        private const string TipAcross = "Toward (-) or away from (+) the camera.";
        private const string TipAlong = "Toward the donkey (+) or the back of the cart (-).";
        private const string TipHeightAlone = "Height above the cart floor when this is the only item aboard.";
        private const string TipHeightBed = "Height above the cart floor, in the bed layer.";
        private const string TipHeightRel = "Height relative to the layer's first item. The layer itself rests by the box above.";
        private const string TipHeightLayerFirst = "The first item sits where its layer sits: set the layer's height in the 'x on y' box above.";
        private const string TipHeightTwo = "Height above the cart floor. For the item on top this is how the two stack.";
        private const string TipOnPair = "This item starts over the middle of the two below. This is an extra move for it, only when it lies alone on top of a pair. Nothing under it moves.";
        private const string TipLooksRight = "Freeze this contact exactly as it lies now. It counts as tuned and no fallback can move it again.";
        private const string TipSeat = "Rest this item on what is really under it (its measured shape, 3 cm sunk in). Written as a tuning; one Undo step.";
        private const string TipReset = "Forget this contact's tuning; it follows the fallbacks again.";
        private const string TipAlone = "Show just these two on the cart, as a two-item cart, to tune them without the rest. The whole load stays aboard.";
        private const string TipAcceptLoad = "Freeze every untuned contact in this load exactly as it lies. You stay on this load.";
        private const string TipAcceptNext = "Freeze every untuned contact here, then put the next contact's load on the cart (a test load): ready ones first, up the ladder.";
        private const string TipSkip = "Put the next untuned contact's load on the cart without accepting anything here. Skipped contacts go to the back of the queue.";
        private const string TipLoad = "Put this contact's smallest load on the cart as a test load (real items, made for you). Replaces the last test load; refused while the cart holds other cargo.";
        private const string TipCoverLoad = "The fewest loads that together show every contact: load them one by one and you have seen everything.";
        private const string TipTakeOff = "Remove the test items this tool put on the cart (and their test zombies). Items you set down stay yours.";
        private const string TipUndo = "Undo the last layout edit (a drag, a nudge run, Looks right, Seat it, Reset).";
        private const string TipRedo = "Redo the edit you just undid.";
        private const string TipCompare = "Show the layout as it was when this session began. Editing is locked and nothing is saved until you come back.";
        private const string TipTint = "Colour the items on the cart like their names here: blue the first item of a layer, orange the second or one alone on top, lighter higher up.";
        private const string TipShow = "Hide part of the load to see the rest. View only: hidden items still count in the layout and come back when the tab closes.";
        private const string TipFloor = "Where the bottom of the load rests on the cart. Set this once; it moves everything, tuned or not.";
        private const string TipArrangement = "How a two-item cart lays these two out. The lit button again starts this contact over; the others switch and keep both tunings.";
        private const string TipOverhang = "How far a pair may stick out past the bed before the packer stacks them instead (pairs set to 'let the rules pick').";
        private const string TipGap = "Space between two untuned items lying beside each other.";
        private const string TipNestle = "How far an untuned upper layer sinks into the one below.";
        private const string TipLayerShift = "Each untuned upper layer moved forward or back by this much (+ back, - toward the donkey).";
        private const string TipSeatOnShapes = "Upper layers rest on the items' measured shapes (a layer on a round log sits on the trunk). Off: on their boxes.";

        // What the tab is drawn from, frozen while any panel control is held (GUIUtility.hotControl, with a button down). IMGUI knows a
        // control by its order in the pass, so the boxes above a slider must not come and go while it is dragged, or
        // the drag lands on another slider (2026-10-10, video 18-19-36). Dragging the overhang or the gap re-arranges
        // the load (more or fewer contact boxes above those sliders), and a pack can change the load in between; the
        // structure follows on release. The values being dragged come from the slider itself, so they stay live.
        private static List<Cargo.PlacedItem> cargoView = new List<Cargo.PlacedItem>();
        private static bool cargoViewOnCart, cargoViewIsolating;
        private static int cargoViewIsolatedFrom;
        private static string cargoViewPairMode = "";

        // The whole layout's state over every load of up to six items (CargoCoverage via CargoSheet.AnalyseNow), redone
        // when the layout or the fallbacks change, never while a control is held (a drag changes the layout every frame),
        // and only in a Layout event (both passes of an event draw the same lines).
        private static CargoCoverage.Report coverage;
        private static string coverageSig;

        private static void UpdateCoverage()
        {
            bool held = GUIUtility.hotControl != 0 && (BepInEx.UnityInput.Current.GetMouseButton(0) || BepInEx.UnityInput.Current.GetMouseButton(1));
            if (held || Event.current.type != EventType.Layout)
            {
                return;
            }
            string sig;
            try
            {
                sig = CargoSheet.CurrentSignature();
            }
            catch (Exception e)
            {
                sig = "error " + e.Message;
            }
            if (sig == coverageSig)
            {
                return;
            }
            // Wait until the edits settle (half a second without a change): every numpad press or slider release
            // would run the 1,715 packs (about 60 ms) otherwise.
            if (sig != pendingSig)
            {
                pendingSig = sig;
                pendingSince = Time.unscaledTime;
                if (coverage != null) return;
            }
            else if (coverage != null && Time.unscaledTime - pendingSince < 0.5f)
            {
                return;
            }
            coverageSig = sig; // set first: a failure is logged once per change, not every frame
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                coverage = CargoSheet.AnalyseNow(CargoSheet.CoverageMaxItems);
                if (coverageLogged++ < 3)
                {
                    Plugin.Log.LogInfo($"[coverage] {coverage.Tuned} of {coverage.Total} contacts tuned over {coverage.Loads.Count} loads (analysed in {watch.ElapsedMilliseconds} ms).");
                }
            }
            catch (Exception e)
            {
                coverage = null;
                Plugin.Log.LogWarning("[coverage] analysing the layout failed: " + e);
            }
        }

        private static int coverageLogged;
        private static string pendingSig;
        private static float pendingSince;

        // Per analysis: the queue and the fully tuned count (the panel draws them every pass).
        private static CargoCoverage.Report cachedFor;
        private static int cachedSkips = -1, cachedFully;
        private static List<CargoCoverage.Contact> cachedQueue;

        private static List<CargoCoverage.Contact> CachedQueue(out int fully)
        {
            if (!ReferenceEquals(cachedFor, coverage) || cachedSkips != skippedKeys.Count)
            {
                cachedFor = coverage;
                cachedSkips = skippedKeys.Count;
                cachedQueue = Queue(coverage);
                cachedFully = 0;
                foreach (CargoCoverage.Load l in coverage.Loads) if (l.Untuned == 0) cachedFully++;
            }
            fully = cachedFully;
            return cachedQueue;
        }

        // " · used in N loads" for a contact key (CargoCoverage's keys), when the analysis has it.
        private static string UsedIn(string key)
        {
            if (coverage == null || !coverage.ByKey.TryGetValue(key, out CargoCoverage.Contact c))
            {
                return "";
            }
            return $"   <color=#9a9a9a>· in {c.Loads} of {coverage.Loads.Count} loads</color>";
        }

        private static string KeyOf(string relation, string id, string partnerId) => CargoCoverage.KeyOf(relation, id, partnerId, CargoSheet.ClassOf);

        private static void DrawCoverageLine()
        {
            if (coverage == null)
            {
                return;
            }
            List<CargoCoverage.Contact> next = CachedQueue(out int fully);
            string done = coverage.Tuned == coverage.Total ? "<color=#8ee68e>" : "<color=#ffd257>";
            GUILayout.Label($"Whole layout: {done}{coverage.Tuned} of {coverage.Total} contacts tuned</color>, {fully} of {coverage.Loads.Count} loads fully tuned.", label);

            // Read before any button below: a click changes the cart in this pass, and the rows after it must stay the
            // ones the layout pass drew.
            int testOn = TestLoads.OnCart();
            GUILayout.BeginVertical(GUI.skin.box);
            if (next.Count > 0)
            {
                GUILayout.Label(Tip("Tune next  <color=#9a9a9a>(ready first, up the ladder: alone, beside, on, alone on a pair)</color>", "The contacts still on the fallbacks: first those whose load has nothing else untuned (ready), then up the ladder, then the most used. Load puts the contact's load on the cart: the smallest where it is the only untuned contact, else the smallest showing it."), label);
                for (int i = 0; i < Mathf.Min(TuneNextRows, next.Count); i++)
                {
                    CargoCoverage.Contact c = next[i];
                    GUILayout.BeginHorizontal();
                    if (TipButton("Load", TipLoad, 60f))
                    {
                        TestLoads.Put(LoadIds(c.LoadToTune), c.Title);
                    }
                    GUILayout.Label($"<color=#ff8c69>{c.Title}</color>   <color=#9a9a9a>in {c.Loads} loads, completes {c.Completes} · {coverage.Describe(c.LoadToTune)}{(c.Ready ? "" : " · after others")}{(skippedKeys.Contains(c.Key) ? " · skipped" : "")}</color>", label);
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("<color=#8ee68e>Every contact is tuned.</color>", label);
            }
            // Walk the covering loads: together they show every contact of every load.
            if (coverage.Cover.Count > 0)
            {
                coverStep = Mathf.Clamp(coverStep, 0, coverage.Cover.Count - 1);
                CargoCoverage.Load cl = coverage.Loads[coverage.Cover[coverStep]];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", Wd(28f))) coverStep = (coverStep + coverage.Cover.Count - 1) % coverage.Cover.Count;
                if (GUILayout.Button(">", Wd(28f))) coverStep = (coverStep + 1) % coverage.Cover.Count;
                if (TipButton("Load", TipCoverLoad, 60f))
                {
                    TestLoads.Put(LoadIds(cl.Classes), $"covering load {coverStep + 1} of {coverage.Cover.Count}");
                }
                GUILayout.Label(Tip($"Every contact in {coverage.Cover.Count} loads: {coverStep + 1}. {coverage.Describe(cl.Classes)}   <color=#9a9a9a>{cl.Untuned} untuned</color>", TipCoverLoad), label);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            bool was = GUI.enabled;
            GUI.enabled = was && testOn > 0;
            if (GUILayout.Button(Tip(testOn > 0 ? $"Take the test load off ({testOn})" : "Take the test load off", TipTakeOff), Wd(200f)))
            {
                TestLoads.TakeOff();
            }
            GUI.enabled = was;
            GUILayout.Label(TestLoads.Status, small);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // The Debug tab's load builder: how many of each size class are on the cart, changed live. The counts mirror the
        // ridden cart (whatever put the items there); a - or + press puts the new load on at once (TestLoads.Put, which is
        // refused while the cart holds cargo that is not a test load, so nothing of the player's is ever replaced).
        private static void DrawTestLoadBuilder()
        {
            Header("Test loads on the cart (live)");
            List<string> classes = CargoSheet.ClassReps();
            // the counts as the cart stands at the start of the pass; a click below changes the cart, not these
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            int total = 0;
            foreach (Cargo.PlacedItem p in Cargo.Placed)
            {
                string c = CargoSheet.ClassOf(p.Id);
                counts[c] = (counts.TryGetValue(c, out int k) ? k : 0) + 1;
                total++;
            }
            bool riding = Mount.IsMounted && Mount.CurrentStyle == MountStyle.Cart && Cargo.Attached;
            int testOn = TestLoads.OnCart();
            string change = null; // the class to add (+) or remove (-), applied after the rows are drawn
            int delta = 0;
            foreach (string c in classes)
            {
                int k = counts.TryGetValue(c, out int got) ? got : 0;
                GUILayout.BeginHorizontal();
                GUILayout.Label(Tip(CargoSheet.ClassLabel(c), "How many of this size class are on the cart now. - and + change the cart at once (a test load; refused while other cargo is aboard)."), label, Wd(140f));
                bool was = GUI.enabled;
                GUI.enabled = was && riding && k > 0;
                if (GUILayout.Button("-", Wd(28f))) { change = c; delta = -1; }
                GUI.enabled = was;
                GUILayout.Label(k.ToString(), label, Wd(24f));
                GUI.enabled = was && riding && total < CargoSheet.CoverageMaxItems;
                if (GUILayout.Button("+", Wd(28f))) { change = c; delta = 1; }
                GUI.enabled = was;
                GUILayout.Label($"<color=#9a9a9a>{c}</color>", small);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            bool en = GUI.enabled;
            GUI.enabled = en && testOn > 0;
            if (GUILayout.Button(Tip("Take the test load off", TipTakeOff), Wd(170f)))
            {
                TestLoads.TakeOff();
            }
            GUI.enabled = en;
            GUILayout.Label(riding ? TestLoads.Status : "ride in Cart style to use this", small);
            GUILayout.EndHorizontal();
            Help("Real items put straight onto the ridden cart (a corpse with its organs, a zombie made the way the game makes one): no searching the world. Each press re-makes the test load with the new count; the cart must hold nothing else. Items you set down stay yours. The Cargo tab's Tune next list has a Load button per contact.");
            if (change != null)
            {
                var ids = new List<string>();
                foreach (string c in classes)
                {
                    int k = (counts.TryGetValue(c, out int got) ? got : 0) + (c == change ? delta : 0);
                    for (int j = 0; j < k; j++) ids.Add(c);
                }
                if (ids.Count == 0) TestLoads.TakeOff(); else TestLoads.Put(ids, "the load builder");
            }
        }

        // ---------------------------------------------------------------- Looks right (docs/cargo-tuning-tools.md)
        // Each freezes a contact exactly where it lies now: the same freeze a first slider move makes, without the move.
        // True when something was frozen.

        private static bool LooksRightButton(bool active) => TipButton("Looks right", TipLooksRight, 100f, active);

        private static bool AcceptAlone(Cargo.PlacedItem p)
        {
            CargoLayout.ItemEntry e = CargoLayout.EditItem(p.Id);
            if (e.HasAlone) return false;
            e.HasAlone = true;
            e.Alone = e.Base; // where an item without its own alone place lies
            Plugin.Log.LogInfo($"Cargo layout: {p.Id} alone accepted as it lies.");
            return true;
        }

        private static bool AcceptPair2(Cargo.PlacedItem p0, Cargo.PlacedItem p1, bool isolating, string lastPairMode)
        {
            CargoLayout.PairEntry pair = CargoLayout.EditPair(p0.Id, p1.Id);
            string shown = string.IsNullOrEmpty(lastPairMode) ? pair.Mode : lastPairMode;
            if (pair.TunedFor(shown)) return false;
            bool firstIsA = CargoLayout.FirstIsA(pair, p0.Id, p1.Id);
            if (!isolating) pair.Mode = shown; // a two-item cart keeps showing what was accepted
            pair.SetFor(shown, firstIsA ? p0.Pos : p1.Pos, firstIsA ? p1.Pos : p0.Pos);
            Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B} accepted as it lies ({shown}).");
            return true;
        }

        private static bool AcceptOn(Cargo.PlacedItem pl, Cargo.PlacedItem q, List<Cargo.PlacedItem> view = null, int index = -1)
        {
            CargoLayout.PairEntry pair = CargoLayout.EditPair(q.Id, pl.Id);
            if (pair.TopTuned) return false;
            CargoLayout.ItemEntry eq = CargoLayout.ForItem(q.Id);
            CargoPacker.FreezeOnTop(pair, CargoLayout.FirstIsA(pair, q.Id, pl.Id), eq != null ? eq.Base : Vector3.zero, pl.Rel);
            if (view != null) FreezeLayerOf(view, index);
            Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B} on top accepted as it lies.");
            return true;
        }

        // The layer of the item at index (its first item) keeps its arrangement when its on-top relation is tuned
        // (CargoPacker.FreezeLayerArrangement).
        private static void FreezeLayerOf(List<Cargo.PlacedItem> view, int index)
        {
            if (index < 0 || index >= view.Count) return;
            CargoPacker.FreezeLayerArrangement(LiveResult(view), index, CoverageLayoutLookup, CargoLayout.EditPair);
        }

        private static readonly CargoPacker.Lookup CoverageLayoutLookup = new CargoPacker.Lookup
        {
            ForItem = CargoLayout.ForItem, ForPair = CargoLayout.ForPair, FirstIsA = CargoLayout.FirstIsA, SameClass = CargoLayout.SameClass, BoxOf = CargoLayout.BoxOf,
        };

        private static bool AcceptBeside(Cargo.PlacedItem pl, Cargo.PlacedItem q)
        {
            CargoLayout.PairEntry pair = CargoLayout.EditPair(q.Id, pl.Id);
            if (pair.BesideTuned) return false;
            CargoPacker.FreezeBeside(pair, CargoLayout.FirstIsA(pair, q.Id, pl.Id), q.BesideStored, pl.BesideStored, pl.Relation);
            Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B} beside accepted as it lies ({pl.Relation}).");
            return true;
        }

        private static bool AcceptOnPair(Cargo.PlacedItem it)
        {
            CargoLayout.ItemEntry e = CargoLayout.EditItem(it.Id);
            if (e.OnPairTuned) return false;
            e.HasOnPair = true; // its offset stays as it is (zero)
            Plugin.Log.LogInfo($"Cargo layout: {it.Id} alone on a pair accepted as it lies.");
            return true;
        }

        private static bool IsLoneOnPair(List<Cargo.PlacedItem> placed, Cargo.PlacedItem it)
        {
            if (it.Relation != "on") return false;
            int inLayer = 0, inBelow = 0;
            foreach (Cargo.PlacedItem o in placed)
            {
                if (o.Layer == it.Layer) inLayer++;
                if (o.Layer == it.Layer - 1) inBelow++;
            }
            return inLayer == 1 && inBelow >= 2;
        }

        // Every untuned contact of the load on the cart, accepted as it lies (one freeze per contact; the freezes move
        // nothing, so they can all use the same pack). The count frozen.
        private static int AcceptAll(List<Cargo.PlacedItem> view, bool isolating, string lastPairMode)
        {
            int n = view.Count, done = 0;
            if (n == 1)
            {
                return AcceptAlone(LiveOf(view, 0)) ? 1 : 0;
            }
            if (n == 2)
            {
                return AcceptPair2(LiveOf(view, 0), LiveOf(view, 1), isolating, lastPairMode) ? 1 : 0;
            }
            for (int k = 0; k < n; k++)
            {
                Cargo.PlacedItem it = LiveOf(view, k);
                if (it.Relation == null || it.Relation == "bed" || it.Partner < 0 || it.Partner >= n) continue;
                Cargo.PlacedItem q = LiveOf(view, it.Partner);
                if (it.Relation == "on" ? AcceptOn(it, q, view, k) : AcceptBeside(it, q)) done++;
                if (IsLoneOnPair(view, it) && AcceptOnPair(it)) done++;
            }
            return done;
        }

        // How many contacts of the load on the cart are still on the fallbacks (for the button's label).
        private static int UntunedInLoad(List<Cargo.PlacedItem> view, bool isolating)
        {
            int n = view.Count;
            if (n == 1)
            {
                CargoLayout.ItemEntry e = CargoLayout.ForItem(view[0].Id);
                return e != null && e.HasAlone ? 0 : 1;
            }
            if (n == 2)
            {
                CargoLayout.PairEntry pair = CargoLayout.ForPair(view[0].Id, view[1].Id);
                string shown = string.IsNullOrEmpty(cargoViewPairMode) ? (pair != null ? pair.Mode : "auto") : cargoViewPairMode;
                return pair != null && pair.TunedFor(shown) ? 0 : 1;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int open = 0;
            foreach (Cargo.PlacedItem it in view)
            {
                if (it.Relation == null || it.Relation == "bed" || it.Partner < 0 || it.Partner >= n) continue;
                string key = KeyOf(it.Relation, it.Id, view[it.Partner].Id);
                if (seen.Add(key) && !it.Tuned) open++;
                if (IsLoneOnPair(view, it) && seen.Add("onpair:" + CargoSheet.ClassOf(it.Id)))
                {
                    CargoLayout.ItemEntry e = CargoLayout.ForItem(it.Id);
                    if (e == null || !e.OnPairTuned) open++;
                }
            }
            return open;
        }

        // The next untuned contact's load onto the cart (a test load; Queue, LoadToTune). afterAccept: the layout just changed,
        // so the analysis is redone first.
        // Contacts skipped this session (Skip to next): they go to the back of the queue, or skipping would load the same
        // load again (it holds the next untuned contact, which is why it was loaded). When only skipped ones are
        // left, the queue goes round to them.
        private static readonly HashSet<string> skippedKeys = new HashSet<string>(StringComparer.Ordinal);

        // The untuned contacts in the order they are offered: not skipped first; then the READY ones (a load exists where
        // it is the only untuned contact, so it is tuned on its own, on top of tuned ones); then up the ladder (alone,
        // beside, on, alone on a pair: effects run upward only, CargoCoverage.KindRank); then the most used; then by key.
        // The Tune next list shows it and the next / skip buttons load its first one, so the two always agree (they did
        // not on a tie: slab beside slab and corpse on zombie both in 330 loads, 2026-10-12). Most-used-first alone
        // offered 'log on corpse' (corpse + log + log) before 'corpse beside log' was tuned (2026-10-12).
        private static List<CargoCoverage.Contact> Queue(CargoCoverage.Report rep)
        {
            var list = new List<CargoCoverage.Contact>();
            foreach (CargoCoverage.Contact c in rep.Contacts) if (!c.Tuned) list.Add(c);
            list.Sort((x, y) =>
            {
                bool sx = skippedKeys.Contains(x.Key), sy = skippedKeys.Contains(y.Key);
                if (sx != sy) return sx ? 1 : -1;
                if (x.Ready != y.Ready) return x.Ready ? -1 : 1;
                int kx = CargoCoverage.KindRank(x.Kind), ky = CargoCoverage.KindRank(y.Kind);
                if (kx != ky) return kx.CompareTo(ky);
                return y.Loads != x.Loads ? y.Loads.CompareTo(x.Loads) : string.CompareOrdinal(x.Key, y.Key);
            });
            return list;
        }

        // Under a box's header: the untuned contacts of a lower kind in this load. Effects run upward only (a beside
        // relation moves the layers above it, an on relation its layer and above), so those are tuned first; this one
        // then keeps its relation but is carried along. Drawn only from state that cannot change during a drag.
        private static void TuneFirstNote(List<Cargo.PlacedItem> view, string kind)
        {
            if (coverage == null) return;
            int rank = CargoCoverage.KindRank(kind);
            List<string> names = null;
            foreach (string key in UntunedKeysHere(view))
            {
                if (coverage.ByKey.TryGetValue(key, out CargoCoverage.Contact c) && CargoCoverage.KindRank(c.Kind) < rank)
                {
                    (names ?? (names = new List<string>())).Add(c.Title);
                }
            }
            if (names != null)
            {
                GUILayout.Label(Tip($"  <color=#ffd257>tune first: {string.Join(", ", names)}</color>", "Lower on the ladder and still on the fallbacks in this load. Tuning it later would carry this contact along, so settle it first."), small);
            }
        }

        // The untuned contacts of the load on the cart (coverage keys).
        private static List<string> UntunedKeysHere(List<Cargo.PlacedItem> view)
        {
            var keys = new List<string>();
            if (coverage == null) return keys;
            int n = view.Count;
            for (int k = 0; k < n; k++)
            {
                Cargo.PlacedItem it = view[k];
                var here = new List<string>();
                if (n == 1) here.Add("alone:" + CargoSheet.ClassOf(it.Id));
                else if (it.Relation != null && it.Relation != "bed" && it.Partner >= 0 && it.Partner < n)
                {
                    here.Add(KeyOf(it.Relation, it.Id, view[it.Partner].Id));
                    if (IsLoneOnPair(view, it)) here.Add("onpair:" + CargoSheet.ClassOf(it.Id));
                }
                foreach (string key in here)
                {
                    if (coverage.ByKey.TryGetValue(key, out CargoCoverage.Contact c) && !c.Tuned && !keys.Contains(key)) keys.Add(key);
                }
            }
            return keys;
        }

        private static void LoadNextUntuned(bool afterAccept, List<Cargo.PlacedItem> skipping = null)
        {
            try
            {
                if (afterAccept || coverage == null)
                {
                    // the accepted entries are in memory already; the tab marks the change (and saves) at the end of the pass
                    coverage = CargoSheet.AnalyseNow(CargoSheet.CoverageMaxItems);
                }
                if (skipping != null)
                {
                    skippedKeys.UnionWith(UntunedKeysHere(skipping));
                }
                List<CargoCoverage.Contact> queue = Queue(coverage);
                if (queue.Count == 0)
                {
                    TestLoads.Note("every contact is tuned: nothing left to load");
                    return;
                }
                if (skippedKeys.Contains(queue[0].Key))
                {
                    // only skipped ones are left: round again
                    skippedKeys.Clear();
                    queue = Queue(coverage);
                }
                TestLoads.Put(LoadIds(queue[0].LoadToTune), queue[0].Title);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[testload] loading the next untuned contact failed: " + e);
            }
        }

        // ---------------------------------------------------------------- the measured shapes: seats and verdicts
        // (CargoShapes; docs/cargo-tuning-tools.md). Seat it rests an item on the bulk of what is under it (its lowest
        // quarter at the usual 3 cm sink, a small bump ignored), through the same write a slider move makes, so it is
        // one undo step and freezes an untuned contact first. A starting point for the eye, not a verdict.

        private static bool SeatButton(bool active) => TipButton("Seat it", TipSeat, 70f, active);

        // The load on the cart as a packer result (the places relative to the floor; only differences are used).
        private static CargoPacker.Result LiveResult(List<Cargo.PlacedItem> view)
        {
            int n = view.Count;
            var r = new CargoPacker.Result
            {
                Order = new int[n], Id = new string[n], Box = new Vector3[n], Target = new Vector3[n], Origin = new Vector3[n], OnTop = new bool[n],
                Tuned = new bool[n], Layer = new int[n], Partner = new int[n], Relation = new string[n], Lift = new float[n], LayerBase = new float[n], Note = "", PairMode = "",
            };
            for (int k = 0; k < n; k++)
            {
                Cargo.PlacedItem p = LiveOf(view, k);
                r.Order[k] = k;
                r.Id[k] = p.Id;
                r.Box[k] = p.Box;
                r.Target[k] = p.Pos;
                r.Layer[k] = p.Layer;
                r.Partner[k] = p.Partner;
                r.Relation[k] = p.Relation ?? "bed";
                r.Tuned[k] = p.Tuned;
                r.OnTop[k] = p.OnTop;
                r.LayerBase[k] = p.LayerBase;
            }
            return r;
        }

        private static bool SeatOn(List<Cargo.PlacedItem> view, int index, Cargo.PlacedItem pl, Cargo.PlacedItem q)
        {
            float d = CargoShapes.SeatDelta(LiveResult(view), index, CargoSheet.ShapeOf);
            if (float.IsNaN(d)) return false;
            CargoLayout.PairEntry pair = CargoLayout.EditPair(q.Id, pl.Id);
            bool qIsA = CargoLayout.FirstIsA(pair, q.Id, pl.Id);
            if (!pair.TopTuned)
            {
                CargoLayout.ItemEntry eq = CargoLayout.ForItem(q.Id);
                CargoPacker.FreezeOnTop(pair, qIsA, eq != null ? eq.Base : Vector3.zero, pl.Rel);
                FreezeLayerOf(view, index);
            }
            CargoPacker.WriteOnTop(pair, qIsA, pl.Rel + new Vector3(0f, d, 0f));
            Plugin.Log.LogInfo($"Cargo layout: {pl.Id} on {q.Id} seated ({d:+0.000;-0.000}).");
            return true;
        }

        // What looks wrong in the load on the cart (CargoShapes.Judge), worked out again only when the load moved.
        private static string verdictKey;
        private static string verdictText = "";

        private static void DrawVerdicts(List<Cargo.PlacedItem> view)
        {
            if (CargoShapes.Count == 0 || view.Count < 2)
            {
                return;
            }
            if (Event.current.type == EventType.Layout)
            {
                var sbk = new System.Text.StringBuilder();
                for (int k = 0; k < view.Count; k++)
                {
                    Cargo.PlacedItem p = LiveOf(view, k);
                    sbk.Append(p.Id).Append(p.Pos.x.ToString("0.000")).Append(p.Pos.y.ToString("0.000")).Append(p.Pos.z.ToString("0.000")).Append('|');
                }
                string key = sbk.ToString();
                if (key != verdictKey)
                {
                    verdictKey = key;
                    CargoPacker.Result r = LiveResult(view);
                    var parts = new List<string>();
                    for (int i = 0; i < r.Count; i++)
                    {
                        CargoShapes.Verdict v = CargoShapes.Judge(r, i, CargoSheet.ShapeOf);
                        if (v == null) continue;
                        string what = Tinted(LiveOf(view, i), view);
                        string under = v.Under >= 0 && v.Under < r.Count ? Tinted(LiveOf(view, v.Under), view) : "";
                        parts.Add(v.Kind == "floats" ? $"{what} floats {v.Amount * 100:0} cm" : v.Kind == "sinks" ? $"{what} sinks {v.Amount * 100:0} cm into {under}" : $"{what} has nothing under it");
                    }
                    verdictText = parts.Count == 0
                        ? "<color=#8ee68e>Nothing floats or sinks here</color> <color=#9a9a9a>(by the measured shapes)</color>"
                        : "<color=#ff8c69>Looks wrong:</color> " + string.Join(";  ", parts) + "  <color=#9a9a9a>(Seat it rests an item on what is under it)</color>";
                }
            }
            GUILayout.Label(verdictText, label);
        }

        // ---------------------------------------------------------------- undo, redo, compare
        private static int checkpointVersion = -1;

        private static void DrawUndoRow()
        {
            GUILayout.BeginHorizontal();
            bool was = GUI.enabled;
            GUI.enabled = was && CargoLayout.CanUndo;
            if (GUILayout.Button(Tip(CargoLayout.UndoCount > 0 ? $"Undo ({CargoLayout.UndoCount})" : "Undo", TipUndo), Wd(90f)))
            {
                CargoLayout.Undo();
                Cargo.RefreshViews();
            }
            GUI.enabled = was && CargoLayout.CanRedo;
            if (GUILayout.Button(Tip("Redo", TipRedo), Wd(70f)))
            {
                CargoLayout.Redo();
                Cargo.RefreshViews();
            }
            GUI.enabled = was && (CargoLayout.Comparing || CargoLayout.DiffersFromSessionStart);
            if (GUILayout.Button(Tip(CargoLayout.Comparing ? "Back to now" : "Compare with the session start", TipCompare), Wd(230f)))
            {
                CargoLayout.Compare(!CargoLayout.Comparing);
                Cargo.RefreshViews();
            }
            GUI.enabled = was;
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- numpad nudges
        // Numpad + / - move the last slider moved in this tab by 0.01 (Shift: 0.05). RawSlider counts its sliders per
        // pass (rawSeq), remembers the one dragged (nudgeId) and applies a pending nudge to it in the next repaint; the
        // keys are read once per frame. The slider the keys move is marked in gold.
        private static string nudgeShape = "";
        private static int nudgeFrame = -1;
        private static bool nudgedSinceCheckpoint;

        private static void PollNudgeKeys()
        {
            if (Time.frameCount == nudgeFrame) return;
            nudgeFrame = Time.frameCount;
            var input = BepInEx.UnityInput.Current;
            float step = input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift) ? 0.05f : 0.01f;
            if (Plugin.CargoNudgeUp.Value != KeyCode.None && input.GetKeyDown(Plugin.CargoNudgeUp.Value)) nudgePending += step;
            if (Plugin.CargoNudgeDown.Value != KeyCode.None && input.GetKeyDown(Plugin.CargoNudgeDown.Value)) nudgePending -= step;
            if (nudgePending != 0f && nudgeId >= 0) nudgedSinceCheckpoint = true;
            if (nudgeId < 0 || CargoLayout.Comparing)
            {
                nudgePending = 0f; // no slider to move: a press never waits to land on one later
            }
        }

        private static void Help2(string text)
        {
            if (cargoHelpOpen) GUILayout.Label(text, small);
        }

        private const int TuneNextRows = 5;
        private static int coverStep;

        // A load of the analysis as item ids to put on the cart (the classes' representatives).
        private static List<string> LoadIds(int[] classes)
        {
            var ids = new List<string>();
            foreach (int c in classes) ids.Add(coverage.Classes[c]);
            return ids;
        }

        // An item of the frozen view with its values from the last pack: a slider is handed the live value (it compares
        // its result with it, so a drag back to where it started is still written), while the boxes stay frozen. The
        // view's own copy when the load changed shape since it was frozen.
        private static Cargo.PlacedItem LiveOf(List<Cargo.PlacedItem> view, int k)
        {
            List<Cargo.PlacedItem> now = Cargo.Placed;
            if (now.Count == view.Count && k >= 0 && k < now.Count)
            {
                Cargo.PlacedItem a = now[k], b = view[k];
                if (a.Id == b.Id && a.Layer == b.Layer && a.Relation == b.Relation && a.Partner == b.Partner)
                {
                    return a;
                }
            }
            return view[k];
        }

        private static void DrawCargoTab()
        {
            StyleSettings s = Plugin.For(MountStyle.Cart);
            // Held: a panel control has the mouse and a button is really down (a button released while the game was
            // out of focus can leave hotControl set; the view must never stay frozen after that).
            bool held = GUIUtility.hotControl != 0 && (BepInEx.UnityInput.Current.GetMouseButton(0) || BepInEx.UnityInput.Current.GetMouseButton(1));
            if (!held)
            {
                cargoView = new List<Cargo.PlacedItem>(Cargo.Placed);
                cargoViewOnCart = Mount.IsMounted && Mount.CurrentStyle == MountStyle.Cart && Cargo.Attached;
                cargoViewIsolating = Cargo.Isolating;
                cargoViewIsolatedFrom = Cargo.IsolatedFrom;
                cargoViewPairMode = Cargo.LastPairMode ?? "";
            }
            // Everything below reads these, never the live state: a click in this pass (a toggle, Alone, Show) changes
            // the live state at once, and drawing more or fewer controls after it than the layout pass did breaks the
            // pass. The change shows from the next pass on.
            bool onCart = cargoViewOnCart, isolating = cargoViewIsolating;
            var placed = cargoView;
            Cargo.MarkPanelTint(); // the items on the cart show the same colours as the labels below while this tab is open
            int n = placed.Count, layers = 0;
            foreach (Cargo.PlacedItem p in placed) layers = Mathf.Max(layers, p.Layer + 1);
            bool refresh = false;

            // A finished edit (nothing held) becomes an undo step.
            if (!held && Event.current.type == EventType.Layout && CargoLayout.Version != checkpointVersion)
            {
                CargoLayout.Checkpoint(merge: nudgedSinceCheckpoint);
                nudgedSinceCheckpoint = false;
                checkpointVersion = CargoLayout.Version;
            }
            // The numpad nudges the last slider moved in this tab; a new load (another shape) forgets which one that was.
            var shapeKey = new System.Text.StringBuilder(isolating ? "iso|" : "|");
            foreach (Cargo.PlacedItem p in placed) shapeKey.Append(p.Id).Append(',').Append(p.Layer).Append(',').Append(p.Partner).Append(',').Append(p.Relation).Append('|');
            string shape = shapeKey.ToString();
            if (shape != nudgeShape)
            {
                nudgeShape = shape;
                nudgeId = -1;
            }
            PollNudgeKeys();
            nudgeEnabled = true;
            bool comparingAtStart = CargoLayout.Comparing; // the Compare button below switches it in this pass

            Header("Cargo on the cart");
            UpdateCoverage();
            DrawCoverageLine();
            DrawUndoRow();
            DrawReviewStrip(placed, isolating, onCart);
            if (!onCart)
            {
                GUILayout.Label("Mount in Cart style and load items. 'Tune this load' shows the controls for what is on the cart.", small);
            }
            else if (n == 0)
            {
                GUILayout.Label("The cart is empty. Load an item and its controls open under 'Tune this load'.", small);
            }
            else
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{n} item{(n == 1 ? "" : "s")}, {layers} layer{(layers == 1 ? "" : "s")}{(isolating ? $" shown out of {cargoViewIsolatedFrom} on the cart" : "")}", label);
                for (int l = layers - 1; l >= 0; l--)
                {
                    var ids = new List<string>();
                    foreach (Cargo.PlacedItem p in placed) if (p.Layer == l) ids.Add(Tinted(p, placed));
                    string where = layers == 1 ? "" : l == 0 ? " (bed)" : l == layers - 1 ? " (top)" : "";
                    GUILayout.Label($"  Layer {l + 1}{where}:  {string.Join("  |  ", ids)}", label);
                }
                if (isolating)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Two items shown alone, as a two-item cart; the whole load is still on the cart.", small);
                    if (GUILayout.Button("Show the whole load", Wd(170f)))
                    {
                        Cargo.ClearIsolation();
                    }
                    GUILayout.EndHorizontal();
                }
                Toggle("  Tint the items on the cart like the names here", Plugin.CargoTabTint, null, TipTint);
                // Show / hide parts of the load while working: view only, the layout keeps every item.
                if (n >= 2)
                {
                    bool anyHidden = Cargo.Rig.AnyHidden;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Tip("  Show on the cart", TipShow), label, Wd(130f));
                    bool changed = false;
                    bool showFirst = GUILayout.Toggle(!Cargo.Rig.HideFirst, Tip("blue", TipShow), GUI.skin.button, Wd(60f));
                    if (showFirst == Cargo.Rig.HideFirst) { Cargo.Rig.HideFirst = !showFirst; changed = true; }
                    bool showSecond = GUILayout.Toggle(!Cargo.Rig.HideSecond, Tip("orange", TipShow), GUI.skin.button, Wd(70f));
                    if (showSecond == Cargo.Rig.HideSecond) { Cargo.Rig.HideSecond = !showSecond; changed = true; }
                    for (int l = 0; l < layers; l++)
                    {
                        bool shown = !Cargo.Rig.HiddenLayers.Contains(l);
                        bool now = GUILayout.Toggle(shown, Tip($"layer {l + 1}", TipShow), GUI.skin.button, Wd(70f));
                        if (now != shown)
                        {
                            if (now) Cargo.Rig.HiddenLayers.Remove(l); else Cargo.Rig.HiddenLayers.Add(l);
                            changed = true;
                        }
                    }
                    GUILayout.EndHorizontal();
                    if (changed)
                    {
                        Cargo.RefreshViews();
                    }
                }
                GUILayout.EndVertical();
                DrawVerdicts(placed);
            }

            // ---- the cart: the floor (calibration: moves everything, tuned contacts included)
            GUILayout.Space(6f);
            GUILayout.Label("The cart", step);
            FloatSlider("Floor: cargo below the rail top", s.CargoBedDepth, -0.6f, 0.3f, 2, Cargo.RefreshViews, TipFloor);

            // ---- this load: one view, picked by how many items are on the cart
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tune this load", step, Wd(170f));
            int mode = n == 0 ? -1 : Mathf.Min(n, 3);
            // the ladder, the kinds of contact this load has lit: the order to tune in (effects run upward only)
            bool hasBeside = false, hasOn = false, hasLone = false;
            foreach (Cargo.PlacedItem it in placed)
            {
                if (it.Relation == "side" || it.Relation == "along") hasBeside = true;
                if (it.Relation == "on") { hasOn = true; if (IsLoneOnPair(placed, it)) hasLone = true; }
            }
            GUILayout.Label(Tip(ModeChip("alone", n == 1) + "  >  " + ModeChip("beside", hasBeside) + "  >  " + ModeChip("on", hasOn) + "  >  " + ModeChip("alone on a pair", hasLone),
                "The ladder: the kinds of contact, in the order to tune them. A beside relation moves every layer above it, an on relation its layer and the layers above, an alone-on-a-pair offset one item, alone nothing else. Lit: the kinds in this load."), step);
            GUILayout.EndHorizontal();
            bool comparing = comparingAtStart;
            bool wasEnabled = GUI.enabled;
            if (comparing)
            {
                GUILayout.Label("<color=#ffd257>Showing the layout as it was when this session began. Editing is locked; press 'Back to now' above.</color>", label);
                GUI.enabled = false;
            }
            if (mode >= 1 && onCart)
            {
                int open = UntunedInLoad(placed, isolating);
                GUILayout.BeginHorizontal();
                // Accept (stay) is only for a load with something untuned; Accept and next always works (with nothing left to
                // accept it just moves on: greyed out after an Accept, it read as broken, 2026-10-12); Skip never accepts.
                bool en = GUI.enabled;
                GUI.enabled = en && open > 0;
                if (GUILayout.Button(Tip(open > 0 ? $"Accept this load ({open})" : "Accept this load", TipAcceptLoad), Wd(170f)))
                {
                    refresh |= AcceptAll(placed, isolating, cargoViewPairMode) > 0;
                }
                // greyed only when nothing untuned is left anywhere (the queue is empty); read from the analysis, never changed in this pass
                bool anyLeft = coverage == null || coverage.Tuned < coverage.Total || open > 0;
                GUI.enabled = en && anyLeft;
                if (GUILayout.Button(Tip("Accept and next", TipAcceptNext), Wd(150f)))
                {
                    bool any = AcceptAll(placed, isolating, cargoViewPairMode) > 0;
                    refresh |= any;
                    LoadNextUntuned(afterAccept: any);
                }
                GUI.enabled = en && coverage != null && coverage.Tuned < coverage.Total;
                if (GUILayout.Button(Tip("Skip to next", TipSkip), Wd(120f)))
                {
                    LoadNextUntuned(afterAccept: false, skipping: placed);
                }
                GUI.enabled = en;
                GUILayout.EndHorizontal();
            }
            if (mode == 1)
            {
                GUILayout.Label("Where it lies when it is the only thing on the cart.", small);
                DrawCargoAlone(LiveOf(placed, 0), ref refresh);
            }
            else if (mode == 2)
            {
                GUILayout.Label("Pick how they lie, then move each one. Every load where these two lie this way uses it.", small);
                DrawCargoPair(LiveOf(placed, 0), LiveOf(placed, 1), isolating, cargoViewPairMode, ref refresh);
            }
            else if (mode == 3)
            {
                GUILayout.Label("One box per kind of contact in this load: an item on the layer below, or two items in one layer. Move a slider and the same contact follows everywhere. Alone shows a contact's two items by themselves.", small);
                DrawCargoStack(placed, layers, ref refresh);
            }
            else
            {
                GUILayout.Label("(nothing on the cart: the view follows the number of items you load)", small);
            }
            GUI.enabled = wasEnabled;

            // ---- options (folded)
            GUILayout.Space(10f);
            GUILayout.Label("Options", step);
            // fallbacks for untuned contacts
            GUILayout.BeginHorizontal();
            int contacts = 0, untuned = 0;
            foreach (Cargo.PlacedItem p in placed)
            {
                if (p.Relation == null || p.Relation == "bed") continue;
                contacts++;
                if (!p.Tuned) untuned++;
            }
            GUILayout.Label("Fallbacks for contacts not tuned yet" + (n >= 2 ? $"   <color=#9a9a9a>({untuned} of {contacts} in this load)</color>" : ""), label);
            bool cargoRulesOpenNow = cargoRulesOpen;
            if (GUILayout.Button(cargoRulesOpen ? "Hide" : "Show", Wd(60f))) cargoRulesOpen = !cargoRulesOpen;
            GUILayout.EndHorizontal();
            if (cargoRulesOpenNow)
            {
                GUILayout.Label(Tip("Starting points for contacts you have not tuned; a tuned contact never follows these.", "Tuning a contact (a drag, Looks right, Seat it) freezes it. Only the cart floor still moves a tuned contact."), small);
                FloatSlider("Overhang before a pair stacks", s.CargoOverhang, 0f, 0.5f, 2, Cargo.RefreshViews, TipOverhang);
                FloatSlider("Gap between items beside each other", s.CargoSideGap, 0f, 0.3f, 2, Cargo.RefreshViews, TipGap);
                FloatSlider("Nestle of an upper layer", s.CargoNestle, 0f, 0.3f, 2, Cargo.RefreshViews, TipNestle);
                FloatSlider("Layer shift, per layer up", s.CargoLayerShift, -0.3f, 0.3f, 2, Cargo.RefreshViews, TipLayerShift);
                Toggle("Upper layers rest on the measured shapes", Plugin.CargoSeatOnShapes, null, TipSeatOnShapes);
            }

            // behaviour
            GUILayout.BeginHorizontal();
            GUILayout.Label("Behaviour", label);
            bool cargoBehaviourOpenNow = cargoBehaviourOpen;
            if (GUILayout.Button(cargoBehaviourOpen ? "Hide" : "Show", Wd(60f))) cargoBehaviourOpen = !cargoBehaviourOpen;
            GUILayout.EndHorizontal();
            if (cargoBehaviourOpenNow)
            {
                Toggle("The mod draws corpses too (off: the game's shroud for the first corpse)", s.CargoDrawCorpse, Cargo.RefreshViews);
                Toggle("Cargo rocks with the cart while it rolls", s.CargoSwaysWithCart, null);
                bool behind = s.CargoSetDownBehindCart.Value;
                Toggle("Set items down behind the cart (off: in front of the keeper)", s.CargoSetDownBehindCart, null);
                if (behind)
                {
                    FloatSlider("   gap between the cart and the item set down", s.CargoSetDownGap, 0f, 1.5f, 2, null);
                }
                GUILayout.Label("How many items the cart takes: General tab, 'Cart capacity'.", small);
            }

            // help, and how to check
            GUILayout.BeginHorizontal();
            GUILayout.Label("How it fits together", label);
            bool cargoHelpOpenNow = cargoHelpOpen;
            if (GUILayout.Button(cargoHelpOpen ? "Hide" : "Show", Wd(60f))) cargoHelpOpen = !cargoHelpOpen;
            GUILayout.EndHorizontal();
            if (cargoHelpOpenNow)
            {
                GUILayout.Label("- Items of the same size are one class (all low crates, both slabs). Tuning one tunes the class.", small);
                GUILayout.Label("- A load is built from contacts between two items: lying beside each other in a layer, or one on the layer below.", small);
                GUILayout.Label("- Each contact of two classes is one tuning, used in every load where it occurs: tune it once, with 2 items or 3 or more.", small);
                GUILayout.Label("- Height in a 'beside' contact floats an item inside its layer; height in an 'on' contact is how layers stack.", small);
                GUILayout.Label("- Reset puts a contact back to the fallbacks. Right-click a slider of a lone item to zero it.", small);
                GUILayout.Label("- Check: Debug tab, 'Photograph all cargo combinations' pictures every pair twice, beside and stacked; the shapes page shows the size classes.", small);
                GUILayout.Label("- " + CargoLayout.Summary(), small);
            }

            if (refresh)
            {
                Cargo.RefreshViews();
                CargoLayout.MarkChanged();
            }
            GUILayout.Label(CargoLayout.SavesToRepo ? "Saved to the repo's cargo-layout.cfg as you edit." : "Not saved on this machine (no source folder).", small);
            nudgeEnabled = false;
            if (Event.current.type == EventType.Repaint && nudgeId > rawSeq)
            {
                nudgeId = -1; // that slider is not drawn any more
                nudgePending = 0f;
            }
        }

        // Colours for a state word inside a label (the styles have rich text on), and for an item's name in the colour
        // the item is tinted on the cart while this tab is open (Cargo.Rig: the first item of a layer blue, the second orange).
        private const string TunedWord = "<color=#8ee68e>tuned</color>";
        private const string RulesWord = "<color=#9a9a9a>by the rules</color>";
        private static string Blue(string text) => "<color=#59a6ff>" + text + "</color>";
        private static string Orange(string text) => "<color=#ff9933>" + text + "</color>";
        private static string Tinted(Cargo.PlacedItem p, List<Cargo.PlacedItem> all, string text = null)
        {
            Color c = Cargo.TintFor(p, all);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text ?? CargoSheet.ClassLabel(p.Id)}</color>";
        }

        // One of the three views of 'Tune this load': lit (gold) when it matches the load, grey otherwise.
        // Reset, always drawn (greyed when there is nothing to reset). IMGUI knows a control by its order in the frame: a
        // button that appears once a box is tuned shifts every slider after it, so the slider being dragged (whose
        // first move tuned the box) lost the drag to the slider below it (found 2026-10-10, video 18-19-36).
        private static bool ResetButton(bool active) => TipButton("Reset", TipReset, 60f, active);

        private static string ModeChip(string text, bool on) => on ? "<color=#ffd257>[ " + text + " ]</color>" : "<color=#6e6e6e>" + text + "</color>";

        private const string AlongLabel = "Forward / back   (+ toward the donkey)"; // along the cart (x); "along" and "across" read alike
        private const string AcrossLabel = "Near / far   (+ away from the camera)"; // across the cart (z)

        // One item: the item's own alone place (its class entry).
        private static void DrawCargoAlone(Cargo.PlacedItem p, ref bool refresh)
        {
            CargoLayout.ItemEntry e = CargoLayout.EditItem(p.Id);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{CargoSheet.ClassLabel(p.Id)} alone  ({(e.HasAlone ? TunedWord : RulesWord)}; {p.Id}, box {p.Box.x:0.00} × {p.Box.y:0.00} × {p.Box.z:0.00}){UsedIn("alone:" + CargoSheet.ClassOf(p.Id))}", label);
            if (LooksRightButton(!e.HasAlone))
            {
                refresh |= AcceptAlone(p);
            }
            GUILayout.EndHorizontal();
            // An entry without its own alone place lies at its bottom/beside values: the sliders start there, and the first
            // move (or Looks right) gives it its own alone place. Just looking no longer tunes it.
            Vector3 v = e.HasAlone ? e.Alone : e.Base;
            bool moved = false;
            moved |= RawSlider("  Height", ref v.y, -0.4f, 0.4f, tip: TipHeightAlone);
            moved |= RawSlider("  " + AcrossLabel, ref v.z, -0.5f, 0.5f, tip: TipAcross);
            moved |= RawSlider("  " + AlongLabel, ref v.x, -0.5f, 0.5f, tip: TipAlong);
            if (moved)
            {
                e.HasAlone = true;
                e.Alone = v;
                refresh = true;
            }
            GUILayout.EndVertical();
        }

        // Two items: the pair's contact as shown (beside or on top), with its arrangement and each item's place.
        private static void DrawCargoPair(Cargo.PlacedItem p0, Cargo.PlacedItem p1, bool isolating, string lastPairMode, ref bool refresh)
        {
            CargoLayout.PairEntry pair = CargoLayout.EditPair(p0.Id, p1.Id);
            bool firstIsA = CargoLayout.FirstIsA(pair, p0.Id, p1.Id);
            // The contact on screen: what the packer did (side / along: beside; stack: on top), which is the pair's mode on
            // a two-item cart and the isolated contact otherwise. The sliders edit that contact's set.
            string shown = string.IsNullOrEmpty(lastPairMode) ? pair.Mode : lastPairMode;
            bool shownStack = CargoLayout.PairEntry.IsStack(shown);
            bool tunedNow = pair.TunedFor(shown);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{CargoSheet.ClassLabel(p0.Id)} + {CargoSheet.ClassLabel(p1.Id)}: {(shownStack ? "one on the other" : shown == "along" ? "end to end along the cart" : "side by side across")}  ({(tunedNow ? TunedWord : RulesWord)}; the other way: {(shownStack ? (pair.BesideTuned ? TunedWord : RulesWord) : (pair.TopTuned ? TunedWord : RulesWord))}){UsedIn(KeyOf(shownStack ? "on" : "side", p1.Id, p0.Id))}", label);
            if (!isolating)
            {
                // The arrangement a two-item cart shows; each arrangement keeps its own tuning. The lit button again (or
                // side <-> along) starts that contact over by the rules.
                GUILayout.BeginHorizontal();
                GUILayout.Label("  Arrangement", label, Wd(120f));
                foreach (string mode in new[] { "auto", "side", "along", "stack" })
                {
                    bool on = pair.Mode == mode;
                    string text = mode == "auto" ? "let the rules pick" : mode == "side" ? "across" : mode == "along" ? "along" : "stacked";
                    // A click is a change of the toggle's state (a lit toggle returns true on every event; taking that as
                    // a click started a tuned pair over every frame, 2026-09-30).
                    bool now = GUILayout.Toggle(on, Tip(text, TipArrangement), GUI.skin.button, Wd(mode == "auto" ? 110f : 70f));
                    bool clicked = now != on && (tunedNow || now);
                    if (clicked)
                    {
                        bool sameKind = CargoLayout.PairEntry.IsStack(mode) == shownStack;
                        if (tunedNow && (on || sameKind))
                        {
                            pair.ClearFor(shown);
                            Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B}: the {(shownStack ? "on-top" : "beside")} contact back to the rules ({mode}).");
                        }
                        pair.Mode = mode;
                        refresh = true;
                    }
                }
                if (LooksRightButton(!tunedNow))
                {
                    refresh |= AcceptPair2(p0, p1, isolating, lastPairMode);
                }
                if (ResetButton(tunedNow))
                {
                    pair.ClearFor(shown);
                    refresh = true;
                }
                GUILayout.EndHorizontal();
            }
            // Stacked, the item on top is listed first, as on screen; k stays the packing index (which maps to A or B).
            bool topFirst = p1.OnTop;
            for (int n = 0; n < 2; n++)
            {
                int k = topFirst ? 1 - n : n;
                Cargo.PlacedItem p = k == 0 ? p0 : p1;
                Cargo.PlacedItem other = k == 0 ? p1 : p0;
                Vector3 v = p.Pos;
                string tag = CargoSheet.ClassLabel(p.Id) == CargoSheet.ClassLabel(other.Id) ? $" ({n + 1})" : "";
                GUILayout.BeginHorizontal();
                GUILayout.Label($"  {(k == 0 ? Blue(CargoSheet.ClassLabel(p.Id) + tag) : Orange(CargoSheet.ClassLabel(p.Id) + tag))}{(shownStack ? (k == 0 ? ", below" : ", on top") : "")}", label);
                bool seatPressed = SeatButton(shownStack && p.Layer > 0 && CargoShapes.Count > 0);
                GUILayout.EndHorizontal();
                bool moved = false;
                if (seatPressed)
                {
                    float d = CargoShapes.SeatDelta(LiveResult(new List<Cargo.PlacedItem> { p0, p1 }), k, CargoSheet.ShapeOf);
                    if (!float.IsNaN(d))
                    {
                        v.y += d;
                        moved = true;
                        Plugin.Log.LogInfo($"Cargo layout: {p.Id} seated on {other.Id} ({d:+0.000;-0.000}).");
                    }
                }
                moved |= RawSlider("    Height above the floor", ref v.y, -0.3f, 1.0f, canReset: false, tip: TipHeightTwo);
                moved |= RawSlider("    " + AcrossLabel, ref v.z, -0.5f, 0.5f, canReset: false, tip: TipAcross);
                moved |= RawSlider("    " + AlongLabel, ref v.x, -0.6f, 0.6f, canReset: false, tip: TipAlong);
                if (moved)
                {
                    if (!tunedNow)
                    {
                        // freeze where both are now (in the contact on screen), then apply this move
                        if (!isolating) pair.Mode = shown; // a two-item cart keeps showing what was tuned
                        pair.SetFor(shown, firstIsA ? p0.Pos : p1.Pos, firstIsA ? p1.Pos : p0.Pos);
                        tunedNow = true;
                        Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B} tuned ({shown}: the {(shownStack ? "on-top" : "beside")} contact).");
                    }
                    pair.SetPos(shown, (k == 0) == firstIsA, v);
                    refresh = true;
                }
            }
            GUILayout.EndVertical();
        }

        // Three or more: one box per kind of contact in the load. On: the layer's origin relative to its support (the item the
        // layer sits on). Beside: both items' places in their layer. The sliders are live in the load; the freeze on the
        // first move writes the contact where it is now, so nothing jumps (CargoPacker, tested in PackerScan).
        private static void DrawCargoStack(List<Cargo.PlacedItem> placed, int layers, ref bool refresh)
        {
            var first = new Dictionary<string, int>();
            var where = new Dictionary<string, List<int>>();
            var order = new List<string>();
            for (int k = 0; k < placed.Count; k++)
            {
                Cargo.PlacedItem it = placed[k];
                if (it.Relation == null || it.Relation == "bed" || it.Partner < 0 || it.Partner >= placed.Count)
                {
                    continue;
                }
                Cargo.PlacedItem partner = placed[it.Partner];
                string key = KeyOf(it.Relation, it.Id, partner.Id); // by size class: two members of one class are one contact (one layout entry)
                if (!first.ContainsKey(key))
                {
                    first[key] = k;
                    where[key] = new List<int>();
                    order.Add(key);
                }
                if (!where[key].Contains(it.Layer + 1)) where[key].Add(it.Layer + 1);
            }
            // in the order the layers are built, bottom up: the bed pair first, then what sits on it
            // An item lying alone on top of a pair: its own correction (the class's onpair offset), one box per class.
            var lone = new HashSet<string>();
            for (int k = 0; k < placed.Count; k++)
            {
                Cargo.PlacedItem it = placed[k];
                if (it.Relation != "on" || it.Partner < 0) continue;
                int inLayer = 0, inBelow = 0;
                foreach (Cargo.PlacedItem o in placed)
                {
                    if (o.Layer == it.Layer) inLayer++;
                    if (o.Layer == it.Layer - 1) inBelow++;
                }
                if (inLayer != 1 || inBelow < 2 || !lone.Add(it.Id)) continue;
                CargoLayout.ItemEntry e = CargoLayout.EditItem(it.Id);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Tinted(it, placed)} alone on top of a pair   ({(e.OnPairTuned ? TunedWord : RulesWord)}; layer {it.Layer + 1}){UsedIn("onpair:" + CargoSheet.ClassOf(it.Id))}", label);
                if (LooksRightButton(!e.OnPairTuned))
                {
                    refresh |= AcceptOnPair(it);
                }
                if (SeatButton(CargoShapes.Count > 0))
                {
                    float d = CargoShapes.SeatDelta(LiveResult(placed), k, CargoSheet.ShapeOf);
                    if (!float.IsNaN(d))
                    {
                        e.OnPair.y += d;
                        e.HasOnPair = true;
                        refresh = true;
                        Plugin.Log.LogInfo($"Cargo layout: {it.Id} alone on a pair seated ({d:+0.000;-0.000}).");
                    }
                }
                if (ResetButton(e.OnPairTuned))
                {
                    e.OnPair = Vector3.zero;
                    e.HasOnPair = false;
                    refresh = true;
                }
                GUILayout.EndHorizontal();
                TuneFirstNote(placed, "onpair");
                bool movedP = false;
                movedP |= RawSlider("  Height", ref e.OnPair.y, -0.4f, 0.4f, canReset: false, tip: TipOnPair);
                movedP |= RawSlider("  " + AcrossLabel, ref e.OnPair.z, -0.4f, 0.4f, canReset: false, tip: TipOnPair);
                movedP |= RawSlider("  " + AlongLabel, ref e.OnPair.x, -0.4f, 0.4f, canReset: false, tip: TipOnPair);
                if (movedP)
                {
                    e.HasOnPair = true;
                    refresh = true;
                }
                GUILayout.EndVertical();
            }
            foreach (string key in order)
            {
                Cargo.PlacedItem pl = LiveOf(placed, first[key]);
                Cargo.PlacedItem q = LiveOf(placed, pl.Partner);
                bool onTop = pl.Relation == "on";
                CargoLayout.PairEntry pair = CargoLayout.EditPair(q.Id, pl.Id);
                bool qIsA = CargoLayout.FirstIsA(pair, q.Id, pl.Id);
                bool tuned = onTop ? pair.TopTuned : pair.BesideTuned;
                var ls = where[key];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label((onTop ? $"{Tinted(pl, placed)} on {Tinted(q, placed)}" : $"{Tinted(q, placed)}  |  {Tinted(pl, placed)} in one layer") + $"   ({(tuned ? TunedWord : RulesWord)}; layer{(ls.Count == 1 ? "" : "s")} {string.Join(", ", ls)}){UsedIn(KeyOf(pl.Relation, pl.Id, q.Id))}", label);
                if (TipButton("Alone", TipAlone, 60f))
                {
                    Cargo.Isolate(q.Guid, pl.Guid, onTop ? "stack" : "beside");
                }
                if (LooksRightButton(!tuned))
                {
                    refresh |= onTop ? AcceptOn(pl, q, placed, first[key]) : AcceptBeside(pl, q);
                }
                if (SeatButton(onTop && CargoShapes.Count > 0))
                {
                    refresh |= SeatOn(placed, first[key], pl, q);
                }
                if (ResetButton(tuned))
                {
                    pair.ClearFor(onTop ? "stack" : "side");
                    Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B}: the {(onTop ? "on-top" : "beside")} contact back to the rules (from a load of {placed.Count}).");
                    refresh = true;
                }
                GUILayout.EndHorizontal();
                TuneFirstNote(placed, onTop ? "on" : "beside");
                if (onTop)
                {
                    Vector3 v = pl.Rel;
                    Vector3 was = v;
                    bool moved = false;
                    moved |= RawSlider("  Height", ref v.y, -0.3f, 1.0f, canReset: false, tip: TipHeightOn + $" ({CargoSheet.ClassLabel(q.Id)} is {q.Box.y:0.00} tall.)");
                    moved |= RawSlider("  " + AcrossLabel, ref v.z, -0.5f, 0.5f, canReset: false, tip: TipAcross);
                    moved |= RawSlider("  " + AlongLabel, ref v.x, -0.6f, 0.6f, canReset: false, tip: TipAlong);
                    if (moved)
                    {
                        if (!pair.TopTuned)
                        {
                            CargoLayout.ItemEntry eq = CargoLayout.ForItem(q.Id);
                            CargoPacker.FreezeOnTop(pair, qIsA, eq != null ? eq.Base : Vector3.zero, was);
                            FreezeLayerOf(placed, first[key]);
                            Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B} tuned (on top, from a load of {placed.Count}).");
                        }
                        CargoPacker.WriteOnTop(pair, qIsA, v);
                        refresh = true;
                    }
                }
                else
                {
                    for (int n = 0; n < 2; n++)
                    {
                        Cargo.PlacedItem item = n == 0 ? q : pl;
                        bool isA = n == 0 ? qIsA : !qIsA;
                        string tag = CargoSheet.ClassLabel(q.Id) == CargoSheet.ClassLabel(pl.Id) ? $" ({n + 1})" : "";
                        Vector3 v = item.LayerPos;
                        bool moved = false;
                        bool layerFirstAbove = n == 0 && item.Layer > 0; // at the origin: its height is the layer's (the on-top box)
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("  " + Tinted(item, placed, CargoSheet.ClassLabel(item.Id) + tag), label);
                        if (SeatButton(item.Layer > 0 && !layerFirstAbove && CargoShapes.Count > 0))
                        {
                            float d = CargoShapes.SeatDelta(LiveResult(placed), n == 0 ? pl.Partner : first[key], CargoSheet.ShapeOf);
                            if (!float.IsNaN(d))
                            {
                                v.y += d;
                                moved = true;
                                Plugin.Log.LogInfo($"Cargo layout: {item.Id} seated in its layer ({d:+0.000;-0.000}).");
                            }
                        }
                        GUILayout.EndHorizontal();
                        // the slider is always drawn (the control order); greyed for the first item of an upper layer
                        bool en = GUI.enabled;
                        GUI.enabled = en && !layerFirstAbove;
                        moved |= RawSlider(layerFirstAbove ? "    Height (the layer's)" : item.Layer == 0 ? "    Height above the bed" : "    Height, relative to " + CargoSheet.ClassLabel(q.Id), ref v.y, -0.3f, 1.0f, canReset: false,
                            tip: layerFirstAbove ? TipHeightLayerFirst : item.Layer == 0 ? TipHeightBed : TipHeightRel);
                        GUI.enabled = en;
                        moved |= RawSlider("    " + AcrossLabel, ref v.z, -0.5f, 0.5f, canReset: false, tip: TipAcross);
                        moved |= RawSlider("    " + AlongLabel, ref v.x, -0.6f, 0.6f, canReset: false, tip: TipAlong);
                        if (moved)
                        {
                            if (!pair.BesideTuned)
                            {
                                CargoPacker.FreezeBeside(pair, qIsA, q.BesideStored, pl.BesideStored, pl.Relation);
                                Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B} tuned (beside, from a load of {placed.Count}).");
                            }
                            CargoPacker.WriteBeside(pair, isA, v + new Vector3(0f, item.LayerBase, 0f));
                            refresh = true;
                        }
                    }
                }
                GUILayout.EndVertical();
            }
        }
    }
}
