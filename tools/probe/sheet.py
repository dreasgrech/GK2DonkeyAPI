"""The ok / wrong sheet for the cargo probe's calibration (docs/cargo-auto-tune.md).

    python tools/probe/sheet.py <probe-run folder>

Writes label-sheet.html into the run folder: sweep pictures spread over what they measure (the largest mutual cover of
the moved item, the largest air gap), the "before" variants (one-pair offsets switched off) and some ok loads as
controls, shuffled. Open it in a browser, mark each picture ok / unsure / wrong (marks are kept in the browser), then
Copy or Download the marks and hand them back.
"""
import json
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import analyze  # noqa: E402


def sweep_samples(run):
    out = []
    for L in run.loads:
        stem = L["file"][:-4]
        for line in L["sweeps"]:
            head, *parts = line.split(" | ")
            t = head.split()
            k, axis, d = int(t[1]), t[2], float(t[3])
            if abs(d) < 0.039:
                continue
            mut, gap = 0, 0
            for part in parts:
                q = part.split()
                gap = max(gap, int(q[q.index("gap_max") + 1]))
                if "mutual" in q:
                    i = q.index("mutual")
                    mut = max(mut, min(int(q[i + 1]), int(q[i + 2])))
            sign = "+" if d > 0 else "-"
            pic = f"{stem}_sweep{k}_{axis}_{sign}{int(round(abs(d) * 100))}_normal.png"
            if os.path.exists(pic):
                out.append({"key": L["key"], "k": k, "axis": axis, "d": d, "mutual": mut, "gap": gap, "pic": pic, "kind": "sweep"})
    return out


def bucket(v, edges):
    for i, e in enumerate(edges):
        if v <= e:
            return i
    return len(edges)


def build(run, n_sweeps=60, n_controls=10, seed=20261016):
    rng = random.Random(seed)
    cells = {}
    for sm in sweep_samples(run):
        cells.setdefault((bucket(sm["mutual"], [0, 100, 300, 1000, 3000]), bucket(sm["gap"], [0, 8, 20, 40])), []).append(sm)
    keys = sorted(cells)
    rng.shuffle(keys)
    pick = []
    while len(pick) < n_sweeps and any(cells[c] for c in keys):
        for c in keys:
            if cells[c] and len(pick) < n_sweeps:
                pick.append(cells[c].pop(rng.randrange(len(cells[c]))))
    for L in run.loads:
        for v in L["variants"]:
            k = int(v.split()[1])
            pic = f"{L['file'][:-4]}_variant{k}_before_normal.png"
            if os.path.exists(pic):
                pick.append({"key": L["key"], "k": k, "axis": "before", "d": 0, "mutual": -1, "gap": -1, "pic": pic, "kind": "before"})
    oks = [L for L in run.loads if L.get("verdict") == "Ok" and os.path.exists(L["file"][:-4] + "_normal.png")]
    for L in rng.sample(oks, min(n_controls, len(oks))):
        pick.append({"key": L["key"], "k": -1, "axis": "", "d": 0, "mutual": -1, "gap": -1, "pic": L["file"][:-4] + "_normal.png", "kind": "control"})
    rng.shuffle(pick)
    for i, p in enumerate(pick):
        p["n"] = i + 1
        p["src"] = os.path.relpath(p["pic"], run.folder).replace(os.sep, "/")
    data = json.dumps([{k: p[k] for k in ("n", "key", "k", "axis", "d", "mutual", "gap", "kind", "src")} for p in pick])
    page = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "sheet.html"), encoding="utf-8").read()
    path = os.path.join(run.folder, "label-sheet.html")
    open(path, "w", encoding="utf-8").write(page.replace("__DATA__", data))
    kinds = {k: sum(p["kind"] == k for p in pick) for k in ("sweep", "before", "control")}
    print(f"{len(pick)} pictures ({kinds['sweep']} sweeps, {kinds['before']} before, {kinds['control']} controls): {path}")


if __name__ == "__main__":
    build(analyze.Run(sys.argv[1]))
