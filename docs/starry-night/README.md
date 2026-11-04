# The Starry Night (step 4: the first art pass)

Owner: Cici (Sprint 4). A small hilltop above Saint-Rémy at night. Vincent sits by a rock with a lantern and his
sketchbook; one cypress; a village far below; a huge sky. **The sky is the game:** it starts as a real night and turns
into his painting as the player helps him. Score attention, never speed: no fail state, no timer, and a later step done
early is accepted.

Step 2 made it playable with placeholders; step 4 (2026-11-04) is the first art pass: the gameplay, states, timings and
lines are unchanged; the seat moved, the sky was re-aimed from it, and most placeholders were replaced by name (see
"What is made of what" below). Vincent is still the standing placeholder.

## Open and play

- Scene: `Assets/StarryNight/Scenes/StarryNight.unity`. It holds only the player rig (the Starter Assets
  `XR Origin (XR Rig)`, scaled 0.8 with 2 m rays, as in the Gallery and the Bedroom) and one `SN_Bootstrap`, which
  spawns everything else from `Assets/StarryNight/Prefabs` at runtime.
- After changing `SN_Layout.asset` (every number of the scene: places, sizes, timings), `lines.txt`, or the sources in
  `Art/Source/StarryNight`: **Tools > InnerFrame > Starry Night > Build**. Running it twice changes nothing. An older
  `SN_Layout.asset` (version 0, the blockout's) gets the art pass's values once, each logged `old -> new`; a tuned one
  (version 2) is kept.
- The art comes from scripts that give the same files on every run (Cici's input images and Tripo GLBs live outside the
  repo, in `refs/3-starry-night/art-inputs`):
  - `make_textures.py <inputs> <concept dir>` and `make_path.py <inputs>` (`/usr/bin/python3 -I`, Pillow, numpy);
  - `make_tripo.py -- <inputs>` and `make_props.py` (Blender 5.2.2: `Blender --background --factory-startup --python …`):
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

The UI (an objective hint and Vincent's dialogue box) follows the head lazily with XRI's `LazyFollow`: 1.6 m ahead and
22° below the gaze (`SN_Layout.uiBelowGaze`; at a level gaze the box spans −24° to −18° and the hint's top is at −10°,
under the village), upright, moving only once you look more than 20° away; drawn on top of the world. Text sizes are
named constants in `SN_DialogueBox` (4.0 cm capitals for his line at 1.6 m).

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
| Lantern | base, iron frame, glass (transparent), cap, handle, a wick key that turns as it dims; 628 triangles | `make_props.py` |
| Sketchbook, page, charcoal | the sketchbook open to 120° on one cover, the sketch on its page; a torn page with the sketch; a stick of charcoal by Vincent's seat (all grabbable; each on the rock where its footprint is flat) | `make_props.py`, `sketch-page.png` |
| The sky | the night (`sky-night.png`) round the whole dome, mirrored in yaw, from the start; the painting (`sky-painted.png`) on a panel centred at yaw +28°, 112.5° × 75° (pitch −8° to 67°), seen from the seat | `make_textures.py`, `SN_Sky.shader` |
| The wind's path | Cici's red line along the swirl, 24 points, the 3 at its outer end dropped (inside the panel's soft edge): 21 points from (−14°, 40°) up to 54° and into the swirl's centre at (23°, 31°); the swirl band 8° either side | `make_path.py`, `SN_Layout.guidePath` |
| Stars | the 12 story stars with painted halos (N3's 5° across, 2.5° clear right of the cypress; the others 3.5°); 150 small stars that twinkle (a third, faint, before the lantern) and 25 medium ones whose halos grow as stars are woken and as the sky is painted: one mesh and one draw call each; a painted moon | `SN_Sky`, `make_textures.py` |
| The valley | the painted horizon (`horizon.png`, keyed): 96° wide, its top 4° above the horizontal, the spire at yaw 6° (the village's direction), the ends fading over 8°; the lit windows glow at night, with three soft glow sprites; the same painted hills (`SN_FarHills.png`) round the rest of the horizon; the blockout's window dots are off (`SN_Layout.villageDots`) | `make_textures.py`, `SN_Horizon.shader` |
| Mist | three drifting, near-horizontal noise layers under the hill and five soft puffs that face you (8 transparent renderers) | `SN_Mist.shader`, `SN_MeshGen.MistLayer` |
| Ground | `grass.png` made seamless (offset blend), tiled every 1.6 m; the path a ragged, alpha-clipped warm dirt strip; 80 crossed-card flower clusters (one mesh, one material), denser by the rock, the lantern and the path, none on the path or the seat | `make_textures.py`, `SN_MeshGen` |
| Light | a cool, low blue moonlight and the lantern's warm point light, neither casting shadows (one additional light: within the Mobile quality level's limit) | `SN_Layout` |
| Still placeholder | Vincent (the static model, standing past the rock's left end at his Bedroom scale; `SN_VincentSeat` marks where the seated Vincent goes), the UI sprites, Vincent's lines (macOS `say`, voice Daniel) | |

## Tests

- **Tools > InnerFrame > Starry Night > Run Playthrough Test** (or batch:
  `-executeMethod InnerFrame.StarryNight.EditorTools.SN_PlaythroughTest.RunBatch -snLog <file>`): the whole story from
  Arrive to Done through a scripted input, and the negative checks (a trigger 10° from every star wakes nothing; N3 before
  N1 wakes it but doesn't advance; tracing the wind backwards doesn't complete it; the lantern down before sitting skips
  Sit), each with its positive control; and the letter found during Stars: after L10 the Stars hint is still shown and
  the 25 s repeat still fires.
- `SN_PlayCheck.RunBatch` (batch, without `-quit`): the Play-mode check of the temporary network measure above.

## Requested hardware tests (Quest)

1. Text: is Vincent's line readable without leaning in, at the default distance? Too big or too small?
2. The UI: does it follow comfortably (not too lazy, not jittery)? Does it hide the village or the swirl you are tracing?
3. Aiming: can you wake each named star on the first or second try? Is 4° of aim assist too generous?
4. The wind: is the dotted line visible against the night, and does following it feel like painting?
5. The lantern: grab it by the handle, pull the trigger; does the hill go dark enough for the stars to read?
6. Sitting: does the seat zone (0.6 m) catch you when you sit down on the floor beside the rock?
7. Frame rate through the whole scene, especially with the painted sky, the mist layers and the horizon (33 transparent
   renderers at the most, about 47,000 triangles).
8. The art pass: from the seat, do the cypress, Vincent and the valley sit where the concept (G2) has them? Is N3 easy to
   tell from the cypress? Can you reach the sketchbook (0.96 m from the seat) and the page (1.1 m) without getting up?
