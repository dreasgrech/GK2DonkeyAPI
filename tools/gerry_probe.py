"""Gerry's sprites out of the game's bundles, for looking at (docs/gerry-mount.md): lists every 0901_* sprite in the
character atlas bundle (name, size, pivot, pixels per unit), the parts and animations, and exports each frame as a
PNG (trimmed to the drawn pixels, as the atlas packs them). The exports are the game's art: keep them out of the
repository.

    pip install UnityPy
    python tools/gerry_probe.py "<game>\\GraveyardKeeper2_Data\\StreamingAssets\\aa\\StandaloneWindows64" <out folder>

The game's bundles carry no Unity version string, so UnityPy needs the fallback set (the game is Unity 6000.3.9f1).
Another preset: change PREFIX (the donkey is 0301_, Gerry 0901_) and, if its frames are in another bundle, find it
with grep -l on the bundle folder first.
"""
import os
import sys

import UnityPy

UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.9f1"

PREFIX = "0901_"
ATLAS_BUNDLE = "8f5cc77be26057b5c166ea2465c7902f.bundle"  # the Characters atlas (donkey, Gerry, ...)


def main():
    folder, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    env = UnityPy.load(os.path.join(folder, ATLAS_BUNDLE))
    sprites = []
    for obj in env.objects:
        if obj.type.name != "Sprite":
            continue
        d = obj.read()
        if not d.m_Name.startswith(PREFIX):
            continue
        r = d.m_Rect
        sprites.append((d.m_Name, int(r.width), int(r.height), float(d.m_Pivot.x), float(d.m_Pivot.y), float(d.m_PixelsToUnits)))
        d.image.save(os.path.join(out, d.m_Name + ".png"))
    sprites.sort()
    print("sprites:", len(sprites))
    for s in sprites:
        print("  %-40s %3dx%-3d pivot %.3f,%.3f ppu %.0f" % s)
    names = [s[0] for s in sprites]
    parts = sorted({n.split("_")[1] for n in names})
    anims = sorted({"_".join(n.split("_")[2:-1]) if n.split("_")[-1].isdigit() else "_".join(n.split("_")[2:]) for n in names})
    print("parts:", parts)
    print("animations:", anims)
    print("exported to", out)


if __name__ == "__main__":
    main()
