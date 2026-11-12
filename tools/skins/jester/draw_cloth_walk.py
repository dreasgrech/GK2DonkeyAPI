"""The Jester's caparison in the walk: the skirt below the seam swings as a PENDULUM over the stride (2026-11-12).

The shipped design slid the hem's points a pixel or two under a skirt that never changed: "the sway is happening only
at the red line" (the seam), then a round of warped skirts (the hem squashed and stretched, the folds bent): "needs to
be more pendulum like as opposed to warping" (the user). Drawn after Saint11's Fabric/Flags tutorial (the user's
reference): few folds, two tones (base + fold line), no highlights, a guide line for the hem.

Per walk frame (the body's bob: 01/05 low, 03/07 high), from the frame's own pieces laid as stamp lays them:
- the 3 rows above the seam are drawn again without highlights (the folds start there, so the seam is no hinge);
- below the seam the skirt swings rigidly: a line's offset is SWING * (its distance from the seam) / the skirt's
  length, a straight tilt, never a bend; SWING over the 8 frames is +3 +2 0 -2 -3 -2 0 +2 (+ = back), eased at both
  ends; the length never changes; each panel is rigid and takes its 1-px steps on rows of its own (PANEL_PHASE):
  stepping together they drew a straight break across the skirt, the seam again;
- the hem is dagged like the tutorial's banner: each panel runs on down into its own point in its own colour (no
  band, nothing of another colour hung under it: "they're supposed to be the same cloth", the user), the panel's
  middle line down to the tip, the notches at the folds, the middle one longest, a bell at each tip; the outline goes
  round the outside only; the front strip ends in a flap; the tips lag a frame (they lean against the cloth's last
  move); a dag mostly behind the near leg is left out with its bell;
- the old cloth right of the scarf's outline is cleared first (none of it may be left), the scarf, the near leg and
  the outlines are never painted over.
Only the frame's body@ drawing changes, by the pixels the new frame differs in. Running it again gives the same
pieces. draw_jester.py calls it after drawing the pieces; alone:

    python -I tools/skins/jester/draw_cloth_walk.py [<pieces folder>]      # then stamp.py build
"""
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

PAL = {"o": "3a0a14", "d": "641a28", "r": "862534", "l": "a23444", "h": "c1485b", "k": "141a3a", "n": "243266",
       "b": "34488a", "c": "4e66a8", "v": "6e86c8", "u": "5a3a0a", "w": "966414", "g": "e2a830", "y": "ffd652"}
PAL = {k: tuple(int(v[i:i + 2], 16) for i in (0, 2, 4)) + (255,) for k, v in PAL.items()}
CLOTH = set(PAL.values())
BLUE = {PAL[c] for c in "kncbv"}
GOLD = {PAL[c] for c in "uwgy"}
OUTLINE = (0x1e, 0x19, 0x0e, 255)     # the scarf's and the leg's outline
CLEAR = (0, 0, 0, 0)

SEAM = {1: 49, 2: 48, 3: 47, 4: 48, 5: 49, 6: 48, 7: 47, 8: 48}   # the seam row of each walk frame (the bob)
SWING = {1: 3, 2: 2, 3: 0, 4: -2, 5: -3, 6: -2, 7: 0, 8: 2}
# The skirt: BODY rows of panels below the seam, then each panel runs on down into its own dag (the hem cut into
# points), in its own colour, with no band between: "the part marked in blue is completely disconnected ... they're
# supposed to be the same cloth" (the user, 2026-11-12: the band and the points of the other colour under it read as a
# second strip sewn on). After the tutorial's banner: the cloth runs into its tatters, the outline goes round the
# outside only, the folds end in the notches, the tatters' lengths vary (the middle one longest).
BODY = 6
# A dag per 8-px panel, drawn row by row below the body, each row with its own outline (E = the colour's darkest,
# B = its base, L = its line): starts at the panel's column 1 (column 0, the fold line, is the notch), the middle line
# (column 4) runs down to the tip. The middle (blue) panel's one row longer. The dag rows follow the same lines as the
# body (the middle line runs on unbroken); its rows from TIP_LAG_FROM on trail by the tips' lean.
DAG = [1, 2, 3, None]              # each row's inset from the panel's two fold lines; None = the tip (1 px)
DAG_LONG = [1, 2, 3, 3, None]
TIP_LAG_FROM = 2
# Each panel (front red A, blue B, rear red C) swings as one rigid piece and takes its 1-px steps on rows of its own.
# One tilt for the whole skirt put every step on the same row: a straight break across it, the seam again ("a seam
# here which is like the seam we set out to fix at the beginning", the user, 2026-11-12); a step per LINE instead made
# the 3-px checks 2 and 4 wide and bent the dags. The phases were searched (swings 2 and 3 over the 11 rows): no two
# panels step on the same body row, none on the row under the trim, so the step runs across the skirt as a staircase.
PANEL_PHASE = [0.0, 0.7, 0.2]
# a panel's columns (from its fold line) and letters: base and line colour; C carries the rear fold and outline
PANELS = [("r", "d", 8), ("b", "n", 8), ("r", "d", 9)]
UPPER = "bbbnbbbdrrrdrrrnbbbnbbnk"         # the rows above the seam from column 43 to the outline at 66


