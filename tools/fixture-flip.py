# Makes a released-version cfg fixture: every value set to something other than its default (docs/compatibility.md 4).
# usage: flip_cfg.py <in.cfg> <out.cfg> <header line>
import sys, re
src, dst, header = sys.argv[1], sys.argv[2], sys.argv[3]
raw = open(src, "rb").read().decode("utf-8-sig")
nl = "\r\n" if "\r\n" in raw else "\n"
lines = raw.replace("\r\n", "\n").split("\n")
keys = iter(["Keypad1", "Keypad2", "Keypad3", "Keypad4", "Keypad5", "Keypad6", "Keypad7", "Keypad8", "Keypad9",
             "Keypad0", "KeypadPlus", "KeypadMinus", "Insert", "Home", "PageUp", "PageDown", "End", "Delete"])
meta = {}
out = []
flipped = kept = 0
report = []
def fnum(x):
    s = ("%.4f" % x).rstrip("0").rstrip(".")
    return s if s not in ("", "-0") else "0"
for ln in lines:
    m = re.match(r"# (Setting type|Default value|Acceptable values|Acceptable value range): ?(.*)$", ln)
    if m:
        meta[m.group(1)] = m.group(2)
        out.append(ln); continue
    m = re.match(r"^([A-Za-z0-9_.\-]+) = (.*)$", ln)
    if m and "Setting type" in meta:
        k, v = m.group(1), m.group(2)
        t, d = meta.get("Setting type"), meta.get("Default value", "")
        nv = None
        if t == "Boolean":
            nv = "false" if d == "true" else "true"
        elif t in ("Int32", "Single", "Double"):
            rng = meta.get("Acceptable value range")
            dv = float(d) if d else 0.0
            if rng:
                a, b = re.match(r"From (\S+) to (\S+)", rng).groups()
                lo, hi = float(a), float(b)
            else:
                lo, hi = dv - 1, dv + 1
            if t == "Int32":
                nv = str(int(lo) if int(dv) != int(lo) else int(hi))
            else:
                cand = (dv + hi) / 2 if abs(hi - dv) > 1e-6 else (dv + lo) / 2
                nv = fnum(cand)
                if nv == fnum(dv): nv = fnum(lo if abs(lo - dv) > 1e-6 else hi)
        elif "Acceptable values" in meta:
            vals = [x.strip() for x in meta["Acceptable values"].split(",")]
            nv = vals[(vals.index(d) + 1) % len(vals)] if d in vals else vals[0]
        elif t == "KeyboardShortcut" or t == "KeyCode":
            nv = next(keys)
        elif t == "String":
            if re.fullmatch(r"[0-9A-Fa-f]{6}", d or ""):
                nv = "336699" if d.upper() != "336699" else "993366"
            elif k.startswith("Name"):
                nv = "Fixture"
            else:
                nv = (d + "-x") if d else "x"
        if nv is None or nv == d:
            report.append("left %s (%s, default %r)" % (k, t, d)); kept += 1
            out.append(ln)
        else:
            out.append("%s = %s" % (k, nv)); flipped += 1
        meta = {}
        continue
    out.append(ln)
text = "## " + header + "\n" + "\n".join(out)
open(dst, "wb").write(text.replace("\n", nl).encode("utf-8"))
print("%s: %d set off their default, %d left" % (dst.rsplit("/", 1)[-1], flipped, kept))
for r in report[:40]: print("  " + r)
