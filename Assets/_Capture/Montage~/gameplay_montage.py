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

    # Normalise every segment to the same stream layout so the concat is a plain copy.
    pieces, offset, timeline = [], 0.0, []
    for seg in segments:
        stem = os.path.splitext(os.path.basename(seg))[0]
        dst = os.path.join(WORK, stem + ".mp4")
        run(["ffmpeg", "-v", "error", "-y", "-i", seg, "-vf", "scale=1080:1920:flags=lanczos,fps=60,setsar=1",
             "-c:v", "libx264", "-preset", "medium", "-crf", "14", "-pix_fmt", "yuv420p",
             "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2", dst])
        pieces.append(dst)
        for t, label in marks.get(stem, []):
            timeline.append((offset + float(t), label))
        timeline.append((offset, f"[corte] {stem}"))
        offset += duration(dst)

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

    # The still: the middle of the match (segment 03, the ceiling change, has towers and enemies in play).
    still_at = next((t for t, label in timeline if label.startswith("5 enxame")), offset / 2)
    run(["ffmpeg", "-v", "error", "-y", "-ss", f"{max(0.0, still_at - 1.0):.2f}", "-i", master, "-frames:v", "1",
         os.path.join(OUT, "bisect-td-gameplay-still.png")])

    timeline.sort()
    with open(os.path.join(OUT, "marcacoes.txt"), "w", encoding="utf-8") as f:
        f.write(f"Bisect TD - gameplay ({mmss(offset)} no total)\n\n")
        for t, label in timeline:
            f.write(f"{mmss(t)}  {label}\n")

    print(f"total {offset:.2f}s, slides {os.path.getsize(slides) / 1048576:.1f} MB")


if __name__ == "__main__":
    main()
