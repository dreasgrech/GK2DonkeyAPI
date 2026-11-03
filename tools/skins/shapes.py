"""Shapes for drawing a skin's pieces in code (docs/sprite-tools.md "Catching what the user caught" 4): a tapered
stroke (a hat's point, a horn, a strap) along a polyline or a curve, a polygon filled, the ring just outside a shape
(its outline), a shape lit from the top left, a small bell, a pose (a shape drawn on one frame moved and turned onto
another by two points), a hanging part's swing (a pendulum over a clip, the parts of a picture that hang free moved
each by its own), and a picture written from pixels. Pixels are {(x, y): (r, g, b, a)} on the frame's canvas;
a ramp is [shade, base, light]. First used for the Jester (tools/skins/jester/draw_jester.py); tools/skins/pixgrid.py
is the way to touch the result up pixel by pixel.
"""
import math

from PIL import Image


def stroke(path, w0, w1, steps=60):
    """The pixels of a tapered stroke along a polyline of (x, y): discs from width w0 at its start to w1 at its end."""
    lens = [0.0]
    for (ax, ay), (bx, by) in zip(path, path[1:]):
        lens.append(lens[-1] + math.hypot(bx - ax, by - ay))
    total = lens[-1] or 1.0
    out = set()
    for i in range(steps + 1):
        t = total * i / steps
        k = max(j for j in range(len(path) - 1) if lens[j] <= t + 1e-9) if len(path) > 1 else 0
        k = min(k, len(path) - 2)
        seg = (lens[k + 1] - lens[k]) or 1.0
        f = (t - lens[k]) / seg
        x = path[k][0] + (path[k + 1][0] - path[k][0]) * f
        y = path[k][1] + (path[k + 1][1] - path[k][1]) * f
        r = (w0 + (w1 - w0) * t / total) / 2
        for py in range(int(math.floor(y - r)), int(math.ceil(y + r)) + 1):
            for px in range(int(math.floor(x - r)), int(math.ceil(x + r)) + 1):
                if (px - x) ** 2 + (py - y) ** 2 <= r * r + 0.25:
                    out.add((px, py))
    return out



def curve(ctrl, steps=24):
    """A smooth path through the ends of 'ctrl' bent towards its middle points (a Bezier curve of any order), as a
    polyline of 'steps' + 1 points: the path of a stroke that arches (a jester's point curling over and drooping)."""
    out = []
    for i in range(steps + 1):
        t = i / steps
        ps = list(ctrl)
        while len(ps) > 1:
            ps = [(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t) for a, b in zip(ps, ps[1:])]
        out.append(ps[0])
    return out


def polygon(points):
    """The pixels whose middles lie inside a polygon of (x, y) corners (a hood, a cloth's outline): drawn as its outline
    is read, corner by corner, where a stroke can only round it."""
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    out = set()
    for y in range(int(min(ys)), int(max(ys)) + 2):
        for x in range(int(min(xs)), int(max(xs)) + 2):
            inside = False
            for (ax, ay), (bx, by) in zip(points, points[1:] + points[:1]):
                if (ay > y) != (by > y) and x < ax + (y - ay) * (bx - ax) / (by - ay):
                    inside = not inside
            if inside:
                out.add((x, y))
    return out

def ring(shape):
    """The pixels just outside a shape, side by side with it (its outline)."""
    out = set()
    for x, y in shape:
        for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if q not in shape:
                out.add(q)
    return out


def lit(shape, ramp):
    """A shape's colours, lit from the top left: its top-left edge the light, its bottom-right edge the shade."""
    out = {}
    for x, y in shape:
        if (x - 1, y) not in shape or (x, y - 1) not in shape:
            out[(x, y)] = ramp[2]
        elif (x + 1, y) not in shape or (x, y + 1) not in shape:
            out[(x, y)] = ramp[0]
        else:
            out[(x, y)] = ramp[1]
    return out


