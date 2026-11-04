"""The look pass's Tripo models: two bushes and a cloud from Cici's GLBs (refs/3-starry-night/art-inputs, outside the
repo). No decimation (each is under 4,000 triangles). FBX only, no .blend.

    Blender --background --factory-startup --python make_tripo_look.py -- <art-inputs dir>

SN_BushA:  bush-a.glb, one uniform scale to 0.5 m tall (knee-high at the 0.8 rig); origin at the base centre.
SN_BushB:  bush-b.glb, one horizontal scale (x = y) so its widest horizontal extent over every direction is 0.8 m, and a
           vertical one to 0.25 m tall (the model is 0.55 as tall as wide: one uniform scale can't give both, as with the
           cypress); origin at the base centre.
SN_CloudA: cloud-a.glb, one uniform scale so its widest horizontal extent is 3.5 m; origin at the centre of its box.
Writes Assets/StarryNight/Models/SN_<name>.fbx and tripo_look_scales.json (the measured numbers). The base-colour
textures are made by make_textures.py. The FBX bytes repeat (sn_blender.py pins the exporter's time and ids).
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True   # no __pycache__ next to the sources
sys.path.insert(0, HERE)
import sn_blender as B

IN = sys.argv[sys.argv.index("--") + 1]
REPO = os.path.abspath(os.path.join(HERE, "../../.."))
MODELS = os.path.join(REPO, "Assets/StarryNight/Models")

BUSH_A_HEIGHT = 0.5
BUSH_B_WIDEST, BUSH_B_HEIGHT = 0.8, 0.25
CLOUD_WIDEST = 3.5


def import_glb(path, name):
    B.clear_scene()
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o != ob:
            bpy.data.objects.remove(o, do_unlink=True)
    ob.name = ob.data.name = name
    return ob


def verts(ob):
    a = np.empty(len(ob.data.vertices) * 3)
    ob.data.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def widest(v):
    best = 0.0
    for k in range(360):
        t = math.radians(k * 0.5)
        p = v[:, 0] * math.cos(t) + v[:, 1] * math.sin(t)
        best = max(best, float(p.max() - p.min()))
    return best


def scale(ob, sx, sy, sz):
    ob.scale = (sx, sy, sz)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def base_to_origin(ob):
    """Moves the mesh so its lowest point is at z = 0 and its base (the lowest 5 % of its height) is centred on x = y = 0."""
    v = verts(ob)
    zmin, h = v[:, 2].min(), np.ptp(v[:, 2])
    base = v[v[:, 2] < zmin + 0.05 * h]
    c = ((base[:, 0].min() + base[:, 0].max()) / 2, (base[:, 1].min() + base[:, 1].max()) / 2, zmin)
    ob.data.transform(Matrix.Translation((-c[0], -c[1], -c[2])))
    return c


def centre_to_origin(ob):
    v = verts(ob)
    c = (v.min(0) + v.max(0)) / 2
    ob.data.transform(Matrix.Translation((-c[0], -c[1], -c[2])))
    return c


def finish(ob, mat_name, rgba):
    ob.data.materials.clear()
    ob.data.materials.append(B.material(mat_name, rgba))
    for m in list(bpy.data.materials):
        if m.users == 0:
            bpy.data.materials.remove(m)
    for img in list(bpy.data.images):
        bpy.data.images.remove(img)
    ob.location = (0, 0, 0)


def measure(ob, extra):
    v = verts(ob)
    out = {"triangles": B.triangles(ob), "vertices": len(ob.data.vertices),
           "bbox_m": [round(float(np.ptp(v[:, i])), 4) for i in range(3)], "widest_m": round(widest(v), 4),
           "z_range_m": [round(float(v[:, 2].min()), 4), round(float(v[:, 2].max()), 4)]}
    out.update(extra)
    return out


out = {}

ob = import_glb(os.path.join(IN, "bush-a.glb"), "SN_BushA")
v = verts(ob)
h0, w0 = float(np.ptp(v[:, 2])), widest(v)
s = BUSH_A_HEIGHT / h0
scale(ob, s, s, s)
moved = base_to_origin(ob)
finish(ob, "SN_BushA", (0.25, 0.4, 0.2, 1))
out["bush_a"] = measure(ob, {"source_height_m": round(h0, 4), "source_widest_m": round(w0, 4), "scale_uniform": round(s, 4),
                             "origin_shift_m": [round(float(x), 4) for x in moved]})
B.export_fbx(os.path.join(MODELS, "SN_BushA.fbx"), [ob])

ob = import_glb(os.path.join(IN, "bush-b.glb"), "SN_BushB")
v = verts(ob)
h0, w0 = float(np.ptp(v[:, 2])), widest(v)
sh, sz = BUSH_B_WIDEST / w0, BUSH_B_HEIGHT / h0
scale(ob, sh, sh, sz)
moved = base_to_origin(ob)
finish(ob, "SN_BushB", (0.2, 0.4, 0.2, 1))
out["bush_b"] = measure(ob, {"source_height_m": round(h0, 4), "source_widest_m": round(w0, 4), "scale_horizontal": round(sh, 4),
                             "scale_vertical": round(sz, 4), "origin_shift_m": [round(float(x), 4) for x in moved]})
B.export_fbx(os.path.join(MODELS, "SN_BushB.fbx"), [ob])

ob = import_glb(os.path.join(IN, "cloud-a.glb"), "SN_CloudA")
v = verts(ob)
h0, w0 = float(np.ptp(v[:, 2])), widest(v)
s = CLOUD_WIDEST / w0
scale(ob, s, s, s)
moved = centre_to_origin(ob)
finish(ob, "SN_CloudA", (0.6, 0.65, 0.85, 1))
v = verts(ob)
out["cloud_a"] = measure(ob, {"source_widest_m": round(w0, 4), "scale_uniform": round(s, 4), "origin_shift_m": [round(float(x), 4) for x in moved],
                              "depth_to_width": round(float(min(np.ptp(v[:, 0]), np.ptp(v[:, 1])) / max(np.ptp(v[:, 0]), np.ptp(v[:, 1]))), 3)})
B.export_fbx(os.path.join(MODELS, "SN_CloudA.fbx"), [ob])

with open(os.path.join(HERE, "tripo_look_scales.json"), "w") as f:
    json.dump(out, f, indent=1)
print("TRIPO_LOOK|" + json.dumps(out))
