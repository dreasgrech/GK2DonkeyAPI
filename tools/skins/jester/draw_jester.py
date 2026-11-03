"""The Jester's pieces, drawn once (docs/sprite-tools.md "Catching what the user caught" 4, 5 and 13:
tools/skins/stamp.py places them into all 44 frames). The third design (2026-11-04), after the user's references (a reenactment fool's
hood in burgundy and navy, a manuscript fool, jesters on donkeys, donkeys in costume) and against our shipped skins'
construction (Royal Parade's cloth edged in its own dark with a light line inside its top, Armored's plates shaded
light to dark): the second design was "stickers" ("like a 5 year old drew it", the user: a third of its pixels
near-black outline, flat pure red and blue, a hat as big as the head, nothing moving with the donkey).

- body.png: the caparison, quartered burgundy and navy, shaded top light to bottom dark with soft folds, edged in each
  colour's own dark, its hem cut into points with two small bells; body@bdy_walk_left_0N.png: the hem's points and
  bells trailing the body's bob;
- neck.png: a collar wrapped round the neck behind the jaw (a curved navy band, its ends tucked into the neck's
  outline) with three hanging points and small bells, moved by stamp with the collar strap; neck@<frame>.png where the
  strap bends (graze 02-05: the side collar posed by the strap's two ends; turn 04-06: a U under the chin) and in the
  walk (the points trailing the neck);
- head.png: the cap on the standing head, a dome with a gold band and two thick points shaded like soft tubes,
  burgundy forward and navy back, a small bell on each, the ears cleared under it; head@bdy_walk_left_0N.png the tips
  trailing the head, head@bdy_ear_left_02..04.png the navy point lifting as the ear flicks under it;
- head@bdy_turn_left_02.png, head@bdy_graze_left_02.png: the side cap posed by the poll and the muzzle;
- head_turn.png, head_front.png (turn 04 as drawn) and head_down.png: the cap's other views.

Colours: the burgundy is Harness, the navy the Second tone; gold only on the band and the bells. The donkey's strap is
tinted the burgundy under the skin ([Base.Harness]).

    python -I tools/skins/jester/draw_jester.py [--into <folder>]   # then: python -I tools/skins/stamp.py build ...

Running it again overwrites the pieces: once they are touched by hand (the template, pixgrid), that is the source.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "donkey"))
from anatomy import body, load, rig
from shapes import curve, polygon, pose, save, side, stroke

hexc = lambda h: (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)
BURG = [hexc(h) for h in ("3A0A14", "641A28", "862534", "A23444", "C2485A")]   # edge, shade, base, lit, light: Harness
NAVY = [hexc(h) for h in ("141A3A", "243266", "34488A", "4E66A8", "6E86C8")]   # the Second tone (Muzzle)
GOLD = [hexc(h) for h in ("5A3A0A", "966414", "E2A830", "FFD652")]             # edge, shade, base, light: as drawn
STANDING = "bdy_static_left"
ERASE = (255, 0, 255, 255)   # skin.cfg's erase colour: clears the donkey under it


# --- drawing: shading like the shipped skins, edges in each colour's own dark, small bells -------------------------

def tube(part, ramp, px):
    """A part shaded like a soft tube lit from above (the cap's points, the collar's band): its top edge light, the
    row under it lit, its underside shade, the rest base."""
    for (x, y) in part:
        if (x, y - 1) not in part:
            c = ramp[4]
        elif (x, y - 2) not in part:
            c = ramp[3]
        elif (x, y + 1) not in part:
            c = ramp[1]
        else:
            c = ramp[2]
        px[(x, y)] = c


def edge(parts, px, keep=()):
    """Each part's outline in its own colour's darkest, where it meets anything that isn't one of the parts (no black
    anywhere: the second design's 34% near-black outline read as stickers). Pixels in 'keep' are left."""
    every = set()
    for part, _ in parts:
        every |= part
    for part, ramp in parts:
        for x, y in part:
            for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if q not in every and q not in keep and q not in px:
                    px[q] = ramp[0]


