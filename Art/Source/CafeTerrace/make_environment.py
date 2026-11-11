"""Cafe Terrace at Night: the detailed set, built from cafe_layout.json (Blender 5.2, run in the background):

    /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
        --python Art/Source/CafeTerrace/make_environment.py -- --out <render dir> [--blend <file.blend>]
        [--only cam1,cam2] [--set "key.path=value;..."] [--objects <dir with the Tripo .glb files>]
        [--look lit] [--swatches <dir with painted colour swatches and swatches.json>]

--look lit (the look-dev view, Cycles): real lights stand in the scene (the lantern, the lit windows, a blue night
sky), so walls darken into corners, tables shade the planks and the lamp falls off across the cobbles. This is the
light that gets baked for the headset; nothing here is a realtime light on Quest. Without it: flat, unlit preview.
--swatches: painted colour textures (cut or generated from the target images) replace the script-made grey streaks,
per surface, as listed in that folder's swatches.json.

Everything is geometry, nothing is a picture on a plane: each cobble is its own low rounded stone, walls have cut
openings with reveals, shutters are slats, the terrace is separate planks, the gate has an arch of stones.

How the look is carried:
- No lights. The light is painted into the vertex colours ("warm" below: sulphur yellow and orange round the
  lantern, the awning and the lit windows; blue-violet along the street; nothing black).
- Each mesh has UVs in metres. The material multiplies the vertex colour by a tileable grey dry-brush swatch x 2
  (make_swatches.py), so one swatch serves every colour.
- Meshes are merged per layer and material: CT_<Layer>_<Material>. Layers: Near (to the carriage), Far (beyond it).
- Tripo objects: with --objects, the chair, table and lantern models found there replace their PH_ stand-ins
  (objects.json beside this script says which file, and the real size each is brought to). The rest stay stand-ins.
  The models keep Tripo's own texture; each copy is tinted warm or cool by where it stands.
"""
import math
import os
import random
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_blockout as bo  # noqa: E402

TEX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "CafeTerrace", "Textures"))

# material -> (swatch, metres per tile)
MATERIALS = {
    "Wall": ("CT_StrokeWall.png", 1.6), "Wood": ("CT_StrokeWood.png", 0.9), "Stone": ("CT_StrokeStone.png", 0.8),
    "Cloth": ("CT_StrokeCloth.png", 1.2), "Glow": (None, 1.0), "Iron": (None, 1.0),
    "WallCafe": ("CT_StrokeWall.png", 1.6), "Plank": ("CT_StrokeWood.png", 0.9), "Cobble": ("CT_StrokeStone.png", 0.8),
}


def srgb(hexcode):
    """'#rrggbb' as painted -> linear rgb."""
    h = hexcode.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return tuple(out)


# Colours read from 01_master_v2.webp (cool = away from the lamps, warm = in their light).
PAL = {
    "cafe_cool": srgb("#6276a0"), "cafe_warm": srgb("#f8c230"), "cafe_warm_deep": srgb("#e8a81e"),
    "house_cool": srgb("#56688c"), "house_violet": srgb("#5e6a90"), "house_deep": srgb("#2c3a66"),
    "house_warm": srgb("#8a8f8a"),
    "trim_cool": srgb("#7480a8"), "trim_warm": srgb("#e8c070"),
    "roof": srgb("#3e4662"), "roof_light": srgb("#56608a"), "pot": srgb("#b0683a"),
    "shutter": srgb("#3c5a46"), "shutter_dark": srgb("#24382c"), "shutter_light": srgb("#597a62"),
    "door_green": srgb("#35553f"), "door_orange": srgb("#f79a0a"), "door_orange_light": srgb("#fcb62a"),
    "shop_blue": srgb("#36446a"), "shop_blue_dark": srgb("#262f4a"), "shop_blue_light": srgb("#425278"),
    "muntin": srgb("#d2551e"), "shop_green": srgb("#2b4a40"), "shop_green_light": srgb("#41665a"),
    "lit": srgb("#fcc63f"), "lit_deep": srgb("#f59f02"), "glass_dark": srgb("#27304a"), "curtain": srgb("#b9b4a6"),
    "plank": srgb("#d4853c"), "plank_dark": srgb("#91581f"), "plank_cool": srgb("#7a5a5a"),
    "awning": srgb("#ecb02c"), "awning_under": srgb("#f6c038"), "awning_stripe": srgb("#6f8f4a"),
    "awning_cool": srgb("#a89060"),
    "joint": srgb("#3a3550"), "joint_warm": srgb("#6a4a30"),
    "iron": srgb("#26262e"), "iron_warm": srgb("#5a3a20"),
    "pave_cool": srgb("#8f7a84"), "pave_warm": srgb("#c79a62"),
}
COBBLE_COOL = [srgb(c) for c in ("#62688c", "#7a7086", "#8a7a7c", "#70748e", "#908486", "#5c6284")]
COBBLE_WARM = [srgb(c) for c in ("#b98a5c", "#c79a62", "#a87a58", "#d2a56a", "#b5805f")]


TONE = {}
TONE_GROUPS = {
    "house": ("house_cool", "house_violet", "house_warm", "trim_cool", "roof", "roof_light"),   # not house_deep: the far street stays dark
    "cafe": ("cafe_warm", "cafe_warm_deep", "trim_warm"),
    "shop": ("shop_blue", "shop_blue_light", "shop_blue_dark"),
    "shutter": ("shutter", "shutter_dark", "shutter_light"),
    "door": ("door_green",),
    "glow": ("lit", "lit_deep", "door_orange", "door_orange_light"),
    "awning": ("awning", "awning_under", "awning_cool"),
}


def apply_tone(layout):
    """cafe_layout.json "tone": each surface's paint multiplied to match the effect images (tone_check.py)."""
    TONE.clear()
    TONE.update({k: tuple(v) for k, v in layout.get("tone", {}).items()})
    for group, keys in TONE_GROUPS.items():
        t = TONE.get(group)
        if t:
            for k in keys:
                PAL[k] = (PAL[k][0] * t[0], PAL[k][1] * t[1], PAL[k][2] * t[2])


def toned(group, c):
    t = TONE.get(group)
    return c if not t else (c[0] * t[0], c[1] * t[1], c[2] * t[2])


def mix(a, b, w):
    w = 0.0 if w < 0 else (1.0 if w > 1 else w)
    return (a[0] + (b[0] - a[0]) * w, a[1] + (b[1] - a[1]) * w, a[2] + (b[2] - a[2]) * w)


def mul(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def smooth(e0, e1, x):
    t = (x - e0) / (e1 - e0)
    t = 0.0 if t < 0 else (1.0 if t > 1 else t)
    return t * t * (3 - 2 * t)


class Buf:
    """One merged mesh: unshared vertices, a colour and a UV per vertex."""

    def __init__(self, tile):
        self.v, self.f, self.c, self.uv, self.tile = [], [], [], [], tile
        self.smooth = False

    def mesh(self, verts, faces, cols, uvs):
        """Shared-vertex geometry (for rounded things that are shaded smooth)."""
        base = len(self.v)
        self.v.extend(verts)
        self.c.extend(cols)
        self.uv.extend(uvs)
        self.f.extend(tuple(base + i for i in f) for f in faces)

    def poly(self, pts, shade, uvs=None):
        base = len(self.v)
        a, b, c = pts[0], pts[1], pts[2]
        nx = (b[1] - a[1]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[1] - a[1])
        ny = (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2])
        nz = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        ln = math.sqrt(nx * nx + ny * ny + nz * nz) or 1.0
        flat = abs(nz) / ln > 0.7
        hl = math.hypot(nx, ny) or 1.0
        tx, ty = -ny / hl, nx / hl
        for p in pts:
            self.v.append((p[0], p[1], p[2]))
            self.c.append(shade(p) if callable(shade) else shade)
            if uvs is not None:
                self.uv.append(uvs[len(self.v) - 1 - base])
            elif flat:
                self.uv.append((p[0] / self.tile, p[1] / self.tile))
            else:
                self.uv.append(((p[0] * tx + p[1] * ty) / self.tile, p[2] / self.tile))
        self.f.append(tuple(range(base, base + len(pts))))

    def tris(self):
        return sum(len(f) - 2 for f in self.f)


class Frame:
    """A wall's own axes: point = o + t*u + n*v, height z. t runs along the wall, n points out of it."""

    def __init__(self, a, b):
        ax, ay, bx, by = a[0], a[1], b[0], b[1]
        self.length = math.hypot(bx - ax, by - ay)
        self.o = (ax, ay)
        self.t = ((bx - ax) / self.length, (by - ay) / self.length)
        self.n = (self.t[1], -self.t[0])

    def p(self, u, v, z):
        return (self.o[0] + self.t[0] * u + self.n[0] * v, self.o[1] + self.t[1] * u + self.n[1] * v, z)

    def shifted(self, u, v):
        f = Frame.__new__(Frame)
        f.length, f.t, f.n = self.length, self.t, self.n
        p = self.p(u, v, 0)
        f.o = (p[0], p[1])
        return f


