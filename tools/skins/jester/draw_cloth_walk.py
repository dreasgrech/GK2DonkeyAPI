"""The Jester's caparison in the walk: the skirt below the seam swings as a PENDULUM over the stride (2026-11-12/13).

The shipped design slid the hem's points a pixel or two under a skirt that never changed: "the sway is happening only
at the red line" (the seam), then a round of warped skirts (the hem squashed and stretched, the folds bent): "needs to
be more pendulum like as opposed to warping" (the user). Then rows of the skirt slid sideways for the lean, and every
vertical line in it jogged where a row slid: a crease across the skirt, three creases jumping between frames once the
panels slid on rows of their own ("moving seams", the user's video, 2026-11-13).

Drawn after the user's references, studied at pixel level (2026-11-13): Saint11's Fabric/Flags cape (two tones, the
outline on the silhouette only, every fold a DIAGONAL line 1 px wide stepping every 2-3 rows, radiating from the
shoulders; between its swing frames the hem moves 1 px with the top pinned and each fold is drawn anew with its steps
spread evenly for its slant; not one vertical line in it) and a pixel-art laundry line (solid fill, sparse diagonal
dither, a curved hem whose shape changes each frame). Nothing in them slides a row.

Per walk frame (the body's bob: 01/05 low, 03/07 high), from the frame's own pieces laid as stamp lays them:
- the 3 rows above the seam are drawn again without highlights (the cloth is pinned there, the cape's shoulders);
- below the seam the skirt's panel boundaries are LINES drawn anew each frame, from their fixed tops at the seam to
  their hem points (a Bresenham line: its steps spread evenly for its slant), the front edge leaning a pixel forward
  and the back edge a pixel back at rest (the skirt widens, as the cape does); the swing moves every hem point by
  SWING_BODY (+2 +1 0 -1 -2 -1 0 +1 over the stride, + = back): a step every 3 rows, the cape's slopes;
- no dark line inside the skirt: a boundary is a 1-px column of the panel's own darker tone (the cape's fold shade),
  the outline goes round the silhouette only; two tones a colour, no highlights ("avoid too many folds");
- the hem is dagged like the tutorial's banner: each panel runs on down into its own point in its own colour, the
  notches at the boundaries, the middle one longest, a bell at each tip; the dags hang at SWING_DAG (+3 +2 0 -2 -3 -2
  0 +2: a little past the body's lean, the hem trailing the cloth), their lower rows a frame behind (TIP_LAG_FROM: the
  tips lean against the cloth's last move); a dag mostly behind the near leg is left out with its bell;
- the old cloth right of the scarf's outline is cleared first (none of it may be left), the scarf, the near leg and
  the outlines are never painted over.
Only the frame's body@ drawing changes, by the pixels the new frame differs in. Running it again gives the same
pieces. draw_jester.py calls it after drawing the pieces; alone:

    python -I tools/skins/jester/draw_cloth_walk.py [<pieces folder>]      # then stamp.py build
"""
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
SWING_BODY = {1: 2, 2: 1, 3: 0, 4: -1, 5: -2, 6: -1, 7: 0, 8: 1}  # the hem points' move (+ = back)
SWING_DAG = {1: 3, 2: 2, 3: 0, 4: -2, 5: -3, 6: -2, 7: 0, 8: 2}   # the dags' (a little past the body: the hem trails)
BODY = 6                                                           # rows of skirt below the seam
# The skirt's seven pleat lines (the shipped design's: a panel's edge and its middle line, 4 px apart, each in the
# dark of the panel it starts), front to back: (top column at the seam, letter, lean at the hem at rest (+ = back),
# step phase). Each is drawn anew per frame as a line to its hem point; the phases put neighbouring lines' 1-px
# steps on different rows (pixel art's rule against banding: lines stepping together on one row drew a crease across
# the skirt, the user's "moving seams"). The front and back edges lean a pixel out at rest (the skirt widens, as the
# cape does). The back edge is the outline with C's dark before it. Panels: the strip (blue, under the scarf) before
# line 0, red A to line 2, blue B to line 4, red C to line 6.
LINES = [(42, "d", -1, 0.0), (46, "d", 0, 0.5), (50, "n", 0, 0.0), (54, "n", 0, 0.5), (58, "d", 0, 0.0),
         (62, "d", 0, 0.5), (66, "o", 1, 0.0)]
BOUNDARY = [0, 2, 4, 6]                                            # the lines between panels
PANEL_BASE = ["b", "r", "b", "r"]                                  # the strip, A, B, C
DARKEST = {"r": "o", "d": "o", "b": "k", "n": "k"}
# A dag per panel, row by row below the body: each row's inset from the panel's two hem points; None = the tip (1 px).
# The middle (blue) panel's one row longer. The rows from TIP_LAG_FROM on trail by the tips' lean.
DAG = [1, 2, 3, None]
DAG_LONG = [1, 2, 3, 3, None]
TIP_LAG_FROM = 2
UPPER = "bbbnbbbdrrrdrrrnbbbnbbnk"         # the rows above the seam from column 43 to the outline at 66


def phase(f):
    """The body's and the dags' swing at the hem, and the tips' lean (against the dags' last move)."""
    prev = SWING_DAG[8 if f == 1 else f - 1]
    return SWING_BODY[f], SWING_DAG[f], max(-1, min(1, prev - SWING_DAG[f]))


