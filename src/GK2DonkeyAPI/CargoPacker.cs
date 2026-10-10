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
    // relation puts the origin at the support plus (b minus a). The on-top relation places the LAYER, the beside
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
            public string Note, PairMode;
            public bool PairTuned;
            public float Floor;
            public int Count => Target.Length;

            // What the on-top relation stores: the layer's origin relative to the support. Beside: the item relative to
            // the layer's first item. The bed: the place itself (above the floor origin).
            public Vector3 Rel(int i) => Relation[i] == "on" ? Origin[i] - Target[Partner[i]] : Partner[i] >= 0 ? Target[i] - Target[Partner[i]] : Target[i] - new Vector3(0f, Floor, 0f);

            // The item's place in its layer's frame.
            public Vector3 LayerPos(int i) => Target[i] - Origin[i];
        }

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
                OnTop = new bool[n], Tuned = new bool[n], Layer = new int[n], Partner = new int[n], Relation = new string[n],
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
                    layerOrigin = target[support] + (posCur - posSup);
                    target[i] = layerOrigin;
                    if (i == 1) r.PairTuned = true;
                    note.Append($"{(layer > 1 || belowPair ? "; " : "")}{cur} on {sup} (tuned)");
                }
                else
                {
                    float lx = samePair ? cx : target[support].x, lz = samePair ? cz : target[support].z;
                    if (belowPair) lx += s.LayerShift;
                    layerOrigin = new Vector3(lx, topY - s.Nestle, lz) + (e != null ? e.Top : Vector3.zero);
                    target[i] = layerOrigin;
                    string how = vp != null && vp.Mode != "auto" && !belowPair ? " (" + vp.Mode + ")" : "";
                    note.Append($"{(layer > 1 || belowPair ? "; " : "")}{cur} on {(samePair ? "the pair of " + sup : sup)}{how}");
                }
                centre = new Vector3(layerOrigin.x, 0f, layerOrigin.z);
                layerFloor = layerOrigin.y;
                r.Origin[i] = layerOrigin;
            }
            if (n == 1)
            {
                note.Append(r.Id[0] + " alone" + (aloneOwn ? " (own alone correction)" : ""));
            }
            r.Note = note.ToString();
            return r;
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

        // Freezes an untuned beside relation at both items' places in the layer's frame. firstIsA: whether the layer's
        // first item is the pair entry's A.
        internal static void FreezeBeside(CargoLayout.PairEntry pair, bool firstIsA, Vector3 first, Vector3 second)
        {
            pair.SetFor("side", firstIsA ? first : second, firstIsA ? second : first);
        }

        internal static void WriteBeside(CargoLayout.PairEntry pair, bool isA, Vector3 layerPos)
        {
            pair.SetPos("side", isA, layerPos);
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
