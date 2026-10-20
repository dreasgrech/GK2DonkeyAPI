"""Gerry's sprites out of the game's bundles, for looking at (docs/gerry-mount.md): lists every 0901_* sprite in the
character atlas bundle (name, size, pivot, pixels per unit), the parts and animations, and exports each frame as a
PNG (trimmed to the drawn pixels, as the atlas packs them). The exports are the game's art: keep them out of the
repository.

    pip install UnityPy
    python tools/skins/gerry/gerry_probe.py "<game>\\GraveyardKeeper2_Data\\StreamingAssets\\aa\\StandaloneWindows64" <out folder>

The game's bundles carry no Unity version string, so UnityPy needs the fallback set (the game is Unity 6000.3.9f1).
Another preset: change PREFIX (the donkey is 0301_, Gerry 0901_) and, if its frames are in another bundle, find it
with grep -l on the bundle folder first.
"""
import os
import sys

import UnityPy

UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.9f1"

PREFIX = sys.argv[3] if len(sys.argv) > 3 else "0901_"  # the character's preset prefix on its sprites (Gerry: 0901_)
MARK = "bdy_walk_left_01"                                   # the sprite every character has: the bundle holding <prefix>MARK is the atlas


def find_bundle(folder):
    """The addressables bundle that holds the character's sprites, by content (the bundle names change with the game)."""
    import glob
    for path in sorted(glob.glob(os.path.join(folder, "**", "*.bundle"), recursive=True), key=os.path.getsize, reverse=True):
        try:
            env = UnityPy.load(path)
            for obj in env.objects:
                if obj.type.name == "Sprite" and obj.read().m_Name == PREFIX + MARK:
                    return path
        except Exception:
            continue
    return None


def main():
    folder, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    bundle = find_bundle(folder)
    if bundle is None:
        print(f"no bundle under {folder} holds a sprite named {PREFIX}{MARK}"); return
    print("atlas:", bundle)
    env = UnityPy.load(bundle)
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
    # usage: python tools/skins/gerry/gerry_probe.py <game folder> <out folder> [<prefix>]


if __name__ == "__main__":
    main()
