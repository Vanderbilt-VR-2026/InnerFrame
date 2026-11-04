"""Shared helpers for The Starry Night's Blender scripts (Blender 5.2, run in the background):

    /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python <script> -- <args>

- export_fbx(): one FBX per object group, with the team's export settings (ground truth #7: Apply Transform, Forward -Z,
  Up Y, FBX scale "All", no leaf bones) and the same bytes on every run: Blender's FBX exporter stamps the file with the
  current time (export_fbx_bin.fbx_header_elements) and derives object ids from Python's per-process string hash
  (fbx_utils._key_to_uuid); both are pinned here (a fixed date, an MD5 of the key).
- save_blend(): the .blend kept next to the scripts, uncompressed.
"""
import datetime
import hashlib
import os

import bpy

FIXED_TIME = datetime.datetime(2026, 11, 4, 12, 0, 0)


def _pin_fbx_exporter():
    from io_scene_fbx import export_fbx_bin, fbx_utils
    if getattr(export_fbx_bin, "_sn_pinned", False):
        return
    header = export_fbx_bin.fbx_header_elements

    def fixed_header(root, scene_data, time=None):
        return header(root, scene_data, FIXED_TIME)

    export_fbx_bin.fbx_header_elements = fixed_header
    key_to_uuid = fbx_utils._key_to_uuid

    def stable_key_to_uuid(uuids, key):
        if isinstance(key, int) and 0 <= key < 2 ** 63:
            return key_to_uuid(uuids, key)
        h = int.from_bytes(hashlib.md5(repr(key).encode("utf-8")).digest()[:8], "little") & (2 ** 63 - 1)
        return key_to_uuid(uuids, h)

    fbx_utils._key_to_uuid = stable_key_to_uuid
    export_fbx_bin._sn_pinned = True


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export_fbx(path, objects):
    _pin_fbx_exporter()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="RELATIVE", embed_textures=False)
    print("EXPORT|%s|%d bytes" % (path, os.path.getsize(path)))


def save_blend(path):
    bpy.context.preferences.filepaths.save_version = 0   # no .blend1 backup beside it
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=False, check_existing=False)
    print("BLEND|%s|%d bytes" % (path, os.path.getsize(path)))


def material(name, rgba):
    """A plain named material (Unity ignores it: SN_Setup assigns its own by name; the colour is for the .blend only)."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = rgba
    return m


def triangles(ob):
    me = ob.data
    me.calc_loop_triangles()
    return len(me.loop_triangles)
