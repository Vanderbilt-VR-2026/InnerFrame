"""Vincent v2's base colour, out of his FBX: Mixamo's character file (VincentV2.fbx, from Cici's Tripo model) embeds
Tripo's 4096 x 4096 PNG. This copies that PNG's bytes out of the FBX (it is stored whole, between the PNG signature and
its IEND chunk), checks it decodes, and writes it at 2048 x 2048 (Lanczos) as Assets/StarryNight/Textures/
SN_VincentV2_BaseColor.png, so Unity never has to extract it (no .fbm folder) and the material points at a texture of
our own size. The same bytes on every run (same input, same Pillow).

    /usr/bin/python3 -I make_vincent_texture.py
"""
import hashlib
import io
import sys
from pathlib import Path

sys.path.append("/Users/cicisss/Library/Python/3.9/lib/python/site-packages")
from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
FBX = REPO / "Assets/StarryNight/Models/Vincent/VincentV2.fbx"
OUT = REPO / "Assets/StarryNight/Textures/SN_VincentV2_BaseColor.png"
SIZE = 2048

SIG, IEND = b"\x89PNG\r\n\x1a\n", b"IEND\xaeB`\x82"

data = FBX.read_bytes()
found = []
at = data.find(SIG)
while at >= 0:
    end = data.find(IEND, at)
    if end < 0:
        sys.exit(f"a PNG signature at byte {at} has no IEND")
    found.append(data[at:end + len(IEND)])
    at = data.find(SIG, end)
if len(found) != 1:
    sys.exit(f"expected one embedded PNG in {FBX.name}, found {len(found)}")
png = found[0]
im = Image.open(io.BytesIO(png))
im.load()
print(f"embedded PNG: {len(png):,} bytes, md5 {hashlib.md5(png).hexdigest()}, {im.size[0]} x {im.size[1]} {im.mode}")
alpha = im.getchannel("A").getextrema() if im.mode == "RGBA" else None
rgb = im.convert("RGB")
if rgb.size != (SIZE, SIZE):
    rgb = rgb.resize((SIZE, SIZE), Image.LANCZOS)
rgb.save(OUT, optimize=False)
print(f"alpha range {alpha} (dropped: opaque)" if alpha else "no alpha")
print(f"wrote {OUT.relative_to(REPO)}: {SIZE} x {SIZE} RGB, {OUT.stat().st_size:,} bytes, md5 {hashlib.md5(OUT.read_bytes()).hexdigest()}")
