"""Fixed views for judging refinements (#44): the same cameras before and after every change.

    blender -b painted_bedroom.blend -P render_views.py -- [--regen] [--out DIR] [--groups chairs,bed]

- views.json (next to this script) holds, per object group, three cameras in Unity coordinates:
  the painter's view (key P in Unity), the room centre at eye height, and a close-up. Missing
  groups are generated from the room; existing ones are kept, so the views stay fixed. --regen
  rebuilds them all.
- For each group it renders the three views and crops the painting to the group (its bounding box
  seen from the painter's view), then writes one comparison sheet per group:
  painting crop | painter's view, same crop | room centre | close-up.
"""
import json
import math
import os
import sys

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import room_io  # noqa: E402

VIEWS = os.path.join(HERE, "views.json")
PAINTING = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "Assets", "Art", "Painting", "The_Bedroom_Van_Gogh.jpg"))
DEFAULT_OUT = os.path.normpath(os.path.join(HERE, "..", "reference"))
EYE = 1.6

GROUPS = {
    "bed": ["Bed", "Pillow_Back", "Pillow_Front", "Blanket"],
    "chairs": ["Chair_Front", "Chair_Back"],
    "washstand": ["Washstand", "Carafe", "Basin", "Pitcher", "Tumbler", "SoapDish", "ClothesBrush",
                  "Bottle_Tall", "Bottle_Short", "BrushBox"],
    "pictures": ["Mirror", "Picture_Landscape", "Picture_PortraitMan", "Picture_PortraitWoman",
                 "Picture_DrawingLeft", "Picture_DrawingRight"],
    "room": ["Room_Walls", "Room_Floor", "Window", "Door_Left", "Door_Right"],
}
# what the close-up looks at, when a group is too big for one close-up
CLOSEUP_FOCUS = {"room": ["Window"], "pictures": ["Picture_Landscape", "Mirror"], "chairs": ["Chair_Front"], "washstand": ["Washstand", "Pitcher", "Basin"]}


def bbox(names):
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for n in names:
        emp = bpy.data.objects[n]
        for ob in emp.children_recursive:
            if ob.type != "MESH" or ob.name.endswith("_ink"):
                continue
            for c in ob.bound_box:
                w = ob.matrix_world @ Vector(c)
                lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi


def b2u(v):
    return [round(x, 4) for x in room_io.u2b(list(v)).tolist()]


def u2v(a):
    return Vector(room_io.u2b(a).tolist())


def make_views(scene):
    """Cameras in Unity coordinates (x right, y up, z forward)."""
    cam = scene.camera
    painter = {"position": b2u(cam.location),
               "target": b2u(bpy.data.objects["Marker_PainterView_Target"].location),
               "fov": round(scene["if_painterFov"], 3), "lensShiftY": round(scene["if_painterLensShiftY"], 4),
               "size": [1686, 1320]}
    flo, fhi = bbox(["Room_Floor"])
    centre = (flo + fhi) / 2; centre.z = EYE
    groups = {}
    for g, names in GROUPS.items():
        lo, hi = bbox(names)
        c = (lo + hi) / 2
        flo_, fhi_ = bbox(CLOSEUP_FOCUS.get(g, names))
        fc = (flo_ + fhi_) / 2
        size = max((fhi_ - flo_).x, (fhi_ - flo_).y, (fhi_ - flo_).z)
        towards = cam.location - fc; towards.z = 0; towards.normalize()
        near = fc + towards * (size * 1.3 + 0.5); near.z = min(fc.z + size * 0.35 + 0.15, 2.2)
        groups[g] = {"objects": names,
                     "centre": {"position": b2u(centre), "target": b2u(c), "fov": 60, "size": [1200, 900]},
                     "closeup": {"position": b2u(near), "target": b2u(fc), "fov": 45, "size": [1200, 900]}}
    return {"note": "Unity coordinates (metres, y up). fov is vertical, in degrees. painter = key P in Unity.",
            "painter": painter, "groups": groups}


