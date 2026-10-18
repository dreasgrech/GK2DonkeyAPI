using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Hellfire: the donkey's own frames with horns where the ears were, a devil's tail where the tail was, eyes that
    // glow, and lava cracks across an ash coat (the user's model of what a skin is, 2026-10-18: features no colour
    // setting can give; docs/mount-colours.md). A recipe (LooksRecipes.cs): every piece is placed from the frame's own
    // pixels (the eye, the rump, the ear, the torso's box), so it covers every clip; the cracks are fixed lines in the
    // torso's own coordinates, so they ride with the body instead of flickering. Prototyped frame by frame in Python
    // over exported frames (the job's hellfire_proto.py, 2026-10-18); this is that code, in top-down coordinates over
    // the canvas (Unity's rows run from the bottom: Grid turns them over).
    internal static partial class Looks
    {
        private static readonly Color32 HfOutline = new Color32(30, 14, 12, 255);
        private static readonly Color32[] HfBone = { new Color32(236, 220, 180, 255), new Color32(200, 176, 130, 255), new Color32(120, 90, 60, 255) };
        private static readonly Color32[] HfLava = { new Color32(255, 150, 40, 255), new Color32(255, 90, 24, 255), new Color32(150, 34, 12, 255) };
        private static readonly Color32 HfEye = new Color32(255, 170, 40, 255), HfEyeRim = new Color32(220, 60, 20, 255);
        private static readonly Color32 HfTail = new Color32(44, 18, 16, 255), HfTailUnder = new Color32(70, 26, 20, 255), HfSpade = new Color32(200, 40, 30, 255), HfSpadeDark = new Color32(120, 20, 16, 255);
        // the cracks, in torso coordinates (u across from the shoulder, v down from the back line); the third branches off the second
        private static readonly float[][] HfCracks =
        {
            new[] { 0.12f, 0.30f, 0.20f, 0.48f, 0.16f, 0.66f, 0.26f, 0.84f },
            new[] { 0.46f, 0.22f, 0.52f, 0.40f, 0.62f, 0.50f, 0.58f, 0.72f },
            new[] { 0.52f, 0.40f, 0.42f, 0.56f },
            new[] { 0.80f, 0.34f, 0.86f, 0.52f, 0.78f, 0.70f, 0.84f, 0.86f },
        };

        // The colours Hellfire adds, by region, for the colour engine (a skin's other colours are its Coat).
        private static Dictionary<string, HashSet<string>> HellfireRegions()
        {
            string Hex(Color32 c) => $"{c.r:X2}{c.g:X2}{c.b:X2}";
            return new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["Muzzle"] = new HashSet<string>(StringComparer.Ordinal) { Hex(HfLava[0]), Hex(HfLava[1]), Hex(HfLava[2]) },
                ["Dark"] = new HashSet<string>(StringComparer.Ordinal) { Hex(HfOutline), Hex(HfBone[0]), Hex(HfBone[1]), Hex(HfBone[2]), Hex(HfTail), Hex(HfTailUnder) },
                ["Eye"] = new HashSet<string>(StringComparer.Ordinal) { Hex(HfEye), Hex(HfEyeRim), Hex(HfSpade), Hex(HfSpadeDark) },
            };
        }

        // A canvas seen top-down, with the donkey's coat and outline colour sets.
        private sealed class Grid
        {
            internal readonly int W, H;
            internal readonly Color32[] P; // Unity's order (bottom row first); the accessors take top-down y
            private readonly HashSet<int> coat, dark;

            internal Grid(Looks.SkinFrame f, HashSet<int> coat, HashSet<int> dark)
            {
                W = f.W;
                H = f.H;
                P = (Color32[])f.P.Clone();
                this.coat = coat;
                this.dark = dark;
            }

            private int At(int x, int y) => (H - 1 - y) * W + x;
            internal bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
            internal Color32 Get(int x, int y) => In(x, y) ? P[At(x, y)] : default;
            internal bool Opaque(int x, int y) => In(x, y) && P[At(x, y)].a != 0;
            internal void Set(int x, int y, Color32 c) { if (In(x, y)) P[At(x, y)] = c; }
            internal void Clear(int x, int y) { if (In(x, y)) P[At(x, y)] = default; }
            internal static int Key(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;
            internal bool IsCoat(int x, int y) => Opaque(x, y) && coat.Contains(Key(Get(x, y)));
            internal bool IsDark(int x, int y) => Opaque(x, y) && dark.Contains(Key(Get(x, y)));
            internal bool IsLava(int x, int y) { Color32 c = Get(x, y); return Opaque(x, y) && (Same(c, HfLava[0]) || Same(c, HfLava[1]) || Same(c, HfLava[2])); }
            internal static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

            // the opaque runs of a row, (x0, x1)
            internal List<(int A, int B)> Runs(int y)
            {
                var o = new List<(int, int)>();
                int start = -1;
                for (int x = 0; x <= W; x++)
                {
                    bool on = x < W && Opaque(x, y);
                    if (on && start < 0) start = x;
                    else if (!on && start >= 0) { o.Add((start, x - 1)); start = -1; }
                }
                return o;
            }

            internal bool Bounds(out int x0, out int y0, out int x1, out int y1)
            {
                x0 = W; y0 = H; x1 = -1; y1 = -1;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (Opaque(x, y)) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
                return x1 >= 0;
            }
        }

        private static Color32[] Hellfire(SkinFrame f)
        {
            var g = new Grid(f, Colors.RegionSourceColours("Coat"), Colors.RegionSourceColours("Dark"));
            bool headOnly = f.Key.StartsWith("hed_", StringComparison.Ordinal);
            // the eye: the white pixels
            var eye = new List<(int X, int Y)>();
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    Color32 c = g.Get(x, y);
                    if (c.a != 0 && c.r == 255 && c.g == 255 && c.b == 255) eye.Add((x, y));
                }
            if (HfEarFrames.TryGetValue(f.Key, out var byHand) && byHand.Eyes != null)
            {
                eye.Clear();
                eye.AddRange(byHand.Eyes);
            }
            float ex = 0f, ey = 0f;
            foreach (var p in eye) { ex += p.X; ey += p.Y; }
            if (eye.Count > 0) { ex /= eye.Count; ey /= eye.Count; }
            (int X, int Y)? root = null;
            if (!headOnly)
            {
                int left;
                if (eye.Count > 0) left = Mathf.RoundToInt(ex) + 10;
                else { g.Bounds(out int bx0, out _, out int bx1, out _); left = bx0 + (int)(0.4f * (bx1 - bx0)); }
                if (HfTorsoBox(g, left, out int tx0, out int ty0, out int tx1, out int ty1))
                {
                    int tailFrom = HfEraseTail(g);
                    HfTorsoBox(g, left, out tx0, out ty0, out tx1, out ty1); // without the tail
                    HfCracks_(g, tx0, ty0, tx1, ty1);
                    if (tailFrom >= 0)
                    {
                        // the rump's back rounded out where the tail was; the devil's tail grows from its upper third
                        int[] curve = HfFillRump(g, tailFrom, ty1);
                        int ry = tailFrom + (ty1 - tailFrom) / 3;
                        if (ry >= 0 && ry < g.H && curve[ry] >= 0)
                        {
                            var rr = g.Runs(ry); // the row's actual edge: the body may be wider than the curve there
                            root = (rr.Count > 0 ? rr[rr.Count - 1].B : curve[ry], ry);
                        }
                    }
                }
            }
            // the ears out, the horns where they were: a few frames by hand (HfEarFrames), the rest by rule, the horns
            // from the skull's top (it moves only with the head; the eye pixel animates on its own and is gone in the
            // blinks: the horns jumped or vanished in the idles, 2026-10-19), placed as they were from the eye in the
            // standing frame: skull top right end (31, 25) -> horns at (29, 26) and (34, 27)
            (int X, int Y, int Lean)[] horns;
            if (HfEarFrames.TryGetValue(f.Key, out var special))
            {
                foreach (var box in special.Erase) for (int y = Math.Max(0, box.Y0); y <= Math.Min(g.H - 1, box.Y1); y++) for (int x = Math.Max(0, box.X0); x <= Math.Min(g.W - 1, box.X1); x++) g.Clear(x, y);
                HfOutlineBaredTop(g);
                horns = special.Horns;
            }
            else
            {
                HfEraseEars(g, eye.Count > 0 ? (int?)Mathf.RoundToInt(ey) : null);
                HfSkullTop(g, out int sx, out int sy);
                horns = new[] { (sx - 2, sy + 1, 1), (sx + 3, sy + 2, 1) };
            }
            HfDarken(g);
            if (eye.Count > 0)
            {
                foreach (var p in eye) g.Set(p.X, p.Y, HfEye);
                foreach (var p in eye)
                {
                    foreach (var (nx, ny) in new[] { (p.X - 1, p.Y), (p.X + 1, p.Y), (p.X, p.Y - 1), (p.X, p.Y + 1) })
                        if (g.Opaque(nx, ny) && !Grid.Same(g.Get(nx, ny), HfEye)) g.Set(nx, ny, HfEyeRim);
                }
            }
            foreach (var hn in horns)
            {
                // on the head, not above it: the base drops until the head is right under it (the second horn floated in
                // walk 4 and 8 and lips 6, where the skull slopes away, 2026-10-19)
                int hy = hn.Y;
                for (int k = 0; k < 4 && !g.Opaque(hn.X, hy + 1); k++) hy++;
                HfHorn(g, hn.X, hy, hn.Lean);
            }
            if (root.HasValue)
            {
                HfDevilTail(g, root.Value.X, root.Value.Y, HfPhase(f.Key));
                HfSmoothRoot(g, root.Value.X, root.Value.Y);
            }
            return g.P;
        }

        // the tail's sway: the walk's eight frames go -1..0.75; everything else hangs still
        private static float HfPhase(string key)
        {
            int at = key.IndexOf("walk_left_", StringComparison.Ordinal);
            if (at < 0 || !int.TryParse(key.Substring(at + 10), out int n)) return 0f;
            return (n - 1) / 4f - 1f;
        }

        // The donkey's own tail, found as it is drawn (compared frame by frame with the game's 41 body frames,
        // 2026-10-19): it lies against the rump as an outline, a coat-coloured strip and an outline, and hangs below as
        // a dark tuft. The rump's own edge is the inner dark line where it branches off: the first row (top down) whose
        // rear end reads dark, coat, dark within five pixels. From there the edge is followed down, moving only along
        // drawn pixels (never across a gap onto the tuft), one or two pixels thick; a one-row notch is filled. Then
        // everything right of the edge that is connected to the tail where it starts is cleared: a leg's edge pixel
        // right of the line belongs to another region and stays. Earlier rules (a run past a gap, the outer outline
        // tracked) kept the strip against the rump or ate a hind leg. Returns whether a tail was found.
        private static int HfEraseTail(Grid g)
        {
            if (!g.Bounds(out int bx0, out _, out int bx1, out _)) return -1;
            float rear = bx0 + 0.7f * (bx1 - bx0);
            int y0 = -1, prev = -1;
            for (int y = 0; y < g.H && y0 < 0; y++)
            {
                var r = g.Runs(y);
                if (r.Count == 0) continue;
                int end = r[r.Count - 1].B;
                if (end < rear || !g.IsDark(end, y)) continue;
                int x = end;
                while (x - 1 >= 0 && g.IsDark(x - 1, y)) x--;
                if (x - 1 < 0 || !g.Opaque(x - 1, y)) continue;
                x--;
                while (x - 1 >= 0 && g.Opaque(x - 1, y) && !g.IsDark(x - 1, y)) x--;
                int inner = x - 1;
                if (inner >= 0 && g.IsDark(inner, y) && end - inner <= 5) { y0 = y; prev = inner; }
            }
            if (y0 < 0) return -1;
            var line = new int[g.H];
            for (int y = y0; y < g.H; y++)
            {
                int L = -1;
                for (int x = Math.Max(0, prev - 1); x <= Math.Min(g.W - 1, prev + 1) && L < 0; x++)
                {
                    if (!g.IsDark(x, y)) continue;
                    int lo = Math.Min(x, prev - 1), hi = Math.Max(x, prev - 1);
                    bool joined = true;
                    for (int k = lo; k <= hi; k++) if (!g.Opaque(k, y)) { joined = false; break; }
                    if (joined) L = x;
                }
                if (L >= 0) prev = g.IsDark(L + 1, y) ? L + 1 : L;
                line[y] = prev;
            }
            for (int y = y0 + 1; y < g.H - 1; y++) line[y] = Math.Max(line[y], Math.Min(line[y - 1], line[y + 1]));
            // an edge in a run a gap away from a body run right beside it is tuft, not rump: the edge is the body's end
            // (a lone pixel stayed at the rump's foot in every idle frame, 2026-10-19)
            for (int y = y0; y < g.H; y++)
            {
                var r = g.Runs(y);
                for (int i = 1; i < r.Count; i++)
                {
                    if (r[i].A <= line[y] && line[y] <= r[i].B && r[i].A - r[i - 1].B >= 2 && line[y] - r[i - 1].B <= 4) { line[y] = r[i - 1].B; break; }
                }
            }
            bool Right(int x, int y) => y >= y0 && y < g.H && x > line[y] && x < g.W && g.Opaque(x, y);
            var seen = new HashSet<int>();
            var stack = new Stack<(int X, int Y)>();
            for (int y = y0; y <= Math.Min(g.H - 1, y0 + 1); y++)
                for (int x = line[y] + 1; x < g.W; x++)
                    if (Right(x, y)) stack.Push((x, y));
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                if (!Right(x, y) || !seen.Add(y * g.W + x)) continue;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (dx != 0 || dy != 0) stack.Push((x + dx, y + dy));
            }
            // and every other piece right of the edge unless it reaches the ground (a leg): the rest is tuft left over
            int bottom = 0;
            for (int y = 0; y < g.H; y++) for (int x = 0; x < g.W; x++) if (g.Opaque(x, y)) bottom = y;
            var visited = new HashSet<int>(seen);
            for (int y = y0; y < g.H; y++)
            {
                for (int x = line[y] + 1; x < g.W; x++)
                {
                    if (!Right(x, y) || visited.Contains(y * g.W + x)) continue;
                    var comp = new List<int>();
                    int lowest = -1;
                    stack.Push((x, y));
                    while (stack.Count > 0)
                    {
                        var (cx, cy) = stack.Pop();
                        if (!Right(cx, cy) || !visited.Add(cy * g.W + cx)) continue;
                        comp.Add(cy * g.W + cx);
                        lowest = Math.Max(lowest, cy);
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if (dx != 0 || dy != 0) stack.Push((cx + dx, cy + dy));
                    }
                    if (lowest < bottom - 2) foreach (int k in comp) seen.Add(k);
                }
            }
            foreach (int k in seen) g.Clear(k % g.W, k / g.W);
            return y0;
        }

        // The rump's back where the tail was, as one curve (2026-10-19, after a notch, a straight stripe and a right
        // angle at the thigh were each seen in game): from the edge just above where the tail branched off (y0 - 1) down
        // to the back of the thigh (the first row where the body splits into legs), fullest about a third of the way
        // down and turning in gradually, so it rolls into the leg. Every row between is set to the curve: filled out in
        // the row's coat colour or trimmed, the outline at the end. Returns the curve's column per row (-1: none).
        private const float HfRumpShape = 0.65f, HfRumpBulgePerRow = 1f / 7f;

        private static int[] HfFillRump(Grid g, int y0, int yb)
        {
            var curve = new int[g.H];
            for (int i = 0; i < curve.Length; i++) curve[i] = -1;
            int Edge(int y) { var r = g.Runs(y); return r.Count > 0 ? r[r.Count - 1].B : -1; }
            if (y0 < 1) return curve;
            int top = Edge(y0 - 1);
            int yl = -1, bot = -1;
            for (int y = y0 + 3; y < g.H && yl < 0 && top >= 0; y++)
            {
                var r = g.Runs(y);
                // the hind leg's run starts within 14 columns of the rump's edge (a lowered head is a run too, far
                // forward: grazing frame 2 2026-10-19)
                if (r.Count >= 2 && r[r.Count - 1].A - r[r.Count - 2].B >= 2 && r[r.Count - 1].A >= top - 14) { yl = y; bot = r[r.Count - 1].B; }
            }
            if (top < 0 || yl < 0) return curve;
            Color32 outline = g.Get(top, y0 - 1);
            int n = yl - (y0 - 1);
            float bulge = Mathf.Max(1f, n * HfRumpBulgePerRow);
            var want = new int[yl + 1];
            for (int y = y0; y < yl; y++)
            {
                float tt = (float)(y - (y0 - 1)) / n;
                want[y] = Mathf.RoundToInt(top + (bot - top) * tt + bulge * Mathf.Sin(Mathf.PI * Mathf.Pow(tt, HfRumpShape)));
            }
            want[y0 - 1] = top;
            want[yl] = bot;
            for (int y = y0; y < yl; y++) want[y] = Math.Min(want[y], Math.Max(want[y - 1], want[y + 1]));
            for (int y = y0; y < yl; y++)
            {
                int target = want[y];
                int e = Edge(y);
                if (e < 0) continue;
                int last = -1;
                for (int x = Math.Min(e, target); x >= 0 && last < 0; x--) if (g.IsCoat(x, y) || g.IsLava(x, y)) last = x;
                if (last < 0) continue;
                Color32 coat = g.Get(last, y);
                for (int x = last; x > Math.Max(0, last - 10); x--) if (g.IsCoat(x, y)) { coat = g.Get(x, y); break; }
                for (int x = last + 1; x < target; x++) g.Set(x, y, coat);
                for (int x = target + 1; x < g.W; x++) g.Clear(x, y);
                g.Set(target, y, outline);
                curve[y] = target;
            }
            return curve;
        }

        // After the tail is drawn: around its root, any body edge pixel sticking out past both neighbouring rows' edges
        // (the tail's own root pixel counts as a row's edge) is moved in one, the outline to the column before it
        // (a lone pixel on the rump's edge next to the tail, seen in game 2026-10-19).
        private static void HfSmoothRoot(Grid g, int rx, int ry)
        {
            bool IsTail(int x, int y) { Color32 c = g.Get(x, y); return Grid.Same(c, HfTail) || Grid.Same(c, HfTailUnder) || Grid.Same(c, HfSpade) || Grid.Same(c, HfSpadeDark); }
            for (int pass = 0; pass < 4; pass++)
            {
                var e = new Dictionary<int, int>();
                for (int y = Math.Max(0, ry - 8); y < Math.Min(g.H, ry + 9); y++)
                {
                    int x = -1;
                    for (int k = Math.Max(0, rx - 6); k < Math.Min(g.W, rx + 3); k++) if (g.Opaque(k, y)) x = k;
                    if (x < 0) continue;
                    while (x - 1 >= 0 && IsTail(x, y) && IsTail(x - 1, y)) x--;
                    e[y] = x;
                }
                bool moved = false;
                foreach (int y in new List<int>(e.Keys))
                {
                    if (e.TryGetValue(y - 1, out int up) && e.TryGetValue(y + 1, out int down) && e[y] > up && e[y] > down && !IsTail(e[y], y))
                    {
                        g.Set(e[y] - 1, y, g.Get(e[y], y));
                        g.Clear(e[y], y);
                        moved = true;
                    }
                }
                if (!moved) break;
            }
        }

        // Frames whose ears a rule can't read (2026-10-19): the head down grazing (one ear up, the far one lying back on
        // the neck as mane) and the head turned to the camera (two ears either side of the forelock). Per frame key: the
        // boxes that are ear (top-down canvas, inclusive) and the horns (base x, base y, lean: -1 to the left).
        private sealed class HfEarFrame
        {
            internal (int X0, int X1, int Y0, int Y1)[] Erase;
            internal (int X, int Y, int Lean)[] Horns;
            internal (int X, int Y)[] Eyes; // the face drawn from the front: its eyes are light pixels, not the white one
        }

        private static readonly Dictionary<string, HfEarFrame> HfEarFrames = new Dictionary<string, HfEarFrame>(StringComparer.Ordinal)
        {
            ["bdy_graze_left_03"] = new HfEarFrame { Erase = new[] { (19, 26, 0, 22) }, Horns = new[] { (22, 23, 1), (26, 24, 1) } },
            ["bdy_graze_left_04"] = new HfEarFrame { Erase = new[] { (18, 25, 0, 22) }, Horns = new[] { (21, 23, 1), (25, 24, 1) } },
            ["bdy_graze_left_05"] = new HfEarFrame { Erase = new[] { (18, 25, 0, 22) }, Horns = new[] { (21, 23, 1), (25, 24, 1) } },
            ["bdy_turn_left_04"] = new HfEarFrame { Erase = new[] { (15, 35, 0, 22) }, Horns = new[] { (20, 23, -1), (30, 23, 1) }, Eyes = new[] { (20, 30), (27, 30) } },
            ["bdy_turn_left_05"] = new HfEarFrame { Erase = new[] { (15, 35, 0, 22) }, Horns = new[] { (20, 23, -1), (30, 23, 1) }, Eyes = new[] { (21, 31), (27, 31) } },
            ["bdy_turn_left_06"] = new HfEarFrame { Erase = new[] { (15, 35, 0, 22) }, Horns = new[] { (20, 23, -1), (30, 23, 1) }, Eyes = new[] { (21, 31), (27, 31) } },
        };

        private static void HfOutlineBaredTop(Grid g)
        {
            for (int y = 1; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                    if (g.IsCoat(x, y) && !g.Opaque(x, y - 1)) g.Set(x, y, HfOutline);
        }

        // After the ears are gone: the head's top row (topmost pixels within 16 columns of the muzzle tip) and its right end.
        private static void HfSkullTop(Grid g, out int sx, out int sy)
        {
            sx = 0; sy = 0;
            if (!g.Bounds(out int mx, out _, out _, out _)) return;
            sy = g.H;
            for (int y = 0; y < g.H && sy == g.H; y++)
                for (int x = mx; x <= Math.Min(g.W - 1, mx + 16); x++)
                    if (g.Opaque(x, y)) { sy = y; break; }
            for (int x = mx; x <= Math.Min(g.W - 1, mx + 16); x++) if (g.Opaque(x, sy)) sx = x;
        }

        // The ears: from the top row down, the run under the topmost pixel while it is at most 7 wide and above the
        // eyes (the skull is wider); the skull's bared top edge becomes outline.
        private static void HfEraseEars(Grid g, int? eyeY)
        {
            int top = -1, col = -1;
            for (int y = 0; y < g.H && top < 0; y++)
                for (int x = 0; x < g.W; x++)
                    if (g.Opaque(x, y)) { top = y; col = x; break; }
            if (top < 0) return;
            int limit = eyeY.HasValue ? eyeY.Value - 4 : g.H;
            for (int y = top; y < Math.Min(g.H, limit); y++)
            {
                var runs = g.Runs(y);
                (int A, int B)? run = null;
                foreach (var r in runs) if (r.A <= col && col <= r.B) { run = r; break; }
                if (run == null)
                {
                    int best = int.MaxValue;
                    foreach (var r in runs)
                    {
                        int d = Math.Min(Math.Abs(r.A - col), Math.Abs(r.B - col));
                        if (d < best) { best = d; run = r; }
                    }
                    if (run == null || best > 2) break;
                }
                if (run.Value.B - run.Value.A + 1 > 7) break;
                for (int x = run.Value.A; x <= run.Value.B; x++) g.Clear(x, y);
                col = (run.Value.A + run.Value.B) / 2;
            }
            for (int y = 1; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                    if (g.IsCoat(x, y) && !g.Opaque(x, y - 1)) g.Set(x, y, HfOutline);
        }

        // The torso's box: coat (and outline) pixels right of the head, rows whose run is at least 60% of the widest.
        private static bool HfTorsoBox(Grid g, int left, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = g.W; y0 = g.H; x1 = -1; y1 = -1;
            var rows = new Dictionary<int, (int A, int B)>();
            for (int y = 0; y < g.H; y++)
            {
                int a = -1, b = -1;
                for (int x = Math.Max(0, left); x < g.W; x++)
                    if (g.IsCoat(x, y) || g.IsDark(x, y) || g.IsLava(x, y)) { if (a < 0) a = x; b = x; }
                if (a >= 0) rows[y] = (a, b);
            }
            if (rows.Count == 0) return false;
            int widest = 0;
            foreach (var r in rows.Values) widest = Math.Max(widest, r.B - r.A);
            foreach (var kv in rows)
            {
                if (kv.Value.B - kv.Value.A < 0.6f * widest) continue;
                if (kv.Key < y0) y0 = kv.Key;
                if (kv.Key > y1) y1 = kv.Key;
                if (kv.Value.A < x0) x0 = kv.Value.A;
                if (kv.Value.B > x1) x1 = kv.Value.B;
            }
            return x1 >= 0;
        }

        private static void HfCracks_(Grid g, int x0, int y0, int x1, int y1)
        {
            foreach (float[] poly in HfCracks)
            {
                var path = new List<(int X, int Y)>();
                for (int i = 0; i + 3 < poly.Length; i += 2)
                {
                    float ax = x0 + poly[i] * (x1 - x0), ay = y0 + poly[i + 1] * (y1 - y0);
                    float bx = x0 + poly[i + 2] * (x1 - x0), by = y0 + poly[i + 3] * (y1 - y0);
                    int n = Math.Max(1, Mathf.RoundToInt(Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay))));
                    for (int k = 0; k <= n; k++) path.Add((Mathf.RoundToInt(ax + (bx - ax) * k / n), Mathf.RoundToInt(ay + (by - ay) * k / n)));
                }
                for (int i = 0; i < path.Count; i++)
                {
                    var (x, y) = path[i];
                    if (!g.IsCoat(x, y)) continue;
                    g.Set(x, y, i % 4 == 0 ? HfLava[0] : HfLava[1]);
                    if (g.IsCoat(x, y + 1)) g.Set(x, y + 1, HfLava[2]);
                }
            }
        }

        // the coat to dark ash with a red cast (the cracks, horns and eyes are their own colours and stay)
        private static void HfDarken(Grid g)
        {
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    if (!g.IsCoat(x, y)) continue;
                    Color32 c = g.Get(x, y);
                    Color.RGBToHSV(c, out _, out float s, out float v);
                    Color d = Color.HSVToRGB(0.02f, Mathf.Min(1f, s * 0.9f + 0.25f), v * 0.42f);
                    g.Set(x, y, new Color32((byte)(d.r * 255f), (byte)(d.g * 255f), (byte)(d.b * 255f), c.a));
                }
        }

        // a curved horn, 2 px thick, up and back from its base, bone with a dark edge and a light tip
        private static void HfHorn(Grid g, int bx, int by, int lean)
        {
            var pts = new[] { (0, 0), (0, -1), (0, -2), (1, -3), (1, -4), (2, -5), (2, -6), (3, -7) };
            for (int i = 0; i < pts.Length; i++)
            {
                int x = bx + pts[i].Item1 * lean, y = by + pts[i].Item2;
                g.Set(x, y, i < 2 ? HfBone[2] : i >= 6 ? HfBone[0] : HfBone[1]);
                if (i < 6)
                {
                    g.Set(x - lean, y, i > 3 ? HfBone[2] : HfBone[1]);
                    if (i < 3) g.Set(x - 2 * lean, y, HfOutline);
                }
                g.Set(x + lean, y, HfOutline);
            }
            g.Set(bx + 3 * lean, by - 8, HfOutline);
        }

        // from the rump out and up in a curve, with a spade at the tip; phase sways it. Checked over all 41 frames
        // (2026-10-19): it never dips under its own root row; under the root the rump narrows (no row's edge past the
        // root or the row above: a knob showed under the tail); the underside shade is one pixel under each column's
        // lowest tail pixel, only where the tail has risen above its root (at the root it jutted out under the rump).
        private static void HfDevilTail(Grid g, int rx, int ry, float phase)
        {
            var pts = new List<(int X, int Y)>();
            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                var p = (rx + Mathf.RoundToInt(12 * t), Math.Min(ry, ry + Mathf.RoundToInt(-14 * t * t + 2.5f * phase * t)));
                if (pts.Count == 0 || pts[pts.Count - 1] != p) pts.Add(p);
            }
            int limit = rx;
            for (int y = ry + 1; y < Math.Min(g.H, ry + 8); y++)
            {
                var r = g.Runs(y);
                if (r.Count == 0) break;
                int e = r[r.Count - 1].B;
                if (e > limit)
                {
                    Color32 outline = g.Get(e, y);
                    for (int x = limit + 1; x <= e; x++) g.Clear(x, y);
                    g.Set(limit, y, outline);
                    e = limit;
                }
                limit = Math.Min(limit, e);
            }
            foreach (var p in pts) g.Set(p.X, p.Y, HfTail);
            var low = new Dictionary<int, int>();
            int maxX = rx;
            foreach (var p in pts)
            {
                low[p.X] = low.TryGetValue(p.X, out int l) ? Math.Max(l, p.Y) : p.Y;
                maxX = Math.Max(maxX, p.X);
            }
            int span = maxX - rx;
            foreach (var kv in low)
            {
                int x = kv.Key, y = kv.Value;
                if (x >= rx + 2 && x <= rx + Math.Max(3, span * 4 / 10) && y < ry && y + 1 < g.H && !g.Opaque(x, y + 1)) g.Set(x, y + 1, HfTailUnder);
            }
            var (tx, ty) = pts[pts.Count - 1];
            g.Set(tx, ty - 1, HfSpade); g.Set(tx - 1, ty - 1, HfSpade); g.Set(tx + 1, ty - 1, HfSpade); g.Set(tx, ty - 2, HfSpade);
            g.Set(tx, ty - 3, HfSpadeDark); g.Set(tx - 1, ty - 2, HfSpadeDark); g.Set(tx + 1, ty - 2, HfSpadeDark);
        }
    }
}
