"""Build the InnerFrame gallery shell in Blender and export it for Unity.

Run from the repo root (Blender 5.2, no window):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
        --python Art/Source/Gallery/build_gallery.py

Writes Art/Source/Gallery/gallery.blend and Assets/Art/Gallery/Gallery.fbx.
Every run deletes the old scene first, so running it twice gives the same result.

2026-11-10: the room shrinks to 7 x 9 x 4 m and follows the team's concept sheet: red walls over a grey wood
wainscot (dado) with a cap rail, a cornice, a coffered skylight in the middle of the ceiling, an arched entrance
in the south wall with a closed door in it, and one upholstered bench on a rug in the middle. The surfaces take
ChatGPT textures (docs/gallery/TEXTURE_PROMPTS.md): walls, floor and wood repeat by the metre (UVs in metres); the
rug and the door are mapped once, edge to edge.
"""
from math import atan2, cos, exp, pi, radians, sin
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

# ---------------------------------------------------------------------------
# Parameters. 1 Blender unit = 1 m. North = +Y, East = +X, up = +Z.
# (In Unity: x = -X, z = -Y. The entrance is in the south wall; The Bedroom hangs on the north wall.)
# ---------------------------------------------------------------------------
ROOM_LENGTH = 9.0    # inside, south wall to north wall (Y)
ROOM_WIDTH = 7.0     # inside, west wall to east wall (X)
ROOM_HEIGHT = 4.0    # inside, floor to ceiling
WALL_T = 0.2         # thickness of the walls and the floor
CEILING_T = 0.4      # ceiling thickness, which is also the depth of the skylight

# The arched entrance in the middle of Wall_South, closed by a door set at the wall's outer face.
ARCH_W, ARCH_SPRING = 1.6, 1.8      # opening width; height where the arch starts to curve
ARCH_R = ARCH_W / 2                 # a round (semicircular) arch: crown at ARCH_SPRING + ARCH_R = 2.6 m
ARCH_SEGMENTS = 16                  # straight pieces in the curve
ARCH_TRIM_W, ARCH_TRIM_D = 0.15, 0.05   # the moulding round the opening, and how far it stands out
DOOR_T = 0.05
SPAWN_INSET = 1.0                   # how far inside the entrance Spawn_Point stands

# TEAM DECISION: the size and height of the painting the group walks through.
PAINTING_W = 3.0        # opening width  } keeps the landscape ratio of
PAINTING_H = 2.4        # opening height } The Bedroom (about 91 x 72 cm)
PAINTING_BOTTOM = 0.8   # floor to the bottom edge of the opening (hung like a painting)
FRAME_W, FRAME_D = 0.18, 0.12   # frame border width, and how far it stands off the wall

# Placard stand (Pedestal_Bedroom), to the right of the painting.
PEDESTAL_H = 1.0             # floor to the high (back) edge of the top
PEDESTAL_TOP = (0.5, 0.35)   # reading surface: width, and length down the slope
PEDESTAL_TILT = 20           # degrees the top slopes down toward the viewer
PEDESTAL_OUT = 1.5           # painting wall to the centre of the pedestal
PEDESTAL_GAP = 0.1           # frame's right edge to the side of the pedestal
# The stand: rounded-rectangle rings (RR_SEG segments per corner) for its base and head, a turned column between.
RR_SEG, RR_BASE, RR_HEAD = 4, 0.06, 0.035      # segments per corner; the base's and the head's corner radius
STAND_BASE_PROFILE = [(0.000, 0.000), (0.000, 0.035), (0.006, 0.048), (0.018, 0.054),   # (inset, height): plinth,
                      (0.040, 0.058), (0.050, 0.070), (0.062, 0.082), (0.070, 0.086)]     # rounded edge, cove, step
STAND_SIDES = 12                                # the column's sides
STAND_COLUMN = [(0.00, 0.090), (0.03, 0.098), (0.06, 0.076), (0.14, 0.064), (0.70, 0.052),   # (0..1 up, radius):
                (0.88, 0.048), (0.91, 0.066), (0.95, 0.055), (1.00, 0.075)]                    # foot, shaft, collar
STAND_HEAD_Z, STAND_HEAD_EASE = 0.80, 0.05       # where the column meets the head; how far its underside is eased in

# The Starry Night's frame on the east wall (placed in Unity by SN_GallerySetup, RootPosition z = -1.6, so its
# centre is at Y = 1.6 here). Only used to leave its gap in the wainscot.
SN_FRAME_Y = 1.6

GATHER_DEPTH = 3.5      # clear area in front of The Bedroom (kept free of benches)
SKYLIGHT_W, SKYLIGHT_D = 3.0, 2.5   # ceiling opening, along X and along Y, in the middle of the room
SKYLIGHT_PANES = (4, 3)  # gaps between the bars, along X and along Y
BAR = 0.06              # skylight bar width and depth
SKYLIGHT_TRIM = 0.18    # the moulded frame round the skylight on the ceiling

