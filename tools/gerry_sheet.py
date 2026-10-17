"""Contact sheets of the frames gerry_probe.py exported (docs/gerry-mount.md): one row per animation at 2x with the
cell outlined (gerry_sheet.png), and the walk cycles, the somersault and the heads at 8x (gerry_zoom.png).

    python tools/gerry_sheet.py <folder with the exported PNGs>

The sheets land next to the frames. They are the game's art: keep them out of the repository.
"""
import os
import sys

from PIL import Image, ImageDraw

PREFIX = "0901_"


def rows_of(src):
    rows = {}
    for n in sorted(os.listdir(src)):
        if not (n.endswith(".png") and n.startswith(PREFIX)):
            continue
        key = n[:-4]
        parts = key.split("_")
        anim = "_".join(parts[1:-1]) if parts[-1].isdigit() else "_".join(parts[1:])
        rows.setdefault(anim, []).append(key)
    return rows


def sheet(src, rows, scale, cell_w, cell_h, name):
    maxcols = max(len(v) for v in rows.values())
    cw, ch = cell_w * scale, cell_h * scale
    img = Image.new("RGBA", (140 + maxcols * (cw + 6), len(rows) * (ch + 24)), (60, 60, 60, 255))
    d = ImageDraw.Draw(img)
    y = 0
    for anim, keys in rows.items():
        d.text((4, y + ch // 2), anim, fill=(255, 255, 255, 255))
        x = 140
        for k in keys:
            im = Image.open(os.path.join(src, k + ".png")).convert("RGBA")
            d.rectangle([x, y, x + cw - 1, y + ch - 1], outline=(100, 100, 100, 255))
            big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
            img.alpha_composite(big, (x + (cw - big.width) // 2, y + ch - big.height - 2 * scale))
            x += cw + 6
        y += ch + 24
    path = os.path.join(src, name)
    img.save(path)
    print("wrote", path, img.size)


def main():
    src = sys.argv[1]
    rows = rows_of(src)
    sheet(src, dict(sorted(rows.items())), 2, 96, 96, "gerry_sheet.png")
    zoom = {
        "walk_left": [PREFIX + "bdy_walk_left_%02d" % i for i in range(1, 16, 2)],
        "walk_down": [PREFIX + "bdy_walk_down_%02d" % i for i in range(1, 16, 2)],
        "walk_up": [PREFIX + "bdy_walk_up_%02d" % i for i in range(1, 16, 2)],
        "somersault": [PREFIX + "bdy_somersault_left_%02d" % i for i in range(1, 17, 2)],
        "heads": [PREFIX + "hed_static_left", PREFIX + "hed_static_down", PREFIX + "hed_static_up", PREFIX + "hed_speak_left_02", PREFIX + "hed_speak_down_03"],
    }
    zoom = {k: [f for f in v if os.path.exists(os.path.join(src, f + ".png"))] for k, v in zoom.items()}
    sheet(src, zoom, 8, 24, 34, "gerry_zoom.png")


if __name__ == "__main__":
    main()
