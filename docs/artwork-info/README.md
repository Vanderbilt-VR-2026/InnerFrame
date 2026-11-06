# Issue #14 PR112 review corrections

These changes address Cici's [PR112 review](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/112#issuecomment-6476084361). The author reports running the targeted migration and validators in Unity 6000.3.23f1 on the Mac. The supplied Console screenshots show the serialized dependency check and menu 8 multi-frame follow/collision/recovery/X-reopen checks passing, with zero errors and one expected no-safe-space fixture warning. This executor cannot independently launch Unity or inspect the rendered Gallery. Manual Gallery rendering/input and Quest 3 acceptance remain pending; no headset is currently available. Do not run regeneration menu 1 on the approved assets.

## Reproducing the migration in another checkout

The author has already run these steps in the project being submitted. For a separate checkout only:

1. Preserve local work. Install this review package with its guarded `apply-review.py`, which checks the expected branch and all destination hashes before writing source. No Git staging, commit, fetch, merge or push is performed. Existing `.meta` and approved geometry remain intact.
2. Let Unity 6000.3.23f1 import/compile. It creates `.meta` files for the two new Editor scripts. Review/save any dirty scene manually and exit Prefab Mode.
3. Run **Tools → InnerFrame → Artwork Info → 6. Apply PR112 Review Fixes (No Regeneration)**. It preflights both existing prefabs and the canonical AIC texture, backs up the generated folder outside the project, moves `MuseumGenerated 16` to `Generated` using `AssetDatabase.MoveAsset`, updates existing prefab text and portrait texture using `PrefabUtility`, then validates. It never saves Gallery. A rerun reuses `Generated`; it never creates `Generated 2`. If both folders exist it stops without guessing.
4. Run **7. Validate PR112 Migrated Assets**. It checks Gallery dependency closure for both prefabs and the canonical portrait, TMP font/atlas/material references, scripts and UI raycasters. The submitted tree uses the canonical portrait reference; other generated folders remain untouched.
5. Enter `Generated/ArtworkInfoTestScene.unity` Play Mode and run menu **8**. It runs across real frames with temporary wall colliders and camera at the actual low More-button height, including 0.5, 0.8, 1.2 and 1.8 m. The fixture is destroyed afterward. The author reports this check passed in the submitted Mac project; rerun it when validating a separate checkout. Menu **2** is an additional screen-point behavior check.
6. Test the real Gallery at the same distances. Turn, look down at More, strafe, walk toward the wall, Close/reopen, and verify mouse/Quest controller ray first-hit and readability. The native Gallery FBX importer requests colliders; Unity must confirm the resulting colliders. Do not change the rig or global physics settings.
7. Before commit, inspect the rename (same GUID), changed prefabs, old portrait deletion, canonical AIC files, scripts plus Unity-generated metadata and docs. Run repository CI once a commit is authorized; the pre-existing HEAD CI result does not validate uncommitted files.

## Placement behavior

Only the open detail instance follows the resolved active XR Origin camera in LateUpdate. Preferred distance remains **1.35 m**. Full shell and Canvas bounds are measured once, then tested with a center ray, oriented box sweep, initial/final overlap checks, and the placard's wall-plane fallback. Queries ignore triggers, the panel and the current XR rig; full buffers fail closed. The wall placard never moves and existing More/X lifecycle is retained.

Distance is reduced before a wall, with a 2.5 cm geometric margin. The full box is also constrained in front of the near plane. At close range, full headset pitch can make the 1.428 × 1.248 m panel intersect the wall even with its center in front. In that case pitch/roll are constrained toward upright (yaw still follows), only enough to fit; position remains along current gaze. Distance and full tilt recover gradually when space becomes available. Hard safety corrections take precedence over smoothing. No text, font, panel scale, glass or button layout is changed. Very narrow corners or extreme orientations can still have no safe full-size pose: visuals and clicks are gated until space returns, and a warning explains why. Absolute visibility in every geometry cannot be guaranteed without changing scale or moving off gaze. The close-range angular size and tilt behavior need Quest acceptance.

Close disables the GameObject and stops tracking. Reopen re-resolves current eyes and resets clearance. Existing desktop input and both UI raycasters are retained. ENTER remains an unbound extension event; the painting-entry transition is unchanged.

