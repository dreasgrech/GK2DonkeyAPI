"""Checks the release DLLs in dist/ without the game: run `python tools/check-dist.py` from the repo root.

A release build (-p:ForDist=true) must carry no debug directory (its PDB path would hold this machine's user folder),
no path into this machine and no source-folder marker, and the API's dependency manifest (the gk2deps.txt resource,
docs/dependency-check.md) must find every game member it names in the installed game, unchanged: a dist whose members
changed or vanished needs a re-audit and a rebuild before it ships. SaddleUp carries no manifest: it uses no game
member of its own (only the API), which this checks too.
Exit code 1 on any finding."""
import glob, os, re, subprocess, sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOL = os.path.join(REPO, "tools", "DepManifest", "bin", "Release", "net8.0", "DepManifest.dll")
NO_MANIFEST = {"SaddleUp.dll": "no manifest: it uses no game member of its own (it follows the API's check through Mounts.IsAvailable)"}

props = open(os.path.join(REPO, "Directory.Build.props"), encoding="utf-8").read()
GAME = re.search(r"<GameDir[^>]*>([^<]+)</GameDir>", props).group(1).strip()
MANAGED = os.path.join(GAME, "GraveyardKeeper2_Data", "Managed")

if not os.path.exists(TOOL):
    subprocess.run(["dotnet", "build", "-c", "Release", "-nologo", "-v", "q", os.path.join(REPO, "tools", "DepManifest")], check=True)

bad = 0
for path in sorted(glob.glob(os.path.join(REPO, "dist", "*.dll"))):
    name = os.path.basename(path)
    b = open(path, "rb").read()
    findings = []
    # A drive letter, a colon and a backslash occur by chance in binary data (the lantern mesh gave "d:\BZ"): a path
    # has a folder name and a second separator after them.
    for pat in (rb"[A-Za-z]:\\[A-Za-z0-9_. ()-]{1,80}\\[^\x00\"]{0,160}", rb"[^\x00]{0,160}\.pdb"):
        for hit in re.findall(pat, b):
            findings.append("path or pdb: " + hit[:120].decode("latin-1"))
    for hit in re.findall(rb"(?:[A-Za-z]\x00:\x00\\\x00(?:[A-Za-z0-9_. ()-]\x00){1,80}\\\x00(?:[^\x00]\x00){0,120})", b):
        findings.append("path (utf-16): " + hit.decode("utf-16le", "ignore")[:120])
    if "Documents\\GitHub".encode("utf-16le") in b:
        findings.append("the source folder (GK2DonkeyAPISourceDir) is baked in: not a -p:ForDist=true build")
    note = None
    if name in NO_MANIFEST:
        # No manifest by design: then the DLL must reference neither game assembly (a reference would be an unchecked dependency).
        if b"Assembly-CSharp" in b or b"LazyBearTechnology" in b:
            findings.append("references a game assembly but carries no dependency manifest: import DependencyManifest.targets")
        note = NO_MANIFEST[name]
    else:
        # The dependency manifest against the installed game: exit 0 identical, 2 changed, 1 missing or no manifest.
        v = subprocess.run(["dotnet", TOOL, "verify", path, MANAGED], capture_output=True, text=True)
        out = (v.stdout.strip() or (v.stderr.strip().splitlines()[0] if v.stderr.strip() else "no output")).splitlines()
        note = out[0].split(": ", 1)[1] if ": " in out[0] else out[0]
        if v.returncode == 2:
            findings.append("game members this DLL uses changed since it was built: re-audit and rebuild")
            findings.extend(l.strip() for l in out[1:])
        elif v.returncode != 0:
            findings.append(note if out[1:] == [] else note + " / " + " / ".join(l.strip() for l in out[1:]))
            note = None
    status = "ok" if not findings else "FAIL"
    print(f"{status}  {name}  {len(b):,} bytes")
    if note:
        print("      " + note)
    for f in findings:
        print("      " + f)
    bad += len(findings)
sys.exit(1 if bad else 0)
