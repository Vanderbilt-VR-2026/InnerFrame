"""bake_sign.py - the Tripo sign's front, rendered flat into two transparent PNGs for the caption frame.

Run headless:  Blender -b --factory-startup -P bake_sign.py -- [<caption-frame.glb>] [--out DIR]
Defaults: the GLB Cici exported on 2026-10-27 (in cc-reports), PNGs into Assets/VanGogh/Art/UI. Writes
VG_Sign_A.png (unlit: the texture's own colours), VG_Sign_B.png (a soft key light from the upper left plus fill, no
cast shadows), sign_rect.json (where the sign and its parchment panel sit in the PNG) and VG_Sign.blend (the GLB
imported, its texture downsized to 2048 and packed) next to this script. Runs again to the same files.

The caption keeps the sign flat: VanGoghCaption draws one of these PNGs as a uGUI Image at SIGN_WIDTH metres and puts
his words inside the panel rectangle from sign_rect.json.
"""
import bpy, json, math, os, sys
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

print("Blender", bpy.app.version_string)  # first log line: which Blender ran this

# ---- Parameters -----------------------------------------------------------------------------------
WIDTH_PX = 2048                # the PNG's width; the sign spans it minus the margin
MARGIN = 0.02                  # transparent margin each side of the sign, as a fraction of its width (holds the bleed)
BLEED_PX = 24                  # edge colours spread this far into the transparent area, so mipmaps don't ring dark
TEX_MAX = 2048                 # the texture kept in the .blend
FRONT = Vector((0, -1, 0))     # the chosen face: the GLB's -Y side (glTF +Z), the better-painted parchment
RECESS = 0.006                 # the parchment sits at least this far behind the planks' front (model units, ~m)
CELL = 0.004                   # ray-cast grid for finding the panel
INK = (0x40, 0x21, 0x0C)       # VanGoghCaption's ink
TEXT_PAD = (0.035, 0.025)      # VanGoghCaption's TextPadX / TextPadY, metres; the sign is 1 m wide = 1 model unit
SAMPLES = 32

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))   # Caption < VanGogh < Source < Art < repo
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GLB = next((a for a in args if a.endswith(".glb")), "/Users/cicisss/CS-VR/cc-reports/2026-10-27-caption3d/source/caption-frame.glb")
OUT = args[args.index("--out") + 1] if "--out" in args else os.path.join(REPO, "Assets", "VanGogh", "Art", "UI")


def log(step, what):
    print("STEP|%s|%s" % (step, what))


def box(points):
    lo = Vector([min(p[i] for p in points) for i in range(3)]); hi = Vector([max(p[i] for p in points) for i in range(3)])
    return lo, hi


# ---- 1. import ---------------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.import_scene.gltf(filepath=GLB)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, "expected one mesh, got %d" % len(meshes)
sign = meshes[0]
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
sign.parent = None; sign.matrix_world = Matrix.Identity(4)
sign.name = sign.data.name = "VG_Sign"
me = sign.data; me.calc_loop_triangles()
lo, hi = box([v.co for v in me.vertices])
size = hi - lo; centre = (lo + hi) / 2
log("1 import", "%s: %d tris, box %s..%s, %.4f x %.4f x %.4f (x, y, z)" % (os.path.basename(GLB), len(me.loop_triangles),
    tuple(round(v, 4) for v in lo), tuple(round(v, 4) for v in hi), *size))
# stop rule: one piece? count connected pieces by shared vertices (glTF splits UV islands, so count by position)
pos = {}
for v in me.vertices: pos.setdefault(tuple(round(c, 4) for c in v.co), []).append(v.index)
parent = list(range(len(me.vertices)))
def find(a):
    while parent[a] != a: parent[a] = parent[parent[a]]; a = parent[a]
    return a
def union(a, b): parent[find(a)] = find(b)
for group in pos.values():
    for i in group[1:]: union(group[0], i)
for e in me.edges: union(e.vertices[0], e.vertices[1])
groups = {}
for i, v in enumerate(me.vertices): groups.setdefault(find(i), []).append(v.co)
pieces = sorted(groups.values(), key=len, reverse=True)
log("1 import", "%d piece(s) when vertices at the same position are joined (Tripo builds planks, blocks, bolts and panels as separate shells)" % len(pieces))
for p in pieces:
    plo, phi = box(p)
    print("PIECE|verts=%d|size %.3f x %.3f x %.3f|centre (%.3f, %.3f, %.3f)" % (len(p), *(phi - plo), *((plo + phi) / 2)))
