"""Royal Parade drawn once (docs/sprite-tools.md "Catching what the user caught" 4): a tools/skins/stamp.py source cut
out of this generator's own frames (its in-progress cut, out/pack), so stamp's placement can be compared with the
generator's frame by frame. The source is written into out/drawn-once/ (git-ignored):
- body.png: the standing frame's cloth (x 44 and on), its front outline cut to the cloth's top at the withers (the
  generator runs it 3 px up the collar strap's rear edge, a post on the back wherever the strap moves away);
- head.png: the standing frame's crown (x under 44);
- head_turn.png, head_front.png, head_down.png: the crown of turn 03, turn 05 and graze 04;
- head@bdy_turn_left_02.png and head@bdy_graze_left_02.png: their crowns as drawn;
- body@<frame>.png for the walk and the lips clip: the cloth where stamp places body.png there, its hanging bits
  swinging a pixel (swaying()).

    python -I tools/skins/royal-parade/drawn_once.py                    # the source, then build and stage it:
    python -I tools/skins/stamp.py build tools/skins/royal-parade/out/drawn-once
        --out tools/skins/royal-parade/out/pack-once --stage "Royal Parade (drawn once)"   # (one line)
    python -I tools/skins/royal-parade/drawn_once.py compare            # stamp's frames against the generator's

Run make_royal.py first (its out/pack is the source's source). The pictures hold none of the game's art.
"""
import glob
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "donkey"))
from contact import ORDER
from shapes import pendulum, save, swing

GEN = os.path.join(HERE, "out", "pack")
SRC = os.path.join(HERE, "out", "drawn-once")
STAMPED = os.path.join(HERE, "out", "pack-once")
PACK_CFG = os.path.join(REPO, "packs", "GK2SaddleUpDonkeySkinPack", "gk2mounts", "donkey", "skins",
                        "saddleup-donkey-skins.royal-parade", "skin.cfg")
SPLIT = 44        # the crown lies in front of this column, the cloth behind it
WITHERS_X = 48    # rig.json's withers on the standing frame: the first column behind the collar strap
POINT_X = 55      # the cloth's point hangs from its hem behind this column, its bottom edge on row 57


def hangs_free(p):
    """The cloth's bits that hang free on the standing frame: the front fringe's tassels from their tops (row 57, in
    front of the point) and, under the point's bottom edge (row 57), its tassel and the rear fringe's end. Only their
    ends (from row 58) were too little to see (a fresh review: "it reads as a stiff cloth riding the body"); the point's
    edge moved with them slid its red middle off the red above, and tassels a frame apart closed into a block."""
    x, y = p
    return y >= 58 or (y == 57 and x < POINT_X)


# each hanging part's phase, frontmost first: the front fringe's three tassels together (so they never close up), the
# point's tassel and the rear fringe's end a frame behind them, together (a frame apart, the pixel's gap between those
# two closed and opened for single frames: a blink, a fresh review)
PHASES = (-2, -2, -2, -3, -3)
SWAY = (("walk", 8), ("lips", 13))   # the clips the hanging bits swing in, and each clip's length


def part(key, front):
    im = Image.open(os.path.join(GEN, key + ".png")).convert("RGBA")
    out = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    px, op = im.load(), out.load()
    for y in range(96):
        for x in range(96):
            if px[x, y][3] and ((x < SPLIT) == front):
                op[x, y] = px[x, y]
    return out


def trim_front(cloth):
    """The cloth's pixels in front of the withers cut to the cloth's top there (stamp.py build names them otherwise:
    "on the collar strap")."""
    out = cloth.copy()
    op = out.load()
    top = min(y for y in range(96) if op[WITHERS_X, y][3])
    for x in range(WITHERS_X):
        for y in range(top):
            op[x, y] = (0, 0, 0, 0)
    return out


BACK_STRETCH_X = 60   # a plain column of the cloth (its highlight, its red, the bottom band's gold): stretched
BACK_WIDEN = 2        # columns: the back edge (trim, outline, the rear fringe's end) moved this far back


