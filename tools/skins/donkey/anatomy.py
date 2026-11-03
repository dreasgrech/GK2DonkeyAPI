"""The donkey's anatomy per frame, for every skin drawn over it (docs/sprite-tools.md "Making a skin"): the measured
landmarks (anchors.json from measure.py, horn_bases.json from horn_bases.py) and what a generator reads off a frame's
pixels. Royal Parade's cloth and the Armored plates both hang their body pieces from the HIP through body(): the rump's
end at back level, the one point of the body that stays put while the head, neck and legs move (the standing and
walk frames all have it at column 65 or 66; only the turn clip moves it). Head pieces (a crown, a chanfron) sit on the
ear bases (HORNS, the Hellfire horn bases: the same point in every frame of a pose) or the eye (ANCHORS "eye", None
where the head hides it: the graze, two lips frames, the turn facing the camera).

    ORDER, load(key)      the 44 frame keys and the donkey's frame on the game's canvas (contact.py)
    HORNS[key]            [[x, y, lean], [x, y, lean]]: the two ear bases (left first), lean 1 = towards the muzzle
    ANCHORS[key]          eye, torso box, back line, collar_rear, bottom per column (measure.py)
    body(key, donkey)     op (opaque test), collar (the strap's rear edge + 1), top (the back line per column inside
                          the torso box), hip, ground; None for a frame without a body (the head frames)
    body_by_rump(key, d)  body() with the hip from the rump matched to the standing frame's (the one to hang body
                          pieces from: body()'s own hip moves in the turn and where the tail swings)
    belly(b)              the belly row per torso column from a body(): the end of the run down from the back, legs
                          left out and bridged
    head_delta(key)       the head's (dx, dy) against the standing frame by the ear bases: the same for the eye and the
                          muzzle wherever the head only moves (walk, ear, chew, lips); a rotated head (graze 02-05,
                          turn 02-06) needs a picture of its own
"""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from contact import ORDER, load  # noqa: F401  (re-exported for the generators)

HORNS = json.load(open(os.path.join(HERE, "horn_bases.json")))
ANCHORS = json.load(open(os.path.join(HERE, "anchors.json")))


def body(key, donkey):
    """What the generator reads off a frame: the body test, the collar's rear edge, the back line per column inside
    the torso box, and the hip (the rump's end at back level). None for a frame without a body."""
    if key.startswith("hed_"):
        return None
    dp = donkey.load()
    op = lambda x, y: 0 <= x < 96 and 0 <= y < 96 and dp[x, y][3] > 0
    orange = lambda x, y: op(x, y) and dp[x, y][0] >= 90 and dp[x, y][0] > 1.3 * dp[x, y][1]
    a = ANCHORS[key]
    if not a.get("torso"):
        return None
    tx0, ty0, tx1, ty1 = a["torso"]
    col = [x for y in range(96) for x in range(96) if orange(x, y)]
    if not col:
        return None
    collar = max(col) + 1
    top = {}
    for x in range(collar, tx1 + 1):
        ys = [y for y in range(96) if op(x, y)]
        if ys:
            top[x] = min(ys)
    if not top:
        return None
    back = min(top.values())
    hip = max(x for y in range(back, back + 6) for x in range(collar, tx1 + 1) if op(x, y))
    ground = max(y for y in range(96) for x in range(96) if op(x, y))
    return {"op": op, "collar": collar, "top": top, "hip": hip, "ground": ground}


RUMP_BOX = (52, 40, 64, 57)   # the hindquarters on the standing frame, the tail's band left out (x0, y0, x1, y1)
_RUMP = None


def body_by_rump(key, donkey):
    """body() with the hip measured by the rump itself: the standing frame's hindquarters (RUMP_BOX) laid over this
    frame where its pixels match best, the hip moved by that shift. body() takes the back's level from the highest
    body pixel behind the collar, and in turn 02-03 that is the turned head's far ear lying back (rows 36-37): its hip
    came out at 63 and 61 on a rump the same as the standing one (Armored's plates slid forward onto the rump; Royal
    Parade's cloth's rear edge jumped 2 and 4 columns forward, the user, 2026-11-03: "the red rug is being pulled
    back"). Not exact to the pixel elsewhere: in walk 04-06 it reads a column forward of a rump that doesn't move,
    and where the tail swings (walk 03-08, graze 02-05, lips 04-08) body(), this and the rump's own outline disagree
    by a column or two. Armored's measure first (make_armored.body_of), shared since 2026-11-03."""
    global _RUMP
    b = body(key, donkey)
    if b is None:
        return None
    if _RUMP is None:
        s = load("bdy_static_left")
        sp = s.load()
        x0, y0, x1, y1 = RUMP_BOX
        _RUMP = (body("bdy_static_left", s)["hip"], {(x, y): sp[x, y] for y in range(y0, y1) for x in range(x0, x1)})
    s_hip, box = _RUMP
    px = donkey.load()
    best = None
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            score = sum(1 for (x, y), c in box.items() if px[x + dx, y + dy] == c)
            if best is None or score > best[0] or (score == best[0] and abs(dx) + abs(dy) < abs(best[1]) + abs(best[2])):
                best = (score, dx, dy)
    b["hip"] = s_hip + best[1]
    return b


def belly(b):
    """The belly row per torso column: the end of the run of body pixels down from the back; a run to the ground is a
    leg, bridged from its neighbours."""
    op, top, ground = b["op"], b["top"], b["ground"]
    out = {}
    for x in sorted(top):
        y = top[x]
        while op(x, y + 1):
            y += 1
        if y < ground - 3:
            out[x] = y
    known = sorted(out)
    for x in top:
        if x not in out:
            left = max((k for k in known if k < x), default=None)
            right = min((k for k in known if k > x), default=None)
            out[x] = out[right] if left is None else out[left] if right is None else round(out[left] + (out[right] - out[left]) * (x - left) / (right - left))
    return out


def top_outline(op, x0, x1):
    """The first opaque row per column over x0..x1 (the back's or the neck's top line), columns with nothing left out."""
    out = {}
    for x in range(x0, x1 + 1):
        ys = [y for y in range(96) if op(x, y)]
        if ys:
            out[x] = min(ys)
    return out


def run_bottom(op, x, through_row):
    """The last row of the opaque run of column x that contains through_row (the chest's underside at a column where
    the leg hangs lower: the run through the chest's row), or None when that row is clear."""
    if not op(x, through_row):
        return None
    y = through_row
    while op(x, y + 1):
        y += 1
    return y


def head_delta(key):
    """The head's shift against the standing frame, by the first ear base."""
    (ax, ay, _), _ = HORNS[key]
    (sx, sy, _), _ = HORNS["bdy_static_left"]
    return ax - sx, ay - sy
