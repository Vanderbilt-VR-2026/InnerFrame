# Controls verification — updated 2026-11-23

## Current world-fixed panels and Quest Grip acceptance

This section supersedes the historical camera-relative placement experiments below.
Current dialogue, hints and choices remain in the world. Grip on their visible body,
words or name tab moves them in depth and rotation; release leaves the new pose fixed.
Menu retains its title-bar handle. The original black/gold UI is retained.

- [V] Published implementation commit `b36950e` has exactly the same Git tree as the
  tested snapshot `321bcb9`; all 90 task files also matched the post-build source
  manifest. Publication adds only documentation after that implementation commit.
- [V] Unity 6000.3.23f1 final checks on 2026-11-22: simulated native controller Grip
  **100 passed / 0 failed**, dialogue recall **22/0**, graphical surface **11/0**,
  world panels **49/0**, and Menu **120/0**. Pre-build Controls checks **153/0**,
  physical guidance **133/0**, and scripted Starry story **60/0** also passed.
- [V] The controller suite exercises the actual Input System/XRI binding and pose
  drivers, with story progression frozen for the fixture. It covers central text,
  edges/corners, the name tab, both hands, rear pickup, depth/rotation, release,
  the real close button and negative controls. These are simulated controllers,
  not 100 physical-headset checks.
- [V] Surface renders verify readable front faces, hidden rear text and preserved
  TMP font material. Recall checks cover the current scene's spoken caption,
  retained page/audio state and Subtitles Off. They do not establish headset
  legibility or user comfort.
- [V] The private Android ARM64 IL2CPP/GameActivity build completed in **4:30**,
  with **0 errors and 11 warnings**. It repacked player data and all six enabled
  scenes, with Starry Night first only for this private test build.
- [V] On 2026-11-22, the installed APK hash matched the built APK; Quest 3 entered
  focused immersive OpenXR. Device logs recorded three central-face Grip/release
  interactions. The user confirmed: “可以移動，放手後固定” (can move it; it stays
  fixed after release). This accepts central-caption movement and release only.
- [V] On the earlier private Starry build, holding physical right B, speaking and
  releasing produced a new streamed answer; the user confirmed both voice and
  subtitles. Some speech was misrecognized. This was not repeated on the final
  whole-face Grip APK. The earlier total-silence incident resolved after a Quest
  reboot with the same APK; it is not evidence of a repository audio-code fix.
- [V] Two Bedroom clearance failures remain in the follow/carry run (**46/2**):
  right foot/easel contact and right toe/wall contact near the doorway. Both also
  reproduce in original-speed control runs. Those controls isolate speed; they
  are not a pristine-main baseline.
- [I] These builds used an isolated existing clone with an explicit source overlay
  and warm import/native/shader caches. Fresh-checkout Unity validation, complete
  six-scene Quest acceptance, multiplayer, performance, and final-build perceived
  audio/readability remain pending. Keep PR #198 in draft.

Local evidence (outside Git): `deliverables/dialogue-grip-2026-11-22/whole-face-fix/`
contains `verification.json`, `source-manifest.json`, `native/native-grip-checks.txt`,
`recall-edit.txt`, `surface/surface-checks.txt`, `world-panels.txt`, `menu-checks.txt`,
`build-report.txt` and `device-grip-trial.json`. The earlier voice acceptance is in
`deliverables/starry-reading-audio-2026-11-21/acceptance-summary.txt`; Bedroom clearance
reports are in `deliverables/quest-feedback-2026-11-21/follow-graphics/`. These paths
are relative to the local project workspace, not the repository. Logs, private APKs
and credentials are intentionally excluded from publication.

### Publication and teammate overlap

- [V] On 2026-11-23, fetched `origin/main` remained at `418dbc6`, already included in
  this branch. No new teammate source commits were found since the previous check.
- [V] Merge-tree checks against all other open PR heads found conflicts with #205
  in `docs/ASSETS.md` and #226 in `GalleryPaintingMenu.cs`, `SceneFader.cs` and
  `docs/gallery/README.md`. #189 and #191 had no textual conflicts. These PRs were
  inspected, not merged; agree merge order with their owners before landing.
- [V] Independent audits checked runtime integration, source/test correspondence,
  new metadata/GUID references, asset licenses, LFS and secret exclusion. No public
  upload blocker was found. Local generated-font, render-pipeline and project
  settings edits remain outside this upload.
- [I] The final pre-push and remote CI results are recorded in Draft PR #198 after
  running them on the published commit; the earlier passing run is not substituted
  for that final check.

## Historical placement experiments — 2026-11-13

