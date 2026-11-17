"""The audit of the self-tests' move from the API's own runner (F11's Tests tab) to GK2 Mod Test Bench: every scenario
of the last commit that had the old runner against what the bench was given and what it ran, one by one.

    python tools/selftest-migration-audit.py <old commit> <the bench run's file> <the old runner's kept counts> [--table out.md] [--old-log a.log] [--steps]

- <old commit>: the last commit with the old runner (a6d45c4).
- <the bench run's file>: a run of the suite "GK2 Donkey API" alone, from BepInEx\\GK2ModTestBench-runs.
- <the old runner's kept counts>: BepInEx\\GK2-selftest-counts.tsv as tools/log-scenarios.py --keep wrote it after a
  full run of the old runner (a copy made before the first bench run was kept over it).

What it checks, and says for each (nothing is taken as read):
 1. The lists: every `new Scenario { ... }` entry of the two list files, at the old commit and now, parsed whole
    (group, name, body, the hides-the-panel flag); an entry the parser can't read is a failure. Old and new entry by
    entry.
 2. The order: the old runner's run order from the old lists (its AddScenarios, then its Run, which put the checks
    of the whole run last and the world's check the very last) against the order the bench ran them in, read from
    the run's file; and that each scenario's lines stand together in the file (no scenario ran inside another).
 3. The bodies: every source file of the tests, at the old commit and now. A file that differs is listed with each
    differing line; for the runner's file the lines outside the runner's own three blocks must be the same.
 4. The checks: each scenario's PASS, FAIL and WARN lines in the bench's run against the old runner's kept count; a
    scenario with no check in either is listed with the other lines it left, so it is seen to have run.
 5. The run as a whole: failures, warnings, skips, throws, the lines that belong to no scenario.
 6. With --old-log (a log that holds a whole run of the old runner, such as Unity's Player-prev.log of an earlier
    session): the order the scenarios ran in there against the bench's, and each scenario's checks word for word
    (then with the measured numbers masked), and each scenario's number of INFO and SKIP lines. That log may be of
    an older build: a scenario whose own code changed since, or that measures another world, is listed, not failed.
 7. The run's own steps: the statements of the old Run, Drive and check methods against the steps the bench's hooks
    call now (HoldRun to the check methods): how many are there word for word, and whether those stand in the same
    order. --steps lists every statement only one side has (the runner's mechanics that went to the bench, the
    locals that became fields); they are for reading, not failed.
Exit 1 when anything differs. --table writes the scenario-by-scenario table as Markdown.
"""
import collections
import difflib
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LISTS = ("src/GK2DonkeyAPI/Dev/SelfTest.cs", "tools/GK2DonkeyAPI.IntegrationTests/Tests.cs")
TEST_DIRS = ("src/GK2DonkeyAPI/Dev", "tools/GK2DonkeyAPI.IntegrationTests")
AFTER = "the world's own donkeys came through unchanged, no test donkey left behind"
# an entry's body is a method (Body = Reuse, Body = GateTests.Scopes) or a call wrapped for its argument
# (Body = () => Wake(switchOff: true)); the two shapes are all there are, and an entry of another shape is said
ENTRY = re.compile(r'new Scenario \{ Group = (\w+), Name = ("(?:[^"\\]|\\.)*"|AfterRunName), Body = ((?:\(\) => )?[\w.]+(?:\([^)]*\))?)(, HidesPanel = true)? \}')
# the old runner's own code in SelfTest.cs at a6d45c4 (1-based, inclusive): the header comment, the runner's fields
# and the Scenario class, Run / Drive / the check methods. Everything else in that file is scenarios and helpers.
RUNNER_BLOCKS = ((12, 20), (23, 98), (293, 607))
# the steps that took the old Run's place stand between these two comments in today's SelfTest.cs
STEPS_FROM = "// ---- the run's own state, between StartRun and FinishRun"
STEPS_TO = "// ------------------------------------------------------------------ helpers"


def git(*args):
    return subprocess.run(["git", "-C", REPO] + list(args), capture_output=True, text=True, encoding="utf-8", check=True).stdout


def old(commit, rel):
    return git("show", f"{commit}:{rel}")


def now(rel):
    return open(os.path.join(REPO, rel), encoding="utf-8-sig", newline="").read().replace("\r\n", "\n")


