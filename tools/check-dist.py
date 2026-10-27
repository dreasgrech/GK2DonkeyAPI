"""Checks the release DLLs in dist/ without the game: run `python tools/check-dist.py` from the repo root.

A release build (-p:ForDist=true) must carry no debug directory (its PDB path would hold this machine's user folder),
no path into this machine and no source-folder marker, and the API's dependency manifest (the gk2deps.txt resource,
docs/dependency-check.md) must find every game member it names in the installed game, unchanged: a dist whose members
changed or vanished needs a re-audit and a rebuild before it ships. GK2SaddleUp carries no manifest: it uses no game
member of its own (only the API), which this checks too.
The external mount packs (packs/<Pack>/, their own downloads, docs/making-a-mount.md "Built in or external") ship as a
small DLL each with their gk2mounts/ folder embedded (dist/GK2<Pack>.dll, the csproj's AssemblyName, checked as the others: no manifest, no game
assembly) and their README.md and CHANGELOG.md beside it: each needs both, its Plugin.cs Version the CHANGELOG's first
heading, and in every mount its mount.cfg with needs = and version = and its skins/default/skin.cfg; no file may name a
path on this machine; nothing stray (Thumbs.db, editor backups) may be in gk2mounts/ (it would be embedded).
Exit code 1 on any finding."""
import glob, os, re, subprocess, sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOL = os.path.join(REPO, "tools", "DepManifest", "bin", "Release", "net8.0", "DepManifest.dll")
NO_MANIFEST = {"GK2SaddleUp.dll": "no manifest: it uses no game member of its own (it follows the API's check through Mounts.IsAvailable); its built-in content (Gerry) is an embedded pack"}
STRAY = ("thumbs.db", "desktop.ini", ".ds_store")  # never shipped
PACKS_DIR = os.path.join(REPO, "packs")
def assembly_of(pack_dir):
    """A pack's DLL name (without .dll): its csproj's AssemblyName (GK2SaddleUpDragon for packs/SaddleUpDragon)."""
    name = os.path.basename(pack_dir.rstrip("/\\"))
    try:
        text = open(os.path.join(pack_dir, name + ".csproj"), encoding="utf-8").read()
        i = text.index("<AssemblyName>") + len("<AssemblyName>")
        return text[i:text.index("</AssemblyName>", i)].strip()
    except (OSError, ValueError):
        return name

for _p in (sorted(os.listdir(PACKS_DIR)) if os.path.isdir(PACKS_DIR) else []):
    if os.path.isfile(os.path.join(PACKS_DIR, _p, _p + ".csproj")):
        NO_MANIFEST[assembly_of(os.path.join(PACKS_DIR, _p)) + ".dll"] = "no manifest: a mount pack's plugin, it uses the API only; its gk2mounts\\ folder is embedded"
    if os.path.isfile(os.path.join(PACKS_DIR, _p, _p + ".csproj")) and not os.path.isfile(os.path.join(REPO, "dist", assembly_of(os.path.join(PACKS_DIR, _p)) + ".dll")):
        print(f"FAIL  {assembly_of(os.path.join(PACKS_DIR, _p))}.dll  not in dist/ (bash tools/build-dist.sh)")
        NO_MANIFEST["__missing__" + _p] = ""  # counted below


def stray(name):
    n = name.lower()
    return n in STRAY or n.startswith("~") or n.endswith((".un~", ".bak", ".tmp", ".swp"))

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
        findings.append("the source folder (GK2DonkeyAPISourceDir / GK2MountsSourceDir) is baked in: not a -p:ForDist=true build")
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
# the external packs
PACKS = os.path.join(REPO, "packs")
for pack in sorted(os.listdir(PACKS)) if os.path.isdir(PACKS) else []:
    root = os.path.join(PACKS, pack)
    if not os.path.isdir(root):
        continue
    findings = []
    for doc in ("README.md", "CHANGELOG.md"):
        if not os.path.isfile(os.path.join(root, doc)):
            findings.append("no " + doc)
    mounts_dir = os.path.join(root, "gk2mounts")
    mounts = sorted(os.listdir(mounts_dir)) if os.path.isdir(mounts_dir) else []
    if not mounts:
        findings.append("no gk2mounts/<mount>/")
    files = 0
    for dirpath, dirs, names in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj")] if dirpath == root else dirs
        for n in names:
            files += 1
            if stray(n) and os.path.relpath(dirpath, root).startswith("gk2mounts"):
                findings.append("stray file (it would be embedded, delete it): " + os.path.relpath(os.path.join(dirpath, n), root))
            elif n.lower().endswith((".cfg", ".md", ".txt")):
                t = open(os.path.join(dirpath, n), encoding="utf-8", errors="replace").read()
                if re.search(r"[A-Za-z]:\\(Users|Program Files)", t):
                    findings.append("a path on this machine in " + os.path.relpath(os.path.join(dirpath, n), root))
    for m in mounts:
        cfg = os.path.join(mounts_dir, m, "mount.cfg")
        if not os.path.isfile(cfg):
            continue  # a folder adding skins to another's mount
        t = open(cfg, encoding="utf-8").read()
        for key in ("needs", "version"):
            if not re.search(r"(?m)^" + key + r"\s*=\s*\S", t):
                findings.append(f"{m}/mount.cfg: no {key} =")
        if not os.path.isfile(os.path.join(mounts_dir, m, "skins", "default", "skin.cfg")):
            findings.append(f"{m}: no skins/default/skin.cfg")
    pc, cl = os.path.join(root, "Plugin.cs"), os.path.join(root, "CHANGELOG.md")
    if os.path.isfile(pc) and os.path.isfile(cl):
        pv = re.search(r'public const string Version = "([0-9.]+)"', open(pc, encoding="utf-8").read())
        cv = re.search(r"(?m)^## (?:Unreleased \()?([0-9]+\.[0-9]+(?:\.[0-9]+)?)", open(cl, encoding="utf-8").read())
        if not pv or not cv or pv.group(1) != cv.group(1):
            findings.append(f"Plugin.cs Version {pv.group(1) if pv else '?'} and CHANGELOG.md's first heading {cv.group(1) if cv else '?'} differ")
    print(f"{'ok' if not findings else 'FAIL'}  pack {pack}  {len(mounts)} mount folder(s), {files} file(s)")
    for f in findings:
        print("      " + f)
    bad += len(findings)
bad += sum(1 for k in NO_MANIFEST if k.startswith("__missing__"))
sys.exit(1 if bad else 0)
