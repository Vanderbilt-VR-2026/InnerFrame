"""Export the girl for Unity: Girl_Armature (deform bones only, Unity Humanoid names) + Girl_Body,
with a one-frame rest-pose take (PlayerAvatarBuilder keeps it as the standing pose; with no clip a
Humanoid sits its hips on the floor), and the base colour at 2048 for Assets/.../Avatars/Textures.

The FBX carries no file paths (the repo is public): the two objects are appended into an empty, unsaved
session (so FBX's "native file" is blank), the Rigify constraints are dropped (the rest pose is the same),
and the exported material has no image (Unity makes its own, PlayerAvatarBuilder).
Blender -b -P export_unity.py -- <Girl_Rigged.blend> <out.fbx> <out_basecolor.jpg>"""
import bpy, sys
src, out_fbx, out_tex = sys.argv[sys.argv.index('--') + 1:][:3]
bpy.ops.wm.read_factory_settings(use_empty=True)
with bpy.data.libraries.load(src, link=False) as (data_from, data_to):
    data_to.objects = ['Girl_Armature', 'Girl_Body']
sc = bpy.context.scene
for o in data_to.objects:
    sc.collection.objects.link(o)
arm, body = bpy.data.objects['Girl_Armature'], bpy.data.objects['Girl_Body']
for o in [o for o in bpy.data.objects if o not in (arm, body)]:
    bpy.data.objects.remove(o)                       # RIG-Girl, pulled in by the constraints
for pb in arm.pose.bones:
    for c in list(pb.constraints): pb.constraints.remove(c)
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.scale = (1, 1, 1)
for o in (arm, body):
    o.animation_data_clear()
# the base colour, then a plain material for the FBX
img = next(i for i in bpy.data.images if i.size[0] > 0)
img.scale(2048, 2048)
img.save(filepath=out_tex, quality=90)
plain = bpy.data.materials.new('M_Avatar_Girl')
body.data.materials.clear(); body.data.materials.append(plain)
for i in list(bpy.data.images): bpy.data.images.remove(i)
sc.frame_start, sc.frame_end = 1, 2
sc.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, use_armature_deform_only=True,
    bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    primary_bone_axis='Y', secondary_bone_axis='X', mesh_smooth_type='FACE', path_mode='STRIP')
print('[export] ok', out_fbx, out_tex)
