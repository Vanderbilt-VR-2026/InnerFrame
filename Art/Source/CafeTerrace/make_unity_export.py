"""Cafe Terrace at Night: bake the light and export the set for Unity (Blender 5.2, run in the background):

    /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
        --python Art/Source/CafeTerrace/make_unity_export.py -- --objects <dir with the Tripo .glb files>

It builds the same scene as make_environment.py --look lit (same layout, same lights, the swatches of
swatches/swatches.json), then:

1. Bakes the light (Cycles, diffuse direct + indirect) into every vertex of the set. Nothing is a realtime light on
   Quest: each vertex carries paint x light, and the Unity shader multiplies it by the surface's texture.
   Stored as sqrt(value / 4) so lamp-lit yellow above 1 survives an 8-bit channel (CT_Painted.shader squares it back).
2. Writes Assets/CafeTerrace/Models/CT_Set.fbx (one mesh per layer and material), CT_Placeholders.fbx (the stand-ins
   still waiting for their Tripo models, one object each, named PH_...), and CT_Chairs / CT_Tables / CT_Lantern.fbx:
   every copy of a Tripo model in its place, joined into one mesh, with the light of its own spot baked into its
   vertices like the set's. Their textures are baked down to 1024 px (the table's legs repainted iron in it; the
   lantern's panes split off as a second material so Unity can make them see-through).
3. Writes unity_export.json beside this script: everything the Unity Build menu needs (places, tints, cameras,
   colliders), in this file's own axes (x right, y down the street, z up, metres).

The same input gives the same files, except for the path tracer's noise in the baked light.
"""
import datetime
import hashlib
import json
import math
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_blockout as bo  # noqa: E402
import make_environment as env_mod  # noqa: E402

ASSETS = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "CafeTerrace"))
FIXED_TIME = datetime.datetime(2026, 11, 10, 12, 0, 0)
LIGHT_RANGE = 4.0          # the brightest value a vertex can carry
OBJECT_TEXTURE = 1024
JOINED = {"chair": "CT_Chairs", "table": "CT_Tables", "lantern": "CT_Lantern"}

# how much of a surface's look is its own glow rather than received light (the same mixes as its lit material)
OWN_GLOW = {"WallCafe": (0.5, 0.42), "Cloth": (0.55, 0.45 * 0.9)}   # the cafe wall is yellow all the way up in the effect images


def pin_fbx_exporter():
    """As in Art/Source/StarryNight/sn_blender.py (Cici): the exporter stamps the time and hashes ids per process."""
    from io_scene_fbx import export_fbx_bin, fbx_utils
    if getattr(export_fbx_bin, "_ct_pinned", False):
        return
    header = export_fbx_bin.fbx_header_elements
    export_fbx_bin.fbx_header_elements = lambda root, scene_data, time=None: header(root, scene_data, FIXED_TIME)
    key_to_uuid = fbx_utils._key_to_uuid

    def stable(uuids, key):
        if isinstance(key, int) and 0 <= key < 2 ** 63:
            return key_to_uuid(uuids, key)
        h = int.from_bytes(hashlib.md5(repr(key).encode("utf-8")).digest()[:8], "little") & (2 ** 63 - 1)
        return key_to_uuid(uuids, h)

    fbx_utils._key_to_uuid = stable
    export_fbx_bin._ct_pinned = True


def export_fbx(name, objects):
    pin_fbx_exporter()
    path = os.path.join(ASSETS, "Models", name + ".fbx")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objects:
        ob.hide_render = False
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False,
        path_mode="RELATIVE", embed_textures=False, colors_type="LINEAR")
    print("EXPORT|%s|%d bytes" % (path, os.path.getsize(path)))


def encode(values):
    """Linear value (0..LIGHT_RANGE) -> what is stored in the vertex colour."""
    return np.sqrt(np.clip(values / LIGHT_RANGE, 0.0, 1.0))


