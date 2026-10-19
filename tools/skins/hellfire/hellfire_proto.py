"""Hellfire donkey, prototype 2 (the user's notes on 1: no nostril embers, no hoof flames, cracks that stay put and off
the face, the donkey's own tail gone under the devil's tail). Pieces placed from each frame's own pixels; the cracks
are fixed lines in the torso's own coordinates (its bounding box per frame), so they move with the body instead of
being rolled again per frame. Output: a before/after sheet at 5x and a walk GIF. The frames are the game's art
(C:\\ClaudeCodeTemp\\GK2-donkey-frames, read by path: the game's art stays out of the repo).
The shipping code is src/GK2DonkeyAPI/LooksHellfire.cs, this prototype's rules in C#; this file is kept as the
reviewed original (14 rounds with the user, 2026-10-18/19). Run: python tools/skins/hellfire/hellfire_proto.py"""
import colorsys, math, os, sys
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from gamedir import donkey_frames
SRC = donkey_frames()  # the donkey's frames as the game places them (the asset probe's export; the game's art stays out of the repo)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")  # the sheet and the GIF (git-ignored)
PAD = 14

COAT = {(0x73,0x6c,0x57),(0x4a,0x3d,0x20),(0x66,0x5b,0x44),(0x58,0x4c,0x33),(0x3e,0x30,0x19),(0x54,0x50,0x3b),(0x65,0x5c,0x49),(0x4d,0x45,0x39),(0x36,0x28,0x15),(0x45,0x3b,0x2e),(0x3c,0x32,0x23),(0x3a,0x34,0x28),(0x53,0x4f,0x3a),(0x44,0x3b,0x31)}
DARK = {(0x1e,0x19,0x0e),(0x30,0x1f,0x0f),(0x29,0x23,0x16),(0x35,0x2e,0x1e),(0x13,0x0a,0x0c),(0x2f,0x2c,0x1f)}
EYE = (255, 255, 255)
OUTLINE = (30, 14, 12)
BONE = [(236, 220, 180), (200, 176, 130), (120, 90, 60)]
LAVA = [(255, 150, 40), (255, 90, 24), (150, 34, 12)]
# the cracks, in torso coordinates (u across the torso from the shoulder, v down from the back line): three lines,
# the middle one with a branch
CRACKS = [
    [(0.12, 0.30), (0.20, 0.48), (0.16, 0.66), (0.26, 0.84)],
    [(0.46, 0.22), (0.52, 0.40), (0.62, 0.50), (0.58, 0.72)],
    [(0.52, 0.40), (0.42, 0.56)],
    [(0.80, 0.34), (0.86, 0.52), (0.78, 0.70), (0.84, 0.86)],
]


def load(name):
    im = Image.open(os.path.join(SRC, (name[5:] if name.startswith("0301_") else name) + ".png")).convert("RGBA")
    c = Image.new("RGBA", (im.width + 2 * PAD, im.height + 2 * PAD), (0, 0, 0, 0))
    c.alpha_composite(im, (PAD, PAD))
    return c


def put(px, x, y, col, w, h):
    if 0 <= x < w and 0 <= y < h:
        px[x, y] = col + (255,)


def opaque(px, x, y, w, h):
    return 0 <= x < w and 0 <= y < h and px[x, y][3] > 0


def is_coat(px, x, y, w, h):
    return opaque(px, x, y, w, h) and px[x, y][:3] in COAT


def find_eye(px, w, h):
    pts = [(x, y) for y in range(h) for x in range(w) if px[x, y][:3] == EYE and px[x, y][3] > 0]
    if not pts:
        return None
    return (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts), pts)


def runs(px, y, w, h):
    """opaque runs of a row as (x0, x1)"""
    out, start = [], None
    for x in range(w + 1):
        on = x < w and px[x, y][3] > 0
        if on and start is None:
            start = x
        elif not on and start is not None:
            out.append((start, x - 1)); start = None
    return out


def reaches_ground(px, w, h, run, y, bottom):
    """an unbroken chain of drawn pixels from the run down to the body's lowest three rows: a leg; the tuft has a gap"""
    a, b = run
    for yy in range(y + 1, bottom + 1):
        xs = [x for x in range(max(0, a - 1), min(w - 1, b + 1) + 1) if px[x, yy][3] > 0]
        if not xs:
            return False
        if yy >= bottom - 2:
            return True
        a, b = xs[0], xs[-1]
    return True


