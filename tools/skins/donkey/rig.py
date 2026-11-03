"""The donkey's body points per frame (docs/sprite-tools.md "Catching what the user caught", parts 1 and 3): one file,
tools/skins/donkey/rig.json, that every generator drawing over the donkey is to read instead of finding the body with
rules of its own, and a page to check the points by eye once.

    python -I tools/skins/donkey/rig.py build [--refresh]  # pre-fill rig.json from the frames; frames ticked "checked" or with a note are kept
    python -I tools/skins/donkey/rig.py check              # the rig against itself and the frames: jumps, flags, changed frames
    python -I tools/skins/donkey/rig.py sheet <out.png> [--keys a,b] [--scale 8]  # frames with their points drawn (the game's art: outside the repo)
    python -I tools/skins/donkey/rig.py page               # the checking page, rig.html in the asset probe's folder
    python -I tools/skins/donkey/rig.py import <file>      # a rig.json saved from the page: checked, the changes listed, written

The points, on the game's 96 canvas (the asset probe's donkey-frames, the frames every skin is drawn on):
  body  hip       the rump's end at back level: body pieces hang from it (a cloth, plates)
        back      the back's level over the rump (4 columns before the hip)
        withers   the back just behind the collar strap: where the body's columns start
        chest     the breast collar's lowest point (the strap's end under the chest): a plate's or a skirt's front
        tail      where the tail leaves the rump (its root on the body's edge)
  head  ear_left  ear_right   the ear bases, left and right on the picture (horns, a helmet's crest)
        eye       the eye (the left one where the face looks at us); eye2 the other one there, else none
        poll      the top of the head between the ears (a crown, a plume)
        muzzle    the nose's tip (the lowest point of the face where it looks at us)

The pre-fill: a frame whose donkey is pixel for pixel an earlier frame's behind x 50 copies its hip, back and tail; one
whose head only moved (anatomy.head_delta: walk, ear, chew, lips) has the standing head's points moved with it, where
its own measure is missing; the rest is measured (anatomy.body, body_by_rump, the rump's edge curve, the strap's
pixels, horn_bases.json, anchors.json, the eye and ear tables the user approved in Hellfire's rounds). Where measures
disagree the frame is flagged with each one's answer: those are the frames to look at first. Each frame keeps the SHA-1
of the picture it was measured on; the reader warns when the game's frames change (game updates).
"""
import base64
import hashlib
import io
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SKINS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(SKINS))
sys.path.insert(0, HERE)
sys.path.insert(0, SKINS)
from gamedir import donkey_frames, game_dir
from contact import ORDER
from anatomy import ANCHORS, HORNS, body, body_by_rump, head_delta, load
from skin_preview_html import donkey_regions

MUZZLE = donkey_regions()["Muzzle"]   # the donkey's light muzzle colours (Colors.cs)

RIG = os.path.join(HERE, "rig.json")
BODY = ["hip", "back", "withers", "chest", "tail"]
HEAD = ["ear_left", "ear_right", "eye", "eye2", "poll", "muzzle"]
POINTS = BODY + HEAD
REAR = ("hip", "back", "tail")   # the rump's points: the same where the rump is
REAR_X = 50   # behind this column the body is the same in many frames (the head and neck move in front of it)
STANDING = "bdy_static_left"
ABOUT = {
    "hip": "the rump's end at back level: body pieces hang from it",
    "back": "the back's level over the rump, 4 columns before the hip",
    "withers": "the back just behind the collar strap: the body's columns start here",
    "chest": "the breast collar's lowest point, the strap's end under the chest",
    "tail": "where the tail leaves the rump",
    "ear_left": "the left ear's base on the picture",
    "ear_right": "the right ear's base on the picture",
    "eye": "the eye (the left one where the face looks at us)",
    "eye2": "the other eye where the face looks at us, else none",
    "poll": "the top of the head between the ears",
    "muzzle": "the nose's tip (the lowest point of the face where it looks at us)",
}


