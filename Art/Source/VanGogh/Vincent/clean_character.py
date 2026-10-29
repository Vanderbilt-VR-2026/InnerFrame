"""clean_character.py - turn a generated character (Tripo FBX or GLB) into one Quest-ready mesh, VG_Vincent.

Run headless:
  Blender -b --factory-startup -P clean_character.py -- <input.fbx|.glb> <output_name>
          [--height 1.70] [--tris 20000] [--tex-max 2048] [--turn DEG] [--keep-mesh]
          [--out-dir DIR] [--render-dir DIR]
Writes DIR/fbx/<output_name>.fbx (+ downsized textures beside it), DIR/blend/<output_name>.blend and
renders <output_name>_{front,side,back}_{textured,wire}.png. DIR defaults to the folder above this script.
Each step prints a STEP| line saying what it changed, so a run can be checked from its log.
Not done here on purpose: splitting the head, shape keys, rigging (they wait for the real mesh).
v2 (Vincent run): --turn, --keep-mesh, --render-dir, wireframe renders, PASS/FAIL checks on the re-import.
"""
import argparse, bpy, math, os, sys
from mathutils import Matrix, Vector

print("Blender", bpy.app.version_string)  # first log line: which Blender ran this

ap = argparse.ArgumentParser(prog="clean_character.py")
ap.add_argument("input")
ap.add_argument("output_name")
ap.add_argument("--height", type=float, default=1.70)    # [I] Vincent ~1.70 m; change when a source is agreed
ap.add_argument("--tris", type=int, default=20000)       # prompt: triangle budget for the character
ap.add_argument("--tex-max", type=int, default=2048)     # prompt: longest texture side
ap.add_argument("--turn", type=float, default=0.0)      # degrees about the vertical axis so the face points -Y
ap.add_argument("--keep-mesh", action="store_true")      # keep the delivered mesh: no weld, no decimate
ap.add_argument("--out-dir", default=os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ap.add_argument("--render-dir", default=None)            # default: <out-dir>/renders
a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
a.out_dir = os.path.abspath(a.out_dir)  # absolute, or the FBX stores a texture path relative to the shell, not the file
FBX_DIR, BLEND_DIR = (os.path.join(a.out_dir, d) for d in ("fbx", "blend"))
RENDER_DIR = os.path.abspath(a.render_dir or os.path.join(a.out_dir, "renders"))
NAME, MAT_NAME = "VG_Vincent", "M_Vincent"  # stable names: Unity finds them by name, Tripo's UUIDs change per export
TOL = 0.001  # 1 mm, for the checks on the re-imported file
FBX_OUT = os.path.join(FBX_DIR, a.output_name + ".fbx")


def step(n, msg):
    print("STEP|%s|%s" % (n, msg))


def tri_count(obj):
    """Triangles after modifiers (loop_triangles), as the prompt asks."""
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh(); me.calc_loop_triangles(); n = len(me.loop_triangles); ev.to_mesh_clear()
    return n


def images_of(mat):
    return [n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image] if mat and mat.node_tree else []


def import_any(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)  # glTF is Y-up; the importer turns it Z-up
    else:
        sys.exit("unsupported input: " + ext)


def only(objs, active):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active


# ---- 1. import ----------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
import_any(a.input)
objs = list(bpy.context.scene.objects)
meshes = [o for o in objs if o.type == 'MESH']
if not meshes:
    sys.exit("no mesh in " + a.input)
step("1 import", "%s: %d objects %s, %d meshes, %d tris, materials=%s, images=%s" % (
    os.path.basename(a.input), len(objs), sorted({o.type for o in objs}), len(meshes), sum(tri_count(m) for m in meshes),
    sorted({s.material.name for m in meshes for s in m.material_slots if s.material}),
    sorted("%s %dx%d" % (i.name, *i.size) for i in bpy.data.images if i.size[0])))

# ---- 2. apply transforms: unparent, drop non-mesh objects, join, bake transforms into the vertices --
for m in meshes:
    step("2 transforms", "%s before: loc=%s rot_deg=%s scale=%s parent=%s" % (
        m.name, tuple(round(v, 3) for v in m.matrix_world.translation),
        tuple(round(math.degrees(r), 1) for r in m.matrix_world.to_euler()),
        tuple(round(s, 3) for s in m.matrix_world.to_scale()), m.parent and m.parent.name))
    mw = m.matrix_world.copy(); m.parent = None; m.matrix_world = mw  # keep where it is, lose the parent
dropped = [o.name + ":" + o.type for o in objs if o.type != 'MESH']
for o in objs:
    if o.type != 'MESH':
        bpy.data.objects.remove(o)
for m in meshes:  # an armature modifier would point at a deleted rig; rigging is a later step
    for mod in [md for md in m.modifiers if md.type == 'ARMATURE']:
        m.modifiers.remove(mod)
body = max(meshes, key=tri_count)
if len(meshes) > 1:
    only(meshes, body); bpy.ops.object.join()
only([body], body)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
step("2 transforms", "removed %s; joined %d mesh(es) into %s; transforms applied -> loc=%s rot=%s scale=%s" % (
    dropped or "nothing", len(meshes), body.name, tuple(body.location), tuple(body.rotation_euler), tuple(body.scale)))
me = body.data

# ---- 2b. facing: turn about the vertical axis so the face points -Y (Unity +Z after export) ----
if a.turn:
    me.transform(Matrix.Rotation(math.radians(a.turn), 4, 'Z'))  # into the vertices: nothing left on the object
    me.update()
    step("2b facing", "turned %.1f deg about Z (counter-clockwise seen from above), applied to the vertices" % a.turn)
else:
    step("2b facing", "no turn (--turn 0): already facing -Y")

# ---- 3. real height, origin at the feet -------------------------------------
zs = [v.co.z for v in me.vertices]
h0 = max(zs) - min(zs)
s = a.height / h0
me.transform(Matrix.Scale(s, 4))
zmin = min(v.co.z for v in me.vertices)
feet = [v.co for v in me.vertices if v.co.z < zmin + 0.05 * a.height]  # lowest 5% of the body = feet [I]
fx = (min(c.x for c in feet) + max(c.x for c in feet)) / 2
fy = (min(c.y for c in feet) + max(c.y for c in feet)) / 2
me.transform(Matrix.Translation((-fx, -fy, -zmin)))  # object origin (0,0,0) is now between the feet, on the floor
me.update()
dims = [max(v.co[i] for v in me.vertices) - min(v.co[i] for v in me.vertices) for i in range(3)]
step("3 height", "height %.4f -> %.4f m (x%.4f); feet centre moved to origin; dims xyz=(%.3f, %.3f, %.3f)" % (
    h0, dims[2], s, *dims))

# ---- 4. weld seams, then decimate to the triangle budget ---------------------
# GLB files store a separate vertex on each side of every UV/normal seam. Welding vertices that sit
# within 0.1 mm (real size, after step 3) joins the surface again, so the decimate collapses across
# seams instead of tearing there. UVs are stored per face corner, so they survive the weld.
before = tri_count(body)
if a.keep_mesh:  # the delivered mesh is already in budget: change no vertices or faces
    step("4 weld+decimate", "skipped (--keep-mesh): %d tris, %d vertices kept as delivered (budget %d)" % (
        before, len(me.vertices), a.tris))
elif before > a.tris:
    v0 = len(me.vertices)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.0001)
    bpy.ops.object.mode_set(mode='OBJECT')
    step("4 weld", "%d -> %d vertices (merged within 0.1 mm)" % (v0, len(me.vertices)))
    dec = body.modifiers.new("Decimate", 'DECIMATE')
    dec.decimate_type = 'COLLAPSE'; dec.ratio = a.tris / before; dec.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=dec.name)
    loose = len(me.vertices) - len({i for p in me.polygons for i in p.vertices})
    if loose:  # vertices no face uses any more; they only add file size
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.delete_loose(use_verts=True, use_edges=True, use_faces=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    step("4 decimate", "%d -> %d tris (collapse ratio %.4f, target %d); removed %d loose vertices; %d verts left" % (
        before, tri_count(body), a.tris / before, a.tris, loose, len(me.vertices)))
else:
    step("4 decimate", "skipped: %d tris already <= target %d" % (before, a.tris))

# ---- 5. one material ------------------------------------------------------------
mats = [sl.material for sl in body.material_slots]
if len(mats) > 1:
    # Keep the textured material (Tripo puts the look in one texture); others' faces take its look.
    keep = next((m for m in mats if images_of(m)), mats[0])
    counts = {m.name if m else "None": 0 for m in mats}
    for p in me.polygons:
        counts[mats[p.material_index].name if mats[p.material_index] else "None"] += 1
    me.polygons.foreach_set("material_index", [0] * len(me.polygons))
    me.materials.clear(); me.materials.append(keep)
    step("5 materials", "%d -> 1: kept %s; faces per old material %s; dropped materials' colours are LOST "
         "(a bake would be needed if the real mesh has several textured materials)" % (len(mats), keep.name, counts))
else:
    step("5 materials", "skipped: already %d material(s) %s" % (len(mats), [m.name for m in mats if m]))

# ---- 6. downsize textures ------------------------------------------------------
mat = me.materials[0]
for img in images_of(mat):
    w, h = img.size
    if max(w, h) > a.tex_max:
        f = a.tex_max / max(w, h)
        img.scale(round(w * f), round(h * f))
        # Name the file after the socket it feeds (e.g. BaseColor), not Tripo's per-export name;
        # keep JPEG as JPEG so a photo-like texture doesn't grow several times as a PNG.
        socket = next((l.to_socket.name for l in mat.node_tree.links if getattr(l.from_node, "image", None) == img), img.name)
        fmt, ext = ('JPEG', 'jpg') if img.file_format == 'JPEG' else ('PNG', 'png')
        path = os.path.join(FBX_DIR, "%s_%s.%s" % (a.output_name, bpy.path.clean_name(socket.replace(" ", "")), ext))
        img.filepath_raw = path; img.file_format = fmt; img.save()  # save next to the FBX, Unity finds it there
        img.name = os.path.basename(path)  # the FBX texture name follows; Tripo's name changes per export
        if img.packed_file:
            img.unpack(method='REMOVE')  # GLB textures arrive packed; point at the new file instead
            img.filepath = path; img.reload()
        step("6 textures", "%s %dx%d -> %dx%d, saved %s (%d bytes)" % (img.name, w, h, *img.size, path, os.path.getsize(path)))
    else:
        step("6 textures", "%s %dx%d already <= %d, unchanged" % (img.name, w, h, a.tex_max))

# ---- 7. stable name ---------------------------------------------------------------
old, old_mat = body.name, mat.name
body.name = NAME; me.name = NAME; mat.name = MAT_NAME
step("7 rename", "%s -> %s (object and mesh data); material %s -> %s" % (old, body.name, old_mat, mat.name))
bpy.context.view_layer.update()
pre_lo = Vector([min((body.matrix_world @ v.co)[i] for v in me.vertices) for i in range(3)])  # for the upright check
pre_hi = Vector([max((body.matrix_world @ v.co)[i] for v in me.vertices) for i in range(3)])

# ---- 8. save .blend, export FBX (ground truth #7 settings) ----------------------
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, a.output_name + ".blend"))
only([body], body)
bpy.ops.export_scene.fbx(
    filepath=FBX_OUT, use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
    path_mode='RELATIVE', embed_textures=False)  # texture already sits beside the FBX
