"""The Starry Night's image for its place in the Gallery: Assets/StarryNight/Gallery/SN_GalleryPainting.jpg, 2048 px wide
at the painting's own ratio (2048 x 1622), from Wikimedia Commons' full-size Google Art Project reproduction
(https://commons.wikimedia.org/wiki/File:Van_Gogh_-_Starry_Night_-_Google_Art_Project.jpg, public domain, PD-Art;
44,567 x 35,291 px, 696,195,208 bytes, sha1 b3e5f829...). The source stays outside the repo, in
/Users/cicisss/CS-VR/refs/3-starry-night/gallery/; this script checks it is that file before using it.

The source has 1.57 billion pixels, so it is never decoded whole: JPEG "draft" mode decodes it at 1/8 scale
(5571 x 4412, about 74 MB in memory), then Lanczos takes it to 2048 wide. Same input, same Pillow -> same bytes.

    /usr/bin/python3 -I make_gallery_painting.py [SOURCE.jpg]
"""
import hashlib
import math
import sys
from pathlib import Path

sys.path.append("/Users/cicisss/Library/Python/3.9/lib/python/site-packages")
from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
SOURCE = Path("/Users/cicisss/CS-VR/refs/3-starry-night/gallery/van-gogh-starry-night-google-art-project.jpg")
OUT = REPO / "Assets/StarryNight/Gallery/SN_GalleryPainting.jpg"

SOURCE_SIZE = (44567, 35291)
SOURCE_BYTES = 696195208
SOURCE_SHA1 = "b3e5f8291b4137ff50ba876c84a1f7248186104a"  # Commons' own sha1 for the file
WIDTH = 2048
QUALITY = 90


def sha1_of(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 22), b""):
            h.update(block)
    return h.hexdigest()


src = Path(sys.argv[1]) if len(sys.argv) > 1 else SOURCE
if src.stat().st_size != SOURCE_BYTES:
    sys.exit(f"{src}: {src.stat().st_size:,} bytes, expected {SOURCE_BYTES:,} (Commons' full-size file)")
if sha1_of(src) != SOURCE_SHA1:
    sys.exit(f"{src}: sha1 differs from Commons' {SOURCE_SHA1}")

# Checked above that this is the expected file, so lift Pillow's decompression-bomb limit for it alone.
Image.MAX_IMAGE_PIXELS = None
im = Image.open(src)
if im.format != "JPEG" or im.size != SOURCE_SIZE:
    sys.exit(f"{src}: {im.format} {im.size}, expected JPEG {SOURCE_SIZE}")

w, h = im.size
eighth = (math.ceil(w / 8), math.ceil(h / 8))  # what a 1/8-scale decode gives
# Pillow picks the largest scale whose result is still at least the size asked for, by whole-number division,
# so ask for the rounded-down size to get 1/8 (asking for 5571 gives 1/4, since 44567 // 5571 = 7).
im.draft("RGB", (w // 8, h // 8))  # decode at 1/8 scale: the JPEG decoder skips the rest
if im.size != eighth:
    sys.exit(f"draft mode gave {im.size}, expected {eighth}")
im = im.convert("RGB")
print(f"decoded at 1/8: {im.size[0]} x {im.size[1]}")

height = round(WIDTH * h / w)
out = im.resize((WIDTH, height), Image.LANCZOS)
OUT.parent.mkdir(parents=True, exist_ok=True)
out.save(OUT, "JPEG", quality=QUALITY, subsampling=0, optimize=False, progressive=False)
data = OUT.read_bytes()
print(f"wrote {OUT.relative_to(REPO)}: {WIDTH} x {height} (ratio {WIDTH / height:.4f}, source {w / h:.4f}), "
      f"q{QUALITY} 4:4:4, {len(data):,} bytes, sha256 {hashlib.sha256(data).hexdigest()}")
