"""The game folder, from Directory.Build.props (the one place it is set), and the folders the skin tools use in it."""
import os, re

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def game_dir():
    props = os.path.join(REPO, "Directory.Build.props")
    m = re.search(r"<GameDir[^>]*>([^<]+)</GameDir>", open(props, encoding="utf-8").read())
    return m.group(1).strip() if m else r"C:\Program Files (x86)\Steam\steamapps\common\Graveyard Keeper 2"


def donkey_frames():
    """The donkey's 44 frames as the GAME places them (the asset probe's PNGs: the feet on one ground row). Every
    picture for a pack is measured and drawn on this canvas (docs/sprite-tools.md "The canvas, found wrong")."""
    return os.path.join(game_dir(), "BepInEx", "GK2-asset-probe", "donkey-frames")


def packs_dir():
    return os.path.join(game_dir(), "BepInEx", "plugins", "gk2skins")