WAINSCOT_H, WAINSCOT_D = 1.0, 0.025          # panelling from the floor, and how far it stands off the wall
RAIL_H, RAIL_D = 0.06, 0.045                 # cap rail along the wainscot's top
CORNICE_H, CORNICE_D = 0.25, 0.12            # band where the walls meet the ceiling
BASEBOARD_H, BASEBOARD_D = 0.12, 0.04        # in front of the wainscot

# One bench on the rug in the middle, facing The Bedroom: an upholstered top on a dark wood frame with turned legs.
BENCH_L, BENCH_D, BENCH_H = 1.6, 0.55, 0.48
BENCH_AT = (0.0, -1.2)   # centre; Unity z 1.2
BENCH_ALONG_ROOM = True  # long side north-south, along the rug: 0.55 m across the room, so the floor in front of
                         # The Starry Night's pedestal and the walk past it stay clear
CUSHION_T, APRON_H, LEG_INSET = 0.10, 0.08, 0.07
LEG_PROFILE = [(0.00, 0.022), (0.03, 0.030), (0.07, 0.020), (0.18, 0.028), (0.24, 0.034), (0.30, 0.034)]   # (z, radius)
RUG = (2.0, 3.0)        # along X, along Y; centred under the bench
RUG_T = 0.008

COLORS = {  # plain colours, linear RGB (the numbers Blender's colour picker shows); Unity has its own materials
    "Mat_Wall": (0.32, 0.06, 0.04),        # deep Pompeian red
    "Mat_Ceiling": (0.45, 0.38, 0.30),     # warm grey-beige
    "Mat_Wainscot": (0.30, 0.27, 0.23),    # weathered grey wood
    "Mat_Trim": (0.55, 0.45, 0.33),        # warm stone / pale wood: the arch's moulding, the skylight's frame
    "Mat_Cornice": (0.55, 0.45, 0.33),     # the same, as a moulding profile mapped once up the cornice
    "Mat_Rail": (0.55, 0.45, 0.33),        # the same, mapped once up the wainscot's rail
    "Mat_Floor": (0.30, 0.13, 0.05),       # honey oak
    "Mat_Rug": (0.30, 0.05, 0.05),         # red rug
    "Mat_BenchWood": (0.05, 0.025, 0.012), # dark walnut
    "Mat_Cushion": (0.12, 0.06, 0.04),     # dark leather
    "Mat_Door": (0.10, 0.12, 0.06),        # olive-painted wood
    "Mat_Bench": (0.01, 0.01, 0.01),       # near-black (the pedestals)
    "Mat_Frame": (0.17, 0.10, 0.025),      # dark gold
    "Mat_Metal": (0.05, 0.05, 0.05),       # dark grey, skylight bars, door handles
    "Mat_Baseboard": (0.13, 0.05, 0.02),   # dark wood
}

HERE = Path(__file__).resolve().parent                       # Art/Source/Gallery
BLEND_PATH = HERE / "gallery.blend"
FBX_PATH = HERE.parents[2] / "Assets/Art/Gallery/Gallery.fbx"  # parents[2] = repo root

def clear_scene():
    """Delete everything a previous run made, so each run starts from empty."""
    for data in (bpy.data.objects, bpy.data.meshes, bpy.data.materials, bpy.data.collections):
        for block in list(data):
            data.remove(block)


def add_box(bm, axes, u, v, w, holes=()):
    """Add a box to the bmesh bm, with rectangular holes cut straight through it.

    The box is drawn flat in a (u, v) plane, then given thickness along w.
    axes names the world axis used for u, v and w: "xzy" is a wall that runs
    along X, rises along Z and is thick along Y. u, v and w are (min, max)
    pairs; each hole is (u_min, u_max, v_min, v_max).
    """
    # Cut the plane into a grid along every edge of the box and of the holes...
    us = sorted({*u, *(h[k] for h in holes for k in (0, 1))})
    vs = sorted({*v, *(h[k] for h in holes for k in (2, 3))})

    def point(a, b, c):  # (u, v, w) -> (x, y, z)
        xyz = dict(zip(axes, (a, b, c)))
        return xyz["x"], xyz["y"], xyz["z"]

    front = [[bm.verts.new(point(a, b, w[0])) for b in vs] for a in us]
    back = [[bm.verts.new(point(a, b, w[1])) for b in vs] for a in us]
    # ...and keep every grid cell whose centre is not inside a hole.
    solid = {(i, j) for i in range(len(us) - 1) for j in range(len(vs) - 1)
             if not any(h[0] < (us[i] + us[i + 1]) / 2 < h[1] and
                        h[2] < (vs[j] + vs[j + 1]) / 2 < h[3] for h in holes)}
    for i, j in sorted(solid):  # sorted, so every run builds faces in the same order
        corners = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
        bm.faces.new([front[a][b] for a, b in corners])
        bm.faces.new([back[a][b] for a, b in corners])
        # A side face on each cell edge with no solid cell beyond it
        # (edges in order: bottom, right, top, left).
        for k, beyond in enumerate([(i, j - 1), (i + 1, j), (i, j + 1), (i - 1, j)]):
            if beyond not in solid:
                (a, b), (c, d) = corners[k], corners[(k + 1) % 4]
                bm.faces.new([front[a][b], front[c][d], back[c][d], back[a][b]])


