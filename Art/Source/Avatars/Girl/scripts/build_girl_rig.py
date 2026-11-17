"""Rig the Tripo girl (Victorian dress) with Rigify + a clean Unity Humanoid skeleton.

Run headless:
  Blender -b --factory-startup -P build_girl_rig.py -- <in.glb> <out_dir>

Produces in <out_dir>:
  Girl_Rigged.blend   metarig (edit + regenerate), RIG-Girl (Rigify controls),
                      Girl_Armature (deform skeleton, Unity names) driven by the
                      Rigify DEF bones, Girl_Body skinned to Girl_Armature
  Girl_Rigged.fbx     Girl_Armature + Girl_Body only (no control bones), for Unity Humanoid

Landmarks are in "unit" space: the Tripo export is 1.0 m tall, feet at z=0, facing -Y.
Everything is scaled by HEIGHT after import.
"""
import bpy, bmesh, sys, math, os
from mathutils import Vector
from mathutils.geometry import intersect_point_line
import addon_utils

args = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
HEIGHT = 1.60  # metres, feet to top of bonnet

def U(x, y, z):
    return Vector((x, y, z)) * HEIGHT

# ---------------------------------------------------------------- landmarks (left side, +X)
L = {
    'hips':      (0, 0.010, 0.570),
    'spine1':    (0, 0.000, 0.640),
    'spine2':    (0, -0.005, 0.700),
    'spine3':    (0, -0.005, 0.760),
    'neck':      (0, 0.005, 0.830),
    'neck2':     (0, 0.002, 0.855),
    'head':      (0, 0.005, 0.878),
    'head_top':  (0, 0.005, 0.985),
    'clav':      (0.012, -0.008, 0.815),
    'shoulder':  (0.068, 0.010, 0.805),
    'elbow':     (0.113, 0.016, 0.660),
    'wrist':     (0.153, -0.012, 0.540),
    'hand_tip':  (0.169, -0.027, 0.462),
    'hip_j':     (0.045, 0.010, 0.550),
    'knee':      (0.034, -0.010, 0.300),
    'ankle':     (0.026, 0.010, 0.065),
    'ball':      (0.030, -0.045, 0.012),
    'toe':       (0.032, -0.075, 0.012),
    'heel_in':   (0.010, 0.025, 0.0),
    'heel_out':  (0.045, 0.025, 0.0),
}
def P(name, side=1):
    x, y, z = L[name]
    return U(x * side, y, z)

# skirt chains: 4 directions x 3 bones, from waist to hem, ~85% of the surface radius
SKIRT = {
    'skirt_F':    [(0, -0.050, 0.640), (0, -0.080, 0.470), (0, -0.110, 0.300), (0, -0.130, 0.140)],
    'skirt_B':    [(0, 0.060, 0.640), (0, 0.105, 0.470), (0, 0.125, 0.300), (0, 0.140, 0.140)],
    'skirt.L':    [(0.060, 0, 0.640), (0.100, 0, 0.470), (0.130, 0, 0.300), (0.160, 0, 0.140)],
    'skirt.R':    [(-0.060, 0, 0.640), (-0.100, 0, 0.470), (-0.130, 0, 0.300), (-0.160, 0, 0.140)],
}
SKIRT_DIR = {'skirt_F': 0.0, 'skirt.L': 90.0, 'skirt_B': 180.0, 'skirt.R': 270.0}  # angle from front, toward +X
SKIRT_UNITY = {'skirt_F': 'Skirt_Front', 'skirt_B': 'Skirt_Back', 'skirt.L': 'Skirt_Left', 'skirt.R': 'Skirt_Right'}

def chain_names(base):
    # Blender/Rigify style: skirt_F, skirt_F.001 / skirt.L, skirt.001.L
    if base.endswith(('.L', '.R')):
        stem, sfx = base[:-2], base[-2:]
        return [base] + [f"{stem}.{i:03d}{sfx}" for i in (1, 2)]
    return [base] + [f"{base}.{i:03d}" for i in (1, 2)]

# ---------------------------------------------------------------- scene + mesh
bpy.ops.wm.read_factory_settings(use_empty=True)
addon_utils.enable('rigify', default_set=True)
bpy.ops.import_scene.gltf(filepath=SRC)
body = [o for o in bpy.data.objects if o.type == 'MESH'][0]
for o in list(bpy.data.objects):
    if o is not body:
        if o.type == 'EMPTY':
            body.parent = None
            bpy.data.objects.remove(o)
