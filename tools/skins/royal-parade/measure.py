# Per-frame anchors on the game's canvas: the eye, the torso box (coat rows past the head), the back line, the
# collar's rear edge, the bottom per column. Writes anchors.json next to this file; make_royal.py reads it.
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from contact import ORDER, load

HERE = os.path.dirname(os.path.abspath(__file__))
# the donkey's atlas colours (tools/skins/pngcolors.py on the game's frames): the coat, the dark parts, the eye
COAT = {(0x73, 0x6c, 0x57), (0x4a, 0x3d, 0x20), (0x66, 0x5b, 0x44), (0x58, 0x4c, 0x33), (0x3e, 0x30, 0x19), (0x54, 0x50, 0x3b), (0x65, 0x5c, 0x49),
        (0x4d, 0x45, 0x39), (0x36, 0x28, 0x15), (0x45, 0x3b, 0x2e), (0x3c, 0x32, 0x23), (0x3a, 0x34, 0x28), (0x53, 0x4f, 0x3a), (0x44, 0x3b, 0x31)}
DARK = {(0x1e, 0x19, 0x0e), (0x30, 0x1f, 0x0f), (0x29, 0x23, 0x16), (0x35, 0x2e, 0x1e), (0x13, 0x0a, 0x0c), (0x2f, 0x2c, 0x1f)}
EYE = (255, 255, 255)


def find_eye(px, w, h):
    pts = [(x, y) for y in range(h) for x in range(w) if px[x, y][:3] == EYE and px[x, y][3] > 0]
    if not pts:
        return None
    return (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))


def torso_box(px, w, h, ex):
    """The torso's box: coat pixels right of the head (past the eye; with the head down and no eye in view, the right
    60% of the body), rows whose coat run is at least 60% of the widest (the legs fall out)."""
    if ex is None:
        xs_all = [x for y in range(h) for x in range(w) if px[x, y][3] > 0]
        left = int(min(xs_all) + 0.4 * (max(xs_all) - min(xs_all)))
    else:
        left = int(ex) + 10
    rows = {}
    for y in range(h):
        xs = [x for x in range(left, w) if px[x, y][3] > 0 and (px[x, y][:3] in COAT or px[x, y][:3] in DARK)]
        if xs:
            rows[y] = (min(xs), max(xs))
    if not rows:
        return None
    widest = max(b - a for a, b in rows.values())
    keep = [y for y, (a, b) in rows.items() if b - a >= 0.6 * widest]
    return min(rows[y][0] for y in keep), min(keep), max(rows[y][1] for y in keep), max(keep)


def measure(k):
    im = load(k); px = im.load(); w, h = im.size
    e = find_eye(px, w, h)
    known = COAT | DARK | {EYE}
    harness = {px[x, y][:3] for y in range(h) for x in range(w) if px[x, y][3] > 0 and px[x, y][:3] not in known}
    box = torso_box(px, w, h, e[0] if e else None)
    out = {"eye": [round(e[0], 2), round(e[1], 2)] if e else None, "torso": box, "other_colours": len(harness)}
    if box:
        x0, y0, x1, y1 = box
        back = {}
        for x in range(x0, x1 + 1):
            ys = [y for y in range(h) if px[x, y][3] > 0]
            if ys:
                back[x] = min(ys)
        out["back"] = back
        hx = [x for y in range(y0, y1 + 1) for x in range(w) if px[x, y][3] > 0 and px[x, y][:3] in harness]
        out["collar_rear"] = max(hx) if hx else None
        out["bottom"] = {x: (max(ys) if (ys := [y for y in range(y0, h) if px[x, y][3] > 0]) else None) for x in range(x0, x1 + 1)}
    return out


if __name__ == "__main__":
    res = {k: measure(k) for k in ORDER}
    json.dump(res, open(os.path.join(HERE, "anchors.json"), "w"), indent=0)
    for k, r in res.items():
        b = r["torso"]
        print(f"{k:22s} eye={r['eye']} torso={b} collar_rear={r.get('collar_rear')} backmin={min(r['back'].values()) if b else None}")
