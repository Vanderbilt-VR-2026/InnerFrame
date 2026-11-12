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
- Unity batch `-executeMethod InnerFrame.Entrance.Editor.EntranceAudioGoldChecks.Run` (no `-quit`; runs 60 Editor Play-mode assertions, including direct title-to-gold, Gallery loop/wrap, voice priority, scene changes and Skip; add `-verifyGoldBudget` to retain the network session and exercise the 15 s budget)
- With a local Convai key and a valid 16 kHz mono question WAV: `CONVAI_TEST_WAV=<file> Unity -batchmode -projectPath <project> -executeMethod ConvaiDirectTest.RunFromCommandLine` (no `-quit`; verifies a real voiced answer and music gain)

See `verification/` for test evidence, review notes, and the recorded desktop walkthrough. The video records the initial Gallery → Starry Night → B → menu → Gallery test; the subsequent instruction-preservation correction is covered by regression assertions. The historical video exercised keyboard recording/send/error recovery while the local AI proxy was unavailable. A later live Convai check with a local key and a synthetic spoken question succeeded: 5.3 s of reply audio, with BGM gain at 0.10 (`verification/convai-voice-priority.txt`). That check did not capture the microphone or test physical controller input. Photon authentication was unavailable; multiplayer is not claimed. No physical Quest/controller validation, deployment, or merge to main.

Gallery music: selected warm-strings B arrangement, 36-second stereo PCM loop (`GalleryWarmStringsLoop.wav`). The gold cue transitions into the existing arrival cue; after 2.81 s it crossfades into the loop over 2.4 s. Direct Gallery startup and returns fade into the loop over 1.2 s; leaving Gallery fades it out over 0.6 s. Editor: Tools > InnerFrame > Preview Gallery Music from Entrance drives the existing entrance buttons, then leaves Play running for listening. Imported PCM seam and real playback wrap, painting silence and Gallery return are covered by `verification/gallery-bgm.txt`.

Voice-first music policy: all Entrance/Gallery music sources share the director's speech-priority gain. During Vincent's scripted/AI/remote speech or a local recording/request, music falls to 10% (-20 dB) with an 80 ms attack, stays low for 350 ms after speech, then recovers over 1.2 s. Voice gain is unchanged. Future scene BGM must use this same priority behavior and test speech, recording, short gaps, failures and scene changes before delivery. Keep ConvaiApiKey.txt and its .meta local and excluded from all source-control pushes and share archives.
