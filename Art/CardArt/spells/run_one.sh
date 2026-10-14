#!/usr/bin/env bash
# Generates one spell card emblem with Codex and copies it to raw/<name>.png
name="$1"; subject="$2"
CODEX="/c/Program Files/WindowsApps/OpenAI.Codex_26.1002.7124.0_x64__2p2nqsd0c76g0/app/resources/codex.exe"
OUT="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$OUT/raw" "$OUT/logs" "$OUT/work/$name"
prompt="Use your image generation tool to create exactly one image, then stop. Do not write or run any code and do not edit files. The image: a card icon for a minimalist flat 2D mobile tower-defense game whose art is made of simple bold geometric shapes. One single emblem: pure white, completely solid filled shapes on a solid pure black background (#000000). No greys, no gradients, no shading, no glow, no outlines around the shapes, no text, no border, no frame, no ground line. Chunky, thick, rounded forms with rounded corners and round ends, like a heavy rounded UI glyph; no thin lines at all: every part is at least a tenth of the image wide; as few details as possible so the emblem still reads at 48 pixels. Centred, filling about 80 percent of a square canvas with an even black margin all around. Subject: $subject"
"$CODEX" exec --skip-git-repo-check -s read-only -C "$OUT/work/$name" "$prompt" > "$OUT/logs/$name.log" 2>&1
sid=$(grep -oiE 'session id: [0-9a-f-]+' "$OUT/logs/$name.log" | head -1 | awk '{print $3}')
img=$(ls -t ~/.codex/generated_images/"$sid"/*.png 2>/dev/null | head -1)
if [ -n "$img" ]; then cp "$img" "$OUT/raw/$name.png"; echo "OK $name $img"; else echo "FAIL $name (sid=$sid)"; fi
