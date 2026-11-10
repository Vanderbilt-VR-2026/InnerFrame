"""The Gallery's decoration props from Cici's Tripo models (gallery-tripo/, outside the repo), scaled and decimated.

    Blender --background --factory-startup --python make_tripo_props.py -- <gallery-tripo dir>

Input: the five raw Tripo GLBs (gallery-statue.glb, gallery-plant.glb, gallery-vase.glb, gallery-lantern.glb,
gallery-frame-corner.glb). Each is one mesh and one material, 1.0 unit tall, Z up, base at z = 0, origin on the base
plane, front facing -Y (see the zip's README.md).

For each model: join and apply its transforms, scale it to the real size below (the frame corner is also flattened,
since Tripo made it 0.42 deep for 0.93 wide), collapse-decimate it to its triangle budget, keep the origin at the base
centre (the frame corner's at its outer corner), and write
    Assets/Art/Gallery/Props/GP_<Name>.fbx            (mesh only, no embedded textures or materials)
    Assets/Art/Gallery/Props/GP_<Name>_BaseColor.jpg  (the base colour, downsized)
and tripo_props.json here (what was measured). The materials, prefabs and placement in Gallery.unity are made in Unity by
Assets/Art/Gallery/Editor/GalleryDecorSetup.cs.

Axes: the FBX export matches build_gallery.py (forward -Z, up Y), so a prop's front (-Y here) is its +Z (forward) in Unity.
"""
import json
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
IN = sys.argv[sys.argv.index("--") + 1]
REPO = os.path.abspath(os.path.join(HERE, "../../.."))
OUT = os.path.join(REPO, "Assets/Art/Gallery/Props")

# name: (source GLB, height in m, depth (Y) in m or None for uniform, triangle budget, texture size, origin)
# Heights are team choices: a statue a little under life size for its plinth, a floor plant, a vase of flowers for a
# plinth, a hanging lantern, and a frame-corner ornament that covers the 0.18 m frame and 0.12 m of the painting
PROPS = {
    "Statue":      ("gallery-statue.glb",       1.70, None, 5000, 2048, "base"),
    "Plant":       ("gallery-plant.glb",        1.30, None, 3000, 1024, "base"),
    "Vase":        ("gallery-vase.glb",         0.60, None, 5000, 1024, "base"),  # 2,500 shredded the flowers
    "Lantern":     ("gallery-lantern.glb",      0.60, None, 2000, 1024, "top"),
    "FrameCorner": ("gallery-frame-corner.glb", 0.32, 0.04, 1200, 1024, "corner"),
}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def bounds(obj):
    xs, ys, zs = zip(*(v.co[:] for v in obj.data.vertices))
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


def load(path):
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
    for o in list(bpy.context.scene.objects):   # importer empties
        if o != obj:
            bpy.data.objects.remove(o, do_unlink=True)
    return obj


def base_colour(obj):
    for slot in obj.material_slots:
        nodes = slot.material.node_tree.nodes if slot.material and slot.material.node_tree else []
        bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf and bsdf.inputs["Base Color"].is_linked:
            node = bsdf.inputs["Base Color"].links[0].from_node
            if node.type == "TEX_IMAGE":
                return node.image
    raise RuntimeError(f"{obj.name}: no base-colour image")


def build(name, src, height, depth, budget, tex_size, origin):
    clear()
    obj = load(os.path.join(IN, src))
    raw_tris = tris(obj)
    (x0, y0, z0), (x1, y1, z1) = bounds(obj)
    s = height / (z1 - z0)
    sy = depth / (y1 - y0) if depth else s
    for v in obj.data.vertices:
        v.co.x, v.co.y, v.co.z = v.co.x * s, v.co.y * sy, (v.co.z - z0) * s

    if tris(obj) > budget:
        mod = obj.modifiers.new("Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = budget / tris(obj)
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)

    # origin: base centre on the floor; the lantern hangs from the top of its ring; the frame corner's origin is its
    # outer top-left corner at its back face, so it can be put on a frame's corner and mirrored for the other three.
    (x0, y0, z0), (x1, y1, z1) = bounds(obj)
    if origin == "base":
        off = ((x0 + x1) / 2, (y0 + y1) / 2, 0.0)
    elif origin == "top":
        off = ((x0 + x1) / 2, (y0 + y1) / 2, z1)
    else:  # corner: viewer's left (-X), top (+Z), back (+Y; front is -Y)
        off = (x0, y1, z1)
    for v in obj.data.vertices:
        v.co.x -= off[0]
        v.co.y -= off[1]
        v.co.z -= off[2]
    obj.data.update()
    obj.name = obj.data.name = f"GP_{name}"

    img = base_colour(obj)
    out_img = bpy.data.images.new(f"GP_{name}_BaseColor", img.size[0], img.size[1])
    out_img.pixels = img.pixels[:]
    if img.size[0] > tex_size:
        out_img.scale(tex_size, tex_size)
    tex_path = os.path.join(OUT, f"GP_{name}_BaseColor.jpg")
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    out_img.save_render(tex_path, scene=scene)

    for slot in obj.material_slots:   # the FBX carries one material name only; Unity assigns its own
        slot.material.name = f"GP_{name}"
    fbx_path = os.path.join(OUT, f"GP_{name}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path, use_selection=False, object_types={"MESH"},
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL", apply_unit_scale=True,
        bake_space_transform=True, path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE",
    )
    (x0, y0, z0), (x1, y1, z1) = bounds(obj)
    return {
        "source": src, "triangles_raw": raw_tris, "triangles": tris(obj), "budget": budget,
        "size_m_xyz": [round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)],
        "texture": f"{img.size[0]} -> {min(img.size[0], tex_size)}", "origin": origin,
    }


os.makedirs(OUT, exist_ok=True)
report = {name: build(name, *args) for name, args in PROPS.items()}
with open(os.path.join(HERE, "tripo_props.json"), "w") as f:
    json.dump(report, f, indent=2)
    f.write("\n")
for name, r in report.items():
    print(f"GP_{name}: {r['triangles_raw']} -> {r['triangles']} triangles, {r['size_m_xyz']} m, texture {r['texture']}")
