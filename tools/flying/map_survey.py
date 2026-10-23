"""Draw a GK2 Donkey API map survey (the Flying tab's "Survey the whole map" button; docs/flying.md).

    python -I tools/flying/map_survey.py <BepInEx/GK2-map-survey-HHmmss folder> [--px 6]

Writes into that folder:
  map-heights.png   top-down (x right, z up): the drawn ground's top per 0.5 cell (the readable terrain meshes),
                    else each chunk's highest point; grey = no drawn ground found. Walkable ground (layers 11 and 12)
                    outlined in white, water in blue, the keeper as a red cross, the 20 tallest props as yellow dots.
  map-walkable.png  the same cells: green = drawn ground with walkable ground at about its height, red = drawn
                    ground with no walkable ground within 0.6 of its top (the places a flight can't land: tables,
                    ledges, decoration), blue = water.
  report.txt        the heights the map holds (ground levels, the tallest props and objects, the red cells' count).
"""
import csv, io, math, os, sys
from collections import Counter, defaultdict

from PIL import Image, ImageDraw


def rows(folder, name):
    p = os.path.join(folder, name)
    if not os.path.exists(p):
        return []
    return list(csv.DictReader(io.open(p, encoding="utf-8").read().splitlines(), delimiter=";"))


def f(v):
    try:
        return float(v)
    except Exception:
        return float("nan")


def colour(h, lo, hi):
    t = 0.0 if hi <= lo else max(0.0, min(1.0, (h - lo) / (hi - lo)))
    # dark blue -> green -> yellow -> red -> white
    stops = [(0, (30, 30, 90)), (0.25, (40, 140, 60)), (0.5, (220, 210, 60)), (0.75, (210, 80, 40)), (1, (250, 250, 250))]
    for (a, ca), (b, cb) in zip(stops, stops[1:]):
        if t <= b:
            u = (t - a) / (b - a)
            return tuple(int(ca[i] + (cb[i] - ca[i]) * u) for i in range(3))
    return stops[-1][1]


