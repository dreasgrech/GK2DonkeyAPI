# Checks an exported skin pack (sheet.png + skin.cfg) before it is looked at in game. Says what is wrong in plain
# words; never changes anything. Usage: python check_skin.py <pack folder>
# Run by "GK2 Export skin" after each export (the "check =" line of the .gk2export.txt next to the .aseprite file).
import os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gamedir import donkey_frames
FRAMES = donkey_frames()  # the donkey's 44 frames as the game places them (docs/sprite-tools.md)
ORDER = (["bdy_static_left"] + [f"bdy_walk_left_{i:02d}" for i in range(1, 9)] + [f"bdy_ear_left_{i:02d}" for i in range(1, 6)]
         + [f"bdy_graze_left_{i:02d}" for i in range(1, 6)] + [f"bdy_chew_left_{i:02d}" for i in range(1, 4)]
         + [f"bdy_lips_left_{i:02d}" for i in range(1, 14)] + [f"bdy_turn_left_{i:02d}" for i in range(1, 7)]
         + ["hed_static_down", "hed_static_down_02", "hed_static_down_03"])


def donkey(k):
    return Image.open(os.path.join(FRAMES, k + ".png")).convert("RGBA")


def cfg(path):
    sec, out = "", {}
    for line in open(path, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if line.startswith("["):
            sec = line.strip("[]")
        elif "=" in line:
            k, v = (x.strip() for x in line.split("=", 1))
            out[(sec, k.lower())] = v
    return out


def main(pack):
    warn, info = [], []
    c = cfg(os.path.join(pack, "skin.cfg"))
    cw, ch = (int(v) for v in c.get(("Skin", "canvas"), "96 96").split())
    over = c.get(("Skin", "layer"), "replace").lower() == "over"
    sheet = Image.open(os.path.join(pack, c.get(("Skin", "sheet"), "sheet.png"))).convert("RGBA")
    cols = int(c.get(("Skin", "columns"), str(sheet.width // cw)))
    if sheet.width % cw or sheet.height % ch:
        warn.append(f"the sheet is {sheet.width}x{sheet.height}, not whole {cw}x{ch} cells")
    cells = [sheet.crop(((i % cols) * cw, (i // cols) * ch, (i % cols) * cw + cw, (i // cols) * ch + ch)) for i in range(len(ORDER))]

    named = {}
    for (sec, k), v in c.items():
        if sec == "Colors":
            for hx in v.split():
                named[hx.upper()] = k.capitalize()
    donkey_cols = set()
    dk = {k: donkey(k) for k in ORDER}
    for im in dk.values():
        b = im.tobytes()
        donkey_cols |= {tuple(b[i:i + 3]) for i in range(0, len(b), 4) if b[i + 3] > 0}

    used, partial, empty, strays = {}, 0, [], []
    for k, im in zip(ORDER, cells):
        px = im.load(); n = 0
        for y in range(ch):
            for x in range(cw):
                p = px[x, y]
                if p[3] == 0:
                    continue
                n += 1
                if p[3] != 255:
                    partial += 1
                used.setdefault(p[:3], set()).add(k)
                if not any(0 <= x + dx < cw and 0 <= y + dy < ch and px[x + dx, y + dy][3] > 0
                           for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
                    strays.append(f"{k} ({x},{y})")
        if n == 0:
            empty.append(k)

    hexes = {f"{r:02X}{g:02X}{b:02X}": (r, g, b) for (r, g, b) in used}
    clash = [h for h, rgb in hexes.items() if rgb in donkey_cols]
    unnamed = [h for h in hexes if h not in named]
    missing = [h for h in named if h not in hexes]
    if clash:
        warn.append(f"{len(clash)} colour(s) the donkey itself has: {', '.join(sorted(clash))}"
                    + (" (on a skin over the donkey they take the donkey's colour settings; draw them a shade apart)" if over else ""))
    if missing:
        warn.append(f"[Colors] names {len(missing)} colour(s) no picture has: {', '.join(sorted(missing))}")
    if partial:
        warn.append(f"{partial} half-transparent pixel(s): pixel art should be fully on or off")
    if strays:
        warn.append(f"{len(strays)} lone pixel(s) (nothing around them): {', '.join(strays[:8])}{' ...' if len(strays) > 8 else ''}")
    total = len(donkey_cols) + len([h for h in named if h in hexes]) if over else len(hexes)
    if total > 48:
        warn.append(f"{total} colours to swap: over 48, so the colour cube instead of the exact swap")

    # frames where the donkey is drawn the same but the skin differs: likely one of them was missed
    groups = {}
    for k in ORDER:
        groups.setdefault(dk[k].tobytes(), []).append(k)
    for ks in groups.values():
        if len(ks) > 1:
            first = cells[ORDER.index(ks[0])].tobytes()
            odd = [k for k in ks[1:] if cells[ORDER.index(k)].tobytes() != first]
            if odd:
                info.append(f"same donkey picture as {ks[0]} but a different skin: {', '.join(odd)}")

    info.insert(0, f"{len(ORDER) - len(empty)} of {len(ORDER)} frames drawn"
                + (f" (empty: {', '.join(empty)})" if empty else ""))
    info.insert(1, f"{len(hexes)} colours: " + ", ".join(f"{h}={named.get(h, 'kept' if over else 'Coat')}" for h in sorted(hexes)))
    print("CHECK: " + ("all good" if not warn else f"{len(warn)} thing(s) to look at"))
    for w in warn:
        print("  ! " + w)
    for i in info:
        print("  - " + i)


if __name__ == "__main__":
    main(sys.argv[1])
