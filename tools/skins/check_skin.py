# Checks a skin pack (skin.cfg + a PNG per frame, or one sheet.png of cells) before it is looked at in game. Says
# what is wrong in plain words; never changes anything. Usage: python check_skin.py <pack folder>
# Run by "GK2 Export skin" after each export (the "check =" line of the .gk2export.txt next to the .aseprite file).
# A skin of a mount other than the donkey that isn't its "default" is built on that default (docs/skins.md "Variants:
# built on the default"): it is read against ../default (its canvas, the pictures it doesn't draw, its [Colors]), and
# one with layer = over is checked over the default's pictures instead of the donkey's.
import re
import os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gamedir import donkey_frames
FRAMES = donkey_frames()  # the donkey's 44 frames as the game places them (docs/sprite-tools.md); read only for an over skin
RESULT = {"errors": 0}
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
            sec = ".".join(p.capitalize() for p in line.strip("[]").strip().split("."))  # the loader reads [skin] as [Skin]
        elif "=" in line:
            k, v = (x.strip() for x in line.split("=", 1))
            out[(sec, k.lower())] = v
    return out


def default_of(pack):
    # the default a variant is built on: gk2mounts/<mount>/skins/default/ beside it, the mount folder with its
    # mount.cfg (not the donkey's); None for a default, a donkey skin, or a folder of another shape
    pack = os.path.abspath(pack)
    skins_dir = os.path.dirname(pack)
    mount_dir = os.path.dirname(skins_dir)
    if os.path.basename(skins_dir).lower() != "skins" or os.path.basename(pack).lower() == "default" or os.path.basename(mount_dir).lower() == "donkey":
        return None
    if not os.path.exists(os.path.join(mount_dir, "mount.cfg")):
        return None
    d = os.path.join(skins_dir, "default")
    return d if os.path.exists(os.path.join(d, "skin.cfg")) else None


