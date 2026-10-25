"""Painted Bedroom <-> Blender.

Blender is the shared source for refining the Painted Bedroom (#44). This script moves the room
between the data format Unity imports (Data/room.json + Data/room.bytes + Data/Textures) and a
.blend file, both ways, without changing the Unity importer.

    # data -> .blend (once, to start; or to reset)
    blender -b -P room_io.py -- import [--data DIR] [--blend FILE]

    # .blend -> data (after every change), then in Unity: Import and Build (no Blender)
    blender -b painted_bedroom.blend -P room_io.py -- export [--data DIR]

    # check: export to a temp folder and compare with the data on disk
    blender -b painted_bedroom.blend -P room_io.py -- check [--data DIR]

What lives where in the .blend:
- One Empty per room object (Bed, Chair_Front, ...), named exactly as in Unity. Never rename them:
  the Unity builder and runtime scripts find objects by name.
- Under each Empty, one mesh object per part (Chair_Front_00, ...). Edit these freely: move
  vertices, add faces, add modifiers (they are applied on export), move the part.
- Each part's ink outline (Chair_Front_00_ink) sits in the "Ink" collection. Don't edit it: on
  export, if its part changed, the outline is rebuilt from the part (the part pushed out along its
  smoothed normals by `ink_width`, faces flipped). Unchanged parts keep their original outline.
- Material settings Unity uses (colour, emission, unlit, opacity, ...) are custom properties on
  each material (`if_*`). The texture is whatever image the material's Image Texture node uses: a
  new image is copied into Data/Textures on export.

Coordinates: Unity is left-handed, Y up; Blender is right-handed, Z up. Unity (x, y, z) is
Blender (x, z, y): Blender's front view (looking along +Y) is Unity looking along +Z. The mirror
flips triangle winding, so faces are reversed on the way in and on the way out.
"""
import hashlib
import json
import math
import os
import re
import shutil
import sys
import tempfile

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_DATA = os.path.normpath(os.path.join(HERE, "..", "InnerFrameBedroomWeb", "Data"))
DEFAULT_BLEND = os.path.join(HERE, "painted_bedroom.blend")
ROOT_COLLECTION = "PaintedBedroom"
INK_COLLECTION = "Ink"
MARKER_COLLECTION = "Markers"
INK_MATERIAL = "Ink_Outline"
SWAP = np.array([0, 2, 1])   # Unity xyz <-> Blender xzy (its own inverse)


def u2b(a):
    """Unity -> Blender for an (n, 3) array or a 3-list (and back: the swap is its own inverse)."""
    a = np.asarray(a, dtype=np.float64)
    return a[..., SWAP]


# ---------------------------------------------------------------------------------------------
# data files

def read_data(data_dir):
    with open(os.path.join(data_dir, "room.json"), encoding="utf8") as f:
        room = json.load(f)
    with open(os.path.join(data_dir, "room.bytes"), "rb") as f:
        raw = f.read()
    F = np.frombuffer(raw, np.float32, room["floatCount"])
    I = np.frombuffer(raw, np.int32, room["intCount"], room["floatCount"] * 4)
    return room, F, I


def mesh_arrays(m, F, I):
    n = m["vertexCount"]
    P = F[m["positions"]:m["positions"] + 3 * n].reshape(-1, 3)
    N = F[m["normals"]:m["normals"] + 3 * n].reshape(-1, 3)
    U = F[m["uvs"]:m["uvs"] + 2 * n].reshape(-1, 2)
    T = I[m["indices"]:m["indices"] + m["indexCount"]].reshape(-1, 3)
    return P, N, U, T


def geom_hash(P, N, U, T):
    h = hashlib.sha1()
    for a in (np.round(np.asarray(P, np.float64), 5), np.round(np.asarray(N, np.float64), 3),
              np.round(np.asarray(U, np.float64), 5), np.asarray(T, np.int64)):
        h.update(np.ascontiguousarray(a).tobytes())
    return h.hexdigest()


