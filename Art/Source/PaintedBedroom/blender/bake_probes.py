"""Light probes for the objects you can pick up: Cycles panoramas on a grid, for Unity's light probes.

    blender -b painted_bedroom.blend -P bake_probes.py -- --out FILE.json [options]

    --out FILE        probes.json: positions (Unity metres) + a small equirectangular radiance map per probe
    --step M          grid spacing (default 0.5)
    --heights a,b,c   Unity y of the probe layers (default 0.15,0.6,1.1,1.6,2.1)
    --size W          panorama width in pixels, height is W/2 (default 32)
    --samples N       Cycles samples per panorama (default 64)
    --table FILE      PaintedBedroomPhysicsTable.asset (default: the checkout's); liftable objects are hidden
    --preview FILE    also render one panorama at eye height in the room centre at 512 px, to eyeball orientation

The fixed objects get their light from lightmaps (bake_lightmaps.py); the liftable ones (chairs, pillows,
pitcher...) move, so Unity lights them from light probes. Unity's own probe bake uses Unity's lights, which
never matched the Cycles rig. So the probes are sampled here, in the same Cycles scene as the lightmaps, as
tiny panoramas; PaintedBedroomProbeImport (Unity, Editor) turns each panorama into Unity's spherical
harmonics with AddDirectionalLight, one small directional light per pixel, so no SH convention is guessed.

The sun's direct light is NOT in the probes (a sun lamp is invisible to the camera): Unity's realtime sun,
set up by the Builder with the same direction, colour and strength (E / pi), lights the picked-up objects and
casts their shadows; the sky light and all bounce light are in the probes.
Probes inside furniture or walls are dropped (a short ray in any axis direction that hits a back face).
Coordinates: Unity (x, y, z) = Blender (x, z, y); the panorama's pixel directions are given in Blender
space by `direction()` below, mirrored from Cycles' equirectangular mapping, and verified with --preview
(the window must sit in the middle of the image, the floor at the bottom).
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bake_lightmaps as B   # noqa: E402  (scene setup shared with the lightmap bake)


def log(*a):
    print("[bake_probes]", *a, flush=True)


def direction(u, v):
    """Blender-space unit direction of equirectangular pixel centre (u, v in 0..1, v down). Mirrors Cycles'
    equirectangular_range_to_direction with the default range: phi = pi - 2 pi u, theta = pi/2 - pi v... in
    camera space (x right, y up, -z forward); the camera is rotated so its up is world +Z and forward +Y."""
    phi = math.pi * (1 - 2 * u)                    # longitude, 0 at the image centre, + to the left
    theta = math.pi * (0.5 - v)                    # latitude, + up
    cam = Vector((math.cos(theta) * math.sin(-phi), math.sin(theta), -math.cos(theta) * math.cos(phi)))
    return cam    # converted with the camera's matrix in main()


def inside_geometry(scene, deps, p, reach=0.35):
    for d in (Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0)), Vector((0, 0, 1)), Vector((0, 0, -1))):
        hit, loc, normal, _, ob, _ = scene.ray_cast(deps, p, d, distance=reach)
        if hit and normal.dot(d) > 0:              # we see the back of a face: we're inside something
            return True
    return False


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv and argv.index(k) + 1 < len(argv) else d
    out = opt("--out")
    if not out:
        raise SystemExit(__doc__)
    step = float(opt("--step", 0.5)); heights = [float(h) for h in opt("--heights", "0.15,0.6,1.1,1.6,2.1").split(",")]
    W = int(opt("--size", 32)); H = W // 2; samples = int(opt("--samples", 64)); preview = opt("--preview")
    repo = B.repo_of(bpy.data.filepath)
    table = opt("--table", os.path.join(repo, "Assets", "InnerFrameBedroomWeb", "Resources", "PaintedBedroomPhysicsTable.asset"))
    B.TEXTURES = os.path.join(repo, "Assets", "InnerFrameBedroomWeb", "Data", "Textures")

    scene = bpy.context.scene
    B.fix_image_paths()
    liftables = B.liftables_from_table(table)
    used = {s.material for emp in B.room_objects() for ob in B.parts_of(emp) for s in ob.material_slots if s.material}
    for m in used:
        B.rebuild_material(m)
    for c in (bpy.data.collections.get("Ink"), bpy.data.collections.get("Markers")):
        if c is not None:
            c.hide_render = True
    for emp in B.room_objects():
        for ob in B.parts_of(emp):
            if emp.name in liftables:
                ob.hide_render = True              # the probe sees the room, not the object that will use it
            if emp.name == "Window_View" or any(sl.material and sl.material.get("lm_opacity", 1) < 1 for sl in ob.material_slots):
                ob.visible_shadow = False
    excluded = [ob for name in B.RIG["sky"].get("no_direct", []) if name in bpy.data.objects for ob in B.parts_of(bpy.data.objects[name])]
    sun, sky = B.make_rig(scene, [], excluded)
    if hasattr(sky, "visible_camera"):
        sky.visible_camera = True     # the probe must see the sky light to get its direct light; a sun lamp is never seen, Unity's realtime sun supplies that
    B.setup_cycles(scene, samples)
    scene.cycles.use_denoising = True
    scene.render.image_settings.file_format = "OPEN_EXR"; scene.render.image_settings.color_depth = "32"
    scene.render.image_settings.color_mode = "RGB"

    # the probe camera: equirectangular, up = world Z, forward = world +Y (the window wall)
    cam_d = bpy.data.cameras.new("LM_ProbeCam"); cam_d.type = "PANO"
    try:
        cam_d.panorama_type = "EQUIRECTANGULAR"
    except Exception:
        cam_d.cycles.panorama_type = "EQUIRECTANGULAR"
    cam = bpy.data.objects.new("LM_ProbeCam", cam_d); scene.collection.objects.link(cam)
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.camera = cam
    scene.render.resolution_percentage = 100

    # the grid: over the floor's footprint, inset a little from the walls
    floor = bpy.data.objects.get("Room_Floor")
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for ob in B.parts_of(floor):
        for c in ob.bound_box:
            w = ob.matrix_world @ Vector(c); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    inset = 0.2
    xs = np.arange(lo.x + inset, hi.x - inset + 1e-6, step); ys = np.arange(lo.y + inset, hi.y - inset + 1e-6, step)
    deps = bpy.context.evaluated_depsgraph_get()
    positions = []
    for z in heights:
        for y in ys:
            for x in xs:
                p = Vector((float(x), float(y), float(z)))
                if not inside_geometry(scene, deps, p):
                    positions.append(p)
    log(f"grid {len(xs)}x{len(ys)}x{len(heights)}, {len(positions)} probes outside geometry, {W}x{H} px, {samples} samples")

    R = cam.matrix_world.to_3x3()
    dirs = [[(R @ direction((i + 0.5) / W, (j + 0.5) / H)) for i in range(W)] for j in range(H)]
    tmp = os.path.join(bpy.app.tempdir, "probe.exr")

    def render(p, w, h, path):
        cam.location = p
        scene.render.resolution_x, scene.render.resolution_y = w, h
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path, check_existing=False); img.colorspace_settings.name = "Non-Color"
        a = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(a); bpy.data.images.remove(img)
        return a.reshape(h, w, 4)[::-1, :, :3]     # row 0 = top of the image

    if preview:
        centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, 1.6))
        scene.render.image_settings.file_format = "PNG"; scene.render.image_settings.color_depth = "8"
        cam.location = centre; scene.render.resolution_x, scene.render.resolution_y = 512, 256
        scene.render.filepath = os.path.abspath(preview); bpy.ops.render.render(write_still=True)
        scene.render.image_settings.file_format = "OPEN_EXR"; scene.render.image_settings.color_depth = "32"
        log(f"preview panorama at {tuple(round(v, 2) for v in centre)} (Blender) -> {preview}")

    probes = []
    for n, p in enumerate(positions):
        rad = render(p, W, H, tmp)
        probes.append({"position": [round(p.x, 4), round(p.z, 4), round(p.y, 4)],       # Unity x, y, z
                       "radiance": [round(float(v), 5) for v in rad.reshape(-1)]})        # flat r,g,b per pixel (JsonUtility)
        if n % 25 == 0:
            log(f"probe {n + 1}/{len(positions)} at Unity {probes[-1]['position']}, mean radiance {rad.mean():.3f}")
    # pixel directions in Unity space (x, y, z) = Blender (x, z, y), and each pixel's solid angle
    dir_u = [round(c, 5) for row in dirs for d in row for c in (d.x, d.z, d.y)]           # flat x,y,z per pixel
    solid = [round(float((2 * math.pi / W) * (math.pi / H) * math.cos(math.pi * (0.5 - (j + 0.5) / H))), 7) for j in range(H) for i in range(W)]
    meta = {"width": W, "height": H, "directions": dir_u, "solidAngles": solid, "samples": samples, "step": step,
            "heights": heights, "light": B.RIG, "blend": bpy.data.filepath, "probes": probes,
            "note": "radiance per pixel (linear, Blender units); Unity: sh.AddDirectionalLight(dir, radiance, solidAngle / pi)"}
    with open(os.path.abspath(out), "w") as f:
        json.dump(meta, f, separators=(",", ":"))
    log(f"wrote {len(probes)} probes -> {out} ({os.path.getsize(os.path.abspath(out)) // 1024} KB)")


if __name__ == "__main__":
    main()