class Env(bo.Builder):
    def __init__(self, layout):
        super().__init__(layout)
        self.bufs = {}
        self.objects_dir = None
        self.chair_jitter = True
        self.plank_seg = 1.2           # shorter pieces when light is baked per vertex (make_unity_export.py)
        self.lit_look = False          # --look lit: lights do the warm and cool, colours hold only the paint
        self.sw = {}                   # painted colour swatches by surface (swatches.json)
        self.sw_dir = None
        self.rng = random.Random(1888)
        s, aw, ln = layout["street"], layout["awning"], layout["lantern"]
        self.lamp = (s["cafe_wall_x"] + ln["reach"], ln["wall_y"], ln["z"])
        self.aw_box = (s["cafe_wall_x"], aw["outer_x"], aw["y"][0], aw["y"][1])
        bay = layout["shop_bay"]
        self.glows = [((s["right_wall_x"] - bay["depth"] - 0.4, (bay["y"][0] + bay["y"][1]) / 2, 1.6), 2.6, 0.7),
                      ((s["cafe_wall_x"] + 0.5, -0.4, 1.6), 2.2, 0.6)]
        self.models = {}

    # ---- stand-ins need a material that shows without lights ----

    def mat(self, shade):
        if shade not in self.mats:
            m = bpy.data.materials.new("BO_" + shade)
            m.use_nodes = True
            nt = m.node_tree
            nt.nodes.clear()
            out = nt.nodes.new("ShaderNodeOutputMaterial")
            em = nt.nodes.new("ShaderNodeBsdfDiffuse" if self.lit_look else "ShaderNodeEmission")
            r, g, b = bo.SHADES[shade]
            em.inputs["Color"].default_value = (r, g, b, 1.0)
            nt.links.new(em.outputs[0], out.inputs["Surface"])
            self.mats[shade] = m
        return self.mats[shade]

    # ---- painted light ----

    def warm(self, p):
        """0 = night street, 1 = full lamp light, at a point."""
        if self.lit_look:
            return 0.0
        x, y, z = p
        lx, ly, lz = self.lamp
        d2 = (x - lx) ** 2 + (y - ly) ** 2 + (z - lz) ** 2
        w = 1.0 / (1.0 + d2 / 6.0)
        x0, x1, y0, y1 = self.aw_box
        dx = max(x0 - x, 0.0, x - x1)
        dy = max(y0 - y, 0.0, y - y1)
        w = max(w, 0.92 * (1.0 - smooth(0.0, 3.2, math.hypot(dx, dy))))
        for (gx, gy, gz), reach, strength in self.glows:
            d = math.sqrt((x - gx) ** 2 + (y - gy) ** 2 + (z - gz) ** 2)
            w = max(w, strength * (1.0 - smooth(0.3, reach, d)))
        return w * (1.0 - 0.8 * smooth(4.3, 5.3, z))            # above the awning the lamp doesn't reach

    def own(self, p):
        """For surfaces whose own paint is the warm colour (cafe wall, planks, awning): 1 under real lights."""
        return 1.0 if self.lit_look else self.warm(p)

    def deep(self, y):
        """How far down the dark street: 0 by the terrace, 1 at the far end."""
        return smooth(4.0, 22.0, y)      # the effect images: houses get darker down the street (03_overview, 01_master_v2)

    def lit(self, cool, warm_col, jitter=0.0):
        k = 1.0 + (self.rng.random() * 2 - 1) * jitter
        cool_k, warm_k = mul(cool, k), mul(warm_col, k)
        return lambda p: mix(mix(cool_k, PAL["house_deep"], 0.75 * self.deep(p[1])), warm_k, self.warm(p))

    # ---- buffers ----

    def buf(self, layer, material):
        key = "%s_%s" % (layer, material)
        if key not in self.bufs:
            self.bufs[key] = Buf(MATERIALS["Stone" if material == "Cobbles" else material][1])
        return self.bufs[key]

    def fbox(self, b, fr, u0, u1, v0, v1, z0, z1, shade, faces="flrtk"):
        """A box in a frame. faces: f front(+n) b back l(u0) r(u1) t top k bottom."""
        P = fr.p

        def tone(k):
            # with no lights, a box only reads if its sides are painted darker and its top lighter
            if callable(shade):
                return lambda p: mul(shade(p), k)
            return mul(shade, k)

        if "f" in faces:
            b.poly([P(u0, v1, z0), P(u1, v1, z0), P(u1, v1, z1), P(u0, v1, z1)], shade)
        if "b" in faces:
            b.poly([P(u1, v0, z0), P(u0, v0, z0), P(u0, v0, z1), P(u1, v0, z1)], tone(0.8))
        if "r" in faces:
            b.poly([P(u1, v1, z0), P(u1, v0, z0), P(u1, v0, z1), P(u1, v1, z1)], tone(0.74))
        if "l" in faces:
            b.poly([P(u0, v0, z0), P(u0, v1, z0), P(u0, v1, z1), P(u0, v0, z1)], tone(0.84))
        if "t" in faces:
            b.poly([P(u0, v0, z1), P(u0, v1, z1), P(u1, v1, z1), P(u1, v0, z1)], tone(1.14))
        if "k" in faces:
            b.poly([P(u0, v0, z0), P(u1, v0, z0), P(u1, v1, z0), P(u0, v1, z0)], tone(0.6))

    def both(self, b, pts, shade):
        b.poly(pts, shade)
        b.poly(pts[::-1], shade)

    def rod(self, b, p0, p1, r, shade):
        p0, p1 = Vector(p0), Vector(p1)
        d = (p1 - p0).normalized()
        side = d.cross(Vector((0, 0, 1)))
        if side.length < 1e-4:
            side = Vector((1, 0, 0))
        side.normalize()
        up = side.cross(d)
        ring = ((-1, -1), (1, -1), (1, 1), (-1, 1))
        a = [p0 + side * i * r + up * j * r for i, j in ring]
        c = [p1 + side * i * r + up * j * r for i, j in ring]
        for i in range(4):
            j = (i + 1) % 4
            self.both(b, [tuple(a[i]), tuple(a[j]), tuple(c[j]), tuple(c[i])], shade)

    # ---- walls ----

    def wall_grid(self, b, fr, u0, u1, z0, z1, holes, shade_for_column, v=0.0, du=0.3, dz=0.55):
        us = {u0, u1}
        zs = {z0, z1}
        for h in holes:
            us.update((max(u0, h[0]), min(u1, h[1])))
            zs.update((max(z0, h[2]), min(z1, h[3])))
        n = max(1, int(round((u1 - u0) / du)))
        us.update(u0 + (u1 - u0) * i / n for i in range(1, n))
        m = max(1, int(round((z1 - z0) / dz)))
        zs.update(z0 + (z1 - z0) * i / m for i in range(1, m))
        us, zs = sorted(us), sorted(zs)
        us = [u for i, u in enumerate(us) if i == 0 or u - us[i - 1] > 1e-4]
        zs = [z for i, z in enumerate(zs) if i == 0 or z - zs[i - 1] > 1e-4]
        for i in range(len(us) - 1):
            ua, ub = us[i], us[i + 1]
            shade = shade_for_column()
            for j in range(len(zs) - 1):
                za, zb = zs[j], zs[j + 1]
                uc, zc = (ua + ub) / 2, (za + zb) / 2
                if any(h[0] - 1e-6 < uc < h[1] + 1e-6 and h[2] - 1e-6 < zc < h[3] + 1e-6 for h in holes):
                    continue
                b.poly([fr.p(ua, v, za), fr.p(ub, v, za), fr.p(ub, v, zb), fr.p(ua, v, zb)], shade)

    def reveal(self, b, fr, u0, u1, z0, z1, depth, shade, bottom=True):
        P = fr.p
        base = shade
        shade = (lambda p: mul(base(p), 0.72)) if callable(base) else mul(base, 0.72)
        b.poly([P(u0, 0, z0), P(u0, -depth, z0), P(u0, -depth, z1), P(u0, 0, z1)], shade)      # faces +t
        b.poly([P(u1, -depth, z0), P(u1, 0, z0), P(u1, 0, z1), P(u1, -depth, z1)], shade)      # faces -t
        b.poly([P(u0, -depth, z1), P(u1, -depth, z1), P(u1, 0, z1), P(u0, 0, z1)], shade)      # head, faces down
        if bottom:
            b.poly([P(u0, 0, z0), P(u1, 0, z0), P(u1, -depth, z0), P(u0, -depth, z0)], shade)  # sill, faces up

    def glass(self, b, fr, u0, u1, z0, z1, v, lit, curtain=False):
        P = fr.p
        if lit:
            lo, hi = PAL["lit_deep"], PAL["lit"]
            b.poly([P(u0, v, z0), P(u1, v, z0), P(u1, v, z1), P(u0, v, z1)],
                   lambda p: mix(lo, hi, (p[2] - z0) / (z1 - z0) * 0.8 + 0.2))
        else:
            b.poly([P(u0, v, z0), P(u1, v, z0), P(u1, v, z1), P(u0, v, z1)], PAL["glass_dark"])

    def leaf(self, layer, fr, u0, u1, z0, z1, v, lod):
        """One shutter leaf: frame and slats."""
        b = self.buf(layer, "Wood")
        g = self.lit(mix(PAL["shutter"], PAL["shutter_light"], self.rng.random() * 0.4), PAL["shutter_light"], 0.08)
        dark = self.lit(PAL["shutter_dark"], PAL["shutter"], 0.0)
        if lod:
            b.poly([fr.p(u0, v + 0.03, z0), fr.p(u1, v + 0.03, z0), fr.p(u1, v + 0.03, z1), fr.p(u0, v + 0.03, z1)], g)
            return
        s = 0.055
        self.fbox(b, fr, u0, u0 + s, v, v + 0.04, z0, z1, g, "flr")
        self.fbox(b, fr, u1 - s, u1, v, v + 0.04, z0, z1, g, "flr")
        self.fbox(b, fr, u0 + s, u1 - s, v, v + 0.04, z0, z0 + s, g, "ft")
        self.fbox(b, fr, u0 + s, u1 - s, v, v + 0.04, z1 - s, z1, g, "fk")
        b.poly([fr.p(u0 + s, v + 0.004, z0 + s), fr.p(u1 - s, v + 0.004, z0 + s), fr.p(u1 - s, v + 0.004, z1 - s),
                fr.p(u0 + s, v + 0.004, z1 - s)], dark)
        count = max(3, int((z1 - z0 - 2 * s) / 0.085))
        step = (z1 - z0 - 2 * s) / count
        for i in range(count):
            za = z0 + s + step * i + 0.012
            self.both(b, [fr.p(u0 + s, v + 0.038, za), fr.p(u1 - s, v + 0.038, za), fr.p(u1 - s, v + 0.01, za + step * 0.8),
                          fr.p(u0 + s, v + 0.01, za + step * 0.8)], g)

    def window(self, layer, fr, op, lod, trim):
        u, w, h, z = op["u"], op["w"], op["h"], op["z"]
        u0, u1, z1 = u - w / 2, u + w / 2, z + h
        wood, glow, wall = self.buf(layer, "Wood"), self.buf(layer, "Glow"), self.buf(layer, "Wall")
        lit = bool(op.get("lit"))
        depth = 0.0 if lod else 0.16
        sh = op.get("shutters")
        if not lod:
            self.reveal(wall, fr, u0, u1, z, z1, depth, trim)
        if sh != "closed":
            self.glass(glow, fr, u0, u1, z, z1, -depth + (0.004 if lod else 0.0), lit)
            if not lod:
                frame_col = self.lit(PAL["muntin"] if lit else PAL["shutter_dark"], PAL["muntin"], 0.05)
                f, v0, v1 = 0.06, -depth, -depth + 0.045
                self.fbox(wood, fr, u0, u0 + f, v0, v1, z, z1, frame_col, "fr")
                self.fbox(wood, fr, u1 - f, u1, v0, v1, z, z1, frame_col, "fl")
                self.fbox(wood, fr, u0 + f, u1 - f, v0, v1, z, z + f, frame_col, "ft")
                self.fbox(wood, fr, u0 + f, u1 - f, v0, v1, z1 - f, z1, frame_col, "fk")
                self.fbox(wood, fr, u - 0.025, u + 0.025, v0, v1 - 0.01, z + f, z1 - f, frame_col, "flr")
                zm = z + h * 0.55
                self.fbox(wood, fr, u0 + f, u1 - f, v0, v1 - 0.015, zm - 0.02, zm + 0.02, frame_col, "ftk")
                if not lit and op.get("curtain", True) and sh is None:
                    cu = self.lit(PAL["curtain"], PAL["curtain"], 0.1)
                    for a, c in ((u0 + f, u0 + f + w * 0.28), (u1 - f - w * 0.28, u1 - f)):
                        glow.poly([fr.p(a, v0 + 0.006, z + f), fr.p(c, v0 + 0.006, z + f), fr.p(c, v0 + 0.006, z1 - f),
                                   fr.p(a, v0 + 0.006, z1 - f)], lambda p: mul(cu(p), 0.55))
        if op["kind"] == "window":
            self.fbox(wall, fr, u0 - 0.1, u1 + 0.1, 0.0, 0.11, z - 0.09, z, trim, "flrtk")
        if sh == "open":
            sw = w / 2
            self.leaf(layer, fr, u0 - sw - 0.01, u0 - 0.01, z, z1, 0.015, lod)
            self.leaf(layer, fr, u1 + 0.01, u1 + sw + 0.01, z, z1, 0.015, lod)
        elif sh == "closed":
            self.leaf(layer, fr, u0, u - 0.005, z, z1, -0.07 if not lod else 0.0, lod)
            self.leaf(layer, fr, u + 0.005, u1, z, z1, -0.07 if not lod else 0.0, lod)

    def door(self, layer, fr, op, lod, trim):
        u, w, h, z = op["u"], op["w"], op["h"], op["z"]
        u0, u1, z1 = u - w / 2, u + w / 2, z + h
        wood, glow, wall = self.buf(layer, "Wood"), self.buf(layer, "Glow"), self.buf(layer, "Wall")
        lit = bool(op.get("lit"))
        depth = 0.0 if lod else 0.13
        if lit:
            base = self.lit(PAL["door_orange"], PAL["door_orange_light"], 0.04)
            panel = self.lit(PAL["door_orange_light"], PAL["lit"], 0.04)
        else:
            base = self.lit(PAL["door_green"], PAL["shutter_light"], 0.08)
            panel = self.lit(mul(PAL["door_green"], 1.25), PAL["shutter_light"], 0.08)
        if not lod:
            self.reveal(wall, fr, u0, u1, z, z1, depth, trim, bottom=False)
        v = -depth + (0.004 if lod else 0.0)
        top = z1
        if h >= 2.45 and not lit:
            top = z1 - 0.36                                           # a glazed light over the door
            self.glass(glow, fr, u0, u1, top + 0.05, z1, v, False)
            if not lod:
                self.fbox(wood, fr, u0, u1, v, v + 0.05, top, top + 0.05, base, "ftk")
                for k in (1, 2):
                    um = u0 + w * k / 3
                    self.fbox(wood, fr, um - 0.015, um + 0.015, v, v + 0.03, top + 0.05, z1, base, "flr")
        wood.poly([fr.p(u0, v, z), fr.p(u1, v, z), fr.p(u1, v, top), fr.p(u0, v, top)], base)
        if not lod:
            leaves = 2 if w > 1.05 or lit else 1
            lw = w / leaves
            for i in range(leaves):
                a = u0 + lw * i
                for (za, zb) in ((z + 0.14, z + (top - z) * 0.42), (z + (top - z) * 0.48, top - 0.14)):
                    self.fbox(wood, fr, a + 0.1, a + lw - 0.1, v, v + 0.02, za, zb, panel, "flrtk")
            if leaves == 2:
                self.fbox(wood, fr, u - 0.012, u + 0.012, v, v + 0.012, z, top, self.lit(PAL["shutter_dark"], PAL["iron_warm"]), "f")
            ar = 0.16
            self.fbox(wall, fr, u0 - ar, u0, 0.0, 0.07, z, z1 + ar, trim, "flr")
            self.fbox(wall, fr, u1, u1 + ar, 0.0, 0.07, z, z1 + ar, trim, "flr")
            self.fbox(wall, fr, u0, u1, 0.0, 0.07, z1, z1 + ar, trim, "fk")
            self.fbox(wall, fr, u0 - ar - 0.03, u0 - ar + 0.04, 0.0, 0.1, z, z1 + ar + 0.03, trim, "flr")
            self.fbox(wall, fr, u1 + ar - 0.04, u1 + ar + 0.03, 0.0, 0.1, z, z1 + ar + 0.03, trim, "flr")
            self.fbox(wall, fr, u0 - ar - 0.03, u1 + ar + 0.03, 0.0, 0.1, z1 + ar - 0.04, z1 + ar + 0.03, trim, "ftk")
        if op.get("hood"):
            a, c, zt = u0 - 0.3, u1 + 0.3, z1 + (0.12 if not lod else 0.0)
            self.fbox(wall, fr, a + 0.08, c - 0.08, 0.0, 0.07, zt, zt + 0.16, trim, "flrk")
            self.fbox(wall, fr, a, c, 0.0, 0.2, zt + 0.16, zt + 0.23, trim, "flrtk")
            self.fbox(wall, fr, a - 0.04, c + 0.04, 0.0, 0.26, zt + 0.23, zt + 0.29, trim, "flrtk")
            if not lod:
                for ub in (a + 0.1, c - 0.22):
                    self.fbox(wall, fr, ub, ub + 0.12, 0.0, 0.15, zt - 0.14, zt + 0.16, trim, "flrk")
        if sh := op.get("shutters"):
            sw = w / 2
            self.leaf(layer, fr, u0 - sw - 0.01, u0 - 0.01, z, z1, 0.015, lod)
            self.leaf(layer, fr, u1 + 0.01, u1 + sw + 0.01, z, z1, 0.015, lod)
            del sh

    def gate(self, layer, fr, op, trim):
        u, w, h, z = op["u"], op["w"], op["h"], op["z"]
        u0, u1, z1 = u - w / 2, u + w / 2, z + h
        wood, wall, iron = self.buf(layer, "Wood"), self.buf(layer, "Wall"), self.buf(layer, "Iron")
        rise = w * 0.30
        zs = z1 - rise
        seg, depth = 14, 0.22

        def arch(ua, grow=0.0):
            k = (ua - u) / (w / 2 + grow)
            k = max(-1.0, min(1.0, k))
            return zs + (rise + grow) * math.sqrt(1.0 - k * k)

        stone_a = self.lit(mix(PAL["trim_cool"], PAL["pave_cool"], 0.5), PAL["trim_warm"], 0.06)
        stone_b = self.lit(mul(mix(PAL["trim_cool"], PAL["pave_cool"], 0.5), 0.86), PAL["trim_warm"], 0.06)
        wallcol = self.lit(PAL["house_cool"], PAL["house_warm"], 0.03)
        for i in range(seg):
            ua, ub = u0 + w * i / seg, u0 + w * (i + 1) / seg
            wall.poly([fr.p(ua, 0, arch(ua)), fr.p(ub, 0, arch(ub)), fr.p(ub, 0, z1), fr.p(ua, 0, z1)], wallcol)
            wall.poly([fr.p(ua, -depth, arch(ua)), fr.p(ub, -depth, arch(ub)), fr.p(ub, 0, arch(ub)), fr.p(ua, 0, arch(ua))],
                      stone_b)
        wall.poly([fr.p(u0, 0, z), fr.p(u0, -depth, z), fr.p(u0, -depth, zs), fr.p(u0, 0, zs)], stone_b)
        wall.poly([fr.p(u1, -depth, z), fr.p(u1, 0, z), fr.p(u1, 0, zs), fr.p(u1, -depth, zs)], stone_b)
        # boards
        boards = 12
        for i in range(boards):
            ua, ub = u0 + w * i / boards + 0.006, u0 + w * (i + 1) / boards - 0.006
            col = self.lit(mul(PAL["door_green"], 0.9 + 0.3 * self.rng.random()), PAL["shutter_light"], 0.05)
            wood.poly([fr.p(ua, -depth + 0.03, z), fr.p(ub, -depth + 0.03, z), fr.p(ub, -depth + 0.03, arch(ub)),
                       fr.p(ua, -depth + 0.03, arch(ua))], col)
        wood.poly([fr.p(u0, -depth + 0.02, z), fr.p(u1, -depth + 0.02, z), fr.p(u1, -depth + 0.02, zs),
                   fr.p(u0, -depth + 0.02, zs)], self.lit(PAL["shutter_dark"], PAL["shutter_dark"]))
        ironcol = self.lit(PAL["iron"], PAL["iron_warm"])
        for zh in (z + 0.45, z + (zs - z) * 0.92):
            for (a, c) in ((u0 + 0.05, u0 + w * 0.36), (u1 - w * 0.36, u1 - 0.05)):
                self.fbox(iron, fr, a, c, -depth + 0.03, -depth + 0.045, zh - 0.04, zh + 0.04, ironcol, "ftk")
        # jamb stones and the ring of arch stones
        jw, proud = 0.3, 0.06
        n_j = max(3, int((zs - z) / 0.42))
        for i in range(n_j):
            za, zb = z + (zs - z) * i / n_j, z + (zs - z) * (i + 1) / n_j
            col = stone_a if i % 2 else stone_b
            self.fbox(wall, fr, u0 - jw, u0, 0.0, proud, za + 0.008, zb - 0.008, col, "flrtk")
            self.fbox(wall, fr, u1, u1 + jw, 0.0, proud, za + 0.008, zb - 0.008, col, "flrtk")
        ring = 13
        for i in range(ring):
            a0, a1 = math.pi * (1 - i / ring), math.pi * (1 - (i + 1) / ring)
            pts = []
            for ang, grow in ((a0, 0.0), (a1, 0.0), (a1, jw), (a0, jw)):
                pts.append(fr.p(u + (w / 2 + grow) * math.cos(ang), proud, zs + (rise + grow) * math.sin(ang)))
            key = i == ring // 2
            wall.poly(pts, stone_a if (i % 2 or key) else stone_b)
            if key:
                wall.poly([fr.p(u - 0.16, proud + 0.03, zs + rise - 0.05), fr.p(u + 0.16, proud + 0.03, zs + rise - 0.05),
                           fr.p(u + 0.22, proud + 0.03, zs + rise + jw + 0.08), fr.p(u - 0.22, proud + 0.03, zs + rise + jw + 0.08)],
                          stone_a)

    def shopfront(self, layer, fr, op):
        """The blue cafe front of 09_left: pilasters, entablature, a glazed door and a big window with orange bars."""
        u, w, h, z = op["u"], op["w"], op["h"], op["z"]
        u0, u1 = u - w / 2, u + w / 2
        wood, glow = self.buf(layer, "Wood"), self.buf(layer, "Glow")
        blue = lambda k=1.0, j=0.05: self.lit(mul(PAL["shop_blue"], k), mul(PAL["shop_blue_light"], k), j)  # noqa: E731
        orange = self.lit(PAL["muntin"], PAL["door_orange"], 0.04)
        ent = 0.5
        zt = z + h - ent
        pw, dw = 0.3, op["door_w"]
        # back board, entablature, cornice
        wood.poly([fr.p(u0, 0.02, z), fr.p(u1, 0.02, z), fr.p(u1, 0.02, zt), fr.p(u0, 0.02, zt)], blue(0.38))
        self.fbox(wood, fr, u0 - 0.04, u1 + 0.04, 0.0, 0.16, zt, zt + 0.3, blue(), "flrk")
        self.fbox(wood, fr, u0 - 0.1, u1 + 0.1, 0.0, 0.26, zt + 0.3, zt + 0.4, blue(1.3), "flrtk")
        self.fbox(wood, fr, u0 - 0.16, u1 + 0.16, 0.0, 0.36, zt + 0.4, zt + 0.5, blue(1.5), "flrtk")
        # pilasters: ends, and between the door and the window
        d0 = u0 + pw
        d1 = d0 + dw
        for a in (u0, d1, u1 - pw):
            self.fbox(wood, fr, a, a + pw, 0.0, 0.14, z, zt, blue(1.12), "flr")
            self.fbox(wood, fr, a + 0.06, a + pw - 0.06, 0.14, 0.17, z + 0.2, z + 0.9, blue(1.35), "flrtk")
            self.fbox(wood, fr, a + 0.06, a + pw - 0.06, 0.14, 0.17, zt - 0.5, zt - 0.14, blue(1.35), "flrtk")
            self.fbox(wood, fr, a - 0.02, a + pw + 0.02, 0.0, 0.17, zt - 0.08, zt, blue(1.2), "flrk")
        # door: header, 6 panes, lower panel
        self.fbox(wood, fr, d0 + 0.045, d1 - 0.045, 0.02, 0.08, zt - 0.42, zt - 0.04, blue(0.9), "fk")
        dz1 = zt - 0.42
        self.fbox(wood, fr, d0 + 0.045, d1 - 0.045, 0.02, 0.06, z, dz1 - 0.03, blue(0.72), "flr")
        g0, g1 = z + 1.0, dz1 - 0.14
        self.glass(glow, fr, d0 + 0.14, d1 - 0.14, g0, g1, 0.062, True)
        for k in range(3):
            um = d0 + 0.14 + (dw - 0.28) * k / 2
            self.fbox(wood, fr, um - 0.02, um + 0.02, 0.062, 0.08, g0, g1, orange, "flr")
        for k in range(4):
            zm = g0 + (g1 - g0) * k / 3
            self.fbox(wood, fr, d0 + 0.14, d1 - 0.14, 0.062, 0.08, zm - 0.02, zm + 0.02, orange, "ftk")
        self.fbox(wood, fr, d0 + 0.16, d1 - 0.16, 0.06, 0.085, z + 0.18, z + 0.85, blue(1.25), "flrtk")
        # window: stall riser with two panels, glass with a 3 x 3 grid
        w0, w1 = d1 + pw, u1 - pw
        rz = z + 0.9
        self.fbox(wood, fr, w0 + 0.045, w1 - 0.045, 0.02, 0.1, z + 0.03, rz - 0.03, blue(0.72), "ft")
        half = (w1 - w0) / 2
        for k in range(2):
            a = w0 + half * k
            self.fbox(wood, fr, a + 0.1, a + half - 0.1, 0.1, 0.125, z + 0.14, rz - 0.14, blue(1.3), "flrtk")
        self.glass(glow, fr, w0, w1, rz, zt, 0.04, True)
        for k in range(4):
            um = w0 + (w1 - w0) * k / 3
            self.fbox(wood, fr, um - 0.025, um + 0.025, 0.04, 0.07, rz, zt, orange, "flr")
        for k in range(4):
            zm = rz + (zt - rz) * k / 3
            self.fbox(wood, fr, w0, w1, 0.04, 0.07, max(rz, zm - 0.025), min(zt, zm + 0.025), orange, "ftk")

    def opening(self, hname, i, op, fr, layer="Near", lod=0, trim=None):
        kind = op["kind"]
        if kind == "window":
            self.window(layer, fr, op, lod, trim)
        elif kind == "door":
            self.door(layer, fr, op, lod, trim)
        elif kind == "gate":
            self.gate(layer, fr, op, trim)
        elif kind == "shopfront":
            self.shopfront(layer, fr, op)
        elif kind == "sign":
            u, w, h, z = op["u"], op["w"], op["h"], op["z"]
            wood = self.buf(layer, "Wood")
            self.fbox(wood, fr, u - w / 2, u + w / 2, 0.0, 0.04, z, z + h, self.lit(PAL["plank_dark"], PAL["plank"]), "flrtk")
            self.fbox(wood, fr, u - w / 2 + 0.05, u + w / 2 - 0.05, 0.04, 0.045, z + 0.05, z + h - 0.05,
                      self.lit(PAL["awning_cool"], PAL["awning"]), "f")
        elif kind == "drainpipe":
            iron = self.buf(layer, "Iron")
            u, top = op["u"], op.get("h", 6.2)
            col = self.lit(PAL["shutter"], PAL["shutter_light"], 0.05)
            self.fbox(iron, fr, u - 0.05, u + 0.05, 0.03, 0.13, 0.12, top, col, "flr")
            self.fbox(iron, fr, u - 0.11, u + 0.11, 0.0, 0.2, top, top + 0.22, col, "flrtk")
            if not lod:
                for zb in (0.6, 2.6, 4.6):
                    self.fbox(iron, fr, u - 0.075, u + 0.075, 0.0, 0.15, zb, zb + 0.05, col, "flrtk")

    def hole(self, op):
        k = op["kind"]
        if k in ("window", "door", "gate"):
            return (op["u"] - op["w"] / 2, op["u"] + op["w"] / 2, op["z"], op["z"] + op["h"])
        return None

    def house(self, h, layer="Near", lod=0):
        fr = Frame(h["a"], h["b"])
        length, eaves, name = fr.length, sum(h["floors"]), h["name"]
        cafe = name == "L_Cafe"
        wall, stone = self.buf(layer, "Wall"), self.buf(layer, "Stone")
        face = self.buf(layer, "WallCafe") if cafe else wall
        hue = self.rng.random()
        cool = mix(PAL["house_cool"], PAL["house_violet"], hue * 0.7)
        cool = mul(cool, 0.9 + 0.2 * self.rng.random())

        def column():
            k = 1.0 + (self.rng.random() * 2 - 1) * 0.07
            if cafe:
                c0, w0, w1 = mul(PAL["cafe_cool"], k), mul(PAL["cafe_warm"], k), mul(PAL["cafe_warm_deep"], k)

                def shade(p):
                    w = (1.0 - smooth(5.2, 7.2, p[2])) * (0.92 + 0.08 * self.own(p))
                    return mix(c0, mix(w1, w0, 0.35 + 0.65 * self.own(p)), w)
                return shade
            c0, c1 = mul(cool, k), mul(PAL["house_warm"], k)
            return lambda p: mix(mix(c0, PAL["house_deep"], 0.92 * self.deep(p[1])), mix(c1, PAL["trim_warm"], 0.5),
                                 0.8 * self.warm(p))

        trim_base = PAL["cafe_cool"] if cafe else PAL["trim_cool"]
        trim = self.lit(trim_base, PAL["trim_warm"], 0.04)
        if cafe and self.lit_look:
            trim = self.lit(mul(PAL["cafe_warm"], 1.08), PAL["cafe_warm"], 0.04)
        ops = list(h.get("openings", []))
        if "auto" in h:
            ops += self.auto_openings(h, length)
        holes = [x for x in (self.hole(op) for op in ops) if x]
        self.wall_grid(face, fr, 0.0, length, 0.0, eaves, holes, column, du=0.6 if lod else 0.3, dz=1.2 if lod else 0.55)
        for i, op in enumerate(ops):
            self.opening(name, i, op, fr, layer, lod, trim)
        # end walls (seen where a neighbour is lower or stands back)
        side_a, side_b = Frame(fr.p(0, 0, 0), fr.p(0, -h["depth"], 0)), Frame(fr.p(length, -h["depth"], 0), fr.p(length, 0, 0))
        side_ops = list(h.get("side_a_openings", []))
        for sf, sops in ((side_a, side_ops), (side_b, [])):
            sholes = [x for x in (self.hole(op) for op in sops) if x]
            self.wall_grid(wall, sf, 0.0, sf.length, 0.0, eaves, sholes, column, du=1.0 if (lod or not sops) else 0.3,
                           dz=1.2)
            for i, op in enumerate(sops):
                if sf is side_b:
                    continue
                self.opening(name + "_Side", i, op, sf, layer, lod, trim)
        # plinth, floor bands, eaves
        if not lod:
            self.fbox(wall, fr, 0.0, length, 0.0, 0.035, 0.0, 0.45, self.lit(mul(trim_base, 0.8), PAL["cafe_warm_deep"], 0.03), "ft")
            zf = 0.0
            for fh in h["floors"][:-1]:
                zf += fh
                if not cafe:
                    self.fbox(wall, fr, 0.0, length, 0.0, 0.04, zf - 0.07, zf + 0.05, trim, "ftk")
        self.fbox(wall, fr, 0.0, length, 0.0, 0.06, eaves - 0.44, eaves - 0.3, trim, "flrk")
        self.fbox(wall, fr, -0.04, length + 0.04, 0.0, 0.14, eaves - 0.3, eaves - 0.12, trim, "flrk")
        self.fbox(wall, fr, -0.08, length + 0.08, 0.0, 0.3, eaves - 0.12, eaves, trim, "flrtk")
        # roof: one slope rising from the eaves, in rows
        roof_cols = max(2, int(length / (1.5 if lod else 0.7)))
        rows = 2 if lod else 5
        run, rise = 3.6, 1.9
        for i in range(roof_cols):
            ua, ub = length * i / roof_cols, length * (i + 1) / roof_cols
            for j in range(rows):
                va, vb = 0.3 - run * (j + 1) / rows, 0.3 - run * j / rows
                za, zb = eaves + rise * (j + 1) / rows, eaves + rise * j / rows
                col = self.lit(mix(PAL["roof"], PAL["roof_light"], self.rng.random()), PAL["roof_light"], 0.08)
                stone.poly([fr.p(ua, va, za), fr.p(ua, vb, zb), fr.p(ub, vb, zb), fr.p(ub, va, za)], col)
        for u in h.get("chimneys", []):
            cc = self.lit(mul(cool, 0.95), PAL["house_warm"], 0.05)
            base = eaves + rise * (1.3 - 0.3) / run * 0.9
            self.fbox(wall, fr, u - 0.32, u + 0.32, -1.7, -1.0, base, eaves + 2.9, cc, "fblr")
            self.fbox(wall, fr, u - 0.38, u + 0.38, -1.76, -0.94, eaves + 2.9, eaves + 3.02, trim, "fblrtk")
            if not lod:
                pot = self.lit(PAL["pot"], PAL["pot"], 0.1)
                for du_ in (-0.15, 0.15):
                    self.fbox(stone, fr, u + du_ - 0.09, u + du_ + 0.09, -1.44, -1.26, eaves + 3.02, eaves + 3.4, pot, "fblrt")

    def shop_bay(self):
        s, bay = self.L["street"], self.L["shop_bay"]
        x, d, c, kh = s["right_wall_x"], bay["depth"], bay["chamfer"], s["kerb_height"]
        y0, y1 = bay["y"]
        hgt = bay["height"]
        poly = [(x, y0), (x - d + c * 0.6, y0), (x - d, y0 + c), (x - d, y1 - c), (x - d + c * 0.6, y1), (x, y1)]
        wood, glow = self.buf("Near", "Wood"), self.buf("Near", "Glow")
        green = lambda k=1.0: self.lit(mul(PAL["shop_green"], k), PAL["shop_green_light"], 0.06)  # noqa: E731
        pts = poly[::-1]
        for i in range(len(pts) - 1):
            fr = Frame(pts[i], pts[i + 1])
            ln = fr.length
            post = 0.09
            z_glass0, z_glass1 = kh + 0.85, hgt - 0.6
            self.fbox(wood, fr, 0, ln, -0.05, 0.0, kh, z_glass0, green(), "f")
            self.fbox(wood, fr, 0.08, ln - 0.08, 0.0, 0.02, kh + 0.14, z_glass0 - 0.12, green(1.2), "flrtk")
            self.fbox(wood, fr, 0, ln, -0.05, 0.0, z_glass1, hgt, green(), "f")
            self.fbox(wood, fr, 0.06, ln - 0.06, 0.0, 0.015, z_glass1 + 0.1, hgt - 0.12, self.lit(PAL["lit_deep"], PAL["lit"]), "f")
            self.glass(glow, fr, 0, ln, z_glass0, z_glass1, -0.03, True)
            bars = max(1, int(round(ln / 0.6)))
            for k in range(bars + 1):
                um = min(max(ln * k / bars, post / 2), ln - post / 2)
                self.fbox(wood, fr, um - post / 2, um + post / 2, -0.03, 0.03, kh, hgt, green(1.1), "flr")
            zm = z_glass0 + (z_glass1 - z_glass0) * 0.7
            self.fbox(wood, fr, 0, ln, -0.03, 0.015, zm - 0.02, zm + 0.02, green(1.1), "ftk")
            self.fbox(wood, fr, -0.05, ln + 0.05, 0.0, 0.1, hgt, hgt + 0.12, green(1.15), "flrtk")
            self.fbox(wood, fr, -0.1, ln + 0.1, 0.0, 0.18, hgt + 0.12, hgt + 0.22, green(1.25), "flrtk")
        wood.poly([(px, py, hgt + 0.22) for px, py in poly], green(0.8))

    # ---- ground ----

    @staticmethod
    def inside(poly, x, y):
        hit = False
        n = len(poly)
        for i in range(n):
            x0, y0 = poly[i]
            x1, y1 = poly[(i + 1) % n]
            if (y0 > y) != (y1 > y) and x < x0 + (x1 - x0) * (y - y0) / (y1 - y0):
                hit = not hit
        return hit

    def stones(self, b, poly, keep, z, row, wmin, wmax, height, fine, cool_set, warm_set, bounds):
        """Rows of cobbles across the street, each stone its own low dome. fine: 8-sided with a bevel ring."""
        x_lo, x_hi, y_lo, y_hi = bounds
        y = y_lo
        count = 0
        while y < y_hi:
            rh = row * (0.88 + 0.24 * self.rng.random())
            x = x_lo - self.rng.random() * wmax
            while x < x_hi:
                wd = wmin + (wmax - wmin) * self.rng.random()
                cx, cy = x + wd / 2, y + rh / 2 + (self.rng.random() - 0.5) * rh * 0.12
                x += wd
                if not self.inside(poly, cx, cy) or not keep(cx, cy):
                    continue
                count += 1
                a, c = wd / 2 - 0.012, rh / 2 - 0.012
                hz = z + height * (0.7 + 0.6 * self.rng.random())
                wm = self.warm((cx, cy, z)) * (0.75 + 0.5 * self.rng.random())
                base = mix(mix(self.rng.choice(cool_set), PAL["house_deep"], 0.45 * self.deep(cy)),
                           self.rng.choice(warm_set), wm)
                base = mul(base, 0.88 + 0.24 * self.rng.random())
                base = toned("pave", (base[0] * 1.25, base[1] * 1.2, base[2] * 1.05))
                top, rim = mul(base, 1.12), mul(base, 0.72)
                rot = (self.rng.random() - 0.5) * 0.16
                cr, sr = math.cos(rot), math.sin(rot)

                def at(px, py, pz):
                    return (cx + px * cr - py * sr, cy + px * sr + py * cr, pz)

                if fine:
                    k = 0.42                                     # corner cut of the rounded rectangle
                    outer = [(-a, -c + c * k), (-a + a * k, -c), (a - a * k, -c), (a, -c + c * k),
                             (a, c - c * k), (a - a * k, c), (-a + a * k, c), (-a, c - c * k)]
                    inner = [(px * 0.66, py * 0.66) for px, py in outer]
                    b.poly([at(px, py, hz) for px, py in inner], top)
                    for i in range(8):
                        j = (i + 1) % 8
                        b.poly([at(*outer[i], z), at(*outer[j], z), at(*inner[j], hz), at(*inner[i], hz)], rim if i % 2 else base)
                else:
                    outer = [(-a, -c), (a, -c), (a, c), (-a, c)]
                    inner = [(px * 0.7, py * 0.7) for px, py in outer]
                    b.poly([at(px, py, hz) for px, py in inner], top)
                    for i in range(4):
                        j = (i + 1) % 4
                        b.poly([at(*outer[i], z), at(*outer[j], z), at(*inner[j], hz), at(*inner[i], hz)], rim if i % 2 else base)
            y += rh
        return count

    def loaves(self, b, poly, keep, z, bounds):
        """The near cobbles as in the target's close-up: big, tight, uneven, rounded like flat loaves.
        With a Cobble swatch each stone carries one painted stone of the picture; otherwise the palette."""
        x_lo, x_hi, y_lo, y_hi = bounds
        csw = self.sw.get("Cobble")
        seg, rings = 12, ((1.0, 0.0), (0.96, 0.55), (0.84, 0.9), (0.5, 1.0))
        y = y_lo
        count = 0
        while y < y_hi:
            rh = 0.18 + 0.08 * self.rng.random()
            x = x_lo - self.rng.random() * 0.4
            wob = self.rng.random() * 6.28
            while x < x_hi:
                wd = 0.21 + 0.15 * self.rng.random()
                cx = x + wd / 2
                cy = y + rh / 2 + 0.04 * math.sin(cx * 1.7 + wob) + (self.rng.random() - 0.5) * 0.03
                x += wd
                if not self.inside(poly, cx, cy) or not keep(cx, cy):
                    continue
                count += 1
                a, c = wd / 2 + 0.004, rh / 2 + 0.004            # neighbours just touch: the joints are thin and dark
                hgt = 0.011 + 0.008 * self.rng.random()
                rot = (self.rng.random() - 0.5) * 0.3
                cr, sr = math.cos(rot), math.sin(rot)
                # the same stones the whole length of the road; only the mesh gets lighter with distance from where
                # the visitor stands and walks by the terrace
                near = -4.5 < cy < 7.0
                seg, rings = (8, ((1.0, 0.0), (0.95, 0.6), (0.72, 0.94), (0.4, 1.0))) if near else \
                             (6, ((1.0, 0.0), (0.9, 0.7), (0.5, 1.0))) if cy < 24.5 else \
                             (5, ((1.0, 0.0), (0.7, 1.0)))
                if len(rings) < 4:
                    near = False
                lump = [1.0 + (self.rng.random() - 0.5) * 0.16 for _ in range(seg)]
                if csw:
                    wn = self.rng.choice(csw["windows"])
                    flip = self.rng.random() < 0.5
                    k = 0.88 + 0.24 * self.rng.random()
                    k *= 1.15                                   # the night light is dim and blue: the stones' own paint is lighter, warmer
                    base = toned("cobble", (1.06 * k, k * (0.97 + 0.06 * self.rng.random()), 0.86 * k))
                else:
                    wm = self.warm((cx, cy, z)) * (0.75 + 0.5 * self.rng.random())
                    base = mix(mix(self.rng.choice(COBBLE_COOL), PAL["house_deep"], 0.45 * self.deep(cy)),
                               self.rng.choice(COBBLE_WARM), wm)
                    base = mul(base, 0.88 + 0.24 * self.rng.random())
                verts, cols, uvs, faces = [], [], [], []
                for ri, (rk, zk) in enumerate(rings):
                    for i in range(seg):
                        ang = 2 * math.pi * i / seg
                        # a rounded rectangle (superellipse), a little lumpy
                        ca, sa = math.cos(ang), math.sin(ang)
                        px = a * rk * lump[i] * (abs(ca) ** 0.6) * (1 if ca >= 0 else -1)
                        py = c * rk * lump[i] * (abs(sa) ** 0.6) * (1 if sa >= 0 else -1)
                        verts.append((cx + px * cr - py * sr, cy + px * sr + py * cr, z + hgt * zk))
                        if csw:
                            fu, fv = 0.5 + px / (2 * a) * 0.98, 0.5 + py / (2 * c) * 0.98
                            if flip:
                                fu = 1 - fu
                            uvs.append((wn[0] + (wn[2] - wn[0]) * fu, wn[1] + (wn[3] - wn[1]) * fv))
                            cols.append(mul(base, 0.8 if ri == 0 else 1.0))
                        else:
                            uvs.append((verts[-1][0] / b.tile, verts[-1][1] / b.tile))
                            cols.append(mul(base, (0.62, 0.86, 1.0, 1.12)[min(3, ri + (0 if near or ri == 0 else 1))]))
                verts.append((cx, cy, z + hgt))
                cols.append(base if csw else mul(base, 1.14))
                uvs.append(((wn[0] + wn[2]) / 2, (wn[1] + wn[3]) / 2) if csw else (cx / b.tile, cy / b.tile))
                top = len(verts) - 1
                for ri in range(len(rings) - 1):
                    for i in range(seg):
                        j = (i + 1) % seg
                        faces.append((ri * seg + i, ri * seg + j, (ri + 1) * seg + j, (ri + 1) * seg + i))
                last = (len(rings) - 1) * seg
                for i in range(seg):
                    faces.append((last + i, last + (i + 1) % seg, top))
                b.mesh(verts, faces, cols, uvs)
            y += rh
        b.smooth = True
        return count

    def base_plane(self, b, x0, x1, y0, y1, z, step=1.0):
        joint = lambda p: mix(mix(PAL["joint"], PAL["house_deep"], 0.4 * self.deep(p[1])), PAL["joint_warm"], self.warm(p))  # noqa: E731
        nx, ny = max(1, int((x1 - x0) / step)), max(1, int((y1 - y0) / step))
        for i in range(nx):
            for j in range(ny):
                xa, xb = x0 + (x1 - x0) * i / nx, x0 + (x1 - x0) * (i + 1) / nx
                ya, yb = y0 + (y1 - y0) * j / ny, y0 + (y1 - y0) * (j + 1) / ny
                b.poly([(xa, ya, z), (xb, ya, z), (xb, yb, z), (xa, yb, z)], joint)

    def kerb_line(self, b, pts, kh):
        """Kerb stones along a polyline; the road is on the right of the direction of travel."""
        for i in range(len(pts) - 1):
            fr = Frame(pts[i], pts[i + 1])
            u = 0.0
            while u < fr.length - 1e-6:
                ln = min(0.55 + 0.4 * self.rng.random(), fr.length - u)
                col = self.lit(mul(PAL["pave_cool"], 0.85 + 0.3 * self.rng.random()), PAL["pave_warm"], 0.05)
                self.fbox(b, fr, u + 0.006, u + ln - 0.006, -0.24, 0.0, 0.0, kh + 0.012, col, "ftlr")
                u += ln

    def ground(self):
        s, t = self.L["street"], self.L["terrace"]
        kh, by = s["kerb_height"], s["back_wall_y"]
        wx, rx, end = s["cafe_wall_x"], s["right_wall_x"], s["near_end_y"]
        pave_y = by + s["back_pavement_depth"]
        cob_near, cob_mid, stone = self.buf("Near", "Stone"), self.buf("NearMid", "Stone"), self.buf("NearPave", "Stone")
        loaf = self.buf("Near", "Cobble")
        rk, lk = s["right_kerb"], s["left_kerb"]
        road = [(wx, pave_y), (wx, lk[0][1])] + [tuple(p) for p in lk] + [tuple(p) for p in rk[::-1]]
        tr = self.L["tree"]["pos"]
        ring_r = self.L["sizes"]["tree"]["ring_r"]

        def keep(x, y):
            if t["x"][0] <= x <= t["x"][1] and t["y"][0] <= y <= t["y"][1]:
                return False
            return (x - tr[0]) ** 2 + (y - tr[1]) ** 2 > ring_r ** 2

        split = 12.0
        self.base_plane(cob_near, wx - 0.5, rx + 0.5, by, split, -0.004)
        self.base_plane(cob_mid, wx - 0.5, rx + 0.5, split, end, -0.004)
        n1 = self.loaves(loaf, road, keep, 0.0, (wx, rx, pave_y, end))    # one kind of cobble, to the end of the street
        n2 = 0
        print("STONES|near %d|mid %d" % (n1, n2))
        # pavements: a raised bed, flat setts on it, kerb stones along the road
        pave_cool = [mul(PAL["pave_cool"], k) for k in (0.85, 1.0, 1.12)] + [COBBLE_COOL[1], COBBLE_COOL[4]]
        pave_warm = [PAL["pave_warm"], COBBLE_WARM[0]]
        bed = lambda p: mix(mix(PAL["joint"], PAL["house_deep"], 0.4 * self.deep(p[1])), PAL["joint_warm"], self.warm(p))  # noqa: E731
        rf, lf = s["right_front"], s["left_front"]
        for kerb, front in ((rk, rf), (lk, lf)):
            for i in range(len(kerb) - 1):
                quad = [tuple(kerb[i]), tuple(kerb[i + 1]), tuple(front[i + 1]), tuple(front[i])]
                if kerb is rk:
                    quad = quad[::-1]
                    quad = [quad[3], quad[2], quad[1], quad[0]] if False else quad
                # make it face up whatever the order
                ax, ay = quad[1][0] - quad[0][0], quad[1][1] - quad[0][1]
                bx, by_ = quad[2][0] - quad[0][0], quad[2][1] - quad[0][1]
                if ax * by_ - ay * bx < 0:
                    quad = quad[::-1]
                stone.poly([(q[0], q[1], kh - 0.004) for q in quad], bed)
            poly = [tuple(p) for p in kerb] + [tuple(p) for p in front[::-1]]
            xs, ys = [p[0] for p in poly], [p[1] for p in poly]
            self.stones(stone, poly, lambda x, y: True, kh, 0.26, 0.3, 0.5, 0.012, False, pave_cool, pave_warm,
                        (min(xs), max(xs), min(ys), max(ys)))
        stone.poly([(wx, by, kh - 0.004), (rk[0][0], by, kh - 0.004), (rk[0][0], pave_y, kh - 0.004), (wx, pave_y, kh - 0.004)], bed)
        back_poly = [(wx, by), (rk[0][0], by), (rk[0][0], pave_y), (wx, pave_y)]
        self.stones(stone, back_poly, lambda x, y: True, kh, 0.26, 0.3, 0.5, 0.012, False, pave_cool, pave_warm,
                    (wx, rk[0][0], by, pave_y))
        self.kerb_line(stone, [tuple(p) for p in rk[::-1]], kh)
        self.kerb_line(stone, [tuple(p) for p in lk], kh)
        self.kerb_line(stone, [(rk[0][0], pave_y), (wx, pave_y)], kh)
        # the ring of stones round the tree
        n = 14
        for i in range(n):
            a0, a1 = 2 * math.pi * i / n + 0.02, 2 * math.pi * (i + 1) / n - 0.02
            col = mul(mix(self.rng.choice(pave_cool), PAL["pave_warm"], self.warm((tr[0], tr[1], 0))), 0.9 + 0.2 * self.rng.random())
            ri, ro = ring_r - 0.22, ring_r
            pts = [(tr[0] + r * math.cos(a), tr[1] + r * math.sin(a)) for r, a in ((ri, a0), (ro, a0), (ro, a1), (ri, a1))]
            stone.poly([(p[0], p[1], 0.13) for p in pts], mul(col, 1.1))
            stone.poly([(pts[1][0], pts[1][1], 0.0), (pts[2][0], pts[2][1], 0.0), (pts[2][0], pts[2][1], 0.13),
                        (pts[1][0], pts[1][1], 0.13)], mul(col, 0.8))
            stone.poly([(pts[3][0], pts[3][1], 0.0), (pts[0][0], pts[0][1], 0.0), (pts[0][0], pts[0][1], 0.13),
                        (pts[3][0], pts[3][1], 0.13)], mul(col, 0.7))
        stone.poly([(tr[0] + (ring_r - 0.22) * math.cos(2 * math.pi * i / n), tr[1] + (ring_r - 0.22) * math.sin(2 * math.pi * i / n), 0.05)
                    for i in range(n)], self.lit(PAL["joint"], PAL["joint_warm"]))
        self.terrace()

    def terrace(self):
        t = self.L["terrace"]
        (x0, x1), (y0, y1), hgt, pw = t["x"], t["y"], t["height"], t["plank_width"]
        wood = self.buf("Near", "Wood")
        plank = self.buf("Near", "Plank")
        psw = self.sw.get("Plank")
        dark = self.lit(mul(PAL["plank_dark"], 0.5), mul(PAL["plank_dark"], 0.8))
        wood.poly([(x0, y0, hgt - 0.012), (x1, y0, hgt - 0.012), (x1, y1, hgt - 0.012), (x0, y1, hgt - 0.012)], dark)
        n = int(round((x1 - x0) / pw))
        w = (x1 - x0) / n
        gap = 0.005
        for i in range(n):
            xa, xb = x0 + w * i + gap, x0 + w * (i + 1) - gap
            cuts = [y0, y0 + (y1 - y0) * (0.3 + 0.4 * self.rng.random()), y1]
            if self.rng.random() < 0.5:
                cuts.insert(2, cuts[1] + (y1 - cuts[1]) * (0.4 + 0.3 * self.rng.random()))
            for j in range(len(cuts) - 1):
                ya, yb = cuts[j] + gap, cuts[j + 1] - gap
                k = 0.86 + 0.28 * self.rng.random()
                cool, warm_col = mul(PAL["plank_cool"], k), mul(mix(PAL["plank_dark"], PAL["plank"], 0.35 + 0.65 * self.rng.random()), k)
                col = lambda p, c=cool, wc=warm_col: mix(c, wc, 0.35 + 0.65 * self.own(p))  # noqa: E731
                seg = max(1, int((yb - ya) / self.plank_seg))
                strip = self.rng.choice(psw["strips"]) if psw else None
                v_off = self.rng.random()
                for q in range(seg):
                    sa, sb = ya + (yb - ya) * q / seg, ya + (yb - ya) * (q + 1) / seg
                    uvs = None
                    if strip:
                        va, vb = v_off + sa / psw["length_m"], v_off + sb / psw["length_m"]
                        uvs = [(strip[0], va), (strip[1], va), (strip[1], vb), (strip[0], vb)]
                        col = toned("plank", (1.32 * k, 1.32 * k, 1.05 * k))
                    plank.poly([(xa, sa, hgt), (xb, sa, hgt), (xb, sb, hgt), (xa, sb, hgt)], col, uvs)
                    # the board's own thickness shows in the gaps
                    plank.poly([(xb, sa, hgt - 0.012), (xb, sb, hgt - 0.012), (xb, sb, hgt), (xb, sa, hgt)], mul(col, 0.5) if psw else dark,
                               [(strip[1], 0), (strip[1], 0), (strip[1], 0), (strip[1], 0)] if strip else None)
        # the edge: a board over a course of bricks, on the three open sides
        stone = self.buf("NearPave", "Stone")
        for a, b_ in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1))):
            fr = Frame(a, b_)
            self.fbox(wood, fr, 0.0, fr.length, -0.02, 0.012, hgt - 0.035, hgt + 0.001,
                      self.lit(mul(PAL["plank_dark"], 0.9), PAL["plank"], 0.05), "ft")
            u = 0.0
            while u < fr.length - 1e-6:
                ln = min(0.2 + 0.06 * self.rng.random(), fr.length - u)
                col = self.lit(mul(mix(PAL["plank_dark"], PAL["pot"], self.rng.random()), 0.7 + 0.4 * self.rng.random()),
                               mix(PAL["plank"], PAL["pot"], 0.4), 0.05)
                self.fbox(stone, fr, u + 0.004, u + ln - 0.004, -0.02, 0.0, 0.0, hgt - 0.035, col, "f")
                u += ln

    # ---- cafe ----

    def awning(self):
        aw, s = self.L["awning"], self.L["street"]
        wx, (y0, y1) = s["cafe_wall_x"], aw["y"]
        ox, wz, oz = aw["outer_x"], aw["wall_z"], aw["outer_z"]
        cloth, iron = self.buf("Near", "Cloth"), self.buf("Near", "Iron")
        rods = aw["rods_y"]
        span = (rods[-1] - rods[0]) / (len(rods) - 1)

        def at(sv, y):
            sag = 0.10 * math.sin(math.pi * sv) + 0.05 * sv * abs(math.sin(math.pi * (y - rods[0]) / span))
            return (wx + sv * (ox - wx), y, wz + sv * (oz - wz) - sag)

        s_cuts = [0, 0.14, 0.28, 0.42, 0.56, 0.68, 0.76, 0.768, 0.83, 0.838, 0.90, 0.908, 1.0]
        stripes = {7, 9, 11}                                   # thin green lines near the outer edge
        ny = 26
        for i in range(len(s_cuts) - 1):
            sa, sb = s_cuts[i], s_cuts[i + 1]
            stripe = (i + 1) in stripes
            for j in range(ny):
                ya, yb = y0 + (y1 - y0) * j / ny, y0 + (y1 - y0) * (j + 1) / ny
                k = 0.93 + 0.14 * self.rng.random()
                if stripe:
                    top = under = self.lit(mul(PAL["awning_stripe"], k), PAL["awning_stripe"])
                else:
                    cool, wc, uc = mul(PAL["awning_cool"], k), mul(PAL["awning"], k), mul(PAL["awning_under"], k)
                    top = lambda p, c=cool, w_=wc: mix(c, w_, 0.55 + 0.45 * self.own((p[0], p[1], 2.0)))  # noqa: E731
                    under = lambda p, c=cool, w_=uc: mix(c, w_, 0.3 + 0.7 * self.own((p[0], p[1], 2.0)))  # noqa: E731
                quad = [at(sa, ya), at(sb, ya), at(sb, yb), at(sa, yb)]
                cloth.poly(quad, top)
                cloth.poly(quad[::-1], under)
        # scalloped valance
        nv = int(round((y1 - y0) / 0.32))
        for j in range(nv):
            ya, yb = y0 + (y1 - y0) * j / nv, y0 + (y1 - y0) * (j + 1) / nv
            ym = (ya + yb) / 2
            pa, pb, pm = at(1.0, ya), at(1.0, yb), at(1.0, ym)
            drop = aw["valance"]
            col = self.lit(mul(PAL["awning_cool"], 0.95), mul(PAL["awning"], 0.92 + 0.12 * self.rng.random()))
            sh = lambda p, c=col: c((p[0], p[1], 2.0))  # noqa: E731
            self.both(cloth, [pa, pb, (pb[0], pb[1], pb[2] - drop * 0.7), (pm[0], pm[1], pm[2] - drop),
                              (pa[0], pa[1], pa[2] - drop * 0.7)], sh)
        ironcol = self.lit(PAL["iron"], PAL["iron_warm"])
        self.rod(iron, at(1.0, y0), at(1.0, y1), 0.018, ironcol)
        for y in rods:
            self.rod(iron, (wx, y, aw["rod_wall_z"]), (ox, y, oz + 0.02), 0.014, ironcol)
            self.rod(iron, (wx + 0.02, y, wz), (ox, y, oz + 0.02), 0.012, ironcol)

    def balcony(self):
        b, s = self.L["balcony"], self.L["street"]
        wx, (y0, y1) = s["cafe_wall_x"], b["y"]
        z, d, rh = b["floor_z"], b["depth"], b["rail_height"]
        fr = Frame((wx, y0), (wx, y1))
        wall, iron = self.buf("Near", "Wall"), self.buf("Near", "Iron")
        trim = self.lit(PAL["cafe_cool"], PAL["trim_warm"], 0.03)
        self.fbox(wall, fr, 0.0, fr.length, 0.0, d, z - 0.12, z, trim, "flrtk")
        ironcol = self.lit(PAL["iron"], PAL["shutter_dark"])
        self.fbox(iron, fr, 0.0, fr.length, d - 0.05, d - 0.01, z + rh - 0.04, z + rh, ironcol, "fbtk")
        self.fbox(iron, fr, 0.0, fr.length, d - 0.045, d - 0.015, z + 0.08, z + 0.11, ironcol, "fbtk")
        n = int(round(fr.length / 0.13))
        for i in range(n + 1):
            u = fr.length * i / n
            self.fbox(iron, fr, u - 0.009, u + 0.009, d - 0.039, d - 0.021, z, z + rh - 0.04, ironcol, "fblr")
        for u0 in (0.0, fr.length):
            end = Frame(fr.p(u0, 0, 0), fr.p(u0, d, 0))
            self.fbox(iron, end, 0.0, d, -0.02, 0.02, z + rh - 0.04, z + rh, ironcol, "fbtk")
            m = int(round(d / 0.13))
            for i in range(m):
                v = d * i / m
                self.fbox(iron, end, v - 0.009, v + 0.009, -0.009, 0.009, z, z + rh - 0.04, ironcol, "fblr")
        for u in [fr.length * k / 4 for k in range(5)]:
            self.fbox(wall, fr, max(0.0, u - 0.05), min(fr.length, u + 0.05) if u > 0.05 else 0.1, 0.0, d * 0.7, z - 0.42, z - 0.12, trim, "flrk")

    # ---- far layer ----

    def far(self):
        f = self.L["far"]
        n, y0, ln, width = f["houses_per_side"], f["start_y"], f["length"], f["width"]

        def centre(y):
            k = (y - y0) / ln
            return f["bend_x"] * k * k

        for side in (-1, 1):
            for i in range(n):
                ya, yb = y0 + ln * i / n, y0 + ln * (i + 1) / n
                pa, pb = (centre(ya) + side * width / 2, ya), (centre(yb) + side * width / 2, yb)
                a, b_ = (pa, pb) if side < 0 else (pb, pa)
                floors = [3.3, 3.1] + ([1.0 + 0.4 * ((i * 5 + 1) % 3)] if (i + (side > 0)) % 2 else [])
                h = {"name": "Far_%s_%02d" % ("L" if side < 0 else "R", i), "a": a, "b": b_, "depth": 5.0, "floors": floors,
                     "chimneys": [1.2 + (i % 3) * 0.9], "auto": {"spacing": 2.2, "lit": [(i * 3 + 1) % 7] if i % 2 == 0 else []}}
                self.house(h, "Far", 1)
        yend = y0 + ln
        cx = centre(yend)
        for k in range(3):
            xa = cx - 9 + 6 * k
            h = {"name": "Far_End_%d" % k, "a": (xa, yend), "b": (xa + 6, yend), "depth": 4.0,
                 "floors": [3.4, 3.2, 0.8 * (k % 2)], "chimneys": [2.0], "auto": {"spacing": 2.0, "lit": [2 + k]}}
            self.house(h, "Far", 1)
        tw = f["tower"]
        w, (tx, ty), th = tw["w"] / 2, tw["pos"], tw["height"]
        wall, stone = self.buf("Far", "Wall"), self.buf("Far", "Stone")
        tf = Frame((tx - w, ty - w), (tx + w, ty - w))
        col = self.lit(mul(PAL["house_cool"], 0.8), PAL["house_cool"])
        self.fbox(wall, tf, 0.0, 2 * w, -2 * w, 0.0, 0.0, th, col, "flr")
        self.fbox(wall, tf, -0.15, 2 * w + 0.15, -2 * w - 0.15, 0.15, th, th + 0.25, col, "flrtk")
        self.fbox(wall, tf, 0.35, 2 * w - 0.35, -2 * w + 0.35, -0.35, th + 0.25, th + 2.6, col, "flr")
        tip = (tx, ty, th + 6.0)
        c = [(tx - w * 0.75, ty - w * 0.75), (tx + w * 0.75, ty - w * 0.75), (tx + w * 0.75, ty + w * 0.75), (tx - w * 0.75, ty + w * 0.75)]
        roofcol = self.lit(PAL["roof"], PAL["roof"])
        for i in range(4):
            j = (i + 1) % 4
            stone.poly([(c[i][0], c[i][1], th + 2.6), (c[j][0], c[j][1], th + 2.6), tip], roofcol)
        # the far roadway: the same cobbles as the near street, no change where the layers meet
        cob = self.buf("Far", "Cobbles")
        left = [(centre(y0 + ln * i / n) - width / 2, y0 + ln * i / n) for i in range(n + 1)]
        right = [(centre(y0 + ln * i / n) + width / 2, y0 + ln * i / n) for i in range(n + 1)]
        road = left + right[::-1]
        xs = [p[0] for p in road]
        self.base_plane(cob, min(xs) - 0.5, max(xs) + 0.5, y0, yend, -0.004, step=2.5)
        nf = self.loaves(self.buf("Far", "Cobble"), road, lambda x, y: True, 0.0, (min(xs), max(xs), y0, yend))   # and on
        print("STONES|far %d" % nf)

    # ---- Tripo objects ----

    def load_models(self, folder):
        import json
        with open(os.path.join(HERE, "objects.json")) as f:
            spec = json.load(f)
        for key, sp in spec.items():
            if key.startswith("_"):
                continue
            path = os.path.join(folder, sp["file"])
            if not os.path.exists(path):
                print("MODEL|%s|missing %s" % (key, path))
                continue
            before = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=path)
            new = [o for o in bpy.data.objects if o not in before]
            meshes = [o for o in new if o.type == "MESH"]
            ob = meshes[0]
            me = ob.data
            me.transform(ob.matrix_world)
            for o in new:
                bpy.data.objects.remove(o)
            me.name = "CT_" + key
            xs = [v.co.x for v in me.vertices]
            ys = [v.co.y for v in me.vertices]
            zs = [v.co.z for v in me.vertices]
            cx, cy, z0 = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)
            for v in me.vertices:                                  # stand it on the origin
                v.co.x -= cx
                v.co.y -= cy
                v.co.z -= z0
            size = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
            for m in me.materials:
                unlit_tinted(m, sp.get("iron"), sp.get("iron_below", 0.0) * size[2], self.lit_look,
                             1.2 if key == "lantern" else 0.0)
            me.calc_loop_triangles()
            info = {"mesh": me, "size": size, "spec": sp, "tris": len(me.loop_triangles)}
            # which way it faces: the chair's back and the lantern's lamp are found from the mesh itself
            hi = [v.co for v in me.vertices if v.co.z > size[2] * 0.7]
            lo = [v.co for v in me.vertices if v.co.z < size[2] * 0.12]
            info["high"] = (sum(c.x for c in hi) / len(hi), sum(c.y for c in hi) / len(hi))
            info["low"] = (sum(c.x for c in lo) / len(lo), sum(c.y for c in lo) / len(lo))
            wide = [v.co.x for v in me.vertices if abs(v.co.y) > size[1] * 0.3]
            info["wide"] = sum(wide) / len(wide)
            body = [v.co for v in me.vertices if abs(v.co.y) > size[1] * 0.3]
            info["body"] = (sum(c.x for c in body) / len(body), sum(c.z for c in body) / len(body))
            self.models[key] = info
            print("MODEL|%s|%d tris|%.2f x %.2f x %.2f m as made" % ((key, info["tris"]) + size))

    def place(self, key, name, pos, rot_z, scale):
        info = self.models[key]
        ob = bpy.data.objects.new(name, info["mesh"])
        ob.location = pos
        ob.rotation_euler = (0.0, 0.0, rot_z)
        ob.scale = scale
        w = self.warm((pos[0], pos[1], pos[2] + 0.5))
        ob.color = (1.0, 1.0, 1.0, 1.0) if self.lit_look else (*mix((0.62, 0.66, 0.98), (1.12, 1.0, 0.78), w), 1.0)
        self.col("Objects").objects.link(ob)
        self.tris["Objects"] = self.tris.get("Objects", 0) + info["tris"]
        return ob

    def table(self, t):
        if "table" not in self.models:
            return super().table(t)
        info, sz = self.models["table"], self.L["sizes"]["table"]
        k = sz["diameter"] / max(info["size"][0], info["size"][1])
        self.place("table", t["name"].replace("PH_", ""), (t["pos"][0], t["pos"][1], self.base_z(t.get("on"))),
                   self.rng.random() * 6.28, (k, k, sz["height"] / info["size"][2]))

    def chair(self, name, x, y, z, yaw_deg):
        if "chair" not in self.models:
            return super().chair(name, x, y, z, yaw_deg)
        info, sz = self.models["chair"], self.L["sizes"]["chair"]
        k = sz["back_height"] / info["size"][2]
        bx, by = info["high"]                                    # the hoop back is the high part
        faces = math.atan2(-bx, -by)                             # the way a sitter faces, as made
        jitter = (self.rng.random() - 0.5) * 0.12 if self.chair_jitter else 0.0   # only the plan's chairs get a hand's turn of chance
        self.place("chair", name.replace("PH_", ""), (x, y, z), faces - math.radians(yaw_deg) + jitter, (k, k, k))

    def lantern(self):
        if "lantern" not in self.models:
            return super().lantern()
        info, ln, s = self.models["lantern"], self.L["lantern"], self.L["street"]
        k = ln["reach"] / info["size"][0]
        rot = 0.0 if info["wide"] > 0 else math.pi               # the lamp is the wide end: it goes to the street side
        x = s["cafe_wall_x"] + info["size"][0] * k / 2
        ob = self.place("lantern", "Lantern", (x, ln["wall_y"], ln["z"] - 0.45), rot, (k, k, k))
        ob.color = (1.0, 1.0, 1.0, 1.0) if self.lit_look else (1.15, 1.05, 0.85, 1.0)
        ob.visible_shadow = False                                # the lamp must not block its own light
        # the flame inside: the panes are see-through (unlit_tinted), so this is what is seen glowing in them
        bx, bz = info["body"]
        fx = x + bx * k * (1 if rot == 0.0 else -1)
        self.flame = (fx, ln["wall_y"], ln["z"] - 0.45 + bz * k)
        fm = bpy.data.materials.new("CT_Flame")
        fm.use_nodes = True
        fm.node_tree.nodes.clear()
        fo = fm.node_tree.nodes.new("ShaderNodeOutputMaterial")
        fe = fm.node_tree.nodes.new("ShaderNodeEmission")
        fe.inputs["Color"].default_value = (*srgb("#ffd67a"), 1.0)
        fe.inputs["Strength"].default_value = 40.0 if self.lit_look else 1.0
        fm.node_tree.links.new(fe.outputs[0], fo.inputs["Surface"])
        flame = self.ball("Lantern_Flame", self.flame, (0.035, 0.035, 0.075), "lit", "Objects")
        flame.data.materials.clear()
        flame.data.materials.append(fm)
        flame.visible_shadow = False

    # ---- assemble ----

    def emit(self):
        mats = {}
        for key, b in sorted(self.bufs.items()):
            layer, material = key.rsplit("_", 1)
            if material == "Cobbles":
                material = "Stone"
            if material not in mats:
                mats[material] = make_material(material, self.lit_look, self.sw.get(material), self.sw_dir,
                                               self.L.get("light", {}).get("window_glow", 2.0))
            me = bpy.data.meshes.new("CT_" + key)
            me.from_pydata(b.v, [], b.f)
            col = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
            flat = []
            for c in b.c:
                flat.extend((c[0], c[1], c[2], 1.0))
            col.data.foreach_set("color", flat)
            uvl = me.uv_layers.new(name="UVMap")
            uvs = []
            for f in b.f:
                for vi in f:
                    uvs.extend(b.uv[vi])
            uvl.data.foreach_set("uv", uvs)
            me.materials.append(mats[material])
            if b.smooth:
                for poly in me.polygons:
                    poly.use_smooth = True
            me.update()
            ob = bpy.data.objects.new("CT_" + key, me)
            self.col("Far" if layer == "Far" else "Near").objects.link(ob)
            self.tris["Set:" + key] = b.tris()

    def build(self):
        self.ground()
        for h in self.L["houses"]:
            self.house(h)
        self.shop_bay()
        self.awning()
        self.balcony()
        self.far()
        self.emit()
        if self.objects_dir:
            self.load_models(self.objects_dir)
        self.furniture_and_people()
        self.lantern()
        self.tree()
        self.easel()
        self.carriage()
        self.markers()


