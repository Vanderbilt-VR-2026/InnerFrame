"""Bake the Painted Bedroom's daylight into one lightmap per fixed object (Cycles), for Unity.

    blender -b painted_bedroom.blend -P bake_lightmaps.py -- --out DIR [options]

    --out DIR          where lm_<object>.png + lightmaps.json go (required)
    --uv-blend FILE    also save a copy of the .blend with only the "Lightmap" UV layers added; export
                       the room from THAT file:  blender -b FILE -P room_io.py -- export --lightmaps DIR
    --table FILE       PaintedBedroomPhysicsTable.asset; objects with liftable: 1 are not baked (default:
                       Assets/InnerFrameBedroomWeb/Resources/ in the checkout the .blend lives in)
    --textures DIR     where to find textures the .blend points at elsewhere (default: that checkout's
                       Assets/InnerFrameBedroomWeb/Data/Textures)
    --samples N        Cycles samples per texel (default 256, adaptive)
    --texels N         lightmap texels per metre of surface (default 64)
    --only A,B         bake only these objects (debugging)
    --no-denoise       skip the OpenImageDenoise pass
    --skip-bake        don't bake: reuse the lm_*.png already in --out (to re-run --verify after a change)
    --verify FILE      after baking, render the painter's view twice: Cycles with the lights (truth) and
                       albedo x lightmap with no lights (what Unity shows); write them side by side to FILE
    --verify-samples N samples for the verify renders (default 128)

Why this exists (10/29, notes/painterly-research/FINDINGS.md "路二"): Unity's own baker never gave the
soft, bright daylight the target sheet has; Cycles did at the first try. So the light is computed here
and Unity only displays it. The lightmap stores light, not colour: L = diffuse direct + diffuse indirect
(+ the object's own glow as a constant), per texel, in the "Lightmap" UV layer; the PNG holds
sqrt(L / max), max = 8. The Unity shader (InnerFrame/Painted Lightmapped) shows (albedo + 0.03) x e^2 x 8.

What the script does, in order:
1. Light rig (RIG below): the same sun + window sky light + dim world that produced the accepted Cycles
   reference (blender_light/P1_rig_cycles_warm_painter.png). The window frame is excluded from the sky
   light (light linking), so the frame right next to the light does not clip.
2. Materials rebuilt as Principled (roughness 0.85): base = texture x if_color, emission = base x
   if_emission, alpha = if_opacity. Ink outlines and markers are hidden; glass and the view outside the
   window cast no shadow. This mirrors what Unity's materials show, not the viewport nodes.
3. Every fixed object (not liftable in the physics table) gets a "Lightmap" UV layer
   on each part (smart project 66 deg + pack, all parts of one object in one image). The first UV layer
   stays the render layer, so colour textures are still sampled right.
4. Bake DIFFUSE direct+indirect, then EMIT with white emission (light only), add, denoise, encode, save.

Lessons baked in: (a) the new UV layer must not become active_render; (b) lightmap_pack starves
high-poly Tripo meshes, smart_project doesn't; (c) Metal GPU baking crashed, CPU is forced; (d) export
from a blend that has only the UVs added, never from the bake scene (its materials are rebuilt).
"""
import json
import math
import os
import re
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
def repo_of(blend_path):
    """The checkout the .blend lives in (Art/Source/PaintedBedroom/blender/<file>): its Data and Resources."""
    return os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(blend_path)), "..", "..", "..", ".."))


TEXTURES = None   # set in main(): --textures DIR, else <repo of the .blend>/Assets/InnerFrameBedroomWeb/Data/Textures
ROOT_COLLECTION = "PaintedBedroom"
LM_UV = "Lightmap"
LM_MAX = 8.0            # encoded range; the Unity shader's _LightMax
NEVER_BAKED = set()             # the scenery outside the window is baked too since #177's clear glass (11/12): Unity lit it white
SIZE_MIN = {"Bed": 1024}        # per-object floor on the lightmap size: the blanket's dense Tripo mesh still showed seams at 512 (11/11)
SIZE_MAX = {"Window_View": 512} # the 50 m scenery dome would get 2048 by area; it is only seen through the window
SIZE_FLOOR = 256                # every object: the pictures' 64 px maps banded on their flat faces (11/12)
UNWRAP_ANYWAY = {"Bed"}         # Tripo objects whose own atlas overlaps itself (the bed: dark slits on the blanket wherever two
                                # surfaces share texels); these get a fresh smart-project unwrap at SIZE_MIN instead
BAKE_MARGIN = 8                 # texels of margin the bake extends past each UV island (4 left seams at island borders)

