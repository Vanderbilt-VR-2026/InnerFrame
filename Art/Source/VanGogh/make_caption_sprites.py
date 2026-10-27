"""Make the caption sprites for Vincent's storybook captions (Pillow, numpy).

    python make_caption_sprites.py

Writes the PNGs to Assets/VanGogh/Art/UI/, then run Tools > InnerFrame > Van Gogh > Rebuild assets (it sets the
9-slice borders):
  VG_Caption_Parchment.png  the parchment his words sit on, over the sign's panel, darker toward its edges (9-slice)
  VG_Caption_Pill.png       the dark-brown rounded hint pill (9-slice)
  VG_Caption_Button.png     the cream circle behind the button letter
The frame itself is the Tripo sign baked flat by Caption/bake_sign.py (VG_Sign_A/B.png); the drawn planks and the
shadow of v1.3 are gone. The parchment follows Cici's concept (variant 1, 2026-10-27). Same output every run.
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parents[3] / "Assets/VanGogh/Art/UI"
SS = 2            # drawn at 2x, then downsampled: smooth edges
SEED = 1888

# Sampled from the concept (raw/p2-concept-colours.txt in the v1.2 report); the ink is the room's outline colour
PARCHMENT, PILL, BUTTON = (0xF0, 0xCC, 0x9E), (0x55, 0x36, 0x1C), (0xE0, 0xC3, 0x9D)

# The frame sprite: BORDER px on every side are the corners (a plank crossing with its bolt); the strip between them
# is one tile along the edge, which repeats, so it is painted periodic. In the world the border is 6.5 cm
# (VanGoghCaption.FrameBorder), so one px is 0.051 cm and a tile is 13 cm.
PX_PER_CM = 128 / 6.5             # the v1.3 frame's scale: a 6.5 cm plank was 128 px; the parchment keeps it

PARCHMENT_SIZE, PARCHMENT_BORDER = 256, 96       # the border holds the darkening toward the edge
PARCHMENT_EDGE = 0.86                            # the edge is this much of the middle's brightness (14 % darker)

PILL_W, PILL_H = 256, 128          # pill: radius PILL_H / 2, border PILL_H / 2 on every side
BUTTON_D = 128

rng = np.random.default_rng(SEED)


def cm(x):
    return int(round(x * PX_PER_CM * SS))






def parchment():
    n, b = PARCHMENT_SIZE * SS, PARCHMENT_BORDER * SS
    yy, xx = np.mgrid[0:n, 0:n]
    # darker toward the edge: the distance in from the nearest edge, over the border band
    d = np.minimum.reduce([xx, yy, n - 1 - xx, n - 1 - yy]) / b
    bright = PARCHMENT_EDGE + (1 - PARCHMENT_EDGE) * np.clip(d, 0, 1) ** 0.8
    # faint horizontal strokes, a few percent of lightness, that tile in the centre
    strokes = Image.new("L", (n, n), 128)
    draw = ImageDraw.Draw(strokes)
    for _ in range(200):
        x, y = rng.uniform(-n, n), rng.uniform(0, n)
        L, W = cm(rng.uniform(4, 12)), cm(rng.uniform(0.3, 0.8))
        v = int(128 + rng.choice([-1, 1]) * rng.uniform(5, 11))
        for wrap in (0, n):
            draw.line([(x + wrap, y), (x + wrap + L, y + rng.uniform(-2, 2) * SS)], fill=v, width=W)
    strokes = np.asarray(strokes.filter(ImageFilter.GaussianBlur(SS)), float) / 128 - 1   # about -0.09..0.09
    bright *= 1 + 0.45 * strokes
    rgb = np.array(PARCHMENT, float) * bright[..., None]
    img = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), np.full((n, n), 255.0)]).astype(np.uint8))
    return img.resize((PARCHMENT_SIZE, PARCHMENT_SIZE), Image.LANCZOS)



def pill():
    s = 4   # the pill and button are drawn at 4x, as before
    w, h = PILL_W * s, PILL_H * s
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    rim = tuple(int(c * 0.6) for c in PILL)
    draw.rounded_rectangle([0, 0, w - 1, h - 1], radius=h // 2, fill=rim + (255,))
    inset = 3 * s
    draw.rounded_rectangle([inset, inset, w - 1 - inset, h - 1 - inset], radius=h // 2 - inset, fill=PILL + (255,))
    # a faint highlight along the top half, constant along the length so it stretches cleanly
    hl = Image.new("L", (w, h), 0)
    ImageDraw.Draw(hl).rounded_rectangle([h // 4, inset * 2, w - 1 - h // 4, h // 2], radius=h // 4, fill=26)
    glow = Image.new("RGBA", (w, h), (255, 232, 205, 0))
    glow.putalpha(hl.filter(ImageFilter.GaussianBlur(2 * s)))
    im = Image.alpha_composite(im, glow)
    return im.resize((PILL_W, PILL_H), Image.LANCZOS)


def button():
    s = 4
    d = BUTTON_D * s
    im = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    draw.ellipse([0, 0, d - 1, d - 1], fill=tuple(int(c * 0.8) for c in BUTTON) + (255,))
    draw.ellipse([3 * s, 3 * s, d - 1 - 3 * s, d - 1 - 3 * s], fill=BUTTON + (255,))
    return im.resize((BUTTON_D, BUTTON_D), Image.LANCZOS)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for name, make in (("VG_Caption_Parchment", parchment), ("VG_Caption_Pill", pill), ("VG_Caption_Button", button)):
        make().save(OUT / f"{name}.png", optimize=True)
        print(OUT / f"{name}.png")
