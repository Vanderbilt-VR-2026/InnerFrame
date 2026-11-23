# InnerFrame controls and first-use teaching

Updated 2026-11-13 from Cici's five-page **InnerFrame controls: one button, one job** (2026-11-12), with Gallery #199 merged from `main` at `ce62132`. Jump is disabled and the right controller's A button remains free.

## Quest controls

| Control | Action |
| --- | --- |
| Left stick | Move |
| Right stick left / right | Smooth Turn |
| Right stick forward | Where teleport is available, aim and release at a valid floor destination |
| Either trigger | Pull the trigger to select UI; open a Gallery painting's menu, then choose Enter or Back; select interactions in The Starry Night |
| Either grip | Hold grip to pick up an object |
| Right Ⓐ | Unassigned; Jump is disabled |
| Right Ⓑ | Hold to ask Vincent, release to send; pressing while he speaks interrupts him |
| Left Ⓧ | Hold to talk to the other player, release to stop |
| Left ≡ Menu | Open / close Player Menu; Controls opens the full guide in the current scene |
| Left Ⓨ | Unassigned |

From Gallery onward, press the left controller Menu button to open the black/gold two-column menu. The persistent corner shortcuts and wrist tag are no longer shown. Hold Grip on the title bar to move the menu closer, farther, or rotate it. Releasing leaves it at its world position; closing and reopening centres it in front of you.