# The light rig that produced the accepted Cycles reference (10/29 - 10/30). Blender coordinates: Z up,
# the window wall is at +Y. Sun: direction given as where the sun stands (elevation above the horizon,
# azimuth from +Y towards +X), so the light comes in through the window at ~50 deg. Sky: a rectangle
# behind the window opening, standing in for the bright sky; moved back to y=4.12 so it sits outside
# the open casements. World: a dim neutral fill so nothing is pitch black.
# 11/10-11 (Charlyne, blender_light/W2-W4, W10-W12): no material glow (it lit every face evenly, like a ceiling light, and
# flattened the shadows), window light x1.65 and warmer (sun 1,0.87,0.66; sky 1,0.91,0.78); the sky light is tilted down
# and does not reach the ceiling directly (it is lit by bounce only). The world fill never
# reached the room (the scenery outside the window closes it), so it is 0. The dark end is now lower than the
# target sheet's (床尾, corners): accepted as physically right; to be re-judged once the room shrinks (#144).
RIG = {
    "sun": {"energy": 9.9, "color": (1.0, 0.87, 0.66), "angle_deg": 4.0, "elevation_deg": 50.0, "azimuth_deg": 28.0},
    "sky": {"energy": 594.0, "color": (1.0, 0.91, 0.78), "outside_m": 0.10, "oversize_m": 0.10, "tilt_deg": 25.0,   # placed from the Window's bounds in make_rig (the room moved, 11/12)
            "no_direct": ["Window", "Room_Ceiling"]},   # objects the sky light skips (light linking): the frame it sits against, the ceiling (W11/W12: lit directly it read as a ceiling lamp)
    "world": {"color": (0.95, 0.93, 0.90), "strength": 0.0},
    "material_glow": 0.0,     # multiplies every material's if_emission in the bake (1 = as the data says); unlit stays
    "clamp_indirect": 8.0, "max_bounces": 8, "roughness": 0.85,
}


def log(*a):
    print("[bake_lightmaps]", *a, flush=True)


# ---------------------------------------------------------------------------------------------
# which objects

def liftables_from_table(path):
    """objectName -> liftable from the physics table's YAML (no hand-editing, only reading)."""
    if not path or not os.path.exists(path):
        log(f"no physics table at {path}: every object counts as fixed")
        return set()
    txt = open(path, encoding="utf8").read()
    names = re.findall(r"objectName:\s*(\S+)\s*\n\s*liftable:\s*(\d)", txt)
    lift = {n for n, v in names if v == "1"}
    log(f"physics table: {len(names)} rows, liftable: {sorted(lift)}")
    return lift


def room_objects():
    root = bpy.data.collections.get(ROOT_COLLECTION)
    if root is None:
        raise SystemExit("[bake_lightmaps] no 'PaintedBedroom' collection: is this the right .blend?")
    return sorted([o for o in root.all_objects if o.type == "EMPTY"], key=lambda o: o.name)


def parts_of(emp):
    return sorted([c for c in emp.children if c.type == "MESH" and not c.name.endswith("_ink")],
                  key=lambda o: (o.get("if_order", 1e9), o.name))


def world_area(ob):
    deps = bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(deps).to_mesh()
    try:
        M = ob.matrix_world
        a = 0.0
        for p in me.polygons:
            vs = [M @ me.vertices[i].co for i in p.vertices]
            for i in range(1, len(vs) - 1):
                a += (vs[i] - vs[0]).cross(vs[i + 1] - vs[0]).length / 2
        return a
    finally:
        ob.evaluated_get(deps).to_mesh_clear()


def lightmap_size(area, texels, tris=0):
    px = math.sqrt(max(area, 1e-6)) * texels * 1.15      # 15% for packing gaps
    size = 2 ** round(math.log2(px))
    size = max(size, 2 ** math.ceil(math.log2(max(math.sqrt(tris * 12.0), 1))))  # dense meshes: at least ~12 texels per triangle (bed: 1024)
    return int(min(2048, max(32, size)))


# ---------------------------------------------------------------------------------------------
# images, materials, lights

