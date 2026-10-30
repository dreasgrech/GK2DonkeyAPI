"""Armored (donkey/saddleup-donkey-skins.armored): plate barding drawn OVER the donkey (docs/sprite-tools.md "Making a
skin"), the third skin of the Donkey Skin Pack. From the references (a war horse in plate, knights' chargers with a
plumed chanfron): a chanfron (face plate with an eye hole and a forehead spike), a plume at the poll, a crinet (neck
lames), a peytral (chest plate), flank lames over the barrel and a crupper over the rump; ears, tail and legs stay the
donkey's (nothing erased). Writes a PNG per frame into
packs/GK2SaddleUpDonkeySkinPack/gk2mounts/donkey/skins/saddleup-donkey-skins.armored/ and the review pictures into out\\
(strips\\<animation>.png: look at every strip before the game does; preview.png; pack\\sheet.png; armored.aseprite).

    python -I tools/skins/armored/make_armored.py              # the pack's pictures + out\\
    python -I tools/skins/armored/make_armored.py --no-assets  # out\\ only
    python -I tools/skins/armored/make_armored.py --cfg        # rewrite the pack's skin.cfg too (by hand otherwise)

How each piece is anchored (tools/skins/donkey/anatomy.py; the measurements that decided it: the head is a pure
translation of the standing one in the walk, ear, chew and lips frames, rotates down in graze 02-05 and towards the
camera in turn 02-06; the hip stays at column 65/66 in every frame but the turn's):
  - chanfron, spike, plume: a band along the head's AXIS from the poll (the ear bases) to the muzzle tip, cut once on
    the standing frame and moved with the head where the head only moves, cut again on the frame's own axis where
    the head rotates (graze 02-05, turn 02-03), a frontal pattern where it faces the camera (turn 04-06);
  - crinet: lames fitted to the frame's own neck-top outline between the rear ear and the collar (the neck stretches
    in the walk: a head-anchored neck plate sank 6 rows into it);
  - peytral: fitted to the frame's own chest underside (the chest moves with the neck, 2-4 px, not with the hip);
  - flank lames and crupper: hung from the HIP exactly as Royal Parade's cloth (the same width back from the hip,
    each column's top on the frame's own back line, the standing frame's depth), so they ride the walk's bob and
    the graze's dip without sliding; the seams are straight rows under the back level.
"""
import math, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "donkey"))
from anatomy import ORDER, load, HORNS, ANCHORS, body, belly, head_delta, top_outline, run_bottom
import ase_write
import review
from gamedir import game_dir

REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
ASSETS = os.path.join(REPO, "packs", "GK2SaddleUpDonkeySkinPack", "gk2mounts", "donkey", "skins", "saddleup-donkey-skins.armored")
OUT = os.path.join(HERE, "out")

# colours (none is a donkey atlas colour: check_skin says so). The steel is the skin's Muzzle (the page's Second tone
# row: black iron, bronze, gold at the player's choice, as Hellfire's lava), the reds its Harness (the plume and the
# padded cloth under the plates follow the harness colour, as Royal Parade's cloth), the outline Dark; the brass trim
# is kept as drawn (as Royal Parade's gold).
STEEL_L, STEEL, STEEL_D, STEEL_DD = (206, 212, 222), (150, 158, 172), (96, 104, 122), (58, 62, 78)
OUTLINE = (26, 28, 38)
BRASS_L, BRASS, BRASS_D = (240, 200, 90), (206, 156, 46), (140, 98, 26)
RED_L, RED, RED_D = (226, 58, 80), (196, 30, 56), (150, 18, 44)
STEELS = [STEEL_L, STEEL, STEEL_D, STEEL_DD]
REDS = [RED_L, RED, RED_D]
BRASSES = [BRASS_L, BRASS, BRASS_D]

# the pieces' measures (art pixels)
CHANFRON_ABOVE = 3.0    # the face plate reaches this far above the head's axis (the forehead; the ears stay out)
CHANFRON_BELOW = 6.0    # and this far below it (the jaw stays bare)
CHANFRON_END = 0.80     # of the poll-to-muzzle length: the nostrils stay bare
CRINET_DEPTH = 4        # rows of neck lame under the neck's top line
CRINET_LAME = 2         # columns per neck lame
PEYTRAL_DEPTH = 7       # rows of chest plate above the chest's underside
PEYTRAL_TOP = 46        # no higher than this row (the throat stays bare)
PEYTRAL_CAP = 58        # no lower than this row (the legs stay bare)
PEYTRAL_COLS = 10       # columns of chest plate in front of the collar
HEM_ABOVE = 2           # the flank lames end this many rows above the belly (the belly and the legs show)
GAP = 0                 # columns between the lames' rear edge and the hip (the crupper takes over there)
CRUPPER_COLS = 4        # columns of rump plate behind the hip
CRUPPER_LOW = 54        # the crupper no lower than this row (the tail root stays bare)
SEAM_ROWS = (5, 10)     # the lame seams this many rows under the back level
RIVET_EVERY = 3

