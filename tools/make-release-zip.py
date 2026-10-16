"""The release zip for Nexus (docs/release-checklist.md, section 3).

    python tools/make-release-zip.py

Runs tools/check-dist.py first and stops if it fails. Then writes release/GK2-Saddle-Up-<version>.zip holding
BepInEx/plugins/GK2DonkeyAPI/GK2DonkeyAPI.dll and BepInEx/plugins/GK2SaddleUp/GK2SaddleUp.dll from dist/ (a folder per
mod, as other mods do; BepInEx finds plugins and their references in subfolders), so the zip extracts straight into
the game folder (and mod managers see the BepInEx layout). The versions are read from the plugins' sources; the zip is read back
and every DLL compared with dist/ byte for byte.
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


def main():
    if subprocess.run([sys.executable, os.path.join(ROOT, "tools", "check-dist.py")]).returncode != 0:
        sys.exit("check-dist.py failed: no zip written")
    saddle, api = version("src/SaddleUp/Plugin.cs"), version("src/GK2DonkeyAPI/Plugin.cs")
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, f"GK2-Saddle-Up-{saddle}.zip")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        # the folders as entries of their own: some extractors and managers want them
        z.writestr(zipfile.ZipInfo("BepInEx/"), b"")
        z.writestr(zipfile.ZipInfo(INNER), b"")
        for f in FILES:
            z.writestr(zipfile.ZipInfo(INNER + f[:-4] + "/"), b"")
            z.write(os.path.join(DIST, f), inner(f))
    with zipfile.ZipFile(path) as z:
        if z.testzip() is not None:
            sys.exit("the zip reads back broken")
        names = z.namelist()
        want = ["BepInEx/", INNER]
        for f in FILES:
            want += [INNER + f[:-4] + "/", inner(f)]
        if names != want:
            sys.exit(f"unexpected entries: {names}")
        for f in FILES:
            if sha(z.read(inner(f))) != sha(open(os.path.join(DIST, f), "rb").read()):
                sys.exit(f"{f} in the zip differs from dist/")
    print(f"{path}: GK2 Saddle Up {saddle} + GK2 Donkey API {api}, {os.path.getsize(path):,} bytes")
    for n in names:
        print("  " + n)


if __name__ == "__main__":
    main()
