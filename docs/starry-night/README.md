# The Starry Night (step 3: Vincent v2)

Owner: Cici (Sprint 4). A small hilltop above Saint-Rémy at night. Vincent sits by a rock with a lantern and his
sketchbook; one cypress; a village far below; a huge sky. **The sky is the game:** it starts as a real night and turns
into his painting as the player helps him. Score attention, never speed: no fail state, no timer, and a later step done
early is accepted.

Step 2 made it playable with placeholders; step 4 (2026-11-04) is the first art pass: the gameplay, states, timings and
lines are unchanged; the seat moved, the sky was re-aimed from it, and most placeholders were replaced by name (see
"What is made of what" below).

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

Step 3 (2026-11-04, after the checks): **Vincent v2**, Mixamo's rig of Cici's Tripo model, sits on the rock, talks
while his words are in the box, looks at you, at the star he names, at the wind's next dot and at the morning star,
points his charcoal at each named star, and stands up at dawn (see "Vincent"). The rock is raised to his clip's seat;
the page and the sketchbook moved round him; the charcoal is in his hand. The gameplay, states, timings and lines are
unchanged: the playthrough's log is byte-identical.

The lantern (2026-11-09, after the 11/05 playtest): **it hangs on a post beside the seat** and is turned down by
**pointing at it (or touching it) and pulling the trigger**, so the whole scene uses one button; grabbing it is
optional, and let go it glides back to its hook. It pulses until it's turned down; a ray or a hand on it brightens it
and gives one buzz. A blanket marks the seat. The hint takes you through one step at a time (2026-11-10): its title is
the step you are on ("Walk up to Vincent", "Sit beside Vincent", "Dim the lantern", "Wake the stars", "Trace the wind",
"Dawn", then "Take his page" once the page is there, and at the end the scene's name again), its line says how in one
short line that fits the box, and a button comes with its finger ("the trigger (index finger)"). Two museum-style cards
tried on 2026-11-09 were taken out: they felt staged and stayed up the whole scene. Three fixes for the playtest's
camera and movement glitches, each proven by a check with a control (see "Checks"): the props now start where
`SN_Layout` puts them (the lantern and the sketchbook had started inside the rock); a held prop no longer floats you
over a slope and drops you; and the rig no longer steps up onto the lantern or the rock (a keep-off rim round the rock).
Vincent's lines and the sky, the wind and dawn are unchanged.

In the Gallery (2026-11-05): **The Starry Night hangs on the Gallery's east wall** and is entered the way the Bedroom is,
from its pedestal; the player menu brings you back, and at Done it opens by itself (see "In the Gallery"). The scene is in
the build list now, after the existing scenes; the Gallery still starts the game.

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
- The scene is in the build list (`EditorBuildSettings`, enabled, after the existing scenes; the Gallery still starts the
  game), because the Gallery's way in loads it by name. Build with the Android build profile, not a Meta Quest one.

## In the Gallery: the way in and the way back

- **Where:** the east wall's north half (spot A of the 11/04 proposal): the painting's centre at (−4.9, 2.0, −3.6),
  facing into the room, The Bedroom's size (3.0 m wide, 2.376 m tall at the image's own ratio, bottom edge 0.81 m up) in
  The Bedroom's frame, The Bedroom's pedestal on the viewer's right at (−3.5, 0, −1.57). Read from the entrance: the
  portrait, The Bedroom, its pedestal, then The Starry Night on the right-hand wall.
- **The way in:** The Bedroom's own mechanism. The invisible button on the pedestal (a 0.4 m box) has an XR Simple
  Interactable whose Select Entered calls `SceneChangeButton.Activate` (target `StarryNight`, 1 s): point at the pedestal
  (or touch it) and press **grip**; in the Editor without a headset, put the dot on the pedestal and press **E**. One
  second to black, then The Starry Night starts at the path's foot, as when opened directly. Nothing in
  `SceneChangeButton`, `SceneFader` or the menu changed.
- **The way back:** any time, the player menu (left Menu button, or M) → **Back to Gallery**, as from the Bedroom. At
  Done ("The night is painted"), `SN_GalleryReturn` opens that menu by itself 10 s later (the morning star's 8 s fade,
  then 2 s); X closes it and you can stay. You land at the Gallery's start (0, 0, 6), facing The Bedroom.
- **Two players:** each scene is its own room, so whoever enters first is alone in the night until the other follows;
  both then share one night (see "Two players (shared night)").