def fix_image_paths():
    """Textures may point at another checkout (the .blend was made in InnerFrame-s3): fall back to this
    repo's Data/Textures by file name."""
    fixed = unpacked = 0
    for img in bpy.data.images:
        if img.source != "FILE" or not img.filepath:
            continue
        p = bpy.path.abspath(img.filepath)
        alt = os.path.join(TEXTURES, os.path.basename(img.filepath))
        if os.path.exists(alt):                 # the checkout's copy is the truth, even if the .blend points elsewhere
            if img.packed_file is not None:     # Tripo textures are packed: a packed image ignores its path, so unpack it
                img.filepath = alt; img.unpack(method="REMOVE"); img.filepath = alt; img.reload(); unpacked += 1
            elif os.path.normpath(alt) != os.path.normpath(p):
                img.filepath = alt; img.reload(); fixed += 1
        elif not os.path.exists(p):
            log(f"WARNING missing texture {img.name}: {p}")
    if unpacked:
        log(f"unpacked {unpacked} textures so the copies in {TEXTURES} are used (the uv-blend keeps them unpacked)")
    if fixed:
        log(f"re-pointed {fixed} textures to {TEXTURES}")


def image_of(mat):
    if not mat.use_nodes:
        return None
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image is not None:
            return n.image
    return None


def rebuild_material(mat):
    """Principled from the if_* properties. The Value node "emit_white" (set_emit_white below) picks the
    emission colour: 0 = the base colour (what the eye sees), 1 = white (bake light only)."""
    img = image_of(mat)
    color = list(mat.get("if_color", (1, 1, 1)))[:3]
    emission = float(mat.get("if_emission", 0) or 0)
    opacity = float(mat.get("if_opacity", 1) if mat.get("if_opacity", 1) is not None else 1)
    unlit = bool(mat.get("if_unlit", False))
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    emit_white = nt.nodes.new("ShaderNodeValue"); emit_white.name = "emit_white"; emit_white.outputs[0].default_value = 0.0
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Roughness"].default_value = RIG["roughness"]
    tint = nt.nodes.new("ShaderNodeMix"); tint.data_type = "RGBA"; tint.blend_type = "MULTIPLY"
    tint.inputs["Factor"].default_value = 1.0
    tint.inputs[7].default_value = (*color, 1.0)                       # B colour = if_color
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img; tex.name = "albedo"
        tex.interpolation = "Linear"
        nt.links.new(tex.outputs["Color"], tint.inputs[6])             # A colour = texture
    else:
        tint.inputs[6].default_value = (1.0, 1.0, 1.0, 1.0)
    base = tint.outputs[2]
    nt.links.new(base, bsdf.inputs["Base Color"])
    # emission colour: mix(base, white, emit_white) x strength
    emix = nt.nodes.new("ShaderNodeMix"); emix.data_type = "RGBA"; emix.blend_type = "MIX"
    nt.links.new(emit_white.outputs[0], emix.inputs["Factor"])
    nt.links.new(base, emix.inputs[6]); emix.inputs[7].default_value = (1.0, 1.0, 1.0, 1.0)
    nt.links.new(emix.outputs[2], bsdf.inputs["Emission Color"])
    if unlit:
        bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
        nt.links.remove(bsdf.inputs["Base Color"].links[0]) if bsdf.inputs["Base Color"].links else None
        bsdf.inputs["Emission Strength"].default_value = 1.0
    else:
        bsdf.inputs["Emission Strength"].default_value = emission * RIG["material_glow"]
    if opacity < 1.0:
        bsdf.inputs["Alpha"].default_value = opacity
    # the bake target: an image node that each bake points at its object's lightmap (must be active)
    tgt = nt.nodes.new("ShaderNodeTexImage"); tgt.name = "bake_target"; tgt.location = (-600, 400)
    uvn = nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map = LM_UV
    nt.links.new(uvn.outputs["UV"], tgt.inputs["Vector"])
    nt.nodes.active = tgt
    mat["lm_opacity"] = opacity


def set_emit_white(materials, v):
    for m in materials:
        n = m.node_tree.nodes.get("emit_white")
        if n is not None:
            n.outputs[0].default_value = v


