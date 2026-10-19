# The crown's fallback anchors: the Hellfire horn bases per frame (the reviewed v14 positions, measured on the old
# top-left-aligned .raw canvas: horn_bases_raw.json), moved onto the game's canvas by each frame's own shift.
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from contact import ORDER, raw_to_game_shift

HERE = os.path.dirname(os.path.abspath(__file__))
raw = json.load(open(os.path.join(HERE, "horn_bases_raw.json")))
out = {}
for k in ORDER:
    dx, dy = raw_to_game_shift(k)
    out[k] = [[x + dx, y + dy, lean] for x, y, lean in raw[k]]
json.dump(out, open(os.path.join(HERE, "horn_bases.json"), "w"))
for k, v in out.items():
    print(f"{k:22s} {raw[k]} -> {v}")