def line_x(top, bottom, rows, i, ph=0.0):
    """Column of a line from (top, row 0) to (bottom, row rows) at row i: the steps spread evenly, the phase (0 to 1)
    moving them up the line (0.5: a step at the span's middle rather than its end); a phase that would put a step on
    row 1, right under the trim, is not used."""
    dx = bottom - top
    if dx == 0:
        return top
    sign = 1 if dx > 0 else -1
    n = abs(dx)
    if (n + ph * rows) // rows >= 1:
        ph = 0.0
    return top + sign * int((n * i + ph * rows) // rows)


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
    isolated pixels in the new cloth). The cloth below the seam is built as one shape first (the body rows between
    their boundary lines, the front strip's flap, the dags), then laid where nothing in front of it (the near leg,
    the scarf) is."""
    im = frame.copy()
    px = im.load()
    seam = SEAM[f]
    sw_body, sw_dag, lean = phase(f)
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

    # the body rows: each pleat line from its top at the seam to its hem point, the panels' base between the boundary
    # lines, the back edge the outline with C's dark before it
    cloth = {}
    bottom = seam + BODY
    hems = [t + fl + sw_body for t, _, fl, _ in LINES]

    def cols(i):
        return [line_x(t, h, BODY, i, ph) for (t, _, _, ph), h in zip(LINES, hems)]

    for i in range(1, BODY + 1):
        y, lx = seam + i, cols(i)
        thin = lx[0] - edge[y] < 2       # a 1-px strip was a speck between the scarf and red A: A's dark instead
        for x in range(edge[y], lx[6] + 1):
            if x == lx[6]:
                ch = "o"
            elif x == lx[6] - 1:
                ch = "d"
            elif x in lx:
                ch = LINES[lx.index(x)][1]
            else:
                p = sum(1 for b in BOUNDARY[:3] if x > lx[b])   # the panel x lies in (0: the strip)
                ch = "d" if (p == 0 and thin) else PANEL_BASE[p]
            cloth[(x, y)] = ch
    hem = cols(BODY)
    # the front strip's flap: one row more under it, as wide as it is, in the blue's shade (a row of outline there
    # was a dark bar; "dark areas have less detail", the tutorial)
    flap = [x for x in range(edge[bottom + 1], hem[0]) if (x, bottom) in cloth and cloth[(x, bottom)] in "bnk"
            and free(x, bottom + 1)]      # the near leg in front of a flap pixel hides it
    if len(flap) >= 2:                     # 1 px of flap was a speck beside the leg (walk 05, 06)
        for x in flap:
            cloth[(x, bottom + 1)] = "n"
    # the dags: each panel runs on down from its two boundary lines' hem points, narrowing to the tip, its middle
    # pleat line on down to the tip, at the dags' swing (the difference to the body's from the third row on, with the
    # tips' lean: the hem trails the cloth)
    hang = set()
    dag_cells = {}
    for p in range(3):
        base, line = PANEL_BASE[p + 1], LINES[2 * p + 1][1]
        dag = DAG_LONG if p == 1 else DAG
        dag_cells[p] = {}
        left, right, mid = hem[2 * p], hem[2 * p + 2], hem[2 * p + 1]
        for j, inset in enumerate(dag):
            y = bottom + 1 + j
            sh = (sw_dag - sw_body if j >= 2 else 0) + (lean if j >= TIP_LAG_FROM + 1 else 0)
            if inset is None:
                row = {mid: "E"}
            else:
                a, b = left + inset, right - inset
                row = {x: "E" if x in (a, b) else "L" if x == mid else "B" for x in range(a, b + 1)}
            for x, role in row.items():
                dag_cells[p][(x + sh, y)] = DARKEST[base] if role == "E" else line if role == "L" else base
    # a panel a pixel ahead of the next one brought their dags together (no notch): the front dag's row gives up its
    # last pixel there, its new last one the outline
    for p in (0, 1):
        nxt = set(dag_cells[p + 1])
        for y in {y for _, y in dag_cells[p]}:
            xs = sorted(x for x, yy in dag_cells[p] if yy == y)
            if xs and (xs[-1] + 1, y) in nxt and len(xs) > 2:
                del dag_cells[p][(xs[-1], y)]
                dag_cells[p][(xs[-2], y)] = DARKEST[PANEL_BASE[p + 1]]
    for cells in dag_cells.values():
        hang.update(cells)
    # the body's last row: where nothing hangs below it (a notch) it is the outline
    for (x, y), ch in list(cloth.items()):
        if y == bottom and (x, y + 1) not in cloth and (x, y + 1) not in hang:
            cloth[(x, y)] = DARKEST.get(ch, "o")
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
        (tx, ty) = max(cells, key=lambda c: (c[1], -c[0]))   # the bell under the tip
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
        sw_body, sw_dag, lean = phase(f)
        print(f"walk {f:02d}: hem {sw_body:+d}, dags {sw_dag:+d}, tips lean {lean:+d}: {changed} px of body@ drawn again; "
              f"old cloth left {len(left)}, isolated {len(stray)}")
        bad += len(left) + len(stray)
    if bad:
        sys.exit(f"draw_cloth_walk: {bad} pixel(s) left or isolated (see above)")
    return 0


if __name__ == "__main__":
    sys.exit(run(os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE))