def set_colour(mesh, rgb):
    """One colour attribute named Color, per vertex; every other one is dropped."""
    for a in list(mesh.color_attributes):
        mesh.color_attributes.remove(a)
    attr = mesh.color_attributes.new("Color", "FLOAT_COLOR", "POINT")
    data = np.ones((len(mesh.vertices), 4), dtype=np.float32)
    data[:, :3] = rgb
    attr.data.foreach_set("color", data.ravel())
    mesh.update()


def read_colour(mesh, name):
    out = np.zeros(len(mesh.vertices) * 4, dtype=np.float32)
    mesh.color_attributes[name].data.foreach_get("color", out)
    return out.reshape(-1, 4)[:, :3]


def bake_light(ob):
    mesh = ob.data
    light = mesh.color_attributes.new("Light", "FLOAT_COLOR", "POINT")
    mesh.color_attributes.active_color = light
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"DIRECT", "INDIRECT"}, target="VERTEX_COLORS")
    return read_colour(mesh, "Light")


def lift_buried_corners(mesh, light):
    """A vertex bake samples the light exactly at the corners, and a flat panel's corners sit inside its frame, in the
    dark: a door or a board came out black all over. Per face: a face with no lit corner takes the light of the nearest
    lit vertex; a dark corner of a lit face is raised to a share of that face's brightest corner (it stays a soft
    corner shadow). Only for meshes whose faces don't share vertices."""
    from mathutils import kdtree
    lum = light @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    lit = np.nonzero(lum > 0.04)[0]
    if len(lit) == 0:
        return light
    tree = kdtree.KDTree(len(lit))
    for k, i in enumerate(lit):
        tree.insert(mesh.vertices[i].co, k)
    tree.balance()
    out = light.copy()
    fixed = 0
    for poly in mesh.polygons:
        idx = list(poly.vertices)
        top = max(lum[i] for i in idx)
        if top <= 0.04:
            _, k, _ = tree.find(poly.center)
            out[idx] = light[lit[k]]
            fixed += 1
        else:
            best = light[idx[int(np.argmax([lum[i] for i in idx]))]]
            for i in idx:
                if lum[i] < 0.45 * top:
                    out[i] = best * 0.45
    print("CORNERS|%s|%d dark faces relit" % (mesh.name, fixed))
    return out


def bake_object_texture(key, info):
    """The Tripo texture, with the table's iron legs painted in, at OBJECT_TEXTURE px. Returns its pixels."""
    mesh = info["mesh"]
    ob = bpy.data.objects.new("CT_" + key.capitalize(), mesh)
    bpy.context.scene.collection.objects.link(ob)
    img = bpy.data.images.new("CT_%s_bake" % key, OBJECT_TEXTURE, OBJECT_TEXTURE, alpha=False)
    for m in mesh.materials:
        nt = m.node_tree
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        em = nt.nodes.new("ShaderNodeEmission")
        source = next(o for o in nt.nodes[m["ct_colour_node"]].outputs if o.identifier == m["ct_colour_socket"])
        nt.links.new(source, em.inputs["Color"])
        nt.links.new(em.outputs[0], out.inputs["Surface"])
        target = nt.nodes.new("ShaderNodeTexImage")
        target.image = img
        nt.nodes.active = target
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.context.scene.render.bake.margin = 8
    bpy.ops.object.bake(type="EMIT", target="IMAGE_TEXTURES")
    path = os.path.join(ASSETS, "Textures", "CT_%s.png" % key.capitalize())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("TEXTURE|%s|%d bytes" % (path, os.path.getsize(path)))
    px = np.array(img.pixels[:], dtype=np.float32).reshape(OBJECT_TEXTURE, OBJECT_TEXTURE, 4)
    return ob, px


