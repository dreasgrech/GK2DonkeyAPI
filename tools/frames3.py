# The knight facing up (back view) on the side-on donkey, three ways, from the atlas: as built now (donkey behind,
# legs and torso drawn whole); the donkey drawn in front for this facing (everything below the back line covered);
# legs hidden and the torso cut at the back line (donkey behind). Seat lift 7 px (0.22 world), rider 6 px to the rear.
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
from frames import rows, atlas, FLIP_Y, OUT
from frames2 import part
from PIL import Image, ImageDraw

LIFT, REAR = 7, 6
BACK_LINE = 55  # the donkey's back line in frame rows (feet 28 + 2 + 25)

def place(img, n, dx=0, dy=0, cut_below=None):
    p, ox, oy = part(n)
    if cut_below is not None:
        # keep only the rows at or above frame row cut_below (after the lift): the sprite's bottom row is at oy + dy
        keep_from = cut_below - (oy + dy)  # rows to drop from the bottom
        if keep_from > 0:
            if keep_from >= p.height:
                return
            p = p.crop((0, 0, p.width, p.height - keep_from))
            oy += keep_from
    img.alpha_composite(p, (ox + dx, 96 - oy - p.height - dy))

def scene(label, rider, donkey_in_front=False, hide_legs=False, cut=False):
    img = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    def rider_layers():
        for n in rider:
            if hide_legs and "_leg_" in n:
                continue
            place(img, n, REAR, LIFT, cut_below=BACK_LINE if cut and ("_bdy_" in n or "_arm_" in n) else None)
    if donkey_in_front:
        rider_layers()
        place(img, "0301_bdy_static_left")
    else:
        place(img, "0301_bdy_static_left")
        rider_layers()
    return label, img

UP_STATIC = ["9019_leg_static_up", "9019_bdy_static_sword_up", "9019_arm_static_sword_up", "9019_hed_static_up"]
UP_BOW = ["9019_leg_atk_bow_up", "9019_bdy_atk_hero_bow_up_03", "9019_arm_atk_hero_bow_up_03", "9019_hed_static_up"]
UP_SWORD = ["9019_leg_static_up", "9019_bdy_atk_sword_up_03", "9019_arm_atk_sword_up_03", "9019_hed_static_up"]
for n in UP_STATIC + UP_BOW + UP_SWORD:
    if n not in rows:
        print("missing", n)

scenes = [
    scene("UP as built: donkey behind, whole rider", UP_STATIC),
    scene("UP: donkey drawn IN FRONT", UP_STATIC, donkey_in_front=True),
    scene("UP: legs hidden, torso cut at back line", UP_STATIC, hide_legs=True, cut=True),
    scene("UP bow shot: donkey in front", UP_BOW, donkey_in_front=True),
    scene("UP bow shot: legs hidden + cut", UP_BOW, hide_legs=True, cut=True),
    scene("UP sword: legs hidden + cut", UP_SWORD, hide_legs=True, cut=True),
]
scale, pad = 5, 8
W = len(scenes) * (96 * scale + pad) + pad
H = 96 * scale + pad * 2 + 36
out = Image.new("RGBA", (W, H), (40, 40, 48, 255))
d = ImageDraw.Draw(out)
for i, (label, img) in enumerate(scenes):
    x0 = pad + i * (96 * scale + pad)
    d.rectangle((x0, pad, x0 + 96 * scale, pad + 96 * scale), outline=(90, 90, 100))
    back = pad + (96 - BACK_LINE) * scale
    d.line((x0, back, x0 + 96 * scale, back), fill=(200, 120, 80))
    out.alpha_composite(img.resize((96 * scale, 96 * scale), Image.NEAREST), (x0, pad))
    d.text((x0 + 2, pad + 96 * scale + 4), label, fill=(230, 230, 230))
path = os.path.join(OUT, "rider_up_options.png")
out.save(path)
print("wrote", path)
