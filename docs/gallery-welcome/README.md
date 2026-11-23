# Gallery welcome sign

Owner/integration: Rowling's Controls feature; Gallery layout and the shared in-world sign task (#164) are Cici's. The visitor-facing sign was approved by Rowling on 2026-11-18. This change spawns a separate prefab at runtime; it does not save Gallery.unity or replace the original caption model.

## Experience

- One pale yellow, painted wooden sign at the Gallery entrance. Its pose is derived once from Spawn_Point. It stays in the world, with a solid base; neither head motion nor UI dragging moves it.
- The runtime stand is 0.71 of the source model (about 0.85 m wide). It sits 1.45 m inward and 0.85 m to the left of Spawn_Point so the existing 2 m controller rays can reach both buttons on arrival. The painted UI follows the inset's slight tilt and sits less than 1 mm above its wood surface, behind the carved rim.
- Home has two clearly framed wooden buttons, **Controls** and **Experience Mode**: pale yellow wood, hand-painted gold borders, oil-painted emblems and the existing Caveat Brush font. Their generous 455 × 340 hit areas remain. Explore Freely, Follow Vincent, Back and Begin use the same sliced wooden-button sprite, with hover/press colour feedback.
- Controls opens the complete English controller image as a separate, enlarged, movable world-anchored panel in front of the visitor (1.35m wide, initially 1.15m away). It never replaces the wood's face. Its upper-right painted × closes it in the same Gallery. Menu > Controls opens the identical panel; when launched there, × returns to the Controls menu. The Menu button backs out of the guide to Controls, and a second press closes Menu without restoring a hidden guide. There is no Previous/Next/Continue sequence and no automatic overview or Move popup on arrival.
- Opening Experience Mode leaves the sign in place. Walking away from that page does not keep the game modal or block painting selection.
- **Explore Freely** leaves painting selection unrestricted and suppresses the automatic Follow Vincent invitation at completed Bedroom/Starry exits. Story interactions and B voice conversation remain available. Return uses the existing player menu.
- **Follow Vincent** stores a local, session-only choice. The sign's Begin uses the existing SceneFader to open the first unfinished ready stop from VG_StoryOrder. Existing Bedroom and Starry exit flows report completion only after their actual Over predicate succeeds and retain their existing follow/back choice.
- Merely entering or leaving a scene does not complete it. No shared network mode, no new multiplayer state, no PlayerPrefs persistence.

## Pending team integration

The global mode choice relates to #162 (Sonya/Rowling), and the physical shared signs to #164 (Cici). Current source has completion adapters only for Bedroom and Starry Night. Café (#217/#206) and Almond have no completion adapter here. The itinerary therefore must not be described as a complete four-scene guided tour. A pending Café completion must not be inferred from reaching its door or entering the scene. Route destinations come from story-order scene IDs, so #226's changing visible Act 4 painting title is not hardcoded.

Existing branch overlap with open PR #226: GalleryPaintingMenu.cs, SceneFader.cs, docs/gallery/README.md. Existing docs/ASSETS.md overlap with #205. This change does not edit those scene/transition files; merge coordination remains necessary for the pre-existing branch changes. Published to draft PR #198 on 21 November; not merged into main.

## Build / inspect

Unity 6000.3.23f1, Android profile. Run **InnerFrame > Gallery Welcome > Build assets** after changing the model, then **Open Gallery to try**. Asset setup creates materials/prefab and importer settings through Unity APIs. No hand-written Unity YAML.

Blender source: Art/Source/GalleryWelcome/build_welcome.py reads the original Art/Source/VanGogh/Caption/VG_Sign.blend and saves a separate source/model. It extends the inset while preserving corner blocks, decimates the wooden frame and adds a walnut stand. 2,848 triangles total; four mesh renderers; two existing-style material assignments. Frame texture 2048 square; overview 1536×1024; painted icon atlas 1774×887; wooden button 1254 square. Android ASTC 6×6 with mipmaps. No new lights or third-party downloads. Both new painted assets were made with built-in image generation; prompts are in Art/Source/GalleryWelcome.

## Checks

**InnerFrame > Gallery Welcome > Test in Play Mode** drives a simulated IUIInteractor through the real XRUIInputModule. It covers native press/release routing, Controls and ×, both mode choices, world pose across live head-motion frames, actual Begin/return scene loads, no invented completion and one sign after return. Screenshots/results: Logs/GalleryWelcome. Separate shared-controls and transition regressions remain applicable.

Quest performance, real hardware controller input, live Vincent voice and multiplayer require separate device validation. Editor results are not headset acceptance.

### Quest feedback, 2026-11-21

The user reported an oversized board, buttons/text floating in front of it, and unsuccessful button clicks. Source inspection identified the previous UI plane at z=-0.14 m while the actual inset is approximately z=-0.0208 m: roughly 12 cm of separation. The previous entrance placement was about 2.4 m from Spawn_Point, beyond the Gallery's existing 2 m controller rays. The compact runtime placement addresses these two concrete causes without editing Cici's Gallery scene or increasing global controller reach.

