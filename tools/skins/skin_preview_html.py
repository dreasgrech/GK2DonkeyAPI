"""The skin preview page: every skin the API knows, grouped by mount, playing the donkey's clips next to the plain donkey
at the game's speed, pixel-sharp, with a frame-strip view for a frame-by-frame look (docs/sprite-tools.md).

Sources, in this order (the first that holds a skin id wins):
  1. the plain donkey: <probe>\\donkey-frames\\ (the game's 44 frames on the 96 canvas, from the asset probe);
  2. the built-in skins: <repo>\\src\\GK2DonkeyAPI\\assets\\skins\\<id>\\ (skin.cfg + a picture per frame, front\\ for the
     front layer; a skin with source = game has no pictures and is listed as such);
  3. the packs in the game: <probe>\\..\\plugins\\gk2skins\\<id>\\ (skin.cfg + pictures, or one sheet of cells);
  4. recipes, drawn from the donkey's frames by the game's code and saved as pictures: --recipe <id>=<folder>
     (Hellfire: C:\\ClaudeCodeTemp\\GK2-donkey-frames\\hellfire-96 when it exists, the frames the game's code drew,
     the code version's output on the game's canvas, kept until Hellfire is confirmed in game);
  5. the leftover test folders <probe>\\test\\skins\\<name>\\ without a skin.cfg: "Experiments", collapsed at the end.
A skin drawn over the donkey (layer = over) is composited over the donkey's frame here, as the game draws it (without
the player's colours: the page shows the art as drawn). The front layer (the front folder) is drawn in front of the rider, in
the animated view and in the strips; "Rider" puts the probe's rider on the skin's seat row (seat = <row>, the donkey's
52, a picture seat's top row) shifted by its RiderOffsetPx, with the seat row drawn as a line. A card says the seat,
the hitch gap, the babies' size and the colour regions the player's colours reach. A skin with source = game (Gerry)
shows the game's export when there is one. The captures of the probe's Capture tab go on top as before.

LOCAL ONLY: the frames are the game's art (and ours); the page embeds them and stays on this machine.

    python tools/skins/skin_preview_html.py <probe folder> <out.html> [--recipe id=folder ...] [--study <skin folder> ...]
"""
import base64, io, json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))  # tools/skins -> the repo
ORDER = (["bdy_static_left"] + [f"bdy_walk_left_{i:02d}" for i in range(1, 9)] + [f"bdy_ear_left_{i:02d}" for i in range(1, 6)]
         + [f"bdy_graze_left_{i:02d}" for i in range(1, 6)] + [f"bdy_chew_left_{i:02d}" for i in range(1, 4)]
         + [f"bdy_lips_left_{i:02d}" for i in range(1, 14)] + [f"bdy_turn_left_{i:02d}" for i in range(1, 7)]
         + ["hed_static_down", "hed_static_down_02", "hed_static_down_03"])
# the flying experiment's frames (docs/flying.md "the frames"): not the game's, the mod plays them while a mount flies;
# a skin without them shows empty cells there (the donkey has none)
FLIGHT = ([f"bdy_takeoff_left_{i:02d}" for i in range(1, 5)] + [f"bdy_fly_left_{i:02d}" for i in range(1, 9)]
          + [f"bdy_land_left_{i:02d}" for i in range(1, 5)] + [f"bdy_glide_left_{i:02d}" for i in range(1, 9)]
          + [f"bdy_glidein_left_{i:02d}" for i in range(1, 5)] + [f"bdy_glideout1_left_{i:02d}" for i in range(1, 5)])
MOUNT_NAMES = {"donkey": "Donkey"}  # every other mount's name comes from the [Mount] section of the skin that declares it
RIDER_HIP_ABOVE_FEET = 7  # the keeper's hip is 7 px above his feet (Mount.SeatOn, tools/skins/review.py)


