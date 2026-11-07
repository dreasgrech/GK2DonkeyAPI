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
  python -I tools/skins/frame_check.py sweep <dir> [clip ...] [--min N]
      every clip (or the ones named), every frame against the one before (and the last against the first, marked
      "wrap"): the motion taken out block by block (each 6x6 block at its best shift, up to 3 px), and every group of
      pixels still changed listed with its box, size and colours (now <- before). A group of N px or more (default 3).
      What it lists is a place to look at with closeup and shift: motion that bends a part (a leg, a beating wing)
      shows too, and only a look tells it from a defect.
  python -I tools/skins/frame_check.py heat <dir> <out.png> [clip ...]
      the sweep as pictures: per clip its first frame in grey, each pixel tinted by how many frame pairs it changed in
      (yellow few, red most), four clips a row.

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


def clips_of(d):
    # {clip: frame count} from the folder's bdy_<clip>_left_NN.png
    out = {}
    for f in os.listdir(d):
        if f.startswith("bdy_") and f.endswith(".png") and "_left_" in f:
            clip, num = f[4:-4].rsplit("_left_", 1)
            if num.isdigit():
                out[clip] = max(out.get(clip, 0), int(num))
    return out


def packed(im):
    import numpy as np
    a = np.asarray(im, dtype=np.uint32)
    p = (a[..., 0] << 24) | (a[..., 1] << 16) | (a[..., 2] << 8) | a[..., 3]
    p[a[..., 3] == 0] = 0                                   # every transparent pixel the same
    return p


