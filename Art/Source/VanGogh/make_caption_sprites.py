"""Make the caption sprites for Vincent's storybook captions (Pillow, numpy).

    python make_caption_sprites.py

Writes three PNGs to Assets/VanGogh/Art/UI/, then run Tools > InnerFrame > Van Gogh > Rebuild assets (it sets
the 9-slice borders):
  VG_Caption_Frame.png   rustic wooden planks with a bolt in each corner, around a parchment panel (9-slice)
  VG_Caption_Pill.png    the dark-brown rounded hint pill (9-slice)
  VG_Caption_Button.png  the cream circle behind the button letter
The design follows Cici's concept (variant 1, 2026-10-27); the colours were sampled from it. Same output every run.
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parents[3] / "Assets/VanGogh/Art/UI"
SS = 4            # drawn at 4x, then downsampled: smooth edges
SEED = 1888

# Sampled from the concept (raw/p2-concept-colours.txt in the v1.2 report)
WOOD, WOOD_LIGHT, WOOD_DARK = (0x92, 0x64, 0x2E), (0xBD, 0x82, 0x38), (0x7A, 0x57, 0x2C)
PARCHMENT = (0xF0, 0xCC, 0x9E)
PILL, BUTTON, BOLT = (0x55, 0x36, 0x1C), (0xE0, 0xC3, 0x9D), (0x4F, 0x44, 0x40)

# Frame layout, in final pixels. The 9-slice border is BORDER on every side: everything that must not stretch
# (the plank crossings, their ends and the bolts) sits in the corners; along the edges the planks vary only across
# their width, so they stretch cleanly.
SIZE, BORDER = 384, 112
PLANK_IN, PLANK_OUT = 16, 90       # each plank runs 16-90 px in from the outside (74 px wide)
END = 3                            # plank ends stick out past the crossing plank, to 3 px from the edge
BOLT_R = 15
OUTLINE = (0x3A, 0x24, 0x10)       # the painted dark outline around the planks

PILL_W, PILL_H = 256, 128          # pill: radius PILL_H / 2, border PILL_H / 2 on every side
BUTTON_D = 128

rng = np.random.default_rng(SEED)


def smooth_noise(n, scale, rng):
    """1D noise in [-1, 1], features about `scale` samples long."""
    k = max(2, int(n / scale) + 2)
    return np.interp(np.arange(n), np.linspace(0, n - 1, k), rng.uniform(-1, 1, k))


def plank_profile(width, rng):
    """Brightness across a plank (outer edge first): grain streaks, darker rims, a lit bevel."""
    x = np.arange(width)
    grain = 0.13 * smooth_noise(width, width / 7, rng) + 0.06 * np.sin(2 * np.pi * x / width * rng.uniform(5, 8) + rng.uniform(0, 6))
    streaks = np.zeros(width)
    for _ in range(9):   # thin dark grain lines
        c, w = rng.uniform(0.08, 0.92) * width, rng.uniform(0.5, 1.4) * SS
        streaks -= rng.uniform(0.12, 0.3) * np.exp(-((x - c) / w) ** 2)
    rim = -0.35 * np.exp(-x / (4 * SS)) - 0.35 * np.exp(-(width - 1 - x) / (3 * SS))
    bevel = 0.18 * np.exp(-((x - 8 * SS) / (4 * SS)) ** 2)
    return 1 + grain + streaks + rim + bevel


def wood_colour(brightness):
    """Brightness around 1 -> a colour between the dark and the light wood."""
    b = np.clip(brightness, 0, 2)[..., None]
    lo, mid, hi = (np.array(c, float) for c in (WOOD_DARK, WOOD, WOOD_LIGHT))
    return np.where(b < 1, lo * 0.5 + (mid - lo * 0.5) * b, mid + (hi - mid) * (b - 1) * 2)


def mask(n, boxes, radius):
    m = Image.new("L", (n, n), 0)
    for box in boxes:
        ImageDraw.Draw(m).rounded_rectangle(box, radius=radius, fill=255)
    return np.asarray(m, float) / 255


def frame():
    n, p0, p1, e = SIZE * SS, PLANK_IN * SS, PLANK_OUT * SS, END * SS
    w = p1 - p0
    yy, xx = np.mgrid[0:n, 0:n] / n

    # parchment: soft, low-contrast mottling, and a shadow along the planks
    mottle = sum(a * np.sin(2 * np.pi * (fx * xx + fy * yy) + ph)
                 for a, fx, fy, ph in [(0.025, 1.3, 0.7, 0.4), (0.02, 0.6, 1.9, 2.1), (0.015, 2.7, 1.1, 4.0)])
    edge_dist = np.minimum.reduce([xx * n - p1, yy * n - p1, (1 - xx) * n - p1, (1 - yy) * n - p1]) / SS
    shadow = 0.25 * np.exp(-np.clip(edge_dist, 0, None) / 7.0)
    rgb = np.array(PARCHMENT, float) * (1 + mottle - shadow)[..., None]
    alpha = mask(n, [(p0 + w // 2, p0 + w // 2, n - p0 - w // 2, n - p0 - w // 2)], 0)

    # side planks (under), then top and bottom planks (over); each runs past the other to a rounded end
    side_prof, top_prof = plank_profile(w, rng), plank_profile(w, rng)
    sides = [(p0, e, p1 - 1, n - e - 1), (n - p1, e, n - p0 - 1, n - e - 1)]
    tops = [(e, p0, n - e - 1, p1 - 1), (e, n - p1, n - e - 1, n - p0 - 1)]
    side_tex = np.zeros((n, n)); side_tex[:, p0:p1] = side_prof; side_tex[:, n - p1:n - p0] = side_prof[::-1]
    top_tex = np.zeros((n, n)); top_tex[p0:p1, :] = top_prof[:, None]; top_tex[n - p1:n - p0, :] = top_prof[::-1, None]
    for boxes, tex, drop in ((sides, side_tex, False), (tops, top_tex, True)):
        m = mask(n, boxes, 7 * SS)
        ring = np.clip(mask(n, [(b[0] - 2 * SS, b[1] - 2 * SS, b[2] + 2 * SS, b[3] + 2 * SS) for b in boxes], 9 * SS) - m, 0, 1)
        if drop:   # the top and bottom planks cast a soft shadow on the side planks below them
            sh = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4 * SS)), float) / 255
            rgb *= (1 - 0.35 * np.clip(sh - m, 0, 1))[..., None]
        rgb = rgb * (1 - ring[..., None]) + np.array(OUTLINE, float) * ring[..., None]
        rgb = rgb * (1 - m[..., None]) + wood_colour(tex) * m[..., None]
        alpha = np.maximum(alpha, np.maximum(m, ring))

    im = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8))
    draw = ImageDraw.Draw(im)
    for cx in ((p0 + p1) // 2, n - (p0 + p1) // 2):
        for cy in ((p0 + p1) // 2, n - (p0 + p1) // 2):
            r = BOLT_R * SS
            draw.ellipse([cx - r, cy - r + 2 * SS, cx + r + 2 * SS, cy + r + 4 * SS], fill=(40, 25, 12, 120))   # shadow
            draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BOLT + (255,), outline=(30, 24, 20, 255), width=2 * SS)
            draw.ellipse([cx - r // 2, cy - r // 2, cx + r // 3, cy + r // 3], fill=(0x7E, 0x74, 0x6C, 255))
            draw.ellipse([cx - r // 2, cy - r // 2, cx - r // 6, cy - r // 6], fill=(0xC8, 0xC0, 0xB6, 255))
    im = im.filter(ImageFilter.GaussianBlur(SS * 0.35))
    return im.resize((SIZE, SIZE), Image.LANCZOS)


def pill():
    w, h = PILL_W * SS, PILL_H * SS
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    rim = tuple(int(c * 0.6) for c in PILL)
    draw.rounded_rectangle([0, 0, w - 1, h - 1], radius=h // 2, fill=rim + (255,))
    inset = 3 * SS
    draw.rounded_rectangle([inset, inset, w - 1 - inset, h - 1 - inset], radius=h // 2 - inset, fill=PILL + (255,))
    # a faint highlight along the top half, constant along the length so it stretches cleanly
    hl = Image.new("L", (w, h), 0)
    ImageDraw.Draw(hl).rounded_rectangle([h // 4, inset * 2, w - 1 - h // 4, h // 2], radius=h // 4, fill=26)
    glow = Image.new("RGBA", (w, h), (255, 232, 205, 0))
    glow.putalpha(hl.filter(ImageFilter.GaussianBlur(2 * SS)))
    im = Image.alpha_composite(im, glow)
    return im.resize((PILL_W, PILL_H), Image.LANCZOS)


def button():
    d = BUTTON_D * SS
    im = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    draw.ellipse([0, 0, d - 1, d - 1], fill=tuple(int(c * 0.8) for c in BUTTON) + (255,))
    draw.ellipse([3 * SS, 3 * SS, d - 1 - 3 * SS, d - 1 - 3 * SS], fill=BUTTON + (255,))
    return im.resize((BUTTON_D, BUTTON_D), Image.LANCZOS)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for name, make in (("VG_Caption_Frame", frame), ("VG_Caption_Pill", pill), ("VG_Caption_Button", button)):
        make().save(OUT / f"{name}.png", optimize=True)
        print(OUT / f"{name}.png")