def sha1(key):
    return hashlib.sha1(open(os.path.join(donkey_frames(), key + ".png"), "rb").read()).hexdigest()


def pixels(im):
    px = im.load()
    return {(x, y): (px[x, y] if px[x, y][3] else (0, 0, 0, 0)) for y in range(im.height) for x in range(im.width)}


def rear_of(pix):
    return tuple(pix[x, y] for y in range(96) for x in range(REAR_X, 96))


def orange(pix, x, y):
    p = pix.get((x, y))
    return p is not None and p[3] and p[0] >= 90 and p[0] > 1.3 * p[1]


def chest_point(pix):
    """The breast collar (the strap's piece holding its topmost pixel): its lowest row and that row's front pixel."""
    strap = {p for p in pix if orange(pix, *p)}
    if not strap:
        return None
    first = min(strap, key=lambda p: (p[1], p[0]))
    collar, todo = {first}, [first]
    while todo:
        x, y = todo.pop()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                q = (x + dx, y + dy)
                if q in strap and q not in collar:
                    collar.add(q)
                    todo.append(q)
    last = max(y for x, y in collar)
    return [min(x for x, y in collar if y == last), last]


def rump_edge(key):
    """The rump's back edge as a curve: from the first row whose last body pixel behind x REAR_X reaches x 60, five
    rows down: (that row, [the last body pixel's x per row])."""
    px = load(key).load()
    for top in range(30, 70):
        xs = [x for x in range(REAR_X, 96) if px[x, top][3]]
        if xs and max(xs) >= 60:
            break
    else:
        return None
    edge = []
    for y in range(top, top + 5):
        xs = [x for x in range(REAR_X, 96) if px[x, y][3]]
        edge.append(max(xs) if xs else None)
    return top, edge


def rump_shift(key, standing_edge):
    """The rump's sideways shift against the standing frame's: its curve matched from its own top row, the median of
    the five rows' differences. The walk's bob lifts the curve a row or two without moving it sideways (walk 02 is the
    standing curve a row up, walk 03 two); read row by row against the standing frame's rows, that lift on the slanted
    curve read as a column (2026-11-03: the walk came out 66 for a rump at 65, and body()'s 65/66 alternation is the
    same thing)."""
    return rump_move(key, standing_edge)[0]


