# Bedroom Speak sign

Owner: Rowling. Approved design: 19 November 2026, the subdued painted-wood variant, Speak only.

The Bedroom easel carries a 32 × 16 cm printed wooden plaque: **Speak / Hold Ⓑ to speak / Release to send**. It is a child of the existing stationary `VG_Easel`, beneath the canvas-support rail. Two wooden tabs mount it to the rail. It has ordinary opaque scene depth, no Canvas, no collider, no input binding and no head-follow code. Vincent's existing model, animation, hands and props are not rebuilt.

Only the duplicate Bedroom idle B reminder and that Vincent's first-use talk instruction yield to the physical sign. Recording/error status, mirror task instructions, subtitles, voice input and other scenes keep their existing behavior. A missing/inactive plaque restores the old idle reminder. No Grip plaque or exit buttons are added. Arrival orientation and a proposed first-arrival voice/brief highlight are not changed by this feature.

## Assets and rebuilding

- `Assets/BedroomSpeak/Art/SpeakFace.png`: generated flat face from the approved concept; readable English and circled B.
- `Assets/BedroomSpeak/Resources/BedroomSpeakSign.prefab`: Unity-created physical plaque and mounting tabs, 38 triangles total, existing easel material on the sides/tabs, referenced opaque URP Unlit material on the face.
- `Tools > InnerFrame > Bedroom Speak > Rebuild sign`: reimports the new face and rebuilds only the new prefab/material. Does not save the Bedroom or Vincent prefab.
- Texture: 2048 × 1024 imported, mipmaps/trilinear, Android ASTC 4×4. No new realtime lights.

The new prop uses the 2D-plaque route plus a shallow Unity primitive backing; existing character/furniture assets stay intact. Generated-image terms were checked on 2026-11-21 and are recorded in `docs/ASSETS.md`.

## Local verification

- Unity 6000.3.23f1 compiled the scripts and built the prefab.
- `Logs/BedroomSpeak/editor-checks.txt`: 11 checks, including idempotent attachment, correct parent, depth rendering, actual Standing animation, idle-reminder fallback and recording/error status.
- `Tools > InnerFrame > Bedroom Speak > Test and open Bedroom`: real Editor Play Mode, offline preview, negative control for the first-use prompt and 72 distinct game frames of camera motion. Both the camera's applied motion and the stationary plaque are checked after frame updates. Leaves the Bedroom open for inspection. Does not alter the saved spawn pose.
- Captures and the Play report are in `Logs/BedroomSpeak/` (not committed).
- B recording, live Vincent answers, multiplayer and Quest were **not** tested in this change.

## Integration notes

Open PRs were read and merge-tree checked on 19 November. No other owner's PR touches the three existing source files edited for the Speak behavior. Existing branch-wide conflicts with #226 (Gallery/transition/docs) and #205 (`docs/ASSETS.md`) were observed before this change. The feature was published to draft PR #198 on 21 November; it has not been merged into main.