body.matrix_world = body.matrix_world.copy()
body.name = body.data.name = 'Girl_Body'
bpy.context.view_layer.objects.active = body
body.select_set(True)
body.scale = (HEIGHT, HEIGHT, HEIGHT)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bm = bmesh.new(); bm.from_mesh(body.data)
n0 = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002 * HEIGHT)
bm.to_mesh(body.data); bm.free()
for poly in body.data.polygons: poly.use_smooth = True
print(f"[mesh] merged {n0} -> {len(body.data.vertices)} verts")

# ---------------------------------------------------------------- metarig
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.object.armature_basic_human_metarig_add()
meta = bpy.context.object
meta.name = 'metarig'
bpy.ops.object.mode_set(mode='EDIT')
eb = meta.data.edit_bones
for n in ('breast.L', 'breast.R', 'pelvis.L', 'pelvis.R'):
    eb.remove(eb[n])

def place(name, h, t):
    b = eb[name]; b.head = h; b.tail = t

place('spine', P('hips'), P('spine1'))
place('spine.001', P('spine1'), P('spine2'))
place('spine.002', P('spine2'), P('spine3'))
place('spine.003', P('spine3'), P('neck'))
place('spine.004', P('neck'), P('neck2'))
place('spine.005', P('neck2'), P('head'))
place('spine.006', P('head'), P('head_top'))
for side, s in (('L', 1), ('R', -1)):
    place(f'shoulder.{side}', P('clav', s), P('shoulder', s) + Vector((0, 0, 0.004 * HEIGHT)))
    place(f'upper_arm.{side}', P('shoulder', s), P('elbow', s))
    place(f'forearm.{side}', P('elbow', s), P('wrist', s))
    place(f'hand.{side}', P('wrist', s), P('hand_tip', s))
    place(f'thigh.{side}', P('hip_j', s), P('knee', s))
    place(f'shin.{side}', P('knee', s), P('ankle', s))
    place(f'foot.{side}', P('ankle', s), P('ball', s))
    place(f'toe.{side}', P('ball', s), P('toe', s))
    place(f'heel.02.{side}', P('heel_in', s), P('heel_out', s))
# rolls: arms bend back at the elbow, legs forward at the knee -> let Blender align X to the bend
for side in 'LR':
    for n in (f'upper_arm.{side}', f'forearm.{side}', f'hand.{side}'):
        eb[n].select = True
bpy.ops.armature.select_all(action='DESELECT')
for side in 'LR':
    for n in (f'upper_arm.{side}', f'forearm.{side}', f'hand.{side}'):
        eb[n].select = True
        eb[n].select_head = eb[n].select_tail = True
bpy.ops.armature.calculate_roll(type='GLOBAL_POS_Y')
bpy.ops.armature.select_all(action='DESELECT')
for side in 'LR':
    for n in (f'thigh.{side}', f'shin.{side}', f'foot.{side}', f'toe.{side}'):
        eb[n].select = True
        eb[n].select_head = eb[n].select_tail = True
bpy.ops.armature.calculate_roll(type='GLOBAL_POS_X' if False else 'GLOBAL_NEG_Y')
bpy.ops.armature.select_all(action='DESELECT')

# skirt chains, parented to the hips (spine)
for base, pts in SKIRT.items():
    parent = eb['spine']
    for i, n in enumerate(chain_names(base)):
        b = eb.new(n)
        b.head = U(*pts[i]); b.tail = U(*pts[i + 1])
        b.parent = parent
        b.use_connect = i > 0
        b.roll = 0.0
        parent = b
bpy.ops.object.mode_set(mode='POSE')
for base in SKIRT:
    pb = meta.pose.bones[chain_names(base)[0]]
    pb.rigify_type = 'limbs.simple_tentacle'
    try:
        pb.rigify_parameters.copy_rotation_axes = [False, False, False]
    except Exception:
        pass
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- generate Rigify rig
bpy.ops.object.select_all(action='DESELECT')
meta.select_set(True); bpy.context.view_layer.objects.active = meta
bpy.ops.pose.rigify_generate()
rig = [o for o in bpy.data.objects if o.type == 'ARMATURE' and o is not meta][0]
rig.name = 'RIG-Girl'
defs = sorted(b.name for b in rig.data.bones if b.name.startswith('DEF-'))
print('[rigify] generated', len(rig.data.bones), 'bones,', len(defs), 'DEF:', ' '.join(defs))

