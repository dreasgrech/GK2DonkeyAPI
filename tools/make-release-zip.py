"""The release zips for Nexus and the Steam Workshop staging folders (docs/release-checklist.md, section 3).

    python tools/make-release-zip.py                 # every item: Saddle Up and each pack
    python tools/make-release-zip.py --only saddleup # the Saddle Up zip alone (the packs' numbers are public: a
                                                     #   released version is never rebuilt, section 6)
    python tools/make-release-zip.py --only GK2SaddleUpDragonsMountPack
    python tools/make-release-zip.py --no-workshop   # zips only
    python tools/make-release-zip.py --no-zip --only GK2SaddleUpDragonsMountPack   # the Workshop folder alone (a public
                                                     #   pack version: its zip is not rebuilt, its item can still be staged)
    python tools/make-release-zip.py --overwrite     # replace a zip that exists in release/ (only one never uploaded:
                                                     #   a released version is never rebuilt, it gets a new number)

A zip that already exists in release/ is never overwritten without --overwrite: it may be the public file.

Runs tools/check-dist.py first and stops if it fails.

One item per download (the user, 2026-10-27: each pack ships on its own, there is no bundle): GK2 Saddle Up with the
Donkey API inside, and each external pack. Every item is one folder laid out as the game's code-mod contract wants
(README_MODDING.txt in the game folder since 1.011, docs/mods-window-and-workshop.md):

    modinfo.json                        id (the plugin GUID), name, version: the game's Mods window shows name and version
    BepInEx/plugins/<Mod>/<Mod>.dll     a folder per plugin (Saddle Up's zip: GK2DonkeyAPI and GK2SaddleUp)

The same folder serves both channels, byte for byte (the lab run of 2026-10-30 in the report):
- zipped, it is the Nexus download: release/GK2-Saddle-Up-<version>.zip and release/GK2-Saddle-Up-<Pack words
  dashed>-<version>.zip (GK2-Saddle-Up-Dragons-Mount-Pack-1.0.0.zip). Extracted into the game folder it is the loose
  install of old (BepInEx finds plugins in subfolders; the stray modinfo.json next to the exe is read by nothing);
  extracted into Mods\\Code\\<Name>\\ in the save data folder it is a local code mod the Mods window lists, with
  Enable/Disable, and the game copies the plugin folders into BepInEx\\plugins itself.
- as a folder, workshop/<Item>/ is what the game's Workshop creator (Shift+F11) uploads: the item content plus
  Thumbnail.png|jpg (the preview; from media/workshop/<Item>/), _description.bbcode (from media/..._Workshop.txt, Steam's
  BBCode) and _changenote.txt (the item's "For players" entry of its changelog); the uploader skips the three
  (names starting with _ and Thumbnail.*). workshop/ is not kept in git.

The pack's DLL carries its gk2mounts/ folder embedded, so the zip holds the DLL alone (the user, 2026-10-29). Versions
are read from the plugins' sources (Plugin.cs; check-dist.py keeps a pack's equal to its CHANGELOG's first heading).
Every zip is read back and each file compared with its source byte for byte; so is the staging folder.
"""
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(ROOT, "dist")
OUT = os.path.join(ROOT, "release")
WORKSHOP = os.path.join(ROOT, "workshop")
MEDIA = os.path.join(ROOT, "media")
PACKS = os.path.join(ROOT, "packs")
INNER = "BepInEx/plugins/"
MODINFO = "modinfo.json"
STEAM_DESCRIPTION_MAX = 8000  # k_cchPublishedDocumentDescriptionMax: Steam refuses a longer description


def plugin_facts(rel):
    """(guid, name, version) of a plugin's Plugin.cs: its Guid constant, the [BepInPlugin] name, its Version constant."""
    src = open(os.path.join(ROOT, rel), encoding="utf-8").read()
    v = re.search(r'public const string Version = "([0-9.]+)"', src)
    g = re.search(r'public const string Guid = "([^"]+)"', src)
    n = re.search(r'\[BepInPlugin\(Guid, "([^"]+)", Version\)\]', src)
    for what, m in (("Version constant", v), ("Guid constant", g), ("[BepInPlugin(Guid, \"name\", Version)]", n)):
        if not m:
            sys.exit(f"no {what} in {rel}")
    return g.group(1), n.group(1), v.group(1)


