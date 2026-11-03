"""Place the Jester's cap by hand in Aseprite (the user, 2026-11-03: "make it work by layer in aseprite so that i try
moving it by hand for one image and see how that works"; docs/sprite-tools.md "Placing a piece by hand").

    python -I tools/skins/jester/place.py template [--force]   # out/place.aseprite, a frame per view of the cap
    python -I tools/skins/jester/place.py import               # the cap layers' moves and scales: drawn and built
    python -I tools/skins/jester/place.py watch                # import on every save (Ctrl+C stops)
    python -I tools/skins/jester/place.py edit                 # the file opened in Aseprite (made if missing), and watch

out/place.aseprite has a frame per view of the cap:
- the side cap on the standing frame: every side frame is drawn from it (the walk, the ear, chew and lips clips,
  turn 02 and graze 02), its bells' swings too;
- the three-quarter cap on turn 03; the front cap on turn 05 (used in turn 04-06); the graze cap on graze 04 (graze
  03-05).
Its layers: the donkey with the rest of the skin (locked; the ears cleared as the cap clears them now), and "cap".
Move the cap layer's pixels on a frame (the Move tool) or scale them (Edit > Transform, Ctrl+T) and save: import
measures the cap's box on each frame against the box the file was made with and adds the change to placement.json
(per view: a move in pixels and a scale about the cap's middle); draw_jester.py draws the cap again there from its
shapes (a scale in Aseprite resamples the pixels: drawn again, they stay clean), stamp.py places it into all 44
frames and the card "Jester" on the preview page shows it. The file is not changed by an import: keep moving and
saving; each save is measured from the file as it was made. template --force makes it again from the cap as it is
now. The file holds the game's art (the donkey's frames): it stays in the git-ignored out/.
"""
import json
import os
import shutil
import subprocess
import sys
import time

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SKINS = os.path.dirname(HERE)
sys.path.insert(0, SKINS)
sys.path.insert(0, os.path.join(SKINS, "donkey"))
import ase_write
import stamp
from gamedir import donkey_frames
from watch_skin import ASEPRITE

FILE = os.path.join(HERE, "out", "place.aseprite")
REF = os.path.join(HERE, "out", "place")          # the cap layers as the file was made, and the placement then
PLACEMENT = os.path.join(HERE, "placement.json")
VIEWS = [("side", "bdy_static_left", "head.png"), ("turn", "bdy_turn_left_03", "head_turn.png"),
         ("front", "bdy_turn_left_05", "head_front.png"), ("down", "bdy_graze_left_04", "head_down.png")]


def read_placement():
    return json.load(open(PLACEMENT, encoding="utf-8")) if os.path.exists(PLACEMENT) else {}


def box(im):
    """The box of a cap picture's drawn pixels (the erase colour left out): (x0, y0, x1, y1), x1 and y1 past the end."""
    p = im.load()
    pts = [(x, y) for y in range(im.height) for x in range(im.width) if p[x, y][3] and p[x, y][:3] != (255, 0, 255)]
    if not pts:
        return None
    xs, ys = [q[0] for q in pts], [q[1] for q in pts]
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def template(force=False):
    if stamp.tracked(FILE):
        sys.exit(f"place: {FILE} would go into the repository (it holds the game's frames)")
    if os.path.exists(FILE) and not force:
        sys.exit(f"place: {FILE} exists (a move in it not imported yet would be lost): import it, then template --force")
    pieces, per_frame = stamp.read_source(HERE)
    erase = stamp.erase_of(HERE)
    os.makedirs(REF, exist_ok=True)
    frames, tags, notes = [], [], []
    for i, (view, key, piece) in enumerate(VIEWS):
        cap_px = stamp.read_piece(os.path.join(HERE, piece)) or {}
        rest = stamp.laid([l for l in stamp.frame_layers(key, pieces, per_frame, notes) if l[0] != "head"])
        under = Image.open(os.path.join(donkey_frames(), key + ".png")).convert("RGBA")
        u = under.load()
        for (x, y), c in cap_px.items():   # the ears cleared as the cap clears them
            if erase and c[:3] == erase:
                u[x, y] = (0, 0, 0, 0)
        for (x, y), c in rest.items():
            u[x, y] = (0, 0, 0, 0) if erase and c[:3] == erase else c
        cap = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
        cp = cap.load()
        for (x, y), c in cap_px.items():
            if not (erase and c[:3] == erase):
                cp[x, y] = c
        cap.save(os.path.join(REF, f"ref_{view}.png"))
        frames.append({0: under, 1: cap})
        tags.append((view, i, i))
    json.dump(read_placement(), open(os.path.join(REF, "base.json"), "w", encoding="utf-8"), indent=1)
    ase_write.write(FILE, 96, 96, [("donkey and the rest of the skin", stamp.LOCKED), ("cap", stamp.DRAWN)], frames,
                    tags, [1000] * len(frames))
    print(f"template: {FILE} ({', '.join(f'{v} on {k[4:]}' for v, k, _ in VIEWS)}): move or scale the cap layer on a "
          f"frame and save")


