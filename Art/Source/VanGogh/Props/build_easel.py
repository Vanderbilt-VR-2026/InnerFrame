"""build_easel.py - VG_Easel (portable tripod field easel) with VG_Canvas on its ledge.

Run headless:  Blender -b --factory-startup -P build_easel.py
Writes ../blend/VG_Easel.blend and ../fbx/VG_Easel.fbx (relative to this file).
Every part is a box (12 triangles), so the whole easel stays far under its budget.
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

print("Blender", bpy.app.version_string)  # first log line: which Blender built this

# ---- Parameters (metres, real size; Z is up) --------------------------------
EASEL_HEIGHT = 1.70            # prompt: tripod field easel, about 1.7 m tall
TILT_DEG = 8.0                 # [I] front frame leans back so the canvas rests on it (field easels ~5-15 deg)
LEDGE_Z = 1.05                 # prompt: canvas sits on the ledge at about 1.05 m
CANVAS_W, CANVAS_H, CANVAS_D = 0.50, 0.61, 0.02  # prompt: Sept 1888 self-portrait, 61.5 x 50.3 cm
FOOT_SPREAD = 0.66             # [I] distance between the two front feet
APEX_V = 1.45                  # [I] height along the front frame where the legs meet
APEX_HALF = 0.03               # [I] front legs nearly touch at the apex
BACK_FOOT_Y = 0.60             # [I] back leg lands this far behind the front feet
LEG_W, LEG_D = 0.030, 0.022    # [I] leg section, ~3 x 2 cm hardwood
MAST_W, MAST_D = 0.040, 0.028  # [I] centre upright that carries the ledge and the clamp
MAST_BOTTOM_V = 0.90           # [I] short stub below the ledge (0.55 read as a broken 4th leg in review)
LEDGE_W, LEDGE_D, LEDGE_T = 0.58, 0.055, 0.022  # [I] a bit wider than the canvas
LIP_H, LIP_D = 0.018, 0.012    # [I] front lip stops the canvas sliding off
CLAMP_W, CLAMP_T, JAW_H = 0.09, 0.035, 0.025    # [I] top clamp block and its front jaw
WRAP = 0.02                    # [I] UV strip used by the canvas sides/back (front gets 0-1)

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.dirname(HERE)
MATS = {"M_Wood": "8A5A2B", "M_Canvas": "EDE6D3"}  # prompt: flat placeholder colours


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


def add_box(bm, size, matrix):
    """Unit cube scaled to size, then placed by matrix."""
    bmesh.ops.create_cube(bm, size=1.0, matrix=matrix @ Matrix.Diagonal((*size, 1.0)))


def add_beam(bm, p0, p1, sx, sy, y_hint):
    """Box from p0 to p1 with an sx * sy section; the sy side faces y_hint (so legs sit flat in their frame)."""
    a = (p1 - p0).normalized()
    y = (y_hint - a * y_hint.dot(a)).normalized()
    m = Matrix((y.cross(a), y, a)).transposed().to_4x4()  # columns = local X, Y, Z
    m.translation = (p0 + p1) / 2
    add_box(bm, (sx, sy, (p1 - p0).length), m)


def tri_count(obj):
    """Triangles after modifiers, counted the way the prompt asks (loop_triangles)."""
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh(); me.calc_loop_triangles(); n = len(me.loop_triangles); ev.to_mesh_clear()
    return n


bpy.ops.wm.read_factory_settings(use_empty=True)  # start from nothing: no default cube/camera/light
bpy.context.preferences.filepaths.save_version = 0  # no .blend1 backups on re-runs

# ---- Easel: built upright in a "front frame" space, then tilted back -------
# Front frame space: X across, Y = depth (front face of legs at y=0, canvas in front at y<0), Z = along the frame.
t = math.radians(TILT_DEG)
TILT = Matrix.Rotation(-t, 4, 'X')        # leans +Z toward +Y (backwards, away from the painter)
v_ledge = LEDGE_Z / math.cos(t)           # ledge top, measured along the tilted frame
v_ctop = v_ledge + CANVAS_H               # canvas top edge: where the clamp grips
v_top = EASEL_HEIGHT / math.cos(t)        # mast top
Y = Vector((0, 1, 0))

bm = bmesh.new()
for s in (-1, 1):  # two front legs, splayed in the frame plane
    add_beam(bm, Vector((s * FOOT_SPREAD / 2, LEG_D / 2, 0)), Vector((s * APEX_HALF, LEG_D / 2, APEX_V)), LEG_W, LEG_D, Y)
add_beam(bm, Vector((0, MAST_D / 2, MAST_BOTTOM_V)), Vector((0, MAST_D / 2, v_top)), MAST_W, MAST_D, Y)  # mast
add_box(bm, (LEDGE_W, LEDGE_D, LEDGE_T), Matrix.Translation((0, -LEDGE_D / 2, v_ledge - LEDGE_T / 2)))  # ledge
add_box(bm, (LEDGE_W, LIP_D, LIP_H), Matrix.Translation((0, -LEDGE_D + LIP_D / 2, v_ledge + LIP_H / 2)))  # lip
jaw_y0 = -CANVAS_D - 0.012  # clamp reaches over the canvas top edge
add_box(bm, (CLAMP_W, MAST_D - jaw_y0, CLAMP_T), Matrix.Translation((0, (MAST_D + jaw_y0) / 2, v_ctop + CLAMP_T / 2)))
add_box(bm, (CLAMP_W, 0.012, JAW_H), Matrix.Translation((0, jaw_y0 + 0.006, v_ctop - JAW_H / 2)))  # front jaw
bmesh.ops.transform(bm, matrix=TILT, verts=bm.verts)

# Back leg: from just behind the apex (after tilt) straight to the floor behind the easel.
apex_back = TILT @ Vector((0, LEG_D * 1.5, APEX_V))
add_beam(bm, apex_back, Vector((0, LEG_D / 2 + BACK_FOOT_Y, 0)), LEG_W, LEG_D, Vector((1, 0, 0)))

# Pivot at floor centre: centre of the footprint in X/Y, lowest point at z = 0.
xs, ys, zs = zip(*(v.co for v in bm.verts))
shift = Vector((-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs)))
bmesh.ops.translate(bm, vec=shift, verts=bm.verts)

me = bpy.data.meshes.new("VG_Easel")
bm.to_mesh(me); bm.free()
easel = bpy.data.objects.new("VG_Easel", me)
bpy.context.collection.objects.link(easel)
me.materials.append(make_material("M_Wood"))

bpy.context.view_layer.objects.active = easel  # smart project runs in edit mode on the active object
easel.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
bpy.ops.object.mode_set(mode='OBJECT')

# ---- Canvas: its own object, origin at the centre of its bottom face (it sits on the ledge) ----
bm = bmesh.new()
add_box(bm, (CANVAS_W, CANVAS_D, CANVAS_H), Matrix.Translation((0, 0, CANVAS_H / 2)))
uv = bm.loops.layers.uv.new("UVMap")
for f in bm.faces:  # explicit UVs: the front (-Y, painter side) fills 0-1 so a portrait image maps 1:1
    n = f.normal
    for lp in f.loops:
        x, y, z = lp.vert.co
        fu, fv, fd = (x + CANVAS_W / 2) / CANVAS_W, z / CANVAS_H, (y + CANVAS_D / 2) / CANVAS_D
        if n.y < -0.9:   u, v = fu, fv                      # front: whole image
        elif n.y > 0.9:  u, v = WRAP * (1 - fu), WRAP * fv  # back: tiny corner patch
        elif n.x < -0.9: u, v = WRAP * fd, fv               # sides/top/bottom: thin strips at the image edge
        elif n.x > 0.9:  u, v = 1 - WRAP * fd, fv
        elif n.z > 0.9:  u, v = fu, 1 - WRAP * fd
        else:            u, v = fu, WRAP * fd
        lp[uv].uv = (u, v)
me = bpy.data.meshes.new("VG_Canvas")
bm.to_mesh(me); bm.free()
canvas = bpy.data.objects.new("VG_Canvas", me)
bpy.context.collection.objects.link(canvas)
me.materials.append(make_material("M_Canvas"))
canvas.parent = easel  # moves with the easel in Unity; easel sits at the origin so local = world
canvas.location = (TILT @ Vector((0, -CANVAS_D / 2, v_ledge))) + shift
canvas.rotation_euler = (-t, 0, 0)  # same lean as the front frame

# ---- Report, save, export ---------------------------------------------------
bpy.context.view_layer.update()
for o in (easel, canvas):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = [min(p[i] for p in pts) for i in range(3)]; hi = [max(p[i] for p in pts) for i in range(3)]
    print("STAT|%s|tris=%d|mats=%s|bbox_min=%s|bbox_max=%s" % (o.name, tri_count(o), [m.name for m in o.data.materials],
          ["%.3f" % v for v in lo], ["%.3f" % v for v in hi]))
assert tri_count(easel) + tri_count(canvas) <= 1500, "over the 1,500-triangle budget"

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "blend", "VG_Easel.blend"))
bpy.ops.object.select_all(action='DESELECT')
for o in (easel, canvas):
    o.select_set(True)
bpy.ops.export_scene.fbx(  # ground truth #7 settings
    filepath=os.path.join(OUT, "fbx", "VG_Easel.fbx"), use_selection=True, object_types={'MESH', 'EMPTY'},
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    mesh_smooth_type='FACE', add_leaf_bones=False)
print("DONE build_easel")