# ---------------------------------------------------------------- Unity deform skeleton
# (unity name, parent, metarig bone giving head/tail/roll, rigify DEF bone it copies)
MAP = [
    ('Hips', None, 'spine', 'DEF-spine'),
    ('Spine', 'Hips', 'spine.001', 'DEF-spine.001'),
    ('Chest', 'Spine', 'spine.002', 'DEF-spine.002'),
    ('UpperChest', 'Chest', 'spine.003', 'DEF-spine.003'),
    ('Neck', 'UpperChest', ('spine.004', 'spine.005'), 'DEF-spine.004'),
    ('Head', 'Neck', 'spine.006', 'DEF-spine.006'),
]
for side, Side in (('L', 'Left'), ('R', 'Right')):
    MAP += [
        (f'{Side}Shoulder', 'UpperChest', f'shoulder.{side}', f'DEF-shoulder.{side}'),
        (f'{Side}UpperArm', f'{Side}Shoulder', f'upper_arm.{side}', f'DEF-upper_arm.{side}'),
        (f'{Side}LowerArm', f'{Side}UpperArm', f'forearm.{side}', f'DEF-forearm.{side}'),
        (f'{Side}Hand', f'{Side}LowerArm', f'hand.{side}', f'DEF-hand.{side}'),
        (f'{Side}UpperLeg', 'Hips', f'thigh.{side}', f'DEF-thigh.{side}'),
        (f'{Side}LowerLeg', f'{Side}UpperLeg', f'shin.{side}', f'DEF-shin.{side}'),
        (f'{Side}Foot', f'{Side}LowerLeg', f'foot.{side}', f'DEF-foot.{side}'),
        (f'{Side}Toes', f'{Side}Foot', f'toe.{side}', f'DEF-toe.{side}'),
    ]
SKIRT_BONES = []  # (unity name, base, index)
for base in SKIRT:
    parent = 'Hips'
    for i, n in enumerate(chain_names(base)):
        un = f'{SKIRT_UNITY[base]}_{i + 1}'
        MAP.append((un, parent, n, 'DEF-' + n))
        SKIRT_BONES.append((un, base, i))
        parent = un

# geometry comes from the generated DEF bones so the rest matrices match exactly
# (Copy Transforms needs identical rest orientation, roll included); twist pairs
# (DEF-x, DEF-x.001) are collinear, so the export bone spans both
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
reb = rig.data.edit_bones
geo = {}
for un, par, src, d in MAP:
    b = reb[d]
    tail = b.tail.copy()
    if d + '.001' in reb and not d.startswith(('DEF-spine', 'DEF-skirt')):
        tail = reb[d + '.001'].tail.copy()
    geo[un] = (b.head.copy(), tail, b.roll)
bpy.ops.object.mode_set(mode='OBJECT')
rig.select_set(False)

arm_data = bpy.data.armatures.new('Girl_Armature')
arm = bpy.data.objects.new('Girl_Armature', arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for un, par, _, _ in MAP:
    h, t, r = geo[un]
    b = arm_data.edit_bones.new(un)
    b.head, b.tail, b.roll = h, t, r
for un, par, _, _ in MAP:
    if par:
        b = arm_data.edit_bones[un]
        b.parent = arm_data.edit_bones[par]
        b.use_connect = (b.head - b.parent.tail).length < 1e-4
bpy.ops.object.mode_set(mode='POSE')
for un, _, _, d in MAP:
    c = arm.pose.bones[un].constraints.new('COPY_TRANSFORMS')
    c.name = 'Follow Rigify'
    c.target = rig; c.subtarget = d
bpy.ops.object.mode_set(mode='OBJECT')
arm.show_in_front = True
arm_data.display_type = 'STICK'

# ---------------------------------------------------------------- bind with automatic weights
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True); arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
print('[weights] automatic weights done')

# ---------------------------------------------------------------- weight fixes
me = body.data
vg = {g.name: g for g in body.vertex_groups}
gname = {g.index: g.name for g in body.vertex_groups}
W = [dict() for _ in me.vertices]
for v in me.vertices:
    for g in v.groups:
        if g.weight > 1e-4:
            W[v.index][gname[g.group]] = g.weight

# islands
adj = [[] for _ in me.vertices]
for e in me.edges:
    a, b = e.vertices; adj[a].append(b); adj[b].append(a)
