"""Armored (donkey/saddleup-donkey-skins.armored): plate barding drawn OVER the donkey (docs/sprite-tools.md "Making a
skin"), the third skin of the Donkey Skin Pack. Second cut (2026-11-01, the user: "a lot more detail and exquisiteness",
"looking professional while matching the game's aesthetic"). What it is made of comes from the references (a war horse
in plate, knights' chargers with a plumed chanfron and a caparison); HOW it is drawn comes from the game's own armoured
guards (presets 0341, 0622, 0626, 0641: dull warm steel shaded round, lit from the front and above, a dark warm
outline, one or two bright pixels where a curve meets the light, brass bits). The user's choices: the steel is always
steel (kept as drawn), a cloth skirt and the plume in the Second tone (since 2026-11-02; the harness colour before,
which left the Second tone doing nothing on this skin); neck plates and a forehead spike were chosen and dropped
(2026-11-02, the user's videos and marked screenshot: on a donkey's head and neck anything that stands out reads as
another ear); the face plate (with a brass boss on the brow) gave way to an ornate armet the same
day (the user, after seeing it in game: "lacklustre", "fill the donkey's face more").

    python -I tools/skins/armored/make_armored.py              # the pack's pictures + out\\
    python -I tools/skins/armored/make_armored.py --no-assets  # out\\ only (the review folder; the pack untouched)
    python -I tools/skins/armored/make_armored.py --cfg        # rewrite the pack's skin.cfg too (by hand otherwise)

How it is drawn: every piece is cut and shaded ONCE, on the standing frame, then moved into each frame with the part of
the donkey it rides on, so its pixels never change between frames (no shimmer):
  - a plate is a shape (its edges as numbers below), given a surface (a barrel's curve, a dome, a breast plate's bulge,
    every edge rounded over a few pixels), lit by one light from the front and above (the game's: the donkey's back and
    forehead are its lit planes) and put into the steel's shades; then the hand-placed things: the outline, the rolled
    rim along the top, the seams, the rivets, the brass trims;
  - the middle of the body is five steel lames (the user's pick, 2026-11-02, over a padded caparison): the donkey's
    body stretches in the walk (the shoulders move, the hip stays) and one plate there either slid or seemed to
    stretch (the user's videos); each lame moves part way between the two, so no seam shifts more than a pixel;
  - under them a cloth skirt in the Second tone (the [Colors] Muzzle row), folds with a dagged hem, stretching as the body does;
  - nothing of the body's armour reaches the tail (it runs down the rump's back edge); a steel guard of rings
    covers its upper half, the tuft free (the user's choice, 2026-11-02, after a ribbon bow and a steel ball on
    the tip were tried and dropped);
  - what each piece moves with (tools/skins/donkey/anatomy.py; measured: the head is a pure translation of the
    standing head in the walk, ear, chew and lips frames and rotates in graze 02-05 and turn 02-06; the hip stays at
    column 65/66 in every frame but the turn's; the chest and the collar strap move with the neck):
      breast plate: the shoulders (the collar strap's shift); the breast collar is the seam between it and the
        caparison (the strap stays in view);
      lames and skirt: spread between the shoulders and the hip (each lame whole at its middle's share);
      the tail guard: fitted to the frame's own tail;
      rump plate: the hip;
      every body piece's columns draped on the frame's own back line (Royal Parade's cloth rule), so they ride the
        walk's bob;
      the armet and its plume: the head; graze 03-05 (the head turned down, the jaw open to the grass) and turn
        03-06 (three quarters, the front) have their own drawings of the helmet, the plume turned with the head.
"""
import math, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "donkey"))
from anatomy import ORDER, load, HORNS, ANCHORS, body, head_delta
import ase_write
import review
from gamedir import game_dir

REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
ASSETS = os.path.join(REPO, "packs", "GK2SaddleUpDonkeySkinPack", "gk2mounts", "donkey", "skins", "saddleup-donkey-skins.armored")
OUT = os.path.join(HERE, "out")

# The colours. The steel is the guards' steel (sampled from the game's helmets and breastplates: a warm grey with a
# purple lean), kept as drawn; the brass is the guards' brass; the cloth (skirt, plume) is a red the player's Second
# tone replaces (the [Colors] Muzzle row; the harness colour reaches only the donkey's own harness). None is a donkey
# colour (check_skin says so).
K = (40, 27, 38)  # the steel's outline
STEEL = [K, (57, 49, 52), (72, 62, 63), (93, 83, 87), (112, 102, 112), (131, 121, 136), (157, 146, 163), (177, 172, 177), (205, 201, 205)]
BRASS = [(78, 48, 10), (125, 83, 8), (164, 128, 11), (207, 169, 15), (241, 210, 116)]
CLOTH = [(56, 18, 26), (94, 28, 36), (138, 40, 42), (178, 58, 50), (208, 92, 70)]
PAL = {"k": K, ".": None}
PAL.update({str(i): STEEL[i] for i in range(1, 9)})
PAL.update({c: BRASS[i] for i, c in enumerate("vwxyz")})
PAL.update({c: CLOTH[i] for i, c in enumerate("qrstu")})

LIGHT = (-0.55, -0.72, 0.42)   # from the front (the donkey faces left), above, and towards us
RIVET_EVERY = 3                # rivets along a rim, repeated from the piece's anchor
PLEAT = 4                      # the skirt's fold, in columns, repeated from the piece's anchor


def norm(v):
    l = math.sqrt(sum(c * c for c in v)) or 1.0
    return tuple(c / l for c in v)


L = norm(LIGHT)
H = norm((L[0], L[1], L[2] + 1.0))


def lit(n, gain=1.0, lift=0.0):
    """A surface normal to a steel shade: the diffuse light into STEEL[2..6], a highlight where the curve turns to the
    light (STEEL[7]) and the one bright pixel or two of the guards' helmets (STEEL[8])."""
    d = max(0.0, sum(a * b for a, b in zip(n, L)))
    s = max(0.0, sum(a * b for a, b in zip(n, H))) ** 40
    if s > 0.6:
        return STEEL[8]
    if s > 0.22:
        return STEEL[7]
    v = min(1.0, max(0.0, d * gain + lift))
    return STEEL[2 + min(4, int(v * 5.0))]


def edge_field(mask, R):
    """Per pixel of a shape: the distance to the nearest pixel outside it (up to R) and the unit direction towards it."""
    out = {}
    rr = int(math.ceil(R))
    for (x, y) in mask:
        best = None
        for dy in range(-rr, rr + 1):
            for dx in range(-rr, rr + 1):
                if (x + dx, y + dy) not in mask:
                    d = math.hypot(dx, dy)
                    if d <= R and (best is None or d < best[0]):
                        best = (d, dx, dy)
        out[(x, y)] = (R, 0.0, 0.0) if best is None else (best[0] - 0.5, best[1] / best[0], best[2] / best[0])
    return out


def shade(mask, normal, R=2.5, tilt=1.1, gain=1.0, lift=0.0):
    """A shape lit as steel: `normal(x, y)` is the piece's surface (a barrel, a dome); every edge rounds over R pixels."""
    ef = edge_field(mask, R)
    out = {}
    for p in mask:
        nx, ny, nz = normal(*p)
        d, ox, oy = ef[p]
        t = tilt * max(0.0, 1.0 - d / R) ** 1.5
        out[p] = lit(norm((nx + ox * t, ny + oy * t, nz)), gain, lift)
    return out


