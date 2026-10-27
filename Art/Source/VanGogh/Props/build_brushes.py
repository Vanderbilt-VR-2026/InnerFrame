"""build_brushes.py - VG_Brush_01..03: long-handled oil brushes (handle, metal ferrule, bristle tip).

Run headless:  Blender -b --factory-startup -P build_brushes.py
Writes ../blend/VG_Brushes.blend and ../fbx/VG_Brushes.fbx (relative to this file).
Each brush is a stack of 8-sided rings along Z (bristles up), joined by quads; the pivot is the
brush's centre. A flat brush is the same rings squashed sideways towards the tip.
"""
import bpy, bmesh, math, os
from mathutils import Vector

print("Blender", bpy.app.version_string)  # first log line: which Blender built this

# ---- Parameters (metres, real size; brush axis = Z, bristles at +Z) --------
BRUSHES = [  # name, total length [I prompt], shape [I], bristle colour = paint loaded on it [I]
    ("VG_Brush_01", 0.25, "round", "M_Paint_Yellow"),
    ("VG_Brush_02", 0.28, "flat", "M_Paint_Blue"),
    ("VG_Brush_03", 0.30, "flat", "M_Paint_Red"),
]
SIDES = 8                       # [I] 8 sides reads as round at hand distance
HANDLE_F, FERRULE_F, BRISTLE_F = 0.79, 0.12, 0.09  # [I] share of total length per part
R_BACK, R_MID, R_BELLY, R_NECK = 0.0025, 0.0035, 0.0048, 0.0040  # [I] handle radii, back to ferrule
R_FERRULE = 0.0043              # [I] ferrule sleeve, a hair wider than the handle neck
FLAT_X, FLAT_Y = 1.6, 0.45      # [I] how much a flat brush widens/thins at the ferrule tip
GRAB_F = 0.45                   # [I] hand holds the handle 45% of the way up from the back end
SPACING = 0.05                  # [I] brushes side by side in X so they don't overlap

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.dirname(HERE)
MATS = {"M_Wood": "8A5A2B", "M_Metal": "9A9A9A", "M_Paint_Yellow": "E8B73F",
        "M_Paint_Blue": "2F4FA3", "M_Paint_Red": "C93E32"}  # prompt


def srgb_hex_to_linear(h):
    """Hex colours are sRGB; Blender materials store linear values."""
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]


def get_material(name):
    """One shared material per role: flat colour on Principled BSDF (FBX) and viewport colour (Workbench)."""
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        rgba = srgb_hex_to_linear(MATS[name])
        m.diffuse_color = rgba
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba
    return m


def tube(bm, rings, mat, cap_start=False, end="open", tip_z=None):
    """Connect rings of (z, r, sx, sy) with quads. end: 'open', 'cap' (flat n-gon) or 'point' (cone tip at tip_z)."""
    vs = [[bm.verts.new((r * sx * math.cos(a), r * sy * math.sin(a), z))
           for a in (2 * math.pi * i / SIDES for i in range(SIDES))] for z, r, sx, sy in rings]
    faces = [bm.faces.new((lo[i], lo[(i + 1) % SIDES], hi[(i + 1) % SIDES], hi[i]))
             for lo, hi in zip(vs, vs[1:]) for i in range(SIDES)]
    if cap_start:
        faces.append(bm.faces.new(list(reversed(vs[0]))))
    if end == "cap":
        faces.append(bm.faces.new(vs[-1]))
    elif end == "point":
        tip = bm.verts.new((0, 0, tip_z))
        faces += [bm.faces.new((vs[-1][i], vs[-1][(i + 1) % SIDES], tip)) for i in range(SIDES)]
    for f in faces:
        f.material_index = mat; f.smooth = True
    return faces


def tri_count(obj):
    """Triangles after modifiers, counted the way the prompt asks (loop_triangles)."""
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh(); me.calc_loop_triangles(); n = len(me.loop_triangles); ev.to_mesh_clear()
    return n


bpy.ops.wm.read_factory_settings(use_empty=True)  # start from nothing
bpy.context.preferences.filepaths.save_version = 0  # no .blend1 backups on re-runs

export = []
for k, (name, L, shape, paint) in enumerate(BRUSHES):
    h, f = HANDLE_F * L, FERRULE_F * L
    sx, sy = (FLAT_X, FLAT_Y) if shape == "flat" else (1.0, 1.0)
    bm = bmesh.new()
    tube(bm, [(0, R_BACK, 1, 1), (0.35 * h, R_MID, 1, 1), (0.80 * h, R_BELLY, 1, 1), (h, R_NECK, 1, 1)],
         mat=0, cap_start=True)                                   # handle (wood), back end closed
    tube(bm, [(h - 0.005, R_FERRULE, 1, 1), (h + f, R_FERRULE * 0.95, sx, sy)], mat=1)  # ferrule (metal)
    b0 = h + f - 0.003                                            # bristles start just inside the ferrule
    bristles = [(b0, R_FERRULE * 0.9, sx, sy), (b0 + 0.55 * BRISTLE_F * L, R_FERRULE * 1.0, sx * 1.05, sy)]
    if shape == "flat":  # chisel edge: squash the last ring and close it
        tube(bm, bristles + [(h + f + BRISTLE_F * L, R_FERRULE * 0.8, sx * 1.1, sy * 0.35)], mat=2, end="cap")
    else:                # round: taper to a point
        tube(bm, bristles, mat=2, end="point", tip_z=L)  # tip lands exactly at the brush length
    # Hard edges only where the back cap or the flat tip meets the sides; everything else stays smooth.
    for e in bm.edges:
        e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(50))
    bmesh.ops.translate(bm, vec=Vector((0, 0, -L / 2)), verts=bm.verts)  # pivot at the brush centre

    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in ("M_Wood", "M_Metal", paint):
        me.materials.append(get_material(m))
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.location = ((k - 1) * SPACING, 0, L / 2)  # stand them on the floor, side by side

    bpy.context.view_layer.objects.active = ob  # smart project runs in edit mode on the active object
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    ob.select_set(False)

    # Blender keeps names unique per file, so the 2nd and 3rd empties become GrabPoint.001/.002.
    grab = bpy.data.objects.new("GrabPoint", None)
    grab.empty_display_size = 0.02
    bpy.context.collection.objects.link(grab)
    grab.parent = ob
    grab.location = (0, 0, GRAB_F * h - L / 2)
    export += [ob, grab]

    bpy.context.view_layer.update()
    dims = [max(c[i] for c in ob.bound_box) - min(c[i] for c in ob.bound_box) for i in range(3)]
    print("STAT|%s|tris=%d|mats=%s|dims=%s|grab=%s(%s)" % (name, tri_count(ob), [m.name for m in me.materials],
          ["%.4f" % d for d in dims], grab.name, tuple(round(v, 4) for v in grab.location)))
    assert tri_count(ob) <= 150, name + " over the 150-triangle budget"

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "blend", "VG_Brushes.blend"))
bpy.ops.object.select_all(action='DESELECT')
for o in export:
    o.select_set(True)
bpy.ops.export_scene.fbx(  # ground truth #7 settings
    filepath=os.path.join(OUT, "fbx", "VG_Brushes.fbx"), use_selection=True, object_types={'MESH', 'EMPTY'},
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    mesh_smooth_type='FACE', add_leaf_bones=False)
print("DONE build_brushes")
