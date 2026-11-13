# InnerFrame controls and first-use teaching

Updated 2026-11-13 from Cici's five-page **InnerFrame controls: one button, one job** (2026-11-12), with Gallery #199 merged from `main` at `ce62132`. Jump is disabled and the right controller's A button remains free.

## Quest controls

| Control | Action |
| --- | --- |
| Left stick | Move |
| Right stick left / right | Smooth Turn |
| Right stick forward | Where teleport is available, aim and release at a valid floor destination |
| Either trigger | Pull the trigger to select UI; open a Gallery painting's menu, then choose Enter or Back; select interactions in The Starry Night |
| Either grip | Hold grip to pick up an object or move a menu by its title bar |
| Right Ⓐ | Unassigned; Jump is disabled |
| Right Ⓑ | Hold to ask Vincent, release to send; pressing while he speaks interrupts him |
| Left Ⓧ | Hold to talk to the other player, release to stop |
| Left ≡ Menu | Open / close Player Menu; Controls opens the full guide in the current scene |
| Left Ⓨ | Unassigned |

Gallery uses Cici's new arch, corridor and hanging order: Self-portrait, The Bedroom, Café Terrace, The Starry Night, Almond Blossom. The self-portrait is for Vincent's Ⓑ conversation; the other four paintings have Trigger menus. Almond Blossom shows Coming soon with Enter disabled. The serialized Gallery scene, layout and artwork assets are taken unchanged from #199.

`ControlsCatalog` supplies the full guide and first-use wording. Update this table, the catalog, and other affected hints in the same PR as any binding change. Headset pages contain Quest controls only. In Unity, `{B}`, `{X}`, `{A}` and `{Y}` render using the shared key-circle sprite; the menu bars are drawn inside that same circle. This avoids missing Unicode circled-letter glyphs in the font.

## Full guide and shared UI

`Resources/EntranceControllerGuide` retains its asset GUID. Its component instantiates `Assets/Shared/UI/Prefabs/VG_DialogueUI.prefab`, with the actual Vincent font, box, name tab, star, key circle, materials and settings. Previous / Next / Close use those same assets. The Entrance supplies the existing intro continuation; Player Menu supplies no scene transition. The Menu button can also close Controls.

The first-use view instantiates the same complete dialogue UI. It sets `FollowHead = false` and places the layout in world space, scaling with distance to preserve the shared text's angular size. The Gallery movement prompt stays where it first appears; object prompts use their target's location. Vincent uses the existing caption anchor, with the whole panel below a portrait or above his head; gaze targets his head/portrait, not the easel. Full Controls uses the normal dialogue follow behavior and has eleven pages, including entering a painting. Physical placement and readability require a Quest review.

## First-use rules (PDF page 5)

| Lesson | Availability | Completion evidence | Integration |
| --- | --- | --- | --- |
| Gallery Move | First Gallery visit | 2 m accumulated horizontal camera travel, measured in steps of at least 5 cm | Automatic; a new camera/scene or fade resets the measurement origin |
| Bedroom Grip | Pointing at a scene pickup through an XRI ray | Actual `XRGrabInteractable.selectEntered` or an already active selection | Automatic in PaintedBedroom; excludes menu/UI grabbables |
| Gallery painting | The real Gallery pointer is hovering a menu painting | The matching painting menu is open and visible | Automatic runtime binding to #199's four menu paintings; disabled/stale input cannot complete it |
| Vincent Ⓑ | Looking within 20° at Vincent's head/portrait within 3 world metres | A nonempty local `SpeechToProxy.Answered` response | Automatic for existing Vincent talkers; remote speech, B presses, failed requests and empty end events do not complete it |
| Companion Ⓧ | A second player is present | Confirmed voice exchange | `FirstUseCompanionAdapter`; pending voice integration |

The local `TutorialManager` survives scene changes. It holds a `TutorialSession` in memory; completed lessons stay completed until the next play session. No completion state is saved to disk. Available requests are ordered by first availability; only one can be shown. Unavailable, disabled and destroyed sources are withdrawn. A successful action can complete a lesson before its hint appears.

Vincent's speech, listening/thinking/error states, existing task instructions, arrival/entrance hints, open artwork cards, Gallery painting menus, scene fades and Player Menu take priority. First-use UI hides immediately when blocked, then resumes if still relevant. The idle B reminder in `VanGoghCaption` yields while a first-use request is available and returns afterwards; mirror instructions and conversation state remain owned by Vincent. Existing Starry Night story/B reminders remain owned by its conversation system. If Entrance opens a guide already opened from Menu, it promotes that same guide to Continue and retains the intro callback.

