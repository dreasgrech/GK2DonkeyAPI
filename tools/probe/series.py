"""The series sheet for the cargo probe's calibration (docs/cargo-auto-tune.md).

    python tools/probe/series.py <probe-run folder> [rows]

Writes series-sheet.html into the run folder: rows of one load, one item, one axis, its pictures at -8, -4, 0 (the
tuned place), +4 and +8 cm side by side. The user clicks the pictures that look wrong (the rest count as ok) and ticks
the row as checked; marks are kept in the browser and copied or downloaded as JSON. Rows are chosen to cover as many
different contacts (the moved item's class against what it touches) and axes as possible, from loads of two and three
items, reviewed loads first.
"""
import json
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import analyze  # noqa: E402

STEPS = [-8, -4, 0, 4, 8]


def rows_of(run):
    out = []
    for L in run.loads:
        if len(L["items"]) < 2:
            continue
        stem = L["file"][:-4]
        base = stem + "_normal.png"
        if not os.path.exists(base):
            continue
        for it in L["items"][1:]:
            k = it["index"]
            partner = L["items"][it["partner"]]["id"] if 0 <= it["partner"] < len(L["items"]) else ""
            for axis in ("along", "up", "across"):
                pics = []
                for s in STEPS:
                    if s == 0:
                        pics.append(base)
                    else:
                        pics.append(f"{stem}_sweep{k}_{axis}_{'+' if s > 0 else '-'}{abs(s)}_normal.png")
                if all(os.path.exists(p) for p in pics):
                    out.append({"key": L["key"], "k": k, "id": it["id"], "relation": it["relation"], "partner": partner,
                                "axis": axis, "verdict": L.get("verdict", "none"), "items": len(L["items"]), "pics": pics})
    return out


def build(run, n_rows=45, seed=20261016):
    rng = random.Random(seed)
    cand = rows_of(run)
    rng.shuffle(cand)
    # reviewed loads first, then two-item loads (the plainest contacts), then the rest
    cand.sort(key=lambda r: (r["verdict"] != "Ok", r["items"]))
    seen_contact = {}
    pick = []
    # first pass: one row per (contact, axis); later passes fill up
    for limit in (1, 2, 3):
        for r in cand:
            if len(pick) >= n_rows:
                break
            c = (r["id"], r["partner"], r["relation"], r["axis"])
            if seen_contact.get(c, 0) >= limit or r in pick:
                continue
            seen_contact[c] = seen_contact.get(c, 0) + 1
            pick.append(r)
    rng.shuffle(pick)
    for i, r in enumerate(pick):
        r["n"] = i + 1
        r["srcs"] = [os.path.relpath(p, run.folder).replace(os.sep, "/") for p in r["pics"]]
    data = json.dumps([{k: r[k] for k in ("n", "key", "k", "id", "relation", "partner", "axis", "srcs")} for r in pick])
    page = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "series.html"), encoding="utf-8").read()
    path = os.path.join(run.folder, "series-sheet.html")
    open(path, "w", encoding="utf-8").write(page.replace("__DATA__", data))
    print(f"{len(pick)} rows of {len(STEPS)} pictures: {path}")


if __name__ == "__main__":
    build(analyze.Run(sys.argv[1]), int(sys.argv[2]) if len(sys.argv) > 2 else 45)