def widen_back(cloth):
    """The cloth's back edge moved BACK_WIDEN columns back, column BACK_STRETCH_X drawn that many times more: behind
    the cloth, 2-3 columns of the donkey's rump showed, and in walk 01, 04, 05 and 08 the hind leg's stride put its
    outline's brown there where the other frames show the coat, a blink behind the cloth (the user, 2026-11-11: "in
    some frames, near the tail, brown pixels are appearing"). Wider, it covers that strip in every frame; the
    pattern in front of the column (the fleur, the folds) stays where it is."""
    src = cloth.load()
    out = Image.new("RGBA", cloth.size, (0, 0, 0, 0))
    op = out.load()
    w, h = cloth.size
    for y in range(h):
        for x in range(w):
            c = src[x, y]
            if not c[3]:
                continue
            if x < BACK_STRETCH_X:
                op[x, y] = c
            elif x == BACK_STRETCH_X:
                for k in range(BACK_WIDEN + 1):
                    op[x + k, y] = c
            elif x + BACK_WIDEN < w:
                op[x + BACK_WIDEN, y] = c
    return out


ROUND_ENDS = {41: 61, 42: 63, 43: 64}   # the last column of the cloth's top rows at the back: the Jester's corner
ROUND_REGION = (58, 41, 55)             # the back corner repainted: from this column back, rows 41 to 55
OUTLINE, TRIM, HIGHLIGHT = (0x3A, 0x0A, 0x12, 255), (0xE3, 0xA8, 0x2F, 255), (0xE2, 0x3A, 0x50, 255)


def round_back(cloth):
    """The cloth's back top corner rounded along the rump, a step in a column a row as the Jester's cloth (the user,
    2026-11-11: "can we make the rump part of the cloth not as right angle and that it curves more with the donkey's
    rump"; "we got the rump curve good with the jester skin"): its top rows end at ROUND_ENDS, and the corner is
    repainted in the cloth's own layers, the outline on the outside of every step, the gold trim inside it, the pink
    highlight under the gold where the edge faces up (as along the top)."""
    out = cloth.copy()
    op = out.load()
    for y, end in ROUND_ENDS.items():
        for x in range(end + 1, 96):
            op[x, y] = (0, 0, 0, 0)
    x0, y0, y1 = ROUND_REGION
    solid = lambda x, y: 0 <= x < 96 and 0 <= y < 96 and op[x, y][3] > 0
    open_ = lambda x, y: not solid(x, y)
    region = [(x, y) for y in range(y0, y1 + 1) for x in range(x0, 96) if solid(x, y)]
    edge = {p for p in region if open_(p[0], p[1] - 1) or open_(p[0] + 1, p[1]) or open_(p[0] + 1, p[1] - 1)}
    trim = {p for p in region if p not in edge and ((p[0], p[1] - 1) in edge or (p[0] + 1, p[1]) in edge or (p[0] + 1, p[1] - 1) in edge)}
    light = {p for p in region if p not in edge and p not in trim and (p[0], p[1] - 1) in trim}
    for p in edge:
        op[p] = OUTLINE
    for p in trim:
        op[p] = TRIM
    for p in light:
        op[p] = HIGHLIGHT
    return out


def swaying(cloth):
    """body@<frame>.png for the clips in SWAY: the cloth where stamp places body.png in that frame (stamp.body_move,
    by its columns), each of its hanging bits (shapes.hanging by hangs_free) swung a pixel across on a pendulum by its
    own of PHASES: over the stride in the walk, slowly over the lips clip's 13 frames.
    Drawn once, the cloth stood still in every frame ("it just looks a little bit too staticey, maybe we just need to
    add very very slight animation to it", the user, 2026-11-03)."""
    import stamp
    im = cloth.load()
    art = {(x, y): im[x, y] for y in range(96) for x in range(96) if im[x, y][3]}
    cols = sorted({x for x, y in art})
    for old in glob.glob(os.path.join(SRC, "body@*.png")):
        os.remove(old)
    for clip, period in SWAY:
        for i, key in enumerate(k for k in ORDER if k.startswith(f"bdy_{clip}_left_")):
            dx, dy = stamp.body_move(key, cols)
            swung = swing(art, hangs_free, [pendulum(i, ph, 1, period) for ph in PHASES])
            placed = {(x + dx, y + dy): c for (x, y), c in swung.items()}
            placed, fitted = hug_back(placed, art, cols, key, dx, dy)
            if fitted:
                print(f"   {key}: the cloth's top fitted to the back at " + " ".join(f"{x} ({d:+d})" for x, d in fitted))
            save(placed, os.path.join(SRC, f"body@{key}.png"))


BAND = 3   # the cloth's top band: its outline, the gold trim and the pink highlight (body.png's columns, top down)