SATURATION = 1.0          # set from the layout's light.saturation


def less_saturated(nt, socket):
    if SATURATION == 1.0:
        return socket
    hs = nt.nodes.new("ShaderNodeHueSaturation")
    hs.inputs["Saturation"].default_value = SATURATION
    nt.links.new(socket, hs.inputs["Color"])
    return hs.outputs["Color"]


def unlit_tinted(m, iron=None, iron_below=0.0, lit=False, glow=0.0):
    """A Tripo material, shown without lights: its own base colour texture x the object's tint.
    iron: below that height (metres, as made) the texture's light and dark are kept but its colour becomes iron."""
    nt = m.node_tree
    images = [n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image]
    named = [n for n in images if "basecolor" in n.image.name.lower()]
    tex = (named or images or [None])[0]
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    em = nt.nodes.new("ShaderNodeEmission")
    info = nt.nodes.new("ShaderNodeObjectInfo")
    mixn = nt.nodes.new("ShaderNodeMix")
    mixn.data_type = "RGBA"
    mixn.blend_type = "MULTIPLY"
    mixn.inputs[0].default_value = 1.0
    if tex is not None:
        source = tex.outputs["Color"]
        if iron:
            coord = nt.nodes.new("ShaderNodeTexCoord")
            sep = nt.nodes.new("ShaderNodeSeparateXYZ")
            below = nt.nodes.new("ShaderNodeMath")
            below.operation = "LESS_THAN"
            below.inputs[1].default_value = iron_below
            bw = nt.nodes.new("ShaderNodeRGBToBW")
            paint = nt.nodes.new("ShaderNodeMix")
            paint.data_type = "RGBA"
            paint.blend_type = "MULTIPLY"
            paint.inputs[0].default_value = 1.0
            paint.inputs[7].default_value = (*mul(srgb(iron), 2.2), 1.0)
            pick = nt.nodes.new("ShaderNodeMix")
            pick.data_type = "RGBA"
            nt.links.new(coord.outputs["Object"], sep.inputs[0])
            nt.links.new(sep.outputs["Z"], below.inputs[0])
            nt.links.new(tex.outputs["Color"], bw.inputs[0])
            nt.links.new(bw.outputs[0], paint.inputs[6])
            nt.links.new(below.outputs[0], pick.inputs[0])
            nt.links.new(tex.outputs["Color"], pick.inputs[6])
            nt.links.new(paint.outputs[2], pick.inputs[7])
            source = pick.outputs[2]
        nt.links.new(source, mixn.inputs[6])
    nt.links.new(info.outputs["Color"], mixn.inputs[7])
    final = less_saturated(nt, mixn.outputs[2])
    m["ct_colour_node"], m["ct_colour_socket"] = final.node.name, final.identifier   # for make_unity_export.py
    nt.links.new(final, em.inputs["Color"])
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    m.use_backface_culling = False
    if lit:
        dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
        nt.links.new(final, dif.inputs["Color"])
        if glow:
            # the lantern: its yellow panes shine, its iron is only lit
            add = nt.nodes.new("ShaderNodeAddShader")
            sep = nt.nodes.new("ShaderNodeSeparateColor")
            gate = nt.nodes.new("ShaderNodeMath")
            gate.operation = "GREATER_THAN"
            gate.inputs[1].default_value = 0.42
            power = nt.nodes.new("ShaderNodeMath")
            power.operation = "MULTIPLY"
            power.inputs[1].default_value = glow
            nt.links.new(mixn.outputs[2], sep.inputs[0])
            nt.links.new(sep.outputs[1], gate.inputs[0])
            nt.links.new(gate.outputs[0], power.inputs[0])
            # panes: half see-through glass with a faint glow of its own; the flame inside does the shining
            em.inputs["Strength"].default_value = glow
            clear = nt.nodes.new("ShaderNodeBsdfTransparent")
            clear.inputs["Color"].default_value = (1.0, 0.93, 0.72, 1.0)
            pane = nt.nodes.new("ShaderNodeMixShader")
            pane.inputs[0].default_value = 0.62
            nt.links.new(clear.outputs[0], pane.inputs[1])
            nt.links.new(em.outputs[0], pane.inputs[2])
            del add, power
            pick = nt.nodes.new("ShaderNodeMixShader")
            nt.links.new(gate.outputs[0], pick.inputs[0])
            nt.links.new(dif.outputs[0], pick.inputs[1])
            nt.links.new(pane.outputs[0], pick.inputs[2])
            nt.links.new(pick.outputs[0], out.inputs["Surface"])
        else:
            nt.links.new(dif.outputs[0], out.inputs["Surface"])


