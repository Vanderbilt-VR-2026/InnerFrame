# Café Terrace at Night (environment, first pass)

Owner: Charlyne (Sprint 4, #141). The square in front of the café in Arles at night: the yellow wall and awning, the
terrace with its tables, the cobbled street running down between blue houses, a starry sky. The visitor walks in; Vincent
will stand at his easel where the scene lines up with the painting.

**State (2026-11-11): the environment only.** It builds and renders in the Editor. Not yet: in the build list, entered
from the Gallery, a multiplayer room, Vincent, people, the tree, the carriage (grey and pink stand-ins named `PH_...`),
sound. **Not tried on a headset yet.**

## Open it

- Scene: `Assets/CafeTerrace/Scenes/CafeTerrace.unity`. As in The Starry Night, it holds only the player rig (the Starter
  Assets `XR Origin (XR Rig)`, scaled 0.8 with 2 m rays) and one `CT_Bootstrap`, which spawns everything else from
  `Assets/CafeTerrace/Prefabs` at runtime.
- **Tools > InnerFrame > Cafe Terrace > Build** remakes the layout asset, import settings, materials, star meshes, prefabs
  and the scene. Running it twice changes nothing.
- **Tools > InnerFrame > Cafe Terrace > Render Check Views** writes the fixed check views to `Logs/CT_shots` (Editor, no
  Play, no headset) and logs the triangle and renderer counts.

Batch:

```
Unity -batchmode -projectPath . -executeMethod InnerFrame.CafeTerrace.EditorTools.CT_Setup.BuildBatch -quit -logFile Logs/ct_build.log
Unity -batchmode -projectPath . -executeMethod InnerFrame.CafeTerrace.EditorTools.CT_Shots.RunBatch -ctOut <dir> -quit -logFile Logs/ct_shots.log
```

## Where the numbers live

One source of truth: `Art/Source/CafeTerrace/cafe_layout.json` (real-world metres; x right, y down the street, z up; its
notes say who decided what and from which picture). Nothing is placed by hand in Unity.

```
cafe_layout.json ──> make_unity_export.py (Blender) ──> Assets/CafeTerrace/Models/*.fbx, Textures/CT_Chair|Table|Lantern.png
                                                   └──> Art/Source/CafeTerrace/unity_export.json ──> Build ──> CT_Layout.asset, prefabs, scene
```

- `make_blockout.py`: the grey blockout and the fixed cameras (scale check).
- `make_environment.py`: the set as geometry (each cobble a stone, cut openings, slatted shutters, planks, awning, balcony).
  `--look lit` renders it in Blender with the same lights that get baked.
- `make_unity_export.py`: bakes the light into the vertices and exports. About 3 minutes.
  `Blender --background --factory-startup --python Art/Source/CafeTerrace/make_unity_export.py -- --objects <folder with the Tripo .glb files>`
  (`objects.json` names the files; the `.glb` files are raw inputs and are not in the repo).
- `tone_check.py`: measures each surface's colour in Unity's check views against the target images and retunes the paint
  (`tone` in the layout). The target images are raw inputs and are not in the repo.
- `make_compare.py`, `make_swatches.py`: side-by-side sheets; the grey streak textures.

## How it is lit

No realtime lights. Blender computes the lantern, the glow under the awning and the night sky once and writes
paint × light into every vertex (the set, and each chair and table where it stands). `CT_Painted.shader` is unlit: vertex
light × texture. The lit windows and the lantern's flame are just bright colours.

## The sky

A copy of The Starry Night's dome and stars (Cici's `SN_Sky.shader`, `SN_StarField.shader`, night texture and star atlas,
copied into this folder so the scenes don't depend on each other): her painting, swirl and dawn stay off; the blue is
tuned to this scene's target image; more stars than hers, as in the painting.

## Scale

The rig is 0.8, as in every scene. The set is modelled in real metres and scaled 0.9 at runtime
(`CT_Layout.worldScale`), so it feels 1.125× real size: chosen on a Quest 3 on 2026-11-11 (at 0.8, which is 1:1 and
what the Bedroom uses today, it felt a little small). Both numbers are in the layout only. Vincent, when he is added,
has to be scaled by the same 0.9 / 0.8.

## Hooks for the rest of the journey

- `CT_Bootstrap.SpawnPose`: where the visitor arrives, in front of the small door of the back row (from the Bedroom).
- `CT_Bootstrap.PainterPose`, `VincentPose`: where the scene lines up with the painting, and where Vincent stands.
- `CT_ExitTrigger.Reached`: a step or two past the end of the street (on to The Starry Night). It only raises the event
  unless `loadScene` is ticked.

## Counts (Editor, edit mode, 2026-11-11)

About 414,000 triangles, 51 renderers, 56 material slots, 19 materials. Budget on Quest: 300–500k triangles. The people,
tree and carriage are not in yet. Draw calls in a frame and frame rate on a headset: not measured.

## Known gaps

- Surfaces: the yellow wall, cobbles, planks and awning use prototype textures cut from the target images; the other
  walls, the shop front and the shutters use script-made streaks. Painted swatches are to come.
- Stand-ins for people, tree, carriage, easel.
- No colliders on chairs; tables have boxes. The ground, terrace and pavements are teleport areas.

## Headset tests wanted

1. Does it hold 72 fps standing by the terrace and looking down the street?
2. Do the terrace, the tables and the doors feel real-sized (rig 0.8, set 0.8)?
3. Can you walk and teleport onto the terrace and along the street to its end without getting stuck?
