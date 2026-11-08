"""Mounts drawn from nothing (docs/mount-looks-design.md): a giant worm, a giant snail, a giant rat and a dragon, as
Saddle Up's external mount packs packs/<Pack>/gk2mounts/<mount>/ (mount.cfg; HOMES below) with their default look in
skins/<skin>/ (the one pipeline, docs/sprite-tools.md "Making a skin": a PNG per frame, skin.cfg and mount.cfg kept as
written by hand once they exist). Our own art, made by code: no game pixels.

Every donkey frame name gets a picture (96x96, facing left, the donkey's pivot in the middle), so the creature covers
every clip: the 8 walk frames are one crawl cycle; standing, ear, lips, chew, turn and graze are idle frames (graze puts
the head down). The donkey's head layer gets empty pictures, so "Instead of it" hides the donkey completely.
Fitted to the donkey's measures so the rider sits right without tuning: the back (the seat) at row 41 around columns
47-63, the ground at row 64, the front (where the cart's shafts and the harness are) around column 33.
    python tools/skins/creatures/make_creatures.py                 # every creature into its pack: packs\\<Pack>\\gk2mounts\\<mount id>\\skins\\default\\
    python tools/skins/creatures/make_creatures.py rat             # one creature
    python tools/skins/creatures/make_creatures.py rat <folder>    # the pictures into <folder>\\rat\\ (a preview, never the game's plugins)
Then tools/skins/skin_colors.py when the art's colours changed, and tests/SkinsScan."""
import math, os, struct, sys, zlib
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import review  # the strips, preview and sheet every generator writes the same way (tools/skins/review.py)
from gamedir import game_dir

W = H = 96          # the canvas (set per creature below)
OX = OY = 0         # where the donkey's 96x96 frame sits in the canvas
GROUND = 64
SEAT_X, SEAT_Y = 55, 41


