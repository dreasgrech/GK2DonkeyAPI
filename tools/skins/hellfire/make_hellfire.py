"""Hellfire as a pack drawn over the donkey (docs/skins.md "Erasing from the donkey", "Recolouring the donkey under the
skin"): the reviewed prototype (hellfire_proto.py, 14 rounds) run over the donkey's frames on the GAME's canvas, and
every frame turned into pack pixels by comparing the result with the frame it came from:
  - a donkey pixel the prototype removed (the ears, the tail) becomes the erase colour (magenta);
  - a pixel the prototype added or changed (horns, the devil's tail, the rump fill, cracks, the glowing eyes) is drawn;
  - the ash coat is not in the pictures at all: it is the skin's [Base.Coat] recolour, the prototype's darken_coat as
    the colour knobs (hue 7 degrees, saturation about 0.5, brightness 0.42), so the player's coat colour still works.
So the pack holds nothing of the game's art, and the recipe code (LooksHellfire.cs) is not needed any more.

    python tools/skins/hellfire/make_hellfire.py            # packs/SaddleUpDonkeySkins/gk2mounts/donkey/skins/saddleup-donkey-skins.hellfire/ (the pack:
                                                             # skin.cfg + a PNG per frame) and out\\ (strips, preview, a sheet)
    python tools/skins/hellfire/make_hellfire.py --no-assets # out\\ only
    python tools/skins/hellfire/make_hellfire.py --cfg       # rewrite the built-in's skin.cfg too (it is by hand otherwise)

Review: out\\strips\\<animation>.png (every frame over the donkey), out\\preview.png, and the skins preview page
(tools/skins/skin_preview_html.py). The checker: python tools/skins/check_skin.py tools/skins/hellfire/out/pack
"""
import colorsys, math, os, shutil, sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
from gamedir import donkey_frames
import hellfire_proto as HP

REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
ASSETS = os.path.join(REPO, "packs", "SaddleUpDonkeySkins", "gk2mounts", "donkey", "skins", "saddleup-donkey-skins.hellfire")  # a donkey skin carries its mod's prefix (docs/skins.md)
OUT = os.path.join(HERE, "out")
FRAMES = donkey_frames()
ERASE = (255, 0, 255, 255)
ORDER = (["bdy_static_left"] + [f"bdy_walk_left_{i:02d}" for i in range(1, 9)] + [f"bdy_ear_left_{i:02d}" for i in range(1, 6)]
         + [f"bdy_graze_left_{i:02d}" for i in range(1, 6)] + [f"bdy_chew_left_{i:02d}" for i in range(1, 4)]
         + [f"bdy_lips_left_{i:02d}" for i in range(1, 14)] + [f"bdy_turn_left_{i:02d}" for i in range(1, 7)]
         + ["hed_static_down", "hed_static_down_02", "hed_static_down_03"])

# the prototype on the game's canvas: its loader reads the probe's frame (no padding: the canvas has the room), and
# its coat darkening is left out (the pack's [Base.Coat] does that in the game)
HP.load = lambda name: Image.open(os.path.join(FRAMES, (name[5:] if name.startswith("0301_") else name) + ".png")).convert("RGBA")
HP.darken_coat = lambda px, w, h: None


def phase_of(key):
    """The devil's tail's sway per walk frame, exactly as the reviewed frames had it (write_v14.py, the C# recipe):
    (i - 1) / 4 - 1 for walk frame i, so -1 to 0.75 across the cycle; still otherwise."""
    if "_walk_" in key:
        i = int(key.rsplit("_", 1)[1])
        return (i - 1) / 4.0 - 1.0
    return 0.0


def pack_frame(key):
    """The pack's picture for one frame: the prototype's result against the original, pixel by pixel."""
    orig = HP.load("0301_" + key)
    res = HP.hellfire("0301_" + key, phase_of(key))
    assert res.size == orig.size, (key, res.size, orig.size)
    w, h = orig.size
    op, rp = orig.load(), res.load()
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pp = out.load()
    erased = drawn = 0
    for y in range(h):
        for x in range(w):
            o, r = op[x, y], rp[x, y]
            if o[3] and not r[3]:
                pp[x, y] = ERASE; erased += 1
            elif r[3] and (not o[3] or r[:3] != o[:3]):
                pp[x, y] = (r[0], r[1], r[2], 255); drawn += 1
    return out, orig, res, erased, drawn