# the frames whose head is not the standing head moved: cut on their own axis (graze, the turn's three-quarter
# views) or a frontal pattern (the turn facing the camera). Measured by tools/skins/donkey/measure.py: everywhere
# else the eye, the ear bases and the muzzle move by one (dx, dy).
ROTATED = {"bdy_graze_left_02", "bdy_graze_left_03", "bdy_graze_left_04", "bdy_graze_left_05", "bdy_turn_left_02", "bdy_turn_left_03"}
FRONTAL = {"bdy_turn_left_04", "bdy_turn_left_05", "bdy_turn_left_06"}


def opaque(im):
    px = im.load()
    return lambda x, y: 0 <= x < 96 and 0 <= y < 96 and px[x, y][3] > 0


def poll(key):
    """The point between the ear bases, and the forward ear base's column."""
    (ax, ay, _), (bx, by, _) = HORNS[key]
    return (ax + bx) / 2.0, (ay + by) / 2.0, min(ax, bx)


def muzzle(op):
    """The muzzle tip: the leftmost opaque pixel (the head faces left; not for a head facing the camera)."""
    pts = [(x, y) for x in range(96) for y in range(96) if op(x, y)]
    return min(pts)


def head_axis(key, op):
    """P (just in front of the forward ear base), N (the muzzle tip), the unit axis u (P towards N) and the outward
    normal n (away from the jaw, towards the forehead)."""
    px_, py_, front = poll(key)
    P = (front - 1.0, py_ - 1.0)
    N = muzzle(op)
    dx, dy = N[0] - P[0], N[1] - P[1]
    L = math.hypot(dx, dy)
    u = (dx / L, dy / L)
    n = (u[1], -u[0])          # a perpendicular; make it point up (negative y) = the forehead's side
    if n[1] > 0:
        n = (-n[0], -n[1])
    return P, N, u, n, L


def chanfron_mask(key, op, eye):
    """The face plate: head pixels along the axis from the poll to CHANFRON_END of the way to the muzzle, within
    CHANFRON_ABOVE of the axis on the forehead side and CHANFRON_BELOW on the cheek side, with a hole for the eye."""
    P, N, u, n, L = head_axis(key, op)
    mask = set()
    for x in range(12, 60):
        for y in range(12, 70):
            if not op(x, y):
                continue
            rx, ry = x - P[0], y - P[1]
            t = rx * u[0] + ry * u[1]
            s = rx * n[0] + ry * n[1]
            if -1.0 <= t <= CHANFRON_END * L and -CHANFRON_BELOW <= s <= CHANFRON_ABOVE:
                if eye and abs(x - eye[0]) <= 1 and abs(y - eye[1]) <= 1:
                    continue
                mask.add((x, y))
    return mask, P, u, n


def spike_pixels(op, P, u, n):
    """The forehead spike: from the head's top outline two steps in front of the poll, four pixels along the
    outward normal, brass base, steel shaft, brass tip."""
    cx = int(round(P[0] + 2 * u[0]))
    ys = [y for y in range(96) if op(cx, y) and abs(y - P[1]) < 10]
    if not ys:
        return {}
    base = (cx, min(ys))
    out = {base: BRASS}
    for k in range(1, 5):
        p = (int(round(base[0] + k * n[0])), int(round(base[1] + k * n[1])))
        out[p] = BRASS_L if k == 4 else STEEL_L if k >= 2 else STEEL
    return out


PLUME = [  # the tuft in head coordinates: rows from the base up, cells as (back offset, colour); leans back as it rises
    [(0, RED_D)],
    [(0, RED), (1, RED_D)],
    [(0, RED_L), (1, RED), (2, RED_D)],
    [(0, RED_L), (1, RED), (2, RED)],
    [(1, RED_L), (2, RED), (3, RED_D)],
    [(1, RED_L), (2, RED)],
    [(2, RED_L), (3, RED)],
    [(3, RED_L)],
]


