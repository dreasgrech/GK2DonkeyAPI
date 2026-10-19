# A minimal .aseprite writer (RGBA, layers, cels with positions, tags). Format: aseprite docs/ase-file-specs.md.
import struct, zlib

def _s(t):
    t = t.encode("utf-8")
    return struct.pack("<H", len(t)) + t

def _chunk(typ, data):
    return struct.pack("<IH", len(data) + 6, typ) + data

VISIBLE, EDITABLE = 1, 2

def write(path, w, h, layers, frames, tags, durations):
    """layers: [(name, flags)] bottom first; frames: [{layer_index: PIL RGBA image (full canvas) or None}];
    tags: [(name, from, to)]; durations: [ms] per frame. Each cel is cropped to its drawn box and placed there."""
    out = []
    for n, cels in enumerate(frames):
        ch = []
        if n == 0:
            ch.append(_chunk(0x2019, struct.pack("<III8x", 1, 0, 0) + struct.pack("<HBBBB", 0, 0, 0, 0, 255)))
            for name, flags in layers:
                ch.append(_chunk(0x2004, struct.pack("<HHHHHHB3x", flags, 0, 0, 0, 0, 0, 255) + _s(name)))
            t = struct.pack("<H8x", len(tags))
            for name, a, b in tags:
                t += struct.pack("<HHBH6x3Bx", a, b, 0, 0, 0, 0, 0) + _s(name)
            ch.append(_chunk(0x2018, t))
        for li in sorted(cels):
            im = cels[li]
            if im is None:
                continue
            box = im.getbbox()
            if box is None:
                continue
            c = im.crop(box)
            ch.append(_chunk(0x2005, struct.pack("<HhhBHh5x", li, box[0], box[1], 255, 2, 0)
                             + struct.pack("<HH", c.width, c.height) + zlib.compress(c.tobytes(), 9)))
        body = b"".join(ch)
        out.append(struct.pack("<IHHH2xI", 16 + len(body), 0xF1FA, min(len(ch), 0xFFFF), durations[n], len(ch)) + body)
    data = b"".join(out)
    hdr = struct.pack("<IHHHHHIHIIB3xHBBhhHH84x", 128 + len(data), 0xA5E0, len(frames), w, h, 32, 1, 100, 0, 0, 0, 0,
                      1, 1, 0, 0, 16, 16)
    assert len(hdr) == 128
    open(path, "wb").write(hdr + data)
