# Gallery

The room visitors start in (Cici's scene): `Assets/Scenes/Gallery.unity`. Five paintings hang round it in the order you
meet them from the door, left to right: Vincent's 1887 self-portrait, then the four of the story, each with its wall
label (number and time of day): 1 The Bedroom (Afternoon), 2 Café Terrace at Night (Late afternoon), 3 The Starry Night
(Evening), 4 Almond Blossom (Morning). You enter a painting by pointing at it and pulling the trigger: a small menu opens
beside it (Enter, Back). There are no stands (2026-11-12, Part B; until then each painting had a pedestal with a button).

Since 2026-11-10 the room follows the team's concept sheet (a small, warm 19th-century gallery): painted walls over a grey
wood wainscot, a cornice, a skylight in the middle of the ceiling, an arched entrance with lanterns, one upholstered
bench on a rug, a statue, plants and flowers. Its textures come from ChatGPT:
[TEXTURE_PROMPTS.md](TEXTURE_PROMPTS.md) has the prompts and how to bring the images in. Since 2026-11-12 the walls are
yellow and the arch looks into a corridor, after the yellow concept photo
(`Art/Source/Gallery/textures/concept_yellow.png`); until then they were red and a closed double door filled the arch.

## Open it

Open `Assets/Scenes/Gallery.unity` and press Play (XR Device Simulator) or build with the Android profile.

## Layout (Unity coordinates)

**One source of truth: `Assets/Art/Gallery/Layout/GalleryLayout.asset`** (class `Assets/Art/Gallery/Scripts/GalleryLayout.cs`).
It holds the room's size, the bench, the paintings in order (title, caption, real canvas size in cm, image, material, the
scene Enter opens or none for "Coming soon", wall and place, label number and time) and where the decor stands. Edit it in
the Inspector, then build:

1. `Art/Source/Gallery/build_gallery.py` (Blender) reads the room, the bench and The Bedroom's place and size from it
   and writes `Gallery.fbx` (only needed when those change).
2. **Tools > InnerFrame > Gallery > Build Layout** (`GalleryLayoutSetup.cs`) works out the hanging sizes, writes them back
   into the asset, and builds the scene from it. It is idempotent: a second run changes nothing.

The room is 9 × 12 × 4 m: x from −4.5 (east wall) to 4.5 (west wall), z from −6 (north wall) to 6 (south wall, the arch).
Visitors spawn at (0, 0, 5) facing The Bedroom. (7 × 9 m until 2026-11-12, 10 × 14 m until 2026-11-10.)

**Hanging.** Every painting at one scale (2.163 m of wall per metre of canvas), so their real sizes compare; The Bedroom
was to be 2.2 m wide, but then the Café's centre would be at 2.29 m, so all shrank by the same ratio until no centre is
above 2.2 m (`maxCentre`): The Bedroom is 2.00 m wide. Every frame's bottom edge is 10 cm above the wainscot's rail
(1.03 m), so no frame cuts the wainscot.

| # | Painting | Wall, place | Canvas as hung | Enter |
|---|---|---|---|---|
| — | Self-Portrait, 1887 (AIC) | west, z 2.5 (by the door) | 0.70 × 0.89 m | none: hold B to ask Vincent |
| 1 | The Bedroom, 1889 | west, z −2.5 | 2.00 × 1.59 m | PaintedBedroom |
| 2 | Café Terrace at Night, 1888 | north, centre | 1.41 × 1.75 m | CafeTerrace |
| 3 | The Starry Night, 1889 | east, z −2.5 | 1.99 × 1.59 m | StarryNight |
| 4 | Almond Blossom, 1890 | east, z 2.5 (by the door) | 2.00 × 1.59 m | Coming soon (no scene yet) |