def smoothed_normals(P, N):
    """Average the normals of vertices that share a position (how the web study grew its ink shells)."""
    keys = [tuple(k) for k in np.round(np.asarray(P, np.float64), 4)]
    acc = {}
    for k, n in zip(keys, np.asarray(N, np.float64)):
        acc[k] = acc.get(k, 0) + n
    S = np.array([acc[k] for k in keys])
    L = np.linalg.norm(S, axis=1, keepdims=True)
    S = np.where(L > 1e-9, S / np.maximum(L, 1e-9), np.asarray(N, np.float64))
    return S


def make_ink(P, N, U, T, width):
    Pi = np.asarray(P, np.float64) + width * smoothed_normals(P, N)
    return Pi.astype(np.float32), np.asarray(N, np.float32), np.asarray(U, np.float32), np.asarray(T)[:, [0, 2, 1]].copy()


# ---------------------------------------------------------------------------------------------
# import: data -> Blender

def build_mesh(name, P, N, U, T):
    """A Blender mesh with exactly the Unity vertices (no merging), so an untouched mesh exports unchanged."""
    me = bpy.data.meshes.new(name)
    nv, nt = len(P), len(T)
    me.vertices.add(nv)
    me.vertices.foreach_set("co", u2b(P).astype(np.float32).ravel())
    tris = np.asarray(T, np.int64)[:, ::-1]                       # mirror: reverse winding
    me.loops.add(nt * 3)
    me.loops.foreach_set("vertex_index", tris.ravel().astype(np.int32))
    me.polygons.add(nt)
    me.polygons.foreach_set("loop_start", (np.arange(nt) * 3).astype(np.int32))
    me.polygons.foreach_set("loop_total", np.full(nt, 3, np.int32))
    uv = me.uv_layers.new(name="UVMap")
    uv.data.foreach_set("uv", np.asarray(U, np.float32)[tris.ravel()].ravel())
    me.update()
    me.normals_split_custom_set_from_vertices([tuple(n) for n in u2b(N)])
    # the original Unity vertex data, so a part nobody touched exports bit for bit (custom normals
    # alone can't: Blender drops them on degenerate triangles and on vertices no triangle uses)
    me.attributes.new("if_co", "FLOAT_VECTOR", "POINT").data.foreach_set("vector", u2b(P).astype(np.float32).ravel())
    me.attributes.new("if_normal", "FLOAT_VECTOR", "POINT").data.foreach_set("vector", u2b(N).astype(np.float32).ravel())
    me.attributes.new("if_uv", "FLOAT2", "POINT").data.foreach_set("vector", np.asarray(U, np.float32).ravel())
    me["if_topo"] = topo_hash(tris.ravel())
    return me


def topo_hash(loop_vertex_indices):
    return hashlib.sha1(np.asarray(loop_vertex_indices, np.int64).tobytes()).hexdigest()


def untouched_arrays(ob, M):
    """The stored Unity data of a part nobody has edited, or None if it was edited."""
    me = ob.data
    if any(m.show_render for m in ob.modifiers) or not np.allclose(np.array(M), np.eye(4), atol=1e-7):
        return None
    if not all(k in me.attributes for k in ("if_co", "if_normal", "if_uv")) or "if_topo" not in me:
        return None
    nv = len(me.vertices)
    co = np.empty(nv * 3, np.float32); me.vertices.foreach_get("co", co)
    ico = np.empty(nv * 3, np.float32); me.attributes["if_co"].data.foreach_get("vector", ico)
    if not np.array_equal(co, ico) or any(len(p.vertices) != 3 for p in me.polygons):
        return None
    lv = np.empty(len(me.loops), np.int32); me.loops.foreach_get("vertex_index", lv)
    if topo_hash(lv) != me["if_topo"]:
        return None
    N = np.empty(nv * 3, np.float32); me.attributes["if_normal"].data.foreach_get("vector", N)
    U = np.empty(nv * 2, np.float32); me.attributes["if_uv"].data.foreach_get("vector", U)
    T = lv.reshape(-1, 3)[:, ::-1]
    return (u2b(co.reshape(-1, 3)).astype(np.float32), u2b(N.reshape(-1, 3)).astype(np.float32),
            U.reshape(-1, 2), T.astype(np.int32))


