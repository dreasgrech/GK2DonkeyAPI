"""Reads Performance Hog reports (docs/performance-hog.md) and puts our mods' numbers side by side, run by run.

    python tools/perfhog-report.py                      the newest report in the game's BepInEx/PerformanceHog
    python tools/perfhog-report.py --last 5             the five newest, oldest first
    python tools/perfhog-report.py a.txt b.txt          these reports (with their -frames.csv next to them)
    python tools/perfhog-report.py --label walk,ride,cart --last 3
    python tools/perfhog-report.py --suite              the newest suite of tools/GK2PerfRunner (its labels and reports)
    python tools/perfhog-report.py --suite suite-2026-10-16_21-00-00.txt

For each report: the recording's line (FPS, 1% low, the mods' share of the frame), before against during (Performance
Hog 1.2: the 30 s before the recording key), our mods' rows and their slowest methods, the frames where our mods took
1 ms or more, the long frames with a garbage collection in them, and the memory change between two snapshots. From the
frames file (every frame, Performance Hog 1.2): percentiles per phase, hitches, and whether the hitches come back on a
timer (the same gap again and again: a periodic job, like the 0.25 s Mods-window lookups found 2026-10-16). Last, a
table of all the runs given.

Performance Hog times entry points only (Update/LateUpdate, patch methods, coroutines, delegates): the Donkey API's
work shows as Plugin.LateUpdate. What inside it costs is our own [perf] line (docs/performance.md) or a probe.
"""
import argparse
import csv
import glob
import os
import re
import statistics
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OURS = ("GK2 Donkey API", "GK2 Saddle Up")
RUNNER = "GK2 Perf Runner"


def game_dir():
    props = open(os.path.join(ROOT, "Directory.Build.props"), encoding="utf-8").read()
    return re.search(r"<GameDir[^>]*>([^<]+)</GameDir>", props).group(1).strip()


def reports_folder():
    return os.path.join(game_dir(), "BepInEx", "PerformanceHog")


class Run:
    def __init__(self, path, label):
        self.path = path
        self.label = label
        self.text = open(path, encoding="utf-8", errors="replace").read()
        self.lines = self.text.splitlines()
        self.version = (re.search(r"Performance Hog (\d+\.\d+\.\d+)", self.text) or [None, "?"])[1]
        self.rows = {}
        self.details = {}
        self.parse()
        self.frames = self.read_frames()

    def section(self, title):
        out, on = [], False
        for ln in self.lines:
            if ln.startswith(title):
                on = True
                continue
            if on:
                if ln.strip() == "" or (ln.isupper() and not ln.startswith("-")):
                    if out:
                        break
                    continue
                out.append(ln)
        return out

    def parse(self):
        rec = [ln for ln in self.lines if ln.startswith("Last recording") or ln.startswith("Recording so far")]
        self.recording = rec[0] if rec else None
        m = re.search(r"([\d.]+) FPS \(1% low ([\d.]+), median frame ([\d.]+) ms\)\. Mods ([\d.]+) ms of ([\d.]+) ms", self.recording or "")
        self.fps, self.low, self.median, self.mods_ms, self.work_ms = (float(v) for v in m.groups()) if m else (None,) * 5
        m = re.search(r"Last recording: ([\d:]+), (\d+) frames", self.recording or "")
        self.length = m.group(1) if m else "?"
        # the table: the name in the first 39 characters, then columns two spaces apart
        start = next((i for i, ln in enumerate(self.lines) if ln.startswith("Mod ") and "CPU ms/frame" in ln), None)
        if start is not None:
            for ln in self.lines[start + 1:]:
                if not ln.strip():
                    break
                name = ln[:39].strip()
                cols = re.split(r"\s{2,}", ln[39:].strip())
                self.rows[name] = cols
        # details: "== <name> <version>  [...]" blocks
        cur = None
        for ln in self.lines:
            m = re.match(r"== (.+?) (\d[\w.\-]*)\s+\[", ln)
            if m:
                cur = m.group(1)
                self.details[cur] = []
                continue
            if cur is not None:
                if ln.startswith("== "):
                    cur = None
                    continue
                self.details[cur].append(ln)
        self.pace = [ln for ln in self.lines if re.match(r"- (Before|Preparing|Recording): ", ln)]
        self.against = next((ln for ln in self.lines if ln.startswith("Recording against before")), None)
        self.mod_frames = self.section("FRAMES WHERE MODS TOOK THE MOST TIME")
        self.long_frames = self.section("LONGEST FRAMES")
        self.memory = self.section("MEMORY SNAPSHOT") or self.section("MEMORY")

    def read_frames(self):
        m = re.search(r"Every frame, with the sensors' reading at that frame: (\S+)", self.text)
        name = m.group(1).rstrip(".") if m else None
        path = os.path.join(os.path.dirname(self.path), name) if name else os.path.splitext(self.path)[0] + "-frames.csv"
        if not os.path.exists(path):
            return None
        with open(path, encoding="utf-8") as f:
            return [r for r in csv.DictReader(f)]