def small_bell(x, y, px):
    """A bell hanging from (x, y - 1): a 2x2 of gold, its shine top left, edged in dark gold on its sides and below."""
    px[(x, y)], px[(x + 1, y)] = GOLD[3], GOLD[2]
    px[(x, y + 1)], px[(x + 1, y + 1)] = GOLD[2], GOLD[1]
    for q in ((x - 1, y), (x + 2, y), (x - 1, y + 1), (x + 2, y + 1), (x, y + 2), (x + 1, y + 2)):
        px.setdefault(q, GOLD[0])


def lag(prev, now):
    """How a trailing part moves against its frame's move: where the move changed since the frame before, a pixel
    the other way."""
    return max(-1, min(1, prev[0] - now[0])), max(-1, min(1, prev[1] - now[1]))


# --- the cap -------------------------------------------------------------------------------------------------------

def smaller(points, k=0.72):
    """A point's curve drawn shorter: every control point pulled towards its root by k (the second design's points
    made 72% as long: its cap, 27 px wide on an 18-px head, read as "huge")."""
    (x0, y0) = points[0]
    return [points[0]] + [(x0 + (x - x0) * k, y0 + (y - y0) * k) for x, y in points[1:]]


def cap(c, tf=None):
    """The cap from its shapes 'c' (on the frame it is drawn on), each point taken through tf (a pose) first: a dome
    round the skull (a polygon), burgundy in front of the split line and navy behind it, its lowest row a gold band
    along the brim line; a point out of each half, a stroke tapering from c["w"] along a curve, the burgundy forward,
    the navy back; all shaded like soft tubes and edged in their own dark; a small bell at each point's end."""
    tf = tf or (lambda p: p)
    path = lambda ps: [tf(p) for p in ps]
    dome = polygon(path(c["hood"]))
    a, b = tf(c["split"][0]), tf(c["split"][1])
    front = {q for q in dome if side(q, a, b) > 0}
    back = dome - front
    b0, b1 = tf(c["brim"][0]), tf(c["brim"][1])
    blen = math.hypot(b1[0] - b0[0], b1[1] - b0[1])
    # a strip along the brim line (only its lowest pixels, tilted, broke into steps: turn 02, graze 02)
    band = {(x, y) for x, y in dome if 0 <= -side((x, y), b0, b1) / blen <= 1.3 or
            (x, y + 1) not in dome and abs(side((x, y), b0, b1)) / blen <= 1.3}
    red = stroke(curve(path(c["red"])), c["w"], 1.5) - dome
    blue = stroke(curve(path(c["blue"])), c["w"], 1.5) - dome
    px = {}
    tube(red | front, BURG, px)
    tube(blue | back, NAVY, px)
    for x, y in band:
        px[(x, y)] = GOLD[3] if (x - 1, y) not in band else GOLD[1] if (x + 1, y) not in band else GOLD[2]
    edge([(red | front, BURG), (blue | back, NAVY)], px)
    for name in ("red", "blue"):
        x, y = path(c[name])[-1]
        small_bell(int(round(x - 0.5)), int(round(y)) + 1, px)
    return px


def spec(red, blue, **kw):
    """A cap's shapes with its points drawn shorter (smaller())."""
    return dict(kw, red=smaller(red), blue=smaller(blue), w=5.5)


# The side cap on the standing head. The skull's top runs from (30, 36) up the forehead to (36, 31); the ear rises
# from (37-41, 30-35) to its tip at (43, 23) and sweeps, in the ear clip, over x 32-45 and rows 23-32 (stamp.moving):
# all of that is cleared, and the dome stands over the skull from the brow to behind the poll, its band straight
# along row 36 (the eye under it at (34, 38)), its back to x 49 (the head's back showed behind it at 47 in walk 02,
# 03, 06 and 07, where the head moves forward over the neck).
SIDE = spec(hood=[(29, 36.9), (29.8, 34.6), (31.8, 32.4), (34.8, 30.8), (38.5, 30.2), (42.5, 30.8), (46.5, 32),
                  (48.5, 34), (49, 36.9)], brim=[(29, 36.9), (49, 36.9)], split=[(38.5, 0), (38.5, 90)],
            red=[(34, 32.5), (31.5, 23.5), (26.5, 23), (25.5, 28)],
            blue=[(41.5, 32), (44, 21), (49.5, 21.5), (50, 27)])

