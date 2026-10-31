"""The translators' English templates, from a dev run into the repository (docs/localization.md, "Built").

    python -I tools/l10n-sync.py           # copy BepInEx/GK2-l10n/<guid>/en.txt and en.csv into each plugin's Languages/
    python -I tools/l10n-sync.py --check   # copy nothing; exit 1 when the repository's differ from the last dev run's,
                                           # or when the API's game code says a sentence of its own (below)

A dev run (the dev tools unlocked) writes each plugin's English when a settings page is shown: a page's rows, its
sections and title, the API's own words (UiWords: the windows, the keeper's bubbles, the prompts on foot), and the
names and descriptions of the mounts and skins each mod or pack registered (Texts.WriteEnglish, Texts.Owners). The code is the source of the English; these
files are what translators copy (en.txt for a text editor, en.csv for a spreadsheet), shipped beside the DLL
(tools/make-release-zip.py, DeployToGame.targets). A plugin's GUID is read from its Plugin.cs (Guid constant) to find
its source folder: src/GK2DonkeyAPI, src/SaddleUp, packs/<Pack>. Also reports, for every translation in a Languages/
folder, the keys the English no longer has (left over after a rename: harmless, the game ignores them). Line endings
don't count in the comparison (git's core.autocrlf changes them on checkout).

--check also looks for a bubble or a prompt said with a literal in the API's game code (src/GK2DonkeyAPI, not Dev/):
Mount.Say("..."), HintText("..."), HintFor(key, "..."), a foot action's Set(..., "..."). Those sentences are words of
UiWords (the code uses a word's English, Word.Make for a template), or they would never be translated.
"""
import os
import re
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEMPLATES = ("en.txt", "en.csv")


def game_dir():
    props = open(os.path.join(ROOT, "Directory.Build.props"), encoding="utf-8").read()
    m = re.search(r"<GameDir[^>]*>([^<]+)</GameDir>", props)
    if not m:
        sys.exit("no GameDir in Directory.Build.props")
    return m.group(1).strip()


def plugins():
    """{guid: source folder} for the API, Saddle Up and every pack (their Plugin.cs Guid constant)."""
    found = {}
    folders = [os.path.join(ROOT, "src", "GK2DonkeyAPI"), os.path.join(ROOT, "src", "SaddleUp")]
    packs = os.path.join(ROOT, "packs")
    if os.path.isdir(packs):
        folders += [os.path.join(packs, d) for d in sorted(os.listdir(packs)) if os.path.isfile(os.path.join(packs, d, "Plugin.cs"))]
    for f in folders:
        src = open(os.path.join(f, "Plugin.cs"), encoding="utf-8").read()
        m = re.search(r'public const string Guid = "([^"]+)"', src)
        if m:
            found[m.group(1)] = f
    return found


LITERALS = [
    re.compile(r'\bSay\(\s*\$?"'),
    re.compile(r'\bHintText\(\s*\$?"'),
    re.compile(r'\bHintFor\([^,()]+,\s*\$?"'),
    re.compile(r'\bSet\([^,()]+,\s*FootAction\.\w+,\s*\$?"'),
]


def literals():
    """Every bubble or prompt said with a literal in the API's game code: (file:line, the line)."""
    found = []
    api = os.path.join(ROOT, "src", "GK2DonkeyAPI")
    for dirpath, dirs, files in os.walk(api):
        rel_dir = os.path.relpath(dirpath, api).replace("\\", "/")
        if rel_dir.split("/")[0] in ("Dev", "bin", "obj"):
            continue
        for f in files:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(dirpath, f)
            for n, line in enumerate(open(path, encoding="utf-8-sig"), 1):
                code = line.split("//", 1)[0] if line.split("//", 1)[0].count('"') % 2 == 0 else line
                if any(rx.search(code) for rx in LITERALS):
                    found.append((f"{os.path.relpath(path, ROOT)}:{n}", line.strip()))
    return found


def keys_of(path):
    """The keys of a .txt translation file (key = text lines; # lines are comments)."""
    keys = []
    for line in open(path, encoding="utf-8-sig").read().split("\n"):
        s = line.strip()
        if s and not s.startswith("#") and "=" in s:
            keys.append(s.split("=", 1)[0].strip())
    return keys


def csv_keys(path):
    """The first column of a .csv translation file after its header (enough for the leftover-key report)."""
    text = open(path, encoding="utf-8-sig").read()
    lines = text.split("\n")
    sep = ";" if lines and lines[0].startswith("key;") else "\t" if lines and lines[0].startswith("key\t") else ","
    keys = []
    for line in lines[1:]:
        if line.strip() and not line.startswith('"'):
            keys.append(line.split(sep, 1)[0].strip())
    return keys


def main():
    check = "--check" in sys.argv[1:]
    dumps = os.path.join(game_dir(), "BepInEx", "GK2-l10n")
    if not os.path.isdir(dumps):
        sys.exit(f"{dumps} doesn't exist: start the game once with the dev tools (a dev run writes the English there)")
    differ = 0
    for guid, folder in plugins().items():
        src_dir = os.path.join(dumps, guid)
        if not os.path.isdir(src_dir):
            continue  # a plugin with no page, no mount and no skin
        dest_dir = os.path.join(folder, "Languages")
        rel = os.path.relpath(dest_dir, ROOT)
        for name in TEMPLATES:
            src = os.path.join(src_dir, name)
            if not os.path.isfile(src):
                continue
            dest = os.path.join(dest_dir, name)
            new = open(src, "rb").read()
            old = open(dest, "rb").read() if os.path.isfile(dest) else None
            # the same text whatever the line endings: git's core.autocrlf turns the committed file's LF into CRLF on checkout
            if old is not None and new.replace(b"\r\n", b"\n") == old.replace(b"\r\n", b"\n"):
                print(f"same     {rel}/{name}")
                continue
            differ += 1
            if name == "en.txt":
                before = set(keys_of(dest)) if old is not None else set()
                after = set(keys_of(src))
                print(f"{'differs' if check else 'copied ':8} {rel}/{name}: {len(after - before)} key(s) added, {len(before - after)} removed"
                      + (", text changed" if before == after else ""))
            else:
                print(f"{'differs' if check else 'copied ':8} {rel}/{name}")
            if not check:
                os.makedirs(dest_dir, exist_ok=True)
                shutil.copyfile(src, dest)
        # the translations' keys the English no longer has
        en = os.path.join(dest_dir, "en.txt")
        if os.path.isfile(en) and os.path.isdir(dest_dir):
            known = set(keys_of(en))
            for n in sorted(os.listdir(dest_dir)):
                if n in TEMPLATES or not n.lower().endswith((".txt", ".csv")):
                    continue
                p = os.path.join(dest_dir, n)
                ks = keys_of(p) if n.lower().endswith(".txt") else csv_keys(p)
                gone = [k for k in ks if k not in known]
                if gone:
                    print(f"note     {rel}/{n}: {len(gone)} key(s) the English no longer has (ignored by the mod): {', '.join(gone[:8])}{' ...' if len(gone) > 8 else ''}")
    said = literals() if check else []
    for where, line in said:
        print(f"literal  {where}: {line}")
    if check and differ:
        sys.exit(f"{differ} English template(s) differ from the last dev run's: run python -I tools/l10n-sync.py and commit them")
    if said:
        sys.exit(f"{len(said)} bubble(s) or prompt(s) said with a literal in the API's game code: make each a word of UiWords (docs/localization.md)")
    print("the English templates match the last dev run" if check else "done")


if __name__ == "__main__":
    main()