def version(rel):
    return plugin_facts(rel)[2]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def modinfo_bytes(guid, name, ver):
    return (json.dumps({"schemaVersion": 1, "id": guid, "name": name, "version": ver}, indent=2) + "\n").encode("utf-8")


def assembly_of(pack_dir):
    """A pack's DLL name (without .dll): its csproj's AssemblyName (GK2SaddleUpDragonsMountPack for packs/GK2SaddleUpDragonsMountPack)."""
    name = os.path.basename(pack_dir.rstrip("/\\"))
    try:
        text = open(os.path.join(pack_dir, name + ".csproj"), encoding="utf-8").read()
        i = text.index("<AssemblyName>") + len("<AssemblyName>")
        return text[i:text.index("</AssemblyName>", i)].strip()
    except (OSError, ValueError):
        return name


class Item:
    """One download: its folder name (the plugin folder of its main DLL), the zip name, modinfo, and the entries
    [(source path or None for a folder, path inside the item)] in zip order."""

    def __init__(self, folder, zip_name, guid, name, ver, dlls, what, changelog, media_dir, text_name, nexus_note=None):
        self.folder, self.zip_name, self.guid, self.name, self.version, self.what = folder, zip_name, guid, name, ver, what
        self.changelog, self.media_dir, self.text_name, self.nexus_note = changelog, media_dir, text_name, nexus_note
        self.entries = [(None, MODINFO), (None, "BepInEx/"), (None, INNER)]
        for dll in dlls:  # a folder per plugin, named after the DLL
            self.entries += [(None, INNER + dll + "/"), (os.path.join(DIST, dll + ".dll"), INNER + dll + "/" + dll + ".dll")]

    def modinfo(self):
        return modinfo_bytes(self.guid, self.name, self.version)

    def data(self, src, name):
        return self.modinfo() if name == MODINFO else open(src, "rb").read()


def saddle_up_item():
    guid, name, ver = plugin_facts("src/SaddleUp/Plugin.cs")
    api = version("src/GK2DonkeyAPI/Plugin.cs")
    return Item("GK2SaddleUp", f"GK2-Saddle-Up-{ver}.zip", guid, name, ver, ["GK2DonkeyAPI", "GK2SaddleUp"],
                f"GK2 Saddle Up {ver} + GK2 Donkey API {api}", os.path.join(ROOT, "CHANGELOG.md"), MEDIA, "GK2_DonkeyAPISaddleUp",
                os.path.join(ROOT, "docs", "nexus", f"saddleup-{ver}-changelog.md"))


def pack_item(pack):
    guid, name, ver = plugin_facts(f"packs/{pack}/Plugin.cs")  # check-dist.py: the version equals its CHANGELOG.md's first heading
    dll = assembly_of(os.path.join(PACKS, pack))
    short = pack[len("GK2SaddleUp"):] if pack.startswith("GK2SaddleUp") and len(pack) > len("GK2SaddleUp") else pack
    dashed = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "-", short)  # DonkeySkinPack: GK2-Saddle-Up-Donkey-Skin-Pack-1.0.0.zip
    # the Mods window shows the pack's public name, the one of its Nexus page and Workshop item ("GK2 Saddle Up: Dragons
    # Mount Pack"), not the plugin's short name ("GK2 Saddle Up: Dragon"; the user, 2026-10-30)
    name = "GK2 Saddle Up: " + dashed.replace("-", " ")
    return Item(dll, f"GK2-Saddle-Up-{dashed}-{ver}.zip", guid, name, ver, [dll], f"the pack {pack} {ver}",
                os.path.join(PACKS, pack, "CHANGELOG.md"), os.path.join(MEDIA, dashed.lower()), "GK2_DonkeyAPISaddleUp_" + short,
                os.path.join(ROOT, "docs", "nexus", f"{dashed.lower()}-{ver}-changelog.md"))  # the player note, when written


