"""Every line that carries a version number, checked or moved together (docs/release-checklist.md, section 6).

    python tools/release-versions.py check
    python tools/release-versions.py set saddleup 0.13.2 --api 0.41.2
    python tools/release-versions.py set GK2SaddleUpDragonsMountPack 1.0.1

`check` (make-release-zip.py runs it first; exit 1 on any finding) reads the one place each version lives (the
plugin's `Version` constant in Plugin.cs) and verifies every other line that repeats it: the csproj, README.md's table
and log line, the two mods' READMEs, docs/api.md's version line and history entry, dist/README.md's rows, CHANGELOG.md's
first release heading, the Nexus note docs/nexus/saddleup-<version>-changelog.md, tools/rehearsal.sh's zip names, and
for each pack its csproj, its CHANGELOG's first heading and its dist/README.md row. It also checks the game build pair
on the lines that carry it.

`set` rewrites the mechanical lines (the constants, the csproj, the READMEs, docs/api.md's version line, dist/README.md's
rows, rehearsal.sh); the editorial ones stay yours and `check` then names what is missing: the CHANGELOG heading (the
Unreleased block split into the release and what stays), docs/api.md's history entry, the Nexus note.
Every edit matches exactly once; line endings are kept.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME_BUILD, GAME_VERSION = "26297960", "1.011"
PACKS = os.path.join(ROOT, "packs")


def read(rel):
    return open(os.path.join(ROOT, rel), "rb").read().decode("utf-8").replace("\r\n", "\n")


def write(rel, text):
    p = os.path.join(ROOT, rel)
    nl = "\r\n" if b"\r\n" in open(p, "rb").read() else "\n"
    open(p, "wb").write(text.replace("\n", nl).encode("utf-8"))


def constant(rel):
    m = re.search(r'public const string Version = "([0-9.]+)"', read(rel))
    if not m:
        sys.exit(f"no Version constant in {rel}")
    return m.group(1)


def pack_dirs():
    return sorted(d for d in os.listdir(PACKS) if os.path.isfile(os.path.join(PACKS, d, d + ".csproj"))) if os.path.isdir(PACKS) else []


def dll_of(pack):
    text = read(f"packs/{pack}/{pack}.csproj")
    m = re.search(r"<AssemblyName>([^<]+)</AssemblyName>", text)
    return m.group(1).strip() if m else pack


def zip_name(pack, ver):
    short = pack[len("GK2SaddleUp"):] if pack.startswith("GK2SaddleUp") and len(pack) > len("GK2SaddleUp") else pack
    return f"GK2-Saddle-Up-{re.sub(r'(?<=[a-z0-9])(?=[A-Z])', '-', short)}-{ver}.zip"


def expectations():
    """[(file, regex that must match exactly once, what it is)] for the current constants."""
    api, su = constant("src/GK2DonkeyAPI/Plugin.cs"), constant("src/SaddleUp/Plugin.cs")
    e = re.escape
    want = [
        ("src/GK2DonkeyAPI/GK2DonkeyAPI.csproj", rf"<Version>{e(api)}</Version>", "the API csproj"),
        ("src/SaddleUp/SaddleUp.csproj", rf"<Version>{e(su)}</Version>", "Saddle Up's csproj"),
        ("README.md", rf"\| \[GK2 Saddle Up\]\(src/SaddleUp/README\.md\) \| {e(su)} \|", "README.md's table, Saddle Up"),
        ("README.md", rf"\| \[GK2 Donkey API\]\(src/GK2DonkeyAPI/README\.md\) \| {e(api)} \|", "README.md's table, the API"),
        ("README.md", rf"says `GK2 Donkey API {e(api)} loaded` and `GK2 Saddle Up {e(su)}\n", "README.md's install step, the log line"),
        ("src/GK2DonkeyAPI/README.md", rf"Version {e(api)} \(one number", "the API README's version line"),
        ("src/GK2DonkeyAPI/README.md", rf"the log says `GK2 Donkey API {e(api)} loaded;", "the API README's log line"),
        ("src/GK2DonkeyAPI/README.md", rf"against Steam game build {e(GAME_BUILD)}, game {e(GAME_VERSION)}", "the API README's game build"),
        ("src/SaddleUp/README.md", rf"Version {e(su)}, for GK2 Donkey API {e(api)} or newer, and Steam game build {e(GAME_BUILD)} \(game\n{e(GAME_VERSION)}\)", "Saddle Up's README version line (with the game build and version)"),
        ("docs/api.md", rf"API version \*\*{e(api)}\*\* \(", "docs/api.md's version line"),
        ("docs/api.md", rf"(?m)^- {e(api)} \(", "docs/api.md's history entry for this API version (editorial: write it)"),
        ("dist/README.md", rf"\| `GK2DonkeyAPI\.dll` \| {e(api)} \(with Saddle Up {e(su)};", "dist/README.md's API row"),
        ("dist/README.md", rf"\| `GK2SaddleUp\.dll` \| {e(su)} \(needs GK2 Donkey API {e(api)} or newer;", "dist/README.md's Saddle Up row"),
        ("CHANGELOG.md", rf"(?m)^## {e(su)} \(GK2 Donkey API {e(api)}\), \d{{4}}-\d{{2}}-\d{{2}}$", "CHANGELOG.md's release heading (editorial: split Unreleased)"),
        ("tools/rehearsal.sh", rf'SADDLEUP_ZIP="GK2-Saddle-Up-{e(su)}\.zip"', "rehearsal.sh's Saddle Up zip"),
    ]
    for pack in pack_dirs():
        v = constant(f"packs/{pack}/Plugin.cs")
        dll = dll_of(pack)
        want += [
            (f"packs/{pack}/{pack}.csproj", rf"<Version>{e(v)}</Version>", f"{pack}'s csproj"),
            (f"packs/{pack}/CHANGELOG.md", rf"(?m)^## {e(v)}, \d{{4}}-\d{{2}}-\d{{2}}$", f"{pack}'s CHANGELOG release heading (editorial)"),
            ("dist/README.md", rf"\| `{e(dll)}\.dll` \| {e(v)} \(", f"dist/README.md's {pack} row"),
            ("tools/rehearsal.sh", rf"{e(zip_name(pack, v))}", f"rehearsal.sh's {pack} zip"),
        ]
    return api, su, want


def check():
    api, su, want = expectations()
    bad = 0
    print(f"GK2 Donkey API {api}, GK2 Saddle Up {su}, " + ", ".join(f"{p} {constant(f'packs/{p}/Plugin.cs')}" for p in pack_dirs()))
    for rel, pattern, what in want:
        n = len(re.findall(pattern, read(rel)))
        if n != 1:
            bad += 1
            print(f"FAIL  {what}: {rel} has {n} match(es) of the expected line")
    nexus = f"docs/nexus/saddleup-{su}-changelog.md"
    if not os.path.isfile(os.path.join(ROOT, nexus)):
        bad += 1
        print(f"FAIL  the Nexus note {nexus} is missing (editorial: the simplified player text of this release)")
    # a pack's CHANGELOG first versioned heading must be its version (an Unreleased block above it is fine)
    for pack in pack_dirs():
        v = constant(f"packs/{pack}/Plugin.cs")
        m = re.search(r"(?m)^## ([0-9]+\.[0-9]+\.[0-9]+)", read(f"packs/{pack}/CHANGELOG.md"))
        if not m or m.group(1) != v:
            bad += 1
            print(f"FAIL  {pack}: Plugin.cs says {v}, its CHANGELOG's first release heading says {m.group(1) if m else 'nothing'}")
    m = re.search(r"(?m)^## ([0-9]+\.[0-9]+\.[0-9]+) \(GK2 Donkey API ([0-9]+\.[0-9]+\.[0-9]+)\)", read("CHANGELOG.md"))
    if m and (m.group(1), m.group(2)) != (su, api):
        bad += 1
        print(f"FAIL  CHANGELOG.md's first release heading is {m.group(1)} / {m.group(2)}, the constants say {su} / {api}")
    print("ok    every version line agrees" if not bad else f"{bad} finding(s)")
    return bad


def sub1(rel, pattern, repl, what):
    text = read(rel)
    n = len(re.findall(pattern, text))
    if n != 1:
        sys.exit(f"{what}: {rel} has {n} match(es), expected one; nothing written")
    write(rel, re.sub(pattern, repl, text))
    print(f"set   {what} ({rel})")


def set_saddleup(su_new, api_new):
    api, su = constant("src/GK2DonkeyAPI/Plugin.cs"), constant("src/SaddleUp/Plugin.cs")
    if api_new is None:
        sys.exit("set saddleup needs --api <version>")
    e = re.escape
    sub1("src/SaddleUp/Plugin.cs", rf'public const string Version = "{e(su)}";', f'public const string Version = "{su_new}";', "Saddle Up's constant")
    sub1("src/SaddleUp/SaddleUp.csproj", rf"<Version>{e(su)}</Version>", f"<Version>{su_new}</Version>", "Saddle Up's csproj")
    sub1("src/GK2DonkeyAPI/Plugin.cs", rf'public const string Version = "{e(api)}";', f'public const string Version = "{api_new}";', "the API's constant")
    sub1("src/GK2DonkeyAPI/GK2DonkeyAPI.csproj", rf"<Version>{e(api)}</Version>", f"<Version>{api_new}</Version>", "the API's csproj")
    sub1("README.md", rf"\| \[GK2 Saddle Up\]\(src/SaddleUp/README\.md\) \| {e(su)} \|", f"| [GK2 Saddle Up](src/SaddleUp/README.md) | {su_new} |", "README.md's table, Saddle Up")
    sub1("README.md", rf"\| \[GK2 Donkey API\]\(src/GK2DonkeyAPI/README\.md\) \| {e(api)} \|", f"| [GK2 Donkey API](src/GK2DonkeyAPI/README.md) | {api_new} |", "README.md's table, the API")
    sub1("README.md", rf"says `GK2 Donkey API {e(api)} loaded` and `GK2 Saddle Up {e(su)}\n", f"says `GK2 Donkey API {api_new} loaded` and `GK2 Saddle Up {su_new}\n", "README.md's log line")
    sub1("src/GK2DonkeyAPI/README.md", rf"Version {e(api)} \(one number", f"Version {api_new} (one number", "the API README's version line")
    sub1("src/GK2DonkeyAPI/README.md", rf"the log says `GK2 Donkey API {e(api)} loaded;", f"the log says `GK2 Donkey API {api_new} loaded;", "the API README's log line")
    sub1("src/SaddleUp/README.md", rf"Version {e(su)}, for GK2 Donkey API {e(api)} or newer, and Steam game build {e(GAME_BUILD)} \(game\n[0-9.]+\)",
         f"Version {su_new}, for GK2 Donkey API {api_new} or newer, and Steam game build {GAME_BUILD} (game\n{GAME_VERSION})", "Saddle Up's README version line")
    sub1("docs/api.md", rf"API version \*\*{e(api)}\*\* \(", f"API version **{api_new}** (", "docs/api.md's version line")
    sub1("dist/README.md", rf"\| `GK2DonkeyAPI\.dll` \| {e(api)} \(with Saddle Up {e(su)};", f"| `GK2DonkeyAPI.dll` | {api_new} (with Saddle Up {su_new};", "dist/README.md's API row")
    sub1("dist/README.md", rf"\| `GK2SaddleUp\.dll` \| {e(su)} \(needs GK2 Donkey API {e(api)} or newer;", f"| `GK2SaddleUp.dll` | {su_new} (needs GK2 Donkey API {api_new} or newer;", "dist/README.md's Saddle Up row")
    sub1("tools/rehearsal.sh", r'SADDLEUP_ZIP="GK2-Saddle-Up-[0-9.]+\.zip"', f'SADDLEUP_ZIP="GK2-Saddle-Up-{su_new}.zip"', "rehearsal.sh's Saddle Up zip")
    print(f"\nStill yours: CHANGELOG.md's heading '## {su_new} (GK2 Donkey API {api_new}), <date>' (split Unreleased), docs/api.md's '- {api_new} (<date>): ...' history entry, docs/nexus/saddleup-{su_new}-changelog.md, dist/README.md's dates and sizes. Then: python tools/release-versions.py check")


def set_pack(pack, v_new):
    if pack not in pack_dirs():
        sys.exit(f"no pack {pack} (one of {', '.join(pack_dirs())})")
    v = constant(f"packs/{pack}/Plugin.cs")
    e = re.escape
    dll = dll_of(pack)
    sub1(f"packs/{pack}/Plugin.cs", rf'public const string Version = "{e(v)}";', f'public const string Version = "{v_new}";', f"{pack}'s constant")
    sub1(f"packs/{pack}/{pack}.csproj", rf"<Version>{e(v)}</Version>", f"<Version>{v_new}</Version>", f"{pack}'s csproj")
    sub1("dist/README.md", rf"\| `{e(dll)}\.dll` \| {e(v)} \(", f"| `{dll}.dll` | {v_new} (", f"dist/README.md's {pack} row")
    sub1("tools/rehearsal.sh", e(zip_name(pack, v)), zip_name(pack, v_new), f"rehearsal.sh's {pack} zip")
    print(f"\nStill yours: packs/{pack}/CHANGELOG.md's heading '## {v_new}, <date>' (from its Unreleased), dist/README.md's note on the row. Then: python tools/release-versions.py check")


def main():
    args = sys.argv[1:]
    if not args or args[0] not in ("check", "set"):
        sys.exit(__doc__)
    if args[0] == "check":
        sys.exit(1 if check() else 0)
    if len(args) < 3:
        sys.exit(__doc__)
    api_new = args[args.index("--api") + 1] if "--api" in args else None
    if args[1].lower() == "saddleup":
        set_saddleup(args[2], api_new)
    else:
        set_pack(args[1], args[2])


if __name__ == "__main__":
    main()