BONE = {c[:3] for c in HP.BONE}


def horn_runs(pp, y, w):
    """The bone-coloured runs of row y: one per horn where the row crosses both."""
    runs = []
    x = 0
    while x < w:
        if pp[x, y][3] and pp[x, y][:3] in BONE:
            s = x
            while x < w and pp[x, y][3] and pp[x, y][:3] in BONE:
                x += 1
            runs.append((s, x - 1))
        else:
            x += 1
    return runs


TUFT_ROWS = 4  # rows of mane above the horns' base row, between the horns, in every frame
TUFT_DARK = (30, 25, 14, 255)   # the donkey's own outline dark (region Dark): the tuft's top two rows
TUFT_MANE = (53, 46, 30, 255)   # the donkey's own mane shade (region Dark): the rest


def fill_between_horns(pack, orig, rows=TUFT_ROWS):
    """The same mane tuft between the two horns in every frame. The horns are thinner than the ears they replace, so
    what stood between the ears showed between them: one column in the standing frames, a block three wide where the
    head bobs (walk 3/4/7/8, the turn), and the gap flickered open and shut as the donkey walked (the user, 2026-10-19
    evening; the recipe code did the same). Erasing it all left a hole in his head (the user: "it should be filled").
    Row by row, wherever the row crosses both horns (two bone-coloured runs): the `rows` rows above the bases plus the
    base row are the tuft, every pixel between the runs the pack doesn't draw gets the mane's colours (the donkey's
    own shades, so the player's Dark colour and the recolour reach them as they reach the mane); above the tuft every
    donkey pixel between the runs becomes an erase mark. Returns (erased, drawn)."""
    pp, op = pack.load(), orig.load()
    w, h = pack.size
    two = [y for y in range(h) if len(horn_runs(pp, y, w)) >= 2]
    if not two:
        return 0, 0
    base = two[-1]
    erased = drawn = 0
    for y in two:
        runs = horn_runs(pp, y, w)
        for x in range(runs[0][1] + 1, runs[-1][0]):
            if pp[x, y][3]:
                continue
            if y < base - rows:
                if op[x, y][3]:
                    pp[x, y] = ERASE
                    erased += 1
            else:
                c = TUFT_DARK if y <= base - rows + 1 else TUFT_MANE
                if not op[x, y][3] or op[x, y][:3] != c[:3]:
                    pp[x, y] = c
                    drawn += 1
    return erased, drawn


def coat_rule():
    """darken_coat as the game's colour knobs. The prototype: hue 0.02, saturation s*0.9+0.25, value *0.42. The rule's
    maths (Brightness, then Colorize mixed by amount) can't be that formula exactly; these are the knobs a grid search
    fitted over the donkey's 14 coat colours (2026-10-19 21:30: rms 1.8 of 255 per channel; the first guess was 3.0)."""
    return "FF4747", 0.41, 0.6

def hexes(colours):
    return " ".join(f"{r:02X}{g:02X}{b:02X}" for (r, g, b) in colours)


