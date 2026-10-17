"""An HTML page that animates every test skin (docs/mount-looks-design.md): each skin in test\\skins\\, next to the plain
donkey (donkey-frames\\), playing the donkey's clips at the game's speed, 4x, pixel-sharp, over day or night ground.
LOCAL ONLY: the frames are the game's art (edited, for the skins); the page embeds them and stays on this machine.
Args: the probe folder (BepInEx\\GK2-asset-probe), the output .html."""
import base64, json, os, re, sys

probe, out = sys.argv[1], sys.argv[2]
frames_dir = os.path.join(probe, "donkey-frames")
skins_dir = os.path.join(probe, "test", "skins")


def load_set(d):
    s = {}
    for f in sorted(os.listdir(d)):
        if f.endswith(".png") and f.startswith(("bdy_", "hed_")):
            s[f[:-4]] = "data:image/png;base64," + base64.b64encode(open(os.path.join(d, f), "rb").read()).decode()
    return s


# films from the probe's Capture tab (captures\<time>\frame_###.png), newest first, at most six
caps_dir = os.path.join(probe, "captures")
captures = []
if os.path.isdir(caps_dir):
    for name in sorted(os.listdir(caps_dir), reverse=True)[:6]:
        d = os.path.join(caps_dir, name)
        files = sorted(f for f in os.listdir(d) if f.endswith(".png"))
        if files:
            captures.append({"name": name, "frames": ["data:image/png;base64," + base64.b64encode(open(os.path.join(d, f), "rb").read()).decode() for f in files]})

sets = {"donkey": load_set(frames_dir)}
for name in sorted(os.listdir(skins_dir)):
    if os.path.isdir(os.path.join(skins_dir, name)):
        sets[name] = load_set(os.path.join(skins_dir, name))

# clips: frame names grouped by clip ("bdy_walk_left_03" -> "walk"), numbered frames in order
clips = {}
for key in sets["donkey"]:
    m = re.match(r"bdy_(.+?)_left(?:_(\d+))?$", key)
    if m:
        clips.setdefault(m.group(1), []).append((int(m.group(2) or 0), key))
clips = {c: [k for _, k in sorted(v)] for c, v in clips.items()}
# the game's clip lengths where known (walk 0.97 s); others at 10 frames a second
speed = {"walk": 970 / 8}

html = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Donkey Skin Previews</title>
<style>
:root { --bg:#1c1f1a; --fg:#e8e6df; --muted:#a9a69c; --card:#262a23; --line:#3a4034; --day:#4f6a3a; --night:#1f2a3d; }
* { box-sizing:border-box; }
body { margin:0; background:var(--bg); color:var(--fg); font:15px/1.4 system-ui, sans-serif; }
header { padding:16px; display:flex; flex-wrap:wrap; gap:12px; align-items:center; border-bottom:1px solid var(--line); }
h1 { font-size:18px; margin:0 12px 0 0; }
label { color:var(--muted); }
select, button { background:var(--card); color:var(--fg); border:1px solid var(--line); border-radius:6px; padding:6px 10px; font:inherit; }
main { padding:16px; display:grid; grid-template-columns:repeat(auto-fill, minmax(260px, 1fr)); gap:16px; }
.card { background:var(--card); border:1px solid var(--line); border-radius:10px; padding:10px; }
.card h2 { font-size:15px; margin:0 0 8px; text-transform:capitalize; }
.stage { background:var(--day); border-radius:6px; display:flex; justify-content:center; align-items:center; overflow:hidden; }
body.night .stage { background:var(--night); }
body.night .stage img { filter:brightness(.55) saturate(.8) hue-rotate(10deg); }
.stage img { width:100%; max-width:384px; aspect-ratio:1; image-rendering:pixelated; }
.stage.flip img { transform:scaleX(-1); }
p.note { color:var(--muted); margin:6px 16px 0; font-size:13px; }
#caps { padding:16px 16px 0; display:grid; grid-template-columns:repeat(auto-fill, minmax(320px, 1fr)); gap:16px; }
#caps:empty { display:none; }
.cap img { width:100%; image-rendering:pixelated; border-radius:6px; display:block; }
.cap input { width:100%; }
</style></head>
<body>
<header>
  <h1>Donkey skin previews</h1>
  <label>Clip <select id="clip"></select></label>
  <label>Facing <select id="facing"><option value="left">left (as drawn)</option><option value="right">right (mirrored)</option></select></label>
  <button id="night">Night</button>
  <button id="pause">Pause</button>
</header>
<p class="note">Recipe prototypes made from the donkey's own frames (tools/skin_recipes.py). Local only: the frames are the game's art. Night is only a rough tint, not the game's lighting.</p>
<section id="caps"></section>
<main id="grid"></main>
<script>
const SETS = __SETS__;
const CLIPS = __CLIPS__;
const SPEED = __SPEED__;
const CAPS = __CAPS__;
const capsEl = document.getElementById('caps');
CAPS.forEach(c => {
  const card = document.createElement('div'); card.className = 'card cap';
  const h = document.createElement('h2'); h.textContent = 'In game: ' + c.name + ' (' + c.frames.length + ' pictures)'; card.appendChild(h);
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
  if (!paused && t - capLast >= 1000 / 15) {
    capLast = t;
    CAPS.forEach(c => { if (!c.held) { c.i = (c.i + 1) % c.frames.length; c.im.src = c.frames[c.i]; c.sl.value = c.i; } });
  }
  requestAnimationFrame(capTick);
}
const clipSel = document.getElementById('clip');
Object.keys(CLIPS).forEach(c => { const o = document.createElement('option'); o.value = c; o.textContent = c + ' (' + CLIPS[c].length + ')'; clipSel.appendChild(o); });
clipSel.value = 'walk' in CLIPS ? 'walk' : Object.keys(CLIPS)[0];
const grid = document.getElementById('grid');
const imgs = {};
Object.keys(SETS).forEach(name => {
  const card = document.createElement('div'); card.className = 'card';
  const h = document.createElement('h2'); h.textContent = name; card.appendChild(h);
  const st = document.createElement('div'); st.className = 'stage';
  const im = document.createElement('img'); im.alt = name; st.appendChild(im); card.appendChild(st);
  grid.appendChild(card); imgs[name] = { im, st };
});
let frame = 0, paused = false, last = 0;
function show() {
  const keys = CLIPS[clipSel.value];
  const key = keys[frame % keys.length];
  for (const name in imgs) {
    const s = SETS[name];
    imgs[name].im.src = s[key] || SETS.donkey[key];
    imgs[name].st.classList.toggle('flip', document.getElementById('facing').value === 'right');
  }
}
function tick(t) {
  const ms = SPEED[clipSel.value] || 100;
  if (!paused && t - last >= ms) { frame++; last = t; show(); }
  requestAnimationFrame(tick);
}
clipSel.onchange = () => { frame = 0; show(); };
document.getElementById('facing').onchange = show;
document.getElementById('night').onclick = e => { document.body.classList.toggle('night'); e.target.textContent = document.body.classList.contains('night') ? 'Day' : 'Night'; };
document.getElementById('pause').onclick = e => { paused = !paused; e.target.textContent = paused ? 'Play' : 'Pause'; };
show(); requestAnimationFrame(tick); requestAnimationFrame(capTick);
</script>
</body></html>
"""
html = html.replace("__SETS__", json.dumps(sets)).replace("__CLIPS__", json.dumps(clips)).replace("__SPEED__", json.dumps(speed)).replace("__CAPS__", json.dumps(captures))
open(out, "w", encoding="utf-8").write(html)
print("ok", out, "captures:", len(captures), "skins:", ", ".join(sets), "clips:", ", ".join(f"{c} {len(v)}" for c, v in clips.items()), f"{len(html)//1024} KB")
