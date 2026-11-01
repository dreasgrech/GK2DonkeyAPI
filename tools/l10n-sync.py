"""The translators' English templates, from a dev run into the repository (docs/localization.md, "Built").

    python -I tools/l10n-sync.py           # copy BepInEx/GK2-l10n/<guid>/en.csv into each plugin's Languages/
    python -I tools/l10n-sync.py --check   # copy nothing; exit 1 when the repository's differ from the last dev run's,
                                           # or when the API's game code says a sentence of its own (below)

A dev run (the dev tools unlocked) writes each plugin's English when a settings page is shown: a page's rows, its
sections and title, the API's own words (UiWords: the windows, the keeper's bubbles, the prompts on foot), and the
names and descriptions of the mounts and skins each mod or pack registered (Texts.WriteEnglish, Texts.Owners). The code is the source of the English; these
files are what translators copy (en.csv, saved as de.csv and so on: CSV only since 2026-11-01), shipped beside the DLL
(tools/make-release-zip.py, DeployToGame.targets). A plugin's GUID is read from its Plugin.cs (Guid constant) to find
its source folder: src/GK2DonkeyAPI, src/SaddleUp, packs/<Pack>. Also reports, for every translation in a Languages/
folder, the keys the English no longer has (left over after a rename: harmless, the game ignores them). Line endings
don't count in the comparison (git's core.autocrlf changes them on checkout).

Each shipped translation (Languages/<id>.csv) is reported: how many lines are translated, missing, the same as the
English, without their English (the mod leaves those out) or have English that changed since, and any whose {0}, {1}
don't match the English (--check fails on those). A file is read as the mod reads it (TranslationFiles): the separator
after the first cell of its first rows, the columns by its header's names, keys in any case, a text cleaned the same way.

--check also looks for a bubble or a prompt said with a literal in the API's game code (src/GK2DonkeyAPI, not Dev/):
Mount.Say("..."), HintText("..."), HintFor(key, "..."), a foot action's Set(..., "...") (also @"...", $@"..." and a
literal in a ?: inside Say( )). Those sentences are words of
UiWords (the code uses a word's English, Word.Make for a template), or they would never be translated.
"""
import os
import re
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEMPLATES = ("en.csv",)


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
    re.compile(r'\bSay\(\s*[$@]{0,2}"'),
    re.compile(r'\bSay\([^;"]*[?:]\s*[$@]{0,2}"'),
    re.compile(r'\bHintText\(\s*[$@]{0,2}"'),
    re.compile(r'\bHintText\([^;"]*[?:]\s*[$@]{0,2}"'),
    re.compile(r'\bHintFor\([^,()]+,\s*[$@]{0,2}"'),
    re.compile(r'\bSet\([^,()]+,\s*FootAction\.\w+,\s*[$@]{0,2}"'),
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


def csv_keys(path):
    """The keys of a translation CSV, lower case (the mod's keys are any case), as the mod reads them (csv_rows)."""
    return [row[0] for row in csv_rows(path)]


INVISIBLE = "\ufeff\u200b\u200c\u200d\u2060\u00ad"


def clean(s):
    """A text as the mod cleans it (TranslationFiles.Clean): invisible characters off its ends, a literal \\n a line
    break, a tab a space, <nobr> out, { 0 } and the full-width ｛０｝ as {0}."""
    s = (s or "").strip(" \t\r\n\u00a0" + INVISIBLE).replace("\\n", "\n").replace("\t", " ").replace("\r", "")
    s = s.replace("<nobr>", "").replace("</nobr>", "").strip(" \t\r\n\u00a0" + INVISIBLE)
    s = re.sub(r"\{\{( *[0-9][0-9 ]*)\}\}", r"{\1}", s)  # {{0}}, other tools' habit; {{ }} kept as written
    digits = {ord(c): ord("0") + i for i, c in enumerate("０１２３４５６７８９")}
    # inside braces only, as the mod does: full-width digits, a letter O, spaces
    return re.sub(r"[{｛][ \u3000]*([0-9Oo０-９]+)[ \u3000]*[}｝]", lambda m: "{" + m.group(1).translate(digits).replace("O", "0").replace("o", "0") + "}", s)


