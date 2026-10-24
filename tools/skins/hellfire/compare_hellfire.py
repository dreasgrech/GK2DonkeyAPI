"""Hellfire old against new, pixel by pixel, on one page (the user, 2026-10-19: "make sure they're the same").

Old: the frames the code drew (the reviewed prototype's output, moved onto the game's canvas: C:\\ClaudeCodeTemp\\
GK2-donkey-frames\\hellfire-96\\; the C# recipe LooksHellfire.cs was its port, pixel for pixel).
New: the pack (src/SaddleUp/assets/mounts/donkey/skins/saddleup.hellfire) composited over the donkey's frame as the game does it (the
erase colour clears, the other pixels draw), shown two ways:
  - "new, prototype coat": the coat darkened with the prototype's own formula (darken_coat): must equal old exactly;
  - "new, game coat": the coat through the game's [Base.Coat] rule (Brightness, then Colorize at full amount, the
    shader's maths emulated here): the only differences allowed are coat shades, and the page counts them.
Every frame: old | new | difference (changed pixels in red over a faded old), with the counts; a summary on top.

    python tools/skins/hellfire/compare_hellfire.py            -> tools/skins/hellfire/out/compare.html
"""
import base64, colorsys, io, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
from gamedir import donkey_frames
import hellfire_proto as HP

REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
PACK = os.path.join(REPO, "src", "SaddleUp", "assets", "mounts", "donkey", "skins", "saddleup.hellfire")
OLD = r"C:\ClaudeCodeTemp\GK2-donkey-frames\hellfire-96"
OUT = os.path.join(HERE, "out", "compare.html")
ORDER = (["bdy_static_left"] + [f"bdy_walk_left_{i:02d}" for i in range(1, 9)] + [f"bdy_ear_left_{i:02d}" for i in range(1, 6)]
         + [f"bdy_graze_left_{i:02d}" for i in range(1, 6)] + [f"bdy_chew_left_{i:02d}" for i in range(1, 4)]
         + [f"bdy_lips_left_{i:02d}" for i in range(1, 14)] + [f"bdy_turn_left_{i:02d}" for i in range(1, 7)]
         + ["hed_static_down", "hed_static_down_02", "hed_static_down_03"])
ERASE = (255, 0, 255)


def read_cfg(path):
    sec, out = "", {}
    for line in open(path, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if line.startswith("["):
            sec = line.strip("[]").strip()
        elif "=" in line:
            k, v = (x.strip() for x in line.split("=", 1))
            out[(sec, k.lower())] = v
    return out


def composite(donkey, pack):
    out = donkey.copy()
    o, p = out.load(), pack.load()
    for y in range(96):
        for x in range(96):
            c = p[x, y]
            if not c[3]:
                continue
            o[x, y] = (0, 0, 0, 0) if c[:3] == ERASE else (c[0], c[1], c[2], 255)
    return out


def game_coat(c, brightness, cz_h, cz_s, amount):
    """The game's RegionMap on one colour: Brightness (a value multiplier), then Colorize (hue and saturation of the
    colour, the pixel's own value), mixed in by amount. Gamma space, as the shader sees it."""
    h, s, v = colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)
    v = min(1.0, v * brightness)
    r, g, b = colorsys.hsv_to_rgb(h, s, v)
    cr, cg, cb = colorsys.hsv_to_rgb(cz_h, cz_s, v)
    r, g, b = r + (cr - r) * amount, g + (cg - g) * amount, b + (cb - b) * amount
    return (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))


def with_coat(im, rule):
    out = im.copy()
    px = out.load()
    for y in range(96):
        for x in range(96):
            c = px[x, y]
            if c[3] and c[:3] in HP.COAT:
                px[x, y] = rule(c[:3]) + (255,)
    return out


def proto_coat(c):
    hh, s, v = colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)
    nr, ng, nb = colorsys.hsv_to_rgb(0.02, min(1.0, s * 0.9 + 0.25), v * 0.42)
    return (int(nr * 255), int(ng * 255), int(nb * 255))


