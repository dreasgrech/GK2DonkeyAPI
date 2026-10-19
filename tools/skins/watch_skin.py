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


def previews(path, pack):
    c = read_cfg(os.path.join(pack, "skin.cfg"))
    cw, ch = (int(v) for v in c.get(("Skin", "canvas"), "96 96").split())
    over = c.get(("Skin", "layer"), "replace").lower() == "over"
    sheet = Image.open(os.path.join(pack, c.get(("Skin", "sheet"), "sheet.png"))).convert("RGBA")
    cols = int(c.get(("Skin", "columns"), str(sheet.width // cw)))
    frames = []
    for i, k in enumerate(ORDER):
        cell = sheet.crop(((i % cols) * cw, (i // cols) * ch, (i % cols) * cw + cw, (i // cols) * ch + ch))
        f = Image.new("RGBA", (cw, ch), BG)
        if over:
            d = donkey(k)
            f.alpha_composite(d, ((cw - d.width) // 2, (ch - d.height) // 2))
        f.alpha_composite(cell)
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
