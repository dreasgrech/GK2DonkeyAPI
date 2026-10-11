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

        // What the tab is drawn from, frozen while any panel control is held (GUIUtility.hotControl, with a button down). IMGUI knows a
        // control by its order in the pass, so the boxes above a slider must not come and go while it is dragged, or
        // the drag lands on another slider (2026-10-10, video 18-19-36). Dragging the overhang or the gap re-arranges
        // the load (more or fewer contact boxes above those sliders), and a pack can change the load in between; the
        // structure follows on release. The values being dragged come from the slider itself, so they stay live.
        private static List<Cargo.PlacedItem> cargoView = new List<Cargo.PlacedItem>();
        private static bool cargoViewOnCart, cargoViewIsolating;
        private static int cargoViewIsolatedFrom;
        private static string cargoViewPairMode = "";

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

            Header("Cargo on the cart");
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
                bool tintOn = Plugin.CargoTabTint.Value;
                Toggle("  Tint the items on the cart like the names here", Plugin.CargoTabTint, null);
                if (tintOn)
                {
                    GUILayout.Label("  " + Blue("Blue") + ": the first item of a layer. " + Orange("Orange") + ": the second, or one alone on top. Lighter: a higher layer. The same hue moves with the same sliders.", small);
                }
                // Show / hide parts of the load while working: view only, the layout keeps every item.
                if (n >= 2)
                {
                    bool anyHidden = Cargo.Rig.AnyHidden;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Show on the cart", label, Wd(130f));
                    bool changed = false;
                    bool showFirst = GUILayout.Toggle(!Cargo.Rig.HideFirst, "blue", GUI.skin.button, Wd(60f));
                    if (showFirst == Cargo.Rig.HideFirst) { Cargo.Rig.HideFirst = !showFirst; changed = true; }
                    bool showSecond = GUILayout.Toggle(!Cargo.Rig.HideSecond, "orange", GUI.skin.button, Wd(70f));
                    if (showSecond == Cargo.Rig.HideSecond) { Cargo.Rig.HideSecond = !showSecond; changed = true; }
                    for (int l = 0; l < layers; l++)
                    {
                        bool shown = !Cargo.Rig.HiddenLayers.Contains(l);
                        bool now = GUILayout.Toggle(shown, $"layer {l + 1}", GUI.skin.button, Wd(70f));
                        if (now != shown)
                        {
                            if (now) Cargo.Rig.HiddenLayers.Remove(l); else Cargo.Rig.HiddenLayers.Add(l);
                            changed = true;
                        }
                    }
                    GUILayout.EndHorizontal();
                    if (anyHidden)
                    {
                        GUILayout.Label("  Hidden items still count in the layout (nothing moves); they show again when this tab closes.", small);
                    }
                    if (changed)
                    {
                        Cargo.RefreshViews();
                    }
                }
                GUILayout.EndVertical();
            }

            // ---- the cart: the floor (calibration: moves everything, tuned contacts included)
            GUILayout.Space(6f);
            GUILayout.Label("The cart", step);
            GUILayout.Label("Set once: where the bottom of the load rests. It moves everything, tuned or not.", small);
            FloatSlider("Floor: cargo below the rail top", s.CargoBedDepth, -0.6f, 0.3f, 2, Cargo.RefreshViews);

            // ---- this load: one view, picked by how many items are on the cart
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tune this load", step, Wd(170f));
            int mode = n == 0 ? -1 : Mathf.Min(n, 3);
            GUILayout.Label(ModeChip("1 item", mode == 1) + "    " + ModeChip("2 items", mode == 2) + "    " + ModeChip("3 or more", mode == 3), step);
            GUILayout.EndHorizontal();
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
                GUILayout.Label("Starting points for any contact you have not tuned. Tuning a contact freezes it: these never move it again.", small);
                GUILayout.Label("  Which way two items lie (pairs set to 'let the rules pick')", label);
                FloatSlider("Overhang: how far a pair may exceed the bed before stacking", s.CargoOverhang, 0f, 0.5f, 2, Cargo.RefreshViews);
                FloatSlider("Gap between two items lying beside each other", s.CargoSideGap, 0f, 0.3f, 2, Cargo.RefreshViews);
                GUILayout.Label("  How an untuned layer sits on the one below", label);
                FloatSlider("Nestle: how far it sinks in", s.CargoNestle, 0f, 0.3f, 2, Cargo.RefreshViews);
                FloatSlider("Layer shift: each layer up moves forward / back   (+ back, - toward the donkey)", s.CargoLayerShift, -0.3f, 0.3f, 2, Cargo.RefreshViews);
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
            return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text ?? p.Id}</color>";
        }

        // One of the three views of 'Tune this load': lit (gold) when it matches the load, grey otherwise.
        // Reset, always drawn (greyed when there is nothing to reset). IMGUI knows a control by its order in the frame: a
        // button that appears once a box is tuned shifts every slider after it, so the slider being dragged (whose
        // first move tuned the box) lost the drag to the slider below it (found 2026-10-10, video 18-19-36).
        private static bool ResetButton(bool active)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && active;
            bool pressed = GUILayout.Button("Reset", Wd(60f));
            GUI.enabled = was;
            return pressed && active;
        }

        private static string ModeChip(string text, bool on) => on ? "<color=#ffd257>[ " + text + " ]</color>" : "<color=#6e6e6e>" + text + "</color>";

        private const string AlongLabel = "Forward / back   (+ toward the donkey)"; // along the cart (x); "along" and "across" read alike
        private const string AcrossLabel = "Near / far   (+ away from the camera)"; // across the cart (z)

        // One item: the item's own alone place (its class entry).
        private static void DrawCargoAlone(Cargo.PlacedItem p, ref bool refresh)
        {
            CargoLayout.ItemEntry e = CargoLayout.EditItem(p.Id);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{p.Id} alone  (class '{e.Id}', box {p.Box.x:0.00} long × {p.Box.y:0.00} high × {p.Box.z:0.00} across)", label);
            // An entry without its own alone place starts from its bottom/beside values, where it sits now.
            if (!e.HasAlone)
            {
                e.HasAlone = true;
                e.Alone = e.Base;
            }
            refresh |= RawSlider("  Height", ref e.Alone.y, -0.4f, 0.4f);
            refresh |= RawSlider("  " + AcrossLabel, ref e.Alone.z, -0.5f, 0.5f);
            refresh |= RawSlider("  " + AlongLabel, ref e.Alone.x, -0.5f, 0.5f);
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
            GUILayout.Label($"{p0.Id} + {p1.Id}: {(shownStack ? "one on the other" : shown == "along" ? "end to end along the cart" : "side by side across")}  ({(tunedNow ? TunedWord : RulesWord)}; the other way: {(shownStack ? (pair.BesideTuned ? TunedWord : RulesWord) : (pair.TopTuned ? TunedWord : RulesWord))})", label);
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
                    bool now = GUILayout.Toggle(on, text, GUI.skin.button, Wd(mode == "auto" ? 110f : 70f));
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
                if (ResetButton(tunedNow))
                {
                    pair.ClearFor(shown);
                    refresh = true;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("  The lit arrangement again starts this contact over by the rules; the others switch and keep both tunings.", small);
            }
            // Stacked, the item on top is listed first, as on screen; k stays the packing index (which maps to A or B).
            bool topFirst = p1.OnTop;
            for (int n = 0; n < 2; n++)
            {
                int k = topFirst ? 1 - n : n;
                Cargo.PlacedItem p = k == 0 ? p0 : p1;
                Cargo.PlacedItem other = k == 0 ? p1 : p0;
                Vector3 v = p.Pos;
                string tag = p.Id == other.Id ? $" ({n + 1})" : "";
                GUILayout.Label($"  {(k == 0 ? Blue(p.Id + tag) : Orange(p.Id + tag))}{(shownStack ? (k == 0 ? ", below" : ", on top") : "")}", label);
                bool moved = false;
                moved |= RawSlider("    Height above the floor", ref v.y, -0.3f, 1.0f, canReset: false);
                moved |= RawSlider("    " + AcrossLabel, ref v.z, -0.5f, 0.5f, canReset: false);
                moved |= RawSlider("    " + AlongLabel, ref v.x, -0.6f, 0.6f, canReset: false);
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
                string key = (it.Relation == "on" ? "on:" : "beside:") + (string.CompareOrdinal(partner.Id, it.Id) <= 0 ? partner.Id + "+" + it.Id : it.Id + "+" + partner.Id);
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
                GUILayout.Label($"{Tinted(it, placed)} alone on top of a pair   ({(e.OnPair != Vector3.zero ? TunedWord : RulesWord)}; layer {it.Layer + 1})", label);
                if (ResetButton(e.OnPair != Vector3.zero))
                {
                    e.OnPair = Vector3.zero;
                    refresh = true;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label($"  Moves only a {it.Id} lying alone on top of a pair; nothing under it moves.", small);
                refresh |= RawSlider("  Height", ref e.OnPair.y, -0.4f, 0.4f, canReset: false);
                refresh |= RawSlider("  " + AcrossLabel, ref e.OnPair.z, -0.4f, 0.4f, canReset: false);
                refresh |= RawSlider("  " + AlongLabel, ref e.OnPair.x, -0.4f, 0.4f, canReset: false);
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
                GUILayout.Label((onTop ? $"{Tinted(pl, placed)} on {Tinted(q, placed)}" : $"{Tinted(q, placed)}  |  {Tinted(pl, placed)} in one layer") + $"   ({(tuned ? TunedWord : RulesWord)}; layer{(ls.Count == 1 ? "" : "s")} {string.Join(", ", ls)})", label);
                if (GUILayout.Button("Alone", Wd(60f)))
                {
                    Cargo.Isolate(q.Guid, pl.Guid, onTop ? "stack" : "beside");
                }
                if (ResetButton(tuned))
                {
                    pair.ClearFor(onTop ? "stack" : "side");
                    Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B}: the {(onTop ? "on-top" : "beside")} contact back to the rules (from a load of {placed.Count}).");
                    refresh = true;
                }
                GUILayout.EndHorizontal();
                if (onTop)
                {
                    GUILayout.Label($"  Height: how high {Tinted(pl, placed)} sits on {Tinted(q, placed)} ({q.Id} is {q.Box.y:0.00} tall); the whole upper layer moves with it. This is how the layers stack.", small);
                    Vector3 v = pl.Rel;
                    Vector3 was = v;
                    bool moved = false;
                    moved |= RawSlider("  Height", ref v.y, -0.3f, 1.0f, canReset: false);
                    moved |= RawSlider("  " + AcrossLabel, ref v.z, -0.5f, 0.5f, canReset: false);
                    moved |= RawSlider("  " + AlongLabel, ref v.x, -0.6f, 0.6f, canReset: false);
                    if (moved)
                    {
                        if (!pair.TopTuned)
                        {
                            CargoLayout.ItemEntry eq = CargoLayout.ForItem(q.Id);
                            CargoPacker.FreezeOnTop(pair, qIsA, eq != null ? eq.Base : Vector3.zero, was);
                            Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B} tuned (on top, from a load of {placed.Count}).");
                        }
                        CargoPacker.WriteOnTop(pair, qIsA, v);
                        refresh = true;
                    }
                }
                else
                {
                    GUILayout.Label("  Each item's place in the layer. Height floats an item inside its layer; the layer above sits on the highest point.", small);
                    for (int n = 0; n < 2; n++)
                    {
                        Cargo.PlacedItem item = n == 0 ? q : pl;
                        bool isA = n == 0 ? qIsA : !qIsA;
                        string tag = q.Id == pl.Id ? $" ({n + 1})" : "";
                        Vector3 v = item.LayerPos;
                        bool moved = false;
                        GUILayout.Label("  " + Tinted(item, placed, item.Id + tag), label);
                        moved |= RawSlider("    Height in the layer", ref v.y, -0.3f, 1.0f, canReset: false);
                        moved |= RawSlider("    " + AcrossLabel, ref v.z, -0.5f, 0.5f, canReset: false);
                        moved |= RawSlider("    " + AlongLabel, ref v.x, -0.6f, 0.6f, canReset: false);
                        if (moved)
                        {
                            if (!pair.BesideTuned)
                            {
                                CargoPacker.FreezeBeside(pair, qIsA, q.LayerPos, pl.LayerPos, pl.Relation);
                                Plugin.Log.LogInfo($"Cargo layout: pair {pair.A}+{pair.B} tuned (beside, from a load of {placed.Count}).");
                            }
                            CargoPacker.WriteBeside(pair, isA, v);
                            refresh = true;
                        }
                    }
                }
                GUILayout.EndVertical();
            }
        }
    }
}
