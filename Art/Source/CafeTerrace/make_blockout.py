"""Cafe Terrace at Night: grey blockout, built from cafe_layout.json (Blender 5.2, run in the background):

    /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
        --python Art/Source/CafeTerrace/make_blockout.py -- --out <render dir> [--blend <file.blend>] [--only cam1,cam2]
        [--set "key.path=value;..."]

What it is for: checking scale and proportions before any detail is made. Every number comes from cafe_layout.json;
nothing is placed by hand. Running it again gives the same scene.

- Buildings, ground, terrace, awning, balcony: plain boxes and prisms. Openings are flat panels on the wall face
  (dark = door or window, pale yellow = lit, mid grey = shutters); the detail pass cuts them for real.
- Tripo objects (tables, chairs, lantern, people, carriage, tree, easel): stand-ins named PH_<Thing>_<nn> at their
  real sizes, so the real models can replace them by name.
- One render per camera in the layout's "cameras", written to <render dir>/<camera>.png, plus counts on stdout.
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))

# Blockout shades (linear RGB). Categories are told apart by value, with a slight tint on the stand-ins.
SHADES = {
    "wall": (0.55, 0.56, 0.60), "cafe_wall": (0.70, 0.68, 0.58), "trim": (0.74, 0.74, 0.76),
    "roof": (0.33, 0.34, 0.38), "ground": (0.30, 0.30, 0.33), "pavement": (0.40, 0.40, 0.43),
    "terrace": (0.62, 0.52, 0.42), "awning": (0.80, 0.72, 0.45), "iron": (0.12, 0.12, 0.13),
    "opening": (0.10, 0.11, 0.13), "lit": (0.95, 0.80, 0.35), "shutter": (0.26, 0.34, 0.30),
    "shop_blue": (0.30, 0.38, 0.62), "shop_green": (0.22, 0.36, 0.32),
    "ph_table": (0.82, 0.84, 0.88), "ph_chair": (0.80, 0.62, 0.30), "ph_person": (0.72, 0.42, 0.42),
    "ph_tree": (0.35, 0.55, 0.35), "ph_wood": (0.55, 0.40, 0.25), "ph_carriage": (0.25, 0.42, 0.40),
    "far": (0.42, 0.44, 0.52),
}


def args_after_dashes():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {"out": None, "blend": None, "only": None, "set": None, "objects": None, "look": None,
            "swatches": None}
    i = 0
    while i < len(argv):
        key = argv[i].lstrip("-")
        opts[key] = argv[i + 1]
        i += 2
    return opts


def apply_overrides(layout, text):
    """--set "awning.outer_x=-3.9;painter.easel=[-3,-3.8]" : try values without editing the layout file."""
    for item in text.split(";"):
        path, value = item.split("=")
        node = layout
        keys = path.split(".")
        for k in keys[:-1]:
            node = node[k]
        node[keys[-1]] = json.loads(value)
        print("SET|%s|%s" % (path, value))


class Builder:
    def __init__(self, layout):
        self.L = layout
        self.mats = {}
        self.cols = {}
        self.tris = {}

    # ---- basics ----

    def mat(self, shade):
        if shade not in self.mats:
            m = bpy.data.materials.new("BO_" + shade)
            r, g, b = SHADES[shade]
            m.diffuse_color = (r, g, b, 1.0)
            self.mats[shade] = m
        return self.mats[shade]

    def col(self, name):
        if name not in self.cols:
            c = bpy.data.collections.new(name)
            bpy.context.scene.collection.children.link(c)
            self.cols[name] = c
        return self.cols[name]

    def add(self, name, bm, shade, col):
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        self.tris[col] = self.tris.get(col, 0) + sum(len(f.verts) - 2 for f in bm.faces)
        bm.free()
        me.materials.append(self.mat(shade))
        ob = bpy.data.objects.new(name, me)
        self.col(col).objects.link(ob)
        return ob

    def prism(self, name, poly, z0, z1, shade, col):
        """A vertical prism over a 2D polygon [(x, y), ...]."""
        bm = bmesh.new()
        bot = [bm.verts.new((p[0], p[1], z0)) for p in poly]
        top = [bm.verts.new((p[0], p[1], z1)) for p in poly]
        n = len(poly)
        bm.faces.new(bot)
        bm.faces.new(top)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((bot[i], bot[j], top[j], top[i]))
        return self.add(name, bm, shade, col)

    def box(self, name, lo, hi, shade, col):
        return self.prism(name, [(lo[0], lo[1]), (hi[0], lo[1]), (hi[0], hi[1]), (lo[0], hi[1])],
                          lo[2], hi[2], shade, col)

    def obox(self, name, o, t, n, u0, u1, v0, v1, z0, z1, shade, col):
        """A box in a front's own frame: point = o + t*u + n*v (t along the front, n out of it)."""
        o, t, n = Vector(o), Vector(t), Vector(n)
        poly = [o + t * u + n * v for u, v in ((u0, v0), (u1, v0), (u1, v1), (u0, v1))]
        return self.prism(name, poly, z0, z1, shade, col)

    def cyl(self, name, c, r, z0, z1, shade, col, seg=12):
        poly = [(c[0] + r * math.cos(2 * math.pi * i / seg), c[1] + r * math.sin(2 * math.pi * i / seg))
                for i in range(seg)]
        return self.prism(name, poly, z0, z1, shade, col)

    def ball(self, name, c, radii, shade, col):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=1.0)
        for v in bm.verts:
            v.co = Vector((c[0] + v.co.x * radii[0], c[1] + v.co.y * radii[1], c[2] + v.co.z * radii[2]))
        return self.add(name, bm, shade, col)

    def bar(self, name, p0, p1, r, shade, col):
        """A thin square bar between two 3D points."""
        p0, p1 = Vector(p0), Vector(p1)
        d = (p1 - p0).normalized()
        side = d.cross(Vector((0, 0, 1)))
        if side.length < 1e-4:
            side = Vector((1, 0, 0))
        side.normalize()
        up = side.cross(d)
        bm = bmesh.new()
        ring0 = [bm.verts.new(p0 + side * a * r + up * b * r) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        ring1 = [bm.verts.new(p1 + side * a * r + up * b * r) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        bm.faces.new(ring0)
        bm.faces.new(ring1)
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((ring0[i], ring0[j], ring1[j], ring1[i]))
        return self.add(name, bm, shade, col)

    # ---- ground ----

    def ground(self):
        s, t = self.L["street"], self.L["terrace"]
        m, kh = s["ground_margin"], s["kerb_height"]
        far_y = self.L["far"]["start_y"] + self.L["far"]["length"]
        self.box("Ground_Cobbles", (s["cafe_wall_x"] - m, s["back_wall_y"] - m, -0.05),
                 (s["right_wall_x"] + m, far_y, 0.0), "ground", "Near")
        self.prism("Pavement_Right", s["right_kerb"] + s["right_front"][::-1], 0.0, kh, "pavement", "Near")
        self.prism("Pavement_Left", s["left_kerb"] + s["left_front"][::-1], 0.0, kh, "pavement", "Near")
        by = s["back_wall_y"]
        self.box("Pavement_Back", (s["cafe_wall_x"], by, 0.0),
                 (s["right_wall_x"], by + s["back_pavement_depth"], kh), "pavement", "Near")
        self.box("Terrace_Floor", (t["x"][0], t["y"][0], 0.0), (t["x"][1], t["y"][1], t["height"]),
                 "terrace", "Near")

    # ---- houses ----

    def auto_openings(self, h, length):
        """Generic fronts for the houses beyond the cafe: a door, then windows, on each floor."""
        a = h["auto"]
        count = max(1, int(length / a["spacing"]))
        step = length / count
        out, k = [], 0
        z = 0.0
        for floor, fh in enumerate(h["floors"]):
            if fh < 2.4:
                break
            for i in range(count):
                u = step * (i + 0.5)
                if floor == 0 and i == count // 2:
                    out.append({"kind": "door", "u": u, "w": 1.0, "h": 2.5, "z": 0.12, "hood": True})
                else:
                    lit = k in a.get("lit", [])
                    out.append({"kind": "window", "u": u, "w": 0.9, "h": 1.4, "z": z + 1.0,
                                "shutters": "open" if lit else ("closed" if k % 2 else None), "lit": lit})
                k += 1
            z += fh
        return out

    def opening(self, hname, i, op, o, t, n):
        kind, u = op["kind"], op["u"]
        nm = "%s_%s_%02d" % (hname, kind, i)
        c = "Near"
        if kind == "drainpipe":
            self.obox(nm, o, t, n, u - 0.06, u + 0.06, 0.0, 0.14, 0.0, op.get("h", 6.2), "shutter", c)
            return
        w, hgt, z = op["w"], op["h"], op["z"]
        fill = "lit" if op.get("lit") else "opening"
        if kind == "shopfront":
            # the blue cafe front of 09_left: frame, cornice, door on the low-u side, window on the other
            self.obox(nm + "_Frame", o, t, n, u - w / 2, u + w / 2, 0.0, 0.18, z, z + hgt - 0.45, "shop_blue", c)
            self.obox(nm + "_Cornice", o, t, n, u - w / 2 - 0.15, u + w / 2 + 0.15, 0.0, 0.38, z + hgt - 0.45,
                      z + hgt, "shop_blue", c)
            dw = op["door_w"]
            d0 = u - w / 2 + 0.35
            self.obox(nm + "_DoorGlass", o, t, n, d0 + 0.12, d0 + dw - 0.12, 0.18, 0.2, z + 1.0, z + 2.2, "lit", c)
            w0 = d0 + dw + 0.35
            self.obox(nm + "_Window", o, t, n, w0, u + w / 2 - 0.35, 0.18, 0.2, z + 0.95, z + 2.45, "lit", c)
            return
        if kind == "sign":
            self.obox(nm, o, t, n, u - w / 2, u + w / 2, 0.0, 0.04, z, z + hgt, "trim", c)
            return
        self.obox(nm, o, t, n, u - w / 2, u + w / 2, 0.0, 0.03, z, z + hgt, fill, c)
        if kind == "gate":
            # arch stand-in: a flatter panel over the top
            self.obox(nm + "_Arch", o, t, n, u - w / 2 - 0.25, u + w / 2 + 0.25, 0.0, 0.10, z + hgt, z + hgt + 0.3,
                      "trim", c)
            self.obox(nm + "_JambA", o, t, n, u - w / 2 - 0.25, u - w / 2, 0.0, 0.10, z, z + hgt, "trim", c)
            self.obox(nm + "_JambB", o, t, n, u + w / 2, u + w / 2 + 0.25, 0.0, 0.10, z, z + hgt, "trim", c)
        if op.get("hood"):
            self.obox(nm + "_Hood", o, t, n, u - w / 2 - 0.2, u + w / 2 + 0.2, 0.0, 0.22, z + hgt + 0.05,
                      z + hgt + 0.25, "trim", c)
        if kind == "window":
            self.obox(nm + "_Sill", o, t, n, u - w / 2 - 0.08, u + w / 2 + 0.08, 0.0, 0.10, z - 0.08, z, "trim", c)
        sh = op.get("shutters")
        if sh == "open":
            sw = w / 2
            self.obox(nm + "_ShutterA", o, t, n, u - w / 2 - sw, u - w / 2, 0.0, 0.06, z, z + hgt, "shutter", c)
            self.obox(nm + "_ShutterB", o, t, n, u + w / 2, u + w / 2 + sw, 0.0, 0.06, z, z + hgt, "shutter", c)
        elif sh == "closed":
            self.obox(nm + "_Shutters", o, t, n, u - w / 2, u + w / 2, 0.03, 0.08, z, z + hgt, "shutter", c)

    def house(self, h):
        a, b = Vector(h["a"]), Vector(h["b"])
        length = (b - a).length
        t = (b - a) / length
        n = Vector((t.y, -t.x))
        eaves = sum(h["floors"])
        back_a, back_b = a - n * h["depth"], b - n * h["depth"]
        shade = "cafe_wall" if h["name"] == "L_Cafe" else "wall"
        self.prism(h["name"] + "_Body", [a, b, back_b, back_a], 0.0, eaves, shade, "Near")
        # eaves slab and a low roof block set back from the front
        self.prism(h["name"] + "_Eaves", [a + n * 0.18, b + n * 0.18, back_b, back_a], eaves, eaves + 0.14,
                   "trim", "Near")
        self.prism(h["name"] + "_Roof", [a - n * 0.5, b - n * 0.5, back_b, back_a], eaves + 0.14, eaves + 0.9,
                   "roof", "Near")
        for i, u in enumerate(h.get("chimneys", [])):
            self.obox("%s_Chimney_%02d" % (h["name"], i), a, t, n, u - 0.3, u + 0.3, -1.6, -1.0, eaves + 0.14,
                      eaves + 1.9, "wall", "Near")
        ops = list(h.get("openings", []))
        if "auto" in h:
            ops += self.auto_openings(h, length)
        for i, op in enumerate(ops):
            self.opening(h["name"], i, op, a, t, n)
        for i, op in enumerate(h.get("side_a_openings", [])):
            # the end wall at a: u runs from the street corner into the block, the wall faces back along the row
            self.opening(h["name"] + "_Side", i, op, a, -n, -t)

    def shop_bay(self):
        s, bay = self.L["street"], self.L["shop_bay"]
        x, d, c, kh = s["right_wall_x"], bay["depth"], bay["chamfer"], s["kerb_height"]
        y0, y1 = bay["y"]
        poly = [(x, y0), (x - d + c * 0.6, y0), (x - d, y0 + c), (x - d, y1 - c), (x - d + c * 0.6, y1), (x, y1)]
        self.prism("ShopBay_Body", poly, kh, bay["height"], "shop_green", "Near")
        self.box("ShopBay_Cornice", (x - d - 0.12, y0 - 0.12, bay["height"]), (x, y1 + 0.12, bay["height"] + 0.25),
                 "shop_green", "Near")
        self.box("ShopBay_Glass", (x - d - 0.02, y0 + c + 0.15, kh + 0.9), (x - d, y1 - c - 0.15, bay["height"] - 0.5),
                 "lit", "Near")

    # ---- cafe ----

    def awning(self):
        aw, s = self.L["awning"], self.L["street"]
        wx, (y0, y1) = s["cafe_wall_x"], aw["y"]
        th = aw["thickness"]
        bm = bmesh.new()
        pts = [(wx, y0, aw["wall_z"]), (aw["outer_x"], y0, aw["outer_z"]), (aw["outer_x"], y1, aw["outer_z"]),
               (wx, y1, aw["wall_z"])]
        top = [bm.verts.new(p) for p in pts]
        bot = [bm.verts.new((p[0], p[1], p[2] - th)) for p in pts]
        bm.faces.new(top)
        bm.faces.new(bot)
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((top[i], top[j], bot[j], bot[i]))
        self.add("Awning_Cloth", bm, "awning", "Near")
        self.box("Awning_Valance", (aw["outer_x"] - 0.02, y0, aw["outer_z"] - aw["valance"]),
                 (aw["outer_x"], y1, aw["outer_z"]), "awning", "Near")
        for i, y in enumerate(aw["rods_y"]):
            self.bar("Awning_Rod_%02d" % i, (wx, y, aw["rod_wall_z"]), (aw["outer_x"], y, aw["outer_z"]), 0.015,
                     "iron", "Near")

    def balcony(self):
        b, s = self.L["balcony"], self.L["street"]
        wx, (y0, y1) = s["cafe_wall_x"], b["y"]
        z, d, rh = b["floor_z"], b["depth"], b["rail_height"]
        self.box("Balcony_Floor", (wx, y0, z - 0.12), (wx + d, y1, z), "trim", "Near")
        self.box("Balcony_Rail_Top", (wx + d - 0.03, y0, z + rh - 0.04), (wx + d, y1, z + rh), "iron", "Near")
        count = int(round((y1 - y0) / b["post_spacing"]))
        for i in range(count + 1):
            y = y0 + (y1 - y0) * i / count
            self.box("Balcony_Post_%02d" % i, (wx + d - 0.025, y - 0.01, z), (wx + d - 0.005, y + 0.01, z + rh),
                     "iron", "Near")
        for i, y in enumerate((y0, y1)):
            self.box("Balcony_Rail_End_%d" % i, (wx, y - 0.015, z + rh - 0.04), (wx + d, y + 0.015, z + rh),
                     "iron", "Near")

    # ---- stand-ins for the Tripo objects ----

    def base_z(self, on):
        return self.L["terrace"]["height"] if on == "terrace" else 0.0

    def table(self, t):
        sz = self.L["sizes"]["table"]
        x, y = t["pos"]
        z = self.base_z(t.get("on"))
        r, h = sz["diameter"] / 2, sz["height"]
        top = self.cyl(t["name"], (x, y), r, z + h - 0.03, z + h, "ph_table", "Placeholders", seg=16)
        stem = self.cyl(t["name"] + "_Stem", (x, y), 0.03, z, z + h - 0.03, "iron", "Placeholders", seg=6)
        foot = self.cyl(t["name"] + "_Foot", (x, y), r * 0.7, z, z + 0.02, "iron", "Placeholders", seg=10)
        stem.parent = top
        foot.parent = top

    def chair(self, name, x, y, z, yaw_deg):
        sz = self.L["sizes"]["chair"]
        s, sh, bh = sz["seat"] / 2, sz["seat_height"], sz["back_height"]
        yaw = math.radians(yaw_deg)
        fwd = (math.sin(yaw), math.cos(yaw))    # the way the sitter faces
        side = (math.cos(yaw), -math.sin(yaw))
        seat = self.obox(name, (x, y), side, fwd, -s, s, -s, s, z + sh - 0.04, z + sh, "ph_chair", "Placeholders")
        back = self.obox(name + "_Back", (x, y), side, fwd, -s, s, -s, -s + 0.03, z + sh, z + bh, "ph_chair",
                         "Placeholders")
        back.parent = seat
        for i, (a, b) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
            leg = self.obox("%s_Leg_%d" % (name, i), (x, y), side, fwd, a * s - 0.015, a * s + 0.015,
                            b * s - 0.015, b * s + 0.015, z, z + sh - 0.04, "ph_chair", "Placeholders")
            leg.parent = seat

    def person(self, name, x, y, z, height):
        w = self.L["sizes"]["person"]["width"] / 2
        body = self.cyl(name, (x, y), w, z, z + height - 0.24, "ph_person", "Placeholders", seg=10)
        head = self.ball(name + "_Head", (x, y, z + height - 0.12), (0.11, 0.11, 0.12), "ph_person", "Placeholders")
        head.parent = body

    def furniture_and_people(self):
        sz = self.L["sizes"]
        chair_n, cust_n = 0, 0
        for t in self.L["tables"]:
            self.table(t)
            x, y = t["pos"]
            z = self.base_z(t.get("on"))
            for dx, dy, customer in t.get("seats", []):
                chair_n += 1
                yaw = math.degrees(math.atan2(-dx, -dy))        # the sitter faces the table
                self.chair("PH_Chair_%02d" % chair_n, x + dx, y + dy, z, yaw)
                if customer:
                    cust_n += 1
                    self.person("PH_Customer_%02d" % cust_n, x + dx, y + dy, z, sz["person"]["seated"])
        for c in self.L.get("loose_chairs", []):
            chair_n += 1
            self.chair_jitter = "_is" not in c           # a chair read off the front view keeps exactly its direction
            self.chair("PH_Chair_%02d" % chair_n, c["pos"][0], c["pos"][1], self.base_z(c.get("on")), c["yaw_deg"])
        for p in self.L["people"]:
            self.person(p["name"], p["pos"][0], p["pos"][1], self.base_z(p.get("on")),
                        p.get("height", sz["person"]["standing"]))
        vx, vy = self.L["painter"]["vincent"]
        self.person("PH_Vincent", vx, vy, 0.0, sz["person"]["standing"])

    def lantern(self):
        ln, s, sz = self.L["lantern"], self.L["street"], self.L["sizes"]["lantern"]
        wx, y, z = s["cafe_wall_x"], ln["wall_y"], ln["z"]
        x = wx + ln["reach"]
        w, h = sz["w"] / 2, sz["h"]
        body = self.box("PH_Lantern", (x - w, y - w, z - h), (x + w, y + w, z), "lit", "Placeholders")
        arm = self.bar("PH_Lantern_Bracket", (wx, y, z + 0.25), (x, y, z + 0.25), 0.015, "iron", "Placeholders")
        drop = self.bar("PH_Lantern_Hook", (x, y, z + 0.25), (x, y, z), 0.01, "iron", "Placeholders")
        arm.parent = body
        drop.parent = body

    def tree(self):
        tr, sz = self.L["tree"], self.L["sizes"]["tree"]
        x, y = tr["pos"]
        trunk = self.cyl(tr["name"], (x, y), sz["trunk_r"], 0.0, sz["trunk_h"], "ph_wood", "Placeholders", seg=10)
        r = sz["crown_r"]
        crown = self.ball(tr["name"] + "_Crown", (x, y, sz["crown_z"]), (r, r, r * 0.8), "ph_tree", "Placeholders")
        ring = self.cyl("Tree_StoneRing", (x, y), sz["ring_r"], 0.0, 0.12, "pavement", "Near", seg=16)
        crown.parent = trunk
        del ring

    def easel(self):
        sz, p = self.L["sizes"]["easel"], self.L["painter"]
        x, y = p["easel"]
        yaw = math.radians(p["easel_yaw_deg"])
        fwd = Vector((math.sin(yaw), math.cos(yaw)))     # the way the canvas faces: back at the painter
        side = Vector((fwd.y, -fwd.x))
        o = Vector((x, y)) - fwd * 0.75                  # the easel stands 0.75 m in front of the painter's feet
        cw, ch, cz = sz["canvas_w"] / 2, sz["canvas_h"], sz["canvas_z"]
        canvas = self.obox("PH_Easel", o, side, fwd, -cw, cw, 0.0, 0.03, cz, cz + ch, "ph_table", "Placeholders")
        for i, (u, v) in enumerate(((-0.32, 0.05), (0.32, 0.05), (0.0, -0.55))):
            foot = o + side * u + fwd * v
            leg = self.bar("PH_Easel_Leg_%d" % i, (foot.x, foot.y, 0.0), (o.x, o.y, sz["height"]), 0.02, "ph_wood",
                           "Placeholders")
            leg.parent = canvas

    def carriage(self):
        c, sz = self.L["carriage"], self.L["sizes"]["carriage"]
        x, y = c["pos"]
        yaw = math.radians(c["yaw_deg"])
        fwd = Vector((math.sin(yaw), math.cos(yaw)))
        side = Vector((fwd.y, -fwd.x))
        w = sz["w"] / 2
        body = self.obox(c["name"], (x, y), side, fwd, -w, w, -sz["body_l"], 0.0, 0.55, sz["h"], "ph_carriage",
                         "Placeholders")
        horse = self.obox(c["name"] + "_Horse", (x, y), side, fwd, -0.3, 0.3, 0.5, 0.5 + sz["horse_l"], 0.75,
                          sz["horse_h"], "ph_wood", "Placeholders")
        horse.parent = body
        for i, (u, v) in enumerate(((-w, -0.5), (w, -0.5), (-w, -sz["body_l"] + 0.4), (w, -sz["body_l"] + 0.4))):
            wheel = self.obox("%s_Wheel_%d" % (c["name"], i), (x, y), side, fwd, u - 0.04, u + 0.04, v - 0.5,
                              v + 0.5, 0.0, 1.0, "iron", "Placeholders")
            wheel.parent = body

    # ---- far layer stand-in ----

    def far(self):
        f = self.L["far"]
        n, y0, ln = f["houses_per_side"], f["start_y"], f["length"]

        def centre(y):
            k = (y - y0) / ln
            return f["bend_x"] * k * k

        for side in (-1, 1):
            for i in range(n):
                ya, yb = y0 + ln * i / n, y0 + ln * (i + 1) / n
                xa, xb = centre(ya) + side * f["width"] / 2, centre(yb) + side * f["width"] / 2
                hgt = 6.4 + 1.3 * ((i * 7 + (3 if side > 0 else 0)) % 4) / 3.0
                self.prism("Far_%s_%02d" % ("L" if side < 0 else "R", i),
                           [(xa, ya), (xb, yb), (xb + side * 6, yb), (xa + side * 6, ya)], 0.0, hgt, "far", "Far")
        tw = f["tower"]
        w = tw["w"] / 2
        tx, ty = tw["pos"]
        self.box("Far_Tower", (tx - w, ty - w, 0.0), (tx + w, ty + w, tw["height"]), "far", "Far")
        self.box("Far_End", (centre(y0 + ln) - 8, y0 + ln, 0.0), (centre(y0 + ln) + 8, y0 + ln + 4, 7.0), "far", "Far")

    # ---- markers (not geometry of the set) ----

    def markers(self):
        sp, ex = self.L["spawn"], self.L["exit_trigger"]
        self.cyl("MARK_Spawn", sp["pos"], 0.25, 0.12, 0.14, "lit", "Markers", seg=16)
        cx, cy = ex["center"]
        sx, sy = ex["size"]
        self.box("MARK_ExitTrigger", (cx - sx / 2, cy - sy / 2, 0.0), (cx + sx / 2, cy + sy / 2, 0.02), "lit",
                 "Markers")
        self.cyl("MARK_Painter", self.L["painter"]["easel"], 0.15, 0.0, 0.02, "lit", "Markers", seg=16)
        pv = self.L["painting_view"]["pos"]
        self.cyl("MARK_PaintingView", pv, 0.15, 0.0, 0.02, "ph_person", "Markers", seg=16)

    def build(self):
        self.ground()
        for h in self.L["houses"]:
            self.house(h)
        self.shop_bay()
        self.awning()
        self.balcony()
        self.furniture_and_people()
        self.lantern()
        self.tree()
        self.easel()
        self.carriage()
        self.far()
        self.markers()


# ---- cameras and renders ----

def look(yaw_deg, pitch_deg):
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    return Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), math.sin(pitch)))