def split_panes(ob, px):
    """The lantern: faces whose texture is the yellow pane colour get material slot 1 (see-through in Unity)."""
    mesh = ob.data
    glass = bpy.data.materials.new("CT_LanternGlass")
    mesh.materials.append(glass)
    uv = mesh.uv_layers.active.data
    n = 0
    for poly in mesh.polygons:
        u = sum(uv[i].uv[0] for i in poly.loop_indices) / poly.loop_total
        v = sum(uv[i].uv[1] for i in poly.loop_indices) / poly.loop_total
        r, g, b = px[min(OBJECT_TEXTURE - 1, int(v % 1.0 * OBJECT_TEXTURE)), min(OBJECT_TEXTURE - 1, int(u % 1.0 * OBJECT_TEXTURE)), :3]
        if g > 0.42 and r > 0.5 and b < 0.45:                # bright yellow: a pane, not rusty iron
            poly.material_index = 1
            n += 1
    print("PANES|%d of %d faces" % (n, len(mesh.polygons)))


def vfov(spec, src):
    if src.get("vfov_deg") or spec.get("vfov_deg"):
        return src.get("vfov_deg", spec.get("vfov_deg"))
    w, h = spec["res"]
    return math.degrees(2 * math.atan(math.tan(math.radians(spec["hfov_deg"]) / 2) * h / w))


