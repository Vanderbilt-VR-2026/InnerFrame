"""The Gallery's surface textures from ChatGPT images (docs/gallery/TEXTURE_PROMPTS.md), made ready for Unity.

    Blender --background --factory-startup --python make_textures.py -- <folder with wall.png, floor.webp, ...>

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
  - "stretch": the door (1.6 x 2.6 m), mapped once and stretched to its shape rather than cropped, so its frame stays
    whole: crop it to the door's outer frame and the arch's apex first.
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
    "door":         [("Door", "stretch", (945, 1536), 1.6 / 2.6)],
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


def save(px, name, size):
    h, w = px.shape[:2]
    img = bpy.data.images.new(name, w, h)
    img.pixels = px.ravel()
    img.scale(*size)
    scene = bpy.context.scene
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    path = os.path.join(OUT, f"GT_{name}_BaseColor.jpg")
    img.save_render(path, scene=scene)
    return os.path.relpath(path, os.path.join(HERE, "../../.."))


# brightness lifts, for images too dark to show their strokes under the room's lights (linear colour times this)
GAIN = {"Pedestal": 2.6}   # ChatGPT's ebonised wood: mean ~8 %; at 1.8 it still read black from 2 m

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
        px = crop(px, shape) if kind not in ("band", "stretch") else px
        if kind in ("tile", "tile_turned", "tile_x", "band", "plain"):
            px = wrap(px, 1)
        if kind in ("tile", "tile_turned", "plain"):
            px = wrap(px, 0)
        if name in GAIN:
            px = px.copy(); px[:, :, :3] = np.clip(px[:, :, :3] * GAIN[name], 0.0, 1.0)
        done.append(f"{os.path.basename(found[0])} ({w} x {h}) -> {save(np.ascontiguousarray(px), name, size)} ({kind})")
print("\n".join(done) if done else f"no textures found in {IN} (looked for {', '.join(SLOTS)})")
