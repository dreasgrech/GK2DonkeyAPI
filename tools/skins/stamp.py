"""Draw once, get 44 frames (docs/sprite-tools.md "Draw once"): a skin over the donkey drawn as a few pictures on the
standing frame, placed into every frame by the donkey's body points (tools/skins/donkey/rig.json, checked by eye), then
checked and staged on the skins preview page. No rule of the skin's own finds the body.

    python -I tools/skins/stamp.py build <source folder> [--out <folder>] [--stage "<card name>"]
    python -I tools/skins/stamp.py moves <source folder or body.png>
    python -I tools/skins/stamp.py template <source folder> [--force]
    python -I tools/skins/stamp.py export <source folder>
    python -I tools/skins/stamp.py watch <source folder> [--stage "<card name>"]
    python -I tools/skins/stamp.py look <source folder or -> [<frame> ...] [--box x0 y0 x1 y1] [--scale 8] [--out <png>]
    python -I tools/skins/stamp.py views <source folder>

template: the drawing surface, <source folder>/out/pieces.aseprite: a frame per canvas a piece is drawn on, the
donkey locked under them, the current build of that frame faint over it and, in magenta, where the donkey moves
under a head piece drawn there; a layer per piece filled from the source. export: its layers back into the pieces
(a piece changed outside it since is left as it is, and said). watch: export and build on every save in Aseprite.

look: the pieces placed into the frames named (walk_03, turn_05 ...; the standing frame without one) without a
build, each worn over the donkey and zoomed with a grid and its coordinates, the rig's points ringed, then at 1x and
3x: the quick round while drawing, and where to read a coordinate off (<source folder>/out/look.png). "-" for the
source shows the bare donkey (--out needed): the frames to draw on.

views: the head piece in every frame by numbers (its size, box and middle), each clip's neighbours compared: a view
drawn apart from the frame before it shows as a jump (size, box, or a move away from both the poll's and the face's).

moves: before drawing on them, what the placement does to the pieces frame by frame: where body.png's top is a pixel
off the back (the walk's back outline has 1-px bumps), where a pattern across body_drape.png steps, and in which
frames a head piece lies over the donkey's moving parts (the ears' flicks, the blinks, the mouth).

The source folder holds the skin's skin.cfg and its pieces, each a 96x96 PNG on the canvas of the frame it is drawn
on (the asset probe's donkey-frames, the frames every skin is drawn on; transparent where nothing is drawn):

  body.png           on the standing frame: what lies on the body (a cloth, plates, a saddle, badges, bells). In every
                     frame it moves as one with the body: by the hip, and up or down by the middle move of the back
                     line over its columns (from the withers back; held down to the back's level where the neck
                     climbs). It keeps its shape in all 44 frames: it rides the walk's bob, never slides along the
                     body, and a pattern on it never steps. Where the walk's back outline has a 1-px bump its top is
                     a pixel off the back there (stamp.py moves lists them).
  neck.png           on the standing frame: what lies on the collar strap (a collar, a ruff, a garland, bells on the
                     strap). In every frame it moves with the strap, by the rig's chest point (the breast collar's
                     lowest point): in the walk, ear, chew, lips and turn 01-03 the strap only moves, 2-4 px forward
                     of the body in the walk, and that move lays the standing frame's strap on 47-49 of its 50 pixels.
                     In graze 02-05 and turn 04-06 the strap bends (the neck lowers, the breast collar turns to us):
                     there it is left out unless drawn (neck@<frame>.png), and the run says so.
  body_drape.png     on the standing frame: what must hug the back's own outline (a line along the spine): each of its
                     columns moves by the frame's own back line there, so it follows every bump, and anything drawn
                     across a bump steps (stamp.py moves lists them: a pattern or a hem drawn here zig-zagged in walk
                     08, Royal Parade's first drawn-once cut, 2026-11-03). Laid under body.png.
  head.png           on the standing frame: what sits on the head seen from the side (a crown, a hat, horns). In every
                     frame where the head only moves (the walk, ear, chew, lips: the same head moved) it moves with the
                     ear bases; in a frame whose head is drawn another way it is left out unless that view is drawn:
  head_turn.png      on turn 03 (the face three-quarters to us): used in turn 03
  head_front.png     on turn 05 (the face looking at us): used in turn 04-06, moved with their ear bases
  head_down.png      on graze 04 (the head down eating): used in graze 03-05, moved with their ear bases
  <piece>@<frame>.png  any piece drawn on one frame's own canvas, used as drawn there (body@bdy_graze_left_02.png,
                     head@bdy_turn_left_02.png): the way to draw a frame no rule covers (turn 02 and graze 02 have no
                     view of their own; without one the side head is moved there, and the run says so)

Pieces are laid in this order: body_drape, body, neck, then the head's (a view or a one-frame drawing in its place).
The erase colour (FF00FF unless skin.cfg says another) can be drawn in any piece: it clears the donkey there, as in
any pack (docs/skins.md "Erasing from the donkey").

The output (default <source folder>\\out\\pack) is a pack folder: skin.cfg and a PNG per frame. Then
tools/skins/check_skin.py and tools/skins/consistency.py run on it, and with --stage it goes onto the probe's
skins-preview.html as its own card, beside every shipped skin.
"""
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "donkey"))
from gamedir import donkey_frames, game_dir
from anatomy import ORDER, body, load, rig
from watch_skin import ASEPRITE
import ase_write