def place(cam_ob, v, painter=False):
    cam_ob.location = u2v(v["position"])
    d = u2v(v["target"]) - cam_ob.location
    if painter:
        d.z = 0                      # P keeps the camera level and shifts the lens instead
    cam_ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()   # a camera looks down -Z, up is Y
    c = cam_ob.data
    c.sensor_fit = "VERTICAL"
    c.angle_y = math.radians(v["fov"])
    c.shift_x = 0
    c.shift_y = v.get("lensShiftY", 0)   # vertical fit: in frame heights, same as Unity's lens shift


def render(scene, path, size):
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.filepath = path
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)


def crop_rect(scene, names, pad=0.04):
    lo, hi = bbox(names)
    pts = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    uv = [world_to_camera_view(scene, scene.camera, p) for p in pts]
    xs = [min(max(p.x, 0), 1) for p in uv if p.z > 0]; ys = [min(max(1 - p.y, 0), 1) for p in uv if p.z > 0]
    x0, x1 = max(min(xs) - pad, 0), min(max(xs) + pad, 1)
    y0, y1 = max(min(ys) - pad, 0), min(max(ys) + pad, 1)
    return x0, y0, x1, y1


def sheet(out_png, painting, painter_png, rect, others, height=420):
    import numpy as np
    def load(p):
        im = bpy.data.images.load(p, check_existing=False)
        a = np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], 4)[::-1]
        bpy.data.images.remove(im)
        return a
    def crop(a, r):
        h, w = a.shape[:2]
        return a[int(r[1] * h):max(int(r[3] * h), int(r[1] * h) + 1), int(r[0] * w):max(int(r[2] * w), int(r[0] * w) + 1)]
    def fit(a):
        h, w = a.shape[:2]; nw = max(1, round(w * height / h))
        ys = (np.arange(height) * h / height).astype(int); xs = (np.arange(nw) * w / nw).astype(int)
        return a[ys][:, xs]
    pa = load(painting); pr = load(painter_png)
    tiles = [fit(crop(pa, rect)), fit(crop(pr, rect))] + [fit(load(p)) for p in others]
    gap = np.ones((height, 8, 4), np.float32)
    row = tiles[0]
    for t in tiles[1:]:
        row = np.concatenate([row, gap, t], axis=1)
    img = bpy.data.images.new("sheet", row.shape[1], row.shape[0], alpha=True)
    img.pixels.foreach_set(row[::-1].ravel())
    img.filepath_raw = out_png; img.file_format = "PNG"; img.save()
    bpy.data.images.remove(img)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = lambda k, d: argv[argv.index(k) + 1] if k in argv else d
    out = os.path.abspath(opt("--out", DEFAULT_OUT))
    only = opt("--groups", "").split(",") if "--groups" in argv else None
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = 16

    views = None
    if os.path.exists(VIEWS) and "--regen" not in argv:
        with open(VIEWS) as f:
            views = json.load(f)
    fresh = make_views(scene)
    if views is None:
        views = fresh
    else:
        for g, v in fresh["groups"].items():
            views["groups"].setdefault(g, v)
    with open(VIEWS, "w") as f:
        json.dump(views, f, indent=2)

    os.makedirs(out, exist_ok=True)
    cam = scene.camera
    place(cam, views["painter"], painter=True)
    painter_png = os.path.join(out, "painter_view.png")
    render(scene, painter_png, views["painter"]["size"])
    rects = {g: crop_rect(scene, v["objects"]) for g, v in views["groups"].items()}

    for g, v in views["groups"].items():
        if only and g not in only:
            continue
        shots = []
        for k in ("centre", "closeup"):
            place(cam, v[k])
            p = os.path.join(out, f"{g}_{k}.png")
            render(scene, p, v[k]["size"])
            shots.append(p)
        sheet(os.path.join(out, f"{g}_sheet.png"), PAINTING, painter_png, rects[g], shots)
        print(f"[render_views] {g}: crop {tuple(round(x, 3) for x in rects[g])} -> {g}_sheet.png")
    place(cam, views["painter"], painter=True)   # leave the scene camera on the painter's view
    print(f"[render_views] views: {VIEWS}; images: {out}")


if __name__ == "__main__":
    main()