def plume_pixels(key, up, back):
    """The plume at the poll between the ears: a red feather tuft rising from the point between the ear bases and
    leaning back (up, back: this head's own up and back directions, unit vectors). Drawn at half steps so a slanted
    tuft has no holes."""
    px_, py_, _ = poll(key)
    base = (px_, py_ - 1)
    out = {}
    for h, cells in enumerate(PLUME):
        for b, col in cells:
            for hh in (h, h + 0.5):
                x = int(round(base[0] + hh * up[0] + b * back[0]))
                y = int(round(base[1] + hh * up[1] + b * back[1]))
                out.setdefault((x, y), col)
    return out


FRONT_CHANFRON = [  # the head facing the camera (turn 04-06): the plume up from the poll, a brass spike tip (it points
    "...l...",      # at the camera), the brow plate between the eyes, the band down the nose to the muzzle; column 3
    "..rRl..",      # is the poll
    "..rRRl.",
    "...rR..",
    "...b...",
    "OOOSOOO",
    "OLLSLLO",
    "OSSSSSO",
    ".OSSSO.",
    ".ODSDO.",
    ".ODDDO.",
    ".OBbBO.",
    "..OOO..",
]
FRONT_PAL = {"O": OUTLINE, "S": STEEL, "L": STEEL_L, "D": STEEL_D, "B": BRASS, "b": BRASS_L, "R": RED, "r": RED_D, "l": RED_L}


def frontal_pixels(key):
    px_, py_, _ = poll(key)
    cx, cy = int(round(px_)) - 3, int(round(py_)) - 4
    out = {}
    for j, row in enumerate(FRONT_CHANFRON):
        for i, ch in enumerate(row):
            if ch in FRONT_PAL:
                out[(cx + i, cy + j)] = FRONT_PAL[ch]
    return out


