"""The Gallery's surface textures from ChatGPT images (docs/gallery/TEXTURE_PROMPTS.md), made ready for Unity.

    Blender --background --factory-startup --python make_textures.py -- <folder with wall.png, floor.png, ...>

For each image it finds (any missing one is skipped), writes Assets/Art/Gallery/Textures/GT_<Name>_BaseColor.jpg:
  - tiling ones (wall, ceiling, floor, trim, bench wood and fabric): the centre square, its edges cross-faded into the
    opposite edges so it repeats without a seam (the band is SEAM of the width), then sized to 1024 (trim 512);
  - the wainscot: a 1.5 x 1 m strip (3 : 2), seamless left to right only, since it never repeats upwards;
  - the rug and the door: pictures mapped once, cropped from the centre to their shape (rug 2 x 3 m, door 1.6 x 2.6 m)
    and sized so their long side is 1024 (door 1536).
Colours are kept as they are: the images are saved with Blender's Standard view transform (AgX would shift them).
GalleryDecorSetup.cs then puts each on its material at the size in SLOTS (metres per repeat).
"""
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
IN = os.path.expanduser(sys.argv[sys.argv.index("--") + 1])
OUT = os.path.join(HERE, "../../../Assets/Art/Gallery/Textures")
SEAM = 0.12

# input file: (output name, kind, output size (w, h), shape w / h)
SLOTS = {
    "wall.png":         ("Wall",        "tile",   (1024, 1024), 1.0),
    "ceiling.png":      ("Ceiling",     "tile",   (1024, 1024), 1.0),
    "floor.png":        ("Floor",       "tile",   (1024, 1024), 1.0),
    "trim.png":         ("Trim",        "tile",   (512, 512),   1.0),
    "wainscot.png":     ("Wainscot",    "tile_x", (1536, 1024), 1.5),
    "bench_wood.png":   ("BenchWood",   "tile",   (1024, 1024), 1.0),
    "bench_fabric.png": ("Cushion",     "tile",   (1024, 1024), 1.0),
    "rug.png":          ("Rug",         "fit",    (683, 1024),  2.0 / 3.0),
    "door.png":         ("Door",        "fit",    (945, 1536),  1.6 / 2.6),
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
    blended = head * t + tail * (1.0 - t)
    idx = [slice(None)] * px.ndim
    idx[axis] = slice(0, b)
    keep[tuple(idx)] = blended
    return keep


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
    return path


os.makedirs(OUT, exist_ok=True)
done = []
for file, (name, kind, size, shape) in SLOTS.items():
    src = os.path.join(IN, file)
    if not os.path.exists(src):
        continue
    img = bpy.data.images.load(src)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    px[:, :, 3] = 1.0
    px = crop(px, shape)
    if kind in ("tile", "tile_x"):
        px = wrap(px, 1)
    if kind == "tile":
        px = wrap(px, 0)
    done.append(f"{file} ({w} x {h}) -> {os.path.relpath(save(px, name, size), os.path.join(HERE, '../../..'))}")
print("\n".join(done) if done else f"no textures found in {IN} (looked for {', '.join(SLOTS)})")
