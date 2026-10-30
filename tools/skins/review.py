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


def motion_check(layers, anchors, reference="bdy_static_left"):
    """One piece with the body (docs/front-layer.md section 5: a part re-rasterised between two anchors jiggled in the
    world): per frame and per layer, the layer's box against its anchor, compared with the reference frame's. layers:
    {frame key: {layer name: {(x, y): colour} or a PIL image}}; anchors: {frame key: {layer name: (ax, ay)}} (the
    point the layer is meant to move with: the hip, the collar, an ear base). Returns the report's lines: per layer
    the frames whose (left - ax, top - ay, width, height) differ from the reference's, with the numbers; a fitted
    piece (cut to the frame's own outline) changes size by design, so read the lines, the check asserts nothing."""
    def box(layer):
        pts = layer if isinstance(layer, dict) else {(x, y) for x in range(layer.width) for y in range(layer.height) if layer.getpixel((x, y))[3]}
        if not pts:
            return None
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        return min(xs), min(ys), max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
    names = sorted({n for L in layers.values() for n in L})
    lines = []
    for n in names:
        rel = {}
        for k, L in layers.items():
            b = box(L.get(n)) if n in L else None
            a = anchors.get(k, {}).get(n)
            if b is None or a is None:
                continue
            rel[k] = (b[0] - a[0], b[1] - a[1], b[2], b[3])
        if reference not in rel:
            lines.append(f"{n}: no reference frame"); continue
        ref = rel[reference]
        diff = {k: v for k, v in rel.items() if v != ref}
        same = len(rel) - len(diff)
        lines.append(f"{n}: (left-ax, top-ay, w, h) = {ref} in {same} of {len(rel)} frames" + (": differs in " + ", ".join(f"{k} {v}" for k, v in diff.items()) if diff else ""))
    return lines


def phase_check(layers, anchors, periods, skip=()):
    """A repeating pattern on a piece that moves (rivets every third column, a checkered hem) must repeat from the
    piece's anchor, not from the canvas: phased to the canvas it crawls one column along the piece whenever the anchor
    shifts between frames (a shimmer at game speed that strips and box checks never show). periods: {colour: n} for
    the colours that mark a pattern's dots (a rivet's deep steel, the padding's dark red, the hem's light brass);
    for every frame and layer, the columns of each such colour must all sit at the same residues modulo n from the
    layer's anchor column as in the reference frame (bdy_static_left). Returns lines: "ok <layer>" or "FAIL <layer>
    <frame>: <colour> at residues {..} (reference {..})". Pixels of the colour on a solid row (a seam, not dots) are
    counted too, so give the dot colours only."""
    lines = []
    names = sorted({n for L in layers.values() for n in L} - set(skip))
    for n in names:
        res = {}
        for k, L in layers.items():
            layer = L.get(n)
            a = anchors.get(k, {}).get(n)
            if not layer or a is None:
                continue
            pts = layer if isinstance(layer, dict) else {(x, y): layer.getpixel((x, y))[:3] for x in range(layer.width) for y in range(layer.height) if layer.getpixel((x, y))[3]}
            per = {}
            for (x, y), c in pts.items():
                if c in periods:
                    per.setdefault(c, set()).add((x - a[0]) % periods[c])
            res[k] = per
        if "bdy_static_left" not in res:
            continue
        ref = res["bdy_static_left"]
        fails = [(k, c, v) for k, per in res.items() for c, v in per.items() if c in ref and v != ref[c]]
        if fails:
            for k, c, v in fails:
                lines.append(f"FAIL {n} {k}: {c} at residues {sorted(v)} (reference {sorted(ref[c])})")
        else:
            lines.append(f"ok {n}: every pattern repeats from the anchor in all {len(res)} frames")
    return lines
