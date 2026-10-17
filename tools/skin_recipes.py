"""Prototype "recipe" skins (docs/mount-looks-design.md): transforms of the donkey's own frames (donkey-frames\\*.png, 96x96).
LOCAL PREVIEW ONLY: these are edits of the game's art, for judging ideas in game through the probe's test skins.
A shipped skin would be the recipe applied at runtime by the API, never these files.
Args: frames dir, test dir (writes <test>\\skins\\<recipe>\\*.png), preview png path."""
import os, struct, sys, zlib, colorsys, hashlib, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngcolors import rd  # noqa: E402


def write(path, w, h, px):
    raw = b"".join(b"\x00" + bytes(c for p in px[y * w:(y + 1) * w] for c in p) for y in range(h))
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def load(path):
    w, h, rows = rd(path)
    return w, h, [tuple(rows[y][x * 4:x * 4 + 4]) for y in range(h) for x in range(w)]


def lum(c):
    return (c[0] * 0.3 + c[1] * 0.59 + c[2] * 0.11) / 255.0


def ramp(stops, t):
    t = max(0.0, min(1.0, t))
    for i in range(len(stops) - 1):
        a, b = stops[i], stops[i + 1]
        if a[0] <= t <= b[0]:
            k = (t - a[0]) / (b[0] - a[0]) if b[0] > a[0] else 0
            return tuple(int(a[1][j] + (b[1][j] - a[1][j]) * k) for j in range(3))
    return stops[-1][1]


def rim(px, w, h, x, y):
    """Opaque with a transparent neighbour: the outline."""
    if px[y * w + x][3] == 0:
        return False
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = x + dx, y + dy
        if nx < 0 or ny < 0 or nx >= w or ny >= h or px[ny * w + nx][3] == 0:
            return True
    return False


def inner(px, w, h, x, y):
    """One pixel inside the outline."""
    if px[y * w + x][3] == 0 or rim(px, w, h, x, y):
        return False
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        if rim(px, w, h, x + dx, y + dy):
            return True
    return False


# How far this frame's back sits below the standing frame's (row 41 at column 58): patterns use y - BOB, so they ride
# with the body's bob instead of staying fixed on the canvas while the donkey moves under them.
BOB = 0
BACK_COL, BACK_ROW = 58, 41


def frame_bob(px, w, h):
    for y in range(h):
        if px[y * w + BACK_COL][3]:
            return y - BACK_ROW
    return 0


def h01(x, y, salt):
    return hashlib.md5(f"{x},{y},{salt}".encode()).digest()[0] / 255.0