def diff(a, b):
    """Changed pixels in red over a faded a; the count, and how many of them are coat pixels of the donkey's frame."""
    out = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    pa, pb, po = a.load(), b.load(), out.load()
    n = 0
    for y in range(96):
        for x in range(96):
            ca, cb = pa[x, y], pb[x, y]
            same = (ca[3] == 0 and cb[3] == 0) or (ca[3] and cb[3] and ca[:3] == cb[:3])
            if same:
                if ca[3]:
                    po[x, y] = (ca[0] // 3 + 40, ca[1] // 3 + 40, ca[2] // 3 + 40, 255)
            else:
                po[x, y] = (255, 40, 40, 255); n += 1
    return out, n


def url(im, scale=4):
    big = im.resize((96 * scale, 96 * scale), Image.NEAREST)
    buf = io.BytesIO(); big.save(buf, "PNG")
    return "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()


def main():
    cfg = read_cfg(os.path.join(PACK, "skin.cfg"))
    brightness = float(cfg.get(("Base.Coat", "brightness"), "1"))
    colorize = cfg.get(("Base.Coat", "colorize"), "")
    amount = float(cfg.get(("Base.Coat", "colorizeamount"), "1"))
    cz = tuple(int(colorize[i:i + 2], 16) / 255 for i in (0, 2, 4)) if colorize else None
    cz_h, cz_s = colorsys.rgb_to_hsv(*cz)[:2] if cz else (0, 0)
    rows, sum_proto, sum_game, sum_game_coat = [], 0, 0, 0
    for key in ORDER:
        donkey = Image.open(os.path.join(donkey_frames(), key + ".png")).convert("RGBA")
        pack = Image.open(os.path.join(PACK, key + ".png")).convert("RGBA")
        old = Image.open(os.path.join(OLD, key + ".png")).convert("RGBA")
        new = composite(donkey, pack)
        new_proto = with_coat(new, proto_coat)
        new_game = with_coat(new, lambda c: game_coat(c, brightness, cz_h, cz_s, amount)) if cz else new
        d1, n1 = diff(old, new_proto)
        d2, n2 = diff(old, new_game)
        # of the game-coat differences, how many are coat pixels of the new picture (the coat rule's shades: the donkey's
        # coat and the rump fill drawn in coat colours) and how many elsewhere
        np_px = new.load(); pd2 = d2.load()
        coat_only = sum(1 for y in range(96) for x in range(96) if pd2[x, y][:3] == (255, 40, 40) and np_px[x, y][3] and np_px[x, y][:3] in HP.COAT)
        if n1:
            print(f"  {key}: {n1} px differ with the prototype coat")
        sum_proto += n1; sum_game += n2; sum_game_coat += coat_only
        rows.append((key, url(old), url(new_proto), url(d1), n1, url(new_game), url(d2), n2, coat_only))
    rows_html = "\n".join(
        f'<tr class="{"ok" if n1 == 0 else "bad"}"><th>{key}</th><td><img src="{o}"></td><td><img src="{np_}"></td><td><img src="{d1}"><div>{n1} px differ</div></td>'
        f'<td><img src="{ng}"></td><td><img src="{d2}"><div>{n2} px differ, {co} of them coat</div></td></tr>'
        for key, o, np_, d1, n1, ng, d2, n2, co in rows)
    exact = sum(1 for r in rows if r[4] == 0)
    html = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Hellfire Compare</title>
<style>
body {{ margin:0; background:#1c1f1a; color:#e8e6df; font:14px/1.4 system-ui, sans-serif; }}
header {{ position:sticky; top:0; background:#1c1f1a; padding:12px 16px; border-bottom:1px solid #3a4034; }}
h1 {{ font-size:18px; margin:0 0 6px; }} .sum {{ color:#a9a69c; }} .sum b {{ color:#e0b457; }}
table {{ border-collapse:collapse; margin:12px 16px; }} th {{ text-align:left; padding:6px 10px; color:#a9a69c; font-weight:normal; white-space:nowrap; }}
td {{ padding:4px 6px; vertical-align:top; background:#4f6a3a; border:4px solid #1c1f1a; }} img {{ display:block; image-rendering:pixelated; width:192px; height:192px; }}
td div {{ font-size:12px; color:#f0e6d0; margin-top:3px; }} tr.ok th::before {{ content:"= "; color:#8ad17a; }} tr.bad th::before {{ content:"! "; color:#ff6a5a; }}
thead td {{ background:#262a23; color:#e8e6df; font-weight:bold; }}
</style></head><body>
<header><h1>Hellfire: the code-drawn frames against the pack</h1>
<div class="sum">Old = the frames the code drew (the reviewed prototype, pixel for pixel the C# recipe). New = the pack over the donkey.
<b>{exact} of {len(rows)} frames identical</b> with the prototype's coat ({sum_proto} px differ in all).
With the game's coat rule: {sum_game} px differ, {sum_game_coat} of them coat shades (the rule's maths against the prototype's), {sum_game - sum_game_coat} elsewhere.
Red in a difference picture = a pixel that differs. Local only: the game's art.</div></header>
<table><thead><tr><td></td><td>old (code)</td><td>new, prototype coat</td><td>difference</td><td>new, game coat ([Base.Coat])</td><td>difference</td></tr></thead>
<tbody>{rows_html}</tbody></table></body></html>"""
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "w", encoding="utf-8").write(html)
    print(f"{exact} of {len(rows)} frames identical (prototype coat; {sum_proto} px differ in all); game coat: {sum_game} px differ, {sum_game_coat} coat shades, {sum_game - sum_game_coat} elsewhere -> {OUT}")


if __name__ == "__main__":
    main()