def write_zip(item, overwrite=False):
    path = os.path.join(OUT, item.zip_name)
    if os.path.isfile(path) and not overwrite:
        # a zip in release/ may be the public one (0.13.1 was overwritten in place on 2026-10-30 before anyone noticed it
        # was uploaded already): a released version is never rebuilt, and a new one gets a new number
        sys.exit(f"{path} exists: a released zip is never rebuilt (bump the version), or pass --overwrite for one that was never uploaded")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        for src, name in item.entries:
            if name == MODINFO:
                z.writestr(MODINFO, item.modinfo())
            elif src is None:
                z.writestr(zipfile.ZipInfo(name), b"")  # the folders as entries of their own: some extractors and managers want them
            else:
                z.write(src, name)
    with zipfile.ZipFile(path) as z:
        if z.testzip() is not None:
            sys.exit(f"{path} reads back broken")
        names = z.namelist()
        if names != [n for _, n in item.entries]:
            sys.exit(f"{path}: unexpected entries: {names}")
        for src, name in item.entries:
            if src is not None or name == MODINFO:
                if sha(z.read(name)) != sha(item.data(src, name)):
                    sys.exit(f"{name} in {path} differs from its source")
        info = json.loads(z.read(MODINFO).decode("utf-8"))
        if info["version"] != item.version or info["id"] != item.guid:
            sys.exit(f"{path}: modinfo.json says {info}, the plugin {item.guid} {item.version}")
    files = sum(1 for s, _ in item.entries if s is not None) + 1
    print(f"{path}: {item.what}, {files} files, {os.path.getsize(path):,} bytes")
    for n in names:
        print("  " + n)


def changenote(item):
    """The item's change note: the player text written for the release when there is one
    (docs/nexus/saddleup-<version>-changelog.md, the simplified Nexus text), else the 'For players' bullets of its
    changelog entry for this version (Saddle Up: the heading '## <version> (' ; a pack: '## <version>'); before the
    release heading exists, the Unreleased block, said so."""
    # a Workshop note of its own first (docs/nexus/<name>-<version>-workshop-changenote.txt: the Nexus note never sends
    # players to Steam, the user 2026-10-30; the Workshop's may speak of the Workshop), then the Nexus note
    workshop_note = item.nexus_note.replace("-changelog.md", "-workshop-changenote.txt") if item.nexus_note else None
    if workshop_note and os.path.isfile(workshop_note):
        print(f"  change note: {os.path.relpath(workshop_note, ROOT)}")
        return open(workshop_note, encoding="utf-8").read().replace("\r\n", "\n").strip("\n") + "\n"
    if item.nexus_note and os.path.isfile(item.nexus_note):
        note = open(item.nexus_note, encoding="utf-8").read().replace("\r\n", "\n")
        note = re.sub(r"(?m)^# ", "", note)
        note = re.sub(r"\*\*(.+?)\*\*", r"\1", note)
        note = re.sub(r"`([^`]*)`", r"\1", note)
        print(f"  change note: {os.path.relpath(item.nexus_note, ROOT)}")
        return note.strip("\n") + "\n"
    text = open(item.changelog, encoding="utf-8").read().replace("\r\n", "\n")
    heads = [(m.start(), m.group(1)) for m in re.finditer(r"(?m)^## (.+)$", text)]
    block = None
    for i, (pos, title) in enumerate(heads):
        end = heads[i + 1][0] if i + 1 < len(heads) else len(text)
        if title == item.version or title.startswith((item.version + " ", item.version + ",", item.version + " (")):
            block = text[pos:end]
            break
    if block is None:
        for i, (pos, title) in enumerate(heads):
            if title.startswith("Unreleased"):
                end = heads[i + 1][0] if i + 1 < len(heads) else len(text)
                block = text[pos:end]
                print(f"  note: {os.path.relpath(item.changelog, ROOT)} has no heading for {item.version} yet; the change note is its '{title}' block")
                break
    if block is None:
        return f"{item.name} {item.version}\n"
    lines = block.split("\n")[1:]
    # the "For players:" part when the entry has parts, else the whole entry
    if any(l.strip() == "For players:" for l in lines):
        start = next(i for i, l in enumerate(lines) if l.strip() == "For players:") + 1
        stop = next((i for i, l in enumerate(lines) if i > start and l.strip().endswith(":") and not l.startswith((" ", "-"))), len(lines))
        lines = lines[start:stop]
    out = "\n".join(lines).strip("\n")
    out = re.sub(r"\*\*(.+?)\*\*", r"\1", out)  # markdown bold off: the note is plain text
    out = re.sub(r"`([^`]*)`", r"\1", out)
    return f"{item.name} {item.version}\n\n{out}\n"


