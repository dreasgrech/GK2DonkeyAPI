"""The Armored review loop (2026-11-01, the user: "make sure I can see it from our preview html pages so we can iterate
fast"): regenerates the pictures into out\\ only (the pack is left alone until the new cut is approved), stages each
cut being compared as a study ("Armored (...)"), rebuilds the skins preview page with them next to every other skin
(the shipped Armored among them) and opens the page. The page is local only (it embeds the game's frames).

    python -I tools/skins/armored/preview.py            # regenerate, rebuild the page, open it
    python -I tools/skins/armored/preview.py --no-open  # the same without opening a browser
"""
import os, shutil, subprocess, sys, webbrowser

HERE = os.path.dirname(os.path.abspath(__file__))
SKINS = os.path.dirname(HERE)
sys.path.insert(0, SKINS)
from gamedir import game_dir

# the cuts on the page: (folder under out\study, the card's name, the generator's arguments); the last one run is
# what out\pack holds afterwards
STUDIES = [
    ("armored-armet", "Armored (armet)", []),
    ("armored-armet-comb", "Armored (armet, gilt comb)", ["--comb"]),
]


def run(args):
    print(">", " ".join(os.path.basename(a) if os.path.isabs(a) else a for a in args))
    r = subprocess.run([sys.executable, "-I"] + args)
    if r.returncode != 0:
        sys.exit(r.returncode)


def stage(folder, name):
    pack = os.path.join(HERE, "out", "pack")
    study = os.path.join(HERE, "out", "study", folder)
    if os.path.isdir(study):
        shutil.rmtree(study)
    os.makedirs(study)
    for f in os.listdir(pack):
        if f.endswith(".png") and f.startswith(("bdy_", "hed_")):
            shutil.copy2(os.path.join(pack, f), study)
    cfg = open(os.path.join(pack, "skin.cfg"), encoding="utf-8").read().replace("\nname = Armored\n", f"\nname = {name}\n")
    with open(os.path.join(study, "skin.cfg"), "w", encoding="utf-8", newline="\r\n") as f:
        f.write(cfg.replace("\r\n", "\n"))
    return study


def main(argv):
    probe = os.path.join(game_dir(), "BepInEx", "GK2-asset-probe")
    if not os.path.isdir(os.path.join(probe, "donkey-frames")):
        print(f"no donkey frames under {probe} (the asset probe's export): the page has nothing to draw over")
        return 1
    staged = os.path.join(HERE, "out", "study")
    if os.path.isdir(staged):
        shutil.rmtree(staged)   # studies of earlier rounds leave the page
    args = []
    for folder, name, gen in STUDIES:
        run([os.path.join(HERE, "make_armored.py"), "--no-assets"] + gen)
        args += ["--study", stage(folder, name)]
    page = os.path.join(probe, "skins-preview.html")
    run([os.path.join(SKINS, "skin_preview_html.py"), probe, page] + args)
    print("page:", page)
    if "--no-open" not in argv:
        webbrowser.open("file:///" + page.replace("\\", "/"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