def erase_tail(px, w, h, box):
    """the donkey's own tail: everything right of the rump's outline. The outline is one dark pixel at the rear of
    every torso row, a continuous curve from the back line down, moving at most two columns a row: tracked from the
    top of the torso box (where no tail is), and every pixel right of it goes; below the torso, everything right of
    the curve's lowest point (where the tuft hangs). Returns the root for the new tail."""
    x0, y0, x1, y1 = box
    prev = None
    corner = None
    for y in range(y0, y1 + 1):
        r = runs(px, y, w, h)
        if not r:
            continue
        if prev is None:
            prev = r[-1][1]  # the back line's end: no tail that high
        cands = [x for x in range(max(0, prev - 2), min(w - 1, prev + 2) + 1) if px[x, y][3] > 0 and px[x, y][:3] in DARK]
        if not cands:
            cands = [x for x in range(max(0, prev - 2), min(w - 1, prev + 2) + 1) if px[x, y][3] > 0]
        if not cands:
            continue
        outline = min(cands, key=lambda x: (abs(x - prev), -x))
        # a two-pixel outline at the row's end is the body's (the tail's dark pixels run longer, or sit past a gap)
        end = outline
        while end + 1 < w and px[end + 1, y][3] > 0 and px[end + 1, y][:3] in DARK:
            end += 1
        if end - outline <= 1 and (end + 1 >= w or px[end + 1, y][3] == 0):
            outline = end
        for x in range(outline + 1, w):
            px[x, y] = (0, 0, 0, 0)
        px[outline, y] = OUTLINE + (255,)
        prev = corner = outline
    if corner is None:
        return None
    for y in range(y1 + 1, h):
        for x in range(corner, w):  # the tuft hangs from the corner column itself
            if px[x, y][3] > 0:
                px[x, y] = (0, 0, 0, 0)
        r = runs(px, y, w, h)
        if r and r[-1][1] == corner and px[corner, y][:3] in COAT:
            px[corner, y] = OUTLINE + (255,)
    return (corner, y0 + (y1 - y0) // 2)


# The donkey's tail, found as it is drawn: it lies against the rump (an outline, coat fill, an outline) and hangs
# below as a dark tuft. The rump's own edge is the inner dark line where the tail branches off. Top-down coordinates.
def find_tail_start(px, w, h, dark, bx0, bx1):
    rear = bx0 + 0.7 * (bx1 - bx0)
    for y in range(h):
        xs = [x for x in range(w) if px[x, y][3] > 0]
        if not xs: continue
        end = xs[-1]
        if end < rear or px[end, y][:3] not in dark: continue
        # walk left from the end: the end's dark run, then at least one non-dark, then a dark pixel within 5 of the end
        x = end
        while x - 1 >= 0 and px[x - 1, y][3] > 0 and px[x - 1, y][:3] in dark: x -= 1
        if x - 1 < 0 or px[x - 1, y][3] == 0: continue
        x -= 1
        while x - 1 >= 0 and px[x - 1, y][3] > 0 and px[x - 1, y][:3] not in dark: x -= 1
        inner = x - 1
        if inner >= 0 and px[inner, y][3] > 0 and px[inner, y][:3] in dark and end - inner <= 5:
            return y, inner
    return None

def erase_tail_drawn(px, w, h, dark):
    pts = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0]
    bx0 = min(p[0] for p in pts); bx1 = max(p[0] for p in pts)
    start = find_tail_start(px, w, h, dark, bx0, bx1)
    if start is None: return None
    y0, prev = start
    def op(x, y): return 0 <= x < w and 0 <= y < h and px[x, y][3] > 0
    def isd(x, y): return op(x, y) and px[x, y][:3] in dark
    # the rump's edge, row by row: moves along drawn pixels only (never across a gap onto the tuft)
    line = {}
    for y in range(y0, h):
        cands = []
        for x in range(max(0, prev - 1), min(w, prev + 2)):
            lo, hi = min(x, prev - 1), max(x, prev - 1)
            if isd(x, y) and all(op(k, y) for k in range(lo, hi + 1)):
                cands.append(x)
        if cands:
            L = min(cands)
            prev = L + 1 if isd(L + 1, y) else L
        line[y] = prev
    for y in range(y0 + 1, h - 1):
        if y + 1 in line:
            line[y] = max(line[y], min(line[y - 1], line[y + 1]))
    # an edge pixel apart from the body (a gap before it) is the tuft, not the rump: the edge is the body run's end
    # (a lone pixel stayed at the rump's foot in every idle frame, 2026-10-19)
    for y, e in list(line.items()):
        r = runs(px, y, w, h)
        for i, (a, b) in enumerate(r):
            if a <= e <= b and i > 0 and a - r[i - 1][1] >= 2 and e - r[i - 1][1] <= 4:  # the body is right there: this run is tuft
                line[y] = r[i - 1][1]
                break
    # right of the edge; only what is connected to the tail where it starts (a leg's edge pixel stays)
    right = {(x, y) for y, e in line.items() for x in range(e + 1, w) if op(x, y)}
    seeds = [(x, y) for (x, y) in right if y <= y0 + 1]
    tail, stack = set(), list(seeds)
    while stack:
        q = stack.pop()
        if q in tail or q not in right: continue
        tail.add(q)
        x, y = q
        stack += [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
    # and every other piece right of the edge unless it reaches the ground (a leg); the rest is tuft left over
    bottom = max(p[1] for p in pts)
    rest = right - tail
    seen = set()
    for q in rest:
        if q in seen: continue
        comp, stack = set(), [q]
        while stack:
            c = stack.pop()
            if c in comp or c not in rest: continue
            comp.add(c)
            x, y = c
            stack += [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
        seen |= comp
        if max(c[1] for c in comp) < bottom - 2:
            tail |= comp
    for (x, y) in tail:
        px[x, y] = (0, 0, 0, 0)
    return y0, line


SHAPE = 0.65


def thigh_row(px, w, h, y0, top):
    """the first row under the rump where the body splits into legs at the back (the last run, the hind leg, starts
    within 14 columns of the rump's edge: a lowered head is another run too, far forward, grazing frame 2 2026-10-19)"""
    for y in range(y0 + 3, h):
        r = runs(px, y, w, h)
        if len(r) >= 2 and r[-1][0] - r[-2][1] >= 2 and r[-1][0] >= top - 14:
            return y, r[-1][1]
    return None


def fill_rump(px, w, h, y0, yb, bulge_per_row=1 / 7.0):
    """the rump's back where the tail was, as one curve: from the edge just above where the tail branched off (y0 - 1)
    down to the back of the thigh (thigh_row), bowed out mid-way and turning in as it reaches the thigh, so it rounds
    into the leg instead of meeting the belly at a right angle (2026-10-19). Every row between is set to the curve:
    filled out in its coat colour or trimmed, the outline at the end. Returns the curve, row -> column."""
    def edge(y):
        r = runs(px, y, w, h)
        return r[-1][1] if r else None
    top = edge(y0 - 1)
    th = thigh_row(px, w, h, y0, top) if top is not None else None
    if top is None or th is None:
        return {}
    yl, bot = th
    outline = px[top, y0 - 1]
    n = yl - (y0 - 1)
    bulge = max(1.0, n * bulge_per_row)
    # fullest about a third of the way down (SHAPE), turning in gradually to the thigh
    want = {y: top + rh((bot - top) * (y - (y0 - 1)) / n + bulge * math.sin(math.pi * ((y - (y0 - 1)) / n) ** SHAPE)) for y in range(y0, yl)}
    want[y0 - 1], want[yl] = top, bot
    for y in range(y0, yl):  # no single row past both neighbours
        want[y] = min(want[y], max(want[y - 1], want[y + 1]))
    lava = {(255, 150, 40), (255, 90, 24), (150, 34, 12)}
    curve = {}
    for y in range(y0, yl):
        target = want[y]
        e = edge(y)
        if e is None:
            continue
        last = max((x for x in range(0, min(e, target) + 1) if px[x, y][3] > 0 and (px[x, y][:3] in COAT or px[x, y][:3] in lava)), default=None)
        if last is None:
            continue
        coat = next((px[x, y] for x in range(last, max(0, last - 10), -1) if px[x, y][:3] in COAT), px[last, y])
        for x in range(last + 1, target):
            px[x, y] = coat
        for x in range(target + 1, w):
            px[x, y] = (0, 0, 0, 0)
        px[target, y] = outline
        curve[y] = target
    return curve

def smooth_root(px, w, h, root):
    """after the tail is drawn: around its root, any body edge pixel sticking out past both neighbouring rows' edges
    (the tail's own root pixel counts as a row's edge) is moved in one: the outline to the column before it"""
    TAILC = {(44, 18, 16), (70, 26, 20), (200, 40, 30), (120, 20, 16)}
    rx, ry = root
    for _ in range(4):
        e = {}
        for y in range(max(0, ry - 8), min(h, ry + 9)):
            xs = [x for x in range(rx - 6, min(w, rx + 3)) if px[x, y][3] > 0]
            if not xs:
                continue
            x = max(xs)
            while x - 1 >= 0 and px[x, y][:3] in TAILC and px[x - 1, y][:3] in TAILC:
                x -= 1
            e[y] = x
        moved = False
        for y in sorted(e):
            if y - 1 in e and y + 1 in e and e[y] > e[y - 1] and e[y] > e[y + 1] and px[e[y], y][:3] not in TAILC:
                x = e[y]
                px[x - 1, y] = px[x, y]
                px[x, y] = (0, 0, 0, 0)
                moved = True
        if not moved:
            break


# Frames whose ears a rule can't read (2026-10-19): the head down grazing (one ear up, the far one lying back on the
# neck as mane) and the head turned to the camera (two ears either side of the forelock). Per frame key: the boxes
# that are ear (x0, x1, y0, y1, inclusive, top-down canvas rows) and the horns (base x, base y, lean).
EAR_FRAMES = {
    "bdy_graze_left_03": {"erase": [(19, 26, 0, 22)], "horns": [(22, 23, 1), (26, 24, 1)]},
    "bdy_graze_left_04": {"erase": [(18, 25, 0, 22)], "horns": [(21, 23, 1), (25, 24, 1)]},
    "bdy_graze_left_05": {"erase": [(18, 25, 0, 22)], "horns": [(21, 23, 1), (25, 24, 1)]},
    "bdy_turn_left_04": {"erase": [(15, 35, 0, 22)], "horns": [(20, 23, -1), (30, 23, 1)], "eyes": [(20, 30), (27, 30)]},
    "bdy_turn_left_05": {"erase": [(15, 35, 0, 22)], "horns": [(20, 23, -1), (30, 23, 1)], "eyes": [(21, 31), (27, 31)]},
    "bdy_turn_left_06": {"erase": [(15, 35, 0, 22)], "horns": [(20, 23, -1), (30, 23, 1)], "eyes": [(21, 31), (27, 31)]},
}


# The head mid-turn (turn 2 and 3, 2026-10-19): the ears' roots move forward as the face turns, so the eye-based
# offset lands behind them; the horns at the ears' roots by hand.
HORN_FRAMES = {
    "bdy_turn_left_02": [(27, 28, 1), (32, 29, 1)],
    "bdy_turn_left_03": [(19, 26, 1), (25, 26, 1)],
}


def erase_boxes(px, w, h, boxes):
    for (x0, x1, y0, y1) in boxes:
        for y in range(max(0, y0), min(h, y1 + 1)):
            for x in range(max(0, x0), min(w, x1 + 1)):
                px[x, y] = (0, 0, 0, 0)
    for y in range(1, h):
        for x in range(w):
            if px[x, y][3] > 0 and px[x, y - 1][3] == 0 and px[x, y][:3] in COAT:
                px[x, y] = OUTLINE + (255,)


def skull_top(px, w, h):
    """after the ears are gone: the head's top row (topmost pixels within 16 columns of the muzzle tip) and its right end"""
    pts = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0]
    mx = min(p[0] for p in pts)
    head = [p for p in pts if p[0] <= mx + 16]
    ty = min(p[1] for p in head)
    return max(p[0] for p in head if p[1] == ty), ty


def erase_ears(px, w, h, ey):
    """the donkey's ears: from the top row down, the run under the topmost pixel while it is at most 7 wide and above
    the eyes (the skull is wider); the skull's bared top edge becomes outline"""
    top = None
    for y in range(h):
        if any(px[x, y][3] > 0 for x in range(w)):
            top = y
            break
    if top is None:
        return None
    base = None
    col = next(x for x in range(w) if px[x, top][3] > 0)
    limit = int(ey) - 4 if ey is not None else h
    for y in range(top, min(h, limit)):
        r = [run for run in runs(px, y, w, h) if run[0] <= col <= run[1]]
        if not r:
            near = sorted(runs(px, y, w, h), key=lambda run: min(abs(run[0] - col), abs(run[1] - col)))
            if not near or min(abs(near[0][0] - col), abs(near[0][1] - col)) > 2:
                break
            r = [near[0]]
        a, b = r[0]
        if b - a + 1 > 7:
            break
        for x in range(a, b + 1):
            px[x, y] = (0, 0, 0, 0)
        col = (a + b) // 2
        base = (col, y)
    for y in range(1, h):
        for x in range(w):
            if px[x, y][3] > 0 and px[x, y - 1][3] == 0 and px[x, y][:3] in COAT:
                px[x, y] = OUTLINE + (255,)
    return base  # the ear's root: the last ear row erased, its middle column


def darken_coat(px, w, h):
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a and (r, g, b) in COAT:
                hh, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
                nr, ng, nb = colorsys.hsv_to_rgb(0.02, min(1.0, s * 0.9 + 0.25), v * 0.42)
                px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), 255)


