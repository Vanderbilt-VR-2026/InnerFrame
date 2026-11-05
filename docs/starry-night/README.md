# The Starry Night (step 4b: the look pass)

Owner: Cici (Sprint 4). A small hilltop above Saint-Rémy at night. Vincent sits by a rock with a lantern and his
sketchbook; one cypress; a village far below; a huge sky. **The sky is the game:** it starts as a real night and turns
into his painting as the player helps him. Score attention, never speed: no fail state, no timer, and a later step done
early is accepted.

Step 2 made it playable with placeholders; step 4 (2026-11-04) is the first art pass: the gameplay, states, timings and
lines are unchanged; the seat moved, the sky was re-aimed from it, and most placeholders were replaced by name (see
"What is made of what" below). Vincent is still the standing placeholder.

Step 4b (2026-11-04, after #135) changed two things, again with the gameplay, states, timings and lines unchanged:
- **The shared dialogue box.** Vincent's words and the hint now use `VG_DialogueUI`, as in every scene (see Controls).
- **The look pass**, closer to the concepts S01, G1 and G2:
  - Cici's Tripo bushes, 50 + 25 copies, so the hilltop reads lush;
  - her Tripo cloud, 25 copies, as a bank round the rim over a fuller mist;
  - a warmer, stronger lantern, a cooler and brighter moon, more ambient light, and lighter, more saturated grass,
    bushes and path (material tints, not repainted textures);
  - one far horizon line instead of the mirrored hills;
  - the page within reach of the seat;
  - the beam drawn under the UI;
  - an optional colour grading switch.

Checks (2026-11-04, before step 3): one command runs every check (see "Checks"), and a new layout check measures the
budget, the bushes, the clouds, the props and the far ridge. It found two of the look pass's numbers wrong, both fixed:
the most transparent renderers was 36, not 35 (the guide and the beam during the wind; the lantern's two flame quads are
now one mesh), and the far ridge was only held under the painted strip over its faded ends, measured from the hill's
centre (now from the seat eye, and 12° past the ends too).

## Open and play

- Scene: `Assets/StarryNight/Scenes/StarryNight.unity`. It holds only the player rig (the Starter Assets
  `XR Origin (XR Rig)`, scaled 0.8 with 2 m rays, as in the Gallery and the Bedroom) and one `SN_Bootstrap`, which
  spawns everything else from `Assets/StarryNight/Prefabs` at runtime.
- After changing `SN_Layout.asset` (every number of the scene: places, sizes, timings), `lines.txt`, or the sources in
  `Art/Source/StarryNight`: **Tools > InnerFrame > Starry Night > Build**. Running it twice changes nothing. An older
  `SN_Layout.asset` is brought up once, step by step, each value logged `old -> new` (version 2: the art pass's values;
  version 3: `uiBelowGaze` moved into the dialogue box's settings; version 4: the look pass); a current one is kept as
  tuned.
- The art comes from scripts that give the same files on every run (Cici's input images and Tripo GLBs live outside the
  repo, in `refs/3-starry-night/art-inputs`):
  - `make_textures.py <inputs> <concept dir>` and `make_path.py <inputs>` (`/usr/bin/python3 -I`, Pillow, numpy);
  - `make_tripo.py -- <inputs>`, `make_tripo_look.py -- <inputs>` and `make_props.py` (Blender 5.2.2:
    `Blender --background --factory-startup --python …`):
    one FBX per object in `Assets/StarryNight/Models` (the team's export settings; the exporter's timestamp and ids are
    pinned in `sn_blender.py`, so the FBX bytes repeat; the `.blend` files kept beside the scripts do not).
- The scene is not in the build list (`EditorBuildSettings` unchanged). To try it on a Quest, add it to your build
  profile locally, or build it alone (Android build profile, not a Meta Quest one).

## The story

| State | What the player does | Vincent | Hint |
|---|---|---|---|
| Arrive | walks up the path; within 3 m of Vincent | L01 "Ah, a visitor…" | Walk up the path to Vincent |
| Sit | sits down: the head enters the seat behind the rock's right end, Vincent's seat 42° to the left and 1.2 m away | L02 "Too much light…" | Sit down beside Vincent |
| Lantern | grabs the lantern (grip) and pulls the trigger: light and flame ease down over 1.5 s, the other stars fade in | L03, then L04 names N1 | Grab the lantern and pull the trigger to turn it down |
| Stars | points at a star and pulls the trigger (4° aim assist): it blooms. Any star can be woken; N1, N2, N3 move the story only in order | L05 names N2, L06 names N3 | Point at the star he names and pull the trigger |
| Wind | after N3 the sky starts to paint; a dotted curve appears along the painted swirl (Cici's red line, 21 points): hold the trigger and follow it, point by point (6°) | L07, then L08 at 100 % | Hold the trigger and follow the dotted line |
| Dawn | 10 s after the wind: the sky pales over 20 s, the stars fade, then a page appears on the rock | L09 | Wait for dawn, then Take the page |
| Done | grabs the page; the morning star fades last | | The night is painted |
| Sketchbook (any time after the lantern) | holds his sketchbook up against the view toward the valley for 1.5 s | L10 | (unchanged: the current state's hint stays, and its repeat still fires) |

- Early steps are accepted: turning the lantern down before sitting skips Sit; waking N3 first wakes it but N1 must still
  come first for the story.
- Nothing moves for 25 s in Stars or Wind: he says the current instruction again.
- His lines play one after another; a newer story line drops older ones not yet started, so he never asks for something
  already done (L08–L10 are never dropped).
- Score (logged at Done): stars woken (of 12), named stars in order (of 3), wind traced, letter found.

## Controls

**Quest (either hand):** grip grabs (lantern, sketchbook, page); the trigger acts (XRI's Activate, unused anywhere else
in our gameplay): turn the lantern down while holding it, wake a star, hold to trace the wind. A thin beam shows where you
point while there are stars to wake or wind to trace (XRI's own rays stop at 2 m). Thumbsticks: the usual move, turn
and teleport (the hilltop is a teleport area out to 5.5 m; low invisible walls stop you walking off).

**Editor, no headset** (the team's `DesktopTestRig` swaps in, as in the Gallery):

| Key | Does |
|---|---|
| W A S D, mouse (click to capture, Esc to free) | walk and look (DesktopTestRig) |
| mouse | aims (the view's centre while the mouse is captured) |
| Space | the trigger |
| G | grab, or release, the nearest grabbable within 1.5 m (held in front of the view) |
| 1 / 2 | jump to the player start / the player seat |

Vincent's words and the hint use the shared dialogue box, the same as in every scene: `VG_DialogueUI`
(`Assets/VanGogh/Prefabs/VG_DialogueUI.prefab`, spawned by `SN_Bootstrap` like everything else) with this scene's
settings, `VG_Dialogue_StarryNight.asset`: 1.6 m ahead, its top edge 22° below the gaze (at a level gaze the box spans
−29° to −22° and the hint's plate −19° to −14°, under the village's lowest windows; looking 25° up the hint's top is at
10°, under the swirl), 33° wide, 4.0 cm capitals for his line, the hint ("A Night with Vincent" and the state's line) on
its plate, as in the Bedroom. It moves only once you look more than 20° away and is drawn over the world. `SN_Voice` keeps
the line queue (a newer story line drops older ones not yet started) and calls `Say("Vincent", text, clip)` and `Clear`;
`SN_Game` sets the hint (`SetHint`) and steps the UI each frame. The controller beam draws just before the UI (render
queue 2999 against its 3000), so the box and the hint always draw over it. To change where the box sits, edit the
settings asset (the Bedroom has its own).

## The [SN] log (for testers)

Every state change logs `[SN] <state> t=<seconds since the start>` (and raises `SN_Game.StateEntered`, for the portrait
deck later); the score at Done logs `[SN] score stars=…/12 named=…/3 wind=… letter=…`.

## TEMPORARY: solo, the network session is left at Start

`NetworkSession` (`Assets/Script/Network/NetworkSession.cs`, Sonya's file) joins a Photon Fusion room in every scene with
an XR Origin, and has no opt-out yet. This scene is solo for now, so **as a temporary measure for this blockout**
`SN_Bootstrap.LeaveNetworkSession()` stops the session's join and makes it leave at Start (still there after the art pass; log:
`[SN] Solo for now: left the network session …`). The session object stays alive, so the next scene joins its own room
as usual. **To be replaced by an opt-out in NetworkSession** (Sonya's call); nothing in `Assets/Script` was changed.
Checked in Play mode (Editor, no headset): no `[NetworkSession] Joined InnerFrame_StarryNight`, no NetworkRunner or
NetworkPlayer in the scene, the rig not moved (`Editor/Tests/SN_PlayCheck.cs`).

## What is made of what

| Thing | Now | Made by |
|---|---|---|
| The seat | `SN_PlayerSeat` (0.45, 0, −0.9): Vincent's seat 41.6° to the left, 1.20 m away; the cypress 35.5° left, 4.05 m; the lantern on the grass 0.53 m from the seat, valley side | `SN_Layout` |
| Cypress | Tripo model, 9,401 triangles, 7 m tall, 1.6 m at its widest (no decimation) | `make_tripo.py` |
| Rock | Tripo model scaled ×1.457 so its top's area-weighted mean is 0.26 m (1.46 × 0.82 × 0.31 m), with a MeshCollider of its own shape | `make_tripo.py` |
| Lantern | base, iron frame, glass (transparent), cap, handle, a wick key that turns as it dims; 628 triangles; the flame two crossed quads in one mesh (`SN_FlameCross`) | `make_props.py`, `SN_MeshGen` |
| Sketchbook, page, charcoal | the sketchbook open to 120° on one cover, the sketch on its page; a torn page with the sketch; a stick of charcoal by Vincent's seat (all grabbable; each on the rock where its footprint is flat). The page lies 0.73 m from the seat (the look pass); the sketchbook stays 0.96 m away: at its yaw no spot on the rock within 0.75 m is flat enough | `make_props.py`, `sketch-page.png` |
| The sky | the night (`sky-night.png`) round the whole dome, mirrored in yaw, from the start; the painting (`sky-painted.png`) on a panel centred at yaw +28°, 112.5° × 75° (pitch −8° to 67°), seen from the seat | `make_textures.py`, `SN_Sky.shader` |
| The wind's path | Cici's red line along the swirl, 24 points, the 3 at its outer end dropped (inside the panel's soft edge): 21 points from (−14°, 40°) up to 54° and into the swirl's centre at (23°, 31°); the swirl band 8° either side | `make_path.py`, `SN_Layout.guidePath` |
| Stars | the 12 story stars with painted halos (N3's 5° across, 2.5° clear right of the cypress; the others 3.5°); 150 small stars that twinkle (a third, faint, before the lantern) and 25 medium ones whose halos grow as stars are woken and as the sky is painted: one mesh and one draw call each; a painted moon | `SN_Sky`, `make_textures.py` |
| The valley | the painted horizon (`horizon.png`, keyed): 96° wide, its top 4° above the horizontal, the spire at yaw 6° (the village's direction), the ends fading over 8°; the lit windows glow at night, with three soft glow sprites; round the rest of the horizon one generated, continuous ridge (`SN_MeshGen.HillRing`: a seeded sum of sines, nothing repeated), painted with the strip's own hills (`SN_FarHills.png`, seamless across), sunk out of sight behind the strip; seen from the seat eye it stays 0.6° under the strip's own ridge (`horizon_ridge.json`) over the strip's faded ends and 12° past them, then rises back to its own line over the next 12°; the blockout's window dots are off (`SN_Layout.villageDots`) | `make_textures.py`, `SN_Horizon.shader` |
| Mist | four drifting, near-horizontal noise layers under the hill (the upper ones end nearer, so from the seat they stay under the village) and six soft puffs that face you (10 transparent renderers) | `SN_Mist.shader`, `SN_MeshGen.MistLayer` |
| Clouds | Cici's Tripo cloud, 25 copies in 5 clusters round the rim, 6–13 m from the centre, scale 0.8–2.5, turned broadside to the hill, half mirrored; seen from the seat every top stays under −12.5° (under the strip's own painted mist); opaque URP Lit with its normals bent up (soft light from above); one combined mesh, one draw | `make_tripo_look.py`, `SN_MeshGen.Clouds`, `SN_Scatter` |
| Bushes | Cici's two Tripo bushes, 50 + 25 copies (random yaw, scale 0.7–1.3, half mirrored), denser by the rock, the lantern, the path's edges, the cypress's foot and the rim; clear of the path (0.4 m from its centre line), the seat, the rock, the lantern, Vincent, the start and the walls' line; nothing taller than 0.6 m within 1.5 m of the seat; each kind one combined mesh, one draw | `make_tripo_look.py`, `SN_MeshGen.Bushes`, `SN_Scatter` |
| Ground | `grass.png` made seamless (offset blend), tiled every 1.6 m; the path a ragged, alpha-clipped warm dirt strip; 80 crossed-card flower clusters (one mesh, one material), denser by the rock, the lantern and the path, none on the path, the seat or under a bush; grass and path tinted lighter and more saturated (`SN_Layout.grassTint`, `pathTint`) | `make_textures.py`, `SN_MeshGen` |
| Light | a cool blue moonlight (0.55) and the lantern's warm point light (6, range 6.5 m), neither casting shadows (one additional light: within the Mobile quality level's limit); a flat ambient #283355 | `SN_Layout` |
| Colour grading (off by default) | `SN_Layout.colourGrading`: on, `SN_Bootstrap` adds a global Volume with `SN_Grading.asset` (Color Adjustments: post exposure 0.35, contrast 14, saturation 20, a warm filter) and turns post-processing on for the scene's camera at runtime (the rig prefab is not changed). Off by default until its cost is measured on a Quest | `SN_Setup` (the profile), `SN_Bootstrap` |
| Dialogue box and hint | the shared `VG_DialogueUI` with `VG_Dialogue_StarryNight` (above) | `Assets/VanGogh` (VanGoghAssetSetup) |
| Still placeholder | Vincent (the static model, standing past the rock's left end at his Bedroom scale; `SN_VincentSeat` marks where the seated Vincent goes), Vincent's lines (macOS `say`, voice Daniel) | |

## Checks

One command runs every check: the playthrough test, the dialogue box check, the layout check, then the Play-mode check.
It prints one line per check (`[SN Checks] <check>: PASS|FAIL (n passed, m failed); log …`) and a last `[SN Checks] PASS`
or `FAIL` line, and writes them to `SN_checks_summary.txt` beside the logs.

1. **In the Editor:** Tools > InnerFrame > Starry Night > **Run all checks** (it saves nothing; it asks to save your open
   scene first, enters Play mode for the last check, then shows the summary).
2. **From a terminal** (macOS; on Windows, the Unity.exe of the same version), **without `-quit`**, so the Play-mode check
   can run; exit code 0 only if every check passed:

   ```
   /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . \
     -executeMethod InnerFrame.StarryNight.SN_Checks.RunAll -snLogDir Logs/SN_checks -logFile Logs/SN_checks.log
   ```

   With `-quit` the edit-mode checks run and the Play-mode check is skipped (the summary says so).

The layout check (`Editor/Tests/SN_LayoutCheck.cs`, also alone: Tools > InnerFrame > Starry Night > Run Layout Check, or
`-executeMethod InnerFrame.StarryNight.EditorTools.SN_LayoutCheck.RunBatch -snLog <file>`) measures what was built, not
the code that placed it; each line gives the measured value and the limit, and a FAIL names the object:
1. triangles in `SN_World` at most 250,000, at the start and at the most (sampled through a scripted playthrough);
2. transparent renderers at most 35 at the most, the mist's at most 10; renderers × materials reported;
3. the bushes, from their vertices above the ground: 0.4 m from the path's centre line, 0.6 m from the player's seat,
   0.2 m from the rock's footprint, 0.3 m from the lantern; nothing taller than 0.6 m within 1.5 m of the seat; no copy
   across the walls' line (`SN_Layout.bushClear*`, `openView*`);
4. the clouds: every vertex 6 m or more from the centre; seen from the seat eye, the highest at −12.5° or lower;
5. the props after 5 s of physics (in a preview scene with its own physics, so the project's physics settings are not
   touched): the page within 0.75 m of the seat, asleep on `SN_Rock`, tilted 10° or less; the sketchbook's rest reported;
6. the far ridge 0.6° under the painted strip's ridge over the strip's faded ends and 12° past them, seen from the seat eye.

Positive controls (each must FAIL, on a copy): a bush moved onto the path, a cloud raised 3 m, the page dropped 0.9 m from
the seat.

## Headset checklist (team playtest, Thu 11/05)

Install the APK (ask Cici), sit on the floor beside the rock when the hint says so, and note for each: fine, or what's wrong.
1. **Text and the box:** can you read Vincent's line without leaning in? The box sits 22° below your gaze (measured to its
   top edge), 1.6 m away, and moves only once you look more than 20° away: does it follow comfortably, and does it ever
   cover the village or the swirl you're tracing?
2. **Aiming:** the aim assist is 4°. Can you wake each star he names on the first or second try? Too generous?
3. **Reach from the seat:** can you take the page (0.73 m from the seat) and the sketchbook (0.96 m) without getting up?
4. **The halos:** do the woken stars' halos read as painted, and is the morning star (N3) easy to tell from the cypress?
5. **The mist and the clouds:** does the hill float in a sea of mist and cloud round the rim? Anything flat, doubled or
   popping at the far ridge?
6. **Frame rate:** watch it through the whole scene with the colour grading off (the default), then once more with
   `SN_Layout.colourGrading` on (rebuild the APK): 160,500 triangles and 35 transparent renderers at the most.

## Tests

- **Tools > InnerFrame > Starry Night > Run Playthrough Test** (or batch:
  `-executeMethod InnerFrame.StarryNight.EditorTools.SN_PlaythroughTest.RunBatch -snLog <file>`): the whole story from
  Arrive to Done through a scripted input, and the negative checks (a trigger 10° from every star wakes nothing; N3 before
  N1 wakes it but doesn't advance; tracing the wind backwards doesn't complete it; the lantern down before sitting skips
  Sit), each with its positive control; and the letter found during Stars: after L10 the Stars hint is still shown and
  the 25 s repeat still fires.
- `SN_UICheck.RunBatch -snLog <file>` (batch): the dialogue box in this scene: every line's pages fit in three lines at
  full size; where the box and the hint sit from the seat; a 15° head turn leaves it, a 30° turn catches up in under
  0.6 s (72 Hz); the beam's render queue is below the UI's.
- `SN_PlayCheck.RunBatch` (batch, without `-quit`): the Play-mode check of the temporary network measure above, and that
  the dialogue UI shows the first hint at the settings' distance from the camera.

## Requested hardware tests (Quest)

1. Text: is Vincent's line readable without leaning in, at the default distance? Too big or too small?
2. The UI: does it follow comfortably (not too lazy, not jittery)? Does it hide the village or the swirl you are tracing?
3. Aiming: can you wake each named star on the first or second try? Is 4° of aim assist too generous?
4. The wind: is the dotted line visible against the night, and does following it feel like painting?
5. The lantern: grab it by the handle, pull the trigger; does the hill go dark enough for the stars to read?
6. Sitting: does the seat zone (0.6 m) catch you when you sit down on the floor beside the rock?
7. Frame rate through the whole scene, especially with the painted sky, the mist layers, the clouds and the bushes
   (35 transparent renderers at the most, about 160,500 triangles; then once more with `SN_Layout.colourGrading` on).
8. The art pass: from the seat, do the cypress, Vincent and the valley sit where the concept (G2) has them? Is N3 easy to
   tell from the cypress? Can you reach the page (0.73 m from the seat) and the sketchbook (0.96 m) without getting up?
9. The look pass: does the hilltop read lush and the hill float in a sea of cloud? Do the bushes near the seat stay out
   of the way? Does the far ridge read as one line all round, with no doubled hills where the painted strip ends?