def make_material(name, lit=False, sw=None, sw_dir=None, glow=3.0):
    """Colour = (painted swatch x tint) or (vertex colour x grey streaks x 2). lit: a matt surface under the scene's
    lights with a little relief from the swatch; otherwise it shows its colour as it is (no lights)."""
    swatch, _ = MATERIALS[name]
    m = bpy.data.materials.new("CT_" + name)
    m.use_nodes = True
    m.use_backface_culling = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    col = nt.nodes.new("ShaderNodeVertexColor")
    col.layer_name = "Col"

    def multiply(a, b):
        n = nt.nodes.new("ShaderNodeMix")
        n.data_type = "RGBA"
        n.blend_type = "MULTIPLY"
        n.inputs[0].default_value = 1.0
        nt.links.new(a, n.inputs[6])
        if isinstance(b, tuple):
            n.inputs[7].default_value = b
        else:
            nt.links.new(b, n.inputs[7])
        return n.outputs[2]

    colour, height, strength = col.outputs["Color"], None, 0.0
    if sw:                                                   # a painted colour swatch
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(sw_dir, sw["file"]))
        ref = srgb(sw["ref"]) if "ref" in sw else (1.0, 1.0, 1.0)
        tint = multiply(col.outputs["Color"], (1.0 / ref[0], 1.0 / ref[1], 1.0 / ref[2], 1.0))
        source = tex.outputs["Color"]
        if sw.get("desat"):                                  # pull a too-colourful swatch towards its own grey
            bw = nt.nodes.new("ShaderNodeRGBToBW")
            grey = nt.nodes.new("ShaderNodeMix")
            grey.data_type = "RGBA"
            grey.inputs[0].default_value = sw["desat"]
            nt.links.new(tex.outputs["Color"], bw.inputs[0])
            nt.links.new(tex.outputs["Color"], grey.inputs[6])
            nt.links.new(bw.outputs[0], grey.inputs[7])
            source = grey.outputs[2]
        colour = multiply(source, tint)
        height, strength = tex.outputs["Color"], sw.get("bump", 0.2)
    else:
        path = os.path.join(TEX, swatch) if swatch else None
        if path and os.path.exists(path):
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = bpy.data.images.load(path)
            tex.image.colorspace_settings.name = "Non-Color"
            colour = multiply(multiply(col.outputs["Color"], tex.outputs["Color"]), (2.0, 2.0, 2.0, 1.0))
            height, strength = tex.outputs["Color"], 0.12
    colour = less_saturated(nt, colour)
    if not lit:
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(colour, em.inputs["Color"])
        nt.links.new(em.outputs[0], out.inputs["Surface"])
        return m
    if name == "Glow":
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Strength"].default_value = glow
        nt.links.new(colour, em.inputs["Color"])
        nt.links.new(em.outputs[0], out.inputs["Surface"])
        return m
    dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
    nt.links.new(colour, dif.inputs["Color"])
    if height is not None and strength > 0:
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = strength
        bump.inputs["Distance"].default_value = 0.004
        nt.links.new(height, bump.inputs["Height"])
        nt.links.new(bump.outputs[0], dif.inputs["Normal"])
    nt.links.new(dif.outputs[0], out.inputs["Surface"])
    if name == "WallCafe":
        # painted light: the yellow wall keeps some of its glow where the lamps don't reach (above the awning)
        glow_n = nt.nodes.new("ShaderNodeEmission")
        glow_n.inputs["Strength"].default_value = 0.55
        keep = nt.nodes.new("ShaderNodeMixShader")
        keep.inputs[0].default_value = 0.35
        nt.links.new(colour, glow_n.inputs["Color"])
        nt.links.new(dif.outputs[0], keep.inputs[1])
        nt.links.new(glow_n.outputs[0], keep.inputs[2])
        nt.links.new(keep.outputs[0], out.inputs["Surface"])
    if name == "Cloth":
        # lamp light glows through the canvas
        tr = nt.nodes.new("ShaderNodeEmission")               # the lit canvas glows a little by itself
        tr.inputs["Strength"].default_value = 0.9
        add = nt.nodes.new("ShaderNodeMixShader")
        add.inputs[0].default_value = 0.45
        nt.links.new(colour, tr.inputs["Color"])
        nt.links.new(dif.outputs[0], add.inputs[1])
        nt.links.new(tr.outputs[0], add.inputs[2])
        nt.links.new(add.outputs[0], out.inputs["Surface"])
    return m


