"""The Jester's pieces, drawn once (docs/sprite-tools.md "Catching what the user caught" 4, 5 and 13:
tools/skins/stamp.py places them into all 44 frames). The third design (2026-11-03), after the user's references
(a reenactment fool's hood in burgundy and navy, a manuscript fool, jesters on donkeys, donkeys in costume) and against
our shipped skins' construction (Royal Parade's cloth edged in its own dark with a light line inside its top, Armored's
plates shaded light to dark): the second design was "stickers" ("like a 5 year old drew it", the user: a third of its pixels
near-black outline, flat pure red and blue, a hat as big as the head, nothing moving with the donkey).

- body.png: the caparison, chequered burgundy and navy (three columns, two rows), shaded top light to bottom dark with
  soft folds, edged in each colour's own dark, its hem cut into points with small bells, each point the other colour
  of the check above it; body@bdy_walk_left_0N.png: the hem's points and
  bells trailing the body's bob;
- neck.png: a collar wrapped round the neck behind the jaw (a curved navy band, its ends tucked into the neck's
  outline) with a hanging point and a small bell, moved by stamp with the collar strap; neck@<frame>.png where the
  strap bends (graze 02-05: the side collar posed by the strap's two ends; turn 04-06: a U under the chin) and in the
  walk (the points trailing the neck);
- head.png: the cap on the standing head, a dome with a gold band and two thick points shaded like soft tubes,
  burgundy forward and navy back, a small bell on each, the ears cleared under it; head@bdy_walk_left_0N.png the tips
  trailing the head, head@bdy_ear_left_02..04.png the navy point lifting as the ear flicks under it;
- head@bdy_turn_left_02.png, head@bdy_graze_left_02.png: the side cap posed by the poll and the muzzle;
- head_turn.png, head_front.png (turn 04 as drawn) and head_down.png: the cap's other views.

Colours: the burgundy and the navy are the skin's two tones (skin.cfg [Tone.motley1], [Tone.motley2]; each its own row
in the colours window); gold only on the band and the bells. The donkey's strap is tinted the burgundy under
the skin ([Base.Harness]), and the Harness row reaches it only.

    python -I tools/skins/jester/draw_jester.py [--into <folder>]   # then: python -I tools/skins/stamp.py build ...

Running it again overwrites the pieces: once they are touched by hand (the template, pixgrid), that is the source.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "donkey"))
from anatomy import body, load, rig
from shapes import curve, pendulum, polygon, pose, save, side, stroke

hexc = lambda h: (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)
# The motley's burgundy and navy (edge, shade, base, lit, light), each part its own shades of them: every part is a
# tone of its own (skin.cfg; the user, 2026-11-04: "yes so that we start making use of our new multi colour tone
# system"), and a colour is one tone's only. Found by tools/skins/part_palette.py: the caparison's as before, the cap's
# and the collar's one to four steps off, each in colour-cube cells of their own (the same burgundy and navy to the eye).
CLOTH_BURG = [hexc(h) for h in ("3A0A14", "641A28", "862534", "A23444", "C2485A")]    # the tone caparison
CLOTH_NAVY = [hexc(h) for h in ("141A3A", "243266", "34488A", "4E66A8", "6E86C8")]    # the tone caparison-second
CAP_BURG = [hexc(h) for h in ("3E0A14", "641A2A", "862633", "A03444", "C14859")]      # the tone cap-front: its front point and half
CAP_NAVY = [hexc(h) for h in ("131A39", "263266", "36488A", "4F66A7", "7086C8")]      # the tone cap-back
COLLAR_BURG = [hexc(h) for h in ("3A0D14", "671A28", "882534", "A23644", "C1485B")]   # the tone collar: its band and middle point
COLLAR_NAVY = [hexc(h) for h in ("151A39", "243262", "34488C", "4D67A8", "6E88C8")]   # the tone collar-points: the two outer ones
GOLD = [hexc(h) for h in ("5A3A0A", "966414", "E2A830", "FFD652")]             # edge, shade, base, light: the tone bells
BAND = [hexc(h) for h in ("5A3A08", "996414", "E3A82F", "FED653")]             # the tone cap-band (its edge unused): a step off the bells'
#   (tools/skins/part_palette.py), the cap's band and the bells two parts of their own
STANDING = "bdy_static_left"
ERASE = (255, 0, 255, 255)   # skin.cfg's erase colour: clears the donkey under it


# --- drawing: shading like the shipped skins, edges in each colour's own dark, small bells -------------------------

def hem_point(p0, hem, sw=(0, 0)):
    """A hem point hanging from x p0 .. p0 + 4 under row 'hem': its shape at rest (a triangle 5 wide, 3 deep), each
    row moved across by its share of the swing's sw[0] (none at the hem, all of it at the tip). Drawn again as a
    triangle to the swung tip, a point lost pixels and changed shape from frame to frame (dark nubs in walk 05, short
    points in lips 06-09: a fresh review, 2026-11-03), and the swing's lift (sw[1]) shortened it to nubs: not used.
    {pixel: its shade's place in a ramp}, shaded at rest (its outline 0, its lit left 3, the rest 2) and kept through
    the shear: shaded after it, its middle row turned all outline where the tip moved a column (walk 05)."""
    rest = polygon([(p0 - 0.5, hem + 0.5), (p0 + 4.5, hem + 0.5), (p0 + 2.5, hem + 3.5)])
    above = {(x, hem) for x in range(p0 - 1, p0 + 6)}
    shade = {}
    for x, y in rest:
        if any(q not in rest and q not in above for q in ((x - 1, y), (x + 1, y), (x, y + 1))):
            shade[(x, y)] = 0
        else:
            shade[(x, y)] = 3 if (x - 2, y) not in rest else 2
    return {(x + int(round(sw[0] * (y - hem) / 3)), y): v for (x, y), v in shade.items()}


def tube(part, ramp, px, solid=None):
    """A part shaded like a soft tube lit from above (the cap's points, the collar's band): its top edge light, the
    row under it lit, its underside shade, the rest base. 'solid': what counts as below it too (the collar's points
    under its band: shaded as the band's underside, their tops read as a dark bar across them)."""
    solid = solid or part
    for (x, y) in part:
        if (x, y - 1) not in part:
            c = ramp[4]
        elif (x, y - 2) not in part:
            c = ramp[3]
        elif (x, y + 1) not in solid:
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
    tube(red | front, CAP_BURG, px)
    tube(blue | back, CAP_NAVY, px)
    for x, y in band:
        px[(x, y)] = BAND[3] if (x - 1, y) not in band and (x - 1, y - 1) not in band else BAND[2]
    for x, y in band:   # the band's underside a step darker where it turns down: it rounds with the head
        if (x + 1, y + 1) in band or (x - 1, y + 1) in band:
            px[(x, y)] = BAND[1] if px[(x, y)] == BAND[2] else px[(x, y)]
    edge([(red | front, CAP_BURG), (blue | back, CAP_NAVY)], px)
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

# Every view's cap made smaller: SMALLER as tall, its points SMALLER as long and thick, and the side cap SMALLER as long
# front to back too, about the brow: drawn as above, its back reached 3-8 px past the head's back at the poll (rows
# 34-36, the band running on past the head: "the jester hat is too big from the back and that exceeds the donkeys
# head from the back, so i think it needs to be scaled down", the user's marked screenshot, 2026-11-03). The front
# and three-quarter caps keep their width (the head's; the three-quarter one 0.9 as wide left the far ear showing).
SMALLER = 0.8


def smaller(c, anchor, kx):
    """A cap's shapes made smaller about 'anchor': its body SMALLER as tall and kx as wide, each point's base moved
    with it and the point SMALLER as long about its base, as thick."""
    ax, ay = anchor
    at = lambda q: (ax + (q[0] - ax) * kx, ay + (q[1] - ay) * SMALLER)
    out = dict(c, body=[at(q) for q in c["body"]], w=c["w"] * SMALLER,
               split=[(at(q)[0], q[1]) for q in c["split"]])
    for name in ("red", "blue"):
        (bx, by), (nx, ny) = c[name][0], at(c[name][0])
        out[name] = [(nx + (x - bx) * SMALLER, ny + (y - by) * SMALLER) for x, y in c[name]]
    return out


SIDE = smaller(SIDE, (29.2, 37), SMALLER)    # the brow stays, the back comes in to the head's back
# Its back ends at the poll (41, 34): the band's back end at x 42, the navy half's back edge down from the crown at
# x 42, and the navy point rising from the crown (x 39.5) and hanging back over the ears, its bell at x 43-44, rows
# 31-34, well above the collar strap's top (45, 39). Scaled, the back had run on to x 45-47, against the strap: "cant
# u see they both extend to the harness at the back?" (the user, 2026-11-03; a corner taken to the nape at x 45.6
# before that made it worse). Behind it the head's back (x 42-46, rows 35-38) shows, then the strap.
SIDE["body"] = [(29.0, 36.8), (29.4, 35.2), (30.5, 33.5), (32.2, 32.2), (34.3, 31.9), (35.9, 32.7), (36.7, 33.3),
                (37.8, 32.5), (39.8, 31.6), (41.2, 32.3), (41.9, 33.8), (41.9, 35.6), (41.2, 37.0), (39.4, 38.0),
                (36.6, 38.0), (34.2, 37.2), (32.2, 36.8)]
SIDE["blue"] = [(39.5, 32.0), (40.5, 26.8), (43.6, 27.0), (43.9, 30.4)]
# Then narrower and over the head's middle (the user: "ok getting better so now align its center better but also make
# it a bit smaller width wise even"): cut at the poll, its middle (35.45) sat 2 px before the middle of the head's top
# (the forehead at x 30 to the head's back at 45, row 36), the burgundy point far over the face. NARROW as long front
# to back about its middle, then SHIFT columns back: the body x 32-43 (11 px, was 13), the band's front end off the
# brow, the navy bell over the ears at x 44-45, above the strap's top (45, 39).
NARROW, SHIFT, MIDDLE = 0.85, 2, 35.45


def narrowed(c):
    """The side cap's shapes NARROW as long about MIDDLE, moved SHIFT columns back (their rows kept)."""
    at = lambda q: (MIDDLE + (q[0] - MIDDLE) * NARROW + SHIFT, q[1])
    return dict(c, body=[at(q) for q in c["body"]], red=[at(q) for q in c["red"]], blue=[at(q) for q in c["blue"]],
                split=[at(q) for q in c["split"]])


SIDE = narrowed(SIDE)

# Then into the box the user marked (orange, over a walk frame of the page's card, the cap as it was in blue: "your
# hat is aligned in the blue box but im thinking more around the orange box"): on the standing frame x 32-44.5, rows
# 25-35.5, where the cap with its bells took x 28-46, rows 26-38. FIT_FROM is that box, FIT_TO the user's: every point
# of the cap moved and scaled from one to the other, its points' thickness with it; the bells stay 2 px.
FIT_FROM, FIT_TO = ((28, 26), (46, 38)), ((32, 25), (44.5, 35.5))


def fitted(c):
    """The side cap's shapes taken from the box FIT_FROM into FIT_TO (each axis on its own)."""
    (fx0, fy0), (fx1, fy1) = FIT_FROM
    (tx0, ty0), (tx1, ty1) = FIT_TO
    kx, ky = (tx1 - tx0) / (fx1 - fx0), (ty1 - ty0) / (fy1 - fy0)
    at = lambda q: (tx0 + (q[0] - fx0) * kx, ty0 + (q[1] - fy0) * ky)
    return dict(c, body=[at(q) for q in c["body"]], red=[at(q) for q in c["red"]], blue=[at(q) for q in c["blue"]],
                split=[(at(q)[0], q[1]) for q in c["split"]], w=c["w"] * (kx + ky) / 2)


SIDE = fitted(SIDE)

# Where each view's cap is put by hand (tools/skins/jester/place.py: an Aseprite file with the cap on a layer of its own
# on each view's frame, moved or scaled there and saved): per view ("side", "turn", "front", "down"), a move (dx, dy)
# in the frame's pixels and a scale (sx, sy) about the cap's middle, on top of the shapes above; the cap is drawn
# again there from its shapes, so a scale stays clean pixel art. The side cap's move reaches every frame drawn from
# it (the walk, ear, chew and lips, turn 02 and graze 02). A view not in the file is as drawn.
PLACEMENT = os.path.join(HERE, "placement.json")


def placement(view):
    """The view's hand placement: {"dx", "dy", "sx", "sy"} (missing keys: no move, no scale)."""
    try:
        p = json.load(open(PLACEMENT, encoding="utf-8")).get(view) or {}
    except FileNotFoundError:
        p = {}
    return p.get("dx", 0), p.get("dy", 0), p.get("sx", 1), p.get("sy", 1)


def placing(view, pts):
    """The function that places points by the view's hand placement, about the middle of 'pts' (the cap's points)."""
    dx, dy, sx, sy = placement(view)
    xs, ys = [q[0] for q in pts], [q[1] for q in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    return lambda q: (cx + (q[0] - cx) * sx + dx, cy + (q[1] - cy) * sy + dy)


def placed(c, view):
    """A cap's shapes placed by the view's hand placement (its points' thickness scaled with it)."""
    at = placing(view, c["body"] + c["red"] + c["blue"])
    sx, sy = placement(view)[2:]
    return dict(c, body=[at(q) for q in c["body"]], red=[at(q) for q in c["red"]], blue=[at(q) for q in c["blue"]],
                split=[(at(q)[0], q[1]) for q in c["split"]], w=c["w"] * (sx + sy) / 2)


SIDE = placed(SIDE, "side")
TURN = placed(TURN, "turn")
FRONT = placed(FRONT, "front")
TURN = smaller(TURN, (37, 39.8), 1.0)        # its band's lowest middle; as wide (narrower, the far ear lying back showed)
# the graze cap, the front one 0.65 as wide: the lowered head is seen from the side and above, its ear bases 4 px
# apart (11 facing us), and as wide as the front cap it reached 12 px behind the poll, over the neck and the collar
# strap's loop (a fresh review of the smaller cap: "the band running on behind the head" still read there)
DOWN = smaller(FRONT, (36.75, 39.8), 0.65)
FRONT = smaller(FRONT, (36.75, 39.8), 1.0)   # its band's lowest middle, the band as wide as the head


# Graze 04, the head down at the grass, seen from the front and above (the ear upright at x 27-30 cleared, the face
# under the skull's top down to the muzzle at (23, 58)): the graze cap tilted 15 degrees forward with the head, its
# middle over the skull (the poll at (30, 45), the ear bases at x 28 and 32), two columns before the poll.
DOWN_TILT = ((36.5, 34), (28.5, 46.5), -15)   # the graze cap's middle, where it goes on graze 04, and its turn

# the side cap posed (shapes.pose by the poll and the muzzle): turn 02 and graze 02 tilt the head 7-10 degrees;
# graze 02's a row up and a column back, its band off the eye at (27, 44)
TILTED = {"bdy_turn_left_02": (0, 0), "bdy_graze_left_02": (1, -1)}
VIEW_FRAMES = {STANDING, "bdy_turn_left_03", "bdy_turn_left_05", "bdy_graze_left_04"}   # stamp moves these drawings


def down_cap(moves=None):
    """The graze cap (DOWN) turned and moved onto graze 04 by DOWN_TILT, its points' tips moved by 'moves' first."""
    (sx, sy), (dx, dy), turn = DOWN_TILT
    c, s_ = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    tf = lambda p: (dx + c * (p[0] - sx) - s_ * (p[1] - sy), dy + s_ * (p[0] - sx) + c * (p[1] - sy))
    shapes = tips_moved(DOWN, moves or {})
    # the hand placement in graze 04's own pixels, after the tilt (place.py's "down" frame)
    at = placing("down", [tf(q) for q in DOWN["body"] + DOWN["red"] + DOWN["blue"]])
    sx_, sy_ = placement("down")[2:]
    return cap(dict(shapes, w=shapes["w"] * (sx_ + sy_) / 2), lambda p: at(tf(p)))


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
# Two bold points, fanned (70 and 118 degrees, 8 long, 5.5 wide at the band): three thin ones (4 wide, 4.5 long)
# hanging nearly the same way from a band 10 px long had their bells touching in 36 of 38 frames, stacked into one
# gold bar in graze 05, and the band hid all but 2-5 px of each, so a point vanished as it swung (two fresh reviews,
# 2026-11-03). Three fanned still had a point under 4 px in some frames, or two bells touching; two bold ones show 6
# px or more in every frame and no bells touch (measured over every frame's collar).
# One point since 2026-11-08, navy, at the band's end over the chest: the rear one (70 degrees, from 0.2 along the band)
# hung over the cloth's front check, the cloth's own navy there (a blue stripe through the check above it and into the
# one under it, "repeated colours", the user's boxes on walk 02); the front one was burgundy, against the cloth's front
# check, burgundy below since the cloth is chequered in three columns (caparison).
COLLAR = {"line": [(45.4, 40.6), (44.6, 42.6), (43, 45.5), (40.6, 48.2)], "band": 4.5,
          "points": [(0.95, 118)], "len": 8, "w": 5.5, "first": 1}   # burgundy (2026-11-08: navy, it ran into the cloth's navy check under the band)
FRONT_COLLAR = {"line": [(31, 51.5), (36.5, 56.5), (42, 51.5)], "band": 2.5,   # turn 05: a short U under the chin
                "points": [(0.15, 125), (0.5, 90), (0.85, 55)], "len": 5.5, "w": 5}   # fanned: their bells ran together


def collar(c, key, tf=None, swings=None, report=None):
    """The collar from 'c' on frame 'key', its line's points taken through tf (a pose) first: a burgundy band along
    the curve, shaded like a tube and cut to inside the donkey's outline there, a point hanging from it at each (t
    along the band, angle: 90 straight down, each point's tip moved by its own of 'swings'), navy and burgundy in turn,
    edged in their own dark, a small bell at each tip. The band was navy: under the cap's navy back point it read as
    the cap running on down the neck, one blue shape about 25 rows tall ("the cap still looks huge, extending the
    back", the user, 2026-11-03); burgundy, it is the strap's own colour (the skin tints the strap burgundy).
    Where a pose turns the band (the graze) the points turn with it: hanging the same way from a band turned near
    upright, a point ran along it and hid under it, its bell left hanging (graze 04-05). A swing moves a point's tip
    across the point, on a pendulum's arc, and shortens it a pixel at the arc's ends: across the frame, a slanted point
    swung out of its shape (walk 02 and 06). Each bell hangs from its point's tip pixel (the one furthest along it).
    report: a list given gets (point, its pixels showing, its tip pixel) per point (the checks)."""
    tf = tf or (lambda p: p)
    line = curve([tf(p) for p in c["line"]])
    still = curve(c["line"])
    turn = math.degrees(math.atan2(line[-1][1] - line[0][1], line[-1][0] - line[0][0])
                        - math.atan2(still[-1][1] - still[0][1], still[-1][0] - still[0][0]))
    turn = (turn + 180) % 360 - 180
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
        ang += turn
        d = t * lens[-1]
        i = max(j for j in range(len(line) - 1) if lens[j] <= d)
        f = (d - lens[i]) / ((lens[i + 1] - lens[i]) or 1)
        hx = line[i][0] + (line[i + 1][0] - line[i][0]) * f
        hy = line[i][1] + (line[i + 1][1] - line[i][1]) * f
        ux, uy = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        w = c["w"] / 2
        sw = swings[n] if swings else (0, 0)
        reach = c["len"]   # the swing's lift not used: it shortened the point and its bell hopped (walk 04)
        tip = (hx + ux * reach + uy * sw[0], hy + uy * reach - ux * sw[0])   # across: (1, 0) for a point straight down
        part = polygon([(hx - uy * w, hy + ux * w), tip, (hx + uy * w, hy - ux * w)]) - band
        parts.append((part, (COLLAR_NAVY, COLLAR_BURG)[(n + c.get("first", 0)) % 2]))
        end = max(part, key=lambda q: (q[0] * ux + q[1] * uy, q[1])) if part else None
        tips.append(end)
        if report is not None:
            report.append((n, len(part), end))
    px = {}
    tube(band, COLLAR_BURG, px, band.union(*[part for part, _ in parts]))
    for part, ramp in parts:
        for (x, y) in part:   # a point lit on its left, shaded on its right
            px[(x, y)] = ramp[3] if (x - 1, y) not in part else ramp[1] if (x + 1, y) not in part else ramp[2]
    edge([(band, COLLAR_BURG)] + parts, px)
    for end in tips:
        if end:
            small_bell(end[0], end[1] + 1, px)
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
# hem at row 56 (the belly at 57-58), chequered (the columns split at x 58, the rows under row 48), lit along the
# top where the back rounds, shaded to the bottom under the belly's curve, two soft folds, the rear column rounding
# away; three points under the hem, counterchanged, and a small bell under each (under two at first: "extend the bells
# appropriately too", the user, when the cloth was carried forward to the strap: tucked()).
# Since 2026-11-08 chequered in three columns: the part carried forward to the strap (tucked()) is a check column of
# its own, its colours swapped (navy over burgundy), the line at FRONT_FOLDS' 47 its seam; carried on in the middle
# column's colours, the cloth repeated burgundy and navy beside the strap ("the marked regions have repeated colours",
# the user). Four points along the hem, evenly spaced (42 at FRONT_POINT, then 49, 55, 62: bells 7, 6, 7 apart) and
# alternating navy, burgundy, navy, burgundy from the front ("four bells would fit perfectly there ... their colours
# alternate correctly", the user, 2026-11-08: three had a gap between the front two). The bottom row has three checks
# for four points, so the third and fourth share their check's colour: the user's pick, alternating over matching.
CLOTH = {"x0": 48, "x1": 66, "hem": 56, "split": 58, "quarter": 48, "folds": (54, 62), "points": (49, 55, 62),
         "bells": (49, 55, 62)}
POINT_RAMPS = (CLOTH_BURG, CLOTH_NAVY)   # the caparison's points in turn (the front one, tucked()'s, navy before them)


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
        if y > c["quarter"]:   # the bottom row, and the points under it (flipped once more below)
            front = not front
        if y > c["hem"]:
            front = not front
        return CLOTH_BURG if front else CLOTH_NAVY
    cloth = {(x, y) for x in range(c["x0"], c["x1"] + 1) for y in range(top.get(x, 44), c["hem"] + 1)}
    points = []
    for p0, sw in zip(c["points"], ripple):
        points.append(hem_point(p0, c["hem"], sw))
    shape = set(cloth)
    for p in points:
        shape |= set(p)
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
    for n, (p0, p) in enumerate(zip(c["points"], points)):
        r = POINT_RAMPS[n % 2]   # one colour a point, in turn: by the pixel, a point over the split changed colour as it
        for q, v in p.items():   # swung across it (a fresh review)
            px[q] = r[v]
    # each bell hangs from its point's lowest pixel: placed by the swing's numbers, a bell hung a row under the tip
    # where the swing lifted the point (a fresh review, 2026-11-03)
    for b in c["bells"]:
        pt = points[c["points"].index(b)]
        low = max(y for x, y in pt)
        small_bell(min(x for x, y in pt if y == low), low + 1, px)
    # the swing: each row from SWING_FROM down moved across (the points and bells under the hem by the hem's own)
    return {(x + shear(min(y, c["hem"])), y): col for (x, y), col in px.items()}


# The cloth's front carried forward to the collar strap, in each frame ("can you extend the cloth on the donkeys back to
# go right under the harness itself? ... so that none of the donkeys back is shown due to the cloth", the user,
# 2026-11-03). The cloth moves with the body and the strap with the neck: in the walk the strap runs 2-4 px ahead of
# the cloth and up to 2 lower (stamp's neck and body moves), and a strip of the shoulder up to 5 px wide showed between
# them. A cloth drawn to the strap once would open that strip in the walk; a panel moving with the strap would slide
# over the cloth with a seam and a hem stepping 2 px. So each frame's cloth reaches that frame's own strap: on each
# row from just behind the strap's back edge (its orange, and the dark column behind it) to the cloth's front, its
# colours and quarters continued from an inner column, edged where it meets the donkey, one more point under its
# hem (FRONT_POINT) with a bell. In turn 04-06 too (the donkey facing us): the strap still runs down its near shoulder
# there, and the chest showed between the front collar and the cloth ("now fix the turn 04-06 chest gap too").
FRONT_POINT = 42   # under the front check (38-47 on the hem in the walk), 45 straddled its seam at 47
# the new part's folds: the cloth's vertical lines are 5 apart (the folds 52, 62, the seam 57), so 47 and 42 (with
# one, the part from its front to 47 ran 6-10 px without a line in the walk: "that part of the cloth needs another
# seem because it looks too wide to not have a seem", the user); a stretch of a fold showing under 2 rows outside the
# collar in a frame is left out (at 42 it once lay under the collar's bells, a lone pixel showing beside them; with 3,
# a fold showing 2 rows between the collar and the hem's band was left out: "some seems are missing", walk 05)
FRONT_FOLDS = (42,)   # 2026-11-08: its wide bottom half (strap to 49) a second line, its fold at 46 (front_check) the first


def strap_back(key, before):
    """The collar strap's back edge on each row of frame 'key': the last column of its orange (anatomy.body()'s collar
    test) in front of column 'before' (the cloth's front there); a row without any, between two that have, takes the
    larger of the two."""
    don = load(key).convert("RGBA").load()
    orange = lambda c: c[3] and c[0] >= 90 and c[0] > 1.3 * c[1]
    rows = {}
    for y in range(96):
        xs = [x for x in range(20, before) if orange(don[x, y])]
        if xs:
            rows[y] = max(xs)
    ys = sorted(rows)
    for y in range(ys[0], ys[-1]) if ys else ():
        if y not in rows:
            rows[y] = max(rows[max(q for q in ys if q < y)], rows[min(q for q in ys if q > y)])
    return rows


FRONT_SEAM = 2   # the front check reaches this many columns into the cloth's own front (x0): its top shows in every frame
FRONT_FOLD = 4   # its fold this many columns in front of its seam (46 at rest: the middle of its top, 44-49)
FRONT_UNDER = 8   # the bottom row in front of this many columns before the seam (42 at rest, the fold there) is a check of
#   its own, navy: the collar band (burgundy) is the top row's first check above it ("the part which should be blue
#   acording to the chequed pattern", the user, 2026-11-08; burgundy, it lay against the band's burgundy)


def front_check(out, px, hem):
    """The cloth's front check (2026-11-08) on a frame's cloth 'out' (tucked()'s, from the caparison 'px'): on each row
    down to the hem, from the strap to FRONT_SEAM columns into the caparison's own front on that row, the other colour
    at the same shade, and the middle check's first column a shade darker, its seam (as the split's). Only the part
    carried forward to the strap had no top in the standing frames: the strap meets the cloth's front there, and the
    collar's burgundy band lay against the middle check's burgundy."""
    cloth = set(CLOTH_BURG) | set(CLOTH_NAVY)
    firsts = {}
    for (x, y), col in px.items():
        if y <= hem and col in cloth:
            firsts[y] = min(firsts.get(y, x), x)
    if not firsts:
        return out
    top = min(firsts)
    seam = lambda y: firsts.get(y, firsts[top]) + FRONT_SEAM
    other = lambda col: (CLOTH_NAVY[CLOTH_BURG.index(col)] if col in CLOTH_BURG
                         else CLOTH_BURG[CLOTH_NAVY.index(col)])
    res = dict(out)
    for (x, y), col in out.items():
        if y > hem or col not in cloth:
            continue
        if x < seam(y) - FRONT_UNDER and y > hem - (CLOTH["hem"] - CLOTH["quarter"]):
            pass   # under the collar's burgundy band, the bottom row's first check: navy, as it was (the user's mark)
        elif x < seam(y):
            res[(x, y)] = other(col)
        elif x == seam(y) and (x, y - 1) in out and (x, y + 1) in out:
            r = CLOTH_BURG if col in CLOTH_BURG else CLOTH_NAVY
            if r.index(col) > 1:
                res[(x, y)] = r[r.index(col) - 1]
    # its fold, down its middle through both rows as the other checks' (FRONT_FOLD columns in front of the seam): a
    # shade darker, not on its outline nor its top two rows. FRONT_FOLDS' one at 44 lay in the part carried forward
    # only, which has no top at the strap: the bottom row alone had a line ("arent they missing a split in the middle
    # like the other ones have?", the user, walk 02)
    for (x, y), col in list(res.items()):
        if y > hem or col not in cloth or x != seam(y) - FRONT_FOLD:
            continue
        if all(q in res for q in ((x - 1, y), (x + 1, y), (x, y + 1), (x, y - 1))) and res.get((x, y - 2)) in cloth:
            r = CLOTH_BURG if col in CLOTH_BURG else CLOTH_NAVY
            if r.index(col) > 1:
                res[(x, y)] = r[r.index(col) - 1]
    return res


def tucked(px, key, d, sw=(0, 0), over=frozenset()):
    """A frame's cloth (its pixels on frame 'key', placed there by d, stamp's body move) carried forward on each row
    to just behind the strap's back edge, down to its hem: the colour of the cloth's inner column (three in from that
    row's front) on that row, its darkest where the new part meets the donkey (not against the strap's own dark edge,
    which is the line there), folds at FRONT_FOLDS a shade darker, moved with its row's sway, kept where they show 2
    rows or more outside 'over' (the collar's pixels in that frame); the row's old front edge
    (its first two pixels) made inner cloth; a point under the new hem at FRONT_POINT, its tip moved by 'sw', a bell
    hanging from its lowest pixel; and the cloth's top brought up over the donkey's back where it shows 1-2 px above
    it (the walk's back line has bumps: coat poked above the cloth behind the collar). Each row is filled to its own
    front: the walk's and the lips' sway move the lower rows a column, and the gap that left at the old edge, taken
    for outside, had both its sides outlined in the darkest (a dark bar in walk 04 and more frames, the user's orange
    box, 2026-11-03)."""
    c = CLOTH
    x0, hem = c["x0"] + d[0], c["hem"] + d[1]
    back = strap_back(key, x0)
    don = load(key).convert("RGBA").load()
    fill, old_edge, inner, fronts = set(), set(), {}, {}
    for y in range(min(back, default=hem + 1), hem + 1):
        front = next((x for x in range(x0 - 2, x0 + 4) if (x, y) in px), None)
        if y not in back or front is None or back[y] + 2 >= front:
            continue
        col = next((px[(front + k, y)] for k in (3, 2, 1, 0) if (front + k, y) in px), None)
        if col is None:
            continue
        inner[y], fronts[y] = col, front
        fill |= {(x, y) for x in range(back[y] + 2, front)}
        old_edge |= {(front, y), (front + 1, y)}
    out = dict(px)
    if fill:
        p0 = FRONT_POINT + d[0] + fronts.get(hem, x0) - x0   # with the hem's sway, as the cloth's other points
        shades = {q: v for q, v in hem_point(p0, hem, sw).items() if q not in px and q not in fill}
        point = set(shades)
        shape = fill | set(px) | point
        ramp = lambda col: CLOTH_BURG if col in CLOTH_BURG else CLOTH_NAVY
        fold = {}
        for (x, y) in fill | old_edge:
            r, level = ramp(inner[y]), ramp(inner[y]).index(inner[y])
            at_strap = x - 1 == back[y] + 1 and don[x - 1, y][3] > 0   # the strap's dark edge is the line here
            if any(q not in shape for q in ((x, y - 1), (x, y + 1))) or ((x - 1, y) not in shape and not at_strap):
                out[(x, y)] = r[0]
                continue
            out[(x, y)] = r[level]
            if (x in [f + d[0] + (fronts[y] - x0) for f in FRONT_FOLDS] and (x, y - 2) in shape
                    and level > 1):
                fold[(x, y)] = r[level - 1]   # a fold, as the cloth's: a shade darker, not at the top
        # each fold's stretches showing outside the collar: 3 rows or more, else left out
        for q, col in fold.items():
            if q in over:
                continue
            run = 1
            for step in (-1, 1):
                y = q[1] + step
                while any((x, y) in fold and (x, y) not in over for x in (q[0] - 1, q[0], q[0] + 1)):
                    run, y = run + 1, y + step
            if run >= 2:   # 3 left the folds out where only 2 rows show between the collar and the hem's band
                out[q] = col
        for q, v in shades.items():   # navy, the other colour of the front check above it (burgundy
            out[q] = CLOTH_NAVY[v]           # below), shaded as at rest
        low = max(y for x, y in point)
        small_bell(min(x for x, y in point if y == low), low + 1, out)
    # the top over the back's bumps: on each column, the donkey's pixels just above the cloth's top (1 or 2, the coat
    # ending above them) are covered, the top's outline moved up onto them and the rows under it lit as the top was
    cols = sorted({x for x, y in out if y <= hem})
    for x in cols:
        t = min(y for xx, y in out if xx == x and y <= hem)
        k = next((k for k in (1, 2) if all(don[x, t - j][3] for j in range(1, k + 1)) and not don[x, t - k - 1][3]),
                 None)
        if k is None or (x, t + 1) not in out:
            continue
        top, under = out[(x, t)], out[(x, t + 1)]
        for j in range(1, k + 1):
            out[(x, t - j)] = top if j == k else under
        out[(x, t)] = under
    return front_check(out, px, hem)


def front_swing(key):
    """The new front point's swing in frame 'key': the hem's other points' (they swing together: a bell under each, 5 px
    apart, would run into its neighbour's)."""
    import stamp
    for clip in ("walk", "lips", "graze"):
        keys = [k for k in stamp.ORDER if k.startswith(f"bdy_{clip}_left_")]
        if key in keys:
            i = keys.index(key)
            return pendulum(i, -2) if clip == "walk" else CLIP_MOTION[clip]["hem"][0][i]
    return (0, 0)


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
        # the hem's points together: a frame apart, with a bell under each, neighbours swung into each other and
        # their bells ran into one gold bar (walk 01)
        ripple = [pendulum(i, -2)] * len(CLOTH["points"])
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
              "hem": [[Z, Z, (1, 0), (1, 0), Z]] * 3},
    "turn": {"red": [Z, (1, 0), (2, -1), (1, 0), Z, (-1, 0)], "blue": [Z, Z, (1, 0), (2, -1), (1, 0), Z],
             "collar": [[Z, Z, (1, 0), (1, 0), Z, Z], [Z, Z, Z, (1, 0), (1, 0), Z], [Z, Z, Z, Z, (1, 0), (1, 0)]]},
    "lips": {"red": [pendulum(i, 0, 1, 13) for i in range(13)], "blue": [pendulum(i, -3, 1, 13) for i in range(13)],
             "collar": [[pendulum(i, -2 - 2 * n, 1, 13) for i in range(13)] for n in range(3)],
             "hem": [[pendulum(i, -3, 1, 13) for i in range(13)]] * 3},
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
    # every side frame's cloth its own, carried forward to that frame's strap (tucked)
    shift = lambda px, d: {(x + d[0], y + d[1]): c for (x, y), c in px.items()}
    cols = list(range(CLOTH["x0"], CLOTH["x1"] + 1))
    bodies = {n: px for n, k, px in moving if n.startswith("body@")}
    tucked_ = []
    for k in stamp.ORDER:
        d = stamp.body_move(k, cols) if k.startswith("bdy_") else None   # turn 04-06 too: their chest showed
        if d is None:
            continue
        name = f"body@{k}.png"
        neck = dict(necks).get(f"neck@{k}.png") or shift(dict(necks)["neck.png"], stamp.neck_move(k))
        tucked_.append((name, tucked(bodies.get(name) or shift(caparison(), d), k, d, front_swing(k), set(neck))))
    pieces = [("body.png", caparison())] + tucked_ + [(n, px) for n, px in bodies.items()
                                                      if n not in {t[0] for t in tucked_}] + necks
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
