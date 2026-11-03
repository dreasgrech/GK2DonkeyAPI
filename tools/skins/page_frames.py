"""Review what the skins preview page shows, exactly: the page's own pictures of the skins (its composites, with the
skin's erase marks and its own [Base.*] recolour of the donkey already applied, as the user sees them), cut out of the
page and laid out side by side, a row per skin, the clip's frames in order, zoomed. A review of a skin change looks
at this, not at a separate composite (2026-11-02: a review render without the recolour missed what the page showed).

    python tools/skins/page_frames.py <out.png> --skins "Hellfire,Hellfire (in progress)" --clip turn
    python tools/skins/page_frames.py <out.png> --skins Armored --clip all --scale 3
    python tools/skins/page_frames.py <out.png> --skins Hellfire --clip turn --box 20 18 56 52 --scale 8

    --page <html>   the page (default: the probe's skins-preview.html in the game folder)
    --skins         skin names as the cards show them, comma separated (default: every skin of the donkey)
    --clip          a clip of the page's clip list (walk, turn, graze ...), or "all" for every frame (default: all)
    --scale         pixel scale (default 4)
    --box x0 y0 x1 y1   the part of the 96 canvas to show (default: the whole canvas)
    --rider         the page's rider on each skin's seat row, as its Rider button puts him: between the skin's
                    picture and its front layer (docs/front-layer.md), so what the front layer covers of him shows
    --blind         rows titled A, B ... in the order of --skins instead of the skins' names: for a fresh reviewer
                    asked "which is a jester?" against a shipped skin (2026-11-03: six reviews had asked only
                    whether pieces slide, and the Jester's cloth was Royal Parade's block recoloured)

A skin's front layer (its front\\ pictures) is drawn over each frame, as the page and the game draw it: with --rider
over him, without over the donkey (nobody riding).

The output holds the game's art (the donkey's frames): write it outside the repository (the scratchpad, the probe
folder). It refuses a path inside the repo."""
import base64
import io
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from gamedir import game_dir

REPO = os.path.dirname(os.path.dirname(HERE))


def arg(argv, name, default=None, n=1):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if n == 1 else argv[i + 1:i + 1 + n]
    return default


def page_data(path):
    html = open(path, encoding="utf-8").read()
    start = html.index("const GROUPS = ") + len("const GROUPS = ")
    groups, end = json.JSONDecoder().raw_decode(html, start)
    rest = html[end:]
    cs = rest.index("CLIPS = ") + len("CLIPS = ")
    clips, _ = json.JSONDecoder().raw_decode(rest, cs)
    os_ = rest.index("ORDER = ") + len("ORDER = ")
    order, _ = json.JSONDecoder().raw_decode(rest, os_)
    rs = rest.index(", RIDER = ") + len(", RIDER = ")
    rider, _ = json.JSONDecoder().raw_decode(rest, rs)
    hs = rest.index("RIDER_HIP = ") + len("RIDER_HIP = ")
    hip, _ = json.JSONDecoder().raw_decode(rest, hs)
    return groups, clips, order, rider, hip


def rider_at(s, hip):
    """Where the page's Rider button puts the rider's 96 picture on a skin's canvas (its riderPlace(), facing left):
    his hip (row 'hip' of his picture) on the skin's seat row (less its seat nudge), moved back by RiderOffsetPx."""
    c = s.get("canvas") or 96
    return ((c - 96) // 2 - (s.get("rider_dx") or 0), s["seat"] - (s.get("seat_adj") or 0) - hip)


def over(base, top, x, y):
    """top laid over base with its top-left at (x, y), cut to base (either may be negative)."""
    x0, y0 = max(0, -x), max(0, -y)
    part = top.crop((x0, y0, min(top.width, base.width - x), min(top.height, base.height - y)))
    if part.width > 0 and part.height > 0:
        base.alpha_composite(part, (x + x0, y + y0))


def picture(url):
    return Image.open(io.BytesIO(base64.b64decode(url.split(",", 1)[1]))).convert("RGBA")


def main(argv):
    if len(argv) < 1:
        print(__doc__)
        return 1
    out = os.path.abspath(argv[0])
    if os.path.normcase(out).startswith(os.path.normcase(os.path.abspath(REPO)) + os.sep):
        print(f"refused: {out} is inside the repository (the pictures hold the game's art)")
        return 1
    page = arg(argv, "--page") or os.path.join(game_dir(), "BepInEx", "GK2-asset-probe", "skins-preview.html")
    groups, clips, order, rider_url, rider_hip = page_data(page)
    rider = None
    if "--rider" in argv:
        if not rider_url:
            print("no rider on the page (the probe's rider.png is missing)")
            return 1
        rider = picture(rider_url)
    donkey = next(g for g in groups if g["id"] == "donkey")
    names = arg(argv, "--skins")
    wanted = [n.strip() for n in names.split(",")] if names else [s["name"] for s in donkey["skins"]]
    by_name = {s["name"]: s for g in groups for s in g["skins"]}
    missing = [n for n in wanted if n not in by_name]
    if missing:
        print("no such skin on the page:", ", ".join(missing), "| there are:", ", ".join(by_name))
        return 1
    clip = arg(argv, "--clip", "all")
    keys = order if clip == "all" else clips.get(clip)
    if not keys:
        print("no such clip:", clip, "| there are:", ", ".join(clips))
        return 1
    own = by_name["The game's own"]
    keys = [k for k in keys if k in own["frames"] or any(k in by_name[n]["frames"] for n in wanted)]   # the page's ORDER
    # also lists other mounts' frames (the dragon's flight)
    S = int(arg(argv, "--scale", "4"))
    box = tuple(int(v) for v in arg(argv, "--box", None, 4)) if "--box" in argv else (0, 0, 96, 96)
    cw, ch = (box[2] - box[0]) * S, (box[3] - box[1]) * S
    per_row = len(keys) if clip != "all" else 11
    rows = [(n, [keys[i:i + per_row] for i in range(0, len(keys), per_row)]) for n in wanted]
    lines = sum(len(chunks) for _, chunks in rows)
    img = Image.new("RGBA", (per_row * (cw + 4) + 8, lines * (ch + 18) + len(rows) * 16 + 4), (74, 105, 58, 255))
    dr = ImageDraw.Draw(img)
    y = 2
    blind = "--blind" in argv
    for r, (name, chunks) in enumerate(rows):
        s = by_name[name]
        dr.text((6, y), chr(ord("A") + r) if blind else name, fill=(255, 240, 160, 255))
        y += 16
        for chunk in chunks:
            for i, k in enumerate(chunk):
                url = s["frames"].get(k) or own["frames"].get(k)
                if not url:
                    continue
                im = picture(url)
                if rider is not None and s.get("seat") is not None:
                    over(im, rider, *rider_at(s, rider_hip))
                near = s["frames"].get("front/" + k)
                if near:
                    over(im, picture(near), 0, 0)
                im = im.crop(box).resize((cw, ch), Image.NEAREST)
                x = 6 + i * (cw + 4)
                img.alpha_composite(im, (x, y + 14))
                dr.text((x + 2, y + 2), str(order.index(k) + 1 if clip == "all" else i + 1) + " " + k.replace("bdy_", "").replace("_left", ""),
                        fill=(255, 255, 255, 255))
            y += ch + 18
    img.save(out)
    print(out, img.size)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