def make(name, material, *boxes, centred=False):
    """Build one mesh object from one or more boxes (each box = add_box arguments)."""
    bm = bmesh.new()
    for box in boxes:
        add_box(bm, *box)
    finish(name, material, bm, centred)


def finish(name, material, bm, centred=False):
    """Turn the bmesh bm (vertices in world space) into a mesh object.

    By default the object stays at the world origin with no rotation or scale,
    so all transforms are already applied. centred=True moves its origin to
    the centre of its bounding box instead, still with no rotation or scale.
    """
    # Drop grid points that no face uses (points that fell inside a hole).
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)  # every face points outward
    # UVs in metres (1 UV unit = 1 m): each face is projected along the
    # axis it faces most, e.g. the floor's top face gets UV = (x, y).
    uv = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        n = [abs(c) for c in face.normal]
        a, b = [k for k in range(3) if k != n.index(max(n))]
        for loop in face.loops:
            loop[uv].uv = (loop.vert.co[a], loop.vert.co[b])
    centre = (0, 0, 0)
    if centred:  # shift the vertices so the bounding-box centre sits at the origin
        lo = [min(v.co[k] for v in bm.verts) for k in range(3)]
        hi = [max(v.co[k] for v in bm.verts) for k in range(3)]
        centre = [(a + b) / 2 for a, b in zip(lo, hi)]
        bmesh.ops.translate(bm, vec=[-c for c in centre], verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    obj.location = centre  # ...and the object moves there, so nothing changes in the room
    bpy.context.scene.collection.objects.link(obj)


def empty(name, location, turn_deg=0, up_deg=0):
    """An attachment point for Unity. After export, its -Y axis is Unity's forward (+Z)
    and its +Z axis is Unity's up (+Y). up_deg tips the forward axis up from
    horizontal; turn_deg then turns it around the vertical."""
    obj = bpy.data.objects.new(name, None)
    obj.location = location
    obj.rotation_euler = (radians(-up_deg), 0, radians(turn_deg))
    obj.empty_display_type = "ARROWS"
    bpy.context.scene.collection.objects.link(obj)


def uv_fit(obj):
    """UVs 0..1 across the object, projected along each face's main axis: for pictures mapped once (rug, door)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.verify()
    lo = [min(v.co[k] for v in bm.verts) for k in range(3)]
    hi = [max(v.co[k] for v in bm.verts) for k in range(3)]
    for face in bm.faces:
        n = [abs(c) for c in face.normal]
        a, b = [k for k in range(3) if k != n.index(max(n))]
        for loop in face.loops:
            loop[uv].uv = ((loop.vert.co[a] - lo[a]) / (hi[a] - lo[a] or 1), (loop.vert.co[b] - lo[b]) / (hi[b] - lo[b] or 1))
    bm.to_mesh(obj.data)
    bm.free()


def prism_xz(bm, outline, y_range, fan_from=None):
    """A flat shape in the XZ plane (points in order round it), made thick along Y. Its faces are triangles fanned
    from fan_from (a point that sees the whole outline), or from the outline's centre if it is convex."""
    c = fan_from or (sum(p[0] for p in outline) / len(outline), sum(p[1] for p in outline) / len(outline))
    for y in y_range:
        hub = bm.verts.new((c[0], y, c[1]))
        ring = [bm.verts.new((x, y, z)) for x, z in outline]
        for i in range(len(ring)):
            j = (i + 1) % len(ring)
            if fan_from and (outline[i] == tuple(fan_from) or outline[j] == tuple(fan_from)):
                continue
            bm.faces.new([hub, ring[i], ring[j]])
        if y == y_range[0]:
            first = ring
        else:
            for i in range(len(ring)):
                j = (i + 1) % len(ring)
                bm.faces.new([first[i], first[j], ring[j], ring[i]])
    return bm


def arc(cx, cz, r, a0, a1, n):
    return [(cx + r * cos(a0 + (a1 - a0) * k / n), cz + r * sin(a0 + (a1 - a0) * k / n)) for k in range(n + 1)]


def lathe(bm, cx, cy, profile, sides=10):
    """A turned leg: rings at the profile's heights, joined, with caps."""
    rings = [[bm.verts.new((cx + r * cos(2 * pi * k / sides), cy + r * sin(2 * pi * k / sides), z)) for k in range(sides)]
             for z, r in profile]
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new([a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]])
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])


