"""Arranges Suno/Trailer.wav onto the trailer's timeline: exactly 120 BPM, a bar every 2 s.

The trailer's cuts fall on whole seconds (Recordings/Trailer/edit-timeline.json), and at 120 BPM a bar is 2 s,
so every cut lands on a beat. Suno does not follow a timed structure and drifts in tempo (this track runs
~121.6 BPM, wandering by tens of milliseconds within a section), so each piece of the arrangement is cut at
its own locally fitted beat grid and time-stretched (rubberband, ffmpeg) to exactly its length in 2 s bars.

    python -I Art/Music/trailer_music.py                 # writes Recordings/Trailer/music/trailer_music.wav
    python -I Art/Music/trailer_music.py --check         # also reports where the downbeats landed

Needs numpy and an ffmpeg with the rubberband filter on PATH.
"""
import argparse
import math
import os
import shutil
import subprocess
import sys
import tempfile
import wave

import numpy as np

SR = 48000
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Art", "Music", "Suno", "Trailer.wav")
OUT = os.path.join(ROOT, "Recordings", "Trailer", "music", "trailer_music.wav")

TARGET_BAR = 2.0            # seconds: 120 BPM
SOURCE_BPM = 121.624        # the track's average tempo, a starting point for the local fits
SOURCE_PHASE = 0.037        # where its first beat falls (s)
LENGTH = 60.0               # the trailer master's length

# (source bar from, source bar to (exclusive) or None for "to the end of the file"), placed at target bar.
# Source bars count from SOURCE_PHASE in bars of 4 beats at SOURCE_BPM. Target bars are 2 s each from 0:00.
ARRANGEMENT = [
    # 0:00  the hit                                  "UMA CARTA."
    ((2, 3), 0),
    # 0:02  near silence: the music drops out        "UMA WAVE INTEIRA."
    ((1, 2), 1),
    # 0:04  the riser, then the full beat from 0:06  "AGORA, O LADO DELE." -> "MANDE O CAOS." ...
    ((3, 12), 2),
    # 0:22  the full beat carries on (the second drop's opening bars)
    ((16, 18), 11),
    # 0:26  the lift: the brighter part              "Termine a última wave primeiro."
    ((24, 26), 13),
    # 0:30  light breakdown, its riser at 0:32       "Monte seu deck de 8 cartas."
    ((14, 16), 15),
    # 0:34  full beat again                          cards, answers, progression
    ((16, 21), 17),
    # From 0:44 to the end the track plays its own closing run, bars 28 to the end, uncut: each piece is only
    # fitted to its own beat grid, so the downbeats under the cuts stay exact.
    # 0:44  the brighter part                        "Última wave."
    ((28, 30), 22),
    # 0:48  the music thins (no bass) and rises     the final blow: tension, not silence (the game has no
    #       slow motion yet, so the trailer does not pretend it does)
    ((30, 32), 24),
    # 0:52  the two strongest bars land with        VICTORY
    ((32, 34), 26),
    # 0:56  the ending, resolving under the logo and ringing out with the video
    ((34, None), 28),
]

# Measured downbeats of Suno/Trailer.wav (seconds), for every bar the arrangement cuts at. With both ends of a
# piece known, its stretch is exact and nothing is estimated. Bars 1-26: where a full-band and a kick-band grid
# fit agree (within 16 ms). Bars 28-34: read off the kick envelope, because there the two fits disagree in both
# directions: the hi-hats between the beats pull the full-band fit half a beat late in bars 28-29, and after
# bar 30 the bass pumps between the kicks and pulls the kick-band fit off. These four sit exactly 8 beats
# (3.932 s) apart. A bar missing here falls back to fitting (see locate).
DOWNBEATS = {
    1: 1.999, 2: 3.978, 3: 5.957, 12: 23.717, 14: 27.650, 16: 31.588, 18: 35.524, 21: 41.430,
    24: 47.330, 26: 51.264, 28: 55.207, 30: 59.139, 32: 63.071, 34: 67.003,
}