def shaped(shape, ramp, outline, under=None):
    """A lit shape with its outline, the outline left off where it would cover 'under' (an earlier part)."""
    px = {q: outline for q in ring(shape) if under is None or q not in under}
    px.update(lit(shape, ramp))
    return px


def bell(x, y, gold, outline):
    """A bell hanging from (x, y): a 2x2 of the gold ramp with its shine at the top left, outlined, 4 wide."""
    px = {(x, y + 1): gold[2], (x + 1, y + 1): gold[1], (x, y + 2): gold[1], (x + 1, y + 2): gold[0]}
    for q in [(x, y), (x + 1, y), (x - 1, y + 1), (x + 2, y + 1), (x - 1, y + 2), (x + 2, y + 2),
              (x - 1, y + 3), (x, y + 3), (x + 1, y + 3), (x + 2, y + 3)]:
        px[q] = outline
    return px


def pose(src_a, src_b, dst_a, dst_b):
    """The move that takes a shape drawn on one frame onto another by two points of each (a head's poll and muzzle):
    turned about src_a by the angle between the lines a-b, then src_a put on dst_a; the size kept. Returns a function
    of (x, y). For a head that tilts (the graze): its pieces drawn as shapes are posed and drawn again, so their
    outlines stay whole, where turning the pixels would break them."""
    turn = math.atan2(dst_b[1] - dst_a[1], dst_b[0] - dst_a[0]) - math.atan2(src_b[1] - src_a[1], src_b[0] - src_a[0])
    c, s = math.cos(turn), math.sin(turn)

    def tf(p):
        x, y = p[0] - src_a[0], p[1] - src_a[1]
        return (dst_a[0] + c * x - s * y, dst_a[1] + s * x + c * y)
    return tf


def side(p, a, b):
    """Which side of the line a-b a point lies on (positive: to the right going from a to b, in the picture)."""
    return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])


def pendulum(i, phase, amp=2, period=8):
    """A hanging part's swing in frame i of a clip 'period' frames long (the walk's 8): across by amp times a sine over
    the clip, a row up at the ends of its arc when amp is 2 or more (five places in all), a pixel a frame. phase: whole
    frames behind the clip's start, so the parts swing in turn (half frames made them jump two pixels at once). Moved
    by the frame-to-frame lag, the Jester's bells had shown two places ("more framerate", the user, 2026-11-03)."""
    x = int(round(amp * math.sin(2 * math.pi * (i + phase) / period)))
    return x, (-1 if amp >= 2 and abs(x) >= amp else 0)


def hanging(px, free):
    """The parts of a picture that hang free (a fringe's tassels, a hem's point, a bell): its pixels for which
    free((x, y)) is true (lambda p: p[1] >= 58: from row 58 down), split into the ones touching (corner to corner
    too), the frontmost (smallest x) first."""
    left = {p for p in px if free(p)}
    parts = []
    while left:
        todo, part = [left.pop()], set()
        while todo:
            x, y = todo.pop()
            part.add((x, y))
            for q in [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)]:
                if q in left:
                    left.remove(q)
                    todo.append(q)
        parts.append(part)
    return sorted(parts, key=min)


def swing(px, free, moves):
    """A picture with each of its hanging parts (hanging(px, free), frontmost first) moved by its own of 'moves'
    ((dx, dy), one per part); the rest kept, a moved pixel drawn over a kept one."""
    parts = hanging(px, free)
    if len(moves) != len(parts):
        raise ValueError(f"swing: {len(moves)} moves for {len(parts)} hanging parts")
    out = {p: c for p, c in px.items() if not free(p)}
    for part, (dx, dy) in zip(parts, moves):
        for x, y in part:
            out[(x + dx, y + dy)] = px[(x, y)]
    return out


def save(px, path, canvas=96):
    """Pixels written as a picture of the canvas (transparent elsewhere; pixels outside it left out)."""
    im = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    p = im.load()
    for (x, y), c in px.items():
        if 0 <= x < canvas and 0 <= y < canvas:
            p[x, y] = c
    im.save(path)
    return im
