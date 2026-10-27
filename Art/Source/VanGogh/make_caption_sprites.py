"""Make the caption sprites for Vincent's storybook captions (Pillow, numpy).

    python make_caption_sprites.py

Writes the PNGs to Assets/VanGogh/Art/UI/, then run Tools > InnerFrame > Van Gogh > Rebuild assets (it sets the
9-slice borders):
  VG_Caption_Frame.png      wooden planks, painted like the room's furniture, with a bolt on each corner (9-slice,
                            drawn Tiled with an empty centre: the corners stay, the edges repeat)
  VG_Caption_Parchment.png  the panel behind his words, darker toward its edges (9-slice, Sliced)
  VG_Caption_Shadow.png     a soft shadow behind the panel (9-slice, Sliced)
  VG_Caption_Pill.png       the dark-brown rounded hint pill (9-slice)
  VG_Caption_Button.png     the cream circle behind the button letter
The design follows Cici's concept (variant 1, 2026-10-27) and the room's paint: strokes the size of the furniture's
(5-15 cm long, 0.4-1 cm wide, 4-5 tones about 5 L* apart, slightly bent, nearly parallel) and ink lines in the
room's outline colour (v1.3 report, raw/v13-style.md). Same output every run.
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parents[3] / "Assets/VanGogh/Art/UI"
SS = 2            # drawn at 2x, then downsampled: smooth edges
SEED = 1888

# Sampled from the concept (raw/p2-concept-colours.txt in the v1.2 report); the ink is the room's outline colour
WOOD_TONES = [(0x4E, 0x32, 0x1A), (0x6E, 0x4C, 0x27), (0x8C, 0x5F, 0x2C), (0xA8, 0x73, 0x3A), (0xC4, 0x8A, 0x3F)]
WOOD_BASE = (0x7C, 0x54, 0x27)
PARCHMENT, PILL, BUTTON = (0xF0, 0xCC, 0x9E), (0x55, 0x36, 0x1C), (0xE0, 0xC3, 0x9D)
INK = (0x21, 0x29, 0x3B)           # Ink_Outline in the Painted Bedroom
IRON, IRON_RIM, IRON_LIGHT = (0x3A, 0x33, 0x30), (0x1C, 0x19, 0x18), (0xC9, 0xC0, 0xB4)

# The frame sprite: BORDER px on every side are the corners (a plank crossing with its bolt); the strip between them
# is one tile along the edge, which repeats, so it is painted periodic. In the world the border is 6.5 cm
# (VanGoghCaption.FrameBorder), so one px is 0.051 cm and a tile is 13 cm.
SIZE, BORDER = 512, 128
PX_PER_CM = BORDER / 6.5
OUTER_INK, INNER_INK = 0.5, 0.3    # the ink lines, in cm: the room's shells are 0.3-0.9 cm
STROKE_LEN, STROKE_WID = (5.0, 13.0), (0.35, 0.8)  # cm, as on the furniture
STROKES_PER_PLANK = 90
BOLT_R = 1.4                       # cm

PARCHMENT_SIZE, PARCHMENT_BORDER = 256, 96       # the border holds the darkening toward the edge
PARCHMENT_EDGE = 0.86                            # the edge is this much of the middle's brightness (14 % darker)
SHADOW_SIZE, SHADOW_BORDER, SHADOW_BLUR, SHADOW_ALPHA = 256, 96, 22, 0.35

PILL_W, PILL_H = 256, 128          # pill: radius PILL_H / 2, border PILL_H / 2 on every side
BUTTON_D = 128

rng = np.random.default_rng(SEED)


def cm(x):
    return int(round(x * PX_PER_CM * SS))


def periodic_noise(n, period, octaves, rng):
    """Smooth noise in [-1, 1] over n samples that repeats every `period` samples."""
    x = np.arange(n) * 2 * np.pi / period
    out = np.zeros(n)
    for k in range(1, octaves + 1):
        out += (rng.uniform(0.5, 1) / k) * np.sin(k * x + rng.uniform(0, 2 * np.pi))
    return out / np.abs(out).max()


def plank(length, width, rng, horizontal):
    """One plank as an RGBA strip `length` x `width` px (at SS), periodic along its length every `length` px: strokes
    along the grain in 4-5 wood tones, darker toward the outer edge (the top of the strip), with ink lines along both
    edges. Turned for a vertical plank (its outer edge on the left)."""
    w, h = length, width
    img = Image.new("RGBA", (w, h), WOOD_BASE + (255,))
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))   # strokes are drawn here, then laid over the base once
    draw = ImageDraw.Draw(layer)
    for _ in range(STROKES_PER_PLANK):
        x0, y = rng.uniform(0, w), rng.uniform(0.08, 0.92) * h
        L, W = cm(rng.uniform(*STROKE_LEN)), cm(rng.uniform(*STROKE_WID))
        tone = WOOD_TONES[min(4, max(0, int(rng.normal(2.2, 1.1) + 0.5)))] if rng.random() < 0.9 else WOOD_TONES[rng.integers(0, 5)]
        a = int(rng.uniform(190, 245))
        bend = rng.uniform(-0.12, 0.12) * L      # a slight curve, as the room's strokes have
        slope = rng.uniform(-0.05, 0.05)         # nearly parallel to the grain
        for wrap in (-w, 0, w):                  # repeated across the edge, so the strip tiles
            xs = np.linspace(x0 + wrap - L / 2, x0 + wrap + L / 2, 12)
            t = (xs - xs[0]) / L
            ys = y + slope * (xs - x0 - wrap) + bend * 4 * t * (1 - t)
            pts = list(zip(xs, ys))
            draw.line(pts, fill=tone + (a,), width=max(2, W), joint="curve")
            for px, py in (pts[0], pts[-1]):
                draw.ellipse([px - W / 2, py - W / 2, px + W / 2, py + W / 2], fill=tone + (a,))
    img = Image.alpha_composite(img, layer)
    arr = np.asarray(img).astype(float)
    # darker toward the outer edge, a touch darker at the inner edge, a lit band just inside the outer ink
    v = np.arange(h) / h
    shade = 0.70 + 0.30 * np.clip(v / 0.45, 0, 1) - 0.08 * np.clip((v - 0.8) / 0.2, 0, 1)
    shade += 0.10 * np.exp(-((v - 0.16) / 0.06) ** 2)
    arr[..., :3] *= shade[:, None, None]
    # the ink lines, a little ragged along the plank
    rag = periodic_noise(w, w, 4, rng)
    outer, inner = cm(OUTER_INK), cm(INNER_INK)
    yy = np.arange(h)[:, None]
    ink_outer = np.clip((outer + 2.5 * rag[None, :] - yy) / 2.0, 0, 1)
    ink_inner = np.clip((yy - (h - inner - 1.5 * rag[None, :])) / 2.0, 0, 1) * 0.8
    ink = np.maximum(ink_outer, ink_inner)[..., None]
    arr[..., :3] = arr[..., :3] * (1 - ink) + np.array(INK, float) * ink
    out = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    return out if horizontal else out.transpose(Image.Transpose.ROTATE_90)


def bolt(draw, cx, cy):
    r = cm(BOLT_R)
    draw.ellipse([cx - r + 2 * SS, cy - r + 3 * SS, cx + r + 2 * SS, cy + r + 4 * SS], fill=(20, 14, 8, 110))   # shadow
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=IRON + (255,), outline=IRON_RIM + (255,), width=2 * SS)
    draw.ellipse([cx - r * 0.55, cy - r * 0.55, cx + r * 0.3, cy + r * 0.3], fill=(0x5A, 0x52, 0x4E, 255))
    draw.ellipse([cx - r * 0.5, cy - r * 0.5, cx - r * 0.1, cy - r * 0.1], fill=IRON_LIGHT + (255,))


def frame():
    n, b = SIZE * SS, BORDER * SS
    tile = n - 2 * b                  # the edge strip's length: the period
    end = cm(0.3)                     # the horizontal planks stick out past the sides by this
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    # side planks first (under); their strip is periodic over `tile`, laid down the height
    side = plank(tile, b, rng, horizontal=False)   # outer edge on the left
    for x in (0, n - b):
        strip = side if x == 0 else side.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        for y in range(-b, n, tile):
            img.alpha_composite(strip, (x, y))
    # a soft shadow where the top and bottom planks lie over the side planks
    shade = Image.new("L", (n, n), 0)
    ImageDraw.Draw(shade).rectangle([0, 0, n, b + cm(0.6)], fill=90)
    ImageDraw.Draw(shade).rectangle([0, n - b - cm(0.6), n, n], fill=90)
    shade = shade.filter(ImageFilter.GaussianBlur(cm(0.4)))
    arr = np.asarray(img).astype(float)
    arr[..., :3] *= (1 - np.asarray(shade, float) / 255)[..., None]
    img = Image.fromarray(arr.astype(np.uint8))
    # top and bottom planks (over), their strips periodic along the width
    top = plank(tile, b, rng, horizontal=True)
    for y, strip in ((0, top), (n - b, top.transpose(Image.Transpose.FLIP_TOP_BOTTOM))):
        row = Image.new("RGBA", (n, b), (0, 0, 0, 0))
        for x in range(-b, n, tile):
            row.alpha_composite(strip, (x, 0))
        # plank ends: cut just past the side planks, with the ink around them
        mask = Image.new("L", (n, b), 0)
        ImageDraw.Draw(mask).rounded_rectangle([end, 0, n - 1 - end, b - 1], radius=cm(0.5), fill=255)
        row.putalpha(mask)
        ends = Image.new("RGBA", (n, b), (0, 0, 0, 0))
        ImageDraw.Draw(ends).rounded_rectangle([end, 0, n - 1 - end, b - 1], radius=cm(0.5), outline=INK + (230,), width=cm(OUTER_INK))
        row.alpha_composite(ends)
        img.alpha_composite(row, (0, y))
    draw = ImageDraw.Draw(img, "RGBA")
    for cx in (b // 2, n - b // 2):
        for cy in (b // 2, n - b // 2):
            bolt(draw, cx, cy)
    img = img.filter(ImageFilter.GaussianBlur(SS * 0.3))
    return img.resize((SIZE, SIZE), Image.LANCZOS)


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


def shadow():
    n, b, blur = SHADOW_SIZE * SS, SHADOW_BORDER * SS, SHADOW_BLUR * SS
    m = Image.new("L", (n, n), 0)
    ImageDraw.Draw(m).rounded_rectangle([b - blur, b - blur, n - b + blur, n - b + blur], radius=blur, fill=int(255 * SHADOW_ALPHA))
    m = m.filter(ImageFilter.GaussianBlur(blur))
    img = Image.new("RGBA", (n, n), (12, 8, 4, 0))
    img.putalpha(m)
    return img.resize((SHADOW_SIZE, SHADOW_SIZE), Image.LANCZOS)


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
    for name, make in (("VG_Caption_Frame", frame), ("VG_Caption_Parchment", parchment), ("VG_Caption_Shadow", shadow),
                       ("VG_Caption_Pill", pill), ("VG_Caption_Button", button)):
        make().save(OUT / f"{name}.png", optimize=True)
        print(OUT / f"{name}.png")
