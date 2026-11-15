"""The Gallery's arch from Cici's Tripo model, made ready for Unity, and the doorway build_gallery.py cuts for it.

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python Art/Source/Gallery/make_arch.py

Input: Art/Source/Gallery/arch/arch.glb (Tripo, 2026-11-12, made by Cici from arch-front.png and arch-back.png, after
ChatGPT's design c750fde8-f76a-4c64-b574-f7ea9f15bb45.png; all in arch/): one mesh, one material with a 2048 base colour,
1.0 unit tall, Z up, base at z = 0, front and back alike, its depth along Y.

What it does:
  1. joins the model and applies its transforms;
  2. deletes any face inside the doorway: a face whose centre lies inside the opening (rays along the depth find it
     open) by more than 1 cm. The 2026-11-12 model has none, but Tripo can skin an opening over;
  3. scales it by one factor on every axis (so the arch keeps Tripo's shape; Cici's rule: width and height may differ
     by at most 5 %, and here they don't differ at all), so that the opening's crown is CROWN, the old arch's;
  4. puts its origin at the middle of its base on its back face (the side against the wall, toward the corridor): in
     Unity the arch stands with that point on the wall's outer face, so its depth covers the whole wall and the rest
     stands in front of it; its front is -Y here, +Z in Unity (as make_tripo_props.py);
  5. one material, GP_Arch, metallic 0 (Tripo's GLB already says 0) and roughness 1; Unity makes its own URP Lit
     material (GalleryLayoutSetup: smoothness ARCH_SMOOTHNESS there). The base colour goes to GP_Arch_BaseColor.jpg,
     2048 -> 1024;
  6. measures the opening and the arch's outline, by rays along the depth every 5 mm, and from them the doorway the
     south wall needs: a round-headed opening (straight sides, a half-circle on top) that lies between the two
     outlines, at least MARGIN inside the arch's outline and outside its opening everywhere, so the wall's cut edge is
     always behind the arch;
  7. writes Assets/Art/Gallery/Props/GP_Arch.fbx (mesh only), GP_Arch_BaseColor.jpg, and arch.json here (the scale,
     sizes, both outlines and the doorway, read by build_gallery.py).
"""
import json
import os
import sys
from math import sqrt

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
REPO = os.path.abspath(os.path.join(HERE, "../../.."))
SRC = os.path.join(HERE, "arch", "arch.glb")
OUT = os.path.join(REPO, "Assets/Art/Gallery/Props")
CROWN = 2.6          # m, the opening's crown, as the old arch's (build_gallery.py ARCH_SPRING + ARCH_R until 2026-11-12)
MAX_STRETCH = 0.05   # Cici's rule: the width's scale and the height's may differ by at most this
MARGIN = 0.03        # m, the doorway's edge at least this far inside the arch's outline and outside its opening
TEX_SIZE = 1024
STEP = 0.005         # m, the rays' spacing when measuring (after scaling)


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o != obj:
            bpy.data.objects.remove(o, do_unlink=True)
    return obj


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def bounds(obj):
    xs, ys, zs = zip(*(v.co[:] for v in obj.data.vertices))
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


class Rays:
    """Is anything of the mesh along the whole depth at (x, z)?"""
    def __init__(self, obj):
        self.bm = bmesh.new()
        self.bm.from_mesh(obj.data)
        self.tree = BVHTree.FromBMesh(self.bm)
        (_, self.y0, _), (_, self.y1, _) = bounds(obj)

    def solid(self, x, z):
        return self.tree.ray_cast(Vector((x, self.y0 - 1.0, z)), Vector((0, 1, 0)), self.y1 - self.y0 + 2.0)[0] is not None


def opening(rays, x1, z1, step):
    """The opening, row by row: for each height z, its half-width (open from x = 0 outwards), until the crown; and the
    arch's outline: for each height, its half-width (solid from outside inwards)."""
    inner, outer = [], []
    z = step / 2
    crown = None
    while z < z1:
        if crown is None:
            if rays.solid(0.0, z):
                crown = z
            else:
                x = 0.0
                while x < x1 and not rays.solid(x, z) and not rays.solid(-x, z):
                    x += step
                inner.append((round(z, 4), round(x, 4)))
        x = x1 + step
        while x > 0 and not rays.solid(x, z) and not rays.solid(-x, z):
            x -= step
        outer.append((round(z, 4), round(max(x, 0.0), 4)))
        z += step
    return inner, outer, crown


def inside_opening(rays, inner_by_z, x, z, margin):
    """Whether (x, z) lies inside the opening by more than margin (for faces that skin it over)."""
    half = next((h for zz, h in inner_by_z if abs(zz - z) <= STEP), None)
    return half is not None and abs(x) < half - margin


