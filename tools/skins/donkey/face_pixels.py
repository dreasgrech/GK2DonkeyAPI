"""The donkey's eye and nostril pixels (docs/face-pixels.md): the pixels of the game's donkey frames that are the dark
fill's colours (292316, 352e1e) but belong to the eye or the nose, which a colour swap can't tell from the legs' and the
mane's. The API repaints them in the game's atlas with colours of their own (FacePixels.cs) from the list this writes,
src/GK2DonkeyAPI/face-pixels.txt: per pixel its frame key, its canvas place (x, y from the top), its colour and the one
it gets. Coordinates and colours only: no picture of the game's is in the repo.

The rule (checked on every frame against the contact sheet, the user's calls in docs/face-pixels.md section 2):
- eye: 292316 or 352e1e within 2 (Chebyshev) of an eye point of tools/skins/donkey/rig.json (eye, eye2), so the closed
  lid of the blink and the lowered head's eye are found too;
- nostril: 352e1e or 292316 with a 4-neighbour of the nose's colours (the Muzzle's abb298 858f6c 75755a a9a7a2), within
  5 of the muzzle point.
The new colours (PATCH), checked on every run (verify): drawn nowhere (the donkey's frames, the packs' skins over the
donkey), more than 0.004 from every other colour paired on a look over the donkey (the exact swap's reach), alone in
their colour-cube cell (Colors.Rig.LutIndex) among those colours; the eye's in no region of Colors.cs (it stays as
drawn), the nostril's under Muzzle (the user, 2026-11-04: "nostril should be the same as muzzle"). A pixel of a colour
with no new colour chosen for its part stops the tool: a colour is picked by those rules first.

    python tools/skins/donkey/face_pixels.py                 # writes src/GK2DonkeyAPI/face-pixels.txt
    python tools/skins/donkey/face_pixels.py --check         # compares with the file, changes nothing (release scans)
    python tools/skins/donkey/face_pixels.py --sheet <png>   # the contact sheet, outside the repo
The frames: the asset probe's export (gamedir.donkey_frames(), the frames the skins were drawn on; the probe maps the
new colours back to the game's, so it stays the art as drawn).
"""
import glob, json, math, os, re, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from gamedir import donkey_frames

REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
OUT = os.path.join(REPO, "src", "GK2DonkeyAPI", "face-pixels.txt")
COLORS_CS = os.path.join(REPO, "src", "GK2DonkeyAPI", "Colors.cs")
RIG = os.path.join(HERE, "rig.json")
R_EYE, R_MUZZLE = 2, 5
DARK, MANE = (0x29, 0x23, 0x16), (0x35, 0x2E, 0x1E)
NOSE = {(0xAB, 0xB2, 0x98), (0x85, 0x8F, 0x6C), (0x75, 0x75, 0x5A), (0xA9, 0xA7, 0xA2)}
# (part, the game's colour) -> the API's; the region each new colour is in (None: in no region, as drawn)
PATCH = {("eye", DARK): "292319", ("nostril", MANE): "362D1E"}
REGION = {"eye": None, "nostril": "Muzzle"}
SWAP_REACH = 0.004   # Colors.cs: a texel within this RGB distance of a pair's colour takes its replacement