## Shared lower-view placement in every scene

This supersedes the painting-only policy below. Shared dialogue and hint presenters now default to immediate
camera-relative placement in Entrance, Gallery and every painting. Gallery first-use hints retain their real
eligibility/completion rules but no longer use world anchors or distance scaling. Full Controls inherits the same
placement; player menus, painting detail/Enter panels and physical labels retain their separate interaction rules.

- [V] Unity 6000.3.23f1 recompiled the runtime and Editor changes with Android selected in the existing checkout.
- [V] Final `DialoguePlacementPreview` `all-checks` run: **272 assertions passed, 0 failed** at
  2026-11-13 19:05:09 America/Chicago (2026-11-14 01:05:09 UTC): 64 placement/caller assertions,
  158 Controls assertions, 19 Starry Night assertions and 31 Wayfinding assertions. All four suites and the
  original saved-scene restoration passed. The runner never saves scenes or prefabs.
- [V] Placement fixtures exercise actual caption and reused tutorial presenters in all five named build-scene
  contexts, nonempty visible text/art geometry, zero-time yaw/pitch/roll/translation, before-render refresh,
  camera scale, unchanged shared artwork, and speech priority. Controls opens the five actual build scenes;
  Starry Night uses its actual bootstrap and layout. These are Editor fixtures, not five full Play Mode runs.
- [V] Wayfinding checks the real Gallery has zero legacy pedestal prompts, then exercises synthetic legacy
  entrances: hover/distance arbitration, one visible prompt, disabled/other-scene exclusion, and immediate
  yielding. Arrival checks advance its real initialization coroutine, including waiting for and yielding to
  another instruction; elapsed waiting durations are not measured.
- [V] Native before/after renders were inspected: the scene markers change after camera movement while the
  shared black/gold text and shapes retain their viewport positions. Reports and images are in
  `Logs/DialoguePlacement/`; Controls details are in `Logs/Controls/checks.txt`.
- [V] The real Gallery was opened in Play Mode and its actual Move prompt was visually inspected in the lower
  view, including a desktop view adjustment. No live preview sample was spawned. Unity is left in that scene.
- [V] The first expanded Wayfinding reruns exposed an Editor restriction on adding scenes from an unsaved
  Untitled fixture. The final test uses an isolated in-memory copy of the saved Gallery and restores the
  original scene setup; the final complete run above includes that correction.
- [I] No fresh-Library import, APK build, Quest 3 or multiplayer acceptance was performed for this update.
  Native headset comfort, Controls ray targeting and late head-pose timing still need physical-device testing.
  The live Gallery still reports the pre-existing Fusion InvalidAuthentication warning.

Repeat with `InnerFrame > UI > Check all shared dialogue callers`, or write `all-checks` to
`Temp/DialoguePlacementPreview.request`. `render` separately regenerates the placement images. Both run only
after leaving Play Mode and reject dirty scenes; `all-checks` also requires saved scene paths for restoration.

## Immediate lower-view dialogue in paintings

The painting caption, story-hint, arrival-hint and first-use presenters opt into the shared UI's runtime
`LockToView` mode. It keeps the existing black/gold artwork, fonts, sizes and paging, with an immediate
camera-relative pose including yaw, pitch, roll and translation. Gallery context anchors and the full interactive
Controls panel retain their existing placement behavior. This supersedes the older Bedroom world-anchor
placement evidence below.

- [V] Unity 6000.3.23f1 compiled the changed runtime and Editor scripts with Android selected in the existing
  checkout (runtime assembly timestamp 2026-11-14 00:15:13 UTC / November 13 in Chicago).
- [V] `DialoguePlacementPreview` render check: **35 passed, 0 failed**, 00:15:23 UTC. Actual visible UI/text mesh
  coordinates remain fixed after a single zero-delta-time step through yaw, extreme pitch, roll and translation.
  Shared sprites/font and the default legacy behavior are also checked.
- [V] Integration fixtures call actual `VanGoghCaption.Make` in named painting/Gallery contexts, reuse the same
  TutorialManager view across placement modes, and verify speech hides the pending tutorial immediately and
  allows it to resume. These are Editor fixtures, not physical controller or AI tests.
- [V] Native Unity images `Logs/DialoguePlacement/Images/locked-before.png` and
  `locked-after-yaw-pitch-roll.png` were visually inspected. The background changes while the UI retains its position.
- [V] Split-menu render regression: **100 passed, 0 failed**, 00:15:37 UTC.
- [V] PaintedBedroom was opened in Play Mode and visually inspected with an explicitly labelled `UI preview`
  sample from the actual shared prefab. The sample sends no microphone/AI request or completion event.
  The general desktop view was restored after trying its painter-view camera.
