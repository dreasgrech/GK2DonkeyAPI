"""The characters of our shipped translations that the game's own text in their language never draws.

    python -I tools/l10n-glyphs.py [--corpus DIR] [lang ...]

The game's strings tables (lng_<id>.tsv, one a language, extracted from the game earlier; docs/localization.md section
10) are read from --corpus or the GK2_L10N_CORPUS folder, never from a path in the repository. Our translations are
every Languages/<id>.csv of src/* and packs/*. A character the game never draws in a language may be missing from that
language's font: the Japanese and Chinese fonts hold more than the game's own characters, not every common one (the first
part names of the Donkey Skin Pack drew boxes for 摺 鍮 鞍 and 鞍 褥; docs/localization.md, "The skins' part names").
Each such character is listed with the keys that use it. The in-game Fonts check (F11, "translations") is the judge: a
listed character it found in the font is fine, and one it found missing is reworded with characters the table has.
Run it after adding or changing a CJK translation, before the in-game run. Exit 0 with or without findings (the
listing is for a person to read), 1 when the tables can't be read.
"""
import csv
import glob
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def corpus_dir(argv):
    if "--corpus" in argv:
        i = argv.index("--corpus")
        if i + 1 >= len(argv):
            sys.exit("--corpus needs a folder")
        d = argv[i + 1]
        del argv[i:i + 2]
        return d
    return os.environ.get("GK2_L10N_CORPUS", "")


def translations():
    """lang -> [(file, key, text)] of every shipped translation (not en)."""
    found = {}
    for pattern in ("src/*/Languages/*.csv", "packs/*/Languages/*.csv"):
        for path in sorted(glob.glob(os.path.join(REPO, pattern))):
            lang = os.path.splitext(os.path.basename(path))[0]
            if lang.lower() == "en":
                continue
            with open(path, encoding="utf-8-sig", newline="") as f:
                rows = list(csv.reader(f))
            if not rows or "key" not in rows[0] or "value" not in rows[0]:
                print(f"SKIP {os.path.relpath(path, REPO)}: no key,value header")
                continue
            k, v = rows[0].index("key"), rows[0].index("value")
            for r in rows[1:]:
                if len(r) > max(k, v) and r[v]:
                    found.setdefault(lang, []).append((os.path.relpath(path, REPO), r[k], r[v]))
    return found


def main():
    argv = sys.argv[1:]
    corpus = corpus_dir(argv)
    if not corpus or not os.path.isdir(corpus):
        print("the game's strings tables are needed: --corpus DIR or GK2_L10N_CORPUS (a folder of lng_<id>.tsv)")
        return 1
    langs = translations()
    wanted = argv or sorted(langs)
    failed = False
    for lang in wanted:
        if lang not in langs:
            print(f"{lang}: no shipped translation")
            continue
        table = os.path.join(corpus, f"lng_{lang}.tsv")
        if not os.path.isfile(table):
            print(f"{lang}: no {table}")
            failed = True
            continue
        with open(table, encoding="utf-8-sig") as f:
            game = set(f.read())
        users = {}
        for path, key, text in langs[lang]:
            for ch in text:
                if ch.isspace() or ch in game:
                    continue
                users.setdefault(ch, []).append(f"{path}: {key}")
        ours = {ch for _, _, text in langs[lang] for ch in text if not ch.isspace()}
        print(f"{lang}: {len(ours)} characters in {len(langs[lang])} lines, {len(users)} the game's own text never draws")
        for ch in sorted(users):
            where = users[ch]
            more = f" (+{len(where) - 3} more)" if len(where) > 3 else ""
            print(f"  {ch} U+{ord(ch):04X}: {'; '.join(where[:3])}{more}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