def sniff(lines):
    """The separator after the first cell of the first rows (comments and blank lines skipped), as the mod sniffs it."""
    votes = {",": 0, ";": 0, "\t": 0}
    looked = 0
    for line in lines:
        s = line.lstrip(" " + INVISIBLE)
        if not s or s.startswith("#"):
            continue
        looked += 1
        if looked > 20:
            break
        i = 0
        if s.startswith('"'):
            i = 1
            while i < len(s):
                if s[i] == '"':
                    if i + 1 < len(s) and s[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
        for c in s[i:]:
            if c in votes:
                votes[c] += 1
                break
    if votes["\t"] and votes["\t"] >= votes[";"] and votes["\t"] >= votes[","]:
        return "\t"
    return ";" if votes[";"] and votes[";"] >= votes[","] else ","


def columns(header):
    """(value, english) column numbers by a header's names, as the mod maps them (TranslationFiles.Columns): en.csv's own
    names only (key, value, english, note); value -1 when the header doesn't name value and english once each, or names
    the english first (the mod doesn't read the section)."""
    names = [norm(h) for h in header]
    value = [i for i, h in enumerate(names[1:], 1) if h == "value"]
    english = [i for i, h in enumerate(names[1:], 1) if h == "english"]
    if len(value) != 1 or len(english) != 1 or english[0] < value[0]:
        return -1, -1
    return value[0], english[0]


def norm(h):
    """A header cell's name as the mod reads it (TranslationFiles.HeaderName): invisible characters out, whitespace off
    its ends, the Turkish I folded, lower case."""
    h = "".join(c for c in h if c not in INVISIBLE).strip()
    return h.replace("İ", "i").replace("ı", "i").lower()


INDEX_NAMES = ("", "index", "#", "line", "no", "nr", "id", "n°", "nº")


def is_key(k, known=None):
    """A key as the mod tells one (TranslationFiles.IsKey): one the owner has, else a dotted word without spaces that
    starts with a letter."""
    return bool(k) and ((known is not None and k.lower() in known) or ("." in k and k[0].isalpha() and not any(c.isspace() for c in k)))


def damaged(row, known):
    """A key of the owner's damaged by one keystroke, as the mod tells one (TranslationFiles.Damaged)."""
    k0 = "".join(c for c in row[0] if c not in INVISIBLE).strip().lower()
    if not k0:
        return False
    word = re.split(r"[\s=:]", k0, maxsplit=1)[0]
    if word != k0 and is_key(word, known):
        return True  # a line typed with spaces for its separators, or as key = value
    if not known:
        return False
    if len(row) > 1:
        k1 = "".join(c for c in row[1] if c not in INVISIBLE).strip().lower()
        if k1 and (k0 + k1 in known or k0 + "." + k1 in known):
            return True
    tight = "".join(c for c in k0 if not c.isspace())
    if tight != k0 and tight in known:
        return True
    if len(tight) > 64 or "." in tight:
        return False
    return any(tight[:i] + "." + tight[i:] in known for i in range(1, len(tight)))


def foreign(row, english_at, english_texts, known=None):
    """A line under a header that is none of the mod's, as the mod tells one (TranslationFiles.Foreign): it stops the
    reading up to en.csv's header row."""
    if not any(clean(c) for c in row):
        return False
    k0 = "".join(c for c in row[0] if c not in INVISIBLE).strip()
    if is_key(k0, known) or damaged(row, known):
        return False
    if (not k0 or k0.isdigit()) and len(row) > 1 and is_key("".join(c for c in row[1] if c not in INVISIBLE).strip(), known):
        return False
    return not (english_texts and 0 <= english_at < len(row) and clean(row[english_at]) in english_texts)


def csv_rows(path, english_texts=None, known=None):
    """A translation CSV's rows as [key (lower case), value, english, note] the way the mod reads them: Excel's sep= line
    or the separator sniffed from the first rows, read from en.csv's header row on (a file without it: no row), the
    columns by its names, en.csv's own only, an index column before the key dropped, a header in other words under it
    (english_texts and known: the owner's English and keys, as the mod tells one), a row with text past the header's
    cells left out, # comments and rows without a key left out, every text cleaned as the mod cleans it; english "" when
    it has none. The quotes' and the texts' own checks (a quote not doubled, garbled letters, control characters) are
    TranslationScan's: release-scans.sh runs it on every shipped file with the mod's own reader."""
    import csv
    import io
    text = open(path, encoding="utf-8-sig").read()
    lines = text.split("\n")
    if lines and lines[0].lower().startswith("sep=") and len(lines[0].rstrip("\r")) >= 5:
        sep = lines[0][4]  # not stripped: the separator may be a tab
        text = text.split("\n", 1)[1] if "\n" in text else ""
    else:
        sep = sniff(lines)
    value = english = note = -1  # nothing read up to en.csv's header row
    headed, index, width = False, False, 0
    rows = []
    for row in csv.reader(io.StringIO(text), delimiter=sep):
        if not row:
            continue
        if len(row) == 1 and any(c in row[0] for c in ",;\t"):
            # a whole row in one cell (Excel in a ; country opening a , file): split again, as the mod does
            again = next(csv.reader([row[0]], delimiter=sniff([row[0]])), row)
            row = again if len(again) >= 2 else row
        # an index column before the key (its header's first cell empty, "index", "#"...): dropped up to a header without it
        drop = index
        if not index and len(row) > 2 and norm(row[1]) == "key" and (norm(row[0]) in INDEX_NAMES or norm(row[0]).startswith("unnamed")):
            index = drop = True
        elif index and norm(row[0]) == "key":
            index = drop = False
        # (row by row: a row without its index keeps its key)
        if drop and len(row) > 1 and (not is_key(norm(row[0]), known) or is_key(norm(row[1]), known)):
            row = row[1:]
        key = row[0].strip().strip(INVISIBLE).strip()
        if key.lower() == "key":
            value, english = columns(row)
            note = next((i for i, h in enumerate(row) if norm(h) == "note"), -1)
            headed, width = True, len(row)
            continue
        if key.startswith("#") or key.lower().startswith("sep="):
            continue
        if not headed:
            continue  # before en.csv's header row: the mod reads nothing
        if foreign(row, english, english_texts, known):
            value = -1  # none of the mod's lines (a header in other words, a comment without #): the mod stops reading
            continue
        if not key or value < 0:
            continue  # no key, or a section the mod doesn't read
        if any(clean(c) for c in row[width:]):
            continue  # text past the header's cells: the mod leaves the line out
        cell = lambda i: clean(row[i]) if 0 <= i < len(row) else ""
        rows.append([key.lower(), cell(value), cell(english), row[note].strip() if 0 <= note < len(row) else ""])
    return rows


def placeholders(s):
    return sorted(set(re.findall(r"\{\d+\}", s or "")))


def main():
    check = "--check" in sys.argv[1:]
    dumps = os.path.join(game_dir(), "BepInEx", "GK2-l10n")
    if not os.path.isdir(dumps):
        sys.exit(f"{dumps} doesn't exist: start the game once with the dev tools (a dev run writes the English there)")
    differ = 0
    broken_total = 0
    left_out_total = 0  # shipped lines the mod leaves out: without their English, or of a key the English hasn't
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
            if name == "en.csv":
                before = set(csv_keys(dest)) if old is not None else set()
                after = set(csv_keys(src))
                print(f"{'differs' if check else 'copied ':8} {rel}/{name}: {len(after - before)} key(s) added, {len(before - after)} removed"
                      + (", text changed" if before == after else ""))
            else:
                print(f"{'differs' if check else 'copied ':8} {rel}/{name}")
            if not check:
                os.makedirs(dest_dir, exist_ok=True)
                shutil.copyfile(src, dest)
        # each shipped translation against the English: translated, changed since, missing, keys gone, broken templates
        en = os.path.join(dest_dir, "en.csv")
        if os.path.isfile(en) and os.path.isdir(dest_dir):
            english = {r[0]: (r[2], r[3]) for r in csv_rows(en)}
            known = set(english)
            for n in sorted(os.listdir(dest_dir)):
                if n.lower().endswith(".txt"):
                    print(f"note     {rel}/{n}: not read (translations are CSV only since 2026-11-01): make it {n[:-4]}.csv")
                    continue
                if n in TEMPLATES or not n.lower().endswith(".csv"):
                    continue
                p = os.path.join(dest_dir, n)
                # a key twice: the current one counts, as the mod keeps it (Texts.Take); else the first
                rows, at = [], {}
                for r in csv_rows(p, {e for e, _ in english.values()}, known):
                    if r[0] not in at:
                        at[r[0]] = len(rows)
                        rows.append(r)
                    elif r[0] in english and r[2] == english[r[0]][0] and rows[at[r[0]]][2] != english[r[0]][0]:
                        rows[at[r[0]]] = r
                ks = [r[0] for r in rows]
                gone = [k for k in ks if k not in known]
                if gone:
                    print(f"note     {rel}/{n}: {len(gone)} key(s) the English no longer has (ignored by the mod): {', '.join(gone[:8])}{' ...' if len(gone) > 8 else ''}")
                done, stale, broken, same, unsaid = 0, [], [], 0, []
                for r in rows:
                    if r[0] not in english or not r[1]:
                        continue
                    eng, note = english[r[0]]
                    if not r[2]:
                        unsaid.append(r[0])  # the mod leaves a line without its English out
                        continue
                    if r[2] != eng:
                        stale.append(r[0])
                        continue
                    if r[1] == eng:
                        same += 1  # still the English (or the same word in the language)
                        continue
                    need, have = placeholders(eng), placeholders(r[1])
                    if set(have) - set(need) or (set(need) - set(have) and "may leave it out" not in note):
                        broken.append(f"{r[0]} ({' '.join(need) or 'none'} in English, {' '.join(have) or 'none'} here)")
                        continue
                    done += 1
                missing = len(known) - done - same - len(unsaid) - len(stale) - len(broken)
                print(f"lang     {rel}/{n}: {done} of {len(known)} translated, {missing} missing, {same} the same as the English, {len(stale)} with English that changed since"
                      + (f", {len(unsaid)} WITHOUT their English (left out by the mod: {', '.join(unsaid[:6])}{' ...' if len(unsaid) > 6 else ''})" if unsaid else "")
                      + (f" ({', '.join(stale[:6])}{' ...' if len(stale) > 6 else ''})" if stale else "")
                      + (f"; BROKEN templates (the mod shows the English): {'; '.join(broken)}" if broken else ""))
                broken_total += len(broken)
                left_out_total += len(unsaid) + len(gone)
    said = literals() if check else []
    for where, line in said:
        print(f"literal  {where}: {line}")
    if check and differ:
        sys.exit(f"{differ} English template(s) differ from the last dev run's: run python -I tools/l10n-sync.py and commit them")
    if check and left_out_total:
        sys.exit(f"{left_out_total} shipped line(s) the mod leaves out (without their English, or of a key the English no longer has): fix them in the files named above")
    if check and broken_total:
        sys.exit(f"{broken_total} translated line(s) whose {{0}} don't match their English: fix them in the file named above")
    if said:
        sys.exit(f"{len(said)} bubble(s) or prompt(s) said with a literal in the API's game code: make each a word of UiWords (docs/localization.md)")
    print("the English templates match the last dev run" if check else "done")


if __name__ == "__main__":
    main()