# stop rule: a piece that is not part of the sign would sit outside the planks' box or float away from it
for p in pieces:
    plo, phi = box(p)
    assert all(plo[i] >= lo[i] - 1e-4 and phi[i] <= hi[i] + 1e-4 for i in range(3)), "a piece lies outside the sign's box"

# ---- 2. the parchment panel: a recessed rectangle, found by ray casts on the front -------------------------
bm_tree = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
nx, nz = int(size.x / CELL) + 1, int(size.z / CELL) + 1
depth = np.full((nx, nz), np.nan)
for i in range(nx):
    for k in range(nz):
        origin = Vector((lo.x + i * CELL, lo.y - 1.0, lo.z + k * CELL))
        hit = bm_tree.ray_cast(origin, Vector((0, 1, 0)), 3.0)
        if hit[0] is not None: depth[i, k] = hit[0].y
front_y = np.nanmin(depth)
deep = depth > front_y + RECESS                      # everything behind the planks' front: their sides and the panel
panel_y = float(np.nanmedian(depth[deep]))            # the panel is the one big flat surface back there
recessed = deep & (np.abs(depth - panel_y) < RECESS)  # cells on that plate
# largest connected recessed region
labels = np.zeros_like(recessed, dtype=int); n = 0; best = None
for i in range(nx):
    for k in range(nz):
        if recessed[i, k] and labels[i, k] == 0:
            n += 1; stack = [(i, k)]; labels[i, k] = n; cells = []
            while stack:
                a, b = stack.pop(); cells.append((a, b))
                for p, q in ((a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1)):
                    if 0 <= p < nx and 0 <= q < nz and recessed[p, q] and labels[p, q] == 0:
                        labels[p, q] = n; stack.append((p, q))
            if best is None or len(cells) > len(best): best = cells
assert best, "no recessed panel found on the front"
ci = np.array(best)
px0, px1 = lo.x + ci[:, 0].min() * CELL, lo.x + ci[:, 0].max() * CELL
pz0, pz1 = lo.z + ci[:, 1].min() * CELL, lo.z + ci[:, 1].max() * CELL
fill = len(best) / ((ci[:, 0].max() - ci[:, 0].min() + 1) * (ci[:, 1].max() - ci[:, 1].min() + 1))
panel_depths = depth[labels == labels[best[0]]]
log("2 panel", "front plane y %.4f; %d recessed region(s), the largest %d cells, box x %.4f..%.4f z %.4f..%.4f (%.3f x %.3f), filling %.0f%% of its box; depth %.4f..%.4f behind the front (median %.4f)" % (
    front_y, n, len(best), px0, px1, pz0, pz1, px1 - px0, pz1 - pz0, fill * 100, np.min(panel_depths) - front_y, np.max(panel_depths) - front_y, np.median(panel_depths) - front_y))
assert fill > 0.8, "the recessed region is not one roughly flat rectangle (fills %.0f%% of its box); stop rule" % (fill * 100)
# the planks' wavy inner edges bite into that box: shrink it until it lies on the panel alone, so no word sits on wood
mask = labels == labels[best[0]]
i0, i1, k0, k1 = ci[:, 0].min(), ci[:, 0].max(), ci[:, 1].min(), ci[:, 1].max()
while mask[i0:i1 + 1, k0:k1 + 1].mean() < 0.999:
    edges = {"i0": mask[i0, k0:k1 + 1].mean(), "i1": mask[i1, k0:k1 + 1].mean(), "k0": mask[i0:i1 + 1, k0].mean(), "k1": mask[i0:i1 + 1, k1].mean()}
    worst = min(edges, key=edges.get)
    if worst == "i0": i0 += 1
    elif worst == "i1": i1 -= 1
    elif worst == "k0": k0 += 1
    else: k1 -= 1
bx0, bx1, bz0, bz1 = px0, px1, pz0, pz1
px0, px1 = lo.x + i0 * CELL, lo.x + i1 * CELL
pz0, pz1 = lo.z + k0 * CELL, lo.z + k1 * CELL
log("2 panel", "inscribed rectangle on the panel alone: x %.4f..%.4f z %.4f..%.4f (%.3f x %.3f)" % (px0, px1, pz0, pz1, px1 - px0, pz1 - pz0))

