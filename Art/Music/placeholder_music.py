"""Placeholder music loops for Minimalist Defense, until the Suno tracks arrive.

Writes music_menu.wav (calm, 95 BPM) and music_match.wav (driving, 124 BPM): 8 bars each of minimal synth
over Am - F - C - G. Each loop is seamless by construction: it is rendered with a tail and the tail is folded
back onto the start, so a note still ringing at the loop point carries on into the next pass.

    python -I Art/Music/placeholder_music.py Assets/Audio/Music/Placeholder

Needs numpy only.
"""
import math
import os
import sys
import wave

import numpy as np

SR = 44100
TAIL_SECONDS = 3.0

# Am - F - C - G, two bars each. MIDI numbers of the chord tones, root first.
CHORDS = [(57, 60, 64), (53, 57, 60), (48, 52, 55), (55, 59, 62)]


def hz(midi):
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def env_exp(n, tau):
    t = np.arange(n) / SR
    return np.exp(-t / tau)


def env_adsr(n, attack, release):
    e = np.ones(n)
    a = min(n, int(attack * SR))
    r = min(n, int(release * SR))
    if a > 0:
        e[:a] = np.linspace(0.0, 1.0, a)
    if r > 0:
        e[-r:] *= np.linspace(1.0, 0.0, r)
    return e


def tone(freq, n, harmonics=((1, 1.0),), detune_cents=0.0, phase=0.0):
    t = np.arange(n) / SR
    f = freq * 2.0 ** (detune_cents / 1200.0)
    out = np.zeros(n)
    for k, amp in harmonics:
        out += amp * np.sin(2 * math.pi * f * k * t + phase * k)
    return out


def add(buf, start, sig, pan=0.0, gain=1.0):
    """Mix a mono signal into a stereo buffer at a sample offset, with an equal-power pan in -1..1."""
    end = min(len(buf), start + len(sig))
    if end <= start:
        return
    sig = sig[: end - start] * gain
    angle = (pan + 1.0) * math.pi / 4.0
    buf[start:end, 0] += sig * math.cos(angle)
    buf[start:end, 1] += sig * math.sin(angle)


def noise_hat(n, tau, seed):
    rng = np.random.default_rng(seed)
    x = rng.standard_normal(n)
    # One-pole low-pass, subtracted: a crude high-pass that keeps the hiss and drops the thump.
    lp = np.zeros(n)
    a = 0.15
    for i in range(1, n):
        lp[i] = lp[i - 1] + a * (x[i] - lp[i - 1])
    return (x - lp) * env_exp(n, tau)


def kick(n):
    t = np.arange(n) / SR
    freq = 45.0 + 105.0 * np.exp(-t / 0.035)
    phase = 2 * math.pi * np.cumsum(freq) / SR
    return np.sin(phase) * env_exp(n, 0.22)


def fold(buf, loop_len):
    """Fold everything past the loop point back onto the start, so the loop is seamless."""
    out = buf[:loop_len].copy()
    tail = buf[loop_len:]
    out[: len(tail)] += tail[:loop_len]
    return out


def finish(buf, target_db=-18.0):
    rms = math.sqrt(float(np.mean(buf ** 2))) + 1e-9
    buf = buf * (10 ** (target_db / 20.0) / rms)
    return np.tanh(buf * 1.1) / math.tanh(1.1) * 0.95  # gentle limiter: no clipping, peaks under 0 dBFS