def main():
    folder = sys.argv[1]
    px = int(sys.argv[sys.argv.index("--px") + 1]) if "--px" in sys.argv else 6
    cells = {(f(r["x"]), f(r["z"])): f(r["top"]) for r in rows(folder, "cells.csv")}
    terrain = rows(folder, "terrain.csv")
    props = rows(folder, "props.csv")
    wgos = rows(folder, "wgos.csv")
    cols = rows(folder, "colliders.csv")
    head = io.open(os.path.join(folder, "scenes.txt"), encoding="utf-8").readline()

    # the drawn top per 0.5 cell: the readable meshes' cells, else the chunk's highest point over its square
    top = dict(cells)
    for t in terrain:
        if t["readable"] == "True" or t.get("tiles_read") == "True" or t["max_y"] == "":
            continue
        cx, cz, sx, sz, my = f(t["x"]), f(t["z"]), f(t["size_x"]), f(t["size_z"]), f(t["max_y"])
        x = cx - sx / 2
        while x < cx + sx / 2:
            z = cz - sz / 2
            while z < cz + sz / 2:
                k = (math.floor(x * 2) / 2 + 0.25, math.floor(z * 2) / 2 + 0.25)
                if k not in top or my > top[k]:
                    top[k] = my
                z += 0.5
            x += 0.5
    if not top:
        print("no terrain cells in", folder)
        return
    xs = [k[0] for k in top]; zs = [k[1] for k in top]
    x0, x1, z0, z1 = min(xs) - 2, max(xs) + 2, min(zs) - 2, max(zs) + 2
    if "--box" in sys.argv:  # --box x0 x1 z0 z1: only that part of the world (the interiors lie far off the map)
        i = sys.argv.index("--box")
        x0, x1, z0, z1 = (float(v) for v in sys.argv[i + 1:i + 5])
        top = {k: v for k, v in top.items() if x0 <= k[0] <= x1 and z0 <= k[1] <= z1}
        xs = [k[0] for k in top]; zs = [k[1] for k in top]
    W, H = int((x1 - x0) * 2 * px), int((z1 - z0) * 2 * px)
    hs = sorted(top.values())
    lo, hi = hs[int(len(hs) * 0.01)], hs[int(len(hs) * 0.99)]

    def to(x, z):
        return (x - x0) * 2 * px, (z1 - z) * 2 * px

    walk = [c for c in cols if c["layer"] in ("Ground", "Step")]
    water = [c for c in cols if c["layer"] == "Water"]
    # walkable coverage per cell (by the colliders' boxes: a coarse cover, the top within 0.6)
    walk_at = defaultdict(list)
    for c in walk:
        a, b = f(c["min_x"]), f(c["max_x"]); g, h = f(c["min_z"]), f(c["max_z"])
        if (b - a) * (h - g) > 4000:  # a whole level's box says nothing per cell
            continue
        x = math.floor(a * 2) / 2 + 0.25
        while x <= b:
            z = math.floor(g * 2) / 2 + 0.25
            while z <= h:
                walk_at[(x, z)].append(f(c["max_y"]))
                z += 0.5
            x += 0.5

    img = Image.new("RGB", (W, H), (60, 60, 60))
    d = ImageDraw.Draw(img)
    for (x, z), y in top.items():
        sx, sz = to(x - 0.25, z + 0.25)
        d.rectangle([sx, sz, sx + px, sz + px], fill=colour(y, lo, hi))
    for c in water:
        a, b = to(f(c["min_x"]), f(c["max_z"])); e, g = to(f(c["max_x"]), f(c["min_z"]))
        d.rectangle([a, b, e, g], outline=(60, 120, 255), width=2)
    for c in walk:
        a, b = to(f(c["min_x"]), f(c["max_z"])); e, g = to(f(c["max_x"]), f(c["min_z"]))
        if (e - a) * (g - b) < W * H * 0.5:
            d.rectangle([a, b, e, g], outline=(255, 255, 255), width=1)
    tall = sorted((p for p in props if p["max_y"]), key=lambda p: -f(p["max_y"]))[:20]
    for p in tall:
        a, b = to(f(p["x"]), f(p["z"]))
        d.ellipse([a - 5, b - 5, a + 5, b + 5], outline=(255, 230, 0), width=2)
    k = head.split("keeper at ")[1].split(";")[0] if "keeper at " in head else ""
    if k and k != "-":
        kx, ky, kz = [float(v) for v in k.split(",")]
        a, b = to(kx, kz)
        d.line([a - 10, b, a + 10, b], fill=(255, 0, 0), width=3); d.line([a, b - 10, a, b + 10], fill=(255, 0, 0), width=3)
    img.save(os.path.join(folder, "map-heights.png"))

    img2 = Image.new("RGB", (W, H), (40, 40, 40))
    d2 = ImageDraw.Draw(img2)
    red = 0
    for (x, z), y in top.items():
        sx, sz = to(x - 0.25, z + 0.25)
        ok = any(abs(w - y) <= 0.6 for w in walk_at.get((x, z), []))
        if not ok:
            red += 1
        d2.rectangle([sx, sz, sx + px, sz + px], fill=(60, 170, 70) if ok else (200, 50, 50))
    for c in water:
        a, b = to(f(c["min_x"]), f(c["max_z"])); e, g = to(f(c["max_x"]), f(c["min_z"]))
        d2.rectangle([a, b, e, g], outline=(60, 120, 255), width=2)
    img2.save(os.path.join(folder, "map-walkable.png"))

    rep = [head.strip(), f"cells {len(top)} (from readable meshes {len(cells)}), drawn tops {lo:.2f}..{hi:.2f} (1-99 %), highest {hs[-1]:.2f}",
           f"drawn ground with no walkable ground at its height: {red} of {len(top)} cells ({100 * red / max(1, len(top)):.0f} %)",
           "ground levels (drawn tops, rounded to 0.1, the 12 commonest): " + ", ".join(f"{h:.1f}x{n}" for h, n in Counter(round(v, 1) for v in top.values()).most_common(12)),
           "walkable collider tops (rounded to 0.1): " + ", ".join(f"{h:.1f}x{n}" for h, n in sorted(Counter(round(f(c['max_y']), 1) for c in walk).items())),
           "", "the 20 tallest baked props (max y, above its base, path):"]
    for p in tall:
        rep.append(f"  {f(p['max_y']):7.2f}  {f(p['max_y']) - f(p['min_y']):6.2f}  at {f(p['x']):.1f},{f(p['z']):.1f}  {p['path']}")
    rep.append("the 15 tallest world objects:")
    for w in sorted((w for w in wgos if w["max_y"]), key=lambda w: -f(w["max_y"]))[:15]:
        rep.append(f"  {f(w['max_y']):7.2f}  {f(w['max_y']) - f(w['min_y']):6.2f}  at {f(w['x']):.1f},{f(w['z']):.1f}  {w['name']}")
    io.open(os.path.join(folder, "report.txt"), "w", encoding="utf-8").write("\n".join(rep) + "\n")
    print("\n".join(rep[:6]))
    print("wrote map-heights.png, map-walkable.png, report.txt in", folder)


if __name__ == "__main__":
    main()