# ---- 3. camera and image: the sign centred, a 2% margin ----------------------------------------------------
ortho = size.x * (1 + 2 * MARGIN)
sign_px = size.x / ortho * WIDTH_PX
needed = size.z * (1 + 2 * MARGIN) / ortho * WIDTH_PX
height_px = 1 << math.ceil(math.log2(needed))   # a power of two, so Android compresses it (a 2048 x 656 PNG stayed RGBA32); the sign is centred
scene = bpy.context.scene
scene.render.resolution_x, scene.render.resolution_y = WIDTH_PX, height_px
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'; scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = ortho; cam.data.clip_end = 10
cam.location = centre + FRONT * 3; cam.rotation_euler = (math.pi / 2, 0, 0)   # at -Y looking +Y, Z up
units_per_px = ortho / WIDTH_PX
vert = height_px * units_per_px
def frac(x, z):   # normalised PNG coordinates from the bottom left, as uGUI anchors
    return ((x - centre.x) / ortho + 0.5, (z - centre.z) / vert + 0.5)
rect = {"image": [WIDTH_PX, height_px], "frontFace": "the GLB's -Y side (glTF +Z)",
        "sign": dict(zip(("x0", "y0", "x1", "y1"), [*frac(lo.x, lo.z), *frac(hi.x, hi.z)])),
        "panel": dict(zip(("x0", "y0", "x1", "y1"), [*frac(px0, pz0), *frac(px1, pz1)])),
        "panelBox": dict(zip(("x0", "y0", "x1", "y1"), [*frac(bx0, bz0), *frac(bx1, bz1)])),
        "note": "fractions of the PNG from its bottom-left corner; the sign is %.4f model units wide, drawn at SignWidth metres by VanGoghCaption" % size.x}
log("3 camera", "PNG %d x %d, the sign %.0f px wide (%.3f units/px); sign rect %s; panel rect %s" % (
    WIDTH_PX, height_px, sign_px, units_per_px, {k: round(v, 4) for k, v in rect["sign"].items()}, {k: round(v, 4) for k, v in rect["panel"].items()}))

# ---- 4. texture to 2048, material variants --------------------------------------------------------------------
mat = me.materials[0]; mat.name = "M_Sign"
img = next(nd.image for nd in mat.node_tree.nodes if nd.type == 'TEX_IMAGE' and nd.image)
w, h = img.size
if max(w, h) > TEX_MAX:
    f = TEX_MAX / max(w, h); img.scale(round(w * f), round(h * f))
img.name = "VG_Sign_BaseColor"
img.pack()
log("4 texture", "%dx%d -> %dx%d, packed into the .blend" % (w, h, *img.size))
nodes, links = mat.node_tree.nodes, mat.node_tree.links
out = next(nd for nd in nodes if nd.type == 'OUTPUT_MATERIAL')
tex = next(nd for nd in nodes if nd.type == 'TEX_IMAGE' and nd.image == img)
emit = nodes.new('ShaderNodeEmission'); emit.inputs['Strength'].default_value = 1.0
links.new(tex.outputs['Color'], emit.inputs['Color'])
bsdf = next(nd for nd in nodes if nd.type == 'BSDF_PRINCIPLED')
bsdf.inputs['Roughness'].default_value = 0.9
if 'Specular IOR Level' in bsdf.inputs: bsdf.inputs['Specular IOR Level'].default_value = 0.2


def use(shader):
    for l in list(out.inputs['Surface'].links): links.remove(l)
    links.new(shader.outputs[0], out.inputs['Surface'])


for engine in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE'):
    try: scene.render.engine = engine; break
    except TypeError: continue
if hasattr(scene, 'eevee'): scene.eevee.taa_render_samples = SAMPLES
log("4 engine", scene.render.engine)

# lights for B: a large key from the upper left in front, a weaker fill from the right, no cast shadows
world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
bg = world.node_tree.nodes['Background']
def light(name, where, energy, size_):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA')); scene.collection.objects.link(l)
    l.data.energy = energy; l.data.size = size_; l.data.use_shadow = False
    l.location = centre + Vector(where); l.rotation_euler = (centre - l.location).to_track_quat('-Z', 'Y').to_euler()
    return l
key = light("Key", (-1.2, -1.6, 1.0), 400, 2.0)
fill = light("Fill", (1.0, -1.8, -0.2), 120, 3.0)


BRIGHTEN = 1.05                # the lit parchment ends this much brighter than the unlit one (the key light's side)
bg.inputs['Color'].default_value = (1, 1, 1, 1)


def render(path, lit):
    use(bsdf if lit else emit)
    for l in (key, fill): l.hide_render = not lit
    world.use_nodes = lit   # unlit: no world light at all; emission alone
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def load_pixels(path):
    im = bpy.data.images.load(path); im.colorspace_settings.name = 'sRGB'
    px = np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], 4)[::-1]   # top row first
    bpy.data.images.remove(im)
    return px


