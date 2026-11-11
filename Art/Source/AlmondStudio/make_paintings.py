"""Makes the two painting images of the Almond Blossom studio (Assets/AlmondStudio/Textures).

  AS_Almond_Full.jpg  Van Gogh's Almond Blossom (1890, Van Gogh Museum), resized to 2048 wide.
  AS_Almond_Bare.jpg  The same painting with its blossoms removed. Only the blossom areas come from a
                      ChatGPT edit of the painting ("remove every blossom and bud, change nothing else");
                      every other pixel is the original's, so the branches never move when the blossoms
                      appear (AS_Reveal.shader blends the two images along each branch).

Inputs (kept outside the repo): the museum's hi-res image and ChatGPT's bare version.
  python3 make_paintings.py original.jpg chatgpt_bare.png OUT_DIR
Pillow and numpy. 2026-11-10, Claude for Serena.
"""
import sys
import numpy as np
from PIL import Image, ImageFilter

def main(original, bare, out_dir, width=2048):
    A = Image.open(original).convert("RGB")
    W, H = A.size
    B = Image.open(bare).convert("RGB").resize((W, H), Image.LANCZOS)
    a = np.asarray(A).astype(float) / 255
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    # blossoms: warm whites and pinks; the pale branches are greener (g > r) and the sky bluer, so both stay original
    blossom = (r > 0.70) & (r > g - 0.07) & (r > b - 0.02)
    m = Image.fromarray((blossom * 255).astype(np.uint8))
    m = m.filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.MaxFilter(21)).filter(ImageFilter.GaussianBlur(8))
    mm = np.clip(np.asarray(m).astype(float) / 255 * 1.5, 0, 1)[..., None]
    bare_out = (np.asarray(A) * (1 - mm) + np.asarray(B) * mm).astype(np.uint8)
    h = round(width * H / W)
    A.resize((width, h), Image.LANCZOS).save(f"{out_dir}/AS_Almond_Full.jpg", quality=92)
    Image.fromarray(bare_out).resize((width, h), Image.LANCZOS).save(f"{out_dir}/AS_Almond_Bare.jpg", quality=92)

if __name__ == "__main__":
    main(*sys.argv[1:4])