def torso_box(px, w, h, ex):
    """the torso's box: coat pixels right of the head (past the eye; with the head down and no eye in view, the right
    60% of the body), rows whose coat run is at least 60% of the widest (the legs fall out)"""
    if ex is None:
        xs_all = [x for y in range(h) for x in range(w) if px[x, y][3] > 0]
        left = int(min(xs_all) + 0.4 * (max(xs_all) - min(xs_all)))
    else:
        left = int(ex) + 10
    rows = {}
    for y in range(h):
        xs = [x for x in range(left, w) if px[x, y][3] > 0 and (px[x, y][:3] in COAT or px[x, y][:3] in DARK or px[x, y][:3] in LAVA)]
        if xs:
            rows[y] = (min(xs), max(xs))
    if not rows:
        return None
    widest = max(b - a for a, b in rows.values())
    keep = [y for y, (a, b) in rows.items() if b - a >= 0.6 * widest]
    y0, y1 = min(keep), max(keep)
    x0 = min(rows[y][0] for y in keep)
    x1 = max(rows[y][1] for y in keep)
    return x0, y0, x1, y1


def rh(v):
    """round half up: the same in the prototype and the game (Python's and C#'s round() go half to even)"""
    return int(math.floor(v + 0.5))


def line(a, b):
    (x0, y0), (x1, y1) = a, b
    pts = []
    n = max(1, rh(max(abs(x1 - x0), abs(y1 - y0))))
    for i in range(n + 1):
        pts.append((rh(x0 + (x1 - x0) * i / n), rh(y0 + (y1 - y0) * i / n)))
    return pts


