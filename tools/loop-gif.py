"""A looping GIF from a gameplay recording (the Nexus thumbnail, media/README.md).

    python tools/loop-gif.py <video> [-o out.gif] [--start S --end E] [--fade 6] [--fade-before] [--fps 25]
                             [--width 560] [--crop W:H:X:Y] [--from SECONDS] [--to SECONDS] [--dedupe]
                             [--min 2.5] [--max 8] [--match X,Y,W,H] [--start-within SECONDS]

The cut: --start and --end (seconds into the video) cut exactly there. Without them the script searches for the
stretch whose last frame best matches its first: every frame against every later one between --min and --max seconds
on, compared small and grey (a few frames together, so the motion matches too); --match limits the comparison to a
part of the picture, --start-within keeps the start near --from. A take that comes back to where it started loops
cleanly; with a following camera the background never comes back and the seam is a dissolve.

The seam: blended over --fade frames. By default the frames just past the end fade into the first ones; with
--fade-before the last frames fade into the ones just before the start instead, so nothing after the end shows (a cart
slowing down after it). --dedupe leaves out frames the recording repeated (a hitch). --from / --to limit which part of
the video is read (the fade frames must lie inside it); --crop cuts the picture first (ffmpeg's crop=W:H:X:Y, in the
recording's pixels). Writes the GIF (--width wide, one palette for the whole loop) and an MP4 of the same loop beside it.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image

AW = 160  # analysis width, pixels


def ffmpeg(*args):
    r = subprocess.run(["ffmpeg", "-v", "error", "-y", *args], capture_output=True)
    if r.returncode != 0:
        sys.exit("ffmpeg failed: " + r.stderr.decode(errors="replace")[-2000:])
    return r.stdout


def size_of(video, crop):
    out = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height",
                          "-of", "csv=p=0", video], capture_output=True, text=True).stdout.strip()
    w, h = (int(v) for v in out.split(",")[:2])
    if crop:
        w, h = (int(v) for v in crop.split(":")[:2])
    return w, h


def filters(a, extra):
    f = []
    if a.crop:
        f.append("crop=" + a.crop)
    f.append(f"fps={a.fps}")
    f.extend(extra)
    return ",".join(f)


def window_args(a):
    w = []
    if a.from_ is not None:
        w += ["-ss", str(a.from_)]
    if a.to is not None:
        w += ["-to", str(a.to)]
    return w


def analysis_frames(a):
    w, h = size_of(a.video, a.crop)
    ah = max(2, round(h * AW / w / 2) * 2)
    raw = ffmpeg(*window_args(a), "-i", a.video, "-vf", filters(a, [f"scale={AW}:{ah}:flags=area", "format=gray"]),
                 "-f", "rawvideo", "-")
    n = len(raw) // (AW * ah)
    fr = np.frombuffer(raw[:n * AW * ah], dtype=np.uint8).reshape(n, ah, AW).astype(np.float32)
    if a.match:
        # only this part of the picture is compared (fractions x, y, w, h): with a following camera the cart stays put
        # on screen and the background never comes back, so the loop is matched on the cart and the background blended
        x, y, w, h = (float(v) for v in a.match.split(","))
        fr = fr[:, int(y * ah):int((y + h) * ah), int(x * AW):int((x + w) * AW)]
    return fr


def best_loop(fr, a):
    n = len(fr)
    lo, hi = int(a.min * a.fps), int(a.max * a.fps)
    k = 3  # frames compared together: the picture and its motion
    flat = fr.reshape(n, -1)
    best = (np.inf, 0, 0)
    last_start = n - lo - k - a.fade
    if a.start_within is not None:
        last_start = min(last_start, int(a.start_within * a.fps) + 1)
    for i in range(0, last_start):
        js = np.arange(i + lo, min(n - k - a.fade, i + hi + 1))
        if len(js) == 0:
            break
        d = np.zeros(len(js), dtype=np.float32)
        for t in range(k):
            d += np.abs(flat[js + t] - flat[i + t]).mean(axis=1)
        j = int(np.argmin(d))
        if d[j] < best[0]:
            best = (float(d[j] / k), i, int(js[j]))
    if not np.isfinite(best[0]):
        sys.exit(f"the recording is too short for a loop of at least {a.min} s (plus {a.fade} fade frames)")
    # for scale: how different two frames half a loop apart are
    i, j = best[1], best[2]
    typical = float(np.abs(flat[i] - flat[(i + j) // 2]).mean())
    return best[0], i, j, typical


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("video")
    p.add_argument("-o", "--out")
    p.add_argument("--min", type=float, default=2.5, help="shortest loop, seconds")
    p.add_argument("--max", type=float, default=8.0, help="longest loop, seconds")
    p.add_argument("--fps", type=int, default=25)
    p.add_argument("--width", type=int, default=560)
    p.add_argument("--fade", type=int, default=6, help="frames the seam is blended over (0: a plain cut)")
    p.add_argument("--crop")
    p.add_argument("--from", dest="from_", type=float)
    p.add_argument("--to", type=float)
    p.add_argument("--match", help="compare only this part, fractions x,y,w,h of the picture (e.g. the cart)")
    p.add_argument("--start-within", type=float, help="the loop starts within this many seconds of --from")
    p.add_argument("--dither", default="none", help="ffmpeg paletteuse dither (none, bayer, sierra2_4a, ...)")
    p.add_argument("--start", type=float, help="the loop's first frame, seconds into the video (with --end: no search)")
    p.add_argument("--end", type=float, help="the loop's end, seconds into the video (the frame after its last)")
    p.add_argument("--fade-before", action="store_true",
                   help="blend the loop's last frames into the frames just before its start (nothing after the end is "
                        "shown: a cart stopping after it stays out), instead of the frames past its end into its first")
    p.add_argument("--dedupe", action="store_true",
                   help="leave out frames the recording repeated (a hitch: nothing moved since the frame before)")
    a = p.parse_args()
    out = a.out or os.path.splitext(a.video)[0] + "-loop.gif"

    base = a.from_ or 0.0
    if a.start is not None and a.end is not None:
        i, j = int(round((a.start - base) * a.fps)), int(round((a.end - base) * a.fps))
        print(f"loop: {a.start:.2f} s to {a.end:.2f} s as given ({(j - i) / a.fps:.2f} s)")
    else:
        fr = analysis_frames(a)
        score, i, j, typical = best_loop(fr, a)
        t0 = base + i / a.fps
        print(f"loop: frames {i}..{j} of {len(fr)} ({t0:.2f} s to {t0 + (j - i) / a.fps:.2f} s, {(j - i) / a.fps:.2f} s long); "
              f"seam difference {score:.2f} against {typical:.2f} for two frames half a loop apart (0 = identical)")

    tmp = tempfile.mkdtemp(prefix="loopgif-")
    try:
        # the loop's frames plus the fade frames past its end, at the output width
        w, h = size_of(a.video, a.crop)
        oh = round(h * a.width / w / 2) * 2
        ffmpeg(*window_args(a), "-i", a.video, "-vf", filters(a, [f"scale={a.width}:{oh}:flags=lanczos"]),
               os.path.join(tmp, "f%05d.png"))
        files = sorted(f for f in os.listdir(tmp) if f.startswith("f"))
        frames = lambda x: np.asarray(Image.open(os.path.join(tmp, files[x])).convert("RGB"), dtype=np.float32)
        keep = list(range(len(files)))
        if a.dedupe:
            # a repeated frame: hardly a pixel off the one before it (the recording hitched; the game moved on)
            keep, prev, dropped = [0], frames(0), []
            for x in range(1, len(files)):
                cur = frames(x)
                if np.abs(cur - prev).mean() < 1.0:
                    dropped.append(x)
                    continue
                keep.append(x)
                prev = cur
            if dropped:
                print(f"left out {len(dropped)} repeated frame(s) at " + ", ".join(f"{base + x / a.fps:.2f} s" for x in dropped))
        seq = [x for x in keep if i <= x < j]
        after = [x for x in keep if x >= j][:a.fade]
        before = [x for x in keep if x < i][-a.fade:]
        fade = min(a.fade, len(before) if a.fade_before else len(after))
        if fade < a.fade:
            print(f"only {fade} fade frame(s) available {'before the start' if a.fade_before else 'after the end'}")
        length = len(seq)
        for k in range(length):
            img = frames(seq[k])
            if a.fade_before and k >= length - fade:
                # the last frames fading into the ones just before the start, so the loop runs on into its first
                m = k - (length - fade)
                w_next = (m + 1) / (fade + 1)
                img = (1 - w_next) * img + w_next * frames(before[len(before) - fade + m])
            elif not a.fade_before and k < fade:
                # just past the end (continuing the last frame) fading into the start
                w_start = (k + 1) / (fade + 1)
                img = (1 - w_start) * frames(after[k]) + w_start * img
            Image.fromarray(np.clip(img + 0.5, 0, 255).astype(np.uint8)).save(os.path.join(tmp, f"o{k:05d}.png"))
        pattern = os.path.join(tmp, "o%05d.png")
        ffmpeg("-framerate", str(a.fps), "-i", pattern, "-vf",
               f"split[x][y];[x]palettegen=max_colors=256:stats_mode=full[pal];[y][pal]paletteuse=dither={a.dither}",
               "-loop", "0", out)
        mp4 = os.path.splitext(out)[0] + ".mp4"
        ffmpeg("-framerate", str(a.fps), "-i", pattern, "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", mp4)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print(f"{out}: {a.width}x{oh}, {length} frames at {a.fps} fps, {os.path.getsize(out):,} bytes; preview {mp4}")


if __name__ == "__main__":
    main()
