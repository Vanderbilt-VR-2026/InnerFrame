# Almond Blossom studio (Act 4), step 1: blockout

Serena's scene (owner to confirm with the team). Concept page:
https://claude.ai/artifact/2hC8PdVxAdJ7TZjEDYgm1D

Vincent's studio at Saint-Rémy, February 1890, after *Window in the Studio*. You arrive with the window shut,
open it, take a brush, and follow each bare branch of the almond tree outside with its beam. When every branch
is painted, the tree has bloomed. No fail state, no timer; one player is enough (the optional second player is
step 4).

This is the **blockout**: boxes for the room and the furniture, a capsule for Vincent. Outside, a **3D almond
tree fitted to *Almond Blossom*** under a painted turquoise sky. The blossoms and their opening come next
(step 1b), then the art (see "Next steps").

## How to open it

1. Pull this branch (`serena/almond-studio`) in GitHub Desktop.
2. Open the project in Unity 6000.3.23f1. Unity makes the `.meta` files for the new files.
3. Menu **Tools > InnerFrame > Almond Studio > Build**. It makes `AS_Layout.asset`, the materials and
   `Scenes/AlmondStudio.unity`, and opens the scene. Safe to run again.
4. Press **Play**.

The scene is **not** in the Build Settings' list (a shared setting). For a headset build, add it there and name
that change in the PR.

## Controls

| Headset | Editor (no headset) | What it does |
|---|---|---|
| Grip or ray on a casement | **O** | Opens the window (both casements) |
| Grip on the brush (on the sill, right) | **G** | Takes the brush, or puts it back |
| Trigger, aiming the brush at a branch's glowing dot | Hold **Space**, aiming the view's centre | Paints along the branch |
| | **N** | Paints the next branch at once (testing) |
| | **R** | Starts over |
| | W A S D, mouse | Walk and look (DesktopTestRig; click to capture the mouse) |

Let go of the brush and it returns to the sill.

## How it works

| File | What it does |
|---|---|
| `Assets/AlmondStudio/Scripts/AS_Bootstrap.cs` | Builds the room from boxes at runtime, the casements, the brush, Vincent's stand-in, the sky, the ground, the tree, the guides and the shared dialogue box; Editor keys |
| `AS_Game.cs` | The act: Arrive (window shut) → TakeBrush → Paint → Done. Following a branch, the hints and Vincent's lines |
| `AS_Tree.cs` + `Shaders/AS_Bark.shader`, `AS_Ink.shader` | The 3D almond tree: tapered branch tubes with twigs, painted bark, a dark outline shell |
| `AS_Branches.cs` | Loads `Data/AS_Branches.json`; following a branch by the beam's angle |
| `Shaders/AS_Sky.shader` | The painted turquoise sky on the inside of a big sphere |
| `AS_Guides.cs` + `Shaders/AS_Glow.shader` | The faint line and glowing dot on each branch still to paint; the brush's beam |
| `AS_Window.cs` | The two hinged casements |
| `AS_Input.cs` | Triggers and haptics (headset), Space and the view's centre (Editor) |
| `AS_Layout.cs` | Every number: room, window, the tree's fitting, tolerances, lines and hints |
| `Editor/AS_Setup.cs` | The Build menu |

**Fitted to the painting.** The tree is built the way the Bedroom is fitted: from `paintEye` (standing about 1 m
in front of the window) every branch point lies on the ray through that point of *Almond Blossom*, as if the
painting hung on a reference plane just outside the window. Each branch sits at its own depth along those rays
(`depthBase`, `depthTip` in the JSON), so from the window the branches fall where the painting has them, and
leaning out or stepping aside you see a real tree, near limbs and far twigs. Trunks (`toGround`) continue down to
the ground.

**Branch paths.** `Assets/AlmondStudio/Data/AS_Branches.json`: eight paintable paths and three trunk paths
(`decor`), base to tip, in painting UV (x right, y up, 0..1), with a base radius (`width`, in painting widths)
and depths. Edit the numbers and press Play to try a change. At most 8 paintable branches.

**Following a branch.** Aim near a branch's glowing dot (`startToleranceDeg`), hold the trigger, and move along it.
Measured as an angle from the beam, so far branches are as easy as near ones. The progress only moves forward, up
to `lookAhead` per frame, while the beam stays within `followToleranceDeg`. Off the branch for `loseSeconds` and it
is let go (its progress stays). Past `finishAt` it is done. The painted part of the bark warms a little (the
blossoms come in step 1b).

## What is verified

- [I] Not compiled or run yet: written in a workspace without Unity. The first Build and Play may show compile
  errors to fix.
- [V] The tree's fitting, previewed in Python (the same maths, centre lines only): from the paint eye the branches
  fall on the painting's; from the side they spread 1.5 to 4 m beyond the wall.
- [I] Never tested on a Quest.

## Hardware tests (when it runs)

1. Arrive: the window is shut, Vincent's line and the hint "Open the window" show.
2. Grip a casement: both swing into the room.
3. Grip the brush: the hint changes, the guides appear.
4. Trigger on a glowing dot, follow the branch: blossoms open behind the beam; a buzz when it finishes.
5. All branches: the original painting fills the window; Vincent's last line.
6. Frame rate (OVR Metrics, target 72+), and the load hitch when the scene opens.
7. The tree from the window, and leaning out: does it read as *Almond Blossom*, and as a real tree?

## Next steps

1. Step 2: fit the arrival view to *Window in the Studio* (the Bedroom's painter camera); the arched window;
   the room shell in Blender from the floor plan (S10).
2. Step 3, art (`.claude/skills/asset-pipeline/`): the sill objects, table and chairs via Tripo; the canvases
   and the letter as 2D; Vincent v2 (ask Cici) with standing Mixamo clips.
3. Step 4: the optional second player (ask Sonya): share each branch's progress.
4. Step 1b (next): 3D blossoms along the branches that open behind the stroke, petals drifting.
