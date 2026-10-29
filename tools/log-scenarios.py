"""After an F11 run: which self-test scenarios left no line in the game's log (a scenario that never ran), and how many
warnings or errors the log has before the run started (a startup warning is never the tests' doing).

    python tools/log-scenarios.py                # the game's BepInEx\\LogOutput.log (the game folder from Directory.Build.props)
    python tools/log-scenarios.py some.log       # a saved log

The scenario names are read out of the two scenario lists (src/GK2DonkeyAPI/Dev/SelfTest.cs and the integration tests'
Tests.cs). Exit code 1 when a scenario has no line.
"""
import os, re, sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


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


def main(argv):
    path = argv[0] if argv else os.path.join(game_dir() or "", "BepInEx", "LogOutput.log")
    log = open(path, encoding="utf-8", errors="replace").read()
    names = scenario_names()
    missing = [n for n in names if not re.search(r"\[test\] (PASS|FAIL|WARN|INFO|SKIP)[^\n]*" + re.escape(n[1]) + ":", log)]
    print(f"{len(names)} scenarios, {len(missing)} with no line in {path}")
    for src, n in missing:
        print("  ", src, "|", n)
    at = log.find("[test] start")
    startup = log[:at] if at >= 0 else log
    print("startup warnings/errors:", len(re.findall(r"\[(Warning|Error)", startup)))
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
