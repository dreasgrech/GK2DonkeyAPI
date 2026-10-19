# The donkey's frames for the Royal Parade generator, and a contact sheet of them.
import os, sys
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from gamedir import donkey_frames
# The game's own placement of every frame (the feet on one ground row): the asset probe's donkey-frames. Everything
# drawn for a pack is measured and drawn on this canvas (docs/sprite-tools.md "The canvas, found wrong": the old
# top-left-aligned .raw frames were 2 to 17 px off per frame; they and their helpers are gone since 2026-10-19 23:30).
GAME = donkey_frames()
HERE = os.path.dirname(os.path.abspath(__file__))
ORDER = (["bdy_static_left"] + [f"bdy_walk_left_{i:02d}" for i in range(1, 9)] + [f"bdy_ear_left_{i:02d}" for i in range(1, 6)]
         + [f"bdy_graze_left_{i:02d}" for i in range(1, 6)] + [f"bdy_chew_left_{i:02d}" for i in range(1, 4)]
         + [f"bdy_lips_left_{i:02d}" for i in range(1, 14)] + [f"bdy_turn_left_{i:02d}" for i in range(1, 7)]
         + ["hed_static_down", "hed_static_down_02", "hed_static_down_03"])
def load(k):
    """The donkey's frame k on the game's canvas (the probe's PNG)."""
    return Image.open(os.path.join(GAME, k + ".png")).convert("RGBA")
if __name__ == "__main__":
    S = 4; C = 8
    sheet = Image.new("RGBA", (C * 96 * S, 6 * 96 * S), (46, 46, 50, 255)); d = ImageDraw.Draw(sheet)
    for n, k in enumerate(ORDER):
        im = load(k).resize((96 * S, 96 * S), Image.NEAREST); x, y = (n % C) * 96 * S, (n // C) * 96 * S
        sheet.alpha_composite(im, (x, y)); d.text((x + 4, y + 4), k.replace("_left", ""), fill=(255, 255, 0, 255))
        for r in (41, 64): d.line([(x, y + r * S), (x + 96 * S, y + r * S)], fill=(0, 160, 255, 90))
    sheet.save(os.path.join(HERE, "out", "donkey_contact.png"))
    print(load(ORDER[0]).getbbox())