def main(pack):
    warn, info = [], []
    c = cfg(os.path.join(pack, "skin.cfg"))
    parent = default_of(pack)
    pc = cfg(os.path.join(parent, "skin.cfg")) if parent else {}
    cw, ch = (int(v) for v in c.get(("Skin", "canvas"), pc.get(("Skin", "canvas"), "96 96")).split())  # a variant without its own: the default's

    def parent_picture(k):
        # the default's picture for a key (a PNG per frame; a sheet or game default gives none here)
        p = os.path.join(parent, k + ".png") if parent else None
        return Image.open(p).convert("RGBA") if p and os.path.exists(p) else None
    over = c.get(("Skin", "layer"), "replace").lower() == "over"
    front_keys = []  # the keys of the front/ pictures appended to `cells`, in their order
    # the layout every built-in keeps (tools/skins/canonical_cfg.py): said here, refused by the release scan
    try:
        import canonical_cfg
        if canonical_cfg.canonical(os.path.join(pack, "skin.cfg")) != open(os.path.join(pack, "skin.cfg"), encoding="utf-8", newline="").read():
            warn.append("skin.cfg is not in the canonical layout (python -I tools/skins/canonical_cfg.py rewrites a built-in)")
    except (Exception, SystemExit) as e:  # canonical_cfg refuses an unknown key or section with SystemExit
        warn.append("skin.cfg could not be laid out: " + str(e))
    if c.get(("Skin", "source"), "pictures").lower() == "game":
        prefix = c.get(("Game", "prefix"), "")
        if not prefix:
            warn.append("source = game without [Game] prefix")
            RESULT["errors"] += 1
        print("CHECK: " + ("all good" if not warn else f"{len(warn)} thing(s) to look at"))
        for w in warn: print("  ! " + w)
        print(f"  - no pictures of its own: the game's {prefix or '?'} frames are read at run time (the in-game export shows them; the scan checks the [Game] map)")
        return
    sheet_file = os.path.join(pack, c.get(("Skin", "sheet"), "sheet.png"))
    if ("Skin", "sheet") in c or os.path.exists(sheet_file):
        # one sheet of cells (an Aseprite export)
        sheet = Image.open(sheet_file).convert("RGBA")
        cols = max(1, int(c.get(("Skin", "columns"), str(sheet.width // cw))))
        if sheet.width % cw or sheet.height % ch:
            warn.append(f"the sheet is {sheet.width}x{sheet.height}, not whole {cw}x{ch} cells")
        cells = [sheet.crop(((i % cols) * cw, (i // cols) * ch, (i % cols) * cw + cw, (i // cols) * ch + ch)) for i in range(len(ORDER))]
    else:
        # a PNG per frame key (the built-in packs, a generator's output); the front layer's pictures join the checks
        cells, missing = [], []
        for k in ORDER:
            p = os.path.join(pack, k + ".png")
            if os.path.exists(p):
                im = Image.open(p).convert("RGBA")
                if im.size != (cw, ch):
                    warn.append(f"{k}.png is {im.width}x{im.height}, not the {cw}x{ch} canvas")
                    RESULT["errors"] += 1
                    im = im.crop((0, 0, cw, ch))
                cells.append(im)
            else:
                missing.append(k)
                cells.append(Image.new("RGBA", (cw, ch), (0, 0, 0, 0)))
        if parent:
            # built on its default: a frame it doesn't draw is the default's
            from_default = [k for k in missing if parent_picture(k) is not None]
            missing = [k for k in missing if k not in from_default]
            info.append(f"built on {os.path.basename(os.path.dirname(os.path.dirname(os.path.abspath(pack))))}/default: {len(ORDER) - len(missing) - len(from_default)} own picture(s), {len(from_default)} from it" + (", drawn over it" if over else ""))
        if missing:
            warn.append(f"no picture for {len(missing)} frame(s): {' '.join(missing)} (the {'default has none either: the ' if parent else ''}{c.get(('Skin', 'base'), 'donkey')} shows there)")
            if len(missing) == len(ORDER) and not parent:
                RESULT["errors"] += 1
        front_dir = os.path.join(pack, "front")
        if os.path.isdir(front_dir):
            fronts = 0
            for f in sorted(os.listdir(front_dir)):
                if not f.endswith(".png"):
                    continue
                k = f[:-4]
                if k not in ORDER and not re.match(r"^bdy_(fly|flyup[12]|flydown[12]|glide|glidein|glideout[1-4]|takeoff|land)_left_\d\d$", k):  # the flying frames (docs/flying.md)
                    warn.append(f"front/{f}: not a frame key")
                    RESULT["errors"] += 1
                    continue
                im = Image.open(os.path.join(front_dir, f)).convert("RGBA")
                if im.size != (cw, ch):
                    warn.append(f"front/{f} is {im.width}x{im.height}, not the {cw}x{ch} canvas")
                    RESULT["errors"] += 1
                    im = im.crop((0, 0, cw, ch))
                cells.append(im)  # its colours and strays are checked with the body's
                front_keys.append("front/" + k)
                fronts += 1
            info.append(f"front layer: {fronts} picture(s) drawn in front of the rider")

    named = {}
    for (sec, k), v in c.items():
        if sec == "Colors":
            for hx in v.split():
                named[hx.upper()] = k.capitalize()
    own_named = set(named)  # the stale-list warning counts its own lines only (the default's may not be in its pictures)
    for (sec, k), v in pc.items():
        if sec == "Colors":
            for hx in v.split():
                named.setdefault(hx.upper(), k.capitalize())  # a variant's colour map: the default's, its own over it
    donkey_cols = set()
    if over and parent:
        # over its default: the default's pictures are under it (the donkey's are not)
        dk = {k: im for k, im in ((k, parent_picture(k)) for k in ORDER) if im is not None}
    else:
        dk = {k: donkey(k) for k in ORDER} if over else {}  # the donkey is under an over skin only; a replace skin never meets its colours
    for im in dk.values():
        b = im.tobytes()
        donkey_cols |= {tuple(b[i:i + 3]) for i in range(0, len(b), 4) if b[i + 3] > 0}

    # the erase colour of an over skin (docs/skins.md): a pixel of it clears the donkey there and is never drawn; a
    # replace skin has none, so the magenta a drawing left behind is counted as the mistake it is (the scan refuses it)
    e = c.get(("Skin", "erase"), "FF00FF" if over else "none").strip()
    if not over and e.lower() == "none":
        e = "FF00FF"
    erase = None if e.lower() == "none" else tuple(int(e[i:i + 2], 16) for i in (0, 2, 4))
    erased, erased_frames, erased_front = 0, [], 0

    used, partial, empty, strays = {}, 0, [], []
    body_cells = dict(zip(ORDER, cells))

    def placed(im):
        # a picture centred on the canvas (the donkey's 96 under a bigger canvas), as the game places it
        if im.size == (cw, ch):
            return im
        f = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        f.alpha_composite(im, ((cw - im.width) // 2, (ch - im.height) // 2))
        return f

    for k, im in zip(ORDER + front_keys, cells):
        px = im.load(); n = 0; ne = 0
        # what the game draws under this picture: the donkey under an over skin, the body under a front picture. A
        # lone pixel on top of them is not a stray (a lava crack on the coat is one pixel wide; Hellfire walk 2)
        base_key = k[6:] if k.startswith("front/") else k
        under = [placed(dk[base_key]).load()] if base_key in dk else []
        if k.startswith("front/") and base_key in body_cells:
            under.append(body_cells[base_key].load())
        elif k.startswith("front/") and os.path.isfile(os.path.join(pack, base_key + ".png")):
            # a flying frame's body (not in ORDER's cells): its own picture, or a pixel of the near wing over the body was
            # a "stray" (the thumb claw's tip in a climbing frame, 2026-10-23)
            under.append(placed(Image.open(os.path.join(pack, base_key + ".png")).convert("RGBA")).load())
        for y in range(ch):
            for x in range(cw):
                p = px[x, y]
                if p[3] == 0:
                    continue
                n += 1
                if p[3] != 255:
                    partial += 1
                if erase is not None and p[:3] == erase:
                    ne += 1
                    continue
                used.setdefault(p[:3], set()).add(k)
                if not any(0 <= x + dx < cw and 0 <= y + dy < ch and (px[x + dx, y + dy][3] > 0 or any(u[x + dx, y + dy][3] > 0 for u in under))
                           for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
                    strays.append(f"{k} ({x},{y})")
        if n == 0:
            empty.append(k)
        if ne and k.startswith("front/"):
            erased_front += ne
        elif ne:
            erased += ne
            erased_frames.append(k)
    if erased_front:
        warn.append(f"{erased_front} pixel(s) of the erase colour {e} in front/ pictures: the front layer can't erase (the game leaves them out and says so)")
    if erased and not over:
        warn.append(f"{erased} pixel(s) of the erase colour {e} in a replace skin: only a skin over the donkey erases; they would be drawn as magenta (the scan refuses them)")
        RESULT["errors"] += 1
    elif erased:
        info.append(f"erase colour {e}: {erased} pixel(s) in {len(erased_frames)} frame(s) clear the {'default' if parent else 'donkey'} there; those frames are built whole in the game")

    hexes = {f"{r:02X}{g:02X}{b:02X}": (r, g, b) for (r, g, b) in used}
    clash = [h for h, rgb in hexes.items() if rgb in donkey_cols]  # over skins only (dk is empty for a replace skin)
    unnamed = [h for h in hexes if h not in named]
    missing = [h for h in named if h not in hexes and h in own_named]
    # a donkey colour drawn by an over skin follows the donkey's colour settings and the skin's [Base.*], as the
    # donkey's own pixels do: that is how a tuft or a fill continues the body (Hellfire's tuft, rump fill and tail),
    # so it is said, not warned about. The same colour named in [Colors] is a fight (one colour, two regions).
    # over its default: the default's own colours keep the default's regions (one map), only its own lines can fight
    fight = sorted(h for h in clash if h in (own_named if parent else named))
    kept = sorted(h for h in clash if h not in named)
    under_name = "default" if parent else "donkey"
    if fight:
        warn.append(f"{len(fight)} colour(s) named in [Colors] the {under_name} itself has: {', '.join(fight)} (one colour can be in one region only; draw them a shade apart)")
    if kept:
        info.append(f"{len(kept)} colour(s) of the {under_name}'s own, kept: {', '.join(kept)} (" + ("they are the default's colours: its regions reach them, Coat when unnamed" if parent else "they follow the donkey's colour settings and the skin's [Base.*], as the donkey's pixels do") + ")")
    if missing:
        warn.append(f"[Colors] names {len(missing)} colour(s) no picture has: {', '.join(sorted(missing))}")
    if partial:
        warn.append(f"{partial} half-transparent pixel(s): pixel art should be fully on or off")
    if strays:
        warn.append(f"{len(strays)} lone pixel(s) (nothing around them): {', '.join(strays[:8])}{' ...' if len(strays) > 8 else ''}")
    total = len(donkey_cols) + len([h for h in named if h in hexes]) if over else len(hexes)
    if total > 48:
        info.append(f"{total} colours to swap: over 48, so the game's colour cube recolours it instead of the exact swap (shaded art; docs/skins.md)")

    # frames where the donkey is drawn the same but the skin differs: likely one of them was missed (over skins only)
    groups = {}
    for k in ORDER:
        if k in dk and over and not parent: groups.setdefault(dk[k].tobytes(), []).append(k)  # a variant draws a few frames over its default on purpose
    for ks in groups.values():
        if len(ks) > 1:
            first = cells[ORDER.index(ks[0])].tobytes()
            odd = [k for k in ks[1:] if cells[ORDER.index(k)].tobytes() != first]
            if odd:
                info.append(f"same donkey picture as {ks[0]} but a different skin: {', '.join(odd)}")

    if parent:
        empty = [k for k in empty if parent_picture(k) is None]  # the default's picture is drawn there
    info.insert(0, f"{len(ORDER) - len(empty)} of {len(ORDER)} frames drawn"
                + (" (its own or its default's)" if parent else "")
                + (f" (empty: {', '.join(empty)})" if empty else ""))
    # over its default, a colour of the default's pixels is Coat (Looks.SortColours), one only its own is kept as drawn
    info.insert(1, f"{len(hexes)} colours: " + ", ".join(f"{h}={named.get(h, 'kept' if over and not (parent and hexes[h] in donkey_cols) else 'Coat')}" for h in sorted(hexes)))
    print("CHECK: " + ("all good" if not warn else f"{len(warn)} thing(s) to look at"))
    for w in warn:
        print("  ! " + w)
    for i in info:
        print("  - " + i)


if __name__ == "__main__":
    main(sys.argv[1])
    sys.exit(1 if RESULT["errors"] else 0)  # the release scans gate on the errors; the warnings are for the eye
