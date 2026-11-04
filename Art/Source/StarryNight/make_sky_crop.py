"""The placeholder painted sky: the sky of Van Gogh's The Starry Night (1889, MoMA 472.1941), cropped and resized.

    /usr/bin/python3 -I make_sky_crop.py <the downloaded painting.jpg>

Source: Wikimedia Commons, File:Van_Gogh_-_Starry_Night_-_Google_Art_Project.jpg (Google Art Project photo; public
domain, PD-Art). Commons serves only standard thumbnail widths now, so the 3840 px thumbnail is used and resized here.
Keeps the top SKY_FRACTION of the painting (the sky, above the hills) and writes a 2048 x 1024 PNG (a power of two, so
Unity can compress it) to Assets/StarryNight/Textures/SN_PaintSky.png. The sky shader maps it onto the front panel of
the dome (yaw -80..+80 deg, pitch -5..+70 deg). A placeholder: the art pass replaces it by name.
"""
import sys
from pathlib import Path

sys.path.append("/Users/cicisss/Library/Python/3.9/lib/python/site-packages")   # Pillow for /usr/bin/python3 -I
from PIL import Image

SKY_FRACTION = 0.60
OUT_SIZE = (2048, 1024)
OUT = Path(__file__).resolve().parents[3] / "Assets/StarryNight/Textures/SN_PaintSky.png"

src = Image.open(sys.argv[1]).convert("RGB")
w, h = src.size
sky = src.crop((0, 0, w, round(h * SKY_FRACTION)))
sky = sky.resize(OUT_SIZE, Image.LANCZOS)
OUT.parent.mkdir(parents=True, exist_ok=True)
sky.save(OUT, optimize=True)
print(f"{sys.argv[1]} {w}x{h} -> top {SKY_FRACTION:.0%} {w}x{round(h * SKY_FRACTION)} -> {OUT} {OUT_SIZE[0]}x{OUT_SIZE[1]}")
