"""Join the gameplay take's segments into the presentation video.

Run:  python -I Assets/_Capture/Montage~/gameplay_montage.py <marks.json>
The segments are Recordings/Gameplay/raw/gameplay_0N_*.mp4 in name order; the gaps between them are the dry
cuts. marks.json is {"<segment file stem>": [[seconds, "label"], ...]} from the capture log. No captions, no
music: the video is narrated live. Writes the master, the slides version, the still and marcacoes.txt.
"""
import glob
import json
import os
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
RAW = os.path.join(ROOT, "Recordings", "Gameplay", "raw")
OUT = os.path.join(ROOT, "Recordings", "Gameplay")
WORK = os.path.join(OUT, "_work")
SLIDES_LIMIT = 20 * 1024 * 1024


def run(args):
    print(" ".join(str(a) for a in args)[:240])
    subprocess.run(args, check=True)


def duration(path):
    return float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                                check=True, capture_output=True, text=True).stdout.strip())


# What the narrator reads for each beat the take marked.
LABELS = {
    "1 menu e deck": "1. Menu: a tela de deck com os 8 slots, troca rápida entre dois decks salvos e volta",
    "2 vertice na curva": "2. Teto baixo: só cartas baratas cabem; Vértice posicionada numa curva do caminho",
    "3 evolucao nivel 2": "3. Evolução: outra Vértice solta sobre a mesma torre, que sobe para o nível 2",
    "4 teto sobe": "4. Fim da wave 4: o teto de mana sobe (+1 MAX) e o Farol entra no ciclo (próxima carta)",
    "5 swipe para o rival": "5. Swipe para o rival: o tabuleiro dele",
    "5 ice nas torres": "5. Ice congela as torres dele",
    "6 restia": "6. De volta ao próprio tabuleiro: Réstia numa wave em fila",
    "5 enxame": "5. Swipe de novo: o Enxame solto no campo dele (vai até a base)",
    "7 ultimas waves": "7. Últimas waves",
    "7 vitoria e recompensas": "7. Vitória e recompensas (moedas e cópias de carta), tela parada",
}


def mmss(t):
    return f"{int(t // 60):02}:{int(round(t % 60)):02}" if round(t % 60) < 60 else f"{int(t // 60) + 1:02}:00"


def main():
    marks = {}
    if len(sys.argv) > 1:
        with open(sys.argv[1], encoding="utf-8") as f:
            marks = json.load(f)

    os.makedirs(WORK, exist_ok=True)
    segments = sorted(glob.glob(os.path.join(RAW, "gameplay_0*.mp4")))
    if not segments:
        sys.exit("no segments in " + RAW)

    # The match runs as one take; if the whole video would pass 3 minutes, the stretch between the
    # "~corte-inicio" and "~corte-fim" marks (early waves of plain defence) is dropped with one dry cut,
    # trimmed only as much as needed to land near 2:50.
    raw_total = sum(duration(s) for s in segments)
    budget = 170.0

    # Normalise every piece to the same stream layout so the concat is a plain copy.
    pieces, offset, timeline = [], 0.0, []
    for seg in segments:
        stem = os.path.splitext(os.path.basename(seg))[0]
        seg_marks = marks.get(stem, [])
        spans = [(0.0, duration(seg))]
        start = next((t for t, l in seg_marks if l == "~corte-inicio"), None)
        stop = next((t for t, l in seg_marks if l == "~corte-fim"), None)
        if raw_total > 180.0 and start is not None and stop is not None and stop > start:
            drop = min(stop - start, raw_total - budget)
            spans = [(0.0, stop - drop), (stop, spans[0][1])]

        for k, (a, b) in enumerate(spans):
            dst = os.path.join(WORK, f"{stem}_{k}.mp4")
            run(["ffmpeg", "-v", "error", "-y", "-ss", f"{a:.3f}", "-i", seg, "-t", f"{b - a:.3f}",
                 "-vf", "scale=1080:1920:flags=lanczos,fps=60,setsar=1",
                 "-c:v", "libx264", "-preset", "medium", "-crf", "14", "-pix_fmt", "yuv420p",
                 "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2", dst])
            pieces.append(dst)
            for t, label in seg_marks:
                if label.startswith("~") or not (a <= float(t) < b):
                    continue
                timeline.append((offset + float(t) - a, label))
            timeline.append((offset, f"[corte seco] {stem}" + (f" parte {k + 1}" if len(spans) > 1 else "")))
            offset += b - a

    listing = os.path.join(WORK, "concat.txt")
    with open(listing, "w", encoding="utf-8") as f:
        for p in pieces:
            f.write(f"file '{p.replace(os.sep, '/')}'\n")

    body = os.path.join(WORK, "body.mp4")
    run(["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", listing, "-c", "copy", body])

    master = os.path.join(OUT, "bisect-td-gameplay-ptbr.mp4")
    run(["ffmpeg", "-v", "error", "-y", "-i", body, "-c:v", "libx264", "-preset", "slow", "-crf", "18",
         "-pix_fmt", "yuv420p", "-r", "60", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", master])

    slides = os.path.join(OUT, "bisect-td-gameplay-slides.mp4")
    for crf, fps in ((26, 60), (28, 60), (28, 30)):
        run(["ffmpeg", "-v", "error", "-y", "-i", master, "-vf", f"scale=720:1280:flags=lanczos,fps={fps}",
             "-c:v", "libx264", "-preset", "slow", "-crf", str(crf), "-pix_fmt", "yuv420p",
             "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", slides])
        if os.path.getsize(slides) <= SLIDES_LIMIT:
            break

    # The still: the middle of the match, our field full of towers with a line of enemies under the Lance's aim.
    still_at = next((t for t, label in timeline if label.startswith("6 restia")), offset / 2)
    run(["ffmpeg", "-v", "error", "-y", "-ss", f"{max(0.0, still_at + 0.3):.2f}", "-i", master, "-frames:v", "1",
         os.path.join(OUT, "bisect-td-gameplay-still.png")])

    timeline.sort()
    with open(os.path.join(OUT, "marcacoes.txt"), "w", encoding="utf-8") as f:
        f.write(f"Bisect TD - gameplay ({mmss(offset)} no total)\n\n")
        for t, label in timeline:
            f.write(f"{mmss(t)}  {LABELS.get(label, label)}\n")

    print(f"total {offset:.2f}s, slides {os.path.getsize(slides) / 1048576:.1f} MB")


if __name__ == "__main__":
    main()