def pct(xs, p):
    if not xs:
        return float("nan")
    xs = sorted(xs)
    k = min(len(xs) - 1, max(0, int(round(p / 100.0 * (len(xs) - 1)))))
    return xs[k]


def frame_stats(frames, phase):
    rows = [r for r in frames if r["phase"] == phase and not r["left_out"]]
    ms = [float(r["frame_ms"]) for r in rows if r["frame_ms"]]
    if not ms:
        return None
    med = statistics.median(ms)
    hitch = [r for r in rows if r["frame_ms"] and float(r["frame_ms"]) >= 2 * med]
    return {
        "n": len(ms), "fps": 1000.0 * len(ms) / sum(ms), "p50": med, "p95": pct(ms, 95), "p99": pct(ms, 99),
        "max": max(ms), "hitches": len(hitch), "over33": sum(1 for v in ms if v > 33.3),
        "hitch_times": [float(r["time_s"]) for r in hitch],
    }


def periodic(times):
    """The gaps between hitches that come back again and again (0.05 s buckets): a timer, not chance."""
    if len(times) < 6:
        return []
    gaps = [round((b - a) / 0.05) * 0.05 for a, b in zip(times, times[1:]) if b - a > 0.04]
    counts = {}
    for g in gaps:
        counts[g] = counts.get(g, 0) + 1
    top = sorted(counts.items(), key=lambda kv: -kv[1])[:3]
    return [(g, c, len(gaps)) for g, c in top if c >= max(3, len(gaps) // 5)]


def show(run):
    print("=" * 110)
    print(f"{run.label}: {os.path.basename(run.path)} (Performance Hog {run.version})")
    if run.recording is None:
        print("  no recording in this report")
    else:
        print(f"  recording {run.length}: {run.fps} FPS, 1% low {run.low}, median {run.median} ms; mods {run.mods_ms} ms of {run.work_ms} ms work a frame")
    for ln in run.pace:
        print("  " + ln[2:])
    if run.against:
        print("  " + run.against)
    print("  our rows (CPU ms/frame, worst ms, alloc/frame, memory, log lines, disk written, exceptions, requests, startup):")
    for name in OURS:
        if name in run.rows:
            print(f"    {name:16} " + " | ".join(run.rows[name][:9]))
    if RUNNER in run.rows:
        # the runner's own work (the loop steering, the suite's steps); H/G and the test load are billed to Saddle Up
        # and the API, as for real keys, since the runner invokes their own handlers
        print(f"    {RUNNER:16} " + " | ".join(run.rows[RUNNER][:9]) + "   (the runner itself)")
    for name in OURS:
        d = run.details.get(name)
        if not d:
            continue
        keep = [ln.strip() for ln in d if re.match(r"\s+(CPU|Worst frame|Allocations|Log|Disk|Exceptions|\d+x |<data>|[\d.]+ ms/frame)", ln)]
        if keep:
            print(f"  {name}:")
            for ln in keep[:16]:
                print("    " + ln)
    ours = [ln for ln in run.mod_frames if any(o in ln for o in OURS)]
    if ours:
        print(f"  frames where mods took 1 ms or more, with ours in them ({len(ours)} of {len(run.mod_frames)} listed):")
        for ln in ours[:8]:
            print("    " + ln[2:])
    gc = [ln for ln in run.long_frames if "garbage collection" in ln]
    if run.long_frames:
        print(f"  longest frames: {len(run.long_frames)} listed, {len(gc)} with a garbage collection")
        for ln in run.long_frames[:6]:
            print("    " + ln[2:])
    if run.memory:
        print("  memory:")
        for ln in run.memory[:8]:
            print("    " + ln.lstrip("- "))
    if run.frames:
        print("  frames file (ms; left-out frames skipped):")
        print(f"    {'phase':>10} {'frames':>7} {'FPS':>6} {'p50':>6} {'p95':>6} {'p99':>6} {'max':>6} {'hitches':>8} {'>33ms':>6}")
        for ph in ("before", "preparing", "recording"):
            s = frame_stats(run.frames, ph)
            if s:
                print(f"    {ph:>10} {s['n']:7d} {s['fps']:6.1f} {s['p50']:6.2f} {s['p95']:6.2f} {s['p99']:6.2f} {s['max']:6.1f} {s['hitches']:8d} {s['over33']:6d}")
        for ph in ("before", "recording"):
            s = frame_stats(run.frames, ph)
            per = periodic(s["hitch_times"]) if s else []
            for g, c, n in per:
                print(f"    {ph}: {c} of {n} gaps between hitches are {g:.2f} s: a hitch on a timer")
    else:
        print("  no frames file (Performance Hog 1.2 writes <report>-frames.csv on Export)")


def table(runs):
    print("=" * 110)
    print(f"{'run':14} {'FPS':>6} {'1%low':>6} {'p99':>6} {'hitch':>5} {'vs before':>22} {'API ms':>7} {'API worst':>9} {'API alloc':>10} {'SU ms':>6} {'SU worst':>8}")
    prev = None
    for r in runs:
        if prev is not None and r.recording and r.recording == prev.recording:
            # an export without a new recording (the suite's memory report): its CPU columns are the last recording's
            print(f"{r.label:14} (no recording of its own: the numbers are {prev.label}'s; see its memory section)")
            continue
        prev = r
        rec = frame_stats(r.frames, "recording") if r.frames else None
        api = r.rows.get(OURS[0], ["-"] * 3)
        su = r.rows.get(OURS[1], ["-"] * 3)
        vs = ""
        if r.against:
            m = re.search(r"FPS ([+\-]?\d+)%, 1% low ([+\-]?\d+)%", r.against)
            vs = f"FPS {m.group(1)}%, low {m.group(2)}%" if m else ""
        print(f"{r.label:14} {r.fps or 0:6.1f} {r.low or 0:6.1f} {rec['p99'] if rec else float('nan'):6.2f} {rec['hitches'] if rec else 0:5d} {vs:>22} "
              f"{api[0]:>7} {api[1]:>9} {api[2]:>10} {su[0]:>6} {su[1]:>8}")


def read_suite(path):
    """(the comment lines, [(label, report path)]) of a GK2PerfRunner suite file."""
    notes, runs = [], []
    for ln in open(path, encoding="utf-8"):
        ln = ln.rstrip("\r\n")
        if ln.startswith("#"):
            notes.append(ln[2:])
        elif "\t" in ln:
            label, name = ln.split("\t", 1)
            runs.append((label, os.path.join(os.path.dirname(path), name)))
    return notes, runs


def series(suites):
    """Each run's API numbers suite by suite (a steady state repeats; drift shows), and the memory per suite: caches fill
    in the first suite only, so growth in the later ones is a leak."""
    if not suites:
        sys.exit("no suite files in " + reports_folder())
    cols = []
    for path in suites:
        notes, runs = read_suite(path)
        cols.append((os.path.basename(path)[6:-4], notes, {label: Run(rp, label) for label, rp in runs if os.path.exists(rp)}))
    for name, notes, _ in cols:
        print(f"{name}: " + "; ".join(n for n in notes if n.startswith(("time of day", "suite", "done")) or "done in" in n or "stopped" in n))
    print("=" * 110)
    labels = []
    for _, _, runs in cols:
        for label in runs:
            if label not in labels and label != "memory":
                labels.append(label)
    head = "".join(f"{n[-8:]:>22}" for n, _, _ in cols)
    print(f"{'API ms/frame, worst ms':24}{head}")
    for label in labels:
        cells = []
        for _, _, runs in cols:
            r = runs.get(label)
            row = r.rows.get(OURS[0]) if r else None
            cells.append(f"{row[0]} / {row[1]}" if row else "-")
        print(f"{label:24}" + "".join(f"{c:>22}" for c in cells))
    print(f"{'API alloc/frame':24}" + "".join(f"{'':>22}" for _ in cols))
    for label in labels:
        cells = []
        for _, _, runs in cols:
            r = runs.get(label)
            row = r.rows.get(OURS[0]) if r else None
            cells.append(row[2] if row else "-")
        print(f"  {label:22}" + "".join(f"{c:>22}" for c in cells))
    print(f"{'API memory (snapshot)':24}" + "".join(f"{'':>22}" for _ in cols))
    cells = []
    for _, _, runs in cols:
        r = runs.get("memory")
        line = next((ln for ln in (r.memory if r else []) if ln.lstrip("- ").startswith(OURS[0])), None)
        m = re.search(r"([\d.]+ [KMG]?B) \(([+\-][^)]*)\); (\d+) objects", line or "")
        cells.append(f"{m.group(2)}, {m.group(3)} obj" if m else "-")
    print(f"  {'change, objects':22}" + "".join(f"{c:>22}" for c in cells))


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("reports", nargs="*")
    p.add_argument("--last", type=int, default=1, help="the newest N reports in the game's PerformanceHog folder")
    p.add_argument("--label", help="comma-separated names for the runs, in order")
    p.add_argument("--suite", nargs="?", const="", help="a suite file of GK2PerfRunner (default: the newest)")
    p.add_argument("--series", type=int, help="the newest N suite files side by side: each run's numbers suite by suite and the memory change")
    a = p.parse_args()
    labels = a.label.split(",") if a.label else []
    if a.series:
        series(sorted(glob.glob(os.path.join(reports_folder(), "suite-*.txt")))[-a.series:])
        return
    if a.suite is not None:
        suite = a.suite or (sorted(glob.glob(os.path.join(reports_folder(), "suite-*.txt"))) or [None])[-1]
        if not suite:
            sys.exit("no suite file in " + reports_folder())
        if not os.path.isabs(suite) and not os.path.exists(suite):
            suite = os.path.join(reports_folder(), suite)
        paths, labels = [], []
        print(f"suite {os.path.basename(suite)}:")
        for ln in open(suite, encoding="utf-8"):
            ln = ln.rstrip("\r\n")
            if ln.startswith("#"):
                print("  " + ln[2:])
            elif "\t" in ln:
                label, name = ln.split("\t", 1)
                labels.append(label)
                paths.append(os.path.join(os.path.dirname(suite), name))
    else:
        paths = a.reports or sorted(glob.glob(os.path.join(reports_folder(), "report-*.txt")))[-a.last:]
    if not paths:
        sys.exit("no reports found in " + reports_folder())
    runs = [Run(path, labels[i] if i < len(labels) else f"run {i + 1}") for i, path in enumerate(paths)]
    for r in runs:
        show(r)
    if len(runs) > 1:
        table(runs)


if __name__ == "__main__":
    main()
