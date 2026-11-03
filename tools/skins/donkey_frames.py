"""The donkey's frames the skins were drawn on, as a list of fingerprints the API checks the running game against
(docs/skins-design.md section 11). The list holds no art: a frame's key, its canvas size and a hash of its pixels.

    python tools/skins/donkey_frames.py make             # src/GK2DonkeyAPI/donkey-frames.txt from the probe's PNGs
    python tools/skins/donkey_frames.py check            # the list against the probe's PNGs (the folder changed?)
    python tools/skins/donkey_frames.py check <folder>   # the list against a game run's own read (a dev build writes
                                                         # BepInEx\\GK2-donkey-frames\\: its PNGs and its list)

The probe's PNGs are BepInEx\\GK2-asset-probe\\donkey-frames (gamedir.donkey_frames()): every 0301_ sprite of the
game's character atlases on its canvas, which every skin is measured and drawn on. The hash: FNV-1a 64 over the RGBA
bytes, rows from the top, a pixel with alpha 0 as four zeros (what is under a transparent pixel is not drawn). The API
(DonkeyFrames.cs) hashes the frames it reads back from the running game the same way. Where a frame differs, check
compares the two pictures pixel by pixel: a few pixels is the art, every picture off by a little is the read path.
Read only, except make (one file in the repo: hashes, no art).
"""
import datetime
import glob
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gamedir import REPO, donkey_frames

LIST = os.path.join(REPO, "src", "GK2DonkeyAPI", "donkey-frames.txt")


def fnv(data):
    h = 0xcbf29ce484222325
    for b in data:
        h = ((h ^ b) * 0x100000001b3) & 0xffffffffffffffff
    return h


def picture(path):
    """(width, height, the RGBA bytes from the top with transparent pixels as zeros)."""
    im = Image.open(path).convert("RGBA")
    out = bytearray(im.tobytes())   # RGBA, rows from the top
    for i in range(3, len(out), 4):
        if out[i] == 0:
            out[i - 3:i] = b"\0\0\0"
    return im.width, im.height, bytes(out)


def hashes(folder):
    """{key: (width, height, hash)} of the PNGs in a folder."""
    out = {}
    for path in sorted(glob.glob(os.path.join(folder, "*.png"))):
        w, h, px = picture(path)
        out[os.path.splitext(os.path.basename(path))[0]] = (w, h, fnv(px))
    return out


def read_list(path):
    out = {}
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        key, size, h = line.split()
        w, hh = size.split("x")
        out[key] = (int(w), int(hh), int(h, 16))
    return out


def make():
    folder = donkey_frames()
    found = hashes(folder)
    if not found:
        sys.exit(f"no PNGs in {folder}: the asset probe's Export every frame writes them (tools/GK2AssetProbe)")
    days = sorted({datetime.date.fromtimestamp(os.path.getmtime(p)).isoformat()
                   for p in glob.glob(os.path.join(folder, "*.png"))})
    lines = [
        "# The donkey's frames as the skins were drawn on them (docs/skins-design.md section 11): every 0301_ sprite",
        "# of the game's character atlases on its canvas, as the asset probe exported them (BepInEx\\GK2-asset-probe\\",
        f"# donkey-frames, {', '.join(days)}). The API compares the running game's frames with this once a session.",
        "# <key> <width>x<height> <FNV-1a 64 of the RGBA bytes, rows from the top, a transparent pixel as 0 0 0 0>",
        "# Made by tools/skins/donkey_frames.py make. No art: hashes only.",
    ]
    lines += [f"{k} {w}x{h} {v:016x}" for k, (w, h, v) in sorted(found.items())]
    with open(LIST, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(found)} frames from {folder} ({', '.join(days)}) -> {LIST}")


def check(other):
    want = read_list(LIST)
    if other is None:
        folder, have = donkey_frames(), hashes(donkey_frames())
    else:
        folder = other
        listed = os.path.join(other, "donkey-frames.txt")
        have = read_list(listed) if os.path.exists(listed) else hashes(other)
    missing = sorted(set(want) - set(have))
    new = sorted(set(have) - set(want))
    changed = sorted(k for k in set(want) & set(have) if want[k] != have[k])
    print(f"{len(want)} listed, {len(have)} in {folder}: {len(set(want) & set(have)) - len(changed)} the same, "
          f"{len(changed)} changed, {len(missing)} missing, {len(new)} new")
    for k in missing:
        print("  missing", k)
    for k in new:
        print("  new", k)
    probe = donkey_frames()
    for k in changed:
        note = f"{want[k][0]}x{want[k][1]} -> {have[k][0]}x{have[k][1]}" if want[k][:2] != have[k][:2] else ""
        a, b = os.path.join(probe, k + ".png"), os.path.join(folder, k + ".png")
        if not note and os.path.exists(a) and os.path.exists(b) and os.path.abspath(a) != os.path.abspath(b):
            _, _, pa = picture(a)
            _, _, pb = picture(b)
            px = [i // 4 for i in range(0, len(pa), 4) if pa[i:i + 4] != pb[i:i + 4]]
            worst = max((abs(pa[i] - pb[i]) for i in range(len(pa))), default=0)
            note = f"{len(px)} pixel(s) differ, at most {worst} in a channel (first at x {px[0] % want[k][0]}, " \
                   f"y {px[0] // want[k][0]} from the top)" if px else "the same pixels (the hash's rules differ?)"
        print("  changed", k, note)
    return 1 if missing or new or changed else 0


if __name__ == "__main__":
    args = sys.argv[1:]
    if args[:1] == ["make"]:
        make()
    elif args[:1] == ["check"]:
        sys.exit(check(args[1] if len(args) > 1 else None))
    else:
        sys.exit(__doc__)