def a_few_stars(env):
    """Preview only: a handful of stars (the real sky is the dome in Unity). Seen by the camera, they light nothing."""
    spec = env.L.get("light", {}).get("stars")
    if not spec:
        return
    rng = random.Random(spec.get("seed", 7))
    mats = {}
    for key, colour, strength in (("core", "#ffe9a8", 2.5), ("halo", "#3f66c8", 1.0)):
        m = bpy.data.materials.new("CT_Star_" + key)
        m.use_nodes = True
        m.node_tree.nodes.clear()
        o = m.node_tree.nodes.new("ShaderNodeOutputMaterial")
        e = m.node_tree.nodes.new("ShaderNodeEmission")
        e.inputs["Color"].default_value = (*srgb(colour), 1.0)
        e.inputs["Strength"].default_value = strength
        m.node_tree.links.new(e.outputs[0], o.inputs["Surface"])
        mats[key] = m
    dist = 220.0

    def star(name, az, el, r, key, back=0.0):
        d = dist + back
        c = (d * math.sin(az) * math.cos(el), d * math.cos(az) * math.cos(el), d * math.sin(el))
        ob = env.ball(name, c, (r, r, r), "lit", "Sky")
        ob.data.materials.clear()
        ob.data.materials.append(mats[key])
        for flag in ("visible_diffuse", "visible_glossy", "visible_shadow"):
            setattr(ob, flag, False)

    for i in range(spec.get("small", 26)):
        star("Star_S_%02d" % i, rng.uniform(-math.pi, math.pi), math.radians(rng.uniform(7, 60)), rng.uniform(0.9, 1.3), "core")
    for i in range(spec.get("large", 5)):
        az, el = rng.uniform(-0.45, 0.6), math.radians(rng.uniform(9, 30))       # the big ones low over the street ahead
        star("Star_L_%02d_Halo" % i, az, el, 3.2, "halo", 8.0)      # a soft ring of lighter blue behind it
        star("Star_L_%02d" % i, az, el, 1.5, "core")


