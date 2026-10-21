"""Where the skins live in this repo: every src/<project>/assets/skins/<id>/skin.cfg. Two projects hold them since
2026-10-20 (docs/mounts-as-mods.md): the API's own (src/GK2DonkeyAPI: the donkey's Hellfire and Royal Parade) and
Saddle Up's (src/SaddleUp: its five mounts, the dragon, the rat, the snail, the worm, Gerry); a mount mod of its own
would be a third. Every tool and scan that walks the skins goes through this, so a mod's folder is one more root,
never a path in a script.

    roots()            every <repo>/src/<project>/assets/skins that exists, sorted
    skin_dirs()        every skin folder (holding a skin.cfg) under them, sorted by id
    find(skin_id)      the folder of one skin (its folder name), or None
    root_of(project)   <repo>/src/<project>/assets/skins (made on demand by the caller)
"""
import glob, os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # tools/skins -> the repo
API_PROJECT = "GK2DonkeyAPI"
MOUNTS_PROJECT = "SaddleUp"


def roots():
    return sorted(d for d in glob.glob(os.path.join(REPO, "src", "*", "assets", "skins")) if os.path.isdir(d))


def skin_dirs():
    out = []
    for r in roots():
        for d in sorted(os.listdir(r)):
            p = os.path.join(r, d)
            if os.path.isfile(os.path.join(p, "skin.cfg")):
                out.append(p)
    return sorted(out, key=lambda p: os.path.basename(p))


def find(skin_id):
    hits = [p for p in skin_dirs() if os.path.basename(p) == skin_id]
    if len(hits) > 1:
        raise SystemExit(f"the skin id {skin_id} is in more than one project: {', '.join(hits)} (one home per skin)")
    return hits[0] if hits else None


def root_of(project):
    return os.path.join(REPO, "src", project, "assets", "skins")
