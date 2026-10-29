"""review_fbx.py - check an exported prop FBX against its .blend source and render review images.

Run headless:  Blender -b --factory-startup -P review_fbx.py -- <prop.fbx> <prop.blend>
1. Reads each VG_ object and GrabPoint from the .blend (bounding box, triangles, materials).
2. Re-imports the FBX into an empty scene and compares: same box within 1 mm = upright, right size, same place.
3. Renders with Workbench (fast) next to a 1.75 m banded reference post (0.25 m bands):
   three-quarter and side views (orthographic, so sizes compare 1:1), plus a close-up without the post.
Images go to ../renders/<fbx name>_{threequarter,side,closeup}.png.
"""
import bpy, math, os, sys
from mathutils import Vector

print("Blender", bpy.app.version_string)  # first log line: which Blender ran this

POST_H, POST_R, BAND = 1.75, 0.02, 0.25   # prompt: 1.75 m reference post; bands [I] make sizes readable
TOL = 0.001                               # [I] 1 mm: FBX stores floats, anything bigger is a real change
WIDTH = 800                               # prompt: 800 px wide
fbx_path, blend_path = sys.argv[sys.argv.index("--") + 1:][:2]
stem = os.path.splitext(os.path.basename(fbx_path))[0]
RENDERS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "renders")


def tri_count(obj):
    """Triangles after modifiers (loop_triangles), as the prompt asks."""
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh(); me.calc_loop_triangles(); n = len(me.loop_triangles); ev.to_mesh_clear()
    return n


def snapshot():
    """World-space facts for every prop object, keyed by name."""
    bpy.context.view_layer.update()
    out = {}
    for o in bpy.context.scene.objects:
        if not (o.name.startswith("VG_") or o.name.startswith("GrabPoint")):
            continue
        if o.type == 'MESH':
            pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
            out[o.name] = dict(type="MESH", lo=Vector([min(p[i] for p in pts) for i in range(3)]),
                               hi=Vector([max(p[i] for p in pts) for i in range(3)]), tris=tri_count(o),
                               mats=[m.name for m in o.data.materials],
                               rot=tuple(round(math.degrees(a), 2) for a in o.matrix_world.to_euler()),
                               scale=tuple(round(s, 4) for s in o.matrix_world.to_scale()))
        else:
            out[o.name] = dict(type=o.type, pos=o.matrix_world.translation.copy(), parent=o.parent and o.parent.name)
    return out


# ---- 1. source facts from the .blend ---------------------------------------
bpy.ops.wm.open_mainfile(filepath=blend_path)
src = snapshot()

# ---- 2. re-import the FBX into an empty scene ------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_path)  # default axes (-Z forward, Y up) undo the export conversion
imp = snapshot()

print("REVIEW|%s|fbx_bytes=%d|objects_src=%d|objects_imported=%d" % (stem, os.path.getsize(fbx_path), len(src), len(imp)))
ok_all = set(src) == set(imp)
if not ok_all:
    print("REVIEW|%s|NAME MISMATCH src_only=%s imported_only=%s" % (stem, sorted(set(src) - set(imp)), sorted(set(imp) - set(src))))
for name in sorted(src):
    s, i = src[name], imp.get(name)
    if i is None:
        continue
    if s["type"] == "MESH":
        dlo, dhi = (i["lo"] - s["lo"]).length, (i["hi"] - s["hi"]).length
        ok = dlo < TOL and dhi < TOL and s["tris"] == i["tris"] and s["mats"] == i["mats"]
        dims = i["hi"] - i["lo"]
        print("REVIEW|%s|%s|%s|tris=%d->%d|mats=%s|dims_xyz_m=(%.4f, %.4f, %.4f)|box_shift_mm=%.3f/%.3f|imported_rot_deg=%s|scale=%s"
              % (stem, name, "PASS" if ok else "FAIL", s["tris"], i["tris"], i["mats"], *dims, dlo * 1000, dhi * 1000, i["rot"], i["scale"]))
    else:
        d = (i["pos"] - s["pos"]).length
        ok = d < TOL and s["parent"] == i["parent"]
        print("REVIEW|%s|%s|%s|empty parent=%s->%s|pos_shift_mm=%.3f" % (stem, name, "PASS" if ok else "FAIL", s["parent"], i["parent"], d * 1000))
    ok_all &= ok