def write_wav(path, buf):
    data = (np.clip(buf, -1.0, 1.0) * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def render_menu():
    bpm = 95.0
    beat = 60.0 / bpm
    bars = 8
    loop_len = int(round(bars * 4 * beat * SR))
    buf = np.zeros((loop_len + int(TAIL_SECONDS * SR), 2))

    for bar in range(bars):
        chord = CHORDS[bar // 2]
        bar_start = int(bar * 4 * beat * SR)

        # Pad: the chord, held for two bars, with a slow swell and release; detuned left and right.
        if bar % 2 == 0:
            n = int(2 * 4 * beat * SR + 1.2 * SR)
            e = env_adsr(n, 0.8, 1.6)
            for m in chord:
                add(buf, bar_start, tone(hz(m), n, ((1, 1.0), (2, 0.15)), -4) * e, -0.4, 0.05)
                add(buf, bar_start, tone(hz(m), n, ((1, 1.0), (2, 0.15)), 4) * e, 0.4, 0.05)

        # Bass: the root on beats 1 and 3.
        for b in (0, 2):
            n = int(1.4 * SR)
            add(buf, bar_start + int(b * beat * SR), tone(hz(chord[0] - 12), n, ((1, 1.0), (2, 0.2))) * env_exp(n, 0.5), 0.0, 0.16)

        # Pluck arpeggio in eighths, ping-ponging across the stereo field.
        pattern = [0, 1, 2, 1, 0, 2, 1, 2]
        for i, step in enumerate(pattern):
            n = int(0.9 * SR)
            note = chord[step] + 12
            velocity = 1.0 if i % 2 == 0 else 0.7
            sig = tone(hz(note), n, ((1, 1.0), (2, 0.25), (3, 0.08))) * env_exp(n, 0.22)
            add(buf, bar_start + int(i * beat / 2 * SR), sig, -0.35 if i % 2 == 0 else 0.35, 0.1 * velocity)

        # A soft tick on the off-beats.
        for b in range(4):
            n = int(0.06 * SR)
            add(buf, bar_start + int((b + 0.5) * beat * SR), noise_hat(n, 0.012, bar * 4 + b), 0.2, 0.015)

    return finish(fold(buf, loop_len))


def render_match():
    bpm = 124.0
    beat = 60.0 / bpm
    bars = 8
    loop_len = int(round(bars * 4 * beat * SR))
    buf = np.zeros((loop_len + int(TAIL_SECONDS * SR), 2))
    sixteenth = beat / 4

    for bar in range(bars):
        chord = CHORDS[bar // 2]
        bar_start = int(bar * 4 * beat * SR)

        if bar % 2 == 0:
            n = int(2 * 4 * beat * SR + 0.8 * SR)
            e = env_adsr(n, 0.4, 1.0)
            for m in chord:
                add(buf, bar_start, tone(hz(m), n, ((1, 1.0), (2, 0.1)), -5) * e, -0.5, 0.03)
                add(buf, bar_start, tone(hz(m), n, ((1, 1.0), (2, 0.1)), 5) * e, 0.5, 0.03)

        for b in range(4):
            beat_start = bar_start + int(b * beat * SR)
            add(buf, beat_start, kick(int(0.45 * SR)), 0.0, 0.42)
            if b in (1, 3):
                n = int(0.18 * SR)
                add(buf, beat_start, noise_hat(n, 0.05, 1000 + bar * 4 + b), 0.0, 0.05)

        # Bass on the eighths, ducked right after each kick so the two do not mask each other.
        for i in range(8):
            n = int(0.3 * SR)
            duck = 0.55 if i % 2 == 0 else 1.0
            sig = tone(hz(chord[0] - 12), n, ((1, 1.0), (2, 0.5), (3, 0.25), (4, 0.12))) * env_exp(n, 0.12)
            add(buf, bar_start + int(i * beat / 2 * SR), sig, 0.0, 0.11 * duck)

        # Arpeggio in sixteenths.
        pattern = [0, 1, 2, 1, 2, 0, 1, 2, 0, 2, 1, 2, 0, 1, 2, 1]
        for i, step in enumerate(pattern):
            n = int(0.4 * SR)
            note = chord[step] + 12
            velocity = 1.0 if i % 4 == 0 else 0.65
            sig = tone(hz(note), n, ((1, 1.0), (2, 0.3), (3, 0.1))) * env_exp(n, 0.1)
            add(buf, bar_start + int(i * sixteenth * SR), sig, -0.3 if i % 2 == 0 else 0.3, 0.075 * velocity)

        # Hats on every sixteenth, the off-beat eighths accented.
        for i in range(16):
            n = int(0.05 * SR)
            accent = 1.0 if i % 4 == 2 else 0.45
            add(buf, bar_start + int(i * sixteenth * SR), noise_hat(n, 0.01, 2000 + bar * 16 + i), 0.25, 0.03 * accent)

    return finish(fold(buf, loop_len))


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out_dir, exist_ok=True)
    for name, render in (("music_menu.wav", render_menu), ("music_match.wav", render_match)):
        buf = render()
        path = os.path.join(out_dir, name)
        write_wav(path, buf)
        print(f"{path}: {len(buf) / SR:.2f} s, peak {20 * math.log10(np.max(np.abs(buf)) + 1e-9):.1f} dBFS")


if __name__ == "__main__":
    main()
