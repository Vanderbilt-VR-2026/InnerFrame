"""The Starry Night's art-pass textures, from Cici's inputs (refs/3-starry-night/art-inputs, outside the repo) and by
script. Same output every run.

    /usr/bin/python3 -I make_textures.py <art-inputs dir> <concept dir>

Writes to Assets/StarryNight/Textures/ (power-of-two sizes) and the data files beside this script:
  SN_SkyNight.png        sky-night.png, 2048 x 1024: the night round the whole dome (mirrored in yaw by the shader)
  SN_SkyPainted.png      sky-painted.png, 2048 x 1024: the paint panel
  SN_Horizon.png         horizon.png's band, magenta keyed out (soft, despilled), 2048 x 512 RGBA
  SN_HorizonGlow.png     the band's lit windows (warm pixels), 1024 x 256, a mask; horizon_glow.json: the brightest clusters
  SN_FarHills.png        the band's far hills alone (u 0.0195..0.5475, top rows: no spire, no village), over a solid lower
                         part fading to the mist's blue, 1024 x 256 RGBA: the ring of hills round the rest of the horizon
  SN_Grass.png           grass.png made seamless (offset blend), 1024 x 1024
  SN_PathDirt.png        the path: a warm dirt colour sampled from the concept (S01), ragged alpha edges, 256 x 1024
  SN_Flowers.png         flowers.png's six clumps, keyed and despilled, in a 2048 x 1024 atlas; flowers_atlas.json
  SN_Sketch.png          sketch-page.png, 1024 x 1024 (the sketchbook's pages and the page)
  SN_Cypress_BaseColor.png, SN_Rock_BaseColor.png   the Tripo models' base colours, 2048 -> 1024
  SN_HaloPainted.png     a star's halo as Van Gogh paints it: yellow core, pale-yellow dabs, blue-white strokes, 512 x 512
  SN_StarAtlas.png       a star's core (left) and the painted halo (right), 1024 x 512, for the star fields
  SN_MoonPainted.png     a larger crescent in a bigger, softer painted glow, 512 x 512
  SN_MistNoise.png       tileable soft noise for the mist layers, 512 x 512
"""
import json
import math
import struct
import sys
from pathlib import Path

sys.path.append("/Users/cicisss/Library/Python/3.9/lib/python/site-packages")
sys.dont_write_bytecode = True   # no __pycache__ next to the sources
sys.path.insert(0, str(Path(__file__).resolve().parent))   # sn_key.py (explicit, as -I leaves the script's folder out)
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

import sn_key as K

IN, CONCEPT = Path(sys.argv[1]), Path(sys.argv[2])
HERE = Path(__file__).resolve().parent
OUT = Path(__file__).resolve().parents[3] / "Assets/StarryNight/Textures"   # the repo root is three folders up
OUT.mkdir(parents=True, exist_ok=True)
SEED = 1889
SS = 2


def save(img, name):
    img.save(OUT / name, optimize=True)
    print(name, img.size, img.mode)


def blend_px(a, b, t):
    return a * (1 - t) + b * t


# ------------------------------------------------------------------------------------------------- skies

save(Image.open(IN / "sky-night.png").convert("RGB").resize((2048, 1024), Image.LANCZOS), "SN_SkyNight.png")
save(Image.open(IN / "sky-painted.png").convert("RGB").resize((2048, 1024), Image.LANCZOS), "SN_SkyPainted.png")

# ------------------------------------------------------------------------------------------------- horizon

