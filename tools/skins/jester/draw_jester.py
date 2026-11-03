"""The Jester's pieces, drawn once (docs/sprite-tools.md "Catching what the user caught" 4, 5 and 13:
tools/skins/stamp.py places them into all 44 frames). The third design (2026-11-03), after the user's references
(a reenactment fool's hood in burgundy and navy, a manuscript fool, jesters on donkeys, donkeys in costume) and against
our shipped skins' construction (Royal Parade's cloth edged in its own dark with a light line inside its top, Armored's
plates shaded light to dark): the second design was "stickers" ("like a 5 year old drew it", the user: a third of its pixels
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
from shapes import curve, pendulum, polygon, pose, save, side, stroke

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


# --- the cap -------------------------------------------------------------------------------------------------------

def brim(shape):
    """A cap's band: the lowest pixel of each column of its body, so it follows the body's curved bottom edge, a
    pixel-art curve whose steps are diagonal; where it drops more than a row between columns, the column between is
    filled down so the band stays whole."""
    low = {}
    for x, y in shape:
        if (x, y + 1) not in shape:
            low[x] = max(low.get(x, y), y)
    band = {(x, y) for x, y in low.items()}
    xs = sorted(low)
    for a, b in zip(xs, xs[1:]):
        ya, yb = low[a], low[b]
        if ya - yb > 1:
            band |= {(a, y) for y in range(yb + 1, ya)}
        elif yb - ya > 1:
            band |= {(b, y) for y in range(ya + 1, yb)}
    return band


def cap(c, tf=None):
    """The cap from its shapes 'c' (on the frame it is drawn on), each point taken through tf (a pose) first: its
    body (a polygon) rising from a band that curves round the skull, its top dipping between its points, burgundy in
    front of the split line and navy behind it; the band its lowest pixels, gold; a point out of each half, a stroke
    tapering from c["w"] along a curve, the burgundy forward, the navy back; all shaded like soft tubes and edged in
    their own dark; a small bell at each point's end. A straight band under a flat-topped dome with two points stuck
    on read as "an anvil on his head" (the user, 2026-11-03)."""
    tf = tf or (lambda p: p)
    path = lambda ps: [tf(p) for p in ps]
    body_ = polygon(path(c["body"]))
    a, b = tf(c["split"][0]), tf(c["split"][1])
    front = {q for q in body_ if side(q, a, b) > 0}
    back = body_ - front
    band = brim(body_)
    red = stroke(curve(path(c["red"])), c["w"], 1.5) - body_
    blue = stroke(curve(path(c["blue"])), c["w"], 1.5) - body_
    px = {}
    tube(red | front, BURG, px)
    tube(blue | back, NAVY, px)
    for x, y in band:
        px[(x, y)] = GOLD[3] if (x - 1, y) not in band and (x - 1, y - 1) not in band else GOLD[2]
    for x, y in band:   # the band's underside a step darker where it turns down: it rounds with the head
        if (x + 1, y + 1) in band or (x - 1, y + 1) in band:
            px[(x, y)] = GOLD[1] if px[(x, y)] == GOLD[2] else px[(x, y)]
    edge([(red | front, BURG), (blue | back, NAVY)], px)
    for name in ("red", "blue"):
        x, y = path(c[name])[-1]
        small_bell(int(round(x - 0.5)), int(round(y)) + 1, px)
    return px


# The side cap on the standing head. The skull's top runs from (30, 36) up the forehead to (36, 31); the ear rises
# from (37-41, 30-35) to its tip at (43, 23) and sweeps, in the ear clip, over x 32-45 and rows 23-32 (stamp.moving):
# all of that is cleared. The band curves round the skull: across the brow just over the eye (34, 38), lowest behind
# the eye over the temple, rising round the back of the head to the nape (x 48, row 35); the cap's body rises from it
# and dips between its points, the burgundy one forward over the brow, the navy one back; its back reaches x 48-49
# (the head's back showed behind it at 47 in walk 02, 03, 06 and 07, where the head moves forward over the neck).
SIDE = {"body": [(29, 36.8), (29.4, 34.8), (30.8, 32.6), (33, 31), (35.6, 30.6), (37.6, 31.6), (38.6, 32.4),
                 (40, 31.4), (42.5, 30.2), (45.5, 30.2), (47.4, 31.6), (48.6, 33), (48.8, 34.6), (46, 36.4),
                 (42, 38.3), (38.5, 38.3), (35.5, 37.2), (33, 36.7)],
        "split": [(38.6, 0), (38.6, 90)], "w": 6,
        "red": [(33.5, 31.5), (31.5, 25.5), (27, 25.5), (26, 30.5)],
        "blue": [(44, 30.5), (46, 24.5), (50.5, 25), (51, 29.5)]}

# Turn 05, the face looking at us: the ears at x 28-33 and 39-45 (cleared), the forehead between them at rows 33-36,
# the eyes at row 41, the face x 31-42 down to the chin at row 53. The band a U round the forehead, lowest over its
# middle (row 39, the eyes two rows under it), up at both sides; the body dipping between its points, burgundy on our
# left and navy on our right; its right end over the strap's top beside it (a wedge of it read as one ear poking out).
FRONT = {"body": [(29.5, 37.2), (29, 35.5), (30.5, 32.6), (33, 30.8), (35.5, 31), (36.5, 32), (37.5, 31), (40, 30.8),
                  (42.5, 32.6), (44.5, 35), (46.2, 36.2), (45.8, 37.2), (42, 38.7), (38.5, 39.8), (35, 39.8),
                  (31.5, 38.7)],
         "split": [(36.5, 0), (36.5, 90)], "w": 6,
         "red": [(33, 32), (31, 26.5), (27, 26.5), (26, 30.5)],
         "blue": [(40, 32), (42, 26.5), (46, 26.5), (47, 30.5)]}

# Turn 03, the face three-quarters to us: the upright ear at x 30-34 (cleared), the far ear lying back along rows
# 33-37 to x 47 (its tip cleared), the eye at (38, 42). Between the side cap (turn 02, posed) and the front one: the
# band curving round the forehead, lowest a little left of the middle (the face turned to us), up to the back.
TURN = {"body": [(27.5, 37.6), (27.4, 35.4), (29, 32.6), (31.5, 30.8), (34, 31), (35.5, 32), (37, 31), (40, 30.6),
                 (43.5, 32.4), (45.6, 35.4), (46, 37), (42, 38.7), (37, 39.8), (32, 39.4)],
        "split": [(35.5, 0), (35.5, 90)], "w": 6,
        "red": [(31.5, 32.5), (29.5, 26.5), (25, 26.5), (24, 30.5)],
        "blue": [(39.5, 32), (42, 26), (46.5, 26.5), (47.5, 30.5)]}

# Graze 04, the head down at the grass, seen from the front and above (the ear upright at x 27-30 cleared, the face
# under the skull's top down to the muzzle at (23, 58)): the front cap tilted 15 degrees forward with the head.
DOWN_TILT = ((36.5, 34), (30.5, 46.5), -15)   # the front cap's middle, where it goes on graze 04, and its turn

# the side cap posed (shapes.pose by the poll and the muzzle): turn 02 and graze 02 tilt the head 7-10 degrees;
# graze 02's a row up and a column back, its band off the eye at (27, 44)
TILTED = {"bdy_turn_left_02": (0, 0), "bdy_graze_left_02": (1, -1)}
VIEW_FRAMES = {STANDING, "bdy_turn_left_03", "bdy_turn_left_05", "bdy_graze_left_04"}   # stamp moves these drawings


def down_cap(moves=None):
    """The front cap turned and moved onto graze 04 by DOWN_TILT, its points' tips moved by 'moves' first."""
    (sx, sy), (dx, dy), turn = DOWN_TILT
    c, s_ = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    tf = lambda p: (dx + c * (p[0] - sx) - s_ * (p[1] - sy), dy + s_ * (p[0] - sx) + c * (p[1] - sy))
    return cap(tips_moved(FRONT, moves or {}), tf)


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


