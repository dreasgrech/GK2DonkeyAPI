"""Where the mounts and skins live in this repo: every src/<project>/assets/mounts/<mount>/ (its mount.cfg and
sounds/) with its skins in skins/<skin>/skin.cfg (the user, 2026-10-24: "sorted by mount"; docs/skins.md "What a pack
is"). Saddle Up's project holds them (the five mounts, and the donkey's Hellfire and Royal Parade under
mounts/donkey/skins/; the API ships none); a mount mod of its own would be a second root. Every tool and scan that walks
the packs goes through this, so a mod's folder is one more root, never a path in a script.

    roots()             every <repo>/src/<project>/assets/mounts that exists, sorted
    mount_dirs()        every mount folder under them (donkey/ included: it holds skins only), sorted by name
    skin_dirs()         every skin folder (<mount>/skins/<skin>/ holding a skin.cfg), sorted by id
    mount_of(skin_dir)  the mount folder's name a skin sits in
    find(skin_id)       the folder of one skin (its folder name), or None
    find_mount(id)      the folder of one mount, or None
    root_of(project)    <repo>/src/<project>/assets/mounts (made on demand by the caller)
"""
import glob, os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # tools/skins -> the repo
API_PROJECT = "GK2DonkeyAPI"
MOUNTS_PROJECT = "SaddleUp"
SKINS = "skins"


def roots():
    return sorted(d for d in glob.glob(os.path.join(REPO, "src", "*", "assets", "mounts")) if os.path.isdir(d))


def mount_dirs():
    out = []
    for r in roots():
        for d in sorted(os.listdir(r)):
            p = os.path.join(r, d)
            if os.path.isdir(p):
                out.append(p)
    return sorted(out, key=lambda p: os.path.basename(p))


def skin_dirs():
    out = []
    for m in mount_dirs():
        s = os.path.join(m, SKINS)
        if not os.path.isdir(s):
            continue
        for d in sorted(os.listdir(s)):
            p = os.path.join(s, d)
            if os.path.isfile(os.path.join(p, "skin.cfg")):
                out.append(p)
    return sorted(out, key=lambda p: os.path.basename(p))


def mount_of(skin_dir):
    return os.path.basename(os.path.dirname(os.path.dirname(os.path.abspath(skin_dir))))


def find(skin_id):
    hits = [p for p in skin_dirs() if os.path.basename(p) == skin_id]
    if len(hits) > 1:
        raise SystemExit(f"the skin id {skin_id} is in more than one place: {', '.join(hits)} (one home per skin)")
    return hits[0] if hits else None


def find_mount(mount_id):
    hits = [p for p in mount_dirs() if os.path.basename(p) == mount_id]
    if len(hits) > 1:
        raise SystemExit(f"the mount {mount_id} is in more than one project: {', '.join(hits)} (one home per mount)")
    return hits[0] if hits else None


def root_of(project):
    return os.path.join(REPO, "src", project, "assets", "mounts")
