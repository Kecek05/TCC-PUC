"""Edit the Bisect TD trailer from the raw takes in Recordings/Trailer/raw.

Run:  python -I Assets/_Capture/Montage~/trailer_montage.py
Needs ffmpeg/ffprobe on PATH. Writes everything under Recordings/Trailer/.

Each shot is one or more cuts from a raw take ([file, in, out] in raw seconds). Cuts are hard; captions are
drawn once over the finished body with the game's font (Fredoka Bold), never in the bottom 300 px.
"""
import json
import os
import shutil
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
RAW = os.path.join(ROOT, "Recordings", "Trailer", "raw")
OUT = os.path.join(ROOT, "Recordings", "Trailer")
WORK = os.path.join(OUT, "_work")
FONT = os.path.join(ROOT, "Assets", "Font", "Fredoka", "static", "Fredoka-Bold.ttf")
LOGO = os.path.join(ROOT, "PressKit", "logo", "bisect-td-logo-transparente.png")

W, H, FPS = 1080, 1920, 60

# number, caption, cuts [(raw file stem, in, out)], audio filter (optional)
SHOTS = [
    (1, "UMA CARTA.", [("plano_01_uma-carta", 0.25, 2.25)]),
    (2, "UMA WAVE INTEIRA.", [("plano_01_uma-carta", 2.75, 4.75)]),
    (3, "AGORA, O LADO DELE.", [("plano_03_lado-dele", 0.50, 2.50)]),
    (4, "MANDE O CAOS.", [("plano_03_lado-dele", 2.20, 3.20), ("plano_03_lado-dele", 5.40, 6.40)]),
    (5, "PvP 1v1 EM TEMPO REAL", [("plano_05_hud", 2.20, 5.20)]),
    (6, "Arraste. Posicione. Evolua.", [("plano_06_evolua", 0.80, 3.80)]),
    (7, "Cada wave libera cartas mais fortes.", [("plano_07_teto", 1.20, 4.20)]),
    (8, "Feitiços salvam a sua defesa...", [("plano_08_restia", 0.40, 3.40)]),
    (9, "...ou quebram a dele.", [("plano_09_quebram", 0.50, 1.30), ("plano_09_quebram", 3.80, 6.00)]),
    (10, "Combine cartas.", [("plano_10_combine", 0.20, 4.20)]),
    (11, "Termine a última wave primeiro.", [("plano_11_topo", 1.00, 4.00)]),
    (12, "Monte seu deck de 8 cartas.", [("plano_12_deck", 0.40, 2.40), ("plano_12_deck", 5.00, 7.00)]),
    (13, "30 cartas. Torres, feitiços e ataques.", [
        ("plano_13a_agulha", 4.00, 5.00), ("plano_13b_farol", 2.00, 3.00),
        ("plano_13c_square", 0.80, 1.80), ("plano_13d_estilhaco", 1.60, 2.60)]),
    (14, "Toda carta tem resposta.", [("plano_14_resposta", 0.50, 1.50), ("plano_14_resposta", 3.60, 6.60)]),
    (15, "Evolua jogando. Nada à venda.", [("plano_15_nivel", 1.20, 4.20)]),
    # 16-18 come from the finale take; their times are filled in from its marks (see FINALE below).
]

FINALE = "plano_16_ultima-wave"


def run(args, **kw):
    print(" ".join(str(a) for a in args)[:300])
    subprocess.run(args, check=True, **kw)


def duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                         check=True, capture_output=True, text=True).stdout.strip()
    return float(out)


def cut(stem, t_in, t_out, dst, audio_filter=None):
    src = os.path.join(RAW, stem + ".mp4")
    af = ["-af", audio_filter] if audio_filter else []
    run(["ffmpeg", "-v", "error", "-y", "-ss", f"{t_in:.3f}", "-i", src, "-t", f"{t_out - t_in:.3f}",
         "-vf", f"scale={W}:{H}:flags=lanczos,fps={FPS},setsar=1", *af,
         "-c:v", "libx264", "-preset", "medium", "-crf", "14", "-pix_fmt", "yuv420p",
         "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2", dst])


def outro(dst, seconds=5.0):
    """Shot 19: the logo card over a blurred, darkened frame of the board (shot 5's take), its own quiet audio."""
    src = os.path.join(RAW, "plano_05_hud.mp4")
    title = os.path.join(WORK, "outro_title.txt")
    sub = os.path.join(WORK, "outro_sub.txt")
    with open(title, "w", encoding="utf-8") as f:
        f.write("Bisect TD")
    with open(sub, "w", encoding="utf-8") as f:
        f.write("Tower Defense PvP de cartas")
    font = esc(FONT)
    vf = (f"[0:v]scale={W}:{H},fps={FPS},boxblur=24:2,eq=brightness=-0.18:saturation=0.85,setsar=1[bg];"
          f"[1:v]scale=440:-1,format=rgba,fade=in:st=0.15:d=0.45:alpha=1[logo];"
          f"[bg][logo]overlay=(W-w)/2:560[v1];"
          f"[v1]drawtext=fontfile='{font}':textfile='{esc(title)}':fontsize=124:fontcolor=white:"
          f"x=(w-tw)/2:y=1080:borderw=6:bordercolor=0x14171F:alpha='min(1,max(0,(t-0.35)/0.4))',"
          f"drawtext=fontfile='{font}':textfile='{esc(sub)}':fontsize=58:fontcolor=0x97F2DD:"
          f"x=(w-tw)/2:y=1240:borderw=4:bordercolor=0x14171F:alpha='min(1,max(0,(t-0.6)/0.4))',"
          f"fade=out:st={seconds - 0.6}:d=0.6[v]")
    run(["ffmpeg", "-v", "error", "-y", "-ss", "0.3", "-t", f"{seconds}", "-i", src, "-loop", "1", "-t", f"{seconds}",
         "-i", LOGO, "-filter_complex", vf, "-map", "[v]", "-map", "0:a",
         "-af", f"volume=0.35,afade=in:st=0:d=0.3,afade=out:st={seconds - 1.2}:d=1.2",
         "-c:v", "libx264", "-preset", "medium", "-crf", "14", "-pix_fmt", "yuv420p",
         "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2", dst])


