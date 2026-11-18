# Player menu

The selected design is **classification + content in two columns**. The left
column keeps Controls, Audio, Back to Gallery, Settings, Restart and Leave
session in place. The right column changes when a category is selected.
The left controller Menu button (≡), or M in the Editor, opens the Audio page.
There is no Resume button; the top-right × or another Menu press closes the panel.

## Design reference confirmed on 2026-11-18

Keep the approved English-only quick-menu mockup: the shared black/night-blue
and gold artwork, Libre Baskerville, and the original Gallery temple icon.
The full menu has categories on the left and their content on the right:
Controls, Audio, Back to Gallery, Settings, Restart, and Leave session.
Audio provides master volume and mute. Settings provides Vincent subtitles
and menu size (85–115%); English is a read-only language label. Back to
Gallery, Restart, and Leave session require confirmation; Gallery is disabled
when already there. Close with ×, without a Resume button.

This controller-open menu is now implemented. The panel appears in front of
the player, then holds its world position and orientation when the player
turns or walks. It is not parented to the head and does not use the HUD compositor.
The corner shortcuts and wrist tag are no longer spawned. Dialogue, tutorials, wayfinding and story panels now use the same world-anchor behavior, with controller drag and an explicit dismiss cross. The screenshot is kept in the local project deliverables:
`VR-Quick-Menu-Functions-20261117/quick-menu-functions.png`.

The panel is built at runtime from
`Assets/Shared/UI` through `Resources/VG_Caption`: the original box, name tab,
star/ring, button-circle sprites and Libre Baskerville font. No scene, prefab
or shared XR setup needs another menu instance.

## Controls and behavior

| Entry | Behavior |
| --- | --- |
| Left controller Menu / Editor M | Toggle the full panel, starting with Audio; holding does not repeatedly toggle |
| Controls | Basic binding summary; Open controller guide reopens the existing eleven-page EntranceControllerGuide in the same scene; Show hidden panels restores dismissed eligible prompts/choices |
| Audio | Master listener volume and mute/unmute. More settings selects Settings. There are no separate music/voice/effect gains |
| Back to Gallery | Visible but disabled in Gallery. Elsewhere, confirmation is required before SceneFader returns to Gallery |
| Settings | Shared Vincent subtitles on/off, menu size 85–115%, read-only English language |
| Restart | Confirmation before SceneFader reloads the current scene. This does not clear app-session first-use completion |
| Leave session | Confirmation before leaving the network room and quitting the player; in Editor, stops Play Mode |
| Cancel | Returns to the previously selected non-confirmation category without executing the action |
| × | Hides the panel without changing scenes |
| Hold trigger on title bar | Drag along the opening plane; releasing keeps the panel at its world position |

The panel opens 1.05 m ahead in VR (1.1 m on desktop), within the existing
2 m controller UI ray. It opens upright even if the player tilts their head.
Closing and reopening resets it to the new view centre. Opening it from the
guide suspends and preserves the current page; closing it restores that page.
Back to Gallery keeps the original temple glyph and is dimmed in Gallery.
An open panel consumes scene input, including the closing click's frame.

In Editor Play Mode, press M, then use the free mouse to click or drag.
Closing restores the previous mouse mode. VR uses tracked controller rays
and the trigger on the title bar. No visible virtual hands are required.

## Preferences and limits

`PlayerMenuPreferences` stores local keys under `InnerFrame.PlayerMenu.`.
Master gain uses `AudioListener.volume`, retaining the existing per-source
music crossfades and speech ducking. Mute remembers the last audible gain.
Changes are flushed when the menu closes or the app exits.

`VanGoghCaption`, Starry Night's dialogue and Almond Studio's dialogue opt into `RespectSubtitlePreference`. The guide and task hints remain visible with subtitles off. The preference hides words without stopping voice, paging or speaking priority. The shared panel's × dismisses the current answer through its remaining streamed parts; a new answer can show again. First-use dismissal is separate from completion. Controls → Show hidden panels restores dismissed eligible prompts/choices.

Menu size scales this panel, not every label in every scene. English is the
only implemented language here; the menu does not offer a nonfunctional
language selector. Leaving does not promise automatic progress saving.

