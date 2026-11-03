"""The consistency check of a skin drawn over the donkey (docs/sprite-tools.md "Catching what the user caught"): what
the skin itself makes change between frames where the donkey doesn't, over all 44 frames, so a piece that moves,
grows or comes and goes is listed before anyone looks. Two checks:

  1. The body stays put (not in the walk: the body bobs, no two walk frames share one, and a piece that moves with
     it reads as a change; --walk takes them in). For each frame, the frame before it in ORDER (the standing frame
     first) whose donkey body
     (x >= --body-x, the part behind the neck) is most like this one's is its reference; on every pixel there where
     the plain donkey is the same and opaque in both frames, the worn skin (the erase colour cleared, the picture on
     top) must be the same too. Each pixel that is not is the skin's own doing: listed per frame ("body: N px made by
     the skin against <reference>"). A piece meant to move with something else (a tail that sways in the walk) shows
     up here too; such lines are read, not fixed blindly.
  2. Nothing lives for one frame. A piece of the skin's own pixels (4-connected, at least 3) with no pixel of the
     skin within 2 of it in either neighbour frame of its clip (the walk loops) is listed ("appears in this frame
     only").

    python -I tools/skins/consistency.py <skin folder> [--body-x 52] [--walk] [--maps <folder outside the repo>]

The skin folder holds skin.cfg and a PNG per frame (a pack's, or a generator's out\\pack). --maps writes a map per
flagged frame (the game's art under it, so never into the repo: it refuses). Exit code 1 when anything is listed.
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "donkey"))
from gamedir import donkey_frames
from contact import ORDER

REPO = os.path.dirname(os.path.dirname(HERE))
CLEAR = (0, 0, 0, 0)


def erase_colour(folder):
    cfg = open(os.path.join(folder, "skin.cfg"), encoding="utf-8").read()
    over = any(l.split("=", 1)[1].strip().lower() == "over" for l in cfg.splitlines() if l.strip().startswith("layer") and "=" in l)
    if not over:
        sys.exit("consistency: not a skin drawn over the donkey (layer = over)")
    for l in cfg.splitlines():
        s = l.split("#", 1)[0].strip()
        if s.startswith("erase") and "=" in s:
            v = s.split("=", 1)[1].strip()
            return None if v.lower() == "none" else tuple(int(v[i:i + 2], 16) for i in (0, 2, 4))
    return (255, 0, 255)


def frames(folder, erase):
    """{key: (plain, worn, skin)}: each a dict (x, y) -> RGBA, transparent pixels as one value."""
    out = {}
    for k in ORDER:
        if k.startswith("hed_"):
            continue
        d = Image.open(os.path.join(donkey_frames(), k + ".png")).convert("RGBA")
        path = os.path.join(folder, k + ".png")
        s = Image.open(path).convert("RGBA") if os.path.exists(path) else Image.new("RGBA", d.size, CLEAR)
        dp, sp = d.load(), s.load()
        w, h = d.size
        plain, worn, skin = {}, {}, {}
        for y in range(h):
            for x in range(w):
                p = dp[x, y] if dp[x, y][3] else CLEAR
                plain[x, y] = p
                q = sp[x, y]
                if q[3] and erase is not None and q[:3] == erase:
                    worn[x, y] = CLEAR
                elif q[3]:
                    worn[x, y] = q
                    skin[x, y] = q
                else:
                    worn[x, y] = p
        out[k] = (plain, worn, skin)
    return out


def clip_of(k):
    return k.rsplit("_", 1)[0] if k[-2:].isdigit() else k


def body_check(fr, body_x, walk=False):
    keys = [k for k in ORDER if k in fr]
    lines, flagged = [], {}
    for i, k in enumerate(keys):
        if i == 0 or ("_walk_" in k and not walk):
            continue
        pk = fr[k][0]
        region = [p for p in pk if p[0] >= body_x]

        def same(r):
            pr = fr[r][0]
            return sum(1 for p in region if pk[p][3] and pk[p] == pr[p])
        ref = max(keys[:i], key=lambda r: (same(r), r == keys[0]))
        pr, wr = fr[ref][0], fr[ref][1]
        wk = fr[k][1]
        made = [p for p in region if pk[p][3] and pk[p] == pr[p] and wk[p] != wr[p]]
        if made:
            xs, ys = [p[0] for p in made], [p[1] for p in made]
            lines.append(f"{k}: body: {len(made)} px made by the skin against {ref} (x{min(xs)}-{max(xs)} y{min(ys)}-{max(ys)})")
            flagged.setdefault(k, set()).update(made)
    return lines, flagged


def pieces(px):
    left, out = set(px), []
    while left:
        part, todo = set(), [left.pop()]
        while todo:
            x, y = todo.pop()
            part.add((x, y))
            for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if q in left:
                    left.discard(q)
                    todo.append(q)
        out.append(part)
    return out


def one_frame_check(fr):
    clips = {}
    for k in ORDER:
        if k in fr:
            clips.setdefault(clip_of(k), []).append(k)
    lines, flagged = [], {}
    for c, ks in clips.items():
        if len(ks) < 2:
            continue
        loop = "_walk_" in c
        for i, k in enumerate(ks):
            nb = []
            if i > 0 or loop:
                nb.append(ks[i - 1])
            if i < len(ks) - 1 or loop:
                nb.append(ks[(i + 1) % len(ks)])
            near = [fr[n][2] for n in nb]
            for part in pieces(fr[k][2]):
                if len(part) < 3:
                    continue
                alone = all(not any((x + dx, y + dy) in s for (x, y) in part for dx in range(-2, 3) for dy in range(-2, 3)) for s in near)
                if alone:
                    xs, ys = [p[0] for p in part], [p[1] for p in part]
                    lines.append(f"{k}: a piece of {len(part)} px appears in this frame only (x{min(xs)}-{max(xs)} y{min(ys)}-{max(ys)})")
                    flagged.setdefault(k, set()).update(part)
    return lines, flagged


def write_maps(fr, flagged, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for k, pts in flagged.items():
        worn = fr[k][1]
        im = Image.new("RGBA", (96, 96), (70, 78, 64, 255))
        px = im.load()
        for p, c in worn.items():
            if c[3]:
                px[p] = c
        im = im.resize((96 * 6, 96 * 6), Image.NEAREST)
        d = ImageDraw.Draw(im)
        for x, y in pts:
            d.rectangle((x * 6, y * 6, x * 6 + 5, y * 6 + 5), outline=(0, 255, 255, 255), width=2)
        im.save(os.path.join(out_dir, k + ".png"))


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    if not args:
        print(__doc__)
        return 2
    folder = args[0]
    body_x = int(argv[argv.index("--body-x") + 1]) if "--body-x" in argv else 52
    maps = argv[argv.index("--maps") + 1] if "--maps" in argv else None
    if maps and os.path.abspath(maps).lower().startswith(os.path.abspath(REPO).lower()):
        sys.exit("consistency: the maps hold the game's art; write them outside the repository")
    fr = frames(folder, erase_colour(folder))
    b_lines, b_flag = body_check(fr, body_x, "--walk" in argv)
    o_lines, o_flag = one_frame_check(fr)
    for l in b_lines + o_lines:
        print(l)
    print(f"{os.path.basename(os.path.normpath(folder))}: {len(b_lines)} frame(s) with body pixels the skin changes, "
          f"{len(o_lines)} piece(s) in one frame only")
    if maps:
        merged = {}
        for f in (b_flag, o_flag):
            for k, v in f.items():
                merged.setdefault(k, set()).update(v)
        write_maps(fr, merged, maps)
        print("maps:", maps)
    return 1 if b_lines or o_lines else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