hz = Image.open(IN / "horizon.png")
rgba, _ = K.key_out(hz)
a = rgba[..., 3]
rows = np.nonzero((a > 128).any(axis=1))[0]
r0, r1 = int(rows[0]) - 2, int(rows[-1]) + 3          # the band, a 2 px margin
band = Image.fromarray(K.bleed(rgba[r0:r1]))
save(band.resize((2048, 512), Image.LANCZOS), "SN_Horizon.png")
b = np.asarray(band, dtype=np.float64)
warm = (b[..., 0] > 165) & (b[..., 1] > 115) & (b[..., 2] < 125) & (b[..., 0] - b[..., 2] > 70) & (b[..., 3] > 200)
glow = Image.fromarray((warm * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.5))
save(glow.resize((1024, 256), Image.LANCZOS).convert("RGB"), "SN_HorizonGlow.png")
heat = np.asarray(Image.fromarray((warm * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(18)), dtype=np.float64)
peaks = []
for _ in range(3):
    y, x = np.unravel_index(int(np.argmax(heat)), heat.shape)
    peaks.append({"u": round((x + 0.5) / heat.shape[1], 4), "v_from_top": round((y + 0.5) / heat.shape[0], 4), "strength": round(float(heat[y, x] / 255), 3)})
    yy, xx = np.ogrid[:heat.shape[0], :heat.shape[1]]
    heat[(yy - y) ** 2 + (xx - x) ** 2 < 90 ** 2] = 0
spire_px = (1045, 426)   # by looking and by pixel value (cc-reports/2026-11-04-starry-night-art/raw/inputs.md)
json.dump({"source": "horizon.png", "band_rows_in_source": [r0, r1], "band_aspect": round(hz.size[0] / (r1 - r0), 3),
           "spire_tip_uv_in_band": [round((spire_px[0] + 0.5) / hz.size[0], 4), round((spire_px[1] - r0) / (r1 - r0), 4)],
           "glow_clusters": peaks}, open(HERE / "horizon_glow.json", "w"), indent=1)

# the far hills for the ring: the band, u 0.0195..0.5475 (both ends in a valley, so the mirrored repeat shows no twin
# peaks; the spire at u 0.68 stays out), rows v 0..0.28 from the top, then a solid foot
hb = np.asarray(band, dtype=np.float64)
bh, bw = hb.shape[:2]
hills = Image.fromarray(hb[: int(bh * 0.28), int(bw * 0.0195): int(bw * 0.5475)].astype(np.uint8)).resize((1024, 150), Image.LANCZOS)
hv = np.asarray(hills, dtype=np.float64)
solid = hv[..., 3] > 200
foot_top = np.median(hv[-12:][solid[-12:]][:, :3], axis=0)          # the hills' colour along the strip's bottom
mist_rgb = np.array([0.60, 0.67, 0.86]) * 255 * 0.55
foot = np.zeros((106, 1024, 4))
t = (np.arange(106) / 105.0)[:, None, None]
foot[..., :3] = foot_top * (1 - t) + mist_rgb * t
foot[..., 3] = 255
# the strip's lowest rows are solid: blend them into the foot so there's no seam
far = np.concatenate([hv, foot], axis=0)
far[140:150, :, 3] = 255
save(Image.fromarray(K.bleed(far.clip(0, 255).astype(np.uint8), passes=8)), "SN_FarHills.png")

# ------------------------------------------------------------------------------------------------- grass

def seamless(img, out_size):
    g = np.asarray(img.convert("RGB"), dtype=np.float64)
    h, w = g.shape[:2]
    rolled = np.roll(np.roll(g, h // 2, axis=0), w // 2, axis=1)
    y = np.minimum(np.arange(h), h - 1 - np.arange(h)) / (h / 2)       # 0 at the edges, 1 in the middle
    x = np.minimum(np.arange(w), w - 1 - np.arange(w)) / (w / 2)
    def ramp(t):
        t = np.clip((t - 0.06) / 0.30, 0, 1)
        return t * t * (3 - 2 * t)
    m = np.minimum(ramp(y)[:, None], ramp(x)[None, :])[..., None]
    out = g * m + rolled * (1 - m)
    return Image.fromarray(out.clip(0, 255).astype(np.uint8)).resize(out_size, Image.LANCZOS)

grass = Image.open(IN / "grass.png")
save(seamless(grass, (1024, 1024)), "SN_Grass.png")

# ------------------------------------------------------------------------------------------------- path dirt

s01 = np.asarray(Image.open(CONCEPT / "S01-panorama.png").convert("RGB"), dtype=np.float64)
patch = s01[880:1010, 560:700].reshape(-1, 3)      # the path in the foreground of S01 (looked at)
lum = patch.mean(1)
dirt = np.median(patch[(lum > np.percentile(lum, 40)) & (lum < np.percentile(lum, 90))], axis=0)
rng = np.random.default_rng(SEED)
W, H = 256, 1024
noise = Image.fromarray((rng.random((H // 8, W // 8)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(3))
nv = np.asarray(noise, dtype=np.float64) / 255
strokes = Image.fromarray((rng.random((H // 2, W // 16)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
sv = np.asarray(strokes, dtype=np.float64) / 255
col = dirt[None, None, :] * (0.78 + 0.32 * nv[..., None]) * (0.92 + 0.12 * sv[..., None])
u = (np.arange(W) + 0.5) / W
edge = np.minimum(u, 1 - u)[None, :] * 2                                   # 0 at the sides, 1 in the middle
ragged = edge + 0.35 * (nv - 0.5) + 0.15 * (sv - 0.5)
alpha = np.clip((ragged - 0.18) / 0.22, 0, 1)
save(Image.fromarray(np.dstack([col.clip(0, 255), alpha * 255]).astype(np.uint8)), "SN_PathDirt.png")

# ------------------------------------------------------------------------------------------------- flowers

def despill_toward_neighbours(rgba):
    """Pixels still magenta-tinted are pulled toward the average colour of their untinted neighbours (green, yellow)."""
    c = rgba[..., :3].astype(np.float64)
    al = rgba[..., 3].astype(np.float64) / 255
    tint = K.magenta_cast(c)
    ok = (al > 0.5) & (tint < 6)
    w = ok.astype(np.float64)
    def blur(arr, r):
        return np.asarray(Image.fromarray(arr.clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r)), dtype=np.float64)
    num = np.dstack([blur(c[..., i] * w, 6) for i in range(3)])
    den = blur(w * 255, 6)[..., None] / 255
    target = np.where(den > 0.02, num / np.maximum(den, 1e-3), c)
    t = np.clip(tint / 40, 0, 1)[..., None]
    c = c * (1 - t) + target * t
    return np.dstack([c.clip(0, 255), rgba[..., 3]]).astype(np.uint8)

fl = Image.open(IN / "flowers.png")
fr, _ = K.key_out(fl)
fr = despill_toward_neighbours(fr)
fa = fr[..., 3] > 76
small = np.asarray(Image.fromarray((fa * 255).astype(np.uint8)).resize((fl.size[0] // 4, fl.size[1] // 4), Image.BOX).filter(ImageFilter.MaxFilter(7))) > 0
lab = np.zeros(small.shape, dtype=np.int32)
boxes = []
for y0, x0 in zip(*np.nonzero(small)):
    if lab[y0, x0]:
        continue
    n = len(boxes) + 1
    stack = [(y0, x0)]; lab[y0, x0] = n; bx = [y0, y0, x0, x0]
    while stack:
        cy, cx = stack.pop()
        bx = [min(bx[0], cy), max(bx[1], cy), min(bx[2], cx), max(bx[3], cx)]
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                ny, nx = cy + dy, cx + dx
                if 0 <= ny < small.shape[0] and 0 <= nx < small.shape[1] and small[ny, nx] and not lab[ny, nx]:
                    lab[ny, nx] = n; stack.append((ny, nx))
    boxes.append((bx[2] * 4, bx[0] * 4, (bx[3] + 1) * 4, (bx[1] + 1) * 4))
boxes = [bb for bb in boxes if (bb[2] - bb[0]) * (bb[3] - bb[1]) > 20000]
boxes.sort(key=lambda bb: (bb[1] // 300, bb[0]))   # top row left to right, then the bottom row
atlas = np.zeros((1024, 2048, 4), dtype=np.uint8)
cells = []
CW, CH, PAD = 2048 // 3, 512, 6
for i, (x0, y0, x1, y1) in enumerate(boxes[:6]):
    crop = Image.fromarray(fr).crop((max(0, x0 - 8), max(0, y0 - 8), min(fl.size[0], x1 + 8), min(fl.size[1], y1 + 8)))
    s = min((CW - 2 * PAD) / crop.width, (CH - 2 * PAD) / crop.height)
    crop = crop.resize((max(1, round(crop.width * s)), max(1, round(crop.height * s))), Image.LANCZOS)
    cx, cy = (i % 3) * CW, (i // 3) * CH
    px = cx + (CW - crop.width) // 2
    py = cy + CH - PAD - crop.height                       # bottom-aligned: the clump stands on its cell's floor
    atlas[py:py + crop.height, px:px + crop.width] = np.asarray(crop)
    cells.append({"uv_min": [round(px / 2048, 5), round(1 - (py + crop.height) / 1024, 5)],
                  "uv_max": [round((px + crop.width) / 2048, 5), round(1 - py / 1024, 5)],
                  "aspect_w_over_h": round(crop.width / crop.height, 4)})
save(Image.fromarray(K.bleed(atlas, passes=12)), "SN_Flowers.png")
json.dump({"source": "flowers.png", "atlas": [2048, 1024], "clumps": cells}, open(HERE / "flowers_atlas.json", "w"), indent=1)

# ------------------------------------------------------------------------------------------------- sketch, Tripo textures

save(Image.open(IN / "sketch-page.png").convert("RGB").resize((1024, 1024), Image.LANCZOS), "SN_Sketch.png")

def glb_image(path):
    data = open(path, "rb").read()
    clen = struct.unpack_from("<I", data, 12)[0]
    j = json.loads(data[20:20 + clen])
    bin_start = 20 + clen + 8
    bv = j["bufferViews"][j["images"][0]["bufferView"]]
    import io
    return Image.open(io.BytesIO(data[bin_start + bv.get("byteOffset", 0): bin_start + bv.get("byteOffset", 0) + bv["byteLength"]]))

save(glb_image(IN / "cypress.glb").convert("RGB").resize((1024, 1024), Image.LANCZOS), "SN_Cypress_BaseColor.png")
save(glb_image(IN / "rock.glb").convert("RGB").resize((1024, 1024), Image.LANCZOS), "SN_Rock_BaseColor.png")

# ------------------------------------------------------------------------------------------------- painted halos, moon, star atlas

def painted_halo(size=512, core=0.16, dabs=0.42, strokes=0.74, seed=SEED, crescent=False):
    rng = np.random.default_rng(seed)
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img, "RGBA")
    c = S / 2
    def stroke(rad, ang, length, width, colour):
        # a short curved dab along the circle (tangential), as a thick polyline
        pts = []
        for k in range(6):
            a = ang + (k / 5 - 0.5) * length / rad
            rr = rad * (1 + 0.02 * math.sin(k))
            pts.append((c + rr * math.cos(a), c + rr * math.sin(a)))
        d.line(pts, fill=colour, width=int(width), joint="curve")
    # outer ring: blue-white strokes
    for _ in range(170):
        rad = S / 2 * strokes * (0.86 + 0.24 * rng.random())
        stroke(rad, rng.random() * 2 * math.pi, S * (0.05 + 0.06 * rng.random()), S * (0.010 + 0.008 * rng.random()),
               (int(185 + 60 * rng.random()), int(205 + 45 * rng.random()), 255, int(140 + 90 * rng.random())))
    # middle ring: pale yellow dabs
    for _ in range(150):
        rad = S / 2 * dabs * (0.78 + 0.40 * rng.random())
        stroke(rad, rng.random() * 2 * math.pi, S * (0.035 + 0.045 * rng.random()), S * (0.013 + 0.010 * rng.random()),
               (255, int(225 + 25 * rng.random()), int(140 + 60 * rng.random()), int(120 + 100 * rng.random())))
    img = img.filter(ImageFilter.GaussianBlur(S * 0.004))
    arr = np.asarray(img, dtype=np.float64)
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float64)
    r = np.hypot(xx - c, yy - c) / (S / 2)
    # the bright yellow core (or a crescent for the moon)
    if crescent:
        disc = np.hypot(xx - c, yy - c) < S / 2 * core
        bite = np.hypot(xx - c - S / 2 * core * 0.55, yy - c + S / 2 * core * 0.25) < S / 2 * core * 0.95
        shape = (disc & ~bite).astype(np.float64)
        core_a = np.asarray(Image.fromarray((shape * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(S * 0.004)), dtype=np.float64) / 255
        glow = np.exp(-(r / (core * 2.4)) ** 2) * 0.55
        core_rgb = np.array([250, 214, 92.0])
    else:
        core_a = np.clip(1 - r / core, 0, 1) ** 1.2
        glow = np.exp(-(r / (core * 2.2)) ** 2) * 0.6
        core_rgb = np.array([255, 236, 150.0])
    base_a = np.maximum(core_a, glow)
    base = np.dstack([np.broadcast_to(core_rgb, (S, S, 3)), base_a[..., None] * 255])
    fa = arr[..., 3:4] / 255
    rgb = base[..., :3] * (1 - fa) + arr[..., :3] * fa
    rgb = rgb * (1 - core_a[..., None]) + core_rgb * core_a[..., None]
    alpha = np.clip(base[..., 3:4] / 255 + fa * (1 - base[..., 3:4] / 255), 0, 1)
    alpha *= np.clip((1 - r) / 0.06, 0, 1)[..., None]       # nothing at the square's edge
    out = Image.fromarray(np.dstack([rgb.clip(0, 255), alpha * 255]).astype(np.uint8))
    return out.resize((size, size), Image.LANCZOS)

halo = painted_halo()
save(halo, "SN_HaloPainted.png")
save(painted_halo(512, core=0.30, dabs=0.55, strokes=0.82, seed=SEED + 1, crescent=True), "SN_MoonPainted.png")
yy, xx = np.mgrid[0:512, 0:512].astype(np.float64)
rr = np.hypot(xx - 255.5, yy - 255.5) / 256
core_a = np.clip(1 - rr, 0, 1) ** 3 + 0.6 * np.exp(-(rr / 0.12) ** 2)
core_img = Image.fromarray(np.dstack([np.full((512, 512), 255.0), np.full((512, 512), 248.0), np.full((512, 512), 226.0),
                                      (core_a / core_a.max()) * 255]).astype(np.uint8))
star_atlas = Image.new("RGBA", (1024, 512), (0, 0, 0, 0))
star_atlas.paste(core_img, (0, 0)); star_atlas.paste(halo, (512, 0))
save(star_atlas, "SN_StarAtlas.png")

# ------------------------------------------------------------------------------------------------- mist noise

rng = np.random.default_rng(SEED + 2)
acc = np.zeros((512, 512))
for octave, amp in ((8, 1.0), (16, 0.5), (32, 0.25), (64, 0.12)):
    grid = rng.random((octave, octave))
    big = np.tile(grid, (3, 3))
    up = np.asarray(Image.fromarray((big * 255).astype(np.uint8)).resize((512 * 3, 512 * 3), Image.BICUBIC), dtype=np.float64)[512:1024, 512:1024] / 255
    acc += amp * up
acc = (acc - acc.min()) / (acc.max() - acc.min())
mist_a = np.clip((acc - 0.30) / 0.55, 0, 1) ** 1.3
save(Image.fromarray(np.dstack([np.full((512, 512), 236.0), np.full((512, 512), 240.0), np.full((512, 512), 252.0), mist_a * 255]).astype(np.uint8)),
     "SN_MistNoise.png")
