# Gallery

The room visitors start in (Cici's scene): `Assets/Scenes/Gallery.unity`. The Bedroom hangs on the north wall and The
Starry Night on the east wall; each has a pedestal with a button that fades into its painting's scene.

## Open it

Open `Assets/Scenes/Gallery.unity` and press Play (XR Device Simulator) or build with the Android profile.

## Layout (Unity coordinates)

The room is 10 × 14 × 6 m: x from −5 (east wall) to 5 (west wall), z from −7 (north wall, The Bedroom) to 7 (south
wall, entrance). Visitors spawn at (0, 0, 6) facing north.

| What | Where | Made by |
|---|---|---|
| Shell: walls, floor, ceiling, skylight, benches, The Bedroom's frame and pedestal | `Assets/Art/Gallery/Gallery.fbx` | `Art/Source/Gallery/build_gallery.py` |
| The Starry Night, its frame and pedestal | `SN_GalleryEntrance` (last root) | `Assets/StarryNight/Editor/SN_GallerySetup.cs` |
| Decoration (below) | `GalleryDecor` (the root before `SN_GalleryEntrance`) | `Assets/Art/Gallery/Editor/GalleryDecorSetup.cs` |

### Decoration (`GalleryDecor.prefab`)

| Prop | Count | Where |
|---|---|---|
| Marble statue on a 0.6 m plinth | 1 | West side, across from The Starry Night, facing it (4.1, −3.6) |
| Potted plant | 4 | The room's four corners, 0.9 m from both walls |
| Hydrangeas on a 0.9 m plinth | 2 | West of The Bedroom (2.5, −6.4); east wall between the benches (−4.45, 2) |
| Hanging lantern on a chain, with a warm light | 4 | Over the aisle, x = ±2.4, z = 3.5 and −1.5, bottom 2.9 m up |
| Carved frame corner | 8 | The four corners of both paintings' frames |

### Lamp light

Each lantern holds a warm (about 2700 K) realtime point light: intensity 4, range 9 m, no shadows. Its amber panes glow
through an emission map (`GP_Lantern_Emission.jpg`, made by `make_tripo_props.py` from the base colour: the panes kept,
the brass black). The Quest's render asset (`Mobile_RPAsset`) draws up to 4 extra per-pixel lights per object and no
extra-light shadows, so four lamps fit with no settings change. Add a fifth light anywhere in the Gallery and some objects
will drop one; `GalleryDecorCheck` fails if the scene has more than 4.

About 45,000 triangles in total. Only the plinths and the plant pots have colliders (boxes), so you can't walk through
them; the lanterns and frame corners have none. Nothing covers the spawn point's view of The Bedroom.

## Change the decoration

Don't move the props in the scene: the next run of the setup reverts any override. Change the spots in
`GalleryDecorSetup.cs` (the `Statues`, `Plants`, `Vases`, `Lanterns` and `Frames` tables), then run
**Tools > InnerFrame > Gallery > Add Decor** and **Tools > InnerFrame > Gallery > Check Decor**. The check also renders
five views from eye height to `Logs/GalleryDecor/`.

Batch:

```
Unity -batchmode -nographics -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.GalleryDecorSetup.RunBatch -quit
Unity -batchmode -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.Tests.GalleryDecorCheck.RunBatch -quit
```

To rebuild the meshes from Cici's Tripo GLBs (the `gallery-tripo` folder, not in the repo):

```
Blender --background --factory-startup --python Art/Source/Gallery/make_tripo_props.py -- <gallery-tripo dir>
```

## Hardware tests to run on a headset

- Spawn: The Bedroom is in full view; no lantern or plant is in the way.
- Walk round the statue and both vase plinths: you stop at them and don't pass through.
- The corners of both frames: the ornaments sit on the frame, with no flicker (z-fighting) against it.
- The plant leaves, hydrangea petals and lantern panes look solid from every side (they render both faces).
- The lamps: the walls and ceiling round each lantern look warm, the panes glow amber, and nothing flickers as you walk under them.
- Frame rate stays steady while you look down the length of the room (four realtime lights are new here).
