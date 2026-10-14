# Composes keeper frames from the earlier atlas extraction (BepInEx/GK2-sprites: sprites.csv + atlas PNGs) into one
# scaled sheet, so the armour's frames can be looked at and measured. Each frame is placed into its 96x96 frame box by
# its trimmed offset (offset_x, offset_y from the frame's bottom-left, Unity convention); the atlas y is Unity's
# textureRect.y (from the bottom), so the top-based pixel row is 2048 - y - h.
import csv, os, sys
from PIL import Image, ImageDraw

D = r"C:\Program Files (x86)\Steam\steamapps\common\Graveyard Keeper 2\BepInEx\GK2-sprites"
OUT = r"C:\Users\User\AppData\Local\Temp\claude\C--ClaudeCodeTemp\5184282a-9684-4b39-b483-2e43fc08430c\scratchpad"
os.makedirs(OUT, exist_ok=True)
rows = {}
with open(os.path.join(D, "sprites.csv"), encoding="utf-8") as f:
    for r in csv.DictReader(f):
        rows[r["sprite"]] = r
atlases = {}
def atlas(name):
    if name not in atlases:
        atlases[name] = Image.open(os.path.join(D, name)).convert("RGBA")
    return atlases[name]

FLIP_Y = "--topy" not in sys.argv  # default: Unity bottom-based y

def frame_image(names):
    """A 96x96 RGBA frame with the given sprites composited at their offsets (first = bottom layer)."""
    fw = fh = 96
    img = Image.new("RGBA", (fw, fh), (0, 0, 0, 0))
    for n in names:
        if n not in rows:
            print("missing", n)
            continue
        r = rows[n]
        a = atlas(r["atlas"])
        x, y, w, h = int(r["x"]), int(r["y"]), int(r["w"]), int(r["h"])
        top = a.height - y - h if FLIP_Y else y
        part = a.crop((x, top, x + w, top + h))
        ox, oy = int(r["offset_x"]), int(r["offset_y"])
        # offset_y is from the frame's bottom: paste at row fh - oy - h (top-based)
        img.alpha_composite(part, (ox, fh - oy - h))
    return img

def sheet(groups, path, scale=4, pad=6):
    cols = len(groups)
    W = cols * (96 * scale + pad) + pad
    H = 96 * scale + pad * 2 + 40
    out = Image.new("RGBA", (W, H), (40, 40, 48, 255))
    d = ImageDraw.Draw(out)
    for i, (label, names) in enumerate(groups):
        fr = frame_image(names).resize((96 * scale, 96 * scale), Image.NEAREST)
        x0 = pad + i * (96 * scale + pad)
        # the frame box and the pivot line (y = 48 from the bottom) and the ground reference rows
        d.rectangle((x0, pad, x0 + 96 * scale, pad + 96 * scale), outline=(90, 90, 100))
        py = pad + (96 - 48) * scale
        d.line((x0, py, x0 + 96 * scale, py), fill=(80, 120, 200))
        out.alpha_composite(fr, (x0, pad))
        d.text((x0 + 2, pad + 96 * scale + 4), label, fill=(230, 230, 230))
    out.save(path)
    print("wrote", path)

if __name__ == "__main__":
    want = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "all"
    if want in ("all", "static"):
        sheet([
            ("armour static L: leg+bdy+arm+hed", ["9019_leg_static_left", "9019_bdy_static_sword_left", "9019_arm_static_left", "9019_hed_static_left"]),
            ("armour leg_static_left", ["9019_leg_static_left"]),
            ("armour bdy_static_sword_left", ["9019_bdy_static_sword_left"]),
            ("armour arm_static_left", ["9019_arm_static_left"]),
            ("armour hed_static_left", ["9019_hed_static_left"]),
            ("armour walk L 01: leg+bdy", ["9019_leg_walk_left_01", "9019_bdy_walk_left_01"]),
        ], os.path.join(OUT, "frames_static.png"))
    if want in ("all", "updown"):
        sheet([
            ("armour static DOWN", ["9019_leg_static_down", "9019_bdy_static_down", "9019_arm_static_down", "9019_hed_static_down"]),
            ("armour static UP", ["9019_leg_static_up", "9019_bdy_static_up"]),
            ("atk bow down 01 (bdy)", ["9019_leg_static_down", "9019_bdy_atk_bow_down_01"]),
            ("atk bow up 01 (bdy)", ["9019_leg_static_up", "9019_bdy_atk_bow_up_01"]),
            ("atk sword down 01 (bdy)", ["9019_leg_static_down", "9019_bdy_atk_sword_down_01"]),
            ("atk sword up 01 (bdy)", ["9019_leg_static_up", "9019_bdy_atk_sword_up_01"]),
        ], os.path.join(OUT, "frames_updown.png"))
    if want in ("all", "base"):
        names = [n for n in rows if "fishing_idle_left_03" in n][:3] + [n for n in rows if n.endswith("_bdy_static_left") and not n.startswith("9019")][:3]
        print("base candidates:", names)
        sheet([(n, [n]) for n in names] or [("none", [])], os.path.join(OUT, "frames_base.png"))
    if want in ("all", "donkey"):
        names = [n for n in rows if "donkey" in n.lower()][:6]
        print("donkey candidates:", names)
        sheet([(n, [n]) for n in names] or [("none", [])], os.path.join(OUT, "frames_donkey.png"))