## Connecting unfinished interactions

The current Gallery needs no serialized adapter edits. `TutorialManager.RefreshSceneBindings` binds its `GalleryPainting` components to the existing `GalleryPaintingPointer` and `GalleryPaintingMenu`. The adapter reads published hover state after pointer Update and completes only when that painting's visible menu opens. For future independent scenes, the explicit `SetPointing` / `MenuOpened` adapter contract remains available; only set `triggerMenuReady` when the actual interaction exists.

For the voice owner: add `FirstUseCompanionAdapter`, call `SetCompanionPresent` from actual room membership, and call `VoiceExchangeCompleted()` after a confirmed local voice exchange. An X press alone is not evidence. This unfinished voice adapter is not automatically attached.

Coordination: Player Menu builds on merged #181; shared UI builds on #184. Gallery #199 and Cici's Starry Night `PlayerMenu.MenuOpen` input guard are now included through the merge from main. Keep this PR in draft for owner and headset review; do not rebuild or save Cici's Gallery scene as part of tutorial verification.

## Asset preparation and checks

Unity **6000.3.23f1**, Android target. `InnerFrame > Controls > Prepare assets` (batch `ControlsTutorialAssets.Prepare`) writes prefab changes through Unity. It disables the shared Starter rig's JumpProvider, removes its RightHand PrimaryButton → Jump binding, and checks dependencies, GUID references and literal loads before deleting the legacy `ControllerTutorial.prefab`. The input editor also serializes its current `version: 1` and default `priority: 0` fields; other bindings are preserved.

Run these batch entry points in separate Unity invocations:

| Entry point | What it verifies |
| --- | --- |
| `ControlsTutorialChecks.RunBatch` | All five build scenes, modal Controls and scene-preserving close, eleven-page fit, 2 m threshold, arbitration, real Gallery ray/menu fixtures, Vincent asset placement/idle reminder, simulated XRI/answer contracts and session reset |
| `ControlsTutorialPlayChecks.RunBatch` | Real Play Mode bootstrap, synthetic A input with a positive control, actual Single scene transition and completion persistence, Controls after transition; temporary fixture scenes are created/deleted through Unity |
| `ControlsTutorialChecks.RenderBatch` | Full guide, Player Menu and first-use screenshots (run with graphics enabled; adapter views use fixture requests) |
| `PlayerMenuCheck.RunBatch` | Existing Player Menu regression checks |
| `InnerFrame.Gallery.EditorTools.Tests.GalleryLayoutPlayCheck.RunBatch` | Existing Gallery Play Mode suite, including simulated controller rays and menu actions; use `-gpOut Logs/GalleryUpdate`, with graphics enabled |

Reports go to `Logs/Controls/`; renders to `Logs/Controls/Images/`. These are local verification artifacts, not runtime assets. Run `bash scripts/ci-local.sh` after committing and before pushing.

## Quest acceptance still needed

1. From Entrance, complete or skip Controls and confirm the normal intro continues.
2. In Entrance, Gallery, PaintedBedroom, StarryNight and CafeTerrace, use ≡ Menu → Controls. Click Previous / Next / Close with each ray; closing must keep the same scene and position.
3. Press and hold Ⓐ in each scene: no jump or other action. Verify left-stick movement, smooth turn and teleport still work.
4. In a new session, move 2 m in Gallery. The hint ends and stays completed after leaving and returning. Head rotation alone must not complete it.
5. Point at and pick up a Bedroom object. Confirm the grip hint ends. Dragging the menu title bar must not complete object pickup.
6. Look at Vincent within 3 m, ask once successfully, then leave and return. Confirm the B lesson stays complete. Repeat with a failed/empty request: it must remain unfinished.
7. Start Vincent's speech and mirror instructions while a first-use hint is pending. Confirm there is no overlap. Review world-space placement, text size and occlusion on the headset.
8. Point at each rearranged Gallery painting, pull the trigger, then try Back / Enter. The first-use hint completes when the menu actually opens and does not repeat. Almond Blossom must keep Enter disabled. Walk through the new arch and corridor.

See `VERIFICATION.md` for the results actually obtained on this branch. Synthetic input and Editor checks do not establish physical Quest behavior, live microphone/AI success, multiplayer voice, or frame rate.
