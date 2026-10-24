# The Royal Parade skin, generated (docs/sprite-tools.md): a saddle cloth (red, gold trim and fringe, a fleur on the flank) and a crown
# between the ears at the Hellfire horn bases (the positions reviewed for Hellfire v14). A built-in skin, the same
# pipeline as Hellfire (docs/sprite-tools.md "Making a skin"): writes a PNG per frame into
# src/SaddleUp/assets/mounts/donkey/skins/royal-parade/ (skin.cfg there is by hand; the DLL build embeds the folder), plus
# royal.aseprite for anyone who wants to touch a frame by hand, preview.png (every frame at 4x), zoom.png (six
# frames at 8x) and strips/<animation>.png (every animation as a frame strip at 4x: LOOK AT EVERY STRIP before the
# game does).
#
#   python tools/skins/royal-parade/make_royal.py              # the built-in pack's pictures + out\ (previews, strips, .aseprite)
#   python tools/skins/royal-parade/make_royal.py --no-assets  # out\ only (the pictures into out\pack)
#
# How the cloth is placed (per frame, from the donkey's pixels). The cloth is one fixed shape measured on the
# standing frame: its width is a fraction of the back (collar to hip) and its height per column is the standing
# frame's back-to-belly depth minus HEM_ABOVE. In every frame it hangs from the HIP (the rump's end at back level,
# the one point of the body that stays put while the head, neck and legs move): the same width back from the hip,
# each column's top on that frame's own back line, so the cloth rides the back through a walk's bob and a graze's
# dip without changing size or sliding along the body. A frame whose back is shorter (the turn clips, the body
# foreshortened) gets the cloth cut to a fraction of that back instead.
import json, os, sys
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
from contact import ORDER, load
import ase_write

REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
OUTDIR = os.path.join(HERE, "out")                      # generated: the .aseprite, the previews, the strips (git-ignored)
OUT = os.path.join(OUTDIR, "royal.aseprite")
ASSETS = os.path.join(REPO, "src", "SaddleUp", "assets", "mounts", "donkey", "skins", "royal-parade")  # the built-in pack: skin.cfg by hand, PNGs from here
HORNS = json.load(open(os.path.join(HERE, "horn_bases.json")))
ANCHORS = json.load(open(os.path.join(HERE, "anchors.json")))
HEM_ABOVE = 2     # the hem this many rows above the standing frame's belly line: the belly and the legs show
REAR = 1.0        # the cloth covers this much of the back, measured from the hip towards the collar
FRONT = 2         # and this many columns more, over the collar strap's rear edge
GAP = 2           # columns left bare between the cloth's rear edge and the hip (the rump's curve shows)
SAG = 1           # the hem hangs this much lower in the middle than at the ends (a draped cloth)

# colours (none of them is a donkey atlas colour; the checker says so)
OUTLINE = (58, 10, 18)
RED_LIGHT, RED, RED_DARK = (226, 58, 80), (196, 30, 56), (150, 18, 44)
GOLD_LIGHT, GOLD, GOLD_DARK = (255, 214, 82), (226, 168, 48), (150, 100, 20)
JEWEL_RED, JEWEL_BLUE = (220, 20, 60), (60, 110, 230)

CROWN = ["L..L..L",
         "G.GLG.G",
         "GGGGGGG",
         "MRMBMRM",
         "DDDDDDD"]
FLEUR = ["..L..",
         ".GLG.",
         "GGLGG",
         "..G..",
         ".GGG."]
PAL = {"L": GOLD_LIGHT, "G": GOLD, "M": GOLD, "D": GOLD_DARK, "R": JEWEL_RED, "B": JEWEL_BLUE}


def stamp(px, pattern, x0, y0):
    for j, row in enumerate(pattern):
        for i, ch in enumerate(row):
            if ch in PAL and 0 <= x0 + i < 96 and 0 <= y0 + j < 96:
                px[x0 + i, y0 + j] = PAL[ch] + (255,)


CROWN_FORWARD = 3  # columns towards the muzzle from the point between the ears (the user: "a bit more forward")
CROWN_BEHIND_EYE = 2  # with the eye found: the crown's centre this many columns behind the eye
CROWN_ABOVE_EYE = 3   # and its bottom row this many rows above the eye (the forehead)


