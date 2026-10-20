"""A new skin, started the one way (docs/sprite-tools.md "Making a skin: the one pipeline"): the built-in folder with a
skin.cfg in the canonical layout, a generator folder with a stub that draws a placeholder for every frame, and the
Aseprite sidecar, then the scan. Everything else is drawing.

    python -I tools/skins/new_skin.py <id> [--mount <mount id> [--name "Giant Thing"]] [--canvas 96] [--seat 52] [--over]

  <id>        the skin's id and folder name (letters, digits, dashes): giant-crab
  --mount     a creature of its own: the [Mount] block is written (id = the mount id; the skin is its Default when the ids
              match); without it the skin is a skin of the donkey (mount = donkey)
  --over      drawn OVER the donkey (a cloth, a hat): layer = over with the erase colour, no [Mount]
  --canvas    the picture size (96; 128 for a bigger creature; the donkey's frame sits centred)
  --seat      the canvas row the rider's hip sits on (52 = the donkey's own seat on the 96 canvas; +16 on 128)
"""
import os, re, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "src", "GK2DonkeyAPI", "assets", "skins")


def arg(name, default=None):
    if name in sys.argv:
        i = sys.argv.index(name)
        return sys.argv[i + 1] if i + 1 < len(sys.argv) else default
    return default


VALUED = {"--mount", "--name", "--canvas", "--seat"}  # the flags that take a value; --over stands alone


