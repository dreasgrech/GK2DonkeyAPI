"""Where the mounts and skins live in this repo, in two kinds (the user, 2026-10-26: "make a distinction in the
directories between inbuilt mounts/skins in saddle up vs ones which are 'external'"):

- built in: src/<project>/assets/mounts/<mount>/, embedded in the project's DLL (Saddle Up's: Gerry, and the donkey's
  Hellfire and Royal Parade under mounts/donkey/skins/; the API ships none);
- external: packs/<Pack>/gk2mounts/<mount>/, each pack its own download, laid out as BepInEx/plugins/<Pack>/ (the
  Dragon: packs/SaddleUpDragon/; the rat, snail and worm: packs/SaddleUpCritters/). A pack's ids carry its own
  prefix, the pack's name in lower case with dashes where wanted (SaddleUpDragon: saddleup-dragon.dragon); pack_prefix
  and the scans check it.

Each mount folder holds its mount.cfg and sounds/, its skins in skins/<skin>/skin.cfg (docs/skins.md "What a pack is").
Every tool and scan that walks the packs goes through this, so a new pack is one more folder, never a path in a script.

The names (2026-10-24, Looks.SkinKey / Catalog.CheckId): a mount's id is its folder's name, "<mod>.<name>"
(saddleup.dragon; only the game's donkey is plain); a skin's full name is "<mount id>/<folder>" (saddleup.dragon/default,
donkey/saddleup.hellfire), and a mount's own look is its skins/default/. A skin added to a mount another mod owns (a
mount folder without mount.cfg: every donkey skin) is named "<mod>.<name>" (Looks.AddsToOthers); on its own mount any
name.

    roots()             every <repo>/src/<project>/assets/mounts and <repo>/packs/<Pack>/gk2mounts that exists, sorted
    kind(path)          "built in" or "external <Pack>" for a mount or skin folder (or any path under one)
    pack_of(path)       the pack's name a path is under (SaddleUpDragon), or None for a built-in one
    pack_prefix_ok(pack, mount_id)  whether a mount id carries its pack's prefix (its dashes left out, the pack's name)
    mount_dirs()        every mount folder under them (donkey/ included: it holds skins only), sorted by name
    skin_dirs()         every skin folder (<mount>/skins/<skin>/ holding a skin.cfg), sorted by full name
    mount_of(skin_dir)  the mount id (the mount folder's name) a skin sits in
    full_name(skin_dir) the skin's full name, "<mount id>/<skin>"
    find(name)          the folder of one skin, or None: by full name (saddleup.dragon/default, dragon/default), by a
                        mount's id or short name (its own look: dragon -> saddleup.dragon/default), or by a skin's
                        folder name when one mount alone has it (saddleup.hellfire), or the name after its
                        prefix (hellfire, donkey/hellfire: what the skins were called before the prefix, as Looks.Find
                        still reads them); several matches stop with a message
    find_mount(id)      the folder of one mount, or None: by its id (saddleup.dragon) or, when one mount alone has it,
                        the name after the prefix (dragon)
    short_name(id)      the name after a mount id's prefix (saddleup.dragon -> dragon; donkey -> donkey)
    root_of(project)    <repo>/src/<project>/assets/mounts (made on demand by the caller)
    pack_root(pack)     <repo>/packs/<Pack>/gk2mounts (made on demand by the caller)
"""
import glob, os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # tools/skins -> the repo
API_PROJECT = "GK2DonkeyAPI"
MOUNTS_PROJECT = "SaddleUp"
SKINS = "skins"


PACKS = os.path.join(REPO, "packs")


def roots():
    built_in = glob.glob(os.path.join(REPO, "src", "*", "assets", "mounts"))
    external = glob.glob(os.path.join(PACKS, "*", "gk2mounts"))
    return sorted(d for d in built_in + external if os.path.isdir(d))


def pack_of(path):
    rel = os.path.relpath(os.path.abspath(path), PACKS)
    if rel.startswith(".."):
        return None
    return rel.split(os.sep)[0]


def kind(path):
    pack = pack_of(path)
    return "built in" if pack is None else "external " + pack


def pack_prefix_ok(pack, mount_id):
    """A pack's mount ids are "<prefix>.<name>" with the prefix the pack's name in lower case, dashes free:
    SaddleUpDragon -> saddleup-dragon.dragon (the user, 2026-10-26: "ids need to be depending on pack")."""
    if "." not in mount_id:
        return False
    return mount_id.split(".", 1)[0].replace("-", "").lower() == pack.lower()


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
    return sorted(out, key=lambda p: full_name(p).lower())


def mount_of(skin_dir):
    return os.path.basename(os.path.dirname(os.path.dirname(os.path.abspath(skin_dir))))


def full_name(skin_dir):
    return mount_of(skin_dir) + "/" + os.path.basename(os.path.abspath(skin_dir))


def short_name(mount_id):
    return mount_id.split(".", 1)[1] if "." in mount_id else mount_id


def _one(hits, what):
    if len(hits) > 1:
        raise SystemExit(f"{what} is in more than one place: {', '.join(hits)} (say which by its full name)")
    return hits[0] if hits else None


def find_mount(mount_id):
    want = mount_id.strip().lower()
    hits = [p for p in mount_dirs() if os.path.basename(p).lower() == want]
    if not hits and "." not in want:
        hits = [p for p in mount_dirs() if short_name(os.path.basename(p)).lower() == want]
    return _one(hits, f"the mount {mount_id}")


def find(name):
    want = name.strip().replace("\\", "/").lower()
    if "/" in want:
        mount, skin = want.split("/", 1)
        m = find_mount(mount)
        if m is None:
            return None
        p = os.path.join(m, SKINS, skin)
        hits = [d for d in skin_dirs() if os.path.normcase(d) == os.path.normcase(p)]
        if not hits and "." not in skin:  # donkey/hellfire: the skin of that mount named so after its prefix
            hits = [d for d in skin_dirs() if mount_of(d).lower() == os.path.basename(m).lower() and short_name(os.path.basename(d)).lower() == skin]
            return _one(hits, f"the skin {name}")
        return hits[0] if hits else None
    m = find_mount(want)
    if m is not None and os.path.isfile(os.path.join(m, SKINS, "default", "skin.cfg")):
        return os.path.join(m, SKINS, "default")  # a mount's id: its own look (as Looks.Find reads it)
    hits = [p for p in skin_dirs() if os.path.basename(p).lower() == want]
    if not hits and "." not in want:  # hellfire: saddleup.hellfire, the name after its prefix
        hits = [p for p in skin_dirs() if "." in os.path.basename(p) and short_name(os.path.basename(p)).lower() == want]
    return _one(hits, f"the skin {name}")


def root_of(project):
    return os.path.join(REPO, "src", project, "assets", "mounts")


def pack_root(pack):
    return os.path.join(PACKS, pack, "gk2mounts")