def light_scene(env):
    """The lights that get baked: the lantern, a soft bounce under the awning, the night sky. (Lit windows shine by
    their own material.) Numbers are look-dev values, tuned by eye against 01_master_v2."""
    sc = bpy.context.scene
    lx, ly, lz = env.lamp
    lamp = bpy.data.lights.new("CT_Lantern", "POINT")
    lt = env.L.get("light", {})
    lamp.color = srgb(lt.get("lantern_colour", "#ffdc8c"))
    lamp.energy = lt.get("lantern_w", 260.0)
    lamp.shadow_soft_size = 0.3
    ob = bpy.data.objects.new("CT_Lantern_Light", lamp)
    ob.location = getattr(env, "flame", (lx, ly, lz - 0.15))
    sc.collection.objects.link(ob)
    aw = env.L["awning"]
    fill = bpy.data.lights.new("CT_AwningBounce", "AREA")
    fill.color = srgb(lt.get("awning_colour", "#ffd27a"))
    fill.energy = lt.get("awning_w", 110.0)
    fill.shape = "RECTANGLE"
    fill.size, fill.size_y = 2.6, aw["y"][1] - aw["y"][0] + 3.0   # broad and soft: the whole terrace glows evenly
    fo = bpy.data.objects.new("CT_AwningBounce_Light", fill)
    fo.location = ((env.aw_box[0] + env.aw_box[1]) / 2, (aw["y"][0] + aw["y"][1]) / 2, aw["outer_z"] + 0.35)
    sc.collection.objects.link(fo)                           # an area light points down by default


