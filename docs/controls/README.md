# InnerFrame controls and first-use teaching

Updated 2026-11-12 from Cici's five-page **InnerFrame controls: one button, one job** (checked against `main` at `998dacd`). The approved change in this branch is to turn off Jump and leave the right controller's A button free.

## Quest controls

| Control | Action |
| --- | --- |
| Left stick | Move |
| Right stick left / right | Smooth Turn |
| Right stick forward | Aim a teleport; release at a valid destination to teleport |
| Either trigger | Pull the trigger to select a UI button; select interactions in The Starry Night |
| Either grip | Hold grip to pick up an object or move a menu by its title bar |
| Right Ⓐ | Unassigned; Jump is disabled |
| Right Ⓑ | Hold to ask Vincent, release to send; pressing while he speaks interrupts him |
| Left Ⓧ | Hold to talk to the other player, release to stop |
| Left ≡ Menu | Open / close Player Menu; Controls opens the full guide in the current scene |
| Left Ⓨ | Unassigned |

The current Gallery still uses grip at its pedestal entrances. Cici's planned painting Trigger menu is not on this base commit. Its first-use adapter is ready for that implementation, and is not attached to the existing grip entrances.

`ControlsCatalog` supplies the full guide and first-use wording. Update this table, the catalog, and other affected hints in the same PR as any binding change. Headset pages contain Quest controls only. In Unity, `{B}`, `{X}`, `{A}` and `{Y}` render using the shared key-circle sprite; the menu bars are drawn inside that same circle. This avoids missing Unicode circled-letter glyphs in the font.

## Full guide and shared UI

`Resources/EntranceControllerGuide` retains its asset GUID. Its component instantiates `Assets/Shared/UI/Prefabs/VG_DialogueUI.prefab`, with the actual Vincent font, box, name tab, star, key circle, materials and settings. Previous / Next / Close use those same assets. The Entrance supplies the existing intro continuation; Player Menu supplies no scene transition. The Menu button can also close Controls.

The first-use view instantiates the same complete dialogue UI. It sets `FollowHead = false` and places the layout in world space. The Gallery movement prompt stays where it first appears; object prompts use their target's location. Full Controls uses the normal dialogue follow behavior. Physical placement and readability require a Quest review.

## First-use rules (PDF page 5)

| Lesson | Availability | Completion evidence | Integration |
| --- | --- | --- | --- |
| Gallery Move | First Gallery visit | 2 m accumulated horizontal camera travel, measured in steps of at least 5 cm | Automatic; a new camera/scene or fade resets the measurement origin |
| Bedroom Grip | Pointing at a scene pickup through an XRI ray | Actual `XRGrabInteractable.selectEntered` or an already active selection | Automatic in PaintedBedroom; excludes menu/UI grabbables |
| Gallery painting | Pointing at a painting whose Trigger menu is ready | That menu actually opened | `FirstUsePaintingAdapter`; pending Gallery implementation |
| Vincent Ⓑ | Looking within 20° at Vincent's head/portrait within 3 world metres | A nonempty local `SpeechToProxy.Answered` response | Automatic for existing Vincent talkers; remote speech, B presses, failed requests and empty end events do not complete it |
| Companion Ⓧ | A second player is present | Confirmed voice exchange | `FirstUseCompanionAdapter`; pending voice integration |

The local `TutorialManager` survives scene changes. It holds a `TutorialSession` in memory; completed lessons stay completed until the next play session. No completion state is saved to disk. Available requests are ordered by first availability; only one can be shown. Unavailable, disabled and destroyed sources are withdrawn. A successful action can complete a lesson before its hint appears.

Vincent's speech, listening/thinking/error states, existing task instructions, arrival/entrance hints, open artwork cards, scene fades and Player Menu take priority. First-use UI hides immediately when blocked, then resumes if still relevant. The old idle B hint in `VanGoghCaption` yields to the first-use manager; mirror instructions and conversation state remain owned by Vincent. Existing Starry Night story/B reminders remain owned by its conversation system.

## Connecting unfinished interactions

For the Gallery layout owner: add `FirstUsePaintingAdapter` to a finished Trigger menu target, set `triggerMenuReady`, and provide a world-space `hintAnchor`. Call `SetPointing(true/false)` from the real aim/highlight events. Call `MenuOpened()` only after the menu successfully opens. Do not complete this from a trigger press or a pedestal grip.

For the voice owner: add `FirstUseCompanionAdapter`, call `SetCompanionPresent` from actual room membership, and call `VoiceExchangeCompleted()` after a confirmed local voice exchange. An X press alone is not evidence. Neither pending adapter is automatically attached in this branch.

Coordination: Player Menu builds on merged #181; shared UI builds on #184. Gallery layout and Cici's Starry Night trigger-through-menu fix remain separate work. The latter also affects a Controls overlay until scene input honors `PlayerMenu.MenuOpen`. Keep this PR in draft until that interaction and headset results are reviewed.

## Asset preparation and checks

Unity **6000.3.23f1**, Android target. `InnerFrame > Controls > Prepare assets` (batch `ControlsTutorialAssets.Prepare`) writes prefab changes through Unity. It disables the shared Starter rig's JumpProvider, removes its RightHand PrimaryButton → Jump binding, and checks dependencies, GUID references and literal loads before deleting the legacy `ControllerTutorial.prefab`. The input editor also serializes its current `version: 1` and default `priority: 0` fields; other bindings are preserved.

Run these batch entry points in separate Unity invocations:

| Entry point | What it verifies |
| --- | --- |
| `ControlsTutorialChecks.RunBatch` | All five build scenes, modal Controls and scene-preserving close, shared UI/page fit, 2 m threshold, arbitration, simulated XRI/answer contracts, adapter gates and session reset |
| `ControlsTutorialPlayChecks.RunBatch` | Real Play Mode bootstrap, synthetic A input with a positive control, actual Single scene transition and completion persistence, Controls after transition; temporary fixture scenes are created/deleted through Unity |
| `ControlsTutorialChecks.RenderBatch` | Full guide, Player Menu and first-use screenshots (run with graphics enabled; adapter views use fixture requests) |
| `PlayerMenuCheck.RunBatch` | Existing Player Menu regression checks |

Reports go to `Logs/Controls/`; renders to `Logs/Controls/Images/`. These are local verification artifacts, not runtime assets. Run `bash scripts/ci-local.sh` after committing and before pushing.

## Quest acceptance still needed

1. From Entrance, complete or skip Controls and confirm the normal intro continues.
2. In Entrance, Gallery, PaintedBedroom, StarryNight and CafeTerrace, use ≡ Menu → Controls. Click Previous / Next / Close with each ray; closing must keep the same scene and position.
3. Press and hold Ⓐ in each scene: no jump or other action. Verify left-stick movement, smooth turn and teleport still work.
4. In a new session, move 2 m in Gallery. The hint ends and stays completed after leaving and returning. Head rotation alone must not complete it.
5. Point at and pick up a Bedroom object. Confirm the grip hint ends. Dragging the menu title bar must not complete object pickup.
6. Look at Vincent within 3 m, ask once successfully, then leave and return. Confirm the B lesson stays complete. Repeat with a failed/empty request: it must remain unfinished.
7. Start Vincent's speech and mirror instructions while a first-use hint is pending. Confirm there is no overlap. Review world-space placement, text size and occlusion on the headset.

See `VERIFICATION.md` for the results actually obtained on this branch. Synthetic input and Editor checks do not establish physical Quest behavior, live microphone/AI success, multiplayer voice, or frame rate.