# Turn 05, the face looking at us: the ears at x 28-33 and 39-45 (cleared), the forehead between them at rows 33-36,
# the eyes at row 41, the face x 31-42 down to the chin at row 53. The dome over the forehead, its band at row 38,
# burgundy on our left and navy on our right, its right end over the strap's top beside it (a wedge of it read as
# one ear poking out).
FRONT = spec(hood=[(29.5, 38.6), (29, 35), (30.6, 32.2), (33.5, 30.6), (36.5, 30.2), (39.5, 30.6), (42.4, 32.2),
                   (45, 34), (46.2, 36.5), (45.8, 38.8), (36.5, 38.3)], brim=[(29.5, 38.6), (45.8, 38.6)],
             split=[(36.5, 0), (36.5, 90)], red=[(33, 33), (30.5, 25), (25, 25), (24.5, 30)],
             blue=[(40, 33), (42.5, 25), (48, 25), (48.5, 30)])

# Turn 03, the face three-quarters to us: the upright ear at x 30-34 (cleared), the far ear lying back along rows
# 33-37 to x 47 (its tip cleared), the eye at (38, 42). Between the side cap (turn 02, posed) and the front one.
TURN = spec(hood=[(27.5, 38.6), (27.4, 35.2), (29.2, 32.4), (32.4, 30.6), (36, 30.2), (40, 30.8), (43.5, 32.8),
                  (45.6, 35.6), (46, 38.6), (36, 38.4)], brim=[(27.5, 38.6), (46, 38.6)], split=[(36, 0), (36, 90)],
            red=[(31.5, 34), (29, 25.5), (23.5, 25.5), (23, 30.5)],
            blue=[(39, 33.5), (41.5, 23.5), (47, 24), (47.5, 29)])

# Graze 04, the head down at the grass, seen from the front and above (the ear upright at x 27-30 cleared, the face
# under the skull's top down to the muzzle at (23, 58)): the front cap tilted 15 degrees forward with the head.
DOWN_TILT = ((36.5, 34), (30.5, 46.5), -15)   # the front cap's middle, where it goes on graze 04, and its turn

# the side cap posed (shapes.pose by the poll and the muzzle): turn 02 and graze 02 tilt the head 7-10 degrees;
# graze 02's a row up and a column back, its band off the eye at (27, 44)
TILTED = {"bdy_turn_left_02": (0, 0), "bdy_graze_left_02": (1, -1)}
VIEW_FRAMES = {STANDING, "bdy_turn_left_03", "bdy_turn_left_05", "bdy_graze_left_04"}   # stamp moves these drawings


def down_cap():
    """The front cap turned and moved onto graze 04 by DOWN_TILT."""
    (sx, sy), (dx, dy), turn = DOWN_TILT
    c, s_ = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    tf = lambda p: (dx + c * (p[0] - sx) - s_ * (p[1] - sy), dy + s_ * (p[0] - sx) + c * (p[1] - sy))
    return cap(FRONT, tf)


