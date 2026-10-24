"""Writes a [Colors] section into each built-in creature skin.cfg (docs/mount-colours.md 2.6): which of the skin's own
colours are Dark, Muzzle, Eye or Harness; everything else is Coat. The rules are DATA, tools/skins/creatures/colors.json
(per skin, per region, in order: a colour's hue, saturation and value against thresholds; the first match wins; the
rest is Coat), so a new creature adds a block there, never code here. The hex lists come from the PNGs (front/ too),
so they are exact; a skin with "source": "game-export" (Gerry) reads the in-game export folder instead. Re-runnable
(replaces the section, then the canonical layout): run it after the art changes, then tests/SkinsScan. colors.json is
keyed by the skin's full name (saddleup.dragon/default); a skin on the command line is any name skinroots.find
takes (saddleup.dragon/default, dragon, donkey/saddleup.hellfire, hellfire).

    python -I tools/skins/skin_colors.py [skin ...]
"""
import colorsys, io, json, os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image

import skinroots
REPO = skinroots.REPO


RULES_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "creatures", "colors.json")


def export_dir(skin):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from gamedir import game_dir
    return os.path.join(game_dir(), "BepInEx", "GK2-asset-probe", "game-skins", *skin.split("/"))  # Looks.ExportBuilt writes a skin to <mount id>/<skin>/


def colours(skin, source=None):
    d = export_dir(skin) if source == "game-export" else skinroots.find(skin)
    if d is None:
        return None
    if not os.path.isdir(d):
        return None
    cols = {}
    files = [os.path.join(d, f) for f in sorted(os.listdir(d)) if f.endswith(".png")]
    front = os.path.join(d, "front")
    if os.path.isdir(front):
        files += [os.path.join(front, f) for f in sorted(os.listdir(front)) if f.endswith(".png")]
    for path in files:
        im = Image.open(path).convert("RGBA")
        data = im.get_flattened_data() if hasattr(im, "get_flattened_data") else im.getdata()
        for r, g, b, a in data:
            if a:
                cols[(r, g, b)] = cols.get((r, g, b), 0) + 1
    out = []
    for (r, g, b), n in cols.items():
        h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
        out.append(("%02X%02X%02X" % (r, g, b), n, h * 360, s, v))
    return out


# region of a colour by (hue in degrees, saturation, value), from colors.json: {skin: {region: [ {h_min, h_max, s_min, s_max,
# v_min, v_max}, ... ]}}; the regions in the order written, the first matching rule wins, else Coat
def load_rules():
    with open(RULES_FILE, encoding="utf-8") as f:
        data = json.load(f)
    return {k: v for k, v in data.items() if not k.startswith("_")}


def region_of(rules, h, s, v):
    for region, conds in rules.items():
        if region.startswith("_") or region == "source":
            continue
        for c in conds:
            ok = True
            for key, val in c.items():
                if key not in ("h_min", "h_max", "s_min", "s_max", "v_min", "v_max"):
                    raise SystemExit(f"colors.json: '{key}' is not a rule key (h_min, h_max, s_min, s_max, v_min, v_max)")
                x = {"h": h, "s": s, "v": v}[key[0]]
                if key.endswith("_min") and x < val: ok = False
                if key.endswith("_max") and x > val: ok = False
            if ok:
                return region
    return "Coat"


RULES = load_rules()


def write_section(skin, regions):
    p = os.path.join(skinroots.find(skin), "skin.cfg")
    raw = io.open(p, encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in raw else "\n"
    t = raw.replace("\r\n", "\n")
    # every earlier [Colors] section out: the comment lines right above its header, the header, and its key lines up
    # to the next comment or section header (so the comment above the section that follows stays with that section)
    lines = t.split("\n")
    while True:
        at = next((i for i, l in enumerate(lines) if l.strip().lower().startswith("[colors]")), None)
        if at is None:
            break
        start = at
        while start > 0 and lines[start - 1].startswith("#"):
            start -= 1
        while start > 0 and lines[start - 1].strip() == "":
            start -= 1
        end = at + 1
        while end < len(lines) and not lines[end].startswith("[") and not lines[end].startswith("#"):
            end += 1
        del lines[start:end]
    t = "\n".join(lines)
    t = re.sub(r"\n{3,}", "\n\n", t)
    t = t.rstrip("\n") + "\n"
    lines = ["", "# The skin's colours by mount region (docs/mount-colours.md); every colour not named is Coat.", "[Colors]"]
    for region in ("Dark", "Muzzle", "Eye", "Harness"):
        hexes = regions.get(region, [])
        if hexes:
            lines.append("%s = %s" % (region, " ".join(hexes)))
    t += "\n".join(lines) + "\n"
    # then the one layout every built-in keeps (the colour sections before the mount block, the file's line ends):
    # tools/skins/canonical_cfg.py's rule, so a run of this script never moves a section
    import canonical_cfg
    io.open(p, "w", encoding="utf-8", newline="").write(t.replace("\n", nl))
    text = canonical_cfg.canonical(p)  # read back first: opening for writing empties the file
    io.open(p, "w", encoding="utf-8", newline="").write(text)


for skin in RULES:
    if "/" not in skin:
        raise SystemExit(f"colors.json: '{skin}' is not a skin's full name (<mount id>/<skin>, as saddleup.dragon/default)")
WANTED = None
if len(sys.argv) > 1:
    WANTED = set()
    for a in sys.argv[1:]:
        d = skinroots.find(a)
        if d is None and a not in RULES:
            raise SystemExit(f"{a}: no such skin (a full name like saddleup.dragon/default, a mount's name, or a skin's folder name)")
        WANTED.add(skinroots.full_name(d) if d else a)
for skin in sorted(RULES):
    if WANTED is not None and skin not in WANTED:
        continue
    got = colours(skin, RULES[skin].get("source"))
    if got is None:
        print(skin + ": no pictures to read" + (" (export the loaded skins from the Mounts tab first)" if RULES[skin].get("source") == "game-export" else "") + "; left as it is")
        continue
    cols = sorted(got, key=lambda c: -c[1])
    regions = {}
    for hx, n, h, s, v in cols:
        regions.setdefault(region_of(RULES[skin], h, s, v), []).append(hx)
    print(skin + ":", ", ".join("%s %d" % (k, len(v)) for k, v in sorted(regions.items())))
    for k in ("Dark", "Muzzle", "Eye", "Harness"):
        if k in regions and len(regions[k]) <= 12:
            print("   ", k, " ".join(regions[k]))
    write_section(skin, regions)
print("done")