def tips_moved(c, moves):
    """A cap's shapes with each named point's end moved by its own (dx, dy) (and so its bell): {"red": d, ...}. The
    point bends with its bell: its middle control points take half the move."""
    out = dict(c)
    for name, d in moves.items():
        pts = c[name]
        out[name] = [pts[0]] + [(x + d[0] / 2, y + d[1] / 2) for x, y in pts[1:-1]] + [(pts[-1][0] + d[0],
                                                                                        pts[-1][1] + d[1])]
    return out


def posed(key, nudge=(0, 0), c=None):
    """The side cap (or its shapes 'c') posed onto a frame whose head tilts: turned about the poll by the turn of the
    poll-muzzle line, then moved by 'nudge'."""
    s, f = rig(STANDING), rig(key)
    tf = pose(s["poll"], s["muzzle"], f["poll"], f["muzzle"])
    return cap(c or SIDE, lambda p: (tf(p)[0] + nudge[0], tf(p)[1] + nudge[1]))


# --- the collar ----------------------------------------------------------------------------------------------------

# On the standing frame: laid along the collar strap's upper half (the strap from (45-46, 39) at the neck's crest down
# to (40-43, 47), 4 px wide; it goes on to the chest point (36, 52)), covering it, cut to inside the neck's outline:
# the strap is the donkey's own collar, fitted to the neck in every frame, and stamp moves this with it. A band round
# the throat across it read as a second strap crossing the first ("the harness needs to be fitted better", the user;
# before that, a straight bar from the hat to the throat "doesn't wrap around the donkey properly"). The strap goes on
# under it to the chest, tinted the burgundy. Three points hang from it over the shoulder, burgundy and navy.
COLLAR = {"line": [(45.4, 40.6), (44.6, 42.6), (43, 45.5), (40.6, 48.2)], "band": 4.5,
          "points": [(0.35, 80), (0.65, 85), (0.95, 95)], "len": 4.5, "w": 4}