def crown(key, donkey=None):
    """The crown CROWN_FORWARD columns towards the muzzle from the point between the ears, resting on the head's own
    top outline under its three front columns (the ears sit behind, under its back columns, and must not lift it): on a
    lowered head (the graze) the forehead slopes down and the crown follows it. A head facing the camera (the turn
    clip, the ears leaning apart) keeps the crown between the ears, two rows down."""
    im = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    (ax, ay, la), (bx, by, lb) = HORNS[key]
    cx = round((ax + bx) / 2)
    if la != lb or donkey is None:
        stamp(im.load(), CROWN, cx - 3, min(ay, by) - 4 + 2)
        return im
    # the eye, where it is found: the one landmark of the head that no ear flick moves; the crown sits a little
    # behind it and above it (the standing frame's forehead)
    eye = ANCHORS.get(key, {}).get("eye")
    if eye:
        stamp(im.load(), CROWN, round(eye[0]) + CROWN_BEHIND_EYE - 3, round(eye[1]) - CROWN_ABOVE_EYE - 4)
        return im
    dp = donkey.load()
    op = lambda x, y: 0 <= x < 96 and 0 <= y < 96 and dp[x, y][3] > 0
    cxf = cx - CROWN_FORWARD
    # the forehead under the crown's three front columns (the ears are behind, under its back columns)
    tops = []
    for x in range(cxf - 3, cxf):
        ys = [y for y in range(96) if op(x, y)]
        if ys:
            tops.append(min(ys))
    base = sorted(tops)[len(tops) // 2] if tops else min(ay, by)
    stamp(im.load(), CROWN, cxf - 3, base - 4)  # the bottom row on the forehead's outline row
    return im


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


def standing_profile():
    """The cloth's shape from the standing frame: for each column, counted back from the hip, how deep the cloth is
    (back line to HEM_ABOVE rows above the belly), and how many columns it spans."""
    b = body("bdy_static_left", load("bdy_static_left"))
    op, top, hip, collar, ground = b["op"], b["top"], b["hip"], b["collar"], b["ground"]
    # the belly per column: the end of the run of body pixels down from the back; a run to the ground is a leg
    belly = {}
    for x in sorted(top):
        y = top[x]
        while op(x, y + 1):
            y += 1
        if y < ground - 3:
            belly[x] = y
    known = sorted(belly)
    for x in top:
        if x not in belly:
            left = max((k for k in known if k < x), default=None)
            right = min((k for k in known if k > x), default=None)
            belly[x] = belly[right] if left is None else belly[left] if right is None else round(belly[left] + (belly[right] - belly[left]) * (x - left) / (right - left))
    width = round((hip - collar) * REAR) + FRONT
    depth = {}
    for i in range(width):
        x = hip - GAP - i
        if x not in top:
            continue
        win = sorted(belly[k] for k in (x - 1, x, x + 1) if k in belly)
        depth[i] = win[len(win) // 2] - HEM_ABOVE - top[x]
    # a smooth hem: each column's depth the median of its five neighbours (no hanging corner at an end column)
    smooth = {}
    for i in depth:
        win = sorted(depth[k] for k in range(i - 2, i + 3) if k in depth)
        smooth[i] = win[len(win) // 2]
    return width, smooth


PROFILE = None


def cloth_mask(key, donkey):
    """The set of (x, y) the cloth covers in this frame, or None for a frame without a body."""
    global PROFILE
    if PROFILE is None:
        PROFILE = standing_profile()
    width, depth = PROFILE
    b = body(key, donkey)
    if b is None:
        return None
    top, hip, collar = b["top"], b["hip"], b["collar"]
    # a shorter back (the body turned towards the camera) gets a shorter cloth, cut at the collar side
    w = min(width, round((hip - collar) * REAR) + FRONT)
    cols = []
    for i in range(w):
        x = hip - GAP - i
        if x in top and i in depth:
            cols.append((x, i))
    if len(cols) < 4:
        return None
    # the cloth's top is the back, never the neck: the front columns sit where the neck starts to climb, so their
    # top is held down to the back's level (the median of the rear half of the cloth's columns); a back that is
    # lower at the front (a graze) is followed
    rear_half = sorted(top[x] for x, _ in cols[: max(1, len(cols) // 2)])
    back_level = rear_half[len(rear_half) // 2]
    mask = set()
    n = len(cols)
    for j, (x, i) in enumerate(cols):
        mid = 1 - abs(j / max(1, n - 1) * 2 - 1)  # 0 at the ends, 1 in the middle
        t = max(top[x], back_level - 1)
        for y in range(t, t + depth[i] + round(SAG * mid) + 1):
            mask.add((x, y))
    return mask


def caparison(key, donkey):
    im = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    mask = cloth_mask(key, donkey)
    if not mask:
        return im
    px = im.load()
    inside = lambda x, y: (x, y) in mask
    outline = {p for p in mask if not all(inside(p[0] + dx, p[1] + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
    border = {p for p in mask - outline if any((p[0] + dx, p[1] + dy) in outline for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
    cols = {}
    for x, y in mask:
        cols.setdefault(x, []).append(y)
    top = {x: min(ys) for x, ys in cols.items()}
    bottom = {x: max(ys) for x, ys in cols.items()}
    x_first, x_last = min(cols), max(cols)
    for x, y in mask:
        t, b = top[x], bottom[x]
        if (x, y) in outline and y < b:
            c = OUTLINE
        elif y == b:
            c = GOLD_LIGHT if (x + y) % 2 == 0 else GOLD  # the gold band along the hem (the cloth's last row)
        elif (x, y) in border:
            c = GOLD  # the gold border inside the outline
        elif y <= t + 2:
            c = RED_LIGHT  # the light under the back
        elif y >= b - 2:
            c = RED_DARK  # the shadow above the band
        elif (x - x_first) % 6 == 3:
            c = RED_DARK  # a fold
        else:
            c = RED
        px[x, y] = c + (255,)
    # the fringe under the hem, every other column, longer every fourth
    for x in cols:
        b = bottom[x]
        if (x - x_first) % 2 == 0 and x_first < x < x_last and b + 1 < 96:
            px[x, b + 1] = GOLD + (255,)
            if (x - x_first) % 4 == 0 and b + 2 < 96:
                px[x, b + 2] = GOLD_DARK + (255,)
    # the fleur on the flank: centred in the red
    reds = [p for p in mask if px[p][:3] == RED]
    if len(reds) >= 40:
        cx = round(sum(p[0] for p in reds) / len(reds))
        cy = round(sum(p[1] for p in reds) / len(reds))
        stamp(px, FLEUR, cx - 2, cy - 2)
    return im


def build():
    frames, durs, tags, cur = [], [], [], 0
    groups = [("static", 1), ("walk", 8), ("ear", 5), ("graze", 5), ("chew", 3), ("lips", 13), ("turn", 6), ("head", 3)]
    for name, n in groups:
        tags.append((name, cur, cur + n - 1)); cur += n
    for k in ORDER:
        d = load(k)
        frames.append({0: d, 1: caparison(k, d), 2: crown(k, d)})
        durs.append(100 if "walk" in k else 150)
    layers = [("donkey (game)", ase_write.VISIBLE), ("caparison", ase_write.VISIBLE | ase_write.EDITABLE),
              ("crown", ase_write.VISIBLE | ase_write.EDITABLE)]
    ase_write.write(OUT, 96, 96, layers, frames, tags, durs)
    return frames


def write_pack(frames, folder):
    """The pack's pictures: the skin's layers only (no donkey), one PNG per frame key in ORDER, into the built-in
    skin's folder (the same layout as Hellfire's and every other built-in: skin.cfg stays as written by hand, the
    PNGs are replaced). Also out\\pack\\sheet.png, the same cells 8 per row, for the checker's sheet mode."""
    os.makedirs(folder, exist_ok=True)
    for f in os.listdir(folder):
        if f.endswith(".png") and f.startswith(("bdy_", "hed_")):
            os.remove(os.path.join(folder, f))
    C = 8
    rows = (len(frames) + C - 1) // C
    sheet = Image.new("RGBA", (C * 96, rows * 96), (0, 0, 0, 0))
    for n, cels in enumerate(frames):
        f = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
        f.alpha_composite(cels[1])
        f.alpha_composite(cels[2])
        f.save(os.path.join(folder, ORDER[n] + ".png"))
        sheet.alpha_composite(f, ((n % C) * 96, (n // C) * 96))
    os.makedirs(os.path.join(OUTDIR, "pack"), exist_ok=True)
    sheet.save(os.path.join(OUTDIR, "pack", "sheet.png"))
    return len(frames)


def composite(cels):
    f = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    for li in (0, 1, 2):
        f.alpha_composite(cels[li])
    return f


def strips(frames):
    """The review pictures through the shared module (tools/skins/review.py, the same for every generator): the pack
    over the donkey, the rider on the donkey's seat row; strips\\<animation>.png, preview.png, pack\\sheet.png."""
    import review
    from gamedir import game_dir
    pack = {}
    base = {}
    for n, cels in enumerate(frames):
        f = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
        f.alpha_composite(cels[1])
        f.alpha_composite(cels[2])
        pack[ORDER[n]] = f
        base[ORDER[n]] = cels[0]
    rider = review.load_rider(os.path.join(game_dir(), "BepInEx", "GK2-asset-probe"))
    review.write_all(OUTDIR, pack, base=base, rider=rider, seat_row=review.DONKEY_SEAT_ROW)
    return sorted(os.listdir(os.path.join(OUTDIR, "strips")))


if __name__ == "__main__":
    fr = build()
    S, C = 4, 8
    os.makedirs(OUTDIR, exist_ok=True)
    from zoom import zoom
    picks = ["bdy_static_left", "bdy_walk_left_03", "bdy_walk_left_07", "bdy_graze_left_03", "bdy_turn_left_03", "bdy_turn_left_06"]
    zoom([(k, composite(fr[ORDER.index(k)])) for k in picks], os.path.join(OUTDIR, "zoom.png"), S=8, box=(24, 20, 84, 70))
    print("strips:", ", ".join(strips(fr)))
    print("wrote", OUT, "preview.png zoom.png")
    # the pack: a PNG per frame into the built-in skin's folder (embedded into the DLL by the next build; the game
    # shows the new pictures after a build + deploy with the game closed)
    if "--no-assets" not in sys.argv:
        print("pack:", write_pack(fr, ASSETS), "pictures ->", ASSETS)
    else:
        print("pack:", write_pack(fr, os.path.join(OUTDIR, "pack")), "pictures -> out\\pack (--no-assets)")
