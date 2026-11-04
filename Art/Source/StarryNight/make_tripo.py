"""The cypress and the rock from Cici's Tripo models (refs/3-starry-night/art-inputs, outside the repo). No decimation.

    Blender --background --factory-startup --python make_tripo.py -- <art-inputs dir>

SN_Cypress: 7 m tall; one horizontal scale (x = y) so the widest horizontal extent, over every direction, is 1.6 m.
SN_Rock:    one uniform scale that puts the area-weighted mean height of its upward-facing top (normal z > 0.7) at 0.26 m.
Both keep the model's origin at the base centre (lowest point at z = 0). Writes Assets/StarryNight/Models/SN_<name>.fbx,
SN_<name>.blend here, and tripo_scales.json (the measured numbers). The base-colour textures are made by make_textures.py.
"""
import json
import math
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True   # no __pycache__ next to the sources
sys.path.insert(0, HERE)
import sn_blender as B

IN = sys.argv[sys.argv.index("--") + 1]
REPO = os.path.abspath(os.path.join(HERE, "../../.."))
MODELS = os.path.join(REPO, "Assets/StarryNight/Models")

CYPRESS_HEIGHT, CYPRESS_WIDEST = 7.0, 1.6
ROCK_TOP_MEAN = 0.26


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
    # unparent (glTF may add a root node), keep the world transform, then apply it
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


def base_to_origin(ob):
    """Moves the mesh so its lowest point is at z = 0 and its base (the lowest 5 cm) is centred on x = y = 0."""
    v = verts(ob)
    zmin = v[:, 2].min()
    base = v[v[:, 2] < zmin + 0.05]
    c = np.array([(base[:, 0].min() + base[:, 0].max()) / 2, (base[:, 1].min() + base[:, 1].max()) / 2, zmin])
    ob.data.transform(__import__("mathutils").Matrix.Translation((-c[0], -c[1], -c[2])))
    return c


def top_mean(ob):
    me = ob.data
    me.calc_loop_triangles()
    hs, ws = [], []
    for t in me.loop_triangles:
        if t.normal.z > 0.7:
            hs.append(t.center.z)
            ws.append(t.area)
    hs, ws = np.array(hs), np.array(ws)
    m = float((hs * ws).sum() / ws.sum())
    sd = float(math.sqrt(((hs - m) ** 2 * ws).sum() / ws.sum()))
    return m, sd, float(ws.sum())


def finish(ob, mat_name, rgba):
    ob.data.materials.clear()
    ob.data.materials.append(B.material(mat_name, rgba))
    # drop the glTF image and node material (the texture ships as a PNG, made by make_textures.py)
    for m in list(bpy.data.materials):
        if m.users == 0:
            bpy.data.materials.remove(m)
    for img in list(bpy.data.images):
        bpy.data.images.remove(img)
    ob.location = (0, 0, 0)


out = {}

# ------------------------------------------------------------------ cypress
ob = import_glb(os.path.join(IN, "cypress.glb"), "SN_Cypress")
v = verts(ob)
h0, w0 = float(v[:, 2].max() - v[:, 2].min()), widest(v)
sz, sh = CYPRESS_HEIGHT / h0, CYPRESS_WIDEST / w0
ob.scale = (sh, sh, sz)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
moved = base_to_origin(ob)
v = verts(ob)
finish(ob, "SN_Cypress", (0.13, 0.17, 0.15, 1))
out["cypress"] = {
    "source_height_m": round(h0, 4), "source_widest_m": round(w0, 4),
    "scale_vertical": round(sz, 4), "scale_horizontal": round(sh, 4),
    "height_m": round(float(v[:, 2].max() - v[:, 2].min()), 4), "widest_m": round(widest(v), 4),
    "bbox_m": [round(float(v[:, i].max() - v[:, i].min()), 4) for i in range(3)],
    "origin_shift_m": [round(float(x), 4) for x in moved], "triangles": B.triangles(ob),
    "vertices": len(ob.data.vertices)}
B.export_fbx(os.path.join(MODELS, "SN_Cypress.fbx"), [ob])
B.save_blend(os.path.join(HERE, "SN_Cypress.blend"))

# ------------------------------------------------------------------ rock
ob = import_glb(os.path.join(IN, "rock.glb"), "SN_Rock")
base_to_origin(ob)
m0, sd0, _ = top_mean(ob)
s = ROCK_TOP_MEAN / m0
ob.scale = (s, s, s)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
moved = base_to_origin(ob)
m1, sd1, area = top_mean(ob)
v = verts(ob)
finish(ob, "SN_Rock", (0.35, 0.47, 0.6, 1))
top = v[v[:, 2] > np.percentile(v[:, 2], 90)]
out["rock"] = {
    "source_top_mean_m": round(m0, 4), "scale_uniform": round(s, 4),
    "top_mean_m": round(m1, 4), "top_sd_m": round(sd1, 4), "top_area_m2": round(area, 4),
    "bbox_m": [round(float(v[:, i].max() - v[:, i].min()), 4) for i in range(3)],
    "top10pct_span_m": round(float(top[:, 2].max() - top[:, 2].min()), 4),
    "origin_shift_m": [round(float(x), 4) for x in moved], "triangles": B.triangles(ob), "vertices": len(ob.data.vertices)}
B.export_fbx(os.path.join(MODELS, "SN_Rock.fbx"), [ob])
B.save_blend(os.path.join(HERE, "SN_Rock.blend"))

with open(os.path.join(HERE, "tripo_scales.json"), "w") as f:
    json.dump(out, f, indent=1)
print("TRIPO|" + json.dumps(out))
