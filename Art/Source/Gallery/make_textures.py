"""The Gallery's surface textures from ChatGPT images (docs/gallery/TEXTURE_PROMPTS.md), made ready for Unity.

    Blender --background --factory-startup --python make_textures.py -- <folder with wall.png, floor.webp, ...>

The yellow wall and the corridor picture (2026-11-12) keep their source images in the repo, in
Art/Source/Gallery/textures/ (with the yellow concept photo they follow); run the script on that folder to remake the
wall and the corridor's door from them. The other sources are still outside the repo (see docs/gallery/TEXTURE_PROMPTS.md).

Each input is found by its name with any image extension (.png, .webp, .jpg); a missing one is skipped. For each it
writes Assets/Art/Gallery/Textures/GT_<Name>_BaseColor.jpg:
  - "tile": the centre square, its edges cross-faded into the opposite edges so it repeats without a seam (the band is
    SEAM of the width), sized to 1024 (the plain trim to 256). The floor is turned a quarter turn first, so its planks run north-south;
  - "band": a moulding profile (stripes across the image's height) for a strip mapped once from its bottom to its top
    (the cornice, the wainscot's rail): seamless left to right only;
  - "plain": the trim image's widest band with no moulding line in it (the rows whose average changes least), tiled:
    for the arch's moulding and the skylight's frame, which a profile would stripe the wrong way;
  - "tile_x": the wainscot, 1.5 x 1 m (3 : 2), seamless left to right only (it never repeats upwards);
  - "fit": a picture mapped once (the rug, 2 x 3 m), cropped from the centre to its shape;
  - "stretch": a picture mapped once and stretched to its shape rather than cropped (the door, 1.6 x 2.6 m, until
    2026-11-12: so its frame stayed whole);
  - "crop": a part of a picture (CROPS: its pixels, from the top left), mapped once: the corridor's end door, from the
    corridor picture (corridor.png, the ChatGPT original). Its panes also make GT_<Name>_Emission.jpg: the picture where
    it is as bright as the lit panes (GLOW_FROM..GLOW_FULL of the brightest), black elsewhere, so only they glow. (The
    corridor was a flat picture behind the arch until the real one, 2026-11-12: a "backdrop", blurred and darkened.)
For "tile" it prints how strong the seam is before and after: the step across the wrap (last column to first, last row
to first) over the 99th percentile of the steps between neighbouring columns (rows) inside the image. Under 1, the wrap
is no sharper than the image's own brushwork.
Colours are kept as they are: images are saved with Blender's Standard view transform (AgX would shift them).
GalleryShellSetup.cs then puts each on its material at its real-world size.
"""
import glob
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
IN = os.path.expanduser(sys.argv[sys.argv.index("--") + 1])
OUT = os.path.join(HERE, "../../../Assets/Art/Gallery/Textures")
SEAM = 0.12
# corridor.png's door: its two leaves, (left, top, right, bottom) in pixels from the top left (the frame round them is
# modelled); 252 x 418, 0.603 wide for 1 tall (build_gallery.py DOOR_ASPECT). Measured on the 1024 x 1536 original.
CROPS = {"CorridorDoor": (386, 557, 638, 975)}
# the panes: the upper part of the door (its top ROWS of the crop's height; the knobs and the lower panels below), as
# bright as GLOW_FROM..GLOW_FULL (luma, sRGB 0-1)
GLOW_ROWS = 0.62
GLOW_FROM, GLOW_FULL = 0.40, 0.55

