"""The skin preview page: every skin the API knows, grouped by mount, playing the donkey's clips next to the plain donkey
at the game's speed, pixel-sharp, with a frame-strip view for a frame-by-frame look (docs/sprite-tools.md).

Sources, in this order (the first that holds a skin id wins):
  1. the plain donkey: <probe>\\donkey-frames\\ (the game's 44 frames on the 96 canvas, from the asset probe);
  2. the built-in skins: <repo>\\src\\GK2DonkeyAPI\\assets\\skins\\<id>\\ (skin.cfg + a picture per frame, front\\ for the
     front layer; a skin with source = game has no pictures and is listed as such);
  3. the packs in the game: <probe>\\..\\plugins\\gk2skins\\<id>\\ (skin.cfg + pictures, or one sheet of cells);
  4. recipes, drawn from the donkey's frames by the game's code and saved as pictures: --recipe <id>=<folder>
     (Hellfire: C:\\ClaudeCodeTemp\\GK2-donkey-frames\\hellfire-96 when it exists, the frames the game's code drew,
     saved from the raw harness_frames\\*.out);
  5. the leftover test folders <probe>\\test\\skins\\<name>\\ without a skin.cfg: "Experiments", collapsed at the end.
A skin drawn over the donkey (layer = over) is composited over the donkey's frame here, as the game draws it (without
the player's colours: the page shows the art as drawn). The captures of the probe's Capture tab go on top as before.

LOCAL ONLY: the frames are the game's art (and ours); the page embeds them and stays on this machine.

    python tools/skin_preview_html.py <probe folder> <out.html> [--recipe id=folder ...]
"""
import base64, io, json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
ORDER = (["bdy_static_left"] + [f"bdy_walk_left_{i:02d}" for i in range(1, 9)] + [f"bdy_ear_left_{i:02d}" for i in range(1, 6)]
         + [f"bdy_graze_left_{i:02d}" for i in range(1, 6)] + [f"bdy_chew_left_{i:02d}" for i in range(1, 4)]
         + [f"bdy_lips_left_{i:02d}" for i in range(1, 14)] + [f"bdy_turn_left_{i:02d}" for i in range(1, 7)]
         + ["hed_static_down", "hed_static_down_02", "hed_static_down_03"])
MOUNT_NAMES = {"donkey": "Donkey", "gerry": "Gerry"}  # the rest come from the [Mount] section of the skin that declares them


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


def composite_over(base_frames, over_frames):
    """An over skin's frames drawn over the donkey's: what the game shows (without the player's colours)."""
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
            base.alpha_composite(ov)
        buf = io.BytesIO(); base.save(buf, "PNG"); out[k] = buf.getvalue()
    for k, v in over_frames.items():
        if k.startswith("front/"):
            out[k] = v
    return out