island = [-1] * len(me.vertices); islands = []
for s in range(len(me.vertices)):
    if island[s] >= 0: continue
    st = [s]; island[s] = len(islands); comp = []
    while st:
        x = st.pop(); comp.append(x)
        for y in adj[x]:
            if island[y] < 0: island[y] = island[s]; st.append(y)
    islands.append(comp)
order = sorted(range(len(islands)), key=lambda i: -len(islands[i]))
co = [v.co.copy() for v in me.vertices]
def bbox(c):
    return [min(co[i][k] for i in c) for k in range(3)], [max(co[i][k] for i in c) for k in range(3)]
main = order[0]
print('[weights] islands:', len(islands), 'main', len(islands[main]))

LEG = {'LeftUpperLeg', 'LeftLowerLeg', 'LeftFoot', 'LeftToes', 'RightUpperLeg', 'RightLowerLeg', 'RightFoot', 'RightToes'}
def seg_dist(p, a, b):
    t = intersect_point_line(p, a, b)[1]
    t = max(0.0, min(1.0, t))
    return (p - a.lerp(b, t)).length
def normalize(d):
    s = sum(d.values())
    return {k: v / s for k, v in d.items() if v / s > 1e-3} if s > 1e-8 else d

# 1) leg islands (shins+thighs hidden under the skirt): legs only, no skirt/hand bleed
for i in order:
    mn, mx = bbox(islands[i])
    if mn[2] < 0.02 * HEIGHT and mx[2] > 0.3 * HEIGHT and len(islands[i]) > 100:
        side = 'Left' if (mn[0] + mx[0]) > 0 else 'Right'
        keep = {f'{side}UpperLeg', f'{side}LowerLeg', f'{side}Foot', f'{side}Toes', 'Hips'}
        for v in islands[i]:
            d = {k: w for k, w in W[v].items() if k in keep}
            if not d:
                p = co[v]
                d = {f'{side}LowerLeg': 1.0} if p.z < P('knee').z else {f'{side}UpperLeg': 1.0}
            W[v] = normalize(d)
        print(f'[weights] leg island {side}: {len(islands[i])} verts')
# 2) forearm+hand islands: arm bones of that side only
for i in order:
    mn, mx = bbox(islands[i])
    if abs((mn[0] + mx[0]) / 2) > 0.12 * HEIGHT and mn[2] > 0.4 * HEIGHT and mx[2] < 0.7 * HEIGHT and len(islands[i]) > 30:
        side = 'Left' if mn[0] > 0 else 'Right'
        keep = {f'{side}UpperArm', f'{side}LowerArm', f'{side}Hand'}
        for v in islands[i]:
            d = {k: w for k, w in W[v].items() if k in keep}
            if not d:
                p = co[v]
                d = {f'{side}Hand': 1.0} if p.z < P('wrist').z else {f'{side}LowerArm': 1.0}
            W[v] = normalize(d)
        print(f'[weights] arm island {side}: {len(islands[i])} verts')

# 3) skirt region of the main dress island: procedural weights on the skirt chains
WAIST_Z = 0.660 * HEIGHT     # above: automatic weights
BLEND_Z = 0.600 * HEIGHT     # between BLEND_Z and WAIST_Z: blend auto -> skirt
HEM_Z = 0.135 * HEIGHT
def smooth(x):
    x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def skirt_weights(p):
    t = max(0.0, min(1.0, (WAIST_Z - p.z) / (WAIST_Z - HEM_Z)))   # 0 at waist, 1 at hem
    ang = math.degrees(math.atan2(p.x, -p.y)) % 360.0
    dirs = {}
    for base, a in SKIRT_DIR.items():
        da = abs((ang - a + 180) % 360 - 180)
        w = max(0.0, math.cos(math.radians(da)))
        if w > 0: dirs[base] = w
    s = sum(dirs.values()); dirs = {k: v / s for k, v in dirs.items()}
    # along the chain: tent functions centred on each bone's middle (t = 1/6, 1/2, 5/6)
    seg = {}
    for i, c in enumerate((1 / 6, 1 / 2, 5 / 6)):
        w = max(0.0, 1.0 - abs(t - c) * 3.0)
        if t < 1 / 6 and i == 0: w = 1.0
        if t > 5 / 6 and i == 2: w = 1.0
        if w > 0: seg[i] = w
    s = sum(seg.values()); seg = {k: v / s for k, v in seg.items()}
    d = {}
    for base, wd in dirs.items():
        for i, ws in seg.items():
            d[f'{SKIRT_UNITY[base]}_{i + 1}'] = wd * ws
    # hips hold the waistband; thighs pull the front/sides a little so walking/kneeling doesn't punch through
    hips = 1.0 - smooth(t / 0.30)
    side_amt = smooth(abs(p.x) / (0.07 * HEIGHT))
    front = smooth((-p.y) / (0.06 * HEIGHT) + 0.5)
    thigh = 0.30 * math.sin(math.pi * min(1.0, t / 0.8)) * max(side_amt, front * 0.6)
    out = {k: v * (1.0 - hips - thigh) for k, v in d.items()}
    out['Hips'] = out.get('Hips', 0) + hips
    if thigh > 0:
        if abs(p.x) < 0.02 * HEIGHT:
            out['LeftUpperLeg'] = thigh / 2; out['RightUpperLeg'] = thigh / 2
        else:
            wl = smooth(0.5 + p.x / (0.06 * HEIGHT))
            out['LeftUpperLeg'] = thigh * wl; out['RightUpperLeg'] = thigh * (1 - wl)
    return normalize(out)

