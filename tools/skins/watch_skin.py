# Watches a skin's .aseprite file: on every save, exports it to its pack (the "GK2 Export skin" script run from the
# command line, the checker after it) and writes previews next to the file (preview\contact.png: every frame;
# preview\<tag>.gif: each animation looping at game speed), the skin drawn over the donkey when it is worn over it.
# With Debug.SkinPackAutoReload on in the game, the game picks the new sheet up by itself: save, look.
#
#   python tools/skins/watch_skin.py tools/skins/royal-parade/out/royal.aseprite   (watch; Ctrl+C stops)
#   python watch_skin.py ...\royal.aseprite --once                                    (one export, no watching)
#
# The pack folder and the check command come from <name>.gk2export.txt next to the file (as for the menu script);
# "aseprite = <path to aseprite.exe>" there overrides the default build below.
import datetime, os, subprocess, sys, time
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from check_skin import ORDER, donkey, cfg as read_cfg

ASEPRITE = r"C:\Users\User\Documents\GitHub\aseprite\build\bin\aseprite.exe"
EXPORT = os.path.join(HERE, "scripts", "GK2 Export skin.lua")
TAGS = [("static", 1), ("walk", 8), ("ear", 5), ("graze", 5), ("chew", 3), ("lips", 13), ("turn", 6), ("head", 3)]
BG = (46, 46, 50, 255)


def sidecar(path):
    side = os.path.splitext(path)[0] + ".gk2export.txt"
    out = {}
    if os.path.isfile(side):
        for line in open(side, encoding="utf-8"):
            if "=" in line:
                k, v = (x.strip() for x in line.split("=", 1))
                out[k] = v
    return out


def export(path, side):
    exe = side.get("aseprite", ASEPRITE)
    r = subprocess.run([exe, "-b", path, "--script", EXPORT], capture_output=True, text=True, timeout=300)
    text = (r.stdout + r.stderr).strip()
    return r.returncode == 0 and "sheet.png:" in text, text