- [V] Runtime/lifetime review checked cross-scene mode reset, before-render callback cleanup and unchanged
  subtitle/session rules. Arrival now yields immediately to existing speech or instructions; it does not leave
  a fading overlay over newly started dialogue.
- [V] `ControlsTutorialChecks` now uses an actual PaintedBedroom-named fixture for the new policy, retaining
  Gallery anchor assertions. It compiled, but that entire batch suite was not rerun in this update.
- [I] No fresh-Library import, APK build, Quest 3, live AI/microphone or multiplayer acceptance was performed.
  The live scene reports the existing Fusion TickRate and InvalidAuthentication warnings; it is not a successful
  room-join test.

Repeat via `InnerFrame > UI > Check locked dialogue` / `Render locked dialogue`, or write `check` / `render`
to `Temp/DialoguePlacementPreview.request`. `live` opens saved PaintedBedroom in Play Mode with a clearly labelled
sample; `stop` removes that sample and restores the temporarily hidden shared UI. Nothing saves scene or prefab YAML.
The sample also clears on Play Mode exit. Generated reports/images are local under `Logs/DialoguePlacement/`.

## Earlier Gallery integration

Merged `origin/main` at `ce62132dd466f1aeb7bb68d8e313f17e9147bf85` (#199) into `rowling/controls-first-use-tutorial`, merge commit `ece05f0`. No history rewrite. Unity 6000.3.23f1, macOS, Android selected; the original separate checkout was reused.

| Executed check | Result | Local evidence |
| --- | --- | --- |
| C# compilation and final Editor integration suite | **140 passed, 0 failed**; no compiler errors | `ControlsTutorialChecks.RunBatch`, `Logs/Controls/checks.txt` |
| Controls Play Mode lifetime/transition suite | **8 passed, 0 failed**, including 3 synthetic A-input assertions | `ControlsTutorialPlayChecks.RunBatch`, `Logs/Controls/play-mode.txt`, `input.txt` |
| Existing Player Menu suite | **22 passed**, ALL PASSED | `PlayerMenuCheck.RunBatch`, `/tmp/innerframe-menu-update.log` |
| Gallery Play Mode suite | **148 passed, 0 failed**, 38 images | `InnerFrame.Gallery.EditorTools.Tests.GalleryLayoutPlayCheck.RunBatch`, `Logs/GalleryUpdate/raw/play-check.txt` |
| Gallery decor suite | **96 passed, 0 failed**, 9 rendered views | `InnerFrame.Gallery.EditorTools.Tests.GalleryDecorCheck.RunBatch`, `Logs/GalleryDecor/` |
| Starry Night menu input suite | **9 passed, 0 failed**, 6 images | `InnerFrame.StarryNight.EditorTools.SN_MenuInputCheck.RunBatch`, `Logs/GalleryUpdate/StarryNight/raw/sn-menu-input-check.txt` |
| Controls rendering | **17 images**: eleven guide pages, menu, five first-use fixtures; changed Teleport/painting pages and Vincent view visually checked | `ControlsTutorialChecks.RenderBatch`, `Logs/Controls/Images/` |
| Gallery asset preservation | Scene blob `a0793f1a1a50e43aaa6933619b8b0e1792807274` equals main; serialized layout and artwork assets unchanged; LFS assets hydrated | Git diff/hash comparison and independent asset review |
| Physical device availability | `adb devices -l` listed no devices | No Quest or APK test performed |

The final Editor suite opens the real updated Gallery without saving it. It checks the five-entry hanging order, four runtime-bound painting adapters, matching destinations, the self-portrait exclusion, a real `GalleryPaintingPointer.Point` physics raycast, and completion only after the corresponding visible menu opens. Negative cases cover hover without a trigger, disabled pointer with stale hover, another painting's menu, a hidden menu, and no repeat after completion. It publishes the raycast's hover set as the normal Update does; it is an Editor fixture, not a controller-input claim. The separate Gallery Play suite drives XRI simulated HMD/controllers through actual hover, Trigger, Enter, Back, scene changes, near/far placement and Coming soon interactions for all four paintings.

The new regressions use the real Bedroom Vincent prefab and the new Gallery portrait spawner. They verify head/portrait gaze, visible complete dialogue/key hint, all UI corners on the correct side of the caption anchor, angular scaling with distance, and idle B reminder → first-use lesson → restored idle reminder. Menu-open → Entrance-open promotes the existing guide to Continue, retains its callback on a later Menu open, and continues once when closed. Every guide page must retain all three active navigation buttons and fit within a 60-degree view. An eye coincident with a hint anchor cannot accumulate a placement offset.

The Starry Night test covers the Player Menu using simulated XR input, including a positive control with the menu closed. Controls contributes to the same `PlayerMenu.MenuOpen` flag (separately asserted by the Controls tests); the complete headset Controls → lantern interaction remains a hardware acceptance item.

### Review fixes and remaining coordination

Three independent review angles covered events/session lifetime, UI/continuation behavior, and assets/test strength. The Cici review's idle-B suppression, easel/portrait anchor and lost Entrance callback defects are fixed and regression-tested. Her Starry Night input guard is included from main. Review also identified two potentially vacuous assertions; explicit active-button and visible-dialogue/nonempty-bounds assertions now cover them. No Gallery rebuild was run: `GalleryLayoutCheck.RunBatch` invokes `GalleryLayoutSetup.Apply`, so verification used the read-only decor suite, Play suite and integration fixtures instead.

The complete existing Vincent dialogue style and one-app-session completion policy are retained as requested. Companion voice exchange still needs its real integration; it is an adapter only. Owner review is still needed for fixed-furniture Grip eligibility, demo-visitor reset policy, menu/design choices, and the one-off asset preparation tool/unused controller artwork. Starry Night's different Humanoid caption anchor remains a future integration question; ordinary story hints currently take priority over first-use there. No teammate message was sent.

Successful assertions do not mean a clean interactive Console: Play logs still contain OpenXR runtime-unavailable diagnostics on this Mac and Unity Editor SearchDatabase startup exceptions. No APK, physical Quest session, microphone/AI request, multiplayer voice exchange or performance measurement was performed. The full Entrance → intro walkthrough and in-headset placement/readability remain unverified. The new runs use the existing isolated checkout, not another clean-library checkout. The final pre-push/remote CI and open-PR merge checks are recorded in the Draft PR.

## Historical baseline — 2026-11-12

The results and pending integration notes below describe the original `2cc17ea` implementation before #199; the latest section above supersedes its Gallery/Starry Night status and test counts.

Branch: `rowling/controls-first-use-tutorial`, based on `origin/main` at `998dacdd22dc1b53cc18d4d77ca8a44cd80e20e1`. Unity 6000.3.23f1 on macOS, Android target. Work used a separate checkout with a newly imported Library; the existing open Unity project was not modified.

## Executed

| Check | Observed result | Evidence |
| --- | --- | --- |
| Unity import and C# compilation, Android selected | Passed; no `error CS` compiler diagnostics | Initial import, asset preparation and subsequent batch invocations completed successfully |
| Legacy tutorial reference scan | No dependencies, GUID consumers or literal runtime loads found; removed through `AssetDatabase.DeleteAsset` | `ControlsTutorialAssets.Prepare`; `Logs/Controls/legacy-references.txt` |
| Editor integration suite | **89 passed, 0 failed** | `ControlsTutorialChecks.RunBatch`; `Logs/Controls/checks.txt` |
| Play Mode input fixture | **3 passed**: positive control receives the simulated right A press; Jump does not; no other shared action binds A | `ControlsTutorialChecks.RunInputInPlayMode`; `Logs/Controls/input.txt` |
| Play Mode lifetime/transition suite | **8 passed, 0 failed**, including the input fixture result | `ControlsTutorialPlayChecks.RunBatch`; `Logs/Controls/play-mode.txt` |
| Existing Player Menu suite | **22 passed**, `ALL PASSED` | `PlayerMenuCheck.RunBatch`; Unity log |
| UI renders | 16 images produced: ten guide pages, menu and five first-use views; reviewed for shared styling, text fit and button circles | `ControlsTutorialChecks.RenderBatch`; `Logs/Controls/Images/` |
| Device availability | `adb devices -l` listed no devices | Local Android SDK platform tools; no physical Quest test performed |

The 89 Editor assertions cover all five enabled scenes: Entrance, Gallery, PaintedBedroom, StarryNight and CafeTerrace. Each has one XR Origin, a disabled JumpProvider and no missing scripts. They invoke the actual Controls/Close button listeners and Menu toggle, verify the same active scene, inspect the exact shared Vincent sprite/font/settings references, and check all ten guide pages fit one three-line page with navigation inside a 60-degree camera view. XRI selection and Vincent answer events are simulated; these are not physical controller or live AI tests.

Arbitration checks include one prompt for two available lessons, fixed world placement when turning the head, the 1.9 m / 2 m boundary, immediate suppression by newly created Vincent speech UI, suppression by mirror instructions, recovery when the higher-priority hint ends, actual pickup event subscription, unfinished painting/voice adapter gates, gaze/distance gating, empty versus nonempty answers, and session reset. Dragging a menu title bar is explicitly a negative pickup test.

Play Mode uses temporary scenes created and removed through Unity. It tests the automatically created manager, an actual Single scene load, preservation of the same manager and completed lesson, withdrawal of old contextual prompts, and Controls opening/closing after the transition. The fixture removes `NetworkSession` before its deferred join; it does not exercise multiplayer. Synthetic input is processed as a player update, with a separately bound positive-control action so a missing input event cannot make the Jump test pass vacuously. Temporary input focus settings are restored.

## Limits of the run

The final Play Mode process exited successfully and its assertions passed, but its log also contains OpenXR loader diagnostics (`XR_ERROR_RUNTIME_UNAVAILABLE` on this Mac without a headset runtime) and an Editor `UnityEditor.Search.SearchDatabase` startup `ArgumentOutOfRangeException`. A clean interactive Play Mode Console is **not** claimed. Earlier input-fixture attempts failed because Editor updates did not deliver player actions, and one settings-copy attempt failed on restoration; those fixture issues were corrected before the successful run above.

No Android APK build, physical Quest session, immersive display check, controller-ray click, live microphone/Convai request, multiplayer voice exchange or frame-rate measurement was performed. The Entrance continuation callback is checked with a fixture; the complete Entrance → intro video flow still needs a user walkthrough. See the headset acceptance list in `README.md`.

## Review and coordination

Three independent review angles were used: completion events/session lifetime, shared UI/menu integration, and assets/input/reference integrity. The parent also checked rendered layout and claim accuracy.

| Finding | Verdict | Evidence / resolution |
| --- | --- | --- |
| Dragging Player Menu could complete Bedroom Grip | **CONFIRMED, fixed** | Runtime binding now excludes grabbables containing or below a Canvas. The PaintedBedroom menu selection negative test passes; the event reviewer rechecked the fix. |
| Full-guide navigation was clipped at the bottom of a 65-degree render | **CONFIRMED, fixed** | Moved the accessory button row closer to the existing box. Added a 60-degree viewport assertion for every page; all ten pass. |
| Shared input serialization changed other controls | **REFUTED** | Semantic comparison found only removal of RightHand PrimaryButton → Jump. Added `version: 1` and default `priority: 0` fields are Input System serialization defaults. |
| Legacy prefab might still be loaded at runtime | **REFUTED for this checkout** | Full dependency/GUID/literal-load scan before deletion; the separate Entrance guide GUID remains unchanged. |
| Remote Vincent speech or an empty reply completes the B lesson | **REFUTED** | Existing local `Answered` call sites and the adapter's nonempty-reply gate; synthetic empty/success event checks. |
| First-use UI replaces or redesigns Vincent's style | **REFUTED** | It instantiates `Assets/Shared/UI/Prefabs/VG_DialogueUI.prefab`; the same box, name tab, font, stars, key circle and settings are retained. |

The two other open PRs at the final coordination check were #189 (Bedroom daylight) and #191 (Café environment). The branch uses merged Player Menu #181 and shared UI #184. Final merge-tree/pre-push results are recorded in the Draft PR after committing.

Pending integration is intentional: Cici's future Gallery painting Trigger menu must call the painting adapter only after a menu actually opens; the voice owner must supply a confirmed voice exchange. Neither adapter is attached automatically. Cici's separate Starry Night trigger-through-menu fix is still needed for complete scene-input blocking while Controls is open. No Gallery scene layout, Sonya conversation implementation or Charlyne scene asset was rewritten.

## Reproducing

Run the following entry points in separate Unity 6000.3.23f1 invocations with `-batchmode -projectPath <checkout> -buildTarget Android -executeMethod <entry-point> -logFile <log>`. Use `-nographics` for the checks, omit it for renders. These methods exit with a result code; do not add `-quit` to the asynchronous Play Mode suite.

1. `ControlsTutorialChecks.RunBatch`
2. `ControlsTutorialPlayChecks.RunBatch`
3. `PlayerMenuCheck.RunBatch`
4. `ControlsTutorialChecks.RenderBatch`

`ControlsTutorialAssets.Prepare` is only needed when regenerating the assets; add `-quit` to that invocation. After committing, run `bash scripts/ci-local.sh` before pushing. Local reports, logs and screenshots are excluded from source control by repository policy.