arm_bones = {f'{s}{b}' for s in ('Left', 'Right') for b in ('UpperArm', 'LowerArm', 'Hand')}
n_sk = 0
for v in islands[main]:
    p = co[v]
    if p.z >= WAIST_Z: continue
    # the sleeve cuffs hang just below the waist line (z 0.60-0.66): they stay on the arm, or the cuff tears off
    # and hangs from the skirt like a curtain when the arm lifts
    if p.z > 0.595 * HEIGHT and sum(W[v].get(b, 0) for b in arm_bones) > 0.3 and \
            min(seg_dist(p, *geo[f'{s}{b}'][:2]) for s in ('Left', 'Right') for b in ('UpperArm', 'LowerArm')) < 0.045 * HEIGHT:
        continue
    sk = skirt_weights(p)
    a = smooth((WAIST_Z - p.z) / (WAIST_Z - BLEND_Z))
    if a < 1.0:
        auto = {k: w for k, w in W[v].items() if k not in arm_bones and not k.startswith('Skirt')}
        auto = normalize(auto) if auto else {'Hips': 1.0}
        mix = {k: auto.get(k, 0) * (1 - a) + sk.get(k, 0) * a for k in set(auto) | set(sk)}
        W[v] = normalize(mix)
    else:
        W[v] = sk
    n_sk += 1
print(f'[weights] skirt override on {n_sk} verts')

# 4) above the waist: no skirt bones, no leg bones on the bodice
for v in islands[main]:
    if co[v].z >= WAIST_Z:
        d = {k: w for k, w in W[v].items() if not k.startswith('Skirt') and k not in LEG}
        if d: W[v] = normalize(d)

# 4b) bone heat leaks the arms into the bodice and overskirt beside them (a sheet pulled up when the arm lifts):
#     on the dress, arm weight fades out from 0.035 to 0.055 (x HEIGHT) away from the arm bones
ARM_SEGS = {}
for un, *_ in MAP:
    if un.endswith(('UpperArm', 'LowerArm', 'Hand')):
        ARM_SEGS[un] = geo[un][:2]
TORSO_BY_Z = [(P('spine1').z, 'Hips'), (P('spine2').z, 'Spine'), (P('spine3').z, 'Chest'), (1e9, 'UpperChest')]
n_arm = 0
for v in islands[main]:
    p = co[v]; d = dict(W[v]); changed = False
    for b, (h, t) in ARM_SEGS.items():
        if b in d:
            k = 1.0 - smooth((seg_dist(p, h, t) - 0.035 * HEIGHT) / (0.020 * HEIGHT))
            if k < 1.0:
                d[b] *= k; changed = True
    if changed:
        d = {k: w for k, w in d.items() if w > 1e-4}
        if sum(d.values()) < 0.05:
            d[next(n for z, n in TORSO_BY_Z if p.z < z)] = 1.0
        W[v] = normalize(d); n_arm += 1
print(f'[weights] arm falloff on {n_arm} dress verts')

# 5) small loose pieces (curls, bows, frills, buttons, thumbs): rigid, copy the nearest main/limb vertex
big = set()
for i in order:
    if len(islands[i]) >= 150: big.update(islands[i])