def phase(f):
    """The swing at the hem, each panel's offset per body row below the seam ([panel][row 1..BODY]: the pendulum's
    tilt, each panel stepping on rows of its own), the tips' lean."""
    sw = SWING[f]
    prev = SWING[8 if f == 1 else f - 1]
    a = abs(sw) / (BODY + len(DAG_LONG))
    sign = 1 if sw >= 0 else -1
    offs = [[sign * math.floor(a * i + ph) for i in range(1, BODY + 1)] for ph in PANEL_PHASE]
    return sw, offs, max(-1, min(1, prev - sw))


def upper(x):
    return "b" if x < 43 else UPPER[x - 43]


def front_edge(px, y):
    """The first column the cloth may use in a row: right of the scarf's outline; in a row without one, the old blue
    front strip followed to its left end so none of it is left behind."""
    xs = [x for x in range(30, 48) if px[x, y] == OUTLINE]
    if xs:
        return max(xs) + 1
    e = 40
    while e - 1 >= 34 and px[e - 1, y] in BLUE:
        e -= 1
    return e


def redraw(frame, f):
    """The frame (all pieces laid) with the cloth drawn again; returns it and the scan (leftover old cloth pixels,
    isolated pixels in the new cloth). The cloth below the seam is built as one shape first (body rows, the front
    strip's flap, the dags), outlined round its outside only, then laid where nothing in front of it (the near leg,
    the scarf) is."""
    im = frame.copy()
    px = im.load()
    seam = SEAM[f]
    sw, offs, lean = phase(f)
    first = seam - 3
    edge = {y: front_edge(px, y) for y in range(first, 64)}
    placed, cleared = set(), set()
    for y in range(first, 64):
        if y == seam:
            continue
        for x in range(edge[y], 96):
            if px[x, y] in CLOTH:
                px[x, y] = CLEAR
                cleared.add((x, y))

    def free(x, y):
        return px[x, y][3] == 0 or (x, y) in cleared or (x, y) in placed

    def put(x, y, ch):
        if x < edge.get(y, 0) or not free(x, y):
            return
        px[x, y] = PAL[ch]
        placed.add((x, y))

    old = frame.load()
    for y in range(first, seam):
        for x in range(edge[y], 67):
            put(x, y, "k" if (x == edge[y] and old[x, y] == PAL["k"]) else upper(x))

    # the cloth below the seam as one shape {(x, y): letter}: each panel rigid at its own offset for the row (its
    # fold line, its middle line, its base), drawn front to back so a fold line wins where two panels meet; a pixel
    # left between two panels takes the front one's base; the front strip before the first panel
    cloth = {}
    bottom = seam + BODY

    def panel_row(p, x0):
        base, line, width = PANELS[p]
        row = {}
        for u in range(width):
            row[x0 + u] = line if u in (0, 4) else base
        if p == 2:
            row[x0 + 7], row[x0 + 8] = "d", "o"     # the rear fold and outline
        return row

    for i in range(1, BODY + 1):
        y = seam + i
        starts = [42 + 8 * p + offs[p][i - 1] for p in range(3)]
        thin = starts[0] - edge[y] < 2     # a 1-px front strip was a speck between the scarf and the red panel
        for x in range(edge[y], starts[0]):
            cloth[(x, y)] = "d" if thin else "b"
        for p in range(3):
            if p and starts[p] > starts[p - 1] + 8:          # a gap: the front panel's base fills it
                for x in range(starts[p - 1] + 8, starts[p]):
                    cloth[(x, y)] = PANELS[p - 1][0]
            for x, ch in panel_row(p, starts[p]).items():
                if x >= edge[y]:
                    cloth[(x, y)] = ch
    starts = [42 + 8 * p + offs[p][BODY - 1] for p in range(3)]
    # the front strip's flap: one row more under it, as wide as it is, in the blue's shade (a row of outline there
    # was a dark bar; "dark areas have less detail", the tutorial)
    flap = [x for x in range(edge[bottom + 1], starts[0]) if (x, bottom) in cloth and cloth[(x, bottom)] in "bnk"
            and free(x, bottom + 1)]          # the near leg in front of a flap pixel hides it
    if len(flap) >= 2:                     # 1 px of flap was a speck beside the leg (walk 05, 06)
        for x in flap:
            cloth[(x, bottom + 1)] = "n"
    darkest = {c: "o" for c in "rdolh"}
    darkest.update({c: "k" for c in "bnkcv"})
    # the dags: each panel runs on down from its last body row, rigid (a step inside a dag bent it), narrowing between
    # its fold lines to the tip, its middle line to the tip; the rows from TIP_LAG_FROM on trail by the tips' lean
    hang = set()
    dag_cells = {}
    for p in range(3):
        base, line, _ = PANELS[p]
        dag = DAG_LONG if p == 1 else DAG
        dag_cells[p] = {}
        x0 = starts[p]
        for j, inset in enumerate(dag):
            y = bottom + 1 + j
            sh = lean if j >= TIP_LAG_FROM else 0
            if inset is None:
                row = {x0 + 4: "E"}
            else:
                a, b = x0 + inset, x0 + 8 - inset
                row = {x: "E" if x in (a, b) else "L" if x == x0 + 4 else "B" for x in range(a, b + 1)}
            for x, role in row.items():
                dag_cells[p][(x + sh, y)] = {"E": darkest[base], "B": base, "L": line}[role]
    # a panel a pixel ahead of the next one brought their dags together (no notch: walk 01): the front dag's row
    # gives up its last pixel there, its new last one the outline
    for p in (0, 1):
        nxt = {xy for xy in dag_cells[p + 1]}
        for y in {y for _, y in dag_cells[p]}:
            xs = sorted(x for x, yy in dag_cells[p] if yy == y)
            if xs and (xs[-1] + 1, y) in nxt and len(xs) > 2:
                del dag_cells[p][(xs[-1], y)]
                dag_cells[p][(xs[-2], y)] = darkest[PANELS[p][0]]
    for cells in dag_cells.values():
        hang.update(cells)
    # the body's last row: where nothing hangs below it (a notch) it is the outline
    for (x, y), ch in list(cloth.items()):
        if y == bottom and (x, y + 1) not in cloth and (x, y + 1) not in hang:
            cloth[(x, y)] = darkest[ch]
    # a dag that mostly hides behind the near leg is left out whole (a stub of it read as a speck), with its bell
    hidden_dag = set()
    for p, cells in dag_cells.items():
        shown = [c for c in cells if c[0] >= edge.get(c[1], 0) and free(*c)]
        if len(shown) <= len(cells) // 2:
            hidden_dag.add(p)
    for (x, y), ch in sorted(cloth.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        put(x, y, ch)
    for p, cells in dag_cells.items():
        if p in hidden_dag:
            continue
        for (x, y), ch in cells.items():
            put(x, y, ch)
        # the bell under the tip
        (tx, ty) = max(cells, key=lambda c: (c[1], -c[0]))
        for dy, row in enumerate(("uygu", "ugwu", ".uu.")):
            for i, ch in enumerate(row):
                if ch != ".":
                    put(tx - 1 + i, ty + 1 + dy, ch)
    left = [(x, y) for y in range(first, 64) if y != seam for x in range(edge[y], 96)
            if px[x, y] in CLOTH and (x, y) not in placed]
    stray = [(x, y) for (x, y) in placed if px[x, y] not in GOLD and px[x, y] not in (PAL["k"], PAL["o"])
             and not any(px[x + dx, y + dy] == px[x, y] for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy)]
    return im, left, stray


def laid(where, key):
    """The walk frame's pieces laid as stamp lays them (body_drape, body, neck, head), each its frame's own drawing."""
    im = Image.new("RGBA", (96, 96), CLEAR)
    for piece in ("body_drape", "body", "neck", "head"):
        p = os.path.join(where, f"{piece}@{key}.png")
        if os.path.exists(p):
            im.alpha_composite(Image.open(p).convert("RGBA"))
    return im


def run(where=HERE):
    bad = 0
    for f in range(1, 9):
        key = f"bdy_walk_left_{f:02d}"
        body_path = os.path.join(where, f"body@{key}.png")
        if not os.path.exists(body_path):
            sys.exit(f"draw_cloth_walk: no body@{key}.png in {where} (run draw_jester.py first)")
        frame = laid(where, key)
        new, left, stray = redraw(frame, f)
        body = Image.open(body_path).convert("RGBA")
        bp, a, b = body.load(), frame.load(), new.load()
        changed = 0
        for y in range(96):
            for x in range(96):
                if a[x, y] != b[x, y]:
                    bp[x, y] = b[x, y]
                    changed += 1
        body.save(body_path)
        sw, _, lean = phase(f)
        print(f"walk {f:02d}: swing {sw:+d}, tips lean {lean:+d}: {changed} px of body@ drawn again; "
              f"old cloth left {len(left)}, isolated {len(stray)}")
        bad += len(left) + len(stray)
    if bad:
        sys.exit(f"draw_cloth_walk: {bad} pixel(s) left or isolated (see above)")
    return 0


if __name__ == "__main__":
    sys.exit(run(os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE))