step("8 export", "%s (%d bytes)" % (FBX_OUT, os.path.getsize(FBX_OUT)))

# ---- 9. check the exported file and render front / side / back --------------------
bpy.ops.wm.read_factory_settings(use_empty=True)  # judge the FBX itself, not the scene that made it
bpy.ops.import_scene.fbx(filepath=FBX_OUT)
got = [o for o in bpy.context.scene.objects if o.type == 'MESH']
v = got[0]
bpy.context.view_layer.update()
wv = [v.matrix_world @ p.co for p in v.data.vertices]
lo = Vector([min(p[i] for p in wv) for i in range(3)]); hi = Vector([max(p[i] for p in wv) for i in range(3)])
feet = [p for p in wv if p.z < lo.z + 0.05 * a.height]
feet_c = Vector(((min(p.x for p in feet) + max(p.x for p in feet)) / 2, (min(p.y for p in feet) + max(p.y for p in feet)) / 2))
imgs = [i for i in bpy.data.images if i.size[0]]
checks = [  # each line: what Unity will get, judged from the file alone
    ("one mesh named %s" % NAME, len(got) == 1 and v.name == NAME, "%d mesh(es): %s" % (len(got), [o.name for o in got])),
    ("upright, same box as before export", (lo - pre_lo).length < TOL and (hi - pre_hi).length < TOL,
     "box shift %.3f / %.3f mm" % ((lo - pre_lo).length * 1000, (hi - pre_hi).length * 1000)),
    ("%.2f m tall" % a.height, abs(hi.z - lo.z - a.height) < TOL, "height %.4f m" % (hi.z - lo.z)),
    ("origin at the feet", v.matrix_world.translation.length < TOL and abs(lo.z) < TOL and feet_c.length < 0.005,
     "origin %s, lowest point z=%.4f, feet centre xy=(%.4f, %.4f)" % (tuple(round(c, 4) for c in v.matrix_world.translation), lo.z, *feet_c)),
    ("one material", len(v.data.materials) == 1, str([m.name for m in v.data.materials])),
    ("one %d texture" % a.tex_max, len(imgs) == 1 and max(imgs[0].size) == a.tex_max,
     str(["%s %dx%d" % (i.name, *i.size) for i in imgs])),
]
for name, ok, detail in checks:
    step("9 check", "%s|%s|%s" % (name, "PASS" if ok else "FAIL", detail))