FRONT_COLLAR = {"line": [(31, 51.5), (36.5, 56.5), (42, 51.5)], "band": 2.5,
                "points": [(0.2, 105), (0.5, 90), (0.8, 75)], "len": 4, "w": 4}   # turn 05: a short U under the chin


def collar(c, key, tf=None, swings=None):
    """The collar from 'c' on frame 'key', its line's points taken through tf (a pose) first: a navy band along the
    curve, shaded like a tube and cut to inside the donkey's outline there, a point hanging from it at each (t along
    the band, angle: 90 straight down, each point's tip moved by its own of 'swings'), burgundy and navy in turn, edged
    in their own dark, a small bell at each tip."""
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
        sw = swings[n] if swings else (0, 0)
        tip = (hx + ux * c["len"] + sw[0], hy + uy * c["len"] + sw[1])
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


def collar_posed(key, swings=None):
    """The side collar posed onto a frame where the strap bends (the graze): by the strap's two ends there."""
    (st, sc), (ft, fc) = strap_ends(STANDING), strap_ends(key)
    return collar(COLLAR, key, pose(st, sc, ft, fc), swings)


# --- the caparison -------------------------------------------------------------------------------------------------

# On the standing frame: from the withers (x 48, behind the strap) to x 66 over the rump, its top on the back line, its
# hem at row 56 (the belly at 57-58), quartered (the halves split at x 57, the quarters under row 48), lit along the
# top where the back rounds, shaded to the bottom under the belly's curve, two soft folds, the rear column rounding
# away; three points under the hem, counterchanged, and a small bell under two.
CLOTH = {"x0": 48, "x1": 66, "hem": 56, "split": 57, "quarter": 48, "folds": (52, 62), "points": (50, 55, 60),
         "bells": (50, 60)}


SWING_FROM = 50   # the cloth's rows from this one down swing with the stride; above, it lies on the back


def caparison(sway=0, ripple=None):
    """The caparison from CLOTH on the standing frame, its lower part swung across by 'sway' (its rows from
    SWING_FROM down, the lower the more, so its folds and its corners move too) and each of the hem's points' tips
    moved by its own of 'ripple', a bell with each of two ("make the cloth draped on his back a bit less staticey",
    the user: only the hem's points had moved, a pixel)."""
    c = CLOTH
    shear = lambda y: int(round(sway * (y - SWING_FROM + 1) / (c["hem"] - SWING_FROM + 1))) if y >= SWING_FROM else 0
    ripple = ripple or [(0, 0)] * len(c["points"])
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
    for p0, sw in zip(c["points"], ripple):
        tip = (p0 + 2 + sw[0], c["hem"] + 3 + sw[1])
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
        sw = ripple[c["points"].index(b)]
        small_bell(b + 2 + sw[0], c["hem"] + 4 + sw[1], px)
    # the swing: each row from SWING_FROM down moved across (the points and bells under the hem by the hem's own)
    return {(x + shear(min(y, c["hem"])), y): col for (x, y), col in px.items()}


