"""Cargo probe analysis (docs/cargo-auto-tune.md): reads a probe run of the showcase director's picture probe.

    python tools/probe/analyze.py <probe-run folder> validate   # does the atlas rebuild every load as the game drew it?
    python tools/probe/analyze.py <probe-run folder> calibrate  # what the ok loads, the "before" variants and the sweeps measure

The game camera is orthographic: an item moved by d in the world is its atlas outline shifted by the projection of d
on the screen, and its camera distances plus the distance component of d. So a load is composed from the atlas: each
item's atlas depth map shifted to where the load's view sits, the nearest distance per pixel winning (the depth
buffer). "validate" compares that with what the game rendered for the load (its mask picture and its measure lines).
"""
import math
import os
import struct
import sys

import numpy as np
from PIL import Image


def floats(s):
    return [float(x) for x in s.split()]


def mat(vals):
    return np.array(vals, dtype=np.float64).reshape(4, 4)


class Camera:
    def __init__(self, w2c, proj, tw, th):
        self.w2c, self.proj, self.tw, self.th = w2c, proj, tw, th

    def screen(self, p):
        """World point -> (x, y) in the target's pixels (rows upward) and the camera distance."""
        c = self.w2c @ np.array([p[0], p[1], p[2], 1.0])
        n = self.proj @ c
        return (n[0] + 1) / 2 * self.tw, (n[1] + 1) / 2 * self.th, -c[2]


def read_depth(path):
    b = open(path, "rb").read()
    assert b[:4] == b"GKD1", path
    x, y, w, h = struct.unpack_from("<4i", b, 4)
    a = np.frombuffer(b, dtype="<f4", count=w * h, offset=20).reshape(h, w)
    k = np.frombuffer(b, dtype="<f4", count=w * h, offset=20 + 4 * w * h).reshape(h, w)
    return (x, y, w, h), a.copy(), k.copy()


def parse_kv_line(line):
    """'key v v v key2 v ...' is not regular enough: the callers pick by position."""
    return line.split()


class Run:
    def __init__(self, folder):
        self.folder = folder
        run = open(os.path.join(folder, "run.txt"), encoding="utf-8").read().splitlines()
        self.tw = self.th = None
        for line in run:
            t = line.split()
            if t[0] == "camera":
                i = t.index("target")
                self.tw, self.th = int(t[i + 1]), int(t[i + 2])
        self.atlas = {}
        adir = os.path.join(folder, "atlas")
        if os.path.isdir(adir):
            for f in os.listdir(adir):
                if f.endswith(".txt") and not f.startswith("_"):
                    a = self.read_atlas(os.path.join(adir, f))
                    if a:
                        self.atlas[a["id"]] = a
        self.loads = []
        for f in sorted(os.listdir(folder)):
            if f.endswith(".txt") and f[:4].isdigit():
                self.loads.append(self.read_load(os.path.join(folder, f)))

    def read_atlas(self, path):
        a = {"file": path}
        for line in open(path, encoding="utf-8"):
            t = line.split()
            if not t:
                continue
            if t[0] == "id":
                a["id"] = t[1]
            elif t[0] == "roi":
                a["roi"] = tuple(int(v) for v in t[1:5])
            elif t[0] == "view_local":
                i = t.index("view_world")
                a["view_world"] = np.array(floats(" ".join(t[i + 1:i + 4])))
            elif t[0] == "outline":
                a["outline"] = int(t[1])
        dpath = path[:-4] + ".depth"
        if not os.path.exists(dpath) or "roi" not in a:
            return None
        a["box"], a["front"], a["back"] = read_depth(dpath)
        return a

    def read_load(self, path):
        L = {"file": path, "items": [], "measures": [], "pairs": [], "sweeps": [], "variants": []}
        for line in open(path, encoding="utf-8"):
            t = line.split()
            if not t:
                continue
            k = t[0]
            if k == "load":
                L["key"] = line[5:].strip()
            elif k == "verdict":
                L["verdict"] = t[1]
            elif k == "picture":
                L["w"], L["h"] = int(t[1]), int(t[2])
                L["roi"] = tuple(int(v) for v in t[4:8])
            elif k == "probe_mode":
                L["mode"] = t[1]
            elif k == "world_to_camera":
                L["w2c"] = mat(floats(" ".join(t[1:17])))
            elif k == "projection":
                L["proj"] = mat(floats(" ".join(t[1:17])))
            elif k == "item":
                it = {"index": int(t[1]), "id": t[3], "layer": int(t[5]), "relation": t[7], "partner": int(t[9]), "tuned": t[11] == "True"}
                i = t.index("view_world")
                it["view_world"] = np.array(floats(" ".join(t[i + 1:i + 4])))
                i = t.index("pos")
                it["pos"] = np.array(floats(" ".join(t[i + 1:i + 4])))
                L["items"].append(it)
            elif k == "measure":
                m = {"index": int(t[1]), "outline": int(t[3]), "visible": int(t[5]), "by_cart": int(t[7])}
                i = t.index("covered_by")
                m["covered_by"] = [int(v) for v in t[i + 1:i + 7]]
                i = t.index("gap_cols")
                m["gap_cols"], m["gap_max"], m["gap_sum"], m["under_cols"] = int(t[i + 1]), int(t[i + 3]), int(t[i + 5]), int(t[i + 7])
                L["measures"].append(m)
            elif k == "pair":
                L["pairs"].append((int(t[1]), int(t[2]), int(t[4]), int(t[6])))
            elif k == "crop":
                L["crop"] = tuple(int(v) for v in t[1:5])
            elif k == "sweep" or k == "variant":
                L["sweeps" if k == "sweep" else "variants"].append(line.strip())
        L["cam"] = Camera(L["w2c"], L["proj"], self.tw, self.th) if "w2c" in L else None
        base = path[:-4]
        L["mask_png"] = base + "_mask.png"
        return L