Gallery uses Cici's new arch, corridor and hanging order: Self-portrait, The Bedroom, Café Terrace, The Starry Night, Almond Blossom. The self-portrait is for Vincent's Ⓑ conversation; the other four paintings have Trigger menus. Almond Blossom now has Enter enabled and leads to the real Almond Studio scene (#202). The serialized Gallery scene, layout and artwork assets come from main, synchronized through `418dbc6` on 2026-11-18.

`ControlsCatalog` supplies the full guide and first-use wording. Update this table, the catalog, and other affected hints in the same PR as any binding change. Headset pages contain Quest controls only. In Unity, `{B}`, `{X}`, `{A}` and `{Y}` render using the shared key-circle sprite; the menu bars are drawn inside that same circle. This avoids missing Unicode circled-letter glyphs in the font.

## Full guide and shared UI

`Resources/EntranceControllerGuide` retains its asset GUID and shared contextual-dialogue reference. It displays the complete painted `ControllerOverview` image on request, with one functional upper-right × and no Previous/Next sequence. Start and Join in Entrance take the player straight into the story intro. Gallery does not automatically open this diagram: the physical welcome sign’s Controls button or Menu → Controls opens it in the current scene.

The guide, Vincent subtitles, first-use prompts and story dialogue share the original Vincent artwork. They open in front of the player, then remain at that world position and orientation through head rotation, roll, walking and Vincent movement. They do not use a camera-attached canvas or VIEW-space compositor. Looking away can move them outside the view; this is the selected world-anchored design.

Aim anywhere on a dialogue or hint box, including its words or Vincent name tab, and hold Grip to move it closer, farther, or rotate it with the controller; release to leave it there. Pickup faces it toward the player once. Menu uses its title bar; other panels retain their top-edge handles. The rear shows a plain backing, with no mirrored words or hidden front-button interaction. Menu → Controls → Bring dialogue here recalls the current scene’s latest spoken caption without replaying voice, resetting its page, or enabling subtitles that are off. Gallery detail and painting menus, wayfinding, exit choices, and Almond letter/epilogue panels use the same anchor/drag behavior. The intro's physical canvas remains in place and can be dragged by its top gold border.

The approved close design (option 02, 2026-11-18) is a small inset gold outline circle and ×. Both brighten on hover; the visible circle is smaller than its 55 mm transparent click target. Closing captions hides only the current spoken answer, including later streamed parts, while speech continues. A new answer can display again. Closing a first-use prompt suppresses it for this session without recording completion. Controls → Show hidden panels restores dismissed eligible prompts and choices. Settings → Vincent subtitles → Off hides spoken captions in Bedroom/Gallery, Starry Night and Almond Studio; instructions remain visible. No sound source is stopped by either close or the subtitle preference.

## First-use rules (PDF page 5)

| Lesson | Availability | Completion evidence | Integration |
| --- | --- | --- | --- |
| Gallery Move | First Gallery visit | 2 m accumulated horizontal camera travel, measured in steps of at least 5 cm | Automatic; a new camera/scene or fade resets the measurement origin |
| Bedroom Grip | Pointing at a scene pickup through an XRI ray | Actual `XRGrabInteractable.selectEntered` or an already active selection | Automatic in PaintedBedroom; excludes menu/UI grabbables |
| Gallery painting | The real Gallery pointer is hovering a menu painting | The matching painting menu is open and visible | Automatic runtime binding to #199's four menu paintings; disabled/stale input cannot complete it |
| Vincent Ⓑ | Looking within 20° at Vincent's head/portrait within 3 world metres | A nonempty local `SpeechToProxy.Answered` response | Automatic for existing Vincent talkers; remote speech, B presses, failed requests and empty end events do not complete it |
| Companion Ⓧ | A second player is present | Confirmed voice exchange | `FirstUseCompanionAdapter`; pending voice integration |

The local `TutorialManager` survives scene changes. It holds a `TutorialSession` in memory; completed lessons stay completed until the next play session. No completion state is saved to disk. Available requests are ordered by first availability; only one can be shown. Unavailable, disabled and destroyed sources are withdrawn. A successful action can complete a lesson before its hint appears.

Vincent's speech, listening/thinking/error states, existing task instructions, arrival/entrance hints, open artwork cards, Gallery painting menus, scene fades and Player Menu take priority. First-use UI hides immediately when blocked, then resumes if still relevant. The idle B reminder in `VanGoghCaption` yields while a first-use request is available and returns afterwards; mirror instructions and conversation state remain owned by Vincent. Existing Starry Night story/B reminders remain owned by its conversation system. The left controller Menu button opens the two-column menu on Audio. Opening Menu during the guide closes that diagram and returns to Controls. The guide’s × returns to Menu when launched there, or to the scene when launched from the Gallery sign; closing Menu never restores a hidden guide. Persistent corner shortcuts and the wrist tag are no longer spawned.

## Connecting unfinished interactions

The current Gallery needs no serialized adapter edits. `TutorialManager.RefreshSceneBindings` binds its `GalleryPainting` components to the existing `GalleryPaintingPointer` and `GalleryPaintingMenu`. The adapter reads published hover state after pointer Update and completes only when that painting's visible menu opens. For future independent scenes, the explicit `SetPointing` / `MenuOpened` adapter contract remains available; only set `triggerMenuReady` when the actual interaction exists.

For the voice owner: add `FirstUseCompanionAdapter`, call `SetCompanionPresent` from actual room membership, and call `VoiceExchangeCompleted()` after a confirmed local voice exchange. An X press alone is not evidence. This unfinished voice adapter is not automatically attached.

Coordination: Player Menu builds on merged #181; shared UI builds on #184. Gallery #199 and Cici's Starry Night `PlayerMenu.MenuOpen` input guard are now included through the merge from main. Keep this PR in draft for owner and headset review; do not rebuild or save Cici's Gallery scene as part of tutorial verification.

## Asset preparation and checks

Unity **6000.3.23f1**, Android target. `InnerFrame > Controls > Prepare assets` (batch `ControlsTutorialAssets.Prepare`) writes prefab changes through Unity. It disables the shared Starter rig's JumpProvider, removes its RightHand PrimaryButton → Jump binding, and checks dependencies, GUID references and literal loads before deleting the legacy `ControllerTutorial.prefab`. The input editor also serializes its current `version: 1` and default `priority: 0` fields; other bindings are preserved.

Run these batch entry points in separate Unity invocations:

| Entry point | What it verifies |
| --- | --- |
| `ControlsTutorialChecks.RunBatch` | All six build scenes, modal Controls and return-to-menu close, full-diagram clarity, 2 m threshold, arbitration, real Gallery ray/menu fixtures, Vincent asset placement/idle reminder, simulated XRI/answer contracts and session reset |
| `ControlsTutorialPlayChecks.RunBatch` | Real Play Mode bootstrap, synthetic A input with a positive control, actual Single scene transition and completion persistence, Controls after transition; temporary fixture scenes are created/deleted through Unity |
| `ControlsTutorialChecks.RenderBatch` | Full guide, Player Menu and first-use screenshots (run with graphics enabled; adapter views use fixture requests) |
| `PlayerMenuCheck.RunBatch` | Existing Player Menu regression checks |
| `InnerFrame.Gallery.EditorTools.Tests.GalleryLayoutPlayCheck.RunBatch` | Existing Gallery Play Mode suite, including simulated controller rays and menu actions; use `-gpOut Logs/GalleryUpdate`, with graphics enabled |

Reports go to `Logs/Controls/`; renders to `Logs/Controls/Images/`. These are local verification artifacts, not runtime assets. Run `bash scripts/ci-local.sh` after committing and before pushing.

## Quest acceptance still needed

1. From Entrance, start a new room or join an existing one. Confirm the story intro starts immediately on both paths. Try both watching the intro and pressing Skip. Gold should fill both eyes from its first frame. On the first Gallery visit, confirm no controller guide opens automatically. Open it from the physical sign and close it back to the same scene.
2. In Entrance, Gallery, PaintedBedroom, StarryNight and CafeTerrace, use ≡ Menu → Controls. Open the full diagram and click its × with each ray; it must return to Controls in Menu. Close Menu and verify the same scene/player position, with no hidden guide restored.
3. Press and hold Ⓐ in each scene: no jump or other action. Verify left-stick movement, smooth turn and teleport still work.
4. In a new session, move 2 m in Gallery. The hint ends and stays completed after leaving and returning. Head rotation alone must not complete it.
5. Point at and pick up a Bedroom object. Confirm the grip hint ends. Dragging the menu title bar must not complete object pickup.
6. Look at Vincent within 3 m, ask once successfully, then leave and return. Confirm the B lesson stays complete. Repeat with a failed/empty request: it must remain unfinished.
7. Start Vincent's speech and mirror instructions while a first-use hint is pending. Confirm there is no overlap. In Gallery and each painting, turn, look up/down and tilt the headset: captions and hints must retain their world position and orientation, then remain at the released position after a Grip move. Also approach from behind: text must not be mirrored, and grabbing the top edge must turn the front toward the player. Review readability/occlusion and repeat after a scene change.
8. Point at each rearranged Gallery painting, pull the trigger, then try Back / Enter. The first-use hint completes when the menu actually opens and does not repeat. Almond Blossom's Enter must open Almond Studio. Walk through the new arch and corridor.

See `VERIFICATION.md` for the results actually obtained on this branch. Synthetic input and Editor checks do not establish physical Quest behavior, live microphone/AI success, multiplayer voice, or frame rate.

## Controller diagram clarity — 2026-11-21

The approved painted controller style is retained; the diagram wording was updated
on 2026-11-22 to say “Hold Grip on a title bar to move.” Its dedicated Unity
import command, **InnerFrame > Controls > Preserve controller diagram clarity**, keeps
the source’s 1536 × 1024 pixels, disables mip downsampling, and uses uncompressed RGBA32
on Android (6 MiB for this texture). It does not rebuild Gallery or edit its scene.
The full diagram opens 1.35 m wide at 1.15 m, approximately 61° across, remains anchored
in the world, and keeps its original aspect ratio. Reopen to place it in front again.

`ControlsTutorialChecks.RunControllerClarityBatch` applies only this importer change,
checks the loaded pixels/format and unchanged source bytes, and renders the guide to
`Logs/Controls/Images/controller-overview-clarity.png`. Headset legibility is a separate
acceptance check; a desktop render does not establish physical Quest readability.
