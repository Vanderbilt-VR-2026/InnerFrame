# Painted Bedroom: before/after tools (#44)

Two tools so every change to the Painted Bedroom can be judged the same way:

| Tool | What it answers |
|---|---|
| **Render Fixed Views** (Unity menu) | Does it look closer to the painting? Same cameras every time, next to the painting. |
| **`compare_exports.py`** | What did an export change? Lists the objects, materials and textures that differ. |

## Render Fixed Views

`InnerFrameBedroomWeb/views.json` holds fixed cameras: the painter's view (key P) plus, for each
object group (bed, chairs, washstand, pictures, room), the room centre at eye height and a close-up.
**Don't change these cameras**: that would break every before/after comparison.

1. Copy `InnerFrameBedroomWeb` into `Assets/` (or use the project once #28 has moved it there) and
   run *Tools > InnerFrame > Painted Bedroom > Import and Build (no Blender)*.
2. *Tools > InnerFrame > Painted Bedroom > Render Fixed Views*.
3. The folder opens: `FixedViews/<date_time>/` in the project folder (outside `Assets/`, ignored by
   git). For each group, `<group>_sheet.png` shows:

   **painting crop | painter's view, same crop | room centre | close-up**

   plus every view as its own PNG, and `painter_view.png`.

Post-processing is on, so the sheets include the whole-room look (#30). The painter's view uses the
same camera as key P: vertical FOV 76.1°, vertical gate fit, lens shift 0.255 frame heights. The
painting crop is the group's objects as the painter sees them.

**Before/after for a PR**: render once before your change and once after, and put the two sheets
of your group in the PR, one above the other.

From the command line (about 15 s once the project is imported):

```
unity run <project> -- -executeMethod InnerFrame.PaintedBedroom.EditorTools.PaintedBedroomFixedViews.RenderBatch -fixedViewsLabel before
```

## compare_exports.py

Plain Python 3, nothing to install. Before exporting from the generator, keep a copy of the
current data, then compare:

```
cp -R ../InnerFrameBedroomWeb/Data /tmp/data_before
# ...export from the generator...
python3 compare_exports.py /tmp/data_before ../InnerFrameBedroomWeb/Data
```

Example output, after one chair part was changed:

```
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

Use it to check that an export changed only the objects you meant to change (paste the output into
the PR), and that exporting the same source twice gives `identical` (the export must be
deterministic, #31).

## Tested

- Render Fixed Views: Unity 6000.3.23f1, batch mode, on the current data: 16 views and 5 sheets in
  15 s; the painter's view overlays Unity's key P exactly. [V]
- compare_exports.py: same data → `identical`; one chair part moved → exactly that part and its ink
  outline listed. [V]