def join(name, parts):
    """Join objects into one mesh object named name (their materials kept as slots), origin at its box centre."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = obj.data.name = name
    return obj


FRAME_PROFILE = [   # (out from the opening's edge, depth off the wall), inner at the wall round to outer at the wall
    (0.000, 0.000), (0.000, 0.085), (0.006, 0.095), (0.014, 0.100),               # sight step
    (0.020, 0.108), (0.026, 0.113), (0.032, 0.108),                               # inner bead
    (0.038, 0.098), (0.046, 0.096),                                               # hollow
    (0.052, 0.104), (0.110, 0.112),                                               # broad flat band
    (0.118, 0.122), (0.128, 0.136), (0.140, 0.146), (0.154, 0.150), (0.166, 0.144), (0.175, 0.130),  # rounded rim
    (0.180, 0.112), (0.180, 0.000),
]
TUBE_SIDES = 6     # the C-scrolls' cross-section


def ellipsoid(name, centre, radii, rot_y=0.0, seg=6, rings=3):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=centre)
    o = bpy.context.active_object
    o.name = name
    o.scale = radii
    o.rotation_euler = (0, rot_y, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return o


def tube(points, radii, name):
    """A tube along points (x, y, z) in the frame's plane, its radius per point, capped."""
    bm = bmesh.new()
    rings = []
    for i, (pt, r) in enumerate(zip(points, radii)):
        a, b = Vector(points[max(i - 1, 0)]), Vector(points[min(i + 1, len(points) - 1)])
        t = (b - a).normalized()
        n = t.cross(Vector((0, 1, 0))).normalized()   # across the tube, in the frame's plane
        up = Vector((0, -1, 0))                         # towards the room
        rings.append([bm.verts.new(Vector(pt) + r * (cos(2 * pi * k / TUBE_SIDES) * n + sin(2 * pi * k / TUBE_SIDES) * up))
                      for k in range(TUBE_SIDES)])
    for p, q in zip(rings, rings[1:]):
        for k in range(TUBE_SIDES):
            bm.faces.new([p[k], p[(k + 1) % TUBE_SIDES], q[(k + 1) % TUBE_SIDES], q[k]])
    bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.loops.layers.uv.new("UVMap")
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    o = bpy.data.objects.new(name, mesh); bpy.context.scene.collection.objects.link(o)
    return o


def scroll(to_xz, y):
    """A C-scroll in a rail's own coordinates (u along the rail from the corner, w across it, + outward): a tapering
    tail along the rail, then a volute curling outward first, round towards the corner and in."""
    pts, rad = [], []
    for k in range(5):
        f = k / 4
        pts.append((0.36 - f * (0.36 - 0.19), -0.03 * sin(pi * f))); rad.append(0.010 + 0.020 * f)
    C, R0, turns, n = (0.13, 0.0), 0.058, 1.15, 12
    for k in range(1, n + 1):
        f = k / n
        th = 2 * pi * turns * f
        r = R0 * exp(-1.3 * f * turns)
        pts.append((C[0] + r * cos(th), C[1] + r * sin(th))); rad.append(0.030 * (1 - 0.5 * f))
    return tube([(to_xz(u, w)[0], y, to_xz(u, w)[1]) for u, w in pts], rad, "scroll")