## Image, text and paths

- Old Figma `Portrait.png` provenance is unverified. New generator/migration use the **existing main** `Assets/VanGogh/Art/VG_SelfPortrait_1887.jpg` and its original GUID. This is AIC Self-Portrait, 1887, accession1954.326, recorded as CC0 in main's asset inventory. See `docs/ASSETS.md` and `Assets/ArtworkInfo/Design/NOTICE.txt`. No new source licence claim is made for the old file.
- The image occupies the same RectTransform and uses a centered UV crop to preserve aspect ratio; shared texture importer settings are untouched.
- Existing wall metadata becomes exactly **73.6 × 92.3 cm**. Any old exact dimension text found in the detail prefab is replaced too; the baseline detail has no dimension line, so no extra line is invented.
- The single approved folder becomes `Assets/ArtworkInfo/Generated`. Generator now refuses to overwrite approved assets or create another numbered version. Migration, not regeneration, updates existing prefabs.
- Other generated folders are left alone. If they reference the old portrait it is retained locally and reported; a clean publication tree must exclude the old file after dependencies are addressed. Removing the current file cannot erase its earlier Git history; merge strategy requires Cici's review.

## Verification status

The author reports Unity compilation/import succeeded sufficiently to run the review menus. Menu 7 reported serialized dependencies passed; menu 8 reported multi-frame following, distances, low More view, wall corners, invisible-input gate, recovery and X/reopen passed. The Console screenshot shows no errors and one expected warning from the unsafe-placement fixture. These are author-run Editor checks, not independently reproduced here. Actual rendered Gallery inspection, manual mouse/controller interaction, a clean-checkout Unity build, and Quest 3 Build And Run remain **not verified**. See `REVIEW_FIXES.md` for the evidence and limits.

---

## Historical submitted version at 910c82f (superseded by the review fixes)

# Artwork information — Issue #14

## Open the checked-in feature

Use Unity **6000.3.23f1** and open `Assets/Scenes/Gallery.unity`.
The submitted scene uses the existing Gallery XR rig. It references the
`ArtworkWallPlacard.prefab` and `ArtworkDetailPanel.prefab` in
`Assets/ArtworkInfo/MuseumGenerated 16/`. Do not regenerate assets to open or
review the feature. Preserve their `.meta` files and GUIDs.

The wall label is a white, serif museum label with a headphone/12 graphic,
metadata, divider, description and visible More button. Its generated body is
0.22 × 0.3055556 × 0.003 m. The Gallery instance overrides its front material
with `Assets/ArtworkInfo/GalleryPreviewWhite.mat` (URP Unlit white), so gallery
lighting does not tint the board gray. The material's Preview name is historical;
it is a production scene dependency. This trades lit surface shading for stable
white readability; the physical geometry is retained.

The detail panel preserves the user's Figma 2:37 composition: 476 × 416 design
units mapped to a 1428 × 1248 World Space Canvas at 0.001 m/unit, two columns,
separate portrait image, editable TMP text, ENTER and X. The thin rounded shell
uses a lightweight translucent URP glass approximation, not optical blur.
Cormorant Garamond Medium and Inter Regular use baked SDF assets; the fonts,
licenses, design inputs and TMP Essential Resources are included.

## Interaction and placement

- Point the existing controller UI ray at **More** and activate UI Press/Trigger.
- `ArtworkInfoDisplay` uses `scenePanel` if assigned, otherwise creates the
  referenced detail prefab once and reuses it for subsequent openings. The
  Gallery placard has `scenePanel = None` and uses the prefab path.
- `viewingCamera = None` is supported. Resolution prefers an explicitly assigned
  active XR Origin camera, then the unique active `XROrigin.Camera`. Multiple
  candidates produce a diagnostic instead of selecting arbitrarily. While XR is
  enabled, the resolver does not fall back to a desktop camera. Outside XR,
  an active assigned camera or `Camera.main` supports desktop tests.
- The panel opens **1.35 m** along the camera's full forward direction and uses
  its rotation. Its local -Z front faces the viewer. It is detached from the rig.