def make_camera(name, spec, layout):
    cam = bpy.data.cameras.new("CAM_" + name)
    ob = bpy.data.objects.new("CAM_" + name, cam)
    bpy.context.scene.collection.objects.link(ob)
    eye = layout["rig"]["eye_height"]
    if spec.get("ortho"):
        cam.type = "ORTHO"
        cam.ortho_scale = spec["ortho_scale"]
        ob.location = (spec["center"][0], spec["center"][1], 60.0)
        ob.rotation_euler = (0.0, 0.0, 0.0)          # looking straight down, +y up the image
        cam.clip_end = 200.0
        if "cut_above" in spec:                      # leave out everything higher (awning, balcony, roofs)
            cam.clip_start = 60.0 - spec["cut_above"]
        return ob
    src = layout[spec["from"]] if "from" in spec else spec
    if "pos" in src:
        p = src["pos"]
        ob.location = (p[0], p[1], p[2] if len(p) > 2 else eye)
    else:
        e = layout["painter"]["easel"]
        ob.location = (e[0], e[1], eye)
    yaw = src.get("yaw_deg", spec.get("yaw_deg", 0))
    ob.rotation_euler = look(yaw, spec.get("pitch_deg", 0)).to_track_quat("-Z", "Y").to_euler()
    vfov = src.get("vfov_deg", spec.get("vfov_deg"))
    if vfov:
        cam.sensor_fit = "VERTICAL"
        cam.sensor_height = 24.0
        cam.lens = 12.0 / math.tan(math.radians(vfov) / 2)
    else:
        cam.sensor_fit = "HORIZONTAL"
        cam.sensor_width = 36.0
        cam.lens = 18.0 / math.tan(math.radians(spec["hfov_deg"]) / 2)
    cam.clip_start, cam.clip_end = 0.05, 300.0
    return ob


