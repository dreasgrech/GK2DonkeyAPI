"""The review pictures every generator writes the same way (docs/sprite-tools.md "Making a skin: the one pipeline"):
out\\<skin>\\strips\\<animation>.png (every animation as a strip of its frames, 4x, the frame's name on top: LOOK AT
EVERY STRIP before the game does), out\\<skin>\\preview.png (all 44 frames at 4x) and out\\<skin>\\pack\\sheet.png (the
cells 8 per row in frame order, for the checker's sheet mode and Aseprite).

    review.write_all(out_dir, frames, base=None, erase=None, front=None, rider=None, seat_row=None, rider_dx=0, seat_adj=0)

frames: {frame key: PIL RGBA image}, the skin's own pictures (the canvas size). base: {key: image} the pictures the
skin is drawn over (an over skin: the donkey's frames, review.load_base(folder)), composited under with the erase
colour cleared. front: {"front/<key>": image} the front layer, composited on top. rider: a PIL image of the rider (the
probe's rider.png, load_rider) drawn with his hip on seat_row (the canvas row), moved by the skin's own RiderOffsetPx
(rider_dx: positive = towards the head; the pictures face left) and SeatAdjustPx (seat_adj: positive = higher), as the
game places him (rider_tuning(cfg_path) reads both from a skin.cfg); None: no rider.
"""
import os
from PIL import Image, ImageDraw

ORDER = (["bdy_static_left"] + [f"bdy_walk_left_{i:02d}" for i in range(1, 9)] + [f"bdy_ear_left_{i:02d}" for i in range(1, 6)]
         + [f"bdy_graze_left_{i:02d}" for i in range(1, 6)] + [f"bdy_chew_left_{i:02d}" for i in range(1, 4)]
         + [f"bdy_lips_left_{i:02d}" for i in range(1, 14)] + [f"bdy_turn_left_{i:02d}" for i in range(1, 7)]
         + ["hed_static_down", "hed_static_down_02", "hed_static_down_03"])
BG = (46, 46, 50, 255)
RIDER_HIP_ABOVE_FEET = 7  # the keeper's hip is 7 px above his feet (Mount.SeatOn)
DONKEY_SEAT_ROW = 52      # the row the rider's hip sits on over the donkey's own seat, on the 96 canvas (docs/seat-investigation.md); +16 on 128


def from_flat(px, w, h):
    """A PIL image from a flat list of (r, g, b, a) tuples, row by row from the top (the creature generator's form)."""
    im = Image.new("RGBA", (w, h))
    im.putdata(px)
    return im


def load_base(folder):
    """The base's frames from a folder of <key>.png (the asset probe's donkey-frames): {key: image}."""
    out = {}
    for k in ORDER:
        p = os.path.join(folder, k + ".png")
        if os.path.isfile(p):
            out[k] = Image.open(p).convert("RGBA")
    return out


def rider_tuning(cfg_path):
    """(rider_dx, seat_adj): the skin's own RiderOffsetPx and SeatAdjustPx, [Tuning.Ride] over [Tuning], from its
    skin.cfg (0, 0 when absent)."""
    vals = {}
    if os.path.isfile(cfg_path):
        sec = ""
        for line in open(cfg_path, encoding="utf-8"):
            line = line.split("#", 1)[0].strip()
            if line.startswith("["):
                sec = line.strip("[]").strip().lower()
            elif "=" in line and sec in ("tuning", "tuning.ride"):
                k, v = (x.strip() for x in line.split("=", 1))
                try:
                    vals[(sec, k.lower())] = int(float(v))
                except ValueError:
                    pass
    dx = vals.get(("tuning.ride", "rideroffsetpx"), vals.get(("tuning", "rideroffsetpx"), 0))
    adj = vals.get(("tuning.ride", "seatadjustpx"), vals.get(("tuning", "seatadjustpx"), 0))
    return dx, adj