def stamp(out, art, x0, y0):
    """An ASCII drawing (PAL keys, '.' empty) with its top-left at (x0, y0)."""
    for j, row in enumerate(art):
        for i, ch in enumerate(row):
            c = PAL.get(ch)
            if c is not None:
                out[(x0 + i, y0 + j)] = c
    return out


def opaque(im):
    px = im.load()
    return lambda x, y: 0 <= x < 96 and 0 <= y < 96 and px[x, y][3] > 0


# ---------------------------------------------------------------- the standing frame's measures

S_KEY = "bdy_static_left"
S = None


RUMP_BOX = (52, 40, 64, 57)   # the hindquarters on the standing frame, the tail's band left out (x0, y0, x1, y1)
RUMP = None


def body_of(key, donkey):
    """anatomy.body() with the hip measured by the rump itself: the standing frame's hindquarters (RUMP_BOX) laid
    over this frame where its pixels match best, the hip moved by that shift. body() takes the back's level from the
    highest body pixel behind the collar, and in turn 02-03 that is the turned head's far ear lying back (rows
    36-37): its hip came out at 63 and 61 on a rump the same as the standing one, and the rump plate, the lames and
    the tail guard slid forward onto the rump."""
    global RUMP
    b = body(key, donkey)
    if b is None:
        return None
    if RUMP is None:
        sp = load(S_KEY).load()
        x0, y0, x1, y1 = RUMP_BOX
        RUMP = (body(S_KEY, load(S_KEY))["hip"], {(x, y): sp[x, y] for y in range(y0, y1) for x in range(x0, x1)})
    s_hip, box = RUMP
    px = donkey.load()
    best = None
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            score = sum(1 for (x, y), c in box.items() if px[x + dx, y + dy] == c)
            if best is None or score > best[0] or (score == best[0] and abs(dx) + abs(dy) < abs(best[1]) + abs(best[2])):
                best = (score, dx, dy)
    b["hip"] = s_hip + best[1]
    return b


def standing():
    """The standing frame: its body() measures and the barrel's back line per column (the first body row, held down to
    the withers where the neck climbs)."""
    global S
    if S is None:
        d = load(S_KEY)
        b = body_of(S_KEY, d)
        S = {"b": b, "op": opaque(d), "hip": b["hip"], "collar": b["collar"], "back": back_line(b), "strap": strap_of(d)}
    return S


