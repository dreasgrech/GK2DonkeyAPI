"""The game's UI sounds as its prefabs name them (docs/mounts-panel-design.md, "Sounds"), to check after a game update
that the ids our windows play (UiSounds.cs) are still the game's own.

    python tools/ui-sounds-scan.py                  # the game from Directory.Build.props
    python tools/ui-sounds-scan.py <game folder>

Prints each window that names an open/close sound (LazyWindow.openSoundId / closeSoundId), then the buttons' sound
patterns (LazyButton.on*Sound) with a count and example names. Read only. Needs UnityPy (pip install UnityPy); about
half a minute over the Addressables bundles.
"""
import os
import re
import sys
from collections import defaultdict
from concurrent.futures import ProcessPoolExecutor

UNITY = "6000.3.9f1"  # the game's Unity (LogOutput.log); the bundles carry none
WINDOW_IDS = [b"win_open", b"win_close", b"chest_open", b"chest_close", b"bag_open", b"bag_close"]
BUTTON_IDS = [b"gui_click", b"gui_hover", b"gui_hover_light", b"tab_click"]
BUTTON_FIELDS = ["onEnterSound", "onExitSound", "onDownSound", "onUpSound", "onClickSound", "onNotInteractableEnterSound", "onNotInteractableClickSound"]
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def game_dir():
    props = open(os.path.join(REPO, "Directory.Build.props"), encoding="utf-8").read()
    return re.search(r"<GameDir[^>]*>([^<]+)</GameDir>", props).group(1)


def first_pass(path):
    """(bundle, gameobject name, window ids found, button ids found) for each MonoBehaviour naming one."""
    import UnityPy
    UnityPy.config.FALLBACK_UNITY_VERSION = UNITY
    out = []
    try:
        env = UnityPy.load(path)
    except Exception:
        return out
    for o in env.objects:
        if o.type.name != "MonoBehaviour":
            continue
        try:
            raw = o.get_raw_data()
        except Exception:
            continue
        win = [i.decode() for i in WINDOW_IDS if i in raw]
        btn = [i.decode() for i in BUTTON_IDS if i in raw]
        if not win and not btn:
            continue
        name = "?"
        try:
            name = o.read().m_GameObject.read().m_Name
        except Exception:
            pass
        out.append((path, name, win, btn))
    return out


def main():
    import warnings
    warnings.filterwarnings("ignore")
    game = sys.argv[1] if len(sys.argv) > 1 else game_dir()
    aa = os.path.join(game, "GraveyardKeeper2_Data", "StreamingAssets", "aa", "StandaloneWindows64")
    if not os.path.isdir(aa):
        sys.exit(f"no Addressables folder at {aa}")
    files = [os.path.join(aa, f) for f in os.listdir(aa) if f.endswith(".bundle")]
    hits = []
    with ProcessPoolExecutor(max_workers=max(1, (os.cpu_count() or 2) - 1)) as ex:
        for r in ex.map(first_pass, files, chunksize=64):
            hits.extend(r)

    print("Windows naming an open/close sound:")
    windows = sorted({(n, ",".join(w)) for _, n, w, _ in hits if w})
    for n, w in windows:
        print(f"  {n}: {w}")
    print(f"  ({len(windows)})")

    # the buttons: their fields by name, read with the type tree the game's DLLs give
    import UnityPy
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    UnityPy.config.FALLBACK_UNITY_VERSION = UNITY
    gen = TypeTreeGenerator(UNITY)
    gen.load_local_game(game)
    node = gen.get_nodes_up("LazyBearTechnology.dll", "LazyBearTechnology.LazyButton")
    patterns = defaultdict(set)
    for b in sorted({p for p, _, _, btn in hits if btn}):
        env = UnityPy.load(b)
        for o in env.objects:
            if o.type.name != "MonoBehaviour":
                continue
            raw = o.get_raw_data()
            if not any(i in raw for i in BUTTON_IDS):
                continue
            try:
                tt = o.read_typetree(node)
            except Exception:
                continue
            if "onClickSound" not in tt:
                continue
            try:
                name = o.read().m_GameObject.read().m_Name
            except Exception:
                name = "?"
            key = ", ".join(f"{f}={tt.get(f)}" for f in BUTTON_FIELDS if tt.get(f))
            patterns[key].add(name)
    print("Button sound patterns (LazyButton.on*Sound):")
    for k, names in sorted(patterns.items(), key=lambda kv: -len(kv[1])):
        print(f"  {len(names):4} {k}   e.g. {', '.join(sorted(names)[:8])}")


if __name__ == "__main__":
    main()
