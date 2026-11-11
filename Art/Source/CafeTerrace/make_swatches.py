"""Cafe Terrace: the tileable dry-brush swatches, drawn by script (stand-ins until painted ones are approved).

    python3 Art/Source/CafeTerrace/make_swatches.py            (needs numpy and Pillow)

Each is a 512 x 512 grey image that tiles on all four sides, mid-grey on average. The scene's colour is in the
meshes' vertex colours; the material multiplies it by the swatch x 2, so the swatch only adds the thin streaks.
Same seed, same bytes on every run. Written to Assets/CafeTerrace/Textures/.
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "CafeTerrace", "Textures"))
N = 512


def blur(a, sx, sy):
    """Gaussian blur done in frequency space, so it wraps round the edges (tileable)."""
    fx = np.fft.fftfreq(N)[None, :]
    fy = np.fft.fftfreq(N)[:, None]
    k = np.exp(-2 * (np.pi ** 2) * ((sx * fx) ** 2 + (sy * fy) ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * k))


def norm(a):
    a = a - a.mean()
    return a / (a.std() + 1e-9)


def streaks(rng, along, across, flecks):
    """Dry-brush streaks running down the image (rotate for across)."""
    a = norm(blur(rng.standard_normal((N, N)), across, along))
    b = norm(blur(rng.standard_normal((N, N)), across * 0.4, along * 0.35))
    c = norm(blur(rng.standard_normal((N, N)), 14, 30))                    # broad patches of thinner paint
    f = blur((rng.random((N, N)) > 1 - flecks).astype(float), 0.7, 5.0)     # dry bristle marks
    return 0.55 * a + 0.35 * b + 0.25 * c + 2.2 * norm(f) * 0.25


def save(name, a, contrast):
    a = np.clip(0.5 + contrast * norm(a) / 3.0, 0.0, 1.0)
    img = Image.fromarray((a * 255 + 0.5).astype(np.uint8), "L").convert("RGB")
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    img.save(path, optimize=False)
    print("SWATCH|%s|mean %.3f" % (path, a.mean()))


def main():
    rng = np.random.default_rng(1888)
    save("CT_StrokeWall.png", streaks(rng, 46, 1.6, 0.012), 0.30)           # plaster: long vertical drags
    save("CT_StrokeWood.png", streaks(rng, 90, 1.0, 0.02).T, 0.50)          # boards: along the grain (u)
    dabs = norm(blur(rng.standard_normal((N, N)), 5, 3)) + 0.6 * norm(blur(rng.standard_normal((N, N)), 1.2, 4))
    save("CT_StrokeStone.png", dabs, 0.32)                                  # stone: short dabs
    save("CT_StrokeCloth.png", streaks(rng, 60, 0.8, 0.004), 0.30)          # canvas: fine, even


main()
