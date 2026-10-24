"""Rewrites every built-in skin.cfg and mount.cfg into the canonical layout (docs/mounts-foundation-audit.md section 4)
without changing a value, CRLF line ends, each section's comment above it.

skin.cfg (assets/mounts/<mount>/skins/<skin>/): [Skin] keys in one order (SKIN_ORDER), then [Game], the skin's own
geometry ([Tuning], [Tuning.Ride], [Tuning.Cart]), its sounds ([Sounds], [Sounds.Ride], [Sounds.Cart], [Sounds.Baby]),
the colour sections ([Colors], [Base.*]). No [Mount*] section: a mount's values are its mount.cfg's (2026-10-24).

mount.cfg (assets/mounts/<mount>/): [Mount] (MOUNT_ORDER; always id, name, author, version, cart and foals), then
[Stats], [Tuning], [Tuning.Ride], [Tuning.Cart], [Sounds], [Sounds.Ride], [Sounds.Cart], [Sounds.Baby]: the order the
Mounts tab's save writes (Looks.SaveMountCfg).

Run it after editing a cfg by hand, then tests/SkinsScan; --check runs in tools/release-scans.sh and refuses a built-in
that is not byte for byte in this layout. tools/skins/new_skin.py writes new files in this layout from the start.

    python -I tools/skins/canonical_cfg.py            # rewrites every pack cfg under src/*/assets/mounts (skinroots.py)
    python -I tools/skins/canonical_cfg.py --check    # says which files differ from their canonical text, writes nothing
"""
import io, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import skinroots
REPO = skinroots.REPO
SKIN_ORDER = ["format", "needs", "id", "name", "description", "portrait", "author", "version", "base", "colours", "layer", "erase", "canvas", "pivot", "source", "seat",
              "effect", "effect_when", "sheet", "sheet_front", "columns", "frames", "offset", "offset_cart", "mount"]  # the order the Mounts tab's Save writes
MOUNT_ORDER = ["id", "name", "noun", "description", "portrait", "default_skin", "author", "version", "cart", "foals", "flies", "jumps", "foal_scale", "speed", "cargo", "effect", "effect_when"]
MOUNT_DEFAULTS = {"author": "dreas", "version": "1.0", "cart": "true", "foals": "true"}
SKIN_SECTIONS = [("tuning", "[Tuning]"), ("tuning.ride", "[Tuning.Ride]"), ("tuning.cart", "[Tuning.Cart]"), ("sounds", "[Sounds]"), ("sounds.ride", "[Sounds.Ride]"), ("sounds.cart", "[Sounds.Cart]"), ("sounds.baby", "[Sounds.Baby]")]
MOUNT_SECTIONS = [("stats", "[Stats]"), ("tuning", "[Tuning]"), ("tuning.ride", "[Tuning.Ride]"), ("tuning.cart", "[Tuning.Cart]"), ("sounds", "[Sounds]"), ("sounds.ride", "[Sounds.Ride]"), ("sounds.cart", "[Sounds.Cart]"), ("sounds.baby", "[Sounds.Baby]")]


def parse(path):
    """Sections in file order: name (lower) -> {'header': raw, 'above': [comment lines before the header], 'lines': [raw]}.
    The head comment (before any section) is section ''."""
    secs, order = {}, []
    cur = ""
    secs[cur] = {"header": "", "above": [], "lines": []}
    order.append(cur)
    pending = []  # comment lines not yet assigned (they go above the next header, or stay in the section)
    for raw in io.open(path, encoding="utf-8").read().splitlines():
        line = raw.strip()
        if line.startswith("["):
            name = line.split("#", 1)[0].strip().strip("[]").strip().lower()
            secs[name] = {"header": line.split("#", 1)[0].strip(), "above": pending, "lines": []}
            order.append(name)
            pending = []
            cur = name
            continue
        if line.startswith("#") and cur != "":
            pending.append(raw.rstrip())
            continue
        if line == "":
            continue
        secs[cur]["lines"].extend(pending)
        pending = []
        secs[cur]["lines"].append(raw.rstrip())
    secs[cur]["lines"].extend(pending)
    return secs, order


