"""make_canvas_stages.py - the three stages Vincent paints on VG_Canvas while you hold the mirror.

From Assets/VanGogh/Art/VG_SelfPortrait_1887.jpg (AIC 1954.326, public domain), fitted to the canvas's aspect ratio
(padded with primed canvas, never stretched), 1024 px tall:
  VG_Canvas_1_Sketch.png    dark brown lines on primed canvas: edges of a blurred greyscale copy, thresholded
  VG_Canvas_2_Colour.png    flat colour blocks (strong blur, then a few colours), loosely laid in over the sketch
  VG_Canvas_3_Finished.png  the portrait itself
Output: Assets/VanGogh/Art/Canvas/. Needs Pillow. Same input, same PNGs (the ragged edge uses a fixed seed).
Run: python make_canvas_stages.py
"""
import random
from pathlib import Path
from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "Assets/VanGogh/Art/VG_SelfPortrait_1887.jpg"
OUT = ROOT / "Assets/VanGogh/Art/Canvas"

CANVAS_W, CANVAS_H = 0.50, 0.61          # VG_Canvas in build_easel.py (m)
HEIGHT = 1024
PRIMED = (0xED, 0xE6, 0xD3)              # M_Canvas in build_easel.py; MirrorScene shows the blank canvas in it too
CHARCOAL = (0x4A, 0x34, 0x26)            # dark brown
SKETCH_BLUR, SKETCH_SHARE = 5, 0.10      # blur away the brushwork, then keep the strongest 10% of edges as lines
COLOUR_BLUR, COLOURS, COLOUR_SATURATION = 8, 8, 1.3
COLOUR_INSET = 0.04                      # primed canvas shows round the ragged edge of the laid-in colour
SEED = 1887


def fitted(img):
    """The portrait scaled to fit the canvas's aspect ratio (no padding yet) and the canvas's pixel width."""
    width = round(HEIGHT * CANVAS_W / CANVAS_H)
    scale = min(width / img.width, HEIGHT / img.height)
    return img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS), width


def on_primed(img, width):
    ground = Image.new("RGB", (width, HEIGHT), PRIMED)
    ground.paste(img, ((width - img.width) // 2, (HEIGHT - img.height) // 2))
    return ground


def lines_mask(img):
    """Sobel edge strength of a blurred greyscale copy, thresholded so the strongest SKETCH_SHARE become lines."""
    grey = ImageOps.grayscale(img).filter(ImageFilter.GaussianBlur(SKETCH_BLUR))
    signed = lambda k: ImageChops.difference(grey.filter(ImageFilter.Kernel((3, 3), k, 1, 128)), Image.new("L", grey.size, 128))
    strength = ImageChops.add(signed([-1, 0, 1, -2, 0, 2, -1, 0, 1]), signed([-1, -2, -1, 0, 0, 0, 1, 2, 1]))
    histogram, total, above = strength.histogram(), strength.width * strength.height, 0
    threshold = 255
    while threshold > 0 and above + histogram[threshold] < SKETCH_SHARE * total:
        above += histogram[threshold]
        threshold -= 1
    mask = strength.point(lambda v: 255 if v >= threshold else 0)
    mask = mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))   # drop specks
    edge = 8                                                                        # the filters' border artefacts
    clean = Image.new("L", mask.size, 0)
    clean.paste(mask.crop((edge, edge, mask.width - edge, mask.height - edge)), (edge, edge))
    return clean


def sketch(img):
    out = Image.new("RGB", img.size, PRIMED)
    out.paste(Image.new("RGB", img.size, CHARCOAL), mask=lines_mask(img))
    return out


def ragged_inset(size):
    """A mask that covers the image except a ragged margin of COLOUR_INSET."""
    w, h = size
    inset = round(h * COLOUR_INSET)
    rect = Image.new("L", size, 0)
    rect.paste(255, (inset, inset, w - inset, h - inset))
    rng = random.Random(SEED)
    noise = Image.frombytes("L", size, bytes(rng.getrandbits(8) for _ in range(w * h)))
    noise = ImageOps.autocontrast(noise.filter(ImageFilter.GaussianBlur(6)))
    mixed = ImageChops.blend(rect.filter(ImageFilter.GaussianBlur(inset)), noise, 0.35)
    return mixed.point(lambda v: 255 if v > 128 else 0)


def colour(img):
    blurred = ImageEnhance.Color(img.filter(ImageFilter.GaussianBlur(COLOUR_BLUR))).enhance(COLOUR_SATURATION)
    blocks = blurred.quantize(COLOURS, Image.Quantize.MEDIANCUT).convert("RGB")
    out = sketch(img)
    out.paste(blocks, mask=ragged_inset(img.size))
    out.paste(Image.new("RGB", img.size, CHARCOAL), mask=lines_mask(img).point(lambda v: v * 110 // 255))   # sketch shows through
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    portrait, width = fitted(Image.open(SOURCE).convert("RGB"))
    for name, image in (("VG_Canvas_1_Sketch.png", sketch(portrait)),
                        ("VG_Canvas_2_Colour.png", colour(portrait)),
                        ("VG_Canvas_3_Finished.png", portrait)):
        image = on_primed(image, width)
        image.save(OUT / name, optimize=True)
        print(f"{name}: {image.size[0]} x {image.size[1]}")


if __name__ == "__main__":
    main()