def setup_cycles(layout):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "METAL"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = True
        sc.cycles.device = "GPU"
    except Exception as e:                                   # falls back to the CPU
        print("CYCLES|no GPU: %s" % e)
    lt = layout.get("light", {})
    sc.cycles.samples = int(lt.get("samples", 96))
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 4
    sc.world = bpy.data.worlds.new("CT_World")
    sc.world.use_nodes = True
    nt = sc.world.node_tree
    bg = nt.nodes["Background"]
    bg.inputs["Color"].default_value = (*srgb(lt.get("sky_light", "#7f8fc4")), 1.0)
    bg.inputs["Strength"].default_value = lt.get("sky_strength", 1.1)
    seen = nt.nodes.new("ShaderNodeBackground")
    seen.inputs["Color"].default_value = (*srgb(lt.get("sky", "#1c3a78")), 1.0)
    path = nt.nodes.new("ShaderNodeLightPath")
    pick = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(path.outputs["Is Camera Ray"], pick.inputs[0])
    nt.links.new(bg.outputs[0], pick.inputs[1])
    nt.links.new(seen.outputs[0], pick.inputs[2])
    nt.links.new(pick.outputs[0], nt.nodes["World Output"].inputs["Surface"])
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.exposure = lt.get("exposure", 0.0)
    sc.render.image_settings.file_format = "PNG"
    sc.render.film_transparent = False


