# The crown's fallback anchors: the Hellfire horn bases per frame, measured from the Hellfire pack itself
# (packs/SaddleUpDonkeySkins/gk2mounts/donkey/skins/saddleup-donkey-skins.hellfire/<key>.png, bone-coloured pixels on the game's canvas). Each horn is one
# 8-connected cluster of bone pixels; its base is the cluster's lowest row, the x the base's edge on the side the horn
# leans to (the tip's columns against the base's: 1 = leans right, -1 = left, the turn frames facing the camera).
# Writes horn_bases.json as [[x, y, lean], [x, y, lean]] per frame (left horn first), the same file the first
# version produced from the v14 positions on the old .raw canvas (2026-10-19 23:30: 44 of 44 frames identical).
#   python tools/skins/royal-parade/horn_bases.py          # rewrites horn_bases.json
#   python tools/skins/royal-parade/horn_bases.py --check  # compares with the file, changes nothing
import json, os, sys
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from contact import ORDER

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
PACK = os.path.join(REPO, "packs", "SaddleUpDonkeySkins", "gk2mounts", "donkey", "skins", "saddleup-donkey-skins.hellfire")
BONE = {(0xEC, 0xDC, 0xB4), (0xC8, 0xB0, 0x82), (0x78, 0x5A, 0x3C)}  # the horns' light, mid and shadow, as hellfire_proto.BONE


def clusters(px, w, h):
    seen, out = set(), []
    for y in range(h):
        for x in range(w):
            if (x, y) in seen or not px[x, y][3] or px[x, y][:3] not in BONE:
                continue
            todo, cl = [(x, y)], []
            seen.add((x, y))
            while todo:
                cx, cy = todo.pop()
                cl.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and px[nx, ny][3] and px[nx, ny][:3] in BONE:
                            seen.add((nx, ny)); todo.append((nx, ny))
            out.append(cl)
    return out


def bases(key):
    im = Image.open(os.path.join(PACK, key + ".png")).convert("RGBA")
    px = im.load()
    cl = sorted(clusters(px, im.width, im.height), key=len, reverse=True)[:2]
    assert len(cl) == 2, (key, len(cl))
    out = []
    for c in sorted(cl, key=lambda c: min(x for x, _ in c)):
        top, bot = min(y for _, y in c), max(y for _, y in c)
        tip = [x for x, y in c if y == top]
        base = [x for x, y in c if y == bot]
        lean = 1 if sum(tip) / len(tip) >= sum(base) / len(base) else -1
        out.append([max(base) if lean > 0 else min(base), bot, lean])
    return out


if __name__ == "__main__":
    out = {k: bases(k) for k in ORDER}
    path = os.path.join(HERE, "horn_bases.json")
    if "--check" in sys.argv:
        old = json.load(open(path))
        bad = [k for k in ORDER if old.get(k) != out[k]]
        for k in bad:
            print(f"{k:22s} file {old.get(k)} measured {out[k]}")
        print(f"{len(ORDER) - len(bad)} of {len(ORDER)} frames as in the file")
        sys.exit(1 if bad else 0)
    json.dump(out, open(path, "w"))
    for k, v in out.items():
        print(f"{k:22s} {v}")
    print("wrote", path)