## Implementation

- `PlayerMenu`: one persistent owner; scene-local views and camera are rebound
  after scene changes or camera replacement. Keeps SceneFader/network cleanup.
- `PlayerMenuPanel`: two columns, shared sprites, real volume slider, opt-in
  subtitle preference, confirmations and title-bar grab.
- `PlayerMenuShortcutIcon`: reuses the original Gallery symbol in the navigation.
- `PlayerMenuQuickActions`, `PlayerMenuShortcutPointer`, `MenuWristTag`: legacy
  components remain for historical fixtures; PlayerMenu does not create them.
- `MenuLook`: original shared assets, colors, font and controller glyphs.

The current Gallery painting-entry flow is documented in
`docs/controls/README.md`. Existing arrival/wayfinding hints remain separate
and yield to menu interaction. Talking to Vincent retains its existing B/T
binding; the menu does not replace that interaction.

## Editor checks and previews

- **InnerFrame > Menu > Check split menu**: temporary additive fixtures,
  original scene restored without saving it.
- **InnerFrame > Menu > Preview split menu**: native Unity renders of each
  right-hand page, shown in an Editor preview window.
- Batch compatibility: `-executeMethod PlayerMenuCheck.RunBatch
  [-menuShots <folder>]`. The entry point exits with its result.
- Explicit automation: `Temp/SplitMenuPreview.request` accepts `status`,
  `check`, `render`, `preview`, `check-input`, `live`, and `live-audio`. `live` starts the existing clean
  Gallery in Play Mode and opens the two-column Audio page; `live-audio` opens the compact volume popup. Both refuse a different/dirty scene.
- `check-input` enters Gallery Play Mode, tests simulated OpenXR Menu and Editor
  M through the real bindings, then returns to Edit Mode.
- Results: `Logs/MenuSplit/status.json`, `checks.json`, `checks.txt`, `input.txt`, `Images/`.
  These generated files are not committed.
- The render check compares the original Gallery glyph enabled and disabled.
  A blank circle fails even if its Image component and sprite reference exist.

## Controller-open menu validation on 2026-11-18

This update changes existing menu scripts only; no scene, prefab, XR settings,
shared render settings, or artwork asset was authored. Local generated font and
project settings edits remain outside the menu commit.

- [V] Unity 6000.3.23f1 compiled the scripts with Android selected.
- [V] Menu fixtures: **117 passed, 0 failed**, including all six categories,
  confirmations/cancel, shared artwork and original Gallery glyph pixels,
  mouse and controller rays before/after camera movement, world position and
  orientation after release, and recentering only when reopened.
- [V] Gallery Play Mode simulated OpenXR left Menu and keyboard M checks:
  **15 passed, 0 failed**. They exercise real InputActions, held-button behavior,
  close cross, tutorial page preservation, and absence of corner/wrist UI.
- [V] Controls regression: **198 passed, 0 failed** across enabled scenes.
  All **six** shared UI integration groups passed and restored the saved Gallery.
- [V] Gallery Play logged existing Fusion startup errors (`Empty application id` /
  `InvalidAuthentication`). The menu checks still pass, but this is not a clean
  multiplayer run; no credentials or network configuration were changed.
- [I] No physical Quest/controller, new APK, microphone/AI conversation,
  online multiplayer, fresh-Library import, or standalone exit acceptance was
  performed for this menu change. Existing transition results above belong to
  the earlier main integration, not this change.

Review effort: focused self-review of runtime/input and verification evidence
for the existing menu scripts. The earlier main integration's PR-overlap checks
remain applicable; no additional scene/asset ownership changed here.

| Finding | Verdict and evidence | Resolution |
| --- | --- | --- |
| Close cross missed desktop clicks after a camera rotation | Confirmed: the old native screen-rectangle check rejected its projected centre while the tracked world ray hit it; the initial fixture reported 116 passed / 1 failed | Menu mouse events now use the same XRI world-plane ray test as controllers; the final 117 assertions include that pose |
| Initial OpenXR test used a delta event on a bitfield button | Confirmed by Input System exception before a Menu action was sent | Test queues full device state with the button value, then verifies actual menu behavior; 15 input assertions pass |
| A guide page might be lost when Menu is opened | Refuted by Play checks plus Controls checks: the same guide instance/page resumes after × or Menu closes the panel | Keep the existing suspension/resume path |