def plate(mask, anchor_x, hem_brass=False, seams=(), rivets=True, top_light=True):
    """Pixels of one plate from its mask: outline round it, steel inside with a light first row and a dark last row
    per column, seams (rows of deep steel with a lit row under them and rivets on the lit row), a brass hem. Every
    repeating pattern (the rivets, the hem's alternation) is phased to anchor_x, the column the piece moves with (the
    hip, the collar): phased to the canvas it crawled one column along the plate whenever the anchor shifted between
    frames (review.phase_check catches it)."""
    inside = lambda p: p in mask
    out = {}
    cols = {}
    for x, y in mask:
        cols.setdefault(x, []).append(y)
    for x, y in mask:
        edge = not all(inside((x + dx, y + dy)) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        ys = cols[x]
        top, bot = min(ys), max(ys)
        if edge and not (hem_brass and y == bot):
            out[(x, y)] = OUTLINE
        elif hem_brass and y == bot:
            out[(x, y)] = BRASS_L if (x - anchor_x) % 2 == 0 else BRASS
        elif y in seams:
            out[(x, y)] = STEEL_DD
        elif y - 1 in seams or (top_light and y == top + 1):
            out[(x, y)] = STEEL_DD if rivets and (x - anchor_x) % RIVET_EVERY == 0 else STEEL_L
        elif y == bot - 1 or (hem_brass and y == bot - 2):
            out[(x, y)] = STEEL_D
        else:
            out[(x, y)] = STEEL
    return out


def crinet(key, op, b):
    """Neck lames between the rear ear base and the collar, CRINET_DEPTH rows under the neck's own top line, a seam
    every CRINET_LAME columns (the rear lame overlaps the next)."""
    _, py_, _ = poll(key)
    rear = max(HORNS[key][0][0], HORNS[key][1][0]) + 1
    # the neck's own top line: the top of the opaque run through a row below the ear bases (the ears stand on it;
    # a column's first opaque pixel there is an ear tip, which took the first draft's lames)
    top = {}
    for x in range(rear, b["collar"]):
        y = int(round(py_)) + 4
        if not op(x, y):
            continue
        while op(x, y - 1):
            y -= 1
        top[x] = y
    if len(top) < 2:
        return {}
    mask = {(x, y) for x in top for y in range(top[x], top[x] + CRINET_DEPTH) if op(x, y)}
    px = plate(mask, rear, rivets=False, top_light=True)
    x0 = min(top)
    for (x, y), c in list(px.items()):
        if (x - x0) % CRINET_LAME == CRINET_LAME - 1 and c in (STEEL, STEEL_L, STEEL_D) and y > top[x]:
            px[(x, y)] = STEEL_DD if y == top[x] + 1 else STEEL_D
    return px


def peytral(key, op, b):
    """The chest plate: PEYTRAL_COLS columns in front of the collar, PEYTRAL_DEPTH rows up from the chest's own
    underside (capped so the legs and the throat stay bare), a brass rim along its lower edge and a row of red
    padding under it."""
    collar = b["collar"]
    xs = list(range(collar - PEYTRAL_COLS, collar + 1))
    # the chest's underside per column: the run through row 48; a run on to the ground is a leg (bridged from its
    # neighbours, as belly() does), then a three-column median so one column never steps out of the plate
    bottoms = {}
    for x in xs:
        bot = run_bottom(op, x, 48)
        if bot is not None and bot <= PEYTRAL_CAP:
            bottoms[x] = bot
    known = sorted(bottoms)
    if len(known) < 4:
        return {}
    for x in xs:
        if x not in bottoms:
            left = max((k for k in known if k < x), default=None)
            right = min((k for k in known if k > x), default=None)
            if left is None and right is None:
                continue
            bottoms[x] = bottoms[right] if left is None else bottoms[left] if right is None else round(bottoms[left] + (bottoms[right] - bottoms[left]) * (x - left) / (right - left))
    smooth = {x: sorted(bottoms[k] for k in (x - 1, x, x + 1) if k in bottoms)[len([k for k in (x - 1, x, x + 1) if k in bottoms]) // 2] for x in bottoms}
    mask = set()
    for x, bot in smooth.items():
        for y in range(max(PEYTRAL_TOP, bot - PEYTRAL_DEPTH), bot):
            if op(x, y):
                mask.add((x, y))
    if len(mask) < 12:
        return {}
    px = plate(mask, collar, hem_brass=True, rivets=True)
    for x in {p[0] for p in mask}:
        bot = max(y for (xx, y) in mask if xx == x)
        if op(x, bot + 1) and (x, bot + 1) not in px:
            px[(x, bot + 1)] = RED if (x - collar) % 2 else RED_D
    return px


PROFILE = None


def standing_profile():
    """The flank lames' depth per column counted back from the hip, from the standing frame (as the cloth's)."""
    b = body("bdy_static_left", load("bdy_static_left"))
    top, hip, collar = b["top"], b["hip"], b["collar"]
    bel = belly(b)
    width = hip - collar - 1
    depth = {}
    for i in range(width):
        x = hip - GAP - i
        if x not in top:
            continue
        win = sorted(bel[k] for k in (x - 1, x, x + 1) if k in bel)
        depth[i] = win[len(win) // 2] - HEM_ABOVE - top[x]
    smooth = {}
    for i in depth:
        win = sorted(depth[k] for k in range(i - 2, i + 3) if k in depth)
        smooth[i] = win[len(win) // 2]
    return width, smooth


def barding(key, op, b):
    """The flank lames hung from the hip (the cloth's rule) and the crupper behind it, one plate with two straight
    seams under the back level, a brass hem with red padding under the lames."""
    global PROFILE
    if PROFILE is None:
        PROFILE = standing_profile()
    width, depth = PROFILE
    top, hip, collar = b["top"], b["hip"], b["collar"]
    w = min(width, hip - collar - 1)
    cols = [(hip - GAP - i, i) for i in range(w) if (hip - GAP - i) in top and i in depth]
    if len(cols) < 4:
        return {}
    rear_half = sorted(top[x] for x, _ in cols[: max(1, len(cols) // 2)])
    back_level = rear_half[len(rear_half) // 2]
    mask = set()
    for x, i in cols:
        t = max(top[x], back_level - 1)
        for y in range(t, t + depth[i] + 1):
            if op(x, y):
                mask.add((x, y))
    # the crupper: the rump's columns behind the hip, the back line down to CRUPPER_LOW
    rump = top_outline(op, hip + 1, hip + CRUPPER_COLS)
    for x, t in rump.items():
        for y in range(t, CRUPPER_LOW + 1):
            if op(x, y):
                mask.add((x, y))
    seams = tuple(back_level + r for r in SEAM_ROWS)
    px = plate(mask, hip, hem_brass=True, seams=seams, rivets=True)
    # the red padding under the lames' hem (not under the crupper, whose edge is the rump)
    for x, _ in cols:
        bot = max(y for (xx, y) in mask if xx == x)
        if op(x, bot + 1) and (x, bot + 1) not in px:
            px[(x, bot + 1)] = RED if (x - hip) % 2 else RED_D
    return px


CRINET = False  # the neck lames: drawn and dropped (2026-10-30). The donkey's neck top is three columns between the
# rear ear and the collar, under the rider's reins arm: the lames came out as one dark blob, clutter at 1x. The
# chanfron ends at the poll and the barding starts at the collar; the strap between them is the donkey's own.
LAYERS = ("barding", "peytral", "crinet", "chanfron") if CRINET else ("barding", "peytral", "chanfron")
SIDE = None  # the standing head's pieces, moved with the head where it only moves


def head_pieces(key, op):
    """The chanfron with its spike and plume for one frame."""
    global SIDE
    if key in FRONTAL:
        return frontal_pixels(key)
    if key in ROTATED:
        return side_pieces(key, op)
    if SIDE is None:
        SIDE = side_pieces("bdy_static_left", opaque(load("bdy_static_left")))
    dx, dy = head_delta(key)
    return {(x + dx, y + dy): c for (x, y), c in SIDE.items()}


def side_pieces(key, op):
    """The chanfron, spike and plume cut on this frame's own head axis."""
    eye = ANCHORS[key].get("eye")
    mask, P, u, n = chanfron_mask(key, op, eye)
    px = plate(mask, int(round(P[0])), rivets=False)
    px.update(spike_pixels(op, P, u, n))
    px.update(plume_pixels(key, up=n, back=(-u[0], -u[1])))
    return px


def frame(key):
    """One frame's pack picture and its layers (for the .aseprite): the skin's pixels only."""
    donkey = load(key)
    op = opaque(donkey)
    layers = {"barding": {}, "peytral": {}, "chanfron": {}}
    b = body(key, donkey)
    if b is not None:
        layers["barding"] = barding(key, op, b)
        layers["peytral"] = peytral(key, op, b)
        if CRINET:
            layers["crinet"] = crinet(key, op, b)
    layers["chanfron"] = head_pieces(key, op)
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
        "# Armored: plate barding drawn OVER the donkey (docs/skins.md): a chanfron with an eye hole and a forehead spike, a\n"
        "# plume at the poll, neck lames, a chest plate, flank lames and a crupper; ears, tail and legs stay the donkey's.\n"
        "# Generated by tools/skins/armored/make_armored.py (the pictures hold only our pixels; nothing is erased). Our own\n"
        "# art; nothing of the game's.\n"
        "[Skin]\nformat = 1\nname = Armored\n"
        "description = Plate barding with a plumed chanfron, a chest plate and flank lames. The plume and the padding follow your harness colour, the steel your second tone.\n"
        "author = dreas\nversion = 1.0\nbase = donkey\ncolours = own\nlayer = over\ncanvas = 96 96\n\n"
        "# The steel is the Second tone (black iron, bronze or gold at the player's choice, as Hellfire's lava), the plume\n"
        "# and the padded cloth under the plates follow the harness colour (as Royal Parade's cloth), the outline is Dark;\n"
        "# the brass trim is kept as drawn (as Royal Parade's gold).\n"
        f"[Colors]\nDark = {hexes([OUTLINE])}\nMuzzle = {hexes(STEELS)}\nHarness = {hexes(REDS)}\n"
    )


def write_aseprite(frames):
    groups = [("static", 1), ("walk", 8), ("ear", 5), ("graze", 5), ("chew", 3), ("lips", 13), ("turn", 6), ("head", 3)]
    tags, cur = [], 0
    for name, n in groups:
        tags.append((name, cur, cur + n - 1)); cur += n
    cels, durs = [], []
    names = LAYERS
    for k in ORDER:
        donkey, _, layers = frames[k]
        cel = {0: donkey}
        for i, name in enumerate(names):
            im = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
            px = im.load()
            for (x, y), c in layers[name].items():
                if 0 <= x < 96 and 0 <= y < 96:
                    px[x, y] = c + (255,)
            cel[i + 1] = im
        cels.append(cel)
        durs.append(100 if "walk" in k else 150)
    layer_list = [("donkey (game)", ase_write.VISIBLE)] + [(n, ase_write.VISIBLE | ase_write.EDITABLE) for n in names]
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
    # one piece with the body (review.motion_check): the barding against the hip and the back level, the peytral
    # against the collar, the chanfron against the forward ear base; the expected lines are in the docstring's
    # anchoring notes (the head pieces: the same box in every frame that only moves the head)
    layers, anchors = {}, {}
    for k, (donkey, _, L) in frames.items():
        layers[k] = L
        b = body(k, donkey)
        (ax, ay, _), _ = HORNS[k]
        anchors[k] = {"chanfron": (ax, ay)}
        if b is not None:
            anchors[k]["barding"] = (b["hip"], min(b["top"].values()))
            anchors[k]["peytral"] = (b["collar"], 0)
    for line in review.motion_check(layers, anchors):
        print("motion:", line)
    bad = review.phase_check(layers, anchors, {STEEL_DD: RIVET_EVERY, RED_D: 2, BRASS_L: 2}, skip={"chanfron"})
    for line in bad:
        print("phase:", line)
    if any(l.startswith("FAIL") for l in bad):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
