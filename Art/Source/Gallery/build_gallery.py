"""Build the InnerFrame gallery shell in Blender and export it for Unity.

Run from the repo root (Blender 5.2, no window):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
        --python Art/Source/Gallery/build_gallery.py

Writes Art/Source/Gallery/gallery.blend and Assets/Art/Gallery/Gallery.fbx.
Every run deletes the old scene first, so running it twice gives the same result.
"""
from math import cos, radians, sin
from pathlib import Path

import bmesh
import bpy

# ---------------------------------------------------------------------------
# Parameters. 1 Blender unit = 1 m. North = +Y, East = +X, up = +Z.
# The entrance is in the south wall; the painting hangs on the north wall.
# ---------------------------------------------------------------------------
ROOM_LENGTH = 14.0   # inside, south wall to north wall (Y)
ROOM_WIDTH = 10.0    # inside, west wall to east wall (X)
ROOM_HEIGHT = 6.0    # inside, floor to ceiling
WALL_T = 0.2         # thickness of the walls and the floor
CEILING_T = 0.4      # ceiling thickness, which is also the depth of the skylight

ENTRANCE_W, ENTRANCE_H = 2.5, 2.4   # opening in the middle of Wall_South
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

GATHER_DEPTH = 5.0      # clear area in front of the painting (kept free of benches)
SKYLIGHT_W, SKYLIGHT_D = 4.0, 3.0   # ceiling opening, along X and along Y
SKYLIGHT_GAP = 1.0      # painting wall to the near edge of the skylight
SKYLIGHT_PANES = (5, 4)  # gaps between the bars, along X and along Y
BAR = 0.06              # skylight bar width and depth

WINDOWS = 8             # clerestory openings, high on Wall_West
WINDOW_W, WINDOW_H = 1.45, 0.9
WINDOW_TOP_GAP = 0.35   # top of the windows to the ceiling

BENCH_L, BENCH_D, BENCH_H, BENCH_T = 1.8, 0.5, 0.45, 0.08  # length, depth, height, slab
BENCH_WALL_GAP = 0.8    # side wall to the back edge of a bench
BENCHES = [(1, -4.0), (-1, -2.0), (1, 0.0)]  # (1 = east wall, -1 = west wall; Y of centre)

BASEBOARD_H, BASEBOARD_D = 0.1, 0.015