def cracks(px, w, h, box):
    x0, y0, x1, y1 = box
    for poly in CRACKS:
        # relative to the torso box, rounded there, then placed: the same cracks wherever the frame sits on its canvas
        pts = [(u * (x1 - x0), v * (y1 - y0)) for u, v in poly]
        path = []
        for a, b in zip(pts, pts[1:]):
            path += [(x0 + px_, y0 + py_) for (px_, py_) in line(a, b)]
        for i, (x, y) in enumerate(path):
            if is_coat(px, x, y, w, h):
                put(px, x, y, LAVA[0] if i % 4 == 0 else LAVA[1], w, h)
                if is_coat(px, x, y + 1, w, h):
                    put(px, x, y + 1, LAVA[2], w, h)


def horn(px, w, h, bx, by, lean):
    pts = [(0, 0), (0, -1), (0, -2), (1, -3), (1, -4), (2, -5), (2, -6), (3, -7)]
    for i, (ox, oy) in enumerate(pts):
        x, y = bx + ox * lean, by + oy
        put(px, x, y, BONE[2] if i < 2 else BONE[0] if i >= 6 else BONE[1], w, h)
        if i < 6:
            put(px, x - lean, y, BONE[2] if i > 3 else BONE[1], w, h)
            if i < 3:
                put(px, x - 2 * lean, y, OUTLINE, w, h)
        put(px, x + lean, y, OUTLINE, w, h)
    put(px, bx + 3 * lean, by - 8, OUTLINE, w, h)

