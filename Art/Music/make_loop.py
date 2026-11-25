"""Turns a Suno export (WAV or MP3) into a seamless game loop.

A Suno track is a song with an intro and an ending; a game needs a stretch from the middle that can repeat
forever. This cuts a whole number of bars and crossfades what comes right after the cut into its start, so the
end of the loop flows into its beginning exactly as the song itself would have continued.

    # 1. Find the tempo, where the bars fall, and how loud each stretch is.
    python -I Art/Music/make_loop.py analyze Art/Music/Suno/partida.wav

    # 2. Cut 16 bars starting on a downbeat the analysis listed, and write the loop (+ a 3x preview).
    python -I Art/Music/make_loop.py cut Art/Music/Suno/partida.wav Assets/Audio/Music/music_match.wav \
        --bpm 124 --start 31.45 --bars 16 --preview

Then run Kecek/Juice/Assign Music in Unity. Needs numpy, and ffmpeg on PATH to decode MP3.
"""
import argparse
import math
import shutil
import subprocess
import sys
import wave

import numpy as np

SR = 44100


def decode(path):
    """Any audio file to float32 stereo at 44.1 kHz, through ffmpeg."""
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        sys.exit("ffmpeg is not on PATH.")
    raw = subprocess.run(
        [ffmpeg, "-v", "error", "-i", path, "-f", "f32le", "-acodec", "pcm_f32le", "-ac", "2", "-ar", str(SR), "-"],
        check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype="<f4").reshape(-1, 2).astype(np.float64)


WINDOW = 2048


def onset_envelope(x, hop=512, size=WINDOW):
    """Spectral flux: how much new energy arrives in each frame. Peaks on hits and note starts.

    Frame i is indexed by where its window STARTS, but a hit registers once it reaches the window's middle,
    so a frame's time is i * hop + size / 2: callers add that half window back (see analyze)."""
    mono = x.mean(axis=1)
    frames = 1 + max(0, (len(mono) - size) // hop)
    window = np.hanning(size)
    prev = None
    flux = np.zeros(frames)
    for i in range(frames):
        mag = np.abs(np.fft.rfft(mono[i * hop: i * hop + size] * window))
        mag = np.log1p(mag)
        if prev is not None:
            flux[i] = np.sum(np.maximum(0.0, mag - prev))
        prev = mag
    flux -= flux.mean()
    return np.maximum(flux, 0.0), hop / SR


def estimate_bpm(flux, frame_s, lo=70.0, hi=180.0):
    ac = np.correlate(flux, flux, mode="full")[len(flux) - 1:]
    lags = np.arange(len(ac)) * frame_s
    best, best_bpm = -1.0, None
    for bpm in np.arange(lo, hi, 0.1):
        lag = 60.0 / bpm / frame_s
        i = int(round(lag))
        if i <= 0 or i * 4 >= len(ac):
            continue
        # A tempo is credible when the beat AND the bar repeat; weighting the bar favours the real tempo
        # over its double or half.
        score = ac[i] + 0.5 * ac[int(round(lag * 2))] + 0.5 * ac[int(round(lag * 4))]
        if score > best:
            best, best_bpm = score, bpm
    return best_bpm


def beat_phase(flux, frame_s, bpm):
    """The offset (seconds) of the beat grid that lines up best with the hits, and of the strongest beat in 4."""
    period = 60.0 / bpm
    best, best_off = -1.0, 0.0
    for off in np.arange(0.0, period, frame_s):
        idx = (np.arange(off, len(flux) * frame_s, period) / frame_s).astype(int)
        idx = idx[idx < len(flux)]
        score = flux[idx].sum()
        if score > best:
            best, best_off = score, off
    # Which of the four beats carries the most weight: call it the downbeat.
    sums = []
    for k in range(4):
        idx = (np.arange(best_off + k * period, len(flux) * frame_s, 4 * period) / frame_s).astype(int)
        idx = idx[idx < len(flux)]
        sums.append(flux[idx].sum())
    return best_off, best_off + int(np.argmax(sums)) * period


def db(v):
    return 20.0 * math.log10(max(v, 1e-9))


def analyze(args):
    x = decode(args.input)
    flux, frame_s = onset_envelope(x)
    bpm = args.bpm or estimate_bpm(flux, frame_s)
    _, downbeat = beat_phase(flux, frame_s, bpm)
    downbeat += WINDOW / 2 / SR
    bar = 4 * 60.0 / bpm
    print(f"duration {len(x) / SR:.2f} s, tempo ~{bpm:.1f} BPM, a bar is {bar:.3f} s, first downbeat at {downbeat:.3f} s")
    print("(the downbeat is a guess: it can land one beat off. Listen to the --preview of the cut to be sure.)")
    print("4-bar stretches (start s, RMS dBFS). Pick a run of steady, similar levels away from the intro and ending:")
    t = downbeat
    while t + 4 * bar <= len(x) / SR:
        seg = x[int(t * SR): int((t + 4 * bar) * SR)]
        print(f"  {t:8.3f}  {db(math.sqrt(float(np.mean(seg ** 2)))):6.1f}")
        t += 4 * bar


def cut(args):
    x = decode(args.input)
    bar = 4 * 60.0 / args.bpm
    loop_len = int(round(args.bars * bar * SR))
    fade = int(round(args.fade_beats * 60.0 / args.bpm * SR))
    start = int(round(args.start * SR))
    if start + loop_len + fade > len(x):
        sys.exit("The loop runs past the end of the track: start earlier or cut fewer bars.")

    loop = x[start: start + loop_len].copy()
    after = x[start + loop_len: start + loop_len + fade]
    # The head of the loop fades from what follows the cut into the loop's own start (equal power), so the
    # last sample flows into the first exactly as the song would have gone on.
    t = np.linspace(0.0, math.pi / 2.0, fade)[:, None]
    loop[:fade] = loop[:fade] * np.sin(t) + after * np.cos(t)

    rms = math.sqrt(float(np.mean(loop ** 2))) + 1e-9
    loop *= 10 ** (args.target_db / 20.0) / rms
    peak = float(np.max(np.abs(loop)))
    if peak > 0.95:
        loop = np.tanh(loop / peak * 1.2) / math.tanh(1.2) * 0.95

    write(args.output, loop)
    print(f"{args.output}: {args.bars} bars, {loop_len / SR:.3f} s, RMS {args.target_db:.0f} dBFS")
    if args.preview:
        preview = args.output.rsplit(".", 1)[0] + "_preview3x.wav"
        write(preview, np.concatenate([loop, loop, loop]))
        print(f"{preview}: three passes, to listen for the seam (do not import this one).")


def write(path, buf):
    data = (np.clip(buf, -1.0, 1.0) * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)

    a = sub.add_parser("analyze", help="tempo, downbeat and the loudness of each 4-bar stretch")
    a.add_argument("input")
    a.add_argument("--bpm", type=float, help="skip the tempo estimate when you know it")
    a.set_defaults(func=analyze)

    c = sub.add_parser("cut", help="write a seamless loop")
    c.add_argument("input")
    c.add_argument("output")
    c.add_argument("--bpm", type=float, required=True)
    c.add_argument("--start", type=float, required=True, help="seconds, on a downbeat")
    c.add_argument("--bars", type=int, default=16)
    c.add_argument("--fade-beats", type=float, default=1.0, help="length of the seam crossfade, in beats")
    c.add_argument("--target-db", type=float, default=-18.0, help="RMS level, so menu and match sit at the same loudness")
    c.add_argument("--preview", action="store_true", help="also write the loop three times over")
    c.set_defaults(func=cut)

    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