# Each recipe: (w, h, px, name) -> new px. Shading comes from the donkey's own luminance, so the art keeps its form.
def zebra(w, h, px, name):
    out = []
    for i, c in enumerate(px):
        x, y = i % w, i // w
        if c[3] == 0:
            out.append(c); continue
        L = lum(c)
        if L < 0.17 or rim(px, w, h, x, y):
            out.append((18, 18, 20, c[3])); continue
        yy = y - BOB
        stripe = ((x + yy // 2) // 3) % 2 == 0
        v = ramp([(0, (150, 150, 145)), (1, (245, 245, 238))], L * 2.2) if stripe else ramp([(0, (14, 14, 16)), (1, (52, 52, 56))], L * 2)
        out.append(v + (c[3],))
    return out


def ghost(w, h, px, name):
    out = []
    for i, c in enumerate(px):
        x, y = i % w, i // w
        if c[3] == 0:
            out.append(c); continue
        L = lum(c)
        if rim(px, w, h, x, y):
            out.append((190, 245, 255, 235)); continue
        v = ramp([(0, (40, 90, 120)), (0.5, (120, 200, 225)), (1, (225, 250, 255))], L * 1.8)
        out.append(v + (int(c[3] * 0.62),))
    return out


def ember(w, h, px, name):
    """Charcoal body cracked with glowing lava along the outline: an infernal donkey for Hellfire."""
    out = []
    for i, c in enumerate(px):
        x, y = i % w, i // w
        if c[3] == 0:
            out.append(c); continue
        L = lum(c)
        if rim(px, w, h, x, y):
            out.append((255, 150, 40, 255)); continue
        yy = y - BOB
        if inner(px, w, h, x, y) and h01(x, yy // 2, name) > 0.45:
            out.append((210, 60, 15, 255)); continue
        crack = h01(x // 2, yy // 3, "crack") > 0.88
        if crack:
            out.append((255, 110, 20, 255)); continue
        v = ramp([(0, (12, 8, 8)), (1, (70, 42, 36))], L * 1.7)
        out.append(v + (c[3],))
    return out


def zombie(w, h, px, name):
    out = []
    for i, c in enumerate(px):
        x, y = i % w, i // w
        if c[3] == 0:
            out.append(c); continue
        L = lum(c)
        if L < 0.15 or rim(px, w, h, x, y):
            out.append((24, 30, 20, c[3])); continue
        yy = y - BOB
        rot = h01(x // 3, yy // 3, "rot") > 0.8
        stitch = (x % 9 == 0 and h01(x // 9, yy // 2, "st") > 0.6)
        if stitch:
            out.append((40, 26, 20, 255)); continue
        base = [(0, (40, 52, 34)), (0.5, (98, 118, 76)), (1, (170, 182, 132))] if not rot else [(0, (52, 30, 34)), (1, (128, 74, 80))]
        out.append(ramp(base, L * 1.9) + (c[3],))
    return out


def golden(w, h, px, name):
    out = []
    for i, c in enumerate(px):
        x, y = i % w, i // w
        if c[3] == 0:
            out.append(c); continue
        L = lum(c)
        if rim(px, w, h, x, y):
            out.append((70, 40, 8, c[3])); continue
        v = ramp([(0, (90, 52, 10)), (0.35, (190, 130, 30)), (0.7, (250, 210, 90)), (1, (255, 250, 210))], L * 2.0)
        if h01(x, y - BOB, "glint") > 0.985:
            v = (255, 255, 240)
        out.append(v + (c[3],))
    return out


def shadow(w, h, px, name):
    out = []
    for i, c in enumerate(px):
        x, y = i % w, i // w
        if c[3] == 0:
            out.append(c); continue
        L = lum(c)
        if rim(px, w, h, x, y):
            out.append((150, 80, 230, 255)); continue
        v = ramp([(0, (6, 4, 10)), (1, (40, 28, 58))], L * 1.6)
        out.append(v + (c[3],))
    return out


FRAME = ""  # the file being made (dragon's wing flaps with the walk frame)


def inside(poly, x, y):
    """Point in polygon (even-odd), pixel centres."""
    px_, py_ = x + 0.5, y + 0.5
    hit = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if (yi > py_) != (yj > py_) and px_ < (xj - xi) * (py_ - yi) / (yj - yi) + xi:
            hit = not hit
        j = i
    return hit


def dragon(w, h, px, name, off=0, high=False):
    """The donkey as a small dragon: green scales over its own shading, the ears turned to horns, spikes along the
    spine, a bat wing rising from behind the back (flapping with the 8 walk frames), a spade on the tail. The orange
    harness stays (a saddle). Facing left, as all the donkey's frames."""
    head_layer = FRAME.startswith("hed_")
    op = [(i % w, i // w) for i, c in enumerate(px) if c[3]]
    if not op:
        return list(px)
    x0, x1 = min(p[0] for p in op), max(p[0] for p in op)
    y0, y1 = min(p[1] for p in op), max(p[1] for p in op)
    bw = x1 - x0 + 1
    top = {}
    for x, y in op:
        top[x] = min(top.get(x, 99), y)
    # the back: the right part of the body, without the tail's end
    back_cols = [x for x in range(44 + off, 67 + off) if x in top]  # the torso's columns in every frame (measured)
    back_top = min((top[x] for x in back_cols), default=y1)

    def run_width(x, y):
        a = x
        while a - 1 >= 0 and px[y * w + a - 1][3]:
            a -= 1
        b = x
        while b + 1 < w and px[y * w + b + 1][3]:
            b += 1
        return b - a + 1

    out = list(px)
    head_limit = x0 + int(bw * 0.45)
    for i, c in enumerate(px):
        x, y = i % w, i // w
        if c[3] == 0:
            continue
        r_, g_, b_ = c[0] / 255, c[1] / 255, c[2] / 255
        hh, ss, vv = colorsys.rgb_to_hsv(r_, g_, b_)
        harness = ss > 0.45 and 0.03 < hh < 0.13 and vv > 0.25
        if harness:
            continue  # keep the saddle
        L = lum(c)
        if rim(px, w, h, x, y):
            out[i] = (12, 26, 14, c[3]); continue
        # ears to horns: thin runs above the back line in the head's part (or anywhere in the head layer's top)
        if (head_layer or x < head_limit) and y < back_top - 2 and run_width(x, y) <= 6:
            out[i] = ramp([(0, (120, 105, 80)), (1, (240, 228, 196))], L * 2.2) + (c[3],); continue
        yy = y - BOB
        scale = (yy % 3 == 0 and (x + (yy // 3) * 2) % 4 == 0)
        v = ramp([(0, (14, 40, 22)), (0.4, (40, 104, 48)), (0.75, (118, 170, 64)), (1, (214, 222, 120))], L * 1.9)
        if scale:
            v = tuple(int(k * 0.72) for k in v)
        out[i] = v + (c[3],)
    if head_layer:
        return out

    def put(x, y, col, behind_only=False):
        if 0 <= x < w and 0 <= y < h and (not behind_only or px[y * w + x][3] == 0):
            out[y * w + x] = col

    # spikes along the spine: every 4 columns, 3 high, from the back's top pixel
    spike = (150, 30, 24, 255)
    spike_hi = (210, 70, 40, 255)
    for k, x in enumerate(back_cols[::4]):  # columns 44, 48, ... 64: fixed on the torso
        t = top[x]
        put(x, t - 1, spike_hi); put(x, t - 2, spike); put(x, t - 3, spike)
        put(x - 1, t - 1, spike); put(x + 1, t - 1, spike)
    # the wing: a bat wing from the shoulder, behind the body; flaps with the walk
    flap = 0
    if "_walk_" in FRAME:
        try:
            k = int(FRAME.rsplit("_", 1)[1].split(".")[0])
            flap = round(6 * math.sin((k - 1) / 8 * 2 * math.pi))
        except ValueError:
            flap = 0
    FLANK = 6  # how far down the flank the near wing's base reaches, in pixels below the back line
    ROOT = BACK_ROW + BOB + off  # the back line at column 58 in this frame: both wings move with it, rigidly
    # high wings (dragon_high): the rider covers the back from the middle to the rump and reaches ~24 px above it
    # (screenshots 2026-10-17), so these start at the shoulders, in front of his knee, and rise ~1.9x as high and
    # 1.5x as long, to show above and behind him
    SX, SY = (1.3, 1.75) if high else (1.0, 1.0)

    def on_back(x, y):
        return True  # rigid wings: never cut along the body's outline (it changes shape with the legs and neck)

    def draw_wing(rx, ry, f, mem, mem_dark, bone, behind, wide=False):
        def S(pts):
            # scale the shape about its root (rx, ry): wider back, taller up
            return [(round(rx + (x - rx) * SX), round(ry + (y - ry) * (SY if y < ry else 1.0))) for x, y in pts]
        if wide:
            # the near wing: a rigid shape whose base hangs a few pixels down our side of the body, across ~18 px,
            # with a slight sag; it moves only with the back's bob, so it reads as fixed to the shoulder
            wing = [(rx - 2, ry + 12), (rx + 1, ry - 7), (rx + 6, ry - 17 + f), (rx + 14, ry - 22 + f), (rx + 17, ry - 15 + f),
                    (rx + 14, ry - 10 + f // 2), (rx + 21, ry - 7 + f // 2), (rx + 18, ry + 12), (rx + 8, ry + 13)]
        else:
            wing = [(rx, ry + 1), (rx + 3, ry - 7), (rx + 8, ry - 17 + f), (rx + 16, ry - 22 + f), (rx + 19, ry - 15 + f),
                    (rx + 16, ry - 10 + f // 2), (rx + 23, ry - 7 + f // 2), (rx + 15, ry - 3), (rx + 10, ry + 1)]
        wing = S(wing)
        xs = [p[0] for p in wing]; ys = [p[1] for p in wing]
        for y in range(max(0, min(ys)), min(h, max(ys) + 1)):
            for x in range(max(0, min(xs)), min(w, max(xs) + 1)):
                if inside(wing, x, y):
                    if behind or on_back(x, y):
                        put(x, y, mem_dark if (x - rx) % 6 == 0 else mem, behind_only=behind)
        # wing bones: root to tip and to the two finger points
        bones = ((((rx, ry + 11), (rx + 14, ry - 22 + f)), ((rx + 6, ry - 15 + f), (rx + 21, ry - 7 + f // 2)))
                 if wide else (((rx + 2, ry - 2), (rx + 16, ry - 22 + f)), ((rx + 8, ry - 15 + f), (rx + 23, ry - 7 + f // 2))))
        for (ax, ay), (bx, by) in [tuple(S([a, b])) for a, b in bones]:
            n = max(abs(bx - ax), abs(by - ay))
            for s in range(n + 1):
                bx_, by_ = round(ax + (bx - ax) * s / n), round(ay + (by - ay) * s / n)
                if behind or on_back(bx_, by_):
                    put(bx_, by_, bone, behind_only=behind)

    rx = (44 if high else 50) + off  # the shoulder column, fixed on the torso (high: the top of the shoulder, behind the neck)
    ry = ROOT
    # the far wing: behind the body, darker, a little further back
    draw_wing(rx + (12 if high else 8), ry - 3, flap, (72, 22, 34, 255), (48, 14, 26, 255), (22, 10, 14, 255), True)
    # the near wing: over the body, brighter, rooted a little lower and further forward (the side facing the camera)
    nx = rx - 4
    near_before = list(out)
    draw_wing(nx, ry + FLANK - (15 if high else 12), flap, (128, 40, 52, 255), (92, 26, 38, 255), (34, 14, 18, 255), False, wide=True)
    # its lower edge lies on our side of the body: a shadow line under the lowest wing pixel of each column on the body
    for x in range(w):
        ys = [y for y in range(h) if out[y * w + x] != near_before[y * w + x]]
        if ys and x in top:
            yb = max(ys) + 1
            if yb < h and px[yb * w + x][3]:
                c = out[yb * w + x]
                out[yb * w + x] = (int(c[0] * 0.55), int(c[1] * 0.55), int(c[2] * 0.55), c[3])
    # the shoulder joint: a small bony knob on the flank where the near wing starts
    jy = ry + FLANK - 1
    for dx, dy in ((0, 0), (1, 0), (2, 0), (1, -1), (0, 1), (1, 1), (2, 1)):
        put(nx + dx, jy + dy, (40, 18, 22, 255) if dy == 1 else (88, 38, 40, 255))
    put(nx + 1, jy, (150, 70, 62, 255))
    # a spade on the tail: the lowest of the rightmost pixels in the lower half
    # the tail hangs at the right end above the hooves: rows between 45% and 82% of the height (the hooves are lower)
    low = [(x, y) for x, y in op if y0 + (y1 - y0) * 0.45 < y < y0 + (y1 - y0) * 0.82]
    if low:
        mx = max(p[0] for p in low)
        tx, ty = max((p for p in low if p[0] >= mx - 1), key=lambda p: p[1])
        for dx, dy in ((1, 0), (2, 0), (2, -1), (2, 1), (3, 0), (3, -2), (3, 2), (4, 0), (4, -1), (4, 1), (5, 0)):
            put(tx + dx, ty + dy, spike)
        put(tx + 3, ty, spike_hi)
    return out


PAD = 16


def dragon_high(w, h, px, name):
    W, H = w + 2 * PAD, h + 2 * PAD
    big = [(0, 0, 0, 0)] * (W * H)
    for y in range(h):
        for x in range(w):
            big[(y + PAD) * W + x + PAD] = px[y * w + x]
    return W, H, dragon(W, H, big, name, off=PAD, high=True)


RECIPES = {"zebra": zebra, "ghost": ghost, "ember": ember, "zombie": zombie, "golden": golden, "shadow": shadow,
           "dragon": dragon, "dragon_high": dragon_high}

frames, test, preview = sys.argv[1], sys.argv[2], sys.argv[3]
names = sorted(f for f in os.listdir(frames) if f.endswith(".png") and f.startswith(("bdy_", "hed_")))  # not the cart wheels
done = {}
for rname, fn in RECIPES.items():
    out = os.path.join(test, "skins", rname)
    os.makedirs(out, exist_ok=True)
    for f in names:
        w, h, px = load(os.path.join(frames, f))
        FRAME = f
        BOB = frame_bob(px, w, h) if f.startswith("bdy_") else 0
        npx = fn(w, h, px, rname)
        ow, oh = w, h
        if isinstance(npx, tuple):
            ow, oh, npx = npx
        write(os.path.join(out, f), ow, oh, npx)
        if (ow, oh) == (w, h):
            done.setdefault(rname, {})[f] = (w, h, npx)
print("skins:", ", ".join(RECIPES), "frames each:", len(names))

# preview: rows = the donkey + each recipe; columns = static, walk 1-8, ear 3; 3x scale, cropped to the body area
cols = [n for n in ["bdy_static_left.png"] + [f"bdy_walk_left_0{i}.png" for i in range(1, 9)] + ["bdy_ear_left_03.png"] if n in names]
CX0, CY0, CW, CH, S = 18, 14, 60, 62, 3
rows_src = [("donkey", {f: load(os.path.join(frames, f)) for f in cols})] + [(r, done[r]) for r in RECIPES if r in done]
W, H = len(cols) * CW * S, len(rows_src) * CH * S
img = [(58, 74, 44, 255)] * (W * H)  # grass-ish background, like the game
for ri, (rname, fr) in enumerate(rows_src):
    for ci, f in enumerate(cols):
        w, h, px = fr[f]
        for y in range(CH):
            for x in range(CW):
                c = px[(CY0 + y) * w + CX0 + x]
                if c[3] == 0:
                    continue
                for sy in range(S):
                    for sx in range(S):
                        X, Y = ci * CW * S + x * S + sx, ri * CH * S + y * S + sy
                        bg = img[Y * W + X]
                        a = c[3] / 255.0
                        img[Y * W + X] = tuple(int(c[k] * a + bg[k] * (1 - a)) for k in range(3)) + (255,)
write(preview, W, H, img)
print("preview", preview, W, H)