- **Final submitted behavior is world-fixed after opening, not head-following.**
  `ArtworkHeadFollow` retains its historical name, but calls `RefreshPose` only
  from `Begin`; it has no Update/LateUpdate/before-render tracking callback.
- **X** hides the panel. Reopening places the same panel in front of the current
  camera pose. The wall placard never moves with the player.
- ENTER remains the existing separate event hook. An unbound hook must not be
  described as working painting navigation. The existing painting-entry
  interaction is unchanged by this submission.

Both UI raycasters and the existing Canvas/glass sorting are preserved. No
project-wide rendering changes, XR action changes, or controller bindings are
introduced. Panel placement is not collision-aware: a nearby wall can obscure a
panel opened 1.35 m away. This submission does not add an always-on-top mode or
extend the controller ray reach.

## Desktop and device checks

The retained isolated scene is
`Assets/ArtworkInfo/MuseumGenerated 16/ArtworkInfoTestScene.unity`.
Enter Play Mode; release RMB so the cursor is visible; click More and X.
Hold RMB with WASD/QE to navigate, Shift to move faster, Esc to release the
cursor, and Home to reset the camera. Move after opening to verify world lock;
close and reopen from another direction to verify placement and reuse.

The generator's menu **2. Check Behavior in Play Mode** exercises synthetic UI
raycasts and button handlers, placement, reuse, layout and an immediate
camera-motion assertion. It is not a physical mouse/controller test and does not
prove multi-frame world stability. Observe several frames manually.

For Quest, build the existing Gallery using the project's Android/OpenXR setup.
Verify immersive stereo, More hover/Trigger, readable front-facing text, X,
repeated opening from different poses, fixed placement while turning, and
painting-entry behavior. Check Console/player logs. Keep other team features and
rig/settings unchanged. An independent tester is still required by Issue #14.

## Source maintenance

Unity compiles `Assets/ArtworkInfo/{Runtime,Editor}`. Matching source copies live
under `scripts/artwork-info/{Runtime,Editor}`. The uploaded snapshot had three
stale source mirrors; this submission copies the installed Unity versions into
those mirrors (ArtworkInfoDisplay, ArtworkInfoTools and ArtworkFigmaPanel).
Installed scripts, scene, prefabs, materials and metadata are otherwise preserved
from the supplied archive.

`bash scripts/artwork-info/install-source.sh --update-source` is a guarded source
update, not a required setup step for this now self-contained checkout. It refuses
unrecognized local edits and preserves serialized assets and metadata. Its final
generation suggestion applies to generator development, not review of the
checked-in Gallery. No private download is required for the included inputs.

Menu 1 generates a new unique standalone output folder. Do not run it to replace
the reviewed Gallery design. Menu 3 creates an isolated XR test. Menu 4 creates a
Gallery desktop integration copy, and menu 5 changes only that preview's white
surface. These optional tools are retained with their original guards and are
not run by this submission. Failed/old generated outputs and the desktop Gallery
copy are not included; the one reviewed generated folder retains its test scene.

## Evidence and remaining acceptance

- [V — author-reported] Rowling reports building and successfully testing the
  integrated application on Meta Quest 3 with Unity 6000.3.23f1, including More
  opening the Detail Board. No build hash, APK or device log accompanied the
  source archive. This report is not a clean-checkout headset verification.
- [V — source inspection] The submitted installed code is position-once/world-fixed.
  The earlier author summary said head-following; the archive takes precedence for
  documenting implementation. Which APK was tested is not independently established.
- [I — pending] Unity compilation/import from a clean checkout, physical mouse and
  Quest interaction on the submitted commit, X/reopen cycles, comfort/occlusion,
  and independent-user testing. Do not infer these from GENERATION_STATUS.txt.
- [V — source inspection] Gallery also contains an active empty root named
  ArtworkDetailPanel with no children and no Close reference. The placard does not
  reference it; the real panel comes from detailPrefab. It is retained byte-for-byte
  to avoid changing the author's tested scene. Review it in Unity before any cleanup.
- [I — review] Gallery owner Cici should review placement, especially alongside
  the portrait placement proposed in PR #118. No changes to Issue #75 are included.

See `SUBMISSION_REVIEW.md` for packaging checks and exclusions. Third-party sources
and permissions are recorded in `docs/ASSETS.md` and the supplied license files.