def import_():
    if not os.path.exists(FILE):
        sys.exit(f"place: no {FILE} (place.py template makes it)")
    tmp = os.path.join(REF, "export")
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(tmp)
    exe = os.environ.get("ASEPRITE", ASEPRITE)
    r = subprocess.run([exe, "-b", FILE, "--all-layers", "--split-layers", "--save-as",
                        os.path.join(tmp, "{layer}.{frame}.png")], capture_output=True, text=True, timeout=300)
    if r.returncode != 0:
        sys.exit(f"place: Aseprite's export failed: {(r.stdout + r.stderr).strip()}")
    base = json.load(open(os.path.join(REF, "base.json"), encoding="utf-8"))
    placement, said = dict(base), []
    for i, (view, key, piece) in enumerate(VIEWS):
        f = os.path.join(tmp, f"cap.{i}.png")
        new = box(Image.open(f).convert("RGBA")) if os.path.exists(f) else None
        old = box(Image.open(os.path.join(REF, f"ref_{view}.png")).convert("RGBA"))
        if new is None or old is None:
            said.append(f"{view}: the cap layer is empty on {key[4:]}, left as it was")
            continue
        if new == old:
            continue
        b = base.get(view, {})
        rx, ry = (new[2] - new[0]) / (old[2] - old[0]), (new[3] - new[1]) / (old[3] - old[1])
        mx = (new[0] + new[2]) / 2 - (old[0] + old[2]) / 2
        my = (new[1] + new[3]) / 2 - (old[1] + old[3]) / 2
        placement[view] = {"dx": round(b.get("dx", 0) + mx, 2), "dy": round(b.get("dy", 0) + my, 2),
                           "sx": round(b.get("sx", 1) * rx, 3), "sy": round(b.get("sy", 1) * ry, 3)}
        said.append(f"{view}: moved {mx:+.1f}, {my:+.1f}, scaled {rx:.2f} x {ry:.2f} -> {placement[view]}")
    shutil.rmtree(tmp, ignore_errors=True)
    if placement == read_placement():
        print("import: no change" + ("; " + "; ".join(said) if said else ""))
        return False
    with open(PLACEMENT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(placement, fh, indent=1)
        fh.write("\n")
    for line in said:
        print("import:", line)
    for args in ([os.path.join(HERE, "draw_jester.py")],
                 [os.path.join(SKINS, "stamp.py"), "build", HERE, "--stage", "Jester"]):
        r = subprocess.run([sys.executable, "-I"] + args, capture_output=True, text=True)
        for line in (r.stdout or r.stderr).strip().splitlines():
            if not line.startswith("   bdy_"):
                print("   " + line)
    return True


def watch():
    if not os.path.exists(FILE):
        template()
    print(f"watching {FILE}: every save moves the cap there, draws it again and builds the card \"Jester\" (Ctrl+C "
          f"stops)")
    last = os.path.getmtime(FILE)
    while True:
        time.sleep(0.5)
        try:
            m = os.path.getmtime(FILE)
        except OSError:
            continue   # mid-save
        if m == last:
            continue
        time.sleep(0.6)
        try:
            if os.path.getmtime(FILE) != m:
                continue   # not settled yet
        except OSError:
            continue
        last = m
        import_()


def edit():
    """The file opened in Aseprite (made first if missing; the ASEPRITE environment variable, else watch_skin's
    path), then watch."""
    if not os.path.exists(FILE):
        template()
    subprocess.Popen([os.environ.get("ASEPRITE", ASEPRITE), FILE])
    watch()


def main(argv):
    if argv[:1] == ["template"]:
        template("--force" in argv)
    elif argv[:1] == ["import"]:
        import_()
    elif argv[:1] == ["watch"]:
        watch()
    elif argv[:1] == ["edit"]:
        edit()
    else:
        print(__doc__)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