def get_image(data_dir, tex_name):
    img = bpy.data.images.get(tex_name + ".png")
    if img is None:
        img = bpy.data.images.load(os.path.join(data_dir, "Textures", tex_name + ".png"), check_existing=True)
        img.name = tex_name + ".png"
    return img


def build_material(i, md, room, data_dir):
    mat = bpy.data.materials.new(md["name"])
    mat["if_index"] = i
    mat["if_color"] = list(md["color"])
    for k in ("unlit", "emission", "doubleSided", "outline", "opacity"):
        mat["if_" + k] = md[k]
    # stroke relief map (#50): carried through by name, written back as `height` in the same place
    if "height" in md:
        mat["if_height_tex"] = room["textures"][md["height"]] if md["height"] >= 0 else ""
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (400, 0)
    col = tuple(md["color"]) + (1.0,)
    tex = None
    if md["texture"] >= 0:
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.location = (-400, 0)
        tex.image = get_image(data_dir, room["textures"][md["texture"]])
    if md["unlit"] or md["outline"]:
        sh = nt.nodes.new("ShaderNodeEmission"); sh.location = (100, 0)
        sh.inputs["Color"].default_value = col
        if tex: nt.links.new(tex.outputs["Color"], sh.inputs["Color"])
    else:
        sh = nt.nodes.new("ShaderNodeBsdfPrincipled"); sh.location = (100, 0)
        sh.inputs["Base Color"].default_value = col
        sh.inputs["Roughness"].default_value = 1.0
        if "Specular IOR Level" in sh.inputs: sh.inputs["Specular IOR Level"].default_value = 0.0
        if tex: nt.links.new(tex.outputs["Color"], sh.inputs["Base Color"])
        # the web study's glow, as in the Unity importer: a little of the painted colour emits
        e = md["emission"] * 0.35 if md["emission"] > 0 else 0.11
        sh.inputs["Emission Strength"].default_value = e
        if tex: nt.links.new(tex.outputs["Color"], sh.inputs["Emission Color"])
        else: sh.inputs["Emission Color"].default_value = col
        if 0 < md["opacity"] < 0.999:
            sh.inputs["Alpha"].default_value = md["opacity"]
    nt.links.new(sh.outputs[0], out.inputs["Surface"])
    mat.diffuse_color = col
    if 0 < md["opacity"] < 0.999:
        mat.surface_render_method = "BLENDED"
    mat.use_backface_culling = bool(md["outline"])            # ink: inside-out shell, only its far side shows
    return mat