def composite(key, frames, base=None, erase=None, front=None, rider=None, seat_row=None, rider_dx=0, seat_adj=0):
    """One frame as the game shows it: the base under (with the erase colour cleared where the skin marks it), the
    skin, the rider at the seat row, the front layer on top."""
    skin = frames.get(key)
    w, h = (skin.size if skin is not None else next(iter(frames.values())).size)
    f = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    if base is not None and key in base:
        b = base[key].copy()
        ox, oy = (w - b.width) // 2, (h - b.height) // 2  # the base centred on a bigger canvas, as the game places it
        if skin is not None and erase is not None:
            bp, sp = b.load(), skin.load()
            for y in range(h):
                for x in range(w):
                    if sp[x, y][3] and sp[x, y][:3] == erase and 0 <= x - ox < b.width and 0 <= y - oy < b.height:
                        bp[x - ox, y - oy] = (0, 0, 0, 0)
        f.alpha_composite(b, (ox, oy))
    if skin is not None:
        if erase is not None:
            s = skin.copy()
            sp = s.load()
            for y in range(h):
                for x in range(w):
                    if sp[x, y][3] and sp[x, y][:3] == erase:
                        sp[x, y] = (0, 0, 0, 0)
            f.alpha_composite(s)
        else:
            f.alpha_composite(skin)
    if rider is not None and seat_row is not None and not key.startswith("hed_"):
        # the rider's hip on the seat row (less the nudge: positive = higher); his feet row is the picture's lowest
        # opaque row; RiderOffsetPx > 0 moves him towards the head, and the pictures face left
        rp = rider.load()
        feet = max((y for y in range(rider.height) for x in range(rider.width) if rp[x, y][3]), default=rider.height - 1)
        dy = seat_row - seat_adj - (feet - RIDER_HIP_ABOVE_FEET)
        f.alpha_composite(rider, ((w - rider.width) // 2 - rider_dx, dy))
    if front is not None and ("front/" + key) in front:
        f.alpha_composite(front["front/" + key])
    return f


def write_all(out_dir, frames, base=None, erase=None, front=None, rider=None, seat_row=None, rider_dx=0, seat_adj=0, scale=4):
    """Strips, the preview and the pack sheet into out_dir. Returns the strips folder."""
    os.makedirs(out_dir, exist_ok=True)
    keys = [k for k in ORDER if k in frames]
    w, h = next(iter(frames.values())).size
    shown = {k: composite(k, frames, base, erase, front, rider, seat_row, rider_dx, seat_adj) for k in keys}
    # the strips: one per animation, the frame's name above each cell
    strips_dir = os.path.join(out_dir, "strips")
    os.makedirs(strips_dir, exist_ok=True)
    groups = {}
    for k in keys:
        groups.setdefault(k.split("_")[1] if k.startswith("bdy_") else "head", []).append(k)
    S = scale
    for g, items in groups.items():
        strip = Image.new("RGBA", (w * S * len(items), h * S + 16), BG)
        d = ImageDraw.Draw(strip)
        for i, k in enumerate(items):
            strip.alpha_composite(shown[k].resize((w * S, h * S), Image.NEAREST), (i * w * S, 16))
            d.text((i * w * S + 3, 2), k.replace("bdy_", "").replace("_left", ""), fill=(255, 255, 0, 255))
        strip.save(os.path.join(strips_dir, g + ".png"))
    # the preview: all frames, 8 per row, in frame order
    C = 8
    rows = (len(ORDER) + C - 1) // C
    prev = Image.new("RGBA", (C * w * S, rows * h * S), BG)
    for n, k in enumerate(ORDER):
        if k in shown:
            prev.alpha_composite(shown[k].resize((w * S, h * S), Image.NEAREST), ((n % C) * w * S, (n // C) * h * S))
    prev.save(os.path.join(out_dir, "preview.png"))
    # the pack sheet: the skin's own cells at their frame's place (the checker's sheet mode and the watcher's split
    # read cells by frame order, so a missing frame leaves an empty cell)
    pack = os.path.join(out_dir, "pack")
    os.makedirs(pack, exist_ok=True)
    sheet = Image.new("RGBA", (C * w, rows * h), (0, 0, 0, 0))
    for n, k in enumerate(ORDER):
        if k in frames:
            sheet.alpha_composite(frames[k], ((n % C) * w, (n // C) * h))
    sheet.save(os.path.join(pack, "sheet.png"))
    return strips_dir


def load_rider(probe_dir):
    """The probe's rider picture (BepInEx\\GK2-asset-probe\\rider.png), or None."""
    p = os.path.join(probe_dir, "rider.png")
    return Image.open(p).convert("RGBA") if os.path.isfile(p) else None