PRE_ROLL = 0.0              # the segment starts exactly on its downbeat
POST_ROLL = 0.06            # source seconds kept past a segment's end, faded out under the next one
FADE_IN = 0.003             # keeps the next downbeat's attack while killing the click
FADE_OUT = 0.04


def decode(path):
    ffmpeg = shutil.which("ffmpeg") or sys.exit("ffmpeg is not on PATH.")
    raw = subprocess.run([ffmpeg, "-v", "error", "-i", path, "-f", "f32le", "-acodec", "pcm_f32le", "-ac", "2",
                          "-ar", str(SR), "-"], check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype="<f4").reshape(-1, 2).astype(np.float64)


def write(path, buf):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = (np.clip(buf, -1.0, 1.0) * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def onset_flux(x, hop=128, size=1024, below_hz=None):
    """Spectral flux, and the time of each frame (a hit registers at the window's middle).

    below_hz keeps only the low band: the kick. In this track's brighter sections the hi-hats between the
    beats carry more full-band flux than the kick, and a grid fitted to the full band locks half a beat off
    (bars 28-29 came out 0.2 s late, between two kicks)."""
    mono = x.mean(axis=1)
    frames = 1 + max(0, (len(mono) - size) // hop)
    window = np.hanning(size)
    keep = np.fft.rfftfreq(size, 1 / SR) < below_hz if below_hz else slice(None)
    flux = np.zeros(frames)
    prev = None
    for i in range(frames):
        mag = np.log1p(np.abs(np.fft.rfft(mono[i * hop: i * hop + size] * window))[keep])
        if prev is not None:
            flux[i] = np.sum(np.maximum(0.0, mag - prev))
        prev = mag
    times = (np.arange(frames) * hop + size / 2) / SR
    return flux, times


def local_grid(flux, times, t_from, t_to):
    """The beat period and phase that best fit the hits between t_from and t_to."""
    frame = times[1] - times[0]
    best = (-1.0, None, None)
    base = 60.0 / SOURCE_BPM
    for period in np.arange(base * 0.985, base * 1.015, 0.0002):
        for phase in np.arange(0.0, period, frame):
            ts = np.arange(t_from + phase, t_to, period)
            idx = np.searchsorted(times, ts).clip(0, len(flux) - 1)
            score = flux[idx].mean()
            if score > best[0]:
                best = (score, period, t_from + phase)
    return best[1], best[2]


def bar_time(k):
    return SOURCE_PHASE + k * 4 * 60.0 / SOURCE_BPM


def locate(onsets, bar_from, bar_to, duration):
    """Exact source start and end of a run of bars, from a beat grid fitted to the run (plus a bar either side).

    The grid follows the kick; only a stretch with no kick at all (an intro, a breakdown) falls back to the
    full band. The bar either side usually brings a kick into the window even for a breakdown. Bars listed in
    DOWNBEATS skip the fit: measured beats beat estimated ones."""
    if bar_from in DOWNBEATS and (bar_to is None or bar_to in DOWNBEATS):
        start = DOWNBEATS[bar_from]
        if bar_to is not None:
            end = DOWNBEATS[bar_to]
            return start, end, (end - start) / ((bar_to - bar_from) * 4)
        # The ending keeps the tempo of the bars before it.
        before = max(b for b in DOWNBEATS if b < bar_from)
        return start, duration, (start - DOWNBEATS[before]) / ((bar_from - before) * 4)

    (kick, kick_t), (full, full_t) = onsets
    t_from = max(0.0, bar_time(bar_from - 1))
    t_to = min(duration, bar_time((bar_to if bar_to is not None else bar_from + 2) + 1))
    i0, i1 = np.searchsorted(kick_t, [t_from, t_to])
    has_kick = kick[i0:i1].max() > 4.0 * np.median(kick[kick > 0]) if i1 > i0 else False
    flux, times = (kick, kick_t) if has_kick else (full, full_t)
    period, phase = local_grid(flux, times, t_from, t_to)
    # The fitted beat nearest the average grid's downbeat is this run's real downbeat (they differ by tens of
    # milliseconds, a beat is ~490 ms, so the nearest one is never the wrong beat).
    start = phase + round((bar_time(bar_from) - phase) / period) * period
    end = duration if bar_to is None else start + (bar_to - bar_from) * 4 * period
    return start, end, period


def stretch(segment, tempo, tmp):
    src, dst = os.path.join(tmp, "in.wav"), os.path.join(tmp, "out.wav")
    write(src, segment)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", src, "-af",
                    f"rubberband=tempo={tempo:.6f}:transients=crisp:detector=compound", "-ar", str(SR), dst],
                   check=True)
    return decode(dst)