Evidence: `Logs/MenuSplit/checks.json`, `input.txt`, `Images/`,
`Logs/Controls/checks.txt`, and `Logs/DialoguePlacement/checks.json`.
These local generated reports are not committed.

## World panels and approved close control — 2026-11-18

Dialogue, first-use hints, wayfinding, exit choices, Gallery details/painting menus,
and Almond reading panels stay at their opening world pose. Holding the trigger
on the panel background drags it along that plane; release preserves its pose.
The existing Menu title-bar drag remains. Turning away does not recenter or close
panels. Menu close/reopen centres it again; menu suspension preserves a guide's
page and dragged position. Shared canvases detach from moving Vincent owners.

The user selected **02 — Outline circle**: a muted gold circle and × inset at the
panel's top right, brightening together on hover, without a filled gold disk.
The visible circle is 36 mm; the transparent hit target remains 55 mm. Original
VG artwork and English text are retained. Hints reserve space for the cross.

[V] Unity 6000.3.23f1 compiled with Android selected in the current checkout.
All seven integration groups passed and restored the saved Gallery scene:
world-panel/interaction fixtures **45/0**, Menu **117/0**, Controls **199/0**,
Starry Night UI **11/0**, transitions **44/0**, wayfinding and scene restoration.
The fixtures test world corners and rotation through yaw/pitch/roll/translation,
moving Vincent parents, trigger dragging, actual close-button click dispatch,
hover styling/hit area, session dismissal separate from completion, subtitle
preference, real Gallery detail prefab ray hits and native Enter/Close clicks,
and painting-menu Back. Enter's external scene load is intercepted explicitly
in that fixture; it is not a scene-transition acceptance claim.

[V] Gallery Play Mode Menu input **15/0** (simulated OpenXR Menu and keyboard M).
[V] Gallery Play Mode audio continuity **7/0**: a generated silent local clip's
sample position advances after dismissing a caption, across streamed parts,
and with subtitles off/on. This checks playback state, not audible hardware.
Reports: `Logs/WorldPanels/checks.txt`, `dialogue-world.png`, `audio-play.txt`,
plus `Logs/DialoguePlacement/checks.json`, `Logs/MenuSplit/`, `Logs/Controls/`.

[I] No new APK, physical Quest/controller, live microphone/AI response,
fresh-Library import or clean-checkout headset acceptance was performed.
Gallery still reports the existing missing Photon App ID/InvalidAuthentication
startup error, so this is not a clean online multiplayer run.

Review effort: three independent angles (world anchoring/lifecycle, subtitle and
dismissal semantics, input and test evidence), followed by reproduced fixes.
Confirmed fixes: obsolete per-frame recentering, streamed parts reopening a
closed answer, exit choices immediately reopening, native Gallery/choice
raycasts being disabled, scene triggers consuming UI clicks, a 3.5 m painting
panel exceeding Gallery's 2 m ray, and stale destroyed panel registrations.
The test fixture now deactivates only its own temporary scene's views, and
NearFar guards respect UI enablement and the configured ray length. No scene,
prefab, shared settings or teammates' in-progress work was saved by this change.

## Historical validation on 2026-11-13

- [V] Unity 6000.3.23f1 recompiled the changed scripts with Android selected.
  The existing Gallery checkout was used; this was not a new clean import.
- [V] The final split-menu render fixture completed at 23:40:33 UTC:
  **100 passed, 0 failed**. It checks actual UI page selection, compact Audio,
  normal menu restoration, cancel/close behavior, subtitles, viewport bounds,
  shared artwork, disabled Gallery at home and rendered icon visibility.
- [V] Native Unity renders show the Menu, speaker and Gallery symbols. Each
  contributes visible pixels (5062, 5086 and 7589 respectively in the close-up
  fixture). The compact Audio render contains only the audio controls and ×.