def residual(cur, prev, block=6, reach=3, shifts=False):
    # the pixels of cur that differ from prev with each block at its best shift (motion taken out); with shifts=True
    # also each pixel's block's shift (the larger of |dx|, |dy|): what a part moved where it changed
    import numpy as np
    h, w = cur.shape
    hb, wb = -(-h // block), -(-w // block)
    best_bad, best_mask = None, None
    best_mv = np.zeros((hb, wb), dtype=np.int32)
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            sh = np.zeros_like(prev)
            ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
            xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
            sh[yd, xd] = prev[ys, xs]
            diff = cur != sh
            pad = np.zeros((hb * block, wb * block), dtype=bool)
            pad[:h, :w] = diff
            bad = pad.reshape(hb, block, wb, block).sum(axis=(1, 3))
            mv = max(abs(dx), abs(dy))
            if best_bad is None:
                best_bad, best_mask = bad, pad
                best_mv[:] = mv
            else:
                # the better shift, and on a tie the smaller move (a block that is the same either way didn't move)
                better = (bad < best_bad) | ((bad == best_bad) & (mv < best_mv))
                best_bad = np.where(better, bad, best_bad)
                best_mv = np.where(better, mv, best_mv)
                sel = np.repeat(np.repeat(better, block, axis=0), block, axis=1)
                best_mask = np.where(sel, pad, best_mask)
    if shifts:
        return best_mask[:h, :w], np.repeat(np.repeat(best_mv, block, axis=0), block, axis=1)[:h, :w]
    return best_mask[:h, :w]


def groups(mask, min_px):
    # 8-connected groups of a bool mask: [(cells)], biggest first
    h, w = mask.shape
    seen = set()
    out = []
    for y in range(h):
        for x in range(w):
            if mask[y, x] and (x, y) not in seen:
                st, cells = [(x, y)], []
                seen.add((x, y))
                while st:
                    cx, cy = st.pop()
                    cells.append((cx, cy))
                    for ddy in (-1, 0, 1):
                        for ddx in (-1, 0, 1):
                            q = (cx + ddx, cy + ddy)
                            if 0 <= q[0] < w and 0 <= q[1] < h and q not in seen and mask[q[1], q[0]]:
                                seen.add(q)
                                st.append(q)
                if len(cells) >= min_px:
                    out.append(cells)
    out.sort(key=len, reverse=True)
    return out


def sweep(d, names, min_px):
    from collections import Counter
    clips = clips_of(d)
    for clip in (names or sorted(clips)):
        n = clips.get(clip)
        if not n:
            print(f"{clip}: no frames")
            continue
        fr = [load(d, clip, k) for k in range(1, n + 1)]
        px = [packed(f) for f in fr]
        rgb = [f.load() for f in fr]
        lines = []
        total = 0
        for i in range(n if n > 1 else 0):
            j = i - 1                                        # i = 0: the last frame before the first (the loop's wrap)
            m, mv = residual(px[i], px[j], shifts=True)
            gs = groups(m, min_px)
            total += int(m.sum())
            for cells in gs:
                xs, ys = [c[0] for c in cells], [c[1] for c in cells]
                moved = max(int(mv[c[1], c[0]]) for c in cells)
                now = Counter("%02X%02X%02X" % rgb[i][c][:3] if rgb[i][c][3] else "------" for c in cells)
                was = Counter("%02X%02X%02X" % rgb[j][c][:3] if rgb[j][c][3] else "------" for c in cells)
                # moved 0 or 1 px and still changed: the part shimmers in place (the first to look at); moved more: a
                # part moving and bending (a step, a beat), usually motion
                kind = "SHIMMER" if moved <= 1 else "moving "
                lines.append((moved > 1, -len(cells), f"  {kind} {j % n + 1:02d}->{i + 1:02d}{' wrap' if i == 0 else '     '} {len(cells):4d} px  moved {moved}"
                              f"  box {min(xs)},{min(ys)}-{max(xs)},{max(ys)}  now {' '.join(c for c, _ in now.most_common(3))}"
                              f"  was {' '.join(c for c, _ in was.most_common(3))}"))
        lines.sort(key=lambda t: (t[0], t[1]))
        sh = sum(1 for t in lines if not t[0])
        print(f"{clip} ({n} frames): {total} px changed with the motion out, {len(lines)} group(s) of {min_px}+ px, {sh} shimmering in place")
        for _, _, line in lines:
            print(line)


def heat(d, out, names, z=3):
    # per clip a tile: its first frame in grey, each pixel tinted by how many of the clip's frame pairs it changed in
    # with the motion out (yellow few, red most); where a defect hides in a moving clip
    import numpy as np
    clips = clips_of(d)
    names = names or sorted(clips)
    tiles = []
    for clip in names:
        n = clips[clip]
        fr = [load(d, clip, k) for k in range(1, n + 1)]
        px = [packed(f) for f in fr]
        cnt = np.zeros(px[0].shape, dtype=np.int32)
        for i in range(n if n > 1 else 0):
            cnt += residual(px[i], px[i - 1])
        base = np.asarray(fr[0].convert("LA").convert("RGBA"), dtype=np.float32)
        img = np.zeros(base.shape, dtype=np.uint8)
        img[...] = (40, 40, 40, 255)
        a = base[..., 3:4] / 255.0
        img[..., :3] = (base[..., :3] * 0.55 * a + 40 * (1 - a)).astype(np.uint8)
        top = max(1, n)
        hot = cnt > 0
        f = (cnt / top).clip(0, 1)
        img[hot, 0] = 255
        img[hot, 1] = (230 * (1 - f[hot])).astype(np.uint8)
        img[hot, 2] = 0
        tile = Image.fromarray(img, "RGBA").resize((img.shape[1] * z, img.shape[0] * z), Image.NEAREST)
        ImageDraw.Draw(tile).text((3, 2), f"{clip} ({n}): {int(cnt.sum())} px", fill=(255, 255, 255, 255))
        tiles.append(tile)
    cols = 4
    tw, th = tiles[0].size
    sheet = Image.new("RGBA", (cols * (tw + 6), -(-len(tiles) // cols) * (th + 6)), (20, 20, 20, 255))
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % cols) * (tw + 6), (i // cols) * (th + 6)))
    sheet.save(out)
    print(out)


def main(a):
    if len(a) < 2:
        sys.exit(__doc__)
    cmd = a[0]
    if cmd == "heat":
        heat(a[1], a[2], a[3:])
        return
    if cmd == "sweep":
        rest = list(a[2:])
        min_px = 3
        if "--min" in rest:
            k = rest.index("--min")
            min_px = int(rest[k + 1])
            del rest[k:k + 2]
        sweep(a[1], rest, min_px)
        return
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