print("REVIEW|%s|OVERALL|%s" % (stem, "PASS" if ok_all else "FAIL"))

# Show imported material colours, so a lost colour would be visible in the log as well as in the render.
for m in bpy.data.materials:
    print("REVIEW|%s|material|%s|diffuse=%s" % (stem, m.name, tuple(round(c, 3) for c in m.diffuse_color[:3])))

# ---- 3. review renders -------------------------------------------------------
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'     # flat material colours, as Unity will start with
scene.display.shading.show_object_outline = True  # thin props stay visible against the background
scene.view_settings.view_transform = 'Standard'   # show the hex colours as they are (no filmic tone curve)
scene.world = bpy.data.worlds.new("Review")
scene.world.color = (0.62, 0.64, 0.68)

props = [o for o in scene.objects if o.type == 'MESH']
plo = Vector([min(imp[o.name]["lo"][i] for o in props) for i in range(3)])
phi = Vector([max(imp[o.name]["hi"][i] for o in props) for i in range(3)])

grey = bpy.data.materials.new("Post_Dark"); grey.diffuse_color = (0.05, 0.05, 0.05, 1)
white = bpy.data.materials.new("Post_Light"); white.diffuse_color = (0.9, 0.9, 0.9, 1)
post_x = phi.x + max(0.15, 0.4 * (phi.x - plo.x))  # post stands behind-right of the props,
post_y = phi.y + max(0.15, 0.4 * (phi.y - plo.y))  # so neither view puts it in front of them
post = []
for k in range(math.ceil(POST_H / BAND)):
    z0, z1 = k * BAND, min((k + 1) * BAND, POST_H)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=POST_R, depth=z1 - z0,
                                        location=(post_x, post_y, (z0 + z1) / 2))
    bpy.context.object.data.materials.append(grey if k % 2 == 0 else white)
    post.append(bpy.context.object)
floor_mat = bpy.data.materials.new("Floor"); floor_mat.diffuse_color = (0.42, 0.42, 0.44, 1)
bpy.ops.mesh.primitive_plane_add(size=1, location=((plo.x + post_x) / 2, (plo.y + post_y) / 2, -0.0005))
floor = bpy.context.object
floor.scale = (post_x - plo.x + 1.0, post_y - plo.y + 1.0, 1)
floor.data.materials.append(floor_mat)

cam_data = bpy.data.cameras.new("ReviewCam"); cam_data.type = 'ORTHO'
cam = bpy.data.objects.new("ReviewCam", cam_data); scene.collection.objects.link(cam); scene.camera = cam


def shoot(name, direction, objs, height_px):
    """Orthographic camera along `direction` (from target towards camera), framed to fit objs' boxes."""
    corners = []
    for o in objs:
        corners += [o.matrix_world @ Vector(c) for c in o.bound_box]
    centre = sum(corners, Vector()) / len(corners)
    d = Vector(direction).normalized()
    rot = (-d).to_track_quat('-Z', 'Y')
    right, up = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0))
    xs = [(c - centre).dot(right) for c in corners]; ys = [(c - centre).dot(up) for c in corners]
    centre += right * (max(xs) + min(xs)) / 2 + up * (max(ys) + min(ys)) / 2  # centre the framing
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    scene.render.resolution_x, scene.render.resolution_y = WIDTH, height_px
    cam_data.ortho_scale = 1.12 * max(w, h * WIDTH / height_px) if WIDTH >= height_px else 1.12 * max(h, w * height_px / WIDTH)
    cam.location = centre + d * 20
    cam.rotation_euler = rot.to_euler()
    cam_data.clip_end = 100
    scene.render.filepath = os.path.join(RENDERS, "%s_%s.png" % (stem, name))
    bpy.ops.render.render(write_still=True)
    print("RENDER|%s" % scene.render.filepath)


az, el = math.radians(40), math.radians(22)  # [I] classic three-quarter: 40 deg off the front, 22 deg above
three_q = (math.sin(az), -math.cos(az), math.tan(el))  # front of a prop faces -Y
shoot("threequarter", three_q, props + post, 1000)
shoot("side", (1, 0, 0), props + post, 1000)            # from +X: true side, no perspective
for o in post:
    o.hide_render = True
shoot("closeup", three_q, props, 800)
print("DONE review_fbx", stem)
