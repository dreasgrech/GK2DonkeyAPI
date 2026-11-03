"""Pixel art as text: a picture written as grids of letters over a palette, placed on the canvas, rendered into a PNG
(a stamp.py piece, any skin picture); and any picture's box written back as such a grid. Exact to the pixel, and
read in a diff as easily as code: a hat or a bell drawn this way is changed by changing a letter.

    python -I tools/skins/pixgrid.py render <file.pix> <out.png> [--canvas 96]
    python -I tools/skins/pixgrid.py dump <picture.png> x0 y0 x1 y1 [<file.pix>]

A .pix file:

    # a comment (also after a palette line)
    K = 2A161C          # a palette letter (any character but . ~ @ # and space) and its colour, RRGGBB
    R = C42838
    @ 30 22             # a grid: its top-left pixel on the canvas, then its rows, one character per pixel
    ..KK..
    .KRRK.
    KRRRRK
    @ 40 30             # another grid (a later one is drawn over an earlier one where they meet)
    ...

"." is transparent, "~" the erase colour (FF00FF: clears the donkey under an over skin, docs/skins.md). A row may be
shorter than the others; spaces in a row are an error (a grid is written as it is drawn). dump writes the box with
a palette made of the picture's colours (A, B, C ... in the order met), to start a .pix from a picture or to read
one pixel by pixel.
"""
import os
import sys

from PIL import Image

ERASE = (255, 0, 255, 255)
LETTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"


def parse(path):
    """{(x, y): (r, g, b, a)} of a .pix file; exits with the line on a mistake."""
    palette, pixels, at, row = {}, {}, None, 0
    for n, raw in enumerate(open(path, encoding="utf-8").read().splitlines(), 1):
        line = raw.split("#", 1)[0].rstrip() if not raw.lstrip().startswith("@") else raw.split("#", 1)[0].strip()
        if not line.strip():
            continue
        where = f"{os.path.basename(path)} line {n}"
        s = line.strip()
        if s.startswith("@"):
            parts = s[1:].split()
            if len(parts) != 2 or not all(p.lstrip("-").isdigit() for p in parts):
                sys.exit(f"pixgrid: {where}: a grid starts with '@ x y'")
            at, row = (int(parts[0]), int(parts[1])), 0
            continue
        if at is None:
            if "=" not in s:
                sys.exit(f"pixgrid: {where}: a palette line is 'K = RRGGBB' (or a grid's '@ x y' first)")
            k, v = (t.strip() for t in s.split("=", 1))
            if len(k) != 1 or k in ".~@# ":
                sys.exit(f"pixgrid: {where}: a palette letter is one character other than . ~ @ #")
            if len(v) != 6 or any(c not in "0123456789abcdefABCDEF" for c in v):
                sys.exit(f"pixgrid: {where}: a colour is RRGGBB")
            palette[k] = (int(v[0:2], 16), int(v[2:4], 16), int(v[4:6], 16), 255)
            continue
        if " " in line.strip():
            sys.exit(f"pixgrid: {where}: a space inside a grid row")
        for i, ch in enumerate(line.strip()):
            if ch == ".":
                continue
            if ch == "~":
                pixels[(at[0] + i, at[1] + row)] = ERASE
            elif ch in palette:
                pixels[(at[0] + i, at[1] + row)] = palette[ch]
            else:
                sys.exit(f"pixgrid: {where}: '{ch}' is not in the palette")
        row += 1
    return pixels


def render(path, out, canvas=96):
    im = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    px = im.load()
    outside = 0
    for (x, y), c in parse(path).items():
        if 0 <= x < canvas and 0 <= y < canvas:
            px[x, y] = c
        else:
            outside += 1
    if outside:
        sys.exit(f"pixgrid: {path}: {outside} pixel(s) outside the {canvas} canvas")
    im.save(out)
    return im


def dump(picture, box, out=None):
    im = Image.open(picture).convert("RGBA")
    px = im.load()
    x0, y0, x1, y1 = box
    names, rows = {}, []
    for y in range(y0, y1 + 1):
        r = ""
        for x in range(x0, x1 + 1):
            c = px[x, y]
            if not c[3]:
                r += "."
            elif c == ERASE:
                r += "~"
            else:
                h = "%02X%02X%02X" % c[:3]
                if h not in names:
                    if len(names) == len(LETTERS):
                        sys.exit("pixgrid: more colours in the box than letters")
                    names[h] = LETTERS[len(names)]
                r += names[h]
        rows.append(r)
    text = "".join(f"{k} = {h}\n" for h, k in names.items()) + f"@ {x0} {y0}\n" + "".join(r + "\n" for r in rows)
    if out:
        with open(out, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    else:
        print(text, end="")


def main(argv):
    if len(argv) >= 3 and argv[0] == "render":
        canvas = int(argv[argv.index("--canvas") + 1]) if "--canvas" in argv else 96
        render(argv[1], argv[2], canvas)
        print(f"{argv[2]}: rendered from {argv[1]}")
        return 0
    if len(argv) >= 6 and argv[0] == "dump":
        dump(argv[1], tuple(int(v) for v in argv[2:6]), argv[6] if len(argv) > 6 else None)
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