def rump_move(key, standing_edge):
    """(dx, dy): the rump's move against the standing frame's: dy the row shift that fits the standing curve best
    (the least column difference over its five rows), dx the median column difference there."""
    if standing_edge is None:
        return 0, 0
    px = load(key).load()

    def edge(y):
        xs = [x for x in range(REAR_X, 96) if px[x, y][3]]
        return max(xs) if xs else None
    s_top, s_edge = standing_edge
    best = None
    for dy in range(-3, 4):
        d = sorted(edge(s_top + i + dy) - s for i, s in enumerate(s_edge) if s is not None and edge(s_top + i + dy) is not None)
        if len(d) < 3:
            continue
        med = d[len(d) // 2]
        cost = sum(abs(v - med) for v in d)
        if best is None or (cost, abs(dy)) < (best[0], abs(best[2])):
            best = (cost, med, dy)
    return (best[1], best[2]) if best else (0, 0)


def hellfire_tables():
    """The eye and ear tables the user approved in Hellfire's rounds, on the game's canvas (they are kept relative to a
    drawn area at (16, 16)), and its tail finder."""
    sys.path.insert(0, os.path.join(SKINS, "hellfire"))
    import make_hellfire as MH   # sets the prototype's loader to the game's canvas
    return MH.HP


def offset16(pix):
    xs = [x for (x, y), p in pix.items() if p[3]]
    ys = [y for (x, y), p in pix.items() if p[3]]
    return min(xs) - 16, min(ys) - 16


def measure(key, HP, standing=None, standing_edge=None, force_hip=None):
    """One frame's points, the candidates where there are several measures, and the flags. force_hip: the clip's one
    hip (build: a clip whose rump edge reads one shift in every frame)."""
    im = load(key)
    pix = pixels(im)
    pts, cand, flags = {n: None for n in POINTS}, {}, []
    tdx, tdy = offset16(pix)
    b = body(key, im)
    if b is not None:
        m = body_by_rump(key, im)
        out = standing["points"]["hip"][0] + rump_shift(key, standing_edge) if standing else m["hip"]
        hips = {"body()": b["hip"], "rump match": m["hip"], "rump edge": out}
        hs = sorted(hips.values())
        hip = hs[1] if force_hip is None else force_hip
        if len(set(hs)) > 1:
            cand["hip"] = hips
            flags.append("hip: " + ", ".join(f"{k} {v}" for k, v in hips.items())
                         + (f"; the clip's rump edge reads one shift in every frame: {hip} in all" if force_hip is not None else ""))
        cols = sorted(b["top"][x] for x in range(hip - 8, hip) if x in b["top"])
        back = cols[len(cols) // 2] if cols else None
        if standing:   # the hip's row rides the rump's own up and down (the walk's bob), not the back line's median
            back_row = standing["points"]["hip"][1] + rump_move(key, standing_edge)[1]
        else:
            back_row = back
        pts["hip"] = [hip, back_row]
        pts["back"] = [hip - 4, b["top"].get(hip - 4, back)]
        # body()'s collar column is the strap's dark rear edge, a run of 6 or more rows from above the back (its top is
        # the strap's, never the back's, in all 44 frames): the back starts one column behind it (measured
        # 2026-11-03: Royal Parade's cloth front column followed the strap, 2-3 px off its neighbours in the walk)
        w = b["collar"] + 1
        pts["withers"] = [w, b["top"].get(w, back)]
        pts["chest"] = chest_point(pix)
        px = im.copy().load()
        xs = [x for (x, y), p in pix.items() if p[3]]
        t = HP.find_tail_start(px, 96, 96, HP.DARK, min(xs), max(xs))
        if t:
            pts["tail"] = [t[1], t[0]]
        else:
            flags.append("tail: no root found")
    # the head
    tables = HP.EAR_FRAMES.get(key, {})
    if key in HORNS:
        (xl, yl, _), (xr, yr, _) = HORNS[key]
        pts["ear_left"], pts["ear_right"] = [xl, yl], [xr, yr]
    eye = ANCHORS.get(key, {}).get("eye")
    if tables.get("eyes"):
        es = sorted((x + tdx, y + tdy) for x, y in tables["eyes"])
        pts["eye"] = list(es[0])
        if len(es) > 1:
            pts["eye2"] = list(es[1])
    elif eye:
        pts["eye"] = [int(round(eye[0])), int(round(eye[1]))]
    if pts["ear_left"] and pts["ear_right"]:
        pts["poll"] = [(pts["ear_left"][0] + pts["ear_right"][0] + 1) // 2, min(pts["ear_left"][1], pts["ear_right"][1])]
    head = [(x, y) for (x, y), p in pix.items() if p[3] and (pts["withers"] is None or x < pts["withers"][0])]
    if head:
        if pts["eye2"]:   # the face looks at us: the lowest pixel of its light muzzle, the patch of the donkey's
            # Muzzle colours joined to the face just under the eyes (the lowest of any colour under the eyes was the
            # chest, and the chest has those light colours too)
            ex, ey = (pts["eye"][0] + pts["eye2"][0]) // 2, max(pts["eye"][1], pts["eye2"][1])
            light = {(x, y) for x, y in head if pix[x, y][:3] in MUZZLE and abs(x - ex) <= 6}
            seeds = sorted((p for p in light if ey < p[1] <= ey + 8), key=lambda p: (abs(p[0] - ex) + abs(p[1] - ey - 4)))
            patch, todo = set(seeds[:1]), list(seeds[:1])
            while todo:
                x, y = todo.pop()
                for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if q in light and q not in patch:
                        patch.add(q)
                        todo.append(q)
            low = max(patch, key=lambda p: (p[1], -abs(p[0] - ex))) if patch else None
            pts["muzzle"] = list(low) if low else None
            flags.append("muzzle: the face looks at us, the lowest pixel of its light muzzle taken")
        else:
            mx = min(x for x, y in head)
            ys = sorted(y for x, y in head if x == mx)
            pts["muzzle"] = [mx, ys[len(ys) // 2]]
    return pts, cand, flags


def build(refresh=False):
    HP = hellfire_tables()
    old = json.load(open(RIG, encoding="utf-8")) if os.path.exists(RIG) else None
    frames = {}
    rears = {}
    s_pts, _, s_flags = measure(STANDING, HP)
    standing = {"points": s_pts}
    s_edge = rump_edge(STANDING)
    # a clip whose rump edge reads one shift in every frame has one hip: the rump doesn't move in it, and a hip
    # picked per frame from measures 1-2 apart alternated (the walk 65, 65, 66, 66, 65, 65, 66, 66: Royal Parade's
    # shipped jitter again)
    force, clips = {}, {}
    for key in ORDER:
        if not key.startswith("hed_") and key != STANDING:
            clips.setdefault(clip_of(key), []).append(key)
    for ks in clips.values():
        outs = {s_pts["hip"][0] + rump_shift(k, s_edge) for k in ks}
        if len(ks) > 1 and len(outs) == 1:
            v = outs.pop()
            force.update({k: v for k in ks})
    for key in ORDER:
        if old and key in old["frames"] and (old["frames"][key].get("checked") or old["frames"][key].get("note")) and not refresh:
            frames[key] = old["frames"][key]
            if not key.startswith("hed_") and not frames[key].get("same_body_as"):   # a kept frame is still a body later
                rears.setdefault(rear_of(pixels(load(key))), key)                     # frames can be copies of
            continue
        pts, cand, flags = measure(key, HP, standing if key != STANDING else None, s_edge, force.get(key))
        rec = {"points": pts, "candidates": cand, "flags": flags, "same_body_as": None, "head_from": None,
               "checked": False, "sha1": sha1(key)}
        if not key.startswith("hed_"):
            r = rear_of(pixels(load(key)))
            if r in rears:
                src = rears[r]
                rec["same_body_as"] = src
                for n in ("hip", "back", "tail"):
                    rec["points"][n] = list(frames[src]["points"][n]) if frames[src]["points"][n] else None
                rec["candidates"].pop("hip", None)
                rec["flags"] = [f for f in rec["flags"] if not f.startswith(("hip:", "tail:"))]
            else:
                rears[r] = key
        d = head_delta(key) if key != STANDING else None
        if d is not None and key.split("_")[1] in ("walk", "ear", "chew", "lips"):
            rec["head_from"] = {"frame": STANDING, "delta": list(d)}
            for n in ("eye", "poll", "muzzle"):
                if rec["points"][n] is None and s_pts[n] is not None:
                    rec["points"][n] = [s_pts[n][0] + d[0], s_pts[n][1] + d[1]]
                    rec["flags"].append(f"{n}: none found here (a blink?), the standing one moved with the head")
        frames[key] = rec
    rig = {"version": 1, "canvas": [96, 96], "about": ABOUT, "frames": frames}
    with open(RIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(rig, f, indent=1, sort_keys=False)
        f.write("\n")
    flagged = [k for k, r in frames.items() if r["flags"]]
    print(f"rig.json: {len(frames)} frames, {sum(1 for r in frames.values() if r['checked'])} checked, "
          f"{sum(1 for r in frames.values() if r['same_body_as'])} with the body of an earlier frame, {len(flagged)} flagged")
    for k in flagged:
        print(f"   {k}: " + "; ".join(frames[k]["flags"]))


def read():
    return json.load(open(RIG, encoding="utf-8"))


def clip_of(k):
    return k.rsplit("_", 1)[0] if k[-2:].isdigit() else k


def check():
    rig = read()
    fr = rig["frames"]
    bad = 0
    changed = [k for k in ORDER if k in fr and fr[k].get("sha1") != sha1(k)]
    if changed:
        bad += 1
        print(f"the game's frames changed since the rig was measured: {', '.join(changed)} (rebuild, check again)")
    # identical bodies, identical rear points
    for k, r in fr.items():
        s = r.get("same_body_as")
        if s and any(r["points"][n] != fr[s]["points"][n] for n in ("hip", "back", "tail")):
            bad += 1
            print(f"{k}: the body of {s} pixel for pixel behind x {REAR_X}, but its hip, back or tail differ")
    # jumps between neighbour frames of a clip
    clips = {}
    for k in ORDER:
        if k in fr:
            clips.setdefault(clip_of(k), []).append(k)
    for c, ks in clips.items():
        for a, b in zip(ks, ks[1:] + (ks[:1] if "_walk_" in c else [])):
            for n in POINTS:
                pa, pb = fr[a]["points"].get(n), fr[b]["points"].get(n)
                # the rump's points move by no pixel unless the rump does (a 1-px hip jump was the walk's jitter); the
                # withers and the chest move with the neck, the head's points with the head (said, not judged)
                if pa and pb and max(abs(pa[0] - pb[0]), abs(pa[1] - pb[1])) > (0 if n in REAR else 1 if n in BODY else 3):
                    print(f"{a} -> {b}: {n} moves {pb[0] - pa[0]:+d}, {pb[1] - pa[1]:+d}" + ("" if n in REAR else " (moves with the neck or the head)"))
    flagged = [k for k in ORDER if k in fr and fr[k]["flags"] and not fr[k]["checked"]]
    print(f"{len(fr)} frames: {sum(1 for r in fr.values() if r['checked'])} checked, {len(flagged)} flagged and not checked yet")
    return 1 if bad else 0


COLOURS = {"hip": (255, 60, 60), "back": (255, 160, 40), "withers": (255, 230, 40), "chest": (120, 230, 60),
           "tail": (60, 220, 220), "ear_left": (90, 140, 255), "ear_right": (170, 110, 255), "eye": (255, 255, 255),
           "eye2": (220, 220, 220), "poll": (255, 110, 220), "muzzle": (255, 200, 160)}


def sheet(out, keys=None, S=4):
    if os.path.abspath(out).lower().startswith(os.path.abspath(REPO).lower()):
        sys.exit("rig: the sheet holds the game's art; write it outside the repository")
    fr = read()["frames"]
    cols = 8 if S <= 4 else 4
    keys = [k for k in (keys or ORDER) if k in fr]
    rows = (len(keys) + cols - 1) // cols
    W, H = 96 * S, 96 * S + 14
    im = Image.new("RGBA", (cols * W, rows * H), (40, 40, 40, 255))
    d = ImageDraw.Draw(im)
    for i, k in enumerate(keys):
        ox, oy = (i % cols) * W, (i // cols) * H
        f = Image.new("RGBA", (96, 96), (70, 78, 64, 255))
        f.alpha_composite(load(k))
        im.paste(f.resize((96 * S, 96 * S), Image.NEAREST), (ox, oy + 14))
        r = fr[k]
        d.text((ox + 2, oy + 1), k.replace("bdy_", "").replace("_left", "") + (" *" if r["flags"] else "") + (" ok" if r["checked"] else ""),
               fill=(255, 255, 0, 255))
        for n, p in r["points"].items():
            if p:
                cx, cy = ox + p[0] * S + S // 2, oy + 14 + p[1] * S + S // 2
                c = COLOURS[n] + (255,)
                d.line((cx - S - 2, cy, cx + S + 2, cy), fill=c, width=1)
                d.line((cx, cy - S - 2, cx, cy + S + 2), fill=c, width=1)
                if S >= 6:
                    d.text((cx + 3, cy + 2), n, fill=c)
    im.save(out)
    print(out)


def page():
    """The checking page: rig_page.html (the template, next to this file) with the frames and the rig in it, written
    to the asset probe's folder (it carries the game's frames, so never into the repo)."""
    rig = read()
    frames = {k: "data:image/png;base64," + base64.b64encode(open(os.path.join(donkey_frames(), k + ".png"), "rb").read()).decode()
              for k in ORDER}
    clips = []
    for k in ORDER:
        c = clip_of(k)
        if not clips or clips[-1]["name"] != c:
            clips.append({"name": c, "frames": []})
        clips[-1]["frames"].append(k)
    data = {"points": POINTS, "body": BODY, "rear": list(REAR), "about": ABOUT, "order": ORDER, "clips": clips,
            "standing": STANDING, "frames": frames, "rig": rig,
            "colours": {n: "#%02x%02x%02x" % c for n, c in COLOURS.items()},
            "build": hashlib.sha1(json.dumps(rig, sort_keys=True).encode()).hexdigest()[:12]}
    text = json.dumps(data).replace("</", "<\\/")
    html = open(os.path.join(HERE, "rig_page.html"), encoding="utf-8").read().replace("__DATA__", text)
    out = os.path.join(game_dir(), "BepInEx", "GK2-asset-probe", "rig.html")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(html)
    print(f"page: {out} ({len(html) // 1024} KB)")


def problems(rig):
    """What is wrong with a rig read from a file (an empty list: nothing)."""
    out = []
    if rig.get("version") != 1 or not isinstance(rig.get("frames"), dict):
        return ["not a rig.json of version 1"]
    missing = [k for k in ORDER if k not in rig["frames"]]
    extra = [k for k in rig["frames"] if k not in ORDER]
    if missing or extra:
        out.append(f"frames missing {missing}, unknown {extra}")
    for k, f in rig["frames"].items():
        pts = f.get("points", {})
        if set(pts) != set(POINTS):
            out.append(f"{k}: the points are {sorted(pts)}, not {sorted(POINTS)}")
            continue
        for n, p in pts.items():
            if p is not None and not (isinstance(p, list) and len(p) == 2 and all(isinstance(v, int) and 0 <= v < 96 for v in p)):
                out.append(f"{k}: {n} is {p!r}, not two whole numbers on the 96 canvas or none")
        if not isinstance(f.get("checked", False), bool):
            out.append(f"{k}: 'checked' is not true or false")
    return out


def do_import(path):
    new = json.load(open(path, encoding="utf-8"))
    bad = problems(new)
    if bad:
        for l in bad:
            print("not written:", l)
        return 1
    old = read()
    changes = []
    for k in ORDER:
        o, n = old["frames"][k], new["frames"][k]
        for p in POINTS:
            if o["points"][p] != n["points"][p]:
                changes.append(f"{k}: {p} {o['points'][p]} -> {n['points'][p]}")
        if bool(o.get("checked")) != bool(n.get("checked")):
            changes.append(f"{k}: {'ticked' if n.get('checked') else 'unticked'}")
        if n.get("sha1") != sha1(k):
            changes.append(f"{k}: measured on another picture than the game's frame now (check it again)")
    with open(RIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(new, f, indent=1)
        f.write("\n")
    for l in changes:
        print(l)
    print(f"rig.json written: {len(changes)} change(s), {sum(1 for f in new['frames'].values() if f.get('checked'))} frame(s) checked")
    return 0

def main(argv):
    if not argv:
        print(__doc__)
        return 2
    cmd = argv[0]
    if cmd == "build":
        build("--refresh" in argv)
        return 0
    if cmd == "page":
        page()
        return 0
    if cmd == "import" and len(argv) > 1:
        return do_import(argv[1])
    if cmd == "check":
        return check()
    if cmd == "sheet" and len(argv) > 1:
        keys = argv[argv.index("--keys") + 1].split(",") if "--keys" in argv else None
        S = int(argv[argv.index("--scale") + 1]) if "--scale" in argv else 4
        sheet(argv[1], keys, S)
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
