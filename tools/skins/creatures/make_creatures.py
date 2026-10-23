"""Mounts drawn from nothing (docs/mount-looks-design.md): a giant worm, a giant snail, a giant rat and a dragon, as
the built-in skins src/GK2DonkeyAPI/assets/skins/<name>/ (the one pipeline, docs/sprite-tools.md "Making a skin": a
PNG per frame, skin.cfg kept as written by hand once it exists). Our own art, made by code: no game pixels.

Every donkey frame name gets a picture (96x96, facing left, the donkey's pivot in the middle), so the creature covers
every clip: the 8 walk frames are one crawl cycle; standing, ear, lips, chew, turn and graze are idle frames (graze puts
the head down). The donkey's head layer gets empty pictures, so "Instead of it" hides the donkey completely.
Fitted to the donkey's measures so the rider sits right without tuning: the back (the seat) at row 41 around columns
47-63, the ground at row 64, the front (where the cart's shafts and the harness are) around column 33.
    python tools/skins/creatures/make_creatures.py                 # every creature into the repo's assets\\skins\\<name>\\
    python tools/skins/creatures/make_creatures.py rat             # one creature
    python tools/skins/creatures/make_creatures.py rat <folder>    # into another folder (a test pack, never the game's plugins)
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
            px[y * W + x] = (60, 18, 26, 255)
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


def dragon(phase, head_down=0.0, idle=0.0, pal=RED_DRAGON, fly=None, legs_mode="walk", lift=0.0, beat=None, glide=None, mix=None, pitch=0.0):
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
    px = [(0, 0, 0, 0)] * (W * H)
    fpx = [(0, 0, 0, 0)] * (W * H)   # the front layer: drawn in front of the rider (the near wing)
    L = (-0.6, -0.8)

    def pitched(xx_world, y):
        # the column's own shift (climbing: forward of the seat up, behind it down), whole pixels for the whole column
        return y + (round(PITCH_SHEAR * pitch * (xx_world - PITCH_X)) if pitch else 0)

    def put(x, y, col, dst=None):
        cx = int(round(x))
        xx, yy = cx + OX, int(round(pitched(cx, y))) + OY
        if 0 <= xx < W and 0 <= yy < H:
            (dst if dst is not None else px)[yy * W + xx] = col + (255,)

    def get(x, y):
        cx = int(round(x))
        xx, yy = cx + OX, int(round(pitched(cx, y))) + OY
        return px[yy * W + xx] if 0 <= xx < W and 0 <= yy < H else (0, 0, 0, 0)

    def lp(a, b, t):
        return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)

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
        ring = [sh, el, wr] + tips + [root]
        lerp = lambda a, b, f: (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f)
        # The veins end along the trailing edge about 9.5 px apart, as the fold's finger tips meet its hem: every finger,
        # and as many between as the edge's length there takes. Each end is a point of the scalloped edge (the fold's hem
        # is scalloped between every pair of vein ends; three big scallops between the fingers alone read plain).
        trail = []
        for a_, b_ in zip(tips + [root], tips[1:] + [root]):
            k_ = max(1, int(round(math.dist(a_, b_) / 9.5)))
            trail += [lerp(a_, b_, j / k_) for j in range(k_)]
        trail.append(root)
        # and the inner part of the membrane, from the wing's root on the back to the shoulder, gets veins too (the
        # fold's fan covers its whole membrane down to the shoulder; spread, the triangle between the arm and the back
        # was one flat colour): ends along that line, as far apart, no scallops there (it is the body's edge)
        k_ = max(1, int(round(math.dist(root, sh) / 9.5)))
        inner = [lerp(root, sh, j / k_) for j in range(1, k_)]
        scal = []
        for a, b in zip(trail, trail[1:]):                        # the trailing edge's scallops, bulging into the membrane
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            vx, vy = mx - wr[0], my - wr[1]
            vl = math.hypot(vx, vy) or 1
            half = math.dist(a, b) / 2
            depth = half * 0.32
            k = (half * half - depth * depth) / (2 * depth)
            scal.append((mx + vx / vl * k, my + vy / vl * k, math.dist((mx + vx / vl * k, my + vy / vl * k), a)))

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
        xs = [q[0] for q in ring]
        ys = [q[1] for q in ring]
        far_d = max(math.dist(wr, q) for q in tips) or 1
        cells = {}
        for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
            for x in range(int(min(xs)) - 1, int(max(xs)) + 2):
                X, Y = x + 0.5, y + 0.5
                if not inside(X, Y) or any(math.hypot(X - cx, Y - cy) < r for cx, cy, r in scal):
                    continue
                dv, vl = min((_seg(X, Y, *a, *b), math.dist(a, b)) for a, b in veins)
                # how far out along its own vein (0 at the wrist, 1 at the edge), as the fold's light runs from its peak
                # to its hem: the dark bands beside the veins in their outer third, along every vein (measured against
                # the longest vein, only the farthest tip had them)
                u = min(1.0, math.dist((X, Y), wr) / max(1.0, vl))
                cells[(x, y)] = min(0.49, 0.62 - 0.36 * u) + (0.0 if dv > 2.0 else -0.14)   # the fold's light rule, in the fold's tones (its peak's lighter wine has no room on the fold; spread, it filled the wing near the wrist)
        for (x, y), light in cells.items():
            border = any((x + dx, y + dy) not in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            put(x, y, DOUT[1] if border else _tone(DWING, light), fpx if (near and not in_rider_at(x, y)) else px)
        for (ax, ay), (bx, by) in veins:                         # the veins: 1 px red lines over the membrane, as the fold's
            n = int(math.dist((ax, ay), (bx, by)) * 2) + 1
            for i in range(n + 1):
                x, y = ax + (bx - ax) * i / n, ay + (by - ay) * i / n
                if (int(round(x)), int(round(y))) in cells:
                    put(x, y, DRG[2], fpx if (near and not in_rider_at(int(round(x)), int(round(y)))) else px)
        for (ax, ay), (bx, by) in ((sh, el), (el, wr)):          # the arm, the leading edge: a thicker bone, the same red
            for y in range(int(min(ay, by)) - 3, int(max(ay, by)) + 4):
                for x in range(int(min(ax, bx)) - 3, int(max(ax, bx)) + 4):
                    if _seg(x + 0.5, y + 0.5, ax, ay, bx, by) <= 1.3:
                        put(x, y, DRG[2], fpx if (near and not in_rider_at(x, y)) else px)
        for k in range(4):                                       # the thumb claw at the wrist, hooking forward (behind his face too)
            cx_, cy_ = wr[0] - 1 - k, wr[1] - k * 0.5
            face = WING_OVER_RIDER == "head" and rider_in_view(int(round(cx_)) + OX, int(round(cy_)) + OY)
            put(cx_, cy_, DHORN[2 if k > 1 else 1], fpx if (near and not face) else px)

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
            return (x + ox, y + bob + oy)
        sh = at(46.0, 43.0)                                                       # the shoulder, where the wing leaves the back
        peak = at(52.0, 26.0)
        # the wing tip points back along the tail (the blue references: a long wedge from the wrist to past the hips,
        # the user's mark 2026-10-20: over the rump and the first third of the tail, never near the ground)
        tip = at(99.0, 51.0)
        hem = [at(93.0, 56.0), at(84.0, 58.5), at(74.0, 59.5), at(64.0, 60.5), at(55.0, 60.5), at(48.0, 58.0)]   # the lower edge along the belly line
        ring = [peak, at(66.0, 29.0), at(84.0, 38.0), tip] + hem + [at(47.0, 54.0), sh]
        tips = [at(84.0, 39.0), tip, at(88.0, 58.0), at(78.0, 59.5), at(68.0, 60.5), at(59.0, 61.0), at(51.0, 59.5)]
        fingers = [(peak, t) for t in tips] + [(peak, sh)]                      # the last: the membrane's leading edge down to the shoulder
        dst = px if far else fpx
        # the lower edge's scallops between the finger tips: shallow arcs bulging up into the membrane
        scal = []
        for a, b in zip(tips[1:-1], tips[2:]):
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            half = math.dist(a, b) / 2
            depth = half * 0.3
            k = (half * half - depth * depth) / (2 * depth)
            scal.append((mx, my + k, math.dist((mx, my + k), a)))

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
                if any(math.hypot(X - cx, Y - cy) < r for cx, cy, r in scal):
                    continue
                df = min(_seg(X, Y, *a, *b) for a, b in fingers)
                down = (Y - peak[1]) / max(1.0, max(ys) - peak[1])
                light = 0.62 - 0.36 * down + (0.0 if df > 2.0 else -0.14)       # lit at the peak, dark at the hem, dark by the bones
                cells[(x, y)] = light
        for (x, y), light in cells.items():
            border = any((x + dx, y + dy) not in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            put(x, y, DOUT[1] if border else _tone(DWING, light), dst)
        for (ax, ay), (bx, by) in fingers:                                       # the finger bones: red lines down the membrane
            n = int(math.dist((ax, ay), (bx, by)) * 2) + 1
            for i in range(n + 1):
                x, y = ax + (bx - ax) * i / n, ay + (by - ay) * i / n
                if (int(round(x)), int(round(y))) in cells:
                    put(x, y, DRG[2], dst)
        for k in range(5):                                                        # the thumb claw: a hook from the peak towards the head
            put(peak[0] - 1 - k * 0.9, peak[1] - 1 - k * 0.5 + (0.6 * (k - 2) ** 2 if k > 2 else 0), DHORN[2 if k > 1 else 1], dst)

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
        ax, ay = a
        bx, by = b
        Lg = math.dist(a, b) or 1e-6
        ux, uy = (bx - ax) / Lg, (by - ay) / Lg
        nx, ny = -uy, ux                                      # the side normal; lit where it faces the light (upper left)
        if nx * L[0] + ny * L[1] < 0:
            nx, ny = -nx, -ny
        R = max(r0, r1)
        for y in range(int(min(ay, by) - R) - 1, int(max(ay, by) + R) + 2):
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
    }

    def paw(at, kind, ramp, dst=None):
        (ox, oy), rows = PAWS[kind]
        cols = {"r": ramp[2], "R": ramp[1], "g": DBELLY[3], "G": DBELLY[2], "d": DBELLY[1]}
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

    STRIDE, LIFT, STANCE = 9.0, 4.0, 0.62

    def step_at(q, cx):
        # the foot's place over the cycle: planted and sliding back, then lifted and swung forward
        q %= 1.0
        if q < STANCE:
            return (cx - STRIDE / 2 + STRIDE * q / STANCE, GROUND), False   # planted: slides back towards the tail (+x) as the body walks on (it faces left)
        u = (q - STANCE) / (1 - STANCE)
        e = u * u * (3 - 2 * u)
        return (cx + STRIDE / 2 - STRIDE * e, GROUND - LIFT * math.sin(math.pi * u)), True   # lifted: swings forward to the head (-x); walked backwards the other way round (the user, 2026-10-23)

    def foreleg(hip, foot, ramp, paw_kind):
        elbow = ik(hip, foot, 6.5 + RAISE * 0.5, 6.5 + RAISE * 0.5, +1)
        bone(hip, elbow, 3.6, 2.6, ramp)
        bone(elbow, foot, 2.6, 1.8, ramp)
        paw(foot, paw_kind, ramp)

    def hindleg(hip, hock, toe, ramp, paw_kind):
        knee = ik(hip, hock, 7.5 + RAISE * 0.55, 6.0 + RAISE * 0.45, -1)
        bone(hip, knee, 5.2, 3.2, ramp)                       # the big thigh
        bone(knee, hock, 3.0, 2.0, ramp)
        bone(hock, toe, 2.0, 1.6, ramp)                       # the long foot up to the hock
        paw(toe, paw_kind, ramp)

    def legs(far):
        ramp = (DRG[0], DRG[0], DRG[1], DRG[2]) if far else (DRG[0], DRG[1], DRG[2], DRG[3])
        dx, dy = (3, -1) if far else (0, 0)
        fhip, hhip = (39 + dx, 53 + bob + dy), (66 + dx, 53 + bob + dy)
        fwd = (-1.0, 0.0)
        if legs_mode == "walk":
            qf = phase + (0.75 if far else 0.25)
            qh = phase + (0.5 if far else 0.0)
            if not walking:
                qf = qh = 0.31                                 # standing: each foot under its hip
            (ff, f_up), (ht, h_up) = step_at(qf, 36 + dx), step_at(qh, 66 + dx)
            ff = (ff[0], ff[1] + dy - 1)
            ht = (ht[0], ht[1] + dy - 1)                      # the paw's pad row; its bottom row is the ground's
            foreleg(fhip, ff, ramp, "hang" if f_up else "ground")
            hock = (ht[0] + 3.0, ht[1] - (4.5 if h_up else 3.5))
            hindleg(hhip, hock, ht, ramp, "hang" if h_up else "ground")
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
    tail = catmull(tpts, 160)
    tail_top = {}
    for i, (x, y) in enumerate(tail):
        u = i / (len(tail) - 1)
        r = 6.0 * (1 - u) ** 1.4 + 1.0
        for yy in range(int(y - r - 1), int(y + r + 2)):
            for xx in range(int(x - r - 1), int(x + r + 2)):
                d = math.hypot(xx + 0.5 - x, yy + 0.5 - y)
                if d <= r:
                    up = (y - (yy + 0.5)) / max(r, 0.6)
                    if up < -0.35 and u < 0.75:
                        put(xx, yy, DBELLY[1] if int(xx) % 4 else DBELLY[0])     # the plates under the tail
                    else:
                        put(xx, yy, DRG[4] if up > 0.5 else DRG[3] if up > -0.05 else DRG[2])
                    if xx not in tail_top or yy < tail_top[xx]:
                        tail_top[xx] = yy
    for xx in range(74, 103, 4):                                                 # the spines along the tail's top, as the ridge's
        if xx in tail_top and xx + 1 in tail_top:
            h = 3 if xx < 86 else 2
            top = min(tail_top[xx], tail_top[xx + 1])
            for k in range(h):
                put(xx, top - 1 - k, DHORN[1 if k < h - 1 else 2])
                put(xx + 1, top - 1 - k, DHORN[0])
    tx, ty = tail[-1]
    for k, half in enumerate((2, 2, 1, 1, 1, 0, 0, 0)):                         # the tip: a spike, the tail tapering on to a point (a diamond
        for oy in range(-half, half + 1):                                        # spade read as a hanging lantern: the user, 2026-10-20)
            put(tx + 1 + k * 0.7, ty - 1 + oy, DRG[2] if abs(oy) >= half else DRG[3])

    # ---- the body, neck and head as one shape: a deep chest, a dipped back on the seat line, big haunches, a thick
    # neck arching up (down when grazing), a big skull with a long jaw
    hd = head_down
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
    hx, hy = neck[-1]

    def sdf(x, y):
        hb = gb if (beat is None and glide is None) else gw(0.06) * 0.45            # in the beat the hump is the stroke's muscle: smaller, just after it
        chest = _ell(x, y, 39, 48.5 + bob - (1.0 - gw(0.0)) * (1.0 if tb is not None else 0.0), 11.5, 9.0)
        back = _ell(x, y, 52, 50.0 + bob, 11.0, 8.6)
        hips = _ell(x, y, 63, 49.0 + bob - 3.5 * hb, 11.0, 9.2 + 2.2 * hb)   # flying: humped behind the saddle on the downstroke
        haunch = _ell(x, y, 66 - 2.0 * (gb if (beat is None and glide is None) else gw(0.12) * 0.5), 54.0 + bob, 8.0, 6.0)
        d = _smin(chest, back, 5.0)
        d = _smin(d, hips, 5.0)
        d = _smin(d, haunch, 4.0)
        for (ax, ay), (bx, by), r in zip(neck, neck[1:], (6.2, 5.0, 4.2)):
            d = _smin(d, _seg(x, y, ax, ay, bx, by) - r, 3.5)
        skull = _ell(x, y, hx + 1, hy - 1, 6.5, 5.0)
        jaw = _ell(x, y, hx - 5, hy + 1.8, 7.0, 2.8)
        d = _smin(d, skull, 3.0)
        d = _smin(d, jaw, 2.5)
        return d

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
            put(x, y, col)
            if x not in tops or y < tops[x]:
                tops[x] = y

    # ---- the ridge of spines from behind the skull to the hips (none under the saddle)
    for x in range(int(hx) + 5, 72, 4):
        if x in tops and not (45 <= x <= 66):
            h = 3 if x < 36 else 2
            for k in range(h):
                put(x, tops[x] - 1 - k, DHORN[1 if k < h - 1 else 2])
                put(x + 1, tops[x] - 1 - k, DHORN[0])

    # ---- the head: two long horns swept back, a brow, the eye, a nostril, teeth along the jaw
    for dx0, dy0, dx1, dy1 in ((4, -4, 16, -11), (2, -5, 12, -14)):
        ax, ay, bx, by = hx + dx0, hy + dy0 - hd * 2, hx + dx1, hy + dy1 - hd * 2
        for t in range(16):
            u = t / 15
            x, y = ax + (bx - ax) * u, ay + (by - ay) * u
            put(x, y, DHORN[2] if u < 0.5 else DHORN[1])
            if u < 0.7:
                put(x, y + 1, DHORN[0])
    ex, ey = hx - 1, hy - 2
    for dx, dy, c in ((-1, 0, DEYE), (0, 0, DEYE), (-1, -1, DRG[0]), (0, -1, DRG[0]), (1, -1, DRG[0]), (0, 1, (30, 10, 10))):
        put(ex + dx, ey + dy, c)
    put(hx - 11, hy + 1, DRG[0])                                                 # the nostril
    for dx in range(-11, 3):                                                     # the mouth line
        if get(hx + dx, hy + 3)[3] and get(hx + dx, hy + 2)[3]:
            put(hx + dx, hy + 3, DRG[0])

    legs(far=False)

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

    # ---- the outline, dark, round the union of both layers (no line along the cut between them); an outline pixel
    # goes to the front layer when it borders a front pixel, else to the back
    back, front = list(px), list(fpx)
    for y in range(H):
        for x in range(W):
            if px[y * W + x][3] or fpx[y * W + x][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx2, ny2 = x + dx, y + dy
                if 0 <= nx2 < W and 0 <= ny2 < H and (px[ny2 * W + nx2][3] or fpx[ny2 * W + nx2][3]):
                    col = (DOUT[0] if dy == 1 else DOUT[1]) + (255,)
                    (front if fpx[ny2 * W + nx2][3] else back)[y * W + x] = col
                    break
    return back, front


dragon.flies = True  # its frames include flying, taking off and landing (frames_for)


def dragon_blue(phase, head_down=0.0, idle=0.0):
    return dragon(phase, head_down, idle, BLUE_DRAGON)


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
# HitchGapPx; the donkey's is 0; None = not measured yet: the "cart readout" self-test says what it is)
CREATURES = (("worm", worm, "Giant Worm", 96, ("worm", "Giant Worm"), 52, None), ("snail", snail, "Giant Snail", 96, ("snail", "Giant Snail"), 55, None),
             ("rat", rat, "Giant Rat", 96, ("rat", "Giant Rat"), 52, 9),
             ("dragon", dragon, "Red Dragon", 128, ("dragon", "Dragon"), 68 - RAISE, 24))  # seat rows: the donkey's 52 (+16 on 128), the snail 3 lower

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # tools/skins/creatures/make_creatures.py -> the repo
only = sys.argv[1] if len(sys.argv) > 1 else None     # one creature's name, to redraw just that one
assets = sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, "src", "SaddleUp", "assets", "skins")  # Saddle Up ships the five mounts (docs/mounts-as-mods.md)
if only and only not in {c[0] for c in CREATURES}:
    sys.exit("no creature called " + only + "; one of " + ", ".join(c[0] for c in CREATURES))
FLYING = {"dragon"}  # the mounts whose [Mount] block says flies = true (written once, with the skin.cfg)
for name, fn, title, size, (mount, mount_name), seat_row, hitch_gap in CREATURES:
    if only and name != only:
        continue
    W = H = size
    OX = OY = (size - 96) // 2
    fr = frames_for(fn)
    outs = [os.path.join(assets, name)]
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
                "# A built-in skin of GK2 Donkey API (docs/skins-design.md). Drawn by tools/skins/creatures/make_creatures.py: our own art.\n"
                "[Skin]\nformat = 1\nid = " + name + "\nname = " + title + "\ndescription = " + title + ", our own art drawn by code (edit me).\nauthor = dreas\nversion = 1.0\nbase = donkey\n"
                "colours = own\nlayer = replace\ncanvas = " + str(size) + " " + str(size) + "\nseat = " + str(seat_row)
                + "            # the rider's hip on this row of the picture, every frame, for every player (docs/seat-investigation.md)\nmount = " + mount + "\n"
                + ("\n# With the cart only: the whole cart this many px further behind the body (the skin owns its drawing's geometry).\n[Tuning.Cart]\nHitchGapPx = " + str(hitch_gap) + "\n" if hitch_gap is not None else "")
                + "\n# The mount this skin is the Default of (docs/mounts-design.md): what the player rides, shared by every skin of it.\n"
                "[Mount]\nid = " + mount + "\nname = " + mount_name + "\ndescription = A " + mount_name.lower() + " of the keeper's own: it pulls the cart and has young (edit me).\nauthor = dreas\nversion = 1.0\ncart = true\nfoals = true\n" + ("flies = true\n" if mount in FLYING else "") + "speed = 2  # riding, times the keeper's walking speed (the donkey: 2)\ncargo = 6  # items its cart carries, 1 to 6\n")
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