def save_pixels(path, px):
    hgt, wid = px.shape[:2]
    im = bpy.data.images.new("out", wid, hgt, alpha=True); im.colorspace_settings.name = 'sRGB'
    im.pixels.foreach_set(px[::-1].astype(np.float32).ravel())
    im.filepath_raw = path; im.file_format = 'PNG'; im.alpha_mode = 'STRAIGHT'; im.save(); bpy.data.images.remove(im)


def bleed(px, steps):
    rgb, a = px[..., :3].copy(), px[..., 3]
    filled = a > 0.001
    for _ in range(steps):
        acc = np.zeros_like(rgb); cnt = np.zeros(filled.shape, dtype=np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            m = np.roll(filled, (dy, dx), axis=(0, 1)); c = np.roll(rgb, (dy, dx), axis=(0, 1))
            acc += c * m[..., None]; cnt += m
        new = (~filled) & (cnt > 0)
        rgb[new] = acc[new] / cnt[new][:, None]; filled |= new
    return np.concatenate([rgb, a[..., None]], axis=-1)


def luminance(rgb):   # sRGB floats -> relative luminance
    c = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    return 0.2126 * c[..., 0] + 0.7152 * c[..., 1] + 0.0722 * c[..., 2]


ink_l = float(luminance(np.array(INK, dtype=np.float32) / 255))
results = {}
panel_mean = {}
# the text area: the panel inset by the pads, in pixels (1 model unit = SignWidth metres = 1 m)
x0 = int((rect["panel"]["x0"] + TEXT_PAD[0] / ortho) * WIDTH_PX); x1 = int((rect["panel"]["x1"] - TEXT_PAD[0] / ortho) * WIDTH_PX)
y0 = int((1 - rect["panel"]["y1"] + TEXT_PAD[1] / vert) * height_px); y1 = int((1 - rect["panel"]["y0"] - TEXT_PAD[1] / vert) * height_px)
for tag, lit in (("A", False), ("B", True)):
    path = os.path.join(OUT, "VG_Sign_%s.png" % tag)
    if lit:   # calibrate: a dim first pass, then scale the lights so the lit parchment is as bright as the unlit one
        for l in (key, fill): l.data.energy /= 20
        bg.inputs['Strength'].default_value = 0.05
        render(path, True)
        trial = float(luminance(load_pixels(path)[y0:y1, x0:x1, :3]).mean())
        gain = panel_mean["A"] * BRIGHTEN / trial
        for l in (key, fill): l.data.energy *= gain
        bg.inputs['Strength'].default_value = 0.05 * gain
        log("5 calibrate", "dim pass mean luminance %.3f -> lights x%.2f (key %.0f W, fill %.0f W, world %.2f)" % (trial, gain, key.data.energy, fill.data.energy, 0.05 * gain))
    render(path, lit)
    px = load_pixels(path)
    area = px[y0:y1, x0:x1, :3]
    lum = luminance(area)
    contrast = (np.maximum(lum, ink_l) + 0.05) / (np.minimum(lum, ink_l) + 0.05)
    results[tag] = dict(min=float(contrast.min()), p1=float(np.percentile(contrast, 1)), median=float(np.median(contrast)),
                        darkest_px=[int(v * 255) for v in area.reshape(-1, 3)[lum.argmin()]], mean_rgb=[int(v * 255) for v in area.reshape(-1, 3).mean(axis=0)])
    panel_mean[tag] = float(lum.mean())
    px = bleed(px, BLEED_PX)
    save_pixels(path, px)
    alpha = px[..., 3]
    log("5 render " + tag, "%s: %d x %d, %d bytes; opaque %.1f%% of the canvas; text area px x %d..%d y %d..%d; ink contrast min %.2f:1, 1st percentile %.2f:1, median %.2f:1; darkest pixel %s, mean %s" % (
        path, WIDTH_PX, height_px, os.path.getsize(path), (alpha > 0.5).mean() * 100, x0, x1, y0, y1, results[tag]["min"], results[tag]["p1"], results[tag]["median"], results[tag]["darkest_px"], results[tag]["mean_rgb"]))
log("5 brightness", "text area mean luminance A %.3f, B %.3f (B/A %.2f)" % (panel_mean["A"], panel_mean["B"], panel_mean["B"] / panel_mean["A"]))
rect["contrast"] = {k: {kk: round(vv, 2) for kk, vv in v.items() if kk in ("min", "p1", "median")} for k, v in results.items()}
rect["textPad"] = list(TEXT_PAD)
with open(os.path.join(HERE, "sign_rect.json"), "w") as f: json.dump(rect, f, indent=2)
log("6 rect", os.path.join(HERE, "sign_rect.json"))
use(bsdf)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "VG_Sign.blend"))
log("7 blend", os.path.join(HERE, "VG_Sign.blend"))
print("DONE bake_sign")