step("9 check", "re-imported: %d tris, %d vertices, box size xyz=(%.4f, %.4f, %.4f)" % (tri_count(v), len(v.data.vertices), *(hi - lo)))

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.view_settings.view_transform = 'Standard'
scene.world = bpy.data.worlds.new("Review"); scene.world.color = (0.62, 0.64, 0.68)
scene.render.resolution_x, scene.render.resolution_y = 700, 1000   # 1000 px tall
cam_data = bpy.data.cameras.new("Cam"); cam_data.type = 'ORTHO'; cam_data.clip_end = 100
cam = bpy.data.objects.new("Cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
centre = (lo + hi) / 2
cam_data.ortho_scale = 1.1 * max(hi.z - lo.z, max(hi.x - lo.x, hi.y - lo.y) * 1000 / 700)
# Wireframe pass: Workbench renders no overlays, so a linked copy with a Wireframe modifier draws the edges
# (render-only; it is never exported). ~1.5 mm wires are about one pixel at this scale.
wire = v.copy(); scene.collection.objects.link(wire)
wm = wire.modifiers.new("Wire", 'WIREFRAME'); wm.thickness = 0.0015; wm.use_replace = True; wm.use_even_offset = True
v.color = (0.62, 0.62, 0.62, 1); wire.color = (0.05, 0.05, 0.05, 1)
for look, colour_type, show_wire in (("textured", 'TEXTURE', False), ("wire", 'OBJECT', True)):
    scene.display.shading.color_type = colour_type  # TEXTURE = the downsized texture; OBJECT = flat grey + black wires
    wire.hide_render = not show_wire
    for view, d in (("front", (0, -1, 0)), ("side", (1, 0, 0)), ("back", (0, 1, 0))):  # front of the body faces -Y
        d = Vector(d)
        cam.location = centre + d * 20
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(RENDER_DIR, "%s_%s_%s.png" % (a.output_name, view, look))
        bpy.ops.render.render(write_still=True)
        step("9 render", scene.render.filepath)
print("DONE clean_character", a.output_name)
