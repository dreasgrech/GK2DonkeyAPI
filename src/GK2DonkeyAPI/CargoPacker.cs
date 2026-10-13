using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The cargo packer's arithmetic, free of the scene: items are boxes, corrections come through Lookup, and the
    // result is places and relations. Cargo.Rig.Pack feeds it the measured views and applies the result; the panel's
    // edits go through the Freeze/Write helpers below; tests/PackerScan runs all of it without the game, so a slider
    // loop that drifts or a layout that jumps is caught there and not on the cart (docs/cargo-layout-layers.md).
    //
    // A load is composed of RELATIONS between two items. BESIDE: two items in one layer, side by side across the bed
    // when the size classes' widths plus the gap are under the bed plus the overhang (whole centimetres; equal is no
    // room), else end to end along the cart by the same test; a tuned beside relation is both places in the layer's
    // frame. ON TOP: a new layer over its support, the layer below's first item (the larger footprint; a fixed
    // choice); the layer's origin is at the highest point of the layer below less the nestle, over the support (or the
    // middle of a same-class pair), moved along by the layer shift when the layer below is a pair; a tuned on-top
    // relation puts the origin at the support plus (b minus a), raised by the shelf lift when the support's neighbour stands
    // taller. The on-top relation places the LAYER, the beside
    // relation arranges the items inside it; they never move the same number. One and two items come out exactly as
    // they did before layers existed: a two-item cart shows the relation of the pair's mode.
    internal static class CargoPacker
    {
        internal struct Item
        {
            public string Id;
            public Vector3 Box;   // measured size: x along the cart, y up, z across
            public int LoadOrder; // inventory index, the last tie-breaker
        }

        internal sealed class Settings
        {
            public float Floor, Gap, Overhang, Nestle, LayerShift, BedWidth, BedLength;
            public string Forced; // null, "beside" or "stack": a pair shown in one relation (the sheet, the panel's Alone)
        }

        internal sealed class Lookup
        {
            public Func<string, CargoLayout.ItemEntry> ForItem;
            public Func<string, string, CargoLayout.PairEntry> ForPair;
            public Func<CargoLayout.PairEntry, string, string, bool> FirstIsA;
            public Func<string, string, bool> SameClass;
            public Func<string, Vector3> BoxOf; // the size class's box; zero when unknown (the live box is used then)
            public Func<string, CargoShapes.Shape> ShapeOf; // the measured shapes (null: boxes only, as before)
        }

        internal sealed class Result
        {
            public int[] Order;         // result index -> input index (the packer sorts: larger footprints first)
            public string[] Id;
            public Vector3[] Box;
            public Vector3[] Target;    // footprint centre (x, z) and bottom (y), before the view's pivot correction
            public Vector3[] Origin;    // the origin of the item's layer (the floor origin on the bed)
            public bool[] OnTop;
            public bool[] Tuned;        // placed by a tuned relation
            public int[] Layer;
            public int[] Partner;       // the layer's first item (beside) or the support (on); -1 on the bed
            public string[] Relation;   // "bed", "side", "along", "on"
            public float[] Lift;        // on: how far the shelf (the layer below's highest point) stands above the support's top
            public float[] LayerBase;   // beside, above the bed: the first item's own height in the layer, taken off both items (see the beside branch)
            public string Note, PairMode;
            public bool PairTuned;
            public float Floor;
            public int Count => Target.Length;

            // What the on-top relation stores: the layer's origin relative to the support, without the shelf lift. Beside:
            // the item relative to the layer's first item. The bed: the place itself (above the floor origin).
            public Vector3 Rel(int i) => Relation[i] == "on" ? Origin[i] - Target[Partner[i]] - new Vector3(0f, Lift[i], 0f) : Partner[i] >= 0 ? Target[i] - Target[Partner[i]] : Target[i] - new Vector3(0f, Floor, 0f);

            // The item's place in its layer's frame (what the panel's beside sliders show).
            public Vector3 LayerPos(int i) => Target[i] - Origin[i];

            // What a beside relation stores for item i: its place in the layer plus the layer base taken off above the bed, so
            // a freeze or a write from an upper layer keeps the pair's bed heights (every beside freeze and write goes
            // through this).
            public Vector3 BesideStored(int i) => LayerPos(i) + new Vector3(0f, LayerBase[i], 0f);
        }

        internal static bool LiftOff; // tests only: the packer without the shelf lift, to measure what it does

        internal static Result Pack(IList<Item> input, Settings s, Lookup L)
        {
            int n = input.Count;
            var order = new int[n];
            for (int k = 0; k < n; k++) order[k] = k;
            Array.Sort(order, (ia, ib) =>
            {
                Item a = input[ia], b = input[ib];
                // Footprints in whole square centimetres: each view is measured on its own, and the float noise of that
                // measurement moves with the view, so two equal items (two zombies) compared exactly swapped order, and
                // with it their places in a tuned pair, on every slider move (seen 2026-10-05).
                int areaA = Mathf.RoundToInt(a.Box.x * a.Box.z * 10000f), areaB = Mathf.RoundToInt(b.Box.x * b.Box.z * 10000f);
                int c = areaB.CompareTo(areaA);
                if (c == 0) c = string.CompareOrdinal(a.Id, b.Id); // equal footprints: by id, not by who came first
                return c != 0 ? c : a.LoadOrder.CompareTo(b.LoadOrder);
            });
            var r = new Result
            {
                Order = order, Id = new string[n], Box = new Vector3[n], Target = new Vector3[n], Origin = new Vector3[n],
                OnTop = new bool[n], Tuned = new bool[n], Layer = new int[n], Partner = new int[n], Relation = new string[n], Lift = new float[n], LayerBase = new float[n],
                Floor = s.Floor, PairMode = "", PairTuned = false, Note = "",
            };
            if (n == 0)
            {
                r.Note = "empty";
                return r;
            }
            for (int i = 0; i < n; i++)
            {
                r.Id[i] = input[order[i]].Id;
                r.Box[i] = input[order[i]].Box;
            }
            Vector3[] target = r.Target;
            float floor = s.Floor;
            var note = new System.Text.StringBuilder();

            CargoLayout.ItemEntry e0 = L.ForItem(r.Id[0]);
            // Alone on the cart: the item's own alone offsets when it has them, else the bottom/beside ones.
            bool aloneOwn = n == 1 && e0 != null && e0.HasAlone;
            target[0] = new Vector3(0f, floor, 0f) + (e0 == null ? Vector3.zero : aloneOwn ? e0.Alone : e0.Base);
            r.Relation[0] = "bed";
            r.Partner[0] = -1;
            r.Origin[0] = new Vector3(0f, floor, 0f);

            float gap = s.Gap, over = s.Overhang;
            int layer = 0, layerStart = 0;   // the current layer and the index of its first item
            Vector3 centre = Vector3.zero;   // where the current layer is centred (x along, z across), no offsets
            float layerFloor = floor;
            for (int i = 1; i < n; i++)
            {
                string cur = r.Id[i];
                Vector3 curBox = r.Box[i];
                CargoLayout.ItemEntry e = L.ForItem(cur);
                if (i == layerStart + 1)
                {
                    // The layer holds one item so far: beside it when the pair fits (or its mode says so).
                    string a = r.Id[layerStart];
                    Vector3 aBox = r.Box[layerStart];
                    CargoLayout.ItemEntry ea = L.ForItem(a);
                    CargoLayout.PairEntry pair = L.ForPair(a, cur);
                    string mode = pair != null ? pair.Mode : "auto";
                    if (s.Forced == "stack")
                    {
                        mode = "stack";
                    }
                    else if (s.Forced == "beside" && CargoLayout.PairEntry.IsStack(mode))
                    {
                        mode = "auto";
                    }
                    float w0 = aBox.z, w1 = curBox.z; // across the bed (the live boxes place the items)
                    float l0 = aBox.x, l1 = curBox.x; // along the cart
                    // Whether they fit is decided on the size classes' boxes in whole centimetres, never on the live
                    // measurement: a crate beside a slab or a log sums to exactly the limit (0.50 + 0.02 + 0.35 against
                    // 0.75 + 0.12), and the live boxes (measured on a cart that may stand on a slope) flipped that pair
                    // between runs of the sheet (seen 2026-10-10). Equal means no room.
                    Vector3 ca = StableBox(L, a, aBox), cb = StableBox(L, cur, curBox);
                    bool side = mode == "side" || (mode == "auto" && Cm(ca.z + gap + cb.z) < Cm(s.BedWidth + over));
                    bool along = !side && (mode == "along" || (mode == "auto" && Cm(ca.x + gap + cb.x) < Cm(s.BedLength + over)));
                    if (i == 1)
                    {
                        r.PairMode = side ? "side" : along ? "along" : "stack";
                    }
                    if (side || along)
                    {
                        if (layer > 0) note.Append($"; layer {layer + 1}: ");
                        var origin = new Vector3(centre.x, layerFloor, centre.z);
                        bool tuned = pair != null && pair.BesideTuned;
                        if (tuned)
                        {
                            bool firstIsA = L.FirstIsA(pair, a, cur);
                            target[layerStart] = origin + (firstIsA ? pair.BesideA : pair.BesideB);
                            target[i] = origin + (firstIsA ? pair.BesideB : pair.BesideA);
                            if (i == 1) r.PairTuned = true;
                            note.Append($"{a} | {cur} beside (tuned {(side ? "side" : "along")})");
                        }
                        else
                        {
                            // A pair is a pair in every layer: the base (bottom / beside) offsets. Freezing the relation
                            // from one layer then reproduces every other layer exactly (PackerScan found the upper
                            // layers moving when the on-top offsets were used there, 2026-10-10).
                            Vector3 offA = ea == null ? Vector3.zero : ea.Base;
                            Vector3 offB = e == null ? Vector3.zero : e.Base;
                            if (side)
                            {
                                float total = w0 + gap + w1;
                                target[layerStart] = origin + new Vector3(0f, 0f, total * 0.5f - w0 * 0.5f) + offA;
                                target[i] = origin + new Vector3(0f, 0f, -total * 0.5f + w1 * 0.5f) + offB;
                                note.Append($"{a} | {cur} side by side across ({w0:0.00}+{w1:0.00} on {s.BedWidth:0.00})");
                            }
                            else
                            {
                                float total = l0 + gap + l1;
                                target[layerStart] = origin + new Vector3(total * 0.5f - l0 * 0.5f, 0f, 0f) + offA;
                                target[i] = origin + new Vector3(-total * 0.5f + l1 * 0.5f, 0f, 0f) + offB;
                                note.Append($"{a} | {cur} end to end along ({l0:0.00}+{l1:0.00} on {s.BedLength:0.00})");
                            }
                        }
                        if (layer > 0)
                        {
                            // Above the bed the layer's FIRST item sits at the origin (the on-top relation, or the seat
                            // pass, places it) and the second keeps only its height relative to the first. A height both
                            // have on the bed (crates 12 cm up over the rails, logs 9 cm down) is how they sit on the flat
                            // bed, not a float or a sink to carry into every layer above (2026-10-12). BesideStored adds it
                            // back for a freeze or a write, so the stored pair never changes meaning.
                            float hb = target[layerStart].y - origin.y;
                            target[layerStart].y -= hb;
                            target[i].y -= hb;
                            r.LayerBase[layerStart] = hb;
                            r.LayerBase[i] = hb;
                        }
                        r.Relation[i] = side ? "side" : "along";
                        r.Partner[i] = layerStart;
                        r.Tuned[i] = tuned;
                        r.OnTop[i] = layer > 0;
                        r.Layer[i] = layer;
                        r.Origin[i] = origin;
                        r.Origin[layerStart] = origin;
                        continue;
                    }
                }

                // A new layer with cur as its first item, on top of the current one (layerStart .. i-1).
                int belowStart = layerStart, belowEnd = i - 1;
                bool belowPair = belowEnd > belowStart;
                float cx = 0f, cz = 0f;
                for (int k = belowStart; k <= belowEnd; k++)
                {
                    cx += target[k].x;
                    cz += target[k].z;
                }
                cx /= belowEnd - belowStart + 1;
                cz /= belowEnd - belowStart + 1;
                // The support is the layer below's FIRST item (the larger footprint, the one the sort puts first): a
                // fixed choice. Picking it by footprint overlap was a tie for two logs side by side, broken by the float
                // noise of the live measurement, so the support alternated between the two logs frame by frame and a
                // tuned relation measured against one was applied against the other: the flicker and the jump on Tune
                // seen 2026-10-10. The height: the highest point of the whole layer below, a shelf (a threshold on the
                // overlapped items flickered the same way earlier that day).
                int support = belowStart;
                float topY = float.MinValue;
                for (int k = belowStart; k <= belowEnd; k++)
                {
                    topY = Mathf.Max(topY, target[k].y + r.Box[k].y);
                }
                bool samePair = belowPair && L.SameClass(r.Id[belowStart], r.Id[belowEnd]);
                // The shelf lift: a tuned relation is measured against its support, and when the support's neighbour in
                // the layer below stands taller, the layer must rise by the difference or it is pushed into the neighbour
                // (a contact tuned on a lone support, then met on a pair; docs/cargo-tuning-tools.md). Zero on a single
                // support and on a pair of equal height, so those look exactly as tuned. Rel takes it off again, so the
                // panel shows and writes the stored relation.
                float lift = belowPair && !LiftOff ? Mathf.Max(0f, topY - (target[support].y + r.Box[support].y)) : 0f;
                r.Lift[i] = lift;
                string sup = r.Id[support];
                CargoLayout.PairEntry vp = L.ForPair(sup, cur);
                bool vTuned = vp != null && vp.TopTuned;
                if (i == 1) r.PairMode = "stack";
                layer++;
                layerStart = i;
                r.Layer[i] = layer;
                r.OnTop[i] = true;
                r.Relation[i] = "on";
                r.Partner[i] = support;
                r.Tuned[i] = vTuned;
                // The on-top relation places the LAYER (its origin relative to the support); the beside relation then
                // arranges the layer's items around that origin. The two never feed into each other (an earlier form
                // moved the first item by both, and the panel's slider ran away reading one and writing the other,
                // 2026-10-10). The layer's first item always sits exactly at the origin. Tuned: origin = support +
                // (b minus a). Untuned: over the support (or the middle of a same-class pair) at the layer's top less the
                // nestle, moved by the first item's on-top class offsets (so freezing the relation there changes nothing).
                Vector3 layerOrigin;
                if (vTuned)
                {
                    bool supIsA = L.FirstIsA(vp, sup, cur);
                    Vector3 posSup = supIsA ? vp.TopA : vp.TopB, posCur = supIsA ? vp.TopB : vp.TopA;
                    if (layer == 1 && !belowPair)
                    {
                        // The bottom item alone on the bed under a tuned on-top relation: its own tuned place, as the
                        // two-item cart was tuned.
                        target[support] = new Vector3(0f, floor, 0f) + posSup;
                        r.Tuned[support] = true;
                    }
                    layerOrigin = target[support] + (posCur - posSup) + new Vector3(0f, lift, 0f);
                    target[i] = layerOrigin;
                    if (i == 1) r.PairTuned = true;
                    note.Append($"{(layer > 1 || belowPair ? "; " : "")}{cur} on {sup} (tuned)");
                }
                else
                {
                    // Over a pair the layer starts at the layer below's ORIGIN (plus the layer shift): a pair on it is then
                    // arranged as on the bed, straight over a same pair and centred over the cart over a mixed one, and
                    // Accept writes exactly that into the relation. Over a lone support: over the support. (Before: the
                    // pair's midpoint over a same pair, the support over a mixed one; Accept wrote the half spread into the
                    // shared relation and four crates came out 2 cm off straight, 2026-10-12.)
                    float lx = belowPair ? r.Origin[belowStart].x + s.LayerShift : target[support].x, lz = belowPair ? r.Origin[belowStart].z : target[support].z;
                    layerOrigin = new Vector3(lx, topY - s.Nestle, lz) + (e != null ? e.Top : Vector3.zero);
                    target[i] = layerOrigin;
                    string how = vp != null && vp.Mode != "auto" && !belowPair ? " (" + vp.Mode + ")" : "";
                    note.Append($"{(layer > 1 || belowPair ? "; " : "")}{cur} on {(samePair ? "the pair of " + sup : sup)}{how}");
                }
                // Alone on top of a pair: over the MIDDLE of the two below (their footprint centres), at the layer's height,
                // plus the class's alone-on-a-pair correction. The layer's origin stays where the on-top relation put it, so
                // a second item joining the layer re-places this one from the origin (the beside branch) and the middle
                // and the correction apply only to a lone item; never on a two-item cart (its layer below is one item).
                // (Before, the lone item sat at the origin plus the correction, and one per-class correction had to
                // centre it over every pair it could lie on, whose middles differ: 2026-10-12.)
                if (belowPair)
                {
                    target[i] = new Vector3(cx, target[i].y, cz) + (e != null ? e.OnPair : Vector3.zero);
                }
                centre = new Vector3(layerOrigin.x, 0f, layerOrigin.z);
                layerFloor = layerOrigin.y;
                r.Origin[i] = layerOrigin;
            }
            if (n == 1)
            {
                note.Append(r.Id[0] + " alone" + (aloneOwn ? " (own alone correction)" : ""));
            }
            if (L.ShapeOf != null && n > 1)
            {
                SeatOnShapes(r, s, L);
            }
            r.Note = note.ToString();
            return r;
        }

        // The seat pass (docs/cargo-tuning-tools.md): with the measured shapes, every upper layer is placed on what is
        // really under it, bottom up, once all its items have their places across and along. Untuned: the layer's first
        // item rests with its lowest quarter at the nestle on the layers below (the class's on-top and alone-on-a-pair
        // heights kept on top), the same rule as the panel's Seat it; an untuned second item is seated on its own. Tuned:
        // as stored, raised only when a neighbour's real surface (not its box: the log's stub) stands under the item above
        // its support's (the shelf lift by the shapes). Whatever a layer moves, the layers above move with it. The lift is
        // kept in Lift, so Rel (the stored relation) and freezing stay exact: an accepted layer lands where it was.
        private static void SeatOnShapes(Result r, Settings s, Lookup L)
        {
            int n = r.Count, top = 0;
            var shapes = new CargoShapes.Shape[n];
            for (int i = 0; i < n; i++) shapes[i] = L.ShapeOf(r.Id[i]);
            for (int i = 0; i < n; i++) top = Mathf.Max(top, r.Layer[i]);
            for (int layer = 1; layer <= top; layer++)
            {
                int first = -1, second = -1, below = 0;
                for (int i = 0; i < n; i++)
                {
                    if (r.Layer[i] == layer && r.Relation[i] == "on") first = i;
                    else if (r.Layer[i] == layer && (r.Relation[i] == "side" || r.Relation[i] == "along")) second = i;
                    if (r.Layer[i] == layer - 1) below++;
                }
                if (first < 0 || !ShapesFor(shapes, r, layer)) continue;
                int support = r.Partner[first];
                CargoShapes.SeatQuantiles(r, first, shapes, support, 0.25f, out float pAll, out float pSup);
                if (float.IsNaN(pAll)) continue; // nothing measured under it: the boxes stay
                float lift = float.IsNaN(pSup) ? 0f : Mathf.Max(0f, pSup - pAll);
                float shift;
                if (!r.Tuned[first])
                {
                    // An automatic layer RESTS: its origin is placed so that its first item sits at the nestle on what is
                    // under it (the same rule as the panel's Seat it).
                    // The first item sits at the origin in an upper layer (its bed height taken off in the beside branch), so
                    // its height in the layer is only a lone item's alone-on-a-pair offset; that and the class's on-top
                    // height stay on top of the rest.
                    CargoLayout.ItemEntry e = L.ForItem(r.Id[first]);
                    float keep = (r.Target[first].y - r.Origin[first].y) + (e != null ? e.Top.y : 0f);
                    shift = -(pAll - keep + s.Nestle);
                }
                else
                {
                    shift = lift - r.Lift[first];
                }
                r.Lift[first] = lift;
                if (shift != 0f)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (r.Layer[i] < layer) continue;
                        r.Target[i].y += shift;
                        r.Origin[i].y += shift;
                    }
                }
                // A second item still untuned beside is seated on its own (nothing about it is anyone's choice yet). Once
                // its beside relation is tuned it keeps its place in the layer and moves with it: the panel freezes it
                // together with the layer's on-top relation, so a tuned layer moves as one.
                if (second >= 0 && !r.Tuned[second])
                {
                    CargoShapes.SeatQuantiles(r, second, shapes, -1, 0.25f, out float p2, out _);
                    if (!float.IsNaN(p2))
                    {
                        // its own height in the layer kept, as for the first item
                        float keep2 = r.Target[second].y - r.Origin[second].y;
                        r.Target[second].y += -(p2 - keep2 + s.Nestle);
                    }
                }
            }
        }

        // Every item of the layer and of the layers below it has a measured shape.
        private static bool ShapesFor(CargoShapes.Shape[] shapes, Result r, int layer)
        {
            for (int i = 0; i < r.Count; i++)
            {
                if (r.Layer[i] <= layer && shapes[i] == null) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ the panel's edits of a relation

        // What the on-top sliders show for the upper item i: the layer's origin relative to the support, which is the
        // stored relation when tuned and where the item sits either way, so freezing it there moves nothing.
        internal static Vector3 OnTopShown(Result r, int i, Lookup L, CargoLayout.PairEntry pair) => r.Rel(i);

        // Freezes an untuned on-top relation: the lower item at its bed place (as a two-item cart shows it), the upper
        // at rel from it. lowerIsA: whether the lower item is the pair entry's A.
        internal static void FreezeOnTop(CargoLayout.PairEntry pair, bool lowerIsA, Vector3 lowerBase, Vector3 rel)
        {
            pair.SetFor("stack", lowerIsA ? lowerBase : lowerBase + rel, lowerIsA ? lowerBase + rel : lowerBase);
        }

        // Writes the upper item's place relative to the lower into a tuned on-top relation.
        internal static void WriteOnTop(CargoLayout.PairEntry pair, bool lowerIsA, Vector3 rel)
        {
            Vector3 lower = lowerIsA ? pair.TopA : pair.TopB;
            pair.SetPos("stack", !lowerIsA, lower + rel);
        }

        // Tuning a layer (its on-top relation) also freezes its second item's place, untuned until then: the seat pass seats
        // a second item on its own only in an automatic layer, so it would jump back onto the layer otherwise. index: the
        // layer's first item.
        internal static void FreezeLayerArrangement(Result r, int index, Lookup L, Func<string, string, CargoLayout.PairEntry> editPair)
        {
            for (int k = 0; k < r.Count; k++)
            {
                if (r.Layer[k] != r.Layer[index] || r.Partner[k] != index || (r.Relation[k] != "side" && r.Relation[k] != "along")) continue;
                CargoLayout.PairEntry pair = editPair(r.Id[index], r.Id[k]);
                if (pair.BesideTuned) return;
                FreezeBeside(pair, L.FirstIsA(pair, r.Id[index], r.Id[k]), r.BesideStored(index), r.BesideStored(k), r.Relation[k]);
                return;
            }
        }

        // Freezes an untuned beside relation at both items' stored places (Result.BesideStored: the place in the layer plus
        // the layer base above the bed). firstIsA: whether the layer's first item is the pair entry's A.
        // arrangement: "side" or "along", the way the two lie now. A pair left on "auto" is pinned to it, so the fallback
        // rules (gap, overhang) can never flip a tuned pair into a stack or the other way round.
        internal static void FreezeBeside(CargoLayout.PairEntry pair, bool firstIsA, Vector3 first, Vector3 second, string arrangement = null)
        {
            pair.SetFor("side", firstIsA ? first : second, firstIsA ? second : first);
            if (pair.Mode == "auto" && (arrangement == "side" || arrangement == "along"))
            {
                pair.Mode = arrangement;
            }
        }

        // stored: the item's place in its layer plus the layer base (Result.BesideStored plus the slider's change).
        internal static void WriteBeside(CargoLayout.PairEntry pair, bool isA, Vector3 stored)
        {
            pair.SetPos("side", isA, stored);
        }

        // ------------------------------------------------------------------ helpers

        // The size class's box (measured once from the keeper's overhead element, the same numbers the layout file is
        // keyed by), else the live one.
        private static Vector3 StableBox(Lookup L, string id, Vector3 live)
        {
            Vector3 b = L.BoxOf != null ? L.BoxOf(id) : Vector3.zero;
            return b == Vector3.zero ? live : b;
        }

        internal static int Cm(float v) => Mathf.RoundToInt(v * 100f);

    }
}