def stage_workshop(item):
    """workshop/<Item>/: the item content as the zip holds it, plus the uploader's own files."""
    dest = os.path.join(WORKSHOP, item.folder)
    if os.path.isdir(dest):
        shutil.rmtree(dest)
    os.makedirs(dest)
    for src, name in item.entries:
        target = os.path.join(dest, name.replace("/", os.sep))
        if name == MODINFO:
            open(target, "wb").write(item.modinfo())
        elif src is None:
            os.makedirs(target, exist_ok=True)
        else:
            shutil.copyfile(src, target)
            if sha(open(target, "rb").read()) != sha(open(src, "rb").read()):
                sys.exit(f"{target} differs from {src}")
    notes = []
    thumb = None
    for ext in ("png", "jpg", "jpeg"):
        cand = os.path.join(MEDIA, "workshop", item.folder, "Thumbnail." + ext)
        if os.path.isfile(cand):
            thumb = cand
            break
    if thumb:
        shutil.copyfile(thumb, os.path.join(dest, os.path.basename(thumb)))
        if os.path.getsize(thumb) >= 1_000_000:
            notes.append(f"Thumbnail is {os.path.getsize(thumb):,} bytes: Steam wants the preview under 1 MB")
    else:
        notes.append(f"no media/workshop/{item.folder}/Thumbnail.png (or .jpg): the item uploads without a preview")
    text = os.path.join(item.media_dir, item.text_name + "_Workshop.txt")
    if os.path.isfile(text):
        desc = open(text, encoding="utf-8").read().replace("\r\n", "\n")
        open(os.path.join(dest, "_description.bbcode"), "w", encoding="utf-8", newline="\n").write(desc)
        if "{{" in desc:
            notes.append(f"{os.path.relpath(text, ROOT)} still has a {{{{ placeholder }}}}: fill it in before uploading")
        if len(desc) > STEAM_DESCRIPTION_MAX:
            notes.append(f"{os.path.relpath(text, ROOT)} is {len(desc):,} characters; Steam's description limit is {STEAM_DESCRIPTION_MAX:,}")
    else:
        notes.append(f"no {os.path.relpath(text, ROOT)}: the item uploads without a description (write it in Steam's BBCode)")
    open(os.path.join(dest, "_changenote.txt"), "w", encoding="utf-8", newline="\n").write(changenote(item))
    print(f"{dest}: the Workshop folder ({item.what})")
    for dirpath, _, names in os.walk(dest):
        for n in sorted(names):
            p = os.path.join(dirpath, n)
            print(f"  {os.path.relpath(p, dest).replace(os.sep, '/')} ({os.path.getsize(p):,} bytes)")
    for n in notes:
        print("  note: " + n)


def main():
    args = sys.argv[1:]
    workshop = "--no-workshop" not in args
    zips = "--no-zip" not in args  # staging only: a pack whose number is public gets its Workshop folder without a new zip
    only = None
    if "--only" in args:
        i = args.index("--only")
        only = args[i + 1:i + 2]
        if not only:
            sys.exit("--only needs saddleup or a pack folder name")
    if subprocess.run([sys.executable, "-I", os.path.join(ROOT, "tools", "release-versions.py"), "check"]).returncode != 0:
        sys.exit("release-versions.py check failed: a version line is stale or an editorial piece is missing; no zip written")
    if subprocess.run([sys.executable, os.path.join(ROOT, "tools", "check-dist.py")]).returncode != 0:
        sys.exit("check-dist.py failed: no zip written")
    items = [saddle_up_item()]
    packs = sorted(d for d in os.listdir(PACKS) if os.path.isfile(os.path.join(PACKS, d, d + ".csproj"))) if os.path.isdir(PACKS) else []
    items += [pack_item(p) for p in packs]
    if only:
        want = only[0].lower()
        items = [it for it in items if (want == "saddleup" and it.folder == "GK2SaddleUp") or it.folder.lower() == want or it.what.lower().startswith(f"the pack {want} ")]
        if not items:
            sys.exit(f"--only {only[0]}: no such item (saddleup or one of {', '.join(packs)})")
    os.makedirs(OUT, exist_ok=True)
    for it in items:
        if zips:
            write_zip(it, overwrite="--overwrite" in args)
        if workshop:
            stage_workshop(it)


if __name__ == "__main__":
    main()