def setup_render():
    sc = bpy.context.scene
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = engine
            break
        except TypeError:
            continue
    sc.world = bpy.data.worlds.new("CT_World")
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (*srgb("#1f3f8f"), 1.0)
    sc.view_settings.view_transform = "Standard"
    sc.render.image_settings.file_format = "PNG"
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = 16
    except Exception:
        pass


def main():
    import json
    opts = bo.args_after_dashes()
    with open(os.path.join(HERE, "cafe_layout.json")) as f:
        layout = json.load(f)
    if opts["set"]:
        bo.apply_overrides(layout, opts["set"])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    apply_tone(layout)
    env = Env(layout)
    env.objects_dir = opts.get("objects")
    env.lit_look = opts.get("look") == "lit"
    global SATURATION
    SATURATION = layout.get("light", {}).get("saturation", 1.0) if env.lit_look else 1.0
    if opts.get("swatches"):
        env.sw_dir = os.path.abspath(opts["swatches"])
        with open(os.path.join(env.sw_dir, "swatches.json")) as f:
            env.sw = {k: v for k, v in json.load(f).items() if not k.startswith("_")}
    env.build()
    if env.lit_look:
        setup_cycles(layout)
        light_scene(env)
        a_few_stars(env)
    else:
        setup_render()
    total = 0
    for key, n in sorted(env.tris.items()):
        print("TRIS|%s|%d" % (key, n))
        if key.startswith("Set:"):
            total += n
    print("TRIS|set total (no stand-ins)|%d" % total)
    print("DRAWS|set meshes|%d" % len(env.bufs))
    cams = {name: bo.make_camera(name, spec, layout) for name, spec in layout["cameras"].items()
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
            hide = tuple(spec.get("hide", []))
            for o in bpy.data.objects:
                if o.type == "MESH":
                    o.hide_render = bool(hide) and o.name.startswith(hide)
            for o in env.col("Markers").objects:
                o.hide_render = True
            sc.render.filepath = os.path.join(out, name + ".png")
            bpy.ops.render.render(write_still=True)
            print("RENDER|%s|%s" % (name, sc.render.filepath))


if __name__ == "__main__":
    main()
