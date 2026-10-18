"""Writes a [Colors] section into each built-in creature skin.cfg (docs/mount-colours.md 2.6): which of the skin's own
colours are Dark, Muzzle, Eye or Harness; everything else is Coat. The rules per skin were read off the enlarged
standing frames; the hex lists come from the PNGs, so they are exact. Re-runnable (replaces the section): run it
after the art changes, then tests/SkinsScan. Pillow needed: python -I tools/skin_colors.py"""
import colorsys, io, os, re
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # tools/ -> the repo
SKINS = os.path.join(REPO, "src", "GK2DonkeyAPI", "assets", "skins")


def colours(skin):
    d = os.path.join(SKINS, skin)
    cols = {}
    for f in sorted(os.listdir(d)):
        if not f.endswith(".png"):
            continue
        im = Image.open(os.path.join(d, f)).convert("RGBA")
        data = im.get_flattened_data() if hasattr(im, "get_flattened_data") else im.getdata()
        for r, g, b, a in data:
            if a:
                cols[(r, g, b)] = cols.get((r, g, b), 0) + 1
    out = []
    for (r, g, b), n in cols.items():
        h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
        out.append(("%02X%02X%02X" % (r, g, b), n, h * 360, s, v))
    return out


# region of a colour by (hex, count, hue in degrees, saturation, value)
RULES = {
    # Coat = the red scales and the wing; Muzzle = the golden belly; Dark = outlines, horns and back spikes (bone);
    # Eye = the yellow eye.
    "dragon": lambda hx, n, h, s, v: "Dark" if v < 0.2 or (s < 0.5 and v < 0.9 and 25 <= h <= 45) else "Eye" if h >= 40 and v > 0.9 else "Muzzle" if 20 <= h <= 45 else "Coat",
    # The blue dragon: Coat = the blue scales and wing; Muzzle = the golden belly; Dark = outlines, horns, spikes; Eye = yellow.
    "dragon-blue": lambda hx, n, h, s, v: "Dark" if v < 0.2 or (s < 0.5 and v < 0.9 and 25 <= h <= 45) else "Eye" if 35 <= h <= 60 and v > 0.9 else "Muzzle" if 20 <= h <= 45 else "Coat",
    # Coat = the grey fur; Muzzle = the pink tail, ears and nose; Dark = outlines; Eye = the red eye.
    "rat": lambda hx, n, h, s, v: "Dark" if v < 0.15 else "Eye" if s > 0.6 and h > 340 or h < 15 and s > 0.6 else "Muzzle" if s >= 0.3 and (h >= 330 or h <= 20) else "Coat",
    # Coat = the green body; Muzzle = the brown shell; Dark = the outline.
    "snail": lambda hx, n, h, s, v: "Dark" if v < 0.16 else "Muzzle" if 8 <= h <= 36 and s >= 0.45 else "Coat",
    # Coat = the pink skin; Dark = the outline.
    "worm": lambda hx, n, h, s, v: "Dark" if v < 0.25 else "Coat",
}


def write_section(skin, regions):
    p = os.path.join(SKINS, skin, "skin.cfg")
    raw = io.open(p, encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in raw else "\n"
    t = raw.replace("\r\n", "\n")
    # every earlier section out (a run of them too: each match eats the newline the next one starts with, so loop),
    # its comment line and its lines up to the next section or the end with it
    pattern = r"\n(?:# The skin's colours by mount region[^\n]*\n)?\[Colors\][^\n]*\n(?:[^\[]*)"
    while True:
        t2 = re.sub(pattern, "\n", t)
        if t2 == t:
            break
        t = t2
    t = re.sub(r"\n{3,}", "\n\n", t)
    t = t.rstrip("\n") + "\n"
    lines = ["", "# The skin's colours by mount region (docs/mount-colours.md); every colour not named is Coat.", "[Colors]"]
    for region in ("Dark", "Muzzle", "Eye", "Harness"):
        hexes = regions.get(region, [])
        if hexes:
            lines.append("%s = %s" % (region, " ".join(hexes)))
    t += "\n".join(lines) + "\n"
    io.open(p, "w", encoding="utf-8", newline="").write(t.replace("\n", nl))


for skin in sorted(RULES):
    cols = sorted(colours(skin), key=lambda c: -c[1])
    regions = {}
    for hx, n, h, s, v in cols:
        regions.setdefault(RULES[skin](hx, n, h, s, v), []).append(hx)
    print(skin + ":", ", ".join("%s %d" % (k, len(v)) for k, v in sorted(regions.items())))
    for k in ("Dark", "Muzzle", "Eye", "Harness"):
        if k in regions and len(regions[k]) <= 12:
            print("   ", k, " ".join(regions[k]))
    write_section(skin, regions)
print("done")
