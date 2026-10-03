# Offline audition of foal sounds made from the game's own donkey clips (exported earlier to GK2-donkey-sfx-audition).
# Every variant mirrors something the mod could do at run time:
#   A  plain pitch      = SoundHandler.SetPitch (Unity resamples: higher AND shorter)
#   B  pitch + filter   = Unity's AudioHighPassFilter / AudioLowPassFilter / AudioChorusFilter on the AudioSource
#   C  custom DSP       = OnAudioFilterRead (a pitch shift that keeps the length, a warble)
#   D  calls            = several plays in a row from code (timers), no new audio tech
import os, wave, math
import numpy as np

SRC = r"C:\ClaudeCodeTemp\GK2-donkey-sfx-audition"
OUT = r"C:\ClaudeCodeTemp\GK2-foal-sound-audition"
os.makedirs(OUT, exist_ok=True)
for f in os.listdir(OUT):
    os.remove(os.path.join(OUT, f))


def load(name):
    w = wave.open(os.path.join(SRC, name + ".wav"))
    sr, ch, n = w.getframerate(), w.getnchannels(), w.getnframes()
    a = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float64) / 32768.0
    if ch == 2:
        a = a.reshape(-1, 2).mean(axis=1)
    return a, sr


def save(name, a, sr):
    a = np.clip(a, -1, 1)
    w = wave.open(os.path.join(OUT, name + ".wav"), "w")
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes((a * 32767).astype(np.int16).tobytes())
    w.close()


def pitch(a, p):  # Unity's AudioSource.pitch: read the clip p times faster
    idx = np.arange(0, len(a) - 1, p)
    return np.interp(idx, np.arange(len(a)), a)


def biquad(a, sr, f0, q, kind):  # RBJ cookbook, as Unity's resonant 2-pole filters
    w0 = 2 * math.pi * f0 / sr; al = math.sin(w0) / (2 * q); c = math.cos(w0)
    if kind == "hp":
        b = [(1 + c) / 2, -(1 + c), (1 + c) / 2]
    else:
        b = [(1 - c) / 2, 1 - c, (1 - c) / 2]
    aa = [1 + al, -2 * c, 1 - al]
    b = [x / aa[0] for x in b]; aa = [x / aa[0] for x in aa]
    y = np.zeros_like(a); x1 = x2 = y1 = y2 = 0.0
    for i, x in enumerate(a):
        v = b[0] * x + b[1] * x1 + b[2] * x2 - aa[1] * y1 - aa[2] * y2
        x2, x1, y2, y1 = x1, x, y1, v
        y[i] = v
    return y


def chorus(a, sr, depth_ms=2.0, rate=1.5, mix=0.5):  # one modulated delayed copy, as AudioChorusFilter
    n = np.arange(len(a))
    d = (depth_ms / 1000 * sr) * (1 + np.sin(2 * math.pi * rate * n / sr)) / 2 + 0.008 * sr
    return (1 - mix) * a + mix * np.interp(n - d, n, a, left=0)


def keep_length(a, sr, p, grain_ms=30):  # granular pitch shift, length kept (what OnAudioFilterRead could run)
    g = int(sr * grain_ms / 1000); hop = g // 2; win = np.hanning(g)
    out = np.zeros(len(a) + g)
    for start in range(0, len(a) - g, hop):
        grain = pitch(a[start:start + int(g * p) + 2], p)[:g]
        if len(grain) < g:
            break
        out[start:start + g] += grain * win
    return out[:len(a)]


def warble(a, sr, rate=9.0, depth=0.04):  # vibrato: a wobbly, young voice (a varying read speed)
    n = np.arange(len(a), dtype=np.float64)
    pos = n + depth * sr / (2 * math.pi * rate) * np.sin(2 * math.pi * rate * n / sr)
    return np.interp(np.clip(pos, 0, len(a) - 1), np.arange(len(a)), a)


def gap(sr, s):
    return np.zeros(int(sr * s))


def level(a, ref):  # keep the original's peak, so the comparisons are about the sound, not the loudness
    m = np.abs(a).max()
    return a * (np.abs(ref).max() / m) if m > 0 else a


lines = []


def out(name, a, sr, what):
    save(name, a, sr)
    lines.append(f"| `{name}.wav` | {len(a) / sr:.2f} s | {what} |")


brays = {k: load(k) for k in ["v_donkey_1", "v_donkey_2", "v_donkey_3", "v_donkey_4"]}

# A: plain pitch, all four brays one after another at each pitch, so a step is judged on the whole set
for p in [1.0, 1.25, 1.4, 1.6, 1.8, 2.0, 2.4]:
    parts = []
    for k, (a, sr) in brays.items():
        parts += [pitch(a, p), gap(sr, 0.5)]
    out(f"A_pitch_{p:.2f}".replace(".", "_"), np.concatenate(parts), 44100,
        "Plain pitch x%.2f (all four brays). In game: `SetPitch(%.2f)`. %s" % (p, p,
        "The adult as now, for reference." if p == 1.0 else "Higher and %d%% shorter." % round(100 * (1 - 1 / p))))