def entries(text, what, problems):
    found = [(m.group(1), AFTER if m.group(2) == "AfterRunName" else m.group(2)[1:-1].replace('\\"', '"').replace("\\\\", "\\"), m.group(3), bool(m.group(4)))
             for m in ENTRY.finditer(text)]
    written = text.count("new Scenario {")
    if written != len(found):
        problems.append(f"{what}: {written} entries written, {len(found)} read whole")
    return found


def merged(api, more):
    """The old AddScenarios: each after the last of its group, a new group before the After group."""
    out = list(api)
    for e in more:
        at = max((i for i, x in enumerate(out) if x[0] == e[0]), default=-1)
        if at < 0:
            at = next(i for i, x in enumerate(out) if x[0] == "GroupAfter") - 1
        out.insert(at + 1, e)
    return out


def run_order(all_entries):
    """The old Run: the list's order, the After group's taken out and put last, the world's check the very last."""
    rest = [e for e in all_entries if e[0] != "GroupAfter"]
    after = [e for e in all_entries if e[0] == "GroupAfter" and e[1] != AFTER]
    last = [e for e in all_entries if e[1] == AFTER]
    return rest + after + last


def code_lines(lines):
    """The statements of a stretch of C#: comments, blank lines and lone braces left out, whitespace as one space."""
    out = []
    for l in lines:
        s = re.sub(r"\s+", " ", l.strip())
        if not s.startswith('"'):
            s = re.sub(r"\s*//.*$", "", s)
        if s and not s.startswith("//") and s not in ("{", "}"):
            out.append(s)
    return out