def build_frame():
    """Frame_Bedroom (2026-11-10, after the team's baroque reference): FRAME_PROFILE swept round the opening with mitred
    corners, and at each corner a cartouche: a raised cushion, a C-scroll along each rail, a fan of five acanthus leaves
    and a boss on the mitre. About 2,600 triangles; 0.18 m wide, so its outer edge (the wainscot's gaps, Vincent's
    portrait, The Starry Night's checks) is unchanged. The gilt (GT_Frame, 3:1) runs along each rail: u along it, one
    image width per 3x the profile's arc length, v up the profile from the inner edge (0) to the outer (1). Each ornament
    takes a patch of the gilt's middle band and is smooth-shaded."""
    w2, h2, zc = PAINTING_W / 2, PAINTING_H / 2, PAINTING_BOTTOM + PAINTING_H / 2
    arc = [0.0]
    for (o0, d0), (o1, d1) in zip(FRAME_PROFILE, FRAME_PROFILE[1:]):
        arc.append(arc[-1] + ((o1 - o0) ** 2 + (d1 - d0) ** 2) ** 0.5)
    S = arc[-1]
    repeat = 3.0 * S
    rect = lambda o: [(-(w2 + o), -(h2 + o)), (w2 + o, -(h2 + o)), (w2 + o, h2 + o), (-(w2 + o), h2 + o)]
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    for side in range(4):                                  # bottom, right, top, left: each rail its own vertices
        rows = []
        for (o, d), t in zip(FRAME_PROFILE, arc):
            r = rect(o)
            (xa, za), (xb, zb) = r[side], r[(side + 1) % 4]
            rows.append(((bm.verts.new((xa, y1 - d, zc + za)), bm.verts.new((xb, y1 - d, zc + zb))), (xa, za), (xb, zb), t / S))
        horiz = side in (0, 2)
        for (pv, pa, pb, pt), (qv, qa, qb, qt) in zip(rows, rows[1:]):
            f = bm.faces.new([pv[0], pv[1], qv[1], qv[0]])
            for loop, (pos, v) in zip(f.loops, ((pa, pt), (pb, pt), (qb, qt), (qa, qt))):
                loop[uvl].uv = ((pos[0] if horiz else pos[1]) / repeat, v)
            c = f.calc_center_median()                     # outward: away from the middle of this rail's cross-section
            inner = Vector((c.x, y1 - 0.06, c.z))
            if horiz: inner.z = zc + (1 if side == 2 else -1) * (h2 + FRAME_W / 2)
            else: inner.x = (1 if side == 1 else -1) * (w2 + FRAME_W / 2)
            f.normal_update()
            if f.normal.dot(c - inner) < 0: f.normal_flip()
    mesh = bpy.data.meshes.new("Frame_Bedroom")
    bm.to_mesh(mesh); bm.free()
    mesh.materials.append(mat["Mat_Frame"])
    frame = bpy.data.objects.new("Frame_Bedroom", mesh)
    bpy.context.scene.collection.objects.link(frame)

    parts = []
    mid = FRAME_W / 2
    for sx in (-1, 1):
        for sz in (-1, 1):
            cx, cz = sx * (w2 + mid), zc + sz * (h2 + mid)   # the corner, mid-band
            along_h = lambda u, w, sx=sx, sz=sz, cx=cx, cz=cz: (cx - sx * u, cz + sz * w)   # horizontal rail, w outward
            along_v = lambda u, w, sx=sx, sz=sz, cx=cx, cz=cz: (cx + sx * w, cz - sz * u)   # vertical rail
            parts += [scroll(along_h, y1 - 0.128), scroll(along_v, y1 - 0.128)]
            parts.append(ellipsoid("cushion", (cx, y1 - 0.118, cz), (0.105, 0.03, 0.105), seg=8, rings=4))
            for k, ang in enumerate((pi / 4, pi / 4 - 0.55, pi / 4 + 0.55, -0.05, pi / 2 + 0.05)):
                d = Vector((sx * cos(ang), 0, sz * sin(ang)))
                big = 1.0 if k == 0 else 0.8 if k < 3 else 0.65
                c = Vector((cx, y1 - 0.142, cz)) + d * 0.06 * big
                parts.append(ellipsoid("leaf", c, (0.06 * big, 0.022, 0.026 * big), rot_y=-atan2(d.z, d.x)))
            parts.append(ellipsoid("boss", (cx, y1 - 0.158, cz), (0.034, 0.03, 0.034), seg=6, rings=4))
    for n, o in enumerate(parts):
        o.data.materials.clear()
        o.data.materials.append(mat["Mat_Frame"])
        bm2 = bmesh.new(); bm2.from_mesh(o.data)
        uv = bm2.loops.layers.uv.verify()
        xs, zs = [v.co.x for v in bm2.verts], [v.co.z for v in bm2.verts]
        for f in bm2.faces:
            f.smooth = True
            for loop in f.loops:
                co = loop.vert.co
                loop[uv].uv = ((co.x - min(xs)) / repeat + 0.137 * n, 0.3 + 0.4 * (co.z - min(zs)) / ((max(zs) - min(zs)) or 1))
        bm2.to_mesh(o.data); bm2.free()
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    frame.select_set(True)
    bpy.context.view_layer.objects.active = frame
    bpy.ops.object.join()

clear_scene()
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
mat = {}
for name, rgb in COLORS.items():
    mat[name] = bpy.data.materials.new(name)
    mat[name].diffuse_color = (*rgb, 1)  # viewport colour
    mat[name].node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)

# Inside faces of the walls, and the outer footprint including the walls.
x0, x1 = -ROOM_WIDTH / 2, ROOM_WIDTH / 2     # west, east
y0, y1 = -ROOM_LENGTH / 2, ROOM_LENGTH / 2   # south (entrance), north (painting)
H, T = ROOM_HEIGHT, WALL_T
outer_x, outer_y = (x0 - T, x1 + T), (y0 - T, y1 + T)
assert BENCH_AT[1] + (BENCH_L if BENCH_ALONG_ROOM else BENCH_D) / 2 <= y1 - GATHER_DEPTH, "bench in the gathering area"
assert ARCH_SPRING + ARCH_R + ARCH_TRIM_W < H - CORNICE_H, "the arch reaches the cornice"

# Floor and ceiling; the skylight in the middle of the ceiling, with a moulded frame round it.
make("Floor", mat["Mat_Floor"], ("xyz", outer_x, outer_y, (-T, 0)))
sky = (-SKYLIGHT_W / 2, SKYLIGHT_W / 2, -SKYLIGHT_D / 2, SKYLIGHT_D / 2)
make("Ceiling", mat["Mat_Ceiling"], ("xyz", outer_x, outer_y, (H, H + CEILING_T), [sky]))
st = SKYLIGHT_TRIM
make("Skylight_Trim", mat["Mat_Trim"], ("xyz", (sky[0] - st, sky[1] + st), (sky[2] - st, sky[3] + st), (H - 0.06, H), [sky]))

