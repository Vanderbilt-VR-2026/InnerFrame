"""inspect_glb.py - describe a character GLB as delivered, before any cleanup. Changes nothing.

Run headless:  Blender -b --factory-startup -P inspect_glb.py -- <file.glb> <render_dir>
Prints INSPECT| lines: what the file itself stores (its JSON header), what Blender imports,
and two measurements of which way the figure faces. Renders four orthographic views (textured,
Workbench) named by the side the camera is on, so the facing can be checked by eye too.
"""
import bpy, json, math, os, struct, sys
from mathutils import Vector

print("Blender", bpy.app.version_string)
glb, render_dir = sys.argv[sys.argv.index("--") + 1:][:2]
stem = os.path.splitext(os.path.basename(glb))[0]

# ---- what the file stores: read the GLB's JSON chunk (no Blender involved) ----
with open(glb, "rb") as f:
    magic, version, length = struct.unpack("<4sII", f.read(12))
    clen, ctype = struct.unpack("<II", f.read(8))
    gltf = json.loads(f.read(clen))
acc = gltf["accessors"]
for mi, m in enumerate(gltf.get("meshes", [])):
    for pi, p in enumerate(m["primitives"]):
        idx = acc[p["indices"]]["count"] if "indices" in p else None
        print("INSPECT|file|mesh %d '%s' prim %d|mode=%s|stored vertices=%d|triangles=%s|attributes=%s|material=%s" % (
            mi, m.get("name"), pi, p.get("mode", 4), acc[p["attributes"]["POSITION"]]["count"],
            idx // 3 if idx else None, sorted(p["attributes"]), p.get("material")))
for i, mat in enumerate(gltf.get("materials", [])):
    print("INSPECT|file|material %d '%s'|%s" % (i, mat.get("name"), json.dumps(mat)[:300]))
for i, im in enumerate(gltf.get("images", [])):
    print("INSPECT|file|image %d|name=%s|mimeType=%s" % (i, im.get("name"), im.get("mimeType")))
for i, n in enumerate(gltf.get("nodes", [])):
    print("INSPECT|file|node %d|%s" % (i, {k: v for k, v in n.items() if k != "extras"}))
print("INSPECT|file|generator=%s|extensionsUsed=%s" % (gltf.get("asset", {}).get("generator"), gltf.get("extensionsUsed")))

# ---- what Blender imports ------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
for o in bpy.context.scene.objects:
    print("INSPECT|blender|object %s|type=%s|parent=%s|loc=%s|rot_deg=%s|scale=%s" % (
        o.name, o.type, o.parent and o.parent.name, tuple(round(v, 4) for v in o.location),
        tuple(round(math.degrees(r), 2) for r in o.rotation_euler), tuple(round(s, 4) for s in o.scale)))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in meshes:
    me = o.data
    sizes = {}
    for p in me.polygons:
        sizes[len(p.vertices)] = sizes.get(len(p.vertices), 0) + 1
    ev = o.evaluated_get(dg).to_mesh(); ev.calc_loop_triangles(); tris = len(ev.loop_triangles); o.evaluated_get(dg).to_mesh_clear()
    pts = [o.matrix_world @ v.co for v in me.vertices]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    print("INSPECT|blender|mesh %s|vertices=%d|faces=%d|face sizes=%s|triangles=%d|uv=%s|materials=%s" % (
        o.name, len(me.vertices), len(me.polygons), dict(sorted(sizes.items())), tris,
        [u.name for u in me.uv_layers], [m.name for m in me.materials]))
    print("INSPECT|blender|bbox min=(%.4f, %.4f, %.4f) max=(%.4f, %.4f, %.4f) size=(%.4f, %.4f, %.4f)" % (*lo, *hi, *(hi - lo)))
for m in bpy.data.materials:
    links = [(l.from_node.name, l.from_node.image.name if getattr(l.from_node, "image", None) else "", l.to_socket.name)
             for l in m.node_tree.links] if m.node_tree else []
    print("INSPECT|blender|material %s|links=%s" % (m.name, links))
for im in bpy.data.images:
    print("INSPECT|blender|image %s|size=%dx%d|colorspace=%s|packed=%s" % (
        im.name, *im.size, im.colorspace_settings.name, bool(im.packed_file)))

# ---- facing: two independent measurements in world space ----------------------
pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
zmin = min(p.z for p in pts); H = max(p.z for p in pts) - zmin


def band(a, b):  # vertices between a and b (fractions of height)
    return [p for p in pts if zmin + a * H <= p.z <= zmin + b * H]


def centroid(ps):
    return sum(ps, Vector()) / len(ps)


# (a) toes point forward: the lowest 3 % (feet) sits ahead of the ankles (8-12 %).
feet, ankles = band(0.0, 0.03), band(0.08, 0.12)
d = (centroid(feet) - centroid(ankles)).to_2d()
print("INSPECT|facing|feet-ahead-of-ankles vector=(%.4f, %.4f)|yaw=%.1f deg (0 = facing -Y)|feet n=%d ankles n=%d" % (
    d.x, d.y, math.degrees(math.atan2(d.x, -d.y)), len(feet), len(ankles)))
# (b) shoulder line: the widest horizontal direction of the chest band (72-80 %) runs left-right;
#     the figure faces perpendicular to it (sign is not given by this one).
chest = band(0.72, 0.80)
c = centroid(chest)
sxx = sum((p.x - c.x) ** 2 for p in chest); syy = sum((p.y - c.y) ** 2 for p in chest); sxy = sum((p.x - c.x) * (p.y - c.y) for p in chest)
axis = 0.5 * math.atan2(2 * sxy, sxx - syy)  # principal axis angle from +X
print("INSPECT|facing|shoulder line angle from X axis=%.1f deg|chest width x=%.3f depth y=%.3f (n=%d)" % (
    math.degrees(axis), max(p.x for p in chest) - min(p.x for p in chest), max(p.y for p in chest) - min(p.y for p in chest), len(chest)))
# (c) knees: the line from the left knee centre to the right knee centre runs left-right (no arms at this height).
knees = band(0.25, 0.30); kc = centroid(knees)
kl, kr = centroid([p for p in knees if p.x < kc.x]), centroid([p for p in knees if p.x >= kc.x])
print("INSPECT|facing|knee line angle from X axis=%.1f deg (0 = shoulders/hips square to -Y)" % math.degrees(math.atan2(kr.y - kl.y, kr.x - kl.x)))
# (d) thighs: principal axis of the upper-leg band (both legs), again left-right.
th = band(0.40, 0.46); tc = centroid(th)
txx = sum((p.x - tc.x) ** 2 for p in th); tyy = sum((p.y - tc.y) ** 2 for p in th); txy = sum((p.x - tc.x) * (p.y - tc.y) for p in th)
print("INSPECT|facing|thigh band principal axis from X axis=%.1f deg|width x=%.3f depth y=%.3f (n=%d)" % (
    math.degrees(0.5 * math.atan2(2 * txy, txx - tyy)), max(p.x for p in th) - min(p.x for p in th), max(p.y for p in th) - min(p.y for p in th), len(th)))

# ---- four reference renders, named by where the camera is ------------------------
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.view_settings.view_transform = 'Standard'
scene.world = bpy.data.worlds.new("Inspect"); scene.world.color = (0.62, 0.64, 0.68)
scene.render.resolution_x, scene.render.resolution_y = 600, 800
lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
cam_data = bpy.data.cameras.new("Cam"); cam_data.type = 'ORTHO'; cam_data.clip_end = 1000
cam_data.ortho_scale = 1.15 * max(hi.z - lo.z, max(hi.x - lo.x, hi.y - lo.y) * 800 / 600)
cam = bpy.data.objects.new("Cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
for name, d in (("cam_minusY", (0, -1, 0)), ("cam_plusY", (0, 1, 0)), ("cam_minusX", (-1, 0, 0)), ("cam_plusX", (1, 0, 0))):
    d = Vector(d)
    cam.location = (lo + hi) / 2 + d * 50 * max(H, 1)
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(render_dir, "inspect_%s_%s.png" % (stem, name))
    bpy.ops.render.render(write_still=True)
    print("INSPECT|render|" + scene.render.filepath)
print("DONE inspect_glb")
