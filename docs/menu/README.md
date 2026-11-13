# Player menu

The selected design is **classification + content in two columns**. The left
column keeps Controls, Audio, Back to Gallery, Settings, Restart and Leave
session in place. The right column changes when a category is selected.
There is no Resume button; the top-right × closes the panel.

The panel and the three circular shortcuts are built at runtime from
`Assets/Shared/UI` through `Resources/VG_Caption`: the original box, name tab,
star/ring, button-circle sprites and Libre Baskerville font. No scene, prefab
or shared XR setup needs another menu instance.

## Controls and behavior

| Entry | Behavior |
| --- | --- |
| Left controller Menu / Editor M | Toggle the panel in front of the current camera |
| Circular Menu / Audio / Gallery | Menu opens the two-column panel; Audio opens only a compact volume/mute popup; Gallery requests confirmation |
| Controls | Basic binding summary; Open controller guide reopens the existing eleven-page EntranceControllerGuide in the same scene |
| Audio | Master listener volume and mute/unmute. More settings selects Settings. There are no separate music/voice/effect gains |
| Back to Gallery | Visible but disabled in Gallery. Elsewhere, confirmation is required before SceneFader returns to Gallery |
| Settings | Shared Vincent subtitles on/off, menu size 85–115%, read-only English language |
| Restart | Confirmation before SceneFader reloads the current scene. This does not clear app-session first-use completion |
| Leave session | Confirmation before leaving the network room and quitting the player; in Editor, stops Play Mode |
| Cancel | Returns to the previously selected non-confirmation category without executing the action |
| × | Hides the panel without changing scenes |
| Grip on title bar | Moves the world-space panel; it stays upright and faces the camera while dragged |

The panel opens 1.05 m ahead in VR (1.1 m on desktop), within the existing
2 m controller UI ray. Shortcuts follow the lower-right camera view; hover
shows the label. They hide while the full tutorial or a scene fade is active.
The shared menu modal flag includes active UI pointer focus and a short
closing-click guard so that a shortcut click is not passed to a painting
behind it. The mere presence of the shortcuts is not modal.

In Editor Play Mode, focus Game and press M to free the mouse and use the
buttons. The menu still supports tracked controller UI rays and the title-bar
XR grab handle. No A binding is added. Physical headset placement, ray use and
comfort remain acceptance tests, not claims based on Editor renders.

## Preferences and limits

`PlayerMenuPreferences` stores local keys under `InnerFrame.PlayerMenu.`.
Master gain uses `AudioListener.volume`, retaining the existing per-source
music crossfades and speech ducking. Mute remembers the last audible gain.
Changes are flushed when the menu closes or the app exits.

Only `VanGoghCaption` opts into `RespectSubtitlePreference`. The full guide,
first-use tutorial words and instruction hints stay visible when Vincent
subtitles are off. Paging and speaking priority remain unchanged. This is not
a promise to hide other independently implemented scene text.

Menu size scales this panel, not every label in every scene. English is the
only implemented language here; the menu does not offer a nonfunctional
language selector. Leaving does not promise automatic progress saving.

## Implementation

- `PlayerMenu`: one persistent owner; scene-local views and camera are rebound
  after scene changes or camera replacement. Keeps SceneFader/network cleanup.
- `PlayerMenuPanel`: two columns, shared sprites, real volume slider, opt-in
  subtitle preference, confirmations and title-bar grab.
- `PlayerMenuQuickActions`, `PlayerMenuShortcutPointer`, `PlayerMenuShortcutIcon`:
  circular entries, line symbols and pointer protection for desktop and XRI.
- `MenuLook`: original shared assets, colors, font and controller glyphs.
- `MenuWristTag`: existing left-controller hint, retained.

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
  `check`, `render`, `preview`, `live`, and `live-audio`. `live` starts the existing clean
  Gallery in Play Mode and opens the two-column Audio page; `live-audio` opens the compact volume popup. Both refuse a different/dirty scene.
- Results: `Logs/MenuSplit/status.json`, `checks.json`, `checks.txt`, `Images/`.
  These generated files are not committed.
- The render check compares each shortcut with its symbol enabled and disabled.
  A blank circle fails even if its Image component and sprite reference exist.

## Validation on 2026-11-13

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

Check all five scenes with a physical Quest: circular hit targets and tooltips,
no trigger click-through, panel readability at each size, title-bar dragging,
full guide close-in-place, Gallery disabled at home, cancellation and confirmed
scene transitions, mute restoration, Vincent subtitles versus tutorial words,
network leave, and actual standalone exit. Editor success is not a Quest pass.
