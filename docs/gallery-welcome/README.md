# Gallery welcome sign

Owner/integration: Rowling's Controls feature; Gallery layout and the shared in-world sign task (#164) are Cici's. The visitor-facing sign was approved by Rowling on 2026-11-18. This change spawns a separate prefab at runtime; it does not save Gallery.unity or replace the original caption model.

## Experience

- One pale yellow, painted wooden sign at the Gallery entrance. Its pose is derived once from Spawn_Point. It stays in the world, with a solid base; neither head motion nor UI dragging moves it.
- Home has two clearly framed wooden buttons, **Controls** and **Experience Mode**: pale yellow wood, hand-painted gold borders, oil-painted emblems and the existing Caveat Brush font. Their generous 455 × 340 hit areas remain. Explore Freely, Follow Vincent, Back and Begin use the same sliced wooden-button sprite, with hover/press colour feedback.
- Controls opens the complete English controller image as a separate, enlarged, movable world-anchored panel in front of the visitor (1.2m wide, initially 1.3m away). It never replaces the wood's face. Its upper-right painted × closes it in the same Gallery. Menu > Controls opens the identical panel. There is no Previous/Next/Continue sequence and no automatic overview or Move popup on arrival.
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

### Verified 2026-11-18

Unity 6000.3.23f1 `GalleryWelcomeChecks.RunAllBatch` completed with exit code 0 after importing the approved gold-rim buttons. Actual Gallery Play Mode: **34 passed, 0 failed** using native simulated XRI input. Editor regressions: Controls 139/0, world panels 45/0, player menu 117/0, transitions 44/0, Vincent lead 15/0. Home, mode and independent overview screenshots were visually inspected for complete lettering, painted gold borders and a visible close control. Button mipmaps, sliced borders, alpha, reference integrity and LFS attributes checked.

One pre-existing transition test incorrectly assumed it was not starting in Gallery; its interruption now chooses a genuinely different scene. Production transition behavior was unchanged. No Quest build, fresh-checkout validation, live voice-service or multiplayer test is included. No commit or push was performed for this sign change.

### Recorded walkthrough, 2026-11-19

**InnerFrame > Gallery Welcome > Record tested walkthrough** runs the same native XRI checks with visible diagnostic pointing, readable pauses and smooth simulated head motion. It captures actual camera frames under `Logs/GalleryWelcome/Recording-<UTC timestamp>` with step subtitles, frame provenance and results. It neither saves scenes nor substitutes a mock UI. The recording is silent, fixed-step 24 fps, and must not be treated as Quest input, live voice, multiplayer or performance evidence.

The recorded run passed **34/34** checks and produced **1,339 frames (55.79 seconds)**. The exported video has English step labels and a permanent Unity/simulated-controller label. Capture cleanup also restores rendering/timing state and clears pending flags after cancellation or write errors; the subsequent ordinary Play Mode regression passed 34/34. The MP4 and result text are in the parent workspace's `test-videos/gallery-welcome-2026-11-19` folder, outside the repository.