def skin_cfg():
    colorize, brightness, amount = coat_rule()
    lava = [c[:3] for c in HP.LAVA]
    dark = [HP.OUTLINE] + [c[:3] for c in HP.BONE] + [(44, 18, 16), (70, 26, 20)]
    eye = [(255, 170, 40), (220, 60, 20), (200, 40, 30), (120, 20, 16)]  # the glow, its rim, the spade, its dark
    return (
        "# Hellfire: horns where the ears were, a devil's tail where the tail was, eyes that glow, lava cracks across an ash\n"
        "# coat: a skin drawn OVER the donkey (docs/skins.md). Generated by tools/skins/hellfire/make_hellfire.py from the\n"
        "# reviewed prototype: the pictures hold only our pixels and the erase marks (magenta) that take the ears and the\n"
        "# tail off the donkey; the ash coat is the [Base.Coat] recolour below. Our own art; nothing of the game's.\n"
        "[Skin]\nformat = 1\nname = Hellfire\ndescription = Horns, a devil's tail, glowing eyes and lava cracks over an ash coat.\nauthor = dreas\nversion = 2.0\nbase = donkey\ncolours = own\n"
        "layer = over\nerase = FF00FF\ncanvas = 96 96\n\n"  # no id = and no mount =: the folder donkey/skins/saddleup-donkey-skins.hellfire/ names it (donkey/saddleup-donkey-skins.hellfire)
        "# The cracks follow the player's muzzle colour, the horns and the tail's dark its dark parts, the glow its eye\n"
        "# colour, as the recipe had it; everything else is kept as drawn.\n"
        f"[Colors]\nMuzzle = {hexes(lava)}\nDark = {hexes(dark)}\nEye = {hexes(eye)}\n\n"
        "# The ash coat: the donkey's coat under the skin, darkened and warmed (the prototype's darken_coat), before the\n"
        "# player's own coat colour.\n"
        f"[Base.Coat]\nBrightness = {brightness}\nSaturation = 1\nColorize = {colorize}\nColorizeAmount = {amount}\n"
    )


def main(argv):
    os.makedirs(OUT, exist_ok=True)
    pack_dir = os.path.join(OUT, "pack")
    os.makedirs(pack_dir, exist_ok=True)
    frames, originals, results = {}, {}, {}
    total_erased = total_drawn = 0
    for key in ORDER:
        frames[key], originals[key], results[key], e, d = pack_frame(key)
        e2, d2 = fill_between_horns(frames[key], originals[key])
        e += e2; d += d2
        total_erased += e; total_drawn += d
    cfg = skin_cfg()
    # the pack: a PNG per frame + skin.cfg, into out\pack (the checker and the page read it) and the repo's built-in folder
    targets = [pack_dir] + ([ASSETS] if "--no-assets" not in argv else [])
    for t in targets:
        os.makedirs(t, exist_ok=True)
        for f in os.listdir(t):
            if f.endswith(".png") and f.startswith(("bdy_", "hed_")):
                os.remove(os.path.join(t, f))
        for key, im in frames.items():
            im.save(os.path.join(t, key + ".png"))
        # the cfg is by hand once it exists (the pipeline rule: the generator makes the pictures and keeps the cfg;
        # --cfg rewrites it from the recipe's colour lists); out\pack always gets the generated one
        cfg_path = os.path.join(t, "skin.cfg")
        if t != ASSETS or "--cfg" in argv or not os.path.exists(cfg_path):
            with open(cfg_path, "w", encoding="utf-8", newline="\r\n") as f:  # CRLF, as the repo has it
                f.write(cfg)
    # a sheet of the pack cells too, for the checker's sheet mode
    C = 8
    sheet = Image.new("RGBA", (C * 96, 6 * 96), (0, 0, 0, 0))
    for n, key in enumerate(ORDER):
        sheet.alpha_composite(frames[key], ((n % C) * 96, (n // C) * 96))
    sheet.save(os.path.join(pack_dir, "sheet.png"))
    # the review pictures (tools/skins/review.py, the same for every generator): the pack over the donkey with the
    # erase marks cleared, the rider on the donkey's seat row; out\strips\, out\preview.png, out\pack\sheet.png
    import review
    from gamedir import game_dir
    rider = review.load_rider(os.path.join(game_dir(), "BepInEx", "GK2-asset-probe"))
    strips_dir = review.write_all(OUT, frames, base=originals, erase=ERASE[:3], rider=rider, seat_row=review.DONKEY_SEAT_ROW)
    print(f"{len(ORDER)} frames: {total_erased} erase pixels, {total_drawn} drawn pixels; [Base.Coat] {coat_rule()}")
    print("pack ->", ", ".join(targets), "| strips ->", strips_dir)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
