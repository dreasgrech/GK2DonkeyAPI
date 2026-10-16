"""What separates the user's 'wrong' from 'ok' on the look sheet (docs/cargo-auto-tune.md).

    python tools/probe/fit.py <probe-run folder>      # reads cargo-look-marks.json there

For every marked picture the state is rebuilt from the atlas (the load's items where the game put them, the moved item
shifted by its sweep step or its 'before' delta) and measured: every pair's one-way and mutual cover, pixels of an item
showing where another item's outline is, the moved item's share hidden, the air under upper items, the cart's cover.
Prints each feature for ok and wrong side by side, and the single thresholds that split them best.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import analyze as A  # noqa: E402


def load_by_key(run):
    return {L["key"]: L for L in run.loads}


def container_axes(L):
    """The container's x, y, z axes in the world, from its matrix in the load file."""
    for line in open(L["file"], encoding="utf-8"):
        t = line.split()
        if t and t[0] == "container_to_world":
            m = A.mat(A.floats(" ".join(t[1:17])))
            return m[:3, 0], m[:3, 1], m[:3, 2]
    return np.array([1.0, 0, 0]), np.array([0, 1.0, 0]), np.array([0, 0, 1.0])


def state_from_line(L, line):
    """The move of the line's item (container space) from a sweep or variant line: (k, local offset)."""
    t = line.split(" | ")[0].split()
    k = int(t[1])
    if t[0] == "sweep":
        off = np.zeros(3)
        off[{"along": 0, "up": 1, "across": 2}[t[2]]] = float(t[3])
    else:
        i = t.index("delta")
        off = np.array([float(t[i + 1]), float(t[i + 2]), float(t[i + 3])])
    return k, off


def features(run, L, k=None, off=None):
    """Measures of a load composed from the atlas, item k moved by off (container space)."""
    items = [dict(it) for it in L["items"]]
    if k is not None and k >= 0:
        ax, ay, az = container_axes(L)
        items[k]["view_world"] = items[k]["view_world"] + ax * off[0] + ay * off[1] + az * off[2]
    LL = dict(L)
    LL["items"] = items
    comp = A.compose(run, LL)
    n = len(items)
    inf = np.full((L["h"], L["w"]), np.inf, dtype=np.float32)
    stack = np.stack([np.where(np.isnan(c), np.inf, c) if c is not None else inf for c in comp])
    solo = ~np.isinf(stack)
    cart = cart_depth(run, L)
    f = {"pair_mutual": 0, "pair_mutual_share": 0.0, "upper_by_lower": 0, "upper_by_lower_share": 0.0, "lower_by_upper": 0,
         "same_layer_mutual": 0, "same_layer_mutual_share": 0.0, "pierce": 0, "pierce_share": 0.0}
    layers = [it["layer"] for it in items]
    for a in range(n):
        for b in range(a + 1, n):
            both = solo[a] & solo[b]
            if not both.any():
                continue
            a_front = both & (stack[a] < stack[b])
            b_front = both & (stack[b] < stack[a])
            m = min(int(a_front.sum()), int(b_front.sum()))
            share = m / max(1, min(int(solo[a].sum()), int(solo[b].sum())))
            f["pair_mutual"] = max(f["pair_mutual"], m)
            f["pair_mutual_share"] = max(f["pair_mutual_share"], share)
            if layers[a] == layers[b]:
                f["same_layer_mutual"] = max(f["same_layer_mutual"], m)
                f["same_layer_mutual_share"] = max(f["same_layer_mutual_share"], share)
            else:
                up, lo = (a, b) if layers[a] > layers[b] else (b, a)
                ul = int((both & (stack[lo] < stack[up])).sum())
                f["upper_by_lower"] = max(f["upper_by_lower"], ul)
                f["upper_by_lower_share"] = max(f["upper_by_lower_share"], ul / max(1, int(solo[up].sum())))
            # piercing: where both overlap on screen, the one behind shows through islands inside the front one's
            # outline: count pixels of the farther item enclosed by the nearer item's outline on both sides in a row
            for x, y in ((a, b), (b, a)):
                inner = both & (stack[y] < stack[x])  # y in front of x here
                if not inner.any():
                    continue
                rows = np.where(inner.any(axis=1))[0]
                cnt = 0
                for r in rows:
                    xs = np.where(solo[x][r])[0]
                    if len(xs) < 3:
                        continue
                    seg = inner[r, xs[0]:xs[-1] + 1]
                    # pixels of y in front strictly inside x's span (x on both sides)
                    cnt += int(seg[1:-1].sum())
                f["pierce"] = max(f["pierce"], cnt)
                f["pierce_share"] = max(f["pierce_share"], cnt / max(1, int(solo[x].sum())))
    # air under upper items, column by column: the bottom of the upper item's outline to the top of a lower item's
    gap_max, gap_cols_share = 0, 0.0
    for u in range(n):
        if layers[u] == 0 or not solo[u].any():
            continue
        cols = np.where(solo[u].any(axis=0))[0]
        under = 0
        gapc = 0
        for x in cols:
            ys = np.where(solo[u][:, x])[0]
            bottom = ys.min()
            lower = np.zeros(L["h"], dtype=bool)
            for j in range(n):
                if layers[j] < layers[u]:
                    lower |= solo[j][:, x]
            ly = np.where(lower[:bottom + 1])[0]
            if len(ly) == 0:
                continue
            under += 1
            g = bottom - ly.max() - 1
            if g > 0:
                gapc += 1
                gap_max = max(gap_max, int(g))
        if under:
            gap_cols_share = max(gap_cols_share, gapc / under)
    f["gap_max"] = gap_max
    f["gap_cols_share"] = gap_cols_share
    # the cart: pixels where an item is in front of the cart body (over the rails) and where the cart hides an item
    if cart is not None:
        cs = ~np.isnan(cart)
        over = hidden = 0
        for a in range(n):
            both = solo[a] & cs
            over += int((both & (stack[a] < cart)).sum())
            hidden += int((both & (cart < stack[a])).sum())
        f["item_over_cart"] = over
        f["cart_over_item"] = hidden
    # every ordered pair's one-way cover (a's outline showing b in front), for deltas against the tuned state
    f["_cover"] = {(a, b): int((solo[a] & solo[b] & (stack[b] < stack[a])).sum()) for a in range(n) for b in range(n) if a != b}
    return f


