"""Offline dry run of the settings page's carry-over (docs/compatibility.md section 2.1): every key of an older
GK2 Saddle Up cfg must land on a row of today's page, by its own (section, key), by the same plain section under
another number, or by a MovedKey alias. A key that lands nowhere is a player's value the update would lose.

The rows and the aliases are read out of src/SaddleUp/Plugin.cs itself: p.X("Section", "Key"...) calls, the
MovedKey and RetiredKey calls, Color (a colour key and its <key>Lightness), MountRows / FoalRows / EffectRows, and the PerMount
suffixes (the mounts below; a player's file holds the donkey's unsuffixed set only). The in-game upgrade self-test
is the same check through the live page; run this one first, it needs no game.

    python tools/page-carryover.py                 # the fixtures in tests/fixtures/released/*/
    python tools/page-carryover.py some.cfg ...    # any older cfg(s) as well

Exit code 1 when a value is lost.
"""
import glob, io, os, re, sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLUGIN = os.path.join(REPO, "src", "SaddleUp", "Plugin.cs")
# the per-mount sets a cfg may hold: the built-in kinds and the example mod's (checker-beast); a player's is "" only
MOUNTS = ["", "_dragon", "_gerry", "_rat", "_snail", "_worm", "_checker-beast"]
KINDS = ["donkey", "dragon", "gerry", "rat", "snail", "worm", "checker-beast"]
FOAL_ROWS = ["Foals", "FoalsFollow", "FoalsIndoors", "FoalsWearSkin"]
EFFECT_ROWS = ["Effect", "EffectWhen", "EffectTrail", "EffectWithCart", "EffectInFights", "EffectOnFoals"]


def plain(sec):
    m = re.match(r"^(\d+)\. (.*)$", sec)
    return m.group(2) if m else sec


def page_rows(src):
    rows, aliases = set(), {}
    for m in re.finditer(r'p\.(Key|Text|Toggle|Choice|Steps|Color|List)\("(\w+)", "(\w+)"( \+ sfx)?', src):
        kind, sec, key, sfx = m.groups()
        for s in (MOUNTS if sfx else [""]):
            rows.add((sec, key + s))
            if kind == "Color":
                rows.add((sec, key + "Lightness" + s))
    for m in re.finditer(r'p\.MovedKey\("(\w+)", "(\w+)"( \+ sfx)?, "(\w+)"(?:, "(\w+)")?\)', src):
        sec, key, sfx, old, oldKey = m.groups()
        for s in (MOUNTS if sfx else [""]):
            aliases.setdefault((sec, key + s), []).append((old, (oldKey or key) + s))  # a key may name several old places
            if (sec, key + "Lightness" + s) in rows:  # Color() carries the lightness alias along
                aliases.setdefault((sec, key + "Lightness" + s), []).append((old, (oldKey or key) + "Lightness" + s))
    for m in re.finditer(r'p\.RetiredKey\("(\w+)", "(\w+)"\)', src):
        rows.add((m.group(1), m.group(2)))  # retired on purpose: its line leaves the file, never a value lost
    for m in re.finditer(r'p\.MountRows\("(\w+)"', src):
        rows.add((m.group(1), "Mount"))
        rows.update((m.group(1), "Skin_" + k) for k in KINDS)
    for m in re.finditer(r'p\.FoalRows\("(\w+)"', src):
        rows.update((m.group(1), k) for k in FOAL_ROWS)
    for m in re.finditer(r'p\.EffectRows\("(\w+)"', src):
        rows.update((m.group(1), k) for k in EFFECT_ROWS)
    return rows, aliases


def cfg_keys(path):
    out, sec = [], None
    with io.open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\r\n")
            if line.startswith("[") and line.endswith("]"):
                sec = line[1:-1]
            elif sec and " = " in line and not line.startswith("#"):
                out.append((sec, line.split(" = ", 1)[0]))
    return out


def main(argv):
    with io.open(PLUGIN, encoding="utf-8") as f:
        rows, aliases = page_rows(f.read())
    claimable = set(rows) | {place for places in aliases.values() for place in places}
    print(f"{len(rows)} rows, {len(aliases)} aliases (src/SaddleUp/Plugin.cs)")
    files = sorted(glob.glob(os.path.join(REPO, "tests", "fixtures", "released", "*", "dreas.gk2.saddleup.cfg"))) + argv
    ok = True
    for path in files:
        keys = cfg_keys(path)
        lost = [(s, k) for s, k in keys if (plain(s), k) not in claimable]
        moved = sum(1 for s, k in keys if (plain(s), k) not in rows)
        label = os.path.relpath(path, REPO) if path.startswith(REPO) else path
        print(f"{label}: {len(keys)} keys, {moved} through an alias, {len(lost)} lost" + ("; lost: " + ", ".join(f"[{s}] {k}" for s, k in lost) if lost else ""))
        ok &= not lost
    print("OK: every value lands on a row" if ok else "LOST VALUES: a MovedKey is missing")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