# Walls. East and west run the full outer length so the corners close. The south wall has a rectangular opening
# as tall as the arch's crown; the two spandrels fill its top corners outside the curve.
crown = ARCH_SPRING + ARCH_R
door = (-ARCH_W / 2, ARCH_W / 2, 0, crown)
make("Wall_North", mat["Mat_Wall"], ("xzy", (x0, x1), (0, H), (y1, y1 + T)))  # solid behind the painting
make("Wall_South", mat["Mat_Wall"], ("xzy", (x0, x1), (0, H), (y0 - T, y0), [door]))
make("Wall_East", mat["Mat_Wall"], ("yzx", outer_y, (0, H), (x1, x1 + T)))
make("Wall_West", mat["Mat_Wall"], ("yzx", outer_y, (0, H), (x0 - T, x0)))
bm = bmesh.new()
for side in (1, -1):
    corner = (side * ARCH_R, crown)
    curve = arc(0, ARCH_SPRING, ARCH_R, 0 if side > 0 else pi, pi / 2, ARCH_SEGMENTS // 2)
    outline = [corner] + curve if side > 0 else [corner] + curve
    prism_xz(bm, outline, (y0 - T, y0), fan_from=corner)
finish("Arch_Spandrels", mat["Mat_Wall"], bm)

# The moulding round the arch, on the room side: two pilasters up to the spring and a band round the curve.
bm = bmesh.new()
tw, td = ARCH_TRIM_W, ARCH_TRIM_D
for side in (1, -1):
    xa, xb = sorted((side * ARCH_R, side * (ARCH_R + tw)))
    add_box(bm, "xzy", (xa, xb), (0, ARCH_SPRING), (y0, y0 + td))
inner = arc(0, ARCH_SPRING, ARCH_R, 0, pi, ARCH_SEGMENTS)
outer = arc(0, ARCH_SPRING, ARCH_R + tw, 0, pi, ARCH_SEGMENTS)
fi = [bm.verts.new((x, y0 + td, z)) for x, z in inner]; fo = [bm.verts.new((x, y0 + td, z)) for x, z in outer]
bi = [bm.verts.new((x, y0, z)) for x, z in inner]; bo = [bm.verts.new((x, y0, z)) for x, z in outer]
for k in range(ARCH_SEGMENTS):
    bm.faces.new([fi[k], fo[k], fo[k + 1], fi[k + 1]])      # front
    bm.faces.new([fo[k], bo[k], bo[k + 1], fo[k + 1]])      # outer edge
    bm.faces.new([bi[k], fi[k], fi[k + 1], bi[k + 1]])      # inner edge (the soffit's lip)
finish("Arch_Moulding", mat["Mat_Trim"], bm)

# The door: one slab the shape of the opening, at the wall's outer face, seen down the 0.2 m reveal; its picture is
# mapped once, edge to edge (uv_fit). Two brass handles on the room side.
bm = bmesh.new()
door_outline = [(-ARCH_R, 0.0), (ARCH_R, 0.0)] + arc(0, ARCH_SPRING, ARCH_R, 0, pi, ARCH_SEGMENTS)
prism_xz(bm, door_outline, (y0 - T, y0 - T + DOOR_T), fan_from=(0.0, ARCH_SPRING))
finish("Door", mat["Mat_Door"], bm)
uv_fit(bpy.data.objects["Door"])
dz, dy = 1.0, y0 - T + DOOR_T
make("Door_Handles", mat["Mat_Metal"], ("xzy", (-0.14, -0.06), (dz - 0.02, dz + 0.02), (dy, dy + 0.06)),
     ("xzy", (0.06, 0.14), (dz - 0.02, dz + 0.02), (dy, dy + 0.06)))

# Picture frame around the painting opening, standing FRAME_D off the north wall.
pz0, pz1 = PAINTING_BOTTOM, PAINTING_BOTTOM + PAINTING_H
fx = PAINTING_W / 2 + FRAME_W
assert pz0 - FRAME_W >= BASEBOARD_H, "frame's bottom rail must clear the baseboard"
# 2026-11-10: a carved baroque frame (see build_frame), instead of a plain box and the Tripo corner ornaments.
build_frame()

# Wainscot, cap rail, cornice and baseboard: strips on the walls' inside faces. The wainscot and its rail stop at
# the two paintings' frames (which stand on the wall from 0.62 m) and at the arch's moulding.
fz = pz0 - FRAME_W - 0.03                 # frames' bottom edge, less the corner cartouches' overhang
gx = fx + 0.03                            # the frames' corner cartouches stand 1.5 cm past their outer edge
sn = (SN_FRAME_Y - gx, SN_FRAME_Y + gx)   # The Starry Night's frame along the east wall
gap = ARCH_R + ARCH_TRIM_W                # arch moulding's outer edge
wd, rd = WAINSCOT_D, RAIL_D
def strips(z0, z1, d, cut_frames):
    north = [(-gx, gx, max(z0, fz), z1)] if cut_frames and z1 > fz else []
    east = [(sn[0], sn[1], max(z0, fz), z1)] if cut_frames and z1 > fz else []
    return [
        ("xzy", (x0, x1), (z0, z1), (y1 - d, y1), north),
        ("xzy", (x0, x1), (z0, z1), (y0, y0 + d), [(-gap, gap, z0, z1)] if z0 < crown + ARCH_TRIM_W else []),
        ("yzx", (y0 + d, y1 - d), (z0, z1), (x1 - d, x1), east),
        ("yzx", (y0 + d, y1 - d), (z0, z1), (x0, x0 + d)),
    ]
make("Wainscot", mat["Mat_Wainscot"], *strips(0, WAINSCOT_H, wd, True))
make("Wainscot_Rail", mat["Mat_Rail"], *strips(WAINSCOT_H - RAIL_H / 2, WAINSCOT_H + RAIL_H / 2, rd, True))
make("Cornice", mat["Mat_Cornice"], *strips(H - CORNICE_H, H, CORNICE_D, False))
make("Baseboard", mat["Mat_Baseboard"], *strips(0, BASEBOARD_H, BASEBOARD_D, False))

# Skylight bars: a grid at the top of the skylight opening; the panes are holes.
nx, ny = SKYLIGHT_PANES
pw = (SKYLIGHT_W - (nx + 1) * BAR) / nx   # pane size along X
pd = (SKYLIGHT_D - (ny + 1) * BAR) / ny   # pane size along Y
panes = [(sky[0] + BAR + i * (pw + BAR), sky[0] + BAR + i * (pw + BAR) + pw,
          sky[2] + BAR + j * (pd + BAR), sky[2] + BAR + j * (pd + BAR) + pd)
         for i in range(nx) for j in range(ny)]
make("Skylight_Grid", mat["Mat_Metal"],
     ("xyz", sky[:2], sky[2:], (H + CEILING_T - BAR, H + CEILING_T), panes))

# The bench (Bench_01): an upholstered top with rounded edges, an apron and four turned legs. Its origin is its own
# centre, so it can be moved on its own in Unity.
bx, by = BENCH_AT
L, D, top = BENCH_L, BENCH_D, BENCH_H
bpy.ops.mesh.primitive_cube_add(size=1, location=(bx, by, top - CUSHION_T / 2))
cushion = bpy.context.active_object
cushion.scale = (L, D, CUSHION_T)
bpy.ops.object.transform_apply(scale=True)
bev = cushion.modifiers.new("Bevel", "BEVEL"); bev.width = 0.03; bev.segments = 3
bpy.ops.object.modifier_apply(modifier=bev.name)
cushion.data.materials.append(mat["Mat_Cushion"])
bm = bmesh.new()
az = top - CUSHION_T
add_box(bm, "xyz", (bx - L / 2 + 0.03, bx + L / 2 - 0.03), (by - D / 2 + 0.03, by + D / 2 - 0.03), (az - APRON_H, az))
leg_top = az - APRON_H
scale = leg_top / LEG_PROFILE[-1][0]
for sx in (-1, 1):
    for sy in (-1, 1):
        lathe(bm, bx + sx * (L / 2 - LEG_INSET), by + sy * (D / 2 - LEG_INSET), [(z * scale, r) for z, r in LEG_PROFILE])
add_box(bm, "xyz", (bx - L / 2 + LEG_INSET, bx + L / 2 - LEG_INSET), (by - 0.012, by + 0.012), (0.10, 0.125))   # stretcher
finish("Bench_Frame", mat["Mat_BenchWood"], bm)
bench = join("Bench_01", [bpy.data.objects["Bench_Frame"], cushion])
bm = bmesh.new(); bm.from_mesh(bench.data)                       # UVs in metres for the joined bench, then centre it
uv = bm.loops.layers.uv.verify()
for face in bm.faces:
    n = [abs(c) for c in face.normal]
    a, b = [k for k in range(3) if k != n.index(max(n))]
    for loop in face.loops:
        loop[uv].uv = (loop.vert.co[a], loop.vert.co[b])
bm.to_mesh(bench.data); bm.free()
bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
if BENCH_ALONG_ROOM:   # built along X; turned about its own centre, then the turn applied, so the object has none
    bench.rotation_euler = (0, 0, radians(90))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

# The rug under the bench: a thin slab whose picture is mapped once (uv_fit).
rw, rl = RUG
make("Rug", mat["Mat_Rug"], ("xyz", (bx - rw / 2, bx + rw / 2), (by - rl / 2, by + rl / 2), (0.002, 0.002 + RUG_T)))
uv_fit(bpy.data.objects["Rug"])

# Placard stand: a box whose top is tilted toward the viewer (who stands to the
# south) by lowering its front edge. It stands just right (east) of the frame,
# inside the gathering area but not in front of the painting.
top_w, top_len = PEDESTAL_TOP
tilt = radians(PEDESTAL_TILT)
pyc = y1 - PEDESTAL_OUT                                  # pedestal centre (Y)
px = (fx + PEDESTAL_GAP, fx + PEDESTAL_GAP + top_w)
py = (pyc - top_len * cos(tilt) / 2, pyc + top_len * cos(tilt) / 2)  # front, back
# 2026-11-10, after the concept sheet's reading stand, then softened: a moulded base and a head with rounded corners
# (rounded-rectangle rings, RR_*), joined by a turned round column (STAND_COLUMN). The whole still fills the old box
# (px, py, 0..PEDESTAL_H) and its top is the same tilted plane, so its centre (the object's origin), the Placard and
# Button empties, and The Starry Night's copy of it stay where they were.
bm = bmesh.new()
cx, cy = sum(px) / 2, sum(py) / 2
hx, hy = (px[1] - px[0]) / 2, (py[1] - py[0]) / 2
top_z = lambda y: PEDESTAL_H - (py[1] - y) / (py[1] - py[0]) * top_len * sin(tilt)   # the tilted reading plane


def rr_ring(inset, z, radius, smooth_z=None):
    """A rounded rectangle round (cx, cy), inset from the full footprint, at height z (or on the reading plane)."""
    # (never below 1 cm: the base's radius shrinks with its inset, and a negative one turned the corners inside out,
    # so the top cap crossed itself and was not drawn: the floor showed through, 2026-11-10)
    ax, ay = hx - inset, hy - inset
    r = max(0.01, min(radius, ax, ay))
    pts = []
    for qx, qy, a0 in ((1, 1, 0), (-1, 1, pi / 2), (-1, -1, pi), (1, -1, 3 * pi / 2)):
        for k in range(RR_SEG + 1):
            a = a0 + (pi / 2) * k / RR_SEG
            pts.append((cx + qx * (ax - r) + r * cos(a), cy + qy * (ay - r) + r * sin(a)))
    return [bm.verts.new((x, y, top_z(y) if z is None else z)) for x, y in pts]


def stack(rings, smooth, cap_bottom=False, cap_top=False):
    n = len(rings[0])
    for a, b in zip(rings, rings[1:]):
        for k in range(n):
            f = bm.faces.new([a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]])
            f.smooth = smooth
    if cap_bottom: bm.faces.new(list(reversed(rings[0])))
    if cap_top: bm.faces.new(rings[-1])