def make_rig(scene, window_parts, extra_excluded=()):
    for o in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        bpy.data.objects.remove(o)
    s = RIG["sun"]
    sun_d = bpy.data.lights.new("LM_Sun", "SUN"); sun_d.energy = s["energy"]; sun_d.color = s["color"]
    sun_d.angle = math.radians(s["angle_deg"])
    sun = bpy.data.objects.new("LM_Sun", sun_d); scene.collection.objects.link(sun)
    el, az = math.radians(s["elevation_deg"]), math.radians(s["azimuth_deg"])
    towards_sun = Vector((math.cos(el) * math.sin(az), math.cos(el) * math.cos(az), math.sin(el)))
    sun.rotation_euler = (-towards_sun).to_track_quat("-Z", "Y").to_euler()   # a light shines down its -Z
    sun.location = (0, 0, 5)
    k = RIG["sky"]
    # the rectangle sits just outside the window opening, a little larger than it, facing in: derived from the Window
    # object's bounds so it follows the room (a hard-coded spot was 1.2 m behind the wall after #193 and lit nothing)
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for ob in parts_of(bpy.data.objects["Window"]):
        for c in ob.bound_box:
            w = ob.matrix_world @ Vector(c); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    sky_d = bpy.data.lights.new("LM_WindowSky", "AREA"); sky_d.shape = "RECTANGLE"
    sky_d.energy = k["energy"]; sky_d.color = k["color"]
    sky_d.size, sky_d.size_y = (hi.x - lo.x) + k["oversize_m"], (hi.z - lo.z) + k["oversize_m"]
    sky = bpy.data.objects.new("LM_WindowSky", sky_d); scene.collection.objects.link(sky)
    sky.location = ((lo.x + hi.x) / 2, hi.y + k["outside_m"], (lo.z + hi.z) / 2)
    log(f"sky light {sky_d.size:.2f} x {sky_d.size_y:.2f} m at {tuple(round(v, 2) for v in sky.location)} (window y {lo.y:.2f}..{hi.y:.2f})")
    # shines into the room (-Y), tilted down: sky light through a window travels downward, to the floor and the lower
    # walls, not up to the ceiling (11/10, W9/W10: level, the ceiling by the window went white)
    t = math.radians(k.get("tilt_deg", 0.0))
    sky.rotation_euler = Vector((0, -math.cos(t), -math.sin(t))).to_track_quat("-Z", "Z").to_euler()
    for ob in (sun, sky):
        if hasattr(ob, "visible_camera"):
            ob.visible_camera = False      # the sky rectangle stands where the trees are: light only, never seen
    # light linking: the window frame does not receive the sky light (it sits against it and clipped at 37%)
    try:
        coll = bpy.data.collections.new("LM_SkyReceivers")
        sky.light_linking.receiver_collection = coll
        for ob in list(window_parts) + list(extra_excluded):
            if ob.name not in coll.objects: coll.objects.link(ob)
        for co in coll.collection_objects:
            co.light_linking.link_state = "EXCLUDE"
        log(f"sky light excludes {len(coll.objects)} parts (window frame, ceiling)")
    except Exception as e:   # older Blender: no light linking
        log(f"WARNING light linking unavailable ({e}); the window frame may clip")
    w = RIG["world"]
    world = scene.world or bpy.data.worlds.new("LM_World"); scene.world = world
    world.use_nodes = True
    bg = next((n for n in world.node_tree.nodes if n.type == "BACKGROUND"), None) or world.node_tree.nodes.new("ShaderNodeBackground")
    bg.inputs["Color"].default_value = (*w["color"], 1.0); bg.inputs["Strength"].default_value = w["strength"]
    return sun, sky


def setup_cycles(scene, samples):
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"                      # Metal crashed on the 10/29 bakes
    scene.cycles.samples = samples
    scene.cycles.use_adaptive_sampling = True
    scene.cycles.adaptive_threshold = 0.01
    scene.cycles.max_bounces = RIG["max_bounces"]; scene.cycles.diffuse_bounces = RIG["max_bounces"]
    scene.cycles.glossy_bounces = 2; scene.cycles.transmission_bounces = 4; scene.cycles.transparent_max_bounces = 8
    scene.cycles.sample_clamp_indirect = RIG["clamp_indirect"]; scene.cycles.sample_clamp_direct = 0
    scene.cycles.use_denoising = False               # bakes are denoised separately below
    scene.render.bake.margin = BAKE_MARGIN
    scene.render.bake.use_clear = True
    scene.render.bake.use_selected_to_active = False
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"; scene.view_settings.look = "None"; scene.view_settings.exposure = 0


# ---------------------------------------------------------------------------------------------
# UVs

def is_tripo(ob):
    """A Tripo model: one atlas texture (<name>_tripo_basecolor) already unwrapped without overlap."""
    for s in ob.material_slots:
        img = image_of(s.material) if s.material else None
        if img is not None and "_tripo_" in img.name.lower():
            return True
    return False


