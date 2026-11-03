"""Rebuild the skins preview page (the asset probe's skins-preview.html) with every cut being reviewed on it: each
study folder under tools/skins/*/out/study/ (a generator's or a stamp build's "<Skin> (in progress)" cards,
git-ignored), plus any folder given, beside every shipped skin. One command, so staging one cut never drops another's
card.

    python -I tools/skins/page.py [<study folder> ...] [--list]

A study folder is a skin folder (skin.cfg and its pictures) whose skin.cfg names the card ("name = Armored (in
progress)"). --list only prints the studies found.
"""
import glob
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from gamedir import game_dir


def studies(extra):
    found = sorted(d for d in glob.glob(os.path.join(HERE, "*", "out", "study", "*"))
                   if os.path.exists(os.path.join(d, "skin.cfg")))
    for e in extra:
        e = os.path.abspath(e)
        if not os.path.exists(os.path.join(e, "skin.cfg")):
            sys.exit(f"page: no skin.cfg in {e}")
        if e not in found:
            found.append(e)
    return found


def card_name(d):
    for l in open(os.path.join(d, "skin.cfg"), encoding="utf-8").read().splitlines():
        s = l.split("#", 1)[0].strip()
        if s.startswith("name") and "=" in s:
            return s.split("=", 1)[1].strip()
    return os.path.basename(d)


def main(argv):
    extra = [a for a in argv if not a.startswith("--")]
    found = studies(extra)
    for d in found:
        print(f"study: {card_name(d)}  ({d})")
    if "--list" in argv:
        return 0
    probe = os.path.join(game_dir(), "BepInEx", "GK2-asset-probe")
    page = os.path.join(probe, "skins-preview.html")
    args = []
    for d in found:
        args += ["--study", d]
    r = subprocess.run([sys.executable, "-I", os.path.join(HERE, "skin_preview_html.py"), probe, page] + args,
                       capture_output=True, text=True)
    out = (r.stdout or r.stderr).strip().splitlines()
    print(out[0] if out else "skin_preview_html.py: no output")
    print("page:", page)
    return r.returncode


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
