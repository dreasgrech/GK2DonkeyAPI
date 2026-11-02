"""The jump probe's landing audits as one table (docs/flying.md "The jump probe"): for every touch-down judged in the
traces of BepInEx\\GK2-jumps (Debug.JumpTrace on), how far it came down from the take-off, whether the game's walk grid
(a 4.4 square round the body, rebuilt at each ask) held the take-off at all, whether the NPCs' mesh vouches for the
spot (on it, the take-off's part: the grid's way is then not asked), what the grid said, and the verdicts.

    python tools/jump-audits.py                  # the game's traces (the game folder from Directory.Build.props)
    python tools/jump-audits.py <folder>         # a saved copy
    python tools/jump-audits.py --all            # every audit, not only those whose take-off lies off the grid

Read only.
"""
import math, os, re, sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

V = r"\((-?[\d.]+), (-?[\d.]+), (-?[\d.]+)\)"


def game_dir():
    props = open(os.path.join(REPO, "Directory.Build.props"), encoding="utf-8").read()
    m = re.search(r"<GameDir[^>]*>([^<]+)</GameDir>", props)
    return m.group(1).strip() if m else None


def vec(m, i=1):
    return tuple(float(m.group(i + k)) for k in range(3))


def audits(path):
    """Every landing audit in one trace, as a dict."""
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    out = []
    for i, line in enumerate(lines):
        m = re.search(r";audit at " + V + r" \(take-off " + V, line)
        if not m:
            continue
        a = {"file": os.path.basename(path), "spot": vec(m), "take": vec(m, 4)}
        j = i - 1
        while j >= 0 and "touch-down judged at" not in lines[j] and not lines[j].startswith("F;"):
            j -= 1
        jm = re.search(r"touch-down judged at " + V + r": (.*)$", lines[j]) if j >= 0 else None
        a["judged"] = jm.group(4) if jm else "-"
        for line2 in lines[i + 1:]:
            if line2.startswith(("N;", "F;", "S;", "#")):
                break
            m2 = re.search(r"2 walkable mesh: the spot's part (-?\d+).*the take-off's part (-?\d+)", line2)
            if m2:
                a["spot_part"], a["take_part"] = int(m2.group(1)), int(m2.group(2))
            m2 = re.search(r"the grid: center " + V + r", (\d+) x (\d+) cells of ([\d.]+)", line2)
            if m2:
                a["grid"] = (vec(m2), int(m2.group(4)), int(m2.group(5)), float(m2.group(6)))
            m2 = re.search(r"from the take-off's (?:nearest free )?cell " + V + r"(?:, [\d.]+ off)?: (a way|no way|-)", line2)
            if m2:
                a["take_cell"], a["way"] = vec(m2), m2.group(4)
            m2 = re.search(r"5 the NPCs' mesh: (.*)$", line2)
            if m2:
                a["mesh"] = "connected" if m2.group(1) == "connected" else "cut off"
            m2 = re.search(r"6 walk-away: (.*?) \(", line2)
            if m2:
                a["walk"] = m2.group(1)
            m2 = re.search(r"=> the flight's rule: (.*); the jump's \(beside things\): (.*)$", line2)
            if m2:
                a["flight_rule"], a["jump_rule"] = m2.group(1), m2.group(2)
        out.append(a)
    return out


def flat(a, b):
    return math.hypot(a[0] - b[0], a[2] - b[2])


def main():
    args = [x for x in sys.argv[1:] if not x.startswith("--")]
    every = "--all" in sys.argv
    folder = args[0] if args else os.path.join(game_dir() or "", "BepInEx", "GK2-jumps")
    if not os.path.isdir(folder):
        sys.exit(f"no traces folder: {folder}")
    rows = []
    for name in sorted(os.listdir(folder)):
        if name.startswith("jump-") and name.endswith(".txt"):
            rows.extend(audits(os.path.join(folder, name)))
    off_grid = vouched_off = 0
    print(f"{'trace':38s} {'dist':>5s} {'take-off on grid':16s} {'mesh vouches':12s} {'grid way':8s}  jump's rule")
    for a in rows:
        g = a.get("grid")
        on = None
        if g:
            (cx, _, cz), w, d, s = g
            on = abs(a["take"][0] - cx) <= w * s / 2 and abs(a["take"][2] - cz) <= d * s / 2
        vouches = a.get("spot_part", -1) >= 0 and a.get("spot_part") == a.get("take_part")
        if on is False:
            off_grid += 1
            if not vouches:
                vouched_off += 1
        if not every and on is not False:
            continue
        cell = a.get("take_cell")
        miss = f" (its cell {flat(cell, a['take']):.2f} off)" if cell else ""
        print(f"{a['file']:38s} {flat(a['spot'], a['take']):5.2f} {('yes' if on else 'no' if on is False else '-') + miss:16s} "
              f"{'yes' if vouches else 'no':12s} {a.get('way', '-'):8s}  {a.get('jump_rule', '-')}")
    print(f"\n{len(rows)} audit(s); the take-off off the grid in {off_grid}, of those the mesh not vouching (the grid's way "
          f"decides) in {vouched_off}")


if __name__ == "__main__":
    main()