def write(path, px):
    raw = b"".join(b"\x00" + bytes(c for p in px[y * W:(y + 1) * W] for c in p) for y in range(H))
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def outline(px, col):
    """A 1 px outline round everything drawn (outside it), like the game's sprites."""
    out = list(px)
    for y in range(H):
        for x in range(W):
            if px[y * W + x][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and px[ny * W + nx][3]:
                    out[y * W + x] = col + (255,)
                    break
    return out


def catmull(pts, n=200):
    """A smooth line through the points."""
    res = []
    P = [pts[0]] + pts + [pts[-1]]
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(n // (len(P) - 3)):
            t = k / (n // (len(P) - 3))
            t2, t3 = t * t, t * t * t
            res.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(2)))
    res.append(pts[-1])
    return res


# ---------------------------------------------------------------- the worm

def worm(phase, head_down=0.0, idle=0.0):
    """A giant earthworm, facing left: its tail on the ground at the right, a hump under the seat, the front lifted
    with the head up. phase 0..1: one crawl (a wave travelling from head to tail and the hump breathing)."""
    s = math.sin(2 * math.pi * phase)
    c = math.cos(2 * math.pi * phase)
    hump = SEAT_Y + 6 + round(1.2 * s)                         # the tube's centre under the seat (top ~ row 41)
    pts = [(78 + c, GROUND - 4),                               # tail tip on the ground
           (69, GROUND - 6 - 2 * max(0, -s)),
           (SEAT_X + 2, hump),                                  # the hump: the seat
           (44 - c, hump + 3 + s),
           (36, GROUND - 7 + 2 * c),                            # touching down in front of the hump
           (29, GROUND - 14 - 2 * s + head_down * 14),          # the neck rising
           (23 + idle, 40 + 2 * c + head_down * 18)]            # the head
    line = catmull(pts, 240)
    # arc length along the body, for the segment rings
    L = [0.0]
    for i in range(1, len(line)):
        L.append(L[-1] + math.dist(line[i - 1], line[i]))
    total = L[-1]
    px = [(0, 0, 0, 0)] * (W * H)
    best = [99.0] * (W * H)
    for i, (x, y) in enumerate(line):
        u = L[i] / total                                       # 0 at the tail, 1 at the head
        r = 3.0 + 3.6 * math.sin(math.pi * min(1.0, u * 1.05)) # thin tail, fat middle, rounded head
        r = max(2.2, r)
        for yy in range(int(y - r - 1), int(y + r + 2)):
            for xx in range(int(x - r - 1), int(x + r + 2)):
                if 0 <= xx < W and 0 <= yy < H:
                    d = math.hypot(xx + 0.5 - x, yy + 0.5 - y)
                    if d <= r and d / r < best[yy * W + xx]:
                        best[yy * W + xx] = d / r
                        # shading: light from above, segments every 4 px of length, the clitellum band near the front
                        up = (y - (yy + 0.5)) / r                       # +1 at the top edge
                        base = (196, 112, 118)
                        if 0.62 < u < 0.72:
                            base = (222, 150, 140)                      # the saddle band (clitellum)
                        col = mix(base, (250, 196, 190), max(0.0, up) * 0.7)
                        col = mix(col, (110, 52, 62), max(0.0, -up) * 0.8)
                        ring = (L[i] + phase * 8) % 4.0 < 0.9
                        if ring and u < 0.95:
                            col = mix(col, (120, 58, 66), 0.55)
                        px[yy * W + xx] = col + (255,)
    # the head: a dark mouth at the front and two small eyes
    hx, hy = line[-1]
    for dx, dy in ((-3, 1), (-3, 2), (-2, 2)):
        x, y = round(hx + dx), round(hy + dy)
        if 0 <= x < W and 0 <= y < H and px[y * W + x][3]:
            px[y * W + x] = (66, 18, 26, 255)  # 42121A: a colour-cube cell apart from the outline (36141C), so a Mouth colour reaches only the mouth
    for dx, dy in ((-1, -2), (1, -2)):
        x, y = round(hx + dx), round(hy + dy)
        if 0 <= x < W and 0 <= y < H:
            px[y * W + x] = (24, 8, 12, 255)
    return outline(px, (54, 20, 28))


# ---------------------------------------------------------------- the snail

def snail(phase, head_down=0.0, idle=0.0):
    """A giant snail, facing left: a long slimy foot on the ground, a spiral shell as the saddle (its top at the seat),
    two eye stalks. phase 0..1: one crawl (a ripple running back along the foot, the body stretching, the stalks
    swaying)."""
    s = math.sin(2 * math.pi * phase)
    c = math.cos(2 * math.pi * phase)
    px = [(0, 0, 0, 0)] * (W * H)
    # the foot: from the tail (right) to the head (left), lower edge on the ground with a ripple
    x_tail, x_head = 78 + round(c), 22 - round(s * 0.8)
    for x in range(x_head, x_tail + 1):
        u = (x - x_head) / (x_tail - x_head)                   # 0 at the head, 1 at the tail
        top = GROUND - (13 - 9 * u) - (2 if u < 0.15 else 0)   # tall at the front, flat at the tail
        top -= round(head_down * 6 * max(0.0, 0.3 - u) / 0.3)
        top += round(head_down * 8 * max(0.0, 0.25 - u) / 0.25)
        ripple = 1 if math.sin((u * 6 + phase) * 2 * math.pi) > 0.6 else 0
        bottom = GROUND - ripple
        # a rounded head: the first columns curve in from the top and lift off the ground a little at the nose, so
        # the front isn't a straight vertical edge (2026-10-17)
        nose = x - x_head
        if nose < 10:
            k = (10 - nose) / 10.0
            top += round(9 * k ** 1.6)
            bottom -= round(4 * k ** 2)
            top = min(top, bottom - 2)
        for y in range(int(top), bottom + 1):
            v = (y - top) / max(1, bottom - top)               # 0 at the top, 1 at the sole
            col = mix((170, 176, 138), (108, 114, 86), v)
            if v > 0.82:
                col = (86, 88, 70)
            if (x * 7 + y * 3) % 23 == 0:
                col = mix(col, (220, 228, 196), 0.6)          # a glint of slime
            px[y * W + x] = col + (255,)
    # a small dark mouth low on the rounded front
    for dx, dy in ((2, -3), (3, -3), (3, -2)):
        mx_, my_ = x_head + dx, GROUND + dy
        if px[my_ * W + mx_][3]:
            px[my_ * W + mx_] = (58, 52, 40, 255)
    # the eye stalks: two thin stalks from the front of the head, swaying; a dark eye ball at the top of each
    for k, (bx, lean) in enumerate(((x_head + 3, -0.35), (x_head + 6, -0.15))):
        sway = 1.5 * math.sin(2 * math.pi * (phase + k * 0.25)) + idle
        by = GROUND - 14 + round(head_down * 6)
        length = 13 - round(head_down * 5)
        for t in range(length):
            x = round(bx + (lean * t) + sway * t / length)
            y = by - t
            if 0 <= x < W and 0 <= y < H:
                px[y * W + x] = (150, 156, 120, 255)
        ex, ey = round(bx + lean * length + sway), by - length
        for dx, dy in ((0, 0), (1, 0), (0, -1), (1, -1)):
            if 0 <= ex + dx < W and 0 <= ey + dy < H:
                px[(ey + dy) * W + ex + dx] = (30, 26, 22, 255)
        if 0 <= ex + 1 < W and 0 <= ey - 1 < H:
            px[(ey - 1) * W + ex + 1] = (230, 230, 210, 255)
    # the shell: a spiral, its top at the seat, bobbing a pixel with the crawl
    cx, cy = SEAT_X + 1, SEAT_Y + 11 + round(0.6 * s)
    R = 11.5
    for y in range(int(cy - R - 1), int(cy + R + 2)):
        for x in range(int(cx - R * 1.15 - 1), int(cx + R * 1.15 + 2)):
            dx, dy = (x + 0.5 - cx) / 1.15, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > R or y > GROUND - 4:
                continue
            a = math.atan2(dy, dx)
            spiral = (d - 1.7 * a) % 4.6                         # the spiral's line
            col = mix((214, 140, 62), (120, 64, 30), d / R)
            col = mix(col, (240, 196, 120), max(0.0, -dy / R) * 0.5)
            if spiral < 1.1:
                col = (82, 40, 22)
            px[y * W + x] = col + (255,)
    # the saddle: a leather pad on the shell's top (the keeper sat "into" the shell without one: capture 2026-10-17),
    # a raised back (cantle), and a strap down the shell's side
    top_y = round(cy - R) + 1
    for x in range(cx - 7, cx + 8):
        for y in range(top_y - 1, top_y + 3):
            edge = y == top_y - 1 or x in (cx - 7, cx + 7)
            px[y * W + x] = ((70, 38, 24) if edge else (128, 72, 40)) + (255,)
    for y in range(top_y - 3, top_y):                           # the cantle at the back (right: the snail faces left)
        for x in (cx + 5, cx + 6, cx + 7):
            px[y * W + x] = ((70, 38, 24) if x == cx + 7 or y == top_y - 3 else (150, 88, 50)) + (255,)
    for x in range(cx - 6, cx + 6):                              # a lighter stitch line
        if x % 2 == 0:
            px[(top_y + 1) * W + x] = (176, 120, 72, 255)
    for y in range(top_y + 3, round(cy + R * 0.5)):              # the girth strap down the side, near the back
        px[y * W + cx + 5] = (60, 32, 20, 255)
        px[y * W + cx + 6] = (96, 54, 30, 255)
    return outline(px, (36, 30, 22))


# ---------------------------------------------------------------- the rat

# Fur and skin palettes, dark to light (pixel-art ramps, a little warm in the lights, cool in the shadows)
# Style cues from reference sheets the user shared (2026-10-17; our own drawing, no pixels taken): a low, hunched rat,
# the back arching to a peak at the hips, the head held low with the snout down and forward, small ears set back, a
# highlight along the spine and a shadowed belly, red eyes, yellow incisors, a thin dark-mauve tail trailing low.
FUR = [(48, 40, 48), (70, 60, 66), (96, 84, 88), (124, 112, 112), (154, 142, 136), (186, 174, 164)]
BELLY = [(52, 42, 50), (70, 60, 66), (90, 78, 82)]
SKIN = [(96, 50, 62), (132, 74, 86), (166, 104, 112), (196, 140, 142)]
EYE = [(70, 10, 16), (178, 26, 30), (236, 92, 70)]
TOOTH = (232, 206, 120)
DITHER = ((0.0, 0.5), (0.75, 0.25))  # 2x2 ordered dither thresholds


def _ell(x, y, cx, cy, rx, ry):
    """Signed distance (about, in pixels) to an ellipse: negative inside."""
    return (math.hypot((x - cx) / rx, (y - cy) / ry) - 1.0) * min(rx, ry)


def _smin(a, b, k):
    """A smooth union of two distances (round blends where shapes meet)."""
    h = max(k - abs(a - b), 0.0) / k
    return min(a, b) - h * h * k * 0.25


def _shade(ramp, light, x, y):
    """light 0..1 to a ramp colour, dithered between neighbouring tones."""
    v = max(0.0, min(0.999, light)) * (len(ramp) - 1)
    i = int(v)
    frac = v - i
    # dither only right at the boundary between two tones (a full ordered dither read as a checkerboard)
    if frac > 0.62 or (0.38 < frac <= 0.62 and DITHER[y % 2][x % 2] < 0.5):
        i = min(i + 1, len(ramp) - 1)
    return ramp[i]


def rat(phase, head_down=0.0, idle=0.0):
    """A giant rat, facing left, in pixel art: a round rump with the back on the seat line, a narrower chest, a long
    head with a pointed snout; shaded from the upper left in a six-tone fur ramp with ordered dithering, a pale belly,
    a translucent pink ear, an eye with a glint, whiskers; thin front legs with small paws, hind legs under a haunch;
    a long tail tapering to a point, with ring segments. Walk: diagonal pairs (near front with far hind), feet lifting
    on the forward swing, a small body bob and tail sway."""
    walking = phase != 0.0
    s_ = math.sin(2 * math.pi * phase)
    bob = 0.8 * math.sin(4 * math.pi * phase)
    hd = head_down
    px = [(0, 0, 0, 0)] * (W * H)
    L = (-0.55, -0.83)  # light from the upper left (screen y grows down)

    # ---- the legs (far ones first, behind the body)
    def gait(off):
        a = math.sin(2 * math.pi * (phase + off)) if walking else 0.0
        lift = max(0.0, math.cos(2 * math.pi * (phase + off))) * 2.0 if walking else 0.0
        return a, lift

    def leg(hip, knee, foot, ramp, paw, width):
        pts = [hip, knee, foot]
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            n = int(max(abs(bx - ax), abs(by - ay)) * 2) + 1
            for t in range(n + 1):
                x, y = ax + (bx - ax) * t / n, ay + (by - ay) * t / n
                for ox in range(-(width // 2), width - width // 2):
                    xx, yy = int(round(x)) + ox, int(round(y))
                    if 0 <= xx < W and 0 <= yy < H:
                        px[yy * W + xx] = ramp[1 if ox <= 0 else 2] + (255,)
        fx, fy = int(round(foot[0])), int(round(foot[1]))
        for ox in range(-3, 1):  # the paw points forward (left)
            if 0 <= fx + ox < W and 0 <= fy < H:
                px[fy * W + fx + ox] = (paw[2] if ox > -3 else paw[1]) + (255,)
        if 0 <= fx - 1 < W and 0 <= fy - 1 < H:
            px[(fy - 1) * W + fx - 1] = paw[1] + (255,)

    def legs(far):
        ramp = FUR[0:3] if far else FUR[1:4]
        paw = (SKIN[0], SKIN[0], SKIN[1]) if far else (SKIN[1], SKIN[1], SKIN[2])
        dx = 2 if far else 0
        # front: thin, straight, from under the chest
        a, lift = gait(0.5 if far else 0.0)
        hx, hy = 40 + dx, 57 + bob
        fx, fy = 39 + dx - 3.5 * a, GROUND - lift
        leg((hx, hy), ((hx + fx) / 2 + 0.5, (hy + fy) / 2), (fx, fy), ramp, paw, 2)
        # hind: from the haunch, the knee forward, the heel back, a long foot
        a, lift = gait(0.0 if far else 0.5)
        hx, hy = 63 + dx, 57 + bob
        fx, fy = 62 + dx - 4.0 * a, GROUND - lift
        leg((hx, hy), (hx - 3, (hy + fy) / 2 - 1), (fx + 1, fy), ramp, paw, 3)

    legs(far=True)

    # ---- the tail: from under the rump, a tapering curve with ring segments, swaying
    tail = []
    for t in range(60):
        u = t / 59
        x = 72 + 22 * u
        y = 56 + bob + 6.5 * math.sin(u * 1.9) - 1.5 * u + (1.6 * math.sin(2 * math.pi * (phase + 0.6 * u)) * u if walking else idle * u)
        tail.append((x, y, 1.5 * (1 - u) + 0.5))
    for i, (x, y, r) in enumerate(tail):
        for yy in range(int(y - r - 1), int(y + r + 2)):
            for xx in range(int(x - r - 1), int(x + r + 2)):
                if 0 <= xx < W and 0 <= yy < H and math.hypot(xx + 0.5 - x, yy + 0.5 - y) <= r:
                    up = (y - (yy + 0.5)) / max(r, 0.6)
                    col = SKIN[2] if up > 0.3 else SKIN[1] if up > -0.4 else SKIN[0]
                    if i % 6 == 0:
                        col = SKIN[0]
                    px[yy * W + xx] = col + (255,)

    # ---- the body and head: one smooth shape, shaded from its gradient
    hy0 = 53 + bob + hd * 6  # the head's centre: held low, lower still when grazing
    snout = 14.5 - (0.6 if idle > 0.3 else 0.0) + 0.5 * s_

    def sdf(x, y):
        rump = _ell(x, y, 60, 50 + bob, 13.5, 10.5)            # the hips: top at ~39.5, the seat
        back = _ell(x, y, 50, 49.5 + bob, 10.0, 8.0)            # the arch down to the shoulders
        chest = _ell(x, y, 40, 53.5 + bob, 9.5, 6.8)            # shoulders lower than the hips
        haunch = _ell(x, y, 64, 54.5 + bob, 7.5, 6.5)
        head = _ell(x, y, 28, hy0, 7.5, 5.2)
        muzzle = _ell(x, y, (snout + 26) / 2, hy0 + 2.2, (26 - snout) / 2, 2.8)
        d = _smin(rump, back, 6.0)
        d = _smin(d, chest, 6.0)
        d = _smin(d, haunch, 3.0)
        d = _smin(d, head, 4.5 if hd < 0.5 else 3.0)
        d = _smin(d, muzzle, 3.0)
        return d

    for y in range(20, H):
        for x in range(8, 84):
            d = sdf(x + 0.5, y + 0.5)
            if d > 0:
                continue
            gx = sdf(x + 1.0, y + 0.5) - sdf(x, y + 0.5)
            gy = sdf(x + 0.5, y + 1.0) - sdf(x + 0.5, y)
            g = math.hypot(gx, gy) or 1.0
            nx, ny = gx / g, gy / g
            rim = min(1.0, -d / 4.0)                             # 0 at the edge, 1 inside: rounder light
            # (nx, ny) points out of the shape; L points towards the light (upper left): facing it is bright
            light = 0.44 + 0.34 * (nx * L[0] + ny * L[1]) * (1 - 0.35 * rim)
            if ny < -0.75 and -d < 2.5:
                light += 0.14                                   # the highlight along the spine
            belly = ny > 0.45 and 32 < x < 72
            if belly:
                col = _shade(BELLY, light, x, y)
            else:
                col = _shade(FUR, light, x, y)
                # sparse short fur strokes, slanting back along the body (2 px each)
                k = (x + 2 * y) % 17
                if (k == 0 or k == 1) and ((x * 7 + y * 3) % 5 == 0 or k == 1) and -d > 1.5 and ny < 0.3 and (y * 5 + x) % 3 == 0:
                    col = FUR[max(0, FUR.index(col) - 1)]
            px[y * W + x] = col + (255,)

    # ---- the ear: a round ear behind the head, a translucent pink inside
    ex, ey = 33 + idle * 0.8, hy0 - 5.0
    for y in range(int(ey - 4), int(ey + 4)):
        for x in range(int(ex - 4), int(ex + 4)):
            if 0 <= x < W and 0 <= y < H:
                d = math.hypot((x + 0.5 - ex) / 2.8, (y + 0.5 - ey) / 3.1)
                if d <= 1.0:
                    inner = math.hypot((x + 0.5 - ex + 0.5) / 1.6, (y + 0.5 - ey) / 2.0) <= 1.0
                    px[y * W + x] = (SKIN[1] if inner else FUR[3] if y < ey else FUR[2]) + (255,)

    # ---- the eye, the nose, the whiskers
    exy = (int(round(23.0)), int(round(hy0 - 1.0)))
    for dx, dy, c in ((-1, 0, FUR[0]), (0, -1, FUR[0]), (1, -1, FUR[0]), (2, 0, FUR[0]), (0, 0, EYE[1]), (1, 0, EYE[1]),
                      (0, 1, EYE[0]), (1, 1, EYE[0])):
        px[(exy[1] + dy) * W + exy[0] + dx] = c + (255,)
    px[exy[1] * W + exy[0]] = EYE[2] + (255,)                      # the bright point
    nx_, ny_ = int(round(snout)), int(round(hy0 + 1.8))
    for dx, dy in ((2, 2), (3, 2), (2, 3)):                         # the incisors
        if 0 <= nx_ + dx < W and 0 <= ny_ + dy < H:
            px[(ny_ + dy) * W + nx_ + dx] = TOOTH + (255,)
    for dx, dy, c in ((0, 0, SKIN[1]), (1, 0, SKIN[2]), (0, 1, SKIN[0]), (1, 1, SKIN[1])):
        if 0 <= nx_ + dx < W:
            px[(ny_ + dy) * W + nx_ + dx] = c + (255,)
    for ey_, slope in ((-1, -0.25), (1, 0.15), (2, 0.4)):
        for t in range(2, 8):
            x, y = nx_ + 4 - t, ny_ + ey_ + round(slope * t)
            if 0 <= x < W and 0 <= y < H and px[y * W + x][3] == 0 and t % 3 != 0:
                px[y * W + x] = (74, 66, 72, 255)

    legs(far=False)

    # ---- a selective outline: dark, a little warmer where lit from above
    out = list(px)
    for y in range(H):
        for x in range(W):
            if px[y * W + x][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx2, ny2 = x + dx, y + dy
                if 0 <= nx2 < W and 0 <= ny2 < H and px[ny2 * W + nx2][3]:
                    out[y * W + x] = ((52, 40, 44) if dy == 1 else (34, 26, 32)) + (255,)
                    break
    return out


# ---------------------------------------------------------------- the dragon

# A dragon in pixel art, on a 128 canvas (the donkey's 96 frame sits 16 px in; coordinates below are the donkey's).
# Style cues from reference sheets the user shared (2026-10-17; our own drawing, no pixels taken): a deep chest and
# big haunches on clawed legs, the back on the seat line, a thick neck arching up to a big-jawed horned head, a thick
# tail ending in a spade, bat wings raised over the back (the leading edge along the top, the membrane hanging behind
# the rider with the scallops on the trailing edge), a ridge of spines, scaled belly plates. Second pass 2026-10-17
# after the first read as "derpy" at the players' scale: a tube body, stick legs, a small head, a spiky sail.
# a palette: the body ramp, the wing membrane ramp, the belly plates, the horns and claws, the eye, the outline (below, lit)
RED_DRAGON = ([(40, 8, 16), (84, 16, 26), (128, 26, 36), (172, 40, 46), (208, 70, 62), (234, 116, 92)],
              [(38, 8, 40), (74, 18, 66), (112, 34, 96), (148, 58, 124)],      # the wing membrane: wine, apart from the body's red
              [(112, 62, 26), (168, 106, 40), (218, 160, 66), (244, 206, 118)],
              [(82, 64, 46), (146, 128, 98), (212, 198, 168)],
              (250, 214, 64), ((46, 12, 20), (28, 6, 12)))
BLUE_DRAGON = ([(14, 18, 52), (26, 38, 98), (40, 66, 148), (62, 102, 196), (104, 150, 228), (164, 202, 246)],
               [(12, 14, 46), (24, 32, 84), (40, 56, 126), (62, 84, 160)],
               [(118, 70, 24), (180, 118, 38), (228, 176, 60), (248, 220, 120)],
               [(84, 72, 56), (150, 138, 112), (216, 206, 180)],
               (250, 168, 52), ((14, 18, 52), (8, 10, 34)))
# The Armored Dragon: a study (2026-11-07).
# The user wanted it "radically different than the default skin", with new horns and "definitely also the wings".
# Its look (docs/dragon-references.md section 9):
# - dark indigo scales under steel plate, the Armored donkey's steel and brass;
# - leather wings with a steel leading edge, a spike on the wrist and hooked claws at the finger tips;
# - big black horns curling back over a helm;
# - blades along the ridge, a banded tail ending in a spiked mace, and greaves on the legs.
# Our own drawing: the references' war dragon gives the type only. dragon(style="armored") draws it; the Red Dragon is
# unchanged.
ARMORED_DRAGON = ([(14, 12, 30), (24, 22, 52), (36, 34, 78), (52, 50, 108), (72, 72, 138), (100, 102, 168)],   # dark hide, so the steel stands off it
                  [(58, 36, 26), (92, 62, 40), (128, 92, 58), (166, 126, 82)],      # leather, apart from the steel and the scales
                  [(70, 48, 28), (110, 78, 42), (150, 110, 58), (190, 150, 84)],
                  [(22, 18, 24), (48, 42, 50), (90, 84, 94)],                       # black horn: the horns and the claws
                  (240, 96, 48), ((14, 10, 22), (8, 6, 14)))
# The steel: the game's own knight armour (the keeper's 9019_ helmet in the MainCharacter atlas, its colours by count,
# 2026-11-07; the user: "make it match the game's art style"): a cool green-teal steel from its dark teal line up to a
# mint glint. Its gold is the BRASS below, the same four colours (the Armored donkey's brass is the game's gold).
STEEL = [(30, 55, 55), (50, 68, 63), (86, 91, 78), (106, 112, 98), (122, 135, 120), (154, 181, 166), (193, 230, 217)]   # 1E3737 32443F 565B4E 6A7062 7A8778 9AB5A6 C1E6D9
STEEL_LINE = (25, 44, 45)                                                            # its line, 192C2D
BRASS = [(125, 83, 8), (164, 128, 11), (207, 169, 15), (241, 210, 116)]
CLOTH = [(56, 18, 26), (94, 28, 36), (138, 40, 42), (178, 58, 50)]                 # its skirt and plume: 38121A 5E1C24 8A282A B23A32


def _in_poly(x, y, poly):
    """even-odd: is (x, y) inside the polygon"""
    hit = False
    for i in range(len(poly)):
        (ax, ay), (bx, by) = poly[i], poly[(i + 1) % len(poly)]
        if (ay > y) != (by > y) and x < ax + (y - ay) * (bx - ax) / (by - ay):
            hit = not hit
    return hit


def _steel(light):
    """light 0..1 to a steel tone: metal has hard bands, a bright top and a dark underside."""
    for lim, k in ((0.25, 2), (0.4, 3), (0.55, 4), (0.7, 5)):
        if light < lim:
            return STEEL[k]
    return STEEL[6]


def _seg(x, y, ax, ay, bx, by):
    """Distance from a point to a segment."""
    vx, vy = bx - ax, by - ay
    L2 = vx * vx + vy * vy or 1e-6
    t = max(0.0, min(1.0, ((x - ax) * vx + (y - ay) * vy) / L2))
    return math.hypot(x - ax - vx * t, y - ay - vy * t)


def _tri(px_, py_, a, b, c):
    def cross(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    d1, d2, d3 = cross(a, b, (px_, py_)), cross(b, c, (px_, py_)), cross(c, a, (px_, py_))
    return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))


def _rot(p, o, ang):
    s_, c_ = math.sin(ang), math.cos(ang)
    dx, dy = p[0] - o[0], p[1] - o[1]
    return (o[0] + dx * c_ - dy * s_, o[1] + dx * s_ + dy * c_)


def _tone(ramp, light):
    """light 0..1 to a ramp colour, no dithering: clean bands read better at the players' scale."""
    return ramp[max(0, min(len(ramp) - 1, int(light * len(ramp))))]


SPAN = 1.35  # the spread wings' size against the raised wing the dragon first had
# The Armored Dragon's head and wing shapes, studies for the user to pick from (2026-11-07): the head beak | greathelm |
# wedge, the wing bat | pointed | saw (docs/dragon-references.md section 10). The defaults are the shipped skin's.
ARM_HEAD = os.environ.get("DRAGON_ARM_HEAD", "greathelm")   # the shipped skin's (the user's pick, 2026-11-07); the others are studies
ARM_WING = os.environ.get("DRAGON_ARM_WING", "saw-blade")
# the wings' surface and holes (2026-11-07, studies, then the user's pick: blackened with see-through eyelets): leather |
# steel | blackened, and tears (cut in the leather) | eyelets | punched (pierced metal: a hole in a bevelled steel ring)
ARM_WINGMAT = os.environ.get("DRAGON_ARM_WINGMAT", "blackened")   # the shipped skin's (the user's pick, 2026-11-07)
ARM_HOLES = os.environ.get("DRAGON_ARM_HOLES", "eyelets")
ARM_SEE = os.environ.get("DRAGON_ARM_SEE", "1") == "1"   # the holes see-through: the wing left out there, a steel rim round
RAISE = int(os.environ.get("DRAGON_RAISE", "6"))  # the body 6 px higher on longer legs (the user chose it from the preview page, 2026-10-23); the seat row (68 -> 62) moves with it
NO_SPREAD_WINGS = os.environ.get("DRAGON_STUDY") == "nowings"  # a study: the flying frames without the spread wings, to measure the body alone
# The spread near wing over the rider: "all", the whole wing in front of him (docs/front-layer.md section 3's rule; B of
# the in-game study, chosen by the user 2026-10-23: "the best looking one is b: the rule"). Studies kept as switches:
# "head" in front of him except his head (A, the cut), "box" behind him in his box.
WING_OVER_RIDER = os.environ.get("DRAGON_WING_OVER_RIDER", "all")


def rider_in_view(xc, yc):
    """Where the rider stays in front of the dragon's near wing, in canvas pixels (the 128 canvas, the seat at row 62):
    his head only, the hair (rows 26-37 across x 64-82) and the face and beard below it (rows 38-42, x 64-75), read off
    the rider at 12x (2026-10-23), a pixel more round. The near wing is nearer the camera than he is, so it covers him
    wherever it crosses him, his back, shoulders and arms too, as the folded wing does on the ground; only his face
    stays in view. A first window, everything the fold leaves in view on the ground (a band down to row 37 across his
    whole width and his reins arm), left his shoulders and upper back in front of a wing sweeping across at chin height:
    a straight cut across his body (the user's video, 2026-10-23: "frames which the occlusion is missing")."""
    if yc <= 37:                     # up to the picture's top: no rider above row 27, and no stray edge over his hair
        return 64 <= xc <= 82
    return 38 <= yc <= 42 and 64 <= xc <= 75


# The walk's legs and weight (the user, 2026-10-24: "his legs ... too dinky", "walks like a lady", "a camp walk"; the
# second reference's Walking studied, docs/flying.md "The walk, studied"). The defaults are the walk chosen from the
# previews (2026-10-24: preview 4 with the old tail and a calmer body); the tail's wave (TAIL_WAVE) was judged frantic.
LEG_BULK = float(os.environ.get("DRAGON_LEG_BULK", "1.2"))        # every leg bone's thickness times this
LEG_WRIST = float(os.environ.get("DRAGON_LEG_WRIST", "0.7"))      # 0..1: a bone's lower end that much of the way to its upper end's thickness (no thin ankle)
PAW_HEAVY = os.environ.get("DRAGON_PAW", "heavy") == "heavy"         # a broad flat paw on the ground, lifted flat (not flicked claws-down)
STEP_LIFT = float(os.environ.get("DRAGON_STEP_LIFT", "2.5"))      # how high a swinging foot rises, px
STEP_STANCE = float(os.environ.get("DRAGON_STEP_STANCE", "0.72"))  # the share of the cycle a foot is planted
STEP_STRIDE = float(os.environ.get("DRAGON_STEP_STRIDE", "18.4"))  # how far a planted foot slides back, px
FAR_DX = float(os.environ.get("DRAGON_FAR_DX", "5"))            # the far legs this far back ...
FAR_DY = float(os.environ.get("DRAGON_FAR_DY", "-2"))           # ... and up from the near ones (the feet not on one line)
FAR_DARK = os.environ.get("DRAGON_FAR_DARK", "1") == "1"         # the far legs one tone darker again
HIND_LEN = float(os.environ.get("DRAGON_HIND_LEN", "0.82"))        # the hind leg's thigh and shin this much of their length (shorter: less of a deep fold, a column)
HOCK_UP = float(os.environ.get("DRAGON_HOCK_UP", "-1"))          # walking: the hock this many px higher (a longer foot, the knee off the ground)
FORE_LEN = float(os.environ.get("DRAGON_FORE_LEN", "0.9"))        # the foreleg's two bones this much of their length
WALK_NOD = float(os.environ.get("DRAGON_WALK_NOD", "0"))        # walking: the head dips this many px with each step (twice a cycle)
HEAD_STEADY = float(os.environ.get("DRAGON_HEAD_STEADY", "1"))  # walking: 0..1 of the body's bob taken off the head (1: the head holds its height, the neck's base still bobs)
HEAD_FOLLOW = float(os.environ.get("DRAGON_HEAD_FOLLOW", "1.2"))  # walking: the head rides this share of the body's bob again, HEAD_LAG of a cycle later (a heavy head that follows)
HEAD_LAG = float(os.environ.get("DRAGON_HEAD_LAG", "0.15"))
HEAD_FWD = float(os.environ.get("DRAGON_HEAD_FWD", "1"))        # walking: the head reaches forward and back this many px with the steps, HEAD_LAG later
TAIL_WAVE = float(os.environ.get("DRAGON_TAIL_WAVE", "0"))      # walking: the tail lifts in a wave running down it, the tip this many px (0: the old stiff sway)
TAIL_LAG = float(os.environ.get("DRAGON_TAIL_LAG", "0.07"))     # walking: each of the tail's four points this much of a cycle after the one before
TAIL_IN = float(os.environ.get("DRAGON_TAIL_IN", "0"))          # walking: the tip swings in towards the body this many px (the reference's tail swinging to the side)
WING_ROCK = float(os.environ.get("DRAGON_WING_ROCK", "0.7"))      # walking: the folded wings take this share of the body's rock (front with the shoulders, back with the haunch)
HIPS_ROCK = float(os.environ.get("DRAGON_HIPS_ROCK", "1"))      # walking: the hips' top line takes this share of the haunch's heave (the back's line rocks, not only what is under it)
NECK_WAVE = os.environ.get("DRAGON_NECK_WAVE", "1") == "1"       # walking: the head's motion runs up the neck as a wave (each point its share of HEAD_LAG), not the whole neck at once
FORE_HEAVY = os.environ.get("DRAGON_FORE_HEAVY", "1") == "1"     # the forelegs built like the hind: a shoulder mass over a thick upper arm, a thick forearm, a short wrist to the paw
WALK_HEAVE = float(os.environ.get("DRAGON_WALK_HEAVE", "1"))    # walking: shoulders and haunch rise over the planted legs, in turn (the seat holds)
LEG_SWAY = float(os.environ.get("DRAGON_LEG_SWAY", "1.5"))  # flying: each leg's own small loop, px (2026-10-23; the preview page shows 1 and 2 next to it)
# The glide (docs/flying.md "the glide"): the wings held open, no beat. GLIDE_T is the held wing pose (0 raised .. 0.5
# level back .. 1 swept down). Held still the wings read dead ("it's just making the wings look very static", the user
# 2026-10-23), so they flex in the air: each joint's pose sways GLIDE_FLEX_PX (in pixels) about GLIDE_T, slowly, the arm first and each
# finger later (GLIDE_WING_LAGS), the wing bending like a sail under the air, its tips moving 4-5 px. (A tip flexing 1 px
# alone re-drew every vein at a new angle with no visible move: speckle. A sway the eye follows is a move.) The tail
# waves and each leg keeps its loop, smaller. GLIDE_FRAMES one slow loop (Flight.GlideLoopS plays it).
# The way in and out (the user: "the transition from flying to gliding and vice versa is more natural for the wings"):
# in-between frames whose every part is its beat pose blended into its glide pose, the arm ahead and the finger tips
# behind (GLIDE_MIX_LAGS), so the wing slows and opens out of an upstroke going in, and pushes off with a downstroke
# coming out (docs/flying.md "the glide").
# 0.9 (the user's pick on the preview page, 2026-10-23, of 0.4, 0.55, 0.8, 0.85 and 0.9): the wings held low and spread
# back, the near one hanging in front of the flank, over the rider's hips and legs (44 % of his lower body, 4 % of his
# torso) rather than across his chest (0.4: 60 % of his torso), so the wing doesn't cut through him at the waist
GLIDE_T = float(os.environ.get("DRAGON_GLIDE_T", "0.9"))
# Climbing and diving (the user, 2026-10-23: "when going down we can tilt his head a bit lower and when going upwards we
# can maybe tilt his back a bit upwards"): the whole dragon pitched about the rider's seat, column by column. Each column
# of the picture moves up or down as one, by a whole number of pixels, PITCH_SHEAR px per px from the seat's column at
# full pitch (forward of it up when climbing, behind it down; a dive the other way), so the pixel art stays clean (a
# rotated sprite's pixels go jagged: slanted sprites were tried for slopes and dropped, 2026-10-04) and the rider's seat
# stays where it is. Two strengths each way (bdy_flyup1/2, bdy_flydown1/2: half and full pitch, the beat's eight
# moments each) so the game eases into a climb or a dive (Flight.FrameFor).
# What the reference shows ("Dragon - Fully Animated", 2026-10-23: rearing in Attack 2 frames 61-106 and Hurt 19-37,
# lunging down in Attack 2 136-151): the body itself tilts only a little; the NECK carries the motion (arching up and
# lifting the head high above the back when rearing; reaching forward and down, the head near the ground, lunging) and
# the TAIL answers with a curve (trailing low when rearing; rising, its tip curling up most, lunging). A rigid tilt
# alone (the first study, 0.11) moved the head only 4-5 px and never bent the neck. So: a small tilt of the whole
# (PITCH_SHEAR), the neck bent by PITCH_NECK px at the head (each point a share, the base least, an arch back up / a
# reach forward down), the tail by PITCH_TAIL px at its tip (the root least).
PITCH_SHEAR = float(os.environ.get("DRAGON_PITCH_SHEAR", "0.05"))
PITCH_NECK = float(os.environ.get("DRAGON_PITCH_NECK", "6"))
PITCH_TAIL = float(os.environ.get("DRAGON_PITCH_TAIL", "4"))
PITCH_X = 48.0  # the seat's column, donkey coordinates (the rider is drawn at the picture's middle)
# The flying neck 20 % shorter than first drawn, from its base at the chest (the user, 2026-10-23: "the current flying
# idle neck seems a bit dance like an egyptian ... i think its a bit too long with its body proportions": 25 px from
# the chest to the head). The ground's arched neck, judged good, stays as it is.
FLY_NECK = float(os.environ.get("DRAGON_FLY_NECK", "0.8"))
# Gliding, the head nods slowly (the user, 2026-10-23: "the head is completely 100% static ... we cant have it fully
# static"): GLIDE_HEAD_PX at the head, less down the neck (none at its base), a little after the wing's flex, the way a
# head rides the air. Whole pixels on purpose: a 0.3 px sway only re-rounded the neck's shading into speckle (the first
# glide), so the neck and head had been held still.
GLIDE_HEAD_PX = float(os.environ.get("DRAGON_GLIDE_HEAD_PX", "1.5"))
# the flex in pixels at the wing's farthest-moving joint, whatever the held pose: the poses change at very different
# speeds along the beat (a tip moves 84 px per unit of pose at 0.4, 163 at 0.85), so a flex given as pose (0.06) swung
# the tips 5 px at 0.4 and almost 10 at 0.85, a flap (the user, 2026-10-23: "the glide in preview is still flapping")
GLIDE_FLEX_PX = float(os.environ.get("DRAGON_GLIDE_FLEX_PX", "3"))
GLIDE_FRAMES = 8
GLIDE_WING_LAGS = {"elbow": 0.0, "wrist": 0.05, "root": 0.08, "t1": 0.12, "t2": 0.17, "t3": 0.22}
GLIDE_MIX_LAGS = {"elbow": 0.0, "wrist": 0.04, "root": 0.06, "t1": 0.10, "t2": 0.15, "t3": 0.20}
GLIDE_IN = 4    # bdy_glidein_left_01..04: the beat at th 0.75, 0.875, 0, 0.125 blended into the glide 0.2 .. 0.8
GLIDE_OUT = 4   # bdy_glideout<q>_left_01..04, q 1..4 from the glide's quarters: the beat at th 0.25 .. 0.625, 0.8 .. 0.2 glide


# ---- the wing beat in time (docs/flying.md "the beat in time"). Every part of the flying dragon once followed the
# wings' pose at the same moment: the neck, the head, the hump, the legs and the whole tail peaked on the same frame,
# and the dragon "danced" (the user, 2026-10-23). The second reference (a 60 fps rig, idle and walk) moves part after
# part: the back leads, the neck and head come about half a loop later, the tail's middle and its tip later still,
# each by its own amount. So a frame is a moment of the beat, th 0..1, and each part reads the beat a little in the
# past (its lag) and by its own amount: the wing's arm leads and its fingers trail, the body rises after the stroke
# (the game's bob, Flight.LiftFor), the head rides steadier than the body, the legs dangle, the tail is a wave
# running from its root to its tip.
BEAT_FRAMES = 8
BEAT_BOB_PX = 3.0  # Flight.BeatBobPx: the whole picture rises this much, highest at th 0.5


def wing_w(th):
    """The wings' stroke at th: 0 raised .. 1 swept down. The downstroke (the power) takes 40 % of the beat, the
    upstroke the rest, each eased at its ends; the wings are lowest at th 0.4, the body highest after it (0.5)."""
    th %= 1.0
    if th < 0.4:
        return 0.5 - 0.5 * math.cos(math.pi * th / 0.4)
    return 0.5 + 0.5 * math.cos(math.pi * (th - 0.4) / 0.6)


def body_rise(th):
    """How high the body is in the world at th, 0..1 (the game's bob: 0.5 (1 - cos 2 pi th), highest at 0.5)."""
    return 0.5 * (1.0 - math.cos(2.0 * math.pi * (th % 1.0)))


def dragon(phase, head_down=0.0, idle=0.0, pal=RED_DRAGON, fly=None, legs_mode="walk", lift=0.0, beat=None, glide=None, mix=None, pitch=0.0, style=None):
    # fly: the spread wings' angle about the shoulder (degrees: 0 raised high over the back, 45 level back, 90 swept
    # down past the belly), None = folded (walking, standing). legs_mode: "walk" on the ground, "tuck" (flying: the
    # forelegs bent up under the chest, the hind legs trailing), "reach" (landing: both stretched down for the ground),
    # "push" (taking off: the hind legs straight down off the ground, the forelegs tucking). lift: the whole body this
    # many px up (a leap, the body rising on a downstroke); the seat moves with it (the rider sits on the back).
    # beat: the moment of the wing beat, 0..1 (the flying frames): each part then reads the beat at its own lag.
    # glide: the moment of the glide's slow loop, 0..1 (fly = the held pose): the beat's body held still at that pose.
    # beat and glide both, with mix = (m, +1 into the glide | -1 out of it): every part m of the way from its beat pose
    # to its glide pose (the wing's joints each at their own share, the arm ahead).
    DRG, DWING, DBELLY, DHORN, DEYE, DOUT = pal
    if style == "armored" and ARM_WINGMAT == "steel":
        DWING = (STEEL[1], STEEL[2], STEEL[3], STEEL[4])                        # metal wings: the armour's mid steels
    elif style == "armored" and ARM_WINGMAT == "blackened":
        DWING = (STEEL[0], STEEL[1], STEEL[2], STEEL[3])                        # its darkest: blackened plates
    ARM = style == "armored"   # the Armored Dragon's parts (ARMORED_DRAGON above); every other dragon draws as before
    # a half up, always (the Armored Dragon): round() takes a half to the even number, so a detail half a px off its
    # part moved with the part's row's parity (the helm's breaths jumped a row as the head moved one)
    rnd = (lambda v: math.floor(v + 0.5)) if ARM else round
    walking = phase != 0.0 and fly is None
    tb = fly if (fly is not None and legs_mode == "tuck") else None   # the beat drives the body (flying only)
    gb = 0.0 if tb is None else tb                                    # 0 wings raised .. 1 swept down

    # how much of the glide's pose a frame takes: 0 the beat, 1 the glide, between them a way in or out
    if beat is not None and glide is not None:
        assert mix is not None, "a frame between the beat and the glide needs mix=(m, +1 into the glide | -1 out of it)"
        M, MDIR = mix
    else:
        M, MDIR = (1.0, 1) if glide is not None else (0.0, 1)

    def mshare(lag):
        # a part's own share of the glide in a way in or out: the arm (lag 0) ahead, the finger tips (lag 0.2) behind,
        # eased at both ends; going in the arm reaches the glide first, coming out it reaches the beat first
        if M in (0.0, 1.0):
            return M
        v = (M - lag) / 0.8 if MDIR > 0 else 1.0 - ((1.0 - M) - lag) / 0.8
        v = max(0.0, min(1.0, v))
        return v * v * (3 - 2 * v)

    def blend(bv, gv, m):
        return gv if bv is None else bv if gv is None else bv + (gv - bv) * m

    def gw(lag):
        # the stroke as a part feels it, lag of a beat later (without a beat: the pose given); gliding the body holds
        # the glide's pose (its own flex would only re-round the chest's and the hump's shading)
        if beat is None and glide is None:
            return gb
        return blend(wing_w(beat - lag) if beat is not None else None, GLIDE_T if glide is not None else None, M)

    def follow(lag, amp):
        # a loosely hung part, in the picture (y down, px): the body rises BEAT_BOB_PX * body_rise; the part rises
        # amp of that, lag of a beat later, so in the picture it sinks as the body rises and catches up after.
        # Gliding the body does not rise with a beat: the part rests where it hangs on average in the beat (so the two
        # meet without a jump) and only what hangs looser than the body sways, slowly, a wave running down it: the
        # tail, its tip 1.5 px; the neck and head hold still (their 0.3 px only re-rounded the neck's shading)
        if beat is None and glide is None:
            return 0.0
        bv = BEAT_BOB_PX * (body_rise(beat) - amp * body_rise(beat - lag)) if beat is not None else None
        gv = BEAT_BOB_PX * 0.5 * (1.0 - amp) + max(0.0, amp - 1.0) * math.sin(2 * math.pi * (glide - lag)) if glide is not None else None
        return blend(bv, gv, M)
    bob = (0.8 * math.sin(4 * math.pi * phase) if walking else 0.0) - lift - RAISE
    bob_rest = -lift - RAISE   # the same without the walk's bob: the tail rings' fixed angles (tail_rings)
    # the walk's weight: the shoulders rise over a planted foreleg and the haunch over a planted hind leg, twice a cycle
    # each and in turn (the body rocks; the back under the seat keeps the bob only)
    heave_f = -WALK_HEAVE * math.cos(4 * math.pi * (phase + 0.25 - 0.31)) if walking else 0.0
    heave_h = -WALK_HEAVE * math.cos(4 * math.pi * (phase - 0.31)) if walking else 0.0
    if ARM:
        # whole pixels: a part keeps its pixels as it moves, so its shading and its plates stay put between frames
        # and the body rigid while it walks: one bob for all of it, no heave of the shoulders against the hips (that
        # reshaped the body and moved its shading bands between frames); the legs and the tail walk
        bob, heave_f, heave_h = float(round(bob)), 0.0, 0.0
    px = [(0, 0, 0, 0)] * (W * H)
    fpx = [(0, 0, 0, 0)] * (W * H)   # the front layer: drawn in front of the rider (the near wing)
    see_through = set()              # (ARM_SEE) a hole's pixels: the last passes leave them empty
    L = (-0.6, -0.8)

    def pitched(xx_world, y):
        # the column's own shift (climbing: forward of the seat up, behind it down), whole pixels for the whole column
        return y + (round(PITCH_SHEAR * pitch * (xx_world - PITCH_X)) if pitch else 0)

    def put(x, y, col, dst=None):
        cx = int(rnd(x))
        xx, yy = cx + OX, int(rnd(pitched(cx, y))) + OY
        if 0 <= xx < W and 0 <= yy < H:
            (dst if dst is not None else px)[yy * W + xx] = col + (255,)

    def get(x, y):
        cx = int(rnd(x))
        xx, yy = cx + OX, int(rnd(pitched(cx, y))) + OY
        return px[yy * W + xx] if 0 <= xx < W and 0 <= yy < H else (0, 0, 0, 0)

    def lp(a, b, t):
        return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)

    # The Armored Dragon's small parts (ARM).
    def claw(base, direction, n, dst, toward):
        # A hooked black claw out of a finger tip: on along the finger, then bent towards `toward` (the trailing edge).
        base = (rnd(base[0]), rnd(base[1]))                                       # on a whole pixel: the same shape every frame
        dx, dy = direction
        ln = math.hypot(dx, dy) or 1.0
        dx, dy = dx / ln, dy / ln
        sx, sy = -dy, dx
        if sx * (toward[0] - base[0]) + sy * (toward[1] - base[1]) < 0:
            sx, sy = -sx, -sy
        for k in range(n):
            bend = 1.4 * (k / max(1, n - 1)) ** 2
            x_, y_ = base[0] + dx * k + sx * bend, base[1] + dy * k + sy * bend
            if not (2 <= rnd(x_) + OX < W - 2 and 2 <= rnd(pitched(rnd(x_), y_)) + OY < H - 2):
                break
            put(x_, y_, DHORN[2] if k < n - 1 else DHORN[1], dst)
            if k == 0:
                put(x_ - sx, y_ - sy, DHORN[1], dst)

    def at_index(x, y):
        # the picture's index of a drawing point, as put() places it
        cx = int(rnd(x))
        return (int(rnd(pitched(cx, y))) + OY) * W + cx + OX

    def rim_of(hole_cells):
        # a see-through hole's rim: every cell beside it, lit on its upper left, the line colour on its lower right
        hs_ = set(hole_cells)
        mx2 = sum(h[0] for h in hole_cells) / len(hole_cells)
        my2 = sum(h[1] for h in hole_cells) / len(hole_cells)
        out_ = []
        for c_ in sorted({(h[0] + dx, h[1] + dy) for h in hole_cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))} - hs_):
            side = (c_[0] - mx2) + (c_[1] - my2)
            out_.append((c_, STEEL[6] if side < -0.75 else STEEL_LINE if side > 0.75 else STEEL[4]))
        return out_

    def pierce(centres, cells, bones, dst_of, small=False, plan=None):
        # Pierced holes (ARM_HOLES eyelets | punched): each a stamp on a whole pixel, a dark hole in a steel ring lit on its
        # upper left; drawn only where the whole stamp lies in the wing's cells, clear of every bone
        if small:
            hole = [(0, 0), (1, 0), (0, 1), (1, 1)]                              # the spread wing: the eyelet's hole, a glint and a shadow (its full ring rarely fits between the fingers)
        elif ARM_HOLES == "eyelets":
            hole = [(0, 0), (1, 0), (0, 1), (1, 1)]
        else:
            hole = [(0, -1), (-1, 0), (0, 0), (1, 0), (0, 1)]                    # round: a plus, three across
        hs = set(hole)
        ring = sorted({(h[0] + dx, h[1] + dy) for h in hole for dx in (-1, 0, 1) for dy in (-1, 0, 1)} - hs)
        if small:
            ring = [(-1, -1), (0, -1), (-1, 0), (2, 2), (1, 2), (2, 1)]          # its glint on the upper left, its shadow on the lower right
        elif ARM_HOLES == "eyelets":
            ring = [r for r in ring if not (r[0] in (-1, 2) and r[1] in (-1, 2))]   # its corners left out: a round ring
        mx_ = sum(h[0] for h in hole) / len(hole)
        my_ = sum(h[1] for h in hole) / len(hole)
        drew = 0
        def clear(s, d_):
            return not bones or min(_seg(s[0] + 0.5, s[1] + 0.5, *a, *b) for a, b in bones) >= d_
        for c in centres:
            ox_, oy_ = rnd(c[0]), rnd(c[1])
            if small:
                # part of the wing (the spread wing in the beat): drawn wherever its spot is on the surface, off a finger;
                # its glint and shadow only where they fall on the surface too (a whole stamp that had to fit came and went)
                hole_, ring_ = hole, ring
                if not all((ox_ + h[0], oy_ + h[1]) in cells and clear((ox_ + h[0], oy_ + h[1]), 0.9) for h in hole_):
                    # no room for the 2x2 this frame (the panel narrowed as the wing turned): a pin at its middle, so the
                    # hole shrinks for a frame instead of going
                    ox_, oy_ = rnd(c[0] + 0.5), rnd(c[1] + 0.5)
                    hole_, ring_ = [(0, 0)], [(-1, -1), (1, 1)]
                    if not ((ox_, oy_) in cells and clear((ox_, oy_), 0.9)):
                        continue
                if plan is not None:
                    hc_ = [(ox_ + h[0], oy_ + h[1]) for h in hole_]
                    plan.append((hc_, rim_of(hc_)))
                    drew += 1
                    continue
                for h in hole_:
                    put(ox_ + h[0], oy_ + h[1], DOUT[1], dst_of(ox_ + h[0], oy_ + h[1]))
                for r in ring_:
                    q = (ox_ + r[0], oy_ + r[1])
                    if q in cells and clear(q, 0.6):
                        put(q[0], q[1], STEEL[6] if r[0] + r[1] < 0 else STEEL_LINE, dst_of(q[0], q[1]))
                drew += 1
                continue
            stamp = [(ox_ + h[0], oy_ + h[1]) for h in hole] + [(ox_ + r[0], oy_ + r[1]) for r in ring]
            if not all(s in cells for s in stamp):
                continue
            if bones and any(min(_seg(s[0] + 0.5, s[1] + 0.5, *a, *b) for a, b in bones) < 0.9 for s in stamp):
                continue
            if plan is not None:
                hc_ = [(ox_ + h[0], oy_ + h[1]) for h in hole]
                plan.append((hc_, rim_of(hc_)))
                drew += 1
                continue
            for h in hole:
                put(ox_ + h[0], oy_ + h[1], DOUT[1], dst_of(ox_ + h[0], oy_ + h[1]))
            for r in ring:
                side = (r[0] - mx_) + (r[1] - my_)                               # < 0 upper left (lit), > 0 lower right
                put(ox_ + r[0], oy_ + r[1], STEEL[6] if side < -0.25 else STEEL_LINE if side > 0.25 else STEEL[4], dst_of(ox_ + r[0], oy_ + r[1]))
            drew += 1
        return drew

    def spike(base, direction, n, dst=None, wide=True):
        # A steel spike: two px at its root (lit and shade) and one px towards its tip, on a whole pixel.
        base = (rnd(base[0]), rnd(base[1]))
        dx, dy = direction
        ln = math.hypot(dx, dy) or 1.0
        dx, dy = dx / ln, dy / ln
        sx, sy = (-dy, dx) if (-dy * L[0] + dx * L[1]) > 0 else (dy, -dx)    # the side towards the light
        for k in range(n):
            x, y = base[0] + dx * k, base[1] + dy * k
            if not (2 <= rnd(x) + OX < W - 2 and 2 <= rnd(pitched(rnd(x), y)) + OY < H - 2):   # where put() will land it
                break
            put(x, y, STEEL[6] if k < n - 1 else STEEL[4], dst)
            if wide and k < n - 2:
                put(x - sx, y - sy, STEEL[3], dst)

    # ---- a wing: the arm from the shoulder up to the wrist (the peak), the first finger carrying the leading edge on
    # back along the top, three more fanning down behind the body, the membrane hanging between them down to the
    # trailing root above the hips; scallops between the finger tips. The whole wing rocks about the shoulder.
    # The near wing and the rider: the whole wing in front of him hides him (seen 2026-10-17), the whole wing behind
    # him reads as a cape growing from his back. So the arm bone (shoulder to wrist) and the membrane above his head
    # go in front of him, the rest of the membrane and the fingers behind: he sits between the wings, the leading edge
    # crossing in front of his chest.
    def wing(near, flap, span=1.0):
        sh = (46.0 + (0 if near else 7), 42.0 + bob - (0 if near else 6))
        pts = {"elbow": (48, 26), "wrist": (54, 8), "root": (73, 38),
               "tips": [(84, 1), (97, 15), (99, 33), (90, 48)]}
        if not near:
            pts = {k: ([(x + 7, y - 10) for x, y in v] if k == "tips" else (v[0] + 7, v[1] - 10)) for k, v in pts.items()}
        if span != 1.0:  # spread for flying: the wing longer than the body is deep (the reference's span), the root stays
            o = (46.0 + (0 if near else 7), 42.0 - (0 if near else 6))
            sc = lambda q: (o[0] + (q[0] - o[0]) * span, o[1] + (q[1] - o[1]) * span)
            pts = {k: ([sc(q) for q in v] if k == "tips" else (v if k == "root" else sc(v))) for k, v in pts.items()}
            pts = {k: ([(x, y + bob) for x, y in v] if k == "tips" else (v[0], v[1] + bob)) for k, v in pts.items()}
        ang = flap * math.pi / 180
        elbow, wrist, root = (_rot(pts[k], sh, ang) for k in ("elbow", "wrist", "root"))
        tips = [_rot(t, sh, ang) for t in pts["tips"]]
        ramp = DWING if near else DWING[:3]
        scal = []
        for a, b in list(zip(tips, tips[1:])) + [(tips[-1], root)]:
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            vx, vy = mx - wrist[0], my - wrist[1]
            vl = math.hypot(vx, vy) or 1
            half = math.dist(a, b) / 2
            depth = half * 0.3
            k = (half * half - depth * depth) / (2 * depth)
            cx, cy = mx + vx / vl * k, my + vy / vl * k
            scal.append((cx, cy, math.dist((cx, cy), a)))
        outer = [(wrist, a, b) for a, b in zip(tips, tips[1:])] + [(wrist, tips[-1], root)]
        inner = [(sh, wrist, root), (sh, elbow, wrist)]
        tris = outer + inner
        far = max(math.dist(wrist, t) for t in tips)
        bones = [(wrist, lp(wrist, t, 0.95)) for t in tips]        # the fingers end just inside the membrane's points
        xs = [p[0] for p in tips] + [sh[0], root[0], wrist[0]]
        ys = [p[1] for p in tips] + [sh[1], root[1], wrist[1]]
        for y in range(int(min(ys)) - 2, int(max(ys)) + 3):
            for x in range(int(min(xs)) - 2, int(max(xs)) + 3):
                X, Y = x + 0.5, y + 0.5
                in_outer = any(_tri(X, Y, *t) for t in outer)
                if not in_outer and not any(_tri(X, Y, *t) for t in inner):
                    continue
                if any(math.hypot(X - cx, Y - cy) < r for cx, cy, r in scal):
                    continue
                u = math.dist((X, Y), wrist) / far
                db = min(_seg(X, Y, *a, *b) for a, b in bones + [(sh, elbow), (elbow, wrist)])
                light = 0.3 + 0.5 * u + (0.18 if db > 3.5 else 0.0)     # lighter away from the bones and the wrist
                # the near wing over the rider: in the front layer, except where the rider sits (spread wings sweep
                # right over his seat: there he stays in front, the membrane behind him, as the folded wing's arm is)
                in_rider = span != 1.0 and 43 <= x <= 66 and 16 <= y - bob <= 52
                put(x, y, _tone(ramp, light), fpx if (near and in_outer and not in_rider) else px)
        for (ax, ay), (bx, by) in bones + [(sh, elbow), (elbow, wrist)]:
            dst = fpx if near else px
            thick = 1.6 if (ax, ay) == wrist else 2.6
            for y in range(int(min(ay, by)) - 3, int(max(ay, by)) + 4):
                for x in range(int(min(ax, bx)) - 3, int(max(ax, bx)) + 4):
                    d = _seg(x + 0.5, y + 0.5, ax, ay, bx, by)
                    if d <= thick / 2:
                        put(x, y, (DRG[3] if d < thick / 2 - 0.7 else DRG[2]) if near else DWING[1], dst)
        for k in range(4):                                                  # the thumb claw at the wrist
            put(wrist[0] - 1 - k, wrist[1] - k * 0.6, DHORN[2 if k > 1 else 1], dst)

    # ---- the wings SPREAD, flying (docs/flying.md "the frames"; the user's reference 2026-10-22: a red dragon in side
    # view, three poses of the beat; our own drawing). A bat wing seen from the side: the arm from the shoulder over
    # the elbow to the wrist is the leading edge, three long fingers fan from the wrist, the membrane spans them and
    # runs back to the root on the back before the hips, scalloped between the finger tips. Poses, t: 0 raised high
    # over the back (the tips up and back), 0.5 level back over the tail (seen nearly edge-on: thin), 1 swept down
    # under the belly; between them the joints move on together. All inside the 128 canvas (x <= 111, y >= -16 in the
    # donkey's coordinates). The far wing is the same a few px back and up, darker, behind the body. The near wing is
    # in front of everything except where the rider sits: there it passes behind him (else it hides him on the
    # upstroke), as the folded wing's arm does.
    WING_POSES = {
        "elbow": ((52, 28), (58, 34), (54, 49)),
        "wrist": ((58, 9), (72, 25), (58, 63)),
        "t1": ((64, -13), (102, 15), (62, 86)),
        "t2": ((89, -10), (110, 29), (75, 84)),
        "t3": ((105, 5), (104, 44), (86, 72)),
        "root": ((78, 41), (78, 45), (74, 47)),
    }
    # The raised pose 18 % lower about the shoulder (y 42), and the far wing only 4 px above the near one while raised
    # (7 level and down): with the body 6 px higher on its longer legs (RAISE) the raised wings ran 8-11 px past the
    # picture's top and were cut off flat (the user's video, 2026-10-23: "the upper part of the wing when flying seems
    # to be clipped"; measured on a picture 32 px taller, tools: tmp tallstudy.py). Now the highest pixel, outline
    # included, is inside the 128 picture in every frame.
    WING_POSES = {k: ((a[0], 42 + (a[1] - 42) * 0.82), b, c) for k, (a, b, c) in WING_POSES.items()}

    def pose_at(key, t):
        a, b, c = WING_POSES[key]
        if t <= 0.5:
            u = t / 0.5
            return (a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u)
        u = (t - 0.5) / 0.5
        return (b[0] + (c[0] - b[0]) * u, b[1] + (c[1] - b[1]) * u)

    # In the beat the joints do not move together: the arm leads, the hand a moment later, each finger later than
    # the one before (the tips trail up on the downstroke, down on the upstroke: the wing bends like a sail, it does
    # not swing as a plank), and on the upstroke the hand folds in a little (a bat's wing comes up half closed).
    WING_LAGS = {"elbow": 0.0, "wrist": 0.03, "root": 0.05, "t1": 0.07, "t2": 0.10, "t3": 0.13}

    # the glide's flex as pose: GLIDE_FLEX_PX over how fast the fastest joint moves per unit of pose at GLIDE_T (on the
    # pose's own side of the level pose, 0.5, where the curve bends)
    def joint_speed(key):
        e = 0.01
        lo, hi = (GLIDE_T - e, GLIDE_T + e)
        if lo < 0.5 <= hi:
            lo, hi = (GLIDE_T - 2 * e, GLIDE_T) if GLIDE_T <= 0.5 else (GLIDE_T, GLIDE_T + 2 * e)
        return math.dist(pose_at(key, max(0.0, lo)), pose_at(key, min(1.0, hi))) / (min(1.0, hi) - max(0.0, lo))
    glide_flex = GLIDE_FLEX_PX / max(1.0, max(joint_speed(k) for k in GLIDE_WING_LAGS))

    def tk(key):
        # a wing joint's pose: the beat's at its lag; gliding, GLIDE_T swaying glide_flex at its own lag; between them
        # (a way in or out) its own share of the way (mshare)
        if beat is None and glide is None:
            return fly
        bv = wing_w(beat - WING_LAGS[key]) if beat is not None else None
        gv = GLIDE_T + glide_flex * math.sin(2 * math.pi * (glide - GLIDE_WING_LAGS[key])) if glide is not None else None
        if ARM and glide is not None and key in ("elbow", "wrist"):
            gv = GLIDE_T                                                         # the arm held, the fingers flexing on from it: a px of
            #                                                                      the arm's move re-laid its whole steel edge
        return blend(bv, gv, mshare(GLIDE_MIX_LAGS[key]))

    def spread_wing(near, t):
        ox, oy = (0.0, 0.0) if near else (-3.0, -7.0)  # the far wing: up and a little forward (the perspective)
        if not near:
            tw = tk("wrist")
            oy += 3.0 * max(0.0, 1.0 - tw / 0.5)        # raised: 4 px above the near wing, not 7 (the picture's top)

        # w: how much of the far wing's offset a point takes. The far wing joins the far side of the spine, the same line
        # of the back from this side, so its shoulder and its root on the back take none of it and only its outer parts
        # rise, as the camera looks down on them; the whole wing moved up 4-7 px floated its joint above the back, a gap
        # of ground between the wing and the body ("detached from the body", the user 2026-10-23, the level and down
        # frames)
        def at(q, w=1.0):
            return (q[0] + ox * w, q[1] + oy * w + bob)

        sh = at((47.0, 42.0), 0.0)
        el, wr = at(pose_at("elbow", tk("elbow")), 0.35), at(pose_at("wrist", tk("wrist")), 0.7)
        tips = [at(pose_at(k, tk(k))) for k in ("t1", "t2", "t3")]
        root = at(pose_at("root", tk("root")), 0.0)
        if beat is not None:
            th = beat % 1.0
            fold = 0.16 * math.sin(math.pi * (th - 0.4) / 0.6) if th >= 0.4 else 0.0
            fold *= 1.0 - mshare(GLIDE_MIX_LAGS["t2"])   # the upstroke's half-closed hand opens as the glide takes it
            tips = [(wr[0] + (q[0] - wr[0]) * (1 - fold), wr[1] + (q[1] - wr[1]) * (1 - fold)) for q in tips]
        if ARM and ARM_WING in ("saw-long", "saw-blade"):
            # saw-long: the fingers closed up towards the first and longer, one long narrow wing swept back; saw-blade:
            # spread wider apart towards the root, a broad square wing (each finger turned about the wrist)
            a1 = math.atan2(tips[0][1] - wr[1], tips[0][0] - wr[0])
            turn, grow = ((0.0, 0.38, 0.5), (1.18, 1.16, 1.08)) if ARM_WING == "saw-long" else ((0.0, -0.22, -0.4), (1.0, 1.04, 1.1))
            nt = []
            for q, k_, g_ in zip(tips, turn, grow):
                a_ = math.atan2(q[1] - wr[1], q[0] - wr[0])
                da = (a1 - a_ + math.pi) % (2 * math.pi) - math.pi
                a_ += da * k_
                l_ = math.dist(q, wr) * g_
                nt.append((wr[0] + l_ * math.cos(a_), wr[1] + l_ * math.sin(a_)))
            tips = [(min(max(q[0], 2 - OX), W - OX - 5), min(max(q[1], 5 - OY), H - OY - 5)) for q in nt]
        if ARM and ARM_WING == "pointed":
            # the first finger far longer and the second a little: one long swept point; the third shorter (kept inside
            # the picture: 4 px from its top, 4 from its end)
            tips = [(wr[0] + (q[0] - wr[0]) * f, wr[1] + (q[1] - wr[1]) * f) for q, f in zip(tips, (1.3, 1.1, 0.9))]
            tips = [(min(q[0], W - OX - 5), max(q[1], 5 - OY)) for q in tips]
        if ARM:
            # the joints on whole pixels: the membrane's pockets and stretch lines (measured from the wrist) keep their
            # pixels while a joint holds, and step with it when it moves
            sh, el, wr, root = ((float(round(q[0])), float(round(q[1]))) for q in (sh, el, wr, root))
            tips = [(float(round(q[0])), float(round(q[1]))) for q in tips]
        ring = [sh, el, wr] + tips + [root]
        lerp = lambda a, b, f: (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f)
        # The veins end along the trailing edge about 9.5 px apart, as the fold's finger tips meet its hem: every finger,
        # and as many between as the edge's length there takes. Each end is a point of the scalloped edge (the fold's hem
        # is scalloped between every pair of vein ends; three big scallops between the fingers alone read plain).
        trail = []
        # the Armored Dragon: a fourth heavy finger from the wrist down to the trailing edge between the last finger and
        # the root, so the wing's lower half is cut too (a bat's wing, not a sail)
        wtips = tips + [lerp(tips[-1], root, 0.5)] if ARM else tips
        if ARM and ARM_WING.startswith("saw"):
            wtips = tips + [lerp(tips[-1], root, 0.34), lerp(tips[-1], root, 0.67)]   # five fingers
        for a_, b_ in zip(wtips + [root], wtips[1:] + [root]):
            k_ = 1 if ARM else max(1, int(round(math.dist(a_, b_) / 9.5)))   # the Armored Dragon: one deep cut between fingers
            trail += [lerp(a_, b_, j / k_) for j in range(k_)]
        trail.append(root)
        # and the inner part of the membrane, from the wing's root on the back to the shoulder, gets veins too (the
        # fold's fan covers its whole membrane down to the shoulder; spread, the triangle between the arm and the back
        # was one flat colour): ends along that line, as far apart, no scallops there (it is the body's edge)
        k_ = max(1, int(round(math.dist(root, sh) / 9.5)))
        inner = ([lerp(root, sh, 0.35), lerp(root, sh, 0.68)] if ARM_WING.startswith("saw") else []) if ARM else [lerp(root, sh, j / k_) for j in range(1, k_)]
        scal = []
        for a, b in zip(trail, trail[1:]):                        # the trailing edge's scallops, bulging into the membrane
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            vx, vy = mx - wr[0], my - wr[1]
            vl = math.hypot(vx, vy) or 1
            half = math.dist(a, b) / 2
            depth = half * (0.55 if ARM else 0.32)                # the Armored Dragon's cuts between its fingers
            k = (half * half - depth * depth) / (2 * depth)
            scal.append((mx + vx / vl * k, my + vy / vl * k, math.dist((mx + vx / vl * k, my + vy / vl * k), a)))
        notches = []
        if ARM and ARM_WING.startswith("saw"):
            # straight notches, not round scallops: a triangle cut in towards the wrist between each pair of fingers
            # (saw-claw: almost to the wrist, the fingers a hand of talons)
            scal = []
            for ni, (a, b) in enumerate(zip(trail, trail[1:])):
                if b == root:
                    continue                                                     # none by the body: it opened a hole by the hip
                mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
                vx, vy = wr[0] - mx, wr[1] - my
                vl = math.hypot(vx, vy) or 1
                dep = math.dist(a, b) * (1.25 if ARM_WING == "saw-claw" else 0.55)
                if ARM_WING == "saw-blade":
                    dep *= 1.35 if ni % 2 == 0 else 0.7                       # a war-torn edge: deep and shallow cuts in turn
                dep = min(dep, vl - 3.0)
                notches.append((a, b, (mx + vx / vl * dep, my + vy / vl * dep)))

        def inside(X, Y):
            hit = False
            for i in range(len(ring)):
                (ax, ay), (bx, by) = ring[i], ring[(i + 1) % len(ring)]
                if (ay > Y) != (by > Y) and X < ax + (Y - ay) * (bx - ax) / (by - ay):
                    hit = not hit
            return hit
        # Drawn the way the folded wing is (the user, 2026-10-23: "the veins aren't as plentiful or the same as when on
        # the ground"; measured: the fold's veins are 26 % of its pixels, one wine tone with a dark border inside its
        # edge; the spread wing had 11-18 % veins, a lighter purple the fold never uses, and thicker bones with a light
        # centre). So: the membrane first, its tone from the fold's rule (lit near the wrist, darker away from it and by
        # the veins), a dark border on its edge; then the veins, 1 px lines in the fold's red over the membrane only,
        # fanning from the wrist: the three fingers and more between them, spaced along the trailing edge as the fold's
        # along its hem. The arm, the leading edge, stays a thicker bone in the same red.
        def in_rider_at(x, y):
            return (WING_OVER_RIDER == "box" and 43 <= x <= 66 and 16 <= y - bob <= 52) or (WING_OVER_RIDER == "head" and rider_in_view(x + OX, y + OY))
        # each vein from 1.5 px out of the wrist (all meeting in the wrist's pixel made a red knot there) to its end
        def from_wrist(q):
            dd = math.dist(wr, q) or 1
            return (wr[0] + (q[0] - wr[0]) * 1.5 / dd, wr[1] + (q[1] - wr[1]) * 1.5 / dd)
        veins = [(from_wrist(q), lerp(wr, q, 0.98)) for q in trail[:-1] + inner]
        detail = ARM and ARM_WING.startswith("saw")
        # the Armored Dragon's wing detail: thin veins to the notches' inner points, two tears through the membrane
        veins2 = [(from_wrist(n_[2]), lerp(wr, n_[2], 0.92)) for n_ in notches] if detail else []
        tears = []
        if detail and ARM_HOLES == "tears" and len(trail) > 3 and abs((ring[0][0] - wr[0]) * (root[1] - wr[1]) - (ring[0][1] - wr[1]) * (root[0] - wr[0])) > 260:   # tears only where the wing shows enough membrane
            for i_, f_, r_ in ((0, 0.62, 1.3), (2, 0.55, 1.1)):
                a_, b_ = trail[i_], trail[i_ + 1]
                mid_ = lerp(a_, b_, 0.5)
                tears.append((lerp(wr, mid_, f_), r_))
        xs = [q[0] for q in ring]
        ys = [q[1] for q in ring]
        far_d = max(math.dist(wr, q) for q in tips) or 1
        cells = {}
        lines_ = set()
        for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
            for x in range(int(min(xs)) - 1, int(max(xs)) + 2):
                X, Y = x + 0.5, y + 0.5
                if not inside(X, Y) or any(math.hypot(X - cx, Y - cy) < r for cx, cy, r in scal) or any(_tri(X, Y, *n_) for n_ in notches):
                    continue
                dv, vl = min((_seg(X, Y, *a, *b), math.dist(a, b)) for a, b in veins)
                # how far out along its own vein (0 at the wrist, 1 at the edge), as the fold's light runs from its peak
                # to its hem: the dark bands beside the veins in their outer third, along every vein (measured against
                # the longest vein, only the farthest tip had them)
                u = min(1.0, math.dist((X, Y), wr) / max(1.0, vl))
                if detail:
                    if any(math.hypot(X - c_[0], Y - c_[1]) < r_ for c_, r_ in tears):
                        continue                                                 # a tear through it
                    # pockets: lit along a finger, darker between two, darker out to the edge; fine stretch lines
                    # across it (rings about the wrist), a step lighter
                    lt = 0.6 - 0.22 * min(1.0, dv / 5.0) - 0.14 * u
                    if ARM_WINGMAT != "leather":
                        # metal: lit smoothly out from the wrist, a step darker by a finger; the stretch lines are seams
                        lt = 0.66 - 0.2 * u - (0.14 if dv < 1.5 else 0.0)
                        ring_ = math.dist((X, Y), wr) % 4.5
                        if dv > 1.5 and ring_ < 0.9:
                            lt -= 0.3                                            # the seam: the plate's edge
                        elif dv > 1.5 and ring_ < 1.9:
                            lt += 0.2                                            # the next plate's lit edge
                        cells[(x, y)] = lt
                        continue
                    if math.dist((X, Y), wr) % 4.5 < 0.9 and dv > 1.5:
                        lines_.add((x, y))                                       # a stretch line here (a full step up: below)
                    cells[(x, y)] = lt
                    continue
                cells[(x, y)] = min(0.49, 0.62 - 0.36 * u) + (0.0 if dv > 2.0 else -0.14)   # the fold's light rule, in the fold's tones (its peak's lighter wine has no room on the fold; spread, it filled the wing near the wrist)
        if detail:
            # trim the membrane's spurs as the fold's (a sliver one px wide is all outline: a black spike), twice
            for _ in range(2):
                spur = [c for c in cells if sum((c[0] + dx, c[1] + dy) in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) < 2]
                for c in spur:
                    del cells[c]
            # the stretch lines only 2 px inside the membrane: on a wing seen nearly edge-on (the glide, the downstroke)
            # they were noise that re-shuffled with every px of flex
            for c in lines_:
                if c in cells and all((c[0] + dx, c[1] + dy) in cells for dx in (-2, 0, 2) for dy in (-2, 0, 2)):
                    cells[c] += 0.27
        hole_spots, see_plan = [], []
        if detail and ARM_HOLES != "tears" and len(trail) > 3:
            # pierced holes between the fingers: each at its panel's roomiest point this frame (part of the wing)
            def edge_room(c_):
                # how far a cell is from the wing's edge (a cell not on the surface), up to 3
                for r_ in (1, 2, 3):
                    for dx_ in range(-r_, r_ + 1):
                        for dy_ in range(-r_, r_ + 1):
                            if max(abs(dx_), abs(dy_)) == r_ and (c_[0] + dx_, c_[1] + dy_) not in cells:
                                return r_ - 0.5
                return 3.0
            for i_ in ((0, 2, 3) if ARM_HOLES == "eyelets" else (1, 3)):
                if i_ + 1 >= len(veins):
                    continue
                # a fixed aim in the panel: out from the wrist along its middle (the fingers' mean direction snapped to
                # 6 degrees), a whole even number of px; it holds while the wing holds
                (a0, b0), (a1, b1) = veins[i_], veins[i_ + 1]
                l0, l1 = math.dist(wr, b0) or 1.0, math.dist(wr, b1) or 1.0
                mdx, mdy = (b0[0] - wr[0]) / l0 + (b1[0] - wr[0]) / l1, (b0[1] - wr[1]) / l0 + (b1[1] - wr[1]) / l1
                ang = math.radians(6.0 * round(math.degrees(math.atan2(mdy, mdx)) / 6.0))
                dist_ = 2.0 * round(0.3 * min(l0, l1))
                aim = (wr[0] + math.cos(ang) * dist_, wr[1] + math.sin(ang) * dist_)
                best, best_pin = None, None
                for c_ in cells:
                    if any(max(abs(c_[0] + 0.5 - s_[0] - 1.0), abs(c_[1] + 0.5 - s_[1] - 1.0)) < 4.0 for s_ in hole_spots):
                        continue                                                 # a hole placed already: 4 px apart
                    ds_ = [_seg(c_[0] + 0.5, c_[1] + 0.5, *a, *b) for a, b in veins]
                    if min(ds_) < min(ds_[i_], ds_[i_ + 1]) - 1e-6:
                        continue                                                 # nearer another finger: another panel's
                    key = (math.dist((c_[0] + 1.0, c_[1] + 1.0), aim), c_[1], c_[0])   # the nearest to the aim (a tie: the same each frame)
                    quad = [(c_[0] + dx_, c_[1] + dy_) for dx_ in (0, 1) for dy_ in (0, 1)]
                    if all(q_ in cells and min(_seg(q_[0] + 0.5, q_[1] + 0.5, *a, *b) for a, b in veins) >= 0.9 for q_ in quad):
                        if best is None or key < best[0]:
                            best = (key, c_)                                     # a 2x2 fits here, as pierce draws it
                    elif ds_[i_] >= 0.9 and ds_[i_ + 1] >= 0.9 and (best_pin is None or key < best_pin[0]):
                        best_pin = (key, c_)                                     # a pin fits here
                if best is not None:
                    hole_spots.append((float(best[1][0]), float(best[1][1])))  # its top-left cell (pierce rounds it as is)
                elif best_pin is not None:
                    hole_spots.append((best_pin[1][0] - 0.5, best_pin[1][1] - 0.5))   # no 2x2 anywhere in the panel: a pin
            if ARM_SEE:
                # see-through: the holes' cells left out of the surface (and its veins and seams), their rims drawn after
                pierce(hole_spots, cells, veins, None, small=True, plan=see_plan)
                for hc_, _ in see_plan:
                    for c_ in hc_:
                        cells.pop(c_, None)
        for (x, y), light in cells.items():
            border = any((x + dx, y + dy) not in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            put(x, y, DOUT[1] if border else _tone(DWING, light), fpx if (near and not in_rider_at(x, y)) else px)
        for (ax, ay), (bx, by) in veins:                         # the veins: 1 px red lines over the membrane, as the fold's
            n = int(math.dist((ax, ay), (bx, by)) * 2) + 1
            vx_, vy_ = (bx - ax), (by - ay)
            vl_ = math.hypot(vx_, vy_) or 1.0
            for i in range(n + 1):
                x, y = ax + (bx - ax) * i / n, ay + (by - ay) * i / n
                if (int(round(x)), int(round(y))) in cells:
                    if ARM:
                        # a heavy finger: two px, its lit side a step lighter
                        put(x, y, DRG[3], fpx if (near and not in_rider_at(int(round(x)), int(round(y)))) else px)
                        q = (x + vy_ / vl_, y - vx_ / vl_)
                        if (int(round(q[0])), int(round(q[1]))) in cells:
                            put(q[0], q[1], DRG[1], fpx if (near and not in_rider_at(int(round(q[0])), int(round(q[1])))) else px)
                    else:
                        put(x, y, DRG[2], fpx if (near and not in_rider_at(int(round(x)), int(round(y)))) else px)
        for (ax, ay), (bx, by) in veins2:                        # the thin veins to the notches
            n = int(math.dist((ax, ay), (bx, by)) * 2) + 1
            for i in range(n + 1):
                x, y = ax + (bx - ax) * i / n, ay + (by - ay) * i / n
                if (int(round(x)), int(round(y))) in cells:
                    put(x, y, DRG[2], fpx if (near and not in_rider_at(int(round(x)), int(round(y)))) else px)
        if see_plan:
            for hc_, rim_ in see_plan:                                           # the see-through holes' steel rims
                for c_, col_ in rim_:
                    if c_ in cells:
                        put(c_[0], c_[1], col_, fpx if (near and not in_rider_at(c_[0], c_[1])) else px)
                for c_ in hc_:
                    see_through.add(at_index(c_[0], c_[1]))
        elif hole_spots:
            pierce(hole_spots, cells, veins, lambda x_, y_: fpx if (near and not in_rider_at(x_, y_)) else px, small=True)
        if False:
            # (removed 2026-11-07, the user: "weird gray artifacts on the wing") a steel band with a brass rivet round each finger
            for (ax, ay), (bx, by) in veins:
                ln_ = math.dist((ax, ay), (bx, by)) or 1.0
                ux_, uy_ = (bx - ax) / ln_, (by - ay) / ln_
                cx_, cy_ = ax + (bx - ax) * 0.34, ay + (by - ay) * 0.34
                for s_ in (-1, 0, 1, 2):
                    for w_ in (0, 1):
                        qx, qy = cx_ - uy_ * (s_ - 0.5) + ux_ * w_, cy_ + ux_ * (s_ - 0.5) + uy_ * w_
                        if (int(round(qx)), int(round(qy))) in cells:
                            put(qx, qy, STEEL[5] if w_ == 0 else STEEL[3], fpx if (near and not in_rider_at(int(round(qx)), int(round(qy)))) else px)
                put(cx_, cy_, BRASS[2], fpx if (near and not in_rider_at(int(round(cx_)), int(round(cy_)))) else px)
        for (ax, ay), (bx, by) in ((sh, el), (el, wr)):          # the arm, the leading edge: a thicker bone, the same red
            for y in range(int(min(ay, by)) - 3, int(max(ay, by)) + 4):
                for x in range(int(min(ax, bx)) - 3, int(max(ax, bx)) + 4):
                    dd = _seg(x + 0.5, y + 0.5, ax, ay, bx, by)
                    if dd <= (1.9 if ARM else 1.3):
                        if ARM:
                            # A steel-clad leading edge: the lit side bright, the far side shaded, a seam at each joint.
                            ux, uy = bx - ax, by - ay
                            side = ((x + 0.5 - ax) * -uy + (y + 0.5 - ay) * ux) * (1 if (-uy * L[0] + ux * L[1]) > 0 else -1)
                            col = STEEL[6] if side > 0.6 else STEEL[4] if side > -0.6 else STEEL[2]
                            if min(math.dist((x + 0.5, y + 0.5), el), math.dist((x + 0.5, y + 0.5), wr)) < 1.0:
                                col = STEEL_LINE
                            # (no rivets along the forearm: picked from cells within half a px of its middle line,
                            # they showed only at some angles, gold blinking in the beat)
                        else:
                            col = DRG[2]
                        put(x, y, col, fpx if (near and not in_rider_at(x, y)) else px)
        for k in range(4):                                       # the thumb claw at the wrist, hooking forward (behind his face too)
            cx_, cy_ = wr[0] - 1 - k, wr[1] - k * 0.5
            face = WING_OVER_RIDER == "head" and rider_in_view(int(round(cx_)) + OX, int(round(cy_)) + OY)
            put(cx_, cy_, DHORN[2 if k > 1 else 1], fpx if (near and not face) else px)
        if ARM:
            dst = fpx if near else px
            # a steel spike off the wrist, pointing on from the forearm
            spike((wr[0], wr[1]), (wr[0] - el[0], wr[1] - el[1]), 5, dst)
            if ARM_WING.startswith("saw"):
                # a big hooked thumb claw off the wrist, curling forward and down
                claw((wr[0] - 1, wr[1]), (-1.0, -0.5), 6, dst, (wr[0] - 6, wr[1] + 6))
            if ARM_WING == "saw-blade":
                # three blades standing off the forearm's leading side, raked back towards the shoulder
                ux_, uy_ = wr[0] - el[0], wr[1] - el[1]
                ul_ = math.hypot(ux_, uy_) or 1.0
                nx_, ny_ = -uy_ / ul_, ux_ / ul_
                if (nx_ * (root[0] - el[0]) + ny_ * (root[1] - el[1])) > 0:
                    nx_, ny_ = -nx_, -ny_                                        # away from the membrane
                for f_ in (0.3, 0.55, 0.8):
                    b_ = (el[0] + ux_ * f_, el[1] + uy_ * f_)
                    spike(b_, (nx_ - ux_ / ul_ * 0.6, ny_ - uy_ / ul_ * 0.6), 4, dst)
            # each finger ends in a hooked black claw past the membrane, bent towards the trailing edge
            for q in tips:
                start = None                                       # no claw where the membrane is cut away round the tip
                for f in [1.0 - 0.02 * i for i in range(16)]:
                    p_ = lerp(wr, q, f)
                    if (int(p_[0]), int(p_[1])) in cells:
                        start = p_
                        break
                if start is not None:
                    if ARM_WING.startswith("saw"):
                        spike(start, (q[0] - wr[0], q[1] - wr[1]), 4 if ARM_WING in ("saw-claw", "saw-blade") else 3, dst, wide=False)   # steel caps on the finger tips
                    else:
                        claw(start, (q[0] - wr[0], q[1] - wr[1]), 4 if ARM_WING == "pointed" else 3, dst, root)

    # ---- the wings, FOLDED, as the user's references draw them (2026-10-20; docs/mount-looks-design.md): the wrist is a
    # tall PEAK above the back, its thumb claw hooking towards the head; the fingers run DOWN from the peak; the membrane
    # hangs between them like a curtain to the ground BEHIND the hindquarters, scalloped along its bottom; the body and
    # its belly line stay in view. The near wing's peak stands behind the rider, its curtain in the front layer over
    # the tail root (the camera side); the arm from the shoulder up to the peak is drawn above the back, behind him.
    # The far wing is a shorter peak above the shoulder, its membrane a wedge behind the neck, all of it behind the
    # body. The membrane is wine against the body's red, outlined, the finger bones red lines.
    def folded_wing(flap, far):
        # one fold for both wings, TUCKED, as the references draw a dragon that walks (the red walk frames, every blue
        # frame; the curtain to the ground is the big idle pose only): the wrist is a peak above the shoulder with the
        # thumb claw hooking forward, the fingers run down and back from it, the membrane lies against the flank from
        # the peak back to the hip, its lower edge along the belly line, the belly band and the legs in view under it;
        # nothing of the wing forward of the peak, nothing on the ground. The whole wing moves with the body as one
        # piece: no sway and no pinned hem (a peak that bobbed and swayed over a hem pinned to the ground re-drew the
        # membrane every frame and jiggled; the user's video, 2026-10-20). The whole near wing is the front layer,
        # over the rider wherever the two overlap: he sits inside the fold with his face, the top of his head and the
        # reins arm out of it. The far wing is the same shape a few pixels back and up (the perspective), drawn before
        # the body, which hides all but its peak and the top of its membrane.
        # The near fold 3 px lower than first drawn: in the game the front layer was lifted along the part's own axis,
        # which (the camera looking down) drew it 2.7 art px lower than the body picture while ridden (the [occlusion]
        # readout, both facings), and that is the look judged good (2026-10-20). The lift now runs along the camera's
        # line of sight on the ground too (Looks.Rig, as in flight), so the drop is drawn into the picture instead: the
        # same look ridden, and now also unridden, at the take-off crouch and after a landing, on the review page and in
        # the Mods picture (it jumped 2.7 px between them). The far fold is in the body picture, never lifted: as drawn.
        ox, oy = (4.0, -5.0) if far else (0.0, 3.0)

        def at(x, y):
            # WING_ROCK: the fold rides the body's rock, the shoulder's heave at the front, the haunch's behind, the seat
            # between them about still (a tilt of the whole fold, not a sway of its parts)
            rock = 0.0
            if WING_ROCK:
                t = max(0.0, min(1.0, (x - 39.0) / 24.0))
                rock = WING_ROCK * (heave_f + (heave_h - heave_f) * t)
                if ARM:
                    rock = float(round(rock))
            return (x + ox, y + bob + oy + rock)
        sh = at(46.0, 43.0)                                                       # the shoulder, where the wing leaves the back
        peak = at(52.0, 26.0)
        # the wing tip points back along the tail (the blue references: a long wedge from the wrist to past the hips,
        # the user's mark 2026-10-20: over the rump and the first third of the tail, never near the ground)
        tip = at(99.0, 51.0)
        hem = [at(93.0, 56.0), at(84.0, 58.5), at(74.0, 59.5), at(64.0, 60.5), at(55.0, 60.5), at(48.0, 58.0)]   # the lower edge along the belly line
        ring = [peak, at(66.0, 29.0), at(84.0, 38.0), tip] + hem + [at(47.0, 54.0), sh]
        if ARM and ARM_WING == "saw-long":
            peak, tip = at(52.0, 22.0), at(104.0, 42.0)                          # a taller peak, the point long and swept up
            ring = [peak, at(70.0, 25.0), at(90.0, 32.0), tip] + hem + [at(47.0, 54.0), sh]
        if ARM and ARM_WING == "pointed":
            peak, tip = at(52.0, 22.0), at(103.0, 49.0)                          # a taller peak, a longer point back
            ring = [peak, at(68.0, 26.0), at(88.0, 37.0), tip] + hem + [at(47.0, 54.0), sh]
        tips = [at(84.0, 39.0), tip, at(88.0, 58.0), at(78.0, 59.5), at(68.0, 60.5), at(59.0, 61.0), at(51.0, 59.5)]
        if ARM:
            tips = [at(84.0, 39.0), tip, at(77.0, 59.5), at(60.0, 61.0), at(51.0, 59.5)]   # three heavy fingers, deep cuts between
            if ARM_WING.startswith("saw"):
                tips = [at(84.0, 39.0), tip, at(86.0, 58.0), at(76.0, 59.5), at(66.0, 60.5), at(57.0, 61.0), at(51.0, 59.5)]
        fingers = [(peak, t) for t in tips] + [(peak, sh)]                      # the last: the membrane's leading edge down to the shoulder
        dst = px if far else fpx
        # the lower edge's scallops between the finger tips: shallow arcs bulging up into the membrane
        scal = []
        for a, b in zip(tips[1:-1], tips[2:]):
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            half = math.dist(a, b) / 2
            depth = half * (0.4 if ARM else 0.3)
            k = (half * half - depth * depth) / (2 * depth)
            scal.append((mx, my + k, math.dist((mx, my + k), a)))
        notches = []
        if ARM and ARM_WING.startswith("saw"):
            scal = []
            for a, b in zip(tips[2:-1], tips[3:]):                               # straight notches up into the hem (none beside the tip)
                mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
                notches.append((a, b, (mx - 1.0, my - math.dist(a, b) * (1.1 if ARM_WING == "saw-claw" else 0.6))))

        def inside(X, Y):                                                         # even-odd point in polygon
            hit = False
            for i in range(len(ring)):
                (ax, ay), (bx, by) = ring[i], ring[(i + 1) % len(ring)]
                if (ay > Y) != (by > Y) and X < ax + (Y - ay) * (bx - ax) / (by - ay):
                    hit = not hit
            return hit
        cells = {}
        xs = [q[0] for q in ring]
        ys = [q[1] for q in ring]
        for y in range(int(min(ys)) - 2, int(max(ys)) + 3):
            for x in range(int(min(xs)) - 2, int(max(xs)) + 3):
                X, Y = x + 0.5, y + 0.5
                if not inside(X, Y):
                    continue
                if any(math.hypot(X - cx, Y - cy) < r for cx, cy, r in scal) or any(_tri(X, Y, *n_) for n_ in notches):
                    continue
                df = min(_seg(X, Y, *a, *b) for a, b in fingers)
                down = (Y - peak[1]) / max(1.0, max(ys) - peak[1])
                light = 0.62 - 0.36 * down + (0.0 if df > 2.0 else -0.14)       # lit at the peak, dark at the hem, dark by the bones
                if ARM and ARM_WING.startswith("saw") and False:
                    pass
                if ARM and ARM_WING.startswith("saw"):
                    tear_ = (peak[0] + (tips[2][0] - peak[0]) * 0.6 + (tips[3][0] - tips[2][0]) * 0.25,
                             peak[1] + (tips[2][1] - peak[1]) * 0.6 + (tips[3][1] - tips[2][1]) * 0.25)
                    if ARM_HOLES == "tears" and math.hypot(X - tear_[0], Y - tear_[1]) < 1.2:
                        continue                                                 # a tear through it
                    light = 0.6 - 0.2 * down - 0.22 * min(1.0, df / 4.5)
                    if ARM_WINGMAT != "leather":
                        # metal: lit from the peak down, a step darker by a finger; the stretch lines are seams
                        light = 0.66 - 0.24 * down - (0.14 if df < 1.5 else 0.0)
                        ring_ = math.dist((X, Y), peak) % 4.5
                        if df > 1.5 and ring_ < 0.9:
                            light -= 0.3
                        elif df > 1.5 and ring_ < 1.9:
                            light += 0.2
                    elif math.dist((X, Y), peak) % 4.5 < 0.9 and df > 1.5:
                        light += 0.27
                cells[(x, y)] = light
        if ARM and ARM_WING.startswith("saw"):
            # trim the membrane's spurs: a cell with fewer than two of its four neighbours (a sliver one px wide is all
            # outline, a black spike), twice
            for _ in range(2):
                spur = [c for c in cells if sum((c[0] + dx, c[1] + dy) in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) < 2]
                for c in spur:
                    del cells[c]
        fold_plan = []
        if ARM and ARM_WING.startswith("saw") and ARM_HOLES != "tears" and ARM_SEE:
            # see-through: the fold's holes found now (the first place along each gap that fits), left out of its surface
            for i_ in ((1, 3) if ARM_HOLES == "eyelets" else (2,)):
                if i_ + 1 >= len(tips):
                    continue
                for f_ in (0.55, 0.45, 0.65, 0.35, 0.75):
                    if pierce([lp(peak, lp(tips[i_], tips[i_ + 1], 0.5), f_)], cells, fingers, None, plan=fold_plan):
                        break
            for hc_, _ in fold_plan:
                for c_ in hc_:
                    cells.pop(c_, None)
        for (x, y), light in cells.items():
            border = any((x + dx, y + dy) not in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            put(x, y, DOUT[1] if border else _tone(DWING, light), dst)
        for (ax, ay), (bx, by) in fingers:                                       # the finger bones: red lines down the membrane
            n = int(math.dist((ax, ay), (bx, by)) * 2) + 1
            for i in range(n + 1):
                x, y = ax + (bx - ax) * i / n, ay + (by - ay) * i / n
                if (int(round(x)), int(round(y))) in cells:
                    put(x, y, DRG[3] if ARM else DRG[2], dst)
                    if ARM and (int(round(x + 1)), int(round(y))) in cells:
                        put(x + 1, y, DRG[1], dst)                               # heavy fingers, two px
            if False:                                                            # (the finger bands: removed, see the spread wing)
                cx_, cy_ = ax + (bx - ax) * 0.4, ay + (by - ay) * 0.4
                for dx_ in (-1, 0, 1, 2):
                    if (int(round(cx_ + dx_)), int(round(cy_))) in cells:
                        put(cx_ + dx_, cy_, STEEL[5], dst)
                    if (int(round(cx_ + dx_)), int(round(cy_ + 1))) in cells:
                        put(cx_ + dx_, cy_ + 1, STEEL[3], dst)
                put(cx_ + 0.5, cy_, BRASS[2], dst)
        if fold_plan:
            for hc_, rim_ in fold_plan:                                          # the see-through holes' steel rims
                for c_, col_ in rim_:
                    if c_ in cells:
                        put(c_[0], c_[1], col_, dst)
                for c_ in hc_:
                    see_through.add(at_index(c_[0], c_[1]))
        elif ARM and ARM_WING.startswith("saw") and ARM_HOLES != "tears":
            # pierced holes in the fold, between its fingers, part way down from the peak (eyelets two, punched one)
            for i_ in ((1, 3) if ARM_HOLES == "eyelets" else (2,)):
                if i_ + 1 >= len(tips):
                    continue
                for f_ in (0.55, 0.45, 0.65, 0.35, 0.75):                         # the first that fits (the fold is the same every frame)
                    if pierce([lp(peak, lp(tips[i_], tips[i_ + 1], 0.5), f_)], cells, fingers, lambda x_, y_: dst):
                        break
        for k in range(5):                                                        # the thumb claw: a hook from the peak towards the head
            put(peak[0] - 1 - k * 0.9, peak[1] - 1 - k * 0.5 + (0.6 * (k - 2) ** 2 if k > 2 else 0), DHORN[2 if k > 1 else 1], dst)
        if ARM:
            # the folded arm from the shoulder up to the peak clad in steel, a spike standing up off the peak, and a
            # hooked black claw at every finger's end along the hem
            for y in range(int(min(sh[1], peak[1])) - 2, int(max(sh[1], peak[1])) + 3):
                for x in range(int(min(sh[0], peak[0])) - 2, int(max(sh[0], peak[0])) + 3):
                    if _seg(x + 0.5, y + 0.5, *sh, *peak) <= 1.6 and (x, y) != (int(peak[0]), int(peak[1])):
                        lit_ = x + 0.5 < sh[0] + (peak[0] - sh[0]) * ((y + 0.5 - sh[1]) / ((peak[1] - sh[1]) or 1))
                        # a mid steel, not white: a white line from the shoulder up read the two folds as one wing
                        put(x, y, (STEEL[4] if lit_ else STEEL[2]) if not far else (STEEL[3] if lit_ else STEEL[1]), dst)
            spike((peak[0] + 1, peak[1] - 1), (0.35, -1.0), 5, dst)
            if ARM_WING == "saw-blade":
                for f_ in (0.35, 0.65):                                          # blades off the folded arm, raked back
                    b_ = (sh[0] + (peak[0] - sh[0]) * f_, sh[1] + (peak[1] - sh[1]) * f_)
                    spike(b_, (-0.9, -0.5), 4, dst)

    flap = (8.0 * math.sin(2 * math.pi * phase) if walking else 3.0 * idle)
    if fly is None:
        folded_wing(flap * 0.8, far=True)
    else:
        if not NO_SPREAD_WINGS:
            spread_wing(False, fly)

    # ---- the legs, as the user's reference draws a dragon's (2026-10-23: ours were sticks with flat bar feet that slid
    # along, "a slide or a crawl rather than a walk", and in the air "hooves"): thick muscled limbs that taper, the
    # hind leg a big thigh, a shin and a long foot rising to the hock, the foreleg an upper arm and a forearm; every
    # paw a pad with three curved claws in the horn colour, the claws resting on the ground walking and hanging down
    # (the forepaws) or trailing back (the hind) in the air. Lit from the upper left like the body; the far pair darker
    # and a few px back. Walking: each foot is planted and slides back under the body while it carries (the stance),
    # then lifts, the knee bending, and swings forward on an arc (the swing); the four in the walk's order (near hind,
    # near fore, far hind, far fore, a quarter of the cycle apart).
    def bone(a, b, r0, r1, ramp, dst=None):
        if ARM:
            # whole-pixel ends: from the solver's fractions the light, mid and dark bands along the bone were re-laid
            # every frame; a joint two bones share stays shared
            a, b = (float(rnd(a[0])), float(rnd(a[1]))), (float(rnd(b[0])), float(rnd(b[1])))
        ax, ay = a
        bx, by = b
        Lg = math.dist(a, b) or 1e-6
        ux, uy = (bx - ax) / Lg, (by - ay) / Lg
        nx, ny = -uy, ux                                      # the side normal; lit where it faces the light (upper left)
        if nx * L[0] + ny * L[1] < 0:
            nx, ny = -nx, -ny
        r0, r1 = r0 * LEG_BULK, r1 * LEG_BULK
        r1 = r1 + (r0 - r1) * LEG_WRIST
        R = max(r0, r1)
        for y in range(int(min(ay, by) - R) - 1, int(max(ay, by) + R) + 2):
            if legs_mode == "walk" and y > GROUND:
                continue                                      # on the ground nothing goes below the paws' ground row (a thick ankle's round end did)
            for x in range(int(min(ax, bx) - R) - 1, int(max(ax, bx) + R) + 2):
                X, Y = x + 0.5, y + 0.5
                t = max(0.0, min(1.0, ((X - ax) * ux + (Y - ay) * uy) / Lg))
                cx, cy = ax + ux * Lg * t, ay + uy * Lg * t
                r = r0 + (r1 - r0) * t
                d = math.hypot(X - cx, Y - cy)
                if d > r:
                    continue
                side = ((X - cx) * nx + (Y - cy) * ny) / max(r, 0.5)   # +1 the lit side, -1 the shadow side
                col = ramp[3] if side > 0.45 else ramp[2] if side > -0.35 else ramp[1]
                put(x, y, col, dst)

    # The paws, placed by hand pixel by pixel (procedural claws at this size read as a broom or a hoof, the user's
    # second look 2026-10-23): the reference's paw is a red pad with separate claws in yellow (ours: the belly's gold),
    # a gap between each. Facing left; (0, 0) is the end of the limb. 'ground' walking: the toes forward along the
    # ground, the claw tips touching it (the bottom row is the ground row); 'hang' in the air or lifted: the claws
    # down and a little forward; 'trail' the hind paw flying: the claws back and down.
    # r/R the pad (lit, shadow), g/G/d the claws (light, mid, dark)
    PAWS = {
        "ground": ((-5, 0), ["...rrR",
                             "GgdgrR"]),
        "hang": ((-2, 0), [".rrR",
                           ".rrR",
                           "g.g.",
                           "G.G."]),
        "trail": ((-1, 0), ["Rrr.",
                            "Rrr.",
                            ".g.g",
                            "..G.G"]),
        # DRAGON_PAW=heavy: a broad paw, the heel behind the ankle, three claws apart in front; lifted, the same flat
        "ground_h": ((-6, -1), ["..rrrrR.",
                                ".rrrrrRR",
                                "g.g.g.RR"]),
        "lift_h": ((-5, -1), ["..rrrR",
                              ".rrrrR",
                              "g.g.g."]),
    }

    def paw(at, kind, ramp, dst=None):
        (ox, oy), rows = PAWS[kind]
        claws = DHORN if ARM else DBELLY[1:]                  # the Armored Dragon's claws black horn, the Red Dragon's gold
        cols = {"r": ramp[2], "R": ramp[1], "g": claws[2], "G": claws[1], "d": claws[0]}
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch in cols:
                    put(round(at[0]) + ox + i, round(at[1]) + oy + j, cols[ch], dst)

    def ik(hip, foot, l1, l2, bend):
        # the knee of a two-bone limb from hip to foot; bend +1 = the knee behind the line (an elbow), -1 = in front
        d = min(math.dist(hip, foot), l1 + l2 - 0.01)
        ang = math.atan2(foot[1] - hip[1], foot[0] - hip[0])
        a = math.acos(max(-1.0, min(1.0, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
        k = ang - bend * a
        return (hip[0] + l1 * math.cos(k), hip[1] + l1 * math.sin(k))

    STRIDE, LIFT, STANCE = STEP_STRIDE, STEP_LIFT, STEP_STANCE

    def step_at(q, cx):
        # the foot's place over the cycle: planted and sliding back, then lifted and swung forward
        q %= 1.0
        if q < STANCE:
            return (cx - STRIDE / 2 + STRIDE * q / STANCE, GROUND), False   # planted: slides back towards the tail (+x) as the body walks on (it faces left)
        u = (q - STANCE) / (1 - STANCE)
        e = u * u * (3 - 2 * u)
        return (cx + STRIDE / 2 - STRIDE * e, GROUND - LIFT * math.sin(math.pi * u)), True   # lifted: swings forward to the head (-x); walked backwards the other way round (the user, 2026-10-23)

    def foreleg(hip, foot, ramp, paw_kind):
        fl = FORE_LEN if legs_mode == "walk" else 1.0       # the lengths on the ground only (the flying legs keep theirs)
        if FORE_HEAVY and legs_mode == "walk":
            # on the ground only (in the air the folded heavy arm and its shoulder mass made one lump under the chest
            # that changed shape between the beat's, the glide's and the climb's frames and hid the far paw: the user,
            # 2026-10-24, "the front leg is jumping from one place to the other"); like the hind leg: a thick upper arm (the thigh's 5.2), a thick forearm (the shin's 3.0) to a wrist above
            # the paw, and a short wrist bone down to it (the hind foot's), the paw ahead of the wrist
            wrist = (foot[0] + 1.0, foot[1] - 2.5)
            elbow = ik(hip, wrist, (6.5 + RAISE * 0.5) * fl, (6.0 + RAISE * 0.5) * fl, +1)
            bone(hip, elbow, 5.0, 3.4, ramp)
            bone(elbow, wrist, 3.2, 2.4, ramp)
            if greave[0] is not None:
                greave_segs.append((elbow, wrist, 3.2, 2.4))
            bone(wrist, foot, 2.2, 1.8, ramp)
            paw(foot, paw_kind, ramp)
            return
        elbow = ik(hip, foot, (6.5 + RAISE * 0.5) * fl, (6.5 + RAISE * 0.5) * fl, +1)
        bone(hip, elbow, 3.6, 2.6, ramp)
        bone(elbow, foot, 2.6, 1.8, ramp)
        if greave[0] is not None:
            greave_segs.append((elbow, foot, 2.6, 1.8))
        paw(foot, paw_kind, ramp)

    def hindleg(hip, hock, toe, ramp, paw_kind):
        hl = HIND_LEN if legs_mode == "walk" else 1.0
        knee = ik(hip, hock, (7.5 + RAISE * 0.55) * hl, (6.0 + RAISE * 0.45) * hl, -1)
        bone(hip, knee, 5.2, 3.2, ramp)                       # the big thigh
        bone(knee, hock, 3.0, 2.0, ramp)
        if greave[0] is not None:
            greave_segs.append((knee, hock, 3.0, 2.0))
        bone(hock, toe, 2.0, 1.6, ramp)                       # the long foot up to the hock
        paw(toe, paw_kind, ramp)

    greave = [None]   # the Armored Dragon: steel greaves on the near forearm and shin (plates over them, after the legs)
    greave_segs = []

    def legs(far):
        ramp = ((DRG[0], DRG[0], DRG[0], DRG[1]) if FAR_DARK else (DRG[0], DRG[0], DRG[1], DRG[2])) if far else (DRG[0], DRG[1], DRG[2], DRG[3])
        if ARM:
            greave[0] = None if far else True
        dx, dy = (FAR_DX, FAR_DY) if far else (0, 0)
        fhip, hhip = (39 + dx, 53 + bob + dy + heave_f), (66 + dx, 53 + bob + dy + heave_h)
        fwd = (-1.0, 0.0)
        if legs_mode == "walk":
            qf = phase + (0.75 if far else 0.25)
            qh = phase + (0.5 if far else 0.0)
            if not walking:
                qf = qh = 0.31                                 # standing: each foot under its hip
            (ff, f_up), (ht, h_up) = step_at(qf, 36 + dx), step_at(qh, 66 + dx)
            ff = (ff[0], ff[1] + dy - 1)
            ht = (ht[0], ht[1] + dy - 1)                      # the paw's pad row; its bottom row is the ground's
            up_paw, down_paw = ("lift_h", "ground_h") if PAW_HEAVY else ("hang", "ground")
            foreleg(fhip, ff, ramp, up_paw if f_up else down_paw)
            hock = (ht[0] + 3.0, ht[1] - (4.5 if h_up else 3.5) - HOCK_UP)
            hindleg(hhip, hock, ht, ramp, up_paw if h_up else down_paw)
            return
        if legs_mode == "reach":                              # landing: both pairs stretched down for the ground
            foreleg(fhip, (34 + dx, 66 + bob + dy), ramp, "hang")
            hindleg(hhip, (67 + dx, 63 + bob + dy), (64 + dx, 67 + bob + dy), ramp, "hang")
            return
        if legs_mode == "push":                               # taking off: the hind legs straight down off the ground
            foreleg(fhip, (33 + dx, 60 + bob + dy), ramp, "hang")
            hindleg(hhip, (69 + dx, 61 + bob + dy), (67 + dx, min(GROUND - 1, 66 + bob + dy)), ramp, "ground")
            return
        # flying (the reference): the forearms hanging, the paws' claws down and forward; the hind leg's thigh forward
        # and down, the foot trailing back, its claws pointing back; raised wings = the legs stretched back, the
        # downstroke = drawn up under the body
        if beat is not None or glide is not None:
            # In the beat the legs are HELD, tucked (the flying reference: across its three wing poses the forelegs stay
            # curled under the chest and the hind legs folded under the belly; the body carries them). Drawn up and
            # reaching again on every beat they paddled: "he looks like he's swimming, like how dogs swim" (the user,
            # 2026-10-23). A "hanging loose" trail (each leg following the body's rise at its own lag) moved all four
            # 3-4 px on the same frames, the shared body term swamping the lags ("all legs are still bobbing exactly
            # the same"); held still they were dead ("all now completely static????"). So each leg has its own small
            # loop in the airflow, not a share of the body's rise: the forelegs mostly up and down (a curl), the hind
            # mostly back and forth (trailing), each at its own moment spread unevenly round the beat (no two in step,
            # no pair opposite: alternating strokes are a paddle again). LEG_SWAY px (0 = still).
            ph = {(False, "f"): 0.0, (True, "f"): 0.3, (False, "h"): 0.55, (True, "h"): 0.8}
            if far:
                # the far pair further back and up than on the ground (5, -2 from the near, not 3, -1): 3 px apart,
                # their claws met as the legs swayed (a four-claw bar, blobs) and the near legs hid the far ones' motion
                dx, dy = dx + 2, dy - 1
                fhip, hhip = (fhip[0] + 2, fhip[1] - 1), (hhip[0] + 2, hhip[1] - 1)

            def loops(lb, a):
                u = 2 * math.pi * (lb - ph[(far, "f")])
                v = 2 * math.pi * (lb - ph[(far, "h")])
                return (0.5 * a * math.cos(u), a * math.sin(u), a * math.sin(v), 0.5 * a * math.cos(v))
            # gliding the same loops, smaller (no beat stirs the air); a way in or out blends the two
            bl = loops(beat, LEG_SWAY) if beat is not None else None
            gl = loops(glide, LEG_SWAY * 0.6) if glide is not None else None
            fdx, fdy, hdx, hdy = [blend(None if bl is None else bl[i], None if gl is None else gl[i], M) for i in range(4)]
            if ARM:
                fdx, fdy, hdx, hdy = (float(round(v)) for v in (fdx, fdy, hdx, hdy))   # whole pixels
            foreleg(fhip, (34.5 + dx + fdx, 63 + bob + dy + fdy), ramp, "hang")
            hock = lp((74, 62), (70, 63), 0.55)
            toe = lp((81, 66), (66, 67), 0.55)
            hindleg(hhip, (hock[0] + dx + hdx * 0.5, hock[1] + bob + dy + hdy * 0.5), (toe[0] + dx + hdx, toe[1] + bob + dy + hdy), ramp, "trail")
            return
        ff = lp((32, 67), (35, 62), gb)
        foreleg(fhip, (ff[0] + dx, ff[1] + bob + dy), ramp, "hang")
        hock = lp((74, 62), (70, 63), gb)
        toe = lp((81, 66), (66, 67), gb)
        hindleg(hhip, (hock[0] + dx, hock[1] + bob + dy), (toe[0] + dx, toe[1] + bob + dy), ramp, "trail")

    legs(far=True)

    # ---- the tail: thick at the hips, tapering steadily, trailing low to the ground and swaying, a flat spade at the
    # end (the references: a long tail lying low behind the dragon with spines along its top; ours is as long as the
    # 128 canvas allows, the drawing's last column being 111). The spines sit on the tail's DRAWN top edge, found per
    # column after the fill: placed from the curve's radius they floated a row above it and read as notches with a
    # hole under each (the user's blue box, 2026-10-20). A near-even thickness with a kink up into the spade read as a
    # sausage (the same day).
    sway = (1.5 * math.sin(2 * math.pi * phase) if walking else idle)
    if ARM:
        sway = float(round(sway))
    if fly is not None:
        # flying: the tail streams back nearly level behind the hips, a slow wave down its length (the reference). Its
        # last points 1, 1.5 and 3 px shorter than first drawn (91, 98.5, 103): the spike reached column 128-129 of the
        # 128 picture and was cut off at its edge in every flying frame (measured on a 160 copy, 2026-10-23); now it ends
        # at 126, the walking tail's margin
        # the tail swings with the beat: straight out, the tip a little up with the wings raised; curving down and in
        # on the downstroke (the reference's first pose). In the beat the tail is a whip: each point follows the
        # body's rise a little later than the one before and swings further, the tip overshooting (a wave running
        # down the tail, the reference's tail middle and tip on their own lags); the curl down at each point's low
        # (gw at lag - 0.4: the stroke's bottom is th 0.4)
        if beat is None and glide is None:
            wv = 1.2 * math.sin(2 * math.pi * phase)
            tpts = [(70, 50 + bob), (82, 54 + bob + wv * 0.3 + 2.5 * gb), (91 - 1.5 * gb, 57.5 + bob + wv * 0.7 + 5 * gb),
                    (98.5 - 3 * gb, 60 + bob + wv + 7 * gb), (103 - 4.5 * gb, 61.5 + bob + wv * 1.2 + 9 * gb)]
        else:
            lags, amps = (0.06, 0.14, 0.22, 0.30), (1.0, 1.4, 1.9, 2.5)
            c = [gw(l - 0.4) * 0.3 for l in lags]
            w_ = [follow(l, a) for l, a in zip(lags, amps)]
            tpts = [(70, 50 + bob), (82, 54 + bob + w_[0] + 2.5 * c[0]), (91 - 1.5 * c[1], 57.5 + bob + w_[1] + 5 * c[1]),
                    (98.5 - 3 * c[2], 60 + bob + w_[2] + 7 * c[2]), (103 - 4.5 * c[3], 61.5 + bob + w_[3] + 9 * c[3])]
            if pitch:
                # climbing the tail trails lower, diving it rises with the tip curling up most (PITCH_TAIL above)
                tshare = (0.0, 0.15, 0.4, 0.7, 1.0)
                curl = 1.0 if pitch > 0 else 1.3      # a dive's tip curls up more than a climb's droops
                tpts = [(x, y + PITCH_TAIL * pitch * f * curl) for (x, y), f in zip(tpts, tshare)]
    else:
        tpts = [(70, 50 + bob), (81, 53 + bob * 0.7 + sway * 0.3), (91, 57 + sway * 0.7), (99, 59.5 + sway), (103, 60.5 + sway * 1.2)]   # the tip's lowest row is the feet's
        if walking and (TAIL_WAVE or TAIL_IN):
            # a wave down the tail with each step (the reference: its middle and tip lift on their own lags, the tip most,
            # and swing to the side): each point lifts its share of TAIL_WAVE, TAIL_LAG of a cycle after the one before;
            # only up from the old line, so the tip's lowest row stays the feet's
            base = [(70, 50 + bob), (81, 53 + bob * 0.7), (91, 57.0), (99, 59.5), (103, 60.5)]
            tpts = [base[0]]
            for i, f in enumerate((0.2, 0.45, 0.75, 1.0), start=1):
                lag = TAIL_LAG * i
                up = TAIL_WAVE * f * (0.5 + 0.5 * math.sin(4 * math.pi * (phase - 0.1 - lag)))
                inn = TAIL_IN * f * (0.5 + 0.5 * math.sin(4 * math.pi * (phase - 0.35 - lag)))
                tpts.append((base[i][0] - inn, base[i][1] - up))
    if ARM:
        tpts = [(float(round(x)), float(round(y))) for x, y in tpts]             # whole pixels: the tail's rings stay put
    tail = catmull(tpts, 160)
    tail_top = {}
    tail_len = sum(math.dist(a, b) for a, b in zip(tail, tail[1:]))
    arc = 0.0
    for i, (x, y) in enumerate(tail):
        u = i / (len(tail) - 1)
        arc += math.dist(tail[i - 1], (x, y)) if i else 0.0
        r = 6.0 * (1 - u) ** 1.4 + 1.0
        band = False
        # the plates under the tail end at a share of its samples; the Armored Dragon's at a fixed length along it, under
        # its last ring (26 to 28.6 px): ended by the share, the bending walk tail moved their end along it and the brown
        # under the tip came and went, 4 to 9 px a frame (the user, 2026-11-08: walk 04 and 08)
        under = arc < 27.3 if ARM else u < 0.75
        for yy in range(int(y - r - 1), int(y + r + 2)):
            for xx in range(int(x - r - 1), int(x + r + 2)):
                d = math.hypot(xx + 0.5 - x, yy + 0.5 - y)
                if d <= r:
                    up = (y - (yy + 0.5)) / max(r, 0.6)
                    if band:
                        put(xx, yy, STEEL[6] if up > 0.4 else STEEL[4] if up > -0.3 else STEEL[2])
                    elif up < -0.35 and under:
                        put(xx, yy, DBELLY[1] if int(xx) % 4 else DBELLY[0])     # the plates under the tail
                    else:
                        put(xx, yy, DRG[4] if up > 0.5 else DRG[3] if up > -0.05 else DRG[2])
                    if xx not in tail_top or yy < tail_top[xx]:
                        tail_top[xx] = yy
    for xx in range(74, 103, 4):                                                 # the spines along the tail's top, as the ridge's
        if xx in tail_top and xx + 1 in tail_top:
            h = 3 if xx < 86 else 2
            top = min(tail_top[xx], tail_top[xx + 1])
            if ARM:
                continue                                                         # the Armored Dragon's tail: bands, no spines
            for k in range(h):
                put(xx, top - 1 - k, DHORN[1 if k < h - 1 else 2])
                put(xx + 1, top - 1 - k, DHORN[0])
    tx, ty = tail[-1]
    if ARM:
        # the tail's end a spiked steel mace: a ball, five spikes round it (up, back, down and between)
        mx_, my_ = tx - 0.5, ty - 0.5
        for yy in range(int(my_) - 3, int(my_) + 4):
            for xx in range(int(mx_) - 3, int(mx_) + 4):
                dd = math.hypot(xx + 0.5 - mx_, yy + 0.5 - my_)
                if dd <= 2.6:
                    lit = ((xx + 0.5 - mx_) * L[0] + (yy + 0.5 - my_) * L[1]) / 2.6
                    put(xx, yy, STEEL[6] if lit > 0.35 else STEEL[4] if lit > -0.3 else STEEL[2])
        for ddx, ddy in ((-0.2, -1.0), (0.9, -0.45), (0.7, 0.7)):
            spike((mx_ + ddx * 2.6, my_ + ddy * 2.6), (ddx, ddy), 3, wide=False)
    else:
        for k, half in enumerate((2, 2, 1, 1, 1, 0, 0, 0)):                     # the tip: a spike, the tail tapering on to a point (a diamond
            for oy in range(-half, half + 1):                                    # spade read as a hanging lantern: the user, 2026-10-20)
                put(tx + 1 + k * 0.7, ty - 1 + oy, DRG[2] if abs(oy) >= half else DRG[3])

    # ---- the body, neck and head as one shape: a deep chest, a dipped back on the seat line, big haunches, a thick
    # neck arching up (down when grazing), a big skull with a long jaw
    hd = head_down
    crinet_ref = None                                                            # (Armored) the neck's resting shape and the head's whole-pixel move from it
    neck = [(34.0, 44.0 + bob), lp((27, 36 + bob), (30, 50), hd), lp((22, 27 + bob), (23, 57), hd), lp((17, 22 + bob), (16, 59), hd)]
    if tb is not None:
        # flying: the neck stretched forward, the head ahead of the chest (the reference), not arched up; the head a
        # little higher with the wings raised, lower and the neck shorter on the downstroke
        neck = [(34.0, 44.0 + bob), lp((26, 39 + bob), (27, 42 + bob), gb), lp((19, 34 + bob), (21, 41 + bob), gb), lp((12, 31 + bob), (14, 41 + bob), gb)]

        def shorter(pts):
            # FLY_NECK of each point's way from the neck's base (the chest), the base itself where it was
            bx, by = pts[0]
            return [pts[0]] + [(bx + (x - bx) * FLY_NECK, by + (y - by) * FLY_NECK) for x, y in pts[1:]]
        neck = shorter(neck)
        if beat is not None or glide is not None:
            # in the beat the neck does not dip with the wings (the old 10 px nod was most of the "dance"): it holds
            # the raised line, a little lower as the stroke passes it, and the head rides a little steadier than the
            # body and a moment behind it. Riding much steadier (0.8 of the body's rise, a quarter beat late) the head
            # moved 3.9 px up and down against the body every beat on the long neck: the "dance like an egyptian" (the
            # user, 2026-10-23; traced from this function). Now 0.95 of it, a tenth of a beat late: 1.6 px.
            n1, n2, n3 = gw(0.08) * 0.08, gw(0.14) * 0.08, gw(0.2) * 0.08
            neck = [(34.0, 44.0 + bob), (26 + n1, 39 + bob + 3 * n1 + follow(0.04, 0.99)),
                    (19 + 2 * n2, 34 + bob + 7 * n2 + follow(0.07, 0.97)), (12 + 2 * n3, 31 + bob + 10 * n3 + follow(0.1, 0.95))]
            if glide is not None:
                # the glide's nod (GLIDE_HEAD_PX above), its share of the frame's glide (the way in and out: M)
                neck = [(x, y + M * GLIDE_HEAD_PX * f * math.sin(2 * math.pi * (glide - 0.3 - 0.05 * j)))
                        for j, ((x, y), f) in enumerate(zip(neck, (0.0, 0.35, 0.7, 1.0)))]
            neck = shorter(neck)
            if pitch:
                # climbing the neck arches up and a little back, the head high; diving it reaches forward and down
                # (PITCH_NECK above): the base of the neck moves least, the head most
                share = (0.0, 0.35, 0.7, 1.0)
                back = 0.25 if pitch > 0 else 0.35   # px of x per px of lift: an arch back up, a reach forward down
                neck = [(x + (back * PITCH_NECK * pitch * f if pitch > 0 else back * PITCH_NECK * pitch * f), y - PITCH_NECK * pitch * f)
                        for (x, y), f in zip(neck, share)]
            if ARM:
                # the neck and head as one piece (as walking): the neck's resting shape in this clip (the beat's neck
                # without its beat terms, pitched as the clip is), every point past the root moved by the head's whole-
                # pixel move. Each point following the beat at its own lag, rounded on its own, bent the neck by a px
                # differently every frame and the lames along it shimmered.
                rest_ = shorter([(34.0, 44.0 + bob), (26.0, 39.0 + bob), (19.0, 34.0 + bob), (12.0, 31.0 + bob)])
                if pitch:
                    back = 0.25 if pitch > 0 else 0.35
                    rest_ = [(x + back * PITCH_NECK * pitch * f, y - PITCH_NECK * pitch * f) for (x, y), f in zip(rest_, (0.0, 0.35, 0.7, 1.0))]
                rest_ = [(float(round(x)), float(round(y))) for x, y in rest_]
                d_ = (round(neck[-1][0] - rest_[-1][0]), round(neck[-1][1] - rest_[-1][1]))
                neck = [neck[0]] + [(x + d_[0], y + d_[1]) for x, y in rest_[1:]]
                crinet_ref = (rest_, d_)
    if walking and (WALK_NOD or HEAD_STEADY or HEAD_FOLLOW or HEAD_FWD):
        # the head dips with each step, the neck's base least (twice a cycle, a little after the body's bob); HEAD_STEADY
        # takes the body's bob off it the same way (a heavy head carried level)
        def nod_at(lag):
            return (WALK_NOD * math.sin(4 * math.pi * (phase - 0.04)) - HEAD_STEADY * 0.8 * math.sin(4 * math.pi * phase)
                    + HEAD_FOLLOW * 0.8 * math.sin(4 * math.pi * (phase - lag)))

        def fwd_at(lag):
            return HEAD_FWD * math.sin(4 * math.pi * (phase - lag - 0.125))
        shares = (0.0, 0.35, 0.7, 1.0)
        lags = [HEAD_LAG * f if NECK_WAVE else HEAD_LAG for f in shares]
        neck = [(x - fwd_at(l) * f, y + nod_at(l) * f) for (x, y), f, l in zip(neck, shares, lags)]
        if ARM:
            # the neck and head as one piece: every point past the root takes the head's own move, rounded (only the
            # root's first span stretches), so the crinet's lames keep their pixels
            base_ = [(x + fwd_at(l) * f, y - nod_at(l) * f) for (x, y), f, l in zip(neck, shares, lags)]
            # the neck and head ride the body's bob, no move of their own: holding its height while the body bobbed under
            # it, the head and its one-piece neck slid against the chest (the user, 2026-11-07: "not attached")
            mx_, my_ = 0, 0
            neck = [base_[0]] + [(bx_ + mx_, by_ + my_) for (bx_, by_) in base_[1:]]
            crinet_ref = ([(float(round(x)), float(round(y))) for x, y in base_], (mx_, my_))
    if ARM:
        neck = [(float(round(x)), float(round(y))) for x, y in neck]             # whole pixels: the helm and the lames stay put
    hx, hy = neck[-1]

    def sdf(x, y, parts=None):
        hb = gb if (beat is None and glide is None) else gw(0.06) * 0.45            # in the beat the hump is the stroke's muscle: smaller, just after it
        lift_ = (1.0 - gw(0.0)) * (1.0 if tb is not None else 0.0)
        if ARM:
            lift_, hb = float(round(lift_)), round(hb * 4) / 4.0                  # whole pixels (the hump in quarter steps)
        chest = _ell(x, y, 39, 48.5 + bob + heave_f - lift_, 11.5, 9.0)
        back = _ell(x, y, 52, 50.0 + bob, 11.0, 8.6)
        hips = _ell(x, y, 63, 49.0 + bob + heave_h * HIPS_ROCK - 3.5 * hb, 11.0, 9.2 + 2.2 * hb)   # flying: humped behind the saddle on the downstroke
        hshift_ = 2.0 * (gb if (beat is None and glide is None) else gw(0.12) * 0.5)
        if ARM:
            hshift_ = float(round(hshift_))
        haunch = _ell(x, y, 66 - hshift_, 54.0 + bob + heave_h, 8.0, 6.0)
        if FORE_HEAVY and legs_mode == "walk":
            # the shoulder: a muscle mass over the foreleg as the haunch is over the hind (the reference's big shoulders)
            haunch = _smin(haunch, _ell(x, y, 39.5, 54.0 + bob + heave_f, 6.5, 5.5), 3.0)
        d = _smin(chest, back, 5.0)
        d = _smin(d, hips, 5.0)
        d = _smin(d, haunch, 4.0)
        nk = 1e9
        for (ax, ay), (bx, by), r in zip(neck, neck[1:], (6.2, 5.0, 4.2)):
            sg = _seg(x, y, ax, ay, bx, by) - r
            nk = min(nk, sg)
            d = _smin(d, sg, 3.5)
        skull = _ell(x, y, hx + 1, hy - 1, 6.5, 5.0)
        jaw = _ell(x, y, hx - 5, hy + 1.8, 7.0, 2.8)
        if ARM and ARM_HEAD.startswith("wedge"):                                 # long and flat: a brute's head
            skull = _ell(x, y, hx + 1, hy - 0.5, 7.0, 4.2)
            jaw = _ell(x, y, hx - 6.5, hy + (2.6 if ARM_HEAD == "wedge-snarl" else 1.6), 8.5, 2.5)   # snarl: the jaw dropped
        elif ARM and ARM_HEAD.startswith("greathelm"):                           # a taller skull under a closed helm
            skull = _ell(x, y, hx + 0.5, hy - 1.5, 7.0, 5.8)
            jaw = _ell(x, y, hx - 5, hy + 1.5, 7.0, 3.2)
        d = _smin(d, skull, 3.0)
        d = _smin(d, jaw, 2.5)
        if parts is not None:
            parts.update(chest=chest, back=back, hips=min(hips, haunch), neck=nk, skull=min(skull, jaw))
        return d

    plate_of = {}
    neck_len = sum(math.dist(a, b) for a, b in zip(neck, neck[1:]))

    def neck_s(x, y):
        # how far along the neck from the head a point lies (its nearest point on the neck's line)
        best, acc, s_ = None, 0.0, 0.0
        for (ax, ay), (bx, by) in zip(neck, neck[1:]):
            ln = math.dist((ax, ay), (bx, by)) or 1e-6
            tt = max(0.0, min(1.0, ((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / (ln * ln)))
            dd = math.hypot(x - ax - (bx - ax) * tt, y - ay - (by - ay) * tt)
            if best is None or dd < best:
                best, s_ = dd, acc + tt * ln
            acc += ln
        return neck_len - s_

    part_top = {}

    def armour_at(x, y, nx, ny, light, scales):
        # The Armored Dragon at a body pixel. The fill runs top to bottom, so the first pixel of a part in a column is
        # that part's top there. The parts:
        # - a helm over the skull and the snout (the jaw left in scales);
        # - a crinet of lames down the neck, dark hide between them, the throat left in scales;
        # - on the body, banded plates over its upper rows, a brass rim, a red skirt and hide below (armour_seams).
        return scales                                                            # the plates are pieces over it (armour_pieces)

    armour_px = set()

    def plate(poly, up=(0.0, -1.0), rivets=False, ridge=False, rim=False, gold=False):
        # One piece of plate over the scales: its own flat metal shading by its row from its top edge (along up): a
        # lit edge, a light row, the flat face, a darker band and a dark bevel; its own steel line where it meets the
        # hide (where it meets the air the dragon's outline does it); a shadow on the hide under it; brass rivets on
        # its second row, a lit ridge down its middle, or a brass rim on its bottom row.
        ul = math.hypot(*up) or 1.0
        up = (up[0] / ul, up[1] / ul)
        # the corners on a 1/64 px grid: a cell centre exactly on an edge is then decided the same way on every row
        # (in floating point 25 - 9.6 and 26 - 9.6 differ, and the crest gained a cell with the head one row lower)
        poly = [(round(q[0] * 64) / 64.0, round(q[1] * 64) / 64.0) for q in poly]
        xs, ys = [q[0] for q in poly], [q[1] for q in poly]
        cells = {(x, y) for y in range(int(min(ys)) - 1, int(max(ys)) + 2) for x in range(int(min(xs)) - 1, int(max(xs)) + 2)
                 if _in_poly(x + 0.5, y + 0.5, poly)}
        if not cells:
            return
        v = {c: -(c[0] * up[0] + c[1] * up[1]) for c in cells}                  # how far down the piece a cell is
        top, bot = min(v.values()), max(v.values())
        span = max(1.0, bot - top)
        cx_ = sum(c[0] for c in cells) / len(cells)
        cy_ = sum(c[1] for c in cells) / len(cells)
        ox_, oy_ = min(c[0] for c in cells), min(c[1] for c in cells)          # the plate's own corner: its rivets ride with it
        for (x, y) in cells:                                                     # the shadow it casts on the hide below it
            q = (int(round(x - up[0])), int(round(y - up[1])))
            if q not in cells and q not in armour_px and get(*q)[3]:
                put(q[0], q[1], DRG[0])
        for (x, y) in cells:
            r = v[(x, y)] - top
            nb = [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
            meets = any(q not in cells and get(*q)[3] for q in nb)
            if gold:
                # a gold piece (the helm's crest): its lit top, mid gold, the dark gold where it meets the helm
                col = BRASS[3] if r < 1.0 else BRASS[1] if meets else BRASS[2]
            elif rim and r > span - 1.0:
                col = BRASS[2]                                                   # its gold rim, even where it meets the hide (2026-11-07)
            elif meets and r > 0.6:
                col = STEEL_LINE
            elif r < 1.0:
                col = STEEL[6]
            elif r < 2.0:
                col = STEEL[5]
            elif r > span - 1.0:
                col = BRASS[2] if rim else STEEL[2]                              # one gold: an alternating rim shimmered as the plate moved
            elif r > span - 2.2:
                col = STEEL[3]
            else:
                col = STEEL[4]
            if rivets and 2.0 <= r < 3.0 and (x - ox_ + y - oy_) % 3 == 0:
                col = BRASS[2]
            if ridge and abs((x + 0.5 - cx_) * up[1] - (y + 0.5 - cy_) * up[0]) < 0.6 and 1.0 <= r <= span - 1.5:
                col = STEEL[6]
            put(x, y, col)
        armour_px.update(cells)

    def armour_pieces():
        # The Armored Dragon's plate, on the skeleton the body was drawn from: the crupper and the flank's two lames
        # (the lower first, the upper over it), the chest plate, the neck's lames (from its base up, each nearer the
        # head over the one behind it). The helm is drawn with the head; the tail's rings first (the tail is drawn).
        tail_rings()
        hb_ = gb if (beat is None and glide is None) else gw(0.06) * 0.45
        hipy = 49.0 + bob + heave_h * HIPS_ROCK - round(3.5 * round(hb_ * 4) / 4.0)
        chx, chy = 39.0, 48.5 + bob + heave_f - round((1.0 - gw(0.0)) * (1.0 if tb is not None else 0.0))
        plate([(65, hipy - 9.4), (72, hipy - 8.6), (76.5, hipy - 4), (77, hipy + 2.5), (73, hipy + 6.5), (66, hipy + 5.5)],
              up=(0.3, -1.0), rivets=True, rim=True)                             # the crupper over the hips
        plate([(44, 47.5 + bob), (66, 47 + bob), (68, 53.5 + bob), (45, 54 + bob)], up=(0, -1), rim=True)   # the flank's lower lame
        plate([(43, 42.5 + bob), (55, 41.6 + bob), (67, 42 + bob), (67.5, 48 + bob), (44, 48.5 + bob)], up=(0, -1), rivets=True)
        plate([(chx - 12.6, chy - 4), (chx - 7, chy - 8.8), (chx + 2, chy - 8.2), (chx + 3.5, chy + 1), (chx - 2, chy + 6.6),
               (chx - 9, chy + 5.2), (chx - 12.6, chy + 1.2)], up=(-0.35, -1.0), ridge=True, rim=True)   # the chest plate
        # the crinet: lames down the neck from behind the skull, a px proud of its back, across to its middle. Always
        # four, 5.5 px apart on the ground and 4.4 in the air (the flying neck is shorter), closer only on a neck too
        # short for them (takeoff's and landing's in-between frames): counted
        # from each frame's neck length the lowest came and went as the walk's bob stretched the neck's root (the user,
        # 2026-11-07: "the flashing armor part between frames"). Walking they are laid out on the neck before the head's
        # move and then moved with it, one piece with the head, by its whole pixels.
        ref_, off_ = crinet_ref if crinet_ref is not None else (neck, (0, 0))
        segs = list(zip(ref_, ref_[1:]))
        radii = (6.2, 5.0, 4.2)
        total = sum(math.dist(a, b) for a, b in segs)
        n_lames = 4
        step = min(4.4 if tb is not None else 5.5, (total - 6.0) / (n_lames - 1))
        lames = []
        for k in range(n_lames):
            s = 3.0 + step * k
            acc = 0.0
            for i in range(len(segs) - 1, -1, -1):                               # from the head end down
                a, b = segs[i]
                ln = math.dist(a, b) or 1e-6
                if acc + ln >= s:
                    f = 1.0 - (s - acc) / ln
                    P = (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f)
                    T = ((a[0] - b[0]) / ln, (a[1] - b[1]) / ln)                  # down the neck, towards the body
                    rr = radii[i]
                    break
                acc += ln
            P = (P[0] + off_[0], P[1] + off_[1])                                 # walking: with the head's move
            Nb = (T[1], -T[0])                                                   # the neck's back (away from the throat)
            if Nb[1] > 0 or (Nb[1] == 0 and Nb[0] < 0):
                Nb = (-Nb[0], -Nb[1])
            out_, in_ = rr + 1.0, rr * 0.15
            q0 = (P[0] + Nb[0] * out_, P[1] + Nb[1] * out_)
            q3 = (P[0] - Nb[0] * in_, P[1] - Nb[1] * in_)
            lames.append(([q0, (q0[0] + T[0] * 6.5, q0[1] + T[1] * 6.5), (q3[0] + T[0] * 6.5, q3[1] + T[1] * 6.5), q3], (-T[0], -T[1])))
        for poly, up_ in reversed(lames):                                        # the base first: each nearer the head lies over
            plate(poly, up=up_, rim=True)                                       # a gold rim on each (2026-11-07)

    def tail_rings():
        # steel rings round the tail, a px proud of it above and below: always four, 8, 14, 20 and 26 px along the tail
        # from its root, each its own size. (Counted while they fitted in three quarters of each frame's tail length
        # and placed at a share of it, a ring came and went in flight and they all slid as the sway changed the length.)
        segs_ = list(zip(tail, tail[1:]))
        rest_dirs = None
        if fly is None:
            # the resting tail (no sway): its direction at each ring's place, for the rings' fixed angles
            # without the walk's bob: with it the rounded root points stepped a px up or down twice a cycle and the rings
            # took three angles through the walk, their dark edges jumping (the user, 2026-11-08: walk 04 and 08)
            rp = [(70.0, 50.0 + bob_rest), (81.0, 53.0 + bob_rest * 0.7), (91.0, 57.0), (99.0, 59.5), (103.0, 60.5)]
            rc = catmull([(float(round(x)), float(round(y))) for x, y in rp], 160)
            rest_dirs, acc_ = {}, 0.0
            rsegs = list(zip(rc, rc[1:]))
            for s_ in (8.0, 14.0, 20.0, 26.0):
                acc_ = 0.0
                for (ax_, ay_), (bx_, by_) in rsegs:
                    ln_ = math.dist((ax_, ay_), (bx_, by_))
                    if acc_ + ln_ >= s_ or (bx_, by_) == rc[-1]:
                        break
                    acc_ += ln_
                ln_ = ln_ or 1e-6
                rest_dirs[s_] = ((bx_ - ax_) / ln_, (by_ - ay_) / ln_)
        for s in (8.0, 14.0, 20.0, 26.0):
            acc = 0.0
            for (ax, ay), (bx, by) in segs_:
                ln = math.dist((ax, ay), (bx, by))
                if acc + ln >= s or (bx, by) == tail[-1]:
                    break
                acc += ln
            ln = ln or 1e-6
            f = min(1.0, max(0.0, (s - acc) / ln))
            T = ((bx - ax) / ln, (by - ay) / ln)
            if rest_dirs is not None:
                # on the ground a stamp: the resting tail's direction at this ring, the same in every standing and
                # walking frame (snapped to steps, the walk's sway flipped a ring between two steps back and forth)
                T = rest_dirs[s]
            N = (-T[1], T[0])
            if N[1] > 0:
                N = (-N[0], -N[1])
            u = s / 37.5                                                         # its size by its place on the standing tail
            rr = 6.0 * (1 - u) ** 1.4 + 1.0 + 1.0
            P = (float(rnd(ax + (bx - ax) * f)), float(rnd(ay + (by - ay) * f)))   # on a whole pixel
            plate([(P[0] + N[0] * rr, P[1] + N[1] * rr), (P[0] + N[0] * rr + T[0] * 2.6, P[1] + N[1] * rr + T[1] * 2.6),
                   (P[0] - N[0] * rr + T[0] * 2.6, P[1] - N[1] * rr + T[1] * 2.6), (P[0] - N[0] * rr, P[1] - N[1] * rr)], up=(T[0], T[1]))

    def armour_seams(tops):
        # The body plates by their row from the part's top, as the Armored donkey's: a lit top row, a light row, a row
        # of rivets, the plate a step darker below, its last row the steel line; then a brass rim, the red skirt with
        # folds and a dagged hem, and the hide below. The chest plate is deeper (no skirt over the forelegs). Where two
        # body plates meet, a px of dark hide between them.
        for (x, y), (part, light) in list(plate_of.items()):
            if part in ("helm", "neck"):
                continue
            r = y - part_top[(part, x)]
            plate_rows = 9 if part == "chest" else 5
            if r == 0:
                col = STEEL[6]
            elif r == 1:
                col = STEEL[5]
            elif r == 2:
                col = STEEL[6] if x % 3 == 0 else STEEL[4]                       # the rivets
            elif r < plate_rows - 1:
                col = STEEL[4] if light > 0.45 else STEEL[3]
            elif r == plate_rows - 1:
                col = STEEL_LINE
            elif part != "chest" and r == plate_rows:
                col = BRASS[2] if x % 2 else BRASS[1]
            elif part != "chest" and r <= plate_rows + 4:
                col = CLOTH[1] if x % 3 == 0 else CLOTH[3] if r == plate_rows + 1 else CLOTH[2]
            elif part != "chest" and r == plate_rows + 5 and x % 4 in (0, 1):
                col = CLOTH[0]                                                   # the dagged hem
            else:
                continue                                                         # the hide (the scales as drawn)
            put(x, y, col)
        for (x, y), (part, light) in list(plate_of.items()):
            if part in ("helm", "neck"):
                continue
            r = y - part_top[(part, x)]
            if r < (9 if part == "chest" else 5):
                other = plate_of.get((x + 1, y))
                if other is not None and other[0] != part and other[0] not in ("helm", "neck"):
                    put(x, y, DRG[1])                                            # hide between two plates

    tops = {}
    for y in range(-OY, H - OY):
        for x in range(-OX, W - OX):
            d = sdf(x + 0.5, y + 0.5)
            if d > 0:
                continue
            gx = sdf(x + 1.0, y + 0.5) - sdf(x, y + 0.5)
            gy = sdf(x + 0.5, y + 1.0) - sdf(x + 0.5, y)
            g = math.hypot(gx, gy) or 1.0
            nx, ny = gx / g, gy / g
            light = 0.5 + 0.42 * (nx * L[0] + ny * L[1])
            if -d < 1.6 and ny > 0.2:
                light -= 0.18                                                   # a darker edge under the belly and jaw
            belly = ny > 0.38 and 28 < x < 74 and y > 47
            if belly:
                col = _tone(DBELLY, light + 0.15)
                if y % 4 == 0:
                    col = DBELLY[max(0, DBELLY.index(col) - 1)]                 # the plates' seams (every 3 rows read as planks)
            else:
                col = _tone(DRG, light)
                if ARM:
                    col = armour_at(x, y, nx, ny, light, col)
            put(x, y, col)
            if x not in tops or y < tops[x]:
                tops[x] = y
    if ARM:
        armour_pieces()

    # ---- the ridge of spines from behind the skull to the hips (none under the saddle)
    for x in range(int(hx) + 5, 72, 4):
        if x in tops and not (45 <= x <= 66):
            h = 3 if x < 36 else 2
            if ARM:
                continue                                                         # the Armored Dragon's blades: at fixed columns, below
            for k in range(h):
                put(x, tops[x] - 1 - k, DHORN[1 if k < h - 1 else 2])
                put(x + 1, tops[x] - 1 - k, DHORN[0])

    if ARM:
        # the ridge's blades at fixed columns of the body (two on the neck's root, one behind the saddle), each on the
        # column's drawn top: they ride with the body and never jump along it
        for x in (30, 38, 70):
            if x in tops:
                spike((x, tops[x] - 1), (0.5, -1.0), 5 if x < 45 else 4)

    # ---- the head: two long horns swept back, a brow, the eye, a nostril, teeth along the jaw
    def horn(points, r0, far_):
        # a black horn along a curve, thick at its root, ridged, its lit side a step lighter (the far one darker)
        curve = catmull(points, 60)
        for i, (cx_, cy_) in enumerate(curve):
            u = i / (len(curve) - 1)
            rr = r0 * (1 - u) + 0.4
            ring_ = (u * 26) % 3.2 < 0.8
            for yy in range(int(cy_ - rr) - 1, int(cy_ + rr) + 2):
                for xx in range(int(cx_ - rr) - 1, int(cx_ + rr) + 2):
                    if math.hypot(xx + 0.5 - cx_, yy + 0.5 - cy_) <= rr:
                        lit = ((xx + 0.5 - cx_) * L[0] + (yy + 0.5 - cy_) * L[1]) / max(rr, 0.5)
                        put(xx, yy, DHORN[1] if (far_ or ring_) else (DHORN[2] if lit > 0.15 else DHORN[1]))

    if ARM and ARM_HEAD == "beak":
        # the war helm's skull face: a beak hooked down past the snout's tip, a dark socket round the eye, a crown of
        # three blades fanning back off the helm, a black horn curling back under them (the far one behind)
        HP = lambda dx, dy: (hx + dx, hy + dy)
        horn([HP(4, -3), HP(10, -3.5), HP(14, -1), HP(13.5, 3), HP(10.5, 4.5)], 1.5, False)
        plate([HP(-14.8, 3.6), HP(-13.6, -0.6), HP(-8, -5.2), HP(0.5, -7.8), HP(6.5, -7.2), HP(8.6, -3.2), HP(5.5, 0.6), HP(-3, 0.4),
               HP(-9, 1.0), HP(-12.2, 1.6)], up=(0.15, -1.0), rivets=True)
        for dx in (-2, -1, 0):
            put(hx + dx - 1, hy - 3, STEEL_LINE)
            put(hx + dx - 1, hy - 2, STEEL_LINE)
        for d_, n_ in (((0.7, -1.0), 6), ((1.0, -0.45), 7), ((1.0, 0.05), 5)):
            spike(HP(7.5, -4.5), d_, n_)
    elif ARM and ARM_HEAD == "greathelm":
        # a closed knight's helm over the whole head: a flat front, the visor's slit across it with breaths under it, a
        # crest along its top, a brass rim; two black horns out of its sides, up and back
        HP = lambda dx, dy: (hx + dx, hy + dy)
        horn([HP(3, -5), HP(6, -9), HP(11, -12), HP(16, -12.5)], 1.4, True)
        plate([HP(-12.6, -2.6), HP(-12.6, 3.0), HP(-7, 3.6), HP(5, 3.0), HP(8.4, -0.8), HP(7.4, -6.2), HP(1, -8.4), HP(-6, -7.4),
               HP(-11, -5.2)], up=(0.0, -1.0))                                   # no brass rim: its bottom row flickered with the head
        for dx in range(-12, -1):                                                # the slit
            put(hx + dx, hy - 2, STEEL_LINE)
        for dx in (-10, -8, -6):                                                 # the breaths
            put(hx + dx, hy + 0.5, STEEL_LINE)
        plate([HP(-8, -7.2), HP(1, -9.6), HP(6, -8.6), HP(6, -7.4), HP(1, -8.2), HP(-8, -6.4)], up=(0.0, -1.0), gold=True)   # the crest, gold
        horn([HP(2, -5), HP(5, -9.5), HP(10, -12.5), HP(15, -13.5)], 1.4, False)
    elif ARM and ARM_HEAD == "greathelm-cross":
        # the game knight's helm (9019_hed_static_left): a rounded dome over the whole head, a gold band down its front
        # and one across the brow above the slit, a mint glint on the dome, two black horns out of its sides
        HP = lambda dx, dy: (hx + dx, hy + dy)
        horn([HP(4, -5), HP(7, -9), HP(12, -11), HP(17, -11)], 1.4, True)
        plate([HP(-12.6, -2.0), HP(-12.6, 3.0), HP(-7, 3.6), HP(5, 3.0), HP(8.4, -0.8), HP(8.0, -5.4), HP(3, -8.8), HP(-4, -9.0),
               HP(-9.5, -6.8), HP(-12.0, -4.4)], up=(0.0, -1.0), rim=True)
        for dy in range(-4, 4):                                                  # the gold band down the front
            put(hx - 11.5, hy + dy, BRASS[2] if dy < 0 else BRASS[1])
        for dx in range(-11, 7):                                                 # and across the brow
            if (int(round(hx + dx)), int(round(hy - 3.5))) in armour_px:
                put(hx + dx, hy - 3.5, BRASS[3] if dx < -4 else BRASS[2])
        for dx in range(-11, -1):                                                # the slit under it
            put(hx + dx, hy - 2, STEEL_LINE)
        put(hx - 3, hy - 7, STEEL[6])                                            # the glint
        put(hx - 2, hy - 7, STEEL[6])
        put(hx - 3, hy - 6, STEEL[5])
        horn([HP(3, -5), HP(6, -9.5), HP(11, -11.5), HP(16, -12)], 1.4, False)
    elif ARM and ARM_HEAD == "greathelm-bull":
        # a boxy bucket helm, flat-topped, a grille of slots in its face, rivets; big black bull horns out of its sides,
        # curving up and forward
        HP = lambda dx, dy: (hx + dx, hy + dy)
        horn([HP(4, -5), HP(5, -10), HP(2, -13.5), HP(-2, -15)], 1.5, True)
        plate([HP(-12.8, -6.0), HP(-12.8, 3.2), HP(6, 3.4), HP(8.4, -0.6), HP(7.6, -6.6), HP(1, -7.4)], up=(0.0, -1.0), rivets=True, rim=True)
        for dx in (-11, -9, -7, -5):                                             # the grille
            for dy in (-3, -2, -1, 0, 1):
                put(hx + dx, hy + dy, STEEL_LINE)
        put(hx - 9, hy - 2, DEYE)                                                # the ember eye behind it
        horn([HP(3, -5), HP(3.5, -10.5), HP(0, -14), HP(-4.5, -15.5)], 1.6, False)
    elif ARM and ARM_HEAD == "greathelm-sallet":
        # a rounded sallet whose tail flares back over the neck, the visor's slit, a gold band, a blade standing off
        # its crown and raked back; the jaw under it in scales
        HP = lambda dx, dy: (hx + dx, hy + dy)
        plate([HP(-12.8, -1.4), HP(-11, -5.6), HP(-5, -8.6), HP(2, -8.8), HP(8, -6.6), HP(14, -3.0), HP(12.5, -1.4), HP(8, -1.2),
               HP(4, 1.2), HP(-3, 1.0), HP(-9, 1.6), HP(-12.8, 1.6)], up=(0.1, -1.0), rim=True)
        for dx in range(-12, -2):
            put(hx + dx, hy - 2, STEEL_LINE)
        for dx in range(-10, 10):
            if (int(round(hx + dx)), int(round(hy - 4.2))) in armour_px:
                put(hx + dx, hy - 4.2, BRASS[2])
        spike(HP(1, -9), (0.55, -1.0), 6)
        put(hx - 4, hy - 7, STEEL[6])
    elif ARM and ARM_HEAD.startswith("wedge"):
        # a long flat head: a narrow plate down the snout and over the brow with a ridge of spikes along it, fangs out
        # of the jaw, two long black horns swept back almost flat (and per variant: a jaw guard and a crest, the jaw
        # dropped in a snarl with horns curling down, or a horn on the nose)
        HP = lambda dx, dy: (hx + dx, hy + dy)
        V = ARM_HEAD
        if V == "wedge-snarl":
            horn([HP(4, -3), HP(10, -4.5), HP(13.5, -1.5), HP(12, 3), HP(8.5, 4.5)], 1.6, False)   # curling down and forward
            plate([HP(-15, -0.4), HP(-8, -3.6), HP(1, -5.2), HP(7, -4.6), HP(8.6, -1.6), HP(3, -0.6), HP(-15, 0.8)], up=(0.1, -1.0), ridge=True)
            for dx in range(-14, -1):                                            # the open mouth
                if get(hx + dx, hy + 2.5)[3]:
                    put(hx + dx, hy + 2.5, DOUT[0])
            for dx in (-14, -11, -8, -5):                                        # fangs down from the top
                put(hx + dx, hy + 2.5, (212, 198, 168))
            for dx in (-12.5, -9.5, -6.5):                                       # and up from the jaw
                put(hx + dx, hy + 3.5, (212, 198, 168))
        else:
            if V == "wedge-jaw":
                horn([HP(3, -5), HP(8, -6.5), HP(13, -7.5)], 1.3, True)
            else:
                horn([HP(3, -5), HP(9, -8), HP(15, -10.5), HP(20, -12)], 1.3, True)
            plate([HP(-15, -0.4), HP(-8, -3.6), HP(1, -5.2), HP(7, -4.6), HP(8.6, -1.6), HP(3, -0.6), HP(-15, 0.8)], up=(0.1, -1.0), ridge=True)
            if V == "wedge-jaw":
                plate([HP(-14, 2.2), HP(-6, 2.0), HP(2, 2.6), HP(3.6, 4.2), HP(-6, 4.6), HP(-13, 3.8)], up=(0.0, -1.0), rim=True)   # the jaw guard
                for dx in (-6, -2, 2, 6):                                        # a crest of spikes along the skull
                    spike(HP(dx, -5.2 + abs(dx) * 0.05), (0.3, -1.0), 4)
                horn([HP(4, -3), HP(9, -4.5), HP(14, -5.5)], 1.5, False)
            else:
                for dx in (-9, -4, 1):
                    spike(HP(dx, -4.6 + dx * 0.05), (0.35, -1.0), 3, wide=False)
                if V == "wedge-rhino":
                    horn([HP(-11.5, -1.0), HP(-13, -4.5), HP(-12.5, -8), HP(-10.5, -10)], 1.4, False)   # the nose horn, curving back
                horn([HP(4, -3), HP(10, -5), HP(16, -6.5), HP(22, -7)], 1.5, False)
    elif False:
        HP = lambda dx, dy: (hx + dx, hy + dy)
        horn([HP(3, -5), HP(9, -8), HP(15, -10.5), HP(20, -12)], 1.3, True)
        plate([HP(-15, -0.4), HP(-8, -3.6), HP(1, -5.2), HP(7, -4.6), HP(8.6, -1.6), HP(3, -0.6), HP(-15, 0.8)], up=(0.1, -1.0), ridge=True)
        for dx in (-9, -4, 1):
            spike(HP(dx, -4.6 + dx * 0.05), (0.35, -1.0), 3, wide=False)
        horn([HP(4, -3), HP(10, -5), HP(16, -6.5), HP(22, -7)], 1.5, False)
    if False:
        for (ox_, oy_), far_ in ():
            hp = []
            curve = catmull(hp, 60)
            for i, (cx_, cy_) in enumerate(curve):
                u = i / (len(curve) - 1)
                rr = 1.3 * (1 - u) + 0.4
                ring_ = (u * 26) % 3.2 < 0.8                                    # the horn's ridges
                for yy in range(int(cy_ - rr) - 1, int(cy_ + rr) + 2):
                    for xx in range(int(cx_ - rr) - 1, int(cx_ + rr) + 2):
                        if math.hypot(xx + 0.5 - cx_, yy + 0.5 - cy_) <= rr:
                            lit = ((xx + 0.5 - cx_) * L[0] + (yy + 0.5 - cy_) * L[1]) / max(rr, 0.5)
                            put(xx, yy, DHORN[1] if far_ else DHORN[1] if ring_ else (DHORN[2] if lit > 0.15 else DHORN[1]))
    for dx0, dy0, dx1, dy1 in (() if ARM else ((4, -4, 16, -11), (2, -5, 12, -14))):
        ax, ay, bx, by = hx + dx0, hy + dy0 - hd * 2, hx + dx1, hy + dy1 - hd * 2
        for t in range(16):
            u = t / 15
            x, y = ax + (bx - ax) * u, ay + (by - ay) * u
            put(x, y, DHORN[2] if u < 0.5 else DHORN[1])
            if u < 0.7:
                put(x, y + 1, DHORN[0])
    ex, ey = hx - 1, hy - 2
    if ARM:
        closed = ARM_HEAD in ("greathelm", "greathelm-cross", "greathelm-bull", "greathelm-sallet")
        if not closed:
            for dx in range(-2, 2):                                             # the plate's eye hole, the ember eye in it
                put(ex + dx, ey, STEEL_LINE)
        if ARM_HEAD != "greathelm-bull":
            put(ex - 1, ey, DEYE)
            put(ex, ey, DEYE)
        if ARM_HEAD in ("wedge", "wedge-jaw", "wedge-rhino"):
            for dx in (-13, -10, -7):                                           # fangs out of the jaw
                put(hx + dx, hy + 3, DHORN[2])
                put(hx + dx, hy + 4, (212, 198, 168))
    else:
        for dx, dy, c in ((-1, 0, DEYE), (0, 0, DEYE), (-1, -1, DRG[0]), (0, -1, DRG[0]), (1, -1, DRG[0]), (0, 1, (30, 10, 10))):
            put(ex + dx, ey + dy, c)
    helmed = ARM and ARM_HEAD in ("greathelm", "greathelm-cross", "greathelm-bull")   # a closed helm hides the nostril and the mouth
    if not helmed:
        put(hx - 11, hy + 1, DRG[0])                                             # the nostril
    for dx in (range(-11, 3) if not helmed and ARM_HEAD != "wedge-snarl" else ()):  # the mouth line
        if get(hx + dx, hy + 3)[3] and get(hx + dx, hy + 2)[3]:
            put(hx + dx, hy + 3, DRG[0])

    legs(far=False)
    if ARM:
        # the greaves: a plate over each near forearm and shin, from just under the joint to above the paw, a px wider
        # than the leg
        for a, b, r0, r1 in greave_segs:
            ln = math.dist(a, b) or 1e-6
            T = ((b[0] - a[0]) / ln, (b[1] - a[1]) / ln)
            N = (-T[1], T[0])
            w0, w1 = r0 * LEG_BULK + 0.6, (r1 + (r0 - r1) * LEG_WRIST) * LEG_BULK + 0.6
            # a fixed length centred on the bone, so a foreshortened forearm (the swing) keeps its greave (it was the
            # bone less 2 px: nothing on a short one, the panel the user saw vanish in walk 06)
            if ln < 1.0:
                T = (0.0, 1.0)
                N = (-1.0, 0.0)
            # a stamp: the bone's direction snapped to 22.5-degree steps and the middle on a whole pixel, so the swing
            # shows a few fixed greaves, not one re-laid every frame
            a_ = math.atan2(T[1], T[0])
            a_ = math.radians(22.5 * round(math.degrees(a_) / 22.5))
            T = (math.cos(a_), math.sin(a_))
            N = (-T[1], T[0])
            mid_ = (float(rnd((a[0] + b[0]) / 2)), float(rnd((a[1] + b[1]) / 2)))
            half_ = 2.6
            A = (mid_[0] - T[0] * half_, mid_[1] - T[1] * half_)
            B = (mid_[0] + T[0] * half_, mid_[1] + T[1] * half_)
            plate([(A[0] + N[0] * w0, A[1] + N[1] * w0), (B[0] + N[0] * w1, B[1] + N[1] * w1),
                   (B[0] - N[0] * w1, B[1] - N[1] * w1), (A[0] - N[0] * w0, A[1] - N[1] * w0)], up=(0.0, -1.0))   # lit from above
            # (no brass rim: on a small plate turning with the leg its bottom row came and went, gold blinking)

    if fly is None:
        folded_wing(flap, far=False)
    else:
        if not NO_SPREAD_WINGS:
            spread_wing(True, fly)

    # ---- a pitched frame: a pixel the column shift could leave alone (nothing round it in either layer, two neighbouring
    # columns moved by different whole pixels across a thin edge) is dropped. A safeguard: the first build had none (the
    # one check_skin.py flagged was the thumb claw over the body, a gap in the checker, fixed there)
    if pitch:
        lone = []
        for y in range(H):
            for x in range(W):
                i = y * W + x
                if not (px[i][3] or fpx[i][3]):
                    continue
                if not any(0 <= x + dx < W and 0 <= y + dy < H and (px[(y + dy) * W + x + dx][3] or fpx[(y + dy) * W + x + dx][3])
                           for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
                    lone.append(i)
        for i in lone:
            px[i] = fpx[i] = (0, 0, 0, 0)

    # ---- the Armored Dragon: a scrap of picture not joined to the dragon (a sliver a notch or a cut leaves at a tip) is
    # dropped, under 12 px; a part of its own (a far horn's tip, a blade) is bigger and joined anyway
    if ARM:
        filled = {i for i in range(W * H) if px[i][3] or fpx[i][3]}
        seen = set()
        for i0 in filled:
            if i0 in seen:
                continue
            comp, stack = [], [i0]
            seen.add(i0)
            while stack:
                i = stack.pop()
                comp.append(i)
                x, y = i % W, i // W
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        if 0 <= x + dx < W and 0 <= y + dy < H:
                            j = (y + dy) * W + x + dx
                            if j in filled and j not in seen:
                                seen.add(j)
                                stack.append(j)
            if len(comp) < 12:
                for i in comp:
                    px[i] = fpx[i] = (0, 0, 0, 0)

    # ---- the outline, dark, round the union of both layers (no line along the cut between them); an outline pixel
    # goes to the front layer when it borders a front pixel, else to the back
    back, front = list(px), list(fpx)
    for y in range(H):
        for x in range(W):
            if px[y * W + x][3] or fpx[y * W + x][3] or (y * W + x) in see_through:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx2, ny2 = x + dx, y + dy
                if 0 <= nx2 < W and 0 <= ny2 < H and (px[ny2 * W + nx2][3] or fpx[ny2 * W + nx2][3]):
                    col = (DOUT[0] if dy == 1 else DOUT[1]) + (255,)
                    (front if fpx[ny2 * W + nx2][3] else back)[y * W + x] = col
                    break
    if ARM:
        # a pocket of grass shut in between the near wing and the body (its edge across the tail rings' tops at the top
        # of the upstroke) is a shadow under the wing: the darkest hide, behind. Only one the front layer borders, of
        # 40 px or less (the gaps between claws and legs are the drawing's own)
        seen = set()
        for i0 in range(W * H):
            if back[i0][3] or front[i0][3] or i0 in seen or i0 in see_through:
                continue
            comp, stack, edge, by_wing = [], [i0], False, False
            seen.add(i0)
            while stack:
                i = stack.pop()
                comp.append(i)
                x, y = i % W, i // W
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx2, ny2 = x + dx, y + dy
                    if not (0 <= nx2 < W and 0 <= ny2 < H):
                        edge = True
                        continue
                    j = ny2 * W + nx2
                    if back[j][3] or front[j][3]:
                        by_wing = by_wing or bool(front[j][3])
                    elif j not in seen:
                        seen.add(j)
                        stack.append(j)
            if not edge and by_wing and len(comp) <= 40:
                for i in comp:
                    if i not in see_through:                                     # a see-through hole stays a hole
                        back[i] = DRG[0] + (255,)
    return back, front


dragon.flies = True  # its frames include flying, taking off and landing (frames_for)


def dragon_blue(phase, head_down=0.0, idle=0.0):
    return dragon(phase, head_down, idle, BLUE_DRAGON)


def dragon_armored(phase, head_down=0.0, idle=0.0, **kw):
    return dragon(phase, head_down, idle, ARMORED_DRAGON, style="armored", **kw)


dragon_armored.flies = True


# ---------------------------------------------------------------- the frames

def frames_for(fn):
    out = {}
    # a creature drawn in two layers returns (back, front): the front pictures are drawn in front of the rider
    plain = fn

    def fn(*a, **k):
        r = plain(*a, **k)
        return r if isinstance(r, tuple) else (r, None)

    for k in range(1, 9):
        out[f"bdy_walk_left_{k:02d}"] = fn((k - 1) / 8)
    out["bdy_static_left"] = fn(0.0)
    for k in range(1, 6):
        out[f"bdy_ear_left_{k:02d}"] = fn(0.0, idle=0.6 * math.sin(k / 5 * math.pi))
    for k in range(1, 14):
        out[f"bdy_lips_left_{k:02d}"] = fn(0.0, idle=0.5 * math.sin(k / 13 * 2 * math.pi))
    for k in range(1, 4):
        out[f"bdy_chew_left_{k:02d}"] = fn(0.0, head_down=0.7, idle=0.4 * (k % 2))
    for k in range(1, 6):
        out[f"bdy_graze_left_{k:02d}"] = fn(0.0, head_down=min(1.0, k / 3))
    for k in range(1, 7):
        out[f"bdy_turn_left_{k:02d}"] = fn(0.0, idle=0.8 * math.sin(k / 6 * math.pi))
    if getattr(plain, "flies", False):
        # flying (docs/flying.md, "the frames"): not frames the game shows; the mod plays them while the mount flies.
        # The wing beat: raised, level, swept down, level (the reference's three poses, our own drawing), eight
        # moments of the beat, each part at its own lag ("the beat in time" above). Take-off: a crouch with the wings opening, the leap (hind legs
        # pushing, wings high), the first downstroke. Landing: the wings flared high, the legs reaching down, the
        # touch (the body down on its feet, the wings level), the wings folding.
        # No lift drawn into a frame: the seat is a fixed row of the picture, the rider would come off the back; the
        # flight moves the whole picture (the crouch, the climb, the thud: Flight.cs).
        # Eight moments of the beat (th = 0, 1/8, ..), each part at its own lag (wing_w, "the beat in time" above)
        for k in range(1, BEAT_FRAMES + 1):
            th = (k - 1) / BEAT_FRAMES
            out[f"bdy_fly_left_{k:02d}"] = fn(th, head_down=0.0, fly=wing_w(th), legs_mode="tuck", beat=th)
        # Climbing and diving: the beat pitched, half and full (PITCH_SHEAR above)
        for tag, pt in (("flyup1", 0.5), ("flyup2", 1.0), ("flydown1", -0.5), ("flydown2", -1.0)):
            for k in range(1, BEAT_FRAMES + 1):
                th = (k - 1) / BEAT_FRAMES
                out[f"bdy_{tag}_left_{k:02d}"] = fn(th, head_down=0.0, fly=wing_w(th), legs_mode="tuck", beat=th, pitch=pt)
        # The glide: the wings held open at GLIDE_T, one slow loop of the rest (the user, 2026-10-23: "a glide animation
        # where the wings are just open while they are gliding"; Flight.cs plays it now and then, flying along)
        for k in range(1, GLIDE_FRAMES + 1):
            g = (k - 1) / GLIDE_FRAMES
            out[f"bdy_glide_left_{k:02d}"] = fn(0.0, head_down=0.0, fly=GLIDE_T, legs_mode="tuck", glide=g)
        # (the in-between frames pass fly=GLIDE_T: in flight gb is read only where there is no beat, and tb only as
        # "flying", so the beat's share of each part comes from beat= alone)
        # the way in: from the beat's frame 7 (th 0.75, the wings coming up through the glide's pose) on through the top
        # of the stroke, each frame a fifth more of the glide, the glide's loop running up to its frame 1
        for k in range(1, GLIDE_IN + 1):
            out[f"bdy_glidein_left_{k:02d}"] = fn(0.0, head_down=0.0, fly=GLIDE_T, legs_mode="tuck",
                                                  beat=0.75 + (k - 1) / BEAT_FRAMES, glide=(k - 1 - GLIDE_IN) / GLIDE_FRAMES, mix=(k / (GLIDE_IN + 1), 1))
        # the way out, from each quarter of the glide's loop (the game waits for the next one): a downstroke from th
        # 0.25, each frame a fifth less of the glide, handing over to the beat's frame 7 (th 0.75)
        for q in range(4):
            for k in range(1, GLIDE_OUT + 1):
                out[f"bdy_glideout{q + 1}_left_{k:02d}"] = fn(0.0, head_down=0.0, fly=GLIDE_T, legs_mode="tuck",
                                                               beat=0.25 + (k - 1) / BEAT_FRAMES, glide=q / 4 + (k - 1) / GLIDE_FRAMES, mix=(1 - k / (GLIDE_OUT + 1), -1))
        takeoff = ((None, "walk"), (0.0, "push"), (0.15, "push"), (0.8, "tuck"))
        for k, (ang, mode) in enumerate(takeoff, 1):
            out[f"bdy_takeoff_left_{k:02d}"] = fn(0.0, head_down=0.1 + 0.1 * k, idle=0.8 if ang is None else 0.0, fly=ang, legs_mode=mode)
        land = ((0.1, "reach"), (0.0, "reach"), (0.45, "walk"), (None, "walk"))
        for k, (ang, mode) in enumerate(land, 1):
            out[f"bdy_land_left_{k:02d}"] = fn(0.0, head_down=0.4 - 0.1 * k, idle=0.8 if ang is None else 0.0, fly=ang, legs_mode=mode)
    empty = [(0, 0, 0, 0)] * (W * H)
    for name in ("hed_static_down", "hed_static_down_02", "hed_static_down_03"):
        out[name] = (empty, None)
    flat = {}
    for key, (back, front) in out.items():
        flat[key] = back
        if front is not None:
            flat["front/" + key] = front
    return flat


# name, drawing function, title, canvas (96: the donkey's; 128: the picture's centre stays the donkey's centre, so a
# creature drawn in the donkey's coordinates sits 16 px in from each edge)
# name, drawing function, title, canvas, the mount (id, name) the skin is a look of (docs/mounts-design.md)
# (the Blue Dragon, dragon_blue above, was dropped from the shipped set 2026-10-19: the player's colours do that)
# name, drawing function, title, canvas, the mount (id, name), the seat row: the canvas row (from the top) the
# rider's hip sits on for every player (docs/seat-investigation.md: 46 is the donkey's own seat on the 96 canvas, so a
# creature drawn to the donkey's measures says 46; the 128 canvas adds 16; the snail sits 3 px lower on its shell)
# ... and the hitch gap: how many px further behind the body the whole cart sits with the cart hitched ([Tuning.Cart]
# HitchGapPx; the donkey's is 0; None = none: the mount pulls no cart (the dragon, 2026-10-25), or not measured yet (the
# "cart readout" self-test says what it is))
CREATURES = (("worm", worm, "Worm", 96, ("worm", "Worm"), 52, None), ("snail", snail, "Snail", 96, ("snail", "Snail"), 55, None),
             ("rat", rat, "Rat", 96, ("rat", "Rat"), 52, 9),
             ("dragon", dragon, "Red Dragon", 128, ("dragon", "Dragon"), 68 - RAISE, None),  # seat rows: the donkey's 52 (+16 on 128), the snail 3 lower
             ("armored-dragon", dragon_armored, "Mecha", 128, ("dragon", "Dragon"), 68 - RAISE, None))
# Creatures drawn only when named (python -I make_creatures.py <name> <folder>): none now (the Armored Dragon ships in
# the Dragons pack since 2026-11-07). A creature that is a variant of a mount is written to the mount's skins folder
# SKIN_DIR names (never its default).
ONLY_WHEN_ASKED = set()
SKIN_DIR = {"armored-dragon": "armored"}

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # tools/skins/creatures/make_creatures.py -> the repo
only = sys.argv[1] if len(sys.argv) > 1 else None     # one creature's name, to redraw just that one
custom = len(sys.argv) > 2                            # a folder of its own: the pictures flat in <folder>\<name>\ (the previews)
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import skinroots  # noqa: E402
# each creature's pack and its id there (2026-10-26: the mounts are external packs, their ids by the pack: packs/,
# tools/skins/skinroots.py); the folder is the id
HOMES = {"dragon": ("GK2SaddleUpDragonsMountPack", "saddleup-dragon.dragon"), "rat": ("GK2SaddleUpCrittersMountPack", "saddleup-critters.rat"),
         "snail": ("GK2SaddleUpCrittersMountPack", "saddleup-critters.snail"), "worm": ("GK2SaddleUpCrittersMountPack", "saddleup-critters.worm")}
if only and only not in {c[0] for c in CREATURES}:
    sys.exit("no creature called " + only + "; one of " + ", ".join(c[0] for c in CREATURES))
FLYING = {"dragon"}  # the mounts whose [Mount] block says flies = true (written once, with the mount.cfg)
NO_CART = {"dragon"}  # the mounts whose [Mount] block says cart = false (the dragon pulls no cart: the user, 2026-10-25)
for name, fn, title, size, (mount, mount_name), seat_row, hitch_gap in CREATURES:
    if only and name != only:
        continue
    if not only and name in ONLY_WHEN_ASKED:
        continue
    W = H = size
    OX = OY = (size - 96) // 2
    fr = frames_for(fn)
    pack, mid = HOMES[mount]  # GK2SaddleUpDragonsMountPack, saddleup-dragon.dragon
    assets = sys.argv[2] if custom else skinroots.pack_root(pack)
    outs = [os.path.join(assets, name) if custom else os.path.join(assets, mid, "skins", SKIN_DIR.get(name, "default"))]  # each mount's own look: skins\default\
    if not custom and not os.path.isfile(os.path.join(assets, mid, "mount.cfg")):
        # the mount, written once (the Mounts tab saves its values here afterwards)
        mcfg = os.path.join(assets, mid, "mount.cfg")
        os.makedirs(os.path.dirname(mcfg), exist_ok=True)
        open(mcfg, "w", encoding="utf-8", newline="\r\n").write(
            "# A mount of the pack " + pack + ", an external pack of GK2 Saddle Up (packs/, docs/making-a-mount.md): what the\n"
            "# player rides, shared by every skin in skins\\.\n"
            "[Mount]\nneeds = 0.40\nid = " + mid + "\nname = " + mount_name + "\ndescription = A " + mount_name.lower() + " of the keeper's own: it "
            + ("has young and pulls no cart" if mount in NO_CART else "pulls the cart and has young") + " (edit me).\n"
            "author = dreas\nversion = 1.0\ncart = " + ("false" if mount in NO_CART else "true") + "\nfoals = true\n" + ("flies = true\n" if mount in FLYING else "")
            + "speed = 2  # riding, times the keeper's walking speed (the donkey: 2)\n" + ("" if mount in NO_CART else "cargo = 6  # items its cart carries, 1 to 6\n"))
        import canonical_cfg
        mtext = canonical_cfg.canonical_mount(mcfg)
        open(mcfg, "w", encoding="utf-8", newline="").write(mtext)
    for d in outs:
        os.makedirs(d, exist_ok=True)
        if any(k.startswith("front/") for k in fr):
            os.makedirs(os.path.join(d, "front"), exist_ok=True)
        for key, px in fr.items():
            write(os.path.join(d, key.replace("/", os.sep) + ".png"), px)
        if not os.path.isfile(os.path.join(d, "skin.cfg")):
            # written once, in the canonical layout (tools/skins/canonical_cfg.py; tools/skins/skin_colors.py adds [Colors]);
            # the tuning panel's Mounts tab saves the creature's values into this file afterwards
            open(os.path.join(d, "skin.cfg"), "w", encoding="utf-8", newline="\r\n").write(
                "# A skin of GK2 Saddle Up's " + mount + " mount (docs/skins.md): its look, the mount's own values in ..\\..\\mount.cfg. Drawn by tools/skins/creatures/make_creatures.py: our own art.\n"
                "[Skin]\nformat = 1\nname = " + title + "\ndescription = " + title + ", our own art drawn by code (edit me).\nauthor = dreas\nversion = 1.0\nbase = donkey\n"
                "colours = own\nlayer = replace\ncanvas = " + str(size) + " " + str(size) + "\nseat = " + str(seat_row)
                + "            # the rider's hip on this row of the picture, every frame, for every player (docs/seat-investigation.md)\n"
                + ("\n# With the cart only: the whole cart this many px further behind the body (the skin owns its drawing's geometry).\n[Tuning.Cart]\nHitchGapPx = " + str(hitch_gap) + "\n" if hitch_gap is not None else ""))
            import canonical_cfg  # byte for byte the layout the scans expect
            text = canonical_cfg.canonical(os.path.join(d, "skin.cfg"))
            open(os.path.join(d, "skin.cfg"), "w", encoding="utf-8", newline="").write(text)
    # the review pictures: out\<name>\strips\, preview.png, pack\sheet.png (the front layer composited, the rider on the seat row)
    pil = {k: review.from_flat(px, W, H) for k, px in fr.items() if not k.startswith("front/")}
    front = {k: review.from_flat(px, W, H) for k, px in fr.items() if k.startswith("front/")}
    rider = review.load_rider(os.path.join(game_dir(), "BepInEx", "GK2-asset-probe"))
    rider_dx, seat_adj = review.rider_tuning(os.path.join(outs[0], "skin.cfg"))  # the skin's own RiderOffsetPx / SeatAdjustPx (the snail's -6)
    strips = review.write_all(os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", name), pil, front=front or None, rider=rider, seat_row=seat_row, rider_dx=rider_dx, seat_adj=seat_adj)
    print(name, len(fr), "pictures ->", ", ".join(outs), "| strips ->", strips)
