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


def dragon(phase, head_down=0.0, idle=0.0, pal=RED_DRAGON):
    DRG, DWING, DBELLY, DHORN, DEYE, DOUT = pal
    walking = phase != 0.0
    bob = 0.8 * math.sin(4 * math.pi * phase) if walking else 0.0
    px = [(0, 0, 0, 0)] * (W * H)
    fpx = [(0, 0, 0, 0)] * (W * H)   # the front layer: drawn in front of the rider (the near wing)
    L = (-0.6, -0.8)

    def put(x, y, col, dst=None):
        xx, yy = int(round(x)) + OX, int(round(y)) + OY
        if 0 <= xx < W and 0 <= yy < H:
            (dst if dst is not None else px)[yy * W + xx] = col + (255,)

    def get(x, y):
        xx, yy = int(round(x)) + OX, int(round(y)) + OY
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
    def wing(near, flap):
        sh = (46.0 + (0 if near else 7), 42.0 + bob - (0 if near else 6))
        pts = {"elbow": (48, 26), "wrist": (54, 8), "root": (73, 38),
               "tips": [(84, 1), (97, 15), (99, 33), (90, 48)]}
        if not near:
            pts = {k: ([(x + 7, y - 10) for x, y in v] if k == "tips" else (v[0] + 7, v[1] - 10)) for k, v in pts.items()}
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
                put(x, y, _tone(ramp, light), fpx if (near and in_outer) else px)
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

    # ---- the wings, FOLDED, as the user's references draw them (2026-10-20; docs/mount-looks-design.md): the wrist is a
    # tall PEAK above the back, its thumb claw hooking towards the head; the fingers run DOWN from the peak; the membrane
    # hangs between them like a curtain to the ground BEHIND the hindquarters, scalloped along its bottom; the body and
    # its belly line stay in view. The near wing's peak stands behind the rider, its curtain in the front layer over
    # the tail root (the camera side); the arm from the shoulder up to the peak is drawn above the back, behind him.
    # The far wing is a shorter peak above the shoulder, its membrane a wedge behind the neck, all of it behind the
    # body. The membrane is wine against the body's red, outlined, the finger bones red lines.
    def folded_wing(flap, far):
        ang = flap * math.pi / 180 * 0.3                                         # the peak sways a little
        # one fold for both wings, THE REFERENCE'S SILHOUETTE (the user's sheets, 2026-10-20): the wrist is a tall peak
        # above the shoulder with the thumb claw hooking forward, the fingers run down and back from it, the membrane
        # hangs between them like a curtain from the peak back over the flank, its hem on the ground only behind the
        # hindquarters and rising forward along the belly line; nothing of the wing lies forward of the peak. The near
        # hind leg is under the curtain, which reaches the ground over it (drawn in front, its foot read as part of
        # the wing: the user, 2026-10-20). The whole near wing is the front layer, over the rider wherever
        # the two overlap (the user, 2026-10-20: the wing is nearer the camera than he is, no part of him may show
        # in front of it): he sits inside the fold with his face, the top of his head and the reins arm out of it,
        # chest and everything below under the membrane. (Tried and rejected the same day: a cut that kept his
        # chest in view by drawing the membrane behind him there, and a one-layer wing shaped around him, which
        # grew a lobe under his seat that stood out like a thumb when nobody rode.) The far wing is the same shape
        # a few pixels back and up (the perspective), drawn before the body, which hides all but its peak and the
        # top of its curtain.
        ox, oy = (4.0, -5.0) if far else (0.0, 0.0)

        def at(x, y):
            return (x + ox, y + bob + oy)
        sh = at(46.0, 43.0)                                                       # the shoulder, where the wing leaves the back
        peak = _rot(at(52.0, 26.0), sh, ang)
        hem = [at(85.0, 58.0), at(79.0, 66.5), at(62.0, 66.5), at(59.0, 63.0), at(52.0, 58.0)]   # the curtain's hem: on the ground over the hind legs
        ring = [peak, at(64.0, 30.0), at(78.0, 43.0)] + hem + [at(47.0, 55.0), sh]
        tips = [at(76.0, 45.0), at(79.0, 66.0), at(65.0, 66.0), at(59.0, 62.5), at(52.0, 58.0)]
        fingers = [(peak, t) for t in tips] + [(peak, sh)]                      # the last: the membrane's leading edge down to the shoulder

        def layer(x, y):
            return px if far else fpx                                             # the near wing: all of it in front of the rider
        # the hem's scallops between the finger tips: arcs bulging up into the membrane
        scal = []
        for a, b in zip(tips[1:-1], tips[2:]):
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            half = math.dist(a, b) / 2
            depth = half * 0.2                                                    # shallow: the finger tips stand a little out of the hem, the feet stay under it
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
                if Y < GROUND + bob and any(math.hypot(X - cx, Y - cy) < r for cx, cy, r in scal):   # no scallop at the ground: the feet stay under the hem
                    continue
                df = min(_seg(X, Y, *a, *b) for a, b in fingers)
                down = (Y - peak[1]) / max(1.0, max(ys) - peak[1])
                light = 0.62 - 0.36 * down + (0.0 if df > 2.0 else -0.14)       # lit at the peak, dark at the hem, dark by the bones
                cells[(x, y)] = light
        for (x, y), light in cells.items():
            border = any((x + dx, y + dy) not in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            put(x, y, DOUT[1] if border else _tone(DWING, light), layer(x, y))
        for (ax, ay), (bx, by) in fingers:                                       # the finger bones: red lines down the curtain
            n = int(math.dist((ax, ay), (bx, by)) * 2) + 1
            for i in range(n + 1):
                x, y = ax + (bx - ax) * i / n, ay + (by - ay) * i / n
                if (int(round(x)), int(round(y))) in cells:
                    put(x, y, DRG[2], layer(x, y))
        for k in range(5):                                                        # the thumb claw: a hook from the peak towards the head
            put(peak[0] - 1 - k * 0.9, peak[1] - 1 - k * 0.5 + (0.6 * (k - 2) ** 2 if k > 2 else 0), DHORN[2 if k > 1 else 1], layer(peak[0], peak[1]))

    flap = (8.0 * math.sin(2 * math.pi * phase) if walking else 3.0 * idle)
    folded_wing(flap * 0.8, far=True)

    # ---- the legs (far first): a thigh, a shin, a wide clawed foot
    def gait(off):
        a = math.sin(2 * math.pi * (phase + off)) if walking else 0.0
        lift = max(0.0, math.cos(2 * math.pi * (phase + off))) * 2.0 if walking else 0.0
        return a, lift

    def limb(a, b, w, ramp, dst=None):
        n = int(max(abs(b[0] - a[0]), abs(b[1] - a[1])) * 2) + 1
        for t in range(n + 1):
            x, y = a[0] + (b[0] - a[0]) * t / n, a[1] + (b[1] - a[1]) * t / n
            for ox in range(-(w // 2), w - w // 2):
                put(x + ox, y, ramp[2] if ox < 0 else ramp[1], dst)

    def foot(fx, fy, ramp, dst=None):
        for ox in range(-5, 2):
            put(fx + ox, fy, ramp[1] if ox > -5 else DHORN[1], dst)
        for ox in (-5, -3, -1):
            put(fx + ox, fy - 1, DHORN[2], dst)
        put(fx + 1, fy - 1, ramp[2], dst)

    def legs(far):
        ramp = DRG[0:3] if far else DRG[1:4]
        dx = 3 if far else 0
        a, lift = gait(0.5 if far else 0.0)
        hx, hy = 39 + dx, 54 + bob
        fx, fy = 36 + dx - 3.5 * a, GROUND - lift
        limb((hx, hy), (hx + 1.5, 59 + bob), 4, ramp)
        limb((hx + 1.5, 59 + bob), (fx, fy), 3, ramp)
        foot(fx, fy, ramp)
        a, lift = gait(0.0 if far else 0.5)
        hx, hy = 66 + dx, 54 + bob
        fx, fy = 66 + dx - 4.0 * a, GROUND - lift
        if far:                                                               # the near hind leg is under the folded wing's curtain: not drawn
            limb((hx, hy), (hx - 2, 59 + bob), 5, ramp)
            limb((hx - 2, 59 + bob), (fx, fy), 3, ramp)
            foot(fx, fy, ramp)

    legs(far=True)

    # ---- the tail: thick at the hips, tapering to a spade, trailing low and swaying
    sway = (1.5 * math.sin(2 * math.pi * phase) if walking else idle)
    tpts = [(70, 50 + bob), (83, 50 + bob + sway * 0.3), (95, 56 + sway * 0.7), (104, 61 + sway), (110, 57 + sway * 1.2)]
    tail = catmull(tpts, 160)
    for i, (x, y) in enumerate(tail):
        u = i / (len(tail) - 1)
        r = 6.2 * (1 - u) ** 0.8 + 1.0
        for yy in range(int(y - r - 1), int(y + r + 2)):
            for xx in range(int(x - r - 1), int(x + r + 2)):
                d = math.hypot(xx + 0.5 - x, yy + 0.5 - y)
                if d <= r:
                    up = (y - (yy + 0.5)) / max(r, 0.6)
                    if up < -0.35 and u < 0.75:
                        put(xx, yy, DBELLY[1] if int(xx) % 4 else DBELLY[0])     # the plates under the tail
                    else:
                        put(xx, yy, DRG[4] if up > 0.5 else DRG[3] if up > -0.05 else DRG[2])
        if i % 14 == 7 and u < 0.8:                                             # a spine on top of the tail
            for k in range(2 if u > 0.4 else 3):
                put(x, y - r - k, DHORN[1 if k else 0])
    tx, ty = tail[-1]
    for k in range(6):                                                           # the spade: an arrowhead, wider than the tip
        for ox in range(-k // 2 - 1, k // 2 + 2):
            put(tx + 1 + k * 0.7, ty - 4 + k * 1.3 + ox, DRG[2] if abs(ox) >= k // 2 + 1 else DRG[3])

    # ---- the body, neck and head as one shape: a deep chest, a dipped back on the seat line, big haunches, a thick
    # neck arching up (down when grazing), a big skull with a long jaw
    hd = head_down
    neck = [(34.0, 44.0 + bob), lp((27, 36 + bob), (30, 50), hd), lp((22, 27 + bob), (23, 57), hd), lp((17, 22 + bob), (16, 59), hd)]
    hx, hy = neck[-1]

    def sdf(x, y):
        chest = _ell(x, y, 39, 48.5 + bob, 11.5, 9.0)
        back = _ell(x, y, 52, 50.0 + bob, 11.0, 8.6)
        hips = _ell(x, y, 63, 49.0 + bob, 11.0, 9.2)
        haunch = _ell(x, y, 66, 54.0 + bob, 8.0, 6.0)
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
                if y % 3 == 0:
                    col = DBELLY[max(0, DBELLY.index(col) - 1)]                 # the plates' seams
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

    folded_wing(flap, far=False)

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
# name, drawing function, title, canvas, the mount kind (id, name) the skin is a look of (docs/mounts-design.md)
# (the Blue Dragon, dragon_blue above, was dropped from the shipped set 2026-10-19: the player's colours do that)
# name, drawing function, title, canvas, the mount kind (id, name), the seat row: the canvas row (from the top) the
# rider's hip sits on for every player (docs/seat-investigation.md: 46 is the donkey's own seat on the 96 canvas, so a
# creature drawn to the donkey's measures says 46; the 128 canvas adds 16; the snail sits 3 px lower on its shell)
# ... and the hitch gap: how many px further behind the body the whole cart sits with the cart hitched ([Tuning.Cart]
# HitchGapPx; the donkey's is 0; None = not measured yet: the "cart readout" self-test says what it is)
CREATURES = (("worm", worm, "Giant Worm", 96, ("worm", "Giant Worm"), 52, None), ("snail", snail, "Giant Snail", 96, ("snail", "Giant Snail"), 55, None),
             ("rat", rat, "Giant Rat", 96, ("rat", "Giant Rat"), 52, 9),
             ("dragon", dragon, "Red Dragon", 128, ("dragon", "Dragon"), 68, 24))  # seat rows: the donkey's 52 (+16 on 128), the snail 3 lower

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # tools/skins/creatures/make_creatures.py -> the repo
only = sys.argv[1] if len(sys.argv) > 1 else None     # one creature's name, to redraw just that one
assets = sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, "src", "GK2DonkeyAPI", "assets", "skins")
if only and only not in {c[0] for c in CREATURES}:
    sys.exit("no creature called " + only + "; one of " + ", ".join(c[0] for c in CREATURES))
for name, fn, title, size, (kind, kind_name), seat_row, hitch_gap in CREATURES:
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
                "[Skin]\nformat = 1\nid = " + name + "\nname = " + title + "\nauthor = dreas\nversion = 1.0\nbase = donkey\n"
                "kind = own\nlayer = replace\ncanvas = " + str(size) + " " + str(size) + "\nseat = " + str(seat_row)
                + "            # the rider's hip on this row of the picture, every frame, for every player (docs/seat-investigation.md)\nmount = " + kind + "\n"
                + ("\n# With the cart only: the whole cart this many px further behind the body (the skin owns its drawing's geometry).\n[Tuning.Cart]\nHitchGapPx = " + str(hitch_gap) + "\n" if hitch_gap is not None else "")
                + "\n# The mount this skin is the Default of (docs/mounts-design.md): what the player rides, shared by every skin of it.\n"
                "[Mount]\nid = " + kind + "\nname = " + kind_name + "\nauthor = dreas\nversion = 1.0\ncart = true\nfoals = true\n")
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