def split_sheet(pack, cfg, columns=None):
    """The exported sheet.png (and front.png) into the per-frame PNGs the built-ins use (bdy_*.png, front/bdy_*.png) in
    the pack, keys in ORDER, `columns` cells per row (the sidecar's, as the export laid them out; else the cfg's, else
    8); the sheets are removed unless the cfg says 'sheet =' (a sheet pack). A front picture whose cell is empty now
    is removed (a part erased in Aseprite goes from the pack too)."""
    cw, ch = (int(v) for v in cfg.get(("Skin", "canvas"), "96 96").split())
    cols = int(columns or cfg.get(("Skin", "columns"), "8"))
    if ("Skin", "sheet") in cfg:
        return 0
    n = 0
    for sheet_name, prefix in (("sheet.png", ""), ("front.png", "front/")):
        p = os.path.join(pack, sheet_name)
        if not os.path.isfile(p):
            continue
        sheet = Image.open(p).convert("RGBA")
        for i, k in enumerate(ORDER):
            cell = sheet.crop(((i % cols) * cw, (i // cols) * ch, (i % cols) * cw + cw, (i // cols) * ch + ch))
            target = os.path.join(pack, prefix.replace("/", os.sep), k + ".png")
            if prefix and not cell.getbbox():
                if os.path.isfile(target):
                    os.remove(target)  # an empty front cell: no front picture for that frame any more
                continue
            os.makedirs(os.path.dirname(target), exist_ok=True)
            cell.save(target)
            n += 1
        os.remove(p)
    return n


def load_frames(pack, cfg):
    """The pack's pictures as {key: image}: the per-frame PNGs, else the cells of its sheet."""
    cw, ch = (int(v) for v in cfg.get(("Skin", "canvas"), "96 96").split())
    out, front = {}, {}
    sheet_path = os.path.join(pack, cfg.get(("Skin", "sheet"), "sheet.png"))
    if ("Skin", "sheet") in cfg and os.path.isfile(sheet_path):
        sheet = Image.open(sheet_path).convert("RGBA")
        cols = int(cfg.get(("Skin", "columns"), str(sheet.width // cw)))
        for i, k in enumerate(ORDER):
            out[k] = sheet.crop(((i % cols) * cw, (i // cols) * ch, (i % cols) * cw + cw, (i // cols) * ch + ch))
        return out, front
    for k in ORDER:
        p = os.path.join(pack, k + ".png")
        if os.path.isfile(p):
            out[k] = Image.open(p).convert("RGBA")
        pf = os.path.join(pack, "front", k + ".png")
        if os.path.isfile(pf):
            front["front/" + k] = Image.open(pf).convert("RGBA")
    return out, front


def previews(path, pack):
    import review
    c = read_cfg(os.path.join(pack, "skin.cfg"))
    cw, ch = (int(v) for v in c.get(("Skin", "canvas"), "96 96").split())
    over = c.get(("Skin", "layer"), "replace").lower() == "over"
    own, front = load_frames(pack, c)
    e = c.get(("Skin", "erase"), "FF00FF" if over else "none").strip()
    erase = None if e.lower() == "none" else tuple(int(e[i:i + 2], 16) for i in (0, 2, 4))
    base = {k: donkey(k) for k in ORDER} if over else None
    seat = c.get(("Skin", "seat"), "")
    seat_row = int(seat) if seat.isdigit() else (review.DONKEY_SEAT_ROW + (max(cw, ch) - 96) // 2 if over else None)
    rider_dx, seat_adj = review.rider_tuning(os.path.join(pack, "skin.cfg"))
    from gamedir import game_dir
    rider = review.load_rider(os.path.join(game_dir(), "BepInEx", "GK2-asset-probe"))
    frames = []
    for k in ORDER:
        if k not in own and not over:
            frames.append(Image.new("RGBA", (cw, ch), BG))
            continue
        f = Image.new("RGBA", (cw, ch), BG)  # an over skin's missing frame shows the donkey, as the game does
        f.alpha_composite(review.composite(k, own, base, erase, front or None, rider, seat_row, rider_dx, seat_adj))
        frames.append(f)
    out = os.path.join(os.path.dirname(path), "preview")
    os.makedirs(out, exist_ok=True)
    S, C = 4, 8
    rows = (len(frames) + C - 1) // C
    contact = Image.new("RGBA", (C * cw * S, rows * ch * S), BG)
    for i, f in enumerate(frames):
        contact.paste(f.resize((cw * S, ch * S), Image.NEAREST), ((i % C) * cw * S, (i // C) * ch * S))
    contact.save(os.path.join(out, "contact.png"))
    n = 0
    for name, count in TAGS:
        seq = [f.resize((cw * S, ch * S), Image.NEAREST).convert("RGB") for f in frames[n:n + count]]
        n += count
        if len(seq) > 1:
            seq[0].save(os.path.join(out, name + ".gif"), save_all=True, append_images=seq[1:],
                        duration=100 if name == "walk" else 150, loop=0, optimize=False)
    return out


def once(path):
    side = sidecar(path)
    if not side.get("pack"):
        print(f"No pack folder: write 'pack = ...' into {os.path.splitext(path)[0]}.gk2export.txt (or export once from the menu).")
        return
    stamp = datetime.datetime.now().strftime("%H:%M:%S")
    ok, text = export(path, side)
    print(f"[{stamp}] export {'done' if ok else 'FAILED'}")
    if ok:
        # the built-ins' format: a PNG per frame (the game reads a sheet only with 'sheet =' in the cfg)
        n = split_sheet(side["pack"], read_cfg(os.path.join(side["pack"], "skin.cfg")), side.get("columns"))
        if n: print(f"   {n} pictures written per frame into {side['pack']} (the sheet split, as the built-ins are kept)")
    for line in text.splitlines():
        if line.strip() and not line.startswith("In game:"):
            print("   " + line)
    if ok:
        print("   previews: " + previews(path, side["pack"]))


def watch(path):
    print(f"Watching {path} (Ctrl+C stops). Save in Aseprite to export.")
    last = os.path.getmtime(path)
    while True:
        time.sleep(0.5)
        try:
            m = os.path.getmtime(path)
        except OSError:
            continue  # mid-save: the editor replaces the file
        if m == last:
            continue
        time.sleep(0.6)  # settled: the same stamp after a moment
        try:
            if os.path.getmtime(path) != m:
                continue
        except OSError:
            continue
        last = m
        once(path)


if __name__ == "__main__":
    target = os.path.abspath(sys.argv[1])
    if "--once" in sys.argv:
        once(target)
    else:
        try:
            watch(target)
        except KeyboardInterrupt:
            print("Stopped.")
