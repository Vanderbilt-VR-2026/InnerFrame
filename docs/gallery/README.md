# Gallery

The room visitors start in (Cici's scene): `Assets/Scenes/Gallery.unity`. The Bedroom hangs on the north wall and The
Starry Night on the east wall; each has a pedestal with a button that fades into its painting's scene.

Since 2026-11-10 the room follows the team's concept sheet (a small, warm 19th-century gallery): red walls over a grey
wood wainscot, a cornice, a skylight in the middle of the ceiling, an arched entrance with a closed door and lanterns,
one upholstered bench on a rug, a statue, plants and flowers. Its textures come from ChatGPT:
[TEXTURE_PROMPTS.md](TEXTURE_PROMPTS.md) has the prompts and how to bring the images in.

## Open it

Open `Assets/Scenes/Gallery.unity` and press Play (XR Device Simulator) or build with the Android profile.

## Layout (Unity coordinates)

The room is 7 × 9 × 4 m: x from −3.5 (east wall) to 3.5 (west wall), z from −4.5 (north wall, The Bedroom) to 4.5
(south wall, the arch). Visitors spawn at (0, 0, 3.5) facing north. (It was 10 × 14 × 6 m until 2026-11-10.)

| What | Where | Made by |
|---|---|---|
| Shell: walls, wainscot and rail, cornice, floor, ceiling and skylight, the arch and its door, the bench, the rug, The Bedroom's frame and pedestal | `Assets/Art/Gallery/Gallery.fbx` | `Art/Source/Gallery/build_gallery.py` |
| The shell's materials and textures, the rug's teleport area, the XR Origin at the spawn point | `Assets/Art/Gallery/Materials`, `Textures`; `Gallery.unity` | `Assets/Art/Gallery/Editor/GalleryShellSetup.cs` (run by Add Decor) |
| The Starry Night, its frame and pedestal: on the east wall, centred at z = −1.6 | `SN_GalleryEntrance` (last root) | `Assets/StarryNight/Editor/SN_GallerySetup.cs` |
| Vincent's self-portrait (at run time), west of The Bedroom, 0.2 m from its frame | — | `Assets/VanGogh/Scripts/VanGoghSpawner.cs` |
| Decoration (below) | `GalleryDecor` (the root before `SN_GalleryEntrance`) | `Assets/Art/Gallery/Editor/GalleryDecorSetup.cs` |

### The shell

| Part | What | Material (texture slot) |
|---|---|---|
| Walls, arch spandrels | 0.2 m thick | `Mat_Wall` (`wall.png`, 2 m per repeat) |
| Wainscot | 1.0 m panelling on every wall, stopping at the two frames and the arch | `Mat_Wainscot` (`wainscot.png`, 1.5 m per repeat, once upwards) |
| Cornice (0.25 m under the ceiling) | | `Mat_Cornice` (`cornice.png`, a moulding profile mapped once up the band, 0.5 m across) |
| Wainscot rail (0.06 m) | | `Mat_Rail` (`trim.png`, mapped once up the band) |
| Arch moulding, skylight frame | | `Mat_Trim` (a plain panel cut from `trim.png`, 0.5 m) |
| Ceiling, with the 3 × 2.5 m skylight in its middle | | `Mat_Ceiling` (`ceiling.png`, 2 m) |
| Floor | | `Mat_Floor` (`floor.png`, 2 m) |
| Arch | 1.6 m wide, round-headed, 2.6 m to the crown; the door fills it at the wall's outer face | door: `Mat_Door` (`door.png`, once) |
| Bench (`Bench_01`) | 1.6 × 0.55 m, 0.48 m high, centred at z = 1.2: an upholstered top on a walnut frame with turned legs, on the rug, facing The Bedroom (0.5 m clear of where you stand at The Starry Night's pedestal) | `Mat_BenchWood` (`bench_wood.png`, 0.6 m), `Mat_Cushion` (`bench_fabric.png`, 0.5 m) |
| Rug | 2 × 3 m under the bench, long side towards The Bedroom; you can teleport onto it | `Mat_Rug` (`rug.png`, once) |

Without its texture a surface keeps a plain colour from the concept sheet. All eleven are in (2026-11-10). The door came as one quarter of a
ChatGPT sheet: 423 × 546 pixels, so it is soft up close; a full-size `door.png` (1024 × 1536) would sharpen it.

### Decoration (`GalleryDecor.prefab`)

| Prop | Count | Where |
|---|---|---|
| Marble statue on a 0.6 m plinth | 1 | West of the arch (1.9, 2.8), turned to the bench and The Bedroom |
| Potted plant (0.7×, 0.9 m tall) | 4 | The room's four corners |
| Hydrangeas on a 0.9 m plinth | 2 | East wall by the arch (−3.0, 2.4); west wall (3.0, −1.0) |
| Lantern with a lit candle inside, on a wall arm | 3 | Over the arch (its ring at 2.72 m) and each side of it (x = ±1.45, 2.35 m) |
| Carved frame corner | 8 | The four corners of both paintings' frames |

### Lamp light

Each lantern is built like The Starry Night's (`SN_Lantern`):

- **Glass:** `make_tripo_props.py` splits the Tripo lantern into its brass (`GP_Lantern`, with the Tripo candle inside)
  and its six amber panes (`GP_Lantern_Glass`), by the texture's colour under each face. The panes get
  `GP_LanternGlass.mat`, see-through amber glass. It is URP Unlit, not Lit as The Starry Night's glass is: the lamp sits
  a few centimetres from the panes and would light them white.
- **Flame:** The Starry Night's own flame (`SN_FlameCross` mesh, `SN_Flame` material), 1.3× its size, standing on the
  candle's wick (0.417 m below the ring's top, measured into `tripo_props.json`).
- **Light:** a warm (about 2700 K) realtime point light 1 cm above the flame: intensity 3, range 6 m, no shadows.

The Quest's render asset (`Mobile_RPAsset`) draws up to 4 extra per-pixel lights per object and no extra-light shadows.
Three lamps leave one for the table lamp the concept sheet has (below). `GalleryDecorCheck` fails if the scene has more
than 4.

Only the plinths and plant pots (boxes) and the shell (Gallery.fbx's generated mesh colliders) stop you; the lanterns and
frame corners have none. Nothing covers the spawn point's view of The Bedroom or Vincent's portrait.

### Still to make (in the concept sheet, no model yet)

| Asset | Where it would go | Tripo prompt idea |
|---|---|---|
| Table lamp with a pleated shade | On a side table by the east wall, near The Bedroom (the 4th light) | "a brass table lamp with a pleated cream fabric shade, 19th-century, warm, single object" |
| Small round side table, dark wood | Under the table lamp; another by the east wall for the flowers | "a small round side table in dark walnut, turned legs, Victorian" |
| Hanging ivy | Spilling from a corner high on the north wall | "a trailing ivy plant, long hanging vines, single object" |
| Stack of old books | On the bench and the side table | "a small stack of three old leather-bound books" |
| Reading stand | Beside the statue, for its caption | "a black metal museum reading stand with a slanted top" |

## Change the decoration

Don't move the props in the scene: the next run of the setup reverts any override. Change the spots in
`GalleryDecorSetup.cs` (the `Statues`, `Plants`, `Vases`, `Lanterns` and `Frames` tables), then run
**Tools > InnerFrame > Gallery > Add Decor** and **Tools > InnerFrame > Gallery > Check Decor**. The check also renders
six views (five from eye height, one close to the arch lantern) to `Logs/GalleryDecor/`.

To change the room itself, edit the parameters at the top of `build_gallery.py`, run it, then Add Decor. If the east
wall or the north wall moves, move `SN_GallerySetup.RootPosition` and `BedroomWall` with it and run
**Tools > InnerFrame > Starry Night > Add to Gallery**.

Batch:

```
Blender -b --factory-startup --python Art/Source/Gallery/build_gallery.py
Unity -batchmode -nographics -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.GalleryDecorSetup.RunBatch -quit
Unity -batchmode -nographics -projectPath . -executeMethod InnerFrame.StarryNight.EditorTools.SN_GallerySetup.RunBatch -quit
Unity -batchmode -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.Tests.GalleryDecorCheck.RunBatch -quit
```

To rebuild the meshes from Cici's Tripo GLBs (the `gallery-tripo` folder, not in the repo):

```
Blender --background --factory-startup --python Art/Source/Gallery/make_tripo_props.py -- <gallery-tripo dir>
```

## Hardware tests to run on a headset

- Spawn: The Bedroom and Vincent's portrait are in full view; nothing is in the way.
- Teleport onto the rug and round the bench; you can't walk through the bench, the plinths, the pots or the door.
- Look back at the arch: the door fills it, the three lanterns glow, you see each candle through the amber glass.
- The wainscot stops cleanly at both paintings' frames; the frame corners sit on the frames with no flicker.
- The plant leaves, hydrangea petals and lantern panes look solid from every side.
- With textures in: no visible seam where the wall, floor, wainscot, wood or fabric repeat.
- Frame rate stays steady looking down the room.