def add_lightmap_uvs(emp, parts):
    """One 'Lightmap' UV layer per part, all parts of the object packed into one 0..1 square.
    The first UV layer stays the render layer. Tripo parts (dense meshes with their own non-overlapping atlas)
    keep a copy of that atlas as the lightmap UVs: smart-projecting 19k tiny triangles starved them (11/10, the
    bed: 70% of triangles under one texel, black patches on the blanket)."""
    tripo = all(is_tripo(ob) for ob in parts) and emp.name not in UNWRAP_ANYWAY
    for ob in parts:
        me = ob.data
        if LM_UV in me.uv_layers:
            me.uv_layers.remove(me.uv_layers[LM_UV])
        if not me.uv_layers:
            me.uv_layers.new(name="UVMap")
        me.uv_layers[0].active_render = True
        me.uv_layers.active_index = 0
        lm = me.uv_layers.new(name=LM_UV, do_init=tripo)     # do_init copies the active (first) UV map
        me.uv_layers.active = lm
    if tripo:
        return "tripo atlas"
    bpy.ops.object.select_all(action="DESELECT")
    for ob in parts:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.01, rotate=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    return "smart project"


def restore_uv_active(parts):
    for ob in parts:
        ob.data.uv_layers.active_index = 0
        ob.data.uv_layers[0].active_render = True


# ---------------------------------------------------------------------------------------------
# bake

def new_float_image(name, size):
    img = bpy.data.images.get(name)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, size, size, alpha=False, float_buffer=True)
    img.colorspace_settings.name = "Non-Color"
    return img


def pixels(img):
    a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)[..., :3]


def bake_object(scene, emp, parts, size, sun=None, sky=None):
    """Returns (L, D_sun): total light (diffuse direct + indirect + white emission) and the sun's direct part alone.
    Unity stores sun_direct / L in the lightmap's alpha, so a realtime shadow from a picked-up object can darken
    just the sunlit part of the floor (11/10, Charlyne: the objects you pick up must cast shadows)."""
    for ob in parts:
        ob.data.uv_layers.active = ob.data.uv_layers[LM_UV]
        for slot in ob.material_slots:
            if slot.material is None:
                continue
            tgt = slot.material.node_tree.nodes.get("bake_target")
            slot.material.node_tree.nodes.active = tgt
    bpy.ops.object.select_all(action="DESELECT")
    for ob in parts:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    mats = {s.material for ob in parts for s in ob.material_slots if s.material}

    diffuse = new_float_image(f"lmD_{emp.name}", size)
    for m in mats:
        m.node_tree.nodes["bake_target"].image = diffuse
    set_emit_white(mats, 0.0)
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"DIRECT", "INDIRECT"}, margin=BAKE_MARGIN, use_clear=True)
    D = pixels(diffuse).copy()

    emit = new_float_image(f"lmE_{emp.name}", size)
    for m in mats:
        m.node_tree.nodes["bake_target"].image = emit
    set_emit_white(mats, 1.0)
    bpy.ops.object.bake(type="EMIT", margin=BAKE_MARGIN, use_clear=True)
    E = pixels(emit).copy()
    set_emit_white(mats, 0.0)
    Dsun = None
    if sun is not None:                           # the sun alone, direct only: hide the sky light for this pass
        if sky is not None: sky.hide_render = True
        direct = new_float_image(f"lmS_{emp.name}", size)
        for m in mats:
            m.node_tree.nodes["bake_target"].image = direct
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"DIRECT"}, margin=BAKE_MARGIN, use_clear=True)
        Dsun = pixels(direct).copy()
        bpy.data.images.remove(direct)
        if sky is not None: sky.hide_render = False
    bpy.data.images.remove(diffuse); bpy.data.images.remove(emit)
    bpy.ops.object.select_all(action="DESELECT")
    return D + E, Dsun


