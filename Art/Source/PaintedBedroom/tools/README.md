# Painted Bedroom: before/after tools (#44)

Two tools so every change to the Painted Bedroom can be judged the same way:

| Tool | What it answers |
|---|---|
| **Render Fixed Views** (Unity menu) | Does it look closer to the painting? Same cameras every time, next to the painting. |
| **`compare_exports.py`** | What did an export change? Lists the objects, materials and textures that differ. |

## Render Fixed Views

`Assets/InnerFrameBedroomWeb/views.json` holds fixed cameras: the painter's view (key P) plus, for each
object group (bed, chairs, washstand, pictures, room), the room centre at eye height and a close-up.
**Don't change these cameras**: that would break every before/after comparison. They are a frozen
copy: if the generator ever re-fits the painter camera in `room.json`, `views.json` deliberately
doesn't follow, so old and new renders stay comparable.

1. Run *Tools > InnerFrame > Painted Bedroom > Import and Build (no Blender)* if the scene is out
   of date.
2. *Tools > InnerFrame > Painted Bedroom > Render Fixed Views*.
3. The folder opens: `FixedViews/<date_time>/` in the project folder (outside `Assets/`, ignored by
   git). For each group, `<group>_sheet.png` shows:

   **painting crop | painter's view, same crop | room centre | close-up**

   plus every view as its own PNG, and `painter_view.png`.

Post-processing is on, so the sheets include the whole-room look (#30). (The in-game camera has it
off until #30 turns it on for the desktop and XR cameras.) The painter's view matches key P to
within a pixel: vertical FOV 76.12° (P: 76.1°), vertical gate fit, lens shift -0.2554 frame heights
(P: -0.255). The painting crop is the group's objects as the painter sees them; a group that
reaches behind the painter (the room shell) gets the whole frame.

**Before/after for a PR**: render once before your change and once after, and put the two sheets
of your group in the PR, one above the other.

From the command line (about 15 s once the project is imported):

```
unity run <project> -- -executeMethod InnerFrame.PaintedBedroom.EditorTools.PaintedBedroomFixedViews.RenderBatch -fixedViewsLabel before
```

## compare_exports.py

Plain Python 3, nothing to install (git and git-lfs for `--git`). After exporting from Blender,
compare the game's data with what is on `main`:

```
python3 compare_exports.py --git origin/main
```

Or compare two data folders: `python3 compare_exports.py OLD_DATA_DIR NEW_DATA_DIR`.

Example output, after one chair part was changed:

```
scene settings changed (markers, painter camera...): -
objects: 37 -> 37
  added:   -
  removed: -
  moved:   -
  meshes changed: 1 object(s)
    Chair_Front: 2 mesh(es) ['Chair_Front_00', 'Chair_Front_00_ink']
materials: added -, removed -, changed -
textures:  added -, removed -, changed 0 []
different
```

It also compares the scene settings in `room.json` (markers such as the painter's eye and the
spawn point, the painter FOV and lens shift). Exit code 0 when identical, 1 when different.

Use it to check that an export changed only the objects you meant to change (paste the output into
the PR), and that exporting the same source twice gives `identical` (the export must be
deterministic, #31).

The room data lives once, in `Assets/InnerFrameBedroomWeb/Data/`; `--git` reads older revisions
from wherever the data was at the time.

## Tested

- Render Fixed Views [V]: Unity 6000.3.23f1 in batch mode (`unity run`), in a separate worktree of
  this branch with the package copied into `Assets/` and *Import and Build* run first, on the
  current data (10/13): 16 views and 5 sheets, about 15 s for the whole Unity run. The painter
  camera's projection matrix equals key P's (vertical FOV 76.1°, m12 = -0.51), checked by logging
  both in Unity. Not yet run from the Unity menu by a person, nor on another machine. [I]
- compare_exports.py [V]: same data → `identical`; a test export with one part of `Chair_Front`
  moved 3 cm (made with the Blender round trip on `charlyne/blender-roundtrip`, which rebuilds the
  outline) → exactly that part and its ink outline; scripted one-change copies of `Data/` (a
  vertex, an index, an object position, a marker, the painter FOV, a material colour, one texture,
  a uv, a renamed or removed object, a missing `Textures/` folder, renumbered materials) → each
  reported alone with exit code 1; renumbered materials → `identical`, exit code 0.
