"""What the user's series marks have in common (docs/cargo-auto-tune.md, series).

    python tools/probe/series_fit.py <probe-run folder>      # reads cargo-series-marks.json there

Every row is one item of a two-item load moved -8, -4, 0, +4, +8 cm along one axis. For each state this prints the
3D facts of the moved item's box against its partner's box and the cart bed (container space, from the load file):
overlap per axis, the gap between them, how far past the bed's sides/ends, height above the floor or the item below,
plus the in-game pixel measures of the sweep line. Then the single rules that split ok from wrong best.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import analyze as A  # noqa: E402

AX = {"along": 0, "up": 1, "across": 2}


def items_3d(L):
    """(pos, half size) per item in container space from the load file's item lines."""
    out = []
    for line in open(L["file"], encoding="utf-8"):
        t = line.split()
        if t and t[0] == "item":
            p = np.array([float(v) for v in t[t.index("pos") + 1:t.index("pos") + 4]])
            b = np.array([float(v) for v in t[t.index("box") + 1:t.index("box") + 4]])
            out.append((p, b / 2))
    return out


def bed(L):
    for line in open(L["file"], encoding="utf-8"):
        t = line.split()
        if t and t[0] == "bed":
            return float(t[2]), float(t[4]), float(t[6])
    return None


def sweep_line(L, k, axis, d):
    for s in L["sweeps"]:
        t = s.split(" | ")[0].split()
        if int(t[1]) == k and t[2] == axis and abs(float(t[3]) - d) < 1e-6:
            return s
    return None


def facts(L, k, axis, d):
    its = items_3d(L)
    (pk, hk), (pp, hp) = its[k], its[1 - k]
    pk = pk.copy()
    pk[AX[axis]] += d
    lo_k, hi_k, lo_p, hi_p = pk - hk, pk + hk, pp - hp, pp + hp
    ov = np.minimum(hi_k, hi_p) - np.maximum(lo_k, lo_p)  # >0 overlaps on that axis
    f = {}
    f["pen"] = float(min(ov)) if (ov > 0).all() else 0.0          # 3D penetration depth (smallest overlap)
    f["gap"] = float(-max(ov)) if (ov < 0).any() and max(ov) < 0 else float(max(0.0, -min(ov)))
    w, ln, floor = bed(L)
    f["past_side"] = float(max(0.0, hi_k[2] - w / 2, -w / 2 - lo_k[2]))
    f["past_end"] = float(max(0.0, hi_k[0] - ln / 2, -ln / 2 - lo_k[0]))
    f["d"] = d
    # in-game measures from the sweep line (moved item's chunk)
    line = sweep_line(L, k, axis, d) if d else None
    if line:
        parts = line.split(" | ")
        mine = parts[1 + k].split()
        oth = parts[2 - k].split()
        f["by_cart"] = int(mine[mine.index("by_cart") + 1])
        f["gap_max"] = int(mine[mine.index("gap_max") + 1])
        mu = parts[1].split()
        i = mu.index("mutual")
        f["mutual"] = min(int(mu[i + 1]), int(mu[i + 2]))
        f["mutual_max"] = max(int(mu[i + 1]), int(mu[i + 2]))
    return f, its, (w, ln, floor)


def main(folder):
    run = A.Run(folder)
    byk = {L["key"]: L for L in run.loads}
    marks = json.load(open(os.path.join(folder, "cargo-series-marks.json"), encoding="utf-8"))
    rows = []
    for m in marks:
        L = byk[m["key"]]
        base, its, b = facts(L, m["k"], m["axis"], 0.0)
        ml = [x for x in L["measures"]] if "measures" in L else []
        print(f"#{m['n']:2d} {m['key']:24s} {m['id']:17s} {m['relation']:5s} on {m['partner']:17s} {m['axis']:6s} wrong {sorted(m['wrong'])}")
        pk, hk = its[m["k"]]
        pp, hp = its[1 - m["k"]]
        print(f"     moved pos {np.round(pk, 3)} half {np.round(hk, 3)} | partner pos {np.round(pp, 3)} half {np.round(hp, 3)} | bed w {b[0]} len {b[1]}")
        for s in (-8, -4, 4, 8):
            f, _, _ = facts(L, m["k"], m["axis"], s / 100)
            f["wrong"] = s in m["wrong"]
            f["n"] = m["n"]
            f["axis"] = m["axis"]
            f["rel"] = m["relation"]
            f["dpen"] = f["pen"] - base["pen"]
            f["dpast_side"] = f["past_side"] - base["past_side"]
            f["dpast_end"] = f["past_end"] - base["past_end"]
            rows.append(f)
            print(f"     {s:+d}: {'WRONG' if f['wrong'] else 'ok   '} pen {f['pen']:.3f} (d {f['dpen']:+.3f}) gap {f['gap']:.3f} "
                  f"past_side {f['past_side']:.3f} past_end {f['past_end']:.3f} by_cart {f.get('by_cart', '-')} "
                  f"mutual {f.get('mutual', '-')}/{f.get('mutual_max', '-')} gap_px {f.get('gap_max', '-')}")
    print()
    ok = [r for r in rows if not r["wrong"]]
    bad = [r for r in rows if r["wrong"]]
    print(f"{len(ok)} ok, {len(bad)} wrong moved states (plus 45 tuned, all ok)")
    for name in ("pen", "dpen", "gap", "past_side", "dpast_side", "past_end", "dpast_end", "by_cart", "mutual", "mutual_max", "gap_max"):
        o = np.array([r.get(name, 0) for r in ok], float)
        w = np.array([r.get(name, 0) for r in bad], float)
        best = (0, None)
        for t in sorted(set(np.concatenate([o, w]))):
            acc = ((o <= t).sum() + (w > t).sum()) / (len(o) + len(w))
            if acc > best[0]:
                best = (acc, t)
        print(f"{name:12s} ok median {np.median(o):8.3f} max {o.max():8.3f} | wrong min {w.min():8.3f} median {np.median(w):8.3f} | split > {best[1]}: {best[0]:.3f}")


if __name__ == "__main__":
    main(sys.argv[1])
