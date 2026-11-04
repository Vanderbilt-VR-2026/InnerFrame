"""Shared helpers for The Starry Night's image scripts: keying out a flat magenta background with a soft edge and a
despill (no pink fringe), and small numpy utilities. Imported by make_textures.py (and by the input
checks of the art run). Same output every run.
"""
import sys

sys.path.append("/Users/cicisss/Library/Python/3.9/lib/python/site-packages")   # Pillow, numpy for /usr/bin/python3 -I
import numpy as np
from PIL import Image, ImageFilter

KEY_INNER, KEY_OUTER = 60.0, 150.0     # RGB distance from the key: below = background, above = opaque, soft between


def key_colour(rgb):
    """The background colour: the median of the image's outer 4-pixel frame."""
    a = np.asarray(rgb, dtype=np.float64)
    frame = np.concatenate([a[:4].reshape(-1, 3), a[-4:].reshape(-1, 3), a[:, :4].reshape(-1, 3), a[:, -4:].reshape(-1, 3)])
    return np.median(frame, axis=0)


def magenta_cast(a):
    """How much a pixel leans toward magenta: R and B both above G (0 when it doesn't)."""
    return np.clip(np.minimum(a[..., 0], a[..., 2]) - a[..., 1], 0, None)


def key_out(img, inner=KEY_INNER, outer=KEY_OUTER):
    """RGBA from an image on a flat magenta background: a soft alpha from the distance to the key colour; the key
    colour un-mixed from semi-transparent edge pixels; then a despill that pulls the remaining magenta cast toward
    green (min(R, B) is brought down to G). Returns (rgba uint8 array, key colour)."""
    rgb = img.convert("RGB")
    k = key_colour(rgb)
    a = np.asarray(rgb, dtype=np.float64)
    d = np.linalg.norm(a - k, axis=2)
    t = np.clip((d - inner) / (outer - inner), 0, 1)
    alpha = t * t * (3 - 2 * t)
    # un-mix the key from edge pixels: c = (observed - (1 - alpha) * key) / alpha
    safe = np.maximum(alpha, 1e-3)[..., None]
    c = (a - (1 - alpha)[..., None] * k) / safe
    c = np.where(alpha[..., None] > 1e-3, c, a)
    c = np.clip(c, 0, 255)
    # despill: what is still magenta-tinted (R and B above G) is pulled to G
    spill = magenta_cast(c)
    c[..., 0] -= spill
    c[..., 2] -= spill
    c = np.clip(c, 0, 255)
    out = np.dstack([c, alpha * 255]).round().astype(np.uint8)
    return out, k


def bleed(rgba, passes=24):
    """Fills fully transparent pixels with the nearest opaque colour (so mipmaps and filtering don't pull in black)."""
    img = Image.fromarray(rgba)
    a = np.asarray(img.getchannel("A"))
    base = np.asarray(img.convert("RGB"), dtype=np.float64)
    known = a > 8
    col = base * known[..., None]
    w = known.astype(np.float64)
    for _ in range(passes):
        cs = np.asarray(Image.fromarray(col.clip(0, 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(2)), dtype=np.float64)
        ws = np.asarray(Image.fromarray((w * 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(2)), dtype=np.float64) / 255
        fill = (ws > 1e-3) & ~known
        col[fill] = cs[fill] / ws[fill][:, None] * 1.0
        w = np.where(known | fill, 1.0, w)
        known = known | fill
    rgb = np.where((a > 8)[..., None], base, col)
    return np.dstack([rgb.clip(0, 255), a]).astype(np.uint8)


def next_pow2(n):
    p = 1
    while p < n:
        p *= 2
    return p
