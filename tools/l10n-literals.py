"""The API's own translation check: no bubble or prompt said with a literal in its game code (docs/localization.md).

    python -I tools/l10n-literals.py

Looks through src/GK2DonkeyAPI (not Dev/) for Mount.Say("..."), HintText("..."), HintFor(key, "..."), a foot action's
Set(..., "...") (also @"...", $@"..." and a literal in a ?: inside Say( )). Those sentences are words of UiWords (the
code uses a word's English, Word.Make for a template), or they would never be translated. Exit 1 when one is found.

The rest of the translation tooling is the window library's and serves every mod that carries the windows
(lib/GK2DreasUI/tools/l10n-sync.py: the English templates and the window's words; lib/GK2DreasUI/tests/TranslationScan:
the reader's mistake catalogue and every shipped file). This check was part of tools/l10n-sync.py until 2026-11-17; it
names the API's own calls, so it stays here.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
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


def main():
    said = literals()
    for where, line in said:
        print(f"literal  {where}: {line}")
    if said:
        sys.exit(f"{len(said)} bubble(s) or prompt(s) said with a literal in the API's game code: make each a word of UiWords (docs/localization.md)")
    print("no bubble or prompt is said with a literal")


if __name__ == "__main__":
    main()