def main():
    opts = bo.args_after_dashes()
    with open(os.path.join(HERE, "cafe_layout.json")) as f:
        layout = json.load(f)
    sw_dir = os.path.join(HERE, "swatches")
    with open(os.path.join(sw_dir, "swatches.json")) as f:
        swatches = {k: v for k, v in json.load(f).items() if not k.startswith("_")}
    lt = layout.get("light", {})

    bpy.ops.wm.read_factory_settings(use_empty=True)
    env_mod.apply_tone(layout)
    e = env_mod.Env(layout)
    e.lit_look, e.plank_seg = True, 0.3
    e.sw, e.sw_dir = swatches, sw_dir
    e.objects_dir = opts.get("objects")
    e.build()
    env_mod.setup_cycles(layout)
    env_mod.light_scene(e)
    sc = bpy.context.scene
    sc.cycles.samples = int(lt.get("bake_samples", 48))

    # ---- 1. the set: paint x baked light per vertex
    set_objects, set_rows, ground = [], [], []
    for key in sorted(e.bufs):
        ob = bpy.data.objects["CT_" + key]
        material = key.rsplit("_", 1)[1]
        material = "Stone" if material == "Cobbles" else material
        mesh = ob.data
        paint = read_colour(mesh, "Col")
        if material == "Glow":
            value = paint * lt.get("window_glow", 1.5)
        else:
            light = bake_light(ob)
            if not e.bufs[key].smooth:
                light = lift_buried_corners(mesh, light)
            a, b = OWN_GLOW.get(material, (1.0, 0.0))
            sw = swatches.get(material)
            if sw and "ref" in sw:
                gain = 1.0 / np.array(env_mod.srgb(sw["ref"]), dtype=np.float32)
            elif sw:
                gain = 1.0
            else:
                gain = 2.0 if env_mod.MATERIALS[material][0] else 1.0     # grey streak textures average 0.5
            value = paint * gain * (a * light + b)
            if key in ("Near_Plank", "Near_Cobble", "NearMid_Stone"):
                co = np.zeros(len(mesh.vertices) * 3, dtype=np.float32)
                mesh.vertices.foreach_get("co", co)
                ground.append((co.reshape(-1, 3), light))
        set_colour(mesh, encode(value))
        mesh.calc_loop_triangles()
        set_objects.append(ob)
        set_rows.append({"name": ob.name, "material": material, "triangles": len(mesh.loop_triangles)})
        print("BAKED|%s|%d tris|max %.2f" % (ob.name, len(mesh.loop_triangles), float(value.max())))
    export_fbx("CT_Set", set_objects)

    ground_co = np.concatenate([g[0] for g in ground])
    ground_light = np.concatenate([g[1] for g in ground])

    def light_at(x, y):
        # the open floor round it, not the shadow under it: the brighter part of the ground within reach
        d = (ground_co[:, 0] - x) ** 2 + (ground_co[:, 1] - y) ** 2
        near = np.argsort(d)[:160]
        lum = ground_light[near].sum(axis=1)
        keep = near[lum >= np.percentile(lum, 70)]
        return [round(float(v), 3) for v in np.clip(ground_light[keep].mean(axis=0) * 1.25, 0.05, LIGHT_RANGE)]

    # ---- 2. the Tripo objects: one mesh each, texture baked down, places written out
    object_rows, model_rows = [], []
    if e.models:
        # Each chair, table and the lantern gets the light of the very spot it stands on, baked into its own vertices
        # (a flat tint per object made them look pasted on: no shading, no shadow from the neighbours). All copies of
        # a model are joined into one mesh in place: one draw call, and the texture is still the model's own.
        bpy.context.view_layer.update()
        placed = [o for o in e.col("Objects").objects]
        joined = []
        for key, info in sorted(e.models.items()):
            ob, px = bake_object_texture(key, info)
            if key == "lantern":
                split_panes(ob, px)
            bpy.data.objects.remove(ob)
            for m in info["mesh"].materials:                 # back to a matt surface for the light bake
                if m.node_tree is None:
                    continue
                out = next(n for n in m.node_tree.nodes if n.type == "OUTPUT_MATERIAL")
                dif = m.node_tree.nodes.new("ShaderNodeBsdfDiffuse")
                m.node_tree.links.new(dif.outputs[0], out.inputs["Surface"])
            insts = [o for o in placed if o.data is info["mesh"]]
            copies = []
            for inst in insts:
                object_rows.append({
                    "name": inst.name, "model": JOINED[key],
                    "x": round(inst.location.x, 4), "y": round(inst.location.y, 4), "z": round(inst.location.z, 4),
                    "rotZ": round(math.degrees(inst.rotation_euler.z), 3),
                    "sx": round(inst.scale.x, 5), "sy": round(inst.scale.y, 5), "sz": round(inst.scale.z, 5)})
                c = bpy.data.objects.new(inst.name + "_copy", info["mesh"].copy())
                c.matrix_world = inst.matrix_world.copy()
                sc.collection.objects.link(c)
                copies.append(c)
            placed = [o for o in placed if o not in insts]
            for inst in insts:
                bpy.data.objects.remove(inst)
            bpy.ops.object.select_all(action="DESELECT")
            for c in copies:
                c.select_set(True)
            bpy.context.view_layer.objects.active = copies[0]
            if len(copies) > 1:
                bpy.ops.object.join()
            one = copies[0]
            one.name = JOINED[key]
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            one.visible_shadow = key != "lantern"            # the lamp must not block its own light
            joined.append((key, one, info, len(insts)))
        for key, one, info, n in joined:
            light = bake_light(one)
            set_colour(one.data, encode(light))
            export_fbx(one.name, [one])
            model_rows.append({"name": one.name, "texture": "CT_%s.png" % key.capitalize(), "triangles": info["tris"] * n,
                               "copies": n, "glass": key == "lantern"})
            print("BAKED|%s|%d copies|max %.2f" % (one.name, n, float(light.max())))
    flame = getattr(e, "flame", None)

    # ---- 3. the stand-ins: one object each, flat colour x the light where they stand
    ph = []
    for parent in [o for o in e.col("Placeholders").objects if o.parent is None]:
        kids = [o for o in e.col("Placeholders").objects if o.parent is parent]
        parts = [parent] + kids
        colours = []
        for o in parts:
            c = np.array(o.data.materials[0].node_tree.nodes[1].inputs["Color"].default_value[:3], dtype=np.float32)
            colours.append(np.tile(c, (len(o.data.vertices), 1)))
        cx = sum(v.co.x for v in parent.data.vertices) / len(parent.data.vertices)
        cy = sum(v.co.y for v in parent.data.vertices) / len(parent.data.vertices)
        tint = np.array(light_at(cx, cy), dtype=np.float32)
        for o, c in zip(parts, colours):
            set_colour(o.data, encode(c * tint))
        bpy.ops.object.select_all(action="DESELECT")
        for o in parts:
            o.select_set(True)
        bpy.context.view_layer.objects.active = parent
        if kids:
            bpy.ops.object.join()
        ph.append(parent)
    ph_rows = [{"name": o.name} for o in ph]
    if ph:
        export_fbx("CT_Placeholders", ph)

    # ---- 4. what Unity needs to know
    s, t, f = layout["street"], layout["terrace"], layout["far"]
    cams = []
    for name, spec in layout["cameras"].items():
        if name.startswith("_") or spec.get("ortho"):
            continue
        src = layout[spec["from"]] if "from" in spec else spec
        p = src.get("pos") or layout["painter"]["easel"]
        cams.append({"name": name, "x": p[0], "y": p[1], "z": p[2] if len(p) > 2 else layout["rig"]["eye_height"],
                     "yaw": src.get("yaw_deg", spec.get("yaw_deg", 0)), "pitch": spec.get("pitch_deg", 0),
                     "vfov": round(vfov(spec, src), 3), "w": spec["res"][0], "h": spec["res"][1],
                     "target": spec.get("target") or "", "hide": spec.get("hide", [])})
    walls = [{"name": h["name"], "ax": h["a"][0], "ay": h["a"][1], "bx": h["b"][0], "by": h["b"][1],
              "height": sum(h["floors"])} for h in layout["houses"]]
    materials = []
    for name, (grey, tile) in sorted(env_mod.MATERIALS.items()):
        sw = swatches.get(name)
        materials.append({"name": name, "texture": os.path.basename(sw["file"]) if sw else (grey or ""),
                          "texGain": 1.0 if sw else (2.0 if grey else 1.0), "desat": sw.get("desat", 0.0) if sw else 0.0})
    out = {
        "_about": "Written by make_unity_export.py from cafe_layout.json. Do not edit: change the layout and export again.",
        "rigScale": layout["rig"]["scale"], "worldScale": layout["rig"]["world_scale"], "eyeHeight": layout["rig"]["eye_height"],
        "saturation": lt.get("saturation", 1.0), "lightRange": LIGHT_RANGE,
        "sky": lt["sky"], "skyHorizon": lt["sky_horizon"],
        "starsSmall": lt.get("stars", {}).get("small", 30), "starsLarge": lt.get("stars", {}).get("large", 5),
        "starsSeed": lt.get("stars", {}).get("seed", 7),
        "spawn": {"x": layout["spawn"]["pos"][0], "y": layout["spawn"]["pos"][1], "yaw": layout["spawn"]["yaw_deg"]},
        "painter": {"x": layout["painter"]["easel"][0], "y": layout["painter"]["easel"][1], "yaw": layout["painter"]["yaw_deg"]},
        "vincent": {"x": layout["painter"]["vincent"][0], "y": layout["painter"]["vincent"][1], "yaw": 0},
        "exit": {"x": layout["exit_trigger"]["center"][0], "y": layout["exit_trigger"]["center"][1],
                 "sx": layout["exit_trigger"]["size"][0], "sy": layout["exit_trigger"]["size"][1]},
        "ground": {"x0": s["cafe_wall_x"] - 1.0, "x1": s["right_wall_x"] + 1.0, "y0": s["back_wall_y"] - 1.0,
                   "y1": f["start_y"], "kerb": s["kerb_height"]},
        "terrace": {"x0": t["x"][0], "x1": t["x"][1], "y0": t["y"][0], "y1": t["y"][1], "h": t["height"]},
        "tableDiameter": layout["sizes"]["table"]["diameter"], "tableHeight": layout["sizes"]["table"]["height"],
        "flame": {"x": flame[0], "y": flame[1], "z": flame[2]} if flame else {"x": 0, "y": 0, "z": -10},
        "materials": materials, "set": set_rows, "models": model_rows, "objects": object_rows, "placeholders": ph_rows,
        "walls": walls, "cameras": cams,
    }
    path = os.path.join(HERE, "unity_export.json")
    with open(path, "w") as fh:
        json.dump(out, fh, indent=1)
        fh.write("\n")
    total = sum(r["triangles"] for r in set_rows)
    objs = sum(m["triangles"] for m in model_rows)
    print("COUNT|set %d tris in %d meshes|objects %d tris in %d|stand-ins %d" % (total, len(set_rows), objs, len(object_rows), len(ph_rows)))
    print("JSON|%s" % path)


main()
