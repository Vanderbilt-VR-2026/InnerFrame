"""The Starry Night's seat blanket, drawn with Pillow and numpy (same output every run).

    /usr/bin/python3 -I make_blanket.py

Writes Assets/StarryNight/Textures/SN_Blanket.png (1024 x 512): the blanket that marks where to sit beside Vincent,
striped wool in the painting's colours, fringed at its short ends. Then run Tools > InnerFrame > Starry Night > Build in
Unity (it sets the import settings and lays the blanket on the seat, SN_Layout.blanketSize and blanketYaw).
"""
import sys
from pathlib import Path

sys.path.append("/Users/cicisss/Library/Python/3.9/lib/python/site-packages")   # Pillow, numpy for /usr/bin/python3 -I
import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[3]
OUT = REPO / "Assets/StarryNight/Textures"
SEED = 1889

OCHRE = (200, 145, 58)
COBALT = (47, 78, 154)
MADDER = (154, 59, 38)
CREAM = (236, 224, 190)
GRASS_DARK = (48, 64, 40)


def blanket(rng):
    """Striped wool, 1024 x 512 (the blanket is about 0.7 x 0.5 m), its stripes across, fringes at both short ends."""
    bw, bh = 1024, 512
    x = np.arange(bw)[None, :].repeat(bh, 0).astype(np.float32)
    col = np.zeros((bh, bw, 3), np.float32) + np.array(OCHRE, np.float32)
    bands = [(150, 230, COBALT), (400, 440, MADDER), (584, 624, MADDER), (794, 874, COBALT),
             (300, 312, CREAM), (712, 724, CREAM)]
    for a, b, c in bands:
        m = ((x >= a) & (x < b))[..., None]
        col = np.where(m, np.array(c, np.float32), col)
    # woven texture: fine threads and a soft painterly wobble
    yy = np.arange(bh)[:, None].astype(np.float32)
    weave = (np.sin(x * 1.9) * np.sin(yy * 2.3)) * 9
    wobble = rng.normal(0, 1, (bh // 16, bw // 16)).astype(np.float32)
    wobble = np.array(Image.fromarray(((wobble * 30 + 128).clip(0, 255)).astype(np.uint8)).resize((bw, bh), Image.BICUBIC), np.float32) - 128
    col += weave[..., None] + wobble[..., None] * 0.25
    # fringes: the outer 34 px of each short end in thin threads over the grass-dark gap
    for x0, x1 in ((0, 34), (bw - 34, bw)):
        seg = col[:, x0:x1]
        threads = (np.arange(bh) % 10 < 5)[:, None]
        seg[:] = np.where(threads[..., None], seg * 0.92, np.array(GRASS_DARK, np.float32))
    Image.fromarray(col.clip(0, 255).astype(np.uint8)).save(OUT / "SN_Blanket.png", optimize=False)


def main():
    blanket(np.random.default_rng(SEED))
    print("SN_Blanket.png 1024 x 512")


if __name__ == "__main__":
    main()
