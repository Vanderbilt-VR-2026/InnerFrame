"""Player avatars, step 1 of 3: turns each Tripo GLB in this folder into an FBX that Mixamo's auto-rigger accepts
(Mixamo takes FBX, OBJ or ZIP, not GLB). One mesh, feet at the origin, facing -Y (Blender's front, which Mixamo
and Unity read as the front), scaled to HEIGHT metres, transforms applied, texture embedded.

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P make_mixamo_upload.py

Writes mixamo-upload/<name>.fbx. Step 2 is by hand: upload each to https://www.mixamo.com, place the auto-rigger's
markers, and download "FBX for Unity, T-pose, With Skin" into
Assets/Script/Network/Avatars/. Step 3 is Unity: Tools > InnerFrame > Multiplayer > Build Player Avatars.
"""
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
OUT = HERE / "mixamo-upload"
HEIGHT = 1.70   # metres, top of the head (or hat) to the feet; the avatar is rescaled to each player at runtime

# source GLB (in this folder) -> upload name
AVATARS = {
    "boy worker 3d model.glb": "Avatar_BoyWorker",
}


def convert(src: Path, name: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(src))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if len(meshes) != 1:
        raise SystemExit(f"{src.name}: expected one mesh, found {len(meshes)}")
    body = meshes[0]

    # unparent from Tripo's RootNode, keeping where it is, and bake every transform into the vertices
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o is not body:
            bpy.data.objects.remove(o, do_unlink=True)

    # feet on the origin, centred, HEIGHT tall
    pts = [Vector(v.co) for v in body.data.vertices]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    s = HEIGHT / (hi.z - lo.z)
    offset = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for v in body.data.vertices:
        v.co = (v.co - offset) * s
    body.name = body.data.name = name
    body.data.materials[0].name = name

    OUT.mkdir(exist_ok=True)
    out = OUT / f"{name}.fbx"
    bpy.ops.export_scene.fbx(filepath=str(out), use_selection=False, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             path_mode="COPY", embed_textures=True, mesh_smooth_type="FACE", add_leaf_bones=False)
    print(f"[avatars] {out.name}: {len(body.data.polygons)} faces, {HEIGHT} m, {out.stat().st_size / 1e6:.1f} MB")


for src_name, name in AVATARS.items():
    src = HERE / src_name
    if not src.exists():
        raise SystemExit(f"missing {src}")
    convert(src, name)