def link(obj, coll):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def do_import(data_dir, blend_path):
    room, F, I = read_data(data_dir)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene["if_version"] = room["version"]
    scene["if_painterFov"] = room["painterFov"]
    scene["if_painterLensShiftY"] = room["painterLensShiftY"]
    scene["if_textures"] = list(room["textures"])

    root = bpy.data.collections.new(ROOT_COLLECTION); scene.collection.children.link(root)
    ink_c = bpy.data.collections.new(INK_COLLECTION); scene.collection.children.link(ink_c)
    mk_c = bpy.data.collections.new(MARKER_COLLECTION); scene.collection.children.link(mk_c)

    mats = [build_material(i, md, room, data_dir) for i, md in enumerate(room["materials"])]

    for oi, o in enumerate(room["objects"]):
        emp = bpy.data.objects.new(o["name"], None)
        emp.empty_display_type = "PLAIN_AXES"; emp.empty_display_size = 0.1
        emp.location = Vector(u2b(o["position"]))
        emp["if_order"] = oi
        emp["if_position"] = list(o["position"])           # exact original, kept while the Empty isn't moved
        root.objects.link(emp)
        meshes = {m["name"]: m for m in o["meshes"]}
        order = 0
        for m in o["meshes"]:
            if m["name"].endswith("_ink") and m["name"][:-4] in meshes:
                continue
            P, N, U, T = mesh_arrays(m, F, I)
            ob = bpy.data.objects.new(m["name"], build_mesh(m["name"], P, N, U, T))
            ob.data.materials.append(mats[m["material"]])
            ob.parent = emp
            ob["if_order"] = order; order += 1
            root.objects.link(ob)
            ink = meshes.get(m["name"] + "_ink")
            if ink is not None:
                Pi, Ni, Ui, Ti = mesh_arrays(ink, F, I)
                width = float(np.linalg.norm(Pi.astype(np.float64) - P, axis=1).mean()) if len(Pi) == len(P) else 0.007
                io = bpy.data.objects.new(ink["name"], build_mesh(ink["name"], Pi, Ni, Ui, Ti))
                io.data.materials.append(mats[ink["material"]])
                io.parent = ob
                io.hide_select = True
                link(io, ink_c)
                ob["ink_width"] = round(width, 5)
                ob["ink_material"] = ink["material"]
            # fingerprint of the part as exported now; on export a different fingerprint means "edited"
            ob["if_hash"] = geom_hash(*part_arrays(ob))

    for mk in room["markers"]:
        e = bpy.data.objects.new(mk["name"], None)
        e.empty_display_type = "SPHERE"; e.empty_display_size = 0.05
        e.location = Vector(u2b(mk["position"]))
        e["if_position"] = list(mk["position"])
        mk_c.objects.link(e)

    add_painter_camera(scene, room)
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
    world = bpy.data.worlds.new("World"); scene.world = world
    world.color = (0.55, 0.55, 0.55)

    os.makedirs(os.path.dirname(os.path.abspath(blend_path)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend_path), relative_remap=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_mainfile()
    print(f"[room_io] imported {len(room['objects'])} objects, {len(mats)} materials, "
          f"{len(room['textures'])} textures -> {blend_path}")


def add_painter_camera(scene, room):
    """The painter's view: same eye, target, vertical FOV and lens shift as key P in Unity, framed
    like the canvas (the Art Institute of Chicago image in Assets/Art/Painting is 1686 x 1320)."""
    eye = Vector(u2b(next(m["position"] for m in room["markers"] if m["name"] == "Marker_PainterView")))
    tgt = Vector(u2b(next(m["position"] for m in room["markers"] if m["name"] == "Marker_PainterView_Target")))
    cam = bpy.data.cameras.new("Cam_PainterView")
    cam.sensor_fit = "VERTICAL"
    cam.angle_y = math.radians(room["painterFov"])
    # with a vertical sensor fit, Blender's shift is in frame heights, like Unity's lens shift
    # (checked: both give a projection matrix with m12 = -0.51)
    cam.shift_y = room["painterLensShiftY"]
    cam.clip_start = 0.03; cam.clip_end = 120
    ob = bpy.data.objects.new("Cam_PainterView", cam)
    ob.location = eye
    ob.rotation_euler = (tgt - eye).to_track_quat("-Z", "Y").to_euler()   # a camera looks down -Z, up is Y
    scene.collection.objects.link(ob)
    scene.camera = ob
    scene.render.resolution_x = 1686; scene.render.resolution_y = 1320


# ---------------------------------------------------------------------------------------------
# export: Blender -> data

def part_arrays(ob, relative_to=None):
    """(P, N, U, T) in Unity space, relative to the part's room object, with modifiers applied.
    Vertex i of an untouched mesh stays vertex i; a vertex whose corners disagree on normal or UV is
    split, the extra copies going at the end."""
    parent = relative_to if relative_to is not None else ob.parent
    M = (parent.matrix_world.inverted() if parent else Matrix()) @ ob.matrix_world
    same = untouched_arrays(ob, M)
    if same is not None:
        return same
    deps = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(deps)
    me = ev.to_mesh()
    try:
        me.calc_loop_triangles()
        nv = len(me.vertices)
        co = np.empty(nv * 3, np.float32); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
        nl = len(me.loops)
        lv = np.empty(nl, np.int32); me.loops.foreach_get("vertex_index", lv)
        cn = np.empty(nl * 3, np.float32); me.corner_normals.foreach_get("vector", cn); cn = cn.reshape(-1, 3)
        uvl = me.uv_layers.active
        uv = np.zeros(nl * 2, np.float32)
        if uvl: uvl.data.foreach_get("uv", uv)
        uv = uv.reshape(-1, 2)
        nlt = len(me.loop_triangles)
        tl = np.empty(nlt * 3, np.int32); me.loop_triangles.foreach_get("loops", tl); tl = tl.reshape(-1, 3)
    finally:
        ev.to_mesh_clear()

    identity = np.allclose(np.array(M), np.eye(4), atol=1e-7)
    if not identity:
        R = np.array(M.to_3x3()); Rn = np.array(M.to_3x3().inverted_safe().transposed())
        co = (co.astype(np.float64) @ R.T + np.array(M.translation)).astype(np.float32)
        cn = cn.astype(np.float64) @ Rn.T
        cn = (cn / np.maximum(np.linalg.norm(cn, axis=1, keepdims=True), 1e-9)).astype(np.float32)

    # vertex slots: the first (normal, uv) seen at vertex v keeps index v
    out_of_loop = np.empty(nl, np.int64)
    slot_of = {}
    claimed = set()
    extra = []
    nk = np.round(cn, 4); uk = np.round(uv, 5)
    for l in range(nl):
        v = int(lv[l])
        key = (v, *nk[l], *uk[l])
        slot = slot_of.get(key)
        if slot is None:
            if v not in claimed:
                slot = v; claimed.add(v)
            else:
                slot = nv + len(extra); extra.append(l)
            slot_of[key] = slot
        out_of_loop[l] = slot

    P = np.zeros((nv + len(extra), 3), np.float32); P[:nv] = co
    N = np.zeros((nv + len(extra), 3), np.float32)
    U = np.zeros((nv + len(extra), 2), np.float32)
    # vertices used by no face keep their position and a zero normal
    N[out_of_loop] = cn; U[out_of_loop] = uv
    if extra:
        P[nv:] = co[lv[extra]]
    T = out_of_loop[tl][:, ::-1]                                   # mirror back: reverse winding
    return u2b(P).astype(np.float32), u2b(N).astype(np.float32), U, T.astype(np.int32)


def texture_name(mat):
    if not mat.use_nodes:
        return None
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image is not None:
            return os.path.splitext(n.image.name)[0] if n.image.name.endswith(".png") else n.image.name
    return None


def do_export(data_dir, check_only=False):
    scene = bpy.context.scene
    root = bpy.data.collections.get(ROOT_COLLECTION)
    if root is None:
        raise SystemExit("[room_io] no 'PaintedBedroom' collection: is this the right .blend?")

    objects = sorted([o for o in root.objects if o.type == "EMPTY" and o.parent is None],
                     key=lambda o: (o.get("if_order", 1e9), o.name))
    # materials: original order first, new ones after
    used = []
    for emp in objects:
        for ob in emp.children:
            if ob.type == "MESH":
                used += [m for m in ob.data.materials if m]
                used += [m for c in ob.children for m in (c.data.materials if c.type == "MESH" else []) if m]
    mats = sorted(set(used), key=lambda m: (m.get("if_index", 1e9), m.name))
    if bpy.data.materials.get(INK_MATERIAL) and bpy.data.materials[INK_MATERIAL] not in mats:
        mats.insert(0, bpy.data.materials[INK_MATERIAL])
    mat_index = {m.name: i for i, m in enumerate(mats)}

    textures = list(scene.get("if_textures", []))
    copy_images = []
    mat_rows = []
    for m in mats:
        tn = texture_name(m)
        ti = -1
        if tn:
            if tn not in textures:
                textures.append(tn)
                copy_images.append((tn, m))
            ti = textures.index(tn)
        row = {"name": m.name, "texture": ti}
        if "if_height_tex" in m:
            hn = m["if_height_tex"]
            if hn and hn not in textures:
                textures.append(hn)
            row["height"] = textures.index(hn) if hn else -1
        mat_rows.append({**row,
                         "color": list(m.get("if_color", list(m.diffuse_color)[:3])),
                         "unlit": bool(m.get("if_unlit", False)), "emission": m.get("if_emission", 0),
                         "doubleSided": bool(m.get("if_doubleSided", False)), "outline": bool(m.get("if_outline", False)),
                         "opacity": m.get("if_opacity", 1)})

    floats, ints = [], []
    fpos = ipos = 0
    obj_rows = []
    edited = []

    def add_mesh(name, mat, P, N, U, T):
        nonlocal fpos, ipos
        n = len(P)
        row = {"name": name, "material": mat, "vertexCount": n, "positions": fpos, "normals": fpos + 3 * n,
               "uvs": fpos + 6 * n, "indexCount": int(T.size), "indices": ipos}
        floats.extend([P.ravel(), N.ravel(), U.ravel()]); ints.append(T.ravel())
        fpos += 8 * n; ipos += int(T.size)
        return row

    for emp in objects:
        pos = list(emp.get("if_position", [])) or None
        here = u2b(list(emp.location)).tolist()
        if pos is None or np.abs(np.array(pos) - here).max() > 1e-6:
            pos = here
        rows = []
        parts = sorted([c for c in emp.children if c.type == "MESH"], key=lambda o: (o.get("if_order", 1e9), o.name))
        for ob in parts:
            P, N, U, T = part_arrays(ob, relative_to=emp)
            if not ob.data.materials or ob.data.materials[0] is None:
                raise SystemExit(f"[room_io] {ob.name} has no material")
            rows.append(add_mesh(ob.name, mat_index[ob.data.materials[0].name], P, N, U, T))
            if "ink_width" in ob:
                ink = next((c for c in ob.children if c.name.endswith("_ink")), None)
                changed = ob.get("if_hash") != geom_hash(P, N, U, T)
                if changed:
                    edited.append(ob.name)
                # `ink_rebuild` on a part forces a new outline (e.g. after changing its `ink_width`);
                # the outline's material is whatever its _ink object carries (a per-object ink colour), else Ink_Outline
                if ink is not None and not changed and not ob.get("ink_rebuild", False):
                    Pi, Ni, Ui, Ti = part_arrays(ink, relative_to=emp)
                else:
                    Pi, Ni, Ui, Ti = make_ink(P, N, U, T, float(ob["ink_width"]))
                ink_mat = ink.data.materials[0].name if ink is not None and ink.data.materials and ink.data.materials[0] else INK_MATERIAL
                rows.append(add_mesh(ob.name + "_ink", mat_index[ink_mat], Pi, Ni, Ui, Ti))
        obj_rows.append({"name": emp.name, "position": pos, "meshes": rows})

    markers = []
    mk_c = bpy.data.collections.get(MARKER_COLLECTION)
    for e in (mk_c.objects if mk_c else []):
        pos = list(e.get("if_position", [])) or None
        here = u2b(list(e.location)).tolist()
        if pos is None or np.abs(np.array(pos) - here).max() > 1e-6:
            pos = here
        markers.append({"name": e.name, "position": pos})
    order = ["Marker_PainterView", "Marker_PainterView_Target", "Marker_Spawn"]
    markers.sort(key=lambda m: order.index(m["name"]) if m["name"] in order else 99)

    F = np.concatenate(floats).astype("<f4") if floats else np.zeros(0, "<f4")
    I = np.concatenate(ints).astype("<i4") if ints else np.zeros(0, "<i4")
    room = {"version": scene.get("if_version", 1), "objects": obj_rows, "markers": markers, "materials": mat_rows,
            "textures": textures, "floatCount": int(F.size), "intCount": int(I.size),
            "painterFov": scene.get("if_painterFov"), "painterLensShiftY": scene.get("if_painterLensShiftY")}

    out = tempfile.mkdtemp(prefix="room_io_") if check_only else data_dir
    os.makedirs(os.path.join(out, "Textures"), exist_ok=True)
    with open(os.path.join(out, "room.json"), "w", encoding="utf8") as f:
        f.write(re.sub(r"(\d)e([+-])0(\d)", r"\1e\2\3", json.dumps(js_numbers(room), separators=(",", ":"))))
    with open(os.path.join(out, "room.bytes"), "wb") as f:
        f.write(F.tobytes()); f.write(I.tobytes())
    if not check_only:
        for tn, m in copy_images:
            img = next(n.image for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)
            dst = os.path.join(out, "Textures", tn + ".png")
            src = bpy.path.abspath(img.filepath)
            if os.path.exists(src): shutil.copyfile(src, dst)
            else: img.save(filepath=dst)
    print(f"[room_io] exported {len(obj_rows)} objects, {sum(len(o['meshes']) for o in obj_rows)} meshes, "
          f"{len(mat_rows)} materials, {len(textures)} textures -> {out}")
    print(f"[room_io] parts changed since import (outlines rebuilt): {len(edited)} {edited[:20]}")
    return out


def js_numbers(v):
    """Write numbers the way the web study's exporter (JavaScript) did: 0 not 0.0, 1e-9 not 1e-09."""
    if isinstance(v, dict):
        return {k: js_numbers(x) for k, x in v.items()}
    if isinstance(v, (list, tuple)):
        return [js_numbers(x) for x in v]
    if isinstance(v, float) and v.is_integer():
        return int(v)
    return v


def do_check(data_dir):
    out = do_export(data_dir, check_only=True)
    a, Fa, Ia = read_data(data_dir)
    b, Fb, Ib = read_data(out)
    report = []
    same_struct = [(o["name"], [m["name"] for m in o["meshes"]]) for o in a["objects"]] == \
                  [(o["name"], [m["name"] for m in o["meshes"]]) for o in b["objects"]]
    report.append(f"objects and mesh names identical: {same_struct}")
    report.append(f"materials identical: {a['materials'] == b['materials']}")
    report.append(f"textures identical: {a['textures'] == b['textures']}")
    report.append(f"markers identical: {a['markers'] == b['markers']}")
    report.append(f"object positions identical: {[o['position'] for o in a['objects']] == [o['position'] for o in b['objects']]}")
    if same_struct:
        worst = {"P": 0.0, "N": 0.0, "U": 0.0}
        tri_diff = vc_diff = 0
        for oa, ob_ in zip(a["objects"], b["objects"]):
            for ma, mb in zip(oa["meshes"], ob_["meshes"]):
                if ma["vertexCount"] != mb["vertexCount"]:
                    vc_diff += 1; continue
                Pa, Na, Ua, Ta = mesh_arrays(ma, Fa, Ia); Pb, Nb, Ub, Tb = mesh_arrays(mb, Fb, Ib)
                worst["P"] = max(worst["P"], float(np.abs(Pa - Pb).max(initial=0)))
                worst["N"] = max(worst["N"], float(np.abs(Na - Nb).max(initial=0)))
                worst["U"] = max(worst["U"], float(np.abs(Ua - Ub).max(initial=0)))
                tri_diff += int(not np.array_equal(Ta, Tb))
        report.append(f"meshes with a different vertex count: {vc_diff}; with different triangles: {tri_diff}")
        report.append(f"largest difference: position {worst['P']:.2e} m, normal {worst['N']:.2e}, uv {worst['U']:.2e}")
    for fn in ("room.json", "room.bytes"):
        with open(os.path.join(data_dir, fn), "rb") as f1, open(os.path.join(out, fn), "rb") as f2:
            report.append(f"{fn} byte-identical: {f1.read() == f2.read()}")
    print("[room_io] check against " + data_dir)
    for r in report:
        print("  " + r)
    shutil.rmtree(out, ignore_errors=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        raise SystemExit(__doc__)
    cmd, args = argv[0], argv[1:]
    opt = lambda k, d: args[args.index(k) + 1] if k in args else d
    data_dir = os.path.abspath(opt("--data", DEFAULT_DATA))
    if cmd == "import":
        do_import(data_dir, opt("--blend", DEFAULT_BLEND))
    elif cmd == "export":
        do_export(data_dir)
    elif cmd == "check":
        do_check(data_dir)
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main()