- **Made by** Tools > InnerFrame > Starry Night > **Add to Gallery** (`Editor/SN_GallerySetup.cs`): the image's import,
  `Gallery/SN_GalleryPainting.mat`, `Gallery/SN_GalleryEntrance.prefab` (the frame and pedestal are Gallery.fbx's own
  meshes and materials, by reference, at The Bedroom's offsets from its wall, read from `Gallery.unity`), one instance in
  `Gallery.unity` (its last root; extra copies removed, drift reverted) and the build-list entry. Running it twice changes
  nothing. Batch: `-executeMethod InnerFrame.StarryNight.EditorTools.SN_GallerySetup.RunBatch -quit`.
- **The image:** `Art/Source/StarryNight/make_gallery_painting.py` (`/usr/bin/python3 -I`, Pillow) makes the 2048 × 1622
  JPEG from Commons' full-size Google Art Project file (44,567 × 35,291; Cici's download, kept in
  `refs/3-starry-night/gallery/`; decoded at 1/8, never whole); same input, same bytes. It is imported at 2048 × 2048
  (power of two, to larger) with mipmaps, ASTC 6×6 on Android: Unity 6000.3 leaves a non-power-of-two texture with
  mipmaps uncompressed (RGBA32, ~17 MB), and without mipmaps the painting would shimmer from across the room.

## The story

