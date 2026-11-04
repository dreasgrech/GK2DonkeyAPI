"""Shades for a skin's parts, each part its own: a skin over the donkey whose parts share a ramp (the Jester's cap,
collar and caparison all in one burgundy and one navy) can't name them as tones as they are, since a colour belongs to
one place only (docs/mount-colours.md 7.1). This finds, for every part and ramp, the nearest shades to the ramp's that
the checks take (check_skin.py, SkinsScan):

  - a cube cell of their own (Colors.Rig.LutIndex: red and green rounded to 32 steps, blue floored): no colour of
    another place (the donkey's, one the skin keeps as drawn, another part's) in it; a part's own shades may share one;
  - not one step in one channel from another place's colour (the exact swap's reach, 0.004);
  - not a colour of the look already.

    python -I tools/skins/part_palette.py --ramp burg=3A0A14,641A28,862534,A23444,C2485A
        --ramp navy=141A3A,243266,34488A,4E66A8,6E86C8 --part caparison=burg,navy --part cap=burg,navy
        --part collar=burg,navy [--keep 5A3A0A,966414,E2A830,FFD652]

The parts are placed in the order given, each keeping its ramp exactly where the checks allow (give the one the eye
knows best first). The donkey's colours are read from its 44 frames (gamedir.donkey_frames(), the asset probe's
pictures); --keep adds the skin's colours kept as drawn. Prints each part's shades in the ramp's order and how far
each moved (in 0..255 steps per channel); exit 1 when a shade has no place within 12 steps of each channel.
"""
import argparse
import itertools
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gamedir import donkey_frames  # noqa: E402

REACH = 12  # how far a shade may move in each channel


def hexrgb(h):
    h = h.strip().lstrip("#")
    if len(h) != 6:
        raise SystemExit(f"not a colour: {h}")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def cell(rgb):
    r, g, b = rgb
    return (min(31, int(g / 255 * 31 + 0.5)), min(31, int(b / 255 * 31)), min(31, int(r / 255 * 31 + 0.5)))


def neighbours(rgb):
    for ch in range(3):
        for d in (-1, 1):
            n = list(rgb)
            n[ch] += d
            if 0 <= n[ch] <= 255:
                yield tuple(n)


def distance(a, b):
    # "redmean": closer to how far apart two colours look than plain RGB
    rm = (a[0] + b[0]) / 2
    dr, dg, db = a[0] - b[0], a[1] - b[1], a[2] - b[2]
    return ((2 + rm / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rm) / 256) * db * db) ** 0.5


def donkey_colours():
    folder = donkey_frames()
    if not os.path.isdir(folder):
        raise SystemExit(f"the donkey's frames aren't there: {folder} (the asset probe writes them; docs/sprite-tools.md)")
    cols = set()
    for name in os.listdir(folder):
        if name.endswith(".png"):
            b = Image.open(os.path.join(folder, name)).convert("RGBA").tobytes()
            cols |= {tuple(b[i:i + 3]) for i in range(0, len(b), 4) if b[i + 3] > 0}
    return cols


def main(argv):
    ap = argparse.ArgumentParser(description="shades for a skin's parts, each part its own")
    ap.add_argument("--ramp", action="append", required=True, help="name=HEX,HEX,... (dark to light)")
    ap.add_argument("--part", action="append", required=True, help="part=ramp,ramp,...")
    ap.add_argument("--keep", default="", help="the skin's colours kept as drawn: HEX,HEX,...")
    a = ap.parse_args(argv)
    ramps = {}
    for r in a.ramp:
        name, _, hexes = r.partition("=")
        ramps[name] = [hexrgb(h) for h in hexes.split(",") if h.strip()]
    places = {}  # colour -> place
    for c in donkey_colours():
        places[c] = "the donkey's"
    for h in a.keep.split(","):
        if h.strip():
            places.setdefault(hexrgb(h), "kept")
    cells = {}  # cell -> places in it
    for c, p in places.items():
        cells.setdefault(cell(c), set()).add(p)
    offsets = sorted(itertools.product(range(-REACH, REACH + 1), repeat=3), key=lambda d: d[0] * d[0] + d[1] * d[1] + d[2] * d[2])
    failed = False
    for spec in a.part:
        part, _, names = spec.partition("=")
        for rname in names.split(","):
            if rname not in ramps:
                raise SystemExit(f"no ramp {rname} (part {part})")
            place = f"{part}.{rname}"
            out = []
            for want in ramps[rname]:
                tries = sorted((tuple(v + d for v, d in zip(want, off)) for off in offsets), key=lambda c: distance(c, want))
                got = None
                for c in tries:
                    if any(v < 0 or v > 255 for v in c) or c in places or c in out:
                        continue
                    if cells.get(cell(c), set()) - {place}:
                        continue
                    if any(places.get(n, place) != place for n in neighbours(c)):
                        continue
                    got = c
                    break
                if got is None:
                    print(f"{place}: no shade for {'%02X%02X%02X' % want} within {REACH} steps")
                    failed = True
                    continue
                out.append(got)
                places[got] = place
                cells.setdefault(cell(got), set()).add(place)
            moved = [max(abs(x - y) for x, y in zip(o, w)) for o, w in zip(out, ramps[rname])]
            print(f"{place} = {' '.join('%02X%02X%02X' % c for c in out)}   # moved {moved}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