def devil_tail(px, w, h, rx, ry, phase):
    TAILC = (44, 18, 16)
    pts = []
    for i in range(61):
        t = i / 60
        x = rx + rh(12 * t)  # from the edge pixel itself: the tail grows out of the body
        y = min(ry, ry + rh(-14 * t * t + 2.5 * phase * t))  # never under its own root (the sway dipped it)
        if not pts or pts[-1] != (x, y):
            pts.append((x, y))
    # under the root the rump narrows: no row's edge past the root or past the row above (a knob under the tail)
    limit = rx
    for y in range(ry + 1, min(h, ry + 8)):
        r = runs(px, y, w, h)
        if not r:
            break
        e = r[-1][1]
        if e > limit:
            outline = px[e, y]
            for x in range(limit + 1, e + 1):
                px[x, y] = (0, 0, 0, 0)
            px[limit, y] = outline
            e = limit
        limit = min(limit, e)
    for (x, y) in pts:
        put(px, x, y, TAILC, w, h)
    # the underside: one shade pixel under each column's lowest tail pixel, from three columns out, first part only
    low = {}
    for (x, y) in pts:
        low[x] = max(low.get(x, -1), y)
    span = max(low) - rx if low else 0
    for x, y in low.items():
        if rx + 2 <= x <= rx + max(3, span * 4 // 10) and y < ry and 0 <= y + 1 < h and px[x, y + 1][3] == 0:
            put(px, x, y + 1, (70, 26, 20), w, h)
    tx, ty = pts[-1]
    for ox, oy in ((0, -1), (-1, -1), (1, -1), (0, -2), (0, -3)):
        put(px, tx + ox, ty + oy, (200, 40, 30) if oy != -3 else (120, 20, 16), w, h)
    put(px, tx - 1, ty - 2, (120, 20, 16), w, h)
    put(px, tx + 1, ty - 2, (120, 20, 16), w, h)

def hellfire(name, phase=0.0):
    im = load(name)
    w, h = im.size
    px = im.load()
    head_only = "_hed_" in name
    drawn = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0]
    tdx, tdy = min(p[0] for p in drawn) - 16, min(p[1] for p in drawn) - 16  # the tables are relative to a drawn area at (16, 16)
    eye = find_eye(px, w, h)
    special_eyes = EAR_FRAMES.get(name[5:] if name.startswith("0301_") else name, {}).get("eyes")
    if special_eyes:  # the face drawn from the front: its eyes are light pixels, not the white one (turn 4-6)
        se = [(e[0] + tdx, e[1] + tdy) for e in special_eyes]
        eye = (sum(e[0] for e in se) / len(se), sum(e[1] for e in se) / len(se), se)
    root = None
    if not head_only:
        box = torso_box(px, w, h, eye[0] if eye else None)
        if box:
            root = erase_tail_drawn(px, w, h, DARK)
            box = torso_box(px, w, h, eye[0] if eye else None)  # without the tail
            cracks(px, w, h, box)  # before the coat darkens: the cracks sit on coat pixels and keep their own colours
            if root:
                y0 = root[0]
                curve = fill_rump(px, w, h, y0, box[3])
                ry = y0 + (box[3] - y0) // 3  # the tail grows from the upper third of the rump's curve
                rr = runs(px, ry, w, h)
                root = (rr[-1][1], ry) if rr else None  # the row's actual edge (the body may be wider than the curve there)
    key = name[5:] if name.startswith("0301_") else name
    special = EAR_FRAMES.get(key)
    if special:
        erase_boxes(px, w, h, [(a + tdx, b + tdx, c + tdy, d + tdy) for (a, b, c, d) in special["erase"]])
        horns = [(x + tdx, y + tdy, l) for (x, y, l) in special["horns"]]
    else:
        erase_ears(px, w, h, eye[1] if eye else None)
        # the horns from the skull's top, which moves only with the head (the eye pixel animates on its own and is
        # gone in the blinks: the horns jumped or vanished in the idles, 2026-10-19); placed as they were from the eye
        # in the standing frame: skull top right end (31, 25) -> horns at (29, 26) and (34, 27)
        # the horns from the eye, as in ear twitch 2-4 (the user's reference, 2026-10-19): eye + (4, -4) and
        # eye + (9, -3); in a blink (no eye drawn) from the skull's top, calibrated so the standing frame gets the same
        if key in HORN_FRAMES:  # the head mid-turn: the ears (and so the horns) move forward on the face
            horns = [(x + tdx, y + tdy, l) for (x, y, l) in HORN_FRAMES[key]]
        elif eye and not special_eyes:
            ex0, ey0 = rh(eye[0]), rh(eye[1])  # half up, as in the game
            horns = [(ex0 + 4, ey0 - 4, 1), (ex0 + 9, ey0 - 3, 1)]
        else:
            sx, sy = skull_top(px, w, h)
            horns = [(sx - 2, sy + 2, 1), (sx + 3, sy + 3, 1)]
    darken_coat(px, w, h)
    if eye:
        ex, ey, pts = eye
        for (x, y) in pts:
            px[x, y] = (255, 170, 40, 255)
        for (x, y) in pts:
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if opaque(px, nx, ny, w, h) and px[nx, ny][:3] != (255, 170, 40):
                    px[nx, ny] = (220, 60, 20, 255)
    for (hx, hy, lean) in horns:
        # on the head, not above it: the base drops until the head is right under it (the second horn floated in
        # walk 4 and 8 and lips 6, where the skull slopes away, 2026-10-19)
        # rooted in the head: the base and the pixel beside it inside the head, not perched on its edge (ear twitch
        # 2-4 had it so and looked right; the rest sat a row high, 2026-10-19)
        k = 0
        while k < 5 and not (opaque(px, hx, hy, w, h) and opaque(px, hx - lean, hy, w, h)):
            hy += 1
            k += 1
        horn(px, w, h, hx, hy, lean)
    if root:
        devil_tail(px, w, h, root[0], root[1], phase)
        smooth_root(px, w, h, root)
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    rows = [("0301_bdy_static_left", 0.0), ("0301_bdy_walk_left_01", -1.0), ("0301_bdy_walk_left_03", -0.3), ("0301_bdy_walk_left_05", 0.5), ("0301_bdy_walk_left_07", 1.0), ("0301_bdy_graze_left_03", 0.2), ("0301_hed_static_down", 0.0)]
    S = 5
    cell_w = (59 + 2 * PAD) * S
    cell_h = (47 + 2 * PAD) * S
    sheet = Image.new("RGBA", (2 * cell_w + 3 * 8, len(rows) * (cell_h + 8) + 8), (46, 46, 50, 255))
    d = ImageDraw.Draw(sheet)
    y = 8
    for name, phase in rows:
        before = load(name)
        after = hellfire(name, phase)
        for col, im in enumerate((before, after)):
            big = im.resize((im.width * S, im.height * S), Image.NEAREST)
            x = 8 + col * (cell_w + 8)
            sheet.alpha_composite(big, (x + (cell_w - big.width) // 2, y + (cell_h - big.height) // 2))
        d.text((12, y + 4), name.replace("0301_", ""), fill=(255, 255, 255, 255))
        y += cell_h + 8
    sheet.save(os.path.join(OUT, "hellfire_proto.png"))
    frames = []
    for i in range(1, 9):
        im = hellfire("0301_bdy_walk_left_%02d" % i, phase=((i - 1) / 4.0) - 1.0)
        big = im.resize((im.width * 4, im.height * 4), Image.NEAREST)
        bg = Image.new("RGBA", big.size, (46, 46, 50, 255))
        bg.alpha_composite(big)
        frames.append(bg.convert("P", palette=Image.ADAPTIVE))
    frames[0].save(os.path.join(OUT, "hellfire_walk.gif"), save_all=True, append_images=frames[1:], duration=110, loop=0)
    print("wrote hellfire_proto.png and hellfire_walk.gif")



if __name__ == "__main__":
    main()
