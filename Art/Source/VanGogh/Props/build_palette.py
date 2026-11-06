"""build_palette.py - VG_Palette: flat kidney-shaped palette with a thumb hole and raised paint blobs.

Run headless:  Blender -b --factory-startup -P build_palette.py
Writes ../blend/VG_Palette.blend and ../fbx/VG_Palette.fbx (relative to this file).
The outline is a 2D polygon (ellipse with a finger notch) extruded to 6 mm; the hole is cut by
tessellating the outline and the hole together, so no boolean modifier is needed.
"""
import bpy, bmesh, math, os
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

print("Blender", bpy.app.version_string)  # first log line: which Blender built this

# ---- Parameters (metres, real size; Z is up, palette lies flat) ------------
LENGTH, WIDTH = 0.40, 0.30     # prompt: about 0.40 x 0.30 m
THICK = 0.006                  # prompt [I]: 6 mm
OUTLINE_SEGS = 28              # [I] enough for a smooth-looking rim at hand distance
NOTCH_ANGLE = math.radians(215)  # [I] finger notch on the lower-left, next to the thumb hole
NOTCH_DEPTH, NOTCH_HALF = 0.055, 0.45  # [I] notch depth (m) and half-width (radians)
HOLE_CENTRE = (-0.135, 0.005)  # [I] thumb hole near the notch end, ~6 cm from the edges
HOLE_R, HOLE_SEGS = 0.0165, 10  # [I] ~33 mm hole fits an adult thumb
BLOB_R, BLOB_H, BLOB_SEGS = 0.018, 0.006, 6  # [I] low paint bumps: 36 mm across, 6 mm high
BLOBS = [("M_Paint_White", 100), ("M_Paint_Yellow", 55), ("M_Paint_Red", 15),
         ("M_Paint_Blue", -25), ("M_Paint_Green", -60)]  # angle (deg) around the far rim [I]
BLOB_RING = 0.72               # [I] blobs sit at 72% of the way to the rim

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.dirname(HERE)
MATS = {"M_Wood": "8A5A2B", "M_Paint_Yellow": "E8B73F", "M_Paint_Blue": "2F4FA3",
        "M_Paint_Red": "C93E32", "M_Paint_Green": "2E7D4F", "M_Paint_White": "F2EFE6"}  # prompt


def srgb_hex_to_linear(h):
    """Hex colours are sRGB; Blender materials store linear values."""
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]


def make_material(name):
    """Flat colour on Principled BSDF (what the FBX exporter reads) and viewport colour (Workbench)."""
    m = bpy.data.materials.new(name)
    rgba = srgb_hex_to_linear(MATS[name])
    m.diffuse_color = rgba
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba
    return m


def smooth_by_angle(bm, deg=40):
    """Smooth shading on curved parts, hard edges where faces meet at > deg (rim vs. top face)."""
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(deg))


def tri_count(obj):
    """Triangles after modifiers, counted the way the prompt asks (loop_triangles)."""
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh(); me.calc_loop_triangles(); n = len(me.loop_triangles); ev.to_mesh_clear()
    return n


bpy.ops.wm.read_factory_settings(use_empty=True)  # start from nothing
bpy.context.preferences.filepaths.save_version = 0  # no .blend1 backups on re-runs

# ---- Outline: ellipse pushed inwards around the notch angle ---------------
outline = []
for i in range(OUTLINE_SEGS):
    a = 2 * math.pi * i / OUTLINE_SEGS
    p = Vector((LENGTH / 2 * math.cos(a), WIDTH / 2 * math.sin(a)))
    d = (a - NOTCH_ANGLE + math.pi) % (2 * math.pi) - math.pi  # signed angle to the notch centre
    if abs(d) < NOTCH_HALF:  # cosine-shaped dent, deepest at the centre
        p *= 1 - NOTCH_DEPTH / p.length * math.cos(d / NOTCH_HALF * math.pi / 2) ** 2
    outline.append(p)
hole = [Vector(HOLE_CENTRE) + HOLE_R * Vector((math.cos(a), math.sin(a)))
        for a in (2 * math.pi * i / HOLE_SEGS for i in range(HOLE_SEGS))]