def atlas_camera(run, a):
    """The camera the atlas entry was taken with: the same file layout as a load (the atlas txt has no matrices, the
    run.txt ones are the run's start); the keeper stands still for the run, so the run's camera is used."""
    lines = open(os.path.join(run.folder, "run.txt"), encoding="utf-8").read().splitlines()
    w2c = proj = None
    for line in lines:
        t = line.split()
        if t[0] == "world_to_camera":
            w2c = mat(floats(" ".join(t[1:17])))
        elif t[0] == "projection":
            proj = mat(floats(" ".join(t[1:17])))
    return Camera(w2c, proj, run.tw, run.th)


def compose(run, L):
    """Each item's outline and distances in the load's region, from the atlas; None for an item without an entry."""
    w, h = L["w"], L["h"]
    rx, ry = L["roi"][0], L["roi"][1]
    out = []
    acam = atlas_camera(run, None)
    for it in L["items"]:
        a = run.atlas.get(it["id"])
        if a is None:
            out.append(None)
            continue
        lx, ly, ld = L["cam"].screen(it["view_world"])
        ax, ay, ad = acam.screen(a["view_world"])
        dx, dy, dd = lx - ax, ly - ay, ld - ad
        bx, by, bw, bh = a["box"]
        # the atlas box in target pixels, moved by the shift, then into the load's region
        ox = a["roi"][0] + bx + int(round(dx)) - rx
        oy = a["roi"][1] + by + int(round(dy)) - ry
        depth = np.full((h, w), np.nan, dtype=np.float32)
        src = a["front"] + np.float32(dd)
        x0, y0 = max(0, ox), max(0, oy)
        x1, y1 = min(w, ox + bw), min(h, oy + bh)
        if x1 > x0 and y1 > y0:
            depth[y0:y1, x0:x1] = src[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
        out.append(depth)
    return out


def measured_outlines(L):
    """The load's own per-item outlines from its mask picture (g: the outlines' bits), in region pixels, rows upward."""
    img = np.array(Image.open(L["mask_png"]).convert("RGBA"))
    cx, cy, cw, ch = L["crop"]
    img = img[::-1]  # the picture's top row is the texture's last
    full = np.zeros((L["h"], L["w"]), dtype=np.uint8)
    full[cy:cy + ch, cx:cx + cw] = img[:, :, 1]
    return [(full >> k) & 1 == 1 for k in range(len(L["items"]))]


def validate(run):
    print(f"{len(run.atlas)} atlas entries, {len(run.loads)} loads")
    worst = []
    ious = []
    vis_err = []
    for L in run.loads:
        if L.get("cam") is None or not os.path.exists(L["mask_png"]):
            continue
        comp = compose(run, L)
        meas = measured_outlines(L)
        stack = np.stack([np.where(np.isnan(c), np.inf, c) if c is not None else np.full((L["h"], L["w"]), np.inf) for c in comp])
        owner = np.where(np.isinf(stack.min(axis=0)), -1, stack.argmin(axis=0))
        for k, c in enumerate(comp):
            if c is None:
                continue
            a = ~np.isnan(c)
            b = meas[k]
            inter, union = np.logical_and(a, b).sum(), np.logical_or(a, b).sum()
            iou = inter / union if union else 1.0
            ious.append(iou)
            worst.append((iou, L["key"], k, L["items"][k]["id"]))
            m = next((m for m in L["measures"] if m["index"] == k), None)
            if m and m["outline"]:
                vis_comp = int(np.logical_and(owner == k, a).sum())
                # the game's visible count includes the cart covering; compare against items only: visible + by_cart
                vis_err.append(abs(vis_comp - (m["visible"] + m["by_cart"])) / m["outline"])
    ious = np.array(ious)
    print(f"outline match (IoU), composed from the atlas vs the game's render: {len(ious)} items, median {np.median(ious):.4f}, 5th percentile {np.percentile(ious, 5):.4f}, min {ious.min():.4f}")
    ve = np.array(vis_err)
    print(f"visible part (items only), composed vs measured, error as a share of the outline: median {np.median(ve):.4f}, 95th {np.percentile(ve, 95):.4f}, max {ve.max():.4f}")
    worst.sort()
    for iou, key, k, id_ in worst[:8]:
        print(f"  lowest: {iou:.4f}  {key}  item {k} ({id_})")


def calibrate(run):
    oks = [L for L in run.loads if L.get("verdict") == "Ok"]
    print(f"{len(oks)} ok loads")
    mut, gaps, gapcols, cart = [], [], [], []
    for L in oks:
        for a, b, ab, ba in L["pairs"]:
            mut.append(min(ab, ba))
        for m in L["measures"]:
            if m["under_cols"]:
                gaps.append(m["gap_max"])
                gapcols.append(m["gap_cols"] / m["under_cols"])
    def q(v):
        v = np.array(v) if len(v) else np.array([0])
        return f"median {np.median(v):.1f}, 90th {np.percentile(v, 90):.1f}, max {v.max():.1f}"
    print(f"ok loads, mutual cover per pair (px, both ways, the smaller): {q(mut)}")
    print(f"ok loads, largest air gap under an upper item (px): {q(gaps)}")
    print(f"ok loads, share of columns with air under an upper item: {q(gapcols)}")
    variants = [(L["key"], v) for L in run.loads for v in L["variants"]]
    print(f"{len(variants)} 'before' variants")
    for key, v in variants:
        print(f"  {key}: {v[:220]}")


if __name__ == "__main__":
    run = Run(sys.argv[1])
    what = sys.argv[2] if len(sys.argv) > 2 else "validate"
    {"validate": validate, "calibrate": calibrate}[what](run)
