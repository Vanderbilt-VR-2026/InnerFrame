"""Pose RIG-Girl through Rigify controls and render front/side views per pose.
Blender -b <Girl_Rigged.blend> -P pose_test.py -- <out_prefix> [save_blend_with_actions]"""
import bpy, sys, math
from mathutils import Matrix, Vector
args = sys.argv[sys.argv.index('--') + 1:]
OUTP = args[0]
SAVE = args[1] if len(args) > 1 else None
sc = bpy.context.scene
rig = bpy.data.objects['RIG-Girl']
pbs = rig.pose.bones
H = 1.60
print('CTRL', ' '.join(b.name for b in rig.data.bones if not b.name.startswith(('DEF-', 'ORG-', 'MCH-', 'VIS'))))

def reset():
    for pb in pbs:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    for n in ('thigh_parent.L', 'thigh_parent.R'):
        pbs[n]['IK_FK'] = 0.0      # legs IK
    for n in ('upper_arm_parent.L', 'upper_arm_parent.R'):
        pbs[n]['IK_FK'] = 1.0      # arms FK
    bpy.context.view_layer.update()

def move(name, d):
    pb = pbs[name]
    pb.matrix = Matrix.Translation(Vector(d) * H) @ pb.matrix
    bpy.context.view_layer.update()

def rot(name, axis, deg, pivot=None):
    pb = pbs[name]
    h = pb.head.copy() if pivot is None else Vector(pivot)
    R = Matrix.Translation(h) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-h)
    pb.matrix = R @ pb.matrix
    bpy.context.view_layer.update()

def chain_rot(base, axis, degs):
    names = [base] + ([f"{base[:-2]}.{i:03d}{base[-2:]}" for i in (1, 2)] if base.endswith(('.L', '.R')) else [f"{base}.{i:03d}" for i in (1, 2)])
    for n, d in zip(names, degs):
        if n in pbs: rot(n, axis, d)

def pose_stand():
    pass

def pose_walk():
    move('foot_ik.L', (0, -0.075, 0.0))
    move('foot_ik.R', (0, 0.07, 0.025)); rot('foot_ik.R', 'X', 20)
    move('torso', (0, 0.0, -0.012))
    rot('upper_arm_fk.L', 'X', -12, ); rot('upper_arm_fk.R', 'X', 12)
    chain_rot('skirt_F', 'X', [-8, -6, 0])

def pose_crouch():
    move('torso', (0, 0.05, -0.19))
    rot('torso', 'X', 25)
    chain_rot('skirt_F', 'X', [-45, -15, 10])
    chain_rot('skirt.L', 'Y', [-15, -5, 5]); chain_rot('skirt.R', 'Y', [15, 5, -5])
    chain_rot('skirt_B', 'X', [10, 10, 5])

def pose_kneel():
    move('torso', (0, 0.0, -0.24))
    move('foot_ik.L', (0, -0.19, 0.0))                      # left foot planted forward
    move('foot_ik.R', (0, 0.23, -0.01)); rot('foot_ik.R', 'X', 70)  # right shin along the floor
    chain_rot('skirt_F', 'X', [-58, 38, 20])
    chain_rot('skirt_B', 'X', [10, 15, 10])
    chain_rot('skirt.L', 'X', [-38, 18, 10]); chain_rot('skirt.R', 'X', [-18, 10, 8])

def pose_arms():
    rot('upper_arm_fk.L', 'X', -85); rot('upper_arm_fk.R', 'X', -85)
    rot('upper_arm_fk.L', 'Y', 25); rot('upper_arm_fk.R', 'Y', -25)
    rot('forearm_fk.L', 'X', -30)

POSES = [('stand', pose_stand), ('arms', pose_arms), ('walk', pose_walk), ('crouch', pose_crouch), ('kneel', pose_kneel)]

sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 520; sc.render.resolution_y = 900
cam = bpy.data.cameras.new('c'); cam.type = 'ORTHO'; cam.ortho_scale = 1.15 * H
co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
views = {'front': ((0, -5, 0.5 * H), (math.radians(90), 0, 0)),
         'side': ((5, 0, 0.5 * H), (math.radians(90), 0, math.radians(90))),
         'q': ((3.5, -3.5, 0.5 * H), (math.radians(90), 0, math.radians(45)))}
if SAVE:
    rig.animation_data_create()
    for a in [a for a in bpy.data.actions if a.name.startswith('Test_')]:
        bpy.data.actions.remove(a)
for pn, fn in POSES:
    if rig.animation_data: rig.animation_data.action = None
    reset(); fn()
    for vn, (l, r) in views.items():
        co.location = l; co.rotation_euler = r
        sc.render.filepath = f'{OUTP}_{pn}_{vn}.png'
        bpy.ops.render.render(write_still=True)
    if SAVE:
        act = bpy.data.actions.new(f'Test_{pn}')
        rig.animation_data.action = act
        for pb in pbs:
            if not pb.name.startswith(('DEF-', 'ORG-', 'MCH-')):
                for path in ('location', 'rotation_quaternion', 'scale'):
                    try: pb.keyframe_insert(path, frame=1)
                    except Exception: pass
        act.use_fake_user = True
if SAVE:
    reset()
    rig.animation_data.action = None
    bpy.data.objects.remove(co)
    bpy.ops.wm.save_as_mainfile(filepath=SAVE)
