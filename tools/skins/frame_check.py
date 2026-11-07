"""Frame-by-frame checks of a skin's pictures (a folder of bdy_<clip>_left_NN.png, with front\\ composited over), for
flicker and parts that come loose, before a skin is shown (docs/dragon-references.md 12).

  python -I tools/skins/frame_check.py closeup <dir> <clip> <n> <x0> <y0> <x1> <y1> <zoom> <out.png>
      the clip's frames cropped and enlarged, in rows: composed on grass, main alone, front alone; a grid every 4 px.
  python -I tools/skins/frame_check.py shift <dir> <clip> <n> <x0> <y0> <x1> <y1>
      each frame's box against frame 1's at its best whole-pixel shift (up to 4 px): the shift, and how many pixels
      still differ (0: the part only moved; more: it changed). A crop of a moving part misleads the eye; this doesn't.
  python -I tools/skins/frame_check.py attach <dir> <clip> <n> <part box x0 y0 x1 y1> <body box x0 y0 x1 y1>
      a part's move against the body's, per frame (each box's best shift against frame 1): a part riding the body
      moves with it; one that moves on its own (a head that holds its height while the body bobs) shows the difference.
  python -I tools/skins/frame_check.py gold <dir> <rrggbb> <clip:n>...
      how many pixels of a colour each frame has (a rim or rivet that blinks changes the count).

Boxes are in the picture's pixels. Read-only: it writes only the out.png it is given."""
import os
import sys

from PIL import Image, ImageDraw


def load(d, clip, k):
    name = f"bdy_{clip}_left_{k:02d}"
    path = os.path.join(d, name + ".png")
    if not os.path.isfile(path):
        sys.exit(f"no {path}")
    im = Image.open(path).convert("RGBA")
    fp = os.path.join(d, "front", name + ".png")
    if os.path.isfile(fp):
        im.alpha_composite(Image.open(fp).convert("RGBA"))
    return im


def best_shift(ref, cur, box, reach=4):
    x0, y0, x1, y1 = box
    a, b = ref.load(), cur.load()
    w, h = cur.size
    best = None
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            bad = 0
            for y in range(y0, y1):
                for x in range(x0, x1):
                    q = b[x + dx, y + dy] if 0 <= x + dx < w and 0 <= y + dy < h else (0, 0, 0, 0)
                    if a[x, y] != q:
                        bad += 1
            if best is None or bad < best[2]:
                best = (dx, dy, bad)
    return best


def closeup(d, clip, n, box, z, out):
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    cw, ch = w * z + 10, h * z + 16
    sheet = Image.new("RGBA", (cw * n + 40, ch * 3 + 4), (30, 30, 30, 255))
    dr = ImageDraw.Draw(sheet)
    for k in range(1, n + 1):
        name = f"bdy_{clip}_left_{k:02d}"
        m = Image.open(os.path.join(d, name + ".png")).convert("RGBA").crop(box)
        fp = os.path.join(d, "front", name + ".png")
        f = Image.open(fp).convert("RGBA").crop(box) if os.path.isfile(fp) else Image.new("RGBA", (w, h))
        comp = m.copy()
        comp.alpha_composite(f)
        for r, im in enumerate((comp, m, f)):
            bg = Image.new("RGBA", (w, h), (86, 120, 52, 255) if r == 0 else (60, 60, 70, 255))
            bg.alpha_composite(im)
            big = bg.resize((w * z, h * z), Image.NEAREST)
            gd = ImageDraw.Draw(big)
            for gx in range(0, w, 4):
                gd.line([(gx * z, 0), (gx * z, h * z)], fill=(0, 0, 0, 90))
            for gy in range(0, h, 4):
                gd.line([(0, gy * z), (w * z, gy * z)], fill=(0, 0, 0, 90))
            ox, oy = 40 + (k - 1) * cw, r * ch + 14
            sheet.alpha_composite(big, (ox, oy))
            if r == 0:
                dr.text((ox, 1), f"{clip}_{k:02d}", fill=(255, 255, 255, 255))
    for r, lab in enumerate(("comp", "main", "front")):
        dr.text((2, r * ch + 20), lab, fill=(255, 255, 255, 255))
    dr.text((2, 2), f"x{x0} y{y0}", fill=(255, 255, 0, 255))
    sheet.save(out)
    print(out)


def main(a):
    if len(a) < 2:
        sys.exit(__doc__)
    cmd = a[0]
    if cmd == "closeup" and len(a) == 10:
        closeup(a[1], a[2], int(a[3]), tuple(int(v) for v in a[4:8]), int(a[8]), a[9])
    elif cmd == "shift" and len(a) == 8:
        d, clip, n, box = a[1], a[2], int(a[3]), tuple(int(v) for v in a[4:8])
        ref = load(d, clip, 1)
        for k in range(1, n + 1):
            dx, dy, bad = best_shift(ref, load(d, clip, k), box)
            print(f"{clip}_{k:02d} shift ({dx},{dy}) differ {bad}")
    elif cmd == "attach" and len(a) == 12:
        d, clip, n = a[1], a[2], int(a[3])
        part, body = tuple(int(v) for v in a[4:8]), tuple(int(v) for v in a[8:12])
        ref = load(d, clip, 1)
        worst = 0
        for k in range(1, n + 1):
            cur = load(d, clip, k)
            p, b = best_shift(ref, cur, part), best_shift(ref, cur, body)
            rel = (p[0] - b[0], p[1] - b[1])
            worst = max(worst, abs(rel[0]), abs(rel[1]))
            print(f"{clip}_{k:02d} part ({p[0]},{p[1]}) left {p[2]}  body ({b[0]},{b[1]}) left {b[2]}  part against body {rel}")
        print(f"worst move against the body: {worst} px")
    elif cmd == "gold" and len(a) >= 4:
        d, col = a[1], tuple(int(a[2][i:i + 2], 16) for i in (0, 2, 4))
        for spec in a[3:]:
            clip, n = spec.split(":")
            row = []
            for k in range(1, int(n) + 1):
                row.append(sum(1 for q in load(d, clip, k).get_flattened_data() if q[3] and q[:3] == col))
            print(clip, row)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])