| What | Where | Made by |
|---|---|---|
| Shell: walls, wainscot and rail, cornice, floor, ceiling and skylight, the arch and its door, the bench, the rug, The Bedroom's frame | `Assets/Art/Gallery/Gallery.fbx` | `Art/Source/Gallery/build_gallery.py` (reads `GalleryLayout.asset`) |
| The shell's materials and textures, the rug's teleport area | `Assets/Art/Gallery/Materials`, `Textures`; `Gallery.unity` | `GalleryShellSetup.cs` (run by Add Decor) |
| The paintings (`Gallery Paintings`), their wall labels (`Cici Artwork Cards`), the pointer and menu (`Gallery Painting Menu`), the spotlights, the decor's places, the XR Origin at the spawn point | `Gallery.unity` | `GalleryLayoutSetup.cs` (Build Layout) |
| Vincent's voice at his portrait (at run time, `VanGogh_Portrait` at `Slot_SelfPortrait`) | — | `Assets/VanGogh/Scripts/VanGoghSpawner.cs` |
| The decoration's models (below) | `GalleryDecor` | `GalleryDecorSetup.cs` (Add Decor) |

"Add to Gallery" (`SN_GallerySetup`, The Starry Night's image and material) and Rowling's "Install Cafe and space
artworks" (`CafeGallerySetup`) now hand the placing to Build Layout.

### Entering a painting

- Point at a painting: its frame gets a pale rim (#178's hover look) and a thin cream ray shows, longer than the
  controller's own 2 m ray. Pull the **trigger**: a menu opens beside the painting (or, from farther than 3.5 m, 3.5 m in
  front of you toward it), always the same size to the eye. Point at **Enter** and pull the trigger: the usual fade into
  the painting (its `SceneChangeButton`, as the pedestals did). **Back** closes it.
- The menu closes on Back, when you open another painting's, when you walk 2 m from where you opened it, or when you
  turn your back on it. It is yours alone (not shared). Almond Blossom says "Coming soon" until its scene exists.
- Nothing is rebound: grip is still XRI Select, the trigger Activate (and UI Press), A Jump. The trigger is read from each
  hand's own Activate input (`GalleryPaintingPointer`). In the Editor without a headset: the centre dot or the mouse, E or a click.
- The wall labels' "More →" and "×" work with the trigger as well as the grip. ENTER is off the labels.

### The shell

| Part | What | Material (texture slot) |
|---|---|---|
| Walls, arch spandrels | 0.2 m thick | `Mat_Wall` (`wall.png`, yellow, 2 m per repeat, colours as ChatGPT made them; the red wall before 2026-11-12 was shifted from crimson to orange-red by `make_textures.py`'s `GAIN`) |
| Wainscot | 1.0 m panelling on every wall, unbroken except at the arch (the frames hang above it) | `Mat_Wainscot` (`wainscot.png`, 1.5 m per repeat, once upwards) |
| Cornice (0.25 m under the ceiling) | | `Mat_Cornice` (`cornice.png`, a moulding profile mapped once up the band, 0.5 m across) |
| Wainscot rail (0.06 m) | | `Mat_Rail` (`trim.png`, mapped once up the band) |
| Arch moulding, skylight frame | | `Mat_Trim` (a plain panel cut from `trim.png`, 0.5 m) |
| Ceiling, with the 3 × 2.5 m skylight in its middle | | `Mat_Ceiling` (`ceiling.png`, 2 m; lightened to cream by `GAIN`) |
| Floor | | `Mat_Floor` (`floor.png`, 2 m) |
| Arch | 1.6 m wide, round-headed, 2.6 m to the crown; a slab (still named `Door`, with its collider, so nobody walks out) closes it at the wall's outer face and shows the corridor beyond | `Mat_Door` (`corridor.png`, once: cropped to the arch's 1.6 × 2.6 m, blurred and darkened by `make_textures.py`'s `backdrop`) |
| Frame (`Frame_Bedroom`; every other painting's frame is a copy of it, scaled to its canvas) | A carved baroque frame made in `build_gallery.py`, after the team's reference: a moulded profile (stepped sight edge, inner bead, hollow, broad flat band, bold rounded outer rim) swept round the opening, and at each corner a cartouche: a raised cushion, a C-scroll curling along each rail, a fan of five acanthus leaves and a boss. 0.18 m wide (the cartouches stand 1.5 cm past its edge; the wainscot's cut-outs leave 3 cm round it), about 2,600 triangles | `Mat_Frame` (`frame.png`, ChatGPT gilt): its UVs run along each rail and up the profile from the inner edge, so the strokes follow the moulding; each ornament takes a patch of the gilt's middle band, smooth-shaded (also Vincent's portrait's frame) |
| Bench (`Bench_01`) | 1.6 × 0.55 m, 0.48 m high, in the middle of the room (`GalleryLayout.bench`): an upholstered top on a walnut frame with turned legs, on the rug | `Mat_BenchWood` (`bench_wood.png`, 0.6 m), `Mat_Cushion` (`bench_fabric.png`, 0.5 m) |
| Rug | 2 × 3 m under the bench, long side towards The Bedroom; you can teleport onto it | `Mat_Rug` (`rug.png`, once) |

Without its texture a surface keeps a plain colour from the concept sheet. All eleven are in (2026-11-10).

**The yellow wall and the corridor (2026-11-12).** Their source images are in the repo, in
`Art/Source/Gallery/textures/` (`wall.png`, `corridor.png`, and the concept photo they follow, `concept_yellow.png`);
`make_textures.py -- Art/Source/Gallery/textures` remakes `GT_Wall` and `GT_Corridor` from them, then **Add Decor**.
- Seams: ChatGPT's wall was nearly seamless already. `make_textures.py` prints the step across each repeat over the
  99th percentile of the steps inside the image: left to right 1.00, top to bottom 1.17 (a faint seam). It cross-fades
  12 % of each edge into the opposite one, as for every tiling texture, and now scales the image with a margin wrapped
  round from the opposite edge (scaled on its own, the edges resampled differently and brought a faint seam back):
  0.95 and 0.91 after. The 90 % JPEG adds a little at the edge (about 1.1 measured on the file, the red wall's was 1.08);
  no line shows in a full-size crop where four repeats meet.
- Brightness: the yellow is used as it came (mean sRGB 246, 185, 55; the red was 179, 63, 35), with no `GAIN`. Above
  every painting the wall stays yellow where its spotlight's cone grazes it: no point as white as 235 in all three
  channels; red clips on about 1 to 3 % of the 60 cm above the frame (The Starry Night most, 3 %, as with the red wall),
  which shows as a light yellow (255, 200, 80), not white.
- The corridor: `corridor.png` cropped from its centre to the arch's shape, blurred (sigma 0.4 % of its width) and darkened
  (× 0.7) so it reads as further off than the lit room; the old `GT_Door` is gone, and so are the door's two handles.

### Decoration (`GalleryDecor.prefab`; where each stands: `GalleryLayout.asset`'s decor list, set by Build Layout)

Since Part B everything stands along the walls, clear of every painting, its label and its menu: the statue in the
north-west corner, plants in the north-east corner, on the west wall between the portrait and The Bedroom, and by the arch,
a flower stand between The Starry Night and Almond Blossom and one by the arch. The table below is the models.

| Prop | Count | Where |
|---|---|---|
| Marble statue on a cream marble plinth (`marble.png`, ChatGPT, 0.6 m) (0.7 m square, 0.6 m: base, step, die, cap, top slab) | 1 | West of the arch (1.9, 2.8), turned to the bench and The Bedroom |
| Potted plant (0.7×, 0.9 m tall) | 3 | The two north corners, and one on the east side of the arch, beside the door (the one behind the statue was taken out) |
| Hydrangeas on a turned walnut column stand (top 0.45 m square at 0.9 m) | 2 | East wall by the arch (−3.0, 2.4); west wall (3.0, −1.0) |
| Lantern with a lit candle inside, on a wall arm | 4 | Each side of the arch (x = ±1.45, its ring at 2.35 m), and one on each side wall at z = 1 (just south of The Starry Night's frame, so neither covers a painting) |
| Sky | 1 | A painted sky card (`GT_Sky`, ChatGPT clouds) seen through the skylight: unlit at 95 % brightness (72 % until the room was brightened on 2026-11-11), facing down, 14 m square, 2 m above the ceiling, so its edge never shows; the clouds repeat every 4 m. It casts no shadow, so the sun still comes in |
| Ceiling spotlight: a dark fitting, a warm bulb, a ball of light under it, a glow on the ceiling and a pool of light on the floor below | 16 | A ring 0.3 m outside the skylight's frame, five along each long side and three along each short one, as in the concept sheet. Not lights (the four lanterns use the four the Quest lights per object): the bulb is unlit; the ceiling glow (2 m), the ball (0.8 m) and the floor pool (1.8 m, just above the rug) are The Starry Night's glow shader and soft dot added on top. The ball is a card that `Scripts/GP_GlowBillboards.cs` turns to face each camera as it renders (and puts back after, so the scene never saves it turned); the ceiling hides its upper half. The floor pools brighten the floor, not the objects standing on it |

### Lamp light

Each lantern is built like The Starry Night's (`SN_Lantern`):

- **Glass:** `make_tripo_props.py` splits the Tripo lantern into its brass (`GP_Lantern`, with the Tripo candle inside)
  and its six amber panes (`GP_Lantern_Glass`), by the texture's colour under each face. The panes get
  `GP_LanternGlass.mat`, see-through amber glass. It is URP Unlit, not Lit as The Starry Night's glass is: the lamp sits
  a few centimetres from the panes and would light them white.
- **Flame:** The Starry Night's own flame (`SN_FlameCross` mesh, `SN_Flame` material), 1.3× its size, standing on the
  candle's wick (0.417 m below the ring's top, measured into `tripo_props.json`).
- **Light:** a warm (2800 K, `LampKelvin`) realtime point light 1 cm above the flame: intensity 1.6, range 4 m, no
  shadows. Decoration, not the room's main light (intensity 3 and range 6 m until 2026-11-11): dimmer, so its bright patch on the wall is small.

**Since Part B (2026-11-12) the lanterns' lamps are off**: their glass and flame still glow (the materials are
unchanged), but they light nothing, so the five painting spotlights fit the budget below with one to spare
(`GalleryLayout` decor `lit`). Before, the four lanterns and the two painting spotlights used all six.

The Quest's render asset (`Mobile_RPAsset`) draws up to 6 extra per-pixel lights per object and no extra-light shadows.
Before Part B, the four lanterns and the two painting spotlights used all six (the floor, walls and rug are one mesh each across the
room, so they meet every light); the table lamp the concept sheet has (below) would have to replace one. `GalleryDecorCheck`
fails if the scene has more point and spot lights than the limit. (A fifth, hanging in front of The Bedroom, was tried on 2026-11-11 and
taken out: it added little.)

### Sun, shadows, fill and spotlights (`GalleryLightingSetup.cs`, added by Add Decor, reset by Reset Lighting to Built)

Three kinds of light, by colour temperature: the lanterns warmest (2800 K), the painting spotlights white (6600 K,
`SpotKelvin`, so the paintings keep their true colours), the fill 3800 K (`AmbientKelvin`). `Kelvin()` turns a temperature into a colour.

- **Sun:** the scene's one directional light, aimed through the skylight onto the floor about 2.5 m in front of The
  Bedroom (`SunEuler` 58, 180, 0), warm, intensity 1.6, soft shadows. A second directional light is switched off, not deleted.
- **Shadows** (both render assets, so the editor matches the headset): 12 m distance over one 2048 map (about 1 cm per
  texel), depth and normal bias 0.3, soft shadows at low quality. Before, 50 m over a 1024 map on the Quest with a
  normal bias of 1 made the skylight's shadow blocky, and the bias ate its 6 cm bars, so light leaked between them.
- **Fill (ambient):** trilight, soft and neutral-warm, so the room isn't dark between the lamps: 3800 K at 0.8 from above,
  0.65 at the walls' height and 0.55 from below (`AmbientSkyLevel`, `AmbientEquatorLevel`, `AmbientGroundLevel`; the last
  lights the ceiling, which faces down). The default skybox ambient was dim and blue,
  and without a lighting bake the headset doesn't get the editor's skybox ambient.
- **The Bedroom's painting** (`Mat_TheBedroom.mat`): URP Lit, white, smoothness 0.1
  (no glow: its spotlight lights it). It was URP Unlit tinted
  blue-grey (0.42, 0.58, 0.71), so no lamp lit it and the tint halved its brightness.
  The Starry Night's (`SN_GalleryPainting.mat`) is URP Lit, white and matte too.
- **Painting spotlights** (`PaintingLights`, a scene root after the sun): one per painting in `GalleryLayout.asset` (five), on a small dark fitting (a stem
  and a can) 0.12 m below the ceiling and 1.2 m out from the painting, aimed at its middle, its cone 6° wider than the
  painting's diagonal; white (6600 K), intensity 5 (`SpotIntensity`), no shadows. Placed from the paintings' own positions in
  the scene, so they follow a painting that moves.

Don't tune the light in Play mode: Unity throws away every change made in Play mode when you stop. Tune it in Edit mode
and save (Add Decor keeps it), or change the constants at the top of `GalleryLightingSetup.cs` and run
**Tools > InnerFrame > Gallery > Reset Lighting to Built**.

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

**Hand edits stay.** Since 2026-11-11, **Tools > InnerFrame > Gallery > Add Decor** only adds what is missing (the decor,
its prefab, a material, the painting spotlights, an empty texture slot) and leaves everything already there as it is. Move
a lantern, tune the sun or a material in the editor, save, and Add Decor keeps it. Make those edits in **Edit mode**: Unity
itself throws away changes made in Play mode when Play mode ends.

To go back to the code's values, run **Tools > InnerFrame > Gallery > Reset to Built (discards hand edits)**. It reverts the
decor instance's overrides, rebuilds the prefab, and rewrites the materials, the lighting and the render settings. It never
deletes objects you added yourself. **Reset Lighting to Built** does the same for the lighting only. Changing the spots in
`GalleryDecorSetup.cs` (the `Statues`, `Plants`, `Vases` and `Lanterns` tables) takes a Reset to Built to show. Then run
**Tools > InnerFrame > Gallery > Check Decor**. The check also renders
nine views (five from eye height; a side-wall lamp, the frame's corner, the ceiling spots and a pedestal close up) to `Logs/GalleryDecor/`.

To change the room, the paintings or where things stand, edit `GalleryLayout.asset`, run `build_gallery.py` if the room,
the bench or The Bedroom changed, then **Build Layout**. Check it with **Tools > InnerFrame > Gallery > Check Layout**
(`GalleryLayoutCheck`: order, places, no stands, every painting pointable, labels, spotlights, the light budget, a second
run changing nothing; each with a control that must fail) and, in Play mode, `GalleryLayoutPlayCheck` (simulated XR input:
point, rim, trigger, menu, Enter into each scene from 2 m, 5 m and across the room; the menu's closing rules; the music).

Batch:

```
Blender -b --factory-startup --python Art/Source/Gallery/build_gallery.py
Unity -batchmode -nographics -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.GalleryDecorSetup.RunBatch -quit
Unity -batchmode -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.GalleryLayoutSetup.RunBatch
Unity -batchmode -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.Tests.GalleryLayoutCheck.RunBatch -glLog <file>
Unity -batchmode -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.Tests.GalleryDecorCheck.RunBatch -quit
Unity -batchmode -projectPath . -executeMethod InnerFrame.Gallery.EditorTools.Tests.GalleryLayoutPlayCheck.RunBatch -gpOut <dir>
```

To rebuild the meshes from Cici's Tripo GLBs (the `gallery-tripo` folder, not in the repo):

```
Blender --background --factory-startup --python Art/Source/Gallery/make_tripo_props.py -- <gallery-tripo dir>
```

## Hardware tests to run on a headset

- Spawn: you face The Bedroom; Vincent's portrait is on your left by the door; nothing is in the way.
- Point at each painting from close, from the middle and from across the room: the rim shows, the trigger opens the menu
  beside it (or in front of you from afar), readable; Enter fades into the right scene; Almond Blossom says Coming soon.
- The menu closes when you walk away or turn round; the grip and A do nothing new; B still asks Vincent.
- Teleport onto the rug and round the bench; you can't walk through the bench, the plinths, the pots or the door.
- Look back at the arch: you look into a dim corridor, not a flat poster; the four lanterns glow, you see each candle
  through the amber glass.
- The wall just above each painting, where its spotlight grazes it, stays yellow, not white.
- Look up at the skylight: its bars throw crisp shadows on the floor, no light leaks between the bars and the frame.
  The Bedroom is lit, not dim and blue.
- Every frame hangs above the wainscot's rail; the frames' carving reads as one piece with the frame.
- The plant leaves, hydrangea petals and lantern panes look solid from every side.
- With textures in: no visible seam where the wall, floor, wainscot, wood or fabric repeat.
- Frame rate stays steady looking down the room.