# B: pitch 1.6 plus a built-in filter
sr = 44100
base = [(pitch(a, 1.6), a) for a, _ in brays.values()]
def set_of(f):
    parts = []
    for p_, ref in base:
        parts += [level(f(p_), p_), gap(sr, 0.5)]
    return np.concatenate(parts)
out("B_pitch1_6_highpass700", set_of(lambda x: biquad(x, sr, 700, 1.0, "hp")), sr,
    "x1.6 + high-pass 700 Hz: less chest, a thinner, smaller body. `AudioHighPassFilter`.")
out("B_pitch1_6_highpass1200", set_of(lambda x: biquad(x, sr, 1200, 1.0, "hp")), sr,
    "x1.6 + high-pass 1200 Hz: pushed further, quite thin.")
out("B_pitch1_6_lowpass3000", set_of(lambda x: biquad(x, sr, 3000, 1.0, "lp")), sr,
    "x1.6 + low-pass 3000 Hz: softer and rounder, takes off the pitched-up hiss. `AudioLowPassFilter`.")
out("B_pitch1_6_chorus", set_of(lambda x: chorus(x, sr)), sr,
    "x1.6 + chorus: slightly wobbly, doubled. `AudioChorusFilter`.")

# C: custom DSP (OnAudioFilterRead)
parts = []
for a, _ in brays.values():
    parts += [level(keep_length(a, sr, 1.6), a), gap(sr, 0.5)]
out("C_pitch1_6_same_length", np.concatenate(parts), sr,
    "x1.6 higher but as long as the adult's (a granular shifter). Needs our own DSP (`OnAudioFilterRead`). Slightly grainy.")
parts = []
for a, _ in brays.values():
    parts += [level(warble(pitch(a, 1.6), sr), a), gap(sr, 0.5)]
out("C_pitch1_6_warble", np.concatenate(parts), sr,
    "x1.6 + a fast vibrato (a shaky young voice). Own DSP, or done in code by changing the pitch every frame.")

# D: calls made of several plays (code only)
a1, a2, a3, a4 = [v[0] for v in brays.values()]
call = np.concatenate([pitch(a4, 1.7), gap(sr, 0.12), pitch(a1, 1.6), gap(sr, 0.12), pitch(a4, 1.8)])
out("D_call_three_squeaks", call, sr, "Three quick pitched brays in a row (4, 1, 4 at x1.7/1.6/1.8): a foal calling.")
heehaw = np.concatenate([pitch(a1, 1.7), gap(sr, 0.05), pitch(a2, 1.4), gap(sr, 0.1), pitch(a1, 1.75), gap(sr, 0.05), pitch(a2, 1.45)])
out("D_hee_haw", heehaw, sr, "Two clips alternated, up and down (1 at x1.7, 2 at x1.4, twice): a small hee-haw.")
reply = np.concatenate([a2, gap(sr, 0.7), pitch(a4, 1.7), gap(sr, 0.15), pitch(a4, 1.85)])
out("D_parent_then_foal_answers", reply, sr, "The parent brays (as now), the foal answers 0.7 s later, higher and twice.")
fam = np.concatenate([a3, gap(sr, 0.5), pitch(a4, 1.55), gap(sr, 0.25), pitch(a1, 1.85), gap(sr, 0.3), pitch(a4, 1.7)])
out("D_three_foals_answer", fam, sr, "Parent, then three foals each at their own pitch (x1.55, x1.85, x1.7): siblings sound different.")

# The other donkey sounds, small
lips, lsr = load("donkey_lips_1")
out("E_lips_adult", lips, lsr, "The lips snort as the game plays it now (foals already make this one, at this pitch).")
out("E_lips_x1_5", level(pitch(lips, 1.5), lips) * 0.7, lsr, "Lips x1.5 at 70% volume: a small snort.")
graze, gsr = load("donkey_eating_grass")
out("E_graze_x1_3", pitch(graze, 1.3) * 0.6, gsr, "Grazing x1.3 at 60% volume: a foal nibbling.")
for k in ["grass_footstep_1", "road_footstep_1"]:
    s, ssr = load(k)
    out(f"E_{k}_adult_vs_foal", np.concatenate([s, gap(ssr, 0.4), pitch(s, 1.5) * 0.5, gap(ssr, 0.25), pitch(s, 1.6) * 0.5]), ssr,
        f"{k}: the adult's step, then two foal steps (x1.5/x1.6 at 50%).")

open(os.path.join(OUT, "README.md"), "w", encoding="utf-8").write(
    "# Foal sound audition\n\nMade offline from the game's donkey clips (see GK2DonkeyAPI docs/foal-sounds.md). "
    "Peaks matched to the original unless a volume is given.\n\n| File | Length | What |\n|---|---|---|\n" + "\n".join(lines) + "\n")
print("\n".join(lines))