def denoise(L, name):
    """OpenImageDenoise via a throw-away compositor scene: Image -> Denoise -> file (EXR), read back."""
    h, w = L.shape[:2]
    src = bpy.data.images.new(f"dn_src_{name}", w, h, alpha=False, float_buffer=True)
    src.colorspace_settings.name = "Non-Color"
    rgba = np.concatenate([L, np.ones((h, w, 1), np.float32)], axis=2).astype(np.float32)
    src.pixels.foreach_set(rgba.ravel())
    sc = bpy.data.scenes.new(f"dn_{name}")
    try:
        sc.render.engine = "CYCLES"; sc.cycles.samples = 1; sc.cycles.device = "CPU"
        sc.render.resolution_x, sc.render.resolution_y = w, h; sc.render.resolution_percentage = 100
        sc.render.use_compositing = True
        if hasattr(sc, "compositing_node_group"):          # Blender 5: the compositor is a node group
            nt = bpy.data.node_groups.new(f"dn_{name}", "CompositorNodeTree"); sc.compositing_node_group = nt
            nt.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
            n_out = nt.nodes.new("NodeGroupOutput")
        else:                                              # Blender 4: scene.node_tree + Composite node
            sc.use_nodes = True; nt = sc.node_tree; nt.nodes.clear()
            n_out = nt.nodes.new("CompositorNodeComposite")
        n_img = nt.nodes.new("CompositorNodeImage"); n_img.image = src
        n_dn = nt.nodes.new("CompositorNodeDenoise")
        for attr, val in (("use_hdr", True), ("prefilter", "ACCURATE")):   # names differ between Blender 4 and 5
            if hasattr(n_dn, attr): setattr(n_dn, attr, val)
        for inp in n_dn.inputs:                                             # Blender 5 exposes them as inputs
            if inp.name == "HDR": inp.default_value = True
        nt.links.new(n_img.outputs["Image"], n_dn.inputs["Image"]); nt.links.new(n_dn.outputs["Image"], n_out.inputs[0])
        sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
        sc.render.image_settings.file_format = "OPEN_EXR"; sc.render.image_settings.color_depth = "32"
        sc.render.image_settings.color_mode = "RGB"
        path = os.path.join(bpy.app.tempdir, f"dn_{name}.exr")
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True, scene=sc.name)
        res = bpy.data.images.load(path, check_existing=False)
        res.colorspace_settings.name = "Non-Color"
        out = pixels(res).copy()
        bpy.data.images.remove(res)
        try: os.remove(path)
        except OSError: pass
        return out
    finally:
        bpy.data.scenes.remove(sc); bpy.data.images.remove(src)
        ng = bpy.data.node_groups.get(f"dn_{name}")
        if ng is not None: bpy.data.node_groups.remove(ng)