# input name: [(output name, kind, output size (w, h), shape w / h), ...]
SLOTS = {
    "wall":         [("Wall", "tile", (1024, 1024), 1.0)],
    "ceiling":      [("Ceiling", "tile", (1024, 1024), 1.0)],
    "sky":          [("Sky", "tile", (1024, 1024), 1.0)],   # seen through the skylight, on a card above it
    "floor":        [("Floor", "tile_turned", (1024, 1024), 1.0)],
    "wainscot":     [("Wainscot", "tile_x", (1536, 1024), 1.5)],
    "frame":        [("Frame", "tile_x", (2048, 683), 3.0)],   # gilt, along the frame's rails; bottom = inner edge
    "cornice":      [("Cornice", "band", (1024, 1024), 1.0)],
    "trim":         [("Rail", "band", (1024, 1024), 1.0), ("Trim", "plain", (256, 256), 1.0)],
    "bench_wood":   [("BenchWood", "tile", (1024, 1024), 1.0)],
    "pedestal":     [("Pedestal", "tile", (1024, 1024), 1.0)],   # the button pedestals' ebonised wood
    "marble":       [("Marble", "tile", (1024, 1024), 1.0)],     # the statue's plinth
    "bench_fabric": [("Cushion", "tile", (1024, 1024), 1.0)],
    "rug":          [("Rug", "fit", (683, 1024), 2.0 / 3.0)],
    "corridor":     [("CorridorDoor", "crop", (512, 1024), None)],   # the corridor's end door (2026-11-12)
}


def crop(px, shape):
    h, w = px.shape[:2]
    if w / h > shape:
        cw = int(round(h * shape)); x = (w - cw) // 2
        return px[:, x:x + cw]
    ch = int(round(w / shape)); y = (h - ch) // 2
    return px[y:y + ch]


def wrap(px, axis):
    """Cross-fade the last SEAM of the image into its first, then drop it: the result repeats without a seam."""
    n = px.shape[axis]
    b = int(n * SEAM)
    keep = np.take(px, range(0, n - b), axis=axis).copy()
    head = np.take(px, range(0, b), axis=axis)
    tail = np.take(px, range(n - b, n), axis=axis)
    t = np.linspace(0.0, 1.0, b, endpoint=False).reshape([-1 if k == axis else 1 for k in range(px.ndim)])
    idx = [slice(None)] * px.ndim
    idx[axis] = slice(0, b)
    keep[tuple(idx)] = head * t + tail * (1.0 - t)
    return keep


def seam(px, axis):
    """The step across the wrap over the 99th percentile of the steps between neighbouring lines inside (see above)."""
    a = np.moveaxis(px[:, :, :3], axis, 0)
    inside = np.abs(np.diff(a, axis=0)).mean(axis=(1, 2))
    return float(np.abs(a[-1] - a[0]).mean() / np.percentile(inside, 99))