- [V] Gallery Play Mode was opened and visually inspected: all three shortcut
  symbols and the compact Audio popup are visible in the actual scene.
- [V] Three review angles covered runtime interaction, preferences/lifetime and
  test evidence. Confirmed fixes include subtitle preference leaking into the
  tutorial, rebinding disabled cameras, blank custom-mesh symbols, button
  nine-slicing and the volume handle geometry. The symbols now use native
  Image sprites generated in memory, preserving the shared circular buttons.
- [I] Automated desktop coordinate clicks could not be completed because the
  computer-control tool returned `noWindowsAvailable`. The fixture invokes
  actual UI controls, but this is not a physical mouse/controller acceptance pass.
- [I] No new APK, Quest 3, multiplayer, microphone/AI, confirmed scene-transition
  or standalone exit test was performed for this menu update. Earlier controls
  and Gallery regression runs belong to their recorded earlier commits.
- [V] Gallery scene YAML, shared render/XR settings and the pre-existing local
  TMP fallback font-cache edit were not included in this menu change.

## Headset acceptance still required

Check all six enabled scenes, including Almond Studio, with a physical Quest: left Menu opens/closes, no persistent corner/wrist UI, world-anchored panel,
no trigger click-through, panel readability at each size, title-bar dragging,
full guide close-in-place, Gallery disabled at home, cancellation and confirmed
scene transitions, mute restoration, Vincent subtitles versus tutorial words,
network leave, and actual standalone exit. Editor success is not a Quest pass.

## Main synchronization on 2026-11-18

Merged main through `418dbc6` into `rowling/controls-first-use-tutorial` locally.
This includes Almond Studio and its Gallery entry (#202), controller fixes
(#203), music (#208), Vincent speech (#209), the Gallery Bedroom-frame hover
fix (#204), mobile image settings (#201), and Vincent's Bedroom/Starry Night
departure routes (#210). All LFS assets were downloaded. Existing local Unity
settings and build backup folders were retained separately from this change.

The #210 choice card now uses `VG_FollowGroup.CreateLayout`, puts its raycasters
on the displayed canvas, and tests hits against the displayed button plane.
Its explicit Hide releases that canvas. Shared dialogue deactivation no longer
reparents a canvas while Unity deactivates its camera hierarchy; the Starry
Night Play check reproduced this error before the fix and records zero errors
afterwards. Destruction still cleans up detached surfaces, and TutorialManager
still rebuilds a surface lost with an old camera.

Regression fixtures now recognize Almond Studio's real entrance, Vincent v2's
head bone, and the two-column menu's Gallery confirmation. No scene or prefab
was authored during this integration.

Unity 6000.3.23f1, Android target, existing working checkout:

| Check | Result |
| --- | --- |
| Script compilation | Passed |
| Menu interactions and rendered icons | 175 passed, 0 failed |
| Controls, all enabled scenes, simulated tutorial events | 186 passed, 0 failed |
| Shared transitions and choice card | 44 passed, 0 failed |
| Shared placement, GPU/controller-ray, Starry UI, wayfinding, scene restoration | All six integration groups passed |
| Starry Night Play: Back, interrupt Follow with confirmed Back, Follow to Almond Studio | 18 passed, 0 failed; 0 logged errors |
| Bedroom Play: Back, Follow through Door_Left into Café Terrace | 25 passed, 0 failed; 0 logged errors |

Local evidence is in `Logs/MainUpdate20261118/`, `Logs/MenuSplit/`,
`Logs/Controls/` and `Logs/DialoguePlacement/`. Play checks use scripted input;
the Bedroom door check uses one local Fusion peer. No new APK, physical Quest,
microphone/AI exchange, online multiplayer, or fresh-Library import was tested.

Unmerged work was inspected but not incorporated: #226's Gallery studio/blank
frame conflicts with this branch in `SceneFader.cs` and `docs/gallery/README.md`;
#205's player avatars conflict in `docs/ASSETS.md`. Landing order/resolution
needs coordination before either is combined. #189 and #191 merge cleanly in
memory as of this check. No coordination message was sent by this local sync.