_cart_cache = {}


def cart_depth(run, L):
    """The cart body's camera distances in the load's region (from atlas/_cart.depth), or None."""
    path = os.path.join(run.folder, "atlas", "_cart.depth")
    if not os.path.exists(path):
        return None
    if path not in _cart_cache:
        # the cart was measured in the session of the first atlas entry that went on the cart (the run's atlas order
        # starts with the corpse class), so its box is inside that entry's region
        first = run.atlas.get("body_corpse") or next(iter(run.atlas.values()), None)
        _cart_cache[path] = (A.read_depth(path), first["roi"] if first else None)
    (box, front, _), roi = _cart_cache[path]
    if roi is None:
        return None
    out = np.full((L["h"], L["w"]), np.nan, dtype=np.float32)
    ox = roi[0] + box[0] - L["roi"][0]
    oy = roi[1] + box[1] - L["roi"][1]
    bw, bh = box[2], box[3]
    x0, y0, x1, y1 = max(0, ox), max(0, oy), min(L["w"], ox + bw), min(L["h"], oy + bh)
    if x1 > x0 and y1 > y0:
        out[y0:y1, x0:x1] = front[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
    return out


def main(folder):
    run = A.Run(folder)
    marks = json.load(open(os.path.join(folder, "cargo-look-marks.json"), encoding="utf-8"))
    byk = load_by_key(run)
    rows = []
    for m in marks:
        if m["mark"] not in ("ok", "wrong"):
            continue
        L = byk[m["key"]]
        if m["kind"] == "control":
            f = features(run, L)
        else:
            want = ("sweep" if m["kind"] == "sweep" else "variant")
            line = None
            for s in (L["sweeps"] if want == "sweep" else L["variants"]):
                t = s.split(" | ")[0].split()
                if want == "sweep" and int(t[1]) == m["k"] and t[2] == m["axis"] and abs(float(t[3]) - m["d"]) < 1e-6:
                    line = s
                if want == "variant" and int(t[1]) == m["k"]:
                    line = s
            k, off = state_from_line(L, line)
            f = features(run, L, k, off)
        base = features(run, L)
        for name in ("item_over_cart", "cart_over_item", "gap_max", "upper_by_lower", "same_layer_mutual"):
            if name in f:
                f["d_" + name] = f[name] - base.get(name, 0)
        grown = [f["_cover"].get(p, 0) - base["_cover"].get(p, 0) for p in f["_cover"]]
        f["d_cover_max"] = max(grown) if grown else 0
        f["d_cover_mutual"] = max((min(f["_cover"][(a, b)] - base["_cover"][(a, b)], f["_cover"][(b, a)] - base["_cover"][(b, a)]) for (a, b) in f["_cover"] if a < b), default=0)
        del f["_cover"]
        f["mark"] = m["mark"]
        f["n"] = m["n"]
        f["what"] = f"{m['key']} {m['kind']} {m['axis']} {m['d']:+.2f}"
        rows.append(f)
    names = [k for k in rows[0] if k not in ("mark", "n", "what")]
    ok = [r for r in rows if r["mark"] == "ok"]
    bad = [r for r in rows if r["mark"] == "wrong"]
    print(f"{len(ok)} ok, {len(bad)} wrong")
    for name in names:
        o = np.array([r[name] for r in ok], dtype=float)
        b = np.array([r[name] for r in bad], dtype=float)
        # the best single threshold: wrong when value > t
        best = (0, None)
        for t in sorted(set(np.concatenate([o, b]))):
            acc = ((o <= t).sum() + (b > t).sum()) / (len(o) + len(b))
            if acc > best[0]:
                best = (acc, t)
        print(f"{name:24s} ok: median {np.median(o):9.3f} max {o.max():9.3f} | wrong: min {b.min():9.3f} median {np.median(b):9.3f} | split > {best[1]}: {best[0]:.3f}")
    print()
    for r in sorted(rows, key=lambda r: (r["mark"], -r.get("d_item_over_cart", 0))):
        print(f"{r['mark']:5s} #{r['n']:2d} d_over_cart {r.get('d_item_over_cart', 0):6d} d_cart_over {r.get('d_cart_over_item', 0):6d} d_cover_max {r['d_cover_max']:6d} d_cover_mutual {r['d_cover_mutual']:6d} d_gap {r['d_gap_max']:4d}  {r['what']}")


if __name__ == "__main__":
    main(sys.argv[1])