from mathutils.kdtree import KDTree
kd = KDTree(len(big))
for v in big: kd.insert(co[v], v)
kd.balance()
for i in order:
    if len(islands[i]) >= 150: continue
    c = sum((co[v] for v in islands[i]), Vector()) / len(islands[i])
    # nearest big-island vertex to the piece's own closest point
    best = min(((kd.find(co[v])) for v in islands[i]), key=lambda r: r[2])
    src = W[best[1]]
    for v in islands[i]:
        W[v] = dict(src)

# 6) cap at 4 influences (Unity), renormalise, write back
for g in body.vertex_groups:
    pass
for un, *_ in MAP:
    if un not in vg:
        vg[un] = body.vertex_groups.new(name=un)
for g in list(body.vertex_groups):
    g.remove(list(range(len(me.vertices))))
unweighted = 0
for v in range(len(me.vertices)):
    d = sorted(W[v].items(), key=lambda kv: -kv[1])[:4]
    d = normalize(dict(d))
    if not d: unweighted += 1; continue
    for k, w in d.items():
        vg[k].add([v], w, 'REPLACE')
print(f'[weights] unweighted verts: {unweighted}')

# 7) Tripo fused the sleeves to the dress where they touch (the inside of the upper arm to the bodice, the cuffs to
#    the overskirt): neighbours 5 mm apart, one on the arm and one on the hips, so a lifted arm drags a curtain of
#    dress up with it. Cut the mesh along that line below the shoulder; each side then keeps its own weights.
ARMISH = {f'{s}{b}' for s in ('Left', 'Right') for b in ('UpperArm', 'LowerArm', 'Hand')}
CUT_BELOW = 0.775 * HEIGHT
bm = bmesh.new(); bm.from_mesh(me)
dl = bm.verts.layers.deform.verify()
gidx = {g.name: g.index for g in body.vertex_groups}
arm_ids = {gidx[n] for n in ARMISH if n in gidx}
def armness(v): return sum(w for g, w in v[dl].items() if g in arm_ids)
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
main_set = set(islands[main])
face_arm = {f.index: (sum(armness(v) for v in f.verts) / len(f.verts)) > 0.5 for f in bm.faces}
cut = [e for e in bm.edges if len(e.link_faces) == 2 and all(v.index in main_set for v in e.verts)
       and face_arm[e.link_faces[0].index] != face_arm[e.link_faces[1].index]
       and (e.verts[0].co.z + e.verts[1].co.z) / 2 < CUT_BELOW]
side = {f: face_arm[f.index] for f in bm.faces}
bmesh.ops.split_edges(bm, edges=cut)
# a vertex on the seam now belongs to one side only: give it that side's weights (copied from its nearest
# neighbour that is clearly on that side) if its own say the other side
fixed = 0
for _ in range(4):
    for v in bm.verts:
        if not v.link_faces: continue
        on_arm = sum(1 for f in v.link_faces if side.get(f, False)) > len(v.link_faces) / 2
        a = armness(v)
        if (on_arm and a < 0.5) or (not on_arm and a > 0.5):
            nbrs = [e.other_vert(v) for e in v.link_edges]
            good = [n for n in nbrs if (armness(n) > 0.5) == on_arm]
            if good:
                src = min(good, key=lambda n: (n.co - v.co).length)
                v[dl].clear()
                for g, w in src[dl].items(): v[dl][g] = w
                fixed += 1
bm.to_mesh(me); bm.free()
print(f'[weights] cut {len(cut)} sleeve-to-dress edges below the shoulder, re-weighted {fixed} seam verts')

# ---------------------------------------------------------------- tidy, save, export
for o in (meta, rig):
    o.hide_render = True
meta.hide_set(True)
body.data.name = 'Girl_Body'
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'Girl_Rigged.blend'))
print('[out] saved blend')
# rest-pose check: with the rig at rest the skinned mesh must not move
dg = bpy.context.evaluated_depsgraph_get()
ev = body.evaluated_get(dg).data
mx = max((a.co - b.co).length for a, b in zip(ev.vertices, body.data.vertices))
print(f'[check] max rest-pose displacement {mx*1000:.3f} mm')
for un, *_ in MAP:
    pb = arm.pose.bones[un]
    dd = (pb.matrix - arm.data.bones[un].matrix_local)
    e = max(abs(dd[i][j]) for i in range(4) for j in range(4))
    if e > 1e-4: print('[check] bone off at rest', un, round(e, 5))
