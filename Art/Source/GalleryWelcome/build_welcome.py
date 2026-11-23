"""Reuse Cici's caption sign, with a taller inset and a painted walnut stand.

Run Blender -b Art/Source/VanGogh/Caption/VG_Sign.blend -P this_file.
Dimensions are the approved Gallery welcome concept, in metres. The original
caption source is read only. No generated reference image is used as a mesh.
"""
import bpy
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/GalleryWelcome/Art'
OUT.mkdir(parents=True, exist_ok=True)
WIDTH, HEIGHT, CENTRE = 1.20, .80, 1.20
sign = bpy.data.objects['VG_Sign']
for obj in list(bpy.data.objects):
    if obj != sign:
        bpy.data.objects.remove(obj, do_unlink=True)
sign.name = 'Welcome_Frame'
verts = sign.data.vertices
lo = Vector(tuple(min(v.co[i] for v in verts) for i in range(3)))
hi = Vector(tuple(max(v.co[i] for v in verts) for i in range(3)))
middle = (lo + hi) * .5
scale = WIDTH / (hi.x - lo.x)
# Keep the carved corner blocks square; extend the middle of the vertical rails.
old_half = (hi.z - lo.z) * scale * .5
end = .085
for v in verts:
    q = (v.co - middle) * scale
    z = abs(q.z)
    q.z = (1 if q.z >= 0 else -1) * ((HEIGHT * .5 - old_half + z) if z > old_half - end
        else z * (HEIGHT * .5 - end) / (old_half - end))
    q.z += CENTRE
    v.co = q
sign.data.update()
bpy.context.view_layer.objects.active = sign
sign.select_set(True)
dec = sign.modifiers.new('Welcome budget', 'DECIMATE')
dec.ratio = .25
bpy.ops.object.modifier_apply(modifier=dec.name)
image = bpy.data.images['VG_Sign_BaseColor']
image.filepath_raw = str(OUT / 'WelcomeFrame_BaseColor.png')
image.file_format = 'PNG'
image.save()
sign.data.materials[0].name = 'WelcomeFrame'

wood = bpy.data.materials.new('WelcomeWalnut')
wood.use_nodes = True
nodes = wood.node_tree.nodes
tex = nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(str(ROOT / 'Assets/Art/Gallery/Textures/GT_BenchWood_BaseColor.jpg'))
wood.node_tree.links.new(tex.outputs['Color'], nodes.get('Principled BSDF').inputs['Base Color'])
nodes.get('Principled BSDF').inputs['Roughness'].default_value = .85

def block(name, at, size):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new('Soft painted edges', 'BEVEL')
    mod.width, mod.segments = .012, 2
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(wood)
    return obj

block('Welcome_Post', (0, .10, .45), (.15, .14, .82))
block('Welcome_Base', (0, .045, .055), (.62, .40, .11))
block('Welcome_BaseStep', (0, .045, .12), (.50, .30, .035))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'Art/Source/GalleryWelcome/GalleryWelcome.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT / 'GalleryWelcome.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=True, add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for obj in meshes:
    obj.data.calc_loop_triangles()
print('WELCOME_MODEL', [(o.name, len(o.data.loop_triangles)) for o in meshes])
