"""The release zips for Nexus (docs/release-checklist.md, section 3).

    python tools/make-release-zip.py

Besides Saddle Up's zip: one per external mount pack, release/GK2-Saddle-Up-<Pack without SaddleUp>-<version>.zip
(GK2-Saddle-Up-Dragon-1.0.0.zip) holding BepInEx/plugins/GK2<Pack>/GK2<Pack>.dll (the csproj's AssemblyName) (from dist/: its gk2mounts/ folder
embedded, so an update replaces everything) with the pack's README.md and CHANGELOG.md beside it (the version is its
Plugin.cs Version), and release/GK2-Saddle-Up-All-Mounts-<Saddle Up's version>.zip holding every pack, each in its own
folder (a mount lives in exactly one pack: the bundle is the packs side by side). Every zip is read back and each file
compared with its source byte for byte.

Runs tools/check-dist.py first and stops if it fails. Then writes release/GK2-Saddle-Up-<version>.zip holding
BepInEx/plugins/GK2DonkeyAPI/GK2DonkeyAPI.dll and BepInEx/plugins/GK2SaddleUp/GK2SaddleUp.dll from dist/ (a folder per
mod, as other mods do; BepInEx finds plugins and their references in subfolders; Saddle Up's built-in Gerry is inside its
DLL; the other mounts are packs of their own, below), so the zip extracts straight into the game folder (and mod managers see the BepInEx layout). The versions are
read from the plugins' sources; the zip is read back and every DLL compared with dist/ byte for byte.
"""
import hashlib
import os
import re
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(ROOT, "dist")
OUT = os.path.join(ROOT, "release")
FILES = ["GK2DonkeyAPI.dll", "GK2SaddleUp.dll"]
INNER = "BepInEx/plugins/"


def inner(f):
    return INNER + f[:-4] + "/" + f  # BepInEx/plugins/<mod>/<mod>.dll


def version(rel):
    src = open(os.path.join(ROOT, rel), encoding="utf-8").read()
    m = re.search(r'public const string Version = "([0-9.]+)"', src)
    if not m:
        sys.exit(f"no Version constant in {rel}")
    return m.group(1)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_zip(path, files, what):
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        # the folders as entries of their own: some extractors and managers want them
        z.writestr(zipfile.ZipInfo("BepInEx/"), b"")
        z.writestr(zipfile.ZipInfo(INNER), b"")
        for f in files:
            z.writestr(zipfile.ZipInfo(INNER + f[:-4] + "/"), b"")
            z.write(os.path.join(DIST, f), inner(f))
    with zipfile.ZipFile(path) as z:
        if z.testzip() is not None:
            sys.exit("the zip reads back broken")
        names = z.namelist()
        want = ["BepInEx/", INNER]
        for f in files:
            want += [INNER + f[:-4] + "/", inner(f)]
        if names != want:
            sys.exit(f"unexpected entries: {names}")
        for f in files:
            if sha(z.read(inner(f))) != sha(open(os.path.join(DIST, f), "rb").read()):
                sys.exit(f"{f} in the zip differs from dist/")
    print(f"{path}: {what}, {os.path.getsize(path):,} bytes")
    for n in names:
        print("  " + n)


PACKS = os.path.join(ROOT, "packs")
STRAY = ("thumbs.db", "desktop.ini", ".ds_store")


def stray(name):
    n = name.lower()
    return n in STRAY or n.startswith("~") or n.endswith((".un~", ".bak", ".tmp", ".swp"))


def pack_version(pack):
    return version(f"packs/{pack}/Plugin.cs")  # check-dist.py: the same as its CHANGELOG.md's first heading


def assembly_of(pack_dir):
    """A pack's DLL name (without .dll): its csproj's AssemblyName (GK2SaddleUpDragon for packs/SaddleUpDragon)."""
    name = os.path.basename(pack_dir.rstrip("/\\"))
    try:
        text = open(os.path.join(pack_dir, name + ".csproj"), encoding="utf-8").read()
        i = text.index("<AssemblyName>") + len("<AssemblyName>")
        return text[i:text.index("</AssemblyName>", i)].strip()
    except (OSError, ValueError):
        return name


def pack_files(pack):
    """(source path, zip name) of a pack's files: its DLL from dist/ (the mounts embedded) and its README.md and
    CHANGELOG.md; its folder as an entry of its own."""
    dll = assembly_of(os.path.join(PACKS, pack))  # GK2SaddleUpDragon: its plugin folder and DLL, as the main mods'
    prefix = INNER + dll + "/"
    out = [(None, prefix), (os.path.join(DIST, dll + ".dll"), prefix + dll + ".dll")]
    for doc in ("README.md", "CHANGELOG.md"):
        out.append((os.path.join(PACKS, pack, doc), prefix + doc))
    return out


def write_pack_zip(path, packs, what):
    entries = [(None, "BepInEx/"), (None, INNER)]
    for p in packs:
        entries += pack_files(p)
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        for src, name in entries:
            if src is None:
                z.writestr(zipfile.ZipInfo(name), b"")
            else:
                z.write(src, name)
    with zipfile.ZipFile(path) as z:
        if z.testzip() is not None:
            sys.exit("the zip reads back broken")
        if z.namelist() != [n for _, n in entries]:
            sys.exit(f"{path}: unexpected entries")
        for src, name in entries:
            if src is not None and sha(z.read(name)) != sha(open(src, "rb").read()):
                sys.exit(f"{name} in the zip differs from {src}")
    files = sum(1 for s, _ in entries if s is not None)
    print(f"{path}: {what}, {files} files, {os.path.getsize(path):,} bytes")


def main():
    if subprocess.run([sys.executable, os.path.join(ROOT, "tools", "check-dist.py")]).returncode != 0:
        sys.exit("check-dist.py failed: no zip written")
    saddle, api = version("src/SaddleUp/Plugin.cs"), version("src/GK2DonkeyAPI/Plugin.cs")
    os.makedirs(OUT, exist_ok=True)
    write_zip(os.path.join(OUT, f"GK2-Saddle-Up-{saddle}.zip"), FILES, f"GK2 Saddle Up {saddle} + GK2 Donkey API {api}")
    packs = sorted(d for d in os.listdir(PACKS) if os.path.isfile(os.path.join(PACKS, d, d + ".csproj"))) if os.path.isdir(PACKS) else []
    for p in packs:
        v = pack_version(p)
        short = p[len("SaddleUp"):] if p.startswith("SaddleUp") and len(p) > len("SaddleUp") else p  # SaddleUpDragon: GK2-Saddle-Up-Dragon-1.0.0.zip
        write_pack_zip(os.path.join(OUT, f"GK2-Saddle-Up-{short}-{v}.zip"), [p], f"the pack {p} {v}")
    if len(packs) > 1:
        write_pack_zip(os.path.join(OUT, f"GK2-Saddle-Up-All-Mounts-{saddle}.zip"), packs, "every pack: " + ", ".join(f"{p} {pack_version(p)}" for p in packs))


if __name__ == "__main__":
    main()
