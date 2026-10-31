"""Every line that carries a version number, checked or moved together (docs/release-checklist.md, section 6).

    python tools/release-versions.py check
    python tools/release-versions.py items           # which DLLs this release carries (raised) and which stay released (kept)
    python tools/release-versions.py set saddleup 0.13.2 --api 0.41.2
    python tools/release-versions.py set GK2SaddleUpDragonsMountPack 1.0.1

The release rule (`items`, used by build-dist.sh and make-release-zip.py): a DLL whose source version differs from the
one committed in dist/ is in this release ("raised"); one with the same version is a released build ("kept"):
build-dist.sh puts its committed bytes back and make-release-zip.py writes no zip for it. A released version is never
rebuilt; to ship a change, raise the version.

`check` (make-release-zip.py runs it first; exit 1 on any finding) reads the one place each version lives (the
plugin's `Version` constant in Plugin.cs) and verifies every other line that repeats it: the csproj, README.md's table
and log line, the two mods' READMEs, docs/api.md's version line and history entry, dist/README.md's rows, CHANGELOG.md's
first release heading, the Nexus note docs/nexus/saddleup-<version>-changelog.md, tools/rehearsal.sh's zip names, and
for each pack its csproj, its CHANGELOG's first heading and its dist/README.md row. It also checks the game build pair
on the lines that carry it. A note (not a failure) names a kept item whose Languages/ (its translation files,
docs/localization.md) differs from its released zip's: a translation added since ships only with a raised version.

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
    # a pack raised in this release needs its player note too (its Nexus file's text, and its Workshop change note)
    raised = {dll for dll, ver, committed in dll_items() if committed is not None and ver != committed}
    for pack in pack_dirs():
        if dll_of(pack) not in raised:
            continue
        v = constant(f"packs/{pack}/Plugin.cs")
        short = zip_name(pack, v)[len("GK2-Saddle-Up-"):-len(f"-{v}.zip")].lower()
        note = f"docs/nexus/{short}-{v}-changelog.md"
        if not os.path.isfile(os.path.join(ROOT, note)):
            bad += 1
            print(f"FAIL  {pack} {v} is in this release but its player note {note} is missing")
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
    # a released build whose translation files changed since: the release rule leaves it out until its version is raised
    for dll, ver, committed in dll_items():
        if committed is None or ver != committed:
            continue
        diff = languages_changed(dll)
        if diff:
            print(f"note  {dll} {ver} is kept (a released build), but its Languages/ differs from {diff[0]} ({diff[1]}): "
                  f"raise its version to ship them")
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


_dll_items = None


def dll_items():
    """[(dll name, its source version, the version of the dll committed in dist/ or None)] for the API, Saddle Up and each pack."""
    global _dll_items
    if _dll_items is not None:
        return _dll_items
    import subprocess, tempfile
    out = [("GK2DonkeyAPI", constant("src/GK2DonkeyAPI/Plugin.cs")), ("GK2SaddleUp", constant("src/SaddleUp/Plugin.cs"))]
    out += [(dll_of(p), constant(f"packs/{p}/Plugin.cs")) for p in pack_dirs()]
    res = []
    for dll, ver in out:
        blob = subprocess.run(["git", "-C", ROOT, "show", f"HEAD:dist/{dll}.dll"], capture_output=True)
        committed = None
        if blob.returncode == 0:
            with tempfile.TemporaryDirectory() as d:
                path = os.path.join(d, dll + ".dll")
                open(path, "wb").write(blob.stdout)
                v = subprocess.run(["pwsh", "-NoProfile", "-Command", f"[Reflection.AssemblyName]::GetAssemblyName('{path}').Version.ToString(3)"],
                                   capture_output=True, text=True)
                committed = v.stdout.strip() or None
        res.append((dll, ver, committed))
    _dll_items = res
    return res


def source_dir(dll):
    """A DLL's source folder: src/GK2DonkeyAPI, src/SaddleUp, or the pack whose csproj builds it."""
    if dll == "GK2DonkeyAPI":
        return os.path.join(ROOT, "src", "GK2DonkeyAPI")
    if dll == "GK2SaddleUp":
        return os.path.join(ROOT, "src", "SaddleUp")
    for pack in pack_dirs():
        if dll_of(pack) == dll:
            return os.path.join(PACKS, pack)
    return None


def languages_changed(dll):
    """For a released build: (its zip, what differs) when its source Languages/ isn't what that zip shipped, else None.
    Its zip is the one in release/ whose BepInEx/plugins/<dll>/<dll>.dll is the dll committed in dist/."""
    import subprocess, zipfile
    blob = subprocess.run(["git", "-C", ROOT, "show", f"HEAD:dist/{dll}.dll"], capture_output=True)
    if blob.returncode != 0:
        return None
    inner = f"BepInEx/plugins/{dll}/"
    released = None
    out_dir = os.path.join(ROOT, "release")
    for name in sorted(os.listdir(out_dir)) if os.path.isdir(out_dir) else []:
        if not name.endswith(".zip"):
            continue
        with zipfile.ZipFile(os.path.join(out_dir, name)) as z:
            if inner + dll + ".dll" in z.namelist() and z.read(inner + dll + ".dll") == blob.stdout:
                released = (name, {n[len(inner + "Languages/"):]: z.read(n).replace(b"\r\n", b"\n")
                                   for n in z.namelist() if n.startswith(inner + "Languages/") and not n.endswith("/")})
                break
    if released is None:
        return None
    src = source_dir(dll)
    folder = os.path.join(src, "Languages") if src else None
    now = {}
    if folder and os.path.isdir(folder):
        for n in sorted(os.listdir(folder)):
            if n.lower().endswith((".txt", ".csv")):
                now[n] = open(os.path.join(folder, n), "rb").read().replace(b"\r\n", b"\n")
    zip_name_, shipped = released
    added = [n for n in now if n not in shipped]
    changed = [n for n in now if n in shipped and now[n] != shipped[n]]
    gone = [n for n in shipped if n not in now]
    if not (added or changed or gone):
        return None
    what = "; ".join(x for x in (f"new {', '.join(added)}" if added else "", f"changed {', '.join(changed)}" if changed else "",
                                   f"gone {', '.join(gone)}" if gone else "") if x)
    return zip_name_, what


def items():
    """Which DLLs this release carries: raised = the source's version differs from the one committed in dist/ (a new
    release of it); kept = the same version, a released build (build-dist.sh puts the committed bytes back; no zip)."""
    for dll, ver, committed in dll_items():
        state = "new" if committed is None else ("raised" if ver != committed else "kept")
        print(f"{state:6} {dll} {ver}" + ("" if committed in (None, ver) else f" (dist/ has {committed})"))


def main():
    args = sys.argv[1:]
    if not args or args[0] not in ("check", "set", "items"):
        sys.exit(__doc__)
    if args[0] == "check":
        sys.exit(1 if check() else 0)
    if args[0] == "items":
        items()
        return
    if len(args) < 3:
        sys.exit(__doc__)
    api_new = args[args.index("--api") + 1] if "--api" in args else None
    if args[1].lower() == "saddleup":
        set_saddleup(args[2], api_new)
    else:
        set_pack(args[1], args[2])


if __name__ == "__main__":
    main()