def fill_holes(L, mask, passes=6):
    """Texels the bake never wrote (mask True) take the mean of their written neighbours, a few passes out."""
    L = L.copy(); written = ~mask
    for _ in range(passes):
        if written.all():
            break
        acc = np.zeros_like(L); cnt = np.zeros(L.shape[:2], np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0: continue
                sh = np.roll(np.roll(L, dy, 0), dx, 1); shw = np.roll(np.roll(written, dy, 0), dx, 1)
                acc += sh * shw[..., None]; cnt += shw
        fill = (~written) & (cnt > 0)
        L[fill] = acc[fill] / cnt[fill][:, None]; written = written | fill
    return L


def save_png(L, path, sun_fraction=None):
    """sqrt(L / LM_MAX) in an 8-bit PNG (Non-Color: bytes are stored as given); alpha = the sun's share of L."""
    h, w = L.shape[:2]
    e = np.sqrt(np.clip(L / LM_MAX, 0, 1)).astype(np.float32)
    # 8 bits of sqrt(L) step by ~2.5% of L on a dim ceiling, which bands on a flat surface: dither by up to half a
    # step before the byte rounding, so the steps turn into fine grain (invisible under the paint strokes)
    rng = np.random.default_rng(1)
    e = np.clip(e + (rng.random(e.shape, dtype=np.float32) - 0.5) / 255.0, 0, 1)
    a = np.ones((h, w, 1), np.float32) if sun_fraction is None else np.clip(sun_fraction, 0, 1).astype(np.float32)[..., None]
    img = bpy.data.images.new("lm_out", w, h, alpha=True, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"; img.alpha_mode = "STRAIGHT"
    img.pixels.foreach_set(np.concatenate([e, a], axis=2).ravel())
    img.filepath_raw = path; img.file_format = "PNG"; img.save()
    bpy.data.images.remove(img)


# ---------------------------------------------------------------------------------------------
# verify: Cycles with lights vs albedo x lightmap

def verify(scene, baked, liftables, out_png, samples):
    import render_views
    with open(render_views.VIEWS) as f:
        views = json.load(f)
    cam = scene.camera
    render_views.place(cam, views["painter"], painter=True)
    size = [views["painter"]["size"][0] // 2, views["painter"]["size"][1] // 2]
    for emp in room_objects():            # liftables are not lightmapped: hide them in both renders
        if emp.name in liftables:
            for ob in emp.children_recursive: ob.hide_render = True
    scene.cycles.samples = samples; scene.cycles.use_denoising = True
    a = os.path.join(os.path.dirname(out_png), "verify_cycles.png")
    render_views.render(scene, a, size)
    # albedo x lightmap, no lights: emission = base x e^2 x LM_MAX
    for o in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        o.hide_render = True
    for n in scene.world.node_tree.nodes:
        if n.type == "BACKGROUND":
            n.inputs["Strength"].default_value = 0.0
    for emp, (png, _) in baked.items():
        lm_img = bpy.data.images.load(png, check_existing=True); lm_img.colorspace_settings.name = "Non-Color"
        for ob in parts_of(bpy.data.objects[emp]):
            for slot in ob.material_slots:
                m = slot.material
                if m is None or m.get("lm_verify_done"):
                    continue
                nt = m.node_tree
                bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
                base_link = bsdf.inputs["Base Color"].links[0].from_socket if bsdf.inputs["Base Color"].links else None
                tgt = nt.nodes["bake_target"]; tgt.image = lm_img
                sq = nt.nodes.new("ShaderNodeVectorMath"); sq.operation = "MULTIPLY"
                nt.links.new(tgt.outputs["Color"], sq.inputs[0]); nt.links.new(tgt.outputs["Color"], sq.inputs[1])
                sc8 = nt.nodes.new("ShaderNodeVectorMath"); sc8.operation = "SCALE"; sc8.inputs["Scale"].default_value = LM_MAX
                nt.links.new(sq.outputs[0], sc8.inputs[0])
                spec = nt.nodes.new("ShaderNodeVectorMath"); spec.operation = "ADD"; spec.inputs[1].default_value = (0.03, 0.03, 0.03)
                if base_link is not None: nt.links.new(base_link, spec.inputs[0])
                else: spec.inputs[0].default_value = bsdf.inputs["Base Color"].default_value[:3]
                mul = nt.nodes.new("ShaderNodeVectorMath"); mul.operation = "MULTIPLY"
                nt.links.new(spec.outputs[0], mul.inputs[0]); nt.links.new(sc8.outputs[0], mul.inputs[1])
                em = nt.nodes.new("ShaderNodeEmission"); nt.links.new(mul.outputs[0], em.inputs["Color"])
                outn = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
                if m["lm_opacity"] < 1.0:
                    mixs = nt.nodes.new("ShaderNodeMixShader"); tr = nt.nodes.new("ShaderNodeBsdfTransparent")
                    mixs.inputs["Fac"].default_value = m["lm_opacity"]
                    nt.links.new(tr.outputs[0], mixs.inputs[1]); nt.links.new(em.outputs[0], mixs.inputs[2])
                    nt.links.new(mixs.outputs[0], outn.inputs["Surface"])
                else:
                    nt.links.new(em.outputs[0], outn.inputs["Surface"])
                m["lm_verify_done"] = True
    scene.cycles.samples = max(16, samples // 4)
    b = os.path.join(os.path.dirname(out_png), "verify_lightmapped.png")
    render_views.render(scene, b, size)
    A = np.clip(pixels(bpy.data.images.load(a)), 0, 1); B = np.clip(pixels(bpy.data.images.load(b)), 0, 1)   # byte PNGs: pixels are the stored (display) values
    diff = np.abs(A - B).mean() * 255; ratio = B.mean() / max(A.mean(), 1e-6)
    log(f"verify: mean |A-B| {diff:.1f}/255, B/A {ratio:.3f}, A mean {A.mean() * 255:.1f}, B mean {B.mean() * 255:.1f}")
    sheet = np.concatenate([A, np.ones((A.shape[0], 8, 3), np.float32), B], axis=1)
    h, w = sheet.shape[:2]
    img = bpy.data.images.new("verify", w, h, alpha=False)
    img.pixels.foreach_set(np.concatenate([sheet, np.ones((h, w, 1), np.float32)], axis=2).ravel())
    img.filepath_raw = out_png; img.file_format = "PNG"; img.save()
    log(f"verify sheet (left Cycles truth, right albedo x lightmap): {out_png}")
    return diff, ratio


# ---------------------------------------------------------------------------------------------

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv and argv.index(k) + 1 < len(argv) else d
    out = opt("--out")
    if not out:
        raise SystemExit(__doc__)
    out = os.path.abspath(out); os.makedirs(out, exist_ok=True)
    samples = int(opt("--samples", 256)); texels = float(opt("--texels", 64))
    only = set(opt("--only", "").split(",")) if "--only" in argv else None
    use_denoise = "--no-denoise" not in argv; skip_bake = "--skip-bake" in argv
    uv_blend = opt("--uv-blend"); verify_png = opt("--verify")
    repo = repo_of(bpy.data.filepath)
    table = opt("--table", os.path.join(repo, "Assets", "InnerFrameBedroomWeb", "Resources", "PaintedBedroomPhysicsTable.asset"))
    global TEXTURES
    TEXTURES = opt("--textures", os.path.join(repo, "Assets", "InnerFrameBedroomWeb", "Data", "Textures"))

    scene = bpy.context.scene
    fix_image_paths()
    liftables = liftables_from_table(table)
    statics = [e for e in room_objects() if e.name not in liftables | NEVER_BAKED and (only is None or e.name in only)]
    log(f"fixed objects to bake: {len(statics)}: {[e.name for e in statics]}")

    # 1. UVs first, on the untouched scene, so the uv-blend carries nothing else
    sizes = {}
    for emp in statics:
        parts = parts_of(emp)
        if not parts:
            continue
        how = add_lightmap_uvs(emp, parts)
        tris = sum(len(p.data.polygons) for p in parts)
        sizes[emp.name] = max(lightmap_size(sum(world_area(p) for p in parts), texels, tris), SIZE_MIN.get(emp.name, 0), SIZE_FLOOR)
        sizes[emp.name] = min(sizes[emp.name], SIZE_MAX.get(emp.name, 2048))
        log(f"UV {emp.name}: {len(parts)} parts, {tris} faces, {how}, {sizes[emp.name]} px")
    if uv_blend:
        for emp in statics:
            restore_uv_active(parts_of(emp))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(uv_blend), copy=True)
        log(f"saved {uv_blend} (original + Lightmap UVs only); export the room from it")

    # 2. scene for baking
    for c in (bpy.data.collections.get("Ink"), bpy.data.collections.get("Markers")):
        if c is not None:
            c.hide_render = True
    for ob in bpy.data.objects:
        if ob.name.endswith("_ink"):
            ob.hide_render = True
    used = {s.material for emp in room_objects() for ob in parts_of(emp) for s in ob.material_slots if s.material}
    for m in used:
        rebuild_material(m)
    log(f"rebuilt {len(used)} materials as Principled")
    for emp in room_objects():
        for ob in parts_of(emp):
            if emp.name == "Window_View" or any(s.material and s.material.get("lm_opacity", 1) < 1 for s in ob.material_slots):
                ob.visible_shadow = False              # sun passes through glass and the scenery outside
    excluded = [ob for name in RIG["sky"].get("no_direct", []) if name in bpy.data.objects for ob in parts_of(bpy.data.objects[name])]
    sun, sky = make_rig(scene, [], excluded)
    setup_cycles(scene, samples)

    # 3. bake
    meta = {"encoding": "sqrt(L/max); alpha = sun direct / L", "max": LM_MAX, "samples": samples, "texels_per_m": texels, "denoise": use_denoise,
            "light": RIG, "blend": bpy.data.filepath, "objects": {}}
    baked = {}
    for emp in statics:
        parts = parts_of(emp)
        if not parts:
            continue
        size = sizes[emp.name]
        png = os.path.join(out, f"lm_{emp.name}.png")
        if skip_bake and os.path.exists(png):
            old = bpy.data.images.load(png, check_existing=False); old.colorspace_settings.name = "Non-Color"
            L = (pixels(old) ** 2 * LM_MAX).copy(); bpy.data.images.remove(old)
            baked[emp.name] = (png, L); log(f"reusing {png}")
            continue
        L, Dsun = bake_object(scene, emp, parts, size, sun, sky)
        unwritten = (L.max(axis=2) == 0) & (Dsun.max(axis=2) == 0)
        L = fill_holes(L, unwritten); Dsun = fill_holes(Dsun, unwritten)
        if use_denoise:
            L = denoise(L, emp.name); Dsun = denoise(Dsun, emp.name + "_sun")
        clipped = float((L.max(axis=2) > LM_MAX).mean())
        sun_fraction = np.clip(Dsun.mean(axis=2) / np.maximum(L.mean(axis=2), 1e-4), 0, 1)
        save_png(L, png, sun_fraction)
        meta["objects"][emp.name] = {"size": size, "parts": [p.name for p in parts], "mean": float(L.mean()),
                                     "max": float(L.max()), "clipped": clipped, "sunFractionMean": float(sun_fraction.mean())}
        baked[emp.name] = (png, L)
        log(f"baked {emp.name}: {size} px, mean L {L.mean():.3f}, max {L.max():.2f}, clipped {clipped * 100:.1f}%")
    if not skip_bake:
        with open(os.path.join(out, "lightmaps.json"), "w") as f:
            json.dump(meta, f, indent=1)
        log(f"wrote {len(baked)} lightmaps + lightmaps.json to {out}")

    if verify_png:
        verify(scene, baked, liftables, os.path.abspath(verify_png), int(opt("--verify-samples", 128)))


if __name__ == "__main__":
    main()