def ears_off(px, key, left=9, right=9, outer=None, above=2):
    """A drawing with the donkey's ears cleared above it: on frame 'key' (the one it is drawn on), every pixel 'above'
    rows or more above the poll, from 'left' columns before it to 'right' after (and on the columns 'outer' or more from
    it, down to a row under the poll: the front view's ear bases beside the cap), which the donkey has there or which
    its ear moves to in the frames this drawing is used in (stamp.moving), and which the drawing doesn't cover, takes
    the erase colour. The collar strap's orange and the pixels beside it are never cleared."""
    import stamp
    p = rig(key)["poll"]
    don = load(key).convert("RGBA").load()

    def orange(x, y):   # anatomy.body()'s collar test
        c = don[x, y] if 0 <= x < 96 and 0 <= y < 96 else (0, 0, 0, 0)
        return c[3] and c[0] >= 90 and c[0] > 1.3 * c[1]
    moves = stamp.moving(key) if key in VIEW_FRAMES else {}
    out = dict(px)
    for x in range(p[0] - left, p[0] + right + 1):
        last = p[1] + 1 if outer is not None and abs(x - p[0]) >= outer else p[1] - above
        for y in range(0, last + 1):
            if (x, y) in px or any(orange(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                continue
            if don[x, y][3] or (x, y) in moves:
                out[(x, y)] = ERASE
    return out


def tips_moved(c, d, names=("red", "blue")):
    """A cap's shapes with the named points' ends moved by d (and so their bells)."""
    out = dict(c)
    for name in names:
        out[name] = c[name][:-1] + [(c[name][-1][0] + d[0], c[name][-1][1] + d[1])]
    return out


def posed(key, nudge=(0, 0)):
    """The side cap posed onto a frame whose head tilts: turned about the poll by the turn of the poll-muzzle line,
    then moved by 'nudge'."""
    s, f = rig(STANDING), rig(key)
    tf = pose(s["poll"], s["muzzle"], f["poll"], f["muzzle"])
    return cap(SIDE, lambda p: (tf(p)[0] + nudge[0], tf(p)[1] + nudge[1]))


# --- the collar ----------------------------------------------------------------------------------------------------

# On the standing frame: the neck behind the jaw, from its crest (the strap's top at (45-46, 39), the neck's back
# outline at x 47) round to the throat (the neck's front outline at x 33-35 under the jaw's bottom row 44). The band
# curves round it, bulging towards the shoulder as a ring does seen from the side, and is cut to inside the neck's
# outline, so both its ends tuck into the neck's edges (the second design's collar, a straight bar from the hat to
# the throat, "doesn't wrap around the donkey properly", the user). Three points hang from it, burgundy and navy.
COLLAR = {"line": [(47.5, 39.5), (43, 47), (34.5, 47.5)], "band": 2.5,
          "points": [(0.3, 90), (0.55, 90), (0.8, 95)], "len": 4.5, "w": 4}
FRONT_COLLAR = {"line": [(31, 51.5), (36.5, 56.5), (42, 51.5)], "band": 2.5,
                "points": [(0.2, 105), (0.5, 90), (0.8, 75)], "len": 4, "w": 4}   # turn 05: a short U under the chin


def collar(c, key, tf=None, swing=(0, 0)):
    """The collar from 'c' on frame 'key', its line's points taken through tf (a pose) first: a navy band along the
    curve, shaded like a tube and cut to inside the donkey's outline there, a point hanging from it at each (t along
    the band, angle: 90 straight down, the points' tips moved by 'swing'), burgundy and navy in turn, edged in their
    own dark, a small bell at each tip."""
    tf = tf or (lambda p: p)
    line = curve([tf(p) for p in c["line"]])
    band = stroke(line, c["band"], c["band"])
    don = load(key).convert("RGBA").load()
    solid = lambda x, y: 0 <= x < 96 and 0 <= y < 96 and don[x, y][3] > 0
    inside = lambda x, y: all(solid(x + dx, y + dy) for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)))
    band = {q for q in band if inside(*q)}
    lens = [0.0]
    for (ax, ay), (bx, by) in zip(line, line[1:]):
        lens.append(lens[-1] + math.hypot(bx - ax, by - ay))
    parts, tips = [], []
    for n, (t, ang) in enumerate(c["points"]):
        d = t * lens[-1]
        i = max(j for j in range(len(line) - 1) if lens[j] <= d)
        f = (d - lens[i]) / ((lens[i + 1] - lens[i]) or 1)
        hx = line[i][0] + (line[i + 1][0] - line[i][0]) * f
        hy = line[i][1] + (line[i + 1][1] - line[i][1]) * f
        ux, uy = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        w = c["w"] / 2
        tip = (hx + ux * c["len"] + swing[0], hy + uy * c["len"] + swing[1])
        parts.append((polygon([(hx - uy * w, hy + ux * w), tip, (hx + uy * w, hy - ux * w)]) - band,
                      (BURG, NAVY)[n % 2]))
        tips.append(tip)
    px = {}
    tube(band, NAVY, px)
    for part, ramp in parts:
        for (x, y) in part:   # a point lit on its left, shaded on its right
            px[(x, y)] = ramp[3] if (x - 1, y) not in part else ramp[1] if (x + 1, y) not in part else ramp[2]
    edge([(band, NAVY)] + parts, px)
    for x, y in tips:
        small_bell(int(math.floor(x - 0.5)), int(round(y)) + 1, px)
    return px


