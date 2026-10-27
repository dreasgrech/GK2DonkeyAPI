"""The layering rule (the user, 2026-10-27): GK2 Donkey API knows nothing of GK2 Saddle Up, and Saddle Up knows nothing of
its packs. Fails on any name of a higher layer in a lower one: code, strings and comments alike.

    python -I tools/layering-scan.py

- src/GK2DonkeyAPI (every .cs and .csproj, Dev/ included): no name of Saddle Up or of its content (its GUID, its
  assembly, its mounts and skins, its packs' ids). The API's in-game tests of those live in
  tools/GK2DonkeyAPI.IntegrationTests, a dev plugin the API knows only by that name (Dev/AssemblyInfo.cs).
- src/SaddleUp (every .cs and .csproj): no name of its packs (the Dragon, the Critters, the Donkey Skins, their mounts,
  skins and ids). Gerry is Saddle Up's own.
- the built API DLL (src/GK2DonkeyAPI/bin/Release, or dist/ when given --dist): none of the API's names in its strings
  (UTF-16, as .NET stores them).
An exception is a line in ALLOWED: the file, the text, and why.
"""
import os, re, sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
API_TERMS = [r"saddle ?up", r"\bgerry\b", r"hellfire", r"royal[ -]parade", r"critters", r"\bdragons?\b", r"\bsnails?\b",
             r"\bworms?\b", r"\brats?\b", r"\bskull\b", r"donkey ?skins"]
SADDLEUP_TERMS = [r"\bdragons?\b", r"critters", r"\bsnails?\b", r"\bworms?\b", r"\brats?\b", r"hellfire", r"royal[ -]parade",
                  r"donkey ?skins", r"saddleup-"]
ALLOWED = [
    # (path relative to the repo, a text the line contains, why)
    ("src/GK2DonkeyAPI/LooksGame.cs", "docs/gerry-mount.md", "a doc's file name (the investigation of mounts drawn from the game's own characters)"),
]
DLL_TERMS = ["Saddle Up", "saddleup", "Hellfire", "hellfire", "Gerry", "dragon", "critters", "royal-parade"]


def allowed(rel, line):
    return any(rel == f and text in line for f, text, _ in ALLOWED)


def scan(root, terms):
    hits = []
    rx = re.compile("|".join(terms), re.IGNORECASE)
    for dirpath, dirs, files in os.walk(os.path.join(REPO, root)):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
        for f in files:
            if not f.endswith((".cs", ".csproj")):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, REPO).replace("\\", "/")
            for n, line in enumerate(open(p, encoding="utf-8-sig").read().splitlines(), 1):
                m = rx.search(line)
                if m and not allowed(rel, line):
                    hits.append(f"{rel}:{n}: '{m.group(0)}': {line.strip()[:140]}")
    return hits


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # a test's look-alike letters (a Cyrillic a in an id)
    bad = 0
    for root, terms, what in (("src/GK2DonkeyAPI", API_TERMS, "the API names Saddle Up or its content"),
                              ("src/SaddleUp", SADDLEUP_TERMS, "Saddle Up names a pack or its content")):
        hits = scan(root, terms)
        for h in hits:
            print("FAIL", what + ":", h)
        bad += len(hits)
        if not hits:
            print("ok  ", root, "names no higher layer")
    dll = os.path.join(REPO, "dist" if "--dist" in sys.argv else "src/GK2DonkeyAPI/bin/Release/netstandard2.1", "GK2DonkeyAPI.dll")
    if os.path.exists(dll):
        b = open(dll, "rb").read()
        found = [t for t in DLL_TERMS if t.encode("utf-16-le") in b or t.encode() in b]
        if found:
            print("FAIL", os.path.relpath(dll, REPO), "holds", ", ".join(found))
            bad += 1
        else:
            print("ok  ", os.path.relpath(dll, REPO).replace("\\", "/"), "holds none of the names")
    else:
        print("FAIL", dll, "not built")
        bad += 1
    print("LAYERING OK" if bad == 0 else f"{bad} finding(s)")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