def hug_back(placed, art, cols, key, dx, dy):
    """The cloth's top band moved, column by column, to lie on the back as it lies on the standing frame's: placed as
    one (stamp.body_move), its top was a pixel off the back wherever the walk's back outline has a 1-px bump (stamp.py
    moves lists them: walk 01, 07 and 08 a pixel under the back, the donkey's coat showing between them, white under a
    Snow coat, the user's video of 2026-11-11; walk 04-06 a pixel over it). The band moves; the red under it grows or
    shrinks by as much (its columns are one red, or a fold stripe running down), so the trim follows the back and no
    pattern steps. Returns the cloth and [(frame column, rows moved: - up, + down)]."""
    import stamp
    top = {}
    for x, y in art:
        top[x] = min(y, top.get(x, y))
    ts = stamp.held_top(stamp.STANDING, cols)
    tf = stamp.held_top(key, [c + dx for c in cols])
    out = dict(placed)
    fitted = []
    for x in cols:
        if x not in ts or x + dx not in tf:
            continue
        want = tf[x + dx] + (top[x] - ts[x])   # the frame's back, as far above the cloth's top as on the standing frame
        now = top[x] + dy
        d = want - now
        # raised only: where the cloth lies a pixel over the back nothing of the donkey shows, and lowered at a single
        # column (a 1-px dip of the back line) the cloth's top edge was notched (walk 08, the widened cloth, 2026-11-11)
        if d >= 0:
            continue
        fx = x + dx
        band = [placed.get((fx, now + k)) for k in range(BAND)]
        under = placed.get((fx, now + BAND))
        if any(c is None for c in band) or under is None:
            continue   # not the cloth's top here (an edge column shorter than the band): left as placed
        for k in range(BAND):
            out.pop((fx, now + k), None)
        for k in range(BAND):
            out[(fx, want + k)] = band[k]
        for y in range(want + BAND, now + BAND):   # raised: the red fills the rows the band left
            out[(fx, y)] = under
        fitted.append((fx, d))
    return out, fitted


def make():
    if not os.path.isdir(GEN):
        sys.exit(f"drawn_once: no {GEN} (run make_royal.py first)")
    os.makedirs(SRC, exist_ok=True)
    shutil.copy2(PACK_CFG, os.path.join(SRC, "skin.cfg"))
    text = open(os.path.join(SRC, "skin.cfg"), encoding="utf-8", newline="").read()
    with open(os.path.join(SRC, "skin.cfg"), "w", encoding="utf-8", newline="") as f:
        f.write(text.replace("name = Royal Parade", "name = Royal Parade (drawn once)", 1))
    cloth = round_back(widen_back(trim_front(part("bdy_static_left", False))))
    cloth.save(os.path.join(SRC, "body.png"))
    swaying(cloth)
    for name, key in (("head", "bdy_static_left"), ("head_turn", "bdy_turn_left_03"),
                      ("head_front", "bdy_turn_left_05"),
                      ("head_down", "bdy_graze_left_04"), ("head@bdy_turn_left_02", "bdy_turn_left_02"),
                      ("head@bdy_graze_left_02", "bdy_graze_left_02")):
        part(key, True).save(os.path.join(SRC, name + ".png"))
    print("source:", SRC)


def compare():
    rows = []
    for k in ORDER:
        a = Image.open(os.path.join(GEN, k + ".png")).convert("RGBA").load()
        b = Image.open(os.path.join(STAMPED, k + ".png")).convert("RGBA").load()
        same = lambda x, y: (a[x, y] if a[x, y][3] else 0) == (b[x, y] if b[x, y][3] else 0)
        cloth = sum(1 for y in range(96) for x in range(SPLIT, 96) if not same(x, y))
        crown = sum(1 for y in range(96) for x in range(0, SPLIT) if not same(x, y))
        rows.append((k, cloth, crown))
    for k, c1, c2 in rows:
        if c1 or c2:
            print(f"   {k:22s} cloth {c1:3d} px, crown {c2:3d} px")
    print(f"frames the same: {sum(1 for _, a, b in rows if not a and not b)} of {len(rows)}; cloth differs in "
          f"{sum(1 for _, a, _ in rows if a)}, crown in {sum(1 for _, _, b in rows if b)}")


if __name__ == "__main__":
    compare() if sys.argv[1:] == ["compare"] else make()