def strap_ends(key):
    """The collar strap's two ends on a frame: the middle of its top four rows and the rig's chest point (the breast
    collar's lowest point). The one place this skin still measures the body itself: the rig has no point at the
    strap's top (docs/sprite-tools.md, left open)."""
    don = load(key).convert("RGBA").load()
    strap = [(x, y) for y in range(96) for x in range(96)
             if don[x, y][3] and don[x, y][0] >= 90 and don[x, y][0] > 1.3 * don[x, y][1]]
    first = min(y for x, y in strap)
    top = [(x, y) for x, y in strap if y < first + 4]
    return (sum(x for x, y in top) / len(top), sum(y for x, y in top) / len(top)), tuple(rig(key)["chest"])


def collar_posed(key):
    """The side collar posed onto a frame where the strap bends (the graze): by the strap's two ends there."""
    (st, sc), (ft, fc) = strap_ends(STANDING), strap_ends(key)
    return collar(COLLAR, key, pose(st, sc, ft, fc))


# --- the caparison -------------------------------------------------------------------------------------------------

# On the standing frame: from the withers (x 48, behind the strap) to x 66 over the rump, its top on the back line, its
# hem at row 56 (the belly at 57-58), quartered (the halves split at x 57, the quarters under row 48), lit along the
# top where the back rounds, shaded to the bottom under the belly's curve, two soft folds, the rear column rounding
# away; three points under the hem, counterchanged, and a small bell under two.
CLOTH = {"x0": 48, "x1": 66, "hem": 56, "split": 57, "quarter": 48, "folds": (52, 62), "points": (50, 55, 60),
         "bells": (50, 60)}


def caparison(swing=(0, 0)):
    """The caparison from CLOTH on the standing frame, the hem's points' tips (and their bells) moved by 'swing'."""
    c = CLOTH
    top = body(STANDING, load(STANDING))["top"]

    def ramp_of(x, y):
        front = x < c["split"]
        if c["hem"] >= y > c["quarter"]:
            front = not front
        if y > c["hem"]:
            front = not front
        return BURG if front else NAVY
    cloth = {(x, y) for x in range(c["x0"], c["x1"] + 1) for y in range(top.get(x, 44), c["hem"] + 1)}
    points = []
    for p0 in c["points"]:
        tip = (p0 + 2 + swing[0], c["hem"] + 3 + swing[1])
        points.append(polygon([(p0 - 0.5, c["hem"] + 0.5), (p0 + 4.5, c["hem"] + 0.5), (tip[0] + 0.5, tip[1] + 0.5)]))
    shape = set(cloth)
    for p in points:
        shape |= p
    px = {}
    for (x, y) in cloth:
        r = ramp_of(x, y)
        if any(q not in shape for q in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))):
            px[(x, y)] = r[0]
            continue
        depth = y - top.get(x, 44)
        level = 4 if depth == 1 else 3 if depth <= 3 else 1 if y >= c["hem"] - 1 else 2
        if x in c["folds"] and 2 < depth and level > 1:
            level -= 1
        if x == c["x0"] + 1 and level in (2, 3):
            level = 3
        if x == c["x1"] - 1 and depth > 1:
            level = 1
        if (x == c["split"] or y == c["quarter"] + 1) and depth > 0 and level > 1:
            level -= 1   # the seams
        px[(x, y)] = r[level]
    for p in points:
        for (x, y) in p:
            r = ramp_of(x, y)
            edge_ = any(q not in shape for q in ((x - 1, y), (x + 1, y), (x, y + 1)))
            px[(x, y)] = r[0] if edge_ else r[3] if (x - 2, y) not in p else r[2]
    for b in c["bells"]:
        small_bell(b + 2 + swing[0], c["hem"] + 4 + swing[1], px)
    return px


# --- the frames ----------------------------------------------------------------------------------------------------

