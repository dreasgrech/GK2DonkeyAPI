"""The art and data half of a game-update audit (tests/GameDiff is the code half): every asset file the update changed,
compared object by object, so the audit can tell whether a sprite, texture or text the mods use changed
(docs/skins-design.md section 11: the donkey's sprite keys).

The game folder is a git repository with the update committed on top of the previous build (the user's habit since
1.008); the old files come from a revision of it, default HEAD~1, into a temporary folder that is removed afterwards.

    python tools/game-asset-diff.py                     # the game from Directory.Build.props, HEAD~1 against the files
    python tools/game-asset-diff.py <game folder> [old revision] [output file]

Prints totals per change and type, the game's version before and after (GameInfo.Version, what a new save is stamped
with), whether the donkey's art changed (its 0301_ sprites, the character atlases' textures: what every donkey skin is
drawn over; the API checks the same at run time, DonkeyFrames.cs), and the GameBalance changes that are not just
regenerated quest-node ids. The full list goes to the output
file (default %TEMP%\\game-asset-diff.txt, not into the repo). Read only: nothing in the game folder is written.
Needs UnityPy (pip install UnityPy).
"""
import difflib
import hashlib
import os
import re
import shutil
import subprocess
import sys
import tempfile
import warnings

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DONKEY, ATLAS = "0301_", "characters"   # the donkey's preset prefix and its atlases' name (Looks.Donkey)
GUID = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}.?$")   # .?: the next string's length


def game_dir():
    props = open(os.path.join(REPO, "Directory.Build.props"), encoding="utf-8").read()
    return re.search(r"<GameDir[^>]*>([^<]+)</GameDir>", props).group(1)


def git(game, *args):
    return subprocess.run(["git", "-C", game] + list(args), capture_output=True, check=True).stdout


def objects(path):
    """{(type, name) or (type, '', path id): (md5 of the raw bytes, size)} of one asset file."""
    out = {}
    for o in UnityPy.load(path).objects:
        try:
            name = o.peek_name() or ""
        except Exception:
            name = ""
        try:
            raw = o.get_raw_data()
        except Exception as e:
            raw = repr(e).encode()
        key = (o.type.name, name) if name else (o.type.name, "", o.path_id)
        base, n = key, 2
        while key in out:   # the same type and name twice: keep both
            key, n = base + (n,), n + 1
        out[key] = (hashlib.md5(raw).hexdigest(), len(raw))
    return out


def raw_of(path, name):
    for o in UnityPy.load(path).objects:
        if o.type.name == "MonoBehaviour" and o.peek_name() == name:
            return o.get_raw_data()
    return None


def texts(raw):
    return [m.group().decode("latin-1") for m in re.finditer(rb"[\x20-\x7e]{3,}", raw or b"")]


def main():
    args = sys.argv[1:]
    game = args[0] if args else game_dir()
    rev = args[1] if len(args) > 1 else "HEAD~1"
    out_path = args[2] if len(args) > 2 else os.path.join(tempfile.gettempdir(), "game-asset-diff.txt")
    data = os.path.join(game, "GraveyardKeeper2_Data")
    if not os.path.isdir(data):
        sys.exit(f"not a game folder: {game}")
    try:
        git(game, "rev-parse", "--verify", rev)
    except (subprocess.CalledProcessError, FileNotFoundError):
        sys.exit(f"{game} is not a git repository with a revision {rev}")
    version = re.search(rb"\d{4}\.\d+\.\d+f\d+", open(os.path.join(data, "globalgamemanagers"), "rb").read(4096))
    if not version:
        sys.exit("no Unity version in globalgamemanagers")
    UnityPy.config.FALLBACK_UNITY_VERSION = version.group().decode()   # the bundles carry none of their own
    warnings.filterwarnings("ignore", category=UnityPy.exceptions.UnityVersionFallbackWarning)   # so it is wanted

    changed = []   # (old path, new path), relative to the game folder
    for line in git(game, "diff", "--name-status", "-M", rev, "--", "GraveyardKeeper2_Data").decode().splitlines():
        parts = line.split("\t")
        old, new = (parts[1], parts[2]) if parts[0].startswith("R") else (parts[1], parts[1])
        if "/Managed/" in new or new.endswith((".dll", ".hash", ".config", ".bin")) or parts[0] in ("A", "D"):
            continue   # the code is tests/GameDiff's; the catalogue and settings are not assets
        changed.append((old, new))
    tmp = tempfile.mkdtemp(prefix="gk2-asset-diff-")
    lines, totals, info, balance, donkey = [], {}, {}, [], []
    try:
        for old, new in changed:
            old_file = os.path.join(tmp, "old")
            with open(old_file, "wb") as f:
                f.write(git(game, "show", f"{rev}:{old}"))
            a, b = objects(old_file), objects(os.path.join(game, new))
            diff = [("-", k) for k in sorted(set(a) - set(b))] + [("+", k) for k in sorted(set(b) - set(a))]
            diff += [("~", k) for k in sorted(k for k in set(a) & set(b) if a[k][0] != b[k][0])]
            lines.append(f"## {os.path.basename(new)}: {len(a)} -> {len(b)} objects, {len(diff)} differ")
            for tag, k in diff:
                lines.append(f"  {tag} {k[0]} {k[1]!r}" + (f" {a[k][1]} -> {b[k][1]} bytes" if tag == "~" else ""))
                totals[(tag, k[0])] = totals.get((tag, k[0]), 0) + 1
                if (k[0] == "Sprite" and k[1].startswith(DONKEY)) or (k[0] == "Texture2D" and ATLAS in k[1].lower()):
                    donkey.append(f"{tag} {k[0]} {k[1]}")
                if k[:2] == ("MonoBehaviour", "GameInfo"):
                    info = {"old": texts(raw_of(old_file, "GameInfo")),
                            "new": texts(raw_of(os.path.join(game, new), "GameInfo"))}
                if k[:2] == ("MonoBehaviour", "GameBalance"):
                    sa = texts(raw_of(old_file, "GameBalance"))
                    sb = texts(raw_of(os.path.join(game, new), "GameBalance"))
                    for op, i1, i2, j1, j2 in difflib.SequenceMatcher(a=sa, b=sb, autojunk=False).get_opcodes():
                        gone, came = sa[i1:i2], sb[j1:j2]
                        if op != "equal" and not all(GUID.match(s) for s in gone + came):
                            balance.append(f"  near {sa[max(0, i1 - 3):i1]}: -{gone} +{came}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    lines.append("## GameBalance changes that are not regenerated ids")
    lines += balance
    open(out_path, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(f"{len(changed)} changed asset files against {rev}; totals by change and type:")
    for (tag, t), n in sorted(totals.items()):
        print(f"  {tag} {t}: {n}")
    if info:
        print(f"GameInfo (the save version): {info['old']} -> {info['new']}")
    print("The donkey's art (0301_ sprites, the character atlases): " + (
        "unchanged" if not donkey else f"CHANGED, {len(donkey)}: " + "; ".join(donkey[:12])
        + " (compare the skins: tools/skins/donkey_frames.py, docs/skins-design.md section 11)"))
    print(f"GameBalance: {len(balance)} changed regions that are not just regenerated ids")
    print("Full list:", os.path.abspath(out_path))


if __name__ == "__main__":
    try:
        import UnityPy
    except ImportError:
        sys.exit("needs UnityPy: pip install UnityPy")
    main()
