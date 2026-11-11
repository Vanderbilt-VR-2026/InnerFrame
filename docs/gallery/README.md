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
| Frame (`Frame_Bedroom`, and The Starry Night's copy of it) | A carved baroque frame made in `build_gallery.py`, after the team's reference: a moulded profile (stepped sight edge, inner bead, hollow, broad flat band, bold rounded outer rim) swept round the opening, and at each corner a cartouche: a raised cushion, a C-scroll curling along each rail, a fan of five acanthus leaves and a boss. 0.18 m wide (the cartouches stand 1.5 cm past its edge; the wainscot's cut-outs leave 3 cm round it), about 2,600 triangles | `Mat_Frame` (`frame.png`, ChatGPT gilt): its UVs run along each rail and up the profile from the inner edge, so the strokes follow the moulding; each ornament takes a patch of the gilt's middle band, smooth-shaded (also Vincent's portrait's frame) |
| Pedestals (`Pedestal_Bedroom`, and The Starry Night's copy of it) | A black reading stand: a moulded base, a slim post widening to its foot, and a head with the same tilted reading surface as before, filling the same 0.5 × 0.33 × 1 m box, so its button and placard did not move | `Mat_Bench` |
| Bench (`Bench_01`) | 1.6 × 0.55 m, 0.48 m high, centred at z = 1.2: an upholstered top on a walnut frame with turned legs, on the rug, facing The Bedroom (0.5 m clear of where you stand at The Starry Night's pedestal) | `Mat_BenchWood` (`bench_wood.png`, 0.6 m), `Mat_Cushion` (`bench_fabric.png`, 0.5 m) |
| Rug | 2 × 3 m under the bench, long side towards The Bedroom; you can teleport onto it | `Mat_Rug` (`rug.png`, once) |

Without its texture a surface keeps a plain colour from the concept sheet. All eleven are in (2026-11-10). The door came as one quarter of a
ChatGPT sheet: 423 × 546 pixels, so it is soft up close; a full-size `door.png` (1024 × 1536) would sharpen it.

### Decoration (`GalleryDecor.prefab`)

| Prop | Count | Where |
|---|---|---|
| Marble statue on a cream marble plinth (0.7 m square, 0.6 m: base, step, die, cap, top slab) | 1 | West of the arch (1.9, 2.8), turned to the bench and The Bedroom |
| Potted plant (0.7×, 0.9 m tall) | 3 | The two north corners, and one on the east side of the arch, beside the door (the one behind the statue was taken out) |
| Hydrangeas on a turned walnut column stand (top 0.45 m square at 0.9 m) | 2 | East wall by the arch (−3.0, 2.4); west wall (3.0, −1.0) |
| Lantern with a lit candle inside, on a wall arm | 4 | Each side of the arch (x = ±1.45, its ring at 2.35 m), and one on each side wall at z = 1 (just south of The Starry Night's frame, so neither covers a painting) |
| Sky | 1 | A painted sky card (`GT_Sky`, ChatGPT clouds) seen through the skylight: unlit at 72 % brightness (full glared against the warm room), facing down, 14 m square, 2 m above the ceiling, so its edge never shows; the clouds repeat every 4 m. It casts no shadow, so the sun still comes in |
| Ceiling spotlight: a dark fitting, a warm bulb, a ball of light under it, a glow on the ceiling and a pool of light on the floor below | 16 | A ring 0.3 m outside the skylight's frame, five along each long side and three along each short one, as in the concept sheet. Not lights (the four lanterns use the four the Quest lights per object): the bulb is unlit; the ceiling glow (2 m), the ball (0.8 m) and the floor pool (1.8 m, just above the rug) are The Starry Night's glow shader and soft dot added on top. The ball is a card that `Scripts/GP_GlowBillboards.cs` turns to face each camera as it renders (and puts back after, so the scene never saves it turned); the ceiling hides its upper half. The floor pools brighten the floor, not the objects standing on it |

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
Four lamps use all four; the table lamp the concept sheet has (below) would have to replace one. `GalleryDecorCheck` fails if the scene has more
than 4.

Only the plinths and plant pots (boxes) and the shell (Gallery.fbx's generated mesh colliders) stop you; the lanterns and
the frames' carving has its generated collider like the rest of the shell. Nothing covers the spawn point's view of The Bedroom or Vincent's portrait.

### Still to make (in the concept sheet, no model yet)

| Asset | Where it would go | Tripo prompt idea |
|---|---|---|
| Table lamp with a pleated shade | On a side table by the east wall, near The Bedroom (its light would replace one of the four lanterns') | "a brass table lamp with a pleated cream fabric shade, 19th-century, warm, single object" |
| Small round side table, dark wood | Under the table lamp; another by the east wall for the flowers | "a small round side table in dark walnut, turned legs, Victorian" |
| Hanging ivy | Spilling from a corner high on the north wall | "a trailing ivy plant, long hanging vines, single object" |
| Stack of old books | On the bench and the side table | "a small stack of three old leather-bound books" |
| Reading stand | Beside the statue, for its caption | "a black metal museum reading stand with a slanted top" |

## Change the decoration

Don't move the props in the scene: the next run of the setup reverts any override. Change the spots in
`GalleryDecorSetup.cs` (the `Statues`, `Plants`, `Vases`, `Lanterns` and `Frames` tables), then run
**Tools > InnerFrame > Gallery > Add Decor** and **Tools > InnerFrame > Gallery > Check Decor**. The check also renders
eight views (five from eye height; a side-wall lamp, the frame's corner and the ceiling spots close up) to `Logs/GalleryDecor/`.

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
- Look back at the arch: the door fills it; the four lanterns glow, you see each candle through the amber glass.
- The wainscot stops cleanly at both paintings' frames; the frames' carving reads as one piece with the frame.
- The plant leaves, hydrangea petals and lantern panes look solid from every side.
- With textures in: no visible seam where the wall, floor, wainscot, wood or fabric repeat.
- Frame rate stays steady looking down the room.