def walk():
    """Each walk frame's own drawings, where stamp would place the pieces, their trailing parts a pixel behind the
    frame's move: the cap's tips behind the head, the collar's points behind the neck, the hem's points and bells
    behind the body's bob and the stride's surge (the head's). The second design moved its pieces rigidly ("like a
    static picture", the user)."""
    import stamp
    keys = [k for k in stamp.ORDER if k.startswith("bdy_walk_left_")]
    cols = list(range(CLOTH["x0"], CLOTH["x1"] + 1))
    out = []
    for i, k in enumerate(keys):
        prev = keys[i - 1]
        hd, hp = stamp.head_move(k, STANDING), stamp.head_move(prev, STANDING)
        nd, np_ = stamp.neck_move(k), stamp.neck_move(prev)
        bd, bp = stamp.body_move(k, cols), stamp.body_move(prev, cols)
        shift = lambda px, d: {(x + d[0], y + d[1]): c for (x, y), c in px.items()}
        h = cap(tips_moved(SIDE, lag(hp, hd)), lambda p, d=hd: (p[0] + d[0], p[1] + d[1]))
        out.append((f"head@{k}.png", k, h))
        out.append((f"neck@{k}.png", k, shift(collar(COLLAR, STANDING, swing=lag(np_, nd)), nd)))
        surge = lag(hp, hd)[0]
        out.append((f"body@{k}.png", k, shift(caparison((surge, lag(bp, bd)[1])), bd)))
    return out


def main(argv):
    """The pieces into this folder, or into the folder after --into (a draft: this skin.cfg copied there)."""
    import stamp
    where = HERE
    if "--into" in argv:
        where = os.path.abspath(argv[argv.index("--into") + 1])
        os.makedirs(where, exist_ok=True)
        with open(os.path.join(HERE, "skin.cfg"), encoding="utf-8", newline="") as fh:
            text = fh.read()
        with open(os.path.join(where, "skin.cfg"), "w", encoding="utf-8", newline="") as fh:
            fh.write(text)
    heads = [("head.png", STANDING, cap(SIDE)), ("head_turn.png", "bdy_turn_left_03", cap(TURN)),
             ("head_front.png", "bdy_turn_left_05", cap(FRONT)), ("head_down.png", "bdy_graze_left_04", down_cap())]
    heads += [(f"head@{k}.png", k, posed(k, d)) for k, d in TILTED.items()]
    heads.append(("head@bdy_turn_left_04.png", "bdy_turn_left_04", cap(FRONT)))
    # the ear's flick under the cap lifts its navy point (five frames the same read as frozen)
    heads += [(f"head@bdy_ear_left_0{i}.png", f"bdy_ear_left_0{i}", cap(tips_moved(SIDE, (0, -lift), ("blue",))))
              for i, lift in ((2, 2), (3, 2), (4, 1))]
    moving = walk()
    heads += [(n, k, px) for n, k, px in moving if n.startswith("head@")]
    # how far each clearing reaches: (left, right, outer, above) of the poll
    reach = {"head_down.png": (7, 4, None, 2), "head@bdy_graze_left_02.png": (9, 0, None, 4),
             "head_front.png": (9, 9, 7, 2), "head@bdy_turn_left_04.png": (9, 9, 7, 2),
             "head_turn.png": (9, 12, None, -1)}
    necks = [("neck.png", collar(COLLAR, STANDING))]
    necks += [(f"neck@{k}.png", collar_posed(k)) for k in stamp.ORDER if k.startswith("bdy_graze_left_")
              and k in stamp.STRAP_BENDS]
    necks += [(f"neck@bdy_turn_left_0{i}.png", collar(FRONT_COLLAR, f"bdy_turn_left_0{i}")) for i in (4, 5, 6)]
    necks += [(n, px) for n, k, px in moving if n.startswith("neck@")]
    pieces = [("body.png", caparison())] + [(n, px) for n, k, px in moving if n.startswith("body@")] + necks
    pieces += [(n, ears_off(px, k, *reach.get(n, (9, 9, None, 2)))) for n, k, px in heads]
    for name, px in pieces:
        save(px, os.path.join(where, name))
    print("drew", len(pieces), "pieces")
    made = {name for name, _ in pieces}
    for name in sorted(os.listdir(where)):   # a piece of an earlier run this one no longer draws
        if name.endswith(".png") and name.startswith(("head", "body", "neck")) and name not in made:
            os.remove(os.path.join(where, name))
            print("removed", name)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
