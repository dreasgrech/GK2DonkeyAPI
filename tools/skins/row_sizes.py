"""How large each colour row's part looks on a skin, to order its rows (the user, 2026-11-08: the colours window's rows
"in descending order of the largest, most visible part", the donkey's own rows first, then the skin's parts by size).

    python -I tools/skins/row_sizes.py <skin folder>

Reads the folder's skin.cfg: each [Tone.*] section's colours and each [Colors] <region> list. A part's size is its
largest single piece: the biggest connected patch of its colours (touching sideways or up and down) in a frame,
averaged over the skin's frame pictures (bdy_*.png; the front\\ copies left out, they repeat the main pictures). Not
its pixels added up: the Jester's eight bells came to more pixels than a half of its cap, yet each is a speck ("the
bells should be after the cap since they are smaller on screen. thats how you need to think, not just pixels per
frame blindly", the user). The total and the number of pieces are printed beside it.

The tone rows follow the sections' order in skin.cfg (SkinTones.Add), after the donkey's rows; a [Colors] region's
colours are listed for their size only: its row is the donkey's, in the donkey's place.
"""
import os
import re
import sys

from PIL import Image


def hexes(value):
    return {tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in value.split() if re.fullmatch(r"[0-9a-fA-F]{6}", h)}


def pieces(cells):
    """The sizes of the connected pieces (4-neighbours) of a set of pixels."""
    left, sizes = set(cells), []
    while left:
        stack, n = [left.pop()], 0
        while stack:
            x, y = stack.pop()
            n += 1
            for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if q in left:
                    left.remove(q)
                    stack.append(q)
        sizes.append(n)
    return sizes


def main(argv):
    if len(argv) != 1 or not os.path.isfile(os.path.join(argv[0], "skin.cfg")):
        print(__doc__)
        return 2
    d = argv[0]
    rows, tones, section = {}, [], None
    for line in open(os.path.join(d, "skin.cfg"), encoding="utf-8").read().splitlines():
        s = line.split("#")[0].strip()
        m = re.fullmatch(r"\[(.+)\]", s)
        if m:
            section = m.group(1)
            continue
        if not section or "=" not in s:
            continue
        key, value = (t.strip() for t in s.split("=", 1))
        if section.startswith("Tone.") and key.lower() == "colours":
            rows[section] = hexes(value)
            tones.append(section)
        elif section == "Colors":
            rows["Colors " + key] = hexes(value)
    frames = sorted(f for f in os.listdir(d) if f.startswith("bdy_") and f.endswith(".png"))
    if not frames:
        print(f"row_sizes: no bdy_*.png in {d} (a sheet skin: give its exported frames)")
        return 1
    # a part's colours are its own though [Colors] lists them too (the listing an older API reads: the API moves
    # them from the region to the part, SkinTones.Add): the parts first
    owner = {}
    for k in sorted(rows, key=lambda k: not k.startswith("Tone.")):
        for c in rows[k]:
            owner.setdefault(c, k)
    biggest = {k: 0 for k in rows}
    total = {k: 0 for k in rows}
    count = {k: 0 for k in rows}
    for f in frames:
        im = Image.open(os.path.join(d, f)).convert("RGBA")
        w = im.width
        raw = im.tobytes()
        cells = {k: set() for k in rows}
        for i in range(0, len(raw), 4):
            if raw[i + 3]:
                k = owner.get((raw[i], raw[i + 1], raw[i + 2]))
                if k:
                    cells[k].add(((i // 4) % w, (i // 4) // w))
        for k, c in cells.items():
            sizes = pieces(c)
            biggest[k] += max(sizes, default=0)
            total[k] += len(c)
            count[k] += len(sizes)
    n = len(frames)
    print(f"{n} frames; rows by their largest piece (pixels, average a frame; then all its pixels and its pieces):")
    for k in sorted(rows, key=lambda k: -biggest[k]):
        where = f"[Tone] #{tones.index(k) + 1} in skin.cfg" if k in tones else "the donkey's row"
        print(f"  {k:30s} largest {biggest[k] / n:6.1f}   all {total[k] / n:6.1f} in {count[k] / n:4.1f} piece(s)   {where}")
    wanted = sorted(tones, key=lambda k: -biggest[k])
    print("tone sections, largest piece first: " + ", ".join(t[5:] for t in wanted)
          + ("   (the file's order already)" if wanted == tones else "   (reorder skin.cfg to this)"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
