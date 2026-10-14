# The armoured rider on the donkey, to scale, from the atlas extraction: the donkey's static left frame with the knight
# lifted by the Ride seat (SeatOffset 0.22 world = 0.22 / 0.0333 = 6.6 art px) and set RiderOffsetPx (-6, towards the
# rear = +x for a left-facing donkey) and TorsoOffsetPx (-3, torso only, not drawn separately here). Horizontal
# placement is approximate (the keeper is centred over the donkey's body by CenterOffset in game). Three riders: the
# knight side-on (static), facing the camera (static down), facing away (static up), and the base fishing-idle rider
# for comparison.
import csv, os, sys
sys.path.insert(0, os.path.dirname(__file__))
from frames import rows, atlas, FLIP_Y, OUT
from PIL import Image, ImageDraw

def part(n):
    r = rows[n]
    a = atlas(r["atlas"])
    x, y, w, h = int(r["x"]), int(r["y"]), int(r["w"]), int(r["h"])
    top = a.height - y - h if FLIP_Y else y
    return a.crop((x, top, x + w, top + h)), int(r["offset_x"]), int(r["offset_y"])

def paste(img, n, dx=0, dy=0, fw=96, fh=96, mirror=False):
    p, ox, oy = part(n)
    if mirror:
        p = p.transpose(Image.FLIP_LEFT_RIGHT)
        ox = fw - ox - p.width
    img.alpha_composite(p, (ox + dx, fh - oy - p.height - dy))

def scene(rider_parts, label, lift_px=7, rear_px=6, mirror_rider=False):
    img = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    paste(img, "0301_bdy_static_left")  # the donkey, facing left; its back line is at offset 28 + 2 + 25 = 55 px
    for n in rider_parts:
        paste(img, n, dx=rear_px, dy=lift_px, mirror=mirror_rider)
    return label, img

def main():
    scenes = [
        scene(["9019_leg_static_left", "9019_bdy_static_sword_left", "9019_arm_static_left", "9019_hed_static_left"], "knight side-on (static L), seat 7px, rear 6px"),
        scene(["9019_leg_static_down", "9019_bdy_static_down", "9019_arm_static_down", "9019_hed_static_down"], "knight facing camera (static DOWN) on side donkey"),
        scene(["9019_leg_static_up", "9019_bdy_static_up"], "knight facing away (static UP) on side donkey"),
        scene(["9019_leg_static_down", "9019_bdy_atk_bow_down_01"], "knight: bow attack DOWN frame 01"),
        scene(["9005_bdy_fishing_idle_left_03", "9001_arm_fishing_idle_left_03"], "today: fishing-idle rider (base skin)"),
    ]
    scale, pad = 5, 8
    W = len(scenes) * (96 * scale + pad) + pad
    H = 96 * scale + pad * 2 + 36
    out = Image.new("RGBA", (W, H), (40, 40, 48, 255))
    d = ImageDraw.Draw(out)
    for i, (label, img) in enumerate(scenes):
        x0 = pad + i * (96 * scale + pad)
        d.rectangle((x0, pad, x0 + 96 * scale, pad + 96 * scale), outline=(90, 90, 100))
        back = pad + (96 - 55) * scale
        d.line((x0, back, x0 + 96 * scale, back), fill=(200, 120, 80))  # the donkey's back line (55 px)
        ground = pad + (96 - 28) * scale
        d.line((x0, ground, x0 + 96 * scale, ground), fill=(80, 160, 80))  # the donkey's feet (28 px)
        out.alpha_composite(img.resize((96 * scale, 96 * scale), Image.NEAREST), (x0, pad))
        d.text((x0 + 2, pad + 96 * scale + 4), label, fill=(230, 230, 230))
    path = os.path.join(OUT, "rider_on_donkey.png")
    out.save(path)
    print("wrote", path)

main()
