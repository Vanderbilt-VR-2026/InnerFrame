import bpy, sys
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[-1])
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
me = [o for o in bpy.data.objects if o.type == 'MESH'][0]
print('[fbx] armature', arm.name, 'bones', len(arm.data.bones))
def walk(b, d=0):
    print('[fbx] ' + '  ' * d + b.name)
    for c in b.children: walk(c, d + 1)
for b in arm.data.bones:
    if not b.parent: walk(b)
import collections
cnt = collections.Counter(len([g for g in v.groups if g.weight > 0]) for v in me.data.vertices)
print('[fbx] mesh', me.name, 'verts', len(me.data.vertices), 'tris', sum(len(p.vertices)-2 for p in me.data.polygons), 'influences/vert', dict(cnt))
d = me.dimensions; print('[fbx] height', round(max(d), 3), 'images', [i.name for i in bpy.data.images])
