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
from math import cos, pi, radians, sin
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
STAND_BASE_H, STAND_STEP, STAND_STEP_H = 0.05, 0.04, 0.03   # the stand's base slab, the step on it (inset), its height
STAND_POST, STAND_POST_FOOT = 0.09, 1.5                       # the post's width at the top, and how much wider at its foot
STAND_HEAD_Z = 0.80                                           # where the post meets the head (its front top edge is 0.88)

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


FRAME_PROFILE = [   # (out, depth) round the cross-section, back at the wall to the front and out
    (0.000, 0.000), (0.000, 0.100), (0.004, 0.118), (0.012, 0.128), (0.022, 0.130), (0.030, 0.124), (0.034, 0.114),
    (0.046, 0.104), (0.062, 0.100), (0.078, 0.104), (0.090, 0.116), (0.104, 0.132), (0.120, 0.142), (0.136, 0.145),
    (0.150, 0.140), (0.160, 0.128), (0.166, 0.118), (0.172, 0.116), (0.180, 0.104), (0.180, 0.000),
]
ROSETTE_R, ROSETTE_PETALS = 0.088, 8      # corner rosettes (they stay within the 0.18 m rail); the side ones are 0.75x
# (coarse pieces: 10 petals of 64 triangles each made the rosettes 55 % of the frame, detail nobody sees from 2 m)
LEAF_STEP, LEAF_SIZE = 0.085, (0.036, 0.016, 0.024)   # the run of carved leaves round the crown: spacing; half length, depth, width
SHELL_R, SHELL_RIBS = 0.20, 9             # the fan shell over the top rail


def ellipsoid(name, centre, radii, rot_y=0.0, rot_z=0.0, seg=8, rings=5):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=centre)
    o = bpy.context.active_object
    o.name = name
    o.scale = radii
    o.rotation_euler = (0, rot_y, rot_z)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return o