# Pivot at the thumb hole: shift everything so the hole centre is the origin.
outline = [p - Vector(HOLE_CENTRE) for p in outline]
hole = [p - Vector(HOLE_CENTRE) for p in hole]

bm = bmesh.new()
loops2d = outline + hole
top = [bm.verts.new((p.x, p.y, THICK / 2)) for p in loops2d]
bot = [bm.verts.new((p.x, p.y, -THICK / 2)) for p in loops2d]
for tri in tessellate_polygon([[p.to_3d() for p in outline], [p.to_3d() for p in hole]]):
    a, b, c = tri  # fill top and bottom with the same triangles (bottom reversed so it faces down)
    bm.faces.new((top[a], top[b], top[c])); bm.faces.new((bot[c], bot[b], bot[a]))
n_out = len(outline)
for ring, start, n in ((outline, 0, n_out), (hole, n_out, len(hole))):  # rim walls (outer and hole)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[start + i], bot[start + j], top[start + j], top[start + i]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)  # tessellation winding is not guaranteed
smooth_by_angle(bm)

# ---- Paint blobs: low six-sided domes standing on the top face -------------
mat_names = ["M_Wood"] + [b[0] for b in BLOBS]
for k, (mat, ang) in enumerate(BLOBS, start=1):
    a = math.radians(ang)
    c = Vector((BLOB_RING * LENGTH / 2 * math.cos(a), BLOB_RING * WIDTH / 2 * math.sin(a))) - Vector(HOLE_CENTRE)
    z0 = THICK / 2 - 0.0005  # sink 0.5 mm into the board so there is no visible gap
    ring = lambda r, z: [bm.verts.new((c.x + r * math.cos(t), c.y + r * math.sin(t), z))
                         for t in (2 * math.pi * i / BLOB_SEGS + 0.3 * k for i in range(BLOB_SEGS))]
    r0, r1 = ring(BLOB_R, z0), ring(BLOB_R * 0.65, z0 + BLOB_H * 0.8)
    tip = bm.verts.new((c.x, c.y, z0 + BLOB_H))
    new = [bm.faces.new((r0[i], r0[(i + 1) % BLOB_SEGS], r1[(i + 1) % BLOB_SEGS], r1[i])) for i in range(BLOB_SEGS)]
    new += [bm.faces.new((r1[i], r1[(i + 1) % BLOB_SEGS], tip)) for i in range(BLOB_SEGS)]
    for f in new:
        f.material_index = k; f.smooth = True

me = bpy.data.meshes.new("VG_Palette")
bm.to_mesh(me); bm.free()
pal = bpy.data.objects.new("VG_Palette", me)
bpy.context.collection.objects.link(pal)
for n in mat_names:
    me.materials.append(make_material(n))

bpy.context.view_layer.objects.active = pal  # smart project runs in edit mode on the active object
pal.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
bpy.ops.object.mode_set(mode='OBJECT')

# GrabPoint: the thumb goes through the hole, so the hand holds the palette at its pivot.
grab = bpy.data.objects.new("GrabPoint", None)
grab.empty_display_size = 0.03
bpy.context.collection.objects.link(grab)
grab.parent = pal

# ---- Report, save, export ---------------------------------------------------
bpy.context.view_layer.update()
pts = [pal.matrix_world @ Vector(c) for c in pal.bound_box]
print("STAT|VG_Palette|tris=%d|mats=%s|dims=%s|grab=%s" % (tri_count(pal), mat_names,
      ["%.3f" % (max(p[i] for p in pts) - min(p[i] for p in pts)) for i in range(3)], tuple(grab.matrix_world.translation)))
assert tri_count(pal) <= 300, "over the 300-triangle budget"

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "blend", "VG_Palette.blend"))
bpy.ops.object.select_all(action='DESELECT')
pal.select_set(True); grab.select_set(True)
bpy.ops.export_scene.fbx(  # ground truth #7 settings
    filepath=os.path.join(OUT, "fbx", "VG_Palette.fbx"), use_selection=True, object_types={'MESH', 'EMPTY'},
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    mesh_smooth_type='FACE', add_leaf_bones=False)
print("DONE build_palette")
