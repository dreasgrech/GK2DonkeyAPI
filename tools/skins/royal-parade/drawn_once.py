"""Royal Parade drawn once (docs/sprite-tools.md "Catching what the user caught" 4): a tools/skins/stamp.py source cut
out of this generator's own frames (its in-progress cut, out/pack), so stamp's placement can be compared with the
generator's frame by frame. The source is written into out/drawn-once/ (git-ignored):
- body.png: the standing frame's cloth (x 44 and on), its front outline cut to the cloth's top at the withers (the
  generator runs it 3 px up the collar strap's rear edge, a post on the back wherever the strap moves away);
- head.png: the standing frame's crown (x under 44);
- head_turn.png, head_front.png, head_down.png: the crown of turn 03, turn 05 and graze 04;
- head@bdy_turn_left_02.png and head@bdy_graze_left_02.png: their crowns as drawn.

    python -I tools/skins/royal-parade/drawn_once.py                    # the source, then build and stage it:
    python -I tools/skins/stamp.py build tools/skins/royal-parade/out/drawn-once
        --out tools/skins/royal-parade/out/pack-once --stage "Royal Parade (drawn once)"   # (one line)
    python -I tools/skins/royal-parade/drawn_once.py compare            # stamp's frames against the generator's

Run make_royal.py first (its out/pack is the source's source). The pictures hold none of the game's art.
"""
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "donkey"))
from contact import ORDER

GEN = os.path.join(HERE, "out", "pack")
SRC = os.path.join(HERE, "out", "drawn-once")
STAMPED = os.path.join(HERE, "out", "pack-once")
PACK_CFG = os.path.join(REPO, "packs", "GK2SaddleUpDonkeySkinPack", "gk2mounts", "donkey", "skins",
                        "saddleup-donkey-skins.royal-parade", "skin.cfg")
SPLIT = 44        # the crown lies in front of this column, the cloth behind it
WITHERS_X = 48    # rig.json's withers on the standing frame: the first column behind the collar strap


def part(key, front):
    im = Image.open(os.path.join(GEN, key + ".png")).convert("RGBA")
    out = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    px, op = im.load(), out.load()
    for y in range(96):
        for x in range(96):
            if px[x, y][3] and ((x < SPLIT) == front):
                op[x, y] = px[x, y]
    return out


def trim_front(cloth):
    """The cloth's pixels in front of the withers cut to the cloth's top there (stamp.py build names them otherwise:
    "on the collar strap")."""
    out = cloth.copy()
    op = out.load()
    top = min(y for y in range(96) if op[WITHERS_X, y][3])
    for x in range(WITHERS_X):
        for y in range(top):
            op[x, y] = (0, 0, 0, 0)
    return out


def make():
    if not os.path.isdir(GEN):
        sys.exit(f"drawn_once: no {GEN} (run make_royal.py first)")
    os.makedirs(SRC, exist_ok=True)
    shutil.copy2(PACK_CFG, os.path.join(SRC, "skin.cfg"))
    text = open(os.path.join(SRC, "skin.cfg"), encoding="utf-8", newline="").read()
    with open(os.path.join(SRC, "skin.cfg"), "w", encoding="utf-8", newline="") as f:
        f.write(text.replace("name = Royal Parade", "name = Royal Parade (drawn once)", 1))
    trim_front(part("bdy_static_left", False)).save(os.path.join(SRC, "body.png"))
    for name, key in (("head", "bdy_static_left"), ("head_turn", "bdy_turn_left_03"),
                      ("head_front", "bdy_turn_left_05"),
                      ("head_down", "bdy_graze_left_04"), ("head@bdy_turn_left_02", "bdy_turn_left_02"),
                      ("head@bdy_graze_left_02", "bdy_graze_left_02")):
        part(key, True).save(os.path.join(SRC, name + ".png"))
    print("source:", SRC)


def compare():
    rows = []
    for k in ORDER:
        a = Image.open(os.path.join(GEN, k + ".png")).convert("RGBA").load()
        b = Image.open(os.path.join(STAMPED, k + ".png")).convert("RGBA").load()
        same = lambda x, y: (a[x, y] if a[x, y][3] else 0) == (b[x, y] if b[x, y][3] else 0)
        cloth = sum(1 for y in range(96) for x in range(SPLIT, 96) if not same(x, y))
        crown = sum(1 for y in range(96) for x in range(0, SPLIT) if not same(x, y))
        rows.append((k, cloth, crown))
    for k, c1, c2 in rows:
        if c1 or c2:
            print(f"   {k:22s} cloth {c1:3d} px, crown {c2:3d} px")
    print(f"frames the same: {sum(1 for _, a, b in rows if not a and not b)} of {len(rows)}; cloth differs in "
          f"{sum(1 for _, a, _ in rows if a)}, crown in {sum(1 for _, _, b in rows if b)}")


if __name__ == "__main__":
    compare() if sys.argv[1:] == ["compare"] else make()
