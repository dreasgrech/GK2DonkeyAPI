# The donkey's frames for the Royal Parade generator, and a contact sheet of them.
import os, struct, sys
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from gamedir import donkey_frames
# The game's own placement of every frame (the feet on one ground row): the asset probe's donkey-frames. Everything
# drawn for a pack is measured and drawn on this canvas. The old .raw frames (C:\ClaudeCodeTemp\GK2-aseprite\
# harness_frames, 2026-10-19) put every frame's top-left corner at (25, 21), 2 to 17 px off per frame, and are kept
# only to move things measured on them (the Hellfire horn bases) onto the game's canvas.
GAME = donkey_frames()
IO = r"C:\ClaudeCodeTemp\GK2-aseprite\harness_frames"
HERE = os.path.dirname(os.path.abspath(__file__))
ORDER = (["bdy_static_left"] + [f"bdy_walk_left_{i:02d}" for i in range(1, 9)] + [f"bdy_ear_left_{i:02d}" for i in range(1, 6)]
         + [f"bdy_graze_left_{i:02d}" for i in range(1, 6)] + [f"bdy_chew_left_{i:02d}" for i in range(1, 4)]
         + [f"bdy_lips_left_{i:02d}" for i in range(1, 14)] + [f"bdy_turn_left_{i:02d}" for i in range(1, 7)]
         + ["hed_static_down", "hed_static_down_02", "hed_static_down_03"])
def load(k):
    """The donkey's frame k on the game's canvas (the probe's PNG)."""
    return Image.open(os.path.join(GAME, k + ".png")).convert("RGBA")
def load_raw(k):
    """The old .raw frame (top-left aligned): only to map things measured on it onto the game's canvas."""
    b = open(os.path.join(IO, k + ".raw"), "rb").read(); W, H = struct.unpack("<ii", b[:8])
    rows = [b[8 + 4 * W * y: 8 + 4 * W * (y + 1)] for y in range(H)]
    return Image.frombytes("RGBA", (W, H), b"".join(reversed(rows)))
def raw_to_game_shift(k):
    """(dx, dy) that moves a point measured on the .raw frame k onto the game's canvas."""
    a, b = load_raw(k).getbbox(), load(k).getbbox()
    return (b[0] - a[0], b[1] - a[1])
if __name__ == "__main__":
    S = 4; C = 8
    sheet = Image.new("RGBA", (C * 96 * S, 6 * 96 * S), (46, 46, 50, 255)); d = ImageDraw.Draw(sheet)
    for n, k in enumerate(ORDER):
        im = load(k).resize((96 * S, 96 * S), Image.NEAREST); x, y = (n % C) * 96 * S, (n // C) * 96 * S
        sheet.alpha_composite(im, (x, y)); d.text((x + 4, y + 4), k.replace("_left", ""), fill=(255, 255, 0, 255))
        for r in (41, 64): d.line([(x, y + r * S), (x + 96 * S, y + r * S)], fill=(0, 160, 255, 90))
    sheet.save(os.path.join(HERE, "out", "donkey_contact.png"))
    print(load(ORDER[0]).getbbox())
