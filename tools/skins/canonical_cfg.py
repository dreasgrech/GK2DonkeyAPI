"""Rewrites every built-in skin.cfg into the canonical layout (docs/mounts-foundation-audit.md section 4) without
changing a value: [Skin] keys in one order (SKIN_ORDER), then [Game], the skin's own geometry ([Tuning],
[Tuning.Ride], [Tuning.Cart]), the colour sections ([Colors], [Base.*]), and the [Mount] block last ([Mount], then
[Mount.Tuning], [Mount.Tuning.Ride], [Mount.Tuning.Cart]), each section's comment above it, CRLF line ends. The
[Mount] block always carries id, name, author, version, cart and foals; noun, foal_scale, effect and effect_when
only when set (MOUNT_ORDER). Run it after editing a cfg by hand, then tests/SkinsScan (the section order and the key
rules); --check runs in tools/release-scans.sh and refuses a built-in that is not byte for byte in this layout.
tools/skins/new_skin.py writes a new skin's cfg in this layout from the start.

    python -I tools/skins/canonical_cfg.py            # rewrites every src/*/assets/skins/*/skin.cfg (skinroots.py)
    python -I tools/skins/canonical_cfg.py --check    # says which files differ from their canonical text, writes nothing
"""
import io, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import skinroots
REPO = skinroots.REPO
SKIN_ORDER = ["format", "id", "name", "author", "version", "base", "kind", "layer", "erase", "canvas", "pivot", "source", "seat",
              "effect", "effect_when", "sheet", "sheet_front", "columns", "frames", "offset", "offset_cart", "mount"]  # the order the Mounts tab's Save writes
MOUNT_ORDER = ["id", "name", "noun", "author", "version", "cart", "foals", "foal_scale", "effect", "effect_when"]
MOUNT_DEFAULTS = {"author": "dreas", "version": "1.0", "cart": "true", "foals": "true"}
TUNING_SECTIONS = ["mount.tuning", "mount.tuning.ride", "mount.tuning.cart"]
MOUNT_COMMENT = "# The mount this skin is the Default of (docs/mounts-design.md): what the player rides, shared by every skin of it."


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


def canonical(path):
    secs, order = parse(path)
    out = []
    out.extend(secs[""]["lines"])  # the head comment
    # [Skin]
    items, trailing = keyed(secs["skin"]["lines"])
    out.append("[Skin]")
    emit_keyed(out, items, SKIN_ORDER, trailing=trailing)
    # [Game] right after, as written
    if "game" in secs:
        out.append("")
        out.extend(secs["game"]["above"])
        out.append("[Game]")
        out.extend(secs["game"]["lines"])
    # the skin's own geometry ([Tuning], then per style), as written
    for name, header in (("tuning", "[Tuning]"), ("tuning.ride", "[Tuning.Ride]"), ("tuning.cart", "[Tuning.Cart]")):
        if name in secs and any("=" in l for l in secs[name]["lines"]):  # an empty section is left out (the scan refuses one)
            out.append("")
            out.extend(secs[name]["above"])
            out.append(header)
            out.extend(secs[name]["lines"])
    # the colour sections, as written, in file order
    for name in order:
        if name == "colors" or name.startswith("base."):
            out.append("")
            out.extend(secs[name]["above"])
            out.append(secs[name]["header"])
            out.extend(secs[name]["lines"])
    # the mount block last
    if "mount" in secs:
        out.append("")
        above = secs["mount"]["above"] or [MOUNT_COMMENT]
        out.extend(above)
        out.append("[Mount]")
        items, trailing = keyed(secs["mount"]["lines"])
        emit_keyed(out, items, MOUNT_ORDER, defaults=MOUNT_DEFAULTS, trailing=trailing)
        for name in TUNING_SECTIONS:
            if name in secs and any("=" in l for l in secs[name]["lines"]):
                out.append("")
                out.extend(secs[name]["above"])
                out.append(secs[name]["header"])
                out.extend(secs[name]["lines"])
    if "mount" not in secs and any(name in secs for name in TUNING_SECTIONS):
        raise SystemExit(f"{path}: a [Mount.Tuning] section without a [Mount] section (the game ignores it; add the block or take the section out)")
    known = {"", "skin", "game", "tuning", "tuning.ride", "tuning.cart", "colors", "mount"} | set(TUNING_SECTIONS)
    for name in order:
        if name not in known and not name.startswith("base."):
            raise SystemExit(f"{path}: section [{name}] is not one the loader knows")
    return "\r\n".join(out) + "\r\n"


def main(argv):
    check = "--check" in argv
    diff = 0
    for folder in skinroots.skin_dirs():
        skin = os.path.basename(folder)
        path = os.path.join(folder, "skin.cfg")
        text = canonical(path)
        current = io.open(path, encoding="utf-8", newline="").read()
        if current == text:
            print(f"{skin:14s} canonical")
            continue
        diff += 1
        if check:
            print(f"{skin:14s} DIFFERS from the canonical layout (run without --check to rewrite it)")
        else:
            io.open(path, "w", encoding="utf-8", newline="").write(text)
            print(f"{skin:14s} rewritten")
    return 1 if (check and diff) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
