"""Whether two builds of a .NET DLL hold the same code and content, apart from the build's identity.

    python tools/same-build.py <a.dll> <b.dll>        exit 0: the same build; 1: they differ (where, printed); 2: unreadable

The builds are deterministic, but each carries the git commit it was built at (AssemblyInformationalVersion
"<version>+<40 hex>", once in an attribute blob and once in UTF-16 in the version resource), and the MVID (#GUID heap),
the PE timestamp and the debug directory's ids are hashes of the whole, so they change with the commit. Those are
masked; every other byte must match. Used by build-dist.sh for a DLL committed in dist/ for a release still in progress
(release-versions.py items: "pending"): the committed bytes ship only when the sources build to the same thing
(2026-11-12: dist/ was committed from a build of the uncommitted release prep on the previous commit; the code was
the same, only the stamp differed).
"""
import re
import struct
import sys


def identity_ranges(data):
    """[(start, end, what)] of the bytes that carry the build's identity."""
    out = []
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("not a PE file")
    out.append((pe + 8, pe + 12, "PE timestamp"))
    opt = pe + 24
    magic = struct.unpack_from("<H", data, opt)[0]
    out.append((opt + 64, opt + 68, "PE checksum"))
    ddir = opt + (96 if magic == 0x10B else 112)
    nsec = struct.unpack_from("<H", data, pe + 6)[0]
    sec = opt + struct.unpack_from("<H", data, pe + 20)[0]
    secs = []
    for i in range(nsec):
        vsize, va, rsize, raw = struct.unpack_from("<IIII", data, sec + 40 * i + 8)
        secs.append((va, max(vsize, rsize), raw))

    def off(rva):
        for va, size, raw in secs:
            if va <= rva < va + size:
                return rva - va + raw
        raise ValueError(f"rva {rva:#x} in no section")

    dbg_rva, dbg_size = struct.unpack_from("<II", data, ddir + 6 * 8)
    if dbg_rva:
        d = off(dbg_rva)
        for i in range(dbg_size // 28):
            e = d + 28 * i
            out.append((e + 4, e + 8, "debug entry timestamp"))
            size, _, ptr = struct.unpack_from("<III", data, e + 16)
            if size and ptr:
                out.append((ptr, ptr + size, "debug data (PDB id)"))
    cli = off(struct.unpack_from("<I", data, ddir + 14 * 8)[0])
    md = off(struct.unpack_from("<I", data, cli + 8)[0])
    p = md + 16 + struct.unpack_from("<I", data, md + 12)[0] + 2
    n = struct.unpack_from("<H", data, p)[0]
    p += 2
    for _ in range(n):
        o, size = struct.unpack_from("<II", data, p)
        p += 8
        end = data.index(b"\0", p)
        name = data[p:end].decode("ascii")
        p = (end + 1 + 3) & ~3
        if name == "#GUID":
            out.append((md + o, md + o + size, "MVID (#GUID)"))
    # the commit stamp of AssemblyInformationalVersion, ASCII and UTF-16
    for m in re.finditer(rb"\d+\.\d+\.\d+\+([0-9a-f]{40})", data):
        out.append((m.start(1), m.end(1), "commit stamp"))
    for m in re.finditer(rb"(?:\d\x00)+\.\x00(?:\d\x00)+\.\x00(?:\d\x00)+\+\x00((?:[0-9a-f]\x00){40})", data):
        out.append((m.start(1), m.end(1), "commit stamp (UTF-16)"))
    return out


def masked(data):
    b = bytearray(data)
    for s, e, _ in identity_ranges(data):
        b[s:e] = bytes(e - s)
    return bytes(b)


def same(a, b):
    """(True, "") or (False, why)."""
    if len(a) != len(b):
        return False, f"sizes differ: {len(a):,} / {len(b):,} bytes"
    ma, mb = masked(a), masked(b)
    if ma == mb:
        return True, ""
    first = next(i for i in range(len(ma)) if ma[i] != mb[i])
    count = sum(1 for i in range(len(ma)) if ma[i] != mb[i])
    return False, f"{count:,} byte(s) differ outside the build identity, the first at {first:#x}"


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    try:
        a, b = (open(p, "rb").read() for p in sys.argv[1:])
        ok, why = same(a, b)
    except (OSError, ValueError, struct.error) as e:
        print(f"unreadable: {e}")
        sys.exit(2)
    print("the same build (only the build identity differs)" if ok else why)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
