"""Placeholder WAVs for the Spell Juice cues: small synth recipes, one voice per spell. Pure Python (no numpy)."""
import math
import os
import random
import struct
import sys
import wave

RATE = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "."
random.seed(7)


def env(t, attack, decay_total, curve=3.0):
    if t < attack:
        return t / attack if attack > 0 else 1.0
    x = (t - attack) / max(1e-6, decay_total - attack)
    return max(0.0, 1.0 - x) ** curve


class LowPass:
    def __init__(self):
        self.y = 0.0

    def __call__(self, x, cutoff):
        a = 1.0 - math.exp(-2.0 * math.pi * max(20.0, cutoff) / RATE)
        self.y += a * (x - self.y)
        return self.y


class HighPass:
    def __init__(self):
        self.lp = LowPass()

    def __call__(self, x, cutoff):
        return x - self.lp(x, cutoff)


def write(name, samples, gain=0.9):
    peak = max(1e-6, max(abs(s) for s in samples))
    scale = gain / peak
    path = os.path.join(OUT, f"sfx_{name}.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        frames = bytearray()
        fade = int(RATE * 0.004)
        n = len(samples)
        for i, s in enumerate(samples):
            k = 1.0
            if i < fade:
                k = i / fade
            elif i > n - fade:
                k = (n - i) / fade
            v = int(max(-1.0, min(1.0, s * scale * k)) * 32767)
            frames += struct.pack("<h", v)
        w.writeframes(bytes(frames))
    print("wrote", path)


def render(duration, fn):
    return [fn(i / RATE) for i in range(int(duration * RATE))]


def ignite():
    lp, lp2 = LowPass(), LowPass()
    def f(t):
        n = random.uniform(-1, 1)
        cutoff = 400 + 5200 * min(1.0, t / 0.12) * math.exp(-t * 3)
        roar = lp(n, cutoff) * env(t, 0.03, 0.45, 2.2)
        thump = math.sin(2 * math.pi * (90 - 50 * t) * t) * env(t, 0.005, 0.25, 3) * 0.8
        crackle = (random.random() < 0.004) * random.uniform(-1, 1) * env(t, 0.02, 0.45, 1.5)
        return roar + thump + lp2(crackle, 6000) * 2.5
    return render(0.45, f)


def freeze():
    hp, hp2 = HighPass(), HighPass()
    partials = [(2350, 0.5), (3520, 0.35), (4710, 0.25), (1760, 0.4)]
    def f(t):
        burst = hp(random.uniform(-1, 1), 3500) * env(t, 0.002, 0.18, 2.5) * 0.9
        chime = sum(a * math.sin(2 * math.pi * fr * (1 - 0.06 * t) * t) for fr, a in partials) * env(t, 0.004, 0.6, 2.2)
        crack = (random.random() < 0.02 * math.exp(-t * 6)) * random.uniform(-1, 1)
        return burst + chime * 0.6 + hp2(crack, 2000) * 1.5
    return render(0.6, f)


def shatter():
    hp = HighPass()
    tinks = [(random.uniform(0.0, 0.25), random.uniform(2500, 6500), random.uniform(0.2, 0.6)) for _ in range(14)]
    def f(t):
        noise = hp(random.uniform(-1, 1), 2800) * env(t, 0.001, 0.22, 3) * 0.8
        tink = 0.0
        for start, fr, a in tinks:
            if t >= start:
                dt = t - start
                tink += a * math.sin(2 * math.pi * fr * dt) * math.exp(-dt * 38)
        return noise + tink * 0.7
    return render(0.5, f)


def zap():
    lp = LowPass()
    phase = [0.0]
    def f(t):
        fr = 110 + random.uniform(-40, 60)
        phase[0] += fr / RATE
        saw = 2 * (phase[0] % 1.0) - 1
        buzz = (1 if math.sin(2 * math.pi * 47 * t) > 0 else -1) * 0.3
        noise = random.uniform(-1, 1) * 0.5
        return lp(saw + buzz + noise, 4200) * env(t, 0.002, 0.16, 1.6)
    return render(0.16, f)


def pierce():
    def f(t):
        fr = 1900 * math.exp(-t * 4.0) + 700
        ring = math.sin(2 * math.pi * fr * t) + 0.4 * math.sin(2 * math.pi * fr * 2.01 * t)
        click = random.uniform(-1, 1) * env(t, 0.0005, 0.01, 1)
        return ring * env(t, 0.003, 0.2, 2.4) + click * 0.6
    return render(0.2, f)


def rumble():
    lp, lp2 = LowPass(), LowPass()
    def f(t):
        brown = lp(random.uniform(-1, 1), 160)
        trem = 0.6 + 0.4 * math.sin(2 * math.pi * 11 * t)
        sub = math.sin(2 * math.pi * 46 * t) * 0.6
        grit = lp2(random.uniform(-1, 1), 900) * 0.25
        return (brown * 6 * trem + sub + grit) * env(t, 0.06, 0.85, 1.8)
    return render(0.85, f)


def hiss():
    hp = HighPass()
    bubbles = [(random.uniform(0.05, 0.6), random.uniform(500, 1100)) for _ in range(7)]
    def f(t):
        h = hp(random.uniform(-1, 1), 3200) * env(t, 0.08, 0.7, 1.6) * 0.7
        pop = 0.0
        for start, fr in bubbles:
            if t >= start:
                dt = t - start
                pop += math.sin(2 * math.pi * fr * (1 + 3 * dt) * dt) * math.exp(-dt * 45)
        return h + pop * 0.35
    return render(0.7, f)


def gust():
    lp1, lp2 = LowPass(), LowPass()
    def f(t):
        center = 300 + 1100 * math.sin(math.pi * min(1.0, t / 0.6))
        n = random.uniform(-1, 1)
        band = lp1(n, center * 1.6) - lp2(n, center * 0.6)
        return band * 2.4 * env(t, 0.12, 0.6, 1.4)
    return render(0.6, f)


def seal():
    lp = LowPass()
    def f(t):
        cutoff = 3200 * math.exp(-t * 9) + 200
        zipn = lp(random.uniform(-1, 1), cutoff) * env(t, 0.005, 0.22, 2)
        thud = math.sin(2 * math.pi * (120 - 160 * t) * t) * env(t, 0.003, 0.18, 3) * 0.7
        return zipn + thud
    return render(0.3, f)


def windup():
    clicks = []
    t = 0.0
    gap = 0.07
    while t < 0.42:
        clicks.append(t)
        t += gap
        gap = max(0.022, gap * 0.82)
    def f(t):
        c = 0.0
        for start in clicks:
            if t >= start:
                dt = t - start
                c += math.sin(2 * math.pi * 3200 * dt) * math.exp(-dt * 260)
        rise = math.sin(2 * math.pi * (380 + 900 * t) * t) * 0.35 * env(t, 0.05, 0.5, 1.2)
        return c * 0.8 + rise
    return render(0.5, f)


os.makedirs(OUT, exist_ok=True)
write("spellignite", ignite())
write("spellfreeze", freeze())
write("spellshatter", shatter())
write("spellzap", zap(), 0.8)
write("spellpierce", pierce(), 0.8)
write("spellrumble", rumble())
write("spellhiss", hiss(), 0.75)
write("spellgust", gust(), 0.85)
write("spellseal", seal(), 0.8)
write("spellwindup", windup(), 0.85)