def build_frame():
    """Frame_Bedroom: the profile swept round the opening, with its ornaments, as one mesh at the world origin (as before)."""
    w2, h2, zc = PAINTING_W / 2, PAINTING_H / 2, PAINTING_BOTTOM + PAINTING_H / 2
    front = y1 - max(d for _, d in FRAME_PROFILE)        # the frame's front, as a y
    # The gilt texture (GT_Frame, 3:1) runs along each rail: u along the rail, v up the profile from the inner edge (0)
    # to the outer (1), so its strokes follow the moulding as a gilder's brush would. Each rail has its own vertices, so
    # its u starts afresh at its corner; u is in units of the image's own width at the profile's arc length (no stretch).
    arc = [0.0]
    for (o0, d0), (o1, d1) in zip(FRAME_PROFILE, FRAME_PROFILE[1:]):
        arc.append(arc[-1] + ((o1 - o0) ** 2 + (d1 - d0) ** 2) ** 0.5)
    S = arc[-1]
    repeat = 3.0 * S                                       # metres along the rail for one image width
    rect = lambda out: [(-(w2 + out), -(h2 + out)), (w2 + out, -(h2 + out)), (w2 + out, h2 + out), (-(w2 + out), h2 + out)]
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    for side in range(4):                                  # bottom, right, top, left
        rows = []
        for (out, depth), t in zip(FRAME_PROFILE, arc):
            r = rect(out)
            (xa, za), (xb, zb) = r[side], r[(side + 1) % 4]
            y = y1 - depth
            rows.append(((bm.verts.new((xa, y, zc + za)), bm.verts.new((xb, y, zc + zb))), (xa, za), (xb, zb), t / S))
        horizontal = side in (0, 2)
        for (pv, pa, pb, pt), (qv, qa, qb, qt) in zip(rows, rows[1:]):
            f = bm.faces.new([pv[0], pv[1], qv[1], qv[0]])
            for loop, (pos, v) in zip(f.loops, ((pa, pt), (pb, pt), (qb, qt), (qa, qt))):
                along = pos[0] if horizontal else pos[1]
                loop[uvl].uv = (along / repeat, v)
            # outward: away from the middle of this rail's cross-section
            c = f.calc_center_median()
            inner = Vector((c.x, y1 - 0.06, c.z))
            if horizontal: inner.z = zc + (1 if side == 2 else -1) * (h2 + FRAME_W / 2)
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

    def rosette(cx, cz, r):
        parts.append(ellipsoid("ros", (cx, front - r * 0.45, cz), (r * 0.42, r * 0.5, r * 0.42), seg=6, rings=4))   # the domed centre
        for k in range(ROSETTE_PETALS):
            t = 2 * pi * k / ROSETTE_PETALS
            parts.append(ellipsoid("pet", (cx + cos(t) * r * 0.6, front - r * 0.22, cz + sin(t) * r * 0.6),
                                   (r * 0.4, r * 0.3, r * 0.24), rot_y=-t, seg=6, rings=3))
    for sx in (-1, 1):
        for sz in (-1, 1):
            rosette(sx * (w2 + mid), zc + sz * (h2 + mid), ROSETTE_R)
        rosette(sx * (w2 + mid), zc, ROSETTE_R * 0.75)

    # leaves along the crown (out 0.13, its highest point), each lying along the rail, clear of the rosettes and the shell
    crown_out, crown_depth = 0.13, max(d for _, d in FRAME_PROFILE)
    ll, ld, lw = LEAF_SIZE
    def leaf_run(x0, z0, x1, z1, skip):
        n = int(((x1 - x0) ** 2 + (z1 - z0) ** 2) ** 0.5 / LEAF_STEP)
        for k in range(n + 1):
            f = k / n
            x, z = x0 + (x1 - x0) * f, z0 + (z1 - z0) * f
            if any(abs(x - sx_) < r_ and abs(z - sz_) < r_ for sx_, sz_, r_ in skip):
                continue
            along = 0.0 if abs(z1 - z0) < 1e-6 else pi / 2
            parts.append(ellipsoid("leaf", (x, y1 - crown_depth - ld * 0.4, z), (ll, ld, lw), rot_y=along, seg=4, rings=3))
    a, b = w2 + crown_out, h2 + crown_out
    corners = [(sx * (w2 + mid), zc + sz * (h2 + mid), ROSETTE_R * 1.3) for sx in (-1, 1) for sz in (-1, 1)]
    sides = [(sx * (w2 + mid), zc, ROSETTE_R * 1.0) for sx in (-1, 1)]
    top_shell = [(0.0, zc + b, SHELL_R * 1.35)]
    skip = corners + sides + top_shell
    leaf_run(-a, zc + b, a, zc + b, skip)      # top
    leaf_run(-a, zc - b, a, zc - b, skip)      # bottom
    leaf_run(-a, zc - b, -a, zc + b, skip)     # sides
    leaf_run(a, zc - b, a, zc + b, skip)

    def shell(cz, r, up):
        """A fan of ribs standing on the rail's outer edge, opening up (up=1) or down (-1), with a C-scroll each side."""
        base = (0.0, cz)
        for k in range(SHELL_RIBS):
            t = pi * (0.08 + 0.84 * k / (SHELL_RIBS - 1))   # spread over most of a half circle
            x, z = cos(t) * r * 0.55, sin(t) * r * 0.55 * up
            parts.append(ellipsoid("rib", (base[0] + x, front - 0.025, base[1] + z), (r * 0.5, 0.035, r * 0.12),
                                   rot_y=-t * up, seg=8, rings=4))
        parts.append(ellipsoid("hub", (base[0], front - 0.035, base[1]), (r * 0.3, 0.05, r * 0.22)))
        for sx in (-1, 1):   # C-scrolls: a torus arc lying on the frame each side of the shell
            bpy.ops.mesh.primitive_torus_add(major_radius=r * 0.28, minor_radius=r * 0.06, major_segments=10, minor_segments=5,
                                             location=(sx * r * 0.95, front - 0.015, base[1] + up * r * 0.12),
                                             rotation=(pi / 2, 0, 0))
            o = bpy.context.active_object
            o.name = "scroll"
            bm2 = bmesh.new(); bm2.from_mesh(o.data)
            cut = [v for v in bm2.verts if (v.co.x * sx < -r * 0.08)]   # open the ring on the side towards the shell: a C
            bmesh.ops.delete(bm2, geom=cut, context="VERTS")
            bm2.to_mesh(o.data); bm2.free()
            parts.append(o)
    shell(zc + h2 + FRAME_W - 0.02, SHELL_R, 1)   # only over the top: one under the bottom rail would reach the wainscot

    for n, o in enumerate(parts):   # each ornament part: a patch from the gilt's middle band, along its longer side
        o.data.materials.clear()
        o.data.materials.append(mat["Mat_Frame"])
        bm2 = bmesh.new(); bm2.from_mesh(o.data)
        uv = bm2.loops.layers.uv.verify()
        xs, zs = [v.co.x for v in bm2.verts], [v.co.z for v in bm2.verts]
        ax = 0 if max(xs) - min(xs) >= max(zs) - min(zs) else 2
        lo_u = min(xs) if ax == 0 else min(zs)
        lo_v, span_v = (min(zs), max(zs) - min(zs) or 1) if ax == 0 else (min(xs), max(xs) - min(xs) or 1)
        for f in bm2.faces:
            for loop in f.loops:
                co = loop.vert.co
                a, b = (co.x, co.z) if ax == 0 else (co.z, co.x)
                loop[uv].uv = ((a - lo_u) / repeat + 0.137 * n, 0.3 + 0.4 * (b - lo_v) / span_v)
        bm2.to_mesh(o.data); bm2.free()
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    frame.select_set(True)
    bpy.context.view_layer.objects.active = frame
    bpy.ops.object.join()
    # (each ornament part got its own patch of the gilt above, before the join)


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
# 2026-11-10: a carved, gilt-style frame instead of a plain box (and instead of the Tripo corner ornaments, which never
# matched it). FRAME_PROFILE is its cross-section, swept round the opening with mitred corners: each point (out, depth),
# out from the opening's edge (0) to the frame's outer edge (FRAME_W), depth off the wall. From the inside: a sight bead,
# a cove, a rounded crown, an outer step. On it: a rosette at each corner and one on each side rail, and a fan shell with
# C-scrolls at the top centre (none under the bottom rail: it would reach into the wainscot). It keeps the 0.18 m width, so everything that knows the
# frame's outer edge (the wainscot's gaps, Vincent's portrait, The Starry Night's copy) is unchanged.
build_frame()

