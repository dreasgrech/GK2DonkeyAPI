"""A released-version Saddle Up cfg fixture made fit to stage in a live game for the upgrade rehearsal
(docs/release-checklist.md 3c; tools/rehearsal.sh stage-b). A fixture has every value off its default, so its keys are
on the numpad and its chosen skin is a made-up id: the copy gets the three keys a tester has (L, H, G) and, with
--skin, a real skin worn by its mount, so the carry-over of a skin's colours (0.13.0, docs/per-skin-looks.md) is
rehearsed too. The API cfg is never staged from a fixture (its [General] Enabled = false switches the API off).

    python tools/fixture-live.py tests/fixtures/released/0.12.0/dreas.gk2.saddleup.cfg out.cfg --skin donkey=donkey/saddleup-donkey-skins.hellfire
"""
import re, sys


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    src, dst = argv[0], argv[1]
    skins = []
    i = 2
    while i < len(argv):
        if argv[i] == "--skin" and i + 1 < len(argv):
            mount, skin = argv[i + 1].split("=", 1)
            skins.append((mount, skin))
            i += 2
        else:
            print("unknown argument:", argv[i])
            return 2
    raw = open(src, "rb").read().decode("utf-8-sig")
    out, n = raw, 0
    for key, value in (("MountsKey", "L"), ("RideKey", "H"), ("CartKey", "G")):
        out, c = re.subn(r"(?m)^" + key + r" = .*$", key + " = " + value, out)
        n += c
    if n != 3:
        print(f"the three keys were not all found ({n} of 3): is this a Saddle Up cfg?")
        return 1
    for mount, skin in skins:
        out, c = re.subn(r"(?m)^Skin_" + re.escape(mount) + r" = .*$", "Skin_" + mount + " = " + skin, out)
        if c != 1:
            print(f"no Skin_{mount} line in the fixture")
            return 1
        out, c = re.subn(r"(?m)^Mount = .*$", "Mount = " + mount, out)
    open(dst, "wb").write(out.encode("utf-8"))
    print(f"{dst}: keys L/H/G" + (", " + ", ".join(f"{m} wears {s}" for m, s in skins) if skins else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
