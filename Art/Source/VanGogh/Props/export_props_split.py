"""export_props_split.py - one FBX per hand-held prop, each with a child Empty named exactly "GrabPoint".

Run headless:  Blender -b --factory-startup -P export_props_split.py
Reads ../blend/VG_Brushes.blend and ../blend/VG_Tubes.blend (never saves them), writes
../fbx/VG_Brush_01.fbx ... ../fbx/VG_Tube_Red.fbx. The combined VG_Brushes.fbx / VG_Tubes.fbx stay as they are.
Blender allows one "GrabPoint" per file, so the other empties get temporary names while each prop is
exported. Each prop is moved to the origin for its own file (its pivot is its centre), then every new
FBX is re-imported into an empty scene and checked.
"""
import bpy, os
from mathutils import Vector

print("Blender", bpy.app.version_string)  # first log line: which Blender ran this

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.dirname(HERE)
SOURCES = {"VG_Brushes.blend": ["VG_Brush_01", "VG_Brush_02", "VG_Brush_03"],
           "VG_Tubes.blend": ["VG_Tube_Yellow", "VG_Tube_Blue", "VG_Tube_Red"]}
TOL = 0.001  # 1 mm


def tri_count(obj):
    """Triangles after modifiers (loop_triangles)."""
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh(); me.calc_loop_triangles(); n = len(me.loop_triangles); ev.to_mesh_clear()
    return n


def facts(ob, grab):
    """What must survive the export: triangles, materials, box and grab offset measured from the prop's origin
    along world axes (the FBX importer puts a 90 deg X turn on the object, so object-space axes would not match)."""
    bpy.context.view_layer.update()
    o = ob.matrix_world.translation
    pts = [ob.matrix_world @ Vector(c) - o for c in ob.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    return dict(tris=tri_count(ob), mats=[m.name for m in ob.data.materials], lo=lo, hi=hi,
                grab=grab.matrix_world.translation - o)


expected = {}
for blend, names in SOURCES.items():
    bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT, "blend", blend))
    grabs = {o.parent.name: o for o in bpy.data.objects if o.type == 'EMPTY' and o.parent}
    for owner, g in grabs.items():
        g.name = "tmp_grab_" + owner  # frees the name "GrabPoint" for whichever prop is exported next
    for name in names:
        ob, g = bpy.data.objects[name], grabs[name]
        g.name = "GrabPoint"
        assert g.name == "GrabPoint", g.name
        expected[name] = facts(ob, g)
        saved = ob.matrix_world.copy()
        ob.location = (0, 0, 0)  # this file holds one prop: put its pivot (centre) at the file's origin
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True); g.select_set(True)
        path = os.path.join(OUT, "fbx", name + ".fbx")
        bpy.ops.export_scene.fbx(  # same settings as the combined files (ground truth #7)
            filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
            apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
            mesh_smooth_type='FACE', add_leaf_bones=False)
        ob.matrix_world = saved; g.name = "tmp_grab_" + name  # back as it was; the .blend is never saved
        print("EXPORT|%s|%d bytes" % (path, os.path.getsize(path)))

# ---- check every new file on its own ---------------------------------------------
all_ok = True
for name, exp in expected.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, "fbx", name + ".fbx"))
    bpy.context.view_layer.update()
    objs = {o.name: o for o in bpy.context.scene.objects}
    ob, g = objs.get(name), objs.get("GrabPoint")
    ok = ob is not None and g is not None and len(objs) == 2 and g.parent == ob
    if ok:
        got = facts(ob, g)
        pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        centre = sum(pts, Vector()) / 8
        ok = (got["tris"] == exp["tris"] and got["mats"] == exp["mats"] and (got["lo"] - exp["lo"]).length < TOL
              and (got["hi"] - exp["hi"]).length < TOL and (got["grab"] - exp["grab"]).length < TOL and centre.length < TOL)
        dims = exp["hi"] - exp["lo"]
        print("CHECK|%s|%s|objects=%s|GrabPoint parent=%s|tris=%d|mats=%s|size=(%.4f, %.4f, %.4f)|box centre=(%.4f, %.4f, %.4f)|grab offset=(%.4f, %.4f, %.4f)" % (
            name, "PASS" if ok else "FAIL", sorted(objs), g.parent.name, got["tris"], got["mats"], *dims, *centre, *got["grab"]))
    else:
        print("CHECK|%s|FAIL|objects=%s" % (name, sorted(objs)))
    all_ok &= ok
print("CHECK|ALL|%s" % ("PASS" if all_ok else "FAIL"))
print("DONE export_props_split")