REPO = os.path.dirname(os.path.dirname(HERE))
STANDING = "bdy_static_left"
VIEWS = {  # a view of the head drawn on its own: (the frame it is drawn on, the frames it is used in)
    "head_turn": ("bdy_turn_left_03", ["bdy_turn_left_03"]),
    "head_front": ("bdy_turn_left_05", ["bdy_turn_left_04", "bdy_turn_left_05", "bdy_turn_left_06"]),
    "head_down": ("bdy_graze_left_04", ["bdy_graze_left_03", "bdy_graze_left_04", "bdy_graze_left_05"]),
}
SIDE_ONLY_MOVED = {"bdy_turn_left_02", "bdy_graze_left_02"}   # no view of their own: the side head moved, said
# where the collar strap bends: the standing frame's strap laid over the frame's by its best shift fits 29-42 of its 50
# pixels (graze 02-05: the neck lowers, the strap's top goes down with it and its end stays; turn 04-06: the breast
# collar turns to us), where every other frame fits 47-50 by the chest point's own move (measured 2026-11-03)
STRAP_BENDS = {"bdy_graze_left_02", "bdy_graze_left_03", "bdy_graze_left_04", "bdy_graze_left_05",
               "bdy_turn_left_04", "bdy_turn_left_05", "bdy_turn_left_06"}
LAYERS = ["body_drape", "body", "neck", "head"]   # the pieces on the standing frame, bottom first (the order laid)


def read_piece(path):
    if not os.path.exists(path):
        return None
    im = Image.open(path).convert("RGBA")
    if im.size != (96, 96):
        sys.exit(f"stamp: {os.path.basename(path)} is {im.size[0]}x{im.size[1]}, not 96x96 (the frames' canvas)")
    px = im.load()
    return {(x, y): px[x, y] for y in range(96) for x in range(96) if px[x, y][3]}


