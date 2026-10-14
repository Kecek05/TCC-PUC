"""White-on-black Codex renders -> white sprites whose alpha is the brightness, cropped and sized for Unity."""
import os
from PIL import Image, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "raw")
OUT = os.path.join(HERE, "final")
os.makedirs(OUT, exist_ok=True)

MAX_SIDE = 512
# Radial art keeps its centre on the image centre (the rune, the nova and the zones spin or scale about it).
CENTRED = {"fx_frost_nova", "fx_haste_rune", "fx_scorch", "fx_corrosion", "fx_smoke", "fx_ice_block", "fx_chain_ring"}


def to_alpha(img):
    rgb = img.convert("RGB")
    r, g, b = rgb.split()
    lum = ImageChops.lighter(ImageChops.lighter(r, g), b)
    # Lift the near-black noise floor to zero so the background is truly transparent.
    alpha = lum.point(lambda v: 0 if v < 12 else min(255, int((v - 12) * 255 / 243)))
    white = Image.new("L", rgb.size, 255)
    return Image.merge("RGBA", (white, white, white, alpha))


def crop(img, name, pad=0.06):
    alpha = img.getchannel("A")
    box = alpha.point(lambda v: 255 if v > 6 else 0).getbbox()
    if box is None:
        return img
    w, h = img.size
    if name in CENTRED:
        cx, cy = w / 2, h / 2
        half = max(cx - box[0], box[2] - cx, cy - box[1], box[3] - cy)
        half *= 1 + pad
        box = (int(cx - half), int(cy - half), int(cx + half), int(cy + half))
    else:
        bw, bh = box[2] - box[0], box[3] - box[1]
        p = int(max(bw, bh) * pad)
        box = (box[0] - p, box[1] - p, box[2] + p, box[3] + p)
    out = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), (255, 255, 255, 0))
    out.paste(img.crop((max(0, box[0]), max(0, box[1]), min(w, box[2]), min(h, box[3]))),
              (max(0, -box[0]), max(0, -box[1])))
    return out


def fit(img):
    w, h = img.size
    s = MAX_SIDE / max(w, h)
    if s < 1:
        img = img.resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS)
    return img


for file in sorted(os.listdir(RAW)):
    if not file.endswith(".png"):
        continue
    name = file[:-4]
    img = to_alpha(Image.open(os.path.join(RAW, file)))
    if name == "fx_flakes":
        # Four flakes in a 2x2 grid: one sprite each.
        w, h = img.size
        for i, (x, y) in enumerate([(0, 0), (1, 0), (0, 1), (1, 1)]):
            part = img.crop((x * w // 2, y * h // 2, (x + 1) * w // 2, (y + 1) * h // 2))
            part = fit(crop(part, f"fx_flake_{i}", pad=0.1))
            part.save(os.path.join(OUT, f"fx_flake_{i}.png"))
            print(f"fx_flake_{i}", part.size)
        continue
    img = fit(crop(img, name))
    img.save(os.path.join(OUT, f"{name}.png"))
    print(name, img.size)
