"""The Starry Night's small props, built by script (no inputs). Same FBX bytes every run.

    Blender --background --factory-startup --python make_props.py

SN_Lantern     about 0.32 m: Base, Body (iron frame), Glass (separate, for a transparent material), Cap, Handle (a bail),
               Knob (the wick key, its pivot on its own axis: SN_Lantern turns it as the light dims), and two empties,
               FlameAnchor and GrabPoint (the top of the handle).
SN_Sketchbook  open to 120 degrees, lying on one cover: CoverA (flat), CoverB (lifted 60 degrees), PageA (the sketch on
               top), PageB, GrabPoint (at the spine).
SN_Page        the page Vincent gives you: a sheet with a torn binding edge, the sketch on the front, plain paper behind.
SN_Charcoal    a short, slightly uneven stick of charcoal, GrabPoint at one end.

Blender is Z up; with the team's export settings (sn_blender.export_fbx) Unity gets x' = -x, y' = z, z' = -y.
Writes Assets/StarryNight/Models/SN_<name>.fbx and SN_<name>.blend here (one file each: every prop has a GrabPoint, and
names are unique within a .blend). UVs on the pages and the sheet point at SN_Sketch.png (sketch-page.png): the sketch
for the drawn faces, a blank corner of the paper for the rest.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True   # no __pycache__ next to the sources
sys.path.insert(0, HERE)
import sn_blender as B

REPO = os.path.abspath(os.path.join(HERE, "../../.."))
MODELS = os.path.join(REPO, "Assets/StarryNight/Models")
PAPER_UV = (0.93, 0.965)        # a blank corner of sketch-page.png (top right), for paper without the drawing


def obj(name, bm, mat, location=(0, 0, 0), smooth=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = smooth
    o = bpy.data.objects.new(name, me)
    o.location = location
    bpy.context.scene.collection.objects.link(o)
    return o


def empty(name, location):
    o = bpy.data.objects.new(name, None)
    o.empty_display_size = 0.02
    o.location = location
    bpy.context.scene.collection.objects.link(o)
    return o


def box(bm, x0, x1, y0, y1, z0, z1, matrix=None):
    r = bmesh.ops.create_cube(bm, size=1.0)
    m = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)) @ Matrix.Diagonal((x1 - x0, y1 - y0, z1 - z0, 1))
    if matrix is not None:
        m = matrix @ m
    bmesh.ops.transform(bm, matrix=m, verts=r["verts"])


def cone(bm, r1, r2, depth, seg, matrix):
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth, matrix=matrix)


def uv_planar(o, u_of, v_of, drawn=None):
    """UVs from each loop's vertex position (object space); faces where drawn(p) is False get the blank paper corner."""
    me = o.data
    uv = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        use = drawn(p) if drawn else True
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (u_of(co), v_of(co)) if use else PAPER_UV