The earlier 34-check suite uses a custom `IUIInteractor` with rays extended to each target, so it did not cover this arrival-distance failure. **InnerFrame > Gallery Welcome > Test native controller from arrival** (`GalleryWelcomeChecks.RunNativeBatch`) now drives the real scene's NearFarInteractor and Trigger binding using simulated tracked devices, keeps the rig at Spawn_Point, and checks both mode choices and the independent Controls overview. It also measures the paint gap against a temporary collider made from the imported frame mesh and captures front/oblique views. This remains an Editor simulation; the revised sign requires Quest acceptance.

Native controller reproduction also exposed a shared click-routing bug: the parent `VG_PanelInteraction` advertised a zero-threshold drag handler even when a child Button owned the press, or when the sign could not move. Slight hand movement made XRI cancel the button click as a parent drag. The shared handler now leaves child-button presses alone and never claims a drag on a fixed sign. Native checks include 6 mm of aim drift within the same button. The separate `RunNativeDialogueBatch` regression uses Starry's real L01/L02 caption after approaching Vincent, before opening Menu; it checks the close cross and the outer part of its 55 mm hit area through the original NearFar Trigger binding.

Unity 6000.3.23f1 graphical Play Mode on the isolated test clone passed **20/20** native Gallery checks: imported width 0.850 m, both painted controls 0.55 mm above the actual wood mesh, Controls hit at 1.69 m, Experience Mode at 1.64 m, independent overview/close and both mode selections. Front and oblique camera captures were visually inspected. The separate shared-close regression reproduced **6 passes / 2 failures** with the published runtime (actual Trigger performed, parent `Panel` began dragging), then passed **8/8** with the fix (actual `pointerClick: Dismiss`, no stolen drag). Both runs verified visible captions before attempting input. These are simulated tracked-device tests, not new Quest acceptance. Evidence is in the parent workspace's `deliverables/quest-feedback-2026-11-21/native-ui` folder; no scene or prefab was changed.

The additional `GalleryWelcomeChecks.RunNativeMenuFlowBatch` passed **29/29** in graphical Play Mode with the latest caption, Menu and guide changes. It operates the native left Menu binding and right NearFar Trigger, including 6 mm hand drift: guide × returns to Controls, Menu × clears the entire modal stack, subtitles become visible again, and Menu-button back/close does not retain a hidden guide. It also places a draggable caption explicitly across the right-hand lantern ray (Trigger correctly blocked), then beside it with the left native pointer on the caption and the right pointer on the lantern (the actual lantern dims). `SN_Game` now distinguishes a global open/closing modal from UI under only one hand; this does not permit that hand to click through a caption. The report and two inspected camera captures are `native-menu-flow-checks.txt`, `native-menu-controller-guide.png` and `native-controls-return-menu.png` in the same evidence folder. An unrelated Unity Editor Search startup-indexing exception was logged; the Play test itself finished normally with exit code 0. Quest acceptance is still separate.

### Verified 2026-11-18

Unity 6000.3.23f1 `GalleryWelcomeChecks.RunAllBatch` completed with exit code 0 after importing the approved gold-rim buttons. Actual Gallery Play Mode: **34 passed, 0 failed** using native simulated XRI input. Editor regressions: Controls 139/0, world panels 45/0, player menu 117/0, transitions 44/0, Vincent lead 15/0. Home, mode and independent overview screenshots were visually inspected for complete lettering, painted gold borders and a visible close control. Button mipmaps, sliced borders, alpha, reference integrity and LFS attributes checked.

One pre-existing transition test incorrectly assumed it was not starting in Gallery; its interruption now chooses a genuinely different scene. Production transition behavior was unchanged. No Quest build, fresh-checkout validation, live voice-service or multiplayer test is included. No commit or push was performed for this sign change.

### Recorded walkthrough, 2026-11-19

**InnerFrame > Gallery Welcome > Record tested walkthrough** runs the same native XRI checks with visible diagnostic pointing, readable pauses and smooth simulated head motion. It captures actual camera frames under `Logs/GalleryWelcome/Recording-<UTC timestamp>` with step subtitles, frame provenance and results. It neither saves scenes nor substitutes a mock UI. The recording is silent, fixed-step 24 fps, and must not be treated as Quest input, live voice, multiplayer or performance evidence.

The recorded run passed **34/34** checks and produced **1,339 frames (55.79 seconds)**. The exported video has English step labels and a permanent Unity/simulated-controller label. Capture cleanup also restores rendering/timing state and clears pending flags after cancellation or write errors; the subsequent ordinary Play Mode regression passed 34/34. The MP4 and result text are in the parent workspace's `test-videos/gallery-welcome-2026-11-19` folder, outside the repository.