def keyed(lines):
    """[(key, value, inline comment)] for the 'key = value  # note' lines, plus the comment lines that stood above each key."""
    out, above = [], []
    for raw in lines:
        s = raw.strip()
        if s.startswith("#"):
            above.append(raw)
            continue
        if "=" not in s:
            continue
        k, v = s.split("=", 1)
        note = ""
        if "#" in v:
            v, note = v.split("#", 1)
            note = "# " + note.strip()
        out.append((k.strip(), v.strip(), note, above))
        above = []
    return out, above


def emit_keyed(out, items, order, defaults=None, trailing=()):
    have = {k.lower(): (k, v, n, a) for k, v, n, a in items}
    for key in order:
        if key in have:
            k, v, n, a = have[key]
        elif defaults and key in defaults:
            k, v, n, a = key, defaults[key], "", []
        else:
            continue
        for line in a:
            out.append(line)
        out.append(f"{key} = {v}" + (f"  {n}" if n else ""))
    for k, v, n, a in items:
        if k.lower() not in order:
            raise SystemExit(f"unknown key {k} (add it to the order list)")
    out.extend(trailing)


def sections(out, secs, names):
    for name, header in names:
        if name in secs and any("=" in l for l in secs[name]["lines"]):  # an empty section is left out (the scan refuses one)
            out.append("")
            out.extend(secs[name]["above"])
            out.append(header)
            out.extend(secs[name]["lines"])


def canonical(path):
    """skin.cfg"""
    secs, order = parse(path)
    for name in order:
        if name == "mount" or name.startswith("mount."):
            raise SystemExit(f"{path}: [{secs[name]['header'].strip('[]')}] in a skin.cfg: a mount's values go in its mount.cfg (assets/mounts/<mount>/mount.cfg)")
    out = []
    out.extend(secs[""]["lines"])  # the head comment
    items, trailing = keyed(secs["skin"]["lines"])
    out.append("[Skin]")
    emit_keyed(out, items, SKIN_ORDER, trailing=trailing)
    if "game" in secs:
        out.append("")
        out.extend(secs["game"]["above"])
        out.append("[Game]")
        out.extend(secs["game"]["lines"])
    sections(out, secs, SKIN_SECTIONS)
    for name in order:
        if name == "colors" or name.startswith("base."):
            out.append("")
            out.extend(secs[name]["above"])
            out.append(secs[name]["header"])
            out.extend(secs[name]["lines"])
    known = {"", "skin", "game", "colors"} | {n for n, _ in SKIN_SECTIONS}
    for name in order:
        if name not in known and not name.startswith("base."):
            raise SystemExit(f"{path}: section [{name}] is not one a skin.cfg has")
    return "\r\n".join(out) + "\r\n"


def canonical_mount(path):
    """mount.cfg"""
    secs, order = parse(path)
    if "mount" not in secs:
        raise SystemExit(f"{path}: no [Mount] section")
    out = []
    out.extend(secs[""]["lines"])
    out.extend(secs["mount"]["above"])
    out.append("[Mount]")
    items, trailing = keyed(secs["mount"]["lines"])
    emit_keyed(out, items, MOUNT_ORDER, defaults=MOUNT_DEFAULTS, trailing=trailing)
    sections(out, secs, MOUNT_SECTIONS)
    known = {"", "mount"} | {n for n, _ in MOUNT_SECTIONS}
    for name in order:
        if name not in known:
            raise SystemExit(f"{path}: section [{name}] is not one a mount.cfg has ([Mount], [Stats], [Tuning*], [Sounds*])")
    return "\r\n".join(out) + "\r\n"


def files():
    """(label, path, canonical function) for every pack cfg"""
    out = []
    for m in skinroots.mount_dirs():
        p = os.path.join(m, "mount.cfg")
        if os.path.isfile(p):
            out.append((os.path.basename(m) + "/mount.cfg", p, canonical_mount))
    for s in skinroots.skin_dirs():
        out.append((skinroots.mount_of(s) + "/" + os.path.basename(s), os.path.join(s, "skin.cfg"), canonical))
    return out


def main(argv):
    check = "--check" in argv
    diff = 0
    for label, path, fn in files():
        text = fn(path)
        current = io.open(path, encoding="utf-8", newline="").read()
        if current == text:
            print(f"{label:28s} canonical")
            continue
        diff += 1
        if check:
            print(f"{label:28s} DIFFERS from the canonical layout (run without --check to rewrite it)")
        else:
            io.open(path, "w", encoding="utf-8", newline="").write(text)
            print(f"{label:28s} rewritten")
    return 1 if (check and diff) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
