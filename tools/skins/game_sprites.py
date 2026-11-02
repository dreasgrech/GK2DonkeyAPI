"""The game's character sprites as style references (2026-11-01, Armored's redraw: the steel was sampled from the
game's armoured guards). Reads the character atlas bundle (the addressables bundle holding the donkey's
0301_bdy_walk_left_01; found by content, the bundle names change with the game) and writes pictures for looking at.
The pictures are the game's art: they are never written inside this repository (the tool refuses).

    pip install UnityPy        (run without -I: UnityPy sits in the user's site-packages)
    python tools/skins/game_sprites.py presets <out.png>                       # every preset: a body and a head, 3x
    python tools/skins/game_sprites.py export <out.png> <scale> <prefix> ... [--only <regex>]
    python tools/skins/game_sprites.py palette <out folder> <sprite name> ...  # colours (count, hex, HSV) + a 12x crop

Presets worth knowing: 0301 the donkey; 0341, 0622, 0626, 0627, 0641 armoured guards (helmets, breastplates: the
steel Armored uses); 0901 Gerry. The game is Unity 6000.3.9f1 and its bundles carry no version string, so UnityPy needs
the fallback set below.
"""
import colorsys, glob, os, re, sys

import UnityPy
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gamedir import game_dir

UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.9f1"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MARK = "0301_bdy_walk_left_01"
BG = (46, 46, 50, 255)


def outside_repo(path):
    p = os.path.abspath(path)
    if os.path.commonpath([p, REPO]) == REPO:
        sys.exit(f"refused: {p} is inside the repository (the game's art stays out of it)")
    return p


def atlas():
    """The character atlas bundle: the one holding the donkey's MARK sprite; largest bundles first (it is big)."""
    folder = os.path.join(game_dir(), "GraveyardKeeper2_Data", "StreamingAssets", "aa", "StandaloneWindows64")
    for path in sorted(glob.glob(os.path.join(folder, "*.bundle")), key=os.path.getsize, reverse=True)[:200]:
        try:
            env = UnityPy.load(path)
            for obj in env.objects:
                if obj.type.name == "Sprite" and obj.read().m_Name == MARK:
                    return env
        except Exception:
            continue
    sys.exit(f"no bundle under {folder} holds {MARK}")


def sprites(env):
    """{prefix: {rest of the name: sprite}} for the NNNN_ sprites."""
    out = {}
    for obj in env.objects:
        if obj.type.name != "Sprite":
            continue
        d = obj.read()
        m = re.match(r"^(\d{4})_(.*)$", d.m_Name or "")
        if m:
            out.setdefault(m.group(1), {})[m.group(2)] = d
    return out


def first(names, patterns):
    for pat in patterns:
        for n in sorted(names):
            if re.match(pat, n):
                return n
    return None


def presets(out):
    by = sprites(atlas())
    cells = []
    for pre in sorted(by):
        chosen = []
        for pats in ((r"bdy_static_left$", r"bdy_idle.*left", r"bdy_walk_left_01$", r"bdy_.*left"), (r"hed_static_left$", r"hed_.*left", r"hed_")):
            n = first(by[pre], pats)
            if n and n not in chosen:
                chosen.append(n)
        ims = []
        for n in chosen:
            try:
                ims.append(by[pre][n].image.convert("RGBA"))
            except Exception:
                pass
        if ims:
            cells.append((pre, len(by[pre]), ims))
    S, W = 3, 1800
    x = y = rowh = 0
    placed = []
    for pre, count, ims in cells:
        w = sum(im.width * S + 6 for im in ims) + 10
        h = max(im.height * S for im in ims) + 16
        if x + w > W:
            x, y, rowh = 0, y + rowh + 6, 0
        placed.append((x, y, pre, count, ims))
        x += w
        rowh = max(rowh, h)
    sheet = Image.new("RGBA", (W, y + rowh + 10), BG)
    dr = ImageDraw.Draw(sheet)
    for x, y, pre, count, ims in placed:
        dr.text((x + 2, y), f"{pre} ({count})", fill=(255, 230, 90, 255))
        xx = x
        for im in ims:
            sheet.alpha_composite(im.resize((im.width * S, im.height * S), Image.NEAREST), (xx, y + 12))
            xx += im.width * S + 6
    sheet.save(out)
    print(out, sheet.size, len(cells), "presets")


def export(out, scale, prefixes, only):
    by = sprites(atlas())
    cells, W, y = [], 2400, 0
    for p in prefixes:
        x = rowh = 0
        for n in sorted(by.get(p, {})):
            if only and not only.search(n):
                continue
            try:
                im = by[p][n].image.convert("RGBA")
            except Exception:
                continue
            w, h = im.width * scale + 8, im.height * scale + 14
            if x + w > W:
                x, y, rowh = 0, y + rowh, 0
            cells.append((x, y, f"{p}_{n}", im))
            x += w
            rowh = max(rowh, h)
        y += rowh + 10
    sheet = Image.new("RGBA", (W, y + 10), BG)
    dr = ImageDraw.Draw(sheet)
    for x, y, n, im in cells:
        dr.text((x, y), n, fill=(255, 230, 90, 255))
        sheet.alpha_composite(im.resize((im.width * scale, im.height * scale), Image.NEAREST), (x, y + 12))
    sheet.save(out)
    print(out, sheet.size, len(cells), "sprites")


def palette(out_dir, names):
    os.makedirs(out_dir, exist_ok=True)
    want = set(names)
    for obj in atlas().objects:
        if obj.type.name != "Sprite":
            continue
        d = obj.read()
        if d.m_Name not in want:
            continue
        im = d.image.convert("RGBA")
        im.resize((im.width * 12, im.height * 12), Image.NEAREST).save(os.path.join(out_dir, d.m_Name + "_12x.png"))
        im.save(os.path.join(out_dir, d.m_Name + ".png"))
        counts = {}
        for p in im.getdata():
            if p[3]:
                counts[p[:3]] = counts.get(p[:3], 0) + 1
        print(f"== {d.m_Name} {im.size} {len(counts)} colours")
        for c, n in sorted(counts.items(), key=lambda t: -t[1])[:40]:
            h, s, v = colorsys.rgb_to_hsv(*(x / 255 for x in c))
            print(f"  {n:4d}  #{c[0]:02X}{c[1]:02X}{c[2]:02X}  h{h * 360:4.0f} s{s:.2f} v{v:.2f}")


def main(argv):
    if len(argv) < 2:
        print(__doc__); return 2
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = re.compile(argv[i + 1])
        argv = argv[:i] + argv[i + 2:]
    cmd = argv[0]
    if cmd == "presets":
        presets(outside_repo(argv[1]))
    elif cmd == "export" and len(argv) >= 4:
        export(outside_repo(argv[1]), int(argv[2]), argv[3:], only)
    elif cmd == "palette" and len(argv) >= 3:
        palette(outside_repo(argv[1]), argv[2:])
    else:
        print(__doc__); return 2
    return 0


if __name__ == "__main__":
    import warnings
    warnings.filterwarnings("ignore", message=".*Unity version.*")
    sys.exit(main(sys.argv[1:]))