def held_top(key, cols):
    """The body's back line per column (its first pixel from the top), from the rig's withers back (in front of it is
    the collar strap, whose top stands above the back), held down to the back's level where the neck climbs (the
    median of the rear half's tops, less one)."""
    b = body(key, load(key))
    if b is None:
        return {}
    top = b["top"]
    w = rig(key)["withers"]
    xs = sorted(c for c in cols if c in top and (w is None or c >= w[0]))
    if not xs:
        return {}
    rear = sorted(top[x] for x in xs[len(xs) // 2:])
    level = rear[len(rear) // 2]
    return {x: max(top[x], level - 1) for x in xs}


def body_moves(key, cols):
    """(dx, {standing column: dy}): how each standing column of a body piece moves into a frame by the back line."""
    dx = rig(key)["hip"][0] - rig(STANDING)["hip"][0]
    ts = held_top(STANDING, cols)
    tf = held_top(key, [c + dx for c in cols])
    return dx, {x: tf[x + dx] - ts[x] for x in cols if x in ts and x + dx in tf}


def body_move(key, cols):
    """(dx, dy): how a body piece over these columns moves into a frame as one: by the hip, and by the middle of its
    columns' moves (body_moves). None for a frame without a body."""
    if body(key, load(key)) is None:
        return None
    dx, ms = body_moves(key, cols)
    if not ms:
        return None
    v = sorted(ms.values())
    return dx, v[len(v) // 2]


def place_body(art, key):
    """A body piece drawn on the standing frame, placed into a frame as one (body_move): its shape kept. Moved column
    by column (place_drape) a pattern, a motif or a hem stepped wherever the walk's back outline has a 1-px bump
    (Royal Parade's fleur came apart in walk 04-06 and its hem zig-zagged in walk 08, 2026-11-03)."""
    m = body_move(key, sorted({x for x, y in art}))
    return moved(art, m) if m else {}


def place_drape(art, key):
    """A body piece placed column by column: moved with the hip, each column by the frame's back line there. Columns
    outside the body's back line (in front of the withers, behind the hip) keep the nearest column's move."""
    if body(key, load(key)) is None:
        return {}
    dx, moves = body_moves(key, sorted({x for x, y in art}))
    if not moves:
        return {}
    known = sorted(moves)
    out = {}
    for (x, y), c in art.items():
        m = moves.get(x)
        if m is None:
            m = moves[min(known, key=lambda k: abs(k - x))]
        out[(x + dx, y + m)] = c
    return out


def neck_move(key):
    """How a neck piece moves into a frame: with the collar strap, by the rig's chest point. None for a frame without
    one (the head-only frames)."""
    a, b = rig(STANDING)["chest"], rig(key)["chest"]
    if not a or not b:
        return None
    return b[0] - a[0], b[1] - a[1]


def on_the_strap(art):
    """A body piece's pixels in front of the withers and above the back's row there: on the collar strap in the
    standing frame, a post standing on the back wherever the strap moves away (Royal Parade's front outline ran up
    the strap's rear edge, 2026-11-03). The strap moves with the neck: nothing drawn on it is a body piece."""
    w = rig(STANDING)["withers"]
    if not art or w is None:
        return []
    return sorted((x, y) for x, y in art if x < w[0] and y < w[1])


def on_the_harness(key, drawn, erase):
    """The pixels of a frame where the skin's erase colour clears the donkey's harness: its orange (anatomy.body()'s
    collar test: red 90 or more and over 1.3 times the green)."""
    if not erase:
        return []
    don = load(key).convert("RGBA").load()
    return sorted(q for q, c in drawn.items() if c[:3] == erase and don[q][3] and don[q][0] >= 90
                  and don[q][0] > 1.3 * don[q][1])


def head_move(key, ref):
    """The head's move from frame 'ref' to frame 'key' by the ear bases (their mean, rounded). Moving by the face
    (the eyes and the muzzle) instead was tried on 2026-11-03 and taken back: the two agree in every frame but the
    turned heads and turn 04, and there the face moved Royal Parade's crown a pixel sideways for one frame while the
    forehead stays (the review of its drawn-once card); the Jester's turn 04 has a drawing of its own."""
    a, b = rig(ref), rig(key)
    if not (a["ear_left"] and a["ear_right"] and b["ear_left"] and b["ear_right"]):
        return None
    dx = (b["ear_left"][0] + b["ear_right"][0] - a["ear_left"][0] - a["ear_right"][0]) / 2
    dy = (b["ear_left"][1] + b["ear_right"][1] - a["ear_left"][1] - a["ear_right"][1]) / 2
    return int(round(dx)), int(round(dy))


def moved(art, d):
    return {(x + d[0], y + d[1]): c for (x, y), c in art.items()}


def read_source(src, need=True):
    """(pieces, per_frame) of a source folder: {piece: pixels or None}, {(piece, frame key): pixels}; with need, a
    folder without a piece is refused."""
    pieces = {n: read_piece(os.path.join(src, n + ".png")) for n in LAYERS + list(VIEWS)}
    if need and not any(pieces.values()):
        sys.exit(f"stamp: none of body.png, neck.png, head.png, {', '.join(v + '.png' for v in VIEWS)} in {src}")
    per_frame = {}
    for f in os.listdir(src):
        if "@" in f and f.endswith(".png"):
            piece, key = f[:-4].split("@", 1)
            if key not in ORDER:
                sys.exit(f"stamp: {f}: no frame {key}")
            per_frame[(piece, key)] = read_piece(os.path.join(src, f))
    return pieces, per_frame


def frame_of(key, pieces, per_frame, notes):
    """The skin's picture of one frame, {(x, y): colour}: the pieces placed and laid in their order."""
    view_of = {k: v for v, (_, ks) in VIEWS.items() for k in ks}
    layers = []
    # the body
    for name, place in (("body_drape", place_drape), ("body", place_body)):
        if (name, key) in per_frame:
            layers.append(per_frame[(name, key)])
        elif pieces[name]:
            layers.append(place(pieces[name], key))
    # the neck: with the strap, left out where it bends unless drawn there
    if ("neck", key) in per_frame:
        layers.append(per_frame[("neck", key)])
    elif pieces["neck"] and key in STRAP_BENDS:
        notes.append(f"{key}: the collar strap bends there, so no neck piece (draw neck@{key}.png to set it)")
    elif pieces["neck"]:
        d = neck_move(key)
        if d is not None:
            layers.append(moved(pieces["neck"], d))
    # the head
    if ("head", key) in per_frame:
        layers.append(per_frame[("head", key)])
    elif key in view_of and pieces[view_of[key]]:
        ref = VIEWS[view_of[key]][0]
        d = head_move(key, ref)
        layers.append(moved(pieces[view_of[key]], d) if d else pieces[view_of[key]])
    elif key in view_of and pieces["head"]:
        notes.append(f"{key}: no {view_of[key]}.png, so no head piece there (the side view would sit wrong on that "
                     f"head)")
    elif pieces["head"]:
        d = head_move(key, STANDING)
        if d is not None:
            layers.append(moved(pieces["head"], d))
            if key in SIDE_ONLY_MOVED:
                notes.append(f"{key}: the side head moved there (no view of its own: draw head@{key}.png to set it)")
    out = {}
    for layer in layers:
        for (x, y), c in layer.items():
            if 0 <= x < 96 and 0 <= y < 96:
                out[(x, y)] = c
    return out


def build(src, out, stage=None):
    cfg = os.path.join(src, "skin.cfg")
    if not os.path.exists(cfg):
        sys.exit(f"stamp: no skin.cfg in {src}")
    pieces, per_frame = read_source(src)
    notes = []
    for name in ("body_drape", "body"):
        strap = on_the_strap(pieces[name])
        if strap:
            notes.append(f"{name}.png: {len(strap)} px on the collar strap, in front of the withers and above the back "
                         f"({', '.join(f'{x},{y}' for x, y in strap[:6])}{' ...' if len(strap) > 6 else ''}): they "
                         f"stand "
                         f"up as a post wherever the strap moves away; cut them")
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith(".png"):
            os.remove(os.path.join(out, f))
    shutil.copy2(cfg, os.path.join(out, "skin.cfg"))
    erase = erase_of(src)
    for key in ORDER:
        im = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
        px = im.load()
        drawn = frame_of(key, pieces, per_frame, notes)
        for q, c in drawn.items():
            px[q] = c
        im.save(os.path.join(out, key + ".png"))
        cut = on_the_harness(key, drawn, erase)
        if cut:
            notes.append(f"{key}: the erase colour clears {len(cut)} px of the harness (the collar strap's orange: "
                         f"{', '.join(f'{x},{y}' for x, y in cut[:4])}{' ...' if len(cut) > 4 else ''}): an ear's "
                         f"clearing reached the strap (the Jester's graze, 2026-11-03)?")
    for n in notes:
        print("note:", n)
    print(f"stamped: {len(ORDER)} frames -> {out}")
    for tool in ("check_skin.py", "consistency.py"):
        r = subprocess.run([sys.executable, "-I", os.path.join(HERE, tool), out], capture_output=True, text=True)
        lines = (r.stdout or r.stderr).strip().splitlines()
        print(f"{tool}: " + (lines[0] if tool == "check_skin.py" else lines[-1] if lines else "no output"))
        for l in (lines[1:] if tool == "check_skin.py" else lines[:-1]):
            if tool != "check_skin.py" or l.lstrip().startswith("!"):
                # a frame with a body drawing of its own (a hem swaying where the body stands still: the Jester's
                # clips) changes there on purpose: said, so the line is read as the skin's motion, not taken for a slip
                key = l.split(":", 1)[0].strip()
                own = tool == "consistency.py" and any((n, key) in per_frame for n in ("body", "body_drape"))
                print("   " + l.strip() + ("   [its own body drawing: the skin's motion]" if own else ""))
    if stage:
        slug = "".join(ch if ch.isalnum() else "-" for ch in stage.lower()).strip("-")
        # beside the pack folder, in an out\study\ folder: tools/skins/<skin>/out/study/ is where page.py finds studies
        study = os.path.join(os.path.dirname(os.path.abspath(out)), "study", slug)
        if os.path.isdir(study):
            shutil.rmtree(study)
        shutil.copytree(out, study)
        text = open(os.path.join(study, "skin.cfg"), encoding="utf-8").read().replace("\r\n", "\n")
        lines = [f"name = {stage}" if l.strip().startswith("name") and "=" in l and not l.strip().startswith("#") else l
                 for l in text.split("\n")]
        with open(os.path.join(study, "skin.cfg"), "w", encoding="utf-8", newline="\r\n") as f:
            f.write("\n".join(lines))
        r = subprocess.run([sys.executable, "-I", os.path.join(HERE, "page.py"), study], capture_output=True, text=True)
        for l in (r.stdout or r.stderr).strip().splitlines():
            print("page.py: " + l)
    return 0


TEMPLATE = os.path.join("out", "pieces.aseprite")
LOCKED, DRAWN = 1, 3   # an .aseprite layer's flags: visible; visible and editable
FAINT = 110            # the "stamped (preview)" and "moving (guide)" layers' opacity
GUIDE = (255, 0, 200, 255)   # the guide's colour: where the donkey moves under a head piece drawn there (moving())


def canvases(src):
    """The template's frames in order: (frame key, {layer: the piece it is exported to}). The standing frame carries
    the pieces themselves, a view's frame its head view; every other layer of a frame (and the head on turn 02 and
    graze 02) is a drawing for that frame alone, <piece>@<frame>. A frame the source has such a drawing for gets a
    frame too, and where one exists its layer is that drawing (the build uses it there, before any view)."""
    view_on = {ref: v for v, (ref, _) in VIEWS.items()}
    drawn = {f[:-4] for f in os.listdir(src) if "@" in f and f.endswith(".png")} if os.path.isdir(src) else set()
    extra = {d.split("@", 1)[1] for d in drawn}
    necks = os.path.exists(os.path.join(src, "neck.png"))
    keys = [STANDING] + list(view_on) + [k for k in ORDER if (k in SIDE_ONLY_MOVED or k in extra or necks and k in
                                                              STRAP_BENDS) and k not in view_on and k != STANDING]
    out = []
    for k in keys:
        if k == STANDING:
            out.append((k, {l: l for l in LAYERS}))
            continue
        layers = {}
        for l in LAYERS:
            own = f"{l}@{k}"
            layers[l] = own if own in drawn or not (l == "head" and k in view_on) else view_on[k]
        out.append((k, layers))
    return out


CANVASES = os.path.join("out", "pieces.canvases.json")   # the frames a template was made with, for its export


def template_canvases(src):
    """The frames and pieces the source's template was made with (canvases() then), else canvases() now."""
    p = os.path.join(src, CANVASES)
    if os.path.exists(p):
        return [(k, m) for k, m in json.load(open(p, encoding="utf-8"))]
    return canvases(src)


def tracked(path):
    """Whether a path would go into the repository (inside it and not git-ignored)."""
    p = os.path.abspath(path)
    if not p.lower().startswith(REPO.lower() + os.sep):
        return False
    return subprocess.run(["git", "-C", REPO, "check-ignore", "-q", p], capture_output=True).returncode != 0


def template(src, force=False):
    """The drawing surface for a source folder: <source>/out/pieces.aseprite, a frame per canvas a piece is drawn on
    (canvases(), kept in out/pieces.canvases.json for the export). Its layers:
    - the donkey at the bottom, locked: the game's art, so the file never goes into the repository;
    - the current build of that frame over it, locked and faint: what stamp makes of the pieces there, once a build
      ran;
    - on the standing frame and the views, the pixels where the donkey moves under a head piece, locked and faint:
      moving(), the ears' flicks; a piece over them hides them in some frames only;
    - the pieces' layers, filled from the source's PNGs.
    Draw, save, then export (or watch)."""
    path = os.path.join(src, TEMPLATE)
    if tracked(path):
        sys.exit(f"stamp: {path} would go into the repository (it holds the game's frames): git-ignore the skin "
                 f"folder's out/ first")
    if os.path.exists(path) and not force:
        sys.exit(f"stamp: {path} exists (its drawing would be lost): export it first, then template --force")
    frames, tags = [], []
    cs = canvases(src)
    for i, (key, pieces) in enumerate(cs):
        cels = {0: Image.open(os.path.join(donkey_frames(), key + ".png")).convert("RGBA")}
        built = os.path.join(src, "out", "pack", key + ".png")
        if os.path.exists(built):
            cels[1] = Image.open(built).convert("RGBA")
        if key == STANDING or key in {r for r, _ in VIEWS.values()}:
            guide = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
            for q in moving(key):
                guide.putpixel(q, GUIDE)
            cels[2] = guide
        for j, l in enumerate(LAYERS):
            p = os.path.join(src, pieces[l] + ".png")
            if os.path.exists(p):
                cels[3 + j] = Image.open(p).convert("RGBA")
        frames.append(cels)
        name = key[4:].replace("_left", "").replace("_", " ")
        views = [p for l, p in pieces.items() if "@" not in p and p not in LAYERS]
        tags.append((name + (f" ({views[0]})" if views else ""), i, i))
    layers = [("donkey (game)", LOCKED), ("stamped (preview)", LOCKED, FAINT), ("moving (guide)", LOCKED, FAINT)] + [
        (l, DRAWN) for l in LAYERS]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ase_write.write(path, 96, 96, layers, frames, tags, [1000] * len(frames))
    hold(src, {p: pixels_id(os.path.join(src, p + ".png")) for _, pieces in cs for p in pieces.values()})
    with open(os.path.join(src, CANVASES), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(cs, fh, indent=1)
    print(f"template: {path}")
    for (key, pieces), (name, _, _) in zip(cs, tags):
        print(f"   frame {name:24} " + ", ".join(f"{l} -> {p}.png" for l, p in pieces.items()))
    return 0


HELD = os.path.join("out", "pieces.held.json")   # what the template held of each piece when made or last exported


def pixels_id(path):
    """A piece file's pixels as one id (None: no file), to tell a piece changed outside the template."""
    if not os.path.exists(path):
        return None
    return hashlib.sha1(Image.open(path).convert("RGBA").tobytes()).hexdigest()


def hold(src, ids):
    with open(os.path.join(src, HELD), "w", encoding="utf-8", newline="\n") as f:
        json.dump(ids, f, indent=1, sort_keys=True)
        f.write("\n")


def export(src):
    """The template's drawing layers back into the source's pieces (Aseprite's command line exports every layer of
    every frame apart). A piece whose layer is empty on its frame is removed. A piece file changed since the template
    last held it (drawn or edited outside it) is kept, and said: template --force takes it in. Returns the changes."""
    path = os.path.join(src, TEMPLATE)
    if not os.path.exists(path):
        sys.exit(f"stamp: no {path} (stamp.py template <source folder> makes it)")
    held_path = os.path.join(src, HELD)
    held = json.load(open(held_path, encoding="utf-8")) if os.path.exists(held_path) else {}
    tmp = os.path.join(src, "out", "export")
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(tmp)
    exe = os.environ.get("ASEPRITE", ASEPRITE)
    r = subprocess.run([exe, "-b", path, "--all-layers", "--split-layers", "--save-as",
                        os.path.join(tmp, "{layer}.{frame}.png")], capture_output=True, text=True, timeout=300)
    if r.returncode != 0:
        sys.exit(f"stamp: Aseprite's export failed: {(r.stdout + r.stderr).strip()}")
    changes, kept = [], []
    for i, (key, pieces) in enumerate(template_canvases(src)):
        for l in LAYERS:
            name = pieces[l]
            f = os.path.join(tmp, f"{l}.{i}.png")
            im = Image.open(f).convert("RGBA") if os.path.exists(f) else None
            if im is not None and im.size != (96, 96):
                sys.exit(f"stamp: the template's frames are {im.size[0]}x{im.size[1]}, not 96x96 (the canvas was "
                         f"resized?)")
            if im is not None and im.getbbox() is None:
                im = None
            target = os.path.join(src, name + ".png")
            now = pixels_id(target)
            new = hashlib.sha1(im.tobytes()).hexdigest() if im is not None else None
            if now == new:
                held[name] = new
                continue
            if name in held and now != held[name] or name not in held and now is not None:
                kept.append(f"{name}.png")
                continue
            if im is None:
                os.remove(target)
                changes.append(f"{name}.png removed (its layer is empty on {key})")
            else:
                im.save(target)
                changes.append(f"{name}.png {'written' if now is None else 'changed'}")
            held[name] = new
    shutil.rmtree(tmp, ignore_errors=True)
    hold(src, held)
    print("export: " + ("; ".join(changes) if changes else "no piece changed"))
    if kept:
        print(f"export: left as they are: {', '.join(kept)} (changed outside the template since it last held them; "
              f"template --force takes them in, and the template's drawing on those layers is lost)")
    return changes


def watch(src, stage):
    """Export and build on every save of the template (Ctrl+C stops)."""
    path = os.path.join(src, TEMPLATE)
    staging = " and stages " + stage if stage else ""
    print(f"watching {path}: every save exports the pieces and builds{staging} (Ctrl+C stops)")
    last = os.path.getmtime(path)
    while True:
        time.sleep(0.5)
        try:
            m = os.path.getmtime(path)
        except OSError:
            continue   # mid-save: the editor replaces the file
        if m == last:
            continue
        time.sleep(0.6)
        try:
            if os.path.getmtime(path) != m:
                continue   # not settled yet
        except OSError:
            continue
        last = m
        if export(src):
            build(src, os.path.join(src, "out", "pack"), stage)


MARKS = {"hip": (255, 60, 60), "back": (255, 160, 40), "withers": (255, 230, 40), "chest": (120, 230, 60),
         "tail": (60, 220, 220), "ear_left": (90, 140, 255), "ear_right": (170, 110, 255), "eye": (255, 255, 255),
         "eye2": (255, 255, 255), "poll": (255, 90, 220), "muzzle": (255, 200, 150)}   # rig.py's colours


def erase_of(src):
    """The skin's erase colour (skin.cfg's erase =, FF00FF by default; None for 'none')."""
    for line in open(os.path.join(src, "skin.cfg"), encoding="utf-8"):
        s = line.split("#", 1)[0].strip()
        if s.lower().startswith("erase") and "=" in s:
            v = s.split("=", 1)[1].strip()
            return None if v.lower() == "none" else tuple(int(v[i:i + 2], 16) for i in (0, 2, 4))
    return (255, 0, 255)


def worn(key, skin, erase):
    """The donkey's frame with the skin's picture over it, the erase colour clearing the donkey."""
    im = load(key).convert("RGBA").copy()
    px = im.load()
    for q, c in skin.items():
        px[q] = (0, 0, 0, 0) if erase and c[:3] == erase else c
    return im


def moving(ref=STANDING):
    """Where the donkey moves under a head piece drawn on frame 'ref': in every frame that piece is used in (the
    standing frame's: the walk, ear, chew and lips, the same head moved; a view's: its frames), the pixels in front of
    the withers moved back by the head's move and compared with ref's outline (a pixel there in one, none in the
    other; inside the outline the walk's head is shaded otherwise all over, so colours are not compared). {(x, y)
    on ref: the frames it differs in}:
    the ears' flicks and lies, the mouth, the neck behind the head."""
    view_on = {r: ks for _, (r, ks) in VIEWS.items()}
    if ref in view_on:
        keys = [k for k in view_on[ref] if k != ref]
    else:
        # the clips whose head is the standing one moved (rig.py's head_from); turn 02 and graze 02 turn it, so their
        # outline differs nearly everywhere: build names them instead
        keys = [k for k in ORDER if k != ref and k.split("_")[1] in ("walk", "ear", "chew", "lips")]
    s = load(ref).convert("RGBA").load()
    ws = rig(ref)["withers"][0]
    out = {}
    for key in keys:
        d = head_move(key, ref)
        w = rig(key)["withers"]
        if d is None or w is None:
            continue
        f = load(key).convert("RGBA").load()
        for y in range(96):
            for x in range(min(w[0], 96)):
                sx, sy = x - d[0], y - d[1]
                if not (0 <= sx < ws and 0 <= sy < 96):
                    continue
                if bool(f[x, y][3]) != bool(s[sx, sy][3]):   # the outline changes: a part appears or goes
                    out.setdefault((sx, sy), []).append(key)
    return out


def look(src, keys, box=None, scale=8, path=None):
    """The pieces placed into chosen frames without a build: each frame worn (the erase colour clearing the donkey),
    zoomed, a grid every 5 pixels with the coordinates every 10, the rig's points ringed; then each at 1x and 3x. The
    quick round while drawing, and where to read a coordinate off. Written to <source>/out/look.png (it holds the
    game's frames: never in the repository). The source "-" is the bare donkey (no pieces; --out needed): the frames
    to draw on, with their coordinates and points."""
    from PIL import ImageDraw
    if src == "-":
        if not path:
            sys.exit("stamp: look - (the bare donkey) needs --out <png> outside the repository")
        pieces, per_frame, erase = {n: None for n in LAYERS + list(VIEWS)}, {}, None
    else:
        pieces, per_frame = read_source(src, need=False)
        erase = erase_of(src)
    x0, y0, x1, y1 = box or (0, 0, 96, 96)
    W, H = (x1 - x0) * scale, (y1 - y0) * scale
    pad, bg = 28, (74, 96, 60, 255)
    col = max(W, 288 + 10 + 96)
    sheet = Image.new("RGBA", (pad + len(keys) * (col + pad), pad + H + 20 + 96 * 3 + 20), (40, 40, 44, 255))
    d = ImageDraw.Draw(sheet)
    notes = []
    for i, key in enumerate(keys):
        if key not in ORDER:
            sys.exit(f"stamp: no frame {key}")
        full = Image.new("RGBA", (96, 96), bg)
        full.alpha_composite(worn(key, frame_of(key, pieces, per_frame, notes), erase))
        ox = pad + i * (col + pad)
        sheet.alpha_composite(full.crop((x0, y0, x1, y1)).resize((W, H), Image.NEAREST), (ox, pad))
        d.text((ox, 2), key[4:].replace("_left", ""), fill=(230, 230, 120, 255))
        for x in range(x0, x1 + 1):
            if x % 5 == 0:
                X = ox + (x - x0) * scale
                d.line([(X, pad), (X, pad + H)], fill=(150, 150, 60, 255) if x % 10 == 0 else (90, 90, 100, 255))
                if x % 10 == 0:
                    d.text((X + 2, 14), str(x), fill=(210, 210, 210, 255))
        for y in range(y0, y1 + 1):
            if y % 5 == 0:
                Y = pad + (y - y0) * scale
                d.line([(ox, Y), (ox + W, Y)], fill=(150, 150, 60, 255) if y % 10 == 0 else (90, 90, 100, 255))
                if y % 10 == 0 and i == 0:
                    d.text((2, Y + 2), str(y), fill=(210, 210, 210, 255))
        for name, p in rig(key).items():
            if name in MARKS and p and x0 <= p[0] < x1 and y0 <= p[1] < y1:
                X, Y = ox + (p[0] - x0) * scale, pad + (p[1] - y0) * scale
                d.rectangle([X, Y, X + scale - 1, Y + scale - 1], outline=MARKS[name] + (255,))
        sheet.alpha_composite(full.resize((288, 288), Image.NEAREST), (ox, pad + H + 20))
        sheet.alpha_composite(full, (ox + 288 + 10, pad + H + 20))
    path = path or os.path.join(src, "out", "look.png")
    if tracked(path):
        sys.exit(f"stamp: {path} would go into the repository (it holds the game's frames)")
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    sheet.save(path)
    for n in notes:
        print("note:", n)
    print(f"look: {path} ({', '.join(k[4:].replace('_left', '') for k in keys)}; points ringed: "
          + ", ".join(f"{n}" for n in MARKS) + " in rig.py's colours)")
    return 0


def moves(path):
    """What the placement does to a source's body pieces, frame by frame, before anything is drawn on them:
    body.png (moved as one) where its top is off the back line in a frame (a pixel above it or below it, where the
    back outline has a bump), body_drape.png (column by column) where a pattern across it steps. Read before choosing
    a pattern or a shape: vertical stripes and folds never show a step, diagonals, checks, a hem and lettering do."""
    src = path if os.path.isdir(path) else os.path.dirname(path)
    found = False
    for name in ("body", "body_drape"):
        art = read_piece(os.path.join(src, name + ".png"))
        if not art:
            continue
        found = True
        cols = sorted({x for x, y in art})
        top = {}
        for x, y in art:
            top[x] = min(y, top.get(x, y))
        ts = held_top(STANDING, cols)
        steps, alike = {}, 0
        print(f"{name}.png: columns {cols[0]}..{cols[-1]}")
        for key in ORDER:
            m = body_move(key, cols)
            if m is None:
                continue
            dx, ms = body_moves(key, cols)
            if name == "body":
                tf = held_top(key, [c + dx for c in cols])
                off = [(x, (top[x] - ts[x]) - (top[x] + m[1] - tf[x + dx])) for x in cols if x in ts and x + dx in tf]
                off = [(x, d) for x, d in off if d]
                if off:
                    print(f"   {key:22} moved {m[0]:+d}, {m[1]:+d}; its top against the back, off by a pixel at: "
                          + " ".join(f"{x} ({'above' if d > 0 else 'below'})" for x, d in off))
                else:
                    alike += 1
                continue
            known = sorted(ms)
            runs = []
            for x in cols:
                v = ms[x] if x in ms else ms[min(known, key=lambda k: abs(k - x))]
                if not runs or runs[-1][1] != v:
                    runs.append((x, v))
            for (_, a), (x, b) in zip(runs, runs[1:]):
                steps.setdefault(x, []).append(f"{key[4:]} {b - a:+d}")
            if len(runs) > 1:
                print(f"   {key:22} hip {dx:+d}  " + "  ".join(f"{x}: {v:+d}" for x, v in runs))
            else:
                alike += 1
        if name == "body":
            print(f"   ({alike} frames: its top on the back as in the standing frame; it keeps its shape in every "
                  f"frame)")
            continue
        print(f"   ({alike} frames move all its columns alike)")
        for x in sorted(steps):
            print(f"   a pattern crossing column {x} steps there in {len(steps[x])} frame(s): {', '.join(steps[x])}")
    art = read_piece(os.path.join(src, "neck.png"))
    if art:
        found = True
        drawn = {f[5:-4] for f in os.listdir(src) if f.startswith("neck@") and f.endswith(".png")}
        bare = [k for k in ORDER if k in STRAP_BENDS and k not in drawn]
        with_strap = sum(1 for k in ORDER if neck_move(k) and k not in STRAP_BENDS)
        print(f"neck.png: moves with the collar strap (the chest point) in {with_strap} frames; where the strap bends "
              f"it is drawn in {len(STRAP_BENDS) - len(bare)} of {len(STRAP_BENDS)}"
              + (f", left out in {', '.join(k[4:] for k in bare)}" if bare else ""))
    for name, ref in [("head", STANDING)] + [(v, r) for v, (r, _) in VIEWS.items()]:
        art = read_piece(os.path.join(src, name + ".png"))
        if not art:
            continue
        found = True
        mv = moving(ref)
        under = {}
        for q in art:
            for k in mv.get(q, []):
                under[k] = under.get(k, 0) + 1
        print(f"{name}.png (drawn on {ref[4:]}): over the donkey's moving parts in {len(under)} frame(s)"
              + ("" if under else ": nothing under it moves"))
        if under:
            in_order = sorted(under.items(), key=lambda t: ORDER.index(t[0]))
            print("   " + ", ".join(f"{k[4:]} {n} px" for k, n in in_order))
            print("   there the donkey under the piece changes (an ear flicking, a blink, the mouth): the piece hides "
                  "it "
                  "in some frames and not in others; look at them (stamp.py look), or keep the piece off the guide "
                  "layer's pixels (stamp.py template)")
    if not found:
        sys.exit(f"stamp: no body or head piece in {src}")
    return 0


def views(src):
    """The head piece frame by frame, by numbers: its size, its box and its middle in every frame, and each clip's
    neighbours compared. A view drawn apart from the one before it shows here as a jump: its size changing by more
    than an eighth, its box by more than 2 px, or its middle moving more than 2 px away from both the poll's move (the
    top of the head) and the face's. The Jester's reviews measured this by hand (turn 03's cap 162 px between 131 and
    125, the graze cap shrinking, the cap a pixel off in turn 04)."""
    pieces, per_frame = read_source(src)
    heads = {n: p for n, p in pieces.items() if n not in ("body", "body_drape", "neck")}
    heads.update({n: None for n in ("body", "body_drape", "neck")})
    mine = {k: v for k, v in per_frame.items() if k[0] == "head"}
    stats, notes = {}, []
    for key in ORDER:
        px = frame_of(key, heads, mine, notes)
        if not px:
            continue
        xs, ys = [x for x, y in px], [y for x, y in px]
        stats[key] = (len(px), (min(xs), min(ys), max(xs), max(ys)), (sum(xs) / len(xs), sum(ys) / len(ys)))
    jumps = 0
    clips = {}
    for k in ORDER:
        clips.setdefault(k.rsplit("_", 1)[0] if k[-2:].isdigit() else k, []).append(k)
    for c, ks in clips.items():
        ks = [k for k in ks if k in stats]
        if len(ks) < 2:
            continue
        print(f"{c[4:].replace('_left', '')}:")
        for a, b in zip(ks, ks[1:]):
            (na, ba, ma), (nb, bb, mb) = stats[a], stats[b]
            # against the top of the head (the poll, which a cap sits on) and against the face (head_move): across a
            # turn the two part (from turn 02 to 03 the eye moves 7 px right of the poll), so a piece moving away from
            # both is what jumps
            pa, pb = rig(a)["poll"], rig(b)["poll"]
            refs = [("poll", (pb[0] - pa[0], pb[1] - pa[1]))] if pa and pb else []
            refs.append(("face", head_move(b, a) or (0, 0)))
            offs = [(n, d, (mb[0] - ma[0] - d[0], mb[1] - ma[1] - d[1])) for n, d in refs]
            off = min((o for _, _, o in offs), key=lambda o: max(abs(o[0]), abs(o[1])))
            why = []
            if abs(nb - na) > na / 8:
                why.append(f"size {na} -> {nb} px")
            wa, ha, wb, hb = ba[2] - ba[0], ba[3] - ba[1], bb[2] - bb[0], bb[3] - bb[1]
            if abs(wb - wa) > 2 or abs(hb - ha) > 2:
                why.append(f"box {wa + 1}x{ha + 1} -> {wb + 1}x{hb + 1}")
            if max(abs(off[0]), abs(off[1])) > 2:
                why.append("middle " + ", ".join(f"{o[0]:+.1f}, {o[1]:+.1f} beyond the {n}'s move {d}"
                                                 for n, d, o in offs))
            jumps += bool(why)
            print(f"   {a[4:].replace('_left', '')} -> {b[4:].replace('_left', '')}: "
                  + ("; ".join(why) if why else f"steady ({nb} px, middle {off[0]:+.1f}, {off[1]:+.1f})"))
    print(f"{jumps} jump(s) between neighbours")
    return 0   # a report for the eye: a turn's views part from their neighbours by design (the Jester's turn 02 -> 03)


def frame_key(name):
    """A frame by its key or its short name (static, walk_03, turn_05, hed_static_down_02)."""
    if name in ORDER:
        return name
    for k in ORDER:
        if k[4:].replace("_left", "") == name:
            return k
    sys.exit(f"stamp: no frame {name} (a key such as bdy_walk_left_03, or walk_03, static, hed_static_down)")


def main(argv):
    if len(argv) < 2 or argv[0] not in ("build", "moves", "template", "export", "watch", "look", "views"):
        print(__doc__)
        return 2
    cmd, src = argv[0], argv[1]
    stage = argv[argv.index("--stage") + 1] if "--stage" in argv else None
    if cmd == "look":
        box = tuple(int(v) for v in argv[argv.index("--box") + 1:argv.index("--box") + 5]) if "--box" in argv else None
        scale = int(argv[argv.index("--scale") + 1]) if "--scale" in argv else 8
        path = argv[argv.index("--out") + 1] if "--out" in argv else None
        taken = set()
        for flag, n in (("--box", 4), ("--scale", 1), ("--out", 1)):
            if flag in argv:
                i = argv.index(flag)
                taken |= set(range(i, i + n + 1))
        keys = [frame_key(a) for i, a in enumerate(argv[2:], 2) if i not in taken]
        return look(src, keys or [STANDING], box, scale, path)
    if cmd == "views":
        return views(src)
    if cmd == "moves":
        return moves(src)
    if cmd == "template":
        return template(src, "--force" in argv)
    if cmd == "export":
        export(src)
        return 0
    if cmd == "watch":
        try:
            watch(src, stage)
        except KeyboardInterrupt:
            print("stopped")
        return 0
    out = argv[argv.index("--out") + 1] if "--out" in argv else os.path.join(src, "out", "pack")
    return build(src, out, stage)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
