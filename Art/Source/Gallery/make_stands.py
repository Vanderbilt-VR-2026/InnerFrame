"""The Gallery's stands for the statue and the flowers, modelled in Blender and exported for Unity.

    Blender --background --factory-startup --python Art/Source/Gallery/make_stands.py

Writes Assets/Art/Gallery/Props/GP_Plinth.fbx and GP_FlowerStand.fbx (mesh only; Unity assigns the materials, see
Assets/Art/Gallery/Editor/GalleryDecorSetup.cs). Both: Z up here, origin at the base's centre on the floor, UVs in metres
(each face projected along the axis it faces most), so a tiling texture shows at its real size. Same axes as
build_gallery.py (forward -Z, up Y in the FBX).

GP_Plinth: a classical marble plinth for the statue, 0.70 m square and 0.60 m tall: a bevelled base slab, a step, the
die (the plain block), a cap moulding and a top slab the statue stands on.
GP_FlowerStand: a turned column stand for a vase, its top 0.45 m square at 0.90 m: a square base, a turned column with
rings, a collar and a top plate.
"""
from math import cos, pi, sin
from pathlib import Path

import bmesh
import bpy

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[2] / "Assets/Art/Gallery/Props"

# GP_Plinth: (half width, z bottom, z top, bevel) per tier, bottom to top
PLINTH = [
    (0.350, 0.00, 0.08, 0.012),   # base slab
    (0.320, 0.08, 0.12, 0.010),   # step
    (0.280, 0.12, 0.50, 0.006),   # die
    (0.310, 0.50, 0.54, 0.010),   # cap moulding
    (0.330, 0.54, 0.60, 0.012),   # top slab: the statue stands at 0.60 (GalleryDecorSetup.StatuePlinthH)
]
# GP_FlowerStand
STAND_TOP, STAND_H = 0.225, 0.90          # half the top plate's width; its height (GalleryDecorSetup.VasePlinthH)
STAND_BASE = [(0.200, 0.00, 0.05, 0.008), (0.170, 0.05, 0.08, 0.006)]   # square base, two tiers
COLUMN = [(0.08, 0.095), (0.10, 0.075), (0.13, 0.085), (0.16, 0.070), (0.45, 0.060), (0.74, 0.052),
          (0.77, 0.070), (0.80, 0.058), (0.82, 0.090)]   # (z, radius): turned column, a ring near each end
COLLAR = (0.150, 0.82, 0.86, 0.006)
PLATE = (STAND_TOP, 0.86, STAND_H, 0.008)
SIDES = 12


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def box(h, z0, z1, bevel):
    """A square box, half width h, from z0 to z1, its edges bevelled."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, (z0 + z1) / 2))
    o = bpy.context.active_object
    o.scale = (2 * h, 2 * h, z1 - z0)
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        m = o.modifiers.new("Bevel", "BEVEL"); m.width = bevel; m.segments = 1
        bpy.ops.object.modifier_apply(modifier=m.name)
    return o


def lathe(profile, sides=SIDES):
    bm = bmesh.new()
    rings = [[bm.verts.new((r * cos(2 * pi * k / sides), r * sin(2 * pi * k / sides), z)) for k in range(sides)] for z, r in profile]
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new([a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]])
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    mesh = bpy.data.meshes.new("column")
    bm.to_mesh(mesh); bm.free()
    o = bpy.data.objects.new("column", mesh)
    bpy.context.scene.collection.objects.link(o)
    return o


def finish(name, parts):
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:   # UVs in metres
        n = [abs(c) for c in f.normal]
        a, b = [k for k in range(3) if k != n.index(max(n))]
        for loop in f.loops:
            loop[uv].uv = (loop.vert.co[a], loop.vert.co[b])
    bm.to_mesh(o.data); bm.free()
    for p in o.data.polygons:
        p.use_smooth = False
    o.data.materials.clear()
    o.data.materials.append(bpy.data.materials.new(name))
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    path = OUT / f"{name}.fbx"
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_ALL", apply_unit_scale=True, bake_space_transform=True,
                             path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE")
    dims = tuple(round(v, 3) for v in o.dimensions)
    print(f"{name}: {tris} triangles, {dims} m -> {path.relative_to(HERE.parents[2])}")


clear()
finish("GP_Plinth", [box(*t) for t in PLINTH])
clear()
finish("GP_FlowerStand", [box(*t) for t in STAND_BASE] + [lathe(COLUMN), box(*COLLAR), box(*PLATE)])