def esc(path):
    """A path inside an ffmpeg filter string: forward slashes, the drive colon escaped."""
    return path.replace("\\", "/").replace(":", "\\:")


def caption_filter(captions):
    """One drawtext per caption, shown between its start and end, fading in, in the safe area."""
    font = esc(FONT)
    parts = []
    for i, (text, start, end) in enumerate(captions):
        if not text:
            continue
        path = os.path.join(WORK, f"cap_{i:02}.txt")
        with open(path, "w", encoding="utf-8") as f:
            f.write(text)
        size = 84 if len(text) <= 18 else 72 if len(text) <= 28 else 62
        alpha = f"min(1,(t-{start:.3f})/0.12)"
        # A band in the game's sheet colour behind the line, like the in-game banners; centred at y=1330,
        # which keeps it clear of the hand (from ~1560) and of the store's bottom 300 px.
        parts.append(
            f"drawbox=x=0:y=1268:w=iw:h=124:color=0x14171F@0.62:t=fill:enable='between(t,{start:.3f},{end:.3f})'")
        parts.append(
            f"drawtext=fontfile='{font}':textfile='{esc(path)}':fontsize={size}:fontcolor=white:"
            f"x=(w-tw)/2:y=1330-th/2:borderw=5:bordercolor=0x14171F:alpha='{alpha}':"
            f"enable='between(t,{start:.3f},{end:.3f})'")
    return ",".join(parts) if parts else "null"


def finale_cuts():
    """Shots 16-18 from the finale take, placed by the marks its log wrote (see finale.json)."""
    with open(os.path.join(OUT, "finale.json"), encoding="utf-8") as f:
        m = json.load(f)
    kill = m["final-kill"]
    victory = m["victory"]
    return [
        (16, "Última wave.", [(FINALE, m["cuts_in"], m["cuts_in"] + 3.0)], None),
        # Slow motion: the last 4 s up to and just past the kill; the audio muffled (low-pass) as in a slow-mo beat.
        (17, "", [(FINALE, kill - 3.3, kill + 0.7)], "lowpass=f=650,volume=0.8"),
        (18, "", [(FINALE, victory + 0.4, victory + 3.4)], None),
    ]


def main():
    os.makedirs(WORK, exist_ok=True)
    shots = [(n, c, cuts, None) for n, c, cuts in SHOTS]
    if os.path.exists(os.path.join(OUT, "finale.json")):
        shots += finale_cuts()
    else:
        print("finale.json missing: shots 16-18 skipped", file=sys.stderr)

    pieces, captions, t = [], [], 0.0
    for n, caption, cuts, af in shots:
        start = t
        for k, (stem, a, b) in enumerate(cuts):
            dst = os.path.join(WORK, f"s{n:02}_{k}.mp4")
            cut(stem, a, b, dst, af)
            pieces.append(dst)
            t += b - a
        captions.append((caption, start, t))

    outro_path = os.path.join(WORK, "s19_0.mp4")
    outro(outro_path)
    pieces.append(outro_path)
    t += 5.0

    listing = os.path.join(WORK, "concat.txt")
    with open(listing, "w", encoding="utf-8") as f:
        for p in pieces:
            f.write(f"file '{p.replace(os.sep, '/')}'\n")

    body = os.path.join(WORK, "body.mp4")
    run(["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", listing, "-c", "copy", body])

    master = os.path.join(OUT, "bisect-td-trailer-60s-ptbr.mp4")
    run(["ffmpeg", "-v", "error", "-y", "-i", body, "-vf", caption_filter(captions),
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-r", str(FPS),
         "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", master])

    store = os.path.join(OUT, "bisect-td-trailer-30s-ptbr.mp4")
    run(["ffmpeg", "-v", "error", "-y", "-i", master, "-t", "30", "-af", "afade=out:st=29.6:d=0.4",
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p",
         "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", store])

    slides = os.path.join(OUT, "bisect-td-trailer-60s-slides.mp4")
    run(["ffmpeg", "-v", "error", "-y", "-i", master, "-vf", "scale=720:1280:flags=lanczos",
         "-c:v", "libx264", "-preset", "slow", "-crf", "26", "-pix_fmt", "yuv420p",
         "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", slides])

    still = os.path.join(OUT, "bisect-td-trailer-still.png")
    run(["ffmpeg", "-v", "error", "-y", "-ss", "3.35", "-i", os.path.join(RAW, "plano_01_uma-carta.mp4"),
         "-frames:v", "1", "-vf", f"scale={W}:{H}", still])

    with open(os.path.join(OUT, "edit-timeline.json"), "w", encoding="utf-8") as f:
        json.dump([{"caption": c, "start": round(a, 3), "end": round(b, 3)} for c, a, b in captions]
                  + [{"caption": "outro", "start": round(t - 5.0, 3), "end": round(t, 3)}], f, ensure_ascii=False, indent=1)

    print(f"total {t:.2f}s")


if __name__ == "__main__":
    main()