# --- the frames ----------------------------------------------------------------------------------------------------

def walk():
    """Each walk frame's own drawings, where stamp would place the pieces (the head's, the strap's and the body's
    moves), every hanging part swinging on its own pendulum over the stride, each a little behind the one before: the
    cap's points (the burgundy, then the navy), the collar's three points, the cloth's lower part and its hem's
    points. The bells hang from those tips, so they swing through five places. The second design moved its pieces
    rigidly ("like a static picture", the user), then its tips a pixel each way ("two different positions")."""
    import stamp
    keys = [k for k in stamp.ORDER if k.startswith("bdy_walk_left_")]
    cols = list(range(CLOTH["x0"], CLOTH["x1"] + 1))
    shift = lambda px, d: {(x + d[0], y + d[1]): c for (x, y), c in px.items()}
    out = []
    for i, k in enumerate(keys):
        hd, nd, bd = stamp.head_move(k, STANDING), stamp.neck_move(k), stamp.body_move(k, cols)
        tips = {"red": pendulum(i, -1), "blue": pendulum(i, -2)}
        out.append((f"head@{k}.png", k, cap(tips_moved(SIDE, tips), lambda p, d=hd: (p[0] + d[0], p[1] + d[1]))))
        swings = [pendulum(i, -1 - n) for n in range(len(COLLAR["points"]))]
        out.append((f"neck@{k}.png", k, shift(collar(COLLAR, STANDING, swings=swings), nd)))
        ripple = [pendulum(i, -2 - n) for n in range(len(CLOTH["points"]))]
        out.append((f"body@{k}.png", k, shift(caparison(pendulum(i, -2, 1)[0], ripple), bd)))
    return out


# The other clips, frame by frame: each hanging part's offset (a pixel a step, back to its start at the loop). Ear: the
# ear flicks under the cap in 02-04, its navy point kicked up and settling, the burgundy one jiggling with it. Chew:
# the jaw bobs the cap's bells and the collar's points in 02. Graze: the head goes down in 02-04, the cap's tips and
# the collar's points trailing up and back, then settling, the hem stirred after. Turn: the head swings round to us
# in 02-04, the tips and the points trailing it. Lips (13 frames, the long idle): every bell on a slow pendulum.
Z = (0, 0)
CLIP_MOTION = {
    "ear": {"red": [Z, Z, (1, 0), Z, Z], "blue": [Z, (1, -1), (1, -2), (0, -1), (-1, 0)]},
    "chew": {"red": [Z, (0, 1), Z], "blue": [Z, (0, 1), Z],
             "collar": [[Z, (0, 1), Z], [Z, (1, 1), Z], [Z, (0, 1), Z]]},
    "graze": {"red": [Z, (1, -1), (2, -1), (1, 0), Z], "blue": [Z, (1, 0), (2, -1), (2, -1), (1, 0)],
              "collar": [[Z, (1, 0), (1, -1), Z, (-1, 0)], [Z, Z, (1, 0), (1, -1), Z], [Z, Z, Z, (1, 0), (1, -1)]],
              "hem": [[Z, Z, (1, 0), Z, Z], [Z, Z, Z, (1, 0), Z], [Z, Z, Z, Z, (1, 0)]]},
    "turn": {"red": [Z, (1, 0), (2, -1), (1, 0), Z, (-1, 0)], "blue": [Z, Z, (1, 0), (2, -1), (1, 0), Z],
             "collar": [[Z, Z, (1, 0), (1, 0), Z, Z], [Z, Z, Z, (1, 0), (1, 0), Z], [Z, Z, Z, Z, (1, 0), (1, 0)]]},
    "lips": {"red": [pendulum(i, 0, 1, 13) for i in range(13)], "blue": [pendulum(i, -3, 1, 13) for i in range(13)],
             "collar": [[pendulum(i, -2 - 2 * n, 1, 13) for i in range(13)] for n in range(3)],
             "hem": [[pendulum(i, -3 - 2 * n, 1, 13) for i in range(13)] for n in range(3)]},
}
FRONT_VIEW = ("bdy_turn_left_04", "bdy_turn_left_05", "bdy_turn_left_06")
DOWN_VIEW = ("bdy_graze_left_03", "bdy_graze_left_04", "bdy_graze_left_05")


