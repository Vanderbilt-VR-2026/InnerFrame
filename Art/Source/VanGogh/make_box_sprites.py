"""Sprites for Vincent's dialogue box and hint (Cici's concepts V1-V3, 2026-11-04), into Assets/VanGogh/Art/UI/.

Run from the project folder:  python3 Art/Source/VanGogh/make_box_sprites.py   (needs Pillow)
Then Tools > InnerFrame > Van Gogh > Rebuild assets, which imports them with the 9-slice borders below.

- VG_Box.png        256 x 256, 9-slice 64 px: night blue #0E1A3F at 88 %, a chrome-yellow #E9C14A line 8 px wide.
                    VG_DialogueBox draws the 64 px slice 32 mm wide, so the line is 4 mm in the world.
- VG_NameTab.png    256 x 128, 9-slice 64 px: the same as a pill, its fill opaque so the box's border, which it sits
                    on, doesn't show through it (drawn 32 mm per slice, so 4 mm again).
- VG_HintPlate.png  256 x 256, 9-slice 64 px: night blue, rounded, no border, its edge softened over about 8 px (4 mm at
                    VG_Hint's 32 mm slice). A clean plate, not a cloud. "About 70 %" is meant as the concept shows it,
                    composited in sRGB; the project blends in linear space, where 70 % over a light background reads
                    grey, so the alpha is the one that gives the same colour over the room's wall (plate_alpha).
- VG_Star.png       128 x 128: a four-point star in chrome yellow with a faint glow.
- VG_StarRing.png   128 x 128: a smaller star inside a thin chrome-yellow circle on night blue.
Everything is drawn 4x larger and scaled down (premultiplied), so the edges are smooth. Same output every run.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

OUT = "Assets/VanGogh/Art/UI"
SS = 4
NIGHT = (0x0E, 0x1A, 0x3F)
CHROME = (0xE9, 0xC1, 0x4A)
SLICE = 64          # the 9-slice border, px (VanGoghAssetSetup.BoxBorderPixels)
LINE = 8            # the chrome line inside it, px
WALL = (108, 145, 186)   # the Painted Bedroom's blue wall, the mean of a patch in renders/talking.png


def linear(c):
    c = c / 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def plate_alpha(alpha, over=WALL):
    """The alpha that, blended in linear space, matches NIGHT at `alpha` composited in sRGB over `over`."""
    shares = []
    for n, b in zip(NIGHT, over):
        want = linear(alpha * n + (1 - alpha) * b)
        shares.append((linear(b) - want) / (linear(b) - linear(n)))
    return round(255 * sum(shares) / len(shares))


def down(big, size):
    """Scale a 4x drawing down with premultiplied alpha, so edges don't darken."""
    return big.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


def panel(size, radius, alpha, line):
    w, h = size
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    d.rounded_rectangle((0, 0, w * SS - 1, h * SS - 1), radius * SS, fill=CHROME + (255,))
    d.rounded_rectangle((line * SS, line * SS, (w - line) * SS - 1, (h - line) * SS - 1), (radius - line) * SS, fill=NIGHT + (alpha,))
    return down(big, size)


def plate(size, radius, alpha, soft):
    w, h = size
    mask = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(mask).rounded_rectangle((soft * SS, soft * SS, (w - soft) * SS - 1, (h - soft) * SS - 1), (radius - soft) * SS, fill=alpha)
    mask = mask.filter(ImageFilter.GaussianBlur(soft * SS / 3))
    big = Image.new("RGBA", mask.size, NIGHT + (0,))
    big.putalpha(mask)
    return down(big, size)


def star_points(cx, cy, outer, inner):
    pts = []
    for i in range(8):
        r = outer if i % 2 == 0 else inner
        dx, dy = [(0, -1), (0.7071, -0.7071), (1, 0), (0.7071, 0.7071), (0, 1), (-0.7071, 0.7071), (-1, 0), (-0.7071, -0.7071)][i]
        pts.append((cx + dx * r, cy + dy * r))
    return pts


def star(size, outer, inner, glow):
    c = size * SS / 2
    halo = Image.new("L", (size * SS, size * SS), 0)
    ImageDraw.Draw(halo).polygon(star_points(c, c, outer * SS, inner * SS * 1.6), fill=int(255 * glow))
    halo = halo.filter(ImageFilter.GaussianBlur(size * SS / 14))
    big = Image.new("RGBA", halo.size, CHROME + (0,))
    big.putalpha(halo)
    ImageDraw.Draw(big).polygon(star_points(c, c, outer * SS, inner * SS), fill=CHROME + (255,))
    return down(big, (size, size))


def star_ring(size):
    big = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    c, r, ring = size * SS / 2, (size / 2 - 2) * SS, 5 * SS
    d.ellipse((c - r, c - r, c + r, c + r), fill=CHROME + (255,))
    d.ellipse((c - r + ring, c - r + ring, c + r - ring, c + r - ring), fill=NIGHT + (255,))
    d.polygon(star_points(c, c, size * 0.33 * SS, size * 0.09 * SS), fill=CHROME + (255,))
    return down(big, (size, size))


def main():
    os.makedirs(OUT, exist_ok=True)
    sprites = {
        "VG_Box.png": panel((256, 256), 44, 224, LINE),
        "VG_NameTab.png": panel((256, 128), 64, 255, LINE),
        "VG_HintPlate.png": plate((256, 256), 48, plate_alpha(0.70), 6),
        "VG_Star.png": star(128, 58, 14, 0.35),
        "VG_StarRing.png": star_ring(128),
    }
    for name, image in sprites.items():
        image.save(os.path.join(OUT, name), optimize=True)
        print(name, image.size)


if __name__ == "__main__":
    main()