| State | What the player does | Vincent | Hint: title / line |
|---|---|---|---|
| Arrive | walks up the path; within 3 m of Vincent | L01 "Ah, a visitor…" | Walk up to Vincent / Walk up the path to Vincent |
| Sit | sits down on the blanket: the head enters the seat (0.6 m round it, any height) behind the rock's right end, Vincent's seat 42° to the left and 1.2 m away | L02 "Too much light…" | Sit beside Vincent / Sit on the blanket next to him. |
| Lantern | points at the lantern on its post (or touches it, or holds it) and pulls the trigger: light and flame ease down over 1.5 s, the other stars fade in | L03, then L04 names N1 | Dim the lantern / Point at it, pull the trigger (index finger). |
| Stars | points at a star and pulls the trigger (4° aim assist): it blooms. Any star can be woken; N1, N2, N3 move the story only in order | L05 names N2, L06 names N3 | Wake the stars / Point at the star he names, pull the trigger. |
| Wind | after N3 the sky starts to paint; a dotted curve appears along the painted swirl (Cici's red line, 21 points): hold the trigger and follow it, point by point (6°) | L07, then L08 at 100 % | Trace the wind / Hold the trigger and follow the dots. |
| Dawn | 10 s after the wind: the sky pales over 20 s, the stars fade, then a page appears on the rock | L09 | Dawn / Wait for dawn; once the page is there, Take his page / Take the page |
| Done | takes the page; the morning star fades last | | A Night with Vincent / The night is painted |
| Sketchbook (any time after the lantern) | holds his sketchbook up against the view toward the valley for 1.5 s | L10 | (unchanged: the current state's title and line stay, and its repeat still fires) |

- Early steps are accepted: turning the lantern down before sitting skips Sit; waking N3 first wakes it but N1 must still
  come first for the story.
- Nothing moves for 25 s in Stars or Wind: he says the current instruction again.
- His lines play one after another; a newer story line drops older ones not yet started, so he never asks for something
  already done (L08–L10 are never dropped).
- Score (logged at Done): stars woken (of 12), named stars in order (of 3), wind traced, letter found.

## Controls

**Quest (either hand):** the trigger (index finger) acts (XRI's Activate, unused anywhere else in our gameplay): turn the
lantern down (that hand's ray within 10° of it and 1.6 m, or the hand within 0.2 m of it, or the lantern held), wake a
star, hold to trace the wind. The grip (middle finger) picks up (the lantern, optional; the sketchbook; the page); let
go, the lantern glides back to its hook in 0.4 s. A thin beam shows where you
point while there are stars to wake or wind to trace (XRI's own rays stop at 2 m). Thumbsticks: the usual move, turn
and teleport (the hilltop is a teleport area out to 5.5 m; low invisible walls stop you walking off).

**Editor, no headset** (the team's `DesktopTestRig` swaps in, as in the Gallery):

| Key | Does |
|---|---|
| W A S D, mouse (click to capture, Esc to free) | walk and look (DesktopTestRig) |
| mouse | aims (the view's centre while the mouse is captured) |
| Space | the trigger (the lantern: the view's centre on it, within 1.6 m) |
| G | grab, or release, the nearest grabbable within 1.5 m (held in front of the view) |
| 1 / 2 | jump to the player start / the player seat |

Vincent's words and the hint use the shared dialogue box, the same as in every scene: `VG_DialogueUI`
(`Assets/VanGogh/Prefabs/VG_DialogueUI.prefab`, spawned by `SN_Bootstrap` like everything else) with this scene's
settings, `VG_Dialogue_StarryNight.asset`: 1.6 m ahead, its top edge 22° below the gaze (at a level gaze the box spans
−29° to −22° and the hint's plate −19° to −14°, under the village's lowest windows; looking 25° up the hint's top is at
10°, under the swirl), 33° wide, 4.0 cm capitals for his line, the hint (the step's title and its line, see "The story";
at Done the scene's name, "A Night with Vincent", the settings' `hintTitle`) on its plate, as in the Bedroom. It moves
only once you look more than 20° away and is drawn over the world.
It follows the gaze down to 60° below level (`lowestGaze` −60 since 2026-11-10; at −30 a chair-seated player looking
down at the lantern had the box and the hint over it). `SN_Voice` keeps
the line queue (a newer story line drops older ones not yet started) and calls `Say("Vincent", text, clip)` and `Clear`;
`SN_Game` sets the hint (`SetHint(SN_Game.TitleFor(state), SN_Game.HintFor(state))`, the shared API as it was) and
steps the UI each frame. The controller beam draws just before the UI (render
queue 2999 against its 3000), so the box and the hint always draw over it. To change where the box sits, edit the
settings asset (the Bedroom has its own).

## The [SN] log (for testers)

Every state change logs `[SN] <state> t=<seconds since the start>` (and raises `SN_Game.StateEntered`, for the portrait
deck later); the score at Done logs `[SN] score stars=…/12 named=…/3 wind=… letter=…`.

## Two players (shared night)

Set up the same way as the Gallery and the Painted Bedroom (`docs/multiplayer/README.md`): nothing in the scene.
`NetworkSession` joins `InnerFrame_StarryNight` by itself and spawns your avatar (the default `NetworkPlayer`; a
`Resources/NetworkPlayer_StarryNight` prefab would give this scene its own, as the Painted Bedroom has). What the scene
itself needs to share is done by **`StarryNightLink`** (`Assets/Script/Network/`), one more component on each avatar
beside `VincentLink` and `MirrorLink`:

- **The night.** Each player sends how far their night has got (`SN_Game.Progress`: arrived, seated, the lantern down,
  the stars woken, the wind's dots, the letter, the page taken). Each headset merges everyone else's into its own game
  (`SN_Game.ShareRoom`), which plays it through the same paths as its own player's actions: a star one player wakes
  blooms for both, the wind can be traced together (the furthest dot counts), and Vincent says the same line on every
  headset. Progress only grows and each player re-sends what it merged, so a late joiner catches up and someone who
  leaves takes nothing with them. Dawn, the paint and the voice keep each headset's own clock (a few frames apart).
- **The lantern, the sketchbook, the page.** They are spawned by `SN_Bootstrap`, not saved in the scene, so
  `NetworkGrabbable` (scene objects only) doesn't fit; they are shared like the bedroom's mirror instead. Whoever moves
  one sends its pose; the other copies follow it and can't be grabbed meanwhile. Two grabs at the same moment: the lower
  player number keeps it. After a let-go the mover keeps sending until its physics has put it down (at most 4 s), and
  every copy is put exactly there. The lantern's glide back to its hook is such a let-go: a moving, non-kinematic body
  (`SN_Grabbable.Settling`), so the mover keeps sending its pose until it is on the hook and kinematic, and every copy
  follows it there (a copy that stops within 2 cm of the hook is put exactly on it). Turning the lantern down without
  holding it is the same `SN_Lantern.TurnDown` as holding it, so it is shared as before (`SN_Game.Progress.lanternDown`).
- **Not shared:** where each player stands, walks and sits (each player's own, through the avatar), and the score's
  attention (each headset logs its own `[SN] score`, which counts the room's stars).

Alone, or offline, nothing calls `ShareRoom` and the night plays exactly as before. The temporary
`SN_Bootstrap.LeaveNetworkSession()` (blockout, 2026-11-04) is gone; `NetworkSession` is unchanged.

After pulling this: **InnerFrame → Multiplayer → Rebuild Network Player Prefab** and **Rebuild Painted Bedroom Avatar**,
so both avatar prefabs carry `StarryNightLink` (Fusion rebuilds its prefab table). Until then the room still joins and
avatars appear, but the night isn't shared.

## What is made of what

| Thing | Now | Made by |
|---|---|---|
| The seat | `SN_PlayerSeat` (0.45, 0, −0.9): Vincent's seat 41.6° to the left, 1.20 m away; the cypress 35.5° left, 4.05 m; the lantern on its post 0.45 m from the seat at yaw 25°; marked by the blanket (2026-11-09) | `SN_Layout` |
| The lantern's home (2026-11-09) | a thin iron post 0.62 m tall, 0.14 m right of the lantern seen from the seat, its arm and hook over the handle; the lantern hangs there kinematic, its handle 0.50 m above the grass, 0.45 m from the seat at yaw 25° (right of the spire); let go anywhere, it glides back (0.4 s). Until it's turned down its light and flame pulse (±15 %, 0.6 Hz); a ray or a hand on it: +30 % and one buzz (0.15, 0.05 s). No collider | `SN_MeshGen.LanternPost`, `SN_Layout.lanternHome`, `lanternPost*`, `lanternPulse*` |
| The blanket (2026-11-09) | striped wool in the painting's colours, 0.70 × 0.50 m, fringed at its ends, on the grass at the seat; no collider | `make_blanket.py`, `SN_MeshGen.Blanket`, `SN_Layout.blanket*` |
| Cypress | Tripo model, 9,401 triangles, 7 m tall, 1.6 m at its widest (no decimation) | `make_tripo.py` |
| Rock | Tripo model scaled ×1.457 across (its top's area-weighted mean 0.26 m), raised ×1.19 in height for Vincent's seat (`SN_Layout.rockHeightScale`): 1.46 × 0.82 × 0.37 m, its top's mean 0.31 m; a MeshCollider of its own shape | `make_tripo.py`, `SN_Layout` |
| The rock's keep-off rim (2026-11-09) | an invisible convex prism round the rock (`SN_Hilltop/RockRim`, layer Ignore Raycast, no renderer): its outline seen from above pushed out 3 cm, 1.0 m above the ground (`SN_Layout.rockRim*`). The rig's CharacterController climbs walls up to 0.55 m (measured: its step offset, 0.5, is not scaled with the 0.8 rig), so without it the rig stepped onto the rock from the seat (camera +0.17 m), slid and dropped off. The props pass through it (`SN_Grabbable.IgnoreAlways`); XRI's rays and ground check leave Ignore Raycast out; Vincent has no collider | `SN_MeshGen.RockRim`, `SN_Setup` |
| Lantern | base, iron frame, glass (transparent), cap, handle, a wick key that turns as it dims; 628 triangles; the flame two crossed quads in one mesh (`SN_FlameCross`); its box collider a trigger while held or gliding home, and never in contact with the rig's CharacterController or the rock's rim | `make_props.py`, `SN_MeshGen` |
| Sketchbook, page, charcoal | the sketchbook open to 120° on one cover, the sketch on its page; a torn page with the sketch (both grabbable, on the raised rock, clear of Vincent; placed where physics leaves them asleep and tilted 10° or less): the page 0.73 m from the seat (tilted 8°), the sketchbook at the rock's right end, 0.78 m (no spot within 0.75 m rests; its old spot is under Vincent now); the stick of charcoal in Vincent's right hand (its model only: no physics, not grabbable) | `make_props.py`, `sketch-page.png`, `SN_Layout` |
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
| Vincent | Vincent v2: Cici's Tripo model (10,619 triangles, one 2048² texture), rigged by Mixamo (33 bones, 28 skinned; mitten hands), 1.70 m, sitting on the rock (his root at (−0.17, 0, 0.19), facing the valley); SittingIdle, SittingTalking, SitToStand, BreathingIdle; look and point by Humanoid IK (`SN_VincentDriver`); see "Vincent" | `Models/Vincent` (Mixamo files, unchanged), `make_vincent_texture.py`, `SN_Setup` |
| Still placeholder | Vincent's lines (Piper, voice `en_US-norman-medium`) | |

## Checks

One command runs every check: the playthrough test, the dialogue box check, the layout check, the Vincent check and the
Gallery entrance check, then the Play-mode check, the props' spawn check, the rig check and the Gallery way in and back
(Play mode, one after the other).
It prints one line per check (`[SN Checks] <check>: PASS|FAIL (n passed, m failed); log …`), then one
`[SN Checks] SKIP (not a pass): …` line for each thing a check could not test here (2026-11-09: the Gallery way in and
back's real Photon join, where the checkout has no Photon App Settings), and a last `[SN Checks] PASS` or `FAIL` line
(a PASS with skips says how many), and writes them to `SN_checks_summary.txt` beside the logs. A skip is not a failure
(exit code 0) and not a pass.

1. **In the Editor:** Tools > InnerFrame > Starry Night > **Run all checks** (it saves nothing; it asks to save your open
   scene first, enters Play mode for the last check, then shows the summary).
2. **From a terminal** (macOS; on Windows, the Unity.exe of the same version), **without `-quit`**, so the Play-mode check
   can run; exit code 0 only if every check passed:

   ```
   /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . \
     -executeMethod InnerFrame.StarryNight.SN_Checks.RunAll -snLogDir Logs/SN_checks -logFile Logs/SN_checks.log
   ```

   With `-quit` the edit-mode checks run and the two Play-mode checks are skipped (the summary says so).

The lantern check (`Editor/Tests/SN_LanternCheck.cs`, edit mode; Tools > InnerFrame > Starry Night > Run Lantern Check, or
`-executeMethod InnerFrame.StarryNight.EditorTools.SN_LanternCheck.RunBatch -snLog <file>`), from a floor-seated eye
(0.64 m) and a chair-seated one (0.96 m) over the seat:
1. the lantern's home: its handle 0.50 m above the grass, 0.45 m from the seat at yaw 25° (1 cm, 1°), on its hook,
   kinematic;
2. from both seated eyes, the handle within 0.65 m and the lantern within 45° of a gaze at the valley 15° down;
3. looking at the lantern's centre from both seated eyes (the Lantern step's title and line shown), the dialogue box
   covers at most 10 % of it, and the hint at most 10 % (15 % from the floor-seated eye: from 0.47 m the lantern is
   about 40° tall and the hint lies over its base, not its flame; Cici's option A, 2026-11-10);
4. no bush within 0.10 m of the post or the hanging lantern;
5. the hints, as in "The story": each state's title and line (Dawn's two); no title or line says "grab"; the Lantern
   line names the trigger's finger; a line naming the grip would name its finger.
Controls that must fail: the lantern on its old spot on the grass (2, 3), a bush vertex on the post (4), the old Lantern
hint and the Stars step under a wrong title (5).

The Gallery entrance check (`Editor/Tests/SN_GalleryCheck.cs`, edit mode;
`-executeMethod InnerFrame.StarryNight.EditorTools.SN_GalleryCheck.RunBatch -snLog <file> -quit`) opens `Gallery.unity`:
1. one entrance, at its spot, no overrides beyond its pose and name, appended as the last root;
2. one interactable and one collider per part; the button's one persistent call (`SceneChangeButton.Activate`, target
   `StarryNight`, 1 s, a 0.4 m box, not a trigger);
3. in the room: the painting's centre and size, the frame on the wall's face, the pedestal on the floor, the button on
   its top, no Gallery collider crossing the frame or the pedestal (physics), the gap to the nearest bench (0.42 m);
4. the same offsets, meshes, materials and button settings as The Bedroom's (in the same scene);
5. the build list (enabled, after the Gallery, not first); the image (2048 × 1622, imported 2048 × 2048, ASTC 6×6 on
   Android) and its material (URP Unlit, white);
6. `SN_GallerySetup` run again changes nothing (the files' hashes are equal); with the entrance moved in memory it puts it
   back and saves `Gallery.unity` byte for byte as it was.
Controls that must fail: a button aimed at PaintedBedroom, no persistent call, a second entrance, the entrance moved
0.5 m, a build list without StarryNight or with it off, a pedestal-sized box on Bench_03.

The Gallery way in and back (`Editor/Tests/SN_GalleryPlayCheck.cs`, Play mode, Editor without a headset, **offline**: on
each scene's first frame it notes the join NetworkSession was starting, then stops it before it connects;
`-executeMethod InnerFrame.StarryNight.EditorTools.SN_GalleryPlayCheck.RunBatch -snLog <file>`, without `-quit`):
1. StarryNight opened directly: the world's fingerprint (SN_World's objects, renderers, triangles, names);
2. the Gallery as the game starts; the desktop dot on the button from 1.2 m in front of the pedestal; the press → a fade
   to black → StarryNight by name: one scene, one XR Origin, one desktop rig, one camera, one audio listener,
   NetworkSession starting StarryNight's own join (for `InnerFrame_StarryNight`) with no runner left, Arrive, the dialogue box at 1.6 m, and the same world and light as opened directly; at 9 s still
   no runner;
3. back from anywhere (the menu): the Gallery's start (0, 0, 6) yaw 180, one rig, one listener, the portrait once, one
   entrance, NetworkSession starting the Gallery's join (for `InnerFrame_Gallery`) with no runner left;
4. in again; Done: the menu still shut 9 s later, open by 11.5 s with "Back to Gallery"; Back → the same landing.
Controls that must fail: the dot turned away, one extra object in SN_World, StarryNight loaded additively over the
Gallery (two scenes, rigs, cameras, listeners), Back with the menu's home set to PaintedBedroom.
Session 1 joins `InnerFrame_StarryNight` for real only where Photon's App Settings exist
(`Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`, gitignored: the App ID is a key and the repo is public). In a
checkout without them (2026-11-09) its join is stopped on its first frame, like session 2's, and the real join is reported
as `SKIP`: before, Fusion's "Photon Application Settings not found" errors failed the session's "no errors" line.

The props' spawn check (`Editor/Tests/SN_SpawnCheck.cs`, Play mode, offline, the scene as `SN_Bootstrap` spawns it;
`-executeMethod InnerFrame.StarryNight.EditorTools.SN_SpawnCheck.RunBatch -snLog <file>`, without `-quit`):
1. `SN_World`'s Rigidbodies are exactly the lantern, the sketchbook and the page (the charcoal in his hand is only its model);
2. over the first 90 frames, on every frame and physics step, and again 2 s in, each within 5 cm of its spawn spot
   (`SN_Bootstrap.LanternSpawn`, `SketchbookSpawn`, `PageSpawn`; the page is hidden until dawn: its Transform);
3. the page shown as at dawn rests within 5 cm of its spot 1.5 s later.
Control (a second Play session, must fail): `SN_Bootstrap.ControlPlaceByTransformOnly`, the props placed through their
Transform alone, as before 2026-11-09 (the lantern then ends 1.09 m away inside the rock, the sketchbook 0.67 m away).
Why nothing caught it before: the props' bodies interpolate, and an interpolating body moved only through its Transform
right after `Instantiate` keeps the prefab's pose (the world origin, inside the rock); `SN_Bootstrap` now places them with
`SN_Grabbable.Place`, which moves the body too. The layout check settles the props in a preview scene
(`SN_TestWorld.Settle`: `Physics.SyncTransforms`, then `Simulate`), where nothing interpolates, and measured only the page,
the sketchbook and the charcoal; the Play-mode checks measured the rig, the network, the UI and `SN_World`'s counts and
names, not where the props are.

The rig check (`Editor/Tests/SN_RigCheck.cs`, Play mode, offline, **the scene's own XR Origin** with its TrackedPoseDrivers
and XRInputModalityManager off: the head and the right hand are set by the check, the lantern is selected by the rig's
own right Near-Far Interactor and the rig walks through its own DynamicMoveProvider, at 72 frames a second;
`-executeMethod InnerFrame.StarryNight.EditorTools.SN_RigCheck.RunBatch -snLog <file>`, without `-quit`). Each with a
control that must fail:
1. holding the lantern close and walking 2 m downhill, the rig follows the ground as with nothing held (within 2 cm) and
   doesn't drop when the hand moves out (control: its box forced solid while held: 0.40 m apart, a 0.43 m drop). While
   held, the props' colliders are triggers: XRI's GravityProvider decides "grounded" by a sphere cast down from the head,
   which a solid lantern held under the head stops;
2. walking into the lantern on the grass, the camera steps no more than without it and the CharacterController never hits
   it (control: the `Physics.IgnoreCollision` pair removed: a 0.27 m camera step);
3. the rim: from the seat toward Vincent the rig stays on the ground (control: the rim off: 0.26 m up the rock); the
   straight way from the player start to the seat is clear; seated at the seat, still and leaning toward Vincent, the
   rig isn't moved and the head stays within the seat's 0.6 m; pushing the stick toward the rock, it doesn't climb it;
4. the props pass through the rim: the page shown at dawn and the sketchbook rest on their spots (control: a page copy
   that doesn't ignore the rim is pushed off); Vincent has no collider or Rigidbody;
5. let go 1.5 m away, the lantern glides back to its hook within 1 s (0.44 s), kinematic and solid again, `Settling` on
   the way (what `StarryNightLink` keeps sending on) and a trigger (control: `SN_Grabbable.ControlNoReturn`: it stays
   1.5 m away).

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
6. the far ridge 0.6° under the painted strip's ridge over the strip's faded ends and 12° past them, seen from the seat eye;
7. the rock's rim: convex, no renderer, on Ignore Raycast; its top at least 0.75 m above the ground all round (the rig
   climbs walls up to 0.55 m); at least 0.25 m from the seat and from the straight line from the player start to the
   seat; every prop set to ignore it.

Positive controls (each must FAIL, on a copy): the rim's top lowered to 0.3 m, a bush moved onto the path, a cloud raised 3 m, the page dropped 0.9 m from
the seat.

The Vincent check (`Editor/Tests/SN_VincentCheck.cs`; Tools > InnerFrame > Starry Night > Run Vincent Check, or
`-executeMethod InnerFrame.StarryNight.EditorTools.SN_VincentCheck.RunBatch -snLog <file>`) steps his Animator by hand in
edit mode and measures his bones and his baked skin:
1. the avatar valid and human; his bind-pose height 1.70 m ± 1 cm;
2. sitting (SittingIdle 12 frames, SittingTalking 60): no thigh or buttock vertex more than 1.5 cm inside the rock, no gap
   over 1.5 cm under the hips, the soles within 3 cm of the ground, no lower-leg or foot vertex inside the rock;
3. Talking on whenever the box speaks, off 1 s after it stops;
4. 1 s after each look target changes, his head within 15° of it (more for the sketch, looked at with weight 0.6), or, for
   a target past `vincentLookMaxYaw` (you, about 150° round), turned to that limit;
5. the charcoal's axis within 10° of N1, N2 and N3 during their lines (positive control: N2 during N1's line fails);
6. each loop drifts under 1 cm in 20 s; no hip jump over 2 cm at any transition (against the clips' own motion);
7. after SitToStand: no leg vertex inside the rock, no bush vertex inside him, 0.6 m or more from the player's seat, the
   soles on the ground (± 3 cm).

## Headset checklist (team playtest, Thu 11/05)

Install the APK (ask Cici), sit on the floor beside the rock when the hint says so, and note for each: fine, or what's wrong.
1. **Text and the box:** can you read Vincent's line without leaning in? The box sits 22° below your gaze (measured to its
   top edge), 1.6 m away, and moves only once you look more than 20° away: does it follow comfortably, and does it ever
   cover the village or the swirl you're tracing?
2. **Aiming:** the aim assist is 4°. Can you wake each star he names on the first or second try? Too generous?
3. **Reach from the seat:** can you take the page (0.73 m from the seat) and the sketchbook (0.78 m, at the rock's right
   end) without getting up?
4. **The halos:** do the woken stars' halos read as painted, and is the morning star (N3) easy to tell from the cypress?
5. **The mist and the clouds:** does the hill float in a sea of mist and cloud round the rim? Anything flat, doubled or
   popping at the far ridge?
6. **Vincent:** does he sit on the rock (no floating, no sinking)? When he speaks, does he turn toward you, and is his
   head turned far enough (he can turn about 100° of the 150° round to you)? Does the charcoal read as pointing at the
   star he names (watch N1 over the church and N3 right of the cypress)? Any tearing or creasing at the shoulders,
   elbows, hips, the jacket's tails, or the neck when he looks back at you? At dawn, does he stand up cleanly in front of
   the rock?
7. **Frame rate:** watch it through the whole scene with the colour grading off (the default), then once more with
   `SN_Layout.colourGrading` on (rebuild the APK): about 161,100 triangles (his 10,619, one skinned mesh) and 35
   transparent renderers at the most.
8. **The lantern (2026-11-09) and the steps (2026-11-10):** from the path, do you notice the blanket? Does the hint's
   title (the step you are on) and its line tell you what to do next, each time, without anyone explaining? Sitting on the
   floor, then on a chair, can you see the lantern on its post without searching? Point at it: does it brighten and buzz
   once? Pull the trigger without grabbing it: does it turn down? Grab it, walk down the
   path holding it close, let go: no floating or dropping, and does it glide back to its hook? Does the dialogue box or
   the hint cover the lantern's flame when you look down at it?
   Walk into the rock from the seat: you should stop at it, never step up onto it.
9. **Two headsets (2026-11-09):** both in the night. One points at the lantern and turns it down without grabbing it:
   does it turn down on both headsets at the same moment (and the stars come out on both)? Then one grabs it, carries it
   away and lets go: does it glide back to its hook on both, and end on the hook on both? Then the other grabs it.

## Vincent

What drives him (`SN_VincentDriver` on his Animator's GameObject, stepped by `SN_Game` after the voice and the box;
every number in `SN_Layout`, the `vincent*` fields):
- **The controller** (`Prefabs/SN_Vincent.controller`, built by the tool): SittingIdle (the default) and SittingTalking
  on the bool `Talking` (0.3 s fades); the trigger `Stand` from either into SitToStand (0.25 s), then Standing
  (BreathingIdle) at its end (0.25 s); the IK pass on. `Talking` follows the box's `IsSpeaking` with a 0.6 s hold, so a
  pause between sentences doesn't flick him back. `Stand` fires once, when Dawn begins (10 s after L08); root motion is
  on from then (SitToStand's step forward: 0.30 m).
- **The look** (Humanoid look-at IK: body 0.15, head 0.8, eyes 0, clamp 0.5; the weight ramps over 0.5 s; the look
  follows its target as a yaw and a pitch in his frame, about 0.35 s):
  - Arrive: a point 0.5 m in front of his hips, at weight 0.6, as if sketching;
  - from L01 through the lantern: your head (the head the box follows);
  - while he names a star (L04 N1, L05 N2, L06 N3): that star until it's woken, then you;
  - L10 (the sketch): you; Wind: the guide's next dot; Dawn: the morning star (N3), from L09 you; Done: you.
  - You sit about 150° round behind his right shoulder. Past about 115° Unity's clamp blends the look out and his head
    swings back, so the look's yaw is kept within `vincentLookMaxYaw` (100°): he turns his head as far as the clamp lets
    him toward you, not fully round.
- **The pointing** (right-hand IK, then the wrist turned so the charcoal points): from the start of each naming line until
  1 s after it ends or the star is woken, whichever is first; the hand 0.85 of his arm's length from the shoulder toward
  the star, the elbow hinted out and down; the weight up over 0.4 s, down over 0.5 s. The named stars are 22° (N1), 6°
  (N2) and 25° (N3) from his facing (a star past 110° would get weight 0.5). His hands are mittens (no index finger), so
  the charcoal points: in his right hand, its tip out past the fingers (`charcoalInHand`).
- **The seat rule:** the seat is built to match the clip, not the clip squashed to the seat. At 1.70 m his sitting
  clips' seat (the lowest thigh or buttock point) is 0.316 m; the rock is raised (×1.19 in height, its width and depth
  as before) so its surface under his hips is there: 0.8 cm in at the deepest, 0.6 cm gap at the worst, over 72 sitting
  frames. His root is his centre of mass, the hips 0.12 m behind it. At the art pass's flattest spot (−0.17, 0.12) the
  seat fit but his shins and feet were inside the rock's front, so his root is at (−0.17, 0.19), facing the valley (yaw 0).
- **The import** (by the tool): Humanoid, scaled ×1.7008 (the files are in centimetres; Tripo's model is 0.9995 m); the
  avatar is made from BreathingIdle's skeleton and copied to the character and the other clips, because the character
  file's own rest pose has the hips' offset along Z (Tripo's Z-up model through Mixamo), and an avatar made from it
  retargets every clip wrongly. No materials from the FBX (Mixamo's has Metallic 1): `SN_VincentV2.mat` is ours (URP
  Lit, metallic 0); the texture is copied out of the FBX at 2048² by `make_vincent_texture.py` (no `.fbm` folder).
- **Skinning:** his `SkinnedMeshRenderer.quality` is Bone4. Under the Mobile quality level (skin weights 2) in the Editor,
  Auto and Bone4 render the same, and Bone2 differs, so Bone4 changes nothing here; whether the Quest build applies the
  quality level's 2 is for the headset test. `QualitySettings` is a team setting and is unchanged.
- **Arrive** is still measured from where the placeholder stood (`SN_Layout.vincentStand`), so the walk-up triggers his
  greeting at the same place as before; the bushes keep clear of that spot as before.
- Batch-mode renders (for reports) draw his skin as last skinned, without that frame's IK: draw a `BakeMesh` copy instead.

## Tests

## Tests

- **Tools > InnerFrame > Starry Night > Run Playthrough Test** (or batch:
  `-executeMethod InnerFrame.StarryNight.EditorTools.SN_PlaythroughTest.RunBatch -snLog <file>`): the whole story from
  Arrive to Done through a scripted input, and the negative checks (a trigger 10° from every star wakes nothing; N3 before
  N1 wakes it but doesn't advance; tracing the wind backwards doesn't complete it; the lantern down before sitting skips
  Sit), each with its positive control; and the letter found during Stars: after L10 the Stars hint is still shown and
  the 25 s repeat still fires. Since 2026-11-09 also the lantern turned down without holding it (F): the trigger aimed 20°
  off it and from 2.5 m does nothing (the controls), the ray on it brightens it with one buzz, the hand 0.1 m from it
  (touching) turns it down, and so does the ray from a chair-seated eye; `Progress.lanternDown` is set. Since 2026-11-10
  the hint's title as drawn, sampled after every step: in each state it is that state's title and only it (Dawn: "Dawn",
  then "Take his page"; control: a wrong title shown during Done fails); each state's line in the log shows its title
  and its hint.
- `SN_UICheck.RunBatch -snLog <file>` (batch): the dialogue box in this scene: every line's pages fit in three lines at
  full size; every hint (each state's title and line) fits: its plate no wider than the box (control: a line twice
  over); where the box and the hint sit from the seat; a 15° head turn leaves it, a 30° turn catches up in under
  0.6 s (72 Hz); the beam's render queue is below the UI's.
- `SN_PlayCheck.RunBatch` (batch, without `-quit`): the Play-mode check of the temporary network measure above, and that
  the dialogue UI shows the first hint at the settings' distance from the camera.
- `SN_GalleryCheck.RunBatch` and `SN_GalleryPlayCheck.RunBatch`: the Gallery's way in and back (see "Checks").

## Requested hardware tests (Quest)

1. Text: is Vincent's line readable without leaning in, at the default distance? Too big or too small?
2. The UI: does it follow comfortably (not too lazy, not jittery)? Does it hide the village or the swirl you are tracing?
3. Aiming: can you wake each named star on the first or second try? Is 4° of aim assist too generous?
4. The wind: is the dotted line visible against the night, and does following it feel like painting?
5. The lantern: point at it on its post and pull the trigger; does the hill go dark enough for the stars to read?
6. Sitting: does the seat zone (0.6 m) catch you when you sit down on the blanket beside the rock?
7. Frame rate through the whole scene, especially with the painted sky, the mist layers, the clouds, the bushes and
   Vincent (35 transparent renderers at the most, about 161,100 triangles, one skinned mesh; then once more with
   `SN_Layout.colourGrading` on).
8. The art pass: from the seat, do the cypress, Vincent and the valley sit where the concept (G2) has them? Is N3 easy to
   tell from the cypress? Can you reach the page (0.73 m from the seat) and the sketchbook (0.78 m) without getting up?
9. The look pass: does the hilltop read lush and the hill float in a sea of cloud? Do the bushes near the seat stay out
   of the way? Does the far ridge read as one line all round, with no doubled hills where the painted strip ends?
10. Vincent (step 3): see item 6 of the headset checklist above.
11. The Gallery: does The Starry Night on the east wall read as part of the gallery (size, frame, light), and is its
    pedestal easy to find and press (grip, pointing or touching)? The fade in, the start on the path, and Back to
    Gallery from the menu, any time and at Done (the menu opens by itself 10 s after "The night is painted").
12. Online (not covered by the Editor check, which stays offline): two headsets in the Gallery; one enters The Starry
    Night: does the other see their avatar leave, and does the first rejoin `InnerFrame_Gallery` on the way back
    (`[NetworkSession] Joined InnerFrame_Gallery`)? With the other player already in the room, the returning one is moved
    0.7 m sideways from the start (`NetworkSession.ShiftRig`): is that comfortable?