def hexs(c):
    return "%02X%02X%02X" % c


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def find(folder, rig):
    """[(key, x, y_from_top, part, original hex, patch hex)] over every frame of the export."""
    rows = []
    for key in sorted(rig):
        path = os.path.join(folder, key + ".png")
        if not os.path.exists(path):
            continue
        im = Image.open(path).convert("RGBA")
        px, (w, h) = im.load(), im.size
        pts = rig[key].get("points", {})
        eyes = [tuple(pts[k]) for k in ("eye", "eye2") if pts.get(k)]
        mz = tuple(pts["muzzle"]) if pts.get("muzzle") else None

        def opaque(x, y):
            return 0 <= x < w and 0 <= y < h and px[x, y][3] >= 128
        for y in range(h):
            for x in range(w):
                if not opaque(x, y):
                    continue
                c = px[x, y][:3]
                if c not in (DARK, MANE):
                    continue
                if any(max(abs(x - ex), abs(y - ey)) <= R_EYE for ex, ey in eyes):
                    part = "eye"
                elif mz and max(abs(x - mz[0]), abs(y - mz[1])) <= R_MUZZLE and any(opaque(x + dx, y + dy) and px[x + dx, y + dy][:3] in NOSE for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    part = "nostril"
                else:
                    continue
                if (part, c) not in PATCH:
                    sys.exit(f"{key} ({x}, {y}): a {part} pixel of {hexs(c)}, which has no new colour chosen (PATCH): pick one by the rules in this file's docstring first")
                rows.append((key, x, y, part, hexs(c), PATCH[(part, c)]))
    return rows


def regions():
    """{region: set of hex} of the donkey's sprites in Colors.cs (the R("Coat", ..., "Donkey", (Group.DonkeySprites, "...")) lines)."""
    out = {}
    for m in re.finditer(r'R\("(\w+)", "[^"]*", "Donkey", \(Group\.DonkeySprites, "([0-9a-fA-F ]+)"\)', open(COLORS_CS, encoding="utf-8").read()):
        out[m.group(1)] = {h.upper() for h in m.group(2).split()}
    return out


def cell(c):
    """Colors.Rig.LutIndex's cell: red and green rounded to 32 steps, blue floored."""
    r, g, b = c
    return (min(31, int(r / 255 * 31 + 0.5)), min(31, int(g / 255 * 31 + 0.5)), min(31, int(b / 255 * 31)))


def verify(folder):
    """The new colours against the rules in the docstring; [problems] (empty: all hold)."""
    bad = []
    reg = regions()
    if not reg:
        return [f"no donkey region read from {COLORS_CS}"]
    paired = set().union(*reg.values())
    drawn = set()
    skins = sorted(glob.glob(os.path.join(REPO, "packs", "*", "gk2mounts", "donkey", "skins", "*", "skin.cfg")))
    for cfg in skins:
        sec = None
        for line in open(cfg, encoding="utf-8"):
            line = line.split("#", 1)[0].strip()
            if line.startswith("["):
                sec = line.strip("[]").strip()
            elif "=" in line and sec and (sec.lower() == "colors" or (sec.lower().startswith(("tone.", "region.")) and line.lower().startswith(("colours", "colors")))):
                paired |= {t.upper() for t in re.findall(r"\b[0-9a-fA-F]{6}\b", line.split("=", 1)[1])}
        for p in glob.glob(os.path.join(os.path.dirname(cfg), "**", "*.png"), recursive=True):
            b = Image.open(p).convert("RGBA").tobytes()
            drawn |= {"%02X%02X%02X" % tuple(b[i:i + 3]) for i in range(0, len(b), 4) if b[i + 3] > 0}
    for p in glob.glob(os.path.join(folder, "*.png")):
        if not os.path.basename(p).startswith(("bdy_", "hed_")):
            continue  # the cart's wheel (wheel_walk_left_*) shares the export but is coloured with the cart, not the donkey's sprites
        b = Image.open(p).convert("RGBA").tobytes()
        drawn |= {"%02X%02X%02X" % tuple(b[i:i + 3]) for i in range(0, len(b), 4) if b[i + 3] > 0}
    for (part, _), new in sorted(PATCH.items()):
        where = [r for r, cols in reg.items() if new in cols]
        want = REGION[part]
        if (want is None and where) or (want is not None and where != [want]):
            bad.append(f"{new} ({part}) is in {', '.join(where) or 'no region'} of Colors.cs, wanted in {want or 'none'}")
        if new in drawn:
            bad.append(f"{new} ({part}) is drawn in the donkey's frames or a pack's skin: those pixels would be taken for repainted ones (the export's and the check's way back)")
        others = paired - {new}
        near = min((math.dist(rgb(new), rgb(o)) / 255.0, o) for o in others)
        if near[0] <= SWAP_REACH:
            bad.append(f"{new} ({part}) is {near[0]:.4f} from {near[1]}, inside the exact swap's {SWAP_REACH}")
        share = sorted(o for o in (others | drawn) - {new} if cell(rgb(o)) == cell(rgb(new)) and (o in paired or want is not None))
        if share:
            bad.append(f"{new} ({part}) shares its colour-cube cell {cell(rgb(new))} with {', '.join(share)}")
    return bad


def text(rows):
    lines = ["# The donkey's eye and nostril pixels the API repaints in the game's atlas (FacePixels.cs; docs/face-pixels.md).",
             "# Made by tools/skins/donkey/face_pixels.py from tools/skins/donkey/rig.json and the asset probe's export of the",
             "# donkey's frames; do not edit. key x y(from the top, 96 canvas) part original new"]
    lines += [f"{k} {x} {y} {part} {o} {n}" for k, x, y, part, o, n in rows]
    return "\n".join(lines) + "\n"


def sheet(folder, rows, out):
    p = os.path.abspath(out)
    if os.path.commonpath([p, REPO]) == REPO:
        sys.exit(f"refused: {p} is inside the repository (the game's art stays out of it)")
    by = {}
    for k, x, y, part, o, n in rows:
        by.setdefault(k, []).append((x, y, part))
    tiles = []
    for key in sorted(by):
        im = Image.open(os.path.join(folder, key + ".png")).convert("RGBA")
        xs, ys = [x for x, _, _ in by[key]], [y for _, y, _ in by[key]]
        box = (max(0, min(xs) - 8), max(0, min(ys) - 8), min(im.size[0], max(xs) + 9), min(im.size[1], max(ys) + 9))
        crop, k = im.crop(box), 10
        base = Image.new("RGBA", crop.size, (40, 40, 40, 255))
        base.alpha_composite(crop)
        big = base.resize((crop.size[0] * k, crop.size[1] * k), Image.NEAREST)
        d = ImageDraw.Draw(big)
        for x, y, part in by[key]:
            X, Y = (x - box[0]) * k, (y - box[1]) * k
            d.rectangle([X + 1, Y + 1, X + k - 2, Y + k - 2], outline=(255, 40, 40) if part == "eye" else (40, 120, 255), width=2)
        d.text((3, 2), key, fill=(255, 255, 255))
        tiles.append(big)
    cols = 6
    tw, th = max(t.width for t in tiles), max(t.height for t in tiles)
    s = Image.new("RGBA", (cols * (tw + 4), ((len(tiles) + cols - 1) // cols) * (th + 4)), (0, 0, 0, 255))
    for i, t in enumerate(tiles):
        s.paste(t, ((i % cols) * (tw + 4), (i // cols) * (th + 4)))
    s.save(p)
    print("sheet:", p)


def main(argv):
    folder = donkey_frames()
    if not os.path.isdir(folder):
        sys.exit(f"no export of the donkey's frames at {folder}: run the asset probe (tools/GK2AssetProbe, Shift+F7) first")
    rig = json.load(open(RIG, encoding="utf-8"))["frames"]
    rows = find(folder, rig)
    if not rows:
        sys.exit("no eye or nostril pixel found: the export or rig.json is not what the rule was checked on")
    new = {n for *_, n in rows}
    t = text(rows)
    frames = len({r[0] for r in rows})
    counts = {}
    for r in rows:
        counts[(r[3], r[4])] = counts.get((r[3], r[4]), 0) + 1
    summary = ", ".join(f"{part} {o}->{PATCH[(part, rgb(o))]}: {n}" for (part, o), n in sorted(counts.items()))
    if "--sheet" in argv:
        sheet(folder, rows, argv[argv.index("--sheet") + 1])
        return
    bad = verify(folder)
    if bad:
        sys.exit("the new colours break their rules:\n  " + "\n  ".join(bad))
    print(f"the new colours ({', '.join(sorted(new))}): drawn nowhere, outside the exact swap's reach of every other paired colour, alone in their cube cells, in their regions")
    if "--check" in argv:
        have = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else ""
        if have.replace("\r\n", "\n") == t:
            print(f"face-pixels.txt as the rule finds it: {len(rows)} pixels on {frames} frames ({summary})")
            return
        sys.exit(f"face-pixels.txt differs from what the rule finds ({len(rows)} pixels on {frames} frames): run without --check")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(t)
    print(f"{OUT}: {len(rows)} pixels on {frames} frames ({summary})")


if __name__ == "__main__":
    main(sys.argv[1:])
