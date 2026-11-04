"""Which pixels each colour row of a skin reaches: the skin's frames with every tone painted one vivid colour of its
own, every [Colors] region's colours another, the colours no row reaches (kept as drawn) white, and the donkey under
an over skin grey; a legend below with each part's name and pixel count. For finding the parts a skin still lacks
(docs/mount-colours.md section 7: every part of a skin its own row) and checking a split.

    python -I tools/skins/part_map.py <pack folder> [frame ...] [--out file.png]

Frames: bdy_static_left bdy_walk_left_03 bdy_turn_left_05 bdy_graze_left_04 unless named. Out: part_map.png in the
current folder. Read only: never changes the pack.
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gamedir import donkey_frames  # noqa: E402

VIVID = [(255, 60, 60), (60, 120, 255), (255, 200, 0), (0, 220, 120), (255, 0, 255), (0, 230, 230), (255, 140, 0),
         (160, 90, 255), (140, 255, 60), (255, 120, 190), (0, 150, 255), (200, 255, 255), (255, 230, 140), (120, 60, 20)]
REGIONS = {"coat": (150, 110, 70), "dark": (90, 70, 50), "muzzle": (230, 170, 150), "eye": (255, 255, 120), "harness": (180, 120, 40), "outline": (70, 40, 110)}
SCALE = 5


def parse(path):
    """[(place, name, [rgb])] in the file's order: each tone, then each region of [Colors] and [Region.<region>] colours =."""
    tones, regions, sec = {}, {}, None
    order = []
    for line in open(path, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if not line:
            continue
        if line.startswith("["):
            sec = line.strip("[]").strip().lower()
            if sec.startswith("tone.") and sec not in tones:
                tones[sec] = {"name": sec[5:], "colours": []}
                order.append(sec)
            continue
        if "=" not in line or sec is None:
            continue
        k, v = (x.strip() for x in line.split("=", 1))
        k = k.lower()
        if sec.startswith("tone."):
            if k == "name":
                tones[sec]["name"] = v
            elif k in ("colours", "colors"):
                tones[sec]["colours"] += [h for h in v.replace(",", " ").split() if len(h) == 6]
        elif sec == "colors" and k in REGIONS:
            regions.setdefault(k, []).extend(h for h in v.replace(",", " ").split() if len(h) == 6)
        elif sec.startswith("region.") and sec[7:] in REGIONS and k in ("colours", "colors"):
            regions.setdefault(sec[7:], []).extend(h for h in v.replace(",", " ").split() if len(h) == 6)
    rgb = lambda h: tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
    out = [(t, tones[t]["name"], [rgb(h) for h in tones[t]["colours"]]) for t in order]
    out += [("region." + r, r.capitalize() + " (region)", [rgb(h) for h in hs]) for r, hs in regions.items()]
    return out


def main(argv):
    out_path = "part_map.png"
    if "--out" in argv:
        i = argv.index("--out")
        out_path = argv[i + 1]
        del argv[i:i + 2]
    if not argv:
        raise SystemExit(__doc__)
    pack, frames = argv[0], argv[1:] or ["bdy_static_left", "bdy_walk_left_03", "bdy_turn_left_05", "bdy_graze_left_04"]
    places = parse(os.path.join(pack, "skin.cfg"))
    over = any(l.split("#", 1)[0].replace(" ", "").lower() == "layer=over" for l in open(os.path.join(pack, "skin.cfg"), encoding="utf-8"))
    paint, legend = {}, []
    for n, (place, name, cols) in enumerate(places):
        colour = VIVID[n % len(VIVID)]
        legend.append([name, colour, 0])
        for c in cols:
            paint.setdefault(c, len(legend) - 1)
    kept = {}
    tiles = []
    donkey_dir = donkey_frames()
    own = set()  # over the donkey: its own colours, which a skin drawing one follows the donkey's rows with (Coat and the others)
    for name in (os.listdir(donkey_dir) if over and os.path.isdir(donkey_dir) else []):
        if name.endswith(".png"):
            b = Image.open(os.path.join(donkey_dir, name)).convert("RGBA").tobytes()
            own |= {tuple(b[i:i + 3]) for i in range(0, len(b), 4) if b[i + 3] > 0}
    if over:
        legend.append(["the donkey's own colours (its Coat and other rows)", (200, 170, 130), 0])
    else:  # a look in its own colours: every colour it names nowhere is its Coat (docs/mount-colours.md 2.2)
        legend.append(["Coat (region: every colour not named)", VIVID[len(legend) % len(VIVID)], 0])
    donkey_row = len(legend) - 1
    for f in frames:
        size = Image.open(os.path.join(pack, f + ".png")).size
        im = Image.new("RGBA", size, (30, 30, 34, 255))
        base = os.path.join(donkey_dir, f + ".png")
        if over and os.path.exists(base):  # the donkey under a skin worn over it only
            d = Image.open(base).convert("RGBA")
            grey = Image.new("RGBA", d.size, (0, 0, 0, 0))
            dp, gp = d.load(), grey.load()
            for y in range(d.height):
                for x in range(d.width):
                    if dp[x, y][3] > 0:
                        v = sum(dp[x, y][:3]) // 3
                        gp[x, y] = (60 + v // 3, 60 + v // 3, 64 + v // 3, 255)
            im.alpha_composite(grey, (0, 0))
        for layer in (os.path.join(pack, f + ".png"), os.path.join(pack, "front", f + ".png")):
            if not os.path.exists(layer):
                continue
            sk = Image.open(layer).convert("RGBA")
            sp, ip = sk.load(), im.load()
            for y in range(sk.height):
                for x in range(sk.width):
                    p = sp[x, y]
                    if p[3] == 0:
                        continue
                    c = p[:3]
                    if c == (255, 0, 255):
                        continue  # the erase colour: clears the donkey, draws nothing
                    if c in paint or c in own or not over:
                        n = paint.get(c, donkey_row)
                        legend[n][2] += 1
                        ip[x, y] = legend[n][1] + (255,)
                        continue
                    kept[c] = kept.get(c, 0) + 1
                    ip[x, y] = (255, 255, 255, 255)
        tiles.append(im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST))
    rows = len(legend) + (1 if kept else 0)
    w = sum(t.width for t in tiles)
    sheet = Image.new("RGBA", (w, tiles[0].height + 18 * rows + 10), (20, 20, 24, 255))
    x = 0
    for t in tiles:
        sheet.paste(t, (x, 0))
        x += t.width
    draw = ImageDraw.Draw(sheet)
    y = tiles[0].height + 4
    for name, colour, count in legend:
        draw.rectangle([8, y + 2, 22, y + 14], fill=colour)
        draw.text((30, y + 2), f"{name}: {count} px", fill=(230, 230, 230))
        y += 18
    if kept:
        draw.rectangle([8, y + 2, 22, y + 14], fill=(255, 255, 255))
        draw.text((30, y + 2), "kept as drawn (no row reaches it): " + ", ".join(f"{'%02X%02X%02X' % c} {n}px" for c, n in sorted(kept.items(), key=lambda kv: -kv[1])), fill=(230, 230, 230))
    sheet.save(out_path)
    for name, colour, count in legend:
        print(f"{name}: {count} px")
    if kept:
        print("kept as drawn:", ", ".join(f"{'%02X%02X%02X' % c} ({n} px)" for c, n in sorted(kept.items(), key=lambda kv: -kv[1])))
    print("saved", out_path)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
