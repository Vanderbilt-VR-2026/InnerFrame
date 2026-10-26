# Artwork information display — Issue #14

**Status: source-only draft, not integrated into Unity.** No prefab, test scene,
Gallery change, Unity compilation result, or Quest test is included yet. The
execution host has neither Unity nor adb. AGENTS.md prohibits hand-editing
`.meta`, `.prefab`, and `.unity`, so source is staged outside `Assets/` until
Unity can import it and generate these files. Do not merge this draft as a
completed implementation of #14.

## Decisions implemented in source

- Reusable `ArtworkInfoDisplay`; ivory placard beside the painting, dark TMP
  type, title, artist, 1889 date, collection location and short introduction.
- More opens an upright World Space Canvas 1.35 world meters ahead of the
  camera. It is placed once; no tracking or per-frame positioning code.
- The panel is an independent scene object, so neither rig scale (0.8 in the
  current Gallery) nor placard movement changes its scale or position.
- Close dismisses it; repeated More reuses it. Disabling/destroying the display
  closes/destroys its panel. No grab, collider, scene transition, portrait,
  voice, new input bindings, or #57 implementation is added.
- Uses XRI's `TrackedDeviceGraphicRaycaster` and `XRUIInputModule`. The current
  Starter Assets use Trigger for UI Press; no controller remapping is made.
  Existing non-XR EventSystems produce an error instead of silently replacing
  another feature's input module. A missing EventSystem is created once.
- Generated placard: 0.64 × 0.54 m. Panel: 1.04 × 0.86 m. Fonts are 28–48 px
  at 0.001 m/px; actual glyph size and legibility require headset measurement.

## Import and isolated test (Unity 6000.3.23f1)

1. Use this feature branch in a clean checkout. Run from its root:
   `bash scripts/artwork-info/install-source.sh`. It refuses to overwrite an
   existing `Assets/ArtworkInfo` folder. Source is copied; no YAML is written.
2. Open Unity, let it generate metadata and compile. Import TMP Essential
   Resources via Window > TextMeshPro if a default TMP font is absent.
   Review all imported files and record their font licenses in `docs/ASSETS.md`.
3. Choose Tools > InnerFrame > Artwork Info > **1. Build Isolated Test Scene**.
   This closes other scenes after the save prompt, then creates `Assets/ArtworkInfo/ArtworkInfoDisplay.prefab` and
   `Assets/ArtworkInfo/ArtworkInfoTest.unity` via Unity APIs, using the current
   Gallery XR rig including its scene overrides. It does not save Gallery.
   Existing generated assets are deliberately not overwritten.
4. In that scene, enter Play Mode; choose **2. Check Behavior in Play Mode**.
   This invokes the actual More/Close Button events and checks distance,
   independence from the rig, no immediate head-motion coupling, no duplicate
   panel, reopening, and TMP text overflow. It does **not** simulate Trigger or
   prove persistence across frames. These checks have not been executed here.
5. Verify actual rays and Trigger with both controllers. Turn/move your head
   over multiple frames; the panel must stay at its opening location. Repeat
   while looking up/down and after moving around. Close then reopen: it must
   appear in front of the new viewpoint. Inspect line wrapping and clipping.
6. Read the placard at 0.5–0.7 m; inspect panel text at 1.35 m. Confirm roughly
   1.5 cm or larger letter height and comfortable controller targets. Confirm
   the cream surface is opaque, the contrast clear, and every paragraph fits.

## Gallery integration (scene owner: Cici)

Gallery is `Assets/Scenes/Gallery.unity`, owned by Cici. The requested change
is prepared for owner review through the PR; do not overwrite their scene.

After isolated testing, pull the latest main and open its Gallery. Select
`Painting_TheBedroom`, then choose **3. Add Prefab Beside Selected Painting**.
The command adds one root prefab instance with Undo, estimates placement from
painting bounds at 1.35 m above floor, and leaves the scene dirty for visual
inspection. It refuses duplicates. Confirm its face points into the room,
clearance from the wall, no occlusion of the painting/entry button, and player
sightlines. Adjust only the new instance, then save with Unity. Recheck the
scene diff: existing transition events, rig, colliders and controls must be
unchanged. This command has not been executed or visually validated here.

Commit generated `.meta` files, runtime/editor sources, prefab, test scene,
and the reviewed Gallery diff together using explicit filenames. Source under
`scripts/artwork-info` remains the delivery staging copy; after import, make
future feature edits in `Assets/ArtworkInfo` and keep the staging copy in sync
until the team chooses one canonical location. Edit the artwork content fields,
then use the component context menu **Refresh Artwork Text** to update the
Editor preview; Awake and Open also refresh text in a player. No shared settings are changed
by this draft. Do not include incidental package/settings upgrades.

## Facts, text and licenses

The displayed introduction is new, concise prose drafted for this change.
The earlier issue mentions a draft but does not contain it; this text needs
team editorial review. Factual sources checked on 2026-10-26:

- [Art Institute collection record](https://www.artic.edu/artworks/28560/the-bedroom):
  Vincent van Gogh, *The Bedroom*, 1889, Art Institute of Chicago.
- [Art Institute exhibition timeline](https://archive.artic.edu/van-gogh-bedrooms/timeline):
  the depicted bedroom is in Arles; the Chicago version was painted in
  Saint-Rémy in September 1889, following the 1888 original. Do not describe
  the Chicago canvas as painted in Arles.
- [Art Institute discussion of the three versions](https://archive.artic.edu/van-gogh-bedrooms/about-paintings).

No external image, portrait, font file, texture, material, or model is added by
this draft. The existing painting's CC0 image provenance is already recorded
in `docs/ASSETS.md`. A self-portrait is omitted; #57 remains separate. The
Unity TMP default font must be imported with its supplied license before
building; no new font license is asserted by this draft.

## Build and Quest verification still required

Use the project's Android build profile, OpenXR Meta Quest Support, minimum
API 32, GameActivity. Do not change these for this feature. Add the isolated
scene only to a temporary local test build configuration; preserve the team
build's Gallery → PaintedBedroom ordering. Build from a clean worktree.

Record tester/date/commit/APK source for all hardware results. Test launch in
immersive VR, both controllers' Trigger on More/Close, holding/releasing
Trigger (no accidental repeated activation), panel readability and world lock,
scene exit with panel open, reopen after returning, and unchanged entry control
and player menu. Have someone other than the builder run the acceptance test.
Neither APK deployment nor any Quest check was performed here.

Run `bash scripts/ci-local.sh` on the committed branch before pushing. A PASS
is an asset/history check, not Unity compilation or headset validation.
