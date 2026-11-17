"""After a run of the scenarios (GK2 Mod Test Bench, F4: Run on the suite "GK2 Donkey API"; until 2026-11-17 the
F11 panel's Tests tab): which scenarios left no line in the game's log (a scenario that never ran), how many
warnings or errors the log has before the run started (a startup warning is never the tests' doing), and how many
checks each scenario made, against the run before.

    python tools/log-scenarios.py                # the game's BepInEx\\LogOutput.log (the game folder from Directory.Build.props)
    python tools/log-scenarios.py some.log       # a saved log, or a run's own file from BepInEx\\GK2ModTestBench-runs
    python tools/log-scenarios.py --diff old.log [new.log]   # the scenarios whose number of checks differs between two logs
    python tools/log-scenarios.py --keep         # after the usual report: remember this run's counts for the next comparison

The scenario names are read out of the two scenario lists (src/GK2DonkeyAPI/Dev/SelfTest.cs and the integration tests'
Tests.cs). Exit code 1 when a scenario has no line.

The counts (since 2026-11-17: a full run came back green with 13 checks fewer than the one before, and nothing could
say which, the game's log being overwritten at every start): a scenario's checks are its PASS, FAIL and WARN lines.
Their number depends on the code and on the save (a scenario over the world's own babies makes none with one baby
out), so a total that moved is not by itself a fault, but a scenario that makes fewer checks than last time is worth
a look. Without arguments the report compares the log with the counts kept last (BepInEx\\GK2-selftest-counts.tsv in
the game folder, written by --keep; never by a plain run), when there are any. Unity's own Player-prev.log
(%USERPROFILE%\\AppData\\LocalLow\\Lazy Bear Games\\Graveyard Keeper 2) holds the session before the last one and can
be given to --diff.

The lines are the same under either runner ("[test] PASS <scenario>: <what>"), so counts kept before the move to the
bench compare with runs after it. Run the API's suite alone for a comparison: a run of every suite has the other
mods' tests in the log too, and each shows here as a line of no scenario of this repository's lists.
"""
import collections
import os
import re
import sys
import time

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KEPT = "GK2-selftest-counts.tsv"


def game_dir():
    props = open(os.path.join(REPO, "Directory.Build.props"), encoding="utf-8").read()
    m = re.search(r"<GameDir[^>]*>([^<]+)</GameDir>", props)
    return m.group(1).strip() if m else None


def scenario_names():
    names = []
    for f in ("src/GK2DonkeyAPI/Dev/SelfTest.cs", "tools/GK2DonkeyAPI.IntegrationTests/Tests.cs"):
        t = open(os.path.join(REPO, f), encoding="utf-8").read()
        for m in re.finditer(r'new Scenario \{ Group = (\w+), Name = ("(?:[^"\\]|\\.)*"|AfterRunName)', t):
            n = m.group(2)
            if n == "AfterRunName":
                n = "the world's own donkeys came through unchanged, no test donkey left behind"
            else:
                n = n[1:-1].replace('\\"', '"').replace("\\\\", "\\")
            names.append((f.split("/")[0], n))
    return names


def counts_of(log, names):
    """{scenario name: its PASS, FAIL and WARN lines} of a log's text; a line of no scenario of today's lists under
    its own first words."""
    by_length = sorted((n for _, n in names), key=lambda n: -len(n))
    counts = collections.Counter()
    for m in re.finditer(r"\[test\] (?:PASS|FAIL|WARN) ([^\n]*)", log):
        rest = m.group(1)
        for n in by_length:
            if rest.startswith(n):
                counts[n] += 1
                break
        else:
            counts["(no scenario of today's lists) " + rest.split(":")[0][:70]] += 1
    return counts


def compare(before, after, what):
    """Prints the scenarios whose number of checks differs; returns how many make fewer."""
    fewer = 0
    lines = []
    for n in sorted(set(before) | set(after), key=lambda n: (after[n] - before[n], n)):
        d = after[n] - before[n]
        if d == 0:
            continue
        fewer += 1 if d < 0 else 0
        lines.append(f"  {d:+4}  ({before[n]} -> {after[n]})  {n[:130]}")
    print(f"checks: {sum(before.values())} {what}, {sum(after.values())} now; {len(lines)} scenario(s) differ, {fewer} make fewer")
    for line in lines:
        print(line)
    return fewer


def read_kept(path):
    counts = collections.Counter()
    when = "?"
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if line.startswith("# "):
            when = line[2:]
        elif "\t" in line:
            n, name = line.split("\t", 1)
            counts[name] = int(n)
    return counts, when


def main(argv):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    keep = "--keep" in argv
    names = scenario_names()
    game = game_dir() or ""
    if "--diff" in argv:
        files = [a for a in argv if not a.startswith("--")]
        if not files:
            sys.exit("--diff needs the older log (and the newer one, else the game's LogOutput.log)")
        newer = files[1] if len(files) > 1 else os.path.join(game, "BepInEx", "LogOutput.log")
        before = counts_of(open(files[0], encoding="utf-8", errors="replace").read(), names)
        after = counts_of(open(newer, encoding="utf-8", errors="replace").read(), names)
        compare(before, after, "in " + files[0])
        return 0
    files = [a for a in argv if not a.startswith("--")]
    path = files[0] if files else os.path.join(game, "BepInEx", "LogOutput.log")
    log = open(path, encoding="utf-8", errors="replace").read()
    missing = [n for n in names if not re.search(r"\[test\] (PASS|FAIL|WARN|INFO|SKIP)[^\n]*" + re.escape(n[1]) + ":", log)]
    print(f"{len(names)} scenarios, {len(missing)} with no line in {path}")
    for src, n in missing:
        print("  ", src, "|", n)
    at = log.find("[test] start")
    startup = log[:at] if at >= 0 else log
    print("startup warnings/errors:", len(re.findall(r"\[(Warning|Error)", startup)))
    now = counts_of(log, names)
    kept = os.path.join(game, "BepInEx", KEPT)
    if os.path.isfile(kept):
        before, when = read_kept(kept)
        compare(before, now, "kept " + when)
    else:
        print(f"checks: {sum(now.values())} now; no counts kept yet (--keep writes {kept})")
    if keep:
        done = re.search(r"\[test\] (?:done|aborted)[^\n]*", log)  # "done: ...", and the bench's "done (187 of 204 tests): ..." for one suite
        with open(kept, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"# {time.strftime('%Y-%m-%d %H:%M', time.localtime(os.path.getmtime(path)))}, {done.group(0) if done else 'no summary line'}\n")
            for n in sorted(now):
                f.write(f"{now[n]}\t{n}\n")
        print(f"kept: {sum(now.values())} checks of {len(now)} scenarios in {kept}")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