def main():
    ids, skip = [], False
    for a in sys.argv[1:]:
        if skip:
            skip = False
            continue
        if a in VALUED:
            skip = True
        elif a.startswith("--"):
            if a != "--over":
                print("unknown flag", a); print(__doc__); return 2
        else:
            ids.append(a)
    if len(ids) != 1:
        print(__doc__); return 2
    sid = ids[0]
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]*", sid):
        print("the id is letters, digits and dashes, lower case:", sid); return 2
    mount = arg("--mount")
    over = "--over" in sys.argv
    if over and mount:
        print("--over is a skin drawn over the donkey: it has no mount of its own (drop --mount)"); return 2
    canvas = int(arg("--canvas", "96"))
    seat = int(arg("--seat", str(52 + (canvas - 96) // 2)))
    name = arg("--name") or sid.replace("-", " ").title()
    folder = os.path.join(ASSETS, sid)
    gen_dir = os.path.join(HERE, sid)
    if os.path.exists(folder) or os.path.exists(gen_dir):
        print("exists already:", folder if os.path.exists(folder) else gen_dir); return 2
    os.makedirs(folder)
    os.makedirs(gen_dir)
    gen = f"make_{sid.replace('-', '_')}.py"
    # the cfg, in the canonical layout (tools/skins/canonical_cfg.py: [Skin], the colours, the mount block last)
    lines = [f"# {name}: a {'skin drawn over the donkey' if over else 'mount of its own' if mount else 'skin of the donkey'} (docs/skins.md). Our own art, drawn by tools/skins/{sid}/{gen}.",
             "[Skin]", "format = 1", f"id = {sid}", f"name = {name}", "author = dreas", "version = 0.1", "base = donkey", "kind = own",
             f"layer = {'over' if over else 'replace'}"]
    if over:
        lines.append("erase = FF00FF       # over only: pixels of this colour clear the donkey there (the ears under horns)")
    lines.append(f"canvas = {canvas} {canvas}")
    if not over:
        lines.append(f"seat = {seat}            # the canvas row the rider's hip sits on, every frame, for every player (52 = the donkey's own on 96)")
    lines.append(f"mount = {mount or 'donkey'}")
    lines += ["", "# The skin's colours by mount region (docs/mount-colours.md); every colour not named is Coat.", "[Colors]",
              "Dark = 141E14        # the placeholder's outline: replace with the hexes your pictures use (tools/skins/skin_colors.py writes the section from a colors.json block)"]
    if mount:
        lines += ["", "# The mount this skin is the Default of (docs/mounts-design.md): what the player rides, shared by every skin of it.",
                  "[Mount]", f"id = {mount}", f"name = {name}", "author = dreas", "version = 0.1", "cart = true", "foals = true",
                  "# foal_scale = 0.6  # the babies' size (0.2 to 1) when not 0.6",
                  "", "# What the creature is: its sounds (docs/reference.md); the rider's seat and the cart are the skin's rows above.",
                  "# [Mount.Tuning]", "# BrayVolume = 0", "# HoofVolume = 0"]
    cfg_path = os.path.join(folder, "skin.cfg")
    with open(cfg_path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("\n".join(lines) + "\n")
    # through the canonical writer, so the file is byte for byte what the scans expect from a built-in
    sys.path.insert(0, HERE)
    import canonical_cfg
    text = canonical_cfg.canonical(cfg_path)
    with open(cfg_path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    # the generator stub: a placeholder block per frame on the canvas, through the shared review pictures
    stub = f'''"""{name}: the generator (docs/sprite-tools.md "Making a skin"). Draws every frame of the skin on the {canvas}x{canvas}
canvas (the donkey's frame sits centred; its back at row 41, feet on row 64 of the 96 canvas), writes a PNG per frame
into src/GK2DonkeyAPI/assets/skins/{sid}/ and the review pictures into out\\\\ (look at every strip before the game).

    python -I tools/skins/{sid}/{gen}            # the built-in's pictures + out\\\\
    python -I tools/skins/{sid}/{gen} --no-assets  # out\\\\ only
"""
import os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import review
from gamedir import donkey_frames, game_dir  # donkey_frames: the base's pictures an over skin is drawn on

REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
ASSETS = os.path.join(REPO, "src", "GK2DonkeyAPI", "assets", "skins", "{sid}")
OUT = os.path.join(HERE, "out")
CANVAS = {canvas}
OFF = (CANVAS - 96) // 2


def frame(key, phase):
    """One frame: replace this with your drawing. key: the donkey frame key; phase: 0..1 through the clip."""
    im = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    bob = int(round(2 * abs(0.5 - phase) * 2)) if "walk" in key else 0
    d.rectangle([OFF + 28, OFF + 40 + bob, OFF + 70, OFF + 64], fill=(120, 160, 90, 255), outline=(20, 30, 20, 255))
    return im


def main(argv):
    frames = {{}}
    for k in review.ORDER:
        if k.startswith("hed_"):
            frames[k] = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))  # the head key empty: the body frames draw the head
            continue
        n = int(k.rsplit("_", 1)[1]) if k[-1].isdigit() else 1
        frames[k] = frame(k, (n - 1) / 8.0)
    if "--no-assets" not in argv:
        os.makedirs(ASSETS, exist_ok=True)
        for k, im in frames.items():
            im.save(os.path.join(ASSETS, k + ".png"))
        print("pictures ->", ASSETS)
    rider = review.load_rider(os.path.join(game_dir(), "BepInEx", "GK2-asset-probe"))
    base = {'review.load_base(donkey_frames())' if over else 'None'}  # an over skin is shown over the donkey, with its erase colour cleared
    strips = review.write_all(OUT, frames, base=base, erase={'(255, 0, 255)' if over else 'None'}, rider=rider, seat_row={seat if not over else 'review.DONKEY_SEAT_ROW'})
    print("strips ->", strips)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
'''
    with open(os.path.join(gen_dir, gen), "w", encoding="utf-8", newline="\n") as f:
        f.write(stub)
    with open(os.path.join(gen_dir, ".gitignore"), "w", encoding="utf-8", newline="\n") as f:
        f.write("out/\n__pycache__/\n")
    # the Aseprite sidecar: the watcher exports a drawing into the built-in's folder as per-frame PNGs
    with open(os.path.join(gen_dir, f"{sid}.gk2export.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(f"pack = {folder}\ncolumns = 8\ncheck = python \"{os.path.join(HERE, 'check_skin.py')}\" \"{folder}\"\n")
    print("made:", folder, "and", gen_dir)
    # the placeholder pictures, then the scan
    r = subprocess.run([sys.executable, "-I", os.path.join(gen_dir, gen)], capture_output=True, text=True)
    print(r.stdout.strip() or r.stderr.strip())
    print("scan:")
    r = subprocess.run(["dotnet", "run", "--project", os.path.join(REPO, "tests", "SkinsScan"), "-c", "Release"], capture_output=True, text=True, cwd=REPO)
    for line in (r.stdout + r.stderr).splitlines():
        if sid in line or line.startswith(("ALL PASS", "FAILURES")):
            print("  " + line)
    print(f"Next: draw in tools/skins/{sid}/{gen} (or in Aseprite with the watcher), fill [Colors] (tools/skins/skin_colors.py),"
          f" run the checks, build, and look in game. To remove it: delete {folder} and {gen_dir}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
