"""Saddle Up's external mount packs (packs/<Pack>/, docs/making-a-mount.md "Built in or external") in the game.

    python -I tools/packs.py status    each pack: linked, a real folder (an unzipped copy), or missing
    python -I tools/packs.py link      makes BepInEx\\plugins\\<Pack> a junction to packs\\<Pack> where there is none
    python -I tools/packs.py unlink    removes the junctions (the link only, never the files behind it)

A dev build links them by itself (SaddleUp.csproj, LinkPacks). Through the junction the game reads the repo's files:
the F11 Mounts tab's Save and the auto-reload work on packs/ directly. Unlink before unzipping a release zip into
plugins\\ (the zip rehearsal, docs/release-checklist.md): extracting into a junction writes into the repo.
"""
import os, subprocess, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "skins"))
import gamedir  # noqa: E402

REPO = gamedir.REPO
PACKS = os.path.join(REPO, "packs")


def packs():
    return sorted(d for d in os.listdir(PACKS) if os.path.isdir(os.path.join(PACKS, d))) if os.path.isdir(PACKS) else []


def plugins():
    return os.path.join(gamedir.game_dir(), "BepInEx", "plugins")


def state(name):
    there = os.path.join(plugins(), name)
    if os.path.isjunction(there) or os.path.islink(there):
        return "linked" if os.path.normcase(os.path.realpath(there)) == os.path.normcase(os.path.realpath(os.path.join(PACKS, name))) else "linked elsewhere"
    if os.path.isdir(there):
        return "a real folder (an unzipped copy?)"
    return "missing"


def link():
    bad = 0
    for name in packs():
        st = state(name)
        if st == "linked":
            print(f"ok    {name}: linked")
            continue
        if st != "missing":
            print(f"WARN  {name}: {st}; left alone (move it away to link the repo's)")
            bad += 1
            continue
        there = os.path.join(plugins(), name)
        r = subprocess.run(["cmd", "/c", "mklink", "/J", there, os.path.join(PACKS, name)], capture_output=True, text=True)
        print(("ok    " if r.returncode == 0 else "FAIL  ") + f"{name}: {'linked' if r.returncode == 0 else r.stderr.strip() or r.stdout.strip()}")
        bad += r.returncode != 0
    return bad


def unlink():
    for name in packs():
        there = os.path.join(plugins(), name)
        if os.path.isjunction(there) or os.path.islink(there):
            os.rmdir(there)  # the link only: rmdir of a junction never touches what it points to
            print(f"ok    {name}: unlinked")
        else:
            print(f"      {name}: {state(name)}")
    return 0


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "status"
    if cmd == "link":
        return 1 if link() else 0
    if cmd == "unlink":
        return unlink()
    for name in packs():
        print(f"{name}: {state(name)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