def doorway(inner, outer, crown):
    """A round-headed doorway (half-width a, spring s: straight sides to s, then a half circle of radius a) lying
    between the opening and the arch's outline, with the most room on both sides. Returns (a, s, clearance)."""
    zs = [z for z, _ in outer]
    out_h = [h for _, h in outer]
    in_h = [h for _, h in inner] + [0.0] * (len(outer) - len(inner))
    # each row against its neighbours too (the outline steps between rows)
    out_min = [min(out_h[max(k - 1, 0):k + 2]) for k in range(len(zs))]
    in_max = [max(in_h[max(k - 1, 0):k + 2]) for k in range(len(zs))]
    best = None
    a = max(in_h) + MARGIN
    while a < max(out_h):
        for s10 in range(0, int(crown * 1000), 10):
            s = s10 / 1000
            worst = s + a - crown   # the doorway's top above the opening's crown
            for k, z in enumerate(zs):
                if z > s + a:
                    break
                half = a if z <= s else sqrt(max(a * a - (z - s) ** 2, 0.0))
                worst = min(worst, out_min[k] - half, half - in_max[k])
                if worst < MARGIN:
                    break
            if worst >= MARGIN and (best is None or worst > best[2]):
                best = (round(a, 4), round(s, 4), round(worst, 4))
        a += 0.005
    if best is None:
        raise RuntimeError("no round-headed doorway fits between the arch's opening and its outline")
    return best


obj = load(SRC)
raw_tris = tris(obj)
(x0, y0, z0), (x1, y1, z1) = bounds(obj)
raw_size = [round(x1 - x0, 4), round(y1 - y0, 4), round(z1 - z0, 4)]

# 2. faces inside the doorway (measured on the model as it came)
rays = Rays(obj)
inner_raw, _, crown_raw = opening(rays, x1, z1, 0.0025)
skin = []
for p in obj.data.polygons:
    c = p.center
    if c.z < crown_raw - 0.01 and inside_opening(rays, inner_raw, c.x, c.z, 0.01):
        skin.append(p.index)
if skin:
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in skin], context="FACES")
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
rays.bm.free()

# 3. one scale, so the opening's crown is CROWN
s = CROWN / crown_raw
sx = sy = sz = s
assert abs(sx / sz - 1) <= MAX_STRETCH, (sx, sz)
# 4. origin: base centre (x, z), back face (+Y: front is -Y)
(x0, y0, z0), (x1, y1, z1) = bounds(obj)
cx = (x0 + x1) / 2
for v in obj.data.vertices:
    v.co.x, v.co.y, v.co.z = (v.co.x - cx) * sx, (v.co.y - y1) * sy, (v.co.z - z0) * sz
obj.data.update()
obj.name = obj.data.name = "GP_Arch"

# 6. measure the scaled arch, and the doorway for the wall
rays = Rays(obj)
(x0, y0, z0), (x1, y1, z1) = bounds(obj)
inner, outer, crown = opening(rays, x1, z1, STEP)
a, spring, clearance = doorway(inner, outer, crown)
rays.bm.free()

# 5. the material and its base colour
mat = obj.material_slots[0].material
bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
img = bsdf.inputs["Base Color"].links[0].from_node.image
metallic_was = round(bsdf.inputs["Metallic"].default_value, 3)
roughness_was = round(bsdf.inputs["Roughness"].default_value, 3)
bsdf.inputs["Metallic"].default_value = 0.0
bsdf.inputs["Roughness"].default_value = 1.0
mat.name = "GP_Arch"
out_img = bpy.data.images.new("GP_Arch_BaseColor", img.size[0], img.size[1])
out_img.pixels = img.pixels[:]
if img.size[0] > TEX_SIZE:
    out_img.scale(TEX_SIZE, TEX_SIZE)
scene = bpy.context.scene
scene.render.image_settings.file_format = "JPEG"
scene.render.image_settings.quality = 90
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
os.makedirs(OUT, exist_ok=True)
out_img.save_render(os.path.join(OUT, "GP_Arch_BaseColor.jpg"), scene=scene)

# 7. the FBX, as make_tripo_props.py exports
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT, "GP_Arch.fbx"), use_selection=False, object_types={"MESH"},
    axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL", apply_unit_scale=True,
    bake_space_transform=True, path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE",
)
report = {
    "source": "arch/arch.glb",
    "triangles": tris(obj), "triangles_raw": raw_tris, "faces_deleted_in_doorway": len(skin),
    "size_raw_xyz": raw_size,
    "scale_xyz": [round(sx, 4), round(sy, 4), round(sz, 4)],
    "size_m_xyz": [round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)],
    "metallic": [metallic_was, 0.0], "roughness": [roughness_was, 1.0],
    "texture": f"{img.size[0]} -> {min(img.size[0], TEX_SIZE)}",
    "origin": "base centre, back face (the wall side); front -Y (Unity +Z)",
    "opening": {"crown": round(crown, 3), "half_width_max": round(max(h for _, h in inner), 3),
                "half_width_min": round(min(h for _, h in inner if _ < crown - 0.3), 3)},
    "doorway": {"half_width": a, "spring": spring, "crown": round(spring + a, 4), "clearance_min": clearance,
                "margin": MARGIN},
    "outline_half_width_by_height": outer[::10],
    "opening_half_width_by_height": inner[::10],
}
with open(os.path.join(HERE, "arch.json"), "w") as f:
    json.dump(report, f, indent=1)
    f.write("\n")
print(f"GP_Arch: {raw_tris} -> {report['triangles']} triangles ({len(skin)} deleted in the doorway), "
      f"{report['size_m_xyz']} m (scale {s:.4f} on every axis), opening crown {crown:.3f} m, "
      f"doorway half-width {a} m, spring {spring} m, crown {spring + a:.3f} m, clearance {clearance} m")