def read_cfg(path):
    """skin.cfg as {(section, key): value}, lower-case keys, comments off."""
    sec, out = "", {}
    for line in open(path, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if line.startswith("["):
            sec = line.strip("[]").strip()
        elif "=" in line:
            k, v = (x.strip() for x in line.split("=", 1))
            out[(sec, k.lower())] = v
    return out


def png_size(path):
    b = open(path, "rb").read(24)
    return int.from_bytes(b[16:20], "big"), int.from_bytes(b[20:24], "big")


def data_url(png_bytes):
    return "data:image/png;base64," + base64.b64encode(png_bytes).decode()


def frames_from_folder(d):
    """{frame key: PNG bytes} for the per-frame pictures of a folder (bdy_*, hed_*), and the front\\ layer as front/<key>."""
    out = {}
    for f in sorted(os.listdir(d)):
        if f.endswith(".png") and f.startswith(("bdy_", "hed_")):
            out[f[:-4]] = open(os.path.join(d, f), "rb").read()
    front = os.path.join(d, "front")
    if os.path.isdir(front):
        for f in sorted(os.listdir(front)):
            if f.endswith(".png"):
                out["front/" + f[:-4]] = open(os.path.join(front, f), "rb").read()
    return out


def frames_from_sheet(d, cfg):
    """A pack's one sheet of cells (columns = N, cells in ORDER) cut into frames."""
    from PIL import Image
    sheet = Image.open(os.path.join(d, cfg[("Skin", "sheet")])).convert("RGBA")
    cols = int(cfg.get(("Skin", "columns"), "8"))
    cw, ch = (int(x) for x in cfg.get(("Skin", "canvas"), "96 96").split())
    out = {}
    for n, k in enumerate(ORDER):
        cell = sheet.crop(((n % cols) * cw, (n // cols) * ch, (n % cols) * cw + cw, (n // cols) * ch + ch))
        if cell.getbbox() is None:
            continue
        buf = io.BytesIO(); cell.save(buf, "PNG"); out[k] = buf.getvalue()
    return out


def composite_over(base_frames, over_frames, erase=(255, 0, 255)):
    """An over skin's frames drawn over the donkey's: what the game shows (without the player's colours). A pixel of
    the erase colour clears the donkey there and is not drawn (docs/skins.md, "erase")."""
    from PIL import Image
    out = {}
    for k in ORDER:
        if k not in base_frames:
            continue
        base = Image.open(io.BytesIO(base_frames[k])).convert("RGBA")
        if k in over_frames:
            ov = Image.open(io.BytesIO(over_frames[k])).convert("RGBA")
            if ov.size != base.size:  # canvases of different sizes share their centre
                w, h = max(ov.width, base.width), max(ov.height, base.height)
                big = Image.new("RGBA", (w, h), (0, 0, 0, 0)); big.alpha_composite(base, ((w - base.width) // 2, (h - base.height) // 2)); base = big
                big = Image.new("RGBA", (w, h), (0, 0, 0, 0)); big.alpha_composite(ov, ((w - ov.width) // 2, (h - ov.height) // 2)); ov = big
            if erase is not None:
                bp, op = base.load(), ov.load()
                for y in range(ov.height):
                    for x in range(ov.width):
                        c = op[x, y]
                        if c[3] and c[:3] == erase:
                            bp[x, y] = (0, 0, 0, 0)
                            op[x, y] = (0, 0, 0, 0)
            base.alpha_composite(ov)
        buf = io.BytesIO(); base.save(buf, "PNG"); out[k] = buf.getvalue()
    for k, v in over_frames.items():
        if k.startswith("front/"):
            out[k] = v
    return out


def donkey_regions():
    """The donkey's region colour lists as the API has them (src/GK2DonkeyAPI/Colors.cs, the R("Coat", ...) lines):
    {region key: set of (r, g, b)}. Read from the source so the page can't drift from the game."""
    src = open(os.path.join(REPO, "src", "GK2DonkeyAPI", "Colors.cs"), encoding="utf-8").read()
    out = {}
    for m in re.finditer(r'R\("(\w+)",\s*"[^"]*",\s*"Donkey",\s*(.*?)\)\s*,?\s*\n', src, re.S):
        key, body = m.group(1), m.group(2)
        cols = set()
        for hexes in re.findall(r'"([0-9a-fA-F ]+)"', body):
            for h in hexes.split():
                cols.add(tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)))
        if cols:
            out[key] = cols
    return out


def tweak_colour(c, t):
    """One colour through a [Base.<Region>] section, in the order the game's RegionMap applies it: hue shift,
    saturation and brightness multipliers, contrast about mid grey, colorize (the colour's hue and saturation, the
    pixel's own value, mixed by amount), tint (multiply), overlay (mix). Gamma space, as the shader sees it."""
    import colorsys
    r, g, b = c[0] / 255, c[1] / 255, c[2] / 255
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    h = (h + float(t.get("hue", 0)) / 360) % 1.0
    s = min(1.0, s * float(t.get("saturation", 1)))
    v = min(1.0, v * float(t.get("brightness", 1)))
    r, g, b = colorsys.hsv_to_rgb(h, s, v)
    k = float(t.get("contrast", 1))
    if abs(k - 1) > 0.001:
        r, g, b = (min(1, max(0, (x - 0.5) * k + 0.5)) for x in (r, g, b))
    cz = t.get("colorize", "")
    amount = float(t.get("colorizeamount", 1))
    if len(cz) == 6 and amount > 0:
        czh, czs, _ = colorsys.rgb_to_hsv(*(int(cz[i:i + 2], 16) / 255 for i in (0, 2, 4)))
        _, _, cv = colorsys.rgb_to_hsv(r, g, b)
        tr, tg, tb = colorsys.hsv_to_rgb(czh, czs, cv)
        r, g, b = r + (tr - r) * amount, g + (tg - g) * amount, b + (tb - b) * amount
    tn = t.get("tint", "")
    if len(tn) == 6:
        tr, tg, tb = (int(tn[i:i + 2], 16) / 255 for i in (0, 2, 4))
        r, g, b = r * tr, g * tg, b * tb
    ov = t.get("overlay", "")
    oa = float(t.get("overlayamount", 0.5))
    if len(ov) == 6 and oa > 0:
        tr, tg, tb = (int(ov[i:i + 2], 16) / 255 for i in (0, 2, 4))
        r, g, b = r + (tr - r) * oa, g + (tg - g) * oa, b + (tb - b) * oa
    return (int(round(min(1, max(0, r)) * 255)), int(round(min(1, max(0, g)) * 255)), int(round(min(1, max(0, b)) * 255)))


def apply_base_regions(frames, cfg, regions):
    """The skin's own recolour of the donkey under it ([Base.<Region>] sections): every pixel in a region's colours,
    wherever it is in the picture (the swap is by colour, as the game's palette does it), through the section."""
    from PIL import Image
    tweaks = {}
    for (sec, key), value in cfg.items():
        if sec.lower().startswith("base."):
            tweaks.setdefault(sec[5:].capitalize(), {})[key] = value
    if not tweaks:
        return frames, False
    table = {}
    for region, t in tweaks.items():
        for c in regions.get(region, ()):
            table[c] = tweak_colour(c, t)
    out = {}
    for k, png in frames.items():
        im = Image.open(io.BytesIO(png)).convert("RGBA")
        px = im.load()
        for y in range(im.height):
            for x in range(im.width):
                c = px[x, y]
                if c[3] and c[:3] in table:
                    px[x, y] = table[c[:3]] + (c[3],)
        buf = io.BytesIO(); im.save(buf, "PNG"); out[k] = buf.getvalue()
    return out, True


def top_row(png):
    """The first row with an opaque pixel of a PNG (bytes), or None."""
    if png is None:
        return None
    from PIL import Image
    im = Image.open(io.BytesIO(png)).convert("RGBA")
    px = im.load()
    return next((y for y in range(im.height) if any(px[x, y][3] for x in range(im.width))), None)


def bottom_row(png):
    """The last row with an opaque pixel of a PNG (bytes), or None."""
    from PIL import Image
    im = Image.open(io.BytesIO(png)).convert("RGBA")
    px = im.load()
    return next((y for y in range(im.height - 1, -1, -1) if any(px[x, y][3] for x in range(im.width))), None)


def pixel_diff(a, b):
    """How many pixels differ between two PNGs (bytes), transparent counting as one colour; all of a when b is missing."""
    from PIL import Image
    if a is None and b is None:
        return 0
    if a is None or b is None:
        im = Image.open(io.BytesIO(a or b)).convert("RGBA")
        return sum(1 for p in im.getdata() if p[3])
    ia, ib = Image.open(io.BytesIO(a)).convert("RGBA"), Image.open(io.BytesIO(b)).convert("RGBA")
    if ia.size != ib.size:
        return ia.width * ia.height
    n = 0
    for pa, pb in zip(ia.getdata(), ib.getdata()):
        if (pa[3] == 0) != (pb[3] == 0) or (pa[3] and pa[:3] != pb[:3]):
            n += 1
    return n


def is_whole(exported, donkey_png):
    """An exported over-skin frame built whole holds the donkey's body under it: most of the donkey's opaque pixels are
    opaque in it too; a plain overlay holds only its own few."""
    from PIL import Image
    if donkey_png is None:
        return False
    d = Image.open(io.BytesIO(donkey_png)).convert("RGBA"); e = Image.open(io.BytesIO(exported)).convert("RGBA")
    if d.size != e.size:
        return False
    both = sum(1 for pd, pe in zip(d.getdata(), e.getdata()) if pd[3] and pe[3])
    opaque = sum(1 for pd in d.getdata() if pd[3])
    return opaque > 0 and both >= opaque * 0.6


def main(argv):
    if len(argv) < 2:
        print(__doc__); return 2
    probe, out_path = argv[0], argv[1]
    recipes = {}
    studies = []
    i = 2
    while i < len(argv):
        if argv[i] == "--recipe" and i + 1 < len(argv) and "=" in argv[i + 1]:
            rid, folder = argv[i + 1].split("=", 1); recipes[rid] = folder; i += 2
        elif argv[i] == "--study" and i + 1 < len(argv):
            studies.append(argv[i + 1]); i += 2   # a skin folder (skin.cfg + pictures) being drawn, not in the game
        else:
            i += 1
    # (Hellfire was a recipe until 2026-10-19 and had its drawn frames listed here; it is a built-in pack now, found with the others.)

    # the game's own export (the Mounts tab, "Export the loaded skins as built"): the frames as the game draws them;
    # when a skin has one, the page shows it and counts where its own composite would differ (no silent divergence)
    export_dir = os.path.join(probe, "game-skins")
    export_note = ""
    if os.path.isfile(os.path.join(export_dir, "manifest.txt")):
        export_note = open(os.path.join(export_dir, "manifest.txt"), encoding="utf-8").readline().strip()
    donkey_dir = os.path.join(probe, "donkey-frames")
    donkey = frames_from_folder(donkey_dir)
    game_donkey = frames_from_folder(os.path.join(export_dir, "donkey")) if os.path.isdir(os.path.join(export_dir, "donkey")) else {}
    donkey_diverge = None
    if game_donkey:
        donkey_diverge = sum(pixel_diff(donkey.get(k), game_donkey.get(k)) for k in ORDER if k in game_donkey)
        donkey = {**donkey, **game_donkey}  # the game's reading of its own frames wins
    skins = []
    mount_names = dict(MOUNT_NAMES)
    seen = set()
    regions = donkey_regions()

    def add(sid, name, cfg, frames, source, folder):
        if sid in seen:
            return
        seen.add(sid)
        layer = (cfg.get(("Skin", "layer"), "replace") if cfg else "replace").lower()
        cw, ch = (int(x) for x in (cfg.get(("Skin", "canvas"), "96 96") if cfg else "96 96").split())
        mount = (cfg.get(("Skin", "mount"), "donkey") if cfg else "donkey").lower()
        if cfg and ("Mount", "name") in cfg:
            mount_names[cfg.get(("Mount", "id"), mount).lower()] = cfg[("Mount", "name")]
        note = ""
        if cfg and cfg.get(("Skin", "source"), "").lower() == "game":
            note = "no pictures of its own: the game's " + cfg.get(("Game", "prefix"), "") + " frames, read back and scaled at run time" + (
                "; shown from the game's export" if os.path.isdir(os.path.join(export_dir, sid)) else "; export the loaded skins from the Mounts tab to see them here")
        shown = frames
        if layer == "over" and frames:
            e = (cfg.get(("Skin", "erase"), "FF00FF") if cfg else "FF00FF").strip()
            erase = None if e.lower() == "none" else tuple(int(e[i:i + 2], 16) for i in (0, 2, 4))
            shown = composite_over(donkey, frames, erase)
            note = "drawn over the donkey (layer = over): shown composited, without the player's colours"
        # the game's export of this skin, when it has one: shown instead, and the page's composite checked against it
        game_dir = os.path.join(export_dir, sid)
        diverge = None
        if os.path.isdir(game_dir):
            exported = frames_from_folder(game_dir)
            if exported:
                game_shown = exported
                if layer == "over":
                    # the export holds a plain overlay as the overlay alone and a whole frame as the whole: the page
                    # composites the overlays over the game's donkey, as the game draws them, and leaves whole frames be
                    game_shown = {}
                    for k, v in exported.items():
                        if k.startswith("front/") or k not in ORDER:
                            game_shown[k] = v; continue
                        game_shown[k] = v if is_whole(v, donkey.get(k)) else composite_over({k: donkey[k]}, {k: v}, None)[k]
                diverge = sum(pixel_diff(shown.get(k), game_shown.get(k)) for k in ORDER if k in game_shown or k in shown)
                shown = game_shown
                note = (note + "; " if note else "") + ("as the game built it: the page's own composite matches, 0 px differ" if diverge == 0 else f"as the game built it: the page's own composite DIFFERS in {diverge} px, look")
        # the skin's own recolour of the donkey under it ([Base.Coat] ...): the game's shader does it at draw time, so
        # the page does the same maths here (after the divergence count, which is about the pictures as built)
        if cfg and layer == "over":
            shown, recoloured = apply_base_regions(shown, cfg, regions)
            if recoloured:
                note = (note + "; " if note else "") + "the skin's own recolour of the donkey ([Base.*]) applied as the game's rule does"
        missing = [k for k in ORDER if k not in shown]
        # the skin's facts for the card: its seat row (a picture seat: the standing frame's top row), the rider's own
        # offset, the hitch gap, the mount's babies and the colour regions the player's colours reach
        seat = None
        seat_text = cfg.get(("Skin", "seat"), "").strip().lower() if cfg else ""
        if seat_text.isdigit():
            seat = int(seat_text)
        elif seat_text == "picture":
            seat = top_row(shown.get("bdy_static_left"))
        elif layer == "over" or not cfg or mount == "donkey":
            seat = 52 + (max(cw, ch) - 96) // 2  # the donkey's own seat (docs/seat-investigation.md)
        rider_dx = int(float((cfg or {}).get(("Tuning.Ride", "rideroffsetpx"), (cfg or {}).get(("Tuning", "rideroffsetpx"), "0"))))
        seat_adj = int(float((cfg or {}).get(("Tuning.Ride", "seatadjustpx"), (cfg or {}).get(("Tuning", "seatadjustpx"), "0"))))  # positive = higher
        hitch = (cfg or {}).get(("Tuning.Cart", "hitchgappx"), (cfg or {}).get(("Tuning", "hitchgappx"), ""))
        colour_regions = sorted({k.capitalize() for (sec, k) in (cfg or {}) if sec == "Colors"})
        if cfg and layer != "over":
            colour_regions = ["Coat"] + [r for r in colour_regions if r != "Coat"]  # every colour not named is Coat
        elif cfg and colour_regions:
            colour_regions = ["the donkey's five"] + colour_regions  # an over skin: the donkey's regions under it, its own on top
        babies = (cfg or {}).get(("Mount", "foal_scale"), "0.6" if ("Mount", "id") in (cfg or {}) else "")
        facts = []
        if seat is not None: facts.append(f"seat row {seat}" + (" (the picture's top)" if seat_text == "picture" else "") + (f", rider {rider_dx:+d} px" if rider_dx else "") + (f", seat nudge {seat_adj:+d} px" if seat_adj else ""))
        if hitch: facts.append(f"hitch gap {hitch} px")
        if babies: facts.append(f"babies {babies}")
        if colour_regions: facts.append("colours: " + ", ".join(colour_regions))
        skins.append({"id": sid, "name": name, "mount": mount, "source": source, "layer": layer, "canvas": max(cw, ch),
                      "frames": {k: data_url(v) for k, v in shown.items()}, "note": note, "missing": len(missing), "folder": folder,
                      "game": os.path.isdir(game_dir), "diverge": diverge, "seat": seat, "rider_dx": rider_dx, "seat_adj": seat_adj, "facts": " · ".join(facts)})

    # 1. the plain donkey
    skins.append({"id": "donkey", "name": "The game's own", "mount": "donkey", "source": "the game", "layer": "replace", "canvas": 96,
                  "frames": {k: data_url(v) for k, v in donkey.items()}, "note": "the donkey as the game draws it: the reference", "missing": 0, "folder": donkey_dir,
                  "seat": 52, "rider_dx": 0, "seat_adj": 0, "facts": "seat row 52 · colours: Coat, Dark, Muzzle, Eye, Harness"})
    seen.add("donkey")
    # 2. the built-in skins
    # (every src/*/assets/skins: the API's own and Saddle Up's, skinroots.py)
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import skinroots
    for p in skinroots.skin_dirs():
        d = os.path.basename(p)
        project = os.path.basename(os.path.dirname(os.path.dirname(os.path.dirname(p))))
        cfg = read_cfg(os.path.join(p, "skin.cfg"))
        frames = frames_from_sheet(p, cfg) if ("Skin", "sheet") in cfg else frames_from_folder(p)
        add(cfg.get(("Skin", "id"), d), cfg.get(("Skin", "name"), d), cfg, frames, "built in" if project == skinroots.API_PROJECT else "the " + project + " mod", p)
    # 3. the packs in the game
    packs = os.path.normpath(os.path.join(probe, "..", "plugins", "gk2skins"))
    if os.path.isdir(packs):
        for d in sorted(os.listdir(packs)):
            p = os.path.join(packs, d)
            if os.path.isfile(os.path.join(p, "skin.cfg")):
                cfg = read_cfg(os.path.join(p, "skin.cfg"))
                frames = frames_from_sheet(p, cfg) if ("Skin", "sheet") in cfg else frames_from_folder(p)
                add(cfg.get(("Skin", "id"), d), cfg.get(("Skin", "name"), d), cfg, frames, "pack in the game", p)
    # 3b. studies: skin folders being drawn (a generator's output in a temp folder), shown with their mount
    for p in studies:
        if os.path.isfile(os.path.join(p, "skin.cfg")):
            cfg = read_cfg(os.path.join(p, "skin.cfg"))
            add(cfg.get(("Skin", "id"), os.path.basename(p)), cfg.get(("Skin", "name"), os.path.basename(p)), cfg, frames_from_folder(p), "a study (not in the game)", p)
    # 4. recipes drawn to pictures
    for rid, folder in sorted(recipes.items()):
        if os.path.isdir(folder):
            add(rid, rid.capitalize(), None, frames_from_folder(folder), "recipe (frames drawn by the game's code)", folder)
    # 5. the leftover test folders
    experiments = []
    tests = os.path.join(probe, "test", "skins")
    if os.path.isdir(tests):
        for d in sorted(os.listdir(tests)):
            p = os.path.join(tests, d)
            if not os.path.isdir(p) or os.path.isfile(os.path.join(p, "skin.cfg")) or d in seen:
                continue
            frames = frames_from_folder(p)
            if not frames:
                continue
            w, h = png_size(os.path.join(p, sorted(f for f in os.listdir(p) if f.endswith(".png"))[0]))
            experiments.append({"id": d, "name": d, "mount": "experiments", "source": "test folder in the probe", "layer": "replace", "canvas": max(w, h),
                                "frames": {k: data_url(v) for k, v in frames.items()}, "note": "an old experiment: no skin.cfg, not known to the API",
                                "missing": len([k for k in ORDER if k not in frames]), "folder": p})

    # groups by mount: Donkey first, then by mount name; inside, the mount's own look first, then by name
    mounts = sorted({s["mount"] for s in skins}, key=lambda m: (0 if m == "donkey" else 1, mount_names.get(m, m).lower()))
    groups = []
    for m in mounts:
        items = [s for s in skins if s["mount"] == m]
        items.sort(key=lambda s: (0 if s["id"] in (m, "donkey") else 1, s["name"].lower()))
        groups.append({"id": m, "name": mount_names.get(m, m), "skins": items})
    if experiments:
        groups.append({"id": "experiments", "name": "Experiments (old test folders)", "skins": experiments, "collapsed": True})

    # clips: frame names grouped by clip, numbered frames in order; the game's clip lengths where known
    clips = {}
    for key in ORDER:
        mm = re.match(r"bdy_(.+?)_left(?:_(\d+))?$", key)
        if mm:
            clips.setdefault(mm.group(1), []).append(key)
    clips["head"] = [k for k in ORDER if k.startswith("hed_")]
    speed = {"walk": 970 / 8, "head": 300}
    # flying: the beat (eight frames in Flight.WingBeatS, 0.6 s), the take-off, the landing, the glide (eight frames in
    # Flight.GlideLoopS, 1.6 s), and the whole flight once round (take-off, three beats, landing) as the game plays it,
    # if any skin has the frames
    if any(k in sk.get("frames", {}) for g in groups for sk in g["skins"] for k in FLIGHT):
        fly = [k for k in FLIGHT if "_fly_" in k]
        clips["fly"] = fly
        clips["takeoff"] = [k for k in FLIGHT if "_takeoff_" in k]
        clips["land"] = [k for k in FLIGHT if "_land_" in k]
        clips["flight"] = clips["takeoff"] + fly * 3 + clips["land"]
        speed.update({"fly": 75, "takeoff": 80, "land": 110, "flight": 95})
        if any(k in sk.get("frames", {}) for g in groups for sk in g["skins"] for k in FLIGHT if "_glide_" in k):
            clips["glide"] = [k for k in FLIGHT if "_glide_" in k]
            speed["glide"] = 200
            # the beat into a glide and out again as the game plays it (one speed for the clip, 100 ms: the in-betweens
            # and the beat at it, and each glide frame twice for the loop's 200 ms)
            beat_ = [k for k in FLIGHT if "_fly_" in k]
            clips["glide-in-out"] = (beat_ + beat_[:6] + [k for k in FLIGHT if "_glidein_" in k]
                                     + [k for k in clips["glide"] for _ in (0, 1)] + [k for k in FLIGHT if "_glideout1_" in k] + beat_[6:])
            speed["glide-in-out"] = 100

    caps_dir = os.path.join(probe, "captures")
    captures = []
    if os.path.isdir(caps_dir):
        for name in sorted(os.listdir(caps_dir), reverse=True)[:6]:
            d = os.path.join(caps_dir, name)
            files = sorted(f for f in os.listdir(d) if f.endswith(".png"))
            if files:
                captures.append({"name": name, "frames": ["captures/" + name + "/" + f for f in files]})
    rider_png = os.path.join(probe, "rider.png")
    rider = data_url(open(rider_png, "rb").read()) if os.path.isfile(rider_png) else ""
    rider_hip = 51
    if rider:
        feet = bottom_row(open(rider_png, "rb").read())
        if feet is not None:
            rider_hip = feet - RIDER_HIP_ABOVE_FEET

    if export_note:
        same = "the same as the probe's" if donkey_diverge == 0 else str(donkey_diverge) + " px differ from the probe's donkey-frames"
        export_note += "; the donkey's frames: " + same
    page = HTML.replace("__GROUPS__", json.dumps(groups)).replace("__CLIPS__", json.dumps(clips)).replace("__SPEED__", json.dumps(speed)) \
               .replace("__CAPS__", json.dumps(captures)).replace("__RIDER__", json.dumps(rider)).replace("__ORDER__", json.dumps(ORDER + (FLIGHT if "fly" in clips else []))) \
               .replace("__EXPORT__", json.dumps(export_note)).replace("__RIDER_HIP__", json.dumps(rider_hip))
    open(out_path, "w", encoding="utf-8").write(page)
    print("ok", out_path, f"{len(page) // 1024} KB" + (f" | game export: {export_note}" if export_note else " | no game export yet (Mounts tab: Export the loaded skins as built)"))
    for g in groups:
        print(f"  {g['name']}: " + ", ".join(f"{s['name']} [{s['id']}, {s['source']}, {s['layer']}, {s['canvas']}px" + (f", {s['missing']} frames missing" if s['missing'] else "") + "]" for s in g["skins"]))
    print("  captures:", len(captures), "| clips:", ", ".join(f"{c} {len(v)}" for c, v in clips.items()))
    return 0


HTML = r"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Mount Skins</title>
<style>
:root { --bg:#1c1f1a; --fg:#e8e6df; --muted:#a9a69c; --card:#262a23; --line:#3a4034; --day:#4f6a3a; --night:#1f2a3d; --accent:#e0b457; --stage:288px; --card-w:310px; }
* { box-sizing:border-box; }
body { margin:0; background:var(--bg); color:var(--fg); font:15px/1.4 system-ui, sans-serif; }
header { position:sticky; top:0; z-index:5; background:var(--bg); padding:10px 16px; display:flex; flex-wrap:wrap; gap:10px 14px; align-items:center; border-bottom:1px solid var(--line); }
h1 { font-size:18px; margin:0 8px 0 0; }
label { color:var(--muted); white-space:nowrap; }
select, button, input[type=range] { background:var(--card); color:var(--fg); border:1px solid var(--line); border-radius:6px; padding:5px 9px; font:inherit; }
button.on { border-color:var(--accent); color:var(--accent); }
#frameinfo { color:var(--muted); font-variant-numeric:tabular-nums; min-width:220px; }
nav { padding:8px 16px; display:flex; flex-wrap:wrap; gap:8px; border-bottom:1px solid var(--line); }
nav a { color:var(--accent); text-decoration:none; padding:3px 8px; border:1px solid var(--line); border-radius:6px; }
nav a:hover { border-color:var(--accent); }
p.note { color:var(--muted); margin:8px 16px 0; font-size:13px; }
section.mount { padding:12px 16px 4px; }
section.mount > h2 { font-size:17px; margin:8px 0 10px; border-bottom:1px solid var(--line); padding-bottom:6px; }
section.mount > h2 small { color:var(--muted); font-weight:normal; font-size:13px; margin-left:8px; }
section.mount > h2 .toggle { float:right; font-size:13px; }
.cards { display:grid; grid-template-columns:repeat(auto-fill, minmax(var(--card-w), 1fr)); gap:14px; }
.collapsed .cards { display:none; }
.card { background:var(--card); border:1px solid var(--line); border-radius:10px; padding:10px; }
.card h3 { font-size:15px; margin:0 0 2px; }
.card .meta { color:var(--muted); font-size:12px; margin:0 0 8px; min-height:30px; }
.card .meta code { color:var(--fg); }
.stage { background:var(--day); border-radius:6px; position:relative; overflow:hidden; width:var(--stage); height:var(--stage); }
.stage img { position:absolute; image-rendering:pixelated; left:50%; top:50%; transform:translate(-50%, -50%); }
.stage.flip img { transform:translate(-50%, -50%) scaleX(-1); }
.rider { pointer-events:none; display:none; }
.seat { position:absolute; left:0; right:0; height:1px; background:rgba(255,220,80,.7); display:none; pointer-events:none; }
.stage .empty { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; color:var(--muted); font-size:13px; }
body.with-rider .rider { display:block; }
body.with-rider .seat { display:block; }
body.night .stage { background:var(--night); }
body.night .stage img { filter:brightness(.55) saturate(.8) hue-rotate(10deg); }
.strip { display:none; background:var(--day); border-radius:6px; overflow-x:auto; white-space:nowrap; padding:4px; }
.strip .cell { display:inline-block; vertical-align:top; position:relative; width:var(--stage); height:var(--stage); margin-right:4px; }
.strip .cell img { position:absolute; image-rendering:pixelated; left:50%; top:50%; transform:translate(-50%, -50%); }
.strip .cell span { position:absolute; left:3px; top:2px; font-size:11px; color:#fff; text-shadow:0 0 3px #000; }
.strip .cell.fallback img { opacity:.45; }
body.night .strip { background:var(--night); }
body.night .strip img { filter:brightness(.55) saturate(.8) hue-rotate(10deg); }
body.strips .stage, body.all .stage { display:none; }
body.strips .strip, body.all .strip { display:block; }
body.strips .cards, body.all .cards { grid-template-columns:1fr; }
body.all .strip { white-space:normal; }
body.all .strip .cell { margin-bottom:4px; }
.card .meta .game { color:#8ad17a; } .card .meta .diverge { color:#ff6a5a; font-weight:bold; }
#caps { padding:12px 16px 0; display:grid; grid-template-columns:repeat(auto-fill, minmax(320px, 1fr)); gap:14px; }
#caps:empty { display:none; }
.cap img { width:100%; image-rendering:pixelated; border-radius:6px; display:block; }
.cap input { width:100%; }
</style></head>
<body>
<header>
  <h1>Mount skins</h1>
  <label>Clip <select id="clip"></select></label>
  <label>Frame <input type="range" id="scrub" min="0" max="0" value="0" style="width:120px"></label>
  <button id="prev" title="previous frame (left arrow)">&#9664;</button>
  <button id="pause" title="space">Pause</button>
  <button id="next" title="next frame (right arrow)">&#9654;</button>
  <span id="frameinfo"></span>
  <label>View <select id="view"><option value="anim">animated</option><option value="strips">frame strips</option><option value="all">all 44 frames</option></select></label>
  <label>Scale <select id="scale"><option>2</option><option selected>3</option><option>4</option><option>5</option></select></label>
  <label>Facing <select id="facing"><option value="left">left (as drawn)</option><option value="right">right (mirrored)</option></select></label>
  <button id="night">Night</button>
  <button id="rider">Rider: off</button>
</header>
<nav id="nav"></nav>
<p class="note">Every skin the API knows, by mount: the mount's own look first, then by name. Local only: the frames are the game's art and ours. The page shows the art as drawn (no player colours); night is a rough tint, not the game's lighting. A frame a skin has no picture for shows the donkey's own (faded in the strips). Frame strips: every frame of the clip side by side; all 44 frames: the whole skin, every picture named. Rider: the probe's rider with his hip on the skin's seat row (the yellow line), shifted by the skin's RiderOffsetPx; a front layer is drawn in front of him. Keys: space pauses, the arrows step.</p>
<p class="note" id="export"></p>
<section id="caps"></section>
<div id="mounts"></div>
<script>
const GROUPS = __GROUPS__, CLIPS = __CLIPS__, SPEED = __SPEED__, RIDER = __RIDER__, ORDER = __ORDER__, CAPS = __CAPS__, EXPORT = __EXPORT__, RIDER_HIP = __RIDER_HIP__;
document.getElementById('export').textContent = EXPORT ? 'The game\'s own export is in use where a skin has one (' + EXPORT + '). A card says whether the page\'s composite matched it.' : 'No export from the game yet: the pictures are the page\'s own composite. In game: F11, Mounts tab, "Export the loaded skins as built", then rebuild this page.';
const DONKEY = GROUPS[0].skins.find(s => s.id === 'donkey');
const $ = id => document.getElementById(id);
let frame = 0, paused = false, last = 0, scale = 3;
const stages = [];
const hasFrames = s => Object.keys(s.frames).length > 0;
// a section's stage is the largest canvas among its skins with pictures (the donkey's 96, the dragon's 128), so a
// section's pictures share one pixel scale and their pivots (the canvas centres) line up
function sectionSize(g) { return Math.max(96, ...g.skins.filter(hasFrames).map(s => s.canvas || 96)) * scale; }
function applyScale() {
  GROUPS.forEach(g => { const sec = document.getElementById('m-' + g.id); sec.style.setProperty('--stage', sectionSize(g) + 'px'); sec.style.setProperty('--card-w', (sectionSize(g) + 22) + 'px'); });
  stages.forEach(x => {
    const n = (x.skin.canvas || 96) * scale;
    x.im.style.width = x.im.style.height = n + 'px'; x.fr.style.width = x.fr.style.height = n + 'px';
    x.rd.style.width = x.rd.style.height = (96 * scale) + 'px';
    x.strip.querySelectorAll('img:not(.rider)').forEach(i => { i.style.width = i.style.height = n + 'px'; });
    x.strip.querySelectorAll('img.rider').forEach(i => { i.style.width = i.style.height = (96 * scale) + 'px'; i.style.transform = riderPlace(x.skin, false); });
    x.rd.style.transform = riderPlace(x.skin, $('facing').value === 'right');  // the offsets are in scaled pixels
    if (x.skin.seat != null) x.sl.style.top = seatTop(x.skin);
  });
}
const nav = $('nav'), mountsEl = $('mounts');
GROUPS.forEach(g => {
  const a = document.createElement('a'); a.href = '#m-' + g.id; a.textContent = g.name + ' (' + g.skins.length + ')'; nav.appendChild(a);
  const sec = document.createElement('section'); sec.className = 'mount' + (g.collapsed ? ' collapsed' : ''); sec.id = 'm-' + g.id;
  const h = document.createElement('h2'); h.innerHTML = g.name + '<small>' + g.skins.length + ' skin' + (g.skins.length === 1 ? '' : 's') + '</small>';
  const tg = document.createElement('button'); tg.className = 'toggle'; tg.textContent = g.collapsed ? 'show' : 'hide';
  tg.onclick = () => { sec.classList.toggle('collapsed'); tg.textContent = sec.classList.contains('collapsed') ? 'show' : 'hide'; };
  h.appendChild(tg); sec.appendChild(h);
  const cards = document.createElement('div'); cards.className = 'cards'; sec.appendChild(cards);
  g.skins.forEach(s => {
    const card = document.createElement('div'); card.className = 'card';
    const h3 = document.createElement('h3'); h3.textContent = s.name; card.appendChild(h3);
    const meta = document.createElement('p'); meta.className = 'meta';
    const noteHtml = s.note ? '<br>' + s.note.replace(/as the game built it: the page's own composite matches, 0 px differ/, '<span class="game">as the game built it; the page\'s own composite matches (0 px differ)</span>').replace(/(as the game built it: the page's own composite DIFFERS in \d+ px, look)/, '<span class="diverge">$1</span>') : '';
    meta.innerHTML = '<code>' + s.id + '</code> &middot; ' + s.source + ' &middot; ' + s.layer + ' &middot; ' + s.canvas + ' px' + (s.missing ? ' &middot; <b>' + s.missing + ' frames missing</b>' : '') + (s.game ? '' : ' &middot; <i>not exported by the game yet</i>') + (s.facts ? '<br>' + s.facts : '') + noteHtml;
    card.appendChild(meta);
    const st = document.createElement('div'); st.className = 'stage';
    if (!hasFrames(s)) { const e = document.createElement('div'); e.className = 'empty'; e.textContent = 'no pictures to show'; st.appendChild(e); }
    const im = document.createElement('img'); im.alt = s.name; if (!hasFrames(s)) im.style.display = 'none'; st.appendChild(im);
    const rd = document.createElement('img'); rd.className = 'rider'; rd.alt = ''; if (RIDER) rd.src = RIDER; st.appendChild(rd);
    const fr = document.createElement('img'); fr.className = 'front'; fr.alt = ''; st.appendChild(fr);
    const sl = document.createElement('div'); sl.className = 'seat'; if (s.seat == null) sl.style.display = 'none'; st.appendChild(sl);
    card.appendChild(st);
    const strip = document.createElement('div'); strip.className = 'strip'; card.appendChild(strip);
    cards.appendChild(card);
    stages.push({ skin: s, im, fr, st, rd, sl, strip });
  });
  mountsEl.appendChild(sec);
});
// the rider's hip (row RIDER_HIP of his 96 picture) on the skin's seat row; RiderOffsetPx > 0 moves him towards the
// head, and the picture faces left; mirrored (facing right) the shift and the picture flip together
function riderPlace(s, flip) {
  if (s.seat == null) return '';
  const C = s.canvas || 96;
  const dy = (s.seat - (s.seat_adj || 0) - C / 2 - (RIDER_HIP - 48)) * scale;  // SeatAdjustPx: positive = higher
  const dx = (flip ? 1 : -1) * (s.rider_dx || 0) * scale;
  return 'translate(calc(-50% + ' + dx + 'px), calc(-50% + ' + dy + 'px))' + (flip ? ' scaleX(-1)' : '');
}
function seatTop(s) { const C = s.canvas || 96; return 'calc(50% + ' + ((s.seat - C / 2) * scale) + 'px)'; }
function frameOf(s, key) { return hasFrames(s) ? (s.frames[key] || DONKEY.frames[key] || '') : ''; }
function frontOf(s, key) { return s.frames['front/' + key] || ''; }
function hasOwn(s, key) { return !!s.frames[key]; }
function buildStrips() {
  const keys = $('view').value === 'all' ? ORDER : CLIPS[$('clip').value];
  stages.forEach(x => {
    x.strip.innerHTML = '';
    keys.forEach((key, i) => {
      const cell = document.createElement('div'); cell.className = 'cell' + (hasOwn(x.skin, key) ? '' : ' fallback');
      const im = document.createElement('img'); im.src = frameOf(x.skin, key); im.alt = key; cell.appendChild(im);
      if (RIDER && x.skin.seat != null && !key.startsWith('hed_')) { const rd = document.createElement('img'); rd.className = 'rider'; rd.src = RIDER; rd.alt = ''; rd.style.transform = riderPlace(x.skin, false); cell.appendChild(rd); }
      const f = frontOf(x.skin, key); if (f) { const fr = document.createElement('img'); fr.className = 'front'; fr.src = f; fr.alt = ''; cell.appendChild(fr); }
      const lb = document.createElement('span'); lb.textContent = (i + 1) + ' ' + key.replace(/^bdy_|_left/g, ''); cell.appendChild(lb);
      x.strip.appendChild(cell);
    });
  });
  applyScale();
}
function show() {
  const keys = CLIPS[$('clip').value];
  const key = keys[frame % keys.length];
  const flip = $('facing').value === 'right';
  stages.forEach(x => {
    x.im.src = frameOf(x.skin, key);
    const f = frontOf(x.skin, key);
    x.fr.style.display = f ? 'block' : 'none'; if (f) x.fr.src = f;
    x.st.classList.toggle('flip', flip);
    x.rd.style.transform = riderPlace(x.skin, flip);
    x.rd.style.visibility = (x.skin.seat != null && !key.startsWith('hed_')) ? 'visible' : 'hidden';
  });
  $('scrub').max = keys.length - 1; $('scrub').value = frame % keys.length;
  $('frameinfo').textContent = $('clip').value + ' ' + (frame % keys.length + 1) + ' / ' + keys.length + '  ' + key;
}
function tick(t) {
  const ms = SPEED[$('clip').value] || 100;
  if (!paused && t - last >= ms) { frame++; last = t; show(); }
  requestAnimationFrame(tick);
}
function hold() { paused = true; $('pause').textContent = 'Play'; }
const clipSel = $('clip');
Object.keys(CLIPS).forEach(c => { const o = document.createElement('option'); o.value = c; o.textContent = c + ' (' + CLIPS[c].length + ')'; clipSel.appendChild(o); });
clipSel.value = 'walk';
clipSel.onchange = () => { frame = 0; buildStrips(); show(); };
$('facing').onchange = show;
$('scale').onchange = e => { scale = +e.target.value; applyScale(); };
$('view').onchange = e => { document.body.classList.toggle('strips', e.target.value === 'strips'); document.body.classList.toggle('all', e.target.value === 'all'); buildStrips(); };
$('scrub').oninput = e => { hold(); frame = +e.target.value; show(); };
$('prev').onclick = () => { hold(); const n = CLIPS[clipSel.value].length; frame = (frame % n + n - 1) % n; show(); };
$('next').onclick = () => { hold(); frame = (frame + 1) % CLIPS[clipSel.value].length; show(); };
$('pause').onclick = e => { paused = !paused; e.target.textContent = paused ? 'Play' : 'Pause'; };
$('night').onclick = e => { document.body.classList.toggle('night'); e.target.textContent = document.body.classList.contains('night') ? 'Day' : 'Night'; e.target.classList.toggle('on'); };
const riderBtn = $('rider');
if (!RIDER) { riderBtn.disabled = true; riderBtn.textContent = 'Rider: export it in the probe first'; }
riderBtn.onclick = () => { document.body.classList.toggle('with-rider'); riderBtn.textContent = document.body.classList.contains('with-rider') ? 'Rider: on' : 'Rider: off'; riderBtn.classList.toggle('on'); };
document.addEventListener('keydown', e => {
  if (e.target.tagName === 'SELECT' || e.target.tagName === 'INPUT') return;
  if (e.key === ' ') { e.preventDefault(); $('pause').click(); }
  if (e.key === 'ArrowLeft') $('prev').click();
  if (e.key === 'ArrowRight') $('next').click();
});
const capsEl = $('caps');
CAPS.forEach(c => {
  const card = document.createElement('div'); card.className = 'card cap';
  const h = document.createElement('h3'); h.textContent = 'In game: ' + c.name + ' (' + c.frames.length + ' pictures)'; card.appendChild(h);
  const im = document.createElement('img'); im.alt = c.name; card.appendChild(im);
  const sl = document.createElement('input'); sl.type = 'range'; sl.min = 0; sl.max = c.frames.length - 1; sl.value = 0; card.appendChild(sl);
  capsEl.appendChild(card);
  c.im = im; c.sl = sl; c.i = 0; c.held = false;
  sl.oninput = () => { c.held = true; c.i = +sl.value; im.src = c.frames[c.i]; };
  sl.onchange = () => { c.held = false; };
  im.src = c.frames[0];
});
let capLast = 0;
function capTick(t) {
  if (!paused && t - capLast >= 1000 / 15) { capLast = t; CAPS.forEach(c => { if (!c.held) { c.i = (c.i + 1) % c.frames.length; c.im.src = c.frames[c.i]; c.sl.value = c.i; } }); }
  requestAnimationFrame(capTick);
}
buildStrips(); show(); requestAnimationFrame(tick); requestAnimationFrame(capTick);
</script>
</body></html>
"""

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