# ---------------------------------------------------------------------------------------------------- lantern
def lantern():
    brass = B.material("SN_LanternBrass", (0.62, 0.46, 0.2, 1))
    iron = B.material("SN_LanternIron", (0.08, 0.08, 0.08, 1))
    glass_mat = B.material("SN_LanternGlass", (0.8, 0.85, 0.9, 0.3))

    bm = bmesh.new()
    cone(bm, 0.07, 0.07, 0.022, 16, Matrix.Translation((0, 0, 0.011)))           # plinth
    cone(bm, 0.066, 0.06, 0.008, 16, Matrix.Translation((0, 0, 0.026)))          # its bevelled top
    base = obj("Base", bm, brass, smooth=True)

    bm = bmesh.new()
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(bm, sx * 0.05 - 0.005, sx * 0.05 + 0.005, sy * 0.05 - 0.005, sy * 0.05 + 0.005, 0.03, 0.216)    # posts
    for z in (0.03, 0.206):                                                                               # two rings
        box(bm, -0.055, 0.055, -0.055, -0.047, z, z + 0.01)
        box(bm, -0.055, 0.055, 0.047, 0.055, z, z + 0.01)
        box(bm, -0.055, -0.047, -0.047, 0.047, z, z + 0.01)
        box(bm, 0.047, 0.055, -0.047, 0.047, z, z + 0.01)
    body = obj("Body", bm, iron)

    bm = bmesh.new()   # four panes: an open square tube inside the posts
    c = [(-0.046, -0.046), (0.046, -0.046), (0.046, 0.046), (-0.046, 0.046)]
    lo = [bm.verts.new((x, y, 0.04)) for x, y in c]
    hi = [bm.verts.new((x, y, 0.206)) for x, y in c]
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    glass = obj("Glass", bm, glass_mat)

    bm = bmesh.new()
    cone(bm, 0.075, 0.016, 0.05, 4, Matrix.Translation((0, 0, 0.241)) @ Matrix.Rotation(math.pi / 4, 4, "Z"))   # roof
    cone(bm, 0.016, 0.014, 0.03, 12, Matrix.Translation((0, 0, 0.276)))                                         # chimney
    cone(bm, 0.02, 0.02, 0.004, 12, Matrix.Translation((0, 0, 0.292)))                                          # its rim
    cap = obj("Cap", bm, brass)

    bm = bmesh.new()   # the bail: half a torus over the cap, its ends at the roof's sides
    seg, tube, R, r, zc = 18, 6, 0.05, 0.0035, 0.268
    rings = []
    for k in range(seg + 1):
        a = math.pi * k / seg
        centre = Vector((R * math.cos(a), 0, zc + R * math.sin(a) * 0.95))
        tangent = Vector((-math.sin(a), 0, math.cos(a)))
        n1 = Vector((0, 1, 0))
        n2 = tangent.cross(n1).normalized()
        rings.append([bm.verts.new(centre + r * (math.cos(t) * n1 + math.sin(t) * n2))
                      for t in (2 * math.pi * q / tube for q in range(tube))])
    for k in range(seg):
        for q in range(tube):
            bm.faces.new((rings[k][q], rings[k][(q + 1) % tube], rings[k + 1][(q + 1) % tube], rings[k + 1][q]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    handle = obj("Handle", bm, iron, smooth=True)

    bm = bmesh.new()   # the wick key on the plinth's +x side: a short shaft and a flat grip, round its own x axis
    cone(bm, 0.004, 0.004, 0.014, 8, Matrix.Rotation(math.pi / 2, 4, "Y"))
    box(bm, 0.008, 0.013, -0.011, 0.011, -0.003, 0.003)
    knob = obj("Knob", bm, brass, location=(0.076, 0, 0.012))

    return [base, body, glass, cap, handle, knob, empty("FlameAnchor", (0, 0, 0.1)), empty("GrabPoint", (0, 0, 0.3155))]


# ---------------------------------------------------------------------------------------------------- sketchbook
def sketchbook():
    cover = B.material("SN_Sketchbook", (0.36, 0.24, 0.14, 1))
    sketch = B.material("SN_Sketch", (0.9, 0.85, 0.7, 1))
    W, D, CT, PT, INSET = 0.15, 0.22, 0.004, 0.008, 0.005      # half width, depth (along the spine), cover, pages
    lift = Matrix.Rotation(math.radians(60), 4, "Y")            # the -x half turns up about the spine (y axis)

    bm = bmesh.new(); box(bm, 0.0, W, -D / 2, D / 2, 0.0, CT)
    cover_a = obj("CoverA", bm, cover)
    bm = bmesh.new(); box(bm, -W, 0.0, -D / 2, D / 2, 0.0, CT, lift)
    cover_b = obj("CoverB", bm, cover)
    x0, x1, y0, y1 = INSET * 0.6, W - INSET, -D / 2 + INSET, D / 2 - INSET
    bm = bmesh.new(); box(bm, x0, x1, y0, y1, CT, CT + PT)
    page_a = obj("PageA", bm, sketch)
    bm = bmesh.new(); box(bm, -x1, -x0, y0, y1, CT, CT + PT, lift)
    page_b = obj("PageB", bm, sketch)
    # the sketch on PageA's top: its top edge toward -y (Unity +z, away from a reader at -z), its left toward +x (Unity -x)
    uv_planar(page_a, lambda co: (x1 - co.x) / (x1 - x0), lambda co: (y1 - co.y) / (y1 - y0), drawn=lambda p: p.normal.z > 0.9)
    uv_planar(page_b, lambda co: 0, lambda co: 0, drawn=lambda p: False)
    return [cover_a, cover_b, page_a, page_b, empty("GrabPoint", (0, 0, CT))]


# ---------------------------------------------------------------------------------------------------- page
def page():
    sketch = B.material("SN_Sketch", (0.9, 0.85, 0.7, 1))
    PW, PH = 0.21, 0.297
    offsets = [0.0, 0.004, 0.001, 0.006, 0.002, 0.007, 0.003, 0.005, 0.0, 0.006, 0.002, 0.008, 0.003, 0.005, 0.001,
               0.007, 0.002, 0.004, 0.006, 0.001, 0.005, 0.003, 0.0]
    torn = [(-PW / 2 + o, -PH / 2 + PH * i / (len(offsets) - 1)) for i, o in enumerate(offsets)]   # the binding edge
    outline = [(PW / 2, -PH / 2), (PW / 2, PH / 2)] + list(reversed(torn))
    bm = bmesh.new()
    front = [bm.verts.new((x, y, 0.0005)) for x, y in outline]
    back = [bm.verts.new((x, y, -0.0005)) for x, y in outline]
    f = bm.faces.new(front)
    g = bm.faces.new(list(reversed(back)))
    bmesh.ops.triangulate(bm, faces=[f, g])
    for face in bm.faces:   # front faces up, back faces down
        if (face.normal.z > 0) != (face.calc_center_median().z > 0):
            face.normal_flip()
    sheet = obj("Sheet", bm, sketch)
    uv_planar(sheet, lambda co: (PW / 2 - co.x) / PW, lambda co: (PH / 2 - co.y) / PH, drawn=lambda p: p.normal.z > 0)
    return [sheet]


# ---------------------------------------------------------------------------------------------------- charcoal
def charcoal():
    mat = B.material("SN_Charcoal", (0.05, 0.05, 0.05, 1))
    length, rad = 0.075, 0.0055
    wobble = [1.0, 0.86, 1.08, 0.93, 1.04, 0.9]
    bm = bmesh.new()
    rings = []
    for k, x in enumerate((-length / 2, -length / 6, length / 6, length / 2)):
        taper = 0.82 if k == 0 else 1.0                       # one end worn down
        rings.append([bm.verts.new((x, rad * taper * wobble[(q + k) % 6] * math.cos(2 * math.pi * q / 6),
                                    rad * taper * wobble[(q + 2 * k) % 6] * math.sin(2 * math.pi * q / 6))) for q in range(6)])
    for k in range(3):
        for q in range(6):
            bm.faces.new((rings[k][q], rings[k][(q + 1) % 6], rings[k + 1][(q + 1) % 6], rings[k + 1][q]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[3])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return [obj("Stick", bm, mat), empty("GrabPoint", (length / 2, 0, 0))]


# ---------------------------------------------------------------------------------------------------- export
for name, build in (("SN_Lantern", lantern), ("SN_Sketchbook", sketchbook), ("SN_Page", page), ("SN_Charcoal", charcoal)):
    B.clear_scene()
    group = build()
    tris = sum(B.triangles(o) for o in group if o.type == "MESH")
    print("PROPS|%s|%d triangles|%s" % (name, tris, ",".join(o.name for o in group)))
    B.export_fbx(os.path.join(MODELS, name + ".fbx"), group)
    B.save_blend(os.path.join(HERE, name + ".blend"))
