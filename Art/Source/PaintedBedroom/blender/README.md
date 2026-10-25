# Painted Bedroom in Blender

**In use since the freeze (#51, 10/14).** The generator (`../web/`) was exported one last time in #52;
from then on the room is refined here, object by object, and `Data/` is written back from this file.
Don't re-export from the generator: it would overwrite the work in the `.blend`.

The shared source for refining the Painted Bedroom (#44, part of #31). You change objects in
`painted_bedroom.blend`, export, and Unity imports the result with Serena's importer, unchanged.

```
InnerFrameBedroomWeb/Data  ──import──▶  painted_bedroom.blend  ──export──▶  InnerFrameBedroomWeb/Data  ──▶  Unity
(room.json, room.bytes,                 (you edit here)                     (same format)                    Import and Build
 Textures/tex_*.png)                                                                                          (no Blender)
```

Why not FBX: the Builder's *Build Scene from Blender FBX* path drops glass, per-material glow and
unlit settings. Writing Serena's own data format back keeps every material setting and needs no
change to the Unity scripts.

## What's here

| File | What it is |
|---|---|
| `painted_bedroom.blend` | The room, 37 objects. **The file you edit.** In Git LFS. |
| `room_io.py` | Data ⇄ `.blend`: `import`, `export`, `check` |
| `views.json` | Fixed cameras for every before/after, in Unity coordinates |
| `render_views.py` | Renders the fixed views and a comparison sheet per object group |

Needs Blender 5.2 (tested with 5.2.2). Below, `blender` means
`/Applications/Blender.app/Contents/MacOS/Blender` on a Mac. Run the commands from this folder.

## Change an object (the daily loop)

1. **Open** `painted_bedroom.blend`. Switch the viewport to Material Preview (`Z`) to see the
   paint; numpad `0` is the painter's view (key P in Unity).
2. **Edit** the parts of your own objects (see *Who owns what*). Each object is an Empty, named
   exactly as in Unity (`Chair_Front`), with one mesh per part under it (`Chair_Front_00`, ...).
   You can move vertices, add or delete faces, add modifiers (they are applied on export), move
   or rotate a part, swap a texture. Textures can also be edited in place in `Data/Textures/`
   (the `.blend` references them by file name).
   - To redraw a part's outline thinner or thicker, set its `ink_width` custom property and set
     `ink_rebuild` to true; otherwise an unchanged part keeps its original outline.
   - An outline's material is whatever its `_ink` object carries: give an object its own ink
     material (the bed uses `Ink_Bed`, warm brown, 65% opaque) for a softer edge.
   - The stroke height maps (`tex_*_height.png`, `height` in `room.json`) are carried through by
     name; the Unity importer ignores them unless `Relief` is on.
3. **Save**, then **export**:
   ```
   blender -b painted_bedroom.blend -P room_io.py -- export
   ```
   It rewrites `../InnerFrameBedroomWeb/Data/room.json` and `room.bytes` (and copies any new
   texture into `Data/Textures/`). The last line lists the parts that changed; their ink outlines
   were rebuilt.
4. **Look at it in Unity**: copy `InnerFrameBedroomWeb` into `Assets/` (don't commit that copy),
   then *Tools > InnerFrame > Painted Bedroom > Import and Build (no Blender)*, Play, press P.
5. **Before/after**: `render_views.py` (below) from the same cameras, next to the painting.
6. **Commit together**: the `.blend` and the `Data/` files it exported, in one commit, staged by
   name. Never commit one without the other.

To check that nothing changed by accident (for example after only looking around):

```
blender -b painted_bedroom.blend -P room_io.py -- check
```

On an untouched file every line says `True`, including `room.json byte-identical` and
`room.bytes byte-identical`.

## Rules

- **Never rename an object Empty** (`Bed`, `Chair_Front`, ...). The Unity builder and every runtime
  script find objects by name. Part names (`Chair_Front_03`) may change, but keep them unique.
- **One owner per object.** Edit only your objects; ask the owner first otherwise. Two people
  saving the same `.blend` at once conflict (it's a binary file): say in Slack before you start a
  session and pull before you open it.
- **Don't edit the `Ink` collection.** When a part changes, its outline is rebuilt on export: the
  part pushed out along its smoothed normals by the part's `ink_width` (a custom property), faces
  flipped. Unchanged parts keep their original outline. To make an outline thicker or thinner,
  change `ink_width` on the part.
- **Material settings Unity uses** (colour, glow, unlit, opacity, double-sided) are the `if_*`
  custom properties on each material, not the Blender shader nodes (those only make the viewport
  look right). The texture is whatever image the material's Image Texture node uses.
- **Don't touch the `Markers` collection** unless the team agrees: it holds the painter's eye, its
  target and the spawn point.

## Who owns what

Decided at Checkpoint 1 (Tue 10/13). Proposed:

| Owner | Objects |
|---|---|
| Serena | Bed, Pillow_Back, Pillow_Front, Blanket (#32); Washstand set (#34) |
| Charlyne | Chair_Front, Chair_Back (#33); Mirror, Picture_* (#35); Room_*, Window, Window_View, Door_Left, Door_Right, Door_Frames (#36) |
| (unassigned) | Towel, PegRail, Smock, Clothes_Dark, Cloth_Pale, Hat |

## Fixed views

```
blender -b painted_bedroom.blend -P render_views.py -- --out ../reference
```

For each group (bed, chairs, washstand, pictures, room) it renders three cameras from
`views.json`: the painter's view, the room centre at eye height, and a close-up. It also writes
`<group>_sheet.png`: painting crop | painter's view, same crop | room centre | close-up. The crop is
the group's bounding box seen from the painter's eye. `--groups chairs,bed` limits it to some
groups; `--regen` rebuilds `views.json` from the room (don't, once people use the views: that's
what keeps before and after comparable).

The painter camera matches Unity's key P exactly: vertical FOV 76.1°, vertical gate fit, lens shift
0.255 frame heights. Unity's projection matrix for P and Blender's are the same, and the renders
overlay with no offset. The views are in Unity coordinates, so a Unity script can use the same
cameras for the final screenshots.

## Notes

- **Coordinates.** Unity (x, y, z) is Blender (x, z, y): Blender's front view (along +Y) is Unity
  looking along +Z. The mirror flips triangle winding, so faces are reversed on the way in and out.
- **Untouched parts export bit for bit.** Each part keeps its original Unity vertex data as point
  attributes (`if_co`, `if_normal`, `if_uv`). A part counts as touched once a vertex moves, a face
  changes, it gets a modifier, or it is moved; from then on its normals come from Blender.
- **Lighting differs from Unity.** Judge colour and brightness in Unity. Blender is for shape,
  placement and textures.
- **Reset**: `blender -b -P room_io.py -- import` rebuilds `painted_bedroom.blend` from
  `../InnerFrameBedroomWeb/Data`, discarding edits that weren't exported.
- **Tested** [V]: an untouched export is byte-identical to Serena's data; moving one part of
  Chair_Front changed only that part and its outline; the exported data imports in Unity
  6000.3.23f1 without errors (37 objects, 76 materials, 451 renderers).
  **Not tested yet** [I]: adding a new object or part, a new texture, and the outline rebuilt for
  `Blanket_00` (its original outline covers only one side of the cloth).
