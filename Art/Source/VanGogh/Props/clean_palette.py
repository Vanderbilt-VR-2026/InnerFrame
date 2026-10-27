"""clean_palette.py - the Tripo palette (one mesh, textured) cleaned for the VG_Vincent prefab, with its GrabPoint.

Run headless:  Blender -b --factory-startup -P clean_palette.py -- [<palette.glb>] [--out-fbx DIR] [--out-blend DIR]
Defaults: the GLB Cici exported on 2026-10-27 (copied to cc-reports), the FBX and texture into Assets/VanGogh/Art/Props,
the .blend next to this script. Writes VG_Palette.fbx (VG_Palette with the child empty GrabPoint),
VG_Palette_BaseColor.jpg and VG_Palette.blend. No decimation: Tripo's triangles are kept.

What it does, in order: import, name, fit the board's plane and lay it flat with the paint up (+Z), find the thumb
hole and the outline by ray casts on a grid, turn the long axis along X with the hole at -X (Unity +X after the
export's mirror, as the old palette: the hole at his hand, the board out to his left), scale the long axis to
LENGTH, put the origin at the thumb hole at mid-thickness, add GrabPoint there, downsize the texture, save, export,
re-import and check.
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

print("Blender", bpy.app.version_string)  # first log line: which Blender ran this

# ---- Parameters (metres; Z is up in Blender) ----------------------------------------------------
LENGTH = 0.40                  # the old palette's long axis (10/26 build; phase 1 dump 0.4000)
CELL = 0.004                   # ray-cast grid, in the imported model's units (Tripo's box is about 1 unit wide)
TEX_MAX = 1024                 # base colour, longest side (sRGB JPEG, as Vincent's)
# the old palette's board centre, seen from its GrabPoint (the thumb hole), in Unity's prefab space: phase 1 dump
OLD_CENTRE_FROM_GRAB = Vector((-0.1350, 0.0027, 0.0050))

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))   # Props < VanGogh < Source < Art < repo
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GLB = next((a for a in args if a.endswith(".glb")), "/Users/cicisss/CS-VR/cc-reports/2026-10-27-vangogh-props/source/palette.glb")
FBX_DIR = args[args.index("--out-fbx") + 1] if "--out-fbx" in args else os.path.join(REPO, "Assets", "VanGogh", "Art", "Props")
BLEND_DIR = args[args.index("--out-blend") + 1] if "--out-blend" in args else HERE


def log(step, what):
    print("STEP|%s|%s" % (step, what))


def tri_count(obj):
    me = obj.data; me.calc_loop_triangles(); return len(me.loop_triangles)


def box(points):
    lo = Vector([min(p[i] for p in points) for i in range(3)]); hi = Vector([max(p[i] for p in points) for i in range(3)])
    return lo, hi


def plane_fit(P):
    """Least-squares plane through the rows of P: centroid and unit normal (the smallest principal axis)."""
    c = P.mean(axis=0)
    _, _, vt = np.linalg.svd(P - c, full_matrices=False)
    return c, vt[2]


# ---- 1. import -----------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.import_scene.gltf(filepath=GLB)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, "expected one mesh in the GLB, got %d" % len(meshes)
pal = meshes[0]
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
pal.parent = None
pal.matrix_world = Matrix.Identity(4)
pal.name = pal.data.name = "VG_Palette"
me = pal.data
tris_in = tri_count(pal)
lo, hi = box([v.co for v in me.vertices])
log("1 import", "%s: %d tris, %d verts, box %s..%s (model units)" % (os.path.basename(GLB), tris_in, len(me.vertices),
    tuple(round(v, 4) for v in lo), tuple(round(v, 4) for v in hi)))

# ---- 2. the board's plane: fit all vertices, then refit on the board alone (the paint blobs stand off it) ---------
P = np.array([v.co[:] for v in me.vertices])
c, n = plane_fit(P)
d = (P - c) @ n
inl = np.abs(d) < 2 * np.median(np.abs(d))      # the two faces of the board, not the blobs
c, n = plane_fit(P[inl]); d = (P - c) @ n
if d.max() < -d.min(): n = -n; d = -d           # the paint side (the blobs stick out farther) becomes +Z
tilt = math.degrees(math.acos(min(1.0, abs(n[2]))))
rot = Vector(n).rotation_difference(Vector((0, 0, 1))).to_matrix().to_4x4()
me.transform(Matrix.Translation(-Vector(c))); me.transform(rot)
P = np.array([v.co[:] for v in me.vertices])
board = P[np.abs(P[:, 2]) < 2 * np.median(np.abs(P[:, 2]))]
thick = np.percentile(board[:, 2], 97) - np.percentile(board[:, 2], 3)
log("2 flat", "board tilted %.1f deg in the GLB (normal %s); laid flat, paint up; mid-thickness at z=0, board %.4f thick (model units), %d of %d vertices on it" % (
    tilt, tuple(round(float(v), 4) for v in n), thick, len(board), len(P)))

# ---- 3. the thumb hole and the outline, by ray casts straight down on a grid ---------------------------------------
bm = bmesh.new(); bm.from_mesh(me)
tree = BVHTree.FromBMesh(bm)
lo, hi = box([v.co for v in me.vertices])
nx, ny = int((hi.x - lo.x) / CELL) + 3, int((hi.y - lo.y) / CELL) + 3
hit = np.zeros((nx, ny), dtype=bool)
for i in range(nx):
    for j in range(ny):
        origin = Vector((lo.x + (i - 1) * CELL, lo.y + (j - 1) * CELL, hi.z + 1.0))
        hit[i, j] = tree.ray_cast(origin, Vector((0, 0, -1)), 3.0)[0] is not None
bm.free()
outside = np.zeros_like(hit); stack = [(0, 0)]; outside[0, 0] = True   # flood fill the empty cells from the border
while stack:
    i, j = stack.pop()
    for a, b in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
        if 0 <= a < nx and 0 <= b < ny and not hit[a, b] and not outside[a, b]:
            outside[a, b] = True; stack.append((a, b))
holes = ~hit & ~outside
labels = np.zeros_like(hit, dtype=int); nlab = 0
for i in range(nx):
    for j in range(ny):
        if holes[i, j] and labels[i, j] == 0:
            nlab += 1; stack = [(i, j)]; labels[i, j] = nlab
            while stack:
                a, b = stack.pop()
                for p, q in ((a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1)):
                    if 0 <= p < nx and 0 <= q < ny and holes[p, q] and labels[p, q] == 0:
                        labels[p, q] = nlab; stack.append((p, q))
assert nlab >= 1, "no thumb hole found"
sizes = [(labels == k).sum() for k in range(1, nlab + 1)]
k = int(np.argmax(sizes)) + 1
hole_cells = np.argwhere(labels == k)
to_xy = lambda cells: np.stack([lo.x + (cells[:, 0] - 1) * CELL, lo.y + (cells[:, 1] - 1) * CELL], axis=1)
hole_xy = to_xy(hole_cells); hole_c = hole_xy.mean(axis=0); hole_r = math.sqrt(sizes[k - 1] * CELL * CELL / math.pi)
board_xy = to_xy(np.argwhere(hit)); board_c = board_xy.mean(axis=0)
log("3 hole", "%d enclosed empty region(s) %s cells; thumb hole centre %s, radius %.4f (model units), %.4f from the board's centre %s" % (
    nlab, sizes, tuple(round(float(v), 4) for v in hole_c), hole_r, float(np.linalg.norm(hole_c - board_c)), tuple(round(float(v), 4) for v in board_c)))

# ---- 4. long axis along X, the hole at -X (Unity +X: at his hand, the board out to his left) -----------------------
# The long axis runs from the board's centre through the thumb hole, as on the old palette (this board is nearly
# round, so its principal axes would not say which way the thumb goes).
_, _, vt = np.linalg.svd(board_xy - board_c, full_matrices=False)
pca = [((board_xy - board_c) @ vt[i]).max() - ((board_xy - board_c) @ vt[i]).min() for i in (0, 1)]
axis = (board_c - hole_c) / np.linalg.norm(board_c - hole_c)   # +X after the turn: away from the hole
proj = (board_xy - board_c) @ axis; length = proj.max() - proj.min()
across = np.array([-axis[1], axis[0]])
width = ((board_xy - board_c) @ across).max() - ((board_xy - board_c) @ across).min()
ang = math.atan2(axis[1], axis[0])
me.transform(Matrix.Rotation(-ang, 4, 'Z'))
log("4 axes", "through the hole: %.4f long, %.4f across (principal extents %.4f and %.4f, model units); turned %.1f deg about Z" % (
    length, width, pca[0], pca[1], -math.degrees(ang)))

# ---- 5. scale, origin at the thumb hole at mid-thickness, GrabPoint there --------------------------------------
scale = LENGTH / length
me.transform(Matrix.Scale(scale, 4))
rz = Matrix.Rotation(-ang, 4, 'Z')
hole_now = (rz @ Vector((hole_c[0], hole_c[1], 0))) * scale
board_now = (rz @ Vector((board_c[0], board_c[1], 0))) * scale
me.transform(Matrix.Translation(-hole_now))
board_now -= hole_now
lo, hi = box([v.co for v in me.vertices])
grab = bpy.data.objects.new("GrabPoint", None)
grab.empty_display_size = 0.03
bpy.context.collection.objects.link(grab)
grab.parent = pal
centre_u = Vector((-board_now.x, board_now.z, -board_now.y))   # Blender -> Unity: x mirrored, z up, -y forward
log("5 scale", "x%.4f: %.4f long, %.4f across, %.4f thick; hole radius %.4f; board centre from the hole: Blender %s, Unity %s; old %s, residual %.1f mm" % (
    scale, hi.x - lo.x, hi.y - lo.y, thick * scale, hole_r * scale, tuple(round(v, 4) for v in board_now), tuple(round(v, 4) for v in centre_u),
    tuple(OLD_CENTRE_FROM_GRAB), (centre_u - OLD_CENTRE_FROM_GRAB).length * 1000))

# ---- 6. material and texture -------------------------------------------------------------------------------
mat = me.materials[0]; mat.name = "M_Palette"
img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)
w, h = img.size
os.makedirs(FBX_DIR, exist_ok=True)
tex_path = os.path.join(FBX_DIR, "VG_Palette_BaseColor.jpg")
if max(w, h) > TEX_MAX:
    f = TEX_MAX / max(w, h); img.scale(round(w * f), round(h * f))
img.filepath_raw = tex_path; img.file_format = 'JPEG'; img.save()
img.name = os.path.basename(tex_path)
if img.packed_file:
    img.unpack(method='REMOVE'); img.filepath = tex_path; img.reload()
log("6 texture", "%dx%d -> %dx%d, %s (%d bytes), colour space %s" % (w, h, *img.size, tex_path, os.path.getsize(tex_path), img.colorspace_settings.name))

# ---- 7. report, save, export ----------------------------------------------------------------------------
bpy.context.view_layer.update()
print("STAT|VG_Palette|tris=%d|mats=%s|bbox_min=%s|bbox_max=%s|grab=%s" % (tri_count(pal), [m.name for m in me.materials],
      ["%.4f" % v for v in lo], ["%.4f" % v for v in hi], tuple(grab.matrix_world.translation)))
assert tri_count(pal) == tris_in, "triangles changed: %d -> %d" % (tris_in, tri_count(pal))
os.makedirs(BLEND_DIR, exist_ok=True)
blend_path = os.path.join(BLEND_DIR, "VG_Palette.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
bpy.ops.object.select_all(action='DESELECT')
pal.select_set(True); grab.select_set(True)
fbx_path = os.path.join(FBX_DIR, "VG_Palette.fbx")
bpy.ops.export_scene.fbx(  # ground truth #7 settings, as the other VanGogh props and Vincent
    filepath=fbx_path, use_selection=True, object_types={'MESH', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
    path_mode='RELATIVE', embed_textures=False)
log("7 export", "%s (%d bytes), %s" % (fbx_path, os.path.getsize(fbx_path), blend_path))

# ---- 8. check: re-import the FBX into an empty scene ---------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_path)
bpy.context.view_layer.update()
objs = {o.name: o for o in bpy.context.scene.objects}
ok = set(objs) == {"VG_Palette", "GrabPoint"} and objs["GrabPoint"].parent == objs["VG_Palette"]
o = objs.get("VG_Palette")
if o:
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    clo, chi = box(pts)
    g = objs["GrabPoint"].matrix_world.translation
    ok &= tri_count(o) == tris_in and (clo - lo).length < 0.001 and (chi - hi).length < 0.001 and g.length < 0.001
    print("CHECK|VG_Palette|tris=%d|mats=%s|box %s..%s|size %s|GrabPoint at %s" % (tri_count(o), [m.name for m in o.data.materials],
          ["%.4f" % v for v in clo], ["%.4f" % v for v in chi], ["%.4f" % (chi[i] - clo[i]) for i in range(3)], tuple(round(v, 4) for v in g)))
print("CHECK|ALL|%s" % ("PASS" if ok else "FAIL"))
print("DONE clean_palette")
