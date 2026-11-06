"""clean_easel.py - the Tripo easel (one mesh, textured) cleaned for the VG_Vincent prefab, with the canvas plane on its ledge.

Run headless:  Blender -b --factory-startup -P clean_easel.py -- [<easel.glb>] [--out-fbx DIR] [--out-blend DIR]
Defaults: the GLB Cici exported on 2026-10-27 (copied to cc-reports), the FBX and texture into Assets/VanGogh/Art/Props,
the .blend next to this script. Writes VG_Easel.fbx (VG_Easel with the child plane VG_Canvas), VG_Easel_BaseColor.jpg
and VG_Easel.blend. No decimation: Tripo's triangles are kept; the canvas adds 4.

What it does, in order: import, name, origin between the feet on the floor, ledge toward -Y (Blender's front, Unity's
+Z after export), scale so the ledge top is at LEDGE_HEIGHT, add VG_Canvas resting on the ledge and leaning back LEAN_DEG
just in front of the frame and the clamp, downsize the texture, save, export, re-import and check.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

print("Blender", bpy.app.version_string)  # first log line: which Blender ran this

# ---- Parameters (metres unless said; Z is up in Blender) --------------------------------------
LEDGE_HEIGHT = 1.056           # the old VG_Canvas's bottom edge in the prefab (10/26 build; phase 1 dump 1.0561)
CANVAS_W, CANVAS_H = 0.50, 0.61  # the old canvas (Sept 1888 self-portrait, 61.5 x 50.3 cm)
LEAN_DEG = 8.0                 # the old canvas's lean, back from vertical
CLEARANCE = 0.008              # the plane stays at least this far in front of the frame and the clamp
WRAP = 0.02                    # the back face maps to this corner patch of the image (as the old canvas did)
TEX_MAX = 1024                 # base colour, longest side (sRGB JPEG, as Vincent's)
# the old canvas's centre in prefab space, from the phase 1 dump; the easel's -45 deg turn is the tool's EaselTurn
OLD_CANVAS_CENTRE = Vector((0.5064, 1.3595, 0.1936))
EASEL_TURN_DEG = -45.0
# where the ledge is looked for, in the imported model's own units (Tripo's box is 1 unit tall): a bar that sticks out
# in front (y < LEDGE_MIN_Y) between LEDGE_Z[0] and LEDGE_Z[1]; its top is its highest vertex (a flat bar has vertices
# only at its corners, so the ends count)
LEDGE_Z, LEDGE_MIN_Y = (0.40, 0.70), -0.10
CANVAS_COLOUR = "EDE6D3"       # primed canvas (make_canvas_stages.py's ground)

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))   # Props < VanGogh < Source < Art < repo
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GLB = next((a for a in args if a.endswith(".glb")), "/Users/cicisss/CS-VR/cc-reports/2026-10-27-vangogh-props/source/easel.glb")
FBX_DIR = args[args.index("--out-fbx") + 1] if "--out-fbx" in args else os.path.join(REPO, "Assets", "VanGogh", "Art", "Props")
BLEND_DIR = args[args.index("--out-blend") + 1] if "--out-blend" in args else HERE


def log(step, what):
    print("STEP|%s|%s" % (step, what))


def tri_count(obj):
    me = obj.data; me.calc_loop_triangles(); return len(me.loop_triangles)


def box(points):
    lo = Vector([min(p[i] for p in points) for i in range(3)]); hi = Vector([max(p[i] for p in points) for i in range(3)])
    return lo, hi


def srgb_hex_to_linear(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]


# ---- 1. import -----------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.import_scene.gltf(filepath=GLB)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, "expected one mesh in the GLB, got %d" % len(meshes)
easel = meshes[0]
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
easel.parent = None
easel.matrix_world = Matrix.Identity(4)
easel.name = easel.data.name = "VG_Easel"
me = easel.data
tris_in = tri_count(easel)
lo, hi = box([v.co for v in me.vertices])
log("1 import", "%s: %d tris, %d verts, box %s..%s (model units)" % (os.path.basename(GLB), tris_in, len(me.vertices),
    tuple(round(v, 4) for v in lo), tuple(round(v, 4) for v in hi)))

# ---- 2. facing: the ledge sticks out in front; the front is -Y in Blender (Unity +Z after export) --------------------
band = [v.co for v in me.vertices if LEDGE_Z[0] < v.co.z < LEDGE_Z[1]]
front, back = -min(p.y for p in band), max(p.y for p in band)
if back > front:
    me.transform(Matrix.Rotation(math.pi, 4, 'Z'))
    log("2 facing", "ledge was at +Y (sticks out %.3f behind, %.3f in front): turned 180 deg" % (back, front))
else:
    log("2 facing", "ledge at -Y already (sticks out %.3f in front, %.3f behind)" % (front, back))

# ---- 3. origin between the feet, on the floor -------------------------------------------------------------------
feet = [v.co for v in me.vertices if v.co.z < lo.z + 0.02]
flo, fhi = box(feet)
shift = Vector((-(flo.x + fhi.x) / 2, -(flo.y + fhi.y) / 2, -lo.z))
me.transform(Matrix.Translation(shift))
log("3 origin", "footprint %.3f x %.3f; moved by %s so the floor is z=0 and the feet centre on x=y=0" % (
    fhi.x - flo.x, fhi.y - flo.y, tuple(round(v, 4) for v in shift)))

# ---- 4. the ledge, then scale so its top is at LEDGE_HEIGHT -------------------------------------------------------
ledge = [v.co for v in me.vertices if LEDGE_Z[0] < v.co.z < LEDGE_Z[1] and v.co.y < LEDGE_MIN_Y]
assert ledge, "no ledge found in front between z %.2f and %.2f" % LEDGE_Z
ledge_top = max(p.z for p in ledge)
ledge_front = min(p.y for p in ledge)
ledge_w = max(p.x for p in ledge) - min(p.x for p in ledge)
# stop rule: one ledge across the front. Two would show as two separate z-clusters of front-sticking vertices.
zs = sorted(p.z for p in ledge)
gaps = [b - a for a, b in zip(zs, zs[1:]) if b - a > 0.05]
assert not gaps, "more than one ledge: vertical gaps %s between front-sticking parts" % gaps
scale = LEDGE_HEIGHT / ledge_top
me.transform(Matrix.Scale(scale, 4))
ledge_top *= scale; ledge_front *= scale; ledge_w *= scale
lo, hi = box([v.co for v in me.vertices])
log("4 scale", "ledge top %.4f (model) -> x%.4f: top at %.4f m, front edge y %.4f, %.3f m wide; easel %.3f x %.3f x %.3f m" % (
    ledge_top / scale, scale, ledge_top, ledge_front, ledge_w, hi.x - lo.x, hi.y - lo.y, hi.z - lo.z))

# ---- 5. the canvas plane: on the ledge, leaning back, just in front of the frame and the clamp --------------------
t = math.radians(LEAN_DEG)
z_top = ledge_top + CANVAS_H * math.cos(t)
profile = {}   # the frame's front face (smallest y) per 1 cm of height, under the canvas's width
for v in me.vertices:
    p = v.co
    if ledge_top + 0.01 < p.z < z_top + 0.02 and abs(p.x) < CANVAS_W / 2 + 0.02:
        k = int(p.z * 100); profile[k] = min(profile.get(k, 9.0), p.y)
tight = min((y - (k / 100 - ledge_top) * math.tan(t), k) for k, y in profile.items())
y_bottom = tight[0] - CLEARANCE
assert y_bottom > ledge_front + 0.005, "the plane would stand in front of the ledge: y %.4f, ledge front %.4f" % (y_bottom, ledge_front)
log("5 canvas", "frame front per cm from z %.2f to %.2f: tightest at z %.2f (y %.4f); plane bottom at y %.4f, %.1f mm behind the ledge's front edge, top at z %.4f" % (
    ledge_top, z_top, tight[1] / 100, profile[tight[1]], y_bottom, (y_bottom - ledge_front) * 1000, z_top))

bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
W, H = CANVAS_W, CANVAS_H
quad = [(-W / 2, 0, 0), (W / 2, 0, 0), (W / 2, 0, H), (-W / 2, 0, H)]
front_v = [bm.verts.new(c) for c in quad]
back_v = [bm.verts.new(c) for c in quad]
f = bm.faces.new(front_v)                       # normal -Y: the painter's side, the whole image
for lp in f.loops:
    x, _, z = lp.vert.co; lp[uv].uv = ((x + W / 2) / W, z / H)
b = bm.faces.new(list(reversed(back_v)))        # normal +Y: a corner patch, so the back is primed canvas, not the portrait
for lp in b.loops:
    x, _, z = lp.vert.co; lp[uv].uv = (WRAP * (1 - (x + W / 2) / W), WRAP * z / H)
bm.normal_update()   # the windings above set the normals; recalc_face_normals would turn both faces the same way
assert f.normal.y < 0 < b.normal.y, "canvas faces: front %s back %s" % (f.normal, b.normal)
cme = bpy.data.meshes.new("VG_Canvas"); bm.to_mesh(cme); bm.free()
canvas = bpy.data.objects.new("VG_Canvas", cme)
bpy.context.collection.objects.link(canvas)
cmat = bpy.data.materials.new("M_Canvas"); cmat.use_nodes = True
rgba = srgb_hex_to_linear(CANVAS_COLOUR)
cmat.diffuse_color = rgba; cmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba
cme.materials.append(cmat)
canvas.parent = easel
canvas.location = (0, y_bottom, ledge_top)      # origin: the centre of the bottom edge, where it rests on the ledge
canvas.rotation_euler = (-t, 0, 0)              # leans back, top toward +Y
bpy.context.view_layer.update()
centre_b = canvas.matrix_world @ Vector((0, 0, H / 2))
centre_u = Vector((-centre_b.x, centre_b.z, -centre_b.y))   # Blender -> Unity (FBX -Z forward, Y up): x mirrored, z up, -y forward
a = math.radians(EASEL_TURN_DEG)
turned = Vector((centre_u.x * math.cos(a) + centre_u.z * math.sin(a), centre_u.y, -centre_u.x * math.sin(a) + centre_u.z * math.cos(a)))
easel_at = Vector((OLD_CANVAS_CENTRE.x - turned.x, 0, OLD_CANVAS_CENTRE.z - turned.z))
log("5 canvas", "centre in easel space: Blender %s, Unity %s; with the easel turned %.0f deg, EaselAt = %s puts it at (%.4f, %.4f, %.4f): %.1f mm from the old centre (height differs by %.1f mm)" % (
    tuple(round(v, 4) for v in centre_b), tuple(round(v, 4) for v in centre_u), EASEL_TURN_DEG, tuple(round(v, 4) for v in easel_at),
    easel_at.x + turned.x, turned.y, easel_at.z + turned.z, (Vector((easel_at.x + turned.x, turned.y, easel_at.z + turned.z)) - OLD_CANVAS_CENTRE).length * 1000,
    (turned.y - OLD_CANVAS_CENTRE.y) * 1000))

# ---- 6. material and texture: Tripo's base colour, downsized, saved beside the FBX ----------------------------------
mat = me.materials[0]; mat.name = "M_Easel"
img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)
w, h = img.size
os.makedirs(FBX_DIR, exist_ok=True)
tex_path = os.path.join(FBX_DIR, "VG_Easel_BaseColor.jpg")
if max(w, h) > TEX_MAX:
    f = TEX_MAX / max(w, h); img.scale(round(w * f), round(h * f))
img.filepath_raw = tex_path; img.file_format = 'JPEG'; img.save()
img.name = os.path.basename(tex_path)
if img.packed_file:
    img.unpack(method='REMOVE'); img.filepath = tex_path; img.reload()
log("6 texture", "%dx%d -> %dx%d, %s (%d bytes), colour space %s" % (w, h, *img.size, tex_path, os.path.getsize(tex_path), img.colorspace_settings.name))

# ---- 7. report, save, export ----------------------------------------------------------------------------------
for o in (easel, canvas):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo, hi = box(pts)
    print("STAT|%s|tris=%d|mats=%s|bbox_min=%s|bbox_max=%s" % (o.name, tri_count(o), [m.name for m in o.data.materials],
          ["%.4f" % v for v in lo], ["%.4f" % v for v in hi]))
assert tri_count(easel) == tris_in, "triangles changed: %d -> %d" % (tris_in, tri_count(easel))
os.makedirs(BLEND_DIR, exist_ok=True)
blend_path = os.path.join(BLEND_DIR, "VG_Easel.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
bpy.ops.object.select_all(action='DESELECT')
easel.select_set(True); canvas.select_set(True)
fbx_path = os.path.join(FBX_DIR, "VG_Easel.fbx")
bpy.ops.export_scene.fbx(  # ground truth #7 settings, as the other VanGogh props and Vincent
    filepath=fbx_path, use_selection=True, object_types={'MESH', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
    path_mode='RELATIVE', embed_textures=False)
log("7 export", "%s (%d bytes), %s" % (fbx_path, os.path.getsize(fbx_path), blend_path))

# ---- 8. check: re-import the FBX into an empty scene -------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_path)
bpy.context.view_layer.update()
objs = {o.name: o for o in bpy.context.scene.objects}
ok = set(objs) == {"VG_Easel", "VG_Canvas"} and objs["VG_Canvas"].parent == objs["VG_Easel"]
for name, o in objs.items():
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo, hi = box(pts)
    print("CHECK|%s|parent=%s|tris=%d|mats=%s|box %s..%s|size %s" % (name, o.parent.name if o.parent else None, tri_count(o),
          [m.name for m in o.data.materials], ["%.4f" % v for v in lo], ["%.4f" % v for v in hi], ["%.4f" % (hi[i] - lo[i]) for i in range(3)]))
    if name == "VG_Canvas":
        c = o.matrix_world @ (sum((Vector(v) for v in o.bound_box), Vector()) / 8)
        ok &= (c - centre_b).length < 0.001 and abs(lo.z - LEDGE_HEIGHT) < 0.001
        print("CHECK|VG_Canvas|centre %s (expected %s)|bottom z %.4f (expected %.4f)" % (tuple(round(v, 4) for v in c), tuple(round(v, 4) for v in centre_b), lo.z, LEDGE_HEIGHT))
    if name == "VG_Easel": ok &= tri_count(o) == tris_in
print("CHECK|ALL|%s" % ("PASS" if ok else "FAIL"))
print("DONE clean_easel")