def setup_render():
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_cavity = True
    sh.cavity_type = "BOTH"
    sh.show_shadows = False      # flat light: a blockout is read by shape, and the scene's light is painted in later
    sc.world = bpy.data.worlds.new("BO_World")
    sc.world.color = (0.10, 0.16, 0.42)
    sc.view_settings.view_transform = "Standard"
    sc.render.image_settings.file_format = "PNG"
    sc.render.film_transparent = False


def main():
    opts = args_after_dashes()
    with open(os.path.join(HERE, "cafe_layout.json")) as f:
        layout = json.load(f)
    if opts["set"]:
        apply_overrides(layout, opts["set"])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    b = Builder(layout)
    b.build()
    setup_render()
    for col, n in sorted(b.tris.items()):
        print("TRIS|%s|%d" % (col, n))
    print("TRIS|total|%d" % sum(b.tris.values()))
    print("OBJECTS|%d" % len(bpy.data.objects))
    cams = {name: make_camera(name, spec, layout) for name, spec in layout["cameras"].items()
            if not name.startswith("_")}
    if opts["blend"]:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(opts["blend"]), compress=False)
    if opts["out"]:
        out = os.path.abspath(opts["out"])
        os.makedirs(out, exist_ok=True)
        only = opts["only"].split(",") if opts["only"] else None
        sc = bpy.context.scene
        for name, ob in cams.items():
            if only and name not in only:
                continue
            spec = layout["cameras"][name]
            sc.camera = ob
            sc.render.resolution_x, sc.render.resolution_y = spec["res"]
            # the markers are for the plan only; "hide" leaves out stand-ins by name prefix
            hide = tuple(spec.get("hide", []))
            for o in bpy.data.objects:
                if o.type == "MESH":
                    o.hide_render = bool(hide) and o.name.startswith(hide)
            for o in b.col("Markers").objects:
                o.hide_render = name != "plan"
            sc.render.filepath = os.path.join(out, name + ".png")
            bpy.ops.render.render(write_still=True)
            print("RENDER|%s|%s" % (name, sc.render.filepath))


if __name__ == "__main__":
    main()
