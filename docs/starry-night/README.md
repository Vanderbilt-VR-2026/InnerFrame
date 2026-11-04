# The Starry Night (step 2: a playable blockout)

Owner: Cici (Sprint 4). A small hilltop above Saint-Rémy at night. Vincent sits by a rock with a lantern and his
sketchbook; one cypress; a village far below; a huge sky. **The sky is the game:** it starts as a real night and turns
into his painting as the player helps him. Score attention, never speed: no fail state, no timer, and a later step done
early is accepted.

This step is a blockout: **the gameplay is real; every model, texture and sound is a placeholder** that later passes
replace by name.

## Open and play

- Scene: `Assets/StarryNight/Scenes/StarryNight.unity`. It holds only the player rig (the Starter Assets
  `XR Origin (XR Rig)`, scaled 0.8 with 2 m rays, as in the Gallery and the Bedroom) and one `SN_Bootstrap`, which
  spawns everything else from `Assets/StarryNight/Prefabs` at runtime.
- After changing `SN_Layout.asset` (every number of the scene: places, sizes, timings), `lines.txt`, or the sources in
  `Art/Source/StarryNight`: **Tools > InnerFrame > Starry Night > Build**. Running it twice changes nothing.
- The scene is not in the build list (`EditorBuildSettings` unchanged). To try it on a Quest, add it to your build
  profile locally, or build it alone (Android build profile, not a Meta Quest one).

## The story

| State | What the player does | Vincent | Hint |
|---|---|---|---|
| Arrive | walks up the path; within 3 m of Vincent | L01 "Ah, a visitor…" | Walk up the path to Vincent |
| Sit | sits down: the head enters the seat beside the rock (on his right) | L02 "Too much light…" | Sit down beside Vincent |
| Lantern | grabs the lantern (grip) and pulls the trigger: light and flame ease down over 1.5 s, the other stars fade in | L03, then L04 names N1 | Grab the lantern and pull the trigger to turn it down |
| Stars | points at a star and pulls the trigger (4° aim assist): it blooms. Any star can be woken; N1, N2, N3 move the story only in order | L05 names N2, L06 names N3 | Point at the star he names and pull the trigger |
| Wind | after N3 the sky starts to paint; a dotted curve appears: hold the trigger and follow it, point by point (6°) | L07, then L08 at 100 % | Hold the trigger and follow the dotted line |
| Dawn | 10 s after the wind: the sky pales over 20 s, the stars fade, then a page appears on the rock | L09 | Wait for dawn, then Take the page |
| Done | grabs the page; the morning star fades last | | The night is painted |
| Sketchbook (any time after the lantern) | holds his sketchbook up against the view toward the valley for 1.5 s | L10 | |

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
15° below the gaze, upright, moving only once you look more than 20° away; drawn on top of the world. Text sizes are
named constants in `SN_DialogueBox` (4.0 cm capitals for his line at 1.6 m).

## The [SN] log (for testers)

Every state change logs `[SN] <state> t=<seconds since the start>` (and raises `SN_Game.StateEntered`, for the portrait
deck later); the score at Done logs `[SN] score stars=…/12 named=…/3 wind=… letter=…`.

## TEMPORARY: solo, the network session is left at Start

`NetworkSession` (`Assets/Script/Network/NetworkSession.cs`, Sonya's file) joins a Photon Fusion room in every scene with
an XR Origin, and has no opt-out yet. This scene is solo for now, so **as a temporary measure for this blockout**
`SN_Bootstrap.LeaveNetworkSession()` stops the session's join and makes it leave at Start (log:
`[SN] Solo for now: left the network session …`). The session object stays alive, so the next scene joins its own room
as usual. **To be replaced by an opt-out in NetworkSession** (Sonya's call); nothing in `Assets/Script` was changed.
Checked in Play mode (Editor, no headset): no `[NetworkSession] Joined InnerFrame_StarryNight`, no NetworkRunner or
NetworkPlayer in the scene, the rig not moved (`Editor/Tests/SN_PlayCheck.cs`).

## What is placeholder

The hill (a generated mesh), the rock (a flattened box, top at 0.26 m: the seated Mixamo Vincent's seat height at his
project scale), the cypress (stacked cones), the lantern (cylinders and a flame sprite), the sketchbook and the page
(boxes), Vincent (the project's static model, standing beside the rock at his Bedroom scale; `SN_VincentSeat` marks where
the rigged, seated Vincent goes), the far hills and the village (a dark ring and warm dots), the mist (soft cards), the
painted sky (*The Starry Night*'s own sky, MoMA 472.1941, cropped), the wind's path (`SN_Layout.guidePath`, data), the
UI sprites, and Vincent's lines (macOS `say`, voice Daniel; the text in `Art/Source/StarryNight/lines.txt`).

## Tests

- **Tools > InnerFrame > Starry Night > Run Playthrough Test** (or batch:
  `-executeMethod InnerFrame.StarryNight.EditorTools.SN_PlaythroughTest.RunBatch -snLog <file>`): the whole story from
  Arrive to Done through a scripted input, and the negative checks (a trigger 10° from every star wakes nothing; N3 before
  N1 wakes it but doesn't advance; tracing the wind backwards doesn't complete it; the lantern down before sitting skips
  Sit), each with its positive control.
- `SN_PlayCheck.RunBatch` (batch, without `-quit`): the Play-mode check of the temporary network measure above.

## Requested hardware tests (Quest)

1. Text: is Vincent's line readable without leaning in, at the default distance? Too big or too small?
2. The UI: does it follow comfortably (not too lazy, not jittery)? Does it hide the village or the swirl you are tracing?
3. Aiming: can you wake each named star on the first or second try? Is 4° of aim assist too generous?
4. The wind: is the dotted line visible against the night, and does following it feel like painting?
5. The lantern: grab it by the handle, pull the trigger; does the hill go dark enough for the stars to read?
6. Sitting: does the seat zone (0.6 m) catch you when you sit down on the floor beside the rock?
7. Frame rate through the whole scene, especially with the painted sky and the mist.