def main(argv):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    table = None
    if "--table" in argv:
        i = argv.index("--table")
        table = argv[i + 1]
        del argv[i:i + 2]
    steps = "--steps" in argv
    if steps:
        argv.remove("--steps")
    old_log = None
    if "--old-log" in argv:
        i = argv.index("--old-log")
        old_log = argv[i + 1]
        del argv[i:i + 2]
    if len(argv) != 3:
        sys.exit(__doc__)
    commit, run_file, kept_file = argv
    problems = []

    # 1. the lists
    old_lists = [entries(old(commit, f), f"{f} at {commit}", problems) for f in LISTS]
    new_lists = [entries(now(f), f"{f} now", problems) for f in LISTS]
    print(f"1. The lists: {len(old_lists[0])} + {len(old_lists[1])} = {len(old_lists[0]) + len(old_lists[1])} scenarios at {commit}; {len(new_lists[0])} + {len(new_lists[1])} = {len(new_lists[0]) + len(new_lists[1])} now")
    for f, a, b in zip(LISTS, old_lists, new_lists):
        if a == b:
            print(f"   {f}: every entry the same (group, name, body, the hides-the-panel flag), in the same place")
        else:
            problems.append(f"{f}: the list differs")
            for k, (x, y) in enumerate(zip(a, b)):
                if x != y:
                    print(f"   DIFFERS at entry {k + 1}: {x} / {y}")
            if len(a) != len(b):
                print(f"   DIFFERS in length: {len(a)} / {len(b)}")
    old_all = merged(old_lists[0], old_lists[1])
    names = [e[1] for e in old_all]
    twice = sorted({n for n in names if names.count(n) > 1})
    if twice:
        problems.append(f"{len(twice)} name(s) used twice: {twice[:3]}")
    hidden = [e[1] for e in old_all if e[3]]
    print(f"   {len(names)} names, {len(set(names))} different; {len(hidden)} hide the panel: {'; '.join(n[:60] for n in hidden) or 'none'}")

    # 2. the order
    expected = [e[1] for e in run_order(old_all)]
    log = open(run_file, encoding="utf-8", errors="replace").read()
    by_length = sorted(names, key=lambda n: -len(n))
    kinds = collections.defaultdict(collections.Counter)
    ran, last, split, strays = [], None, [], collections.Counter()
    for m in re.finditer(r"\[test\] (PASS|FAIL|WARN|INFO|SKIP) ([^\n]*)", log):
        kind, rest = m.group(1), m.group(2)
        name = next((n for n in by_length if rest.startswith(n + ":")), None)
        if name is None:
            strays[kind + " " + rest.split(":")[0][:60]] += 1
            continue
        kinds[name][kind] += 1
        if name != last:
            if name in ran:
                split.append(name)
            else:
                ran.append(name)
            last = name
    print(f"2. The order: {len(expected)} scenarios in the old runner's order; {len(ran)} left lines in the bench's run")
    if ran == expected:
        print("   the bench ran them in exactly the old runner's order, every one")
    else:
        problems.append("the run order differs")
        for k, (a, b) in enumerate(zip(expected, ran)):
            if a != b:
                print(f"   DIFFERS first at {k + 1}: old {a[:70]} / ran {b[:70]}")
                break
        for n in expected:
            if n not in ran:
                print(f"   NO LINE: {n[:100]}")
    if split:
        problems.append(f"{len(split)} scenario(s) with lines in two places")
        print("   LINES IN TWO PLACES:", "; ".join(n[:60] for n in split))
    else:
        print("   each scenario's lines stand together: none ran inside another")

    # 3. the bodies
    print(f"3. The bodies: every source file of the tests at {commit} and now")
    changed = [l.split("\t") for l in git("diff", "--name-status", commit, "HEAD", "--", *TEST_DIRS).splitlines() if l]
    files_old = [f for f in git("ls-tree", "-r", "--name-only", commit, "--", *TEST_DIRS).splitlines() if f.endswith(".cs")]
    print(f"   {len(files_old)} .cs files then; {len(files_old) - sum(1 for c in changed if c[1].endswith('.cs') and c[0] != 'A')} of them the same byte for byte now")
    for status, path in changed:
        if not path.endswith(".cs"):
            print(f"   {status} {path} (no test code)")
            continue
        if status == "A":
            print(f"   added: {path}")
            continue
        if status == "D":
            print(f"   deleted: {path}")
            continue
        a, b = old(commit, path).splitlines(), now(path).splitlines()
        outside = []
        for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes():
            if tag == "equal":
                continue
            in_runner = path == LISTS[0] and any(lo <= i1 + 1 and i2 <= hi for lo, hi in RUNNER_BLOCKS)
            if not in_runner:
                outside.append((i1, i2, j1, j2))
        where = "outside the runner's own blocks" if path == LISTS[0] else ""
        print(f"   changed: {path}: {len(outside)} place(s) {where}".rstrip())
        for i1, i2, j1, j2 in outside:
            # a long place (the Tests tab's own drawing, which went) by its first lines and its length
            for k, l in enumerate(a[i1:i2][:4]):
                print(f"      - {i1 + 1 + k}: {l.strip()[:150]}")
            if i2 - i1 > 4:
                print(f"      - ... {i2 - i1 - 4} more line(s) taken out, to line {i2}")
            for k, l in enumerate(b[j1:j2][:4]):
                print(f"      + {j1 + 1 + k}: {l.strip()[:150]}")
            if j2 - j1 > 4:
                print(f"      + ... {j2 - j1 - 4} more line(s) put in, to line {j2}")

    # 4. the checks
    kept = collections.Counter()
    for line in open(kept_file, encoding="utf-8"):
        if "\t" in line and not line.startswith("#"):
            n, name = line.rstrip("\n").split("\t", 1)
            kept[name] = int(n)
    unknown = [n for n in kept if n not in names]
    if unknown:
        problems.append(f"{len(unknown)} kept count(s) of no scenario")
    rows, differ, none = [], 0, []
    for k, name in enumerate(expected):
        c = kinds[name]
        checks = c["PASS"] + c["FAIL"] + c["WARN"]
        same = checks == kept[name]
        differ += 0 if same else 1
        if checks == 0 and kept[name] == 0:
            none.append((name, c["INFO"], c["SKIP"]))
        e = next(x for x in old_all if x[1] == name)
        rows.append((k + 1, e[0].replace("Group", ""), name, e[2], kept[name], checks, c["PASS"], c["FAIL"], c["WARN"], c["INFO"], c["SKIP"], same))
    total_old, total_new = sum(kept.values()), sum(r[5] for r in rows)
    print(f"4. The checks: {total_old} under the old runner, {total_new} under the bench; {differ} scenario(s) differ")
    for r in rows:
        if not r[11]:
            print(f"   DIFFERS: {r[4]} -> {r[5]}  {r[2][:110]}")
    if differ:
        problems.append(f"{differ} scenario(s) make another number of checks")
    print(f"   {len(none)} scenario(s) make no check under either runner (measurements); the lines each left in the bench's run:")
    for name, info, skip in none:
        print(f"      {info} INFO, {skip} SKIP  {name[:110]}")
        if info + skip == 0:
            problems.append(f"no line at all: {name[:80]}")

    # 5. the run as a whole
    fails = sum(r[7] for r in rows)
    warns = sum(r[8] for r in rows)
    skips = sum(r[10] for r in rows)
    threw = len(re.findall(r"\[test\] FAIL [^\n]* threw ", log))
    done = re.search(r"\[test\] (?:done|aborted)[^\n]*", log)
    print(f"5. The run: {fails} failed, {warns} warned, {skips} skipped, {threw} threw; {done.group(0) if done else 'NO SUMMARY LINE'}")
    print(f"   lines of no scenario (the run's own start and finish): {sum(strays.values())}")
    for s, n in sorted(strays.items()):
        print(f"      {n} x {s}")
    if fails or warns or threw or not done or "aborted" in (done.group(0) if done else ""):
        problems.append("the run was not clean")

    # 6. an old runner's own log, line by line (any full run of it that survives: Unity's Player-prev.log)
    if old_log:
        text = open(old_log, encoding="utf-8", errors="replace").read()
        o_checks, o_order, o_last = collections.defaultdict(list), [], None
        for m in re.finditer(r"\[test\] (PASS|FAIL|WARN) ([^\n]*)", text):
            rest = m.group(2).rstrip("\r")
            name = next((n for n in by_length if rest.startswith(n + ":")), None)
            if name is None:
                continue
            o_checks[name].append(m.group(1) + rest[len(name):])
            if name != o_last and name not in o_order:
                o_order.append(name)
            o_last = name
        n_checks = collections.defaultdict(list)
        for m in re.finditer(r"\[test\] (PASS|FAIL|WARN) ([^\n]*)", log):
            rest = m.group(2).rstrip("\r")
            name = next((n for n in by_length if rest.startswith(n + ":")), None)
            if name is not None:
                n_checks[name].append(m.group(1) + rest[len(name):])
        done_old = re.search(r"\[test\] (?:done|aborted)[^\n]*", text)
        both = [n for n in expected if n in o_checks]
        print(f"6. An old runner's own log ({os.path.basename(old_log)}; {done_old.group(0).strip() if done_old else 'no summary'}): {len(o_order)} of today's scenarios made checks in it")
        new_order = [n for n in ran if n in o_checks]
        if [n for n in o_order] == new_order:
            print(f"   the {len(new_order)} scenarios both runs have ran in the same order under the old runner and under the bench")
        else:
            problems.append("the order differs from the old runner's own log")
            for k, (a, b) in enumerate(zip(o_order, new_order)):
                if a != b:
                    print(f"   ORDER DIFFERS first at {k + 1}: old log {a[:60]} / bench {b[:60]}")
                    break
        mask = lambda s: re.sub(r"-?\d+(?:[.,]\d+)*", "#", s)  # the numbers a check measures move from run to run
        same_n = same_text = same_masked = 0
        other = []
        for n in both:
            a, b = o_checks[n], n_checks[n]
            if len(a) == len(b):
                same_n += 1
                if a == b:
                    same_text += 1
                elif [mask(x) for x in a] == [mask(x) for x in b]:
                    same_masked += 1
                else:
                    other.append((n, len(a), len(b), sum(1 for x, y in zip(a, b) if mask(x) != mask(y))))
            else:
                other.append((n, len(a), len(b), -1))
        print(f"   of those {len(both)}: {same_n} make the same number of checks in both; {same_text} say every check in the very same words, {same_masked} more in the same words but for the numbers measured")
        print(f"   {len(other)} differ in the words or the number of their checks (the scenarios' own code changed between the two runs' dates, or what they say holds a name that moves):")
        for n, la, lb, d in other:
            print(f"      {la} -> {lb} checks{'' if d < 0 else f', {d} worded otherwise'}  {n[:100]}")
        # the lines that are no checks (what a scenario measured or found, a skip with its reason), counted per scenario
        o_notes = collections.defaultdict(collections.Counter)
        for m in re.finditer(r"\[test\] (INFO|SKIP) ([^\n]*)", text):
            rest = m.group(2).rstrip("\r")
            name = next((n for n in by_length if rest.startswith(n + ":")), None)
            if name is not None:
                o_notes[name][m.group(1)] += 1
        note_differs = [(n, o_notes[n], kinds[n]) for n in expected
                        if n in o_checks or n in o_notes
                        if (o_notes[n]["INFO"], o_notes[n]["SKIP"]) != (kinds[n]["INFO"], kinds[n]["SKIP"])]
        ran_old = sum(1 for n in expected if n in o_checks or n in o_notes)
        print(f"   INFO and SKIP lines: {ran_old - len(note_differs)} of the {ran_old} scenarios that left a line in that log left the same number of each in the bench's run; {len(note_differs)} differ (measurements: what they find depends on the save and the build):")
        for n, a, b in note_differs:
            print(f"      INFO {a['INFO']} -> {b['INFO']}, SKIP {a['SKIP']} -> {b['SKIP']}  {n[:100]}")
        absent = [n for n in expected if n not in o_checks and n not in o_notes]
        print(f"   {len(absent)} of today's scenarios left no line in that log (added to the list after it; the kept counts of the old runner's last run, step 4, cover them):")
        for n in absent:
            print(f"      {kept[n]} check(s) under the old runner's last run, {kinds[n]['PASS'] + kinds[n]['FAIL'] + kinds[n]['WARN']} under the bench  {n[:100]}")

    # 7. the run's own steps: the old Run / Drive / check methods against the steps the bench's hooks call now
    a_code = code_lines(old(commit, LISTS[0]).splitlines()[RUNNER_BLOCKS[2][0] - 1:RUNNER_BLOCKS[2][1]])
    b_all = now(LISTS[0]).splitlines()
    try:
        lo = next(i for i, l in enumerate(b_all) if STEPS_FROM in l)
        hi = next(i for i, l in enumerate(b_all) if STEPS_TO in l)
    except StopIteration:
        lo = hi = 0
        problems.append("the new steps' block not found in SelfTest.cs (its two marker comments)")
    b_code = code_lines(b_all[lo:hi])
    shared = set(a_code) & set(b_code)
    a_shared, b_shared = [s for s in a_code if s in shared], [s for s in b_code if s in shared]
    print(f"7. The run's own steps: the old runner {len(a_code)} code lines, the new steps {len(b_code)}; {len(a_shared)} of the old are in the new word for word")
    if a_shared == b_shared:
        print(f"   those {len(a_shared)} stand in the same order in both, each as many times")
    else:
        moved = [(tag, a_shared[i1:i2], b_shared[j1:j2]) for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, a_shared, b_shared, autojunk=False).get_opcodes() if tag != "equal"]
        print(f"   in another order or another number of times, {len(moved)} place(s) (each to be read: a statement the old loop ran once for each scenario or in another step):")
        for tag, x, y in moved:
            for s in x:
                print(f"      old here: {s[:170]}")
            for s in y:
                print(f"      new here: {s[:170]}")
    print(f"   {len(a_code) - len(a_shared)} old line(s) are not in the new steps, {len(b_code) - len(b_shared)} new line(s) were not in the old{'' if steps else ' (--steps lists them)'}")
    if steps:
        for s in a_code:
            if s not in shared:
                print(f"      - {s[:200]}")
        for s in b_code:
            if s not in shared:
                print(f"      + {s[:200]}")

    if table:
        with open(table, "w", encoding="utf-8", newline="\n") as f:
            f.write("| # | Group | Scenario | Body | Checks, old runner | Checks, bench | PASS | FAIL | WARN | INFO | SKIP | Same |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n")
            for r in rows:
                f.write(f"| {r[0]} | {r[1]} | {r[2].replace('|', '/')} | `{r[3]}` | {r[4]} | {r[5]} | {r[6]} | {r[7]} | {r[8]} | {r[9]} | {r[10]} | {'yes' if r[11] else '**NO**'} |\n")
        print(f"the table of all {len(rows)} scenarios is in {table}")
    print("AUDIT: every scenario accounted for, nothing differs" if not problems else "AUDIT: " + "; ".join(problems))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