def main(argv):
    if len(argv) < 2:
        print(__doc__); return 2
    probe, out_path = argv[0], argv[1]
    recipes = {}
    i = 2
    while i < len(argv):
        if argv[i] == "--recipe" and i + 1 < len(argv) and "=" in argv[i + 1]:
            rid, folder = argv[i + 1].split("=", 1); recipes[rid] = folder; i += 2
        else:
            i += 1
    # Hellfire as the game's code drew it, saved from the probe's raw frames (C:\ClaudeCodeTemp\GK2-aseprite\harness_frames\*.out)
    hellfire = r"C:\ClaudeCodeTemp\GK2-donkey-frames\hellfire-96"
    if "hellfire" not in recipes and os.path.isdir(hellfire):
        recipes["hellfire"] = hellfire

    donkey_dir = os.path.join(probe, "donkey-frames")
    donkey = frames_from_folder(donkey_dir)
    skins = []
    mount_names = dict(MOUNT_NAMES)
    seen = set()

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
            note = "no pictures of its own: the game's " + cfg.get(("Game", "prefix"), "") + " frames, read back and scaled at run time; not shown here"
        shown = frames
        if layer == "over" and frames:
            shown = composite_over(donkey, frames)
            note = "drawn over the donkey (layer = over): shown composited, without the player's colours"
        missing = [k for k in ORDER if k not in frames] if frames else ORDER
        skins.append({"id": sid, "name": name, "mount": mount, "source": source, "layer": layer, "canvas": max(cw, ch),
                      "frames": {k: data_url(v) for k, v in shown.items()}, "note": note, "missing": len(missing), "folder": folder})

    # 1. the plain donkey
    skins.append({"id": "donkey", "name": "The game's own", "mount": "donkey", "source": "the game", "layer": "replace", "canvas": 96,
                  "frames": {k: data_url(v) for k, v in donkey.items()}, "note": "the donkey as the game draws it: the reference", "missing": 0, "folder": donkey_dir})
    seen.add("donkey")
    # 2. the built-in skins
    builtin = os.path.join(REPO, "src", "GK2DonkeyAPI", "assets", "skins")
    if os.path.isdir(builtin):
        for d in sorted(os.listdir(builtin)):
            p = os.path.join(builtin, d)
            if os.path.isfile(os.path.join(p, "skin.cfg")):
                cfg = read_cfg(os.path.join(p, "skin.cfg"))
                frames = frames_from_sheet(p, cfg) if ("Skin", "sheet") in cfg else frames_from_folder(p)
                add(cfg.get(("Skin", "id"), d), cfg.get(("Skin", "name"), d), cfg, frames, "built in", p)
    # 3. the packs in the game
    packs = os.path.normpath(os.path.join(probe, "..", "plugins", "gk2skins"))
    if os.path.isdir(packs):
        for d in sorted(os.listdir(packs)):
            p = os.path.join(packs, d)
            if os.path.isfile(os.path.join(p, "skin.cfg")):
                cfg = read_cfg(os.path.join(p, "skin.cfg"))
                frames = frames_from_sheet(p, cfg) if ("Skin", "sheet") in cfg else frames_from_folder(p)
                add(cfg.get(("Skin", "id"), d), cfg.get(("Skin", "name"), d), cfg, frames, "pack in the game", p)
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

    page = HTML.replace("__GROUPS__", json.dumps(groups)).replace("__CLIPS__", json.dumps(clips)).replace("__SPEED__", json.dumps(speed)) \
               .replace("__CAPS__", json.dumps(captures)).replace("__RIDER__", json.dumps(rider)).replace("__ORDER__", json.dumps(ORDER))
    open(out_path, "w", encoding="utf-8").write(page)
    print("ok", out_path, f"{len(page) // 1024} KB")
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
.stage .rider { pointer-events:none; display:none; }
.stage .empty { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; color:var(--muted); font-size:13px; }
body.with-rider .stage .rider { display:block; }
body.night .stage { background:var(--night); }
body.night .stage img { filter:brightness(.55) saturate(.8) hue-rotate(10deg); }
.strip { display:none; background:var(--day); border-radius:6px; overflow-x:auto; white-space:nowrap; padding:4px; }
.strip .cell { display:inline-block; vertical-align:top; position:relative; width:var(--stage); height:var(--stage); margin-right:4px; }
.strip .cell img { position:absolute; image-rendering:pixelated; left:50%; top:50%; transform:translate(-50%, -50%); }
.strip .cell span { position:absolute; left:3px; top:2px; font-size:11px; color:#fff; text-shadow:0 0 3px #000; }
.strip .cell.fallback img { opacity:.45; }
body.night .strip { background:var(--night); }
body.night .strip img { filter:brightness(.55) saturate(.8) hue-rotate(10deg); }
body.strips .stage { display:none; }
body.strips .strip { display:block; }
body.strips .cards { grid-template-columns:1fr; }
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
  <label>View <select id="view"><option value="anim">animated</option><option value="strips">frame strips</option></select></label>
  <label>Scale <select id="scale"><option>2</option><option selected>3</option><option>4</option><option>5</option></select></label>
  <label>Facing <select id="facing"><option value="left">left (as drawn)</option><option value="right">right (mirrored)</option></select></label>
  <button id="night">Night</button>
  <button id="rider">Rider: off</button>
</header>
<nav id="nav"></nav>
<p class="note">Every skin the API knows, by mount: the mount's own look first, then by name. Local only: the frames are the game's art and ours. The page shows the art as drawn (no player colours); night is a rough tint, not the game's lighting. A frame a skin has no picture for shows the donkey's own (faded in the strips). Frame strips: every frame of the clip side by side, for a frame-by-frame look. Keys: space pauses, the arrows step.</p>
<section id="caps"></section>
<div id="mounts"></div>
<script>
const GROUPS = __GROUPS__, CLIPS = __CLIPS__, SPEED = __SPEED__, RIDER = __RIDER__, ORDER = __ORDER__, CAPS = __CAPS__;
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
    x.strip.querySelectorAll('img').forEach(i => { i.style.width = i.style.height = n + 'px'; });
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
    meta.innerHTML = '<code>' + s.id + '</code> &middot; ' + s.source + ' &middot; ' + s.layer + ' &middot; ' + s.canvas + ' px' + (s.missing ? ' &middot; <b>' + s.missing + ' frames missing</b>' : '') + (s.note ? '<br>' + s.note : '');
    card.appendChild(meta);
    const st = document.createElement('div'); st.className = 'stage';
    if (!hasFrames(s)) { const e = document.createElement('div'); e.className = 'empty'; e.textContent = 'no pictures to show'; st.appendChild(e); }
    const im = document.createElement('img'); im.alt = s.name; if (!hasFrames(s)) im.style.display = 'none'; st.appendChild(im);
    const rd = document.createElement('img'); rd.className = 'rider'; rd.alt = ''; if (RIDER) rd.src = RIDER; st.appendChild(rd);
    const fr = document.createElement('img'); fr.className = 'front'; fr.alt = ''; st.appendChild(fr);
    card.appendChild(st);
    const strip = document.createElement('div'); strip.className = 'strip'; card.appendChild(strip);
    cards.appendChild(card);
    stages.push({ skin: s, im, fr, st, rd, strip });
  });
  mountsEl.appendChild(sec);
});
function frameOf(s, key) { return hasFrames(s) ? (s.frames[key] || DONKEY.frames[key] || '') : ''; }
function hasOwn(s, key) { return !!s.frames[key]; }
function buildStrips() {
  const keys = CLIPS[$('clip').value];
  stages.forEach(x => {
    x.strip.innerHTML = '';
    keys.forEach((key, i) => {
      const cell = document.createElement('div'); cell.className = 'cell' + (hasOwn(x.skin, key) ? '' : ' fallback');
      const im = document.createElement('img'); im.src = frameOf(x.skin, key); im.alt = key; cell.appendChild(im);
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
    const f = x.skin.frames['front/' + key] || x.skin.frames['front/' + key.replace(/_[0-9]+$/, '')] || x.skin.frames['front/' + key.split('_')[0]];
    x.fr.style.display = f ? 'block' : 'none'; if (f) x.fr.src = f;
    x.st.classList.toggle('flip', flip);
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
$('view').onchange = e => { document.body.classList.toggle('strips', e.target.value === 'strips'); };
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
