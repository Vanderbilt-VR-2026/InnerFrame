"""Export the girl for Unity: Girl_Armature (deform bones only, Unity Humanoid names) + Girl_Body,
with a one-frame rest-pose take (PlayerAvatarBuilder keeps it as the standing pose; with no clip a
Humanoid sits its hips on the floor), and the base colour at 2048 for Assets/.../Avatars/Textures.
Blender -b Girl_Rigged.blend -P export_unity.py -- <out.fbx> <out_basecolor.jpg>"""
import bpy, sys
out_fbx, out_tex = sys.argv[sys.argv.index('--') + 1:][:2]
sc = bpy.context.scene
rig = bpy.data.objects['RIG-Girl']
if rig.animation_data: rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
sc.frame_start, sc.frame_end = 1, 2
sc.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for n in ('Girl_Armature', 'Girl_Body'):
    o = bpy.data.objects[n]; o.hide_set(False); o.select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects['Girl_Armature']
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, use_armature_deform_only=True,
    bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    primary_bone_axis='Y', secondary_bone_axis='X', mesh_smooth_type='FACE', path_mode='STRIP')
img = next(i for i in bpy.data.images if i.size[0] > 0)
img.scale(2048, 2048)
img.filepath_raw = out_tex; img.file_format = 'JPEG'
sc.render.image_settings.quality = 90
img.save(filepath=out_tex, quality=90)
print('[export] ok', out_fbx, out_tex)
