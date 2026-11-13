# Controls verification — 2026-11-12

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
