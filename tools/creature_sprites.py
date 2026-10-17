"""Mounts drawn from nothing (docs/mount-looks-design.md): a giant worm and a giant snail, as picture skins for the
asset probe's tester (BepInEx\\GK2-asset-probe\\test\\skins\\<name>\\). Our own art, made by code: no game pixels.

Every donkey frame name gets a picture (96x96, facing left, the donkey's pivot in the middle), so the creature covers
every clip: the 8 walk frames are one crawl cycle; standing, ear, lips, chew, turn and graze are idle frames (graze puts
the head down). The donkey's head layer gets empty pictures, so "Instead of it" hides the donkey completely.
Fitted to the donkey's measures so the rider sits right without tuning: the back (the seat) at row 41 around columns
47-63, the ground at row 64, the front (where the cart's shafts and the harness are) around column 33.
Args: the probe folder (BepInEx\\GK2-asset-probe)."""
import math, os, struct, sys, zlib

W = H = 96
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
    return outline(px, (36, 30, 22))


# ---------------------------------------------------------------- the frames

def frames_for(fn):
    out = {}
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
        out[name] = empty
    return out


probe = sys.argv[1]
for name, fn in (("worm", worm), ("snail", snail)):
    d = os.path.join(probe, "test", "skins", name)
    os.makedirs(d, exist_ok=True)
    fr = frames_for(fn)
    for key, px in fr.items():
        write(os.path.join(d, key + ".png"), px)
    print(name, len(fr), "pictures ->", d)