# Wainscot, cap rail, cornice and baseboard: strips on the walls' inside faces. The wainscot and its rail stop at
# the two paintings' frames (which stand on the wall from 0.62 m) and at the arch's moulding.
fz = pz0 - FRAME_W                        # frames' bottom edge
sn = (SN_FRAME_Y - fx, SN_FRAME_Y + fx)   # The Starry Night's frame along the east wall
gap = ARCH_R + ARCH_TRIM_W                # arch moulding's outer edge
wd, rd = WAINSCOT_D, RAIL_D
def strips(z0, z1, d, cut_frames):
    north = [(-fx, fx, max(z0, fz), z1)] if cut_frames and z1 > fz else []
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
# 2026-11-10, after the concept sheet's reading stand: a moulded base, a slim tapered post and a head whose top is the
# same tilted reading surface. The whole still fills the old box (px, py, 0..PEDESTAL_H), so its centre (the object's
# origin), the Placard and Button empties, and The Starry Night's copy of it stay where they were.
bm = bmesh.new()
cx, cy = sum(px) / 2, sum(py) / 2
add_box(bm, "xyz", px, py, (0, STAND_BASE_H))                                               # base
add_box(bm, "xyz", (px[0] + STAND_STEP, px[1] - STAND_STEP), (py[0] + STAND_STEP, py[1] - STAND_STEP),
        (STAND_BASE_H, STAND_BASE_H + STAND_STEP_H))                                        # step
post0 = len(bm.verts)
add_box(bm, "xyz", (cx - STAND_POST / 2, cx + STAND_POST / 2), (cy - STAND_POST / 2, cy + STAND_POST / 2),
        (STAND_BASE_H + STAND_STEP_H, STAND_HEAD_Z))                                        # post
for v in bm.verts[post0:]:
    if v.co.z < STAND_HEAD_Z / 2:   # wider at its foot
        v.co.x = cx + (v.co.x - cx) * STAND_POST_FOOT
        v.co.y = cy + (v.co.y - cy) * STAND_POST_FOOT
head0 = len(bm.verts)
add_box(bm, "xyz", px, py, (STAND_HEAD_Z, PEDESTAL_H))                                     # head
for v in bm.verts[head0:]:
    if v.co.z > (STAND_HEAD_Z + PEDESTAL_H) / 2 and v.co.y < pyc:   # the two top corners at the front
        v.co.z -= top_len * sin(tilt)
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