def back_line(b):
    """The barrel's top per column from SIDE_FRONT in front of the hip to 3 behind it: the frame's own first body row,
    held down to one row above the back's level in front of the withers (the neck climbs there)."""
    top, hip = b["top"], b["hip"]
    rear = sorted(top[hip - i] for i in range(0, 9) if (hip - i) in top)
    level = rear[len(rear) // 2]
    out = {}
    for i in range(-3, SIDE_FRONT + 1):
        x = hip - i
        t = top.get(x)
        if t is None:
            ys = [y for y in range(level - 4, level + 16) if b["op"](x, y)]
            t = min(ys) if ys else None
        if t is not None:
            out[i] = max(t, level - 1) if i > 6 else t
    # columns in front of the measured ones (in front of the strap: cut away in every frame): the last known
    last = max(out)
    for i in range(last + 1, SIDE_FRONT + 1):
        out[i] = out[last]
    return out


# ---------------------------------------------------------------- the skirt (stretches), the rump plate (hip)

SIDE_FRONT = 34          # the lames and the skirt reach this many columns in front of the hip: to the breast, where
# every frame cuts them along the breast collar and the chest's own outline (cutter)
BODY_LAST = {i: (53 if i <= 21 else 54 if i <= 25 else 55) for i in range(-3, SIDE_FRONT + 1)}  # the lames' last row,
# lower under the deeper chest (standing); the skirt's gold band under it, then the skirt
SKIRT_HEM = 58           # the skirt's last full row (standing); the dags hang one row lower
REAR = 2                 # nothing reaches nearer the hip than this: the tail runs down the rump's back edge from its
# top (columns hip-1 to hip+2 on the standing frame) and stays in view whole (the user, 2026-11-02)
CRUPPER_FRONT = 7        # the rump plate starts this many columns in front of the hip
CRUPPER_BOTTOM = {7: 50, 6: 51, 5: 51, 4: 52, 3: 52, 2: 52}


def skirt_colour(xs, ys):
    """The skirt's colour at a point of the standing frame, or None outside it. `xs` may fall between columns: the
    walk stretches the cloth, and its pattern is drawn where the stretched cloth puts it (crisp, never resampled).
    A gold band under the lames, then the folds lit on their front side, the top row in the plates' shadow, the hem
    dagged (two columns of every fold one row longer)."""
    s = standing()
    i = s["hip"] - int(round(xs))
    if i < REAR or i > SIDE_FRONT:
        return None
    last = BODY_LAST[i]
    if ys <= last or ys > SKIRT_HEM + 1:
        return None
    col = math.floor(xs)
    if ys == last + 1:
        return BRASS[3] if col % 3 == 0 else BRASS[2]
    p = col % PLEAT
    if ys == last + 2:
        return CLOTH[1] if p != 1 else CLOTH[2]
    if ys == SKIRT_HEM + 1:
        return (CLOTH[1] if p == 1 else CLOTH[0]) if p in (1, 2) else None
    if ys == SKIRT_HEM:
        return CLOTH[2] if p in (1, 2) else CLOTH[0]
    return [CLOTH[1], CLOTH[3], CLOTH[2], CLOTH[2]][p]


def rump_layer():
    """The rump plate on the standing frame, {(i, y): colour} with i the columns in front of the hip: a dome over the
    hindquarters in front of the tail, its rear edge an outline beside the tail, its front edge lit over the lames
    it lies on, rivets, the brass trim."""
    s = standing()
    hip, back = s["hip"], s["back"]
    out = {}
    crup = set()
    for i in range(REAR, CRUPPER_FRONT + 1):
        for y in range(back[i] - 1, CRUPPER_BOTTOM[i] + 1):
            crup.add((hip - i, y))
    cx, cy = hip - (REAR + CRUPPER_FRONT) / 2.0 - 1.0, back[4] + 3.0

    def dome(x, y):
        nx, ny = (x - cx) / 5.0, (y - cy) / 7.5
        return (nx, ny, math.sqrt(max(0.05, 1.0 - nx * nx - ny * ny)))

    cp = shade(crup, dome, R=1.6, tilt=0.6, gain=1.05)
    for (x, y) in crup:
        i = hip - x
        t = back[i] - 1
        if y == t or i == CRUPPER_FRONT or i == REAR:
            cp[(x, y)] = K
        elif i == CRUPPER_FRONT - 1 and y < CRUPPER_BOTTOM[i]:
            cp[(x, y)] = STEEL[6] if y < CRUPPER_BOTTOM[i] - 1 else STEEL[4]   # its front edge, lit
        elif y == CRUPPER_BOTTOM[i]:
            cp[(x, y)] = STEEL[1]
        elif y == CRUPPER_BOTTOM[i] - 1 and i % RIVET_EVERY == 1:
            cp[(x, y)] = STEEL[7]
    for (x, y), c in cp.items():
        out[(hip - x, y)] = c
    for i in range(REAR, CRUPPER_FRONT):
        out[(i, CRUPPER_BOTTOM[i] + 1)] = BRASS[3] if i % RIVET_EVERY == 1 else BRASS[2]
    return out


def breast_collar(donkey):
    """The breast collar in a frame: the strap's piece holding its topmost pixel (the girth under it is another)."""
    strap = strap_of(donkey)
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
    return collar


CHEST_FROM = 6   # under the collar's end the chest's front is the first body pixel at most this far in front of it


def near(d, i):
    """d[i], or the nearest key's value."""
    return d[i] if i in d else d[min(d, key=lambda k: abs(k - i))]


def spread(x, b, cdx):
    """How far a standing column moves in a frame: with the shoulders (the collar strap's shift) in front of the
    collar's standing column, with the hip behind the hip, spread between the two in between."""
    s = standing()
    hdx = b["hip"] - s["hip"]
    u = min(1.0, max(0.0, (x - s["collar"]) / float(s["hip"] - s["collar"])))
    return cdx * (1.0 - u) + hdx * u


def cutter(b, op, key, donkey):
    """The cut every body piece in front of the rump takes in a frame: on the breast collar's rows nothing on or in
    front of it (the breast plate's); under the collar nothing in front of the chest's own front, the piece's edge
    there darkened; the strap's shadow on what lies behind it. Returns keep(x, y, colour) -> colour or None."""
    collar = breast_collar(donkey)
    right = {}
    for x, y in collar:
        right[y] = max(right.get(y, -1), x)
    last = max(right)
    end_x = min(x for x, y in collar if y == last)
    lim = LOW_HEAD[key][2] if key in LOW_HEAD else end_x - CHEST_FROM
    front = {}
    for y in range(last + 1, 57):
        xs = [x for x in range(lim + 1, b["hip"]) if op(x, y)]
        if xs:
            front[y] = xs[0]
    deepest = max(front) if front else last

    def keep(xt, yt, c):
        if yt in right:
            if xt <= right[yt]:
                return None
        elif yt > last:
            f = front.get(yt, front.get(deepest))
            if f is None or xt < f - 1:
                return None
            if xt == f - 1:
                c = CLOTH[0] if c in CLOTH else K
        if (xt - 1, yt) in collar or (xt, yt - 1) in collar:
            c = CLOTH[1] if c in CLOTH[2:] else STEEL[2] if c in STEEL[2:] else c   # the strap's shadow
        return c
    return keep


def place_skirt(b, op, key, donkey, cdx):
    """The skirt in a frame. It stretches as the body does (spread()): the walk moves the shoulders up to five columns
    and cloth takes it as the donkey's own coat does; its folds are drawn where the stretched cloth puts them. Each
    column is draped on the frame's back line and cut along the strap and the chest (cutter())."""
    s = standing()
    sb, fb = s["back"], back_line(b)
    hdx = b["hip"] - s["hip"]
    xf, xh = s["collar"], s["hip"]
    span = float(xh - xf)

    def source(xt):
        if xt < xf + cdx:
            return float(xt - cdx)
        if xt > xh + hdx:
            return float(xt - hdx)
        return (xt - cdx + (hdx - cdx) * xf / span) / (1.0 + (hdx - cdx) / span)

    keep = cutter(b, op, key, donkey)
    out = {}
    for xt in range(b["hip"] - SIDE_FRONT - 6, b["hip"] + 3):
        xs = source(xt)
        i_s = s["hip"] - int(round(xs))
        if i_s < REAR or i_s > SIDE_FRONT:
            continue
        dy = near(fb, b["hip"] - xt) - near(sb, i_s)
        for ys in range(BODY_LAST[i_s] + 1, SKIRT_HEM + 2):
            c = skirt_colour(xs, ys)
            if c is None:
                continue
            c = keep(xt, ys + dy, c)
            if c is not None:
                out[(xt, ys + dy)] = c
    return out


LAMES = [(REAR, 6), (7, 11), (12, 16), (17, 21), (22, SIDE_FRONT)]  # the steel lames, rear to front: columns in
# front of the hip; the rear one lies under the rump plate, the front one is cut along the strap


def lame_plates():
    """The lames on the standing frame: {n: {(x, y): colour}}: the barrel lit from the front and above (as a plate),
    each lame running two columns on under the lame behind it (so the walk never opens a gap), its front edge an
    outline and a lit rim with two rivets, its top one row proud of the back, its bottom turning away."""
    s = standing()
    hip, back = s["hip"], s["back"]
    region = set()
    for i in range(REAR, SIDE_FRONT + 1):
        for y in range(back[i] - 1, BODY_LAST[i] + 1):
            region.add((hip - i, y))
    y0, y1 = min(y for _, y in region), 56.0

    def barrel(x, y):
        f = (y - y0) / (y1 - y0)          # 0 at the spine, 1 at the belly
        ny = -0.95 + 1.7 * f
        return (0.0, ny, math.sqrt(max(0.05, 1.0 - ny * ny)))

    lit_ = shade(region, barrel, R=2.0, tilt=0.9)
    out = {}
    for n, (lo, hi) in enumerate(LAMES):
        px = {}
        for i in range(max(REAR, lo - 2), hi + 1):
            x = hip - i
            t = back[i] - 1
            for y in range(t, BODY_LAST[i] + 1):
                c = lit_[(x, y)]
                if y == t:
                    c = K
                elif i == hi and n < len(LAMES) - 1:
                    c = K                                   # the front edge
                elif i == hi - 1 and n < len(LAMES) - 1:
                    c = STEEL[7] if y in (t + 3, BODY_LAST[i] - 2) else STEEL[6]   # its lit rim, two rivets
                elif y == t + 1:
                    c = STEEL[6]
                elif y == BODY_LAST[i]:
                    c = STEEL[1]
                px[(x, y)] = c
        out[n] = px
    return out


def place_lames(b, op, key, donkey, cdx):
    """The lames in a frame, front first and each one behind over it: every lame whole, moved by spread() at its
    middle column (so neighbours differ by a pixel at most), its columns draped on the frame's back line, cut along
    the strap and the chest."""
    s = standing()
    sb, fb = s["back"], back_line(b)
    keep = cutter(b, op, key, donkey)
    out = {}
    plates = cached("lames", lame_plates)
    for n in reversed(range(len(LAMES))):
        lo, hi = LAMES[n]
        d = int(round(spread(s["hip"] - (lo + hi) / 2.0, b, cdx)))
        for (x, y), c in plates[n].items():
            xt = x + d
            yt = y + near(fb, b["hip"] - xt) - near(sb, s["hip"] - x)
            c = keep(xt, yt, c)
            if c is not None:
                out[(xt, yt)] = c
    return out


def place_rump(layer, b):
    """The rump layer into a frame: each column from its own hip, draped on the frame's back line."""
    sb, fb = standing()["back"], back_line(b)
    return {(b["hip"] - i, y + near(fb, i) - near(sb, i)): c for (i, y), c in layer.items()}


# ---------------------------------------------------------------- the tail guard

GUARD_SHARE = 0.55   # the guard covers this much of the tail from its root (the tuft hangs free below)
RING = 3             # each ring's length along the tail, pixels


TAIL_FROM = 1        # the tail's band starts this many columns in front of the hip (on the standing frame it lies
# down the rump's back edge over columns hip-1 to hip+2, then hangs free)


def tail_of(donkey, b):
    """The frame's tail: the donkey's pixels from TAIL_FROM in front of the hip column back, below the rump's top,
    joined to the rump's back edge (it swings in the walk; the hind legs are not joined to it there), and its root."""
    op = opaque(donkey)
    hip = b["hip"]
    level = min(b["top"][x] for x in range(hip - 8, hip + 1) if x in b["top"])
    root = (hip - TAIL_FROM, level + 2)
    seeds = [(x, y) for y in range(level + 3, level + 12) for x in range(hip - TAIL_FROM, hip + 4) if op(x, y)]
    tail, todo = set(seeds), list(seeds)
    while todo:
        x, y = todo.pop()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                q = (x + dx, y + dy)
                if q not in tail and op(*q) and q[0] >= hip - TAIL_FROM and level + 3 <= q[1] < b["ground"] - 4:
                    tail.add(q)
                    todo.append(q)
    return tail, root


def tail_guard(donkey, b):
    """The steel guard on this frame's tail: its pixels from the root to GUARD_SHARE of the way to the tip (the tip
    the pixel farthest from the root), cut into rings RING pixels long along the tail; each ring lit where it comes
    out from under the one above, darker where the next tucks under it, lighter on the side facing the light; an
    outline one pixel proud round the guard so it reads as a sleeve over the tail. The tuft hangs free below."""
    tail, root = tail_of(donkey, b)
    if not tail:
        return {}
    dist = {p: math.hypot(p[0] - root[0], p[1] - root[1]) for p in tail}
    length = max(dist.values())
    guard = {p for p in tail if dist[p] <= GUARD_SHARE * length}
    if not guard:
        return {}
    gx = sum(p[0] for p in guard) / float(len(guard))
    out = {}
    for p in guard:
        f = (dist[p] % RING) / RING
        step = 6 if f < 0.34 else 4 if f < 0.67 else 2
        if p[0] < gx - 0.5:
            step += 1                         # the side towards the light
        out[p] = STEEL[min(7, step)]
    for (x, y) in guard:
        for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if q not in guard and q not in out and q[0] >= b["hip"] - REAR and dist.get(q, 0) <= GUARD_SHARE * length + 1:
                out[q] = K
    return out


# ---------------------------------------------------------------- the breast plate (collar)

CHEST_ROWS = (45, 52)   # the breast plate's outline row and its last row (the collar's end, standing)
CHEST_ROUND = {45: 4, 46: 2, 47: 1}   # its top rows start this many columns further back: the top runs down into the
# front as the throat runs into the chest (a square corner there "doesn't look realistic": the user, 2026-11-02)


def strap_of(im):
    """The harness strap's pixels in a frame (the donkey's orange)."""
    px = im.load()
    return {(x, y) for y in range(96) for x in range(96) if px[x, y][3] and px[x, y][0] >= 90 and px[x, y][0] > 1.3 * px[x, y][1]}


def chest_layer():
    """The breast plate on the standing frame: the chest in front of the breast collar, from the throat down to the
    collar's end, one column proud of the chest in front; lit as a breast plate from the front and above, a rolled
    rim along its top, a brass boss. Behind the collar the side plate takes over (the collar is the seam)."""
    s = standing()
    op = s["op"]
    collar = breast_collar(load(S_KEY))
    left = {}
    for x, y in collar:
        left[y] = min(left.get(y, 99), x)
    y0, y1 = CHEST_ROWS
    mask = set()
    for y in range(y0, y1 + 1):
        f = min(x for x in range(28, 50) if op(x, y))
        for x in range(f - 1 + CHEST_ROUND.get(y, 0), left.get(y, f)):
            mask.add((x, y))
    cx, cy = 37.0, 48.5

    def breast(x, y):
        nx, ny = (x - cx) / 7.0, (y - cy) / 7.0
        return (nx, ny, math.sqrt(max(0.05, 1.0 - nx * nx - ny * ny)))

    px = shade(mask, breast, R=1.5, tilt=0.8, gain=1.1)
    for (x, y) in mask:
        if (x, y - 1) not in mask or (x - 1, y) not in mask:
            px[(x, y)] = K                              # the top and the front, one pixel proud of the chest
        elif (x, y - 2) not in mask:
            px[(x, y)] = STEEL[7] if x % RIVET_EVERY == 0 else STEEL[6]
        elif (x + 1, y) in collar:
            px[(x, y)] = STEEL[3]                       # the edge under the strap
    stamp(px, [".x.", "xzw", ".w."], 35, 47)            # the boss (its brass shades are not the trims' rivet shade)
    return px


GRAZE_CHEST_DY = 1  # graze 02-05: the chest's lower front sits one row lower (the frames' outlines); the strap turns
# there, so its best fit (+1, +2, +4, +4) says more than the chest moves


def chest_delta(key, donkey, b):
    """How the chest moves in a frame: the collar's column shift, and the row shift that lays the standing frame's
    breast collar best on this frame's (-1 in two walk frames, 0 elsewhere but in the graze)."""
    s = standing()
    dx = b["collar"] - s["collar"]
    mine = strap_of(donkey)
    best = (-1, 0)
    for dy in range(-4, 5):
        hit = sum(1 for (x, y) in s["strap"] if (x + dx, y + dy) in mine)
        if hit > best[0] or (hit == best[0] and abs(dy) < abs(best[1])):
            best = (hit, dy)
    if key.startswith("bdy_graze") and best[1] > 0:
        return dx, GRAZE_CHEST_DY
    return dx, best[1]


# ---------------------------------------------------------------- the head: an armet and its plume

# The user, 2026-11-02, after seeing the second cut in game: the face plate was "lacklustre"; "fill the donkey's face
# more ... like a knight armet type helmet but more ornate" (their references: armets with a round skull, a beaked
# visor pivoting on the cheek, the sight between brow and visor, brass rivets along the lower edge). The chanfron, its
# rivets and the brow's boss went with it.
HELMET = [  # top-left at (24, 30): the skull over the forehead and the poll, flush with the head's own top line (a
    ".............kkk",    # 30   round dome standing above it and upright at the front read as a bulge on the
    "...........kkxwk",    # 31   brain: the user's marked screenshot, 2026-11-02); the ear comes out behind it (rows
    ".........kkyxk",      # 32   30-35); a gilt comb along its top edge from the brow to the plume's holder (the
    "........kyy6k",       # 33   user's pick over the plain skull, 2026-11-02); darker towards the ear and the jaw
    ".......ky7665k",      # 34
    "......k6676554k",     # 35
    ".....k666655443k",    # 36
    "..k77yzyyxxxwwwk",    # 37  the gilt brow
    ".k776kkkkk.kk443k",   # 38  the sight (the eye's white shows through at (34, 38))
    ".k776666555444y3k",   # 39  the visor, closed over the whole muzzle (the nose's tip and the lips left out drew
    ".k87776665544yzxk",   # 40  the donkey's olive outline round the helm's mouth: the user's marked screenshot,
    ".k655k5k4k4333w3k",   # 41  2026-11-02): its upper face lit, the beak's crease over the nose, the breaths; its
    "..k444333333232k",    # 42  pivot, a brass rosette, on the cheek
    "..kyxyxyxyxxwxwk",    # 43  the roped gilt rim along the jaw
    "...kkkkkkkkkkkk",     # 44
]
HELMET_AT = (24, 30)
# turn 02 (the head starting to turn towards us, moved by its eye) shows more of its forehead: the skull's top-front
# stood 1-2 px inside it and the donkey's forehead showed above it ("we can partially see the donkey's head", the user,
# 2026-11-02). Its own skull rows (31-36 in HELMET's coordinates), the outline on its forehead, the gilt comb inside.
TURN2_HELMET = HELMET[:1] + [
    "........kkkkkxwk",    # 31
    ".......kyyxxwk",      # 32
    "......ky7766k",       # 33
    ".....ky777665k",      # 34
    ".....ky6676554k",     # 35
    "....kx666655443k",    # 36
] + HELMET[7:10] + [   # and its own visor rows (40-45): its muzzle hangs a pixel further forward and a row lower
    "k887776665544yzxk",   # 40   (the seal had painted those pixels flat dark under the visor: "that black something")
    "k6655k5k4k4333w3k",   # 41
    "k54444333333232k",    # 42
    ".k4333333322232k",    # 43
    ".kyxyxyxyxxwxwk",     # 44  the roped rim
    "..kkkkkkkkkkkk",      # 45
]
PLUME = [  # top-left at PLUME_AT: a short plume rising from a brass holder on the skull's top and falling back, in front
    "..qqq...",  # of the ear's root (the ear stays in view behind it); the cloth's colours, so it takes the Second tone
    ".qtuutq.",
    ".qttusrq",
    "..qtssrq",
    "..qtsrq.",
    "..qsrq..",
    "..qsrq..",
    "..qrq...",
    "..yx....",
    "..xw....",
]


PLUME_AT = (33, 22)         # its holder on the skull's top edge, forward of the poll (the user, 2026-11-02: "further
PLUME_ROOT = (36.0, 32.0)   # forward"); the holder's foot on the standing head
PLUME_TURN = 0.5            # a lowered head turns the plume only this much of its own turn: feathers still rise


def face_layer():
    return stamp({}, HELMET, *HELMET_AT)


def plume_layer():
    return stamp({}, PLUME, *PLUME_AT)


# graze 03-05 turn the head down about 45 degrees with its jaw open to the grass: their own drawings of the armet,
# top-left on the frame (the user's pick, 2026-11-02, over the turned standing helmet: its one-pixel brow, sight and
# rim broke into steps, and the open jaw hung out under it, sealed as a flat grey strip, "an extra gray part"), drawn
# on the frame's own head (its eye, its muzzle, its outline), the visor closed over the open jaw; graze 05 is graze
# 04's head with a chewing jaw.
GRAZE3_HELMET = [  # top-left at (20, 44): the lowered head seen more from the front and above, so its forehead
    "...kkkkkk",        # 44   stands tall over the face and the face is short: the skull narrow and tall from the ear
    "...ky765k",        # 45   down to the gilt brow, the sight across the face over the frame's own eye at (25, 53),
    "..ky7665k",        # 46   the visor over the muzzle with the beak to the left, the rosette at (28, 54), the
    "..ky76654k",       # 47   breaths, the roped rim along the jaw's bottom; flush with the head's own outline (the
    "..ky76554k",       # 48   standing helmet turned with the head stayed its full size over the smaller lowered head:
    "..ky66544k",       # 49   "the helm is expanding when he's bowing down his head", the user, 2026-11-02)
    "..ky65443k",       # 50
    ".k66544332k",      # 51
    ".kyzyyxxxwk",      # 52  the gilt brow
    "kkkkk.kky43k",     # 53  the sight, the eye's highlight showing through
    "k877665yzx3k",     # 54
    "k5k5k4k3w2k",      # 55  the breaths
    ".kyxyxwk",         # 56  the roped rim
    "..kkkkk",          # 57
]
GRAZE4_HELMET = [  # top-left at (22, 48): graze 03's helmet two columns right and four rows down, the face a column
    "...kkkkkk",        # 48   shorter in front of the eye at (27, 57)
    "...ky765k",        # 49
    "..ky7665k",        # 50
    "..ky76654k",       # 51
    "..ky76554k",       # 52
    "..ky66544k",       # 53
    "..ky65443k",       # 54
    "..k66544332k",     # 55
    ".kyzyyxxxxwk",     # 56  the gilt brow
    ".kkkk.kky43k",     # 57  the sight
    ".k87766yzx3k",     # 58
    ".k5k5k4kw22k",     # 59  the breaths
    "..kyxyxyxwk",      # 60  the roped rim, closed over the open mouth
    "...kkkkkkk",       # 61
]
GRAZE_HELMET = {  # the drawing, its top-left, and the plume's shift from where the turn puts it (its holder onto the
    "bdy_graze_left_03": (GRAZE3_HELMET, 20, 44, (2, 4)),   # drawn skull's top)
    "bdy_graze_left_04": (GRAZE4_HELMET, 22, 48, (2, 4)),
    "bdy_graze_left_05": (GRAZE4_HELMET, 22, 48, (2, 4)),
}


# ---------------------------------------------------------------- per frame

# How the head's drawings reach each frame (measured by tools/skins/donkey: the ear base, the eye, the muzzle tip):
#   moved: the head only moves (walk, ear, chew, lips, the first graze and turn frames, graze 02: the ear and the eye
#     keep their places against each other, only the jaw opens to the grass); turn 02 turns it a little towards us
#     and is moved by its eye (an affine map through the ear base, the eye and the muzzle tip sheared it apart);
#   warped: graze 03-05 turn the head down about 45 degrees (a rotation about the ear base onto the frame's ear-to-muzzle
#     line; the plume turns half as much, so it still rises; the armet has its own drawings there, GRAZE_HELMET);
#   drawn: turn 03 (three quarters) and turn 04-06 (the face towards us) have their own drawings.
WARP_ROTATE = {"bdy_graze_left_03", "bdy_graze_left_04", "bdy_graze_left_05"}
MOVE_BY_EYE = {"bdy_turn_left_02"}  # moved by the eye (its ear base moves the other way as the head turns)
HEAD_OVER_CHEST = {"bdy_graze_left_02", "bdy_turn_left_02"}  # moved heads that come down or round over the chest
DRAWN = {"bdy_turn_left_03", "bdy_turn_left_04", "bdy_turn_left_05", "bdy_turn_left_06"}
LAYERS = ("skirt", "lames", "rump", "chest", "tail", "head")
CACHE = {}


def cached(name, fn):
    if name not in CACHE:
        CACHE[name] = fn()
    return CACHE[name]


def pale_tip(im, ear):
    """The muzzle's tip: the pale pixel farthest from the ear base."""
    px = im.load()
    pts = [(x, y) for y in range(96) for x in range(96) if px[x, y][3] and px[x, y][1] > px[x, y][0] and px[x, y][1] >= 140]
    return max(pts, key=lambda q: (q[0] - ear[0]) ** 2 + (q[1] - ear[1]) ** 2)


def landmarks(key, im):
    (ax, ay, _), _ = HORNS[key]
    return (ax, ay), ANCHORS[key].get("eye"), pale_tip(im, (ax, ay))


def turn(a, src, dst):
    """The map (A, t) turning by `a` radians about `src` and putting `src` at `dst`."""
    c, s = math.cos(a), math.sin(a)
    A = ((c, -s), (s, c))
    return A, (dst[0] - (c * src[0] - s * src[1]), dst[1] - (s * src[0] + c * src[1]))


def apply(A, t, p):
    (a, b), (c, d) = A
    return a * p[0] + b * p[1] + t[0], c * p[0] + d * p[1] + t[1]


def rotation(src_a, src_b, dst_a, dst_b):
    """The angle and the map (A, t) turning the line src_a-src_b onto dst_a-dst_b about src_a (no scaling: the art
    keeps its size)."""
    a = math.atan2(dst_b[1] - dst_a[1], dst_b[0] - dst_a[0]) - math.atan2(src_b[1] - src_a[1], src_b[0] - src_a[0])
    return (a,) + turn(a, src_a, dst_a)


def warp(px, A, t, keep=None):
    """Pixel art through an affine map: every target pixel samples its 4x4 points back in the source and takes the
    colour most of them land on (empty if most land on nothing); `keep` (a set of colours) wins over others when
    present at all (the brass foot, the eye's hole edges: one-pixel details a majority would lose)."""
    (a, b), (c, d) = A
    det = a * d - b * c
    ia, ib, ic, id_ = d / det, -b / det, -c / det, a / det
    xs = [a * x + b * y + t[0] for x, y in px] + [a * (x + 1) + b * (y + 1) + t[0] for x, y in px]
    ys = [c * x + d * y + t[1] for x, y in px] + [c * (x + 1) + d * (y + 1) + t[1] for x, y in px]
    out = {}
    for ty in range(int(min(ys)) - 1, int(max(ys)) + 2):
        for tx in range(int(min(xs)) - 1, int(max(xs)) + 2):
            votes = {}
            empty = 0
            for sy in range(4):
                for sx in range(4):
                    u, v = tx + (sx + 0.5) / 4 - t[0], ty + (sy + 0.5) / 4 - t[1]
                    p = (math.floor(ia * u + ib * v), math.floor(ic * u + id_ * v))
                    col = px.get(p)
                    if col is None:
                        empty += 1
                    else:
                        votes[col] = votes.get(col, 0) + 1
            if not votes or empty > 8:
                continue
            best = max(votes, key=votes.get)
            if keep:
                for kc in keep:
                    if votes.get(kc, 0) >= 4:
                        best = kc
            out[(tx, ty)] = best
    return out


HEAD_ROWS = 44  # on the standing frame the head (and its jaw) is everything above this row left of the neck


def head_region():
    """The standing head's pixels (the jaw, the muzzle, the face, the ears): what the chest plate must stay behind
    where a frame brings the head down in front of the chest."""
    op = standing()["op"]
    return {(x, y) for y in range(96) for x in range(96) if op(x, y) and ((y <= HEAD_ROWS and x <= 43) or (y <= 34 and x <= 46))}


# turn 03 (three quarters) and turn 04 (the face towards us): their own drawings, top-left on the frame; turn 05/06
# are turn 04 a row higher (their ear bases are)
TURN3_HELMET = [  # top-left at (29, 35): the skull between the near ear and the far one lying back, flush with the
    ".....kkkkkkk",      # 35   head's top (no dome above it), the gilt comb from its top down its front, the gilt
    "....k67y7654k",     # 36   brow, the sight with the near eye's white at (38, 42), the beak's crease down the
    "..k567y76654k",     # 37   face's front (x 33, bending left as the muzzle comes towards us), the breaths and the
    ".k566x666554k",     # 38   rosette on the near cheek; the visor closed over the whole muzzle as in profile, the
    ".k456x655543k",     # 39   roped rim at its bottom
    ".k44x5554433k",     # 40
    ".kxyzyyxxxwwk",     # 41  the gilt brow
    ".kkkkkkkk.kkk",     # 42  the sight
    "k6778665544y3k",    # 43
    "k56785k5k4yzxk",    # 44
    ".k66744k333wk",     # 45
    "k5675443322k",      # 46
    "k56644332k",        # 47
    "k45543322k",        # 48
    "k4443322k",         # 49
    "k3443221k",         # 50
    ".kyxyxwk",          # 51  the roped rim
    "..kkkkk",           # 52
]
TURN3_PLUME = [  # top-left at (34, 26): between the side view's plume and the front's, bushy, falling back to the right,
    "..qqq..",       # its holder on the skull's top middle clear of the near ear (a four-pixel stick there looked "quite bad",
    ".qtuutq",       # the user, 2026-11-02)
    ".qttusq",
    "..qtsrq",
    "..qtsq.",
    "..qsrq.",
    "..qsq..",
    "..yx...",
    "..xw...",
]
FRONT_HELMET = [  # top-left at (30, 33): the skull between the ears, flush with the head's top, the gilt brow, the
    "....kkkkk",         # 33   gilt comb down its middle, the sight across the face with the eyes' glints at its
    "...k78y65k",        # 34   sides, the beak's crease down the middle (lit on the left), a rosette at each side
    "..k678y654k",       # 35   where the visor pivots, the breaths; the visor closed over the whole muzzle as in
    "..k677y6554k",      # 36   profile, the roped rim at its bottom
    ".k5667x65543k",     # 37
    "k55666x55443k",     # 38
    "k45566x54433k",     # 39
    "k44555w44332k",     # 40
    "kxyyzyyxxxwwk",     # 41  the gilt brow
    "kkk8kkkkkk8kk",     # 42  the sight
    "ky667785444xk",     # 43
    "kw566784433wk",     # 44
    ".k5k56743k3k",      # 45
    "..k4556332k",       # 46
    "..k4556322k",       # 47
    "..k3455322k",       # 48
    "..k3445222k",       # 49
    "..k2345221k",       # 50
    "..k334421k",        # 51
    "...kyxyxwk",        # 52  the roped rim
    "....kkkkk",         # 53
]
FRONT_PLUME = ["..qq..", ".qtuq.", ".qtusq", "..qsq.", "..qsrq", "..qrq.", "..yx..", "..xw.."]  # top-left at (34, 25)


def drawn_head(key):
    dy = 0 if key in ("bdy_turn_left_03", "bdy_turn_left_04") else -1
    px = {}
    if key == "bdy_turn_left_03":
        stamp(px, TURN3_HELMET, 29, 35)
        stamp(px, TURN3_PLUME, 34, 26)
    else:
        stamp(px, FRONT_HELMET, 30, 33 + dy)
        stamp(px, FRONT_PLUME, 34, 25 + dy)
    return px


# the head's region in the drawn frames (the chest plate stays behind it): per row, the last column of the head
DRAWN_HEAD = {
    "bdy_turn_left_03": {**{y: 38 for y in range(25, 47)}, 47: 37, 48: 36, 49: 35, 50: 35, 51: 34, 52: 33},
    "bdy_turn_left_04": {**{y: 40 for y in range(25, 46)}, 46: 38, 47: 38, 48: 37, 49: 37, 50: 37, 51: 37, 52: 36},
}


def head_place(key, donkey):
    """The head's pixels for a frame and the frame's head region (None where the head stays clear of the chest)."""
    face = cached("face", face_layer)
    if key in DRAWN:
        rows = DRAWN_HEAD["bdy_turn_left_03" if key == "bdy_turn_left_03" else "bdy_turn_left_04"]
        dy = 0 if key in ("bdy_turn_left_03", "bdy_turn_left_04") else -1
        region = {(x, y + dy) for y, last in rows.items() for x in range(0, last + 1)}
        # sealed on the visor's outer side only (turn 05's muzzle is not quite turn 04's a row up; on the inner side
        # the strap and the neck show as they should)
        left = {(x, y) for y in range(40, 57) for x in range(24, 34)}
        return sealed(drawn_head(key), donkey, left), region, ("drawn",)
    if key in WARP_ROTATE:
        s_ear, _, s_tip = landmarks(S_KEY, load(S_KEY))
        ear, _, tip = landmarks(key, donkey)
        a, A, t = rotation(s_ear, s_tip, ear, tip)
        region = set(warp({p: K for p in cached("head_region", head_region)}, A, t)) | neck_front(key, donkey)
        art, ax, ay, (sx, sy) = GRAZE_HELMET[key]
        px = stamp({}, art, ax, ay)   # drawn on the frame's own head: nothing to seal (it would shut the eye)
        foot = apply(A, t, PLUME_ROOT)
        PA, Pt = turn(a * PLUME_TURN, PLUME_ROOT, (foot[0] + sx, foot[1] + sy))
        px.update(warp(cached("plume", plume_layer), PA, Pt, {BRASS[3]}))
        return px, region, ("warp", A, t, a)
    if key in MOVE_BY_EYE:
        s_eye, eye = ANCHORS[S_KEY]["eye"], ANCHORS[key]["eye"]
        hd = (round(eye[0] - s_eye[0]), round(eye[1] - s_eye[1]))
    else:
        hd = HEAD_MOVE.get(key) or head_delta(key)
    # graze 02 and turn 02 bring the head down or round over the chest plate; every other moved frame keeps it clear
    # (graze 01 and turn 01 are the standing picture: their armour must be the standing one, pixel for pixel)
    region = {(x + hd[0], y + hd[1]) for x, y in cached("head_region", head_region)} if key in HEAD_OVER_CHEST else None
    art = stamp({}, TURN2_HELMET, *HELMET_AT) if key in MOVE_BY_EYE else face
    px = {(x + hd[0], y + hd[1]): c for (x, y), c in art.items()}
    px = sealed(px, donkey, {(x + hd[0], y + hd[1]) for x, y in mouth_box() | set(EXTRA_SEAL.get(key, ()))})
    for p in POKE_ERASE.get(key, ()):   # the donkey's own pixels poking past the helm's outline: taken off the donkey
        px[p] = ERASE
    px_, py_ = PLUME_SHIFT.get(key, (0, 0))
    px.update({(x + hd[0] + px_, y + hd[1] + py_): c for (x, y), c in cached("plume", plume_layer).items()})
    return px, region, ("move", hd)


ERASE = (255, 0, 255)   # the erase colour (docs/skins.md "Erasing from the donkey"): clears the donkey's pixel there
HEAD_MOVE = {"bdy_lips_left_05": (0, 0)}   # lips 05's ear base rises a row but its jaw doesn't: moved up with the ear, the helm
# showed a row more of the throat under its rim for one frame (the gate review, 2026-11-02); it stays as in lips 04
POKE_ERASE = {  # frame pixels of the donkey's own outline left outside the helm by a frame's small head move (lips 04-06: the
    "bdy_graze_left_02": [(25, 51), (26, 51)],   # forehead lifts a pixel; graze 02: the chin under the visor), erased:
    "bdy_lips_left_04": [(31, 33), (32, 32), (34, 31)],   # sealed in the helm's outline they stood as bumps, left they
    "bdy_lips_left_05": [(31, 33), (32, 32), (33, 31), (34, 31)],   # made a double stair of outline (the gate review);
    "bdy_lips_left_06": [(34, 31)],                                 # turn 02: the ear gone behind the plume left a
    "bdy_turn_left_02": [(39, 23), (40, 28), (40, 29), (39, 30), (40, 30)],   # 1 px strip down its side and its tip
}
PLUME_SHIFT = {"bdy_turn_left_02": (-1, -1)}   # turn 02 moves the helmet 2 px onto the ear (the eye moves, the ear base
# goes the other way): the ear peeked out at the plume's tip and down its left side (a fresh review, 2026-11-02)
MOUTH_BOX = (16, 34, 32, 48)   # x0, y0, x1, y1 on the standing frame: in front of and under the visor's front half
EXTRA_SEAL = {  # standing-frame pixels outside the box a frame's own head pushes past the helm: graze 02's poll showed at
    "bdy_graze_left_02": [(34, 31)],   # the plume's foot (a fresh review, 2026-11-02; its chin under the visor is erased,
}                                      # POKE_ERASE)


def mouth_box():
    x0, y0, x1, y1 = MOUTH_BOX
    return {(x, y) for y in range(y0, y1) for x in range(x0, x1)}


def sealed(px, donkey, zone):
    """The helm's mouth kept closed where a frame's own mouth moves (chew, lips, the turn): the donkey's pixels in
    `zone` within two pixels of the helmet take its outline (its dark steel inside), so no olive outline or lip of the
    donkey shows round the helm's edge (the user's marked screenshot, 2026-11-02). The standing frame has none."""
    op = opaque(donkey)
    near = lambda x, y: any((x + i, y + j) in px for i in range(-2, 3) for j in range(-2, 3))
    add = {(x, y) for (x, y) in zone if op(x, y) and (x, y) not in px and near(x, y)}
    out = dict(px)
    for (x, y) in add:
        edge = any(not op(x + i, y + j) and (x + i, y + j) not in px for i, j in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        out[(x, y)] = K if edge else STEEL[2]
    return out


SCRAP = 8   # a piece of a plate the head's region cut off from the rest, smaller than this, is dropped


def drop_scraps(px, op):
    """A plate cut behind the head (graze 02, turn 02: the head coming down or round over the chest) left loose bits of
    1-3 pixels standing in the neck (a fresh review, 2026-11-02: "a stray light-grey 2-px cluster") and, in graze 02,
    a 9-pixel corner hanging in the air before the neck with no outline on its cut edge (the gate review): the
    pieces (4-connected) smaller than SCRAP go, and those mostly off the donkey (`op`); an uncut plate is one piece
    on the body and stays whole."""
    left, out = set(px), {}
    while left:
        part, todo = set(), [left.pop()]
        while todo:
            x, y = todo.pop()
            part.add((x, y))
            for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if q in left:
                    left.discard(q)
                    todo.append(q)
        if len(part) >= SCRAP and 2 * sum(1 for p in part if op(*p)) > len(part):
            out.update({p: px[p] for p in part})
    return out


LOW_HEAD = {  # under the breast collar the lowered head itself: (first row, last row, last column)
    "bdy_graze_left_03": (53, 60, 32),
    "bdy_graze_left_04": (55, 62, 34),
    "bdy_graze_left_05": (55, 62, 34),
}


def neck_front(key, donkey):
    """Where the neck hangs in front of the chest (graze 03-05): everything left of the breast collar on its rows, and
    the lowered head under it."""
    left = {}
    for x, y in breast_collar(donkey):
        left[y] = min(left.get(y, 99), x)
    region = {(x, y) for y, x0 in left.items() for x in range(0, x0)}
    y0, y1, x1 = LOW_HEAD[key]
    region |= {(x, y) for y in range(y0, y1 + 1) for x in range(0, x1 + 1)}
    return region


def frame(key):
    """One frame's pack picture and its layers (for the .aseprite): the skin's pixels only."""
    donkey = load(key)
    op = opaque(donkey)
    layers = {n: {} for n in LAYERS}
    b = body_of(key, donkey)
    head, region, how = head_place(key, donkey)
    behind = (lambda p: p in region) if region else (lambda p: False)
    if b is not None:
        cdx, cdy = chest_delta(key, donkey, b)
        # the head turned towards us (turn 03-06) hangs its muzzle in front of the body: the lames and the skirt stay
        # behind it there (the front lame covered it in turn 04-06); elsewhere they are as the user approved them
        front = behind if how[0] == "drawn" else (lambda p: False)
        layers["skirt"] = {p: c for p, c in place_skirt(b, op, key, donkey, cdx).items() if not front(p)}
        layers["lames"] = {p: c for p, c in place_lames(b, op, key, donkey, cdx).items() if not front(p)}
        layers["rump"] = place_rump(cached("rump", rump_layer), b)
        chest = {(x + cdx, y + cdy): c for (x, y), c in cached("chest", chest_layer).items() if not behind((x + cdx, y + cdy))}
        layers["chest"] = drop_scraps(chest, op)
        layers["tail"] = tail_guard(donkey, b)
    layers["head"] = head
    im = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    px = im.load()
    for name in LAYERS:
        for (x, y), c in layers[name].items():
            if 0 <= x < 96 and 0 <= y < 96:
                px[x, y] = c + (255,)
    return donkey, im, layers


def hexes(colours):
    return " ".join(f"{r:02X}{g:02X}{b:02X}" for (r, g, b) in colours)


def skin_cfg():
    return (
        "# Armored: plate barding drawn OVER the donkey (docs/skins.md): an armet (a closed helmet: a round skull, a gilt\n"
        "# brow, the sight, a beaked visor on a brass rosette, a roped gilt rim) with a plume, a chest plate, steel lames, a\n"
        "# rump plate, a cloth skirt and a steel guard on the tail; ears, tail and legs stay the donkey's. Generated by\n"
        "# tools/skins/armored/make_armored.py (the pictures hold only our pixels; the few the donkey's head pokes past the\n"
        "# helm in five frames are erased, FF00FF).\n"
        "# Our own art; nothing of the game's.\n"
        "[Skin]\nformat = 1\nname = Armored\n"
        "description = Plate barding with a plumed knight's helmet, steel lames, a cloth skirt and a steel tail guard. The skirt and the plume follow your second tone.\n"
        "author = dreas\nversion = 1.0\nbase = donkey\ncolours = own\nlayer = over\ncanvas = 96 96\n\n"
        "# The steel and the brass are kept as drawn (always steel, as the game's guards wear it); the skirt and the plume\n"
        "# follow the Second tone (the Muzzle row), so the harness colour reaches only the donkey's own harness (the user,\n"
        "# 2026-11-02: under Harness the cloth took the harness colour and the Second tone did nothing on this skin).\n"
        f"[Colors]\nMuzzle = {hexes(CLOTH)}\n"
    )


def write_aseprite(frames):
    groups = [("static", 1), ("walk", 8), ("ear", 5), ("graze", 5), ("chew", 3), ("lips", 13), ("turn", 6), ("head", 3)]
    tags, cur = [], 0
    for name, n in groups:
        tags.append((name, cur, cur + n - 1)); cur += n
    cels, durs = [], []
    for k in ORDER:
        donkey, _, layers = frames[k]
        cel = {0: donkey}
        for i, name in enumerate(LAYERS):
            im = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
            px = im.load()
            for (x, y), c in layers[name].items():
                if 0 <= x < 96 and 0 <= y < 96:
                    px[x, y] = c + (255,)
            cel[i + 1] = im
        cels.append(cel)
        durs.append(100 if "walk" in k else 150)
    layer_list = [("donkey (game)", ase_write.VISIBLE)] + [(n, ase_write.VISIBLE | ase_write.EDITABLE) for n in LAYERS]
    ase_write.write(os.path.join(OUT, "armored.aseprite"), 96, 96, layer_list, cels, tags, durs)


def main(argv):
    os.makedirs(OUT, exist_ok=True)
    frames = {k: frame(k) for k in ORDER}
    pack = {k: v[1] for k, v in frames.items()}
    base = {k: v[0] for k, v in frames.items()}
    targets = [os.path.join(OUT, "pack")] + ([ASSETS] if "--no-assets" not in argv else [])
    for t in targets:
        os.makedirs(t, exist_ok=True)
        for f in os.listdir(t):
            if f.endswith(".png") and f.startswith(("bdy_", "hed_")):
                os.remove(os.path.join(t, f))
        for k, im in pack.items():
            im.save(os.path.join(t, k + ".png"))
        cfg_path = os.path.join(t, "skin.cfg")
        if t != ASSETS or "--cfg" in argv or not os.path.exists(cfg_path):
            with open(cfg_path, "w", encoding="utf-8", newline="\r\n") as f:
                f.write(skin_cfg())
    write_aseprite(frames)
    rider = review.load_rider(os.path.join(game_dir(), "BepInEx", "GK2-asset-probe"))
    strips = review.write_all(OUT, pack, base=base, rider=rider, seat_row=review.DONKEY_SEAT_ROW)
    drawn = sum(sum(1 for _ in im.get_flattened_data() if _[3]) for im in pack.values())
    print(f"{len(ORDER)} frames, {drawn} drawn pixels; pack -> {', '.join(targets)}; strips -> {strips}")
    layers, anchors = {}, {}
    for k, (donkey, _, Lr) in frames.items():
        layers[k] = Lr
        b = body_of(k, donkey)
        (ax, ay, _), _ = HORNS[k]
        anchors[k] = {"head": (ax, ay)}
        if b is not None:
            anchors[k]["skirt"] = (b["collar"], min(b["top"].values()))
            anchors[k]["lames"] = (b["collar"], min(b["top"].values()))
            anchors[k]["rump"] = (b["hip"], min(b["top"].values()))
            anchors[k]["chest"] = (b["collar"], 0)
    for line in review.motion_check(layers, anchors):
        print("motion:", line)
    bad = review.phase_check(layers, anchors, {BRASS[3]: RIVET_EVERY}, skip={"head", "skirt", "lames", "tail"})   # the skirt stretches and the lames shift apart by
    # design; the guard follows the tail
    for line in bad:
        print("phase:", line)
    if any(l.startswith("FAIL") for l in bad):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