def clips():
    """The clips' own drawings for every frame in CLIP_MOTION, each piece where stamp would place it there (the cap
    by its view, moved with the ear bases; the collar with the strap, posed where it bends, the U in the front view;
    the cloth with the body) with its parts moved by the table. A piece whose parts all stay is left to stamp."""
    import stamp
    shift = lambda px, d: {(x + d[0], y + d[1]): c for (x, y), c in px.items()}
    cols = list(range(CLOTH["x0"], CLOTH["x1"] + 1))
    out = []
    for clip, m in CLIP_MOTION.items():
        keys = [k for k in stamp.ORDER if k.startswith(f"bdy_{clip}_left_")]
        for i, k in enumerate(keys):
            tips = {name: m[name][i] for name in ("red", "blue")}
            if any(tips.values()) or k in TILTED or k == "bdy_turn_left_04":
                if k in TILTED:
                    h = posed(k, TILTED[k], tips_moved(SIDE, tips))
                elif k == "bdy_turn_left_03":
                    h = cap(tips_moved(TURN, tips))
                elif k in FRONT_VIEW:
                    d = (0, 0) if k == "bdy_turn_left_04" else stamp.head_move(k, "bdy_turn_left_05")
                    h = shift(cap(tips_moved(FRONT, tips)), d)
                elif k in DOWN_VIEW:
                    h = shift(down_cap(tips), stamp.head_move(k, "bdy_graze_left_04"))
                else:
                    h = shift(cap(tips_moved(SIDE, tips)), stamp.head_move(k, STANDING))
                out.append((f"head@{k}.png", k, h))
            swings = [p[i] for p in m["collar"]] if "collar" in m else None
            if swings and any(swings):
                if k in FRONT_VIEW:
                    n = collar(FRONT_COLLAR, k, swings=swings)
                elif k in stamp.STRAP_BENDS:
                    n = collar_posed(k, swings)
                else:
                    n = shift(collar(COLLAR, STANDING, swings=swings), stamp.neck_move(k))
                out.append((f"neck@{k}.png", k, n))
            ripple = [p[i] for p in m["hem"]] if "hem" in m else None
            if ripple and any(ripple):
                out.append((f"body@{k}.png", k, shift(caparison(0, ripple), stamp.body_move(k, cols))))
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
    heads += [(f"head@bdy_ear_left_0{i}.png", f"bdy_ear_left_0{i}", cap(tips_moved(SIDE, {"blue": (0, -lift)})))
              for i, lift in ((2, 2), (3, 2), (4, 1))]
    moving = walk() + clips()
    own = {n for n, k, px in moving if n.startswith("head@")}
    heads = [h for h in heads if h[0] not in own] + [(n, k, px) for n, k, px in moving if n.startswith("head@")]
    # how far each clearing reaches: (left, right, outer, above) of the poll
    reach = {"head_down.png": (7, 4, None, 2), "head@bdy_graze_left_02.png": (9, 0, None, 4),
             "head_front.png": (9, 9, 7, 2), "head@bdy_turn_left_04.png": (9, 9, 7, 2),
             "head_turn.png": (9, 12, None, -1)}
    necks = [("neck.png", collar(COLLAR, STANDING))]
    necks += [(f"neck@{k}.png", collar_posed(k)) for k in stamp.ORDER if k.startswith("bdy_graze_left_")
              and k in stamp.STRAP_BENDS]
    necks += [(f"neck@bdy_turn_left_0{i}.png", collar(FRONT_COLLAR, f"bdy_turn_left_0{i}")) for i in (4, 5, 6)]
    own = {n for n, k, px in moving if n.startswith("neck@")}
    necks = [n for n in necks if n[0] not in own] + [(n, px) for n, k, px in moving if n.startswith("neck@")]
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
