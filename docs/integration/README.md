# Latest Gallery, Entrance and hold-B integration

Scene owners: Gallery = Cici; Bedroom = Rowling. The Gallery scene is the user-approved GalleryIntegration design with Cici cards. This change does not edit the Bedroom scene.

Use Unity **6000.3.23f1**. Open `Assets/Entrance/Scenes/Entrance.unity`, press Play, select a character, and continue through the controller guide. Gallery is `Assets/Scenes/Gallery.unity`.

Gallery controls in the Editor: WASD to walk, J/L to turn, I/K to look vertically, Escape to free the mouse. Click More, close, and ENTER on the artwork cards. The existing pedestal entry remains available. M opens the navy/gold player menu; Back to Gallery returns from paintings. Hold keyboard B or the right controller's B to record a question, then release to send. T remains a compatibility shortcut. A configured Convai key or local AI proxy is required for real replies; no credentials are included.

Entrance includes the controller tutorial, title video, opening soundtrack, click sounds, and gold rays. The Van Gogh lettering now dissolves directly into the gold transition and then the latest Gallery; the three immersion preview cards no longer play between the title and Gallery. Gold prelude is 1.1 s, scene fade-out/reveal are 0.6 s each (half their previous duration); music crossfades use 0.3 s without changing playback speed. The full gold-to-Gallery transition targets at most 15 s, including the prelude, fades, room shutdown, disk load and connection wait. If required scene loading itself exceeds this budget, it finishes safely and reveals without an extra connection wait. The gold edges are brighter, and the video buffer is replaced by gold before release. Other scene transitions retain their existing defaults. Starry Night reuses its dialogue UI for live conversation, pausing story time/audio while a conversation is active and resuming afterwards. Story instructions remain visible alongside the B reminder.

Shared settings:
- `ProjectSettings/EditorBuildSettings.asset`: Entrance first, followed by Gallery, PaintedBedroom, and StarryNight; disabled SampleScene and Bedroom_StyleExperiment entries removed. Saved by Unity in the tested delivery.
- `ProjectSettings/ProjectSettings.asset`: Standalone adds `PHOTON_REALTIME_4`, initialized by Photon for Editor compilation.
- Packages, XR setup, Android minimum API 32, GameActivity and Meta Quest Support remain as on main.

Validation commands:
- `bash scripts/ci-local.sh`
- Unity batch `-executeMethod InnerFrame.StarryNight.SN_Checks.RunAll -quit -snLogDir <folder>` (Editor suites; explicitly skips extra Play-mode suites)
- Unity batch `-executeMethod PlayerMenuCheck.RunBatch -menuShots <folder>`
- Unity batch `-executeMethod ConversationDeliveryCheck.RunBatch`
- Unity batch `-executeMethod InnerFrame.Entrance.Editor.EntranceAudioGoldChecks.Run` (no `-quit`; runs 44 Editor Play-mode assertions, including direct title-to-gold, Gallery reveal/audio and Skip; add `-verifyGoldBudget` to retain the network session and exercise the 15 s budget)

See `verification/` for test evidence, review notes, and the recorded desktop walkthrough. The video records the initial Gallery → Starry Night → B → menu → Gallery test; the subsequent instruction-preservation correction is covered by regression assertions. Keyboard recording/send/error recovery was exercised, but the local AI proxy was unavailable and no AI answer was obtained. Photon authentication was unavailable; multiplayer is not claimed. No physical Quest/controller validation, deployment, or merge to main.