def plain_band(px):
    """A square from the trim image's quietest stretch: the window, a fifth of its height, whose sharpest row-to-row change
    (a moulding line) is smallest, so a panel of plain painted wood with no line across it."""
    rows = px[:, :, :3].mean(axis=(1, 2))
    step = np.abs(np.diff(rows))
    n = len(rows) // 5
    a = min(range(len(step) - n), key=lambda i: step[i:i + n].max())
    band = px[a:a + n]
    w = px.shape[1]
    return band[:, (w - n) // 2:(w - n) // 2 + n]


def save(px, name, size, wraps=(), suffix="BaseColor"):
    """Scale to `size` and write the JPEG. Along each axis in `wraps` (0 up, 1 across) the image is first extended by
    a margin of itself from the opposite edge and cropped back after scaling, so the resampling runs across the
    repeat as it does inside the image (scaled on its own, the edges were resampled differently: a faint seam)."""
    h, w = px.shape[:2]
    m = 16
    pad = [(m, m) if a in wraps else (0, 0) for a in (0, 1)] + [(0, 0)]
    big = np.pad(px, pad, mode="wrap") if wraps else px
    sy, sx = size[1] / h, size[0] / w
    bw, bh = (round(big.shape[1] * sx), round(big.shape[0] * sy)) if wraps else size
    img = bpy.data.images.new(name + "_big", big.shape[1], big.shape[0])
    img.pixels = np.ascontiguousarray(big).ravel()
    img.scale(bw, bh)
    if wraps:
        out = np.array(img.pixels[:], dtype=np.float32).reshape(bh, bw, 4)
        y0 = round(m * sy) if 0 in wraps else 0
        x0 = round(m * sx) if 1 in wraps else 0
        out = np.ascontiguousarray(out[y0:y0 + size[1], x0:x0 + size[0]])
        bpy.data.images.remove(img)
        img = bpy.data.images.new(name, size[0], size[1])
        img.pixels = out.ravel()
    scene = bpy.context.scene
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    path = os.path.join(OUT, f"GT_{name}_{suffix}.jpg")
    img.save_render(path, scene=scene)
    if wraps:
        global last_seam
        last_seam = (seam(np.array(img.pixels[:], dtype=np.float32).reshape(size[1], size[0], 4), 1),
                     seam(np.array(img.pixels[:], dtype=np.float32).reshape(size[1], size[0], 4), 0))
    return os.path.relpath(path, os.path.join(HERE, "../../.."))


# brightness lifts, for images too dark to show their strokes under the room's lights (linear colour times this)
# A tuple is per channel (R, G, B), to shift the colour. The Ceiling gain (2026-11-11) was applied to the
# GT_*_BaseColor.jpg already in the repo, so don't apply it again by hand; running this script from the source images does.
# (The red wall's (1.55, 3.7, 3.2), crimson to orange-red, went with it on 2026-11-12: the yellow wall needs no shift.)
GAIN = {
    "Pedestal": 2.6,               # ChatGPT's ebonised wood: mean ~8 %; at 1.8 it still read black from 2 m
    "Ceiling": (1.22, 1.43, 1.6),  # beige (203, 162, 122) to a light cream (222, 190, 152): the room's biggest surface
}

os.makedirs(OUT, exist_ok=True)
done = []
for stem, outputs in SLOTS.items():
    found = sorted(f for f in glob.glob(os.path.join(IN, stem + ".*")) if f.lower().endswith((".png", ".webp", ".jpg", ".jpeg")))
    if not found:
        continue
    img = bpy.data.images.load(found[0])
    w, h = img.size
    src = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)   # row 0 is the image's bottom
    src[:, :, 3] = 1.0
    for name, kind, size, shape in outputs:
        px = src
        if kind == "tile_turned":
            px = np.ascontiguousarray(np.rot90(px))
        if kind == "plain":
            px = plain_band(px)
        if kind == "crop":
            x0, y0, x1, y1 = CROPS[name]
            px = px[h - y1:h - y0, x0:x1]   # row 0 is the image's bottom
        px = crop(px, shape) if kind not in ("band", "stretch", "crop") else px
        note = ""
        if kind == "tile":
            before = (seam(px, 1), seam(px, 0))
        if kind in ("tile", "tile_turned", "tile_x", "band", "plain"):
            px = wrap(px, 1)
        if kind in ("tile", "tile_turned", "plain"):
            px = wrap(px, 0)
        if kind == "crop":
            luma = px[:, :, 0] * 0.2126 + px[:, :, 1] * 0.7152 + px[:, :, 2] * 0.0722
            m = np.clip((luma - GLOW_FROM) / (GLOW_FULL - GLOW_FROM), 0.0, 1.0)
            m[: int(px.shape[0] * (1.0 - GLOW_ROWS))] = 0.0   # the rows below the panes (row 0 is the bottom)
            glow = px.copy(); glow[:, :, :3] *= m[:, :, None]
            save(np.ascontiguousarray(glow), name, size, (), "Emission")
            note = f", {px.shape[1]} x {px.shape[0]} px, its panes ({(m > 0.5).mean() * 100:.0f} % of it) -> GT_{name}_Emission.jpg"
        if name in GAIN:
            px = px.copy(); px[:, :, :3] = np.clip(px[:, :, :3] * np.asarray(GAIN[name], dtype=np.float32), 0.0, 1.0)
        wraps = (0, 1) if kind in ("tile", "tile_turned", "plain") else (1,) if kind in ("tile_x", "band") else ()
        saved = save(np.ascontiguousarray(px), name, size, wraps)
        if kind == "tile":
            note = (f", seam left-right {before[0]:.2f} -> {last_seam[0]:.2f}, top-bottom {before[1]:.2f} -> {last_seam[1]:.2f}"
                    " (as scaled, before the JPEG)")
        done.append(f"{os.path.basename(found[0])} ({w} x {h}) -> {saved} ({kind}{note})")
print("\n".join(done) if done else f"no textures found in {IN} (looked for {', '.join(SLOTS)})")