# base: (inset, height), a plinth with a rounded top edge and a cove up to a smaller step
stack([rr_ring(i_, z, RR_BASE - i_) for i_, z in STAND_BASE_PROFILE], True, cap_bottom=True, cap_top=True)
# column: turned, round
z0 = STAND_BASE_PROFILE[-1][1]
col = [[bm.verts.new((cx + r * cos(2 * pi * k / STAND_SIDES), cy + r * sin(2 * pi * k / STAND_SIDES), z0 + (STAND_HEAD_Z - z0) * f))
        for k in range(STAND_SIDES)] for f, r in STAND_COLUMN]
stack(col, True)
# head: eased up from a smaller ring under it to the full rounded outline, then straight up to the reading plane
head = [rr_ring(STAND_HEAD_EASE, STAND_HEAD_Z, RR_HEAD - STAND_HEAD_EASE), rr_ring(STAND_HEAD_EASE * 0.35, STAND_HEAD_Z + 0.012, RR_HEAD),
        rr_ring(0.0, STAND_HEAD_Z + 0.03, RR_HEAD), rr_ring(0.0, None, RR_HEAD)]
stack(head, False, cap_bottom=True, cap_top=True)
finish("Pedestal_Bedroom", mat["Mat_Bench"], bm, centred=True)