COLORS = {  # plain colours, linear RGB (the numbers Blender's colour picker shows)
    "Mat_Wall": (0.85, 0.81, 0.73),        # warm off-white
    "Mat_Floor": (0.22, 0.09, 0.035),      # warm mid-brown wood
    "Mat_Bench": (0.01, 0.01, 0.01),       # near-black
    "Mat_Frame": (0.17, 0.10, 0.025),      # dark gold
    "Mat_Metal": (0.05, 0.05, 0.05),       # dark grey, skylight bars
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

# The gathering area is the GATHER_DEPTH strip in front of the painting wall.
assert SKYLIGHT_GAP + SKYLIGHT_D <= GATHER_DEPTH, "skylight must be over the gathering area"
assert all(yc + BENCH_L / 2 <= y1 - GATHER_DEPTH for _, yc in BENCHES), "bench in gathering area"

# Floor and ceiling. The skylight sits over the gathering area, near the painting.
make("Floor", mat["Mat_Floor"], ("xyz", outer_x, outer_y, (-T, 0)))
sky = (-SKYLIGHT_W / 2, SKYLIGHT_W / 2, y1 - SKYLIGHT_GAP - SKYLIGHT_D, y1 - SKYLIGHT_GAP)
make("Ceiling", mat["Mat_Wall"], ("xyz", outer_x, outer_y, (H, H + CEILING_T), [sky]))

# Walls. East and west run the full outer length so the corners close.
door = (-ENTRANCE_W / 2, ENTRANCE_W / 2, 0, ENTRANCE_H)
pitch = ROOM_LENGTH / WINDOWS
windows = [(y0 + (i + 0.5) * pitch - WINDOW_W / 2, y0 + (i + 0.5) * pitch + WINDOW_W / 2,
            H - WINDOW_TOP_GAP - WINDOW_H, H - WINDOW_TOP_GAP) for i in range(WINDOWS)]
make("Wall_North", mat["Mat_Wall"], ("xzy", (x0, x1), (0, H), (y1, y1 + T)))  # solid behind the painting
make("Wall_South", mat["Mat_Wall"], ("xzy", (x0, x1), (0, H), (y0 - T, y0), [door]))
make("Wall_East", mat["Mat_Wall"], ("yzx", outer_y, (0, H), (x1, x1 + T)))
make("Wall_West", mat["Mat_Wall"], ("yzx", outer_y, (0, H), (x0 - T, x0), windows))

# Picture frame around the painting opening, standing FRAME_D off the north wall.
pz0, pz1 = PAINTING_BOTTOM, PAINTING_BOTTOM + PAINTING_H
fx = PAINTING_W / 2 + FRAME_W
assert pz0 - FRAME_W >= BASEBOARD_H, "frame's bottom rail must clear the baseboard"
make("Frame_Bedroom", mat["Mat_Frame"],
     ("xzy", (-fx, fx), (pz0 - FRAME_W, pz1 + FRAME_W), (y1 - FRAME_D, y1),
      [(-PAINTING_W / 2, PAINTING_W / 2, pz0, pz1)]))

# Skylight bars: a grid at the top of the skylight opening; the panes are holes.
nx, ny = SKYLIGHT_PANES
pw = (SKYLIGHT_W - (nx + 1) * BAR) / nx   # pane size along X
pd = (SKYLIGHT_D - (ny + 1) * BAR) / ny   # pane size along Y
panes = [(sky[0] + BAR + i * (pw + BAR), sky[0] + BAR + i * (pw + BAR) + pw,
          sky[2] + BAR + j * (pd + BAR), sky[2] + BAR + j * (pd + BAR) + pd)
         for i in range(nx) for j in range(ny)]
make("Skylight_Grid", mat["Mat_Metal"],
     ("xyz", sky[:2], sky[2:], (H + CEILING_T - BAR, H + CEILING_T), panes))

# Benches along the side walls: a seat on two legs, drawn as a U-shaped profile.
# Each bench's origin is its own centre, so it can be moved on its own in Unity.
for n, (side, yc) in enumerate(BENCHES, start=1):
    back = side * (ROOM_WIDTH / 2 - BENCH_WALL_GAP)   # edge nearest the wall
    ys = (yc - BENCH_L / 2, yc + BENCH_L / 2)
    under_seat = (ys[0] + BENCH_T, ys[1] - BENCH_T, 0, BENCH_H - BENCH_T)
    make(f"Bench_{n:02d}", mat["Mat_Bench"],
         ("yzx", ys, (0, BENCH_H), tuple(sorted((back, back - side * BENCH_D))), [under_seat]),
         centred=True)

# Placard stand: a box whose top is tilted toward the viewer (who stands to the
# south) by lowering its front edge. It stands just right (east) of the frame,
# inside the gathering area but not in front of the painting.
top_w, top_len = PEDESTAL_TOP
tilt = radians(PEDESTAL_TILT)
pyc = y1 - PEDESTAL_OUT                                  # pedestal centre (Y)
px = (fx + PEDESTAL_GAP, fx + PEDESTAL_GAP + top_w)
py = (pyc - top_len * cos(tilt) / 2, pyc + top_len * cos(tilt) / 2)  # front, back
bm = bmesh.new()
add_box(bm, "xyz", px, py, (0, PEDESTAL_H))
for v in bm.verts:
    if v.co.z > PEDESTAL_H / 2 and v.co.y < pyc:   # the two top corners at the front
        v.co.z -= top_len * sin(tilt)
finish("Pedestal_Bedroom", mat["Mat_Bench"], bm, centred=True)

# Baseboard: one strip per wall, with a gap at the entrance.
bh, bd = BASEBOARD_H, BASEBOARD_D
make("Baseboard", mat["Mat_Baseboard"],
     ("xzy", (x0, x1), (0, bh), (y1 - bd, y1)),                                            # north
     ("xzy", (x0, x1), (0, bh), (y0, y0 + bd), [(-ENTRANCE_W / 2, ENTRANCE_W / 2, 0, bh)]),  # south
     ("yzx", (y0 + bd, y1 - bd), (0, bh), (x1 - bd, x1)),                                  # east
     ("yzx", (y0 + bd, y1 - bd), (0, bh), (x0, x0 + bd)))                                  # west

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