def render(check=False):
    x = decode(SOURCE)
    duration = len(x) / SR
    onsets = (onset_flux(x, size=2048, below_hz=120), onset_flux(x))
    out = np.zeros((int((LENGTH + 2.0) * SR), 2))

    with tempfile.TemporaryDirectory() as tmp:
        for (bar_from, bar_to), target_bar in ARRANGEMENT:
            start, end, period = locate(onsets, bar_from, bar_to, duration)
            if bar_to is None:
                # The ending keeps its own length: stretched at its local tempo, it rings out as long as it does.
                tempo = (4 * period) / TARGET_BAR
                target_len = (end - start) / tempo
            else:
                target_len = (bar_to - bar_from) * TARGET_BAR
                tempo = (end - start) / target_len

            piece = x[int(start * SR): int(min(duration, end + POST_ROLL) * SR)]
            body = stretch(piece, tempo, tmp)

            n_body = int(round(target_len * SR))
            n_tail = int(round(FADE_OUT * SR))
            body = body[: n_body + n_tail]
            env = np.ones(len(body))
            n_in = int(FADE_IN * SR)
            env[:n_in] = np.linspace(0.0, 1.0, n_in)
            if len(body) > n_body:
                env[n_body:] = np.linspace(1.0, 0.0, len(body) - n_body)
            body = body * env[:, None]

            at = int(round(target_bar * TARGET_BAR * SR))
            end_at = min(len(out), at + len(body))
            out[at:end_at] += body[: end_at - at]
            print(f"  source bars {bar_from:2d}-{bar_to if bar_to is not None else 'end':>3}: {start:7.3f}-{end:7.3f} s "
                  f"(beat {period * 1000:6.2f} ms) -> {target_bar * TARGET_BAR:5.1f}-{target_bar * TARGET_BAR + target_len:5.1f} s, "
                  f"stretch x{1 / tempo:.4f}")

    out = out[: int(LENGTH * SR)]
    fade = int(0.4 * SR)
    out[-fade:] *= np.linspace(1.0, 0.0, fade)[:, None]

    # Level: -16 dBFS RMS over the parts that play, then a soft ceiling. The mix sets the music's final level.
    playing = np.abs(out).max(axis=1) > 1e-4
    rms = math.sqrt(float(np.mean(out[playing] ** 2))) + 1e-9
    out *= 10 ** (-16.0 / 20.0) / rms
    peak = float(np.max(np.abs(out)))
    if peak > 0.95:
        out = np.tanh(out / peak * 1.2) / math.tanh(1.2) * 0.95
    write(OUT, out)
    print(f"{OUT}: {len(out) / SR:.3f} s")

    if check:
        flux2, times2 = onset_flux(out)
        print("downbeats in the result (expected on even seconds):")
        for t in (0, 4, 6, 8, 14, 22, 26, 30, 32, 34, 42, 44, 48, 52, 56):
            i0, i1 = np.searchsorted(times2, [t - 0.06, t + 0.06])
            if i1 <= i0:
                continue
            j = i0 + int(np.argmax(flux2[i0:i1]))
            print(f"  {t:5.1f} s -> hit at {times2[j]:7.3f} s ({1000 * (times2[j] - t):+5.1f} ms)")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--check", action="store_true")
    render(p.parse_args().check)


if __name__ == "__main__":
    main()