# Attachment points. -Y is Unity's forward, so the slot needs no turn to face
# into the room, and the spawn point turns 180 degrees to face the painting.
empty("Slot_Bedroom", (0, y1, pz0 + PAINTING_H / 2))
empty("Spawn_Point", (0, y0 + SPAWN_INSET, 0), turn_deg=180)
# On the pedestal's top, facing the viewer like Slot_Bedroom: forward sticks
# straight out of the tilted top (90 - tilt degrees up from horizontal) and up
# points up the slope. f = how far up the slope, from front edge (0) to back (1).
for name, f in (("Placard_Bedroom", 0.65), ("Button_Bedroom", 0.2)):  # title text, Enter button
    spot = (sum(px) / 2, py[0] + f * (py[1] - py[0]), PEDESTAL_H - (1 - f) * top_len * sin(tilt))
    empty(name, spot, up_deg=90 - PEDESTAL_TILT)

tris = sum(len(p.vertices) - 2 for o in scene.objects if o.type == "MESH" for p in o.data.polygons)
print(f"Triangles: {tris} (budget 20000)")
assert tris < 20000, tris

bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
FBX_PATH.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=str(FBX_PATH),
    object_types={"MESH", "EMPTY"},     # no cameras, no lights
    axis_forward="-Z", axis_up="Y",     # Unity's axes
    apply_scale_options="FBX_SCALE_ALL", apply_unit_scale=True,
    bake_space_transform=True,          # "Apply Transform"
)
print(f"Saved {BLEND_PATH}\nExported {FBX_PATH}")
