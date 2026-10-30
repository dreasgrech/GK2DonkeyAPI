"""The hand-over render (docs/sprite-tools.md "Making a skin", step 5 "Look"): chosen frames of a skin as the game
composes them, at 1x, 3x and 8x, each with and without the rider, and the reference picture at the 3x height, so the
checklist is answered on these pictures before anyone else looks. One command for any skin in the repo; the pictures
go to the generator's out\\handover\\ (handover_1x3x.png, handover_8x_<n>.png one pair of frames each, handover_ref.png).

    python -I tools/skins/handover.py <skin> [frame ...] [--ref <picture>] [--out <folder>]

  <skin>   any name tools/skins/skinroots.py takes (armored, donkey/saddleup-donkey-skins.armored, dragon)
  frames   the frame keys to show (default: standing, walk 03, graze 04, turn 03, turn 05: one of every pose the
           head takes)
  --ref    a reference picture to put under the rows (the one the drawing was made from)
  --out    the folder for the pictures (default: tools/skins/<skin's short name>/out/handover)
"""
import os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "donkey"))
import review, skinroots
from gamedir import donkey_frames, game_dir

DEFAULT_FRAMES = ["bdy_static_left", "bdy_walk_left_03", "bdy_graze_left_04", "bdy_turn_left_03", "bdy_turn_left_05"]
BOX96 = (14, 14, 90, 70)  # the donkey and what is drawn on it, on the 96 canvas


def read_cfg(path):
    sec, out = "", {}
    for line in open(path, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if line.startswith("["):
            sec = line.strip("[]").strip().lower()
        elif "=" in line:
            k, v = (x.strip() for x in line.split("=", 1))
            out[(sec, k.lower())] = v
    return out


def arg(name, default=None):
    if name in sys.argv:
        i = sys.argv.index(name)
        return sys.argv[i + 1] if i + 1 < len(sys.argv) else default
    return default


def main():
    words = [a for i, a in enumerate(sys.argv[1:], 1) if not a.startswith("--") and not sys.argv[i - 1] in ("--ref", "--out")]
    if not words:
        print(__doc__); return 2
    folder = skinroots.find(words[0])
    if folder is None:
        print("no such skin:", words[0]); return 2
    keys = words[1:] or DEFAULT_FRAMES
    cfg = read_cfg(os.path.join(folder, "skin.cfg"))
    over = cfg.get(("skin", "layer"), "replace").lower() == "over"
    canvas = int(cfg.get(("skin", "canvas"), "96 96").split()[0])
    erase = None
    if over:
        e = cfg.get(("skin", "erase"), "FF00FF")
        erase = None if e.lower() == "none" else tuple(int(e[i:i + 2], 16) for i in (0, 2, 4))
    seat = cfg.get(("skin", "seat"))
    seat_row = review.DONKEY_SEAT_ROW + (canvas - 96) // 2 if over or seat is None or not seat.isdigit() else int(seat)
    rider_dx, seat_adj = review.rider_tuning(os.path.join(folder, "skin.cfg"))
    frames, front = {}, {}
    for k in keys:
        p = os.path.join(folder, k + ".png")
        frames[k] = Image.open(p).convert("RGBA") if os.path.isfile(p) else Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
        fp = os.path.join(folder, "front", k + ".png")
        if os.path.isfile(fp):
            front["front/" + k] = Image.open(fp).convert("RGBA")
    base = {k: v for k, v in review.load_base(donkey_frames()).items() if k in keys} if over else None
    rider = review.load_rider(os.path.join(game_dir(), "BepInEx", "GK2-asset-probe"))
    out_dir = arg("--out") or os.path.join(HERE, skinroots.short_name(os.path.basename(folder)), "out", "handover")
    os.makedirs(out_dir, exist_ok=True)
    off = (canvas - 96) // 2
    box = (BOX96[0] + off, BOX96[1] + off, BOX96[2] + off, BOX96[3] + off) if canvas >= 96 else (0, 0, canvas, canvas)
    bw, bh = box[2] - box[0], box[3] - box[1]
    shown = {}
    for k in keys:
        for with_rider in (False, True):
            f = review.composite(k, frames, base=base, erase=erase, front=front or None, rider=rider if with_rider else None,
                                 seat_row=seat_row, rider_dx=rider_dx, seat_adj=seat_adj)
            shown[(k, with_rider)] = f.crop(box)

    def row(S, items, label=True):
        im = Image.new("RGBA", ((bw * S + 5) * len(items), bh * S + 14), review.BG)
        d = ImageDraw.Draw(im)
        for i, (k, r) in enumerate(items):
            im.alpha_composite(shown[(k, r)].resize((bw * S, bh * S), Image.NEAREST), (i * (bw * S + 5), 14))
            if label:
                d.text((i * (bw * S + 5) + 2, 1), f"{k}{' rider' if r else ''} {S}x", fill=(255, 255, 0, 255))
        return im

    pairs = [(k, r) for k in keys for r in (False, True)]
    r1, r3 = row(1, pairs, label=False), row(3, pairs)
    sheet = Image.new("RGBA", (max(r1.width, r3.width), r1.height + r3.height), review.BG)
    sheet.alpha_composite(r1, (0, 0)); sheet.alpha_composite(r3, (0, r1.height))
    sheet.save(os.path.join(out_dir, "handover_1x3x.png"))
    for n, k in enumerate(keys):
        row(8, [(k, False), (k, True)]).save(os.path.join(out_dir, f"handover_8x_{n}.png"))
    ref = arg("--ref")
    if ref and os.path.isfile(ref):
        im = Image.open(ref).convert("RGBA")
        im = im.resize((max(1, im.width * bh * 3 // im.height), bh * 3), Image.LANCZOS)
        im.save(os.path.join(out_dir, "handover_ref.png"))
    print("handover ->", out_dir, "frames:", ", ".join(keys))
    return 0


if __name__ == "__main__":
    sys.exit(main())
