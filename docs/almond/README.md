# Almond Blossom studio (Act 4), step 1: blockout

Serena's scene (owner to confirm with the team). Concept page:
https://claude.ai/artifact/2hC8PdVxAdJ7TZjEDYgm1D

Vincent's studio at Saint-Rémy, February 1890, after *Window in the Studio*. You arrive with the window shut,
open it, take a brush, and follow each bare branch of the almond tree outside: the blossoms open behind your
stroke. When every branch has bloomed, the window frames *Almond Blossom*. No fail state, no timer; one player
is enough (the optional second player is step 4).

This is the **blockout**: boxes for the room and the furniture, a capsule for Vincent, the real painting
outside. The art comes later (see "Next steps").

## How to open it

1. Pull this branch (`serena/almond-studio`) in GitHub Desktop.
2. **First time only:** copy `AS_Almond_Full.jpg` and `AS_Almond_Bare.jpg` (sent separately) into
   `Assets/AlmondStudio/Textures/`. They are images, so they go in Git LFS; GitHub Desktop does that.
3. Open the project in Unity 6000.3.23f1. Unity makes the `.meta` files for the new files.
4. Menu **Tools > InnerFrame > Almond Studio > Build**. It makes `AS_Layout.asset`, the materials and
   `Scenes/AlmondStudio.unity`, and opens the scene. Safe to run again.
5. Press **Play**.

The scene is **not** in the Build Settings' list (a shared setting). For a headset build, add it there and name
that change in the PR.

## Controls

| Headset | Editor (no headset) | What it does |
|---|---|---|
| Grip or ray on a casement | **O** | Opens the window (both casements) |
| Grip on the brush (on the sill, right) | **G** | Takes the brush, or puts it back |
| Trigger, aiming the brush at a branch's glowing dot | Hold **Space**, aiming the view's centre | Paints along the branch |
| | **N** | Blooms the next branch at once (testing) |
| | **R** | Starts over |
| | W A S D, mouse | Walk and look (DesktopTestRig; click to capture the mouse) |

Let go of the brush and it returns to the sill.

## How it works

| File | What it does |
|---|---|
| `Assets/AlmondStudio/Scripts/AS_Bootstrap.cs` | Builds the room from boxes at runtime, the casements, the brush, Vincent's stand-in, the painting, the guides and the shared dialogue box; Editor keys |
| `AS_Game.cs` | The act: Arrive (window shut) → TakeBrush → Paint → Done. Following a branch, the hints and Vincent's lines |
| `AS_Branches.cs` | Loads `Data/AS_Branches.json`; path maths; builds the **reveal map** |
| `AS_Painting.cs` + `Shaders/AS_Reveal.shader` | The painting outside: bare and original versions, blended per pixel |
| `AS_Guides.cs` + `Shaders/AS_Glow.shader` | The faint line and glowing dot on each branch still to paint; the brush's beam |
| `AS_Window.cs` | The two hinged casements |
| `AS_Input.cs` | Triggers and haptics (headset), Space and the view's centre (Editor) |
| `AS_Layout.cs` | Every number: room, window, painting, tolerances, lines and hints |
| `Editor/AS_Setup.cs` | The Build menu |

**The reveal map.** Every pixel of the painting belongs to its nearest branch path and has a place along it
(0 at the base, 1 at the tip). A pixel shows the original once its branch's progress passes that place, so the
blossoms open one after another behind the stroke, and when every branch is done the whole original shows. The
same idea as Cici's swirl mask in The Starry Night. The map is built at load from the paths (about a second;
bake it to a texture later if the load hitch matters on Quest).

**Branch paths.** `Assets/AlmondStudio/Data/AS_Branches.json`: eight paths, base to tip, in painting UV (x right,
y up, 0..1). Edit the numbers and press Play to try a change. At most 8 branches (the shader's array).

**Following a branch.** Aim near a branch's glowing dot (`startTolerance`), hold the trigger, and move along it.
The progress only moves forward, up to `lookAhead` per frame, while the beam stays within `followTolerance` of
the path. Off the path for `loseSeconds` and the branch is let go (its progress stays). Past `finishAt` it
blooms to its tip.

## What is verified

- [I] Not compiled or run yet: written in a workspace without Unity. The first Build and Play may show compile
  errors to fix.
- [V] The reveal itself, previewed in Python on the real images: bare, one branch half done, four branches done,
  all done (the last equals the original).
- [I] Never tested on a Quest.

## Hardware tests (when it runs)

1. Arrive: the window is shut, Vincent's line and the hint "Open the window" show.
2. Grip a casement: both swing into the room.
3. Grip the brush: the hint changes, the guides appear.
4. Trigger on a glowing dot, follow the branch: blossoms open behind the beam; a buzz when it finishes.
5. All branches: the original painting fills the window; Vincent's last line.
6. Frame rate (OVR Metrics, target 72+), and the load hitch when the scene opens.
7. Comfort: is the painting at 1.2 m outside the window convincing, or too flat when you lean out?

## Next steps

1. Step 2: fit the arrival view to *Window in the Studio* (the Bedroom's painter camera); the arched window;
   the room shell in Blender from the floor plan (S10).
2. Step 3, art (`.claude/skills/asset-pipeline/`): the sill objects, table and chairs via Tripo; the canvases
   and the letter as 2D; petals; Vincent v2 (ask Cici) with standing Mixamo clips.
3. Step 4: the optional second player (ask Sonya): share each branch's progress.
4. Fill in the TO FILL parts of the `docs/ASSETS.md` rows (the museum image's URL and terms; the ChatGPT account).
